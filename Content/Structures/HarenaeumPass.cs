using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    public class HarenaeumSystem : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int index = tasks.FindIndex(pass => pass.Name.Equals("Final Cleanup"));

            if (index == -1)
                index = tasks.Count;

            tasks.Insert(index, new HarenaeumPass("PisAndDag: Harenaeum", 20.0));
        }
    }

    /// <summary>
    /// Harenaeum Ore in a new world, by request ("generated on harden sandstone tiles in underground desert"; both
    /// the hardened sand and the sandstone, asked; new worlds only, asked): veins through the underground desert,
    /// in nothing but its hardened sand and sandstone, the rest of it left as it is.
    ///
    /// Where the desert is: the box the game made it in (GenVars.UndergroundDesertLocation); a world that has none
    /// (one the plan made some other way) is looked over for its sandstone under the surface instead.
    ///
    /// How much: a vein for every 2000 tiles of the box under the surface (a large world's desert, some sixty; a small
    /// one's, some twenty), each a wandering clump of 5 to 14 ore, thicker the deeper; a vein that finds no sand or
    /// sandstone where it starts is not put. Mine, the numbers: enough in a small world for the set's hundred-odd bars,
    /// not so much that the desert is made of it.
    /// </summary>
    public class HarenaeumPass : GenPass
    {
        public HarenaeumPass(string name, double weight) : base(name, weight)
        {
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Gold settles in the sand";

            Rectangle desert = GenVars.UndergroundDesertLocation;

            if (desert.Width <= 0 || desert.Height <= 0)
                desert = Survey();

            if (desert.Width <= 0 || desert.Height <= 0)
                return;

            // Under the surface only: the box can reach up into the dunes.
            int top = Math.Max(20, (int)Main.worldSurface + 5);
            desert = Rectangle.Intersect(desert, new Rectangle(20, top, Main.maxTilesX - 40, Main.maxTilesY - 40 - top));

            if (desert.Width <= 0 || desert.Height <= 0)
                return;

            int veins = desert.Width * desert.Height / 2000;
            int placed = 0;

            for (int v = 0, tries = 0; v < veins && tries < veins * 20; tries++)
            {
                int x = WorldGen.genRand.Next(desert.Left, desert.Right);
                int y = WorldGen.genRand.Next(desert.Top, desert.Bottom);

                if (!Host(x, y))
                    continue;

                // Deeper, bigger.
                float depth = (y - desert.Top) / (float)Math.Max(1, desert.Height);
                placed += Vein(x, y, WorldGen.genRand.Next(5, 10) + (int)(depth * 5f));
                v++;

                progress.Set(v / (float)Math.Max(1, veins));
            }

            GenLog.Log?.Info($"Harenaeum: {placed} ore in {veins} veins, desert {desert}.");
        }

        /// <summary>Hardened sand or sandstone, the rock it is found in.</summary>
        private static bool Host(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return false;

            Tile tile = Main.tile[x, y];

            return tile.HasTile && (tile.TileType == TileID.HardenedSand || tile.TileType == TileID.Sandstone);
        }

        /// <summary>A clump of ore wandering out from here, through hardened sand and sandstone only: how many it put.</summary>
        private static int Vein(int x, int y, int size)
        {
            ushort ore = (ushort)ModContent.TileType<Tiles.HarenaeumOre>();
            int put = 0;
            Vector2 at = new Vector2(x, y);
            Vector2 way = WorldGen.genRand.NextVector2Unit();

            for (int step = 0; step < size * 3 && put < size; step++)
            {
                int cx = (int)Math.Round(at.X), cy = (int)Math.Round(at.Y);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        // A plus, not a square: rounder clumps.
                        if (dx != 0 && dy != 0 && !WorldGen.genRand.NextBool(3))
                            continue;

                        int tx = cx + dx, ty = cy + dy;

                        if (Host(tx, ty) && WorldGen.genRand.NextBool(2))
                        {
                            Main.tile[tx, ty].TileType = ore;
                            put++;
                        }
                    }
                }

                way = way.RotatedBy(WorldGen.genRand.NextFloat(-0.8f, 0.8f));
                at += way;
            }

            return put;
        }

        /// <summary>The box round the sandstone under the surface, for a world whose desert was not kept: its middle half by count, so a stray block far off does not stretch it.</summary>
        private static Rectangle Survey()
        {
            List<int> xs = new(), ys = new();

            for (int x = 50; x < Main.maxTilesX - 50; x += 4)
            {
                for (int y = (int)Main.worldSurface; y < Main.UnderworldLayer; y += 4)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Sandstone)
                    {
                        xs.Add(x);
                        ys.Add(y);
                    }
                }
            }

            if (xs.Count < 50)
                return Rectangle.Empty;

            xs.Sort();
            ys.Sort();

            int left = xs[xs.Count / 8], right = xs[xs.Count * 7 / 8];
            int top = ys[ys.Count / 8], bottom = ys[ys.Count * 7 / 8];

            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
    }
}
