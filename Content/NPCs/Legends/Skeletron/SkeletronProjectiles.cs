using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>What his shots share: finding the head that made one, for those that go where he says (puppets).</summary>
    internal static class SkeletronShots
    {
        /// <summary>
        /// The head this shot was made for (its slot in ai[0]) and that head's state, while it is still in the move that
        /// made the shot (its count of moves, in ai[2]).
        /// </summary>
        internal static bool Head(Projectile projectile, out NPC head, out LegendsSkeletron state)
        {
            head = null;
            state = null;

            int index = (int)projectile.ai[0];

            if (index < 0 || index >= Main.maxNPCs)
                return false;

            head = Main.npc[index];

            if (!head.active || head.type != NPCID.SkeletronHead || !head.TryGetGlobalNPC(out state))
                return false;

            return state.serial == (int)projectile.ai[2] && state.move != LegendsSkeletron.Move.None;
        }

        /// <summary>Whether a box touches a ball of this radius about this point.</summary>
        internal static bool Touches(Rectangle box, Vector2 centre, float radius)
        {
            float x = MathHelper.Clamp(centre.X, box.Left, box.Right);
            float y = MathHelper.Clamp(centre.Y, box.Top, box.Bottom);
            return Vector2.DistanceSquared(centre, new Vector2(x, y)) <= radius * radius;
        }

        /// <summary>Whether a box touches a thick line.</summary>
        internal static bool Touches(Rectangle box, Vector2 from, Vector2 to, float width)
        {
            float ignored = 0f;
            return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(), from, to, width, ref ignored);
        }

        /// <summary>The light where something is, never so dark it cannot be seen.</summary>
        internal static Color Lit(Vector2 at, float least = 0.35f)
        {
            Color light = Lighting.GetColor((int)(at.X / 16f), (int)(at.Y / 16f));
            return Color.Lerp(light, Color.White, least * (1f - light.ToVector3().Length() / 1.732f));
        }
    }

    // ================================================================================================================

    /// <summary>
    /// His skulls (the game's own skull, Projectile_270, and only that, by request: "using only the homing-skull
    /// texture"). One kind, many ways of flying (ai[0]):
    ///
    ///     HOMING    out of his mouth, then turned on whoever is nearest, faster and faster; past them once, it flies on
    ///     RAIN      breathed up into the sky, slowing, falling back as a rain; shattered on the ground (his hands' loss)
    ///     INDICATED up and slowing; turned on the nearest, the line it will take drawn bright; then down it, fast and
    ///               straight, through anything (ai[1] its place in the breath, ai[2] its way once decided)
    ///     SUMMONED  out of a puff of smoke, spinning in place a while (ai[1] ticks), then homing (the seals)
    ///     SPAT      spat out in time to the guitar, homing softly (the solo)
    ///
    /// Each machine flies it from what the server made it with; the way an indicated skull takes is the server's, sent.
    /// </summary>
    public class SkeletronSkull : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Skull;

        internal const float Homing = 0f;
        internal const float Rain = 1f;
        internal const float Indicated = 2f;
        internal const float Summoned = 3f;
        internal const float Spat = 4f;

        /// <summary>An indicated skull's way, before it is decided.</summary>
        private const float NoWay = 1000f;

        /// <summary>Indicated: slowing, then turned (one after another), then the line held this long, then gone this fast.</summary>
        private const int IndicateRise = 28;
        private const int IndicateStagger = 5;
        private const int IndicateTell = 42;
        private const float DashSpeed = 24f;
        private const float LineLength = 1800f;

        /// <summary>Each skull's eye, as the sheet has them: yellow, blue, red.</summary>
        private static readonly Color[] Eyes = { new Color(255, 220, 80), new Color(100, 190, 255), new Color(255, 70, 70) };

        private float Mode => Projectile.ai[0];
        private int Age => (int)Projectile.localAI[0];

        /// <summary>A homing skull that has gone past whoever it chased, or lost them, flies on, straight.</summary>
        private bool spent;

        /// <summary>The nearest it has come to whoever it chases.</summary>
        private float closest = float.MaxValue;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)LineLength;
        }

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            Projectile.frame = Math.Abs((int)Projectile.ai[1]) % 3;

            if (Mode == Indicated)
                Projectile.ai[2] = NoWay;

            if (Mode == Rain)
                Projectile.timeLeft = 240;

            if (Projectile.damage <= 0)
                Projectile.hostile = false;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;
            int t = Age;
            Projectile.frame = Math.Abs((int)Projectile.ai[1]) % 3;

            if (Mode == Summoned)
                Projectile.alpha = Math.Max(0, Projectile.alpha - 12);
            else
                Projectile.alpha = Math.Max(0, Projectile.alpha - 40);

            if (Mode == Rain)
            {
                // Up, slowing; then down, a rain; shattered on the ground.
                if (t > 12)
                    Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.22f, 11f);

                Projectile.velocity.X *= 0.99f;
                Projectile.tileCollide = Projectile.velocity.Y > 2f;
                Projectile.rotation += Projectile.velocity.X * 0.03f;
            }
            else if (Mode == Indicated)
            {
                Indicate(t);
            }
            else if (Mode == Summoned)
            {
                int wait = (int)Projectile.ai[1];

                if (t < wait)
                {
                    Projectile.velocity *= 0.85f;
                    Projectile.rotation += 0.35f * (1f - t / (float)Math.Max(1, wait));
                    Projectile.scale = MathHelper.Lerp(0.2f, 1f, LegendsSkeleton_Ease(t / 14f));
                }
                else
                {
                    Home(0.075f, 11f, 0.035f);
                }
            }
            else
            {
                int delay = Mode == Spat ? 14 : 20;

                if (t > delay)
                    Home(Mode == Spat ? 0.05f : 0.07f, Mode == Spat ? 12f : 11f, 0.04f);

                Projectile.rotation = Projectile.velocity.X * 0.04f;
            }

            if (Projectile.velocity.X != 0f)
                Projectile.spriteDirection = Projectile.velocity.X > 0f ? 1 : -1;

            Lighting.AddLight(Projectile.Center, Eyes[Projectile.frame].ToVector3() * 0.35f);
        }

        /// <summary>
        /// Turned on the nearest player, faster and faster, and turning wider the faster it goes; given up (flying on,
        /// straight) once it has gone past them, or once they cut out of its sight at speed (by request: "if i break their
        /// ankle they better stopp following me").
        /// </summary>
        private void Home(float turn, float most, float gain)
        {
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];

            if (spent || !target.active || target.dead)
            {
                // Past them: on, a little faster, never faster than it homed.
                if (Projectile.velocity.Length() < most)
                    Projectile.velocity *= 1.01f;

                return;
            }

            Vector2 to = target.Center - Projectile.Center;
            float distance = to.Length();
            float speed = Math.Min(most, Projectile.velocity.Length() + most * gain);
            Vector2 way = Projectile.velocity.SafeNormalize(to.SafeNormalize(Vector2.UnitY));

            // Past them: through them, or by them and now going away.
            if (distance < 46f || closest < 150f && distance > closest + 20f)
                spent = true;

            closest = Math.Min(closest, distance);

            // Ankles broken: up to speed, it cannot follow someone who cuts more than about 65 degrees off its nose.
            float off = Math.Abs(MathHelper.WrapAngle(to.ToRotation() - way.ToRotation()));

            if (Projectile.velocity.Length() > most * 0.6f && off > 1.15f)
                spent = true;

            if (spent)
                return;

            turn *= MathHelper.Lerp(1f, 0.55f, speed / most);
            float angle = way.ToRotation().AngleTowards(to.ToRotation(), turn);

            Projectile.velocity = angle.ToRotationVector2() * speed;
        }

        private int AimAt => IndicateRise + (int)Projectile.ai[1] * IndicateStagger;
        private bool Aimed => Projectile.ai[2] != NoWay;

        /// <summary>Slowing; turned; the line held; then down it.</summary>
        private void Indicate(int t)
        {
            if (t < AimAt)
            {
                Projectile.velocity *= 0.93f;
                Projectile.rotation = Projectile.velocity.X * 0.05f;
                return;
            }

            if (t == AimAt && Projectile.owner == Main.myPlayer)
            {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];

                // Not quite all at one point: a little each side, so they cross the ground there.
                float spread = (Projectile.ai[1] - 2.5f) * 0.06f;
                Projectile.ai[2] = (target.Center - Projectile.Center).ToRotation() + spread;
                Projectile.netUpdate = true;
            }

            if (t < AimAt + IndicateTell)
            {
                Projectile.velocity *= 0.8f;
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);

                if (Aimed && t == AimAt + 2)
                    SoundEngine.PlaySound(SkeletronSounds.Clack with { Volume = 0.45f, Pitch = 0.4f + Projectile.ai[1] * 0.08f }, Projectile.Center);

                return;
            }

            if (!Aimed)
                return;

            if (t == AimAt + IndicateTell && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 0.7f, MaxInstances = 6 }, Projectile.Center);

            Projectile.velocity = Projectile.ai[2].ToRotationVector2() * DashSpeed;
            Projectile.rotation = 0f;
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        public override bool CanHitPlayer(Player target)
        {
            if (Projectile.damage <= 0)
                return false;

            if (Mode == Indicated)
                return Aimed && Age >= AimAt + IndicateTell;

            if (Mode == Summoned)
                return Age >= (int)Projectile.ai[1];

            return Age > 6;
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SkeletronSounds.Clack with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);

            for (int i = 0; i < 10; i++)
                Dust.NewDustPerfect(Projectile.Center, DustID.Bone, Main.rand.NextVector2Circular(4f, 4f), 0, default, Main.rand.NextFloat(0.9f, 1.3f));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D skull = TextureAssets.Projectile[Type].Value;
            int frameHeight = skull.Height / Main.projFrames[Type];
            Rectangle frame = new Rectangle(0, Projectile.frame * frameHeight, skull.Width, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            float shown = 1f - Projectile.alpha / 255f;
            int t = Age;
            Color eye = Eyes[Projectile.frame];
            SpriteEffects flip = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color lit = SkeletronShots.Lit(Projectile.Center) * shown;
            SpriteBatch spriteBatch = Main.spriteBatch;

            // An indicated skull's line: faint and then sure, until it has gone down it.
            if (Mode == Indicated && Aimed && t < AimAt + IndicateTell + 4)
            {
                float sure = MathHelper.Clamp((t - AimAt) / (float)IndicateTell, 0f, 1f);
                Vector2 end = Projectile.Center + Projectile.ai[2].ToRotationVector2() * LineLength;
                float flash = t >= AimAt + IndicateTell - 8 ? 0.5f + 0.5f * (float)Math.Sin(t * 1.6f) : 0f;

                SkeletronRig.Beam(spriteBatch, Projectile.Center, end, eye * ((0.15f + 0.35f * sure + 0.3f * flash) * shown), 10f + 10f * sure);
                SkeletronRig.Line(spriteBatch, Projectile.Center, end, eye * ((0.25f + 0.5f * sure) * shown), 1.5f + sure);
            }

            // Where it has been, once it flies.
            bool flying = Projectile.velocity.LengthSquared() > 16f;

            if (flying)
            {
                for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    Vector2 was = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;

                    spriteBatch.Draw(skull, was - Main.screenPosition, frame, lit * (0.35f * fade), Projectile.rotation, origin, Projectile.scale, flip, 0f);
                    SkeletronRig.Glow(spriteBatch, was, eye * (0.18f * fade * shown), 16f * Projectile.scale);
                }
            }

            SkeletronRig.Glow(spriteBatch, Projectile.Center, eye * (0.3f * shown), 24f * Projectile.scale);
            spriteBatch.Draw(skull, Projectile.Center - Main.screenPosition, frame, lit, Projectile.rotation, origin, Projectile.scale, flip, 0f);

            // The eye lit: the socket on the side it faces.
            Vector2 socket = new Vector2(6f * Projectile.spriteDirection, -3f).RotatedBy(Projectile.rotation) * Projectile.scale;
            SkeletronRig.Hot(spriteBatch, Projectile.Center + socket, eye * shown, 5f * Projectile.scale);

            return false;
        }

        private static float LegendsSkeleton_Ease(float t) => LegendsSkeletron.Ease(t);
    }

    // ================================================================================================================

    /// <summary>
    /// A blast: a ring of force out from where something of his struck (a fist into the ground, the ball bursting, the
    /// hands meeting in purple), hurting whoever is inside it as it goes out. ai[0] its reach, ai[1] its colour (bone,
    /// purple, fire).
    /// </summary>
    public class SkeletronBlast : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        internal const float Bone = 0f;
        internal const float Purple = 1f;
        internal const float Fire = 2f;

        private const int Life = 16;
        private const int Grows = 7;

        private static readonly Color[] Colours = { new Color(240, 228, 190), new Color(170, 70, 255), new Color(255, 110, 40) };

        private float Reach => Math.Max(20f, Projectile.ai[0]);
        private int Age => (int)Projectile.localAI[0];
        private float Radius => Reach * LegendsSkeletron.EaseOut(Age / (float)Grows);

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;
            Projectile.velocity = Vector2.Zero;

            Color colour = Colours[Math.Clamp((int)Projectile.ai[1], 0, 2)];
            Lighting.AddLight(Projectile.Center, colour.ToVector3() * 0.8f * (1f - Age / (float)Life));
        }

        public override bool CanHitPlayer(Player target) => Age <= Grows + 2;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => SkeletronShots.Touches(targetHitbox, Projectile.Center, Radius);

        public override bool PreDraw(ref Color lightColor)
        {
            Color colour = Colours[Math.Clamp((int)Projectile.ai[1], 0, 2)];
            float fade = 1f - LegendsSkeletron.Ease((Age - Grows) / (float)(Life - Grows));
            float r = Radius;

            SkeletronRig.Glow(Main.spriteBatch, Projectile.Center, colour * (0.45f * fade), r * 1.1f);
            SkeletronRig.SoftRing(Main.spriteBatch, Projectile.Center, r, colour * (0.8f * fade), 14f * fade + 4f);
            SkeletronRig.Ring(Main.spriteBatch, Projectile.Center, r * 0.92f, colour * (0.6f * fade), 2f);
            return false;
        }
    }

    // ================================================================================================================

    /// <summary>
    /// The clap's shock (his hands' Clap, by request: "a shockwave upwards"): a wall of force going up from where his
    /// hands met, fast, a hand's width wide: whoever jumped the clap meets it.
    /// </summary>
    public class ShockColumn : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        internal const float Speed = 26f;
        internal const int Life = 26;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults()
        {
            Projectile.width = 130;
            Projectile.height = 90;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            Projectile.velocity = new Vector2(0f, -Speed);
            Projectile.localAI[0]++;

            if (!Main.dedServ)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), Main.rand.NextFloat(-30f, 30f));
                    Dust dust = Dust.NewDustPerfect(at, DustID.Bone, new Vector2(0f, -Main.rand.NextFloat(4f, 10f)), 100, default, 1.2f);
                    dust.noGravity = true;
                }

                if ((int)Projectile.localAI[0] % 4 == 0)
                    PADParticleHandler.SpawnParticle(new WindStreak(Projectile.Center + new Vector2(Main.rand.NextFloat(-50f, 50f), 30f), new Vector2(0f, -18f), 60f, 3f, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float fade = 1f - LegendsSkeletron.Ease((Projectile.localAI[0] - Life + 8) / 8f);
            Color bone = new Color(240, 230, 200);

            // The front: three arcs stacked, bowed up, and fainter ones where it has been.
            for (int i = 0; i < Projectile.oldPos.Length; i += 3)
            {
                if (Projectile.oldPos[i] == Vector2.Zero && i > 0)
                    continue;

                Vector2 at = (i == 0 ? Projectile.position : Projectile.oldPos[i]) + Projectile.Size * 0.5f;
                float strength = (1f - i / (float)Projectile.oldPos.Length) * fade;

                for (int k = 0; k < 3; k++)
                {
                    Vector2 arc = at + new Vector2(0f, -16f + k * 16f);
                    DrawArc(arc, 70f - k * 8f, bone * (strength * (0.7f - k * 0.18f)));
                }
            }

            SkeletronRig.Glow(Main.spriteBatch, Projectile.Center, bone * (0.25f * fade), 80f);
            return false;
        }

        /// <summary>An arc bowed upward, flattened: the top half of a ring squashed.</summary>
        private static void DrawArc(Vector2 centre, float radius, Color colour)
        {
            const int Pieces = 14;
            Vector2 last = centre + new Vector2(-radius, 0f);

            for (int i = 1; i <= Pieces; i++)
            {
                float a = MathHelper.Pi + MathHelper.Pi * i / Pieces;
                Vector2 next = centre + new Vector2((float)Math.Cos(a) * radius, (float)Math.Sin(a) * radius * 0.45f);
                SkeletronRig.Beam(Main.spriteBatch, last, next, colour, 10f);
                last = next;
            }
        }
    }

    // ================================================================================================================

    /// <summary>
    /// What hurts where a move says, drawing nothing of its own (the move draws it): his arms spun round him (the
    /// tornado), the flick's fingertip. ai[0] its head, ai[1] which (<see cref="LegendsSkeletron.HitArea"/>), ai[2] the
    /// move it belongs to; gone when that move is.
    /// </summary>
    public class SkeletronHitbox : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        internal const float ArmLeft = 0f;
        internal const float ArmRight = 1f;
        internal const float FlickTip = 2f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.hide = true;
        }

        public override void AI()
        {
            if (!SkeletronShots.Head(Projectile, out NPC head, out LegendsSkeletron state))
            {
                if (Projectile.owner == Main.myPlayer)
                    Projectile.Kill();

                return;
            }

            Projectile.velocity = Vector2.Zero;
            Projectile.Center = head.Center;

            // As long as its move (the tornado spins on past a second's life).
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 2);

            // Nothing stops the flick (the yellow of it is drawn on the hand by its head).
            if (Projectile.ai[1] == FlickTip)
            {
                Projectile.PAD().CCBypass = true;
                Projectile.PAD().CCQuiet = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!SkeletronShots.Head(Projectile, out NPC head, out LegendsSkeletron state))
                return false;

            return state.HitArea(head, (int)Projectile.ai[1], targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (SkeletronShots.Head(Projectile, out NPC head, out LegendsSkeletron state))
                state.AreaHit(head, (int)Projectile.ai[1], target);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    // ================================================================================================================

    /// <summary>A bone, shot out of a finger gun (his hands' Guns): straight and fast; it breaks on what it hits. ai[1] 1 in the red hat's look.</summary>
    public class BoneBullet : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.35f, 0.32f, 0.25f);
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 8; i++)
                Dust.NewDustPerfect(Projectile.Center, DustID.Bone, Main.rand.NextVector2Circular(3f, 3f) - Projectile.velocity * 0.1f, 0, default, 1f);

            SoundEngine.PlaySound(SkeletronSounds.Clack with { Volume = 0.35f, Pitch = 0.6f, MaxInstances = 5 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D bone = SkeletronRig.Bone(Projectile.ai[1] == 1f);

            if (bone == null)
                return false;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Color lit = SkeletronShots.Lit(Projectile.Center, 0.5f);

            SkeletronRig.Beam(Main.spriteBatch, Projectile.Center - way * 60f, Projectile.Center, new Color(255, 236, 190) * 0.35f, 8f);
            Main.spriteBatch.Draw(bone, Projectile.Center - Main.screenPosition, null, lit, Projectile.rotation, bone.Size() * 0.5f, 0.25f, SpriteEffects.None, 0f);
            return false;
        }
    }

    // ================================================================================================================

    /// <summary>
    /// A bone spike out of the ground (his hands' Snap): a crack first where it will come (ai[0] ticks), then up fast, held,
    /// and back down. Its foot is where it was made; ai[1] 1 in the red hat's look.
    /// </summary>
    public class BoneSpike : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        private const int Rise = 6;
        private const int Hold = 26;
        private const int Sink = 12;
        private const float Tall = 84f;
        private const float Wide = 22f;

        private int Age => (int)Projectile.localAI[0];
        private int Delay => (int)Projectile.ai[0];

        /// <summary>Its foot (the ground's top, at its middle): where it was made.</summary>
        private Vector2 Foot => new Vector2(Projectile.localAI[1], Projectile.localAI[2]);

        public override void SetDefaults()
        {
            Projectile.width = (int)Wide;
            Projectile.height = (int)Tall;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            Projectile.localAI[1] = Projectile.Center.X;
            Projectile.localAI[2] = Projectile.Center.Y;
        }

        /// <summary>How far out of the ground it is now.</summary>
        private float Height
        {
            get
            {
                int t = Age - Delay;

                if (t < 0)
                    return 0f;

                if (t < Rise)
                    return Tall * LegendsSkeletron.EaseOut(t / (float)Rise);

                if (t < Rise + Hold)
                    return Tall;

                return Tall * (1f - LegendsSkeletron.EaseIn((t - Rise - Hold) / (float)Sink));
            }
        }

        public override void AI()
        {
            if (Projectile.localAI[1] == 0f && Projectile.localAI[2] == 0f)
            {
                Projectile.localAI[1] = Projectile.Center.X;
                Projectile.localAI[2] = Projectile.Center.Y;
            }

            Projectile.localAI[0]++;
            Projectile.velocity = Vector2.Zero;

            // Kept where it stands, its box over its foot.
            Projectile.position = Foot - new Vector2(Wide * 0.5f, Tall);

            int t = Age - Delay;

            if (t > Rise + Hold + Sink && Projectile.owner == Main.myPlayer)
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            if (t < 0 && Age % 3 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Foot + new Vector2(Main.rand.NextFloat(-10f, 10f), -2f), DustID.Bone, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 2.5f)), 0, default, 1f);
                dust.noGravity = false;
            }

            if (t == 0)
            {
                SoundEngine.PlaySound(SkeletronSounds.SpikeRise, Foot);

                for (int i = 0; i < 10; i++)
                    Dust.NewDustPerfect(Foot, DustID.Bone, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 7f)), 0, default, Main.rand.NextFloat(1f, 1.4f));

                for (int i = 0; i < 4; i++)
                    PADParticleHandler.SpawnParticle(new StoneChip(Foot, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 6f)), GroundWave.FogColour((int)(Foot.X / 16f), (int)(Foot.Y / 16f) + 1), Main.rand.Next(30, 50)));
            }
        }

        public override bool CanHitPlayer(Player target) => Height > 10f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float h = Height;

            if (h <= 6f)
                return false;

            Rectangle body = new Rectangle((int)(Foot.X - Wide * 0.5f), (int)(Foot.Y - h), (int)Wide, (int)h);
            return body.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D spike = SkeletronRig.Spike(Projectile.ai[1] == 1f);

            if (spike == null)
                return false;

            Vector2 foot = Foot;
            float h = Height;

            // A crack where it will come.
            if (Age < Delay + Rise)
            {
                float warn = MathHelper.Clamp(Age / (float)Math.Max(1, Delay), 0f, 1f);
                SkeletronRig.Line(Main.spriteBatch, foot + new Vector2(-14f * warn, -1f), foot + new Vector2(14f * warn, -1f), new Color(30, 22, 16) * 0.9f, 2f);
                SkeletronRig.Glow(Main.spriteBatch, foot, new Color(255, 230, 190) * (0.25f * warn), 22f);
            }

            if (h < 1f)
                return false;

            // Out of the ground: only what is above it.
            int shown = Math.Min(spike.Height, (int)Math.Ceiling(h / Tall * spike.Height));
            Rectangle frame = new Rectangle(0, 0, spike.Width, shown);
            Color lit = SkeletronShots.Lit(foot + new Vector2(0f, -h * 0.5f));

            Main.spriteBatch.Draw(spike, foot + new Vector2(-spike.Width * 0.5f, -h) - Main.screenPosition, frame, lit, 0f, Vector2.Zero, new Vector2(1f, h / shown), SpriteEffects.None, 0f);
            return false;
        }
    }

    // ================================================================================================================

    /// <summary>The paper of his rock-paper-scissors: a wall of air a flat hand pushed, going the way it was pushed, throwing whoever it meets.</summary>
    public class PalmWave : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        /// <summary>Half its height: low enough to be jumped.</summary>
        private const int HalfHigh = 64;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 70;
            Projectile.height = HalfHigh * 2;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 52;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;

            if (Main.dedServ)
                return;

            float way = Math.Sign(Projectile.velocity.X);

            for (int i = 0; i < 2; i++)
            {
                Vector2 at = Projectile.Center + new Vector2(-way * Main.rand.NextFloat(0f, 30f), Main.rand.NextFloat(-HalfHigh + 8f, HalfHigh - 8f));
                PADParticleHandler.SpawnParticle(new WindStreak(at, Projectile.velocity * 1.2f, Main.rand.NextFloat(40f, 80f), 2.5f, 14));
            }

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + new Vector2(0f, Main.rand.NextFloat(-HalfHigh + 8f, HalfHigh - 8f)), DustID.Smoke, Projectile.velocity * 0.6f, 120, default, 1.3f);
                dust.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (target.whoAmI == Main.myPlayer)
                target.velocity = new Vector2(Math.Sign(Projectile.velocity.X) * 13f, -5f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float fade = Math.Min(1f, Projectile.timeLeft / 12f) * Math.Min(1f, Projectile.localAI[0] / 6f);
            float way = Math.Sign(Projectile.velocity.X);
            Color air = new Color(235, 230, 220);

            // A crescent bowed the way it goes, and two fainter behind it.
            for (int k = 0; k < 3; k++)
            {
                Vector2 centre = Projectile.Center - new Vector2(way * k * 22f, 0f);
                Vector2 last = centre + new Vector2(0f, -HalfHigh);

                for (int i = 1; i <= 12; i++)
                {
                    float y = -HalfHigh + HalfHigh * 2f * i / 12f;
                    float bow = 22f * (1f - (y / HalfHigh) * (y / HalfHigh));
                    Vector2 next = centre + new Vector2(way * bow, y);
                    SkeletronRig.Beam(Main.spriteBatch, last, next, air * (fade * (0.55f - k * 0.17f)), 9f - k * 2f);
                    last = next;
                }
            }

            return false;
        }
    }

    // ================================================================================================================

    /// <summary>
    /// The scissors of his rock-paper-scissors: an X cut where whoever it was played against stood: drawn faint first
    /// (ai[0] ticks), then it snips: two strokes crossed, for a moment.
    /// </summary>
    public class SnipCut : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        private const int Active = 8;
        private const float Half = 120f;

        private int Age => (int)Projectile.localAI[0];
        private int Delay => (int)Projectile.ai[0];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 300;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;
            Projectile.velocity = Vector2.Zero;

            if (Age == Delay && !Main.dedServ)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 along = new Vector2(1f, k).SafeNormalize(Vector2.UnitX);
                    PADParticleHandler.SpawnParticle(new SlashStreak(Projectile.Center, along, Half, 10f, 14));
                }

                LegendsSkeletron.Shake(Projectile.Center, 4f, 10);
            }

            if (Age > Delay + Active + 6 && Projectile.owner == Main.myPlayer)
                Projectile.Kill();
        }

        public override bool CanHitPlayer(Player target) => Age >= Delay && Age < Delay + Active;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                Vector2 along = new Vector2(1f, k).SafeNormalize(Vector2.UnitX) * Half;

                if (SkeletronShots.Touches(targetHitbox, Projectile.Center - along, Projectile.Center + along, 18f))
                    return true;
            }

            return false;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            bool cut = Age >= Delay;
            float warn = MathHelper.Clamp(Age / (float)Math.Max(1, Delay), 0f, 1f);
            float fade = cut ? 1f - MathHelper.Clamp((Age - Delay) / (float)(Active + 6), 0f, 1f) : 1f;
            Color edge = cut ? new Color(255, 250, 240) : Color.Lerp(new Color(255, 255, 255), new Color(255, 70, 60), warn);

            for (int k = -1; k <= 1; k += 2)
            {
                Vector2 along = new Vector2(1f, k).SafeNormalize(Vector2.UnitX) * Half;
                Vector2 a = Projectile.Center - along;
                Vector2 b = Projectile.Center + along;

                if (cut)
                {
                    SkeletronRig.Beam(Main.spriteBatch, a, b, edge * (0.9f * fade), 22f * fade + 4f);
                    SkeletronRig.Line(Main.spriteBatch, a, b, Color.White * fade, 3f);
                }
                else
                {
                    SkeletronRig.Line(Main.spriteBatch, a, b, edge * (0.25f + 0.5f * warn), 1f + 2f * warn);
                }
            }

            return false;
        }
    }

    // ================================================================================================================

    /// <summary>
    /// His breath of fire (his own Blaze, by request: "a purple-to-red flaming breath, max 50 tiles"): out of his mouth,
    /// along the way he breathes it (a puppet: its head's BreathLine), purple at his mouth going red at its end, wider as
    /// it goes. It sets on fire whoever it touches. ai[0] its head, ai[2] the move it belongs to.
    /// </summary>
    public class SkeletronBreath : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        /// <summary>By request: at most fifty tiles.</summary>
        internal const float Reach = 800f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)Reach + 200;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
        }

        private bool Line(out Vector2 from, out Vector2 way, out float length)
        {
            from = way = Vector2.Zero;
            length = 0f;

            if (!SkeletronShots.Head(Projectile, out NPC head, out LegendsSkeletron state))
                return false;

            return state.BreathLine(head, out from, out way, out length);
        }

        public override void AI()
        {
            if (!Line(out Vector2 from, out Vector2 way, out float length))
            {
                if (Projectile.owner == Main.myPlayer)
                    Projectile.Kill();

                return;
            }

            Projectile.velocity = Vector2.Zero;
            Projectile.Center = from + way * (length * 0.5f);
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 10);

            // Not out of his mouth yet (its first tick): nothing to light, burn or bend.
            if (length < 4f)
                return;

            for (float s = 0f; s < length; s += 80f)
                Lighting.AddLight(from + way * s, Color.Lerp(LegendsSkeletron.BlazePurple, LegendsSkeletron.BlazeRed, s / Reach).ToVector3() * 0.9f);

            if (Main.dedServ)
                return;

            // Flames off it, the colour where they leave it; and the air shimmering along it.
            for (int i = 0; i < 4; i++)
            {
                float s = Main.rand.NextFloat(length);
                float wide = Width(s);
                Vector2 at = from + way * s + new Vector2(-way.Y, way.X) * Main.rand.NextFloat(-wide, wide) * 0.6f;
                Color tint = Color.Lerp(LegendsSkeletron.BlazePurple, LegendsSkeletron.BlazeRed, s / Reach);
                Vector2 speed = way * Main.rand.NextFloat(4f, 9f) + new Vector2(0f, -Main.rand.NextFloat(0.5f, 2f));

                PADParticleHandler.SpawnParticle(new FlameSmoke(at, speed, tint, Main.rand.Next(22, 38), wide * 0.5f, wide * 1.4f, 0.55f));

                if (Main.rand.NextBool(2))
                    PADParticleHandler.SpawnParticle(new Ember(at, speed * 1.2f + Main.rand.NextVector2Circular(2f, 2f), tint, Main.rand.NextFloat(0.6f, 1.1f), Main.rand.Next(24, 44)));
            }

            NPCs.Empress.EmpressWarp.Want();
            NPCs.Empress.EmpressWarp.Haze(from, from + way * length, 60f, 3f, 80f, Main.GlobalTimeWrappedHourly * 3f, 12f);
        }

        /// <summary>How wide it is this far from his mouth.</summary>
        private static float Width(float along) => MathHelper.Lerp(16f, 56f, MathHelper.Clamp(along / Reach, 0f, 1f));

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Line(out Vector2 from, out Vector2 way, out float length) || length < 4f)
                return false;

            for (float s = 0f; s <= length; s += 20f)
            {
                if (SkeletronShots.Touches(targetHitbox, from + way * s, Width(s) * 0.8f))
                    return true;
            }

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.OnFire, 180);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!Line(out Vector2 from, out Vector2 way, out float length) || length < 4f)
                return false;

            float wobble = Main.GlobalTimeWrappedHourly * 14f;

            for (float s = 0f; s <= length; s += 14f)
            {
                float p = s / Reach;
                Color tint = Color.Lerp(LegendsSkeletron.BlazePurple, LegendsSkeletron.BlazeRed, p);
                float wide = Width(s) * (0.9f + 0.15f * (float)Math.Sin(wobble + s * 0.05f));
                float end = 1f - LegendsSkeletron.Ease((s - length + 80f) / 80f);

                SkeletronRig.Glow(Main.spriteBatch, from + way * s, tint * (0.32f * end), wide * 1.5f);

                if (p < 0.45f)
                    SkeletronRig.Hot(Main.spriteBatch, from + way * s, Color.Lerp(Color.White, tint, 0.4f + p) * (0.35f * end), wide * 0.55f);
            }

            return false;
        }
    }

    // ================================================================================================================

    /// <summary>
    /// His jaw, thrown (his own Jaw, by request: "spin in place and throw the jaw like a boomerang"): out to past where
    /// they were, and back to him, turning (a puppet: its head's JawSpot). ai[0] its head, ai[2] the move it belongs to.
    /// </summary>
    public class JawBoomerang : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        /// <summary>The jaw's own middle on its canvas (the skull's 80 by 102, the jaw at the bottom of it).</summary>
        private static readonly Vector2 JawMiddle = new Vector2(40f, 78f);

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            if (!SkeletronShots.Head(Projectile, out NPC head, out LegendsSkeletron state) || !state.JawSpot(head, out Vector2 at, out float turn))
            {
                if (Projectile.owner == Main.myPlayer)
                    Projectile.Kill();

                return;
            }

            Projectile.velocity = Vector2.Zero;
            Projectile.Center = at;
            Projectile.rotation = turn;
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 10);

            if (!Main.dedServ && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(24f, 24f), DustID.Bone, Vector2.Zero, 100, default, 1f);
                dust.noGravity = true;
            }
        }

        /// <summary>Out, on this machine: its head still in the move that threw it, and the jaw not yet caught.</summary>
        private bool Out() => SkeletronShots.Head(Projectile, out NPC head, out LegendsSkeletron state) && state.JawSpot(head, out _, out _);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Out() && SkeletronShots.Touches(targetHitbox, Projectile.Center, 34f);

        public override bool PreDraw(ref Color lightColor)
        {
            if (!Out())
                return false;

            bool red = SkeletronShots.Head(Projectile, out _, out LegendsSkeletron state) && state.redHat;
            Texture2D jaw = SkeletronRig.Jaw(red);

            if (jaw == null)
                return false;

            Color lit = SkeletronShots.Lit(Projectile.Center);

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(jaw, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null, lit * (0.3f * fade), Projectile.oldRot[i], JawMiddle, 1f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(jaw, Projectile.Center - Main.screenPosition, null, lit, Projectile.rotation, JawMiddle, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
