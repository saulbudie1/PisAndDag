using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// One of the knight's movements, written as poses at moments: at this tick he holds
    /// himself so, at that one so, and in between he goes from the one to the other along the
    /// easing the later pose asks for (Easing, as the mod's own swings are written). As
    /// FoighTrack is for Foigh.
    ///
    /// Written by chaining: <c>At(tick)</c> starts a pose that begins as a copy of the one
    /// before it, and every call after it changes one thing about it. Angles in degrees, as
    /// KnightRig measures them:
    ///
    ///   limbs   from straight down, forward positive (0 hangs, 90 ahead, 180 up, -90 behind)
    ///   Lean    forward positive; Nod on top of it, down positive
    ///   blades  where each points: 0 ahead, 90 at the ground, -90 at the sky, 180 behind.
    ///           It turns the way the numbers go, so a number may be past a whole turn.
    ///
    /// What cannot be half done (a limb bent or straight, which hand has a sword, in the air
    /// or not) changes halfway between the two poses: a limb bends as it passes the middle of
    /// its swing, where the eye is least on it.
    ///
    /// Sampled at any time, whole ticks or between them (a blade's trail is the blade at
    /// times between ticks). Nothing here touches the game.
    /// </summary>
    public sealed class KnightTrack
    {
        private sealed class Node
        {
            public float Tick;
            public Ease Ease = Ease.SineInOut;
            public int Degree = 2;
            public float Switch = 0.5f;
            public KnightRig.Pose Pose;

            /// <summary>How far he has gone ahead along the ground, and up, by this pose (pixels, from where the movement began).</summary>
            public float Ahead;
            public float Up;
        }

        private readonly List<Node> nodes = new List<Node>();
        private Node now;

        /// <summary>The tick of the last pose: how long the movement is.</summary>
        public float Length => nodes.Count == 0 ? 0f : nodes[nodes.Count - 1].Tick;

        private const float Rad = MathHelper.Pi / 180f;

        public KnightTrack()
        {
        }

        /// <summary>A movement that begins as another's pose: for one that picks up where another left off.</summary>
        public KnightTrack(KnightRig.Pose from, float tick = 0f)
        {
            now = new Node { Tick = tick, Pose = from };
            nodes.Add(now);
        }

        /// <summary>The next pose, at this tick, reached along this easing. It begins as the one before it.</summary>
        public KnightTrack At(float tick, Ease ease = Ease.SineInOut, int degree = 2)
        {
            Node next = new Node
            {
                Tick = tick,
                Ease = ease,
                Degree = degree,
                Pose = now != null ? now.Pose : KnightRig.Pose.Standing,
                Ahead = now != null ? now.Ahead : 0f,
                Up = now != null ? now.Up : 0f,
            };

            nodes.Add(next);
            now = next;
            return this;
        }

        public KnightTrack Lean(float degrees) { now.Pose.Lean = degrees * Rad; return this; }
        public KnightTrack Nod(float degrees) { now.Pose.Nod = degrees * Rad; return this; }

        /// <summary>
        /// The near (top) arm by its fist: the way from the shoulder, and how far (left out, as it
        /// was). KnightRig.ArmLong is the arm straight, ArmBent bent at a right angle; between and
        /// past them it is drawn bent or straight and stretched to fit.
        /// </summary>
        public KnightTrack Near(float degrees, float reach = -1f)
        {
            now.Pose.NearArm = degrees * Rad;
            if (reach > 0f)
                now.Pose.NearReach = reach;
            now.Pose.NearOn = -1;
            return this;
        }

        public KnightTrack Far(float degrees, float reach = -1f)
        {
            now.Pose.FarArm = degrees * Rad;
            if (reach > 0f)
                now.Pose.FarReach = reach;
            now.Pose.FarOn = -1;
            return this;
        }

        /// <summary>The legs by their soles: the near one's way from its hip, then the far one's; and how far (left out, as they were: KnightRig.LegLong straight).</summary>
        public KnightTrack Legs(float near, float far, float nearStep = -1f, float farStep = -1f)
        {
            now.Pose.NearLeg = near * Rad;
            now.Pose.FarLeg = far * Rad;
            if (nearStep > 0f)
                now.Pose.NearStep = nearStep;
            if (farStep > 0f)
                now.Pose.FarStep = farStep;
            return this;
        }

        /// <summary>
        /// What cannot be half done (a sword taken from its scabbard, a hand let go of a grip,
        /// in the air or not) changes this far through the time from the pose before: a half by
        /// default; 0 at once, as a sword drawn that leaves its scabbard the moment the arm goes on.
        /// </summary>
        public KnightTrack SwitchAt(float share) { now.Switch = share; return this; }

        /// <summary>Each blade where its scabbard holds it (as it lies home, for this pose's lean): for poses before a draw, so the blade leaves from there.</summary>
        public KnightTrack Home()
        {
            KnightRig.Scabbard(KnightRig.Sun, now.Pose.Lean, 0f, out _, out float sun);
            KnightRig.Scabbard(KnightRig.Grace, now.Pose.Lean, 0f, out _, out float grace);
            now.Pose.SunAngle = sun;
            now.Pose.GraceAngle = grace;
            return this;
        }

        /// <summary>Where each blade points.</summary>
        public KnightTrack Blades(float sun, float grace) { now.Pose.SunAngle = sun * Rad; now.Pose.GraceAngle = grace * Rad; return this; }
        public KnightTrack Sun(float degrees) { now.Pose.SunAngle = degrees * Rad; return this; }
        public KnightTrack Grace(float degrees) { now.Pose.GraceAngle = degrees * Rad; return this; }

        /// <summary>
        /// Each blade turned on from where it was by so many degrees (positive clockwise facing
        /// right: down through the front), so that a sweep goes the way it is written and not
        /// the short way round.
        /// </summary>
        public KnightTrack SunTurn(float degrees) { now.Pose.SunAngle += degrees * Rad; return this; }
        public KnightTrack GraceTurn(float degrees) { now.Pose.GraceAngle += degrees * Rad; return this; }

        /// <summary>Each blade to point this way, by the turn from where it was that is nearest the given direction of turning (+1 clockwise, -1 anticlockwise, 0 whichever is shorter).</summary>
        public KnightTrack SunTo(float degrees, int way) { now.Pose.SunAngle = Toward(now.Pose.SunAngle, degrees * Rad, way); return this; }
        public KnightTrack GraceTo(float degrees, int way) { now.Pose.GraceAngle = Toward(now.Pose.GraceAngle, degrees * Rad, way); return this; }

        /// <summary>The angle equal to <paramref name="target"/> (by whole turns) reached from <paramref name="from"/> turning the given way, less than a whole turn on.</summary>
        public static float Toward(float from, float target, int way)
        {
            float delta = target - from;
            float turn = MathHelper.TwoPi;

            delta %= turn;

            if (way > 0 && delta < 0f)
                delta += turn;
            else if (way < 0 && delta > 0f)
                delta -= turn;
            else if (way == 0)
                delta = MathHelper.WrapAngle(delta);

            return from + delta;
        }

        /// <summary>From this pose on: where each sword is (KnightRig.Sheathed, InNear, InFar).</summary>
        public KnightTrack Hold(int sun, int grace) { now.Pose.SunIn = sun; now.Pose.GraceIn = grace; return this; }

        /// <summary>Both drawn, each in its own hand: the Sun in the near, the Grace in the far.</summary>
        public KnightTrack Drawn() => Hold(KnightRig.InNear, KnightRig.InFar);

        /// <summary>A sheathed sword drawn this many pixels out of its scabbard.</summary>
        public KnightTrack Out(float sun, float grace) { now.Pose.SunDrawn = sun; now.Pose.GraceDrawn = grace; return this; }

        /// <summary>The hands on these swords' grips, wherever they are (KnightRig.Sun, Grace, or -1).</summary>
        public KnightTrack On(int near, int far) { now.Pose.NearOn = near; now.Pose.FarOn = far; return this; }

        public KnightTrack Reach(float sun, float grace) { now.Pose.SunReach = sun; now.Pose.GraceReach = grace; return this; }

        /// <summary>By this pose he has gone this far ahead along the ground (and up), from where the movement began: the step that goes with a cut.</summary>
        public KnightTrack Go(float ahead, float up = 0f) { now.Ahead = ahead; now.Up = up; return this; }
        public KnightTrack Rise(float pixels) { now.Pose.Rise = pixels; return this; }
        public KnightTrack Air(bool air) { now.Pose.Air = air; return this; }
        public KnightTrack Turn(float share) { now.Pose.Turn = share; return this; }

        /// <summary>Turned at the waist: 1 three quarters toward the eye (the far arm reaching further), -1 away (the near arm further), 0 side on (KnightRig.Pose.Twist).</summary>
        public KnightTrack Twist(float share) { now.Pose.Twist = share; return this; }

        /// <summary>Each foot and knee turned: 1 toward the eye, -1 away, 0 side on (KnightRig.Pose.NearToe, FarToe).</summary>
        public KnightTrack Toes(float near, float far) { now.Pose.NearToe = near; now.Pose.FarToe = far; return this; }

        /// <summary>The pose at this time, in ticks: before the first pose it is the first, after the last the last.</summary>
        public KnightRig.Pose Sample(float tick)
        {
            if (nodes.Count == 0)
                return KnightRig.Pose.Standing;

            if (tick <= nodes[0].Tick)
                return nodes[0].Pose;

            int last = nodes.Count - 1;

            if (tick >= nodes[last].Tick)
                return nodes[last].Pose;

            int i = 0;

            while (i < last && tick >= nodes[i + 1].Tick)
                i++;

            Node a = nodes[i];
            Node b = nodes[i + 1];

            float span = b.Tick - a.Tick;
            float raw = span <= 0f ? 1f : (tick - a.Tick) / span;
            float t = Easing.Apply(b.Ease, raw, b.Degree);

            KnightRig.Pose from = a.Pose;
            KnightRig.Pose to = b.Pose;
            KnightRig.Resolve(ref from);
            KnightRig.Resolve(ref to);

            return Blend(from, to, t, raw >= b.Switch && raw > 0f);
        }

        /// <summary>How far ahead (x) and up (y, negative up as the world counts) he has gone by this time, along the same easings as the poses.</summary>
        public Vector2 Travel(float tick)
        {
            if (nodes.Count == 0)
                return Vector2.Zero;

            if (tick <= nodes[0].Tick)
                return new Vector2(nodes[0].Ahead, -nodes[0].Up);

            int last = nodes.Count - 1;

            if (tick >= nodes[last].Tick)
                return new Vector2(nodes[last].Ahead, -nodes[last].Up);

            int i = 0;

            while (i < last && tick >= nodes[i + 1].Tick)
                i++;

            Node a = nodes[i];
            Node b = nodes[i + 1];
            float span = b.Tick - a.Tick;
            float t = span <= 0f ? 1f : Easing.Apply(b.Ease, (tick - a.Tick) / span, b.Degree);

            return new Vector2(MathHelper.Lerp(a.Ahead, b.Ahead, t), -MathHelper.Lerp(a.Up, b.Up, t));
        }

        /// <summary>The pose at the start of this movement.</summary>
        public KnightRig.Pose First => nodes.Count == 0 ? KnightRig.Pose.Standing : nodes[0].Pose;

        /// <summary>The pose at its end.</summary>
        public KnightRig.Pose Last => nodes.Count == 0 ? KnightRig.Pose.Standing : nodes[nodes.Count - 1].Pose;

        /// <summary>
        /// Between two poses, <paramref name="t"/> of the way from the first (eased): every turn
        /// and length goes its share; what cannot be half done is the first's until
        /// <paramref name="switched"/>, the second's after.
        /// </summary>
        public static KnightRig.Pose Blend(in KnightRig.Pose a, in KnightRig.Pose b, float t, bool switched)
        {
            KnightRig.Pose pose = switched ? b : a;

            pose.Lean = MathHelper.Lerp(a.Lean, b.Lean, t);
            pose.Nod = MathHelper.Lerp(a.Nod, b.Nod, t);
            pose.NearArm = MathHelper.Lerp(a.NearArm, b.NearArm, t);
            pose.FarArm = MathHelper.Lerp(a.FarArm, b.FarArm, t);
            pose.NearReach = MathHelper.Lerp(OrLong(a.NearReach, KnightRig.ArmLong), OrLong(b.NearReach, KnightRig.ArmLong), t);
            pose.FarReach = MathHelper.Lerp(OrLong(a.FarReach, KnightRig.ArmLong), OrLong(b.FarReach, KnightRig.ArmLong), t);
            pose.NearLeg = MathHelper.Lerp(a.NearLeg, b.NearLeg, t);
            pose.FarLeg = MathHelper.Lerp(a.FarLeg, b.FarLeg, t);
            pose.NearStep = MathHelper.Lerp(OrLong(a.NearStep, KnightRig.LegLong), OrLong(b.NearStep, KnightRig.LegLong), t);
            pose.FarStep = MathHelper.Lerp(OrLong(a.FarStep, KnightRig.LegLong), OrLong(b.FarStep, KnightRig.LegLong), t);
            pose.Rise = MathHelper.Lerp(a.Rise, b.Rise, t);
            pose.SunAngle = MathHelper.Lerp(a.SunAngle, b.SunAngle, t);
            pose.GraceAngle = MathHelper.Lerp(a.GraceAngle, b.GraceAngle, t);
            pose.SunDrawn = MathHelper.Lerp(a.SunDrawn, b.SunDrawn, t);
            pose.GraceDrawn = MathHelper.Lerp(a.GraceDrawn, b.GraceDrawn, t);
            pose.SunReach = MathHelper.Lerp(Or1(a.SunReach), Or1(b.SunReach), t);
            pose.GraceReach = MathHelper.Lerp(Or1(a.GraceReach), Or1(b.GraceReach), t);
            pose.Turn = MathHelper.Lerp(a.Turn, b.Turn, t);
            pose.Twist = MathHelper.Lerp(a.Twist, b.Twist, t);
            pose.NearToe = MathHelper.Lerp(a.NearToe, b.NearToe, t);
            pose.FarToe = MathHelper.Lerp(a.FarToe, b.FarToe, t);

            return pose;
        }

        private static float OrLong(float v, float otherwise) => v <= 0f ? otherwise : v;

        private static float Or1(float v) => v <= 0f ? 1f : v;

        /// <summary>
        /// Between any two poses, eased over the whole of it: for going from wherever one
        /// movement left him into the first pose of the next, the blades by the short way.
        /// </summary>
        public static KnightRig.Pose Between(KnightRig.Pose a, KnightRig.Pose b, float share)
        {
            b.SunAngle = KnightRig.Nearest(b.SunAngle, a.SunAngle);
            b.GraceAngle = KnightRig.Nearest(b.GraceAngle, a.GraceAngle);

            float t = Easing.Apply(Ease.SineInOut, share);
            return Blend(a, b, t, share >= 0.5f);
        }
    }
}
