using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// What a charged crack of Joint Arms of the Defiled gives (JointArmsStreak.Spend): 50%
    /// more summon damage, for 11 seconds. Whips count as summon damage, so the whip hits
    /// harder under it as well as the minions. Another charged crack sets it back to 11
    /// seconds; it does not stack.
    /// </summary>
    public class ManyHands : ModBuff
    {
        public const float SummonDamage = 0.5f;
        public const int Ticks = 11 * 60;

        public override void SetStaticDefaults()
        {
            // A fight's buff: it does not outlast the world it was earned in.
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetDamage(DamageClass.Summon) += SummonDamage;
        }
    }
}
