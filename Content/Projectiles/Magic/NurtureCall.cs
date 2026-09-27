using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// The moment Forest, Nurture Me is said: green light drawn in from all round the caster
    /// to them, a bell and a rustle as it starts, and a flash of green as it arrives, just as
    /// the shield (NurtureShield) finishes growing. Nothing else: it deals nothing and the buff
    /// is put on by the cast itself.
    ///
    /// A projectile rather than particles from the cast, because the cast happens on the
    /// caster's machine only and this is sent to everyone; each client then draws its own.
    /// Hidden: all of it is particles and dust. ai[0] is its age.
    /// </summary>
    public class NurtureCall : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        public const int Ticks = 26;

        /// <summary>Ticks it spends drawing the light in; the flash comes as most of it has arrived.</summary>
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
            {
                SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.6f, Pitch = -0.35f }, at);
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = -0.2f }, at);
            }

            if (age < GatherTicks)
            {
                for (int i = 0; i < 2; i++)
                {
                    PADParticleHandler.SpawnParticle(new NurtureMote(Projectile.owner, Main.rand.NextFloat(MathHelper.TwoPi),
                        Main.rand.NextFloat(80f, 130f), Main.rand.Next(14, 21)));
                }
            }

            if (age == GatherTicks + 8)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, NurtureShield.Light, 0.3f, 16));
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, NurtureShield.Pale, 0.14f, 10, lights: false));

                for (int i = 0; i < 10; i++)
                {
                    Dust dust = Dust.NewDustPerfect(at, DustID.DryadsWard, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(1.5f, 3.5f),
                        0, default, Main.rand.NextFloat(0.9f, 1.3f));
                    dust.noGravity = true;
                }
            }
        }
    }
}
