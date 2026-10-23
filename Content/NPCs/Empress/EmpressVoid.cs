using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// How her arena looks, by request ("a hexagonal Arena with outlines", made a decagon after, a corner
    /// for each of her ten stars; and to look and behave as the border in the video it was shown by: a
    /// wall of another sky closing everything off, a bright line where it meets the world, closing in at
    /// the end): everything outside the decagon is gone
    /// behind what lies outside her, a night of clouds lit in her colours, every colour of her wheel
    /// at once in places, drifting across them, with nearer clouds in other colours over them and
    /// stars, deeper the further off; along the border a line of her colours, running round it, white
    /// at its heart, with a glow out into the dark and a fainter one in over the arena; her ten stars
    /// on it, turning; and faint lines of light across the arena from star to star, with beads of
    /// light running along them.
    ///
    /// What lies outside is two textures, each one big square that repeats (EmpressVoid.png, the
    /// clouds, in grey: how much light, not what colour; EmpressVoidStars.png), made so that along
    /// every edge each is the same turned a quarter as not, so each repeat is laid turned one of the
    /// four ways (by where it is) and still meets its neighbours without a seam, and the repeat does not
    /// show. The colour is laid over the grey here, at the corners of a fine grid that the graphics
    /// card blends between: her wheel of colours, deepened, run across each layer by slow fields of its
    /// own, turning with time; and where the clouds are thick and thin by another such field, much
    /// bigger than the texture's own clouds, so no two screens of it are alike.
    ///
    /// It is drawn over the world's tiles and under everything that lives in it (by detour of the
    /// game's drawing of its solid tiles, after them), so the players, the Empress and whatever is
    /// thrown are seen over it wherever they are, and nothing of the world is seen through it.
    ///
    /// The shapes are the one thing a sprite batch cannot draw, so they are triangles straight to the
    /// graphics device through the game's own BasicEffect (as Strip draws its bands); the stars and
    /// the beads are the sprite batch's. No shader of the mod's own.
    ///
    /// It eases in as the arena comes and out as it goes, on its own clock, so an arena that is gone
    /// in a tick (a despawn) still fades rather than vanishing.
    /// </summary>
    public class EmpressVoidSystem : ModSystem
    {
        private static BasicEffect effect;
        private static VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[4096];
        private static int count;

        /// <summary>How much of it is there, 0 to 1: eased, so it fades in and out on its own.</summary>
        private static float shown;

        /// <summary>
        /// Its own clock, in seconds, for the drift: the game's clock for such things starts again every
        /// hour, which would jump every layer back to where it began.
        /// </summary>
        private static double clock;

        /// <summary>Its clock, for her sky's ribbons too (EmpressSkyEffect).</summary>
        internal static double Clock => clock;

        /// <summary>The arena as last seen, to fade out where it was once it is gone.</summary>
        private static Vector2 centre;
        private static float size = EmpressArena.Radius;
        private static float outline = 1f;
        private static float veil = 1f;
        private static float flare;

        /// <summary>
        /// A layer of what lies outside: a texel of it, in world pixels; how much of the camera's movement
        /// it takes; its drift, in world pixels a second; its seed, for how its repeats are turned and for
        /// its fields; how much light it gives at most; where on her wheel its colours start; and, for the
        /// stars, how near to white their colours are.
        /// </summary>
        private sealed class Layer
        {
            public readonly float Texel;
            public readonly float Parallax;
            public readonly Vector2 Drift;
            public readonly int Seed;
            public readonly float Light;
            public readonly float Phase;
            public readonly float White;

            public Layer(float texel, float parallax, Vector2 drift, int seed, float light, float phase, float white = 0f)
            {
                Texel = texel;
                Parallax = parallax;
                Drift = drift;
                Seed = seed;
                Light = light;
                Phase = phase;
                White = white;
            }
        }

        private static readonly Layer Far = new Layer(2f, 0.35f, new Vector2(-9f, 2.5f), 1, 1f, 0f);
        private static readonly Layer Near = new Layer(3f, 0.6f, new Vector2(14f, -4f), 2, 0.32f, 0.15f);
        private static readonly Layer StarLayer = new Layer(2f, 0.5f, new Vector2(5f, 1f), 3, 0.85f, 0.2f, 0.55f);

        /// <summary>The dark under the clouds.</summary>
        private static readonly Vector3 Deep = new Vector3(5f, 5f, 14f) / 255f;

        /// <summary>The grid the colours are worked out on, in world pixels: fine enough that the blend between its corners is not seen.</summary>
        private const float CellSize = 128f;

        /// <summary>The glow out into the dark from the border, and in over the arena; the line's colour and its heart, in world pixels.</summary>
        private const float GlowOut = 240f;
        private const float GlowIn = 110f;
        private const float LineWidth = 14f;
        private const float HeartWidth = 3.5f;

        /// <summary>What lies outside shows first along the border, and runs out from it this far, and then fades out over its fringe (EmpressArena.Reach is the two).</summary>
        private const float VeilRun = 2600f;
        private const float VeilFringe = 420f;

        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.DoDraw_Tiles_Solid += AfterSolidTiles;
        }

        public override void Unload()
        {
            BasicEffect old = effect;
            effect = null;

            if (old != null)
                Main.QueueMainThreadAction(old.Dispose);
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            clock += 1.0 / 60.0;

            if (EmpressArena.Up)
            {
                shown = Math.Min(1f, shown + 1f / 20f);
                centre = EmpressArena.Centre;
                size = EmpressArena.Size;
                outline = EmpressArena.Outline;
                veil = EmpressArena.Veil;
                flare = EmpressArena.Flare;
            }
            else
            {
                shown = Math.Max(0f, shown - 1f / 30f);
            }
        }

        public override void OnWorldUnload()
        {
            shown = 0f;
        }

        private static void AfterSolidTiles(On_Main.orig_DoDraw_Tiles_Solid orig, Main self)
        {
            orig(self);

            if (shown <= 0f || Main.gameMenu || Main.mapFullscreen)
                return;

            try
            {
                Draw();
            }
            catch (Exception e)
            {
                // A frame of the arena lost is not worth the game.
                ModContent.GetInstance<EmpressVoidSystem>()?.Mod.Logger.Warn("Empress arena draw: " + e.Message);
            }
        }

        // ---- the drawing -------------------------------------------------------------------------------

        private static void Draw()
        {
            if (!EmpressArt.Ready(EmpressArt.Void) || !EmpressArt.Ready(EmpressArt.VoidStars))
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;

            effect ??= new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = true,
            };

            Viewport viewport = device.Viewport;
            effect.World = Matrix.Identity;
            effect.View = Main.GameViewMatrix.TransformationMatrix;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, -1f, 1f);

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;

            float time = (float)Main.timeForVisualEffects / 60f;
            float s = shown * shown * (3f - 2f * shown);

            // How far out from the border what lies outside has come, running out from the line: all of it
            // is a band of VeilRun, past the corners of the screen of anyone at the border looking out, and
            // its fringe fading past that. Further off the world is as it was, for anyone out there.
            float veilTo = size + VeilRun * veil;
            float veilFringe = VeilFringe * Math.Min(1f, veil * 4f);
            float dark = s * Math.Min(1f, veil * 5f);

            // ---- what lies outside: the deep, the clouds, the nearer clouds, the stars ----

            if (dark > 0.003f)
            {
                Texture2D clouds = EmpressArt.Void.Value;
                Texture2D stars = EmpressArt.VoidStars.Value;
                float drift = (float)clock;

                Regions(veilTo, veilFringe, dark);

                effect.TextureEnabled = false;
                Begin();
                Cover(Far, clouds, true, drift);
                Flush(null, SamplerState.PointClamp);

                effect.TextureEnabled = true;
                Begin();
                Cover(Far, clouds, false, drift);
                Flush(clouds, SamplerState.PointClamp);

                Begin();
                Cover(Near, clouds, false, drift);
                Flush(clouds, SamplerState.PointClamp);

                Begin();
                Cover(StarLayer, stars, false, drift);
                Flush(stars, SamplerState.PointClamp);
            }

            // ---- the glow, out into the dark and in over the arena ----

            Texture2D white = TextureAssets.MagicPixel.Value;
            float lineShown = s * outline;
            effect.TextureEnabled = true;

            if (lineShown > 0.003f || flare > 0.003f)
            {
                Begin();

                float glowStrength = (0.55f + 0.25f * flare) * s * Math.Max(lineShown, flare);
                GlowBand(size, size + GlowOut * (1f + 0.5f * flare), glowStrength, 0f, time);
                GlowBand(size - GlowIn * (1f + 0.5f * flare), size, 0f, glowStrength * 0.45f, time);

                Flush(white, SamplerState.LinearClamp);

                // ---- the line ----

                Begin();
                Outline(LineWidth * (1f + 1.5f * flare), 0.85f * s, false, time);
                Outline(HeartWidth * (1f + 1f * flare), s, true, time);
                Flush(white, SamplerState.LinearClamp);
            }

            // ---- the stars on it, the lines across, the beads on them ----

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            try
            {
                if (lineShown > 0.003f)
                    DrawChords(Main.spriteBatch, lineShown, time);

                DrawStars(Main.spriteBatch, s, time);
            }
            finally
            {
                Main.spriteBatch.End();
            }
        }

        private static void Begin() => count = 0;

        private static void Flush(Texture2D texture, SamplerState sampler)
        {
            if (count < 3)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;

            effect.Texture = texture;
            effect.CurrentTechnique.Passes[0].Apply();
            device.SamplerStates[0] = sampler;
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, count / 3);
        }

        private static void Vertex(Vector2 world, Color colour, Vector2 uv)
        {
            if (count + 1 > vertices.Length)
                Array.Resize(ref vertices, vertices.Length * 2);

            vertices[count++] = new VertexPositionColorTexture(new Vector3(world - Main.screenPosition, 0f), colour, uv);
        }

        private static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vertex(a, ca, ua);
            Vertex(b, cb, ub);
            Vertex(c, cc, uc);

            Vertex(a, ca, ua);
            Vertex(c, cc, uc);
            Vertex(d, cd, ud);
        }

        // ---- what lies outside -------------------------------------------------------------------------

        /// <summary>
        /// A piece of where what lies outside is drawn: a stretch of the band between two decagons about
        /// the arena's middle, one side of them (so a four-cornered piece, never bent), and how strong it
        /// is at its inner decagon and at its outer.
        /// </summary>
        private struct Piece
        {
            public Vector2 A, B, C, D;
            public float From, To;
            public float InnerStrength, OuterStrength;
            public float Left, Top, Right, Bottom;
        }

        private static readonly Piece[] pieces = new Piece[2 * Decagon.Sides];
        private static int pieceCount;

        private static readonly Vector2[] cutA = new Vector2[16];
        private static readonly Vector2[] cutB = new Vector2[16];

        /// <summary>Where what lies outside is drawn this frame: the band from the border out as far as it has come, all of it at full strength, and its fringe fading out past that.</summary>
        private static void Regions(float veilTo, float veilFringe, float dark)
        {
            pieceCount = 0;
            Band(size, veilTo, dark, dark);

            if (veilFringe > 1f)
                Band(veilTo, veilTo + veilFringe, dark, 0f);
        }

        private static void Band(float from, float to, float innerStrength, float outerStrength)
        {
            for (int j = 0; j < Decagon.Sides; j++)
            {
                Piece piece = new Piece
                {
                    A = Decagon.Corner(centre, from, j),
                    B = Decagon.Corner(centre, from, j + 1),
                    C = Decagon.Corner(centre, to, j + 1),
                    D = Decagon.Corner(centre, to, j),
                    From = from,
                    To = to,
                    InnerStrength = innerStrength,
                    OuterStrength = outerStrength,
                };

                piece.Left = Math.Min(Math.Min(piece.A.X, piece.B.X), Math.Min(piece.C.X, piece.D.X));
                piece.Right = Math.Max(Math.Max(piece.A.X, piece.B.X), Math.Max(piece.C.X, piece.D.X));
                piece.Top = Math.Min(Math.Min(piece.A.Y, piece.B.Y), Math.Min(piece.C.Y, piece.D.Y));
                piece.Bottom = Math.Max(Math.Max(piece.A.Y, piece.B.Y), Math.Max(piece.C.Y, piece.D.Y));

                pieces[pieceCount++] = piece;
            }
        }

        private static float Strength(in Piece piece, Vector2 world)
        {
            if (piece.InnerStrength == piece.OuterStrength)
                return piece.InnerStrength;

            // Decagon.RadiusAt is the same all along one side of a decagon, so a piece's strength goes evenly
            // across it, and what the graphics card blends between its corners is exact.
            float along = MathHelper.Clamp((Decagon.RadiusAt(world, centre) - piece.From) / Math.Max(1f, piece.To - piece.From), 0f, 1f);
            return MathHelper.Lerp(piece.InnerStrength, piece.OuterStrength, along);
        }

        /// <summary>
        /// One layer of what lies outside, over where it is drawn: cut into the cells of the layer's own
        /// grid (each inside one repeat of its texture), each cell's piece of it laid with the texture as
        /// that repeat is turned, and coloured at its corners. The deep is the dark under the clouds, drawn
        /// without the texture; the rest is light, added.
        /// </summary>
        private static void Cover(Layer layer, Texture2D texture, bool deep, float drift)
        {
            float period = texture.Width * layer.Texel;
            int cellsPerRepeat = Math.Max(1, (int)Math.Round(period / CellSize));
            float cell = period / cellsPerRepeat;

            // The layer's own place: as much of the camera as it takes, its drift; to whole pixels, so its texels keep their size.
            Vector2 shift = -Main.screenPosition * (1f - layer.Parallax) + layer.Drift * drift;
            shift = new Vector2((float)Math.Round(shift.X), (float)Math.Round(shift.Y));

            // What the camera sees, and a texel round it.
            Vector2 middle = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 half = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f / Math.Max(0.25f, Main.GameViewMatrix.Zoom.X) + new Vector2(8f);
            Vector2 low = middle - half;
            Vector2 high = middle + half;

            int fromX = (int)Math.Floor((low.X + shift.X) / cell);
            int toX = (int)Math.Floor((high.X + shift.X) / cell);
            int fromY = (int)Math.Floor((low.Y + shift.Y) / cell);
            int toY = (int)Math.Floor((high.Y + shift.Y) / cell);

            float twinkle = 0.75f + 0.25f * (float)Math.Sin(drift * 1.7f);

            for (int cy = fromY; cy <= toY; cy++)
            {
                float top = cy * cell - shift.Y;
                float bottom = top + cell;
                int repeatY = FloorDiv(cy, cellsPerRepeat);

                for (int cx = fromX; cx <= toX; cx++)
                {
                    float left = cx * cell - shift.X;
                    float right = left + cell;
                    int repeatX = FloorDiv(cx, cellsPerRepeat);
                    int turn = (int)(Hash(repeatX, repeatY, layer.Seed * 7 + 11) * 4f) & 3;

                    for (int p = 0; p < pieceCount; p++)
                    {
                        ref Piece piece = ref pieces[p];

                        if (piece.Right < left || piece.Left > right || piece.Bottom < top || piece.Top > bottom)
                            continue;

                        int n = Cut(piece, left, top, right, bottom);

                        for (int k = 1; k + 1 < n; k++)
                        {
                            Corner(cutA[0], piece, layer, shift, period, repeatX, repeatY, turn, deep, drift, twinkle);
                            Corner(cutA[k], piece, layer, shift, period, repeatX, repeatY, turn, deep, drift, twinkle);
                            Corner(cutA[k + 1], piece, layer, shift, period, repeatX, repeatY, turn, deep, drift, twinkle);
                        }
                    }
                }
            }
        }

        /// <summary>A corner of a cell's piece of a layer: where it is, where on the texture (turned as its repeat is), its colour.</summary>
        private static void Corner(Vector2 world, in Piece piece, Layer layer, Vector2 shift, float period, int repeatX, int repeatY, int turn, bool deep, float drift, float twinkle)
        {
            float strength = Strength(piece, world);
            Vector2 at = world + shift;

            float u = (at.X - repeatX * period) / period;
            float v = (at.Y - repeatY * period) / period;

            Vector2 uv = turn switch
            {
                0 => new Vector2(u, v),
                1 => new Vector2(v, 1f - u),
                2 => new Vector2(1f - u, 1f - v),
                _ => new Vector2(1f - v, u),
            };

            // Her colour here: a turn of her wheel, deepened (as light in the dark, her pastels are too pale).
            // Laid on the world, not on the layer, so every layer is lit alike where it is (each a little
            // further round the wheel), and the colours slide over the clouds as the camera moves.
            Vector3 tint = EmpressArt.Prism(Hue(world, layer.Phase, drift)).ToVector3();
            tint *= tint;

            Color colour;

            if (deep)
            {
                Vector3 rgb = (Deep + tint * (0.045f * Thickness(at, layer.Seed))) * strength;
                colour = new Color(rgb.X, rgb.Y, rgb.Z, strength);
            }
            else if (layer.White > 0f)
            {
                Vector3 rgb = (tint + (Vector3.One - tint) * layer.White) * (layer.Light * twinkle * strength);
                colour = new Color(rgb.X, rgb.Y, rgb.Z, 0f);
            }
            else
            {
                Vector3 rgb = tint * (layer.Light * Thickness(at, layer.Seed) * strength);
                colour = new Color(rgb.X, rgb.Y, rgb.Z, 0f);
            }

            Vertex(world, colour, uv);
        }

        /// <summary>A four-cornered piece cut to a rectangle: its corners left in cutA, and how many.</summary>
        private static int Cut(in Piece piece, float left, float top, float right, float bottom)
        {
            cutA[0] = piece.A;
            cutA[1] = piece.B;
            cutA[2] = piece.C;
            cutA[3] = piece.D;

            int n = CutSide(cutA, 4, cutB, 0, left);
            n = CutSide(cutB, n, cutA, 1, right);
            n = CutSide(cutA, n, cutB, 2, top);
            return CutSide(cutB, n, cutA, 3, bottom);
        }

        /// <summary>A shape with no inward bends, cut by one side of a rectangle (0 left, 1 right, 2 top, 3 bottom): what is left of it into another list.</summary>
        private static int CutSide(Vector2[] from, int n, Vector2[] into, int side, float at)
        {
            if (n < 3)
                return 0;

            int m = 0;
            Vector2 last = from[n - 1];
            bool lastIn = Keeps(last, side, at);

            for (int i = 0; i < n; i++)
            {
                Vector2 point = from[i];
                bool pointIn = Keeps(point, side, at);

                if (pointIn != lastIn)
                    into[m++] = Crossing(last, point, side, at);

                if (pointIn)
                    into[m++] = point;

                last = point;
                lastIn = pointIn;
            }

            return m;
        }

        private static bool Keeps(Vector2 point, int side, float at) => side switch
        {
            0 => point.X >= at,
            1 => point.X <= at,
            2 => point.Y >= at,
            _ => point.Y <= at,
        };

        /// <summary>Where a line crosses a side of the rectangle: put exactly on it, so neighbouring cells meet without a crack.</summary>
        private static Vector2 Crossing(Vector2 a, Vector2 b, int side, float at)
        {
            if (side < 2)
            {
                float t = (at - a.X) / (b.X - a.X);
                return new Vector2(at, MathHelper.Lerp(a.Y, b.Y, t));
            }
            else
            {
                float t = (at - a.Y) / (b.Y - a.Y);
                return new Vector2(MathHelper.Lerp(a.X, b.X, t), at);
            }
        }

        // ---- the fields that colour it -----------------------------------------------------------------------

        /// <summary>
        /// Where on her wheel the colour of what lies outside is, at a point of the world and a time: one
        /// way across the world slowly, bent by a slow field, the whole of it turning a little with time;
        /// a layer further round it by its phase.
        /// </summary>
        private static float Hue(Vector2 world, float phase, float time)
        {
            Vector2 bent = Turn(world, HueTurn);
            return (world.X * 0.8f + world.Y * 0.6f) / 2200f + 0.9f * Noise(bent.X / 1700f, bent.Y / 1700f, 300) + time * 0.03f + phase;
        }

        /// <summary>How thick the clouds of a layer are at a point of it, as the light they give, 0.3 to 1: far bigger than its texture's own clouds.</summary>
        private static float Thickness(Vector2 at, int seed)
        {
            Vector2 a = Turn(at, ThickTurn);
            Vector2 b = Turn(at, ThinTurn);
            float d = 0.62f * Noise(a.X / 1500f, a.Y / 1500f, seed + 100) + 0.38f * Noise(b.X / 640f, b.Y / 640f, seed + 200);
            float t = MathHelper.Clamp((d - 0.30f) / 0.45f, 0f, 1f);

            return 0.3f + 0.7f * t * t * (3f - 2f * t);
        }

        /// <summary>The fields' grids, turned off the world's, so nothing in them lines up with it: as (cos, sin) of the turn.</summary>
        private static readonly Vector2 HueTurn = new Vector2((float)Math.Cos(-0.7), (float)Math.Sin(-0.7));
        private static readonly Vector2 ThickTurn = new Vector2((float)Math.Cos(0.5), (float)Math.Sin(0.5));
        private static readonly Vector2 ThinTurn = new Vector2((float)Math.Cos(1.9), (float)Math.Sin(1.9));

        private static Vector2 Turn(Vector2 at, Vector2 turn) => new Vector2(at.X * turn.X - at.Y * turn.Y, at.X * turn.Y + at.Y * turn.X);

        /// <summary>Smooth noise, 0 to 1: a number for each whole point of a grid, eased between them.</summary>
        private static float Noise(float x, float y, int seed)
        {
            float floorX = (float)Math.Floor(x);
            float floorY = (float)Math.Floor(y);
            int x0 = (int)floorX;
            int y0 = (int)floorY;
            float fx = x - floorX;
            float fy = y - floorY;
            float sx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float sy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

            float a = Hash(x0, y0, seed);
            float b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed);
            float d = Hash(x0 + 1, y0 + 1, seed);

            float upper = a + (b - a) * sx;
            float lower = c + (d - c) * sx;
            return upper + (lower - upper) * sy;
        }

        /// <summary>A number from 0 to 1 for a whole point of a grid and a seed, the same every time.</summary>
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>
        /// A band of glow between two decagons, of her colours going round the border, from one strength
        /// at its inner edge to another at its outer; only where the line has grown, so the border is not
        /// shown before it is drawn.
        /// </summary>
        private static void GlowBand(float from, float to, float innerStrength, float outerStrength, float time)
        {
            const int Pieces = 12;

            for (int j = 0; j < Decagon.Sides; j++)
            {
                for (int p = 0; p < Pieces; p++)
                {
                    float u0 = (j + p / (float)Pieces) / Decagon.Sides;
                    float u1 = (j + (p + 1) / (float)Pieces) / Decagon.Sides;
                    float g0 = Grown(u0);
                    float g1 = Grown(u1);

                    if (g0 <= 0f && g1 <= 0f)
                        continue;

                    Vector2 a = Along(from, u0);
                    Vector2 b = Along(from, u1);
                    Vector2 c = Along(to, u1);
                    Vector2 d = Along(to, u0);

                    Color c0 = Hue(u0, time);
                    Color c1 = Hue(u1, time);

                    Quad(a, b, c, d,
                        EmpressArt.Additive(c0, innerStrength * g0), EmpressArt.Additive(c1, innerStrength * g1),
                        EmpressArt.Additive(c1, outerStrength * g1), EmpressArt.Additive(c0, outerStrength * g0),
                        Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero);
                }
            }
        }

        /// <summary>
        /// How much of the border's line is there at a share of the way round, 0 to 1: all of it once it has
        /// grown; while it grows, the stretches out of each star so far, soft at their tips.
        /// </summary>
        private static float Grown(float u)
        {
            float grown = Math.Max(outline, flare);

            if (grown >= 0.999f)
                return 1f;

            float best = 0f;

            for (int i = 0; i < Decagon.Stars; i++)
            {
                float u0 = StarShare(i);
                float u1 = i + 1 < Decagon.Stars ? StarShare(i + 1) : 1f;
                float half = (u1 - u0) * 0.5f * grown;
                float off = Math.Min(Outside(u, u0, u0 + half), Outside(u, u1 - half, u1));

                best = Math.Max(best, 1f - MathHelper.Clamp(off / 0.012f, 0f, 1f));
            }

            return best;
        }

        private static float Outside(float u, float from, float to) => u < from ? from - u : u > to ? u - to : 0f;

        /// <summary>Her colour at a share of the way round the border, going round.</summary>
        private static Color Hue(float u, float time) => EmpressArt.Prism(u * 2f - time * 0.12f);

        /// <summary>A point a share of the way round the border of a decagon this size, from its right corner.</summary>
        private static Vector2 Along(float radius, float u)
        {
            u = (u % 1f + 1f) % 1f * Decagon.Sides;
            int j = (int)u;

            return Vector2.Lerp(Decagon.Corner(centre, radius, j), Decagon.Corner(centre, radius, j + 1), u - j);
        }

        /// <summary>A share of the way round the border of each of the ten stars: one on each corner.</summary>
        private static float StarShare(int star) => star / (float)Decagon.Stars;

        /// <summary>
        /// The line along the border, this wide, soft at its sides (a colour's strength falling to its
        /// edges): grown out of the stars, each stretch between two of them filled in from both ends, as
        /// much of it as Outline says. White at its heart.
        /// </summary>
        private static void Outline(float width, float strength, bool heart, float time)
        {
            if (strength <= 0.003f)
                return;

            float grown = Math.Max(outline, flare);

            for (int i = 0; i < Decagon.Stars; i++)
            {
                float u0 = StarShare(i);
                float u1 = i + 1 < Decagon.Stars ? StarShare(i + 1) : 1f;
                float half = (u1 - u0) * 0.5f * grown;

                LineStretch(u0, u0 + half, width, strength, heart, time);
                LineStretch(u1 - half, u1, width, strength, heart, time);
            }
        }

        /// <summary>One stretch of the line, from one share of the way round to another, in pieces that turn the corners.</summary>
        private static void LineStretch(float from, float to, float width, float strength, bool heart, float time)
        {
            if (to - from <= 0.0005f)
                return;

            const float Step = 1f / 48f;

            for (float u = from; u < to - 0.00001f;)
            {
                // Never across a corner: a piece ends at the next corner.
                float corner = ((float)Math.Floor(u * Decagon.Sides + 0.0001f) + 1f) / Decagon.Sides;
                float next = Math.Min(Math.Min(u + Step, to), corner);

                Vector2 a = Along(size, u);
                Vector2 b = Along(size, next);
                Vector2 across = Decagon.Normal((int)Math.Floor(u * Decagon.Sides + 0.0001f)) * (width * 0.5f);

                Color ca = heart ? EmpressArt.Additive(Color.White, strength) : EmpressArt.Additive(Hue(u, time), strength);
                Color cb = heart ? EmpressArt.Additive(Color.White, strength) : EmpressArt.Additive(Hue(next, time), strength);

                // Full at the middle of the line, nothing at its sides: two quads, out from the middle each way.
                Quad(a, b, b + across, a + across, ca, cb, Color.Transparent, Color.Transparent, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero);
                Quad(a, b, b - across, a - across, ca, cb, Color.Transparent, Color.Transparent, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero);

                u = next;
            }
        }

        /// <summary>Her ten stars, on the border: each a soft ball of her colour, a star of light turning in it, and a smaller one across it.</summary>
        private static void DrawStars(SpriteBatch spriteBatch, float s, float time)
        {
            Vector2 view = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float cull = new Vector2(Main.screenWidth, Main.screenHeight).Length() / Math.Max(0.5f, Main.GameViewMatrix.Zoom.X) * 0.6f + 400f;

            for (int i = 0; i < Decagon.Stars; i++)
            {
                Vector2 at = Decagon.Star(centre, size, i);

                if (Vector2.Distance(at, view) > cull)
                    continue;

                float pulse = 1f + 0.08f * (float)Math.Sin(time * 2.2f + i * 1.3f);
                float across = 120f * pulse * (1f + 0.6f * flare);
                Color colour = EmpressArt.Prism(i / 10f + time * 0.05f);
                float turn = time * 0.5f + i * 0.7f;

                EmpressArt.DrawBloom(spriteBatch, at, across * 2.4f, EmpressArt.Additive(colour, 0.45f * s));
                EmpressArt.DrawStar(spriteBatch, at, across, turn, EmpressArt.Additive(colour, 0.9f * s));
                EmpressArt.DrawStar(spriteBatch, at, across * 0.55f, turn + MathHelper.PiOver4, EmpressArt.Additive(Color.White, 0.8f * s));
                EmpressArt.DrawBloom(spriteBatch, at, across * 0.35f, EmpressArt.Additive(Color.White, 0.9f * s));
            }
        }

        /// <summary>
        /// Faint lines of light across the arena from star to star (each to the star three on, so that
        /// together they draw a star of ten points across it), as the lines across the border in the video
        /// it was shown by, and a bead of light running along each now and then.
        /// </summary>
        private static void DrawChords(SpriteBatch spriteBatch, float strength, float time)
        {
            for (int i = 0; i < Decagon.Stars; i += 1)
            {
                int j = (i + 3) % Decagon.Stars;

                Vector2 a = Decagon.Star(centre, size, i);
                Vector2 b = Decagon.Star(centre, size, j);
                Color colour = EmpressArt.Prism(i / 10f + time * 0.03f);

                EmpressArt.DrawLine(spriteBatch, a, b, 3f, EmpressArt.Additive(colour, 0.07f * strength));

                // A bead along it, every few seconds, out of step with the others.
                float cycle = (time * 0.22f + i * 0.37f) % 1f;

                if (cycle < 0.5f)
                {
                    float t = cycle / 0.5f;
                    Vector2 bead = Vector2.Lerp(a, b, t);
                    float fade = (float)Math.Sin(t * MathHelper.Pi);

                    EmpressArt.DrawBloom(spriteBatch, bead, 34f, EmpressArt.Additive(colour, 0.5f * fade * strength));
                    EmpressArt.DrawStar(spriteBatch, bead, 22f, time * 2f, EmpressArt.Additive(Color.White, 0.6f * fade * strength));
                }
            }
        }
    }
}
