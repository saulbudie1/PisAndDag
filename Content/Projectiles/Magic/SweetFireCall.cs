using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Mage;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// The moment Rage! Sweet Fire is said: embers drawn in from all round the caster with a
    /// breath of fire, then a burst of flame out of them, just as the ring (SweetFireAura)
    /// finishes growing. Nothing else: it deals nothing, and the buff (SweetFire) is put on by
    /// the cast itself.
    ///
    /// The same shape as Forest, Nurture Me's (NurtureCall): a projectile rather than particles
    /// from the cast, because the cast happens on the caster's machine only and this is sent
    /// to everyone; each client then draws its own. Hidden: all of it is particles and dust.
    /// ai[0] is its age.
    /// </summary>
    public class SweetFireCall : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        public const int Ticks = 26;

        /// <summary>Ticks it spends drawing the embers in; the burst comes as most of them have arrived.</summary>
        public const int GatherTicks = 12;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Ticks;
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.MountedCenter;
            int age = (int)Projectile.ai[0]++;

            if (Main.dedServ)
                return;

            Vector2 at = Projectile.Center;

            if (age == 0)
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = -0.35f }, at);

            // Embers drawn in from round about, arriving as the burst comes.
            if (age < GatherTicks)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 from = at + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(70f, 120f);
                    int life = Main.rand.Next(14, 20);
                    Vector2 velocity = (at - from) / (life * 0.85f);
                    Color colour = Main.rand.NextBool(3) ? MeagreFlameBall.Yellow : MeagreFlameBall.Orange;

                    PADParticleHandler.SpawnParticle(new Ember(from, velocity, colour, Main.rand.NextFloat(0.45f, 0.8f), life, lift: 0f));
                }
            }

            if (age == GatherTicks + 8)
            {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.7f, Pitch = 0.2f }, at);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.1f }, at);

                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, MeagreFlameBall.Orange, 0.45f, 16));
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, MeagreFlameBall.Yellow, 0.2f, 10, lights: false));

                Color[] palette = { MeagreFlameBall.Orange, MeagreFlameBall.Yellow, MeagreFlameBall.Yellow, MeagreFlameBall.Heart };
                SparkShard.Spray(at, MeagreFlameBall.Yellow, 14, 6f, 26f, 18, palette: palette);

                for (int i = 0; i < 14; i++)
                {
                    Dust dust = Dust.NewDustPerfect(at, DustID.Torch,
                        Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(1.5f, 4f), 0, default, Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                }
            }
        }
    }
}
