using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// A shot of a player's that Foigh struck out of the air, on its way back to whoever sent
    /// it: the same thing to look at (it is drawn with the picture of what was shot), going
    /// straight, and broken on the first tile it meets.
    ///
    /// A hundred through armour, the knife's, varied as vanilla varies any hit, once, against
    /// each machine's own player, checked as anything that hurts by touch is. It can be
    /// parried in its turn (Parry.AddProjectile): met by a parry it is gone.
    /// </summary>
    public class FoighReturned : ModProjectile
    {
        /// <summary>No picture of its own: it borrows the picture of what it was.</summary>
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        /// <summary>The part that hurts: this far round its middle; and the knock.</summary>
        private const int Reach = 7;
        private const float Knockback = 4f;

        private static readonly Color Steel = new Color(214, 226, 240);

        // ai[0] the type of what it was · ai[1] his whoAmI
        // localAI[0] 1 once it has hit this machine's player

        /// <summary>What it was, if that is still a thing the game has.</summary>
        private int Was
        {
            get
            {
                int type = (int)Projectile.ai[0];
                return type > ProjectileID.None && type < ProjectileLoader.ProjectileCount ? type : ProjectileID.WoodenArrowFriendly;
            }
        }

        public override void SetStaticDefaults()
        {
            // In the air, and not already through this machine's player.
            Parry.AddProjectile(Type, projectile => projectile.localAI[0] == 0f);
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI()
        {
            // Most shots are drawn pointing up.
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, 0.2f, 0.14f, 0.12f);

            Strike();
        }

        /// <summary>Against this machine's own player: a hundred through armour, once.</summary>
        private void Strike()
        {
            if (Main.dedServ || Projectile.localAI[0] != 0f)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            Rectangle body = Projectile.Hitbox;

            body.Inflate(Reach, Reach);

            if (!body.Intersects(me.Hitbox))
                return;

            // On them: the parry first, if it came from their front.
            if (me.GetModPlayer<ParryPlayer>().Takes(Projectile, Projectile.Center - Projectile.velocity))
                return;

            if (me.immune)
                return;

            int damage = Main.DamageVar(Foigh.DaggerDamage, -me.luck);
            int direction = Projectile.velocity.X >= 0f ? 1 : -1;

            if (!Foigh.Pierce(me, "FoighReturned", damage, direction, Knockback))
                return;

            Projectile.localAI[0] = 1f;
            Projectile.alpha = 255;

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 0.8f }, me.Center);
            me.PIS().BumpScreenShake(3f, 8);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!Main.dedServ && Projectile.alpha < 255)
            {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
                SparkShard.Spray(Projectile.Center, Steel, 4, 3.5f, 14f, 12, falls: true);
            }

            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.alpha >= 255)
                return false;

            int type = Was;

            Main.instance.LoadProjectile(type);

            Texture2D texture = TextureAssets.Projectile[type].Value;
            int frames = Math.Max(1, Main.projFrames[type]);
            Rectangle frame = new Rectangle(0, 0, texture.Width, texture.Height / frames);
            Vector2 origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            Vector2 at = Projectile.Center - Main.screenPosition;

            // His now: a little of the red of his eye on it.
            Color tint = Color.Lerp(lightColor, new Color(255, 150, 150), 0.35f);

            Main.EntitySpriteDraw(texture, at, frame, tint, Projectile.rotation, origin, 1f, SpriteEffects.None);
            return false;
        }
    }
}
