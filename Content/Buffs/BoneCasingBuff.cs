using PisAndDag.Content.Items.Accessories;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Bone Casing: shown while the ribs stand around their wearer (BoneCasingPlayer), with how much
    /// of them is left. Put on every tick by the wearer's machine while they stand, as the game puts
    /// its Ice Barrier on, so it has no time to show and cannot be clicked off. Not saved.
    /// </summary>
    public class BoneCasingBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override bool RightClick(int buffIndex) => false;

        public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
        {
            tip = Description.Format(Main.LocalPlayer.GetModPlayer<BoneCasingPlayer>().Ribs, BoneCasingPlayer.Whole);
        }
    }

    /// <summary>
    /// Splintered: the minute and a half after the ribs break before they stand again, by request
    /// ("15 min cool down or by respawn", then "Change bone Casing cooldown to 1.5 mins"). A debuff
    /// that cannot be clicked off and the Nurse does not cure, as long in expert and master as in
    /// classic. Death takes it, as it takes every buff the game does not keep, and the ribs stand
    /// again at the respawn (BoneCasingPlayer.OnRespawn). Saved with the player, unlike the mod's
    /// other buffs, so that leaving the world is no way round the wait.
    /// </summary>
    public class Splintered : ModBuff
    {
        /// <summary>A minute and a half.</summary>
        public const int Duration = 90 * 60;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }
    }
}
