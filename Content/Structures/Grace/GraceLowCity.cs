using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The Low City: the good years, standing up. A street at the height of the
    /// Processional's deck, carried on vaulted cellars; the gate with its watch tower, six
    /// granaries, the market hall, the almshouses, and the waterworks at the foot of the
    /// Ward. Everything in it is whole. Nobody is in any of it.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>The Low City's street: the Processional's deck carried on into the town.</summary>
        private const int Street = DeckY;

        /// <summary>Lot widths along the street, west to east.</summary>
        private const int GateW = 44, GranW = 11, GranGap = 4, MarketW = 70, AlmsW = 20, WaterW = 40;

        /// <summary>What stands over each cellar, so the cellars can be stocked for it.</summary>
        private enum Above { Street, Gate, Granary, Market, Alms, Water }

        private readonly List<(Room room, Above above)> cellars = new List<(Room, Above)>();

        private void LowCity()
        {
            int x0 = LowCityX, x1 = WardX - 1;
            int avail = x1 - x0 + 1;

            // What fits. Six granaries and three almshouses on a full plain; fewer of both if
            // the way up ate into it.
            int fixedW = GateW + 8 + 6 + MarketW + 6 + 6 + WaterW + 12;
            int granaries = 6;
            while (granaries > 3 && fixedW + GranRow(granaries) > avail)
                granaries--;
            int alms = Math.Max(0, Math.Min(4, (avail - fixedW - GranRow(granaries)) / AlmsW));
            int slack = Math.Max(0, avail - fixedW - GranRow(granaries) - alms * AlmsW);

            Terrace(x0, x1);

            int x = x0;
            LowGate(x);
            x += GateW;

            int squareW = 8 + slack / 3;
            Fountain(x + squareW / 2 - 1);
            CellarHatch(x + 1);
            x += squareW;

            int granEnd = x + GranRow(granaries) - 1;
            for (int k = 0; k < granaries; k++)
            {
                int gx = x + k * (GranW + GranGap);
                Granary(gx, Street, 60, k == 0 ? "Three years of it, against a year of need." : null);
                MarkCellars(gx, gx + GranW - 1, Above.Granary);
            }
            x = granEnd + 1 + 6 + slack / 3;

            Market(x, x + MarketW - 1);
            MarkCellars(x, x + MarketW - 1, Above.Market);
            x += MarketW + 6;

            int almsStart = x;
            for (int k = 0; k < alms; k++)
            {
                int hx = x + k * AlmsW;
                Almshouse(hx, hx + AlmsW - 1, k);
                MarkCellars(hx, hx + AlmsW - 1, Above.Alms);
            }
            x += alms * AlmsW + 6;
            CellarHatch(x - 5);

            int w0 = x1 - 11 - WaterW + 1;
            Waterworks(w0, w0 + WaterW - 1, x1);
            MarkCellars(w0, x1, Above.Water);

            // The cistern runs under the almshouses and the waterworks.
            Cistern(Math.Max(almsStart + 2, w0 - 60), x1 - 4);

            StockCellars();
            StreetFurniture(x0 + GateW, x1);
        }

        private static int GranRow(int n) => n * GranW + (n - 1) * GranGap;

        // ------------------------------------------------------------ the terrace

        /// <summary>
        /// The street, three rows of stone, and the cellars under it: vaulted bays fourteen
        /// wide on piers two wide, each pier with a doorway through it, so the whole length
        /// can be walked below the town.
        /// </summary>
        private void Terrace(int x0, int x1)
        {
            Box(x0, Street, x1, -1, TileID.MarbleBlock, White);
            Box(x0, Street, x1, Street, TileID.StoneSlab, White);

            const int bay = 14;
            int first = x0 + 3;
            int count = (x1 - 3 - first + 1) / bay;

            for (int i = 0; i < count; i++)
            {
                int a0 = first + i * bay, a1 = a0 + bay - 3;
                Room r = Vaulted(a0, a1, 0, -4, 4, WallID.StoneSlab, NoPaint);
                Registers(new Room(a0, a1, -4, 0), Scheme.Store);

                if (i + 1 < count)
                    Hollow(a1 + 1, -3, a1 + 2, -1, WallID.StoneSlab, NoPaint);

                HangAt((a0 + a1) / 2, -2, TileID.HangingLanterns, Suite.Marble.Lantern);
                cellars.Add((new Room(a0, a1, -7, 0), Above.Street));
            }
        }

        private void MarkCellars(int x0, int x1, Above what)
        {
            for (int i = 0; i < cellars.Count; i++)
            {
                var (r, _) = cellars[i];
                int mid = r.Mid;
                if (mid >= x0 && mid <= x1)
                    cellars[i] = (r, what);
            }
        }

        /// <summary>Each cellar holds what the house over it kept.</summary>
        private void StockCellars()
        {
            foreach (var (r, above) in cellars)
            {
                if (c.TypeAt(r.Mid, r.Stand) >= 0 || !c.Solid(r.Mid, r.Floor))
                    continue;

                switch (above)
                {
                    case Above.Granary:
                        // Grain in heaps against the ends, measures between them, a bin of the dole.
                        Box(r.X0, r.Stand - 1, r.X0 + 2, r.Stand, TileID.HayBlock);
                        Cut(r.X0 + 2, r.Stand - 1, Grace.Cut.DownRight);
                        Box(r.X1 - 2, r.Stand - 1, r.X1, r.Stand, TileID.HayBlock);
                        Cut(r.X1 - 2, r.Stand - 1, Grace.Cut.DownLeft);
                        Along(new Room(r.X0 + 4, r.X1 - 4, r.Top, r.Floor), 0, 1, 1,
                            (TileID.FishingCrate, 0, 1), (TileID.Kegs, 0, 1));
                        if (c.OneIn(2))
                            StashSomewhere(new Room(r.X0 + 3, r.X1 - 3, r.Top, r.Floor), Loot.Store, Suite.Wood);
                        break;

                    case Above.Market:
                        stashKind = Loot.Store;
                        stashSuite = Suite.Wood;
                        Along(new Room(r.X0 + 1, r.X1 - 1, r.Top, r.Floor), 0, 0, 1,
                            (TileID.Kegs, 0, 1), (TileID.Kegs, 0, 1), (TileID.FishingCrate, 0, 1),
                            (TileID.Containers, Suite.Wood.Chest, 1), (TileID.Kegs, 0, 1));
                        break;

                    case Above.Alms:
                        stashKind = Loot.Home;
                        stashSuite = Suite.Wood;
                        Along(new Room(r.X0 + 1, r.X1 - 1, r.Top, r.Floor), 0, 1, 2,
                            (TileID.Containers, 0, 1),
                            (TileID.WorkBenches, 0, 1), (TileID.Kegs, 0, 1), (TileID.CookingPots, 0, 1),
                            (TileID.FishingCrate, 0, 1));
                        break;

                    case Above.Gate:
                        Furnish(r, Theme.Armoury, Suite.Marble);
                        break;

                    default:
                        Along(new Room(r.X0 + 2, r.X1 - 2, r.Top, r.Floor), 0, 2, 4,
                            (TileID.FishingCrate, 0, 1), (TileID.Kegs, 0, 1));
                        break;
                }

                Neglect(r, 2, 0);
            }
        }

        // ------------------------------------------------------------ the gate

        /// <summary>
        /// The gate of the Low City at the end of the Processional: a round arch the width of
        /// the road between two square towers, a walk over the arch, and over the crown of
        /// the arch the watch tower, the tallest thing in the town, a needle of white over an
        /// open lookout.
        /// </summary>
        private void LowGate(int g0)
        {
            const int flank = 12;
            int g1 = g0 + GateW - 1;
            int a0 = g0 + flank, a1 = g1 - flank;          // the arch, twenty wide
            int walk = Street - 26;                         // the walk over the arch
            int flankTop = Street - 50;

            // The arch: its opening, then a ring of white two deep and a ring of gold round
            // the head, then the spandrels hollowed to a wall of white so they are not a
            // dark mass, framed in gold.
            int spring = Street - 14, rise = 8;
            Box(a0, walk, a1, Street - 1, TileID.MarbleBlock, White);
            ArchOpening(a0, a1, Street - 1, spring, rise, WallID.StoneSlab, NoPaint);

            for (int x = a0; x <= a1; x++)
            {
                int head = HeadTop(x, a0, a1, spring, rise);
                c.Block(x, head - 1, TileID.MarbleBlock, White);
                c.Block(x, head - 2, TileID.MarbleBlock, White);
                c.Block(x, head - 3, TileID.AncientGoldBrick, NoPaint);

                if (head - 4 > walk + 2)
                {
                    Hollow(x, walk + 2, x, head - 4, WallID.MarbleBlock, White);
                    Back(x, walk + 2, x, walk + 2, WallID.AncientGoldBrickWall, NoPaint);
                }
            }

            // In the vault of the gate: a gold line at the springing, and the portcullis drawn up
            // into the head of the arch, its bars hanging clear of the road.
            for (int x = a0; x <= a1; x++)
                if (c.TypeAt(x, spring) < 0)
                    c.Wall(x, spring, WallID.AncientGoldBrickWall, NoPaint);
            for (int x = a0 + 1; x <= a1 - 1; x += 2)
            {
                int head = HeadTop(x, a0, a1, spring, rise);
                for (int y = head; y <= spring - 2; y++)
                    if (c.TypeAt(x, y) < 0)
                        c.Block(x, y, TileID.Chain);
            }

            // The keystone: gold, standing proud of the ring.
            int kx = (a0 + a1) / 2;
            int crown = HeadTop(kx, a0, a1, spring, rise);
            Box(kx - 1, crown - 4, kx + 1, crown - 1, TileID.AncientGoldBrick, NoPaint);
            Cut(kx - 1, crown - 1, Grace.Cut.UpLeft);
            Cut(kx + 1, crown - 1, Grace.Cut.UpRight);

            // The walk over the arch: slab, a gold line under it, a railing drawn behind.
            Box(a0, walk, a1, walk, TileID.StoneSlab, White);
            Box(a0, walk + 1, a1, walk + 1, TileID.AncientGoldBrick, NoPaint);
            Back(a0, walk - 2, a1, walk - 1, WallID.WroughtIronFence, NoPaint);

            // Banners down the jambs.
            Hang(a0, HeadTop(a0, a0, a1, spring, rise), TileID.Banners, 3);
            Hang(a1, HeadTop(a1, a0, a1, spring, rise), TileID.Banners, 3);

            // The two towers. Their storeys fall on the street and on the walk, so a door
            // from each tower opens onto the walk.
            Tower(g0, a0 - 1, Street, flankTop, Roof.Battlements, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Marble, storey: 13, doorWest: true, doorEast: true, decor: Decor.Military);
            Tower(a1 + 1, g1, Street, flankTop, Roof.Battlements, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Marble, storey: 13, doorWest: true, doorEast: true, decor: Decor.Military);

            DoorOnto(a0 - 2, a0 - 1, walk, a0 - 1);
            DoorOnto(a1 + 1, a1 + 2, walk, a1 + 1);

            // Guard rooms in the towers' feet.
            Furnish(new Room(g0 + 2, a0 - 3, Street - 12, Street), Theme.Guard, Suite.Marble);
            Furnish(new Room(a1 + 3, g1 - 2, Street - 12, Street), Theme.Guard, Suite.Marble);

            // The watch tower, standing on the walk over the crown.
            Belvedere(kx - 6, kx + 6, walk, Street - 126);

            MarkCellars(g0, g1, Above.Gate);
        }

        /// <summary>
        /// A doorway through a wall at floor row <paramref name="floor"/>: every column from
        /// <paramref name="x0"/> to <paramref name="x1"/> cleared door-high, the door itself in
        /// column <paramref name="doorX"/>.
        /// </summary>
        private void DoorOnto(int x0, int x1, int floor, int doorX)
        {
            Air(x0, floor - 3, x1, floor - 1);
            Reveal(x0, floor - 3, x1, floor - 1);
            c.Place(doorX, floor - 1, TileID.ClosedDoor, Suite.Marble.Door);
        }

        /// <summary>
        /// A watch tower in three stages: a broad foot, a shaft of storeys with a window in
        /// every one, an open belfry with a balcony round it, then a drum, a gold dome and a
        /// gold needle. [x0, x1] is the shaft and should be odd in width; <paramref name="top"/>
        /// is the top of the drum.
        /// </summary>
        private void Belvedere(int x0, int x1, int baseY, int top)
        {
            int cx = (x0 + x1) / 2;
            int lookFloor = top + 20;
            int belfryTop = lookFloor - 14;
            int s1 = baseY - 36;

            // The shaft, all its storeys, up to the belfry floor.
            Tower(x0, x1, baseY, lookFloor, Roof.Flat, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Marble, storey: 12, doorWest: true, doorEast: true, decor: Decor.Military);

            // The foot: a column more on each face, weathered back at the top.
            Box(x0 - 1, s1, x0 - 1, baseY - 1, TileID.MarbleBlock, White);
            Box(x1 + 1, s1, x1 + 1, baseY - 1, TileID.MarbleBlock, White);
            Cut(x0 - 1, s1, Grace.Cut.DownLeft);
            Cut(x1 + 1, s1, Grace.Cut.DownRight);
            Air(x0 - 1, baseY - 3, x0 - 1, baseY - 1);

            // String courses at every floor above the foot.
            for (int y = s1 - 12; y > lookFloor + 2; y -= 12)
                Course(x0, x1, y, TileID.MarbleBlock, White);

            // The belfry: open slots through both faces, the back wall open in a round arch
            // so the sky shows through, a balcony round it on a gold course.
            Box(x0, belfryTop, x1, lookFloor - 1, TileID.MarbleBlock, White);
            Hollow(x0 + 2, belfryTop + 2, x1 - 2, lookFloor - 1, WallID.MarbleBlock, White);
            Air(x0, lookFloor - 9, x0 + 1, lookFloor - 1);
            Air(x1 - 1, lookFloor - 9, x1, lookFloor - 1);
            foreach (int sx in new[] { x0, x0 + 1, x1 - 1, x1 })
                for (int y = lookFloor - 9; y <= lookFloor - 1; y++)
                    c.Wall(sx, y, y >= lookFloor - 2 ? WallID.WroughtIronFence : WallID.Glass, NoPaint);
            for (int x = cx - 3; x <= cx + 3; x++)
            {
                int head = HeadTop(x, cx - 3, cx + 3, lookFloor - 8, 3);
                for (int y = head; y <= lookFloor - 1; y++)
                    c.Wall(x, y, WallID.Glass, NoPaint);
            }
            Back(x0 + 2, lookFloor - 2, x1 - 2, lookFloor - 1, WallID.WroughtIronFence, NoPaint);

            Box(x0 - 2, lookFloor, x1 + 2, lookFloor, TileID.StoneSlab, White);
            Cut(x0 - 2, lookFloor, Grace.Cut.UpLeft);
            Cut(x1 + 2, lookFloor, Grace.Cut.UpRight);
            Course(x0 - 1, x1 + 1, lookFloor + 1, TileID.AncientGoldBrick, NoPaint, 0);
            Back(x0 - 2, lookFloor - 2, x0 - 1, lookFloor - 1, WallID.WroughtIronFence, NoPaint);
            Back(x1 + 1, lookFloor - 2, x1 + 2, lookFloor - 1, WallID.WroughtIronFence, NoPaint);

            // A hatch up into it from the room below, rungs to the hatch.
            for (int x = x0 + 2; x <= x0 + 4; x++)
            {
                c.Erase(x, lookFloor + 1);
                c.Platform(x, lookFloor, Suite.Marble.Platform);
                for (int y = lookFloor + 3; y <= lookFloor + 9; y += 3)
                    if (c.TypeAt(x, y) < 0)
                        c.Platform(x, y, Suite.Marble.Platform);
            }

            // The watch fire, and a lamp over it.
            c.Place(cx - 1, lookFloor - 1, TileID.Campfire, 0);
            Hang(cx, belfryTop + 2, TileID.HangingLanterns, Suite.Gold.Lantern);

            // Cornice, drum, dome, needle.
            Cornice(x0, x1, belfryTop);
            Box(cx - 4, top, cx + 4, belfryTop - 2, TileID.MarbleBlock, White);
            Hollow(cx - 3, top + 1, cx + 3, belfryTop - 2, WallID.MarbleBlock, White);
            Glaze(cx - 4, top + 2, cx - 4, belfryTop - 3);
            Glaze(cx + 4, top + 2, cx + 4, belfryTop - 3);
            Box(cx - 5, top, cx + 5, top, TileID.AncientGoldBrick, NoPaint);
            Dome(cx, top - 1, 5, 6, 1, TileID.GoldBrick, NoPaint, WallID.GoldBrick, NoPaint);
            Spire(cx, top - 7, 3, 9, TileID.GoldBrick, NoPaint);
        }

        // ------------------------------------------------------------ squares

        /// <summary>A fountain in a square: a basin a step high with water standing in it and the spout in the middle.</summary>
        private void Fountain(int x, int floor = Street)
        {
            int y = floor - 1;
            Box(x - 4, y, x + 5, y, TileID.MarbleBlock, White);
            Cut(x - 4, y, Grace.Cut.DownLeft);
            Cut(x + 5, y, Grace.Cut.DownRight);
            c.Place(x, y - 1, TileID.WaterFountain, 0);
        }

        // ------------------------------------------------------------ the granaries

        /// <summary>
        /// A granary: a tall stone bin eleven wide, golden sandstone banded with white, full to
        /// two thirds with grain and domed over. Its door is at the foot; the lintel over it
        /// may carry a line.
        /// </summary>
        private void Granary(int x0, int baseY, int h, string lintel)
        {
            int x1 = x0 + GranW - 1;
            int cx = x0 + GranW / 2;
            int top = baseY - h;

            for (int x = x0; x <= x1; x++)
                for (int y = top; y < baseY; y++)
                {
                    bool band = (baseY - y) % 12 == 0;
                    c.Block(x, y, band ? TileID.MarbleBlock : TileID.SandstoneBrick, band ? White : NoPaint);
                }

            // The bands stand proud: every white band is a course.
            for (int y = baseY - 12; y > top + 2; y -= 12)
                Course(x0, x1, y, TileID.MarbleBlock, White);

            // The bin. In section the grain is a wall of hay to two thirds of the height, its
            // top a crust heaped to the middle; over it air, a lamp, and slits in both faces
            // that let the day down onto it.
            int fill = baseY - (h * 2) / 3;
            Hollow(x0 + 2, top + 2, x1 - 2, baseY - 6, WallID.SandstoneBrick, NoPaint);
            Back(x0 + 2, fill, x1 - 2, baseY - 6, WallID.Hay, NoPaint);
            for (int x = x0 + 2; x <= x1 - 2; x++)
            {
                int d = Math.Abs(x - cx);
                int crest = fill - (d <= 1 ? 2 : d <= 2 ? 1 : 0);
                Box(x, crest, x, fill + 1, TileID.HayBlock, NoPaint);
            }
            Cut(cx - 2, fill - 1, Grace.Cut.DownLeft);
            Cut(cx + 2, fill - 1, Grace.Cut.DownRight);
            Cut(cx - 1, fill - 2, Grace.Cut.DownLeft);
            Cut(cx + 1, fill - 2, Grace.Cut.DownRight);
            // Vents: two rows high, too tight to climb through, a grille behind them.
            for (int y = baseY - 9; y > top + 4; y -= 6)
            {
                foreach (int vx in new[] { x0, x0 + 1, x1 - 1, x1 })
                    for (int yy = y - 1; yy <= y; yy++)
                    {
                        c.Erase(vx, yy);
                        c.Wall(vx, yy, WallID.WroughtIronFence, NoPaint);
                    }
            }
            Hang(cx, top + 2, TileID.HangingLanterns, Suite.Marble.Lantern);

            // The loading room at the foot.
            Hollow(x0 + 2, baseY - 4, x1 - 2, baseY - 1, WallID.SandstoneBrick, NoPaint);
            Box(x0 + 2, baseY - 5, x1 - 2, baseY - 5, TileID.MarbleBlock, White);
            Air(x0, baseY - 3, x0 + 1, baseY - 1);
            Reveal(x0, baseY - 3, x0 + 1, baseY - 1);
            c.Place(x0, baseY - 1, TileID.ClosedDoor, Suite.Marble.Door);
            Air(x1 - 1, baseY - 3, x1, baseY - 1);
            Reveal(x1 - 1, baseY - 3, x1, baseY - 1);
            c.Place(x1, baseY - 1, TileID.ClosedDoor, Suite.Marble.Door);
            c.Place(x0 + 4, baseY - 1, TileID.Kegs, 0);
            c.Place(x0 + 6, baseY - 1, TileID.FishingCrate, 0);
            Hang(x1 - 3, baseY - 4, TileID.HangingLanterns, Suite.Marble.Lantern);

            if (lintel != null)
                c.Sign(x0 + 2, baseY - 3, TileID.Signs, 0, lintel);

            // Cap: gold under a white dome with a gold knob.
            Box(x0 - 1, top, x1 + 1, top, TileID.AncientGoldBrick, NoPaint);
            Dome(cx, top - 1, 6, 6, 2, TileID.MarbleBlock, White, WallID.SandstoneBrick, NoPaint);
            Spire(cx, top - 7, 3, 4, TileID.GoldBrick, NoPaint);
        }

        // ------------------------------------------------------------ the market

        /// <summary>
        /// The market hall. One long vaulted room over the street, stalls down both sides of
        /// the floor under tall windows, the office of weights at the west end with the table
        /// of prices on its wall, and at the east end a door in the floor.
        /// </summary>
        private void Market(int m0, int m1)
        {
            int eave = Street - 25;
            Box(m0, eave, m1, Street - 1, TileID.MarbleBlock, White);

            // The hall.
            Room hall = Vaulted(m0 + 3, m1 - 3, Street, Street - 18, 6, WallID.MarbleBlock, White);
            Panelled(new Room(hall.X0, hall.X1, Street - 19, Street), WallID.MarbleBlock, White, 9);

            // Doors at both ends.
            DoorOnto(m0, m0 + 2, Street, m0);
            DoorOnto(m1 - 2, m1, Street, m1);

            // The office of weights: a partition at the west end with a counter.
            int office = hall.X0 + 11;
            Box(office, Street - 9, office, Street - 4, TileID.MarbleBlock, White);
            Box(hall.X0, Street - 9, office, Street - 9, TileID.StoneSlab, White);
            Back(hall.X0, Street - 8, office - 1, Street - 1, WallID.WhiteDynasty, NoPaint);
            c.Place(hall.X0 + 1, Street - 1, TileID.Bookcases, Suite.Marble.Bookcase);
            c.Place(hall.X0 + 5, Street - 1, TileID.Chairs, Suite.Marble.Chair, 1);
            c.Place(hall.X0 + 6, Street - 1, TileID.Tables, Suite.Marble.Table);
            c.Place(hall.X0 + 6, Street - 3, TileID.MetalBars, 3);
            c.Place(hall.X0 + 7, Street - 3, TileID.MetalBars, 1);
            c.Place(hall.X0 + 8, Street - 3, TileID.CopperCoinPile, 0);
            Hang(hall.X0 + 4, Street - 8, TileID.HangingLanterns, Suite.Marble.Lantern);
            c.Sign(hall.X0 + 8, Street - 6, TileID.Signs, 0, MarketText.Prices);
            Adorn(new Room(hall.X0, office - 1, Street - 8, Street), Decor.Scholar, 4, 4);

            // Stalls down the floor.
            int x = office + 3;
            int k = 0;
            while (x + 8 <= hall.X1 - 8)
            {
                Stall(x, Street - 1, k++);
                x += 10;
            }

            // Chandeliers down the crown of the vault.
            for (int cx = hall.X0 + 16; cx <= hall.X1 - 12; cx += 18)
                HangAt(cx - 1, Street - 22, TileID.Chandeliers, Suite.Marble.Chandelier);

            // The door in the floor. It opens; there is stone a little way under it, and the
            // stone was set from the other side.
            int tx = hall.X1 - 5;
            MarketTrapdoor(tx);

            // The roof: a long low pitch in white, gold on the ridge, a gold lantern at its middle.
            Cornice(m0, m1, eave);
            int ridge = Pitch2(m0 - 1, m1 + 1, eave - 2, 5, TileID.MarbleBlock, White, WallID.MarbleBlock, White);
            int mid = (m0 + m1) / 2;

            // Round lights in the attic, so the roof space is lit by day.
            for (int ox = m0 + 10; ox <= m1 - 10; ox += 12)
            {
                int oy = eave - 4;
                if (c.TypeAt(ox, oy) < 0 && c.TypeAt(ox, oy - 1) < 0)
                    BackArch(ox - 1, ox + 1, oy + 1, oy, 1, WallID.Glass);
            }

            Box(mid - 3, ridge - 4, mid + 3, ridge - 1, TileID.MarbleBlock, White);
            Hollow(mid - 2, ridge - 4, mid + 2, ridge - 1, WallID.Glass, NoPaint);
            Dome(mid, ridge - 5, 4, 4, 1, TileID.GoldBrick, NoPaint, WallID.GoldBrick, NoPaint);
            Spire(mid, ridge - 9, 3, 4, TileID.GoldBrick, NoPaint);
        }

        /// <summary>
        /// A pitched roof in two courses resting on <paramref name="eave"/>, rising one row
        /// every <paramref name="run"/> columns, gold along the ridge, a wall under it. Returns
        /// the ridge row.
        /// </summary>
        private int Pitch2(int x0, int x1, int eave, int run, ushort type, byte paint, ushort under = WallID.Wood, byte underPaint = NoPaint)
        {
            int mid = (x0 + x1) / 2;
            int ridge = eave;

            for (int x = x0; x <= x1; x++)
            {
                int d = Math.Min(x - x0, x1 - x);
                int t = eave - d / run;
                ridge = Math.Min(ridge, t);

                c.Block(x, t, type, paint);
                c.Block(x, t + 1, type, paint);
                for (int y = t + 2; y <= eave; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, under, underPaint);
                }
            }

            for (int x = x0; x <= x1; x++)
            {
                int d = Math.Min(x - x0, x1 - x);
                int t = eave - d / run;
                if (t == ridge)
                    c.Block(x, t, TileID.AncientGoldBrick, NoPaint);
            }

            return ridge;
        }

        /// <summary>A market stall: a counter with goods on it under a striped awning on two posts.</summary>
        private void Stall(int x, int stand, int k)
        {
            ushort a = k % 2 == 0 ? TileID.RedStucco : TileID.YellowStucco;
            int awning = stand - 5;

            for (int i = 0; i < 8; i++)
            {
                ushort t = i % 2 == 0 ? a : TileID.SnowBrick;
                c.Block(x + i, awning, t, NoPaint, Grace.Cut.Half);
            }

            for (int y = awning + 1; y <= stand; y++)
            {
                if (c.TypeAt(x, y) < 0)
                    c.Block(x, y, TileID.WoodenBeam);
                if (c.TypeAt(x + 7, y) < 0)
                    c.Block(x + 7, y, TileID.WoodenBeam);
            }

            c.Place(x + 2, stand, TileID.Tables, Suite.Wood.Table);
            int top = stand - 2;
            switch (k % 4)
            {
                case 0:
                    c.Place(x + 2, top, TileID.Bowls, 0);
                    c.Place(x + 3, top, TileID.Bowls, 0);
                    c.Place(x + 4, top, TileID.Bowls, 0);
                    c.Place(x + 5, stand, TileID.Kegs, 0);
                    break;
                case 1:
                    c.Place(x + 2, top, TileID.Bottles, 0);
                    c.Place(x + 3, top, TileID.Bottles, 1);
                    c.Place(x + 4, top, TileID.Bottles, 2);
                    c.Place(x + 5, stand, TileID.FishingCrate, 0);
                    break;
                case 2:
                    c.Place(x + 3, top, TileID.Books, c.Next(0, 5));
                    c.Place(x + 4, top, TileID.Candles, Suite.Wood.Candle);
                    Stash(x + 5, stand, Loot.Store, Suite.Wood);
                    break;
                default:
                    c.Place(x + 2, top, TileID.MetalBars, 0);
                    c.Place(x + 3, top, TileID.MetalBars, 2);
                    c.Place(x + 5, stand, TileID.Kegs, 0);
                    break;
            }
        }

        /// <summary>
        /// The door in the floor at the east end of the market hall. Under it a shaft goes
        /// straight down through the terrace and the ground toward the Undercity, walled off
        /// from the cellars on either side, rungs all the way, and at the bottom a plug of
        /// stone set from underneath.
        /// </summary>
        /// <summary>The first column of the market shaft, for the Undercity's lock.</summary>
        private int marketPlugX;

        private void MarketTrapdoor(int tx)
        {
            const int plug = 56;
            marketPlugX = tx;

            Box(tx - 1, Street + 1, tx + 2, plug + 3, TileID.MarbleBlock, White);
            Hollow(tx, Street + 1, tx + 1, plug - 1, WallID.StoneSlab, NoPaint);

            for (int y = Street + 3; y < plug - 1; y += 3)
            {
                c.Platform(tx, y, Suite.Marble.Platform);
                c.Platform(tx + 1, y, Suite.Marble.Platform);
            }

            for (int y = Street + 4; y < plug - 3; y += 12)
                Hang(tx, y, TileID.HangingLanterns, Suite.Marble.Lantern);

            Box(tx, plug, tx + 1, plug + 2, TileID.StoneSlab, NoPaint);

            c.Erase(tx, Street);
            c.Erase(tx + 1, Street);
            c.Place(tx, Street, TileID.TrapdoorClosed, 0);
            c.Mark("Grace.MarketTrapdoor", tx, Street, 2, 1);
            c.Mark("Grace.MarketPlug", tx, plug, 2, 3);
        }

        /// <summary>
        /// A way down from the street into the cellars: the paving opened three wide and
        /// bridged with platforms, rungs down to the cellar floor.
        /// </summary>
        private void CellarHatch(int x)
        {
            for (int xx = x; xx <= x + 2; xx++)
            {
                for (int y = Street; y <= -1; y++)
                {
                    c.Erase(xx, y);
                    c.Wall(xx, y, WallID.StoneSlab, NoPaint);
                }

                c.Platform(xx, Street, Suite.Marble.Platform);
                for (int y = Street + 3; y <= -3; y += 3)
                    c.Platform(xx, y, Suite.Marble.Platform);
            }
        }

        // ------------------------------------------------------------ the almshouses

        /// <summary>
        /// An almshouse: three storeys of warm plaster between white quoins, glass in every
        /// window, a hearth and a kitchen on the ground floor and bunks above, a red roof.
        /// </summary>
        private void Almshouse(int x0, int x1, int k)
        {
            const int storey = 8;
            int storeys = 3;
            int top = Street - storeys * storey - 1;

            Box(x0, top, x1, Street - 1, TileID.YellowStucco, NoPaint);
            Box(x0, top, x0, Street - 1, TileID.MarbleBlock, White);
            Box(x1, top, x1, Street - 1, TileID.MarbleBlock, White);

            for (int s = 0; s < storeys; s++)
            {
                int floor = Street - s * storey;
                var r = new Room(x0 + 2, x1 - 2, floor - storey + 1, floor);
                Hollow(r.X0, r.Top, r.X1, floor - 1, WallID.Wood, NoPaint);

                // Two rows of stone along the floor, a rail of planks, wood above, planks
                // under the ceiling.
                Registers(r, Scheme.Home);

                // Windows: two in the back wall, glazed and framed, standing on the rail.
                Window(r.X0 + 3, r.X0 + 5, floor - 4, floor - 5, 1, WallID.PearlstoneBrick, White);
                Window(r.X1 - 5, r.X1 - 3, floor - 4, floor - 5, 1, WallID.PearlstoneBrick, White);

                if (s > 0)
                {
                    Box(r.X0, floor, r.X1, floor, TileID.WoodBlock, NoPaint);
                    int hx = s % 2 == 1 ? r.X1 - 2 : r.X0;
                    for (int x = hx; x <= hx + 2; x++)
                        c.Platform(x, floor, Suite.Wood.Platform);
                    for (int y = floor + 3; y < floor + storey; y += 3)
                        for (int x = hx; x <= hx + 2; x++)
                            if (c.TypeAt(x, y) < 0)
                                c.Platform(x, y, Suite.Wood.Platform);
                }

                if (s == 0)
                {
                    c.Place(r.X0 + 1, r.Stand, TileID.Fireplace, 0);
                    c.Place(r.X0 + 5, r.Stand, TileID.CookingPots, 0);
                    c.Place(r.X0 + 8, r.Stand, TileID.Tables, Suite.Wood.Table);
                    c.Place(r.X0 + 8, r.Stand - 2, TileID.Bowls, 0);
                    c.Place(r.X0 + 11, r.Stand, TileID.Chairs, Suite.Wood.Chair, -1);
                    Hang(r.Mid, r.Top, TileID.HangingLanterns, Suite.Wood.Lantern);
                }
                else
                {
                    int x = r.X0 + 3;
                    int end = r.X1 - 3;
                    while (x + 3 <= end)
                    {
                        c.Place(x, r.Stand, TileID.Beds, Suite.Wood.Bed, (x / 4) % 2 == 0 ? 1 : -1);
                        x += 5;
                    }
                    Hang(r.Mid, r.Top, TileID.HangingLanterns, Suite.Wood.Lantern);
                }

                Neglect(r, 1, 0);
                if (c.OneIn(2))
                    StashSomewhere(r, Loot.Home, Suite.Wood);
                Adorn(r, Decor.Home, 7, 4);
                HangingPot(s % 2 == 0 ? r.X1 - 4 : r.X0 + 3, r.Top + 3);
            }

            // The door, west side.
            Air(x0, Street - 3, x0 + 1, Street - 1);
            Reveal(x0, Street - 3, x0 + 1, Street - 1);
            c.Place(x0, Street - 1, TileID.ClosedDoor, Suite.Wood.Door);
            Air(x1 - 1, Street - 3, x1, Street - 1);
            Reveal(x1 - 1, Street - 3, x1, Street - 1);
            c.Place(x1, Street - 1, TileID.ClosedDoor, Suite.Wood.Door);

            // Eaves and a red roof; a chimney.
            Course(x0, x1, top, TileID.MarbleBlock, White);
            Pitch(x0 - 2, x1 + 2, top - 1, 2, TileID.RedDynastyShingles, NoPaint, WallID.Wood, NoPaint);
            int ch = k % 2 == 0 ? x0 + 3 : x1 - 4;
            Box(ch, top - 9, ch + 1, top - 1, TileID.RedBrick, NoPaint);
        }

        // ------------------------------------------------------------ the waterworks

        /// <summary>
        /// The waterworks: a yard, the cistern tower with its tank at the top under a copper
        /// roof, and the aqueduct coming in from the Ward at the height of the tank.
        /// </summary>
        private void Waterworks(int w0, int w1, int endX)
        {
            int t0 = w1 - 15, t1 = w1;
            int top = Street - 58;

            // The tower: storeys with the pumps, a stair of platforms, the tank on top.
            Tower(t0, t1, Street, top, Roof.Flat, TileID.MarbleBlock, White, WallID.StoneSlab, White,
                Suite.Marble, storey: 12, doorWest: true, doorEast: true);

            // Copper bands where the pipes run.
            for (int y = Street - 6; y > top; y -= 12)
                Course(t0, t1, y, TileID.CopperPlating, NoPaint);

            // The tank.
            int tank = top - 1;
            Box(t0 - 1, tank - 8, t1 + 1, tank, TileID.MarbleBlock, White);
            Box(t0 + 1, tank - 7, t1 - 1, tank - 1, TileID.CopperPlating, NoPaint);
            for (int x = t0 + 2; x <= t1 - 2; x++)
                for (int y = tank - 6; y <= tank - 1; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.CopperPlating, NoPaint);
                    if (y >= tank - 4)
                        c.Liquid(x, y, 255, 0);
                }

            // Roof: a copper pitch with a gold finial.
            int ridge = Pitch2(t0 - 2, t1 + 2, tank - 9, 2, TileID.CopperPlating, NoPaint);
            Spire((t0 + t1) / 2, ridge - 1, 3, 5, TileID.GoldBrick, NoPaint);

            // The aqueduct: from the tank east over the gap to the Ward, on piers.
            int channel = tank - 5;
            for (int x = t1 + 2; x <= endX; x++)
            {
                c.Block(x, channel + 1, TileID.MarbleBlock, White);
                c.Block(x, channel + 2, TileID.AncientGoldBrick, NoPaint);
                c.Block(x, channel - 2, TileID.MarbleBlock, White, Grace.Cut.Half);
                c.Erase(x, channel);
                c.Erase(x, channel - 1);
                c.Liquid(x, channel, 255, 0);
            }
            for (int x = t1 + 1; x <= t1 + 1; x++)
            {
                c.Erase(x, channel);
                c.Liquid(x, channel, 255, 0);
            }

            // Under it, two tiers of arches between the tower and the Ward: the street runs
            // through the lower one, the upper one is blind, a panel of grey in the white.
            Box(t1 + 1, channel + 3, endX, Street - 1, TileID.MarbleBlock, White);
            ArchOpening(t1 + 1, endX, Street - 1, Street - 17, 5, WallID.StoneSlab, NoPaint);
            Course(t1 + 1, endX, Street - 25, TileID.AncientGoldBrick, NoPaint, 0);
            ArchOpening(t1 + 3, endX - 2, Street - 30, Street - 50, 3, WallID.StoneSlab, NoPaint);
            Box(t1 + 3, Street - 30, endX - 2, Street - 30, TileID.MarbleBlock, White);

            // The yard: a standpipe and a trough.
            c.Place(w0 + 4, Street - 1, TileID.WaterFountain, 0);
            Box(w0 + 8, Street - 2, w0 + 16, Street - 1, TileID.MarbleBlock, White);
            for (int x = w0 + 9; x <= w0 + 15; x++)
            {
                c.Erase(x, Street - 2);
                c.Liquid(x, Street - 2, 255, 0);
            }
        }

        // ------------------------------------------------------------ the cistern

        /// <summary>
        /// The cistern under the east end of the town: one vaulted chamber as deep again as the
        /// terrace, a forest of white columns standing in black water, a walk round it on
        /// platforms, lamps. The waterworks' stair comes down into it.
        /// </summary>
        private void Cistern(int x0, int x1)
        {
            if (x1 - x0 < 30)
                return;

            // The chamber is cut to a whole number of bays, so the columns stand the same
            // distance in from both end walls.
            x1 = x0 + 6 + (x1 - x0 - 6) / 5 * 5;

            int ceil = Street + 3;       // first open row
            int floor = 24;              // the floor under the water
            int water = 8;               // the surface

            // Solid round it, then the chamber.
            Box(x0 - 3, ceil - 1, x1 + 3, floor + 3, TileID.MarbleBlock, White);
            Hollow(x0, ceil, x1, floor - 1, WallID.StoneSlab, NoPaint);
            Back(x0, water, x1, floor - 1, WallID.GrayBrick, NoPaint);

            for (int x = x0; x <= x1; x++)
                for (int y = water; y < floor; y++)
                    c.Liquid(x, y, 255, 0);

            // The columns: from the floor to the ceiling, every five, capitals of gold.
            for (int x = x0 + 3; x <= x1 - 3; x += 5)
            {
                Column(x, ceil + 1, floor - 1);
                c.Block(x, ceil, TileID.AncientGoldBrick, NoPaint);
            }

            // Vaults between the columns: the ceiling dips between each pair.
            for (int x = x0 + 3; x + 5 <= x1 - 3; x += 5)
            {
                c.Block(x + 1, ceil, TileID.MarbleBlock, White, Grace.Cut.UpRight);
                c.Block(x + 4, ceil, TileID.MarbleBlock, White, Grace.Cut.UpLeft);
            }

            // The walk: platforms along the west end and a landing at the east end, just over the water.
            for (int x = x0; x <= x0 + 6; x++)
                c.Platform(x, water - 1, Suite.Marble.Platform);
            for (int x = x1 - 6; x <= x1; x++)
                c.Platform(x, water - 1, Suite.Marble.Platform);

            for (int x = x0 + 3; x + 5 <= x1 - 3; x += 10)
                Hang(x + 2, ceil, TileID.HangingLanterns, Suite.Marble.Lantern);

            // Lamps on the wall at the waterline, one in every bay, doubled in the water.
            for (int x = x0 + 5; x <= x1 - 3; x += 5)
                c.Place(x, water - 2, TileID.Torches, 0);

            // Down from the town: a stair of platforms at the east end, through the street,
            // in the room between the last column and the end wall.
            int sx = x1 - 1;
            for (int y = Street; y < water - 1; y++)
            {
                c.Erase(sx, y);
                c.Erase(sx + 1, y);
                c.Wall(sx, y, WallID.StoneSlab, NoPaint);
                c.Wall(sx + 1, y, WallID.StoneSlab, NoPaint);
            }
            for (int y = Street; y < water - 1; y += 3)
            {
                c.Platform(sx, y, Suite.Marble.Platform);
                c.Platform(sx + 1, y, Suite.Marble.Platform);
            }

            c.Mark("Grace.Cistern", x0, ceil, x1 - x0 + 1, floor - ceil);
        }

        // ------------------------------------------------------------ the street

        /// <summary>
        /// Lamps along the street wherever there is open air over the paving, a bench under
        /// every other one, a standpipe between.
        /// </summary>
        private void StreetFurniture(int x0, int x1)
        {
            int since = 99;
            int n = 0;

            for (int x = x0; x <= x1; x++)
            {
                since++;
                bool open = true;
                for (int y = Street - 7; y <= Street - 1; y++)
                    for (int dx = -1; dx <= 3; dx++)
                        if (c.TypeAt(x + dx, y) >= 0)
                            open = false;

                if (!open || !c.Solid(x, Street) || since < 14)
                    continue;

                if (c.Place(x, Street - 1, TileID.Lampposts, 0))
                {
                    since = 0;
                    n++;
                    if (n % 2 == 0)
                        c.Place(x + 1, Street - 1, TileID.Benches, Suite.Marble.Bench);
                }
            }
        }
    }

    /// <summary>What is written in the market hall.</summary>
    public static class MarketText
    {
        /// <summary>The table of fair prices, on the wall of the office of weights.</summary>
        public const string Prices = "Bread, the loaf: two coppers. Oil, the jar: three. Salt, the measure: three. Grain from the granaries: nothing.";
    }
}
