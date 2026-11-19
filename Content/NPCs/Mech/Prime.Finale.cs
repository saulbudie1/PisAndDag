using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its end ----------------------------------------------------------------------------------------------------------
    //
    // By request: "Head: cinematic: pans over, shakes, opens mouth and theres a expo glowing nuclear green aura from mouth
    // like about to shoot something, Pans to the nearest arm and its moving, flaoting and flies over to the head, pans to
    // head and all four limbs reach the mouth at once and cover its mouth, things go darker temporarily with only the rays
    // of green from mouth that spreads outwards, covered by hands, and create a super cool impact explosion frame,
    // creating a giant explosion and an area that is radio active for 5 mins. Make it cool". Asked after: the explosion
    // a big hit, dodgeable.
    //
    //     CEASE     50   the camera goes to it; held at one life, untouchable, harmless, it stops where it is. Its arms
    //                    still up go down with it: each falls and lies as one struck down does. The player is held, and
    //                    nothing touches them
    //     SHAKE     70   it shakes, harder and harder, sparking, smoking (its eyes guttering, if they are red still)
    //     OPEN     110   its jaw drops; a light of its green in its mouth, small, then growing faster and faster (its
    //                    eyes its green from here, if they were not), a whine rising with it (PrimeCharge)
    //     ARM       60   the camera goes to the nearest of its arms, lying on the ground
    //     STIR      70   it stirs: it shakes, its lamp comes on green; it lifts off the ground and hangs there, its
    //                    hand turning toward its head (every arm does)
    //     FLY       80   every arm flies at its mouth, quicker and quicker, to reach it in the same tick; the camera
    //                    goes with the nearest, back to its head
    //     COVER     40   their hands close over its mouth, hard: the whine cut off. The player is let go, to run; the
    //                    camera pulls back to a shot that keeps the end and the player both (EndShot), held on by
    //                    its blast till its cloud has climbed
    //     DARK     170   things go dark; only its green shows, in rays out between its hands and up out of its eyes,
    //                    reaching further and further (as far as it will hurt at the last); its hands are thrown back
    //                    and forced down again, sooner and sooner (PrimeSeal)
    //     IMPACT    12   three still frames, black and white and its green; and it goes up (PrimeBlast: thirty tiles
    //                    of it hurts), its arms with it, and the ground it stood over is left radioactive (PrimeFallout)
    //
    // No arm of it here: from OPEN straight to DARK, its light in the open. Skipped (E held, single player): on to COVER,
    // the camera home.

    public partial class Prime
    {
        internal const int FinCease = 0;
        internal const int FinShake = 1;
        internal const int FinOpen = 2;
        internal const int FinArm = 3;
        internal const int FinStir = 4;
        internal const int FinFly = 5;
        internal const int FinCover = 6;
        internal const int FinDark = 7;
        internal const int FinImpact = 8;

        private const int FinCeaseTicks = 50;
        private const int FinShakeTicks = 70;
        private const int FinOpenTicks = 110;
        private const int FinArmTicks = 60;
        internal const int FinStirTicks = 70;
        internal const int FinFlyTicks = 80;
        private const int FinCoverTicks = 40;
        internal const int FinDarkTicks = 170;
        internal const int FinImpactTicks = 12;

        /// <summary>Ticks into OPEN its jaw drops; ticks into COVER the camera lets go.</summary>
        private const int OpenAt = 10;
        private const int CoverHold = 22;

        /// <summary>Ticks into STIR an arm lifts off the ground, and how high it hangs.</summary>
        internal const int LiftAt = 34;
        internal const float StirHigh = 74f;

        /// <summary>How far its end hurts, and its light reaches at the last (by request: "a giant explosion"; asked after: a big hit, dodgeable).</summary>
        public const float BlastReach = 30f * 16f;

        /// <summary>The arm the camera goes to: the nearest to it (the server's pick).</summary>
        private int nearest = -1;

        /// <summary>This machine's player was in its end's shot: held on its mouth as its hands close (not after a skip).</summary>
        private bool endWatching;

        private static readonly List<int> EndSparks = SparkTimes(FinShakeTicks, 22f, 0.84f, 4);

        /// <summary>Ticks into DARK its hands are thrown back: each gap 0.8 of the last, from 34 down to five.</summary>
        internal static readonly List<int> Pulses = SparkTimes(FinDarkTicks, 34f, 0.8f, 5);

        private void BeginFinale(NPC npc)
        {
            holdAt = npc.Center + npc.velocity * 3f;
            seed = Main.rand.Next(int.MaxValue);
            nearest = -1;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            ClearShots();
            LegendsStop(npc);

            // Its arms still up go down with it.
            int n = FindArms(npc);
            Span<int> arms = stackalloc int[n];
            armList.AsSpan(0, n).CopyTo(arms);

            foreach (int i in arms)
            {
                if (Main.npc[i].TryGetGlobalNPC(out PrimeArm arm) && arm.Up)
                    arm.BeginFall(Main.npc[i]);
            }

            Go(npc, Scene.Finale, FinCease);
        }

        /// <summary>
        /// How far its mouth's light has grown, 0 to 1 (by request: "expo glowing"): from its jaw's drop, slowly and then
        /// all at once to most of it while the camera is on it, and the rest by the time its arms reach it.
        /// </summary>
        internal float Aura
        {
            get
            {
                if (scene != Scene.Finale || step < FinOpen)
                    return 0f;

                if (step >= FinCover)
                    return 1f;

                const float grown = 0.7f;
                int open = FinOpenTicks - OpenAt;

                if (step == FinOpen)
                {
                    float u = MathHelper.Clamp((timer - OpenAt) / (float)open, 0f, 1f);
                    return grown * (MathF.Exp(4.2f * u) - 1f) / (MathF.Exp(4.2f) - 1f) + 0.05f * Smooth(u * 5f) * (1f - u);
                }

                int since = timer + (step > FinArm ? FinArmTicks : 0) + (step > FinStir ? FinStirTicks : 0);
                return MathHelper.Lerp(grown, 1f, MathHelper.Clamp(since / (float)(FinArmTicks + FinStirTicks + FinFlyTicks), 0f, 1f));
            }
        }

        // ---- where its arms go (PrimeArm.FollowFinale) --------------------------------------------------------------------

        /// <summary>
        /// The way an arm's limb lies out from its mouth as its hand covers it: its upper pair in from either side,
        /// under its eyes; its lower pair up from under it. Its light comes out between them.
        /// </summary>
        internal static float CoverAngle(int type) => MathHelper.ToRadians(type switch
        {
            NPCID.PrimeCannon => -12f,
            NPCID.PrimeVice => 52f,
            NPCID.PrimeLaser => 192f,
            _ => 128f,
        });

        /// <summary>How far an arm's hand is thrown back off its mouth, this many ticks into DARK: a blow at each pulse, dying away, and a tremble that grows.</summary>
        internal static float PushAt(int t, int type)
        {
            float u = MathHelper.Clamp(t / (float)FinDarkTicks, 0f, 1f);
            float push = 0f;

            for (int k = Pulses.Count - 1; k >= 0; k--)
            {
                if (Pulses[k] > t)
                    continue;

                push = 12f * (0.45f + u) * MathF.Exp(-(t - Pulses[k]) / 5f);
                break;
            }

            return push + 1.4f * u * (1f + MathF.Sin(t * 1.9f + type * 2.1f));
        }

        /// <summary>
        /// Where one of its arms is as its hand covers its mouth: the middle of its hand's picture, and its turn. Its
        /// tool's end on its mouth, its limb lying out along its way; <paramref name="push"/> thrown back off it.
        /// </summary>
        internal void CoverPose(NPC head, int type, float handHalf, float push, out Vector2 pivot, out float rotation)
        {
            Vector2 tail = CoverAngle(type).ToRotationVector2();
            pivot = Mouth(head) + tail * (handHalf - 6f + push);
            rotation = PrimeArm.TurnOf(-tail);
        }

        // ---- its own ------------------------------------------------------------------------------------------------------

        private void RunFinale(NPC npc, bool entered)
        {
            int t = timer;
            int arms = FindArms(npc);
            NPC near = null;

            if (nearest >= 0 && nearest < Main.maxNPCs && Main.npc[nearest].active && IsArm(Main.npc[nearest].type) && (int)Main.npc[nearest].ai[1] == npc.whoAmI)
                near = Main.npc[nearest];

            Vector2 mouth = Mouth(npc);
            Vector2 armAt = near != null ? PrimeArm.LimbMiddle(near) : mouth;
            bool watching = Watching(npc.Center);

            if (watching && step <= FinCover)
                endWatching = true;

            // E held in the corner (single player): on to its hands closing, the camera home.
            if (watching && step < FinCover)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    StopSounds();
                    endWatching = false;

                    if (!Main.dedServ)
                        PisPlayer.PanHome(Main.LocalPlayer.Center, 30);

                    Next(npc, arms > 0 ? FinCover : FinDark);
                    return;
                }
            }

            float aura = Aura;

            switch (step)
            {
                case FinCease:
                    {
                        Move(npc, Vector2.Lerp(npc.Center, holdAt, 0.25f));
                        npc.rotation = npc.rotation.AngleLerp(0f, 0.15f);
                        shake = 3f * (1f - Smooth(t / 20f));

                        if (entered)
                        {
                            Failing(npc);
                            endShot = -1;
                        }

                        Smoke(npc, 0.5f);

                        if (watching)
                            Shot(npc.Center, Smooth(t / 40f), MathHelper.Lerp(1f, 1.08f, Smooth(t / (float)FinCeaseTicks)));

                        if (Authority && t >= FinCeaseTicks)
                            Next(npc, FinShake);

                        break;
                    }

                case FinShake:
                    {
                        float u = t / (float)FinShakeTicks;
                        Move(npc, holdAt);
                        npc.rotation = 0.08f * u * (float)Math.Sin(t * 1.4f);
                        shake = 0.8f + 4.2f * u * u;

                        for (int k = 0; k < EndSparks.Count; k++)
                        {
                            if (At(EndSparks[k]))
                                Spark(npc, seed + k * 7919, 0.8f + 0.7f * u, green: changed);
                        }

                        Smoke(npc, 0.5f + 1.5f * u);

                        if (watching)
                            Shot(npc.Center, 1f, MathHelper.Lerp(1.08f, 1.14f, Smooth(u)));

                        if (Authority && t >= FinShakeTicks)
                            Next(npc, FinOpen);

                        break;
                    }

                case FinOpen:
                    {
                        Move(npc, holdAt);
                        npc.rotation = 0.03f * (float)Math.Sin(t * 1.1f);
                        shake = 1.2f + 2.6f * aura;

                        if (At(OpenAt))
                            JawDrop(npc);

                        AuraTick(npc, aura);
                        Smoke(npc, 0.4f);

                        if (watching)
                            Shot(mouth, 1f, MathHelper.Lerp(1.14f, 1.24f, Smooth(t / (float)FinOpenTicks)));

                        if (Authority && t >= FinOpenTicks)
                        {
                            nearest = Nearest(npc, arms);
                            Next(npc, arms > 0 ? FinArm : FinDark);
                        }

                        break;
                    }

                case FinArm:
                case FinStir:
                case FinFly:
                    {
                        Move(npc, holdAt);
                        npc.rotation = 0.03f * (float)Math.Sin(t * 1.1f);
                        shake = 1.2f + 2.6f * aura;
                        AuraTick(npc, aura);
                        Smoke(npc, 0.4f);

                        if (watching)
                        {
                            if (step == FinArm)
                            {
                                float u = Smooth((t + 1f) / FinArmTicks);
                                Shot(Vector2.Lerp(mouth, armAt, u), 1f, MathHelper.Lerp(1.24f, 1.12f, u));
                            }
                            else if (step == FinStir)
                            {
                                Shot(armAt, 1f, MathHelper.Lerp(1.12f, 1.16f, Smooth(t / (float)FinStirTicks)));
                            }
                            else
                            {
                                // With it, and onto its head as it gets there.
                                float u = Smooth((t + 1f) / FinFlyTicks * 1.1f);
                                Shot(Vector2.Lerp(armAt, mouth, u), 1f, MathHelper.Lerp(1.16f, 1.22f, u));
                            }
                        }

                        int length = step == FinArm ? FinArmTicks : step == FinStir ? FinStirTicks : FinFlyTicks;

                        if (Authority && t >= length)
                            Next(npc, step + 1);

                        break;
                    }

                case FinCover:
                    {
                        // Their hands close on it: it is knocked back a little by it.
                        Move(npc, holdAt + new Vector2(0f, -7f * MathF.Exp(-t / 5f)));
                        npc.rotation = 0.05f * MathF.Exp(-t / 8f) * (float)Math.Sin(t * 1.9f);
                        shake = 1f + 6f * MathF.Exp(-t / 8f);

                        if (entered)
                            Slam(npc, arms);

                        SealTick(npc, 0f);

                        if (t < CoverHold)
                        {
                            if (endWatching && watching)
                                Shot(mouth, 1f, 1.24f);
                        }
                        else if (watching)
                        {
                            endWatching = false;
                            EndShot(mouth, EndAbove);
                        }

                        if (Authority && t >= FinCoverTicks)
                            Next(npc, FinDark);

                        break;
                    }

                case FinDark:
                    {
                        float u = t / (float)FinDarkTicks;
                        Move(npc, holdAt);
                        npc.rotation = 0.05f * u * (float)Math.Sin(t * 1.6f);
                        shake = 1f + 5f * u * u;

                        // Here with no hands to close on it: its seal's sound begun here.
                        if (entered && arms == 0)
                            Slam(npc, 0);

                        for (int k = 0; k < Pulses.Count; k++)
                        {
                            if (At(Pulses[k]))
                                Pulse(npc, 0.45f + u, arms);
                        }

                        SealTick(npc, u);

                        if (watching)
                        {
                            endWatching = false;
                            EndShot(mouth, MathHelper.Lerp(EndAbove, EndAboveDark, MathF.Pow(u, 1.7f)));
                            Cinematic.Shade(mouth, 130f, 0.84f * Smooth(t / 30f));
                            Cinematic.Light(DarkDraw(npc));
                        }

                        if (Authority && t >= FinDarkTicks)
                            Next(npc, FinImpact);

                        break;
                    }

                case FinImpact:
                    {
                        Move(npc, holdAt);
                        npc.rotation = 0f;

                        if (entered)
                            StopSounds();

                        if (watching)
                        {
                            EndShot(mouth, EndAboveDark);
                            Cinematic.Shade(mouth, 130f, 0.84f);
                            Cinematic.Light(ImpactDraw(npc));
                        }

                        if (Authority && t >= FinImpactTicks - 1)
                            Blast(npc);

                        break;
                    }
            }
        }

        // ---- the end's wide shot ------------------------------------------------------------------------------------------
        //
        // Asked after (seen in the previews): once the player is let go to run, the camera stays on the end, so its rays and
        // its cloud are not lost off the edge of the screen. A shot that keeps its mouth, what it puts up, and the player:
        // the player free, the lean let go (back to the player's own zoom), the bars gone. PrimeBlast holds it on till its
        // cloud has climbed, then walks it home.

        /// <summary>How far over its mouth the shot keeps: its head and its first rays; the most its rays reach up in DARK; room over its cloud's cap.</summary>
        internal const float EndAbove = 160f;
        internal const float EndAboveDark = 400f;
        internal const float CapRoom = 90f;

        /// <summary>Ticks the shot takes to come round from wherever the camera was.</summary>
        private const int EndEase = 40;

        /// <summary>Ticks into the end's shot (this machine's; -1 before it), and where the camera was as it began.</summary>
        private int endShot = -1;
        private Vector2 endFrom;

        private void EndShot(Vector2 mouth, float above)
        {
            if (Main.dedServ)
                return;

            if (endShot < 0)
            {
                endShot = 0;
                endFrom = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            }

            PisPlayer.BlendCamera(Vector2.Lerp(endFrom, EndFrame(mouth, above), Smooth(endShot / (float)EndEase)), 1f);
            endShot++;
        }

        /// <summary>
        /// Where the end's shot looks, for this machine's player: the middle of what it keeps (from <paramref name="above"/>
        /// over its mouth to a little under it, a little to either side, and the player), moved as little as it must for
        /// the player to stay well inside the screen (they are playing).
        /// </summary>
        internal static Vector2 EndFrame(Vector2 mouth, float above)
        {
            Player me = Main.LocalPlayer;
            float left = Math.Min(mouth.X - 220f, me.position.X - 60f);
            float right = Math.Max(mouth.X + 220f, me.position.X + me.width + 60f);
            float top = Math.Min(mouth.Y - above, me.position.Y - 60f);
            float bottom = Math.Max(mouth.Y + 120f, me.position.Y + me.height + 40f);
            Vector2 at = new((left + right) * 0.5f, (top + bottom) * 0.5f);

            float zoom = Math.Max(1f, Main.GameViewMatrix.Zoom.X);
            float halfWide = Math.Max(0f, Main.screenWidth * 0.5f / zoom - 90f);
            float halfHigh = Math.Max(0f, Main.screenHeight * 0.5f / zoom - 80f);

            return new Vector2(
                MathHelper.Clamp(at.X, me.Center.X - halfWide, me.Center.X + halfWide),
                MathHelper.Clamp(at.Y, me.Center.Y - halfHigh, me.Center.Y + halfHigh));
        }

        /// <summary>The nearest of its arms to it (-1 if it has none): the one the camera goes to.</summary>
        private static int Nearest(NPC npc, int arms)
        {
            int best = -1;
            float least = float.MaxValue;

            for (int k = 0; k < arms; k++)
            {
                float d = Vector2.DistanceSquared(Main.npc[armList[k]].Center, npc.Center);

                if (d < least)
                {
                    least = d;
                    best = armList[k];
                }
            }

            return best;
        }

        /// <summary>
        /// What it drops is thrown clear of the ground it leaves radioactive (or its loot would lie in the middle of it
        /// for five minutes): it lands just outside it, toward the nearest player.
        /// </summary>
        private const bool ThrowsLootClear = true;

        /// <summary>It goes up: the blast at its mouth, the ground left radioactive, its arms gone with it, and itself, with the loot. The server's.</summary>
        private void Blast(NPC npc)
        {
            Vector2 at = Mouth(npc);
            PrimeBlast.Spawn(npc, at);
            PrimeFallout.Add(at);

            int n = FindArms(npc);
            Span<int> arms = stackalloc int[n];
            armList.AsSpan(0, n).CopyTo(arms);

            foreach (int i in arms)
            {
                if (Main.npc[i].TryGetGlobalNPC(out PrimeArm arm))
                    arm.End(Main.npc[i]);
            }

            // Vanilla drops what it drops where it is: so it is put, for its last tick, where its loot should land.
            if (ThrowsLootClear)
                npc.Center = ClearOf(at);

            End(npc);
        }

        /// <summary>
        /// A place just outside the ground a blast at <paramref name="at"/> leaves radioactive, toward the nearest
        /// living player (on their far side of its middle, if they are inside it); not in the ground (up to twelve tiles
        /// over it; else where that player is). The blast's own middle if there is nobody.
        /// </summary>
        private static Vector2 ClearOf(Vector2 at)
        {
            Player nearest = null;
            float least = float.MaxValue;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];

                if (!p.active || p.dead)
                    continue;

                float d = Vector2.DistanceSquared(p.Center, at);

                if (d < least)
                {
                    least = d;
                    nearest = p;
                }
            }

            if (nearest == null)
                return at;

            Vector2 way = (nearest.Center - at).SafeNormalize(-Vector2.UnitY);
            Vector2 spot = at + way * (PrimeFallout.Reach + 56f);

            for (int k = 0; k < 12; k++)
            {
                Vector2 open = spot - new Vector2(0f, k * 16f);

                if (!Collision.SolidCollision(open - new Vector2(16f), 32, 32))
                    return open;
            }

            return nearest.Center;
        }
    }
}
