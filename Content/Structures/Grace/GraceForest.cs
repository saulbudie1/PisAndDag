using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    public sealed partial class GraceBuilder
    {
        /// <summary>The ground row in this column: the first solid tile near the plain's level.</summary>
        private int GroundAt(int x)
        {
            int g = c.FirstSolidDown(x, -60, 120);
            return g == int.MinValue ? 0 : g;
        }

        /// <summary>
        /// The willows. The flat of the plateau between the top of the way up and the
        /// Curtain is a close wood of yellow willow grown three times the height the kind
        /// usually reaches, and the road runs through it on the old ground: stones set into
        /// the turf where the road was mended and grass where it was not, every step of it
        /// smooth underfoot.
        /// </summary>
        private void Forest()
        {
            int from = Math.Max(0, ForestStart - 24);
            int to = CurtainX - 14;

            // The road first: runs of set stone in the turf.
            bool stone = false;
            int run = c.Next(2, 6);

            for (int x = from; x <= CurtainX + 2; x++)
            {
                if (--run <= 0)
                {
                    stone = !stone;
                    run = stone ? c.Next(1, 5) : c.Next(2, 7);
                }

                // Nearer the wall the road was kept better.
                bool near = x > CurtainX - 40;
                if (!stone && !near)
                    continue;

                int g = GroundAt(x);
                if (c.TypeAt(x, g) != TileID.Grass && c.TypeAt(x, g) != TileID.Dirt)
                    continue;

                Cut keep = c.CutAt(x, g);
                ushort t = c.OneIn(4) ? TileID.MarbleBlock : TileID.StoneSlab;
                c.Block(x, g, t, c.OneIn(5) ? Gray : White, keep);
            }

            // Then the trees, as close as willow will stand.
            int xT = from + c.Next(0, 3);

            while (xT <= to)
            {
                int g = GroundAt(xT);

                if (c.TypeAt(xT, g) == TileID.Grass && (c.TypeAt(xT - 1, g) == TileID.Grass || c.TypeAt(xT + 1, g) == TileID.Grass)
                    && c.CutAt(xT, g) == Grace.Cut.Full)
                {
                    // A yellow willow grows seven to twelve; these are three times that.
                    int tall = c.Next(21, 37);
                    if (c.Tree(xT, g, TileID.VanityTreeYellowWillow, tall))
                    {
                        xT += c.Next(5, 8);
                        continue;
                    }
                }

                xT++;
            }

            // The leaves down under them.
            LeafDrifts(from, to);

            // Grass and flowers wherever nothing else stands.
            for (int x = from; x <= to; x++)
            {
                int g = GroundAt(x);

                if (c.TypeAt(x, g) != TileID.Grass || c.TypeAt(x, g - 1) >= 0 || c.CutAt(x, g) != Grace.Cut.Full)
                    continue;

                if (c.OneIn(3))
                    continue;

                int style = c.OneIn(5) ? c.Next(6, 11) : c.Next(0, 6);
                c.Place(x, g - 1, TileID.Plants, style);
            }
        }

        /// <summary>
        /// Autumn under the willows. Drifts of fallen leaf banked up behind, as walls, their
        /// height rising and falling along the wood; and in front small piles of leaf on the
        /// turf between the trees, never more than a tile high and run out in slopes at both
        /// ends, so they are walked straight over. Orange mostly, then yellow in the banks and
        /// deep orange in the piles, now and then a brown one. Clear of the road, the trunks
        /// and the roots.
        /// </summary>
        private void LeafDrifts(int from, int to)
        {
            byte[] banks = { PaintID.OrangePaint, PaintID.OrangePaint, PaintID.OrangePaint, PaintID.YellowPaint };
            byte[] piles =
            {
                PaintID.OrangePaint, PaintID.OrangePaint, PaintID.OrangePaint, PaintID.DeepOrangePaint,
                PaintID.DeepOrangePaint, PaintID.YellowPaint, PaintID.BrownPaint,
            };
            int seed = c.Next(1, 1 << 20);

            // Behind: the banks.
            for (int x = from; x <= to; x++)
            {
                int g = GroundAt(x);
                int h = (int)Math.Round((Noise(x, 0, 7.0, seed) - 0.25) * 5.0 + (Hash01(x, 1, seed) - 0.5));
                for (int y = g - 1; y >= g - h; y--)
                {
                    if (c.WallAt(x, y) != 0)
                        continue;
                    int k = (int)(Noise(x, y, 4.0, seed + 5) * banks.Length);
                    c.Wall(x, y, WallID.LivingLeaf, banks[Math.Min(k, banks.Length - 1)]);
                }
            }

            // In front: a pile in most of the stretches of clear turf between the trees.
            int x0 = from;
            while (x0 <= to)
            {
                if (!LevelTurf(x0, x0))
                {
                    x0++;
                    continue;
                }

                int x1 = x0;
                while (x1 + 1 <= to && x1 - x0 < 9 && LevelTurf(x0, x1 + 1))
                    x1++;

                int room = x1 - x0 + 1;
                if (room >= 2 && !c.OneIn(4))
                {
                    int len = c.Next(2, Math.Min(6, room) + 1);
                    int at = x0 + c.Next(0, room - len + 1);
                    int g = GroundAt(at);
                    byte paint = piles[c.Next(0, piles.Length)];
                    for (int i = 0; i < len; i++)
                    {
                        Cut cut = i == 0 ? Grace.Cut.DownLeft
                            : i == len - 1 ? Grace.Cut.DownRight
                            : len >= 4 && (i == len / 2 || c.OneIn(3)) ? Grace.Cut.Full
                            : Grace.Cut.Half;
                        byte p = c.OneIn(4) ? piles[c.Next(0, piles.Length)] : paint;
                        c.Block(at + i, g - 1, TileID.LeafBlock, p, cut);
                    }
                }

                x0 = x1 + 1;
            }
        }

        /// <summary>Columns <paramref name="x0"/> to <paramref name="x1"/> are turf at one level, square-topped, with nothing on them.</summary>
        private bool LevelTurf(int x0, int x1)
        {
            int g = GroundAt(x0);
            for (int x = x0; x <= x1; x++)
            {
                if (GroundAt(x) != g || c.CutAt(x, g) != Grace.Cut.Full || c.TypeAt(x, g - 1) >= 0)
                    return false;
                int t = c.TypeAt(x, g);
                if (t != TileID.Grass && t != TileID.Dirt)
                    return false;
            }

            return true;
        }
    }
}
