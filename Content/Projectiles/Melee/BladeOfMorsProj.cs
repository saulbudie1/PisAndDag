using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Blade of Mors' released charge. Pierces everything, ignores terrain, and hangs in
    /// the air for a quarter second every time it bites before carrying on.
    /// </summary>
    public class BladeOfMorsProj : ModProjectile
    { 
        private const float SpriteAngleOffset = MathHelper.PiOver4;
         
        private const int FreezeTicks = 15;
         
        private const float TailOffset = 18f;
         
        private static readonly Color BloodColor = new Color(117, 9, 24);
         
        public ref float FreezeTimer => ref Projectile.ai[0];
         
        private Vector2 storedVelocity;

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;

            Projectile.penetrate = -1;       // pierces everything
            Projectile.tileCollide = false;  // and passes through walls
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360;       // 10 seconds, then it bursts

            // A projectile that pierces forever would otherwise hit the same enemy on
            // every single tick. Local immunity gives each target its own cooldown.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI()
        {
            if (FreezeTimer > 0f)
            {
                FreezeTimer--;
                Projectile.velocity = Vector2.Zero;

                // Timer just ran out — pick the flight back up exactly where it left off.
                if (FreezeTimer <= 0f)
                    Projectile.velocity = storedVelocity;
            }

            // While stopped the real velocity is zero, so face the direction it was
            // travelling instead of snapping to a rotation of nothing.
            Vector2 facing = Projectile.velocity.LengthSquared() > 0.01f
                ? Projectile.velocity
                : storedVelocity;

            if (facing.LengthSquared() > 0.01f)
                Projectile.rotation = facing.ToRotation() + SpriteAngleOffset;

            if (!Main.dedServ)
                SpawnTailTrail(facing);
        }

        /// <summary>
        /// Bleeds droplets from the blade's trailing end rather than its centre, so the
        /// trail is left behind it instead of wrapped around it.
        /// </summary>
        private void SpawnTailTrail(Vector2 facing)
        {
            if (facing.LengthSquared() < 0.01f)
                return;

            Vector2 direction = facing.SafeNormalize(Vector2.UnitX);
            Vector2 tail = Projectile.Center - direction * TailOffset;

            PADParticleHandler.SpawnParticle(new BloodDroplet(
                tail + Main.rand.NextVector2Circular(3f, 3f),

                // Drifts backwards out of the blade, with a little scatter.
                -direction * Main.rand.NextFloat(0.4f, 1.4f)
                    + Main.rand.NextVector2Circular(0.6f, 0.6f),

                Main.rand.Next(30, 55),

                // Same range the hold aura uses at full charge.
                Main.rand.NextFloat(0.4f, 0.85f) * 1.1f,

                BloodColor,

                // Weightless, like the aura. Only the death burst falls.
                affectedByGravity: false));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Capture the travel velocity only on the first freeze. A later hit while
            // already stopped would otherwise stash Vector2.Zero and strand the blade.
            if (FreezeTimer <= 0f)
                storedVelocity = Projectile.velocity;

            // Assignment, not addition — repeated hits refresh the quarter second rather
            // than stacking it up.
            FreezeTimer = FreezeTicks;
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion, Projectile.Center);

            int count = Main.rand.Next(35, 55);
            for (int k = 0; k < count; k++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(7f, 7f),
                    Main.rand.Next(40, 75),
                    Main.rand.NextFloat(0.5f, 1f),
                    BloodColor,

                    // The burst falls, unlike the trail.
                    affectedByGravity: true));
            }
             
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            // Color.White instead of lightColor: the blade keeps its own colours in a
            // pitch black cave, but it emits nothing — there is no Lighting.AddLight
            // anywhere in this file, so it does not actually glow.
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                Color.White, Projectile.rotation, texture.Size() * 0.5f,
                Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
