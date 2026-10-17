using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The Vast Ocean, the biome, by request ("its own biome called the Vast Ocean"): all of
    /// the place of that name (Subworlds/VastOcean), from its sky to its floor. To the game
    /// it is the sea as well (Player.ZoneBeach, by the game's own count there: VastOcean's
    /// notes), so everything the game does for a beach it does here; this is what is the
    /// Vast Ocean's own.
    ///
    /// Being in it: its water, its waves and its five shaders (VastOceanSky), turned on and
    /// off from here; its light in the depths (VastOceanLight) and what is heard there
    /// (VastOceanSound). Its music and its map are the place's (SubworldScene). From the 7th
    /// section down two biomes of their own lie inside it: the Forevernight Zone and the
    /// Abyssal Trench (VastOceanDepths).
    ///
    /// No bestiary picture of its own yet: the game's own stands in for it (tModLoader asks
    /// for VastOceanBiome_Icon and VastOceanBiome_Background beside this file, and takes
    /// its own if they are not there).
    /// </summary>
    public class VastOceanBiome : ModBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        public override bool IsBiomeActive(Player player) => SubworldKeeper.Current is VastOcean;

        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (Main.dedServ || player.whoAmI != Main.myPlayer)
                return;

            VastOceanSky.Visuals(isActive);
        }
    }
}
