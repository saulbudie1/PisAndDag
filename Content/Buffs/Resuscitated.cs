using PisAndDag.Content.Biomes;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// What Water! Resuscitate Me! puts on: the water lets the caster breathe it, as the Gills
    /// Potion does (Player.gills: no breath spent under water), and keeps its weight off them
    /// down to the end of the Vast Ocean's 8th section (<see cref="HoldsTo"/>,
    /// VastOceanWeight.HoldTo), 3584 feet as the Depth Meter reads. Not the game's Gills buff, so
    /// the depth does not wear it down as it does the potion's (VastOceanGills): its five minutes
    /// are five minutes anywhere.
    /// </summary>
    public class Resuscitated : ModBuff
    {
        /// <summary>The deepest section it keeps the water's weight off in: the 8th.</summary>
        public const int HoldsTo = 8;

        public override void SetStaticDefaults()
        {
            // As Nurtured: the water's answer does not outlast the game it was asked in.
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.gills = true;
            VastOceanWeight.HoldTo(player, HoldsTo);
        }

        public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
        {
            tip += "\n" + Language.GetTextValue("Mods.PisAndDag.Buffs.Resuscitated.Holds", VastOceanWeight.FeetAt(HoldsTo + 1));
        }
    }
}
