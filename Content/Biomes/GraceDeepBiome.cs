using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Structures;
using PisAndDag.Content.Structures.Grace;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// Beneath Grace: everything under the Kingdom of Grace's plain, from a few tiles under
    /// its surface (where GraceBiome ends) down to the bottom of what the builders built, the
    /// Rootworks round the Shimmer. The cellars, the harbour basin, the Undercity, the
    /// Reliquary, the fissure, the Font, all of it. Membership is by position, as the plain's
    /// is: its span (Layout.Plain), its level (GraceBiome.PlainLevel), and the bottom of the
    /// rectangle the kingdom's guard map covers (GraceWorld.Area), which is saved with the
    /// world and sent to every client.
    ///
    /// What being in it does:
    ///   the "PisAndDag:GraceDeep" screen filter, if its .xnb compiled: the dark sunk into
    ///     sapphire, lit things gone gold and glowing (Effects/GraceDeep.fx);
    ///   the water, clear and mint (GraceDeepWaterStyle), which needs no shader;
    ///   gold motes hanging where there is light to catch (GraceDeepAir), which need none either.
    ///
    /// No music of its own and no background of its own.
    ///
    /// Textures, in this folder, loaded by name convention:
    ///   GraceDeepBiome_Icon.png        30 x 30   bestiary filter icon
    ///   GraceDeepBiome_Background.png  115 x 65  bestiary card background and the
    ///                                            full-screen map background
    /// </summary>
    public class GraceDeepBiome : ModBiome
    {
        public const string FilterKey = "PisAndDag:GraceDeep";

        /// <summary>
        /// Set by the Mod class once the screen filter has actually loaded. Until then
        /// SpecialVisuals does nothing, so a missing .xnb means no grade, not a crash.
        /// </summary>
        internal static bool FilterReady;

        // Over the other biomes of the deep: its water is the one that counts here.
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        public override ModWaterStyle WaterStyle => ModContent.GetInstance<GraceDeepWaterStyle>();

        public override string MapBackground => BackgroundPath;

        /// <summary>
        /// The lowest row that is still Grace's: the bottom of the rectangle its guard map
        /// covers, or where there is no map (a world from before it was kept), the bottom of
        /// the lowest place the builders marked. -1 where there is no kingdom.
        /// </summary>
        public static int Bottom
        {
            get
            {
                Rectangle area = GraceWorld.Area;

                if (area.Width > 0 && area.Height > 0)
                    return area.Bottom;

                int bottom = -1;
                var marks = GraceWorld.Marks;

                for (int i = 0; i < marks.Count; i++)
                {
                    if (marks[i].area.Bottom > bottom)
                        bottom = marks[i].area.Bottom;
                }

                return bottom;
            }
        }

        /// <summary>
        /// True for a tile over the plain's span, below the plain's own biome and no lower
        /// than the bottom of the kingdom. One rule for every use, so the water, the grade and
        /// the motes never disagree about where it is.
        /// </summary>
        public static bool Covers(int tileX, int tileY)
        {
            if (!ExtraLargeWorld.Active)
                return false;

            int level = GraceBiome.PlainLevel;

            if (level < 0 || tileY <= level + GraceBiome.Below)
                return false;

            (int left, int right) = Layout.Plain;

            if (tileX < left || tileX > right)
                return false;

            return tileY <= Bottom;
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
            // reads that as uOpacity, so the grade never snaps on or off.
            player.ManageSpecialBiomeVisuals(FilterKey, isActive, player.Center);
        }
    }

    /// <summary>
    /// The air and the water Beneath Grace, on this machine.
    ///
    /// THE WATER. A modded water style only gets its turn in Main.CalculateWaterStyle where
    /// the surface background has not already picked a vanilla one, and the plain runs to the
    /// sea, whose background picks one of its own. So this makes sure: while the local player
    /// is Beneath Grace, the water is Grace's, unless a water fountain on screen says
    /// otherwise, as a fountain does everywhere. Blood moons do not reach this far down.
    ///
    /// THE MOTES. A few a second, gold, now and then a pale mint one, hanging in open air
    /// where there is light to catch (a torch, a lamp, a lit hall), brighter the more light
    /// there is. GraceMote, as the plain's; none in the dark and none in the water.
    ///
    /// Client only; nothing here is saved or sent.
    /// </summary>
    public class GraceDeepAir : ModSystem
    {
        /// <summary>Two tries a tick at this chance each: about twenty a second on a lit screen.</summary>
        private const float MoteChance = 0.18f;

        /// <summary>No mote where the light is less than this.</summary>
        private const float MoteLight = 0.1f;

        private static readonly Color Gold = new Color(255, 206, 128);
        private static readonly Color PaleMint = new Color(165, 236, 212);

        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.CalculateWaterStyle += GraceWater;
        }

        private static int GraceWater(On_Main.orig_CalculateWaterStyle orig, bool ignoreFountains)
        {
            int style = orig(ignoreFountains);

            if (Main.gameMenu || Main.dedServ)
                return style;

            // A fountain has its say here as everywhere.
            if (!ignoreFountains && Main.SceneMetrics.ActiveFountainColor >= 0)
                return style;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || !me.InModBiome<GraceDeepBiome>())
                return style;

            return ModContent.GetInstance<GraceDeepWaterStyle>().Slot;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || !me.InModBiome<GraceDeepBiome>())
                return;

            for (int k = 0; k < 2; k++)
            {
                if (Main.rand.NextFloat() > MoteChance)
                    continue;

                Vector2 at = Main.screenPosition + new Vector2(Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
                Point cell = at.ToTileCoordinates();

                if (!WorldGen.InWorld(cell.X, cell.Y, 2) || !GraceDeepBiome.Covers(cell.X, cell.Y))
                    continue;

                Tile tile = Main.tile[cell.X, cell.Y];

                if (tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated)
                    continue;

                if (tile.LiquidAmount > 0)
                    continue;

                float light = Lighting.Brightness(cell.X, cell.Y);

                if (light < MoteLight)
                    continue;

                Color tint = (Main.rand.NextBool(5) ? PaleMint : Gold) * MathHelper.Clamp(0.4f + light, 0.4f, 1f);

                PADParticleHandler.SpawnParticle(new GraceMote(at, tint, Main.rand.Next(200, 340)));
            }
        }
    }
}
