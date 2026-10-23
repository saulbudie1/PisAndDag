using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Deep
{
    /// <summary>
    /// A crevice an eel lives in (VastOceanFloor.Crevices cuts them): a straight burrow into the
    /// rock, two cells across, <see cref="Length"/> long. (<see cref="X"/>, <see cref="Y"/>) is its
    /// back, the deepest cell of it; the other cell across is the next row down for one that runs
    /// across, the next column right for one that runs up or down; (<see cref="Dx"/>,
    /// <see cref="Dy"/>) is the way out of it.
    /// </summary>
    public readonly struct EelLair
    {
        public readonly int X, Y, Dx, Dy, Length;

        public EelLair(int x, int y, int dx, int dy, int length)
        {
            X = x;
            Y = y;
            Dx = dx;
            Dy = dy;
            Length = length;
        }

        /// <summary>The crevice's middle line at this many cells out from its back (0, its back cell's middle), in world pixels: between its two cells across.</summary>
        public Vector2 Point(float k)
        {
            return Dy == 0
                ? new Vector2((X + Dx * k) * 16f + 8f, (Y + 1) * 16f)
                : new Vector2((X + 1) * 16f, (Y + Dy * k) * 16f + 8f);
        }

        /// <summary>Its mouth's cell's middle: where a lurking eel's head is.</summary>
        public Vector2 Mouth => Point(Length - 1);

        public bool Same(EelLair other) => X == other.X && Y == other.Y && Dx == other.Dx && Dy == other.Dy;
    }

    /// <summary>
    /// Finding the eels' crevices in the world as it is, the game's tiles read: the same
    /// shape VastOceanFloor.Crevices cuts (<see cref="At"/>).
    /// </summary>
    public static class EelLairs
    {
        /// <summary>The shortest and the longest a crevice may be to be one.</summary>
        public const int Shortest = 10;
        public const int Longest = 26;

        /// <summary>Rock, as a crevice is made of it: a solid tile (a slope is rock; a platform is not); and anything off the world.</summary>
        public static bool Rock(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return true;

            Tile t = Main.tile[x, y];
            return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
        }

        /// <summary>
        /// A crevice whose back is (<paramref name="x"/>, <paramref name="y"/>), running out the way
        /// given: two rows of rock six across behind its back; then, cell by cell out from it, its
        /// two cells open with two cells of rock either side of them, for <see cref="Shortest"/>
        /// to <see cref="Longest"/> cells; then its two cells open for two more, where the rock
        /// either side gives out (its mouth). Its length, out to where the rock gives out.
        /// </summary>
        public static bool At(int x, int y, int dx, int dy, out int length)
        {
            length = 0;
            int px = dy == 0 ? 0 : 1, py = dy == 0 ? 1 : 0;

            for (int k = -2; k <= -1; k++)
            {
                for (int j = -2; j <= 3; j++)
                {
                    if (!Rock(x + dx * k + px * j, y + dy * k + py * j))
                        return false;
                }
            }

            int run = 0;

            for (int k = 0; k <= Longest + 1; k++)
            {
                int cx = x + dx * k, cy = y + dy * k;

                if (Rock(cx, cy) || Rock(cx + px, cy + py))
                    return false;

                if (!Rock(cx - px, cy - py) || !Rock(cx - 2 * px, cy - 2 * py) || !Rock(cx + 2 * px, cy + 2 * py) || !Rock(cx + 3 * px, cy + 3 * py))
                    break;

                run = k + 1;
            }

            if (run < Shortest || run > Longest)
                return false;

            for (int k = run; k <= run + 1; k++)
            {
                int cx = x + dx * k, cy = y + dy * k;

                if (Rock(cx, cy) || Rock(cx + px, cy + py))
                    return false;
            }

            length = run;
            return true;
        }

        /// <summary>The four ways out a crevice can run.</summary>
        private static readonly Point[] Outs = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };

        /// <summary>Every crevice whose back is in this box, into the list.</summary>
        public static void Find(int x0, int y0, int x1, int y1, List<EelLair> into)
        {
            for (int y = Math.Max(2, y0); y <= Math.Min(Main.maxTilesY - 3, y1); y++)
            {
                for (int x = Math.Max(2, x0); x <= Math.Min(Main.maxTilesX - 3, x1); x++)
                {
                    if (Rock(x, y))
                        continue;

                    foreach (Point o in Outs)
                    {
                        // Cheap first: rock right behind it, and its other cell across open.
                        if (!Rock(x - o.X, y - o.Y))
                            continue;

                        if (At(x, y, o.X, o.Y, out int length))
                            into.Add(new EelLair(x, y, o.X, o.Y, length));
                    }
                }
            }
        }
    }

    /// <summary>
    /// The way a Crevice Eel's head can swim from one place to another, by request ("they
    /// should have smart ai to find their way back inside"): cell by cell (a cell two tiles by
    /// two from its top left, the room the head needs, as its chart of a hunt has it:
    /// CreviceEelHead), the eight ways, never cutting a corner of rock; the shortest, found by
    /// looking first where the way is likeliest to be shortest (A*: steps taken so far, and as
    /// few again as could be left to the goal), within a box of <see cref="Side"/> cells about
    /// the two places, giving up past <see cref="MostLooked"/> cells looked at. Where the head
    /// or the goal has no room (against the rock), the nearest cell about it that has, within
    /// three. One way found at a time, on whichever machine moves the eel.
    /// </summary>
    internal static class EelWays
    {
        /// <summary>The box's side, in cells, and the most cells looked at before giving up.</summary>
        public const int Side = 384;
        public const int MostLooked = 40000;

        private const int Straight = 10;
        private const int Slant = 14;

        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private static int[] cost;
        private static int[] costStamp;
        private static byte[] came;
        private static byte[] room;
        private static int[] roomStamp;
        private static int[] heap;
        private static int[] heapKey;
        private static int heapCount;
        private static int stamp;
        private static int ox, oy;

        /// <summary>Room for the head at this cell of the box (its world cell's two tiles by two open), looked at once a search.</summary>
        private static bool Room(int i)
        {
            if (roomStamp[i] != stamp)
            {
                roomStamp[i] = stamp;
                int x = ox + i % Side, y = oy + i / Side;
                room[i] = (byte)(!EelLairs.Rock(x, y) && !EelLairs.Rock(x + 1, y) && !EelLairs.Rock(x, y + 1) && !EelLairs.Rock(x + 1, y + 1) ? 1 : 2);
            }

            return room[i] == 1;
        }

        /// <summary>The cell of the box nearest this world cell that has room, within three; -1 if none or off the box.</summary>
        private static int Near(Point cell)
        {
            for (int r = 0; r <= 3; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                            continue;

                        int x = cell.X + dx - ox, y = cell.Y + dy - oy;

                        if (x < 0 || y < 0 || x >= Side || y >= Side)
                            continue;

                        int i = y * Side + x;

                        if (Room(i))
                            return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>As few steps as could be left from this cell of the box to that one (straight and slanting).</summary>
        private static int Least(int a, int b)
        {
            int dx = Math.Abs(a % Side - b % Side), dy = Math.Abs(a / Side - b / Side);
            return Straight * Math.Max(dx, dy) + (Slant - Straight) * Math.Min(dx, dy);
        }

        /// <summary>
        /// The way from the cell <paramref name="from"/> to the cell <paramref name="to"/> (world
        /// cells, as CreviceEelHead's CellOf gives them), each cell of it into
        /// <paramref name="into"/> from the first after the start to the goal; false if there is
        /// none within the box, or none found looking at <see cref="MostLooked"/> cells.
        /// </summary>
        public static bool Find(Point from, Point to, List<Point> into)
        {
            into.Clear();

            if (Math.Abs(from.X - to.X) > Side - 12 || Math.Abs(from.Y - to.Y) > Side - 12)
                return false;

            if (cost == null)
            {
                cost = new int[Side * Side];
                costStamp = new int[Side * Side];
                came = new byte[Side * Side];
                room = new byte[Side * Side];
                roomStamp = new int[Side * Side];
                heap = new int[MostLooked * 8 + 16];
                heapKey = new int[MostLooked * 8 + 16];
            }

            if (++stamp == int.MaxValue)
            {
                Array.Clear(costStamp);
                Array.Clear(roomStamp);
                stamp = 1;
            }

            ox = (from.X + to.X) / 2 - Side / 2;
            oy = (from.Y + to.Y) / 2 - Side / 2;

            int start = Near(from), goal = Near(to);

            if (start < 0 || goal < 0)
                return false;

            heapCount = 0;
            cost[start] = 0;
            costStamp[start] = stamp;
            came[start] = 255;
            Push(start, Least(start, goal));

            int looked = 0;

            while (heapCount > 0)
            {
                Pop(out int node, out int key);

                // Already reached a shorter way since this was put on the heap.
                if (key != cost[node] + Least(node, goal))
                    continue;

                if (node == goal)
                {
                    for (int at = goal; at != start; at -= DX[came[at]] + DY[came[at]] * Side)
                        into.Add(new Point(ox + at % Side, oy + at / Side));

                    into.Reverse();
                    return true;
                }

                if (++looked > MostLooked)
                    return false;

                int nx0 = node % Side, ny0 = node / Side;

                for (int k = 0; k < 8; k++)
                {
                    int nx = nx0 + DX[k], ny = ny0 + DY[k];

                    if (nx < 0 || ny < 0 || nx >= Side || ny >= Side)
                        continue;

                    int n = ny * Side + nx;

                    if (!Room(n))
                        continue;

                    // Never cutting a corner of rock.
                    if (k >= 4 && (!Room(ny0 * Side + nx) || !Room(ny * Side + nx0)))
                        continue;

                    int c = cost[node] + (k < 4 ? Straight : Slant);

                    if (costStamp[n] == stamp && cost[n] <= c)
                        continue;

                    cost[n] = c;
                    costStamp[n] = stamp;
                    came[n] = (byte)k;

                    if (heapCount < heap.Length)
                        Push(n, c + Least(n, goal));
                }
            }

            return false;
        }

        private static void Push(int node, int key)
        {
            int i = heapCount++;

            while (i > 0)
            {
                int parent = (i - 1) / 2;

                if (heapKey[parent] <= key)
                    break;

                heap[i] = heap[parent];
                heapKey[i] = heapKey[parent];
                i = parent;
            }

            heap[i] = node;
            heapKey[i] = key;
        }

        private static void Pop(out int node, out int key)
        {
            node = heap[0];
            key = heapKey[0];

            int lastNode = heap[--heapCount];
            int lastKey = heapKey[heapCount];
            int i = 0;

            while (true)
            {
                int child = i * 2 + 1;

                if (child >= heapCount)
                    break;

                if (child + 1 < heapCount && heapKey[child + 1] < heapKey[child])
                    child++;

                if (heapKey[child] >= lastKey)
                    break;

                heap[i] = heap[child];
                heapKey[i] = heapKey[child];
                i = child;
            }

            if (heapCount > 0)
            {
                heap[i] = lastNode;
                heapKey[i] = lastKey;
            }
        }

        /// <summary>The memory kept for finding ways let go (the mod unloading).</summary>
        public static void Unload()
        {
            cost = null;
            costStamp = null;
            came = null;
            room = null;
            roomStamp = null;
            heap = null;
            heapKey = null;
            stamp = 0;
        }
    }

    /// <summary>
    /// The eels put in their crevices, where the world is decided (the server, or the one
    /// player's own game): for each player in the Vast Ocean from the 8th section down, every
    /// six and a quarter to fifteen and a half seconds (eight to twenty before; by request "1.3x
    /// spawn rate": 1.3 times as often, <see cref="WaitLeast"/>, <see cref="WaitMost"/>), if fewer
    /// than <see cref="MostNear"/> eels are within <see cref="NearTiles"/> tiles of them, one is
    /// put in a crevice (<see cref="EelLairs"/>) no eel has, its body fitting it, within
    /// <see cref="ReachTiles"/> tiles of them and out of their sight, lurking there
    /// (CreviceEelHead); or, by request ("place random chunk of these in large rooms of
    /// caverns"), a group in a den: a crevice with others free within <see cref="GroupTiles"/>
    /// tiles is three times as likely to be the one chosen, and brings the nearest of those
    /// with it, <see cref="LeastGroup"/> to <see cref="MostGroup"/> eels in all (the big rooms'
    /// dens are cut so: VastOceanFloor.Dens). The crevices are found once for each stretch of
    /// forty by forty tiles and kept, and looked at again before one is used.
    /// </summary>
    public class CreviceEelSpawns : ModSystem
    {
        public const int MostNear = 2;
        public const int NearTiles = 150;
        public const int ReachTiles = 110;

        /// <summary>Out of a player's sight: farther than this across, or this up or down.</summary>
        private const int UnseenAcross = 66;
        private const int UnseenUp = 40;

        private const int Stretch = 40;

        /// <summary>A den: free crevices whose mouths are this many tiles apart or less are filled together, this many eels at the least and the most (as many as there are crevices).</summary>
        public const int GroupTiles = 40;
        public const int LeastGroup = 2;
        public const int MostGroup = 4;

        /// <summary>Ticks between one try and the next for each player, at the least and the most: eight and twenty seconds, over 1.3.</summary>
        public const int WaitLeast = 369;
        public const int WaitMost = 923;

        private static readonly Dictionary<long, List<EelLair>> found = new Dictionary<long, List<EelLair>>();
        private static readonly int[] wait = new int[Main.maxPlayers];

        public override void OnWorldLoad() => found.Clear();

        public override void OnWorldUnload() => found.Clear();

        public override void Unload() => EelWays.Unload();

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || SubworldKeeper.Current is not VastOcean)
                return;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead || player.ghost)
                    continue;

                if (--wait[player.whoAmI] > 0)
                    continue;

                wait[player.whoAmI] = Main.rand.Next(WaitLeast, WaitMost + 1);

                if (VastOcean.RowOf(player) >= VastOcean.SectionTop(8))
                    TryPut(player);
            }
        }

        private static List<EelLair> In(int sx, int sy)
        {
            long key = (long)sy << 32 | (uint)sx;

            if (!found.TryGetValue(key, out List<EelLair> list))
            {
                list = new List<EelLair>();
                EelLairs.Find(sx * Stretch, sy * Stretch, sx * Stretch + Stretch - 1, sy * Stretch + Stretch - 1, list);
                found[key] = list;
            }

            return list;
        }

        private static void TryPut(Player player)
        {
            int headType = ModContent.NPCType<CreviceEelHead>();
            int near = 0;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == headType && npc.Distance(player.Center) < NearTiles * 16f)
                    near++;
            }

            if (near >= MostNear)
                return;

            int px = (int)(player.Center.X / 16f), py = (int)(player.Center.Y / 16f);
            var choices = new List<EelLair>();

            for (int sy = (py - ReachTiles) / Stretch; sy <= (py + ReachTiles) / Stretch; sy++)
            {
                for (int sx = (px - ReachTiles) / Stretch; sx <= (px + ReachTiles) / Stretch; sx++)
                {
                    if (sx < 0 || sy < 0)
                        continue;

                    foreach (EelLair lair in In(sx, sy))
                    {
                        Vector2 mouth = lair.Mouth / 16f;
                        float dx = Math.Abs(mouth.X - px), dy = Math.Abs(mouth.Y - py);

                        if (dx * dx + dy * dy > ReachTiles * ReachTiles || dx <= UnseenAcross && dy <= UnseenUp)
                            continue;

                        // Only where the least of them fits (CreviceEelHead.Fits), and no eel lives.
                        if (CreviceEelHead.Fits(lair.Length, 0) && !Taken(lair, headType))
                            choices.Add(lair);
                    }
                }
            }

            if (choices.Count == 0)
                return;

            // A den: crevices with others free within GroupTiles of their mouths are three times
            // as likely to be chosen, and bring those others with them, two to four eels in all.
            var others = new int[choices.Count];

            for (int i = 0; i < choices.Count; i++)
            {
                for (int j = 0; j < choices.Count; j++)
                {
                    if (i != j && Vector2.DistanceSquared(choices[i].Mouth, choices[j].Mouth) < GroupTiles * GroupTiles * 256f)
                        others[i]++;
                }
            }

            int total = 0;

            for (int i = 0; i < choices.Count; i++)
                total += others[i] > 0 ? 3 : 1;

            int roll = Main.rand.Next(total), first = 0;

            for (int i = 0; i < choices.Count; i++)
            {
                roll -= others[i] > 0 ? 3 : 1;

                if (roll < 0)
                {
                    first = i;
                    break;
                }
            }

            EelLair seed = choices[first];
            var group = new List<EelLair> { seed };

            if (others[first] > 0)
            {
                int size = Main.rand.Next(LeastGroup, MostGroup + 1);
                var mates = choices.FindAll(l => !l.Same(seed) && Vector2.DistanceSquared(l.Mouth, seed.Mouth) < GroupTiles * GroupTiles * 256f);

                mates.Sort((a, b) => Vector2.DistanceSquared(a.Mouth, seed.Mouth).CompareTo(Vector2.DistanceSquared(b.Mouth, seed.Mouth)));

                for (int i = 0; i < mates.Count && group.Count < size; i++)
                    group.Add(mates[i]);
            }

            foreach (EelLair chosen in group)
                Put(chosen, headType);
        }

        /// <summary>
        /// One eel put lurking in this crevice, if the crevice is still there (the world may have
        /// changed since it was found): its length rolled, none to two pieces past the least, by
        /// request ("their length is randomized, base now to base + 2 segment"), shorter if its
        /// body would not fit the crevice (CreviceEelHead.Fits).
        /// </summary>
        private static void Put(EelLair lair, int headType)
        {
            if (!EelLairs.At(lair.X, lair.Y, lair.Dx, lair.Dy, out int length) || !CreviceEelHead.Fits(length, 0))
                return;

            lair = new EelLair(lair.X, lair.Y, lair.Dx, lair.Dy, length);
            int extra = Main.rand.Next(CreviceEelHead.MostExtra + 1);

            while (extra > 0 && !CreviceEelHead.Fits(length, extra))
                extra--;

            Vector2 mouth = lair.Mouth;
            int index = NPC.NewNPC(new Terraria.DataStructures.EntitySource_SpawnNPC("PisAndDag:CreviceEel"), (int)mouth.X, (int)mouth.Y + 12, headType, 0, 0f, 0f, extra);

            if (index < Main.maxNPCs && Main.npc[index].ModNPC is CreviceEelHead eel)
                eel.Settle(lair);
        }

        /// <summary>
        /// A crevice near this place (its mouth within <paramref name="reach"/> tiles) that no
        /// other eel lives in and this one's body fits, with the way to it from
        /// <paramref name="from"/> (world cells): the nearest by the way through the water of the
        /// nearest few as the crow flies. For a Crevice Eel that cannot get to its own.
        /// </summary>
        internal static bool Nearest(CreviceEelHead eel, Point from, int reach, List<Point> way, out EelLair found)
        {
            found = default;
            int headType = ModContent.NPCType<CreviceEelHead>();
            int tx = from.X + 1, ty = from.Y + 1;
            var near = new List<EelLair>();

            for (int sy = Math.Max(0, (ty - reach) / Stretch); sy <= (ty + reach) / Stretch; sy++)
            {
                for (int sx = Math.Max(0, (tx - reach) / Stretch); sx <= (tx + reach) / Stretch; sx++)
                {
                    foreach (EelLair lair in In(sx, sy))
                    {
                        Vector2 mouth = lair.Mouth / 16f;

                        if (Vector2.DistanceSquared(mouth, new Vector2(tx, ty)) <= reach * reach && CreviceEelHead.Fits(lair.Length, eel.Extra) && !Taken(lair, headType, eel))
                            near.Add(lair);
                    }
                }
            }

            near.Sort((a, b) => Vector2.DistanceSquared(a.Mouth / 16f, new Vector2(tx, ty)).CompareTo(Vector2.DistanceSquared(b.Mouth / 16f, new Vector2(tx, ty))));

            int best = int.MaxValue;
            var tried = new List<Point>();

            for (int i = 0; i < near.Count && i < 4; i++)
            {
                EelLair lair = near[i];

                // The world may have changed since it was found (it can be dug).
                if (!EelLairs.At(lair.X, lair.Y, lair.Dx, lair.Dy, out int length) || !CreviceEelHead.Fits(length, eel.Extra))
                    continue;

                lair = new EelLair(lair.X, lair.Y, lair.Dx, lair.Dy, length);

                if (EelWays.Find(from, CreviceEelHead.CellOf(lair.Mouth), tried) && tried.Count < best)
                {
                    best = tried.Count;
                    found = lair;
                    way.Clear();
                    way.AddRange(tried);
                }
            }

            return best < int.MaxValue;
        }

        /// <summary>An eel lives in this crevice already (but the one asking).</summary>
        private static bool Taken(EelLair lair, int headType, CreviceEelHead asking = null)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == headType && npc.ModNPC is CreviceEelHead eel && eel != asking && eel.Lives(lair))
                    return true;
            }

            return false;
        }
    }
}
