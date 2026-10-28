using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Pseudo
{
    /// <summary>
    /// A pseudo-NPC, by request ("Create Pseudo NPC class that behaves like NPCs but doesnt affect max
    /// NPC; somewhat like ambiance"): a creature kept, moved and drawn the way the game keeps its own
    /// (a kind, an AI with its slots, a place, a speed, a turn, a scale, how faded it is, its age), but
    /// none of the game's: it is not in Main.npc, so it is not counted in the game's 200, nor against its
    /// spawn rates and limits, and nothing in the game can find it, hit it or catch it. For the looks
    /// only, on each player's own machine (none on a server, and nothing of them is sent).
    ///
    /// The kinds are <see cref="ModPseudoNPC"/>s, loaded as ModNPCs are; the creatures are kept in one
    /// array of these (<see cref="PseudoNPCs.Pool"/>), as the game keeps its own.
    /// </summary>
    public struct PseudoNPC
    {
        public bool Active;

        /// <summary>Its kind: <see cref="ModPseudoNPC.Type"/>.</summary>
        public int Type;

        /// <summary>Its place in <see cref="PseudoNPCs.Pool"/>.</summary>
        public int WhoAmI;

        /// <summary>Its middle, in the world's pixels; and how it goes, pixels a tick (added to its middle after its AI).</summary>
        public Vector2 Center;
        public Vector2 Velocity;

        public float Rotation;
        public float Scale;

        /// <summary>1 seen, 0 not.</summary>
        public float Opacity;

        /// <summary>1 facing right, -1 left.</summary>
        public int Direction;

        /// <summary>Ticks since it came.</summary>
        public int Age;

        /// <summary>Which of its kind's looks it has (a colour, a frame): its kind's to say.</summary>
        public int Variant;

        /// <summary>The group its kind keeps it in (a school, a flock), and its place in it; -1 none.</summary>
        public int Group;
        public int Slot;

        /// <summary>Its kind's to use, as an NPC's ai and localAI are.</summary>
        public float Ai0, Ai1, Ai2, Ai3;
        public float LocalAi0, LocalAi1, LocalAi2, LocalAi3;
    }

    /// <summary>
    /// A kind of <see cref="PseudoNPC"/>: what a ModNPC is to an NPC. Loaded by tModLoader as any ModType
    /// is; its picture by its namespace and name, as a ModNPC's (or <see cref="Texture"/>).
    /// </summary>
    public abstract class ModPseudoNPC : ModType
    {
        public int Type { get; internal set; }

        public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');

        public Asset<Texture2D> TextureAsset { get; private set; }

        /// <summary>The most of this kind there can be at once.</summary>
        public virtual int Most => 400;

        /// <summary>Drawn sampled smoothly, to be turned and scaled freely (else pixel for pixel, as the game draws its own).</summary>
        public virtual bool Smooth => false;

        /// <summary>Taken away when this far from the player on this machine (pixels): out of sight.</summary>
        public virtual float DespawnDistance => 3600f;

        protected sealed override void Register()
        {
            ModTypeLookup<ModPseudoNPC>.Register(this);
            Type = PseudoNPCs.Add(this);
        }

        public sealed override void SetupContent()
        {
            if (!Main.dedServ)
                TextureAsset = ModContent.Request<Texture2D>(Texture);

            SetStaticDefaults();
        }

        /// <summary>One of this kind has come: its own looks and slots.</summary>
        public virtual void OnSpawn(ref PseudoNPC npc)
        {
        }

        /// <summary>Once a tick, before any of this kind moves.</summary>
        public virtual void PreUpdateAll()
        {
        }

        /// <summary>Each tick: its speed and its turn (its speed is added to its place after this).</summary>
        public virtual void AI(ref PseudoNPC npc)
        {
        }

        /// <summary>Whether it stays, over its distance (<see cref="DespawnDistance"/>): false takes it away.</summary>
        public virtual bool CheckActive(ref PseudoNPC npc) => true;

        /// <summary>Its picture where it is (the batch begun in the world's pixels, with the camera's zoom): by default its whole picture, turned, in the light where it is.</summary>
        public virtual void Draw(SpriteBatch batch, ref PseudoNPC npc, Vector2 screenPosition)
        {
            if (TextureAsset == null || !TextureAsset.IsLoaded)
                return;

            Texture2D picture = TextureAsset.Value;
            Color light = Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f));

            batch.Draw(picture, npc.Center - screenPosition, null, light * npc.Opacity, npc.Rotation, picture.Size() * 0.5f, npc.Scale,
                npc.Direction > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        /// <summary>How many times its own light is drawn over (<see cref="DrawGlow"/>): each pass for all of every kind before the next, so a kind drawing two pictures draws each in a run of its own, in one go.</summary>
        public virtual int GlowPasses => 0;

        /// <summary>Its own light, added to what is there (the batch begun for light: a colour with no alpha adds), in this pass (<see cref="GlowPasses"/>). Drawn by the place's light layer: in the Vast Ocean, over its water (VastOceanSky).</summary>
        public virtual void DrawGlow(SpriteBatch batch, ref PseudoNPC npc, Vector2 screenPosition, int pass)
        {
        }
    }

    /// <summary>
    /// Keeps the pseudo-NPCs (<see cref="PseudoNPC"/>): makes them (<see cref="New"/>), moves them each
    /// tick (their kinds' AI, then their speed), takes them away out of sight or when their kind says,
    /// and draws them with the world, after its tiles and its creatures, before the players and the
    /// water over everything (so a thing in the water is seen through it).
    /// </summary>
    public class PseudoNPCs : ModSystem
    {
        public const int Max = 1600;

        public static readonly PseudoNPC[] Pool = new PseudoNPC[Max];

        /// <summary>How many there are; and one past the highest place in use.</summary>
        public static int Count { get; private set; }
        private static int highest;

        /// <summary>Where to look for a free place first.</summary>
        private static int nextFree;

        private static readonly List<ModPseudoNPC> kinds = new List<ModPseudoNPC>();
        private static int[] counts = Array.Empty<int>();

        internal static int Add(ModPseudoNPC kind)
        {
            kinds.Add(kind);
            Array.Resize(ref counts, kinds.Count);
            return kinds.Count - 1;
        }

        public static ModPseudoNPC Kind(int type) => kinds[type];

        public static int CountOf(int type) => type >= 0 && type < counts.Length ? counts[type] : 0;

        public override void Unload()
        {
            kinds.Clear();
            counts = Array.Empty<int>();
            Clear();
        }

        public override void OnWorldUnload() => Clear();

        public static void Clear()
        {
            Array.Clear(Pool);
            Array.Clear(counts);
            Count = 0;
            highest = 0;
            nextFree = 0;
        }

        /// <summary>One of this kind, here, going so: its place in <see cref="Pool"/>, or -1 (none made on a server, or with its kind at its most, or the pool full).</summary>
        public static int New(int type, Vector2 at, Vector2 velocity, int group = -1, int slot = 0)
        {
            if (Main.dedServ || type < 0 || type >= kinds.Count || counts[type] >= kinds[type].Most || Count >= Max)
                return -1;

            int i = nextFree;

            while (Pool[i].Active)
                i = (i + 1) % Max;

            nextFree = (i + 1) % Max;

            Pool[i] = new PseudoNPC
            {
                Active = true,
                Type = type,
                WhoAmI = i,
                Center = at,
                Velocity = velocity,
                Scale = 1f,
                Opacity = 1f,
                Direction = velocity.X < 0f ? -1 : 1,
                Group = group,
                Slot = slot,
            };

            Count++;
            counts[type]++;
            highest = Math.Max(highest, i + 1);
            kinds[type].OnSpawn(ref Pool[i]);
            return i;
        }

        /// <summary>Takes it away.</summary>
        public static void Kill(int i)
        {
            if (i < 0 || i >= Max || !Pool[i].Active)
                return;

            Pool[i].Active = false;
            Count--;
            counts[Pool[i].Type]--;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Count == 0)
                return;

            for (int k = 0; k < kinds.Count; k++)
            {
                if (counts[k] > 0)
                    kinds[k].PreUpdateAll();
            }

            Vector2 near = Main.LocalPlayer.Center;

            for (int i = 0; i < highest; i++)
            {
                ref PseudoNPC npc = ref Pool[i];

                if (!npc.Active)
                    continue;

                ModPseudoNPC kind = kinds[npc.Type];

                kind.AI(ref npc);
                npc.Center += npc.Velocity;
                npc.Age++;

                float far = kind.DespawnDistance;

                if (!kind.CheckActive(ref npc) || Vector2.DistanceSquared(npc.Center, near) > far * far)
                    Kill(i);
            }

            while (highest > 0 && !Pool[highest - 1].Active)
                highest--;
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Count == 0)
                return;

            // Those turned and scaled freely, sampled smoothly; then those drawn pixel for pixel.
            for (int pass = 0; pass < 2; pass++)
            {
                bool smooth = pass == 0;

                if (!AnyDrawn(smooth))
                    continue;

                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, smooth ? SamplerState.LinearClamp : Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

                try
                {
                    Draw(Main.spriteBatch, smooth, false);
                }
                finally
                {
                    Main.spriteBatch.End();
                }
            }
        }

        private static bool AnyDrawn(bool smooth)
        {
            for (int k = 0; k < kinds.Count; k++)
            {
                if (counts[k] > 0 && kinds[k].Smooth == smooth)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Their own light, each kind's (<see cref="ModPseudoNPC.DrawGlow"/>), into a batch begun for
        /// light by the place's light layer (in the Vast Ocean, VastOceanSky, over its water).
        /// </summary>
        public static void DrawGlow(SpriteBatch batch)
        {
            if (Main.dedServ || Count == 0)
                return;

            int passes = 0;

            for (int k = 0; k < kinds.Count; k++)
            {
                if (counts[k] > 0)
                    passes = Math.Max(passes, kinds[k].GlowPasses);
            }

            for (int pass = 0; pass < passes; pass++)
                Draw(batch, false, true, pass);
        }

        private static void Draw(SpriteBatch batch, bool smooth, bool glow, int pass = 0)
        {
            Vector2 screen = Main.screenPosition;
            Rectangle view = new Rectangle((int)screen.X - 64, (int)screen.Y - 64, Main.screenWidth + 128, Main.screenHeight + 128);

            for (int i = 0; i < highest; i++)
            {
                ref PseudoNPC npc = ref Pool[i];

                if (!npc.Active || !view.Contains((int)npc.Center.X, (int)npc.Center.Y))
                    continue;

                ModPseudoNPC kind = kinds[npc.Type];

                if (glow)
                {
                    if (pass < kind.GlowPasses)
                        kind.DrawGlow(batch, ref npc, screen, pass);
                }
                else if (kind.Smooth == smooth)
                    kind.Draw(batch, ref npc, screen);
            }
        }
    }
}
