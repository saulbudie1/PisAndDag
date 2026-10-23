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
    /// A cap come up where something died in a standing ring (FairyRingDeaths): a fruiting body of
    /// what the ring is. It comes up out of the ground over <see cref="GrowTicks"/>, roots of light
    /// spreading under it, and every <see cref="PuffEvery"/> ticks puffs a spore (FairyRingSpore) at the
    /// nearest enemy it can see within <see cref="Reach"/>, squashing as it does. It goes back into the
    /// ground when its ring does, or after <see cref="Life"/> ticks. <see cref="Most"/> to a player.
    ///
    /// The owner's machine aims and puffs, and counts its puffs in ai[0] (sent), so every machine
    /// squashes the cap when it puffs.
    /// </summary>
    public class FairyRingFruit : ModProjectile
    {
        public const int Most = 6;
        public const int GrowTicks = 26;
        public const int PuffEvery = 48;
        public const float Reach = 12f * 16f;
        public const int Life = 12 * 60;
        public const int WitherTicks = 30;

        /// <summary>Bigger than the ring's own caps: it has eaten.</summary>
        private const float Size = 1.2f;

        private const int SquashTicks = 12;

        public override string Texture => FairyRing.CapTexture;

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>Ticks since the last puff this machine saw (ai[0] counting up), for the squash.</summary>
        private float sincePuff = 999f;
        private float puffsSeen;

        /// <summary>Which cap of the sheet, which way it faces, its breath: rolled from where it stands.</summary>
        private int frame;
        private bool flip;
        private float phase;
        private Vector2[][] roots;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life + WitherTicks;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private bool Withering => Projectile.timeLeft <= WitherTicks;

        public override void AI()
        {
            if (Age == 0f)
                Born();

            Age++;
            Projectile.velocity = Vector2.Zero;

            // Its ring gone, it goes too.
            if (!Withering && FairyRing.StandingOf(Projectile.owner) == null)
                Projectile.timeLeft = WitherTicks;

            if (Projectile.ai[0] != puffsSeen)
            {
                puffsSeen = Projectile.ai[0];
                sincePuff = 0f;
            }

            sincePuff++;

            float g = Grown();
            Lighting.AddLight(Projectile.Center - new Vector2(0f, 14f), 0.08f * g, 0.24f * g, 0.65f * g);

            if (Projectile.owner == Main.myPlayer && Age >= GrowTicks && !Withering && Age % PuffEvery == 0f)
                Puff();

            if (!Main.dedServ && g > 0.5f && Main.rand.NextBool(30))
            {
                PADParticleHandler.SpawnParticle(new SporeMote(Top() + Main.rand.NextVector2Circular(5f, 2f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1f, -0.3f)), FairyRing.Pale * 0.8f,
                    Main.rand.NextFloat(0.5f, 0.8f), Main.rand.Next(40, 70)));
            }
        }

        /// <summary>Where the spores leave from: the top of the cap.</summary>
        private Vector2 Top() => Projectile.Center - new Vector2(0f, FairyRing.CapHigh * 0.75f * Size * Grown());

        /// <summary>How far up it is: out of the ground with a little too much, settling; back down as it withers.</summary>
        private float Grown()
        {
            float t = MathHelper.Clamp(Age / GrowTicks, 0f, 1f);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            float g = t <= 0f ? 0f : 1f + c3 * u * u * u + c1 * u * u;

            if (Withering)
            {
                float w = MathHelper.Clamp(Projectile.timeLeft / (float)WitherTicks, 0f, 1f);
                g *= w * w;
            }

            return Math.Max(0f, g);
        }

        /// <summary>Where it comes up: dark under the ground, then light; its roots laid; a rustle.</summary>
        private void Born()
        {
            var roll = new Random((int)(Projectile.Center.X * 13f) ^ (int)(Projectile.Center.Y * 7f));
            frame = roll.Next(FairyRing.CapFrames);
            flip = roll.Next(2) == 0;
            phase = (float)(roll.NextDouble() * MathHelper.TwoPi);

            roots = new Vector2[5][];

            for (int r = 0; r < roots.Length; r++)
            {
                float angle = MathHelper.Lerp(0.15f, MathHelper.Pi - 0.15f, (r + (float)roll.NextDouble() * 0.6f) / roots.Length);
                Vector2 way = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                float reach = 14f + (float)roll.NextDouble() * 18f;

                roots[r] = new Vector2[5];

                for (int s = 0; s < 5; s++)
                {
                    float t = s / 4f;
                    roots[r][s] = Projectile.Center + new Vector2(0f, 3f) + way * reach * t
                        + new Vector2((float)(roll.NextDouble() - 0.5), (float)(roll.NextDouble() - 0.5)) * (s == 0 ? 0f : 4f);
                }
            }

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.8f, Volume = 0.65f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f, Volume = 0.35f }, Projectile.Center);

            PADParticleHandler.SpawnParticle(new SoftGlow(Projectile.Center - new Vector2(0f, 6f), Vector2.Zero, FairyRing.Mid * 0.55f, 0.5f, 36, false));

            for (int k = 0; k < 10; k++)
            {
                PADParticleHandler.SpawnParticle(new SporeMote(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-4f, 2f)),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.6f, -0.5f)), FairyRing.Bright,
                    Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(45, 80)));
            }
        }

        /// <summary>Owner only: a spore at the nearest enemy it can see, if any is near enough.</summary>
        private void Puff()
        {
            Vector2 top = Top();
            NPC target = null;
            float best = Reach;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float d = Vector2.Distance(top, npc.Center);

                if (d < best && Collision.CanHitLine(top, 1, 1, npc.position, npc.width, npc.height))
                {
                    best = d;
                    target = npc;
                }
            }

            if (target == null)
                return;

            Vector2 velocity = (target.Center - top).SafeNormalize(-Vector2.UnitY) * 4.5f + new Vector2(0f, -1.2f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), top, velocity, ModContent.ProjectileType<FairyRingSpore>(),
                Projectile.damage, 0f, Projectile.owner);

            Projectile.ai[0]++;
            Projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float g = Grown();

            // Its roots, under the ground: bright as it comes up, fading to a faint glow.
            Texture2D line = FairyRing.Line;

            if (line != null && roots != null)
            {
                float k = Withering ? g : MathHelper.Clamp(1f - (Age - GrowTicks) / 120f, 0.25f, 1f) * MathHelper.Clamp(Age / 10f, 0f, 1f);

                foreach (Vector2[] root in roots)
                {
                    for (int s = 0; s < root.Length - 1; s++)
                    {
                        float fall = 1f - s / (float)root.Length;
                        FairyRing.Segment(line, root[s], root[s + 1], FairyRing.Mid * (0.5f * k * fall), 6f);
                        FairyRing.Segment(line, root[s], root[s + 1], FairyRing.Pale * (0.8f * k * fall), 2f);
                    }
                }
            }

            // Squashed down as it puffs, and up again.
            float squash = sincePuff < SquashTicks ? (float)Math.Sin(sincePuff / SquashTicks * MathHelper.Pi) : 0f;
            Vector2 scale = new Vector2(Size * g * (1f + 0.22f * squash), Size * g * (1f - 0.28f * squash));

            FairyRing.DrawCap(TextureAssets.Projectile[Type].Value, FairyRing.Glow, Projectile.Center, frame, scale, phase, flip,
                Withering ? 1f - g : 0f);

            return false;
        }
    }
}
