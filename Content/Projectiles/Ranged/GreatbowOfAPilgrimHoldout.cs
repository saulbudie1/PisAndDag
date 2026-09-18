//using System;
//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Graphics;
//using Terraria;
//using Terraria.Audio;
//using Terraria.ID;
//using Terraria.ModLoader;
//using PisAndDag.Content.Projectiles.Base;
//using PisAndDag.Content.Items.Ranged;   

//namespace PisAndDag.Content.Projectiles.Ranged
//{
//    public class GreatbowOfAPilgrimHoldout : BaseIdleHoldoutProjectile
//    { 
//        public bool OwnerCanShoot => Owner.HasAmmo(Owner.HeldItem) && !Owner.noItems && !Owner.CCed;

//        public float StringReelbackInterpolant
//        {
//            get
//            {
//                int duration = Owner.HeldItem.useAnimation;
//                float time = duration - ShootDelay;
//                float firstHalf = Utils.GetLerpValue(8f, 0f, time, true);
//                float secondHalf = Utils.GetLerpValue(8f, duration * 0.6f, time, true);
//                return firstHalf + secondHalf;
//            }
//        }
//        public float StringReelbackDistance => Projectile.width * StringReelbackInterpolant * 0.3f;

//        // Where the string should go, relative to the center of the projectile
//        public Vector2 topStringOffset => new Vector2(-40f, -56f);
//        public Vector2 bottomStringOffset => new Vector2(-40f, 46f);
//        public float StringHalfHeight => (Math.Abs(topStringOffset.Y) + Math.Abs(bottomStringOffset.Y)) * 0.5f;

//        public float ChargeupInterpolant => Utils.GetLerpValue(GreatbowOfAPilgrim.ShootDelay, GreatbowOfAPilgrim.MaxChargeTime, ChargeTimer, true);
//        public ref float CurrentChargingFrames => ref Projectile.ai[0];
//        public ref float ChargeTimer => ref Projectile.ai[1];

//        public ref float ShootDelay => ref Projectile.localAI[0];

//        public override int AssociatedItemID => ModContent.ItemType<GreatbowOfAPilgrim>();
//        public override int IntendedProjectileType => ModContent.ProjectileType<GreatbowOfAPilgrimHoldout>();

//        public override void SetDefaults()
//        {
//            Projectile.width = Projectile.height = 64;
//            Projectile.friendly = true;
//            Projectile.penetrate = -1;
//            Projectile.tileCollide = false;
//            Projectile.ignoreWater = true;
//            Projectile.DamageType = DamageClass.Ranged;
//            Projectile.MaxUpdates = 2;
//        }

//        public override void SafeAI()
//        {
//            Vector2 armPosition = Owner.RotatedRelativePoint(Owner.MountedCenter, true);
//            Vector2 tipPosition = armPosition + Projectile.velocity * Projectile.width * 0.45f;

//            // Activate shot behavior if the owner stops channeling or otherwise cannot use the weapon.
//            bool activatingShoot = ShootDelay <= 0 && Main.mouseLeft && !Main.mapFullscreen && !Owner.mouseInterface;
//            if (Main.myPlayer == Projectile.owner && OwnerCanShoot && activatingShoot)
//            {
//                SoundEngine.PlaySound(SoundID.Item75, Projectile.Center);
//                ShootDelay = Owner.HeldItem.useAnimation;
//                Projectile.netUpdate = true;
//            }

//            // Update damage based on current ranged damage stat, since this projectile exists regardless of if it's being fired.
//            Projectile.damage = Owner.HeldItem is null ? 0 : Owner.GetWeaponDamage(Owner.HeldItem);

//            UpdateProjectileHeldVariables(armPosition);
//            ManipulatePlayerVariables();

//            // Fire arrows.
//            if (ShootDelay > 0f && Projectile.FinalExtraUpdate())
//            {
//                float shootCompletionRatio = 1f - ShootDelay / (Owner.HeldItem.useAnimation - 1f);
//                float bowAngularOffset = (float)Math.Sin(MathHelper.TwoPi * shootCompletionRatio) * 0.4f;
//                float damageFactor = Utils.Remap(ChargeTimer, 0f, GreatbowOfAPilgrim.MaxChargeTime, 1f, GreatbowOfAPilgrim.MaxChargeDamageBoost);

//                // Fire arrows.
//                if (ShootDelay % GreatbowOfAPilgrim.ArrowShootRate == 0)
//                {
//                    Vector2 arrowDirection = Projectile.velocity.RotatedBy(bowAngularOffset);

//                    // Release a streak of energy.
//                    Color energyBoltColor = PADUtils.MulticolorLerp(shootCompletionRatio, PADUtils.ExoPalette);
//                    energyBoltColor = Color.Lerp(energyBoltColor, Color.White, 0.35f); 

//                    // Update the tip position for one frame.
//                    tipPosition = armPosition + arrowDirection * Projectile.width * 0.45f;

//                    if (Main.myPlayer == Projectile.owner && Owner.HasAmmo(Owner.HeldItem))
//                    {
//                        Item heldItem = Owner.HeldItem;
//                        Owner.PickAmmo(heldItem, out int projectileType, out float shootSpeed, out int damage, out float knockback, out _);
//                        damage = (int)(damage * damageFactor);
//                        projectileType = 1;

//                        bool createLightning = ChargeTimer / GreatbowOfAPilgrim.MaxChargeTime >= GreatbowOfAPilgrim.ChargeLightningCreationThreshold;
//                        Vector2 arrowVelocity = arrowDirection * shootSpeed;
//                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), tipPosition, arrowVelocity, projectileType, damage, knockback, Projectile.owner, createLightning.ToInt());
//                    }
//                }

//                ShootDelay--;
//                if (ShootDelay <= 0f)
//                    ChargeTimer = 0f;
//            }

//            // Create orange exo energy at the tip of the bow.
//            Color energyColor = Color.Orange;
//            Vector2 verticalOffset = Vector2.UnitY.RotatedBy(Projectile.rotation) * 8f;
//            if (Math.Cos(Projectile.rotation) < 0f)
//                verticalOffset *= -1f;
             
//            // Create light at the tip of the bow.
//            DelegateMethods.v3_1 = energyColor.ToVector3();
//            Utils.PlotTileLine(tipPosition - verticalOffset, tipPosition + verticalOffset, 10f, DelegateMethods.CastLightOpen);
//            Lighting.AddLight(tipPosition, energyColor.ToVector3());

//            // Create a puff of energy in a star shape and play a sound to indicate that the bow is at max charge.
//            if (ShootDelay <= 0)
//                ChargeTimer++;
//            if (ChargeTimer == GreatbowOfAPilgrim.MaxChargeTime)
//            {
//                SoundEngine.PlaySound(SoundID.Item158 with { Volume = 1.6f }, Projectile.Center);
//                for (int i = 0; i < 75; i++)
//                {
//                    float offsetAngle = MathHelper.TwoPi * i / 75f;

//                    // Parametric equations for an asteroid.
//                    float unitOffsetX = (float)Math.Pow(Math.Cos(offsetAngle), 3D);
//                    float unitOffsetY = (float)Math.Pow(Math.Sin(offsetAngle), 3D);

//                    Vector2 puffDustVelocity = new Vector2(unitOffsetX, unitOffsetY) * 5f;
//                    Dust magic = Dust.NewDustPerfect(tipPosition, DustID.RainbowMk2, puffDustVelocity);
//                    magic.scale = 1.8f;
//                    magic.fadeIn = 0.5f;
//                    magic.color = PADUtils.MulticolorLerp(i / 75f, PADUtils.ExoPalette);
//                    magic.noGravity = true;
//                }
//                ChargeTimer++;
//            }
//        }

//        public void UpdateProjectileHeldVariables(Vector2 armPosition)
//        {
//            if (Main.myPlayer == Projectile.owner)
//            {
//                // 15NOV2024: Ozzatron: clamped mouse position unnecessary, this is only used to aim the Heavenly Gale bow

//                float aimInterpolant = Utils.GetLerpValue(10f, 40f, Projectile.Distance(Main.MouseWorld), true);
//                Vector2 oldVelocity = Projectile.velocity;
//                Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.SafeDirectionTo(Main.MouseWorld), aimInterpolant);
//                if (Projectile.velocity != oldVelocity)
//                    Projectile.ForceNetUpdate();
//            }

//            Projectile.position = armPosition - Projectile.Size * 0.5f + Projectile.velocity.SafeNormalize(Vector2.UnitY) * 44f;
//            Projectile.rotation = Projectile.velocity.ToRotation();
//            Projectile.spriteDirection = Projectile.direction;
//            Projectile.timeLeft = 2;
//        }

//        public void ManipulatePlayerVariables()
//        {
//            Owner.ChangeDir(Projectile.direction);
//            Owner.heldProj = Projectile.whoAmI;

//            // Make the player lower their front arm a bit to indicate the pulling of the string.
//            // This is precisely calculated by representing the top half of the string as a right triangle and using SOH-CAH-TOA to
//            // calculate the respective angle from the appropriate widths and heights.
//            float frontArmRotation = (float)Math.Atan(StringHalfHeight / MathHelper.Max(StringReelbackDistance, 0.001f) * 0.5f);
//            if (Owner.direction == -1)
//                frontArmRotation += MathHelper.PiOver4;
//            else
//                frontArmRotation = MathHelper.PiOver2 - frontArmRotation;
//            frontArmRotation += Projectile.rotation + MathHelper.Pi + Owner.direction * MathHelper.PiOver2 + 0.12f;
//            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
//            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.velocity.ToRotation() - MathHelper.PiOver2);
//        }

//        public override bool PreDraw(ref Color lightColor)
//        {
//            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Type].Value; 
//            Vector2 origin = texture.Size() * 0.5f;
//            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

//            // Draw the string of the bow. It reels back in the initial frames.
//            Vector2 topOfBow = Projectile.Center + Vector2.UnitX.RotatedBy(Projectile.rotation + topStringOffset.ToRotation()) * topStringOffset.Length();
//            Vector2 bottomOfBow = Projectile.Center + Vector2.UnitX.RotatedBy(Projectile.rotation + bottomStringOffset.ToRotation()) * bottomStringOffset.Length();
//            Vector2 endOfString = Projectile.Center - Projectile.rotation.ToRotationVector2() * (StringReelbackDistance + (1f - StringReelbackInterpolant) * 25f);

//            float chargeOffset = ChargeupInterpolant * Projectile.scale * 3f;
//            Color chargeColor = Color.Lerp(Color.Lime, Color.Cyan, (float)Math.Cos(Main.GlobalTimeWrappedHourly * 7.1f) * 0.5f + 0.5f) * ChargeupInterpolant * 0.6f;
//            chargeColor.A = 0;

//            float rotation = Projectile.rotation;
//            SpriteEffects direction = SpriteEffects.None;
//            if (Math.Cos(rotation) < 0f)
//            {
//                direction = SpriteEffects.FlipHorizontally;
//                rotation += MathHelper.Pi;
//            }

//            Color stringColor = new(105, 239, 145);
//            Main.spriteBatch.DrawLineBetter(topOfBow, endOfString, stringColor, 2f);
//            Main.spriteBatch.DrawLineBetter(bottomOfBow, endOfString, stringColor, 2f);

//            // Draw a backglow effect as an indicator of charge.
//            for (int i = 0; i < 8; i++)
//            {
//                Vector2 drawOffset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * chargeOffset;
//                Main.spriteBatch.Draw(texture, drawPosition + drawOffset, null, chargeColor, rotation, origin, Projectile.scale, direction, 0f);
//            }
//            Main.spriteBatch.Draw(texture, drawPosition, null, Projectile.GetAlpha(lightColor), rotation, origin, Projectile.scale, direction, 0f); 
//            return false;
//        }

//        // The bow itself should not do contact damage.
//        public override bool? CanDamage() => false;
//    }
//}
