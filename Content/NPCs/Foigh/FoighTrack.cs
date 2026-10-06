using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// One of Foigh's movements, written as poses at moments: at this tick he holds himself
    /// so, at that one so, and in between he goes from the one to the other along the easing
    /// the later pose asks for (Easing, as the swings of the mod's own weapons are written).
    ///
    /// Written by chaining: <c>At(tick)</c> starts a pose that begins as a copy of the one
    /// before it, and every call after it changes one thing about it. Angles are given in
    /// degrees, measured as FoighRig measures them:
    ///
    ///   limbs   from straight down, forward positive (0 hangs, 90 ahead, 180 up, -90 behind)
    ///   Lean    forward positive; Nod on top of it, down positive
    ///   Sword   where the blade points: 0 ahead, 90 at the ground, -90 at the sky, 180 behind.
    ///           It turns the way the numbers go: from 170 to 300 is over the top, clockwise;
    ///           from 170 to -60 is the same place by way of the ground. So a number may be
    ///           well past a whole turn.
    ///
    /// What cannot be half done (which hand has the sword, which edge leads, whether it is
    /// behind him, whether he is in the air) changes at its pose's own tick.
    ///
    /// Sampled at any time, whole ticks or between them (the trail of the blade is the blade
    /// at times between ticks). Nothing here touches the game.
    /// </summary>
    public sealed class FoighTrack
    {
        private sealed class Node
        {
            public float Tick;
            public Ease Ease = Ease.SineInOut;
            public int Degree = 2;
            public FoighRig.Pose Pose;
        }

        private readonly List<Node> nodes = new List<Node>();
        private Node now;

        /// <summary>The tick of the last pose: how long the movement is.</summary>
        public float Length => nodes.Count == 0 ? 0f : nodes[nodes.Count - 1].Tick;

        private const float Rad = MathHelper.Pi / 180f;

        public FoighTrack()
        {
        }

        /// <summary>A movement that begins as another's pose at some time: for one that picks up where another left off.</summary>
        public FoighTrack(FoighRig.Pose from)
        {
            now = new Node { Tick = 0f, Pose = from };
            nodes.Add(now);
        }

        /// <summary>The next pose, at this tick, reached along this easing. It begins as the one before it.</summary>
        public FoighTrack At(float tick, Ease ease = Ease.SineInOut, int degree = 2)
        {
            Node next = new Node
            {
                Tick = tick,
                Ease = ease,
                Degree = degree,
                Pose = now != null ? now.Pose : FoighRig.Pose.Standing,
            };

            nodes.Add(next);
            now = next;
            return this;
        }

        public FoighTrack Lean(float degrees) { now.Pose.Lean = degrees * Rad; return this; }
        public FoighTrack Nod(float degrees) { now.Pose.Nod = degrees * Rad; return this; }
        public FoighTrack Near(float degrees) { now.Pose.NearArm = degrees * Rad; return this; }
        public FoighTrack Far(float degrees) { now.Pose.FarArm = degrees * Rad; return this; }

        /// <summary>The legs: the near one, then the far one.</summary>
        public FoighTrack Legs(float near, float far)
        {
            now.Pose.NearLeg = near * Rad;
            now.Pose.FarLeg = far * Rad;
            return this;
        }

        public FoighTrack Sword(float degrees) { now.Pose.Sword = degrees * Rad; return this; }
        public FoighTrack Reach(float share) { now.Pose.Reach = share; return this; }
        public FoighTrack Rise(float pixels) { now.Pose.Rise = pixels; return this; }
        public FoighTrack Turn(float share) { now.Pose.Turn = share; return this; }
        public FoighTrack HeadTurn(float share) { now.Pose.HeadTurn = share; return this; }

        /// <summary>The sword on its way from the hand that has it to the other: 0 in the one, 1 at the other (which then takes it: Hand).</summary>
        public FoighTrack Pass(float share) { now.Pose.Pass = share; return this; }

        /// <summary>From this pose on: 0 neither hand, 1 the near (top) hand, 2 the far (bottom) hand.</summary>
        public FoighTrack Hand(int hand) { now.Pose.Hand = hand; return this; }

        /// <summary>From this pose on: the clean edge leads a cut that turns clockwise (downward, ahead of him), or the other way.</summary>
        public FoighTrack Clean(bool clockwise) { now.Pose.CleanClockwise = clockwise; return this; }

        public FoighTrack Behind(bool behind) { now.Pose.Behind = behind; return this; }
        public FoighTrack Air(bool air) { now.Pose.Air = air; return this; }

        /// <summary>The pose at this time, in ticks: before the first pose it is the first, after the last the last.</summary>
        public FoighRig.Pose Sample(float tick)
        {
            if (nodes.Count == 0)
                return FoighRig.Pose.Standing;

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
            float t = span <= 0f ? 1f : Easing.Apply(b.Ease, (tick - a.Tick) / span, b.Degree);

            return Blend(a.Pose, b.Pose, t);
        }

        /// <summary>
        /// Between two poses, <paramref name="t"/> of the way from the first: every turn and
        /// length goes its share; what cannot be half done is the first's until the second is
        /// reached.
        /// </summary>
        public static FoighRig.Pose Blend(in FoighRig.Pose a, in FoighRig.Pose b, float t)
        {
            FoighRig.Pose pose = t >= 1f ? b : a;

            pose.Lean = MathHelper.Lerp(a.Lean, b.Lean, t);
            pose.Nod = MathHelper.Lerp(a.Nod, b.Nod, t);
            pose.NearArm = MathHelper.Lerp(a.NearArm, b.NearArm, t);
            pose.FarArm = MathHelper.Lerp(a.FarArm, b.FarArm, t);
            pose.NearLeg = MathHelper.Lerp(a.NearLeg, b.NearLeg, t);
            pose.FarLeg = MathHelper.Lerp(a.FarLeg, b.FarLeg, t);
            pose.Sword = MathHelper.Lerp(a.Sword, b.Sword, t);
            pose.Reach = MathHelper.Lerp(a.Reach, b.Reach, t);
            pose.Rise = MathHelper.Lerp(a.Rise, b.Rise, t);
            pose.Turn = MathHelper.Lerp(a.Turn, b.Turn, t);
            pose.HeadTurn = MathHelper.Lerp(a.HeadTurn, b.HeadTurn, t);
            pose.Pass = MathHelper.Lerp(a.Pass, b.Pass, t);

            return pose;
        }

        /// <summary>
        /// A pose's blade angle brought within half a turn of <paramref name="near"/>, by whole
        /// turns: so that going from one movement's last pose to another's first never takes
        /// the blade the long way round.
        /// </summary>
        public static float Nearest(float angle, float near)
        {
            return near + MathHelper.WrapAngle(angle - near);
        }
    }
}
