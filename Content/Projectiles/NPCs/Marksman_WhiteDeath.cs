using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.NPCs
{ 
	public class Marksman_WhiteDeath : ModProjectile 
	{
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // draw it manually (after the player draws)
            Main.spriteBatch.Draw(
                texture,
                drawPos,
                null,
                Color.White,
                Projectile.rotation,
                texture.Size() / 2,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );

            return false; // skip default drawing
        }
        public override void PostDraw(Color lightColor)
        {
            Texture2D glowTex = ModContent.Request<Texture2D>(Texture).Value;

            // Convert Terraria alpha (255 → 0) to opacity (0 → 1)
            float glowOpacity = (255 - Projectile.alpha) / 255f;

            // final glow color using your converted opacity
            Color glowColor = Color.White * glowOpacity;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glowTex.Size() / 2f;

            Main.EntitySpriteDraw(
                glowTex,
                drawPos,
                null,
                glowColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
        }

        public override void SetDefaults()
		{
			base.Projectile.width = (base.Projectile.height = 38); 
			base.Projectile.timeLeft = 250; 
            Projectile.aiStyle = -1;
			base.Projectile.hostile = true;
            Projectile.tileCollide = false;
			base.Projectile.penetrate = -1; 
            Projectile.rotation = 5f;
            Projectile.scale = 5f;
		}
		 
		public override void AI()
		{
            Player player = Main.player[(int)Player.FindClosest(base.Projectile.Center, 1, 1)];
			Projectile.alpha -= 6; 
            if (Projectile.alpha <= 0 ) {Projectile.alpha = 0;}

            Vector2 offset = new Vector2(Projectile.ai[0], Projectile.ai[1]);
			if(Projectile.scale <= 1f)
            {
                Projectile.scale = 1f;
            }
            else
            {

                Projectile.scale *= 0.95f;
            }
                Projectile.Center = player.Center + offset;
            Projectile.rotation *= 0.95f;
            Projectile.velocity = Vector2.Zero;
			 
            if (!player.active || player.dead)
                Projectile.Kill();
        }
        public override void OnKill(int timeLeft)
        {
            Player player = Main.player[(int)Player.FindClosest(base.Projectile.Center, 1, 1)];
            if (player.statLife > player.statLifeMax2 * 0.4f)
            {
                player.statLife -= (int)(player.statLifeMax2 * 0.33333f)-1; 
                player.lifeRegenTime = 0;
                player.lifeRegen = 0;
                player.Hurt(PlayerDeathReason.ByCustomReason("was cut"), 1, Main.rand.NextFromList(-1, 1)); 
            }
            else
            {
                player.statLife -= (int)(player.statLife -2); 
                player.lifeRegenTime = 0;
                player.lifeRegen = 0;
                player.Hurt(PlayerDeathReason.ByCustomReason("was cut"), 1, Main.rand.NextFromList(-1,1));
            } 
            Vector2 baseDirection = -Projectile.velocity.SafeNormalize(Vector2.UnitY);

            int dustAmount = Main.rand.Next(35, 45);
            Vector2 pos =Projectile.Center;

            for (int i = 0; i < dustAmount; i++)
            {
                float angleOffset = MathHelper.ToRadians(Main.rand.NextFloat(-20f, 20f));
                Vector2 sprayDirection = baseDirection.RotatedBy(angleOffset);

                float distanceFromCenter = Math.Abs(angleOffset) / MathHelper.ToRadians(20f);
                float speed = MathHelper.Lerp(6f, 1.2f, distanceFromCenter);

                Vector2 dustVelocity = sprayDirection * speed;
                int d = Dust.NewDust(pos, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 0, default, 1.1f);
                Main.dust[d].velocity = dustVelocity;
                Main.dust[d].noGravity = false;
                
            }
            player.PIS().AddScreenShake(3.2f, 1);
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion, Projectile.Center);
        }

    }
}
