using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The Forevernight Zone, by request ("below section 6 to section 10 is a new biome:
    /// Forevernight zone. (no NPC spawn)"): the Vast Ocean's 7th, 8th and 9th sections (read as
    /// not sharing the 10th with the Abyssal Trench). Nothing spawns in it (VastOceanSea); it is
    /// where the murk begins, then the silence, and where nothing put down stays (VastOceanSky,
    /// VastOceanSound, VastOceanPressure).
    ///
    /// No bestiary picture of its own yet: tModLoader asks for ForevernightZoneBiome_Icon and
    /// ForevernightZoneBiome_Background beside this file, and takes its own if they are not there.
    /// </summary>
    public class ForevernightZoneBiome : ModBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        public override bool IsBiomeActive(Player player)
        {
            float row = VastOcean.RowOf(player);

            return row >= VastOcean.ForevernightFrom && row < VastOcean.AbyssalTrenchFrom;
        }
    }

    /// <summary>
    /// The Abyssal Trench, by request ("from section 10-12, it is new biome: Abyssal Trench"):
    /// the Vast Ocean's 10th, 11th and 12th sections, down to its floor and under. Silent, pitch
    /// black, nothing spawns, nothing put down stays: left for creatures of the user's own.
    ///
    /// No bestiary picture of its own yet: tModLoader asks for AbyssalTrenchBiome_Icon and
    /// AbyssalTrenchBiome_Background beside this file, and takes its own if they are not there.
    /// </summary>
    public class AbyssalTrenchBiome : ModBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        public override bool IsBiomeActive(Player player) => VastOcean.RowOf(player) >= VastOcean.AbyssalTrenchFrom;
    }
}
