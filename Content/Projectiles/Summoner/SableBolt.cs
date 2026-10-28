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

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// A bolt of packed sand from Jelle's ballista (SableConstruct), by request: 60 tiles a second, as far as 60
    /// tiles. Through three things (mine: a ballista's bolt goes through), each once; it bursts to sand on a wall or at
    /// the end of its way. ai[0] is the ballista's identity, so the ballista kicks on every machine as it leaves.
    /// The picture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class SableBolt : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 3;
            Projectile.extraUpdates = 1;

            // Sixty tiles at sixteen pixels a tick: sixty ticks, two updates each.
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;

                foreach (Projectile p in Main.ActiveProjectiles)
                {
                    if (p.owner == Projectile.owner && p.identity == (int)Projectile.ai[0] && p.ModProjectile is SableConstruct ballista)
                    {
                        ballista.Loosed(Projectile.Center, Projectile.velocity);
                        break;
                    }
                }
            }

            if (!Main.dedServ && Main.rand.NextBool(2))
            {
                PADParticleHandler.SpawnParticle(new SandGrain(Projectile.Center - Projectile.velocity * Main.rand.NextFloat(), -Projectile.velocity * 0.12f
                    + Main.rand.NextVector2Circular(0.7f, 0.7f), Harenaeum.AnySand, Main.rand.Next(12, 22), Main.rand.NextFloat(0.9f, 1.5f)));
            }

            Lighting.AddLight(Projectile.Center, Harenaeum.PaleGold.ToVector3() * 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            Harenaeum.Burst(Projectile.Center, Projectile.velocity, 10, 4f, 0.8f, 0.6f);
            PADParticleHandler.SpawnParticle(new SparkShard(Projectile.Center, Projectile.velocity * 0.4f, Harenaeum.PaleGold, 8f, 2.5f, 22f, 8));
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            Harenaeum.Burst(Projectile.Center, -Projectile.velocity, 14, 3.5f, 1.2f, 1.2f);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // A wake: where it has just been, the sand drawn out behind it.
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float f = 1f - i / (float)Projectile.oldPos.Length;
                Color c = Harenaeum.Sand[2] * f * 0.45f;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null, c, Projectile.rotation,
                    origin, new Vector2(1f, 0.7f + 0.3f * f), SpriteEffects.None, 0);
            }

            Color lit = Color.Lerp(Lighting.GetColor(Projectile.Center.ToTileCoordinates()), Color.White, 0.35f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lit, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);

            return false;
        }
    }
}
