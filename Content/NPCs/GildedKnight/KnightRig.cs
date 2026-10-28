using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// Where everything is on the Gilded Knight's pictures, and the sums that put them together:
    /// one place for the drawing and the fight, so the blade that is drawn is the blade that
    /// cuts.
    ///
    /// Every number here was measured off the pictures in this folder (drawn by code, each
    /// joint recorded by the code that drew it), on the pictures' own grid: a joint "at pixel
    /// (9, 4)" is the middle of that pixel, (9.5, 4.5) as SpriteBatch counts an origin.
    ///
    ///   Torso           26x34   the cuirass, faulds and belt; turns about the hips
    ///   TorsoFront/Back 30x34   the same turned three quarters toward the eye, or away (Pose.Twist)
    ///   Helm            22x25   about the neck
    ///   Top/BotPauldron 20x17   about the shoulder, turning a third as far as the arm under it
    ///   Top/BotArm      18x42   straight, about the shoulder: the fist 29.2 below it
    ///   Top/BotArmBent  32x28   the elbow at a right angle, the forearm forward
    ///   Top/BotLeg      26x46   straight, about the hip
    ///   Top/BotLegBent  38x34   the thigh forward, the shin down
    ///   ...Toward/Away          each leg, straight and bent, its knee and foot turned toward the
    ///                           eye or away from it (Pose.NearToe, FarToe): the joints the same
    ///   Sun/GraceBlade  17x84   upright, the point up, about the middle of the grip
    ///   Sun/GraceScabbard 11x73 upright, the mouth up, about the mouth
    ///   Cape, FrontFlap, BackFlap, Plume: cloth, laid along chains (KnightCloth) as strips,
    ///                   the picture's x along the cloth and its y across it
    ///
    /// The art faces RIGHT. EVERYTHING BELOW IS WORKED OUT FACING RIGHT, forward along +x and
    /// down along +y, from the point on the ground between his feet; facing left mirrors it.
    /// The ways of saying a turn, each the natural one for what it turns:
    ///
    ///   a limb      by where its first part points (the upper arm, the thigh), from straight
    ///               down, forward positive: 0 hangs, a quarter turn points ahead, a half up
    ///   the body    by its lean, forward positive; the head by its nod on top of that
    ///   a blade     by where it points, from straight ahead, turning downward positive: 0
    ///               ahead, a quarter turn at the ground, minus a quarter at the sky. It may
    ///               be past a whole turn: from 170 to 300 degrees is over the top.
    ///
    /// The Sun is the near hand's sword and the Grace the far hand's. Sheathed, each hangs at
    /// the hip across from the hand that draws it (a cross draw): the Sun at the far hip, the
    /// Grace at the near one.
    ///
    /// Nothing in this file touches the game: vectors and angles only, so the same code is
    /// run outside it to see what he looks like.
    /// </summary>
    public static class KnightRig
    {
        public const string Folder = "PisAndDag/Content/NPCs/GildedKnight/";

        // ---- the pieces ---------------------------------------------------------------------

        public const int Torso = 0;
        public const int Helm = 1;
        public const int TopPauldron = 2;
        public const int BotPauldron = 3;
        public const int TopArm = 4;
        public const int TopArmBent = 5;
        public const int BotArm = 6;
        public const int BotArmBent = 7;
        public const int TopLeg = 8;
        public const int TopLegBent = 9;
        public const int BotLeg = 10;
        public const int BotLegBent = 11;
        public const int SunBlade = 12;
        public const int GraceBlade = 13;
        public const int SunScabbard = 14;
        public const int GraceScabbard = 15;
        public const int TorsoFront = 16;
        public const int TorsoBack = 17;
        public const int TopLegToward = 18;
        public const int TopLegAway = 19;
        public const int TopLegBentToward = 20;
        public const int TopLegBentAway = 21;
        public const int BotLegToward = 22;
        public const int BotLegAway = 23;
        public const int BotLegBentToward = 24;
        public const int BotLegBentAway = 25;
        public const int Pieces = 26;

        public static readonly string[] Names =
        {
            "Torso", "Helm", "TopPauldron", "BotPauldron",
            "TopArm", "TopArmBent", "BotArm", "BotArmBent",
            "TopLeg", "TopLegBent", "BotLeg", "BotLegBent",
            "SunBlade", "GraceBlade", "SunScabbard", "GraceScabbard",
            "TorsoFront", "TorsoBack",
            "TopLegToward", "TopLegAway", "TopLegBentToward", "TopLegBentAway",
            "BotLegToward", "BotLegAway", "BotLegBentToward", "BotLegBentAway",
        };

        public static readonly Point[] Sizes =
        {
            new Point(26, 34), new Point(22, 25), new Point(20, 17), new Point(20, 17),
            new Point(18, 42), new Point(32, 28), new Point(18, 42), new Point(32, 28),
            new Point(26, 46), new Point(38, 34), new Point(26, 46), new Point(38, 34),
            new Point(17, 84), new Point(17, 84), new Point(11, 73), new Point(11, 73),
            new Point(30, 34), new Point(30, 34),
            new Point(26, 46), new Point(26, 46), new Point(38, 34), new Point(38, 34),
            new Point(26, 46), new Point(26, 46), new Point(38, 34), new Point(38, 34),
        };

        /// <summary>What each piece turns about, on its own picture (facing right, as drawn).</summary>
        public static readonly Vector2[] Pivots =
        {
            new Vector2(12.5f, 30.5f),  // the hips
            new Vector2(10.5f, 23.5f),  // the neck
            new Vector2(10f, 7.5f),     // the shoulder, under the pauldron
            new Vector2(10f, 7.5f),
            new Vector2(9.5f, 4.5f),    // the shoulder
            new Vector2(9.5f, 4.5f),
            new Vector2(9.5f, 4.5f),
            new Vector2(9.5f, 4.5f),
            new Vector2(9.5f, 4.5f),    // the hip
            new Vector2(7.5f, 6.5f),
            new Vector2(9.5f, 4.5f),
            new Vector2(7.5f, 6.5f),
            new Vector2(8.5f, 74.5f),   // the middle of the grip
            new Vector2(8.5f, 74.5f),
            new Vector2(5.5f, 1.5f),    // the mouth
            new Vector2(5.5f, 1.5f),
            new Vector2(14.5f, 30.5f),  // the hips, turned
            new Vector2(14.5f, 30.5f),
            new Vector2(9.5f, 4.5f),    // the hip, the leg turned
            new Vector2(9.5f, 4.5f),
            new Vector2(7.5f, 6.5f),
            new Vector2(7.5f, 6.5f),
            new Vector2(9.5f, 4.5f),
            new Vector2(9.5f, 4.5f),
            new Vector2(7.5f, 6.5f),
            new Vector2(7.5f, 6.5f),
        };

        /// <summary>The cloth's pictures: laid along their chains as strips (KnightCloth).</summary>
        public const string Cape = "Cape";
        public const string FrontFlap = "FrontFlap";
        public const string BackFlap = "BackFlap";
        public const string Plume = "Plume";

        /// <summary>The blades' light: the edges, the fuller and the point, white, for drawing added over the blade.</summary>
        public const string SunGlow = "SunBladeGlow";
        public const string GraceGlow = "GraceBladeGlow";

        // ---- where the joints are, facing right, y down -----------------------------------------

        /// <summary>The hips over the ground, standing straight: hip to the underside of the sole.</summary>
        public const float HipsHeight = 39.5f;

        /// <summary>From the hips: the neck, each shoulder, each leg's hip, the cape's hem at the back, the belt front and back.</summary>
        public static readonly Vector2 Neck = new Vector2(0f, -27f);
        public static readonly Vector2 NearShoulder = new Vector2(-1f, -22f);
        public static readonly Vector2 FarShoulder = new Vector2(2f, -23f);
        public static readonly Vector2 NearHip = new Vector2(-1f, 0f);
        public static readonly Vector2 FarHip = new Vector2(2f, 0f);
        public static readonly Vector2 CapeAt = new Vector2(-6f, -25f);
        public static readonly Vector2 BeltFront = new Vector2(7f, -4f);
        public static readonly Vector2 BeltBack = new Vector2(-6f, -4f);

        /// <summary>The scabbards' mouths at the belt, from the hips: the Grace's at the near hip, the Sun's at the far.</summary>
        public static readonly Vector2 GraceMouth = new Vector2(5f, -3f);
        public static readonly Vector2 SunMouth = new Vector2(4f, -4f);

        /// <summary>How each scabbard hangs, from straight down toward his back, before it swings.</summary>
        public const float GraceHang = 1.02f;
        public const float SunHang = 0.96f;

        /// <summary>From the neck, with the head level: the eye (the sight's light), the plume's socket, the top of the crown.</summary>
        public static readonly Vector2 Eye = new Vector2(6.5f, -11f);
        public static readonly Vector2 PlumeAt = new Vector2(-5.5f, -17.5f);
        public static readonly Vector2 Crown = new Vector2(0f, -22f);

        /// <summary>An arm's reach, straight and bent as drawn; nearer than BendBelow, the bent picture is used.</summary>
        public const float ArmLong = 29.2f;
        public const float ArmBent = 20.67f;
        public const float BendBelow = 25f;

        /// <summary>A leg from hip to sole, straight and bent as drawn; nearer than KneeBelow, the bent picture.</summary>
        public const float LegLong = 39.5f;
        public const float LegBent = 28.19f;
        public const float KneeBelow = 33.8f;

        /// <summary>From the shoulder, the arm as drawn: the fist, straight and bent, and the elbow.</summary>
        public static readonly Vector2 FistStraight = new Vector2(0f, 29.2f);
        public static readonly Vector2 FistBent = new Vector2(15.2f, 14f);
        public static readonly Vector2 Elbow = new Vector2(0f, 14f);

        /// <summary>From the hip, the leg as drawn: the underside of the sole (under the ankle), straight and bent.</summary>
        public static readonly Vector2 SoleStraight = new Vector2(0f, 39.5f);
        public static readonly Vector2 SoleBent = new Vector2(17f, 22.5f);

        /// <summary>Where each limb's first part points on its picture (from straight down, forward positive).</summary>
        public const float ArmRest = 0f;
        public const float LegRest = 0f;
        public const float LegBentRest = MathHelper.PiOver2;

        /// <summary>The bent arm's fist is this far round from its upper arm.</summary>
        public static readonly float BentFistTurn = (float)Math.Atan2(FistBent.X, FistBent.Y);

        /// <summary>The bent leg's sole is this far round (back) from its thigh.</summary>
        public static readonly float BentSoleTurn = LegBentRest - (float)Math.Atan2(SoleBent.X, SoleBent.Y);

        // ---- the swords ---------------------------------------------------------------------

        /// <summary>Along the blade from the middle of the grip: to the point, to the guard, back to the end of the pommel.</summary>
        public const float GripToTip = 73.5f;
        public const float GripToGuard = 10f;
        public const float GripToPommel = 9f;

        /// <summary>How far a sword in its scabbard can be drawn before its point is clear of the mouth (the blade's length).</summary>
        public const float BladeLength = GripToTip - GripToGuard;

        /// <summary>A pauldron turns this share of the way its arm turns.</summary>
        public const float PauldronFollows = 0.3f;

        /// <summary>The narrowest he is ever seen, turning about: edge on, a man is still this share of his width.</summary>
        public const float Thin = 0.3f;

        // ---- turning at the waist (Pose.Twist) -------------------------------------------------

        /// <summary>
        /// Half the width across his shoulders and across his hips: turned at the waist, each
        /// shoulder goes this far round, forward or back, as the sine of the turn. Turned three
        /// quarters to the eye (Twist 1, 45 degrees) the near shoulder is 8.5 back and the far 8.5
        /// forward: his shoulders 20 apart across the turned picture (3 side on), and the far arm
        /// reaching 17 further ahead than the near; turned away, the other way. (Wider by request:
        /// "when slanted his shoulder should be further from each other too".)
        /// </summary>
        public const float ShoulderSwing = 12f;
        public const float HipSwing = 4f;

        /// <summary>The cape's root, along his back, turned: toward the eye's side of his back as it turns away.</summary>
        public const float CapeSwing = 5f;

        /// <summary>The most he turns at the waist, at Twist 1: three quarters.</summary>
        public const float TwistMost = MathHelper.PiOver4;

        /// <summary>From Twist (and Toe) this far either way, the turned picture is the one drawn.</summary>
        public const float TurnedFrom = 0.5f;

        /// <summary>An upper arm this far forward of straight down (the sine of its angle) comes across his front, turned to the eye; this far back, behind his back turned away.</summary>
        public const float ArmAcross = 0.35f;

        /// <summary>How far round a shoulder or hip goes for this twist (the sine of the turn).</summary>
        public static float Swing(float twist) => (float)Math.Sin(MathHelper.Clamp(twist, -1f, 1f) * TwistMost);

        public static Vector2 NearShoulderAt(float twist) => NearShoulder - new Vector2(ShoulderSwing * Swing(twist), 0f);
        public static Vector2 FarShoulderAt(float twist) => FarShoulder + new Vector2(ShoulderSwing * Swing(twist), 0f);
        public static Vector2 NearHipAt(float twist) => NearHip - new Vector2(HipSwing * Swing(twist), 0f);
        public static Vector2 FarHipAt(float twist) => FarHip + new Vector2(HipSwing * Swing(twist), 0f);

        // ============================================================
        // THE POSE
        // ============================================================

        /// <summary>Which sword: the near hand's, the far hand's.</summary>
        public const int Sun = 0;
        public const int Grace = 1;

        /// <summary>Where a sword is: in its scabbard, in the near hand, in the far hand; or let go of (drawn by whoever let it go, not here).</summary>
        public const int Sheathed = 0;
        public const int InNear = 1;
        public const int InFar = 2;
        public const int Dropped = 3;

        /// <summary>How he holds himself, facing right. See the class for what each turn is measured from.</summary>
        public struct Pose
        {
            public float Lean;
            public float Nod;

            /// <summary>
            /// Each arm by where its fist is: the way from the shoulder (as a limb's angle), and how
            /// far (pixels). Nearer than <see cref="BendBelow"/> it is drawn bent at the elbow, further
            /// straight, either stretched or shortened along itself to put the fist exactly there.
            /// </summary>
            public float NearArm;
            public float NearReach;
            public float FarArm;
            public float FarReach;

            /// <summary>Each leg by where its sole is: the way from the hip, and how far. Nearer than <see cref="KneeBelow"/> it is bent at the knee.</summary>
            public float NearLeg;
            public float NearStep;
            public float FarLeg;
            public float FarStep;

            /// <summary>In the air: the hips are where <see cref="Rise"/> says, not on whichever foot is lower.</summary>
            public bool Air;

            /// <summary>The hips this much higher than the legs alone would have them (down is negative).</summary>
            public float Rise;

            /// <summary>Where each blade points.</summary>
            public float SunAngle;
            public float GraceAngle;

            /// <summary>Where each sword is: <see cref="Sheathed"/>, <see cref="InNear"/>, <see cref="InFar"/>.</summary>
            public int SunIn;
            public int GraceIn;

            /// <summary>
            /// A sword in its scabbard drawn this many pixels out of it along the scabbard (0 home,
            /// <see cref="BladeLength"/> its point at the mouth). Only while it is sheathed.
            /// </summary>
            public float SunDrawn;
            public float GraceDrawn;

            /// <summary>Each blade as long as it is seen, 1 all of it: shorter pointed toward the eye or away.</summary>
            public float SunReach;
            public float GraceReach;

            /// <summary>The hand is on this sword's grip wherever that is (its arm turned and stretched to it): -1 none, or Sun or Grace.</summary>
            public int NearOn;
            public int FarOn;

            /// <summary>The whole of him seen turning about his own middle: 1 side on, toward 0 edge on, past it negative and mirrored.</summary>
            public float Turn;

            /// <summary>
            /// Turned at the waist, the legs staying as they are: 1 three quarters toward the eye
            /// (the breast seen, the far shoulder forward: the far arm reaches further), 0 side on,
            /// -1 three quarters away (the back seen, the near shoulder forward: the near arm
            /// reaches further). Past <see cref="TurnedFrom"/> either way, the turned picture.
            /// </summary>
            public float Twist;

            /// <summary>Each leg's knee and foot turned: 1 toward the eye, 0 side on, -1 away from it.</summary>
            public float NearToe;
            public float FarToe;

            public static Pose Standing => new Pose
            {
                NearReach = ArmLong,
                FarReach = ArmLong,
                NearStep = LegLong,
                FarStep = LegLong,
                SunIn = Sheathed,
                GraceIn = Sheathed,
                SunReach = 1f,
                GraceReach = 1f,
                NearOn = -1,
                FarOn = -1,
                Turn = 1f,
            };
        }

        /// <summary>One picture to draw, where and how, in the world, side on.</summary>
        public struct Part
        {
            public int Piece;

            /// <summary>Where the pivot goes, in the world.</summary>
            public Vector2 At;

            /// <summary>The pivot on the picture as it will be drawn (mirrored with it).</summary>
            public Vector2 Origin;

            /// <summary>Clockwise, as SpriteBatch turns.</summary>
            public float Rotation;

            public bool FlipX;

            /// <summary>The picture's own scale, along its own x and y.</summary>
            public Vector2 Scale;

            /// <summary>A shadow of it is laid on what is under it first (the near limbs, the head, the near blade).</summary>
            public bool Shadow;
        }

        /// <summary>Where the joints came out, in the world, for whoever needs them: the fight, the light, the sparks, the cloth.</summary>
        public struct Frame
        {
            public Vector2 Hips;
            public Vector2 NeckAt;
            public Vector2 EyeAt;
            public Vector2 CrownAt;
            public Vector2 NearShoulderAt;
            public Vector2 FarShoulderAt;
            public Vector2 NearFist;
            public Vector2 FarFist;
            public Vector2 NearSole;
            public Vector2 FarSole;

            /// <summary>The cloth's roots: the cape at his back, the tabard's flaps at the belt, the plume at the crown.</summary>
            public Vector2 CapeRoot;
            public Vector2 FrontRoot;
            public Vector2 BackRoot;
            public Vector2 PlumeRoot;

            /// <summary>The way the plume's root points (a world angle), with the head.</summary>
            public float PlumeAngle;

            /// <summary>The swords: the middle of each grip, the way each blade points (a world angle, clockwise from +x), its guard and its point.</summary>
            public Vector2 SunGrip, SunGuard, SunTip;
            public float SunBlade;
            public Vector2 GraceGrip, GraceGuard, GraceTip;
            public float GraceBlade;
            public bool SunOut, GraceOut;

            /// <summary>How wide he is seen (1 side on, never under Thin), and the upright line he is narrowed about, x in the world.</summary>
            public float Turn;
            public float TurnAbout;
        }

        // ============================================================
        // THE SUMS
        // ============================================================

        /// <summary>A vector turned clockwise (on screen) by <paramref name="angle"/>.</summary>
        public static Vector2 Turned(Vector2 v, float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>The way a limb points: from straight down, forward positive.</summary>
        public static Vector2 Down(float angle) => new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));

        /// <summary>A limb's angle (from straight down, forward positive) of a way, given as a vector.</summary>
        public static float AngleOf(Vector2 way) => (float)Math.Atan2(way.X, way.Y);

        /// <summary>A blade angle as a vector.</summary>
        public static Vector2 Along(float blade) => new Vector2((float)Math.Cos(blade), (float)Math.Sin(blade));

        /// <summary>From the shoulder to the fist.</summary>
        public static Vector2 FistFrom(float angle, float reach) => Down(angle) * (reach <= 0f ? ArmLong : reach);

        /// <summary>From the hip to the underside of the sole.</summary>
        public static Vector2 SoleFrom(float angle, float step) => Down(angle) * (step <= 0f ? LegLong : step);

        /// <summary>
        /// How high the hips are over the ground for these legs: on whichever foot is lower, so a
        /// stride lifts the other foot and a wide stance lowers him.
        /// </summary>
        public static float HipsOver(in Pose pose)
        {
            if (pose.Air)
                return HipsHeight + pose.Rise;

            float near = NearHip.Y + SoleFrom(pose.NearLeg, pose.NearStep).Y;
            float far = FarHip.Y + SoleFrom(pose.FarLeg, pose.FarStep).Y;

            return Math.Max(near, far) + pose.Rise;
        }

        /// <summary>Where a scabbard's mouth is, from the hips, and the way it hangs (a world angle along it from the mouth), for this lean and swing.</summary>
        public static void Scabbard(int sword, float lean, float swing, out Vector2 mouth, out float angle)
        {
            mouth = Turned(sword == Sun ? SunMouth : GraceMouth, lean);

            // Hanging toward the back by its hang, from straight down: as a blade angle, a quarter
            // turn (down) and on round toward the back.
            float hang = (sword == Sun ? SunHang : GraceHang) + swing;
            angle = MathHelper.PiOver2 + hang + lean;
        }

        /// <summary>The pose with a hand on this point: its fist put there (the arm bends, stretches or shortens to it).</summary>
        public static void Reach(ref Pose pose, bool near, Vector2 target, Vector2 hips)
        {
            Vector2 shoulder = hips + Turned(near ? NearShoulderAt(pose.Twist) : FarShoulderAt(pose.Twist), pose.Lean);
            Vector2 way = target - shoulder;
            float dist = way.Length();

            if (dist < 0.001f)
                return;

            if (near)
            {
                pose.NearArm = AngleOf(way);
                pose.NearReach = dist;
            }
            else
            {
                pose.FarArm = AngleOf(way);
                pose.FarReach = dist;
            }
        }

        /// <summary>The middle of a sheathed sword's grip: its guard at the mouth, drawn out along the scabbard by so much.</summary>
        private static Vector2 SheathedGrip(Vector2 mouth, float hang, float drawn) => mouth - Along(hang) * (GripToGuard + drawn);

        /// <summary>
        /// The pose with each hand that is on a sheathed sword's grip (Pose.NearOn, FarOn) put
        /// there: its arm turned, bent or not, and stretched to the grip. Done by Build, and by
        /// KnightTrack to the poses either side of a moment before it goes between them, so
        /// an arm that lets go of a grip leaves from where it was.
        /// </summary>
        public static void Resolve(ref Pose pose, float sunSwing = 0f, float graceSwing = 0f)
        {
            if (pose.NearOn < 0 && pose.FarOn < 0)
                return;

            Vector2 hips = new Vector2(0f, -HipsOver(pose));

            // Both hands on the Grace: the near one on its grip above the far one, toward the pommel.
            if (pose.NearOn == Grace && pose.GraceIn == InFar)
            {
                Vector2 farFist = hips + Turned(FarShoulderAt(pose.Twist), pose.Lean) + FistFrom(pose.FarArm, pose.FarReach);
                Reach(ref pose, true, farFist - Along(pose.GraceAngle) * 6f, hips);
                return;
            }

            Scabbard(Sun, pose.Lean, sunSwing, out Vector2 sunMouth, out float sunHang);
            Scabbard(Grace, pose.Lean, graceSwing, out Vector2 graceMouth, out float graceHang);
            sunMouth += hips;
            graceMouth += hips;

            if (pose.NearOn == Sun && pose.SunIn == Sheathed)
                Reach(ref pose, true, SheathedGrip(sunMouth, sunHang, pose.SunDrawn), hips);
            else if (pose.NearOn == Grace && pose.GraceIn == Sheathed)
                Reach(ref pose, true, SheathedGrip(graceMouth, graceHang, pose.GraceDrawn), hips);

            if (pose.FarOn == Sun && pose.SunIn == Sheathed)
                Reach(ref pose, false, SheathedGrip(sunMouth, sunHang, pose.SunDrawn), hips);
            else if (pose.FarOn == Grace && pose.GraceIn == Sheathed)
                Reach(ref pose, false, SheathedGrip(graceMouth, graceHang, pose.GraceDrawn), hips);
        }

        /// <summary>
        /// Everything of him for this pose, standing with the ground between his feet at
        /// <paramref name="feet"/> and facing <paramref name="facing"/>: the pictures to draw,
        /// back to front, added to <paramref name="parts"/> (if given), and where his joints and
        /// swords came out. <paramref name="sunSwing"/> and <paramref name="graceSwing"/> are how
        /// far each scabbard swings on its strap this tick (the NPC's, from how he moves).
        /// The cloth is not among the pictures: it is drawn from its chains, at the places
        /// in the frame, before and between them (see <see cref="Layer"/>).
        /// </summary>
        public static Frame Build(Pose pose, Vector2 feet, int facing, List<Part> parts = null, float sunSwing = 0f, float graceSwing = 0f)
        {
            int faces = facing >= 0 ? 1 : -1;
            int d = pose.Turn < 0f ? -faces : faces;
            float wide = Math.Max(Thin, Math.Min(1f, Math.Abs(pose.Turn)));

            Vector2 hips = new Vector2(0f, -HipsOver(pose));

            // The hands on a grip, before anything is worked out from where the hands are.
            Resolve(ref pose, sunSwing, graceSwing);

            Scabbard(Sun, pose.Lean, sunSwing, out Vector2 sunMouth, out float sunHang);
            Scabbard(Grace, pose.Lean, graceSwing, out Vector2 graceMouth, out float graceHang);
            sunMouth += hips;
            graceMouth += hips;

            Vector2 nearShoulder = hips + Turned(NearShoulderAt(pose.Twist), pose.Lean);
            Vector2 farShoulder = hips + Turned(FarShoulderAt(pose.Twist), pose.Lean);
            Vector2 neck = hips + Turned(Neck, pose.Lean);
            Vector2 nearHip = NearHipAt(pose.Twist);
            Vector2 farHip = FarHipAt(pose.Twist);
            Vector2 capeAt = CapeAt - new Vector2(CapeSwing * Swing(pose.Twist), 0f);

            Vector2 farFist = farShoulder + FistFrom(pose.FarArm, pose.FarReach);
            Vector2 nearFist = nearShoulder + FistFrom(pose.NearArm, pose.NearReach);

            float head = pose.Lean + pose.Nod;

            // Each sword: in its scabbard, or in a hand.
            Vector2 sunGrip, graceGrip;
            float sunBlade, graceBlade;

            if (pose.SunIn == Sheathed)
            {
                sunBlade = sunHang;
                sunGrip = SheathedGrip(sunMouth, sunHang, pose.SunDrawn);
            }
            else
            {
                sunBlade = pose.SunAngle;
                sunGrip = pose.SunIn == InFar ? farFist : nearFist;
            }

            if (pose.GraceIn == Sheathed)
            {
                graceBlade = graceHang;
                graceGrip = SheathedGrip(graceMouth, graceHang, pose.GraceDrawn);
            }
            else
            {
                graceBlade = pose.GraceAngle;
                graceGrip = pose.GraceIn == InNear ? nearFist : farFist;
            }

            float sunReach = pose.SunIn == Sheathed || pose.SunReach <= 0f ? 1f : pose.SunReach;
            float graceReach = pose.GraceIn == Sheathed || pose.GraceReach <= 0f ? 1f : pose.GraceReach;

            Vector2 sunAlong = Along(sunBlade);
            Vector2 graceAlong = Along(graceBlade);

            Vector2 nearSole = hips + nearHip + SoleFrom(pose.NearLeg, pose.NearStep);
            Vector2 farSole = hips + farHip + SoleFrom(pose.FarLeg, pose.FarStep);

            Frame frame = new Frame
            {
                Hips = Seen(hips, feet, d, wide),
                NeckAt = Seen(neck, feet, d, wide),
                EyeAt = Seen(neck + Turned(Eye, head), feet, d, wide),
                CrownAt = Seen(neck + Turned(Crown, head), feet, d, wide),
                NearShoulderAt = Seen(nearShoulder, feet, d, wide),
                FarShoulderAt = Seen(farShoulder, feet, d, wide),
                NearFist = Seen(nearFist, feet, d, wide),
                FarFist = Seen(farFist, feet, d, wide),
                NearSole = Seen(nearSole, feet, d, wide),
                FarSole = Seen(farSole, feet, d, wide),
                CapeRoot = Seen(hips + Turned(capeAt, pose.Lean), feet, d, wide),
                FrontRoot = Seen(hips + Turned(BeltFront, pose.Lean), feet, d, wide),
                BackRoot = Seen(hips + Turned(BeltBack, pose.Lean), feet, d, wide),
                PlumeRoot = Seen(neck + Turned(PlumeAt, head), feet, d, wide),
                PlumeAngle = WorldAngle(-MathHelper.PiOver2 - 0.52f + head, d),
                SunGrip = Seen(sunGrip, feet, d, wide),
                SunGuard = Seen(sunGrip + sunAlong * (GripToGuard * sunReach), feet, d, wide),
                SunTip = Seen(sunGrip + sunAlong * (GripToTip * sunReach), feet, d, wide),
                SunBlade = WorldAngle(sunBlade, d),
                GraceGrip = Seen(graceGrip, feet, d, wide),
                GraceGuard = Seen(graceGrip + graceAlong * (GripToGuard * graceReach), feet, d, wide),
                GraceTip = Seen(graceGrip + graceAlong * (GripToTip * graceReach), feet, d, wide),
                GraceBlade = WorldAngle(graceBlade, d),
                SunOut = pose.SunIn == InNear || pose.SunIn == InFar,
                GraceOut = pose.GraceIn == InNear || pose.GraceIn == InFar,
                Turn = wide,
                TurnAbout = feet.X,
            };

            if (parts == null)
                return frame;

            // ---- back to front ----

            // The Sun's scabbard at the far hip, with the Sun in it if it is home.
            if (pose.SunIn == Sheathed)
                parts.Add(SwordPart(SunBlade, World(sunGrip, feet, d), sunBlade, 1f, d, false));

            parts.Add(Piece(SunScabbard, World(sunMouth, feet, d), sunHang - MathHelper.PiOver2, d, Vector2.One, false));

            // Which arm goes where (front and back placement): turned to the eye, the far arm
            // brought forward goes across in front of his breast; turned away, the near arm
            // brought back goes behind his back. Otherwise the far arm is behind him and the near
            // in front, as side on. Each arm takes its sword with it (under the fist that holds it).
            bool farInFront = pose.Twist >= TurnedFrom && Math.Sin(UpperArm(pose.FarArm, pose.FarReach)) > ArmAcross;
            bool nearBehind = pose.Twist <= -TurnedFrom && Math.Sin(UpperArm(pose.NearArm, pose.NearReach)) < -ArmAcross;

            void FarArm(bool shadow)
            {
                if (pose.GraceIn == InFar)
                    parts.Add(SwordPart(GraceBlade, World(graceGrip, feet, d), graceBlade, graceReach, d, shadow));

                if (pose.SunIn == InFar)
                    parts.Add(SwordPart(SunBlade, World(sunGrip, feet, d), sunBlade, sunReach, d, shadow));

                Part arm = Arm(false, farShoulder, pose.FarArm, pose.FarReach, feet, d);
                arm.Shadow = shadow;
                parts.Add(arm);
            }

            void NearArm(bool shadow)
            {
                if (pose.SunIn == InNear)
                    parts.Add(SwordPart(SunBlade, World(sunGrip, feet, d), sunBlade, sunReach, d, shadow));

                if (pose.GraceIn == InNear)
                    parts.Add(SwordPart(GraceBlade, World(graceGrip, feet, d), graceBlade, graceReach, d, shadow));

                Part arm = Arm(true, nearShoulder, pose.NearArm, pose.NearReach, feet, d);
                arm.Shadow = shadow;
                parts.Add(arm);
            }

            // The far arm behind him, its sword behind it.
            if (!farInFront)
                FarArm(false);

            parts.Add(Piece(BotPauldron, World(farShoulder, feet, d), pose.Lean * 0.7f - UpperArm(pose.FarArm, pose.FarReach) * PauldronFollows, d, Vector2.One, false));

            // (the back flap)
            parts.Add(Leg(false, hips + farHip, pose.FarLeg, pose.FarStep, pose.FarToe, feet, d));
            parts.Add(Leg(true, hips + nearHip, pose.NearLeg, pose.NearStep, pose.NearToe, feet, d));

            // The Grace's scabbard at the near hip, over the near leg, with the Grace in it if it is home.
            if (pose.GraceIn == Sheathed)
                parts.Add(SwordPart(GraceBlade, World(graceGrip, feet, d), graceBlade, 1f, d, false));

            parts.Add(Piece(GraceScabbard, World(graceMouth, feet, d), graceHang - MathHelper.PiOver2, d, Vector2.One, false));

            // The near arm swung back behind his back turned to the eye.
            if (nearBehind)
                NearArm(false);

            parts.Add(Piece(TorsoFor(pose.Twist), World(hips, feet, d), pose.Lean, d, Vector2.One, false));

            // The far arm across in front of his breast turned to the eye.
            if (farInFront)
                FarArm(true);

            // (the front flap, the plume)
            parts.Add(Piece(Helm, World(neck, feet, d), head, d, Vector2.One, true));

            // The near arm in front, its sword under the fist that holds it.
            if (!nearBehind)
                NearArm(true);

            parts.Add(Piece(TopPauldron, World(nearShoulder, feet, d), pose.Lean * 0.7f - UpperArm(pose.NearArm, pose.NearReach) * PauldronFollows, d, Vector2.One, true));

            return frame;
        }

        /// <summary>
        /// Where each piece of cloth goes among the pictures Build lays down, back to front:
        /// before the picture at this place in the list (the cape before everything; the back
        /// flap before the far leg; the front flap and the plume before the helm). For a pose
        /// whose list Build made: the Sun's sword is first only when it is home.
        /// </summary>
        public static void Layer(List<Part> parts, out int cape, out int backFlap, out int frontFlap)
        {
            // The cape behind everything; but with his back turned to the eye (TorsoBack), over his
            // back, just after it.
            int back = parts.FindIndex(p => p.Piece == TorsoBack);
            cape = back >= 0 ? back + 1 : 0;
            backFlap = parts.FindIndex(p => IsFarLeg(p.Piece));
            frontFlap = parts.FindIndex(p => p.Piece == Helm);

            if (backFlap < 0)
                backFlap = 0;

            if (frontFlap < 0)
                frontFlap = parts.Count;
        }

        private static Vector2 World(Vector2 local, Vector2 feet, int d) => feet + new Vector2(d * local.X, local.Y);

        /// <summary>The cuirass's picture for this twist.</summary>
        public static int TorsoFor(float twist) => twist >= TurnedFrom ? TorsoFront : twist <= -TurnedFrom ? TorsoBack : Torso;

        /// <summary>One of the far leg's pictures, however it is turned.</summary>
        public static bool IsFarLeg(int piece) => piece == BotLeg || piece == BotLegBent || (piece >= BotLegToward && piece <= BotLegBentAway);

        /// <summary>Where a point of him is seen, turning: that much nearer the line through his feet.</summary>
        private static Vector2 Seen(Vector2 local, Vector2 feet, int d, float wide) => feet + new Vector2(d * local.X * wide, local.Y);

        /// <summary>A blade angle facing right, as a world angle for the way he faces.</summary>
        private static float WorldAngle(float angle, int d) => d > 0 ? angle : MathHelper.Pi - angle;

        /// <summary>A piece turned clockwise by <paramref name="turn"/> (facing right) about its pivot.</summary>
        private static Part Piece(int piece, Vector2 at, float turn, int d, Vector2 scale, bool shadow)
        {
            Vector2 pivot = Pivots[piece];

            // The art faces right: facing left is the mirror of it, pivot and all.
            return new Part
            {
                Piece = piece,
                At = at,
                Origin = d > 0 ? pivot : new Vector2(Sizes[piece].X - pivot.X, pivot.Y),
                Rotation = d > 0 ? turn : -turn,
                FlipX = d < 0,
                Scale = scale,
                Shadow = shadow,
            };
        }

        /// <summary>Where the upper arm points, for a fist this way and this far: along it, straight; a bent arm's turned back by its bend.</summary>
        public static float UpperArm(float angle, float reach) => (reach <= 0f ? ArmLong : reach) < BendBelow ? angle - BentFistTurn : angle;

        private static Part Arm(bool near, Vector2 shoulder, float angle, float reach, Vector2 feet, int d)
        {
            if (reach <= 0f)
                reach = ArmLong;

            bool bent = reach < BendBelow;
            int piece = near ? (bent ? TopArmBent : TopArm) : (bent ? BotArmBent : BotArm);

            // Straight, it is stretched along itself; bent, the whole of it (it has no one way along).
            float stretch = reach / (bent ? ArmBent : ArmLong);
            Vector2 scale = bent ? new Vector2(stretch, stretch) : new Vector2(1f, stretch);

            return Piece(piece, World(shoulder, feet, d), -(UpperArm(angle, reach) - ArmRest), d, scale, near);
        }

        private static Part Leg(bool near, Vector2 hip, float angle, float step, float toe, Vector2 feet, int d)
        {
            if (step <= 0f)
                step = LegLong;

            bool bent = step < KneeBelow;
            int piece = LegPiece(near, bent, toe);
            float stretch = step / (bent ? LegBent : LegLong);

            // A bent leg's thigh is forward of the way to its sole by its bend.
            float thigh = bent ? angle + BentSoleTurn : angle;
            float rest = bent ? LegBentRest : LegRest;
            Vector2 scale = bent ? new Vector2(stretch, stretch) : new Vector2(1f, stretch);

            return Piece(piece, World(hip, feet, d), -(thigh - rest), d, scale, near);
        }

        /// <summary>A leg's picture: near or far, straight or bent, and its knee and foot turned toward the eye, away, or side on.</summary>
        public static int LegPiece(bool near, bool bent, float toe)
        {
            if (toe >= TurnedFrom)
                return near ? (bent ? TopLegBentToward : TopLegToward) : (bent ? BotLegBentToward : BotLegToward);

            if (toe <= -TurnedFrom)
                return near ? (bent ? TopLegBentAway : TopLegAway) : (bent ? BotLegBentAway : BotLegAway);

            return near ? (bent ? TopLegBent : TopLeg) : (bent ? BotLegBent : BotLeg);
        }

        /// <summary>
        /// A sword out of anyone's hand (dropped, lying on the ground, standing in it): its grip in the
        /// world, the way its blade points (a world angle, clockwise from +x).
        /// </summary>
        public static Part LooseSword(int sword, Vector2 grip, float blade)
        {
            // A world angle is the angle facing right; the picture drawn unmirrored.
            return SwordPart(sword == Sun ? SunBlade : GraceBlade, grip, blade, 1f, 1, false);
        }

        /// <summary>A sword, its grip at <paramref name="grip"/> (in the world), pointing <paramref name="blade"/> (facing right), seen this much of its length.</summary>
        private static Part SwordPart(int piece, Vector2 grip, float blade, float reach, int d, bool shadow)
        {
            // Drawn point up: a blade pointing at angle a is the picture turned by a and a quarter.
            return Piece(piece, grip, blade + MathHelper.PiOver2, d, new Vector2(1f, reach), shadow);
        }

        /// <summary>A blade's angle brought within half a turn of <paramref name="near"/>, by whole turns.</summary>
        public static float Nearest(float angle, float near) => near + MathHelper.WrapAngle(angle - near);
    }
}
