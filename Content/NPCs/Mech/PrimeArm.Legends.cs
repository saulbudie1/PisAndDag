using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- Skeletron Prime's arms in Legends ----------------------------------------------------------------------------------
    //
    // By request ("Skeletron Prime Legends: ..."). Three times their life. Each fights as vanilla's does (its first
    // attack), and between stretches of that, one of its own, one arm at a time (its head says when: Prime.ArmMayAct):
    //
    //   LASER    "Attack 1: shoot vanilla normally but green laser" (PrimeVanillaShots; its lens green to match)
    //            BURST    "slow down, shakes and shoots barrages of 3 unevenly spaced lasers with interval of random
    //                     between 30 to 50 ticks, with recoil x5-7" (asked after: five to seven barrages)
    //                       SLOW 40, FIRE (each barrage marked a moment before; kicked back by every shot), END 24
    //            CORNER   "flies to corner and shoot a column of laser down and the laser moves slowly to the side,
    //                     going 1 screen length in 4 secs"
    //                       FLY (to the screen's upper corner on its side), WARN 44 (straight down, as wide as it will
    //                       be, and the way it will go), SWEEP 240 (PrimeBeam; across, 1840 pixels), END 30
    //   CANNON   "Attack 1: vanilla style Shoots mini missles that loosely home onto player and player can break the
    //            missle ankles": every bomb vanilla would have fired is a missile instead (PrimeMissile)
    //            BOMB RUN "flies over in a upward facing flat parabol and drop bombs, on the arc marks 10 points that
    //                     will have bomb falling down once the arm goes past it happens rarer"
    //                       FLY (to one end of the arc), RUN 150, END 30; the ten points are not shown (by request:
    //                       "doing too much indicator")
    //   VICE     "Attack 1: Clenched and try to punch/swing at the player" (vanilla's lunges, its jaws shut for them)
    //            GRAPPLE  "open and clenching like a grappling hook, once touch the player, player is pickede up and it
    //                     does the hulk swing around like 5 times and throw up ccbypass style" (asked after: the throw
    //                     is not a knockback, so nothing that resists knockback resists it; a player who cannot be hurt
    //                     that moment is not caught)
    //                       AIM 44 (open and clenching; a line at the player, stopped ten ticks before it goes),
    //                       SHOOT 20 (out along it; three tries, BACK 30 between), SWING 150 (lifted clear of the
    //                       ground, five turns about its far end, quicker and quicker; then let go, upward), END 30
    //   SAW      "Attack 1: Swing saw like normal" (vanilla's)
    //            DIG      "Saw spins faster, with smokes coming out, dig into ground and run along the ground and scoop
    //                     up, throwingdirt and dust fragments"
    //                       REV 50 (its way marked along the ground), DIVE, RUN (eleven and a half pixels a tick,
    //                       through where the player stood), SCOOP 26 (up out of it: seven clods thrown), END 30
    //
    // The server (or a single player) picks each and its marks; every machine plays it from that (SendLegends).

    public partial class PrimeArm
    {
        internal enum Act : byte
        {
            None,
            Burst, Corner,
            BombRun,
            Grapple,
            Dig,
        }

        private Act act, lastAct;
        private int actStep, actT;

        /// <summary>Ticks of vanilla's left before its next own attack (the server's count; under nothing: not yet set).</summary>
        private int restFor = -1;

        /// <summary>Two places, a number (a direction, or a length), a side, a count, a seed: what each means is the attack's own.</summary>
        private Vector2 mark, mark2;
        private float aim;
        private int aside = 1, acount, aseed;

        /// <summary>The player its vice has (its grapple): -1 none.</summary>
        private int held = -1;

        /// <summary>Vanilla's count as its own attack began: given back after, so vanilla's own keep their time.</summary>
        private float savedCount;

        // ---- set each tick by what moves it (this machine's) ------------------------------------------------------------

        /// <summary>Its touch hurts though it is not vanilla that moves it, and by how much more.</summary>
        private bool hurts;
        private float hurtScale = 1f;

        /// <summary>Its column of laser is on.</summary>
        private bool beam;

        /// <summary>Its vice's jaws: -1 as vanilla works them, 0 shut, 1 open.</summary>
        private int jaw = -1;

        /// <summary>How much faster than vanilla's its saw turns (0 to 1): seen. Its lens's light (0 to 1): seen.</summary>
        private float rev, charge;

        /// <summary>Following one of its head's attacks of all its arms.</summary>
        private bool inGroup;

        /// <summary>Where it was, and how turned, as the step it follows began (this machine's).</summary>
        private Vector2 stepFrom;
        private float stepTurn;

        /// <summary>Where its tool's end was a tick ago (the server's: what it swept through).</summary>
        private Vector2 lastTip;

        /// <summary>The ground's height under it as it runs along it (this machine's).</summary>
        private float runY;

        /// <summary>How fast it turns over, thrown back (its clash): only seen.</summary>
        private float tumble;

        internal bool Acting => act != Act.None;
        internal bool Beaming => beam;

        public override void SetDefaults(NPC entity)
        {
            if (PADWorld.Legends)
                entity.lifeMax = (int)(entity.lifeMax * Prime.ArmLifeScale);
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (!NPC.IsMechQueenUp && hurts && hurtScale != 1f)
                modifiers.SourceDamage *= hurtScale;
        }

        // ---- burst (the laser's) ----------------------------------------------------------------------------------------

        private const int BuSlow = 0, BuFire = 1, BuEnd = 2;
        private const int BuSlowTicks = 40, BuEndTicks = 24;

        /// <summary>Ticks before a barrage it is marked; how fast its lasers go.</summary>
        private const int BuTell = 14;
        private const float BuSpeed = 10f;

        // ---- corner (the laser's) ---------------------------------------------------------------------------------------

        private const int CoFly = 0, CoWarn = 1, CoSweep = 2, CoEnd = 3;
        private const int CoFlyMost = 80, CoWarnTicks = 44, CoSweepTicks = 240, CoEndTicks = 30;

        /// <summary>Where the screen's upper corner is from the player; how far across it goes (by request: "1 screen length in 4 secs").</summary>
        private const float CoWide = 880f, CoHigh = 430f, CoLength = 1840f;

        // ---- bomb run (the cannon's) ------------------------------------------------------------------------------------

        private const int BrFly = 0, BrRun = 1, BrEnd = 2;
        private const int BrFlyMost = 70, BrRunTicks = 150, BrEndTicks = 30;

        /// <summary>
        /// Its arc (by request: "a upward facing flat parabol"): half as wide as this, its lowest point this far over
        /// the player, its ends this much higher; flown from a little before one end to a little past the other. Ten
        /// marks along it.
        /// </summary>
        private const float BrHalf = 800f, BrOver = 300f, BrDeep = 120f, BrLead = 1.2f;
        private const int BrMarks = 10;

        // ---- grapple (the vice's) ---------------------------------------------------------------------------------------

        private const int GpAim = 0, GpShoot = 1, GpBack = 2, GpSwing = 3, GpEnd = 4;
        private const int GpAimTicks = 44, GpLockAt = 34, GpShootTicks = 20, GpBackTicks = 30, GpSwingTicks = 150, GpEndTicks = 30;

        /// <summary>How far it shoots out, how near its tool's end must pass to take hold, its tries, its turns (by request: "like 5 times").</summary>
        private const float GpReach = 640f, GpGrip = 30f;
        private const int GpTries = 3, GpTurns = 5;

        /// <summary>How fast it lets them go, upward, after a whole swing.</summary>
        private const float GpThrow = 18f;

        // ---- dig (the saw's) --------------------------------------------------------------------------------------------

        private const int DgRev = 0, DgDive = 1, DgRun = 2, DgScoop = 3, DgEnd = 4;
        private const int DgRevTicks = 50, DgScoopTicks = 26, DgEndTicks = 30;

        /// <summary>How fast it runs along the ground; how far before the player it goes in, and how far past them it comes out.</summary>
        private const float DgSpeed = 11.5f, DgBefore = 330f, DgPast = 280f;
        private const int DgClods = 7;

        // ---- between its own ----------------------------------------------------------------------------------------------

        private static int Rest(int type) => type switch
        {
            NPCID.PrimeLaser => Main.rand.Next(300, 481),

            // By request: "happens rarer".
            NPCID.PrimeCannon => Main.rand.Next(840, 1201),
            NPCID.PrimeVice => Main.rand.Next(420, 601),
            _ => Main.rand.Next(360, 541),
        };

        /// <summary>Vanilla's is at rest by its head (not in a lunge, a rush, or on its way back to it).</summary>
        private static bool Idle(NPC npc) =>
            npc.type == NPCID.PrimeSaw || npc.type == NPCID.PrimeVice ? npc.ai[2] == 0f || npc.ai[2] == 3f : npc.ai[2] == 0f;

        /// <summary>
        /// Vanilla's AI moves it this tick. What Legends changes of that: its vice shut as it lunges, its cannon's
        /// bombs missiles; and, when a stretch of it has run and its head lets it, its next own attack (the server's).
        /// </summary>
        private void LegendsBetween(NPC npc, NPC head, Prime prime)
        {
            seenStep = -1;

            if (npc.type == NPCID.PrimeVice && npc.ai[2] != 0f && npc.ai[2] != 3f && npc.ai[2] != 99f)
                jaw = 0;

            if (!Authority)
                return;

            if (npc.type == NPCID.PrimeCannon)
                Missiles(npc);

            if (restFor < 0)
                restFor = Rest(npc.type) / 2 + 120;

            Player p = Prime.Target(head);

            if (p == null || head.ai[1] >= 2f || Main.IsItDay())
                return;

            if (restFor > 0)
            {
                restFor--;
                return;
            }

            if (Idle(npc) && prime.ArmMayAct(head))
                BeginAct(npc, p);
        }

        /// <summary>
        /// Where vanilla's cannon would fire a bomb this tick (its count run out: at rest, every hundred and forty
        /// ticks; in its rush, every forty), it fires a mini missile the way it points instead, and its count begins again.
        /// </summary>
        private static void Missiles(NPC npc)
        {
            if (npc.ai[2] != 0f && npc.ai[2] != 1f)
                return;

            if (npc.localAI[0] < (npc.ai[2] == 1f ? 40f : 140f))
                return;

            npc.localAI[0] = 0f;
            Vector2 way = ToolWay(npc.rotation);
            PrimeShots.Missile(npc, Tip(npc) + way * 4f, way * 6f + npc.velocity * 0.3f);
        }

        private void BeginAct(NPC npc, Player p)
        {
            Vector2 pivot = Pivot(npc);

            act = npc.type switch
            {
                NPCID.PrimeLaser => lastAct == Act.Burst ? Act.Corner : lastAct == Act.Corner ? Act.Burst : Main.rand.NextBool() ? Act.Burst : Act.Corner,
                NPCID.PrimeCannon => Act.BombRun,
                NPCID.PrimeVice => Act.Grapple,
                _ => Act.Dig,
            };

            lastAct = act;
            actStep = 0;
            actT = 0;
            aseed = Main.rand.Next(int.MaxValue);
            acount = 0;
            held = -1;
            aim = 0f;
            mark = mark2 = pivot;
            aside = Math.Abs(pivot.X - p.Center.X) > 60f ? Math.Sign(pivot.X - p.Center.X) : Main.rand.NextBool() ? 1 : -1;
            savedCount = npc.ai[3];

            switch (act)
            {
                case Act.Burst:
                    // By request: "x5-7".
                    acount = Main.rand.Next(5, 8);
                    break;

                case Act.BombRun:
                    // From the side it is on, over them, to the other.
                    aside = -aside;
                    mark = p.Center + new Vector2(0f, -BrOver);
                    break;

                case Act.Dig:
                    {
                        // Into the ground on its side of them, through where they stand, and out past.
                        aside = -aside;
                        float x = p.Center.X - aside * DgBefore;

                        if (!Prime.GroundUnder(x, p.Center.Y - 220f, 60, out float top))
                        {
                            // No ground to dig: not now.
                            act = Act.None;
                            restFor = 150;
                            return;
                        }

                        mark = new Vector2(x, top);
                        aim = DgBefore + DgPast;
                        break;
                    }
            }

            npc.netUpdate = true;
        }

        private void ActNext(NPC npc, int to)
        {
            actStep = to;
            actT = 0;
            npc.netUpdate = true;
        }

        /// <summary>Its own attack over: back to vanilla's, for a stretch (the server's).</summary>
        private void EndAct(NPC npc, Prime prime)
        {
            act = Act.None;
            actStep = 0;
            actT = 0;
            held = -1;
            restFor = Rest(npc.type);
            BackToVanilla(npc);
            prime?.ArmDone();
        }

        /// <summary>Vanilla's AI from its rest by its head, its own counts as they were.</summary>
        private void BackToVanilla(NPC npc)
        {
            npc.ai[2] = 0f;
            npc.ai[3] = savedCount;
            npc.localAI[0] = 0f;
            savedCount = 0f;

            float speed = npc.velocity.Length();

            if (speed > 12f)
                npc.velocity *= 12f / speed;

            npc.netUpdate = true;
        }

        /// <summary>Its head's attack of all its arms is over (or it is no longer one of them): vanilla's again.</summary>
        private void LeaveGroup(NPC npc)
        {
            inGroup = false;
            seenStep = -1;

            if (!Authority)
                return;

            BackToVanilla(npc);
            restFor = Math.Max(restFor, 150);
        }

        /// <summary>Whatever of Legends it was at is dropped (struck down; Legends off; its head in a scene): nothing held, nothing firing. The server's.</summary>
        private void DropAct(NPC npc, Prime prime)
        {
            bool own = act != Act.None;
            act = Act.None;
            actStep = 0;
            actT = 0;
            held = -1;
            inGroup = false;
            seenStep = -1;

            if (own)
                prime?.ArmDone();
        }

        // ---- run ----------------------------------------------------------------------------------------------------------

        /// <summary>The step of its own attack it is in, and its clock in it, for what happens at a tick of it; whether the step is new to this machine.</summary>
        private bool FollowOwn(NPC npc)
        {
            int at = 1000 + (int)act * 16 + actStep;
            bool fresh = at != seenStep;

            if (fresh)
            {
                seenStep = at;
                seenTick = -1;
                stepFrom = Pivot(npc);
                stepTurn = npc.rotation;
            }

            clock = actT;
            return fresh;
        }

        /// <summary>Its clock passes this tick, or one of every so many after it, now (once each, as <see cref="At"/>).</summary>
        private bool Beat(int from, int every)
        {
            if (clock < from)
                return false;

            return At(from + (clock - from) / every * every);
        }

        private void RunAct(NPC npc, NPC head, Prime prime)
        {
            bool fresh = FollowOwn(npc);
            Player p = Prime.Target(head);

            if (p == null || head.ai[1] >= 2f)
            {
                npc.velocity *= 0.9f;

                if (Authority)
                    EndAct(npc, prime);

                return;
            }

            // Where it is, told again now and then.
            if (Authority && actT % 20 == 10)
                npc.netUpdate = true;

            switch (act)
            {
                case Act.Burst: RunBurst(npc, prime, p, fresh); break;
                case Act.Corner: RunCorner(npc, prime, p, fresh); break;
                case Act.BombRun: RunBombRun(npc, prime, p, fresh); break;
                case Act.Grapple: RunGrapple(npc, prime, p, fresh); break;
                case Act.Dig: RunDig(npc, prime, p, fresh); break;

                default:
                    if (Authority)
                        EndAct(npc, prime);

                    break;
            }
        }

        // ---- how it moves -------------------------------------------------------------------------------------------------

        /// <summary>Its hand's middle brought toward a place, easing in: its velocity brought toward what would take it there, by this share a tick.</summary>
        private static void GlidePivot(NPC npc, Vector2 to, float speed, float share)
        {
            Vector2 want = to - Pivot(npc);
            float far = want.Length();
            want = far > 1f ? want / far * Math.Min(speed, far * 0.16f + 0.4f) : Vector2.Zero;
            npc.velocity = Vector2.Lerp(npc.velocity, want, share);
        }

        /// <summary>Turned toward pointing its tool a way, by this share a tick.</summary>
        private static void TurnTo(NPC npc, Vector2 way, float share) => npc.rotation = npc.rotation.AngleLerp(TurnOf(way), share);

        /// <summary>
        /// The first player its tool's end swept onto this tick (within <paramref name="reach"/> of the way it came):
        /// not one who cannot be hurt just now (asked after: a dodge is a dodge), nor one already held. The server's.
        /// </summary>
        private static int Catch(Vector2 from, Vector2 to, float reach)
        {
            int pieces = Math.Max(1, (int)(Vector2.Distance(from, to) / 12f));

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];

                if (!p.active || p.dead || p.ghost || p.immune || MimicSystem.Is(p) || p.GetModPlayer<PrimeGrip>().Held)
                    continue;

                Rectangle box = p.Hitbox;

                for (int k = 0; k <= pieces; k++)
                {
                    Vector2 at = Vector2.Lerp(from, to, k / (float)pieces);
                    Vector2 near = Vector2.Clamp(at, box.TopLeft(), box.BottomRight());

                    if (Vector2.DistanceSquared(at, near) <= reach * reach)
                        return i;
                }
            }

            return -1;
        }

        /// <summary>A player held by it this tick, on every machine: put at a place, turned, and how they go when it lets them.</summary>
        private static void Grip(NPC npc, int who, Vector2 at, float turn, Vector2 letGo, bool spared)
        {
            if (who < 0 || who >= Main.maxPlayers)
                return;

            Player p = Main.player[who];

            if (p.active && !p.dead)
                p.GetModPlayer<PrimeGrip>().Hold(npc.whoAmI, at, turn, letGo, spared);
        }

        // ---- burst --------------------------------------------------------------------------------------------------------

        private int[] shots;
        private int shotsFor;

        /// <summary>
        /// The tick of FIRE each laser goes, the same on every machine: for each barrage three, a short gap and a long
        /// one between them, either first (by request: "unevenly spaced"); then thirty to fifty ticks to the next.
        /// </summary>
        private int[] BurstShots()
        {
            if (shots != null && shotsFor == aseed)
                return shots;

            Random dice = new(aseed);
            int barrages = Math.Clamp(acount, 1, 8);
            shots = new int[barrages * 3];
            int at = BuTell + 2;

            for (int b = 0; b < barrages; b++)
            {
                int brief = dice.Next(4, 8), wide = dice.Next(9, 16);
                bool briefFirst = dice.Next(2) == 0;
                shots[b * 3] = at;
                at += briefFirst ? brief : wide;
                shots[b * 3 + 1] = at;
                at += briefFirst ? wide : brief;
                shots[b * 3 + 2] = at;
                at += dice.Next(30, 51);
            }

            shotsFor = aseed;
            return shots;
        }

        private void RunBurst(NPC npc, Prime prime, Player p, bool fresh)
        {
            int t = clock;
            Vector2 pivot = Pivot(npc);
            Vector2 toPlayer = (p.Center - pivot).SafeNormalize(Vector2.UnitY);

            switch (actStep)
            {
                case BuSlow:
                    // By request: "slow down, shakes".
                    npc.velocity *= 0.9f;
                    TurnTo(npc, toPlayer, 0.12f);
                    shake = 0.5f + 2f * t / BuSlowTicks;
                    charge = 0.4f * t / BuSlowTicks;

                    if (fresh)
                        Prime.Sound(SoundID.Item24 with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 3 }, pivot, 6);

                    if (Authority && t >= BuSlowTicks)
                    {
                        mark = pivot;
                        ActNext(npc, BuFire);
                    }

                    break;

                case BuFire:
                    {
                        int[] when = BurstShots();
                        float kick = 0f;
                        int next = -1;

                        TurnTo(npc, toPlayer, 0.07f);
                        Vector2 way = ToolWay(npc.rotation);

                        for (int k = 0; k < when.Length; k++)
                        {
                            int s = when[k];

                            if (t >= s)
                                kick += 13f * MathF.Exp(-(t - s) / 4f);
                            else if (next < 0)
                                next = k;

                            if (At(s))
                                BurstShot(npc, way, k);
                        }

                        // Held where it stopped, kicked back by each shot; its aim after the player, never quite on them.
                        MovePivot(npc, mark - way * kick);
                        shake = 1.2f + Math.Min(3f, kick * 0.2f);

                        // Its lens: up before each barrage, bright through it.
                        if (next >= 0 && next % 3 == 0)
                            charge = MathHelper.Clamp(1f - (when[next] - t) / (float)BuTell, 0.15f, 1f);
                        else if (next >= 0)
                            charge = 1f;

                        if (Authority && t >= when[^1] + 14)
                            ActNext(npc, BuEnd);

                        break;
                    }

                default:
                    npc.velocity *= 0.92f;
                    TurnTo(npc, toPlayer, 0.1f);

                    if (fresh)
                        LensSmokeFx(npc);

                    if (Authority && t >= BuEndTicks)
                        EndAct(npc, prime);

                    break;
            }
        }

        private void BurstShot(NPC npc, Vector2 way, int which)
        {
            Vector2 tip = Tip(npc);

            if (Authority)
            {
                Random dice = new(unchecked(aseed * 31 + which * 7919));
                float off = ((float)dice.NextDouble() * 2f - 1f) * 0.05f;
                PrimeShots.Laser(npc, tip + way * 6f, way.RotatedBy(off) * BuSpeed);
            }

            ShotFx(tip, way, 1f);
        }

        // ---- corner -------------------------------------------------------------------------------------------------------

        private void RunCorner(NPC npc, Prime prime, Player p, bool fresh)
        {
            int t = clock;
            Vector2 pivot = Pivot(npc);

            switch (actStep)
            {
                case CoFly:
                    {
                        Vector2 corner = p.Center + new Vector2(aside * CoWide, -CoHigh);
                        GlidePivot(npc, corner, 17f, 0.12f);
                        TurnTo(npc, Vector2.UnitY, 0.1f);
                        charge = 0.2f;

                        if (Authority && (t >= CoFlyMost || Vector2.DistanceSquared(pivot, corner) < 40f * 40f))
                        {
                            mark = pivot + npc.velocity * 3f;
                            ActNext(npc, CoWarn);
                        }

                        break;
                    }

                case CoWarn:
                    MovePivot(npc, Vector2.Lerp(pivot, mark, 0.25f));
                    TurnTo(npc, Vector2.UnitY, 0.25f);
                    charge = MathHelper.Clamp(t / (float)CoWarnTicks, 0f, 1f);
                    shake = 1.6f * charge;

                    if (fresh)
                    {
                        WakeFx(npc);
                        Prime.Sound(SoundID.Item24 with { Volume = 0.7f, Pitch = 0.5f, MaxInstances = 3 }, pivot, 6);
                    }

                    lampGreen = true;

                    if (Authority && t >= CoWarnTicks)
                        ActNext(npc, CoSweep);

                    break;

                case CoSweep:
                    {
                        // Across, straight and level, its column straight down.
                        float u = MathHelper.Clamp(t / (float)CoSweepTicks, 0f, 1f);
                        MovePivot(npc, new Vector2(mark.X - aside * CoLength * u, mark.Y + 3f * MathF.Sin(t * 0.3f)));
                        npc.rotation = 0.02f * MathF.Sin(t * 1.7f);
                        beam = true;
                        charge = 1f;
                        shake = 2f;
                        lampGreen = true;

                        if (fresh)
                        {
                            PrimeShots.Beam(npc, CoSweepTicks);
                            BeamFx(Tip(npc));
                        }

                        if (t % 7 == 0)
                            Prime.Shake(Tip(npc), 2.5f, 8);

                        if (Authority && t >= CoSweepTicks)
                            ActNext(npc, CoEnd);

                        break;
                    }

                default:
                    npc.velocity *= 0.9f;

                    if (fresh)
                        LensSmokeFx(npc);

                    if (Authority && t >= CoEndTicks)
                        EndAct(npc, prime);

                    break;
            }
        }

        // ---- bomb run -----------------------------------------------------------------------------------------------------

        /// <summary>A place on its arc: 0 its lowest, over where the player was; -1 and 1 its ends.</summary>
        private static Vector2 RunPoint(Vector2 lowest, int way, float u) => new(lowest.X + way * u * BrHalf, lowest.Y - BrDeep * u * u);

        private static float MarkU(int k) => -0.9f + 1.8f * k / (BrMarks - 1);

        /// <summary>The tick of RUN it passes a mark.</summary>
        private static int MarkTick(int k) => (int)MathF.Round((MarkU(k) + BrLead) / (2f * BrLead) * BrRunTicks);

        private void RunBombRun(NPC npc, Prime prime, Player p, bool fresh)
        {
            int t = clock;
            Vector2 pivot = Pivot(npc);

            switch (actStep)
            {
                case BrFly:
                    {
                        Vector2 start = RunPoint(mark, aside, -BrLead);
                        GlidePivot(npc, start, 18f, 0.12f);
                        TurnTo(npc, Vector2.UnitY, 0.12f);

                        if (fresh)
                            Prime.Sound(SoundID.Item24 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 3 }, pivot, 6);

                        if (Authority && (t >= BrFlyMost || Vector2.DistanceSquared(pivot, start) < 44f * 44f) && t >= 30)
                            ActNext(npc, BrRun);

                        break;
                    }

                case BrRun:
                    {
                        float u = -BrLead + 2f * BrLead * MathHelper.Clamp(t / (float)BrRunTicks, 0f, 1.05f);
                        MovePivot(npc, Vector2.Lerp(stepFrom, RunPoint(mark, aside, u), Prime.Smooth(t / 10f)));

                        // Pointing down, tipped a little into its way.
                        TurnTo(npc, new Vector2(aside * 0.25f, 1f).SafeNormalize(Vector2.UnitY), 0.2f);

                        if (fresh)
                            Prime.Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 3 }, pivot, 6);

                        for (int k = 0; k < BrMarks; k++)
                        {
                            if (!At(MarkTick(k)))
                                continue;

                            Vector2 at = RunPoint(mark, aside, MarkU(k)) + new Vector2(0f, 34f);
                            PrimeShots.Bomb(npc, at, new Vector2(0f, 2f));
                            DropFx(at);
                        }

                        if (Authority && t >= BrRunTicks)
                            ActNext(npc, BrEnd);

                        break;
                    }

                default:
                    npc.velocity *= 0.94f;

                    if (Authority && t >= BrEndTicks)
                        EndAct(npc, prime);

                    break;
            }
        }

        // ---- grapple ------------------------------------------------------------------------------------------------------

        /// <summary>The way its limb points at a tick of its swing: from how it pointed as it closed, five turns and what is left to where it lets go, quicker and quicker.</summary>
        private float SwingAngle(int t)
        {
            float u = MathHelper.Clamp(t / (float)GpSwingTicks, 0f, 1f);

            // Let go as its tool's end is at one side and on its way up.
            float end = aside > 0 ? MathHelper.Pi : 0f;
            float more = MathHelper.WrapAngle((end - aim) * aside);

            if (more < 0f)
                more += MathHelper.TwoPi;

            return aim + aside * (GpTurns * MathHelper.TwoPi + more) * (0.22f * u + 0.78f * u * u);
        }

        /// <summary>The middle of its swing (its limb's far end): lifted, over its first ticks, to where its whole length clears the ground.</summary>
        private Vector2 SwingMiddle(int t) => Vector2.Lerp(mark2, mark, Prime.Smooth(t / 36f));

        private void RunGrapple(NPC npc, Prime prime, Player p, bool fresh)
        {
            int t = clock;
            Vector2 pivot = Pivot(npc);
            float s = npc.scale;

            switch (actStep)
            {
                case GpAim:
                    {
                        // Drawn back from them a little, open and clenching.
                        Vector2 way = t < GpLockAt ? (p.Center - pivot).SafeNormalize(Vector2.UnitY) : aim.ToRotationVector2();
                        npc.velocity = npc.velocity * 0.88f - way * 0.3f;
                        TurnTo(npc, way, 0.2f);
                        jaw = t >= GpLockAt || t / 5 % 2 == 0 ? 1 : 0;
                        shake = t >= GpLockAt ? 1.5f : 0f;

                        if (t < GpLockAt && t % 10 == 5)
                            Prime.Sound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 4 }, pivot, 6);

                        if (Authority && At(GpLockAt))
                        {
                            aim = (p.Center - pivot).ToRotation();
                            npc.netUpdate = true;
                        }

                        if (Authority && t >= GpAimTicks)
                        {
                            mark2 = pivot;
                            mark = pivot + aim.ToRotationVector2() * GpReach;
                            lastTip = Tip(npc);
                            ActNext(npc, GpShoot);
                        }

                        break;
                    }

                case GpShoot:
                    {
                        // Out along its line, open; what its jaws pass over, it has.
                        float u = MathHelper.Clamp((t + 1f) / GpShootTicks, 0f, 1f);
                        Vector2 dir = aim.ToRotationVector2();
                        MovePivot(npc, Vector2.Lerp(mark2, mark, 1f - (1f - u) * (1f - u)));
                        npc.rotation = TurnOf(dir);
                        jaw = 1;

                        if (fresh)
                        {
                            Prime.Sound(SoundID.DD2_BallistaTowerShot with { Volume = 0.9f, Pitch = 0.1f, MaxInstances = 3 }, pivot, 6);
                            Prime.Sound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, pivot, 6);
                        }

                        if (!Authority)
                            break;

                        Vector2 tip = Tip(npc);
                        int who = Catch(lastTip, tip, GpGrip);
                        lastTip = tip;

                        if (who >= 0)
                        {
                            // Closed on them: its whirl about its far end, lifted clear of the ground, up and over first.
                            Vector2 tail = Tail(npc);
                            float top = Prime.GroundUnder(tail.X, tail.Y - 240f, 70, out float found) ? found : tail.Y + 600f;
                            float clear = (TailBack(npc.type) + HandHigh(npc.type) * 0.5f) * s + 64f;
                            held = who;
                            aim = dir.ToRotation();
                            aside = dir.X >= 0f ? -1 : 1;
                            mark2 = tail;
                            mark = new Vector2(tail.X, Math.Min(tail.Y, top - clear));
                            ActNext(npc, GpSwing);
                        }
                        else if (t >= GpShootTicks)
                        {
                            ActNext(npc, GpBack);
                        }

                        break;
                    }

                case GpBack:
                    // Nothing in its jaws: back the way it came.
                    MovePivot(npc, Vector2.Lerp(pivot, mark2, 0.12f));
                    jaw = t / 4 % 2;

                    if (Authority && t >= GpBackTicks)
                    {
                        if (++acount < GpTries)
                            ActNext(npc, GpAim);
                        else
                            ActNext(npc, GpEnd);
                    }

                    break;

                case GpSwing:
                    {
                        float a = SwingAngle(t), next = SwingAngle(t + 1);
                        Vector2 way = a.ToRotationVector2(), nextWay = next.ToRotationVector2();
                        Vector2 middle = SwingMiddle(t);
                        float reach = TailBack(npc.type) * s;
                        float u = MathHelper.Clamp(t / (float)GpSwingTicks, 0f, 1f);

                        MovePivot(npc, middle + way * reach);
                        npc.rotation = TurnOf(way);
                        jaw = 0;
                        thrust = true;

                        if (fresh)
                            ClenchFx(npc, true);

                        // Where its jaws will be a tick on (the one it has is placed before it moves): feet in them, head out.
                        Vector2 grip = SwingMiddle(t + 1) + nextWay * (reach + HandHigh(npc.type) * 0.5f * s + 22f);
                        Grip(npc, held, grip, next + MathHelper.PiOver2, new Vector2(aside * 2.5f, -(6f + (GpThrow - 6f) * u)), true);

                        SwingFx(npc, middle, way, a, next, u);

                        if (Authority && t >= GpSwingTicks)
                        {
                            held = -1;
                            ActNext(npc, GpEnd);
                        }

                        break;
                    }

                default:
                    npc.velocity *= 0.9f;
                    jaw = t < 12 ? 1 : -1;

                    if (fresh && seenTick < 0)
                        Prime.Sound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = 0.3f }, pivot, 6);

                    if (Authority && t >= GpEndTicks)
                        EndAct(npc, prime);

                    break;
            }
        }

        // ---- dig ----------------------------------------------------------------------------------------------------------

        /// <summary>How far over the ground's top its hand's middle rides as it runs along it (its blade under).</summary>
        private const float RideHigh = 6f;

        private void RunDig(NPC npc, Prime prime, Player p, bool fresh)
        {
            int t = clock;
            Vector2 pivot = Pivot(npc);
            float s = npc.scale;
            Vector2 bite = new Vector2(aside * 0.8f, 0.6f);
            Vector2 run = new Vector2(aside, 0.3f).SafeNormalize(Vector2.UnitX);

            // Its hand's middle as its blade goes into the ground at its mark.
            Vector2 entry = mark - bite * (HandHigh(npc.type) * 0.5f * s - 10f);

            switch (actStep)
            {
                case DgRev:
                    {
                        // Over where it will go in; its saw quicker, smoking (by request: "spins faster, with smokes coming out").
                        GlidePivot(npc, mark + new Vector2(-aside * 130f, -180f), 15f, 0.1f);
                        TurnTo(npc, bite, 0.1f);
                        rev = 0.25f + 0.5f * MathHelper.Clamp(t / (float)DgRevTicks, 0f, 1f);
                        shake = 1f + 1.5f * rev;
                        RevFx(npc, rev, false);

                        if (Authority && t >= DgRevTicks)
                        {
                            mark2 = pivot;
                            acount = (int)MathHelper.Clamp(Vector2.Distance(pivot, entry) / 16f + 4f, 8f, 26f);
                            ActNext(npc, DgDive);
                        }

                        break;
                    }

                case DgDive:
                    {
                        float u = MathHelper.Clamp((t + 1f) / Math.Max(1, acount), 0f, 1f);
                        MovePivot(npc, Vector2.Lerp(mark2, entry, u * u));
                        TurnTo(npc, bite, 0.25f);
                        rev = 0.85f;
                        hurts = true;
                        RevFx(npc, rev, false);

                        if (Authority && t >= acount)
                            ActNext(npc, DgRun);

                        break;
                    }

                case DgRun:
                    {
                        if (fresh)
                        {
                            runY = mark.Y;
                            BiteFx(mark, aside, 0.7f);
                        }

                        // Along the ground, over its rises and dips; a wall ends it.
                        float gone = DgSpeed * t;
                        float x = mark.X + aside * gone;
                        bool open = Prime.SurfaceNear(x + aside * 18f, runY + 2f, 3, 6, out float top);

                        if (open)
                            runY += MathHelper.Clamp(top - runY, -14f, 14f);

                        MovePivot(npc, new Vector2(x, runY - RideHigh));
                        TurnTo(npc, run, 0.3f);
                        rev = 0.9f;
                        hurts = true;
                        shake = 2f;
                        RevFx(npc, rev, false);
                        DigFx(npc, new Vector2(x, runY), aside, 0.6f);

                        if (Authority && (gone >= aim || !open))
                            ActNext(npc, DgScoop);

                        break;
                    }

                case DgScoop:
                    {
                        // Up out of it in an arc, what it scooped thrown ahead of it and behind.
                        float u = MathHelper.Clamp((t + 1f) / DgScoopTicks, 0f, 1f);
                        MovePivot(npc, stepFrom + new Vector2(aside * 150f * u, -210f * (1f - (1f - u) * (1f - u))));
                        TurnTo(npc, Vector2.Lerp(run, new Vector2(aside * 0.3f, -1f), Prime.Smooth(u * 1.4f)).SafeNormalize(-Vector2.UnitY), 0.3f);
                        rev = 0.9f - 0.6f * u;
                        hurts = u < 0.6f;

                        if (fresh)
                            ScoopFx(stepFrom + new Vector2(0f, RideHigh), aside);

                        for (int k = 0; k < DgClods; k++)
                        {
                            if (At(2 + k * 2))
                                Clod(npc, k);
                        }

                        if (Authority && t >= DgScoopTicks)
                            ActNext(npc, DgEnd);

                        break;
                    }

                default:
                    npc.velocity *= 0.92f;
                    rev = Math.Max(0f, 0.3f - t * 0.02f);

                    if (Authority && t >= DgEndTicks)
                        EndAct(npc, prime);

                    break;
            }
        }

        /// <summary>A clod off its blade: most ahead of it, steeper and steeper, the last two back over where it came from.</summary>
        private void Clod(NPC npc, int which)
        {
            if (!Authority)
                return;

            Random dice = new(unchecked(aseed * 31 + which * 7919));
            bool back = which >= DgClods - 2;
            float lift = MathHelper.ToRadians(back ? 58f + 14f * (which - (DgClods - 2)) : 26f + 11f * which) + ((float)dice.NextDouble() - 0.5f) * 0.12f;
            float speed = 8.5f + 4.5f * (float)dice.NextDouble();
            int way = back ? -aside : aside;
            PrimeShots.Clod(npc, Tip(npc), new Vector2(way * MathF.Cos(lift), -MathF.Sin(lift)) * speed);
        }

        // ---- what is seen and heard ---------------------------------------------------------------------------------------

        private static readonly Color[] HotSparks = { new(255, 214, 120), new(255, 170, 70), PrimeArt.NuclearHot, new(255, 240, 200) };

        /// <summary>A laser leaves its lens: a glint of its green, a splinter or two ahead of it, its sound.</summary>
        private static void ShotFx(Vector2 tip, Vector2 way, float power)
        {
            if (Main.dedServ || !Prime.Seen(tip, 200f))
                return;

            Prime.Add(new BloomGeneric(tip, Vector2.Zero, PrimeArt.Nuclear * 0.9f, 0.5f * power, 8));

            for (int i = 0; i < 2; i++)
                PADParticleHandler.SpawnParticle(new SparkShard(tip, way.RotatedByRandom(0.4f) * Main.rand.NextFloat(5f, 11f), PrimeArt.NuclearHot, 12f, 3f, 40f, Main.rand.Next(8, 13)));

            Lighting.AddLight(tip, 0.7f, 1f, 0.05f);
            Prime.Sound(SoundID.Item33 with { Volume = 0.55f, Pitch = 0.1f, PitchVariance = 0.2f, MaxInstances = 6 }, tip, 6);
        }

        /// <summary>A column of laser lets go from an arm: the crack of it, its light.</summary>
        private static void BeamFx(Vector2 tip)
        {
            if (Main.dedServ)
                return;

            PADParticleHandler.SpawnParticle(new BloomGeneric(tip, Vector2.Zero, PrimeArt.NuclearHot * 0.95f, 1.5f, 10));
            PADParticleHandler.SpawnParticle(new BloomGeneric(tip, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 2.6f, 18));
            Prime.Sound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.3f, MaxInstances = 2 }, tip, 8);
            Prime.Sound(SoundID.Zombie104 with { Volume = 0.6f, Pitch = 0f, MaxInstances = 2 }, tip, 8);
            Prime.Shake(tip, 9f, 18);
        }

        /// <summary>Its lens after it has fired: smoke out of it.</summary>
        private static void LensSmokeFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 300f))
                return;

            Vector2 tip = Tip(npc), way = ToolWay(npc.rotation);

            for (int i = 0; i < 5; i++)
                Prime.Add(new MuzzleSmoke(tip, way.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.3f, 1.4f) + new Vector2(0f, -0.4f), Main.rand.Next(30, 50), Main.rand.NextFloat(0.26f, 0.42f)));

            Prime.Sound(SoundID.Item34 with { Volume = 0.35f, Pitch = 0.5f }, tip, 6);
        }

        /// <summary>A bomb let go off a mark: a clunk, a glint.</summary>
        private static void DropFx(Vector2 at)
        {
            if (Main.dedServ || !Prime.Seen(at, 200f))
                return;

            Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 0.4f, 8));
            Prime.Add(new MuzzleSmoke(at, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -0.5f), Main.rand.Next(20, 32), 0.3f));
            Prime.Sound(SoundID.Item11 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 6 }, at, 6);
        }

        /// <summary>Its jaws shut on someone: a clang; hard, if it hurts.</summary>
        private void ClenchFx(NPC npc, bool hurting)
        {
            if (Main.dedServ)
                return;

            Vector2 tip = Tip(npc);
            Prime.Add(new BloomGeneric(tip, Vector2.Zero, Color.White * 0.8f, 0.5f, 8));
            Prime.Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.1f, MaxInstances = 3 }, tip, 8);
            Prime.Sound(SoundID.Mech with { Volume = 0.9f, Pitch = 0.2f, MaxInstances = 3 }, tip, 8);
            Prime.Shake(tip, 8f, 14);

            // What its closing does to the one it closed on: their own machine's to deal.
            if (hurting && held == Main.myPlayer)
                Main.LocalPlayer.GetModPlayer<PrimeGrip>().Clench(npc);
        }

        /// <summary>As it whirls: the air off its jaws' end, the rush of it each half turn (higher each time); the one it has, shaken; their camera kept on the middle of it.</summary>
        private void SwingFx(NPC npc, Vector2 middle, Vector2 way, float a, float next, float u)
        {
            if (Main.dedServ)
                return;

            Vector2 tip = Tip(npc);

            if (held == Main.myPlayer)
            {
                PisPlayer.BlendCamera(middle, Prime.Smooth(clock / 24f));
                Main.LocalPlayer.PIS().BumpScreenShake(1f + 3f * u, 4);
            }

            // Each half turn.
            if ((int)MathF.Floor(a / MathHelper.Pi) != (int)MathF.Floor(next / MathHelper.Pi))
                Prime.Sound(SoundID.Item1 with { Volume = 0.6f + 0.4f * u, Pitch = -0.6f + 0.9f * u, MaxInstances = 4 }, tip, 6);

            if (!Prime.Seen(tip, 200f) || Main.rand.NextFloat() > (0.3f + 0.7f * u) * Prime.Room)
                return;

            Vector2 along = new Vector2(-way.Y, way.X) * aside;
            PADParticleHandler.SpawnParticle(new WindStreak(tip + way * Main.rand.NextFloat(-30f, 40f), along * Main.rand.NextFloat(2f, 5f), Main.rand.NextFloat(40f, 90f) * (0.5f + u), 2f, Main.rand.Next(8, 14)));
        }

        /// <summary>
        /// Its saw turning quicker than it should: smoke off it, sparks off its teeth, its whine (higher the quicker);
        /// <paramref name="ultra"/>: as quick as it goes, its teeth throwing its head's green.
        /// </summary>
        private void RevFx(NPC npc, float speed, bool ultra)
        {
            if (Main.dedServ)
                return;

            Vector2 tip = Tip(npc), way = ToolWay(npc.rotation);
            Vector2 blade = tip - way * 22f * npc.scale;

            if (clock % 7 == 0)
                Prime.Sound(SoundID.Item22 with { Volume = 0.35f + 0.45f * speed, Pitch = -0.3f + (ultra ? 1.1f : 0.8f) * speed, MaxInstances = 4 }, blade, 6);

            if (ultra)
                Lighting.AddLight(blade, 0.6f * speed, 0.8f * speed, 0.05f);
            else
                Lighting.AddLight(blade, 0.5f * speed, 0.3f * speed, 0.05f);

            if (!Prime.Seen(blade, 200f))
                return;

            float room = Prime.Room;

            if (Main.rand.NextFloat() < 0.5f * speed * room)
                Prime.Add(new MuzzleSmoke(blade + Main.rand.NextVector2Circular(10f, 10f), new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.6f)) + npc.velocity * 0.1f,
                    Main.rand.Next(24, 40), Main.rand.NextFloat(0.24f, 0.4f)));

            if (Main.rand.NextFloat() < (ultra ? 0.9f : 0.35f) * speed * room)
            {
                Vector2 across = new Vector2(-way.Y, way.X) * (Main.rand.NextBool() ? 1f : -1f);
                Color c = ultra && Main.rand.NextBool() ? PrimeArt.Nuclear : HotSparks[Main.rand.Next(HotSparks.Length)];
                PADParticleHandler.SpawnParticle(new SparkShard(tip - way * Main.rand.NextFloat(0f, 30f), (across + way * Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(4f, 10f), c, 10f, 2.5f, 34f,
                    Main.rand.Next(8, 14), falls: true));
            }
        }

        /// <summary>Its blade goes into the ground: the ground's dust thrown up, a crunch.</summary>
        private static void BiteFx(Vector2 at, int way, float power)
        {
            if (Main.dedServ || !Prime.Seen(at, 300f))
                return;

            float room = Prime.Room;
            Color dust = new(118, 104, 92);

            for (int i = 0; i < (int)(10 * power * room); i++)
                Prime.Add(new StoneChip(at + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f), new Vector2(-way * Main.rand.NextFloat(0.5f, 4f), -Main.rand.NextFloat(3f, 9f)),
                    Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0f, 0.4f)), Main.rand.Next(40, 70), Main.rand.NextFloat(0.7f, 1.1f)));

            for (int i = 0; i < (int)(6 * power * room); i++)
                Prime.Add(new DustFog(at + new Vector2(Main.rand.NextFloat(-30f, 30f), -6f), new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.3f, 1.2f)), dust * 0.8f,
                    Main.rand.Next(40, 70), Main.rand.NextFloat(0.5f, 0.9f), 0.3f));

            TileDust(at, 8, 5f);
            Prime.Sound(SoundID.DD2_OgreGroundPound with { Volume = 0.5f + 0.4f * power, Pitch = 0.3f }, at, 6);
            Prime.Sound(SoundID.WormDig with { Volume = 0.8f, Pitch = -0.2f }, at, 6);
            Prime.Shake(at, 6f + 8f * power, 14);
        }

        /// <summary>The dust of the ground at a place, thrown up.</summary>
        private static void TileDust(Vector2 at, int many, float up)
        {
            for (int i = 0; i < many; i++)
            {
                int tx = (int)((at.X + Main.rand.NextFloat(-24f, 24f)) / 16f), ty = (int)(at.Y / 16f) + Main.rand.Next(0, 2);

                if (!WorldGen.InWorld(tx, ty, 2) || !Main.tile[tx, ty].HasTile)
                    continue;

                int d = WorldGen.KillTile_MakeTileDust(tx, ty, Main.tile[tx, ty]);

                if (d >= 0 && d < Main.maxDust)
                    Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, up));
            }
        }

        /// <summary>As it runs along the ground: earth and stone thrown up and back off its blade, the ground's own dust, a haze left over its furrow; the grind of it.</summary>
        private void DigFx(NPC npc, Vector2 at, int way, float power)
        {
            if (Main.dedServ)
                return;

            if (clock % 8 == 0)
                Prime.Sound(SoundID.WormDig with { Volume = 0.6f, Pitch = 0.2f, MaxInstances = 3 }, at, 6);

            if (clock % 6 == 0)
                Prime.Shake(at, 3f * power + 1f, 8);

            if (!Prime.Seen(at, 240f))
                return;

            float room = Prime.Room;
            Color dust = new(118, 104, 92);

            for (int i = 0; i < 2; i++)
            {
                if (Main.rand.NextFloat() < room)
                    Prime.Add(new StoneChip(at + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f), new Vector2(-way * Main.rand.NextFloat(1f, 6f), -Main.rand.NextFloat(3f, 9f)) * (0.7f + power * 0.5f),
                        Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0f, 0.4f)), Main.rand.Next(36, 60), Main.rand.NextFloat(0.6f, 1f)));
            }

            if (clock % 2 == 0 && Main.rand.NextFloat() < room)
                Prime.Add(new DustFog(at + new Vector2(-way * Main.rand.NextFloat(0f, 30f), -6f), new Vector2(-way * Main.rand.NextFloat(0.2f, 1.4f), -Main.rand.NextFloat(0.2f, 0.9f)), dust * 0.75f,
                    Main.rand.Next(40, 70), Main.rand.NextFloat(0.45f, 0.8f), 0.28f));

            TileDust(at, 2, 5f);
        }

        /// <summary>It comes up out of the ground: a spout of earth the way it was going, the ground's dust, a heavy thump.</summary>
        private static void ScoopFx(Vector2 at, int way)
        {
            if (Main.dedServ || !Prime.Seen(at, 300f))
                return;

            float room = Prime.Room;
            Color dust = new(118, 104, 92);
            Vector2 spout = new Vector2(way * 0.7f, -1f).SafeNormalize(-Vector2.UnitY);

            for (int i = 0; i < (int)(16 * room); i++)
                Prime.Add(new StoneChip(at + new Vector2(Main.rand.NextFloat(-16f, 16f), -4f), spout.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 12f),
                    Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0f, 0.4f)), Main.rand.Next(50, 80), Main.rand.NextFloat(0.8f, 1.2f)));

            for (int i = 0; i < (int)(9 * room); i++)
                Prime.Add(new DustFog(at + new Vector2(Main.rand.NextFloat(-40f, 40f), -8f), spout.RotatedByRandom(0.8f) * Main.rand.NextFloat(0.6f, 3f), dust * 0.8f,
                    Main.rand.Next(44, 76), Main.rand.NextFloat(0.6f, 1f), 0.3f));

            TileDust(at, 10, 6f);
            Prime.Sound(SoundID.DeerclopsRubbleAttack with { Volume = 0.8f, Pitch = 0f }, at, 6);
            Prime.Sound(SoundID.DD2_OgreGroundPound with { Volume = 0.6f, Pitch = 0.4f }, at, 6);
            Prime.Shake(at, 10f, 16);
        }

        // ---- drawn ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Over its hand, where Legends has it doing more than vanilla's does: its saw's blur, as quick as it turns (its
        /// bar doubled a little to either side, a hot light down it, its teeth's edges bright); its lens's light.
        /// </summary>
        private void DrawLegendsOver(NPC npc, SpriteBatch batch, Vector2 screenPos, Texture2D hand, Vector2 pivot, Vector2 way, SpriteEffects flip)
        {
            float s = npc.scale;

            if (npc.type == NPCID.PrimeSaw && rev > 0.03f)
            {
                Vector2 across = new(-way.Y, way.X);
                Vector2 half = npc.frame.Size() * 0.5f;
                Color ghost = new Color(150, 150, 150, 0) * (0.35f * rev);

                for (int k = -1; k <= 1; k += 2)
                    batch.Draw(hand, pivot + across * k * (1.5f + 2f * rev) - screenPos, npc.frame, ghost, npc.rotation, half, s, flip, 0f);

                // Its bar: from under its collar to its end.
                Vector2 from = pivot - way * 4f * s, to = pivot + way * HandHigh(npc.type) * 0.5f * s;
                Color heat = Color.Lerp(new Color(255, 150, 50), lampGreen ? PrimeArt.Nuclear : new Color(255, 214, 120), rev);
                DestroyerArt.DrawOval(batch, (from + to) * 0.5f, way.ToRotation(), Vector2.Distance(from, to) * 1.5f, 46f * s, PrimeArt.Lit(heat * (0.4f * rev)));

                for (int k = -1; k <= 1; k += 2)
                    DestroyerArt.DrawLine(batch, from + across * k * 13f * s, to + across * k * 9f * s, 3f, PrimeArt.Lit(Color.Lerp(heat, Color.White, 0.5f) * (0.55f * rev)));
            }

            if (npc.type == NPCID.PrimeLaser && charge > 0.03f)
            {
                Vector2 tip = pivot + way * HandHigh(npc.type) * 0.5f * s;
                float beat = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 37f);
                DestroyerArt.DrawGlow(batch, tip, (26f + 70f * charge) * beat, PrimeArt.Lit(PrimeArt.Nuclear * (0.25f + 0.4f * charge)));
                DestroyerArt.DrawGlow(batch, tip, (10f + 26f * charge) * beat, PrimeArt.Lit(PrimeArt.NuclearHot * (0.4f + 0.5f * charge)), hotCore: true);
            }
        }

        /// <summary>What its own attacks warn with (smooth, over it).</summary>
        private void DrawOwnMarks(NPC npc, SpriteBatch batch)
        {
            int t = actT;
            Vector2 tip = Tip(npc);

            switch (act)
            {
                case Act.Burst:
                    if (actStep == BuFire)
                    {
                        // A moment before each barrage: the way it points; fading as its first shot goes.
                        int[] when = BurstShots();

                        for (int b = 0; b < when.Length; b += 3)
                        {
                            int left = when[b] - t;

                            if (left <= -8 || left > BuTell)
                                continue;

                            float ready = MathHelper.Clamp(1f - left / (float)BuTell, 0f, 1f);
                            float alpha = left > 0 ? Prime.Smooth(ready * 3f) : 1f + left / 8f;
                            Prime.DrawMark(batch, tip, ToolWay(npc.rotation).ToRotation(), 900f, ready, alpha);
                            break;
                        }
                    }

                    break;

                case Act.Corner:
                    if (actStep == CoWarn || actStep == CoSweep && t < DestroyerArt.WarningFade)
                    {
                        // Straight down, as wide as it will be; and, fainter, the way it will go.
                        bool warn = actStep == CoWarn;
                        float ready = warn ? t / (float)CoWarnTicks : 1f;
                        float alpha = warn ? Prime.Smooth(t / 10f) : 1f - Prime.Smooth(t / (float)DestroyerArt.WarningFade);

                        if (warn)
                            Prime.DrawWarn(batch, tip, MathHelper.PiOver2, 1500f, ready, t >= CoWarnTicks - 10, PrimeBeam.ArmWide, alpha);

                        for (int k = 1; k <= 6; k++)
                            Prime.DrawMark(batch, tip + new Vector2(-aside * 54f * k, 0f), MathHelper.PiOver2, 1200f, 0.08f, 0.5f * (1f - k / 7f) * alpha);
                    }

                    break;

                case Act.Grapple:
                    if (actStep == GpAim)
                    {
                        Player p = HeadOf(npc, out NPC head, out _) ? Prime.Target(head) : null;
                        bool locked = t >= GpLockAt;
                        float along = locked || p == null ? aim : (p.Center - Pivot(npc)).ToRotation();
                        Prime.DrawWarn(batch, tip, along, GpReach, t / (float)GpAimTicks, locked, 30f, Prime.Smooth(t / 10f));
                    }
                    else if (actStep == GpShoot)
                    {
                        Prime.DrawWarn(batch, tip, aim, Math.Max(8f, Vector2.Distance(tip, mark)), 1f, true, 30f, 1f - Prime.Smooth(t / (float)DestroyerArt.WarningFade));
                    }

                    break;

                case Act.Dig:
                    if (actStep <= DgRun)
                    {
                        float ready = actStep == DgRev ? t / (float)DgRevTicks : 1f;
                        float alpha = actStep == DgRev ? Prime.Smooth(t / 12f) : 1f;
                        Prime.DrawGroundMark(batch, mark, aside, aim, ready, actStep != DgRev, 40f, alpha, actStep == DgRun ? DgSpeed * t + 30f : 0f);
                    }

                    break;
            }
        }

        // ---- told across the network ----------------------------------------------------------------------------------------

        private void SendLegends(BinaryWriter writer)
        {
            writer.Write((byte)act);
            writer.Write((byte)actStep);
            writer.Write((short)Math.Clamp(actT, 0, short.MaxValue));
            writer.Write((sbyte)aside);
            writer.Write((byte)Math.Clamp(acount, 0, 255));
            writer.Write((short)held);
            writer.Write(aseed);
            writer.Write(aim);
            writer.Write(mark.X);
            writer.Write(mark.Y);
            writer.Write(mark2.X);
            writer.Write(mark2.Y);
        }

        private void ReceiveLegends(BinaryReader reader)
        {
            act = (Act)reader.ReadByte();
            actStep = reader.ReadByte();
            actT = reader.ReadInt16();
            aside = reader.ReadSByte();
            acount = reader.ReadByte();
            held = reader.ReadInt16();
            aseed = reader.ReadInt32();
            aim = reader.ReadSingle();
            mark = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            mark2 = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
