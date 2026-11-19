using System;
using System.Collections.Generic;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The Sump's plan (SludgePocket lays it in the world), by request: "a sub biome at the bottom of the jungle where
    /// the poison and toxic sludge is", then "this pocket should be 2/3 as wide as the jungle; think sunken sea under
    /// the desert". What goes in each cell of a canvas, from what the ground there holds. Nothing of the game in it, so
    /// that it can be drawn out and looked at before it is ever in a world (the preview harness compiles this file
    /// alone).
    ///
    ///   the cavern    as wide as it is asked to be (two thirds of the Jungle), about three tenths as high: a ceiling and
    ///                 a floor running the length of it, closing at the ends, both wandering; the ceiling hung with
    ///                 points of mud, a few columns standing floor to ceiling, islands rising out of the lake.
    ///   the lake      sludge, from <see cref="Pocket.Level"/> down, across most of the floor, deep (by request, "make it
    ///                 twice as deep": its bed falls twice as far below the surface as the cavern's shape alone would
    ///                 have it, about two fifths of the cavern's height at its deepest); shores at either end and round
    ///                 each island. The dry floor is stepped in terraces three rows apart, flat enough to stand the
    ///                 works on and to walk.
    ///   the shell     <see cref="ShellDepth"/> tiles of its own blocks all round it, solid whatever was there (so the
    ///                 lake can never run out): Acidic Mud, with patches of Stained Cobblestone; the lake's bed and
    ///                 sides lined two deep with the cobblestone.
    ///   the halftone  beyond the shell, a band (wider for a wider cavern) of the ground around turning into it (by
    ///                 request: "blends onto nearby tiles using halftone gradient or halftone dissolve of tiles"): an
    ///                 ordered dither (a 4x4 Bayer matrix, on the world's own grid), all of it at the shell and none at
    ///                 the band's far edge. Only what was solid turns: soft ground into Acidic Mud, hard into Stained
    ///                 Cobblestone. Open ground stays open.
    ///   the ways in   from each end, out and up, and one up from the ceiling: dug until they come out into open
    ///                 ground (on in the world past the canvas, if need be: <see cref="Pocket.Ends"/>).
    ///   the works     the remnants of what poisoned the Jungle (Lore Bible §XVII, "pumps, vats, pipes"), along the
    ///                 shores and terraces: vats, each with a pipe down from the rock (where the pumps were) and an
    ///                 outlet run to the floor's edge, out over the lake; barrels in clusters (upright, dented,
    ///                 toppled, crushed), some standing in the shallows, some with a pipe open over them; a few barrels
    ///                 still shut and holding something (chests).
    ///   what is kept  a temple, an altar, larvae, another mod's work: the cavern keeps its distance from them (the
    ///                 shell and more) and wraps round them. Chests in the way are not kept here: SludgePocket moves
    ///                 them, whole, onto free floor (<see cref="Pocket.Spare"/>).
    /// </summary>
    public static class SludgePocketPlan
    {
        /// <summary>
        /// What the ground holds at a cell: open (or nothing solid); soft (mud, dirt and the like) or hard (stone and
        /// the like), which the halftone turns; other solid ground it leaves alone (ores, hive, bricks); and kept, which
        /// nothing may come near (a temple, an altar, larvae).
        /// </summary>
        public enum Ground : byte { Air, Soft, Hard, Other, Kept }

        public enum Cell : byte { Same, Open, Mud, Cobble, Sludge }

        public enum Barrel : byte { Upright, Dented, Toppled, Crushed }

        public enum Pipe : byte { Across, Down, RightDown, LeftDown, UpRight, UpLeft, MouthDown, MouthRight, MouthLeft, Valve, JointAcross, JointDown, Broken }

        public enum Kind : byte { Barrel, Pipe, Vat, Chest }

        /// <summary>Something set in the pocket: its kind, its top left cell, its style.</summary>
        public readonly struct Thing
        {
            public readonly Kind Kind;
            public readonly int X, Y, Style;

            public Thing(Kind kind, int x, int y, int style)
            {
                Kind = kind;
                X = x;
                Y = y;
                Style = style;
            }

            public int Wide => Kind == Kind.Vat ? 4 : Kind == Kind.Pipe ? 1 : 2;

            public int Tall => Kind == Kind.Vat ? 4 : Kind == Kind.Pipe ? 1 : 2;
        }

        public sealed class Pocket
        {
            public readonly int Wide, High, Band;
            public readonly Cell[,] Cells;
            public readonly List<Thing> Things = new();
            public readonly List<string> Notes = new();
            public bool Fine;
            public string Why = "";

            /// <summary>The lake's surface row: sludge from it down.</summary>
            public int Level;

            /// <summary>The cavern's extent on the canvas (its open cells' bounds).</summary>
            public int Left, Top, Right, Bottom;

            /// <summary>Where each way in that did not reach open ground on the canvas stopped, and which way it was going: dug on in the world.</summary>
            public readonly List<(int X, int Y, float Ax, float Ay)> Ends = new();

            /// <summary>Floor left free, two wide and two high (their top left cells), nearest the middle first: for chests moved out of the way.</summary>
            public readonly List<(int X, int Y)> Spare = new();

            /// <summary>A place in the cavern to stand, on dry floor (the cell over the floor).</summary>
            public (int X, int Y) Inside = (-1, -1);

            /// <summary>Cells something has been set in, or kept clear about it, while the works are laid.</summary>
            internal bool[,] Taken;

            public Pocket(int wide, int high, int band)
            {
                Wide = wide;
                High = high;
                Band = band;
                Cells = new Cell[wide, high];
            }
        }

        public const int ShellDepth = 5;

        /// <summary>The halftone band's depth, for a cavern this wide.</summary>
        public static int BandFor(int cavernWide) => Math.Clamp(cavernWide / 40, 12, 22);

        /// <summary>The canvas's margin round the cavern: the shell, the band, and a little.</summary>
        public static int MarginFor(int cavernWide) => ShellDepth + BandFor(cavernWide) + 3;

        /// <summary>How high a cavern this wide is, its lake's depth with it (before the world's depth limits it).</summary>
        public static int HighFor(int cavernWide) => (int)(Math.Clamp(cavernWide * 0.24f, 56f, 190f) * Deepen);

        /// <summary>How much taller the deeper lake makes the cavern than its shape above the lake asks.</summary>
        private const float Deepen = 1.25f;

        private static readonly int[,] Bayer =
        {
            { 0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            { 3, 11, 1, 9 },
            { 15, 7, 13, 5 },
        };

        // ---- noise -------------------------------------------------------------------------------------------------

        private static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        private static float Noise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float xf = x - xi, yf = y - yi;
            float sx = xf * xf * (3 - 2 * xf), sy = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return (a * (1 - sx) + b * sx) * (1 - sy) + (c * (1 - sx) + d * sx) * sy;
        }

        /// <summary>Two octaves along a line, about -0.5..0.5.</summary>
        private static float Wander(float x, int seed) => Noise(x, 0.5f, seed) * 0.7f + Noise(x * 2.7f, 7.5f, seed + 1) * 0.3f - 0.5f;

        // ---- the plan ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The pocket for a cavern <paramref name="cavernWide"/> by <paramref name="cavernHigh"/>, on a canvas with
        /// <see cref="MarginFor"/> round it, for ground as <paramref name="ground"/> has it (canvas cells);
        /// <paramref name="phaseX"/> and <paramref name="phaseY"/> the canvas's corner on the world's grid, so that the
        /// halftone lines up with it.
        /// </summary>
        public static Pocket Make(Func<int, int, Ground> ground, int seed, int cavernWide, int cavernHigh, int phaseX = 0, int phaseY = 0)
        {
            int margin = MarginFor(cavernWide);
            Pocket p = new(cavernWide + margin * 2, cavernHigh + margin * 2, BandFor(cavernWide));
            int W = p.Wide, H = p.High;
            Random rng = new(seed);

            Ground[,] g = new Ground[W, H];
            bool[,] kept = new bool[W, H];
            bool anyKept = false;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    g[x, y] = ground(x, y);
                    kept[x, y] = g[x, y] == Ground.Kept;
                    anyKept |= kept[x, y];
                }
            }

            float[,] fromKept = anyKept ? Distance(kept) : null;
            bool Clear(int x, int y) => fromKept == null || fromKept[x, y] > ShellDepth + 3;

            bool[,] hollow = Cavern(p, margin, cavernWide, cavernHigh, rng, seed, out int expected);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (hollow[x, y] && !Clear(x, y))
                        hollow[x, y] = false;

            int size = Biggest(hollow);

            if (size < expected * 0.6f)
            {
                p.Why = $"too much kept in the way ({size} of {expected} cells left)";
                return p;
            }

            Terraces(p, hollow, Clear);
            Bounds(p, hollow);

            float[,] far = Distance(hollow);

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float d = far[x, y];

                    if (hollow[x, y])
                    {
                        p.Cells[x, y] = y >= p.Level ? Cell.Sludge : Cell.Open;
                    }
                    else if (g[x, y] == Ground.Kept)
                    {
                        continue;
                    }
                    else if (d <= ShellDepth)
                    {
                        bool lining = d <= 2.2f && y >= p.Level - 1;
                        bool patch = Noise(x * 0.12f, y * 0.12f, seed + 3) > 0.64f;
                        p.Cells[x, y] = lining || patch ? Cell.Cobble : Cell.Mud;
                    }
                    else if (d <= ShellDepth + p.Band)
                    {
                        Ground here = g[x, y];

                        if (here != Ground.Soft && here != Ground.Hard)
                            continue;

                        float t = 1f - (d - ShellDepth) / (p.Band + 1f);
                        float threshold = (Bayer[(y + phaseY) & 3, (x + phaseX) & 3] + 0.5f) / 16f;

                        if (threshold < t)
                            p.Cells[x, y] = here == Ground.Soft ? Cell.Mud : Cell.Cobble;
                    }
                }
            }

            Tunnels(p, hollow, far, g, rng);
            p.Taken = new bool[W, H];
            Works(p, rng, cavernWide);
            Drips(p, hollow, rng, seed, cavernHigh);
            Leftover(p);
            p.Taken = null;
            p.Fine = true;
            return p;
        }

        /// <summary>
        /// The cavern's open cells: between a ceiling and a floor that run its length, wandering, and close at its
        /// ends; islands lifting the floor; columns from floor to ceiling; worn at its edges and smoothed. The lake's
        /// level is set here too. <paramref name="expected"/>: how many cells it has before anything kept is taken out.
        /// </summary>
        private static bool[,] Cavern(Pocket p, int margin, int cavernWide, int cavernHigh, Random rng, int seed, out int expected)
        {
            int W = p.Wide, H = p.High;
            bool[,] h = new bool[W, H];
            // The shape is worked out on the height above the lake's deepening (the rest is the lake's bed, below).
            float body = cavernHigh / Deepen;
            float middle = margin + body * 0.44f;
            float half = body * 0.5f;
            p.Level = (int)(middle + body * 0.22f);

            // Islands: bumps in the floor, rising above the lake. Columns: where floor and ceiling meet.
            int islands = 1 + cavernWide / 260 + rng.Next(2);
            int columns = 1 + cavernWide / 220 + rng.Next(2);
            (float X, float Wide)[] lift = new (float, float)[islands];
            (float X, float Wide)[] stand = new (float, float)[columns];

            for (int i = 0; i < islands; i++)
                lift[i] = (margin + cavernWide * (0.2f + 0.6f * (i + 0.5f) / islands) + rng.Next(-20, 21), cavernWide * (0.03f + 0.025f * (float)rng.NextDouble()));

            for (int i = 0; i < columns; i++)
                stand[i] = (margin + cavernWide * (0.15f + 0.7f * (float)rng.NextDouble()), 3f + rng.Next(0, 6));

            float[] ceiling = new float[W], floor = new float[W];

            for (int x = 0; x < W; x++)
            {
                ceiling[x] = floor[x] = middle;
                float t = (x - margin + 0.5f) / cavernWide;

                if (t <= 0f || t >= 1f)
                    continue;

                float envelope = (float)Math.Pow(Math.Sin(Math.PI * t), 0.5);
                float up = half * envelope * (0.8f + 0.5f * Wander(x / 26f, seed + 11));
                float down = half * envelope * (0.86f + 0.3f * Wander(x / 34f, seed + 13));
                ceiling[x] = middle - up;

                // Under the lake, the bed falls twice as far.
                float bed = middle + down;

                if (bed > p.Level)
                    bed = p.Level + (bed - p.Level) * 2f;

                floor[x] = Math.Min(margin + cavernHigh - 1, bed);

                // An island: flat topped, a few rows above the lake, its sides falling steeply into it.
                foreach (var i in lift)
                {
                    float k = (x - i.X) / i.Wide;
                    float top = p.Level - 3 - 5 * Noise(x / 11f, 2.5f, seed + 17);

                    if (Math.Abs(k) < 1.8f)
                        floor[x] = Math.Min(floor[x], floor[x] + (top - floor[x]) * (float)Math.Exp(-k * k * k * k * 1.4f));
                }
            }

            for (int x = 1; x < W - 1; x++)
            {
                for (int y = 1; y < H - 1; y++)
                {
                    float rough = (Noise(x * 0.14f, y * 0.14f, seed + 21) - 0.5f) * 5f;
                    bool open = y > ceiling[x] + rough && y < floor[x] + rough * 0.6f;

                    foreach (var c in stand)
                    {
                        // A column all but whole: down from the ceiling and up from the floor, wider at either end,
                        // the two not meeting (the gap a fifth of the height or so, so that the cavern stays one).
                        float along = (y - ceiling[x]) / Math.Max(1f, floor[x] - ceiling[x]);
                        float width = c.Wide * (1f + 1.6f * Math.Abs(along - 0.42f)) + rough * 0.3f;

                        if (Math.Abs(x - c.X) < width && Math.Abs(along - 0.42f) > 0.11f + 0.04f * Noise(x * 0.3f, 1.5f, seed + 23))
                            open = false;
                    }

                    h[x, y] = open;
                }
            }

            for (int pass = 0; pass < 2; pass++)
            {
                bool[,] next = new bool[W, H];

                for (int y = 1; y < H - 1; y++)
                {
                    for (int x = 1; x < W - 1; x++)
                    {
                        int n = 0;

                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if ((dx != 0 || dy != 0) && h[x + dx, y + dy])
                                    n++;

                        next[x, y] = n >= 5 || h[x, y] && n == 4;
                    }
                }

                h = next;
            }

            expected = Biggest(h);
            return h;
        }

        /// <summary>Keeps the biggest piece of open cells only; how many cells it has.</summary>
        private static int Biggest(bool[,] h)
        {
            int W = h.GetLength(0), H = h.GetLength(1);
            int[,] piece = new int[W, H];
            int best = 0, bestSize = 0, label = 0;
            Queue<(int, int)> queue = new();

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (!h[x, y] || piece[x, y] != 0)
                        continue;

                    label++;
                    int size = 0;
                    piece[x, y] = label;
                    queue.Enqueue((x, y));

                    while (queue.Count > 0)
                    {
                        var (qx, qy) = queue.Dequeue();
                        size++;

                        if (qx + 1 < W && h[qx + 1, qy] && piece[qx + 1, qy] == 0) { piece[qx + 1, qy] = label; queue.Enqueue((qx + 1, qy)); }
                        if (qx > 0 && h[qx - 1, qy] && piece[qx - 1, qy] == 0) { piece[qx - 1, qy] = label; queue.Enqueue((qx - 1, qy)); }
                        if (qy + 1 < H && h[qx, qy + 1] && piece[qx, qy + 1] == 0) { piece[qx, qy + 1] = label; queue.Enqueue((qx, qy + 1)); }
                        if (qy > 0 && h[qx, qy - 1] && piece[qx, qy - 1] == 0) { piece[qx, qy - 1] = label; queue.Enqueue((qx, qy - 1)); }
                    }

                    if (size > bestSize)
                    {
                        bestSize = size;
                        best = label;
                    }
                }
            }

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    h[x, y] = best != 0 && piece[x, y] == best;

            return bestSize;
        }

        /// <summary>
        /// The dry floor stepped: each column's floor above the lake cut down to the next of a set of levels three rows
        /// apart (counted up from the lake), so that the dry floor is flat runs and low steps.
        /// </summary>
        private static void Terraces(Pocket p, bool[,] h, Func<int, int, bool> clear)
        {
            const int Step = 3;

            for (int x = 1; x < p.Wide - 1; x++)
            {
                int bottom = -1;

                for (int y = p.High - 2; y > 0; y--)
                {
                    if (h[x, y])
                    {
                        bottom = y;
                        break;
                    }
                }

                if (bottom < 0 || bottom >= p.Level - 1)
                    continue;

                int below = p.Level - 1 - bottom;
                int cut = p.Level - 1 - below / Step * Step;

                for (int y = bottom + 1; y <= cut; y++)
                {
                    if (!clear(x, y) || y >= p.High - ShellDepth - 2)
                        break;

                    h[x, y] = true;
                }
            }
        }

        private static void Bounds(Pocket p, bool[,] h)
        {
            p.Left = p.Wide;
            p.Top = p.High;
            p.Right = p.Bottom = -1;

            for (int y = 0; y < p.High; y++)
            {
                for (int x = 0; x < p.Wide; x++)
                {
                    if (!h[x, y])
                        continue;

                    p.Left = Math.Min(p.Left, x);
                    p.Right = Math.Max(p.Right, x);
                    p.Top = Math.Min(p.Top, y);
                    p.Bottom = Math.Max(p.Bottom, y);
                }
            }
        }

        /// <summary>How far each cell is from the marked ones, in tiles (0 on them): a two-pass chamfer.</summary>
        private static float[,] Distance(bool[,] from)
        {
            const float Far = 1e6f, Side = 1f, Corner = 1.414f;
            int W = from.GetLength(0), H = from.GetLength(1);
            float[,] d = new float[W, H];

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    d[x, y] = from[x, y] ? 0f : Far;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (x > 0) d[x, y] = Math.Min(d[x, y], d[x - 1, y] + Side);
                    if (y > 0) d[x, y] = Math.Min(d[x, y], d[x, y - 1] + Side);
                    if (x > 0 && y > 0) d[x, y] = Math.Min(d[x, y], d[x - 1, y - 1] + Corner);
                    if (x < W - 1 && y > 0) d[x, y] = Math.Min(d[x, y], d[x + 1, y - 1] + Corner);
                }
            }

            for (int y = H - 1; y >= 0; y--)
            {
                for (int x = W - 1; x >= 0; x--)
                {
                    if (x < W - 1) d[x, y] = Math.Min(d[x, y], d[x + 1, y] + Side);
                    if (y < H - 1) d[x, y] = Math.Min(d[x, y], d[x, y + 1] + Side);
                    if (x < W - 1 && y < H - 1) d[x, y] = Math.Min(d[x, y], d[x + 1, y + 1] + Corner);
                    if (x > 0 && y < H - 1) d[x, y] = Math.Min(d[x, y], d[x - 1, y + 1] + Corner);
                }
            }

            return d;
        }

        // ---- the ways in -----------------------------------------------------------------------------------------------

        /// <summary>From each end of the cavern, out and up; and one up from the ceiling, somewhere along its middle half.</summary>
        private static void Tunnels(Pocket p, bool[,] hollow, float[,] far, Ground[,] g, Random rng)
        {
            foreach (int side in new[] { -1, 1 })
            {
                // The end of the cavern on this side, high up in it.
                int sx = -1, sy = -1;
                float best = float.MinValue;

                for (int y = 0; y < p.Level - 4; y++)
                {
                    for (int x = 0; x < p.Wide; x++)
                    {
                        if (!hollow[x, y])
                            continue;

                        float score = side * x - 0.5f * y;

                        if (score > best)
                        {
                            best = score;
                            sx = x;
                            sy = y;
                        }
                    }
                }

                if (sx >= 0)
                    Tunnel(p, far, g, rng, sx, sy, side, -0.55f);
                else
                    p.Notes.Add("no way in from one end: nowhere to start it");
            }

            // Up from the ceiling: somewhere in the middle half where it is open under it for ten rows.
            int cx = -1, cy = -1;

            for (int tries = 0; tries < 40 && cx < 0; tries++)
            {
                int x = p.Left + (p.Right - p.Left) / 4 + rng.Next(Math.Max(1, (p.Right - p.Left) / 2));

                for (int y = 1; y < p.Level - 12; y++)
                {
                    if (!hollow[x, y])
                        continue;

                    bool room = true;

                    for (int k = 0; k < 10 && room; k++)
                        room = hollow[x, y + k];

                    if (room)
                    {
                        cx = x;
                        cy = y;
                    }

                    break;
                }
            }

            if (cx >= 0)
                Tunnel(p, far, g, rng, cx, cy, rng.Next(2) == 0 ? -0.35f : 0.35f, -1f);
            else
                p.Notes.Add("no way in from above: no ceiling to start it");
        }

        /// <summary>One way in: wandering, rising, five cells across, until it comes out into open ground beyond the shell.</summary>
        private static void Tunnel(Pocket p, float[,] far, Ground[,] g, Random rng, int sx, int sy, float ax, float ay)
        {
            float length = (float)Math.Sqrt(ax * ax + ay * ay);
            ax /= length;
            ay /= length;
            float px = sx, py = sy;
            int lastX = sx, lastY = sy;

            for (int step = 0; step < 200; step++)
            {
                float turn = (float)(rng.NextDouble() - 0.5) * 0.45f;
                float nx = ax + turn * -ay, ny = ay + turn * ax;
                float len = (float)Math.Sqrt(nx * nx + ny * ny);
                ax = nx / len;
                ay = Math.Min(-0.25f, ny / len);
                px += ax;
                py += ay;

                int cx = (int)Math.Round(px), cy = (int)Math.Round(py);

                if (cx < 3 || cy < 3 || cx >= p.Wide - 3 || cy >= p.High - 3)
                {
                    p.Ends.Add((lastX, lastY, ax, ay));
                    p.Notes.Add("a way in ran to the canvas's edge (dug on in the world)");
                    return;
                }

                bool keptNear = false;

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (dx * dx + dy * dy > 5)
                            continue;

                        int x = cx + dx, y = cy + dy;

                        if (g[x, y] == Ground.Kept)
                        {
                            keptNear = true;
                            continue;
                        }

                        if (y >= p.Level - 1 || p.Cells[x, y] == Cell.Sludge)
                            continue;

                        p.Cells[x, y] = Cell.Open;
                    }
                }

                if (keptNear)
                {
                    p.Notes.Add("a way in stopped against something kept");
                    return;
                }

                if (far[cx, cy] > ShellDepth + 1 && g[cx, cy] == Ground.Air)
                    return;

                lastX = cx;
                lastY = cy;
            }

            p.Ends.Add((lastX, lastY, ax, ay));
            p.Notes.Add("a way in found no open ground on the canvas (dug on in the world)");
        }

        // ---- the works -------------------------------------------------------------------------------------------------

        private static bool Solid(Cell c) => c == Cell.Mud || c == Cell.Cobble;

        /// <summary>Whether the lake is straight under a cell, with nothing but open air between.</summary>
        private static bool LakeUnder(Pocket p, int x, int y)
        {
            for (int below = y + 1; below < p.High; below++)
            {
                Cell c = p.Cells[x, below];

                if (c == Cell.Sludge)
                    return true;

                if (c != Cell.Open)
                    return false;
            }

            return false;
        }

        private static bool Free(Pocket p, int x, int y, int w, int h, bool wet)
        {
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    int cx = x + dx, cy = y + dy;

                    if (cx < 0 || cy < 0 || cx >= p.Wide || cy >= p.High || p.Taken[cx, cy])
                        return false;

                    Cell c = p.Cells[cx, cy];

                    if (c != Cell.Open && !(wet && c == Cell.Sludge))
                        return false;
                }
            }

            return true;
        }

        /// <summary>Solid under every cell of a row, from x for w.</summary>
        private static bool Floor(Pocket p, int x, int y, int w)
        {
            if (y + 1 >= p.High)
                return false;

            for (int dx = 0; dx < w; dx++)
                if (x + dx < 0 || x + dx >= p.Wide || !Solid(p.Cells[x + dx, y + 1]))
                    return false;

            return true;
        }

        private static void Add(Pocket p, Thing t)
        {
            p.Things.Add(t);

            for (int dy = 0; dy < t.Tall; dy++)
                for (int dx = 0; dx < t.Wide; dx++)
                    p.Taken[t.X + dx, t.Y + dy] = true;
        }

        private static void Mark(Pocket p, int x, int y, int w, int h)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    if (x + dx >= 0 && y + dy >= 0 && x + dx < p.Wide && y + dy < p.High)
                        p.Taken[x + dx, y + dy] = true;
        }

        /// <summary>Which way along a row of floor (standing at y) the lake is nearer.</summary>
        private static int TowardLake(Pocket p, int x, int y)
        {
            for (int d = 1; d < p.Wide; d++)
            {
                bool left = x - d >= 0 && LakeUnder(p, x - d, y), right = x + d < p.Wide && LakeUnder(p, x + d, y);

                if (left || right)
                    return left && !right ? -1 : 1;
            }

            return x < p.Wide / 2 ? 1 : -1;
        }

        /// <summary>Every place on the dry floor two wide and two high that is free (top left cells).</summary>
        private static List<(int X, int Y)> Spots(Pocket p)
        {
            List<(int X, int Y)> spots = new();

            for (int y = 2; y < p.Level; y++)
                for (int x = 1; x < p.Wide - 2; x++)
                    if (Floor(p, x, y, 2) && Free(p, x, y - 1, 2, 2, false))
                        spots.Add((x, y - 1));

            return spots;
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void Works(Pocket p, Random rng, int cavernWide)
        {
            // Vats: spread along the dry floor, each with headroom, and room on the floor toward the lake for its outlet.
            int vats = Math.Max(2, cavernWide / 170);
            List<(int X, int Y, int Way)> vatSpots = new();

            for (int y = 2; y < p.Level; y++)
            {
                for (int x = 5; x < p.Wide - 9; x++)
                {
                    if (!Floor(p, x, y, 4) || !Free(p, x, y - 5, 4, 6, false))
                        continue;

                    int way = TowardLake(p, x + 2, y);
                    int run = way > 0 ? x + 4 : x - 4;

                    if (Floor(p, run, y, 4) && Free(p, run, y, 4, 1, false))
                        vatSpots.Add((x, y, way));
                }
            }

            Shuffle(vatSpots, rng);
            List<int> vatXs = new();
            int apart = cavernWide / (vats + 1) * 2 / 3;

            foreach (var (x, y, way) in vatSpots)
            {
                if (vatXs.Count >= vats)
                    break;

                if (vatXs.Exists(v => Math.Abs(v - x) < apart) || !Free(p, x, y - 3, 4, 4, false))
                    continue;

                int style = way > 0 ? 0 : 1;
                Add(p, new Thing(Kind.Vat, x, y - 3, style));
                Inlet(p, x + (style == 0 ? 1 : 2), y - 4, false);
                Outlet(p, way > 0 ? x + 4 : x - 1, y, way);
                Mark(p, x - 2, y - 3, 8, 4);
                vatXs.Add(x);

                if (p.Inside.X < 0)
                    p.Inside = (way > 0 ? x - 2 : x + 5, y);
            }

            if (vatXs.Count < vats)
                p.Notes.Add($"only {vatXs.Count} of {vats} vats found room");

            // Barrels, in clusters about the dry floor.
            List<(int X, int Y)> spots = Spots(p);
            Shuffle(spots, rng);
            int clusters = Math.Max(3, cavernWide / 55), placed = 0, fed = 0;
            Barrel[] order = { Barrel.Upright, Barrel.Toppled, Barrel.Upright, Barrel.Dented, Barrel.Crushed, Barrel.Upright, Barrel.Dented };

            for (int c = 0; c < clusters && c < spots.Count; c++)
            {
                var centre = spots[c];
                List<(int X, int Y)> near = spots.FindAll(s => Math.Abs(s.X - centre.X) <= 12 && Math.Abs(s.Y - centre.Y) <= 4);
                near.Sort((a, b) => Math.Abs(a.X - centre.X).CompareTo(Math.Abs(b.X - centre.X)));
                int inCluster = 0, most = 2 + rng.Next(4);

                foreach (var (x, y) in near)
                {
                    if (inCluster >= most)
                        break;

                    if (!Free(p, x, y, 2, 2, false))
                        continue;

                    Barrel style = order[(placed + c) % order.Length];
                    Add(p, new Thing(Kind.Barrel, x, y, (int)style));
                    Mark(p, x - 1, y, 4, 2);
                    inCluster++;
                    placed++;

                    // Now and then a pipe from the rock overhead, open over an upright one.
                    if (style == Barrel.Upright && fed < vats && rng.Next(2) == 0 && Inlet(p, x + 1, y - 1, true))
                        fed++;
                }
            }

            if (placed < clusters * 2)
                p.Notes.Add($"only {placed} barrels found room");

            // Some standing in the shallows, their tops out of it.
            int sunk = 0, sunkMost = Math.Max(2, cavernWide / 90);

            for (int x = 1; x < p.Wide - 2 && sunk < sunkMost; x++)
            {
                for (int y = p.Level; y <= p.Level + 1; y++)
                {
                    if (p.Cells[x, y] == Cell.Sludge && p.Cells[x + 1, y] == Cell.Sludge && Floor(p, x, y, 2) && Free(p, x, y - 1, 2, 2, true) && rng.Next(3) == 0)
                    {
                        Add(p, new Thing(Kind.Barrel, x, y - 1, (int)(sunk % 2 == 0 ? Barrel.Upright : Barrel.Dented)));
                        Mark(p, x - 2, y - 1, 6, 2);
                        sunk++;
                        x += 6;
                        break;
                    }
                }
            }

            // A few still shut, holding something.
            int chests = Math.Max(1, cavernWide / 220), shut = 0;
            List<(int X, int Y)> left = Spots(p);
            Shuffle(left, rng);

            foreach (var (x, y) in left)
            {
                if (shut >= chests)
                    break;

                if (!Free(p, x, y, 2, 2, false))
                    continue;

                Add(p, new Thing(Kind.Chest, x, y, 0));
                Mark(p, x - 1, y, 4, 2);
                shut++;
            }

            if (shut < chests)
                p.Notes.Add($"only {shut} of {chests} chests found room");

            if (p.Inside.X < 0 && spots.Count > 0)
                p.Inside = (spots[0].X, spots[0].Y + 1);
        }

        /// <summary>
        /// A pipe down from the rock overhead (where the pumps were), on up into the rock four cells: into a vat's top,
        /// or with <paramref name="mouth"/>, open over a barrel. If the rock is more than forty cells up, it hangs from a
        /// joint where the rest of it was taken away (not over barrels: those only where the rock is in reach). False if
        /// there was no room for it.
        /// </summary>
        private static bool Inlet(Pocket p, int x, int bottom, bool mouth)
        {
            int y = bottom, run = 0;

            while (y > 1 && run < 40 && p.Cells[x, y] == Cell.Open && !p.Taken[x, y])
            {
                y--;
                run++;
            }

            bool rock = y > 1 && Solid(p.Cells[x, y]);

            if (run == 0 || mouth && !rock)
                return false;

            for (int k = 0; k < run; k++)
            {
                Pipe piece = k == 0 && mouth ? Pipe.MouthDown : k == run - 1 && !rock ? Pipe.JointDown : k % 4 == 3 ? Pipe.JointDown : Pipe.Down;
                Add(p, new Thing(Kind.Pipe, x, bottom - k, (int)piece));
            }

            // Into the rock over it: the pipe's slot cut through the shell.
            for (int k = 0; k < 4 && rock && y > 1 && Solid(p.Cells[x, y]); k++, y--)
            {
                p.Cells[x, y] = Cell.Open;
                Add(p, new Thing(Kind.Pipe, x, y, (int)Pipe.Down));
            }

            return true;
        }

        /// <summary>
        /// A run from a vat's outlet along the floor, a valve and joints on it, to the floor's edge, and on out over the
        /// drop until the lake is under it, where its mouth is; split if it ends against something first.
        /// </summary>
        private static void Outlet(Pocket p, int x, int y, int way)
        {
            List<int> run = new();
            bool edge = false;
            int overhang = 0;

            for (int k = 0; k < 60; k++, x += way)
            {
                if (x < 1 || x >= p.Wide - 1 || p.Cells[x, y] != Cell.Open || p.Taken[x, y])
                    break;

                run.Add(x);

                if (Solid(p.Cells[x, y + 1]))
                    continue;

                overhang++;

                if (LakeUnder(p, x, y) || overhang >= 8)
                {
                    edge = true;
                    break;
                }
            }

            if (run.Count < 2)
            {
                p.Notes.Add("a vat's outlet had no room");
                return;
            }

            for (int i = 0; i < run.Count; i++)
            {
                Pipe piece = i == run.Count - 1
                    ? edge ? way > 0 ? Pipe.MouthRight : Pipe.MouthLeft : Pipe.Broken
                    : i == 1 ? Pipe.Valve
                    : i % 4 == 3 ? Pipe.JointAcross
                    : Pipe.Across;

                Add(p, new Thing(Kind.Pipe, run[i], y, (int)piece));
            }
        }

        /// <summary>
        /// What hangs from the ceiling: points of Acidic Mud, one or two cells wide at the root, longer in a higher
        /// cavern; never onto anything set there, never into a way in, always with room to walk under them.
        /// </summary>
        private static void Drips(Pocket p, bool[,] hollow, Random rng, int seed, int cavernHigh)
        {
            int longest = 3 + cavernHigh / 22;

            for (int x = 2; x < p.Wide - 2; x++)
            {
                if (Noise(x * 0.5f, 3.7f, seed + 5) < 0.6f || rng.Next(3) == 0)
                    continue;

                int y = 1;

                while (y < p.Level - 6 && !(p.Cells[x, y] == Cell.Open && Solid(p.Cells[x, y - 1])))
                    y++;

                if (y >= p.Level - 6 || !hollow[x, y])
                    continue;

                int length = 1 + rng.Next(longest);

                for (int k = 0; k < length; k++)
                {
                    int cy = y + k;
                    bool clear = p.Cells[x, cy] == Cell.Open && !p.Taken[x, cy];

                    for (int below = 1; below <= 5 && clear; below++)
                        clear = cy + below < p.High && p.Cells[x, cy + below] == Cell.Open && !p.Taken[x, cy + below];

                    if (!clear)
                        break;

                    p.Cells[x, cy] = Cell.Mud;

                    // Wider at the root, for the longer ones.
                    if (k < length / 3 && p.Cells[x + 1, cy] == Cell.Open && !p.Taken[x + 1, cy])
                        p.Cells[x + 1, cy] = Cell.Mud;
                }
            }
        }

        /// <summary>The floor left free: for chests the world had where the cavern now is (SludgePocket moves them here). Nearest the middle first.</summary>
        private static void Leftover(Pocket p)
        {
            List<(int X, int Y)> spots = Spots(p);
            int middle = p.Wide / 2;
            spots.Sort((a, b) => Math.Abs(a.X - middle).CompareTo(Math.Abs(b.X - middle)));

            foreach (var (x, y) in spots)
            {
                if (!Free(p, x, y, 2, 2, false))
                    continue;

                p.Spare.Add((x, y));
                Mark(p, x - 2, y, 6, 2);
            }
        }
    }
}
