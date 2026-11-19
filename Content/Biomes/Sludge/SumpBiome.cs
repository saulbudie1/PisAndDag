using System;
using PisAndDag.Content.Tiles.Sludge;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes.Sludge
{
    /// <summary>
    /// The Sump: the sludge cavern (SludgePocket) as a biome, by request ("a sub biome at the bottom of the jungle").
    /// In it inside a cavern's extent (saved with the world, SludgePocket.Areas: the cavern is higher than the game's
    /// count of blocks reaches, so out over the middle of its lake the count alone would lose it), or with its blocks
    /// about the player: Acidic Mud and Stained Cobblestone, <see cref="Enough"/> of them in what the game counts round
    /// the player (the same count it takes for every biome). Under the Jungle's own music (no track of its own), so it
    /// sits under the Jungle without replacing it.
    ///
    /// Textures, beside this file, by name: SumpBiome_Icon.png (30 x 30, the bestiary filter) and
    /// SumpBiome_Background.png (115 x 65, the bestiary card, and the map's background), drawn in code
    /// (PlaceholderTextures.md).
    /// </summary>
    public class SumpBiome : ModBiome
    {
        /// <summary>Its blocks about the player, at the least, to be in it.</summary>
        public const int Enough = 90;

        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeMedium;

        public override string MapBackground => BackgroundPath;

        public override bool IsBiomeActive(Player player) =>
            SumpCount.Blocks >= Enough || Structures.SludgePocket.InArea((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
    }

    /// <summary>How many of the Sump's blocks the game counted about the local player.</summary>
    public class SumpCount : ModSystem
    {
        public static int Blocks;

        public override void TileCountsAvailable(ReadOnlySpan<int> tileCounts)
        {
            Blocks = tileCounts[ModContent.TileType<AcidicMud>()] + tileCounts[ModContent.TileType<StainedCobblestone>()];
        }

        public override void ClearWorld() => Blocks = 0;
    }
}
