using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.Items.Mage.Spell;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// One of Cold Call's icicles.
    ///
    /// ai[0] is its age in ticks. It is spawned below zero and sits in the staff's head, unseen
    /// and harmless, until the count comes up to zero: that is how the five of one cast leave a
    /// fifth of a period apart. ai[1] is the aim, ai[2] the period of the wave in ticks.
    ///
    /// In flight it holds the wave. Its heading swings either side of the aim on a cosine,
    /// which puts it on a sine path at an even speed, rising first whichever way it was cast.
    /// Every icicle of every cast runs the same path, so together they read as one wave.
    ///
    /// Homing is Calamity's HomeInOnNPC, as a pattern: the closest thing that can be chased
    /// within <see cref="Reach"/> plus half its width and half its height, in plain sight, and
    /// the velocity eased toward it by <see cref="Inertia"/>. The homing speed handed to that
    /// easing is the icicle's own <see cref="Speed"/>, and the extra update Calamity adds while
    /// homing is left out, so it never goes faster homing than it does in the wave. A hard turn
    /// costs it speed, as it does Calamity's; it picks the speed up again once it is lined up,
    /// or straight away if the target is gone. Once it has homed it leaves the wave for good.
    ///
    /// Frostbite and Chilled on whatever it hits; ChilledNPCs is what makes Chilled slow.
    /// </summary>
    public class ColdCallIcicle : ModProjectile
    {
        public const float Speed = 10f;

        /// <summary>
        /// How far the heading swings either side of the aim, in radians. At Speed and a
        /// 25-tick period, 0.9 takes the path about 33 pixels either side of the aim, over about
        /// 200 pixels of travel toward it.
        /// </summary>
        public const float Sway = 0.9f;

        /// <summary>Homing reach: 8 tiles, to the target's centre less half its width and half its height.</summary>
        public const float Reach = 8 * 16f;

        /// <summary>Calamity's inertia: the old velocity counts this many times over against the pull.</summary>
        public const float Inertia = 8f;

        /// <summary>Ticks of each debuff per hit.</summary>
        public const int Frostbite = 120;
        public const int Chill = 120;

        public static readonly Color Ice = new Color(112, 196, 245);
        public static readonly Color Pale = new Color(196, 238, 255);

        /// <summary>The cold mist that comes off the icicles.</summary>
        public static readonly Color Fog = new Color(200, 230, 255);

        /// <summary>No mist until it has been out of the staff this long: half a second.</summary>
        public const int MistDelay = 15;

        /// <summary>One tick in this many, once it is past MistDelay and on screen, it lets go a wisp of mist.</summary>
        public const int MistChance = 5;

        /// <summary>The point of the sprite the hitbox centres on: a third of the way back from the tip.</summary>
        private static readonly Vector2 Pivot = new Vector2(5f, 9f);

        private float Age
        {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        private float Aim => Projectile.ai[1];

        private float Period => Projectile.ai[2] > 0f ? Projectile.ai[2] : 25f;

        /// <summary>Out of the staff. Worked out the same way on every client.</summary>
        private bool released;

        /// <summary>Set the first time it homes. From then on it is done with the wave.</summary>
        private bool homed;

        /// <summary>Which of the five it is, 0 first, from how long it had to wait. Sets the pitch of its note.</summary>
        private int index = -1;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;

            // Vanilla's rule for anything that homes.
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;

            // All five of a cast may reach the same creature: each keeps its own immunity, so one
            // icicle's hit does not turn the next one away.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => released ? null : false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (index < 0)
                index = (int)Math.Round(-Age / (Period / ColdCall.Count));

            if (!released)
            {
                // Waiting its turn: one tick for every tick below zero, so an icicle spawned
                // at -5 leaves five ticks after the one spawned at 0.
                if (Age < 0f)
                {
                    // The call was cut off before this one left: it never does.
                    if (!owner.active || owner.dead || owner.CCed || owner.HeldItem?.ModItem is not Catalyst)
                    {
                        Projectile.Kill();
                        return;
                    }

                    Projectile.Center = ColdCallHold.HeadOf(owner);
                    Projectile.velocity = Vector2.Zero;
                    Projectile.tileCollide = false;
                    Age++;
                    return;
                }

                Release(owner);
            }

            Fly();
            Age++;
        }

        /// <summary>The heading the wave asks for at this age.</summary>
        private float Heading()
        {
            // The half tick keeps the wave centred on the aim; the side makes it rise first
            // whichever way it was cast.
            float side = Math.Cos(Aim) >= 0.0 ? 1f : -1f;
            return Aim - side * Sway * (float)Math.Cos(MathHelper.TwoPi * (Age + 0.5f) / Period);
        }

        /// <summary>Out of the staff's head and away, with a note and a flash. No mist yet: see MistDelay.</summary>
        private void Release(Player owner)
        {
            released = true;
            Projectile.tileCollide = true;
            Projectile.Center = ColdCallHold.HeadOf(owner);
            Projectile.velocity = Heading().ToRotationVector2() * Speed;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // It sat still in the staff; that is not a trail.
            for (int i = 0; i < Projectile.oldPos.Length; i++)
                Projectile.oldPos[i] = Vector2.Zero;

            if (Main.dedServ)
                return;

            Vector2 at = Projectile.Center;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            // Five rising notes to a cast.
            SoundEngine.PlaySound(SoundID.Item28 with
            {
                Volume = 0.35f,
                Pitch = -0.3f + 0.15f * index,
                MaxInstances = 5,
            }, at);

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, dir * 1.5f, Ice, 0.16f, 10));
        }

        private void Fly()
        {
            NPC target = FindTarget();

            if (target != null)
            {
                homed = true;
                Vector2 toward = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = (Projectile.velocity * Inertia + toward * Speed) / (Inertia + 1f);
            }
            else if (!homed)
            {
                Projectile.velocity = Heading().ToRotationVector2() * Speed;
            }
            else
            {
                // Lost what it was after: straight on, and back up to speed.
                float speed = Projectile.velocity.Length();

                if (speed < Speed)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Aim.ToRotationVector2()) * MathHelper.Lerp(speed, Speed, 0.25f);
            }

            // Nearly stopped mid-turn it has no direction to speak of; keep the last one.
            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, Ice.ToVector3() * 0.45f);

            if (!Main.dedServ)
                Trail();
        }

        /// <summary>
        /// Calamity's target: the closest thing that can be chased, within reach of its edge
        /// rather than its centre, that the icicle can see.
        /// </summary>
        private NPC FindTarget()
        {
            NPC best = null;
            float bestDistance = float.MaxValue;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                float edge = npc.width / 2 + npc.height / 2;

                if (!npc.CanBeChasedBy(Projectile) || !Projectile.WithinRange(npc.Center, Reach + edge))
                    continue;

                float distance = Vector2.Distance(npc.Center, Projectile.Center);

                if (distance < bestDistance && Collision.CanHit(Projectile.Center, 1, 1, npc.Center, 1, 1))
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        /// <summary>
        /// Cold coming off it: the odd glint from the start, and from half a second out
        /// (<see cref="MistDelay"/>) a thin, scattered mist that sinks and spreads behind. Only
        /// on screen: a volley flying off the edge would otherwise fill the particle budget with
        /// mist nobody sees.
        /// </summary>
        private void Trail()
        {
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Rectangle view = new Rectangle((int)Main.screenPosition.X - 64, (int)Main.screenPosition.Y - 64,
                Main.screenWidth + 128, Main.screenHeight + 128);

            if (!view.Contains(Projectile.Center.ToPoint()))
                return;

            // Mist only once it is half a second out, now and then rather than on a beat, and
            // scattered either side of its line rather than laid along it.
            if (Age >= MistDelay && Main.rand.NextBool(MistChance))
            {
                Vector2 side = new Vector2(-back.Y, back.X);
                Vector2 at = Projectile.Center + back * Main.rand.NextFloat(4f, 16f)
                    + side * Main.rand.NextFloat(-10f, 10f) + Main.rand.NextVector2Circular(3f, 3f);
                Vector2 drift = back * Main.rand.NextFloat(0.1f, 0.5f) + side * Main.rand.NextFloat(-0.5f, 0.5f)
                    + new Vector2(0f, 0.08f);

                ColdFog.Spawn(at, drift, Fog, 0.12f, 0.7f, 30, 80);
            }

            if (Main.rand.NextBool(4))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), DustID.Frost,
                    back * Main.rand.NextFloat(0.5f, 1.5f), 0, default, Main.rand.NextFloat(0.8f, 1.1f));
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Frostburn2, Frostbite);
            target.AddBuff(BuffID.Chilled, Chill);
        }

        /// <summary>
        /// It shatters: a crack of ice, splinters of light, shards that fall, and, if it had been
        /// out long enough to be making mist, a last breath of cold.
        /// </summary>
        public override void OnKill(int timeLeft)
        {
            if (!released || Main.dedServ)
                return;

            Vector2 at = Projectile.Center;

            SoundEngine.PlaySound(SoundID.Item27 with
            {
                Volume = 0.45f,
                Pitch = 0.25f,
                PitchVariance = 0.2f,
                MaxInstances = 3,
            }, at);

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Ice, 0.22f, 12));
            SparkShard.Spray(at, Pale, 7, 4.5f, 16f, 14, palette: new[] { Ice, Pale, Pale, Color.White });

            for (int i = 0; i < 5; i++)
            {
                Dust.NewDustPerfect(at, DustID.Ice, Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY,
                    0, default, Main.rand.NextFloat(0.8f, 1.1f));
            }

            if (Age >= MistDelay)
            {
                int wisps = Main.rand.Next(1, 3);

                for (int i = 0; i < wisps; i++)
                {
                    ColdFog.Spawn(at + Main.rand.NextVector2Circular(10f, 10f), Main.rand.NextVector2Circular(0.9f, 0.9f),
                        Fog, 0.2f, 0.75f, 40, 85);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!released)
                return false;

            Texture2D icicle = TextureAssets.Projectile[Type].Value;
            Texture2D bloom = ModContent.Request<Texture2D>("PisAndDag/Content/Particles/BloomGeneric").Value;
            Vector2 centre = Projectile.Center - Main.screenPosition;
            int length = Projectile.oldPos.Length;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // The trail: the icicle again at each place it has just been, fainter and thinner
            // the further back, so the path of the wave hangs behind it for a moment. Vanilla
            // fills the cache after the move, so slot 0 is where it is now: skipped.
            for (int i = length - 1; i >= 1; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float near = 1f - (float)i / length;
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                Main.EntitySpriteDraw(icicle, at, null, Ice * (0.45f * near * near), Projectile.oldRot[i], Pivot,
                    new Vector2(0.3f + 0.4f * near, 0.7f + 0.3f * near), SpriteEffects.None, 0);
            }

            // A cold light round it, whiter close in, breathing a little.
            float pulse = 1f + 0.12f * (float)Math.Sin(Age * 0.5f + index);
            Vector2 middle = bloom.Size() * 0.5f;

            Main.EntitySpriteDraw(bloom, centre, null, Ice * 0.55f, 0f, middle, 0.17f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, centre, null, Pale * 0.5f, 0f, middle, 0.08f * pulse, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // The icicle itself, lit from inside.
            Main.EntitySpriteDraw(icicle, centre, null, Color.White, Projectile.rotation, Pivot, 1f, SpriteEffects.None, 0);

            return false;
        }
    }
}
