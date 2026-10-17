using System;
using PisAndDag.Content.Biomes;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// The Water's Weight: shown among the debuffs while the Vast Ocean's water takes life from
    /// the player (VastOceanWeight), saying how much a second. Only shown: what it takes is
    /// worked out where the player is (VastOceanWeightPlayer), not by this, so nothing that
    /// takes a debuff off (or keeps one off) takes the weight off. No time shown; it is put
    /// back on every tick while it lasts, and gone two ticks after.
    /// </summary>
    public class WatersWeight : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
        {
            float taking = Main.LocalPlayer.GetModPlayer<VastOceanWeightPlayer>().Taking;

            if (taking > 0f)
                tip += "\n" + Language.GetTextValue("Mods.PisAndDag.Buffs.WatersWeight.Losing", (int)Math.Round(taking));
        }
    }
}
