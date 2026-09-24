#if !PREVIEW
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// What the Kingdom of Grace keeps with the world once it is built.
    ///
    /// The guard map: one bit for every cell the builders laid a block in and one for every
    /// cell they laid a wall in, over the rectangle the kingdom was built across. A block or
    /// wall whose bit is set cannot be mined, hammered, blown up, swapped for another, or
    /// fitted with an actuator. Nothing a player puts down afterwards has a bit, so whatever
    /// they build inside the walls they can take up again.
    ///
    /// Ground and hangings are never given a bit (dirt, grass, stone, ash, cobweb, rope,
    /// chain, vines), and neither is anything placed as an object: furniture, platforms,
    /// paintings, doors, chests, signs.
    ///
    /// The few blocks the builders fitted with actuators, to open to a lever, open to a
    /// signal down a wire and to nothing else.
    ///
    /// The places the builders marked are kept by name for later code to find.
    ///
    /// Saved with the world, and sent to each player as they join.
    /// </summary>
    public class GraceWorld : ModSystem
    {
        /// <summary>The world rectangle the map covers. Empty when there is no kingdom.</summary>
        public static Rectangle Area { get; private set; }

        private static BitArray tiles, walls;

        /// <summary>Kept blocks that open to a signal, by cell.</summary>
        private static readonly HashSet<int> operable = new HashSet<int>();

        private static readonly List<(string name, Rectangle area)> marks = new List<(string name, Rectangle area)>();

        /// <summary>The places the builders marked, in world coordinates.</summary>
        public static IReadOnlyList<(string name, Rectangle area)> Marks => marks;

        /// <summary>The map as it is saved and sent. Dropped when a bit changes, and made again when next needed.</summary>
        private static byte[] packed;

        /// <summary>A map on its way from the server, in pieces.</summary>
        private static byte[] incoming;
        private static int incomingCount;

        /// <summary>True while a signal down a wire is working an actuator.</summary>
        private static bool signal;

        private const int Version = 1;

        /// <summary>The most of the map one packet carries. A packet may not pass 65535 bytes.</summary>
        private const int ChunkSize = 60000;

        /// <summary>How near the kingdom a meteor may not come down.</summary>
        private const int MeteorReach = 60;

        private enum Msg : byte
        {
            /// <summary>Client to server: send me the map.</summary>
            Ask,

            /// <summary>Server to client: a piece of the map.</summary>
            Map,

            /// <summary>Either way: this cell's bit is cleared.</summary>
            Loose,

            /// <summary>Client to server: I turned the key in a lock of the deep (GraceSeal).</summary>
            Unseal,
        }

        // ------------------------------------------------------------ what is kept

        /// <summary>Laid by the builders but not kept: what a traveller clears out of the way.</summary>
        private static readonly HashSet<int> LooseTiles = new HashSet<int>
        {
            TileID.Dirt, TileID.Grass, TileID.Stone, TileID.Ash, TileID.AshGrass,
            TileID.Cobweb, TileID.Rope, TileID.Chain, TileID.VineRope, TileID.LeafBlock,
        };

        /// <summary>The walls of the bare ground, where the builders cut into it, of the crack under the Crypt, and the leaves under the willows.</summary>
        private static readonly HashSet<int> LooseWalls = new HashSet<int>
        {
            WallID.DirtUnsafe, WallID.RocksUnsafe1, WallID.RocksUnsafe3,
            WallID.Cave4Unsafe, WallID.Cave5Unsafe, WallID.Cave8Unsafe, WallID.LivingLeaf,
        };

        /// <summary>A block of this type, laid by the builders, is kept.</summary>
        public static bool Keeps(int type) => !LooseTiles.Contains(type);

        /// <summary>A wall of this type, laid by the builders, is kept.</summary>
        public static bool KeepsWall(int wall) => wall > 0 && !LooseWalls.Contains(wall);

        private static int Cell(int i, int j) => (i - Area.X) * Area.Height + (j - Area.Y);

        /// <summary>The block here was laid by the builders and may not be broken.</summary>
        public static bool TileKept(int i, int j)
        {
            if (tiles == null || !Area.Contains(i, j))
                return false;

            return tiles[Cell(i, j)] && Main.tile[i, j].HasTile;
        }

        /// <summary>The wall here was laid by the builders and may not be broken.</summary>
        public static bool WallKept(int i, int j)
        {
            if (walls == null || !Area.Contains(i, j))
                return false;

            return walls[Cell(i, j)] && Main.tile[i, j].WallType != 0;
        }

        /// <summary>A kept block that opens to a signal, and a signal is passing.</summary>
        public static bool OpensNow(int i, int j) => signal && tiles != null && Area.Contains(i, j) && operable.Contains(Cell(i, j));

        // ------------------------------------------------------------ the world's own hooks

        public override void Load()
        {
            On_WorldGen.meteor += MeteorElsewhere;
            On_WorldGen.PlaceActuator += NoActuatorsOnKept;
            On_Wiring.ActuateForced += BySignal;
        }

        public override void ClearWorld() => Reset();

        public override void OnWorldUnload() => Reset();

        private static void Reset()
        {
            Area = Rectangle.Empty;
            tiles = null;
            walls = null;
            operable.Clear();
            marks.Clear();
            packed = null;
            incoming = null;
            incomingCount = 0;
            signal = false;
        }

        /// <summary>No meteor comes down on the kingdom or near it. The game rolls again somewhere else.</summary>
        private static bool MeteorElsewhere(On_WorldGen.orig_meteor orig, int i, int j, bool ignorePlayers)
        {
            if (tiles != null)
            {
                Rectangle near = Area;
                near.Inflate(MeteorReach, MeteorReach);
                if (near.Contains(i, j))
                    return false;
            }

            return orig(i, j, ignorePlayers);
        }

        /// <summary>
        /// No actuator on a kept block. An actuator would do nothing there (a kept block turns
        /// off only to its own wire), so the player keeps it.
        /// </summary>
        private static bool NoActuatorsOnKept(On_WorldGen.orig_PlaceActuator orig, int i, int j) =>
            !TileKept(i, j) && orig(i, j);

        /// <summary>
        /// A signal down a wire reaches an actuator through here, and nothing else does: the
        /// actuation rod goes through Wiring.Actuate. While it runs, the blocks the builders
        /// meant to open may.
        /// </summary>
        private static void BySignal(On_Wiring.orig_ActuateForced orig, int i, int j)
        {
            bool was = signal;
            signal = true;

            try
            {
                orig(i, j);
            }
            finally
            {
                signal = was;
            }
        }

        // ------------------------------------------------------------ made at generation

        /// <summary>Takes the map off the canvas the kingdom was built through, when the building is done.</summary>
        public static void Record(GraceWorldCanvas canvas)
        {
            Reset();

            if (canvas.MinX > canvas.MaxX || canvas.MinY > canvas.MaxY)
                return;

            int x0 = Math.Max(0, canvas.MinX), x1 = Math.Min(Main.maxTilesX - 1, canvas.MaxX);
            int y0 = Math.Max(0, canvas.MinY), y1 = Math.Min(Main.maxTilesY - 1, canvas.MaxY);
            Area = new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);

            tiles = new BitArray(Area.Width * Area.Height);
            walls = new BitArray(Area.Width * Area.Height);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Tile t = Main.tile[x, y];
                    int k = Cell(x, y);

                    if (t.HasTile && canvas.LaidTile(x, y))
                    {
                        tiles[k] = true;
                        if (t.HasActuator && canvas.Operable(x, y))
                            operable.Add(k);
                    }

                    if (t.WallType != 0 && canvas.LaidWall(x, y))
                        walls[k] = true;
                }
            }

            marks.AddRange(canvas.Marks);
            packed = Pack();
        }

        // ------------------------------------------------------------ saved

        public override void SaveWorldData(TagCompound tag)
        {
            if (tiles == null)
                return;

            packed ??= Pack();
            tag["map"] = packed;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            if (tag.ContainsKey("map"))
                Unpack(tag.GetByteArray("map"));
        }

        private static byte[] Pack()
        {
            using (var raw = new MemoryStream())
            {
                using (var w = new BinaryWriter(new DeflateStream(raw, CompressionLevel.Optimal, true)))
                {
                    w.Write(Version);
                    w.Write(Area.X);
                    w.Write(Area.Y);
                    w.Write(Area.Width);
                    w.Write(Area.Height);
                    WriteBits(w, tiles);
                    WriteBits(w, walls);

                    w.Write(operable.Count);
                    foreach (int k in operable)
                        w.Write(k);

                    w.Write(marks.Count);
                    foreach ((string name, Rectangle r) in marks)
                    {
                        w.Write(name);
                        w.Write(r.X);
                        w.Write(r.Y);
                        w.Write(r.Width);
                        w.Write(r.Height);
                    }
                }

                return raw.ToArray();
            }
        }

        private static void WriteBits(BinaryWriter w, BitArray bits)
        {
            byte[] bytes = new byte[(bits.Length + 7) / 8];
            bits.CopyTo(bytes, 0);
            w.Write(bits.Length);
            w.Write(bytes);
        }

        private static BitArray ReadBits(BinaryReader r)
        {
            int length = r.ReadInt32();
            if (length < 0)
                throw new InvalidDataException("negative length");

            int count = (length + 7) / 8;
            byte[] bytes = r.ReadBytes(count);
            if (bytes.Length != count)
                throw new EndOfStreamException();

            return new BitArray(bytes) { Length = length };
        }

        /// <summary>Puts a saved or sent map back. A map that cannot be read leaves nothing guarded, and says so.</summary>
        private static void Unpack(byte[] data)
        {
            Reset();

            try
            {
                using (var r = new BinaryReader(new DeflateStream(new MemoryStream(data), CompressionMode.Decompress)))
                {
                    int version = r.ReadInt32();
                    if (version != Version)
                        throw new InvalidDataException($"version {version}");

                    int x = r.ReadInt32(), y = r.ReadInt32(), width = r.ReadInt32(), height = r.ReadInt32();
                    if (width <= 0 || height <= 0 || x < 0 || y < 0 || x + width > Main.maxTilesX || y + height > Main.maxTilesY)
                        throw new InvalidDataException($"area {x}, {y}, {width} by {height} is not inside the world");

                    BitArray t = ReadBits(r), w = ReadBits(r);
                    if (t.Length != width * height || w.Length != width * height)
                        throw new InvalidDataException("the map is not the size of its area");

                    var ops = new List<int>();
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                        ops.Add(r.ReadInt32());

                    var named = new List<(string name, Rectangle area)>();
                    int m = r.ReadInt32();
                    for (int i = 0; i < m; i++)
                    {
                        string name = r.ReadString();
                        int mx = r.ReadInt32(), my = r.ReadInt32(), mw = r.ReadInt32(), mh = r.ReadInt32();
                        named.Add((name, new Rectangle(mx, my, mw, mh)));
                    }

                    Area = new Rectangle(x, y, width, height);
                    tiles = t;
                    walls = w;
                    operable.UnionWith(ops);
                    marks.AddRange(named);
                    packed = data;
                }
            }
            catch (Exception e)
            {
                Reset();
                ModContent.GetInstance<GraceWorld>().Mod.Logger.Warn(
                    "The Kingdom of Grace's guard map could not be read, so nothing in the kingdom is protected in this world.", e);
            }
        }

        // ------------------------------------------------------------ a bit let go

        /// <summary>
        /// A player put something down in a cell with a bit set. The builders' block or wall
        /// there must already have gone some way the guard did not see, so what stands there
        /// now is the player's, and the bit goes.
        /// </summary>
        internal static void Placed(int i, int j, bool wall)
        {
            if (Loosen(i, j, wall) && Main.netMode == NetmodeID.MultiplayerClient)
                SendLoose(ModContent.GetInstance<GraceWorld>().Mod, i, j, wall, -1);
        }

        private static bool Loosen(int i, int j, bool wall)
        {
            BitArray map = wall ? walls : tiles;
            if (map == null || !Area.Contains(i, j))
                return false;

            int k = Cell(i, j);
            if (!map[k])
                return false;

            map[k] = false;
            if (!wall)
                operable.Remove(k);

            packed = null;
            return true;
        }

        // ------------------------------------------------------------ sent

        /// <summary>A player has come into the world: ask the server for its map.</summary>
        internal static void AskForMap(Mod mod)
        {
            ModPacket p = mod.GetPacket();
            p.Write((byte)PADPacket.Grace);
            p.Write((byte)Msg.Ask);
            p.Send();
        }

        private static void SendMap(Mod mod, int to)
        {
            byte[] data = tiles == null ? Array.Empty<byte>() : (packed ??= Pack());
            int offset = 0;

            // An empty map is still sent, as one empty piece, so a client that had a map
            // from somewhere else lets go of it.
            do
            {
                int count = Math.Min(ChunkSize, data.Length - offset);
                ModPacket p = mod.GetPacket(count + 16);
                p.Write((byte)PADPacket.Grace);
                p.Write((byte)Msg.Map);
                p.Write(data.Length);
                p.Write(offset);
                p.Write(count);
                p.Write(data, offset, count);
                p.Send(to);
                offset += count;
            }
            while (offset < data.Length);
        }

        private static void ReceiveMap(BinaryReader reader)
        {
            int total = reader.ReadInt32(), offset = reader.ReadInt32(), count = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(count);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            if (total <= 0)
            {
                Reset();
                return;
            }

            if (offset == 0)
            {
                incoming = new byte[total];
                incomingCount = 0;
            }

            if (incoming == null || incoming.Length != total || offset < 0 || offset + bytes.Length > total)
            {
                incoming = null;
                return;
            }

            Buffer.BlockCopy(bytes, 0, incoming, offset, bytes.Length);
            incomingCount += bytes.Length;

            if (incomingCount >= total)
            {
                byte[] data = incoming;
                incoming = null;
                Unpack(data);
            }
        }

        private static void SendLoose(Mod mod, int i, int j, bool wall, int ignore)
        {
            ModPacket p = mod.GetPacket();
            p.Write((byte)PADPacket.Grace);
            p.Write((byte)Msg.Loose);
            p.Write(i);
            p.Write(j);
            p.Write(wall);
            p.Send(-1, ignore);
        }

        /// <summary>Tells the server this player turned the Reliquary Key in a lock.</summary>
        internal static void SendUnseal(Mod mod)
        {
            ModPacket p = mod.GetPacket();
            p.Write((byte)PADPacket.Grace);
            p.Write((byte)Msg.Unseal);
            p.Send();
        }

        /// <summary>Everything this system is sent. The first byte, which said it was for this system, has been read.</summary>
        internal static void HandlePacket(Mod mod, BinaryReader reader, int from)
        {
            var msg = (Msg)reader.ReadByte();

            switch (msg)
            {
                case Msg.Ask:
                    if (Main.netMode == NetmodeID.Server)
                        SendMap(mod, from);
                    break;

                case Msg.Map:
                    ReceiveMap(reader);
                    break;

                case Msg.Loose:
                {
                    int i = reader.ReadInt32(), j = reader.ReadInt32();
                    bool wall = reader.ReadBoolean();
                    if (Loosen(i, j, wall) && Main.netMode == NetmodeID.Server)
                        SendLoose(mod, i, j, wall, from);
                    break;
                }

                case Msg.Unseal:
                    GraceSeal.Heard(from);
                    break;

                default:
                    mod.Logger.Warn($"Grace: a packet of unknown kind {(byte)msg} came from {from} and was ignored.");
                    break;
            }
        }
    }

    /// <summary>Asks for the guard map on joining a server.</summary>
    public class GraceGuardPlayer : ModPlayer
    {
        public override void OnEnterWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                GraceWorld.AskForMap(Mod);
        }
    }

    /// <summary>The kept blocks: no pickaxe, hammer, bomb or block swap moves them.</summary>
    public class GraceGuardTile : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) =>
            !GraceWorld.TileKept(i, j) || GraceWorld.OpensNow(i, j);

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (GraceWorld.TileKept(i, j))
                fail = true;
        }

        public override bool CanExplode(int i, int j, int type) => !GraceWorld.TileKept(i, j);

        public override bool Slope(int i, int j, int type) => !GraceWorld.TileKept(i, j);

        public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !GraceWorld.TileKept(i, j);

        public override void PlaceInWorld(int i, int j, int type, Item item) => GraceWorld.Placed(i, j, false);
    }

    /// <summary>The kept walls: no hammer, bomb or wall swap moves them.</summary>
    public class GraceGuardWall : GlobalWall
    {
        public override void KillWall(int i, int j, int type, ref bool fail)
        {
            if (GraceWorld.WallKept(i, j))
                fail = true;
        }

        public override bool CanExplode(int i, int j, int type) => !GraceWorld.WallKept(i, j);

        /// <summary>A wall is only ever put where there is none, or in place of one; this refuses the second.</summary>
        public override bool CanPlace(int i, int j, int type) => !GraceWorld.WallKept(i, j);

        public override void PlaceInWorld(int i, int j, int type, Item item) => GraceWorld.Placed(i, j, true);
    }
}
#endif
