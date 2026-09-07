using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.NPCs
{
    /// <summary>
    /// The barrage round. Slower and individually weaker than the aimed shot — the threat
    /// is the volume and the spread, not any one ball, so these are meant to be outrun
    /// rather than dodged one at a time.
    /// </summary>
    public class MegasharkMusket : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            Projectile.hostile = true;
            Projectile.friendly = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 420;

            Projectile.aiStyle = -1;
        }

        public override void AI()
        {
            Projectile.rotation += 0.3f * Projectile.direction;

            // A slight droop over the ball's life. Enough that a wide barrage fans out
            // and sags rather than staying a rigid cone.
            if (Projectile.timeLeft < 380)
                Projectile.velocity.Y += 0.045f;

            Lighting.AddLight(Projectile.Center, 0.35f, 0.22f, 0.08f);

            if (Main.rand.NextBool(6))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    -Projectile.velocity * 0.08f, 140, default, 0.7f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 3; i++)
            {
                Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    Main.rand.NextVector2Circular(1.6f, 1.6f), 120, default, 0.8f);
            }
        }
    }
}
