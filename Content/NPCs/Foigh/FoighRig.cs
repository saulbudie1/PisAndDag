using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// Where everything is on Foigh's art, and the sums that put the pieces together. One
    /// place for both the drawing and the fight, so the sword that is drawn is the sword
    /// that hits.
    ///
    /// Every number here was measured off the PNGs in this folder and the layers of
    /// SwordsmanBoss.aseprite (where each piece sits on the 100 by 80 canvas), not guessed:
    ///
    ///   Swordman_Head.png           24x24   at (38, 26) on the canvas
    ///   SwordsmanBoss_Torso.png     24x27   at (42, 41)
    ///   SwordsmanBoss_TopArmf.png   18x26   at (54, 42)   the near arm, over the torso
    ///   SwordsmanBoss_BotArm.png    12x18   at (42, 50)   the far arm, under everything
    ///   SwordsmanBoss_TopLeg.png    16x23   at (50, 57)   the near leg
    ///   SwordsmanBoss_BotLeg.png    16x19   at (44, 61)   the far leg
    ///   SwordsmanBluntSide.png      90x90   the sword on the diagonal, point up and right
    ///
    /// His soles are on the canvas's last row (79), and the middle of him is column 54. The
    /// art faces LEFT (the toes of both boots are on the left). Each limb is one piece: an
    /// arm turns about its shoulder, a leg about its hip, the body about its hips, the head
    /// about the neck. If a piece is exported again at another size, its numbers here are
    /// what has to change.
    ///
    /// EVERYTHING BELOW IS WORKED OUT FACING RIGHT, forward along +x and down along +y, from
    /// the point on the ground between his feet; facing left mirrors it. Three ways of saying
    /// a turn, each the natural one for what it turns:
    ///
    ///   a limb    by where it points, from straight down, forward positive: 0 hangs, a
    ///             quarter turn points ahead, a half turn straight up
    ///   the body  by its lean, forward positive; the head by its nod on top of that
    ///   the sword by where the blade points, from straight ahead, turning downward
    ///             positive: 0 ahead, a quarter turn at the ground, minus a quarter at the sky
    ///
    /// THE SWORD has a clean side and a dull one (the pale edge and the dark one on the
    /// picture). Which of the two is on the clockwise side of the blade is part of a pose
    /// (<see cref="Pose.CleanClockwise"/>): he cuts with the clean one, and it is the dull
    /// one that is dragged along the ground.
    ///
    /// Nothing in this file touches the game: it is vectors and angles only, so the same code
    /// can be run outside it to see what he looks like.
    /// </summary>
    public static class FoighRig
    {
        public const string Folder = "PisAndDag/Content/NPCs/Foigh/";

        // ---- the pieces, back to front as they are drawn with the sword in the near hand ----

        public const int FarArm = 0;
        public const int FarLeg = 1;
        public const int NearLeg = 2;
        public const int Torso = 3;
        public const int NearArm = 4;
        public const int Head = 5;
        public const int Sword = 6;
        public const int Pieces = 7;

        public static readonly string[] Names =
        {
            "SwordsmanBoss_BotArm",
            "SwordsmanBoss_BotLeg",
            "SwordsmanBoss_TopLeg",
            "SwordsmanBoss_Torso",
            "SwordsmanBoss_TopArmf",
            "Swordman_Head",
            "SwordsmanBluntSide",
        };

        /// <summary>Each picture's size, in pixels.</summary>
        public static readonly Point[] Sizes =
        {
            new Point(12, 18),
            new Point(16, 19),
            new Point(16, 23),
            new Point(24, 27),
            new Point(18, 26),
            new Point(24, 24),
            new Point(90, 90),
        };

        /// <summary>What each piece turns about, on its own picture as it is drawn (facing left).</summary>
        public static readonly Vector2[] Pivots =
        {
            new Vector2(5f, 2f),    // far shoulder
            new Vector2(8f, 1f),    // far hip
            new Vector2(9f, 3f),    // near hip
            new Vector2(12f, 24f),  // the hips, under the middle of the body
            new Vector2(7f, 5f),    // near shoulder
            new Vector2(13f, 20f),  // the neck
            new Vector2(20f, 70f),  // the middle of the grip
        };

        // ---- where the joints are, facing right, y down ------------------------------------

        /// <summary>The hips over the ground between his feet, standing.</summary>
        public const float HipsHeight = 15f;

        /// <summary>From the hips: each leg's own hip, each shoulder, the neck.</summary>
        public static readonly Vector2 NearHip = new Vector2(-5f, -5f);
        public static readonly Vector2 FarHip = new Vector2(2f, -3f);
        public static readonly Vector2 NearShoulder = new Vector2(-7f, -18f);
        public static readonly Vector2 FarShoulder = new Vector2(7f, -13f);
        public static readonly Vector2 Neck = new Vector2(3f, -19f);

        /// <summary>A leg from its hip to its sole, and where it points on the picture (near: a hair forward).</summary>
        public const float NearLegLength = 20f;
        public const float FarLegLength = 18f;
        public const float NearLegRest = 0.05f;
        public const float FarLegRest = 0f;

        /// <summary>An arm from its shoulder to the middle of its fist, and where it points on the picture (both a little back).</summary>
        public const float NearArmLength = 17.5f;
        public const float FarArmLength = 13f;
        public const float NearArmRest = -0.231f;
        public const float FarArmRest = -0.077f;

        /// <summary>His eye, from the neck, with the head level.</summary>
        public static readonly Vector2 Eye = new Vector2(3f, -6f);

        /// <summary>The crown of his head, from the neck: where a line over his head sits.</summary>
        public static readonly Vector2 Crown = new Vector2(1f, -21f);

        // ---- the sword -----------------------------------------------------------------------

        /// <summary>Along the blade from the middle of the grip: to the point, to the guard, to the end of the pommel.</summary>
        public const float GripToTip = 88f;
        public const float GripToGuard = 16f;
        public const float GripToPommel = 16f;

        /// <summary>The picture's blade points an eighth of a turn above straight ahead.</summary>
        public const float SwordDrawn = -MathHelper.PiOver4;

        /// <summary>Standing in the dragging stance, the near fist is this far over the ground.</summary>
        public const float DragFist = 16f;

        /// <summary>The narrowest he is ever seen, turning: edge on, a man is still as deep as this share of his width.</summary>
        public const float Thin = 0.3f;

        // ============================================================
        // THE POSE
        // ============================================================

        /// <summary>How he holds himself, facing right. See the class for what each turn is measured from.</summary>
        public struct Pose
        {
            /// <summary>The body's lean, forward positive, and the head's nod on top of it.</summary>
            public float Lean;
            public float Nod;

            public float NearArm;
            public float FarArm;
            public float NearLeg;
            public float FarLeg;

            /// <summary>In the air: the hips are where <see cref="Rise"/> says, not on whichever foot is lower.</summary>
            public bool Air;

            /// <summary>The hips this much higher than the legs alone would have them (down is negative).</summary>
            public float Rise;

            /// <summary>Which hand has the sword: 0 neither, 1 the near (top) hand, 2 the far (bottom) hand.</summary>
            public int Hand;

            /// <summary>The sword on its way from the hand that has it to the other one: 0 in the hand, 1 at the other.</summary>
            public float Pass;

            /// <summary>Where the blade points.</summary>
            public float Sword;

            /// <summary>The clean edge is on the clockwise side of the blade (the side a downward cut leads with).</summary>
            public bool CleanClockwise;

            /// <summary>The sword's length as seen, 1 all of it: less when it points toward the eye or away.</summary>
            public float Reach;

            /// <summary>The sword is behind him, whichever hand has it.</summary>
            public bool Behind;

            /// <summary>
            /// The whole of him seen turning about his own middle: 1 side on, 0 edge on, and past
            /// edge on, negative, he is seen facing the other way (minus 1 is all the way round).
            /// He has a front as well as a side, so he is never seen narrower than <see cref="Thin"/>.
            /// </summary>
            public float Turn;

            /// <summary>
            /// The same of his head alone: negative, it looks the other way from the way he
            /// faces, whatever the body is seen doing under it.
            /// </summary>
            public float HeadTurn;

            public static Pose Standing => new Pose
            {
                NearArm = NearArmRest,
                FarArm = FarArmRest,
                NearLeg = NearLegRest,
                FarLeg = FarLegRest,
                Hand = 1,
                Sword = MathHelper.Pi - 0.185f,
                CleanClockwise = true,
                Reach = 1f,
                Turn = 1f,
                HeadTurn = 1f,
            };
        }

        /// <summary>
        /// One picture to draw, where and how, in the world, as he would be drawn side on. Seen
        /// turning (<see cref="Frame.Turn"/> less than 1), every picture of him is then drawn
        /// that much narrower across, about the upright line through <see cref="Frame.TurnAbout"/>:
        /// whoever draws him does that to all of them at once.
        /// </summary>
        public struct Part
        {
            public int Piece;

            /// <summary>Where the pivot goes, in the world, side on.</summary>
            public Vector2 At;

            /// <summary>The pivot on the picture as it will be drawn (mirrored with it).</summary>
            public Vector2 Origin;

            /// <summary>Clockwise, as SpriteBatch turns.</summary>
            public float Rotation;

            public bool FlipX;

            /// <summary>Drawn this much shorter along <see cref="Axis"/> (a world angle), about <see cref="At"/>: 1 is not at all.</summary>
            public float Stretch;
            public float Axis;
        }

        /// <summary>
        /// Where the joints came out, in the world, for whoever needs them: the fight, the light,
        /// the sparks. These are where they are SEEN: his turn has been taken into them.
        /// </summary>
        public struct Frame
        {
            public Vector2 Hips;
            public Vector2 NearFist;
            public Vector2 FarFist;
            public Vector2 NeckAt;
            public Vector2 EyeAt;
            public Vector2 CrownAt;

            /// <summary>
            /// The sword: its grip, the way the blade points (a world angle, clockwise from +x,
            /// side on), where the blade begins past the guard, its point and its pommel.
            /// </summary>
            public Vector2 Grip;
            public float Blade;
            public Vector2 Guard;
            public Vector2 Tip;
            public Vector2 Pommel;
            public bool HasSword;

            /// <summary>How wide the whole of him is seen (1 side on, never under Thin), and the upright line he is narrowed about, x in the world.</summary>
            public float Turn;
            public float TurnAbout;
        }

        /// <summary>A vector turned clockwise (on screen) by <paramref name="angle"/>.</summary>
        public static Vector2 Turned(Vector2 v, float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>The way a limb points: from straight down, forward positive.</summary>
        private static Vector2 Down(float angle) => new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));

        /// <summary>
        /// How high the hips are over the ground for these legs: on whichever foot is lower,
        /// so a stride lifts the other foot and a wide stance lowers him.
        /// </summary>
        public static float HipsOver(in Pose pose)
        {
            if (pose.Air)
                return HipsHeight + pose.Rise;

            float near = NearHip.Y + NearLegLength * (float)Math.Cos(pose.NearLeg);
            float far = FarHip.Y + FarLegLength * (float)Math.Cos(pose.FarLeg);

            return Math.Max(near, far) + pose.Rise;
        }

        /// <summary>
        /// Everything of him for this pose, standing with the ground between his feet at
        /// <paramref name="feet"/> and facing <paramref name="facing"/>: the pictures to draw,
        /// back to front, added to <paramref name="parts"/> (if given), and where his joints
        /// and his sword came out.
        /// </summary>
        public static Frame Build(in Pose pose, Vector2 feet, int facing, List<Part> parts = null)
        {
            int faces = facing >= 0 ? 1 : -1;

            // Turned past edge on, the whole of him is the mirror of himself. His head goes by
            // the way he is said to face, not by how the body is seen: looking back over his
            // shoulder, it goes on looking the same way while the body comes round under it.
            int d = pose.Turn < 0f ? -faces : faces;
            int looks = pose.HeadTurn < 0f ? -faces : faces;

            float wide = Math.Max(Thin, Math.Min(1f, Math.Abs(pose.Turn)));

            Vector2 hips = new Vector2(0f, -HipsOver(pose));

            Vector2 nearShoulder = hips + Turned(NearShoulder, pose.Lean);
            Vector2 farShoulder = hips + Turned(FarShoulder, pose.Lean);
            Vector2 neck = hips + Turned(Neck, pose.Lean);

            Vector2 nearFist = nearShoulder + Down(pose.NearArm) * NearArmLength;
            Vector2 farFist = farShoulder + Down(pose.FarArm) * FarArmLength;

            float headTurn = pose.Lean + pose.Nod;

            bool held = pose.Hand == 1 || pose.Hand == 2;
            Vector2 grip = pose.Hand == 2 ? Vector2.Lerp(farFist, nearFist, pose.Pass) : Vector2.Lerp(nearFist, farFist, pose.Pass);
            float reach = pose.Reach <= 0f ? 1f : pose.Reach;
            Vector2 along = new Vector2((float)Math.Cos(pose.Sword), (float)Math.Sin(pose.Sword));

            Frame frame = new Frame
            {
                Hips = Seen(hips, feet, d, wide),
                NearFist = Seen(nearFist, feet, d, wide),
                FarFist = Seen(farFist, feet, d, wide),
                NeckAt = Seen(neck, feet, d, wide),
                EyeAt = Seen(neck + Turned(Eye, headTurn), feet, d, wide),
                CrownAt = Seen(neck + Turned(Crown, headTurn), feet, d, wide),
                HasSword = held,
                Grip = Seen(grip, feet, d, wide),
                Guard = Seen(grip + along * (GripToGuard * reach), feet, d, wide),
                Tip = Seen(grip + along * (GripToTip * reach), feet, d, wide),
                Pommel = Seen(grip - along * (GripToPommel * reach), feet, d, wide),
                Blade = d > 0 ? pose.Sword : MathHelper.Pi - pose.Sword,
                Turn = wide,
                TurnAbout = feet.X,
            };

            if (parts == null)
                return frame;

            // The sword: behind everything in the far hand or when it is behind him, over
            // everything in the near hand.
            bool swordFirst = held && (pose.Behind || pose.Hand == 2);

            if (swordFirst)
                parts.Add(SwordPart(World(grip, feet, d), frame.Blade, d > 0 ? pose.CleanClockwise : !pose.CleanClockwise, reach));

            parts.Add(Limb(FarArm, farShoulder, pose.FarArm - FarArmRest, feet, d));
            parts.Add(Limb(FarLeg, hips + FarHip, pose.FarLeg - FarLegRest, feet, d));
            parts.Add(Limb(NearLeg, hips + NearHip, pose.NearLeg - NearLegRest, feet, d));
            parts.Add(Piece(Torso, hips, pose.Lean, feet, d));
            parts.Add(Limb(NearArm, nearShoulder, pose.NearArm - NearArmRest, feet, d));

            // His head alone, turning: narrower across, about the neck.
            Part head = Piece(Head, neck, looks == d ? headTurn : -headTurn, feet, looks);

            head.At = World(neck, feet, d);

            if (Math.Abs(pose.HeadTurn) < 1f)
            {
                head.Stretch = Math.Max(Thin, Math.Abs(pose.HeadTurn));
                head.Axis = 0f;
            }

            parts.Add(head);

            if (held && !swordFirst)
                parts.Add(SwordPart(World(grip, feet, d), frame.Blade, d > 0 ? pose.CleanClockwise : !pose.CleanClockwise, reach));

            return frame;
        }

        /// <summary>
        /// The sword on its own, not in his hand: standing in the ground, or anywhere. Its grip
        /// in the world, the way its blade points (a world angle, clockwise from +x), and
        /// which side of the blade, in the world, the clean edge is on.
        /// </summary>
        public static Part LooseSword(Vector2 grip, float blade, bool cleanClockwise) => SwordPart(grip, blade, cleanClockwise, 1f);

        private static Vector2 World(Vector2 local, Vector2 feet, int d) => feet + new Vector2(d * local.X, local.Y);

        /// <summary>Where a point of him is seen, turning: that much nearer the line through his feet.</summary>
        private static Vector2 Seen(Vector2 local, Vector2 feet, int d, float wide) => feet + new Vector2(d * local.X * wide, local.Y);

        /// <summary>
        /// How far over the ground the hand that has the sword is, for this pose, standing on
        /// level ground: what the point of the sword can be kept on the ground by.
        /// </summary>
        public static float FistOver(in Pose pose)
        {
            Vector2 hips = new Vector2(0f, -HipsOver(pose));
            bool far = pose.Hand == 2;
            Vector2 shoulder = hips + Turned(far ? FarShoulder : NearShoulder, pose.Lean);
            Vector2 fist = shoulder + Down(far ? pose.FarArm : pose.NearArm) * (far ? FarArmLength : NearArmLength);

            return -fist.Y;
        }

        /// <summary>
        /// The steepest the blade may point down, ahead of him, for its point to rest on the
        /// ground and not go through it, with the fist this far over the ground: the angle
        /// below level, 0 to a quarter turn. Seen shorter (Pose.Reach), it may point steeper.
        /// </summary>
        public static float Dip(float fistOver, float reach = 1f)
        {
            float length = GripToTip * (reach <= 0f ? 1f : reach);

            if (fistOver >= length)
                return MathHelper.PiOver2;

            return (float)Math.Asin(Math.Max(0f, fistOver) / length);
        }

        /// <summary>
        /// The pose with the blade kept out of level ground under his feet: where it points
        /// below level by more than its point has room for, it is laid on the ground instead,
        /// ahead of him or behind, whichever side it was on. True if it had to be.
        /// </summary>
        public static bool Ground(ref Pose pose)
        {
            if (pose.Hand == 0)
                return false;

            float s = (float)Math.Sin(pose.Sword);

            if (s <= 0f)
                return false;

            float dip = Dip(FistOver(pose), pose.Reach);

            if (s <= (float)Math.Sin(dip) + 0.0001f)
                return false;

            // Ahead (cosine positive) the blade lies at dip; behind, at a half turn less dip.
            // Kept on the same turn of the circle it was on, so nothing spins round to get there.
            float turns = (float)Math.Floor(pose.Sword / MathHelper.TwoPi);
            bool ahead = Math.Cos(pose.Sword) >= 0.0;

            pose.Sword = turns * MathHelper.TwoPi + (ahead ? dip : MathHelper.Pi - dip);
            return true;
        }

        /// <summary>A piece turned clockwise by <paramref name="turn"/> (facing right) about its pivot.</summary>
        private static Part Piece(int piece, Vector2 pivotAt, float turn, Vector2 feet, int d)
        {
            Vector2 pivot = Pivots[piece];

            // The art faces left: facing right is the mirror of it, pivot and all.
            return new Part
            {
                Piece = piece,
                At = World(pivotAt, feet, d),
                Origin = d > 0 ? new Vector2(Sizes[piece].X - pivot.X, pivot.Y) : pivot,
                Rotation = d > 0 ? turn : -turn,
                FlipX = d > 0,
                Stretch = 1f,
            };
        }

        /// <summary>A limb swung forward by <paramref name="swing"/> from how it is drawn: forward is anticlockwise, facing right.</summary>
        private static Part Limb(int piece, Vector2 pivotAt, float swing, Vector2 feet, int d) => Piece(piece, pivotAt, -swing, feet, d);

        private static Part SwordPart(Vector2 gripAt, float blade, bool clockwise, float reach)
        {
            Vector2 grip = Pivots[Sword];

            // As drawn, the blade points an eighth above ahead with its clean edge on the
            // anticlockwise side; mirrored across, it points an eighth above behind with the
            // clean edge on the clockwise one.
            return new Part
            {
                Piece = Sword,
                At = gripAt,
                Origin = clockwise ? new Vector2(Sizes[Sword].X - grip.X, grip.Y) : grip,
                Rotation = clockwise ? blade - (MathHelper.Pi - SwordDrawn) : blade - SwordDrawn,
                FlipX = clockwise,
                Stretch = reach,
                Axis = blade,
            };
        }
    }
}
