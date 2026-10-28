using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Base;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>What happens at a tick of a move, for the fight and the drawing to answer: a sound, a light, a shot let go.</summary>
    public enum Cue : byte
    {
        None,

        /// <summary>A foot comes down: the armour's jangle, a little dust.</summary>
        Step,

        /// <summary>A blade starts its cut: the swing's sound.</summary>
        Swing,

        /// <summary>A heavy blade starts its cut.</summary>
        HeavySwing,

        /// <summary>Both blades into the ground before him: sparks, a ring of dust, the screen shaken.</summary>
        Slam,

        /// <summary>He leaves the ground.</summary>
        Leap,

        /// <summary>He comes down: the ground struck, a shockwave either way.</summary>
        Land,

        /// <summary>Light drawn into a blade: it brightens.</summary>
        Gather,

        /// <summary>A crescent of light let go off the Grace, along the ground.</summary>
        Crescent,

        /// <summary>A crescent let go off the Sun, rising.</summary>
        RisingCrescent,

        /// <summary>The line the thrust will go along is shown.</summary>
        Line,

        /// <summary>The line stops following them.</summary>
        LineLocked,

        /// <summary>The thrust: the lancet of light along the line.</summary>
        Lancet,

        /// <summary>A votive blade appears behind him.</summary>
        Votive,

        /// <summary>The next votive blade is loosed.</summary>
        Loose,

        /// <summary>The wheel begins: the ring of light round him.</summary>
        Wheel,

        /// <summary>A glint along a blade (the draw, the point).</summary>
        Glint,

        /// <summary>The swords cross before his face (his scene).</summary>
        Cross,

        /// <summary>A sword leaves its scabbard (his scene).</summary>
        Shing,

        /// <summary>He is turned aside: the blade thrown back, a step away.</summary>
        Reel,

        /// <summary>The Sun swept through its great arc: the great crescent let go.</summary>
        GreatCrescent,

        /// <summary>The great crescent bursts.</summary>
        Burst,

        /// <summary>The Sun driven into the ground: the procession of fire begins.</summary>
        Procession,

        /// <summary>The long blade of light grows out of the Grace.</summary>
        Grow,

        /// <summary>The long blade comes down: a pillar of light where it lands.</summary>
        Pillar,

        /// <summary>The long blade swept level along the ground.</summary>
        Sweep,

        /// <summary>The gilded sun forms over him.</summary>
        SunForm,

        /// <summary>The gilded sun comes down.</summary>
        SunFall,

        /// <summary>A roar: the ring of light out of him.</summary>
        Roar,

        /// <summary>The blades take fire and light (his change).</summary>
        Ignite,

        /// <summary>Fire bursts up out of the ground about him (his change, the pyres).</summary>
        Pyres,

        /// <summary>A dash: the ground left in a blur.</summary>
        Dash,

        /// <summary>He drops to a knee (his end).</summary>
        Kneel,

        /// <summary>Light begins to lift off him (his end).</summary>
        Fade,
    }

    /// <summary>A blade's cut within a move: which blade, the ticks it can hit in, and whether it is a heavy one.</summary>
    public readonly struct Strike
    {
        public readonly int Blade;
        public readonly int From;
        public readonly int To;
        public readonly bool Heavy;

        public Strike(int blade, int from, int to, bool heavy = false)
        {
            Blade = blade;
            From = from;
            To = to;
            Heavy = heavy;
        }

        public bool Live(float tick) => tick >= From && tick <= To;
    }

    /// <summary>One of his moves as the fight plays it: the movement, how long, its cuts, and its cues.</summary>
    public sealed class Move
    {
        public readonly string Name;
        public readonly KnightTrack Track;
        public readonly int Ticks;
        public readonly Strike[] Strikes;
        public readonly (int Tick, Cue What)[] Cues;

        /// <summary>From this tick on he may be made to stop and do something else (the move's work is done; what is left is settling).</summary>
        public readonly int Done;

        public Move(string name, KnightTrack track, Strike[] strikes, (int, Cue)[] cues, int done = -1)
        {
            Name = name;
            Track = track;
            Ticks = (int)Math.Ceiling(track.Length);
            Strikes = strikes ?? Array.Empty<Strike>();
            Cues = cues ?? Array.Empty<(int, Cue)>();
            Done = done < 0 ? Ticks : done;
        }

        /// <summary>The cues at this tick.</summary>
        public IEnumerable<Cue> At(int tick)
        {
            foreach ((int Tick, Cue What) cue in Cues)
            {
                if (cue.Tick == tick)
                    yield return cue.What;
            }
        }
    }

    public static partial class KnightMoves
    {
        /// <summary>Back into the guard from wherever a move left him, over so many ticks, the blades the short way.</summary>
        private static KnightTrack ToGuard(this KnightTrack t, float tick)
        {
            return t.At(tick, Ease.SineInOut).Near(89f, 21f).Far(-18f, KnightRig.ArmLong)
                .SunTo(-52f, 0).GraceTo(158f, 0).Lean(7f).Nod(3f).Legs(16f, -15f, KnightRig.LegLong, KnightRig.LegLong).Rise(0f).Turn(1f);
        }

        // ============================================================
        // THE TWIN CUT: the blades in turn, a step with each
        // ============================================================

        /// <summary>
        /// The twin cut, as Rellana's combos go: the blades in turn, in rhythm, a step with each.
        /// The Sun over the top and down; the Grace up from behind across him; the Sun level from
        /// behind; and the last, both raised high, held (or not: <paramref name="delayed"/>) and
        /// brought down together into the ground before him. <paramref name="strikes"/>: 3 ends
        /// after the level cut, 4 with the last.
        /// </summary>
        public static Move TwinCut(int strikes, bool delayed)
        {
            KnightTrack t = new KnightTrack(Guard)
                // ---- 1: the Sun over the top and down
                .At(16f, Ease.SineOut).Near(160f, 26f).Sun(-152f).Far(-28f).Grace(165f).Lean(-6f).Nod(-6f).Legs(10f, -12f)
                .At(22f, Ease.PolyIn, 3).Near(68f, 31f).Sun(38f).Lean(14f).Nod(8f).Legs(26f, -20f).Go(16f)
                .At(28f, Ease.ExpOut).Near(60f, 30f).Sun(48f).Lean(12f).Nod(6f).Go(18f)
                // ---- 2: the Grace up from behind and across
                .At(38f, Ease.SineOut).Far(-48f, 30f).Grace(162f).Near(48f, 28f).Sun(62f).Lean(9f).Nod(4f).Legs(14f, -16f)
                .At(44f, Ease.PolyIn, 2).Far(128f, 31f).Grace(-58f).Lean(1f).Nod(-2f).Legs(24f, -22f).Go(32f)
                .At(50f, Ease.ExpOut).Far(132f, 30f).Grace(-64f).Go(34f);

            var cut = new List<Strike>
            {
                new Strike(KnightRig.Sun, 17, 23),
                new Strike(KnightRig.Grace, 39, 45),
            };
            var cues = new List<(int, Cue)> { (16, Cue.Swing), (21, Cue.Step), (38, Cue.Swing), (43, Cue.Step) };

            // ---- 3: the Sun level from behind
            t.At(60f, Ease.SineOut).Near(-92f, 27f).Sun(178f).Far(110f, 28f).Grace(-40f).Lean(3f).Nod(2f).Legs(12f, -14f)
             .At(66f, Ease.PolyIn, 2).Near(98f, 31f).Sun(8f).Lean(13f).Nod(5f).Legs(24f, -20f).Go(48f)
             .At(72f, Ease.ExpOut).Near(104f, 30f).Sun(16f).Go(50f);

            cut.Add(new Strike(KnightRig.Sun, 61, 67));
            cues.Add((60, Cue.Swing));
            cues.Add((65, Cue.Step));

            int end;

            if (strikes >= 4)
            {
                // ---- 4: both high, held, and down together into the ground
                int raise = 94;
                int hold = delayed ? 116 : raise;
                int hit = hold + 7;

                t.At(raise, Ease.SineInOut).Near(172f, 25f).Far(176f, 25f).Sun(-118f).Grace(-106f).Lean(-10f).Nod(-10f).Legs(14f, -14f).Rise(2f);

                if (delayed)
                    t.At(hold, Ease.SineInOut).Near(174f).Far(178f).Sun(-121f).Grace(-109f).Lean(-12f).Nod(-12f).Rise(3f);

                t.At(hit, Ease.PolyIn, 3).Near(66f, 31f).Far(56f, 31f).Sun(70f).Grace(64f).Lean(22f).Nod(12f).Legs(30f, -24f, 33f).Rise(0f).Go(64f)
                 .At(hit + 4, Ease.ExpOut).Near(64f).Far(54f).Sun(74f).Grace(68f).Lean(24f).Go(66f)
                 .At(hit + 30, Ease.SineInOut).Near(60f, 30f).Far(50f, 30f).Sun(80f).Grace(74f).Lean(20f)
                 .At(hit + 52, Ease.SineInOut).Near(89f, 21f).Far(-18f, KnightRig.ArmLong).Blades(-52f, 158f).Lean(7f).Nod(3f).Legs(16f, -15f, KnightRig.LegLong).Rise(0f);

                cut.Add(new Strike(KnightRig.Sun, hold + 1, hit, true));
                cut.Add(new Strike(KnightRig.Grace, hold + 1, hit, true));
                cues.Add((hold, Cue.HeavySwing));
                cues.Add((hit - 1, Cue.Slam));
                end = hit + 52;
            }
            else
            {
                t.At(96f, Ease.SineInOut).Near(89f, 21f).Far(-18f, KnightRig.ArmLong).Blades(-52f, 158f).Lean(7f).Nod(3f).Legs(16f, -15f);
                end = 96;
            }

            return new Move(strikes >= 4 ? (delayed ? "TwinCut4Delayed" : "TwinCut4") : "TwinCut3", t, cut.ToArray(), cues.ToArray(), end - 20);
        }

        // ============================================================
        // THE LEAP: up, both blades over his head, and down onto them
        // ============================================================

        /// <summary>The leap's ticks: crouched; off the ground; in the air for <paramref name="air"/>; struck; up again.</summary>
        public const int LeapCrouch = 22;
        public const int LeapRecover = 40;

        /// <summary>
        /// The leap: down into a crouch with both blades drawn back low; off the ground; both
        /// raised over his head as he goes up; brought down as he falls, into the ground where
        /// he lands, a shockwave out either way along it. The arc itself (how far, how high) is
        /// the fight's, for where they are: this is how he holds himself along it, by the tick.
        /// </summary>
        public static Move LeapSlam(int air)
        {
            int up = LeapCrouch;
            int down = up + air;

            // The blades: carried back over his head to lie behind him level as he crouches; up behind
            // him as he rises; cocked over his head at the top; over the top and down into the
            // ground where he lands.
            KnightTrack t = new KnightTrack(Guard)
                .At(up, Ease.SineOut).Near(-70f, 28f).Far(-58f, 28f).SunTo(185f, -1).GraceTo(195f, 1).Lean(18f).Nod(6f).Legs(26f, -22f, 29f, 31f).Rise(0f)
                .At(up + 6, Ease.PolyOut, 2).SwitchAt(0f).Air(true).Near(150f, 28f).Far(140f, 28f).SunTurn(45f).GraceTurn(40f).Lean(2f).Nod(-6f).Legs(40f, -10f, 30f, 36f).Rise(4f)
                .At(down - 8, Ease.SineInOut).Near(178f, 26f).Far(184f, 26f).SunTurn(20f).GraceTurn(20f).Lean(-8f).Nod(-10f).Legs(64f, 4f, 29f, 33f).Rise(6f)
                .At(down, Ease.PolyIn, 3).SwitchAt(1f).Air(false).Near(80f, 30f).Far(70f, 30f).SunTurn(188f).GraceTurn(182f).Lean(28f).Nod(14f).Legs(42f, -46f, 29f, 34f).Rise(-1f)
                .At(down + 6, Ease.ExpOut).Near(76f).Far(68f).SunTurn(8f).GraceTurn(10f).Lean(30f).Nod(16f)
                .At(down + 26, Ease.SineInOut).Near(70f, 30f).Far(64f, 30f).SunTurn(-6f).GraceTurn(-6f).Lean(24f).Legs(30f, -32f, 31f, 37f)
                .At(down + LeapRecover, Ease.SineInOut).Near(89f, 21f).Far(-18f, KnightRig.ArmLong).SunTo(-52f, -1).GraceTo(158f, 1).Lean(7f).Nod(3f).Legs(16f, -15f, KnightRig.LegLong, KnightRig.LegLong).Rise(0f);

            // The air pose switches at once when he leaves the ground and when he lands.
            return new Move("LeapSlam", t,
                new[] { new Strike(KnightRig.Sun, down - 7, down, true), new Strike(KnightRig.Grace, down - 7, down, true) },
                new[] { (up, Cue.Leap), (down - 8, Cue.HeavySwing), (down, Cue.Land) },
                down + 24);
        }

        // ============================================================
        // CRESCENTS: from range, the Grace level along the ground, the Sun rising after
        // ============================================================

        public const int CrescentLet = 26;
        public const int RisingLet = 50;

        /// <summary>
        /// The Grace drawn back behind him at shoulder height while it fills with light, then swept
        /// level through the front: a crescent let go along the ground at them. With
        /// <paramref name="twice"/>, the Sun follows, low behind and up through the front: a
        /// second crescent, rising.
        /// </summary>
        public static Move Crescents(bool twice)
        {
            KnightTrack t = new KnightTrack(Guard)
                .At(20f, Ease.SineOut).Far(-96f, 29f).Grace(186f).Near(70f, 24f).Sun(-30f).Lean(-4f).Nod(0f).Legs(12f, -18f)
                .At(CrescentLet - 3f, Ease.SineInOut).Far(-100f, 30f).Grace(190f).Lean(-6f)
                .At(CrescentLet + 3f, Ease.PolyIn, 2).Far(98f, 32f).Grace(4f).Lean(12f).Nod(4f).Legs(24f, -18f).Go(12f)
                .At(CrescentLet + 10f, Ease.ExpOut).Far(104f, 31f).Grace(10f).Go(14f);

            var cut = new List<Strike> { new Strike(KnightRig.Grace, CrescentLet - 2, CrescentLet + 3) };
            var cues = new List<(int, Cue)> { (2, Cue.Gather), (CrescentLet - 2, Cue.Swing), (CrescentLet, Cue.Crescent) };
            int end;

            if (twice)
            {
                t.At(RisingLet - 5f, Ease.SineOut).Near(-34f, 28f).Sun(150f).Lean(4f).Nod(4f).Legs(14f, -16f)
                 .At(RisingLet + 2f, Ease.PolyIn, 2).Near(150f, 31f).Sun(-62f).Lean(-4f).Nod(-6f).Legs(22f, -20f).Go(26f)
                 .At(RisingLet + 10f, Ease.ExpOut).Near(156f, 30f).Sun(-70f).Go(28f)
                 .At(RisingLet + 34f, Ease.SineInOut).Near(89f, 21f).Far(-18f, KnightRig.ArmLong).Blades(-52f, 158f).Lean(7f).Nod(3f).Legs(16f, -15f);

                cut.Add(new Strike(KnightRig.Sun, RisingLet - 4, RisingLet + 2));
                cues.Add((RisingLet - 5, Cue.Swing));
                cues.Add((RisingLet, Cue.RisingCrescent));
                end = RisingLet + 34;
            }
            else
            {
                t.At(CrescentLet + 36f, Ease.SineInOut).Near(89f, 21f).Far(-18f, KnightRig.ArmLong).Blades(-52f, 158f).Lean(7f).Nod(3f).Legs(16f, -15f);
                end = CrescentLet + 36;
            }

            return new Move(twice ? "Crescents2" : "Crescents", t, cut.ToArray(), cues.ToArray(), end - 16);
        }
    }
}
