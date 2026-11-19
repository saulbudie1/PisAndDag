using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its last stand -------------------------------------------------------------------------------------------------
    //
    //     DIVE      44   it plunges, down and under the player, to one side
    //     LEAP   84..60  up over the player and down the other side: a parabola, its ends twenty-four tiles under them
    //                    and thirty-four out, its top seventeen tiles over them at first and lower each time, to eight;
    //                    each quicker than the last
    //     TURN      20   under the ground, round, to come back the other way
    //     ... leaps and turns till ten seconds are up; then its end.
    //
    // ---- its end ---------------------------------------------------------------------------------------------------------
    //
    //     WIND      46   the camera goes to it, racing round under the ground, far out; the player is held, and nothing
    //                    touches them from here on
    //     RUSH      60   it bursts up out of the ground and comes at them at full speed, and stops short, eight tiles off
    //                    to the side and a little over them, its head on them
    //     BREAK  ~320    explosions along its body, each sooner after the last (42 ticks, then 0.85 of that each time, down to
    //                    two), and a rattle of them at the last; black smoke out of every crease, more and more; the
    //                    Megashark's sparks; its head shaking, its light failing
    //     BEAT      34   still: only the smoke
    //     BUZZ      84   a loud buzzer; its eye flashing in its colour
    //     CHAIN  ~230    it blows up a segment at a time from its tail to its head, each into its pieces (one every two ticks
    //                    far off, one every four near its head); its head last, and biggest; the camera walks home; its loot
    //
    // Skipped (E held, single player): on to CHAIN, run fast.
    //
    // All of it a little slower than it was (by request: "Make everything a bit slower"), and its whole body following
    // its head along its way (Destroyer.Path), parabola and all.

    public partial class Destroyer
    {
        private const int StandDive = 0;
        private const int StandLeap = 1;
        private const int StandTurn = 2;

        /// <summary>How long its last stand lasts (by request: "after 10 seconds").</summary>
        private const int StandTicks = 600;

        private const int StandDiveTicks = 44;
        private const int LeapTicksMost = 84;
        private const int LeapTicksLeast = 60;
        private const int LeapQuicker = 3;
        private const int TurnBackTicks = 20;

        /// <summary>A leap's ends: this far out to either side of the player, this far under them (pixels); its top this far over them at first, lower by so much each leap, to the least (tiles).</summary>
        private const float Span = 34f * 16f;
        private const float Below = 24f * 16f;
        private const float HighFirst = 17f;
        private const float HighLower = 1.5f;
        private const float HighLeast = 8f;

        private const int FinWind = 0;
        private const int FinRush = 1;
        private const int FinBreak = 2;
        private const int FinBeat = 3;
        private const int FinBuzz = 4;
        private const int FinChain = 5;

        private const int WindTicks = 46;
        private const int RushTicks = 60;

        /// <summary>Where its wind-up ends, under the ground, out from the player (pixels).</summary>
        private const float WindOut = 48f * 16f;
        private const float WindDeep = 16f * 16f;
        private const int BeatTicks = 34;
        private const int BuzzTicks = 84;

        /// <summary>How far short of the player it stops, to their side and a little over them, its head on them.</summary>
        private const float Short = 130f;

        /// <summary>Segments this near its head blow up one every three ticks in its chain; the rest one a tick.</summary>
        private const int NearHead = 28;

        /// <summary>Its last stand's clock; its leaps so far; which way the last went.</summary>
        private int standClock, leap, leapSide;

        /// <summary>The curve its head is on now (a cubic; its rush is the first three, a quadratic), and how long it takes.</summary>
        private Vector2 curveA, curveB, curveC, curveD;
        private int curveTicks;

        /// <summary>What its explosions are worked out from (the same on every machine), and whether its end was skipped (its chain run fast).</summary>
        private int seed;
        private bool fast;

        /// <summary>Ticks into BREAK its explosions come, and how long BREAK lasts; ticks into CHAIN each segment blows up (from its tail), and its head.</summary>
        private readonly List<int> breakTimes = new();
        private int breakTicks;
        private int[] chainTimes = Array.Empty<int>();
        private int headTime;

        /// <summary>The last segment to blow up, for the camera.</summary>
        private Vector2 chainFront;

        /// <summary>This machine's player was in its end's shot: the camera is walked home for them.</summary>
        private bool endWatching;

        internal bool Ended => ending || headGone;

        public static readonly Terraria.Audio.SoundStyle Buzzer = new("PisAndDag/Content/Sounds/Mech/DestroyerBuzzer")
        {
            Volume = 1f,
            MaxInstances = 1,
        };

        // ---- its last stand ---------------------------------------------------------------------------------------------

        private void BeginStand(NPC npc)
        {
            npc.TargetClosest(false);
            Player p = Target(npc);
            Vector2 at = p?.Center ?? npc.Center + new Vector2(0f, -300f);

            leap = 0;
            leapSide = npc.Center.X <= at.X ? 1 : -1;
            standClock = 0;

            Vector2 first = new(at.X - leapSide * Span, at.Y + Below);
            SetCurve(npc.Center, npc.Center + new Vector2(0f, 8f * 16f) + npc.velocity * 4f, first + new Vector2(0f, 10f * 16f), first, StandDiveTicks);

            npc.damage = npc.defDamage;
            Go(npc, Scene.Stand, StandDive);

            if (Authority)
                ClearShots();
        }

        /// <summary>What it has out, gone: its lasers and bolts in flight, its beams, its probes (its body is breaking down). The server's.</summary>
        private static void ClearShots()
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile shot = Main.projectile[i];

                if (shot.active && (shot.hostile && shot.type == ProjectileID.DeathLaser || DestroyerShots.IsOurs(shot.type)))
                    shot.Kill();
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC probe = Main.npc[i];

                if (probe.active && probe.type == NPCID.Probe)
                    probe.StrikeInstantKill();
            }
        }

        private void SetCurve(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int ticks)
        {
            curveA = a;
            curveB = b;
            curveC = c;
            curveD = d;
            curveTicks = Math.Max(1, ticks);
        }

        /// <summary>A leap from where it is over the player, to as far out the other side: a parabola (a cubic whose middle is the top).</summary>
        private void SetLeap(NPC npc)
        {
            Player p = Target(npc);
            Vector2 at = p?.Center ?? npc.Center + new Vector2(-leapSide * Span, -Below);
            Vector2 from = npc.Center;
            int side = from.X < at.X ? 1 : -1;

            Vector2 to = new(at.X + side * Span, at.Y + Below);
            float top = at.Y - Math.Max(HighLeast, HighFirst - HighLower * leap) * 16f;
            float lean = (to.X - from.X) * 0.18f;
            float lift = (from.Y + to.Y - 2f * top) / 6f;

            SetCurve(from, new Vector2(from.X + lean, top - lift), new Vector2(to.X - lean, top - lift), to,
                Math.Max(LeapTicksLeast, LeapTicksMost - LeapQuicker * leap));

            leap++;
            leapSide = side;
        }

        /// <summary>Round under the ground at a leap's end, to come back up the other way.</summary>
        private void SetTurnBack(NPC npc)
        {
            Vector2 from = npc.Center;
            Vector2 going = (curveD - curveC).SafeNormalize(Vector2.UnitY);
            Vector2 to = from + new Vector2(leapSide * 4f * 16f, 0f);

            SetCurve(from, from + going * 7f * 16f, to + new Vector2(0f, 7f * 16f), to, TurnBackTicks);
        }

        private void RunStand(NPC npc, bool entered)
        {
            float u = Math.Min(1f, (timer + 1f) / curveTicks);
            Move(npc, Bezier(curveA, curveB, curveC, curveD, u));

            Breach(npc, 1f);
            Decay(npc, 0.15f + 0.35f * standClock / StandTicks);
            HeadGlow(npc, 0.8f);

            if (entered && step == StandDive)
                Sound(SoundID.Item14 with { Pitch = -0.6f, Volume = 0.7f }, npc.Center, 3);

            standClock++;

            if (!Authority || timer < curveTicks - 1)
                return;

            if (standClock >= StandTicks)
            {
                BeginFinale(npc);
                return;
            }

            if (step == StandLeap)
            {
                SetTurnBack(npc);
                Next(npc, StandTurn);
            }
            else
            {
                SetLeap(npc);
                Next(npc, StandLeap);
            }
        }

        // ---- its end ----------------------------------------------------------------------------------------------------

        /// <summary>Its end begins: round under the ground, far out on the side it is on, to come back at the player.</summary>
        private void BeginFinale(NPC npc)
        {
            npc.TargetClosest(false);
            Player p = Target(npc);
            Vector2 at = p?.Center ?? npc.Center;
            Vector2 from = npc.Center;
            int side = from.X >= at.X ? 1 : -1;
            Vector2 going = npc.velocity.SafeNormalize(new Vector2(side, 1f).SafeNormalize(Vector2.UnitY));
            Vector2 round = new(at.X + side * WindOut, at.Y + WindDeep);

            seed = Main.rand.Next(int.MaxValue);
            fast = false;
            SetCurve(from, from + going * 24f * 16f, round + new Vector2(side * 22f * 16f, 16f * 16f), round, WindTicks);
            Go(npc, Scene.Finale, FinWind);
        }

        /// <summary>Its rush: up out of the ground, steeply, then round and straight at the player, to stop short of them.</summary>
        private void SetRush(NPC npc)
        {
            npc.TargetClosest(false);
            Player p = Target(npc);
            Vector2 from = npc.Center;
            Vector2 at = p?.Center ?? from;
            int side = from.X >= at.X ? 1 : -1;
            Vector2 outward = new Vector2(side, -0.5f).SafeNormalize(Vector2.UnitX);
            Vector2 to = at + outward * Short;

            // Bent so it comes up steeply and ends heading straight at them: it faces them by its own way, not by turning.
            Vector2 bend = to + outward * Math.Max(160f, Vector2.Distance(from, to) * 0.5f);

            SetCurve(from, bend, to, to, RushTicks);
        }

        /// <summary>When its explosions come, worked out the same way on every machine.</summary>
        private void Schedule()
        {
            breakTimes.Clear();
            float gap = 42f;
            int t = 10;

            while (gap >= 2f && t < 300)
            {
                breakTimes.Add(t);
                t += Math.Max(2, (int)Math.Round(gap));
                gap *= 0.85f;
            }

            for (int k = 0; k < 7; k++)
            {
                breakTimes.Add(t);
                t += 3;
            }

            breakTicks = t + 14;
        }

        /// <summary>When each segment blows up in its chain, counted from its tail; and its head.</summary>
        private void ScheduleChain()
        {
            int n = chain.Count;
            chainTimes = new int[n];
            float t = 0f;

            for (int fromTail = 0; fromTail < n; fromTail++)
            {
                chainTimes[fromTail] = (int)t;
                int fromHead = n - 1 - fromTail;
                t += fast ? 1f : fromHead >= NearHead ? 2f : 4f;
            }

            headTime = (int)t + (fast ? 6 : 12);
        }

        private void RunFinale(NPC npc, bool entered)
        {
            int t = timer;
            Player p = Target(npc);
            Vector2 at = p?.Center ?? curveC;
            bool watching = Watching(npc.Center) || Watching(at);
            Vector2 face = at - npc.Center;

            if (watching)
                endWatching = true;

            // E held in the corner (single player): on to its chain, run fast.
            if (watching && step < FinChain)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    // Not yet at the player: put there, where it would have stopped.
                    if (step < FinBreak)
                    {
                        SetRush(npc);
                        npc.Center = curveC;
                        npc.velocity = Vector2.Zero;
                    }

                    fast = true;
                    Next(npc, FinChain);
                    return;
                }
            }

            switch (step)
            {
                case FinWind:
                    {
                        float u = Math.Min(1f, (t + 1f) / curveTicks);
                        Move(npc, Bezier(curveA, curveB, curveC, curveD, u));

                        Breach(npc, 1.2f);
                        Decay(npc, 0.5f);
                        HeadGlow(npc, 1f);

                        if (entered)
                            Sound(SoundID.WormDig with { Pitch = -0.5f, Volume = 0.9f }, npc.Center, 3);

                        // The camera goes to it as it races (the player held from the first tick).
                        if (watching)
                            Shot(npc.Center, Smooth(t / 22f), MathHelper.Lerp(1f, 1.08f, Smooth(t / (float)WindTicks)), spare: true);

                        if (Authority && t >= curveTicks - 1)
                        {
                            SetRush(npc);
                            Next(npc, FinRush);
                        }

                        break;
                    }

                case FinRush:
                    {
                        float s = Math.Min(1f, (t + 1f) / RushTicks);
                        float u = 1f - (float)Math.Pow(1f - s, 4.5f);
                        Vector2 to = Bezier(curveA, curveB, curveC, u);

                        // Along its way, which ends heading at the player.
                        Move(npc, to);

                        Breach(npc, 1.6f);
                        Decay(npc, 0.5f);
                        HeadGlow(npc, 1f);

                        if (entered)
                        {
                            Sound(SoundID.WormDig with { Pitch = -0.2f, Volume = 1f }, npc.Center, 3);
                            Sound(SoundID.Item74 with { Pitch = -0.5f, Volume = 0.8f }, npc.Center, 3);
                        }

                        if (watching)
                            Shot(Vector2.Lerp(npc.Center, at, 0.45f * Smooth(s * 1.6f)), 1f, MathHelper.Lerp(1.08f, 1.25f, Smooth(s)), spare: true);

                        if (Authority && t >= RushTicks)
                            Next(npc, FinBreak);

                        break;
                    }

                case FinBreak:
                    {
                        if (entered || breakTimes.Count == 0)
                            Schedule();

                        float grow = Math.Min(1f, t / (float)breakTicks);
                        Move(npc, curveC, face);
                        shake = 1f + 5f * grow * grow;

                        for (int k = 0; k < breakTimes.Count; k++)
                        {
                            if (breakTimes[k] == t)
                                BreakAt(npc, k, grow);
                        }

                        Decay(npc, 0.5f + 1.5f * grow);
                        HeadGlow(npc, Main.rand.NextFloat(0.4f, 1.2f) * (1f - 0.4f * grow));

                        if (watching)
                            Shot(Vector2.Lerp(npc.Center, at, 0.35f), 1f, MathHelper.Lerp(1.25f, 1.15f, grow), spare: true);

                        if (Authority && t >= breakTicks)
                            Next(npc, FinBeat);

                        break;
                    }

                case FinBeat:
                    {
                        // Its shaking settles (still where it is), then it is still.
                        Move(npc, curveC, face);
                        shake = 6f * (1f - Smooth(t / 14f));
                        Decay(npc, 1.2f, sparks: false);
                        HeadGlow(npc, 0.25f);

                        if (watching)
                            Shot(Vector2.Lerp(npc.Center, at, 0.35f), 1f, 1.15f, spare: true);

                        if (Authority && t >= BeatTicks)
                            Next(npc, FinBuzz);

                        break;
                    }

                case FinBuzz:
                    {
                        if (entered && !Main.dedServ)
                        {
                            SoundEngine.PlaySound(Buzzer, Here(npc));
                            Cinematic.Flash(DestroyerArt.Teal, 0.12f);
                        }

                        Move(npc, curveC, face);
                        shake = 4f * Smooth(t / 8f);
                        Decay(npc, 1.2f);

                        // Its eye flashing red, on and off, as the buzzer sounds.
                        HeadGlow(npc, t / 5 % 2 == 0 ? 2.2f : 0.15f, flare: t % 10 == 0);

                        if (watching)
                            Shot(Vector2.Lerp(npc.Center, at, 0.35f), 1f, 1.15f + 0.03f * (float)Math.Sin(t * 0.5f), spare: true);

                        if (Authority && t >= BuzzTicks)
                            Next(npc, FinChain);

                        break;
                    }

                case FinChain:
                    {
                        if (entered || chainTimes.Length != chain.Count)
                            ScheduleChain();

                        Move(npc, curveC, face);
                        shake = 4f - Smooth(t / 20f);
                        Decay(npc, 0.8f, sparks: false);

                        int n = chain.Count;

                        for (int k = 0; k < n; k++)
                        {
                            int fromTail = n - 1 - k;

                            if (fromTail >= chainTimes.Length || t < chainTimes[fromTail])
                                continue;

                            NPC segment = Main.npc[chain[k]];

                            if (!segment.TryGetGlobalNPC(out DestroyerPart part) || part.Exploded)
                                continue;

                            part.Exploded = true;
                            SegmentBurst(segment, k);
                            chainFront = segment.Center;
                        }

                        if (t >= headTime && !headGone)
                        {
                            HeadBurst(npc);
                            headGone = true;

                            if (endWatching && !Main.dedServ)
                                PisPlayer.PanHome(npc.Center, 110);

                            endWatching = false;
                        }

                        if (!headGone && watching)
                        {
                            Vector2 front = Vector2.Distance(chainFront, npc.Center) < 900f ? chainFront : npc.Center;
                            Shot(Vector2.Lerp(Vector2.Lerp(npc.Center, at, 0.3f), front, 0.3f), 1f, 1.05f, spare: true);
                        }

                        if (Authority && t >= headTime + 2)
                            End(npc);

                        break;
                    }
            }
        }

        /// <summary>One of its explosions: at a segment near its head (the nearer the likelier), chosen the same on every machine.</summary>
        private void BreakAt(NPC npc, int k, float grow)
        {
            if (chain.Count == 0)
                return;

            Random rng = new(seed + k * 7919);
            int reach = Math.Min(chain.Count, 34);
            int index = (int)(Math.Pow(rng.NextDouble(), 1.4) * reach);
            NPC segment = Main.npc[chain[Math.Clamp(index, 0, chain.Count - 1)]];
            Vector2 at = segment.Center + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 24f;

            Explosion(at, 0.7f + 0.5f * grow, k % 3 == 0);
        }
    }
}
