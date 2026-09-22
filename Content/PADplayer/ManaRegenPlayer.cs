using System;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// Mana does not come back on its own. Vanilla's regen, which refills the bar for
    /// standing still, is switched off entirely; what comes back is what accessories
    /// give, a few points a second, the way life regen is a thing you wear.
    ///
    /// Vanilla's regen is a counter fed by <c>manaRegen</c> a tick that pays a point
    /// every 120. It is killed by pushing <c>manaRegenBonus</c> far below zero just
    /// before UpdateManaRegen runs, so the counter only ever falls, and by zeroing the
    /// counter after, so it cannot wrap. Mana potions and the Nebula armour's boosters
    /// are separate paths and still work; the Mana Regeneration buff is not, and does
    /// nothing now.
    ///
    /// Accessories add to <see cref="RegenPerSecond"/> from UpdateAccessory (it is
    /// reset every tick). Vanilla's mana regen accessories count too, read off
    /// <c>manaRegenBonus</c> before it is buried: the Mana Regeneration Band's 25
    /// becomes two points a second. After a cast the regen starts from nothing and
    /// climbs back to its full rate over <see cref="RampTicks"/>, so it builds rather
    /// than snaps back.
    ///
    /// Hooked around UpdateManaRegen: PostUpdateMiscEffects runs just before it,
    /// PreUpdateMovement just after.
    /// </summary>
    public class ManaRegenPlayer : ModPlayer
    {
        /// <summary>Mana a second from what is worn. Reset every tick; accessories add to it.</summary>
        public float RegenPerSecond;

        /// <summary>What one point of vanilla manaRegenBonus is worth, in mana a second.</summary>
        public const float VanillaBonusRate = 0.08f;

        /// <summary>Ticks after a cast before the regen is back at its full rate. Three seconds.</summary>
        public const int RampTicks = 180;

        private const int Bury = 100000;

        private float due;
        private int sinceSpend = RampTicks;
        private int lastMana = -1;

        /// <summary>For an accessory: this much more mana a second while it is worn.</summary>
        public static void Add(Player player, float perSecond) =>
            player.GetModPlayer<ManaRegenPlayer>().RegenPerSecond += perSecond;

        public override void ResetEffects()
        {
            RegenPerSecond = 0f;
        }

        public override void PostUpdateMiscEffects()
        {
            // Vanilla's accessories have all reported in by now. Take theirs, then bury
            // the field so vanilla's own regen never pays out.
            if (Player.manaRegenBonus > 0)
                RegenPerSecond += Player.manaRegenBonus * VanillaBonusRate;

            Player.manaRegenBonus -= Bury;
        }

        public override void PreUpdateMovement()
        {
            Player.manaRegenCount = 0;

            // A cast is a drop in mana between one tick and the next. The ramp starts over.
            if (lastMana >= 0 && Player.statMana < lastMana)
                sinceSpend = 0;
            else if (sinceSpend < RampTicks)
                sinceSpend++;

            if (RegenPerSecond > 0f && Player.statMana < Player.statManaMax2)
            {
                float ramp = sinceSpend / (float)RampTicks;
                due += RegenPerSecond * ramp / 60f;

                while (due >= 1f)
                {
                    due -= 1f;
                    Player.statMana = Math.Min(Player.statManaMax2, Player.statMana + 1);
                }
            }
            else
            {
                due = 0f;
            }

            lastMana = Player.statMana;
        }
    }
}
