using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The Cleared Ground, the chopped forest in front of the Mile.
    ///
    /// The ground is left as vanilla made it: its lumps of dirt, its ponds and its cracks
    /// all stay, and nothing is levelled, but for the middle of it, which is shaped for
    /// 'Dame' Quilt. Three things are done to it.
    ///
    /// **The floating land goes.** Vanilla's Tunnels pass throws a crust of soil up a
    /// dozen tiles over the ground with nothing under it, and the room beneath comes out
    /// hung with leaf wall. A crust is floating land when it is thin, has room under it,
    /// and its top stands well over the ground either side of it; a cave's roof is thin
    /// with room under it too, but it lies level with the ground beside it, and is left
    /// alone. What is floating land is taken off down to the ground under it, with
    /// whatever narrow thing held one piece of it to the next, the walls that stood under
    /// it and anything standing on it. The ground it stood over, and any water lying on
    /// that, is not touched. A piece that starts on this ground is followed off either end
    /// of it, so no half of one is left at the edge.
    ///
    /// **The middle of 'Dame' Quilt's ground is made flat.** She rides at whoever she
    /// stops, and her box (BossArena) is half this ground wide; left as vanilla made it,
    /// the flattest place in one world was fifty-seven tiles, with a pit eighteen deep at
    /// the end of them. Her ground is the middle of this one (Layout.CalmStart to
    /// CalmEnd, room for her box), and the middle of that is where she is met.
    ///
    /// That middle (Layout.FlatStart to FlatEnd) is put on a line of its own: the lie of
    /// the land there, its highs still high and its lows still low, at whatever part of
    /// their height keeps the line within <see cref="CalmRise"/> rows of its level.
    /// Ground within a row of the line is left as it lies. What stands higher is cut down
    /// to it, what lies lower is filled up to it, no column is left a step of more than a
    /// row from the next, nothing is left hollow in the first <see cref="Firm"/> rows
    /// under it, and no water is left standing on it or where it would run onto it. A few
    /// small holes that were there are left there: none deeper than
    /// <see cref="HoleDeep"/> rows, which is the user's number.
    ///
    /// The edges of her ground are vanilla's, which is what the user asked them to be: its
    /// hills, its ponds and its cracks. Past either end of the flat the ground eases back
    /// into them over Layout.FlatEase columns, each column put part of the way from where
    /// it lies to the height of the flat, so that a hill, a pond or a crack standing
    /// there is lower, shallower and smaller toward the flat; a pond there keeps its
    /// water, as much of its depth as it keeps of its bed's and none of it higher than it
    /// stood. The ground there is moved and no more: what is hollow under it is left
    /// hollow, except that a cave which a cut goes through into is taken for a pit, and
    /// is filled its part of the way like one. From where the easing stops to the rim
    /// nothing is touched, but for water that would run from there onto the flat.
    ///
    /// No living tree grows on what is made (Layout.NoGiantHere). The world is told it has
    /// such ground and how much of it is flat (PADWorld.CalmGround, CalmFlat), and she is
    /// met at the middle of it.
    ///
    /// **The giants are cut.** A living tree with its foot on this ground is cut to a
    /// stump <see cref="Stump"/> blocks over the ground beside it, a few splinters of the
    /// trunk and of its heartwood above that, and everything at or under the cut, the
    /// foot, the roots, the hollow and the room at the bottom of it, is kept. One that
    /// stands past either end is left alone, crown and all.
    ///
    /// Runs where the plain is made, after everything that carves and before everything
    /// that grows: vanilla's grass comes up afterwards on the ground the floating land
    /// leaves bare, and the trees it plants are what the cleanup at the end turns into
    /// the stumps the field is known by.
    /// </summary>
    public class ClearedGroundPass : GenPass
    {
        // ------------------------------------------------------------------ the field

        /// <summary>
        /// How far either way WastedMilePass rolls its near edge. This ground is taken to
        /// the far end of that, and the ash takes however much of it the roll gives it.
        /// </summary>
        private const int MileWobble = 60;

        /// <summary>A crust this thick or thinner, with this much room or more under it, may be floating land.</summary>
        private const int SlabMost = 12;
        private const int SlabGap = 3;

        /// <summary>It is, when its top stands this many rows or more over the ground either side of it.</summary>
        private const int IslandRise = 5;

        /// <summary>
        /// The ground either side: the middle height of this many columns of plain ground,
        /// looked for from this far past the crust's end to this far.
        /// </summary>
        private const int OutSkip = 2;
        private const int OutLook = 40;
        private const int OutNeed = 6;

        /// <summary>Floating land is followed this far past an end of this ground, and across breaks this wide.</summary>
        private const int EdgeWalk = 120;
        private const int EdgeBreak = 8;

        /// <summary>Looked for this many times over: what one piece hid, or held up, is seen once it is gone.</summary>
        private const int Rounds = 4;

        /// <summary>
        /// This much open air over the last thing cleared and the column is done. An
        /// island floats fifty tiles and more over the highest ground in the world, so
        /// whatever is the other side of a gap this size is not this pass's to take.
        /// </summary>
        private const int OpenSky = 45;

        // ------------------------------------------------------------------ the giants

        /// <summary>A living tree's stump stands this many blocks over the ground beside it.</summary>
        public const int Stump = 5;

        /// <summary>The tallest splinter of wood left on it, and the most heartwood left behind them.</summary>
        private const int SliverMost = 4;
        private const int LobeMost = 6;

        /// <summary>The stump is solid wood this far either side of its hollow. Out from there the roots are left as roots.</summary>
        private const int Core = 3;

        /// <summary>Under a crown: how far the ground may step from one column to the next.</summary>
        private const int Walk = 6;

        /// <summary>How far to one side of a tree the ground beside it is looked for.</summary>
        private const int StartLook = 40;

        /// <summary>How far under the lowest ground a tree's wood is looked for: its roots, not the caves.</summary>
        private const int RootReach = 90;

        /// <summary>The most one tree is followed for, so a fill can never run away across the world.</summary>
        private const int GiantCap = 120000;

        // ------------------------------------------------------------------ her ground

        /// <summary>How far the flat's line rises and falls about its level, in rows.</summary>
        private const int CalmRise = 6;

        /// <summary>
        /// The lie of the land the line follows: the ground's height averaged over this many
        /// columns either way, and that averaged the same way again.
        /// </summary>
        private const int CalmReach = 30;

        /// <summary>Ground within this many rows of the line, over it or under it, is left as it lies.</summary>
        private const int CalmRough = 1;

        /// <summary>
        /// A hole that was there is left, when it is no deeper than this (the user's number)
        /// under the line and under the higher of its lips, and no wider than this.
        /// DonaQuinnEncounter steps over the same hole when it measures flat ground.
        /// </summary>
        public const int HoleDeep = 5;
        public const int HoleWide = 8;

        /// <summary>A few: this many at the most, and this far from one to the next.</summary>
        public const int HolesMost = 4;
        private const int HolesApart = 60;

        /// <summary>Nothing is filled deeper than this under where the ground is put: a shaft is plugged, not packed.</summary>
        private const int FillMost = 120;

        /// <summary>
        /// The flat is sound this many rows down: a cave or a pocket of water that near the
        /// top of it is packed. Under that they are left, and so is every one under the
        /// easing past the flat.
        /// </summary>
        private const int Firm = 4;

        /// <summary>
        /// Past the flat, a cut that goes through into something hollow is followed down
        /// this many times at the most: through one cave into the next under it.
        /// </summary>
        private const int Falls = 8;

        /// <summary>
        /// Made ground has its wall behind it from this many rows down and none over that.
        /// Vanilla's own starts within a few rows of the top; this far down, none shows
        /// behind a top block poured to half.
        /// </summary>
        private const int WallFrom = 2;

        /// <summary>How far past either end of the flat water is looked for that would run onto it.</summary>
        private const int ShedLook = 300;

        /// <summary>
        /// What is claimed of the flat in the structure map, so that the passes which ask
        /// it before they dig leave it alone: this far over its level and this far under.
        /// </summary>
        private const int ClaimOver = 40;
        private const int ClaimUnder = 30;

        // ------------------------------------------------------------------ this world

        /// <summary>The jungle end as a world column, and which way the Mile is from it.</summary>
        private int near;
        private int dir;

        /// <summary>The last column of this ground, counted from the jungle end.</summary>
        private int last;

        /// <summary>Where a search for the ground starts and stops.</summary>
        private int scanTop;
        private int scanEnd;

        public ClearedGroundPass(string name, double weight) : base(name, weight) { }

        /// <summary>The world column this far from the jungle end, toward the Mile.</summary>
        private int Col(int along) => near + dir * along;

        /// <summary>And back.</summary>
        private int Along(int x) => (x - near) * dir;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Clearing the field of fire";

            if (!ExtraLargeWorld.Active)
                return;

            near = WorldMirror.X(Layout.ClearedStart);
            dir = WorldMirror.Dir;
            last = Layout.MileStart + MileWobble - Layout.ClearedStart;
            scanTop = Layout.ScanTop;
            scanEnd = Math.Min(Main.maxTilesY - 10, (int)Main.worldSurface + 60);

            // The floating land first: the saw wants the ground a tree is left standing on.
            int pieces = 0;

            for (int round = 0; round < Rounds; round++)
            {
                int more = Islands();

                if (more == 0)
                    break;

                pieces += more;
            }

            progress.Set(0.4f);

            // Her ground next, before the saw: a stump is measured from the ground beside it.
            Calm(out int level, out int cut, out int filled, out int holes, out int ponds, out int shed);

            progress.Set(0.7f);

            int felled = FellGiants();

            (int calmFrom, int calmTo) = WorldMirror.Span(Layout.CalmStart, Layout.CalmEnd);
            (int flatFrom, int flatTo) = WorldMirror.Span(Layout.FlatStart, Layout.FlatEnd);

            GenLog.Log?.Info(
                $"Cleared Ground: x {Math.Min(Col(0), Col(last))}-{Math.Max(Col(0), Col(last))}. " +
                $"Pieces of floating land taken off: {pieces}. Living trees cut: {felled}. " +
                (level < 0
                    ? "'Dame' Quilt's ground: no ground to make it of."
                    : $"'Dame' Quilt's ground: x {calmFrom}-{calmTo}, flat from x {flatFrom} to {flatTo} and level at y {level}. " +
                      $"{cut} columns cut down, {filled} filled, small holes left on the flat: {holes}, " +
                      $"columns with water on them where the flat eases: {ponds}, " +
                      $"tiles of water taken that would have run onto the flat: {shed}."));
        }

        // ============================================================
        // WHAT IS THERE
        // ============================================================

        private static bool TreeType(ushort type)
            => type == TileID.LivingWood || type == TileID.LeafBlock
            || type == TileID.LivingMahogany || type == TileID.LivingMahoganyLeaves;

        private static bool IsTree(Tile t) => t.HasTile && TreeType(t.TileType);

        private static bool IsWood(Tile t)
            => t.HasTile && (t.TileType == TileID.LivingWood || t.TileType == TileID.LivingMahogany);

        private static bool IsLeaf(Tile t)
            => t.HasTile && (t.TileType == TileID.LeafBlock || t.TileType == TileID.LivingMahoganyLeaves);

        /// <summary>The wall a living tree is papered with inside: behind its wood, and on its own in the hollow.</summary>
        private static bool Heartwood(Tile t)
            => t.WallType == WallID.LivingWoodUnsafe || t.WallType == WallID.LivingWood;

        /// <summary>
        /// Ground: a solid block that is not a platform, not a made thing (a door is solid
        /// too, and a living tree has doors in it) and not a living tree. The tree's tiles
        /// are named rather than left to Main.tileSolid, because vanilla turns the leaf
        /// block's solidity off for the rest of generation once its Living Trees pass is
        /// done.
        /// </summary>
        private static bool IsGround(Tile t)
            => t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]
            && !Main.tileFrameImportant[t.TileType] && !TreeType(t.TileType);

        /// <summary>Water, or any liquid, standing in the open.</summary>
        private static bool OpenWater(Tile t) => !t.HasTile && t.LiquidAmount > 0;

        /// <summary>The trunk at this tile: its wood, or the hollow inside it.</summary>
        private static bool TrunkAt(int x, int y)
        {
            Tile t = Main.tile[x, y];
            return IsWood(t) || (!t.HasTile && Heartwood(t));
        }

        private static bool Hollow(int x, int y)
        {
            Tile t = Main.tile[x, y];
            return !t.HasTile && Heartwood(t);
        }

        private static bool InReach(int x) => x >= 10 && x < Main.maxTilesX - 10;

        /// <summary>
        /// What a walker stands on in this column: the first ground, or the top of open
        /// water. -1 where there is none, and where a living tree's wood or hollow is met
        /// first, because what is found under a trunk is the bottom of the tree and no
        /// ground to go by.
        /// </summary>
        private int Surface(int x)
        {
            if (!InReach(x))
                return -1;

            for (int y = scanTop; y < scanEnd; y++)
            {
                Tile t = Main.tile[x, y];

                if (IsGround(t) || OpenWater(t))
                    return y;

                if (IsWood(t) || Heartwood(t))
                    return -1;
            }

            return -1;
        }

        /// <summary>
        /// A thin crust with room under it, in this column: where its top is, and what is
        /// under the room, which is the water if any stands there and the ground if none
        /// does. False if the column has no such crust.
        ///
        /// Under a living tree's trunk or branch there is none: what is found there is the
        /// bottom of the tree. In the room a root is passed over like air, so a tree grown
        /// through a crust does not make it ground; but a root is not room, and neither is
        /// the hollow of a trunk, so the inside of a tree is not a room under a crust.
        /// </summary>
        private bool Under(int x, out int top, out int floor)
        {
            top = -1;
            floor = -1;

            if (!InReach(x))
                return false;

            int y = scanTop;

            for (; y < scanEnd; y++)
            {
                Tile t = Main.tile[x, y];

                if (IsGround(t))
                    break;

                // Open water before any ground: a lake, not a crust.
                if (OpenWater(t))
                    return false;

                if (IsWood(t) || Heartwood(t))
                    return false;
            }

            if (y >= scanEnd)
                return false;

            top = y;

            while (y < scanEnd && IsGround(Main.tile[x, y]))
                y++;

            if (y - top > SlabMost)
                return false;

            int gap = y, room = 0;

            for (; y < scanEnd; y++)
            {
                Tile t = Main.tile[x, y];

                if (IsGround(t))
                    break;

                if (!IsTree(t) && !Heartwood(t))
                    room++;
            }

            if (y >= scanEnd || room < SlabGap)
                return false;

            floor = y;

            for (int row = gap; row < y; row++)
            {
                if (OpenWater(Main.tile[x, row]))
                {
                    floor = row;
                    break;
                }
            }

            return true;
        }

        // ============================================================
        // THE FLOATING LAND
        // ============================================================

        /// <summary>
        /// One look for floating land, and whatever it finds taken off. Returns how many
        /// pieces that was.
        /// </summary>
        private int Islands()
        {
            List<(int from, int to)> runs = new List<(int, int)>();
            int start = 0, end = 0, breaks = 0;
            bool open = false;

            // Runs of columns with a crust in them. A few columns without one do not end a
            // run: they are what holds one piece to the next. A living tree standing in it
            // ends nothing however wide it is.
            for (int d = -EdgeWalk; d <= last + EdgeWalk; d++)
            {
                int x = Col(d);

                if (Under(x, out _, out _))
                {
                    if (!open)
                    {
                        open = true;
                        start = d;
                    }

                    end = d;
                    breaks = 0;
                }
                else if (open && Surface(x) >= 0 && ++breaks > EdgeBreak)
                {
                    runs.Add((start, end));
                    open = false;
                    breaks = 0;
                }
            }

            if (open)
                runs.Add((start, end));

            // Which of them are floating land, all told before any is touched; and only
            // those with a column on this ground.
            List<(int from, int to)> floating = new List<(int, int)>();

            foreach ((int from, int to) in runs)
            {
                if (to < 0 || from > last)
                    continue;

                if (Floating(from, to))
                    floating.Add((from, to));
            }

            foreach ((int from, int to) in floating)
                Remove(from, to);

            return floating.Count;
        }

        /// <summary>The middle height of the plain ground past one end of a run of crust. -1 if there is none within sight.</summary>
        private int Outside(int from, int step)
        {
            List<int> found = new List<int>();

            for (int k = OutSkip; k < OutLook; k++)
            {
                int x = Col(from + step * k);

                if (Under(x, out _, out _))
                    continue;

                int surface = Surface(x);

                if (surface < 0)
                    continue;

                found.Add(surface);

                if (found.Count >= OutNeed)
                    break;
            }

            if (found.Count == 0)
                return -1;

            found.Sort();
            return found[found.Count / 2];
        }

        /// <summary>The ground either side of a run of crust. One side stands for both where the other has none. False if neither has.</summary>
        private bool Beside(int from, int to, out int before, out int after)
        {
            before = Outside(from, -1);
            after = Outside(to, 1);

            if (before < 0 && after < 0)
                return false;

            if (before < 0)
                before = after;

            if (after < 0)
                after = before;

            return true;
        }

        /// <summary>
        /// How far a crust's top stands over the ground beside its run, at this column,
        /// the ground taken as a straight line from the one side to the other. Times the
        /// run's length, so it stays in whole numbers.
        /// </summary>
        private static int Rise(int from, int to, int before, int after, int d, int top)
        {
            int length = Math.Max(1, to - from);
            return before * length + (after - before) * (d - from) - top * length;
        }

        /// <summary>The ground beside a run, at this column: the straight line from the one side to the other.</summary>
        private static int Line(int from, int to, int before, int after, int d)
        {
            int length = Math.Max(1, to - from);
            return (before * length + (after - before) * (d - from)) / length;
        }

        /// <summary>
        /// Whether a run of crust is floating land: whether the middle one of its columns
        /// stands <see cref="IslandRise"/> rows or more over the ground either side.
        /// </summary>
        private bool Floating(int from, int to)
        {
            if (!Beside(from, to, out int before, out int after))
                return false;

            List<int> rise = new List<int>();

            for (int d = from; d <= to; d++)
            {
                if (Under(Col(d), out int top, out _))
                    rise.Add(Rise(from, to, before, after, d, top));
            }

            if (rise.Count == 0)
                return false;

            rise.Sort();
            return rise[rise.Count / 2] >= IslandRise * Math.Max(1, to - from);
        }

        /// <summary>
        /// Where the ground is, in a column that has floating land over it. The first crust
        /// goes whatever it is. Under it, anything else thin goes too while it stands as
        /// high over the ground beside as floating land does, however small the room under
        /// it: two crusts that crossed leave a lattice, not a gap. What is thick, or does
        /// not stand that high, is the ground, or a cave's roof, and stays. -1 if there is
        /// nothing under it at all.
        /// </summary>
        private int Resolve(int from, int to, int before, int after, int d, int x)
        {
            int need = IslandRise * Math.Max(1, to - from);
            int y = scanTop;
            bool first = true;

            while (true)
            {
                for (; y < scanEnd; y++)
                {
                    Tile t = Main.tile[x, y];

                    if (IsGround(t))
                        break;

                    if (OpenWater(t))
                        return y;
                }

                if (y >= scanEnd)
                    return -1;

                int top = y;

                if (!first && Rise(from, to, before, after, d, top) < need)
                    return top;

                while (y < scanEnd && IsGround(Main.tile[x, y]))
                    y++;

                if (y - top > SlabMost)
                    return top;

                first = false;
            }
        }

        /// <summary>
        /// One piece of floating land, taken off down to the ground under it: every column
        /// of crust that stands over the ground beside, and whatever narrow thing stood
        /// between one stretch of crust and the next, which was holding it up.
        ///
        /// Nothing is cut deeper than a crust can reach under the ground beside, and what
        /// held it up is not cut under that ground at all: a pit or a tree's shaft under
        /// the island is not the island's.
        /// </summary>
        private void Remove(int from, int to)
        {
            if (!Beside(from, to, out int before, out int after))
                return;

            List<int> held = new List<int>();
            int lastFloor = 0;

            for (int d = from; d <= to; d++)
            {
                int x = Col(d);
                int floor = -1;

                if (Under(x, out int top, out _) && Rise(from, to, before, after, d, top) > 0)
                    floor = Resolve(from, to, before, after, d, x);

                if (floor < 0)
                {
                    held.Add(d);
                    continue;
                }

                floor = Math.Min(floor, Line(from, to, before, after, d) + SlabMost);

                foreach (int between in held)
                {
                    int row = Math.Min(Math.Min(lastFloor, floor), Line(from, to, before, after, between));

                    CutAbove(Col(between), row);
                    StripRoom(Col(between), row);
                }

                held.Clear();

                CutAbove(x, floor, top);
                lastFloor = floor;
            }
        }

        /// <summary>
        /// Where a cut stopped short of the ground: the walls of the room that was under
        /// the island, from the cut down to the ground, and no further than a crust could
        /// reach. Only the walls: water lying on the ground there stays.
        /// </summary>
        private void StripRoom(int x, int row)
        {
            if (!InReach(x))
                return;

            for (int y = row; y < scanEnd && y < row + SlabMost; y++)
            {
                Tile t = Main.tile[x, y];

                if (IsGround(t))
                    break;

                if (!t.HasTile && t.WallType != 0 && !Heartwood(t))
                {
                    t.WallType = 0;
                    WorldGen.SquareTileFrame(x, y, true);
                }
            }
        }

        /// <summary>
        /// Everything standing over this row goes: soil, walls, water. A living tree is
        /// left for the saw, and so is whatever stands in its hollow; soil goes even from
        /// inside it. Stops at the first <see cref="OpenSky"/> rows of nothing, and not
        /// before the row <paramref name="reach"/> if one is given: a crust can hang
        /// further over its ground than that.
        /// </summary>
        private void CutAbove(int x, int row, int reach = int.MaxValue)
        {
            if (!InReach(x))
                return;

            int empty = 0;

            for (int y = row - 1; y > 10 && (empty < OpenSky || y >= reach); y--)
            {
                Tile t = Main.tile[x, y];
                bool had = false;

                if (t.HasTile && !TreeType(t.TileType) && (IsGround(t) || !Heartwood(t)))
                {
                    Unhook(x, y, t);
                    t.ClearTile();
                    had = true;
                }

                if (t.WallType != 0 && !Heartwood(t))
                {
                    t.WallType = 0;
                    had = true;
                }

                if (t.LiquidAmount != 0)
                {
                    t.LiquidAmount = 0;
                    had = true;
                }

                if (had)
                {
                    WorldGen.SquareTileFrame(x, y, true);
                    empty = 0;
                }
                else
                {
                    empty++;
                }
            }
        }

        /// <summary>
        /// A chest is a tile and an entry in Main.chest, and clearing the tile alone leaves
        /// the entry pointing at nothing. The same as WastedMileFillPass does it.
        /// </summary>
        private static void Unhook(int x, int y, Tile t)
        {
            int frameWidth = t.TileType switch
            {
                TileID.Containers or TileID.Containers2 => 36,
                TileID.Dressers => 54,
                _ => 0,
            };

            if (frameWidth == 0)
                return;

            int cornerX = x - t.TileFrameX % frameWidth / 18;
            int cornerY = y - t.TileFrameY % 36 / 18;
            int id = Chest.FindChest(cornerX, cornerY);

            if (id >= 0)
                Chest.DestroyChestDirect(cornerX, cornerY, id);
        }

        // ============================================================
        // HER GROUND
        // ============================================================

        /// <summary>The first ground in this column at or under this row, whatever stands or lies over it. -1 if there is none.</summary>
        private int GroundFrom(int x, int from)
        {
            if (!InReach(x))
                return -1;

            for (int y = Math.Max(11, from); y < scanEnd; y++)
            {
                if (IsGround(Main.tile[x, y]))
                    return y;
            }

            return -1;
        }

        /// <summary>
        /// The flat middle of 'Dame' Quilt's ground made (the class says how). What comes
        /// back: the row it is level at, -1 if there was no ground to make it of; how many
        /// columns were cut down, how many were filled, how many small holes were left on
        /// the flat, how many columns of the easing past it still have water standing on
        /// them, and how many tiles of water were taken because they would have run onto
        /// the flat.
        /// </summary>
        private void Calm(out int level, out int cut, out int filled, out int holes, out int ponds, out int shed)
        {
            level = -1;
            cut = 0;
            filled = 0;
            holes = 0;
            ponds = 0;
            shed = 0;

            // The flat, counted from the jungle end, and the easing past either end of it.
            int first = Layout.FlatStart - Layout.ClearedStart;
            int final = Layout.FlatEnd - Layout.ClearedStart;
            int from = first - Layout.FlatEase;
            int count = final + Layout.FlatEase - from + 1;

            // The flat, in what follows: a to b of count columns, the easing either side.
            int a = first - from, b = final - from;

            // The ground as it lies, the water on it looked through. A column with none takes its neighbour's.
            int[] lies = new int[count];
            bool[] bare = new bool[count];
            int known = -1;

            for (int i = 0; i < count; i++)
            {
                lies[i] = GroundFrom(Col(from + i), scanTop);
                bare[i] = lies[i] < 0;

                if (lies[i] >= 0)
                    known = lies[i];
            }

            if (known < 0)
                return;

            for (int i = count - 1; i >= 0; i--)
            {
                if (lies[i] < 0)
                    lies[i] = known;
                else
                    known = lies[i];
            }

            // The water standing where the flat eases, before the ground under it is moved:
            // how many rows of it, and what it is.
            int[] wet = new int[count];
            int[] liquid = new int[count];

            for (int i = 0; i < count; i++)
            {
                int x = Col(from + i);

                if ((i >= a && i <= b) || bare[i] || !InReach(x))
                    continue;

                for (int y = lies[i] - 1; y >= scanTop && OpenWater(Main.tile[x, y]); y--)
                {
                    if (wet[i] == 0)
                        liquid[i] = Main.tile[x, y].LiquidType;

                    wet[i]++;
                }
            }

            // Its level: the middle height of the flat.
            int[] heights = new int[b - a + 1];
            Array.Copy(lies, a, heights, 0, heights.Length);
            Array.Sort(heights);
            level = heights[heights.Length / 2];

            // The line: the lie of the land about that level, at whatever part of its height
            // keeps it within CalmRise. Past either end it holds the height it had at the end.
            double[] lie = Average(Average(Array.ConvertAll(lies, row => (double)row), CalmReach), CalmReach);
            double most = 0.0;

            for (int i = a; i <= b; i++)
                most = Math.Max(most, Math.Abs(lie[i] - level));

            double part = most <= CalmRise ? 1.0 : CalmRise / most;
            int[] line = new int[count];

            for (int i = 0; i < count; i++)
                line[i] = level + (int)Math.Round((lie[Math.Clamp(i, a, b)] - level) * part);

            // Where each column of the flat is to stand: where it lies, if that is within
            // a row of the line once what stands over the line is off it; a row over the line
            // if it stood higher; a row under if it lay lower.
            int[] want = (int[])lies.Clone();
            bool[] hole = new bool[count];

            for (int i = a; i <= b; i++)
            {
                int ground = GroundFrom(Col(from + i), Math.Max(scanTop, line[i] - CalmRough));

                if (ground < 0)
                    ground = line[i] + FillMost;

                want[i] = ground;
                hole[i] = ground > line[i] + CalmRough;
            }

            // The holes. A few small ones that lay open before anything was cut are left;
            // every other is filled.
            int lastKept = -HolesApart;

            for (int i = a; i <= b; i++)
            {
                if (!hole[i])
                    continue;

                int end = i, deepest = 0;
                bool lay = true;

                while (end <= b && hole[end])
                {
                    deepest = Math.Max(deepest, want[end] - line[end]);
                    lay &= want[end] == lies[end];
                    end++;
                }

                // With a lip on the flat both sides of it.
                bool keep = lay && i > a && end <= b
                    && end - i <= HoleWide && deepest <= HoleDeep
                    && holes < HolesMost && i - lastKept >= HolesApart;

                if (keep)
                {
                    holes++;
                    lastKept = end - 1;
                }
                else
                {
                    for (int k = i; k < end; k++)
                    {
                        hole[k] = false;
                        want[k] = line[k] + CalmRough;
                    }
                }

                i = end - 1;
            }

            // No step of more than a row from one column to the next: the low side comes up.
            // A hole that is kept is stepped across, lip to lip, as if it were not there. The
            // lips come up with the rest, so a hole is measured again once they have, from
            // the higher of the two: deeper than HoleDeep, it is filled after all; no deeper
            // than a row, it is no hole, and is walked with the rest. Either way the ground
            // is walked once more.
            for (bool again = true; again;)
            {
                again = false;

                int solid = 0;

                for (int i = a; i <= b; i++)
                {
                    if (!hole[i])
                        solid++;
                }

                int[] walk = new int[solid];

                for (int i = a, n = 0; i <= b; i++)
                {
                    if (!hole[i])
                        walk[n++] = want[i];
                }

                Layout.Level(walk);

                for (int i = a, n = 0; i <= b; i++)
                {
                    if (!hole[i])
                        want[i] = walk[n++];
                }

                for (int i = a; i <= b; i++)
                {
                    if (!hole[i])
                        continue;

                    int end = i, bottom = 0;

                    while (end <= b && hole[end])
                    {
                        bottom = Math.Max(bottom, want[end]);
                        end++;
                    }

                    // A kept hole has a column of the flat either side of it.
                    int deep = bottom - Math.Min(want[i - 1], want[end]);

                    if (deep > HoleDeep || deep <= CalmRough)
                    {
                        for (int k = i; k < end; k++)
                        {
                            hole[k] = false;

                            if (deep > HoleDeep)
                                want[k] = line[k] + CalmRough;
                        }

                        holes--;
                        again = true;
                    }

                    i = end - 1;
                }
            }

            // The easing: how much of the flat each column past it takes on. All of it in
            // the flat itself.
            float[] share = new float[count];

            for (int i = a; i <= b; i++)
                share[i] = 1f;

            Ease(lies, want, share, a, -1);
            Ease(lies, want, share, b, 1);

            // And the ground put there. The lowest ground each column lay on is kept: its
            // own, or the floor of whatever was packed or fallen into under it. Grass that
            // is buried is looked for that deep.
            bool[] made = new bool[count];
            int[] low = (int[])lies.Clone();

            for (int i = 0; i < count; i++)
            {
                int x = Col(from + i);

                if (!InReach(x))
                    continue;

                bool hers = i >= a && i <= b;
                bool laid = false;

                if (hers)
                {
                    if (want[i] > lies[i])
                    {
                        CutAbove(x, want[i]);
                        cut++;
                        made[i] = true;
                    }

                    // The flat: up to it where it lay lower, under it where the cut went
                    // through into a cave, and whatever else is hollow in the first rows
                    // under it: under the bottom of a hole that is kept as well, which
                    // stands where it lay.
                    for (int y = want[i]; y < want[i] + Firm; y++)
                    {
                        int rest = Fill(x, y);

                        if (rest >= 0)
                        {
                            laid = true;
                            low[i] = Math.Max(low[i], rest);
                        }
                    }

                    Drain(x);
                }
                else
                {
                    // Past the flat the ground is moved and no more: what is hollow under it
                    // is left hollow. Packed, a hollow under the lip of a pit would stand as
                    // a pillar of soil, one column wide, down a pit whose other columns are
                    // open. But where the ground is cut, and the cut goes through into
                    // something hollow, the column lies on the floor of that from then on,
                    // and is put its part of the way from there: so beside the flat a cave
                    // that a cut has opened is filled back in, and where the easing stops it
                    // is left open, as a pit that lay open already is.
                    int toward = i < a ? want[a] : want[b];
                    int at = lies[i];

                    for (int fall = 0; fall < Falls && want[i] > at; fall++)
                    {
                        CutAbove(x, want[i]);

                        if (!made[i])
                        {
                            cut++;
                            made[i] = true;
                        }

                        int floor = GroundFrom(x, want[i]);

                        if (floor < 0)
                            break;

                        at = floor;

                        if (floor > want[i])
                            want[i] = (int)Math.Round(at + (toward - at) * share[i]);
                    }

                    // The column stands where its ground does, if that is higher than it was to be put.
                    if (want[i] > at)
                        want[i] = at;

                    low[i] = Math.Max(low[i], at);

                    if (want[i] < at)
                        laid = Fill(x, want[i]) >= 0;
                }

                if (laid)
                {
                    filled++;
                    made[i] = true;
                }

                if (made[i])
                    Dress(x, want[i]);

                // A pond where the flat eases goes with its bed: as much of its depth is
                // left as there is of the land's own shape under it, and none of it higher
                // than it stood. Where neither the bed nor the depth has changed, the water
                // is not touched.
                if (!hers && wet[i] > 0)
                {
                    int stood = lies[i] - wet[i];
                    int deep = Math.Max(0, Math.Min((int)Math.Round(wet[i] * (1f - share[i])), want[i] - stood));

                    if (want[i] != lies[i] || deep != wet[i])
                        Pond(x, want[i], deep, liquid[i]);
                }
            }

            // Water that would still run onto the flat: from past either end, where the
            // ground no longer holds it back, or out from under a ledge.
            shed = Shed(first, final);

            for (int i = 0; i < count; i++)
            {
                if ((i < a || i > b) && Wet(Col(from + i)))
                    ponds++;
            }

            // Grass where soil shows, and none where it no longer does: in what was made, and a
            // column past it as well, whose side is bare now or buried. As deep as the ground
            // lay there before. Then the steps of one row that are left, poured flat
            // (Layout.Hammer's rule).
            for (int i = 0; i < count; i++)
            {
                if (!made[i] && !(i > 0 && made[i - 1]) && !(i < count - 1 && made[i + 1]))
                    continue;

                int deepest = Math.Max(low[i], Math.Max(low[Math.Max(0, i - 1)], low[Math.Min(count - 1, i + 1)]));

                Sod(Col(from + i), deepest);
            }

            for (int i = 1; i < count - 1; i++)
            {
                if (!made[i] || hole[i])
                    continue;

                int x = Col(from + i);
                int top = GroundFrom(x, scanTop);

                if (top < 0)
                    continue;

                int before = GroundFrom(Col(from + i - 1), scanTop);
                int after = GroundFrom(Col(from + i + 1), scanTop);

                // SolidTile is false of a half block and of a slope, so the hammer never
                // turns one back into a whole block.
                if ((before == top + 1 || after == top + 1) && before >= top && after >= top
                    && WorldGen.InWorld(x, top, 4) && WorldGen.SolidTile(x, top, false))
                {
                    WorldGen.PoundTile(x, top);
                }
            }

            int left = Math.Min(Col(first), Col(final));

            GenVars.structures?.AddProtectedStructure(
                new Rectangle(left, level - ClaimOver, final - first + 1, ClaimOver + ClaimUnder), 0);

            // The world has it from here on, and how much of it is flat: her place is its
            // middle (DonaQuinnEncounter).
            PADWorld.CalmGround = true;
            PADWorld.CalmFlat = Layout.FlatHalf;
        }

        /// <summary>Water standing over the ground in this column.</summary>
        private bool Wet(int x)
        {
            int top = GroundFrom(x, scanTop);

            return top > scanTop && OpenWater(Main.tile[x, top - 1]);
        }

        /// <summary>Each value with those up to this many places either side of it, averaged. The ends average what there is.</summary>
        private static double[] Average(double[] values, int reach)
        {
            double[] sums = new double[values.Length + 1];

            for (int i = 0; i < values.Length; i++)
                sums[i + 1] = sums[i] + values[i];

            double[] result = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                int lo = Math.Max(0, i - reach);
                int hi = Math.Min(values.Length - 1, i + reach);

                result[i] = (sums[hi + 1] - sums[lo]) / (hi - lo + 1);
            }

            return result;
        }

        /// <summary>
        /// The columns past one end of the flat: each put part of the way from where it lies
        /// to the height the flat has at that end. All of the way beside the flat and none
        /// of it where the easing stops, so that what vanilla made there, a hill, a pond's
        /// bed, a crack, is lower, shallower and smaller toward the flat and itself again
        /// where the easing stops. <paramref name="share"/> is given the part each column
        /// took.
        /// </summary>
        private static void Ease(int[] lies, int[] want, float[] share, int end, int step)
        {
            int row = want[end];
            int run = step < 0 ? end : lies.Length - 1 - end;

            for (int k = 1; k <= run; k++)
            {
                int i = end + step * k;
                float t = 1f - k / (float)(run + 1);

                share[i] = t * t * (3f - 2f * t);
                want[i] = (int)Math.Round(lies[i] + (row - lies[i]) * share[i]);
            }
        }

        /// <summary>
        /// Soil from this row down to the ground under it, or to a living tree, and no
        /// deeper than <see cref="FillMost"/>: the soil the column has under it. Whatever
        /// stood in the way goes. The ground it is laid on was a top until now, and may
        /// have been sloped or poured to half as one: it is a whole block again, with no
        /// gap left under the fill. What comes back is the row the fill came to rest on,
        /// the first it did not lay; -1 if it laid none.
        /// </summary>
        private int Fill(int x, int row)
        {
            int under = GroundFrom(x, row);
            ushort soil = under >= 0 && (Main.tile[x, under].TileType == TileID.Mud || Main.tile[x, under].TileType == TileID.JungleGrass)
                ? TileID.Mud
                : TileID.Dirt;

            bool any = false;
            int y = row;

            for (; y < row + FillMost && y < Main.maxTilesY - 10; y++)
            {
                Tile t = Main.tile[x, y];

                if (IsGround(t))
                {
                    if (any && (t.Slope != SlopeType.Solid || t.IsHalfBlock))
                    {
                        t.Slope = SlopeType.Solid;
                        t.IsHalfBlock = false;
                        WorldGen.SquareTileFrame(x, y, true);
                    }

                    break;
                }

                if (IsTree(t) || Heartwood(t))
                    break;

                if (t.HasTile)
                {
                    if (Main.tileFrameImportant[t.TileType])
                        TakeWhole(x, y);

                    t.ClearTile();
                }

                t.HasTile = true;
                t.TileType = soil;
                t.Slope = SlopeType.Solid;
                t.IsHalfBlock = false;
                t.LiquidAmount = 0;

                if (y >= row + WallFrom && t.WallType == 0)
                    t.WallType = WallID.DirtUnsafe;

                WorldGen.SquareTileFrame(x, y, true);
                any = true;
            }

            return any ? y : -1;
        }

        /// <summary>A made thing in the way of the fill, taken whole and not by the column: every tile of its kind within a few of this one.</summary>
        private void TakeWhole(int x, int y)
        {
            ushort type = Main.tile[x, y].TileType;

            for (int i = x - 3; i <= x + 3; i++)
            {
                if (!InReach(i))
                    continue;

                for (int j = Math.Max(11, y - 4); j <= Math.Min(Main.maxTilesY - 11, y + 4); j++)
                {
                    Tile t = Main.tile[i, j];

                    if (!t.HasTile || t.TileType != type)
                        continue;

                    Unhook(i, j, t);
                    t.ClearTile();
                    WorldGen.SquareTileFrame(i, j, true);
                }
            }
        }

        /// <summary>
        /// A pond's water in one column of the easing, put back on its bed: this many rows
        /// of it over the ground, which stands at <paramref name="row"/> now, and none over
        /// that. Water that lies in those rows already is left as it lies; it is laid where
        /// the bed went down from under it.
        /// </summary>
        private void Pond(int x, int row, int deep, int kind)
        {
            for (int y = scanTop; y < row; y++)
            {
                Tile t = Main.tile[x, y];
                bool water = y >= row - deep && !t.HasTile;

                if (water ? t.LiquidAmount > 0 && t.LiquidType == kind : t.LiquidAmount == 0)
                    continue;

                t.LiquidAmount = (byte)(water ? 255 : 0);

                if (water)
                    t.LiquidType = kind;

                WorldGen.SquareTileFrame(x, y, true);
            }
        }

        /// <summary>No water is left standing on the flat: whatever lies over the ground in this column goes.</summary>
        private void Drain(int x)
        {
            for (int y = scanTop; y < scanEnd; y++)
            {
                Tile t = Main.tile[x, y];

                if (IsGround(t))
                    break;

                if (t.LiquidAmount != 0)
                {
                    t.LiquidAmount = 0;
                    WorldGen.SquareTileFrame(x, y, true);
                }
            }
        }

        /// <summary>
        /// Water that would run onto the flat when vanilla settles its liquids again, which
        /// it does after this. Water goes sideways and down and never up, so every way it
        /// could come is walked the other way: from the air over each column of the flat
        /// (<paramref name="first"/> to <paramref name="final"/>, counted from the jungle
        /// end), sideways and up and never down, through whatever is not solid, out to
        /// <see cref="ShedLook"/> columns past either end. Water met on that walk goes: the
        /// top of a pond whose rim was brought down under it, what stands under a ledge
        /// beside it, what a cut has opened the side of a cave on. Water the ground still
        /// holds back is not met, and is left where it lies: the ponds of the edges, and
        /// every tile of a pond nothing here touched. Returns how many tiles of water went.
        /// </summary>
        private int Shed(int first, int final)
        {
            int taken = 0;
            int width = final - first + 1 + ShedLook * 2;
            bool[,] seen = new bool[width, scanEnd - scanTop];
            Stack<(int k, int y)> open = new Stack<(int k, int y)>();

            for (int along = first; along <= final; along++)
            {
                int top = GroundFrom(Col(along), scanTop);

                if (top >= 0)
                    open.Push((along - first + ShedLook, top - 1));
            }

            while (open.Count > 0)
            {
                (int k, int y) = open.Pop();

                if (k < 0 || k >= width || y < scanTop || y >= scanEnd || seen[k, y - scanTop])
                    continue;

                seen[k, y - scanTop] = true;

                int x = Col(first - ShedLook + k);

                if (!InReach(x))
                    continue;

                Tile t = Main.tile[x, y];

                // What water does not pass: a solid block, whatever its shape, that is no platform.
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    continue;

                if (t.LiquidAmount != 0)
                {
                    t.LiquidAmount = 0;
                    WorldGen.SquareTileFrame(x, y, true);
                    taken++;
                }

                open.Push((k - 1, y));
                open.Push((k + 1, y));
                open.Push((k, y - 1));
            }

            return taken;
        }

        /// <summary>
        /// The top of a made column. Its first rows are whole blocks: what vanilla sloped
        /// there it sloped for the side of a cave or the foot of a hill that is gone, and
        /// the steps that are left are poured after. And as vanilla leaves the top of its
        /// own ground: rock and clay in the first two rows are soil, with no wall behind
        /// them that near the air.
        /// </summary>
        private static void Dress(int x, int row)
        {
            for (int y = row; y < row + Firm; y++)
            {
                Tile t = Main.tile[x, y];

                if (!IsGround(t))
                    continue;

                t.Slope = SlopeType.Solid;
                t.IsHalfBlock = false;

                if (y < row + WallFrom)
                {
                    if (t.TileType == TileID.Stone || t.TileType == TileID.ClayBlock)
                        t.TileType = TileID.Dirt;

                    if (t.WallType != 0 && !Heartwood(t))
                        t.WallType = 0;
                }

                WorldGen.SquareTileFrame(x, y, true);
            }
        }

        /// <summary>
        /// Grass on the soil that shows in this column, its top and a few rows of its side:
        /// vanilla has laid its grass by now, and lays none on ground made after. And soil
        /// again where grass no longer shows, down to <paramref name="deepest"/>: the old
        /// ground, under what was filled in over it.
        /// </summary>
        private void Sod(int x, int deepest)
        {
            if (!InReach(x))
                return;

            int top = GroundFrom(x, scanTop);

            if (top < 0)
                return;

            // As far down as grass is laid, or as far as it may lie buried, whichever is deeper.
            int bottom = Math.Max(top + HoleDeep + 1, deepest + WallFrom);

            for (int y = top; y <= bottom && y < scanEnd; y++)
            {
                Tile t = Main.tile[x, y];

                if (!t.HasTile)
                    continue;

                bool shows = Shows(x, y);

                if (shows && y < top + HoleDeep + 2 && (t.TileType == TileID.Dirt || t.TileType == TileID.Mud))
                    t.TileType = t.TileType == TileID.Mud ? TileID.JungleGrass : TileID.Grass;
                else if (!shows && (t.TileType == TileID.Grass || t.TileType == TileID.JungleGrass))
                    t.TileType = t.TileType == TileID.JungleGrass ? TileID.Mud : TileID.Dirt;
                else
                    continue;

                WorldGen.SquareTileFrame(x, y, true);
            }
        }

        /// <summary>Air, or anything else that is not ground, beside this tile or over it or at a corner of it.</summary>
        private static bool Shows(int x, int y)
        {
            for (int i = x - 1; i <= x + 1; i++)
            {
                for (int j = y - 1; j <= y + 1; j++)
                {
                    if ((i != x || j != y) && WorldGen.InWorld(i, j, 2) && !IsGround(Main.tile[i, j]))
                        return true;
                }
            }

            return false;
        }

        // ============================================================
        // THE GIANTS
        // ============================================================

        /// <summary>Every living tree with wood in this ground's columns, looked at once each.</summary>
        private int FellGiants()
        {
            HashSet<int> seen = new HashSet<int>();
            int felled = 0;

            int lowest = -1;

            for (int d = 0; d <= last; d++)
                lowest = Math.Max(lowest, Surface(Col(d)));

            if (lowest < 0)
                return 0;

            int from = Math.Max(10, scanTop - 120);
            int to = Math.Min(Main.maxTilesY - 10, lowest + RootReach);

            for (int d = 0; d <= last; d++)
            {
                int x = Col(d);

                if (!InReach(x))
                    continue;

                for (int y = from; y < to; y++)
                {
                    if (!IsWood(Main.tile[x, y]) || seen.Contains(y * Main.maxTilesX + x))
                        continue;

                    if (Fell(Gather(x, y, seen), seen))
                        felled++;
                }
            }

            return felled;
        }

        /// <summary>
        /// One tree, whole: everything joined to this tile through its wood, its leaves and
        /// its heartwood wall. The wall is what joins a trunk to its foot, across the room
        /// vanilla leaves at the bottom of it.
        /// </summary>
        private static List<int> Gather(int startX, int startY, HashSet<int> seen)
        {
            List<int> cells = new List<int>();
            Stack<int> frontier = new Stack<int>();

            frontier.Push(startY * Main.maxTilesX + startX);

            while (frontier.Count > 0 && cells.Count < GiantCap)
            {
                int key = frontier.Pop();
                int x = key % Main.maxTilesX;
                int y = key / Main.maxTilesX;

                if (x < 10 || x >= Main.maxTilesX - 10 || y < 10 || y >= Main.maxTilesY - 10)
                    continue;

                Tile t = Main.tile[x, y];

                if (!IsTree(t) && !Heartwood(t))
                    continue;

                if (!seen.Add(key))
                    continue;

                cells.Add(key);

                frontier.Push(key + 1);
                frontier.Push(key - 1);
                frontier.Push(key + Main.maxTilesX);
                frontier.Push(key - Main.maxTilesX);
            }

            return cells;
        }

        /// <summary>
        /// Cuts one tree, if it is this ground's to cut. True if it was.
        ///
        /// The ground beside it is the ground as it lies, walked under the crown from
        /// either side of it. Where the trunk, its foot or its hollow is standing at that
        /// height is a foot. A tree with a foot on this ground is cut, all of it, every
        /// trunk of a clump. A tree whose feet are all past the ends stands on somebody
        /// else's ground and is left alone. A tree with no foot at all was standing on
        /// floating land that has gone, and everything of it over the ground goes.
        /// </summary>
        private bool Fell(List<int> cells, HashSet<int> seen)
        {
            if (cells.Count == 0)
                return false;

            int lo = int.MaxValue, hi = int.MinValue;

            foreach (int key in cells)
            {
                int d = Along(key % Main.maxTilesX);
                lo = Math.Min(lo, d);
                hi = Math.Max(hi, d);
            }

            lo -= 2;
            hi += 2;

            // The ground under it, walked in from each side. Under the trunk each walk
            // only knows the ground on the side it came from, and the two sides need not
            // agree: the higher of the two is the ground a stump is measured from, so it
            // stands its full height over whichever side is nearer to its top.
            int[] fromBefore = WalkGround(lo, hi, 1);
            int[] fromAfter = WalkGround(lo, hi, -1);
            int[] ground = new int[hi - lo + 1];

            for (int i = 0; i < ground.Length; i++)
            {
                int a = fromBefore[i], b = fromAfter[i];

                // No ground to be found either side of it: nothing to measure a stump from.
                if (a < 0 && b < 0)
                    return false;

                ground[i] = a < 0 ? b : b < 0 ? a : Math.Min(a, b);
            }

            // Where it goes into the ground. A column or two of soil in a foot does not
            // make it two feet.
            List<(int from, int to, int ground, int cut)> feet = new List<(int, int, int, int)>();

            for (int d = lo; d <= hi; d++)
            {
                if (!TrunkAt(Col(d), ground[d - lo] - 1))
                    continue;

                int end = d;

                while (true)
                {
                    int next = 0;

                    for (int k = 1; k <= 3 && next == 0; k++)
                    {
                        if (end + k <= hi && TrunkAt(Col(end + k), ground[end + k - lo] - 1))
                            next = k;
                    }

                    if (next == 0)
                        break;

                    end += next;
                }

                int height = int.MaxValue;

                for (int q = d; q <= end; q++)
                    height = Math.Min(height, ground[q - lo]);

                feet.Add((d, end, height, Lintel(d, end, height - Stump)));
                d = end;
            }

            bool standing = false, onTheField = false;

            foreach ((int from, int to, int height, int _) in feet)
            {
                for (int q = from; q <= to; q++)
                {
                    if (!TrunkAt(Col(q), height))
                        continue;

                    standing = true;

                    if (q >= 0 && q <= last)
                        onTheField = true;
                }
            }

            if (standing && !onTheField)
                return false;

            if (!standing)
                feet.Clear();

            // Off the trunk nothing of it is left above the ground: a branch is not a stump.
            int[] cut = new int[ground.Length];
            int[] sliver = new int[ground.Length];
            int[] lobe = new int[ground.Length];

            for (int i = 0; i < cut.Length; i++)
                cut[i] = ground[i];

            foreach ((int from, int to, int _, int at) in feet)
            {
                for (int q = from; q <= to; q++)
                    cut[q - lo] = at;

                Break(from, to, at, lo, sliver, lobe);
            }

            // The saw. Leaves go wherever they are; wood over the cut goes but for the
            // splinters, and so does whatever stood in the hollow up there.
            foreach (int key in cells)
            {
                int x = key % Main.maxTilesX;
                int y = key / Main.maxTilesX;
                int i = Along(x) - lo;
                Tile t = Main.tile[x, y];

                if (IsLeaf(t))
                {
                    t.ClearTile();
                    WorldGen.SquareTileFrame(x, y, true);
                    continue;
                }

                if (y >= cut[i] || !t.HasTile)
                    continue;

                if (IsWood(t) && y >= cut[i] - sliver[i] && WoodDownTo(x, y, cut[i]))
                    continue;

                if (!IsTree(t))
                    Unhook(x, y, t);

                t.ClearTile();
                WorldGen.SquareTileFrame(x, y, true);
            }

            // And the heartwood over the cut, but for what is left standing behind the splinters.
            foreach (int key in cells)
            {
                int x = key % Main.maxTilesX;
                int y = key / Main.maxTilesX;
                int i = Along(x) - lo;
                Tile t = Main.tile[x, y];

                if (y >= cut[i] || !Heartwood(t))
                    continue;

                if (y >= cut[i] - lobe[i] && HeartwoodDownTo(x, y, cut[i]))
                    continue;

                t.WallType = 0;
            }

            foreach ((int from, int to, int height, int at) in feet)
                Foot(from, to, height, at, seen);

            return true;
        }

        /// <summary>
        /// The ground under one tree, column by column across it from one side, starting
        /// from the ground beside it. Each column's is looked for within a few rows of the
        /// last one's, which is what keeps a crown, a branch or a root from being taken for
        /// it; where the trunk itself is in the way there is none, and the column keeps the
        /// last. -1 in every column if there is no ground beside the tree on that side.
        /// </summary>
        private int[] WalkGround(int lo, int hi, int step)
        {
            int[] ground = new int[hi - lo + 1];
            Array.Fill(ground, -1);

            int start = step > 0 ? lo - 1 : hi + 1;
            int height = -1, skipped = 0;

            for (int k = 0; k < StartLook; k++)
            {
                int surface = Surface(Col(start - step * k));

                if (surface >= 0)
                {
                    height = surface;
                    break;
                }
            }

            if (height < 0)
                return ground;

            for (int d = step > 0 ? lo : hi; d >= lo && d <= hi; d += step)
            {
                int x = Col(d);
                int reach = Walk + skipped;
                int found = -1;

                for (int y = Math.Max(11, height - reach); y <= Math.Min(Main.maxTilesY - 11, height + reach); y++)
                {
                    if (IsGround(Main.tile[x, y]))
                    {
                        found = y;
                        break;
                    }
                }

                if (found > 0 && !IsTree(Main.tile[x, found - 1]) && !Heartwood(Main.tile[x, found - 1]))
                {
                    height = found;
                    skipped = 0;
                }
                else
                {
                    skipped++;
                }

                ground[d - lo] = height;
            }

            return ground;
        }

        /// <summary>
        /// The row a foot is cut at. <see cref="Stump"/> over the ground, unless the tree has
        /// a top of its own within two rows of that: wood lying across the hollow, the way
        /// vanilla closes the room at the bottom of a trunk. A stump with a top on it is the
        /// better stump.
        /// </summary>
        private int Lintel(int from, int to, int at)
        {
            foreach (int shift in new[] { 0, 1, -1, 2, -2 })
            {
                int row = at + shift;
                int across = 0;

                for (int q = from; q <= to; q++)
                {
                    if (IsWood(Main.tile[Col(q), row]) && Hollow(Col(q), row + 1))
                        across++;
                }

                if (across >= 2)
                    return row;
            }

            return at;
        }

        /// <summary>
        /// The broken top of one foot. A tall splinter of wood and maybe a second, a few
        /// stubs, and the heartwood standing behind them higher than any of the wood: all
        /// of it only where the tree had wood and heartwood to leave.
        /// </summary>
        private void Break(int from, int to, int at, int lo, int[] sliver, int[] lobe)
        {
            // Splinters: on the wood that has heartwood behind it or beside it.
            List<int> core = new List<int>();

            for (int q = from; q <= to; q++)
            {
                int x = Col(q);

                if (!IsWood(Main.tile[x, at]) || !IsWood(Main.tile[x, at - 1]))
                    continue;

                if (Heartwood(Main.tile[Col(q - 1), at - 1]) || Heartwood(Main.tile[x, at - 1]) || Heartwood(Main.tile[Col(q + 1), at - 1]))
                    core.Add(q);
            }

            if (core.Count > 0)
            {
                int tall = core[WorldGen.genRand.Next(core.Count)];
                List<int> apart = core.FindAll(q => Math.Abs(q - tall) >= 2);
                int second = apart.Count > 0 ? apart[WorldGen.genRand.Next(apart.Count)] : int.MinValue;

                foreach (int q in core)
                {
                    if (q == tall)
                        sliver[q - lo] = WorldGen.genRand.Next(3, SliverMost + 1);
                    else if (q == second)
                        sliver[q - lo] = WorldGen.genRand.Next(2, 4);
                    else if (Math.Abs(q - tall) == 1 || (second != int.MinValue && Math.Abs(q - second) == 1))
                        sliver[q - lo] = 0;
                    else
                        sliver[q - lo] = WorldGen.genRand.NextDouble() < 0.4 ? 1 : 0;
                }
            }

            List<int> walled = new List<int>();

            for (int q = from; q <= to; q++)
            {
                if (Heartwood(Main.tile[Col(q), at - 1]))
                    walled.Add(q);
            }

            if (walled.Count > 0)
            {
                int peak = walled[WorldGen.genRand.Next(walled.Count)];

                foreach (int q in walled)
                    lobe[q - lo] = Math.Max(2, LobeMost - Math.Abs(q - peak) - WorldGen.genRand.Next(2));
            }
        }

        private static bool WoodDownTo(int x, int y, int cut)
        {
            for (int row = y; row < cut; row++)
            {
                if (!IsWood(Main.tile[x, row]))
                    return false;
            }

            return true;
        }

        private static bool HeartwoodDownTo(int x, int y, int cut)
        {
            for (int row = y; row < cut; row++)
            {
                if (!Heartwood(Main.tile[x, row]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Round its hollow the stump is one piece, with bark either side of the hollow the
        /// whole height of it: vanilla leaves the room at the bottom of some trunks open at
        /// the sides, and a stump cut through that would be hanging on heartwood alone. Soil
        /// the ground left in the middle of a foot is wood. Out past <see cref="Core"/>
        /// columns the roots are left as roots.
        /// </summary>
        private void Foot(int from, int to, int height, int at, HashSet<int> seen)
        {
            int first = int.MaxValue, final = int.MinValue;

            for (int q = from; q <= to; q++)
            {
                for (int row = at; row < height; row++)
                {
                    if (!Hollow(Col(q), row))
                        continue;

                    first = Math.Min(first, q);
                    final = Math.Max(final, q);
                }
            }

            if (first > final)
                return;

            for (int row = at; row < height; row++)
            {
                Wood(Col(first - 1), row, seen);
                Wood(Col(final + 1), row, seen);

                int left = int.MaxValue, right = int.MinValue;

                for (int q = first - Core; q <= final + Core; q++)
                {
                    if (!TrunkAt(Col(q), row))
                        continue;

                    left = Math.Min(left, q);
                    right = Math.Max(right, q);
                }

                for (int q = left; q <= right; q++)
                    Wood(Col(q), row, seen);
            }
        }

        /// <summary>Living wood here, if what is here is soil or open air. The hollow, and anything standing in it, is left.</summary>
        private static void Wood(int x, int y, HashSet<int> seen)
        {
            Tile t = Main.tile[x, y];

            if (IsTree(t))
                return;

            if (!IsGround(t) && (t.HasTile || Heartwood(t)))
                return;

            t.HasTile = true;
            t.TileType = TileID.LivingWood;
            t.Slope = SlopeType.Solid;
            t.IsHalfBlock = false;
            t.LiquidAmount = 0;
            WorldGen.SquareTileFrame(x, y, true);

            // Not a tree to be found and looked at again.
            seen.Add(y * Main.maxTilesX + x);
        }
    }
}
