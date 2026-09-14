using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The Chlorophyte Rifle's round. Fast, flat, and short-lived.
    ///
    /// All the on-hit behaviour — the spill, the purified ground, the combo tick — lives
    /// on PADGlobalProjectile behind the ChloRifle flag, not here. That way the burst
    /// rounds get it from the same code, and anything else in the mod can opt in without
    /// inheriting from this class.
    /// </summary>
    public class ChlorophyteBullet : ModProjectile
    {
        /// <summary>Ticks of straight flight before gravity is allowed to bite.</summary>
        private const int GravityDelay = 26;

        /// <summary>Downward pull once that delay passes. Small — this is a rifle, not a bow.</summary>
        private const float Gravity = 0.055f;

        private const float MaxFall = 12f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.aiStyle = -1;               // hand-written AI, so no vanilla style
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 2;           // the "high velocity" part: 3 moves/tick

            // Set here, not after NewProjectile returns. SetDefaults runs on every side —
            // including a client rebuilding this from a spawn packet — so the flag needs
            // no netcode. Assigning it at the call site would leave it false on the
            // server, which is the side that converts tiles.
            Projectile.PAD().ChloRifle = true;
        }

        public override void AI()
        {
            // Sprite is drawn pointing up, so the neutral rotation is a quarter turn back.
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // extraUpdates means AI runs three times per frame, so the delay is counted in
            // real ticks rather than AI calls or the drop starts three times too early.
            if (Projectile.ai[0]++ > GravityDelay * (Projectile.extraUpdates + 1))
                Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + Gravity, MaxFall);

            Lighting.AddLight(Projectile.Center, 0.25f, 0.55f, 0.18f);

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleGrass,
                    Projectile.velocity * -0.08f, 120, default, 0.85f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleGrass,
                    Main.rand.NextVector2Circular(2.2f, 2.2f), 100, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
