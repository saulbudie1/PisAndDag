using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// The Gilded Knight painted into pictures of his own, once a frame, before anything is drawn
    /// to the screen (Main.OnPreDraw, where the game makes its own; as HoneyGlint does): every
    /// piece of him in its order (KnightRig.Build), his cape, his tabard's two flaps and his
    /// plume laid between them as strips along their chains (KnightCloth), and under each near
    /// piece (the near arm and its pauldron, the helm, a blade in the near hand) a shadow on what
    /// is already there and nowhere else: a blend that darkens what is under it and leaves the
    /// empty air empty, so the shadow keeps to his shape. Side on, as the art is; his turning is
    /// the drawing's, which narrows the picture as it lays it down.
    ///
    /// A second picture holds only his blades' light: every piece in black, in the same order,
    /// and each blade's light added after its blade, so what is in front of a blade hides its
    /// light as it hides the blade. Drawn as light over the world, the light is not dimmed by
    /// the dark the way he is.
    ///
    /// Client only. A fault turns it off for the session, logs once, and he is drawn plainly.
    /// </summary>
    public class KnightCanvas : ModSystem
    {
        /// <summary>His pictures' size, and where the ground between his feet is on them.</summary>
        public const int Width = 400;
        public const int Height = 320;
        public static readonly Vector2 Origin = new Vector2(200f, 260f);

        /// <summary>The most of the picture under his feet there is (a blade driven in, a leg let down in the air).</summary>
        public const int Below = Height - 260;

        /// <summary>Under the near pieces: how far the shadow is laid off them (light from the upper left), and how dark.</summary>
        private static readonly Vector2 ShadowOff = new Vector2(1.6f, 1.2f);
        private const float ShadowDark = 0.42f;

        /// <summary>
        /// Darkens what is drawn over by the picture's alpha and keeps what was there's alpha:
        /// a shadow that falls only on him. (The colour drawn is black; what is added is nothing.)
        /// </summary>
        private static readonly BlendState Darken = new BlendState
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add,
        };

        private sealed class Canvas
        {
            public RenderTarget2D Body;
            public RenderTarget2D Light;
            public uint Painted;
        }

        private static readonly Dictionary<int, Canvas> canvases = new Dictionary<int, Canvas>();
        private static readonly Dictionary<string, Asset<Texture2D>> textures = new Dictionary<string, Asset<Texture2D>>();
        private static readonly List<KnightRig.Part> parts = new List<KnightRig.Part>(KnightRig.Pieces);

        private static BasicEffect effect;
        private static VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[64];
        private static Vector2[] edgeA = new Vector2[16];
        private static Vector2[] edgeB = new Vector2[16];

        /// <summary>The frame being painted (Main.GameUpdateCount is the update's; this counts draws).</summary>
        private static uint frame;

        private static bool broken;

        public override void Load()
        {
            if (!Main.dedServ)
                Main.OnPreDraw += Render;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                Main.OnPreDraw -= Render;

            var old = new List<Canvas>(canvases.Values);
            BasicEffect oldEffect = effect;

            canvases.Clear();
            textures.Clear();
            effect = null;
            broken = false;

            if (old.Count > 0 || oldEffect != null)
            {
                Main.QueueMainThreadAction(() =>
                {
                    foreach (Canvas c in old)
                    {
                        c.Body?.Dispose();
                        c.Light?.Dispose();
                    }

                    oldEffect?.Dispose();
                });
            }
        }

        public override void OnWorldUnload()
        {
            // The pictures are kept for the next knight; nothing of this one is shown again.
            foreach (Canvas c in canvases.Values)
                c.Painted = 0;
        }

        // ============================================================
        // FOR THE DRAWING
        // ============================================================

        /// <summary>His body's picture for this draw, or null if it was not painted (he is drawn plainly then).</summary>
        public static Texture2D Body(int whoAmI) => canvases.TryGetValue(whoAmI, out Canvas c) && c.Painted == frame && frame != 0 && !c.Body.IsDisposed ? c.Body : null;

        /// <summary>His blades' light for this draw, or null.</summary>
        public static Texture2D Light(int whoAmI) => canvases.TryGetValue(whoAmI, out Canvas c) && c.Painted == frame && frame != 0 && !c.Light.IsDisposed ? c.Light : null;

        /// <summary>A picture of his, by piece.</summary>
        public static Texture2D Piece(int piece) => Tex(KnightRig.Folder + KnightRig.Names[piece]);

        /// <summary>A picture by its path, loaded the first time it is asked for (on the main thread: the drawing's), or null if it will not load.</summary>
        public static Texture2D Tex(string path)
        {
            if (Main.dedServ)
                return null;

            if (!textures.TryGetValue(path, out Asset<Texture2D> asset))
            {
                // A picture that is not there is drawn as nothing, rather than stopping his drawing.
                if (!ModContent.RequestIfExists(path, out asset, AssetRequestMode.ImmediateLoad))
                    asset = null;

                textures[path] = asset;
            }

            return asset != null && asset.IsLoaded ? asset.Value : null;
        }

        // ============================================================
        // PAINTING
        // ============================================================

        private static void Render(GameTime gameTime)
        {
            frame++;

            if (frame == 0)
                frame = 1;

            if (broken || Main.gameMenu)
                return;

            Rectangle screen = new Rectangle((int)Main.screenPosition.X - 600, (int)Main.screenPosition.Y - 600, Main.screenWidth + 1200, Main.screenHeight + 1200);
            int type = ModContent.NPCType<GildedKnight>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.type != type || npc.ModNPC is not GildedKnight knight || !screen.Contains(knight.Feet.ToPoint()))
                    continue;

                Paint(knight);

                if (broken)
                    return;
            }
        }

        /// <summary>Both of his pictures, for this draw.</summary>
        private static void Paint(GildedKnight knight)
        {
            GraphicsDevice device = Main.instance.GraphicsDevice;
            SpriteBatch batch = Main.spriteBatch;
            RenderTargetBinding[] previous = device.GetRenderTargets();
            bool open = false;

            try
            {
                Canvas canvas = Ensure(device, knight.NPC.whoAmI);

                device.SetRenderTarget(canvas.Body);
                device.Clear(Color.Transparent);
                Figure(knight, batch, device, false, ref open);

                device.SetRenderTarget(canvas.Light);
                device.Clear(Color.Transparent);
                Figure(knight, batch, device, true, ref open);

                canvas.Painted = frame;
            }
            catch (Exception e)
            {
                broken = true;
                ModContent.GetInstance<KnightCanvas>().Mod.Logger.Error("The Gilded Knight's own picture failed; he is drawn plainly until the mod reloads.", e);

                if (open)
                {
                    try { batch.End(); }
                    catch { }
                }
            }
            finally
            {
                device.SetRenderTargets(previous);

                // Binding the screen again can wipe it (FNA clears a target of this kind when it is
                // bound). Nothing is on it yet this frame; it is left as the game would have it.
                if (previous.Length == 0 && device.PresentationParameters.RenderTargetUsage == RenderTargetUsage.DiscardContents)
                    device.Clear(Color.Black);
            }
        }

        private static Canvas Ensure(GraphicsDevice device, int whoAmI)
        {
            if (!canvases.TryGetValue(whoAmI, out Canvas canvas))
            {
                canvas = new Canvas();
                canvases[whoAmI] = canvas;
            }

            if (canvas.Body == null || canvas.Body.IsDisposed)
                canvas.Body = new RenderTarget2D(device, Width, Height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            if (canvas.Light == null || canvas.Light.IsDisposed)
                canvas.Light = new RenderTarget2D(device, Width, Height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            return canvas;
        }

        private static void Begin(SpriteBatch batch, BlendState blend, ref bool open)
        {
            batch.Begin(SpriteSortMode.Deferred, blend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            open = true;
        }

        private static void End(SpriteBatch batch, ref bool open)
        {
            batch.End();
            open = false;
        }

        /// <summary>
        /// All of him, back to front, on the target bound now: his body (with its shadows) or, for
        /// <paramref name="light"/>, every piece black and each blade's light added after it.
        /// </summary>
        private static void Figure(GildedKnight knight, SpriteBatch batch, GraphicsDevice device, bool light, ref bool open)
        {
            parts.Clear();
            KnightRig.Frame f = KnightRig.Build(knight.Pose, Origin, knight.Faces, parts, knight.SunSwing, knight.GraceSwing);
            KnightRig.Layer(parts, out int capeAt, out int backAt, out int frontAt);

            Color paint = light ? new Color(0, 0, 0, 255) : Color.White;
            Color sunShine = knight.SunShine;
            Color graceShine = knight.GraceShine;

            // The cloth hangs to his back as he is seen (the other way, past edge on in a turn about).
            int faces = knight.SeenFaces;

            // How the cloth's chains (in the world, as he is seen) come onto this picture (side on).
            Vector2 shift = Origin - knight.Feet;
            float wide = Math.Max(KnightRig.Thin, f.Turn);

            Begin(batch, BlendState.AlphaBlend, ref open);

            for (int i = 0; i <= parts.Count; i++)
            {
                if (i == capeAt)
                    Cloth(knight.Cape, KnightRig.Cape, s => MathHelper.Lerp(8f, 18f, s), new Vector2(-faces, 0f), 0.3f, shift, wide, paint, batch, device, ref open);

                if (i == backAt)
                    Cloth(knight.BackFlap, KnightRig.BackFlap, s => MathHelper.Lerp(9f, 10.5f, s), new Vector2(-faces, 0f), 0f, shift, wide, paint, batch, device, ref open);

                if (i == frontAt)
                {
                    Cloth(knight.FrontFlap, KnightRig.FrontFlap, s => MathHelper.Lerp(9f, 10.5f, s), new Vector2(-faces, 0f), 0f, shift, wide, paint, batch, device, ref open);
                    Cloth(knight.Plume, KnightRig.Plume, s => 5f + 6f * (float)Math.Sin(Math.Min(1f, s * 1.3f + 0.15f) * MathHelper.Pi) * 0.75f + 1f,
                        new Vector2(0f, -1f), 0f, shift, wide, paint, batch, device, ref open);
                }

                if (i == parts.Count)
                    break;

                KnightRig.Part p = parts[i];
                Texture2D tex = Piece(p.Piece);

                if (tex == null)
                    continue;

                SpriteEffects flip = p.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                // The shadow it lays on what is under it, before it.
                if (!light && p.Shadow)
                {
                    End(batch, ref open);
                    Begin(batch, Darken, ref open);
                    batch.Draw(tex, p.At + ShadowOff, null, new Color(0f, 0f, 0f, ShadowDark), p.Rotation, p.Origin, p.Scale, flip, 0f);
                    End(batch, ref open);
                    Begin(batch, BlendState.AlphaBlend, ref open);
                }

                batch.Draw(tex, p.At, null, paint, p.Rotation, p.Origin, p.Scale, flip, 0f);

                // Its light, over it, for a blade.
                if (light && (p.Piece == KnightRig.SunBlade || p.Piece == KnightRig.GraceBlade))
                {
                    bool sun = p.Piece == KnightRig.SunBlade;
                    Color shine = sun ? sunShine : graceShine;

                    if (shine.R + shine.G + shine.B > 4)
                    {
                        Texture2D glow = Tex(KnightRig.Folder + (sun ? KnightRig.SunGlow : KnightRig.GraceGlow));

                        if (glow != null)
                            batch.Draw(glow, p.At, null, new Color(shine.R, shine.G, shine.B, 0), p.Rotation, p.Origin, p.Scale, flip, 0f);
                    }
                }
            }

            End(batch, ref open);
        }

        /// <summary>
        /// A piece of cloth as a strip along its chain: the chain's points from the world onto this
        /// picture (widened back out of how narrow he is seen, since the picture is narrowed again as
        /// it is laid down), its picture laid along it, in the colour given.
        /// </summary>
        private static void Cloth(KnightCloth cloth, string name, Func<float, float> width, Vector2 firstSide, float lean, Vector2 shift, float wide,
            Color colour, SpriteBatch batch, GraphicsDevice device, ref bool open)
        {
            if (!cloth.Ready)
                return;

            Texture2D tex = Tex(KnightRig.Folder + name);

            if (tex == null)
                return;

            int n = cloth.Points.Length;

            if (edgeA.Length < n)
            {
                edgeA = new Vector2[n];
                edgeB = new Vector2[n];
            }

            Vector2[] first = edgeA;
            Vector2[] second = edgeB;
            cloth.Edges(width, firstSide, lean, first, second);

            End(batch, ref open);

            if (vertices.Length < n * 2)
                vertices = new VertexPositionColorTexture[n * 2];

            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                vertices[i * 2] = new VertexPositionColorTexture(new Vector3(Widen(first[i] + shift, wide), 0f), colour, new Vector2(u, 0f));
                vertices[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(Widen(second[i] + shift, wide), 0f), colour, new Vector2(u, 1f));
            }

            effect ??= new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = true,
            };

            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, Width, Height, 0f, -1f, 1f);
            effect.Texture = tex;

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique.Passes[0].Apply();
            device.SamplerStates[0] = SamplerState.PointClamp;
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, n * 2 - 2);

            Begin(batch, BlendState.AlphaBlend, ref open);
        }

        /// <summary>A point of him as seen (narrowed about the line through his feet) put back side on.</summary>
        private static Vector2 Widen(Vector2 at, float wide) => new Vector2(Origin.X + (at.X - Origin.X) / wide, at.Y);
    }
}
