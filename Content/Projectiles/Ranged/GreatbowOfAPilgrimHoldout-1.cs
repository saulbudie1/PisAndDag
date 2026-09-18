using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    public class GreatbowOfAPilgrimHoldout : BaseIdleHoldoutProjectile
    {
        public bool OwnerCanShoot => Owner.HasAmmo(Owner.HeldItem) && !Owner.noItems && !Owner.CCed;

        public float StringReelbackInterpolant
        {
            get
            {
                int duration = Owner.HeldItem.useAnimation;
                float time = duration - ShootDelay;
                float firstHalf = Utils.GetLerpValue(8f, 0f, time, true);
                float secondHalf = Utils.GetLerpValue(8f, duration * 0.6f, time, true);
                return firstHalf + secondHalf;
            }
        }
        public float StringReelbackDistance => Projectile.width * StringReelbackInterpolant * 0.3f;

        // Where the string should go, relative to the center of the projectile
        public Vector2 topStringOffset => new Vector2(-8f, -32f);
        public Vector2 bottomStringOffset => new Vector2(-8f, 32f);
        public float StringHalfHeight => (Math.Abs(topStringOffset.Y) + Math.Abs(bottomStringOffset.Y)) * 0.5f;

        public float ChargeupInterpolant => Utils.GetLerpValue(GreatbowOfAPilgrim.ShootDelay, GreatbowOfAPilgrim.MaxChargeTime, ChargeTimer, true);
        public ref float CurrentChargingFrames => ref Projectile.ai[0];
        public ref float ChargeTimer => ref Projectile.ai[1];

        public ref float ShootDelay => ref Projectile.localAI[0];

        public override int AssociatedItemID => ModContent.ItemType<GreatbowOfAPilgrim>();
        public override int IntendedProjectileType => ModContent.ProjectileType<GreatbowOfAPilgrimHoldout>();

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.MaxUpdates = 2;
        }

        public override void SafeAI()
        {
            Vector2 armPosition = Owner.RotatedRelativePoint(Owner.MountedCenter, true);
            Vector2 tipPosition = armPosition + Projectile.velocity * Projectile.width * 0.45f;

            // Activate shot behavior if the owner stops channeling or otherwise cannot use the weapon.
            bool activatingShoot = ShootDelay <= 0 && Main.mouseLeft && !Main.mapFullscreen && !Owner.mouseInterface;
            if (Main.myPlayer == Projectile.owner && OwnerCanShoot && activatingShoot)
            {
                SoundEngine.PlaySound(SoundID.Item75, Projectile.Center);
                ShootDelay = Owner.HeldItem.useAnimation;

                // One arrow per reel-back, along the line the bow is pointing.
                // PickAmmo is what spends the ammunition and what decides the type, the
                // speed and the arrow's own damage and knockback, so a draw that never
                // happens costs nothing.
                Owner.PickAmmo(Owner.HeldItem, out int arrowType, out float shootSpeed, out int arrowDamage, out float arrowKnockback, out int usedAmmoItemID, false);
                 
                if (ChargeTimer >= GreatbowOfAPilgrim.MaxChargeTime)
                {
                    Vector2 aim = Projectile.velocity.SafeNormalize(Owner.SafeDirectionTo(Main.MouseWorld));
                    int a = Projectile.NewProjectile(Owner.GetSource_ItemUse_WithPotentialAmmo(Owner.HeldItem, usedAmmoItemID), tipPosition, aim * shootSpeed*1.125f, arrowType,  (int)(arrowDamage*1.75f), arrowKnockback*2, Projectile.owner);
                    Main.projectile[a].penetrate = -1; 
                    Main.projectile[a].tileCollide = false;
                    Main.projectile[a].usesLocalNPCImmunity = true;
                    Main.projectile[a].localNPCHitCooldown = 6;
                    Main.projectile[a].PAD().Chomp = true;
                    Main.projectile[a].PAD().FreezeTicks = 28;
                    Main.projectile[a].PAD().ChompCool = 7;

                }
                else
                {
                    Vector2 aim = Projectile.velocity.SafeNormalize(Owner.SafeDirectionTo(Main.MouseWorld));
                    int a = Projectile.NewProjectile(Owner.GetSource_ItemUse_WithPotentialAmmo(Owner.HeldItem, usedAmmoItemID), tipPosition, aim * shootSpeed, arrowType,  arrowDamage, arrowKnockback, Projectile.owner);
                    Main.projectile[a].penetrate = 5;

                    // Local immunity is not optional here, it is what makes penetrate mean
                    // anything. SetDefaults_End does maxPenetrate = penetrate, and that ran
                    // inside NewProjectile a line ago, so this arrow still has
                    // maxPenetrate 1. Projectile.Damage skips the shared npc.immune[owner]
                    // window entirely when maxPenetrate is 1 and neither immunity flag is
                    // set, so without these two lines the arrow hits the same enemy on
                    // every tick and spends all five penetrations in five ticks.
                    //
                    // -1 is once per enemy for the arrow's whole life, because Update only
                    // counts localNPCImmunity down while it is above zero. So penetrate 5
                    // is five different enemies, which is the shotbow tier in the plan.
                    Main.projectile[a].usesLocalNPCImmunity = true;
                    Main.projectile[a].localNPCHitCooldown = -1;

                }
                Projectile.netUpdate = true;

                ChargeTimer = 0;

            }

            // Count the reel-back down. Without this the string is pulled once and stays
            // pulled, because activatingShoot can never be true again.
            if (ShootDelay > 0f)
                ShootDelay--;

            // Update damage based on current ranged damage stat, since this projectile exists regardless of if it's being fired.
            Projectile.damage = Owner.HeldItem is null ? 0 : Owner.GetWeaponDamage(Owner.HeldItem);

            UpdateProjectileHeldVariables(armPosition);
            ManipulatePlayerVariables();


            // Create orange exo energy at the tip of the bow.
            Color energyColor = Color.Orange;
            Vector2 verticalOffset = Vector2.UnitY.RotatedBy(Projectile.rotation) * 8f;
            if (Math.Cos(Projectile.rotation) < 0f)
                verticalOffset *= -1f;


            // Create light at the tip of the bow.
            DelegateMethods.v3_1 = energyColor.ToVector3();
            Utils.PlotTileLine(tipPosition - verticalOffset, tipPosition + verticalOffset, 10f, DelegateMethods.CastLightOpen);
            Lighting.AddLight(tipPosition, energyColor.ToVector3());

            // Create a puff of energy in a star shape and play a sound to indicate that the bow is at max charge.
            if (ShootDelay <= 0)
                ChargeTimer++;
            if (ChargeTimer == GreatbowOfAPilgrim.MaxChargeTime)
            {

                Vector2 hit = Owner.Center;

                // Starts above the top edge, so the bolt is already in flight when it enters
                // frame rather than appearing out of nothing.
                Vector2 from = new Vector2(
                    hit.X + Main.rand.NextFloat(-140f, 140f),
                    Owner.Center.Y - Main.screenHeight * 0.75f);

                Lightning.Spawn(from, hit, Color.Lerp(Color.Orange, Color.Pink, (float)Math.Cos(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f) * 0.5f, width: 34f, lifetime: 26, forks: 4);


                for (int i = 0; i < 16; i++)
                {
                    Dust dust = Dust.NewDustPerfect(hit, DustID.Electric,
                        Main.rand.NextVector2Circular(5.5f, 5.5f), 0, Color.Lerp(Color.Orange, Color.Pink, (float)Math.Cos(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f) * 0.5f, 1.6f);
                    dust.noGravity = true;
                }
                 
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.7f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.6f, Pitch = 0.4f }, Owner.Center);
                ChargeTimer++;
            }
        }

        public void UpdateProjectileHeldVariables(Vector2 armPosition)
        {
            if (Main.myPlayer == Projectile.owner)
            {
                // 15NOV2024: Ozzatron: clamped mouse position unnecessary, this is only used to aim the Heavenly Gale bow

                float aimInterpolant = Utils.GetLerpValue(10f, 40f, Projectile.Distance(Main.MouseWorld), true);
                Vector2 oldVelocity = Projectile.velocity;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.SafeDirectionTo(Main.MouseWorld), aimInterpolant);
                if (Projectile.velocity != oldVelocity)
                    Projectile.ForceNetUpdate();
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // The bow hangs off the BACK hand, which is the one SetCompositeArmBack poses
            // below and the one a grip belongs in. Rather than guess a radius, ask Terraria
            // where that hand is: GetBackHandPosition is handed the same rotation the arm
            // is, so the two can never drift apart.
            //
            // No further offset is needed because the grip is already the middle of the
            // sheet. Measured: the limb spans columns 30..35 at mid height of a 64 wide
            // sheet, centre 32.5, and the draw origin is texture.Size() * 0.5f = 32.
            //
            // The 44f this replaces is Heavenly Gale's. Their bow is 176 across and is
            // meant to float out in front of the archer; on a 64 sheet it puts the grip
            // about thirty pixels past the hand.
            //
            // The armPosition term is only the correction for a rotated player, which
            // GetBackHandPosition does not apply and RotatedRelativePoint does.
            Projectile.Center = Owner.GetBackHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2)
                + (armPosition - Owner.MountedCenter);
            Projectile.spriteDirection = Projectile.direction;
            Projectile.timeLeft = 2;
        }

        public void ManipulatePlayerVariables()
        {
            Owner.ChangeDir(Projectile.direction);
            Owner.heldProj = Projectile.whoAmI;

            // Make the player lower their front arm a bit to indicate the pulling of the string.
            // This is precisely calculated by representing the top half of the string as a right triangle and using SOH-CAH-TOA to
            // calculate the respective angle from the appropriate widths and heights.
            float frontArmRotation = (float)Math.Atan(StringHalfHeight / MathHelper.Max(StringReelbackDistance, 0.001f) * 0.5f);
            if (Owner.direction == -1)
                frontArmRotation += MathHelper.PiOver4;
            else
                frontArmRotation = MathHelper.PiOver2 - frontArmRotation;
            frontArmRotation += Projectile.rotation + MathHelper.Pi + Owner.direction * MathHelper.PiOver2 + 0.12f;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.velocity.ToRotation() - MathHelper.PiOver2);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            // Draw the string of the bow. It reels back in the initial frames.
            Vector2 topOfBow = Projectile.Center + Vector2.UnitX.RotatedBy(Projectile.rotation + topStringOffset.ToRotation()) * topStringOffset.Length();
            Vector2 bottomOfBow = Projectile.Center + Vector2.UnitX.RotatedBy(Projectile.rotation + bottomStringOffset.ToRotation()) * bottomStringOffset.Length();
            Vector2 endOfString = Projectile.Center - Projectile.rotation.ToRotationVector2() * (StringReelbackDistance + (1f - StringReelbackInterpolant) * 25f);

            float chargeOffset = ChargeupInterpolant * Projectile.scale * 3f;
            Color chargeColor = Color.Lerp(new Color(239, 118, 3), new Color(181, 86, 144), (float)Math.Cos(Main.GlobalTimeWrappedHourly * 7.1f) * 0.5f + 0.5f) * ChargeupInterpolant * 0.6f;
            chargeColor.A = 0;

            float rotation = Projectile.rotation;
            SpriteEffects direction = SpriteEffects.None;
            if (Math.Cos(rotation) < 0f)
            {
                direction = SpriteEffects.FlipHorizontally;
                rotation += MathHelper.Pi;
            }

            Color stringColor = Color.Lerp(Color.Orange, Color.Pink, (float)Math.Cos(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f) *  0.5f;
            Main.spriteBatch.DrawLineBetter(topOfBow, endOfString, stringColor, 2f);
            Main.spriteBatch.DrawLineBetter(bottomOfBow, endOfString, stringColor, 2f);
            if (ChargeTimer >= GreatbowOfAPilgrim.MaxChargeTime)
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector2 drawOffset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * chargeOffset;
                    Main.spriteBatch.Draw(texture, drawPosition + drawOffset, null, chargeColor, rotation, origin, Projectile.scale, direction, 0f);
                }
            }
            Main.spriteBatch.Draw(texture, drawPosition, null, Projectile.GetAlpha(lightColor), rotation, origin, Projectile.scale, direction, 0f);
            return false;
        }
         
        public override bool? CanDamage() => false;
    }
}
