using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.NPCs.Abomanation_Small;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Summoner
{
    /// <summary>
    /// Joint Arms of the Defiled: a whip, from Amalgaermis. Every player's own off the kill in
    /// classic, and in the treasure bag in expert and master (AbomanationHead.ModifyNPCLoot,
    /// AmalgaermisBag). 30 damage, a 25-tick swing, as long as a Firecracker; the swing itself
    /// is JointArmsOfTheDefiledProj.
    ///
    /// Its streak is the Chlorophyte Rifle's, on a whip (JointArmsStreak): a crack that lands
    /// counts one, however many it hits; five in a row charge it; the next crack spends the
    /// charge and gives Many Hands, 10% more summon damage for 16 seconds. A crack that lands
    /// nothing, 3.5 seconds without one landing, putting the whip away, or dying ends the
    /// streak. The crack that spends it starts the next one if it lands.
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class JointArmsOfTheDefiled : ModItem
    {
        public const int Damage = 30;
        public const int UseTicks = 35;

        /// <summary>Firecracker's and Spinal Tap's.</summary>
        public const float Knockback = 2f;

        /// <summary>Firecracker's. The reach is this times the swing times the lash's RangeMultiplier.</summary>
        public const float ShootSpeed = 4f;

        private static readonly Color StreakColour = new Color(193, 62, 62);
        private static readonly Color ChargedColour = new Color(215, 118, 118);

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.DefaultToWhip(ModContent.ProjectileType<JointArmsOfTheDefiledProj>(), Damage, Knockback, ShootSpeed, UseTicks);
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        /// <summary>Whips take melee prefixes.</summary>
        public override bool MeleePrefix() => true;

        /// <summary>
        /// Throws the swing as vanilla would, telling it whether it spends the streak (ai[2]).
        /// The owner's call, and only the owner's machine uses an item; the swing and its ai
        /// go to everyone else as it is made.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            bool spends = player.GetModPlayer<JointArmsStreak>().Spend();

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0f, 0f, spends ? 1f : 0f);

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            JointArmsStreak streak = Main.LocalPlayer.GetModPlayer<JointArmsStreak>();

            if (streak.Armed)
            {
                tooltips.Add(new TooltipLine(Mod, "JointArmsCharged", this.GetLocalization("Charged").Value)
                {
                    OverrideColor = ChargedColour,
                });
            }
            else if (streak.Count > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "JointArmsStreak", this.GetLocalization("Streak").Format(streak.Count, JointArmsStreak.Required))
                {
                    OverrideColor = StreakColour,
                });
            }
        }
    }

    /// <summary>
    /// The streak of Joint Arms of the Defiled, kept as the Chlorophyte Rifle keeps its own
    /// (PisPlayer's Chlo*): a crack that lands counts one; five charge it; the next crack
    /// spends it for Many Hands. A miss, a lull of 3.5 seconds, the whip put away or a death
    /// ends it, and the blood it had built falls out of the player.
    ///
    /// On a ModPlayer of its own, not PADPlayer, because PADPlayer is reset every tick and a
    /// streak has to last between swings. The owner's only: hits are only ever told to the
    /// owner's machine, and Many Hands reaches everyone else as a buff does. What is seen of
    /// it is the owner's own read-out, as the rifle's is.
    /// </summary>
    public class JointArmsStreak : ModPlayer
    {
        /// <summary>Cracks that land, in a row, to charge it.</summary>
        public const int Required = 5;

        /// <summary>How long a streak lasts without a crack landing: 3.5 seconds, as the rifle's.</summary>
        public const int Decay = 210;

        /// <summary>Cracks that have landed in a row, up to Required.</summary>
        public int Count { get; private set; }

        /// <summary>Charged: the next crack spends it.</summary>
        public bool Armed { get; private set; }

        private int timer;

        private static readonly Color[] Reds =
        {
            Abomanation.Blood,
            new Color(168, 36, 48),
            new Color(193, 62, 62),
            new Color(215, 118, 118),
        };

        /// <summary>A crack landed (its first hit; the rest of that crack do not count). Called by the swing, on the owner's machine.</summary>
        public void Landed()
        {
            timer = Decay;

            // Charged already: it waits for the next crack to spend it.
            if (Armed)
                return;

            Count++;

            if (Count >= Required)
            {
                Count = Required;
                Armed = true;
            }

            Sparks();
        }

        /// <summary>A crack landed nothing.</summary>
        public void Missed() => Break(true);

        /// <summary>
        /// The crack being thrown spends the charge, if there is one: the streak starts again
        /// from nothing, Many Hands is given, and it shows. False if it was not charged.
        /// </summary>
        public bool Spend()
        {
            if (!Armed)
                return false;

            Break(false);
            Player.AddBuff(ModContent.BuffType<ManyHands>(), ManyHands.Ticks);
            Spent();

            return true;
        }

        /// <summary>The streak is over. <paramref name="lost"/> when it was lost rather than spent: then it falls out of the player.</summary>
        private void Break(bool lost)
        {
            if (lost)
                Spill();

            Count = 0;
            timer = 0;
            Armed = false;
        }

        public override void PostUpdate()
        {
            if (Count <= 0 && !Armed)
                return;

            // Here rather than on the item: an item stops being asked anything the moment it
            // leaves the hand, and the streak would wait for it to come back.
            if (Player.HeldItem.ModItem is not JointArmsOfTheDefiled)
            {
                Break(true);
                return;
            }

            if (--timer <= 0)
                Break(true);
        }

        public override void UpdateDead() => Break(true);

        // ---- seen: the owner's read-out ---------------------------------------------------------

        /// <summary>
        /// A crack landed: blood-red sparks off the player, more and longer as the streak runs
        /// (the rifle's ramp), and a bloom on the crack that charges it.
        /// </summary>
        private void Sparks()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer)
                return;

            float charge = MathHelper.Clamp((Count - 1) / (float)(Required - 1), 0f, 1f);
            float reach = Player.height * (0.8f + charge * 0.15f);

            SparkShard.Spray(Player.Center, Reds[2], count: 3 + (int)(charge * 4f), speed: 1.6f + charge * 1.8f,
                maxLength: reach, lifetime: 14 + (int)(charge * 8f), palette: Reds);

            if (Count == Required)
                PADParticleHandler.SpawnParticle(new BloomGeneric(Player.Center, Vector2.Zero, Reds[1], Player.height / 160f, 22));
        }

        /// <summary>The streak was lost: the blood built up falls out of the player, more the longer it was.</summary>
        private void Spill()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Count <= 0)
                return;

            int drops = 4 + Count * 2;

            for (int i = 0; i < drops; i++)
            {
                Vector2 from = new Vector2(Player.position.X + Main.rand.NextFloat(Player.width),
                    Player.position.Y + Main.rand.NextFloat(Player.height));
                Vector2 drift = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.6f, 0.2f));

                PADParticleHandler.SpawnParticle(new BloodDroplet(from, drift, Main.rand.Next(30, 50),
                    Main.rand.NextFloat(0.45f, 0.85f), Abomanation.Blood));
            }
        }

        /// <summary>The charge spent: a red bloom on the player and a wet sound as the crack goes.</summary>
        private void Spent()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.3f, Volume = 0.7f }, Player.Center);
            PADParticleHandler.SpawnParticle(new BloomGeneric(Player.Center, Vector2.Zero, Reds[1], Player.height / 110f, 26));
            SparkShard.Spray(Player.Center, Reds[2], count: 8, speed: 3.2f, maxLength: Player.height, lifetime: 22, palette: Reds);
        }
    }
}
