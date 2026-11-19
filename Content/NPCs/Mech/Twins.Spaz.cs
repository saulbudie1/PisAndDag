using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- the Spazmatism's own, in Legends ------------------------------------------------------------------------------------
    //
    // Its flames give Industrial Flames, its gas and spears Chlorine Poisoning (each 30 life a second, 3 seconds).
    //
    // FIRST FORM
    //   (its cursed flames)   vanilla's, burning in the new fire's green.
    //   WHIRL    "Spin in a circle not in place but fast to suck in the player". It takes the ring twenty-four tiles out from
    //            where the player stood (GATHER 36), then five seconds round it at twenty-two pixels a tick, its eye on the
    //            middle, the ring closing to two and a half tiles (slowly, then fast), everyone within seventy tiles drawn to
    //            the middle harder and harder. Out through the ring between its passes, or it closes on you.
    //   SWEEP    "Dashes horizontally ... full screen length, there mark 7 points on the path and from each point shoot 2 balls
    //            opposite eachother and perpendicular to the path. between dash moves a bit, intervall of 1.5 sec". Three
    //            times, a second and a half apart: a row laid across the player (sixty tiles either side, up to five tiles
    //            over or under them), its seven points marked; it takes the row's end (50 ticks), then crosses it in 40,
    //            throwing a cursed flame up and one down from each point as it passes. Each begins where the last ended.
    //
    // SECOND FORM
    //   (its flamethrower)    vanilla's, its flames going half as far again (by request), in the new fire's green.
    //   SPIRAL   "Draw out the fibonacci sequance path of the rectangle created using the 3 points: target player, spazmatism
    //            and retinazer ... the circle that encapsulate that trinagle ... that circle's diameter as the width of the
    //            rectangle ... dashes that path in 3 sec (2 sec planning) ... 15 points, each point shoots 2 gas projectiles
    //            perpendicular to the tangent". The smallest circle round the three; a golden rectangle as wide as it (56 to
    //            120 tiles) about its middle; the golden spiral in it (eight quarter turns, each smaller by the golden ratio),
    //            turned whichever of four ways winds in nearest the player. PLAN 120: the rectangle, its squares and the
    //            spiral drawn out, its fifteen points marked, while it takes the spiral's outer end. RUN 180: down it to its
    //            eye at an even pace, two clouds of chlorine thrown out square to its way at each point.
    //   INHALE   "Stand in place and sucks in for 5 seconds, the closer the easier to suck then breathe out 3 waves even,
    //            random, even spaced out Toxic Spear". BRACE 30; SUCK 300: still, its mouth following the player slowly,
    //            everyone within eighty tiles drawn to its mouth, six times harder up close than far off; BREATHE: three
    //            waves of Toxic Spears half a second apart (nine evenly over 110 degrees; ten at random; seven evenly over
    //            150, faster).

    public partial class Twins
    {
        // ---- WHIRL --------------------------------------------------------------------------------------------------------------

        private const int WhirlGather = 0, WhirlSpin = 1, WhirlOut = 2;
        private const int WhirlGatherTicks = 36, WhirlTicks = 300, WhirlOutTicks = 30;

        /// <summary>The ring it begins on and ends on, its speed round it, and how far its pull reaches.</summary>
        private const float WhirlFar = 24f * 16f;
        private const float WhirlNear = 2.5f * 16f;
        private const float WhirlSpeed = 22f;
        private const float WhirlReach = 70f * 16f;

        /// <summary>
        /// Its pull as it begins and what is added by its end, pixels a tick each tick. A player holding against it gives
        /// 0.28 (vanilla's run: its slowing and its push), one standing 0.2: so it takes the still from a second in, and
        /// those who fight it only in its last two seconds.
        /// </summary>
        private const float WhirlPull = 0.16f;
        private const float WhirlPullMore = 0.2f;

        /// <summary>The most it moves in a tick taking its ring.</summary>
        private const float WhirlCatchUp = 56f;

        private void RunWhirl(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case WhirlGather:
                    {
                        if (entered && Authority)
                        {
                            markA = p.Center;
                            angle = (npc.Center - markA).ToRotation();
                        }

                        Hover(npc, markA + angle.ToRotationVector2() * WhirlFar, 32f, 0.22f);
                        Turn(npc, markA - npc.Center, 0.2f);

                        if (entered)
                            WhirlSound(npc, 0f);

                        if (Authority && t >= WhirlGatherTicks)
                            Next(npc, WhirlSpin);

                        break;
                    }

                case WhirlSpin:
                    {
                        float u = t / (float)WhirlTicks;
                        float r = MathHelper.Lerp(WhirlFar, WhirlNear, MathF.Pow(u, 1.5f));
                        angle += side * Math.Min(0.2f, WhirlSpeed / r);

                        Vector2 was = npc.Center;
                        Move(npc, markA + angle.ToRotationVector2() * r);

                        // Onto its ring over a few ticks if it was not there yet, never in one.
                        float jump = npc.velocity.Length();

                        if (jump > WhirlCatchUp)
                            npc.velocity *= WhirlCatchUp / jump;

                        npc.rotation = npc.rotation.AngleLerp(LookAlong(markA - npc.Center), 0.5f);

                        Pull(markA, WhirlPull + WhirlPullMore * u, WhirlReach);
                        WhirlFx(npc, was, r, u);

                        if (t % 45 == 20)
                            WhirlSound(npc, u);

                        if (Authority && t >= WhirlTicks)
                            Next(npc, WhirlOut);

                        break;
                    }

                default:
                    {
                        npc.velocity *= 0.92f;
                        Turn(npc, p.Center - npc.Center, 0.12f);

                        if (Authority && t >= WhirlOutTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        private static void WhirlSound(NPC npc, float u)
        {
            if (!Main.dedServ)
                Sound(SoundID.DD2_BetsyWindAttack with { Volume = 0.8f + 0.2f * u, Pitch = -0.4f + 0.7f * u }, npc.Center, 6);
        }

        /// <summary>Its whirl seen: its green fire along its way, the air drawn round and in, gas pulled off toward the middle.</summary>
        private void WhirlFx(NPC npc, Vector2 was, float r, float u)
        {
            if (Main.dedServ)
                return;

            PADFire.Trail(was, npc.Center + npc.velocity, 44f, FirePalette.Toxic, 0.55f, smokes: false);

            if (!PADFire.Near(markA, 900f))
                return;

            for (int i = 0; i < 2; i++)
            {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 out_ = a.ToRotationVector2();
                Vector2 at = markA + out_ * r * Main.rand.NextFloat(0.5f, 1.7f);
                Vector2 round = new Vector2(-out_.Y, out_.X) * side;
                PADParticleHandler.SpawnParticle(new WindStreak(at, round * Main.rand.NextFloat(7f, 12f) - out_ * (2f + 4f * u), Main.rand.NextFloat(40f, 90f), Main.rand.NextFloat(1.5f, 3f), Main.rand.Next(16, 26)));
            }

            if (Main.rand.NextBool(3))
            {
                Vector2 out_ = Main.rand.NextVector2Unit();
                PADFire.Gas(markA + out_ * r * Main.rand.NextFloat(0.4f, 1.2f), -out_ * (2f + 3f * u) + new Vector2(-out_.Y, out_.X) * side * 3f, Main.rand.NextFloat(24f, 40f), Main.rand.Next(26, 40), 0.6f);
            }
        }

        // ---- SWEEP --------------------------------------------------------------------------------------------------------------

        private const int SweepCycle = 90, SweepSet = 50, SweepDashes = 3, SweepMarks = 7, SweepRestTicks = 20;

        /// <summary>The tick of a round its dash begins on, kept inside the round whatever it is set to.</summary>
        private static readonly int SweepSetAt = Math.Min(SweepSet, Math.Max(1, SweepCycle - 8));

        /// <summary>Half a row's length (a full screen's width in all), and how fast its cursed flames leave it.</summary>
        private const float SweepHalf = 60f * 16f;
        private const float SweepBall = 8.5f;

        private void RunSweep(NPC npc, bool entered, Player p)
        {
            int t = timer;

            if (step != 0)
            {
                npc.velocity *= 0.88f;
                Turn(npc, p.Center - npc.Center, 0.12f);

                if (Authority && t >= SweepRestTicks)
                    EndMove(npc);

                return;
            }

            int c = t % SweepCycle, k = t / SweepCycle;

            // A row laid: across the player, from the side it is on.
            if (Authority && count <= k)
            {
                side = k == 0 ? (npc.Center.X <= p.Center.X ? 1 : -1) : -side;
                float row = p.Center.Y + Main.rand.NextFloat(-5f, 5f) * 16f;
                markA = new Vector2(p.Center.X - side * SweepHalf, row);
                markB = new Vector2(p.Center.X + side * SweepHalf, row);
                count = k + 1;
                npc.netUpdate = true;
            }

            bool laid = count > k;
            Vector2 way = (markB - markA).SafeNormalize(Vector2.UnitX);

            if (c < SweepSetAt || !laid)
            {
                // To its row's end ("between dash moves a bit").
                if (laid)
                    Hover(npc, markA, 36f, 0.25f);
                else
                    npc.velocity *= 0.9f;

                Turn(npc, way, 0.2f);
                return;
            }

            float span_ = Math.Max(1, SweepCycle - SweepSetAt);
            float before = (c - SweepSetAt) / span_, now = (c - SweepSetAt + 1f) / span_;
            Vector2 was = npc.Center;
            Move(npc, Vector2.Lerp(markA, markB, now));
            npc.rotation = LookAlong(way);

            if (c == SweepSetAt && !Main.dedServ)
            {
                Sound(SoundID.Roar with { Volume = 0.9f, Pitch = 0.25f }, npc.Center, 6);
                Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f, Pitch = 0.1f }, npc.Center, 6);
            }

            if (!Main.dedServ)
                PADFire.Trail(was, npc.Center + npc.velocity, 50f, FirePalette.Toxic, 0.5f, smokes: false);

            // A cursed flame up and one down from each point it passes.
            for (int i = 0; i < SweepMarks; i++)
            {
                float at = (i + 0.5f) / SweepMarks;

                if (at <= before || at > now)
                    continue;

                Vector2 point = Vector2.Lerp(markA, markB, at);

                if (Authority)
                {
                    TwinsShots.CurseBall(npc, point + new Vector2(0f, -30f), new Vector2(0f, -SweepBall));
                    TwinsShots.CurseBall(npc, point + new Vector2(0f, 30f), new Vector2(0f, SweepBall));
                }

                if (!Main.dedServ)
                {
                    PADFire.Puff(point, -Vector2.UnitY, 36f, FirePalette.Toxic, 0.5f, 0.3f);
                    PADFire.Puff(point, Vector2.UnitY, 36f, FirePalette.Toxic, 0.5f, 0.3f);
                    Sound(SoundID.Item20 with { Volume = 0.6f, Pitch = 0.2f }, point, 6);
                }
            }

            if (Authority && t >= SweepCycle * SweepDashes - 1)
                Next(npc, 1);
        }

        // ---- SPIRAL -------------------------------------------------------------------------------------------------------------

        private const int SpiralPlan = 0, SpiralRun = 1, SpiralRest = 2;
        private const int SpiralPlanTicks = 120, SpiralRunTicks = 180, SpiralRestTicks = 24;
        private const int SpiralArcs = 8, SpiralPoints = 15;

        private const float Phi = 1.6180340f;

        /// <summary>The least and the most the rectangle may be wide; how fast its gas leaves it.</summary>
        private const float SpiralLeast = 56f * 16f;
        private const float SpiralMost = 120f * 16f;
        private const float SpiralGas = 5.5f;

        /// <summary>How long the whole spiral is, in a golden rectangle this wide.</summary>
        private static float SpiralLength(float wide)
        {
            float r = wide / Phi, sum = 0f;

            for (int k = 0; k < SpiralArcs; k++)
            {
                sum += r;
                r /= Phi;
            }

            return MathHelper.PiOver2 * sum;
        }

        /// <summary>
        /// One of the spiral's quarter turns, in the rectangle's own space (its corner the origin, <paramref name="wide"/> by
        /// wide over the golden ratio, unturned): its middle (a corner of the square it crosses), its radius, and the angle
        /// it begins at (it sweeps a quarter turn on from there). The first crosses the great square on the left, from the
        /// rectangle's lower left corner up to its top; each after is smaller by the golden ratio, its middle on the line
        /// from the last one's end to the last one's middle.
        /// </summary>
        private static void SpiralArc(float wide, int which, out Vector2 middle, out float radius, out float from)
        {
            float high = wide / Phi;
            middle = new Vector2(high, high);
            radius = high;
            from = MathHelper.Pi;

            for (int k = 0; k < which; k++)
            {
                Vector2 end = middle + (from + MathHelper.PiOver2).ToRotationVector2() * radius;
                middle = end + (middle - end) / Phi;
                radius /= Phi;
                from += MathHelper.PiOver2;
            }
        }

        /// <summary>
        /// The place this far along the spiral, and the way it runs there, in the world: the rectangle's corner at
        /// <paramref name="origin"/>, mirrored left to right (<paramref name="flips"/> bit 0) and top to bottom (bit 1).
        /// </summary>
        private static void SpiralAt(Vector2 origin, float wide, int flips, float along, out Vector2 at, out Vector2 tangent)
        {
            float high = wide / Phi;
            Vector2 local = Vector2.Zero;
            tangent = Vector2.UnitY;

            for (int k = 0; k < SpiralArcs; k++)
            {
                SpiralArc(wide, k, out Vector2 middle, out float radius, out float from);
                float length = MathHelper.PiOver2 * radius;

                if (along > length && k < SpiralArcs - 1)
                {
                    along -= length;
                    continue;
                }

                float a = from + MathHelper.Clamp(along / length, 0f, 1f) * MathHelper.PiOver2;
                local = middle + a.ToRotationVector2() * radius;
                tangent = new Vector2(-MathF.Sin(a), MathF.Cos(a));
                break;
            }

            if ((flips & 1) != 0)
            {
                local.X = wide - local.X;
                tangent.X = -tangent.X;
            }

            if ((flips & 2) != 0)
            {
                local.Y = high - local.Y;
                tangent.Y = -tangent.Y;
            }

            at = origin + local;
        }

        /// <summary>A place in the rectangle's own space, in the world.</summary>
        private static Vector2 SpiralPlace(Vector2 origin, float wide, int flips, Vector2 local)
        {
            if ((flips & 1) != 0)
                local.X = wide - local.X;

            if ((flips & 2) != 0)
                local.Y = wide / Phi - local.Y;

            return origin + local;
        }

        /// <summary>The smallest circle that holds three places: the one on their longest side if that holds the third, or the one through all three.</summary>
        private static void Enclose(Vector2 a, Vector2 b, Vector2 c, out Vector2 middle, out float radius)
        {
            float ab = Vector2.DistanceSquared(a, b), bc = Vector2.DistanceSquared(b, c), ca = Vector2.DistanceSquared(c, a);
            Vector2 p = a, q = b;
            float longest = ab;

            if (bc > longest)
            {
                p = b;
                q = c;
                longest = bc;
            }

            if (ca > longest)
            {
                p = c;
                q = a;
                longest = ca;
            }

            // From a, so the numbers are small enough to be exact.
            double bx = b.X - a.X, by = b.Y - a.Y, cx = c.X - a.X, cy = c.Y - a.Y;
            double d = 2.0 * (bx * cy - by * cx);

            if (longest >= ab + bc + ca - longest || Math.Abs(d) < 1.0)
            {
                middle = (p + q) * 0.5f;
                radius = MathF.Sqrt(longest) * 0.5f;
                return;
            }

            double b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
            middle = a + new Vector2((float)((b2 * cy - c2 * by) / d), (float)((c2 * bx - b2 * cx) / d));
            radius = Vector2.Distance(middle, a);
        }

        /// <summary>The spiral laid (the server's): the circle, the rectangle, and which way round winds in nearest the player.</summary>
        private void PlanSpiral(NPC npc, Player p, NPC other)
        {
            Vector2 third = other != null && other.active ? other.Center : npc.Center + (p.Center - npc.Center).RotatedBy(MathHelper.PiOver2);
            Enclose(p.Center, npc.Center, third, out Vector2 middle, out float radius);

            float wide = MathHelper.Clamp(radius * 2f, SpiralLeast, SpiralMost);
            Vector2 origin = middle - new Vector2(wide, wide / Phi) * 0.5f;
            float length = SpiralLength(wide);
            int best = 0;
            float nearest = float.MaxValue;

            for (int flips = 0; flips < 4; flips++)
            {
                SpiralAt(origin, wide, flips, length, out Vector2 eye, out _);
                float far = Vector2.DistanceSquared(eye, p.Center);

                if (far < nearest)
                {
                    nearest = far;
                    best = flips;
                }
            }

            markA = origin;
            span = wide;
            count = best;
        }

        private void RunSpiral(NPC npc, bool entered, Player p, NPC other)
        {
            int t = timer;

            if (step == SpiralPlan && entered && Authority)
                PlanSpiral(npc, p, other);

            if (span < 1f)
            {
                // Not laid here yet (a client, a tick early).
                npc.velocity *= 0.9f;
                return;
            }

            float length = SpiralLength(span);

            switch (step)
            {
                case SpiralPlan:
                    {
                        SpiralAt(markA, span, count, 0f, out Vector2 begin, out Vector2 tangent);
                        Hover(npc, begin, 40f, 0.2f);
                        Turn(npc, tangent, 0.12f);

                        if (entered && !Main.dedServ)
                            Sound(SoundID.Mech with { Volume = 1f, Pitch = -0.2f }, npc.Center, 6);

                        if (Authority && t >= SpiralPlanTicks)
                            Next(npc, SpiralRun);

                        break;
                    }

                case SpiralRun:
                    {
                        float before = length * t / SpiralRunTicks, now = length * Math.Min(1f, (t + 1f) / SpiralRunTicks);
                        SpiralAt(markA, span, count, now, out Vector2 at, out Vector2 tangent);
                        Vector2 was = npc.Center;
                        Move(npc, at);
                        npc.rotation = npc.rotation.AngleLerp(LookAlong(tangent), 0.5f);

                        if (entered && !Main.dedServ)
                        {
                            Sound(SoundID.Roar with { Volume = 1f, Pitch = 0.1f }, npc.Center, 6);
                            Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.9f, Pitch = -0.2f }, npc.Center, 6);
                        }

                        if (!Main.dedServ)
                            PADFire.Trail(was, at, 48f, FirePalette.Toxic, 0.55f, smokes: false);

                        // Two clouds of gas out square to its way at each of its fifteen points.
                        for (int i = 0; i < SpiralPoints; i++)
                        {
                            float mark = length * (i + 0.5f) / SpiralPoints;

                            if (mark <= before || mark > now)
                                continue;

                            SpiralAt(markA, span, count, mark, out Vector2 point, out Vector2 along);
                            Vector2 square = new(-along.Y, along.X);

                            if (Authority)
                            {
                                TwinsShots.GasShot(npc, point + square * 30f, square * SpiralGas);
                                TwinsShots.GasShot(npc, point - square * 30f, -square * SpiralGas);
                            }

                            if (!Main.dedServ)
                                Sound(SoundID.Item34 with { Volume = 0.6f, Pitch = 0.5f }, point, 6);
                        }

                        if (Authority && t + 1 >= SpiralRunTicks)
                            Next(npc, SpiralRest);

                        break;
                    }

                default:
                    {
                        npc.velocity *= 0.86f;
                        Turn(npc, p.Center - npc.Center, 0.12f);

                        if (Authority && t >= SpiralRestTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        // ---- INHALE -------------------------------------------------------------------------------------------------------------

        private const int InBrace = 0, InSuck = 1, InBreathe = 2, InRest = 3;
        private const int BraceTicks = 30, SuckTicks = 300, BreatheTicks = 100, InRestTicks = 30;

        /// <summary>How far its breath draws; its pull far off and up close (pixels a tick, each tick), and what counts as far.</summary>
        private const float SuckReach = 80f * 16f;
        private const float SuckWeak = 0.1f;
        private const float SuckStrong = 0.6f;
        private const float SuckFar = 900f;

        private void RunInhale(NPC npc, bool entered, Player p)
        {
            int t = timer;
            Vector2 mouth = Front(npc, 62f);

            switch (step)
            {
                case InBrace:
                    {
                        npc.velocity *= 0.85f;
                        Turn(npc, p.Center - EyeCentre(npc), 0.15f);
                        shake = 1f + 1.5f * t / BraceTicks;

                        if (Authority && t >= BraceTicks)
                            Next(npc, InSuck);

                        break;
                    }

                case InSuck:
                    {
                        npc.velocity *= 0.8f;
                        Turn(npc, p.Center - EyeCentre(npc), 0.035f);
                        shake = 0.8f;

                        // "the closer the easier to suck"
                        if (!Main.dedServ)
                        {
                            Player me = Main.LocalPlayer;
                            float far = Vector2.Distance(me.Center, mouth);

                            if (me.active && !me.dead && far > 20f && far < SuckReach)
                                me.velocity += (mouth - me.Center) / far * MathHelper.Lerp(SuckStrong, SuckWeak, Math.Min(1f, far / SuckFar));
                        }

                        InhaleFx(npc, mouth, t);

                        if (Authority && t >= SuckTicks)
                            Next(npc, InBreathe);

                        break;
                    }

                case InBreathe:
                    {
                        npc.velocity *= 0.85f;
                        Turn(npc, p.Center - EyeCentre(npc), 0.1f);

                        if (t == 8 || t == 38 || t == 68)
                        {
                            int wave = (t - 8) / 30;

                            if (Authority)
                                SpearWave(npc, p, mouth, wave);

                            // Its breath out throws it back.
                            npc.velocity -= Facing(npc) * 6f;

                            if (!Main.dedServ)
                            {
                                PADFire.Puff(mouth, Facing(npc), 62f, FirePalette.Toxic, 1.3f, 0.9f);
                                Sound(SoundID.DD2_OgreSpit with { Volume = 1f, Pitch = -0.2f + 0.15f * wave }, mouth, 6);
                                Sound(SoundID.Item34 with { Volume = 0.8f, Pitch = 0.3f }, mouth, 6);
                                Shake(mouth, 7f, 12);
                            }
                        }

                        if (Authority && t >= BreatheTicks)
                            Next(npc, InRest);

                        break;
                    }

                default:
                    {
                        npc.velocity *= 0.9f;
                        Turn(npc, p.Center - npc.Center, 0.1f);

                        if (Authority && t >= InRestTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>
        /// A wave of Toxic Spears out of its mouth at the player (the server's): evenly; at random; evenly and spaced out
        /// (by request: "3 waves even, random, even spaced out").
        /// </summary>
        private static void SpearWave(NPC npc, Player p, Vector2 mouth, int wave)
        {
            float aim = (p.Center - mouth).ToRotation();

            switch (wave)
            {
                case 0:
                    for (int i = 0; i < 9; i++)
                        TwinsShots.Spear(npc, mouth, (aim + MathHelper.ToRadians(110f) * (i / 8f - 0.5f)).ToRotationVector2() * 11f);

                    break;

                case 1:
                    for (int i = 0; i < 10; i++)
                        TwinsShots.Spear(npc, mouth, (aim + Main.rand.NextFloat(-1.22f, 1.22f)).ToRotationVector2() * Main.rand.NextFloat(9f, 14f));

                    break;

                default:
                    for (int i = 0; i < 7; i++)
                        TwinsShots.Spear(npc, mouth, (aim + MathHelper.ToRadians(150f) * (i / 6f - 0.5f)).ToRotationVector2() * 13f);

                    break;
            }
        }

        /// <summary>Its breath in, seen and heard: the air drawn to its mouth from all before it, gas with it.</summary>
        private static void InhaleFx(NPC npc, Vector2 mouth, int t)
        {
            if (Main.dedServ)
                return;

            if (t % 62 == 0)
                Sound(SoundID.DD2_DrakinBreathIn with { Volume = 1f, Pitch = -0.4f }, mouth, 6);

            if (!PADFire.Near(mouth, 900f))
                return;

            Vector2 way = Facing(npc);

            for (int i = 0; i < 2; i++)
            {
                Vector2 from = mouth + way.RotatedByRandom(1.1f) * Main.rand.NextFloat(180f, 720f);
                Vector2 to = (mouth - from).SafeNormalize(Vector2.UnitY);
                PADParticleHandler.SpawnParticle(new WindStreak(from, to * Main.rand.NextFloat(10f, 18f), Main.rand.NextFloat(40f, 100f), Main.rand.NextFloat(1.5f, 3f), Main.rand.Next(16, 26)));
            }

            if (Main.rand.NextBool(2))
            {
                Vector2 from = mouth + way.RotatedByRandom(0.9f) * Main.rand.NextFloat(90f, 380f);
                PADFire.Gas(from, (mouth - from) * 0.045f, Main.rand.NextFloat(20f, 34f), Main.rand.Next(18, 28), 0.6f);
            }

            Lighting.AddLight(mouth, 0.2f, 0.7f, 0.2f);
        }

        // ---- what its attacks warn with ---------------------------------------------------------------------------------------

        private void DrawSpazMarks(NPC npc, SpriteBatch batch)
        {
            Color tint = TwinsArt.ToxicBright;

            if (act == Act.Sweep && step == 0)
            {
                int c = timer % SweepCycle, k = timer / SweepCycle;

                if (count <= k)
                    return;

                // Its row, and its seven points, each with a stroke up and one down: in over ten ticks, flashing, gone
                // as it crosses.
                float alpha = c < SweepSetAt ? Smooth(c / 10f) : 1f - (c - SweepSetAt) / 14f;

                if (alpha <= 0.01f)
                    return;

                float ready = Math.Min(1f, c / (float)SweepSetAt);
                bool on = Main.GameUpdateCount % 8 < 5 || c >= SweepSetAt - 8;
                float lit = alpha * (on ? 1f : 0.35f);
                DestroyerArt.DrawLine(batch, markA, markB, 2f, DestroyerArt.Lit(tint * (0.22f * lit)));

                for (int i = 0; i < SweepMarks; i++)
                {
                    Vector2 point = Vector2.Lerp(markA, markB, (i + 0.5f) / SweepMarks);
                    DestroyerArt.DrawGlow(batch, point, 26f + 14f * ready, DestroyerArt.Lit(tint * (0.7f * lit)), hotCore: true);
                    DrawMark(batch, point, -MathHelper.PiOver2, 300f, ready, lit, tint);
                    DrawMark(batch, point, MathHelper.PiOver2, 300f, ready, lit, tint);
                }

                return;
            }

            if (act != Act.Spiral || span < 1f || step == SpiralRest)
                return;

            float shown = step == SpiralPlan ? Smooth(timer / 70f) : 1f;
            float fade = step == SpiralPlan ? Smooth(timer / 12f) : 1f - Smooth((timer - SpiralRunTicks + 30f) / 30f);
            float gone = step == SpiralRun ? timer / (float)SpiralRunTicks : 0f;
            float length = SpiralLength(span);
            float high = span / Phi;
            Color faint = DestroyerArt.Lit(tint * (0.16f * fade));

            // The rectangle, and its squares (each arc's two radii are two sides of the square it crosses): drawn out as it plans.
            Vector2 c0 = SpiralPlace(markA, span, count, Vector2.Zero), c1 = SpiralPlace(markA, span, count, new Vector2(span, 0f));
            Vector2 c2 = SpiralPlace(markA, span, count, new Vector2(span, high)), c3 = SpiralPlace(markA, span, count, new Vector2(0f, high));
            DestroyerArt.DrawLine(batch, c0, c1, 2f, faint);
            DestroyerArt.DrawLine(batch, c1, c2, 2f, faint);
            DestroyerArt.DrawLine(batch, c2, c3, 2f, faint);
            DestroyerArt.DrawLine(batch, c3, c0, 2f, faint);

            for (int k = 0; k < SpiralArcs; k++)
            {
                if (k / (float)SpiralArcs > shown)
                    break;

                SpiralArc(span, k, out Vector2 middle, out float radius, out float from);
                Vector2 m = SpiralPlace(markA, span, count, middle);
                DestroyerArt.DrawLine(batch, m, SpiralPlace(markA, span, count, middle + from.ToRotationVector2() * radius), 2f, faint);
                DestroyerArt.DrawLine(batch, m, SpiralPlace(markA, span, count, middle + (from + MathHelper.PiOver2).ToRotationVector2() * radius), 2f, faint);
            }

            // The spiral itself, what of it is still ahead of it: brighter, flickering.
            float flick = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.7f);
            Color bright = DestroyerArt.Lit(Color.Lerp(tint, Color.White, 0.25f) * (0.75f * fade * flick));
            Color halo = DestroyerArt.Lit(tint * (0.22f * fade));
            const int pieces = 110;
            SpiralAt(markA, span, count, length * gone, out Vector2 last, out _);

            for (int i = 1; i <= pieces; i++)
            {
                float f = gone + (1f - gone) * i / pieces;

                if (f > shown)
                    break;

                SpiralAt(markA, span, count, length * f, out Vector2 next, out _);
                DestroyerArt.DrawLine(batch, last, next, 14f, halo);
                DestroyerArt.DrawLine(batch, last, next, 3.5f, bright);
                last = next;
            }

            // Its fifteen points, a stroke out to either side of each.
            for (int i = 0; i < SpiralPoints; i++)
            {
                float f = (i + 0.5f) / SpiralPoints;

                if (f > shown || f < gone)
                    continue;

                SpiralAt(markA, span, count, length * f, out Vector2 point, out Vector2 along);
                float square = new Vector2(-along.Y, along.X).ToRotation();
                DestroyerArt.DrawGlow(batch, point, 22f, DestroyerArt.Lit(tint * (0.7f * fade)), hotCore: true);
                DrawMark(batch, point, square, 150f, 0.5f, 0.8f * fade, tint);
                DrawMark(batch, point, square + MathHelper.Pi, 150f, 0.5f, 0.8f * fade, tint);
            }
        }
    }
}
