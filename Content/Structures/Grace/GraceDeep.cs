using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// What is under the Undercity. The Long Reliquary: four galleries one under another in
    /// one block of white stone, just under the lowest street, the kingdom's record of itself,
    /// reached by one stair from that street through a sealed door. Under its lowest gallery,
    /// the crack down past everything to the Font chamber (<see cref="GraceBuilder.Fissure"/>).
    /// And the Rootworks at the bottom: the Shimmer's own cavern, lined, with roots coming
    /// down through its roof to the pool, and a second tunnel into it that nobody built.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>How tall a gallery is inside, and how far one gallery's floor is from the next.</summary>
        private const int ReliquaryHeight = 30;
        private const int ReliquaryPitch = 40;

        /// <summary>How wide a bay of a gallery is, pier to pier.</summary>
        private const int ReliquaryBay = 22;

        /// <summary>
        /// The Reliquary's galleries, their floor rows: the first a little under the lowest
        /// street's lining, the rest stacked under it with ten rows of stone between.
        /// </summary>
        private int[] ReliquaryFloors
        {
            get
            {
                int f0 = streets[streets.Count - 1].floor + 3 + 12 + ReliquaryHeight;
                return new[] { f0, f0 + ReliquaryPitch, f0 + 2 * ReliquaryPitch, f0 + 3 * ReliquaryPitch };
            }
        }

        private enum Relic { Rolls, Regalia, Accounts, Crypt }

        /// <summary>
        /// The first column of the stair down from the lowest street to the Reliquary. The
        /// street keeps the ground round it clear of houses (<see cref="LodgeLot"/>), so the
        /// lodge at its head stands in the open with its gate on the street.
        /// </summary>
        private int DescentX => Math.Min(streets[2].x1 - 60, ShaftX0 - 60 - 80);

        /// <summary>The stretch of the lowest street kept open for the lodge over the stair and the way up to its gate.</summary>
        private (int lo, int hi) LodgeLot => (DescentX - 12, DescentX + 16);

        // ------------------------------------------------------------ the Reliquary

        private void Reliquary()
        {
            int x0 = Math.Max(ForestStart + 400, 620);
            int x1 = ShaftX0 - 60;
            if (x1 - x0 < 200)
                return;

            int[] floors = ReliquaryFloors;

            // One block of white stone round all four, so the galleries are storeys of one
            // building rather than four boxes in the ground.
            Box(x0 - 6, floors[0] - ReliquaryHeight - 6, x1 + 6, floors[floors.Length - 1] + 6, TileID.MarbleBlock, White);
            deepParts.Add((x0 - 6, floors[0] - ReliquaryHeight - 6, x1 + 6, floors[floors.Length - 1] + 6));

            // Where the descent from the lowest street comes in.
            int floor = streets[2].floor;
            int dx = DescentX;

            // The grand stairs, one from each gallery down to the next, never at the end the
            // gallery was come into: the first at the far end from the descent, and then from
            // end to end, so every gallery is walked its whole length to find the way on. The
            // west stairs stand where the east ones would, turned about the middle.
            var stairs = new int[floors.Length - 1];
            bool east = dx + 4 < (x0 + x1) / 2;
            for (int k = 0; k < stairs.Length; k++)
            {
                stairs[k] = east ? x1 - 30 - k * 3 : x0 + 23 + k * 3;
                east = !east;
            }

            // The galleries, each with the bays its stairs stand in left bare, so a shaft never
            // comes down through half a tomb or half a bookcase.
            for (int k = 0; k < floors.Length; k++)
            {
                var bare = new List<(int lo, int hi)>();
                if (k == 0)
                    bare.Add((dx - 2, dx + 9));
                if (k > 0)
                    bare.Add((stairs[k - 1] - 2, stairs[k - 1] + 9));
                if (k < stairs.Length)
                    bare.Add((stairs[k] - 2, stairs[k] + 9));

                ReliquaryGallery(x0 + k * 10, x1 - k * 10, floors[k], (Relic)k, bare);
            }

            for (int k = 0; k < stairs.Length; k++)
                GrandStair(stairs[k], floors[k], floors[k + 1]);

            // The descent from the lowest street to the first gallery, and the seal on it.
            Descent(dx, floor, floors[0]);

            // Under the Crypt: the tomb that went through the floor, and the crack from it down to
            // the Font chamber's door.
            FallenTomb(x0 + 30, x1 - 30, floors[3]);

            c.Mark("Grace.Reliquary", x0, ReliquaryFloors[0] - ReliquaryHeight, x1 - x0 + 1,
                ReliquaryFloors[ReliquaryFloors.Length - 1] - ReliquaryFloors[0] + ReliquaryHeight + 4);
        }

        /// <summary>One gallery of the Reliquary: a long white vault on piers, fitted for what it keeps.</summary>
        private void ReliquaryGallery(int x0, int x1, int floor, Relic what, List<(int lo, int hi)> bare)
        {
            int top = floor - ReliquaryHeight;

            Box(x0 - 4, top - 4, x1 + 4, floor + 4, TileID.MarbleBlock, White);
            Box(x0 - 4, floor, x1 + 4, floor, TileID.StoneSlab, White);

            // Bays twenty-two wide, each its own vault, a doorway through every pier.
            const int bay = ReliquaryBay;
            int n = 0;
            for (int bx = x0; bx + bay - 1 <= x1; bx += bay, n++)
            {
                int a0 = bx + 2, a1 = bx + bay - 1;
                Room r = Vaulted(a0, a1, floor, top + 8, 7, WallID.MarbleBlock, White);
                Registers(new Room(a0, a1, top + 8, floor), what == Relic.Crypt ? Scheme.Crypt : Scheme.Palace);
                if (bx + bay * 2 - 1 <= x1)
                    Hollow(a1 + 1, floor - 4, a1 + 2, floor - 1, WallID.MarbleBlock, White);
                Hollow(bx, floor - 4, bx + 1, floor - 1, WallID.MarbleBlock, White);

                // A bay a stair stands in keeps its walls and nothing else.
                bool stairBay = false;
                foreach (var (lo, hi) in bare)
                    if (hi >= bx && lo <= a1)
                        stairBay = true;
                if (stairBay)
                    continue;

                var room = new Room(a0, a1, top + 1, floor);
                FurnishRelic(room, what, n);
                if (what == Relic.Regalia || what == Relic.Crypt || c.OneIn(2))
                    StashSomewhere(room, what == Relic.Crypt ? Loot.Crypt : what == Relic.Rolls ? Loot.Scholar : Loot.Royal,
                        what == Relic.Crypt ? Suite.Marble : Suite.Gold);
                Adorn(room, what == Relic.Crypt ? Decor.Crypt : what == Relic.Regalia ? Decor.Royal : Decor.Scholar, 6, 5);
                HangAt((a0 + a1) / 2 - 1, floor - 12, TileID.Chandeliers, Suite.Gold.Chandelier);
                Neglect(new Room(a0, a1, top + 1, floor), 3, 0);
            }
        }

        /// <summary>Fits one bay of a Reliquary gallery.</summary>
        private void FurnishRelic(Room r, Relic what, int n)
        {
            int stand = r.Stand;

            switch (what)
            {
                case Relic.Rolls:
                {
                    // Cases of rolls down both sides, a reading table between.
                    for (int x = r.X0 + 1; x + 2 <= r.X1; x += 4)
                        if (x + 2 < r.Mid - 3 || x > r.Mid + 3)
                            c.Place(x, stand, TileID.Bookcases, Suite.Marble.Bookcase);
                    c.Place(r.Mid - 1, stand, TileID.Tables, Suite.Marble.Table);
                    c.Place(r.Mid - 1, stand - 2, TileID.Books, c.Next(0, 5));
                    c.Place(r.Mid, stand - 2, TileID.Candles, Suite.Marble.Candle);

                    // One case, in one bay, emptied of exactly one shelf and nothing else.
                    if (n == 5)
                        c.Mark("Grace.EditedShelf", r.X0 + 1, stand - 3, 3, 4);
                    break;
                }

                case Relic.Regalia:
                {
                    // The crowns and armour of the house on stands behind glass.
                    int x = r.X0 + 1;
                    while (x + 2 <= r.X1)
                    {
                        int pick = (x + n) % 4;
                        if (pick == 0) c.Place(x, stand, TileID.Statues, 6);
                        else if (pick == 1) c.Place(x, stand, TileID.Statues, n % 2 == 0 ? 40 : 41);
                        else if (pick == 2) c.Place(x, stand, TileID.Statues, 3);
                        else c.Place(x, stand, TileID.Statues, 0);
                        x += 4;
                    }
                    for (int gx = r.X0; gx <= r.X1; gx++)
                        c.Wall(gx, stand - 4, WallID.Glass, NoPaint);
                    break;
                }

                case Relic.Accounts:
                    stashKind = Loot.Royal;
                    stashSuite = Suite.Gold;
                    Along(r, 1, 1, 2,
                        (TileID.Bookcases, Suite.Marble.Bookcase, 1), (TileID.Chairs, Suite.Marble.Chair, 1),
                        (TileID.Tables, Suite.Marble.Table, 1), (TileID.Safes, 0, 1),
                        (TileID.Bookcases, Suite.Marble.Bookcase, 1), (TileID.Containers, Suite.Gold.Chest, 1));
                    break;

                default:
                {
                    // Tombs: a chest of white stone on a gold plinth, a stone at its head. Set in
                    // two from the pier, so there is room at the doorway to climb onto the first.
                    for (int x = r.X0 + 2; x + 6 <= r.X1; x += 9)
                    {
                        Box(x, stand, x + 5, stand, TileID.AncientGoldBrick, NoPaint);
                        Box(x, stand - 2, x + 5, stand - 1, TileID.MarbleBlock, White);
                        Cut(x, stand - 2, Grace.Cut.DownLeft);
                        Cut(x + 5, stand - 2, Grace.Cut.DownRight);
                        c.Place(x + 6, stand, TileID.Tombstones, 6 + (x + n) % 5);
                    }
                    if (n == 3)
                        c.Mark("Grace.OneDateGrave", r.X0 + 2, stand - 2, 7, 3);
                    break;
                }
            }
        }

        /// <summary>A stair of stone flights from one gallery down to the next, in its own shaft.</summary>
        private void GrandStair(int x, int upper, int lower)
        {
            int s0 = x, s1 = x + 7;
            Box(s0 - 2, upper - 5, s1 + 2, lower + 1, TileID.MarbleBlock, White);
            Hollow(s0, upper - 4, s1, lower - 1, WallID.StoneSlab, White);
            for (int xx = s0; xx <= s1; xx++)
            {
                c.Erase(xx, upper);
                c.Platform(xx, upper, Suite.Marble.Platform);
            }
            Air(s0 - 2, upper - 4, s1 + 2, upper - 1);
            Hollow(s0 - 2, lower - 4, s0 - 1, lower - 1, WallID.StoneSlab, White);
            Hollow(s1 + 1, lower - 4, s1 + 2, lower - 1, WallID.StoneSlab, White);

            int y = lower;
            bool east = true;
            while (y - 6 > upper)
            {
                for (int i = 1; i <= 5; i++)
                {
                    int xx = east ? s0 + i - 1 : s1 - i + 1;
                    c.Platform(xx, y - i, Suite.Marble.Platform, east ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
                }
                y -= 6;
                for (int xx = s0; xx <= s1; xx++)
                    if (c.TypeAt(xx, y) < 0)
                        c.Platform(xx, y, Suite.Marble.Platform);
                if ((lower - y) % 24 == 0)
                    Hang(east ? s0 : s1, y + 1, TileID.HangingLanterns, Suite.Marble.Lantern);
                east = !east;
            }
        }

        /// <summary>
        /// The descent from the lowest street of the Undercity to the Reliquary: a long stair
        /// in white stone, and at its head, where it leaves the street, the seal: a door that
        /// was meant to stay shut.
        /// </summary>
        private void Descent(int x, int upper, int lower)
        {
            GrandStair(x, upper, lower);

            // The stair's shaft from under the street is part of the deep, and is walled with it.
            deepParts.Add((x - 2, upper + 1, x + 9, lower + 1));

            // The seal. The head of the stair is a lodge of white stone standing in the street,
            // roofed, shut at its east end, and come into at its west end through one tall gate
            // under a head of gold. There is no other way onto the stair (GraceSeal keeps the
            // gate shut until someone brings its key).
            int gx = x - 2;
            Box(x + 9, upper - 4, x + 9, upper - 1, TileID.MarbleBlock, White);
            Air(gx, upper - 5, gx, upper - 1);
            Box(gx - 1, upper - 7, gx + 1, upper - 6, TileID.AncientGoldBrick, NoPaint);
            c.Place(gx, upper - 1, TileID.TallGateClosed, 0);
            c.Mark("Grace.ReliquarySeal", gx, upper - 5, 1, 5);
        }

        // ------------------------------------------------------------ the Font's shaft

        /// <summary>
        /// A length of the Font's shaft from <paramref name="top"/> down to <paramref name="bottom"/>:
        /// seven wide inside, lined in white, flights of platforms and landings the whole way, a
        /// lamp every few landings and a niche of vessels now and then where the carriers set
        /// down what they carried. The middle is clear from top to bottom. The long length runs
        /// from under the chapel down to the Undercity's first street; the short one from the
        /// boards in the chamber's floor down to the Shimmer. Marked as <paramref name="mark"/>
        /// unless that is null.
        /// </summary>
        private void FontShaft(int top, int bottom, string mark)
        {
            int s0 = ShaftX0, s1 = ShaftX1;

            Box(s0 - 2, top, s1 + 2, bottom, TileID.MarbleBlock, White);
            Hollow(s0, top, s1, bottom, WallID.StoneSlab, White);

            // Flights against the two sides, landings between: the middle column stays clear.
            int y = top + 5;
            bool west = true;
            int landings = 0;
            while (y + 6 < bottom)
            {
                for (int i = 0; i < 3; i++)
                {
                    int xx = west ? s0 + i : s1 - i;
                    c.Platform(xx, y + i, Suite.Marble.Platform, west ? Grace.Cut.DownRight : Grace.Cut.DownLeft);
                }
                y += 6;
                int l0 = west ? s0 : s1 - 2, l1 = west ? s0 + 2 : s1;
                for (int xx = l0; xx <= l1; xx++)
                    c.Platform(xx, y, Suite.Marble.Platform);
                landings++;

                if (landings % 4 == 0)
                    Hang(west ? s0 : s1, y + 1, TileID.HangingLanterns, Suite.Marble.Lantern);

                // A niche in the wall with a jug in it, every so often.
                if (landings % 9 == 0)
                {
                    int nx = west ? s0 - 2 : s1 + 1;
                    Hollow(nx, y - 3, nx + 1, y - 1, WallID.StoneSlab, NoPaint);
                    c.Block(nx, y, TileID.MarbleBlock, White);
                    c.Block(nx + 1, y, TileID.MarbleBlock, White);
                    c.Place(west ? nx : nx + 1, y - 1, TileID.Bottles, 4);
                }

                west = !west;
            }

            if (mark != null)
                c.Mark(mark, s0, top, s1 - s0 + 1, bottom - top);
        }

        // ------------------------------------------------------------ the Rootworks

        /// <summary>How thick the Shimmer room's walls of aether stone and marble are.</summary>
        private const int RootShell = 16;

        /// <summary>The Rootworks' crown: the first open row under its vault.</summary>
        private int RootCeil => site.ShimmerY - 52;

        /// <summary>
        /// The Rootworks: the cavern the Shimmer lies in, walled thick in marble and aether
        /// stone, the pool in the middle of its floor, the short shaft coming down over the
        /// pool from the boards in the Font chamber above, a gallery from the foot of the shaft
        /// to a stair down the wall, the shapes of roots on the walls reaching down into the
        /// pool, and from the west an older tunnel.
        /// </summary>
        private void Rootworks()
        {
            int mx = (ShaftX0 + ShaftX1) / 2;
            int surface = site.ShimmerY;
            int floor = surface;
            int ceil = RootCeil;
            int r0 = mx - 72, r1 = mx + 72;
            int p0 = mx - 34, p1 = mx + 34;
            const int depth = 10;

            FontShaft(ArenaFloor + 1, ceil - 1, "Grace.ShimmerShaft");

            // The cavern: walls of aether stone and marble sixteen thick round a vaulted room,
            // their outer face left rough where it meets the rock.
            int o0 = r0 - RootShell, o1 = r1 + RootShell, oTop = ceil - RootShell, oBottom = floor + depth + RootShell;
            deepParts.Add((o0, oTop, o1, oBottom));
            for (int x = o0; x <= o1; x++)
            {
                for (int y = oTop; y <= oBottom; y++)
                {
                    int edge = Math.Min(Math.Min(x - o0, o1 - x), Math.Min(y - oTop, oBottom - y));
                    if (edge < 3 && Layout01(x * 3 + 7, y * 3 + 11) < 0.45 - 0.15 * edge)
                        continue;
                    c.Block(x, y, Layout01(x, y) < 0.35 ? TileID.MarbleBlock : TileID.ShimmerBlock, Layout01(x, y) < 0.35 ? White : NoPaint);
                }
            }

            Room hall = Vaulted(r0, r1, floor, ceil + 18, 18, WallID.ShimmerBrickWall, NoPaint);
            for (int x = r0; x <= r1; x++)
                for (int y = floor - 3; y < floor; y++)
                    if (c.TypeAt(x, y) < 0)
                        c.Wall(x, y, WallID.MarbleBlock, White);

            // The floor: marble walks both sides of the pool, steps down into it.
            Box(r0, floor, r1, floor + 1, TileID.MarbleBlock, White);
            Box(r0, floor, r1, floor, TileID.StoneSlab, White);

            for (int x = p0; x <= p1; x++)
            {
                int d = Math.Min(x - p0, p1 - x);
                int bottom = floor + Math.Min(depth, 1 + d);
                for (int y = floor; y < bottom; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.ShimmerBlockWall, NoPaint);
                    c.Liquid(x, y, 255, 3);
                }
                c.Block(x, bottom, TileID.ShimmerBrick);
                if (d < depth)
                    Cut(x, bottom, x < mx ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
            }
            c.Block(p0 - 1, floor, TileID.AncientGoldBrick, NoPaint);
            c.Block(p1 + 1, floor, TileID.AncientGoldBrick, NoPaint);

            // The shaft opens in the crown, over the pool.
            for (int x = ShaftX0; x <= ShaftX1; x++)
                for (int y = ceil - RootShell; y <= hall.Top; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.StoneSlab, White);
                }

            // From the foot of the shaft, a gallery to the east wall and a stair down it.
            int gy = hall.Top + 12;
            for (int x = ShaftX1 + 1; x <= r1 - 1; x++)
                if (c.TypeAt(x, gy) < 0)
                    c.Platform(x, gy, Suite.Gold.Platform);
            for (int x = ShaftX0; x <= ShaftX1; x++)
                c.Platform(x, hall.Top + 1, Suite.Gold.Platform);
            for (int y = hall.Top + 2; y < gy; y += 3)
                for (int x = ShaftX1 - 2; x <= ShaftX1; x++)
                    if (c.TypeAt(x, y) < 0)
                        c.Platform(x, y, Suite.Gold.Platform);
            OpenFlight(r1 - 1, floor, floor - gy, -1, Suite.Gold.Platform);

            // Roots through the roof, reaching down into the pool: their shapes on the walls.
            for (int i = 0; i < 5; i++)
            {
                int rx = p0 + 6 + i * 14 + c.Next(-3, 4);
                if (Math.Abs(rx - mx) < 6)
                    continue;
                Root(rx, ceil - 2, floor + 2);
            }

            // Light: lamps on the walks, crystal in the walls.
            for (int x = r0 + 4; x < p0 - 2; x += 9)
                c.Place(x, floor - 1, TileID.Lamps, Suite.Marble.Lamp);
            for (int x = p1 + 4; x < r1 - 6; x += 9)
                c.Place(x, floor - 1, TileID.Lamps, Suite.Marble.Lamp);

            // The older tunnel, from the west, rough, unlined.
            OldTunnel(r0 - 5, floor - 1);

            c.Mark("Grace.Shimmer", p0, floor, p1 - p0 + 1, depth);
            c.Mark("Grace.Rootworks", r0, ceil, r1 - r0 + 1, floor - ceil + depth);
        }

        /// <summary>A cheap fixed noise in [0, 1) for patterning stone.</summary>
        private static double Layout01(int x, int y)
        {
            unchecked
            {
                int h = (x / 3 * 73856093) ^ (y / 3 * 19349663);
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65536.0;
            }
        }

        /// <summary>
        /// A root from the roof down to the pool, as a wall of living wood behind the room:
        /// thick at the top, wandering a little, thinning to a single strand where it goes down
        /// into the Shimmer. Nothing solid: the room stays open.
        /// </summary>
        private void Root(int x, int from, int to)
        {
            double cx = x;
            for (int y = from; y <= to; y++)
            {
                double t = (y - from) / (double)Math.Max(1, to - from);
                int w = (int)Math.Round(4 * (1 - t)) + 1;
                cx += (c.NextDouble() - 0.5) * 0.8;
                int left = (int)Math.Round(cx) - w / 2;

                for (int xx = left; xx < left + w; xx++)
                    if (c.WallAt(xx, y) != 0)
                        c.Wall(xx, y, WallID.LivingWood, NoPaint);
            }
        }

        /// <summary>A tunnel from the west into the Rootworks: nobody lined it, and it goes further than anyone went.</summary>
        private void OldTunnel(int x, int y)
        {
            double fy = y;
            for (int i = 0; i < 180; i++)
            {
                int xx = x - i;
                fy += (c.NextDouble() - 0.5) * 1.2;
                int yy = (int)Math.Round(fy);
                int h = 4 + c.Next(0, 2);
                for (int dy = 0; dy < h; dy++)
                {
                    c.Erase(xx, yy - dy);
                    c.Wall(xx, yy - dy, WallID.RocksUnsafe3, NoPaint);
                }
                if (c.TypeAt(xx, yy + 1) < 0)
                    c.Block(xx, yy + 1, TileID.Stone);
            }
            c.Mark("Grace.OldTunnel", x - 180, y - 6, 180, 8);
        }
    }
}
