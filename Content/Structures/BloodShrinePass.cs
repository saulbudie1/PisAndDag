using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Places one Blood Shrine on the jungle surface.
    ///
    /// The search is deliberately brute force, the same way Calamity's shrines work:
    /// rather than trying to compute where the jungle is, pick a random spot, measure
    /// what is actually there, and reject it if it does not qualify. It is far more
    /// robust than reasoning about world layout, and it costs nothing at runtime.
    /// </summary>
    public class BloodShrinePass : GenPass
    {
        /// <summary>Give up after this many rejected spots rather than hanging worldgen.</summary>
        private const int MaxAttempts = 30000;

        /// <summary>Greatest height difference tolerated across the footprint, in tiles.</summary>
        private const int MaxSlope = 6;

        /// <summary>Fraction of the footprint that must be jungle ground.</summary>
        private const float JungleRatio = 0.8f;

        /// <summary>How far the base is sunk below ground so it looks planted, not dropped.</summary>
        private const int Embed = 2;

        public BloodShrinePass(string name, double loadWeight) : base(name, loadWeight)
        {
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Something stirs in the jungle";

            PADStructure shrine = PADStructures.Get(PADStructureSystem.BloodShrine);
            if (shrine == null)
            {
                ModContent.GetInstance<PisAndDag>()?.Logger
                    .Warn("Blood Shrine pass ran but the structure was never registered.");
                return;
            }

            int width = shrine.Width;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                progress.Set(attempt / (float)MaxAttempts);

                // Stay clear of the world edges, where the ocean lives.
                int centerX = WorldGen.genRand.Next(
                    (int)(Main.maxTilesX * 0.08f),
                    (int)(Main.maxTilesX * 0.92f));

                int left = centerX - width / 2;

                if (!MeasureGround(left, width, out int groundY, out int jungleTiles, out int slope))
                    continue;

                if (slope > MaxSlope)
                    continue;

                if (jungleTiles < width * JungleRatio)
                    continue;

                Point stampAt = new Point(centerX, groundY + Embed);

                Rectangle area = PADStructures.GetArea(
                    PADStructureSystem.BloodShrine, stampAt, StructureAnchor.BottomCenter);

                // Do not land on the temple, a living tree, or anything else already claimed.
                if (GenVars.structures != null && !GenVars.structures.CanPlace(area, 4))
                    continue;

                if (!PADStructures.Stamp(PADStructureSystem.BloodShrine, stampAt,
                        StructureAnchor.BottomCenter,
                        onChestPlaced: PADStructureSystem.FillShrineChest))
                {
                    continue;
                }

                // Reserve the ground so later passes do not carve through it.
                GenVars.structures?.AddProtectedStructure(area, 6);

                progress.Set(1f);
                return;
            }

            ModContent.GetInstance<PisAndDag>()?.Logger
                .Warn($"Blood Shrine found no valid jungle surface in {MaxAttempts} attempts.");
        }

        /// <summary>
        /// Walks the footprint column by column, reporting the lowest ground line, how
        /// much the terrain rises and falls across it, and how much of it is jungle.
        /// </summary>
        private static bool MeasureGround(int left, int width, out int groundY, out int jungleTiles, out int slope)
        {
            groundY = 0;
            jungleTiles = 0;
            slope = 0;

            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int x = left; x < left + width; x++)
            {
                if (!WorldGen.InWorld(x, 20))
                    return false;

                int y = FindSurface(x);
                if (y < 0)
                    return false;

                ushort type = Main.tile[x, y].TileType;
                if (type == TileID.JungleGrass || type == TileID.Mud)
                    jungleTiles++;

                if (y < minY)
                    minY = y;

                if (y > maxY)
                    maxY = y;
            }

            slope = maxY - minY;

            // Anchor to the lowest point, so no part of the base hangs in the air.
            groundY = maxY;
            return true;
        }

        /// <summary>First solid tile below the sky, or -1 if this column has no surface.</summary>
        private static int FindSurface(int x)
        {
            int limit = (int)Main.worldSurface + 20;

            for (int y = (int)(Main.worldSurface * 0.35f); y < limit; y++)
            {
                if (WorldGen.SolidTile(x, y))
                    return y;
            }

            return -1;
        }
    }
}
