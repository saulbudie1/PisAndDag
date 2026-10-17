using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// The moment Water! Resuscitate Me! is said: bubbles drawn up and in to the caster from all
    /// round and below, a burble and a splash as it starts, and as they arrive a flash of pale
    /// sea light and a ring of spray. Nothing else: it deals nothing, and the buff is put on by
    /// the cast itself.
    ///
    /// As NurtureCall: a projectile rather than particles from the cast, because the cast
    /// happens on the caster's machine only and this is sent to everyone; each client then
    /// draws its own. Hidden: all of it is particles and dust. ai[0] is its age.
    /// </summary>
    public class ResuscitateCall : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        public const int Ticks = 30;

        /// <summary>Ticks it spends drawing the bubbles in; the flash comes as most have arrived.</summary>
        public const int GatherTicks = 14;

        /// <summary>The flash: sea light, and the pale of it.</summary>
        public static readonly Color Light = new Color(110, 215, 255);
        public static readonly Color Pale = new Color(215, 248, 255);

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
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.55f, Pitch = -0.35f }, at);
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.3f }, at);
            }

            // Bubbles drawn in from about and below, rising as they come.
            if (age < GatherTicks)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 from = at + new Vector2(Main.rand.NextFloat(-70f, 70f), Main.rand.NextFloat(-20f, 70f));
                    Vector2 way = (at - from) / Main.rand.NextFloat(14f, 20f) + new Vector2(0f, -0.4f);
                    Dust bubble = Dust.NewDustPerfect(from, DustID.BubbleBurst_Blue, way, 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                    bubble.noGravity = true;
                    bubble.fadeIn = 1.1f;
                }
            }

            if (age == GatherTicks + 6)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Light, 0.3f, 18));
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Pale, 0.14f, 12, lights: false));

                for (int i = 0; i < 16; i++)
                {
                    Dust spray = Dust.NewDustPerfect(at, DustID.Water, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 4f),
                        0, default, Main.rand.NextFloat(1f, 1.4f));
                    spray.noGravity = true;
                }
            }
        }
    }
}
