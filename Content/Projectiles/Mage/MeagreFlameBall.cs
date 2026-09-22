using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Mage
{
    /// <summary>
    /// The ball of fire Meagre Flame throws. Falls under gravity, burns whatever it
    /// touches, and goes off on the first thing it meets: tile or creature. The burst
    /// is the hit. Everything in the square around it takes the damage once and is set
    /// alight; the thing it struck directly is hit once too, not twice, because the
    /// local immunity it leaves on a struck creature never runs out.
    ///
    /// ai[0] is 1 for the charged throw: twice the size, three times the damage, twice
    /// the burst, and it burns longer. Size is applied on every client from ai[0] on
    /// the first tick, since OnSpawn runs only where it was thrown.
    ///
    /// The drawn sprite is no sprite at all. The ball is three additive blooms — a wide
    /// orange, a tighter yellow, a white heart — over a trail of embers, so it reads as
    /// a lump of fire rather than a picture of one.
    /// </summary>
    public class MeagreFlameBall : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        public const int Size = 22;
        public const float Gravity = 0.22f;
        public const float FallCap = 16f;

        /// <summary>Burst square, small and charged.</summary>
        public const int Burst = 80;
        public const int BurstBig = 160;

        public const int Burn = 240;
        public const int BurnBig = 360;

        public static readonly Color Orange = new Color(223, 113, 38);
        public static readonly Color Yellow = new Color(251, 242, 54);
        public static readonly Color Heart = new Color(255, 240, 200);

        public bool Big => Projectile.ai[0] == 1f;

        /// <summary>1 for the small ball, 2 for the charged one. Scales the ball's own visuals.</summary>
        private float Scale => Big ? 2f : 1f;

        /// <summary>The burst's visuals on top of that: the small one a little under, the charged one a little over.</summary>
        private float BurstScale => Big ? 2f * 1.25f : 0.8f;

        /// <summary>The charged ball's hitbox. Smaller than twice, which read as bigger than it looks.</summary>
        public const int SizeBig = 32;

        private bool sized;

        public override void SetDefaults()
        {
            Projectile.width = Size;
            Projectile.height = Size;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;

            // A struck creature is done with this projectile: the direct hit is its share
            // of the burst.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            if (!sized)
            {
                sized = true;

                if (Big)
                    Projectile.Resize(SizeBig, SizeBig);
            }

            Projectile.velocity.Y += Gravity;

            if (Projectile.velocity.Y > FallCap)
                Projectile.velocity.Y = FallCap;

            Projectile.rotation += 0.08f * (Projectile.velocity.X < 0f ? -1f : 1f);
            Projectile.localAI[0]++;

            float glow = 0.9f * Scale;
            Lighting.AddLight(Projectile.Center, 1f * glow, 0.5f * glow, 0.15f * glow);

            if (Main.dedServ)
                return;

            Trail();
        }

        /// <summary>Embers peeled off the back, a thread of smoke, the odd torch spark.</summary>
        private void Trail()
        {
            float s = Scale;
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);

            int embers = Big ? 4 : 2;

            for (int i = 0; i < embers; i++)
            {
                Vector2 at = Projectile.Center + Main.rand.NextVector2Circular(5f * s, 5f * s);
                Vector2 vel = back * Main.rand.NextFloat(0.4f, 1.6f) + Main.rand.NextVector2Circular(0.6f, 0.6f);
                Color colour = Main.rand.NextBool(3) ? Yellow : Orange;

                PADParticleHandler.SpawnParticle(new Ember(at, vel, colour,
                    Main.rand.NextFloat(0.5f, 0.9f) * s, Main.rand.Next(16, 28), lift: 0.05f));
            }

            if (Projectile.localAI[0] % 3 == 0)
            {
                PADParticleHandler.SpawnParticle(new MuzzleSmoke(
                    Projectile.Center + back * 6f * s + Main.rand.NextVector2Circular(3f, 3f),
                    back * Main.rand.NextFloat(0.5f, 1.2f),
                    Main.rand.Next(24, 40),
                    Main.rand.NextFloat(0.3f, 0.5f) * s));
            }

            if (Main.rand.NextBool(Big ? 2 : 4))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f * s, 6f * s),
                    DustID.Torch, back * Main.rand.NextFloat(0.5f, 2f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    0, default, Main.rand.NextFloat(0.9f, 1.4f) * s);
                dust.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, Big ? BurnBig : Burn);

            // The burst itself calls this for everything it catches, so only the ball in
            // flight needs to end here. In the burst the projectile is already dying.
            if (Projectile.timeLeft > 1)
                Projectile.Kill();
        }

        public override bool PreKill(int timeLeft)
        {
            // Damage is the owner's to deal. Every client draws the burst.
            if (Projectile.owner == Main.myPlayer)
            {
                int reach = Big ? BurstBig : Burst;
                Vector2 centre = Projectile.Center;

                Projectile.Resize(reach, reach);
                Projectile.Center = centre;

                // Marks it as dying so a hit inside the burst does not try to kill it again.
                Projectile.timeLeft = 1;

                Projectile.Damage();
            }

            if (!Main.dedServ)
                Explode();

            return true;
        }

        /// <summary>The burst: a flash, sparks flung, embers, a roll of smoke and a bang.</summary>
        private void Explode()
        {
            float s = BurstScale;
            Vector2 at = Projectile.Center;

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Orange, 0.7f * s, 16));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Yellow, 0.42f * s, 12));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Heart, 0.2f * s, 8, lights: false));

            Color[] palette = { Orange, Yellow, Yellow, Heart };
            SparkShard.Spray(at, Yellow, Big ? 36 : 18, (Big ? 10f : 7f) * (Big ? 1.25f : 0.8f), (Big ? 30f : 24f) * (Big ? 1.25f : 0.8f), 20, falls: true, palette: palette);

            int embers = Big ? 40 : 18;

            for (int i = 0; i < embers; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 6f) * (Big ? 1.3f : 1f);
                Color colour = Main.rand.NextBool(3) ? Yellow : Orange;

                PADParticleHandler.SpawnParticle(new Ember(at + Main.rand.NextVector2Circular(6f * s, 6f * s), vel, colour,
                    Main.rand.NextFloat(0.8f, 1.5f) * s, Main.rand.Next(30, 50), lift: 0.05f));
            }

            int smokes = Big ? 8 : 4;

            for (int i = 0; i < smokes; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 4.5f) * s;

                PADParticleHandler.SpawnParticle(new MuzzleSmoke(at + Main.rand.NextVector2Circular(8f * s, 8f * s), vel,
                    Main.rand.Next(36, 60), Main.rand.NextFloat(0.5f, 0.9f) * (Big ? 1.4f : 1f)));
            }

            int sparks = Big ? 24 : 12;

            for (int i = 0; i < sparks; i++)
            {
                Dust dust = Dust.NewDustPerfect(at, DustID.Torch,
                    Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 7f) * s,
                    0, default, Main.rand.NextFloat(1.2f, 1.8f) * s);
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with
            {
                Volume = Big ? 0.9f : 0.65f,
                Pitch = Big ? -0.25f : 0.2f,
                PitchVariance = 0.1f,
            }, at);

            if (Big)
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(at, Main.rand.NextVector2Unit(), 5f, 6f, 12, 1000f, "PAD Meagre Flame"));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D bloom = TextureAssets.Projectile[Type].Value;
            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 origin = bloom.Size() * 0.5f;

            // Breathes: a slow pulse on the outer glow, a quicker one on the heart.
            float slow = 1f + 0.08f * (float)System.Math.Sin(Projectile.localAI[0] * 0.25f);
            float quick = 1f + 0.12f * (float)System.Math.Sin(Projectile.localAI[0] * 0.6f);
            float s = Scale;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(bloom, at, null, Orange * 0.85f, Projectile.rotation, origin, 0.32f * s * slow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, at, null, Yellow * 0.9f, -Projectile.rotation, origin, 0.2f * s * quick, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, at, null, Heart, 0f, origin, 0.1f * s * quick, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
