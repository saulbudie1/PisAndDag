using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Graphics
{
    /// <summary>What a flame's colours run through as it ages.</summary>
    public enum FirePalette : byte
    {
        /// <summary>Fire: yellow, orange, red, ember.</summary>
        Fire,

        /// <summary>The Spazmatism's: pale lime, green (#3ea637), dark.</summary>
        Toxic,

        /// <summary>Skeletron Prime's: white-green, its green (#bbff00), olive, dark.</summary>
        Nuclear,
    }

    /// <summary>
    /// Fire, as a system of its own (by request: "New fire effect in game either by particle or seperate system", after
    /// the video it pointed at: a fire built of soft pieces whose colour runs from a hot yellow through orange to red as
    /// each ages, shrinking as it climbs, dark smoke off the top; and "optimize flame system").
    ///
    /// HOW IT LOOKS  Each flame is a soft tongue (or a round puff) added on as light. Young it is yellow, and where many
    /// young ones lie over each other they add up to white: the heart of a fire. As it ages it goes orange, red, ember,
    /// and shrinks; it climbs (its lift), slows (drag) and sways. Tongues point the way they go and stretch with their
    /// speed, so a jet of them reads as a rush and a still fire as licking. Smoke and gas are laid over the world instead
    /// (soot; a green body under a paler crown); sparks are single squares that keep their heat a while. All of it on the
    /// half-size canvas (PADPixelCanvas), so on the two-pixel grid.
    ///
    /// HOW IT IS KEPT CHEAP  One fixed pool of plain structs (no object a flame, nothing for the collector); one loop a
    /// tick that moves them and closes the gaps the dead leave, keeping their order; one loop a frame that draws what is
    /// on screen from one texture (one batch each pass), its colour and size read from tables of 64 made once; a tick's
    /// worth of light from at most forty of them. Emitters thin themselves as the pool fills (<see cref="Density"/>) and
    /// skip what is far off screen. Nothing runs on a server.
    ///
    /// The pictures (Content/Graphics/FireAtlas.png, made in code: scratchpad fire/gen.py) are cells of 32, the canvas's
    /// own resolution: four tongues, four puffs, a glow, a streak, a block of white for sparks. Tuned outside the game on
    /// a stand-in that draws as the canvas does (scratchpad fire/sim.py), which this was written from.
    /// </summary>
    public class PADFire : ModSystem
    {
        /// <summary>The most there can be at once.</summary>
        public const int Capacity = 6000;

        private const byte KindFlame = 0, KindSmoke = 1, KindGas = 2, KindSpark = 3;

        private const int Cell = 32;

        private struct Mote
        {
            public Vector2 Pos, Vel;
            public float Age, Life, Size, Rot, Spin, Lift, Sway, Phase, Heat;
            public byte Kind, Palette, Variant, Shape;
        }

        private static Mote[] pool;
        private static int count;
        private static Asset<Texture2D> atlas;

        /// <summary>A flame's colour through its life, its strength multiplied in: one table a palette.</summary>
        private static Color[][] ramps;
        private static float[] sizeCurve, smokeAlpha, smokeSize, gasAlpha;

        private static readonly Color SootColour = new(26, 21, 20);
        private static readonly Color GasBody = new(44, 120, 46);
        private static readonly Color GasCrown = new(150, 226, 96);

        /// <summary>Whether there is anything to draw.</summary>
        internal static bool Any => count > 0;

        /// <summary>How many there are (for anyone curious).</summary>
        public static int Count => count;

        /// <summary>
        /// How much of what is asked for the emitters make, 1 while there is room, less as the pool fills (to a fifth): a
        /// screen full of fire thins evenly rather than cutting off.
        /// </summary>
        public static float Density => pool == null ? 0f : MathHelper.Clamp((Capacity - count) / (Capacity * 0.45f), 0.2f, 1f);

        public override void Load()
        {
            if (Main.dedServ)
                return;

            pool = new Mote[Capacity];
            count = 0;

            ramps = new[]
            {
                Ramp((0f, 255, 226, 130, 0f), (0.035f, 255, 222, 120, 1f), (0.22f, 255, 176, 58, 1f), (0.44f, 250, 108, 26, 0.95f),
                    (0.66f, 206, 52, 14, 0.8f), (0.85f, 120, 24, 10, 0.5f), (1f, 40, 8, 4, 0f)),
                Ramp((0f, 222, 255, 150, 0f), (0.035f, 214, 255, 140, 1f), (0.22f, 160, 250, 78, 1f), (0.44f, 96, 224, 52, 0.95f),
                    (0.66f, 62, 166, 55, 0.8f), (0.85f, 22, 92, 34, 0.5f), (1f, 6, 30, 12, 0f)),
                Ramp((0f, 240, 255, 196, 0f), (0.035f, 236, 255, 176, 1f), (0.22f, 208, 255, 72, 1f), (0.44f, 187, 255, 0, 0.95f),
                    (0.66f, 122, 190, 8, 0.8f), (0.85f, 56, 104, 12, 0.5f), (1f, 14, 32, 6, 0f)),
            };

            sizeCurve = Curve((0f, 0.5f), (0.22f, 1f), (0.6f, 0.86f), (1f, 0.34f));
            smokeAlpha = Curve((0f, 0f), (0.2f, 0.36f), (0.6f, 0.24f), (1f, 0f));
            smokeSize = Curve((0f, 0.55f), (1f, 1.7f));
            gasAlpha = Curve((0f, 0f), (0.12f, 0.34f), (0.7f, 0.26f), (1f, 0f));
        }

        public override void Unload()
        {
            pool = null;
            count = 0;
            atlas = null;
            ramps = null;
            sizeCurve = smokeAlpha = smokeSize = gasAlpha = null;
        }

        public override void ClearWorld() => count = 0;

        private static Color[] Ramp(params (float t, int r, int g, int b, float k)[] keys)
        {
            Color[] table = new Color[64];

            for (int i = 0; i < 64; i++)
            {
                float t = i / 63f;

                for (int k = 0; k < keys.Length - 1; k++)
                {
                    if (t < keys[k].t || t > keys[k + 1].t)
                        continue;

                    float f = (t - keys[k].t) / Math.Max(1e-5f, keys[k + 1].t - keys[k].t);
                    float strength = MathHelper.Lerp(keys[k].k, keys[k + 1].k, f);
                    table[i] = new Color(
                        (int)(MathHelper.Lerp(keys[k].r, keys[k + 1].r, f) * strength),
                        (int)(MathHelper.Lerp(keys[k].g, keys[k + 1].g, f) * strength),
                        (int)(MathHelper.Lerp(keys[k].b, keys[k + 1].b, f) * strength), 255);
                    break;
                }
            }

            return table;
        }

        private static float[] Curve(params (float t, float v)[] keys)
        {
            float[] table = new float[64];

            for (int i = 0; i < 64; i++)
            {
                float t = i / 63f;

                for (int k = 0; k < keys.Length - 1; k++)
                {
                    if (t < keys[k].t || t > keys[k + 1].t)
                        continue;

                    table[i] = MathHelper.Lerp(keys[k].v, keys[k + 1].v, (t - keys[k].t) / Math.Max(1e-5f, keys[k + 1].t - keys[k].t));
                    break;
                }
            }

            return table;
        }

        /// <summary>A flame's colour at a share of its life (for anything that wants to match it: a glow, a light).</summary>
        public static Color ColourAt(FirePalette palette, float life)
        {
            if (ramps == null)
                return Color.Transparent;

            return ramps[(int)palette][(int)(MathHelper.Clamp(life, 0f, 1f) * 63f)];
        }

        // ---- one at a time ------------------------------------------------------------------------------------------------

        private static bool Ready => pool != null && !Main.gamePaused;

        /// <summary>Whether a place is on screen, or this near it: what is further is not worth making.</summary>
        public static bool Near(Vector2 at, float margin = 320f) =>
            at.X > Main.screenPosition.X - margin && at.X < Main.screenPosition.X + Main.screenWidth + margin &&
            at.Y > Main.screenPosition.Y - margin && at.Y < Main.screenPosition.Y + Main.screenHeight + margin;

        private static ref Mote New(Vector2 at, Vector2 velocity, float size, int life, int delay, byte kind)
        {
            ref Mote m = ref pool[count++];
            m.Pos = at;
            m.Vel = velocity;
            m.Age = -delay;
            m.Life = Math.Max(2, life);
            m.Size = size;
            m.Rot = 0f;
            m.Phase = Main.rand.NextFloat(MathHelper.TwoPi);
            m.Variant = (byte)Main.rand.Next(4);
            m.Kind = kind;
            m.Palette = 0;
            m.Shape = 0;
            m.Heat = 1f;
            return ref m;
        }

        /// <summary>
        /// A flame: a tongue pointing the way it goes (or a round puff, <paramref name="round"/>), <paramref name="size"/>
        /// world pixels across at its fullest, living <paramref name="life"/> ticks; <paramref name="heat"/> its strength
        /// (about a half: they are meant to pile up), <paramref name="lift"/> how hard it climbs. <paramref name="begun"/>
        /// is the share of its life it is born having lived (0 to 1): fire made far down a stream is born already
        /// orange or red, and lives only what is left (a flamethrower's, Violent Crimes).
        /// </summary>
        public static void Flame(Vector2 at, Vector2 velocity, float size, int life, FirePalette palette = FirePalette.Fire,
            float lift = 0.09f, float heat = 0.55f, int delay = 0, bool round = false, float begun = 0f)
        {
            if (!Ready || count >= Capacity)
                return;

            ref Mote m = ref New(at, velocity, size, life, delay, KindFlame);

            if (begun > 0f)
                m.Age = Math.Min(begun, 0.95f) * m.Life;

            m.Palette = (byte)palette;
            m.Spin = Main.rand.NextFloat(-0.04f, 0.04f);
            m.Lift = lift;
            m.Sway = Main.rand.NextFloat(0.04f, 0.11f);
            m.Heat = heat;
            m.Shape = (byte)(round ? 1 : 0);
        }

        /// <summary>Smoke: soot, laid over the world, swelling as it climbs and thins.</summary>
        public static void Smoke(Vector2 at, Vector2 velocity, float size, int life, int delay = 0, float thickness = 1f)
        {
            if (!Ready || count >= Capacity)
                return;

            ref Mote m = ref New(at, velocity, size, life, delay, KindSmoke);
            m.Rot = Main.rand.NextFloat(MathHelper.TwoPi);
            m.Spin = Main.rand.NextFloat(-0.02f, 0.02f);
            m.Lift = 0.035f;
            m.Sway = Main.rand.NextFloat(0.02f, 0.05f);
            m.Heat = thickness * Main.rand.NextFloat(0.8f, 1.1f);
        }

        /// <summary>Gas: the Spazmatism's green, a dark body under a paler crown, hanging where it is left.</summary>
        public static void Gas(Vector2 at, Vector2 velocity, float size, int life, float thickness = 1f)
        {
            if (!Ready || count >= Capacity)
                return;

            ref Mote m = ref New(at, velocity, size, life, 0, KindGas);
            m.Rot = Main.rand.NextFloat(MathHelper.TwoPi);
            m.Spin = Main.rand.NextFloat(-0.015f, 0.015f);
            m.Lift = 0.004f;
            m.Sway = Main.rand.NextFloat(0.02f, 0.05f);
            m.Heat = thickness * Main.rand.NextFloat(0.8f, 1.1f);
        }

        /// <summary>A spark: one square of the grid, hot, thrown.</summary>
        public static void Spark(Vector2 at, Vector2 velocity, int life, FirePalette palette = FirePalette.Fire, float lift = 0.03f)
        {
            if (!Ready || count >= Capacity)
                return;

            ref Mote m = ref New(at, velocity, 2f, life, 0, KindSpark);
            m.Palette = (byte)palette;
            m.Spin = 0f;
            m.Lift = lift;
            m.Sway = 0.1f;
        }

        // ---- the emitters -------------------------------------------------------------------------------------------------

        private static int Share(float many)
        {
            many *= Density;
            int whole = (int)many;
            return Main.rand.NextFloat() < many - whole ? whole + 1 : whole;
        }

        /// <summary>
        /// A fire burning at a place, asked for every tick it burns: flames off a patch <paramref name="radius"/> across
        /// (about <paramref name="power"/> a tick), smoke off the top of it, a spark now and then.
        /// </summary>
        public static void Burn(Vector2 at, float radius, float power, FirePalette palette = FirePalette.Fire, bool smokes = true)
        {
            if (!Ready || !Near(at))
                return;

            for (int n = Share(power); n > 0; n--)
            {
                Vector2 off = Main.rand.NextVector2Circular(radius, radius * 0.5f);
                Flame(at + off, new Vector2(Main.rand.NextFloat(-0.45f, 0.45f), -Main.rand.NextFloat(1f, 2.6f)),
                    Main.rand.NextFloat(0.8f, 1.35f) * (16f + radius * 1.7f), Main.rand.Next(26, 45), palette, 0.1f, 0.5f);
            }

            if (smokes && Main.rand.NextFloat() < 0.5f * power * Density)
                Smoke(at + new Vector2(Main.rand.NextFloat(-radius, radius) * 1.2f, -radius * 3f - Main.rand.NextFloat(10f, 40f)),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.2f, 2.2f)), Main.rand.NextFloat(1f, 1.9f) * (14f + radius * 1.4f), Main.rand.Next(46, 81));

            if (Main.rand.NextFloat() < 0.16f * power)
                Spark(at + new Vector2(Main.rand.NextFloat(-radius, radius), -Main.rand.NextFloat(0f, radius * 2f)),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1.5f, 4f)), Main.rand.Next(22, 41), palette);
        }

        /// <summary>
        /// A column of fire from a place along a direction, asked for every tick it burns: a heart of round flames
        /// rushing down its line, a sheath of tongues peeling off it and cooling. Only what is on screen is made.
        /// <paramref name="width"/> is how wide it is to look at (and to be hurt by).
        /// </summary>
        public static void Jet(Vector2 from, Vector2 direction, float length, float width, FirePalette palette = FirePalette.Fire, float density = 1f)
        {
            if (!Ready)
                return;

            density *= Density;
            float step = Math.Max(14f, width * 0.42f) / Math.Max(0.1f, density);
            Vector2 side = new(-direction.Y, direction.X);

            // A thin one's flames a little over its width, so they run together.
            float big = 1.25f - 0.4f * MathHelper.Clamp((width - 48f) / 48f, 0f, 1f);

            for (float k = Main.rand.NextFloat(step); k < length; k += step)
            {
                Vector2 at = from + direction * k;

                if (!Near(at, 120f))
                    continue;

                if (Main.rand.NextFloat() >= Math.Min(1f, (length - k) / (length * 0.2f)))
                    continue;

                // Its heart.
                Flame(at + side * Main.rand.NextFloat(-0.2f, 0.2f) * width, direction * Main.rand.NextFloat(7f, 12f),
                    width * big * Main.rand.NextFloat(0.8f, 1.1f), Main.rand.Next(8, 14), palette, 0f, 0.5f, round: true);

                // Its sheath.
                if (Main.rand.NextFloat() < 0.7f)
                {
                    float way = Main.rand.NextBool() ? 1f : -1f;
                    Flame(at + side * way * Main.rand.NextFloat(0.1f, 0.5f) * width,
                        direction * Main.rand.NextFloat(2f, 5.5f) + side * way * Main.rand.NextFloat(0.3f, 1.5f),
                        width * big * Main.rand.NextFloat(0.7f, 1.05f), Main.rand.Next(16, 29), palette, 0.05f, 0.42f);
                }

                if (Main.rand.NextBool(20))
                    Spark(at, direction * Main.rand.NextFloat(2f, 6f) + side * Main.rand.NextFloat(-3f, 3f), Main.rand.Next(16, 31), palette);
            }
        }

        /// <summary>
        /// Fire left behind something that moves, asked for every tick with where it was and where it is: flames along
        /// the way, carried a little after it, smoke following.
        /// </summary>
        public static void Trail(Vector2 from, Vector2 to, float size, FirePalette palette = FirePalette.Fire, float density = 1f, bool smokes = true)
        {
            if (!Ready || !Near(to))
                return;

            Vector2 way = to - from;
            int many = Math.Max(1, Share(way.Length() / (size * 0.3f) * density));

            for (int i = 0; i < many; i++)
                Flame(Vector2.Lerp(from, to, Main.rand.NextFloat()) + Main.rand.NextVector2Circular(size * 0.25f, size * 0.25f),
                    way * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f), size * Main.rand.NextFloat(0.7f, 1.2f), Main.rand.Next(18, 35), palette, 0.06f, 0.55f);

            if (smokes && Main.rand.NextFloat() < 0.5f * Density)
                Smoke(from + Main.rand.NextVector2Circular(size * 0.3f, size * 0.3f), new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1f)),
                    size * Main.rand.NextFloat(0.9f, 1.5f), Main.rand.Next(40, 67), 14);
        }

        /// <summary>
        /// A puff of fire thrown one way, once (a muzzle's, a kick's): flames in a cone along a direction, a few sparks
        /// with them. Not a burst: it goes where it is sent.
        /// </summary>
        public static void Puff(Vector2 at, Vector2 direction, float size, FirePalette palette = FirePalette.Fire, float power = 1f, float spread = 0.5f)
        {
            if (!Ready || !Near(at))
                return;

            direction = direction.SafeNormalize(-Vector2.UnitY);
            float thrown = MathF.Sqrt(power);

            for (int n = Share(14f * power); n > 0; n--)
                Flame(at + Main.rand.NextVector2Circular(size * 0.15f, size * 0.15f), direction.RotatedByRandom(spread) * Main.rand.NextFloat(2f, 9f) * thrown,
                    size * Main.rand.NextFloat(0.5f, 1f), Main.rand.Next(14, 31), palette, 0.06f, 0.55f);

            for (int n = Share(6f * power); n > 0; n--)
                Spark(at, direction.RotatedByRandom(spread * 1.4f) * Main.rand.NextFloat(4f, 12f), Main.rand.Next(14, 31), palette);
        }

        /// <summary>
        /// A burst of fire, once: a heart of flames that stay and climb, tongues thrown out of it, smoke rolling up after,
        /// sparks. <paramref name="radius"/> about how far its heart reaches.
        /// </summary>
        public static void Burst(Vector2 at, float radius, FirePalette palette = FirePalette.Fire, float power = 1f, bool smokes = true)
        {
            if (!Ready || !Near(at, 480f))
                return;

            for (int n = Share(30f * power); n > 0; n--)
            {
                Vector2 dir = Main.rand.NextVector2Unit();
                Flame(at + dir * radius * 0.55f * MathF.Sqrt(Main.rand.NextFloat()) * new Vector2(1f, 0.8f),
                    new Vector2(dir.X * Main.rand.NextFloat(0.2f, 1.6f), dir.Y * Main.rand.NextFloat(0.2f, 1.2f) - Main.rand.NextFloat(0.2f, 1f)),
                    Main.rand.NextFloat(0.55f, 1f) * radius, Main.rand.Next(30, 59), palette, 0.07f, 0.5f, Main.rand.Next(0, 6));
            }

            float thrown = MathF.Sqrt(power);

            for (int n = Share(30f * power); n > 0; n--)
            {
                Vector2 dir = Main.rand.NextVector2Unit();
                Flame(at, new Vector2(dir.X, dir.Y * 0.85f) * Main.rand.NextFloat(3f, 8.5f) * thrown + new Vector2(0f, -0.6f),
                    Main.rand.NextFloat(0.35f, 0.7f) * radius, Main.rand.Next(16, 33), palette, 0.08f, 0.6f);
            }

            if (smokes)
            {
                for (int n = Share(16f * power); n > 0; n--)
                {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    Smoke(at + new Vector2(dir.X, dir.Y * 0.7f) * radius * Main.rand.NextFloat(0.2f, 0.7f) + new Vector2(0f, -radius * 0.2f),
                        new Vector2(dir.X * 0.9f, -Main.rand.NextFloat(0.8f, 2.4f)), radius * Main.rand.NextFloat(0.7f, 1.3f), Main.rand.Next(60, 111), Main.rand.Next(10, 35));
                }
            }

            for (int n = Share(22f * power); n > 0; n--)
                Spark(at, Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 11f) + new Vector2(0f, -2f), Main.rand.Next(24, 47), palette);
        }

        /// <summary>
        /// One of its round puffs drawn by hand into a batch open on the half-size canvas (PADPixelCanvas.Queue): for a
        /// cloud with a body of its own. <paramref name="colour"/> as the pass wants it (laid over: multiplied by its own
        /// alpha).
        /// </summary>
        public static void DrawPuff(SpriteBatch batch, Vector2 world, float wide, Color colour, float rotation, int variant)
        {
            if (Main.dedServ)
                return;

            atlas ??= ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/FireAtlas", AssetRequestMode.ImmediateLoad);
            batch.Draw(atlas.Value, world - Main.screenPosition, new Rectangle((variant & 3) * Cell, Cell, Cell, Cell), colour, rotation, new Vector2(Cell * 0.5f), wide / Cell, SpriteEffects.None, 0f);
        }

        // ---- the tick ---------------------------------------------------------------------------------------------------------

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || pool == null || count == 0)
                return;

            int kept = 0, lights = 0;
            uint tick = Main.GameUpdateCount;

            for (int i = 0; i < count; i++)
            {
                ref Mote m = ref pool[i];
                m.Age += 1f;

                if (m.Age >= m.Life)
                    continue;

                if (m.Age > 0f)
                {
                    switch (m.Kind)
                    {
                        case KindFlame:
                            m.Vel.Y -= m.Lift;
                            m.Vel *= 0.955f;
                            m.Vel.X += MathF.Sin(m.Age * 0.23f + m.Phase) * m.Sway;

                            // A share of the young ones light the world.
                            if ((i + tick) % 12 == 0 && lights < 40 && m.Age < m.Life * 0.6f)
                            {
                                lights++;
                                Color c = ramps[m.Palette][(int)(m.Age / m.Life * 63f)];
                                float k = m.Size * (1f / 60f) / 255f;
                                Lighting.AddLight(m.Pos, c.R * k, c.G * k, c.B * k);
                            }

                            break;

                        case KindSpark:
                            m.Vel.Y -= m.Lift;
                            m.Vel *= 0.97f;
                            m.Vel.X += MathF.Sin(m.Age * 0.5f + m.Phase) * m.Sway;
                            break;

                        default:
                            m.Vel.Y -= m.Lift;
                            m.Vel *= 0.94f;
                            m.Vel.X += MathF.Sin(m.Age * 0.11f + m.Phase) * m.Sway;
                            break;
                    }

                    m.Pos += m.Vel;
                    m.Rot += m.Spin;
                }

                // The gaps the dead leave are closed as it goes, the rest kept in their order.
                if (kept != i)
                    pool[kept] = m;

                kept++;
            }

            count = kept;
        }

        // ---- the frame --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Everything of one pass into the batch PADPixelCanvas has open on its canvas: the flames and sparks into its
        /// light, the smoke and gas into what is laid over the world.
        /// </summary>
        internal static void Draw(SpriteBatch batch, bool additive)
        {
            if (pool == null || count == 0)
                return;

            atlas ??= ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/FireAtlas", AssetRequestMode.ImmediateLoad);
            Texture2D texture = atlas.Value;

            Vector2 screen = Main.screenPosition;
            float left = screen.X - 96f, right = screen.X + Main.screenWidth + 96f;
            float top = screen.Y - 96f, bottom = screen.Y + Main.screenHeight + 96f;
            Vector2 middle = new(Cell * 0.5f);
            Rectangle white = new(Cell * 3, Cell * 2, 2, 2);

            for (int i = 0; i < count; i++)
            {
                ref Mote m = ref pool[i];

                if (m.Age <= 0f || (m.Kind == KindFlame || m.Kind == KindSpark) != additive)
                    continue;

                if (m.Pos.X < left || m.Pos.X > right || m.Pos.Y < top || m.Pos.Y > bottom)
                    continue;

                float life = m.Age / m.Life;
                int t = (int)(life * 63f);
                Vector2 at = m.Pos - screen;

                switch (m.Kind)
                {
                    case KindFlame:
                        {
                            Color c = ramps[m.Palette][t];
                            Color colour = new((int)(c.R * m.Heat), (int)(c.G * m.Heat), (int)(c.B * m.Heat), 255);
                            float size = m.Size * sizeCurve[t] * (1f / Cell);

                            if (m.Shape == 1)
                            {
                                batch.Draw(texture, at, new Rectangle(m.Variant * Cell, Cell, Cell, Cell), colour, m.Rot, middle, size, SpriteEffects.None, 0f);
                            }
                            else
                            {
                                float speed = m.Vel.Length();
                                float turned = speed > 0.15f ? MathF.Atan2(m.Vel.Y, m.Vel.X) + MathHelper.PiOver2 : 0f;
                                batch.Draw(texture, at, new Rectangle(m.Variant * Cell, 0, Cell, Cell), colour, turned, middle,
                                    new Vector2(size, size * (1f + Math.Min(0.6f, speed * 0.05f))), SpriteEffects.None, 0f);
                            }

                            break;
                        }

                    case KindSpark:
                        {
                            Color c = ramps[m.Palette][(int)((0.1f + 0.5f * life) * 63f)];
                            float k = Math.Min(1f, 1.3f * (1f - life * life * life));
                            batch.Draw(texture, at, white, new Color((int)(c.R * k), (int)(c.G * k), (int)(c.B * k), 255), 0f, Vector2.One, 1f, SpriteEffects.None, 0f);
                            break;
                        }

                    case KindSmoke:
                        {
                            float size = m.Size * smokeSize[t] * (1f / Cell);
                            batch.Draw(texture, at, new Rectangle(m.Variant * Cell, Cell, Cell, Cell), SootColour * Math.Min(1f, smokeAlpha[t] * m.Heat), m.Rot, middle, size, SpriteEffects.None, 0f);
                            break;
                        }

                    default:
                        {
                            float wide = m.Size * smokeSize[t];
                            float size = wide * (1f / Cell);
                            float o = Math.Min(1f, gasAlpha[t] * m.Heat);
                            Rectangle source = new(m.Variant * Cell, Cell, Cell, Cell);
                            batch.Draw(texture, at, source, GasBody * o, m.Rot, middle, size, SpriteEffects.None, 0f);
                            batch.Draw(texture, at + new Vector2(wide * 0.08f, -wide * 0.16f), source, GasCrown * (o * 0.55f), m.Rot, middle, size * 0.62f, SpriteEffects.None, 0f);
                            break;
                        }
                }
            }
        }
    }
}
