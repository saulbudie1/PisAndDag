using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its Legends moves --------------------------------------------------------------------------------------------------
    //
    //   PULSE (both phases, rare: 25-35 s apart; after Calamity's Thanatos's mouth laser, looked at for how it goes about it)
    //       COME     it swings in to about 26 tiles off the player, slowing as it nears
    //       CHARGE   100  it all but stops, its head turning slowly onto them; red runs up its body from its tail to its
    //                     head, red sparks thrown where it passes; its mouth gathers its light; five warning lines from its
    //                     mouth close in on its aim like a sight
    //       FIRE     150  its beam, four tiles wide, white at its heart and its colour about it, from its mouth; it creeps
    //                     forward and turns onto the player only slowly
    //   STORM (both phases, every 30-40 s; about 8 s)
    //       UP            it climbs into the sky, thirty-odd tiles over the player
    //       RIDE     480  it rides the sky over them, back and forth; lightning crackles at its mouth and strikes out of it
    //                     to places within 30 tiles (harmless); its body glows yellow and, at random within every three
    //                     seconds, zaps: anything within 3 tiles of it is hurt, its own Probes overcharged
    //       DOWN          it dives back on them
    //   VOLLEY (phase one)
    //       it slows; every segment fires a bolt at the player at once, each warned by a line from it first; three times,
    //       90 ticks apart
    //   POP (phase two)
    //       it swings round the player in a half circle over them and stops; every segment shakes, and pops out a Probe
    //   BERSERK (phase two)
    //       lightning sparks off it everywhere; it goes three times as fast and twice as sharp, out to 90 tiles off the
    //       player, turns, a warning line, and dashes at them at seven times its speed with its robotic roar; three times
    //   CAGE (phase two; one of two more, after Thanatos)
    //       it circles the player 42 tiles out, its body a ring with one gap (between its head and its tail); the ring
    //       tightens to 24 tiles; five times every second segment fires a warned bolt at them, but for a stretch of ten
    //   SWEEP (phase two; the other)
    //       it comes to about 36 tiles off the player, charges as for its pulse aiming a little behind them, and fires
    //       its beam as it curls: its head always along its way, the beam turning with it like a lighthouse's, onto the
    //       player and past them; its lightning chains from its body near its head to the beam
    //
    // The server decides where and when (and fires); every machine plays every step from its clock.

    public partial class Destroyer
    {
        private const int PulseCome = 10, PulseCharge = 11, PulseFire = 12, PulseAfter = 13;
        private const int StormUp = 20, StormRide = 21, StormDown = 22;
        private const int VolleySlow = 30, VolleyFire = 31, VolleyAfter = 32;
        private const int PopCome = 40, PopArc = 41, PopShake = 42, PopAfter = 43;
        private const int RageStart = 50, RageOut = 51, RageAim = 52, RageDash = 53, RageAfter = 54;
        private const int CageCome = 60, CageRing = 61, CageAfter = 62;
        private const int SweepCome = 70, SweepCharge = 71, SweepFire = 72, SweepAfter = 73;

        /// <summary>Its usual speed and turn (vanilla's top speed is 16).</summary>
        private const float Speed = 16f;
        private const float TurnRate = 0.045f;

        private const int PulseChargeTicks = 100;
        private const int PulseFireTicks = 150;
        private const int StormRideTicks = 480;
        private const int VolleyEvery = 90;
        private const int VolleyWaves = 3;
        private const int PopArcTicks = 90;
        private const int PopShakeTicks = 70;
        private const int Dashes = 3;
        private const int CageTicks = 380;
        private const int SweepChargeTicks = 80;
        private const int SweepFireTicks = 300;

        private const float PulseOff = 26f * 16f;
        private const float StormHigh = 30f * 16f;
        private const float StormWide = 46f * 16f;
        private const float PopRadius = 34f * 16f;
        private const float RageFar = 90f * 16f;
        private const float RageLock = 50f * 16f;
        private const float CageFrom = 42f * 16f;
        private const float CageTo = 24f * 16f;

        /// <summary>Most of its Probes up at once (its pop stops short of more).</summary>
        private const int MostProbes = 44;

        /// <summary>This machine's angle round a circle (its cage, its sweep), counted on from where the circle began.</summary>
        private float around;

        /// <summary>Its storm's zaps, ticks into its ride (worked out from its seed: the same on every machine).</summary>
        private readonly List<int> zaps = new();

        private void RunMove(NPC npc, bool entered)
        {
            Player p = Target(npc);
            Vector2 at = p?.Center ?? npc.Center + Facing(npc) * 600f;

            if (Authority)
            {
                if (!phase2 && npc.life <= npc.lifeMax * Phase2At)
                {
                    BeginWake(npc);
                    return;
                }

                if (p == null && timer > 90)
                {
                    EndMove(npc);
                    return;
                }
            }

            if (step != RageDash)
                npc.damage = npc.defDamage;

            switch (step / 10)
            {
                case 1:
                    RunPulse(npc, entered, p, at);
                    break;

                case 2:
                    RunStorm(npc, entered, p, at);
                    break;

                case 3:
                    RunVolley(npc, entered, p, at);
                    break;

                case 4:
                    RunPop(npc, entered, p, at);
                    break;

                case 5:
                    RunRage(npc, entered, p, at);
                    break;

                case 6:
                    RunCage(npc, entered, p, at);
                    break;

                case 7:
                    RunSweep(npc, entered, p, at);
                    break;

                default:
                    if (Authority)
                        EndMove(npc);

                    break;
            }
        }

        // ---- PULSE --------------------------------------------------------------------------------------------------------

        private void RunPulse(NPC npc, bool entered, Player p, Vector2 at)
        {
            int t = timer;

            switch (step)
            {
                case PulseCome:
                    {
                        Vector2 to = at + new Vector2(side * PulseOff, -6f * 16f);
                        float d = Vector2.Distance(npc.Center, to);
                        Steer(npc, to, 15f * MathHelper.Clamp(d / 520f, 0.3f, 1f), 0.055f);

                        if (Authority && (d < 140f || t >= 150))
                            Next(npc, PulseCharge);

                        break;
                    }

                case PulseCharge:
                    {
                        float u = Math.Min(1f, (t + 1f) / PulseChargeTicks);
                        Steer(npc, at, 2.2f, 0.02f, 0.1f);
                        ChargeWave(npc, chain.Count * (1f - u));
                        MouthGather(npc, Smooth((u - 0.35f) / 0.65f));
                        HeadGlow(npc, 0.6f + u);

                        if (entered && !Main.dedServ)
                            SoundEngine.PlaySound(ChargeSound, Here(npc));

                        if (Authority && t >= PulseChargeTicks)
                            Next(npc, PulseFire);

                        break;
                    }

                case PulseFire:
                    {
                        if (entered)
                        {
                            StartBeam(npc);

                            if (Authority)
                                DestroyerShots.Ray(npc, DestroyerShots.RayDamage(npc));
                        }

                        Steer(npc, at, 2.5f, 0.008f, 0.1f);
                        beamAngle = npc.rotation - MathHelper.PiOver2;
                        BeamTick(npc);
                        HeadGlow(npc, 1.6f);

                        if (Authority && t >= PulseFireTicks)
                            Next(npc, PulseAfter);

                        break;
                    }

                case PulseAfter:
                    {
                        if (entered)
                            StopBeam();

                        Steer(npc, at, 14f, 0.05f, 0.05f);

                        if (Authority && t >= 40)
                            EndMove(npc);

                        break;
                    }
            }
        }

        // ---- STORM --------------------------------------------------------------------------------------------------------

        private void RunStorm(NPC npc, bool entered, Player p, Vector2 at)
        {
            int t = timer;

            switch (step)
            {
                case StormUp:
                    {
                        Vector2 to = at + new Vector2(side * 36f * 16f, -32f * 16f);
                        float d = Vector2.Distance(npc.Center, to);
                        Steer(npc, to, 20f, 0.06f);
                        stormGlow = Math.Max(stormGlow, 0.5f * Math.Min(1f, t / 60f));

                        if (Authority && (d < 180f || t >= 110))
                            Next(npc, StormRide);

                        break;
                    }

                case StormRide:
                    {
                        if (entered || zaps.Count == 0)
                        {
                            zaps.Clear();
                            Random r = new(moveSeed);

                            for (int z = 20 + r.Next(30); z < StormRideTicks - 10; z += 30 + r.Next(151))
                                zaps.Add(z);
                        }

                        // Back and forth over them, a leg every 160 ticks, a little rise and fall.
                        int leg = t / 160;
                        float way = side * (leg % 2 == 0 ? -1f : 1f);
                        Vector2 to = at + new Vector2(way * StormWide, -StormHigh + 64f * (float)Math.Sin(t * 0.05f));
                        Steer(npc, to, 17f, 0.05f);
                        stormGlow = Math.Max(stormGlow, 0.5f);
                        HeadGlow(npc, 1.2f);

                        if (!Main.dedServ)
                        {
                            if (t % 3 == 0)
                                MouthCrackle(npc, 54f);

                            // Strikes out of its mouth to places within 30 tiles, harmless.
                            Random strike = new(moveSeed ^ t * 7919);

                            if (strike.Next(100) < 4)
                            {
                                float angle = MathHelper.Lerp(0.15f, 0.85f, (float)strike.NextDouble()) * MathHelper.Pi;
                                float reach = MathHelper.Lerp(8f, 30f, (float)strike.NextDouble()) * 16f;
                                Vector2 mouth = Front(npc, 24f);
                                Strike(mouth, mouth + angle.ToRotationVector2() * reach);
                            }
                        }

                        if (zaps.Contains(t))
                            Zap(npc);

                        if (Authority && t >= StormRideTicks)
                            Next(npc, StormDown);

                        break;
                    }

                case StormDown:
                    {
                        Steer(npc, at, 22f, 0.06f);

                        if (Authority && t >= 60)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>
        /// Its body zaps (its storm): its yellow flashes, its lightning runs along it, and anything within 3 tiles of it is
        /// hurt; its own Probes there are overcharged (the server's). Each machine hurts its own player.
        /// </summary>
        private void Zap(NPC npc)
        {
            zapGlow = 1f;
            ZapBody(npc);

            const int reach = 48;

            if (!Main.dedServ)
            {
                Player me = Main.LocalPlayer;

                if (me.active && !me.dead && !me.immune && Near(npc, me.Hitbox, reach))
                {
                    int damage = Math.Max(1, (int)(npc.defDamage * 0.4f));
                    me.Hurt(Terraria.DataStructures.PlayerDeathReason.ByNPC(npc.whoAmI), damage, Math.Sign(me.Center.X - npc.Center.X));
                    Arc(me.Center, NearestPart(npc, me.Center), DestroyerArt.Yellow, 10f, 14, 1);
                }
            }

            if (!Authority)
                return;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC probe = Main.npc[i];

                if (probe.active && probe.type == NPCID.Probe && Near(npc, probe.Hitbox, reach) && probe.TryGetGlobalNPC(out LegendsProbe orb))
                    orb.Overcharge(probe);
            }
        }

        /// <summary>Whether a box is within this many pixels of its body or head.</summary>
        private bool Near(NPC npc, Rectangle box, int reach)
        {
            Rectangle grown = box;
            grown.Inflate(reach, reach);

            if (grown.Intersects(npc.Hitbox))
                return true;

            foreach (int i in chain)
            {
                NPC segment = Main.npc[i];

                if (grown.Intersects(segment.Hitbox) && (!segment.TryGetGlobalNPC(out DestroyerPart part) || !part.Exploded))
                    return true;
            }

            return false;
        }

        /// <summary>The part of it nearest a place.</summary>
        private Vector2 NearestPart(NPC npc, Vector2 to)
        {
            Vector2 best = npc.Center;
            float bestD = Vector2.DistanceSquared(best, to);

            foreach (int i in chain)
            {
                float d = Vector2.DistanceSquared(Main.npc[i].Center, to);

                if (d < bestD)
                {
                    bestD = d;
                    best = Main.npc[i].Center;
                }
            }

            return best;
        }

        // ---- VOLLEY -------------------------------------------------------------------------------------------------------

        private void RunVolley(NPC npc, bool entered, Player p, Vector2 at)
        {
            int t = timer;

            switch (step)
            {
                case VolleySlow:
                    {
                        Steer(npc, at, 4f, 0.03f, 0.06f);
                        HeadGlow(npc, 0.6f);

                        if (Authority && t >= 40)
                            Next(npc, VolleyFire);

                        break;
                    }

                case VolleyFire:
                    {
                        Steer(npc, at, 4f, 0.02f, 0.06f);
                        HeadGlow(npc, 1f);

                        if (t % VolleyEvery == 0 && t < VolleyEvery * VolleyWaves)
                        {
                            if (Authority && p != null)
                                Volley(npc, p, 50, -1, 0, 0);

                            Sound(SoundID.Item93 with { Volume = 0.8f, Pitch = -0.4f }, npc.Center, 4);
                        }

                        if (Authority && t >= VolleyEvery * VolleyWaves)
                            Next(npc, VolleyAfter);

                        break;
                    }

                case VolleyAfter:
                    {
                        Steer(npc, at, 14f, 0.05f, 0.05f);

                        if (Authority && t >= 40)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>
        /// Bolts from its segments at the player, each warned for <paramref name="warn"/> ticks: every one (or every second,
        /// from <paramref name="parity"/>, when <paramref name="every"/> is 2), all but <paramref name="gapFrom"/> and the
        /// <paramref name="gapLength"/> after it; those near enough to the player; no more than 64. The server's.
        /// </summary>
        private void Volley(NPC npc, Player p, int warn, int gapFrom, int gapLength, int parity, int every = 1)
        {
            List<NPC> from = new();

            for (int i = 0; i < chain.Count; i++)
            {
                if (every > 1 && i % every != parity)
                    continue;

                if (gapFrom >= 0 && i >= gapFrom && i < gapFrom + gapLength)
                    continue;

                NPC segment = Main.npc[chain[i]];

                if (Vector2.Distance(segment.Center, p.Center) < 1500f)
                    from.Add(segment);
            }

            int stride = Math.Max(1, (int)Math.Ceiling(from.Count / 64f));
            int damage = DestroyerShots.BoltDamage(npc);

            for (int i = 0; i < from.Count; i += stride)
                DestroyerShots.Bolt(npc, from[i], p, warn, damage);
        }

        // ---- POP ----------------------------------------------------------------------------------------------------------

        private void RunPop(NPC npc, bool entered, Player p, Vector2 at)
        {
            int t = timer;
            float start = side > 0 ? 0f : MathHelper.Pi;

            switch (step)
            {
                case PopCome:
                    {
                        // To the half circle's start: level with them, to its side.
                        Vector2 to = markA + start.ToRotationVector2() * PopRadius;
                        float d = Vector2.Distance(npc.Center, to);
                        Steer(npc, to, 20f, 0.07f);

                        if (Authority && (d < 150f || t >= 100))
                            Next(npc, PopArc);

                        break;
                    }

                case PopArc:
                    {
                        float u = Math.Min(1f, (t + 1f) / PopArcTicks);
                        float e = 1f - (1f - u) * (1f - u);
                        float angle = start - side * MathHelper.Pi * e;
                        Move(npc, markA + angle.ToRotationVector2() * PopRadius);
                        HeadGlow(npc, 1f);

                        if (Authority && t >= PopArcTicks)
                            Next(npc, PopShake);

                        break;
                    }

                case PopShake:
                    {
                        float u = Math.Min(1f, (t + 1f) / PopShakeTicks);
                        float end = start - side * MathHelper.Pi;
                        Vector2 rest = markA + end.ToRotationVector2() * PopRadius;
                        Move(npc, rest, -side * (end + MathHelper.PiOver2).ToRotationVector2());
                        shake = 1.5f + 4f * u;
                        HeadGlow(npc, 1f + u);

                        if (!Main.dedServ)
                        {
                            if (Main.rand.NextFloat() < 0.6f + u)
                            {
                                if (OnScreenCrease(npc, out Vector2 crease, out Vector2 along))
                                    Steam(crease, along, 0.8f + u);
                            }

                            if (t % 6 == 0)
                                Sound(SoundID.NPCHit4 with { Volume = 0.35f + 0.4f * u, Pitch = -0.4f + 0.5f * u, MaxInstances = 4 }, npc.Center, 4);

                            Shake(npc.Center, 1f + 3f * u, 6);
                        }

                        if (t == PopShakeTicks)
                            Pop(npc, p);

                        if (Authority && t >= PopShakeTicks)
                            Next(npc, PopAfter);

                        break;
                    }

                case PopAfter:
                    {
                        Steer(npc, at, 14f, 0.05f);

                        if (Authority && t >= 30)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>Every segment pops out a Probe (as many as may be up), each thrown outward from the circle.</summary>
        private void Pop(NPC npc, Player p)
        {
            Sound(SoundID.Item62 with { Volume = 0.7f, Pitch = 0.3f }, npc.Center, 8);
            Sound(SoundID.Item93 with { Volume = 0.8f }, npc.Center, 8);
            Shake(npc.Center, 8f, 20);

            int up = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.Probe)
                    up++;
            }

            int room = Math.Max(0, MostProbes - up);
            int stride = room <= 0 ? int.MaxValue : Math.Max(1, (int)Math.Ceiling(chain.Count / (float)room));

            for (int i = 0; i < chain.Count; i++)
            {
                NPC segment = Main.npc[chain[i]];
                Vector2 outward = (segment.Center - markA).SafeNormalize(-Vector2.UnitY);
                PopBurst(segment.Center, outward);

                if (!Authority || i % stride != 0 || room <= 0)
                    continue;

                int made = NPC.NewNPC(npc.GetSource_FromAI(), (int)segment.Center.X, (int)segment.Center.Y, NPCID.Probe);

                if (made < Main.maxNPCs)
                {
                    Main.npc[made].velocity = outward * 7f;
                    Main.npc[made].netUpdate = true;
                    room--;
                }

                segment.ai[2] = 1f;
                segment.netUpdate = true;
            }
        }

        // ---- BERSERK ------------------------------------------------------------------------------------------------------

        private void RunRage(NPC npc, bool entered, Player p, Vector2 at)
        {
            int t = timer;

            // Lightning sparks off it everywhere, once in a while.
            if (!Main.dedServ && (step == RageStart ? t % 3 == 0 : Main.rand.NextBool(12)))
                RageSparks(npc, step == RageStart ? 3 : 2);

            switch (step)
            {
                case RageStart:
                    {
                        Steer(npc, at, 6f, 0.05f, 0.08f);
                        shake = 3f;
                        HeadGlow(npc, 1.5f, flare: t % 12 == 0);

                        if (entered)
                            Sound(SoundID.Item94 with { Volume = 1f, Pitch = -0.4f }, npc.Center, 4);

                        Shake(npc.Center, 3f, 8);

                        if (Authority && t >= 60)
                            Next(npc, RageOut);

                        break;
                    }

                case RageOut:
                    {
                        // Three times as fast, twice as sharp, out to ninety tiles off them.
                        Vector2 away = (npc.Center - at).SafeNormalize(Facing(npc));
                        Steer(npc, at + away * RageFar, Speed * 3f, TurnRate * 2f, 0.12f);
                        HeadGlow(npc, 1.2f);

                        if (Authority && (Vector2.Distance(npc.Center, at) >= RageFar - 32f || t >= 100))
                            Next(npc, RageAim);

                        break;
                    }

                case RageAim:
                    {
                        Steer(npc, at, 10f, TurnRate * 2f, 0.15f);
                        HeadGlow(npc, 1.5f + t / 34f);

                        if (!Main.dedServ && t % 2 == 0)
                            MouthCrackle(npc, 60f);

                        if (Authority && t >= 34)
                            Next(npc, RageDash);

                        break;
                    }

                case RageDash:
                    {
                        if (entered)
                        {
                            Bellow(npc, DashRoar, 1.2f);

                            if (!Main.dedServ && Watching(npc.Center))
                                Cinematic.Flash(DestroyerArt.Teal, 0.08f);
                        }

                        // Seven times its speed, still turning (twice as sharp) until it is close.
                        float far = Vector2.Distance(npc.Center, at);
                        Steer(npc, at, Speed * 7f, far > RageLock ? TurnRate * 2f : 0f, 0.25f);
                        npc.damage = (int)(npc.defDamage * 1.3f);
                        HeadGlow(npc, 2f);

                        if (!Main.dedServ && far < 900f)
                            Shake(npc.Center, 4f, 8);

                        Vector2 going = npc.velocity.SafeNormalize(Facing(npc));
                        bool passed = Vector2.Dot(npc.Center - at, going) > 40f * 16f;

                        if (Authority && (passed || t >= 40))
                        {
                            count++;
                            npc.netUpdate = true;
                            Next(npc, count < Dashes ? RageOut : RageAfter);
                        }

                        break;
                    }

                case RageAfter:
                    {
                        Steer(npc, at, 14f, 0.06f, 0.06f);

                        if (Authority && t >= 50)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>Lightning off a few segments on screen, a little way out.</summary>
        private void RageSparks(NPC npc, int many)
        {
            for (int i = 0; i < many && chain.Count > 0; i++)
            {
                NPC segment = Main.npc[chain[Main.rand.Next(chain.Count)]];

                if (!Seen(segment.Center, 60f))
                    continue;

                Arc(segment.Center, segment.Center + Main.rand.NextVector2CircularEdge(120f, 120f) * Main.rand.NextFloat(0.5f, 1f), DestroyerArt.Teal, Main.rand.NextFloat(6f, 11f), 14, 1);
            }

            Sound(SoundID.Item93 with { Volume = 0.45f, Pitch = Main.rand.NextFloat(-0.2f, 0.3f) }, npc.Center, 3);
        }

        // ---- CAGE ---------------------------------------------------------------------------------------------------------

        private void RunCage(NPC npc, bool entered, Player p, Vector2 at)
        {
            int t = timer;

            switch (step)
            {
                case CageCome:
                    {
                        Vector2 dir = (npc.Center - markA).SafeNormalize(Vector2.UnitX);
                        Vector2 to = markA + dir * CageFrom;
                        float d = Vector2.Distance(npc.Center, to);
                        Steer(npc, to, 24f, 0.07f);

                        if (Authority && (d < 140f || t >= 110))
                            Next(npc, CageRing);

                        break;
                    }

                case CageRing:
                    {
                        if (entered)
                            around = (npc.Center - markA).ToRotation();

                        markA += (at - markA) * 0.012f;
                        float radius = MathHelper.Lerp(CageFrom, CageTo, Smooth(t / (float)CageTicks));
                        around += spin * 24f / radius;
                        Vector2 to = markA + around.ToRotationVector2() * radius;
                        Move(npc, to);
                        HeadGlow(npc, 1.2f);

                        // Five waves: every second segment (alternating), but for a stretch of ten.
                        if (t >= 60 && (t - 60) % 70 == 0 && t <= 340)
                        {
                            if (Authority && p != null)
                            {
                                int wave = (t - 60) / 70;
                                Random r = new(moveSeed + wave * 131);
                                int gap = r.Next(Math.Max(1, chain.Count - 10));
                                Volley(npc, p, 40, gap, 10, wave % 2, 2);
                            }

                            Sound(SoundID.Item93 with { Volume = 0.7f, Pitch = -0.3f }, npc.Center, 4);
                        }

                        if (Authority && t >= CageTicks)
                            Next(npc, CageAfter);

                        break;
                    }

                case CageAfter:
                    {
                        Steer(npc, at, 18f, 0.05f);

                        if (Authority && t >= 40)
                            EndMove(npc);

                        break;
                    }
            }
        }

        // ---- SWEEP --------------------------------------------------------------------------------------------------------
        //
        // Its head always faces the way it goes, and its beam comes out along that way (by request: "head should not be broken
        // like this, it should keep natural on its leading path laser too"). So it curls: slow, turning at a steady rate, its
        // body coiling behind it, its beam a lighthouse's turning across the ground about it. It aims a little behind the
        // player as it charges, so the beam comes onto them and on past them; run with it, or through it, or get inside its
        // curl, where the beam never goes.

        /// <summary>How far out it stops to curl; how fast it creeps and turns while it fires (its curl about 8 tiles across, so the player is well outside it).</summary>
        private const float SweepOff = 36f * 16f;
        private const float SweepCreep = 1.5f;
        private const float SweepTurn = 0.012f;

        /// <summary>How far behind the player it aims as it charges, so its beam comes onto them (radians).</summary>
        private const float SweepLead = 1.2f;

        private void RunSweep(NPC npc, bool entered, Player p, Vector2 at)
        {
            int t = timer;

            switch (step)
            {
                case SweepCome:
                    {
                        Vector2 to = at + new Vector2(side * SweepOff, -4f * 16f);
                        float d = Vector2.Distance(npc.Center, to);
                        Steer(npc, to, 20f * MathHelper.Clamp(d / 520f, 0.3f, 1f), 0.06f);

                        if (Authority && (d < 140f || t >= 120))
                        {
                            // It will turn toward the player's side: its beam swings onto them.
                            Vector2 heading = Facing(npc);
                            Vector2 toThem = at - npc.Center;
                            spin = heading.X * toThem.Y - heading.Y * toThem.X >= 0f ? 1f : -1f;
                            Next(npc, SweepCharge);
                        }

                        break;
                    }

                case SweepCharge:
                    {
                        if (entered && !Main.dedServ)
                            SoundEngine.PlaySound(ChargeSound, Here(npc));

                        float u = Math.Min(1f, (t + 1f) / SweepChargeTicks);
                        float aim = (at - npc.Center).ToRotation() - spin * SweepLead;
                        Glide(npc, aim.ToRotationVector2(), SweepCreep, 0.03f, 0.1f);
                        beamAngle = npc.rotation - MathHelper.PiOver2;
                        ChargeWave(npc, chain.Count * (1f - u));
                        MouthGather(npc, Smooth((u - 0.3f) / 0.7f));

                        if (Authority && t >= SweepChargeTicks)
                            Next(npc, SweepFire);

                        break;
                    }

                case SweepFire:
                    {
                        if (entered)
                        {
                            StartBeam(npc);

                            if (Authority)
                                DestroyerShots.Ray(npc, DestroyerShots.RayDamage(npc));
                        }

                        // A steady curl: its heading turned the same each tick, its beam along it.
                        float heading = npc.rotation - MathHelper.PiOver2 + spin * SweepTurn;
                        npc.velocity = heading.ToRotationVector2() * MathHelper.Lerp(npc.velocity.Length(), SweepCreep, 0.1f);
                        npc.rotation = heading + MathHelper.PiOver2;
                        beamAngle = heading;
                        BeamTick(npc);
                        HeadGlow(npc, 1.6f);

                        // Its lightning chains from its body, about its head, to the beam.
                        if (Authority && t % 6 == 3)
                            Chains(npc, 2);

                        if (Authority && t >= SweepFireTicks)
                            Next(npc, SweepAfter);

                        break;
                    }

                case SweepAfter:
                    {
                        if (entered)
                            StopBeam();

                        Steer(npc, at, 14f, 0.05f);

                        if (Authority && t >= 40)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>Lightning (harmful) from segments near its head to the nearest of its beam, close by its mouth.</summary>
        private void Chains(NPC npc, int many)
        {
            Vector2 dir = beamAngle.ToRotationVector2();
            Vector2 mouth = BeamStart(npc);
            int damage = DestroyerShots.ArcDamage(npc);

            for (int tries = 0; tries < many * 4 && many > 0 && chain.Count > 0; tries++)
            {
                NPC segment = Main.npc[chain[Main.rand.Next(Math.Min(chain.Count, 40))]];
                Vector2 s = segment.Center;
                float along = Vector2.Dot(s - mouth, dir);

                if (along < 0f || along > 520f)
                    continue;

                Vector2 foot = mouth + dir * along;

                if (Vector2.Distance(s, foot) > 420f)
                    continue;

                DestroyerShots.Chain(npc, s, foot, damage);
                many--;
            }
        }

        // ---- warnings, drawn with its head ------------------------------------------------------------------------------

        /// <summary>The last warning it showed (this machine's), to fade out once it has gone: where, which way, how spread, how long, how many lines.</summary>
        private Vector2 warnFrom;
        private float warnAngle, warnSpread, warnLength, warnReady;
        private int warnLines, warnFade;

        /// <summary>
        /// Its warning lines for this frame (from its head's draw): its pulse's closing sight, its dash's line. When the step
        /// they belong to ends they fade out where they last were (by request: "indicators need to fade not disappear
        /// immediately").
        /// </summary>
        private void DrawWarnings(NPC npc, SpriteBatch batch)
        {
            bool showing = false;

            if (scene == Scene.Move)
            {
                if (step == PulseCharge && timer >= 40)
                {
                    float u = Math.Min(1f, (timer - 40f) / (PulseChargeTicks - 40f));
                    KeepWarning(npc.rotation - MathHelper.PiOver2, npc.Center, 30f, MathHelper.Lerp(0.55f, 0f, Smooth(u)), 2000f, u, 5);
                    showing = true;
                }
                else if (step == SweepCharge && timer >= 20)
                {
                    float u = Math.Min(1f, (timer - 20f) / (SweepChargeTicks - 20f));
                    KeepWarning(beamAngle, npc.Center, 30f, MathHelper.Lerp(0.55f, 0f, Smooth(u)), 2000f, u, 5);
                    showing = true;
                }
                else if (step == RageAim)
                {
                    Player p = Target(npc);

                    if (p != null)
                    {
                        Vector2 mouth = Front(npc, 26f);
                        KeepWarning((p.Center - mouth).ToRotation(), mouth, 0f, 0f, RageFar + 400f, Math.Min(1f, timer / 34f), 1);
                        showing = true;
                    }
                }
            }

            if (!showing && warnFade <= 0)
                return;

            float alpha = showing ? 1f : warnFade / (float)DestroyerArt.WarningFade;

            for (int k = 0; k < warnLines; k++)
            {
                float off = warnLines == 1 ? 0f : warnSpread * (k - (warnLines - 1) / 2f) / ((warnLines - 1) / 2f);
                bool middle = warnLines == 1 || k == (warnLines - 1) / 2;
                DestroyerArt.DrawWarning(batch, warnFrom, warnAngle + off, warnLength, warnReady, alpha * (middle ? 1f : 0.55f));
            }
        }

        /// <summary>The warning as it is now: kept, to be faded from here when it ends.</summary>
        private void KeepWarning(float aim, Vector2 head, float ahead, float spread, float length, float ready, int lines)
        {
            warnFrom = head + aim.ToRotationVector2() * ahead;
            warnAngle = aim;
            warnSpread = spread;
            warnLength = length;
            warnReady = ready;
            warnLines = lines;
            warnFade = DestroyerArt.WarningFade;
        }

        // ---- told across the network --------------------------------------------------------------------------------

        private void SendLegends(BinaryWriter w)
        {
            w.Write(phase2);
            w.Write((short)Math.Clamp(restFor, short.MinValue, short.MaxValue));
            w.Write((short)Math.Clamp(stormIn, short.MinValue, short.MaxValue));
            w.Write((short)Math.Clamp(pulseIn, short.MinValue, short.MaxValue));
            w.Write((byte)lastAct);
            w.Write(markA.X);
            w.Write(markA.Y);
            w.Write(markB.X);
            w.Write(markB.Y);
            w.Write(spin);
            w.Write((byte)count);
            w.Write(side);
            w.Write(moveSeed);
        }

        private void ReceiveLegends(BinaryReader r)
        {
            phase2 = r.ReadBoolean();
            restFor = r.ReadInt16();
            stormIn = r.ReadInt16();
            pulseIn = r.ReadInt16();
            lastAct = (Act)r.ReadByte();
            markA = new Vector2(r.ReadSingle(), r.ReadSingle());
            markB = new Vector2(r.ReadSingle(), r.ReadSingle());
            spin = r.ReadSingle();
            count = r.ReadByte();
            side = r.ReadSByte();
            moveSeed = r.ReadInt32();
        }
    }
}
