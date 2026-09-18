using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{ 
    public class PilgrimArrow : ModProjectile
    {
        // --------------------------------------------------------------- the tiers

        public readonly struct Tier
        {
            public readonly int Index;
            public readonly float Progress;     // 0..1 inside this tier
            public readonly float DamageScale;
            public readonly float SpeedScale;
            public readonly int Hits;           // bites per lodge
            public readonly int StaggerTicks;   // and therefore the lodge length
            public readonly int PushTiles;
            public readonly float Gravity;      // pixels per tick per tick
            public readonly int Life;

            public Tier(int index, float progress, float dmg, float speed, int hits,
                int stagger, int push, float gravity, int life)
            {
                Index = index; Progress = progress;
                DamageScale = dmg; SpeedScale = speed;
                Hits = hits; StaggerTicks = stagger; PushTiles = push;
                Gravity = gravity; Life = life;
            }

            public bool Straight => Index >= 3;

            /// <summary>Ticks between bites, so the lodge lands exactly Hits times.</summary>
            public int HitCooldown => StaggerTicks <= 0
                ? 20
                : Math.Max(6, StaggerTicks / Math.Max(Hits, 1));
        }

        /// <summary>
        /// Ticks held to what the shot becomes. The 5.5-to-6 second dead band falls out of
        /// this on its own: progress inside tier 2 is clamped at 1 from 5.5 seconds, so
        /// the last half second climbs nothing and then the sixth second snaps to 4x.
        /// </summary>
        public static Tier TierOf(float ticks)
        {
            if (ticks < 60f)
                return new Tier(0, 0f, 1.00f, 0.55f, 1, 0, 0, 0.250f, 240);

            if (ticks < 180f)
            {
                float p = (ticks - 60f) / 120f;
                return new Tier(1, p, MathHelper.Lerp(1.0f, 1.5f, p), 1.00f,
                    5, 60, 0, 0.100f, 600);
            }

            if (ticks < 360f)
            {
                float p = MathHelper.Clamp((ticks - 180f) / 150f, 0f, 1f);
                return new Tier(2, p, MathHelper.Lerp(1.5f, 3.0f, p), 1.25f,
                    7, (int)MathHelper.Lerp(120f, 240f, p), 2, 0.045f, 900);
            }

            return new Tier(3, 1f, 4.00f, 2.20f, 10, 300, 10, 0f, 1800);
        }

        private Tier tier;

        // ------------------------------------------------------------- the lodging

        /// <summary>Ticks left stuck in something. In ai[2] so it survives a sync.</summary>
        public ref float LodgeTimer => ref Projectile.ai[2];

        private Vector2 storedVelocity;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;

            Projectile.penetrate = -1;          // it goes through
            Projectile.tileCollide = true;      // until a full draw says otherwise

            // Piercing forever would hit the same target every tick. Local immunity gives
            // each one its own clock, and that clock is what makes "hits per lodge" a real
            // number rather than a wish.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            tier = TierOf(TicksFromAi());

            Projectile.localNPCHitCooldown = tier.HitCooldown;
            Projectile.timeLeft = tier.Life;
            Projectile.tileCollide = !tier.Straight;
        }

        /// <summary>
        /// ai[0] carries the tier index and ai[1] the progress inside it, both set by the
        /// holdout at release. Rebuilding the tier from those two survives a net sync,
        /// which a cached struct on the instance would not.
        /// </summary>
        private float TicksFromAi()
        {
            int index = (int)Projectile.ai[0];
            float p = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);

            return index switch
            {
                0 => 30f,
                1 => 60f + 120f * p,
                2 => 180f + 150f * p,
                _ => 360f,
            };
        }

        public override void AI()
        {
            // OnSpawn does not run on clients that receive the projectile over the wire.
            if (tier.Hits == 0)
            {
                tier = TierOf(TicksFromAi());
                Projectile.localNPCHitCooldown = tier.HitCooldown;
                Projectile.tileCollide = !tier.Straight;
            }

            if (LodgeTimer > 0f)
            {
                LodgeTimer--;
                Projectile.velocity = Vector2.Zero;

                if (LodgeTimer <= 0f)
                    Projectile.velocity = storedVelocity;
            }
            else if (tier.Gravity > 0f)
            {
                Projectile.velocity.Y += tier.Gravity;
                if (Projectile.velocity.Y > 16f)
                    Projectile.velocity.Y = 16f;
            }

            Vector2 facing = Projectile.velocity.LengthSquared() > 0.01f
                ? Projectile.velocity
                : storedVelocity;

            if (facing.LengthSquared() > 0.01f)
                Projectile.rotation = facing.ToRotation() + MathHelper.PiOver2;

            if (tier.Straight && !Main.dedServ)
                Lighting.AddLight(Projectile.Center, 0.45f, 0.62f, 0.75f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (tier.StaggerTicks <= 0)
                return;

            // Only stash the flight on the first bite. A later hit while already stopped
            // would stash Vector2.Zero and strand the arrow in the air forever.
            if (LodgeTimer <= 0f)
                storedVelocity = Projectile.velocity;

            LodgeTimer = tier.StaggerTicks;

            PADStaggerNPC stagger = target.GetGlobalNPC<PADStaggerNPC>();
            stagger.Stagger(target, tier.StaggerTicks);

            if (tier.PushTiles > 0)
            {
                Vector2 dir = storedVelocity.SafeNormalize(Vector2.UnitX);
                stagger.Slide(dir, tier.PushTiles * 16f, tier.StaggerTicks);
            }

            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.6f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation, texture.Size() * 0.5f,
                Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
