using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.NPCs.Abomanation_Small;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Where things are on the Flesh Maiden's sprite (FleshMaidenSpearDiagonal.png, on the
    /// diagonal in frames of 128 by 128 texels, 168 from butt to point), as VisusGrip reads points
    /// on a polearm: along from the butt, and across from the shaft, + toward the blade (below it
    /// when it is held level, unless it is turned over). And the ground test both its swing down
    /// and its smash stop on.
    /// </summary>
    public static class FleshMaidenGrip
    {
        /// <summary>The very point.</summary>
        public static readonly Vector2 Point = new Vector2(166f, -1f);

        /// <summary>The middle of the eye (Visus's, in the meat).</summary>
        public static readonly Vector2 Eye = new Vector2(116f, 0f);

        /// <summary>The middle of the blade's edge, the deepest part of the crescent.</summary>
        public static readonly Vector2 Edge = new Vector2(116f, 22f);

        /// <summary>The nail on the end of the finger bent back above the head.</summary>
        public static readonly Vector2 Hook = new Vector2(102f, -21f);

        /// <summary>Where the head begins, along the sprite: the collar of meat.</summary>
        public const float HeadFrom = 92f;

        /// <summary>How thick the head is to what it hits, and the shaft behind it, in pixels.</summary>
        public const float HeadWidth = 40f;
        public const float ShaftWidth = 16f;

        /// <summary>How far an edge bites into what it lands on before it stops, in pixels.</summary>
        public const float Bite = 6f;

        /// <summary>A point on it, <paramref name="across"/> taken to the other side when it is turned over.</summary>
        public static Vector2 On(Player player, float angle, Vector2 hand, float grip, Vector2 point, bool turned) =>
            VisusGrip.On(player, angle, hand, grip, point.X, turned ? -point.Y : point.Y);

        /// <summary>
        /// Whether the head at <paramref name="angle"/> is in the ground: its lower edge (the
        /// blade, or the finger when it is turned over), less the bite it takes, or its point.
        /// </summary>
        public static bool InGround(Player player, float angle, Player.CompositeArmStretchAmount stretch, float grip, bool turned,
            out Vector2 contact)
        {
            Vector2 hand = VisusGrip.FrontHand(stretch, angle);
            Vector2 low = turned ? new Vector2(Hook.X, -Hook.Y) : Edge;

            contact = VisusGrip.On(player, angle, hand, grip, low.X, low.Y - Bite);
            if (Collision.SolidCollision(contact - new Vector2(3f), 6, 6, true))
                return true;

            contact = VisusGrip.On(player, angle, hand, grip, Point.X, Point.Y);
            return Collision.SolidCollision(contact - new Vector2(3f), 6, 6, true);
        }

        /// <summary>
        /// Between <paramref name="clear"/> (out of the ground) and <paramref name="buried"/> (in
        /// it), the angle it meets the ground at, halved eight times.
        /// </summary>
        public static float Meet(Player player, float clear, float buried, Player.CompositeArmStretchAmount stretch, float grip, bool turned)
        {
            float lo = clear, hi = buried;

            for (int i = 0; i < 8; i++)
            {
                float mid = (lo + hi) * 0.5f;

                if (InGround(player, mid, stretch, grip, turned, out _))
                    hi = mid;
                else
                    lo = mid;
            }

            return hi;
        }

        /// <summary>Whether a target is struck by it at <paramref name="angle"/>: along the shaft past the hand, and the whole head.</summary>
        public static bool Hits(Player player, Rectangle target, float angle, Vector2 hand, float grip)
        {
            Vector2 u = angle.ToRotationVector2();
            Vector2 shaft = VisusGrip.ToWorld(player, hand + u * 12f);
            Vector2 head = VisusGrip.ToWorld(player, hand + u * Math.Max(12f, HeadFrom - grip));
            Vector2 tip = VisusGrip.ToWorld(player, hand + u * (Point.X - grip));
            float collision = 0f;

            if (HeadFrom - grip > 12f
                && Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), shaft, head, ShaftWidth, ref collision))
                return true;

            return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), head, tip, HeadWidth, ref collision);
        }

        /// <summary>Grass and pots along it, as vanilla's spears cut them.</summary>
        public static void Cut(Player player, float angle, Vector2 hand, float grip)
        {
            Vector2 u = angle.ToRotationVector2();

            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(VisusGrip.ToWorld(player, hand + u * 12f), VisusGrip.ToWorld(player, hand + u * (Point.X - grip)),
                HeadWidth, DelegateMethods.CutTiles);
        }

        // ---- its colours ------------------------------------------------------------------------

        /// <summary>Amalgaermis's own blood: what it is grown from bleeds the same.</summary>
        public static Color Blood => Abomanation.Blood;

        public static readonly Color Meat = new Color(184, 44, 56);
        public static readonly Color DarkMeat = new Color(70, 10, 22);
        public static readonly Color Bone = new Color(236, 222, 196);
        public static readonly Color Cold = new Color(185, 200, 255);

        public static readonly Color[] Spatter = { new Color(150, 8, 12), new Color(184, 44, 56), new Color(110, 6, 16), new Color(236, 222, 196) };

        /// <summary>
        /// Blood off whatever it hit, thrown away from its holder, as much as the hit was hard
        /// (Amalgaermis's own measure, as Joint Arms of the Defiled throws it).
        /// </summary>
        public static void Bleed(Vector2 at, Vector2 away, int damage)
        {
            if (Main.dedServ)
                return;

            float force = MathHelper.Clamp(damage / Abomanation.BloodReference, Abomanation.BloodLeast, Abomanation.BloodMost);
            int count = (int)(Abomanation.SlashDroplets * force);

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = away.RotatedBy(Main.rand.NextFloat(-Abomanation.SlashFan, Abomanation.SlashFan))
                    * Abomanation.SlashSpeed * force * Main.rand.NextFloat(0.6f, 1.25f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(at, velocity, Main.rand.Next(26, 48),
                    Main.rand.NextFloat(0.55f, 1f), Blood));
            }
        }
    }

    /// <summary>
    /// The Flesh Maiden's combo (left click), one of these a step; ai[0] which step (0 a swing
    /// down, 1 a swing up, 2 a thrust), ai[1] how long it lasts in ticks (the item's use time,
    /// 40, less with melee speed). The shape is Calamity's Biting Embrace (Biome Blade): a swing,
    /// a swing back, then a thrust, one each use, picked by a counter on the wielder. This is its
    /// own code.
    ///
    /// THE SWINGS go round through an arc of about 90 degrees laid about the aim (held within 46
    /// degrees of level): a short wind-up, then out of it at full speed and slowing all the way
    /// (a curve that starts steep), then a settle back. The swing down leads with the blade.
    /// The swing up is the halberd turned over on its shaft, so the blade leads that too. The
    /// swing down stops where the ground stops it, as Visus's blow does; the swing up starts
    /// from the ground if the ground is where its wind-up would have put it, and tears up out
    /// of it. They hit along the shaft and the whole head over the sweep since last tick, and
    /// the first thing each hits holds it still for three ticks.
    ///
    /// THE THRUST is Visus's, carried out to 10 tiles, and without the step: it never moves its
    /// user. The hands slide the shaft out through the grip, 160 pixels from the player's middle
    /// to the point when it is level.
    ///
    /// Drawn in the hands by VisusLayer (PolearmArt.FleshMaiden); this draws what it leaves
    /// behind, in PreDraw, behind the player: the swings a smear of dark and meat with a bone
    /// edge, the thrust a streak. Blood off everything it hits.
    /// </summary>
    public class FleshMaidenStrike : ModProjectile
    {
        public override string Texture => PolearmArt.FleshMaiden.SheetPath;

        public const int Down = 0, Up = 1, Thrust = 2;

        // ---- the swings ----------------------------------------------------------------------------

        /// <summary>Wound up by here; the sweep from here to SweepTo; then it settles.</summary>
        private const float WindTo = 0.20f, SweepTo = 0.58f;

        /// <summary>The swings hit from here to here.</summary>
        private const float SwingHitsFrom = 0.20f, SwingHitsTo = 0.60f;

        /// <summary>Held near the butt for the swings: the head goes round 130 pixels out from the hand.</summary>
        private const float SwingGrip = 34f;

        /// <summary>The aim the swings are laid about, no further from level than this.</summary>
        private const float AimLimit = 0.8f;

        /// <summary>Ticks the first thing a swing hits holds it still.</summary>
        private const int Stall = 3;

        /// <summary>How much of the swing the smear trails behind the head.</summary>
        private const float Trail = 0.12f;

        /// <summary>Degrees from the aim, down positive: raised to 48 above it, brought down through 92, and back a little.</summary>
        private static readonly Piece[] SwingDown =
        {
            new Piece(Ease.SineInOut, 0f, -34f, -14f),
            new Piece(Ease.PolyOut, WindTo, -48f, 92f, 3),
            new Piece(Ease.SineInOut, SweepTo, 44f, -8f),
        };

        /// <summary>Lowered to 44 below the aim (UpLowest), brought up through 92, and back a little.</summary>
        private static readonly Piece[] SwingUp =
        {
            new Piece(Ease.SineInOut, 0f, 32f, 12f),
            new Piece(Ease.PolyOut, WindTo, UpLowest, -92f, 3),
            new Piece(Ease.SineInOut, SweepTo, -48f, 8f),
        };

        /// <summary>The bottom of the swing up's wind-up, degrees below the aim: where SwingUp is at WindTo.</summary>
        private const float UpLowest = 44f;

        /// <summary>How far a swing down comes back up off the ground it met, degrees, and over how many ticks.</summary>
        private const float Rebound = 10f;
        private const float ReboundTicks = 8f;

        // ---- the thrust (Visus's timing) -----------------------------------------------------------

        private const float BackAt = 0.16f, OutAt = 0.40f, HeldTo = 0.58f;
        private const float ThrustHitsFrom = 0.20f, ThrustHitsTo = 0.62f;

        /// <summary>
        /// Where the front hand holds the shaft, in texels from the butt: at rest, drawn back, and
        /// driven out. Out, the point is 160 pixels (10 tiles) from the player's middle when the
        /// thrust is level: the hand is 6 in front of the middle and the point 154 past the hand.
        /// </summary>
        private const float GripRest = 66f, GripBack = 78f, GripOut = 12f;

        private static readonly Piece[] ThrustGrip =
        {
            new Piece(Ease.SineOut, 0f, GripRest, GripBack - GripRest),
            new Piece(Ease.PolyOut, BackAt, GripBack, GripOut - GripBack, 4),
            new Piece(Ease.SineInOut, OutAt, GripOut, 2f),
            new Piece(Ease.PolyInOut, HeldTo, GripOut + 2f, GripRest - GripOut - 2f, 2),
        };

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> line;
        private static Asset<Texture2D> streak;

        private Player Owner => Main.player[Projectile.owner];

        private int Mode => (int)Projectile.ai[0];

        private float Duration => Math.Max(1f, Projectile.ai[1]);

        private ref float Timer => ref Projectile.localAI[0];

        private bool started;

        /// <summary>The aim, upright, radians, and the side it faces, set when it starts.</summary>
        private float aim;
        private int facing;

        /// <summary>The angle this tick and last, upright, radians.</summary>
        private float angle;
        private float previousAngle;

        /// <summary>The grip this tick, texels from the butt.</summary>
        private float grip;

        /// <summary>The front hand this tick, upright.</summary>
        private Vector2 hand;

        /// <summary>The most a swing may turn down, radians: where the ground stops it. +Infinity with nothing under it.</summary>
        private float floor = float.PositiveInfinity;
        private float groundTick = -1f;

        private int stall;
        private bool struck;

        /// <summary>The point this tick and the last few, in the world, newest first (the thrust's streak).</summary>
        private readonly Vector2[] points = new Vector2[5];

        public override void Load()
        {
            if (Main.dedServ)
                return;

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            line = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
            streak = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Streak");
        }

        public override void Unload()
        {
            smear = null;
            line = null;
            streak = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 2;
        }

        public override bool ShouldUpdatePosition() => false;

        private bool Swing => Mode != Thrust;

        /// <summary>The swing up is the halberd turned over, so the blade leads it.</summary>
        private bool Turned => Mode == Up;

        private float SwingAt(float progress)
        {
            float a = aim + MathHelper.ToRadians(Easing.At(progress, Mode == Down ? SwingDown : SwingUp));
            a = Math.Min(a, floor);

            // A swing down that met the ground comes back up off it a little, rather than lying there.
            if (Mode == Down && groundTick >= 0f)
            {
                float since = progress * Duration - groundTick;

                if (since > 0f)
                    a = Math.Min(a, floor - MathHelper.ToRadians(Rebound * Easing.Apply(Ease.SineOut, since / ReboundTicks)));
            }

            return a;
        }

        private Player.CompositeArmStretchAmount StretchAt(float t)
        {
            if (Swing)
            {
                return t < WindTo ? Player.CompositeArmStretchAmount.ThreeQuarters
                    : t < SweepTo + 0.1f ? Player.CompositeArmStretchAmount.Full
                    : Player.CompositeArmStretchAmount.ThreeQuarters;
            }

            return t < BackAt + 0.02f ? Player.CompositeArmStretchAmount.None
                : t < ThrustHitsTo ? Player.CompositeArmStretchAmount.Full
                : t < 0.8f ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Quarter;
        }

        private bool Hitting
        {
            get
            {
                float t = Timer / Duration;

                if (!started)
                    return false;

                if (!Swing)
                    return t >= ThrustHitsFrom && t <= ThrustHitsTo;

                return t >= SwingHitsFrom && t <= SwingHitsTo && (groundTick < 0f || Timer - groundTick <= 2f);
            }
        }

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem.ModItem is not FleshMaiden)
            {
                Projectile.Kill();
                return;
            }

            if (!started)
                Start(player);

            Projectile.timeLeft = 2;
            player.ChangeDir(facing);
            player.GetModPlayer<FleshMaidenState>().Idle = 0;

            if (stall > 0)
            {
                stall--;
                Pose(player, Timer / Duration);
                return;
            }

            float before = Timer / Duration;
            Timer++;
            float t = Timer / Duration;

            if (t >= 1f)
            {
                Projectile.Kill();
                return;
            }

            previousAngle = angle;

            if (Swing)
            {
                angle = SwingAt(t);

                // The swing down stops on the ground, once it is coming down.
                if (Mode == Down && groundTick < 0f && t > WindTo && angle > aim - 0.2f)
                    FindGround(player);
            }
            else
            {
                grip = Easing.At(t, ThrustGrip);
            }

            Pose(player, t);

            if (!Main.dedServ)
                Effects(player, before, t);
        }

        private void Start(Player player)
        {
            started = true;

            Vector2 way = Projectile.velocity.SafeNormalize(new Vector2(player.direction, 0f));

            facing = way.X > 0f ? 1 : way.X < 0f ? -1 : player.direction;
            aim = (float)Math.Atan2(way.Y * player.gravDir, Math.Abs(way.X));

            if (Swing)
            {
                aim = MathHelper.Clamp(aim, -AimLimit, AimLimit);
                grip = SwingGrip;

                // The swing up starts from the ground if its wind-up would have put the head in it.
                if (Mode == Up)
                {
                    float lowest = aim + MathHelper.ToRadians(UpLowest);
                    Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.ThreeQuarters;

                    if (FleshMaidenGrip.InGround(player, lowest, stretch, grip, true, out _))
                    {
                        float clear = aim - 0.6f;

                        if (!FleshMaidenGrip.InGround(player, clear, stretch, grip, true, out _))
                            floor = FleshMaidenGrip.Meet(player, clear, lowest, stretch, grip, true) - 0.02f;
                    }
                }

                angle = previousAngle = SwingAt(0f);
            }
            else
            {
                angle = previousAngle = aim;
                grip = GripRest;
            }

            hand = VisusGrip.FrontHand(StretchAt(0f), angle);
            Vector2 point = FleshMaidenGrip.On(player, angle, hand, grip, FleshMaidenGrip.Point, Turned);

            for (int i = 0; i < points.Length; i++)
                points[i] = point;
        }

        private void Pose(Player player, float t)
        {
            Player.CompositeArmStretchAmount stretch = StretchAt(t);
            FleshMaidenState state = player.GetModPlayer<FleshMaidenState>();

            float glow;

            if (Swing)
                glow = t > WindTo && t < SweepTo ? 0.6f : 0.2f;
            else
                glow = Math.Max(0f, 1f - Math.Abs(t - OutAt) / 0.1f) * 0.8f;

            hand = VisusGrip.Hold(player, angle, grip, stretch, stretch, state.RestingEye, glow, PolearmArt.FleshMaiden, Turned);

            for (int i = points.Length - 1; i > 0; i--)
                points[i] = points[i - 1];

            points[0] = FleshMaidenGrip.On(player, angle, hand, grip, FleshMaidenGrip.Point, Turned);

            Projectile.Center = FleshMaidenGrip.On(player, angle, hand, grip, FleshMaidenGrip.Eye, Turned);
            Projectile.direction = Projectile.spriteDirection = facing;
        }

        /// <summary>The swing down meets the ground: where between last tick and this, and it stays there.</summary>
        private void FindGround(Player player)
        {
            Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.Full;

            if (!FleshMaidenGrip.InGround(player, angle, stretch, grip, false, out _))
                return;

            float met = FleshMaidenGrip.InGround(player, previousAngle, stretch, grip, false, out _)
                ? previousAngle
                : FleshMaidenGrip.Meet(player, previousAngle, angle, stretch, grip, false);

            floor = met;
            angle = met;
            groundTick = Timer;

            FleshMaidenGrip.InGround(player, met, stretch, grip, false, out Vector2 contact);

            if (!Main.dedServ)
                Land(player, contact);
        }

        /// <summary>Which way the head is travelling at <paramref name="a"/>, in the world: across the shaft, the way the swing goes.</summary>
        private Vector2 Travel(Player player, float a)
        {
            Vector2 u = a.ToRotationVector2();
            Vector2 across = new Vector2(-u.Y * facing, u.X * player.gravDir);
            return Mode == Up ? -across : across;
        }

        private static bool Crossed(float mark, float before, float t) => before < mark && t >= mark;

        private void Effects(Player player, float before, float t)
        {
            Vector2 point = points[0];
            Vector2 eye = Projectile.Center;

            if (Swing)
            {
                if (Crossed(WindTo, before, t))
                {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.6f, Volume = 0.8f, PitchVariance = 0.1f }, eye);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.55f }, eye);

                    // Tearing up out of the ground.
                    if (Mode == Up && !float.IsPositiveInfinity(floor))
                    {
                        FleshMaidenGrip.InGround(player, floor + 0.04f, Player.CompositeArmStretchAmount.Full, grip, true, out Vector2 torn);
                        Collision.HitTiles(torn - new Vector2(8f), Travel(player, floor) * 5f, 16, 16);
                        SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.5f, Volume = 0.6f }, torn);
                    }
                }

                // Coming round: blood shaken off the head, the way it is going.
                if (t > WindTo && t < SweepTo && groundTick < 0f && Main.rand.NextBool(2))
                {
                    Vector2 travel = Travel(player, angle);
                    Vector2 from = Vector2.Lerp(eye, point, Main.rand.NextFloat(0.2f, 0.9f));

                    PADParticleHandler.SpawnParticle(new BloodDroplet(from, travel * Main.rand.NextFloat(2f, 5f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        Main.rand.Next(24, 40), Main.rand.NextFloat(0.5f, 0.9f), FleshMaidenGrip.Blood));
                }

                if (t > WindTo && t < SweepTo)
                    Lighting.AddLight(eye, FleshMaidenGrip.Meat.ToVector3() * 0.3f);

                return;
            }

            Vector2 way = angle.ToRotationVector2();
            way = new Vector2(way.X * facing, way.Y * player.gravDir);

            if (Crossed(BackAt, before, t))
            {
                SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Pitch = -0.25f, Volume = 0.8f, PitchVariance = 0.1f }, point);
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.7f, Volume = 0.35f }, point);
            }

            // Arrived: a glint on the point, a spit of blood off it.
            if (Crossed(OutAt, before, t))
            {
                PADParticleHandler.SpawnParticle(new CrownGlint(point, 22f, 16, FleshMaidenGrip.Bone));

                for (int i = 0; i < 6; i++)
                {
                    PADParticleHandler.SpawnParticle(new BloodDroplet(point, way.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 5f),
                        Main.rand.Next(24, 40), Main.rand.NextFloat(0.45f, 0.8f), FleshMaidenGrip.Blood));
                }
            }

            if (t > BackAt && t < HeldTo)
                Lighting.AddLight(point, FleshMaidenGrip.Meat.ToVector3() * 0.25f);
        }

        /// <summary>The swing down lands on the ground: a wet thud, the floor jolted, dirt and blood.</summary>
        private void Land(Player player, Vector2 contact)
        {
            Vector2 travel = Travel(player, floor);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.05f, Volume = 0.6f }, contact);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.45f, Volume = 0.55f }, contact);

            if (Projectile.owner == Main.myPlayer)
                Main.LocalPlayer.PIS().BumpScreenShake(2.5f, 8);

            GroundWave.Strike(contact, -travel, 8f, 3, 3.5f, 18);
            Collision.HitTiles(contact - new Vector2(8f), travel * 4f, 16, 16);

            for (int i = 0; i < 5; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(contact, -travel.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.Next(24, 40), Main.rand.NextFloat(0.5f, 0.9f), FleshMaidenGrip.Blood));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Hitting)
                return false;

            Player player = Owner;

            if (!Swing)
                return FleshMaidenGrip.Hits(player, targetHitbox, angle, hand, grip);

            // Last tick's angle to this one's, so nothing between is stepped over.
            const int Samples = 6;

            for (int i = 0; i <= Samples; i++)
            {
                float a = MathHelper.Lerp(previousAngle, angle, i / (float)Samples);
                Vector2 h = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, a);

                if (FleshMaidenGrip.Hits(player, targetHitbox, a, h, grip))
                    return true;
            }

            return false;
        }

        public override void CutTiles()
        {
            if (Hitting)
                FleshMaidenGrip.Cut(Owner, angle, hand, grip);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Owner;
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);
            Vector2 away = Swing ? Travel(player, angle) : (points[0] - VisusGrip.ToWorld(player, hand)).SafeNormalize(new Vector2(facing, 0f));

            if (Swing)
                at = Vector2.Clamp(Vector2.Lerp(Projectile.Center, points[0], 0.5f), target.TopLeft, target.BottomRight);
            else
                at = Vector2.Clamp(points[0], target.TopLeft, target.BottomRight);

            if (Swing && !struck)
            {
                struck = true;
                stall = Stall;
            }

            if (Main.dedServ)
                return;

            FleshMaidenGrip.Bleed(at, away, damageDone);
            SparkShard.Spray(at, FleshMaidenGrip.Blood, 5, 3.5f, 26f, 16, falls: true, palette: FleshMaidenGrip.Spatter);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, FleshMaidenGrip.Meat * 0.7f, 0.5f, 14));
        }

        // ---- drawing -------------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            if (!started)
                return false;

            if (Swing)
                DrawSwing();
            else
                DrawStreak();

            return false;
        }

        /// <summary>How much of the trail is left: all of it while the swing is moving, gone within six ticks of it stopping.</summary>
        private float Fade()
        {
            float stop = groundTick >= 0f ? groundTick : SweepTo * Duration;
            return MathHelper.Clamp(1f - (Timer - stop) / 6f, 0f, 1f);
        }

        private void DrawSwing()
        {
            float t = Timer / Duration;
            float fade = Fade();

            if (t <= WindTo || fade <= 0f || smear == null || line == null)
                return;

            float tail = Math.Max(WindTo, t - Trail);

            if (t - tail <= 0.004f)
                return;

            Main.spriteBatch.End();
            DrawSmear(tail, t, fade);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);
        }

        /// <summary>Steps along the smear.</summary>
        private const int Steps = 20;

        // Laid out again every frame it is drawn, only ever on the main thread.
        private static readonly Vector2[] SmearInner = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearOuter = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearEdge = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearCoreIn = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearCoreOut = new Vector2[Steps + 1];
        private static readonly float[] SmearStrength = new float[Steps + 1];

        /// <summary>
        /// The smear over the sweep from <paramref name="tail"/> to <paramref name="head"/>, from
        /// the collar out past the point, strongest at the head: a dark band over what is behind,
        /// raw red light in it, then along the point's own path a band of blood over what is
        /// behind with a line of pale bone light down its middle.
        /// </summary>
        private void DrawSmear(float tail, float head, float fade)
        {
            Player player = Owner;
            float inner = FleshMaidenGrip.HeadFrom - grip - 8f;
            float outer = FleshMaidenGrip.Point.X - grip + 4f;

            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                float a = SwingAt(MathHelper.Lerp(head, tail, k));
                Vector2 h = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, a);
                Vector2 u = a.ToRotationVector2();

                SmearInner[i] = VisusGrip.ToWorld(player, h + u * inner);
                SmearOuter[i] = VisusGrip.ToWorld(player, h + u * outer);
                SmearEdge[i] = VisusGrip.ToWorld(player, h + u * (outer - 10f));
                SmearCoreIn[i] = VisusGrip.ToWorld(player, h + u * (outer - 6f));
                SmearCoreOut[i] = VisusGrip.ToWorld(player, h + u * (outer - 1f));
                SmearStrength[i] = (float)Math.Pow(1f - k, 1.2f) * fade;
            }

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dark = new Color(30, 4, 10, 255) * (0.7f * SmearStrength[i]);
                Strip.Pair(SmearInner[i], SmearOuter[i], dark, dark, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color red = new Color(150, 20, 34, 0) * (0.45f * SmearStrength[i]);
                Strip.Pair(SmearInner[i], SmearOuter[i], red, red, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color blood = new Color(120, 8, 18, 255) * (0.75f * SmearStrength[i]);
                Strip.Pair(SmearEdge[i], SmearOuter[i], blood, blood, i / (float)Steps);
            }
            Strip.Draw(line.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                Color hot = k < 0.3f
                    ? Color.Lerp(FleshMaidenGrip.Bone, FleshMaidenGrip.Meat, k / 0.3f)
                    : Color.Lerp(FleshMaidenGrip.Meat, new Color(80, 6, 18), (k - 0.3f) / 0.7f);

                hot.A = 0;
                hot *= 0.85f * SmearStrength[i];

                Strip.Pair(SmearCoreIn[i], SmearCoreOut[i], hot, hot, k);
            }
            Strip.Draw(line.Value);
        }

        /// <summary>
        /// The streak the point leaves going out: along the way it went over the last few ticks,
        /// a dark smear under a thread of blood with a pale core. Gone once the point stops.
        /// </summary>
        private void DrawStreak()
        {
            float t = Timer / Duration;

            if (streak == null || t < BackAt || t > HeldTo)
                return;

            Vector2 head = points[0];
            Vector2 run = head - points[points.Length - 1];
            float length = run.Length();

            Vector2 way = angle.ToRotationVector2();
            way = new Vector2(way.X * facing, way.Y * Owner.gravDir);

            if (length < 6f || Vector2.Dot(run, way) <= 0f)
                return;

            Texture2D texture = streak.Value;
            float rotation = run.ToRotation();
            Vector2 origin = new Vector2(texture.Width, texture.Height * 0.5f);
            Vector2 at = head - Main.screenPosition;
            float strength = MathHelper.Clamp(length / 50f, 0f, 1f);
            float along = Math.Min(length, 130f) / texture.Width;

            Main.EntitySpriteDraw(texture, at, null, new Color(20, 4, 8, 255) * (0.55f * strength), rotation, origin,
                new Vector2(along * 1.1f, 20f / texture.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, at, null, new Color(150, 12, 24, 0) * (0.9f * strength), rotation, origin,
                new Vector2(along, 10f / texture.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, at, null, new Color(240, 226, 206, 0) * (0.5f * strength), rotation, origin,
                new Vector2(along * 0.7f, 3f / texture.Height), SpriteEffects.None, 0);
        }
    }
}
