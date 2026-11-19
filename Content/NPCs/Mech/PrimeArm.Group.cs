using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- an arm's part in its head's attacks of all its arms (Legends) ---------------------------------------------------
    //
    // Its head keeps the clock and the marks (Prime.Group); each arm that is up plays its part from those on every
    // machine, as in its head's coming and its end. The arm an attack is led by does the thing; the others hold its
    // base (the laser's column, the cannon's nukes), or stop where they are (the vice's dashes, the saw's train) till
    // they are wanted, or all do their own (the spin, the clash). What is fired is the server's; what is caught, too.

    public partial class PrimeArm
    {
        /// <summary>How far out from its place on its head it waits before it clicks home (a share of that place's distance).</summary>
        private const float SpStandoff = 1.16f;

        /// <summary>How much harder its saw's touch is as it runs along the ground.</summary>
        private const float TrainHurt = 1.5f;

        private void FollowGroup(NPC npc, NPC head, Prime prime)
        {
            Prime.Act what = prime.GroupAct;
            int step = prime.Step, t = clock;
            bool lead = npc.type == Prime.LeadType(what);

            switch (what)
            {
                case Prime.Act.LaserColumn: GroupColumn(npc, head, prime, step, t, lead); break;
                case Prime.Act.NukeDrop: GroupNukes(npc, head, prime, step, t, lead); break;
                case Prime.Act.ViceHold: GroupHold(npc, head, prime, step, t, lead); break;
                case Prime.Act.SawTrain: GroupTrain(npc, head, prime, step, t, lead); break;
                case Prime.Act.Spin: GroupSpin(npc, head, prime, step, t); break;
                case Prime.Act.Clash: GroupClash(npc, head, prime, step, t); break;
                default: Cease(npc); break;
            }
        }

        /// <summary>Stopped where it is (by request: "every other arms cease in place"): its lamp low.</summary>
        private void Cease(NPC npc)
        {
            npc.velocity *= 0.86f;
            lampTo = 0.4f;
        }

        /// <summary>Into a brace from where it was, over a step's first ticks; then held in it.</summary>
        private void IntoBrace(NPC npc, Vector2 pivot, float turn, int t, int ticks, bool lead)
        {
            float u = Prime.Smooth((t + 1f) / (ticks - 14f));
            MovePivot(npc, Vector2.Lerp(stepFrom, pivot, u));
            npc.rotation = stepTurn.AngleLerp(turn, u);

            if (At(ticks - 14))
                GripFx(npc, lead);
        }

        /// <summary>Let go of a brace: drifting off its head.</summary>
        private void OutOfBrace(NPC npc, NPC head)
        {
            Vector2 away = (Pivot(npc) - Prime.Middle(head)).SafeNormalize(-Vector2.UnitY);
            npc.velocity = npc.velocity * 0.9f + away * 0.3f;
        }

        // ---- the laser's column -------------------------------------------------------------------------------------------

        private void GroupColumn(NPC npc, NPC head, Prime prime, int step, int t, bool lead)
        {
            float way = prime.ColumnWay(step, t);
            Prime.ArmsUp(head, npc, NPCID.PrimeLaser, out int rank);

            // Pressed back into its skull as it fires, shuddering.
            float recoil = step == Prime.LcFire ? 6f + 2.5f * MathF.Sin(t * 1.9f) : 0f;
            Prime.BracePose(head, npc, lead, rank, way, recoil, out Vector2 pivot, out float turn);

            if (step == Prime.LcGather)
            {
                IntoBrace(npc, pivot, turn, t, Prime.LcGatherTicks, lead);
                return;
            }

            if (step == Prime.LcEnd)
            {
                OutOfBrace(npc, head);
                lampGreen = lead && t < 24;

                if (lead && At(0))
                    LensSmokeFx(npc);

                return;
            }

            MovePivot(npc, pivot);
            npc.rotation = turn;

            if (lead)
            {
                lampGreen = true;

                if (step == Prime.LcAim)
                {
                    // By request: "this one has a spark of green".
                    charge = MathHelper.Clamp(t / (float)Prime.LcAimTicks, 0f, 1f);
                    shake = 2f * charge;

                    if (At(0))
                        WakeFx(npc);

                    if (t % 9 == 3)
                        LensSparkFx(npc, charge);
                }
                else
                {
                    beam = true;
                    charge = 1f;
                    shake = 3f;

                    if (t % 5 == 0)
                        LensSparkFx(npc, 1f);
                }
            }
            else if (step == Prime.LcFire)
            {
                // Braced against it: its jet hard on, shuddering.
                shake = 1.6f;
                BraceJetFx(npc);
            }
        }

        // ---- the cannon's nukes -------------------------------------------------------------------------------------------

        private void GroupNukes(NPC npc, NPC head, Prime prime, int step, int t, bool lead)
        {
            Prime.ArmsUp(head, npc, NPCID.PrimeCannon, out int rank);

            // Kicked down into its skull by each shell.
            float recoil = 0f;

            if (step == Prime.NdFire)
            {
                foreach (int shot in Prime.NdShots)
                {
                    if (t >= shot)
                        recoil = Math.Max(recoil, 22f * MathF.Exp(-(t - shot) / 5f));
                }
            }

            Prime.BracePose(head, npc, lead, rank, -MathHelper.PiOver2, recoil, out Vector2 pivot, out float turn);

            if (step == Prime.NdGather)
            {
                IntoBrace(npc, pivot, turn, t, Prime.NdGatherTicks, lead);
                return;
            }

            if (step == Prime.NdEnd)
            {
                OutOfBrace(npc, head);
                lampGreen = lead && t < 30;
                return;
            }

            MovePivot(npc, pivot);
            npc.rotation = turn;

            if (!lead)
            {
                if (recoil > 4f)
                    BraceJetFx(npc);

                return;
            }

            if (step == Prime.NdFire)
            {
                for (int k = 0; k < Prime.NdShots.Length; k++)
                {
                    if (!At(Prime.NdShots[k]))
                        continue;

                    Vector2 tip = pivot + new Vector2(0f, -HandHigh(npc.type) * 0.5f * npc.scale);

                    if (Authority)
                    {
                        prime.NukePlan(head, k, t, out Vector2 dropAt, out int fuse);
                        PrimeShots.Nuke(npc, tip, dropAt, fuse);
                    }

                    ShellFx(tip);
                }

                return;
            }

            // They are coming down: its lamp blinking, red; then, a spark, and green and quick (by request: "the light
            // sparks on the hand and changes to green and all those nukes explodes").
            bool green = t >= Prime.NdGreenAt;
            lampGreen = green;
            lampTo = green ? (t / 3 % 2 == 0 ? 1f : 0.2f) : (t / 15 % 2 == 0 ? 1f : 0.25f);

            if (At(Prime.NdGreenAt))
            {
                WakeFx(npc);
                LensSparkFx(npc, 1f);
            }
        }

        // ---- the vice's hold ----------------------------------------------------------------------------------------------

        private void GroupHold(NPC npc, NPC head, Prime prime, int step, int t, bool lead)
        {
            Vector2 pivot = Pivot(npc);
            float s = npc.scale;
            Player p = Prime.Target(head);

            if (!lead)
            {
                HoldOther(npc, prime, step, t);
                return;
            }

            switch (step)
            {
                case Prime.VhWind:
                case Prime.VhAim:
                    {
                        // Open, drawn back, its jaws at where they were.
                        Vector2 at = p == null ? pivot + ToolWay(npc.rotation) : prime.Lagged(p, Prime.VhLag);
                        Vector2 way = (at - pivot).SafeNormalize(Vector2.UnitY);
                        npc.velocity = npc.velocity * 0.86f - way * (step == Prime.VhWind ? 0.3f : 0.12f);
                        TurnTo(npc, way, 0.22f);
                        jaw = 1;
                        shake = step == Prime.VhAim ? 1.4f * t / Prime.VhAimTicks : 0f;

                        if (step == Prime.VhWind && At(0))
                            Prime.Sound(SoundID.Mech with { Volume = 0.9f, Pitch = 0.1f }, pivot, 6);

                        break;
                    }

                case Prime.VhGo:
                    {
                        // Its jaws through where they were, and on a little past: what they pass over, it has.
                        Vector2 from = prime.MarkB, to = prime.MarkA;
                        Vector2 dir = (to - from).SafeNormalize(Vector2.UnitY);
                        Vector2 end = to + dir * (Prime.VhOver - HandHigh(npc.type) * 0.5f * s);
                        float u = MathHelper.Clamp((t + 1f) / Prime.VhGoTicks, 0f, 1f);
                        MovePivot(npc, Vector2.Lerp(from, end, 1f - MathF.Pow(1f - u, 2.2f)));
                        npc.rotation = TurnOf(dir);
                        jaw = 1;

                        if (At(0))
                        {
                            lastTip = Tip(npc);
                            Prime.Sound(SoundID.DD2_BallistaTowerShot with { Volume = 0.9f, Pitch = 0.2f, MaxInstances = 3 }, pivot, 6);
                            Prime.Sound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 3 }, pivot, 6);
                        }

                        if (Authority)
                        {
                            Vector2 tip = Tip(npc);
                            int who = Catch(lastTip, tip, GpGrip);
                            lastTip = tip;

                            if (who >= 0)
                                prime.ViceCaught(head, who, tip);
                        }

                        break;
                    }

                case Prime.VhRest:
                    npc.velocity *= 0.8f;
                    jaw = t / 4 % 2;
                    break;

                case Prime.VhHold:
                    {
                        // Held tight where it closed on them, lifted a little.
                        Vector2 at = stepFrom + new Vector2(0f, -36f * Prime.Smooth(t / 20f));
                        MovePivot(npc, at);
                        npc.rotation = stepTurn;
                        jaw = 0;
                        shake = 1.5f;

                        if (At(0))
                            ClenchFx(npc, false);

                        Vector2 way = ToolWay(stepTurn);
                        Vector2 grip = at + way * (HandHigh(npc.type) * 0.5f * s + 6f);
                        Grip(npc, prime.Grabbed, grip, 0.06f * MathF.Sin(t * 0.7f), new Vector2(0f, -3f), false);
                        break;
                    }

                default:
                    npc.velocity *= 0.9f;
                    jaw = t < 12 ? 1 : -1;
                    break;
            }
        }

        /// <summary>
        /// One of the others, in its vice's hold: stopped where it is till the vice has someone; then over them,
        /// pointing down at them, its laser and its cannon firing (by request: "shoot barrages versions of vanilla"),
        /// its saw swinging through them ("swing saw crazily").
        /// </summary>
        private void HoldOther(NPC npc, Prime prime, int step, int t)
        {
            int who = prime.Grabbed;

            if (step != Prime.VhHold || who < 0 || who >= Main.maxPlayers || !Main.player[who].active || Main.player[who].dead)
            {
                Cease(npc);
                return;
            }

            Vector2 on = Main.player[who].Center;
            Vector2 pivot = Pivot(npc);
            float s = npc.scale;

            if (npc.type == NPCID.PrimeSaw)
            {
                // Its far end over them, the whole of it swinging through them.
                float into = Prime.Smooth(t / 24f);
                float swing = 0.95f * MathF.Sin(t * 0.27f) * into;
                Vector2 way = new(MathF.Sin(swing), MathF.Cos(swing));
                Vector2 hang = on + new Vector2(0f, -(TailBack(npc.type) + 6f) * s);
                MovePivot(npc, Vector2.Lerp(stepFrom, hang + way * TailBack(npc.type) * s, into));
                npc.rotation = stepTurn.AngleLerp(TurnOf(way), into);
                rev = 0.8f * into;
                hurts = t > 24;
                RevFx(npc, rev, false);
                return;
            }

            Vector2 spot = on + new Vector2(Prime.SideOf(npc.type) * 200f, -250f);
            GlidePivot(npc, spot, 19f, 0.14f);
            Vector2 aimAt = (on - pivot).SafeNormalize(Vector2.UnitY);
            TurnTo(npc, aimAt, 0.2f);
            Vector2 tip = Tip(npc), toolWay = ToolWay(npc.rotation);

            if (npc.type == NPCID.PrimeLaser)
            {
                charge = Prime.Smooth(t / 24f);

                if (t < Prime.VhHoldTicks - 8 && Beat(30, 10))
                {
                    PrimeShots.Laser(npc, tip + toolWay * 6f, toolWay * 12f);
                    ShotFx(tip, toolWay, 1f);
                }
            }
            else if (npc.type == NPCID.PrimeCannon && t < Prime.VhHoldTicks - 8 && Beat(34, 16))
            {
                PrimeShots.Missile(npc, tip + toolWay * 4f, toolWay * 7f);
            }
        }

        // ---- the saw's train ----------------------------------------------------------------------------------------------

        private void GroupTrain(NPC npc, NPC head, Prime prime, int step, int t, bool lead)
        {
            if (!lead)
            {
                Cease(npc);
                return;
            }

            int way = prime.Side;
            float s = npc.scale;
            Vector2 pivot = Pivot(npc);
            Vector2 bite = new Vector2(way * 0.35f, 1f).SafeNormalize(Vector2.UnitY);
            Vector2 run = new Vector2(way, 0.22f).SafeNormalize(Vector2.UnitX);

            switch (step)
            {
                case Prime.StRev:
                    {
                        // To one side of the player, over the ground, as quick as it turns (by request: "Spins ultrafast").
                        Player p = Prime.Target(head);

                        if (p == null)
                        {
                            Cease(npc);
                            return;
                        }

                        float x = p.Center.X - way * Prime.StAside;
                        float ground = Prime.GroundUnder(x, p.Center.Y - 320f, 70, out float top) ? top : p.Bottom.Y;
                        GlidePivot(npc, new Vector2(x, ground - Prime.StHigh), 16f, 0.1f);
                        TurnTo(npc, bite, 0.12f);
                        rev = Prime.Smooth(t / 40f);
                        shake = 1f + 3f * rev;
                        lampGreen = t >= 30;
                        RevFx(npc, rev, true);

                        if (At(30))
                            WakeFx(npc);

                        break;
                    }

                case Prime.StDrop:
                    {
                        // Down onto the ground under it, its blade into it.
                        Vector2 land = prime.MarkA + new Vector2(0f, 8f) - bite * HandHigh(npc.type) * 0.5f * s;
                        float u = MathHelper.Clamp((t + 1f) / Math.Max(1, prime.Count), 0f, 1f);
                        MovePivot(npc, Vector2.Lerp(prime.MarkB, land, u * u));
                        TurnTo(npc, bite, 0.3f);
                        rev = 1f;
                        hurts = true;
                        hurtScale = TrainHurt;
                        lampGreen = true;
                        RevFx(npc, rev, true);
                        break;
                    }

                case Prime.StRun:
                    {
                        if (At(0))
                        {
                            runY = prime.MarkA.Y;
                            BiteFx(prime.MarkA, way, 1f);
                            KickFx(prime.MarkA, way);
                        }

                        // Kicked along the ground, over its rises and dips; a wall ends it.
                        float x = prime.MarkA.X + way * Prime.StSpeed * t;
                        bool open = Prime.SurfaceNear(x + way * 26f, runY + 2f, 3, 8, out float top);

                        if (open)
                            runY += MathHelper.Clamp(top - runY, -26f, 26f);
                        else
                            prime.SawBlocked(head);

                        MovePivot(npc, new Vector2(x, runY - 8f));
                        npc.rotation = npc.rotation.AngleLerp(TurnOf(run), 0.5f);
                        rev = 1f;
                        hurts = true;
                        hurtScale = TrainHurt;
                        shake = 2f;
                        lampGreen = true;

                        // Whoever is near is carried toward it (by request: "pulling player onto the saw if they near, just like a high speed train").
                        Prime.Drag(pivot + new Vector2(way * 40f, 0f), Prime.StPull, Prime.StPullReach);
                        RevFx(npc, rev, true);
                        TrainFx(npc, new Vector2(x, runY), way);
                        break;
                    }

                default:
                    {
                        // Up off the ground, slowing, turning its blade up.
                        if (At(0))
                        {
                            npc.velocity = new Vector2(way * 15f, -8f);
                            ScoopFx(pivot + new Vector2(0f, 8f), way);
                        }

                        npc.velocity *= 0.93f;
                        TurnTo(npc, new Vector2(way * 0.5f, -1f).SafeNormalize(-Vector2.UnitY), 0.06f);
                        rev = Math.Max(0f, 1f - t / (float)Prime.StEndTicks);
                        hurts = t < 10;
                        lampGreen = t < 20;
                        RevFx(npc, rev, true);
                        break;
                    }
            }
        }

        // ---- the spin -----------------------------------------------------------------------------------------------------

        private void GroupSpin(NPC npc, NPC head, Prime prime, int step, int t)
        {
            float s = npc.scale;
            Vector2 middle = Prime.Middle(head);

            // Its place on its head as at its coming, from the middle of its skull (what its head turns about).
            Vector2 socket = Prime.OutSpot(npc.type) * head.scale + (head.Center - middle);

            if (step == Prime.SpGather)
            {
                // To just off its place, pointing out.
                float u = Prime.Smooth((t + 1f) / (Prime.SpGatherTicks - 6f));
                MovePivot(npc, Vector2.Lerp(stepFrom, middle + socket * SpStandoff, u));
                npc.rotation = stepTurn.AngleLerp(TurnOf(socket), u);
                return;
            }

            if (step == Prime.SpLock)
            {
                // Home onto it (by request: "clicks onto it with nuclear sparks and steam gas").
                float u = MathHelper.Clamp((t + 1f) / Prime.SpClickAt, 0f, 1f);
                MovePivot(npc, middle + socket * MathHelper.Lerp(SpStandoff, 1f, u * u));
                npc.rotation = TurnOf(socket);
                lampGreen = t >= Prime.SpClickAt;

                if (At(Prime.SpClickAt))
                    SocketFx(npc, middle, socket);

                return;
            }

            // Turning with its head; each does its own as it goes.
            bool turning = step == Prime.SpTurn;
            float share = turning ? MathHelper.Clamp(t / 50f, 0f, 1f) : 1f - MathHelper.Clamp(t / (float)Prime.SpEndTicks, 0f, 1f);
            Vector2 dir = socket.RotatedBy(prime.SpinTurn(step, t));
            Vector2 way = dir.SafeNormalize(Vector2.UnitY);
            Vector2 pivot = middle + dir;
            lampGreen = true;
            hurts = share > 0.3f;

            switch (npc.type)
            {
                case NPCID.PrimeVice:
                    {
                        // Out and in along its own line (by request: "a path perpendicular to the tangent line at attached point").
                        float pump = 0.5f - 0.5f * MathF.Cos(t * MathHelper.TwoPi / 44f);
                        pivot += way * 64f * s * pump * share;
                        jaw = pump > 0.5f ? 1 : 0;
                        break;
                    }

                case NPCID.PrimeSaw:
                    {
                        // Swaying about its far end, forty-five degrees to either side (by request: "total angle path is 90 degrees").
                        Vector2 tail = pivot - way * TailBack(npc.type) * s;
                        way = way.RotatedBy(MathHelper.PiOver4 * MathF.Sin(t * MathHelper.TwoPi / 40f) * share);
                        pivot = tail + way * TailBack(npc.type) * s;
                        rev = 0.7f * share;
                        RevFx(npc, rev, false);
                        break;
                    }

                case NPCID.PrimeLaser:
                    charge = 0.6f * share;

                    // By request: "lasers shoots laser at 17 ticks".
                    if (turning && Beat(17, 17))
                    {
                        Vector2 tip = pivot + way * HandHigh(npc.type) * 0.5f * s;
                        PrimeShots.Laser(npc, tip + way * 6f, way * 9f + head.velocity);
                        ShotFx(tip, way, 1f);
                    }

                    break;

                default:
                    // By request: "cannon shoots bomb at 30 ticks".
                    if (turning && Beat(30, 30))
                    {
                        Vector2 tip = pivot + way * HandHigh(npc.type) * 0.5f * s;
                        PrimeShots.Bomb(npc, tip, way * 8.5f + head.velocity + new Vector2(0f, -2f));
                        ShellFx(tip);
                    }

                    break;
            }

            MovePivot(npc, pivot);
            npc.rotation = TurnOf(way);

            // Its socket sparking as it turns; let go, with a breath of steam, as it stops.
            if (Main.rand.NextBool(40))
                SocketSparkFx(middle, socket.RotatedBy(prime.SpinTurn(step, t)));

            if (!turning && At(Prime.SpEndTicks - 10))
                SteamFx(middle + dir.SafeNormalize(Vector2.UnitY) * 70f * head.scale, way, 5);
        }

        // ---- the clash ----------------------------------------------------------------------------------------------------

        private void GroupClash(NPC npc, NPC head, Prime prime, int step, int t)
        {
            float s = npc.scale;
            Vector2 pivot = Pivot(npc);
            Player p = Prime.Target(head);

            if (npc.type == NPCID.PrimeVice)
                jaw = 0;

            switch (step)
            {
                case Prime.ClPost:
                    {
                        if (p == null)
                        {
                            Cease(npc);
                            return;
                        }

                        // To its place about them, pointing at them.
                        int up = Prime.ArmsUp(head, npc, -1, out int rank);
                        GlidePivot(npc, Prime.ClashPost(p.Center, rank, up), 18f, 0.11f);
                        TurnTo(npc, (p.Center - pivot).SafeNormalize(Vector2.UnitY), 0.16f);
                        rev = 0.4f;
                        break;
                    }

                case Prime.ClLock:
                    {
                        // Stopped, drawn back a little along its line.
                        Vector2 way = (prime.MarkA - stepFrom).SafeNormalize(Vector2.UnitY);
                        MovePivot(npc, stepFrom - way * 28f * Prime.Smooth(t / (float)Prime.ClLockTicks));
                        TurnTo(npc, way, 0.3f);
                        shake = 1.6f * t / Prime.ClLockTicks;
                        lampGreen = true;
                        rev = 0.7f;

                        if (At(0))
                            WakeFx(npc);

                        break;
                    }

                case Prime.ClRun:
                    {
                        // Down its line, quicker and quicker: its tool's end there at the last tick, with the others'.
                        Vector2 way = (prime.MarkA - stepFrom).SafeNormalize(Vector2.UnitY);
                        Vector2 end = prime.MarkA - way * (HandHigh(npc.type) * 0.5f * s + 8f);
                        float u = MathHelper.Clamp((t + 1f) / Prime.ClRunTicks, 0f, 1f);
                        MovePivot(npc, Vector2.Lerp(stepFrom, end, u * u * u));
                        npc.rotation = TurnOf(way);
                        hurts = true;
                        lampGreen = true;
                        rev = 1f;

                        if (At(0))
                            Prime.Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = 0.2f, MaxInstances = 4 }, pivot, 6);

                        break;
                    }

                case Prime.ClHit:
                    {
                        // Thrown back the way it came, turning over, slowing.
                        if (At(0))
                        {
                            Vector2 back = (stepFrom - prime.MarkA).SafeNormalize(-Vector2.UnitY);
                            npc.velocity = back * 17f;
                            tumble = (npc.whoAmI % 2 == 0 ? 1f : -1f) * 0.3f;

                            if (prime.Count > 0)
                                ClashSparks(prime.MarkA, back);
                        }

                        npc.velocity *= 0.93f;
                        npc.rotation += tumble;
                        tumble *= 0.95f;
                        lampTo = Prime.Chance(npc.whoAmI * 31 + t / 3) > 0.4f ? 1f : 0.2f;
                        break;
                    }

                default:
                    Cease(npc);
                    lampTo = 1f;
                    break;
            }
        }

        // ---- what is seen and heard ---------------------------------------------------------------------------------------

        /// <summary>It takes hold (of the leading arm's base, or of its head): a clank.</summary>
        private static void GripFx(NPC npc, bool lead)
        {
            if (Main.dedServ)
                return;

            Vector2 at = lead ? Tail(npc) : Tip(npc);
            Prime.Sound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = lead ? -0.4f : 0f, PitchVariance = 0.2f, MaxInstances = 4 }, at, 6);
            Prime.Sound(SoundID.Mech with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 4 }, at, 6);
            Prime.Shake(at, 4f, 8);

            if (Prime.Seen(at, 200f))
                Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.RedHot * 0.6f, 0.3f, 8));
        }

        /// <summary>A crackle of its head's green at its lens (or its lamp).</summary>
        private static void LensSparkFx(NPC npc, float power)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 300f))
                return;

            Vector2 tip = Tip(npc);
            Prime.Arc(tip, tip + Main.rand.NextVector2Circular(30f, 30f) * (0.5f + power), PrimeArt.Nuclear, 7f + 5f * power, Main.rand.Next(10, 16), Main.rand.Next(2));

            if (Main.rand.NextBool(2))
                Prime.Sound(SoundID.Item93 with { Volume = 0.25f + 0.25f * power, Pitch = Main.rand.NextFloat(0.1f, 0.6f) }, tip, 4);
        }

        /// <summary>Braced against what it holds: its jet hard on, its head's green in it.</summary>
        private static void BraceJetFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 300f) || BehindFace(npc))
                return;

            Vector2 tail = Tail(npc), back = -ToolWay(npc.rotation);
            PADFire.Flame(tail, back * Main.rand.NextFloat(3f, 6f), Main.rand.NextFloat(12f, 20f), Main.rand.Next(7, 12), FirePalette.Nuclear, 0f, 0.6f);
            Lighting.AddLight(tail, 0.4f, 0.56f, 0.02f);
        }

        /// <summary>A shell leaves its cannon: its flash, smoke up after it, the thump of it.</summary>
        private static void ShellFx(Vector2 tip)
        {
            if (Main.dedServ)
                return;

            Prime.Sound(SoundID.Item61 with { Volume = 1f, Pitch = -0.4f, MaxInstances = 4 }, tip, 8);
            Prime.Sound(SoundID.Item62 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 4 }, tip, 8);
            Prime.Shake(tip, 8f, 14);

            if (!Prime.Seen(tip, 300f))
                return;

            Prime.Add(new BloomGeneric(tip, Vector2.Zero, new Color(255, 190, 110) * 0.9f, 0.9f, 9));
            Prime.Add(new BloomGeneric(tip, Vector2.Zero, Color.White * 0.7f, 0.4f, 6));

            for (int i = 0; i < 6; i++)
                Prime.Add(new MuzzleSmoke(tip + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f), new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.4f, 2.6f)), Main.rand.Next(30, 50),
                    Main.rand.NextFloat(0.32f, 0.54f)));

            for (int i = 0; i < 4; i++)
                PADParticleHandler.SpawnParticle(new SparkShard(tip, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -1f).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(5f, 12f), new Color(255, 200, 120), 12f, 3f, 40f,
                    Main.rand.Next(10, 16), falls: true));

            Lighting.AddLight(tip, 1.3f, 0.9f, 0.4f);
        }

        /// <summary>Steam let go from a place, a way.</summary>
        private static void SteamFx(Vector2 at, Vector2 way, int many)
        {
            if (Main.dedServ || !Prime.Seen(at, 200f))
                return;

            Vector2 across = new(-way.Y, way.X);

            for (int i = 0; i < many; i++)
            {
                float k = Main.rand.NextBool() ? 1f : -1f;
                Prime.Add(new SteamPuff(at + Main.rand.NextVector2Circular(6f, 6f), across * k * Main.rand.NextFloat(1.5f, 4.5f) + way * Main.rand.NextFloat(-0.5f, 1.5f), new Color(196, 204, 196),
                    Main.rand.Next(26, 44), Main.rand.NextFloat(7f, 11f), Main.rand.NextFloat(30f, 50f), Main.rand.NextFloat(0.4f, 0.6f)));
            }

            Prime.Sound(SoundID.Item34 with { Volume = 0.4f, Pitch = 0.6f, MaxInstances = 4 }, at, 6);
        }

        /// <summary>It clicks home on its head: its head's green arcing off the joint, steam out of it to either side, sparks along its skull's rim.</summary>
        private void SocketFx(NPC npc, Vector2 middle, Vector2 socket)
        {
            if (Main.dedServ)
                return;

            Vector2 way = socket.SafeNormalize(Vector2.UnitY);
            Vector2 joint = middle + way * 70f;
            Vector2 across = new(-way.Y, way.X);

            Prime.Sound(SoundID.Item93 with { Volume = 0.7f, Pitch = 0.1f, PitchVariance = 0.3f, MaxInstances = 4 }, joint, 8);
            WakeFx(npc);

            if (!Prime.Seen(joint, 300f))
                return;

            Prime.Add(new BloomGeneric(joint, Vector2.Zero, PrimeArt.Nuclear * 0.9f, 0.8f, 14));
            Prime.Add(new BloomGeneric(joint, Vector2.Zero, PrimeArt.NuclearHot * 0.8f, 0.3f, 8));

            for (int i = 0; i < 4; i++)
                Prime.Arc(joint, joint + (way * Main.rand.NextFloat(-10f, 40f) + across * Main.rand.NextFloat(-44f, 44f)), i % 2 == 0 ? PrimeArt.Nuclear : PrimeArt.NuclearHot, Main.rand.NextFloat(8f, 13f),
                    Main.rand.Next(12, 18), Main.rand.Next(2));

            for (int k = -1; k <= 1; k += 2)
            {
                for (int i = 0; i < 3; i++)
                    PADParticleHandler.SpawnParticle(new SparkShard(joint, (across * k + way * Main.rand.NextFloat(-0.3f, 0.5f)) * Main.rand.NextFloat(4f, 10f), Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear,
                        12f, 3f, 44f, Main.rand.Next(10, 18), falls: true));
            }

            SteamFx(joint, way, 6);
        }

        /// <summary>Its joint on its head, sparking as it turns.</summary>
        private static void SocketSparkFx(Vector2 middle, Vector2 socket)
        {
            if (Main.dedServ)
                return;

            Vector2 joint = middle + socket.SafeNormalize(Vector2.UnitY) * 70f;

            if (!Prime.Seen(joint, 200f))
                return;

            Prime.Arc(joint, joint + Main.rand.NextVector2Circular(30f, 30f), PrimeArt.Nuclear, 8f, 12, 1);
            Prime.Sound(SoundID.Item93 with { Volume = 0.3f, Pitch = Main.rand.NextFloat(0f, 0.5f) }, joint, 3);
        }

        /// <summary>Its saw bites and is kicked off along the ground: the shriek of it, a gout of sparks back off the place.</summary>
        private static void KickFx(Vector2 at, int way)
        {
            if (Main.dedServ)
                return;

            Prime.Sound(SoundID.Item22 with { Volume = 1f, Pitch = 0.9f, MaxInstances = 3 }, at, 8);
            Prime.Sound(SoundID.DD2_BetsyWindAttack with { Volume = 0.9f, Pitch = 0.1f, MaxInstances = 3 }, at, 8);
            Prime.Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = 0.4f, MaxInstances = 3 }, at, 8);

            if (!Prime.Seen(at, 400f))
                return;

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, new Color(255, 220, 150) * 0.9f, 1.4f, 9));

            for (int i = 0; i < (int)(22 * Prime.Room); i++)
                PADParticleHandler.SpawnParticle(new SparkShard(at, new Vector2(-way * Main.rand.NextFloat(4f, 22f), -Main.rand.NextFloat(2f, 16f)), HotSparks[Main.rand.Next(HotSparks.Length)], 14f, 3f, 60f,
                    Main.rand.Next(14, 26), falls: true));
        }

        /// <summary>
        /// As it runs (by request: "make sure to add effect of sparks from kickback"): a tail of sparks thrown up and
        /// back off its teeth, white-hot to its head's green; earth with them; its light along the ground; the rush of it.
        /// </summary>
        private void TrainFx(NPC npc, Vector2 at, int way)
        {
            if (Main.dedServ)
                return;

            if (clock % 5 == 0)
            {
                Prime.Sound(SoundID.Item22 with { Volume = 0.9f, Pitch = 1f, MaxInstances = 4 }, at, 6);
                Prime.Shake(at, 7f, 8);
            }

            Lighting.AddLight(at, 1.2f, 1.1f, 0.3f);

            if (!Prime.Seen(at, 300f))
                return;

            float room = Prime.Room;
            Color dust = new(118, 104, 92);

            // Laid along what it covered this tick, so its tail has no gaps in it.
            for (int i = 0; i < (int)(7 * room); i++)
            {
                Vector2 from = at - new Vector2(way * Main.rand.NextFloat(0f, Prime.StSpeed), 4f);
                Color c = Main.rand.NextBool(3) ? PrimeArt.Nuclear : HotSparks[Main.rand.Next(HotSparks.Length)];
                PADParticleHandler.SpawnParticle(new SparkShard(from, new Vector2(-way * Main.rand.NextFloat(3f, 20f), -Main.rand.NextFloat(3f, 17f)), c, 14f, 3f, 64f, Main.rand.Next(14, 28), falls: true));
            }

            for (int i = 0; i < 2; i++)
            {
                if (Main.rand.NextFloat() < room)
                    Prime.Add(new StoneChip(at - new Vector2(way * Main.rand.NextFloat(0f, Prime.StSpeed), 4f), new Vector2(-way * Main.rand.NextFloat(2f, 9f), -Main.rand.NextFloat(4f, 12f)),
                        Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0f, 0.4f)), Main.rand.Next(40, 70), Main.rand.NextFloat(0.7f, 1.1f)));
            }

            if (Main.rand.NextFloat() < room)
                Prime.Add(new DustFog(at - new Vector2(way * Main.rand.NextFloat(0f, Prime.StSpeed), 8f), new Vector2(-way * Main.rand.NextFloat(0.5f, 2.5f), -Main.rand.NextFloat(0.2f, 1f)), dust * 0.75f,
                    Main.rand.Next(40, 70), Main.rand.NextFloat(0.5f, 0.9f), 0.28f));

            // The furrow it leaves, glowing a moment.
            PADFire.Flame(at - new Vector2(way * Main.rand.NextFloat(0f, Prime.StSpeed), 2f), new Vector2(-way * Main.rand.NextFloat(0.5f, 2f), -0.4f), Main.rand.NextFloat(14f, 22f), Main.rand.Next(10, 18),
                FirePalette.Nuclear, 0.02f, 0.5f);

            TileDust(at, 2, 6f);
        }

        /// <summary>Its arms meet: sparks thrown back off the place the way it is thrown, and to either side of that.</summary>
        private static void ClashSparks(Vector2 at, Vector2 back)
        {
            if (Main.dedServ || !Prime.Seen(at, 400f))
                return;

            for (int i = 0; i < (int)(12 * Prime.Room); i++)
            {
                Color c = Main.rand.NextBool(3) ? Color.White : Main.rand.NextBool() ? PrimeArt.NuclearHot : PrimeArt.Nuclear;
                PADParticleHandler.SpawnParticle(new SparkShard(at, back.RotatedByRandom(0.9f) * Main.rand.NextFloat(6f, 24f), c, 16f, 3f, 90f, Main.rand.Next(14, 26)));
            }
        }
    }
}
