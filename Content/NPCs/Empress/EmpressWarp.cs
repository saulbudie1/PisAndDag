using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// The world bent by her light (by request, the pillars' "surrounding distortion effect" as in the clip asked for:
    /// where a falling light passes, everything beside it is kinked sideways; the air beside a beam shimmers).
    ///
    /// The world as the game drew it this frame, into its own capture (Main.screenTarget, the one its screen effects
    /// draw from), is bent in the capture itself before the game lays its filters over it and puts it on the screen
    /// (FilterManager.EndCapture): drawn again through a fine grid, each point of the grid taking its picture from a
    /// little way off, so a Blood Moon's red, the water's own waves and the world turned over for one who walks upside
    /// down go over the bends as over everything else. Where nothing bends, nothing moves. What bends it, each in screen
    /// pixels, summed:
    ///
    ///   KINK     a falling light's bend: the world beside it pushed sideways, most at the light, gone a little above and
    ///            below it (a V), gone out to the sides
    ///   HAZE     the air along a line of light shimmering: rippling across it as heat does, rising along it
    ///   RIPPLE   a ring (or, about a line, a pair of walls) going out: the world pushed out ahead of it and pulled in
    ///            behind it, as a shockwave bends what is seen through it. Timed: it runs out by itself.
    ///   LENS     the world swollen (or drawn in) about a point, and turned about it if asked
    ///
    /// Kinks, hazes and lenses are asked for each frame by whatever draws the light that makes them (as it is drawn, so
    /// they go with it); ripples are started once and run their course. Nothing of the mod's own shader: vanilla's
    /// BasicEffect with the capture as its texture, as the mirror's shards (EmpressMirror). With lighting that makes no
    /// capture (Retro, Trippy), or the game's Waves Quality off, nothing is bent: the light is drawn as ever.
    ///
    /// The capture costs a little, so it is only made while it is wanted: while an Empress near enough to be seen is in
    /// something that bends, or a ripple runs, and only with the waves on; or while a mirror crack is up, whatever the
    /// waves (EmpressMirror, whose shards are drawn here too, after the bends, so nothing is drawn over them).
    /// </summary>
    public class EmpressWarp : ModSystem
    {
        private enum Kind : byte
        {
            Kink,
            Haze,
            Ripple,
            Lens,
        }

        private struct Source
        {
            public Kind Kind;

            /// <summary>World points: the middle (kink, lens, a point ripple), or a line's two ends (haze, a line ripple).</summary>
            public Vector2 A;
            public Vector2 B;

            /// <summary>World pixels: how far it reaches across (kink, haze), its height (kink), the ring's thickness (ripple), the lens's reach.</summary>
            public float Wide;
            public float Tall;

            /// <summary>Screen pixels at the game's own zoom (a lens: a share); the way a kink pushes (unit), or a lens's turn.</summary>
            public float Strength;
            public Vector2 Way;
            public float Turn;

            /// <summary>Haze: the length of its ripples along the line; and its own phase.</summary>
            public float Wave;
            public float Phase;

            /// <summary>Ripple: when it started (game updates), how fast it goes out, how long it lives.</summary>
            public uint Born;
            public float Speed;
            public int Life;
        }

        private static readonly List<Source> frame = new();
        private static readonly List<Source> ripples = new();

        private static bool listening;
        private static BasicEffect effect;
        private static RenderTarget2D aside;
        private static VertexPositionColorTexture[] vertices = Array.Empty<VertexPositionColorTexture>();
        private static int[] indices = Array.Empty<int>();
        private static float[] dx = Array.Empty<float>();
        private static float[] dy = Array.Empty<float>();
        private static int cols;
        private static int rows;
        private static float step;
        private static bool failed;

        /// <summary>The most a point is moved, in screen pixels: a bend, never a tear.</summary>
        private const float Most = 64f;

        /// <summary>World pixels from this machine's player within which an Empress's bends are listened for: further, they are not seen.</summary>
        private const float SeenFrom = 4000f;

        /// <summary>Is the capture being made (something of hers wants it)?</summary>
        internal static bool Listening => listening;

        /// <summary>Can this machine bend the world at all: the game's waves on, a capture made (not Retro or Trippy), nothing gone wrong.</summary>
        private static bool Bendable => Main.WaveQuality > 0 && !Main.drawToScreen && Lighting.NotRetro && !failed;

        /// <summary>Will bends be drawn this frame?</summary>
        internal static bool CanBend => listening && Bendable && !Main.mapFullscreen;

        // ---- asked for ----------------------------------------------------------------------------------------

        /// <summary>
        /// A kink this frame: the world about <paramref name="at"/> pushed <paramref name="strength"/> pixels the way
        /// <paramref name="way"/>, most at the point, gone <paramref name="tall"/> above and below it (along the line it
        /// falls down: <paramref name="along"/>) and <paramref name="wide"/> out to either side.
        /// </summary>
        public static void Kink(Vector2 at, Vector2 along, float wide, float tall, float strength, Vector2 way)
        {
            if (Main.dedServ || strength <= 0.05f || !CanBend)
                return;

            frame.Add(new Source { Kind = Kind.Kink, A = at, B = at + along.SafeNormalize(Vector2.UnitY), Wide = wide, Tall = tall, Strength = strength, Way = way.SafeNormalize(Vector2.UnitX) });
        }

        /// <summary>
        /// The air along a line of light shimmering this frame, out to <paramref name="wide"/> either side of it: ripples
        /// <paramref name="wave"/> apart along it, rising; <paramref name="phase"/> its own, so lines side by side differ.
        /// </summary>
        public static void Haze(Vector2 from, Vector2 to, float wide, float strength, float wave = 90f, float phase = 0f)
        {
            if (Main.dedServ || strength <= 0.05f || !CanBend)
                return;

            frame.Add(new Source { Kind = Kind.Haze, A = from, B = to, Wide = wide, Strength = strength, Wave = Math.Max(8f, wave), Phase = phase });
        }

        /// <summary>The world swollen about a point this frame (a positive share: seen nearer; negative: drawn in), and turned about it (radians at its middle).</summary>
        public static void Lens(Vector2 at, float reach, float strength, float turn = 0f)
        {
            if (Main.dedServ || Math.Abs(strength) <= 0.002f && Math.Abs(turn) <= 0.002f || !CanBend)
                return;

            frame.Add(new Source { Kind = Kind.Lens, A = at, Wide = reach, Strength = strength, Turn = turn });
        }

        /// <summary>
        /// A ring going out from a point (or two walls going out from a line, <paramref name="to"/> given): this fast,
        /// this thick, bending this much at the first, and less as it goes, for this many ticks.
        /// </summary>
        public static void Ripple(Vector2 at, float speed, float thick, float strength, int life, Vector2? to = null)
        {
            if (Main.dedServ || strength <= 0.05f || life <= 0 || !Bendable)
                return;

            ripples.Add(new Source { Kind = Kind.Ripple, A = at, B = to ?? at, Speed = speed, Tall = thick, Strength = strength, Life = life, Born = Main.GameUpdateCount });

            if (ripples.Count > 48)
                ripples.RemoveAt(0);
        }

        // ---- listening ----------------------------------------------------------------------------------------

        public override void Load()
        {
            if (!Main.dedServ)
                On_FilterManager.EndCapture += BendBeforeFilters;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            // Whatever a frame asked for and no capture took (it is taken at the end of the frame it was asked in).
            frame.Clear();

            ripples.RemoveAll(r => Main.GameUpdateCount - r.Born > (uint)r.Life);

            // A crack's shards need the capture whatever the waves; bends only where they can be drawn, and only for an
            // Empress near enough to be seen.
            bool want = EmpressMirror.Wanted;

            if (!want && Bendable)
            {
                want = ripples.Count > 0;

                for (int i = 0; i < Main.maxNPCs && !want; i++)
                    want = (EmpressOfLight.Of(i)?.Bending ?? false) && Main.npc[i].WithinRange(Main.LocalPlayer.Center, SeenFrom);
            }

            if (want == listening)
                return;

            // Anything listening at the end of the frame has the game make its capture (FilterManager.CanCapture); the
            // bending itself is done as the capture ends (BendBeforeFilters).
            if (want)
                Filters.Scene.OnPostDraw += Nothing;
            else
            {
                Filters.Scene.OnPostDraw -= Nothing;
                frame.Clear();
                LetAsideGo();
            }

            listening = want;
        }

        /// <summary>The target the world was set aside in, given back (a screen's worth of memory) until it is wanted again; on the main thread, where the game draws.</summary>
        private static void LetAsideGo()
        {
            RenderTarget2D old = aside;
            aside = null;

            if (old != null)
                Main.QueueMainThreadAction(old.Dispose);
        }

        private static void Nothing()
        {
        }

        public override void OnWorldUnload()
        {
            StopListening();
            LetAsideGo();
            ripples.Clear();
            frame.Clear();
        }

        public override void Unload()
        {
            StopListening();
            LetAsideGo();

            BasicEffect old = effect;
            effect = null;

            if (old != null)
                Main.QueueMainThreadAction(old.Dispose);
        }

        private static void StopListening()
        {
            if (listening)
                Filters.Scene.OnPostDraw -= Nothing;

            listening = false;
        }

        // ---- drawing ------------------------------------------------------------------------------------------

        /// <summary>
        /// The world drawn into the capture, and the game about to lay its filters over it and put it on the screen: what
        /// bends, bent into the capture first, and a mirror crack's shards over that; then the game goes on as ever.
        /// </summary>
        private static void BendBeforeFilters(On_FilterManager.orig_EndCapture orig, FilterManager self, RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor)
        {
            if (listening && self == Filters.Scene && !Main.gameMenu)
                Bend(screenTarget1);

            orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
        }

        /// <summary>
        /// The world (the capture, as drawn) set aside, and drawn back into the capture: as it was where nothing bends,
        /// bent where something does, a crack's shards over it; and this frame's asks let go of.
        /// </summary>
        private static void Bend(RenderTarget2D world)
        {
            RenderTarget2D kept = null;
            bool rebound = false;

            try
            {
                if (world == null || world.IsDisposed)
                    return;

                bool bends = CanBend && (frame.Count > 0 || ripples.Count > 0) && Build(world.Width, world.Height);
                bool shards = EmpressMirror.Wanted;

                if (!bends && !shards)
                    return;

                GraphicsDevice device = Main.instance.GraphicsDevice;
                kept = Aside(device, world);

                device.SetRenderTarget(kept);
                Copy(world);

                // Bound again, the capture is wiped (the game's targets keep nothing): it is drawn back whole first.
                device.SetRenderTarget(world);
                rebound = true;
                Copy(kept);

                if (bends)
                    DrawGrid(kept);

                EmpressMirror.Draw(kept);
            }
            catch (Exception e)
            {
                // A bend gone wrong is not worth the game: no more of them this session, the light drawn as ever, and the
                // world put back in the capture whole if it was wiped.
                failed = true;
                ModContent.GetInstance<EmpressOfLightSystem>()?.Mod.Logger.Warn("Empress warp: " + e.Message);

                if (rebound && kept != null && !kept.IsDisposed)
                {
                    try
                    {
                        Main.instance.GraphicsDevice.SetRenderTarget(world);
                        Copy(kept);
                    }
                    catch (Exception)
                    {
                        // Nothing more to be done for this one frame.
                    }
                }
            }
            finally
            {
                frame.Clear();
            }
        }

        /// <summary>A screen's worth drawn onto the target, pixel for pixel; the batch closed whatever happens, for the game's own after it.</summary>
        private static void Copy(Texture2D from)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            try
            {
                Main.spriteBatch.Draw(from, Vector2.Zero, Color.White);
            }
            finally
            {
                Main.spriteBatch.End();
            }
        }

        /// <summary>Where the world is set aside to be drawn back from: a target the size and kind of the capture, made again if the screen changes.</summary>
        private static RenderTarget2D Aside(GraphicsDevice device, RenderTarget2D like)
        {
            if (aside == null || aside.IsDisposed || aside.Width != like.Width || aside.Height != like.Height || aside.Format != like.Format)
            {
                aside?.Dispose();
                aside = new RenderTarget2D(device, like.Width, like.Height, false, like.Format, DepthFormat.None);
            }

            return aside;
        }

        /// <summary>A world point on the screen, as the world was drawn this frame (its zoom and all).</summary>
        private static Vector2 OnScreen(Vector2 world) => Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);

        /// <summary>The grid laid over the capture (the screen's size) and every bend summed into it; false if nothing moved.</summary>
        private static bool Build(int width, int height)
        {
            // About 30,000 points over the screen whatever its size: 10 px apart at 1080p, more on a bigger screen.
            step = Math.Max(10f, (float)Math.Ceiling(Math.Sqrt(width * (double)height / 30000.0)));
            cols = (int)Math.Ceiling(width / step) + 1;
            rows = (int)Math.Ceiling(height / step) + 1;

            int count = cols * rows;

            if (dx.Length < count)
            {
                dx = new float[count];
                dy = new float[count];
                vertices = new VertexPositionColorTexture[count];
                indices = new int[(cols - 1) * (rows - 1) * 6];
            }
            else
            {
                Array.Clear(dx, 0, count);
                Array.Clear(dy, 0, count);
            }

            if (indices.Length < (cols - 1) * (rows - 1) * 6)
                indices = new int[(cols - 1) * (rows - 1) * 6];

            float zoom = Main.GameViewMatrix.Zoom.X;
            float time = (float)Main.timeForVisualEffects;
            bool any = false;

            foreach (Source s in frame)
                any |= Add(s, zoom, time, width, height);

            foreach (Source s in ripples)
                any |= Add(s, zoom, time, width, height);

            return any;
        }

        /// <summary>One bend summed into the grid, over the grid points it reaches (screen pixels throughout).</summary>
        private static bool Add(Source s, float zoom, float time, int width, int height)
        {
            Vector2 a = OnScreen(s.A);
            Vector2 b = OnScreen(s.B);
            float strength = s.Strength * zoom;
            float reach;
            Vector2 low;
            Vector2 high;

            switch (s.Kind)
            {
                case Kind.Kink:
                    reach = Math.Max(s.Wide, s.Tall) * zoom;
                    low = a - new Vector2(reach);
                    high = a + new Vector2(reach);
                    break;

                case Kind.Haze:
                    reach = s.Wide * zoom;
                    low = Vector2.Min(a, b) - new Vector2(reach);
                    high = Vector2.Max(a, b) + new Vector2(reach);
                    break;

                case Kind.Ripple:
                    {
                        float age = Main.GameUpdateCount - s.Born;
                        float radius = s.Speed * age * zoom;
                        reach = radius + s.Tall * zoom;
                        strength *= 1f - age / (float)Math.Max(1, s.Life);
                        low = Vector2.Min(a, b) - new Vector2(reach);
                        high = Vector2.Max(a, b) + new Vector2(reach);
                        break;
                    }

                default:
                    reach = s.Wide * zoom;
                    low = a - new Vector2(reach);
                    high = a + new Vector2(reach);
                    break;
            }

            if (strength <= 0.05f && s.Kind != Kind.Lens || reach <= 1f)
                return false;

            // Only the grid points it can reach, on the screen.
            int c0 = Math.Max(0, (int)Math.Floor(low.X / step));
            int c1 = Math.Min(cols - 1, (int)Math.Ceiling(high.X / step));
            int r0 = Math.Max(0, (int)Math.Floor(low.Y / step));
            int r1 = Math.Min(rows - 1, (int)Math.Ceiling(high.Y / step));

            if (c0 > c1 || r0 > r1 || high.X < 0f || high.Y < 0f || low.X > width || low.Y > height)
                return false;

            switch (s.Kind)
            {
                case Kind.Kink:
                    AddKink(s, a, b - a, zoom, strength, c0, c1, r0, r1);
                    break;

                case Kind.Haze:
                    AddHaze(s, a, b, zoom, strength, time, c0, c1, r0, r1);
                    break;

                case Kind.Ripple:
                    AddRipple(s, a, b, zoom, strength, c0, c1, r0, r1);
                    break;

                default:
                    AddLens(s, a, zoom, c0, c1, r0, r1);
                    break;
            }

            return true;
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The kink: a V along its line, most at its point; full across a little way, then gone by its width.</summary>
        private static void AddKink(Source s, Vector2 at, Vector2 along, float zoom, float strength, int c0, int c1, int r0, int r1)
        {
            Vector2 u = along.SafeNormalize(Vector2.UnitY);
            Vector2 across = new Vector2(-u.Y, u.X);
            float tall = Math.Max(1f, s.Tall * zoom);
            float wide = Math.Max(1f, s.Wide * zoom);
            Vector2 way = s.Way;

            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    Vector2 q = new Vector2(c * step, r * step) - at;
                    float t = Math.Abs(Vector2.Dot(q, u)) / tall;
                    float w = Math.Abs(Vector2.Dot(q, across)) / wide;

                    if (t >= 1f || w >= 1f)
                        continue;

                    float v = (float)Math.Pow(1f - t, 1.6) * (1f - Smooth((w - 0.25f) / 0.75f));
                    int i = r * cols + c;
                    dx[i] += way.X * strength * v;
                    dy[i] += way.Y * strength * v;
                }
            }
        }

        /// <summary>The haze: across the line, a shimmer most just beside the light and gone by its width; rising along it.</summary>
        private static void AddHaze(Source s, Vector2 a, Vector2 b, float zoom, float strength, float time, int c0, int c1, int r0, int r1)
        {
            Vector2 line = b - a;
            float length = line.Length();

            if (length < 1f)
                return;

            Vector2 u = line / length;
            Vector2 n = new Vector2(-u.Y, u.X);
            float wide = Math.Max(1f, s.Wide * zoom);
            float wave = s.Wave * zoom;
            float k = MathHelper.TwoPi / wave;
            float rise = time * 0.09f;

            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    Vector2 q = new Vector2(c * step, r * step) - a;
                    float along = Vector2.Dot(q, u);
                    float off = Vector2.Dot(q, n);
                    float d = Math.Abs(off) / wide;

                    if (d >= 1f)
                        continue;

                    // Its ends come in soft, a width beyond the line's.
                    float ends = Smooth((along + wide) / wide) * Smooth((length + wide - along) / wide);

                    if (ends <= 0f)
                        continue;

                    float envelope = (1f - Smooth(d)) * (0.45f + 0.55f * Smooth(d * 6f)) * ends;
                    float phase = along * k + rise + s.Phase;
                    float across = (float)Math.Sin(phase) + 0.45f * (float)Math.Sin(phase * 2.3f - rise * 1.7f + off * 0.05f);
                    float lengthwise = 0.35f * (float)Math.Cos(phase * 0.7f + off * 0.03f);

                    int i = r * cols + c;
                    float v = strength * envelope;
                    dx[i] += (n.X * across + u.X * lengthwise) * v;
                    dy[i] += (n.Y * across + u.Y * lengthwise) * v;
                }
            }
        }

        /// <summary>The ripple: at the ring, the world pushed out ahead of it and pulled in behind; about a line, two walls going out.</summary>
        private static void AddRipple(Source s, Vector2 a, Vector2 b, float zoom, float strength, int c0, int c1, int r0, int r1)
        {
            float age = Main.GameUpdateCount - s.Born;
            float radius = s.Speed * age * zoom;
            float thick = Math.Max(1f, s.Tall * zoom);
            Vector2 line = b - a;
            float lengthSq = line.LengthSquared();

            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    Vector2 p = new Vector2(c * step, r * step);

                    // From the nearest point of its line (or its point).
                    Vector2 nearest = lengthSq < 1f ? a : a + line * MathHelper.Clamp(Vector2.Dot(p - a, line) / lengthSq, 0f, 1f);
                    Vector2 q = p - nearest;
                    float distance = q.Length();
                    float u = (distance - radius) / thick;

                    if (u <= -1f || u >= 1f || distance < 0.001f)
                        continue;

                    float v = (float)Math.Sin(u * MathHelper.Pi) * (1f - u * u) * strength;
                    Vector2 out_ = q / distance;
                    int i = r * cols + c;
                    dx[i] -= out_.X * v;
                    dy[i] -= out_.Y * v;
                }
            }
        }

        /// <summary>The lens: the world seen from nearer its middle (swollen), or from further (drawn in), and turned.</summary>
        private static void AddLens(Source s, Vector2 at, float zoom, int c0, int c1, int r0, int r1)
        {
            float reach = Math.Max(1f, s.Wide * zoom);

            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    Vector2 q = new Vector2(c * step, r * step) - at;
                    float x = q.Length() / reach;

                    if (x >= 1f)
                        continue;

                    float fall = (1f - x * x) * (1f - x * x);
                    Vector2 from = q * (1f - s.Strength * fall);

                    if (Math.Abs(s.Turn) > 0.0001f)
                        from = from.RotatedBy(s.Turn * fall);

                    int i = r * cols + c;
                    dx[i] += from.X - q.X;
                    dy[i] += from.Y - q.Y;
                }
            }
        }

        /// <summary>The bent cells drawn into the capture: each grid point where it is, its picture from where it bends to, out of the world set aside.</summary>
        private static void DrawGrid(Texture2D world)
        {
            Vector2 size = new Vector2(world.Width, world.Height);
            int count = cols * rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i = r * cols + c;
                    float x = dx[i];
                    float y = dy[i];
                    float length = (float)Math.Sqrt(x * x + y * y);

                    if (length > Most)
                    {
                        x *= Most / length;
                        y *= Most / length;
                        dx[i] = x;
                        dy[i] = y;
                    }

                    Vector2 at = new Vector2(c * step, r * step);
                    vertices[i] = new VertexPositionColorTexture(new Vector3(at, 0f), Color.White, (at + new Vector2(x, y)) / size);
                }
            }

            // Only the cells something bends.
            int laid = 0;

            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int i = r * cols + c;

                    if (Still(i) && Still(i + 1) && Still(i + cols) && Still(i + cols + 1))
                        continue;

                    indices[laid++] = i;
                    indices[laid++] = i + 1;
                    indices[laid++] = i + cols;
                    indices[laid++] = i + 1;
                    indices[laid++] = i + cols + 1;
                    indices[laid++] = i + cols;
                }
            }

            if (laid < 6)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;
            effect ??= new BasicEffect(device) { VertexColorEnabled = true, TextureEnabled = true };

            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, world.Width, world.Height, 0f, 0f, 1f);
            effect.Texture = world;

            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;

            // Point sampled: where it does not bend, each pixel is its own again, not a blur of four.
            device.SamplerStates[0] = SamplerState.PointClamp;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, count, indices, 0, laid / 3);
            }

            device.Textures[0] = null;
        }

        private static bool Still(int i) => Math.Abs(dx[i]) < 0.05f && Math.Abs(dy[i]) < 0.05f;
    }
}
