using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>What hangs on a room's walls, by who used the room.</summary>
    public enum Decor { Royal, Home, Military, Sea, Scholar, Crypt, Ruin, Chapel, Hall }

    /// <summary>
    /// The finish after the masonry: pictures on the walls, vines over the ledges, the stone
    /// itself varied in colour and course so no face is flat, and the ash over everything
    /// the sky can reach.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        // ------------------------------------------------------------ pictures

        /// <summary>Paintings by kind: (tile, style), chosen from the game's own sheets by what they show.</summary>
        private static readonly Dictionary<Decor, (ushort type, int style)[]> Pictures = new Dictionary<Decor, (ushort, int)[]>
        {
            // Portraits of the house, classical subjects, the crown and the sun.
            [Decor.Royal] = new (ushort, int)[]
            {
                (TileID.Painting6X4, 46), (TileID.Painting6X4, 47), (TileID.Painting6X4, 48), (TileID.Painting6X4, 49),
                (TileID.Painting6X4, 16), (TileID.Painting6X4, 5), (TileID.Painting3X3, 72), (TileID.Painting3X3, 92),
                (TileID.Painting3X3, 24), (TileID.Painting3X3, 33), (TileID.Painting2X3, 9), (TileID.Painting2X3, 3),
            },
            // Small framed flowers and landscapes, sunflowers, a window's worth of sky.
            [Decor.Home] = new (ushort, int)[]
            {
                (TileID.Painting3X2, 2), (TileID.Painting3X2, 3), (TileID.Painting3X2, 4), (TileID.Painting3X2, 5),
                (TileID.Painting3X2, 31), (TileID.Painting3X2, 34), (TileID.Painting2X3, 3), (TileID.Painting2X3, 8),
                (TileID.Painting2X3, 9), (TileID.Painting2X3, 19), (TileID.Painting3X3, 20), (TileID.Painting3X2, 12),
            },
            // Racks of arms, drawn on the wall.
            [Decor.Military] = new (ushort, int)[]
            {
                (TileID.Painting3X3, 41), (TileID.Painting3X3, 43), (TileID.Painting3X3, 44), (TileID.Painting3X3, 45),
                (TileID.Painting3X3, 42),
            },
            // The sea they built for: the wheel, the anchor, the compass, the ring, the wave, a ship.
            [Decor.Sea] = new (ushort, int)[]
            {
                (TileID.Painting3X3, 47), (TileID.Painting3X3, 49), (TileID.Painting3X3, 48), (TileID.Painting3X3, 46),
                (TileID.Painting3X2, 26), (TileID.Painting3X2, 27), (TileID.Painting6X4, 11), (TileID.Painting6X4, 26),
            },
            // Tablets, maps, far places.
            [Decor.Scholar] = new (ushort, int)[]
            {
                (TileID.Painting6X4, 40), (TileID.Painting6X4, 39), (TileID.Painting6X4, 25), (TileID.Painting3X2, 19),
                (TileID.Painting2X3, 8), (TileID.Painting6X4, 37), (TileID.Painting6X4, 42), (TileID.Painting6X4, 41),
            },
            // Niches with the dead in them.
            [Decor.Crypt] = new (ushort, int)[]
            {
                (TileID.Painting4X3, 0), (TileID.Painting4X3, 1), (TileID.Painting4X3, 2), (TileID.Painting4X3, 6),
                (TileID.Painting4X3, 7), (TileID.Painting4X3, 8), (TileID.Painting6X4, 44),
            },
            // What was left on the walls down there.
            [Decor.Ruin] = new (ushort, int)[]
            {
                (TileID.Painting3X3, 16), (TileID.Painting4X3, 6), (TileID.Painting3X2, 34), (TileID.Painting3X2, 2),
                (TileID.Painting4X3, 3), (TileID.Painting3X3, 17),
            },
            // Coloured panels and the sun.
            [Decor.Chapel] = new (ushort, int)[]
            {
                (TileID.Painting2X3, 16), (TileID.Painting2X3, 17), (TileID.Painting3X3, 92), (TileID.Painting2X3, 18),
            },
            // Landscapes for the halls: far country, the plain before the siege.
            [Decor.Hall] = new (ushort, int)[]
            {
                (TileID.Painting6X4, 43), (TileID.Painting6X4, 61), (TileID.Painting6X4, 70), (TileID.Painting6X4, 12),
                (TileID.Painting3X2, 34), (TileID.Painting3X2, 31), (TileID.Painting6X4, 38),
            },
        };

        /// <summary>Walls a picture will not be hung against: glass, coloured glass, railings.</summary>
        private static bool Unhangable(int wall) =>
            wall == 0 || wall == WallID.Glass || wall == WallID.WroughtIronFence || wall == WallID.YellowStainedGlass
            || wall == WallID.BlueStainedGlass || wall == WallID.PurpleStainedGlass || wall == WallID.RainbowStainedGlass
            || wall == WallID.RedStainedGlass || wall == WallID.GreenStainedGlass;

        /// <summary>
        /// Hangs pictures along a room's back wall at the height a person looks, above whatever
        /// stands on the floor, one every so often, never over a window.
        /// </summary>
        private void Adorn(Room r, Decor kind, int every = 11, int eye = 4)
        {
            if (!Pictures.TryGetValue(kind, out var set) || r.W < 5 || r.H < 5)
                return;

            int x = r.X0 + 1 + c.Next(0, 3);
            int k = c.Next(0, set.Length);

            while (x <= r.X1 - 2)
            {
                bool placed = false;

                for (int tries = 0; tries < set.Length && !placed; tries++)
                {
                    var (type, style) = set[(k + tries) % set.Length];
                    var (w, h) = c.Size(type, style);
                    if (x + w - 1 > r.X1)
                        continue;

                    for (int lift = 0; lift <= 3 && !placed; lift++)
                    {
                        int bottom = r.Floor - eye - lift;
                        int top = bottom - h + 1;
                        if (top <= r.Top)
                            break;

                        if (!ClearWall(x, top, x + w - 1, bottom))
                            continue;

                        placed = c.Place(x, bottom, type, style);
                        if (placed)
                        {
                            k = (k + tries + 1) % set.Length;
                            x += w + every - 3 + c.Next(0, 4);
                        }
                    }
                }

                if (!placed)
                    x++;
            }
        }

        /// <summary>Every tile in the box is open, with a wall behind it that will take a nail.</summary>
        private bool ClearWall(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (c.TypeAt(x, y) >= 0 || Unhangable(c.WallAt(x, y)))
                        return false;
            return true;
        }

        /// <summary>A pot of something green on a chain, where a room has the height for it.</summary>
        private void HangingPot(int x, int fromY)
        {
            HangAt(x, fromY, TileID.PotsSuspended, c.Next(0, 6));
        }

        // ------------------------------------------------------------ vines

        /// <summary>
        /// Vines: from the underside of every ledge, arch, gallery and cornice open to the sky,
        /// here and there, a strand of vine rope hanging down, painted: mostly deep green,
        /// some grey and dead, some left as they came. In the ruins below, the same strands
        /// coming through the holes as roots.
        /// </summary>
        private void Vines(int x0, int x1, int y0, int y1)
        {
            byte[] outdoor = { PaintID.DeepGreenPaint, PaintID.DeepGreenPaint, PaintID.GreenPaint, PaintID.DeepGreenPaint, Gray, NoPaint, PaintID.DeepLimePaint };
            byte[] under = { PaintID.BrownPaint, PaintID.BrownPaint, Gray, NoPaint };

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    int t = c.TypeAt(x, y);
                    if (t < 0 || !Masonry.Contains(t) || c.TypeAt(x, y + 1) >= 0)
                        continue;

                    int wall = c.WallAt(x, y + 1);
                    bool sky = y < 0 && wall == 0;
                    bool ruin = y > 40 && (wall == WallID.DirtUnsafe || wall == WallID.RocksUnsafe1);

                    if (!sky && !ruin)
                        continue;
                    if (!c.OneIn(sky ? 7 : 12))
                        continue;

                    byte paint = sky ? outdoor[c.Next(0, outdoor.Length)] : under[c.Next(0, under.Length)];
                    int len = sky ? c.Next(2, 9) : c.Next(2, 6);

                    // A strand stops a tile short of anything under it, and short of water.
                    for (int i = 1; i <= len; i++)
                    {
                        if (c.TypeAt(x, y + i) >= 0 || c.TypeAt(x, y + i + 1) >= 0
                            || c.LiquidAt(x, y + i + 1) > 0 || c.LiquidAt(x, y + i + 2) > 0)
                            break;
                        c.Block(x, y + i, TileID.VineRope, paint);
                    }
                }
            }
        }

        // ------------------------------------------------------------ the stone, varied

        /// <summary>Smooth value noise in [0, 1): the same (x, y) always gives the same number.</summary>
        private static double Noise(int x, int y, double scale, int seed)
        {
            double fx = x / scale, fy = y / scale;
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            double tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            double a = Hash01(ix, iy, seed), b = Hash01(ix + 1, iy, seed);
            double c0 = Hash01(ix, iy + 1, seed), d = Hash01(ix + 1, iy + 1, seed);
            return (a + (b - a) * tx) * (1 - ty) + (c0 + (d - c0) * tx) * ty;
        }

        private static double Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        /// <summary>
        /// The masons' variety, laid over the whole city once it stands:
        /// white stone weathered back to its own grey in soft patches, a course of slab every
        /// few rows in the big white faces, floors laid in alternating slabs, the white walls
        /// of rooms mottled between two marbles, brick grimed where it meets the street.
        /// Paint changes do not break the joints between tiles of one stone, so the patches
        /// run on without seams; the courses change the stone, and so they show as joints.
        /// </summary>
        private void Texture(int x0, int x1, int y0, int y1)
        {
            int seed = c.Next(1, 1 << 20);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    int t = c.TypeAt(x, y);

                    if (t == TileID.MarbleBlock && PaintAt(x, y) == White)
                    {
                        double n = Noise(x, y, 7.0, seed);
                        double fine = Hash01(x, y, seed + 7);

                        // Courses: in a face of white more than a few tiles each way, every
                        // sixth row is laid in slab.
                        bool face = c.TypeAt(x, y - 1) == TileID.MarbleBlock && c.TypeAt(x, y + 1) == TileID.MarbleBlock
                            && (c.TypeAt(x - 1, y) < 0 || c.TypeAt(x + 1, y) < 0 || c.WallAt(x, y) != 0);
                        if (face && Mod(y, 6) == 0 && c.CutAt(x, y) == Grace.Cut.Full)
                        {
                            c.Block(x, y, TileID.StoneSlab, White);
                            continue;
                        }

                        if (n < 0.16)
                            c.Paint(x, y, NoPaint);
                        else if (fine < 0.025)
                            c.Paint(x, y, Gray);
                    }
                    else if (t == TileID.StoneSlab && PaintAt(x, y) == White && c.TypeAt(x, y - 1) < 0)
                    {
                        // Floors and paving in alternating slabs.
                        if (Mod(x, 4) >= 2)
                            c.Paint(x, y, NoPaint);
                    }
                    else if ((t == TileID.GrayBrick || t == TileID.RedBrick) && y > 40)
                    {
                        // Grime: darker low down and in patches.
                        if (Noise(x, y, 5.0, seed + 3) < 0.25)
                            c.Paint(x, y, PaintID.BrownPaint);
                    }

                    int w = c.WallAt(x, y);
                    if (w == WallID.MarbleBlock && WallPaintAt(x, y) == White)
                    {
                        double n = Noise(x, y, 9.0, seed + 11);
                        if (n < 0.18)
                            c.Wall(x, y, WallID.MarbleBlock, NoPaint);
                        else if (n > 0.86)
                            c.Wall(x, y, WallID.Marble, White);
                    }
                    else if (w == WallID.GrayBrick && Noise(x, y, 6.0, seed + 5) < 0.2)
                    {
                        c.Wall(x, y, WallID.GrayBrick, PaintID.BrownPaint);
                    }
                }
            }
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;

        private byte PaintAt(int x, int y) => c.PaintOf(x, y);

        private byte WallPaintAt(int x, int y) => c.WallPaintOf(x, y);

        // ------------------------------------------------------------ the ash

        /// <summary>
        /// Ash over everything inside the Low City's gate that the sky can reach: a skin of it
        /// on every flat top, heaped up against every wall it was blown against, a little
        /// further in under arches. It lies on the roofs and the walks and the paving; it does
        /// not lie in doorways, on stairs, or on anything standing on the floor. The
        /// Processional is kept swept.
        /// </summary>
        private void Ashfall()
        {
            int x0 = LowCityX, x1 = ShoreX + 36;
            var tops = new int[x1 - x0 + 1];

            for (int x = x0; x <= x1; x++)
            {
                tops[x - x0] = int.MinValue;
                for (int y = -site.Top; y < 40; y++)
                {
                    if (c.TypeAt(x, y) < 0)
                        continue;
                    tops[x - x0] = y;
                    break;
                }
            }

            for (int x = x0; x <= x1; x++)
            {
                int y = tops[x - x0];
                if (y == int.MinValue || y < -site.Top + 2)
                    continue;

                int t = c.TypeAt(x, y);
                if (!Masonry.Contains(t) || c.CutAt(x, y) != Grace.Cut.Full || c.WallAt(x, y - 1) != 0)
                    continue;
                if (NearDoor(x, y - 1))
                    continue;

                int left = x > x0 ? tops[x - x0 - 1] : int.MinValue;
                int right = x < x1 ? tops[x - x0 + 1] : int.MinValue;
                bool wallLeft = left != int.MinValue && left < y - 2;
                bool wallRight = right != int.MinValue && right < y - 2;

                byte paint = c.OneIn(3) ? Gray : White;

                if (wallLeft || wallRight)
                {
                    // A drift against the wall: two high at the wall, sloping away.
                    c.Block(x, y - 1, TileID.Ash, paint);
                    if (c.TypeAt(x, y - 2) < 0 && !NearDoor(x, y - 2))
                        c.Block(x, y - 2, TileID.Ash, paint, wallLeft ? Grace.Cut.DownRight : Grace.Cut.DownLeft);
                    int away = wallLeft ? 1 : -1;
                    if (c.TypeAt(x + away, y - 1) < 0 && tops[Math.Clamp(x + away - x0, 0, tops.Length - 1)] == y)
                        c.Block(x + away, y - 1, TileID.Ash, paint, wallLeft ? Grace.Cut.DownRight : Grace.Cut.DownLeft);
                }
                else if (!c.OneIn(4))
                {
                    c.Block(x, y - 1, TileID.Ash, paint, Grace.Cut.Half);
                }
            }
        }

        /// <summary>A door, a gate or a trapdoor within a step of this tile.</summary>
        private bool NearDoor(int x, int y)
        {
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -3; dy <= 1; dy++)
                {
                    int t = c.TypeAt(x + dx, y + dy);
                    if (t == TileID.ClosedDoor || t == TileID.TallGateClosed || t == TileID.TrapdoorClosed || t == TileID.Platforms)
                        return true;
                }
            return false;
        }
    }
}
