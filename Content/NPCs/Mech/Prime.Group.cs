using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its arms' attacks together (Legends) -------------------------------------------------------------------------------
    //
    // Every arm stops what it is doing for one of these ("All special attacks make other arms stop, decide on one"). Its
    // head keeps the clock (its step and its ticks, as in its coming and its end) and the marks; each arm plays its part
    // from that on every machine (PrimeArm.Group). Its head cannot be hurt through any of them; its arms can, and an arm
    // struck down falls out of it (the one that leads it struck down: it is over).
    //
    //   LASER COLUMN   "head slow down, moves to above shoudler, every other arm hold the base of this one and this one
    //                  has a spark of green and a column laser 3 tiles wide is shot out, 400 damage. and the enter
    //                  combination of holding arms and laser moves with the anchor at the shoulder part around a circle
    //                  encapsulating the hitbox of the skull. slowly for 4 secs, gotta have indicator"
    //                    GATHER 70   its head goes up over the player and stops; its laser sets its far end on the circle
    //                                about its skull, pointing out; its other arms take hold of that end, braced behind it
    //                    AIM    70   its lamp sparks green, its lens charges (unmarked, by request: "doing too much
    //                                indicator")
    //                    FIRE  240   the column; the whole of it goes round the circle, slowly, to where it stops
    //                    END    40
    //                  It begins on the far side of the player from straight under it and comes in through where they
    //                  were and on past under it: outrun the way it goes, toward and under its head and out the other side.
    //
    //   NUKE DROP      "every arms hold the base as this arm hold up and shoot 3 of something black onto the sky in
    //                  succession, and its falling down Nukes with parachute, the light sparks on the hand and changes to
    //                  green and all those nukes explodes leaving a radiactive area radius 10 tiles"
    //                    GATHER 56   its cannon set on its crown, pointing up, its other arms braced under it
    //                    FIRE   80   three shells, black, up off the top of the screen
    //                    FALL  200   they come down under parachutes, one over the player and one to either side, in a
    //                                new order each time, each to its own height about the player's floor (one high,
    //                                one about their head, one low), their lamps red; near the end its cannon's lamp
    //                                sparks and goes green, theirs with it, and they go up together in the air
    //                                (PrimeNuke): where each was radioactive thirty seconds. Nothing marks where.
    //                    END    50
    //
    //   VICE HOLD      "every other arms cease in place and this one dashes onto player's position at -15 ticks x4, if
    //                  land on player, it holds player tight and other arms flies to above player, pointing down to him
    //                  and shoot barrages versions of vanilla and swing saw crazily"
    //                    WIND   40   its vice draws back, open; a line from it to where the player was fifteen ticks ago
    //                    AIM 30, GO 14, REST 12, four times: at where they were fifteen ticks before it goes
    //                    HOLD  120   (if it closed on a player: its touch does not hurt, it takes hold) held tight; its
    //                                laser and cannon over them, firing down; its saw swinging through them
    //                    END    40
    //
    //   SAW TRAIN      "Spins ultrafast, creating visual effect, hit onto the nearest ground and have kick back onto
    //                  player at ultra fast speed, pulling player onto the saw if they near, just like a high speed
    //                  train. make sure to add effect of sparks from kickback"
    //                    REV    70   to one side of the player, over the ground, its blade a blur; its way marked: down,
    //                                and along the ground through the player and on
    //                    DROP        down onto the ground under it
    //                    RUN         kicked along the ground at thirty-four pixels a tick, sparks and earth thrown up
    //                                behind it, whoever is near carried toward it
    //                    END    40   up off the ground
    //
    //   SPIN           "All attach to the head like at spawn, clicks onto it with nuclear sparks and steam gas and spin
    //                  around, head spins as well, each arm does its thing during spin: vice goes up and down a path
    //                  perpendicular to the tangent line at attached point of the circle of the head, saw sway side by
    //                  side, total angle path is 90 degrees, lasers shoots laser at 17 ticks, cannon shoots bomb at 30
    //                  ticks; it spins and approach slowly to player"
    //                    GATHER 56, LOCK 24 (the click), TURN 480 (four turns and a little; closing on the player), END 48
    //
    //   CLASH          "points at player with indicator like twins death, the clash onto each other creating supersonic
    //                  wave"
    //                    POST   84   each arm to its place about the player, a line from it to them
    //                    LOCK   18   the lines stop on where the player was
    //                    RUN    16   every arm down its line to that place at once
    //                    HIT    56   they meet; the wave goes out (PrimeShockwave); they are thrown back
    //                    END    30

    public partial class Prime
    {
        // ---- the laser's column ---------------------------------------------------------------------------------------------

        internal const int LcGather = 0, LcAim = 1, LcFire = 2, LcEnd = 3;
        internal const int LcGatherTicks = 70, LcAimTicks = 70, LcFireTicks = 240, LcEndTicks = 40;

        /// <summary>How far over the player its head holds for it: its laser's tool then clears their head.</summary>
        private const float LcHigh = 430f;

        /// <summary>How far round from the player the column begins, and how far past them it goes.</summary>
        private const float LcLead = 0.87f, LcPast = 1.05f;

        /// <summary>
        /// The circle about its skull an arm's far end is set on in a brace (by request: "a circle encapsulating the
        /// hitbox of the skull"), and how far round from the leading arm the two arms to either side of it are set.
        /// </summary>
        internal const float BraceReach = 80f;
        internal const float BraceFan = 0.92f;

        // ---- the cannon's nukes ---------------------------------------------------------------------------------------------

        internal const int NdGather = 0, NdFire = 1, NdFall = 2, NdEnd = 3;
        internal const int NdGatherTicks = 56, NdFireTicks = 80, NdFallTicks = 200, NdEndTicks = 50;

        /// <summary>Ticks into FIRE each shell goes; ticks into FALL its cannon's lamp goes green (theirs with it).</summary>
        internal static readonly int[] NdShots = { 12, 36, 60 };
        internal const int NdGreenAt = NdFallTicks - 56;

        /// <summary>How far to either side of the player the outer two come down.</summary>
        internal const float NdApart = 410f;

        /// <summary>
        /// How far over the middle of a player standing where the player stood they go up, in the air and each at its own
        /// height (by request: "detonate mid air"; "different heigh of detonation, can be lower and above the player"):
        /// one well over their head, one about it, one under their feet. Which gets which is shuffled, and each is a
        /// little more or less every time. Solid ground holds one up over itself; a platform lets it hang under.
        /// </summary>
        internal static readonly float[] NdHeights = { 120f, 48f, -48f };
        internal const float NdHeightJitter = 14f;

        // ---- the vice's hold -------------------------------------------------------------------------------------------------

        internal const int VhWind = 0, VhAim = 1, VhGo = 2, VhRest = 3, VhHold = 4, VhEnd = 5;
        internal const int VhWindTicks = 40, VhAimTicks = 30, VhGoTicks = 14, VhRestTicks = 12, VhHoldTicks = 120, VhEndTicks = 40;

        /// <summary>Its dashes; how far behind the player each is aimed (by request: "player's position at -15 ticks x4"); how far past that it carries.</summary>
        internal const int VhDashes = 4;
        internal const int VhLag = 15;
        internal const float VhOver = 110f;

        // ---- the saw's train -------------------------------------------------------------------------------------------------

        internal const int StRev = 0, StDrop = 1, StRun = 2, StEnd = 3;
        internal const int StRevTicks = 70, StEndTicks = 40;

        /// <summary>How fast it runs, how far past the player, the least and the most it runs; where it revs from the player.</summary>
        internal const float StSpeed = 34f;
        private const float StPast = 560f, StLeast = 700f, StMost = 1500f;
        internal const float StAside = 460f, StHigh = 210f;

        /// <summary>How near it carries a player toward it, and how fast at the most.</summary>
        internal const float StPullReach = 250f, StPull = 6f;

        // ---- the spin --------------------------------------------------------------------------------------------------------

        internal const int SpGather = 0, SpLock = 1, SpTurn = 2, SpEnd = 3;
        internal const int SpGatherTicks = 56, SpLockTicks = 24, SpTurnTicks = 480, SpEndTicks = 48;

        /// <summary>Ticks into LOCK its arms click home; ticks its turning takes to come up to speed; its whole turns.</summary>
        internal const int SpClickAt = 6;
        private const int SpRamp = 50;
        private const int SpTurns = 4;

        /// <summary>Its turning at full, a tick: so that it comes to rest, slowing through END, after whole turns exactly (upright).</summary>
        private const float SpRate = SpTurns * MathHelper.TwoPi / (SpTurnTicks - SpRamp * 0.5f + SpEndTicks * 0.5f);

        /// <summary>How fast it closes on the player, and how much harder its touch is, spinning.</summary>
        private const float SpSpeed = 1.7f;
        private const float SpinHurt = 1.5f;

        // ---- the clash -------------------------------------------------------------------------------------------------------

        internal const int ClPost = 0, ClLock = 1, ClRun = 2, ClHit = 3, ClEnd = 4;
        internal const int ClPostTicks = 84, ClLockTicks = 18, ClRunTicks = 16, ClHitTicks = 56, ClEndTicks = 30;

        /// <summary>How far from the player its arms take their places, and how far round from straight over them the outermost are.</summary>
        internal const float ClFar = 500f;
        internal const float ClSpread = 1.22f;

        // ---- run ---------------------------------------------------------------------------------------------------------------

        private void RunGroup(NPC npc, bool entered)
        {
            Player p = Target(npc);
            int lead = LeadType(act);
            int up = ArmsUp(npc, null, -1, out _);
            bool can = lead >= 0 ? ArmOf(npc, lead, out _) : up >= (act == Act.Clash && step < ClHit ? 2 : 1);

            if (p == null || !can || !PADWorld.Legends || Main.IsItDay())
            {
                // Nobody to do it to, or nothing to do it with (or, on a machine that only watches, word of it not here yet).
                npc.velocity *= 0.92f;
                Settle(npc);

                if (Authority)
                    EndGroup(npc);

                return;
            }

            TrackTarget(p);

            // Where it is, told again now and then: its way depends on a player every machine sees a little differently.
            if (Authority && timer % 20 == 10)
                npc.netUpdate = true;

            eyeHeat = Math.Max(0f, eyeHeat - 0.04f);

            switch (act)
            {
                case Act.LaserColumn: RunLaserColumn(npc, entered, p); break;
                case Act.NukeDrop: RunNukeDrop(npc, entered, p); break;
                case Act.ViceHold: RunViceHold(npc, entered, p); break;
                case Act.SawTrain: RunSawTrain(npc, entered, p); break;
                case Act.Spin: RunSpin(npc, entered, p); break;
                case Act.Clash: RunClash(npc, entered, p, up); break;

                default:
                    if (Authority)
                        EndGroup(npc);

                    break;
            }
        }

        // ---- the laser's column ---------------------------------------------------------------------------------------------

        /// <summary>The way its laser's column points at a tick of a step: at rest up and out to its side, round to where it begins as it aims, and on round as it fires.</summary>
        internal float ColumnWay(int atStep, int clock)
        {
            float rest = new Vector2(SideOf(NPCID.PrimeLaser) * 0.8f, -0.6f).ToRotation();

            switch (atStep)
            {
                case LcGather:
                    return rest;

                case LcAim:
                    return rest.AngleLerp(angle, Smooth(clock / 34f));

                case LcFire:
                    {
                        float u = MathHelper.Clamp(clock / (float)LcFireTicks, 0f, 1f);
                        return angle + span * MathHelper.Lerp(u, Smooth(u), 0.35f);
                    }

                default:
                    return angle + span;
            }
        }

        /// <summary>
        /// Where an arm is in a brace: the leading arm's far end set on the circle about its skull, the arm pointing out
        /// the way given (pushed back in by <paramref name="recoil"/>); the others' tools on that end, one straight
        /// behind it (through its skull, its jet out the far side) and one fanned to either side of that.
        /// </summary>
        internal static void BracePose(NPC head, NPC limb, bool lead, int rank, float way, float recoil, out Vector2 pivot, out float turn)
        {
            Vector2 dir = way.ToRotationVector2();
            Vector2 foot = Middle(head) + dir * (BraceReach * head.scale - recoil);

            if (lead)
            {
                pivot = foot + dir * PrimeArm.TailBack(limb.type) * limb.scale;
                turn = PrimeArm.TurnOf(dir);
                return;
            }

            float off = rank switch
            {
                0 => 0f,
                1 => BraceFan,
                _ => -BraceFan,
            };

            Vector2 hold = (way + off).ToRotationVector2();
            pivot = foot - hold * (PrimeArm.HandHigh(limb.type) * 0.5f * limb.scale - 6f);
            turn = PrimeArm.TurnOf(hold);
        }

        /// <summary>Where the leading arm's tool ends in a brace pointing a way: where its column leaves.</summary>
        internal static Vector2 BraceTip(NPC head, NPC lead, float way) =>
            Middle(head) + way.ToRotationVector2() * (BraceReach * head.scale + (PrimeArm.TailBack(lead.type) + PrimeArm.HandHigh(lead.type) * 0.5f) * lead.scale);

        private void RunLaserColumn(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case LcGather:
                    Glide(npc, p.Center + new Vector2(0f, -LcHigh), 11f, 0.08f);
                    Settle(npc);

                    if (entered)
                        Whir(npc, 1f);

                    if (Authority && t >= LcGatherTicks)
                    {
                        // It begins on the far side of the player from straight under it (either, if they are under it),
                        // and comes in through them and on past.
                        float bearing = (p.Center - Middle(npc)).ToRotation();
                        float off = MathHelper.WrapAngle(bearing - MathHelper.PiOver2);
                        int outer = Math.Abs(off) < 0.14f ? side : Math.Sign(off);
                        angle = bearing + outer * LcLead;
                        span = -outer * (LcLead + LcPast);
                        Next(npc, LcAim);
                    }

                    break;

                case LcAim:
                    npc.velocity *= 0.85f;
                    Settle(npc);
                    shake = 1.6f * Math.Min(1f, t / (float)LcAimTicks);

                    if (entered && !Main.dedServ)
                    {
                        StopSounds();
                        chargeSlot = SoundEngine.PlaySound(ChargeSound with { Pitch = 0.4f, Volume = 0.8f }, npc.Center);
                    }

                    if (Authority && t >= LcAimTicks)
                        Next(npc, LcFire);

                    break;

                case LcFire:
                    npc.velocity *= 0.85f;
                    Settle(npc);
                    shake = 2.6f;

                    if (entered)
                    {
                        StopSounds();

                        if (ArmOf(npc, NPCID.PrimeLaser, out NPC laser))
                        {
                            PrimeShots.Column(laser, LcFireTicks);
                            ColumnFx(PrimeArm.Tip(laser));
                        }
                    }

                    if (t % 7 == 0)
                        Shake(npc.Center, 3.5f, 9);

                    if (Authority && t >= LcFireTicks)
                        Next(npc, LcEnd);

                    break;

                default:
                    Glide(npc, p.Center + new Vector2(0f, -330f), 3f, 0.04f);
                    Settle(npc);

                    if (Authority && t >= LcEndTicks)
                        EndGroup(npc);

                    break;
            }
        }

        /// <summary>The column lets go: the crack of it, its light over everything for a moment.</summary>
        private static void ColumnFx(Vector2 at)
        {
            if (Main.dedServ)
                return;

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearHot * 0.95f, 2.2f, 12));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 4f, 22));
            Sound(SoundID.Zombie104 with { Volume = 0.85f, Pitch = -0.3f, MaxInstances = 2 }, at, 8);
            Sound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 2 }, at, 8);
            Sound(SoundID.Item62 with { Volume = 0.7f, Pitch = -0.6f, MaxInstances = 2 }, at, 8);
            Shake(at, 16f, 30);

            if (Watching(at))
                Cinematic.Flash(PrimeArt.Nuclear, 0.14f);
        }

        // ---- the cannon's nukes ---------------------------------------------------------------------------------------------

        private void RunNukeDrop(NPC npc, bool entered, Player p)
        {
            int t = timer;
            Glide(npc, p.Center + new Vector2(0f, -330f), step == NdGather ? 6f : 2.4f, 0.05f);
            Settle(npc);

            switch (step)
            {
                case NdGather:
                    if (entered)
                        Whir(npc, 1f);

                    if (Authority && t >= NdGatherTicks)
                        Next(npc, NdFire);

                    break;

                case NdFire:
                    // Kicked down by each shell.
                    foreach (int shot in NdShots)
                    {
                        if (t >= shot)
                            shake = Math.Max(shake, 4f * MathF.Exp(-(t - shot) / 5f));
                    }

                    if (Authority && t >= NdFireTicks)
                        Next(npc, NdFall);

                    break;

                case NdFall:
                    if (Authority && t >= NdFallTicks)
                        Next(npc, NdEnd);

                    break;

                default:
                    if (Authority && t >= NdEndTicks)
                        EndGroup(npc);

                    break;
            }
        }

        /// <summary>
        /// Where a shell of its cannon's goes up, and how long from now (the server's). The first lays out the row: three
        /// places, on the player, ahead of them and behind, each at its own height about the floor they stand on. Which
        /// shell goes to which place, and which height, is new each time (by request: "variance of what is dropped
        /// first"); every fuse ends on the same tick, so they all go up together.
        /// </summary>
        internal void NukePlan(NPC head, int which, int clock, out Vector2 dropAt, out int fuse)
        {
            Player p = Target(head) ?? Main.player[Player.FindClosest(head.position, head.width, head.height)];

            if (which == 0)
            {
                if (Math.Abs(p.velocity.X) > 0.5f)
                    side = Math.Sign(p.velocity.X);

                // Over the floor under their feet (not where a jump has them), or where they are if there is none near.
                float level = p.Center.Y;
                float floor = float.MaxValue;

                foreach (float x in new[] { p.position.X + 2f, p.Center.X, p.position.X + p.width - 2f })
                {
                    if (FloorUnder(x, p.Bottom.Y - 2f, 16, out float top))
                        floor = Math.Min(floor, top);
                }

                if (floor < float.MaxValue)
                    level = floor - p.height * 0.5f;

                markA = new Vector2(p.Center.X, level);
            }

            float at = markA.X + Shuffled(41)[Math.Clamp(which, 0, 2)] switch
            {
                0 => 0f,
                1 => side * NdApart,
                _ => -side * NdApart,
            };

            float jitter = ((float)Dice(47 + which).NextDouble() * 2f - 1f) * NdHeightJitter;
            float y = markA.Y - NdHeights[Shuffled(43)[Math.Clamp(which, 0, 2)]] - jitter;

            // Never in solid ground (the low one, or any off to one side where the ground rises): just over it.
            const float half = 22f;

            if (Solid((int)(at / 16f), (int)((y + half) / 16f)) && SurfaceNear(at, y + half, 16, 0, out float surface))
                y = surface - half - 2f;

            dropAt = new Vector2(at, y);
            fuse = NdFireTicks - clock + NdFallTicks;
        }

        /// <summary>0, 1 and 2 in an order of the attack's seed's (and the salt's): for a volley's shells, which place, which height.</summary>
        private int[] Shuffled(int salt)
        {
            int[] order = { 0, 1, 2 };
            Random dice = Dice(salt);

            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = dice.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        // ---- the vice's hold -------------------------------------------------------------------------------------------------

        private void RunViceHold(NPC npc, bool entered, Player p)
        {
            int t = timer;
            Glide(npc, p.Center + new Vector2(0f, -330f), 3f, 0.05f);
            Settle(npc);

            switch (step)
            {
                case VhWind:
                    if (Authority && t >= VhWindTicks)
                        Next(npc, VhAim);

                    break;

                case VhAim:
                    if (Authority && t >= VhAimTicks && ArmOf(npc, NPCID.PrimeVice, out NPC vice))
                    {
                        // At where they were, not where they are.
                        markA = Lagged(p, VhLag);
                        markB = PrimeArm.Pivot(vice);
                        Next(npc, VhGo);
                    }

                    break;

                case VhGo:
                    if (Authority && t >= VhGoTicks)
                        Next(npc, VhRest);

                    break;

                case VhRest:
                    if (Authority && t >= VhRestTicks)
                    {
                        if (count + 1 < VhDashes)
                        {
                            count++;
                            Next(npc, VhAim);
                        }
                        else
                        {
                            Next(npc, VhEnd);
                        }
                    }

                    break;

                case VhHold:
                    {
                        Player held = grabbed >= 0 && grabbed < Main.maxPlayers ? Main.player[grabbed] : null;

                        if (Authority && (held == null || !held.active || held.dead || t >= VhHoldTicks))
                            Next(npc, VhEnd);

                        break;
                    }

                default:
                    if (Authority && t >= VhEndTicks)
                        EndGroup(npc);

                    break;
            }
        }

        /// <summary>Its vice has closed on a player (the server's): held, its other arms brought over them.</summary>
        internal void ViceCaught(NPC head, int who, Vector2 at)
        {
            if (!Authority || scene != Scene.Group || act != Act.ViceHold || step != VhGo)
                return;

            grabbed = who;
            markA = at;
            Next(head, VhHold);
        }

        // ---- the saw's train -------------------------------------------------------------------------------------------------

        private void RunSawTrain(NPC npc, bool entered, Player p)
        {
            int t = timer;
            Glide(npc, p.Center + new Vector2(0f, -330f), 3f, 0.05f);
            Settle(npc);

            switch (step)
            {
                case StRev:
                    if (Authority && t >= StRevTicks && ArmOf(npc, NPCID.PrimeSaw, out NPC saw))
                    {
                        // Down onto the ground under it, and along that at the player and on past.
                        markB = PrimeArm.Pivot(saw);
                        float top = GroundUnder(markB.X, markB.Y, 100, out float found) ? found : Math.Max(markB.Y + 160f, p.Bottom.Y);
                        markA = new Vector2(markB.X, top);
                        float dx = p.Center.X - markB.X;

                        if (Math.Abs(dx) > 40f)
                            side = Math.Sign(dx);

                        span = MathHelper.Clamp(Math.Abs(dx) + StPast, StLeast, StMost);
                        count = (int)MathHelper.Clamp((top - markB.Y) / 26f + 5f, 8f, 22f);
                        Next(npc, StDrop);
                    }

                    break;

                case StDrop:
                    if (Authority && t >= count)
                        Next(npc, StRun);

                    break;

                case StRun:
                    if (Authority && t >= span / StSpeed)
                        Next(npc, StEnd);

                    break;

                default:
                    if (Authority && t >= StEndTicks)
                        EndGroup(npc);

                    break;
            }
        }

        /// <summary>Its saw has met a wall it cannot climb: its run is over (the server's).</summary>
        internal void SawBlocked(NPC head)
        {
            if (Authority && scene == Scene.Group && act == Act.SawTrain && step == StRun)
                Next(head, StEnd);
        }

        // ---- the spin --------------------------------------------------------------------------------------------------------

        /// <summary>How far round it has turned at a tick of a step: up to speed over its first ticks, steady, and slowing to rest through END.</summary>
        internal float SpinTurn(int atStep, int clock)
        {
            float t = Math.Max(0f, clock);

            switch (atStep)
            {
                case SpTurn:
                    t = Math.Min(t, SpTurnTicks);
                    return side * SpRate * (t < SpRamp ? t * t / (2f * SpRamp) : t - SpRamp * 0.5f);

                case SpEnd:
                    t = Math.Min(t, SpEndTicks);
                    return side * SpRate * (SpTurnTicks - SpRamp * 0.5f + t - t * t / (2f * SpEndTicks));

                default:
                    return 0f;
            }
        }

        /// <summary>Its turning at a tick of a step, as a share of its full.</summary>
        private static float SpinShare(int atStep, int clock) => atStep switch
        {
            SpTurn => MathHelper.Clamp(clock / (float)SpRamp, 0f, 1f),
            SpEnd => 1f - MathHelper.Clamp(clock / (float)SpEndTicks, 0f, 1f),
            _ => 0f,
        };

        private void RunSpin(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case SpGather:
                    npc.velocity *= 0.92f;
                    Settle(npc);

                    if (entered)
                        Whir(npc, 1f);

                    if (Authority && t >= SpGatherTicks)
                        Next(npc, SpLock);

                    break;

                case SpLock:
                    npc.velocity *= 0.85f;
                    npc.rotation = npc.rotation.AngleLerp(0f, 0.3f);

                    if (At(SpClickAt))
                        ClickFx(npc);

                    if (t >= SpClickAt)
                        shake = 5f * MathF.Exp(-(t - SpClickAt) / 5f);

                    if (Authority && t >= SpLockTicks)
                        Next(npc, SpTurn);

                    break;

                default:
                    {
                        bool turning = step == SpTurn;
                        float share = SpinShare(step, t);
                        Vector2 want = turning ? (p.Center - npc.Center).SafeNormalize(Vector2.Zero) * SpSpeed : Vector2.Zero;
                        npc.velocity = Vector2.Lerp(npc.velocity, want, 0.05f);
                        npc.rotation = SpinTurn(step, t);
                        hurts = share > 0.3f;
                        hurtScale = SpinHurt;
                        SpinTick(npc, share);

                        if (Authority && t >= (turning ? SpTurnTicks : SpEndTicks))
                        {
                            if (turning)
                            {
                                Next(npc, SpEnd);
                            }
                            else
                            {
                                npc.rotation = 0f;
                                EndGroup(npc);
                            }
                        }

                        break;
                    }
            }
        }

        /// <summary>Its arms click home on it: a great clank, its green over its skull for a moment.</summary>
        private void ClickFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 middle = Middle(npc);
            flash = 0.75f;
            flashGreen = true;
            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.35f, MaxInstances = 3 }, middle, 8);
            Sound(SoundID.Mech with { Volume = 1f, Pitch = -0.1f, MaxInstances = 3 }, middle, 8);
            Sound(SoundID.DD2_BallistaTowerShot with { Volume = 0.8f, Pitch = 0.2f, MaxInstances = 3 }, middle, 8);
            Shake(middle, 12f, 20);
        }

        /// <summary>While it turns: the rush of it, louder and higher the faster; its green thrown off its rim the way it turns.</summary>
        private void SpinTick(NPC npc, float share)
        {
            if (Main.dedServ || share <= 0.05f)
                return;

            Vector2 middle = Middle(npc);

            if (timer % 22 == 0)
                Sound(SoundID.DD2_BetsyWindAttack with { Volume = 0.45f * share, Pitch = -0.4f + 0.5f * share, MaxInstances = 3 }, middle, 4);

            if (!Seen(middle, 300f) || Main.rand.NextFloat() > 0.6f * share * Room)
                return;

            // Off its rim, along the way that part of it is going.
            Vector2 out_ = Main.rand.NextVector2Unit();
            Vector2 along = new Vector2(-out_.Y, out_.X) * side;
            Fleck(middle + out_ * Main.rand.NextFloat(60f, 84f) * npc.scale, along * Main.rand.NextFloat(3f, 7f) * share + npc.velocity, true, Main.rand.NextFloat(0.35f, 0.6f), Main.rand.Next(14, 24), 0.01f);
        }

        // ---- the clash -------------------------------------------------------------------------------------------------------

        /// <summary>Where an arm takes its place about a point for the clash: by its place among those that are up, spread over the sky side of it.</summary>
        internal static Vector2 ClashPost(Vector2 about, int rank, int up)
        {
            float f = up <= 1 ? 0f : rank / (float)(up - 1) * 2f - 1f;
            float spread = up == 2 ? ClSpread * 0.75f : ClSpread;
            float a = f * spread;
            return about + new Vector2(MathF.Sin(a), -MathF.Cos(a)) * ClFar;
        }

        private void RunClash(NPC npc, bool entered, Player p, int up)
        {
            int t = timer;
            Glide(npc, p.Center + new Vector2(0f, -380f), 3f, 0.05f);
            Settle(npc);

            switch (step)
            {
                case ClPost:
                    if (entered)
                        Whir(npc, 1f);

                    if (Authority && t >= ClPostTicks)
                    {
                        markA = p.Center;
                        Next(npc, ClLock);
                    }

                    break;

                case ClLock:
                    if (entered)
                        Sound(SoundID.Mech with { Volume = 0.9f, Pitch = 0.4f }, markA, 6);

                    if (Authority && t >= ClLockTicks)
                        Next(npc, ClRun);

                    break;

                case ClRun:
                    if (Authority && t >= ClRunTicks)
                    {
                        // Something to meet: the wave. One arm alone meets nothing.
                        count = up >= 2 ? 1 : 0;
                        Next(npc, ClHit);
                    }

                    break;

                case ClHit:
                    if (entered && count > 0)
                    {
                        PrimeShots.Shockwave(npc, markA);
                        ClashFx(markA);
                    }

                    if (Authority && t >= ClHitTicks)
                        Next(npc, ClEnd);

                    break;

                default:
                    if (Authority && t >= ClEndTicks)
                        EndGroup(npc);

                    break;
            }
        }

        /// <summary>Its arms meet: the crack of it, a white light and its green behind that, the screen thrown.</summary>
        private static void ClashFx(Vector2 at)
        {
            if (Main.dedServ)
                return;

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White * 0.95f, 2.4f, 9));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearHot * 0.9f, 4.4f, 16));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.7f, 7f, 30));

            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.8f, MaxInstances = 3 }, at, 10);
            Sound(SoundID.Item62 with { Volume = 1f, Pitch = -0.7f, MaxInstances = 3 }, at, 10);
            Sound(SoundID.DD2_OgreGroundPound with { Volume = 1f, Pitch = 0.3f, MaxInstances = 3 }, at, 10);
            Sound(SoundID.Thunder with { Volume = 0.9f, Pitch = 0.4f, MaxInstances = 2 }, at, 10);
            Shake(at, 28f, 36);

            if (Watching(at))
                Cinematic.Flash(new Color(238, 255, 210), 0.4f);
        }

        // ---- drawn: what they warn with ---------------------------------------------------------------------------------------

        private void DrawGroupMarks(NPC npc, SpriteBatch batch)
        {
            switch (act)
            {
                case Act.ViceHold: DrawViceMarks(npc, batch); break;
                case Act.SawTrain: DrawTrainMarks(npc, batch); break;
                case Act.Clash: DrawClashMarks(npc, batch); break;
            }
        }

        /// <summary>The vice's: a line from it to where the player was fifteen ticks ago, and on a little past; solid as it goes, and fading behind it.</summary>
        private void DrawViceMarks(NPC npc, SpriteBatch batch)
        {
            if (!ArmOf(npc, NPCID.PrimeVice, out NPC vice))
                return;

            if (step == VhWind || step == VhAim)
            {
                Player p = Target(npc);

                if (p == null)
                    return;

                Vector2 from = PrimeArm.Tip(vice);
                Vector2 at = Lagged(p, VhLag);
                Vector2 way = at - from;
                float alpha = step == VhWind ? Smooth(timer / 14f) : count > 0 ? Smooth(timer / 8f) : 1f;
                float ready = step == VhWind ? 0.25f * timer / VhWindTicks : 0.25f + 0.75f * timer / VhAimTicks;
                DrawWarn(batch, from, way.ToRotation(), way.Length() + VhOver, ready, false, 34f, alpha);
                DestroyerArt.DrawGlow(batch, at, 54f, PrimeArt.Lit(PrimeArt.Nuclear * (0.3f * alpha * (0.4f + 0.6f * ready))));
            }
            else if (step == VhGo)
            {
                Vector2 way = markA - markB;
                float alpha = 1f - Smooth(timer / (float)DestroyerArt.WarningFade);
                DrawWarn(batch, markB, way.ToRotation(), way.Length() + VhOver, 1f, true, 34f, alpha);
            }
        }

        /// <summary>The saw's: down from it to the ground under it, and along the ground at the player and on past; what it has run over, gone.</summary>
        private void DrawTrainMarks(NPC npc, SpriteBatch batch)
        {
            if (!ArmOf(npc, NPCID.PrimeSaw, out NPC saw))
                return;

            const float high = 50f;

            if (step == StRev)
            {
                Player p = Target(npc);

                if (p == null)
                    return;

                Vector2 from = PrimeArm.Tip(saw);

                if (!GroundUnder(from.X, from.Y, 100, out float top))
                    return;

                float alpha = Smooth(timer / 14f), ready = timer / (float)StRevTicks;
                float dx = p.Center.X - from.X;
                int way = Math.Abs(dx) > 40f ? Math.Sign(dx) : side;
                DrawWarn(batch, from, MathHelper.PiOver2, Math.Max(8f, top - from.Y) / 0.75f, ready, false, 0f, alpha);
                DrawGroundMark(batch, new Vector2(from.X, top), way, MathHelper.Clamp(Math.Abs(dx) + StPast, StLeast, StMost), ready, false, high, alpha);
            }
            else if (step == StDrop || step == StRun)
            {
                float gone = step == StRun ? StSpeed * timer + 30f : 0f;

                if (step == StDrop)
                    DrawWarn(batch, PrimeArm.Tip(saw), MathHelper.PiOver2, Math.Max(8f, markA.Y - PrimeArm.Tip(saw).Y) / 0.75f, 1f, true);

                DrawGroundMark(batch, markA, side, span, 1f, true, high, 1f, gone);
            }
        }

        /// <summary>The clash's: a line from each arm to the player; stopped on where they were as it locks; fading once they have met.</summary>
        private void DrawClashMarks(NPC npc, SpriteBatch batch)
        {
            if (step > ClHit)
                return;

            Player p = Target(npc);
            Vector2 at = step == ClPost ? (p?.Center ?? markA) : markA;
            float alpha = step == ClPost ? Smooth(timer / 16f) : step == ClHit ? 1f - Smooth(timer / (float)DestroyerArt.WarningFade) : 1f;
            float ready = step == ClPost ? timer / (float)ClPostTicks : 1f;
            bool locked = step != ClPost;
            int n = FindArms(npc);

            for (int k = 0; k < n; k++)
            {
                NPC limb = Main.npc[armList[k]];

                if (!limb.TryGetGlobalNPC(out PrimeArm arm) || !arm.Up)
                    continue;

                Vector2 from = PrimeArm.Tip(limb);
                Vector2 way = at - from;

                if (way.LengthSquared() > 16f * 16f)
                    DrawWarn(batch, from, way.ToRotation(), way.Length() + 70f, ready, locked, 0f, alpha);
            }

            if (locked && alpha > 0.01f)
                DestroyerArt.DrawGlow(batch, at, 70f, PrimeArt.Lit(PrimeArt.Nuclear * (0.34f * alpha)));
        }
    }
}
