using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Owns every live particle: spawns, ticks, kills and draws them.
    /// Client-side only — every entry point bails on a dedicated server.
    /// </summary>
    public static class PADParticleHandler
    {
        /// <summary>
        /// Hard cap on simultaneous particles. Anything over it is dropped unless the
        /// particle marks itself Important, so a heavy effect degrades instead of
        /// tanking the framerate. Raise it if the mod ever gets a config.
        /// </summary>
        public const int ParticleLimit = 1000;

        private static List<PADParticle> particles;
        private static List<PADParticle> particlesToKill;

        private static Dictionary<Type, int> particleTypes;
        private static Dictionary<int, Texture2D> particleTextures;

        // Sorting into per-blend-mode buckets means one Begin/End per blend mode per
        // frame instead of a graphics state change per particle. This is the whole
        // reason the system stays cheap at high particle counts.
        private static List<PADParticle> batchedAlphaBlend;
        private static List<PADParticle> batchedAdditive;

        public static int ActiveCount => particles?.Count ?? 0;

        public static int FreeSlots => Main.dedServ || particles == null
            ? 0
            : ParticleLimit - particles.Count;

        /// <summary>
        /// The already-loaded texture for a particle type. Use this inside CustomDraw —
        /// it is a dictionary hit, where ModContent.Request would re-resolve the asset
        /// every frame for every particle on screen.
        /// </summary>
        public static Texture2D GetTexture(int type)
        {
            if (Main.dedServ || particleTextures == null)
                return null;

            return particleTextures.TryGetValue(type, out Texture2D texture) ? texture : null;
        }

        internal static void Load()
        {
            particles = new List<PADParticle>();
            particlesToKill = new List<PADParticle>();
            particleTypes = new Dictionary<Type, int>();
            particleTextures = new Dictionary<int, Texture2D>();
            batchedAlphaBlend = new List<PADParticle>();
            batchedAdditive = new List<PADParticle>();
        }

        internal static void Unload()
        {
            particles = null;
            particlesToKill = null;
            particleTypes = null;
            particleTextures = null;
            batchedAlphaBlend = null;
            batchedAdditive = null;
        }

        /// <summary>
        /// Finds every PADParticle subclass in the mod, gives it an int id and preloads
        /// its texture. Call once from a ModSystem's PostSetupContent.
        /// </summary>
        /// <param name="mod">Pass the calling ModSystem's Mod — avoids having to name
        /// the mod class from inside the PisAndDag namespace, which is ambiguous.</param>
        internal static void LoadParticleTextures(Mod mod)
        {
            if (Main.dedServ)
                return;

            Type baseType = typeof(PADParticle);

            foreach (Type type in AssemblyManager.GetLoadableTypes(mod.Code))
            {
                if (!type.IsSubclassOf(baseType) || type.IsAbstract)
                    continue;

                int id = particleTypes.Count;
                particleTypes[type] = id;

                // Build the instance without running a constructor — we only want to
                // read the Texture property, and constructors here expect real spawn
                // arguments. (RuntimeHelpers, not the older FormatterServices, which
                // is obsolete on net8 and would warn.)
                PADParticle probe = (PADParticle)RuntimeHelpers.GetUninitializedObject(type);

                string path = probe.Texture;
                if (string.IsNullOrEmpty(path))
                {
                    // Namespace-as-folder, matching tModLoader's own convention:
                    // PisAndDag.Content.Particles.BloodDroplet -> PisAndDag/Content/Particles/BloodDroplet
                    path = type.Namespace.Replace('.', '/') + "/" + type.Name;
                }

                particleTextures[id] = ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad).Value;
            }
        }

        public static void SpawnParticle(PADParticle particle)
        {
            if (Main.dedServ || Main.gamePaused || particles == null || particle == null)
                return;

            if (particles.Count >= ParticleLimit && !particle.Important)
                return;

            if (!particleTypes.TryGetValue(particle.GetType(), out int type))
                return;   // never registered — a texture is probably missing

            particle.Type = type;
            particles.Add(particle);
        }

        /// <summary>
        /// Queues a particle for removal. Deferred rather than immediate because Update
        /// iterates the list, and you cannot remove from a collection mid-iteration.
        /// </summary>
        public static void RemoveParticle(PADParticle particle)
        {
            if (Main.dedServ || particlesToKill == null)
                return;

            particlesToKill.Add(particle);
        }

        internal static void Update()
        {
            if (Main.dedServ || particles == null)
                return;

            foreach (PADParticle particle in particles)
            {
                if (particle == null)
                    continue;

                particle.Position += particle.Velocity;
                particle.Time++;
                particle.Update();
            }

            particles.RemoveAll(p =>
                p == null ||
                (p.SetLifetime && p.Time >= p.Lifetime) ||
                particlesToKill.Contains(p));

            particlesToKill.Clear();
        }

        internal static void DrawAll(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || particles == null || particles.Count == 0)
                return;

            foreach (PADParticle particle in particles)
            {
                if (particle == null)
                    continue;

                if (particle.UseAdditiveBlend)
                    batchedAdditive.Add(particle);
                else
                    batchedAlphaBlend.Add(particle);
            }

            DrawBucket(spriteBatch, batchedAlphaBlend, BlendState.AlphaBlend);
            DrawBucket(spriteBatch, batchedAdditive, BlendState.Additive);

            batchedAlphaBlend.Clear();
            batchedAdditive.Clear();
        }

        private static void DrawBucket(SpriteBatch spriteBatch, List<PADParticle> bucket, BlendState blend)
        {
            if (bucket.Count == 0)
                return;

            // Assumes no batch is currently open — the draw hook calls this after
            // vanilla has closed its own. If you move the call site, check that.
            spriteBatch.Begin(SpriteSortMode.Deferred, blend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (PADParticle particle in bucket)
            {
                if (particle.UseCustomDraw)
                {
                    particle.CustomDraw(spriteBatch);
                    continue;
                }

                Texture2D texture = particleTextures[particle.Type];
                Rectangle frame = texture.Frame(1, particle.FrameVariants, 0, particle.Variant);

                spriteBatch.Draw(texture, particle.Position - Main.screenPosition, frame,
                    particle.Color, particle.Rotation, frame.Size() * 0.5f,
                    particle.Scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }
    }
}
