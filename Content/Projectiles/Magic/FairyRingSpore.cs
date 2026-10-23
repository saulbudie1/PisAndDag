using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// A spore puffed off a cap that came up where something died (FairyRingFruit): a soft ball of
    /// blue light that drifts at the enemy it was puffed at, turning after the nearest one it can
    /// find, and bursts on whatever it touches. Glow only: drawn in added light.
    /// </summary>
    public class FairyRingSpore : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        /// <summary>The white-hot middle, made by formula (Content/Graphics/GlowHot.png).</summary>
        private static ReLogic.Content.Asset<Texture2D> hot;

        public override void Load()
        {
            if (!Main.dedServ)
                hot = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowHot");
        }

        public override void Unload()
        {
            hot = null;
        }

        private const float Speed = 5.5f;
        private const float Turn = 0.07f;
        private const float Seek = 22f * 16f;

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 150;
        }

        public override void AI()
        {
            NPC target = null;
            float best = Seek;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float d = Vector2.Distance(Projectile.Center, npc.Center);

                if (d < best)
                {
                    best = d;
                    target = npc;
                }
            }

            float speed = Math.Min(Speed, Projectile.velocity.Length() + 0.08f);

            if (target != null)
            {
                float want = (target.Center - Projectile.Center).ToRotation();
                float have = Projectile.velocity.ToRotation();
                float turned = have + MathHelper.Clamp(MathHelper.WrapAngle(want - have), -Turn, Turn);
                Projectile.velocity = turned.ToRotationVector2() * speed;
            }
            else
            {
                Projectile.velocity *= 0.99f;
            }

            // It wavers as it drifts.
            Projectile.position += new Vector2(0f, (float)Math.Sin(Projectile.timeLeft * 0.25f) * 0.35f);

            Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.8f);

            if (!Main.dedServ && Projectile.timeLeft % 2 == 0)
            {
                PADParticleHandler.SpawnParticle(new SporeMote(Projectile.Center + Main.rand.NextVector2Circular(3f, 3f),
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.3f, 0.3f), FairyRing.Bright * 0.7f,
                    Main.rand.NextFloat(0.45f, 0.7f), Main.rand.Next(20, 35)));
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.6f, Volume = 0.4f }, Projectile.Center);

            for (int k = 0; k < 9; k++)
            {
                PADParticleHandler.SpawnParticle(new SporeMote(Projectile.Center, Main.rand.NextVector2Circular(2.2f, 2.2f),
                    Main.rand.NextBool(2) ? FairyRing.Pale : FairyRing.Bright, Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(25, 45)));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 at = Projectile.Center - Main.screenPosition;
            float flicker = 0.85f + 0.15f * (float)Math.Sin(Projectile.timeLeft * 0.6f);

            Color outer = FairyRing.Mid * (0.5f * flicker);
            outer.A = 0;
            Color middle = FairyRing.Bright * (0.45f * flicker);
            middle.A = 0;
            Color inner = FairyRing.Pale * (0.95f * flicker);
            inner.A = 0;

            // A soft ball of blue light: a wide glow, a brighter one in it, a pale point.
            Main.EntitySpriteDraw(texture, at, null, outer, 0f, texture.Size() * 0.5f, 58f / texture.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, at, null, middle, 0f, texture.Size() * 0.5f, 24f / texture.Width, SpriteEffects.None, 0);

            if (hot != null)
                Main.EntitySpriteDraw(hot.Value, at, null, inner, 0f, hot.Value.Size() * 0.5f, 16f / hot.Value.Width, SpriteEffects.None, 0);

            return false;
        }
    }
}
