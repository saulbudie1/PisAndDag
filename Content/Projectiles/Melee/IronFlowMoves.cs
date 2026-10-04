using System;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>What the Iron Flow is doing in the hand.</summary>
    public enum FlowMove : byte
    {
        /// <summary>Held, not in use.</summary>
        Rest,

        /// <summary>The first cut: raised, and brought down.</summary>
        Down,

        /// <summary>The second: up, from where the first left it.</summary>
        Up,

        /// <summary>The third: all the way round, from where the second left it.</summary>
        Circle,

        /// <summary>The parry: once round the other way, and a flick.</summary>
        Parry,
    }

    /// <summary>
    /// How the Iron Flow moves: every pose it is ever in, as numbers and nothing else, so they
    /// can be read, changed and looked at outside the game. Written as Visus's blow is, as
    /// curves in pieces (Easing).
    ///
    /// Angles are in degrees, in the player's own frame (VisusGrip: facing right, the right way
    /// up, from straight ahead, down positive): the blade's, from the hand, and the front
    /// arm's, from the shoulder. Progress is the share of a move that is done, 0 to 1.
    ///
    /// AT REST it hangs: the arm down and a little forward, the hand at the hip, the blade
    /// pointing down and ahead, its hook by the feet.
    ///
    /// THE CUTS are three, one after another, each from where the last left the blade. Down:
    /// raised through the front to over the shoulder, hung there a moment, brought down
    /// through the front to where it hangs. Up: dipped a little, brought up through the front
    /// and over the shoulder, and left there (<see cref="HighBlade"/>). The circle: drawn back
    /// a little further, then all the way round, down through the front, under, up behind and
    /// over, and down the front again to where it hangs: a turn and a half, the arm going
    /// round with it. The first two take the item's use time (24 ticks, less with melee
    /// speed), the circle half as long again (<see cref="CircleLength"/>).
    ///
    /// THE PARRY takes its own ticks whatever the wielder's speed (ParryTiming.CurvedSword).
    /// The blade is cocked a little lower over the wind; it goes round the other way, up
    /// through the front and over the back, <see cref="SpinAngle"/> degrees over the ticks the
    /// parry is active and on for <see cref="SpinsOn"/> more as it slows; that leaves it a
    /// flick above where it hangs, and it is lowered to rest over what is left: the time its
    /// wielder is open. The arm stays out in front: it is the wrist that turns.
    /// </summary>
    public static class IronFlowMoves
    {
        /// <summary>Where it hangs.</summary>
        public const float RestBlade = 40f;
        public const float RestArm = 60f;

        /// <summary>Where the cut up leaves it: over the shoulder.</summary>
        public const float HighBlade = -135f;
        public const float HighArm = -100f;

        /// <summary>The cut down, in shares of it: raised by, brought down from, through by.</summary>
        public const float DownRaisedBy = 0.25f;
        public const float DownFrom = 0.30f;
        public const float DownTo = 0.62f;

        /// <summary>The cut up: dipped by, brought up from, through by.</summary>
        public const float UpDippedBy = 0.20f;
        public const float UpFrom = 0.25f;
        public const float UpTo = 0.60f;

        /// <summary>The circle: drawn back by (and round from), round by.</summary>
        public const float CircleFrom = 0.14f;
        public const float CircleTo = 0.72f;

        /// <summary>How much longer the circle takes than the other two cuts.</summary>
        public const float CircleLength = 1.5f;

        /// <summary>How far the blade turns while the parry is active, in degrees: three quarters of the way round.</summary>
        public const float SpinAngle = 270f;

        /// <summary>
        /// Ticks the spin goes on for after the parry stops being active: as many as let it
        /// leave those ticks at the speed it had in them and slow from there.
        /// </summary>
        public const int SpinsOn = 6;

        /// <summary>The shares of the parry at which it becomes active, stops being, and the spin ends.</summary>
        public static readonly float ParryFrom = ParryTiming.CurvedSword.Startup / (float)ParryTiming.CurvedSword.Total;
        public static readonly float ParryTo = (ParryTiming.CurvedSword.Startup + ParryTiming.CurvedSword.Active) / (float)ParryTiming.CurvedSword.Total;
        public static readonly float SpinTo = (ParryTiming.CurvedSword.Startup + ParryTiming.CurvedSword.Active + SpinsOn) / (float)ParryTiming.CurvedSword.Total;

        /// <summary>
        /// How far each cut carries its user forward, in pixels (a block, half a block, a block
        /// and a half), and the part of the cut it does it over: from, and for.
        /// </summary>
        public const float DownStep = 16f, UpStep = 8f, CircleStep = 24f;

        private static readonly Piece[] DownBlade =
        {
            new Piece(Ease.SineInOut, 0f, RestBlade, -178f),
            new Piece(Ease.SineInOut, DownRaisedBy, -138f, -4f),
            new Piece(Ease.PolyOut, DownFrom, -142f, 192f, 2),
            new Piece(Ease.SineInOut, DownTo, 50f, -10f),
        };

        private static readonly Piece[] DownArm =
        {
            new Piece(Ease.SineInOut, 0f, RestArm, -164f),
            new Piece(Ease.SineInOut, DownRaisedBy, -104f, -2f),
            new Piece(Ease.PolyOut, DownFrom, -106f, 160f, 2),
            new Piece(Ease.SineInOut, DownTo, 54f, 6f),
        };

        private static readonly Piece[] UpBlade =
        {
            new Piece(Ease.PolyOut, 0f, RestBlade, 22f, 2),
            new Piece(Ease.SineInOut, UpDippedBy, 62f, 3f),
            new Piece(Ease.PolyOut, UpFrom, 65f, -190f, 2),
            new Piece(Ease.SineOut, UpTo, -125f, -10f),
        };

        private static readonly Piece[] UpArm =
        {
            new Piece(Ease.PolyOut, 0f, RestArm, 12f, 2),
            new Piece(Ease.SineInOut, UpDippedBy, 72f, 0f),
            new Piece(Ease.PolyOut, UpFrom, 72f, -167f, 2),
            new Piece(Ease.SineOut, UpTo, -95f, -5f),
        };

        // Round from 150 above and behind to 10 short of a turn and a half on, which is 10
        // above where it hangs; down those 10.
        private static readonly Piece[] CircleBlade =
        {
            new Piece(Ease.PolyOut, 0f, HighBlade, -15f, 2),
            new Piece(Ease.SineInOut, CircleFrom, -150f, 540f),
            new Piece(Ease.SineInOut, CircleTo, 390f, 10f),
        };

        private static readonly Piece[] CircleArm =
        {
            new Piece(Ease.PolyOut, 0f, HighArm, -8f, 2),
            new Piece(Ease.SineInOut, CircleFrom, -108f, 520f),
            new Piece(Ease.SineInOut, CircleTo, 412f, 8f),
        };

        // Cocked to 60 below level; round from there, up through the front; on to 25 short of
        // a whole turn from where it hangs, which is 25 above it; down those 25.
        private static readonly Piece[] ParryBlade =
        {
            new Piece(Ease.PolyOut, 0f, RestBlade, 20f, 2),
            new Piece(Ease.Linear, ParryFrom, 60f, -SpinAngle),
            new Piece(Ease.SineOut, ParryTo, 60f - SpinAngle, -135f),
            new Piece(Ease.SineInOut, SpinTo, RestBlade - 360f - 25f, 25f),
        };

        private static readonly Piece[] ParryArm =
        {
            new Piece(Ease.PolyOut, 0f, RestArm, 8f, 2),
            new Piece(Ease.SineOut, ParryFrom, 68f, -83f),
            new Piece(Ease.SineInOut, ParryTo, -15f, 25f),
            new Piece(Ease.SineInOut, SpinTo, 10f, 50f),
        };

        /// <summary>
        /// The blade's angle, in degrees, in a move begun from where that move expects to begin
        /// (<see cref="StartBlade"/>). It goes past a whole turn in the circle and the parry: it
        /// is the turning that matters, not the number.
        /// </summary>
        public static float Blade(FlowMove move, float progress) => move switch
        {
            FlowMove.Down => Easing.At(progress, DownBlade),
            FlowMove.Up => Easing.At(progress, UpBlade),
            FlowMove.Circle => Easing.At(progress, CircleBlade),
            FlowMove.Parry => Easing.At(progress, ParryBlade),
            _ => RestBlade,
        };

        /// <summary>The front arm's angle, in degrees.</summary>
        public static float Arm(FlowMove move, float progress) => move switch
        {
            FlowMove.Down => Easing.At(progress, DownArm),
            FlowMove.Up => Easing.At(progress, UpArm),
            FlowMove.Circle => Easing.At(progress, CircleArm),
            FlowMove.Parry => Easing.At(progress, ParryArm),
            _ => RestArm,
        };

        /// <summary>Where a move expects the blade and the arm to be when it begins.</summary>
        public static float StartBlade(FlowMove move) => move == FlowMove.Circle ? HighBlade : RestBlade;
        public static float StartArm(FlowMove move) => move == FlowMove.Circle ? HighArm : RestArm;

        /// <summary>The part of the move the blade is fast through: where a cut cuts, where the parry spins, and what it leaves a trail over.</summary>
        public static void Fast(FlowMove move, out float from, out float to)
        {
            switch (move)
            {
                case FlowMove.Down:
                    from = DownFrom;
                    to = DownTo;
                    break;

                case FlowMove.Up:
                    from = UpFrom;
                    to = UpTo;
                    break;

                case FlowMove.Circle:
                    from = CircleFrom;
                    to = CircleTo;
                    break;

                case FlowMove.Parry:
                    from = ParryFrom;
                    to = SpinTo;
                    break;

                default:
                    from = 0f;
                    to = 0f;
                    break;
            }
        }

        /// <summary>
        /// The share of a move by which the blade, begun from anywhere else, is on the move's
        /// own path: the end of its wind, before it is fast.
        /// </summary>
        public static float JoinedBy(FlowMove move) => move switch
        {
            FlowMove.Down => DownRaisedBy,
            FlowMove.Up => UpDippedBy,
            FlowMove.Circle => CircleFrom,
            FlowMove.Parry => ParryFrom,
            _ => 0f,
        };

        /// <summary>The arm is drawn in a little, not out at its full length: while the blade is being wound or cocked.</summary>
        public static bool Bent(FlowMove move, float progress) => move != FlowMove.Rest && progress < JoinedBy(move);

        /// <summary>Whether the blade cuts at this point of the move. The parry never does.</summary>
        public static bool Cuts(FlowMove move, float progress)
        {
            if (move != FlowMove.Down && move != FlowMove.Up && move != FlowMove.Circle)
                return false;

            Fast(move, out float from, out float to);
            return progress >= from && progress <= to;
        }

        /// <summary>The cut that follows this one: down, up, round, and down again. After anything else, down.</summary>
        public static FlowMove After(FlowMove cut) => cut switch
        {
            FlowMove.Down => FlowMove.Up,
            FlowMove.Up => FlowMove.Circle,
            _ => FlowMove.Down,
        };

        /// <summary>How far this cut carries its user forward, in pixels, and the part of the cut it does it over.</summary>
        public static float Step(FlowMove move, out float from, out float length)
        {
            switch (move)
            {
                case FlowMove.Down:
                    from = 0.28f;
                    length = 0.36f;
                    return DownStep;

                case FlowMove.Up:
                    from = 0.20f;
                    length = 0.40f;
                    return UpStep;

                case FlowMove.Circle:
                    from = 0.12f;
                    length = 0.50f;
                    return CircleStep;

                default:
                    from = 0f;
                    length = 0f;
                    return 0f;
            }
        }

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
    /// The Iron Flow from tick to tick: which move it is in, how far into it, and where the
    /// blade and the arm are. One for each sword on each machine; nothing of the game in it,
    /// so that it can be run outside it.
    ///
    /// A move is begun from wherever the blade is. Each has its own path from its own start
    /// (IronFlowMoves); begun from anywhere else, the blade joins that path over the move's
    /// wind, before it is fast (IronFlowMoves.JoinedBy), the shorter way round.
    ///
    /// With no move under way the blade is where the last one left it. If that is not where
    /// it hangs (the cut up leaves it raised), it is held there <see cref="HoldTicks"/> ticks,
    /// for the circle to follow from, and then lowered over <see cref="LowerTicks"/>.
    /// </summary>
    public sealed class IronFlowState
    {
        /// <summary>Ticks it is held where a move left it before it is lowered, and ticks the lowering takes.</summary>
        public const int HoldTicks = 8;
        public const int LowerTicks = 18;

        /// <summary>The move under way, or Rest.</summary>
        public FlowMove Move { get; private set; }

        public bool Busy { get; private set; }

        /// <summary>Ticks into the move, and the ticks it takes.</summary>
        public float Timer { get; private set; }
        public float Duration { get; private set; } = 1f;

        /// <summary>The share of the move that is done.</summary>
        public float Progress => Busy ? Math.Min(1f, Timer / Duration) : 0f;

        /// <summary>The blade's angle this tick and last, and the arm's, in degrees.</summary>
        public float Blade { get; private set; } = IronFlowMoves.RestBlade;
        public float PreviousBlade { get; private set; } = IronFlowMoves.RestBlade;
        public float Arm { get; private set; } = IronFlowMoves.RestArm;

        public bool Bent => Busy && IronFlowMoves.Bent(Move, Progress);

        public bool Cuts => Busy && IronFlowMoves.Cuts(Move, Progress);

        /// <summary>How far the blade and the arm were from the move's own start when it began.</summary>
        private float offBlade;
        private float offArm;

        /// <summary>Ticks since the last move ended, and where it left the blade and the arm.</summary>
        private float idle;
        private float leftBlade = IronFlowMoves.RestBlade;
        private float leftArm = IronFlowMoves.RestArm;

        /// <summary>Ticks since the last move ended; none while one is under way.</summary>
        public float Idle => Busy ? 0f : idle;

        /// <summary>Begins a move, from wherever the blade is, taking this many ticks.</summary>
        public void Begin(FlowMove move, float ticks)
        {
            if (move == FlowMove.Rest)
                return;

            offBlade = IronFlowMoves.Wrap(Blade - IronFlowMoves.StartBlade(move));
            offArm = IronFlowMoves.Wrap(Arm - IronFlowMoves.StartArm(move));

            Move = move;
            Busy = true;
            Timer = 0f;
            Duration = Math.Max(1f, ticks);

            // The same pose, in the move's own numbers.
            Blade = PreviousBlade = IronFlowMoves.StartBlade(move) + offBlade;
            Arm = IronFlowMoves.StartArm(move) + offArm;
        }

        /// <summary>One tick on.</summary>
        public void Tick()
        {
            PreviousBlade = Blade;

            if (Busy)
            {
                Timer++;

                float progress = Math.Min(1f, Timer / Duration);
                float joined = IronFlowMoves.JoinedBy(Move);
                float apart = joined <= 0f ? 0f : 1f - Easing.Smoother(progress / joined);

                Blade = IronFlowMoves.Blade(Move, progress) + offBlade * apart;
                Arm = IronFlowMoves.Arm(Move, progress) + offArm * apart;

                return;
            }

            idle++;

            float lowered = Easing.Apply(Ease.SineInOut, (idle - HoldTicks) / LowerTicks);

            Blade = leftBlade + (IronFlowMoves.RestBlade - leftBlade) * lowered;
            Arm = leftArm + (IronFlowMoves.RestArm - leftArm) * lowered;
        }

        /// <summary>
        /// After the tick's pose has been used: a move that has run its ticks is over, and the
        /// blade is left where it put it.
        /// </summary>
        public void Finish()
        {
            if (!Busy || Timer < Duration)
                return;

            Busy = false;
            Move = FlowMove.Rest;
            idle = 0f;

            Blade = PreviousBlade = leftBlade = IronFlowMoves.Wrap(Blade);
            Arm = leftArm = IronFlowMoves.Wrap(Arm);
        }
    }
}
