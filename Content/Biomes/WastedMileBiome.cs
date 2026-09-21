using Terraria;
using Terraria.ModLoader;
using PisAndDag.Content.Structures;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The Wasted Mile as a biome: the strip of ash in front of the wall, from the
    /// surface up. Membership is by position, not tile count. The Mile is a fixed span
    /// of the extra large world (Layout.Mile, mirrored), so a tile count would only ever
    /// approximate what the layout already knows exactly.
    ///
    /// What being in it does:
    ///   - its own surface background (WastedMileSurfaceBackground),
    ///   - silence instead of the overworld track,
    ///   - the "PisAndDag:WastedMile" screen filter, if its .xnb compiled,
    ///   - no critters (WastedMileCritters).
    ///
    /// Textures, all in this folder, all loaded by name convention:
    ///   WastedMileBiome_Icon.png        30 x 30   bestiary filter icon (tML logs a
    ///                                             complaint and uses a blank if not 30x30)
    ///   WastedMileBiome_Background.png  115 x 65  bestiary card background and the
    ///                                             full-screen map background
    /// </summary>
    public class WastedMileBiome : ModBiome
    {
        public const string FilterKey = "PisAndDag:WastedMile";

        /// <summary>
        /// Set by the Mod class once the screen filter has actually loaded. Until then
        /// SpecialVisuals does nothing, so a missing .xnb means no shader, not a crash.
        /// </summary>
        internal static bool FilterReady;

        // Above the vanilla surface biomes, so ash never reads as desert or snow
        // because of what the ridge happened to churn up. Below Environment and every
        // boss, so events and fights keep their music.
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        // 0 is no track at all (it is what the game picks when the music volume is
        // zero). That is on purpose. Boss and event music sit above BiomeHigh and
        // still play over it. Replace with a MusicLoader slot if the Mile ever gets
        // its own piece.
        public override int Music => 0;

        public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle
            => ModContent.GetInstance<WastedMileSurfaceBackground>();

        // The bestiary card art doubles as the map background.
        public override string MapBackground => BackgroundPath;

        /// <summary>
        /// True for a tile inside the Mile's span, at or above the surface line. One
        /// rule for the player and for spawns, so nothing can spawn in a place the
        /// player is not "in".
        /// </summary>
        public static bool Covers(int tileX, int tileY)
        {
            if (!ExtraLargeWorld.Active)
                return false;

            (int left, int right) = Layout.Mile;

            return tileX >= left && tileX <= right && tileY <= Main.worldSurface;
        }

        public override bool IsBiomeActive(Player player)
        {
            return Covers((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
        }

        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (!FilterReady)
                return;

            // The filter manager fades Opacity over one second on the way in and out;
            // the shader reads that as uOpacity, so the grade never snaps.
            player.ManageSpecialBiomeVisuals(FilterKey, isActive, player.Center);
        }
    }
}
