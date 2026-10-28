using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// How the Gilded Knight holds himself in everything he does: every movement of his as poses
    /// at moments (KnightTrack), the few that are worked out instead (the walk, the run), how far
    /// each carries him along the ground, and the ticks at which things happen in each (the
    /// constants beside each one). The fight says which of these he is in and for how long; the
    /// drawing asks here what that looks like, at whole ticks for him and between them for what
    /// his blades leave behind. So the blade that is drawn, the blade that cuts and its trail are
    /// one sum. Nothing here touches the game.
    /// </summary>
    public static partial class KnightMoves
    {
        private const float Rad = MathHelper.Pi / 180f;

        // ============================================================
        // AT REST: standing, walking, the guard
        // ============================================================

        /// <summary>
        /// Standing with his swords home: his near hand on the Sun's hilt across his body, the
        /// other hanging, his weight a little forward, his head a little down.
        /// </summary>
        public static readonly KnightRig.Pose Rest = new KnightTrack().At(0f)
            .Lean(2f).Nod(5f).Far(6f).Legs(5f, -4f)
            .Hold(KnightRig.Sheathed, KnightRig.Sheathed).On(KnightRig.Sun, -1)
            .Sample(0f);

        /// <summary>A step is this far along the ground, walking: what his legs' swing carries him (two legs of 39.5 swung 0.3 either way).</summary>
        public const float Stride = 23.3f;
        private const float WalkSwing = 0.3f;

        /// <summary>
        /// Walking with his swords home, as he comes up in his scene: <paramref name="gait"/> is how
        /// far he has walked, in steps (a whole number at each footfall). Heavy: he sinks onto
        /// each foot and the body nods with it; the free arm swings; his near hand stays on the hilt.
        /// </summary>
        public static KnightRig.Pose Walk(float gait)
        {
            KnightRig.Pose pose = Rest;

            float s = (float)Math.Sin(gait * MathHelper.Pi);
            float c = (float)Math.Cos(gait * MathHelper.TwoPi);

            pose.NearLeg = 0.04f + WalkSwing * s;
            pose.FarLeg = -0.03f - WalkSwing * s;
            pose.Lean = 3f * Rad + 0.02f * c;
            pose.Nod = 4f * Rad - 0.03f * c;
            pose.FarArm = 6f * Rad - 0.22f * s;

            // The weight comes down on each foot as it lands: a sink just after the footfall.
            float into = gait - (float)Math.Floor(gait);
            pose.Rise = -1.6f * (float)Math.Exp(-into * 6f) * (float)Math.Sin(Math.Min(1f, into * 5f) * MathHelper.Pi);

            return pose;
        }

        /// <summary>The ticks into a step at which the foot comes down (the jangle and the dust): at each whole step.</summary>
        public static bool Footfall(float gaitBefore, float gaitNow) => Math.Floor(gaitBefore) != Math.Floor(gaitNow);

        /// <summary>
        /// The guard, both swords out: the Sun high and forward in the near hand, the Grace low and
        /// back in the far one, the feet apart, the weight on the front one.
        /// </summary>
        public static readonly KnightRig.Pose Guard = new KnightTrack().At(0f)
            .Lean(7f).Nod(3f)
            .Near(89f, 21f).Far(-18f, KnightRig.ArmLong)
            .Legs(16f, -15f)
            .Drawn().Blades(-52f, 158f)
            .Sample(0f);

        /// <summary>A stalking step in his guard is this long.</summary>
        public const float GuardStride = 26f;

        /// <summary>
        /// Walking in his guard, swords out: the blades held, the free arm's sword sways a little
        /// with the steps, the legs going as in the walk but wider.
        /// </summary>
        public static KnightRig.Pose GuardWalk(float gait)
        {
            KnightRig.Pose pose = Guard;

            float s = (float)Math.Sin(gait * MathHelper.Pi);
            float c = (float)Math.Cos(gait * MathHelper.TwoPi);

            pose.NearLeg = 0.08f + 0.34f * s;
            pose.FarLeg = -0.08f - 0.34f * s;
            pose.Lean = 8f * Rad + 0.025f * c;
            pose.Nod = 3f * Rad - 0.03f * c;
            pose.NearArm = 89f * Rad + 0.05f * c;
            pose.SunAngle = -52f * Rad + 0.04f * c;
            pose.FarArm = -18f * Rad - 0.08f * s;
            pose.GraceAngle = 158f * Rad - 0.1f * s;

            float into = gait - (float)Math.Floor(gait);
            pose.Rise = -1.4f * (float)Math.Exp(-into * 6f) * (float)Math.Sin(Math.Min(1f, into * 5f) * MathHelper.Pi);

            return pose;
        }

        // ============================================================
        // HIS SCENE: the draw, the X, the point
        // ============================================================

        /// <summary>The ticks of the draw: both hands to the hilts; held; drawn; out and crossed; held; parted; the guard.</summary>
        public const int DrawHands = 26;
        public const int DrawHold = 44;
        public const int DrawPulled = 60;
        public const int DrawFree = 64;
        public const int DrawCrossed = 76;
        public const int DrawShine = 80;
        public const int DrawParting = 128;
        public const int DrawParted = 146;
        public const int DrawTicks = 176;

        /// <summary>
        /// The draw, by request "from side to side in x formation (left to right)": both hands
        /// across to the hilts at the opposite hips, a breath, both swords drawn together and
        /// swept up until they cross in an X before his face, held there while the light gathers
        /// at the crossing, then parted: the Sun over the top and down ahead of him, the Grace
        /// over the top and down behind, two arcs that between them go all the way round him.
        /// Then the guard.
        /// </summary>
        public static readonly KnightTrack Draw = new KnightTrack(Rest)
            // Both hands to the hilts: the far hand across to the Grace at the near hip.
            .At(DrawHands).Lean(5f).Nod(10f).On(KnightRig.Sun, KnightRig.Grace).Legs(9f, -8f).Home()
            // A breath: he sinks a little, the head comes up.
            .At(DrawHold, Ease.SineInOut).Lean(7f).Nod(-4f).Rise(-2f).Legs(14f, -12f).Home()
            // Drawn, together: each grip forward along its scabbard as far as an arm goes.
            .At(DrawPulled, Ease.PolyIn, 2).Out(26f, 25f).Lean(4f).Nod(-6f).Home()
            // Free the moment the arms go on, and swept up through the front to cross before his face.
            .At(DrawFree, Ease.Linear).SwitchAt(0f).Drawn().Near(70f, 31f).Far(62f, 23f).Blades(-20f, -35f).Rise(-1f)
            .At(DrawCrossed, Ease.ExpOut).Near(72f, 31f).Far(64f, 22f).Blades(-124f, -57f).Lean(-3f).Nod(-8f).Rise(0f).Legs(16f, -14f)
            .At(DrawShine, Ease.SineOut).Near(73f).Blades(-123f, -58f)
            // Held while the light gathers at the crossing.
            .At(DrawParting, Ease.SineInOut).Near(75f).Blades(-121f, -59f).Lean(-4f).Nod(-10f)
            // Parted: the Sun over the top and down ahead, the Grace over the top and down behind.
            .At(DrawParted, Ease.PolyOut, 3).Near(50f, 32f).Far(-40f, 31f).Blades(30f, -200f).Lean(9f).Nod(4f).Legs(18f, -16f).Rise(-3f)
            // Into the guard.
            .At(DrawTicks, Ease.SineInOut).Near(89f, 21f).Far(-18f, KnightRig.ArmLong).Blades(-52f, -202f).Lean(7f).Nod(3f).Legs(16f, -15f).Rise(0f);

        /// <summary>The ticks of the point: the near arm comes up slowly, straight, until the Sun points at them; held.</summary>
        public const int PointRise = 70;
        public const int PointTicks = 150;

        /// <summary>
        /// One blade pointed slowly at them: the Sun brought up and out on a straight arm until it
        /// is level with them (<paramref name="at"/>: the way from his near shoulder to them, a
        /// blade angle facing right), held; the Grace left low behind.
        /// </summary>
        public static KnightTrack Point(float at)
        {
            float deg = MathHelper.Clamp(at, -0.7f, 0.5f) / Rad;

            // The arm along the blade: a limb's angle is from straight down, a blade's from straight
            // ahead turning down, so the arm that lies along a blade at deg is at 90 - deg.
            return new KnightTrack(Guard)
                .At(20f).Near(95f, 23f).Blades(-40f, 160f).Lean(5f).Nod(0f)
                .At(PointRise, Ease.SineInOut).Near(90f - deg, 31f).Sun(deg).Lean(2f).Nod(-2f).Legs(18f, -16f)
                .At(PointTicks, Ease.SineInOut).Near(90.5f - deg, 31.5f).Sun(deg).Lean(2.5f).Nod(-1f);
        }
    }
}
