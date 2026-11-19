using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its own attacks, once no arm is left (Legends) ---------------------------------------------------------------------
    //
    // By request ("Head attacks: ..."). Changed, it can be hurt; its hover between these is vanilla's (its own spin never
    // comes), and each begins after a stretch of that. It can be struck through all of them.
    //
    //   DASH       "Dash while spinninng with spikes, like eye of cthulhu": three in a row (five under four tenths of
    //              its life), each aimed at where the player was ten ticks before it goes, spinning; unmarked (by
    //              request: "doing too much indicator").
    //                WIND 34   GO 22   BRAKE 14
    //
    //   DETONATE   "Moves near the player , anchor at some place, eyes go nuclear green and self detonate, causing
    //              nuclear explosion; before self detonate its sucks everything in for 3 secs"
    //                NEAR    to a place by the player (ninety ticks at the most), where it stops
    //                SUCK 180  its eyes blazing, its jaw open: whoever is within fifty-four tiles is carried toward it,
    //                          faster and faster (run from it: boots outrun it); how far it will reach is a light
    //                          about it that grows
    //                (it goes up: PrimeBurst, twenty-two tiles)
    //                DAZE 110  smoking, its eyes guttering, harmless: the time to strike it
    //
    //   BEAMS      "Slow down, roar eye goes green with radial slowly bigger and shoot out laser upwards, the each eyes
    //              spin to the other half of the circle until both pointing down. nuclear laser" (asked after: a safe
    //              gap under it).
    //                SLOW 30    over the player, where it stops
    //                CHARGE 80  a roar; a light at each eye, growing (the beams' ways are not marked: "doing too much
    //                           indicator")
    //                FIRE 240   a beam up from each eye for twenty ticks; then each round its own half of the circle
    //                           over two hundred, to twenty degrees short of straight down; held there twenty
    //                END 40
    //              Under it is safe: a wedge twenty degrees to either side.
    //
    //   LAUNCH     "Roar and launch head onto player causing a path of toxic radioactivity, slamming onto the ground
    //              also makes it loose 11% health so this happens very rarely": half the others' chance at the best,
    //              and never twice in a minute.
    //                ROAR 60    drawn back, its crown turned at the player; a line at them, stopped sixteen ticks
    //                           before it goes
    //                GO         thirty pixels a tick down its line; the ground it passes over radioactive for thirty
    //                           seconds (PrimeFallout), every hundred and ten pixels of it
    //                STUCK 100  (if it met the ground) eleven hundredths of its life gone; half buried, smoking: the
    //                           time to strike it
    //                RISE 40    up out of it, upright again

    public partial class Prime
    {
        /// <summary>Ticks of vanilla's hover between two of its own attacks.</summary>
        private const int MoveRestLeast = 150, MoveRestMost = 240;

        // ---- dash -------------------------------------------------------------------------------------------------------------

        internal const int DaWind = 0, DaGo = 1, DaBrake = 2;
        private const int DaWindTicks = 34, DaLockAt = 24, DaGoTicks = 22, DaBrakeTicks = 14;
        private const float DaSpeed = 21f;
        private const float DashHurt = 1.5f;

        /// <summary>How fast it turns, a tick (this machine's): only seen.</summary>
        private float whirl;

        // ---- detonate ---------------------------------------------------------------------------------------------------------

        internal const int DeNear = 0, DeSuck = 1, DeDaze = 2;
        private const int DeNearMost = 90, DeSuckTicks = 180, DeDazeTicks = 110;

        /// <summary>How far its going up reaches (twenty-two tiles); how far off it carries a player toward it, and how fast, from first to last.</summary>
        public const float DetonateReach = 22f * 16f;
        private const float DeDragReach = 54f * 16f, DeDragLeast = 0.8f, DeDragMost = 3.4f;

        // ---- beams ------------------------------------------------------------------------------------------------------------

        internal const int BeSlow = 0, BeCharge = 1, BeFire = 2, BeEnd = 3;
        private const int BeSlowTicks = 30, BeChargeTicks = 80, BeFireTicks = 240, BeEndTicks = 40;

        /// <summary>Ticks into FIRE its beams stay up before they turn, and ticks they take to come round.</summary>
        private const int BeUp = 20, BeSweep = 200;

        /// <summary>How far round from straight up each comes (asked after: "Safe gap under it"): twenty degrees short of straight down.</summary>
        private const float BeStop = MathHelper.Pi - 0.35f;
        private const float BeHigh = 340f;

        // ---- launch -----------------------------------------------------------------------------------------------------------

        internal const int LaRoar = 0, LaGo = 1, LaStuck = 2, LaRise = 3;
        private const int LaRoarTicks = 60, LaLockAt = 44, LaGoMost = 50, LaStuckTicks = 100, LaRiseTicks = 40;
        private const float LaSpeed = 30f;
        private const float LaunchHurt = 1.6f;

        /// <summary>How often along its way the ground is left radioactive, and how far about each place.</summary>
        private const float LaTrailEvery = 110f, LaTrailReach = 80f;

        /// <summary>What meeting the ground costs it (by request: "makes it loose 11% health"), and the least ticks between two of them.</summary>
        public const float LaunchCost = 0.11f;
        private const int LaunchEvery = 60 * 60;

        // ---- what comes next --------------------------------------------------------------------------------------------------

        /// <summary>Its next own attack (the server's): its dash most often (not three times running), not the one before, its launch very rarely.</summary>
        private void BeginMove(NPC npc)
        {
            Player p = Target(npc);

            if (p == null)
                return;

            float dash = lastMove == Act.Dash && dashRun >= 2 ? 0f : 5f;
            float detonate = lastMove == Act.Detonate ? 0f : 2f;
            float beams = lastMove == Act.Beams ? 0f : 2f;
            float launch = lastMove == Act.Launch || launchWait > 0 ? 0f : 0.5f;
            float roll = Main.rand.NextFloat(dash + detonate + beams + launch);

            act = roll < dash ? Act.Dash
                : roll < dash + detonate ? Act.Detonate
                : roll < dash + detonate + beams ? Act.Beams
                : Act.Launch;

            dashRun = act == Act.Dash ? dashRun + 1 : 0;

            if (act == Act.Launch)
                launchWait = LaunchEvery;

            lastMove = act;
            seed = Main.rand.Next(int.MaxValue);
            side = Main.rand.NextBool() ? 1 : -1;
            count = 0;
            angle = 0f;
            span = 0f;
            markA = markB = npc.Center;
            wasAtKept = 0;

            // To the side of the player it is already on, over their head.
            if (act == Act.Detonate)
            {
                if (Math.Abs(npc.Center.X - p.Center.X) > 60f)
                    side = Math.Sign(npc.Center.X - p.Center.X);

                markA = p.Center + new Vector2(side * 150f, -160f);
            }

            Go(npc, Scene.Move, 0);
        }

        /// <summary>An attack over: back to vanilla's hover, for a stretch.</summary>
        private void EndMove(NPC npc)
        {
            act = Act.None;
            restFor = Main.rand.Next(MoveRestLeast, MoveRestMost + 1);
            BackToHover(npc);
            Go(npc, Scene.None, 0);
        }

        private void RunMove(NPC npc, bool entered)
        {
            Player p = Target(npc);

            if (p == null || !PADWorld.Legends || Main.IsItDay())
            {
                npc.velocity *= 0.9f;

                if (Authority)
                    EndMove(npc);

                return;
            }

            TrackTarget(p);

            if (Authority && timer % 20 == 10)
                npc.netUpdate = true;

            eyeHeat = Math.Max(0f, eyeHeat - 0.05f);

            switch (act)
            {
                case Act.Dash: RunDash(npc, entered, p); break;
                case Act.Detonate: RunDetonate(npc, entered, p); break;
                case Act.Beams: RunBeams(npc, entered, p); break;
                case Act.Launch: RunLaunch(npc, entered, p); break;

                default:
                    if (Authority)
                        EndMove(npc);

                    break;
            }
        }

        // ---- dash -------------------------------------------------------------------------------------------------------------

        private void RunDash(NPC npc, bool entered, Player p)
        {
            int t = timer;
            int dashes = npc.life < npc.lifeMax * 0.4f ? 5 : 3;

            switch (step)
            {
                case DaWind:
                    {
                        // Slowing, drawn back from the way it will go, its spin coming up.
                        Vector2 at = t < DaLockAt ? p.Center : markA;
                        Vector2 way = (at - npc.Center).SafeNormalize(Vector2.UnitY);
                        npc.velocity = npc.velocity * 0.9f - way * 0.28f;
                        whirl = MathHelper.Lerp(whirl, 0.36f * side, 0.09f);
                        npc.rotation += whirl;
                        hurts = true;

                        if (entered)
                            Sound(SoundID.Item24 with { Volume = 0.7f, Pitch = -0.5f + 0.12f * count, MaxInstances = 3 }, npc.Center, 6);

                        if (Authority && At(DaLockAt))
                        {
                            markA = p.Center;
                            angle = (markA - npc.Center).ToRotation();
                            npc.netUpdate = true;
                        }

                        if (Authority && t >= DaWindTicks)
                        {
                            markB = npc.Center;
                            Next(npc, DaGo);
                        }

                        break;
                    }

                case DaGo:
                    {
                        Vector2 dir = angle.ToRotationVector2();
                        npc.velocity = dir * DaSpeed * Math.Min(1f, (t + 1) / 3f);
                        whirl = 0.46f * side;
                        npc.rotation += whirl;
                        hurts = true;
                        hurtScale = DashHurt;

                        if (entered)
                            DashFx(npc);

                        DashTrail(npc, dir);

                        if (Authority && t >= DaGoTicks)
                            Next(npc, DaBrake);

                        break;
                    }

                default:
                    {
                        bool last = count + 1 >= dashes;
                        npc.velocity *= 0.86f;
                        whirl *= 0.9f;
                        hurts = t < 6;
                        hurtScale = DashHurt;

                        // After its last, upright again as it stops.
                        if (last)
                            npc.rotation = MathHelper.WrapAngle(npc.rotation).AngleLerp(0f, 0.22f);
                        else
                            npc.rotation += whirl;

                        if (Authority && t >= DaBrakeTicks)
                        {
                            if (last)
                            {
                                EndMove(npc);
                            }
                            else
                            {
                                count++;
                                Next(npc, DaWind);
                            }
                        }

                        break;
                    }
            }
        }

        /// <summary>It goes: its roar, the rush of it.</summary>
        private static void DashFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Roar with { Volume = 0.8f, Pitch = 0.3f }, npc.Center, 6);
            Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 3 }, npc.Center, 6);
            Shake(npc.Center, 6f, 12);
        }

        /// <summary>Behind it as it goes: its green torn off its spikes, back along its way.</summary>
        private void DashTrail(NPC npc, Vector2 dir)
        {
            if (Main.dedServ || !Seen(npc.Center, 200f))
                return;

            Vector2 middle = Middle(npc);
            Vector2 across = new(-dir.Y, dir.X);

            for (int i = 0; i < 2; i++)
            {
                if (Main.rand.NextFloat() > Room)
                    continue;

                Vector2 at = middle + across * Main.rand.NextFloat(-60f, 60f) * npc.scale - dir * Main.rand.NextFloat(0f, 30f);
                Fleck(at, -dir * Main.rand.NextFloat(1f, 5f) + across * Main.rand.NextFloat(-1f, 1f), true, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(14, 26), 0.01f);
            }

            if (timer % 2 == 0)
                PADParticleHandler.SpawnParticle(new SparkShard(middle + across * Main.rand.NextFloat(-56f, 56f) * npc.scale, -dir * Main.rand.NextFloat(4f, 9f), PrimeArt.Nuclear, 16f, 3f, 64f,
                    Main.rand.Next(10, 16)));

            Lighting.AddLight(middle, 0.6f, 0.82f, 0.02f);
        }

        // ---- detonate ---------------------------------------------------------------------------------------------------------

        private void RunDetonate(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case DeNear:
                    Glide(npc, markA, 12f, 0.1f);
                    Settle(npc);
                    hurts = true;

                    if (Authority && (t >= DeNearMost || Vector2.DistanceSquared(npc.Center, markA) < 34f * 34f))
                    {
                        markA = npc.Center;
                        Next(npc, DeSuck);
                    }

                    break;

                case DeSuck:
                    {
                        float u = MathHelper.Clamp(t / (float)DeSuckTicks, 0f, 1f);
                        Move(npc, Vector2.Lerp(npc.Center, markA, 0.3f));
                        npc.rotation = npc.rotation.AngleLerp(0.07f * u * MathF.Sin(t * 1.4f), 0.5f);
                        shake = 0.5f + 6f * u * u;
                        eyeHeat = Smooth(t / 30f) * (1f + 2f * u);

                        if (entered && !Main.dedServ)
                        {
                            StopSounds();
                            chargeSlot = SoundEngine.PlaySound(ChargeSound with { Pitch = -0.2f }, npc.Center);
                            Sound(SoundID.DD2_DrakinBreathIn with { Volume = 1f, Pitch = -0.6f }, npc.Center, 6);
                            EyesOn(npc, green: true);
                        }

                        Drag(markA, MathHelper.Lerp(DeDragLeast, DeDragMost, u), DeDragReach);
                        SuckTick(npc, u);

                        if (Authority && t >= DeSuckTicks)
                        {
                            PrimeShots.Detonation(npc, npc.Center, DetonateReach);
                            Next(npc, DeDaze);
                        }

                        break;
                    }

                default:
                    {
                        float left = 1f - MathHelper.Clamp(t / (float)DeDazeTicks, 0f, 1f);

                        if (entered)
                        {
                            StopSounds();
                            flash = 1f;
                            flashGreen = true;
                        }

                        // Hanging where it went up, sinking a little, rocking.
                        npc.velocity = npc.velocity * 0.9f + new Vector2(0f, 0.05f);
                        npc.rotation = npc.rotation.AngleLerp(0.16f * left * MathF.Sin(t * 0.19f), 0.3f);
                        shake = 3f * left * left;
                        Smoke(npc, 0.3f + 1.5f * left);

                        if (t % 15 == 4 && t < DeDazeTicks - 20)
                            Spark(npc, seed + t * 31, 0.6f + 0.6f * left, green: true);

                        if (Authority && t >= DeDazeTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>While it draws everything in: its green, dust and smoke carried in at it from all about, quicker the nearer; a crackle off it now and then; its light; the screen kept shaking.</summary>
        private void SuckTick(NPC npc, float u)
        {
            if (Main.dedServ)
                return;

            Vector2 at = Middle(npc);
            float k = 0.6f + 2.2f * u;
            Lighting.AddLight(at, 0.73f * k, k, 0.02f * k);

            if (timer % 6 == 0)
                Shake(at, 1f + 5f * u, 8);

            if (!Seen(at, 700f))
                return;

            float room = Room;

            for (int i = 0; i < 3; i++)
            {
                if (Main.rand.NextFloat() > (0.4f + 0.6f * u) * room)
                    continue;

                Vector2 off = Main.rand.NextVector2Unit() * Main.rand.NextFloat(140f, 380f + 240f * u);
                Dust dust = Dust.NewDustPerfect(at + off, DustID.CursedTorch, -off * (0.035f + 0.03f * u), 0, default, Main.rand.NextFloat(1f, 1.8f));
                dust.noGravity = true;
            }

            if (Main.rand.NextFloat() < (0.25f + 0.5f * u) * room)
            {
                Vector2 off = Main.rand.NextVector2Unit() * Main.rand.NextFloat(200f, 520f);
                Add(new HoneyMote(at + off, -off * 0.03f, Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear, Main.rand.NextFloat(0.4f, 0.75f), Main.rand.Next(20, 30), 0f));
            }

            if (Main.rand.NextFloat() < 0.35f * u * room)
            {
                Vector2 off = Main.rand.NextVector2Unit() * Main.rand.NextFloat(180f, 460f);
                PADFire.Smoke(at + off, -off * 0.022f, Main.rand.NextFloat(26f, 44f), Main.rand.Next(26, 40), 0, 0.5f);
            }

            if (Main.rand.NextFloat() < 0.03f + 0.1f * u)
            {
                Vector2 on = OnFace(npc, Main.rand.NextFloat(-40f, 40f), Main.rand.NextFloat(-46f, 40f));
                Arc(on, on + Main.rand.NextVector2Circular(36f, 30f) * (0.7f + u), GreenSparks[Main.rand.Next(GreenSparks.Length)], 8f + 5f * u, Main.rand.Next(10, 16), Main.rand.Next(2));
                Sound(SoundID.Item93 with { Volume = 0.25f + 0.35f * u, Pitch = Main.rand.NextFloat(0f, 0.5f) }, on, 3);
            }
        }

        // ---- beams ------------------------------------------------------------------------------------------------------------

        /// <summary>How far round from straight up its eyes' beams are at a tick of FIRE.</summary>
        private static float BeamRound(int clock)
        {
            float u = MathHelper.Clamp((clock - BeUp) / (float)BeSweep, 0f, 1f);
            return BeStop * MathHelper.Lerp(u, Smooth(u), 0.4f);
        }

        /// <summary>The way a beam points that is this far round from straight up, on an eye's side.</summary>
        private static Vector2 BeamWay(int eye, float round) => new(eye * MathF.Sin(round), -MathF.Cos(round));

        /// <summary>One of its eyes' beams: whether it is firing, where from, and the way it points this tick.</summary>
        private bool EyeBeam(NPC npc, int eye, out Vector2 from, out Vector2 way)
        {
            from = Eye(npc, eye);
            way = -Vector2.UnitY;

            if (scene != Scene.Move || act != Act.Beams || step != BeFire)
                return false;

            way = BeamWay(eye, BeamRound(timer));
            from += way * 8f;
            return true;
        }

        private void RunBeams(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case BeSlow:
                    Glide(npc, p.Center + new Vector2(0f, -BeHigh), 9f, 0.1f);
                    Settle(npc);
                    hurts = true;

                    if (Authority && t >= BeSlowTicks)
                    {
                        markA = npc.Center + npc.velocity * 5f;
                        Next(npc, BeCharge);
                    }

                    break;

                case BeCharge:
                    {
                        float u = MathHelper.Clamp(t / (float)BeChargeTicks, 0f, 1f);
                        Move(npc, Vector2.Lerp(npc.Center, markA, 0.2f));
                        npc.rotation = npc.rotation.AngleLerp(0f, 0.2f);
                        shake = 0.5f + 2.2f * u;
                        eyeHeat = 0.3f + 2.7f * u;
                        hurts = true;

                        if (entered && !Main.dedServ)
                        {
                            Cinematic.Roar(npc, 70, 0.8f, ScreechSound with { Pitch = -0.3f }, rim: PrimeArt.Nuclear);
                            StopSounds();
                            chargeSlot = SoundEngine.PlaySound(ChargeSound with { Pitch = 0.3f, Volume = 0.7f }, npc.Center);
                        }

                        if (t % 9 == 4 && !Main.dedServ && Seen(npc.Center, 200f))
                        {
                            int eye = Main.rand.NextBool() ? 1 : -1;
                            Vector2 on = Eye(npc, eye);
                            Arc(on, on + new Vector2(eye * Main.rand.NextFloat(10f, 30f), -Main.rand.NextFloat(10f, 34f)) * (0.6f + u), PrimeArt.Nuclear, 8f + 4f * u, 12, 1);
                        }

                        if (Authority && t >= BeChargeTicks)
                            Next(npc, BeFire);

                        break;
                    }

                case BeFire:
                    Move(npc, Vector2.Lerp(npc.Center, markA, 0.2f));
                    npc.rotation = npc.rotation.AngleLerp(0f, 0.2f);
                    shake = 2.2f;
                    eyeHeat = 3f;
                    hurts = true;

                    if (entered)
                    {
                        StopSounds();
                        PrimeShots.Beam(npc, BeFireTicks, -1);
                        PrimeShots.Beam(npc, BeFireTicks, 1);
                        ColumnFx(Middle(npc));
                    }

                    if (t % 7 == 0)
                        Shake(npc.Center, 3f, 9);

                    if (Authority && t >= BeFireTicks)
                        Next(npc, BeEnd);

                    break;

                default:
                    npc.velocity *= 0.9f;
                    Settle(npc);
                    hurts = true;

                    if (entered)
                        EyesOut(npc);

                    if (Authority && t >= BeEndTicks)
                        EndMove(npc);

                    break;
            }
        }

        // ---- launch -----------------------------------------------------------------------------------------------------------

        private void RunLaunch(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case LaRoar:
                    {
                        // Drawn back from the way it will go, its crown turned that way.
                        float u = MathHelper.Clamp(t / (float)LaRoarTicks, 0f, 1f);
                        Vector2 at = t < LaLockAt ? p.Center : markA;
                        Vector2 way = (at - npc.Center).SafeNormalize(Vector2.UnitY);
                        npc.velocity = npc.velocity * 0.9f - way * 0.32f;
                        npc.rotation = npc.rotation.AngleLerp(way.ToRotation() + MathHelper.PiOver2, 0.12f);
                        shake = 1f + 3.5f * u;
                        eyeHeat = 0.5f + 2f * u;
                        hurts = true;

                        if (entered && !Main.dedServ)
                            Cinematic.Roar(npc, 60, 0.9f, ScreechSound with { Pitch = -0.12f }, rim: PrimeArt.Nuclear);

                        if (Authority && At(LaLockAt))
                        {
                            markA = p.Center;
                            angle = (markA - npc.Center).ToRotation();
                            npc.netUpdate = true;
                        }

                        if (Authority && t >= LaRoarTicks)
                        {
                            markB = npc.Center;
                            count = 0;
                            Next(npc, LaGo);
                        }

                        break;
                    }

                case LaGo:
                    {
                        Vector2 dir = angle.ToRotationVector2();
                        npc.velocity = dir * LaSpeed * Math.Min(1f, (t + 1) / 3f);
                        npc.rotation = angle + MathHelper.PiOver2;
                        eyeHeat = 2.5f;
                        hurts = true;
                        hurtScale = LaunchHurt;

                        if (entered)
                            LaunchFx(npc);

                        LaunchTrail(npc, dir);

                        if (!Authority)
                            break;

                        // The ground it passes over, left radioactive.
                        float gone = Vector2.Distance(npc.Center, markB);

                        while (gone >= (count + 1) * LaTrailEvery)
                        {
                            count++;
                            PrimeFallout.Add(markB + dir * (count * LaTrailEvery), LaTrailReach, PrimeFallout.ShortLasts, tell: count % 3 == 0);
                        }

                        if (gone > 90f && Collision.SolidCollision(npc.Center - new Vector2(22f), 44, 44))
                        {
                            // It has met the ground: what that costs it.
                            npc.life = Math.Max(1, npc.life - (int)(npc.lifeMax * LaunchCost));
                            npc.velocity = Vector2.Zero;
                            markB = npc.Center;
                            PrimeFallout.Add(npc.Center, LaTrailReach * 1.4f, PrimeFallout.ShortLasts);
                            PrimeShots.Slam(npc, npc.Center);
                            Next(npc, LaStuck);
                        }
                        else if (t >= LaGoMost)
                        {
                            // It met nothing.
                            PrimeFallout.Add(npc.Center, LaTrailReach, PrimeFallout.ShortLasts);
                            Next(npc, LaRise);
                        }

                        break;
                    }

                case LaStuck:
                    {
                        float left = 1f - MathHelper.Clamp(t / (float)LaStuckTicks, 0f, 1f);
                        Move(npc, markB);
                        shake = 7f * MathF.Exp(-t / 8f);
                        npc.rotation = angle + MathHelper.PiOver2 + 0.05f * left * MathF.Sin(t * 0.9f);

                        if (entered)
                            SlamFx(npc);

                        Smoke(npc, 0.4f + 1.2f * left);

                        if (t % 16 == 6 && t < LaStuckTicks - 16)
                            Spark(npc, seed + t * 17, 0.7f + 0.5f * left, green: true);

                        if (Authority && t >= LaStuckTicks)
                            Next(npc, LaRise);

                        break;
                    }

                default:
                    // Up out of it (or to a stop, if it met nothing), upright again.
                    npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, -4.5f), 0.1f);
                    npc.rotation = MathHelper.WrapAngle(npc.rotation).AngleLerp(0f, 0.14f);
                    hurts = true;

                    if (entered && !Main.dedServ)
                    {
                        Sound(SoundID.Mech with { Volume = 0.9f, Pitch = -0.4f }, npc.Center, 6);
                        Sound(SoundID.DeerclopsRubbleAttack with { Volume = 0.5f, Pitch = 0.1f }, npc.Center, 6);
                    }

                    if (Authority && t >= LaRiseTicks)
                        EndMove(npc);

                    break;
            }
        }

        private static void LaunchFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = Middle(npc);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 2.4f, 16));
            Sound(SoundID.DD2_BallistaTowerShot with { Volume = 1f, Pitch = -0.4f, MaxInstances = 3 }, at, 8);
            Sound(SoundID.Item74 with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, at, 8);
            Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, at, 8);
            Shake(at, 12f, 20);
        }

        /// <summary>Behind it as it goes: its fire, in its green, all along its way; flecks of it; its light.</summary>
        private void LaunchTrail(NPC npc, Vector2 dir)
        {
            if (Main.dedServ)
                return;

            Vector2 at = Middle(npc);
            PADFire.Trail(at - npc.velocity, at, 54f, FirePalette.Nuclear, 1f, smokes: timer % 2 == 0);
            Lighting.AddLight(at, 1.1f, 1.5f, 0.05f);

            if (!Seen(at, 200f))
                return;

            Vector2 across = new(-dir.Y, dir.X);

            for (int i = 0; i < 2; i++)
                Fleck(at + across * Main.rand.NextFloat(-50f, 50f), -dir * Main.rand.NextFloat(1f, 5f) + across * Main.rand.NextFloat(-1.5f, 1.5f), true, Main.rand.NextFloat(0.45f, 0.8f), Main.rand.Next(20, 36), 0.02f);
        }

        /// <summary>It meets the ground: the ground thrown up and heaved, its green out of the hole, what it lost shown over it.</summary>
        private void SlamFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = npc.Center;
            Vector2 back = -angle.ToRotationVector2();
            Color dust = new(118, 104, 92);
            float room = Room;

            flash = 1f;
            flashGreen = true;

            if (GroundUnder(at.X, at.Y - 64f, 12, out float top))
                GroundWave.Slam(new Vector2(at.X, top), 44f, 16, 22f);

            // Thrown back up the way it came, and up.
            for (int i = 0; i < (int)(18 * room); i++)
                Add(new StoneChip(at + Main.rand.NextVector2Circular(40f, 20f), (back * 0.6f - Vector2.UnitY).SafeNormalize(-Vector2.UnitY).RotatedByRandom(0.8f) * Main.rand.NextFloat(4f, 13f),
                    Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0f, 0.4f)), Main.rand.Next(50, 90), Main.rand.NextFloat(0.8f, 1.3f)));

            for (int i = 0; i < (int)(14 * room); i++)
                Add(new DustFog(at + new Vector2(Main.rand.NextFloat(-90f, 90f), Main.rand.NextFloat(-10f, 20f)), new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(0.3f, 2f)),
                    dust * 0.8f, Main.rand.Next(50, 90), Main.rand.NextFloat(0.6f, 1.1f), 0.32f));

            for (int i = 0; i < (int)(16 * room); i++)
                Fleck(at + Main.rand.NextVector2Circular(30f, 20f), (back * 0.5f - Vector2.UnitY).RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 9f), true, Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(30, 60), -0.06f);

            Sound(SoundID.DD2_OgreGroundPound with { Volume = 1f, Pitch = -0.2f }, at, 8);
            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.7f }, at, 8);
            Sound(SoundID.DeerclopsRubbleAttack with { Volume = 0.9f, Pitch = -0.3f }, at, 8);
            Shake(at, 24f, 30);

            CombatText.NewText(npc.Hitbox, new Color(255, 200, 70), (int)(npc.lifeMax * LaunchCost), dramatic: true);
        }

        // ---- drawn: what they warn with -----------------------------------------------------------------------------------------

        private void DrawMoveMarks(NPC npc, SpriteBatch batch)
        {
            Player p = Target(npc);
            Vector2 middle = npc.Center;

            switch (act)
            {
                case Act.Detonate:
                    if (step == DeSuck)
                    {
                        // How far it will reach: a light about it, growing brighter; and its green drawn in to it.
                        float u = MathHelper.Clamp(timer / (float)DeSuckTicks, 0f, 1f);
                        float beat = 0.85f + 0.15f * MathF.Sin(timer * (0.16f + 0.5f * u));
                        Vector2 at = Middle(npc);
                        DestroyerArt.DrawGlow(batch, at, DetonateReach * 2.5f, PrimeArt.Lit(PrimeArt.NuclearDeep * ((0.1f + 0.3f * u) * beat)));
                        DestroyerArt.DrawGlow(batch, at, DetonateReach * 1.5f, PrimeArt.Lit(PrimeArt.Nuclear * ((0.06f + 0.26f * u) * beat)));
                        DestroyerArt.DrawGlow(batch, at, 60f + 200f * u, PrimeArt.Lit(PrimeArt.NuclearHot * (0.3f + 0.5f * u)), hotCore: true);
                        DrawInward(batch, at, u);
                    }

                    break;

                case Act.Beams:
                    DrawBeamCharge(npc, batch);
                    break;

                case Act.Launch:
                    if (step == LaRoar)
                    {
                        Vector2 at = timer < LaLockAt ? (p?.Center ?? markA) : markA;
                        float ready = timer / (float)LaRoarTicks;
                        DrawWarn(batch, middle, (at - middle).ToRotation(), Vector2.Distance(at, middle) + 420f, ready, timer >= LaLockAt, 84f, Smooth(timer / 10f));
                    }
                    else if (step == LaGo)
                    {
                        DrawWarn(batch, middle, angle, 700f, 1f, true, 84f, 1f - Smooth(timer / (float)DestroyerArt.WarningFade));
                    }

                    break;
            }
        }

        /// <summary>Streaks of its green drawn in to a place from all about, each longer and quicker as it nears (as its end's mouth draws them, from further).</summary>
        private void DrawInward(SpriteBatch batch, Vector2 at, float u)
        {
            const int many = 18;
            const int period = 26;

            for (int k = 0; k < many; k++)
            {
                int tick = (int)(Main.GameUpdateCount % 100000) + k * 3;
                int round = tick / period;
                float phase = tick % period / (float)period;
                float a = Chance(round * 131 + k * 17 + seed) * MathHelper.TwoPi;
                float far = 220f + 300f * u + 220f * Chance(round * 7 + k * 53);
                float near = far * (1f - phase * phase);
                float length = 14f + 70f * phase;
                Vector2 way = a.ToRotationVector2();
                float alpha = MathF.Sin(phase * MathHelper.Pi) * (0.15f + 0.6f * u);

                DestroyerArt.DrawLine(batch, at + way * (near + length), at + way * near, 3f, PrimeArt.Lit(PrimeArt.Nuclear * alpha));
            }
        }

        /// <summary>The light at each eye as its beams charge: growing (by request: "eye goes green with radial slowly bigger").</summary>
        private void DrawBeamCharge(NPC npc, SpriteBatch batch)
        {
            if (step != BeCharge)
                return;

            float u = MathHelper.Clamp(timer / (float)BeChargeTicks, 0f, 1f);

            for (int eye = -1; eye <= 1; eye += 2)
                DestroyerArt.DrawGlow(batch, Eye(npc, eye), 50f + 210f * u, PrimeArt.Lit(PrimeArt.Nuclear * (0.12f + 0.3f * u)));
        }
    }
}
