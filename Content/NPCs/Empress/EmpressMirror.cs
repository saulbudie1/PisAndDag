using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// The world as a broken mirror (EmpressOfLight.Crack; and the mirror barrier's crack, EmpressOfLight.Shield): each shard of a crack drawn again over the world with the
    /// world itself in it, moved and tipped as the shard is, the light its glass catches over it, the world gone dark
    /// behind it as it flies; then the cracks' light over them.
    ///
    /// How the world is had: the game's own capture of the screen, the one its screen effects draw from (the world is
    /// drawn into Main.screenTarget, then the game lays its filters over it and puts it on the screen). The capture is
    /// only made while something listens: EmpressWarp listens while a crack is up (as while an act of hers bends the
    /// world), sets the world aside as the capture ends, and draws it back with its bends, then these shards over them,
    /// so they are never drawn over; the game's filters (a Blood Moon's red, the world turned over for one who walks
    /// upside down) then go over the shards as over the rest. The shards are triangles through vanilla's BasicEffect,
    /// the world set aside their texture: no shader of the mod's own.
    ///
    /// With lighting that makes no capture (Retro, Trippy: the world drawn straight to the screen) there is nothing to
    /// move: the cracks are drawn with the world instead (EmpressOfLight.DrawCrackOver), the shards unmoved.
    /// </summary>
    public class EmpressMirror : ModSystem
    {
        /// <summary>A crack is up somewhere: the shards are wanted (EmpressWarp listens for them).</summary>
        internal static bool Wanted { get; private set; }

        private static BasicEffect effect;
        private static VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[4096];
        private static VertexPositionColor[] light = new VertexPositionColor[2048];

        /// <summary>Will the shards and their cracks be drawn over the screen this frame (and so not with the world)?</summary>
        internal static bool WillDraw => Wanted && EmpressWarp.Listening && !Main.drawToScreen && Lighting.NotRetro && !Main.mapFullscreen;

        /// <summary>World pixels from this machine's player within which a crack is listened for: further, it is not seen.</summary>
        private const float SeenFrom = 4000f;

        /// <summary>Before EmpressWarp decides whether to listen (it runs after this): is a crack up near enough to be seen?</summary>
        public override void PreUpdateEntities()
        {
            if (Main.dedServ)
                return;

            bool want = false;

            for (int i = 0; i < Main.maxNPCs && !want; i++)
            {
                EmpressOfLight her = EmpressOfLight.Of(i);
                want = her != null && (her.Mirroring || her.Barrier) && Main.npc[i].WithinRange(Main.LocalPlayer.Center, SeenFrom);
            }

            Wanted = want;
        }

        public override void OnWorldUnload() => Wanted = false;

        public override void Unload()
        {
            Wanted = false;

            BasicEffect old = effect;
            effect = null;

            if (old != null)
                Main.QueueMainThreadAction(old.Dispose);
        }

        /// <summary>
        /// The world drawn back into the capture and bent (EmpressWarp, which calls this as the capture ends, the capture
        /// its target): the shards over it, out of <paramref name="world"/> (the world as drawn, set aside), then their cracks.
        /// </summary>
        internal static void Draw(Texture2D world)
        {
            if (!Wanted || Main.gameMenu || world == null || world.IsDisposed)
                return;

            try
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    EmpressOfLight her = EmpressOfLight.Of(i);

                    if (her == null)
                        continue;

                    if (her.Barrier)
                    {
                        DrawBarrier(her, Main.npc[i], world);
                        continue;
                    }

                    if (!her.Mirroring)
                        continue;

                    for (int k = 0; k < her.CrackCount; k++)
                        DrawShards(her, k, world);

                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                    try
                    {
                        // Its cracks and its light, over the shards (EmpressOfLight.DrawCrackLight): no lines round the
                        // shards themselves (by request, "remove these sectioned outlines").
                        for (int k = 0; k < her.CrackCount; k++)
                            her.DrawCrackLight(Main.spriteBatch, k);
                    }
                    finally
                    {
                        Main.spriteBatch.End();
                    }
                }
            }
            catch (Exception e)
            {
                ModContent.GetInstance<EmpressOfLightSystem>()?.Mod.Logger.Warn("Empress mirror: " + e.Message);
            }
        }

        /// <summary>
        /// The mirror barrier's crack (EmpressOfLight.Shield, EmpressBarrier): each shard holding the world from a little way
        /// off, seen a little nearer, washed in one of her colours; inside it, twice, a smaller copy of itself holding the
        /// world from further off still and nearer still, more coloured (a mirror in a mirror); the light its glass catches
        /// over each; as it bursts, each flying with the world still in it, going thin. Then its lines over them.
        /// </summary>
        private static void DrawBarrier(EmpressOfLight her, NPC npc, Texture2D world)
        {
            her.BuildBarrier(npc);
            var shards = EmpressOfLight.BarrierShards;
            var corners = EmpressOfLight.BarrierCorners;
            float shown = her.BarrierShown;

            if (shards.Count > 0 && shown > 0.003f)
            {
                Vector2 size = new Vector2(world.Width, world.Height);
                float time = (float)Main.timeForVisualEffects;
                float flight = her.BarrierFlight;
                int count = 0;
                int lit = 0;

                foreach (EmpressOfLight.Shard shard in shards)
                {
                    for (int depth = 0; depth < 3; depth++)
                    {
                        float scale = 1f - 0.27f * depth;
                        float zoom = shard.Zoom * (1f + 0.14f * depth);
                        Vector2 shift = shard.Shift * (1f + 0.9f * depth);
                        float wash = 0.12f + 0.12f * depth + 0.06f * (float)Math.Sin(time * 0.04f + shard.Hue * 7f);
                        float alpha = shown * (1f - flight * 0.5f);
                        Color tint = Color.Lerp(Color.White, EmpressArt.Prism(shard.Hue + depth * 0.2f), wash) * alpha;
                        float glintWay = time * 0.035f + shard.Hue * 9f + depth;
                        Color glass = EmpressArt.Prism(shard.Hue + time / 200f);
                        float sheen = (0.1f + 0.05f * depth + 0.2f * flight) * shown;

                        Vector2 placed = OnScreen(EmpressOfLight.ShardCorner(shard, shard.Middle));
                        Vector2 source = OnScreen(shard.Middle + shift);

                        for (int c = 0; c < shard.Count && count + 3 <= vertices.Length; c++)
                        {
                            Vector2 a = (corners[shard.Start + c] - shard.Middle) * scale;
                            Vector2 b = (corners[shard.Start + (c + 1) % shard.Count] - shard.Middle) * scale;

                            Vector2 pa = OnScreen(EmpressOfLight.ShardCorner(shard, shard.Middle + a));
                            Vector2 pb = OnScreen(EmpressOfLight.ShardCorner(shard, shard.Middle + b));
                            Vector2 sa = OnScreen(shard.Middle + shift + a / zoom);
                            Vector2 sb = OnScreen(shard.Middle + shift + b / zoom);

                            vertices[count++] = new VertexPositionColorTexture(new Vector3(placed, 0f), tint, source / size);
                            vertices[count++] = new VertexPositionColorTexture(new Vector3(pa, 0f), tint, sa / size);
                            vertices[count++] = new VertexPositionColorTexture(new Vector3(pb, 0f), tint, sb / size);

                            if (lit + 3 <= light.Length)
                            {
                                light[lit++] = new VertexPositionColor(new Vector3(placed, 0f), Color.Transparent);
                                light[lit++] = new VertexPositionColor(new Vector3(pa, 0f), EmpressArt.Additive(glass, sheen * Catch(a.ToRotation() + shard.Spin, glintWay) + 0.035f * shown));
                                light[lit++] = new VertexPositionColor(new Vector3(pb, 0f), EmpressArt.Additive(glass, sheen * Catch(b.ToRotation() + shard.Spin, glintWay) + 0.035f * shown));
                            }
                        }
                    }
                }

                Flush(world, count, lit);
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            try
            {
                her.DrawBarrierLines(npc, Main.spriteBatch, true);
            }
            finally
            {
                Main.spriteBatch.End();
            }
        }

        /// <summary>The triangles laid out drawn: the world's through the capture, then the light's, added.</summary>
        private static void Flush(Texture2D world, int count, int lit)
        {
            if (count < 3)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;
            effect ??= new BasicEffect(device) { VertexColorEnabled = true };

            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, world.Width, world.Height, 0f, 0f, 1f);
            effect.TextureEnabled = true;
            effect.Texture = world;

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, count / 3);
            }

            if (lit >= 3)
            {
                effect.TextureEnabled = false;
                effect.Texture = null;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, light, 0, lit / 3);
                }
            }

            device.Textures[0] = null;
        }

        /// <summary>A world point on the screen, as the world was drawn this frame (its zoom and all).</summary>
        private static Vector2 OnScreen(Vector2 world) => Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);

        /// <summary>
        /// One crack's shards, each a fan of triangles drawn where the shard has moved to, holding the world from where it
        /// was (seen nearer or further as the shard asks); behind them as they fly, the world where they were gone dark;
        /// over them, the light the glass catches.
        /// </summary>
        private static void DrawShards(EmpressOfLight her, int which, Texture2D world)
        {
            CrackNet net = CrackNet.Of(her.CrackSeed, which);
            Vector2 centre = her.CrackPoint(which);
            // The world whole until it bursts (by request, "a pure clean glass break": the shards no longer shift and tip
            // as it strains); then thrown, clear.
            const float strain = 0f;
            float flight = her.CrackFlight;

            if (flight <= 0.001f)
                return;

            Vector2 size = new Vector2(world.Width, world.Height);
            int rays = EmpressOfLight.CrackRays;
            int rings = EmpressOfLight.CrackRings.Length;
            int count = 0;
            int lit = 0;
            float time = (float)Main.timeForVisualEffects;

            Span<Vector2> corners = stackalloc Vector2[4];

            // Where the shards were, as they fly: the world there gone dark, a little of her colour at each heart.
            float hole = Math.Min(1f, flight * 2.5f);

            if (hole > 0.001f)
            {
                Color edge = Color.Lerp(Color.White, new Color(22, 12, 40), 0.85f * hole);
                Color heart = Color.Lerp(Color.White, new Color(84, 44, 110), 0.85f * hole);

                for (int i = 0; i < rays; i++)
                {
                    for (int j = 0; j < rings - 1; j++)
                    {
                        int n = net.Cell(i, j, corners);
                        Vector2 middle = Middle(corners, n);
                        Vector2 at = OnScreen(centre + middle);

                        for (int c = 0; c < n && count + 3 <= vertices.Length; c++)
                        {
                            Vector2 sa = OnScreen(centre + corners[c]);
                            Vector2 sb = OnScreen(centre + corners[(c + 1) % n]);

                            vertices[count++] = new VertexPositionColorTexture(new Vector3(at, 0f), heart, at / size);
                            vertices[count++] = new VertexPositionColorTexture(new Vector3(sa, 0f), edge, sa / size);
                            vertices[count++] = new VertexPositionColorTexture(new Vector3(sb, 0f), edge, sb / size);
                        }
                    }
                }
            }

            float sheen = 0.09f * strain + 0.16f * flight;

            for (int i = 0; i < rays; i++)
            {
                for (int j = 0; j < rings - 1; j++)
                {
                    int n = net.Cell(i, j, corners);
                    Vector2 middle = Middle(corners, n);

                    net.Moved(i, j, middle, strain, flight, time, out Vector2 shift, out float turn, out float zoom);

                    // Clear glass, no wash of colour on it (each pane its own colour read as a section); going thin as it flies.
                    Color tint = Color.White * (1f - flight * 0.35f);

                    // The glint each pane catches, going round it as it moves: white, a little of her colours in it.
                    float glintWay = time * 0.035f + i * 0.9f + j * 1.3f;
                    Color glass = Color.Lerp(Color.White, EmpressArt.Prism(i * 0.13f + j * 0.29f + time / 200f), 0.25f);

                    Vector2 source = OnScreen(centre + middle);
                    Vector2 placed = OnScreen(centre + shift + middle);

                    for (int c = 0; c < n; c++)
                    {
                        if (count + 3 > vertices.Length)
                            break;

                        Vector2 a = corners[c] - middle;
                        Vector2 b = corners[(c + 1) % n] - middle;

                        Vector2 sa = OnScreen(centre + middle + a / zoom);
                        Vector2 sb = OnScreen(centre + middle + b / zoom);
                        Vector2 pa = OnScreen(centre + shift + middle + a.RotatedBy(turn));
                        Vector2 pb = OnScreen(centre + shift + middle + b.RotatedBy(turn));

                        vertices[count++] = new VertexPositionColorTexture(new Vector3(placed, 0f), tint, source / size);
                        vertices[count++] = new VertexPositionColorTexture(new Vector3(pa, 0f), tint, sa / size);
                        vertices[count++] = new VertexPositionColorTexture(new Vector3(pb, 0f), tint, sb / size);

                        if (sheen > 0.003f && lit + 3 <= light.Length)
                        {
                            float catchA = Catch(a.ToRotation() + turn, glintWay);
                            float catchB = Catch(b.ToRotation() + turn, glintWay);

                            light[lit++] = new VertexPositionColor(new Vector3(placed, 0f), Color.Transparent);
                            light[lit++] = new VertexPositionColor(new Vector3(pa, 0f), EmpressArt.Additive(glass, sheen * catchA));
                            light[lit++] = new VertexPositionColor(new Vector3(pb, 0f), EmpressArt.Additive(glass, sheen * catchB));
                        }
                    }
                }
            }

            if (count < 3)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;
            effect ??= new BasicEffect(device) { VertexColorEnabled = true };

            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, world.Width, world.Height, 0f, 0f, 1f);
            effect.TextureEnabled = true;
            effect.Texture = world;

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, count / 3);
            }

            // The light on the glass: colour alone, added (premultiplied, no alpha).
            if (lit >= 3)
            {
                effect.TextureEnabled = false;
                effect.Texture = null;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, light, 0, lit / 3);
                }
            }

            device.Textures[0] = null;
        }

        private static Vector2 Middle(Span<Vector2> corners, int n)
        {
            Vector2 middle = Vector2.Zero;

            for (int c = 0; c < n; c++)
                middle += corners[c];

            return middle / n;
        }

        /// <summary>How much of the glint a corner, this way from its pane's middle, catches: a narrow lobe going round.</summary>
        private static float Catch(float way, float glintWay)
        {
            float k = Math.Max(0f, (float)Math.Cos(way - glintWay));
            return k * k * k;
        }
    }
}
