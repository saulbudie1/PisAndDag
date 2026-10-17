using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// What the lighthouse island is handed by the place it stands in (VastOceanIsland), so that
    /// the builder asks nothing of the mod's own tiles or of the world past its canvas.
    /// </summary>
    public sealed class IslandHooks
    {
        /// <summary>How many rows under the sea's surface row the sea floor may be looked for (the world's bottom, less a margin).</summary>
        public int Deepest;

        /// <summary>The lighthouse's lens (LighthouseLens); 0 to leave it out.</summary>
        public ushort Lens;

        /// <summary>
        /// One of the sea's own living things (SeaLife) put down: its middle column, the row it
        /// stands in (the one over its ground), its kind and its picture; true if it went. Null to
        /// put none.
        /// </summary>
        public Func<int, int, int, int, bool> Life;

        /// <summary>The kinds of SeaLife the reef uses, by their numbers (SeaLife.Kind): an anemone and a cup sponge; -1 to leave either out.</summary>
        public int Anemone = -1, CupSponge = -1;

        /// <summary>A palm grown from the sand at this column and ground row, as the game grows one (WorldGen.GrowPalmTree); null for none.</summary>
        public Func<int, int, bool> Palm;

        /// <summary>A stand of kelp (the game's seaweed) set on the ground at this column and ground row and grown to so many tall at most; how tall it grew. Null for none.</summary>
        public Func<int, int, int, int> Kelp;

        /// <summary>The game's chimney (TileID.Chimney) whose left column and bottom row these are, set smoking (its second state, as a switch on its wire would set it); null to leave it.</summary>
        public Action<int, int> Smoke;
    }

    /// <summary>
    /// The lighthouse island of the Vast Ocean (VastOceanIsland, by request: "1000- 1300 tiles to
    /// the left is an island floating in the sea, around 300 tile wide, having a large light house
    /// on the rock like this. With little details of course, paint and ambience elements", "I like
    /// the jagged rocks", "Take especially long time for this structure", "2nd pic from reddit
    /// looks beautiful"; chosen: "Floats", "Red &amp; white, rooms").
    ///
    /// A mass of rock floating in the sea, its underside hanging down into the water in spikes,
    /// two great chains going down from it into the dark to anchors on the floor far below. From
    /// west to east: a sea stack off its end, walled behind with its own stone (by request: "make
    /// sure the stone pillar here is walled and leave a space to squeeze through"); an arch the sea
    /// runs through, a cave behind it, and a way in for one to squeeze through the arch's west leg
    /// from the shelf at the stack's foot; a headland of jagged crags and pinnacles; the plateau
    /// with the lighthouse on it, raised on its plinth, the oil house east of it, the flagstaff to
    /// the west; a stair cut down the rock to a beach of pale sand with palms; a fisherman's house
    /// on stilts over the water, its roof blue; a pier running out from it, a skiff tied at its end
    /// and a sloop riding at anchor beyond; and under the water a shelf of sand and coral, kelp
    /// standing on it.
    ///
    /// The lighthouse (LighthouseTower): on a plinth of white stone with a colonnade of pillars
    /// and statues in its face, five storeys of red brick and white in bands, every one of them
    /// fitted out for the keepers' work and life, a stair turning up a shaft through the middle of
    /// it from floor to floor, a gallery round its top, and the lantern on it, a lens of glass under
    /// a red dome with a vane on it.
    ///
    /// Plan x runs from the island's west end, y rows from the sea's surface row (negative up), as
    /// the canvas the place hands in has it. Built after the sea is poured in: every cell it
    /// clears is left dry, and the place pours the sea back into what is open under its surface.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        public static void BuildLighthouseIsland(Canvas canvas, IslandHooks hooks) => new GraceBuilder(canvas, null).Isle(hooks);

        /// <summary>The game's test for a house over the whole island again: how many rooms would pass (none, once it is built). For the preview.</summary>
        public static int IslandHouses(Canvas canvas)
        {
            new GraceBuilder(canvas, null).Unhouse(IsleX0, IsleY0, IsleX1, IsleY1);
            return Unhoused;
        }

        // ------------------------------------------------------------ the plan

        /// <summary>Every column and row the island's things may touch, the chains to the floor apart.</summary>
        public const int IsleX0 = -40, IsleX1 = 345, IsleY0 = -125, IsleY1 = 150;

        /// <summary>The arch's west leg, between the shelf at the stack's foot and the arch; the passage through it, its floor row and its roof's lowest row.</summary>
        private const int IsleLegX0 = -11, IsleLegX1 = -4, IsleSqueezeFloor = -1, IsleSqueezeTop = -4;

        /// <summary>The top row of the plateau the lighthouse stands on, and its ends.</summary>
        private const int Plateau = -30;
        private const int PlateauX0 = 92, PlateauX1 = 178;

        /// <summary>The lighthouse's middle column.</summary>
        private const int LightX = 123;

        /// <summary>The stair down the east side: from the plateau's end to the beach, a step a column.</summary>
        private const int StairX0 = 179, StairX1 = 203;

        /// <summary>The fisherman's house on its stilts, and the pier from it.</summary>
        private const int HutX0 = 228, HutX1 = 252, HutFloor = -8;
        private const int PierX0 = 257, PierX1 = 292, PierDeck = -4;

        private IslandHooks isle;

        /// <summary>The rock's top row and bottom row in each column (int.MaxValue / int.MinValue where there is none), by column less IsleX0.</summary>
        private int[] isleTop, isleBottom;

        /// <summary>Cells to be left square by the weathering: under the lighthouse, the stair, the yards' level ground.</summary>
        private readonly HashSet<long> isleSquare = new HashSet<long>();

        private static long IsleKey(int x, int y) => ((long)x << 32) ^ (uint)y;

        private int IsleTopAt(int x) => x < IsleX0 || x > IsleX1 ? int.MaxValue : isleTop[x - IsleX0];

        private int IsleBottomAt(int x) => x < IsleX0 || x > IsleX1 ? int.MinValue : isleBottom[x - IsleX0];

        /// <summary>The lens's top left tile, as the plan has it: the place finds it there to draw its beam (VastOceanIsland).</summary>
        public const int IsleLensX = LightX - 1, IsleLensY = LightDeckY - 6;

        private void Isle(IslandHooks hooks)
        {
            isle = hooks;

            // The rock: its mass, its holes, its soil, the spikes under it, all worn and coloured.
            IsleRock();
            IsleCarve();
            IsleSoil();
            IsleUnderside();
            IsleTidy();
            IsleWeather();
            IslePaintRock();
            IsleWallStack();

            // What was built on it.
            IsleTower();
            IsleOilHouse();
            IsleYards();
            IsleStair();
            IsleHut();
            IslePier();
            IsleSkiff(PierX1 + 2, 1);
            IsleSloop(PierX1 + 20);

            // What grows and lies about.
            IsleMounds();
            IsleReef();
            IsleChains();
            IsleGreen();
            IsleVines();

            FillBehindCuts(IsleX0, IsleY0, IsleX1, IsleY1);
            Backfill(IsleX0, IsleY0, IsleX1, IsleY1);

            // Last of all: no room of it will pass for a house.
            Unhouse(IsleX0, IsleY0, IsleX1, IsleY1);
        }

        // ------------------------------------------------------------ the rock

        /// <summary>The top of the rock, west to east, at its turning points: the arch's legs, the crags' peaks, the plateau, the stair, the beach and the shelf under the water.</summary>
        private static readonly (int x, int y)[] IsleSkyline =
        {
            (-12, 3), (-11, -14), (-9, -20), (-6, -23), (-2, -21), (3, -22), (8, -23), (12, -25),
            (15, -29), (19, -35), (24, -45), (28, -41), (34, -52), (38, -61), (42, -53), (46, -47),
            (50, -52), (55, -45), (60, -56), (64, -50), (69, -43), (75, -38), (82, -34), (88, -31),
            (PlateauX0, Plateau), (PlateauX1, Plateau), (StairX1, -6), (208, -5), (216, -3), (224, -1),
            (229, 0), (233, 2), (240, 6), (256, 8), (280, 10), (300, 12), (311, 15), (318, 19),
        };

        /// <summary>The bottom of the rock, the island's underside, west to east.</summary>
        private static readonly (int x, int y)[] IsleKeel =
        {
            (-12, 22), (0, 30), (20, 46), (40, 64), (70, 86), (100, 99), (LightX, 106), (150, 101),
            (180, 89), (210, 71), (240, 52), (270, 38), (300, 27), (318, 20),
        };

        /// <summary>A line through points, eased between each pair (a cosine), flat past its ends.</summary>
        private static double Through((int x, int y)[] points, double x)
        {
            if (x <= points[0].x)
                return points[0].y;

            for (int i = 0; i + 1 < points.Length; i++)
            {
                var (ax, ay) = points[i];
                var (bx, by) = points[i + 1];

                if (x > bx)
                    continue;

                double t = (x - ax) / (double)(bx - ax);

                // The stair is straight: a step a column.
                if (ax == PlateauX1 && bx == StairX1)
                    return ay + (by - ay) * t;

                t = (1 - Math.Cos(t * Math.PI)) * 0.5;
                return ay + (by - ay) * t;
            }

            return points[points.Length - 1].y;
        }

        private void IsleRock()
        {
            int n = IsleX1 - IsleX0 + 1;
            isleTop = new int[n];
            isleBottom = new int[n];
            int seed = c.Next(1, 1 << 20);

            for (int x = IsleX0; x <= IsleX1; x++)
            {
                int i = x - IsleX0;
                isleTop[i] = int.MaxValue;
                isleBottom[i] = int.MinValue;

                if (x < -12 || x > 318)
                    continue;

                double top = Through(IsleSkyline, x);
                double bottom = Through(IsleKeel, x) + 4.0 * (Noise(x, 0, 9.0, seed + 3) - 0.5);

                // The crags: teeth along their ridges, sharp, each its own height.
                if (x >= 14 && x <= 80)
                {
                    double tooth = Noise(x, 0, 2.6, seed + 5);
                    double ridge = Noise(x, 0, 7.0, seed + 6);
                    top -= Math.Pow(tooth, 3) * 9.0 + ridge * 3.0;
                }
                else if (x < -6 || x > 80 && x < PlateauX0 || x > StairX1 + 3 && x < 229)
                {
                    // Elsewhere a little rough: the arch's west leg, the saddle; the beach a little
                    // less. Not the arch's bridge, the plateau or the stair, which are laid as they are.
                    top -= (x > StairX1 ? 0.6 : 1.2) * Noise(x, 0, 3.0, seed + 7);
                }

                isleTop[i] = (int)Math.Round(top);
                isleBottom[i] = (int)Math.Round(bottom);

                for (int y = isleTop[i]; y <= isleBottom[i]; y++)
                    c.Block(x, y, IsleStone(x, y, seed));
            }

            // The sea stack off the west end, on a ledge under the water joined to the island's foot.
            for (int x = -30; x <= -12; x++)
            {
                int ledge = 3 + (int)Math.Round(2.0 * Noise(x, 0, 4.0, seed + 9));
                int foot = 16 + (int)Math.Round(4.0 * Noise(x, 0, 6.0, seed + 10)) - (x < -26 ? (-26 - x) * 2 : 0);
                for (int y = ledge; y <= foot; y++)
                    c.Block(x, y, IsleStone(x, y, seed));
                IsleSetTop(x, ledge, foot);
            }

            IsleSpire(-21, 4, 36, 7.5, 0.10, seed);

            // A low shelf of rock at the stack's foot, just over the water, for the pools.
            for (int x = -17; x <= -12; x++)
            {
                for (int y = -1; y <= 3; y++)
                    c.Block(x, y, IsleStone(x, y, seed));
                IsleSetTop(x, -1, Math.Max(IsleBottomAt(x), 3));
            }

            // Pinnacles on the crags, leaning out of the ridge.
            foreach (var (px, high, width, lean) in new[] { (22, 12, 3.5, -0.12), (31, 9, 3.0, 0.08), (44, 14, 3.5, 0.05), (53, 10, 3.0, -0.10), (66, 13, 4.0, 0.14), (71, 7, 2.5, 0.0) })
                IsleSpire(px, IsleTopAt(px) + 2, high, width, lean, seed + px);
        }

        /// <summary>The rock of the island: grey stone, with bands of the dark granite through it slanting a little, and the underside dark.</summary>
        private static ushort IsleStone(int x, int y, int seed)
        {
            double band = Noise(x + y * 3, y, 5.0, seed + 11);
            if (y > 30 && Noise(x, y, 8.0, seed + 12) < 0.55 + (y - 30) / 120.0)
                return TileID.Granite;

            return band > 0.68 ? TileID.Granite : TileID.Stone;
        }

        /// <summary>The rock's top and bottom in a column, where something is laid on it after the profile.</summary>
        private void IsleSetTop(int x, int top, int bottom)
        {
            if (x < IsleX0 || x > IsleX1)
                return;

            int i = x - IsleX0;
            isleTop[i] = Math.Min(isleTop[i], top);
            isleBottom[i] = Math.Max(isleBottom[i], bottom);
        }

        /// <summary>
        /// A spire of rock from its foot up, narrowing to a point and leaning as it goes (more toward
        /// its tip), laid half a row at a time as a disc that narrows: the stack, and the crags'
        /// pinnacles.
        /// </summary>
        private void IsleSpire(int x, int foot, double high, double width, double lean, int seed)
        {
            int steps = (int)(high * 2);

            for (int s = 0; s <= steps; s++)
            {
                double f = s / (double)steps;
                double cx = x + 0.5 + lean * high * f * f;
                double cy = foot - high * f;
                double r = Math.Max(0.45, width * 0.5 * Math.Pow(1 - f, 0.75)) + 0.6 * (Noise(x, s, 3.0, seed + 13) - 0.5);

                for (int px = (int)Math.Floor(cx - r - 1); px <= (int)Math.Ceiling(cx + r + 1); px++)
                {
                    double dx = px + 0.5 - cx;
                    if (Math.Abs(dx) > r)
                        continue;

                    int py = (int)Math.Round(cy);
                    if (c.TypeAt(px, py) < 0)
                        c.Block(px, py, IsleStone(px, py, seed));
                    IsleSetTop(px, py, py);
                }
            }
        }

        // ------------------------------------------------------------ the holes in it

        /// <summary>
        /// The arch the sea runs through, between its two legs; the sea cave behind it, in under the
        /// crags, a ledge at its back over the water with something left on it; and the pools on the
        /// shelf at the stack's foot.
        /// </summary>
        private void IsleCarve()
        {
            // The arch: ten wide, its head round, from a ledge under the water to ten over it.
            for (int x = -3; x <= 6; x++)
            {
                double u = (x - 1.5) / 5.5;
                int head = -6 - (int)Math.Round(4.5 * Math.Sqrt(Math.Max(0, 1 - u * u)));

                for (int y = head; y <= 5; y++)
                    c.Erase(x, y);
            }

            // The cave: a passage in from the arch's east side, then a chamber, low over the water.
            var path = new List<(double x, double y)> { (6, 0.5), (11, 0), (16, -0.5), (21, -1) };
            foreach (var (px, py) in path)
                IsleHollow(px, py, 3.0, 4.2);

            for (int x = 18; x <= 36; x++)
            {
                double u = (x - 27) / 9.5;
                int up = -1 - (int)Math.Round(6.5 * Math.Sqrt(Math.Max(0, 1 - u * u)));
                int down = 2 + (int)Math.Round(4.0 * Math.Sqrt(Math.Max(0, 1 - u * u)));
                for (int y = up; y <= down; y++)
                    c.Erase(x, y);
            }

            // The ledge at its back: a shelf of rock two over the water, the chamber's east end.
            for (int x = 31; x <= 37; x++)
                for (int y = -2; y <= 3; y++)
                    c.Block(x, y, TileID.Stone);

            c.Mark("Isle.Cave", 18, -8, 20, 12);

            // The cave's back: dark wet rock, green with weed and moss low down where the sea
            // reaches, so that it reads as a cave and not as a second hole through the rock. The
            // arch has none: the sky shows through it.
            int seed = c.Next(1, 1 << 20);
            for (int x = 7; x <= 38; x++)
            {
                for (int y = -14; y <= 10; y++)
                {
                    if (c.TypeAt(x, y) >= 0)
                        continue;

                    double n = Noise(x, y, 4.0, seed);
                    bool wet = y >= -3 + (int)Math.Round(2.0 * Noise(x, 0, 3.0, seed + 1));
                    ushort wall = wet ? (n < 0.6 ? WallID.CaveUnsafe : WallID.Cave4Unsafe) : n < 0.22 ? WallID.RocksUnsafe1 : WallID.Cave4Unsafe;
                    c.Wall(x, y, wall);
                }
            }

            // A way in for one to squeeze through (by request: "leave a space to squeeze
            // through"): a passage three high through the arch's west leg, from the shelf at the
            // stack's foot to the arch, its floor the shelf's own height, its roof uneven; walled
            // as the rock is (IsleWallStack), so it reads as a passage and not a hole.
            for (int x = IsleLegX0; x <= IsleLegX1; x++)
            {
                int roof = Hash01(x, 0, seed + 3) < 0.35 ? IsleSqueezeTop - 1 : IsleSqueezeTop;
                for (int y = roof; y < IsleSqueezeFloor; y++)
                    c.Erase(x, y);
            }

            // The sea in the arch and the cave, standing as it stands outside: they open on it
            // through the rock behind, where the picture cannot show it.
            for (int x = -3; x <= 38; x++)
                for (int y = 0; y <= 12; y++)
                    if (c.TypeAt(x, y) < 0)
                        c.Liquid(x, y, (byte)(y == 0 ? 127 : 255), LiquidID.Water);

            // The pools on the shelf at the stack's foot: water left in the rock by the waves.
            foreach (var (px, w, d) in new[] { (-16, 2, 1), (-13, 2, 1) })
            {
                for (int x = px; x < px + w; x++)
                {
                    for (int y = -1; y < -1 + d; y++)
                    {
                        c.Erase(x, y);
                        c.Liquid(x, y, 255, LiquidID.Water);
                    }
                }
            }
        }

        /// <summary>An oval of rock taken out about a point, so wide and so tall.</summary>
        private void IsleHollow(double cx, double cy, double rx, double ry)
        {
            for (int x = (int)Math.Floor(cx - rx - 1); x <= (int)Math.Ceiling(cx + rx + 1); x++)
            {
                for (int y = (int)Math.Floor(cy - ry - 1); y <= (int)Math.Ceiling(cy + ry + 1); y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy < 1)
                        c.Erase(x, y);
                }
            }
        }

        // ------------------------------------------------------------ the soil

        /// <summary>
        /// Earth and grass over the plateau and the saddle west of it, where the rock lies gentle;
        /// sand over the beach and the shelf under the water, firm sand under it so none of it falls;
        /// the plateau made level, and kept square for what stands on it.
        /// </summary>
        private void IsleSoil()
        {
            for (int x = 70; x <= PlateauX1; x++)
            {
                int top = IsleTopAt(x);
                if (top == int.MaxValue)
                    continue;

                bool gentle = Math.Abs(IsleTopAt(x - 2) - IsleTopAt(x + 2)) <= 3;
                if (!gentle && x < PlateauX0)
                    continue;

                int deep = x >= PlateauX0 ? 3 : 2;
                for (int y = top; y < top + deep; y++)
                    c.Block(x, y, y == top ? TileID.Grass : TileID.Dirt);

                if (x >= PlateauX0)
                    for (int y = top - 1; y <= top + 1; y++)
                        isleSquare.Add(IsleKey(x, y));
            }

            for (int x = StairX1 + 1; x <= 318; x++)
            {
                int top = IsleTopAt(x);
                if (top == int.MaxValue)
                    continue;

                int sand = x < 229 ? 4 : 2;
                for (int y = top; y < top + sand; y++)
                    c.Block(x, y, TileID.Sand);
                for (int y = top + sand; y < top + sand + 3; y++)
                    c.Block(x, y, TileID.HardenedSand);
            }
        }

        // ------------------------------------------------------------ weathering

        /// <summary>Ground of the island's own: rock, earth, grass, sand.</summary>
        private static bool IsleNatural(int t) =>
            t == TileID.Stone || t == TileID.Granite || t == TileID.Dirt || t == TileID.Grass || t == TileID.Sand
            || t == TileID.HardenedSand || t == TileID.Coralstone || t == TileID.GreenMoss;

        /// <summary>
        /// Every face of the island's ground worn to the game's slopes, as the game wears a face
        /// (Tile.SmoothSlope, 1.4.4, read in the IL): a step's top edge sloped toward the open side,
        /// an overhang's underside the same where it is overhung, a lone bump halved; but for what is
        /// kept square for something to stand on.
        /// </summary>
        private void IsleWeather()
        {
            var cuts = new List<(int x, int y, Cut cut)>();

            for (int x = IsleX0 + 1; x < IsleX1; x++)
            {
                for (int y = IsleY0 + 1; y < IsleY1; y++)
                {
                    int t = c.TypeAt(x, y);
                    if (!IsleNatural(t) || isleSquare.Contains(IsleKey(x, y)))
                        continue;

                    bool up = c.TypeAt(x, y - 1) >= 0;
                    bool upSolid = c.Solid(x, y - 1);
                    bool down = c.Solid(x, y + 1);
                    bool left = c.Solid(x - 1, y);
                    bool right = c.Solid(x + 1, y);
                    int mask = (up ? 8 : 0) | (down ? 4 : 0) | (left ? 2 : 0) | (right ? 1 : 0);

                    Cut cut = mask switch
                    {
                        10 when upSolid => Grace.Cut.UpRight,
                        9 when upSolid => Grace.Cut.UpLeft,
                        6 => Grace.Cut.DownRight,
                        5 => Grace.Cut.DownLeft,
                        4 => Grace.Cut.Half,
                        _ => Grace.Cut.Full,
                    };

                    if (cut != Grace.Cut.Full)
                        cuts.Add((x, y, cut));
                }
            }

            foreach (var (x, y, cut) in cuts)
                c.Reshape(x, y, cut);
        }

        // ------------------------------------------------------------ loose rock

        /// <summary>
        /// The rock's loose bits taken away: whatever ground is not joined to the island's mass,
        /// side or corner, through ground (a crumb of a pinnacle's tip the profile left floating).
        /// </summary>
        private void IsleTidy()
        {
            int w = IsleX1 - IsleX0 + 1, hgt = IsleY1 - IsleY0 + 1;
            var seen = new bool[w, hgt];
            var todo = new Stack<(int x, int y)>();
            todo.Push((LightX, 10));

            while (todo.Count > 0)
            {
                var (x, y) = todo.Pop();
                if (x < IsleX0 || x > IsleX1 || y < IsleY0 || y > IsleY1 || seen[x - IsleX0, y - IsleY0] || !IsleNatural(c.TypeAt(x, y)))
                    continue;

                seen[x - IsleX0, y - IsleY0] = true;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (dx != 0 || dy != 0)
                            todo.Push((x + dx, y + dy));
            }

            for (int x = IsleX0; x <= IsleX1; x++)
                for (int y = IsleY0; y <= IsleY1; y++)
                    if (!seen[x - IsleX0, y - IsleY0] && IsleNatural(c.TypeAt(x, y)))
                        c.Erase(x, y);
        }

        /// <summary>
        /// The sea stack, its ledge and the arch's west leg walled behind with their own stone (by
        /// request: "make sure the stone pillar here is walled"): stone behind stone, granite behind
        /// granite, painted as the rock in front of it is, so that where a face is hammered it shows
        /// rock and not the sky; and the passage through the leg walled the same.
        /// </summary>
        private void IsleWallStack()
        {
            for (int x = IsleX0; x <= IsleLegX1; x++)
            {
                for (int y = IsleY0; y <= IsleY1; y++)
                {
                    int t = c.TypeAt(x, y);
                    bool passage = x >= IsleLegX0 && y >= IsleSqueezeTop - 1 && y < IsleSqueezeFloor && t < 0;

                    if (t == TileID.Stone || t == TileID.GreenMoss)
                        c.Wall(x, y, WallID.Stone, c.PaintOf(x, y));
                    else if (t == TileID.Granite)
                        c.Wall(x, y, WallID.Granite, c.PaintOf(x, y));
                    else if (passage)
                        c.Wall(x, y, WallID.Stone);
                }
            }
        }

        // ------------------------------------------------------------ the rock's colours

        /// <summary>
        /// The rock as the sea leaves it: at the waterline a band of black (the black lichen and the
        /// algae of the splash zone), grey and white barnacles just over and under it, green weed
        /// below that; over the crags, now and then a patch of yellow and orange lichen, and moss on
        /// the shaded faces low on the cliffs; the stone faces of the cave green.
        /// </summary>
        private void IslePaintRock()
        {
            int seed = c.Next(1, 1 << 20);

            for (int x = IsleX0 + 1; x < IsleX1; x++)
            {
                for (int y = IsleY0 + 1; y < IsleY1; y++)
                {
                    int t = c.TypeAt(x, y);
                    if (t != TileID.Stone && t != TileID.Granite)
                        continue;

                    bool open = c.TypeAt(x, y - 1) < 0 || c.TypeAt(x, y + 1) < 0 || c.TypeAt(x - 1, y) < 0 || c.TypeAt(x + 1, y) < 0;
                    if (!open)
                        continue;

                    double n = Noise(x, y, 4.0, seed);
                    double fine = Hash01(x, y, seed + 1);

                    if (y >= -2 && y <= 1)
                        c.Paint(x, y, n < 0.8 ? PaintID.BlackPaint : PaintID.GrayPaint);
                    else if (y >= -5 && y < -2)
                        c.Paint(x, y, fine < 0.35 ? PaintID.GrayPaint : fine < 0.5 ? PaintID.WhitePaint : (byte)0);
                    else if (y > 1 && y <= 4)
                        c.Paint(x, y, fine < 0.4 ? PaintID.GrayPaint : PaintID.BlackPaint);
                    else if (y > 4 && y <= 26)
                        c.Paint(x, y, n < 0.55 ? PaintID.DeepGreenPaint : n < 0.75 ? PaintID.GreenPaint : (byte)0);
                    else if (y < -8 && n > 0.78 && fine < 0.55)
                        c.Paint(x, y, fine < 0.3 ? PaintID.YellowPaint : PaintID.OrangePaint);
                    else if (y < -6 && y > -30 && n < 0.18 && t == TileID.Stone && c.TypeAt(x, y - 1) >= 0)
                        c.Block(x, y, TileID.GreenMoss, 0, c.CutAt(x, y));
                }
            }

            // The cave's walls: green with the damp.
            for (int x = 6; x <= 38; x++)
                for (int y = -9; y <= 7; y++)
                    if (c.TypeAt(x, y) == TileID.Stone && Hash01(x, y, seed + 2) < 0.6)
                        c.Block(x, y, TileID.GreenMoss, 0, c.CutAt(x, y));
        }
    }
}
