using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The caves in the ranges, by request ("also more sophisticated cave system please."), with
    /// walls behind them ("also you didnt put walls for the caves"), dripstone in them ("need
    /// more of these dripstone ambience objects in caves"), and the crevices the eels live in.
    ///
    /// A NETWORK IN EACH RANGE. How deep in the rock every cell of the range is, the fewest
    /// steps to the water (<see cref="Depths"/>), is worked out first; then THE GREAT CAVERN,
    /// where the rock is deepest, up to a hundred cells across, its floor flatter than its
    /// roof; then, where the rock is at least <see cref="NodeDepth"/> deep, no two nearer than
    /// <see cref="NodeSpacing"/> and none in the cavern's reach, places for more hollows (one
    /// for every <see cref="RockPerHollow"/> cells of such rock, fourteen to forty-eight in
    /// all), each of a kind, the big kinds oftener where there is room:
    ///   - a HALL, wide and low, its floor flatter than its roof;
    ///   - a DOME, about as high as it is wide;
    ///   - a GALLERY, long and low, leaning a little;
    ///   - a GROTTO, middling, rounder, at any lean;
    ///   - a POCKET, small;
    ///   - or none, the ways meeting there (a JUNCTION).
    /// Each no bigger than leaves <see cref="Skin"/> cells of rock between it and the water and
    /// twelve between it and the next hollow's middle, its edge ragged by noise. The hollows
    /// are joined by the shortest tree through them all (by length; a way that would come
    /// nearer the water than <see cref="WayDepth"/> taken only if there is no other), and three
    /// in ten of the other pairs among each hollow's three nearest joined too, so that there
    /// are loops; each way bending a little, and narrow (one in five), wide (one in seven) or
    /// between. Three to five mouths on the water, at least one on either side of the range:
    /// from a hollow down the rock's own slope to the water (the steepest way down its depths,
    /// so out through the nearest face), the hollows nearest the water first. Half the
    /// hollows have a dead end going off them, as far as the rock is thick, a pocket at its
    /// end where there is room. One hall, dome or grotto in four is a garden of the glowing
    /// mosses, and one great cavern in two.
    ///
    /// WALLS. Every cell cut out of rock while a cave is being cut has a wall put behind it,
    /// of the rock it was (<see cref="WallFor"/>: basalt's grey-blue cobbles with a few patches
    /// of darker ones, stone's rocks, the clay's and the mud's earth, sandstone's, ice's,
    /// marble's...), and every rock cell beside one gets the same wall behind it
    /// (<see cref="FinishWalls"/>), so that no gap shows where the faces are worn to slopes.
    /// So are the trench's caverns and the sinkholes (from the ground down); the geodes have
    /// their gem's wall. The open sea has none; a cave's wall ends at its mouth.
    ///
    /// DRIPSTONE, of the rock (<see cref="Dripstones"/>): in the hollows, here and there, rock
    /// hanging from the roof in points, up to three tenths of the way down, and rising from
    /// the floor, up to a fifth; in the great caverns and the halls a pair now and then all but
    /// meeting, a column broken in the middle; none that would leave fewer than five rows to
    /// swim through. And the game's own small ones, many more of them than before
    /// (VastOceanFloor.Furnish).
    ///
    /// CREVICES (<see cref="Crevices"/>): off half of the hollows, straight burrows two cells
    /// across and <see cref="LairShortest"/> to <see cref="LairLongest"/> long into the rock,
    /// two cells of rock about them all the way, for the eels.
    /// </summary>
    public static partial class VastOceanFloor
    {
        /// <summary>The kinds of hollow the ranges' caves open into.</summary>
        public enum Hollow
        {
            Hall,
            Dome,
            Gallery,
            Grotto,
            Pocket,
            Cavern,
            Junction,
        }

        /// <summary>Every hollow cut (in the ranges, off the trench, at the sinkholes' feet), for the dripstone, the crevices and the preview's checks: its kind, its middle, its half width and half height.</summary>
        public static readonly List<(Hollow kind, int x, int y, float rx, float ry)> Hollows = new List<(Hollow, int, int, float, float)>();

        /// <summary>
        /// Every crevice cut, for the preview's checks: its back (the deepest cell of it; the
        /// other cell across is the next row down, or the next column right), the way out of it,
        /// and how many cells long it is.
        /// </summary>
        public static readonly List<(int x, int y, int dx, int dy, int length)> Lairs = new List<(int, int, int, int, int)>();

        /// <summary>The least depth (cells of rock to the water) a hollow's middle is put at, and the least room between two hollows' middles.</summary>
        private const int NodeDepth = 15;
        private const int NodeSpacing = 40;

        /// <summary>A hollow for every so many cells of rock deep enough for one, and no fewer or more than these.</summary>
        private const int RockPerHollow = 7000;
        private const int LeastHollows = 14;
        private const int MostHollows = 48;

        /// <summary>The great cavern's half width, at the most.</summary>
        private const float CavernMost = 52f;

        /// <summary>Rock left between a hollow and the water, at the least.</summary>
        private const int Skin = 5;

        /// <summary>A way between two hollows bends only where the rock under its line is at least this deep.</summary>
        private const int WayDepth = 7;

        /// <summary>A crevice's length, cut.</summary>
        public const int LairShortest = 14;
        public const int LairLongest = 20;

        /// <summary>The share of the game's dripstone: of the cells under a ceiling, and on a floor (VastOceanFloor.Furnish).</summary>
        private const float CeilingDrips = 0.45f;
        private const float FloorDrips = 0.18f;

        /// <summary>While cutting: no wall put behind what is cut; or the wall of the rock cut (<see cref="WallFor"/>); else this wall.</summary>
        private const ushort NoWalls = 0;
        private const ushort WallByRock = ushort.MaxValue;
        private static ushort walling = NoWalls;

        /// <summary>Every cell given a wall by the cutting, packed (row times the width, plus the column).</summary>
        private static readonly List<int> walled = new List<int>();

        private sealed class Node
        {
            public int X, Y, Depth;
            public Hollow Kind;
            public float Rx, Ry;

            public Vector2 At => new Vector2(X + 0.5f, Y + 0.5f);
        }

        /// <summary>
        /// How deep in the rock each cell of a box is: 0 where it is open, else the fewest steps
        /// (each to one of the eight cells about) to a cell that is; up to 255. Worked out by a
        /// wave from every open cell at once. Outside the box, all rock.
        /// </summary>
        private sealed class Depths
        {
            public readonly int X0, Y0, W, H;
            private readonly byte[] d;

            public Depths(int x0, int y0, int x1, int y1)
            {
                X0 = x0;
                Y0 = y0;
                W = x1 - x0 + 1;
                H = y1 - y0 + 1;
                d = new byte[W * H];

                var queue = new int[W * H];
                int head = 0, tail = 0;

                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x;

                        if (SolidAt(X0 + x, Y0 + y))
                        {
                            d[i] = 255;
                        }
                        else
                        {
                            d[i] = 0;
                            queue[tail++] = i;
                        }
                    }
                }

                while (head < tail)
                {
                    int i = queue[head++];
                    int x = i % W, y = i / W;
                    int next = d[i] + 1;

                    if (next >= 255)
                        continue;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;

                            if (dx == 0 && dy == 0 || nx < 0 || ny < 0 || nx >= W || ny >= H)
                                continue;

                            int j = ny * W + nx;

                            if (d[j] <= next)
                                continue;

                            d[j] = (byte)next;
                            queue[tail++] = j;
                        }
                    }
                }
            }

            public int At(int x, int y) => x < X0 || y < Y0 || x >= X0 + W || y >= Y0 + H ? 255 : d[(y - Y0) * W + (x - X0)];

            public int At(Vector2 p) => At((int)Math.Floor(p.X), (int)Math.Floor(p.Y));

            /// <summary>The least depth along a line, looked at every two cells.</summary>
            public int Shallowest(Vector2 from, Vector2 to)
            {
                int least = 255;
                float length = Vector2.Distance(from, to);
                int steps = Math.Max(1, (int)(length / 2f));

                for (int s = 0; s <= steps; s++)
                    least = Math.Min(least, At(Vector2.Lerp(from, to, (float)s / steps)));

                return least;
            }

            public int Shallowest(List<Vector2> path)
            {
                int least = 255;

                for (int i = 0; i + 1 < path.Count; i++)
                    least = Math.Min(least, Shallowest(path[i], path[i + 1]));

                return least;
            }
        }

        /// <summary>Rock: a solid tile (a slope is rock; a platform is not).</summary>
        private static bool SolidAt(int x, int y)
        {
            if (x < 1 || y < 1 || x >= VastOcean.Wide - 1 || y >= VastOcean.High - 1)
                return true;

            Tile t = Main.tile[x, y];
            return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
        }

        // ---- the networks ----------------------------------------------------------------------

        private static void RangeCaves(UnifiedRandom rand)
        {
            walling = WallByRock;

            foreach (Range range in ranges)
                Network(range, rand);

            walling = NoWalls;
        }

        /// <summary>Of the ranges, this one's middle is the nearest to this column: the column is this range's to cut caves under.</summary>
        private static bool Owns(Range range, int x)
        {
            foreach (Range other in ranges)
            {
                if (other != range && Math.Abs(other.Middle - x) < Math.Abs(range.Middle - x))
                    return false;
            }

            return true;
        }

        private static void Network(Range range, UnifiedRandom rand)
        {
            int reach = (int)(range.Half * 1.3f);
            int xa = Math.Max(Left, range.Middle - reach), xb = Math.Min(Right, range.Middle + reach);
            int x0 = Math.Max(Left - 40, xa - 60), x1 = Math.Min(Right + 40, xb + 60);
            int deepest = 0;

            for (int x = x0; x <= x1; x++)
                deepest = Math.Max(deepest, baseTop[x]);

            int y0 = Math.Max(VastOcean.Surface + 2, range.PeakRow - 60);
            int y1 = Math.Min(VastOcean.High - 60, deepest + 40);
            var depth = new Depths(x0, y0, x1, y1);

            // Where a hollow may go: rock at least NodeDepth deep, in this range's own columns
            // (where it stands thirty rows or more over the sediment), no lower than 25 rows
            // under the sediment's top. Looked at every five cells, each standing for 25.
            var spots = new List<Point>();
            int rock = 0;

            for (int x = xa; x <= xb; x += 5)
            {
                if (mount[x] < 30f || !Owns(range, x))
                    continue;

                int lowest = Math.Min(y1, baseTop[x] + 25);

                for (int y = y0; y <= lowest; y += 5)
                {
                    if (depth.At(x, y) < NodeDepth)
                        continue;

                    spots.Add(new Point(x, y));
                    rock += 25;
                }
            }

            if (spots.Count < 2)
            {
                Log.Add(("range network too thin", range.Middle, range.PeakRow));
                return;
            }

            int want = Math.Clamp(rock / RockPerHollow, LeastHollows, MostHollows);

            // The great cavern first, where the rock is deepest: of the spots within two of the
            // deepest, the one nearest the range's middle column and the middle row of them all;
            // nothing else within its reach and a margin.
            int deepestRock = 0;

            foreach (Point p in spots)
                deepestRock = Math.Max(deepestRock, depth.At(p.X, p.Y));

            var core = spots.FindAll(p => depth.At(p.X, p.Y) >= deepestRock - 2);
            float coreRow = 0f;

            foreach (Point p in core)
                coreRow += p.Y;

            coreRow /= core.Count;

            Point heart = core[0];

            foreach (Point p in core)
            {
                if (Math.Abs(p.X - range.Middle) + Math.Abs(p.Y - coreRow) < Math.Abs(heart.X - range.Middle) + Math.Abs(heart.Y - coreRow))
                    heart = p;
            }

            var nodes = new List<Node> { new Node { X = heart.X, Y = heart.Y, Depth = depth.At(heart.X, heart.Y), Kind = Hollow.Cavern } };
            float heartMost = (nodes[0].Depth - Skin) / 1.15f;
            float heartReach = Math.Min(heartMost, CavernMost) * 1.15f + 25f;

            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                (spots[i], spots[j]) = (spots[j], spots[i]);
            }

            foreach (Point p in spots)
            {
                if (nodes.Count >= want)
                    break;

                if (Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(heart.X, heart.Y)) < heartReach)
                    continue;

                bool apart = true;

                foreach (Node n in nodes)
                {
                    int dx = n.X - p.X, dy = n.Y - p.Y;

                    if (dx * dx + dy * dy < NodeSpacing * NodeSpacing)
                    {
                        apart = false;
                        break;
                    }
                }

                if (apart)
                    nodes.Add(new Node { X = p.X, Y = p.Y, Depth = depth.At(p.X, p.Y) });
            }

            // The hollows: each as big as the rock about it allows and no nearer the next
            // hollow's middle than twelve cells.
            // The cavern first; the others kept twelve cells clear of its ragged edge as well.
            Hollowed(nodes[0], heartMost, rand);

            float cavernEdge = Math.Max(nodes[0].Rx, nodes[0].Ry) * 1.15f;

            for (int i = 1; i < nodes.Count; i++)
            {
                Node n = nodes[i];
                float nearest = Vector2.Distance(n.At, nodes[0].At) - cavernEdge;

                for (int j = 1; j < nodes.Count; j++)
                {
                    if (j != i)
                        nearest = Math.Min(nearest, Vector2.Distance(n.At, nodes[j].At));
                }

                Hollowed(n, Math.Min((n.Depth - Skin) / 1.15f, (nearest - 12f) / 1.15f), rand);
            }

            if (nodes.Count < 2)
            {
                Log.Add(("range network too thin", range.Middle, range.PeakRow));
                return;
            }

            // The ways: the shortest tree through them all, then loops among near pairs.
            var edges = Tree(nodes, depth);
            int treeEdges = edges.Count;
            var joined = new HashSet<long>();

            foreach (var (a, b) in edges)
                joined.Add(Pair(a, b));

            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (int j in Nearest(nodes, i, 3))
                {
                    if (joined.Contains(Pair(i, j)) || rand.NextFloat() >= 0.3f)
                        continue;

                    if (depth.Shallowest(nodes[i].At, nodes[j].At) < WayDepth)
                        continue;

                    joined.Add(Pair(i, j));
                    edges.Add((i, j));
                }
            }

            foreach (var (a, b) in edges)
                Way(nodes[a], nodes[b], depth, rand);

            // The mouths.
            int mouths = Mouths(range, nodes, depth, rand);

            // The dead ends.
            int deadEnds = 0;

            foreach (Node n in nodes.ToArray())
            {
                if (n.Kind == Hollow.Junction || rand.Next(2) == 0)
                    continue;

                if (DeadEnd(n, depth, rand))
                    deadEnds++;
            }

            Log.Add(("range network", range.Middle, range.PeakRow));
            Log.Add(($"range network: {nodes.Count} hollows, {treeEdges} ways in the tree and {edges.Count - treeEdges} loops, {mouths} mouths, {deadEnds} dead ends", range.Middle, range.PeakRow));
        }

        private static long Pair(int a, int b) => a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;

        /// <summary>The <paramref name="count"/> hollows nearest this one.</summary>
        private static List<int> Nearest(List<Node> nodes, int i, int count)
        {
            var order = new List<int>();

            for (int j = 0; j < nodes.Count; j++)
            {
                if (j != i)
                    order.Add(j);
            }

            order.Sort((p, q) => Vector2.DistanceSquared(nodes[i].At, nodes[p].At).CompareTo(Vector2.DistanceSquared(nodes[i].At, nodes[q].At)));

            if (order.Count > count)
                order.RemoveRange(count, order.Count - count);

            return order;
        }

        /// <summary>
        /// The shortest tree through every hollow (Prim's), each way as long as its line, and
        /// dearer by far where its line comes nearer the water than <see cref="WayDepth"/>: such
        /// a way is taken only if there is no other.
        /// </summary>
        private static List<(int a, int b)> Tree(List<Node> nodes, Depths depth)
        {
            int count = nodes.Count;
            var inTree = new bool[count];
            var best = new float[count];
            var from = new int[count];
            var edges = new List<(int, int)>();

            for (int i = 0; i < count; i++)
            {
                best[i] = float.MaxValue;
                from[i] = -1;
            }

            best[0] = 0f;

            for (int step = 0; step < count; step++)
            {
                int u = -1;

                for (int i = 0; i < count; i++)
                {
                    if (!inTree[i] && (u < 0 || best[i] < best[u]))
                        u = i;
                }

                inTree[u] = true;

                if (from[u] >= 0)
                    edges.Add((from[u], u));

                for (int v = 0; v < count; v++)
                {
                    if (inTree[v])
                        continue;

                    float cost = Vector2.Distance(nodes[u].At, nodes[v].At);

                    if (depth.Shallowest(nodes[u].At, nodes[v].At) < WayDepth)
                        cost += 4000f;

                    if (cost < best[v])
                    {
                        best[v] = cost;
                        from[v] = u;
                    }
                }
            }

            return edges;
        }

        /// <summary>
        /// A hollow at this place, of a kind rolled by the room it has, and as big as that room
        /// lets it be (<paramref name="room"/>: the half size its longest way may have, so that no
        /// part of it comes nearer the water than <see cref="Skin"/> cells, nor near the next
        /// hollow's middle; its ragged edge reaches 1.15 times its half sizes at most). The great
        /// cavern (already its kind) is as big as the rock allows, up to <see cref="CavernMost"/>
        /// across its half width; where there is room, the big kinds come oftener.
        /// </summary>
        private static void Hollowed(Node n, float room, UnifiedRandom rand)
        {
            float a = rand.NextFloat(), b = rand.NextFloat();
            float rx, ry, lean = 0f;
            bool flat = false;
            Hollow kind;

            if (n.Kind == Hollow.Cavern)
            {
                kind = Hollow.Cavern;
                rx = Math.Min(room, CavernMost * (0.75f + 0.25f * a));
                ry = rx * (0.42f + 0.13f * b);
                lean = (rand.NextFloat() - 0.5f) * 0.3f;
                flat = true;
            }
            else
            {
                float roll = rand.NextFloat();

                // Weights by room: Hall, Dome, Gallery, Grotto, Pocket; the rest a junction.
                float[] weights = room >= 20f ? new[] { 0.30f, 0.15f, 0.20f, 0.25f, 0.05f }
                    : room >= 12f ? new[] { 0.15f, 0.15f, 0.15f, 0.35f, 0.15f }
                    : new[] { 0f, 0f, 0f, 0.40f, 0.50f };
                kind = Hollow.Junction;

                for (int k = 0; k < weights.Length; k++)
                {
                    if (roll < weights[k])
                    {
                        kind = (Hollow)k;
                        break;
                    }

                    roll -= weights[k];
                }

                // The other kinds no bigger than the cavern's least (Hall, Dome) or a little under.
                room = Math.Min(room, kind == Hollow.Gallery ? 38f : kind == Hollow.Grotto ? 18f : 32f);

                switch (kind)
                {
                    case Hollow.Hall:
                        rx = room * (0.7f + 0.3f * a);
                        ry = rx * (0.42f + 0.18f * b);
                        lean = (rand.NextFloat() - 0.5f) * 0.5f;
                        flat = true;
                        break;
                    case Hollow.Dome:
                        ry = room * (0.7f + 0.3f * a);
                        rx = ry * (0.8f + 0.25f * b);
                        lean = (rand.NextFloat() - 0.5f) * 0.3f;
                        break;
                    case Hollow.Gallery:
                        rx = room * (0.8f + 0.2f * a);
                        ry = Math.Max(4.5f, rx * (0.22f + 0.1f * b));
                        lean = (rand.NextFloat() - 0.5f) * 0.5f;
                        flat = true;
                        break;
                    case Hollow.Grotto:
                        rx = room * (0.5f + 0.3f * a);
                        ry = rx * (0.6f + 0.3f * b);
                        lean = (rand.NextFloat() - 0.5f) * 1.6f;
                        break;
                    case Hollow.Pocket:
                        rx = Math.Min(room, 4.5f + 2.5f * a);
                        ry = rx * (0.75f + 0.25f * b);
                        lean = (rand.NextFloat() - 0.5f) * 0.3f;
                        break;
                    default:
                        n.Kind = Hollow.Junction;
                        n.Rx = n.Ry = 3f;
                        Hollows.Add((Hollow.Junction, n.X, n.Y, 3f, 3f));
                        return;
                }
            }

            // No bigger than the room, whichever way it is longest; never too low to swim in.
            float big = Math.Max(rx, ry);

            if (big > room)
            {
                rx *= room / big;
                ry *= room / big;
            }

            ry = Math.Max(ry, 3.6f);
            rx = Math.Max(rx, 4f);

            n.Kind = kind;
            n.Rx = rx;
            n.Ry = ry;

            Blob(n.X + 0.5f, n.Y + 0.5f, rx, ry, lean, flat, rand.Next(1000));
            Hollows.Add((kind, n.X, n.Y, rx, ry));

            if (kind == Hollow.Cavern && rand.Next(2) == 0 || kind is Hollow.Hall or Hollow.Dome or Hollow.Grotto && rand.Next(4) == 0)
            {
                int ex = (int)(Math.Max(rx, ry) * 1.2f) + 3;

                Moss(n.X - ex, n.Y - ex, n.X + ex, n.Y + ex, Glows[rand.Next(Glows.Length)]);
                Log.Add(("cave moss garden", n.X, n.Y));
            }

            Log.Add(("cave " + kind.ToString().ToLowerInvariant(), n.X, n.Y));
        }

        /// <summary>
        /// A hollow: an ellipse turned by <paramref name="lean"/>, its edge ragged by noise (out
        /// to 1.145 times its half sizes at most); with <paramref name="flat"/>, its lower half
        /// pressed up, so that its floor is flatter than its roof.
        /// </summary>
        private static void Blob(float cx, float cy, float rx, float ry, float lean, bool flat, int salt)
        {
            float c = (float)Math.Cos(lean), s = (float)Math.Sin(lean);
            int r = (int)Math.Ceiling(Math.Max(rx, ry) * 1.2f) + 2;

            for (int x = (int)cx - r; x <= (int)cx + r; x++)
            {
                for (int y = (int)cy - r; y <= (int)cy + r; y++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float u = (dx * c + dy * s) / rx;
                    float v = (-dx * s + dy * c) / ry;

                    if (flat && v > 0f)
                        v *= 1.35f;

                    float edge = 1f + 0.1f * Noise2(x * 0.11f, y * 0.11f, seed + salt) + 0.045f * Noise2(x * 0.3f, y * 0.3f, seed + salt + 1);

                    if (u * u + v * v < edge * edge)
                        Open(x, y);
                }
            }
        }

        /// <summary>
        /// A way between two hollows: bending once (twice on a long one) to one side by up to an
        /// eighth of its length, if the rock under the bent line is deep enough, else straight;
        /// narrow one time in five, wide one in seven, else between.
        /// </summary>
        private static void Way(Node a, Node b, Depths depth, UnifiedRandom rand)
        {
            Vector2 from = a.At, to = b.At;
            float length = Vector2.Distance(from, to);
            Vector2 along = (to - from) / Math.Max(1f, length);
            Vector2 side = new Vector2(-along.Y, along.X);
            List<Vector2> path;

            if (length > 70f)
            {
                path = new List<Vector2>
                {
                    from,
                    Vector2.Lerp(from, to, 0.33f) + side * (rand.NextFloat() * 2f - 1f) * 0.12f * length,
                    Vector2.Lerp(from, to, 0.67f) + side * (rand.NextFloat() * 2f - 1f) * 0.12f * length,
                    to,
                };
            }
            else
            {
                path = new List<Vector2> { from, (from + to) * 0.5f + side * (rand.NextFloat() * 2f - 1f) * 0.12f * length, to };
            }

            if (depth.Shallowest(path) < WayDepth)
                path = new List<Vector2> { from, (from + to) * 0.5f, to };

            float roll = rand.NextFloat();
            float radius = roll < 0.2f ? 2.7f + 0.2f * rand.NextFloat() : roll < 0.34f ? 3.8f + 0.7f * rand.NextFloat() : 3f + 0.4f * rand.NextFloat();

            Tunnel(path, radius, rand);
            Ways.Add(("range way", from, to));
        }

        /// <summary>
        /// Three to five mouths on the water, at least one each side of the range's middle: from
        /// a hollow down the steepest way through the rock's depths (<see cref="Descend"/>), the
        /// hollows with the shortest such way first, no two coming out within forty columns and
        /// rows of each other. Returns how many were cut.
        /// </summary>
        private static int Mouths(Range range, List<Node> nodes, Depths depth, UnifiedRandom rand)
        {
            var ways = new List<(Node node, List<Vector2> path, Vector2 end, int steps)>();

            foreach (Node n in nodes)
            {
                List<Vector2> path = Descend(depth, n.X, n.Y, rand, out Vector2 end, out int steps);

                if (path != null)
                    ways.Add((n, path, end, steps));
            }

            ways.Sort((p, q) => p.steps.CompareTo(q.steps));

            var chosen = new List<(Node node, List<Vector2> path, Vector2 end, int steps)>();

            bool Apart(Vector2 end)
            {
                foreach (var c in chosen)
                {
                    if (Math.Abs(c.end.X - end.X) < 40f && Math.Abs(c.end.Y - end.Y) < 40f)
                        return false;
                }

                return true;
            }

            foreach (int sideOf in new[] { -1, 1 })
            {
                foreach (var w in ways)
                {
                    if (Math.Sign(w.end.X - range.Middle) == sideOf && Apart(w.end) && !chosen.Exists(c => c.node == w.node))
                    {
                        chosen.Add(w);
                        break;
                    }
                }
            }

            int more = 1 + rand.Next(3);

            foreach (var w in ways)
            {
                if (more <= 0)
                    break;

                if (chosen.Exists(c => c.node == w.node) || !Apart(w.end))
                    continue;

                chosen.Add(w);
                more--;
            }

            foreach (var (node, path, end, steps) in chosen)
            {
                Tunnel(path, 3f + 0.4f * rand.NextFloat(), rand);
                Ways.Add(("range mouth", node.At, end));
            }

            return chosen.Count;
        }

        /// <summary>
        /// From a cell in the rock to the water by the steepest way down its depths: each step to
        /// the cell about it that is least deep (the same way as the last step, if it is as good;
        /// across rather than up or down, if those are as good), until in the open; then six cells
        /// on, the same way. A point every six steps, for a tunnel to follow. Null if no way out
        /// was found in four hundred steps.
        /// </summary>
        private static List<Vector2> Descend(Depths depth, int x, int y, UnifiedRandom rand, out Vector2 end, out int steps)
        {
            var path = new List<Vector2> { new Vector2(x + 0.5f, y + 0.5f) };
            int lastX = 0, lastY = 0;

            steps = 0;
            end = Vector2.Zero;

            while (depth.At(x, y) > 0)
            {
                if (++steps > 400)
                    return null;

                int bestScore = int.MaxValue, bx = 0, by = 0;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        int score = depth.At(x + dx, y + dy) * 16 + (dx == lastX && dy == lastY ? 0 : 4) + (dx == 0 ? 2 : 0) + rand.Next(2);

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bx = dx;
                            by = dy;
                        }
                    }
                }

                x += bx;
                y += by;
                lastX = bx;
                lastY = by;

                if (steps % 6 == 0)
                    path.Add(new Vector2(x + 0.5f, y + 0.5f));
            }

            end = new Vector2(x + 0.5f + lastX * 6, y + 0.5f + lastY * 6);
            path.Add(end);
            return path;
        }

        /// <summary>
        /// A dead end off a hollow, at a random heading: from its edge as far as the rock under
        /// it is at least eight deep, fourteen to forty cells; none if that is under ten. A pocket
        /// at its end if the rock there is deep enough for one.
        /// </summary>
        private static bool DeadEnd(Node n, Depths depth, UnifiedRandom rand)
        {
            float heading = rand.NextFloat() * MathHelper.TwoPi;
            Vector2 dir = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading));
            Vector2 start = n.At + dir * Math.Max(n.Rx, n.Ry) * 0.8f;
            int length = 14 + rand.Next(27);
            int ok = 0;

            for (int k = 2; k <= length; k += 2)
            {
                if (depth.At(start + dir * k) < 8)
                    break;

                ok = k;
            }

            if (ok < 10)
                return false;

            Vector2 end = start + dir * ok;
            Vector2 bend = Vector2.Lerp(start, end, 0.5f) + new Vector2(-dir.Y, dir.X) * (rand.NextFloat() * 2f - 1f) * 0.15f * ok;

            Tunnel(new List<Vector2> { n.At, start, bend, end }, 2.7f + 0.2f * rand.NextFloat(), rand);

            if (depth.At(end) >= 11)
            {
                float rx = 3.6f + 1.4f * rand.NextFloat(), ry = 3.6f + 0.8f * rand.NextFloat();

                Blob(end.X, end.Y, rx, ry, rand.NextFloat() * MathHelper.Pi, false, rand.Next(1000));
                Hollows.Add((Hollow.Pocket, (int)end.X, (int)end.Y, rx, ry));
            }

            Ways.Add(("range dead end", n.At, end));
            return true;
        }

        // ---- the dripstone ----------------------------------------------------------------------

        /// <summary>
        /// Dripstone of the rock in every hollow but the smallest: every three to seven columns
        /// across its middle, where its roof is broad (seven columns of rock across) a point of
        /// rock hanging from it, two in five times, up to three tenths of the open height under
        /// it; where its floor is broad, one rising from it, one time in four, up to a fifth;
        /// and where both are broad and fourteen rows or more apart, a pair all but meeting,
        /// five or six rows left between them: one time in three in a great cavern, one in five
        /// in a hall. Each put in only if, with it, every column about it still has five rows
        /// open through the hollow, and every two side by side four rows open together
        /// (<see cref="Leaves"/>): never a way through a hollow shut.
        /// </summary>
        private static void Dripstones(UnifiedRandom rand)
        {
            int hung = 0, stood = 0, columns = 0, refused = 0;

            foreach (var (kind, cx, cy, rx, ry) in Hollows)
            {
                if (kind is Hollow.Pocket or Hollow.Junction)
                    continue;

                int span = (int)(rx * 0.9f);
                int reach = (int)(Math.Max(rx, ry) * 1.3f) + 4;
                int x0 = cx - span - 6;
                int w = span * 2 + 13;

                // The open run of each column through the hollow's middle row: its first and its
                // last open row (none, where the middle row is rock there).
                var top = new int[w];
                var bottom = new int[w];

                for (int i = 0; i < w; i++)
                {
                    int x = x0 + i;

                    if (SolidAt(x, cy))
                    {
                        top[i] = 1;
                        bottom[i] = 0;
                        continue;
                    }

                    int t = cy, b = cy;

                    while (t > cy - reach && !SolidAt(x, t - 1))
                        t--;

                    while (b < cy + reach && !SolidAt(x, b + 1))
                        b++;

                    top[i] = t;
                    bottom[i] = b;
                }

                for (int x = cx - span + rand.Next(3); x <= cx + span; x += 3 + rand.Next(5))
                {
                    int i = x - x0;

                    if (i < 5 || i >= w - 5 || top[i] > bottom[i])
                        continue;

                    int roof = top[i] - 1, floor = bottom[i] + 1;

                    // A closed hollow here: rock over and under, not the reach run out.
                    if (!SolidAt(x, roof) || !SolidAt(x, floor))
                        continue;

                    int open = int.MaxValue;

                    for (int k = -4; k <= 4; k++)
                    {
                        int j = i + k;
                        open = top[j] > bottom[j] ? 0 : Math.Min(open, bottom[j] - top[j] + 1);
                    }

                    if (open < 10)
                        continue;

                    bool broadRoof = Broad(x, roof), broadFloor = Broad(x, floor);
                    int down = 0, up = 0;
                    bool pair = false;

                    if ((kind == Hollow.Cavern && rand.Next(3) == 0 || kind == Hollow.Hall && rand.Next(5) == 0) && broadRoof && broadFloor && open >= 14)
                    {
                        int gap = 5 + rand.Next(2);
                        down = (open - gap) * (45 + rand.Next(16)) / 100;
                        up = open - gap - down;
                        pair = true;
                    }
                    else
                    {
                        if (broadRoof && rand.NextFloat() < 0.4f)
                            down = (int)(open * (0.1f + 0.2f * rand.NextFloat()));

                        if (broadFloor && rand.NextFloat() < 0.25f)
                            up = (int)(open * (0.06f + 0.14f * rand.NextFloat()));
                    }

                    if (down < 2)
                        down = 0;

                    if (up < 2)
                        up = 0;

                    if (down == 0 && up == 0)
                        continue;

                    if (!Leaves(top, bottom, i, roof, down, floor, up))
                    {
                        // A pair goes in whole or not at all; else the hanging one alone, or the
                        // standing one alone, whichever leaves the way open.
                        if (pair)
                        {
                            refused++;
                            continue;
                        }

                        if (down > 0 && Leaves(top, bottom, i, roof, down, floor, 0))
                            up = 0;
                        else if (up > 0 && Leaves(top, bottom, i, roof, 0, floor, up))
                            down = 0;
                        else
                        {
                            refused++;
                            continue;
                        }
                    }

                    if (down > 0)
                    {
                        Drip(x, roof, 1, down, DripRock(x, roof));
                        Narrow(top, bottom, i, roof, down, true);
                        hung++;
                    }

                    if (up > 0)
                    {
                        Drip(x, floor, -1, up, DripRock(x, floor));
                        Narrow(top, bottom, i, floor, up, false);
                        stood++;
                    }

                    if (pair)
                        columns++;
                }
            }

            Log.Add(($"dripstone: {hung} hanging, {stood} standing, {columns} of them pairs all but meeting; {refused} left out for the way through", 0, 0));
        }

        /// <summary>How many cells from its root a point of rock this long fills in the column <paramref name="d"/> from its middle (the shape <see cref="Drip"/> gives it).</summary>
        private static int Reach(int length, int d)
        {
            int half = Math.Min(3, length / 3);
            int most = 0;

            for (int k = 1; k <= length; k++)
            {
                float t = (float)(k - 1) / length;

                if ((int)Math.Round(half * (1f - t)) >= Math.Abs(d))
                    most = k;
            }

            return most;
        }

        /// <summary>The open runs of the columns a point of rock covers, narrowed by it: from the top (hanging) or the bottom (standing).</summary>
        private static void Narrow(int[] top, int[] bottom, int i, int root, int length, bool hanging)
        {
            for (int d = -3; d <= 3; d++)
            {
                int j = i + d;
                int fill = Reach(length, d);

                if (fill == 0 || j < 0 || j >= top.Length || top[j] > bottom[j])
                    continue;

                if (hanging)
                    top[j] = Math.Max(top[j], root + fill + 1);
                else
                    bottom[j] = Math.Min(bottom[j], root - fill - 1);
            }
        }

        /// <summary>
        /// With a point of rock <paramref name="down"/> long hanging from the roof at
        /// <paramref name="roof"/> and one <paramref name="up"/> long standing on the floor at
        /// <paramref name="floor"/> (either 0 for none), over column <paramref name="i"/>: every
        /// open column within four of it still five rows open, and every two open side by side
        /// four rows open together.
        /// </summary>
        private static bool Leaves(int[] top, int[] bottom, int i, int roof, int down, int floor, int up)
        {
            var t = new int[11];
            var b = new int[11];

            for (int d = -5; d <= 5; d++)
            {
                int j = i + d;

                if (j < 0 || j >= top.Length || top[j] > bottom[j])
                {
                    t[d + 5] = 1;
                    b[d + 5] = 0;
                    continue;
                }

                t[d + 5] = top[j];
                b[d + 5] = bottom[j];

                if (down > 0 && Math.Abs(d) <= 3 && Reach(down, d) > 0)
                    t[d + 5] = Math.Max(t[d + 5], roof + Reach(down, d) + 1);

                if (up > 0 && Math.Abs(d) <= 3 && Reach(up, d) > 0)
                    b[d + 5] = Math.Min(b[d + 5], floor - Reach(up, d) - 1);
            }

            for (int k = 1; k <= 9; k++)
            {
                int j = i + k - 5;

                if (j < 0 || j >= top.Length || top[j] > bottom[j])
                    continue;

                if (b[k] - t[k] + 1 < 5)
                    return false;

                int n = j + 1;

                if (n < top.Length && top[n] <= bottom[n] && Math.Min(b[k], b[k + 1]) - Math.Max(t[k], t[k + 1]) + 1 < 4)
                    return false;
            }

            return true;
        }

        /// <summary>A broad face: rock across seven columns at this row.</summary>
        private static bool Broad(int x, int y)
        {
            for (int k = -3; k <= 3; k++)
            {
                if (!SolidAt(x + k, y))
                    return false;
            }

            return true;
        }

        /// <summary>What dripstone hanging from (or standing on) this cell is made of: its rock; basalt where it is a glowing moss; hardened sand for sand; nothing (0) for silt.</summary>
        private static ushort DripRock(int x, int y)
        {
            ushort type = Main.tile[x, y].TileType;

            if (Main.tileMoss[type])
                return Dark;

            if (type == TileID.Sand)
                return TileID.HardenedSand;

            return type == TileID.Silt ? (ushort)0 : type;
        }

        /// <summary>A point of rock from this cell (<paramref name="dir"/> 1, hanging down; -1, standing up), this long: up to seven across at its root (a column more either side for every three of its length, three at most), coming to one.</summary>
        private static void Drip(int x, int root, int dir, int length, ushort type)
        {
            if (type == 0)
                return;

            int half = Math.Min(3, length / 3);

            for (int k = 1; k <= length; k++)
            {
                float t = (float)(k - 1) / length;
                int w = (int)Math.Round(half * (1f - t));

                for (int dx = -w; dx <= w; dx++)
                    Set(x + dx, root + dir * k, type);
            }
        }

        // ---- the crevices --------------------------------------------------------------------

        /// <summary>
        /// The eels' crevices: off half the hollows (one, or two off a hall or a gallery), tried
        /// eight times each: from a little off the hollow's middle line, out to its wall one of
        /// four ways, then straight into the rock (<see cref="TryLair"/>).
        /// </summary>
        private static void Crevices(UnifiedRandom rand)
        {
            walling = WallByRock;

            foreach (var (kind, cx, cy, rx, ry) in Hollows)
            {
                if (kind == Hollow.Junction || rand.Next(2) == 0)
                    continue;

                int most = kind == Hollow.Cavern ? 3 : kind is Hollow.Hall or Hollow.Gallery ? 2 : 1;
                int made = 0;

                for (int attempt = 0; attempt < 8 && made < most; attempt++)
                {
                    int way = rand.Next(4);
                    int ix = way == 0 ? 1 : way == 1 ? -1 : 0, iy = way == 2 ? 1 : way == 3 ? -1 : 0;
                    float spread = iy == 0 ? ry * 0.5f : rx * 0.5f;
                    int off = (int)((rand.NextFloat() * 2f - 1f) * spread);
                    int mx = cx + (iy == 0 ? 0 : off), my = cy + (iy == 0 ? off : 0);

                    if (SolidAt(mx, my))
                        continue;

                    int guard = 0;

                    while (!SolidAt(mx, my) && guard++ < 80)
                    {
                        mx += ix;
                        my += iy;
                    }

                    if (guard >= 80)
                        continue;

                    if (TryLair(mx, my, ix, iy, LairShortest + rand.Next(LairLongest - LairShortest + 1)))
                        made++;
                }
            }

            walling = NoWalls;
        }

        /// <summary>
        /// A crevice from the rock face at (<paramref name="mx"/>, <paramref name="my"/>) straight
        /// into the rock the way given (<paramref name="ix"/>, <paramref name="iy"/>), two cells
        /// across (the second the next row down, for one that runs across; the next column right,
        /// for one that runs up or down), this many long. Cut only if all of it is rock now, with
        /// two cells of rock either side of it from its back to two short of its mouth, and two
        /// rows of rock six across behind its back; and the two cells before its mouth, both
        /// across, open, the second of them with open cells either side (the hollow it opens on,
        /// wider than it: not the end of another crevice). Recorded by its back and the way out.
        /// </summary>
        private static bool TryLair(int mx, int my, int ix, int iy, int length)
        {
            int px = iy == 0 ? 0 : 1, py = iy == 0 ? 1 : 0;
            int bx = mx + ix * (length - 1), by = my + iy * (length - 1);
            int dx = -ix, dy = -iy;

            if (Math.Min(bx, mx) < Left || Math.Max(bx, mx) > Right || Math.Min(by, my) < VastOcean.Surface + 20 || Math.Max(by, my) > VastOcean.High - 50)
                return false;

            for (int k = -2; k < length; k++)
            {
                for (int j = -2; j <= 3; j++)
                {
                    bool burrow = k >= 0 && (j == 0 || j == 1);

                    if (!burrow && k > length - 3)
                        continue;

                    if (!SolidAt(bx + dx * k + px * j, by + dy * k + py * j))
                        return false;
                }
            }

            for (int k = length; k <= length + 1; k++)
            {
                for (int j = 0; j <= 1; j++)
                {
                    if (SolidAt(bx + dx * k + px * j, by + dy * k + py * j))
                        return false;
                }
            }

            // And the space it opens on wider than itself (not another crevice's end).
            if (SolidAt(bx + dx * (length + 1) - px, by + dy * (length + 1) - py) || SolidAt(bx + dx * (length + 1) + 2 * px, by + dy * (length + 1) + 2 * py))
                return false;

            for (int k = 0; k < length; k++)
            {
                for (int j = 0; j <= 1; j++)
                    Open(bx + dx * k + px * j, by + dy * k + py * j);
            }

            Lairs.Add((bx, by, dx, dy, length));
            Log.Add(("eel crevice", bx, by));
            return true;
        }

        // ---- the walls -------------------------------------------------------------------------

        /// <summary>
        /// The wall behind a cell cut out of this rock: the game's cave walls nearest it in
        /// colour. Basalt, and the rest of the volcanic rock, grey-blue cobbles (Cave4) with a
        /// few patches of darker ones (Cave8: the granite's own wall, near black, read as no wall
        /// at all); stone, its rocks; the red clay, a reddish earth
        /// (Cave6); the mud, the mud's; mudstone, a darker earth (Cave7); sand, hardened sand
        /// and the fossils in them, hardened sand's; sandstone, its own; silt, a grey earth
        /// (Cave3); obsidian, ice and marble, their own.
        /// </summary>
        private static ushort WallFor(ushort type, int x, int y)
        {
            switch (type)
            {
                case TileID.Stone:
                    return WallID.RocksUnsafe1;
                case TileID.ClayBlock:
                    return WallID.Cave6Unsafe;
                case TileID.Mud:
                    return WallID.MudUnsafe;
                case TileID.Mudstone:
                    return WallID.Cave7Unsafe;
                case TileID.Sand:
                case TileID.HardenedSand:
                case TileID.FossilOre:
                case TileID.DesertFossil:
                    return WallID.HardenedSand;
                case TileID.Sandstone:
                    return WallID.Sandstone;
                case TileID.Silt:
                    return WallID.Cave3Unsafe;
                case TileID.Obsidian:
                    return WallID.ObsidianBackUnsafe;
                case TileID.IceBlock:
                    return WallID.IceUnsafe;
                case TileID.Marble:
                    return WallID.MarbleUnsafe;
                default:
                    return Noise2(x * 0.08f, y * 0.08f, seed + 301) > 0.5f ? WallID.Cave8Unsafe : WallID.Cave4Unsafe;
            }
        }

        /// <summary>The game's wall of a gem's cave, for a geode lined with that gem.</summary>
        private static ushort GemWall(ushort gem)
        {
            switch (gem)
            {
                case TileID.Amethyst:
                    return WallID.AmethystUnsafe;
                case TileID.Topaz:
                    return WallID.TopazUnsafe;
                case TileID.Sapphire:
                    return WallID.SapphireUnsafe;
                case TileID.Emerald:
                    return WallID.EmeraldUnsafe;
                case TileID.Ruby:
                    return WallID.RubyUnsafe;
                default:
                    return WallID.DiamondUnsafe;
            }
        }

        /// <summary>
        /// Every rock cell next to one given a wall by the cutting (the eight about it) gets the
        /// same wall behind it, if it has none: the faces worn to slopes and halves afterwards
        /// (<see cref="Smooth"/>) show wall, not the open sea, in their corners.
        /// </summary>
        private static void FinishWalls()
        {
            int spread = 0;

            foreach (int packed in walled)
            {
                int x = packed % VastOcean.Wide, y = packed / VastOcean.Wide;
                ushort wall = Main.tile[x, y].WallType;

                if (wall == 0)
                    continue;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;

                        if (nx < 1 || ny <= VastOcean.Surface || nx >= VastOcean.Wide - 1 || ny >= VastOcean.High - 1)
                            continue;

                        Tile t = Main.tile[nx, ny];

                        if (t.HasTile && t.WallType == 0)
                        {
                            t.WallType = wall;
                            spread++;
                        }
                    }
                }
            }

            Log.Add(($"walls: {walled.Count} cut cells walled, {spread} rock cells beside them", 0, 0));
        }
    }
}
