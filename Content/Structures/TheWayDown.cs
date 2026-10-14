using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The way down to the second evil (TheBallPass), by request: "a small jagged cave that
    /// follows from the surface to underground, do not obstruct anything, dont go to the ocean,
    /// prob before the dungeon. withh torches here and there, at surfafce, there is dirt wall,
    /// going down, no wall or sparse rock wall, and nearer to the ball is evil wall"; and "the door
    /// to the hallway cannot be broken, and it is generated on one of the path down there to the
    /// ball (make it so there is a flat way when ur tranversing down, somewhat like pyramid pharaoh
    /// hall, place the door there".
    ///
    /// WHERE IT OPENS. On the ground between the beach and the Dungeon (the ball lies under the
    /// beach, two thirds of the way down to the Underworld): inland of where the beach's sand ends
    /// by <see cref="BeachKeep"/>, short of anything of the Dungeon's by <see cref="DungeonKeep"/>,
    /// and off the graveyard's ground (GraveyardZone) by <see cref="GraveKeep"/>. Where the ground
    /// can be cut and nothing stands near enough to be undermined; the places nearest the
    /// Dungeon first ("prob before the dungeon"), with some chance in it.
    ///
    /// WHAT IT IS. A narrow cave, cut two to three tiles either side of the line it follows and
    /// bulging out in lumps here and there, its edges ragged to the tile, winding down the rock as
    /// a stair does: runs one way and then the other, at forty-five degrees and at the two slopes
    /// either side of that, never straight down, up only to get over something, and never turning
    /// back before <see cref="RunLeast"/> steps the same way. Partway down (about two fifths of the
    /// way, or as near that as the rock allows) it comes out level, into a hall: a flat way
    /// through the rock, built, the length of a pyramid's hall (by request, "somewhat like pyramid
    /// pharaoh hall"), with the Unmarked Door to the Empty Hall standing in the middle of it
    /// (SubworldDoor: nothing breaks it). Then down again, to the top of the shaft that is the one
    /// way into the ball.
    ///
    /// IT CAN BE WALKED DOWN. The line is found so that the floor under it, as it will be once cut
    /// (the cut's own, or a cave's it goes through), never falls away from one tile to the next by
    /// more than <see cref="DropMost"/> nor rises by more than <see cref="ClimbMost"/>. Once cut, it
    /// is walked, as by a player two tiles wide and three tall (<see cref="Walk"/>: steps of a
    /// tile, jumps of <see cref="WalkJump"/>, drops of <see cref="WalkFall"/>, which the game does
    /// not hurt for: it hurts a fall of more than twenty-five), from the ground at the mouth down
    /// to the top of the shaft. Where one would be stopped it is cut wider (<see cref="Mend"/>);
    /// and if even so it cannot be walked down, everything it touched is put back as it was and
    /// the next mouth and hall are tried. The last drop, into the shaft, is the ball's own.
    ///
    /// WHAT IT LEAVES ALONE. It is planned whole before a tile is touched, on a map of what it may
    /// not come near: water, lava, honey and shimmer by <see cref="LiquidKeep"/> (so nothing drains
    /// into it); anything built, grown or worth keeping (furniture, chests, crystals, altars,
    /// statues, traps, wiring, brick, wood) by <see cref="BuiltKeep"/>; a pot, a pile, a
    /// stalactite, a gem or a tree, and what it stands on, hangs from or clings to, not at all; any
    /// wall nobody would call the rock's own by <see cref="WallKeep"/>; the Dungeon by
    /// <see cref="DungeonKeep"/>; the graveyard's ground; the ball itself; and, short of the cavern
    /// layer, everything seaward of where its mouth may be (so it never comes up toward the ocean).
    /// The line it follows keeps <see cref="LineClear"/> further off all of it (from the loose
    /// things only <see cref="LooseClear"/>, so the cave goes round them as a cave does); a
    /// minecart track it may pass, leaving it be. As it cuts, it takes only the ground's own tiles
    /// (dirt, stone, sand, clay, mud, ore, gems, moss, the evils' stone) and the soft things that
    /// grow in it (cobwebs, plants, vines), whatever the map said, and nothing next to water. Sand,
    /// silt or slush it leaves over nothing is made fast (as the game's own Final Cleanup makes it),
    /// so none of it falls in. If no way can be found, nothing is cut, and the log says why.
    ///
    /// ITS WALLS. Behind what it cuts: dirt down to the bottom of the surface layer, by request;
    /// below that none, but for a patch of rock wall here and there (about one tile in five, in
    /// lumps); and within <see cref="EvilReach"/> of the ball's outside, the evil's own (the ball's).
    /// Where it passes through a cave that was already there, that cave is left as it was.
    ///
    /// ITS TORCHES. One every <see cref="TorchGapLeast"/> to <see cref="TorchGapMost"/> tiles along
    /// it, on its floor: a torch, and within the evil's walls the evil's torch, as the ring's.
    ///
    /// ROUND ABOUT. Where the Dungeon lies between (its halls spread out over the ball), so that no
    /// way can be found as above, it goes round, by request: "Why not create a path around it, i
    /// dont mind it going down then to the side tho, maybe even up a little. jaggedy js dont
    /// obstruct the dungeon and goes into the hole of the ball" (<see cref="RoundAbout"/>). It is
    /// found again on a map that keeps it <see cref="RoundKeep"/> off anything of the Dungeon's (not
    /// <see cref="DungeonKeep"/>) and reaches round it; this time as one walking it would go
    /// (<see cref="Burrow"/>): walking, stepping up and down, dropping, jumping up and over a hole,
    /// and digging the ground's own tiles where they are in the way, from the ground at the mouth
    /// to the hall, and from the hall wherever it must go, down, along and up again, into the top
    /// of the shaft. Cut so (as wide as one walking it), made wider, ragged, but never under where
    /// it is stood on, and walked; where one walking it is stopped, found again on the world as it
    /// then is, never taking what they stood on. The rest is as above: the mouths and their window,
    /// the hall and its door, the walls, the torches, and everything it keeps away from.
    ///
    /// INTO THE HOLLOW FROM THE SIDE. Where the Dungeon is round the top of the shaft too (in Flacid
    /// its lowest halls lie over it and beside it), so that no way round about can come to it, the
    /// way round about goes into the ball's hollow from whatever side is nearest instead, by request:
    /// "the access to the inner circle of the ball dont have to be from top down, it can be from any
    /// angle" (<see cref="RoundAbout"/>). It comes down and round as above and through the ball's
    /// shell, dug as any ground is, never within <see cref="HollowKeep"/> of the hollow over its bank,
    /// and comes out low, where one can stand on the bank: walked into, not fallen into. What the
    /// Dungeon left in the hollow when the ball was made round it, its walls, is gone round, never
    /// cut. The top of the shaft is still tried first.
    ///
    /// Run as late as the Grace side's late passes, just before Final Cleanup, so that nothing made
    /// after it is put in its way; and by /struct update into a world made before it existed
    /// (StructureUpdate), where the shaft's top is found again off the tiles.
    /// </summary>
    public class TheWayDownPass : GenPass
    {
        // ---- where its mouth may be ----------------------------------------------------------

        /// <summary>How far inland of where the beach's sand ends its mouth keeps, and short of the cavern layer, all of it.</summary>
        public const int BeachKeep = 50;

        /// <summary>How far from anything of the Dungeon's (its brick, its walls) all of it keeps.</summary>
        public const int DungeonKeep = 40;

        /// <summary>How far off the graveyard's ground (GraveyardZone) all of it keeps.</summary>
        public const int GraveKeep = 30;

        /// <summary>How many mouths are tried at the most, and how far apart; and how many halls under each.</summary>
        private const int MouthsTried = 6;
        private const int MouthsApart = 24;
        private const int HallsTried = 4;

        /// <summary>A mouth on ground this steep, or steeper, over this many columns either side, is passed over.</summary>
        private const int MouthLook = 4;
        private const int MouthSteepest = 6;

        /// <summary>
        /// Air under a stretch of solid ground at least this deep, high in the sky (over this share
        /// of the depth of the surface layer), means the stretch is an island. Lower down it is the
        /// ground, whatever is under it: there are caves under the beach.
        /// </summary>
        private const int IslandGap = 30;
        private const double SkyShare = 0.6;

        // ---- what it keeps away from -----------------------------------------------------------

        public const int LiquidKeep = 2;
        public const int BuiltKeep = 3;
        public const int LooseKeep = 0;
        public const int WallKeep = 2;

        // ---- round about ------------------------------------------------------------------------

        /// <summary>How far from anything of the Dungeon's (its brick, its walls) a way round about keeps: so that it never comes into it, nor opens into it.</summary>
        public const int RoundKeep = 10;

        /// <summary>How far round the Dungeon, the mouths' window and the shaft the map of a way round about reaches; and how far either side of the mouths' window and the shaft the Dungeon is looked for.</summary>
        private const int RoundMargin = 240;
        private const int RoundLook = 1200;

        /// <summary>Round about, how far across one coming down jumps at the most (past three, only from a jump of two or more); and how many times at the most the way is found again from where one walking it was stopped.</summary>
        private const int RoundAcross = 5;
        private const int RoundMends = 12;

        /// <summary>
        /// Into the ball's hollow from the side (<see cref="RoundAbout"/>): how far from its middle
        /// the hollow may reach (Hollow * (1 + HollowRough), 214, three of grain, and six more); the
        /// ball's shell is all between this and its outside. And between how far under its middle one
        /// may come to stand in it: on the bank, whose top is at least BaseFrom - BaseRough under the
        /// middle and at most BaseFrom + BaseRough (two of grain either way; one standing on it is a
        /// row over it; and six more under).
        /// </summary>
        private const int HollowReach = TheBallPass.Hollow + (int)(TheBallPass.Hollow * TheBallPass.HollowRough) + 9;
        private const int BankHigh = TheBallPass.BaseFrom - TheBallPass.BaseRough - 3;
        private const int BankLow = TheBallPass.BaseFrom + TheBallPass.BaseRough + 8;

        /// <summary>Into the hollow from the side: how far off the hollow over where one may stand in it the way keeps, so that it never cuts the hollow's side open over the bank.</summary>
        private const int HollowKeep = 2;

        /// <summary>
        /// Round about, how many places with room for the body joined to where a way must end are
        /// counted at the most (<see cref="Burrow.Joined"/>): fewer, and where none of them is joined
        /// to a hall's far end, no way is looked for from there.
        /// </summary>
        private const int JoinedMost = 1_500_000;

        // ---- its size and its winding ------------------------------------------------------------

        /// <summary>
        /// How far the cut reaches either side of the line it follows, the least and the most; and
        /// how much further it bulges out in lumps (smooth noise a few tiles across, rolled anew
        /// for each world), so that it is a cave, wider here and narrower there, and not a bore.
        /// </summary>
        private const float RadiusLeast = 2.0f;
        private const float RadiusMost = 2.9f;
        private const float BulgeMost = 1.6f;

        /// <summary>And how much in and out it goes, either way, in smaller lumps (three tiles across): ragged, not smooth.</summary>
        private const float Rough = 1.2f;

        /// <summary>
        /// How far the way found keeps from anything it may not come near (with that thing's own
        /// distance already round it): the cut's least reach and a tile, so that wherever it is
        /// squeezed it is still open a tile or two either side of its line. Further out, the cut
        /// stops short of anything of the kind, so the way is narrower there. From a pot, a pile, a
        /// stalactite or a gem (and what it stands on, hangs from or clings to) only
        /// <see cref="LooseClear"/>: the cut goes round one and leaves it where it was, as a cave does;
        /// the deep caves are thick with them, and kept off as far as the rest they would leave no way
        /// through.
        /// </summary>
        private const int LineClear = 3;
        private const int LooseClear = 1;

        /// <summary>How far the way down keeps from the hall's brick before it comes level to its openings.</summary>
        private const int HallKeep = 5;

        /// <summary>The grid the way is found on, in tiles.</summary>
        private const int Cell = 2;

        /// <summary>How far either side of the straight line between its ends it may wander, the least and the most (it changes as it goes down).</summary>
        private const int BandLeast = 14;
        private const int BandMost = 30;

        /// <summary>What turning back the other way costs, in tiles of way: so it runs, and does not wriggle.</summary>
        private const float TurnCost = 4f;

        // ---- the hall ----------------------------------------------------------------------------

        /// <summary>The hall inside, wide and tall: a pyramid's hall is 23 to 27 wide and 8 to 13 tall (WorldGen.Pyramid).</summary>
        public const int HallWide = 26;
        public const int HallTall = 8;

        /// <summary>The brick round it; the way out of either end, level, before it goes up or down again; and how tall that way is.</summary>
        public const int Masonry = 2;
        public const int Approach = HallKeep + 1;
        public const int Opening = 4;

        /// <summary>How deep into the way down the hall is looked for first, as a share of the depth from the mouth to the shaft; and the farthest it is looked for from there.</summary>
        private static readonly double[] HallDepths = { 0.42, 0.36, 0.48, 0.30, 0.54, 0.25, 0.60 };
        private const int HallLookAcross = 160;
        private const int HallLookDown = 40;

        /// <summary>Of the hall and its brick, at least this share must have been solid ground: it is cut into the rock, not built across a cave.</summary>
        private const double HallSolid = 0.85;

        /// <summary>The back wall's registers, from the floor up, and how far apart its pilasters are.</summary>
        private const int DadoRows = 2;
        private const int FriezeRows = 1;
        private const int Bay = 8;

        // ---- torches and walls -------------------------------------------------------------------

        public const int TorchGapLeast = 18;
        public const int TorchGapMost = 32;

        /// <summary>How far outside the ball its walls are the evil's.</summary>
        public const int EvilReach = 200;

        /// <summary>Rows either side of the bottom of the surface layer over which dirt wall gives out.</summary>
        private const int DirtFray = 3;

        /// <summary>How much of the rock wall's noise is over the line: about one tile in five of the way's walls under the surface layer.</summary>
        private const float RockPatches = 0.70f;

        public TheWayDownPass(string name, double weight) : base(name, weight) { }

        /// <summary>The bulges' noise, rolled for each way made.</summary>
        private static int BulgeSeed;

        /// <summary>The way the last way down made goes, tile by tile, from its mouth down to the hall, along it, and down to the shaft: for looking at it.</summary>
        public static IReadOnlyList<Point> LastWay { get; private set; } = Array.Empty<Point>();

        /// <summary>How far off the Dungeon the last way down made kept: <see cref="DungeonKeep"/>, or round about <see cref="RoundKeep"/>.</summary>
        public static int LastKeep { get; private set; }

        /// <summary>Where the last way down made came into the ball's hollow, round about from the side (<see cref="RoundAbout"/>); nothing if it ends at the top of the shaft.</summary>
        public static Point LastInto { get; private set; }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "A way down";
            Make(progress);
        }

        // ===========================================================================================
        // THE WHOLE OF IT
        // ===========================================================================================

        /// <summary>
        /// Finds the way down and, if there is one, cuts it, builds the hall and stands the door.
        /// False, with nothing touched, if there is no ball or no way.
        /// </summary>
        public static bool Make(GenerationProgress progress)
        {
            progress?.Set(0f);

            if (!ExtraLargeWorld.Active)
            {
                Say("not a Grand world: there is no ball to go down to.");
                return false;
            }

            Point ball = TheBallPass.Centre;

            if (ball == Point.Zero)
            {
                Say("the ball was not made in this world.");
                return false;
            }

            Point shaft = TheBallPass.ShaftTop != Point.Zero ? TheBallPass.ShaftTop : FindShaft(ball);

            if (shaft == Point.Zero)
            {
                Say("the top of the ball's shaft could not be found.");
                return false;
            }

            int reach = TheBallPass.ShellReach > 0 ? TheBallPass.ShellReach : (int)Math.Ceiling(TheBallPass.Radius * 1.13) + 6;
            UnifiedRandom rand = WorldGen.genRand;
            BulgeSeed = rand.Next();
            Kinds();

            // The mouth's window, in the plan's columns (the ocean at 0).
            int beach = BeachEnd();
            int dungeon = DungeonSeaSide();
            int from = beach + BeachKeep;
            int to = dungeon - DungeonKeep;

            if (from > to)
            {
                Say($"no ground between the beach (plan {beach}) and the Dungeon (plan {dungeon}).");
                return false;
            }

            (int left, int right) = WorldMirror.Span(from, to);
            int high = int.MaxValue;

            for (int x = left; x <= right; x++)
            {
                int y = Surface(x);

                if (y > 0)
                    high = Math.Min(high, y);
            }

            if (high == int.MaxValue)
            {
                Say("no ground could be found in the window for the mouth.");
                return false;
            }

            int x0 = Math.Max(60, Math.Min(left, shaft.X) - BandMost * 3 - HallLookAcross - 60);
            int x1 = Math.Min(Main.maxTilesX - 61, Math.Max(right, shaft.X) + BandMost * 3 + HallLookAcross + 60);
            int y0 = Math.Max(60, high - 40);
            int y1 = Math.Min(Main.UnderworldLayer - 60, shaft.Y + 40);

            Area area = Map(x0, y0, x1, y1, ball, reach, beach);
            progress?.Set(0.25f);

            List<Point> mouths = Mouths(area, from, to, rand);

            // The first mouth, and hall, that a way can be found from and to (around the hall, never
            // through it), and that, once cut, can be walked down: a way that cannot is put back as
            // it was, and the next tried.
            string why = mouths.Count == 0
                ? $"nowhere between plan {from} and plan {to} could the mouth be opened without undermining something"
                : "no rock solid and clear enough for the hall under any of them";
            int tried = 0;

            foreach (Point mouth in mouths)
            {
                int side = shaft.X >= mouth.X ? 1 : -1;

                foreach (Rectangle hall in Halls(area, mouth, shaft, side))
                {
                    int floor = hall.Bottom;
                    Point entry = new Point(side > 0 ? hall.Left - Masonry - Approach : hall.Right - 1 + Masonry + Approach, floor - 2);
                    Point exit = new Point(side > 0 ? hall.Right - 1 + Masonry + Approach : hall.Left - Masonry - Approach, floor - 2);
                    Rectangle shell = new Rectangle(hall.Left - Masonry, hall.Top - Masonry, hall.Width + Masonry * 2, hall.Height + Masonry * 2);
                    bool[] free = (bool[])area.Free.Clone();
                    Close(area, free, shell, HallKeep);

                    List<Point> upper = Route(area, free, mouth, entry, false, rand);

                    if (upper == null)
                    {
                        why = $"no way from the mouth at {mouth.X}, {mouth.Y} down to the hall's near end at {entry.X}, {entry.Y}";
                        continue;
                    }

                    List<Point> lower = Route(area, free, exit, shaft, true, rand);

                    if (lower == null)
                    {
                        why = $"no way from the hall's far end at {exit.X}, {exit.Y} down to the shaft's top at {shaft.X}, {shaft.Y}";
                        continue;
                    }

                    tried++;
                    progress?.Set(0.6f);

                    // Planned: now it is cut, the hall built, and the door stood in it.
                    undo = new Undo();
                    var cut = new HashSet<Point>();
                    List<Vector2> upperLine = Line(upper, rand, out float[] upperRadius);
                    List<Vector2> lowerLine = Line(lower, rand, out float[] lowerRadius);

                    Dictionary<int, int> bottom = Bottoms(upperLine.Concat(lowerLine));
                    Cut(area, upperLine, upperRadius, hall, bottom, cut);
                    Cut(area, lowerLine, lowerRadius, hall, bottom, cut);
                    Level(area, hall, cut);
                    Build(hall);
                    Point door = Door(hall);
                    progress?.Set(0.75f);

                    // Then walked, and made wider where one walking it would be stopped.
                    var way = new List<Point>();

                    foreach (Vector2 at in upperLine)
                        Step(way, new Point((int)Math.Round(at.X), (int)Math.Round(at.Y)));

                    for (int x = entry.X; x != exit.X; x += Math.Sign(exit.X - entry.X))
                        Step(way, new Point(x, floor - 2));

                    foreach (Vector2 at in lowerLine)
                        Step(way, new Point((int)Math.Round(at.X), (int)Math.Round(at.Y)));

                    int mends = Mend(area, way, mouth, shaft, hall, bottom, cut, out bool walked);

                    if (!walked)
                    {
                        undo.Restore();
                        undo = null;
                        why = $"the way from the mouth at {mouth.X}, {mouth.Y} by the hall at {hall.X}, {floor} could not be walked down, even made wider in {mends} places";
                        continue;
                    }

                    undo = null;
                    progress?.Set(0.85f);

                    Prop(cut);
                    Loosen(cut);
                    Walls(cut, ball, reach);

                    int torches = Torches(upperLine, ball, reach, hall) + Torches(lowerLine, ball, reach, hall);
                    Frame(cut, hall);

                    if (WorldGen.gen)
                        GenVars.structures?.AddProtectedStructure(new Rectangle(shell.X - Approach, shell.Y, shell.Width + Approach * 2, shell.Height), 4);

                    WayDownState.Set(mouth, door);
                    LastWay = way;
                    LastKeep = DungeonKeep;
                    LastInto = Point.Zero;
                    progress?.Set(1f);

                    Say($"made: the mouth at {mouth.X}, {mouth.Y}; the hall at {hall.X}-{hall.Right - 1}, floor {floor}, the door at {door.X}, {door.Y}; " +
                        $"the shaft's top at {shaft.X}, {shaft.Y}; {Many(way.Count, "tile")} of way, {Many(cut.Count, "tile")} cut, {Many(torches, "torch", "torches")}; " +
                        $"walked from the mouth to the shaft ({Many(mends, "place")} made wider for it{(tried > 1 ? $"; {Many(tried - 1, "way")} before it put back, that could not be walked down" : "")}).");
                    return true;
                }
            }

            // Round about (by request): the Dungeon lies between, and the way goes round it, into the
            // top of the shaft; or, where the Dungeon is round that too, into the ball's hollow from
            // the side (by request: "the access to the inner circle of the ball dont have to be from
            // top down, it can be from any angle").
            string first = $"{Many(mouths.Count, "mouth")} and {Many(tried, "way")} tried, the last: {why}";

            if (RoundAbout(progress, ball, reach, shaft, beach, from, to, high, rand, first, null, out string round))
                return true;

            if (RoundAbout(progress, ball, reach, shaft, beach, from, to, high, rand, first, round, out string into))
                return true;

            Say($"no way could be found that keeps clear of everything and can be walked down, from {Many(mouths.Count, "mouth")} and {Many(tried, "way")} tried (the last: {why}); nor round about the Dungeon ({round}); " +
                $"nor round about it into the ball's hollow from the side ({into}).");
            return false;
        }

        /// <summary>A count and its noun, the noun as many as the count says: "1 way", "2 ways".</summary>
        private static string Many(int count, string one, string many = null) => count + " " + (count == 1 ? one : many ?? one + "s");

        // ===========================================================================================
        // PUT BACK
        // ===========================================================================================

        /// <summary>The tiles a way being made has touched, as they were, while it is not yet known that it can be walked down; else null.</summary>
        private static Undo undo;

        /// <summary>The tiles as they were before the way touched them, to put back if the way is given up.</summary>
        private sealed class Undo
        {
            private struct Was
            {
                public bool Has;
                public ushort Type, Wall;
                public short FrameX, FrameY;
                public BlockType Block;
                public TileColorCache Colors, WallColors;
                public byte Liquid;
                public int LiquidKind;
            }

            private readonly Dictionary<Point, Was> was = new Dictionary<Point, Was>();

            public void Keep(int x, int y)
            {
                var at = new Point(x, y);

                if (was.ContainsKey(at))
                    return;

                Tile tile = Main.tile[x, y];

                was[at] = new Was
                {
                    Has = tile.HasTile, Type = tile.TileType, Wall = tile.WallType, FrameX = tile.TileFrameX, FrameY = tile.TileFrameY,
                    Block = tile.BlockType, Colors = tile.BlockColorAndCoating(), WallColors = tile.WallColorAndCoating(),
                    Liquid = tile.LiquidAmount, LiquidKind = tile.LiquidType,
                };
            }

            public void Restore()
            {
                foreach ((Point at, Was w) in was)
                {
                    Tile tile = Main.tile[at.X, at.Y];

                    tile.HasTile = w.Has;
                    tile.TileType = w.Type;
                    tile.WallType = w.Wall;
                    tile.TileFrameX = w.FrameX;
                    tile.TileFrameY = w.FrameY;
                    tile.BlockType = w.Block;
                    tile.UseBlockColors(w.Colors);
                    tile.UseWallColors(w.WallColors);
                    tile.LiquidAmount = w.Liquid;
                    tile.LiquidType = w.LiquidKind;
                }
            }
        }

        private static void Say(string what) => GenLog.Log?.Info("The Way Down: " + what);

        /// <summary>The next tile of the way, and any between it and the last, so that each is next to the one before.</summary>
        private static void Step(List<Point> way, Point next)
        {
            if (way.Count > 0)
            {
                Point last = way[^1];

                while (Math.Max(Math.Abs(next.X - last.X), Math.Abs(next.Y - last.Y)) > 1)
                {
                    last = new Point(last.X + Math.Sign(next.X - last.X), last.Y + Math.Sign(next.Y - last.Y));
                    way.Add(last);
                }

                if (last == next)
                    return;
            }

            way.Add(next);
        }

        // ===========================================================================================
        // WHAT THE GROUND IS
        // ===========================================================================================

        private static bool[] ground, soft, loose, naturalWall;

        /// <summary>
        /// The four kinds, as lists of the game's own names. Whitelists on purpose (as the ball's
        /// arms have it): a tile nobody thought of is kept clear of, not cut.
        /// </summary>
        private static void Kinds()
        {
            int tiles = TileLoader.TileCount;
            int walls = WallLoader.WallCount;

            ground = new bool[tiles];
            soft = new bool[tiles];
            loose = new bool[tiles];
            naturalWall = new bool[walls];

            foreach (ushort type in new ushort[]
            {
                TileID.Dirt, TileID.Stone, TileID.Grass, TileID.ClayBlock, TileID.Mud, TileID.JungleGrass, TileID.MushroomGrass,
                TileID.Sand, TileID.HardenedSand, TileID.Sandstone, TileID.DesertFossil, TileID.Silt, TileID.Slush,
                TileID.SnowBlock, TileID.IceBlock, TileID.CorruptIce, TileID.FleshIce, TileID.HallowedIce,
                TileID.Granite, TileID.Marble, TileID.Ash, TileID.Obsidian, TileID.Mudstone,
                TileID.Ebonstone, TileID.Crimstone, TileID.Pearlstone, TileID.Ebonsand, TileID.Crimsand, TileID.Pearlsand,
                TileID.CorruptGrass, TileID.CrimsonGrass, TileID.HallowedGrass, TileID.CorruptJungleGrass, TileID.CrimsonJungleGrass,
                TileID.CorruptHardenedSand, TileID.CrimsonHardenedSand, TileID.HallowHardenedSand,
                TileID.CorruptSandstone, TileID.CrimsonSandstone, TileID.HallowSandstone,
                TileID.Copper, TileID.Tin, TileID.Iron, TileID.Lead, TileID.Silver, TileID.Tungsten, TileID.Gold, TileID.Platinum,
                TileID.Demonite, TileID.Crimtane,
                TileID.Amethyst, TileID.Topaz, TileID.Sapphire, TileID.Emerald, TileID.Ruby, TileID.Diamond,
                TileID.GreenMoss, TileID.BrownMoss, TileID.RedMoss, TileID.BlueMoss, TileID.PurpleMoss, TileID.LavaMoss,
                TileID.KryptonMoss, TileID.XenonMoss, TileID.ArgonMoss,
            })
                ground[type] = true;

            foreach (ushort type in new ushort[]
            {
                TileID.Cobweb, TileID.Plants, TileID.Plants2, TileID.CorruptPlants, TileID.CrimsonPlants,
                TileID.HallowedPlants, TileID.HallowedPlants2, TileID.JunglePlants, TileID.JunglePlants2, TileID.MushroomPlants,
                TileID.LongMoss, TileID.WaterDrip, TileID.LavaDrip, TileID.HoneyDrip, TileID.SandDrip,
            })
                soft[type] = true;

            for (int type = 0; type < tiles; type++)
            {
                if (TileID.Sets.IsVine[type])
                    soft[type] = true;
            }

            foreach (ushort type in new ushort[]
            {
                TileID.Pots, TileID.Stalactite, TileID.SmallPiles, TileID.LargePiles, TileID.LargePiles2, TileID.ExposedGems,
                TileID.Trees, TileID.PalmTree, TileID.Sunflower, TileID.Pumpkins, TileID.DyePlants, TileID.ImmatureHerbs,
                TileID.MatureHerbs, TileID.BloomingHerbs, TileID.LilyPad, TileID.Cattail,
            })
                loose[type] = true;

            foreach (ushort wall in new ushort[]
            {
                WallID.DirtUnsafe, WallID.DirtUnsafe1, WallID.DirtUnsafe2, WallID.DirtUnsafe3, WallID.DirtUnsafe4,
                WallID.GrassUnsafe, WallID.FlowerUnsafe, WallID.Grass, WallID.Flower, WallID.Jungle,
                WallID.JungleUnsafe, WallID.JungleUnsafe1, WallID.JungleUnsafe2, WallID.JungleUnsafe3, WallID.JungleUnsafe4, WallID.MudUnsafe,
                WallID.CaveUnsafe, WallID.Cave2Unsafe, WallID.Cave3Unsafe, WallID.Cave4Unsafe, WallID.Cave5Unsafe,
                WallID.Cave6Unsafe, WallID.Cave7Unsafe, WallID.Cave8Unsafe, WallID.CaveWall, WallID.CaveWall2,
                WallID.RocksUnsafe1, WallID.RocksUnsafe2, WallID.RocksUnsafe3, WallID.RocksUnsafe4,
                WallID.LavaUnsafe1, WallID.LavaUnsafe2, WallID.LavaUnsafe3, WallID.LavaUnsafe4,
                WallID.AmethystUnsafe, WallID.TopazUnsafe, WallID.SapphireUnsafe, WallID.EmeraldUnsafe, WallID.RubyUnsafe, WallID.DiamondUnsafe,
                WallID.SpiderUnsafe, WallID.MushroomUnsafe, WallID.SnowWallUnsafe, WallID.IceUnsafe,
                WallID.MarbleUnsafe, WallID.GraniteUnsafe,
                WallID.Sandstone, WallID.HardenedSand, WallID.CorruptHardenedSand, WallID.CrimsonHardenedSand, WallID.HallowHardenedSand,
                WallID.CorruptSandstone, WallID.CrimsonSandstone, WallID.HallowSandstone,
                WallID.EbonstoneUnsafe, WallID.CrimstoneUnsafe, WallID.CorruptGrassUnsafe, WallID.CrimsonGrassUnsafe, WallID.HallowedGrassUnsafe,
                WallID.CorruptionUnsafe1, WallID.CorruptionUnsafe2, WallID.CorruptionUnsafe3, WallID.CorruptionUnsafe4,
                WallID.CrimsonUnsafe1, WallID.CrimsonUnsafe2, WallID.CrimsonUnsafe3, WallID.CrimsonUnsafe4,
                WallID.HallowUnsafe1, WallID.HallowUnsafe2, WallID.HallowUnsafe3, WallID.HallowUnsafe4,
            })
                naturalWall[wall] = true;
        }

        private static bool Ground(ushort type) => type < ground.Length && ground[type];
        private static bool Soft(ushort type) => type < soft.Length && soft[type];
        private static bool Loose(ushort type) => type < loose.Length && loose[type];
        private static bool NaturalWall(ushort wall) => wall < naturalWall.Length && naturalWall[wall];

        /// <summary>The Dungeon's brick, cracked or not.</summary>
        private static bool Dungeon(ushort type) => Main.tileDungeon[type]
            || type == TileID.CrackedBlueDungeonBrick || type == TileID.CrackedGreenDungeonBrick || type == TileID.CrackedPinkDungeonBrick;

        /// <summary>The plan's column of a world column: the ocean at 0, whichever way round the world came out.</summary>
        private static int Plan(int x) => WorldMirror.Flipped ? Main.maxTilesX - x : x;

        private static bool Solid(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>
        /// The top tile of the ground in this column: the first solid tile from the sky down, but
        /// for an island (high in the sky, with a long fall of air under it). -1 if there is none
        /// above the surface layer's bottom.
        /// </summary>
        public static int Surface(int x)
        {
            if (x < 10 || x >= Main.maxTilesX - 10)
                return -1;

            int limit = (int)Main.worldSurface + 50;
            int y = 30;

            while (y < limit)
            {
                if (!Solid(x, y))
                {
                    y++;
                    continue;
                }

                int top = y;

                if (top >= Main.worldSurface * SkyShare)
                    return top;

                while (y < limit && Solid(x, y))
                    y++;

                int air = 0;

                while (y < limit && !Solid(x, y) && air < IslandGap)
                {
                    y++;
                    air++;
                }

                if (air < IslandGap)
                    return top;
            }

            return -1;
        }

        /// <summary>
        /// The plan's column where the beach ends: going inland from the sea, the first of twelve
        /// columns in a row that are neither sea nor sand.
        /// </summary>
        private static int BeachEnd()
        {
            int run = 0;

            for (int p = 40; p < Main.maxTilesX / 2; p++)
            {
                if (Shore(WorldMirror.X(p)))
                {
                    run = 0;
                    continue;
                }

                if (++run >= 12)
                    return p - 11;
            }

            return Layout.OceanEnd;
        }

        /// <summary>
        /// The sea or the beach: going down from the sky (past any island in it), water before
        /// ground, no ground at all above the bottom of the surface layer, or ground that is sand.
        /// </summary>
        private static bool Shore(int x)
        {
            int limit = (int)Main.worldSurface + 50;
            int y = 30;

            while (y < limit)
            {
                if (Main.tile[x, y].LiquidAmount > 0)
                    return true;

                if (!Solid(x, y))
                {
                    y++;
                    continue;
                }

                int top = y;
                bool sandy = Main.tile[x, top].TileType is TileID.Sand or TileID.HardenedSand or TileID.ShellPile or TileID.Sandstone;

                if (top >= Main.worldSurface * SkyShare)
                    return sandy;

                while (y < limit && Solid(x, y))
                    y++;

                int air = 0;

                while (y < limit && !Solid(x, y) && Main.tile[x, y].LiquidAmount == 0 && air < IslandGap)
                {
                    y++;
                    air++;
                }

                if (air < IslandGap)
                    return sandy;
            }

            return true;
        }

        /// <summary>
        /// The plan's column of the Dungeon's nearest part to the sea, in the ground over the
        /// cavern layer (its brick and its walls, read off the world). Under it, the map keeps the
        /// way off all of it.
        /// </summary>
        private static int DungeonSeaSide()
        {
            int near = int.MaxValue;
            int x0 = Math.Max(10, Main.dungeonX - 900);
            int x1 = Math.Min(Main.maxTilesX - 10, Main.dungeonX + 900);
            int y1 = (int)Main.rockLayer;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = 20; y < y1; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if ((tile.HasTile && Dungeon(tile.TileType)) || (tile.WallType > 0 && Main.wallDungeon[tile.WallType]))
                    {
                        near = Math.Min(near, Plan(x));
                        break;
                    }
                }
            }

            return near == int.MaxValue ? Plan(Main.dungeonX) - 200 : near;
        }

        /// <summary>
        /// The ball's shaft, in a world made before its top was kept (and in any world loaded
        /// since: its top is not saved): the column through the middle of the ball that is open
        /// furthest up, and the top of it (<see cref="StraightShaft"/>); or, where no column is
        /// open from the hollow far enough up, the shaft followed up as it winds
        /// (<see cref="WindingShaft"/>).
        /// </summary>
        private static Point FindShaft(Point ball)
        {
            Point straight = StraightShaft(ball);

            return straight != Point.Zero ? straight : WindingShaft(ball);
        }

        /// <summary>The column through the middle of the ball that is open furthest up, straight up from the hollow, and the top of it; nothing if none is open far enough up.</summary>
        private static Point StraightShaft(Point ball)
        {
            int start = ball.Y - 40;
            int best = int.MaxValue;
            var tops = new List<int>();

            for (int x = ball.X - 60; x <= ball.X + 60; x++)
            {
                if (!WorldGen.InWorld(x, start, 20) || Solid(x, start))
                    continue;

                int y = start;

                while (y > 20 && !Solid(x, y - 1))
                    y--;

                if (y < best - 1)
                {
                    best = y;
                    tops.Clear();
                }

                if (y <= best + 1)
                    tops.Add(x);
            }

            // Nothing open above the hollow's roof: there is no shaft to find.
            if (tops.Count == 0 || best > ball.Y - TheBallPass.Hollow - 60)
                return Point.Zero;

            return new Point(tops[tops.Count / 2], best);
        }

        /// <summary>
        /// The row the shaft is looked for from over the ring's top knuckle (it reaches five rows
        /// over its orb) and under the hollow's roof where it comes lowest (the hollow is never
        /// nearer its middle than Hollow * (1 - HollowRough), less three of grain).
        /// </summary>
        private const int OverKnuckle = TheBallPass.OrbRing + 11;

        /// <summary>
        /// How far either way the shaft is followed over as it winds up (<see cref="Climb"/>): a
        /// tile to the next column at the most, of the open tiles along a row; and how far from the
        /// column it was begun in.
        /// </summary>
        private const int WindOver = 4;
        private const int WindMost = 20;

        /// <summary>
        /// The ball's shaft as it was cut (TheBallPass): it moves over a column now and then as it
        /// goes down, so that no one column may be open from the hollow to its top; and the ring's
        /// top knuckle may hang right under its foot (in Flacid both: straight up from forty rows
        /// over the hollow's middle, no column is open further than 169 rows over it, and 230 are
        /// asked; the shaft's top is 417 over it). So it is followed up from the hollow's top, from every
        /// open tile of it under the knuckle (forty rows over the middle) and over it
        /// (<see cref="OverKnuckle"/>) within sixty columns of the middle: straight up while it can,
        /// else over to the nearest tile along the row (<see cref="Climb"/>). The highest any of them
        /// comes to, across the middle of the open tiles there, if it is as far over the hollow's roof
        /// as <see cref="StraightShaft"/> asks; else nothing.
        /// </summary>
        private static Point WindingShaft(Point ball)
        {
            Point best = Point.Zero;

            foreach (int start in new[] { ball.Y - 40, ball.Y - OverKnuckle })
            {
                for (int x = ball.X - 60; x <= ball.X + 60; x++)
                {
                    if (!WorldGen.InWorld(x, start, 20) || Solid(x, start))
                        continue;

                    Point top = Climb(x, start);

                    if (best == Point.Zero || top.Y < best.Y)
                        best = top;
                }
            }

            if (best == Point.Zero || best.Y > ball.Y - TheBallPass.Hollow - 60)
                return Point.Zero;

            int left = best.X, right = best.X;

            while (left > best.X - WindOver && !Solid(left - 1, best.Y))
                left--;

            while (right < best.X + WindOver && !Solid(right + 1, best.Y))
                right++;

            return new Point((left + right) / 2, best.Y);
        }

        /// <summary>
        /// Up from an open tile through the open tiles: straight up while it can; where it cannot,
        /// over to the nearest tile along the row that is open over it, <see cref="WindOver"/> at the
        /// most either way and every tile between open, never further than <see cref="WindMost"/>
        /// from the column it began in. Where it comes to, the highest.
        /// </summary>
        private static Point Climb(int x, int y)
        {
            int from = x;

            while (y > 20)
            {
                if (!Solid(x, y - 1))
                {
                    y--;
                    continue;
                }

                int next = int.MinValue;

                for (int d = 1; d <= WindOver && next == int.MinValue; d++)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        int c = x + side * d;

                        if (Math.Abs(c - from) > WindMost)
                            continue;

                        bool along = true;

                        for (int k = 1; k <= d && along; k++)
                            along = !Solid(x + side * k, y);

                        if (along && !Solid(c, y - 1))
                        {
                            next = c;
                            break;
                        }
                    }
                }

                if (next == int.MinValue)
                    break;

                x = next;
                y--;
            }

            return new Point(x, y);
        }

        // ===========================================================================================
        // THE MAP OF WHAT IT MAY NOT COME NEAR
        // ===========================================================================================

        private sealed class Area
        {
            public readonly int X0, Y0, W, H;
            public bool[] Blocked;
            public bool[] Free;

            /// <summary>Of what it keeps away from, all but the loose things (each with its own distance round it): round about, one walking the way may go through the air beside a pot or a pile, but never into any of this.</summary>
            public bool[] Firm;

            /// <summary>How far down from each tile it is to something to stand on (as the world was, before anything is cut), at the most <see cref="AirMost"/>.</summary>
            public byte[] Air;

            public Area(int x0, int y0, int w, int h)
            {
                X0 = x0;
                Y0 = y0;
                W = w;
                H = h;
            }

            public bool In(int x, int y) => x >= X0 && y >= Y0 && x < X0 + W && y < Y0 + H;

            public int I(int x, int y) => (y - Y0) * W + (x - X0);

            public bool FreeAt(int x, int y) => In(x, y) && Free[I(x, y)];

            public bool BlockedAt(int x, int y) => !In(x, y) || Blocked[I(x, y)];

            public int AirAt(int x, int y) => In(x, y) ? Air[I(x, y)] : AirMost;
        }

        /// <summary>
        /// The map of what the way may not come near, over these columns and rows (see the notes
        /// over the pass); keeping <paramref name="dungeonKeep"/> off the Dungeon. Into the ball's
        /// hollow from the side (<paramref name="intoHollow"/>, see <see cref="RoundAbout"/>), the
        /// ball is not kept off; and what the Dungeon left in the hollow when the ball was made round
        /// it (its walls within <see cref="HollowReach"/> of the middle, where there is none of its
        /// brick) is not the Dungeon.
        /// </summary>
        private static Area Map(int x0, int y0, int x1, int y1, Point ball, int reach, int beach, int dungeonKeep = DungeonKeep, bool intoHollow = false)
        {
            var area = new Area(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            int n = area.W * area.H;

            var liquid = new bool[n];
            var built = new bool[n];
            var looseHere = new bool[n];
            var wall = new bool[n];
            var dungeon = new bool[n];
            var zone = new bool[n];

            bool graveyard = GraveyardZone.Exists;
            Rectangle grave = graveyard
                ? new Rectangle(GraveyardZone.Left - GraveKeep, GraveyardZone.Top - GraveKeep,
                    GraveyardZone.Right - GraveyardZone.Left + GraveKeep * 2, GraveyardZone.Bottom - GraveyardZone.Top + GraveKeep * 2)
                : Rectangle.Empty;
            double ballReach = reach * (double)reach;
            double hollowReach = HollowReach * (double)HollowReach;
            int coast = beach + BeachKeep;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = area.I(x, y);

                    if (!WorldGen.InWorld(x, y, 50))
                    {
                        zone[i] = true;
                        continue;
                    }

                    Tile tile = Main.tile[x, y];
                    double dx = x - ball.X, dy = y - ball.Y;

                    if (tile.LiquidAmount > 0)
                        liquid[i] = true;

                    if (tile.HasTile)
                    {
                        ushort type = tile.TileType;

                        if (Dungeon(type))
                            dungeon[i] = true;
                        else if (type == TileID.MinecartTrack)
                        {
                            // A track may be crossed: it is left where it is, the way passing under or
                            // over it, and it stands in the air as tracks do.
                        }
                        else if (!Ground(type) && !Soft(type))
                        {
                            if (Loose(type))
                            {
                                // A pot, a pile, a stalactite, a gem, a tree: it, and what it stands on,
                                // hangs from or clings to (a gem clings to any side).
                                looseHere[i] = true;

                                foreach ((int nx, int ny) in new[] { (x, y - 1), (x, y + 1), (x - 1, y), (x + 1, y) })
                                {
                                    if (area.In(nx, ny))
                                        looseHere[area.I(nx, ny)] = true;
                                }
                            }
                            else
                                built[i] = true;
                        }
                    }

                    if (tile.WallType > 0)
                    {
                        if (Main.wallDungeon[tile.WallType])
                        {
                            // Into the hollow, its walls left in the hollow, with nothing of its brick:
                            // gone round, never cut, as a pot is; not kept away from.
                            if (intoHollow && !dungeon[i] && dx * dx + dy * dy < hollowReach)
                                looseHere[i] = true;
                            else
                                dungeon[i] = true;
                        }
                        else if (!NaturalWall(tile.WallType))
                            wall[i] = true;
                    }

                    if (tile.RedWire || tile.BlueWire || tile.GreenWire || tile.YellowWire || tile.HasActuator)
                        built[i] = true;

                    if (!intoHollow && dx * dx + dy * dy < ballReach)
                        zone[i] = true;
                    else if (graveyard && grave.Contains(x, y))
                        zone[i] = true;
                    else if (y < Main.rockLayer && Plan(x) < coast)
                        zone[i] = true;
                }
            }

            // Everything but the loose things, each with its own distance round it; then those.
            var firm = zone;
            Or(firm, Dilate(liquid, area, LiquidKeep));
            Or(firm, Dilate(built, area, BuiltKeep));
            Or(firm, Dilate(wall, area, WallKeep));
            Or(firm, Dilate(dungeon, area, dungeonKeep));

            bool[] looseKept = Dilate(looseHere, area, LooseKeep);
            var blocked = (bool[])firm.Clone();
            Or(blocked, looseKept);
            area.Blocked = blocked;
            area.Firm = firm;

            bool[] near = Dilate(firm, area, LineClear);
            Or(near, Dilate(looseKept, area, LooseClear));
            var free = new bool[n];

            for (int i = 0; i < n; i++)
                free[i] = !near[i];

            area.Free = free;

            // From the bottom of each column up: how far down it is to stand on something.
            var air = new byte[n];

            for (int x = x0; x <= x1; x++)
            {
                int below = 0;

                for (int y = y1; y >= y0; y--)
                {
                    below = Solid(x, y) ? 0 : Math.Min(below + 1, AirMost);
                    air[area.I(x, y)] = (byte)below;
                }
            }

            area.Air = air;
            return area;
        }

        private static void Or(bool[] into, bool[] from)
        {
            for (int i = 0; i < into.Length; i++)
                into[i] |= from[i];
        }

        /// <summary>Everything within <paramref name="r"/> tiles of a marked tile, across and down (a square), marked.</summary>
        private static bool[] Dilate(bool[] src, Area area, int r) => Spread(src, area.W, area.H, r);

        /// <summary>Everything within <paramref name="r"/> tiles of a marked tile, across and down (a square), marked: on a grid <paramref name="w"/> wide, row by row.</summary>
        private static bool[] Spread(bool[] src, int w, int h, int r)
        {
            var across = new bool[src.Length];

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                int count = 0;

                for (int x = 0; x < Math.Min(r, w); x++)
                {
                    if (src[row + x])
                        count++;
                }

                for (int x = 0; x < w; x++)
                {
                    int add = x + r;
                    int drop = x - r - 1;

                    if (add < w && src[row + add])
                        count++;

                    if (drop >= 0 && src[row + drop])
                        count--;

                    across[row + x] = count > 0;
                }
            }

            var result = new bool[src.Length];

            for (int x = 0; x < w; x++)
            {
                int count = 0;

                for (int y = 0; y < Math.Min(r, h); y++)
                {
                    if (across[y * w + x])
                        count++;
                }

                for (int y = 0; y < h; y++)
                {
                    int add = y + r;
                    int drop = y - r - 1;

                    if (add < h && across[add * w + x])
                        count++;

                    if (drop >= 0 && across[drop * w + x])
                        count--;

                    result[y * w + x] = count > 0;
                }
            }

            return result;
        }

        /// <summary>A box, and everything within <paramref name="r"/> of it, closed to the way.</summary>
        private static void Close(Area area, bool[] free, Rectangle box, int r)
        {
            for (int y = box.Top - r; y < box.Bottom + r; y++)
            {
                for (int x = box.Left - r; x < box.Right + r; x++)
                {
                    if (area.In(x, y))
                        free[area.I(x, y)] = false;
                }
            }
        }

        // ===========================================================================================
        // THE MOUTH AND THE HALL
        // ===========================================================================================

        /// <summary>
        /// Where the mouth may be, in the order they are tried: in the window, on ground the way
        /// can start under (nothing near enough to be undermined, no water near), not on a cliff.
        /// The ones nearest the Dungeon first ("prob before the dungeon"), one of the first eight
        /// at random, then each next one at least <see cref="MouthsApart"/> columns from those
        /// before it; <see cref="MouthsTried"/> at the most.
        /// </summary>
        private static List<Point> Mouths(Area area, int from, int to, UnifiedRandom rand)
        {
            var good = new List<(Point At, int Plan)>();

            for (int p = from; p <= to; p++)
            {
                int x = WorldMirror.X(p);
                int y = Surface(x);

                if (y < 0 || y >= Main.worldSurface)
                    continue;

                int left = Surface(x - MouthLook);
                int right = Surface(x + MouthLook);

                if (left < 0 || right < 0 || Math.Abs(left - y) > MouthSteepest || Math.Abs(right - y) > MouthSteepest)
                    continue;

                if (!area.FreeAt(x, y + 1) || !area.FreeAt(x, y + 6))
                    continue;

                good.Add((new Point(x, y + 1), p));
            }

            good.Sort((a, b) => b.Plan.CompareTo(a.Plan));

            var mouths = new List<Point>();

            if (good.Count == 0)
                return mouths;

            mouths.Add(good[rand.Next(Math.Min(8, good.Count))].At);

            foreach ((Point at, int _) in good)
            {
                if (mouths.Count >= MouthsTried)
                    break;

                if (mouths.TrueForAll(m => Math.Abs(m.X - at.X) >= MouthsApart))
                    mouths.Add(at);
            }

            return mouths;
        }

        /// <summary>
        /// Where the hall may be, in the order they are tried (<see cref="HallsTried"/> at the
        /// most), each its inside (its floor is the row under it): for each depth in turn, a third
        /// of the way to half of the way down from the mouth to the shaft and then further either
        /// way (<see cref="HallDepths"/>), the nearest place to the straight line between them, in
        /// rock that was solid and that nothing near would mind being built in, with room at either
        /// end for the level way out.
        /// </summary>
        private static List<Rectangle> Halls(Area area, Point mouth, Point shaft, int side)
        {
            var halls = new List<Rectangle>();

            foreach (double depth in HallDepths)
            {
                int midY = (int)(mouth.Y + (shaft.Y - mouth.Y) * depth);
                int midX = (int)(mouth.X + (shaft.X - mouth.X) * depth);
                Rectangle? found = null;

                for (int down = 0; down <= HallLookDown && found == null; down = down <= 0 ? -down + 8 : -down)
                {
                    for (int across = 0; across <= HallLookAcross; across = across <= 0 ? -across + 6 : -across)
                    {
                        int floor = midY + down;
                        int left = midX + across - HallWide / 2;

                        if (Fits(area, left, floor, side, mouth, shaft))
                        {
                            found = new Rectangle(left, floor - HallTall, HallWide, HallTall);
                            break;
                        }
                    }
                }

                if (found is Rectangle hall && halls.TrueForAll(h => Math.Abs(h.X - hall.X) >= HallWide || Math.Abs(h.Y - hall.Y) >= HallTall * 3))
                    halls.Add(hall);

                if (halls.Count >= HallsTried)
                    break;
            }

            return halls;
        }

        private static bool Fits(Area area, int left, int floor, int side, Point mouth, Point shaft)
        {
            // Far enough under the mouth and over the shaft for the way to wind down to it and on.
            if (floor - mouth.Y < 100 || shaft.Y - floor < 120)
                return false;

            int outerLeft = left - Masonry - Approach;
            int outerRight = left + HallWide - 1 + Masonry + Approach;
            int top = floor - HallTall - Masonry;
            int bottom = floor + Masonry - 1;
            int solid = 0, all = 0;

            for (int y = top; y <= bottom; y++)
            {
                for (int x = outerLeft; x <= outerRight; x++)
                {
                    if (area.BlockedAt(x, y))
                        return false;

                    all++;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && Ground(tile.TileType))
                        solid++;
                }
            }

            if (solid < all * HallSolid)
                return false;

            // The two ends of the level way, where the way down arrives (from above, outside) and
            // leaves (down, outside), with room round each for it to come and go.
            int entryX = side > 0 ? outerLeft : outerRight;
            int exitX = side > 0 ? outerRight : outerLeft;

            if (!Room(area, entryX, floor - 2, -side, -1) || !Room(area, exitX, floor - 2, side, 1))
                return false;

            if (WorldGen.gen && GenVars.structures != null
                && !GenVars.structures.CanPlace(new Rectangle(outerLeft, top, outerRight - outerLeft + 1, bottom - top + 1), 2))
                return false;

            return true;
        }

        /// <summary>How far round either end of the level way the map must be free, outward and up where the way arrives, outward and down where it leaves.</summary>
        private const int EndRoom = 3;

        private static bool Room(Area area, int x, int y, int across, int down)
        {
            for (int a = 0; a <= EndRoom; a++)
            {
                for (int b = 0; b <= EndRoom; b++)
                {
                    if (!area.FreeAt(x + across * a, y + down * b))
                        return false;
                }
            }

            return true;
        }

        // ===========================================================================================
        // THE WAY
        // ===========================================================================================

        /// <summary>
        /// The steps the way may take on its grid, across and down: the three slopes down either
        /// way (never straight down: a way straight down is a well, not a stair), a level step, and
        /// two slopes up.
        /// </summary>
        private static readonly (int Across, int Down)[] Moves =
        {
            (-1, 1), (1, 1), (-2, 1), (2, 1), (-1, 2), (1, 2), (-1, 0), (1, 0), (-1, -1), (1, -1), (-2, -1), (2, -1),
        };

        /// <summary>
        /// How many steps it goes on the same way across, at the least, before it may turn back:
        /// so that where it winds back under itself there is rock between, and a floor to walk on.
        /// </summary>
        private const int RunLeast = 3;

        /// <summary>What a step costs, for each tile of it, beside a slope down: level, and up.</summary>
        private const float LevelDear = 1.6f;
        private const float UpDear = 2.5f;

        /// <summary>How far either side of the line the way may be at its start, and how much further for each tile down from it.</summary>
        private const float StartBand = 6f;
        private const float StartSpread = 2f;

        /// <summary>Rows of the grid under the end the way may use, to get round something just over it.</summary>
        private const int RowsUnder = 15;

        /// <summary>
        /// Through a cave already there, the way keeps near its floor: a tile of it more than this
        /// far over something to stand on costs more by this much for each tile further (thirty
        /// at the most are counted). The map counts the air under each tile to <see cref="AirMost"/>.
        /// </summary>
        private const int AirFine = 3;
        private const float AirDear = 0.5f;
        private const int AirMost = 250;

        /// <summary>
        /// Whoever comes down the way walks on its floor: the cut's own, about <see cref="CutBelow"/>
        /// under its line, or where it goes through a cave already there, the cave's floor. From one
        /// tile of the way to the next that floor may fall by <see cref="DropMost"/> at the most (the
        /// game hurts a fall of more than twenty-five) and rise by <see cref="ClimbMost"/> (a jump is
        /// about six): so the way never crosses a cave high over its floor, nor goes on into the rock
        /// on its far side out of reach of it. Where the way ends at the top of the ball's shaft it
        /// falls into it, as the ball was made to be come into.
        /// </summary>
        private const int CutBelow = 3;
        private const int DropMost = 12;
        private const int ClimbMost = 2;

        /// <summary>How far round the top of the shaft the way may be over it.</summary>
        private const int ShaftCatch = 6;

        /// <summary>
        /// How far under its line the floor of the way will be: the cut's own, <see cref="CutBelow"/>
        /// under it; or where there is a cave within that (the cut opens into it), or the line is in
        /// one, the cave's floor. A hole a tile wide in it is walked over, as the game has it.
        /// </summary>
        private static int Fall(Area area, int x, int y, int deep)
        {
            int here = FallIn(area, x, y, deep);

            // A hole one tile wide is stepped over, not fallen into.
            int left = FallIn(area, x - 1, y, deep), right = FallIn(area, x + 1, y, deep);

            return Math.Min(here, Math.Max(left, right));
        }

        private static int FallIn(Area area, int x, int y, int deep)
        {
            for (int down = 1; down <= deep; down++)
            {
                int air = area.AirAt(x, y + down);

                if (air > 0)
                    return down - 1 + air;
            }

            return CutBelow;
        }


        /// <summary>
        /// The way from one point down to another, on a grid of <see cref="Cell"/> tiles, found the
        /// cheapest way (A*) through what the map leaves free, within a band either side of the
        /// straight line between them that is wider in some places than others, and narrow where it
        /// starts. It goes down as a stair does, not as a well: slopes down are cheapest, at
        /// forty-five degrees and the slopes either side of it; a level step dearer; a step up
        /// dearer still (only to get over what is in the way); turning back the other way dearer
        /// again, and never before <see cref="RunLeast"/> steps the same way. The floor under it, as
        /// it will be once cut, may not fall away or rise from one tile to the next by more than one
        /// walking it could (<see cref="Passable"/>). The cost of every tile is rolled a little, so
        /// no two ways come out the same. Null if there is none, after trying it again with the band
        /// two, four and eight times as wide (if the band was what stood in the way).
        /// </summary>
        private static List<Point> Route(Area area, bool[] free, Point from, Point to, bool intoShaft, UnifiedRandom rand)
        {
            foreach (int widen in new[] { 1, 2, 4, 8 })
            {
                List<Point> way = Route(area, free, from, to, intoShaft, rand, widen, out bool banded);

                if (way != null)
                    return way;

                if (!banded)
                    break;
            }

            return null;
        }

        /// <summary>
        /// Of each node of the grid, how it was come to: the way it was going across (not yet, left,
        /// right), and how many steps it had gone that way (up to <see cref="RunLeast"/>).
        /// </summary>
        private const int Ways = 3 * (RunLeast + 1);

        private static int State(int node, int across, int run) => node * Ways + (across + 1) * (RunLeast + 1) + run;

        private static List<Point> Route(Area area, bool[] free, Point from, Point to, bool intoShaft, UnifiedRandom rand, int widen, out bool banded)
        {
            banded = false;

            if (to.Y <= from.Y + Cell)
                return null;

            int goalJ = (int)Math.Round((to.Y - from.Y) / (double)Cell);
            int rows = goalJ + RowsUnder + 1;
            int goalI = (int)Math.Round((to.X - from.X) / (double)Cell);
            int half = (BandMost * widen + Math.Abs(to.X - from.X)) / Cell + 4;
            int cols = half * 2 + 1;
            int nodes = cols * rows;

            if (!area.In(from.X, from.Y) || !area.In(to.X, to.Y) || !free[area.I(from.X, from.Y)] || !free[area.I(to.X, to.Y)])
                return null;

            // The band: how far either side of the straight line each row may be.
            var band = new float[rows];
            double phase = rand.NextDouble() * 100.0;

            for (int j = 0; j < rows; j++)
            {
                double wave = 0.5 + 0.35 * Math.Sin(j * 0.11 + phase) + 0.15 * Math.Sin(j * 0.37 + phase * 1.7);
                float wide = (float)(BandLeast + (BandMost - BandLeast) * Math.Clamp(wave, 0.0, 1.0)) * widen;

                band[j] = Math.Min(wide, StartBand + StartSpread * Cell * j);
            }

            int seed = rand.Next();
            var g = new float[nodes * Ways];
            var parent = new int[nodes * Ways];
            var closed = new bool[nodes * Ways];
            Array.Fill(g, float.MaxValue);
            Array.Fill(parent, -1);

            var open = new PriorityQueue<int, float>();
            int startState = State(half, 0, 0);  // i = 0, j = 0, come to from nowhere
            g[startState] = 0f;
            open.Enqueue(startState, Distance(from, to) * 0.85f);

            int goalState = -1;

            while (open.TryDequeue(out int state, out float _))
            {
                if (closed[state])
                    continue;

                closed[state] = true;

                int node = state / Ways;
                int how = state % Ways;
                int run = how % (RunLeast + 1);
                int across = how / (RunLeast + 1) - 1;
                int i = node % cols - half;
                int j = node / cols;

                if (i == goalI && j == goalJ)
                {
                    goalState = state;
                    break;
                }

                float here = g[state];
                int x = from.X + i * Cell;
                int y = from.Y + j * Cell;

                // Where one stands here: at the start, on what the way starts from.
                int fallHere = node == startState / Ways ? Math.Min(Fall(area, x, y, CutBelow), CutBelow) : Fall(area, x, y, CutBelow);

                foreach ((int di, int dj) in Moves)
                {
                    int ni = i + di;
                    int nj = j + dj;

                    if (nj < 0 || nj >= rows || ni < -half || ni > half)
                        continue;

                    int h = Math.Sign(di);
                    bool turning = across != 0 && h != across;

                    if (turning && run < RunLeast)
                        continue;

                    int nx = from.X + ni * Cell;
                    int ny = from.Y + nj * Cell;
                    double t = goalJ == 0 ? 1.0 : Math.Clamp(nj / (double)goalJ, 0.0, 1.0);
                    double line = from.X + (to.X - from.X) * t;

                    if (Math.Abs(nx - line) > band[nj] + Cell && !(ni == goalI && nj == goalJ))
                    {
                        banded = true;
                        continue;
                    }

                    // Every tile of the step free, and what one walks on along it neither falling
                    // away further than a safe drop nor rising further than a jump from one tile
                    // to the next. At the hall's end, the level way; at the shaft, the shaft.
                    if (!Passable(area, free, x, y, nx, ny, y + fallHere, CutBelow, ni == goalI && nj == goalJ && !intoShaft, intoShaft ? to : (Point?)null))
                        continue;

                    float length = (float)Math.Sqrt((di * di + dj * dj) * (double)(Cell * Cell));
                    float weight = Roll(nx, ny, seed);

                    if (dj == 0)
                        weight *= LevelDear;
                    else if (dj < 0)
                        weight *= UpDear;

                    // Through a cave that was there already, it keeps to the floor: high over it,
                    // whoever came down the way would fall, and could not get back up.
                    weight *= 1f + Math.Clamp(area.AirAt(nx, ny) - AirFine, 0, 30) * AirDear;

                    float cost = here + length * weight + (turning ? TurnCost : 0f);
                    int next = State(nj * cols + ni + half, h, turning || across == 0 ? 1 : Math.Min(run + 1, RunLeast));

                    if (closed[next] || cost >= g[next])
                        continue;

                    g[next] = cost;
                    parent[next] = state;
                    open.Enqueue(next, cost + Distance(new Point(nx, ny), to) * 0.85f);
                }
            }

            if (goalState < 0)
                return null;

            var way = new List<Point>();

            for (int state = goalState; state >= 0; state = parent[state])
            {
                int node = state / Ways;
                way.Add(new Point(from.X + (node % cols - half) * Cell, from.Y + node / cols * Cell));
            }

            way.Reverse();

            // The grid's last point to the true end, which may be a tile off it.
            if (way[^1] != to)
                way.Add(to);

            return way;
        }

        private static float Distance(Point a, Point b) => (float)Math.Sqrt((a.X - b.X) * (double)(a.X - b.X) + (a.Y - b.Y) * (double)(a.Y - b.Y));

        /// <summary>
        /// Every tile on the straight step between two points of the grid is free; and one walking
        /// it, on the floor under it (see <see cref="Fall"/>), from <paramref name="floor"/> where it
        /// starts, never drops more than <see cref="DropMost"/> nor climbs more than
        /// <see cref="ClimbMost"/> from one tile to the next. At the hall's end the floor is the level
        /// way's; over the top of the shaft, where the way ends falling into it, it is not asked.
        /// </summary>
        private static bool Passable(Area area, bool[] free, int x0, int y0, int x1, int y1, int floor, int deep, bool toLevel, Point? shaft)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));

            for (int s = 1; s <= steps; s++)
            {
                int x = x0 + (int)Math.Round((x1 - x0) * s / (double)steps);
                int y = y0 + (int)Math.Round((y1 - y0) * s / (double)steps);

                if (!area.In(x, y) || !free[area.I(x, y)])
                    return false;

                if (shaft is Point top && Math.Abs(x - top.X) <= ShaftCatch && y >= top.Y - 8)
                    continue;

                int under = y + (toLevel && s == steps ? 2 : Fall(area, x, y, deep));
                int change = under - floor;

                if (change > DropMost || change < -ClimbMost)
                    return false;

                floor = under;
            }

            return true;
        }

        /// <summary>What a tile of way costs here, 0.8 to 1.45 times its length: lumps eight tiles across, the same for the whole of one way.</summary>
        private static float Roll(int x, int y, int seed)
        {
            const int lump = 8;

            int gx = Math.DivRem(x, lump, out int rx);
            int gy = Math.DivRem(y, lump, out int ry);
            float fx = rx / (float)lump;
            float fy = ry / (float)lump;

            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(gx, gy, seed), b = Hash(gx + 1, gy, seed), c = Hash(gx, gy + 1, seed), d = Hash(gx + 1, gy + 1, seed);
            float value = MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);

            return 0.8f + value * 0.65f;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);

                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;

                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>
        /// The way found, tile by tile, as the search walked it (the same tiles, so that what it
        /// measured of the floor is what is cut), with how far the cut reaches at each tile.
        /// </summary>
        private static List<Vector2> Line(List<Point> way, UnifiedRandom rand, out float[] radius)
        {
            var line = new List<Vector2>();

            for (int k = 0; k + 1 < way.Count; k++)
            {
                Point a = way[k], b = way[k + 1];
                int steps = Math.Max(1, Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)));

                for (int s = 0; s < steps; s++)
                    line.Add(new Vector2(a.X + (int)Math.Round((b.X - a.X) * s / (double)steps), a.Y + (int)Math.Round((b.Y - a.Y) * s / (double)steps)));
            }

            line.Add(way[^1].ToVector2());

            double sizePhase = rand.NextDouble() * 100.0;
            radius = new float[line.Count];

            for (int k = 0; k < line.Count; k++)
            {
                double size = 0.5 + 0.3 * Math.Sin(k * 0.17 + sizePhase) + 0.2 * Math.Sin(k * 0.05 + sizePhase * 0.7);
                radius[k] = MathHelper.Lerp(RadiusLeast, RadiusMost, (float)Math.Clamp(size, 0.0, 1.0));
            }

            return line;
        }

        // ===========================================================================================
        // THE CUT
        // ===========================================================================================

        /// <summary>
        /// Takes out the ground along the line: every tile within its reach (and a tile of grain
        /// either way) that is the ground's own, or soft; but in each column no deeper than
        /// <see cref="CutBelow"/> under the lowest tile of the line in it, so that the way's floor is
        /// where the search took it to be. The level way's floor at either end of the hall it
        /// leaves, as thick as the hall's own, so the level way stays level. Nothing with water near
        /// it is opened, whatever the map.
        /// </summary>
        private static void Cut(Area area, List<Vector2> line, float[] radius, Rectangle hall, Dictionary<int, int> bottom, HashSet<Point> cut)
        {
            int floor = hall.Bottom;
            int levelLeft = hall.Left - Masonry - Approach;
            int levelRight = hall.Right - 1 + Masonry + Approach;

            for (int k = 0; k < line.Count; k++)
            {
                Vector2 at = line[k];
                float r = radius[k];
                int reach = (int)Math.Ceiling(r + BulgeMost + 0.5f);

                for (int x = (int)at.X - reach; x <= (int)at.X + reach; x++)
                {
                    bool overLevel = x >= levelLeft && x <= levelRight;
                    int deepest = Bottom(bottom, x) + CutBelow;

                    for (int y = (int)at.Y - reach; y <= Math.Min((int)at.Y + reach, deepest); y++)
                    {
                        // The level way's floor, as thick as the hall's, stays.
                        if (overLevel && y >= floor && y < floor + Masonry)
                            continue;

                        float edge = r + Lumps(x, y, BulgeSeed) * BulgeMost + (Lumps(x, y, BulgeSeed + 1, 3) - 0.5f) * Rough + Layout.Grain(x, y, 1) * 0.5f;

                        if (Vector2.DistanceSquared(new Vector2(x, y), at) > edge * edge)
                            continue;

                        Take(area, x, y, cut);
                    }
                }
            }
        }

        /// <summary>The lowest tile of the line in each column it goes through.</summary>
        private static Dictionary<int, int> Bottoms(IEnumerable<Vector2> line)
        {
            var bottom = new Dictionary<int, int>();

            foreach (Vector2 at in line)
            {
                int x = (int)Math.Round(at.X), y = (int)Math.Round(at.Y);

                bottom[x] = bottom.TryGetValue(x, out int was) ? Math.Max(was, y) : y;
            }

            return bottom;
        }

        /// <summary>The lowest tile of the line in this column, or failing that in the nearest column either side that it goes through.</summary>
        private static int Bottom(Dictionary<int, int> bottom, int x)
        {
            if (bottom.TryGetValue(x, out int y))
                return y;

            for (int dx = 1; dx <= 4; dx++)
            {
                bool l = bottom.TryGetValue(x - dx, out int left), r = bottom.TryGetValue(x + dx, out int right);

                if (l || r)
                    return Math.Max(l ? left : int.MinValue, r ? right : int.MinValue);
            }

            return int.MinValue / 2;
        }

        private static void Take(Area area, int x, int y, HashSet<Point> cut)
        {
            if (!Takes(area, x, y))
                return;

            Tile tile = Main.tile[x, y];

            undo?.Keep(x, y);
            tile.ClearTile();
            cut.Add(new Point(x, y));
        }

        /// <summary>
        /// Whether the cut takes this tile: the ground's own, or soft, where the map lets it, with no
        /// water on it or beside it; and not from under sand (or silt, or slush) that is to be kept
        /// where it is, which would fall.
        /// </summary>
        private static bool Takes(Area area, int x, int y)
        {
            if (area.BlockedAt(x, y) || !WorldGen.InWorld(x, y, 50))
                return false;

            Tile tile = Main.tile[x, y];

            if (!tile.HasTile || tile.LiquidAmount > 0 || WetBeside(x, y))
                return false;

            if (!Ground(tile.TileType) && !Soft(tile.TileType))
                return false;

            Tile above = Main.tile[x, y - 1];

            return !(above.HasTile && TileID.Sets.Falling[above.TileType] && area.BlockedAt(x, y - 1));
        }

        /// <summary>
        /// What would fall into the way: sand, silt or slush left over a tile that was cut, with
        /// nothing under it. Made into what does not fall, as the game's own last pass makes such
        /// a tile (Final Cleanup): sand into hardened sand, silt into stone, slush into snow.
        /// </summary>
        private static void Prop(HashSet<Point> cut)
        {
            foreach (Point at in cut)
            {
                Tile above = Main.tile[at.X, at.Y - 1];

                if (!above.HasTile || !TileID.Sets.Falling[above.TileType])
                    continue;

                ushort firm = Firm(above.TileType);

                if (firm != 0)
                    above.TileType = firm;
            }
        }

        private static ushort Firm(ushort type) => type switch
        {
            TileID.Sand => TileID.HardenedSand,
            TileID.Ebonsand => TileID.CorruptHardenedSand,
            TileID.Crimsand => TileID.CrimsonHardenedSand,
            TileID.Pearlsand => TileID.HallowHardenedSand,
            TileID.Silt => TileID.Stone,
            TileID.Slush => TileID.SnowBlock,
            _ => 0,
        };

        private static bool WetBeside(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (Main.tile[x + dx, y + dy].LiquidAmount > 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The level way out of either end of the hall: as tall as its openings, from the brick out
        /// to where the way down arrives and leaves, standing on the ground (made good with stone
        /// where a cave had taken it).
        /// </summary>
        private static void Level(Area area, Rectangle hall, HashSet<Point> cut)
        {
            int floor = hall.Bottom;

            foreach (bool left in new[] { true, false })
            {
                int from = left ? hall.Left - Masonry - Approach : hall.Right + Masonry;
                int to = left ? hall.Left - Masonry - 1 : hall.Right - 1 + Masonry + Approach;

                for (int x = from; x <= to; x++)
                {
                    for (int y = floor - Opening; y < floor; y++)
                        Take(area, x, y, cut);

                    Tile under = Main.tile[x, floor];

                    // Stood on: made good with stone where a cave had taken it, or only a plant or
                    // a cobweb was there (but not where the way down goes on from its end).
                    if (!Solid(x, floor) && !cut.Contains(new Point(x, floor)) && under.LiquidAmount == 0 && !area.BlockedAt(x, floor)
                        && (!under.HasTile || Soft(under.TileType)))
                    {
                        undo?.Keep(x, floor);
                        under.ClearTile();
                        under.HasTile = true;
                        under.TileType = TileID.Stone;
                        under.Slope = SlopeType.Solid;
                        under.IsHalfBlock = false;
                    }
                }
            }
        }

        /// <summary>
        /// What grew on or hung from what was cut, and is left with nothing to grow on or hang from,
        /// goes too, as the game would take it the next time it framed it (but with nothing
        /// dropped): a plant on a tile that went, a vine hanging from one, long moss with no moss
        /// left beside it, a drip with no ceiling left over it.
        /// </summary>
        private static void Loosen(HashSet<Point> cut)
        {
            foreach (Point at in cut)
            {
                // A plant on top of a tile that went.
                Tile above = Main.tile[at.X, at.Y - 1];

                if (above.HasTile && Soft(above.TileType) && !TileID.Sets.IsVine[above.TileType] && above.TileType != TileID.Cobweb)
                    above.ClearTile();

                // A vine hanging from one, all the way down.
                for (int y = at.Y + 1; y < at.Y + 40; y++)
                {
                    Tile hanging = Main.tile[at.X, y];

                    if (!hanging.HasTile || !TileID.Sets.IsVine[hanging.TileType])
                        break;

                    hanging.ClearTile();
                }
            }

            foreach (Point at in cut)
            {
                foreach ((int x, int y) in new[] { (at.X, at.Y - 1), (at.X, at.Y + 1), (at.X - 1, at.Y), (at.X + 1, at.Y) })
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile)
                        continue;

                    if (tile.TileType == TileID.LongMoss && !MossBeside(x, y))
                        tile.ClearTile();
                    else if (Drip(tile.TileType) && !CeilingOver(x, y))
                        tile.ClearTile();
                }
            }
        }

        /// <summary>
        /// Whether long moss here has moss beside it to grow from, as WorldGen.TileFrame asks it
        /// before it kills the tile: under it (not a half block, not sloped on top), over it (not
        /// sloped underneath), or either side.
        /// </summary>
        private static bool MossBeside(int x, int y)
        {
            Tile down = Main.tile[x, y + 1], up = Main.tile[x, y - 1], left = Main.tile[x - 1, y], right = Main.tile[x + 1, y];

            return (down.HasTile && !down.IsHalfBlock && !down.TopSlope && WorldGen.GetTileMossColor(down.TileType) != -1)
                || (up.HasTile && !up.BottomSlope && WorldGen.GetTileMossColor(up.TileType) != -1)
                || (left.HasTile && WorldGen.GetTileMossColor(left.TileType) != -1)
                || (right.HasTile && WorldGen.GetTileMossColor(right.TileType) != -1);
        }

        /// <summary>The tiles drips fall from.</summary>
        private static bool Drip(ushort type) => type == TileID.WaterDrip || type == TileID.LavaDrip || type == TileID.HoneyDrip || type == TileID.SandDrip;

        /// <summary>
        /// Whether a drip here has a ceiling over it, as WorldGen.TileFrame asks it before it kills
        /// the tile: a solid tile, not one that is only solid on top, not sloped underneath.
        /// </summary>
        private static bool CeilingOver(int x, int y)
        {
            Tile up = Main.tile[x, y - 1];

            return up.HasTile && !up.BottomSlope && Main.tileSolid[up.TileType] && !Main.tileSolidTop[up.TileType];
        }

        // ===========================================================================================
        // WALKED
        // ===========================================================================================

        /// <summary>How far one coming down the way may drop at once (the game hurts a fall of more than twenty-five tiles), and jump up (a jump is about six).</summary>
        private const int WalkFall = 24;
        private const int WalkJump = 5;

        /// <summary>How many times at the most the way is made wider where one walking it is stopped; how much wider each time, and for how long a stretch of it.</summary>
        private const int MendsMost = 24;
        private const float MendWider = 1.2f;
        private const int MendBack = 4;
        private const int MendOn = 18;

        /// <summary>
        /// Walks the way (<see cref="Walk"/>); where one walking it is stopped, it is cut wider for a
        /// stretch from the furthest tile of it they came near (as far as the map lets it, as every
        /// cut), and walked again: until they come to the shaft, or <see cref="MendsMost"/> times.
        /// How many times it was made wider; and whether it was walked through.
        /// </summary>
        private static int Mend(Area area, List<Point> way, Point mouth, Point shaft, Rectangle hall, Dictionary<int, int> bottom, HashSet<Point> cut, out bool walked)
        {
            var wider = new int[way.Count];
            int mends = 0;

            while (true)
            {
                if (Walk(way, mouth, shaft, out HashSet<Point> stood))
                {
                    walked = true;
                    return mends;
                }

                if (mends >= MendsMost)
                {
                    walked = false;
                    return mends;
                }

                // The furthest tile of the way that one walking it came near.
                int furthest = 0;

                for (int k = way.Count - 1; k >= 0 && furthest == 0; k--)
                {
                    Point at = way[k];

                    for (int dx = -3; dx <= 3 && furthest == 0; dx++)
                    {
                        for (int dy = -3; dy <= 3; dy++)
                        {
                            if (stood.Contains(new Point(at.X + dx, at.Y + dy)))
                            {
                                furthest = k;
                                break;
                            }
                        }
                    }
                }

                bool any = false;
                Rectangle keep = new Rectangle(hall.Left - Masonry - 2, hall.Top - Masonry - 2, hall.Width + (Masonry + 2) * 2, hall.Height + (Masonry + 2) * 2);

                for (int k = Math.Max(0, furthest - MendBack); k < Math.Min(way.Count, furthest + MendOn); k++)
                {
                    // Not the hall: that is walked through as it is built.
                    if (wider[k] >= 2 || keep.Contains(way[k]))
                        continue;

                    wider[k]++;
                    any = true;

                    var line = new List<Vector2> { way[k].ToVector2() };
                    Cut(area, line, new[] { RadiusMost + MendWider * wider[k] }, hall, bottom, cut);
                }

                mends++;

                if (!any)
                {
                    walked = false;
                    return mends;
                }
            }
        }

        /// <summary>Room for a player, as the game has one, two tiles wide and three tall, with its feet in this row.</summary>
        private static bool BodyAt(int x, int y)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 2; dy++)
                {
                    if (!WorldGen.InWorld(x + dx, y - dy, 2) || Solid(x + dx, y - dy))
                        return false;
                }
            }

            return true;
        }

        /// <summary>Something to stand on (platforms too).</summary>
        private static bool Underfoot(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            return tile.HasTile && Main.tileSolid[tile.TileType];
        }

        private static bool StandsAt(Rectangle box, int x, int y) =>
            box.Contains(x, y) && BodyAt(x, y) && (Underfoot(x, y + 1) || Underfoot(x + 1, y + 1));

        /// <summary>Round about, how far from the way one walking it is looked for: the way is long, and the rock round it full of caves.</summary>
        private const int WalkNear = 24;

        /// <summary>
        /// Walks the way as one coming down it would, on the world as it now is: from the ground at
        /// the mouth (<see cref="Window"/>) to the top of the shaft (within four columns of it, ten
        /// rows over it at the most), as <see cref="Walker"/> walks. True if they come to it; the
        /// places stood on, either way.
        /// </summary>
        private static bool Walk(List<Point> way, Point mouth, Point shaft, out HashSet<Point> stood, int across = 0) =>
            Walker(way, Window(mouth), (x, y) => Math.Abs(x - shaft.X) <= 4 && y >= shaft.Y - 10, across, out stood);

        /// <summary>Where one walking the way may start from, on the ground at its mouth: within four columns of it, eight rows over it to four under it, wherever they can stand.</summary>
        private static List<Point> Window(Point mouth)
        {
            var starts = new List<Point>();

            for (int x = mouth.X - 4; x <= mouth.X + 4; x++)
            {
                for (int y = mouth.Y - 8; y <= mouth.Y + 4; y++)
                    starts.Add(new Point(x, y));
            }

            return starts;
        }

        /// <summary>
        /// Walks as one coming down the way would, on the world as it now is: from those of the
        /// places given that they can stand on, walking, stepping up a tile, jumping
        /// (<see cref="WalkJump"/> up, then three across at the most) and dropping
        /// (<see cref="WalkFall"/> at the most), within the way's box. Round about
        /// (<paramref name="across"/> more than none), within <see cref="WalkNear"/> of the way; a jump
        /// goes as far as that across (past three, only from a jump of two or more), and on, over a
        /// hole, down to whatever is under it, a drop of <see cref="WalkFall"/> at the most from the
        /// top of the jump. True if they come to a place the goal says, standing there or falling
        /// into it. The places stood on, either way.
        /// </summary>
        private static bool Walker(List<Point> way, List<Point> starts, Func<int, int, bool> goal, int across, out HashSet<Point> stood)
        {
            int left = int.MaxValue, right = int.MinValue, top = int.MaxValue, bottom = int.MinValue;

            foreach (Point p in way)
            {
                left = Math.Min(left, p.X);
                right = Math.Max(right, p.X);
                top = Math.Min(top, p.Y);
                bottom = Math.Max(bottom, p.Y);
            }

            var box = new Rectangle(left - 14, top - 30, right - left + 29, bottom - top + 61);
            var seen = new HashSet<Point>();
            var queue = new Queue<Point>();
            stood = seen;

            // Round about, only near the way.
            bool[] near = null;

            if (across > 0)
            {
                near = new bool[box.Width * box.Height];

                foreach (Point p in way)
                    near[(p.Y - box.Top) * box.Width + (p.X - box.Left)] = true;

                near = Spread(near, box.Width, box.Height, WalkNear);
            }

            bool Stands(int x, int y) => StandsAt(box, x, y) && (near == null || near[(y - box.Top) * box.Width + (x - box.Left)]);

            void Try(int x, int y)
            {
                if (Stands(x, y) && seen.Add(new Point(x, y)))
                    queue.Enqueue(new Point(x, y));
            }

            foreach (Point start in starts)
                Try(start.X, start.Y);

            int reach = across > 0 ? across : 3;

            while (queue.Count > 0)
            {
                Point at = queue.Dequeue();
                int x = at.X, y = at.Y;

                if (goal(x, y))
                    return true;

                foreach (int dx in new[] { -1, 1 })
                {
                    int nx = x + dx;

                    if (Stands(nx, y))
                        Try(nx, y);
                    else if (BodyAt(x, y - 1) && Stands(nx, y - 1))
                        Try(nx, y - 1);
                    else if (BodyAt(nx, y))
                    {
                        // Off the edge: down to whatever is under.
                        int ny = y;

                        while (ny - y <= WalkFall && box.Contains(nx, ny + 1) && !Stands(nx, ny) && BodyAt(nx, ny + 1))
                            ny++;

                        if (goal(nx, ny))
                            return true;

                        if (ny - y <= WalkFall)
                            Try(nx, ny);
                    }
                }

                // A jump: straight up as far as there is room for it, then across.
                int up = 0;

                while (up < WalkJump && BodyAt(x, y - up - 1))
                    up++;

                for (int h = 1; h <= up; h++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        if (dx == 0 || (Math.Abs(dx) > 3 && h < 2))
                            continue;

                        bool clear = true;

                        for (int k = 1; k <= Math.Abs(dx) && clear; k++)
                            clear = BodyAt(x + Math.Sign(dx) * k, y - h);

                        if (!clear)
                            continue;

                        Try(x + dx, y - h);

                        if (across == 0 || Stands(x + dx, y - h))
                            continue;

                        // Round about: on over a hole, and down to whatever is under it.
                        int nx = x + dx, ny = y - h;

                        while (ny - (y - h) <= WalkFall && box.Contains(nx, ny + 1) && !Stands(nx, ny) && BodyAt(nx, ny + 1))
                            ny++;

                        if (goal(nx, ny))
                            return true;

                        if (ny - (y - h) <= WalkFall)
                            Try(nx, ny);
                    }
                }
            }

            return false;
        }

        // ===========================================================================================
        // ROUND ABOUT
        // ===========================================================================================

        /// <summary>Round about, what a step costs for each tile of it, beside a step along and a step up (<see cref="LevelDear"/>, <see cref="UpDear"/>): a step down a tile, and down two.</summary>
        private const float DownDear = 1.4f;
        private const float DownTwoDear = 2.2f;

        /// <summary>Round about, what a jump costs before its height and how far across it goes; and each tile dug (so that a cave already there is liked better, a little, where it leads the same way).</summary>
        private const float JumpDear = 3f;
        private const float DigDear = 0.12f;

        /// <summary>How much of what is left of the way, as the crow flies, a search round about counts on its costing at the least; and how many places, all told, the searches round about may look at.</summary>
        private const float GuessShare = 0.8f;
        private const long RoundLooked = 30_000_000;

        /// <summary>Round about, how much further out than the way down's own cut the way is made wider round each place of it (<see cref="Widen"/>), from a middle this far over the feet there: the way down's line is two or three tiles over its floor, and this is cut from the floor up.</summary>
        private const float WidenMore = 0.8f;
        private const float WidenOver = 2f;

        /// <summary>
        /// Round about the Dungeon, by request (see the notes over the pass): the map again, keeping
        /// <see cref="RoundKeep"/> off the Dungeon and reaching round it (and the mouths' window and the
        /// shaft) by <see cref="RoundMargin"/>; the mouths in the same window and the halls under them
        /// as before; and for each, the way found as one walking it would go (<see cref="Burrow"/>):
        /// from the ground at the mouth to the hall's near end, and from its far end to the shaft. The
        /// first found is made (<see cref="RoundMade"/>); one that cannot be walked down once made is
        /// put back as it was, and the next tried. True if one was made.
        ///
        /// INTO THE HOLLOW FROM THE SIDE, by request: "the access to the inner circle of the ball dont
        /// have to be from top down, it can be from any angle". Given why no way round into the top of
        /// the shaft was found (<paramref name="notShaft"/>: in Flacid the Dungeon's lowest halls lie
        /// over it and beside it), all of it again, the way from the hall's far end going on as one
        /// walking it would, down and round, through the ball's shell from whatever side is nearest
        /// (dug as it digs any ground), and into its hollow, to stand there (<see cref="InHollow"/>):
        /// walked into, not fallen into, so that it can be walked from the mouth to the hollow's
        /// floor. On its map the ball is not kept off; and what the Dungeon left in the hollow when
        /// the ball was made round it (its walls, with nothing of its brick: in Flacid all of it within
        /// 200 tiles of the middle) is not the Dungeon: never cut, gone round as a pot is, not kept
        /// away from (<see cref="Map"/>).
        /// </summary>
        private static bool RoundAbout(GenerationProgress progress, Point ball, int reach, Point shaft, int beach, int from, int to, int high, UnifiedRandom rand, string first, string notShaft, out string why)
        {
            bool intoHollow = notShaft != null;
            (int left, int right) = WorldMirror.Span(from, to);
            int near = Math.Min(left, shaft.X);
            int far = Math.Max(right, shaft.X);
            int y0 = Math.Max(60, high - 40);
            int y1 = Math.Min(Main.UnderworldLayer - 60, shaft.Y + 40);
            bool[] hollow = null;
            Rectangle hollowBox = Rectangle.Empty;

            if (intoHollow)
            {
                hollow = Hollowed(ball, out hollowBox);

                if (hollow == null)
                {
                    why = $"the ball's hollow is not open at its middle at {ball.X}, {ball.Y}, nor over it";
                    return false;
                }

                // The whole ball and round it, and down to under where one comes to stand in it.
                near = Math.Min(near, ball.X - reach - RoundBeside);
                far = Math.Max(far, ball.X + reach + RoundBeside);
                y1 = Math.Min(Main.UnderworldLayer - 60, Math.Max(y1, ball.Y + BankLow + RoundBeside));
            }

            Rectangle dungeon = DungeonBox(Math.Max(60, near - RoundLook), Math.Min(Main.maxTilesX - 61, far + RoundLook), y1);

            if (dungeon.Width > 0)
            {
                near = Math.Min(near, dungeon.Left);
                far = Math.Max(far, dungeon.Right - 1);
            }

            Area area = Map(Math.Max(60, near - RoundMargin), y0, Math.Min(Main.maxTilesX - 61, far + RoundMargin), y1, ball, reach, beach, RoundKeep, intoHollow);

            if (intoHollow)
                KeepOff(area, hollow, hollowBox, ball);

            List<Point> mouths = Mouths(area, from, to, rand);

            if (mouths.Count == 0)
            {
                why = $"nowhere between plan {from} and plan {to} could the mouth be opened without undermining something";
                return false;
            }

            var burrow = new Burrow(area, rand.Next());
            why = "no rock solid and clear enough for the hall under any of them";
            int tried = 0;
            bool InShaftHere(int x, int y) => InShaft(shaft, x, y);
            bool InHollowHere(int x, int y) => InHollow(ball, hollow, hollowBox, x, y);
            bool HollowHere(int x, int y) => HollowAt(hollow, hollowBox, x, y);
            Func<int, int, bool> goal = intoHollow ? InHollowHere : InShaftHere;
            Rectangle aim = intoHollow ? new Rectangle(ball.X - HollowReach, ball.Y + BankHigh, HollowReach * 2 + 1, BankLow - BankHigh + 1) : Aim(shaft);

            // Where the way must end, and every place with room for the body joined to it, side by side,
            // as the world is (a hall shuts more, never less): where none of a hall's far end is one of
            // them, no way can be found from there (every move goes so), and none is looked for. In
            // Flacid the shaft's top is shut in so by the Dungeon round it and the ball's reach under it.
            var ends = new List<Point>();
            Rectangle endBox = intoHollow ? aim : new Rectangle(shaft.X - 1, shaft.Y, 2, Math.Max(0, area.Y0 + area.H - shaft.Y));

            for (int y = endBox.Top; y < endBox.Bottom; y++)
            {
                for (int x = endBox.Left; x < endBox.Right; x++)
                {
                    if (goal(x, y))
                        ends.Add(new Point(x, y));
                }
            }

            bool[] joined = burrow.Joined(ends, JoinedMost, out int joinedCount);

            foreach (Point mouth in mouths)
            {
                List<Point> starts = burrow.Starts(mouth);

                if (starts.Count == 0)
                {
                    why = $"nowhere to stand on the ground at the mouth at {mouth.X}, {mouth.Y}";
                    continue;
                }

                int side = shaft.X >= mouth.X ? 1 : -1;

                foreach (Rectangle hall in Halls(area, mouth, shaft, side))
                {
                    if (burrow.Looked > RoundLooked)
                    {
                        why = $"{Many(mouths.Count, "mouth")} and {Many(tried, "way")} tried, the last: {why}; and then {burrow.Looked} places looked at, as many as may be";
                        return false;
                    }

                    burrow.Hall(hall, side);

                    if (joined != null && !burrow.ExitStarts().Exists(p => burrow.Of(joined, p)))
                    {
                        why = intoHollow
                            ? $"no way round from the hall's far end at {burrow.ExitAim.X}, {burrow.ExitAim.Y} into the ball's hollow: where one may stand in it is shut in by what the way keeps off ({Many(joinedCount, "place")} with room for the body joined to it, that far end not one of them)"
                            : $"no way round from the hall's far end at {burrow.ExitAim.X}, {burrow.ExitAim.Y} to the shaft's top at {shaft.X}, {shaft.Y}: it is shut in by what the way keeps off ({Many(joinedCount, "place")} with room for the body joined to it, that far end not one of them)";
                        continue;
                    }

                    List<Place> upper = burrow.Search(starts, burrow.InEntry, Aim(burrow.EntryAim));

                    if (upper == null)
                    {
                        why = $"no way round from the mouth at {mouth.X}, {mouth.Y} to the hall's near end at {burrow.EntryAim.X}, {burrow.EntryAim.Y}";
                        continue;
                    }

                    List<Place> lower = burrow.Search(burrow.ExitStarts(), goal, aim);

                    if (lower == null)
                    {
                        why = intoHollow
                            ? $"no way round from the hall's far end at {burrow.ExitAim.X}, {burrow.ExitAim.Y} into the ball's hollow"
                            : $"no way round from the hall's far end at {burrow.ExitAim.X}, {burrow.ExitAim.Y} to the shaft's top at {shaft.X}, {shaft.Y}";
                        continue;
                    }

                    tried++;

                    if (RoundMade(area, burrow, ball, reach, shaft, hall, starts, upper, lower, goal, aim, intoHollow ? HollowHere : null, rand, tried, first, notShaft, ref why))
                    {
                        progress?.Set(1f);
                        return true;
                    }

                    // Put back as it was: looked at again as it is.
                    burrow.Forget();
                }
            }

            why = $"{Many(mouths.Count, "mouth")} and {Many(tried, "way")} tried, the last: {why}";
            return false;
        }

        /// <summary>A place to aim a search round about at, as a box a tile across.</summary>
        private static Rectangle Aim(Point at) => new Rectangle(at.X, at.Y, 1, 1);

        /// <summary>Into the hollow from the side: how far beside the ball, and under where one comes to stand in it, the map reaches.</summary>
        private const int RoundBeside = 60;

        /// <summary>
        /// The ball's hollow as it now is: every open tile within <see cref="HollowReach"/> of its
        /// middle that is joined to the middle (the chamber, and the alcoves, the flues and the foot
        /// of the shaft off it), over the box given back, row by row. Null if neither the middle nor
        /// anything within forty rows over it is open.
        /// </summary>
        private static bool[] Hollowed(Point ball, out Rectangle box)
        {
            int r = HollowReach;
            box = new Rectangle(ball.X - r, ball.Y - r, r * 2 + 1, r * 2 + 1);
            int sy = ball.Y;

            while (sy > ball.Y - 40 && WorldGen.InWorld(ball.X, sy, 20) && Solid(ball.X, sy))
                sy--;

            if (!WorldGen.InWorld(ball.X, sy, 20) || Solid(ball.X, sy))
                return null;

            var open = new bool[box.Width * box.Height];
            var queue = new Queue<Point>();
            long most = (long)r * r;

            open[(sy - box.Y) * box.Width + (ball.X - box.X)] = true;
            queue.Enqueue(new Point(ball.X, sy));

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();

                for (int k = 0; k < 4; k++)
                {
                    int nx = p.X + (k == 0 ? -1 : k == 1 ? 1 : 0);
                    int ny = p.Y + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    long dx = nx - ball.X, dy = ny - ball.Y;

                    if (dx * dx + dy * dy > most || !WorldGen.InWorld(nx, ny, 20))
                        continue;

                    int i = (ny - box.Y) * box.Width + (nx - box.X);

                    if (open[i] || Solid(nx, ny))
                        continue;

                    open[i] = true;
                    queue.Enqueue(new Point(nx, ny));
                }
            }

            return open;
        }

        /// <summary>
        /// Into the hollow from the side: the hollow over where one may stand in it (its open tiles
        /// higher than <see cref="BankHigh"/> under its middle), and everything within
        /// <see cref="HollowKeep"/> of them, kept off, as the map keeps what it may not come near: the
        /// way comes into the hollow only low, where one can stand in it, never along its side nor
        /// over it; its side is never cut open over the bank.
        /// </summary>
        private static void KeepOff(Area area, bool[] hollow, Rectangle box, Point ball)
        {
            var over = new bool[area.W * area.H];

            for (int y = box.Top; y < Math.Min(box.Bottom, ball.Y + BankHigh); y++)
            {
                for (int x = box.Left; x < box.Right; x++)
                {
                    if (area.In(x, y) && HollowAt(hollow, box, x, y))
                        over[area.I(x, y)] = true;
                }
            }

            bool[] kept = Dilate(over, area, HollowKeep);
            Or(area.Firm, kept);
            Or(area.Blocked, kept);

            for (int i = 0; i < kept.Length; i++)
            {
                if (kept[i])
                    area.Free[i] = false;
            }
        }

        /// <summary>Whether a tile is one of the ball's hollow's (<see cref="Hollowed"/>).</summary>
        private static bool HollowAt(bool[] hollow, Rectangle box, int x, int y) => box.Contains(x, y) && hollow[(y - box.Y) * box.Width + (x - box.X)];

        /// <summary>
        /// One walking the way into the ball's hollow from the side has come into it: their feet in
        /// its open tiles, standing on something, as low as its bank's top can be and no lower than
        /// that by much (<see cref="BankHigh"/>, <see cref="BankLow"/> under its middle): on the bank,
        /// or on what is set in it.
        /// </summary>
        private static bool InHollow(Point ball, bool[] hollow, Rectangle box, int x, int y) =>
            y >= ball.Y + BankHigh && y <= ball.Y + BankLow && HollowAt(hollow, box, x, y) && HollowAt(hollow, box, x + 1, y) && (Solid(x, y + 1) || Solid(x + 1, y + 1));

        /// <summary>
        /// The way round about, made: cut as narrow as one walking it (the room for their body along
        /// every move), the hall built and the door stood, then made wider (<see cref="Widen"/>); and
        /// walked, from the ground at the mouth into the hall and from the hall into the shaft (or,
        /// given the hollow's tiles, <paramref name="hollowAt"/>, into the ball's hollow from the side:
        /// where <paramref name="goal"/> says, as the search went, aimed at <paramref name="aim"/>).
        /// Where one walking a half is stopped, that half is found again from its start, on the world
        /// as it now is, never taking what they stood on (and cut, and made wider), at most
        /// <see cref="RoundMends"/> times in all; then walked whole. If it cannot be, everything it
        /// touched is put back as it was. The rest as a way down is made: what would fall made fast,
        /// what would hang left go, the walls, the torches along it, the framing.
        /// </summary>
        private static bool RoundMade(Area area, Burrow burrow, Point ball, int reach, Point shaft, Rectangle hall, List<Point> starts, List<Place> upper, List<Place> lower,
            Func<int, int, bool> goal, Rectangle aim, Func<int, int, bool> hollowAt, UnifiedRandom rand, int tried, string first, string notShaft, ref string why)
        {
            bool intoHollow = hollowAt != null;
            string end = intoHollow ? "the ball's hollow" : "the shaft";
            int floor = hall.Bottom;
            double phase = rand.NextDouble() * 100.0;
            var cut = new HashSet<Point>();
            var keep = new HashSet<Point>();
            undo = new Undo();

            burrow.Dig(upper, cut);
            burrow.Dig(lower, cut);
            Level(area, hall, cut);
            Build(hall);
            Point door = Door(hall);

            Floors(keep, upper);
            Floors(keep, lower);
            Widen(area, burrow, upper, keep, cut, phase);
            Widen(area, burrow, lower, keep, cut, phase);

            // The level way, from end to end, where one walking it stands.
            var level = new List<Point>();

            for (int x = hall.Left - Masonry - Approach; x <= hall.Right - 1 + Masonry + Approach; x++)
                level.Add(new Point(x, floor - 1));

            var inHall = new List<Point>();

            for (int x = hall.Left; x <= hall.Right - 2; x++)
                inHall.Add(new Point(x, floor - 1));

            bool InHall(int x, int y) => y == floor - 1 && x >= hall.Left && x <= hall.Right - 2;

            // From the ground at the mouth into the hall.
            var alongUpper = new List<Point>(Feet(upper));
            alongUpper.AddRange(level);
            int mends = 0;
            HashSet<Point> stood;

            while (!Walker(alongUpper, Window(new Point(upper[0].X, upper[0].Y)), InHall, RoundAcross, out stood))
            {
                if (mends >= RoundMends || (upper = Again(area, burrow, keep, cut, stood, starts, burrow.InEntry, Aim(burrow.EntryAim), phase)) == null)
                    return Unmade(hall, mends, "from the ground at the mouth into the hall", ref why);

                mends++;
                alongUpper.AddRange(Feet(upper));
            }

            // What they stood on coming down to it is not taken, from here on.
            Kept(burrow, keep, stood);

            // From the hall into the shaft (or the ball's hollow).
            var alongLower = new List<Point>(level);
            alongLower.AddRange(Feet(lower));

            while (!Walker(alongLower, inHall, goal, RoundAcross, out stood))
            {
                if (mends >= RoundMends || (lower = Again(area, burrow, keep, cut, stood, burrow.ExitStarts(), goal, aim, phase)) == null)
                    return Unmade(hall, mends, $"from the hall into {end}", ref why);

                mends++;
                alongLower.AddRange(Feet(lower));
            }

            // And whole.
            var along = new List<Point>(alongUpper);
            along.AddRange(alongLower);

            if (!Walker(along, Window(new Point(upper[0].X, upper[0].Y)), goal, RoundAcross, out _))
                return Unmade(hall, mends, $"whole, from the ground at the mouth into {end}", ref why);

            undo = null;

            Prop(cut);
            Loosen(cut);
            Walls(cut, ball, reach);

            List<Point> upperWay = Feet(upper), lowerWay = Feet(lower);
            int torches = Torches(upperWay.ConvertAll(p => p.ToVector2()), ball, reach, hall) + Torches(lowerWay.ConvertAll(p => p.ToVector2()), ball, reach, hall);
            Frame(cut, hall);

            if (WorldGen.gen)
                GenVars.structures?.AddProtectedStructure(new Rectangle(hall.Left - Masonry - Approach, hall.Top - Masonry, hall.Width + (Masonry + Approach) * 2, hall.Height + Masonry * 2), 4);

            // The way, tile by tile: down to the hall's near end, along it, and down from its far end
            // to where it comes into the shaft, and up the shaft to its top; or into the ball's hollow,
            // to where it stands there.
            var way = new List<Point>();

            foreach (Point p in upperWay)
                Step(way, p);

            if (intoHollow)
            {
                foreach (Point p in lowerWay)
                    Step(way, p);

                Point into = lowerWay.Find(p => hollowAt(p.X, p.Y));

                LastInto = into != Point.Zero ? into : lowerWay[^1];
            }
            else
            {
                foreach (Point p in lowerWay)
                {
                    Step(way, p);

                    if (p.Y >= shaft.Y - 1 && Math.Abs(p.X - shaft.X) <= 1)
                        break;
                }

                Step(way, shaft);
                LastInto = Point.Zero;
            }

            WayDownState.Set(way[0], door);
            LastWay = way;
            LastKeep = RoundKeep;

            string found = $"{(tried > 1 ? $"; {Many(tried - 1, "way")} before it put back, that could not be walked down" : "")}";

            if (intoHollow)
            {
                double dx = LastInto.X - ball.X, dy = LastInto.Y - ball.Y;

                Say($"no way down keeping {DungeonKeep} off the Dungeon ({first}); nor round about it into the top of the ball's shaft ({notShaft}); made round about it into the ball's hollow " +
                    $"from the side, {RoundKeep} off it at the least: the mouth at {way[0].X}, {way[0].Y}; the hall at {hall.X}-{hall.Right - 1}, floor {floor}, the door at {door.X}, {door.Y}; " +
                    $"into the hollow at {LastInto.X}, {LastInto.Y}, {(int)Math.Round(Math.Sqrt(dx * dx + dy * dy))} tiles from its middle; {Many(way.Count, "tile")} of way, {Many(cut.Count, "tile")} cut, " +
                    $"{Many(torches, "torch", "torches")}; walked from the mouth into the hollow ({Many(mends, "time")} found again where one walking it was stopped{found}).");
            }
            else
            {
                Say($"no way down keeping {DungeonKeep} off the Dungeon ({first}); made round about it, {RoundKeep} off it at the least: the mouth at {way[0].X}, {way[0].Y}; " +
                    $"the hall at {hall.X}-{hall.Right - 1}, floor {floor}, the door at {door.X}, {door.Y}; the shaft's top at {shaft.X}, {shaft.Y}; {Many(way.Count, "tile")} of way, " +
                    $"{Many(cut.Count, "tile")} cut, {Many(torches, "torch", "torches")}; walked from the mouth into the shaft ({Many(mends, "time")} found again where one walking it was stopped" +
                    $"{found}).");
            }

            return true;
        }

        /// <summary>Everything the way being made touched, put back as it was; and why.</summary>
        private static bool Unmade(Rectangle hall, int mends, string half, ref string why)
        {
            undo.Restore();
            undo = null;
            why = $"the way round by the hall at {hall.X}, {hall.Bottom} could not be walked {half}, even found again {Many(mends, "time")}";
            return false;
        }

        /// <summary>
        /// Where one walking a half was stopped: the half found again from its start, on the world as
        /// it now is, never taking what they stood on (nor, in making it wider, from here on); then
        /// cut, and made wider. Null if there is no way now.
        /// </summary>
        private static List<Place> Again(Area area, Burrow burrow, HashSet<Point> keep, HashSet<Point> cut, HashSet<Point> stood, List<Point> starts, Func<int, int, bool> goal, Rectangle aim, double phase)
        {
            Kept(burrow, keep, stood);
            burrow.Refresh();

            List<Place> path = burrow.Search(starts, goal, aim);

            if (path == null)
                return null;

            burrow.Dig(path, cut);
            Floors(keep, path);
            Widen(area, burrow, path, keep, cut, phase);
            return path;
        }

        /// <summary>What these places stand on (under either foot), not to be taken from here on: not dug by a way found again, nor taken in making it wider.</summary>
        private static void Kept(Burrow burrow, HashSet<Point> keep, HashSet<Point> stood)
        {
            var under = new List<Point>(stood.Count * 2);

            foreach (Point p in stood)
            {
                under.Add(new Point(p.X, p.Y + 1));
                under.Add(new Point(p.X + 1, p.Y + 1));
            }

            burrow.Protect(under);

            foreach (Point p in under)
                keep.Add(p);
        }

        /// <summary>What one walking the way stands on at each place of it (under either foot), and the row under that: never taken in making it wider.</summary>
        private static void Floors(HashSet<Point> keep, List<Place> path)
        {
            foreach (Place p in path)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    for (int dy = 1; dy <= 2; dy++)
                        keep.Add(new Point(p.X + dx, p.Y + dy));
                }
            }
        }

        /// <summary>
        /// Round about, the way, dug only as wide as one walking it, made wider: round each place of
        /// it (round their body, <see cref="WidenOver"/> over their feet), as far as the cut of a way
        /// down reaches and <see cref="WidenMore"/> further, lumps and all (see <see cref="Cut"/>), but
        /// never lower than their feet there, and never what they or anyone walking it stands on
        /// (<paramref name="keep"/>), nor the hall's brick and its level way's floor. As every cut,
        /// only the ground's own tiles, and only where the map lets it.
        /// </summary>
        private static void Widen(Area area, Burrow burrow, List<Place> path, HashSet<Point> keep, HashSet<Point> cut, double phase)
        {
            for (int k = 0; k < path.Count; k++)
            {
                Place p = path[k];
                double size = 0.5 + 0.3 * Math.Sin(k * 0.17 + phase) + 0.2 * Math.Sin(k * 0.05 + phase * 0.7);
                float r = MathHelper.Lerp(RadiusLeast, RadiusMost, (float)Math.Clamp(size, 0.0, 1.0)) + WidenMore;
                var at = new Vector2(p.X + 0.5f, p.Y - WidenOver);
                int most = (int)Math.Ceiling(r + BulgeMost + Rough * 0.5f + 0.5f);

                for (int x = p.X - most; x <= p.X + 1 + most; x++)
                {
                    for (int y = p.Y - (int)WidenOver - most; y <= p.Y; y++)
                    {
                        float edge = r + Lumps(x, y, BulgeSeed) * BulgeMost + (Lumps(x, y, BulgeSeed + 1, 3) - 0.5f) * Rough + Layout.Grain(x, y, 1) * 0.5f;

                        if (Vector2.DistanceSquared(new Vector2(x, y), at) > edge * edge)
                            continue;

                        if (keep.Contains(new Point(x, y)) || !burrow.MayDig(x, y))
                            continue;

                        Take(area, x, y, cut);
                    }
                }
            }
        }

        /// <summary>The places one goes through along a way found round about, tile by tile (where their feet are): up and across a jump, along and down a drop.</summary>
        private static List<Point> Feet(List<Place> path)
        {
            var way = new List<Point>();

            for (int k = 0; k < path.Count; k++)
            {
                Place b = path[k];

                if (k > 0)
                {
                    Place a = path[k - 1];

                    if (b.How == Go.Drop)
                        Step(way, new Point(b.X, a.Y));
                    else if (b.How == Go.JumpOnto || b.How == Go.JumpOver)
                    {
                        Step(way, new Point(a.X, a.Y - b.Lift));
                        Step(way, new Point(b.X, a.Y - b.Lift));
                    }
                }

                Step(way, new Point(b.X, b.Y));
            }

            return way;
        }

        /// <summary>One walking the way round about has come into the shaft: their body over its middle column, their feet as low as its top or lower.</summary>
        private static bool InShaft(Point shaft, int x, int y) => x >= shaft.X - 1 && x <= shaft.X && y >= shaft.Y;

        /// <summary>The box round everything of the Dungeon's (its brick, its walls) in these columns, from the sky down to this row; empty if there is none.</summary>
        private static Rectangle DungeonBox(int x0, int x1, int y1)
        {
            int left = int.MaxValue, right = int.MinValue, top = int.MaxValue, bottom = int.MinValue;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = 20; y <= y1; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if ((tile.HasTile && Dungeon(tile.TileType)) || (tile.WallType > 0 && Main.wallDungeon[tile.WallType]))
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            return left > right ? Rectangle.Empty : new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }

        /// <summary>How one walking the way round about came to a place from the one before it.</summary>
        private enum Go : byte
        {
            /// <summary>Started there.</summary>
            Start,

            /// <summary>A step along.</summary>
            Along,

            /// <summary>A step up a tile.</summary>
            Up,

            /// <summary>A step down a tile.</summary>
            Down,

            /// <summary>A step down two.</summary>
            DownTwo,

            /// <summary>Off an edge, and down through the air.</summary>
            Drop,

            /// <summary>A jump straight up, then across, onto something.</summary>
            JumpOnto,

            /// <summary>A jump straight up, then across, on over a hole, and down through the air.</summary>
            JumpOver,
        }

        /// <summary>A place one walking the way round about stands (their feet there); how they came to it; and how high they jumped to come to it.</summary>
        private readonly struct Place
        {
            public readonly int X, Y;
            public readonly Go How;
            public readonly int Lift;

            public Place(int x, int y, Go how, int lift)
            {
                X = x;
                Y = y;
                How = how;
                Lift = lift;
            }
        }

        /// <summary>
        /// Round about, the way found as one walking it would go, and digging it as they went (A*,
        /// the cheapest first): from where they stand, a step along, up a tile, or down one or two;
        /// off an edge and down to whatever is under it (<see cref="WalkFall"/> at the most); a jump
        /// straight up (<see cref="WalkJump"/> at the most) and across, onto something (three across at
        /// the most), or on over a hole (<see cref="RoundAcross"/> at the most, past three only from a
        /// jump of two or more) and down to whatever is under it. Where one stands is where their feet
        /// are: the body is two tiles wide and three tall over them, as the game has a player. Through
        /// the air where there is air; through the ground's own tiles where there is not, digging what
        /// <see cref="Take"/> would take (each tile dug costs a little); never into anything the map
        /// keeps it from; never standing on what the move itself digs away. Turning back the way it
        /// came costs more (<see cref="TurnCost"/>), so that it runs one way and then the other. The
        /// tiles as they are when it is told to look again (<see cref="Refresh"/>), and the hall being
        /// tried as it will be built: closed, but for its level ways, whose floors are stood on and
        /// never dug.
        /// </summary>
        private sealed class Burrow
        {
            /// <summary>What a tile is: solid; kept (never come into, nor dug, but it may be stood on); one that may be dug.</summary>
            private const byte HardBit = 1, KeptBit = 2, DigBit = 4;

            private static readonly int[] Sides = { -1, 1 };

            private readonly Area area;
            private readonly int x0, y0, w, h;
            private readonly int seed;
            private readonly byte[] tiles;

            /// <summary>Of each place (feet there): room for the body, once what is in the way is dug; and how many of its tiles are solid.</summary>
            private readonly bool[] room;
            private readonly byte[] digs;

            // The search's own: what each place cost to come to, from where and how; whether it is done
            // with; and the places still to look at, cheapest (with what is left, guessed) first.
            private readonly float[] paid, rank;
            private readonly int[] came, heap, slot;
            private readonly byte[] how, rise;
            private readonly bool[] shut;
            private int count;

            /// <summary>Tiles never dug, whatever they are: what one walking the way stood on.</summary>
            private readonly HashSet<Point> keep = new HashSet<Point>();

            // The hall being tried: the box kept round it, its level ways (inside, from the floor up to
            // the openings' tops), and its floor.
            private bool hasHall;
            private Rectangle box, entry, exit;
            private int floor;

            /// <summary>How many places the searches have looked at, all told.</summary>
            public long Looked { get; private set; }

            /// <summary>The far end of the near level way of the hall being tried, and of the far one: where the way comes to it, and where it goes on from.</summary>
            public Point EntryAim { get; private set; }

            public Point ExitAim { get; private set; }

            public Burrow(Area area, int seed)
            {
                this.area = area;
                this.seed = seed;
                x0 = area.X0;
                y0 = area.Y0;
                w = area.W;
                h = area.H;

                int n = w * h;
                tiles = new byte[n];
                room = new bool[n];
                digs = new byte[n];
                paid = new float[n];
                rank = new float[n];
                came = new int[n];
                heap = new int[n];
                slot = new int[n];
                how = new byte[n];
                rise = new byte[n];
                shut = new bool[n];

                Refresh();
            }

            private bool InArea(int x, int y) => x >= x0 && y >= y0 && x < x0 + w && y < y0 + h;

            private int I(int x, int y) => (y - y0) * w + (x - x0);

            private bool Room(int x, int y) => InArea(x, y) && room[I(x, y)];

            private bool Hard(int x, int y) => InArea(x, y) && (tiles[I(x, y)] & HardBit) != 0;

            private bool IsKept(int x, int y) => !InArea(x, y) || (tiles[I(x, y)] & KeptBit) != 0;

            /// <summary>Whether a tile may be dug, as far as the map and the hall being tried go (never anything kept, the hall's brick, its level way's floor).</summary>
            public bool MayDig(int x, int y) => !IsKept(x, y);

            /// <summary>Something to stand on, with the feet here: a solid tile under either foot.</summary>
            private bool Stands(int x, int y) => Hard(x, y + 1) || Hard(x + 1, y + 1);

            /// <summary>Something to stand on, with the feet here, that is not one of the tiles dug by the move (column <paramref name="bx"/> and the next, rows <paramref name="top"/> to <paramref name="bottom"/>).</summary>
            private bool StandsBut(int x, int y, int bx, int top, int bottom) =>
                (Hard(x, y + 1) && !Within(x, y + 1, bx, top, bottom)) || (Hard(x + 1, y + 1) && !Within(x + 1, y + 1, bx, top, bottom));

            private static bool Within(int x, int y, int bx, int top, int bottom) => x >= bx && x <= bx + 1 && y >= top && y <= bottom;

            /// <summary>Everything read off the world again, as it now is.</summary>
            public void Refresh() => Refresh(new Rectangle(x0, y0, w, h));

            private void Refresh(Rectangle r)
            {
                int xa = Math.Max(r.Left, x0), xb = Math.Min(r.Right, x0 + w) - 1;
                int ya = Math.Max(r.Top, y0), yb = Math.Min(r.Bottom, y0 + h) - 1;

                for (int y = ya; y <= yb; y++)
                {
                    for (int x = xa; x <= xb; x++)
                        tiles[I(x, y)] = Overlay(x, y, Read(x, y));
                }

                foreach (Point at in keep)
                {
                    if (at.X >= xa && at.X <= xb && at.Y >= ya && at.Y <= yb)
                        tiles[I(at.X, at.Y)] &= unchecked((byte)~DigBit);
                }

                Derive(xa - 1, xb, ya, yb + 2);
            }

            private byte Read(int x, int y)
            {
                Tile tile = Main.tile[x, y];
                byte k = 0;

                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    k |= HardBit;

                if (area.Firm[area.I(x, y)])
                    k |= KeptBit;
                else if (Takes(area, x, y))
                    k |= DigBit;

                return k;
            }

            /// <summary>The hall being tried, as it will be: shut, and the rock round it as far as the way down keeps from it; but its level ways as they are (they are cut to it), and their floors stood on, never dug.</summary>
            private byte Overlay(int x, int y, byte k)
            {
                if (!hasHall)
                    return k;

                bool level = (x >= entry.Left && x < entry.Right) || (x >= exit.Left && x < exit.Right);

                if (level && y >= floor - Opening && y < floor)
                    return k;

                if (level && y >= floor && y < floor + Masonry)
                    return (byte)(KeptBit | (y == floor ? (FloorWillBe(x) ? HardBit : 0) : k & HardBit));

                if (box.Contains(x, y))
                    return (byte)((k & HardBit) | KeptBit);

                return k;
            }

            /// <summary>Whether the level way's floor will be solid here once it is made (<see cref="Level"/>: solid, or made good with stone).</summary>
            private bool FloorWillBe(int x)
            {
                Tile under = Main.tile[x, floor];

                if (under.HasTile && Main.tileSolid[under.TileType] && !Main.tileSolidTop[under.TileType])
                    return true;

                return under.LiquidAmount == 0 && !area.BlockedAt(x, floor) && (!under.HasTile || Soft(under.TileType));
            }

            /// <summary>Of each place whose body is within these columns and rows (feet there): room for it, and how much of it is solid.</summary>
            private void Derive(int xa, int xb, int ya, int yb)
            {
                xa = Math.Max(xa, x0);
                xb = Math.Min(xb, x0 + w - 2);
                ya = Math.Max(ya, y0 + 2);
                yb = Math.Min(yb, y0 + h - 1);

                for (int y = ya; y <= yb; y++)
                {
                    for (int x = xa; x <= xb; x++)
                    {
                        int i = I(x, y);
                        bool fits = true;
                        int solid = 0;

                        for (int dy = 0; dy <= 2; dy++)
                        {
                            for (int dx = 0; dx <= 1; dx++)
                            {
                                byte k = tiles[i - dy * w + dx];

                                if ((k & KeptBit) != 0)
                                    fits = false;
                                else if ((k & HardBit) != 0)
                                {
                                    solid++;

                                    if ((k & DigBit) == 0)
                                        fits = false;
                                }
                            }
                        }

                        room[i] = fits;
                        digs[i] = (byte)solid;
                    }
                }
            }

            /// <summary>Tiles never to be dug from here on (the next time it looks again).</summary>
            public void Protect(IEnumerable<Point> tiles)
            {
                foreach (Point at in tiles)
                    keep.Add(at);
            }

            /// <summary>Nothing kept from being dug any more; and everything read off the world again (as it was put back).</summary>
            public void Forget()
            {
                keep.Clear();
                Refresh();
            }

            /// <summary>The hall to try: shut, its level ways open, the way to come to the near one and go on from the far one.</summary>
            public void Hall(Rectangle hall, int side)
            {
                Rectangle was = hasHall ? Changed() : Rectangle.Empty;

                hasHall = true;
                floor = hall.Bottom;

                var left = new Rectangle(hall.Left - Masonry - Approach, floor - Opening, Approach, Opening);
                var right = new Rectangle(hall.Right + Masonry, floor - Opening, Approach, Opening);

                entry = side > 0 ? left : right;
                exit = side > 0 ? right : left;
                box = new Rectangle(hall.Left - Masonry - HallKeep, hall.Top - Masonry - HallKeep, hall.Width + (Masonry + HallKeep) * 2, hall.Height + (Masonry + HallKeep) * 2);
                EntryAim = new Point(side > 0 ? entry.Left : entry.Right - 2, floor - 1);
                ExitAim = new Point(side > 0 ? exit.Right - 2 : exit.Left, floor - 1);

                if (was.Width > 0)
                    Refresh(was);

                Refresh(Changed());
            }

            /// <summary>Every tile the hall being tried makes other than it is: the box round it, and its level ways and their floors.</summary>
            private Rectangle Changed() =>
                Rectangle.Union(box, Rectangle.Union(new Rectangle(entry.X, entry.Y, entry.Width, Opening + Masonry), new Rectangle(exit.X, exit.Y, exit.Width, Opening + Masonry)));

            /// <summary>Come into the hall's near level way: standing on its floor, the body all in it.</summary>
            public bool InEntry(int x, int y) => y == floor - 1 && x >= entry.Left && x <= entry.Right - 2;

            /// <summary>Where one walking it may go on from, in the hall's far level way.</summary>
            public List<Point> ExitStarts()
            {
                var starts = new List<Point>();

                for (int x = exit.Left; x <= exit.Right - 2; x++)
                    starts.Add(new Point(x, floor - 1));

                return starts;
            }

            /// <summary>
            /// Every place with room for the body (<see cref="Room"/>) joined to these, side by side, as
            /// the tiles were last read: null if there are more than <paramref name="most"/> (and how many
            /// were counted, then or all told). Every move one walking the way makes, a step, a drop, a
            /// jump up and across, goes from place to place so joined (each place it passes through has
            /// room for the body); so a way can be found from a place only if it is one of them. Read
            /// before any hall is tried: a hall only shuts more.
            /// </summary>
            public bool[] Joined(List<Point> from, int most, out int count)
            {
                var joined = new bool[w * h];
                var queue = new Queue<int>();
                count = 0;

                foreach (Point p in from)
                {
                    if (!Room(p.X, p.Y) || joined[I(p.X, p.Y)])
                        continue;

                    joined[I(p.X, p.Y)] = true;
                    queue.Enqueue(I(p.X, p.Y));
                    count++;
                }

                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    int x = x0 + i % w, y = y0 + i / w;

                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0);
                        int ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);

                        if (!Room(nx, ny))
                            continue;

                        int j = I(nx, ny);

                        if (joined[j])
                            continue;

                        if (++count > most)
                            return null;

                        joined[j] = true;
                        queue.Enqueue(j);
                    }
                }

                return joined;
            }

            /// <summary>Whether a place is one of these (<see cref="Joined"/>).</summary>
            public bool Of(bool[] places, Point at) => InArea(at.X, at.Y) && places[I(at.X, at.Y)];

            /// <summary>Where one may start from, on the ground at the mouth: within two columns of it, standing on the ground there (or as near it as the body has room), both feet on something.</summary>
            public List<Point> Starts(Point mouth)
            {
                var starts = new List<Point>();

                for (int x = mouth.X - 2; x <= mouth.X + 2; x++)
                {
                    int top = Surface(x);

                    if (top < 0)
                        continue;

                    for (int y = top - 1; y <= top + 1; y++)
                    {
                        if (Room(x, y) && Hard(x, y + 1) && Hard(x + 1, y + 1))
                        {
                            starts.Add(new Point(x, y));
                            break;
                        }
                    }
                }

                return starts;
            }

            /// <summary>
            /// The cheapest way from any of these places (those with room for the body) to one the goal
            /// says, as one walking and digging it would go; null if there is none. What is left of the
            /// way is guessed, as the crow flies to the nearest of <paramref name="aim"/> (a place, as a
            /// box a tile across, or the box all the places the goal says are in), a little short of
            /// what it could cost at the least.
            /// </summary>
            public List<Place> Search(List<Point> starts, Func<int, int, bool> goal, Rectangle aim)
            {
                Array.Fill(paid, float.MaxValue);
                Array.Fill(slot, -1);
                Array.Clear(shut);
                count = 0;

                foreach (Point s in starts)
                {
                    if (!Room(s.X, s.Y))
                        continue;

                    int i = I(s.X, s.Y);
                    float cost = DigDear * digs[i];

                    if (cost >= paid[i])
                        continue;

                    paid[i] = cost;
                    came[i] = -1;
                    how[i] = (byte)Go.Start;
                    rise[i] = 0;
                    Push(i, cost + Guess(s.X, s.Y, aim));
                }

                while (count > 0)
                {
                    int i = Pop();
                    shut[i] = true;
                    Looked++;

                    int x = x0 + i % w, y = y0 + i / w;

                    if (goal(x, y))
                        return Trace(i);

                    Moves(i, x, y, goal, aim);
                }

                return null;
            }

            private static float Guess(int x, int y, Rectangle aim) =>
                GuessShare * Math.Max(Math.Max(0, Math.Max(aim.Left - x, x - (aim.Right - 1))), Math.Max(0, Math.Max(aim.Top - y, y - (aim.Bottom - 1))));

            /// <summary>
            /// What a step costs here, for each tile of it, beside how it is made: the way down's own
            /// roll (lumps eight tiles across, <see cref="Roll"/>) over lumps twenty tiles across, 0.56
            /// to 2.2 times in all; so that it winds through the rock as a cave does, and is not
            /// drawn across it straight.
            /// </summary>
            private float Wander(int x, int y) => Roll(x, y, seed) * (0.7f + 0.8f * Lumps(x, y, seed + 1, 20));

            /// <summary>Every move from here, each to where it ends, if that costs less than the best way there yet.</summary>
            private void Moves(int i, int x, int y, Func<int, int, bool> goal, Rectangle aim)
            {
                // Turning back from the way it came costs more (TurnCost), as it does the way down
                // as it was: so that it runs one way and then the other, and does not wriggle down a
                // well.
                int was = came[i] < 0 ? 0 : Math.Sign(x - (x0 + came[i] % w));

                foreach (int d in Sides)
                {
                    int nx = x + d;
                    float roll = Wander(nx, y);
                    float here = paid[i] + (was != 0 && d != was ? TurnCost : 0f);

                    if (Room(nx, y))
                    {
                        int dig = digs[I(nx, y)];

                        // A step along, onto something; or off the edge, and down to whatever is under it.
                        if (Stands(nx, y))
                            Relax(i, nx, y, here + LevelDear * roll + DigDear * dig, Go.Along, 0, aim);
                        else
                        {
                            int ny = Fall(nx, y, y, goal, int.MinValue, 0, 0);

                            if (ny >= 0)
                                Relax(i, nx, ny, here + DigDear * dig + 1f + (ny - y), Go.Drop, 0, aim);
                        }

                        // A step down a tile, or two.
                        if (Room(nx, y + 1))
                        {
                            int dig1 = dig + digs[I(nx, y + 1)];

                            if (Stands(nx, y + 1))
                                Relax(i, nx, y + 1, here + DownDear * roll + DigDear * dig1, Go.Down, 0, aim);

                            if (Room(nx, y + 2) && Stands(nx, y + 2))
                                Relax(i, nx, y + 2, here + DownTwoDear * roll + DigDear * (dig1 + digs[I(nx, y + 2)]), Go.DownTwo, 0, aim);
                        }
                    }

                    // A step up a tile: room over where they stand and beside it, and something to step
                    // onto that the step does not dig away.
                    if (Room(x, y - 1) && Room(nx, y - 1) && StandsBut(nx, y - 1, x, y - 3, y))
                        Relax(i, nx, y - 1, here + UpDear * roll + DigDear * (digs[I(x, y - 1)] + digs[I(nx, y - 1)]), Go.Up, 0, aim);
                }

                // A jump: straight up as far as there is room (dug), then across.
                int up = 0;

                while (up < WalkJump && Room(x, y - up - 1))
                    up++;

                int upDigs = 0;

                for (int lift = 1; lift <= up; lift++)
                {
                    int top = y - lift;
                    upDigs += digs[I(x, top)];

                    foreach (int d in Sides)
                    {
                        float here = paid[i] + (was != 0 && d != was ? TurnCost : 0f);
                        int acrossDigs = 0;

                        for (int k = 1; k <= RoundAcross; k++)
                        {
                            if (k > 3 && lift < 2)
                                break;

                            int nx = x + d * k;

                            if (!Room(nx, top))
                                break;

                            acrossDigs += digs[I(nx, top)];
                            float cost = here + JumpDear + lift + 0.5f * k + DigDear * (upDigs + acrossDigs);

                            // Onto something that the jump's way up does not dig away.
                            if (StandsBut(nx, top, x, top - 2, y))
                            {
                                if (k <= 3)
                                    Relax(i, nx, top, cost, Go.JumpOnto, lift, aim);

                                continue;
                            }

                            // On over a hole, and down to whatever is under it.
                            int ny = Fall(nx, top, top, goal, x, top - 2, y);

                            if (ny >= 0)
                                Relax(i, nx, ny, cost + 0.5f * (ny - top), Go.JumpOver, lift, aim);
                        }
                    }
                }
            }

            /// <summary>
            /// Down through the air from here (the body's room dug, if it had to be): where they come to
            /// rest on something (not on any of the tiles dug by the move: column <paramref name="bx"/>
            /// and the next, rows <paramref name="top"/> to <paramref name="bottom"/>), or come into the
            /// goal; -1 if they would fall further than <see cref="WalkFall"/> from
            /// <paramref name="start"/>, or into anything kept.
            /// </summary>
            private int Fall(int x, int y, int start, Func<int, int, bool> goal, int bx, int top, int bottom)
            {
                for (int ny = y; ; ny++)
                {
                    if (goal(x, ny))
                        return ny;

                    if ((Hard(x, ny + 1) && !Within(x, ny + 1, bx, top, bottom)) || (Hard(x + 1, ny + 1) && !Within(x + 1, ny + 1, bx, top, bottom)))
                        return ny;

                    if (ny - start >= WalkFall || IsKept(x, ny + 1) || IsKept(x + 1, ny + 1))
                        return -1;
                }
            }

            private void Relax(int from, int x, int y, float cost, Go move, int lift, Rectangle aim)
            {
                int j = I(x, y);

                if (shut[j] || cost >= paid[j])
                    return;

                paid[j] = cost;
                came[j] = from;
                how[j] = (byte)move;
                rise[j] = (byte)lift;
                Push(j, cost + Guess(x, y, aim));
            }

            private List<Place> Trace(int i)
            {
                var path = new List<Place>();

                for (int j = i; j >= 0; j = came[j])
                    path.Add(new Place(x0 + j % w, y0 + j / w, (Go)how[j], rise[j]));

                path.Reverse();
                return path;
            }

            // The places still to look at: a heap, cheapest on top, each place in it once.

            private void Push(int i, float value)
            {
                rank[i] = value;

                if (slot[i] < 0)
                {
                    slot[i] = count;
                    heap[count++] = i;
                }

                Rise(slot[i]);
            }

            private int Pop()
            {
                int top = heap[0];
                slot[top] = -1;
                count--;

                if (count > 0)
                {
                    heap[0] = heap[count];
                    slot[heap[0]] = 0;
                    Sink(0);
                }

                return top;
            }

            private void Rise(int at)
            {
                int i = heap[at];
                float value = rank[i];

                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    int p = heap[parent];

                    if (rank[p] <= value)
                        break;

                    heap[at] = p;
                    slot[p] = at;
                    at = parent;
                }

                heap[at] = i;
                slot[i] = at;
            }

            private void Sink(int at)
            {
                int i = heap[at];
                float value = rank[i];

                while (true)
                {
                    int child = at * 2 + 1;

                    if (child >= count)
                        break;

                    if (child + 1 < count && rank[heap[child + 1]] < rank[heap[child]])
                        child++;

                    if (rank[heap[child]] >= value)
                        break;

                    heap[at] = heap[child];
                    slot[heap[at]] = at;
                    at = child;
                }

                heap[at] = i;
                slot[i] = at;
            }

            /// <summary>The tiles a way found takes, as it was found: the room for the body along every move (up and across a jump; at the edge of a drop, the air under it left as it is).</summary>
            public void Dig(List<Place> path, HashSet<Point> cut)
            {
                for (int k = 0; k < path.Count; k++)
                {
                    Place b = path[k];

                    if (k == 0)
                    {
                        Body(b.X, b.Y, cut);
                        continue;
                    }

                    Place a = path[k - 1];

                    switch (b.How)
                    {
                        case Go.Along:
                            Body(b.X, b.Y, cut);
                            break;

                        case Go.Up:
                            Body(a.X, a.Y - 1, cut);
                            Body(b.X, b.Y, cut);
                            break;

                        case Go.Down:
                            Body(b.X, a.Y, cut);
                            Body(b.X, b.Y, cut);
                            break;

                        case Go.DownTwo:
                            Body(b.X, a.Y, cut);
                            Body(b.X, a.Y + 1, cut);
                            Body(b.X, b.Y, cut);
                            break;

                        case Go.Drop:
                            Body(b.X, a.Y, cut);
                            break;

                        case Go.JumpOnto:
                        case Go.JumpOver:
                            for (int up = 1; up <= b.Lift; up++)
                                Body(a.X, a.Y - up, cut);

                            int d = Math.Sign(b.X - a.X);

                            for (int s = 1; s <= Math.Abs(b.X - a.X); s++)
                                Body(a.X + d * s, a.Y - b.Lift, cut);

                            break;
                    }
                }
            }

            private void Body(int x, int y, HashSet<Point> cut)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    for (int dy = 0; dy <= 2; dy++)
                        Take(area, x + dx, y - dy, cut);
                }
            }
        }

        // ===========================================================================================
        // WALLS AND LIGHT
        // ===========================================================================================

        /// <summary>The wall behind each tile that was cut, by how deep it is and how near the ball.</summary>
        private static void Walls(HashSet<Point> cut, Point ball, int reach)
        {
            double evil = reach + EvilReach;

            foreach (Point at in cut)
            {
                Tile tile = Main.tile[at.X, at.Y];
                double dx = at.X - ball.X, dy = at.Y - ball.Y;

                if (Math.Sqrt(dx * dx + dy * dy) < evil + Layout.Grain(at.X, at.Y, 6))
                    tile.WallType = Layout.BallCaveWall();
                else if (at.Y < Main.worldSurface + Layout.Grain(at.X, at.Y, DirtFray))
                    tile.WallType = WallID.DirtUnsafe;
                else if (Lumps(at.X, at.Y) > RockPatches)
                    tile.WallType = (ushort)(WallID.RocksUnsafe1 + (int)(Hash(at.X / 5, at.Y / 5, 77) * 3.99f));
                else
                    tile.WallType = 0;
            }
        }

        /// <summary>Smooth noise, lumps about six tiles across, 0 to 1: for the walls, the same in every world.</summary>
        private static float Lumps(int x, int y) => Lumps(x, y, 31);

        /// <summary>Smooth noise, lumps about this many tiles across (six, unless said), 0 to 1, for this seed.</summary>
        private static float Lumps(int x, int y, int seed, int lump = 6)
        {
            int gx = Math.DivRem(x, lump, out int rx);
            int gy = Math.DivRem(y, lump, out int ry);
            float fx = rx / (float)lump;
            float fy = ry / (float)lump;

            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(gx, gy, seed), b = Hash(gx + 1, gy, seed), c = Hash(gx, gy + 1, seed), d = Hash(gx + 1, gy + 1, seed);

            return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
        }

        /// <summary>
        /// Torches along the way, every <see cref="TorchGapLeast"/> to <see cref="TorchGapMost"/>
        /// tiles of it, each on the floor under that point of it (the game's own placing: it stands
        /// a torch only where one can stand); the evil's torch within the evil's walls. Returns how
        /// many stood.
        /// </summary>
        private static int Torches(List<Vector2> line, Point ball, int reach, Rectangle hall)
        {
            int placed = 0;
            int next = WorldGen.genRand.Next(4, 10);
            double evil = reach + EvilReach;
            Rectangle keepOff = new Rectangle(hall.Left - Masonry - Approach, hall.Top - Masonry, hall.Width + (Masonry + Approach) * 2, hall.Height + Masonry * 2);

            int plain = TorchStyle(ItemID.Torch, out int plainTile);
            int theirs = TorchStyle(Layout.BallIsCrimson ? ItemID.CrimsonTorch : ItemID.CorruptTorch, out int theirTile);

            for (int k = 0; k < line.Count; k++)
            {
                if (k < next)
                    continue;

                int x = (int)Math.Round(line[k].X);
                int y = (int)Math.Round(line[k].Y);

                // Down to the floor.
                int down = 0;

                while (down < 8 && !Solid(x, y + 1))
                {
                    y++;
                    down++;
                }

                if (!Solid(x, y + 1) || Main.tile[x, y].HasTile || Main.tile[x, y].LiquidAmount > 0 || keepOff.Contains(x, y))
                    continue;

                double dx = x - ball.X, dy = y - ball.Y;
                bool evilHere = Math.Sqrt(dx * dx + dy * dy) < evil;
                int tileType = evilHere ? theirTile : plainTile;
                int style = evilHere ? theirs : plain;

                if (tileType <= 0 || !WorldGen.PlaceTile(x, y, tileType, true, false, -1, style))
                    continue;

                placed++;
                next = k + WorldGen.genRand.Next(TorchGapLeast, TorchGapMost + 1);
            }

            return placed;
        }

        /// <summary>The tile and style an item of the game's places, read off the item (as the ball's ring reads its torches).</summary>
        private static int TorchStyle(int item, out int tile)
        {
            Item sample = ContentSamples.ItemsByType[item];

            tile = sample.createTile;
            return sample.placeStyle;
        }

        // ===========================================================================================
        // THE HALL
        // ===========================================================================================

        /// <summary>
        /// The flat way: Gray Brick round it, as the Empty Hall's is, two tiles thick, an opening at
        /// either end as tall as the level way, its two top corners stepped in as a pyramid's hall
        /// is; and its back wall in the Empty Hall's registers, from the floor up: a dado of brick,
        /// a rail of slab, the field of stone patched with brick, a frieze of brick, and a pilaster
        /// of slab every bay, standing on the rail, with its capital a tile wider under the frieze.
        /// The opening carries the hall's wall through (a reveal). A torch on each pilaster.
        /// </summary>
        private static void Build(Rectangle hall)
        {
            int floor = hall.Bottom;
            int ceiling = hall.Top - 1;
            int left = hall.Left, right = hall.Right - 1;

            for (int x = left - Masonry; x <= right + Masonry; x++)
            {
                for (int y = ceiling - Masonry + 1; y <= floor + Masonry - 1; y++)
                    undo?.Keep(x, y);
            }

            // The brick, and Gray Brick wall behind it.
            for (int x = left - Masonry; x <= right + Masonry; x++)
            {
                for (int y = ceiling - Masonry + 1; y <= floor + Masonry - 1; y++)
                {
                    bool inside = x >= left && x <= right && y > ceiling && y < floor;
                    bool opening = !inside && y >= floor - Opening && y < floor && (x < left || x > right);
                    Tile tile = Main.tile[x, y];

                    tile.LiquidAmount = 0;

                    if (inside || opening)
                    {
                        tile.ClearTile();
                        tile.WallType = Register(x, y, hall);
                        continue;
                    }

                    tile.ClearTile();
                    tile.HasTile = true;
                    tile.TileType = TileID.GrayBrick;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.WallType = WallID.GrayBrick;
                }
            }

            // The top corners, stepped in.
            foreach (int x in new[] { left, left + 1, right - 1, right })
                Brick(x, ceiling + 1);

            Brick(left, ceiling + 2);
            Brick(right, ceiling + 2);

            // A torch on each pilaster, four rows over the floor.
            foreach (int x in Pilasters(hall))
            {
                Tile tile = Main.tile[x, floor - 5];

                if (tile.HasTile)
                    continue;

                tile.HasTile = true;
                tile.TileType = TileID.Torches;
                tile.TileFrameX = 0;
                tile.TileFrameY = 0;
            }
        }

        private static void Brick(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            tile.HasTile = true;
            tile.TileType = TileID.GrayBrick;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
        }

        /// <summary>The pilasters' columns: a bay apart from either end in, the same either side of the middle.</summary>
        private static List<int> Pilasters(Rectangle hall)
        {
            var columns = new List<int>();

            for (int off = 1; off < HallWide / 2; off += Bay)
            {
                columns.Add(hall.Left + off);
                columns.Add(hall.Right - 1 - off);
            }

            columns.Sort();
            return columns;
        }

        /// <summary>The back wall at this tile of the hall, or of an opening in its end (the nearest column of the hall's own).</summary>
        private static ushort Register(int x, int y, Rectangle hall)
        {
            int floor = hall.Bottom;
            int up = floor - 1 - y;
            int down = y - hall.Top;
            int column = Math.Clamp(x, hall.Left, hall.Right - 1);
            int off = int.MaxValue;

            foreach (int pilaster in Pilasters(hall))
                off = Math.Min(off, Math.Abs(column - pilaster));

            bool inOpening = x < hall.Left || x > hall.Right - 1;

            if (up < DadoRows || down < FriezeRows)
                return WallID.GrayBrick;

            if (up == DadoRows)
                return WallID.StoneSlab;

            if (!inOpening && (off == 0 || (down == FriezeRows && off <= 1)))
                return WallID.StoneSlab;

            return Lumps(x, y) > 0.72f ? WallID.GrayBrick : WallID.Stone;
        }

        /// <summary>The Unmarked Door, standing on the floor in the middle of the hall. Its top left tile.</summary>
        private static Point Door(Rectangle hall)
        {
            int left = hall.Left + HallWide / 2 - 1;
            int top = hall.Bottom - SubworldDoor.Tall;
            ushort type = (ushort)ModContent.TileType<SubworldDoor>();

            for (int x = 0; x < SubworldDoor.Wide; x++)
            {
                for (int y = 0; y < SubworldDoor.Tall; y++)
                {
                    Tile tile = Main.tile[left + x, top + y];

                    tile.ClearTile();
                    tile.HasTile = true;
                    tile.TileType = type;
                    tile.TileFrameX = (short)(x * 18);
                    tile.TileFrameY = (short)(y * 18);
                }
            }

            return new Point(left, top);
        }

        /// <summary>
        /// The game frames nothing written straight into the world: every tile cut and its
        /// neighbours, and the hall and round it, are framed here, tile by tile. Not the door and
        /// not the torches, which were given their frames (framing them would only check them).
        /// </summary>
        private static void Frame(HashSet<Point> cut, Rectangle hall)
        {
            var near = new HashSet<Point>();

            foreach (Point at in cut)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                        near.Add(new Point(at.X + dx, at.Y + dy));
                }
            }

            for (int x = hall.Left - Masonry - Approach - 1; x <= hall.Right + Masonry + Approach; x++)
            {
                for (int y = hall.Top - Masonry - 1; y <= hall.Bottom + Masonry; y++)
                    near.Add(new Point(x, y));
            }

            foreach (Point at in near)
            {
                if (!WorldGen.InWorld(at.X, at.Y, 2))
                    continue;

                Tile tile = Main.tile[at.X, at.Y];

                if (tile.HasTile && !Main.tileFrameImportant[tile.TileType])
                    WorldGen.TileFrame(at.X, at.Y, true, false);

                if (tile.WallType > 0)
                    Framing.WallFrame(at.X, at.Y, true);
            }
        }
    }

    /// <summary>
    /// Whether this world has its way down, and where its mouth and its door are. Saved with the
    /// world: /struct update reads it to know the way is there (StructureUpdate).
    /// </summary>
    public class WayDownState : ModSystem
    {
        public static bool Made { get; private set; }

        public static Point Mouth { get; private set; }

        public static Point Door { get; private set; }

        public static void Set(Point mouth, Point door)
        {
            Made = true;
            Mouth = mouth;
            Door = door;
        }

        public override void ClearWorld()
        {
            Made = false;
            Mouth = Point.Zero;
            Door = Point.Zero;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (!Made)
                return;

            tag["PADWayDown"] = true;
            tag["PADWayDownMouthX"] = Mouth.X;
            tag["PADWayDownMouthY"] = Mouth.Y;
            tag["PADWayDownDoorX"] = Door.X;
            tag["PADWayDownDoorY"] = Door.Y;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Made = tag.ContainsKey("PADWayDown");
            Mouth = Made ? new Point(tag.GetInt("PADWayDownMouthX"), tag.GetInt("PADWayDownMouthY")) : Point.Zero;
            Door = Made ? new Point(tag.GetInt("PADWayDownDoorX"), tag.GetInt("PADWayDownDoorY")) : Point.Zero;
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Made);
            writer.Write(Mouth.X);
            writer.Write(Mouth.Y);
            writer.Write(Door.X);
            writer.Write(Door.Y);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Made = reader.ReadBoolean();
            Mouth = new Point(reader.ReadInt32(), reader.ReadInt32());
            Door = new Point(reader.ReadInt32(), reader.ReadInt32());
        }
    }
}
