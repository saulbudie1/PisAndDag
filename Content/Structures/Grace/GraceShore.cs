using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The Dry Shore. A broad stair down from the Font's terrace to the quays; the customs
    /// house and a crane on the quay; the harbour basin dug down into the rock with two
    /// ships sitting on their keels in the dust at the bottom of it; the sea wall; the
    /// lighthouse on the sea wall, lit; and beyond it dry rock to the edge of the world.
    /// All of it finished. None of it ever wet.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        private const int Quay = 0;
        private const int BasinFloor = 26;

        private void DryShore()
        {
            int x0 = ShoreX;
            int edge = site.PlainWidth + 20;

            for (int x = x0; x <= edge; x++)
                for (int y = -site.Top; y < Quay; y++)
                {
                    c.Erase(x, y);
                    c.EraseWall(x, y);
                }

            // The stair down from the terrace: thirty-six steps, one to one, dressed as slopes.
            int stairEnd = x0 + 35;
            for (int i = 0; i < 36; i++)
            {
                int x = x0 + i;
                int top = WardFloor + i;
                Box(x, top, x, Quay + 4, TileID.MarbleBlock, White);
                c.Block(x, top, TileID.StoneSlab, White, i == 35 ? Grace.Cut.Full : Grace.Cut.DownRight);
            }
            // A rail down the stair.
            for (int i = 0; i < 36; i++)
                for (int h = 1; h <= 2; h++)
                    c.Wall(x0 + i, WardFloor + i - h, WallID.WroughtIronFence, NoPaint);

            // Under the stair: the harbour stores, a room between each pair of piers, their
            // ceilings stepping down under the treads, a doorway through every pier.
            for (int ax = x0 + 2; ax + 7 <= stairEnd - 6; ax += 9)
            {
                for (int x = ax; x <= ax + 6; x++)
                {
                    int under = WardFloor + (x - x0) + 4;
                    for (int y = under; y < Quay; y++)
                    {
                        c.Erase(x, y);
                        c.Wall(x, y, WallID.StoneSlab, NoPaint);
                    }
                }

                Hollow(ax + 7, Quay - 3, ax + 8, Quay - 1, WallID.StoneSlab, NoPaint);
                var r = new Room(ax, ax + 6, WardFloor + (ax + 6 - x0) + 4, Quay);
                if (r.H >= 4)
                {
                    stashKind = Loot.Sea;
                    stashSuite = Suite.Wood;
                    if (c.OneIn(2))
                        Along(new Room(ax, ax + 6, r.Top, Quay), 0, 1, 1, (TileID.FishingCrate, 0, 1), (TileID.Containers, 5, 1));
                    else
                        Along(new Room(ax, ax + 6, r.Top, Quay), 0, 1, 1, (TileID.FishingCrate, 0, 1), (TileID.Kegs, 0, 1));
                }
                HangAt(ax + 3, Quay - 4, TileID.HangingLanterns, Suite.Marble.Lantern);
            }

            // The quay: paving, a course of gold at its edge.
            int q0 = stairEnd + 1, q1 = q0 + 25;
            Box(q0, Quay, q1, Quay + 4, TileID.StoneSlab, White);
            Box(q0, Quay + 1, q1, Quay + 4, TileID.GrayBrick, NoPaint);

            CustomsHouse(q0 + 2, q0 + 19);
            Crane(q1 - 4);
            for (int x = q0 + 21; x <= q1 - 6; x += 4)
                Bollard(x, Quay - 1);

            // The basin.
            int b0 = q1 + 1, b1 = b0 + 55;
            HarbourBasin(b0, b1);

            // The sea wall, and the lighthouse on it.
            int s0 = b1 + 1, s1 = s0 + 12;
            SeaWall(s0, s1);
            Lighthouse(s0, s1);

            // Beyond: the dry seabed, rock and dust, and the shells of a shore nobody walked.
            DrySeabed(s1 + 1, edge);
        }

        // ------------------------------------------------------------ the quay

        /// <summary>The customs house: two storeys of white, the harbour master's office over the weigh room.</summary>
        private void CustomsHouse(int x0, int x1)
        {
            const int storey = 9;
            int top = Quay - storey * 2 - 1;
            Box(x0, top, x1, Quay - 1, TileID.MarbleBlock, White);

            for (int s = 0; s < 2; s++)
            {
                int floor = Quay - s * storey;
                var r = new Room(x0 + 2, x1 - 2, floor - storey + 1, floor);
                Hollow(r.X0, r.Top, r.X1, floor - 1, WallID.PearlstoneBrick, White);
                DressTowerRoom(r, storey, Scheme.Sea);
                if (s > 0)
                {
                    for (int x = r.X1 - 2; x <= r.X1; x++)
                        c.Platform(x, floor, Suite.Marble.Platform);
                    for (int y = floor + 3; y < floor + storey; y += 3)
                        for (int x = r.X1 - 2; x <= r.X1; x++)
                            if (c.TypeAt(x, y) < 0)
                                c.Platform(x, y, Suite.Marble.Platform);
                }
            }

            // Weigh room below: crates and a chest of gear never used, the bait for the
            // biggest thing in the sea still in it.
            var (chest, chestStyle) = ChestFor(Loot.Sea, Suite.Wood);
            var gear = Roll(Loot.Sea);
            gear.Insert(0, (ItemID.TruffleWorm, 3));
            c.PlaceChest(x0 + 2, Quay - 1, chest, chestStyle, gear);
            c.Place(x0 + 5, Quay - 1, TileID.FishingCrate, 0);
            c.Place(x0 + 8, Quay - 1, TileID.MetalBars, 2);
            c.Mark("Grace.ShoreChest", x0 + 2, Quay - 2, 2, 2);
            Hang(x0 + 6, Quay - storey + 1, TileID.HangingLanterns, Suite.Marble.Lantern);

            // The office above.
            int f2 = Quay - storey;
            c.Place(x0 + 2, f2 - 1, TileID.Chairs, Suite.Marble.Chair, 1);
            c.Place(x0 + 3, f2 - 1, TileID.Tables, Suite.Marble.Table);
            c.Place(x0 + 3, f2 - 3, TileID.ShipInABottle, 0);
            c.Place(x0 + 7, f2 - 1, TileID.Bookcases, Suite.Marble.Bookcase);
            Hang(x0 + 6, top + 2, TileID.HangingLanterns, Suite.Marble.Lantern);

            DoorOnto(x0, x0 + 1, Quay, x0);
            DoorOnto(x1 - 1, x1, Quay, x1);
            for (int s2 = 0; s2 < 2; s2++)
                Adorn(new Room(x0 + 2, x1 - 2, Quay - s2 * storey - storey + 1, Quay - s2 * storey), Decor.Sea, 4, 4);
            Cornice(x0, x1, top);
            Pitch2(x0 - 1, x1 + 1, top - 2, 2, TileID.CopperPlating, NoPaint, WallID.Wood, NoPaint);
        }

        /// <summary>A bollard: a short iron post with a rounded head.</summary>
        private void Bollard(int x, int stand)
        {
            c.Block(x, stand, TileID.IronBrick);
            c.Block(x, stand - 1, TileID.IronBrick, NoPaint, Grace.Cut.Half);
        }

        /// <summary>A timber crane at the quay's edge: a post, a jib out over the lip of the basin, a chain and a crate on it.</summary>
        private void Crane(int x)
        {
            int top = Quay - 22;
            for (int y = top; y < Quay; y++)
                c.Block(x, y, TileID.WoodenBeam);
            const int jib = 6;
            for (int i = 0; i <= jib; i++)
                c.Block(x + i, top - i / 4, TileID.BorealWood, NoPaint, i == jib ? Grace.Cut.UpRight : Grace.Cut.Full);
            int hx = x + jib;
            int hy = top - 3 + 1;
            for (int y = hy + 1; y <= hy + 18; y++)
                c.Block(hx, y, TileID.Chain);
            c.Block(hx - 1, hy + 21, TileID.WoodBlock);
            c.Block(hx, hy + 21, TileID.WoodBlock);
            c.Block(hx + 1, hy + 21, TileID.WoodBlock);
            c.Place(hx - 1, hy + 20, TileID.FishingCrate, 0);
        }

        // ------------------------------------------------------------ the basin

        /// <summary>
        /// The harbour basin, dug down to twenty-six under the quay, faced in grey stone with
        /// steps down its inner face and rings in the wall, the floor of it dust. Two ships sit
        /// in it on their keels.
        /// </summary>
        private void HarbourBasin(int b0, int b1)
        {
            // The rock round it, then the hole.
            Box(b0 - 2, Quay, b1 + 2, BasinFloor + 4, TileID.GrayBrick, NoPaint);
            Box(b0 - 2, Quay, b1 + 2, Quay, TileID.StoneSlab, White);

            for (int x = b0; x <= b1; x++)
                for (int y = Quay; y < BasinFloor; y++)
                {
                    c.Erase(x, y);
                    c.EraseWall(x, y);
                }

            // The floor: dust over stone.
            for (int x = b0; x <= b1; x++)
            {
                c.Block(x, BasinFloor, TileID.Ash, Gray);
                if (c.OneIn(3))
                    c.Block(x, BasinFloor - 1, TileID.Ash, Gray, Grace.Cut.Half);
            }

            // The waterline that never was: a gold course along both faces at quay height.
            for (int y = Quay + 1; y <= Quay + 1; y++)
            {
                c.Block(b0 - 1, y, TileID.AncientGoldBrick, NoPaint);
                c.Block(b1 + 1, y, TileID.AncientGoldBrick, NoPaint);
            }

            // Rungs down the west face.
            for (int y = Quay + 3; y < BasinFloor; y += 3)
                for (int x = b0; x <= b0 + 1; x++)
                    c.Platform(x, y, Suite.Marble.Platform);

            // Two ships: a great ship of three masts, and a small one bow to bow with her.
            int ground = BasinFloor - 1;
            Ship(b0 + 3, ground, 30, 1, true);
            Ship(b0 + 40, ground, 13, -1, false);
        }

        // ------------------------------------------------------------ the sea wall

        /// <summary>The sea wall: a thick wall of grey stone from the basin floor to over the quay, its seaward face battered.</summary>
        private void SeaWall(int s0, int s1)
        {
            int top = Quay - 8;
            Box(s0, top, s1, BasinFloor + 4, TileID.GrayBrick, NoPaint);
            Box(s0, top, s1, top, TileID.StoneSlab, White);
            Box(s0, top + 1, s1, top + 1, TileID.AncientGoldBrick, NoPaint);

            // Battered seaward face.
            for (int k = 0; k < BasinFloor + 4 - top; k++)
            {
                int y = top + k;
                int reach = k / 3;
                for (int x = s1 + 1; x <= s1 + reach; x++)
                    c.Block(x, y, TileID.GrayBrick);
                if (reach > 0)
                    Cut(s1 + reach, y, Grace.Cut.DownRight);
            }

            // The steps up from the quay side: platforms from the basin's east face.
            for (int y = top + 3; y < BasinFloor; y += 3)
                for (int x = s0 - 2; x <= s0 - 1; x++)
                    if (c.TypeAt(x, y) < 0)
                        c.Platform(x, y, Suite.Marble.Platform);
        }

        /// <summary>
        /// The lighthouse on the sea wall: a white shaft banded with gold, storeys up to a
        /// gallery, and a lantern room of glass at the top with the lamps still in it, under
        /// a gold cap.
        /// </summary>
        private void Lighthouse(int s0, int s1)
        {
            int baseY = Quay - 8;
            int top = Quay - 112;
            int cx = (s0 + s1) / 2;

            Tower(s0, s1, baseY, top + 16, Roof.Flat, TileID.MarbleBlock, White, WallID.MarbleBlock, White,
                Suite.Marble, storey: 12, doorWest: true, doorEast: true, decor: Decor.Sea, scheme: Scheme.Sea);

            // Gold bands every other storey.
            for (int y = baseY - 12; y > top + 18; y -= 24)
                Course(s0, s1, y, TileID.AncientGoldBrick, NoPaint);

            // The gallery.
            int gal = top + 16;
            Box(s0 - 2, gal, s1 + 2, gal, TileID.StoneSlab, White);
            Cut(s0 - 2, gal, Grace.Cut.UpLeft);
            Cut(s1 + 2, gal, Grace.Cut.UpRight);
            Back(s0 - 2, gal - 2, s1 + 2, gal - 1, WallID.WroughtIronFence, NoPaint);

            // The lantern room: glass all round, the lamps in it.
            Box(s0 + 1, top + 4, s1 - 1, gal - 1, TileID.MarbleBlock, White);
            Hollow(s0 + 2, top + 5, s1 - 2, gal - 1, WallID.Glass, NoPaint);
            Glaze(s0 + 1, top + 6, s0 + 1, gal - 2);
            Glaze(s1 - 1, top + 6, s1 - 1, gal - 2);
            for (int x = s0 + 3; x <= s1 - 3; x += 3)
                c.Place(x, gal - 1, TileID.Lamps, Suite.Gold.Lamp);
            Hang(cx - 1, top + 5, TileID.Chandeliers, Suite.Gold.Chandelier);

            // Hatch from the top storey.
            for (int x = s0 + 2; x <= s0 + 4; x++)
            {
                c.Erase(x, gal + 1);
                c.Platform(x, gal, Suite.Marble.Platform);
                for (int y = gal + 3; y <= gal + 9; y += 3)
                    if (c.TypeAt(x, y) < 0)
                        c.Platform(x, y, Suite.Marble.Platform);
            }

            // The cap.
            Cornice(s0 + 1, s1 - 1, top + 4);
            Dome(cx, top + 2, 6, 6, 1, TileID.GoldBrick, NoPaint, WallID.GoldBrick, NoPaint);
            Spire(cx, top - 4, 3, 6, TileID.GoldBrick, NoPaint);
            c.Mark("Grace.Lighthouse", s0, top, s1 - s0 + 1, baseY - top);
        }

        // ------------------------------------------------------------ beyond

        /// <summary>
        /// The seabed beyond the wall: the plain's rock cut down to the level of the basin's
        /// floor and on down to the edge of the world, dry, dusted with ash, shells lying on it.
        /// </summary>
        private void DrySeabed(int x0, int x1)
        {
            for (int x = x0; x <= x1; x++)
            {
                int d = x - x0;
                int floor = BasinFloor + 4 + d / 6;

                for (int y = -site.Top; y < floor; y++)
                {
                    if (c.TypeAt(x, y) == TileID.GrayBrick && y > Quay - 9)
                        continue;
                    c.Erase(x, y);
                    c.EraseWall(x, y);
                }

                c.Block(x, floor, TileID.Stone);
                for (int y = floor + 1; y <= floor + 12; y++)
                    if (c.TypeAt(x, y) < 0)
                        c.Block(x, y, TileID.Stone);

                if (c.OneIn(2))
                    c.Block(x, floor - 1, TileID.Ash, Gray, Grace.Cut.Half);
                else if (c.OneIn(6))
                    c.Place(x, floor - 1, TileID.BeachPiles, c.Next(0, 5));
            }
        }
    }
}
