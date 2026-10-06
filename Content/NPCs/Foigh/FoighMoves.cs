using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// How Foigh holds himself in everything he does: every movement of his as poses at moments
    /// (FoighTrack), the few that are worked out instead (the walk, the run, the fall onto the
    /// player, the turn of the whole of him with the sword going round him), how far each
    /// carries him along the ground (<see cref="Advance"/>), and the ticks at which things
    /// happen in each (the constants beside each one).
    ///
    /// The fight (Foigh.Fight) says which of these he is in and for how long; the drawing asks
    /// here what that looks like, at whole ticks for him and between them for what his blade
    /// leaves behind. So the blade that is drawn, the blade that hits and the trail are one
    /// sum. Nothing here touches the game.
    /// </summary>
    public static class FoighMoves
    {
        // ============================================================
        // AT REST: the dragging stance, the walk, the run
        // ============================================================

        /// <summary>Asked for a blade pointing well into the ground behind him; FoighRig.Ground lays it on it.</summary>
        private const float Dragged = 150f;

        private static FoighTrack From()
        {
            return new FoighTrack().At(0f)
                .Lean(7f).Nod(5f).Near(-13.2f).Far(-4.4f).Legs(3f, 0f)
                .Hand(1).Sword(Dragged).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f);
        }

        /// <summary>Standing with the sword behind him, its point on the ground.</summary>
        public static readonly FoighRig.Pose Drag = From().Sample(0f);

        /// <summary>A step is this far along the ground, and the legs swing this far either way for it.</summary>
        public const float Stride = 15f;
        private const float WalkSwing = 0.42f;

        /// <summary>
        /// Walking, the sword dragged: <paramref name="gait"/> is how far he has walked, in
        /// steps (a whole number at each footfall). A heavy walk: he sinks onto each step and
        /// the body nods with it; the far arm swings, the near one only gives with the sword.
        /// </summary>
        public static FoighRig.Pose Walk(float gait)
        {
            FoighRig.Pose pose = Drag;

            float s = (float)Math.Sin(gait * MathHelper.Pi);
            float c = (float)Math.Cos(gait * MathHelper.TwoPi);

            pose.NearLeg = FoighRig.NearLegRest + WalkSwing * s;
            pose.FarLeg = FoighRig.FarLegRest - WalkSwing * s;
            pose.Lean = MathHelper.ToRadians(9f) + 0.03f * c;
            pose.Nod = MathHelper.ToRadians(6f) - 0.04f * c;
            pose.FarArm = FoighRig.FarArmRest + 0.28f * s;
            pose.NearArm = FoighRig.NearArmRest - 0.05f * s;

            return pose;
        }

        /// <summary>
        /// Turning about to face the other way, the sword coming round with him:
        /// <paramref name="share"/> is how far through the turn he is, 0 to 1. Whoever asks has
        /// already turned the way he faces: at 0 this is him as he was, seen from his other side.
        /// </summary>
        public const int AboutTicks = 14;

        public static FoighRig.Pose About(FoighRig.Pose pose, float share)
        {
            float t = Easing.Smoother(share);

            pose.Turn = -(float)Math.Cos(t * MathHelper.Pi);
            pose.Lean += MathHelper.ToRadians(-6f) * (float)Math.Sin(t * MathHelper.Pi);

            return pose;
        }

        /// <summary>
        /// Running, with or without the sword (after it, when he has thrown it): bent into it,
        /// the legs wide, the arms going. <paramref name="gait"/> as the walk's, a step being
        /// <see cref="RunStride"/> long.
        /// </summary>
        public const float RunStride = 22f;

        public static FoighRig.Pose Run(float gait, bool armed)
        {
            FoighRig.Pose pose = Drag;

            float s = (float)Math.Sin(gait * MathHelper.Pi);

            pose.NearLeg = FoighRig.NearLegRest + 0.85f * s;
            pose.FarLeg = FoighRig.FarLegRest - 0.85f * s;
            pose.Lean = MathHelper.ToRadians(24f);
            pose.Nod = MathHelper.ToRadians(-10f);
            pose.FarArm = 0.9f * s + 0.2f;

            if (armed)
            {
                // The sword off the ground behind him, bouncing a little with his steps.
                pose.NearArm = MathHelper.ToRadians(-40f) - 0.1f * s;
                pose.Sword = MathHelper.ToRadians(196f) + 0.04f * s;
            }
            else
            {
                pose.Hand = 0;
                pose.NearArm = -0.9f * s + 0.2f;
            }

            return pose;
        }

        // ============================================================
        // THE ROAD: his back to them, the turn, the sword out of the ground
        // ============================================================

        /// <summary>
        /// Where the sword stands in the ground before he takes it, from the ground between
        /// his feet once he has turned to them: the middle of its grip, and the way the blade
        /// points (down, and away behind him). Before he turns it is the mirror of this, ahead.
        /// </summary>
        public static readonly Vector2 PlantedGrip = new Vector2(-18.3f, -46.4f);
        public const float PlantedBlade = 115f;

        /// <summary>His back to them, the sword in the ground: his hands empty, his head down.</summary>
        public static readonly FoighRig.Pose Await = new FoighTrack().At(0f)
            .Lean(4f).Nod(14f).Near(-13.2f).Far(-4.4f).Legs(3f, 0f)
            .Hand(0).Sword(PlantedBlade).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .Sample(0f);

        /// <summary>
        /// He turns round: his head first, over his shoulder, and a look; then the whole of
        /// him. By its end the body is seen from its other side (Turn at minus one): whoever
        /// runs this then has him face the other way, standing.
        /// </summary>
        public const int LookAt = 24;
        public const int TurnAt = 70;
        public const int TurnTicks = 112;

        public static readonly FoighTrack TurnRound = new FoighTrack(Await)
            .At(LookAt, Ease.SineInOut).HeadTurn(-1f).Nod(2f)
            .At(TurnAt).Nod(0f)
            .At(TurnTicks - 8, Ease.SineInOut).Turn(-1f).Lean(2f)
            .At(TurnTicks).Lean(3f).Nod(3f);

        /// <summary>Turned to them, hands empty, the sword in the ground behind him: how he stands while he talks.</summary>
        public static readonly FoighRig.Pose Talking = new FoighTrack().At(0f)
            .Lean(3f).Nod(3f).Near(-13.2f).Far(-4.4f).Legs(3f, 0f)
            .Hand(0).Sword(PlantedBlade).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .Sample(0f);

        /// <summary>
        /// The sword out of the ground: he reaches back for it, strains, levers it out through
        /// the ground behind him, swings it up over his head and holds it there; then it comes
        /// back over and down behind him, into the dragging stance.
        /// </summary>
        public const int DrawTakes = 18;
        public const int DrawRips = 32;
        public const int DrawHigh = 60;
        public const int DrawFalls = 74;
        public const int DrawLands = 94;
        public const int DrawTicks = 108;

        public static readonly FoighTrack DrawSword = new FoighTrack(Talking)
            .At(DrawTakes, Ease.SineInOut).Near(-139.8f).Lean(0f).Nod(-3f)
            .At(DrawTakes + 0.01f).Hand(1).Sword(PlantedBlade)
            .At(DrawRips, Ease.SineInOut).Near(-146f).Lean(11f).Nod(4f).Legs(-14f, 12f).Sword(121f)
            .At(DrawRips + 8f, Ease.PolyIn, 2).Near(-168f).Lean(-6f).Nod(-6f).Sword(196f)
            .At(DrawHigh, Ease.PolyOut, 2).Near(152f + -360f).Lean(3f).Nod(-12f).Legs(-10f, 8f).Sword(302f)
            .At(DrawFalls).Near(150f - 360f).Sword(298f).Nod(-10f)
            .At(DrawLands, Ease.PolyIn, 2).Near(-13.2f).Lean(7f).Nod(5f).Legs(3f, 0f).Sword(Dragged)
            .At(DrawTicks).Sword(Dragged);

        // ============================================================
        // COMBO ONE: down, up, the thrust, the leap back
        // ============================================================

        public const int OneWind = 26;
        public const int OneHold = 34;
        public const int OneDown = 41;
        public const int OneRest = 52;
        public const int OneUp = 58;
        public const int OneOver = 66;

        /// <summary>The thrust is chambered by here; what the fight adds between this and the thrust is the wait he makes them guess at.</summary>
        public const int OneChambered = 84;
        public const int OneThrust = 90;
        public const int OneHeld = 104;
        public const int OneGather = 112;
        public const int OneLeap = 112;
        public const int OneTicks = 150;

        public static readonly FoighTrack One = From()
            .At(14f, Ease.SineIn).Lean(-2f).Near(118f).Sword(238f).Legs(-12f, 10f).Nod(0f)
            .At(OneWind, Ease.SineOut).Lean(-12f).Nod(-8f).Near(172f).Sword(256f).Legs(-22f, 16f)
            .At(OneHold).Near(176f).Sword(250f).Lean(-14f)
            .At(OneDown, Ease.PolyIn, 3).Lean(24f).Nod(10f).Near(60f).Sword(386f).Legs(20f, -24f)
            .At(OneRest, Ease.SineOut).Lean(19f).Near(54f).Sword(386f).Clean(false)
            .At(OneUp, Ease.PolyIn, 2).Lean(-12f).Nod(-8f).Near(168f).Sword(246f).Legs(-16f, 18f)
            .At(OneOver, Ease.SineOut).Near(196f).Sword(214f).Lean(-16f)
            .At(OneChambered, Ease.SineInOut).Near(-52f + 360f).Sword(360f).Lean(-9f).Nod(2f).Legs(-26f, 22f).Clean(true)
            .At(OneThrust, Ease.PolyIn, 2).Near(84f + 360f).Sword(358f).Lean(30f).Nod(-6f).Legs(34f, -38f)
            .At(OneHeld, Ease.SineOut).Near(80f + 360f).Lean(24f)
            .At(OneGather, Ease.SineInOut).Near(34f + 360f).Sword(372f).Lean(16f).Legs(-12f, 12f)
            .At(OneLeap + 6f, Ease.SineOut).Air(true).Near(48f + 360f).Sword(346f).Lean(-8f).Nod(4f).Legs(44f, -30f)
            .At(OneLeap + 22f, Ease.SineInOut).Near(120f + 360f).Sword(268f).Lean(-4f).Legs(26f, -16f)
            .At(OneLeap + 28f, Ease.SineIn).Air(false).Near(170f + 360f).Sword(212f).Lean(4f).Legs(-10f, 12f)
            .At(OneTicks, Ease.SineOut).Near(-13.2f + 720f).Sword(Dragged).Lean(7f).Nod(5f).Legs(3f, 0f);

        // ============================================================
        // COMBO TWO: the leap onto them, point first
        // ============================================================

        public const int TwoCrouch = 18;

        public static readonly FoighTrack TwoGather = From()
            .At(9f, Ease.SineInOut).Lean(26f).Nod(-10f).Near(20f).Legs(-40f, 38f)
            .At(TwoCrouch, Ease.SineIn).Lean(20f).Near(140f).Sword(232f).Legs(-46f, 42f).Nod(-16f);

        /// <summary>
        /// In the air on the way to them: <paramref name="share"/> is how far through the leap
        /// he is, 0 as he leaves the ground and 1 as he comes down; <paramref name="aim"/> is
        /// where the point is aimed, the blade's own angle. Up with the sword over his head,
        /// over the top with it coming round, and down behind its point.
        /// </summary>
        public static FoighRig.Pose Plunge(float share, float aim)
        {
            FoighRig.Pose pose = TwoGather.Sample(TwoCrouch);
            pose.Air = true;

            float t = MathHelper.Clamp(share, 0f, 1f);
            float over = Easing.Smoother(MathHelper.Clamp(t / 0.55f, 0f, 1f));
            float down = Easing.Smoother(MathHelper.Clamp((t - 0.4f) / 0.6f, 0f, 1f));

            // The blade from up behind him, over the top, down onto its aim.
            float from = MathHelper.ToRadians(232f);
            float to = MathHelper.TwoPi + aim;

            pose.Sword = MathHelper.Lerp(from, to, over);
            pose.NearArm = MathHelper.ToRadians(MathHelper.Lerp(140f, 78f, over) + 14f * down);
            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(-4f, 70f, over));
            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(8f, 30f, over) + 26f * down);
            pose.Nod = MathHelper.ToRadians(-16f + 26f * down);
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(-30f, 42f, over) - 70f * down);
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(36f, 58f, over) - 34f * down);
            pose.Rise = 0f;
            pose.CleanClockwise = true;

            return pose;
        }

        /// <summary>The steepest and the shallowest he brings the point down at.</summary>
        public const float AimSteepest = 1.4f;
        public const float AimShallowest = 0.82f;

        /// <summary>
        /// Down, and the sword in the ground by more than half its blade (it is drawn as it
        /// is held; the ground hides the rest): he stands behind it with it at his shoulder,
        /// at the angle it went in at.
        /// </summary>
        public static FoighRig.Pose Stuck(float aim)
        {
            FoighRig.Pose pose = Drag;

            pose.Lean = MathHelper.ToRadians(15f);
            pose.Nod = MathHelper.ToRadians(12f);
            pose.NearArm = MathHelper.ToRadians(95f);
            pose.FarArm = MathHelper.ToRadians(48f);
            pose.NearLeg = MathHelper.ToRadians(-24f);
            pose.FarLeg = MathHelper.ToRadians(22f);
            pose.Sword = aim;
            pose.CleanClockwise = true;

            return pose;
        }

        /// <summary>How long it stays in the ground: two seconds, by request. He pulls at it twice, and at the third it comes.</summary>
        public const int StuckTicks = 120;
        public const int FirstTug = 34;
        public const int SecondTug = 72;
        public const int TugTicks = 16;
        public const int Heave = 104;

        private static readonly int[] Tugs = { FirstTug, SecondTug };

        /// <summary>
        /// Pulling at it: how far back he throws himself at <paramref name="tick"/> of the two
        /// seconds, 0 to 1. Each pull is a jerk back and a slow giving up of it.
        /// </summary>
        public static float Tug(float tick)
        {
            float pull = 0f;

            foreach (int at in Tugs)
            {
                float t = (tick - at) / TugTicks;

                if (t >= 0f && t <= 1f)
                    pull = Math.Max(pull, (float)Math.Sin(Math.Pow(t, 0.55) * Math.PI));
            }

            return pull;
        }

        /// <summary>
        /// Stuck, at this tick of it, pulling at it or not: thrown back by <see cref="Tug"/>,
        /// the hand staying where the grip is.
        /// </summary>
        public static FoighRig.Pose Pulling(float tick, float aim)
        {
            FoighRig.Pose pose = Stuck(aim);
            float pull = Tug(tick);

            // At the last he gets under it.
            float heave = Easing.Smoother(MathHelper.Clamp((tick - Heave) / (StuckTicks - Heave), 0f, 1f));

            pose.Lean += MathHelper.ToRadians(-24f * pull + 8f * heave);
            pose.Nod += MathHelper.ToRadians(-20f * pull);
            pose.NearArm += MathHelper.ToRadians(-12f * pull + 6f * heave);
            pose.FarArm += MathHelper.ToRadians(40f * pull + 30f * heave);
            pose.NearLeg += MathHelper.ToRadians(-10f * pull - 14f * heave);
            pose.FarLeg += MathHelper.ToRadians(10f * pull + 16f * heave);
            pose.Sword += MathHelper.ToRadians(-3f * pull);

            return pose;
        }

        /// <summary>Out of the ground: torn up out of it ahead of him, over his head, and down behind into the dragging stance.</summary>
        public const int FreeRips = 6;
        public const int FreeTicks = 34;

        public static FoighTrack Free(float aim)
        {
            FoighRig.Pose stuck = Pulling(StuckTicks, aim);
            float at = MathHelper.ToDegrees(aim);

            return new FoighTrack(stuck)
                .At(FreeRips, Ease.PolyIn, 2).Sword(at - 110f).Near(150f).Lean(-10f).Nod(-10f).Far(20f).Clean(false)
                .At(18f, Ease.SineOut).Sword(at - 196f).Near(196f).Lean(-14f)
                .At(FreeTicks, Ease.SineInOut).Sword(Dragged - 360f).Near(-13.2f + 360f).Far(-4.4f).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);
        }

        /// <summary>Down on them, having hit: he lands behind his point with the sword free, low, ready for what comes next.</summary>
        public const int LandedTicks = 12;

        public static FoighTrack Landed(float aim)
        {
            FoighRig.Pose down = Plunge(1f, aim);
            down.Air = false;

            return new FoighTrack(down)
                .At(5f, Ease.SineOut).Lean(34f).Nod(8f).Near(64f).Far(30f).Legs(-40f, 40f).Sword(386f)
                .At(LandedTicks, Ease.SineInOut).Lean(16f).Near(40f).Legs(-16f, 14f).Sword(386f);
        }

        // ============================================================
        // COMBO THREE: the blow down with the bottom hand, the turn with the sword going
        // round him, the knife off the top hand
        // ============================================================

        public const int ThreeTakes = 12;
        public const int ThreeWind = 30;
        public const int ThreeHold = 46;
        public const int ThreeDown = 52;
        public const int ThreeCoil = 70;
        public const int ThreeSpin = 90;
        public const int ThreeKnife = 99;
        public const int ThreeBack = 112;
        public const int ThreeTicks = 138;

        /// <summary>The blow down, as far as the coil for the turn.</summary>
        public static readonly FoighTrack Three = From()
            .At(ThreeTakes - 5f, Ease.SineIn).Near(132f).Far(96f).Sword(252f).Lean(-4f).Nod(-3f).Legs(-8f, 6f)
            .At(ThreeTakes, Ease.SineInOut).Near(150f).Far(128f).Sword(262f).Lean(-6f).Nod(-4f).Legs(-10f, 8f).Pass(1f)
            .At(ThreeTakes + 0.01f).Hand(2).Pass(0f)
            .At(ThreeWind, Ease.SineOut).Far(188f).Near(62f).Sword(236f).Lean(-18f).Nod(-12f).Legs(-28f, 22f)
            .At(ThreeHold).Far(192f).Sword(230f).Lean(-21f)
            .At(ThreeDown, Ease.PolyIn, 3).Far(52f).Near(24f).Sword(392f).Lean(34f).Nod(14f).Legs(26f, -32f)
            .At(ThreeCoil, Ease.SineInOut).Far(70f).Near(-30f).Sword(378f).Lean(20f).Nod(4f).Legs(8f, -10f);

        /// <summary>
        /// The whole of him turning with the sword held out, as it is seen from the side:
        /// <paramref name="round"/> is how far round he is, 0 to 1 a whole turn. He turns
        /// away from the eye first, so the sword goes round the far side of him (behind
        /// him, shortening to nothing as it points away, and out again behind his back),
        /// then round the near side (in front of him, shortening as it points at the eye).
        /// </summary>
        public static FoighRig.Pose Round(FoighRig.Pose from, float round, bool farHand)
        {
            float a = round * MathHelper.TwoPi;
            float c = (float)Math.Cos(a);
            float s = (float)Math.Sin(a);

            FoighRig.Pose pose = from;

            pose.Hand = farHand ? 2 : 1;
            pose.Turn = c;

            // The sword is as long as it is seen from the side, and nothing when it points at
            // the eye or away: the whole of him is drawn narrower by his turn (never under
            // FoighRig.Thin), and the blade by what is left over of its own.
            float seen = Math.Max(0.1f, Math.Abs(c));
            pose.Reach = seen / Math.Max(FoighRig.Thin, Math.Abs(c));

            // Out level from the shoulder, a little down at the point. Seen from its other
            // side he is the mirror of himself, and ahead of him is behind: so the blade's
            // own angle is the same all the way round.
            pose.Sword = MathHelper.ToRadians(6f);
            pose.Behind = s > 0f;
            pose.CleanClockwise = true;

            float lift = (float)Math.Sin(round * MathHelper.Pi);

            pose.Lean = MathHelper.ToRadians(10f);
            pose.Nod = MathHelper.ToRadians(2f);
            pose.NearLeg = MathHelper.ToRadians(-18f + 30f * lift);
            pose.FarLeg = MathHelper.ToRadians(16f - 26f * lift);

            if (farHand)
            {
                pose.FarArm = MathHelper.ToRadians(88f);
                pose.NearArm = MathHelper.ToRadians(-50f);
            }
            else
            {
                pose.NearArm = MathHelper.ToRadians(88f);
                pose.FarArm = MathHelper.ToRadians(-40f);
            }

            return pose;
        }

        /// <summary>Whether the sword going round him is out far enough to cut, this far round, and on which side: 1 ahead of the way he started, -1 behind.</summary>
        public static int RoundCuts(float round)
        {
            float c = (float)Math.Cos(round * MathHelper.TwoPi);
            return c > 0.45f ? 1 : c < -0.45f ? -1 : 0;
        }

        /// <summary>After the turn: the knife off the top hand, and the sword back into it and down behind him.</summary>
        public static readonly FoighTrack ThreeEnd = new FoighTrack(Round(Three.Sample(ThreeCoil), 1f, true))
            .At(5f, Ease.SineOut).Near(-84f).Lean(2f).Nod(-4f).Legs(-12f, 10f)
            .At(ThreeKnife - ThreeSpin, Ease.PolyIn, 3).Near(104f).Lean(16f).Nod(6f)
            .At(ThreeBack - ThreeSpin - 5f, Ease.SineOut).Near(76f).Lean(10f).Far(108f).Sword(6f - 40f)
            .At(ThreeBack - ThreeSpin, Ease.SineInOut).Near(84f).Far(118f).Sword(6f - 60f).Pass(1f)
            .At(ThreeBack - ThreeSpin + 0.01f).Hand(1).Pass(0f)
            .At(ThreeTicks - ThreeSpin - 8f, Ease.SineInOut).Near(150f).Far(40f).Sword(-150f).Lean(2f)
            .At(ThreeTicks - ThreeSpin, Ease.SineInOut).Near(-13.2f + 360f).Far(-4.4f).Sword(Dragged - 360f).Lean(7f).Nod(5f).Legs(3f, 0f);

        // ============================================================
        // COMBO FOUR: up, the sword thrown, down, after it, and twice round with it
        // ============================================================

        public const int FourCrouch = 16;

        public static readonly FoighTrack FourGather = From()
            .At(8f, Ease.SineInOut).Lean(22f).Nod(-12f).Legs(-40f, 38f).Near(10f)
            .At(FourCrouch, Ease.SineIn).Lean(16f).Nod(-20f).Legs(-48f, 44f).Near(-40f).Sword(186f);

        /// <summary>
        /// In the air with the sword back over his shoulder, point at them: <paramref name="aim"/>
        /// is where it points; <paramref name="wound"/> how far back the arm is drawn, 0 to 1,
        /// and past that (to 2) how far through the throw.
        /// </summary>
        public static FoighRig.Pose Aloft(float aim, float wound)
        {
            FoighRig.Pose pose = Drag;
            pose.Air = true;

            float back = Easing.Smoother(MathHelper.Clamp(wound, 0f, 1f));
            float thrown = Easing.Apply(Ease.PolyOut, MathHelper.Clamp(wound - 1f, 0f, 1f), 3);

            pose.Lean = MathHelper.ToRadians(-10f * back + 34f * thrown);
            pose.Nod = MathHelper.ToRadians(-8f + 16f * thrown);
            pose.NearArm = MathHelper.ToRadians(-40f - 150f * back + 290f * thrown);
            pose.FarArm = MathHelper.ToRadians(60f * back - 70f * thrown);
            pose.NearLeg = MathHelper.ToRadians(-30f + 50f * back - 60f * thrown);
            pose.FarLeg = MathHelper.ToRadians(40f - 10f * back + 20f * thrown);

            // From trailing behind him to laid back along his aim.
            pose.Sword = MathHelper.Lerp(MathHelper.ToRadians(186f), MathHelper.TwoPi + aim, back);
            pose.CleanClockwise = true;

            if (wound > 1.15f)
                pose.Hand = 0;

            return pose;
        }

        /// <summary>Coming down without it, and landing.</summary>
        public static FoighRig.Pose Falling(float share)
        {
            FoighRig.Pose pose = Drag;
            pose.Air = true;
            pose.Hand = 0;

            float t = MathHelper.Clamp(share, 0f, 1f);

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(26f, 8f, t));
            pose.Nod = MathHelper.ToRadians(10f);
            pose.NearArm = MathHelper.ToRadians(MathHelper.Lerp(120f, 150f, t));
            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(-20f, 130f, t));
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(-40f, 10f, t));
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(30f, 26f, t));

            return pose;
        }

        /// <summary>Landed without it: down into his knees, and up into the run.</summary>
        public const int TouchTicks = 10;

        public static readonly FoighTrack Touch = new FoighTrack(Falling(1f))
            .At(0.01f).Air(false)
            .At(4f, Ease.SineOut).Lean(30f).Nod(14f).Legs(-52f, 50f).Near(40f).Far(60f)
            .At(TouchTicks, Ease.SineInOut).Lean(22f).Nod(-6f).Legs(-20f, 18f).Near(10f).Far(30f);

        /// <summary>
        /// At the sword in the ground: the hand to its grip (<paramref name="reach"/> is where
        /// the grip is from his shoulder, facing right: what the arm points at).
        /// </summary>
        public const int TakeTicks = 8;

        public static FoighRig.Pose Taking(float share, float reach)
        {
            FoighRig.Pose pose = Run(0f, false);
            float t = Easing.Smoother(share);

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(24f, 12f, t));
            pose.Nod = MathHelper.ToRadians(MathHelper.Lerp(-10f, 4f, t));
            pose.NearArm = MathHelper.Lerp(pose.NearArm, reach, t);
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(0f, -22f, t));
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(0f, 20f, t));

            return pose;
        }

        /// <summary>Twice round with it, and how long each turn takes.</summary>
        public const int RoundTicks = 18;
        public const int TwiceTicks = RoundTicks * 2;

        /// <summary>After the second turn: back into the dragging stance.</summary>
        public const int FourEndTicks = 26;

        public static readonly FoighTrack FourEnd = new FoighTrack(Round(Drag, 1f, false))
            .At(10f, Ease.SineOut).Near(120f).Sword(-70f).Lean(2f).Far(10f).Legs(-10f, 8f)
            .At(FourEndTicks, Ease.SineInOut).Near(-13.2f + 360f).Far(-4.4f).Sword(Dragged - 360f).Lean(7f).Nod(5f).Legs(3f, 0f);

        // ============================================================
        // THE KNIVES
        // ============================================================

        /// <summary>One knife off the bottom hand: the hand back to his belt, and out. It leaves at <see cref="KnifeLeaves"/>.</summary>
        public const int KnifeBack = 10;
        public const int KnifeLeaves = 13;
        public const int KnifeTicks = 30;

        /// <summary>Three, by request twenty ticks apart: each from the hand drawn back again.</summary>
        public const int KnifeEvery = 20;

        public static FoighRig.Pose Knife(float tick, int knives)
        {
            FoighRig.Pose pose = Drag;

            // Which knife this tick belongs to, and how far into it.
            int which = (int)MathHelper.Clamp((float)Math.Floor(tick / KnifeEvery), 0f, knives - 1);
            float t = tick - which * KnifeEvery;

            float back;
            float out_;

            if (t <= KnifeBack)
            {
                back = Easing.Apply(Ease.SineOut, t / KnifeBack);
                out_ = 0f;
            }
            else if (t <= KnifeLeaves + 1f)
            {
                back = 1f;
                out_ = Easing.Apply(Ease.PolyIn, (t - KnifeBack) / (KnifeLeaves + 1f - KnifeBack), 2);
            }
            else
            {
                back = 1f;
                out_ = 1f - 0.55f * Easing.Smoother((t - KnifeLeaves - 1f) / (KnifeTicks - KnifeLeaves - 1f));
            }

            // Between knives the hand comes from where the last throw left it.
            float rest = which > 0 && t <= KnifeBack ? MathHelper.Lerp(64f, -96f, back) : MathHelper.Lerp(-4.4f, -96f, back);

            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(rest, 100f, out_));

            // Back onto his heel as the hand goes back, and through onto the front foot with it.
            float wound = back * (1f - out_);

            pose.Lean = MathHelper.ToRadians(7f - 15f * wound + 13f * out_);
            pose.Nod = MathHelper.ToRadians(5f - 9f * wound + 3f * out_);
            pose.NearLeg = MathHelper.ToRadians(3f - 12f * wound + 20f * out_);
            pose.FarLeg = MathHelper.ToRadians(0f + 10f * wound - 18f * out_);
            pose.NearArm = MathHelper.ToRadians(-13.2f - 6f * out_);

            return pose;
        }

        /// <summary>How long a throw of this many knives takes, the last one's follow-through and all.</summary>
        public static int KnifeLength(int knives) => (knives - 1) * KnifeEvery + KnifeTicks;

        /// <summary>The tick the knife of this number (from 0) leaves his hand.</summary>
        public static int KnifeAt(int which) => which * KnifeEvery + KnifeLeaves;

        // ============================================================
        // THE FLASH STEP, and the cut up from behind them
        // ============================================================

        /// <summary>Going: bent flat into it, the sword trailing off the ground.</summary>
        public static readonly FoighRig.Pose Flash = new FoighTrack().At(0f)
            .Lean(38f).Nod(-14f).Near(-58f).Far(-70f).Legs(44f, -46f).Air(true)
            .Hand(1).Sword(188f).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .Sample(0f);

        /// <summary>
        /// Behind them: there, low, the point on the ground ahead of him, for half a second
        /// (by request, so they have it to get out of the way in); then the cut up, and a
        /// moment before he is gone again.
        /// </summary>
        public const int BehindWait = 30;
        public const int BehindCut = 36;
        public const int BehindTicks = 52;

        public static readonly FoighTrack Behind = new FoighTrack().At(0f)
            .Lean(24f).Nod(-6f).Near(46f).Far(20f).Legs(-34f, 32f)
            .Hand(1).Sword(60f).Clean(false).Reach(1f).Turn(1f).HeadTurn(1f)
            .At(BehindWait - 8f, Ease.SineInOut).Lean(30f).Near(26f).Legs(-40f, 38f).Sword(60f)
            .At(BehindWait, Ease.SineIn).Lean(34f).Near(16f).Nod(-12f)
            .At(BehindCut, Ease.PolyIn, 2).Lean(-16f).Nod(-12f).Near(176f).Sword(-112f).Legs(-12f, 20f)
            .At(BehindCut + 8f, Ease.SineOut).Near(198f).Sword(-146f).Lean(-20f)
            .At(BehindTicks, Ease.SineInOut).Near(170f).Sword(-150f).Lean(-10f);

        // ============================================================
        // TURNING THINGS ASIDE
        // ============================================================

        /// <summary>A shot struck out of the air: the sword up from behind him across his front, there by <see cref="GuardThere"/>.</summary>
        public const int GuardThere = 3;
        public const int GuardHeld = 11;
        public const int GuardTicks = 26;

        public static readonly FoighTrack Guard = From()
            .At(GuardThere, Ease.PolyOut, 3).Near(112f).Sword(306f).Lean(-3f).Nod(-4f).Legs(-10f, 8f).Clean(false)
            .At(GuardHeld).Near(106f).Sword(298f)
            .At(GuardTicks, Ease.SineInOut).Near(-13.2f).Sword(Dragged).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);

        /// <summary>A second shot while the first is still being struck: the blade a little further over, or a little back, by turns.</summary>
        public static FoighRig.Pose GuardAgain(FoighRig.Pose pose, int count, float since)
        {
            float flick = (count % 2 == 0 ? -1f : 1f) * (float)Math.Exp(-since / 3f);

            pose.Sword += MathHelper.ToRadians(34f) * flick;
            pose.NearArm += MathHelper.ToRadians(16f) * flick;

            return pose;
        }

        /// <summary>A swing met: the sword over and into it, and his weight after it.</summary>
        public const int ClashThere = 3;
        public const int ClashHeld = 12;
        public const int ClashTicks = 30;

        public static readonly FoighTrack Clash = From()
            .At(ClashThere, Ease.PolyOut, 3).Near(84f).Sword(336f).Lean(16f).Nod(-6f).Legs(18f, -20f).Clean(true)
            .At(ClashHeld, Ease.SineOut).Near(92f).Sword(352f).Lean(22f)
            .At(ClashTicks, Ease.SineInOut).Near(-13.2f).Sword(Dragged).Lean(7f).Nod(5f).Legs(3f, 0f);

        // ============================================================
        // THE ONE WHO DRANK
        // ============================================================

        /// <summary>There in front of them: the bottom hand out for the throat. It closes at <see cref="GrabCloses"/>.</summary>
        public const int GrabCloses = 10;
        public const int GrabTicks = 16;

        public static readonly FoighTrack Grab = new FoighTrack().At(0f)
            .Lean(20f).Nod(-6f).Near(-30f).Far(30f).Legs(-30f, 30f)
            .Hand(1).Sword(Dragged).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .At(GrabCloses, Ease.PolyIn, 2).Far(94f).Lean(12f).Legs(20f, -24f)
            .At(GrabTicks, Ease.SineOut).Far(90f).Lean(8f);

        /// <summary>Nothing in his hand: he comes up empty and stands open for it.</summary>
        public const int MissTicks = 40;

        public static readonly FoighTrack Miss = new FoighTrack(Grab.Sample(GrabTicks))
            .At(8f, Ease.SineOut).Far(110f).Lean(26f).Legs(30f, -30f)
            .At(MissTicks, Ease.SineInOut).Far(-4.4f).Near(-13.2f).Lean(7f).Nod(5f).Legs(3f, 0f);

        /// <summary>Held: up off their feet, a look, and thrown up over him.</summary>
        public const int LiftUp = 18;
        public const int LiftLooks = 30;
        public const int LiftThrows = 36;
        public const int LiftTicks = 40;

        public static readonly FoighTrack Lift = new FoighTrack(Grab.Sample(GrabTicks))
            .At(LiftUp, Ease.SineInOut).Far(136f).Lean(-6f).Nod(-14f).Legs(-10f, 10f)
            .At(LiftLooks).Far(132f).Lean(-8f)
            .At(LiftThrows - 2f, Ease.SineIn).Far(96f).Lean(6f).Legs(-20f, 20f)
            .At(LiftThrows + 2f, Ease.PolyIn, 2).Far(192f).Lean(-14f).Nod(-24f).Legs(-4f, 6f)
            .At(LiftTicks, Ease.SineOut).Far(170f);

        /// <summary>The sword up under them, point at the sky, and held there with them on it.</summary>
        public const int SpitUp = 16;

        public static readonly FoighTrack Spit = new FoighTrack(Lift.Sample(LiftTicks))
            .At(SpitUp - 4f, Ease.SineIn).Near(-150f).Far(60f).Sword(236f).Lean(-6f).Nod(-26f).Legs(-14f, 12f)
            .At(SpitUp, Ease.PolyIn, 2).Near(-180f).Far(20f).Sword(270f).Lean(-2f).Nod(-28f);

        /// <summary>Holding them up on it: the weight of them.</summary>
        public static FoighRig.Pose Spitted(float tick)
        {
            FoighRig.Pose pose = Spit.Sample(SpitUp);
            float sag = 1f - (float)Math.Exp(-tick / 6f);
            float tremble = (float)Math.Sin(tick * 1.9f) * 0.012f;

            pose.NearArm += MathHelper.ToRadians(7f) * sag + tremble;
            pose.Sword += MathHelper.ToRadians(-2f) * sag + tremble;
            pose.Lean += MathHelper.ToRadians(4f) * sag;
            pose.NearLeg += MathHelper.ToRadians(-6f) * sag;
            pose.FarLeg += MathHelper.ToRadians(6f) * sag;

            return pose;
        }

        /// <summary>How long he holds them there.</summary>
        public const int SpittedTicks = 30;

        /// <summary>The swing of the arm that throws them off it: they leave the blade at <see cref="FlingLets"/>.</summary>
        public const int FlingBack = 6;
        public const int FlingLets = 10;
        public const int FlingDown = 13;
        public const int FlingTicks = 40;

        public static readonly FoighTrack Fling = new FoighTrack(Spitted(SpittedTicks))
            .At(FlingBack, Ease.SineOut).Near(-162f).Sword(250f).Lean(-12f)
            .At(FlingDown, Ease.PolyIn, 2).Near(-296f).Sword(386f).Lean(28f).Nod(8f).Legs(22f, -26f).Far(-30f)
            .At(22f, Ease.SineOut).Near(-300f).Lean(22f)
            .At(30f, Ease.SineIn).Near(-240f).Sword(300f).Lean(4f).Clean(false)
            .At(FlingTicks, Ease.SineOut).Near(-13.2f).Far(-4.4f).Sword(Dragged).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);

        // ============================================================
        // PARRIED, AND STAGGERED
        // ============================================================

        /// <summary>His sword turned aside: thrown up and back, and him after it.</summary>
        public const int ReelTicks = 44;

        public static FoighTrack Reel(FoighRig.Pose from)
        {
            float sword = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(214f), from.Sword));
            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(196f), from.NearArm));

            from.Hand = from.Hand == 0 ? 0 : 1;
            from.Air = false;
            from.Turn = 1f;
            from.Reach = 1f;
            from.Behind = false;

            float rest = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(Dragged), MathHelper.ToRadians(sword)));
            float hang = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(-13.2f), MathHelper.ToRadians(arm)));

            return new FoighTrack(from)
                .At(5f, Ease.PolyOut, 3).Sword(sword).Near(arm).Lean(-24f).Nod(-18f).Legs(-22f, 26f).Far(40f)
                .At(20f, Ease.SineInOut).Sword(sword - 6f).Lean(-14f).Legs(-12f, 14f)
                .At(ReelTicks, Ease.SineInOut).Sword(rest).Near(hang).Far(-4.4f).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);
        }

        /// <summary>Staggered: down on a knee behind the sword, his head hanging, for as long as it lasts.</summary>
        public static FoighRig.Pose Kneeling(float tick)
        {
            FoighRig.Pose pose = Drag;
            float down = Easing.Smoother(MathHelper.Clamp(tick / 14f, 0f, 1f));
            float breath = (float)Math.Sin(tick * 0.09f) * 0.03f;

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(-14f, 34f, down)) + breath;
            pose.Nod = MathHelper.ToRadians(MathHelper.Lerp(-10f, 30f, down));
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(-12f, -68f, down));
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(14f, 58f, down));
            pose.NearArm = MathHelper.ToRadians(MathHelper.Lerp(196f, 66f, down));
            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(40f, 24f, down));
            pose.Sword = MathHelper.ToRadians(MathHelper.Lerp(214f, 420f, down));
            pose.CleanClockwise = true;

            return pose;
        }

        /// <summary>
        /// At the end of it: down on a knee where he stands, slowly, the sword still behind
        /// him where it dragged and his hand still on it, his head down. His breath goes out
        /// of him as it goes on.
        /// </summary>
        public static FoighRig.Pose Fallen(float tick)
        {
            FoighRig.Pose pose = Drag;
            float down = Easing.Smoother(MathHelper.Clamp(tick / 30f, 0f, 1f));
            float low = Easing.Smoother(MathHelper.Clamp((tick - 30f) / 70f, 0f, 1f));
            float breath = (float)Math.Sin(tick * 0.07f) * 0.03f * (1f - low);

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(7f, 26f, down) + 8f * low) + breath;
            pose.Nod = MathHelper.ToRadians(MathHelper.Lerp(5f, 24f, down) + 14f * low);
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(3f, -68f, down));
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(0f, 58f, down));
            pose.NearArm = MathHelper.ToRadians(MathHelper.Lerp(-13.2f, -34f, down));
            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(-4.4f, 22f, down) - 10f * low);

            return pose;
        }

        // ============================================================
        // HOW FAR EACH CARRIES HIM
        // ============================================================

        /// <summary>A step of a movement: from this tick to that one, this far forward (back is negative).</summary>
        public readonly struct Step
        {
            public readonly float From;
            public readonly float To;
            public readonly float Pixels;

            public Step(float from, float to, float pixels)
            {
                From = from;
                To = to;
                Pixels = pixels;
            }
        }

        public static readonly Step[] OneSteps =
        {
            new Step(OneHold, OneDown, 26f),
            new Step(OneRest, OneUp, 18f),
            new Step(OneChambered, OneThrust + 2f, 78f),
        };

        public static readonly Step[] ThreeSteps =
        {
            new Step(ThreeHold, ThreeDown, 30f),
        };

        public static readonly Step[] ClashSteps =
        {
            new Step(0f, ClashHeld, 8f),
        };

        public static readonly Step[] BehindSteps =
        {
            new Step(BehindWait, BehindCut, 14f),
        };

        /// <summary>
        /// How far forward of where he began these steps have carried him by this tick: each
        /// slow off the mark and slow to stop.
        /// </summary>
        public static float Advance(Step[] steps, float tick)
        {
            float far = 0f;

            foreach (Step step in steps)
            {
                if (tick <= step.From)
                    continue;

                float t = step.To <= step.From ? 1f : (tick - step.From) / (step.To - step.From);

                far += step.Pixels * Easing.Smoother(t);
            }

            return far;
        }
    }
}
