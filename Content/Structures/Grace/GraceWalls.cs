using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// How the back wall of a room is laid, floor to ceiling, in registers the way a mason
    /// finishes a room: a dado of something heavy along the floor, a rail over it, the field,
    /// and a frieze under the ceiling, with pilasters standing on the rail at every bay, each
    /// with a head and a foot a tile wider than itself. The field is broken with patches of a
    /// second stone, so no stretch of wall is one flat colour.
    ///
    /// Any register can be left out by leaving its wall 0.
    /// </summary>
    public sealed class Scheme
    {
        public ushort Dado; public byte DadoPaint; public int DadoH = 2;
        public ushort Rail; public byte RailPaint;
        public ushort Field; public byte FieldPaint;
        public ushort Mottle; public byte MottlePaint; public double Mottling; public double MottleScale = 4.5;
        public ushort Frieze; public byte FriezePaint; public int FriezeH = 1;
        public ushort Pilaster; public byte PilasterPaint; public int Bay;
        public ushort Capital; public byte CapitalPaint;

        private const byte White = PaintID.WhitePaint;

        /// <summary>The good rooms: white marble over a slab dado, gold at the rail and the frieze, white brick pilasters.</summary>
        public static readonly Scheme Palace = new Scheme
        {
            Dado = WallID.StoneSlab, DadoPaint = White, DadoH = 2,
            Rail = WallID.AncientGoldBrickWall,
            Field = WallID.MarbleBlock, FieldPaint = White,
            Mottle = WallID.Marble, MottlePaint = White, Mottling = 0.2,
            Frieze = WallID.AncientGoldBrickWall,
            Pilaster = WallID.PearlstoneBrick, PilasterPaint = White, Bay = 8,
            Capital = WallID.GoldBrick,
        };

        /// <summary>The wall's own rooms: a dado of bare slab, the field patched in brick where it was mended, slab pilasters.</summary>
        public static readonly Scheme Guard = new Scheme
        {
            Dado = WallID.StoneSlab, DadoH = 2,
            Rail = WallID.AncientGoldBrickWall,
            Field = WallID.MarbleBlock, FieldPaint = White,
            Mottle = WallID.PearlstoneBrick, MottlePaint = White, Mottling = 0.22,
            Frieze = WallID.GrayBrick,
            Pilaster = WallID.StoneSlab, PilasterPaint = White, Bay = 8,
            Capital = WallID.AncientGoldBrickWall,
        };

        /// <summary>Tower rooms: white brick over a slab dado, gold at rail and frieze. Too narrow for pilasters.</summary>
        public static readonly Scheme Tower = new Scheme
        {
            Dado = WallID.StoneSlab, DadoH = 2,
            Rail = WallID.AncientGoldBrickWall,
            Field = WallID.PearlstoneBrick, FieldPaint = White,
            Mottle = WallID.MarbleBlock, MottlePaint = White, Mottling = 0.25,
            Frieze = WallID.AncientGoldBrickWall,
        };

        /// <summary>The Princess's rooms: plaster over a white dado, gold at rail and frieze.</summary>
        public static readonly Scheme Boudoir = new Scheme
        {
            Dado = WallID.StoneSlab, DadoPaint = White, DadoH = 2,
            Rail = WallID.GoldBrick,
            Field = WallID.WhiteDynasty,
            Frieze = WallID.GoldBrick,
        };

        /// <summary>A house: two rows of stone, a rail of planks, wood above, a band of planks under the ceiling.</summary>
        public static readonly Scheme Home = new Scheme
        {
            Dado = WallID.Stone, DadoH = 2,
            Rail = WallID.Planked,
            Field = WallID.Wood,
            Mottle = WallID.BorealWood, Mottling = 0.12,
            Frieze = WallID.Planked,
            Pilaster = WallID.BorealWood, Bay = 6,
        };

        /// <summary>Stores and cellars: a brick dado, a slab rail, rough stone patched with brick.</summary>
        public static readonly Scheme Store = new Scheme
        {
            Dado = WallID.GrayBrick, DadoH = 2,
            Rail = WallID.StoneSlab,
            Field = WallID.Stone,
            Mottle = WallID.GrayBrick, Mottling = 0.3,
        };

        /// <summary>The Undercity's houses: brick below, planks at the rail, the field gone to rough rock in places.</summary>
        public static readonly Scheme Under = new Scheme
        {
            Dado = WallID.GrayBrick, DadoH = 2,
            Rail = WallID.Planked,
            Field = WallID.GrayBrick,
            Mottle = WallID.RocksUnsafe1, Mottling = 0.3,
            Frieze = WallID.Planked,
        };

        /// <summary>Chapels: a high white dado, gold, marble, a deep gold frieze.</summary>
        public static readonly Scheme Chapel = new Scheme
        {
            Dado = WallID.StoneSlab, DadoPaint = White, DadoH = 3,
            Rail = WallID.GoldBrick,
            Field = WallID.MarbleBlock, FieldPaint = White,
            Mottle = WallID.Marble, MottlePaint = White, Mottling = 0.2,
            Frieze = WallID.GoldBrick, FriezeH = 2,
            Pilaster = WallID.PearlstoneBrick, PilasterPaint = White, Bay = 8,
            Capital = WallID.AncientGoldBrickWall,
        };

        /// <summary>The harbour's rooms: planks below, a rail of boreal, white brick above.</summary>
        public static readonly Scheme Sea = new Scheme
        {
            Dado = WallID.Planked, DadoH = 2,
            Rail = WallID.BorealWood,
            Field = WallID.PearlstoneBrick, FieldPaint = White,
            Mottle = WallID.MarbleBlock, MottlePaint = White, Mottling = 0.15,
            Frieze = WallID.Planked,
        };

        /// <summary>The royal chambers: a high white dado, gold at rail and frieze, marble and pearlstone, gold capitals.</summary>
        public static readonly Scheme Royal = new Scheme
        {
            Dado = WallID.StoneSlab, DadoPaint = White, DadoH = 3,
            Rail = WallID.GoldBrick,
            Field = WallID.MarbleBlock, FieldPaint = White,
            Mottle = WallID.Marble, MottlePaint = White, Mottling = 0.18,
            Frieze = WallID.GoldBrick, FriezeH = 2,
            Pilaster = WallID.PearlstoneBrick, PilasterPaint = White, Bay = 10,
            Capital = WallID.AncientGoldBrickWall,
        };

        /// <summary>The kitchens and service rooms under the Ward: bare slab below, a plank rail, whitened slab above.</summary>
        public static readonly Scheme Service = new Scheme
        {
            Dado = WallID.StoneSlab, DadoH = 2,
            Rail = WallID.Planked,
            Field = WallID.StoneSlab, FieldPaint = White,
            Mottle = WallID.GrayBrick, Mottling = 0.12,
            Frieze = WallID.Planked,
        };

        /// <summary>The crypt of the Reliquary: grey slab below, silver at the rail, marble gone grey, silver brick pilasters.</summary>
        public static readonly Scheme Crypt = new Scheme
        {
            Dado = WallID.StoneSlab, DadoH = 3,
            Rail = WallID.AncientSilverBrickWall,
            Field = WallID.MarbleBlock,
            Mottle = WallID.Marble, Mottling = 0.25,
            Frieze = WallID.AncientSilverBrickWall,
            Pilaster = WallID.StoneSlab, Bay = 10,
            Capital = WallID.AncientSilverBrickWall,
        };

        /// <summary>The waterworks: slab and copper.</summary>
        public static readonly Scheme Works = new Scheme
        {
            Dado = WallID.GrayBrick, DadoH = 2,
            Rail = WallID.CopperPlating,
            Field = WallID.StoneSlab, FieldPaint = White,
            Mottle = WallID.StoneSlab, Mottling = 0.2,
            Frieze = WallID.CopperPlating,
        };
    }

    /// <summary>
    /// The back of every room, and the last passes that make sure nothing in the city shows
    /// the sky through it that should not: walls behind hatches, doorways and hammered
    /// corners.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>
        /// Lays the back wall of a room in its registers (<see cref="Scheme"/>). Over whatever
        /// wall the room had; windows and panels are put in after.
        /// </summary>
        private void Registers(Room r, Scheme s)
        {
            int seed = c.Next(1, 1 << 20);
            int dadoTop = r.Floor - s.DadoH;
            int railRow = s.Rail != 0 ? dadoTop - 1 : dadoTop;
            int friezeEnd = s.Frieze != 0 ? r.Top + s.FriezeH - 1 : r.Top - 1;

            for (int x = r.X0; x <= r.X1; x++)
            {
                for (int y = r.Top; y < r.Floor; y++)
                {
                    ushort w;
                    byte p;

                    if (y >= dadoTop && s.Dado != 0)
                    {
                        w = s.Dado; p = s.DadoPaint;
                    }
                    else if (y == dadoTop - 1 && s.Rail != 0)
                    {
                        w = s.Rail; p = s.RailPaint;
                    }
                    else if (y <= friezeEnd)
                    {
                        w = s.Frieze; p = s.FriezePaint;
                    }
                    else if (s.Mottle != 0 && Noise(x, y, s.MottleScale, seed) < s.Mottling)
                    {
                        w = s.Mottle; p = s.MottlePaint;
                    }
                    else
                    {
                        w = s.Field; p = s.FieldPaint;
                    }

                    if (w != 0)
                        c.Wall(x, y, w, p);
                }
            }

            // Pilasters on the rail, each with a head under the frieze and a foot on the rail.
            if (s.Pilaster == 0 || s.Bay <= 0 || r.W < s.Bay || railRow - friezeEnd < 5)
                return;

            int n = Math.Max(1, (r.W - 1) / s.Bay);

            for (int i = 0; i <= n; i++)
            {
                int px = Spread(r.X0, r.X1, n, i);
                for (int y = friezeEnd + 1; y < railRow; y++)
                    c.Wall(px, y, s.Pilaster, s.PilasterPaint);

                if (s.Capital == 0)
                    continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = px + dx;
                    if (xx < r.X0 || xx > r.X1)
                        continue;
                    c.Wall(xx, friezeEnd + 1, s.Capital, s.CapitalPaint);
                    c.Wall(xx, railRow - 1, s.Capital, s.CapitalPaint);
                }
            }
        }

        /// <summary>
        /// The <paramref name="i"/>-th of <paramref name="n"/> + 1 lines spread evenly from
        /// <paramref name="x0"/> to <paramref name="x1"/>, rounded so the whole set is its own
        /// mirror image: the i-th from one end stands exactly as far in as the i-th from the
        /// other, so pilasters, piers and the bays between them come out the same both ways.
        /// </summary>
        private static int Spread(int x0, int x1, int n, int i)
        {
            if (n <= 0)
                return x0;

            if (2 * i > n)
                return x0 + x1 - Spread(x0, x1, n, n - i);

            return x0 + (int)Math.Floor((x1 - x0) * (double)i / n + 0.5);
        }

        /// <summary>
        /// The middle column of bay <paramref name="i"/> of <paramref name="n"/>, between
        /// columns <paramref name="a"/> and <paramref name="b"/>. A bay an even number wide has
        /// no middle column, so the halves of the room round toward their own ends and the
        /// set stays a mirror image.
        /// </summary>
        private static int BayMid(int a, int b, int n, int i) =>
            2 * i + 1 > n ? (a + b + 1) / 2 : (a + b) / 2;

        /// <summary>Walls you can see the sky through: glass, coloured glass, railings.</summary>
        private static bool SeeThrough(int wall) =>
            wall == WallID.Glass || wall == WallID.WroughtIronFence || wall == WallID.IronFence || wall == WallID.WoodenFence
            || wall == WallID.MetalFence || wall == WallID.AshWoodFence || wall == WallID.YellowStainedGlass
            || wall == WallID.BlueStainedGlass || wall == WallID.PurpleStainedGlass || wall == WallID.RainbowStainedGlass
            || wall == WallID.RedStainedGlass || wall == WallID.GreenStainedGlass;

        /// <summary>
        /// A doorway's reveal: every cell of a passage cut through a wall gets a wall behind it,
        /// the same as the room it opens from, so the passage reads as the thickness of the
        /// wall and not as a slot of sky.
        /// </summary>
        private void Reveal(int x0, int y0, int x1, int y1, ushort wall = 0, byte paint = NoPaint)
        {
            if (wall == 0)
                (wall, paint) = WallBeside(x0, y0, x1, y1);

            Back(x0, y0, x1, y1, wall, paint);
        }

        /// <summary>The wall of whatever room is on either side of a passage; white marble if neither side has one.</summary>
        private (ushort wall, byte paint) WallBeside(int x0, int y0, int x1, int y1)
        {
            int y = (y0 + y1) / 2;
            for (int d = 1; d <= 4; d++)
            {
                foreach (int x in new[] { x1 + d, x0 - d })
                {
                    int w = c.WallAt(x, y);
                    if (w != 0 && !SeeThrough(w) && c.TypeAt(x, y) < 0)
                        return ((ushort)w, c.WallPaintOf(x, y));
                }
            }

            return (WallID.MarbleBlock, White);
        }

        /// <summary>
        /// The last check that nothing indoors shows the sky. A hatch of platforms laid through
        /// a solid floor, or a door standing in its frame, has a wall behind it when the rooms
        /// either side of it have one.
        /// </summary>
        private void Backfill(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (c.WallAt(x, y) != 0)
                        continue;

                    int t = c.TypeAt(x, y);

                    if (t == TileID.Platforms || t == TileID.TrapdoorClosed)
                    {
                        int up = c.WallAt(x, y - 1), dn = c.WallAt(x, y + 1);
                        if (up != 0 && dn != 0 && !SeeThrough(up) && !SeeThrough(dn))
                            c.Wall(x, y, (ushort)up, c.WallPaintOf(x, y - 1));
                    }
                    else if (t == TileID.ClosedDoor || t == TileID.TallGateClosed)
                    {
                        foreach (int dx in new[] { 1, -1 })
                        {
                            int w = c.WallAt(x + dx, y);
                            if (w != 0 && !SeeThrough(w))
                            {
                                c.Wall(x, y, (ushort)w, c.WallPaintOf(x + dx, y));
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A hammered corner shows what is behind the block it was cut from. Where the corner
        /// was cut toward a room, the room's wall is carried in behind the block, so the cut
        /// shows the room and not the sky through the stone.
        /// </summary>
        private void FillBehindCuts(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (c.TypeAt(x, y) < 0 || c.WallAt(x, y) != 0)
                        continue;

                    Cut cut = c.CutAt(x, y);
                    if (cut == Grace.Cut.Full)
                        continue;

                    int ax, ay, bx, by;
                    switch (cut)
                    {
                        case Grace.Cut.DownLeft: ax = x - 1; ay = y; bx = x; by = y - 1; break;
                        case Grace.Cut.DownRight: ax = x + 1; ay = y; bx = x; by = y - 1; break;
                        case Grace.Cut.UpLeft: ax = x - 1; ay = y; bx = x; by = y + 1; break;
                        case Grace.Cut.UpRight: ax = x + 1; ay = y; bx = x; by = y + 1; break;
                        default: ax = x; ay = y - 1; bx = x; by = y - 1; break;
                    }

                    int w = c.WallAt(ax, ay);
                    byte p = c.WallPaintOf(ax, ay);
                    if (w == 0 || SeeThrough(w))
                    {
                        w = c.WallAt(bx, by);
                        p = c.WallPaintOf(bx, by);
                    }

                    if (w != 0 && !SeeThrough(w))
                        c.Wall(x, y, (ushort)w, p);
                }
            }
        }

        /// <summary>
        /// A wall gone to pieces: what is left of the dressed stone in patches, the rough core
        /// showing where the face has come away, mends in brick here and there, and the
        /// darkest rock where it has been dug right back. Nothing open to the sky.
        /// </summary>
        private void BrokenWall(int x0, int y0, int x1, int y1, ushort face, byte facePaint)
        {
            int seed = c.Next(1, 1 << 20);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    double n = Noise(x, y, 3.5, seed);
                    double fine = Hash01(x, y, seed + 5);

                    if (n < 0.34)
                        c.Wall(x, y, face, facePaint);
                    else if (n < 0.5)
                        c.Wall(x, y, fine < 0.5 ? WallID.GrayBrick : WallID.StoneSlab, NoPaint);
                    else if (n < 0.78)
                        c.Wall(x, y, WallID.RocksUnsafe1, NoPaint);
                    else
                        c.Wall(x, y, WallID.RocksUnsafe3, NoPaint);
                }
            }
        }
    }
}
