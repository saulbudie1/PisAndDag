using System;
using System.Collections.Generic;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Vast Ocean's reefs, planned (VastOceanReef lays the plan down). By request: "from section 2- 3 you can create
    /// a clusters of coral reefs connected with walls", "red is cluster of coral reef islands and connected by walls in
    /// neon green", "from section 3 down theres still some but sparser and spareser and smaller islands"; "A few big
    /// fields".
    ///
    ///   the fields       three to five, through the second and third sections: one under the island, the rest spread
    ///                    along the sea and kept off the platform's water. In each, reef islands float, the biggest
    ///                    near its middle and smaller ones further out, and every one is joined to the field by a
    ///                    strand of coral grown over the water between them (walls): staghorn at its heart, the lace
    ///                    of sea fans at its edges and in spurs off it, reef rock where it meets an island.
    ///   further down     from the middle of the third section to the end of the sixth, lone islands, fewer and smaller
    ///                    the deeper they lie, some joined to their nearest by a strand.
    ///   an island        reef rock with coralline crust over it and rubble under it, a deep belly with drips of rock
    ///                    hanging from it; on its top, patches of star coral, mounds of brain coral, thickets of
    ///                    staghorn; shelves of plate coral off its ends; now and then a hollow in it. Its own life on
    ///                    its top and hanging under it (ReefPlan.Decor), sea fans grown up behind its top (walls),
    ///                    staghorn hanging from its underside (walls); reef rock walls behind it all.
    ///
    /// Only into open water: nothing is planned over what the place already has (the island's chains), and an island
    /// keeps its distance from it. Everything here is the plan alone, from the place's shape and what is open water in
    /// it, on a random of its own seeded from the world's: nothing asks for the game, so that it can be previewed
    /// outside it.
    /// </summary>
    public sealed class ReefPlan
    {
        public enum Block : byte { None, Rock, Rubble, Crust, Brain, Star, Staghorn, Plate, Blue, Pipe, Bubble }

        public enum Wall : byte { None, Rock, Staghorn, SeaFan, Brain }

        /// <summary>The reefs' life: what of it stands on a top, what hangs under a reef (Hanging), and the sea pickles that light it.</summary>
        public enum Decor : byte { Polyp, Sponge, Coral, Clam, Table, Hanging, Pickle }

        public const int Blocks = 11, Walls = 5, Decors = 7;

        public static int WideOf(Decor kind) => kind switch { Decor.Coral => 2, Decor.Clam => 2, Decor.Table => 3, _ => 1 };

        public static int TallOf(Decor kind) => kind == Decor.Polyp || kind == Decor.Clam || kind == Decor.Pickle ? 1 : 2;

        /// <summary>How many pictures each has (its sheet's styles, side by side).</summary>
        public static int StylesOf(Decor kind) => kind switch
        {
            Decor.Polyp => 12,
            Decor.Sponge => 5,
            Decor.Coral => 8,
            Decor.Clam => 5,
            Decor.Table => 2,
            Decor.Pickle => 4,
            _ => 3,
        };

        /// <summary>One of the reefs' life to put down, at the cell it goes by: for what stands, its bottom row's left cell; for what hangs, its top cell.</summary>
        public readonly struct Placed
        {
            public readonly int X, Y, Style;
            public readonly Decor Kind;

            public Placed(int x, int y, Decor kind, int style)
            {
                X = x;
                Y = y;
                Kind = kind;
                Style = style;
            }
        }

        /// <summary>What the plan is made from: the place's size and sections, the platform's column and the island's, and whether a cell is open water (nothing in it, no wall, water).</summary>
        public readonly struct Shape
        {
            public readonly int Wide, Surface, SectionRows, Middle, Margin, Hub;
            public readonly Func<int, int, bool> Open;

            public Shape(int wide, int surface, int sectionRows, int middle, int margin, int hub, Func<int, int, bool> open)
            {
                Wide = wide;
                Surface = surface;
                SectionRows = sectionRows;
                Middle = middle;
                Margin = margin;
                Hub = hub;
                Open = open;
            }

            public int SectionTop(int section) => Surface + (section - 1) * SectionRows;
        }

        /// <summary>A reef island: its middle, its columns, and for each its body's top and bottom rows (before what grows on it); which field it is of (-1: none), and its half width.</summary>
        public sealed class Isle
        {
            public int X, Y, Left, Right, Field, Y0, Y1;
            public float Size, Depth;
            public int[] Top, Bottom;

            public int TopAt(int x) => Top[x - Left];

            public int BottomAt(int x) => Bottom[x - Left];
        }

        // ---- where the reefs go ----------------------------------------------------------------------------------

        /// <summary>The fields: how many (from, to), how wide (from, to), and the water kept clear between a field and the platform.</summary>
        private const int FieldsFewest = 3, FieldsMost = 5, FieldWidest = 2600, FieldNarrowest = 1700, OffPlatform = 250;

        /// <summary>Islands tried in a field: one to so many columns of it.</summary>
        private const int ColumnsAnIsle = 30;

        /// <summary>The biggest island's half width, and the smallest's.</summary>
        private const float Biggest = 52f, Smallest = 9f;

        /// <summary>Clear water an island keeps about it: from what was there before, and from another island.</summary>
        private const int KeepsOff = 8, IslesApart = 6;

        /// <summary>The longest strand that joins two islands of a field.</summary>
        private const float LongestStrand = 220f;

        /// <summary>Lone islands further down: one tried to so many columns of the sea.</summary>
        private const int ColumnsALoneIsle = 95;

        // ---- the plan ---------------------------------------------------------------------------------------------

        /// <summary>The rows the plan covers (from, to), and its width (the place's).</summary>
        public readonly int Top, Bottom, Width;

        private readonly byte[] blocks, walls;
        private readonly bool[] taken;
        private readonly Shape shape;
        private readonly Random rand;
        private readonly int seed;

        public readonly List<Isle> Isles = new();
        public readonly List<(Isle a, Isle b)> Strands = new();
        public readonly List<Placed> Life = new();

        /// <summary>The cells at a reef's edge to be worn round (the game's own smoothing of a slope, Tile.SmoothSlope).</summary>
        public readonly List<(int x, int y)> Worn = new();

        /// <summary>The fields' middles and widths, for the preview.</summary>
        public readonly List<(int middle, int wide)> Fields = new();

        public Block BlockAt(int x, int y) => In(x, y) ? (Block)blocks[Index(x, y)] : Block.None;

        public Wall WallAt(int x, int y) => In(x, y) ? (Wall)walls[Index(x, y)] : Wall.None;

        private bool In(int x, int y) => x >= 0 && x < Width && y >= Top && y <= Bottom;

        private int Index(int x, int y) => (y - Top) * Width + x;

        private ReefPlan(Shape shape, int seed)
        {
            this.shape = shape;
            this.seed = seed;
            rand = new Random(seed ^ 0x2EEF);
            Width = shape.Wide;
            Top = shape.SectionTop(2);
            Bottom = shape.SectionTop(7) - 1;
            blocks = new byte[Width * (Bottom - Top + 1)];
            walls = new byte[blocks.Length];
            taken = new bool[blocks.Length];
        }

        public static ReefPlan Make(Shape shape, int seed)
        {
            var plan = new ReefPlan(shape, seed);
            plan.Grow();
            return plan;
        }

        private void Grow()
        {
            PlaceFields();
            PlaceLoneIsles();

            foreach (Isle isle in Isles)
            {
                Body(isle);
                Roughen(isle);
            }

            foreach (Isle isle in Isles)
                Clothe(isle);

            Prune();

            foreach (var (a, b) in Strands)
                Strand(a, b);

            foreach (Isle isle in Isles)
                Settle(isle);

            Wear();
        }

        // ---- the fields and their islands -------------------------------------------------------------------------

        /// <summary>
        /// The fields: the first under the island, the rest wherever they are far enough from it, from each other and
        /// from the platform's water; their islands; and the strands that join each field's islands into one (the
        /// shortest ways that reach them all, and now and then another to a near one).
        /// </summary>
        private void PlaceFields()
        {
            int count = FieldsFewest + rand.Next(FieldsMost - FieldsFewest + 1);
            var fields = new List<(int middle, int wide)>();

            // The island's, kept off the platform: as wide as it may be, its middle moved off the platform's side if need be.
            int hubWide = FieldWidest - rand.Next(300);
            int hub = shape.Hub < shape.Middle ? Math.Min(shape.Hub, shape.Middle - OffPlatform - hubWide / 2) : Math.Max(shape.Hub, shape.Middle + OffPlatform + hubWide / 2);
            fields.Add((hub, hubWide));

            for (int tries = 0; fields.Count < count && tries < 600; tries++)
            {
                int wide = FieldNarrowest + rand.Next(FieldWidest - FieldNarrowest);
                int x = shape.Margin + wide / 2 + 40 + rand.Next(Math.Max(1, Width - shape.Margin * 2 - wide - 80));

                if (Math.Abs(x - shape.Middle) < OffPlatform + wide / 2)
                    continue;

                bool apart = true;

                foreach (var (m, w) in fields)
                    apart &= Math.Abs(m - x) >= (w + wide) / 2 + 120;

                if (apart)
                    fields.Add((x, wide));
            }

            int top = shape.SectionTop(2) + 20, bottom = shape.SectionTop(4) - 20;

            for (int f = 0; f < fields.Count; f++)
            {
                var (middle, wide) = fields[f];
                Fields.Add((middle, wide));

                int start = Isles.Count;
                int many = wide / ColumnsAnIsle;

                for (int k = 0; k < many; k++)
                {
                    float along = k / (float)many;
                    float size = Smallest + (Biggest - Smallest) * MathF.Pow(1f - along, 1.3f) * (0.8f + 0.2f * (float)rand.NextDouble());

                    for (int tries = 0; tries < 60; tries++)
                    {
                        int x = middle + (int)((rand.NextDouble() * 2.0 - 1.0) * wide * 0.48);
                        int y = top + (int)(size * 0.6f) + rand.Next(Math.Max(1, bottom - top - (int)(size * 1.9f)));

                        if (TryIsle(x, y, size, f, 0f))
                            break;
                    }
                }

                Join(start, Isles.Count, LongestStrand);
                Satellites(start, Isles.Count, f);
            }
        }

        /// <summary>About each big island of a field, a few islets close by, each joined to it: so that a field is of clusters.</summary>
        private void Satellites(int from, int to, int field)
        {
            for (int i = from; i < to; i++)
            {
                Isle big = Isles[i];

                if (big.Size < 16f)
                    continue;

                int many = 1 + rand.Next(3);

                for (int k = 0; k < many; k++)
                {
                    for (int tries = 0; tries < 16; tries++)
                    {
                        float size = 5f + 6f * (float)rand.NextDouble();

                        // Off either end, a little up or down, or under its belly.
                        int x, y;

                        switch (rand.Next(3))
                        {
                            case 0:
                                x = big.Right + (int)(size * 1.2f) + 8 + rand.Next(14);
                                y = big.Y + rand.Next(-(int)size, (int)size + 1);
                                break;
                            case 1:
                                x = big.Left - (int)(size * 1.2f) - 8 - rand.Next(14);
                                y = big.Y + rand.Next(-(int)size, (int)size + 1);
                                break;
                            default:
                                x = big.X + rand.Next(-(int)big.Size, (int)big.Size + 1);
                                y = big.Y1 + (int)(size * 0.6f) + 10 + rand.Next(12);
                                break;
                        }

                        if (TryIsle(x, y, size, field, 0f))
                        {
                            Strands.Add((big, Isles[^1]));
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>From the middle of the third section to the end of the sixth, lone islands: fewer and smaller the deeper, some joined to the nearest island by a strand.</summary>
        private void PlaceLoneIsles()
        {
            int top = shape.SectionTop(3) + shape.SectionRows / 2, bottom = shape.SectionTop(7) - 24;
            int tries = Width / ColumnsALoneIsle;

            for (int k = 0; k < tries; k++)
            {
                float depth = MathF.Pow((float)rand.NextDouble(), 1.4f);

                if (rand.NextDouble() > 0.1 + 0.9 * MathF.Pow(1f - depth, 1.2f))
                    continue;

                float size = 6f + 20f * (1f - depth) * (0.5f + 0.5f * (float)rand.NextDouble());
                int y = top + (int)((bottom - top) * depth);
                int x = shape.Margin + 60 + rand.Next(Width - shape.Margin * 2 - 120);

                if (!TryIsle(x, y, size, -1, depth))
                    continue;

                Isle lone = Isles[^1];
                Isle nearest = null;
                float best = 90f + 130f * (1f - depth);

                foreach (Isle other in Isles)
                {
                    float d = Distance(lone, other);

                    if (other != lone && d < best)
                    {
                        best = d;
                        nearest = other;
                    }
                }

                if (nearest != null && rand.NextDouble() < 0.6)
                    Strands.Add((lone, nearest));
            }
        }

        private static float Distance(Isle a, Isle b) => MathF.Sqrt((a.X - b.X) * (float)(a.X - b.X) + (a.Y - b.Y) * (float)(a.Y - b.Y));

        /// <summary>The islands of a field joined: the shortest strands that reach them all (Prim's), then now and then one more to the nearest it is not joined to.</summary>
        private void Join(int from, int to, float longest)
        {
            int n = to - from;

            if (n < 2)
                return;

            var reached = new bool[n];
            var best = new float[n];
            var by = new int[n];

            for (int i = 0; i < n; i++)
            {
                best[i] = float.MaxValue;
                by[i] = -1;
            }

            best[0] = 0f;

            for (int step = 0; step < n; step++)
            {
                int pick = -1;

                for (int i = 0; i < n; i++)
                    if (!reached[i] && (pick < 0 || best[i] < best[pick]))
                        pick = i;

                reached[pick] = true;

                if (by[pick] >= 0 && best[pick] <= longest)
                    Strands.Add((Isles[from + by[pick]], Isles[from + pick]));

                for (int i = 0; i < n; i++)
                {
                    float d = Distance(Isles[from + pick], Isles[from + i]);

                    if (!reached[i] && d < best[i])
                    {
                        best[i] = d;
                        by[i] = pick;
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (rand.NextDouble() > 0.3)
                    continue;

                Isle a = Isles[from + i], near = null;
                float nearest = longest * 0.7f;

                for (int j = 0; j < n; j++)
                {
                    Isle b = Isles[from + j];

                    if (b == a || Joined(a, b))
                        continue;

                    float d = Distance(a, b);

                    if (d < nearest)
                    {
                        nearest = d;
                        near = b;
                    }
                }

                if (near != null)
                    Strands.Add((a, near));
            }
        }

        private bool Joined(Isle a, Isle b)
        {
            foreach (var (p, q) in Strands)
                if (p == a && q == b || p == b && q == a)
                    return true;

            return false;
        }

        // ---- an island's body --------------------------------------------------------------------------------------

        /// <summary>
        /// An island of this half width about this middle, if it can go: its shape, clear of every other island by
        /// <see cref="IslesApart"/> and of everything already in the place by <see cref="KeepsOff"/>. Its top rises in
        /// crags and spires, its belly hangs deep with drips of rock off it, sharp; its ends taper.
        /// </summary>
        private bool TryIsle(int x, int y, float size, int field, float depth)
        {
            var isle = new Isle { X = x, Y = y, Size = size, Field = field, Depth = depth };
            int left = (int)(size * (0.8f + 0.35f * (float)rand.NextDouble()));
            int right = (int)(size * (0.8f + 0.35f * (float)rand.NextDouble()));
            float tall = size * (0.34f + 0.14f * (float)rand.NextDouble());
            int s = rand.Next(1 << 20);

            isle.Left = x - left;
            isle.Right = x + right;
            isle.Top = new int[left + right + 1];
            isle.Bottom = new int[left + right + 1];

            // Spires on its top, and drips of rock under its belly, many and sharp.
            int spires = 1 + (int)(size / 12f);
            var spire = new (float at, float high, float half)[spires];

            for (int k = 0; k < spires; k++)
                spire[k] = (x + (float)(rand.NextDouble() * 1.6 - 0.8) * size, 3f + (float)rand.NextDouble() * Math.Min(9f, tall * 0.5f), 1.5f + (float)rand.NextDouble() * 3f);

            int drips = 2 + (int)(size / 5f);
            var drip = new (float at, float deep, float half)[drips];

            for (int k = 0; k < drips; k++)
                drip[k] = (x + (float)(rand.NextDouble() * 1.6 - 0.8) * size, 3f + (float)rand.NextDouble() * tall * 1.1f, 1f + (float)rand.NextDouble() * 3.5f);

            isle.Y0 = int.MaxValue;
            isle.Y1 = int.MinValue;

            for (int c = isle.Left; c <= isle.Right; c++)
            {
                float u = c < x ? (c - x) / (float)Math.Max(1, left) : (c - x) / (float)Math.Max(1, right);
                float e = Math.Max(0f, 1f - u * u);
                float ridge = 1f - Math.Abs(2f * Noise1(c * 0.21f, s + 3) - 1f);
                float rise = tall * (0.06f + 0.94f * MathF.Pow(e, 0.5f)) * (0.8f + 0.4f * Fbm(c * 0.06f, s))
                    + 4f * ridge * ridge - 1.5f + 2.2f * (Noise1(c * 0.55f, s + 1) - 0.5f);
                float sink = tall * (0.2f + 1.25f * e) * (0.75f + 0.5f * Fbm(c * 0.05f, s + 2)) + 3f * (Noise1(c * 0.4f, s + 4) - 0.5f);

                foreach (var p in spire)
                {
                    float near = 1f - Math.Abs(c - p.at) / p.half;

                    if (near > 0f && e > 0.15f)
                        rise += p.high * MathF.Pow(near, 1.3f);
                }

                foreach (var d in drip)
                {
                    float near = 1f - Math.Abs(c - d.at) / d.half;

                    if (near > 0f)
                        sink = Math.Max(sink, tall * (0.2f + 1.25f * e) * 0.7f + d.deep * MathF.Pow(near, 1.2f) * e);
                }

                int t = y - (int)MathF.Round(Math.Max(0f, rise));
                int b = Math.Max(t, y + (int)MathF.Round(Math.Max(0f, sink)));
                isle.Top[c - isle.Left] = t;
                isle.Bottom[c - isle.Left] = b;
                isle.Y0 = Math.Min(isle.Y0, t - 6);
                isle.Y1 = Math.Max(isle.Y1, b);
            }

            int x0 = isle.Left - 4, x1 = isle.Right + 4;

            if (x0 < shape.Margin + 12 || x1 >= Width - shape.Margin - 12 || isle.Y0 - 14 < Top + 2 || isle.Y1 + 10 > Bottom - 2)
                return false;

            foreach (Isle other in Isles)
            {
                if (x1 + IslesApart >= other.Left - 4 && x0 - IslesApart <= other.Right + 4
                    && isle.Y1 + IslesApart >= other.Y0 && isle.Y0 - IslesApart <= other.Y1)
                    return false;
            }

            for (int cx = x0 - KeepsOff; cx <= x1 + KeepsOff; cx += 2)
                for (int cy = isle.Y0 - 14 - KeepsOff; cy <= isle.Y1 + 10 + KeepsOff; cy += 2)
                    if (!shape.Open(cx, cy))
                        return false;

            Isles.Add(isle);
            return true;
        }

        /// <summary>
        /// Its edges made rough: notches bitten out of them, crags grown out of them (of what is next to each), and what
        /// is left hanging by nothing taken away.
        /// </summary>
        private void Roughen(Isle isle)
        {
            int s = Hash32(isle.X * 5, isle.Y * 3);
            int x0 = isle.Left - 3, x1 = isle.Right + 3, y0 = isle.Y0, y1 = isle.Y1 + 3;
            var bite = new List<(int, int)>();
            var grow = new List<(int, int, Block)>();

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Block here = BlockAt(x, y);

                    if (here != Block.None)
                    {
                        if (Edge(x, y) && Noise2(x * 0.5f, y * 0.5f, s) < 0.3f)
                            bite.Add((x, y));

                        continue;
                    }

                    if (!Free(x, y) || Noise2(x * 0.5f, y * 0.5f, s + 1) < 0.68f)
                        continue;

                    Block next = BlockAt(x, y + 1) != Block.None ? BlockAt(x, y + 1) : BlockAt(x, y - 1) != Block.None ? BlockAt(x, y - 1)
                        : BlockAt(x - 1, y) != Block.None ? BlockAt(x - 1, y) : BlockAt(x + 1, y);

                    if (next != Block.None)
                        grow.Add((x, y, next));
                }
            }

            foreach (var (x, y) in bite)
                SetBlock(x, y, Block.None);

            foreach (var (x, y, kind) in grow)
                SetBlock(x, y, kind);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (BlockAt(x, y) != Block.None && BlockAt(x - 1, y) == Block.None && BlockAt(x + 1, y) == Block.None
                        && BlockAt(x, y - 1) == Block.None && BlockAt(x, y + 1) == Block.None)
                        SetBlock(x, y, Block.None);
        }

        /// <summary>A reef cell with open water at one of its sides.</summary>
        private bool Edge(int x, int y) => Free(x - 1, y) || Free(x + 1, y) || Free(x, y - 1) || Free(x, y + 1);

        /// <summary>
        /// Its body: rubble under it, its crust over it, reef rock between with blobs of rubble in it and veins of star
        /// coral near its top; now and then a hollow in a big one, walled.
        /// </summary>
        private void Body(Isle isle)
        {
            int s = Hash32(isle.X, isle.Y);

            for (int c = isle.Left; c <= isle.Right; c++)
            {
                int top = isle.TopAt(c), bottom = isle.BottomAt(c);
                int rubble = 1 + (int)(Fbm(c * 0.3f, s + 5) * 2.6f);
                int crust = Fbm(c * 0.4f, s + 6) > 0.5f ? 1 : 0;
                int living = crust + 2 + (int)(Fbm(c * 0.12f, s + 10) * 4f);

                for (int y = top; y <= bottom; y++)
                {
                    int down = y - top, up = bottom - y;
                    Block kind;

                    if (up < rubble && down > 0)
                        kind = Block.Rubble;
                    else if (down <= crust)
                        kind = Block.Crust;
                    else if (down <= living)
                        kind = Living(c, y, s);
                    else if (Noise2(c * 0.13f, y * 0.13f, s + 7) > 0.72f)
                        kind = Block.Rubble;
                    else if (Noise2(c * 0.1f, y * 0.16f, s + 8) > 0.78f)
                        kind = Living(c, y, s);
                    else
                        kind = Block.Rock;

                    SetBlock(c, y, kind);
                }
            }

            // A hollow or two in a big island, closed, walled, with three rows of reef round it.
            if (isle.Size >= 22f && rand.NextDouble() < 0.6)
            {
                int hollows = 1 + rand.Next(2);

                for (int k = 0; k < hollows; k++)
                {
                    int c = isle.X + (int)((rand.NextDouble() * 1.2 - 0.6) * isle.Size);
                    int mid = (isle.TopAt(c) + isle.BottomAt(c)) / 2 + rand.Next(3) - 1;
                    float rx = 2f + rand.Next(4), ry = 1f + rand.Next(2);

                    for (int x = c - (int)rx - 1; x <= c + (int)rx + 1; x++)
                    {
                        if (x <= isle.Left + 3 || x >= isle.Right - 3)
                            continue;

                        for (int y = mid - (int)ry - 1; y <= mid + (int)ry + 1; y++)
                        {
                            float dx = (x - c) / rx, dy = (y - mid) / ry;

                            if (dx * dx + dy * dy > 1f || y - isle.TopAt(x) < 4 || isle.BottomAt(x) - y < 4)
                                continue;

                            SetBlock(x, y, Block.None);
                            SetWall(x, y, Wall.Rock);
                        }
                    }
                }
            }
        }

        /// <summary>The living reef under its crust, in patches: star coral, brain coral, staghorn, and reef rock between.</summary>
        private Block Living(int x, int y, int s)
        {
            float n = Noise2(x * 0.09f, y * 0.12f, s + 11);

            if (n < 0.2f)
                return Block.Star;

            if (n < 0.33f)
                return Block.Brain;

            if (n < 0.47f)
                return Block.Staghorn;

            return Block.Rock;
        }

        /// <summary>The bright corals a piece of reef on a top is grown of, one to a piece.</summary>
        private static readonly Block[] Bright = { Block.Blue, Block.Pipe, Block.Bubble, Block.Brain, Block.Staghorn, Block.Star };

        /// <summary>
        /// What grows on its top: patches of star coral in its crust, and over it, packed side by side with a little
        /// crust between, pieces of reef of one bright coral each, of four shapes: a tree (a trunk, and branches
        /// stepping up and out from it, forking), a claw (arms reaching out to either side and turning up), a dome on a
        /// short stalk, and a spire, ragged. And shelves of plate coral off its ends.
        /// </summary>
        private void Clothe(Isle isle)
        {
            for (int c = isle.Left; c <= isle.Right; c++)
                if (Noise1(c * 0.15f, Hash32(isle.X, 7)) > 0.72f)
                    for (int y = isle.TopAt(c), deep = 1 + rand.Next(2); y <= isle.TopAt(c) + deep; y++)
                        if (BlockAt(c, y) != Block.None)
                            SetBlock(c, y, Block.Star);

            float scale = Math.Clamp(isle.Size / 24f, 0.55f, 1.7f);
            int x = isle.Left + 2 + rand.Next(3);

            while (x <= isle.Right - 2)
            {
                Block kind = Bright[rand.Next(Bright.Length)];
                int roll = rand.Next(100);
                int wide;

                if (roll < 34)
                    wide = Tree(x, Highest(x, isle) - 1, kind, scale);
                else if (roll < 58)
                    wide = Claw(x, Highest(x, isle) - 1, kind, scale);
                else if (roll < 82)
                    wide = Dome(x, isle, kind, scale);
                else
                    wide = Spire(x, Highest(x, isle) - 1, kind, scale);

                x += Math.Max(2, wide) + rand.Next(3);
            }

            // Shelves of plate coral off each end, one under another.
            for (int way = -1; way <= 1; way += 2)
            {
                if (rand.NextDouble() > 0.65)
                    continue;

                int from = way < 0 ? isle.Left + 2 : isle.Right - 2;

                if (from < isle.Left || from > isle.Right)
                    continue;

                int row = isle.TopAt(from) + 1;
                int shelves = 1 + rand.Next(3);

                for (int k = 0; k < shelves && row < isle.BottomAt(from); k++)
                {
                    int length = 3 + rand.Next(7);

                    for (int i = 0; i <= length + 2; i++)
                    {
                        int c = from + way * i;

                        if (BlockAt(c, row) != Block.None && i < 3)
                            continue;

                        if (!Free(c, row))
                            break;

                        SetBlock(c, row, Block.Plate);

                        // Thicker near where it grows from.
                        if (i < length / 2 && length > 5 && Free(c, row + 1))
                            SetBlock(c, row + 1, Block.Plate);
                    }

                    row += 2 + rand.Next(2);
                }
            }
        }

        /// <summary>
        /// Whatever reef holds to no island (side by side or one over another, never only corner to corner) taken away:
        /// so that nothing of it floats.
        /// </summary>
        private void Prune()
        {
            var held = new bool[blocks.Length];
            var queue = new Queue<int>();

            foreach (Isle isle in Isles)
            {
                for (int c = isle.Left; c <= isle.Right; c++)
                {
                    int y = (isle.TopAt(c) + isle.BottomAt(c)) / 2;

                    if (In(c, y) && blocks[Index(c, y)] != 0 && !held[Index(c, y)])
                    {
                        held[Index(c, y)] = true;
                        queue.Enqueue(Index(c, y));
                    }
                }
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % Width, y = Top + i / Width;

                Reach(x - 1, y);
                Reach(x + 1, y);
                Reach(x, y - 1);
                Reach(x, y + 1);
            }

            for (int i = 0; i < blocks.Length; i++)
                if (blocks[i] != 0 && !held[i])
                    blocks[i] = 0;

            void Reach(int x, int y)
            {
                if (!In(x, y))
                    return;

                int i = Index(x, y);

                if (blocks[i] != 0 && !held[i])
                {
                    held[i] = true;
                    queue.Enqueue(i);
                }
            }
        }

        /// <summary>Reef put at this cell if it is free; whether it was.</summary>
        private bool Grow(int x, int y, Block kind)
        {
            if (!Free(x, y))
                return false;

            SetBlock(x, y, kind);
            return true;
        }

        /// <summary>A tree of one coral standing on the cell under (x, foot): a trunk, then two to four branches stepping up and out from it, now and then forking. How wide it came to be.</summary>
        private int Tree(int x, int foot, Block kind, float scale)
        {
            int trunk = 1 + rand.Next(Math.Max(1, (int)(3 * scale)));
            bool thick = scale > 0.8f && rand.NextDouble() < 0.6;
            int y = foot;

            for (int k = 0; k < trunk; k++, y--)
            {
                Grow(x, y, kind);

                if (thick)
                    Grow(x + 1, y, kind);
            }

            int branches = 2 + rand.Next(3), least = x, most = x + (thick ? 1 : 0);

            for (int b = 0; b < branches; b++)
            {
                int way = b % 2 == 0 ? -1 : 1;
                int bx = way < 0 ? x : x + (thick ? 1 : 0), by = y + 1;
                int length = 2 + rand.Next(Math.Max(1, (int)(6 * scale)));

                for (int k = 0; k < length; k++)
                {
                    if (k == 0 || rand.NextDouble() < 0.45)
                        bx += way;
                    else
                        by--;

                    if (!Grow(bx, by, kind))
                        break;

                    // A fork straight up, short.
                    if (k > 1 && rand.NextDouble() < 0.18)
                        for (int f = 1, n = 1 + rand.Next(2); f <= n && Grow(bx, by - f, kind); f++) { }

                    least = Math.Min(least, bx);
                    most = Math.Max(most, bx);
                }

                // Its tip up.
                if (BlockAt(bx, by) != Block.None)
                    for (int f = 1, n = 1 + rand.Next(2); f <= n && Grow(bx, by - f, kind); f++) { }
            }

            return most - x + 1;
        }

        /// <summary>A claw of one coral: two or three arms reaching out to either side from its foot and turning up. How wide it came to be.</summary>
        private int Claw(int x, int foot, Block kind, float scale)
        {
            Grow(x, foot, kind);
            Grow(x, foot - 1, kind);

            int arms = 2 + rand.Next(2), most = x;

            for (int a = 0; a < arms; a++)
            {
                int way = a == 1 ? -1 : 1;
                int ax = x, ay = foot - rand.Next(2);
                int reach = 2 + rand.Next(Math.Max(1, (int)(4 * scale)));
                int up = 2 + rand.Next(Math.Max(1, (int)(5 * scale)));

                // Each step one cell on from the last, out or up, never across a corner: the arm holds together.
                bool whole = true;

                for (int k = 0; k < reach && whole; k++)
                {
                    if (k > 0 && rand.NextDouble() < 0.3)
                        ay--;
                    else
                        ax += way;

                    whole = Grow(ax, ay, kind);
                }

                for (int k = 0; k < up && whole; k++)
                {
                    if (k > 0 && rand.NextDouble() < 0.25)
                        ax += way;
                    else
                        ay--;

                    whole = Grow(ax, ay, kind);
                }

                most = Math.Max(most, ax);
            }

            return most - x + 1;
        }

        /// <summary>A dome of one coral on a short stalk down to the ground under its middle, its corners gone and a hole or two in it. How wide.</summary>
        private int Dome(int x, Isle isle, Block kind, float scale)
        {
            int wide = 4 + rand.Next(Math.Max(1, (int)(6 * scale)));
            int high = 3 + rand.Next(Math.Max(1, (int)(3 * scale)));
            int middle = Math.Min(x + wide / 2, isle.Right);
            int foot = Highest(middle, isle) - 1;
            int stalk = rand.Next(3);

            for (int k = 0; k < stalk; k++)
            {
                Grow(middle, foot - k, kind);

                if (wide > 6)
                    Grow(middle + 1, foot - k, kind);
            }

            int bottom = foot - stalk;

            for (int r = 0; r < high; r++)
            {
                float up = (r + 0.5f) / high;
                float half = wide / 2f * MathF.Sqrt(Math.Max(0f, 1f - up * up * 0.8f));

                for (int c = (int)MathF.Round(middle - half); c <= (int)MathF.Round(middle + half); c++)
                {
                    bool corner = r == high - 1 && (c == (int)MathF.Round(middle - half) || c == (int)MathF.Round(middle + half));

                    if (!corner && rand.NextDouble() > 0.08)
                        Grow(c, bottom - r, kind);
                }
            }

            return wide + 1;
        }

        /// <summary>A spire of one coral, ragged: a column that narrows as it rises, a knob out of its side here and there. How wide.</summary>
        private int Spire(int x, int foot, Block kind, float scale)
        {
            int high = 3 + rand.Next(Math.Max(1, (int)(6 * scale)));
            bool thick = rand.NextDouble() < 0.75;

            for (int k = 0; k < high; k++)
            {
                int y = foot - k;

                if (!Grow(x, y, kind))
                    break;

                if (thick && k < high * 0.7f)
                    Grow(x + 1, y, kind);

                if (k > 0 && rand.NextDouble() < 0.22)
                    Grow(rand.Next(2) == 0 ? x - 1 : x + (thick ? 2 : 1), y, kind);
            }

            return thick ? 2 : 1;
        }

        // ---- the walls ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The walls an island has: reef rock behind it (and brain coral in places near its top), behind only what is
        /// within it, so that none shows round its edge; staghorn hanging from its underside; sea fans grown up behind
        /// its top.
        /// </summary>
        private void Settle(Isle isle)
        {
            int s = Hash32(isle.X * 3, isle.Y);
            int x0 = isle.Left - 4, x1 = isle.Right + 4, y0 = isle.Y0, y1 = isle.Y1 + 2;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (BlockAt(x, y) == Block.None || !Within(x, y))
                        continue;

                    bool high = x >= isle.Left && x <= isle.Right && y - isle.TopAt(x) <= 3;
                    SetWall(x, y, high && Noise2(x * 0.18f, y * 0.18f, s) > 0.55f ? Wall.Brain : Wall.Rock);
                }
            }

            // Staghorn hanging from its underside, longest under its belly.
            for (int x = isle.Left + 1; x < isle.Right; x++)
            {
                float u = (x - isle.X) / Math.Max(1f, isle.Size);
                int hang = (int)MathF.Round(Fbm(x * 0.15f, s + 1) * 8f * (1f - u * u) - 2f);
                int under = Lowest(x, isle);

                for (int y = under + 1; y <= under + hang; y++)
                    if (Free(x, y))
                        SetWall(x, y, Wall.Staghorn);
            }

            // Sea fans up behind its top.
            int fans = (int)(isle.Size / 14f) + rand.Next(2);

            for (int k = 0; k < fans; k++)
            {
                int c = isle.X + (int)((rand.NextDouble() * 1.4 - 0.7) * isle.Size);

                if (c <= isle.Left || c >= isle.Right)
                    continue;

                int foot = Highest(c, isle);
                int high = 6 + rand.Next(8);
                float wide = high * 0.55f + rand.Next(2);

                for (int dy = 1; dy <= high; dy++)
                {
                    float up = dy / (float)high;
                    float half = wide * (0.25f + 0.75f * Math.Min(1f, up / 0.6f));

                    for (int dx = -(int)half; dx <= (int)half; dx++)
                    {
                        float across = dx / Math.Max(1f, wide);

                        if (dy > high * (1f - across * across * 0.35f) || Hash01(c + dx, foot - dy, s + 2) < 0.18f)
                            continue;

                        if (Free(c + dx, foot - dy))
                            SetWall(c + dx, foot - dy, Wall.SeaFan);
                    }
                }
            }
        }

        /// <summary>A cell all eight of whose neighbours are reef: a wall behind it does not show round the reef's edge.</summary>
        private bool Within(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (BlockAt(x + dx, y + dy) == Block.None)
                        return false;

            return true;
        }

        /// <summary>The highest and the lowest reef in a column of an island.</summary>
        private int Highest(int x, Isle isle)
        {
            int y = isle.TopAt(x);

            while (BlockAt(x, y - 1) != Block.None)
                y--;

            return y;
        }

        private int Lowest(int x, Isle isle)
        {
            int y = isle.BottomAt(x);

            while (BlockAt(x, y + 1) != Block.None)
                y++;

            return y;
        }

        /// <summary>
        /// A strand of coral between two islands, from the end of one that faces the other to the end of that one that
        /// faces it, bowed and sagging, thick where it meets them and thinner between: staghorn at its heart, the lace
        /// of sea fans at its edges, reef rock where it meets an island; and a spur or two of sea fan off it, mostly
        /// upward. Over open water only, never behind an island.
        /// </summary>
        private void Strand(Isle a, Isle b)
        {
            int s = Hash32(a.X + b.X, a.Y * 7 + b.Y);
            (float x, float y) p0 = End(a, b), p2 = End(b, a);
            float dx = p2.x - p0.x, dy = p2.y - p0.y, length = MathF.Sqrt(dx * dx + dy * dy);

            if (length < 2f)
                return;

            float nx = -dy / length, ny = dx / length;
            float bow = (float)(rand.NextDouble() - 0.5) * 0.45f * length;
            float sag = 0.1f * length * (float)rand.NextDouble();
            (float x, float y) p1 = ((p0.x + p2.x) / 2f + nx * bow, (p0.y + p2.y) / 2f + ny * bow + sag);

            float thickEnds = Math.Clamp(Math.Min(a.Size, b.Size) * 0.8f, 13f, 28f);
            float thickMiddle = 10f + 7f * (float)rand.NextDouble();
            int steps = (int)(length * 2f) + 2;

            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                (float x, float y) p = Bezier(p0, p1, p2, t);

                // It wanders off its line a little, and back, as a growth does.
                float wander = 6f * (Fbm(t * length * 0.04f, s + 3) - 0.5f) * MathF.Sin(t * MathF.PI);
                p = (p.x + nx * wander, p.y + ny * wander);
                float m = 2f * t - 1f;
                float thick = (thickMiddle + (thickEnds - thickMiddle) * m * m) * (0.7f + 0.6f * Fbm(t * length * 0.1f, s));
                bool end = t < 0.1f || t > 0.9f;

                Stamp(p.x, p.y, thick / 2f, end);
            }

            // Spurs: short strands of sea fan off it, mostly up, thinning to nothing.
            int spurs = (int)(length / 50f) + rand.Next(2);

            for (int k = 0; k < spurs; k++)
            {
                float t = 0.2f + 0.6f * (float)rand.NextDouble();
                (float x, float y) from = Bezier(p0, p1, p2, t);
                float angle = -MathF.PI / 2f + (float)(rand.NextDouble() - 0.5) * 2.2f;
                float reach = 8f + rand.Next(16);

                for (float r = 0f; r < reach; r += 0.5f)
                {
                    float bend = 0.35f * MathF.Sin(r * 0.25f + s);
                    float x = from.x + MathF.Cos(angle + bend) * r, y = from.y + MathF.Sin(angle + bend) * r;
                    float half = 3.2f * (1f - r / reach) + 0.6f;

                    for (int cx = (int)(x - half); cx <= (int)(x + half); cx++)
                        for (int cy = (int)(y - half); cy <= (int)(y + half); cy++)
                            if ((cx - x) * (cx - x) + (cy - y) * (cy - y) <= half * half && Free(cx, cy) && WallAt(cx, cy) == Wall.None)
                                SetWall(cx, cy, Wall.SeaFan);
                }
            }
        }

        /// <summary>Where a strand leaves an island toward another: near its end that faces it, halfway down its body there.</summary>
        private (float x, float y) End(Isle from, Isle to)
        {
            int c = to.X >= from.X ? from.Right - (int)((from.Right - from.X) * 0.15f) : from.Left + (int)((from.X - from.Left) * 0.15f);
            return (c, (from.TopAt(c) + from.BottomAt(c)) / 2f);
        }

        private static (float x, float y) Bezier((float x, float y) a, (float x, float y) b, (float x, float y) c, float t)
        {
            float u = 1f - t;
            return (u * u * a.x + 2f * u * t * b.x + t * t * c.x, u * u * a.y + 2f * u * t * b.y + t * t * c.y);
        }

        private void Stamp(float x, float y, float radius, bool end)
        {
            for (int cx = (int)(x - radius - 2); cx <= (int)(x + radius + 2); cx++)
            {
                for (int cy = (int)(y - radius - 2); cy <= (int)(y + radius + 2); cy++)
                {
                    // Its edge ragged.
                    float d = MathF.Sqrt((cx - x) * (cx - x) + (cy - y) * (cy - y)) + 2.2f * (Hash01(cx, cy, 31) - 0.5f);

                    if (d > radius || !Free(cx, cy))
                        continue;

                    Wall wall = d > radius - Math.Max(1.2f, radius * 0.28f) && radius > 2.2f && !end ? Wall.SeaFan : Wall.Staghorn;
                    Wall was = WallAt(cx, cy);

                    // The heart wins over the edge where strands cross.
                    if (was == Wall.None || was == Wall.SeaFan && wall != Wall.SeaFan)
                        SetWall(cx, cy, wall);
                }
            }
        }

        // ---- the reefs' life, and their edges worn --------------------------------------------------------------------

        /// <summary>
        /// Its life, on every top it has (its own, its mounds', its fingers', its shelves'), on ground level for as
        /// wide as the thing: polyps (anemones, urchins, starfish, zoanthids), sponges and whips, sea fans and corals,
        /// clams and mushroom corals, table corals; and soft corals hanging under it. Then its edges worn round, save
        /// where something stands on it or hangs from it.
        /// </summary>
        private void Wear()
        {
            int x0 = 0, x1 = Width - 1, y0 = Top + 1, y1 = Bottom - 1;
            var anchors = new HashSet<(int, int)>();

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (BlockAt(x, y) == Block.None)
                        continue;

                    if (Air(x, y - 1))
                        Stand(x, y, anchors);

                    if (Air(x, y + 1) && rand.NextDouble() < 0.2)
                        Hang(x, y, anchors);
                }
            }

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (BlockAt(x, y) == Block.None || anchors.Contains((x, y)) || rand.NextDouble() > 0.7)
                        continue;

                    if (Free(x - 1, y) || Free(x + 1, y) || Free(x, y - 1) || Free(x, y + 1))
                        Worn.Add((x, y));
                }
            }
        }

        /// <summary>Something on this top cell (<paramref name="x"/>, <paramref name="y"/> the ground), if one is rolled and has the room.</summary>
        private void Stand(int x, int y, HashSet<(int, int)> anchors)
        {
            int roll = rand.Next(100);
            Decor kind;

            if (roll < 12 && PickleRoom(x, y))
                kind = Decor.Pickle;
            else if (roll < 12)
                return;
            else if (roll < 34)
                kind = Decor.Polyp;
            else if (roll < 45)
                kind = Decor.Sponge;
            else if (roll < 59)
                kind = Decor.Coral;
            else if (roll < 65)
                kind = Decor.Clam;
            else if (roll < 68)
                kind = Decor.Table;
            else
                return;

            int wide = WideOf(kind), tall = TallOf(kind);

            for (int c = x; c < x + wide; c++)
            {
                if (BlockAt(c, y) == Block.None || anchors.Contains((c, y)))
                    return;

                for (int r = y - tall; r < y; r++)
                    if (!Air(c, r))
                        return;
            }

            for (int c = x; c < x + wide; c++)
            {
                anchors.Add((c, y));

                for (int r = y - tall; r < y; r++)
                    taken[Index(c, r)] = true;
            }

            Life.Add(new Placed(x, y - 1, kind, rand.Next(StylesOf(kind))));

            if (kind == Decor.Pickle)
                pickles.Add((x / PickleApart, y / PickleApart));
        }

        /// <summary>Sea pickles light the reefs as torches would, none nearer another than about this.</summary>
        private const int PickleApart = 7;

        private readonly HashSet<(int, int)> pickles = new();

        private bool PickleRoom(int x, int y)
        {
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    if (pickles.Contains((x / PickleApart + i, y / PickleApart + j)))
                        return false;

            return true;
        }

        /// <summary>A soft coral hanging from under this cell, if it has the room.</summary>
        private void Hang(int x, int y, HashSet<(int, int)> anchors)
        {
            if (!Air(x, y + 1) || !Air(x, y + 2) || anchors.Contains((x, y)))
                return;

            anchors.Add((x, y));
            taken[Index(x, y + 1)] = true;
            taken[Index(x, y + 2)] = true;
            Life.Add(new Placed(x, y + 1, Decor.Hanging, rand.Next(StylesOf(Decor.Hanging))));
        }

        // ---- cells ---------------------------------------------------------------------------------------------------

        /// <summary>Open water in the place, and nothing of the plan's in it yet.</summary>
        private bool Free(int x, int y) => In(x, y) && blocks[Index(x, y)] == 0 && shape.Open(x, y);

        /// <summary>Free, and nothing of the reefs' life in it.</summary>
        private bool Air(int x, int y) => Free(x, y) && !taken[Index(x, y)];

        private void SetBlock(int x, int y, Block kind)
        {
            if (In(x, y))
                blocks[Index(x, y)] = (byte)kind;
        }

        private void SetWall(int x, int y, Wall kind)
        {
            if (In(x, y))
                walls[Index(x, y)] = (byte)kind;
        }

        // ---- noise ---------------------------------------------------------------------------------------------------

        private int Hash32(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
            }
        }

        /// <summary>A number from 0 to 1, the same for the same cell and salt.</summary>
        private float Hash01(int x, int y, int salt) => (Hash32(x + salt * 7919, y - salt * 104729) & 0xFFFFFF) / 16777216f;

        private float Noise1(float x, int salt)
        {
            int i = (int)MathF.Floor(x);
            float f = x - i;
            f = f * f * (3f - 2f * f);
            return Hash01(i, 0, salt) + (Hash01(i + 1, 0, salt) - Hash01(i, 0, salt)) * f;
        }

        /// <summary>Smooth noise in one dimension from 0 to 1, three octaves.</summary>
        private float Fbm(float x, int salt) => (Noise1(x, salt) * 4f + Noise1(x * 2.1f, salt + 1) * 2f + Noise1(x * 4.3f, salt + 2)) / 7f;

        private float Noise2(float x, float y, int salt)
        {
            int i = (int)MathF.Floor(x), j = (int)MathF.Floor(y);
            float fx = x - i, fy = y - j;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash01(i, j, salt), b = Hash01(i + 1, j, salt), c = Hash01(i, j + 1, salt), d = Hash01(i + 1, j + 1, salt);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
    }
}
