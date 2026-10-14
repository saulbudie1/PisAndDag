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

        /// <summary>
        /// In his third phase it is not stuck (Foigh.Wild): he comes up over it, both
        /// hands on it (<see cref="DriveSets"/>), and drives it deeper
        /// (<see cref="DriveBursts"/>, where the first of the cuts out of the ground is
        /// shown; fourteen in all, the rest within forty ticks of it at uneven ticks, each
        /// open twelve to twenty-two ticks after it is shown, for three), and stays down
        /// over it until he tears it out: five ticks after the last has done cutting, by
        /// <see cref="DriveTicks"/> (as late as that can be) at the latest.
        /// </summary>
        public const int DriveSets = 12;
        public const int DriveBursts = 24;
        public const int DriveTicks = 92;

        public static FoighRig.Pose Driving(float tick, float aim)
        {
            FoighRig.Pose pose = Stuck(aim);

            float up = tick <= DriveSets ? Easing.Smoother(MathHelper.Clamp(tick / DriveSets, 0f, 1f))
                : 1f - Easing.Apply(Ease.PolyIn, MathHelper.Clamp((tick - DriveSets) / (DriveBursts - DriveSets - 4f), 0f, 1f), 3);
            float down = Easing.Apply(Ease.PolyIn, MathHelper.Clamp((tick - DriveSets) / (DriveBursts - DriveSets), 0f, 1f), 3);

            // Up off it, the far hand come to the handle with the near one; and down with
            // his weight on both. (Down is his legs going wide under him, as far as they do
            // when he is on a knee: his hips are never lower than his feet let them be.)
            pose.Lean += MathHelper.ToRadians(-18f * up + 20f * down);
            pose.Nod += MathHelper.ToRadians(-14f * up + 12f * down);
            pose.NearArm += MathHelper.ToRadians(14f * up - 16f * down);
            pose.FarArm += MathHelper.ToRadians(36f * Math.Max(up, down));
            pose.NearLeg += MathHelper.ToRadians(8f * up - 36f * down);
            pose.FarLeg += MathHelper.ToRadians(-8f * up + 30f * down);
            pose.Rise = 5f * up;

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

        /// <summary>
        /// The run at them behind the sword (Foigh.Hold): it is held up across his front
        /// as it is to strike a shot out of the air, and he runs under it, leaning in.
        /// <paramref name="gait"/> as the run's. No longer than <see cref="ChargeLongest"/>.
        /// </summary>
        public const int ChargeLongest = 180;

        public static FoighRig.Pose Charging(float gait)
        {
            FoighRig.Pose pose = Guard.Sample(GuardHeld);

            float s = (float)Math.Sin(gait * MathHelper.Pi);

            pose.NearLeg = FoighRig.NearLegRest + 0.85f * s;
            pose.FarLeg = FoighRig.FarLegRest - 0.85f * s;
            pose.Lean = MathHelper.ToRadians(18f);
            pose.Nod = MathHelper.ToRadians(-10f);
            pose.FarArm = 0.7f * s + 0.2f;
            pose.NearArm += MathHelper.ToRadians(4f) * s;
            pose.Sword += MathHelper.ToRadians(3f) * s;

            return pose;
        }

        /// <summary>His posture worn through (Foigh.Hold): down on a knee with the sword down (<see cref="Kneeling"/>) for this long, by request.</summary>
        public const int BrokenTicks = 180;

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
        // HIS SECOND PHASE'S OWN: the sword swung up and the run at them with it; the blow
        // down and the cut up; down on a knee, and the leap forward and up with the bottom
        // hand out; and whoever is in the hand pulled in against him, the sword pushed
        // slowly through them, and held up on it
        // ============================================================

        /// <summary>The sword swung up from where it drags, over his head, as he sets off.</summary>
        public const int RaiseTicks = 16;

        public static readonly FoighTrack Raise = From()
            .At(8f, Ease.SineIn).Lean(0f).Near(118f).Sword(238f).Legs(-12f, 10f).Nod(0f)
            .At(RaiseTicks, Ease.SineOut).Lean(12f).Nod(-6f).Near(170f).Sword(252f).Legs(-20f, 16f);

        /// <summary>
        /// The run at them with the sword up behind his head: <paramref name="gait"/> as
        /// the run's (a step being <see cref="RunStride"/> long).
        /// </summary>
        public static FoighRig.Pose Rushing(float gait)
        {
            FoighRig.Pose pose = Raise.Sample(RaiseTicks);

            float s = (float)Math.Sin(gait * MathHelper.Pi);

            pose.NearLeg = FoighRig.NearLegRest + 0.85f * s;
            pose.FarLeg = FoighRig.FarLegRest - 0.85f * s;
            pose.Lean = MathHelper.ToRadians(22f);
            pose.Nod = MathHelper.ToRadians(-9f);
            pose.FarArm = 0.9f * s + 0.2f;
            pose.NearArm = MathHelper.ToRadians(168f) - 0.05f * s;
            pose.Sword = MathHelper.ToRadians(250f) + 0.04f * s;

            return pose;
        }

        /// <summary>
        /// The blow down and the cut up: reared back over it, down hard into the ground
        /// ahead of him, and up again through whatever is there, the sword going on over
        /// his shoulder.
        /// </summary>
        public const int HewHold = 6;
        public const int HewDown = 13;
        public const int HewRest = 23;
        public const int HewUp = 30;
        public const int HewOver = 38;
        public const int HewTicks = 42;

        public static readonly FoighTrack Hew = new FoighTrack(Raise.Sample(RaiseTicks))
            .At(HewHold, Ease.SineOut).Lean(-16f).Nod(-10f).Near(178f).Sword(248f).Legs(-24f, 18f)
            .At(HewDown, Ease.PolyIn, 3).Lean(30f).Nod(12f).Near(58f).Sword(390f).Legs(24f, -28f)
            .At(HewRest, Ease.SineOut).Lean(22f).Near(52f).Sword(390f).Clean(false)
            .At(HewUp, Ease.PolyIn, 2).Lean(-14f).Nod(-10f).Near(172f).Sword(244f).Legs(-18f, 20f)
            .At(HewOver, Ease.SineOut).Near(198f).Sword(212f).Lean(-18f)
            .At(HewTicks).Near(200f).Sword(210f);

        /// <summary>
        /// Down on a knee, the sword let down behind him and the bottom hand drawn back by
        /// his hip. The hand that follows wears the star from here.
        /// </summary>
        public const int CrouchTicks = 18;

        public static readonly FoighTrack Crouch = new FoighTrack(Hew.Sample(HewTicks))
            .At(9f, Ease.SineOut).Lean(26f).Nod(-10f).Legs(-60f, 52f).Near(292f).Sword(190f).Far(-44f)
            .At(CrouchTicks, Ease.SineInOut).Lean(34f).Nod(-18f).Legs(-66f, 56f).Near(300f).Sword(186f).Far(-70f);

        /// <summary>
        /// The leap at its furthest, by request: ten tiles forward and five up, in half a
        /// second. Nearer than that it is the same leap, smaller (Foigh.Spring).
        /// </summary>
        public const float SpringFar = 160f;
        public const float SpringHigh = 80f;
        public const int SpringTicks = 30;

        /// <summary>
        /// In the air, the bottom hand thrown out ahead of him: <paramref name="share"/> is
        /// how far through the leap he is, 0 as he leaves the ground and 1 as he comes down.
        /// </summary>
        public static FoighRig.Pose Springing(float share)
        {
            FoighRig.Pose pose = Crouch.Sample(CrouchTicks);
            pose.Air = true;
            pose.Rise = 0f;

            float t = MathHelper.Clamp(share, 0f, 1f);
            float thrown = Easing.Apply(Ease.PolyOut, MathHelper.Clamp(t / 0.22f, 0f, 1f), 2);
            float down = Easing.Smoother(MathHelper.Clamp((t - 0.6f) / 0.4f, 0f, 1f));

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(34f, 48f, thrown) - 26f * down);
            pose.Nod = MathHelper.ToRadians(-18f - 12f * thrown + 16f * down);
            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(-70f, 116f, thrown) - 18f * down);
            pose.NearArm = MathHelper.ToRadians(MathHelper.Lerp(300f, 284f, thrown));
            pose.Sword = MathHelper.ToRadians(MathHelper.Lerp(186f, 200f, thrown));
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(-66f, -34f, thrown) + 62f * down);
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(56f, -52f, thrown) + 66f * down);

            return pose;
        }

        /// <summary>
        /// In the air with somebody in the hand, held out at arm's length, his legs coming
        /// under him: <paramref name="fall"/> is 0 while he still rises and 1 falling fast.
        /// </summary>
        public static FoighRig.Pose Catching(float fall)
        {
            FoighRig.Pose pose = Springing(0.7f);
            float t = MathHelper.Clamp(fall, 0f, 1f);

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(26f, 10f, t));
            pose.Nod = MathHelper.ToRadians(MathHelper.Lerp(-12f, 2f, t));
            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(108f, 100f, t));
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(-10f, 22f, t));
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(-30f, 12f, t));

            return pose;
        }

        /// <summary>Where whoever is in his bottom hand hangs from it, facing right: their middle, from his fist at their throat.</summary>
        public static readonly Vector2 HeldBy = new Vector2(2f, 15f);

        /// <summary>Ticks whoever the hand closes on is pulled in to it over, from where they were: taken hold of, not put there.</summary>
        public const int SnatchTicks = 5;

        /// <summary>
        /// Down, with them in his hand. Pulled in against him as he turns his sword side
        /// away from them and draws the sword back over his shoulder to point at them (it
        /// is seen shorter: it points away from the eye; <see cref="PierceDraws"/>). Then,
        /// slowly, pushed through them, forward and a little up, as he turns back into it
        /// (<see cref="PierceTicks"/>): its guard is against them by the end.
        /// </summary>
        public const int PierceLands = 5;
        public const int PierceDraws = 18;
        public const int PierceTicks = 78;

        /// <summary>The tick its point is in them: the one blow of it.</summary>
        public const int PierceBites = PierceDraws + 4;

        /// <summary>Drawn back: how much of the sword is seen, how far round he is turned, and where the blade points (up from level, ahead of him).</summary>
        public const float PierceReach = 0.42f;
        public const float PierceTurn = 0.74f;
        public const float PierceBlade = -23f;

        /// <summary>From the pose he comes down in.</summary>
        public static FoighTrack Pierce(FoighRig.Pose from)
        {
            from.Behind = false;
            from.UnderHead = false;
            from.Pass = 0f;
            from.Hand = 1;

            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(-36f), from.NearArm));
            float sword = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(PierceBlade), from.Sword));
            float hand = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(128f), from.FarArm));

            return new FoighTrack(from)
                .At(0.01f).Air(false)
                .At(PierceLands, Ease.SineOut).Lean(22f).Nod(4f).Legs(-44f, 40f).Far(hand - 24f)
                .At(PierceDraws, Ease.SineInOut).Lean(-4f).Nod(10f).Legs(-16f, 14f).Far(hand).Near(arm).Sword(sword).Reach(PierceReach).Turn(PierceTurn).Clean(true)
                .At(PierceTicks, Ease.SineInOut).Lean(10f).Nod(8f).Legs(8f, -12f).Far(hand + 22f).Near(arm + 44f).Sword(sword - 1f).Reach(1f).Turn(1f);
        }

        /// <summary>
        /// Held up on it: the sword raised until it points up and ahead of him, by request,
        /// with them on it; his other hand lets go of them. And held there, their weight on
        /// his arm.
        /// </summary>
        public const int HoistUp = 16;
        public const int HoistTicks = 46;
        public const float HoistBlade = -50f;

        /// <summary>Where on the blade they are while it has them, from its guard to its point.</summary>
        public const float HoistAlong = 0.2f;

        /// <summary>From the pose the sword went through them in.</summary>
        public static FoighTrack Hoist(FoighRig.Pose from)
        {
            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(56f), from.NearArm));
            float sword = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(HoistBlade), from.Sword));

            return new FoighTrack(from)
                .At(HoistUp, Ease.SineInOut).Near(arm).Sword(sword).Lean(-8f).Nod(-14f).Far(34f).Legs(-18f, 16f)
                .At(HoistTicks).Lean(-6f);
        }

        /// <summary>Holding them up on it, at this tick of it: the weight of them.</summary>
        public static FoighRig.Pose Hoisted(FoighTrack hoist, float tick)
        {
            FoighRig.Pose pose = hoist.Sample(tick);

            float held = MathHelper.Clamp((tick - HoistUp) / 6f, 0f, 1f);
            float sag = held * (1f - (float)Math.Exp(-(tick - HoistUp) / 8f));
            float tremble = held * (float)Math.Sin(tick * 1.9f) * 0.012f;

            pose.NearArm += MathHelper.ToRadians(-5f) * sag + tremble;
            pose.Sword += MathHelper.ToRadians(3f) * sag + tremble;
            pose.NearLeg += MathHelper.ToRadians(-5f) * sag;
            pose.FarLeg += MathHelper.ToRadians(5f) * sag;

            return pose;
        }

        /// <summary>
        /// Flung off it from there, with the same swing of the arm as the one who drank
        /// (<see cref="Fling"/>, its ticks): back over his shoulder, and over and down ahead
        /// of him; then the sword down behind him again.
        /// </summary>
        public static FoighTrack FlingOff(FoighRig.Pose from)
        {
            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(150f), from.NearArm));
            float sword = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(250f), from.Sword));

            return new FoighTrack(from)
                .At(FlingBack, Ease.SineOut).Near(arm).Sword(sword).Lean(-14f)
                .At(FlingDown, Ease.PolyIn, 2).Near(arm - 86f).Sword(sword + 136f).Lean(28f).Nod(8f).Legs(22f, -26f).Far(-30f)
                .At(22f, Ease.SineOut).Near(arm - 90f).Lean(22f)
                .At(30f, Ease.SineIn).Near(arm - 30f).Sword(sword + 50f).Lean(4f).Clean(false)
                .At(FlingTicks, Ease.SineOut).Near(arm + 196.8f).Far(-4.4f).Sword(sword - 100f).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);
        }

        /// <summary>
        /// Down with nothing in his hand: onto a knee where he lands, the hand still out;
        /// a look at it; and up, open for it (as long as the other miss, <see cref="MissTicks"/>).
        /// </summary>
        public static FoighTrack Empty(FoighRig.Pose from)
        {
            from.Turn = 1f;
            from.Reach = 1f;
            from.Slide = 0f;
            from.Behind = false;

            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(-13.2f), from.NearArm));
            float sword = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(Dragged), from.Sword));

            return new FoighTrack(from)
                .At(0.01f).Air(false)
                .At(5f, Ease.SineOut).Lean(36f).Nod(8f).Legs(-58f, 52f).Far(96f)
                .At(16f, Ease.SineInOut).Lean(28f).Nod(14f).Far(62f).Legs(-46f, 42f)
                .At(MissTicks, Ease.SineInOut).Far(-4.4f).Near(arm).Sword(sword).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);
        }

        // ============================================================
        // HIS SECOND PHASE'S OWN, AGAIN: the cut that comes late; the mill; the cut up out
        // of the ground; and in the air
        // ============================================================

        // ---- the late cut ----

        /// <summary>
        /// Sunk low, the sword drawn back level by his hip, his other hand toward its
        /// handle: he looks at them along the line he will take. A light runs up the blade
        /// from <see cref="PoiseGleams"/>.
        /// </summary>
        public const int PoiseGleams = 6;
        public const int PoiseTicks = 26;

        public static readonly FoighTrack Poise = From()
            .At(10f, Ease.SineOut).Lean(20f).Nod(-4f).Legs(-38f, 36f).Near(-52f).Sword(172f).Far(-30f)
            .At(PoiseTicks, Ease.SineInOut).Lean(27f).Nod(-12f).Legs(-46f, 42f).Near(-62f).Sword(177f).Far(-46f);

        /// <summary>
        /// Past them, at the far end of the line, as the cut left him: deep on his front
        /// leg, the sword out ahead of him and still. He does not move until the line
        /// behind him has opened (<see cref="WakeCuts"/>); then a flick of the blade where
        /// it is (<see cref="WakeFlick"/>), and he comes up, looking back over his
        /// shoulder, with the sword going up over him and down behind.
        /// </summary>
        public const int WakeCuts = 20;
        public const int WakeFlick = 27;
        public const int WakeRises = 31;
        public const int WakeTicks = 66;

        public static readonly FoighTrack Wake = new FoighTrack().At(0f)
            .Lean(31f).Nod(-8f).Near(97f).Far(-52f).Legs(40f, -46f)
            .Hand(1).Sword(-8f).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .At(WakeCuts, Ease.SineOut).Lean(28f).Near(94f).Sword(-6f)
            .At(WakeFlick, Ease.SineInOut).Near(98f).Sword(-12f)
            .At(WakeFlick + 3f, Ease.PolyIn, 2).Near(82f).Sword(14f).Lean(30f)
            .At(WakeRises, Ease.SineOut).Near(84f).Sword(12f)
            .At(WakeRises + 13f, Ease.SineInOut).Lean(9f).Nod(-2f).Near(126f).Far(-18f).Sword(-64f).Legs(18f, -20f).HeadTurn(-1f).Clean(false)
            .At(WakeTicks, Ease.SineInOut).Near(-13.2f + 360f).Far(-4.4f).Sword(Dragged - 360f).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);

        // ---- the mill ----

        /// <summary>
        /// The sword brought up over his head and down until it is level and pointing at
        /// them, at arm's length: there by <see cref="LevelThere"/>, and held.
        /// </summary>
        public const int LevelThere = 14;
        public const int LevelTicks = 22;

        public static readonly FoighTrack Level = From()
            .At(8f, Ease.SineIn).Lean(-6f).Nod(-6f).Near(158f).Sword(250f).Legs(-14f, 12f)
            .At(LevelThere, Ease.SineOut).Lean(8f).Nod(-2f).Near(90f).Sword(366f).Legs(-20f, 18f).Far(-30f)
            .At(LevelTicks).Lean(10f).Near(88f).Sword(366f).Far(-40f);

        /// <summary>One turn of the mill takes this long; he goes round as in his other turns (<see cref="Round"/>), from the sword levelled at them.</summary>
        public const int MillTurn = 16;

        /// <summary>
        /// Out of the last turn: up off the ground with the sword over his head in both
        /// hands (<see cref="MillEndUp"/>), and down with it as he lands
        /// (<see cref="MillEndDown"/>); then the sword up over him again and down behind.
        /// </summary>
        public const int MillEndUp = 9;
        public const int MillEndDown = 19;
        public const int MillEndTicks = 50;

        public static readonly FoighTrack MillEnd = new FoighTrack(Round(Level.Sample(LevelTicks), 1f, false))
            .At(0.01f).Air(true)
            .At(MillEndUp, Ease.SineOut).Lean(-14f).Nod(-10f).Near(178f).Far(150f).Sword(-108f).Legs(-34f, 38f)
            .At(MillEndDown, Ease.PolyIn, 3).Air(false).Lean(32f).Nod(12f).Near(58f).Far(44f).Sword(32f).Legs(24f, -30f)
            .At(MillEndDown + 9f, Ease.SineOut).Lean(24f).Near(52f).Sword(32f)
            .At(MillEndDown + 19f, Ease.SineIn).Near(120f).Far(10f).Sword(-60f).Lean(4f).Legs(8f, -8f).Clean(false)
            .At(MillEndTicks, Ease.SineOut).Near(-13.2f + 360f).Far(-4.4f).Sword(Dragged - 360f).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);

        // ---- the cut up out of the ground ----

        /// <summary>
        /// The run at them with the sword dragged: bent into it, the blade laid along the
        /// ground behind him (<paramref name="gait"/> as the run's).
        /// </summary>
        public static FoighRig.Pose Scraping(float gait)
        {
            FoighRig.Pose pose = Run(gait, true);

            float s = (float)Math.Sin(gait * MathHelper.Pi);

            pose.Lean = MathHelper.ToRadians(27f);
            pose.NearArm = MathHelper.ToRadians(-34f) - 0.06f * s;
            pose.Sword = MathHelper.ToRadians(Dragged) + 0.02f * s;

            return pose;
        }

        /// <summary>
        /// The cut up: he gathers himself low over the blade (<see cref="UpcutSets"/>),
        /// tears it forward along the ground and up through whatever is ahead of him
        /// (<see cref="UpcutCuts"/>), and it goes on over his shoulder
        /// (<see cref="UpcutOver"/>), him up on his toes after it.
        /// </summary>
        public const int UpcutSets = 7;
        public const int UpcutCuts = 13;
        public const int UpcutOver = 20;
        public const int UpcutTicks = 46;

        /// <summary>From the pose he ran in.</summary>
        public static FoighTrack Upcut(FoighRig.Pose from)
        {
            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(-20f), from.NearArm));
            float sword = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(132f), from.Sword));

            return new FoighTrack(from)
                .At(UpcutSets, Ease.SineOut).Lean(34f).Nod(-10f).Near(arm).Sword(sword).Legs(-40f, 38f).Far(30f)
                .At(UpcutCuts, Ease.PolyIn, 2).Lean(-10f).Nod(-12f).Near(arm + 170f).Sword(sword - 190f).Legs(20f, -16f).Far(-20f).Clean(false)
                .At(UpcutOver, Ease.SineOut).Lean(-18f).Near(arm + 210f).Sword(sword - 244f).Legs(8f, -4f)
                .At(UpcutTicks, Ease.SineInOut).Near(arm + 366.8f).Far(-4.4f).Sword(sword - 342f).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);
        }

        // ---- in the air ----

        /// <summary>
        /// Beside them in the air, with no weight: the sword going up over his head in
        /// both hands and his knees coming up under him. <paramref name="share"/> is how
        /// far through the moment he hangs there he is, 0 as he arrives and 1 as the blow
        /// begins.
        /// </summary>
        public static FoighRig.Pose Hanging(float share)
        {
            FoighRig.Pose pose = Drag;
            pose.Air = true;
            pose.Rise = 0f;

            float t = Easing.Smoother(MathHelper.Clamp(share, 0f, 1f));

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(8f, -16f, t));
            pose.Nod = MathHelper.ToRadians(MathHelper.Lerp(0f, -12f, t));
            pose.NearArm = MathHelper.ToRadians(MathHelper.Lerp(118f, 178f, t));
            pose.FarArm = MathHelper.ToRadians(MathHelper.Lerp(50f, 152f, t));
            pose.Sword = MathHelper.ToRadians(MathHelper.Lerp(-66f, -112f, t));
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(-8f, -36f, t));
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(18f, 46f, t));
            pose.CleanClockwise = true;

            return pose;
        }

        /// <summary>
        /// The blow down, in the air: a last reach back (<see cref="SmiteCuts"/>), through
        /// and down ahead of him (<see cref="SmiteDown"/>), and on under him and up behind,
        /// the whole way round. From <see cref="SmiteDown"/> he has his weight again.
        /// </summary>
        public const int SmiteCuts = 3;
        public const int SmiteDown = 8;
        public const int SmiteTicks = 17;

        public static readonly FoighTrack Smite = new FoighTrack(Hanging(1f))
            .At(SmiteCuts, Ease.SineOut).Lean(-21f).Near(184f).Sword(-117f)
            .At(SmiteDown, Ease.PolyIn, 3).Lean(38f).Nod(14f).Near(50f).Far(34f).Sword(58f).Legs(30f, -36f)
            .At(SmiteTicks, Ease.SineOut).Lean(22f).Nod(8f).Near(-44f).Far(10f).Sword(198f).Legs(10f, -8f);

        /// <summary>Falling after it, the sword up behind him: <paramref name="fall"/> is 0 as he begins to fall and 1 falling fast.</summary>
        public static FoighRig.Pose Dropping(float fall)
        {
            FoighRig.Pose pose = Smite.Sample(SmiteTicks);
            float t = MathHelper.Clamp(fall, 0f, 1f);

            pose.Lean = MathHelper.ToRadians(MathHelper.Lerp(22f, 12f, t));
            pose.Nod = MathHelper.ToRadians(MathHelper.Lerp(8f, 4f, t));
            pose.NearLeg = MathHelper.ToRadians(MathHelper.Lerp(10f, -24f, t));
            pose.FarLeg = MathHelper.ToRadians(MathHelper.Lerp(-8f, 30f, t));
            pose.Sword = MathHelper.ToRadians(MathHelper.Lerp(198f, 188f, t));

            return pose;
        }

        /// <summary>Down: into his knees, and up into the stance he fights from.</summary>
        public const int AlightTicks = 24;

        /// <summary>From the pose he came down in.</summary>
        public static FoighTrack Alight(FoighRig.Pose from)
        {
            float sword = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(Dragged), from.Sword));
            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(-13.2f), from.NearArm));

            return new FoighTrack(from)
                .At(0.01f).Air(false)
                .At(5f, Ease.SineOut).Lean(30f).Nod(12f).Legs(-52f, 50f).Near(40f)
                .At(AlightTicks, Ease.SineInOut).Near(arm).Far(-4.4f).Sword(sword).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);
        }

        // ============================================================
        // HIS THIRD PHASE'S OWN: three cuts where he stands, each let go of and gone on
        // through the air (FoighSlash)
        // ============================================================

        /// <summary>
        /// The sword up over his head and held (<see cref="RendHold"/>); down
        /// (<see cref="RendDown"/>), and it rests where it came down. He sinks over it
        /// (<see cref="RendSinks"/>), holds that (<see cref="RendCoiled"/>), and tears it
        /// up (<see cref="RendUp"/>); it goes on over his shoulder
        /// (<see cref="RendOver"/>). Up over his head in both hands
        /// (<see cref="RendHigh"/>), held (<see cref="RendHeld"/>), and down with all of
        /// him (<see cref="RendSmash"/>), where it stays a while (<see cref="RendStays"/>).
        /// </summary>
        public const int RendWind = 16;
        public const int RendHold = 24;
        public const int RendDown = 31;
        public const int RendRest = 40;
        public const int RendSinks = 64;
        public const int RendCoiled = 71;
        public const int RendUp = 77;
        public const int RendOver = 85;
        public const int RendHigh = 107;
        public const int RendHeld = 115;
        public const int RendSmash = 122;
        public const int RendStays = 140;
        public const int RendTicks = 160;

        /// <summary>
        /// The ticks the three cuts are let go of at: the first as the blade comes down
        /// through level, the second as it comes up through it, the third as it lands.
        /// Three quarters of a second apart, near enough: a parry that met one is over
        /// before the next (ParryTiming), and one who jumped the one before in good time
        /// is down again.
        /// </summary>
        public static readonly int[] RendCasts = { RendDown - 1, RendUp - 2, RendSmash };

        /// <summary>Before each of the three he looks for them again: at these ticks, the sword not in a cut.</summary>
        public static readonly int[] RendLooks = { 10, RendRest + 4, RendOver + 4 };

        /// <summary>The blade flares before the first of them: at this tick.</summary>
        public const int RendFlares = RendWind - 4;

        public static readonly FoighTrack Rend = From()
            .At(8f, Ease.SineIn).Lean(-2f).Near(118f).Sword(238f).Legs(-12f, 10f).Nod(0f)
            .At(RendWind, Ease.SineOut).Lean(-12f).Nod(-8f).Near(172f).Sword(256f).Legs(-22f, 16f)
            .At(RendHold).Near(176f).Sword(250f).Lean(-14f)
            .At(RendDown, Ease.PolyIn, 3).Lean(24f).Nod(10f).Near(60f).Sword(386f).Legs(20f, -24f)
            .At(RendRest, Ease.SineOut).Lean(19f).Near(54f).Sword(386f).Clean(false)
            .At(RendSinks, Ease.SineInOut).Lean(27f).Nod(2f).Near(44f).Sword(394f).Legs(28f, -30f)
            .At(RendCoiled).Lean(28f)
            .At(RendUp, Ease.PolyIn, 2).Lean(-12f).Nod(-8f).Near(168f).Sword(246f).Legs(-16f, 18f)
            .At(RendOver, Ease.SineOut).Near(196f).Sword(214f).Lean(-16f)
            .At(RendHigh, Ease.SineInOut).Lean(-14f).Nod(-10f).Near(178f).Far(150f).Sword(252f).Legs(-30f, 34f).Clean(true)
            .At(RendHeld).Near(180f).Sword(250f)
            .At(RendSmash, Ease.PolyIn, 3).Lean(32f).Nod(12f).Near(58f).Far(44f).Sword(392f).Legs(24f, -30f)
            .At(RendSmash + 9f, Ease.SineOut).Lean(24f).Near(52f).Sword(392f)
            .At(RendStays).Lean(22f)
            .At(RendStays + 8f, Ease.SineIn).Near(120f).Far(10f).Sword(300f).Lean(4f).Legs(8f, -8f).Clean(false)
            .At(RendTicks, Ease.SineOut).Near(-13.2f + 360f).Far(-4.4f).Sword(Dragged).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);

        // ============================================================
        // FROM HIS SECOND PHASE: two cuts of wind, and the hook (Foigh.Reach)
        // ============================================================

        /// <summary>
        /// The wind. The sword up over his head and held (<see cref="GaleHold"/>); down
        /// (<see cref="GaleDown"/>), and it rests where it came down. He sinks over it
        /// (<see cref="GaleSinks"/>), holds that (<see cref="GaleCoiled"/>), and tears
        /// it up (<see cref="GaleUp"/>); it goes on over his shoulder
        /// (<see cref="GaleOver"/>), and down behind him into his stance. His third
        /// phase's first two cuts, begun quicker.
        /// </summary>
        public const int GaleWind = 10;
        public const int GaleHold = 14;
        public const int GaleDown = 20;
        public const int GaleRest = 26;
        public const int GaleSinks = 52;
        public const int GaleCoiled = 60;
        public const int GaleUp = 66;
        public const int GaleOver = 74;
        public const int GaleTicks = 92;

        /// <summary>
        /// The ticks the two are let go of at: the first as the blade comes down through
        /// level, the second as it comes up through it. Three quarters of a second
        /// apart (45 ticks), as his third phase's are: a parry that met the first is over
        /// (ParryTiming: 42 ticks in all) before one has to be begun for the second.
        /// </summary>
        public static readonly int[] GaleCasts = { GaleDown - 1, GaleUp - 2 };

        /// <summary>Before each of the two he looks for them again: at these ticks, the sword not in a cut.</summary>
        public static readonly int[] GaleLooks = { 6, GaleRest + 14 };

        public static readonly FoighTrack Gale = From()
            .At(5f, Ease.SineIn).Lean(-2f).Near(118f).Sword(238f).Legs(-12f, 10f).Nod(0f)
            .At(GaleWind, Ease.SineOut).Lean(-12f).Nod(-8f).Near(172f).Sword(256f).Legs(-22f, 16f)
            .At(GaleHold).Near(176f).Sword(250f).Lean(-14f)
            .At(GaleDown, Ease.PolyIn, 3).Lean(24f).Nod(10f).Near(60f).Sword(386f).Legs(20f, -24f)
            .At(GaleRest, Ease.SineOut).Lean(19f).Near(54f).Sword(386f).Clean(false)
            .At(GaleSinks, Ease.SineInOut).Lean(27f).Nod(2f).Near(44f).Sword(394f).Legs(28f, -30f)
            .At(GaleCoiled).Lean(28f)
            .At(GaleUp, Ease.PolyIn, 2).Lean(-12f).Nod(-8f).Near(168f).Sword(246f).Legs(-16f, 18f)
            .At(GaleOver, Ease.SineOut).Near(196f).Sword(214f).Lean(-16f)
            .At(GaleTicks, Ease.SineInOut).Near(-13.2f + 360f).Far(-4.4f).Sword(Dragged).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);

        /// <summary>
        /// The hook thrown from his bottom hand: the hand back with it
        /// (<see cref="HookBack"/>), and through under him (<see cref="HookLets"/>, where
        /// it leaves); the arm out after it (<see cref="HookOut"/>), where it stays for
        /// as long as the chain is out.
        /// </summary>
        public const int HookBack = 12;
        public const int HookLets = 18;
        public const int HookOut = 26;

        public static readonly FoighTrack HookCast = From()
            .At(HookBack, Ease.SineOut).Far(-112f).Lean(-9f).Nod(-5f).Legs(-10f, 12f)
            .At(HookLets, Ease.PolyIn, 2).Far(98f).Lean(15f).Nod(4f).Legs(16f, -14f)
            .At(HookOut, Ease.SineOut).Far(86f).Lean(11f);

        /// <summary>Nothing on it: the chain run back through his hand (he has it again at <see cref="HookIn"/>), and he is in his stance.</summary>
        public const int HookIn = 16;
        public const int HookMissTicks = 36;

        public static readonly FoighTrack HookMiss = new FoighTrack(HookCast.Sample(HookOut))
            .At(HookIn, Ease.SineInOut).Far(24f).Lean(3f).Legs(6f, -4f)
            .At(HookMissTicks, Ease.SineInOut).Far(-4.4f).Lean(7f).Nod(5f).Legs(3f, 0f);

        /// <summary>Pulled along his own chain to whoever it has: bent into it, the hand with the chain out ahead of him, the sword off the ground behind.</summary>
        public static readonly FoighRig.Pose Hauled = From()
            .Lean(34f).Nod(-14f).Far(98f).Near(-42f).Sword(196f).Legs(-36f, 28f)
            .Sample(0f);

        /// <summary>
        /// Whoever it has, pulled in to him: the hand hauled back with the chain and the
        /// sword swung up (as the hand's move raises it), by <see cref="WinchPulled"/>;
        /// held so until they are near.
        /// </summary>
        public const int WinchPulled = 12;

        public static readonly FoighTrack Winch = new FoighTrack(HookCast.Sample(HookOut))
            .At(WinchPulled, Ease.SineInOut).Far(-38f).Lean(-4f).Nod(-6f).Near(170f).Sword(252f).Legs(-20f, 16f);

        /// <summary>
        /// The blow that meets whoever the hook brought to him, or whoever it brought him
        /// to: from the sword up, held a moment (<see cref="ReapHold"/>), down
        /// (<see cref="ReapDown"/>), where it rests (<see cref="ReapRest"/>); and over
        /// and back into his stance.
        /// </summary>
        public const int ReapHold = 4;
        public const int ReapDown = 10;
        public const int ReapRest = 22;
        public const int ReapTicks = 46;

        public static readonly FoighTrack Reap = new FoighTrack(Raise.Sample(RaiseTicks))
            .At(ReapHold, Ease.SineOut).Lean(-16f).Nod(-10f).Near(178f).Sword(248f).Legs(-24f, 18f)
            .At(ReapDown, Ease.PolyIn, 3).Lean(30f).Nod(12f).Near(58f).Sword(390f).Legs(24f, -28f)
            .At(ReapRest, Ease.SineOut).Lean(22f).Near(52f).Sword(390f).Clean(false)
            .At(ReapRest + 8f, Ease.SineIn).Near(120f).Far(10f).Sword(300f).Lean(4f).Legs(8f, -8f)
            .At(ReapTicks, Ease.SineOut).Near(-13.2f + 360f).Far(-4.4f).Sword(Dragged).Lean(7f).Nod(5f).Legs(3f, 0f).Clean(true);

        /// <summary>
        /// The hook taken out, in the passage into his second phase, while his third
        /// line is up: the bottom hand back behind his hip (from
        /// <see cref="HookReaches"/> to <see cref="HookReached"/>), and forward with it
        /// (from <see cref="HookTaken"/>, the tick it is in his hand, to
        /// <see cref="HookShown"/>).
        /// </summary>
        public const int HookReaches = 30;
        public const int HookReached = 44;
        public const int HookTaken = 54;
        public const int HookShown = 72;

        public static FoighRig.Pose TakingHook(FoighRig.Pose pose, float tick)
        {
            float back = tick <= HookReaches ? 0f
                : tick <= HookReached ? Easing.Smoother((tick - HookReaches) / (HookReached - HookReaches))
                : tick <= HookTaken ? 1f
                : tick <= HookShown ? 1f - Easing.Smoother((tick - HookTaken) / (HookShown - HookTaken))
                : 0f;

            pose.FarArm = MathHelper.Lerp(pose.FarArm, MathHelper.ToRadians(-64f), back);
            pose.Lean += MathHelper.ToRadians(-3f) * back;

            return pose;
        }

        // ============================================================
        // HIS THIRD PHASE'S OWN: the wheel (Foigh.Wild)
        // ============================================================

        /// <summary>
        /// Up off the ground for <see cref="WheelRises"/> ticks, slower and slower, and
        /// with no weight after; turning all the while as in the mill, a turn every
        /// <see cref="WheelTurn"/> ticks, his knees up under him. Each time the sword
        /// comes round to ahead of him (<see cref="WheelCasts"/>) a great cut is let go;
        /// half a turn after the last he falls (<see cref="WheelTicks"/>).
        /// </summary>
        public const int WheelRises = 24;
        public const int WheelTurn = 16;
        public const int WheelTicks = 56;

        public static readonly int[] WheelCasts = { WheelTurn, WheelTurn * 2, WheelTurn * 3 };

        public static FoighRig.Pose Wheeling(float tick)
        {
            float turns = Math.Max(0f, tick) / WheelTurn;

            FoighRig.Pose pose = Round(Level.Sample(LevelTicks), turns - (float)Math.Floor(turns), false);

            pose.Air = true;
            pose.Rise = 0f;
            pose.NearLeg = MathHelper.ToRadians(-34f);
            pose.FarLeg = MathHelper.ToRadians(40f);

            return pose;
        }

        // ============================================================
        // THE BALL (Foigh.Wild): what fights for them, answered
        // ============================================================

        /// <summary>
        /// Down for <see cref="CurlLeaps"/> ticks and off the ground, rising for
        /// <see cref="CurlRises"/> more and folding up as he goes: bent double over his legs,
        /// his head down to them, his arms about them and the sword held in against him,
        /// behind him; held there, shaking, a little tighter by the end; and at
        /// <see cref="CurlBursts"/> flung open, arms and legs wide, his head back, the sword
        /// swept round under him and up over him. He falls from <see cref="CurlTicks"/>.
        /// </summary>
        public const int CurlLeaps = 8;
        public const int CurlRises = 16;
        public const int CurlBursts = 52;
        public const int CurlTicks = 58;

        public static readonly FoighTrack Curl = From()
            .At(CurlLeaps, Ease.SineIn).Lean(24f).Nod(-10f).Legs(-46f, 42f).Near(40f).Far(30f).Sword(120f)
            .At(CurlLeaps + 0.01f).Air(true)
            .At(CurlLeaps + CurlRises + 2f, Ease.SineOut).Lean(74f).Nod(34f).Legs(48f, 40f).Near(70f).Far(62f).Sword(110f).Behind(true)
            .At(CurlBursts - 4f, Ease.SineInOut).Lean(80f).Nod(38f).Legs(54f, 46f).Near(74f).Far(66f).Sword(106f)
            .At(CurlBursts, Ease.PolyOut, 3).Lean(-22f).Nod(-18f).Legs(-40f, 46f).Near(200f).Far(-70f).Sword(-100f).Behind(false)
            .At(CurlTicks, Ease.SineOut).Lean(-10f).Nod(-6f).Legs(-24f, 30f).Near(170f).Far(-40f).Sword(-95f);

        /// <summary>The ball at this tick of it: as <see cref="Curl"/> has it, shaking from when he is folded up until he bursts.</summary>
        public static FoighRig.Pose Curling(float tick)
        {
            FoighRig.Pose pose = Curl.Sample(tick);
            float shakes = tick < CurlBursts ? MathHelper.Clamp((tick - CurlLeaps - CurlRises) / 6f, 0f, 1f) : 0f;

            pose.Lean += MathHelper.ToRadians(2.5f) * shakes * (float)Math.Sin(tick * 2.1f);
            pose.Nod += MathHelper.ToRadians(2f) * shakes * (float)Math.Sin(tick * 2.9f + 1f);

            return pose;
        }

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
            from.Slide = 0f;
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

        // ============================================================
        // THE PASSAGE INTO HIS SECOND PHASE: stopped, down on a knee over the sword, blood,
        // the sword out of the ground and up, a stone along it
        // ============================================================

        /// <summary>
        /// Two thirds of his life gone: whatever he was doing, he stops, rocked back by it
        /// and then bowed over it. The picture is going to black while he does.
        /// </summary>
        public const int SpentTicks = 26;

        /// <summary>Asked for a blade pointing into the ground ahead of him; FoighRig.Ground lays it on it.</summary>
        private const float SpentAhead = 30f;

        public static FoighTrack Spent(FoighRig.Pose from)
        {
            from.Turn = 1f;
            from.Reach = 1f;
            from.Slide = 0f;
            from.Behind = false;
            from.Pass = 0f;

            float arm = MathHelper.ToDegrees(FoighTrack.Nearest(MathHelper.ToRadians(-13.2f), from.NearArm));

            // The sword comes down on the side of him it is on: behind him where it drags, or
            // ahead of him on the ground, whichever is the less far for it to go.
            float behind = FoighTrack.Nearest(MathHelper.ToRadians(Dragged), from.Sword);
            float ahead = FoighTrack.Nearest(MathHelper.ToRadians(SpentAhead), from.Sword);
            float sword = MathHelper.ToDegrees(Math.Abs(ahead - from.Sword) < Math.Abs(behind - from.Sword) ? ahead : behind);

            return new FoighTrack(from)
                .At(7f, Ease.PolyOut, 2).Lean(-12f).Nod(-10f)
                .At(SpentTicks, Ease.SineInOut).Lean(22f).Nod(20f).Legs(-18f, 16f).Near(arm).Far(14f).Sword(sword);
        }

        /// <summary>
        /// Down on a knee before his sword, which stands upright in the ground, driven in
        /// until its handle is at his chest (nearly three quarters of the blade under it),
        /// both his hands on the handle and his head bowed over it: how he is when the
        /// picture comes back. The sword is drawn against him, over his body and his arms
        /// and under his head; the ground hides what is in it.
        /// </summary>
        public const float KneeBlade = 90f;

        public static readonly FoighRig.Pose KneeDown = new FoighTrack().At(0f)
            .Lean(30f).Nod(28f).Near(114f).Far(150f).Legs(-56f, 45f)
            .Hand(1).Sword(KneeBlade).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f).UnderHead(true)
            .BothHands()
            .Sample(0f);

        /// <summary>Where the sword's point is in the ground while he kneels, from the ground between his feet, facing right.</summary>
        public static readonly Vector2 KneePoint = FoighRig.PointAt(KneeDown);

        /// <summary>How far ahead of him the blade goes into the ground while he kneels.</summary>
        public static readonly float KneeEnters = Enters(KneeDown);

        /// <summary>
        /// How far ahead of him this pose's blade goes into the ground: its hand over it and
        /// its point under it. (Not in the ground at all, how far ahead its point is.)
        /// </summary>
        public static float Enters(FoighRig.Pose pose)
        {
            Vector2 fist = FoighRig.FistAt(pose);
            Vector2 point = FoighRig.PointAt(pose);

            // From the fist, over the ground, to the point, under it.
            if (point.Y <= 0f || fist.Y >= 0f)
                return point.X;

            return fist.X + (point.X - fist.X) * (-fist.Y / (point.Y - fist.Y));
        }

        /// <summary>
        /// The ticks of the knee at which he spits blood: four times, by request a second,
        /// four tenths of a second and three tenths apart. The first is a while after the
        /// picture is back and the camera has got to him.
        /// </summary>
        public const int FirstSpit = 180;
        public static readonly int[] Spits = { FirstSpit, FirstSpit + 60, FirstSpit + 84, FirstSpit + 102 };

        /// <summary>A spit: the jerk of it, over this many ticks.</summary>
        public const int SpitTicks = 16;

        /// <summary>He raises his head from this tick, and has it up by that one.</summary>
        public const int KneeLooks = FirstSpit + 102 + 46;
        public const int KneeLooked = KneeLooks + 26;

        /// <summary>How hard he is bent by a spit at this tick of the knee, 0 to 1: sharp down, and slowly back.</summary>
        public static float Retch(float tick)
        {
            float bent = 0f;

            foreach (int at in Spits)
            {
                float t = (tick - at + 3f) / SpitTicks;

                if (t >= 0f && t <= 1f)
                    bent = Math.Max(bent, (float)Math.Sin(Math.Pow(t, 0.5) * Math.PI));
            }

            return bent;
        }

        /// <summary>On his knee, at this tick of it: breathing hard, bent by each spit, and at the last his head coming up.</summary>
        public static FoighRig.Pose Knee(float tick)
        {
            FoighRig.Pose pose = KneeDown;

            float breath = (float)Math.Sin(tick * 0.11f);
            float bent = Retch(tick);
            float up = Easing.Smoother(MathHelper.Clamp((tick - KneeLooks) / (KneeLooked - KneeLooks), 0f, 1f));

            pose.Lean += MathHelper.ToRadians(1.6f * breath + 9f * bent - 6f * up);
            pose.Nod += MathHelper.ToRadians(1.2f * breath + 16f * bent - 48f * up);

            // The sword is in the ground: it is he who moves, about its point, and his hands stay on it.
            Hold(ref pose, 0f);

            return pose;
        }

        /// <summary>
        /// The pose with both his hands on the sword where it stands in the ground, drawn up
        /// out of it by this much along the upright line it went in by: the hand that has it
        /// by its grip, the other on the handle under it.
        /// </summary>
        private static void Hold(ref FoighRig.Pose pose, float drawn)
        {
            FoighRig.Plant(ref pose, new Vector2(KneePoint.X, KneePoint.Y - drawn));
            FoighRig.HandOnBlade(ref pose, true);
        }

        /// <summary>
        /// The sword out of the ground and up at the sky; then down before him and turned
        /// toward the eye, the bottom hand to his belt and back with a stone, and the stone
        /// along the blade from the guard to the point (he draws the sword back through it);
        /// the blade turned flat again, the stone put away, and the sword over and down
        /// behind him into the dragging stance.
        ///
        /// Out of the ground: it is in too deep to be lifted clear (it is twice his height).
        /// He sets himself over it (<see cref="WhetGrips"/>) and heaves, coming up off his
        /// knee, and it comes up its own line as far as his arms will take it
        /// (<see cref="WhetDraws"/>, by <see cref="WhetLoose"/>); then he tears it out
        /// forward through the ground, both hands still on it, and it is level before him
        /// at <see cref="WhetRips"/> and on its way up.
        /// </summary>
        public const int WhetGrips = 10;
        public const int WhetLoose = 26;
        public const int WhetRips = 34;
        public const int WhetHigh = 46;
        public const int WhetHeld = 64;
        public const int WhetBelt = 74;
        public const int WhetSet = 88;
        public const int WhetSwipe = 100;
        public const int WhetShown = 120;
        public const int WhetAway = 134;
        public const int WhetTicks = 160;

        /// <summary>How far up out of the ground the heave brings the sword before it is torn out.</summary>
        public const float WhetDraws = 9f;

        /// <summary>The blade as it is seen while the stone goes along it: turned toward the eye, half its length.</summary>
        public const float WhetReach = 0.5f;

        /// <summary>The rest of him through the heave: his head down to it, and up off his knee. His arms are put on the sword after (<see cref="Heaving"/>).</summary>
        private static readonly FoighTrack WhetHeave = new FoighTrack(Knee(KneeLooked))
            .At(WhetGrips, Ease.SineInOut).Lean(34f).Nod(16f)
            .At(WhetLoose, Ease.PolyIn, 2).Lean(40f).Nod(4f).Legs(-30f, 26f);

        /// <summary>How far up out of the ground the sword has come by this tick of it: nothing while he sets himself, then slowly, then faster.</summary>
        public static float Drawn(float tick)
        {
            float t = MathHelper.Clamp((tick - WhetGrips) / (WhetLoose - WhetGrips), 0f, 1f);

            return WhetDraws * Easing.Apply(Ease.PolyIn, t, 2);
        }

        /// <summary>
        /// The heave, at this tick of it: the sword still in the ground, coming up the line
        /// it went in by, and he shaking with it, both hands on its handle.
        /// </summary>
        public static FoighRig.Pose Heaving(float tick)
        {
            FoighRig.Pose pose = WhetHeave.Sample(tick);

            float strain = MathHelper.Clamp((tick - WhetGrips) / (WhetLoose - WhetGrips), 0f, 1f);

            pose.Lean += (float)(Math.Sin(tick * 1.1f) + 0.5 * Math.Sin(tick * 2.9f + 1f)) * 0.02f * strain;

            Hold(ref pose, Drawn(tick));

            return pose;
        }

        public static readonly FoighTrack Whet = new FoighTrack(Heaving(WhetLoose), WhetLoose)
            .At(WhetRips, Ease.PolyIn, 2).UnderHead(false).Lean(10f).Nod(-4f).Near(72f).Legs(-18f, 14f).Sword(6f).BothHands()
            .At(WhetHigh, Ease.PolyOut, 3).Lean(-4f).Nod(-16f).Near(150f).Far(-4.4f).Legs(-10f, 8f).Sword(-74f)
            .At(WhetHeld).Near(152f).Sword(-76f)
            .At(WhetBelt, Ease.SineInOut).Lean(2f).Nod(-2f).Near(104f).Far(-58f).Sword(-40f).Reach(0.8f)
            .At(WhetSet, Ease.SineInOut).Lean(4f).Nod(8f).Near(64f).Far(84f).Sword(-13f).Reach(WhetReach).Legs(-8f, 8f)
            .At(WhetSwipe, Ease.PolyIn, 2).Lean(6f).Nod(6f).Near(-64f).Far(96f).Sword(-13f).Legs(-16f, 12f)
            .At(WhetSwipe + 6f, Ease.SineOut).Near(-68f).Far(118f).Lean(7f)
            .At(WhetShown, Ease.SineInOut).Lean(0f).Nod(-10f).Near(96f).Far(40f).Sword(-52f).Reach(1f).Legs(-10f, 8f)
            .At(WhetAway, Ease.SineInOut).Near(110f).Far(-50f).Sword(-58f).Nod(-12f)
            .At(WhetTicks - 6f, Ease.PolyIn, 2).Near(-13.2f).Far(-4.4f).Sword(Dragged - 360f).Lean(7f).Nod(5f).Legs(3f, 0f)
            .At(WhetTicks).Sword(Dragged - 360f);

        /// <summary>
        /// At this tick of it. Until the sword gives (<see cref="WhetLoose"/>) it is the
        /// heave. And while the stone goes along the blade, the hand that has the stone is
        /// on the blade.
        /// </summary>
        public static FoighRig.Pose Whetting(float tick)
        {
            if (tick < WhetLoose)
                return Heaving(tick);

            FoighRig.Pose pose = Whet.Sample(tick);

            // Onto the blade over the last of the hand's way to it, on it all the way along,
            // and off its point.
            float on = MathHelper.Clamp((tick - (WhetSet - 5f)) / 5f, 0f, 1f) * (tick <= WhetSwipe + 1f ? 1f : 0f);

            if (on > 0f)
            {
                FoighRig.Pose laid = pose;

                if (FoighRig.HandOnBlade(ref laid))
                    pose = FoighTrack.Blend(pose, laid, Easing.Smoother(on));
            }

            return pose;
        }

        /// <summary>
        /// The first tick of it at which no part of the sword is in the ground any more: from
        /// here he is drawn over the tiles again, and the blade is kept out of the ground as
        /// it is in the fight.
        /// </summary>
        public static readonly int WhetOut = FirstOut();

        private static int FirstOut()
        {
            for (int tick = WhetLoose; tick < WhetHigh; tick++)
            {
                if (FoighRig.PointAt(Whetting(tick)).Y <= 0f)
                    return tick;
            }

            return WhetHigh;
        }

        /// <summary>How far ahead of him the sword's point comes up out of the ground, torn through it.</summary>
        public static readonly float WhetBreaks = BreaksAt();

        private static float BreaksAt()
        {
            const int steps = 64;

            // Between the last tick it is under and the first it is not: where it crosses.
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = FoighRig.PointAt(Whetting(WhetOut - 1f + i / (float)steps));

                if (point.Y <= 0f)
                    return point.X;
            }

            return FoighRig.PointAt(Whetting(WhetOut)).X;
        }

        /// <summary>Whether the stone is in his bottom hand at this tick of it.</summary>
        public static bool StoneOut(float tick) => tick >= WhetBelt + 2 && tick < WhetAway;

        // ============================================================
        // THE PASSAGE INTO HIS THIRD PHASE: shaking over his sword, which is in the ground
        // before him; the sword out and up, turned on himself, and through him; torn out
        // again and the blood thrown off it; and behind them, their wings in his hand, cut
        // ============================================================

        /// <summary>
        /// Three tenths of his life left. When the picture comes back he is on his feet,
        /// bent over his sword, which is in the ground before him at the slant it went in
        /// at (more than half its blade under it), both his hands on its handle and his
        /// head hanging over it. The ground hides what is in it.
        /// </summary>
        public const float ShudderBlade = 62f;

        public static readonly FoighRig.Pose ShudderDown = new FoighTrack().At(0f)
            .Lean(24f).Nod(26f).Near(100f).Far(60f).Legs(-30f, 26f)
            .Hand(1).Sword(ShudderBlade).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .BothHands()
            .Sample(0f);

        /// <summary>Where the sword's point is in the ground while he holds on to it, from the ground between his feet, facing right; and how far ahead of him the blade goes into the ground.</summary>
        public static readonly Vector2 ShudderPoint = FoighRig.PointAt(ShudderDown);
        public static readonly float ShudderEnters = Enters(ShudderDown);

        /// <summary>The ticks of it at which he coughs blood, and how long the jerk of one is; and his head comes up from this tick, and is up by that one.</summary>
        public static readonly int[] Coughs = { 76, 104 };
        public const int CoughTicks = 16;
        public const int ShudderLooks = 124;
        public const int ShudderLooked = 148;

        /// <summary>
        /// A shaking, at this tick: between minus one and one, never the same two ticks
        /// running, and with no beat to it (three turns of different lengths, added).
        /// </summary>
        public static float Tremor(float tick)
        {
            return (float)(Math.Sin(tick * 1.7) + 0.6 * Math.Sin(tick * 2.9 + 1.0) + 0.4 * Math.Sin(tick * 4.3 + 2.0)) * 0.5f;
        }

        /// <summary>How hard he is bent by a cough at this tick of it, 0 to 1: sharp down, and slowly back.</summary>
        public static float Cough(float tick)
        {
            float bent = 0f;

            foreach (int at in Coughs)
            {
                float t = (tick - at + 3f) / CoughTicks;

                if (t >= 0f && t <= 1f)
                    bent = Math.Max(bent, (float)Math.Sin(Math.Pow(t, 0.5) * Math.PI));
            }

            return bent;
        }

        /// <summary>
        /// The pose with both his hands on the sword where it is in the ground, its point
        /// at this place: the hand that has it by its grip, the other on the handle (or,
        /// bent so far over it that the handle is past that arm's reach, as near it as
        /// the arm gets: a pixel or so short, at the worst of a cough).
        /// </summary>
        private static void HoldOn(ref FoighRig.Pose pose, Vector2 point)
        {
            FoighRig.Plant(ref pose, point);

            if (!FoighRig.HandOnBlade(ref pose, true))
                FoighRig.HandNear(ref pose);
        }

        /// <summary>
        /// Holding on to his sword, at this tick of it: all of him shaking, bent by each
        /// cough, and at the last his head coming up (he shakes less from there). It is
        /// he who moves: the sword is in the ground, and his hands stay on it.
        /// </summary>
        public static FoighRig.Pose Shudder(float tick)
        {
            FoighRig.Pose pose = ShudderDown;

            float breath = (float)Math.Sin(tick * 0.13f);
            float bent = Cough(tick);
            float up = Easing.Smoother(MathHelper.Clamp((tick - ShudderLooks) / (ShudderLooked - ShudderLooks), 0f, 1f));
            float shake = 1f - 0.6f * up;

            pose.Lean += MathHelper.ToRadians(1.4f * breath + 1.2f * shake * Tremor(tick) + 8f * bent - 5f * up);
            pose.Nod += MathHelper.ToRadians(1f * breath + 0.9f * shake * Tremor(tick + 40f) + 14f * bent - 24f * up);
            pose.NearLeg += MathHelper.ToRadians(1.4f * shake * Tremor(tick + 17f));
            pose.FarLeg += MathHelper.ToRadians(-1.2f * shake * Tremor(tick + 31f));

            HoldOn(ref pose, ShudderPoint);

            return pose;
        }

        /// <summary>As he is when his head is up, with the shaking left out: what the next of it begins from.</summary>
        private static FoighRig.Pose ShudderUp()
        {
            FoighRig.Pose pose = ShudderDown;

            pose.Lean += MathHelper.ToRadians(-5f);
            pose.Nod += MathHelper.ToRadians(-24f);

            HoldOn(ref pose, ShudderPoint);

            return pose;
        }

        /// <summary>
        /// The sword out of the ground and up at the sky in both hands, and held there
        /// (<see cref="GutHigh"/> to <see cref="GutHeld"/>); tipped back over his
        /// shoulder (<see cref="GutTips"/>) and let forward through his hands, until he has it by the blade a
        /// hand from its point, level before him, its hilt a sword's length ahead and its
        /// point at himself (<see cref="GutTurned"/>; it is longer than he is tall, and
        /// there is no other way to turn it on oneself); a breath, the point drawn off
        /// him (<see cref="GutPoised"/>); and through him, to the guard, the blade out of
        /// his back (<see cref="GutThrough"/>). He stays bent over it, shaking, to the end.
        ///
        /// Out of the ground as it comes out of it on his knee: he sets himself
        /// (<see cref="GutSets"/>), heaves, and it comes back up its own slant
        /// (<see cref="GutDraws"/>, by <see cref="GutLoose"/>); then it is torn out forward.
        /// </summary>
        public const int GutSets = 12;
        public const int GutLoose = 24;
        public const int GutRips = 32;
        public const int GutHigh = 46;
        public const int GutHeld = 72;
        public const int GutTips = 82;
        public const int GutTurned = 92;
        public const int GutPoised = 106;
        public const int GutThrough = 111;
        public const int GutSags = 140;
        public const int GutTicks = 162;

        /// <summary>How far back up its own slant the heave brings the sword before it is torn out.</summary>
        public const float GutDraws = 12f;

        /// <summary>
        /// Its point at himself: how far the sword has been let forward through his hands
        /// (they are on the blade, this far from its grip), and how far he draws it off
        /// himself before it goes in. Out of himself again it is seen end on: this much of
        /// it. And through him: where the blade points (behind him, and a little up).
        /// </summary>
        public const float GutFed = 78f;
        public const float GutBack = 5f;
        public const float GutReach = 0.2f;
        public const float GutBlade = -172f;

        /// <summary>The rest of him through the heave: down over it, and up with it. His arms are put on the sword after (<see cref="GutHeaving"/>).</summary>
        private static readonly FoighTrack GutHeave = new FoighTrack(ShudderUp())
            .At(GutSets, Ease.SineInOut).Lean(30f).Nod(8f).Legs(-36f, 32f)
            .At(GutLoose, Ease.PolyIn, 2).Lean(16f).Nod(-2f).Legs(-20f, 16f);

        /// <summary>How far up its slant the sword has come by this tick of it: nothing while he sets himself, then slowly, then faster.</summary>
        public static float GutDrawn(float tick)
        {
            float t = MathHelper.Clamp((tick - GutSets) / (GutLoose - GutSets), 0f, 1f);

            return GutDraws * Easing.Apply(Ease.PolyIn, t, 2);
        }

        /// <summary>The heave, at this tick of it: the sword still in the ground, coming back up the slant it went in by, both his hands on its handle.</summary>
        public static FoighRig.Pose GutHeaving(float tick)
        {
            FoighRig.Pose pose = GutHeave.Sample(tick);

            float strain = MathHelper.Clamp((tick - GutSets) / (GutLoose - GutSets), 0f, 1f);
            float slant = MathHelper.ToRadians(ShudderBlade);

            pose.Lean += Tremor(tick) * 0.03f * (0.4f + 0.6f * strain);

            HoldOn(ref pose, ShudderPoint - new Vector2((float)Math.Cos(slant), (float)Math.Sin(slant)) * GutDrawn(tick));

            return pose;
        }

        public static readonly FoighTrack Gut = new FoighTrack(GutHeaving(GutLoose), GutLoose)
            .At(GutRips, Ease.PolyIn, 2).Lean(6f).Nod(-6f).Near(96f).Legs(-14f, 10f).Sword(-8f).BothHands()
            .At(GutHigh, Ease.PolyOut, 3).Lean(-3f).Nod(-18f).Near(176f).Far(150f).Legs(-8f, 6f).Sword(-90f)
            .At(GutHeld).Near(178f).Sword(-91f)
            .At(GutHeld + 0.01f).Behind(true)
            .At(GutTips, Ease.SineIn).Lean(0f).Nod(-8f).Near(134f).Legs(-12f, 10f).Sword(-132f).Slide(24f).BothHands()
            .At(GutTurned, Ease.SineOut).Lean(4f).Nod(14f).Near(60f).Legs(-16f, 14f).Sword(-180f).Slide(GutFed).BothHands()
            .At(GutPoised, Ease.SineInOut).Lean(-6f).Nod(-10f).Near(66f).Sword(-177f).Slide(GutFed + GutBack).BothHands()
            .At(GutThrough, Ease.PolyIn, 2).Lean(32f).Nod(30f).Near(48f).Legs(-30f, 26f).Sword(GutBlade).Slide(0f).BothHands()
            .At(GutSags, Ease.SineInOut).Lean(36f).Nod(34f).Legs(-40f, 34f).BothHands()
            .At(GutTicks).Lean(35f).BothHands();

        /// <summary>
        /// At this tick of it. Until the sword gives (<see cref="GutLoose"/>) it is the
        /// heave; and with it through him he shakes with it, hardest at first.
        /// </summary>
        public static FoighRig.Pose Gutting(float tick)
        {
            if (tick < GutLoose)
                return GutHeaving(tick);

            FoighRig.Pose pose = Gut.Sample(tick);

            if (tick > GutThrough)
            {
                float hard = 0.35f + 0.65f * (float)Math.Exp(-(tick - GutThrough) / 14f);

                pose.Lean += MathHelper.ToRadians(1.8f * hard * Tremor(tick));
                pose.Nod += MathHelper.ToRadians(1.2f * hard * Tremor(tick + 40f));
                pose.Sword += MathHelper.ToRadians(0.9f * hard * Tremor(tick + 9f));
                pose.NearLeg += MathHelper.ToRadians(1.6f * hard * Tremor(tick + 17f));
                pose.FarLeg += MathHelper.ToRadians(-1.4f * hard * Tremor(tick + 31f));
            }

            return pose;
        }

        /// <summary>The first tick of it at which no part of the sword is in the ground any more; and how far ahead of him its point comes up out of it.</summary>
        public static readonly int GutOut = GutFirstOut();
        public static readonly float GutBreaks = GutBreaksAt();

        private static int GutFirstOut()
        {
            for (int tick = GutLoose; tick < GutHigh; tick++)
            {
                if (FoighRig.PointAt(Gutting(tick)).Y <= 0f)
                    return tick;
            }

            return GutHigh;
        }

        private static float GutBreaksAt()
        {
            const int steps = 64;

            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = FoighRig.PointAt(Gutting(GutOut - 1f + i / (float)steps));

                if (point.Y <= 0f)
                    return point.X;
            }

            return FoighRig.PointAt(Gutting(GutOut)).X;
        }

        /// <summary>
        /// Torn out of himself again, and the blood thrown off it. He sets himself against
        /// it (<see cref="BledSets"/>) and it comes out the way it went in (seen shorter
        /// again; out at <see cref="BledOut"/>, and drawn before him from there); up over
        /// his head (<see cref="BledUp"/>), and flicked down ahead of him, where it stops
        /// short of the ground (<see cref="BledFlick"/>). He comes up slowly
        /// (<see cref="BledRises"/>), brings the sword up before him to look along it
        /// (<see cref="BledShows"/>), and at the last lets it down and looks up at them
        /// (<see cref="BledLowers"/> to <see cref="BledLooks"/>).
        /// </summary>
        public const int BledSets = 10;
        public const int BledOut = 17;
        public const int BledUp = 25;
        public const int BledFlick = 31;
        public const int BledSettles = 46;
        public const int BledRises = 72;
        public const int BledShows = 104;
        public const int BledLowers = 276;
        public const int BledLooks = 292;

        public static readonly FoighTrack Bled = new FoighTrack(Gut.Sample(GutTicks))
            .At(BledSets, Ease.SineInOut).Lean(20f).Nod(4f).Legs(-24f, 20f).Near(52f).BothHands()
            .At(BledOut, Ease.PolyIn, 2).Lean(-8f).Nod(-14f).Legs(-12f, 10f).Near(78f).Reach(GutReach).BothHands()
            .At(BledOut + 0.01f).Behind(false)
            .At(BledOut + 3f, Ease.PolyOut, 2).Reach(1f).Sword(-150f).Near(120f).Far(40f)
            .At(BledUp, Ease.SineOut).Sword(-100f).Near(172f).Far(20f).Lean(-10f).Nod(-12f).Legs(-10f, 8f)
            .At(BledFlick, Ease.PolyIn, 3).Sword(8f).Near(66f).Far(-30f).Lean(20f).Nod(2f).Legs(20f, -24f)
            .At(BledSettles, Ease.SineOut).Sword(14f).Near(52f).Lean(14f)
            .At(BledRises, Ease.SineInOut).Sword(30f).Near(22f).Far(-4.4f).Lean(6f).Nod(2f).Legs(-6f, 6f)
            .At(BledShows, Ease.SineInOut).Sword(-26f).Near(84f).Far(60f).Lean(2f).Nod(8f).Legs(-8f, 8f)
            .At(BledLowers).Sword(-26f)
            .At(BledLooks, Ease.SineInOut).Sword(28f).Near(16f).Far(-4.4f).Lean(5f).Nod(-8f).Legs(-4f, 4f);

        /// <summary>
        /// Behind them. The bottom hand out to their back and closed on their wings
        /// (<see cref="SeverGrabs"/>); a pull, up and back, the sword going up behind his
        /// head (<see cref="SeverPulls"/> to <see cref="SeverUp"/>); and down between his
        /// hand and their back, into the ground to its guard (<see cref="SeverCuts"/>),
        /// the hand coming away with what it has. He lets go of the sword where it stands
        /// (<see cref="SeverLeaves"/>), comes away from them a step
        /// (<see cref="SeverSteps"/>), and stands holding up what he has taken, looking at
        /// it (<see cref="SeverHolds"/>).
        /// </summary>
        public const int SeverGrabs = 10;
        public const int SeverPulls = 28;
        public const int SeverUp = 35;
        public const int SeverCuts = 40;
        public const int SeverLeaves = 43;
        public const int SeverHolds = 60;

        /// <summary>How steep the blade is where the cut ends, in the ground before him: all but upright, between his hand and their back.</summary>
        public const float SeverBlade = 86f;

        public static readonly FoighTrack Sever = new FoighTrack().At(0f)
            .Lean(22f).Nod(-4f).Near(-34f).Far(34f).Legs(-28f, 26f)
            .Hand(1).Sword(Dragged).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .At(SeverGrabs, Ease.PolyIn, 2).Far(94f).Lean(14f).Legs(14f, -18f)
            .At(SeverPulls, Ease.SineInOut).Far(140f).Lean(-8f).Nod(-8f).Near(150f).Sword(236f).Legs(-8f, 10f)
            .At(SeverUp, Ease.SineOut).Near(176f).Sword(254f).Lean(-10f)
            .At(SeverCuts, Ease.PolyIn, 3).Near(75f).Sword(360f + SeverBlade).Lean(24f).Nod(10f).Legs(22f, -26f).Far(168f)
            .At(SeverLeaves).Near(72f).Lean(22f)
            .At(SeverLeaves + 0.01f).Hand(0)
            .At(SeverHolds, Ease.SineInOut).Near(-8f).Lean(3f).Nod(12f).Legs(-4f, 4f).Far(106f);

        /// <summary>At this tick of it.</summary>
        public static FoighRig.Pose Severing(float tick) => Sever.Sample(tick);

        /// <summary>As he holds the sword the moment before he lets go of it: where it is left standing is where this has it.</summary>
        public static readonly FoighRig.Pose SeverLeft = Sever.Sample(SeverLeaves);

        /// <summary>
        /// And away, as the picture goes: the hand opened, and what was in it let fall
        /// (by <see cref="LeaveOpens"/>); he stands as he was until the picture has gone
        /// (<paramref name="gone"/>, in ticks), and under the black he is in the stance
        /// he fights from, the sword in his hand again.
        /// </summary>
        public const int LeaveOpens = 8;

        /// <summary>From the pose the cut left him in.</summary>
        public static FoighTrack Leaving(FoighRig.Pose from, int gone)
        {
            from.Behind = false;
            from.UnderHead = false;
            from.Pass = 0f;
            from.Reach = 1f;
            from.Slide = 0f;
            from.Turn = 1f;

            return new FoighTrack(from)
                .At(LeaveOpens, Ease.SineInOut).Far(34f).Nod(2f)
                .At(gone - 0.01f)
                .At(gone).Lean(7f).Nod(5f).Near(-13.2f).Far(-4.4f).Legs(3f, 0f).Hand(1).Sword(Dragged).Clean(true);
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

        public static readonly Step[] HewSteps =
        {
            new Step(HewHold, HewDown, 32f),
            new Step(HewRest, HewUp, 22f),
        };

        public static readonly Step[] UpcutSteps =
        {
            new Step(UpcutSets, UpcutCuts, 30f),
        };

        /// <summary>A step into the blow, out of the haul along his chain.</summary>
        public static readonly Step[] ReapSteps =
        {
            new Step(ReapHold, ReapDown, 28f),
        };

        /// <summary>A step into each of the three cuts.</summary>
        public static readonly Step[] RendSteps =
        {
            new Step(RendHold, RendDown, 20f),
            new Step(RendCoiled, RendUp, 14f),
            new Step(RendHeld, RendSmash, 22f),
        };

        /// <summary>Their wings in his hand: away from them a step, so that what he has is seen.</summary>
        public static readonly Step[] SeverSteps =
        {
            new Step(SeverCuts + 2f, SeverHolds - 2f, -24f),
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

        // ============================================================
        // THE END (Foigh.End), by request: his roar; down onto his knees and his hands, the
        // sword let fall before him; his head up to them, shaking and bleeding, laughing,
        // coughing; his back straight, his head up, his hands down, on his knees; and then
        // the sword through him, rigid, a look at them, and down on his back; or left there,
        // his head down, and down on his face
        // ============================================================

        private static float Deg(float degrees) => MathHelper.ToRadians(degrees);

        /// <summary>
        /// Shown, before his roar (Foigh.End): standing as he does, the sword dragged, a
        /// little bowed and getting his breath, hard at first; straight, and his breath let
        /// go of, by the last of it (<paramref name="ticks"/>), so that his roar takes its
        /// own from his stance.
        /// </summary>
        public static FoighRig.Pose Showing(float tick, float ticks)
        {
            FoighRig.Pose pose = Drag;

            float breath = (float)Math.Sin(tick * 0.16f);
            float hard = 1f - MathHelper.Clamp((tick - (ticks - 30f)) / 30f, 0f, 1f);

            pose.Lean += Deg(hard * (5f + 2.4f * breath));
            pose.Nod += Deg(hard * (6f + 1.6f * breath));
            pose.FarArm += Deg(hard * 1.2f * breath);

            return pose;
        }

        /// <summary>
        /// His roar where he stands: a breath (to <see cref="RoarDraws"/>); the voice, his
        /// head thrown back and his arms out from him, shaking (to <see cref="RoarLets"/>:
        /// two and a half seconds, "big long roar"); and what is left of his breath going
        /// out of him, his head down (to <see cref="RoarTicks"/>). The sword is dragged
        /// behind him as he stood (FoighRig.Ground lays its point on the ground).
        /// </summary>
        public const int RoarDraws = 22;
        public const int RoarVoice = 150;
        public const int RoarLets = RoarDraws + RoarVoice;
        public const int RoarTicks = RoarLets + 36;

        private static readonly FoighTrack Roared = From()
            .At(RoarDraws, Ease.SineInOut).Lean(-7f).Nod(-12f).Near(-24f).Far(-16f).Legs(-9f, 11f)
            .At(RoarDraws + 8, Ease.PolyOut, 3).Lean(-16f).Nod(-34f).Near(-44f).Far(58f).Legs(-16f, 18f)
            .At(RoarLets - 12, Ease.SineInOut).Lean(-11f).Nod(-26f).Near(-38f).Far(46f).Legs(-15f, 17f)
            .At(RoarTicks, Ease.SineInOut).Lean(16f).Nod(24f).Near(-16f).Far(4f).Legs(-9f, 9f);

        /// <summary>How hard his voice shakes him at this tick of the roar, 0 to 1.</summary>
        private static float Shaken(float tick)
        {
            return MathHelper.Clamp((tick - RoarDraws - 4f) / 6f, 0f, 1f) * (1f - MathHelper.Clamp((tick - (RoarLets - 14f)) / 14f, 0f, 1f));
        }

        /// <summary>His roar, at this tick of it: shaking with the voice, and breathing hard after.</summary>
        public static FoighRig.Pose Roaring(float tick)
        {
            FoighRig.Pose pose = Roared.Sample(tick);

            float shake = Shaken(tick);
            float spent = MathHelper.Clamp((tick - RoarLets) / 20f, 0f, 1f);
            float breath = (float)Math.Sin(tick * 0.18f);

            pose.Lean += Deg(1.5f * shake * Tremor(tick * 1.5f) + 2.2f * spent * breath);
            pose.Nod += Deg(2f * shake * Tremor(tick * 1.5f + 30f) + 1.4f * spent * breath);
            pose.NearArm += Deg(1.4f * shake * Tremor(tick * 1.5f + 23f));
            pose.FarArm += Deg(3f * shake * Tremor(tick * 1.5f + 11f));

            return pose;
        }

        /// <summary>
        /// Down, by request at a slower pace. From where his roar left him, swaying a moment
        /// (to <see cref="FoldFalls"/>; the camera is on him from before his roar); his knees
        /// going, and down onto both of them (<see cref="FoldKnees"/>); his body over and his
        /// arms out ahead of him, the sword let go of on the way (<see cref="FoldLets"/>: it
        /// falls before him, down by <see cref="FoldLands"/>, Foigh.End), and his hands onto
        /// the ground (<see cref="FoldHands"/>); and still, breathing (<see cref="FoldTicks"/>).
        /// </summary>
        public const int FoldFalls = 40;
        public const int FoldKnees = FoldFalls + 46;
        public const int FoldLets = FoldFalls + 66;
        public const int FoldHands = FoldFalls + 84;
        public const int FoldLands = FoldFalls + 94;
        public const int FoldTicks = FoldFalls + 130;

        /// <summary>
        /// On both knees. Each leg is one picture, from the hip to the sole: the near one
        /// back along the ground under him, the far one down from the hip to where its knee
        /// is on the ground and on into it (his hips this much lower than his legs would
        /// have them: under the ground is not drawn while he is down, Foigh.End). So the far
        /// one is seen as the thigh to the knee, and the near one as the leg folded back.
        /// </summary>
        public const float KneelNear = -50f;
        public const float KneelFar = -26f;
        public const float KneelRise = -5f;

        /// <summary>
        /// On his knees and his hands: bent over as far as his arms will have his hands on
        /// the ground, his hips down by his heels (and further into the ground, unseen); and
        /// his head, hanging, and raised to look ahead of him.
        /// </summary>
        public const float FoursLean = 50f;
        public const float FoursRise = -12.7f;
        public const float FoursHangs = -2f;
        public const float FoursLooks = -60f;

        /// <summary>How far forward of straight down an arm from this shoulder points with its fist on the ground: straight down if the ground is out of its reach.</summary>
        private static float ToGround(float hips, Vector2 shoulder, float length, float lean)
        {
            float high = hips - FoighRig.Turned(shoulder, lean).Y;

            if (high >= length)
                return 0f;

            return (float)Math.Acos(Math.Max(0f, high) / length);
        }

        /// <summary>Both his arms down to the ground ahead of him, the fists on it, for this pose as it stands.</summary>
        public static void Palms(ref FoighRig.Pose pose)
        {
            float hips = FoighRig.HipsOver(pose);

            pose.NearArm = ToGround(hips, FoighRig.NearShoulder, FoighRig.NearArmLength, pose.Lean);
            pose.FarArm = ToGround(hips, FoighRig.FarShoulder, FoighRig.FarArmLength, pose.Lean);
        }

        /// <summary>On his knees and his hands, his head hanging, nothing in his hands.</summary>
        public static readonly FoighRig.Pose Fours = OnFours(FoursHangs);

        /// <summary>The same, his head raised to look ahead of him.</summary>
        public static readonly FoighRig.Pose FoursUp = OnFours(FoursLooks);

        private static FoighRig.Pose OnFours(float nod)
        {
            FoighRig.Pose pose = new FoighTrack().At(0f)
                .Lean(FoursLean).Nod(nod).Legs(KneelNear, KneelFar).Rise(FoursRise)
                .Hand(0).Sword(60f).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
                .Sample(0f);

            Palms(ref pose);

            return pose;
        }

        /// <summary>The way down, after the roar's last pose (it is joined to that from FoldFalls): the arms are put on the ground at the end (Folding).</summary>
        private static readonly FoighTrack Folded = new FoighTrack(Roared.Sample(RoarTicks), FoldFalls)
            .At(FoldKnees, Ease.PolyIn, 2).Lean(8f).Nod(22f).Near(-22f).Far(2f).Legs(KneelNear, KneelFar).Rise(KneelRise)
            .At(FoldLets, Ease.SineIn).Lean(40f).Nod(12f).Near(30f).Far(26f).Sword(70f).Hand(0).Rise((KneelRise + FoursRise) * 0.5f)
            .At(FoldHands, Ease.PolyIn, 2).Lean(FoursLean).Nod(FoursHangs).Rise(FoursRise)
            .At(FoldTicks, Ease.SineInOut).Lean(FoursLean).Nod(FoursHangs);

        /// <summary>Down, at this tick of it: swaying a moment; and once his hands are on their way to the ground, put on it.</summary>
        public static FoighRig.Pose Folding(float tick)
        {
            float breath = (float)Math.Sin(tick * 0.16f);

            if (tick < FoldFalls)
            {
                FoighRig.Pose stood = Roared.Sample(RoarTicks);

                stood.Lean += Deg(2.4f * breath + 1.6f * (float)Math.Sin(tick * 0.05f));
                stood.Nod += Deg(1.4f * breath);

                return stood;
            }

            FoighRig.Pose pose = Folded.Sample(tick);

            // From the sword let go of, the arms go to where the ground will have them.
            if (tick > FoldLets)
            {
                FoighRig.Pose down = pose;
                Palms(ref down);

                float t = Easing.Smoother(MathHelper.Clamp((tick - FoldLets) / (FoldHands - FoldLets), 0f, 1f));

                pose.NearArm = MathHelper.Lerp(pose.NearArm, down.NearArm, t);
                pose.FarArm = MathHelper.Lerp(pose.FarArm, down.FarArm, t);
            }

            float settled = MathHelper.Clamp((tick - FoldHands) / 20f, 0f, 1f);

            pose.Lean += Deg(1.8f * settled * breath);
            pose.Nod += Deg(1.2f * settled * breath);

            if (settled > 0f)
                Palms(ref pose);

            return pose;
        }

        /// <summary>On his knees and his hands, his head hanging: shaking, breathing hard.</summary>
        public static FoighRig.Pose Bowing(float tick)
        {
            FoighRig.Pose pose = Fours;

            float breath = (float)Math.Sin(tick * 0.14f);

            pose.Lean += Deg(1.8f * breath + 1.2f * Tremor(tick));
            pose.Nod += Deg(1.2f * breath + 1.6f * Tremor(tick + 40f));

            Palms(ref pose);

            return pose;
        }

        /// <summary>His head up to them, from hanging: over this many ticks.</summary>
        public const int LaughLooked = 36;

        /// <summary>A cough: the jerk of it, over this many ticks; and his coughs this far apart.</summary>
        public const int EndCoughTicks = 18;
        public const int EndCoughEvery = 30;

        /// <summary>
        /// How hard he is bent by a cough at this tick, 0 to 1, of this many coughs from this
        /// tick, each <see cref="EndCoughEvery"/> after the last (none before the first, or
        /// with none).
        /// </summary>
        public static float Coughing(float tick, float first, int count)
        {
            float bent = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (tick - (first + i * EndCoughEvery) + 3f) / EndCoughTicks;

                if (t >= 0f && t <= 1f)
                    bent = Math.Max(bent, (float)Math.Sin(Math.Pow(t, 0.5) * Math.PI));
            }

            return bent;
        }

        /// <summary>His laugh at this tick, -1 to 1: four breaths of it from one tick to the other, nothing outside them.</summary>
        public static float Laughing(float tick, float from, float to)
        {
            if (tick <= from || tick >= to || to <= from)
                return 0f;

            float t = (tick - from) / (to - from);

            return (float)(Math.Sin(t * 4.0 * MathHelper.TwoPi) * Math.Sin(t * MathHelper.Pi));
        }

        /// <summary>
        /// On his knees and his hands, his head raised to them (<paramref name="up"/>, 0 to 1,
        /// how far): shaking, breathing hard, and bent by his laugh (-1 to 1) and by a cough
        /// (0 to 1).
        /// </summary>
        public static FoighRig.Pose Raised(float tick, float up, float laugh, float cough)
        {
            FoighRig.Pose pose = Fours;

            float breath = (float)Math.Sin(tick * 0.14f);

            pose.Nod = Deg(MathHelper.Lerp(FoursHangs, FoursLooks, Easing.Smoother(MathHelper.Clamp(up, 0f, 1f))));
            pose.Lean += Deg(1.6f * breath + 1.1f * Tremor(tick) + 2.5f * laugh + 6f * cough);
            pose.Nod += Deg(1f * breath + 1.4f * Tremor(tick + 40f) - 6f * laugh + 20f * cough);

            Palms(ref pose);

            return pose;
        }

        /// <summary>
        /// Up off his hands, on his knees: his back straight, his head up, his hands down, by
        /// request (<see cref="UpTicks"/>); and so, breathing, shaking a little.
        /// </summary>
        public const int UpTicks = 60;

        public static readonly FoighRig.Pose KneelUp = new FoighTrack().At(0f)
            .Lean(-2f).Nod(-40f).Near(-6f).Far(-3f).Legs(KneelNear, KneelFar).Rise(KneelRise)
            .Hand(0).Sword(60f).Clean(true).Reach(1f).Turn(1f).HeadTurn(1f)
            .Sample(0f);

        private static readonly FoighTrack Rose = new FoighTrack(FoursUp)
            .At(UpTicks * 0.45f, Ease.SineInOut).Lean(36f).Nod(-30f).Near(30f).Far(26f).Rise((KneelRise + FoursRise) * 0.5f)
            .At(UpTicks, Ease.SineInOut).Lean(-2f).Nod(-40f).Near(-6f).Far(-3f).Rise(KneelRise);

        public static FoighRig.Pose Rising(float tick)
        {
            FoighRig.Pose pose = Rose.Sample(tick);

            float breath = (float)Math.Sin(tick * 0.12f);
            float still = MathHelper.Clamp((tick - UpTicks) / 20f, 0f, 1f);

            pose.Lean += Deg(1.2f * still * breath + 0.6f * Tremor(tick));
            pose.Nod += Deg(0.8f * still * breath + 0.8f * Tremor(tick + 40f));

            return pose;
        }

        /// <summary>
        /// The sword through him (Foigh.End: from the player's hand, thirty degrees down, by
        /// request): his back straight as it comes (<see cref="ThrustIn"/>); jolted back as it
        /// goes through (<see cref="ThrustThrough"/>), and rigid there, "like in the anime with
        /// the big ouch": his body square to the blade (as far back as the blade is down), his
        /// head back, his arms flung out, shaking hard; and his head down to them
        /// (<see cref="ThrustLooks"/> to <see cref="ThrustLooked"/>).
        /// </summary>
        public const int ThrustHand = 12;
        public const int ThrustIn = 16;
        public const int ThrustThrough = 22;
        public const int ThrustLooks = 96;
        public const int ThrustLooked = 126;
        public const float ThrustSlant = 30f;

        private static readonly FoighTrack Pierced = new FoighTrack(KneelUp)
            .At(ThrustIn, Ease.SineInOut).Lean(-4f).Nod(-36f)
            .At(ThrustThrough, Ease.PolyOut, 3).Lean(-ThrustSlant).Nod(-24f).Near(96f).Far(-84f)
            .At(ThrustLooks, Ease.SineInOut).Lean(-ThrustSlant).Nod(-20f).Near(88f).Far(-76f)
            .At(ThrustLooked, Ease.SineInOut).Lean(-ThrustSlant + 2f).Nod(26f).Near(54f).Far(-40f);

        public static FoighRig.Pose Thrust(float tick)
        {
            FoighRig.Pose pose = Pierced.Sample(tick);

            float rigid = MathHelper.Clamp((tick - ThrustThrough + 2f) / 4f, 0f, 1f) * (1f - 0.6f * MathHelper.Clamp((tick - ThrustLooks) / (ThrustLooked - ThrustLooks), 0f, 1f));

            pose.Lean += Deg(0.8f * rigid * Tremor(tick * 3f));
            pose.Nod += Deg(1.2f * rigid * Tremor(tick * 3f + 30f));
            pose.NearArm += Deg(2f * rigid * Tremor(tick * 3f + 11f));
            pose.FarArm += Deg(2f * rigid * Tremor(tick * 3f + 23f));

            return pose;
        }

        /// <summary>The last of <see cref="Thrust"/>, with no shaking: what the cast begins from.</summary>
        public static readonly FoighRig.Pose Pinned = Pierced.Sample(ThrustLooked);

        /// <summary>
        /// The blade drawn out of him (<see cref="CastOut"/>): he sags after it. The hand on his
        /// shoulder shoves (<see cref="CastShoved"/>), and he goes over backward, down on his
        /// back (<see cref="CastDown"/>), and lies there.
        /// </summary>
        public const int CastOut = 18;
        public const int CastShoved = 26;
        public const int CastDown = 50;

        /// <summary>On his back: the hips this high over the ground.</summary>
        public const float LyingHips = 7f;

        private static readonly FoighTrack Felled = new FoighTrack(Pinned)
            .At(CastOut, Ease.SineOut).Lean(-16f).Nod(12f).Near(14f).Far(-10f)
            .At(CastShoved, Ease.SineOut).Lean(-34f).Nod(-4f).Near(30f).Far(-40f)
                .Air(true).Rise(FoighRig.HipsOver(Pinned) - FoighRig.HipsHeight)
            .At(CastDown, Ease.PolyIn, 2).Lean(-90f).Nod(-6f).Near(150f).Far(-150f).Legs(72f, 80f)
                .Rise(LyingHips - FoighRig.HipsHeight);

        public static FoighRig.Pose Casting(float tick)
        {
            return Felled.Sample(tick);
        }

        /// <summary>
        /// Spared (Foigh.End): his head down once the sword is thrown (<see cref="SpareBows"/>
        /// to <see cref="SpareBowed"/>); and for the ten seconds after, by request "confused":
        /// a look after them as they go, his head shaken, a look down at his hands, and up.
        /// </summary>
        public const int SpareBows = 100;
        public const int SpareBowed = 130;
        public const int SpareLooks = 300;
        public const int SpareShakes = 372;
        public const int SpareDown = 470;
        public const int SpareUp = 560;
        public const int SpareTicks = SpareBows + 600;

        private static readonly FoighTrack Spurned = new FoighTrack(KneelUp, SpareBows)
            .At(SpareBowed, Ease.SineInOut).Lean(6f).Nod(40f)
            .At(SpareLooks, Ease.SineInOut).Lean(6f).Nod(40f)
            .At(SpareLooks + 40, Ease.SineInOut).Lean(0f).Nod(-6f)
            .At(SpareShakes, Ease.SineInOut).Lean(0f).Nod(-4f)
            .At(SpareDown, Ease.SineInOut).Lean(4f).Nod(8f)
            .At(SpareDown + 40, Ease.SineInOut).Lean(8f).Nod(44f).Near(10f).Far(14f)
            .At(SpareUp, Ease.SineInOut).Lean(8f).Nod(44f)
            .At(SpareUp + 50, Ease.SineInOut).Lean(2f).Nod(6f).Near(-6f).Far(-3f);

        public static FoighRig.Pose Spared(float tick)
        {
            FoighRig.Pose pose = tick < SpareBows ? KneelUp : Spurned.Sample(tick);

            float breath = (float)Math.Sin(tick * 0.12f);

            pose.Lean += Deg(1.2f * breath + 0.6f * Tremor(tick));
            pose.Nod += Deg(0.8f * breath + 0.8f * Tremor(tick + 40f));

            // His head shaken: turned away from the eye and back, twice, slowly.
            if (tick > SpareShakes && tick < SpareShakes + 96)
            {
                float s = (float)Math.Sin((tick - SpareShakes) / 96f * 2f * MathHelper.TwoPi);
                pose.HeadTurn = 1f - 0.55f * Math.Abs(s);
            }

            return pose;
        }

        /// <summary>As he is at the end of the ten seconds: what his last line and his fall begin from.</summary>
        public static readonly FoighRig.Pose LeftThere = Spurned.Sample(SpareTicks);

        /// <summary>Down on his face (Foigh.End): over forward, and on the ground by <see cref="SlumpDown"/>.</summary>
        public const int SlumpDown = 34;

        /// <summary>On his face: the hips this high over the ground.</summary>
        public const float SlumpHips = 6f;

        private static readonly FoighTrack Slumped = new FoighTrack(LeftThere)
            .At(12f, Ease.SineIn).Lean(24f).Nod(30f).Near(16f).Far(20f)
                .Air(true).Rise(FoighRig.HipsOver(LeftThere) - FoighRig.HipsHeight)
            .At(SlumpDown, Ease.PolyIn, 2).Lean(94f).Nod(8f).Near(150f).Far(160f).Legs(-82f, -78f)
                .Rise(SlumpHips - FoighRig.HipsHeight);

        public static FoighRig.Pose Slumping(float tick)
        {
            return Slumped.Sample(tick);
        }
    }
}
