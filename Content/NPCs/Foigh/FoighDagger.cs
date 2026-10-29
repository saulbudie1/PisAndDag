using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// One of Foigh's throwing knives. Fast and straight, however far off they are, and
    /// broken on the first tile it meets.
    ///
    /// A hundred through armour, the same in every world, varied as vanilla varies any hit,
    /// once, against each machine's own player, checked as anything that hurts by touch is.
    /// It can be parried (Parry.AddProjectile): met by a parry it is gone.
    ///
    /// The picture points up; the projectile is its middle.
    /// </summary>
    public class FoighDagger : ModProjectile
    {
        /// <summary>The part that hurts: this far back along it and this far ahead of its middle, this thick; and the knock.</summary>
        private const float Back = 12f;
        private const float Ahead = 9f;
        private const float Width = 9f;
        private const float Knockback = 4f;

        private static readonly Color Steel = new Color(214, 226, 240);

        // ai[0] his whoAmI
        // localAI[0] 1 once it has hit this machine's player

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
            Projectile.timeLeft = 200;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, 0.16f, 0.17f, 0.19f);

            Strike();

            // Or the Everclear Mirror's copy, as a second player: one knife, in whichever of them it
            // reaches first (in them, it is gone; turned aside, it is gone).
            if (Projectile.active)
                MimicSystem.AlsoOnCopy(Strike);
        }

        /// <summary>Against this machine's own player (or the copy, under MimicSystem.AlsoOnCopy): a hundred through armour, once.</summary>
        private void Strike()
        {
            if (Main.dedServ || Projectile.localAI[0] != 0f)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            Vector2 along = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 from = Projectile.Center - along * Back;
            float where = 0f;

            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, from, Projectile.Center + along * Ahead, Width, ref where))
                return;

            // On them: the parry first, if it came from their front. At their arc, before it
            // gets this far, the parry finds it for itself (ParryPlayer.PostUpdate).
            if (me.GetModPlayer<ParryPlayer>().Takes(Projectile, from))
                return;

            if (me.immune)
                return;

            int damage = Main.DamageVar(Foigh.DaggerDamage, -me.luck);
            int direction = Projectile.velocity.X >= 0f ? 1 : -1;

            if (!Foigh.Pierce(me, "FoighDagger", damage, direction, Knockback))
                return;

            Projectile.localAI[0] = 1f;

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.3f, Volume = 0.8f }, me.Center);
            me.PIS().BumpScreenShake(3f, 8);

            // It is in them: on this machine it is gone, and the server's goes when it lands or runs out.
            Projectile.alpha = 255;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!Main.dedServ && Projectile.alpha < 255)
            {
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);
                SparkShard.Spray(Projectile.Center, Steel, 4, 3.5f, 14f, 12, falls: true);
            }

            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.alpha >= 255)
                return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 behind = -Projectile.velocity.SafeNormalize(Vector2.UnitY);

            // Where it has just been, as light.
            for (int i = 3; i >= 1; i--)
            {
                Color pale = new Color(Steel.R, Steel.G, Steel.B, 0) * (0.16f * (4 - i) / 3f);

                Main.EntitySpriteDraw(texture, at + behind * (i * 7f), null, pale, Projectile.rotation, origin, 1f, SpriteEffects.None);
            }

            Main.EntitySpriteDraw(texture, at, null, lightColor, Projectile.rotation, origin, 1f, SpriteEffects.None);
            return false;
        }
    }
}
