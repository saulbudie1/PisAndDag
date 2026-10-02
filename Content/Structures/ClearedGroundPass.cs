using System;
using System.Collections.Generic;
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
    /// all stay, and nothing is levelled. Two things are done to it.
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

            progress.Set(0.5f);

            int felled = FellGiants();

            GenLog.Log?.Info(
                $"Cleared Ground: x {Math.Min(Col(0), Col(last))}-{Math.Max(Col(0), Col(last))}. " +
                $"Pieces of floating land taken off: {pieces}. Living trees cut: {felled}.");
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
