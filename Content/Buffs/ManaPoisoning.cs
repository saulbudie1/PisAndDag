using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Mana Poisoning: too many mana potions too close together (ManaDrinkPlayer). I comes
    /// of more than five lesser or ordinary potions inside two minutes and takes 10 life a
    /// second; II of more than five greater or super potions inside two minutes, takes 20
    /// life a second and halves magic damage. Each drink past the fifth puts it back to its
    /// full <see cref="Duration"/>. With both on, II is the one that counts.
    /// </summary>
    public abstract class ManaPoisoning : ModBuff
    {
        /// <summary>How long it lasts from the last drink that brought it on, in ticks.</summary>
        public const int Duration = 10 * 60;

        /// <summary>Life lost every second.</summary>
        public abstract int LifePerSecond { get; }

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    public class ManaPoisoning1 : ManaPoisoning
    {
        public override int LifePerSecond => 10;
    }

    public class ManaPoisoning2 : ManaPoisoning
    {
        public override int LifePerSecond => 20;

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetDamage(DamageClass.Magic) *= 0.5f;
        }
    }
}
