using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Vast Ocean's reefs laid down (ReefPlan, whose notes say what they are), once the sea is poured and the island
    /// built (VastOceanBuild.Lay): the plan made from what is open water then, and every cell of it written into the
    /// tiles, the reef's blocks with no water in them and its walls in the water; then its life put down by the game's
    /// own hand (WorldGen.PlaceObject), each anchored the game's way, and what will not go left out and counted; then its
    /// edges worn round by the game's own smoothing (Tile.SmoothSlope), save under what stands on them or hangs from them.
    ///
    /// Nothing here asks for the mod's types: they are handed in (<see cref="Types"/>), so that the place can be laid out
    /// with the game alone.
    /// </summary>
    public static class VastOceanReef
    {
        /// <summary>The middle of the island's rock (VastOceanIsland: from 1312 columns left of the middle to 982), where the first field lies under it.</summary>
        public const int Hub = VastOcean.Middle - 1147;

        /// <summary>The tile and wall types of the reef's blocks, walls and life, by ReefPlan.Block, ReefPlan.Wall and ReefPlan.Decor (none for None).</summary>
        public sealed class Types
        {
            public readonly ushort[] Blocks = new ushort[ReefPlan.Blocks];
            public readonly ushort[] Walls = new ushort[ReefPlan.Walls];
            public readonly ushort[] Life = new ushort[ReefPlan.Decors];
        }

        /// <summary>For the preview's log: islands, strands, blocks and walls written, life put down and life that would not go.</summary>
        public static int Isles, Strands, Blocks, Walls, Placed, Missed;

        public static void Grow(int seed, Types types)
        {
            Isles = Strands = Blocks = Walls = Placed = Missed = 0;

            if (types == null)
                return;

            var shape = new ReefPlan.Shape(VastOcean.Wide, VastOcean.Surface, VastOcean.SectionRows, VastOcean.Middle, Subworld.Margin, Hub, Open);
            ReefPlan plan = ReefPlan.Make(shape, seed);

            Isles = plan.Isles.Count;
            Strands = plan.Strands.Count;

            for (int y = plan.Top; y <= plan.Bottom; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    ReefPlan.Block block = plan.BlockAt(x, y);
                    ReefPlan.Wall wall = plan.WallAt(x, y);

                    if (block == ReefPlan.Block.None && wall == ReefPlan.Wall.None)
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (block != ReefPlan.Block.None)
                    {
                        tile.ClearTile();
                        tile.HasTile = true;
                        tile.TileType = types.Blocks[(int)block];
                        tile.LiquidAmount = 0;
                        Blocks++;
                    }

                    if (wall != ReefPlan.Wall.None)
                    {
                        tile.WallType = types.Walls[(int)wall];
                        Walls++;
                    }
                }
            }

            foreach (ReefPlan.Placed life in plan.Life)
            {
                ushort type = types.Life[(int)life.Kind];

                if (type != 0 && WorldGen.PlaceObject(life.X, life.Y, type, true, life.Style) && Main.tile[life.X, life.Y].HasTile && Main.tile[life.X, life.Y].TileType == type)
                    Placed++;
                else
                    Missed++;
            }

            foreach (var (x, y) in plan.Worn)
                Tile.SmoothSlope(x, y, false);
        }

        /// <summary>Open water: nothing in the cell, no wall, water in it.</summary>
        private static bool Open(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
                return false;

            Tile tile = Main.tile[x, y];
            return !tile.HasTile && tile.WallType == 0 && tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;
        }
    }
}
