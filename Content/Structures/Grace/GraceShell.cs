using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The deep walled in. From the stair under the lowest street down (the stair, the Long
    /// Reliquary, the crack under its Crypt, the Font chamber and the Shimmer's room), every
    /// cell of the ground within eight of any of it is laid in grey brick washed white. The
    /// crack keeps its shape: rock the crack was cut through becomes brick cut the same way,
    /// so its floors still walk as they did. Nothing the builders made is touched, and the
    /// older tunnel into the Shimmer's room still comes through, so the only ways into the
    /// deep are the ones it was given.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>How thick the brick round the deep is.</summary>
        private const int DeepShellThick = 8;

        /// <summary>The blocks of the deep, first column and row to last, as they are built.</summary>
        private readonly List<(int x0, int y0, int x1, int y1)> deepParts = new List<(int, int, int, int)>();

        /// <summary>The rectangle the crack was cut through, once it has been.</summary>
        private (int x0, int y0, int x1, int y1)? fissureBox;

        private void DeepShell()
        {
            if (deepParts.Count == 0)
                return;

            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            var boxes = new List<(int x0, int y0, int x1, int y1)>(deepParts);
            if (fissureBox.HasValue)
                boxes.Add(fissureBox.Value);

            foreach (var b in boxes)
            {
                x0 = Math.Min(x0, b.x0);
                y0 = Math.Min(y0, b.y0);
                x1 = Math.Max(x1, b.x1);
                y1 = Math.Max(y1, b.y1);
            }

            int m = DeepShellThick + 1;
            x0 -= m;
            y0 -= m;
            x1 += m;
            y1 += m;

            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var deep = new bool[w * h];

            // The blocks themselves, whole.
            foreach (var p in deepParts)
                for (int x = p.x0; x <= p.x1; x++)
                    for (int y = p.y0; y <= p.y1; y++)
                        deep[(y - y0) * w + (x - x0)] = true;

            // The crack: the way cut open, and what was put in it (ropes, bones, the tomb's
            // stones). The rock of its floors and walls is not the crack: that is what goes to
            // brick.
            if (fissureBox.HasValue)
            {
                var f = fissureBox.Value;
                for (int x = f.x0; x <= f.x1; x++)
                {
                    for (int y = f.y0; y <= f.y1; y++)
                    {
                        if (!c.Built(x, y))
                            continue;

                        int t = c.TypeAt(x, y);
                        if (t < 0 || c.Laid(x, y) && t != TileID.Stone)
                            deep[(y - y0) * w + (x - x0)] = true;
                    }
                }
            }

            bool[] near = Dilate(deep, w, h, DeepShellThick);

            // Ground shut in by the brick on every side (between the turns of the crack, and
            // where it meets the Reliquary) goes to brick with it, so the deep stands in one
            // solid mass rather than round islands of raw rock.
            bool[] outside = Outside(deep, near, w, h);
            for (int i = 0; i < near.Length; i++)
                if (!deep[i] && !outside[i])
                    near[i] = true;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    int i = (y - y0) * w + (x - x0);
                    if (!near[i] || deep[i] || !c.InBounds(x, y))
                        continue;

                    int t = c.TypeAt(x, y);
                    if (t < 0)
                    {
                        // Open ground: a cave the deep came near. Only what the world left open.
                        if (!c.Built(x, y))
                            c.Block(x, y, TileID.GrayBrick, White);
                    }
                    else if (!c.Laid(x, y) || t == TileID.Stone)
                    {
                        // Rock, as the world made it or as the crack left it: brick, cut the same.
                        c.Block(x, y, TileID.GrayBrick, White, c.CutAt(x, y));
                    }
                }
            }
        }

        /// <summary>
        /// The cells beyond the brick that can be reached from the edge of the area without
        /// crossing the deep or the brick round it, a step at a time up, down or across.
        /// </summary>
        private static bool[] Outside(bool[] deep, bool[] near, int w, int h)
        {
            var seen = new bool[w * h];
            var open = new Stack<int>();

            void Visit(int i)
            {
                if (!seen[i] && !deep[i] && !near[i])
                {
                    seen[i] = true;
                    open.Push(i);
                }
            }

            for (int x = 0; x < w; x++)
            {
                Visit(x);
                Visit((h - 1) * w + x);
            }

            for (int y = 0; y < h; y++)
            {
                Visit(y * w);
                Visit(y * w + w - 1);
            }

            while (open.Count > 0)
            {
                int i = open.Pop();
                int x = i % w, y = i / w;
                if (x > 0) Visit(i - 1);
                if (x < w - 1) Visit(i + 1);
                if (y > 0) Visit(i - w);
                if (y < h - 1) Visit(i + w);
            }

            return seen;
        }

        /// <summary>Every cell within <paramref name="r"/> of a true one, counting diagonals as one step.</summary>
        private static bool[] Dilate(bool[] src, int w, int h, int r)
        {
            const int Far = int.MaxValue / 2;
            var rows = new bool[w * h];

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                int since = Far;
                for (int x = 0; x < w; x++)
                {
                    since = src[row + x] ? 0 : since + 1;
                    if (since <= r)
                        rows[row + x] = true;
                }

                since = Far;
                for (int x = w - 1; x >= 0; x--)
                {
                    since = src[row + x] ? 0 : since + 1;
                    if (since <= r)
                        rows[row + x] = true;
                }
            }

            var dst = new bool[w * h];
            for (int x = 0; x < w; x++)
            {
                int since = Far;
                for (int y = 0; y < h; y++)
                {
                    since = rows[y * w + x] ? 0 : since + 1;
                    if (since <= r)
                        dst[y * w + x] = true;
                }

                since = Far;
                for (int y = h - 1; y >= 0; y--)
                {
                    since = rows[y * w + x] ? 0 : since + 1;
                    if (since <= r)
                        dst[y * w + x] = true;
                }
            }

            return dst;
        }
    }
}
