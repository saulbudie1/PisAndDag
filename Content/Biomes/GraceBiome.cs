using PisAndDag.Content.Structures;
using PisAndDag.Content.Structures.Grace;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The Kingdom of Grace as a biome: the plain above ground, from the top of the way up to
    /// the sea, the willow forest and the city on it. Membership is by position, as the Wasted
    /// Mile's is: the plain is a fixed span of the extra large world (Layout.Plain, mirrored),
    /// and its level is the one the kingdom was built on, found again from the Court's mark
    /// (<see cref="PlainLevel"/>). Everything under it, the Undercity, the Crypt, the fissure,
    /// the Font, is not in it: that is Beneath Grace (GraceDeepBiome), with a light of its own.
    ///
    /// What being in it does: the "PisAndDag:Grace" screen filter, if its .xnb compiled, and
    /// the sun it is fed by (GraceLight), and the motes that hang in that sun (GraceMote,
    /// which do not need the filter).
    ///
    /// Textures, all in this folder, all loaded by name convention:
    ///   GraceBiome_Icon.png        30 x 30   bestiary filter icon
    ///   GraceBiome_Background.png  115 x 65  bestiary card background and the
    ///                                        full-screen map background
    /// </summary>
    public class GraceBiome : ModBiome
    {
        public const string FilterKey = "PisAndDag:Grace";

        /// <summary>
        /// Set by the Mod class once the screen filter has actually loaded. Until then
        /// SpecialVisuals does nothing, so a missing .xnb means no shader, not a crash.
        /// </summary>
        internal static bool FilterReady;

        /// <summary>How far under the plain's level still counts as on it, in tiles: the swell of the ground. Beneath Grace starts under it.</summary>
        internal const int Below = 4;

        // No music of its own, and no background: only the light changes.
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeMedium;

        // The bestiary card art doubles as the map background.
        public override string MapBackground => BackgroundPath;

        /// <summary>
        /// The row the plain was levelled to when the kingdom was built, or -1 where there is no
        /// kingdom. It is not saved on its own; the Court's mark is, and its floor is the High
        /// Ward's (GraceBuilder.WardFloor rows over the plain).
        /// </summary>
        public static int PlainLevel
        {
            get
            {
                var marks = GraceWorld.Marks;

                for (int i = 0; i < marks.Count; i++)
                {
                    if (marks[i].name == "Grace.Court")
                        return marks[i].area.Bottom - GraceBuilder.WardFloor;
                }

                return -1;
            }
        }

        /// <summary>
        /// True for a tile over the plain's span, no lower than a little under its level. One
        /// rule for every use, so the light and the motes never disagree about where it is.
        /// </summary>
        public static bool Covers(int tileX, int tileY)
        {
            if (!ExtraLargeWorld.Active)
                return false;

            int level = PlainLevel;

            if (level < 0)
                return false;

            (int left, int right) = Layout.Plain;

            return tileX >= left && tileX <= right && tileY <= level + Below;
        }

        public override bool IsBiomeActive(Player player)
        {
            return Covers((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
        }

        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (!FilterReady)
                return;

            // The filter manager fades Opacity over a second on the way in and out; the shader
            // reads that as uOpacity, so the light never snaps on or off.
            player.ManageSpecialBiomeVisuals(FilterKey, isActive, player.Center);
        }
    }
}
