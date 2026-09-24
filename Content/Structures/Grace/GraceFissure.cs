using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The way to the Font chamber: a crack in the rock under the Long Reliquary. In the
    /// Crypt one tomb has gone through the floor, and under it the crack winds down. Each run
    /// falls gently one way and ends at a sheer drop, and the next run comes back under the
    /// last, the whole of it turned like a stair in the rock, until it comes out level at the
    /// chamber's one door some five hundred rows down.
    ///
    /// Nothing in it was built. The floors are the rock's own, the walls behind are rough
    /// stone with a darker line of crack down the middle of the way, veined blue under the
    /// Reliquary and purple nearer the Shimmer. Bones and the tomb's stones lie where they
    /// fell. The short drops have ledges in them to climb back up by, the long ones a rope.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>
        /// How wide a drop is: a ledge two wide out of either side and two open columns
        /// between them, so a player can jump straight up the middle and step off onto either.
        /// </summary>
        private const int FissureDropW = 6;

        /// <summary>The fewest columns in a run.</summary>
        private const int FissureMinRun = 16;

        /// <summary>The highest a run's ceiling stands over its floor, and the least.</summary>
        private const int FissureMaxH = 7, FissureMinH = 4;

        /// <summary>
        /// The shortest drop: enough that at the turn the run above keeps a floor and a row of
        /// rock under it over the tallest ceiling of the run below.
        /// </summary>
        private const int FissureMinDrop = FissureMaxH + 2;

        /// <summary>The tallest drop, and the shortest that has a rope in it rather than ledges.</summary>
        private const int FissureMaxDrop = 26, FissureRopeDrop = 13;

        /// <summary>The most rows a player is asked to jump up, from one foothold to the next.</summary>
        private const int FissureReach = 5;

        /// <summary>One run of the crack, from the foot of one drop to the lip of the next.</summary>
        private sealed class FissureRun
        {
            /// <summary>First and last column, in the order walked, and which way that is.</summary>
            public int X0, X1, Dir;

            /// <summary>The floor's row at <see cref="X0"/>, and how far it falls by <see cref="X1"/>.</summary>
            public int Floor0, Descent;

            /// <summary>Level columns at the start (the landing under a drop) and at the end.</summary>
            public int Flat0, Flat1;

            /// <summary>The fall past <see cref="X1"/> to the next run's floor; 0 on the last run.</summary>
            public int Drop;

            public int Length => Math.Abs(X1 - X0);
            public int Floor1 => Floor0 + Descent;
        }

        // ------------------------------------------------------------ the tomb

        /// <summary>
        /// Picks the tomb in the Crypt that went through the floor, breaks it open, and cuts the
        /// crack from under it down to the Font chamber's door. <paramref name="g0"/> and
        /// <paramref name="g1"/> are the Crypt's first and last columns and
        /// <paramref name="floor"/> its floor row.
        /// </summary>
        private void FallenTomb(int g0, int g1, int floor)
        {
            int bays = 0;
            for (int bx = g0; bx + ReliquaryBay - 1 <= g1; bx += ReliquaryBay)
                bays++;
            if (bays < 9)
                return;

            // The bay some hundred and eighty columns short of the chamber's door, clear of the
            // grave with one date on it and of the stairs at the Crypt's two ends.
            int endX = ArenaDoorX;
            int k = (int)Math.Round((endX - 180 - g0 - 12) / (double)ReliquaryBay);
            k = Math.Clamp(k, 5, bays - 3);

            // The bay's second tomb, as FurnishRelic lays it: six wide, its headstone after it.
            int t0 = g0 + k * ReliquaryBay + 13, t1 = t0 + 5;

            // Its plinth and lid gone down the hole, and the floor under it; the headstone stands
            // at the east edge, and the west edge of the break is worn round.
            for (int x = t0; x <= t1; x++)
                for (int y = floor - 3; y <= floor; y++)
                    c.Erase(x, y);
            c.Reshape(t0 - 1, floor, Grace.Cut.DownRight);

            c.Mark("Grace.FallenTomb", t0, floor - 3, t1 - t0 + 1, 4);
            Fissure(t0, t1, floor, floor + c.Next(17, 22), endX, ArenaFloor);
        }

        // ------------------------------------------------------------ the crack

        /// <summary>
        /// The crack from the hole at columns <paramref name="hc0"/> to <paramref name="hc1"/>
        /// in a floor at row <paramref name="top"/>: first a straight fall to a ledge at
        /// <paramref name="firstFloor"/>, then the runs and drops, coming out level at
        /// (<paramref name="endX"/>, <paramref name="endFloor"/>) heading east.
        /// </summary>
        private void Fissure(int hc0, int hc1, int top, int firstFloor, int endX, int endFloor)
        {
            int seed = c.Next(1, 1 << 20);
            int dressed = top + 6;            // the last row of the Reliquary's stone under the Crypt
            List<FissureRun> runs = PlanFissure(hc0, hc1, firstFloor, endX, endFloor);

            // First the open way, all of it, top to bottom: the fall out of the tomb, through the
            // Reliquary's floor and on down, then each run and the drop at its end.
            double half = (hc1 - hc0) / 2.0;
            for (int y = top; y < firstFloor; y++)
                for (int x = hc0; x <= hc1; x++)
                    OpenCrack(x, y, Math.Abs(x - (hc0 + hc1) / 2.0) / half, seed, top, endFloor, dressed);

            for (int i = 0; i < runs.Count; i++)
            {
                CarveRun(runs[i], i, seed, top, endFloor, dressed);
                if (i + 1 < runs.Count)
                    CarveDrop(runs[i], i, runs[i + 1], seed, top, endFloor, dressed);
            }

            // Then what is in it, so that no later cut takes it away: the footholds, the ropes,
            // what fell. Nothing solid lies on a landing, so nothing stands a player up into a
            // ledge over it.
            Ledges(hc0, hc1, top, firstFloor, false, dressed);
            c.Place((hc0 + hc1) / 2 - 1, firstFloor - 1, TileID.LargePiles, c.Next(0, 6));

            for (int i = 0; i + 1 < runs.Count; i++)
                FitDrop(runs[i], i, runs[i + 1], seed, dressed);

            int minX = Math.Min(hc0, endX), maxX = Math.Max(hc1, endX);
            foreach (FissureRun r in runs)
            {
                minX = Math.Min(minX, Math.Min(r.X0, r.X1) - FissureDropW);
                maxX = Math.Max(maxX, Math.Max(r.X0, r.X1) + FissureDropW);
            }
            c.Mark("Grace.Fissure", minX, top, maxX - minX + 1, endFloor - top + 1);
            fissureBox = (minX, top, maxX, endFloor + 1);
        }

        /// <summary>
        /// Lays the runs out: how many, where each turns and how far it falls, so that the last
        /// comes out at the door on its floor. The swing is centred on the hole at the top and
        /// drifts to the door at the bottom; every run heading east turns short of the chamber's
        /// wall. Whatever the whole number of runs leaves over is taken up in the drops, then in
        /// the runs.
        /// </summary>
        private List<FissureRun> PlanFissure(int hc0, int hc1, int firstFloor, int endX, int endFloor)
        {
            const int most = 64;
            var swing = new int[most];
            var pitch = new double[most];
            var drop = new int[most];
            for (int i = 0; i < most; i++)
            {
                swing[i] = c.Next(22, 31);
                pitch[i] = 3.2 + c.NextDouble() * 1.3;
                drop[i] = c.OneIn(4) ? c.Next(18, 25) : c.Next(FissureMinDrop, 17);
            }

            List<FissureRun> best = null;
            int bestMiss = int.MaxValue;
            for (int n = 3; n <= most; n++)
            {
                List<FissureRun> runs = LayRuns(n, hc0, hc1, firstFloor, endX, swing, pitch, drop);
                int end = runs[n - 1].Floor1;
                int miss = Math.Abs(endFloor - end);
                if (miss < bestMiss)
                {
                    bestMiss = miss;
                    best = runs;
                }
                if (end > endFloor + 60)
                    break;
            }

            int left = endFloor - best[best.Count - 1].Floor1;
            for (int guard = 0; left != 0 && guard < 10000; guard++)
            {
                bool moved = false;
                for (int i = 0; i + 1 < best.Count && left != 0; i++)
                {
                    FissureRun r = best[i];
                    if (left > 0 && r.Drop < FissureMaxDrop) { r.Drop++; left--; moved = true; }
                    else if (left < 0 && r.Drop > FissureMinDrop) { r.Drop--; left++; moved = true; }
                }

                for (int i = 0; !moved && i < best.Count && left != 0; i++)
                {
                    FissureRun r = best[i];
                    int slope = r.Length - r.Flat0 - r.Flat1;
                    if (left > 0 && r.Descent < slope * 2 / 5) { r.Descent++; left--; moved = true; }
                    else if (left < 0 && r.Descent > 0) { r.Descent--; left++; moved = true; }
                }

                if (!moved)
                    break;
            }

            for (int i = 1; i < best.Count; i++)
                best[i].Floor0 = best[i - 1].Floor1 + best[i - 1].Drop;

            return best;
        }

        /// <summary>One way of laying <paramref name="n"/> runs, the last heading east to the door.</summary>
        private List<FissureRun> LayRuns(int n, int hc0, int hc1, int firstFloor, int endX, int[] swing, double[] pitch, int[] drop)
        {
            var runs = new List<FissureRun>(n);
            int eastmost = endX - FissureDropW - 3;
            double centreTop = (hc0 + hc1) / 2.0, centreFoot = endX - swing[n - 1];
            int floor = firstFloor;
            int x = 0;

            for (int i = 0; i < n; i++)
            {
                int dir = (n - 1 - i) % 2 == 0 ? 1 : -1;
                bool last = i == n - 1;
                int x0 = i == 0 ? (dir > 0 ? hc0 : hc1) : x;

                int x1;
                if (last)
                {
                    x1 = endX;
                }
                else
                {
                    double centre = centreTop + (centreFoot - centreTop) * (i / (double)(n - 1));
                    x1 = (int)Math.Round(centre + dir * swing[i]);
                    if (dir > 0)
                        x1 = Math.Min(x1, eastmost);
                    if ((x1 - x0) * dir < FissureMinRun)
                        x1 = x0 + dir * FissureMinRun;
                }

                var r = new FissureRun
                {
                    X0 = x0,
                    X1 = x1,
                    Dir = dir,
                    Floor0 = floor,
                    Flat0 = i == 0 ? FissureDropW : FissureDropW + 1,
                    Flat1 = last ? 6 : 1,
                    Drop = last ? 0 : drop[i],
                };
                r.Descent = (int)Math.Round(Math.Max(0, r.Length - r.Flat0 - r.Flat1) / pitch[i]);
                runs.Add(r);

                floor = r.Floor1 + r.Drop;
                x = x1 + dir * FissureDropW;
            }

            return runs;
        }

        /// <summary>
        /// The floor's row <paramref name="k"/> columns into a run: level over the landing, then
        /// falling, a little steeper near both ends than in the middle the way a stair turned
        /// round a newel is, then level again.
        /// </summary>
        private static int FissureFloor(FissureRun r, int k)
        {
            int slope = r.Length - r.Flat0 - r.Flat1;
            if (k <= r.Flat0 || slope <= 0)
                return r.Floor0;
            if (k >= r.Length - r.Flat1)
                return r.Floor1;

            double u = (k - r.Flat0) / (double)slope;
            double g = u + 0.6 / (2 * Math.PI) * Math.Sin(2 * Math.PI * u);
            return r.Floor0 + (int)Math.Round(r.Descent * g);
        }

        /// <summary>How high the crack stands over its floor at column <paramref name="x"/> of run <paramref name="i"/>.</summary>
        private static int FissureHeight(FissureRun r, int i, int x, int k, int seed)
        {
            int h = FissureMinH + (int)(Noise(x, i * 97, 5.0, seed + 11) * (FissureMaxH - FissureMinH + 1));
            if (k <= r.Flat0)
                h = Math.Max(h, 6);
            return Math.Min(FissureMaxH, h);
        }

        /// <summary>
        /// Cuts one run: the way open over its floor, the floor made whole where the rock under
        /// it was hollow, the edges of its steps taken off so it walks as a slope.
        /// </summary>
        private void CarveRun(FissureRun r, int i, int seed, int top, int bottom, int dressed)
        {
            int n = r.Length;
            for (int k = 0; k <= n; k++)
            {
                int x = r.X0 + r.Dir * k;
                int fy = FissureFloor(r, k);
                int h = FissureHeight(r, i, x, k, seed);

                // The crack's line wanders a little up and down the middle of the way.
                double line = fy - h / 2.0 + (Noise(x, i * 31, 9.0, seed + 5) - 0.5) * 2.0;
                for (int y = fy - h; y < fy; y++)
                    OpenCrack(x, y, Math.Abs(y - line) / (h / 2.0), seed, top, bottom, dressed);

                for (int y = fy; y <= fy + 1; y++)
                {
                    if (!c.Solid(x, y))
                    {
                        c.Erase(x, y);
                        c.Block(x, y, TileID.Stone);
                    }
                }

                if (k < n && FissureFloor(r, k + 1) > fy)
                    c.Reshape(x, fy, r.Dir > 0 ? Grace.Cut.DownRight : Grace.Cut.DownLeft);
                else
                    c.Reshape(x, fy, Grace.Cut.Full);
            }

        }

        /// <summary>The columns a drop falls through at the end of <paramref name="up"/>, west to east.</summary>
        private static (int lo, int hi) DropColumns(FissureRun up)
        {
            int a = up.X1 + up.Dir, b = up.X1 + up.Dir * FissureDropW;
            return (Math.Min(a, b), Math.Max(a, b));
        }

        /// <summary>The row over a drop: the ceiling of the run it falls from, at its lip.</summary>
        private static int DropRoof(FissureRun up, int i, int seed) => up.Floor1 - FissureHeight(up, i, up.X1, up.Length, seed);

        /// <summary>
        /// Cuts the drop at the end of <paramref name="up"/> (run <paramref name="i"/>) down to
        /// the landing of <paramref name="down"/>, its outer face rough, rock left over it.
        /// </summary>
        private void CarveDrop(FissureRun up, int i, FissureRun down, int seed, int top, int bottom, int dressed)
        {
            var (lo, hi) = DropColumns(up);
            int roof = DropRoof(up, i, seed);
            int foot = down.Floor0;
            double mid = (lo + hi) / 2.0, half = (hi - lo) / 2.0;
            int outer = up.X1 + up.Dir * (FissureDropW + 1);
            bool roped = foot - up.Floor1 >= FissureRopeDrop;

            for (int y = roof; y < foot; y++)
            {
                for (int x = lo; x <= hi; x++)
                    OpenCrack(x, y, Math.Abs(x - mid) / half, seed, top, bottom, dressed);

                // A drop climbed by its rope has a rough outer face; one climbed by ledges keeps
                // its sides whole for the ledges to stand out of.
                if (roped && y > roof + 1 && y < foot - 2 && Hash01(outer, y, seed + 3) < 0.35)
                    OpenCrack(outer, y, 1.0, seed, top, bottom, dressed);
            }

            // Rock over the drop, to close its top and for a rope to hang from.
            for (int x = lo; x <= hi; x++)
                if (!c.Solid(x, roof - 1))
                    c.Block(x, roof - 1, TileID.Stone);
        }

        /// <summary>
        /// What a drop has in it once everything is cut: the way back up, ledges in a short one
        /// and a rope in a long one, hung beside the lip from the rock over it; cobwebs in the
        /// top of it; and at the foot bones, or the rock that came down, or nothing.
        /// </summary>
        private void FitDrop(FissureRun up, int i, FissureRun down, int seed, int dressed)
        {
            var (lo, hi) = DropColumns(up);
            int roof = DropRoof(up, i, seed);
            int foot = down.Floor0;
            int rope = up.X1 + up.Dir;

            if (foot - up.Floor1 >= FissureRopeDrop)
            {
                for (int y = roof; y < foot; y++)
                    c.Block(rope, y, TileID.Rope);
            }
            else
            {
                Ledges(lo, hi, up.Floor1, foot, up.Dir < 0, dressed);
            }

            int web = up.Dir > 0 ? hi : lo;
            for (int j = 0; j < 3; j++)
            {
                int wx = web - up.Dir * (j % 2), wy = roof + j / 2;
                if (c.TypeAt(wx, wy) < 0 && !c.OneIn(3))
                    c.Block(wx, wy, TileID.Cobweb);
            }

            int pile = c.Next(0, 3);
            int px = up.Dir > 0 ? hi - 2 : lo;
            if (pile == 0)
                c.Place(px, foot - 1, TileID.LargePiles, c.Next(0, 6));
            else if (pile == 1)
                c.Place(px, foot - 1, TileID.LargePiles, c.Next(7, 13));
        }

        /// <summary>
        /// Footholds up a fall of open columns <paramref name="lo"/> to <paramref name="hi"/>
        /// from the floor at <paramref name="foot"/> to the lip at <paramref name="lip"/>: two
        /// wide, out of alternate sides, never more than a jump apart. The first comes out of
        /// the west side unless <paramref name="eastFirst"/>. Slab where the fall goes through
        /// the Reliquary's floor, rock under it.
        /// </summary>
        private void Ledges(int lo, int hi, int lip, int foot, bool eastFirst, int dressed)
        {
            int d = foot - lip;
            int m = (d + FissureReach - 1) / FissureReach - 1;
            for (int j = 1; j <= m; j++)
            {
                int ly = lip + (int)Math.Round(d * j / (double)(m + 1));
                bool east = (j % 2 == 1) == eastFirst;
                int wall = east ? hi : lo, free = east ? hi - 1 : lo + 1;
                bool slab = ly <= dressed;

                foreach (int x in new[] { wall, free })
                {
                    if (slab)
                        c.Block(x, ly, TileID.StoneSlab, NoPaint);
                    else
                        c.Block(x, ly, TileID.Stone);
                }

                // The free end's underside falls away toward the middle of the fall.
                c.Reshape(free, ly, east ? Grace.Cut.UpLeft : Grace.Cut.UpRight);
            }
        }

        /// <summary>
        /// Opens one cell of the crack: nothing in it, and the wall behind it the rock's, or the
        /// crack's own where the cell is near the middle of the way (<paramref name="off"/> is 0
        /// on the middle line and 1 at the edge). The crack is veined blue near the top and
        /// purple toward the bottom; where it goes through the Reliquary's stone the rock is
        /// the Crypt's grey marble.
        /// </summary>
        private void OpenCrack(int x, int y, double off, int seed, int top, int bottom, int dressed)
        {
            c.Erase(x, y);
            c.Liquid(x, y, 0, 0);

            bool line = off < 0.45 || Hash01(x, y, seed + 17) < (1 - off) * 0.7;
            ushort wall;
            if (line)
            {
                double deep = (y - top) / (double)Math.Max(1, bottom - top);
                wall = deep + (Noise(x, y, 11.0, seed + 23) - 0.5) * 0.3 < 0.5 ? WallID.Cave4Unsafe : WallID.Cave5Unsafe;
            }
            else if (y <= dressed)
            {
                wall = Noise(x, y, 4.0, seed + 29) < 0.3 ? WallID.Marble : WallID.MarbleBlock;
            }
            else
            {
                wall = Noise(x, y, 6.0, seed + 31) < 0.35 ? WallID.Cave8Unsafe : WallID.RocksUnsafe1;
            }

            c.Wall(x, y, wall, NoPaint);
        }
    }
}
