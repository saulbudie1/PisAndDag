using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The waygate on the beach, by request ("For now create one that is generated in the
    /// middle of the beach"): a small shrine in the middle of the dry sand between the
    /// water's edge and where the sand ends, a dais of marble two steps high with the waygate
    /// on it (Waygate) and two broken columns either side, on ground made level for it. The
    /// same old builders' as the platform it leads to (VastOcean): the same marble, the same
    /// slab, the same broken columns, worn lower on the side the sea comes from.
    ///
    /// WHICH BEACH. A Grand world has one sea, at the Dungeon's end (WorldMirror); any other
    /// world has two, and the shrine is on the Dungeon's side of it there too, so that it is
    /// the same beach in every world. If that side has no beach to stand on, the other.
    ///
    /// WHERE ON IT. The beach is walked from the world's edge in, over the water, to the first
    /// dry column (the water's edge), and on over its sand, and the patches of shell the game
    /// lays in it, to the first column whose ground is neither (its end); the shrine is put as
    /// near the middle of that as it will go, trying further and further either way: on ground
    /// no more than four rows from flat, with nothing on it but sand and earth and what grows
    /// or lies on them (plants, trees, cacti, sea oats, shells, the game's piles), and none of
    /// it in water. The first twenty-four columns up from the water's edge are left for the man
    /// who waits there after the Eye and his fire (PugreamPresence) where the beach is wide
    /// enough; where it is not, twelve.
    ///
    /// WHEN. In a new world, as the last of its making (PostWorldGen). In a world made
    /// before there was one, the first time it is loaded, before anybody stands in it. Once,
    /// either way: the place is saved with the world (<see cref="Gate"/>), sent to every
    /// client, and put down again only once it is taken up by hand (/subworld ungate): the
    /// next time the world is loaded, or at once with /subworld gate raise. If no place will
    /// do, it is tried again the next time the world is loaded, and client.log says so.
    ///
    /// It is all written straight into the tiles, the way a world's own making writes them,
    /// the ground under it framed again after, and in multiplayer the square of it sent.
    /// </summary>
    public class BeachWaygate : ModSystem
    {
        /// <summary>The waygate's top left tile, or (-1, -1) if the world has none yet.</summary>
        public static Point16 Gate = new Point16(-1, -1);

        /// <summary>Columns in from the world's edge before the walk starts: the edge itself is never seen.</summary>
        private const int EdgeMargin = 50;

        /// <summary>Rows from the top of the world the walk down each column starts at.</summary>
        private const int SkyTop = 10;

        /// <summary>The furthest the beach is walked, in columns from the water's edge.</summary>
        private const int LongestBeach = 400;

        /// <summary>Kept clear for Pugream and his fire, up from the water's edge; and as little as will do where the beach is narrow.</summary>
        private const int KeptForHim = 24;
        private const int KeptForHimAtLeast = 12;

        /// <summary>No more than this far from flat across the shrine's ground.</summary>
        private const int MostUneven = 4;

        // The shrine, from the waygate's left column (G) and the row its ground's top is (F):
        // ground made level from G - 9 to G + 10; a dais of marble, its lower step G - 3 to
        // G + 4 on row F - 1, its upper G - 2 to G + 3 on row F - 2; the waygate on that, rows
        // F - 4 and F - 3; broken columns of marble on a foot of slab, at G - 7 and G - 6 and at
        // G + 7 and G + 8, five, four, three and two high with the sea to the right, two, three,
        // four and five with it to the left. Everything over the ground from G - 9 to G + 10,
        // nine rows up, cleared first.
        private const int Reach = 9;
        private const int ClearRows = 9;

        public override void ClearWorld()
        {
            Gate = new Point16(-1, -1);
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Gate.X >= 0)
            {
                tag["PADWaygateX"] = (int)Gate.X;
                tag["PADWaygateY"] = (int)Gate.Y;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Gate = tag.ContainsKey("PADWaygateX") && tag.ContainsKey("PADWaygateY")
                ? new Point16(tag.GetInt("PADWaygateX"), tag.GetInt("PADWaygateY"))
                : new Point16(-1, -1);
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Gate.X);
            writer.Write(Gate.Y);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Gate = new Point16(reader.ReadInt16(), reader.ReadInt16());
        }

        public override void PostWorldGen()
        {
            Raise(true);
        }

        public override void PostWorldLoad()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || Subworld.Active || Gate.X >= 0)
                return;

            Raise(false);
        }

        /// <summary>
        /// The space the shrine takes (its ground and all over it), with this much more each
        /// way; empty if there is none. For keeping other things off it (PugreamPresence).
        /// </summary>
        public static Rectangle Footprint(int margin)
        {
            if (Gate.X < 0)
                return Rectangle.Empty;

            int floor = Gate.Y + Waygate.Tall + 2;

            return new Rectangle(Gate.X - Reach - margin, floor - ClearRows - margin, Reach * 2 + 2 + margin * 2, ClearRows + 1 + margin * 2);
        }

        /// <summary>Puts the shrine down on this world's beach, if a place for it can be found; says so in client.log either way.</summary>
        internal static bool Raise(bool making)
        {
            int side = DungeonSide();

            if (!TryBeach(side, out int left, out int floor))
            {
                side = -side;

                if (!TryBeach(side, out left, out floor))
                {
                    ModContent.GetInstance<BeachWaygate>().Mod.Logger.Warn("Waygate: no beach on either side of this world has room for its shrine. It will be looked for again the next time the world is loaded.");
                    return false;
                }
            }

            Build(left, floor, (ushort)ModContent.TileType<Waygate>(), -side, making);

            Gate = new Point16(left, floor - 2 - Waygate.Tall);

            ModContent.GetInstance<BeachWaygate>().Mod.Logger.Info($"Waygate: put down on the {(left < Main.maxTilesX / 2 ? "left" : "right")} beach, its top left tile at {Gate.X}, {Gate.Y}.");

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);

            return true;
        }

        /// <summary>The sea at the Dungeon's end: -1 the left edge of the world, 1 the right.</summary>
        private static int DungeonSide()
        {
            if (ExtraLargeWorld.Active)
                return WorldMirror.Flipped ? 1 : -1;

            return Main.dungeonX < Main.maxTilesX / 2 ? -1 : 1;
        }

        // ---- finding the place --------------------------------------------------------------------

        /// <summary>
        /// The beach on this side, and the place on it the shrine goes: its waygate's left
        /// column and the row its ground's top is. False if this side has no beach, or none
        /// with room.
        /// </summary>
        public static bool TryBeach(int side, out int gateLeft, out int floor)
        {
            gateLeft = -1;
            floor = -1;

            if (!Walk(side, out int shore, out int end))
                return false;

            int inward = -side;
            int span = Math.Abs(end - shore);
            int middle = shore + inward * (span / 2);
            int kept = span >= (KeptForHim + Reach * 2 + 2) * 2 ? KeptForHim : KeptForHimAtLeast;

            for (int step = 0; step <= span / 2 + Reach; step++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    if (step == 0 && sign > 0)
                        continue;

                    int g = middle + sign * step;

                    // Its nearest column to the water no nearer than what is kept for him.
                    int nearest = inward > 0 ? g - Reach : g + 1 + Reach;

                    if ((nearest - shore) * inward < kept)
                        continue;

                    if (Fits(g, out floor))
                    {
                        gateLeft = g;
                        return true;
                    }
                }
            }

            floor = -1;
            return false;
        }

        /// <summary>The water's edge on this side (its first dry column) and the beach's end (its last column of sand or shell), walking in from the world's edge.</summary>
        private static bool Walk(int side, out int shore, out int end)
        {
            shore = -1;
            end = -1;

            int edge = side < 0 ? EdgeMargin : Main.maxTilesX - 1 - EdgeMargin;
            int inward = -side;
            bool water = false;

            for (int i = 0; i < Main.maxTilesX / 3; i++)
            {
                int x = edge + inward * i;

                if (!Surface(x, out int row, out bool wet))
                    continue;

                if (wet)
                {
                    water = true;
                    continue;
                }

                // Land before any sea: no beach on this side.
                if (!water)
                    return false;

                shore = x;
                end = x;

                for (int j = 1; j < LongestBeach; j++)
                {
                    int sx = x + inward * j;

                    if (!Surface(sx, out int sandRow, out bool damp) || damp)
                        break;

                    if (!Beach(Main.tile[sx, sandRow].TileType))
                        break;

                    end = sx;
                }

                return Math.Abs(end - shore) >= Reach * 2 + 2;
            }

            return false;
        }

        /// <summary>
        /// What a beach is made of at the top: sand of every kind, and the patches of shell the
        /// game lays in it (ShellPile, which is not sand to the game). A world whose beach had
        /// such a patch fifteen columns from the water was walked to there and no further, and
        /// had no shrine.
        /// </summary>
        private static bool Beach(int type) => Main.tileSand[type] || type == TileID.ShellPile;

        /// <summary>The first thing down this column from the sky: water, or something to stand on.</summary>
        private static bool Surface(int x, out int row, out bool wet)
        {
            row = 0;
            wet = false;

            if (x < 1 || x >= Main.maxTilesX - 1)
                return false;

            int bottom = Math.Min(Main.maxTilesY - 20, (int)Main.worldSurface + 60);

            for (int y = SkyTop; y < bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.LiquidAmount > 0)
                {
                    row = y;
                    wet = true;
                    return true;
                }

                if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    row = y;
                    return true;
                }
            }

            return false;
        }

        /// <summary>The shrine goes with its waygate's left column here: the ground near enough flat, dry, and nothing on it but what the world grew. The row its ground will be levelled to.</summary>
        private static bool Fits(int g, out int floor)
        {
            floor = -1;

            int[] rows = new int[Reach * 2 + 2];
            int lowest = int.MinValue;
            int highest = int.MaxValue;

            for (int i = 0; i < rows.Length; i++)
            {
                if (!Surface(g - Reach + i, out int row, out bool wet) || wet)
                    return false;

                rows[i] = row;
                lowest = Math.Max(lowest, row);
                highest = Math.Min(highest, row);
            }

            if (lowest - highest > MostUneven)
                return false;

            // Level to the row most of it is on.
            int[] sorted = (int[])rows.Clone();
            Array.Sort(sorted);
            floor = sorted[sorted.Length / 2];

            for (int x = g - Reach; x <= g + 1 + Reach; x++)
            {
                for (int y = floor - ClearRows; y <= Math.Max(floor, lowest) + 1; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.LiquidAmount > 0 && y < floor)
                        return false;

                    if (tile.WallType != 0 && y < floor)
                        return false;

                    if (tile.HasTile && !Natural(tile.TileType) && !Grown(tile.TileType))
                        return false;
                }
            }

            return true;
        }

        /// <summary>Ground the world made: sand of every kind, earth, clay, stone, mud, snow, and shell.</summary>
        public static bool Natural(int type)
        {
            switch (type)
            {
                case TileID.Sand:
                case TileID.Ebonsand:
                case TileID.Pearlsand:
                case TileID.Crimsand:
                case TileID.HardenedSand:
                case TileID.CorruptHardenedSand:
                case TileID.CrimsonHardenedSand:
                case TileID.HallowHardenedSand:
                case TileID.Sandstone:
                case TileID.CorruptSandstone:
                case TileID.CrimsonSandstone:
                case TileID.HallowSandstone:
                case TileID.Dirt:
                case TileID.Grass:
                case TileID.CorruptGrass:
                case TileID.HallowedGrass:
                case TileID.CrimsonGrass:
                case TileID.ClayBlock:
                case TileID.Stone:
                case TileID.Mud:
                case TileID.JungleGrass:
                case TileID.SnowBlock:
                case TileID.IceBlock:
                case TileID.Silt:
                case TileID.ShellPile:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// What grows or lies on the world's ground by itself, the ambient objects among it:
        /// whatever a blade cuts (plants, grass, vines), trees and their saplings, palms, cacti,
        /// sea oats, shells, and the game's piles of stones and bones. Not its echo piles, which
        /// are a player's (the Rubblemaker's), and not pots, which hold something.
        /// </summary>
        public static bool Grown(int type)
        {
            if (type == TileID.Pots)
                return false;

            if (Main.tileCut[type])
                return true;

            switch (type)
            {
                case TileID.Trees:
                case TileID.PalmTree:
                case TileID.Cactus:
                case TileID.Saplings:
                case TileID.SeaOats:
                case TileID.BeachPiles:
                case TileID.SmallPiles:
                case TileID.LargePiles:
                case TileID.LargePiles2:
                case TileID.Sunflower:
                case TileID.Cattail:
                case TileID.DyePlants:
                    return true;
            }

            return false;
        }

        // ---- building it ------------------------------------------------------------------------------

        /// <summary>
        /// The shrine, its waygate's left column at <paramref name="g"/> and its ground's top on
        /// row <paramref name="floor"/>: what is over the ground cleared away (a tree or a pile
        /// all of it, wherever it reaches), the ground levelled to that row, the dais, the
        /// columns and the waygate, the columns the lower on the sea's side (<paramref name="inward"/>
        /// is 1 if the land is to the right, -1 if to the left). The waygate's type is given, so
        /// that the preview can build it with the game alone.
        /// </summary>
        public static void Build(int g, int floor, ushort gateType, int inward, bool making)
        {
            int left = g - Reach;
            int right = g + 1 + Reach;

            // Clear: everything grown over the ground, whole.
            for (int x = left; x <= right; x++)
            {
                for (int y = floor - ClearRows; y < floor; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && Grown(tile.TileType))
                        Uproot(x, y, tile.TileType);
                }
            }

            // Level: cut what stands over the row down to it, fill what is under it up to it.
            for (int x = left; x <= right; x++)
            {
                for (int y = floor - ClearRows; y < floor; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && Natural(tile.TileType))
                        tile.ClearTile();
                }

                ushort ground = TileID.Sand;

                for (int y = floor; y < floor + 12; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && Main.tileSolid[tile.TileType])
                    {
                        ground = Natural(tile.TileType) ? tile.TileType : ground;
                        break;
                    }
                }

                for (int y = floor; y < floor + 12; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && Main.tileSolid[tile.TileType])
                        break;

                    if (tile.HasTile && Grown(tile.TileType))
                        Uproot(x, y, tile.TileType);

                    tile.HasTile = true;
                    tile.TileType = ground;
                    tile.LiquidAmount = 0;
                }

                // The ground's top whole: a slope or a half block would not hold the dais's edge level.
                Tile top = Main.tile[x, floor];

                top.Slope = SlopeType.Solid;
                top.IsHalfBlock = false;
            }

            // The dais.
            for (int x = g - 3; x <= g + 4; x++)
                Lay(x, floor - 1, TileID.MarbleBlock);

            for (int x = g - 2; x <= g + 3; x++)
                Lay(x, floor - 2, TileID.MarbleBlock);

            // The broken columns, on a foot of slab, lower and lower toward the sea; the top of
            // the lower of each pair broken off at half a block.
            int[] at = { g - 7, g - 6, g + 7, g + 8 };
            int[] high = inward < 0 ? new[] { 5, 4, 3, 2 } : new[] { 2, 3, 4, 5 };

            for (int c = 0; c < at.Length; c++)
                Column(at[c], floor, high[c]);

            for (int pair = 0; pair < 2; pair++)
            {
                int lower = high[pair * 2] < high[pair * 2 + 1] ? pair * 2 : pair * 2 + 1;
                Tile cap = Main.tile[at[lower], floor - high[lower]];

                cap.IsHalfBlock = true;
            }

            if (gateType != 0)
                Waygate.Put(g, floor - 2 - Waygate.Tall, gateType);

            if (making)
                return;

            WorldGen.RangeFrame(left - 2, floor - ClearRows - 2, right + 2, floor + 13);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, left - 2, floor - ClearRows - 2, right - left + 5, ClearRows + 16);
        }

        private static void Lay(int x, int y, ushort type)
        {
            Tile tile = Main.tile[x, y];

            tile.ClearTile();
            tile.HasTile = true;
            tile.TileType = type;
            tile.LiquidAmount = 0;
        }

        private static void Column(int x, int floor, int high)
        {
            Lay(x, floor - 1, TileID.StoneSlab);

            for (int y = floor - 2; y >= floor - high; y--)
                Lay(x, y, TileID.MarbleBlock);
        }

        /// <summary>
        /// Takes up the thing grown here whole: every tile of its kind joined to this one, as
        /// far as a tree's crown or roots, a cactus's arms or a pile's width reach. No item comes
        /// of it, and no sound: nobody is there to hear it.
        /// </summary>
        public static void Uproot(int x, int y, ushort type)
        {
            int reach = type == TileID.Trees || type == TileID.PalmTree || type == TileID.Cactus ? 40 : 4;
            var seen = new HashSet<long>();
            var open = new Stack<Point>();

            open.Push(new Point(x, y));

            while (open.Count > 0)
            {
                Point at = open.Pop();

                if (Math.Abs(at.X - x) > reach || Math.Abs(at.Y - y) > reach || !WorldGen.InWorld(at.X, at.Y, 2))
                    continue;

                if (!seen.Add(at.X * 100000L + at.Y))
                    continue;

                Tile tile = Main.tile[at.X, at.Y];

                if (!tile.HasTile || tile.TileType != type)
                    continue;

                tile.ClearTile();

                open.Push(new Point(at.X + 1, at.Y));
                open.Push(new Point(at.X - 1, at.Y));
                open.Push(new Point(at.X, at.Y + 1));
                open.Push(new Point(at.X, at.Y - 1));
            }
        }
    }
}
