using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The finer tools: courses, cornices, windows, pilasters, friezes, vaulted rooms, and
    /// the last pass that hammers every exposed corner of the city smooth.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        // ------------------------------------------------------------ courses

        /// <summary>
        /// A course across [x0, x1] on row y, standing <paramref name="proud"/> tiles out from
        /// both faces, its ends chamfered underneath so it reads as a moulding.
        /// </summary>
        private void Course(int x0, int x1, int y, ushort type, byte paint, int proud = 1)
        {
            Box(x0 - proud, y, x1 + proud, y, type, paint);

            if (proud > 0)
            {
                Cut(x0 - proud, y, Grace.Cut.UpLeft);
                Cut(x1 + proud, y, Grace.Cut.UpRight);
            }
        }

        /// <summary>
        /// A cornice: a course of white under a course of gold, both proud of the face, the
        /// top one's ends sloped, the lower one's chamfered. Crowns a wall.
        /// </summary>
        private void Cornice(int x0, int x1, int y, byte goldPaint = NoPaint)
        {
            Box(x0 - 1, y, x1 + 1, y, TileID.MarbleBlock, White);
            Cut(x0 - 1, y, Grace.Cut.UpLeft);
            Cut(x1 + 1, y, Grace.Cut.UpRight);
            Box(x0 - 1, y - 1, x1 + 1, y - 1, TileID.AncientGoldBrick, goldPaint);
            Cut(x0 - 1, y - 1, Grace.Cut.DownLeft);
            Cut(x1 + 1, y - 1, Grace.Cut.DownRight);
        }

        // ------------------------------------------------------------ openings

        /// <summary>Glass in a skin: a window cut through a wall of blocks, [x0, x1] by [y0, y1].</summary>
        private void Glaze(int x0, int y0, int x1, int y1, byte paint = NoPaint)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    c.Block(x, y, TileID.Glass, paint);
        }

        /// <summary>
        /// An arched patch of wall in the back of a room: a window of glass wall, a niche of a
        /// darker stone, a painted panel. [x0, x1] wide, from <paramref name="sill"/> up to a
        /// round head that springs at <paramref name="spring"/> and rises <paramref name="rise"/>.
        /// </summary>
        private void BackArch(int x0, int x1, int sill, int spring, int rise, ushort wall, byte paint = NoPaint)
        {
            for (int x = x0; x <= x1; x++)
            {
                int top = HeadTop(x, x0, x1, spring, rise);
                for (int y = top; y <= sill; y++)
                    c.Wall(x, y, wall, paint);
            }
        }

        /// <summary>
        /// A window in the back wall with a stone surround: the surround a tile wider all round
        /// in <paramref name="frame"/>, the light itself in glass.
        /// </summary>
        private void Window(int x0, int x1, int sill, int spring, int rise, ushort frame = WallID.PearlstoneBrick, byte framePaint = White, ushort glass = WallID.Glass, byte glassPaint = NoPaint)
        {
            BackArch(x0 - 1, x1 + 1, sill + 1, spring, rise + 1, frame, framePaint);
            BackArch(x0, x1, sill, spring, rise, glass, glassPaint);
        }

        // ------------------------------------------------------------ the back of a room

        /// <summary>
        /// Dresses the back wall of a room the way the good rooms of the capital were done:
        /// a dado of slab along the floor, pilasters at intervals, a gold frieze under the
        /// ceiling, and tall windows between the pilasters where the room has the height.
        /// </summary>
        private void Panelled(Room r, ushort field, byte fieldPaint, int bay = 8, bool windows = true,
            ushort frieze = WallID.AncientGoldBrickWall, ushort pilaster = WallID.PearlstoneBrick, byte pilasterPaint = White)
        {
            var s = new Scheme
            {
                Dado = WallID.StoneSlab, DadoPaint = White, DadoH = 2,
                Rail = WallID.AncientGoldBrickWall,
                Field = field, FieldPaint = fieldPaint,
                Mottle = field == WallID.MarbleBlock ? WallID.Marble : (ushort)0, MottlePaint = White, Mottling = 0.2,
                Frieze = frieze,
                Pilaster = pilaster, PilasterPaint = pilasterPaint, Bay = bay,
                Capital = WallID.GoldBrick,
            };
            Registers(r, s);

            if (!windows || r.H < 9)
                return;

            // Windows between the pilasters, standing on the rail.
            int n = Math.Max(1, (r.W - 1) / bay);

            for (int i = 0; i < n; i++)
            {
                int a = Spread(r.X0, r.X1, n, i) + 2;
                int b = Spread(r.X0, r.X1, n, i + 1) - 2;
                if (b - a < 1)
                    continue;

                int sill = r.Floor - s.DadoH - 3;
                int head = r.Top + 3;
                int rise = Math.Min(2, (b - a + 1) / 2);
                if (sill - head < rise + 1)
                    continue;
                Window(a, b, sill, head + rise, rise);
            }
        }

        // ------------------------------------------------------------ vaulted rooms

        /// <summary>
        /// A room with a round vault: carved out of whatever solid is already there, walls
        /// straight up to <paramref name="spring"/> and then a curve <paramref name="rise"/>
        /// high, the soffit dressed. Returns the room under the springing line.
        /// </summary>
        private Room Vaulted(int x0, int x1, int floor, int spring, int rise, ushort wall, byte wallPaint)
        {
            ArchOpening(x0, x1, floor - 1, spring, rise, wall, wallPaint);
            return new Room(x0, x1, spring - rise, floor);
        }

        // ------------------------------------------------------------ hanging things

        /// <summary>The first solid row at or above <paramref name="fromY"/> in this column, or int.MinValue.</summary>
        private int CeilingOver(int x, int fromY, int limit = 120)
        {
            for (int y = fromY; y > fromY - limit; y--)
                if (c.Solid(x, y))
                    return y;

            return int.MinValue;
        }

        /// <summary>
        /// Hangs something from whatever ceiling is over its middle column, looking up from
        /// <paramref name="fromY"/>. Vaults curve, so the ceiling is found rather than given.
        /// </summary>
        private bool HangAt(int x, int fromY, ushort type, int style = 0)
        {
            var (w, _) = c.Size(type, style);
            int ceil = CeilingOver(x + w / 2, fromY);
            return ceil != int.MinValue && Hang(x, ceil + 1, type, style);
        }

        // ------------------------------------------------------------ writing

        /// <summary>
        /// A sign as near as it will go to where it was meant: the spot itself, then a column
        /// or two either side, then a row up or down. Signs hang on the wall behind them, so
        /// only spots with a wall behind all four tiles and nothing in front are taken.
        /// </summary>
        private bool SignNear(int x, int bottom, string text, int reach = 8)
        {
            for (int d = 0; d <= reach; d++)
            {
                foreach (int dx in d == 0 ? new[] { 0 } : new[] { d, -d })
                    foreach (int dy in new[] { 0, -1, 1 })
                    {
                        int sx = x + dx, sb = bottom + dy;
                        bool free = true;
                        for (int i = 0; i < 2 && free; i++)
                            for (int j = 0; j < 2 && free; j++)
                                free = c.TypeAt(sx + i, sb - j) < 0 && c.WallAt(sx + i, sb - j) != 0;
                        if (free && c.Sign(sx, sb, TileID.Signs, 0, text))
                            return true;
                    }
            }

            return false;
        }

        // ------------------------------------------------------------ stairs of platforms

        /// <summary>
        /// A flight of platforms from (x, floor) rising <paramref name="n"/> rows toward
        /// <paramref name="dir"/>, clearing a headroom of three rows over every tread and
        /// putting a back wall behind it if one is given.
        /// </summary>
        private void OpenFlight(int x, int floor, int n, int dir, int style, ushort wall = 0, byte wallPaint = NoPaint)
        {
            for (int i = 1; i <= n; i++)
            {
                int xx = x + (i - 1) * dir;
                int y = floor - i;

                for (int h = 0; h <= 3; h++)
                {
                    c.Erase(xx, y - h);
                    if (wall != 0)
                        c.Wall(xx, y - h, wall, wallPaint);
                }

                c.Platform(xx, y, style, dir > 0 ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
            }
        }

        // ------------------------------------------------------------ the finish

        /// <summary>Everything the masons laid, as opposed to the ground it stands on.</summary>
        private static readonly HashSet<int> Masonry = new HashSet<int>
        {
            TileID.MarbleBlock, TileID.Marble, TileID.StoneSlab, TileID.SandstoneBrick, TileID.SandStoneSlab,
            TileID.SmoothSandstone, TileID.GoldBrick, TileID.AncientGoldBrick, TileID.AncientSilverBrick,
            TileID.PearlstoneBrick, TileID.SnowBrick, TileID.GrayBrick, TileID.RedDynastyShingles,
            TileID.BlueDynastyShingles, TileID.DynastyWood, TileID.YellowStucco, TileID.RedStucco,
            TileID.GrayStucco, TileID.CopperPlating, TileID.TinPlating, TileID.Sunplate, TileID.HeavenforgeBrick,
            TileID.AccentSlab, TileID.WoodBlock, TileID.BorealWood, TileID.AshWood, TileID.IronBrick,
            TileID.SilverBrick, TileID.CopperBrick, TileID.TinBrick, TileID.LeadBrick,
        };

        /// <summary>
        /// The mason's last day. A top corner of laid stone that stands out into the air is
        /// taken off where it is a step of a stair or the end of a broad top, so a flight
        /// reads as a slope and a parapet as dressed stone; a top two wide or less keeps its
        /// square corners, so merlons, posts and pilasters stay square. An underside is cut
        /// only where it runs as a stair does, each step one over and one down, so the soffit
        /// of a flight is smooth and the foot of a pilaster, the end of a slab and the head of
        /// a doorway stay square. Nothing that anything stands on or hangs from is touched.
        /// </summary>
        private void Hammer(int x0, int y0, int x1, int y1)
        {
            var cuts = new List<(int x, int y, Cut cut)>();

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (!Masonry.Contains(c.TypeAt(x, y)) || c.CutAt(x, y) != Grace.Cut.Full)
                        continue;

                    Cut cut = TopCorner(x, y);
                    if (cut == Grace.Cut.Full)
                        cut = UnderCorner(x, y);
                    if (cut != Grace.Cut.Full)
                        cuts.Add((x, y, cut));
                }
            }

            // Decided from the stone as it was, then cut, so one cut never causes the next.
            foreach (var (x, y, cut) in cuts)
                c.Reshape(x, y, cut);
        }

        /// <summary>
        /// The cut a top corner takes, or Full. The block has open air over it and on one side,
        /// stone under it, and stone going on away from the open side, either rising (the next
        /// block in is a step higher: a stair) or level for at least two more (a broad top).
        /// </summary>
        private Cut TopCorner(int x, int y)
        {
            if (c.TypeAt(x, y - 1) >= 0 || !Mass(x, y + 1))
                return Grace.Cut.Full;

            foreach (int open in new[] { -1, 1 })
            {
                if (c.TypeAt(x + open, y) >= 0)
                    continue;

                int inward = -open;
                if (!Mass(x + inward, y))
                    continue;

                bool stair = Mass(x + inward, y - 1);
                bool broad = c.TypeAt(x + inward, y - 1) < 0 && Mass(x + 2 * inward, y);
                if (stair || broad)
                    return open < 0 ? Grace.Cut.DownLeft : Grace.Cut.DownRight;
            }

            return Grace.Cut.Full;
        }

        /// <summary>
        /// The cut an underside corner takes, or Full. The block has open air under it and on
        /// one side, stone over it and going on away from the open side, and the next block in
        /// is a step lower with air under that: one step of an underside that runs like a stair.
        /// </summary>
        private Cut UnderCorner(int x, int y)
        {
            if (c.TypeAt(x, y + 1) >= 0 || !Mass(x, y - 1))
                return Grace.Cut.Full;

            foreach (int open in new[] { -1, 1 })
            {
                if (c.TypeAt(x + open, y) >= 0)
                    continue;

                int inward = -open;
                if (Mass(x + inward, y) && Mass(x + inward, y + 1) && c.TypeAt(x + inward, y + 2) < 0)
                    return open < 0 ? Grace.Cut.UpLeft : Grace.Cut.UpRight;
            }

            return Grace.Cut.Full;
        }

        /// <summary>A whole block of stone here: solid, full, and not a platform.</summary>
        private bool Mass(int x, int y) => c.Solid(x, y) && c.TypeAt(x, y) != TileID.Platforms && c.CutAt(x, y) == Grace.Cut.Full;
    }
}
