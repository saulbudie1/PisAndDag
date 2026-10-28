using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The Sidewinder's arrow (Items.Ranged.Sidewinder). In the air it goes as an arrow goes, falling as one after a
    /// moment, but side to side across its way as a sidewinder goes over sand, ten pixels each side, its head always
    /// along the way it moves. Into an enemy, it is an arrow. Onto the ground, it goes in: under the ground straight
    /// to the ground beneath the nearest enemy within 25 tiles of where it went in (one with ground under it within 12
    /// tiles), a furrow of sand thrown up over it as it goes, and up out of the ground there as a spike
    /// (SidewinderSpike). With nothing near it is a puff of sand where it fell.
    ///
    /// ai[0]: 0 flying, 1 under the ground; ai[1] its time; ai[2] what it goes under to, plus one. Its speed in the
    /// air is kept apart from the weave (localAI[1] the weave's phase), so the weave never bends where it goes.
    /// </summary>
    public class SidewinderArrow : ModProjectile
    {
        /// <summary>By the suggestion taken: 25 tiles.</summary>
        public const float SeekRange = 25f * 16f;

        /// <summary>Mine: ten pixels each side, a weave every 26 ticks; under the ground, 9 pixels a tick, for at most two seconds.</summary>
        public const float Weave = 10f, WeaveTicks = 26f, BurrowSpeed = 9f;
        public const int BurrowMost = 120, Fall = 15;

        /// <summary>Mine: the spike, half as much again as the arrow.</summary>
        public const float SpikeShare = 1.5f;

        /// <summary>How far under an enemy the ground may be for the spike to reach it.</summary>
        public const float GroundBelow = 12f * 16f;

        private int Mode
        {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        private ref float Time => ref Projectile.ai[1];

        /// <summary>The way it is shot, without the weave in it.</summary>
        private Vector2 straight;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.arrow = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
        }

        public override void AI()
        {
            Time++;

            if (Mode == 0)
                Fly();
            else
                Burrow();
        }

        private void Fly()
        {
            if (straight == Vector2.Zero)
                straight = Projectile.velocity;

            // Falling as an arrow falls, after a moment.
            if (Time > Fall)
                straight.Y = Math.Min(straight.Y + 0.1f, 16f);

            // Side to side across the straight way: its speed across is the weave's own (ten pixels each side).
            float w = MathHelper.TwoPi / WeaveTicks;
            Vector2 across = straight.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity = straight + across * (Weave * w * (float)Math.Cos(Time * w));
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(4))
            {
                PADParticleHandler.SpawnParticle(new SandGrain(Projectile.Center, -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Harenaeum.AnySand, Main.rand.Next(12, 20), Main.rand.NextFloat(0.8f, 1.3f)));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Mode != 0)
                return false;

            // Into the ground: under it from here.
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.friendly = false;
            Mode = 1;
            Time = 0f;
            Projectile.netUpdate = true;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
                Harenaeum.Burst(Projectile.Center, -oldVelocity, 10, 3f, 0.9f, 0.8f);
            }

            return false;
        }

        /// <summary>
        /// Under the ground: to the ground beneath what it goes for, straight, a furrow thrown up over it as it goes;
        /// up as a spike when it gets there. What it goes for is chosen where it went in, and kept while it lasts.
        /// </summary>
        private void Burrow()
        {
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 2);

            if (Projectile.owner == Main.myPlayer && Projectile.ai[2] == 0f)
            {
                NPC prey = Seek(Projectile.Center);
                Projectile.ai[2] = prey == null ? -1f : prey.whoAmI + 1;
                Projectile.netUpdate = true;
            }

            NPC target = Projectile.ai[2] > 0f && Main.npc[(int)Projectile.ai[2] - 1].active ? Main.npc[(int)Projectile.ai[2] - 1] : null;

            if (Projectile.ai[2] < 0f || Time > BurrowMost || (Projectile.ai[2] > 0f && target == null))
            {
                Fizzle();
                return;
            }

            if (target == null)
                return;

            if (!Under(target, out Vector2 ground))
            {
                Fizzle();
                return;
            }

            Vector2 way = ground - Projectile.Center;
            float distance = way.Length();

            if (distance <= BurrowSpeed + 2f)
            {
                Projectile.Center = ground;

                if (Projectile.owner == Main.myPlayer)
                {
                    float reach = MathHelper.Clamp(ground.Y - target.Top.Y + 10f, 40f, GroundBelow + 24f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), ground, Vector2.Zero, ModContent.ProjectileType<SidewinderSpike>(),
                        (int)(Projectile.damage * SpikeShare), Projectile.knockBack, Projectile.owner, reach);
                }

                Projectile.Kill();
                return;
            }

            Projectile.Center += way / distance * BurrowSpeed;

            if (!Main.dedServ)
                Furrow();
        }

        /// <summary>The nearest enemy within its reach that has ground under it near enough for a spike.</summary>
        public static NPC Seek(Vector2 from)
        {
            NPC best = null;
            float nearest = SeekRange;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy())
                    continue;

                float distance = Vector2.Distance(npc.Center, from);

                if (distance < nearest && Under(npc, out _))
                {
                    nearest = distance;
                    best = npc;
                }
            }

            return best;
        }

        /// <summary>The top of the ground beneath <paramref name="npc"/>, if it is within 12 tiles of its feet.</summary>
        public static bool Under(NPC npc, out Vector2 ground)
        {
            Vector2 feet = npc.Bottom - new Vector2(0f, 4f);
            return CourtOfHandsRite.FindGround(feet, out ground) && ground.Y - npc.Bottom.Y <= GroundBelow && ground.Y >= npc.Bottom.Y - 8f;
        }

        /// <summary>Sand thrown up out of the ground over it, as it goes under.</summary>
        private void Furrow()
        {
            if (!CourtOfHandsRite.FindGround(Projectile.Center, out Vector2 top))
                return;

            for (int i = 0; i < 2; i++)
            {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1.5f, 4f));
                PADParticleHandler.SpawnParticle(new SandGrain(top + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f), velocity, Harenaeum.AnySand,
                    Main.rand.Next(16, 28), Main.rand.NextFloat(1f, 1.8f)));
            }

            if ((int)Time % 3 == 0)
            {
                Dust dust = Dust.NewDustPerfect(top + new Vector2(0f, -2f), DustID.Sand, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 2.5f)), 0, default, 1.1f);
                dust.noGravity = false;
            }

            if ((int)Time % 12 == 1)
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 2 }, top);
        }

        private void Fizzle()
        {
            if (!Main.dedServ && CourtOfHandsRite.FindGround(Projectile.Center, out Vector2 top))
                Harenaeum.Burst(top - new Vector2(0f, 2f), -Vector2.UnitY, 8, 2.5f, 1f, 0.6f);

            Projectile.Kill();
        }

        public override void OnKill(int timeLeft)
        {
            if (Mode == 0 && !Main.dedServ)
                Harenaeum.Burst(Projectile.Center, -Projectile.velocity, 6, 2f, 1.2f, 0.4f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Mode != 0)
                return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.25f);

            // A faint trail of sand behind the weave.
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float f = 1f - i / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null, Harenaeum.Sand[2] * f * 0.35f,
                    Projectile.rotation, origin, Projectile.scale * (0.8f + 0.2f * f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
