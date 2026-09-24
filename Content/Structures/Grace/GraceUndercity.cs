using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The Undercity, where the population went. Three streets one under another, each a
    /// long brick-vaulted tunnel with houses standing in it: the Low City's plan again,
    /// built in a hurry, in grey brick and timber, no windows because there is nothing to
    /// look out of. Dormitories with bunks three high, kitchens, the hospital, the school,
    /// the counting-house. Stairwells between the streets.
    ///
    /// All of it lies under the flat of the plateau, deep enough that nothing of it shows
    /// from the ground above or from the way up.
    ///
    /// This is where the ruin is. Nothing up top was touched; down here nothing was spared.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>The three streets: their floors, their west and east ends.</summary>
        private readonly List<(int floor, int x0, int x1)> streets = new List<(int, int, int)>();

        /// <summary>How tall a street's tunnel is, floor to vault.</summary>
        private const int UnderHeight = 34;

        /// <summary>The first street's floor row; the others are a hundred and two hundred under it.</summary>
        private const int FirstStreetFloor = 100;

        private enum UnderUse { Dormitory, Kitchen, Hospital, School, Counting, Workshop, Chapel, Barracks, Store, Fallen }

        private void Undercity()
        {
            int east = Math.Min(ShaftX0 - 14, ShoreX - 20);

            // Every street starts east of the top of the way up, so the flat of the plateau is
            // over all of it and none of it is near the slope's face.
            streets.Add((FirstStreetFloor, Math.Max(ForestStart + 20, 110), east));
            streets.Add((FirstStreetFloor + 100, Math.Max(ForestStart + 50, 240), east - 40));
            streets.Add((FirstStreetFloor + 200, Math.Max(ForestStart + 150, 360), east - 110));

            for (int k = 0; k < streets.Count; k++)
            {
                var (floor, x0, x1) = streets[k];
                UnderStreet(x0, x1, floor, k);
            }

            // Stairwells between the streets.
            var (f0, a0, a1) = streets[0];
            var (f1, b0, b1) = streets[1];
            var (f2, d0, d1) = streets[2];

            Stairwell(b0 + 30, f0, f1, UnderText.CountedBack);
            Stairwell((b0 + b1) / 2 + 7, f0, f1, null);
            Stairwell(b1 - 40, f0, f1, null);
            Stairwell(d0 + 40, f1, f2, null);
            Stairwell(d1 - 30, f1, f2, UnderText.CountedOut);

            // The lock under the market's door.
            MarketLock();

            // A way from the first street's east end up to the harbour.
            SallyPort(a1, f0);

            c.Mark("Grace.Undercity", a0, f0 - UnderHeight, a1 - a0 + 1, f2 - f0 + UnderHeight + 4);
        }

        // ------------------------------------------------------------ a street

        /// <summary>
        /// One street: a brick tunnel from <paramref name="x0"/> to <paramref name="x1"/>, its
        /// floor at <paramref name="floor"/>, houses along it, lamps here and there, and the
        /// places where the vault has come down.
        /// </summary>
        private void UnderStreet(int x0, int x1, int floor, int k)
        {
            int vault = floor - UnderHeight;

            // The lining: brick round the whole tunnel, the floor paved, the space cleared.
            Box(x0 - 3, vault - 3, x1 + 3, floor + 3, TileID.GrayBrick, NoPaint);
            Box(x0 - 3, floor, x1 + 3, floor, TileID.StoneSlab, NoPaint);
            Hollow(x0, vault, x1, floor - 1, WallID.RocksUnsafe1, NoPaint);

            // Vault ribs every forty: a pier-less arch of brick across the tunnel's crown,
            // shown in section as a thickening of the vault with a chamfered underside.
            for (int rx = x0 + 20; rx <= x1 - 20; rx += 40)
            {
                Box(rx - 2, vault, rx + 2, vault + 1, TileID.GrayBrick);
                Cut(rx - 2, vault + 1, Grace.Cut.UpLeft);
                Cut(rx + 2, vault + 1, Grace.Cut.UpRight);
                Back(rx - 1, vault + 2, rx + 1, floor - 1, WallID.GrayBrick, NoPaint);
            }

            // The houses. On the lowest street none stands where the stair to the Reliquary
            // comes up: its lodge and the way to its gate keep the street open there.
            var (lot0, lot1) = k == 2 ? LodgeLot : (int.MaxValue, int.MaxValue);
            int x = x0 + 8;
            int n = 0;
            while (x < x1 - 24)
            {
                int w = 18 + c.Next(0, 4) * 4;
                if (x + w > x1 - 6)
                    break;

                if (x + w - 1 >= lot0 && x <= lot1)
                {
                    x = lot1 + 4;
                    continue;
                }

                UnderUse use = PickUse(n, k);
                int storeys = use == UnderUse.Fallen ? 1 : (w >= 26 && c.OneIn(2) ? 3 : 2);
                UnderHouse(x, x + w - 1, floor, storeys, use, k);
                x += w + 3 + c.Next(0, 4);
                n++;
            }

            // Lamps: a few still burning.
            for (int lx = x0 + 6; lx <= x1 - 6; lx += 17 + c.Next(0, 9))
            {
                if (lx >= lot0 - 1 && lx <= lot1)
                    continue;
                if (c.TypeAt(lx, floor - 1) < 0 && c.TypeAt(lx, floor - 6) < 0)
                    c.Place(lx, floor - 1, TileID.Lampposts, 0);
            }
            for (int lx = x0 + 10; lx <= x1 - 10; lx += 24 + c.Next(0, 12))
                HangAt(lx, floor - 20, TileID.HangingLanterns, Suite.Wood.Lantern);

            // Where the vault came down: a heap of rock and brick across the street, and the
            // hole above it open into the ground.
            for (int i = 0; i < 2 + k; i++)
            {
                int fx = c.Next(x0 + 80, Math.Max(x0 + 81, x1 - 60));
                if (fx + 12 >= lot0 && fx - 12 <= lot1)
                    fx = lot0 - 14;
                Rockfall(fx, vault, floor);
            }

            // Rubble and bones along the whole street, thicker toward the west end.
            for (int rx = x0 + 2; rx <= x1 - 4; rx += 3 + c.Next(0, 6))
            {
                if (rx + 2 >= lot0 && rx <= lot1)
                    continue;
                if (c.TypeAt(rx, floor - 1) >= 0 || c.TypeAt(rx + 2, floor - 1) >= 0)
                    continue;
                double west = 1.0 - (rx - x0) / (double)Math.Max(1, x1 - x0);
                if (c.NextDouble() > 0.25 + 0.35 * west)
                    continue;
                if (c.OneIn(3))
                    c.Place(rx, floor - 1, TileID.LargePiles, c.Next(0, 6));
                else
                    c.Place(rx, floor - 1, TileID.LargePiles, c.Next(7, 13));
            }

            // Cobwebs along the vault.
            for (int wx = x0; wx <= x1; wx++)
                for (int y = vault; y < vault + 4; y++)
                    if (c.TypeAt(wx, y) < 0 && c.OneIn(y == vault ? 3 : 7))
                        c.Block(wx, y, TileID.Cobweb);
        }

        private UnderUse PickUse(int n, int k)
        {
            if (n == 0)
                return UnderUse.Barracks;
            if (n == 2 && k == 0)
                return UnderUse.Counting;

            int roll = c.Next(0, 20);
            if (roll < 7) return UnderUse.Dormitory;
            if (roll < 9) return UnderUse.Kitchen;
            if (roll < 11) return k == 2 ? UnderUse.Hospital : UnderUse.Store;
            if (roll < 12) return UnderUse.School;
            if (roll < 13) return UnderUse.Chapel;
            if (roll < 15) return UnderUse.Workshop;
            if (roll < 17) return UnderUse.Hospital;
            return UnderUse.Fallen;
        }

        /// <summary>A heap where the vault fell in: rock over the street, the hole above it into the ground.</summary>
        private void Rockfall(int x, int vault, int floor)
        {
            int r = 5 + c.Next(0, 5);
            for (int dx = -r - 3; dx <= r + 3; dx++)
            {
                int h = (int)Math.Round(Math.Sqrt(Math.Max(0, (r + 3) * (r + 3) - dx * dx)) * 0.8);
                for (int y = floor - h; y < floor; y++)
                {
                    if (c.TypeAt(x + dx, y) >= 0 && c.TypeAt(x + dx, y) != TileID.Cobweb)
                        continue;
                    c.Block(x + dx, y, c.OneIn(4) ? TileID.GrayBrick : TileID.Stone);
                }

                // The hole above.
                int up = (int)Math.Round(Math.Sqrt(Math.Max(0, r * r - dx * dx)));
                for (int y = vault - 3 - up; y <= vault + 1; y++)
                {
                    c.Erase(x + dx, y);
                    c.Wall(x + dx, y, WallID.DirtUnsafe, NoPaint);
                }
            }
        }

        // ------------------------------------------------------------ a house

        /// <summary>
        /// A house in the Undercity: grey brick, timber floors, no windows. Each storey is fitted
        /// for what the house was for; then the house is let go, a little or a lot.
        /// </summary>
        private void UnderHouse(int x0, int x1, int floor, int storeys, UnderUse use, int depth)
        {
            const int storey = 11;
            int top = floor - storey * storeys - 1;

            if (use == UnderUse.Fallen)
            {
                // Only the stumps of its walls, and what it was made of lying about.
                for (int x = x0; x <= x1; x++)
                {
                    bool wall = x - x0 < 2 || x1 - x < 2;
                    int h = wall ? c.Next(3, 9) : 0;
                    for (int y = floor - h; y < floor; y++)
                        c.Block(x, y, TileID.GrayBrick);
                    if (!wall && c.OneIn(3))
                        c.Place(x, floor - 1, TileID.LargePiles, c.Next(7, 13));
                }
                Back(x0 + 2, floor - 6, x1 - 2, floor - 1, WallID.GrayBrick, NoPaint);
                return;
            }

            ushort skin = depth == 0 ? TileID.GrayBrick : c.OneIn(2) ? TileID.GrayBrick : TileID.RedBrick;
            Box(x0, top, x1, floor - 1, skin);

            for (int s = 0; s < storeys; s++)
            {
                int f = floor - s * storey;
                var r = new Room(x0 + 2, x1 - 2, f - storey + 1, f);
                Hollow(r.X0, r.Top, r.X1, f - 1, WallID.GrayBrick, NoPaint);
                Registers(r, Scheme.Under);

                if (s > 0)
                {
                    Box(r.X0, f, r.X1, f, TileID.WoodBlock);
                    int hx = s % 2 == 1 ? r.X1 - 2 : r.X0;
                    for (int x = hx; x <= hx + 2; x++)
                        c.Platform(x, f, Suite.Wood.Platform);
                    for (int y = f + 3; y < f + storey; y += 3)
                        for (int x = hx; x <= hx + 2; x++)
                            if (c.TypeAt(x, y) < 0)
                                c.Platform(x, y, Suite.Wood.Platform);
                }

                var inner = new Room(r.X0 + 3, r.X1 - 3, r.Top, r.Floor);
                FurnishUnder(inner, use, s);
                if (c.OneIn(2))
                    StashSomewhere(inner, Loot.Ruin, Suite.Wood);
                Adorn(inner, use == UnderUse.Chapel ? Decor.Chapel : use == UnderUse.School ? Decor.Scholar
                    : use == UnderUse.Barracks ? Decor.Military : c.OneIn(2) ? Decor.Home : Decor.Ruin, 12, 5);
            }

            // Doors both ends.
            DoorOnto(x0, x0 + 1, floor, x0);
            Air(x1 - 1, floor - 3, x1, floor - 1);
            Reveal(x1 - 1, floor - 3, x1, floor - 1);
            c.Place(x1, floor - 1, TileID.ClosedDoor, Suite.Wood.Door);

            // A flat roof with a low parapet.
            Box(x0, top, x1, top, TileID.StoneSlab);
            c.Block(x0, top - 1, TileID.GrayBrick);
            c.Block(x1, top - 1, TileID.GrayBrick);

            // The letting-go.
            Ruin(x0, top - 1, x1, floor - 1, 0.25 + 0.2 * depth);
        }

        /// <summary>Fits one room of an Undercity house.</summary>
        private void FurnishUnder(Room r, UnderUse use, int s)
        {
            if (r.W < 6)
                return;

            switch (use)
            {
                case UnderUse.Dormitory:
                case UnderUse.Barracks:
                    // Bunks: a bed on the floor, a shelf of boards over it, a bed on that, and
                    // on a tall enough room a third.
                    for (int x = r.X0; x + 3 <= r.X1; x += 5)
                    {
                        c.Place(x, r.Stand, TileID.Beds, Suite.Wood.Bed, (x / 5) % 2 == 0 ? 1 : -1);
                        for (int tier = 1; tier <= (r.H >= 10 ? 2 : 1); tier++)
                        {
                            int py = r.Stand - 3 * tier + 1;
                            for (int px = x; px <= x + 3; px++)
                                if (c.TypeAt(px, py) < 0)
                                    c.Platform(px, py, Suite.Wood.Platform);
                            c.Place(x, py - 1, TileID.Beds, Suite.Wood.Bed, 1);
                        }
                    }
                    if (use == UnderUse.Barracks)
                        c.Place(r.X1 - 2, r.Stand - 3, TileID.Painting3X3, 44);
                    HangAt(r.Mid, r.Stand - 7, TileID.HangingLanterns, Suite.Wood.Lantern);
                    break;

                case UnderUse.Kitchen:
                    Along(r, 0, 1, 2,
                        (TileID.Fireplace, 0, 1), (TileID.CookingPots, 0, 1), (TileID.CookingPots, 1, 1),
                        (TileID.Tables, Suite.Wood.Table, 1), (TileID.Kegs, 0, 1), (TileID.Kegs, 0, 1));
                    HangAt(r.Mid, r.Stand - 4, TileID.HangingLanterns, Suite.Wood.Lantern);
                    break;

                case UnderUse.Hospital:
                    for (int x = r.X0; x + 3 <= r.X1; x += 6)
                    {
                        c.Place(x, r.Stand, TileID.Beds, Suite.Wood.Bed, 1);
                        if (x + 4 <= r.X1)
                            c.Place(x + 4, r.Stand, TileID.Bottles, c.Next(0, 3));
                    }
                    HangAt(r.Mid, r.Stand - 4, TileID.HangingLanterns, Suite.Wood.Lantern);
                    break;

                case UnderUse.School:
                    c.Place(r.X0, r.Stand, TileID.Bookcases, 0);
                    for (int x = r.X0 + 5; x <= r.X1; x += 3)
                        c.Place(x, r.Stand, TileID.Chairs, Suite.Wood.Chair, -1);
                    HangAt(r.Mid, r.Stand - 4, TileID.HangingLanterns, Suite.Wood.Lantern);
                    break;

                case UnderUse.Counting:
                    stashKind = Loot.Ruin;
                    stashSuite = Suite.Wood;
                    Along(r, 0, 1, 2,
                        (TileID.Chairs, Suite.Wood.Chair, 1), (TileID.Tables, Suite.Wood.Table, 1),
                        (TileID.Containers, Suite.Wood.Chest, 1), (TileID.Bookcases, 0, 1));
                    HangAt(r.Mid, r.Stand - 4, TileID.HangingLanterns, Suite.Wood.Lantern);
                    break;

                case UnderUse.Workshop:
                    Along(r, 0, 1, 2,
                        (TileID.WorkBenches, 0, 1), (TileID.Anvils, 0, 1), (TileID.Furnaces, 0, 1),
                        (TileID.Sawmill, 0, 1), (TileID.Kegs, 0, 1));
                    HangAt(r.Mid, r.Stand - 4, TileID.HangingLanterns, Suite.Wood.Lantern);
                    break;

                case UnderUse.Chapel:
                    for (int x = r.X0; x + 2 <= r.X1 - 4; x += 4)
                        c.Place(x, r.Stand, TileID.Benches, 0);
                    c.Place(r.X1 - 1, r.Stand, TileID.Candelabras, 0);
                    break;

                default:
                    Furnish(r, Theme.Store, Suite.Wood);
                    break;
            }
        }

        /// <summary>
        /// Lets a stretch of building go: holes knocked through walls and floors, the walls
        /// behind them gone to bare earth, webs, and what the rooms held broken on the floors.
        /// </summary>
        private void Ruin(int x0, int y0, int x1, int y1, double amount)
        {
            int area = (x1 - x0 + 1) * (y1 - y0 + 1);
            int holes = (int)(area * amount / 60.0);

            for (int i = 0; i < holes; i++)
            {
                int hx = c.Next(x0, x1 + 1), hy = c.Next(y0, y1 + 1);
                int r = 1 + c.Next(0, 3);
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (dx * dx + dy * dy > r * r + 1)
                            continue;
                        int x = hx + dx, y = hy + dy;
                        if (x < x0 || x > x1 || y < y0 || y > y1)
                            continue;
                        int t = c.TypeAt(x, y);
                        if (t == TileID.GrayBrick || t == TileID.RedBrick || t == TileID.WoodBlock || t == TileID.StoneSlab)
                            c.Erase(x, y);
                        if (c.WallAt(x, y) != 0)
                            c.Wall(x, y, WallID.DirtUnsafe, NoPaint);
                    }
            }

            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (c.TypeAt(x, y) < 0 && c.WallAt(x, y) != 0 && c.OneIn(40))
                        c.Block(x, y, TileID.Cobweb);
        }

        // ------------------------------------------------------------ stairwells

        /// <summary>
        /// A stairwell from one street down to the next: a brick shaft, flights of five with
        /// landings, and on its wall what the clerk wrote, if anything.
        /// </summary>
        private void Stairwell(int x, int upper, int lower, string text)
        {
            int s0 = x, s1 = x + 6;

            Box(s0 - 2, upper - 5, s1 + 2, lower + 1, TileID.GrayBrick);
            Hollow(s0, upper - 4, s1, lower - 1, WallID.GrayBrick, NoPaint);

            // Openings into both streets.
            for (int y = upper - 4; y <= upper - 1; y++)
                for (int xx = s0 - 2; xx <= s1 + 2; xx++)
                    if (c.TypeAt(xx, y) >= 0)
                        c.Erase(xx, y);
            for (int xx = s0; xx <= s1; xx++)
                c.Platform(xx, upper, Suite.Wood.Platform);
            Hollow(s0 - 2, lower - 4, s0 - 1, lower - 1, WallID.GrayBrick, NoPaint);
            Hollow(s1 + 1, lower - 4, s1 + 2, lower - 1, WallID.GrayBrick, NoPaint);

            int y0 = lower;
            bool east = true;
            while (y0 - 6 > upper)
            {
                for (int i = 1; i <= 5; i++)
                {
                    int xx = east ? s0 + i - 1 : s1 - i + 1;
                    c.Platform(xx, y0 - i, Suite.Wood.Platform, east ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
                }
                y0 -= 6;
                for (int xx = s0; xx <= s1; xx++)
                    if (c.TypeAt(xx, y0) < 0)
                        c.Platform(xx, y0, Suite.Wood.Platform);
                east = !east;
            }

            HangAt(s0 + 3, upper + 8, TileID.HangingLanterns, Suite.Wood.Lantern);

            // What the clerk wrote goes on the wall by the head of the stair, at eye height.
            if (text != null)
            {
                SignNear(s1 + 3, upper - 2, text);
            }
        }

        // ------------------------------------------------------------ the lock under the market

        /// <summary>
        /// Under the plug of the market's shaft, at the top of the first street's vault: a
        /// small room with a ladder down to the street, and a lever in it that opens the plug.
        /// The plug is set on actuators; the lever throws them.
        /// </summary>
        private void MarketLock()
        {
            var (floor, x0, x1) = streets[0];
            int vault = floor - UnderHeight;
            int px = marketPlugX;
            if (px == 0 || px < x0 || px > x1)
                return;

            const int plugTop = 56, plugBottom = 58;
            int roomTop = plugBottom + 1, roomFloor = plugBottom + 6;

            // The room under the plug: brick round it, a floor of boards with a hatch, a ladder
            // down through the vault to the street.
            Box(px - 5, roomTop - 1, px + 6, roomFloor + 1, TileID.GrayBrick);
            Hollow(px - 4, roomTop, px + 5, roomFloor - 1, WallID.GrayBrick, NoPaint);
            for (int x = px - 4; x <= px + 5; x++)
                c.Platform(x, roomFloor, Suite.Wood.Platform);
            for (int y = roomFloor + 1; y < vault; y++)
                for (int x = px; x <= px + 1; x++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.GrayBrick, NoPaint);
                }
            for (int y = roomFloor + 3; y < floor - 1; y += 3)
                for (int x = px; x <= px + 1; x++)
                    if (c.TypeAt(x, y) < 0)
                        c.Platform(x, y, Suite.Wood.Platform);

            // The plug opens from here: actuators in it, a wire from the lever to them.
            for (int x = px; x <= px + 1; x++)
                for (int y = plugTop; y <= plugBottom; y++)
                {
                    c.Actuate(x, y);
                    c.Wire(x, y);
                }

            int lx = px + 3, ly = roomFloor - 1;
            c.Place(lx, ly, TileID.Lever, 0);
            for (int y = plugBottom + 1; y <= ly; y++)
                c.Wire(px + 1, y);
            for (int x = px + 1; x <= lx; x++)
                c.Wire(x, ly);
            c.Mark("Grace.MarketLever", lx, ly - 1, 2, 2);
            HangAt(px - 3, roomFloor - 2, TileID.HangingLanterns, Suite.Wood.Lantern);
        }

        // ------------------------------------------------------------ the sally port

        /// <summary>
        /// From the first street's east end a passage climbs east and up to a door in the wall
        /// of the harbour basin: the way the Undercity came and went from the Shore. On the way
        /// it crosses the foot of the Font's shaft, level, a door on either side: the shaft
        /// comes down from the chapel and ends on the passage's floor, and nothing is under it.
        /// </summary>
        private void SallyPort(int streetX1, int floor)
        {
            int x = streetX1 + 1;
            int y = floor - 1;
            int target = BasinFloor - 1;
            int basinWest = ShoreX + 36 + 26;     // the basin's first open column; its wall is the two before it
            int crossY = int.MinValue;

            while (x < basinWest - 2)
            {
                bool near = x >= ShaftX0 - 6 && x <= ShaftX1 + 6;
                if (!near && y > target && (x % 2 == 0))
                    y--;

                if (x >= ShaftX0 && x <= ShaftX1)
                {
                    // The foot of the shaft: its floor, level with the passage's, and the rest
                    // of what was dug of it under that filled back in.
                    c.Block(x, y + 1, TileID.StoneSlab, White);
                    for (int yy = y + 2; yy <= FontShaftFoot; yy++)
                        c.Block(x, yy, TileID.MarbleBlock, White);
                    crossY = y;
                }
                else
                {
                    for (int h = 0; h < 4; h++)
                    {
                        c.Erase(x, y - h);
                        c.Wall(x, y - h, WallID.GrayBrick, NoPaint);
                    }
                    c.Block(x, y + 1, TileID.StoneSlab);
                    c.Block(x, y - 4, TileID.GrayBrick);

                    if (x % 15 == 0 && !near)
                        HangAt(x, y - 1, TileID.HangingLanterns, Suite.Wood.Lantern);
                }

                x++;
            }

            // The doors either side of the shaft, in its lining. If the passage never came by
            // the shaft, the shaft ends on a floor of its own.
            int shaftFloor;
            if (crossY != int.MinValue)
            {
                foreach (int dx in new[] { ShaftX0 - 1, ShaftX1 + 1 })
                {
                    c.Block(dx, crossY - 3, TileID.GrayBrick);
                    c.Place(dx, crossY, TileID.ClosedDoor, Suite.Wood.Door);
                }
                shaftFloor = crossY + 1;
            }
            else
            {
                shaftFloor = FontShaftFoot;
                for (int sx = ShaftX0; sx <= ShaftX1; sx++)
                    c.Block(sx, shaftFloor, TileID.StoneSlab, White);
            }
            c.Mark("Grace.FontShaft", ShaftX0, FontHead - 4, ShaftX1 - ShaftX0 + 1, shaftFloor - (FontHead - 4));

            // The door in the basin wall: a doorway through both its courses, the door in the
            // inner one under the wall's own brick.
            for (int xx = basinWest - 2; xx <= basinWest - 1; xx++)
                for (int h = 0; h < 3; h++)
                {
                    c.Erase(xx, y - h);
                    c.Wall(xx, y - h, WallID.GrayBrick, NoPaint);
                }
            c.Place(basinWest - 2, y, TileID.ClosedDoor, Suite.Wood.Door);
        }
    }

    /// <summary>What the clerk wrote in the stairwells (PAD_Grace_Structure_Plan.md §4, draft).</summary>
    public static class UnderText
    {
        public const string CountedBack = "Counted out at the gate. Counted back at dusk.";
        public const string CountedOut = "Counted out.";
    }
}
