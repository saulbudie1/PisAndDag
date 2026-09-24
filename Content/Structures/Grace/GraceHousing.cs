using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// Nobody moves into the capital.
    ///
    /// A town NPC will take any room the game counts as a house: a pocket of open tiles closed
    /// by solid tiles, doors and platforms, between sixty and seven hundred and forty-nine
    /// tiles of it, walls a player could have put up never more than two tiles from any point
    /// of it along a row or a column, and in it or round it something to sit on, a table, a
    /// light and a way in. This finds every room the way the game does (WorldGen.StartRoomCheck,
    /// CheckRoom and RoomNeeds in 1.4.4) and makes every one that would pass fail by taking its
    /// chairs out; where there is still something to sit on, a bed or a throne, its tables go
    /// instead. Nothing else in the room is touched.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        // TileID.Sets.RoomNeeds in tModLoader 1.4.4, read from the assembly.
        private static readonly HashSet<int> SitTypes = new HashSet<int>
        {
            TileID.Chairs, TileID.Beds, TileID.Benches, TileID.Thrones, TileID.PicnicTable, TileID.Toilets,
        };

        private static readonly HashSet<int> TableTypes = new HashSet<int>
        {
            TileID.Tables, TileID.WorkBenches, TileID.Pianos, TileID.Dressers, TileID.Bathtubs, TileID.Bookcases,
            TileID.BewitchingTable, TileID.AlchemyTable, TileID.WarTable, TileID.Tables2, TileID.PicnicTable,
        };

        private static readonly HashSet<int> LightTypes = new HashSet<int>
        {
            TileID.Torches, TileID.Candles, TileID.Chandeliers, TileID.Jackolanterns, TileID.HangingLanterns, TileID.WaterCandle,
            TileID.Lamps, TileID.ChineseLanterns, TileID.SkullLanterns, TileID.Candelabras, TileID.HolidayLights,
            TileID.PlatinumCandelabra, TileID.PlatinumCandle, TileID.FireflyinaBottle, TileID.LightningBuginaBottle,
            TileID.BlueJellyfishBowl, TileID.GreenJellyfishBowl, TileID.PinkJellyfishBowl, TileID.Lampposts, TileID.PeaceCandle,
            TileID.ShadowCandle, TileID.Fireplace, TileID.BrazierSuspended, TileID.SoulBottles, TileID.LavaflyinaBottle,
            TileID.ShimmerflyinaBottle,
        };

        private static readonly HashSet<int> WayTypes = new HashSet<int>
        {
            TileID.ClosedDoor, TileID.OpenDoor, TileID.Platforms, TileID.TrapdoorClosed, TileID.TrapdoorOpen,
            TileID.TallGateClosed, TileID.TallGateOpen, TileID.TeamBlockBluePlatform, TileID.TeamBlockGreenPlatform,
            TileID.TeamBlockPinkPlatform, TileID.TeamBlockRedPlatform, TileID.TeamBlockWhitePlatform, TileID.TeamBlockYellowPlatform,
        };

        /// <summary>The seats that can go without taking the room's purpose with them: not the beds, not the throne.</summary>
        private static readonly HashSet<int> LooseSeatTypes = new HashSet<int>
        {
            TileID.Chairs, TileID.Benches, TileID.PicnicTable, TileID.Toilets,
        };

        /// <summary>How many rooms the last build took a chair or a table out of. For the log.</summary>
        public static int Unhoused { get; private set; }

        private void Unhouse(int x0, int y0, int x1, int y1)
        {
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var seen = new bool[w * h];
            var stack = new Stack<int>();
            var cells = new List<int>();
            Unhoused = 0;

            for (int sx = x0; sx <= x1; sx++)
            {
                for (int sy = y0; sy <= y1; sy++)
                {
                    int s = (sx - x0) * h + (sy - y0);
                    if (seen[s])
                        continue;
                    seen[s] = true;
                    if (c.Blocks(sx, sy))
                        continue;

                    // One pocket of open tiles, eight ways round, the way the game walks a room.
                    cells.Clear();
                    bool open = false;
                    int count = 0;
                    stack.Push(s);

                    while (stack.Count > 0)
                    {
                        int k = stack.Pop();
                        int x = x0 + k / h, y = y0 + k % h;
                        count++;
                        if (count < 750)
                            cells.Add(k);

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0)
                                    continue;

                                int nx = x + dx, ny = y + dy;
                                if (nx < x0 || nx > x1 || ny < y0 || ny > y1)
                                {
                                    open = true;
                                    continue;
                                }

                                int n = (nx - x0) * h + (ny - y0);
                                if (seen[n])
                                    continue;
                                seen[n] = true;
                                if (!c.Blocks(nx, ny))
                                    stack.Push(n);
                            }
                        }
                    }

                    if (!open && count >= 60 && count < 750 && WouldHouse(cells, x0, y0, h))
                    {
                        Unfurnish(cells, x0, y0, h);
                        Unhoused++;
#if PREVIEW
                        System.Console.WriteLine($"unhoused room at {sx},{sy} ({count} tiles)");
#endif
                    }
                }
            }
        }

        /// <summary>This pocket passes the game's test for a house.</summary>
        private bool WouldHouse(List<int> cells, int x0, int y0, int h)
        {
            var have = new HashSet<int>();

            foreach (int k in cells)
            {
                int x = x0 + k / h, y = y0 + k % h;

                // Walls near enough on the row and on the column.
                bool row = false, col = false;
                for (int i = -2; i <= 2; i++)
                {
                    row |= c.HouseWall(x + i, y) || c.Blocks(x + i, y);
                    col |= c.HouseWall(x, y + i) || c.Blocks(x, y + i);
                }
                if (!row || !col)
                    return false;

                // What is in the room, and what closes it.
                int t = c.TypeAt(x, y);
                if (t >= 0)
                    have.Add(t);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (c.Blocks(x + dx, y + dy))
                            have.Add(c.TypeAt(x + dx, y + dy));
            }

            return have.Overlaps(SitTypes) && have.Overlaps(TableTypes) && have.Overlaps(LightTypes) && have.Overlaps(WayTypes);
        }

        /// <summary>Takes the chairs out of a room; if it still has a bed or a throne, the tables instead.</summary>
        private void Unfurnish(List<int> cells, int x0, int y0, int h)
        {
            bool seated = false;

            foreach (int k in cells)
            {
                int x = x0 + k / h, y = y0 + k % h;
                int t = c.TypeAt(x, y);
                if (LooseSeatTypes.Contains(t))
                    c.Erase(x, y);
                else if (SitTypes.Contains(t))
                    seated = true;
            }

            if (!seated)
                return;

            foreach (int k in cells)
            {
                int x = x0 + k / h, y = y0 + k % h;
                if (TableTypes.Contains(c.TypeAt(x, y)))
                    c.Erase(x, y);
            }
        }
    }
}
