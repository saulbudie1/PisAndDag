using System;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The island's lighthouse (LighthouseIsland) and what stands round it on the plateau: the oil
    /// house east of it, the flagstaff and a lamp to the west, the stair cut down the rock to the
    /// beach.
    ///
    /// Built after a picture of a brick tower the user sent, by request ("use bricks colored white
    /// wall inside is ok, make it elevated like this with pillar statues and other statues like
    /// this, with marble and other tiles painted white or cream. create the stair case like that.
    /// windows like that too, pretty sure they use a different wall and tile of glass"; "I suggest
    /// taking your time to reseaerch terraria builds to give its rich, no wasted space vibe. and
    /// paint mixing and hammering"), and drawn in section, as the first picture asked for had it
    /// ("2nd pic from reddit looks beautiful": every floor fitted out, a stair up the middle, the
    /// gallery and the lantern on top):
    ///   - raised on a plinth of white stone, a colonnade in its face: pillar statues and two
    ///     statues of the old marble halls (Medusa's and a hoplite's), whitened, on pedestals,
    ///     between strips of bare slab, a lamp hung either side of each; a stair of hammered slab
    ///     down each end of it, a band of marble under its treads;
    ///   - five storeys of twelve rows, in bands of red brick and white, each laid of one brick
    ///     with its colour varied by paint (a few burnt dark, a few gone brown; the white a little
    ///     uneven), a course of white slab standing proud at every floor, white quoins at the
    ///     corners of the red bands; stepping in at the third storey and the fifth;
    ///   - inside, the back walls whitewashed brick and bare red brick in great stepped patches,
    ///     posts of boreal at each end of a room with a brace up to the beams, boards for floors;
    ///   - up the middle a shaft of dark boards between two posts, and in it the stair of plain
    ///     pale boards, to be seen against them: in each storey a flight one way and a flight
    ///     back, turning on a landing, up through a hatch in the floor over it;
    ///   - a window in the back of every room: glass blocks over an iron lattice painted brown, the
    ///     sky through it, in a frame of white slab (the picture's windows; the way in that it is
    ///     glass in front of a wall of another kind); in the white bands and the watch room,
    ///     windows of the same through the skin either side, a sill and a hood of white slab;
    ///   - every room fitted out for the keepers' work and life and nothing left bare, and lit
    ///     warm, by brass (<see cref="LightChandelier"/>): the hall and the store; the kitchen and
    ///     the dining room; the keeper's room and his assistant's; the library and the study; the
    ///     watch room under the lantern.
    /// Then the gallery, an iron deck round the lantern with a railing; the lantern, glass all
    /// round between an iron foot and an iron ring, a door out either side, the lens on its
    /// pedestal in the middle; the dome over it, copper painted red, a black ball on top and the
    /// vane on that.
    ///
    /// No storey of it is a house a town NPC would take: every storey has a light and a way in,
    /// so each has things to sit on or a table (the game counts bookcases, pianos, dressers and
    /// work benches as tables), never both (the Unhouse pass over the island checks it). Chests in
    /// it hold what the room was for (GraceLoot).
    /// </summary>
    public sealed partial class GraceBuilder
    {
        // ------------------------------------------------------------ the tower's plan

        /// <summary>The plinth's deck, the tower's ground floor: eight rows over the turf.</summary>
        private const int LightPlinthTop = Plateau - 8;

        /// <summary>Half the plinth's width, its end walls included: three tiles of terrace either side of the tower's foot.</summary>
        private const int LightPlinthHalf = 18;

        /// <summary>The ground floor's row.</summary>
        private const int LightFloor0 = LightPlinthTop;

        /// <summary>The rows a storey takes, its floor included; the storeys.</summary>
        private const int LightStorey = 12, LightStoreys = 5;

        /// <summary>The gallery's deck: over the last storey, as its floor would be.</summary>
        private const int LightDeckY = LightFloor0 - LightStorey * LightStoreys;

        /// <summary>The row of storey k's floor (storey <see cref="LightStoreys"/>'s is the deck).</summary>
        private static int LightFloor(int k) => LightFloor0 - LightStorey * k;

        /// <summary>Half the tower's width at storey k, its skin included.</summary>
        private static int LightHalf(int k) => k < 2 ? 15 : k < 4 ? 14 : 13;

        /// <summary>Storey k is a red band (the ground floor, the third, the watch room) or a white one.</summary>
        private static bool LightRedBand(int k) => k % 2 == 0;

        /// <summary>The shaft's posts stand this far either side of the middle; the stair turns inside them.</summary>
        private const int LightShaft = 4;

        /// <summary>The red of the dome.</summary>
        private const byte LightRedPaint = PaintID.RedPaint;

        /// <summary>The room of storey k, inside its skin, from under the floor over it down to its own floor.</summary>
        private static Room LightRoom(int k) => new Room(LightX - LightHalf(k) + 2, LightX + LightHalf(k) - 2, LightFloor(k + 1) + 1, LightFloor(k));

        /// <summary>Boreal wood, the lighthouse's furniture: its style of each piece (the items' place styles, 1.4.4); its lights are not hung here (<see cref="LightChandelier"/>).</summary>
        private static readonly Suite IsleBoreal = new Suite
        {
            Chair = 30, Table = 28, Bed = 24, Door = 30, Chandelier = 25, Lantern = 29, Bookcase = 25, Piano = 23,
            Clock = 6, Sofa = 24, Sink = 25, Bathtub = 19, Dresser = 18, Bench = 24, Chest = 33, Candle = 20,
            Candelabra = 20, Lamp = 20, Platform = 19,
        };

        /// <summary>Palm wood, the fisherman's and his boats'.</summary>
        private static readonly Suite IslePalm = new Suite
        {
            Chair = 29, Table = 26, Bed = 22, Door = 29, Chandelier = 23, Lantern = 27, Bookcase = 23, Piano = 21,
            Clock = 18, Sofa = 22, Sink = 23, Bathtub = 17, Dresser = 16, Bench = 21, Chest = 31, Candle = 18,
            Candelabra = 18, Lamp = 18, Platform = 17,
        };

        /// <summary>The place styles used here beyond the suite (1.4.4): the Pillar, Medusa and Hoplite Statues; the Wood Shelf; the Wood Platform the stair is laid of.</summary>
        private const int LightPillar = 36, LightMedusa = 69, LightHoplite = 72, LightShelf = 11, LightStair = 0;

        /// <summary>
        /// The lights, brass and iron, not the boreal set's own: the game burns every boreal light
        /// cyan (TileLightScanner.ApplyTileLight gives them 0, 0.9, 1), and a keeper's rooms want a
        /// warm one. The Gold Chandelier, the Brass Lantern, the Golden Lamp, the plain Candle, and
        /// the Carriage Lantern of black iron for the oil house (place styles, 1.4.4).
        /// </summary>
        private const int LightChandelier = 2, LightBrass = 1, LightLamp = 15, LightCandle = 0, LightCarriage = 3;

        private const byte Black = PaintID.BlackPaint;

        /// <summary>The iron door's style (TileID.ClosedDoor; the Iron Door item's place style, 1.4.4).</summary>
        private const int LightIronDoor = 15;

        // ------------------------------------------------------------ the tower

        private void IsleTower()
        {
            int seed = c.Next(1, 1 << 20);

            LightPlinth();
            LightShell(seed);
            LightInside(seed);

            for (int k = 0; k < LightStoreys; k++)
                LightFlights(LightFloor(k), k == LightStoreys - 1);

            LightDoors();
            LightWindows();
            LightGallery();
            LightLantern();
            LightFurnish();
        }

        // ------------------------------------------------------------ the plinth

        /// <summary>
        /// The plinth: a footing of brick down into the plateau; a base course of white slab; a
        /// colonnade the height of a room, its back whitewashed brick, strips of bare slab every
        /// fifth column from the middle out, and between them, on pedestals of white marble, pillar
        /// statues and two statues, whitened; a frieze of white brick and the deck of white slab
        /// over it, the deck three tiles wide either side of the tower's foot; a stair down each end,
        /// a tread a row, hammered, a band of marble under the treads and white brick under that.
        /// </summary>
        private void LightPlinth()
        {
            const int cx = LightX;
            const int deck = LightPlinthTop;
            int x0 = cx - LightPlinthHalf, x1 = cx + LightPlinthHalf;
            int baseRow = Plateau - 1;
            int run = baseRow - deck;

            Box(x0 - run, Plateau, x1 + run, Plateau + 1, TileID.GrayBrick);

            Box(x0, baseRow, x1, baseRow, TileID.StoneSlab, White);
            Box(x0, deck + 1, x1, deck + 1, TileID.GrayBrick, White);
            Box(x0, deck, x1, deck, TileID.StoneSlab, White);

            for (int y = deck + 2; y < baseRow; y++)
            {
                c.Block(x0, y, TileID.GrayBrick, White);
                c.Block(x1, y, TileID.GrayBrick, White);
            }

            Hollow(x0 + 1, deck + 2, x1 - 1, baseRow - 1, WallID.PearlstoneBrick, White);
            for (int k = -3; k <= 3; k++)
                for (int y = deck + 2; y < baseRow; y++)
                    c.Wall(cx + 5 * k, y, WallID.StoneSlab);

            // Between each two strips a pedestal and on it a pillar or a statue: pillar, statue,
            // pillar either side of the middle strip, the statues looking in.
            int[] at = { -13, -8, -3, 2, 7, 12 };
            for (int i = 0; i < at.Length; i++)
            {
                int x = cx + at[i];
                Box(x, baseRow - 2, x + 1, baseRow - 1, TileID.MarbleBlock, White);

                int style = i == 1 ? LightMedusa : i == 4 ? LightHoplite : LightPillar;
                int facing = at[i] < 0 ? 1 : -1;
                if (c.Place(x, baseRow - 3, TileID.Statues, style, facing))
                    for (int dx = 0; dx < 2; dx++)
                        for (int y = baseRow - 5; y <= baseRow - 3; y++)
                            c.Paint(x + dx, y, White);
            }

            // A brass lamp hung under the frieze either side of each statue and of the middle strip.
            foreach (int dx in new[] { -9, -6, -1, 1, 6, 9 })
                Hang(cx + dx, deck + 2, TileID.HangingLanterns, LightBrass);

            // The stairs: a tread a row down from the deck, each hammered to a slope; under the
            // treads a band of marble two deep, white brick under that, down to the turf.
            foreach (int side in new[] { -1, 1 })
            {
                for (int j = 1; j <= run; j++)
                {
                    int x = side < 0 ? x0 - j : x1 + j;
                    int top = deck + j;

                    c.Block(x, top, TileID.StoneSlab, White, side < 0 ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
                    for (int y = top + 1; y <= baseRow; y++)
                        c.Block(x, y, y == top + 2 || y == top + 3 ? TileID.MarbleBlock : TileID.GrayBrick, White);
                }
            }
        }

        // ------------------------------------------------------------ the skin

        /// <summary>
        /// The tower's skin, storey by storey, in its bands (<see cref="LightBrick"/>); a course of
        /// white slab at every floor, standing a tile proud and chamfered underneath at its ends,
        /// covering the step where the tower comes in; white quoins two wide at the corners of the
        /// red bands, two blocks of three rows a storey with a row of brick between. The watch room
        /// has windows in its skin instead.
        /// </summary>
        private void LightShell(int seed)
        {
            const int cx = LightX;

            for (int k = 0; k < LightStoreys; k++)
            {
                int h = LightHalf(k), f = LightFloor(k);
                bool red = LightRedBand(k);

                for (int x = cx - h; x <= cx + h; x++)
                    for (int y = f - LightStorey + 1; y < f; y++)
                        LightBrick(x, y, red, seed);

                if (!red || k == LightStoreys - 1)
                    continue;

                foreach (int side in new[] { -1, 1 })
                {
                    for (int y = f - LightStorey + 1; y < f; y++)
                    {
                        int fromFloor = f - y;
                        bool quoin = fromFloor >= 4 && fromFloor <= 6 || fromFloor >= 8 && fromFloor <= 10;
                        if (!quoin)
                            continue;

                        c.Block(cx + side * h, y, TileID.StoneSlab, White);
                        c.Block(cx + side * (h - 1), y, TileID.StoneSlab, White);
                    }
                }
            }

            for (int k = 1; k < LightStoreys; k++)
            {
                int hb = LightHalf(k - 1), f = LightFloor(k);
                Box(cx - hb - 1, f, cx + hb + 1, f, TileID.StoneSlab, White);
                Cut(cx - hb - 1, f, Grace.Cut.UpLeft);
                Cut(cx + hb + 1, f, Grace.Cut.UpRight);
            }
        }

        /// <summary>
        /// A brick of the tower's skin, its colour varied by paint as weathered brick is ("paint
        /// mixing"), one brick throughout so its joints run on: in a red band red brick, one in
        /// twelve burnt dark and one in twenty gone brown; in a white band brick painted white, one
        /// in ten left its own pale colour and one in twenty gone grey.
        /// </summary>
        private void LightBrick(int x, int y, bool red, int seed)
        {
            double n = Hash01(x, y, seed);

            if (red)
                c.Block(x, y, TileID.RedBrick, n < 0.08 ? PaintID.DeepRedPaint : n < 0.13 ? PaintID.BrownPaint : NoPaint);
            else
                c.Block(x, y, TileID.PearlstoneBrick, n < 0.1 ? NoPaint : n < 0.15 ? Gray : White);
        }

        // ------------------------------------------------------------ the inside

        /// <summary>
        /// Every storey hollowed: its side rooms walled in patches (<see cref="LightPatches"/>), the
        /// shaft in dark boards between its two posts, a post at each end of the storey with a
        /// brace of boards up to the beams; and boards for every floor over the ground floor.
        /// </summary>
        private void LightInside(int seed)
        {
            const int cx = LightX;
            int style = IsleBoreal.Platform;

            for (int k = 0; k < LightStoreys; k++)
            {
                Room r = LightRoom(k);

                Air(r.X0, r.Top, r.X1, r.Floor - 1);
                LightPatches(r.X0, cx - LightShaft - 1, r.Top, r.Floor, seed + 7 * k);
                LightPatches(cx + LightShaft + 1, r.X1, r.Top, r.Floor, seed + 7 * k + 3);
                Back(cx - LightShaft, r.Top, cx + LightShaft, r.Floor - 1, WallID.Planked);

                if (k > 0)
                {
                    Room below = LightRoom(k - 1);
                    Box(below.X0, r.Floor, below.X1, r.Floor, TileID.BorealWood);
                }

                for (int y = r.Top; y < r.Floor; y++)
                    foreach (int x in new[] { r.X0, cx - LightShaft, cx + LightShaft, r.X1 })
                        c.Block(x, y, TileID.BorealBeam);

                for (int i = 0; i < 3; i++)
                {
                    c.Platform(r.X0 + 1 + i, r.Top + 2 - i, style, Grace.Cut.DownLeft);
                    c.Platform(r.X1 - 1 - i, r.Top + 2 - i, style, Grace.Cut.DownRight);
                }
            }
        }

        /// <summary>
        /// A side room's back wall as the picture had it: brick whitewashed and brick left red, in
        /// great patches with a stepped edge falling two columns to a row, as if the wash had come
        /// away down the wall in steps; which side is higher, how high the edge comes and which of
        /// the two is over it, room by room.
        /// </summary>
        private void LightPatches(int x0, int x1, int top, int floor, int seed)
        {
            bool fallsRight = Hash01(x0, top, seed) < 0.5;
            bool whiteOver = Hash01(x1, top, seed + 1) < 0.7;
            int mid = top + (int)Math.Round((floor - top) * (0.3 + 0.4 * Hash01(x0, floor, seed + 2)));
            int w = x1 - x0 + 1;

            for (int x = x0; x <= x1; x++)
            {
                int along = fallsRight ? x - x0 : x1 - x;
                int edge = mid - w / 4 + along / 2;

                for (int y = top; y < floor; y++)
                {
                    bool white = y < edge == whiteOver;
                    c.Wall(x, y, white ? WallID.PearlstoneBrick : WallID.RedBrick, white ? White : NoPaint);
                }
            }
        }

        // ------------------------------------------------------------ the stair

        /// <summary>
        /// The stair in a storey whose floor is row <paramref name="f"/>, inside the shaft: a flight
        /// of boards up toward the east from a landing at its foot, a tread a row; a landing; a flight
        /// back up toward the west to the floor over it, its last tread laid in that floor, and the
        /// floor opened there with boards where a head goes up through. In the watch room only the
        /// first flight, the rest of the way up by rungs to the gallery (<see cref="LightGallery"/>).
        /// </summary>
        private void LightFlights(int f, bool last)
        {
            const int cx = LightX;
            const int style = LightStair;

            for (int i = 0; i < 6; i++)
            {
                int x = cx - 3 + i, y = f - 1 - i;
                c.Erase(x, y);
                c.Platform(x, y, style, i == 0 ? Grace.Cut.Full : Grace.Cut.DownLeft);
            }

            if (last)
                return;

            for (int j = 0; j < 6; j++)
            {
                int x = cx + 3 - j, y = f - 7 - j;
                c.Erase(x, y);
                c.Platform(x, y, style, j == 0 || j == 5 ? Grace.Cut.Full : Grace.Cut.DownRight);
            }

            for (int x = cx - 1; x <= cx + 1; x++)
            {
                c.Erase(x, f - LightStorey);
                c.Platform(x, f - LightStorey, style);
            }
        }

        // ------------------------------------------------------------ ways in, ways to see out

        /// <summary>A door at each end of the ground floor out onto the plinth's deck, a hood of white slab over each, a lamp on the deck beside each.</summary>
        private void LightDoors()
        {
            const int cx = LightX;
            int f0 = LightFloor0;

            foreach (int side in new[] { -1, 1 })
            {
                int outer = cx + side * LightHalf(0), inner = outer - side;
                int a = Math.Min(outer, inner), b = Math.Max(outer, inner);

                Air(a, f0 - 3, b, f0 - 1);
                Reveal(a, f0 - 3, b, f0 - 1);
                c.Place(outer, f0 - 1, TileID.ClosedDoor, IsleBoreal.Door);

                c.Block(outer, f0 - 4, TileID.StoneSlab, White);
                c.Block(inner, f0 - 4, TileID.StoneSlab, White);
                c.Block(outer + side, f0 - 4, TileID.StoneSlab, White, side < 0 ? Grace.Cut.DownLeft : Grace.Cut.DownRight);

                c.Place(cx + side * (LightPlinthHalf - 1), f0 - 1, TileID.Lampposts);
            }
        }

        /// <summary>
        /// A window in the back of every side room, in the middle of the part the stair leaves it
        /// (<see cref="LightBackWindow"/>); in the white bands and the watch room, one through the
        /// skin either side (<see cref="LightSideWindow"/>).
        /// </summary>
        private void LightWindows()
        {
            for (int k = 0; k < LightStoreys; k++)
            {
                int h = LightHalf(k), f = LightFloor(k);

                foreach (int side in new[] { -1, 1 })
                {
                    LightBackWindow(side < 0 ? LightX - (h - 4) : LightX + (h - 6), f);

                    if (k > 0 && (!LightRedBand(k) || k == LightStoreys - 1))
                        LightSideWindow(side, k);
                }
            }
        }

        /// <summary>
        /// A window in a room's back wall, three wide and five high from the fourth row over the
        /// floor (a head clears it): glass blocks over a lattice of iron painted brown, the sky
        /// through the lattice, in a frame of white slab a tile wide all round.
        /// </summary>
        private void LightBackWindow(int x0, int f)
        {
            Back(x0 - 1, f - 9, x0 + 3, f - 3, WallID.StoneSlab, White);

            for (int x = x0; x <= x0 + 2; x++)
            {
                for (int y = f - 8; y <= f - 4; y++)
                {
                    c.Wall(x, y, WallID.IronFence, PaintID.BrownPaint);
                    c.Block(x, y, TileID.Glass);
                }
            }
        }

        /// <summary>A window through the skin, two wide and five high, of the same glass and lattice; a sill under it and a hood over it of white slab, standing a tile proud.</summary>
        private void LightSideWindow(int side, int k)
        {
            int h = LightHalf(k), f = LightFloor(k);
            int outer = LightX + side * h, inner = outer - side;

            for (int y = f - 8; y <= f - 4; y++)
            {
                foreach (int x in new[] { outer, inner })
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.IronFence, PaintID.BrownPaint);
                    c.Block(x, y, TileID.Glass);
                }
            }

            foreach (int y in new[] { f - 3, f - 9 })
            {
                bool sill = y == f - 3;
                c.Block(outer, y, TileID.StoneSlab, White);
                c.Block(inner, y, TileID.StoneSlab, White);
                c.Block(outer + side, y, TileID.StoneSlab, White,
                    sill ? (side < 0 ? Grace.Cut.UpLeft : Grace.Cut.UpRight) : (side < 0 ? Grace.Cut.DownLeft : Grace.Cut.DownRight));
            }
        }

        // ------------------------------------------------------------ the top

        /// <summary>The gallery: an iron deck round the lantern on brackets, its railing, and the way up to it from the watch room: a rung, and a hatch in the deck over it.</summary>
        private void LightGallery()
        {
            const int cx = LightX;
            const int deck = LightDeckY;
            int reach = LightHalf(LightStoreys - 1) + 3;

            Box(cx - reach, deck, cx + reach, deck, TileID.IronBrick, Black);
            Cut(cx - reach, deck, Grace.Cut.UpLeft);
            Cut(cx + reach, deck, Grace.Cut.UpRight);

            foreach (int side in new[] { -1, 1 })
            {
                int x = cx + side * (LightHalf(LightStoreys - 1) + 1);
                Cut under = side < 0 ? Grace.Cut.UpLeft : Grace.Cut.UpRight;

                c.Block(x, deck + 1, TileID.IronBrick, Black);
                c.Block(x + side, deck + 1, TileID.IronBrick, Black, under);
                c.Block(x, deck + 2, TileID.IronBrick, Black, under);
            }

            for (int x = cx - reach; x <= cx + reach; x++)
            {
                if (Math.Abs(x - cx) <= 6)
                    continue;

                for (int y = deck - 2; y < deck; y++)
                    c.Wall(x, y, WallID.WroughtIronFence);
            }

            for (int x = cx + 1; x <= cx + 3; x++)
            {
                c.Platform(x, deck + 3, LightStair);
                c.Erase(x, deck);
                c.Platform(x, deck, LightStair);
            }
        }

        /// <summary>
        /// The lantern: glass all round over an iron foot, a bar of iron across its middle, a door
        /// out onto the gallery either side; the ring over it; in it, the lens on its pedestal of
        /// iron and brass, and a flue from the lamp up to the roof. Over that the dome, the ball
        /// and the vane.
        /// </summary>
        private void LightLantern()
        {
            const int cx = LightX;
            const int deck = LightDeckY;
            int ring = deck - 11;

            Hollow(cx - 5, ring + 1, cx + 5, deck - 1, WallID.Glass);

            foreach (int side in new[] { -1, 1 })
            {
                int x = cx + side * 6;
                Glaze(x, ring + 1, x, deck - 4);
                c.Block(x, deck - 6, TileID.IronBrick, Black);
                c.Place(x, deck - 1, TileID.ClosedDoor, LightIronDoor);
            }

            Box(cx - 7, ring, cx + 7, ring, TileID.IronBrick, Black);
            Cut(cx - 7, ring, Grace.Cut.UpLeft);
            Cut(cx + 7, ring, Grace.Cut.UpRight);

            Box(cx - 1, deck - 1, cx + 1, deck - 1, TileID.IronBrick, Black);
            Box(cx - 1, deck - 2, cx + 1, deck - 2, TileID.CopperPlating);
            if (isle.Lens != 0)
                c.Place(IsleLensX, IsleLensY + 3, isle.Lens);
            c.Mark("Isle.Lens", IsleLensX, IsleLensY, 3, 4);

            for (int y = ring + 1; y < IsleLensY; y++)
                c.Block(cx, y, TileID.WoodenBeam, Black);

            LightDome(cx, ring - 1, 7, 6);

            int crown = ring - 1 - 6;
            Box(cx - 1, crown - 1, cx + 1, crown - 1, TileID.CopperPlating, Black);
            Box(cx, crown - 2, cx + 1, crown - 2, TileID.CopperPlating, Black);
            Cut(cx - 1, crown - 1, Grace.Cut.DownLeft);
            c.Place(cx, crown - 3, TileID.WeatherVane);
        }

        /// <summary>
        /// The lantern's roof: a dome of copper painted red over [cx - r, cx + r], standing on
        /// <paramref name="baseY"/>, <paramref name="h"/> high, solid (as the dome of a lantern is seen
        /// from outside, not in section), each step of its curve cut, its foot a band of black.
        /// </summary>
        private void LightDome(int cx, int baseY, int r, int h)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                double u = (x - cx) / (r + 0.5);
                int top = baseY - (int)Math.Round(h * Math.Sqrt(Math.Max(0, 1 - u * u)));

                for (int y = top; y <= baseY; y++)
                    c.Block(x, y, TileID.CopperPlating, y == baseY ? Black : LightRedPaint);
            }

            for (int x = cx - r; x <= cx + r; x++)
            {
                int top = TopOf(x, baseY - h - 1, baseY);
                if (top == int.MinValue)
                    continue;

                if (x < cx && !c.Solid(x - 1, top) && c.Solid(x + 1, top))
                    Cut(x, top, Grace.Cut.DownLeft);
                else if (x > cx && !c.Solid(x + 1, top) && c.Solid(x - 1, top))
                    Cut(x, top, Grace.Cut.DownRight);
            }
        }

        // ------------------------------------------------------------ the rooms fitted out

        /// <summary>
        /// The left column of something <paramref name="w"/> wide standing in a side room, its
        /// nearest column to the shaft <paramref name="r"/> columns out from the middle.
        /// </summary>
        private static int LightAt(int side, int r, int w) => side < 0 ? LightX - r - w + 1 : LightX + r;

        /// <summary>The Signal Pistol, in the watch room's east sea chest (PADWorkFlow §16.98).</summary>
        private static int SignalPistolItem => ModContent.ItemType<global::PisAndDag.Content.Items.Ranged.SignalPistol>();

        /// <summary>
        /// Each storey as it was kept, from the ground up, nothing left bare (by request: "its rich,
        /// no wasted space vibe"). Each side room the same in its bones: a chandelier hung by the
        /// shaft, a banner under the beams, a lantern either side of the stair; under the window
        /// only what stands no higher than a head; the tall things by the shaft; a picture, a
        /// shelf or a ship's wheel over them. A storey with something to sit on has no table, and one
        /// with a table nothing to sit on.
        /// </summary>
        private void LightFurnish()
        {
            Suite s = IsleBoreal;

            for (int k = 0; k < LightStoreys; k++)
            {
                int f = LightFloor(k), stand = f - 1, h = LightHalf(k);
                int ceilTop = LightFloor(k + 1) + 1;

                foreach (int side in new[] { -1, 1 })
                {
                    Hang(LightAt(side, 5, 3), ceilTop, TileID.Chandeliers, LightChandelier);
                    if (h >= 14)
                        Hang(LightAt(side, h - 6, 1), ceilTop, TileID.Banners, LightRedBand(k) ? 1 : 0);
                }

                // A lantern either side of the stair; in the watch room only the west one, the
                // hatch up to the gallery being over the east.
                Hang(LightX - 3, ceilTop, TileID.HangingLanterns, LightBrass);
                if (k < LightStoreys - 1)
                    Hang(LightX + 3, ceilTop, TileID.HangingLanterns, LightBrass);

                switch (k)
                {
                    case 0:
                        // The hall and the store: the clock, a lamp, the dresser with a candle and
                        // a mug on it, a keg by the door; kegs, the barrel of stores and a crate.
                        c.Place(LightAt(-1, 5, 2), stand, TileID.GrandfatherClocks, s.Clock);
                        c.Place(LightAt(-1, 7, 1), stand, TileID.Lamps, LightLamp);
                        c.Place(LightAt(-1, 8, 3), stand, TileID.Dressers, s.Dresser);
                        c.Place(LightAt(-1, 10, 1), stand - 2, TileID.Candles, LightCandle);
                        c.Place(LightAt(-1, 8, 1), stand - 2, TileID.Bottles, 4);
                        c.Place(LightAt(-1, 11, 2), stand, TileID.Kegs);
                        c.Place(LightAt(-1, 5, 2), stand - 5, TileID.Painting2X3, 9);

                        c.Place(LightAt(1, 5, 2), stand, TileID.Kegs);
                        c.PlaceChest(LightAt(1, 7, 2), stand, TileID.Containers, 5, Roll(Loot.Store));
                        c.Place(LightAt(1, 9, 2), stand, TileID.FishingCrate);
                        c.Place(LightAt(1, 11, 2), stand, TileID.Kegs);
                        c.Place(LightAt(1, 5, 3), stand - 5, TileID.Painting3X3, 49);
                        break;

                    case 1:
                        // The kitchen: the pot, the sink, the table with a bowl and a mug on it, a
                        // pot of flowers; a shelf of bottles and a book. The dining room: a lamp, the
                        // table with the cake and a platter, the chest, a lamp, a picture.
                        c.Place(LightAt(-1, 5, 2), stand, TileID.CookingPots);
                        c.Place(LightAt(-1, 7, 2), stand, TileID.Sinks, s.Sink);
                        c.Place(LightAt(-1, 9, 3), stand, TileID.Tables, s.Table);
                        c.Place(LightAt(-1, 9, 2), stand - 2, TileID.Bowls);
                        c.Place(LightAt(-1, 11, 1), stand - 2, TileID.Bottles, 4);
                        c.Place(LightAt(-1, 12, 1), stand, TileID.ClayPot);
                        LightShelfOf(-1, f - 5, (TileID.Bottles, 0), (TileID.Bottles, 3), (TileID.Books, 0));

                        c.Place(LightAt(1, 5, 1), stand, TileID.Lamps, LightLamp);
                        c.Place(LightAt(1, 6, 3), stand, TileID.Tables, s.Table);
                        c.Place(LightAt(1, 6, 2), stand - 2, TileID.SliceOfCake);
                        c.Place(LightAt(1, 8, 1), stand - 2, TileID.FoodPlatter);
                        c.PlaceChest(LightAt(1, 9, 2), stand, TileID.Containers, s.Chest, Roll(Loot.Home));
                        c.Place(LightAt(1, 11, 1), stand, TileID.Lamps, LightLamp);
                        c.Place(LightAt(1, 5, 3), stand - 5, TileID.Painting3X3, 20);
                        break;

                    case 2:
                        // The keeper's room: his clock, a lamp, his bed under the window, a
                        // picture. His assistant's: his chest, a chair, his bed, a picture.
                        c.Place(LightAt(-1, 5, 2), stand, TileID.GrandfatherClocks, s.Clock);
                        c.Place(LightAt(-1, 7, 1), stand, TileID.Lamps, LightLamp);
                        c.Place(LightAt(-1, 8, 4), stand, TileID.Beds, s.Bed, 1);
                        c.Place(LightAt(-1, 5, 2), stand - 5, TileID.Painting2X3, 3);

                        c.PlaceChest(LightAt(1, 5, 2), stand, TileID.Containers, s.Chest, Roll(Loot.Home));
                        c.Place(LightAt(1, 7, 1), stand, TileID.Chairs, s.Chair, 1);
                        c.Place(LightAt(1, 8, 4), stand, TileID.Beds, s.Bed, -1);
                        c.Place(LightAt(1, 5, 2), stand - 5, TileID.Painting2X3, 8);
                        break;

                    case 3:
                        // The library: the books; the table with a candle, a book and a bottle on
                        // it; a lamp; the ship in a bottle on a shelf over the books. The study:
                        // the books, the piano with a candle and a book, a lamp, a chart on a shelf.
                        c.Place(LightAt(-1, 5, 3), stand, TileID.Bookcases, s.Bookcase);
                        c.Place(LightAt(-1, 8, 3), stand, TileID.Tables, s.Table);
                        c.Place(LightAt(-1, 8, 1), stand - 2, TileID.Candles, LightCandle);
                        c.Place(LightAt(-1, 9, 1), stand - 2, TileID.Books, 0);
                        c.Place(LightAt(-1, 10, 1), stand - 2, TileID.Bottles, 6);
                        c.Place(LightAt(-1, 11, 1), stand, TileID.Lamps, LightLamp);
                        LightShelfOf(-1, f - 5, (TileID.ShipInABottle, 0));

                        c.Place(LightAt(1, 5, 3), stand, TileID.Bookcases, s.Bookcase);
                        c.Place(LightAt(1, 8, 3), stand, TileID.Pianos, s.Piano);
                        c.Place(LightAt(1, 8, 1), stand - 2, TileID.Candles, LightCandle);
                        c.Place(LightAt(1, 10, 1), stand - 2, TileID.Books, 0);
                        c.Place(LightAt(1, 11, 1), stand, TileID.Lamps, LightLamp);
                        c.Place(LightAt(1, 5, 2), stand - 5, TileID.Painting2X3, 19);
                        break;

                    default:
                        // The watch room: a sea chest each side, a settle under each window to sit
                        // the watch out, a chair at each window in the skin. The east one keeps the
                        // Signal Pistol, first (PADWorkFlow §16.98).
                        foreach (int side in new[] { -1, 1 })
                        {
                            var kept = Roll(Loot.Sea);

                            if (side > 0)
                                kept.Insert(0, (SignalPistolItem, 1));

                            c.PlaceChest(LightAt(side, 5, 2), stand, TileID.Containers, s.Chest, kept);
                            c.Place(LightAt(side, 7, 3), stand, TileID.Benches, s.Sofa);
                            c.Place(LightAt(side, 10, 1), stand, TileID.Chairs, s.Chair, side);
                        }

                        break;
                }
            }
        }

        /// <summary>A shelf of boards three wide by the shaft in a side room, row <paramref name="y"/>, with these things on it from the shaft out.</summary>
        private void LightShelfOf(int side, int y, params (ushort type, int style)[] things)
        {
            for (int r = 5; r <= 7; r++)
                c.Platform(LightAt(side, r, 1), y, LightShelf);

            int at = 5;
            foreach (var (type, style) in things)
            {
                var (w, _) = c.Size(type, style);
                c.Place(LightAt(side, at, w), y - 1, type, style);
                at += w;
            }
        }

        // ------------------------------------------------------------ the oil house

        /// <summary>
        /// The oil house: a low house of white brick on the turf east of the plinth, a door at
        /// each end, a window in its back wall, a roof of red shingle over a loft. In it the oil in
        /// its barrels and kegs, a crate, a chest, the heavy bench; a rung and a hatch up to the
        /// loft, where more is kept, gone to cobwebs.
        /// </summary>
        private void IsleOilHouse()
        {
            int x0 = LightX + LightPlinthHalf + 10, x1 = x0 + 21;
            int floor = Plateau - 1, ceil = floor - 7;
            int seed = c.Next(1, 1 << 20);

            Box(x0, Plateau, x1, Plateau, TileID.GrayBrick);
            for (int x = x0; x <= x1; x++)
                for (int y = ceil; y <= floor; y++)
                    LightBrick(x, y, false, seed);

            var r = new Room(x0 + 2, x1 - 2, ceil + 1, floor);
            Hollow(r.X0, r.Top, r.X1, r.Floor - 1, WallID.Stone);
            Box(x0 + 1, floor, x1 - 1, floor, TileID.StoneSlab);
            Box(r.X0, ceil, r.X1, ceil, TileID.BorealWood);
            Registers(r, Scheme.Store);
            Window(r.Mid - 1, r.Mid + 1, r.Floor - 3, r.Top + 2, 1);

            foreach (int x in new[] { x0, x1 })
            {
                int inner = x == x0 ? x0 + 1 : x1 - 1;
                Air(Math.Min(x, inner), floor - 3, Math.Max(x, inner), floor - 1);
                Reveal(Math.Min(x, inner), floor - 3, Math.Max(x, inner), floor - 1);
                c.Place(x, floor - 1, TileID.ClosedDoor, IsleBoreal.Door);
            }

            // The roof: shingle two thick rising a row every three columns to the ridge, its eaves
            // two over each end; the loft under it walled in wood.
            Pitch(x0 - 2, x1 + 2, ceil - 1, 3, TileID.RedDynastyShingles, NoPaint, WallID.Wood);

            // The way up to the loft: a rung, and a hatch in the ceiling over it.
            for (int x = r.X0; x <= r.X0 + 2; x++)
            {
                c.Platform(x, ceil + 3, IsleBoreal.Platform);
                c.Erase(x, ceil);
                c.Platform(x, ceil, IsleBoreal.Platform);
            }

            // What it holds.
            c.Place(r.X0 + 3, r.Stand, TileID.HeavyWorkBench);
            c.Place(r.X0 + 6, r.Stand, TileID.Kegs);
            c.PlaceChest(r.X0 + 8, r.Stand, TileID.Containers, 5, Roll(Loot.Store));
            c.Place(r.X0 + 11, r.Stand, TileID.FishingCrate);
            c.Place(r.X0 + 13, r.Stand, TileID.Kegs);
            c.Place(r.X0 + 15, r.Stand, TileID.LargePiles, 23);
            Hang(r.X0 + 5, r.Top, TileID.HangingLanterns, LightCarriage);
            Hang(r.X1 - 3, r.Top, TileID.HangingLanterns, LightCarriage);

            // The loft: a chest and crates, cobwebs in its ends.
            int loft = ceil - 1;
            c.PlaceChest(r.Mid - 3, loft, TileID.Containers, 0, Roll(Loot.Store));
            c.Place(r.Mid, loft, TileID.FishingCrate);
            c.Place(r.Mid + 3, loft, TileID.FishingCrate);

            foreach (int end in new[] { x0, x1 })
            {
                int dir = end == x0 ? 1 : -1;
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3 - i; j++)
                    {
                        int x = end + dir * (1 + i), y = loft - j;
                        if (c.TypeAt(x, y) < 0 && c.WallAt(x, y) != 0 && !c.OneIn(4))
                            c.Block(x, y, TileID.Cobweb);
                    }
            }
        }

        // ------------------------------------------------------------ the yards

        /// <summary>
        /// The turf either side of the plinth: to the west a tree and the flagstaff with the red flag
        /// on it; to the east a lamp at the head of the stair down to the beach.
        /// </summary>
        private void IsleYards()
        {
            const int stand = Plateau - 1;

            // The tree at the plateau's west end, its ground made level two either side (the game
            // grows a tree only with five columns open over its foot); the flagstaff past it.
            int treeX = PlateauX0;
            if (IsleLevel(treeX - 2, treeX + 2, Plateau, TileID.Grass))
                c.Tree(treeX, Plateau, TileID.Trees, 0);

            int pole = PlateauX0 + 4;
            for (int y = Plateau - 16; y <= stand; y++)
                c.Block(pole, y, TileID.WoodenBeam);
            c.Block(pole, Plateau - 17, TileID.WoodBlock, White, Grace.Cut.Half);
            c.Block(pole + 1, Plateau - 15, TileID.WoodBlock, White);
            Hang(pole + 1, Plateau - 14, TileID.Banners, 0);

            c.Place(PlateauX1 - 1, stand, TileID.Lampposts);
        }

        // ------------------------------------------------------------ the stair

        /// <summary>
        /// The stair down the rock from the plateau's east end to the beach: a step a column, slab,
        /// each worn to a slope so it walks as a ramp; a landing at its foot; a fence of palings
        /// behind it; a lamp on the sand at its foot.
        /// </summary>
        private void IsleStair()
        {
            for (int x = StairX0; x <= StairX1; x++)
            {
                int top = Math.Min(Plateau + 1 + (x - StairX0), StairFoot);
                bool step = x < StairX1 - 1;

                for (int y = top - 8; y < top; y++)
                    if (IsleNatural(c.TypeAt(x, y)))
                        c.Erase(x, y);

                c.Block(x, top, TileID.StoneSlab, NoPaint, step ? Grace.Cut.DownRight : Grace.Cut.Full);

                for (int y = top + 1; y <= top + 3; y++)
                    if (c.TypeAt(x, y) < 0)
                        c.Block(x, y, TileID.Stone);

                c.Wall(x, top - 1, WallID.WoodenFence);
                c.Wall(x, top - 2, WallID.WoodenFence);
            }

            int lx = StairX1 + 2;
            int g = c.FirstSolidDown(lx, -20, 10);
            if (g != int.MinValue)
            {
                c.Block(lx, g, TileID.Sand);
                c.Place(lx, g - 1, TileID.Lampposts);
            }
        }

        /// <summary>The landing at the stair's foot: the row of its last two columns.</summary>
        private const int StairFoot = -6;
    }
}
