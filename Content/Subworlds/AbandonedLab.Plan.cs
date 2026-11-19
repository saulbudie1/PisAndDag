using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Abandoned Lab laid out (AbandonedLab lays it down), the first of it, by request ("Starter lab": "a first layout:
    /// a few ruined corridors and rooms with vent shafts, dark and broken, plus the vent back out. Built on later"), and
    /// built on by the user's own floor plan ("new flooring plan": a drawing; "purple lines are 1 tile vents with no
    /// light"). What the programme kept under the Mile: the people who worked here left, and what was kept here got out.
    ///
    ///   the way in       a chamber, the vent back up standing on its floor; a stair of shelves up its east side to a
    ///                    duct into the first lab; the elevator down to the document room in its floor.
    ///   the corridor     six rows high, two hundred and thirteen long, from the way in to the containment hall. A
    ///                    shaft up from it into each lab; a hole in its floor down into a duct; a shaft up out of the store
    ///                    room.
    ///   the first lab    benches with what was left on them, a sink, a bookcase, a work bench, lamps.
    ///   the specimen     three tanks, one broken and dry; a bench; a canister.
    ///     lab
    ///   the ducts        crawlways three rows high, grated: between the two labs, and under the corridor to the store
    ///                    room.
    ///   the store room   barrels and lockers, shelves, canisters, a ladder of shelves up its shaft; east of it a
    ///                    corridor to the incineration pit's shaft.
    ///   the containment  the east end, ninety-one by fifty-four: four tanks in a row, two of them broken, either side of
    ///     hall           the great one in the middle, burst; catwalks at two heights; chains; red light; canisters.
    ///   the annex        west of the way in, along a corridor: three floors of whatever was kept and not wanted; no
    ///                    canisters ("js random stuff each floor but no more canister"). A corridor stairs down from its
    ///                    bottom floor to the document room.
    ///   the document     two floors under the way in, the elevator between them and it: shelves of records (LabDecor
    ///     room           Bookshelf, six by six, in pairs), cabinets, consoles.
    ///   the dining room  east along a corridor: a canteen below, a kitchen above.
    ///   the safe room    past a stretch of corridor whose floor has fallen in: bunks, lockers, supplies; a lamp over
    ///                    its door that the Cultures will not pass (CulturePath); a bunk to sleep in.
    ///   the incineration a pit under the store room's corridor, a fire at the bottom of it that takes whatever falls in.
    ///     pit
    ///   the vents        one tile across and unlit, through the rock: over the annex's corridor, from the way in up into
    ///                    the first lab, from the specimen lab over to the hall and down to the corridor, and from the
    ///                    corridor's floor into the store room. Too small for anyone but what lives here.
    ///
    /// Everything is in concrete (Gray Brick) and steel (Tin Plating), every open cell walled in registers as the house
    /// style has it, the ducts, shafts and vents papered with pipes; nothing open to the sky. Lit by long fluorescent
    /// lights on the walls (LabLight), and by the fire in the pit; no torch anywhere. No room passes for a house. Chests
    /// in every room, stocked with what a lab left.
    ///
    /// Drawn in its own frame (as first laid out) and laid that much east in the world (<see cref="Dx"/>): the annex lies
    /// west of where the world began. Written against a canvas (<see cref="ICanvas"/>), so that the same plan is laid in
    /// the game and previewed outside it; nothing here asks for the game but its numbers for tiles, walls and items.
    /// </summary>
    public static class LabPlan
    {
        public interface ICanvas
        {
            void Block(int x, int y, ushort type);
            void Clear(int x, int y);
            void Wall(int x, int y, ushort type);
            void Water(int x, int y);
            bool Solid(int x, int y);

            /// <summary>Nothing at all here: no block, and nothing that is not solid either (a light, a torch, a chain).</summary>
            bool Empty(int x, int y);

            /// <summary>A thing of several tiles (or one) with its top left here, by the game's own placing; true if it went.</summary>
            bool Furnish(ushort type, int style, int left, int top);

            /// <summary>A chest with its top left here, stocked; true if it went.</summary>
            bool Chest(ushort type, int style, int left, int top, List<(int item, int stack)> items);

            /// <summary>One tile of something placed as the game places it (a torch, a platform, a cobweb, a chain).</summary>
            void Place(ushort type, int style, int x, int y);

            /// <summary>The vent back up, its top left here.</summary>
            void Vent(int left, int top);

            /// <summary>A fluorescent light (LabLight: four wide, one tall, on the wall), its left tile here: working, failing or dead.</summary>
            bool Light(int left, int top, int style);

            /// <summary>A specimen canister (LabCanister: three wide, five tall, on the floor), its top left here, whole, a Culture asleep in it.</summary>
            bool Canister(int left, int top);

            /// <summary>One of the lab's own furnishings (<see cref="LabDecor"/>), its top left here (a block: its tile); true if it went.</summary>
            bool Decor(int kind, int style, int left, int top);
        }

        /// <summary>The lab's own furnishings, by kind: how wide and tall, and whether they hang on the wall (else stand on the floor); a block is one tile, solid.</summary>
        public static class LabDecor
        {
            public const int Bookshelf = 0, Monitor = 1, Console = 2, Terminal = 3, ServerRack = 4, FilingCabinet = 5, Pipe = 6, Valve = 7,
                Fan = 8, HazardSign = 9, CanteenTable = 10, VendingMachine = 11, Bunk = 12, Locker = 13, Counter = 14, Burner = 15,
                ElevatorPanel = 16, ElevatorRail = 17, UVLamp = 18, Count = 19;

            public static readonly (int Wide, int Tall, bool OnWall)[] Size =
            {
                (6, 6, false), (3, 2, true), (4, 3, false), (6, 3, true), (2, 4, false), (2, 3, false), (4, 2, true), (2, 2, true),
                (3, 3, true), (2, 2, true), (5, 2, false), (3, 4, false), (4, 3, false), (2, 4, false), (3, 2, false), (1, 1, false),
                (1, 2, true), (1, 1, true), (2, 1, true),
            };

            /// <summary>How many looks each has (styles, side by side on its sheet).</summary>
            public static readonly int[] Styles = { 3, 2, 2, 2, 2, 2, 2, 1, 1, 2, 1, 1, 1, 2, 2, 1, 1, 1, 1 };
        }

        /// <summary>The place's size, edges and all (Subworld: the edges).</summary>
        public const int Wide = 570, High = 290;

        /// <summary>How far east of its own frame the plan is laid in the world: room for the annex, west of it all.</summary>
        public const int Dx = 130;

        /// <summary>The vent back up: its top left tile, in the plan's frame and in the world's (AbandonedLab.Door).</summary>
        private const int PlanVentLeft = 56, PlanVentTop = 146;
        public const int VentLeft = PlanVentLeft + Dx, VentTop = PlanVentTop;

        // Wall and block kinds.
        private const ushort Concrete = TileID.GrayBrick, Slab = TileID.StoneSlab, Steel = TileID.TinPlating, Grating = TileID.Grate;
        private const ushort DadoWall = WallID.TinPlating, RailWall = WallID.IronBrick, FieldWall = WallID.GrayBrick, PatchWall = WallID.StoneSlab,
            RuinWall = WallID.RocksUnsafe1, PilasterWall = WallID.LeadBrick, PipeWall = WallID.CopperPipeWallpaper;

        // Furniture: the game's own styles, read from the items that place them (Item.SetDefaults).
        private const int SteampunkTable = 20, GlassTable = 15, SteampunkBench = 21, GlassBench = 25, SteampunkBookcase = 10,
            SteampunkLamp = 12, SteampunkLantern = 14, MartianLantern = 33, MetalSink = 14, MetalShelf = 9,
            SteampunkChest = 30, GlassChest = 47, MartianChest = 48, TrashCan = 6, Barrel = 5;

        /// <summary>A room's inside: its first and last column and row.</summary>
        private readonly struct Room
        {
            public readonly int X0, Y0, X1, Y1;

            public Room(int x0, int y0, int x1, int y1)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
            }

            public int Floor => Y1 + 1;
            public int Ceiling => Y0 - 1;
        }

        private static readonly Room Entry = new Room(48, 126, 86, 149);
        private static readonly Room Corridor = new Room(87, 144, 299, 149);
        private static readonly Room LabOne = new Room(100, 110, 140, 131);
        private static readonly Room LabTwo = new Room(170, 108, 216, 131);
        private static readonly Room Store = new Room(232, 158, 268, 177);
        private static readonly Room Hall = new Room(300, 96, 390, 149);

        /// <summary>The ducts: between the labs, from the way in to the first lab, and under the corridor to the store room.</summary>
        private static readonly Room UpperDuct = new Room(141, 113, 169, 115);
        private static readonly Room EntryDuct = new Room(87, 129, 99, 131);
        private static readonly Room LowerDuct = new Room(146, 160, 231, 162);

        /// <summary>The shafts: up into each lab, down from the corridor into the lower duct, up out of the store room.</summary>
        private static readonly Room ShaftOne = new Room(116, 132, 119, 143);
        private static readonly Room ShaftTwo = new Room(186, 132, 189, 143);
        private static readonly Room DropShaft = new Room(146, 150, 148, 159);
        private static readonly Room StoreShaft = new Room(262, 150, 265, 157);

        // ---- the new wing (the user's floor plan) ----

        /// <summary>The annex, three floors (floors of steel at rows 150 and 175, a hatch through each), and its corridor to the way in.</summary>
        private static readonly Room Annex = new Room(-82, 132, -35, 200);
        private static readonly Room AnnexTop = new Room(-82, 132, -35, 149);
        private static readonly Room AnnexMiddle = new Room(-82, 151, -35, 174);
        private static readonly Room AnnexBottom = new Room(-82, 176, -35, 200);
        private static readonly Room WestCorridor = new Room(-34, 144, 47, 149);

        /// <summary>The corridor that stairs down from the annex's bottom floor to the document room: seven high, falling 28 rows over its length.</summary>
        private const int SlopeX0 = -34, SlopeX1 = 35, SlopeTop0 = 194, SlopeTop1 = 222, SlopeHigh = 7;

        /// <summary>The document room, two floors (a floor of shelves at row 213 between them).</summary>
        private static readonly Room Documents = new Room(36, 196, 96, 228);
        private const int DocumentsMiddle = 213;

        /// <summary>The elevator's shaft, from the way in's floor down to the document room's middle floor.</summary>
        private const int ShaftLeft = 64, ShaftWide = 5, ShaftTopFloor = 150, ShaftBottomFloor = DocumentsMiddle;

        private static readonly Room LowerCorridor = new Room(97, 223, 158, 228);
        private static readonly Room Dining = new Room(159, 186, 192, 228);
        private const int DiningMiddle = 207;
        private static readonly Room GapCorridor = new Room(193, 223, 236, 228);

        /// <summary>Where the gap corridor's floor has fallen in: a hole seven wide and seven deep.</summary>
        private static readonly Room Gap = new Room(211, 229, 217, 235);

        private static readonly Room Safe = new Room(237, 194, 285, 228);
        private static readonly Room StoreCorridor = new Room(269, 172, 324, 177);
        private static readonly Room PitShaft = new Room(325, 172, 330, 216);
        private static readonly Room Pit = new Room(318, 217, 340, 238);

        /// <summary>The vents, one tile across: each a line of corners, followed a step at a time.</summary>
        private static readonly (int x, int y)[][] Vents =
        {
            new[] { (-40, 131), (-40, 125), (-37, 123), (30, 123), (35, 126), (35, 143) },
            new[] { (83, 125), (83, 116), (99, 116) },
            new[] { (217, 112), (269, 112) },
            new[] { (269, 90), (269, 143) },
            new[] { (269, 90), (314, 90), (314, 95) },
            new[] { (245, 150), (245, 157) },
        };

        // ---- where things are, in the world's frame (for the code that is not the plan) ----

        /// <summary>The safe room, its doorway with it: no Culture comes in (CulturePath).</summary>
        public static readonly (int X0, int Y0, int X1, int Y1) SafeRoom = (Safe.X0 - 1 + Dx, Safe.Y0, Safe.X1 + Dx, Safe.Y1);

        /// <summary>Where the pit's fire is: what is in it burns (LabFire).</summary>
        public static readonly (int X0, int Y0, int X1, int Y1) Fire = (Pit.X0 + Dx, Pit.Y1 - 7, Pit.X1 + Dx, Pit.Y1);

        /// <summary>The elevator (LabElevator): its shaft's first column and width, and the rows of its two floors (top, bottom).</summary>
        public const int ElevatorLeft = ShaftLeft + Dx, ElevatorWide = ShaftWide, ElevatorTop = ShaftTopFloor, ElevatorBottom = ShaftBottomFloor;

        /// <summary>
        /// Where the Cultures come from (CulturePath): two open tiles by two, this tile their bottom right; along the
        /// corridor, in each lab, the store room and the hall, and in the ducts between. None near the way in.
        /// </summary>
        private static readonly (int X, int Y)[] PlanNests =
        {
            (140, 147), (170, 147), (200, 147), (230, 147), (260, 147), (290, 147),
            (120, 125), (193, 120), (250, 170), (320, 140), (345, 120), (370, 140),
            (160, 161), (205, 161), (155, 114),
            (-60, 148), (-50, 173), (-66, 199), (0, 148), (52, 211), (75, 227), (125, 227), (168, 227), (175, 205),
            (200, 227), (300, 176),
        };

        /// <summary>The nests in the world's frame. None in the safe room.</summary>
        public static readonly (int X, int Y)[] Nests = Array.ConvertAll(PlanNests, n => (n.X + Dx, n.Y));

        private static Random rand;
        private static ICanvas c;

        public static void Lay(ICanvas canvas, int seed)
        {
            rand = new Random(seed ^ 0x1AB);

            // All of it concrete, then the rooms cut out of it (drawn in the plan's frame, laid east by Dx).
            for (int x = 0; x < Wide; x++)
                for (int y = 0; y < High; y++)
                    canvas.Block(x, y, Concrete);

            c = new Shifted(canvas);

            Room[] rooms = { Entry, Corridor, LabOne, LabTwo, Store, Hall, AnnexTop, AnnexMiddle, AnnexBottom, WestCorridor, Documents,
                LowerCorridor, Dining, GapCorridor, Safe, StoreCorridor, Pit };

            foreach (Room r in rooms)
                Carve(r, true);

            foreach (Room r in new[] { UpperDuct, EntryDuct, LowerDuct, ShaftOne, ShaftTwo, DropShaft, StoreShaft, PitShaft })
                Carve(r, false);

            Slope(true);
            ElevatorShaft(true);
            Carve(Gap, false);

            foreach (var line in Vents)
                VentLine(line);

            // Floors and ceilings: slab under the rooms, plating under the corridors and over everything; grating in the ducts.
            foreach (Room r in new[] { Entry, LabOne, LabTwo, Store, Hall, AnnexBottom, Documents, Dining, Safe })
                Course(r.X0, r.X1, r.Floor, Slab);

            foreach (Room r in new[] { Corridor, WestCorridor, LowerCorridor, GapCorridor, StoreCorridor })
                Course(r.X0, r.X1, r.Floor, Steel);

            foreach (Room r in rooms)
                Course(r.X0, r.X1, r.Ceiling, Steel);

            // The annex's floors between its floors; the dining room's.
            Course(Annex.X0, Annex.X1, AnnexTop.Floor, Steel);
            Course(Annex.X0, Annex.X1, AnnexMiddle.Floor, Steel);
            for (int x = Dining.X0; x <= Dining.X1; x++)
                c.Block(x, DiningMiddle, Steel);

            foreach (Room d in new[] { UpperDuct, EntryDuct, LowerDuct })
            {
                Course(d.X0, d.X1, d.Ceiling, Steel);

                for (int x = d.X0; x <= d.X1; x++)
                {
                    if (!c.Solid(x, d.Floor))
                        continue;

                    bool grate = (x / 6) % 3 != 0;
                    c.Block(x, d.Floor, grate ? Grating : Steel);

                    // A grate lets light through, and the whole lab is under the sky: a wall behind it, or daylight
                    // comes in by it (by request: "these tiles here are letting light inside").
                    if (grate)
                        c.Wall(x, d.Floor, PipeWall);
                }
            }

            // Walls: the rooms in registers, the ducts, shafts and vents papered with pipe.
            Registers(Entry, 2, 1, 8);
            Registers(LabOne, 2, 1, 8);
            Registers(LabTwo, 2, 1, 8);
            Registers(Store, 2, 1, 8);
            Registers(Hall, 3, 2, 10);
            Registers(Corridor, 2, 1, 8);
            Registers(AnnexTop, 2, 1, 8);
            Registers(AnnexMiddle, 2, 1, 8);
            Registers(AnnexBottom, 2, 1, 8);
            Registers(WestCorridor, 2, 1, 8);
            Registers(Documents, 2, 1, 8);
            Registers(LowerCorridor, 2, 1, 8);
            Registers(new Room(Dining.X0, Dining.Y0, Dining.X1, DiningMiddle - 1), 2, 1, 8);
            Registers(new Room(Dining.X0, DiningMiddle + 1, Dining.X1, Dining.Y1), 2, 1, 8);
            Registers(GapCorridor, 2, 1, 8);
            Registers(Safe, 2, 1, 8);
            Registers(StoreCorridor, 2, 1, 8);

            foreach (Room d in new[] { UpperDuct, EntryDuct, LowerDuct, ShaftOne, ShaftTwo, DropShaft, StoreShaft, PitShaft, Gap })
                Paper(d);

            Slope(false);
            ElevatorShaft(false);
            Scorched(Pit);

            // The floors and walls cut by the hatches and the stairs between floors.
            Hatches();

            Shafts();
            Furnish();
            Hall_();
            Wing();
            Tubes();
            Canisters();
            Webs();

            c.Vent(PlanVentLeft, PlanVentTop);
        }

        /// <summary>The plan's frame to the world's: everything Dx east.</summary>
        private sealed class Shifted : ICanvas
        {
            private readonly ICanvas world;

            public Shifted(ICanvas world) => this.world = world;

            public void Block(int x, int y, ushort type) => world.Block(x + Dx, y, type);
            public void Clear(int x, int y) => world.Clear(x + Dx, y);
            public void Wall(int x, int y, ushort type) => world.Wall(x + Dx, y, type);
            public void Water(int x, int y) => world.Water(x + Dx, y);
            public bool Solid(int x, int y) => world.Solid(x + Dx, y);
            public bool Empty(int x, int y) => world.Empty(x + Dx, y);
            public bool Furnish(ushort type, int style, int left, int top) => world.Furnish(type, style, left + Dx, top);
            public bool Chest(ushort type, int style, int left, int top, List<(int item, int stack)> items) => world.Chest(type, style, left + Dx, top, items);
            public void Place(ushort type, int style, int x, int y) => world.Place(type, style, x + Dx, y);
            public void Vent(int left, int top) => world.Vent(left + Dx, top);
            public bool Light(int left, int top, int style) => world.Light(left + Dx, top, style);
            public bool Canister(int left, int top) => world.Canister(left + Dx, top);
            public bool Decor(int kind, int style, int left, int top) => world.Decor(kind, style, left + Dx, top);
        }

        // ---- cutting ----------------------------------------------------------------------------------------------

        private static void Carve(Room r, bool clear)
        {
            for (int x = r.X0; x <= r.X1; x++)
                for (int y = r.Y0; y <= r.Y1; y++)
                    c.Clear(x, y);
        }

        private static void Course(int x0, int x1, int y, ushort type)
        {
            for (int x = x0; x <= x1; x++)
                if (c.Solid(x, y))
                    c.Block(x, y, type);
        }

        /// <summary>A room's back wall, floor up: a dado of plating, a rail of iron brick, the field (gray brick patched with slab and broken rock), a frieze of plating; lead brick pilasters every bay, a capital a tile wider under the frieze.</summary>
        private static void Registers(Room r, int dado, int frieze, int bay)
        {
            int high = r.Y1 - r.Y0 + 1;
            bool pilasters = high >= dado + 1 + frieze + 2;

            for (int x = r.X0; x <= r.X1; x++)
            {
                int off = (x - r.X0 - 1) % bay;
                bool pilaster = pilasters && (off == 0 || off == 1 || off == 2) && x >= r.X0 + 1 && x <= r.X1 - 1;
                bool capital = pilasters && (off == bay - 1 || off == 3) && x > r.X0 && x < r.X1;

                for (int y = r.Y0; y <= r.Y1; y++)
                {
                    int up = r.Y1 - y, down = y - r.Y0;
                    ushort wall;

                    if (up < dado || down < frieze)
                        wall = DadoWall;
                    else if (up == dado)
                        wall = RailWall;
                    else if (pilaster || capital && down == frieze)
                        wall = PilasterWall;
                    else
                    {
                        float n = Lump(x, y, 3);
                        wall = n > 0.82f ? RuinWall : n > 0.64f ? PatchWall : FieldWall;
                    }

                    c.Wall(x, y, wall);
                }
            }
        }

        private static void Paper(Room d)
        {
            for (int x = d.X0; x <= d.X1; x++)
                for (int y = d.Y0; y <= d.Y1; y++)
                    c.Wall(x, y, Lump(x, y, 9) > 0.8f ? RuinWall : PipeWall);
        }

        // ---- the new wing: its odd shapes -----------------------------------------------------------------------------

        /// <summary>The corridor down from the annex to the document room: its top row at this column.</summary>
        private static int SlopeTopAt(int x) => SlopeTop0 + (int)Math.Round((x - SlopeX0) * (SlopeTop1 - SlopeTop0) / (double)(SlopeX1 - SlopeX0));

        /// <summary>Cut (or, after, wall and floor) the corridor that stairs down from the annex to the document room.</summary>
        private static void Slope(bool carve)
        {
            for (int x = SlopeX0; x <= SlopeX1; x++)
            {
                int top = SlopeTopAt(x);

                for (int y = top; y < top + SlopeHigh; y++)
                {
                    if (carve)
                    {
                        c.Clear(x, y);
                        continue;
                    }

                    int up = top + SlopeHigh - 1 - y;
                    c.Wall(x, y, up < 2 ? DadoWall : up == 2 ? RailWall : y == top ? DadoWall : Lump(x, y, 3) > 0.8f ? RuinWall : FieldWall);
                }

                if (!carve)
                {
                    if (c.Solid(x, top + SlopeHigh))
                        c.Block(x, top + SlopeHigh, Steel);

                    if (c.Solid(x, top - 1))
                        c.Block(x, top - 1, Steel);
                }
            }
        }

        /// <summary>Cut (or, after, wall) the elevator's shaft: from the way in's floor down through the rock into the document room.</summary>
        private static void ElevatorShaft(bool carve)
        {
            for (int x = ShaftLeft; x < ShaftLeft + ShaftWide; x++)
            {
                for (int y = ShaftTopFloor; y < Documents.Y0; y++)
                {
                    if (carve)
                        c.Clear(x, y);
                    else
                        c.Wall(x, y, (x == ShaftLeft || x == ShaftLeft + ShaftWide - 1) ? RailWall : DadoWall);
                }
            }
        }

        /// <summary>A vent a tile across along these corners, a step at a time (never a step on the slant, so that it is one path), papered, unlit.</summary>
        private static void VentLine((int x, int y)[] corners)
        {
            for (int k = 0; k + 1 < corners.Length; k++)
            {
                var (x0, y0) = corners[k];
                var (x1, y1) = corners[k + 1];
                int x = x0, y = y0;

                Cut(x, y);

                while (x != x1 || y != y1)
                {
                    // Along whichever way is further behind the straight line between the corners.
                    long across = (long)Math.Abs(x - x0) * Math.Abs(y1 - y0), down = (long)Math.Abs(y - y0) * Math.Abs(x1 - x0);

                    if (x != x1 && (y == y1 || across <= down))
                        x += Math.Sign(x1 - x);
                    else
                        y += Math.Sign(y1 - y);

                    Cut(x, y);
                }
            }
        }

        private static void Cut(int x, int y)
        {
            c.Clear(x, y);
            c.Wall(x, y, PipeWall);
        }

        /// <summary>The pit's walls, burnt.</summary>
        private static void Scorched(Room r)
        {
            for (int x = r.X0; x <= r.X1; x++)
                for (int y = r.Y0; y <= r.Y1; y++)
                    c.Wall(x, y, Lump(x, y, 13) > 0.45f ? RuinWall : PatchWall);
        }

        /// <summary>
        /// The ways between floors: a hatch of shelves in the annex's two floors and the dining room's, and a stair of shelves
        /// up to each; the document room's middle floor, all shelves, and its stair.
        /// </summary>
        private static void Hatches()
        {
            Hatch(-46, AnnexTop.Floor, AnnexMiddle);
            Hatch(-80, AnnexMiddle.Floor, AnnexBottom);
            Hatch(186, DiningMiddle, new Room(Dining.X0, DiningMiddle + 1, Dining.X1, Dining.Y1));

            Shelf(Documents.X0, Documents.X1, DocumentsMiddle);
            Shelf(57, 62, 225);
            Shelf(64, 69, 221);
            Shelf(57, 62, 217);
        }

        /// <summary>A hatch six wide in a floor, its shelves where the floor was; and under it, up from the floor below, a stair of shelves four rows apart, this side and the next.</summary>
        private static void Hatch(int left, int floor, Room below)
        {
            for (int x = left; x < left + 6; x++)
            {
                c.Clear(x, floor);
                c.Wall(x, floor, DadoWall);
            }

            Shelf(left, left + 5, floor);

            int side = left + 6 <= below.X1 - 5 ? 6 : -6;

            for (int y = below.Floor - 4, k = 0; y > floor + 2; y -= 4, k++)
                Shelf(left + (k % 2 == 0 ? side : 0), left + (k % 2 == 0 ? side : 0) + 5, y);
        }

        // ---- getting about ------------------------------------------------------------------------------------------

        /// <summary>The shelves to climb by: up each lab's shaft, the stair up the way in to its duct, up to the duct between the labs, and the ladder out of the store room.</summary>
        private static void Shafts()
        {
            // Up each shaft from the corridor: a shelf over the corridor's floor, one at the corridor's ceiling, one between,
            // and one in the lab's floor.
            foreach (Room s in new[] { ShaftOne, ShaftTwo })
                foreach (int y in new[] { 145, 141, 137, 132 })
                    Shelf(s.X0, s.X1, y);

            // The way in: a stair of shelves up its east side to the duct (clear of the elevator's shaft).
            Shelf(71, 77, 145);
            Shelf(77, 83, 141);
            Shelf(80, 86, 136);
            Shelf(80, 86, 132);

            // Each lab: a stair up to the duct between them, on the side it opens from.
            Shelf(134, 140, 127);
            Shelf(128, 134, 122);
            Shelf(134, 140, 116);
            Shelf(170, 176, 127);
            Shelf(176, 182, 122);
            Shelf(170, 176, 116);

            // Out of the store room: a ladder of shelves up its shaft, and over the corridor's floor where it opens.
            for (int y = 150; y <= 174; y += 4)
                Shelf(StoreShaft.X0, StoreShaft.X1, y);

            // Down into the store room from its duct: a ledge, and a step.
            Shelf(232, 238, 163);
            Shelf(238, 243, 168);
        }

        private static void Shelf(int x0, int x1, int y)
        {
            for (int x = x0; x <= x1; x++)
                if (!c.Solid(x, y))
                    c.Place(TileID.Platforms, MetalShelf, x, y);
        }

        // ---- what was left ------------------------------------------------------------------------------------------

        private static void Furnish()
        {
            // The way in: a desk and its bottle, a locker (east of the elevator).
            Bench(72, Entry.Floor, SteampunkTable, 1);
            Locker(78, Entry.Floor, SteampunkChest);

            // The first lab: two benches with what was left on them, a sink, a bookcase of records, a work bench, lamps.
            Bench(103, LabOne.Floor, SteampunkTable, 2);
            Bench(108, LabOne.Floor, GlassTable, 3);
            c.Furnish(TileID.Sinks, MetalSink, 113, LabOne.Floor - 2);
            c.Furnish(TileID.Bookcases, SteampunkBookcase, 122, LabOne.Floor - 4);
            c.Furnish(TileID.WorkBenches, GlassBench, 126, LabOne.Floor - 1);
            c.Furnish(TileID.Lamps, SteampunkLamp, 102, LabOne.Floor - 3);
            c.Furnish(TileID.Lamps, SteampunkLamp, 131, LabOne.Floor - 3);
            Locker(128, LabOne.Floor, GlassChest);
            c.Furnish(TileID.HangingLanterns, SteampunkLantern, 112, LabOne.Y0);

            // The specimen lab: three tanks, the last broken and dry; a bench; a locker.
            Tank(192, LabTwo.Floor, 5, 8, false);
            Tank(199, LabTwo.Floor, 5, 8, false);
            Tank(206, LabTwo.Floor, 5, 8, true);
            Bench(178, LabTwo.Floor, SteampunkTable, 2);
            c.Furnish(TileID.WorkBenches, SteampunkBench, 183, LabTwo.Floor - 1);
            Locker(212, LabTwo.Floor, MartianChest);
            c.Furnish(TileID.HangingLanterns, MartianLantern, 199, LabTwo.Y0);

            // The store room: barrels and lockers along its floor, shelves on its walls with bottles on them.
            Locker(244, Store.Floor, Barrel);
            Locker(247, Store.Floor, TrashCan);
            Locker(250, Store.Floor, Barrel);
            Locker(255, Store.Floor, SteampunkChest);
            Locker(258, Store.Floor, TrashCan);

            for (int y = 166; y <= 172; y += 6)
            {
                Shelf(250, 258, y);

                for (int x = 251; x <= 257; x += 2)
                    if (rand.Next(3) > 0)
                        c.Furnish(TileID.Bottles, rand.Next(3), x, y - 1);
            }

            c.Furnish(TileID.HangingLanterns, SteampunkLantern, 248, Store.Y0);
        }

        /// <summary>A bench (a table) on the floor at this column, and on it so many bottles.</summary>
        private static void Bench(int x, int floor, int style, int bottles)
        {
            if (!c.Furnish(TileID.Tables, style, x, floor - 2))
                return;

            for (int k = 0; k < bottles; k++)
                c.Furnish(TileID.Bottles, rand.Next(3), x + k, floor - 3);
        }

        /// <summary>A chest of this kind on the floor at this column, stocked as a lab left it.</summary>
        private static void Locker(int x, int floor, int style) => c.Chest(TileID.Containers, style, x, floor - 2, Loot());

        /// <summary>
        /// A specimen tank on the floor at this column: a plinth of plating, glass sides, a cap of plating, and water in it;
        /// broken, its glass gone from the top down on one side and the other in stumps, and dry.
        /// </summary>
        private static void Tank(int x, int floor, int wide, int tall, bool broken)
        {
            int top = floor - tall;

            for (int k = x; k < x + wide; k++)
            {
                c.Block(k, floor - 1, Steel);
                c.Block(k, top, Steel);
            }

            for (int y = top + 1; y < floor - 1; y++)
            {
                bool leftStands = !broken || y > floor - 4;
                bool rightStands = !broken || y > top + 3 && rand.Next(4) > 0;

                if (leftStands)
                    c.Block(x, y, TileID.Glass);

                if (rightStands)
                    c.Block(x + wide - 1, y, TileID.Glass);

                if (!broken)
                    for (int k = x + 1; k < x + wide - 1; k++)
                        c.Water(k, y);
            }

            // A broken one's cap is gone but for its ends.
            if (broken)
                for (int k = x + 1; k < x + wide - 1; k++)
                    c.Clear(k, top);
        }

        // ---- the containment hall -----------------------------------------------------------------------------------

        private static void Hall_()
        {
            int floor = Hall.Floor;

            // Four tanks in a row, two either side of the great one; the outer two broken.
            Tank(305, floor, 7, 14, true);
            Tank(316, floor, 7, 14, false);
            Tank(368, floor, 7, 14, false);
            Tank(379, floor, 7, 14, true);

            // The great tank, burst: a plinth three high, stumps of glass, its cap hanging off chains over it, flesh on it.
            for (int x = 333; x <= 357; x++)
                for (int y = floor - 3; y < floor; y++)
                    c.Block(x, y, Steel);

            for (int y = floor - 4; y >= floor - 12; y--)
            {
                if (y > floor - 4 - rand.Next(7))
                    c.Block(335, y, TileID.Glass);

                if (y > floor - 4 - rand.Next(9))
                    c.Block(355, y, TileID.Glass);
            }

            for (int x = 336; x <= 354; x++)
            {
                // Flesh where it came out of it, thickest in the middle.
                int heap = Math.Max(0, 2 - Math.Abs(x - 345) / 4) + (rand.Next(3) == 0 ? 1 : 0);

                for (int y = floor - 4; y > floor - 4 - heap; y--)
                    c.Block(x, y, TileID.FleshBlock);
            }

            int cap = Hall.Y0 + 16;

            for (int x = 335; x <= 355; x++)
                if (x < 340 || x > 343 && x < 349 || x > 352)
                    c.Block(x, cap + (x > 345 ? 2 : 0), Steel);

            foreach (int x in new[] { 336, 346, 354 })
                for (int y = Hall.Y0; y < cap + (x > 345 ? 2 : 0); y++)
                    c.Place(TileID.Chain, 0, x, y);

            // A catwalk across at two heights, broken over the great tank; a stair of shelves up to each end of it.
            Shelf(Hall.X0, 330, 128);
            Shelf(360, Hall.X1, 128);
            Shelf(Hall.X0, 326, 112);
            Shelf(364, Hall.X1, 112);

            for (int k = 0; k < 4; k++)
            {
                Shelf(Hall.X0 + 26 - k * 0, Hall.X0 + 30, 145 - k * 4);
                Shelf(Hall.X1 - 30, Hall.X1 - 26, 145 - k * 4);
            }

            for (int k = 0; k < 4; k++)
            {
                Shelf(Hall.X0 + 2, Hall.X0 + 7, 124 - k * 4);
                Shelf(Hall.X1 - 7, Hall.X1 - 2, 124 - k * 4);
            }

            // Chains down from the ceiling here and there.
            foreach (int x in new[] { 309, 327, 331, 361, 367, 385 })
            {
                int length = 5 + rand.Next(12);

                for (int y = Hall.Y0; y < Hall.Y0 + length; y++)
                    if (!c.Solid(x, y))
                        c.Place(TileID.Chain, 0, x, y);
            }

            // A locker at each end of the hall.
            Locker(Hall.X0 + 2, floor, MartianChest);
            Locker(Hall.X1 - 3, floor, SteampunkChest);
        }

        // ---- the new wing: what is in it ---------------------------------------------------------------------------------

        private static bool Put(int kind, int style, int left, int top) => c.Decor(kind, style, left, top);

        /// <summary>What a floor of the annex might have on it, standing; and on its walls.</summary>
        private static readonly int[] FloorStuff = { LabDecor.ServerRack, LabDecor.FilingCabinet, LabDecor.Console, LabDecor.Locker, LabDecor.Counter, LabDecor.Bookshelf };
        private static readonly int[] WallStuff = { LabDecor.Monitor, LabDecor.Pipe, LabDecor.Fan, LabDecor.HazardSign, LabDecor.Valve, LabDecor.Terminal };

        /// <summary>
        /// Whatever was kept and not wanted, along a floor and on its wall (by request: "js random stuff each floor but no
        /// more canister"): racks, cabinets, consoles, lockers, counters, shelves, a desk or two with bottles, a chest; and a
        /// band of screens, pipes, fans and signs over them.
        /// </summary>
        private static void Scatter(Room r)
        {
            bool chest = false;

            for (int x = r.X0 + 2; x < r.X1 - 2;)
            {
                int roll = rand.Next(10);

                if (!chest && roll < 2)
                {
                    chest = c.Chest(TileID.Containers, rand.Next(2) == 0 ? SteampunkChest : MartianChest, x, r.Floor - 2, Loot());
                    x += 3 + rand.Next(3);
                    continue;
                }

                if (roll < 4)
                {
                    Bench(x, r.Floor, rand.Next(2) == 0 ? SteampunkTable : GlassTable, rand.Next(3));
                    x += 4 + rand.Next(3);
                    continue;
                }

                int kind = FloorStuff[rand.Next(FloorStuff.Length)];
                var size = LabDecor.Size[kind];

                if (x + size.Wide > r.X1 - 1)
                {
                    x++;
                    continue;
                }

                Put(kind, rand.Next(LabDecor.Styles[kind]), x, r.Floor - size.Tall);
                x += size.Wide + 1 + rand.Next(4);
            }

            for (int x = r.X0 + 2 + rand.Next(4); x < r.X1 - 6;)
            {
                int kind = WallStuff[rand.Next(WallStuff.Length)];
                var size = LabDecor.Size[kind];

                Put(kind, rand.Next(LabDecor.Styles[kind]), x, r.Floor - 7 - size.Tall + 1);
                x += size.Wide + 3 + rand.Next(8);
            }
        }

        /// <summary>A run of pipe along a wall at this row, a valve now and then.</summary>
        private static void PipeRun(int x0, int x1, int top)
        {
            for (int x = x0; x + 3 <= x1; x += 4)
            {
                if (rand.Next(6) == 0 && Put(LabDecor.Valve, 0, x, top))
                    continue;

                Put(LabDecor.Pipe, rand.Next(2), x, top);
            }
        }

        private static void Wing()
        {
            // The annex: whatever was kept, each floor.
            foreach (Room r in new[] { AnnexTop, AnnexMiddle, AnnexBottom })
                Scatter(r);

            // Its corridor to the way in: pipe along it, a fan, a sign at the annex end.
            PipeRun(WestCorridor.X0 + 2, WestCorridor.X1 - 2, WestCorridor.Y0 + 1);
            Put(LabDecor.HazardSign, 0, WestCorridor.X0 + 1, WestCorridor.Y0 + 3);

            // The elevator: a panel at either floor, beside the shaft; its rails down the shaft.
            Put(LabDecor.ElevatorPanel, 0, ShaftLeft - 2, ShaftTopFloor - 4);
            Put(LabDecor.ElevatorPanel, 0, ShaftLeft - 2, ShaftBottomFloor - 4);

            for (int y = ShaftTopFloor - 10; y < ShaftBottomFloor; y++)
            {
                Put(LabDecor.ElevatorRail, 0, ShaftLeft, y);
                Put(LabDecor.ElevatorRail, 0, ShaftLeft + ShaftWide - 1, y);
            }

            // The document room: shelves of records in pairs at either end of each floor (by request: "in document room the
            // blues are the book shelves; create our own 6 tall and 6 wide sheves and place next to each other"); ledges of
            // books over the upper ones; cabinets, consoles and a screen between; a chest.
            foreach (int floor in new[] { DocumentsMiddle, Documents.Floor })
                foreach (int x in new[] { 38, 44, 83, 89 })
                    Put(LabDecor.Bookshelf, rand.Next(LabDecor.Styles[LabDecor.Bookshelf]), x, floor - 6);

            foreach (var (x0, x1) in new[] { (37, 49), (83, 95) })
            {
                Shelf(x0, x1, 202);

                for (int x = x0; x <= x1; x++)
                    if (rand.Next(3) > 0)
                        c.Furnish(TileID.Books, rand.Next(6), x, 201);
            }

            Put(LabDecor.FilingCabinet, 0, 51, DocumentsMiddle - 3);
            Put(LabDecor.FilingCabinet, 1, 54, DocumentsMiddle - 3);
            Put(LabDecor.Console, 0, 72, DocumentsMiddle - 3);
            Put(LabDecor.FilingCabinet, 1, 78, DocumentsMiddle - 3);
            Put(LabDecor.Terminal, 0, 71, 198);
            Put(LabDecor.Monitor, 0, 52, 198);
            Put(LabDecor.FilingCabinet, 1, 51, Documents.Floor - 3);
            Put(LabDecor.Console, 1, 72, Documents.Floor - 3);
            Put(LabDecor.FilingCabinet, 0, 78, Documents.Floor - 3);
            Locker(53, Documents.Floor, SteampunkChest);
            Put(LabDecor.Fan, 0, 40, 216);

            // Its corridor east: pipe, a sign, a fan.
            PipeRun(LowerCorridor.X0 + 2, LowerCorridor.X1 - 2, LowerCorridor.Y0 + 1);
            Put(LabDecor.Fan, 0, 128, LowerCorridor.Y0 + 3);

            // The dining room: the canteen below (a vending machine, three tables, a bin), the kitchen above (counters, a
            // sink, a locker, a pantry chest, a vending machine), screens and a fan.
            int canteen = Dining.Floor, kitchen = DiningMiddle;

            Put(LabDecor.VendingMachine, 0, 160, canteen - 4);
            Put(LabDecor.CanteenTable, 0, 165, canteen - 2);
            Put(LabDecor.CanteenTable, 0, 171, canteen - 2);
            Locker(177, canteen, TrashCan);
            Put(LabDecor.Monitor, 1, 168, canteen - 9);

            foreach (int x in new[] { 160, 163, 166 })
                Put(LabDecor.Counter, rand.Next(2), x, kitchen - 2);

            c.Furnish(TileID.Sinks, MetalSink, 170, kitchen - 2);
            Put(LabDecor.Locker, 0, 173, kitchen - 4);
            Locker(176, kitchen, SteampunkChest);
            Put(LabDecor.VendingMachine, 0, 180, kitchen - 4);
            Put(LabDecor.Fan, 0, 165, kitchen - 10);

            // The broken stretch: a sign either side of the gap.
            Put(LabDecor.HazardSign, 1, Gap.X0 - 4, GapCorridor.Y0 + 1);
            Put(LabDecor.HazardSign, 1, Gap.X1 + 3, GapCorridor.Y0 + 1);
            PipeRun(GapCorridor.X0 + 2, Gap.X0 - 6, GapCorridor.Y0 + 3);

            // The safe room: the lamp over its door they will not pass; bunks, lockers, supplies, a desk with its screen and
            // console; a fan.
            Put(LabDecor.UVLamp, 0, Safe.X0, GapCorridor.Y0 - 2);

            foreach (int x in new[] { 250, 255, 260 })
                Put(LabDecor.Bunk, 0, x, Safe.Floor - 3);

            foreach (int x in new[] { 266, 269, 272 })
                Put(LabDecor.Locker, rand.Next(2), x, Safe.Floor - 4);

            Locker(276, Safe.Floor, MartianChest);
            Locker(279, Safe.Floor, SteampunkChest);
            Bench(242, Safe.Floor, SteampunkTable, 2);
            Put(LabDecor.Console, 0, 281, Safe.Floor - 3);
            Put(LabDecor.Monitor, 0, 242, Safe.Floor - 8);
            Put(LabDecor.Fan, 0, 262, Safe.Y0 + 4);
            Put(LabDecor.ServerRack, 0, 246, Safe.Floor - 4);

            // A gallery across its upper half, a stair of shelves up to it at the far end; supplies kept up there.
            Shelf(Safe.X0 + 1, 275, 211);
            Shelf(276, 281, 225);
            Shelf(270, 275, 221);
            Shelf(276, 281, 217);
            Shelf(270, 275, 213);
            Put(LabDecor.Locker, 0, 240, 207);
            Put(LabDecor.Locker, 1, 243, 207);
            Put(LabDecor.FilingCabinet, 0, 247, 208);
            Locker(251, 211, SteampunkChest);
            Put(LabDecor.ServerRack, 1, 255, 207);
            Put(LabDecor.Counter, 0, 259, 209);
            Locker(264, 211, MartianChest);

            // The store room's corridor to the pit: pipe, a fan, a sign at the shaft.
            PipeRun(StoreCorridor.X0 + 2, StoreCorridor.X1 - 10, StoreCorridor.Y0 + 1);
            Put(LabDecor.HazardSign, 0, StoreCorridor.X1 - 5, StoreCorridor.Y0 + 1);

            // The pit: its floor all burners.
            for (int x = Pit.X0; x <= Pit.X1; x++)
                Put(LabDecor.Burner, 0, x, Pit.Floor);
        }

        // ---- light, and webs ------------------------------------------------------------------------------------------

        /// <summary>
        /// The fluorescent lights, hung high on the walls of every room and along the corridor under its ceiling: most of
        /// them still working, some failing, some dead (LabLight). By request: "to decorate the lab". They, the lanterns and
        /// the lamps are all the light there is: no torch anywhere (by request: "dont use torch in this lab").
        /// </summary>
        private static void Tubes()
        {
            const int Working = 0, Failing = 1, Dead = 2, RedWorking = 3, RedFailing = 4, WhiteWorking = 5, WhiteFailing = 6;

            // The way in: one working over the vent, one failing further in.
            Tube(52, Entry.Y0 + 3, Working);
            Tube(66, Entry.Y0 + 3, Failing);

            // The corridor, under its ceiling between the red lights: working, then whatever is left.
            int[] corridor = { Working, Failing, Working, Dead, Working, Working, Failing, Dead, Working, Working, Dead, Failing, Working };

            for (int k = 0, x = Corridor.X0 + 6; x + 3 < Corridor.X1 && k < corridor.Length; k++, x += 16)
                Tube(x, Corridor.Y0, corridor[k]);

            // The labs: three in the first, two in the specimen lab (one failing over the tanks).
            Tube(103, LabOne.Y0 + 2, Working);
            Tube(116, LabOne.Y0 + 2, Working);
            Tube(128, LabOne.Y0 + 2, Dead);
            Tube(178, LabTwo.Y0 + 2, Working);
            Tube(198, LabTwo.Y0 + 3, Failing);

            // The store room: one failing, and one working over the canisters.
            Tube(250, Store.Y0 + 2, Failing);
            Tube(236, Store.Y0 + 3, Working);

            // The hall: along the top of it, more of them dead than not; and two over the catwalks. Red (by request: "this
            // room can use red fluorescent light"). (Clear of the chains that hang from the ceiling.)
            (int left, int style)[] hall = { (301, Dead), (312, RedWorking), (320, RedFailing), (338, Dead), (348, Dead), (356, RedFailing), (370, RedWorking), (378, Dead) };

            foreach (var (left, style) in hall)
                Tube(left, Hall.Y0 + 2, style);

            Tube(Hall.X0 + 12, 124, RedWorking);
            Tube(Hall.X1 - 15, 124, RedFailing);

            // And on its walls where its red torches were (by request: "dont use torch in this lab"): two high, two low over
            // the canisters by the great tank.
            Tube(318, 104, RedWorking);
            Tube(369, 104, RedFailing);
            Tube(325, 138, RedWorking);
            Tube(362, 138, RedWorking);

            // The new wing (none in the vents, the shaft or the pit: the vents are unlit, by request; the pit has its fire).
            foreach (Room r in new[] { AnnexTop, AnnexMiddle, AnnexBottom })
            {
                Tube(r.X0 + 6, r.Y0 + 2, Working);
                Tube(r.X1 - 12, r.Y0 + 2, rand.Next(3) == 0 ? Failing : rand.Next(3) == 0 ? Dead : Working);
            }

            foreach (Room r in new[] { WestCorridor, LowerCorridor, StoreCorridor })
                for (int x = r.X0 + 6; x + 3 < r.X1; x += 16)
                    Tube(x, r.Y0, rand.Next(4) == 0 ? Failing : rand.Next(5) == 0 ? Dead : Working);

            // The document room's are white (by request: "white light in document room").
            Tube(42, Documents.Y0 + 2, WhiteWorking);
            Tube(86, Documents.Y0 + 2, WhiteFailing);
            Tube(46, DocumentsMiddle + 1, WhiteWorking);
            Tube(80, DocumentsMiddle + 1, Dead);
            Tube(163, Dining.Y0 + 2, Working);
            Tube(176, Dining.Y0 + 2, Working);
            Tube(163, DiningMiddle + 2, Working);
            Tube(176, DiningMiddle + 2, Failing);
            Tube(198, GapCorridor.Y0, Failing);
            Tube(226, GapCorridor.Y0, Dead);

            foreach (int x in new[] { 244, 258, 272 })
                Tube(x, Safe.Y0 + 2, Working);
        }

        /// <summary>
        /// The specimen canisters (LabCanister), a Culture asleep in each (by request: "add a 5tall 3wide decorative walk
        /// thru tile that is basically js a canister with this thing inside; breaking it with pickaxe opens it"): one at
        /// the far end of the specimen lab, past its tanks; four in the hall, between its tanks and either side of the great
        /// one; and two in the store room, kept against the wall.
        /// </summary>
        private static void Canisters()
        {
            c.Canister(214, LabTwo.Floor - 5);

            foreach (int x in new[] { 312, 323, 365, 375 })
                c.Canister(x, Hall.Floor - 5);

            foreach (int x in new[] { 234, 238 })
                c.Canister(x, Store.Floor - 5);
        }

        /// <summary>A fluorescent light, its left tile here, if its four tiles are free of anything already put there.</summary>
        private static void Tube(int left, int top, int style)
        {
            for (int x = left; x < left + 4; x++)
                if (c.Solid(x, top))
                    return;

            c.Light(left, top, style);
        }

        /// <summary>Cobwebs in the corners of every room and along the ducts.</summary>
        private static void Webs()
        {
            foreach (Room r in new[] { Entry, Corridor, LabOne, LabTwo, Store, Hall, UpperDuct, LowerDuct, EntryDuct, AnnexTop, AnnexMiddle,
                AnnexBottom, WestCorridor, Documents, LowerCorridor, Dining, GapCorridor, StoreCorridor })
            {
                for (int x = r.X0; x <= r.X1; x++)
                {
                    for (int y = r.Y0; y <= r.Y1; y++)
                    {
                        // Only where there is nothing: a web put over one tile of a light would break it. Never in the
                        // elevator's shaft, where the car would carry its riders through it.
                        if (!c.Empty(x, y) || x >= ShaftLeft - 1 && x <= ShaftLeft + ShaftWide)
                            continue;

                        bool corner = (x - r.X0 < 3 || r.X1 - x < 3) && y - r.Y0 < 3;
                        float n = Lump(x, y, 21);

                        if (corner && n > 0.35f || y == r.Y0 && n > 0.78f)
                            c.Place(TileID.Cobweb, 0, x, y);
                    }
                }
            }
        }

        // ---- loot --------------------------------------------------------------------------------------------------

        /// <summary>One line of the lab's loot: an item, how many at least and at most, how likely against the rest.</summary>
        private static readonly (int item, int min, int max, int weight)[] Table =
        {
            (ItemID.Bottle, 2, 6, 12), (ItemID.Lens, 1, 3, 8), (ItemID.Glowstick, 3, 10, 10), (ItemID.HealingPotion, 1, 3, 7),
            (ItemID.LesserRestorationPotion, 1, 3, 6), (ItemID.RegenerationPotion, 1, 2, 5), (ItemID.IronskinPotion, 1, 2, 5),
            (ItemID.Bomb, 2, 6, 6), (ItemID.Rope, 10, 30, 8), (ItemID.IronBar, 3, 8, 6), (ItemID.LeadBar, 3, 8, 6),
            (ItemID.SilverBar, 2, 5, 4), (ItemID.SilverCoin, 5, 30, 10), (ItemID.GoldCoin, 1, 2, 2), (ItemID.Cobweb, 5, 20, 6),
            (ItemID.Bone, 2, 8, 6), (ItemID.Vertebrae, 1, 4, 5), (ItemID.Gel, 3, 10, 6), (ItemID.Book, 1, 3, 5),
            (ItemID.AdhesiveBandage, 1, 1, 1), (ItemID.Bezoar, 1, 1, 1),
        };

        /// <summary>Three to six of the lines, none twice, each its own amount.</summary>
        private static List<(int item, int stack)> Loot()
        {
            var items = new List<(int, int)>();
            var taken = new bool[Table.Length];
            int slots = 3 + rand.Next(4);
            int total = 0;

            foreach (var line in Table)
                total += line.weight;

            for (int s = 0; s < slots; s++)
            {
                int roll = rand.Next(total);

                for (int i = 0; i < Table.Length; i++)
                {
                    roll -= Table[i].weight;

                    if (roll >= 0)
                        continue;

                    if (!taken[i])
                    {
                        taken[i] = true;
                        items.Add((Table[i].item, Table[i].min + rand.Next(Table[i].max - Table[i].min + 1)));
                    }

                    break;
                }
            }

            return items;
        }

        // ---- noise ---------------------------------------------------------------------------------------------------

        /// <summary>Smooth lumps from 0 to 1, four tiles across, the same for the same cell.</summary>
        private static float Lump(int x, int y, int salt)
        {
            const int cell = 4;
            int gx = x / cell, gy = y / cell;
            float fx = (x % cell) / (float)cell, fy = (y % cell) / (float)cell;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(gx, gy, salt), b = Hash(gx + 1, gy, salt), d = Hash(gx, gy + 1, salt), e = Hash(gx + 1, gy + 1, salt);
            float top = a + (b - a) * fx, bottom = d + (e - d) * fx;
            return top + (bottom - top) * fy;
        }

        private static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
