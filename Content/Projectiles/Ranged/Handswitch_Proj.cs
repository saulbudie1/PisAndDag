using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    public class Handswitch_Proj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 3;    
            Projectile.height = 3;  
            Projectile.aiStyle = 1;  
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.extraUpdates = 3;
            Projectile.timeLeft = 300; 
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            AIType = ProjectileID.Bullet;  
        }
        public override void AI()
        {
             
            Lighting.AddLight(Projectile.Center, 0.3f, 0.3f, 0.3f);
             
            if (Main.rand.NextBool(8))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, 1, 1, DustID.Smoke);
                d.scale = 0.7f;
                d.velocity = -Projectile.velocity * 0.1f;
                d.noGravity = true;
            }
        }
         
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
             
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White, // ignore lighting
                Projectile.rotation,
                texture.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            // Draw trail (faint stretched streak)
            Vector2 trailDirection = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            Vector2 start = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 6; i++)
            {
                float t = i / 6f;
                float alpha = (1f - t) * 0.4f;
                Vector2 pos = start + trailDirection * (i * 4);
                Main.EntitySpriteDraw(
                    texture,
                    pos,
                    null,
                    Color.White * alpha,
                    Projectile.rotation,
                    texture.Size() / 2f,
                    Projectile.scale * (1f - t * 0.7f),
                    SpriteEffects.None,
                    0
                );
            }

            return false;  
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        { 
            Vector2 baseDirection = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
             
            int dustAmount = Main.rand.Next(13,35);
            Vector2 pos = new Vector2(target.position.X + Main.rand.NextFloat(0f, target.width), target.position.Y + Main.rand.NextFloat(0f, target.height));

            for (int i = 0; i < dustAmount; i++)
            { 
                float angleOffset = MathHelper.ToRadians(Main.rand.NextFloat(-20f, 20f));
                Vector2 sprayDirection = baseDirection.RotatedBy(angleOffset);
                 
                float distanceFromCenter = Math.Abs(angleOffset) / MathHelper.ToRadians(20f);  
                float speed = MathHelper.Lerp(6f, 1.2f, distanceFromCenter);

                Vector2 dustVelocity = sprayDirection * speed; 
                int d = Dust.NewDust(pos, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 0, default, 1f);
                Main.dust[d].velocity = dustVelocity;
                Main.dust[d].noGravity = false;  
            }
        }
    }
}
