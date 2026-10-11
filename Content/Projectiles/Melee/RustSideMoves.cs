using System;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>What Foigh's Rust Side Sword is doing in the hand.</summary>
    public enum RustMove : byte
    {
        /// <summary>Held, not in use: on the shoulder.</summary>
        Rest,

        /// <summary>The first cut: drawn back and down behind, brought up through the front.</summary>
        Up,

        /// <summary>The second: from over the head, down through the front.</summary>
        Down,

        /// <summary>The third, without the blood: all the way round, where its wielder stands.</summary>
        Spin,

        /// <summary>The fourth (the third with the blood): raised behind the head and brought down into the ground.</summary>
        Smash,

        /// <summary>The fifth, without the blood: drawn back to the hip and driven straight ahead.</summary>
        Pierce,

        /// <summary>Right click: a leap and a roll in the air, into a cut that lets a slash go.</summary>
        Air1,

        /// <summary>The second in the air: another roll, a rising cut, another slash.</summary>
        Air2,

        /// <summary>The third in the air: two rolls, a cut from far back, the great slash.</summary>
        Air3,

        /// <summary>With the blood, the fourth: a leap at the nearest enemy, a roll over, and a cut down onto it.</summary>
        Leap,

        /// <summary>After the leap, the button still held, off the ground: down onto the point.</summary>
        Dive,

        /// <summary>After the leap, the button still held, on the ground: the sword driven in, and the straight cuts out of the ground.</summary>
        Drive,

        /// <summary>Enter held: the sword upright before the face, three seconds.</summary>
        Focus,
    }

    /// <summary>How far out the front arm is, as the game draws it (Player.CompositeArmStretchAmount).</summary>
    public enum RustReach : byte
    {
        Full,
        ThreeQuarters,
        Quarter,
        None,
    }

    /// <summary>
    /// How Foigh's Rust Side Sword moves: every pose it is ever in, as numbers and nothing else,
    /// so that they can be read, changed and looked at outside the game (IronFlowMoves' way, in
    /// pieces of curves: Easing).
    ///
    /// Angles are in degrees, in the player's own frame (VisusGrip: facing right, the right way
    /// up, from straight ahead, down positive): the blade's, from the hand, and the front arm's,
    /// from the shoulder. Progress is the share of a move that is done, 0 to 1. A roll is the
    /// whole body turned over, forward, in degrees (the holdout makes it the player's
    /// fullRotation, the right way for the side they face).
    ///
    /// AT REST it lies on the shoulder: the hand before the chest, the blade up and back over
    /// the shoulder, behind the head. The sword is 88 px from the grip to the point (its art:
    /// FoighRig), longer than its wielder is tall, so it is carried, not hung.
    ///
    /// THE CHAIN is five cuts, each from where the last left the blade: Up (drawn back and down
    /// behind, then up through the front to over the head; a tile forward), Down (cocked a
    /// little further, then down through the front; half a tile), Spin (on round from there, a
    /// turn and a quarter, left trailing behind; no step), Smash (raised behind the head, held,
    /// and brought down over the top into the ground ahead, fast at the end; a tile and a half) and
    /// Pierce (drawn back to the hip, level, and driven straight ahead; two tiles). With the
    /// blood (BloodDrenchedSword) it is Up, Down, Smash and Leap: at the nearest enemy, a roll
    /// over in the air and a cut down onto it; and, the button still held, Dive and Drive.
    ///
    /// THE AIR (right click) is up to three: each a roll forward, then a cut aimed at the
    /// cursor (its <c>aim</c>, an angle in the same frame) that lets a slash go as the blade
    /// passes the aim. The thrust is aimed too, turned whole about the shoulder: at the enemy
    /// the chain is cut at, where it has one (RustSideHoldout, the chain's aim), and level
    /// where it has none.
    ///
    /// Lengths: the item's use time (its swing) for Up and Down, more for the rest
    /// (<see cref="Length"/>); the leap by how far it goes; the drive, the dive and the focus
    /// their own ticks whatever the wielder's speed.
    /// </summary>
    public static class RustSideMoves
    {
        /// <summary>On the shoulder.</summary>
        public const float RestBlade = -130f;
        public const float RestArm = 40f;

        /// <summary>Held out from the body while it rolls, so the roll swings it round.</summary>
        public const float RollBlade = 35f;
        public const float RollArm = 50f;

        /// <summary>Upright before the face, focusing.</summary>
        public const float FocusBlade = -90f;
        public const float FocusArm = -30f;

        /// <summary>Straight down: the dive and the drive.</summary>
        public const float PlungeBlade = 90f;
        public const float PlungeArm = 80f;

        /// <summary>Ticks of the moves that are not cuts of the item's: the drive, the longest dive, and the focus.</summary>
        public const int DriveTicks = 70;
        public const int DiveTicks = 120;
        public const int FocusTicks = 180;

        /// <summary>The leap's ticks: from so few, and so many more for every so many pixels it goes, to so many.</summary>
        public const int LeapLeast = 20;
        public const int LeapMost = 42;
        public const float LeapPixelsATick = 14f;

        /// <summary>How far each cut carries its wielder forward, in pixels: a tile, half a tile, none, a tile and a half, two tiles.</summary>
        public const float UpStep = 16f, DownStep = 8f, SmashStep = 24f, PierceStep = 32f;

        /// <summary>How long a cut is against the item's use time.</summary>
        public static float Length(RustMove move) => move switch
        {
            RustMove.Spin => 1.5f,
            RustMove.Smash => 1.6f,
            RustMove.Pierce => 1.3f,
            RustMove.Air1 => 1.25f,
            RustMove.Air3 => 1.5f,
            _ => 1f,
        };

        /// <summary>Whether a move is the item's (its use lasts exactly as long), not one of the sword's own (the dive, the drive, the focus).</summary>
        public static bool OfTheItem(RustMove move) => move != RustMove.Rest && move != RustMove.Dive && move != RustMove.Drive && move != RustMove.Focus;

        /// <summary>One of the three in the air.</summary>
        public static bool InAir(RustMove move) => move == RustMove.Air1 || move == RustMove.Air2 || move == RustMove.Air3;

        /// <summary>The ticks of a leap that goes this far.</summary>
        public static int LeapTicks(float distance) => Math.Clamp((int)Math.Round(LeapLeast + distance / LeapPixelsATick * 0.5f), LeapLeast, LeapMost);

        // ---- the cuts, in pieces --------------------------------------------------------------

        private static readonly Piece[] UpBlade =
        {
            new Piece(Ease.SineInOut, 0f, RestBlade, -90f),
            new Piece(Ease.PolyOut, 0.30f, -220f, -210f, 2),
            new Piece(Ease.SineOut, 0.62f, -430f, -15f),
        };

        private static readonly Piece[] UpArm =
        {
            new Piece(Ease.SineInOut, 0f, RestArm, 65f),
            new Piece(Ease.PolyOut, 0.30f, 105f, -180f, 2),
            new Piece(Ease.SineOut, 0.62f, -75f, -8f),
        };

        /// <summary>Where the cut up leaves it: over the head, the point a little forward.</summary>
        public const float HighBlade = -85f;
        public const float HighArm = -83f;

        private static readonly Piece[] DownBlade =
        {
            new Piece(Ease.SineInOut, 0f, HighBlade, -35f),
            new Piece(Ease.PolyOut, 0.28f, -120f, 188f, 2),
            new Piece(Ease.SineInOut, 0.60f, 68f, 8f),
        };

        private static readonly Piece[] DownArm =
        {
            new Piece(Ease.SineInOut, 0f, HighArm, -20f),
            new Piece(Ease.PolyOut, 0.28f, -103f, 158f, 2),
            new Piece(Ease.SineInOut, 0.60f, 55f, 5f),
        };

        /// <summary>Where the cut down leaves it: low before the feet.</summary>
        public const float LowBlade = 76f;
        public const float LowArm = 60f;

        // On round from there, the arm out along the blade the whole way: down, back, up, over,
        // down and back again, a turn and a quarter, and left trailing behind, low (530 is 170,
        // back and a little down; the arm under it).
        private static readonly Piece[] SpinBlade =
        {
            new Piece(Ease.SineIn, 0f, LowBlade, 8f),
            new Piece(Ease.SineInOut, 0.08f, 84f, 436f),
            new Piece(Ease.SineOut, SpinTo, 520f, 10f),
        };

        private static readonly Piece[] SpinArm =
        {
            new Piece(Ease.SineIn, 0f, LowArm, 5f),
            new Piece(Ease.SineInOut, 0.08f, 65f, 436f),
            new Piece(Ease.SineOut, SpinTo, 501f, 5f),
        };

        /// <summary>The spin: round by.</summary>
        public const float SpinTo = 0.86f;

        /// <summary>Where the spin leaves it: trailing behind, low; and where the smash begins.</summary>
        public const float TrailBlade = 170f;
        public const float TrailArm = 146f;

        // From low behind (the spin's end; or the cut down's, swung back along the ground),
        // raised: the arms up and back over the head, the blade laid back behind; held; and down
        // over the top, slow, then faster and faster into the ground ahead, where it stays a
        // moment. (374 is 14: ahead and a little down.)
        private static readonly Piece[] SmashBlade =
        {
            new Piece(Ease.SineInOut, 0f, TrailBlade, -12f),
            new Piece(Ease.SineInOut, SmashRaisedBy, 158f, -4f),
            new Piece(Ease.PolyIn, SmashFrom, 154f, 220f, 3),
            new Piece(Ease.SineOut, SmashLands, 374f, 4f),
            new Piece(Ease.SineInOut, 0.82f, 378f, -10f),
        };

        private static readonly Piece[] SmashArm =
        {
            new Piece(Ease.SineInOut, 0f, TrailArm, 104f),
            new Piece(Ease.SineInOut, SmashRaisedBy, 250f, -3f),
            new Piece(Ease.PolyIn, SmashFrom, 247f, 175f, 3),
            new Piece(Ease.SineOut, SmashLands, 422f, 2f),
            new Piece(Ease.SineInOut, 0.82f, 424f, -4f),
        };

        /// <summary>The smash: raised by, brought down from, and in the ground at.</summary>
        public const float SmashRaisedBy = 0.34f;
        public const float SmashFrom = 0.44f;
        public const float SmashLands = 0.62f;

        // Drawn back level to the hip, then out at once, the blade level all the while.
        private static readonly Piece[] PierceBlade =
        {
            new Piece(Ease.SineInOut, 0f, 8f, -12f),
            new Piece(Ease.Linear, PierceFrom, -4f, 2f),
            new Piece(Ease.SineInOut, 0.56f, -2f, 0f),
            new Piece(Ease.SineInOut, 0.80f, -2f, -28f),
        };

        private static readonly Piece[] PierceArm =
        {
            new Piece(Ease.SineInOut, 0f, 60f, 100f),
            new Piece(Ease.PolyOut, PierceFrom, 160f, -152f, 3),
            new Piece(Ease.SineInOut, 0.56f, 8f, 0f),
            new Piece(Ease.SineInOut, 0.80f, 8f, 32f),
        };

        /// <summary>The thrust: drawn back by, and out until.</summary>
        public const float PierceFrom = 0.36f;
        public const float PierceOut = 0.80f;

        // Up as the knees bend, held up through the leap and the roll over, and down onto them.
        private static readonly Piece[] LeapBlade =
        {
            new Piece(Ease.SineInOut, 0f, RestBlade, 20f),
            new Piece(Ease.SineInOut, LeapOff, -110f, -15f),
            new Piece(Ease.PolyIn, LeapStrikes, -125f, 205f, 2),
            new Piece(Ease.SineOut, LeapLands, 80f, 4f),
        };

        private static readonly Piece[] LeapArm =
        {
            new Piece(Ease.SineInOut, 0f, RestArm, -140f),
            new Piece(Ease.SineInOut, LeapOff, -100f, -12f),
            new Piece(Ease.PolyIn, LeapStrikes, -112f, 174f, 2),
            new Piece(Ease.SineOut, LeapLands, 62f, 2f),
        };

        /// <summary>The leap: off the ground at, the flight's end, the cut down from, and down at.</summary>
        public const float LeapOff = 0.15f;
        public const float LeapArrives = 0.88f;
        public const float LeapStrikes = 0.75f;
        public const float LeapLands = 0.92f;

        // Straight down: driven in, held while the cuts open, and torn out and up.
        private static readonly Piece[] DriveBlade =
        {
            new Piece(Ease.SineOut, 0f, PlungeBlade, 0f),
            new Piece(Ease.PolyIn, DriveTears, PlungeBlade, -150f, 2),
        };

        private static readonly Piece[] DriveArm =
        {
            new Piece(Ease.SineOut, 0f, PlungeArm, 12f),
            new Piece(Ease.PolyOut, DriveTears, PlungeArm + 12f, -130f, 2),
        };

        /// <summary>The drive: torn out from.</summary>
        public const float DriveTears = 0.86f;

        private static readonly Piece[] FocusBladePieces =
        {
            new Piece(Ease.SineInOut, 0f, RestBlade, FocusBlade - RestBlade),
            new Piece(Ease.Linear, FocusRaisedBy, FocusBlade, 0f),
        };

        private static readonly Piece[] FocusArmPieces =
        {
            new Piece(Ease.SineInOut, 0f, RestArm, FocusArm - RestArm),
            new Piece(Ease.Linear, FocusRaisedBy, FocusArm, 0f),
        };

        /// <summary>The focus: raised before the face by.</summary>
        public const float FocusRaisedBy = 0.10f;

        // ---- the three in the air: a roll, then a cut aimed at the cursor ----------------------

        /// <summary>The roll forward: from, to, and how far (degrees).</summary>
        private static void RollOf(RustMove move, out float from, out float to, out float turn)
        {
            switch (move)
            {
                case RustMove.Air1: from = 0.04f; to = 0.50f; turn = 360f; return;
                case RustMove.Air2: from = 0.02f; to = 0.40f; turn = 360f; return;
                case RustMove.Air3: from = 0.02f; to = 0.46f; turn = 720f; return;
                case RustMove.Leap: from = 0.18f; to = 0.66f; turn = 360f; return;
                default: from = 0f; to = 0f; turn = 0f; return;
            }
        }

        /// <summary>
        /// A cut in the air: where the wind starts and the cut starts and ends, and the blade's
        /// and the arm's angles from the aim at the cut's start and end, and where they end up.
        /// The second is a rising cut (it starts below the aim); the first and the third fall.
        /// </summary>
        private static void AirCut(RustMove move, out float wind, out float cut, out float done,
            out float bladeFrom, out float bladeTo, out float bladeLast, out float armFrom, out float armTo, out float armLast)
        {
            switch (move)
            {
                case RustMove.Air2:
                    wind = 0.36f; cut = 0.52f; done = 0.70f;
                    bladeFrom = 115f; bladeTo = -70f; bladeLast = -80f;
                    armFrom = 100f; armTo = -60f; armLast = -65f;
                    return;

                case RustMove.Air3:
                    wind = 0.42f; cut = 0.58f; done = 0.74f;
                    bladeFrom = -155f; bladeTo = 75f; bladeLast = 85f;
                    armFrom = -120f; armTo = 60f; armLast = 62f;
                    return;

                default:
                    wind = 0.46f; cut = 0.60f; done = 0.76f;
                    bladeFrom = -110f; bladeTo = 70f; bladeLast = 80f;
                    armFrom = -85f; armTo = 55f; armLast = 60f;
                    return;
            }
        }

        private static float Seg(float p, float a, float b, float from, float to, Ease ease, int degree = 1)
        {
            float t = b <= a ? 1f : (p - a) / (b - a);
            return from + (to - from) * Easing.Apply(ease, t, degree);
        }

        private static float AirAngle(RustMove move, float progress, float aim, bool blade)
        {
            AirCut(move, out float wind, out float cut, out float done,
                out float bladeFrom, out float bladeTo, out float bladeLast, out float armFrom, out float armTo, out float armLast);

            float held = blade ? RollBlade : RollArm;
            float from = aim + (blade ? bladeFrom : armFrom);
            float to = aim + (blade ? bladeTo : armTo);
            float last = aim + (blade ? bladeLast : armLast);

            if (progress < wind)
                return held;

            if (progress < cut)
                return Seg(progress, wind, cut, held, from, Ease.SineInOut);

            if (progress < done)
                return Seg(progress, cut, done, from, to, move == RustMove.Air3 ? Ease.PolyIn : Ease.PolyOut, 2);

            return Seg(Math.Min(progress, 1f), done, 1f, to, last, Ease.SineOut);
        }

        // ---- what everything else asks ---------------------------------------------------------

        /// <summary>
        /// The blade's angle, in degrees, in a move begun from where that move expects to begin
        /// (<see cref="StartBlade"/>). It goes past a whole turn in some: it is the turning that
        /// matters, not the number. <paramref name="aim"/> is for the three in the air and the thrust.
        /// </summary>
        public static float Blade(RustMove move, float progress, float aim = 0f) => move switch
        {
            RustMove.Up => Easing.At(progress, UpBlade),
            RustMove.Down => Easing.At(progress, DownBlade),
            RustMove.Spin => Easing.At(progress, SpinBlade),
            RustMove.Smash => Easing.At(progress, SmashBlade),
            RustMove.Pierce => Easing.At(progress, PierceBlade) + aim,
            RustMove.Air1 or RustMove.Air2 or RustMove.Air3 => AirAngle(move, progress, aim, true),
            RustMove.Leap => Easing.At(progress, LeapBlade),
            RustMove.Dive => PlungeBlade,
            RustMove.Drive => Easing.At(progress, DriveBlade),
            RustMove.Focus => Easing.At(progress, FocusBladePieces),
            _ => RestBlade,
        };

        /// <summary>The front arm's angle, in degrees.</summary>
        public static float Arm(RustMove move, float progress, float aim = 0f) => move switch
        {
            RustMove.Up => Easing.At(progress, UpArm),
            RustMove.Down => Easing.At(progress, DownArm),
            RustMove.Spin => Easing.At(progress, SpinArm),
            RustMove.Smash => Easing.At(progress, SmashArm),
            RustMove.Pierce => Easing.At(progress, PierceArm) + aim,
            RustMove.Air1 or RustMove.Air2 or RustMove.Air3 => AirAngle(move, progress, aim, false),
            RustMove.Leap => Easing.At(progress, LeapArm),
            RustMove.Dive => PlungeArm,
            RustMove.Drive => Easing.At(progress, DriveArm),
            RustMove.Focus => Easing.At(progress, FocusArmPieces),
            _ => RestArm,
        };

        /// <summary>How far the whole body is turned over, forward, in degrees: 0 but in the air and the leap.</summary>
        public static float Roll(RustMove move, float progress)
        {
            RollOf(move, out float from, out float to, out float turn);

            if (turn == 0f || progress <= from)
                return 0f;

            // Over: the right way up, which is no turn at all (the roll is whole turns).
            if (progress >= to)
                return 0f;

            return turn * Easing.Apply(Ease.SineInOut, (progress - from) / (to - from));
        }

        /// <summary>How far out the front arm is.</summary>
        public static RustReach Reach(RustMove move, float progress) => move switch
        {
            RustMove.Up => progress < 0.30f ? RustReach.ThreeQuarters : RustReach.Full,
            RustMove.Down => progress < 0.28f ? RustReach.ThreeQuarters : RustReach.Full,
            RustMove.Spin => RustReach.Full,
            RustMove.Smash => progress < SmashRaisedBy ? RustReach.ThreeQuarters : RustReach.Full,
            RustMove.Pierce => progress < PierceFrom ? RustReach.Quarter : progress < PierceOut ? RustReach.Full : RustReach.ThreeQuarters,
            RustMove.Air1 or RustMove.Air2 or RustMove.Air3 => RustReach.Full,
            RustMove.Leap => progress < LeapStrikes ? RustReach.ThreeQuarters : RustReach.Full,
            RustMove.Dive or RustMove.Drive => RustReach.Full,
            _ => RustReach.Quarter,
        };

        /// <summary>Where a move expects the blade and the arm to be when it begins (the thrust turned to its aim).</summary>
        public static float StartBlade(RustMove move, float aim = 0f) => move switch
        {
            RustMove.Down => HighBlade,
            RustMove.Spin => LowBlade,
            RustMove.Smash => TrailBlade,
            RustMove.Pierce => 8f + aim,
            RustMove.Air1 or RustMove.Air2 or RustMove.Air3 => RollBlade,
            RustMove.Dive or RustMove.Drive => PlungeBlade,
            _ => RestBlade,
        };

        public static float StartArm(RustMove move, float aim = 0f) => move switch
        {
            RustMove.Down => HighArm,
            RustMove.Spin => LowArm,
            RustMove.Smash => TrailArm,
            RustMove.Pierce => 60f + aim,
            RustMove.Air1 or RustMove.Air2 or RustMove.Air3 => RollArm,
            RustMove.Dive or RustMove.Drive => PlungeArm,
            _ => RestArm,
        };

        /// <summary>
        /// The share of a move by which the blade, begun from anywhere else, is on the move's
        /// own path: the end of its wind, before it is fast.
        /// </summary>
        public static float JoinedBy(RustMove move) => move switch
        {
            RustMove.Up => 0.30f,
            RustMove.Down => 0.28f,
            RustMove.Spin => 0.08f,
            RustMove.Smash => SmashRaisedBy,
            RustMove.Pierce => PierceFrom,
            RustMove.Air1 => 0.10f,
            RustMove.Air2 or RustMove.Air3 => 0.08f,
            RustMove.Leap => LeapOff,
            RustMove.Dive => 0.06f,
            RustMove.Drive => 0.04f,
            RustMove.Focus => FocusRaisedBy,
            _ => 0f,
        };

        /// <summary>The part of the move the blade is fast through: what it leaves a trail over.</summary>
        public static void Fast(RustMove move, out float from, out float to)
        {
            switch (move)
            {
                case RustMove.Up: from = 0.30f; to = 0.62f; return;
                case RustMove.Down: from = 0.28f; to = 0.60f; return;
                case RustMove.Spin: from = 0.08f; to = SpinTo; return;
                case RustMove.Smash: from = SmashFrom; to = SmashLands + 0.01f; return;
                case RustMove.Pierce: from = PierceFrom; to = 0.58f; return;
                case RustMove.Air1: from = 0.10f; to = 0.78f; return;
                case RustMove.Air2: from = 0.08f; to = 0.72f; return;
                case RustMove.Air3: from = 0.08f; to = 0.76f; return;
                case RustMove.Leap: from = 0.18f; to = 0.94f; return;
                case RustMove.Drive: from = DriveTears; to = 0.98f; return;
                default: from = 0f; to = 0f; return;
            }
        }

        /// <summary>Whether the blade cuts at this point of the move. In the air the slashes do the cutting, not the blade.</summary>
        public static bool Cuts(RustMove move, float progress) => move switch
        {
            RustMove.Up => progress >= 0.30f && progress <= 0.64f,
            RustMove.Down => progress >= 0.28f && progress <= 0.62f,
            RustMove.Spin => progress >= 0.08f && progress <= SpinTo,
            RustMove.Smash => progress >= SmashFrom && progress <= SmashLands + 0.02f,
            RustMove.Pierce => progress >= PierceFrom && progress <= 0.62f,
            RustMove.Leap => progress >= LeapStrikes && progress <= LeapLands + 0.02f,
            RustMove.Dive => true,
            RustMove.Drive => progress <= 0.08f,
            _ => false,
        };

        /// <summary>Swung with both hands (the other on the grip below the first): the smash, the drive, the dive, the focus, and the leap's cut down.</summary>
        public static bool TwoHands(RustMove move, float progress) => move switch
        {
            RustMove.Smash or RustMove.Drive or RustMove.Dive or RustMove.Focus => true,
            RustMove.Leap => progress >= LeapStrikes,
            _ => false,
        };

        /// <summary>A thrust: the blade goes along itself, not across.</summary>
        public static bool Thrusts(RustMove move) => move == RustMove.Pierce;

        /// <summary>The share of an air cut at which its slash is let go (as the blade passes the aim); none for anything else.</summary>
        public static float Release(RustMove move) => move switch
        {
            RustMove.Air1 => 0.66f,
            RustMove.Air2 => 0.60f,
            RustMove.Air3 => 0.66f,
            _ => -1f,
        };

        /// <summary>The share of an air cut at which the cut itself begins (after the roll and the wind): its aim is held from there.</summary>
        public static float CutFrom(RustMove move)
        {
            AirCut(move, out _, out float cut, out _, out _, out _, out _, out _, out _, out _);
            return cut;
        }

        /// <summary>What an air cut's slash does, against the sword's own damage: the first two half as much again, the third three times.</summary>
        public static float SlashShare(RustMove move) => move == RustMove.Air3 ? 3f : 1.5f;

        /// <summary>Whether its wielder hangs in the air at this point of a move (falls slowly), and is popped up as it begins.</summary>
        public static bool Hovers(RustMove move, float progress) => move switch
        {
            RustMove.Air1 => progress >= 0.40f,
            RustMove.Air2 => true,
            RustMove.Air3 => progress <= 0.80f,
            _ => false,
        };

        /// <summary>How far this cut carries its wielder forward, in pixels, and the part of the cut it does it over.</summary>
        public static float Step(RustMove move, out float from, out float length)
        {
            switch (move)
            {
                case RustMove.Up: from = 0.26f; length = 0.38f; return UpStep;
                case RustMove.Down: from = 0.24f; length = 0.36f; return DownStep;
                case RustMove.Smash: from = 0.40f; length = 0.24f; return SmashStep;
                case RustMove.Pierce: from = 0.34f; length = 0.26f; return PierceStep;
                default: from = 0f; length = 0f; return 0f;
            }
        }

        /// <summary>The cut a left click gets after this one. Without the blood: up, down, round, smash, pierce. With it: up, down, smash, leap.</summary>
        public static RustMove After(RustMove cut, bool drenched) => cut switch
        {
            RustMove.Up => RustMove.Down,
            RustMove.Down => drenched ? RustMove.Smash : RustMove.Spin,
            RustMove.Spin => RustMove.Smash,
            RustMove.Smash => drenched ? RustMove.Leap : RustMove.Pierce,
            _ => RustMove.Up,
        };

        /// <summary>The air cut after this one: the first, the second, the third, and then none (<see cref="RustMove.Rest"/>).</summary>
        public static RustMove AfterInAir(RustMove move) => move switch
        {
            RustMove.Air1 => RustMove.Air2,
            RustMove.Air2 => RustMove.Air3,
            RustMove.Air3 => RustMove.Rest,
            _ => RustMove.Air1,
        };

        /// <summary>The angle between -180 and 180 that points the same way.</summary>
        public static float Wrap(float degrees)
        {
            degrees %= 360f;

            if (degrees > 180f)
                degrees -= 360f;
            else if (degrees <= -180f)
                degrees += 360f;

            return degrees;
        }
    }

    /// <summary>
    /// The Rust Side Sword from tick to tick: which move it is in, how far into it, and where
    /// the blade, the arm and the roll are. One for each sword on each machine; nothing of the
    /// game in it, so that it can be run outside it (IronFlowState's way).
    ///
    /// A move is begun from wherever the blade is, and joins its own path over its wind, the
    /// shorter way round. With no move under way the blade is where the last left it, held
    /// <see cref="HoldTicks"/> ticks for the next cut to follow from, then laid back on the
    /// shoulder over <see cref="LowerTicks"/>, the shorter way round.
    /// </summary>
    public sealed class RustSideState
    {
        public const int HoldTicks = 10;
        public const int LowerTicks = 20;

        public RustMove Move { get; private set; }

        public bool Busy { get; private set; }

        /// <summary>Ticks into the move, and the ticks it takes.</summary>
        public float Timer { get; private set; }
        public float Duration { get; private set; } = 1f;

        /// <summary>The way an air cut or the thrust is aimed, in the player's own frame, degrees.</summary>
        public float Aim { get; private set; }

        public float Progress => Busy ? Math.Min(1f, Timer / Duration) : 0f;

        /// <summary>The blade's angle this tick and last, the arm's, and the roll's this tick and last, in degrees.</summary>
        public float Blade { get; private set; } = RustSideMoves.RestBlade;
        public float PreviousBlade { get; private set; } = RustSideMoves.RestBlade;
        public float Arm { get; private set; } = RustSideMoves.RestArm;
        public float Roll { get; private set; }
        public float PreviousRoll { get; private set; }

        public RustReach Reach { get; private set; } = RustReach.Quarter;

        public bool Cuts => Busy && RustSideMoves.Cuts(Move, Progress);

        private float offBlade;
        private float offArm;

        private float idle;
        private float leftBlade = RustSideMoves.RestBlade;
        private float leftArm = RustSideMoves.RestArm;
        private RustReach leftReach = RustReach.Quarter;

        /// <summary>Ticks since the last move ended; none while one is under way.</summary>
        public float Idle => Busy ? 0f : idle;

        /// <summary>The move that ended last, and whether it ran to its end.</summary>
        public RustMove Last { get; private set; }

        /// <summary>Begins a move, from wherever the blade is, taking this many ticks.</summary>
        public void Begin(RustMove move, float ticks, float aim = 0f)
        {
            if (move == RustMove.Rest)
            {
                Stop();
                return;
            }

            offBlade = RustSideMoves.Wrap(Blade - RustSideMoves.StartBlade(move, aim));
            offArm = RustSideMoves.Wrap(Arm - RustSideMoves.StartArm(move, aim));

            Move = move;
            Busy = true;
            Timer = 0f;
            Duration = Math.Max(1f, ticks);
            Aim = aim;

            Blade = PreviousBlade = RustSideMoves.StartBlade(move, aim) + offBlade;
            Arm = RustSideMoves.StartArm(move, aim) + offArm;
            Roll = PreviousRoll = 0f;
        }

        /// <summary>The way a cut in the air is aimed, as its owner moves it (until the cut begins).</summary>
        public void SetAim(float aim) => Aim = aim;

        /// <summary>One tick on.</summary>
        public void Tick()
        {
            PreviousBlade = Blade;
            PreviousRoll = Roll;

            if (Busy)
            {
                Timer++;

                float progress = Math.Min(1f, Timer / Duration);
                float joined = RustSideMoves.JoinedBy(Move);
                float apart = joined <= 0f ? 0f : 1f - Easing.Smoother(progress / joined);

                Blade = RustSideMoves.Blade(Move, progress, Aim) + offBlade * apart;
                Arm = RustSideMoves.Arm(Move, progress, Aim) + offArm * apart;
                Roll = RustSideMoves.Roll(Move, progress);
                Reach = RustSideMoves.Reach(Move, progress);

                return;
            }

            idle++;
            Roll = 0f;

            float lowered = Easing.Apply(Ease.SineInOut, (idle - HoldTicks) / LowerTicks);

            // Back onto the shoulder the shorter way round.
            Blade = leftBlade + RustSideMoves.Wrap(RustSideMoves.RestBlade - leftBlade) * lowered;
            Arm = leftArm + RustSideMoves.Wrap(RustSideMoves.RestArm - leftArm) * lowered;
            Reach = lowered < 0.5f ? leftReach : RustReach.Quarter;
        }

        /// <summary>After the tick's pose has been used: a move that has run its ticks is over, and the blade is left where it put it. Whether one just was.</summary>
        public bool Finish()
        {
            if (!Busy || Timer < Duration)
                return false;

            End();
            return true;
        }

        /// <summary>The move ends now, wherever it is (told to by its owner: let go of, or landed).</summary>
        public void Stop()
        {
            if (Busy)
                End();
        }

        private void End()
        {
            Last = Move;
            Busy = false;
            Move = RustMove.Rest;
            idle = 0f;
            Roll = 0f;

            Blade = PreviousBlade = leftBlade = RustSideMoves.Wrap(Blade);
            Arm = leftArm = RustSideMoves.Wrap(Arm);
            leftReach = Reach;
        }
    }
}
