using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The ground the Queen Bee needs, found or made: honey to come up out of and to go
    /// down into, and a flat piece of hive to leave her larva on.
    ///
    /// HONEY. A surface of it: three honey tiles in a row with nothing wet or solid over
    /// them, at least two deep in the middle, and room above for her to come out. The
    /// nearest to a point, in a box around it.
    ///
    /// THE POOL. Where there is no honey near enough, she makes some: a bucket of hive
    /// sunk into the ground beside the player, thirty-two tiles of honey in four rows,
    /// ten, eight, eight and six wide, so it narrows as it goes down. Its rim is flush
    /// with the ground and runs flat three tiles out either side, which is a piece of
    /// hive a larva can be left on. Only dug into plain ground (no dungeon or temple
    /// brick, no furniture, no chests, no other liquid), with open air over it.
    ///
    /// THE NEST. Where a larva can go: three tiles of whole, flat hive with nothing in
    /// the three by three over them but honey or air. Failing any hive near, the same
    /// on any flat, solid ground.
    ///
    /// All of it the server's: the tiles it changes are sent on.
    /// </summary>
    public static class QueenBeeGround
    {
        // ---- honey -------------------------------------------------------------------

        /// <summary>A tile with enough honey in it to count, and nothing solid in the way.</summary>
        private static bool Honey(Tile tile) =>
            tile.LiquidAmount >= 128 && tile.LiquidType == LiquidID.Honey && !Blocks(tile);

        /// <summary>Solid, and not a platform.</summary>
        private static bool Blocks(Tile tile) =>
            tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];

        /// <summary>Whether (x, y) is the middle of a surface of honey she can come out of.</summary>
        private static bool SurfaceAt(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 12))
                return false;

            for (int i = -1; i <= 1; i++)
            {
                if (!Honey(Main.tile[x + i, y]))
                    return false;

                Tile above = Main.tile[x + i, y - 1];

                if (above.LiquidAmount > 0 || Blocks(above))
                    return false;
            }

            // Two deep at least, and room over it.
            if (!Honey(Main.tile[x, y + 1]))
                return false;

            for (int k = 2; k <= 4; k++)
            {
                if (Blocks(Main.tile[x, y - k]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The nearest surface of honey to <paramref name="near"/>, within this many tiles
        /// either way: the world point in the middle of its top.
        /// </summary>
        public static bool FindHoney(Vector2 near, int reachX, int reachY, out Vector2 surface)
        {
            surface = default;

            int cx = (int)(near.X / 16f);
            int cy = (int)(near.Y / 16f);
            float best = float.MaxValue;
            bool found = false;

            for (int x = cx - reachX; x <= cx + reachX; x++)
            {
                for (int y = cy - reachY; y <= cy + reachY; y++)
                {
                    if (!SurfaceAt(x, y))
                        continue;

                    Vector2 at = new Vector2(x * 16f + 8f, Level(x, y));
                    float distance = Vector2.DistanceSquared(at, near);

                    if (distance < best)
                    {
                        best = distance;
                        surface = at;
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>The top of the honey in a tile, in world pixels. Liquid fills a tile from the bottom.</summary>
        private static float Level(int x, int y) => y * 16f + (255 - Main.tile[x, y].LiquidAmount) / 255f * 16f;

        // ---- the pool ----------------------------------------------------------------

        /// <summary>How wide the bucket is, row by row from the top. Thirty-two tiles of honey.</summary>
        private static readonly int[] PoolWidths = { 10, 8, 8, 6 };

        /// <summary>The hive around it, from the middle column (tiles), and the rim out either side.</summary>
        private const int ShellLeft = -6;
        private const int ShellRight = 5;
        private const int RimLeft = -8;
        private const int RimRight = 7;

        /// <summary>Open air it wants over itself, in tiles.</summary>
        private const int ClearAbove = 6;

        /// <summary>How far to either side of the player a pool is tried, nearest the way they face first.</summary>
        private static readonly int[] PoolOffsets = { 18, 24, 30, 12, 36, 44, 52 };

        /// <summary>Where the ground is looked for, from the player's feet: this far up, and this far down.</summary>
        private const int GroundUp = 8;
        private const int GroundDown = 16;

        /// <summary>
        /// Digs the bucket beside the player, filled, and gives the middle of its top.
        /// False if there is nowhere plain and flat enough near them.
        /// </summary>
        public static bool BuildPool(Player player, out Vector2 surface)
        {
            surface = default;

            if (player == null || !player.active)
                return false;

            int px = (int)(player.Center.X / 16f);
            int feet = (int)((player.position.Y + player.height) / 16f);
            int face = player.direction >= 0 ? 1 : -1;

            foreach (int step in PoolOffsets)
            {
                for (int side = 0; side < 2; side++)
                {
                    int c = px + (side == 0 ? face : -face) * step;

                    if (!PoolSite(c, feet, out int s))
                        continue;

                    DigPool(c, s);

                    // Even widths: the middle is the line between c - 1 and c.
                    surface = new Vector2(c * 16f, s * 16f);
                    return true;
                }
            }

            return false;
        }

        /// <summary>The top row of solid ground in this column near the feet, or -1.</summary>
        private static int Ground(int x, int feet)
        {
            for (int y = feet - GroundUp; y <= feet + GroundDown; y++)
            {
                if (!WorldGen.InWorld(x, y, 12))
                    return -1;

                if (Blocks(Main.tile[x, y]) && !Blocks(Main.tile[x, y - 1]))
                    return y;
            }

            return -1;
        }

        /// <summary>Earth, stone and the like, that may be dug out or built over; or nothing.</summary>
        private static bool Plain(Tile tile)
        {
            if (!tile.HasTile)
                return true;

            ushort type = tile.TileType;

            if (!Main.tileSolid[type] || Main.tileSolidTop[type] || Main.tileFrameImportant[type])
                return false;

            return type != TileID.BlueDungeonBrick && type != TileID.GreenDungeonBrick && type != TileID.PinkDungeonBrick
                && type != TileID.LihzahrdBrick;
        }

        /// <summary>
        /// Whether the bucket can go with its middle at column <paramref name="c"/>: the
        /// ground under all sixteen columns of it within a tile of level (<paramref name="s"/>,
        /// its top), only plain ground where it goes, no other liquid in it, and nothing
        /// but grass and the like over it.
        /// </summary>
        private static bool PoolSite(int c, int feet, out int s)
        {
            s = Ground(c, feet);

            if (s < 0)
                return false;

            for (int x = c + RimLeft; x <= c + RimRight; x++)
            {
                int g = Ground(x, feet);

                if (g < s - 1 || g > s + 1)
                    return false;

                // What is dug out or built over, and what it stands on.
                for (int y = s - 1; y <= s + 5; y++)
                {
                    Tile tile = Main.tile[x, y];

                    // Grass and flowers on the rim row go; anything else there stops it.
                    if (y == s - 1 && tile.HasTile && !Blocks(tile))
                    {
                        if (!Main.tileCut[tile.TileType])
                            return false;

                        continue;
                    }

                    if (!Plain(tile))
                        return false;

                    if (y >= s && tile.LiquidAmount > 0 && tile.LiquidType != LiquidID.Honey)
                        return false;
                }

                // Open air over it: nothing there but what a sword would cut, and dry.
                for (int y = s - ClearAbove; y <= s - 1; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (y <= s - 2 && tile.HasTile && !Main.tileCut[tile.TileType])
                        return false;

                    if (tile.LiquidAmount > 0 && tile.LiquidType != LiquidID.Honey)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The bucket: the rim row and the air over it cleared, a block of hive laid under
        /// it, the bucket cut out of that and filled, and the rim run flat out either side.
        /// </summary>
        private static void DigPool(int c, int s)
        {
            for (int x = c + RimLeft; x <= c + RimRight; x++)
            {
                for (int y = s - ClearAbove; y <= s - 1; y++)
                    Main.tile[x, y].ClearTile();
            }

            for (int r = 0; r <= PoolWidths.Length; r++)
            {
                for (int x = c + ShellLeft; x <= c + ShellRight; x++)
                {
                    Tile tile = Main.tile[x, s + r];
                    bool inside = r < PoolWidths.Length && x >= c - PoolWidths[r] / 2 && x <= c + PoolWidths[r] / 2 - 1;

                    tile.ClearTile();

                    if (inside)
                    {
                        tile.LiquidType = LiquidID.Honey;
                        tile.LiquidAmount = 255;
                    }
                    else
                    {
                        Hive(tile);
                    }
                }
            }

            // The rim, flat out either side, and something under it.
            for (int x = c + RimLeft; x <= c + RimRight; x++)
            {
                if (x >= c + ShellLeft && x <= c + ShellRight)
                    continue;

                Hive(Main.tile[x, s]);

                Tile under = Main.tile[x, s + 1];

                if (!under.HasTile)
                    Hive(under);
            }

            WorldGen.RangeFrame(c + RimLeft - 1, s - ClearAbove - 1, c + RimRight + 1, s + PoolWidths.Length + 2);

            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendTileSquare(-1, c + RimLeft - 1, s - ClearAbove - 1, RimRight - RimLeft + 3,
                    ClearAbove + PoolWidths.Length + 4, TileChangeType.None);
            }
        }

        /// <summary>A whole, square block of hive, dry and bare, whatever was painted there before.</summary>
        private static void Hive(Tile tile)
        {
            tile.ClearTile();
            tile.ClearBlockPaintAndCoating();
            tile.LiquidAmount = 0;
            tile.HasTile = true;
            tile.TileType = TileID.Hive;
        }

        // ---- the nest ----------------------------------------------------------------

        /// <summary>
        /// Where the nearest larva can go: the tile at the bottom of its middle. Hive first,
        /// anywhere in the box; flat solid ground of any kind if there is no hive in it.
        /// </summary>
        public static bool FindNest(Vector2 near, int reachX, int reachY, out Point spot, out bool onHive)
        {
            onHive = true;

            if (Nest(near, reachX, reachY, true, out spot))
                return true;

            onHive = false;
            return Nest(near, reachX, reachY, false, out spot);
        }

        private static bool Nest(Vector2 near, int reachX, int reachY, bool hiveOnly, out Point spot)
        {
            spot = default;

            int cx = (int)(near.X / 16f);
            int cy = (int)(near.Y / 16f);
            float best = float.MaxValue;
            bool found = false;

            for (int x = cx - reachX; x <= cx + reachX; x++)
            {
                for (int y = cy - reachY; y <= cy + reachY; y++)
                {
                    if (!NestAt(x, y, hiveOnly))
                        continue;

                    float distance = Vector2.DistanceSquared(new Vector2(x * 16f + 8f, y * 16f - 8f), near);

                    if (distance < best)
                    {
                        best = distance;
                        spot = new Point(x, y);
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>Whether a larva can still go where one was found a place: nothing built or grown there since.</summary>
        public static bool NestFree(Point spot) => NestAt(spot.X, spot.Y, false);

        /// <summary>Whether a larva could sit with the middle of its bottom row at (x, y).</summary>
        private static bool NestAt(int x, int y, bool hiveOnly)
        {
            if (!WorldGen.InWorld(x, y, 12))
                return false;

            for (int i = -1; i <= 1; i++)
            {
                Tile ground = Main.tile[x + i, y + 1];

                if (!Blocks(ground) || ground.IsHalfBlock || ground.Slope != SlopeType.Solid)
                    return false;

                if (hiveOnly && ground.TileType != TileID.Hive)
                    return false;

                for (int j = 0; j <= 2; j++)
                {
                    Tile tile = Main.tile[x + i, y - j];

                    if (tile.HasTile)
                        return false;

                    if (tile.LiquidAmount > 0 && tile.LiquidType != LiquidID.Honey)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A larva, there: placed as vanilla places one, or, if that will not take, laid tile
        /// by tile in its first frame, which is what the placing gives. Sent on.
        /// </summary>
        public static void PlaceLarva(Point spot)
        {
            int x = spot.X;
            int y = spot.Y;

            WorldGen.PlaceTile(x, y, TileID.Larva, true, false, -1, 0);

            Tile placed = Main.tile[x, y];

            if (!placed.HasTile || placed.TileType != TileID.Larva)
            {
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -2; j <= 0; j++)
                    {
                        Tile tile = Main.tile[x + i, y + j];

                        tile.ClearTile();
                        tile.HasTile = true;
                        tile.TileType = TileID.Larva;
                        tile.TileFrameX = (short)((i + 1) * 18);
                        tile.TileFrameY = (short)((j + 2) * 18);
                    }
                }
            }

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x - 1, y - 2, 3, 3, TileChangeType.None);
        }
    }

    /// <summary>
    /// A larva is broken with a pickaxe and nothing else: not a sword swung through it
    /// (vanilla lets anything that cuts grass cut it), not an explosion, not lava and
    /// water meeting over it, and not the ground going out from under it. A whole larva
    /// is left as it is whatever is under it; once a pickaxe has taken a piece of it,
    /// vanilla has it, and the rest breaks and the Queen comes as she always did.
    /// </summary>
    public class LarvaWard : GlobalTile
    {
        public override bool CanExplode(int i, int j, int type) => type != TileID.Larva;

        public override bool TileFrame(int i, int j, int type, ref bool resetFrame, ref bool noBreak)
        {
            if (type != TileID.Larva || WorldGen.generatingWorld)
                return true;

            return !Whole(i, j);
        }

        /// <summary>All nine pieces of the larva this tile belongs to are there.</summary>
        private static bool Whole(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            int left = i - tile.TileFrameX % 54 / 18;
            int top = j - tile.TileFrameY % 54 / 18;

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (!WorldGen.InWorld(left + x, top + y))
                        return false;

                    Tile piece = Main.tile[left + x, top + y];

                    if (!piece.HasTile || piece.TileType != TileID.Larva || piece.TileFrameX % 54 != x * 18
                        || piece.TileFrameY % 54 != y * 18)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// The two of vanilla's tile flags that let other things break a larva: a sword cuts
    /// it, and obsidian forming over it kills it. Both off while the mod is loaded, and
    /// put back as they were when it is not.
    /// </summary>
    public class LarvaWardSystem : ModSystem
    {
        private static bool set;
        private static bool wasCut;
        private static bool wasObsidianKill;

        public override void PostSetupContent()
        {
            wasCut = Main.tileCut[TileID.Larva];
            wasObsidianKill = Main.tileObsidianKill[TileID.Larva];

            Main.tileCut[TileID.Larva] = false;
            Main.tileObsidianKill[TileID.Larva] = false;
            set = true;
        }

        public override void Unload()
        {
            if (!set)
                return;

            Main.tileCut[TileID.Larva] = wasCut;
            Main.tileObsidianKill[TileID.Larva] = wasObsidianKill;
            set = false;
        }
    }
}
