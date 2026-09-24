using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    public enum Roof
    {
        Flat,
        Battlements,
        Dome,
        Spire,
        Pitched,
    }

    /// <summary>
    /// Building types: towers, houses, granaries, arcades. Each is a thin skin of stone
    /// round a room with a wall behind it and a light in it, because that is how a
    /// building reads from the side: solid stone more than a few tiles thick is dark.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        // ------------------------------------------------------------ towers

        /// <summary>
        /// A tower [x0, x1] from <paramref name="baseY"/> (the floor it stands on) up to
        /// <paramref name="top"/> (its highest solid row before the roof). Hollow, with a floor
        /// every <paramref name="storey"/> rows reached by rungs, a window each storey on both
        /// faces, a lamp in each room, and a roof.
        /// </summary>
        private void Tower(int x0, int x1, int baseY, int top, Roof roof, ushort skin, byte skinPaint,
            ushort wall, byte wallPaint, Suite s, int storey = 12, bool doorWest = true, bool doorEast = false, Decor? decor = Decor.Hall,
            Scheme scheme = null)
        {
            scheme ??= wall == WallID.WhiteDynasty ? Scheme.Boudoir : wall == WallID.StoneSlab ? Scheme.Works : Scheme.Tower;
            Box(x0, top, x1, baseY - 1, skin, skinPaint);
            int ix0 = x0 + 2, ix1 = x1 - 2;

            int floor = baseY;
            bool left = true;

            while (floor - storey > top + 2)
            {
                int f = floor - storey;
                var room = new Room(ix0, ix1, f + 1, floor);
                Hollow(ix0, f + 1, ix1, floor - 1, wall, wallPaint);

                // The room's back wall in its registers, and a tall arched window that lets
                // the day in.
                DressTowerRoom(room, storey, scheme);

                // Slits in both faces, glazed.
                int wy = floor - storey / 2;
                Glaze(x0, wy - 1, x0 + 1, wy);
                Glaze(x1 - 1, wy - 1, x1, wy);

                // A hatch and rungs up to it.
                int hx = left ? ix0 : ix1 - 2;
                for (int x = hx; x <= hx + 2; x++)
                    c.Platform(x, f, s.Platform);
                for (int y = floor - 3; y > f; y -= 3)
                    for (int x = hx; x <= hx + 2; x++)
                        c.Platform(x, y, s.Platform);

                if (ix1 - ix0 >= 6)
                    Hang((ix0 + ix1) / 2 - (left ? 0 : 1), f + 1, TileID.HangingLanterns, s.Lantern);

                if (decor.HasValue)
                    Adorn(room, decor.Value, 6);

                floor = f;
                left = !left;
            }

            // The top room, up to the roof, and what the tower kept in it.
            if (floor - 1 > top + 1)
            {
                Hollow(ix0, top + 2, ix1, floor - 1, wall, wallPaint);
                DressTowerRoom(new Room(ix0, ix1, top + 2, floor), floor - top - 2, scheme);
                Hang((ix0 + ix1) / 2, top + 2, TileID.HangingLanterns, s.Lantern);
                Loot kept = decor == Decor.Military ? Loot.Military : decor == Decor.Sea ? Loot.Sea
                    : decor == Decor.Royal || decor == null ? Loot.Royal : Loot.Home;
                StashSomewhere(new Room(ix0, ix1, top + 2, floor), kept, s);
            }

            // Doors at the foot.
            if (doorWest)
            {
                Air(x0, baseY - 3, x0 + 1, baseY - 1);
                Reveal(x0, baseY - 3, x0 + 1, baseY - 1);
                c.Place(x0, baseY - 1, TileID.ClosedDoor, s.Door);
            }

            if (doorEast)
            {
                Air(x1 - 1, baseY - 3, x1, baseY - 1);
                Reveal(x1 - 1, baseY - 3, x1, baseY - 1);
                c.Place(x1, baseY - 1, TileID.ClosedDoor, s.Door);
            }

            Crown(x0, x1, top, roof);
        }

        /// <summary>The back of a tower room: its registers, and a window as tall as the room allows, standing on the rail.</summary>
        private void DressTowerRoom(Room r, int storey, Scheme s = null)
        {
            s ??= Scheme.Tower;
            Registers(r, s);

            if (r.H < 8 || r.W < 5)
                return;

            int half = r.W >= 9 ? 1 : 0;
            int mid = (r.X0 + r.X1) / 2;
            int sill = r.Floor - s.DadoH - 3;
            int head = r.Top + 3;
            if (sill - head < 2)
                return;
            Window(mid - half, mid + half + (r.W % 2 == 0 ? 1 : 0), sill, head + 1, 1);
        }

        /// <summary>What goes on top of a tower whose top solid row is <paramref name="top"/>.</summary>
        private void Crown(int x0, int x1, int top, Roof roof)
        {
            int cx = (x0 + x1) / 2;
            int w = x1 - x0 + 1;

            switch (roof)
            {
                case Roof.Battlements:
                    Box(x0 - 1, top, x1 + 1, top, TileID.AncientGoldBrick, NoPaint);
                    Merlons(x0 - 1, x1 + 1, top, 2, 2, 3, TileID.MarbleBlock, White);
                    break;

                case Roof.Dome:
                {
                    Box(x0 - 1, top - 1, x1 + 1, top, TileID.MarbleBlock, White);
                    Box(x0 - 1, top, x1 + 1, top, TileID.AncientGoldBrick, NoPaint);
                    int r = w / 2 + 1;
                    Dome(cx, top - 2, r, r + 2, 2, TileID.GoldBrick, NoPaint, WallID.GoldBrick, NoPaint);
                    Spire(cx, top - r - 4, 3, Math.Max(6, w), TileID.GoldBrick, NoPaint);
                    break;
                }

                case Roof.Spire:
                    Box(x0 - 1, top, x1 + 1, top, TileID.AncientGoldBrick, NoPaint);
                    Needle(cx, top - 1, w + 1, w * 2, TileID.MarbleBlock, White, WallID.MarbleBlock, White);
                    for (int y = top - 7; y > top - w * 2 + 4; y -= 8)
                        for (int x = cx - w; x <= cx + w; x++)
                            if (c.TypeAt(x, y) == TileID.MarbleBlock)
                                c.Block(x, y, TileID.AncientGoldBrick, NoPaint, c.CutAt(x, y));
                    Spire(cx, top - w * 2, 3, 6, TileID.GoldBrick, NoPaint);
                    break;

                case Roof.Pitched:
                    Pitch(x0 - 2, x1 + 2, top, 2, TileID.RedDynastyShingles, NoPaint, WallID.Wood, NoPaint);
                    break;

                default:
                    Box(x0 - 1, top, x1 + 1, top, TileID.MarbleBlock, White);
                    break;
            }
        }

        // ------------------------------------------------------------ houses

        /// <summary>
        /// A house [x0, x1] standing on <paramref name="baseY"/>: storeys of the given height,
        /// stuccoed outside, a window on each storey, a door at the foot, a pitched roof, each
        /// storey fitted out for its theme.
        /// </summary>
        private void House(int x0, int x1, int baseY, int storeys, int storeyH, ushort skin, byte skinPaint,
            ushort wall, byte wallPaint, ushort roofType, byte roofPaint, Suite s, params Theme[] themes)
        {
            int top = baseY - storeys * storeyH - 1;
            Box(x0, top, x1, baseY - 1, skin, skinPaint);

            for (int k = 0; k < storeys; k++)
            {
                int floor = baseY - k * storeyH;
                int ceil = floor - storeyH;
                var room = new Room(x0 + 2, x1 - 2, ceil + 1, floor);
                Hollow(room.X0, room.Top, room.X1, floor - 1, wall, wallPaint);

                // A window in the back wall, glazed and framed.
                int wx = room.Mid - 1;
                int wy = floor - storeyH / 2 - 1;
                Back(wx - 1, wy - 1, wx + 3, wy + 3, WallID.PearlstoneBrick, White);
                Back(wx, wy, wx + 2, wy + 2, WallID.Glass, NoPaint);

                Theme t = themes.Length > 0 ? themes[k % themes.Length] : Theme.Bedroom;
                Furnish(room, t, s);

                // Stairs between storeys: platforms at one end.
                if (k > 0)
                {
                    int hx = k % 2 == 1 ? room.X1 - 2 : room.X0;
                    for (int x = hx; x <= hx + 2; x++)
                        c.Platform(x, floor, s.Platform);
                }
            }

            // Door at the foot, on the west side.
            Air(x0, baseY - 3, x0 + 1, baseY - 1);
            c.Place(x0, baseY - 1, TileID.ClosedDoor, s.Door);

            // Eaves course and roof.
            Box(x0 - 1, top, x1 + 1, top, TileID.MarbleBlock, White);
            Pitch(x0 - 2, x1 + 2, top - 1, 2, roofType, roofPaint, WallID.Wood, NoPaint);
        }

        // ------------------------------------------------------------ street things

        private void Lamppost(int x, int stand) => c.Place(x, stand, TileID.Lampposts, 0);

        private void Bench(int x, int stand, Suite s) => c.Place(x, stand, TileID.Benches, s.Bench);

        private void Standpipe(int x, int stand) => c.Place(x, stand, TileID.WaterFountain, 0);
    }
}
