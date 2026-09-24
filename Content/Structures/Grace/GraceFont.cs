using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The Font. A chapel on the Ward's terrace, domed, lit through coloured glass, with
    /// a font of water under the dome and a stair at its west end going down. The stair
    /// comes down under the terrace to the head of the shaft, and the shaft goes down to
    /// the Undercity's first street and ends there.
    ///
    /// The chamber is at the bottom of everything, just over the Shimmer's own room: a hall
    /// a hundred and sixty wide and sixty-four high, galleries of shelves down both its long
    /// sides full of ewers and jugs, and in the middle of its floor, boarded over flush with
    /// the stone, the head of the short shaft down to the Shimmer. A line is worn into the
    /// floor from the boards to the door. It has one door, at the west end of the floor, and
    /// the only way to it is the crack under the Reliquary's Crypt (<see cref="Fissure"/>).
    /// It is also where the Cupbearer is fought, and the room is built for that without
    /// looking it.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>The Font chamber's floor row: deep, on the Shimmer room's walls.</summary>
        private int ArenaFloor => RootCeil - RootShell - 6;

        /// <summary>The Font chamber's first open row under its ceiling.</summary>
        private int ArenaCeil => ArenaFloor - 63;

        /// <summary>Where the stair from the chapel comes down to under the terrace: the floor of the head of the shaft.</summary>
        private const int FontHead = 20;

        /// <summary>
        /// How far the shaft from the chapel is dug: to the Undercity's first street. The sally
        /// port crosses it there and lays its floor (<see cref="SallyPort"/>).
        /// </summary>
        private const int FontShaftFoot = FirstStreetFloor;

        /// <summary>How thick the Font chamber's walls are, and so how long its doorway.</summary>
        private const int ArenaWall = 6;

        /// <summary>The column just outside the Font chamber's door, where the crack comes out.</summary>
        private int ArenaDoorX => FontX + ArenaInset - ArenaWall - 1;

        /// <summary>
        /// The columns of the Font's shafts, the one from the chapel and the short one from the
        /// chamber's floor to the Shimmer, over the middle of the chamber: set by
        /// <see cref="Font"/>, read by the Undercity and the deep builders.
        /// </summary>
        private int ShaftX0, ShaftX1;

        private void Font()
        {
            int x0 = FontX, x1 = ShoreX - 1;
            int ar0 = x0 + ArenaInset, ar1 = ar0 + ArenaW - 1;

            // Clear above the terrace, then the terrace solid down to under the head of the shaft.
            for (int x = x0; x <= x1; x++)
                for (int y = -site.Top; y < WardFloor; y++)
                {
                    c.Erase(x, y);
                    c.EraseWall(x, y);
                }

            Box(x0, WardFloor, x1, FontHead + 6, TileID.MarbleBlock, White);
            Box(x0, WardFloor, x1, WardFloor, TileID.StoneSlab, White);

            int mx = (ar0 + ar1) / 2;
            ShaftX0 = mx - 3;
            ShaftX1 = mx + 3;

            FontChapel(x0 + 4, x0 + 127);
            FontTerrace(x0 + 128, x1);

            // The way down: the shaft first, so the passages cut into its lining. It is marked
            // once the sally port has given it a floor.
            FontShaft(FontHead - 4, FontShaftFoot, null);
            FontStair(x0 + 10);
            FontChamber(ar0, ar1);
        }

        // ------------------------------------------------------------ the chamber

        private void FontChamber(int a0, int a1)
        {
            int mx = (a0 + a1) / 2;
            var hall = new Room(a0, a1, ArenaCeil, ArenaFloor);

            // The chamber's block of white stone.
            Box(a0 - ArenaWall, ArenaCeil - 8, a1 + ArenaWall, ArenaFloor + 6, TileID.MarbleBlock, White);
            deepParts.Add((a0 - ArenaWall, ArenaCeil - 8, a1 + ArenaWall, ArenaFloor + 6));
            Hollow(a0, ArenaCeil, a1, ArenaFloor - 1, WallID.MarbleBlock, White);
            Box(a0, ArenaFloor, a1, ArenaFloor, TileID.StoneSlab, White);

            // The ceiling: flat, coffered in gold between white beams.
            for (int x = a0; x <= a1; x++)
            {
                bool beam = (x - a0) % 10 < 2;
                c.Block(x, ArenaCeil - 1, beam ? TileID.MarbleBlock : TileID.AncientGoldBrick, beam ? White : NoPaint);
            }
            for (int x = a0 + 1; x <= a1 - 1; x += 10)
            {
                c.Block(x, ArenaCeil, TileID.MarbleBlock, White, Grace.Cut.UpRight);
                c.Block(x + 1, ArenaCeil, TileID.MarbleBlock, White, Grace.Cut.UpLeft);
            }

            // The back wall: a dado, pilasters every twenty, a gold frieze under the ceiling.
            Back(a0, ArenaFloor - 3, a1, ArenaFloor - 1, WallID.StoneSlab, White);
            Back(a0, ArenaCeil + 1, a1, ArenaCeil + 2, WallID.AncientGoldBrickWall, NoPaint);
            for (int x = a0 + 9; x <= a1 - 9; x += 20)
                Back(x, ArenaCeil + 3, x + 1, ArenaFloor - 4, WallID.PearlstoneBrick, White);

            // The galleries: three long shelves the whole length, the vessels stood along them.
            int[] shelves = { ArenaFloor - 14, ArenaFloor - 28, ArenaFloor - 42 };
            foreach (int y in shelves)
            {
                for (int x = a0; x <= a1; x++)
                    c.Platform(x, y, Suite.Gold.Platform);
                Back(a0, y + 1, a1, y + 1, WallID.AncientGoldBrickWall, NoPaint);

                for (int x = a0 + 4; x <= a1 - 4; x += 7)
                {
                    if (Math.Abs(x - mx) < 8)
                        continue;
                    int k = (x + y) % 5;
                    c.Place(x, y - 1, TileID.Bottles, k == 0 ? 3 : k == 1 ? 4 : 0);
                }

                for (int x = a0 + 13; x <= a1 - 11; x += 42)
                    if (c.TypeAt(x, y - 1) < 0)
                        c.Place(x, y - 1, TileID.Lamps, Suite.Gold.Lamp);
            }

            // Chandeliers from the ceiling, between the beams.
            for (int x = a0 + 15; x <= a1 - 15; x += 20)
                Hang(x - 1, ArenaCeil, TileID.Chandeliers, Suite.Gold.Chandelier);

            // Every light in the hall is out. The chandeliers and the lamps are all still
            // there, and nobody has lit one in a long time.
            c.Snuff(a0, ArenaCeil, a1, ArenaFloor - 1);

            // The shaft head: boards flush with the floor, the width of the shaft.
            for (int x = ShaftX0; x <= ShaftX1; x++)
                c.Block(x, ArenaFloor, TileID.WoodBlock);
            c.Mark("Grace.ShaftHead", ShaftX0, ArenaFloor, ShaftX1 - ShaftX0 + 1, 1);
            c.Sign(ShaftX1 + 3, ArenaFloor - 3, TileID.Signs, 0, FontText.ShaftHead);

            // The trolley just inside the door, with the one ewer on it, and beside the ewer
            // the only light in the room: a candle somebody keeps.
            c.Place(a0 + 1, ArenaFloor - 1, TileID.Tables, Suite.Marble.Table);
            c.Place(a0 + 2, ArenaFloor - 3, TileID.Bottles, 3);
            c.Place(a0 + 3, ArenaFloor - 3, TileID.Candles, Suite.Marble.Candle);
            c.Mark("Grace.Trolley", a0 + 1, ArenaFloor - 2, 3, 2);

            // The worn line: a groove half a tile deep from the trolley to the boards.
            for (int x = a0 + 5; x < ShaftX0; x++)
                c.Block(x, ArenaFloor, TileID.StoneSlab, Gray, Grace.Cut.Half);

            c.Mark("Grace.Arena", a0, ArenaCeil, a1 - a0 + 1, ArenaFloor - ArenaCeil);

            // The one door: at the west end of the floor, through the wall to where the crack
            // under the Crypt comes out. Locked until somebody brings the key (GraceSeal).
            DoorOnto(a0 - ArenaWall, a0 - 1, ArenaFloor, a0 - 1);
            c.Mark("Grace.ArenaDoor", a0 - 1, ArenaFloor - 3, 1, 3);
        }

        // ------------------------------------------------------------ the stair down

        /// <summary>
        /// The way down from the chapel: a stair in a shaft from the chapel's floor down the
        /// terrace, in flights of five with landings, and at its foot a passage east under the
        /// chapel to the head of the shaft down to the Undercity.
        /// </summary>
        private void FontStair(int s0)
        {
            int s1 = s0 + 6;
            int top = WardFloor, bottom = FontHead;

            Hollow(s0, top, s1, bottom - 1, WallID.StoneSlab, White);

            // Flights: five up east, a landing, five up west, a landing; from the bottom.
            int y = bottom;
            bool east = true;
            while (y - 6 > top)
            {
                for (int i = 1; i <= 5; i++)
                {
                    int x = east ? s0 + i - 1 : s1 - i + 1;
                    c.Platform(x, y - i, Suite.Marble.Platform, east ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
                }
                y -= 6;
                for (int x = s0; x <= s1; x++)
                    c.Platform(x, y, Suite.Marble.Platform);
                if ((bottom - y) % 24 == 0)
                    Hang(east ? s0 : s1, y + 1, TileID.HangingLanterns, Suite.Marble.Lantern);
                east = !east;
            }

            // The passage to the head of the shaft, and a landing across the shaft at its floor.
            Hollow(s1 + 1, bottom - 4, ShaftX0 - 1, bottom - 1, WallID.StoneSlab, White);
            Registers(new Room(s1 + 1, ShaftX0 - 1, bottom - 4, bottom), Scheme.Service);
            for (int x = s1 + 8; x < ShaftX0 - 3; x += 16)
                HangAt(x, bottom - 2, TileID.HangingLanterns, Suite.Marble.Lantern);
            for (int x = ShaftX0; x <= ShaftX1; x++)
                c.Platform(x, bottom, Suite.Marble.Platform);

            // The shaft's head in the chapel floor: open, with a rail.
            for (int x = s0; x <= s1; x++)
            {
                c.Erase(x, top);
                c.Platform(x, top, Suite.Marble.Platform);
            }
        }

        // ------------------------------------------------------------ the chapel

        /// <summary>
        /// The chapel over the Font: a nave lit through coloured glass, benches down it, the
        /// font of water under the dome, an altar at the east end with an angel over it.
        /// </summary>
        private void FontChapel(int c0, int c1)
        {
            int eave = WardFloor - 44;
            int mid = (c0 + c1) / 2;

            Box(c0, eave, c1, WardFloor - 1, TileID.MarbleBlock, White);
            Room nave = Vaulted(c0 + 3, c1 - 3, WardFloor, eave + 12, 8, WallID.MarbleBlock, White);

            // Windows of coloured glass between pilasters, and clear glass high up.
            ushort[] glass = { WallID.BlueStainedGlass, WallID.PurpleStainedGlass, WallID.YellowStainedGlass, WallID.RainbowStainedGlass };
            const int bay = 12;
            int k = 0;
            Back(nave.X0, WardFloor - 3, nave.X1, WardFloor - 1, WallID.StoneSlab, White);
            for (int x = nave.X0 + 1; x + bay <= nave.X1; x += bay, k++)
            {
                Back(x, eave + 13, x + 1, WardFloor - 4, WallID.PearlstoneBrick, White);
                int wm = x + bay / 2 + 1;
                if (Math.Abs(wm - mid) < 8)
                    continue;
                Window(wm - 2, wm + 2, WardFloor - 7, WardFloor - 24, 2, WallID.AncientGoldBrickWall, NoPaint, glass[k % glass.Length], NoPaint);
                Window(wm - 1, wm + 1, WardFloor - 29, WardFloor - 33, 1);
            }
            Back(nave.X1 - 1, eave + 13, nave.X1, WardFloor - 4, WallID.PearlstoneBrick, White);

            // Benches down the nave, the font under the dome, the altar at the east end.
            for (int x = nave.X0 + 12; x <= mid - 12; x += 5)
                c.Place(x, WardFloor - 1, TileID.Benches, 0);
            for (int x = mid + 10; x <= nave.X1 - 22; x += 5)
                c.Place(x, WardFloor - 1, TileID.Benches, 0);

            // The font: a basin of white a step high, water standing in it.
            Box(mid - 4, WardFloor - 2, mid + 4, WardFloor - 1, TileID.MarbleBlock, White);
            Cut(mid - 4, WardFloor - 2, Grace.Cut.DownLeft);
            Cut(mid + 4, WardFloor - 2, Grace.Cut.DownRight);
            for (int x = mid - 3; x <= mid + 3; x++)
            {
                c.Erase(x, WardFloor - 2);
                c.Liquid(x, WardFloor - 2, 255, 0);
            }
            c.Mark("Grace.Font", mid - 3, WardFloor - 2, 7, 1);

            int altar = nave.X1 - 14;
            Box(altar - 2, WardFloor - 1, altar + 8, WardFloor - 1, TileID.MarbleBlock, White);
            Cut(altar - 2, WardFloor - 1, Grace.Cut.DownLeft);
            c.Place(altar, WardFloor - 2, TileID.Tables, Suite.Gold.Table);
            c.Place(altar, WardFloor - 4, TileID.Candelabras, Suite.Gold.Candelabra);
            c.Place(altar + 4, WardFloor - 2, TileID.Statues, 1);
            c.Place(altar + 2, WardFloor - 4, TileID.Bottles, 3);

            // Light.
            for (int x = nave.X0 + 10; x <= nave.X1 - 10; x += 16)
                if (Math.Abs(x - mid) > 10 && !HangAt(x - 1, WardFloor - 12, TileID.Chandeliers, Suite.Gold.Chandelier))
                    HangAt(x, WardFloor - 12, TileID.HangingLanterns, Suite.Gold.Lantern);

            // Roof, drum and dome.
            Cornice(c0, c1, eave);
            Pitch2(c0 - 1, c1 + 1, eave - 2, 4, TileID.MarbleBlock, White, WallID.MarbleBlock, White);
            int dTop = eave - 14;
            Box(mid - 14, dTop, mid + 14, eave - 2, TileID.MarbleBlock, White);
            Hollow(mid - 12, dTop + 2, mid + 12, eave, WallID.MarbleBlock, White);
            for (int x = mid - 10; x <= mid + 10; x += 5)
                Window(x - 1, x + 1, eave - 3, dTop + 5, 1);
            for (int x = mid - 12; x <= mid + 12; x++)
                for (int y = eave - 1; y <= nave.Top + 2; y++)
                    if (c.Solid(x, y))
                    {
                        c.Erase(x, y);
                        c.Wall(x, y, WallID.AncientGoldBrickWall, NoPaint);
                    }
            Cornice(mid - 14, mid + 14, dTop);
            Dome(mid, dTop - 2, 15, 13, 2, TileID.GoldBrick, NoPaint, WallID.GoldStarryGlassWall, NoPaint);
            Spire(mid, dTop - 15, 3, 10, TileID.GoldBrick, NoPaint);
            HangAt(mid - 1, dTop, TileID.Chandeliers, Suite.Gold.Chandelier);

            // Doors: in from the Ward at the west end, out to the garden at the east.
            DoorOnto(c0, c0 + 2, WardFloor, c0);
            DoorOnto(c1 - 2, c1, WardFloor, c1);
            Adorn(new Room(nave.X0, nave.X1, WardFloor - 12, WardFloor), Decor.Chapel, 6, 4);
        }

        // ------------------------------------------------------------ the east terrace

        /// <summary>The terrace east of the chapel: a garden gone to ash, its tall trees pale as the stone, and the head of the stair down to the Shore.</summary>
        private void FontTerrace(int t0, int t1)
        {
            int stand = WardFloor - 1;

            // A balustrade along the east edge.
            Back(t1 - 20, stand - 1, t1, stand, WallID.WroughtIronFence, NoPaint);

            for (int px = t0 + 8; px <= t1 - 26; px += 18)
            {
                Box(px - 2, stand, px + 2, stand, TileID.MarbleBlock, White);
                Cut(px - 2, stand, Grace.Cut.DownLeft);
                Cut(px + 2, stand, Grace.Cut.DownRight);
                for (int x = px - 1; x <= px + 1; x++)
                    c.Block(x, stand, TileID.AshGrass);
                c.Tree(px, stand, TileID.TreeAsh, c.Next(22, 30), White);
            }

            for (int x = t0 + 16; x <= t1 - 28; x += 18)
                c.Place(x, stand, TileID.Statues, c.Pick(1, 12, 0));

            Lamppost(t0 + 3, stand);
            Lamppost(t1 - 22, stand);
        }
    }

    /// <summary>What is written in the Font.</summary>
    public static class FontText
    {
        /// <summary>At the shaft head (PAD_Grace_Structure_Plan.md §5, draft).</summary>
        public const string ShaftHead = "Drawn here. Carried to the table. None asked what it was.";
    }
}
