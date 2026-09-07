using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.NPCs
{
    /// <summary>
    /// The Megashark's aimed shot: fast, straight, and meant to be dodged by moving
    /// rather than by reacting. Fired on the long intervals.
    /// </summary>
    public class MegasharkBullet : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            // Hostile, so it hurts the player and ignores NPCs.
            Projectile.hostile = true;
            Projectile.friendly = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;

            Projectile.aiStyle = -1;

            // The texture is a streak, so a trail of copies would smear. One long frame
            // plus the stretch below reads cleaner than an afterimage stack.
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Only a faint drag: this shot is supposed to arrive.
            Projectile.velocity *= 0.999f;

            Lighting.AddLight(Projectile.Center, 0.7f, 0.55f, 0.25f);

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.05f, 0, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
