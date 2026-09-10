using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Melee;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Items.Melee
{  
	public class BladeOfMors : ModItem
	{
        private const float MeterMax = 100f;
         
        private const float MaxDropletsPerTick = 1.35f;
         
        private const float ConeHalfAngle = 25f;

        private static readonly Color BloodColor = new Color(117, 9, 24);
         
        private static readonly Color SiphonColor = new Color(128, 0, 60);

        private int ReleaseTimer;

        private float Meterlogy = 0f;

        private bool Full = false;

        private float dropletBudget;

        /// <summary>Ticks a second click still counts as a double click.</summary>
        private const int DoubleClickWindow = 10;

        /// <summary>Damage multiplier the release gets over a normal swing.</summary>
        private const float ReleaseDamageScale = 3f;

        private const float ReleaseSpeed = 13f;

        // ---- release animation, 45 ticks = 0.75 seconds ----------------------------
        //
        // 1..22   one siphon mote per tick, sweeping clockwise around the player
        // 40      the last mote lands; blood bursts out of the player
        // 45      the blade is thrown
        //
        // SiphonLifetime is what ties it together: a mote spawned on the final tick of
        // the sweep (22) arrives at exactly BurstTick (22 + 18 = 40), so the burst reads
        // as the ring finishing rather than as a separate event.

        private const int ReleaseDuration = 45;
        private const int SiphonCount = 22;
        private const int SiphonLifetime = 18;
        private const int BurstTick = 40;
        private const float RingRadius = 92f;

        private int clickWindow;
        private int clickCount;

        private bool releaseActive;
        private int releaseTimer;
        public override void SetDefaults()
        {
            Item.damage = 30;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = 10000;
            Item.PAD().Lore = true;
            Item.PAD().LoreText = "A blade meant not to kill, but to finish what death had already begun.\r\nIts former bearer walked beside the dying, carrying no banner and seeking no glory.\r\nStrangely, none remember his name.";
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
            Item.useTurn = true; 
            Item.autoReuse = true;
        }
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (PADWorld.Legends)
            {
                float percent = MathHelper.Clamp(Meterlogy / MeterMax * 100f, 0f, 100f);


                tooltips.Add(new TooltipLine(Mod, "DeathMeter", "Upon hitting an enemy, the blade charges with dark energy\r\nOnce fully charged, Double Click can unleash a devastating attack"));
                tooltips.Add(new TooltipLine(Mod, "DeathMeter",
                    percent.ToString("F1") + "% of death meter filled"));
                
            }
        }
        public override bool? UseItem(Player player)
        {


            return base.UseItem(player);
        }
        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (PADWorld.Legends)
            {
                Meterlogy += Main.rand.NextFloat(1.5f, 3f) * (hit.Crit ? 1.5f : 1f);

                if (Meterlogy > MeterMax)
                    Meterlogy = MeterMax;

                Full = Meterlogy >= MeterMax;

                // Pull blood out of the target and into the wielder — the meter filling,
                // made visible at the moment it happens.
                if (!Main.dedServ)
                    BloodSiphon.SpawnDrain(target, player, Main.rand.Next(5, 9), SiphonColor);
            }
        }
        /// <summary>
        /// Runs every tick while the blade is held. Bleeds blood from the player in
        /// proportion to the charge, so the meter is readable without opening a tooltip.
        /// </summary>
        public override void HoldItem(Player player)
        {
            // Deliberately outside the Legends gate — the charge and its release are not
            // conditional on the world mode, only the visual aura below is.
            if (player.whoAmI == Main.myPlayer)
            {
                UpdateReleaseInput(player);

                if (releaseActive)
                    UpdateReleaseSequence(player);
            }

            if (PADWorld.Legends)
            {
                // Purely decorative and client-side — never run it on a server.
                if (Main.dedServ)
                    return;

                float charge = MathHelper.Clamp(Meterlogy / MeterMax, 0f, 1f);
                if (charge <= 0f)
                {
                    dropletBudget = 0f;
                    return;
                }

                // Squared, so a nearly-empty blade barely weeps and a full one gushes.
                // Swap for plain `charge` if you want the ramp to feel linear.
                dropletBudget += charge * charge * MaxDropletsPerTick;

                while (dropletBudget >= 1f)
                {
                    dropletBudget -= 1f;

                    // Near the centre, with just enough jitter that the two cones don't
                    // emerge from a single pixel.
                    Vector2 position = player.Center + Main.rand.NextVector2Circular(4f, 6f);

                    // Straight out to one side or the other, within 20 degrees of level —
                    // so each side is a 40 degree wedge, 20 up and 20 down.
                    int side = Main.rand.NextBool() ? 1 : -1;
                    float spread = MathHelper.ToRadians(Main.rand.NextFloat(-ConeHalfAngle, ConeHalfAngle));
                    Vector2 velocity = new Vector2(side, 0f).RotatedBy(spread)
                        * Main.rand.NextFloat(1.4f, 3.6f);

                    // Weightless: it drifts out and hangs rather than raining down.
                    PADParticleHandler.SpawnParticle(new BloodDroplet(
                        position,
                        velocity,
                        Main.rand.Next(30, 55),
                        Main.rand.NextFloat(0.4f, 0.7f) * (0.6f + charge * 0.5f),
                        BloodColor,
                        affectedByGravity: false));
                }

                // A faint red wash on the player sells the charge even between droplets.
                Lighting.AddLight(player.Center, BloodColor.ToVector3() * 0.45f * charge);
            }
        }

        /// <summary>
        /// Watches for a double left click and releases the charge on the second one.
        /// </summary>
        private void UpdateReleaseInput(Player player)
        {
            // A sequence already running owns the blade; ignore further clicks.
            if (releaseActive)
                return;

            if (clickWindow > 0)
                clickWindow--;
            else
                clickCount = 0;

            // True only on the frame the button goes down. Without the release check,
            // holding the button to autoswing would read as sixty clicks a second.
            if (!Main.mouseLeft || !Main.mouseLeftRelease)
                return;

            clickCount++;
            clickWindow = DoubleClickWindow;

            if (clickCount < 2 || !Full)
                return;

            BeginRelease();
            clickCount = 0;
            clickWindow = 0;
        }

        /// <summary>
        /// Starts the wind-up. The charge is spent here rather than at the throw, so a
        /// second double click during the animation cannot queue another release.
        /// </summary>
        private void BeginRelease()
        {
            releaseActive = true;
            releaseTimer = 0;

            Meterlogy = 0f;
            Full = false;
            dropletBudget = 0f;
        }

        /// <summary>
        /// Advances the 0.75 second release: the ring draws itself in, blood bursts out,
        /// and only then does the blade leave the player's hand.
        /// </summary>
        private void UpdateReleaseSequence(Player player)
        {
            releaseTimer++;

            if (!Main.dedServ)
            {
                SpawnSiphonRingStep(player);

                if (releaseTimer == BurstTick)
                    BurstFromPlayer(player);
            }

            if (releaseTimer >= ReleaseDuration)
            {
                FireProjectile(player);
                releaseActive = false;
                releaseTimer = 0;
            }
        }

        /// <summary>
        /// One mote per tick, sweeping around the player and flying inward.
        /// </summary>
        private void SpawnSiphonRingStep(Player player)
        {
            if (releaseTimer > SiphonCount)
                return;

            // 0 on the first tick, just under 1 on the last, so the ring closes without
            // doubling up on its starting point.
            float around = (releaseTimer - 1) / (float)SiphonCount;

            // Start directly above the player. Screen Y grows downward, so an INCREASING
            // angle sweeps clockwise here — the opposite of the usual maths convention.
            float angle = -MathHelper.PiOver2 + around * MathHelper.TwoPi;

            Vector2 outward = angle.ToRotationVector2();

            // Land somewhere in the middle of the hitbox rather than one exact pixel.
            Vector2 landing = new Vector2(player.width * 0.5f, player.height * 0.5f)
                + Main.rand.NextVector2Circular(4f, 6f);

            // A tangential shove makes each mote spiral inward instead of falling
            // straight at the player, which reads far more like being drawn in.
            Vector2 launch = outward.RotatedBy(MathHelper.PiOver2)
                * Main.rand.NextFloat(2f, 3.5f);

            PADParticleHandler.SpawnParticle(new BloodSiphon(
                player.Center + outward * RingRadius,
                player,
                landing,
                launch,
                SiphonLifetime,
                Main.rand.NextFloat(0.55f, 0.95f),
                SiphonColor));

            // Tension rises as the ring closes.
            player.PIS().AddScreenShake(1.2f + 3.4f * around, 5);
        }

        /// <summary>
        /// Everything the ring gathered, thrown straight back out.
        /// </summary>
        private void BurstFromPlayer(Player player)
        {
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion, player.Center);
            player.PIS().AddScreenShake(8f, 14);

            int count = Main.rand.Next(40, 60);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2();

                // Weightless, so it reads as a discharge rather than gore falling.
                // Flip this to true if you would rather it rain down.
                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    player.Center + direction * Main.rand.NextFloat(0f, 8f),
                    direction * Main.rand.NextFloat(3f, 9f),
                    Main.rand.Next(35, 60),
                    Main.rand.NextFloat(0.5f, 1f),
                    BloodColor,
                    affectedByGravity: false));
            }
        }

        /// <summary>
        /// The throw itself, at the very end of the sequence. Aim is taken now rather
        /// than at the click, so the blade goes where the cursor actually is.
        /// </summary>
        private void FireProjectile(Player player)
        {
            Vector2 velocity = (Main.MouseWorld - player.Center)
                .SafeNormalize(new Vector2(player.direction, 0f)) * ReleaseSpeed;

            Projectile.NewProjectile(
                player.GetSource_ItemUse(Item),
                player.Center,
                velocity,
                ModContent.ProjectileType<BladeOfMorsProj>(),
                (int)(Item.damage * ReleaseDamageScale),
                10f,
                player.whoAmI);

            SoundEngine.PlaySound(SoundID.Item1, player.Center);
            player.PIS().AddScreenShake(6f, 18);

            // The meter was already spent in BeginRelease.
        }

        public override bool CanUseItem(Player player)
        {
            return ReleaseTimer <= 0;
        }
         
        public override void SaveData(TagCompound tag)
        {
            if (Meterlogy > 0f)
                tag["Meterlogy"] = Meterlogy;
        }

        public override void LoadData(TagCompound tag)
        { 
            Meterlogy = tag.GetFloat("Meterlogy");
            Full = Meterlogy >= MeterMax;
        }
        //public override void AddRecipes()
        //{
        //	Recipe recipe = CreateRecipe();
        //	recipe.AddIngredient(ItemID.DirtBlock, 10);
        //	recipe.AddTile(TileID.WorkBenches);
        //	recipe.Register();
        //}
    }
}
