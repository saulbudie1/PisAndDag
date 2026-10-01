using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The High Ward. A terrace twenty-four rows over the Low City's street, walled, and
    /// the Ward Gate shut in its west face. Behind it the courtyard, the Court of Grace
    /// under the great dome, the Princess's tower and the armoury. Under the terrace the
    /// grand stair from the gate, and the kitchens that served the table.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>
        /// The Ward's floor: the courtyard, the Court, everything on the terrace. GraceBiome
        /// reads it to find the plain's level again in a built world, under the Court's mark.
        /// </summary>
        internal const int WardFloor = -36;

        /// <summary>The Ward's wall walk, and the gallery that runs from it over the courtyard.</summary>
        private const int WardWalk = -64;

        private void HighWard()
        {
            int x0 = WardX, x1 = FontX - 1;

            int gate0 = x0, gate1 = x0 + 43;
            int court0 = x0 + 80, court1 = court0 + 199;
            int pr0 = court1 + 1, pr1 = pr0 + 21;
            int arm0 = pr1 + 1, arm1 = x1;

            // The terrace, solid, paved, and cleared above.
            for (int x = x0; x <= x1; x++)
                for (int y = -site.Top; y < WardFloor; y++)
                {
                    c.Erase(x, y);
                    c.EraseWall(x, y);
                }

            Box(x0, WardFloor, x1, -1, TileID.MarbleBlock, White);
            Box(x0, WardFloor, x1, WardFloor, TileID.StoneSlab, White);

            WardUndercroft(x0, x1, court0, court1);
            WardGate(gate0, gate1);
            Courtyard(gate1 + 1, court0 - 1);
            Court(court0, court1);
            PrincessTower(pr0, pr1);
            Armoury(arm0, arm1);
            Gallery(gate1 - 11, court0);
        }

        // ------------------------------------------------------------ the gate

        /// <summary>
        /// The Ward Gate: an arch in the retaining wall at the level of the street, shut with
        /// leaves of dark timber studded in gold and a wicket in them, between two towers, a
        /// walk over the arch. Behind the leaves the grand stair climbs through the terrace
        /// to the courtyard.
        /// </summary>
        private void WardGate(int g0, int g1)
        {
            int a0 = g0 + 12, a1 = g1 - 12;
            int spring = Street - 12, rise = 7;
            int towerTop = -104;

            // The west tower stands on the street, outside the gate; the east tower on the terrace.
            Tower(g0, a0 - 1, Street, towerTop, Roof.Dome, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Marble, storey: 13, doorWest: true, doorEast: false, decor: Decor.Military);
            Tower(a1 + 1, g1, WardFloor, towerTop, Roof.Dome, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Marble, storey: 14, doorWest: false, doorEast: true, decor: Decor.Military);

            // Between the towers: the gatehouse over the arch, up to the walk.
            Box(a0, WardWalk, a1, Street - 1, TileID.MarbleBlock, White);

            // The arch, ringed in white and gold.
            ArchOpening(a0, a1, Street - 1, spring, rise, WallID.StoneSlab, NoPaint);
            for (int x = a0; x <= a1; x++)
            {
                int head = HeadTop(x, a0, a1, spring, rise);
                c.Block(x, head - 1, TileID.MarbleBlock, White);
                c.Block(x, head - 2, TileID.AncientGoldBrick, NoPaint);
            }

            // The leaves: timber, two thick, filling the arch from sill to head at its west
            // face, studded in gold, and a wicket in them.
            for (int x = a0; x <= a0 + 1; x++)
            {
                int head = HeadTop(x, a0, a1, spring, rise);
                for (int y = head; y <= Street - 1; y++)
                {
                    bool stud = (y - head) % 4 == 2;
                    c.Block(x, y, stud ? TileID.AncientGoldBrick : TileID.DynastyWood, NoPaint);
                }
            }
            Air(a0, Street - 5, a0 + 1, Street - 1);
            c.Place(a0, Street - 1, TileID.TallGateClosed, 0);
            c.Mark("Grace.WardWicket", a0, Street - 5, 1, 5);

            // The chamber over the arch, where the gate was worked from.
            var room = new Room(a0 + 2, a1 - 2, WardWalk + 3, spring - rise - 4);
            Hollow(room.X0, room.Top, room.X1, room.Floor - 1, WallID.MarbleBlock, White);
            Panelled(room, WallID.MarbleBlock, White, 8);
            Furnish(room, Theme.Guard, Suite.Marble);
            Adorn(room, Decor.Military, 5);

            // The walk over the arch, with doors into both towers.
            Box(a0, WardWalk, a1, WardWalk + 1, TileID.StoneSlab, White);
            Box(a0, WardWalk + 2, a1, WardWalk + 2, TileID.AncientGoldBrick, NoPaint);
            Back(a0, WardWalk - 2, a1, WardWalk - 1, WallID.WroughtIronFence, NoPaint);
            DoorOnto(a0 - 2, a0 - 1, WardWalk, a0 - 1);
            DoorOnto(a1 + 1, a1 + 2, WardWalk, a1 + 1);

            // Banners down the west tower's face toward the town, each from a gold bracket.
            for (int y = Street - 20; y > towerTop + 12; y -= 26)
                BracketBanner(g0 - 1, y);

            // The grand stair: from inside the leaves up through the terrace to the courtyard.
            WardStair(a0 + 3);
        }

        /// <summary>
        /// The grand stair from the gate to the courtyard: a straight flight in half steps up
        /// through the terrace, twice as long as it is high, under a ceiling that steps up
        /// with it, and where it comes up into the courtyard the paving is open over it with a
        /// rail round the opening.
        /// </summary>
        private void WardStair(int xs)
        {
            int rise = Street - WardFloor;
            int n = rise * 2;

            for (int k = 1; k <= n; k++)
            {
                int x = xs + k - 1;
                int full = k / 2;
                int top = Street - full - (k % 2 == 1 ? 1 : 0);

                int roof = Math.Max(top - 12, WardFloor + 4);
                bool open = top - 4 < WardFloor + 4;

                for (int y = open ? WardFloor : roof; y < top; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.StoneSlab, White);
                }

                if (!open && (x - xs) % 9 == 4)
                    HangAt(x, top - 4, TileID.HangingLanterns, Suite.Marble.Lantern);

                // A gold line on the wall, running up with the stair.
                if (!open)
                    c.Wall(x, top - 7, WallID.AncientGoldBrickWall, NoPaint);
            }

            Ramp(xs, Street, rise, 1, TileID.MarbleBlock, White);

            // The flat before the first step, inside the leaves.
            Hollow(xs - 1, Street - 11, xs - 1, Street - 1, WallID.StoneSlab, White);

            // The rail round the opening in the courtyard.
            for (int k = 1; k <= n; k++)
            {
                int x = xs + k - 1;
                int full = k / 2;
                int top = Street - full - (k % 2 == 1 ? 1 : 0);
                if (top - 4 < WardFloor + 4)
                {
                    c.Wall(x, WardFloor - 1, WallID.WroughtIronFence, NoPaint);
                    c.Wall(x, WardFloor - 2, WallID.WroughtIronFence, NoPaint);
                }
            }
        }

        // ------------------------------------------------------------ the courtyard

        /// <summary>
        /// The courtyard between the gate and the Court: paved white, a fountain, benches,
        /// lamps, and two tall ash trees in stone planters where there were fruit trees once,
        /// their bark gone pale as the stone.
        /// </summary>
        private void Courtyard(int y0x, int y1x)
        {
            int stand = WardFloor - 1;

            // Planters with ash in them, and a dead tree in each.
            foreach (int px in new[] { y1x - 22, y1x - 8 })
            {
                if (px < y0x + 20)
                    continue;

                Box(px - 2, stand, px + 2, stand, TileID.MarbleBlock, White);
                Cut(px - 2, stand, Grace.Cut.DownLeft);
                Cut(px + 2, stand, Grace.Cut.DownRight);
                for (int x = px - 1; x <= px + 1; x++)
                    c.Block(x, stand, TileID.AshGrass);
                c.Tree(px, stand, TileID.TreeAsh, c.Next(18, 23), White);
            }

            Fountain(y1x - 15, WardFloor);
            Lamppost(y1x - 3, stand);
            c.Place(y1x - 12, stand, TileID.Benches, Suite.Marble.Bench);
        }

        // ------------------------------------------------------------ the gallery

        /// <summary>
        /// The high gallery: from the east tower of the gate along the top of an arcade over
        /// the courtyard to a door in the west wall of the Court, at the height of the wall
        /// walk. From it the courtyard is seen from above, which is how it should be seen first.
        /// </summary>
        private void Gallery(int from, int to)
        {
            int deck = WardWalk;

            for (int x = from + 12; x < to; x++)
            {
                c.Block(x, deck, TileID.StoneSlab, White);
                c.Block(x, deck + 1, TileID.MarbleBlock, White);
                c.Block(x, deck + 2, TileID.AncientGoldBrick, NoPaint);
                c.Wall(x, deck - 1, WallID.WroughtIronFence, NoPaint);
                c.Wall(x, deck - 2, WallID.WroughtIronFence, NoPaint);
            }

            // Corbels under the deck, every four.
            for (int x = from + 13; x < to - 1; x += 4)
            {
                c.Block(x, deck + 3, TileID.MarbleBlock, White, Grace.Cut.UpRight);
                c.Block(x + 1, deck + 3, TileID.MarbleBlock, White, Grace.Cut.UpLeft);
            }

            // Lamps along it.
            for (int x = from + 16; x < to - 2; x += 10)
                c.Place(x, deck - 1, TileID.Lamps, Suite.Marble.Lamp);
        }

        // ------------------------------------------------------------ the Court of Grace

        /// <summary>
        /// The Court of Grace. The great hall on its floor of gold, two hundred wide, the long
        /// table down the middle of it and the throne raised at the east end and smaller than
        /// the room; over the middle of the hall the crossing rises clear to the great dome.
        /// Either side of the crossing, over the hall, two storeys of the royal chambers, each
        /// wing a bedchamber on each storey, their balconies out over the crossing and a bridge
        /// between the upper ones; stairs up from the musicians' gallery, and from the upper
        /// chambers the way up into the towers at the ends of the roof.
        /// </summary>
        private void Court(int k0, int k1)
        {
            const int wall = 4;
            int top = WardFloor - 92;            // the top of the hall's masonry
            int ceil = top + 4;                  // the first open row under the ceiling
            int i0 = k0 + wall, i1 = k1 - wall;
            int mid = (k0 + k1) / 2;
            const int crossHalf = 30;
            int cw0 = mid - crossHalf, cw1 = mid + crossHalf;
            int f2 = WardFloor - 44;             // the floor of the first storey of chambers
            int f3 = f2 - 22;                    // the floor of the second

            // The shell.
            Box(k0, top, k1, WardFloor - 1, TileID.MarbleBlock, White);
            var hall = new Room(i0, i1, ceil, WardFloor);
            Hollow(i0, ceil, i1, WardFloor - 1, WallID.MarbleBlock, White);

            // The floor of gold, on a course of white.
            Box(i0, WardFloor, i1, WardFloor, TileID.GoldBrick, NoPaint);

            CourtWall(hall);

            // The crossing: the ceiling open over the middle into the drum and the dome.
            for (int x = cw0; x <= cw1; x++)
                for (int y = top; y < ceil; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.AncientGoldBrickWall, NoPaint);
                }

            // The ends of the ceiling, dressed with a gold soffit.
            Box(i0, ceil - 1, cw0 - 1, ceil - 1, TileID.AncientGoldBrick, NoPaint);
            Box(cw1 + 1, ceil - 1, i1, ceil - 1, TileID.AncientGoldBrick, NoPaint);

            // ---- the chambers' floors, their balconies over the crossing, the bridge between
            foreach (int f in new[] { f2, f3 })
            {
                Box(i0, f, cw0 + 2, f + 1, TileID.MarbleBlock, White);
                Box(cw1 - 2, f, i1, f + 1, TileID.MarbleBlock, White);
                Box(i0, f + 1, cw0 + 2, f + 1, TileID.AncientGoldBrick, NoPaint);
                Box(cw1 - 2, f + 1, i1, f + 1, TileID.AncientGoldBrick, NoPaint);
                Cut(cw0 + 2, f + 1, Grace.Cut.UpRight);
                Cut(cw1 - 2, f + 1, Grace.Cut.UpLeft);

                // Balusters along each balcony.
                for (int x = cw0; x <= cw0 + 2; x += 2)
                    Column(x, f - 1, f - 1);
                for (int x = cw1 - 2; x <= cw1; x += 2)
                    Column(x, f - 1, f - 1);
            }

            for (int x = cw0 + 3; x <= cw1 - 3; x++)
            {
                c.Platform(x, f3, Suite.Gold.Platform);
                if ((x - cw0) % 2 == 1)
                    Column(x, f3 - 1, f3 - 1);
            }

            // ---- the chambers: a wall between each and the crossing with a door onto its
            // balcony, and the room itself
            var chambers = new[]
            {
                (new Room(i0, cw0 - 3, f3 + 2, f2), true, false),
                (new Room(cw1 + 3, i1, f3 + 2, f2), false, false),
                (new Room(i0, cw0 - 3, ceil, f3), true, true),
                (new Room(cw1 + 3, i1, ceil, f3), false, true),
            };

            foreach (var (r, west, upper) in chambers)
            {
                int p0 = west ? cw0 - 2 : cw1 + 1;
                Box(p0, r.Top, p0 + 1, r.Floor - 1, TileID.MarbleBlock, White);
                DoorOnto(p0, p0 + 1, r.Floor, west ? p0 + 1 : p0);
                RoyalChamber(r, west, upper);
            }

            // ---- the roof over the ends, the drum and dome over the crossing
            Cornice(k0, k1, top);
            Pitch2(k0 - 1, cw0 - 1, top - 2, 3, TileID.MarbleBlock, White, WallID.MarbleBlock, White);
            Pitch2(cw1 + 1, k1 + 1, top - 2, 3, TileID.MarbleBlock, White, WallID.MarbleBlock, White);
            for (int ox = k0 + 24; ox <= k1 - 24; ox += 14)
            {
                if (Math.Abs(ox - mid) <= crossHalf + 8)
                    continue;
                int oy = top - 6;
                if (c.TypeAt(ox, oy) < 0 && c.TypeAt(ox, oy - 2) < 0)
                    BackArch(ox - 1, ox + 1, oy + 1, oy - 1, 1, WallID.Glass);
            }
            GreatDome(mid, crossHalf, top);

            // ---- the hall
            // The line at the head of the table goes on the wall over the last candelabra.
            int tableEnd = CourtTable(i0 + 6, i1 - 24);
            Throne(i1 - 20, i1 - 1 - ThroneGap);
            c.Sign(tableEnd - 2, WardFloor - 5, TileID.Signs, 0, CourtText.Head);

            // The musicians' gallery over the door at the west end, rungs up to it from the
            // floor and on up through the chambers' floor.
            int bal = WardWalk;
            for (int x = i0; x <= i0 + 14; x++)
                c.Platform(x, bal, Suite.Gold.Platform);
            Back(i0, bal - 2, i0 + 14, bal - 1, WallID.WroughtIronFence, NoPaint);
            DoorOnto(k0, k0 + wall - 1, bal, k0);
            c.Place(i0 + 8, bal - 1, TileID.Pianos, Suite.Gold.Piano);
            Rungs(i0 + 1, WardFloor, bal, Suite.Gold.Platform);
            Rungs(i0 + 1, bal, f2, Suite.Gold.Platform);

            // The great doors from the courtyard; the east door to the Princess's tower.
            DoorOnto(k0, k0 + wall - 1, WardFloor, k0);
            DoorOnto(k1 - wall + 1, k1, WardFloor, k1);

            // Chandeliers on chains down the length of the hall: from the chambers' floors over
            // the wings, from the bridge in the crossing.
            for (int x = i0 + 20; x <= i1 - 20; x += 22)
            {
                if (x >= cw0 - 1 && x <= cw1 + 1)
                    ChainedChandelier(x, f3, WardFloor - 22);
                else
                    ChainedChandelier(x, f2 + 1, WardFloor - 16);
            }

            // Two towers at the ends of the roof, and the way up into each from the upper chambers.
            Tower(k0, k0 + 12, top - 1, top - 50, Roof.Dome, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Gold, storey: 12, doorWest: false, doorEast: false, decor: Decor.Royal, scheme: Scheme.Royal);
            Tower(k1 - 12, k1, top - 1, top - 50, Roof.Dome, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Gold, storey: 12, doorWest: false, doorEast: false, decor: Decor.Royal, scheme: Scheme.Royal);
            Rungs(i0 + 1, f3, top - 1, Suite.Gold.Platform);
            Rungs(i1 - 3, f3, top - 1, Suite.Gold.Platform);

            // The house on the walls, between the windows: its portraits.
            Adorn(new Room(i0, i1, WardFloor - 12, WardFloor), Decor.Royal, 8, 4);
            c.Mark("Grace.Court", i0, ceil, i1 - i0 + 1, WardFloor - ceil);
        }

        /// <summary>
        /// Rungs of platform three wide from the floor at <paramref name="floor"/> up to the
        /// floor at <paramref name="upper"/>, every three rows, through whatever solid is in
        /// the way, with a wall behind each: the way up through the floors of a building.
        /// </summary>
        private void Rungs(int x, int floor, int upper, int style)
        {
            for (int y = floor - 1; y >= upper; y--)
            {
                bool rung = y == upper || (floor - y) % 3 == 0;
                for (int xx = x; xx <= x + 2; xx++)
                {
                    int t = c.TypeAt(xx, y);
                    if (rung)
                    {
                        if (c.WallAt(xx, y) == 0)
                            c.Wall(xx, y, WallID.MarbleBlock, White);
                        c.Platform(xx, y, style);
                    }
                    else if (t >= 0 && t != TileID.Platforms && c.Solid(xx, y))
                    {
                        c.Erase(xx, y);
                        if (c.WallAt(xx, y) == 0)
                            c.Wall(xx, y, WallID.MarbleBlock, White);
                    }
                }
            }
        }

        /// <summary>
        /// A royal bedchamber: the walls in the court's registers with tall windows of glass,
        /// the canopied bed in the middle of the room between its posts, gold furniture either
        /// side of it, chandeliers, the portraits, and the chest at the foot. The lower chamber
        /// has the flight up in it and the upper the well the flight comes up through, both at
        /// the outer end, so the furniture keeps to the end by the door.
        /// </summary>
        private void RoyalChamber(Room r, bool west, bool upper)
        {
            var s = Scheme.Royal;
            Registers(r, s);

            // A tall narrow window in the middle of every bay, framed in gold, standing on the rail.
            int n = Math.Max(1, (r.W - 1) / s.Bay);
            for (int i = 0; i < n; i++)
            {
                int a = Spread(r.X0, r.X1, n, i);
                int b = Spread(r.X0, r.X1, n, i + 1);
                int m = BayMid(a, b, n, i);
                int sill = r.Floor - s.DadoH - 3;
                int head = r.Top + s.FriezeH + 5;
                if (sill - head < 6)
                    continue;
                Window(m - 1, m + 1, sill, head + 1, 1, WallID.AncientGoldBrickWall, NoPaint,
                    upper ? WallID.Glass : WallID.YellowStainedGlass, NoPaint);
            }

            // The stair between the two storeys of chambers: in the lower chambers, up to the upper.
            int stairLow = west ? r.X0 + 33 : r.X1 - 33;
            if (!upper)
                OpenFlight(stairLow, r.Floor, r.Floor - (r.Top - 2), west ? -1 : 1, Suite.Gold.Platform, WallID.MarbleBlock, White);

            // The free end of the room: in the lower chamber past the foot of the flight, in the
            // upper past the well it comes up through, and a column spare by the door. The set
            // reaches lo columns before the bed and hi after it, and stands in the middle of it.
            int clear = upper ? 19 : 35;
            int f0 = west ? r.X0 + clear : r.X0 + 1;
            int f1 = west ? r.X1 - 1 : r.X1 - clear;
            int lo = upper ? 14 : 10, hi = upper ? 17 : 16;
            if (f1 - f0 < lo + hi)
                return;
            int bx = f0 + lo + (f1 - f0 - lo - hi) / 2;
            int stand = r.Stand;

            // The bed on a step of white edged in gold, under its canopy: posts, a gold tester,
            // curtains down either side.
            Box(bx - 2, stand, bx + 5, stand, TileID.MarbleBlock, White);
            c.Block(bx - 3, stand, TileID.AncientGoldBrick, NoPaint, Grace.Cut.DownLeft);
            c.Block(bx + 6, stand, TileID.AncientGoldBrick, NoPaint, Grace.Cut.DownRight);
            int bed = stand - 1;
            c.Place(bx, bed, TileID.Beds, Suite.Gold.Bed, west ? 1 : -1);
            Column(bx - 1, bed - 5, bed);
            Column(bx + 4, bed - 5, bed);
            Box(bx - 2, bed - 6, bx + 5, bed - 6, TileID.AncientGoldBrick, NoPaint);
            Cut(bx - 2, bed - 6, Grace.Cut.UpLeft);
            Cut(bx + 5, bed - 6, Grace.Cut.UpRight);
            Hang(bx, bed - 5, TileID.Banners, upper ? 6 : 3);
            Hang(bx + 3, bed - 5, TileID.Banners, upper ? 6 : 3);

            // Either side: dressers with candelabra on them, the chest of the house, and at the
            // ends the books and the clock above, the angels below, where the clock stands
            // under the flight instead.
            c.Place(bx - 7, stand, TileID.Dressers, Suite.Gold.Dresser);
            c.Place(bx - 7, stand - 2, TileID.Candelabras, Suite.Gold.Candelabra);
            c.Place(bx + 8, stand, TileID.Dressers, Suite.Gold.Dresser);
            c.Place(bx + 9, stand - 2, TileID.Candelabras, Suite.Gold.Candelabra);
            Stash(bx + 12, stand, Loot.Royal, Suite.Gold);
            if (upper)
            {
                c.Place(bx - 10, stand, TileID.GrandfatherClocks, Suite.Gold.Clock);
                c.Place(bx - 14, stand, TileID.Bookcases, Suite.Gold.Bookcase);
                c.Place(bx + 15, stand, TileID.Bookcases, Suite.Gold.Bookcase);
            }
            else
            {
                c.Place(west ? r.X0 + 22 : r.X1 - 23, stand, TileID.GrandfatherClocks, Suite.Gold.Clock);
                c.Place(bx - 10, stand, TileID.Statues, 1);
                c.Place(bx + 15, stand, TileID.Statues, 1);
            }

            // Chandeliers from the ceiling, clear of the canopy; the portraits between the windows.
            foreach (int hx in new[] { bx - 9, bx + 12 })
                HangAt(hx, r.Top + 1, TileID.Chandeliers, Suite.Gold.Chandelier);
            Adorn(new Room(f0, f1, r.Top, r.Floor), Decor.Royal, 6, 9);
        }

        /// <summary>
        /// The back wall of the Court, in its tiers: a dado, the arcade with its tall windows
        /// of yellow glass, a gold frieze, a triforium of small arches, another frieze, the
        /// clerestory in clear glass that lets the day in, and a gold cornice under the ceiling.
        /// </summary>
        private void CourtWall(Room r)
        {
            int floor = r.Floor;
            int arcadeTop = floor - 40;
            int triTop = arcadeTop - 12;
            int clereTop = r.Top + 3;
            const int bay = 16;

            Back(r.X0, floor - 3, r.X1, floor - 1, WallID.StoneSlab, White);
            Back(r.X0, arcadeTop, r.X1, arcadeTop + 1, WallID.AncientGoldBrickWall, NoPaint);
            Back(r.X0, triTop, r.X1, triTop, WallID.AncientGoldBrickWall, NoPaint);
            Back(r.X0, r.Top, r.X1, r.Top + 2, WallID.AncientGoldBrickWall, NoPaint);

            int n = Math.Max(1, (r.W - 2) / bay);

            // Two wide, so the left columns are spread and each pilaster's mirror is the
            // pair that ends where this one starts, counted from the other end.
            for (int i = 0; i <= n; i++)
            {
                int px = Spread(r.X0, r.X1 - 1, n, i) + 1;
                Back(px - 1, r.Top + 3, px, floor - 4, WallID.PearlstoneBrick, White);
            }

            for (int i = 0; i < n; i++)
            {
                int a = Spread(r.X0, r.X1 - 1, n, i) + 3;
                int b = Spread(r.X0, r.X1 - 1, n, i + 1) - 2;
                int m = BayMid(a, b, n, i);

                // The arcade: an arch of darker stone, a tall yellow window in it.
                BackArch(a, b, floor - 4, arcadeTop + 2 + (b - a) / 2, (b - a) / 2, WallID.MarbleBlock, NoPaint);
                Window(m - 2, m + 2, floor - 8, arcadeTop + 8, 2, WallID.AncientGoldBrickWall, NoPaint, WallID.YellowStainedGlass, NoPaint);

                // The triforium: three small arches.
                for (int t = 0; t < 3; t++)
                {
                    int ta = a + 1 + t * ((b - a) / 3);
                    BackArch(ta, ta + 2, arcadeTop - 2, triTop + 3, 1, WallID.StoneSlab, NoPaint);
                }

                // The clerestory: clear glass, as tall as the wall allows.
                Window(m - 2, m + 2, triTop - 3, clereTop + 4, 2);
            }
        }

        /// <summary>
        /// The table: forty-one chairs of gold and forty tables between them, end to end down
        /// the hall from <paramref name="x"/>. Returns the column of the last chair.
        /// </summary>
        private int CourtTable(int x, int limit)
        {
            int stand = WardFloor - 1;
            int chairs = 0;
            int last = x;

            while (chairs < 41 && x <= limit)
            {
                c.Place(x, stand, TileID.Chairs, Suite.Gold.Chair, chairs % 2 == 0 ? 1 : -1);
                last = x;
                chairs++;
                x++;

                if (chairs == 41 || x + 2 > limit)
                    break;

                c.Place(x, stand, TileID.Tables, Suite.Gold.Table);
                int k = chairs % 4;
                if (k == 0)
                    c.Place(x, stand - 2, TileID.Candelabras, Suite.Gold.Candelabra);
                else if (k == 1)
                    c.Place(x + 1, stand - 2, TileID.Bowls, 0);
                else if (k == 2)
                    c.Place(x + 1, stand - 2, TileID.Candles, Suite.Gold.Candle);
                else
                    c.Place(x + 1, stand - 2, TileID.Bottles, 4);
                x += 3;
            }

            return last;
        }

        /// <summary>
        /// Clear floor left between the back of the dais and the east wall, so the way in from
        /// the east door comes out into the room and not against the side of the dais.
        /// </summary>
        private const int ThroneGap = 4;

        /// <summary>
        /// The dais: three broad steps of white with gold nosings, and on it the throne. Plain.
        /// A banner each side of it, and the east wall of the room above, with the floor clear
        /// behind it (ThroneGap) for whoever comes in by the east door.
        /// </summary>
        private void Throne(int d0, int d1)
        {
            int floor = WardFloor;
            for (int s = 1; s <= 3; s++)
            {
                int y = floor - s;
                Box(d0 + s * 2, y, d1, y, TileID.MarbleBlock, White);
                c.Block(d0 + s * 2, y, TileID.AncientGoldBrick, NoPaint, Grace.Cut.DownLeft);
            }

            int tx = (d0 + 6 + d1) / 2;
            c.Place(tx - 1, floor - 4, TileID.Thrones, 0);
            BracketBanner(d1 - 1, WardFloor - 18);
            BracketBanner(d0 + 7, WardFloor - 18);
            c.Mark("Grace.Throne", tx - 1, floor - 7, 3, 4);
        }

        /// <summary>A banner hung from a bracket of gold set into the wall above it.</summary>
        private void BracketBanner(int x, int top, int style = 3)
        {
            if (c.TypeAt(x, top - 1) < 0)
                c.Block(x, top - 1, TileID.AncientGoldBrick, NoPaint, Grace.Cut.Half);
            Hang(x, top, TileID.Banners, style);
        }

        /// <summary>A chandelier on a chain from the ceiling, hung from a rose of gold at the chain's end.</summary>
        private void ChainedChandelier(int x, int ceilingRow, int hangTo)
        {
            if (!c.Solid(x, ceilingRow))
                return;

            for (int y = ceilingRow + 1; y < hangTo; y++)
                c.Block(x, y, TileID.Chain);
            c.Block(x, hangTo, TileID.AncientGoldBrick, NoPaint);
            Hang(x - 1, hangTo + 1, TileID.Chandeliers, Suite.Gold.Chandelier);
        }

        /// <summary>
        /// The great dome over the crossing: a drum of white with clear windows all round it,
        /// a dome of gold on the drum, the inside of it painted with stars, and a lantern
        /// and a needle on top.
        /// </summary>
        private void GreatDome(int mid, int half, int top)
        {
            int drumTop = top - 26;
            int d0 = mid - half - 2, d1 = mid + half + 2;

            // The drum.
            Box(d0, drumTop, d1, top - 1, TileID.MarbleBlock, White);
            Hollow(d0 + 3, drumTop + 2, d1 - 3, top - 1, WallID.MarbleBlock, White);
            for (int x = d0 + 3; x <= d1 - 3; x++)
                for (int y = top; y < top + 4; y++)
                    c.Wall(x, y, WallID.AncientGoldBrickWall, NoPaint);

            for (int x = d0 + 7; x + 4 <= d1 - 5; x += 9)
                Window(x, x + 3, top - 4, drumTop + 7, 2);

            Cornice(d0, d1, drumTop);
            Course(d0, d1, top - 1, TileID.AncientGoldBrick, NoPaint);

            // The dome.
            int r = half + 3;
            Dome(mid, drumTop - 2, r, r - 4, 3, TileID.GoldBrick, NoPaint, WallID.GoldStarryGlassWall, NoPaint, lit: false);
            for (int x = mid - 20; x <= mid + 20; x += 10)
            {
                if (x == mid)
                    continue;
                int shell = CeilingOver(x, drumTop - 3);
                if (shell != int.MinValue)
                    ChainedChandelier(x, shell, shell + 6 + (20 - Math.Abs(x - mid)) / 2);
            }

            // The lantern on top, and the needle; the dome open under the lantern, so the day
            // comes down through the crown.
            int crown = drumTop - 2 - (r - 4);
            for (int x = mid - 2; x <= mid + 2; x++)
                for (int y = crown - 1; y <= crown + 4; y++)
                {
                    if (y > crown + 1 && !c.Solid(x, y))
                        continue;
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.Glass, NoPaint);
                }
            Box(mid - 3, crown - 7, mid - 3, crown - 1, TileID.MarbleBlock, White);
            Box(mid + 3, crown - 7, mid + 3, crown - 1, TileID.MarbleBlock, White);
            Box(mid - 3, crown - 7, mid + 3, crown - 7, TileID.MarbleBlock, White);
            Hollow(mid - 2, crown - 6, mid + 2, crown - 1, WallID.Glass, NoPaint);
            Dome(mid, crown - 8, 4, 4, 1, TileID.GoldBrick, NoPaint, WallID.GoldBrick, NoPaint);
            Spire(mid, crown - 12, 3, 12, TileID.GoldBrick, NoPaint);
        }

        // ------------------------------------------------------------ the Princess's tower

        /// <summary>
        /// The Princess's tower: tall and narrow at the east end of the Court, her rooms one
        /// over another, untouched. The bed is made. The books are where she left them.
        /// </summary>
        private void PrincessTower(int x0, int x1)
        {
            int top = WardFloor - 128;
            Tower(x0, x1, WardFloor, top, Roof.Spire, TileID.MarbleBlock, White,
                WallID.WhiteDynasty, NoPaint, Suite.Gold, storey: 14, doorWest: true, doorEast: true, decor: null);

            // Her rooms, bottom up: the ante-room, the schoolroom, the library, the bedroom,
            // the chapel, the music room, the belvedere.
            Theme[] rooms = { Theme.Hall, Theme.School, Theme.Library, Theme.Bedroom, Theme.Chapel, Theme.Study, Theme.Bare };
            int floor = WardFloor;
            int k = 0;
            while (floor - 14 > top + 2 && k < rooms.Length)
            {
                var r = new Room(x0 + 2, x1 - 2, floor - 13, floor);
                Theme t = rooms[k++];
                PrincessRoom(r, t, k % 2 == 1);
                if (t == Theme.Bedroom)
                    c.Place(r.X0 + 6, r.Floor - 5, TileID.Painting6X4, 45);
                Adorn(r, Decor.Royal, 5, 5);
                HangingPot(r.X1 - 4, r.Top + 2);
                floor -= 14;
            }

            // Balconies on the upper floors, facing the Font.
            for (int y = WardFloor - 42; y > top + 10; y -= 28)
            {
                Box(x1 + 1, y, x1 + 3, y, TileID.MarbleBlock, White);
                Cut(x1 + 3, y, Grace.Cut.UpRight);
                Back(x1 + 1, y - 2, x1 + 3, y - 1, WallID.WroughtIronFence, NoPaint);
            }
        }

        /// <summary>Furnishes one of the Princess's rooms. The rungs to the next floor are at one end; everything keeps clear of them.</summary>
        private void PrincessRoom(Room r, Theme t, bool hatchRight)
        {
            // Keep away from the rungs of this floor and the one above.
            var clear = new Room(r.X0 + 3, r.X1 - 3, r.Top, r.Floor);

            switch (t)
            {
                case Theme.Bedroom:
                    c.Place(clear.X0, clear.Stand, TileID.Dressers, Suite.Gold.Dresser);
                    c.Place(clear.X0 + 4, clear.Stand, TileID.Beds, Suite.Gold.Bed, -1);
                    c.Place(clear.X0 + 9, clear.Stand, TileID.Candelabras, Suite.Gold.Candelabra);
                    c.Place(clear.X0 + 2, clear.Stand - 2, TileID.Candles, Suite.Gold.Candle);
                    StashSomewhere(clear, Loot.Royal, Suite.Gold);
                    break;
                case Theme.Library:
                    c.Place(clear.X0, clear.Stand, TileID.Bookcases, Suite.Gold.Bookcase);
                    c.Place(clear.X0 + 4, clear.Stand, TileID.Bookcases, Suite.Gold.Bookcase);
                    c.Place(clear.X0 + 8, clear.Stand, TileID.Bookcases, Suite.Gold.Bookcase);
                    break;
                case Theme.School:
                    c.Place(clear.X0, clear.Stand, TileID.Chairs, Suite.Gold.Chair, 1);
                    c.Place(clear.X0 + 1, clear.Stand, TileID.Tables, Suite.Gold.Table);
                    c.Place(clear.X0 + 1, clear.Stand - 2, TileID.Books, 0);
                    c.Place(clear.X0 + 2, clear.Stand - 2, TileID.Books, 3);
                    c.Place(clear.X0 + 3, clear.Stand - 2, TileID.Candles, Suite.Gold.Candle);
                    c.Place(clear.X0 + 5, clear.Stand, TileID.Bookcases, Suite.Gold.Bookcase);
                    c.Place(clear.X0 + 9, clear.Stand, TileID.GrandfatherClocks, Suite.Gold.Clock);
                    c.Mark("Grace.PrincessDesk", clear.X0 + 1, clear.Stand - 2, 3, 1);
                    break;
                case Theme.Chapel:
                    c.Place(clear.X0, clear.Stand, TileID.Benches, Suite.Gold.Bench);
                    c.Place(clear.X0 + 5, clear.Stand, TileID.Candelabras, Suite.Gold.Candelabra);
                    c.Place(clear.X0 + 8, clear.Stand, TileID.Statues, 1);
                    break;
                case Theme.Study:
                    c.Place(clear.X0, clear.Stand, TileID.Pianos, Suite.Gold.Piano);
                    c.Place(clear.X0 + 4, clear.Stand, TileID.Benches, Suite.Gold.Bench);
                    c.Place(clear.X0 + 8, clear.Stand, TileID.Lamps, Suite.Gold.Lamp);
                    break;
                case Theme.Hall:
                    c.Place(clear.X0, clear.Stand, TileID.Statues, 12);
                    c.Place(clear.X0 + 3, clear.Stand, TileID.Benches, Suite.Gold.Bench);
                    c.Place(clear.X0 + 7, clear.Stand, TileID.Statues, 12);
                    break;
                default:
                    c.Place(clear.X0 + 3, clear.Stand, TileID.Sundial, 0);
                    break;
            }
        }

        // ------------------------------------------------------------ the armoury

        /// <summary>
        /// The armoury, at the east end of the Ward: two storeys of racks and stands in white
        /// and gold, beautiful and worse than what is outside the walls, with the Ward's east
        /// tower over it.
        /// </summary>
        private void Armoury(int x0, int x1)
        {
            if (x1 - x0 < 16)
                return;

            const int storey = 13;
            int top = WardFloor - storey * 2 - 1;
            Box(x0, top, x1, WardFloor - 1, TileID.MarbleBlock, White);

            for (int s = 0; s < 2; s++)
            {
                int floor = WardFloor - s * storey;
                var r = new Room(x0 + 2, x1 - 2, floor - storey + 1, floor);
                Hollow(r.X0, r.Top, r.X1, floor - 1, WallID.MarbleBlock, White);
                Panelled(r, WallID.MarbleBlock, White, 9);

                if (s > 0)
                {
                    for (int x = r.X0; x <= r.X0 + 2; x++)
                        c.Platform(x, floor, Suite.Marble.Platform);
                    for (int y = floor + 3; y < floor + storey; y += 3)
                        for (int x = r.X0; x <= r.X0 + 2; x++)
                            if (c.TypeAt(x, y) < 0)
                                c.Platform(x, y, Suite.Marble.Platform);
                }

                var inner = new Room(r.X0 + 4, r.X1, r.Top, r.Floor);
                Furnish(inner, s == 0 ? Theme.Armoury : Theme.Barracks, Suite.Marble);
                Neglect(inner, 1, 0);
                Adorn(inner, Decor.Military, 5, 5);
            }

            DoorOnto(x0, x0 + 1, WardFloor, x0);
            DoorOnto(x1 - 1, x1, WardFloor, x1);
            Cornice(x0, x1, top);

            // The east tower of the Ward over it.
            int t0 = x1 - 12;
            Tower(t0, x1, top - 1, WardFloor - 110, Roof.Battlements, TileID.MarbleBlock, White,
                WallID.MarbleBlock, White, Suite.Marble, storey: 12, doorWest: false, doorEast: false, decor: Decor.Military);
            for (int x = t0 + 2; x <= t0 + 4; x++)
            {
                c.Erase(x, top - 1);
                c.Erase(x, top);
                c.Platform(x, top, Suite.Marble.Platform);
            }
        }

        // ------------------------------------------------------------ under the Ward

        /// <summary>
        /// Under the terrace: two levels of vaulted rooms. Over the Court's length the upper
        /// level is the kitchens and the servants' hall that fed the table; the lower is the
        /// cellars. The stair from the gate is carved through afterwards.
        /// </summary>
        private void WardUndercroft(int x0, int x1, int court0, int court1)
        {
            int upperFloor = -18;
            int lowerFloor = 0;
            const int bay = 16;

            for (int bx = court0 - 10; bx + bay <= x1 - 6; bx += bay)
            {
                int a0 = bx + 2, a1 = bx + bay - 1;

                Room up = Vaulted(a0, a1, upperFloor, upperFloor - 9, 5, WallID.StoneSlab, White);
                Room low = Vaulted(a0, a1, lowerFloor, lowerFloor - 9, 5, WallID.GrayBrick, NoPaint);
                Registers(new Room(a0, a1, upperFloor - 9, upperFloor), Scheme.Service);
                Registers(new Room(a0, a1, lowerFloor - 9, lowerFloor), Scheme.Store);

                if (bx + bay * 2 <= x1 - 6)
                {
                    Hollow(a1 + 1, upperFloor - 3, a1 + 2, upperFloor - 1, WallID.StoneSlab, White);
                    Hollow(a1 + 1, lowerFloor - 3, a1 + 2, lowerFloor - 1, WallID.GrayBrick, NoPaint);
                }

                bool underCourt = a0 >= court0 && a1 <= court1;
                int k = (bx - court0) / bay;
                Theme upper = !underCourt ? Theme.Store : (k % 3 == 0 ? Theme.Hall : Theme.Kitchen);
                Furnish(new Room(a0, a1, upperFloor - 9, upperFloor), upper, Suite.Marble);
                Furnish(new Room(a0, a1, lowerFloor - 9, lowerFloor), Theme.Store, Suite.Wood);
                Adorn(new Room(a0, a1, upperFloor - 9, upperFloor), upper == Theme.Hall ? Decor.Hall : Decor.Home, 8, 4);
                Neglect(new Room(a0, a1, upperFloor - 9, upperFloor), 2, 0);
                Neglect(new Room(a0, a1, lowerFloor - 9, lowerFloor), 3, 1);
            }

            // The service stair: from the courtyard down through both levels, beside the Court.
            int sx = court0 - 8;
            for (int y = WardFloor; y <= lowerFloor - 1; y++)
            {
                for (int x = sx; x <= sx + 2; x++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.StoneSlab, White);
                }
            }
            for (int y = WardFloor; y < lowerFloor - 1; y += 3)
                for (int x = sx; x <= sx + 2; x++)
                    c.Platform(x, y, Suite.Marble.Platform);
            Hollow(sx + 3, upperFloor - 3, court0 - 1, upperFloor - 1, WallID.StoneSlab, White);
            Hollow(sx + 3, lowerFloor - 3, court0 - 1, lowerFloor - 1, WallID.GrayBrick, NoPaint);
            Box(sx, upperFloor, sx + 2, upperFloor, TileID.MarbleBlock, White);
            for (int x = sx; x <= sx + 2; x++)
                c.Platform(x, upperFloor, Suite.Marble.Platform);
        }
    }

    /// <summary>What is written in the Court.</summary>
    public static class CourtText
    {
        /// <summary>Carved at the head of the table (Lore Bible §XIX, locked).</summary>
        public const string Head = "May I say grace?";
    }
}
