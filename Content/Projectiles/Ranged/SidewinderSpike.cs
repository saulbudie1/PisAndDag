using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The spike a Sidewinder arrow comes up as (SidewinderArrow): a stake of packed sand out of the ground, up to the
    /// one it came for in a twelfth of a second, held there, and sunk back. What it goes up through it hurts once and
    /// throws up off its feet, by how far that can be thrown (nothing that will not be pushed; no boss). Its center
    /// is the ground it comes out of; ai[0] how high it goes.
    ///
    /// The picture is a placeholder (PlaceholderTextures.md): the spike standing, point up; only as much of it is
    /// drawn as is out of the ground.
    /// </summary>
    public class SidewinderSpike : ModProjectile
    {
        public const int Rise = 5, Hold = 10, Sink = 12;

        /// <summary>How hard it throws a thing up, the lightest. Mine.</summary>
        public const float Toss = 8f;

        private float Height => Math.Max(24f, Projectile.ai[0]);

        private ref float Time => ref Projectile.ai[1];

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Rise + Hold + Sink;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>How much of it is out of the ground, 0 to 1.</summary>
        private float Out
        {
            get
            {
                float t = Time;

                if (t < Rise)
                {
                    float k = t / Rise;
                    return 1f - (1f - k) * (1f - k);
                }

                if (t < Rise + Hold)
                    return 1f;

                return MathHelper.Clamp(1f - (t - Rise - Hold) / Sink, 0f, 1f);
            }
        }

        public override void AI()
        {
            if (Time == 0f && !Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = 0.35f }, Projectile.Center);
                Harenaeum.Burst(Projectile.Center - new Vector2(0f, 2f), -Vector2.UnitY, 22, 5.5f, 0.8f, 1.6f);
                Harenaeum.Glint(Projectile.Center - new Vector2(0f, Height * 0.8f), 0.9f);

                for (int i = 0; i < 6; i++)
                    PADParticleHandler.SpawnParticle(new StoneChip(Projectile.Center, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 6f)), Harenaeum.Sand[1], Main.rand.Next(24, 40)));
            }

            Time++;

            if (!Main.dedServ && Time > Rise + Hold && Main.rand.NextBool(2))
            {
                Vector2 at = Projectile.Center - new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(Height * Out));
                PADParticleHandler.SpawnParticle(new SandGrain(at, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), 0.5f), Harenaeum.AnySand, Main.rand.Next(14, 24), Main.rand.NextFloat(1f, 1.6f)));
            }
        }

        public override bool? CanDamage() => Time <= Rise + 2 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float top = Height * Out;
            Rectangle spike = new Rectangle((int)Projectile.Center.X - 9, (int)(Projectile.Center.Y - top), 18, (int)Math.Max(4f, top));
            return spike.Intersects(targetHitbox);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target.knockBackResist > 0f && !target.boss && !target.noGravity)
            {
                float size = MathHelper.Clamp((float)Math.Sqrt(900f / Math.Max(1f, target.width * target.height)), 0.45f, 1.15f);
                float weight = MathHelper.Clamp(target.knockBackResist, 0f, 1f) * size;
                Harenaeum.Launch(target, new Vector2(target.velocity.X * 0.5f, -Toss * (0.35f + 0.65f * weight)));
            }

            if (!Main.dedServ)
                Harenaeum.Burst(Vector2.Clamp(Projectile.Center - new Vector2(0f, Height * Out), target.Hitbox.TopLeft(), target.Hitbox.BottomRight()),
                    -Vector2.UnitY, 10, 4f, 0.9f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float shown = Height * Out;

            if (shown < 1f)
                return false;

            // The picture's top part, as much as is out of the ground, stood on the ground; stretched to its height.
            float stretch = Height / texture.Height;
            int rows = Math.Clamp((int)(shown / stretch), 1, texture.Height);
            Rectangle part = new Rectangle(0, 0, texture.Width, rows);
            Color lit = Color.Lerp(Lighting.GetColor((Projectile.Center - new Vector2(0f, shown * 0.5f)).ToTileCoordinates()), Harenaeum.PaleGold, 0.15f);

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, part, lit, 0f, new Vector2(texture.Width * 0.5f, rows),
                new Vector2(1f, stretch), SpriteEffects.None, 0);

            return false;
        }
    }
}
