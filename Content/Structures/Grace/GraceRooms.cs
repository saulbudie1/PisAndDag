using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>A room: its inside columns, its first row under the ceiling, and the row of its floor.</summary>
    public readonly struct Room
    {
        public readonly int X0, X1, Top, Floor;

        public Room(int x0, int x1, int top, int floor)
        {
            X0 = x0;
            X1 = x1;
            Top = top;
            Floor = floor;
        }

        public int W => X1 - X0 + 1;
        public int H => Floor - Top;
        public int Stand => Floor - 1;
        public int Mid => (X0 + X1) / 2;
    }

    /// <summary>A furniture set: the styles of one material across every piece.</summary>
    public sealed class Suite
    {
        public int Chair, Table, Bed, Door, Chandelier, Lantern, Bookcase, Piano, Clock, Sofa, Sink,
            Bathtub, Dresser, Bench, Chest, Candle, Candelabra, Lamp, Platform;

        /// <summary>White marble: the good rooms of the Low City and the Curtain.</summary>
        public static readonly Suite Marble = new Suite
        {
            Chair = 35, Table = 34, Bed = 30, Door = 35, Chandelier = 36, Lantern = 36, Bookcase = 31, Piano = 29,
            Clock = 27, Sofa = 32, Sink = 31, Bathtub = 30, Dresser = 27, Bench = 32, Chest = 51, Candle = 29,
            Candelabra = 30, Lamp = 30, Platform = 29,
        };

        /// <summary>Gold: the court.</summary>
        public static readonly Suite Gold = new Suite
        {
            Chair = 19, Table = 18, Bed = 10, Door = 21, Chandelier = 20, Lantern = 24, Bookcase = 20, Piano = 29,
            Clock = 27, Sofa = 32, Sink = 31, Bathtub = 30, Dresser = 27, Bench = 32, Chest = 1, Candle = 16,
            Candelabra = 15, Lamp = 15, Platform = 31,
        };

        /// <summary>Plain wood: the almshouses and the Undercity.</summary>
        public static readonly Suite Wood = new Suite
        {
            Chair = 0, Table = 0, Bed = 0, Door = 0, Chandelier = 2, Lantern = 1, Bookcase = 0, Piano = 0,
            Clock = 0, Sofa = 0, Sink = 0, Bathtub = 0, Dresser = 0, Bench = 0, Chest = 0, Candle = 0,
            Candelabra = 0, Lamp = 0, Platform = 0,
        };
    }

    public enum Theme
    {
        Hall,
        Barracks,
        Armoury,
        Store,
        Chapel,
        Kitchen,
        Library,
        Study,
        Bedroom,
        Dormitory,
        Infirmary,
        School,
        Guard,
        Bare,
    }

    public sealed partial class GraceBuilder
    {
        /// <summary>What the chests placed by <see cref="Along"/> hold, and what they are made of: set by whoever is furnishing.</summary>
        private Loot stashKind = Loot.Store;
        private Suite stashSuite = Suite.Wood;

        /// <summary>
        /// A chest somewhere along a room's floor, trying from the ends inward: where two
        /// tiles of floor are clear and solid, and not over a hatch. False if there is no room.
        /// </summary>
        private bool StashSomewhere(Room r, Loot kind, Suite s)
        {
            for (int k = 0; k <= r.W / 2; k++)
            {
                foreach (int x in new[] { r.X0 + k, r.X1 - 1 - k })
                {
                    if (x < r.X0 || x + 1 > r.X1)
                        continue;
                    if (c.TypeAt(x, r.Stand) >= 0 || c.TypeAt(x + 1, r.Stand) >= 0
                        || c.TypeAt(x, r.Stand - 1) >= 0 || c.TypeAt(x + 1, r.Stand - 1) >= 0)
                        continue;
                    if (c.TypeAt(x, r.Floor) == TileID.Platforms || c.TypeAt(x + 1, r.Floor) == TileID.Platforms)
                        continue;
                    if (!c.Solid(x, r.Floor) || !c.Solid(x + 1, r.Floor))
                        continue;
                    if (Stash(x, r.Stand, kind, s))
                        return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------ lighting

        /// <summary>Lamps down the ceiling of a room, one every <paramref name="every"/> columns.</summary>
        private void Lights(Room r, Suite s, int every = 9, bool chandeliers = false)
        {
            int n = Math.Max(1, (r.W - 2) / every);
            double step = (r.W) / (double)(n + 1);

            for (int i = 1; i <= n; i++)
            {
                int x = r.X0 + (int)Math.Round(step * i);
                int from = Math.Max(r.Top, r.Floor - 4);
                if (chandeliers && r.H >= 7)
                    HangAt(x - 1, from, TileID.Chandeliers, s.Chandelier);
                else
                    HangAt(x, from, TileID.HangingLanterns, s.Lantern);
            }
        }

        // ------------------------------------------------------------ the floor, walked

        /// <summary>
        /// Things along a floor, left to right with gaps, each one a (type, style, dir)
        /// triple, until the room runs out. Anything that will not go is skipped.
        /// </summary>
        private void Along(Room r, int margin, int gapMin, int gapMax, params (ushort type, int style, int dir)[] items)
        {
            int x = r.X0 + margin;

            foreach (var (type, style, dir) in items)
            {
                var (w, h) = c.Size(type, style);

                if (x + w - 1 > r.X1 - margin)
                    break;

                if (h <= r.H)
                {
                    if (type == TileID.Containers || type == TileID.Containers2)
                        Stash(x, r.Stand, stashKind, stashSuite);
                    else
                        c.Place(x, r.Stand, type, style, dir);
                }

                x += w + c.Next(gapMin, gapMax + 1);
            }
        }

        /// <summary>A table with a chair drawn up to each side, and something on the table.</summary>
        private int DiningSet(int x, int stand, Suite s, bool dressed = true)
        {
            c.Place(x, stand, TileID.Chairs, s.Chair, 1);
            c.Place(x + 1, stand, TileID.Tables, s.Table);
            c.Place(x + 4, stand, TileID.Chairs, s.Chair, -1);

            if (dressed)
            {
                int top = stand - 2;
                if (c.OneIn(2))
                    c.Place(x + 2, top, TileID.Candles, s.Candle);
                else
                    c.Place(x + 2, top, TileID.Bottles, c.Next(0, 3));
            }

            return x + 5;
        }

        // ------------------------------------------------------------ the rooms

        /// <summary>Fit a room out for what it was. Lights always; the rest by theme.</summary>
        private void Furnish(Room r, Theme theme, Suite s)
        {
            if (r.W < 5 || r.H < 4)
                return;

            stashKind = LootFor(theme, s);
            stashSuite = s;

            switch (theme)
            {
                case Theme.Barracks:
                case Theme.Dormitory:
                {
                    int x = r.X0 + 1;
                    while (x + 4 <= r.X1)
                    {
                        c.Place(x, r.Stand, TileID.Beds, s.Bed, c.OneIn(2) ? 1 : -1);
                        x += 5;
                        if (c.OneIn(3) && x + 2 <= r.X1)
                        {
                            Stash(x, r.Stand, Loot.Military, s);
                            x += 3;
                        }
                    }
                    Lights(r, s, 10);
                    break;
                }

                case Theme.Armoury:
                {
                    int x = r.X0 + 1;

                    // The racks' chest first: what was not on a stand (GraceLoot.StashArmoury).
                    if (x + 2 <= r.X1 && StashArmoury(x, r.Stand, s))
                        x += 3;

                    while (x + 2 <= r.X1)
                    {
                        if (c.OneIn(2))
                            c.Place(x, r.Stand, TileID.Statues, 3);
                        else
                            c.Place(x, r.Stand, TileID.Statues, c.OneIn(2) ? 0 : 6);
                        x += 3;
                    }
                    for (int wx = r.X0 + 2; wx + 2 <= r.X1 && r.H >= 9; wx += 6)
                        c.Place(wx, r.Top + 4, TileID.Painting3X3, c.Pick(41, 43, 44, 45));
                    Lights(r, s, 8);
                    break;
                }

                case Theme.Store:
                {
                    int x = r.X0 + 1;
                    while (x + 2 <= r.X1)
                    {
                        int pick = c.Next(4);
                        if (pick == 0) c.Place(x, r.Stand, TileID.Kegs, 0);
                        else if (pick == 1) Stash(x, r.Stand, Loot.Store, s);
                        else if (pick == 2) c.Place(x, r.Stand, TileID.FishingCrate, 0);
                        else if (x + 3 <= r.X1) { c.Place(x, r.Stand, TileID.LargePiles, c.Pick(22, 23, 24)); x++; }
                        x += 2 + c.Next(0, 3);
                    }
                    Lights(r, s, 12);
                    break;
                }

                case Theme.Chapel:
                {
                    int x = r.X0 + 2;
                    while (x + 2 < r.X1 - 6)
                    {
                        c.Place(x, r.Stand, TileID.Benches, s.Bench);
                        x += 4;
                    }
                    c.Place(r.X1 - 4, r.Stand, TileID.Candelabras, s.Candelabra);
                    c.Place(r.X1 - 2, r.Stand, TileID.Statues, 1);
                    Lights(r, s, 8, true);
                    break;
                }

                case Theme.Kitchen:
                    Along(r, 1, 1, 2,
                        (TileID.CookingPots, 0, 1), (TileID.Kegs, 0, 1), (TileID.Tables, s.Table, 1),
                        (TileID.Sinks, s.Sink, 1), (TileID.Kegs, 0, 1), (TileID.Containers, s.Chest, 1));
                    Lights(r, s, 8);
                    break;

                case Theme.Library:
                {
                    int x = r.X0 + 1;
                    while (x + 2 <= r.X1)
                    {
                        c.Place(x, r.Stand, TileID.Bookcases, s.Bookcase);
                        x += 3;
                        if (c.OneIn(4) && x + 2 <= r.X1)
                        {
                            c.Place(x, r.Stand, TileID.Chairs, s.Chair, 1);
                            c.Place(x + 1, r.Stand, TileID.Tables, s.Table);
                            c.Place(x + 2, r.Stand - 2, TileID.Books, c.Next(0, 5));
                            x += 5;
                        }
                    }
                    Lights(r, s, 9);
                    break;
                }

                case Theme.Study:
                    Along(r, 1, 1, 2,
                        (TileID.Bookcases, s.Bookcase, 1), (TileID.Chairs, s.Chair, 1), (TileID.Tables, s.Table, 1),
                        (TileID.GrandfatherClocks, s.Clock, 1), (TileID.Bookcases, s.Bookcase, 1));
                    Lights(r, s, 10);
                    break;

                case Theme.Bedroom:
                    Along(r, 1, 1, 3,
                        (TileID.Dressers, s.Dresser, 1), (TileID.Beds, s.Bed, -1), (TileID.Candelabras, s.Candelabra, 1),
                        (TileID.Bookcases, s.Bookcase, 1), (TileID.Chairs, s.Chair, 1));
                    Lights(r, s, 12);
                    break;

                case Theme.Infirmary:
                {
                    int x = r.X0 + 1;
                    while (x + 3 <= r.X1)
                    {
                        c.Place(x, r.Stand, TileID.Beds, s.Bed, 1);
                        x += 4;
                        if (c.OneIn(2) && x + 1 <= r.X1)
                            c.Place(x, r.Stand, TileID.Bottles, c.Next(0, 4));
                        x += 2;
                    }
                    Lights(r, s, 8);
                    break;
                }

                case Theme.School:
                {
                    int x = r.X0 + 2;
                    c.Place(x, r.Stand, TileID.Bookcases, s.Bookcase);
                    x += 5;
                    while (x + 1 <= r.X1 - 1)
                    {
                        c.Place(x, r.Stand, TileID.Chairs, s.Chair, -1);
                        x += 3;
                    }
                    Lights(r, s, 10);
                    break;
                }

                case Theme.Guard:
                    Along(r, 1, 2, 4,
                        (TileID.Kegs, 0, 1), (TileID.Chairs, s.Chair, 1), (TileID.Tables, s.Table, 1),
                        (TileID.Chairs, s.Chair, -1), (TileID.Containers, s.Chest, 1));
                    Lights(r, s, 10);
                    break;

                case Theme.Hall:
                {
                    int x = r.X0 + 2;
                    while (x + 5 <= r.X1 - 1)
                        x = DiningSet(x, r.Stand, s) + c.Next(2, 5);
                    Lights(r, s, 10, true);
                    break;
                }

                default:
                    Lights(r, s, 12);
                    break;
            }
        }

        // ------------------------------------------------------------ neglect

        /// <summary>Webs in the top corners of a room and a little rubble on its floor.</summary>
        private void Neglect(Room r, int webs = 3, int rubble = 1)
        {
            for (int k = 0; k < webs; k++)
            {
                bool left = c.OneIn(2);
                int x = left ? r.X0 : r.X1;
                int dir = left ? 1 : -1;
                int size = c.Next(2, 5);

                for (int i = 0; i < size; i++)
                    for (int j = 0; j < size - i; j++)
                    {
                        int xx = x + i * dir, yy = r.Top + j;
                        if (c.TypeAt(xx, yy) < 0 && !c.OneIn(4))
                            c.Block(xx, yy, TileID.Cobweb);
                    }
            }

            for (int k = 0; k < rubble; k++)
            {
                int x = c.Next(r.X0 + 1, Math.Max(r.X0 + 2, r.X1 - 3));
                if (c.OneIn(2))
                    c.Place(x, r.Stand, TileID.LargePiles2, c.Next(10, 13));
                else
                    c.Place(x, r.Stand, TileID.LargePiles, c.Next(7, 13));
            }
        }
    }
}
