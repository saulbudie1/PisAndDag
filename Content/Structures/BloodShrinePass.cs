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
    /// <summary>
    /// Places one Blood Shrine near the spawn: at the end of the forest the player wakes up
    /// in, on the side where that end is nearer, a few tiles in from where the next ground
    /// begins. Whoever walks out that way passes it. In the planned world that is the snow
    /// side, some five hundred tiles east of the spawn.
    ///
    /// The forest is read off the ground rather than off the plan, so this holds in a world
    /// of any size and either way round. Going out from the spawn, the forest ends where the
    /// surface turns to another biome's ground (snow, sand, jungle, the evils, the Hallow,
    /// ash) and stays that way for EndRun columns: a sand patch or a pond does not end it.
    /// From there the shrine goes on the first dry spot that is flat enough, mostly forest
    /// ground and not claimed by anything else, searching back toward the spawn. If that
    /// side has none, the other side is tried the same way.
    /// </summary>
    public class BloodShrinePass : GenPass
    {
        /// <summary>Greatest height difference tolerated across the footprint, in tiles. Doubled as a last resort.</summary>
        private const int MaxSlope = 6;

        /// <summary>Fraction of the footprint that must be forest ground.</summary>
        private const float ForestRatio = 0.8f;

        /// <summary>How far the base is sunk below ground so it looks planted, not dropped.</summary>
        private const int Embed = 2;

        /// <summary>Columns of another biome's ground in a row that end the forest.</summary>
        private const int EndRun = 30;

        /// <summary>Tiles kept between the shrine and the first column of the ground that ends the forest.</summary>
        private const int Inset = 12;

        /// <summary>Tiles kept clear between the shrine and the spawn column, so nobody wakes up inside it.</summary>
        private const int SpawnClear = 20;

        /// <summary>How far over the top of the shrine a tree standing on its ground is cleared to. Past the tallest forest tree.</summary>
        private const int TreeReach = 30;

        /// <summary>Columns this near the world's edge are never read.</summary>
        private const int Edge = 50;

        /// <summary>How far under a solid tile to look for more, to tell ground from a floating island.</summary>
        private const int GroundProbe = 40;

        /// <summary>Solid tiles, out of GroundProbe, that make it ground.</summary>
        private const int GroundSolidMin = 18;

        /// <summary>How far to drop past an island's crust before reading on.</summary>
        private const int IslandSkip = 20;

        /// <summary>The forest floor.</summary>
        private static readonly HashSet<ushort> ForestGround = new HashSet<ushort>
        {
            TileID.Grass, TileID.Dirt, TileID.Stone, TileID.ClayBlock,
        };

        /// <summary>The ground of the biomes a forest ends at.</summary>
        private static readonly HashSet<ushort> OtherGround = new HashSet<ushort>
        {
            TileID.SnowBlock, TileID.IceBlock,
            TileID.Sand, TileID.HardenedSand, TileID.Sandstone,
            TileID.JungleGrass, TileID.Mud,
            TileID.CorruptGrass, TileID.Ebonstone, TileID.Ebonsand,
            TileID.CrimsonGrass, TileID.Crimstone, TileID.Crimsand,
            TileID.HallowedGrass, TileID.Pearlstone, TileID.Pearlsand,
            TileID.MushroomGrass, TileID.Ash, TileID.AshGrass,
        };

        public BloodShrinePass(string name, double loadWeight) : base(name, loadWeight)
        {
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Something waits where the trees end";

            PADStructure shrine = PADStructures.Get(PADStructureSystem.BloodShrine);
            if (shrine == null)
            {
                ModContent.GetInstance<PisAndDag>()?.Logger
                    .Warn("Blood Shrine pass ran but the structure was never registered.");
                return;
            }

            int spawnX = Main.spawnTileX;

            // Each side's stretch of forest, the side whose end is nearer the spawn first.
            var sides = new List<(int dir, int start, int end)>();
            foreach (int dir in new[] { 1, -1 })
            {
                if (Forest(spawnX, dir, out int start, out int end))
                    sides.Add((dir, start, end));
            }

            sides.Sort((a, b) => Math.Abs(a.end - spawnX).CompareTo(Math.Abs(b.end - spawnX)));

            // Both sides on ground as level as the shrine likes; then, rather than a world
            // with no Orb in it, both sides again on ground twice as rough.
            int[] slopes = { MaxSlope, MaxSlope * 2 };
            for (int pass = 0; pass < slopes.Length; pass++)
            {
                for (int i = 0; i < sides.Count; i++)
                {
                    progress.Set((pass * sides.Count + i) / (float)Math.Max(slopes.Length * sides.Count, 1));

                    var (dir, start, end) = sides[i];
                    if (PlaceAlong(shrine, spawnX, dir, start, end, slopes[pass]))
                    {
                        progress.Set(1f);
                        return;
                    }
                }
            }

            ModContent.GetInstance<PisAndDag>()?.Logger
                .Warn($"Blood Shrine found no place at either end of the forest round the spawn ({spawnX}).");
        }

        /// <summary>
        /// The stretch of forest going out from <paramref name="from"/> one way: its first
        /// forest column (the spawn's own, in any ordinary world), and the first column of
        /// the other ground that ends it. False if the forest does not end on this side
        /// before the world does, or there is no forest on it.
        /// </summary>
        private static bool Forest(int from, int dir, out int start, out int end)
        {
            start = -1;
            end = -1;

            int run = 0, runStart = -1;

            for (int x = from; x > Edge && x < Main.maxTilesX - Edge; x += dir)
            {
                // A column with no ground, or water standing on it, says nothing either way.
                if (!Surface(x, out int y) || Wet(x, y))
                    continue;

                ushort type = Main.tile[x, y].TileType;

                if (ForestGround.Contains(type))
                {
                    if (start < 0)
                        start = x;

                    run = 0;
                }
                else if (start >= 0 && OtherGround.Contains(type))
                {
                    if (run == 0)
                        runStart = x;

                    if (++run >= EndRun)
                    {
                        end = runStart;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries the shrine at every centre from the forest's end back toward its start, the
        /// far edge of the footprint always Inset short of the end, on ground that rises and
        /// falls no more than <paramref name="maxSlope"/> across it. Stamps it at the first
        /// that fits and says so.
        /// </summary>
        private static bool PlaceAlong(PADStructure shrine, int spawnX, int dir, int start, int end, int maxSlope)
        {
            int width = shrine.Width;
            int half = width / 2;

            for (int centerX = end - dir * (Inset + half + 1); ; centerX -= dir)
            {
                int left = centerX - half;
                int right = left + width - 1;

                // Back past the start of the forest: nothing left to try on this side.
                if (dir > 0 ? left < start : right > start)
                    return false;

                if (Math.Abs(centerX - spawnX) < half + SpawnClear)
                    continue;

                if (!MeasureGround(left, width, out int groundY, out int forestTiles, out int slope))
                    continue;

                if (slope > maxSlope || forestTiles < width * ForestRatio)
                    continue;

                Point stampAt = new Point(centerX, groundY + Embed);

                Rectangle area = PADStructures.GetArea(
                    PADStructureSystem.BloodShrine, stampAt, StructureAnchor.BottomCenter);

                // Do not land on a living tree, a cabin or anything else already claimed.
                if (GenVars.structures != null && !GenVars.structures.CanPlace(area, 4))
                    continue;

                ClearStanding(left, right, groundY, shrine.Height);

                if (!PADStructures.Stamp(PADStructureSystem.BloodShrine, stampAt,
                        StructureAnchor.BottomCenter,
                        onChestPlaced: PADStructureSystem.FillShrineChest))
                {
                    continue;
                }

                // Reserve the ground so later passes do not carve through it.
                GenVars.structures?.AddProtectedStructure(area, 6);
                return true;
            }
        }

        /// <summary>
        /// Clears what stands on the ground the shrine is about to take, and two columns
        /// either side: every tree whole, since the stamp would otherwise cut through its
        /// trunk and leave the crown hanging over the roof, and the plants, pots and logs
        /// round its feet. The ground itself is left to the stamp.
        /// </summary>
        private static void ClearStanding(int left, int right, int groundY, int height)
        {
            int x0 = Math.Max(10, left - 2), x1 = Math.Min(Main.maxTilesX - 10, right + 2);
            int treeTop = Math.Max(10, groundY - height - TreeReach);
            int standTop = Math.Max(10, groundY - height);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = treeTop; y <= groundY + Embed; y++)
                {
                    Tile t = Main.tile[x, y];
                    if (!t.HasTile)
                        continue;

                    bool tree = TileID.Sets.IsATreeTrunk[t.TileType];
                    bool standing = y >= standTop && y <= groundY
                        && (!Main.tileSolid[t.TileType] || Main.tileFrameImportant[t.TileType]);

                    if (tree || standing)
                        t.ClearTile();
                }
            }
        }

        /// <summary>
        /// Walks the footprint column by column, reporting the lowest ground line, how
        /// much the terrain rises and falls across it, and how much of it is forest floor.
        /// False if any column has no ground or water standing on it.
        /// </summary>
        private static bool MeasureGround(int left, int width, out int groundY, out int forestTiles, out int slope)
        {
            groundY = 0;
            forestTiles = 0;
            slope = 0;

            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int x = left; x < left + width; x++)
            {
                if (!WorldGen.InWorld(x, 20))
                    return false;

                if (!Surface(x, out int y) || Wet(x, y))
                    return false;

                if (ForestGround.Contains(Main.tile[x, y].TileType))
                    forestTiles++;

                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            slope = maxY - minY;

            // Anchor to the lowest point, so no part of the base hangs in the air.
            groundY = maxY;
            return true;
        }

        /// <summary>
        /// The ground in this column: the first solid tile from the sky down that has more
        /// solid under it. A floating island is a slab with air beneath, and is passed over.
        /// False if the column has no ground above the rock layer.
        /// </summary>
        private static bool Surface(int x, out int y)
        {
            int bottom = Math.Min(Main.maxTilesY - 50, (int)Main.rockLayer);

            for (y = 50; y < bottom; y++)
            {
                Tile t = Main.tile[x, y];

                if (!t.HasTile || t.IsActuated || !Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType])
                    continue;

                if (!Grounded(x, y))
                {
                    y += IslandSkip;
                    continue;
                }

                return true;
            }

            y = -1;
            return false;
        }

        /// <summary>Whether the solid tile at (x, y) has real ground under it rather than being an island's crust.</summary>
        private static bool Grounded(int x, int y)
        {
            int solid = 0;
            int limit = Math.Min(Main.maxTilesY - 2, y + GroundProbe);

            for (int probe = y; probe < limit; probe++)
            {
                Tile t = Main.tile[x, probe];
                if (t.HasTile && Main.tileSolid[t.TileType])
                    solid++;
            }

            return solid >= GroundSolidMin;
        }

        /// <summary>Liquid stands on the ground at (x, y).</summary>
        private static bool Wet(int x, int y) => y > 0 && Main.tile[x, y - 1].LiquidAmount > 0;
    }
}
