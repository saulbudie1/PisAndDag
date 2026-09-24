using System;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures.Grace
{
    public sealed partial class GraceBuilder
    {
        /// <summary>The row the Curtain's walk is laid on.</summary>
        private int WalkY => -(CurtainHeight - 10);

        /// <summary>The Handswitch: always in the chest in the watch tower's lookout, the first thing in it.</summary>
        private static int HandswitchItem => ModContent.ItemType<global::PisAndDag.Content.Items.Ranged.Handswitch>();

        /// <summary>The floors inside the Curtain, bottom up: every twelve rows from the roof of the bore.</summary>
        private const int CurtainFirstFloor = -6;
        private const int CurtainStorey = 12;

        /// <summary>The guns on the walk.</summary>
        private const int Guns = 5;

        /// <summary>
        /// The Curtain. A hundred and sixty tiles of white stone, forty thick, unbroken
        /// along its whole length, its foot battered out into the plateau and its crown
        /// banded in gold. Inside, twelve storeys of galleries one over the other, slit to
        /// the west; a stair runs up the inner side from the foot to the watch tower, and the
        /// tower opens onto the walk, where the guns stand behind the parapet facing the willows.
        ///
        /// No gate opens in it. The way in is the goblins' bore: three tiles high, dead
        /// straight, clean through the foot of the wall and into the bottom of the stair.
        /// </summary>
        private void Curtain()
        {
            int x0 = CurtainX, x1 = CurtainX + CurtainThick - 1;
            int crown = WalkY;

            int ix0 = x0 + 5, ix1 = x0 + 29;      // the galleries
            int sx0 = x0 + 31, sx1 = x0 + 36;     // the stair
            int t0 = sx0 - 5, t1 = x1 + 1;        // the watch tower

            // ---- footing and the whole body, solid first
            Box(x0 - 9, 1, x1 + 6, 22, TileID.MarbleBlock, NoPaint);
            Box(x0, crown, x1, 0, TileID.MarbleBlock, White);

            // ---- the batter
            for (int k = 0; k <= 12; k++)
            {
                int y = -k;
                int reach = (int)Math.Round((12 - k) * 0.7);

                for (int x = x0 - reach; x < x0; x++)
                    c.Block(x, y, TileID.MarbleBlock, White);

                if (reach > 0)
                    Cut(x0 - reach, y, Grace.Cut.DownLeft);
            }

            // ---- storeys
            int storeys = 0;
            Theme[] themes =
            {
                Theme.Guard, Theme.Barracks, Theme.Store, Theme.Armoury, Theme.Barracks, Theme.Kitchen,
                Theme.Hall, Theme.Barracks, Theme.Store, Theme.Chapel, Theme.Guard, Theme.Armoury,
            };

            for (int floor = CurtainFirstFloor; floor - CurtainStorey >= crown; floor -= CurtainStorey)
            {
                int top = floor - CurtainStorey + 2;
                var room = new Room(ix0, ix1, top, floor);

                Hollow(ix0, top, ix1, floor - 1, WallID.MarbleBlock, White);

                // Floor laid in slab, a line of gold under it.
                Box(ix0, floor, ix1, floor, TileID.StoneSlab, White);
                Box(ix0, floor + 1, ix1, floor + 1, TileID.SandstoneBrick, White);

                // The back wall in its registers: a dado of bare slab, gold at the rail, the
                // marble mended in brick, slab pilasters, grey brick under the ceiling.
                Registers(room, Scheme.Guard);

                // Slits: two high, through the outer skin, glazed, dark stone in the reveal. From
                // the willows they read as a column of dark marks.
                for (int y = floor - 5; y <= floor - 4; y++)
                {
                    for (int x = x0; x < ix0; x++)
                    {
                        c.Block(x, y, TileID.Glass);
                        c.Wall(x, y, WallID.StoneSlab, NoPaint);
                    }
                }

                Theme th = themes[storeys % themes.Length];
                Furnish(room, th, Suite.Marble);
                if (storeys % 3 != 2)
                    StashSomewhere(room, LootFor(th, Suite.Marble), Suite.Marble);
                Neglect(room, 2, storeys % 3 == 1 ? 1 : 0);
                Adorn(room, th == Theme.Chapel ? Decor.Chapel : th == Theme.Hall ? Decor.Royal
                    : th == Theme.Store || th == Theme.Kitchen ? Decor.Home : Decor.Military, 9);

                // The garrison's colours from the ceiling, between the pilasters.
                if (th == Theme.Hall || th == Theme.Chapel || th == Theme.Guard)
                    for (int bx = ix0 + 4; bx <= ix1 - 4; bx += 8)
                        if (c.TypeAt(bx, top) < 0 && c.TypeAt(bx, top + 1) < 0 && c.TypeAt(bx, top + 2) < 0)
                            Hang(bx, top, TileID.Banners, th == Theme.Chapel ? 8 : 3);

                storeys++;
            }

            // ---- string courses on both faces, every third storey
            for (int y = CurtainFirstFloor - 3 * CurtainStorey; y > crown + 6; y -= 3 * CurtainStorey)
            {
                c.Block(x0 - 1, y, TileID.SandstoneBrick, White, Grace.Cut.DownLeft);
                c.Block(x0 - 1, y + 1, TileID.SandstoneBrick, White, Grace.Cut.UpLeft);
                c.Block(x1 + 1, y, TileID.SandstoneBrick, White, Grace.Cut.DownRight);
                c.Block(x1 + 1, y + 1, TileID.SandstoneBrick, White, Grace.Cut.UpRight);
            }

            // ---- the stair, the full height of the inner side
            Hollow(sx0, crown - 1, sx1, -1, WallID.PearlstoneBrick, White);

            for (int b = 0; b > crown; b -= CurtainStorey)
            {
                // A landing between galleries (the foot of the stair is the floor of the bore).
                if (b < 0)
                {
                    for (int x = sx0; x <= sx1; x++)
                        c.Platform(x, b, Suite.Marble.Platform);

                    Hang(sx0, b + 1, TileID.HangingLanterns, Suite.Marble.Lantern);

                    // A band of gold on the stair's wall at every landing, a rail of slab under it.
                    Back(sx0, b - 1, sx1, b - 1, WallID.StoneSlab, NoPaint);
                }

                // Up east to the gallery floor...
                for (int i = 1; i <= 5; i++)
                    if (b - i > crown)
                        c.Platform(sx0 + i - 1, b - i, Suite.Marble.Platform, Grace.Cut.DownLeft);

                int f = b - 6;
                if (f <= crown)
                    break;

                for (int x = sx0; x <= sx1; x++)
                    c.Platform(x, f, Suite.Marble.Platform);

                // ...through the arch into the gallery...
                Air(sx0 - 1, f - 3, sx0 - 1, f - 1);
                Back(sx0 - 1, f - 3, sx0 - 1, f - 1, WallID.PearlstoneBrick, White);

                // ...and up west to the next landing.
                for (int i = 1; i <= 5; i++)
                    if (f - i > crown)
                        c.Platform(sx1 - i + 1, f - i, Suite.Marble.Platform, Grace.Cut.DownRight);
            }

            // ---- the walk: gold through the whole thickness under it
            Box(x0, crown + 1, ix1, crown + 1, TileID.AncientGoldBrick, NoPaint);
            Box(sx1 + 1, crown + 1, x1, crown + 1, TileID.AncientGoldBrick, NoPaint);

            // Corbels out over the west face, the soffit running in a smooth line down to the wall.
            for (int k = 1; k <= 3; k++)
            {
                for (int y = crown - 1; y <= crown + 3 - k; y++)
                    c.Block(x0 - k, y, TileID.MarbleBlock, White);

                Cut(x0 - k, crown + 3 - k, Grace.Cut.UpLeft);
            }

            // On the corbels, the parapet: white to the height of a gun's muzzle, a gold coping,
            // and at its outer end a merlon with a beacon on it.
            Box(x0 - 3, crown - 3, x0 - 1, crown - 1, TileID.MarbleBlock, White);
            Box(x0 - 4, crown - 4, x0, crown - 4, TileID.AncientGoldBrick, NoPaint);
            Cut(x0 - 4, crown - 4, Grace.Cut.UpLeft);
            Cut(x0, crown - 4, Grace.Cut.UpRight);
            Box(x0 - 3, crown - 7, x0 - 2, crown - 5, TileID.MarbleBlock, White);
            Box(x0 - 3, crown - 8, x0 - 2, crown - 8, TileID.AncientGoldBrick, NoPaint);
            c.Place(x0 - 3, crown - 9, TileID.Lamps, Suite.Marble.Lamp);

            // The far parapet, drawn behind the walk: its merlons against the sky over the guns.
            for (int x = x0 + 1; x < t0; x++)
            {
                Back(x, crown - 3, x, crown - 1, WallID.MarbleBlock, White);
                Back(x, crown - 4, x, crown - 4, WallID.AncientGoldBrickWall, NoPaint);
                if ((x - x0 - 1) % 5 < 3)
                    Back(x, crown - 7, x, crown - 5, WallID.MarbleBlock, White);
            }

            // The battery: guns in a row along the walk, trained out over the willows.
            for (int g = 0; g < Guns; g++)
            {
                int gx = x0 + 1 + g * 5;
                if (gx + 3 < t0)
                    c.Place(gx, crown - 1, TileID.Cannon, 0, -1);
            }

            // ---- the bore: three high, straight through the foot, into the bottom of the stair
            for (int x = x0 - 12; x < sx0; x++)
            {
                for (int y = -3; y <= -1; y++)
                {
                    c.Erase(x, y);
                    c.EraseWall(x, y);
                }
            }

            // Scored where the head went round: the roof of the bore is not square.
            for (int x = x0 - 10; x < sx0; x += 3)
            {
                Cut(x, -4, Grace.Cut.UpRight);
                Cut(x + 1, -4, Grace.Cut.UpLeft);
            }

            // Where the bore went through dressed stone, what is left of the facing: patches
            // of the marble, the rubble core where it came away, a mend or two.
            int covered = x0 - 12;
            while (covered < x0 && !c.Solid(covered, -4))
                covered++;
            BrokenWall(covered, -3, sx0 - 1, -1, WallID.MarbleBlock, White);

            // Spoil from the bore, still where it was thrown.
            for (int x = x0 - 22; x < x0 - 12; x++)
            {
                int g = GroundAt(x);
                if (c.OneIn(2))
                    c.Block(x, g - 1, TileID.MarbleBlock, White, c.OneIn(2) ? Grace.Cut.Half : Grace.Cut.Full);
            }

            // Door out to the city at the foot of the stair.
            Air(sx1 + 1, -3, x1, -1);
            Back(sx1 + 1, -3, x1, -1, WallID.PearlstoneBrick, White);
            c.Place(x1, -1, TileID.ClosedDoor, Suite.Marble.Door);

            // ---- the watch tower over the stair
            WatchTower(t0, t1, crown);

            // The stair comes up through the walk into the tower's lowest room, and the tower
            // has a door out onto the walk.
            for (int x = sx0; x <= sx1; x++)
            {
                c.Erase(x, crown);
                c.Erase(x, crown + 1);
                c.Platform(x, crown, Suite.Marble.Platform);
                c.Wall(x, crown, WallID.PearlstoneBrick, White);
                c.Wall(x, crown + 1, WallID.PearlstoneBrick, White);
            }

            DoorOnto(t0, t0 + 1, crown, t0);

            // ---- what it says, and where
            int gs = x0 - 26;
            c.Sign(gs, GroundAt(gs) - 1, TileID.Signs, 0, "Held on the ninth day. Held on the ninetieth.");
            c.Sign(x1 + 3, -1, TileID.Signs, 0, "The stone was never breached.");
        }

        /// <summary>
        /// The watch tower over the Curtain's stair, standing on the walk from
        /// <paramref name="x0"/> to <paramref name="x1"/>. Four storeys of guard rooms, each
        /// with its window, its gold and its colours; the stair comes up into the lowest and
        /// rungs go up from each to the next. From the top room a rope hangs under a trapdoor,
        /// and over the trapdoor is the lookout: the sword statue, the house's chest, a railing
        /// all round and a balcony out over each face, under a drum, a gold dome, a lantern
        /// and a needle.
        /// </summary>
        private void WatchTower(int x0, int x1, int baseY)
        {
            const int storey = 12;
            const int storeys = 4;
            int ix0 = x0 + 2, ix1 = x1 - 2;
            int lookFloor = baseY - storey * storeys;      // the lookout's floor
            int lookTop = lookFloor - 12;                   // the lookout's ceiling row
            int cx = (x0 + x1) / 2;

            // The shaft, solid, then a room in every storey.
            Box(x0, lookTop, x1, baseY - 1, TileID.MarbleBlock, White);

            for (int s = 0; s < storeys; s++)
            {
                int floor = baseY - s * storey;
                int f = floor - storey;
                var room = new Room(ix0, ix1, f + 1, floor);

                // Rungs go up from each room at alternate ends; a room is entered at the end
                // the room under it goes up from, and the lowest from the stair in the middle.
                bool upLeft = s % 2 == 0;
                bool top = s + 1 == storeys;

                Hollow(ix0, f + 1, ix1, floor - 1, WallID.PearlstoneBrick, White);
                Registers(room, Scheme.Tower);

                // Windows through both faces: tall, glazed, arched in the stone. None over the
                // door out to the walk.
                int wy = floor - 3;
                for (int y = wy - 5; y <= wy; y++)
                {
                    if (s > 0)
                    {
                        c.Block(x0, y, TileID.Glass);
                        c.Block(x0 + 1, y, TileID.Glass);
                    }
                    c.Block(x1 - 1, y, TileID.Glass);
                    c.Block(x1, y, TileID.Glass);
                }
                if (s > 0)
                    Cut(x0, wy - 5, Grace.Cut.DownLeft);
                Cut(x1, wy - 5, Grace.Cut.DownRight);

                // A course at the floor over this storey, proud of both faces.
                if (!top)
                {
                    c.Block(x0 - 1, f, TileID.AncientGoldBrick, NoPaint, Grace.Cut.UpLeft);
                    c.Block(x1 + 1, f, TileID.AncientGoldBrick, NoPaint, Grace.Cut.UpRight);
                }

                // The hatch in the ceiling and the rungs up to it.
                if (!top)
                {
                    int hx = upLeft ? ix0 : ix1 - 2;
                    for (int x = hx; x <= hx + 2; x++)
                        c.Platform(x, f, Suite.Marble.Platform);
                    for (int y = floor - 3; y > f; y -= 3)
                        for (int x = hx; x <= hx + 2; x++)
                            c.Platform(x, y, Suite.Marble.Platform);
                }

                // In the back wall: a window of glass in a white frame, over the middle.
                Window(cx - 1, cx + 1, floor - 4, f + 5, 1);
                HangAt(cx - 1, f + 1, TileID.Chandeliers, Suite.Marble.Chandelier);

                // The colours hang over the end nobody climbs at; what stands in the room
                // stands clear of the rungs, the hatch and the stair.
                int quiet = top ? ix1 - 1 : upLeft ? ix1 - 1 : ix0 + 1;
                Hang(quiet, f + 1, TileID.Banners, 3);

                if (s == 0)
                    c.Place(ix1 - 1, floor - 1, TileID.Statues, 0);
                else if (s == 1)
                    c.Place(cx + 1, floor - 1, TileID.Statues, 0);
                else if (s == 2)
                    c.Place(cx - 2, floor - 1, TileID.Statues, 0);
                else
                {
                    // The top room: the watch's table, its chest, and the rope.
                    c.Place(ix1 - 4, floor - 1, TileID.Tables, Suite.Marble.Table);
                    c.Place(ix1 - 1, floor - 1, TileID.Chairs, Suite.Marble.Chair, -1);
                    StashSomewhere(new Room(ix0 + 3, ix1 - 5, f + 1, floor), Loot.Military, Suite.Marble);
                }

                Adorn(room, Decor.Military, 4, 4);
            }

            // ---- the rope and the trapdoor into the lookout, over the hatch into the top room
            int topFloor = baseY - (storeys - 1) * storey;
            bool enteredLeft = (storeys - 2) % 2 == 0;
            int tx = enteredLeft ? ix0 + 1 : ix1 - 2;
            for (int y = lookFloor + 2; y < topFloor; y++)
                c.Block(tx, y, TileID.Rope);

            // ---- the lookout
            Hollow(x0 + 2, lookTop + 1, x1 - 2, lookFloor - 1, WallID.MarbleBlock, White);
            Registers(new Room(x0 + 2, x1 - 2, lookTop + 1, lookFloor), Scheme.Palace);

            // Its floor: gold, out two over each face as a balcony, dressed underneath.
            Box(x0 - 2, lookFloor, x1 + 2, lookFloor, TileID.AncientGoldBrick, NoPaint);
            c.Block(x0 - 1, lookFloor + 1, TileID.MarbleBlock, White, Grace.Cut.UpLeft);
            c.Block(x1 + 1, lookFloor + 1, TileID.MarbleBlock, White, Grace.Cut.UpRight);
            Cut(x0 - 2, lookFloor, Grace.Cut.UpLeft);
            Cut(x1 + 2, lookFloor, Grace.Cut.UpRight);

            // Open arches through both faces, a railing across them and round the balconies,
            // posts at the corners, and glass over the railing so the lookout is weathertight.
            foreach (int ax in new[] { x0, x1 - 1 })
            {
                ArchOpening(ax, ax + 1, lookFloor - 1, lookFloor - 7, 2);
                for (int x = ax; x <= ax + 1; x++)
                {
                    int head = HeadTop(x, ax, ax + 1, lookFloor - 7, 2);
                    for (int y = head; y <= lookFloor - 1; y++)
                        c.Wall(x, y, y >= lookFloor - 2 ? WallID.WroughtIronFence : WallID.Glass, NoPaint);
                }
            }

            foreach (int bx in new[] { x0 - 2, x0 - 1, x1 + 1, x1 + 2 })
                Back(bx, lookFloor - 2, bx, lookFloor - 1, WallID.WroughtIronFence, NoPaint);
            Column(x0 - 2, lookFloor - 2, lookFloor - 1);
            Column(x1 + 2, lookFloor - 2, lookFloor - 1);

            // The trapdoor in the floor, over the rope.
            c.Erase(tx, lookFloor);
            c.Erase(tx + 1, lookFloor);
            c.Place(tx, lookFloor, TileID.TrapdoorClosed, 0);
            c.Mark("Grace.CurtainTrapdoor", tx, lookFloor, 2, 1);

            // The statue in the middle, the chest beside it, a candelabrum by the chest. The
            // Handswitch is in the chest, with whatever else the watch kept up here.
            int st = cx - 1;
            c.Place(st, lookFloor - 1, TileID.Statues, 3);
            int chestX = enteredLeft ? st + 3 : st - 4;
            var kept = Roll(Loot.Royal);
            kept.Insert(0, (HandswitchItem, 1));
            if (c.PlaceChest(chestX, lookFloor - 1, TileID.Containers, 1, kept))
                c.Mark("Grace.CurtainChest", chestX, lookFloor - 2, 2, 2);
            c.Place(enteredLeft ? chestX + 2 : chestX - 2, lookFloor - 1, TileID.Candelabras, Suite.Gold.Candelabra);
            Hang(cx, lookTop + 1, TileID.HangingLanterns, Suite.Gold.Lantern);

            // ---- the crown: a cornice, a drum with small windows, the dome, the lantern, the needle
            Cornice(x0, x1, lookTop);
            int drumTop = lookTop - 5;
            Box(x0 + 1, drumTop, x1 - 1, lookTop - 2, TileID.MarbleBlock, White);
            Hollow(x0 + 3, drumTop + 1, x1 - 3, lookTop - 2, WallID.GoldBrick, NoPaint);
            for (int x = cx - 3; x <= cx + 3; x += 3)
                c.Wall(x, drumTop + 2, WallID.Glass, NoPaint);
            c.Block(x0 + 1, drumTop + 2, TileID.Glass);
            c.Block(x1 - 1, drumTop + 2, TileID.Glass);
            Box(x0, drumTop, x1, drumTop, TileID.AncientGoldBrick, NoPaint);

            int r = (x1 - x0) / 2;
            Dome(cx, drumTop - 1, r, r + 1, 2, TileID.GoldBrick, NoPaint, WallID.GoldBrick, NoPaint);
            int crownRow = drumTop - 1 - (r + 1);

            // The lantern: a little glazed drum on the crown of the dome.
            Box(cx - 2, crownRow - 5, cx + 2, crownRow, TileID.MarbleBlock, White);
            Hollow(cx - 1, crownRow - 4, cx + 1, crownRow - 1, WallID.Glass, NoPaint);
            Box(cx - 2, crownRow - 5, cx + 2, crownRow - 5, TileID.AncientGoldBrick, NoPaint);
            Spire(cx, crownRow - 6, 3, 10, TileID.GoldBrick, NoPaint);
        }
    }
}
