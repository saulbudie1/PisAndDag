using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Everhunger. A mouth of blood let out of the caster's chest, level, toward the
    /// side the cursor is on.
    ///
    /// The first thing it bites is the one it wants. From then on it does not go
    /// anywhere else: it goes on through, swings wide, and comes back round into the
    /// same body, once every <see cref="Loop"/> ticks, each time a little smaller, up to
    /// <see cref="MaxBites"/> bites. Only a bite on THAT one starts the next loop.
    /// Anything else in the way is bitten in passing for two thirds, and does not
    /// change where it is going. If what it wants dies first, it goes with it.
    ///
    /// The loop is a path, not steering, so it is the same shape every time and always
    /// arrives: on through the body the way it was going, round a circle on whichever
    /// side is up, and straight back in to the target's centre, wherever the target
    /// has got to. Each loop leaves the way the last came in, so round a target that
    /// stands still the loops lay out like petals.
    ///
    /// State: ai[0] the target's index plus one (0 none), ai[1] ticks into the loop,
    /// ai[2] bites. The loop's start and heading and which side it swings to go in
    /// SendExtraAI. The bite's blood and shake are played where each client sees the
    /// bite count go up, so everyone sees them, not only the caster.
    ///
    /// The glow is the Court's: eight copies in its red with no alpha, under the sprite.
    /// </summary>
    public class EverHungerHead : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Projectiles/Magic/EverHungerHead";

        // ---- numbers ---------------------------------------------------------

        /// <summary>Four frames down the sheet, one full run every this many ticks, in equal parts.</summary>
        public const int Frames = 4;
        public const int FrameCycle = 45;

        /// <summary>Speed out, in pixels a tick, and how long it goes before it gives up if it bites nothing.</summary>
        public const float Speed = 12f;
        public const int Reach = 90;

        /// <summary>A little rise and fall on the way out, in pixels and ticks, so it swims rather than slides.</summary>
        private const float SwayHeight = 5f;
        private const float SwayPeriod = 44f;

        /// <summary>Ticks from one bite to the next on the same body: the whole loop.</summary>
        public const int Loop = 60;

        /// <summary>
        /// The loop's shape: on through the target this far, round a circle this big,
        /// and back in. About six hundred pixels of path, so ten a tick on average.
        /// The turn is drawn in steps of LoopStep radians.
        /// </summary>
        private const float LoopPass = 70f;
        private const float LoopRadius = 85f;
        private const float LoopStep = 0.03f;

        /// <summary>Each loop is rolled: the pass and the circle this much either side of those, and one in this many turns the other way, downward.</summary>
        private const float LoopPassVary = 0.3f;
        private const float LoopRadiusVary = 0.2f;
        private const int LoopDownChance = 4;

        /// <summary>How much quicker it is through the body than over the top: 1.3 times and 0.7 times the average.</summary>
        private const float LoopEase = 0.3f;

        /// <summary>Bites on its target before it is spent.</summary>
        public const int MaxBites = 5;

        /// <summary>Scale lost per bite. 1, 0.87, 0.74, 0.61, 0.48.</summary>
        private const float ShrinkPerBite = 0.13f;

        /// <summary>Damage to anything that is not its target.</summary>
        public const float PassingDamage = 2f / 3f;

        /// <summary>The last bite on its target, the one that spends it, is this much.</summary>
        public const float LastBiteDamage = 2f;

        /// <summary>The sprite is 68 by 80; it is drawn at this, and the hitbox is this square, before shrinking.</summary>
        private const float DrawScale = 0.62f;
        private const int HitSize = 34;

        /// <summary>A loop that goes round this many times without landing and it gives up.</summary>
        private const int MissedLoops = 3;

        /// <summary>The bite's shake: strength in pixels, cycles a second, ticks.</summary>
        private const float BiteShake = 3.5f;
        private const float BiteShakeRate = 9f;
        private const int BiteShakeTicks = 12;

        public static readonly Color Glow = CourtHand.Glow;
        public static readonly Color BloodDark = CourtHand.BloodDark;
        public static readonly Color BloodBright = CourtHand.BloodBright;

        // ---- state -----------------------------------------------------------

        private int Target => (int)Projectile.ai[0] - 1;
        private bool Locked => Projectile.ai[0] > 0f;
        private int LoopTick { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        private int Bites { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        /// <summary>
        /// The loop: where it started, in the loop's own frame about the target; the
        /// way it was going; the side it turns to (+1 or -1 about the heading); loops
        /// gone round without landing; this loop's pass and radius. Synced. The path is
        /// built from these, locally.
        /// </summary>
        private Vector2 loopFrom;
        private Vector2 loopHeading;
        private int loopSide;
        private int missed;
        private float loopPass = LoopPass;
        private float loopRadius = LoopRadius;

        private System.Collections.Generic.List<Vector2> loopPath;
        private float loopLength;

        /// <summary>What was locked: its type too, so a slot handed to something else after a death does not keep it going.</summary>
        private int targetType;

        /// <summary>Local: the bites this client has already bled for, the way it faces (eased), its size (eased), its age.</summary>
        private int bitesSeen;
        private float facing;
        private float size = 1f;
        private int age;
        private bool spilled;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>The size it is heading for: one step smaller for every bite.</summary>
        private float WantedSize => 1f - ShrinkPerBite * Math.Min(Bites, MaxBites - 1);

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = Frames;
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = HitSize;
            Projectile.height = HitSize;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Reach;

            // Its own clock per body: long enough that going through something does not
            // bite it twice on the way in and out, short enough that the loop back in
            // always lands.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(loopFrom);
            writer.WriteVector2(loopHeading);
            writer.Write((sbyte)loopSide);
            writer.Write((byte)missed);
            writer.Write(loopPass);
            writer.Write(loopRadius);
            writer.Write(targetType);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            loopFrom = reader.ReadVector2();
            loopHeading = reader.ReadVector2();
            loopSide = reader.ReadSByte();
            missed = reader.ReadByte();
            loopPass = reader.ReadSingle();
            loopRadius = reader.ReadSingle();
            targetType = reader.ReadInt32();

            if (loopSide != 0)
                BuildLoop();
        }

        // ---- AI --------------------------------------------------------------

        public override void AI()
        {
            age++;

            if (!spilled)
            {
                spilled = true;
                facing = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                if (!Main.dedServ)
                    Spill();
            }

            // A bite this client has not bled for yet. First, so the last bite is
            // seen before it goes.
            if (Bites > bitesSeen)
            {
                bitesSeen = Bites;

                if (!Main.dedServ)
                    Chomp();
            }

            if (Bites >= MaxBites)
            {
                if (Projectile.owner == Main.myPlayer)
                    Projectile.Kill();

                Projectile.velocity *= 0.5f;
                return;
            }

            if (Locked)
            {
                NPC target = Main.npc[Target];

                // What it wanted is gone. So is it.
                if (!target.active || target.life <= 0 || target.type != targetType)
                {
                    Projectile.Kill();
                    return;
                }

                Projectile.timeLeft = 2;
                Circle(target);
            }
            else
            {
                Out();
            }

            size = MathHelper.Lerp(size, WantedSize, 0.15f);

            int side = Math.Max(8, (int)(HitSize * size));

            if (side != Projectile.width)
                Projectile.Resize(side, side);

            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                float want = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                facing += MathHelper.WrapAngle(want - facing) * 0.35f;
            }

            Projectile.rotation = facing;
            Projectile.frame = age % FrameCycle * Frames / FrameCycle;

            float light = 0.55f * size * Fade;
            Lighting.AddLight(Projectile.Center, light, light * 0.08f, light * 0.08f);

            if (!Main.dedServ)
                Fume();
        }

        /// <summary>Unlocked: straight out, level, with a small swim in it.</summary>
        private void Out()
        {
            float dir = Math.Sign(Projectile.velocity.X);

            if (dir == 0f)
                dir = Owner.direction;

            // The sway as a velocity, so the hitbox sweeps honestly: the derivative of
            // a sine of height SwayHeight.
            float w = MathHelper.TwoPi / SwayPeriod;
            float rise = SwayHeight * w * (float)Math.Cos(age * w);

            Projectile.velocity = new Vector2(dir * Speed, rise);
        }

        /// <summary>
        /// Locked: along the loop, in the target's own frame, so wherever the target
        /// goes the loop goes with it and always ends in its centre. Written as a
        /// velocity to the next point, so the engine does the moving and the hit test
        /// sweeps.
        ///
        /// Time along the loop is eased so it is quick through the body and slow over
        /// the top of the turn, like something thrown up that comes back down.
        /// </summary>
        private void Circle(NPC target)
        {
            if (loopPath == null)
                BuildLoop();

            LoopTick++;

            if (LoopTick > Loop)
            {
                // Came round and did not land (it was immune, or the target got out of
                // the way). Round again from here, without a bite, and not for ever.
                if (++missed >= MissedLoops)
                {
                    Projectile.Kill();
                    return;
                }

                LoopTick = 1;
                StartLoop(target, Projectile.velocity);
            }

            float u = LoopTick / (float)Loop;
            float along = u + LoopEase * (float)Math.Sin(MathHelper.TwoPi * u) / MathHelper.TwoPi;

            Vector2 local = LoopPoint(along * loopLength);
            Vector2 side = new Vector2(-loopHeading.Y, loopHeading.X) * loopSide;
            Vector2 next = target.Center + loopHeading * local.X + side * local.Y;

            Projectile.velocity = next - Projectile.Center;
        }

        /// <summary>
        /// A new loop from here, carrying on the way it is going. Kept as where it
        /// starts relative to the target, the heading, and the side it turns to:
        /// usually the side that is more up, so it rises over what it is eating rather
        /// than burrowing under it, and one time in four the other. The pass and the
        /// circle are rolled too, so the loops never repeat and never stray far.
        /// </summary>
        private void StartLoop(NPC target, Vector2 going)
        {
            loopHeading = going.SafeNormalize(new Vector2(Owner.direction, 0f));

            // Rolled here, by the owner, and sent: no two loops the same.
            Vector2 normal = new Vector2(-loopHeading.Y, loopHeading.X);
            loopSide = normal.Y <= 0f ? 1 : -1;

            if (Main.rand.NextBool(LoopDownChance))
                loopSide = -loopSide;

            loopPass = LoopPass * Main.rand.NextFloat(1f - LoopPassVary, 1f + LoopPassVary);
            loopRadius = LoopRadius * Main.rand.NextFloat(1f - LoopRadiusVary, 1f + LoopRadiusVary);

            Vector2 side = normal * loopSide;
            Vector2 offset = Projectile.Center - target.Center;

            loopFrom = new Vector2(Vector2.Dot(offset, loopHeading), Vector2.Dot(offset, side));
            BuildLoop();

            Projectile.netUpdate = true;
        }

        /// <summary>
        /// The loop's shape, in its own frame: x along the heading, y toward the side it
        /// turns to, the target at the origin. Straight on from where it bit, through
        /// the body and <see cref="LoopPass"/> beyond it; round a circle of
        /// <see cref="LoopRadius"/> until it is facing the target again; and straight
        /// back in to the centre. Rebuilt from the synced three numbers on every client,
        /// so everyone draws the same loop.
        /// </summary>
        private void BuildLoop()
        {
            loopPath ??= new System.Collections.Generic.List<Vector2>(200);
            loopPath.Clear();

            Vector2 from = loopFrom;
            loopPath.Add(from);

            // On through, the sideways offset of the bite worked out as it goes.
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f;
                loopPath.Add(new Vector2(MathHelper.Lerp(from.X, loopPass, t), from.Y * (1f - t) * (1f - t)));
            }

            // Round, until the way it faces is the way to the target.
            Vector2 centre = new Vector2(loopPass, loopRadius);
            Vector2 last = loopPath[loopPath.Count - 1];

            for (float turn = LoopStep; turn < MathHelper.TwoPi; turn += LoopStep)
            {
                last = centre + new Vector2((float)Math.Sin(turn), -(float)Math.Cos(turn)) * loopRadius;
                loopPath.Add(last);

                Vector2 heading = new Vector2((float)Math.Cos(turn), (float)Math.Sin(turn));
                Vector2 home = -last;

                if (turn > MathHelper.PiOver2 && heading.X * home.Y - heading.Y * home.X <= 0f && Vector2.Dot(heading, home) > 0f)
                    break;
            }

            // And straight back in.
            int steps = Math.Max(2, (int)(last.Length() / 8f));

            for (int i = 1; i <= steps; i++)
                loopPath.Add(last * (1f - i / (float)steps));

            loopLength = 0f;

            for (int i = 1; i < loopPath.Count; i++)
                loopLength += Vector2.Distance(loopPath[i - 1], loopPath[i]);
        }

        /// <summary>The point this far along the loop's path, in its frame.</summary>
        private Vector2 LoopPoint(float distance)
        {
            if (distance <= 0f)
                return loopPath[0];

            for (int i = 1; i < loopPath.Count; i++)
            {
                float step = Vector2.Distance(loopPath[i - 1], loopPath[i]);

                if (distance <= step)
                    return Vector2.Lerp(loopPath[i - 1], loopPath[i], step > 0.0001f ? distance / step : 0f);

                distance -= step;
            }

            return loopPath[loopPath.Count - 1];
        }

        // ---- hitting ---------------------------------------------------------

        public override bool? CanHitNPC(NPC target)
        {
            // Nothing in the last moment of fading out.
            return Fade < 0.3f ? false : null;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!Locked)
                return;

            if (target.whoAmI != Target)
                modifiers.SourceDamage *= PassingDamage;
            else if (Bites == MaxBites - 1)
                modifiers.SourceDamage *= LastBiteDamage;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!Locked)
            {
                // The first bite picks it.
                Projectile.ai[0] = target.whoAmI + 1;
                targetType = target.type;
            }
            else if (target.whoAmI != Target)
            {
                // In passing. It keeps going where it was going.
                PassingBlood(target);
                return;
            }

            Bites++;
            LoopTick = 0;
            missed = 0;

            // The last one: it is spent, and goes on the next tick, once the bite has
            // been seen.
            if (Bites >= MaxBites)
            {
                Projectile.netUpdate = true;
                return;
            }

            StartLoop(target, Projectile.velocity);
        }

        /// <summary>How visible it is: it fades in over the first ticks, and out at the end of an unlocked run.</summary>
        private float Fade
        {
            get
            {
                float fadeIn = MathHelper.Clamp(age / 6f, 0f, 1f);
                float fadeOut = Locked ? 1f : MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
                return fadeIn * fadeOut;
            }
        }

        // ---- effects ---------------------------------------------------------

        /// <summary>Where the back of the head is: the fumes come off here.</summary>
        private Vector2 Neck => Projectile.Center - (facing - MathHelper.PiOver2).ToRotationVector2() * (30f * DrawScale * size);

        /// <summary>The cast: the caster's chest opens and blood goes out of it every way, most of it the way the mouth went.</summary>
        private void Spill()
        {
            Player owner = Owner;
            Vector2 chest = owner.MountedCenter + new Vector2(0f, -4f * owner.gravDir);
            float dir = Math.Sign(Projectile.velocity.X);

            if (dir == 0f)
                dir = owner.direction;

            for (int i = 0; i < 22; i++)
            {
                Vector2 way = Main.rand.NextVector2CircularEdge(1f, 1f);

                // Pulled toward the mouth's way, so it reads as coming out after it.
                way = Vector2.Normalize(way + new Vector2(dir * 0.9f, -0.2f));

                PADParticleHandler.SpawnParticle(new BloodDroplet(chest + Main.rand.NextVector2Circular(5f, 7f),
                    way * Main.rand.NextFloat(1.5f, 5.5f), Main.rand.Next(30, 52), Main.rand.NextFloat(0.6f, 1.05f),
                    Main.rand.NextBool() ? BloodDark : BloodBright));
            }

            for (int i = 0; i < 5; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodFume(chest + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(dir * Main.rand.NextFloat(0.4f, 1.4f), -Main.rand.NextFloat(0.2f, 0.9f)),
                    Main.rand.NextFloat(0.1f, 0.16f), Main.rand.Next(30, 50)));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(chest, Vector2.Zero, CourtOfHandsRite.FumeMid, 0.28f, 18));

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.4f, Volume = 0.7f }, chest);
        }

        /// <summary>Off the back of the head as it goes: the Court's vapour, trailing, and now and then a drop.</summary>
        private void Fume()
        {
            float fade = Fade;

            if (fade <= 0.05f)
                return;

            Vector2 back = (facing + MathHelper.PiOver2).ToRotationVector2();
            Vector2 neck = Neck;
            int count = Main.rand.NextBool(3) ? 2 : 1;

            for (int i = 0; i < count; i++)
            {
                Vector2 at = neck + Main.rand.NextVector2Circular(7f, 7f) * size;
                Vector2 vel = back * Main.rand.NextFloat(0.3f, 1.1f) + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.2f, 0.8f));

                PADParticleHandler.SpawnParticle(new BloodFume(at, vel, Main.rand.NextFloat(0.09f, 0.15f) * size, Main.rand.Next(28, 46)));
            }

            if (age % 4 == 0)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(neck, back * 0.4f, CourtOfHandsRite.FumeMid,
                    Main.rand.NextFloat(0.12f, 0.18f) * size, 14, lights: false));
            }

            if (Main.rand.NextBool(5))
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(neck + Main.rand.NextVector2Circular(4f, 4f),
                    back * Main.rand.NextFloat(0.5f, 1.5f) + new Vector2(0f, 0.5f), Main.rand.Next(26, 40),
                    Main.rand.NextFloat(0.5f, 0.8f) * size, Main.rand.NextBool() ? BloodDark : BloodBright));
            }
        }

        /// <summary>A bite on its target: blood through and out the far side, a spray back the way it came, and a small shake.</summary>
        private void Chomp()
        {
            Vector2 at = Projectile.Center + (facing - MathHelper.PiOver2).ToRotationVector2() * (20f * DrawScale * size);
            Vector2 through = (facing - MathHelper.PiOver2).ToRotationVector2();

            for (int i = 0; i < 16; i++)
            {
                Vector2 vel = through.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 7f) + new Vector2(0f, -1.5f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(at + Main.rand.NextVector2Circular(6f, 6f), vel,
                    Main.rand.Next(36, 60), Main.rand.NextFloat(0.7f, 1.2f), Main.rand.NextBool() ? BloodDark : BloodBright));
            }

            for (int i = 0; i < 7; i++)
            {
                Vector2 vel = -through.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 4f) + new Vector2(0f, -1f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(at + Main.rand.NextVector2Circular(6f, 6f), vel,
                    Main.rand.Next(30, 48), Main.rand.NextFloat(0.6f, 1f), Main.rand.NextBool() ? BloodDark : BloodBright));
            }

            for (int i = 0; i < 6; i++)
            {
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(8f, 8f), DustID.Blood,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = false;
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, CourtOfHandsRite.FumeBright, 0.3f * size + 0.1f, 16));

            Main.instance.CameraModifiers.Add(new PunchCameraModifier(at, through, BiteShake, BiteShakeRate, BiteShakeTicks, 1400f, "EverHunger"));

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.25f + 0.1f * Bites, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.Item2 with { Pitch = -0.3f, Volume = 0.8f }, at);
        }

        /// <summary>A body bitten in passing: less, and no shake.</summary>
        private void PassingBlood(NPC target)
        {
            if (Main.dedServ)
                return;

            Vector2 through = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < 6; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    through.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4.5f) + new Vector2(0f, -1f),
                    Main.rand.Next(30, 48), Main.rand.NextFloat(0.6f, 0.95f), Main.rand.NextBool() ? BloodDark : BloodBright));
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            // Spent: the last of it comes apart where it is. A run that bit nothing
            // just thins out.
            bool spent = Locked;
            Vector2 at = Projectile.Center;
            int drops = spent ? 18 : 6;

            for (int i = 0; i < drops; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(at + Main.rand.NextVector2Circular(8f, 8f) * size,
                    Main.rand.NextVector2Circular(3.5f, 3.5f) + new Vector2(0f, -1.5f), Main.rand.Next(30, 50),
                    Main.rand.NextFloat(0.6f, 1f) * Math.Max(0.6f, size), Main.rand.NextBool() ? BloodDark : BloodBright));
            }

            for (int i = 0; i < (spent ? 8 : 4); i++)
            {
                PADParticleHandler.SpawnParticle(new BloodFume(at + Main.rand.NextVector2Circular(10f, 10f) * size,
                    Main.rand.NextVector2Circular(0.8f, 0.8f) + new Vector2(0f, -0.5f), Main.rand.NextFloat(0.1f, 0.18f), Main.rand.Next(34, 56)));
            }

            if (spent)
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.2f, Volume = 0.6f }, at);
        }

        // ---- draw ------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame(1, Frames, 0, Projectile.frame);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 at = Projectile.Center - Main.screenPosition;
            float scale = DrawScale * size;
            float fade = Fade;

            if (fade <= 0f)
                return false;

            // Under everything: a soft red light at the neck, where the vapour comes
            // from, so the head sits in its own haze.
            Texture2D bloom = ModContent.Request<Texture2D>("PisAndDag/Content/Particles/BloomGeneric").Value;
            Color haze = CourtOfHandsRite.FumeDark * (0.8f * fade);
            haze.A = 0;
            Main.EntitySpriteDraw(bloom, Neck - Main.screenPosition, null, haze, facing, bloom.Size() * 0.5f,
                new Vector2(0.22f, 0.3f) * size, SpriteEffects.None, 0);

            // The wake: where it was, in its glow, thinning.
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color ghost = Glow * (0.28f * t * fade);
                ghost.A = 0;

                Vector2 past = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, past, frame, ghost, Projectile.oldRot[i], origin, scale * (0.85f + 0.15f * t), SpriteEffects.None, 0);
            }

            // The Court's glow: eight copies three pixels out, no alpha, under the sprite.
            Color glow = Glow * (0.5f * fade);
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 3f;
                Main.EntitySpriteDraw(texture, at + offset, frame, glow, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            }

            // It is lit by the room, but never darker than its own glow lets it be.
            Color body = Color.Lerp(lightColor, Color.White, 0.45f) * fade;
            Main.EntitySpriteDraw(texture, at, frame, body, Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
