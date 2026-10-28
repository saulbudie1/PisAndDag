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

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// A needle of sand flung by Pocket Sandstorm's whirl (PocketSandstormYoyo), by request ("damages them slowly
    /// with piercing particles"): a fast pale streak, straight through what it is flung at and on, three things at
    /// most, each once, gone in an eighth of a second (some 11 tiles). It goes past ten of a thing's defence (mine: so small a
    /// blow is not nothing against a skeleton). Through walls, as sand blown through a crack is.
    /// </summary>
    public class PocketSandstormNeedle : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 15;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ArmorPenetration = 10;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;

                if (!Main.dedServ && Main.rand.NextBool(3))
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.18f, Pitch = 0.7f, PitchVariance = 0.2f, MaxInstances = 2 }, Projectile.Center);
            }

            if (!Main.dedServ && Main.rand.NextBool(3))
            {
                PADParticleHandler.SpawnParticle(new SandGrain(Projectile.Center, Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Harenaeum.AnySand, Main.rand.Next(10, 18), Main.rand.NextFloat(0.8f, 1.3f)));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 5; i++)
            {
                PADParticleHandler.SpawnParticle(new SandGrain(Projectile.Center, Projectile.velocity.RotatedByRandom(0.7f) * Main.rand.NextFloat(0.2f, 0.45f),
                    Harenaeum.AnySand, Main.rand.Next(12, 22), Main.rand.NextFloat(0.9f, 1.5f)));
            }

            PADParticleHandler.SpawnParticle(new SparkShard(Projectile.Center, Projectile.velocity * 0.3f, Harenaeum.PaleGold, 6f, 2f, 14f, 8));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f);

            // The tail: where it was, thinning and going dark the further back.
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i - 1] == Vector2.Zero)
                    continue;

                Vector2 a = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                Vector2 b = Projectile.oldPos[i - 1] + Projectile.Size * 0.5f;
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Color c = Color.Lerp(Harenaeum.Sand[1], Harenaeum.PaleGold, k) * k * 0.8f * fade;
                DrawLine(pixel, a, b, c, 1f + k);
            }

            // The head, bright.
            Vector2 head = Projectile.Center;
            Color tip = Harenaeum.Shine * fade;
            tip.A = 0;
            DrawLine(pixel, head - Projectile.velocity * 0.6f, head + Projectile.velocity * 0.25f, tip, 2f);

            return false;
        }

        private static void DrawLine(Texture2D pixel, Vector2 a, Vector2 b, Color colour, float thickness)
        {
            Vector2 d = b - a;
            float length = d.Length();

            if (length < 0.5f)
                return;

            Main.EntitySpriteDraw(pixel, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), colour, d.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(length, thickness), SpriteEffects.None, 0);
        }
    }
}
