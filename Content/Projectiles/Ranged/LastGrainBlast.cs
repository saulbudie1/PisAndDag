using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The Last Grain's compressed sand, asked: "an effect from the muzzle, can also damage near surrounding". With
    /// each shot while its glass runs, a blast of packed sand out of the muzzle, a cone 3.5 tiles long and 80 degrees
    /// wide, hurting what is in it once and pushing it back. Its velocity is only the way it goes; it stays where it
    /// was shot from, a twelfth of a second. It is also the shot's sound and flash, so that everyone near hears it; with
    /// the glass empty (ai[0] 1) it is only that: a dry crack and a wisp, hurting nothing.
    /// </summary>
    public class LastGrainBlast : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        /// <summary>Mine: 3.5 tiles, forty degrees each side.</summary>
        public const float Reach = 56f, Half = 0.7f;

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 5;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        private bool Dry => Projectile.ai[0] == 1f;

        public override void AI()
        {
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);

                if (Dry)
                {
                    SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.45f, Pitch = -0.35f }, Projectile.Center);

                    if (!Main.dedServ)
                    {
                        Smoke(aim, 1);
                        Harenaeum.Burst(Projectile.Center, aim, 3, 1.5f, 0.6f, 0.3f);
                    }

                    return;
                }

                SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.8f, Pitch = 0.05f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.35f, Pitch = 0.8f, PitchVariance = 0.1f }, Projectile.Center);

                if (!Main.dedServ)
                {
                    MuzzleFlash.Spawn(Projectile.Center, aim);
                    Smoke(aim, 2);
                    Burst();
                }
            }
        }

        public override bool? CanDamage() => Dry ? false : null;

        private void Smoke(Vector2 aim, int puffs)
        {
            for (int i = 0; i < puffs; i++)
                PADParticleHandler.SpawnParticle(new MuzzleSmoke(Projectile.Center, aim.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.6f, 1.6f), Main.rand.Next(24, 36), Main.rand.NextFloat(0.4f, 0.7f)));
        }

        private void Burst()
        {
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < 26; i++)
            {
                Vector2 velocity = aim.RotatedByRandom(Half) * Main.rand.NextFloat(3f, 9f);
                PADParticleHandler.SpawnParticle(new SandGrain(Projectile.Center, velocity, Harenaeum.AnySand, Main.rand.Next(12, 22), Main.rand.NextFloat(1f, 2f)));
            }

            for (int i = 0; i < 3; i++)
            {
                PADParticleHandler.SpawnParticle(new DustFog(Projectile.Center + aim * Main.rand.NextFloat(6f, 30f), aim.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.6f, 1.6f),
                    Harenaeum.Sand[2], Main.rand.Next(24, 40), Main.rand.NextFloat(0.4f, 0.7f), 0.35f));
            }

            Harenaeum.Glint(Projectile.Center + aim * 6f, 0.7f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 from = Projectile.Center;
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 nearest = Vector2.Clamp(from, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            Vector2 to = nearest - from;
            float distance = to.Length();

            if (distance > Reach)
                return false;

            // Right on it, or in the cone, by the nearest point or the middle.
            if (distance < 10f)
                return true;

            Vector2 middle = targetHitbox.Center.ToVector2() - from;
            bool inCone = Math.Abs(MathHelper.WrapAngle(to.ToRotation() - aim.ToRotation())) < Half
                || Math.Abs(MathHelper.WrapAngle(middle.ToRotation() - aim.ToRotation())) < Half;

            return inCone && Collision.CanHitLine(from, 0, 0, nearest, 0, 0);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
