using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Spared: the 5 seconds after Dryad's Blessing keeps its wearer alive, by request ("a buff that
    /// boosts your regen by 20hp/s for 5 sec"): 20 more life a second, added after the game has taken
    /// what it takes for burning, poison and the like (DryadsBlessingPlayer.UpdateLifeRegen), so it is
    /// 20 more whatever else is on them; and the leaves around them (DryadsBlessingLeaves). Not saved.
    /// </summary>
    public class Spared : ModBuff
    {
        /// <summary>Five seconds.</summary>
        public const int Duration = 5 * 60;

        /// <summary>
        /// 20 life a second, in the game's count (Player.lifeRegen): it adds this to a count every
        /// tick and gives a life for every 120 counted (Player.UpdateLifeRegen), so a second's 60 ticks
        /// of 40 are 20 life.
        /// </summary>
        public const int LifeRegen = 40;

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>
    /// Wilted Clover: the 10 minutes after Dryad's Blessing has spared its wearer before it can
    /// again, by request ("Cooldown for this: 15 mins or respawn", then "Blessing cool down to 10
    /// mins"). A debuff that cannot be clicked off and the Nurse does not cure, as long in expert
    /// and master as in classic. Death takes it, as it takes every buff the game does not keep:
    /// after a respawn the clover is ready. Saved with the player, unlike the mod's other buffs, so
    /// that leaving the world is no way round the wait.
    /// </summary>
    public class WiltedClover : ModBuff
    {
        /// <summary>Ten minutes.</summary>
        public const int Duration = 10 * 60 * 60;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }
    }
}
