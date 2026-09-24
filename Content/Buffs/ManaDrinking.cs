using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Every mana potion drunk, however it was drunk: from the hand, by the quick-mana key,
    /// or by a Mana Flower reaching for one. All of those consume the potion through
    /// ItemLoader.ConsumeItem, which is what calls OnConsumeItem. Counted on the drinker's own
    /// machine only.
    /// </summary>
    public class ManaDrinkItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
            entity.type == ItemID.LesserManaPotion || entity.type == ItemID.ManaPotion
            || entity.type == ItemID.GreaterManaPotion || entity.type == ItemID.SuperManaPotion;

        public override void OnConsumeItem(Item item, Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                player.GetModPlayer<ManaDrinkPlayer>().Drank(item.type);
        }
    }

    /// <summary>
    /// What a mana potion does on its way down, besides the mana.
    ///
    /// Every drink: Mana Nausea, by strength (ManaNausea), added on to any still going up to
    /// its most. And a count of the drinks in the
    /// last <see cref="Window"/>, the lesser and ordinary potions in one tally and the greater
    /// and super in another: one more than <see cref="Limit"/> in a tally and it is Mana
    /// Poisoning, I or II (ManaPoisoning), for its full duration from that drink.
    ///
    /// The tallies are this player's and this session's; a death clears them, as it clears
    /// the poisoning.
    /// </summary>
    public class ManaDrinkPlayer : ModPlayer
    {
        /// <summary>How far back the drinks are counted, in ticks. Two minutes.</summary>
        public const int Window = 2 * 60 * 60;

        /// <summary>More drinks than this inside the window is poisoning.</summary>
        public const int Limit = 5;

        /// <summary>When each drink went down, in game ticks: lesser and ordinary potions, and greater and super.</summary>
        private readonly Queue<uint> small = new Queue<uint>(), strong = new Queue<uint>();

        internal void Drank(int potion)
        {
            int nausea = potion == ItemID.SuperManaPotion ? 3 : potion == ItemID.GreaterManaPotion ? 2 : 1;
            int nauseaType = ManaNausea.TypeFor(nausea);
            int index = Player.FindBuffIndex(nauseaType);

            // A drink on top of one still going adds to it, up to the most three drinks give.
            if (index >= 0)
                Player.buffTime[index] = Math.Min(Player.buffTime[index] + ManaNausea.Durations[nausea], ManaNausea.MaxDurations[nausea]);
            else
                Player.AddBuff(nauseaType, ManaNausea.Durations[nausea]);

            bool isStrong = nausea >= 2;
            Queue<uint> tally = isStrong ? strong : small;
            uint now = Main.GameUpdateCount;

            tally.Enqueue(now);
            while (tally.Count > 0 && now - tally.Peek() > Window)
                tally.Dequeue();

            if (tally.Count > Limit)
            {
                int poisoning = isStrong ? ModContent.BuffType<ManaPoisoning2>() : ModContent.BuffType<ManaPoisoning1>();
                Player.AddBuff(poisoning, ManaPoisoning.Duration);
            }
        }

        /// <summary>The poisoning's life loss. lifeRegen counts half points of life a second.</summary>
        public override void UpdateBadLifeRegen()
        {
            int perSecond = 0;
            if (Player.HasBuff<ManaPoisoning2>())
                perSecond = ModContent.GetInstance<ManaPoisoning2>().LifePerSecond;
            else if (Player.HasBuff<ManaPoisoning1>())
                perSecond = ModContent.GetInstance<ManaPoisoning1>().LifePerSecond;

            if (perSecond == 0)
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= perSecond * 2;
        }

        /// <summary>
        /// Life regen that runs out with nothing more particular to blame reports the death as
        /// burning (vanilla's "other" reason 8). Poisoned by mana, it was the mana.
        /// </summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (damageSource.SourceOtherIndex == 8
                && (Player.HasBuff<ManaPoisoning1>() || Player.HasBuff<ManaPoisoning2>()))
            {
                damageSource = PlayerDeathReason.ByCustomReason(
                    NetworkText.FromKey("Mods.PisAndDag.DeathText.ManaPoisoning", Player.name));
            }

            return true;
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            small.Clear();
            strong.Clear();
        }
    }
}
