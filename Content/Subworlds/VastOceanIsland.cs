using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Structures.Grace;
using PisAndDag.Content.Tiles.SeaFloor;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Vast Ocean's island (GraceBuilder.BuildLighthouseIsland, whose notes say what it is), by
    /// request: "1000- 1300 tiles to the left is an island floating in the sea, around 300 tile
    /// wide, having a large light house on the rock". Its rock runs from 1312 columns left of the
    /// middle to 982, 331 wide; the sea stack stands off its west end on a ledge reaching to 1330,
    /// and the sloop rides at anchor off its east end.
    ///
    /// Laid out once the sea is poured and before anything is put on the floor (VastOceanBuild.Lay),
    /// so that nothing of the floor's is laid where its two chains come down; on a random of its own
    /// seeded from the world's, so it comes out the same for the same world. What it clears it
    /// leaves dry; after it the sea is let back into every open cell under the surface that the sea
    /// can reach from round the island, as the place pours it (VastOceanFloor.Water: half full on
    /// the surface row, full under it). What the sea cannot reach (the inside of the skiff, a pool
    /// left in the rock) is left as it was made.
    ///
    /// Nothing here is the mod's but the lens's type, handed in, so that the preview can lay the
    /// island out with the game alone.
    /// </summary>
    public static class VastOceanIsland
    {
        /// <summary>The world column of the island's plan column 0 (its plan: GraceBuilder.IsleX0 to IsleX1).</summary>
        public const int Left = VastOcean.Middle - 1300;

        /// <summary>The lens's top left tile (LighthouseLens; LighthouseBeam draws its beam from it).</summary>
        public static Point LensAt => new Point(Left + GraceBuilder.IsleLensX, VastOcean.Surface + GraceBuilder.IsleLensY);

        /// <summary>For the preview's log: what the last build put down, what would not go (by tile type), what it marked, and how many rooms the last pass found that would pass for a house (0 when built as planned).</summary>
        public static int Placed, Houses;
        public static readonly Dictionary<int, int> Failed = new Dictionary<int, int>();
        public static readonly List<(string name, Rectangle area)> Marks = new List<(string, Rectangle)>();

        public static void Build(int seed, VastOceanFloor.Planter plant, ushort lensType)
        {
            UnifiedRandom was = WorldGen._genRand;
            WorldGen._genRand = new UnifiedRandom(seed ^ 0x1516E);

            try
            {
                var canvas = new GraceWorldCanvas(Left, VastOcean.Surface, 1);
                var hooks = new IslandHooks
                {
                    Deepest = VastOcean.High - VastOcean.Surface - 40,
                    Lens = lensType,
                    Life = plant == null ? null : (x, y, kind, style) => plant(canvas.WX(x), canvas.WY(y), (SeaLife.Kind)kind, style),
                    Anemone = (int)SeaLife.Kind.DeepAnemone,
                    CupSponge = (int)SeaLife.Kind.CupSponge,
                    Palm = (x, y) => WorldGen.GrowPalmTree(canvas.WX(x), canvas.WY(y)),
                    Kelp = (x, y, tall) => Kelp(canvas.WX(x), canvas.WY(y), tall),
                    Smoke = (x, y) => Smoke(canvas.WX(x), canvas.WY(y)),
                };

                GraceBuilder.BuildLighthouseIsland(canvas, hooks);
                Houses = GraceBuilder.Unhoused;
                canvas.Finish();

                Pour(canvas.WX(GraceBuilder.IsleX0), canvas.WX(GraceBuilder.IsleX1), VastOcean.Surface, VastOcean.Surface + GraceBuilder.IsleY1);

                Placed = canvas.Placed;
                Failed.Clear();
                foreach (var pair in canvas.Failed)
                    Failed[pair.Key] = pair.Value;
                Marks.Clear();
                Marks.AddRange(canvas.Marks);
            }
            finally
            {
                WorldGen._genRand = was;
            }
        }

        /// <summary>Open to the sea: no tile here the water cannot stand in (a solid block; a platform, a post or a thing on the floor lets it in).</summary>
        private static bool Open(int x, int y)
        {
            Tile t = Main.tile[x, y];

            return !(t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]);
        }

        /// <summary>
        /// The sea let back in, over the island's columns from the surface row down to under its
        /// rock: every open cell the sea reaches from round them, a cell at a time, the way water
        /// runs (left, right, up and down), half full on the surface row and full under it.
        /// </summary>
        private static void Pour(int x0, int x1, int y0, int y1)
        {
            x0--;
            x1++;
            y1++;

            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var reached = new bool[w * h];
            var stack = new Stack<int>();

            void Reach(int x, int y)
            {
                int k = (x - x0) * h + (y - y0);
                if (reached[k] || !Open(x, y))
                    return;

                reached[k] = true;
                stack.Push(k);
            }

            for (int y = y0; y <= y1; y++)
            {
                Reach(x0, y);
                Reach(x1, y);
            }

            for (int x = x0; x <= x1; x++)
                Reach(x, y1);

            while (stack.Count > 0)
            {
                int k = stack.Pop();
                int x = x0 + k / h, y = y0 + k % h;

                if (x > x0)
                    Reach(x - 1, y);
                if (x < x1)
                    Reach(x + 1, y);
                if (y > y0)
                    Reach(x, y - 1);
                if (y < y1)
                    Reach(x, y + 1);
            }

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (!reached[(x - x0) * h + (y - y0)])
                        continue;

                    Tile t = Main.tile[x, y];
                    t.LiquidType = LiquidID.Water;
                    t.LiquidAmount = (byte)(y == y0 ? 127 : 255);
                }
            }
        }

        /// <summary>
        /// A stand of kelp (the game's seaweed) on the ground at this column and row, put down and
        /// grown the game's way (WorldGen.PlaceTile, then WorldGen.GrowCheckSeaweed from its top,
        /// which grows it only in deep water, less often the taller it is) to so many tall at most;
        /// the water over it poured first, where the builder had left it dry. How tall it grew.
        /// </summary>
        private static int Kelp(int x, int ground, int tall)
        {
            int y = ground - 1;

            for (int w = y; w > VastOcean.Surface && Open(x, w); w--)
            {
                Tile t = Main.tile[x, w];
                t.LiquidType = LiquidID.Water;
                t.LiquidAmount = 255;
            }

            if (!WorldGen.PlaceTile(x, y, TileID.Seaweed, true) || !Main.tile[x, y].HasTile || Main.tile[x, y].TileType != TileID.Seaweed)
                return 0;

            for (int grow = 0; grow < tall * 6 && Height(x, y) < tall; grow++)
                WorldGen.GrowCheckSeaweed(x, y - Height(x, y) + 1);

            return Height(x, y);
        }

        private static int Height(int x, int y)
        {
            int count = 0;

            while (y - count > 0 && Main.tile[x, y - count].HasTile && Main.tile[x, y - count].TileType == TileID.Seaweed)
                count++;

            return count;
        }

        /// <summary>The chimney (TileID.Chimney) whose left column and bottom row these are, set smoking: its second state (frames 54 down), as its switch would set it.</summary>
        private static void Smoke(int left, int bottom)
        {
            for (int x = left; x < left + 3; x++)
            {
                for (int y = bottom - 2; y <= bottom; y++)
                {
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && t.TileType == TileID.Chimney && t.TileFrameY < 54)
                        t.TileFrameY += 54;
                }
            }
        }
    }
}
