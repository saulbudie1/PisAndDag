using System;
using PisAndDag.Content.Items.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Blood Drenched Sword: what holding Enter three seconds with Foigh's Rust Side Sword in
    /// hand puts on (RustSideHoldout, the focus). By request: two minutes of it; the sword's
    /// every hit <see cref="DamageMultiplier"/> times as hard; defense
    /// <see cref="DefenseMultiplier"/> times; the most life <see cref="LifeMultiplier"/> of
    /// what it was (three fifths); every speed <see cref="SpeedMultiplier"/> times. The sword
    /// itself is red under it, burning black and red, its slashes red, its chain ends in the
    /// leap and the drive (RustSideMoves), and every blow of it leaves the enemy bleeding
    /// (UltraBleedingGeneric). When it ends, however it ends, Dried Blood follows: two minutes
    /// before the sword can be drenched again.
    ///
    /// Every number is here and nowhere else. What it does is BloodDrenchedPlayer's; the
    /// sword's damage is the item's (FoighsRustSideSword.ModifyWeaponDamage). Not saved with
    /// the player, as the mod's other buffs are not; and it can be clicked off, which starts
    /// the wait as its end would.
    /// </summary>
    public class BloodDrenchedSword : ModBuff
    {
        /// <summary>Two minutes.</summary>
        public const int Duration = 2 * 60 * 60;

        public const float DamageMultiplier = 1.75f;
        public const float DefenseMultiplier = 1.6f;
        public const float LifeMultiplier = 0.6f;
        public const float SpeedMultiplier = 1.25f;

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>
    /// Dried Blood: the two minutes after Blood Drenched Sword ends before the sword can be
    /// drenched again (by request: "Cooldown for 2 mins"). Counted from the end of the blood,
    /// not its start, so that it is two minutes with the blood and two without. A debuff that
    /// cannot be clicked off; the Nurse does not cure it; it is kept through death (the game
    /// takes every other buff then), and not saved.
    /// </summary>
    public class DriedBlood : ModBuff
    {
        /// <summary>Two minutes.</summary>
        public const int Duration = 2 * 60 * 60;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.persistentBuff[Type] = true;

            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }
    }

    /// <summary>
    /// What Blood Drenched Sword does to a player, and its wait.
    ///
    /// Defense and the most life: after everything else of the player's has been added up
    /// (PostUpdateMiscEffects): defense multiplied as the game multiplies it (its final
    /// multiplier, DefenseStat), the most life cut to three fifths of all of it, what is worn
    /// included; the game then brings their life down to it if it was over. Speed, all of it:
    /// every item used, the sword and any other weapon, a tool, a block (UseSpeedMultiplier:
    /// the use time and the animation, as the game reckons both); running (top speed,
    /// acceleration, and a pair of boots' top speed: PostUpdateRunSpeeds); and the jump (the
    /// speed it leaves the ground at: the game's jump speed is set afresh for each player every
    /// update, and this is after it is and before it is used).
    ///
    /// The wait: on the player's own machine, the tick the blood is gone, however it went (run
    /// out, clicked off, taken by death), Dried Blood goes on. The game tells every other
    /// machine.
    ///
    /// And Enter: while the sword is in hand and could be drenched, a press of Enter on this
    /// machine is the sword's, not the chat's (the game opens the chat on a press of Enter
    /// only once Enter has been let go since the last: Main.DoUpdate_Enter_ToggleChat, which
    /// this keeps from thinking it has). Drenched, or waiting, or with anything else in hand,
    /// Enter opens the chat as it always does.
    /// </summary>
    public class BloodDrenchedPlayer : ModPlayer
    {
        private bool wasDrenched;

        public static bool Drenched(Player player) => player.HasBuff(ModContent.BuffType<BloodDrenchedSword>());

        /// <summary>Neither drenched nor waiting: the sword can be drenched.</summary>
        public static bool CanDrench(Player player) => !Drenched(player) && !player.HasBuff(ModContent.BuffType<DriedBlood>());

        /// <summary>
        /// The blood on, on the player's own machine: the buff for its two minutes, and every
        /// debuff the Nurse would take off (her own rule: a debuff with time left that the game
        /// does not keep from her, BuffID.Sets.NurseCannotRemoveDebuff, which keeps the buffs it
        /// only marks as debuffs so they cannot be clicked off: a campfire's, a heart lantern's,
        /// honey's; and Potion Sickness and hunger).
        /// </summary>
        public static void Drench(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            for (int i = 0; i < player.buffType.Length; i++)
            {
                int type = player.buffType[i];

                if (type > 0 && player.buffTime[i] > 0 && Main.debuff[type] && !BuffID.Sets.NurseCannotRemoveDebuff[type])
                {
                    player.DelBuff(i);
                    i = -1;
                }
            }

            player.AddBuff(ModContent.BuffType<BloodDrenchedSword>(), BloodDrenchedSword.Duration);
        }

        public override void PostUpdateBuffs() => Watch();

        public override void UpdateDead() => Watch();

        /// <summary>The blood gone since last tick: the wait begins.</summary>
        private void Watch()
        {
            bool now = Drenched(Player);

            if (wasDrenched && !now && Player.whoAmI == Main.myPlayer)
                Player.AddBuff(ModContent.BuffType<DriedBlood>(), DriedBlood.Duration);

            wasDrenched = now;
        }

        public override void PostUpdateMiscEffects()
        {
            if (!Drenched(Player))
                return;

            Player.statDefense *= BloodDrenchedSword.DefenseMultiplier;
            Player.statLifeMax2 = Math.Max(1, (int)(Player.statLifeMax2 * BloodDrenchedSword.LifeMultiplier));
        }

        public override float UseSpeedMultiplier(Item item) => Drenched(Player) ? BloodDrenchedSword.SpeedMultiplier : 1f;

        public override void PostUpdateRunSpeeds()
        {
            if (!Drenched(Player))
                return;

            Player.maxRunSpeed *= BloodDrenchedSword.SpeedMultiplier;
            Player.accRunSpeed *= BloodDrenchedSword.SpeedMultiplier;
            Player.runAcceleration *= BloodDrenchedSword.SpeedMultiplier;
            Player.jumpSpeed *= BloodDrenchedSword.SpeedMultiplier;
        }

        public override void PostUpdate()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Main.drawingPlayerChat)
                return;

            if (Player.HeldItem.type == ModContent.ItemType<FoighsRustSideSword>() && CanDrench(Player))
                Main.chatRelease = false;
        }
    }
}
