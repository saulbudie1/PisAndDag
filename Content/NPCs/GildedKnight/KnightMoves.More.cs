using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    public static partial class KnightMoves
    {
        // ============================================================
        // THE GILDED WHEEL: blades out, round and round at them, a leap back, a cut
        // ============================================================

        public const int WheelWind = 18;
        public const int WheelSpun = 54;
        public const int WheelLanded = 76;
        public const int WheelCut = 84;

        /// <summary>
        /// Rellana's spin: both blades out level to his sides, a crouch, then twice round about
        /// himself going at them (seen side on, he turns edge on and past it and back, his blades
        /// with him); a leap back out of it, and a cut from the Sun as he lands that lets a
        /// crescent go.
        /// </summary>
        public static Move Wheel()
        {
            KnightTrack t = new KnightTrack(Guard)
                .At(WheelWind, Ease.SineOut).Near(80f, 31f).Far(-80f, 31f).SunTo(14f, 1).GraceTo(166f, 1).Lean(10f).Nod(4f).Legs(28f, -30f, 34f, 35f);

            // Twice round: the whole of him seen turning, eight steps of the turn.
            for (int k = 1; k <= 8; k++)
            {
                float share = k / 8f;
                float turn = (float)Math.Cos(share * MathHelper.TwoPi * 2f);
                t.At(WheelWind + (WheelSpun - WheelWind) * share, Ease.Linear).Turn(turn).Go(170f * share)
                 .Legs(28f + 6f * (float)Math.Sin(share * MathHelper.TwoPi * 4f), -30f, 34f, 35f);
            }

            t.At(WheelSpun + 6f, Ease.PolyOut, 2).SwitchAt(0f).Air(true).Turn(1f).Near(40f, 28f).Far(-60f, 30f).SunTo(60f, 1).GraceTo(150f, -1)
                 .Lean(-12f).Nod(-6f).Legs(40f, -20f, 30f, 34f).Rise(10f).Go(140f)
             .At(WheelLanded, Ease.PolyIn, 2).SwitchAt(1f).Air(false).Near(-40f, 27f).SunTo(150f, 1).Lean(10f).Legs(24f, -26f, 32f, 35f).Rise(-2f).Go(96f)
             .At(WheelCut, Ease.PolyIn, 2).Near(150f, 31f).SunTo(-64f, -1).Lean(-2f).Nod(-6f).Legs(26f, -22f, KnightRig.LegLong, KnightRig.LegLong).Rise(0f).Go(108f)
             .At(WheelCut + 6, Ease.ExpOut).Near(156f).SunTurn(-6f).Go(110f)
             .ToGuard(WheelCut + 30);

            return new Move("Wheel", t,
                new[]
                {
                    new Strike(KnightRig.Sun, WheelWind + 1, WheelSpun - 2, true),
                    new Strike(KnightRig.Grace, WheelWind + 1, WheelSpun - 2, true),
                    new Strike(KnightRig.Sun, WheelCut - 5, WheelCut + 1),
                },
                new[] { (2, Cue.Gather), (WheelWind, Cue.Wheel), (WheelSpun, Cue.Leap), (WheelLanded, Cue.Step), (WheelCut - 5, Cue.Swing), (WheelCut - 1, Cue.RisingCrescent) },
                WheelCut + 14);
        }

        // ============================================================
        // THE LANCET: the Grace drawn back to thrust, a line shown, a lance of light
        // ============================================================

        public const int LancetLine = 4;
        public const int LancetLock = 28;
        public const int LancetThrust = 38;

        /// <summary>
        /// The Grace drawn back low by his hip, its point at them; a thin line of light along the
        /// way it will go, following them, then still; the thrust, a long step into it, and the
        /// light goes on down the line the length of the field. <paramref name="aim"/>: the way at
        /// them, a blade angle facing right, as the line locked.
        /// </summary>
        public static Move Lancet(float aim)
        {
            float deg = MathHelper.Clamp(aim, -0.6f, 0.45f) / Rad;

            KnightTrack t = new KnightTrack(Guard)
                .At(16f, Ease.SineOut).Far(-62f, 22f).GraceTo(deg, 0).Near(60f, 20f).SunTo(-70f, 0).Lean(-8f).Nod(-2f).Legs(26f, -22f, KnightRig.LegLong, 35f).Rise(-1f)
                .At(LancetThrust - 4f, Ease.SineInOut).Far(-66f, 21f).Grace(deg).Lean(-10f).Rise(-2f)
                // The arm thrust out along the blade (a limb's angle along a blade at deg is 90 - deg).
                .At(LancetThrust, Ease.PolyIn, 3).Far(92f - deg, 33f).Lean(18f).Nod(6f).Legs(34f, -30f, 33f, KnightRig.LegLong).Rise(-3f).Go(40f)
                .At(LancetThrust + 4, Ease.ExpOut).Far(93f - deg, 34f).Go(44f)
                .At(LancetThrust + 24, Ease.SineInOut).Far(90f - deg, 31f).Lean(14f)
                .ToGuard(LancetThrust + 50);

            return new Move("Lancet", t,
                new[] { new Strike(KnightRig.Grace, LancetThrust - 3, LancetThrust + 2) },
                new[] { (2, Cue.Gather), (LancetLine, Cue.Line), (LancetLock, Cue.LineLocked), (LancetThrust - 3, Cue.Swing), (LancetThrust - 1, Cue.Lancet) },
                LancetThrust + 30);
        }

        // ============================================================
        // VOTIVE BLADES: blades of light in a row over him, loosed in turn
        // ============================================================

        public const int VotiveFirst = 8;
        public const int VotiveEvery = 6;
        public const int VotiveLoose = 58;
        public const int VotiveSpacing = 8;

        /// <summary>
        /// The Grace raised straight up, and one after another blades of light standing in the air
        /// in a row over and behind him (<paramref name="count"/>, three to five), each turning to
        /// point at them; then the Grace swept down at them and the blades loosed one after
        /// another.
        /// </summary>
        public static Move Votive(int count)
        {
            int loose = Math.Max(VotiveLoose, VotiveFirst + VotiveEvery * count + 18);
            int last = loose + VotiveSpacing * (count - 1);

            KnightTrack t = new KnightTrack(Guard)
                .At(22f, Ease.SineOut).Far(178f, 27f).GraceTo(-90f, -1).Near(70f, 20f).SunTo(-40f, 0).Lean(-5f).Nod(-8f).Legs(14f, -14f)
                .At(loose - 6f, Ease.SineInOut).Far(180f, 28f).Grace(-92f).Lean(-6f).Nod(-10f)
                .At(loose, Ease.PolyIn, 2).Far(96f, 31f).GraceTo(6f, 1).Lean(10f).Nod(4f).Legs(20f, -18f).Go(10f)
                .At(loose + 6, Ease.ExpOut).Far(100f).GraceTurn(6f).Go(12f)
                .ToGuard(last + 24);

            var cues = new System.Collections.Generic.List<(int, Cue)> { (2, Cue.Gather), (loose - 2, Cue.Swing) };

            for (int k = 0; k < count; k++)
            {
                cues.Add((VotiveFirst + VotiveEvery * k, Cue.Votive));
                cues.Add((loose + VotiveSpacing * k, Cue.Loose));
            }

            return new Move("Votive", t, null, cues.ToArray(), last + 8);
        }

        // ============================================================
        // TURNED ASIDE, AND GETTING ABOUT
        // ============================================================

        public const int ReelTicks = 60;

        /// <summary>
        /// Parried: the blade that was met thrown back and up, the whole of him rocked back and
        /// sliding a step and a half away, then gathered into his guard again; the second it
        /// takes, nothing lands on him (a ward of gold about him: the fight's).
        /// </summary>
        public static Move Reel(int blade)
        {
            KnightTrack t = new KnightTrack(Guard);

            if (blade == KnightRig.Sun)
                t.At(7f, Ease.ExpOut).Near(150f, 30f).SunTo(-130f, -1).Far(-40f, 30f).Lean(-16f).Nod(-14f).Legs(30f, -6f, 34f, KnightRig.LegLong).Go(-30f);
            else
                t.At(7f, Ease.ExpOut).Far(150f, 30f).GraceTo(-140f, -1).Near(60f, 22f).Lean(-16f).Nod(-14f).Legs(30f, -6f, 34f, KnightRig.LegLong).Go(-30f);

            t.At(22f, Ease.SineOut).Lean(-8f).Nod(-6f).Legs(20f, -12f).Go(-44f)
             .ToGuard(ReelTicks);

            return new Move("Reel", t, null, new[] { (0, Cue.Reel), (14, Cue.Step) }, ReelTicks);
        }

        /// <summary>
        /// Every third parry (the parry's own count, as for anything that stands on the ground): down
        /// on a knee for five seconds (by request), open to the riposte (PADStaggerNPC), the stun's
        /// stars over him. StunDown: down by then.
        /// </summary>
        public const int StunTicks = 300;
        public const int StunDown = 26;

        /// <summary>
        /// The stun: rocked back off the parry with both blades flung out, then down on one knee, the
        /// blades let hang with their points in the turf, his head hanging; held, swaying a little
        /// (GildedKnight.StunnedTick), until it wears off or the riposte wakes him.
        /// </summary>
        public static KnightTrack Stun => stun ??= new KnightTrack(Guard)
            .At(8f, Ease.ExpOut).Near(140f, 30f).SunTo(-120f, -1).Far(-60f, 29f).GraceTo(200f, 1).Lean(-16f).Nod(-16f).Legs(28f, -8f, 35f, KnightRig.LegLong).Go(-22f)
            .At(StunDown, Ease.SineInOut).Near(28f, 29f).SunTo(74f, 1).Far(-24f, 29f).GraceTo(108f, -1).Lean(18f).Nod(26f).Legs(37f, -53f, 30f, KnightRig.LegBent).Rise(0f).Go(-30f)
            .At(StunTicks, Ease.SineInOut).Lean(20f).Nod(30f);

        private static KnightTrack stun;

        public static readonly (int Tick, Cue What)[] StunCues = { (0, Cue.Reel), (StunDown - 4, Cue.Kneel) };

        public const int BackStepTicks = 34;

        /// <summary>A hop back out of reach: low, quick, both blades kept before him.</summary>
        public static Move BackStep()
        {
            KnightTrack t = new KnightTrack(Guard)
                .At(6f, Ease.SineOut).Legs(20f, -10f, 33f, 34f).Lean(12f).Rise(-2f)
                .At(10f, Ease.PolyOut, 2).SwitchAt(0f).Air(true).Legs(30f, -14f, 33f, 36f).Lean(-6f).Rise(12f).Go(-50f)
                .At(20f, Ease.PolyIn, 2).SwitchAt(1f).Air(false).Legs(10f, -26f, 34f, 33f).Lean(4f).Rise(-2f).Go(-92f)
                .ToGuard(BackStepTicks);

            return new Move("BackStep", t, null, new[] { (8, Cue.Leap), (20, Cue.Step) }, BackStepTicks);
        }

        public const int DashTicks = 22;

        /// <summary>A glide at them, low, the blades trailing, ten tiles in a breath: to close on them, and into whatever follows.</summary>
        public static Move Dash(float distance)
        {
            KnightTrack t = new KnightTrack(Guard)
                .At(5f, Ease.SineIn).Near(-30f, 26f).SunTo(160f, 1).Far(-50f, 29f).GraceTo(170f, 1).Lean(26f).Nod(10f).Legs(34f, -36f, 35f, 37f).Rise(-2f).Go(8f)
                .At(16f, Ease.PolyOut, 2).Lean(24f).Go(distance * 0.9f)
                .At(DashTicks, Ease.SineOut).Near(40f, 24f).SunTo(60f, -1).Far(-30f, 29f).Lean(14f).Nod(6f).Legs(24f, -20f, KnightRig.LegLong, KnightRig.LegLong).Rise(0f).Go(distance);

            return new Move("Dash", t, null, new[] { (4, Cue.Dash), (DashTicks - 2, Cue.Step) }, DashTicks);
        }

        public const int AboutTicks = 14;

        /// <summary>
        /// Turning about to face the other way: seen narrowing to edge on and opening again the
        /// other way. Whoever runs this has already turned the way he faces: at 0 this is him as
        /// he was, seen from his other side.
        /// </summary>
        public static KnightRig.Pose About(KnightRig.Pose pose, float share)
        {
            float t = Easing.Smoother(share);

            pose.Turn = -(float)Math.Cos(t * MathHelper.Pi);
            pose.Lean += -6f * Rad * (float)Math.Sin(t * MathHelper.Pi);

            return pose;
        }

        // ============================================================
        // HIS SECOND HALF: the great crescent, the procession, the crown, the long blade, the sun
        // ============================================================

        public const int GreatCharge = 46;
        public const int GreatSwept = 56;
        public const int GreatBurst = 80;

        /// <summary>
        /// The Sun held low behind him while it fills with fire (the ground under it glowing, motes
        /// of fire drawn in), then swept up through the whole front of him and on over his head
        /// behind: a crescent of fire the size of the arc, held a breath in the air, and burst.
        /// </summary>
        public static Move GreatCrescent()
        {
            // The Sun wound back over the top of him to lie low behind (not down through his front: that
            // would read as a blow), the Grace kept low behind him out of its way.
            KnightTrack t = new KnightTrack(Guard)
                .At(20f, Ease.SineInOut).Near(-64f, 27f).SunTo(160f, -1).Far(-30f, 28f).GraceTo(150f, 0).Lean(20f).Nod(8f).Legs(30f, -30f, 31f, 36f).Rise(0f)
                .At(GreatCharge, Ease.SineInOut).Near(-70f, 28f).Sun(-194f).Grace(148f).Lean(24f).Nod(10f).Rise(-3f)
                .At(GreatSwept, Ease.PolyIn, 2).Near(176f, 31f).SunTurn(-304f).Lean(-10f).Nod(-12f).Legs(24f, -24f, KnightRig.LegLong, KnightRig.LegLong).Rise(2f).Go(24f)
                .At(GreatSwept + 6, Ease.ExpOut).Near(180f).SunTurn(-10f).Lean(-12f).Go(26f)
                .At(GreatBurst, Ease.SineInOut).Near(170f, 29f).Lean(-8f)
                .ToGuard(GreatBurst + 42);

            return new Move("GreatCrescent", t,
                new[] { new Strike(KnightRig.Sun, GreatCharge + 1, GreatSwept, true) },
                new[] { (2, Cue.Gather), (GreatCharge, Cue.HeavySwing), (GreatSwept - 4, Cue.GreatCrescent), (GreatBurst, Cue.Burst) },
                GreatBurst + 16);
        }

        public const int ProcessionPlunge = 30;
        public const int ProcessionHold = 96;

        /// <summary>
        /// The Sun raised in both hands, the point down, and driven into the ground before him: fire
        /// runs out from it along the ground at them, burst after burst, and he stays down on a
        /// knee behind it until it has gone; then the blade is pulled free.
        /// </summary>
        public static Move Procession()
        {
            KnightTrack t = new KnightTrack(Guard)
                .At(22f, Ease.SineOut).Near(176f, 24f).SunTo(-92f, -1).Far(170f, 22f).GraceTo(-80f, -1).Lean(-6f).Nod(-10f).Legs(16f, -16f).Rise(2f)
                .At(ProcessionPlunge, Ease.PolyIn, 3).Near(80f, 25f).SunTo(92f, 1).Far(84f, 22f).GraceTo(100f, 1).Lean(26f).Nod(16f).Legs(56f, -60f, 29f, 30f).Rise(-2f).Go(10f)
                .At(ProcessionPlunge + 6, Ease.ExpOut).Lean(28f).Nod(18f)
                .At(ProcessionHold, Ease.SineInOut).Lean(26f).Nod(14f)
                .At(ProcessionHold + 16, Ease.SineOut).Near(110f, 28f).SunTo(-30f, -1).Lean(10f).Legs(24f, -24f, KnightRig.LegLong, KnightRig.LegLong).Rise(0f)
                .ToGuard(ProcessionHold + 40);

            return new Move("Procession", t,
                new[] { new Strike(KnightRig.Sun, ProcessionPlunge - 5, ProcessionPlunge, true) },
                new[] { (2, Cue.Gather), (ProcessionPlunge - 6, Cue.HeavySwing), (ProcessionPlunge, Cue.Procession), (ProcessionHold + 4, Cue.Swing) },
                ProcessionHold + 20);
        }

        public const int CrownFirst = 10;
        public const int CrownEvery = 7;
        public const int CrownLoose = 150;
        public const int CrownSpacing = 7;

        /// <summary>
        /// The crown of blades: the Grace raised, and blades of light coming one after another
        /// into a ring over his head (<paramref name="count"/>), turning together until they all
        /// point at them; held; then loosed one after another, fanned. He follows straight after them.
        /// </summary>
        public static Move Crown(int count)
        {
            int last = CrownLoose + CrownSpacing * (count - 1);

            KnightTrack t = new KnightTrack(Guard)
                .At(26f, Ease.SineOut).Far(178f, 27f).GraceTo(-90f, -1).Near(40f, 22f).SunTo(60f, 1).Lean(-6f).Nod(-12f).Legs(14f, -14f)
                .At(CrownLoose - 8f, Ease.SineInOut).Far(180f, 28f).Grace(-92f).Lean(-8f).Nod(-14f)
                .At(CrownLoose, Ease.PolyIn, 2).Far(100f, 31f).GraceTo(10f, 1).Lean(12f).Nod(4f).Legs(22f, -20f).Go(10f)
                .At(CrownLoose + 6, Ease.ExpOut).Far(104f).GraceTurn(6f).Go(12f)
                .ToGuard(last + 14);

            var cues = new System.Collections.Generic.List<(int, Cue)> { (2, Cue.Gather), (CrownLoose - 2, Cue.Swing) };

            for (int k = 0; k < count; k++)
            {
                cues.Add((CrownFirst + CrownEvery * k, Cue.Votive));
                cues.Add((CrownLoose + CrownSpacing * k, Cue.Loose));
            }

            return new Move("Crown", t, null, cues.ToArray(), last + 6);
        }

        public const int LongGrow = 18;
        public const int LongRaised = 46;
        public const int LongSlam = 54;
        public const int LongSweepFrom = 84;
        public const int LongSweepTo = 104;

        /// <summary>
        /// The long blade: the Grace raised straight up and light growing out of it until it is a
        /// blade the height of the sky; brought down ahead of him onto them, a pillar of light where
        /// it lands; then, from where it lies, swept level along the ground the length of the field
        /// (jumped over, or not); and it is only the Grace again.
        /// </summary>
        public static Move LongBlade()
        {
            KnightTrack t = new KnightTrack(Guard)
                .At(LongGrow, Ease.SineOut).Far(178f, 27f).GraceTo(-90f, -1).Near(60f, 20f).SunTo(-30f, 0).Lean(-4f).Nod(-10f).Legs(18f, -18f)
                .At(LongRaised, Ease.SineInOut).Far(180f, 28f).Grace(-91f).Lean(-8f).Nod(-12f).Rise(2f)
                .At(LongSlam, Ease.PolyIn, 3).Far(96f, 31f).GraceTo(12f, 1).Lean(24f).Nod(12f).Legs(34f, -30f, 32f, KnightRig.LegLong).Rise(-2f).Go(14f)
                .At(LongSlam + 6, Ease.ExpOut).Far(98f).Grace(14f).Lean(26f).Nod(14f)
                .At(LongSweepFrom, Ease.SineInOut).Far(90f, 30f).Grace(16f).Lean(20f)
                .At(LongSweepFrom + 4, Ease.SineIn).Far(-40f, 30f).GraceTo(168f, 1).Lean(4f).Legs(26f, -26f, 34f, 36f)
                .At(LongSweepTo, Ease.PolyIn, 2).Far(100f, 32f).GraceTo(-4f, -1).Lean(16f).Nod(6f).Go(30f)
                .At(LongSweepTo + 6, Ease.ExpOut).Far(104f).GraceTurn(-4f).Go(32f)
                .ToGuard(LongSweepTo + 40);

            return new Move("LongBlade", t,
                new[] { new Strike(KnightRig.Grace, LongRaised + 1, LongSlam, true), new Strike(KnightRig.Grace, LongSweepFrom + 5, LongSweepTo, true) },
                new[] { (2, Cue.Gather), (LongGrow, Cue.Grow), (LongRaised, Cue.HeavySwing), (LongSlam, Cue.Pillar), (LongSweepFrom + 4, Cue.HeavySwing), (LongSweepFrom + 8, Cue.Sweep) },
                LongSweepTo + 24);
        }

        public const int SunRaise = 40;
        public const int SunForm = 30;
        public const int SunHold = 170;
        public const int SunDown = 196;

        /// <summary>
        /// The gilded sun: the Sun raised straight up in his hand; a sun of fire forming over its
        /// point with two rings round it, light drawn up into it from the whole field; held while it
        /// swells; then the blade brought down at them and the sun with it: where it comes down, a
        /// pillar of fire to the sky.
        /// </summary>
        public static Move GildedSun()
        {
            KnightTrack t = new KnightTrack(Guard)
                .At(SunRaise, Ease.SineInOut).Near(180f, 28f).SunTo(-90f, -1).Far(-30f, 29f).GraceTo(170f, 0).Lean(-6f).Nod(-16f).Legs(16f, -16f).Rise(2f)
                .At(SunHold, Ease.SineInOut).Near(181f, 29f).Sun(-91f).Lean(-8f).Nod(-18f).Rise(3f)
                .At(SunDown, Ease.PolyIn, 2).Near(100f, 32f).SunTo(8f, 1).Lean(16f).Nod(6f).Legs(26f, -22f).Rise(0f).Go(12f)
                .At(SunDown + 8, Ease.ExpOut).Near(104f).SunTurn(6f).Go(14f)
                .ToGuard(SunDown + 50);

            return new Move("GildedSun", t, null,
                new[] { (2, Cue.Gather), (SunForm, Cue.SunForm), (SunHold, Cue.HeavySwing), (SunDown - 4, Cue.SunFall) },
                SunDown + 30);
        }

        // ============================================================
        // HIS CHANGE, AND HIS END
        // ============================================================

        public const int ChangeCross = 64;
        public const int ChangeRoar = 104;
        public const int ChangeSpin = 160;
        public const int ChangePyres = 214;
        public const int ChangeTicks = 270;

        /// <summary>
        /// At half his life: rocked back; the blades lowered, a breath; raised and crossed before his
        /// face as in his scene; a roar of gold out of him, and the blades take fire and light;
        /// round once about himself with fire and light wheeling after the blades; and fire bursts
        /// up out of the ground about him. Nothing lands on him through it.
        /// </summary>
        public static KnightTrack Change => change ??= BuildChange();

        private static KnightTrack change;

        // Built on first use, not as a field's first value: Guard is another file's field, and the
        // order the files' fields are given their values in is not the language's to promise.
        private static KnightTrack BuildChange() => new KnightTrack(Guard)
            .At(10f, Ease.ExpOut).Near(50f, 28f).SunTo(70f, 1).Far(-40f, 29f).GraceTo(130f, -1).Lean(-12f).Nod(-12f).Legs(26f, -4f, 35f, KnightRig.LegLong).Go(-24f)
            .At(40f, Ease.SineInOut).Near(20f, 28f).Sun(80f).Far(-10f, 29f).Grace(100f).Lean(14f).Nod(22f).Legs(18f, -14f).Rise(-3f).Go(-30f)
            .At(ChangeCross, Ease.PolyOut, 3).Near(72f, 31f).Far(64f, 22f).SunTo(-124f, -1).GraceTo(-57f, -1).Lean(-3f).Nod(-8f).Legs(16f, -14f).Rise(0f)
            .At(ChangeRoar, Ease.SineInOut).Near(74f).Blades(-122f, -59f).Lean(-6f).Nod(-12f)
            .At(ChangeRoar + 10, Ease.ExpOut).Near(130f, 31f).Far(-130f, 31f).SunTo(-60f, 1).GraceTo(-120f, -1).Lean(-12f).Nod(-20f).Rise(2f)
            .At(ChangeSpin, Ease.SineInOut).Near(132f).Far(-132f).Lean(-10f).Nod(-16f)
            .At(ChangeSpin + 6, Ease.SineIn).Near(96f, 31f).Far(-96f, 31f).SunTo(4f, 1).GraceTo(176f, -1).Lean(6f).Nod(2f).Legs(26f, -26f, 34f, 35f).Rise(0f)
            .At(ChangeSpin + 12, Ease.Linear).Turn(0f)
            .At(ChangeSpin + 24, Ease.Linear).Turn(-1f)
            .At(ChangeSpin + 36, Ease.Linear).Turn(0f)
            .At(ChangeSpin + 48, Ease.SineOut).Turn(1f)
            .At(ChangePyres, Ease.PolyIn, 2).Near(70f, 30f).Far(60f, 30f).SunTo(84f, 1).GraceTo(80f, 1).Lean(24f).Nod(12f).Legs(40f, -44f, 30f, 34f).Rise(-2f)
            .At(ChangePyres + 8, Ease.ExpOut).Lean(26f)
            .ToGuard(ChangeTicks);

        public static readonly (int Tick, Cue What)[] ChangeCues =
        {
            (0, Cue.Reel), (40, Cue.Step), (ChangeCross - 8, Cue.Swing), (ChangeCross, Cue.Cross), (ChangeRoar, Cue.Roar), (ChangeRoar + 4, Cue.Ignite),
            (ChangeSpin + 6, Cue.Wheel), (ChangePyres - 4, Cue.HeavySwing), (ChangePyres, Cue.Pyres),
        };

        /// <summary>
        /// His end's moments: three times as long as they first were (by request: "slower, like last
        /// three times"). The Grace planted, both knees down, his head bowed; the gold smouldering off
        /// him (EndFade); come apart into golden grains (EndApart), the Sun on the ground after him
        /// (EndSunApart); gone (EndTicks), the grains drifting off.
        /// </summary>
        public const int EndPlant = 120;
        public const int EndKnees = 240;
        public const int EndBow = 360;
        public const int EndFade = 450;
        public const int EndApart = 720;
        public const int EndSunApart = 750;
        public const int EndTicks = 990;

        /// <summary>
        /// His end, by request "falls down knees first and perish into light": the last blow rocks
        /// him; the Sun falls from his hand; he stumbles and drives the Grace into the ground before
        /// him; sinks onto it, both knees down, both hands on its hilt; his head bows; then, as the
        /// Guide perishes but in gold (by request), the gold smoulders off him and he comes apart
        /// into golden grains that hold his shape a moment and drift away (the fight's).
        /// </summary>
        public static KnightTrack End => end ??= BuildEnd();

        private static KnightTrack end;

        private static KnightTrack BuildEnd() => new KnightTrack(Guard)
            .At(30f, Ease.ExpOut).Near(40f, 28f).SunTo(70f, 1).Far(-50f, 29f).GraceTo(140f, -1).Lean(-14f).Nod(-16f).Legs(26f, -6f, 35f, KnightRig.LegLong).Go(-18f)
            // The Sun let fall (the fight lays it on the ground and draws it there).
            .At(84f, Ease.SineInOut).Hold(KnightRig.Dropped, KnightRig.InFar).Near(10f, 29f).Far(80f, 24f).GraceTo(-70f, -1).Lean(10f).Nod(14f).Legs(16f, -18f).Go(-20f)
            // The Grace driven into the ground before him.
            .At(EndPlant, Ease.PolyIn, 2).Near(20f, 29f).Far(90f, 26f).GraceTo(88f, 1).Lean(20f).Nod(18f).Legs(30f, -20f, 34f, KnightRig.LegLong).Rise(-4f).Go(-14f)
            // One knee, then the other, both hands on its hilt.
            .At(EndKnees - 48, Ease.SineIn).Far(84f, 24f).Grace(90f).On(KnightRig.Grace, -1).Lean(14f).Nod(20f).Legs(37f, -53f, 30f, KnightRig.LegBent).Rise(0f)
            .At(EndKnees, Ease.SineOut).Far(80f, 22f).Lean(10f).Nod(24f).Legs(-50f, -53f, KnightRig.LegBent, KnightRig.LegBent)
            // His head bowed over it.
            .At(EndBow, Ease.SineInOut).Far(82f, 21f).Lean(14f).Nod(34f)
            .At(EndTicks, Ease.SineInOut).Lean(16f).Nod(38f);

        /// <summary>His end's moments, all without a sound (by request): only the sight of it.</summary>
        public static readonly (int Tick, Cue What)[] EndCues =
        {
            (0, Cue.Reel), (72, Cue.Step), (EndPlant, Cue.Slam), (EndKnees - 30, Cue.Kneel), (EndFade, Cue.Fade),
        };
    }
}
