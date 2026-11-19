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
    // ---- no arm left: its change ------------------------------------------------------------------------------------------
    //
    // By request: "Transition to no arm: Cinematic, always spikes on his head, things go dark and he shakes with sparks,
    // and his eyes glow the nuclear green and has a long trail of that green as well". Asked after: its spikes are a look
    // only; what it does stays vanilla's (its hover and its spin, their numbers). In Legends, changed, it can be hurt, and
    // has four attacks of its own (Prime.Moves).
    //
    // It begins a little after its last arm has gone down (all four fallen or lying, or gone).
    //
    //     PAN       50   the camera goes to it; it stops where it is; the player is held, and nothing touches them
    //     DARK     110   things go dark about it (a pool of light on it); it shakes, harder and harder, red sparks off
    //                    it, each sooner after the last, smoke; its eyes gutter, and go out
    //     IGNITE    90   its spikes snap out; its eyes light its green (#bbff00), a flash of it over everything; it
    //                    screeches; the dark lifts
    //     HOME      40   the camera comes home; from here its spikes stay out and its eyes stay green, a long trail of
    //                    it behind each, and the fight goes on
    //
    // Skipped (E held, single player): changed at once, the camera home.

    public partial class Prime
    {
        internal const int ChPan = 0;
        internal const int ChDark = 1;
        internal const int ChIgnite = 2;
        internal const int ChHome = 3;

        private const int ChPanTicks = 50;
        private const int ChDarkTicks = 110;
        private const int ChIgniteTicks = 90;
        private const int ChHomeTicks = 40;

        /// <summary>Ticks into DARK its eyes go out.</summary>
        private const int EyesOutAt = 86;

        /// <summary>Where it stops for its change, and for its end.</summary>
        private Vector2 holdAt;

        /// <summary>This machine's player was in the shot: the camera is walked home for them.</summary>
        private bool changeWatching;

        /// <summary>Ticks into DARK its sparks come: each gap 0.82 of the last, from 26 down to four.</summary>
        private static readonly List<int> ChangeSparks = SparkTimes(ChDarkTicks, 26f, 0.82f, 4);

        /// <summary>Ticks at which sparks come over a stretch: the first at six, each gap a share of the last, down to a least.</summary>
        private static List<int> SparkTimes(int over, float gap, float share, int least)
        {
            List<int> times = new();
            int t = 6;

            while (t < over)
            {
                times.Add(t);
                t += Math.Max(least, (int)Math.Round(gap));
                gap *= share;
            }

            return times;
        }

        private void BeginChange(NPC npc)
        {
            holdAt = npc.Center + npc.velocity * 4f;
            seed = Main.rand.Next(int.MaxValue);
            armsGone = 0;

            // Out of its spin: its spikes in, till its change puts them out for good.
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            ClearShots();
            LegendsStop(npc);
            Go(npc, Scene.Change, ChPan);
        }

        /// <summary>What its arms had out, gone: their bombs and lasers in flight (the server's).</summary>
        private static void ClearShots()
        {
            if (!Authority)
                return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile shot = Main.projectile[i];

                if (shot.active && shot.hostile && (shot.type == ProjectileID.DeathLaser || shot.type == ProjectileID.BombSkeletronPrime))
                    shot.Kill();
            }
        }

        private void RunChange(NPC npc, bool entered)
        {
            int t = timer;
            bool watching = Watching(npc.Center);

            if (watching && step < ChHome)
                changeWatching = true;

            // E held in the corner (single player): changed at once.
            if (watching && step < ChHome)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    if (!Main.dedServ)
                        PisPlayer.PanHome(Main.LocalPlayer.Center, 30);

                    changeWatching = false;
                    Changed(npc);
                    return;
                }
            }

            switch (step)
            {
                case ChPan:
                    {
                        Move(npc, Vector2.Lerp(npc.Center, holdAt, 0.2f));
                        npc.rotation = npc.rotation.AngleLerp(0f, 0.15f);

                        if (watching)
                            Shot(npc.Center, Smooth(t / 40f), MathHelper.Lerp(1f, 1.1f, Smooth(t / (float)ChPanTicks)));

                        if (Authority && t >= ChPanTicks)
                            Next(npc, ChDark);

                        break;
                    }

                case ChDark:
                    {
                        float u = t / (float)ChDarkTicks;
                        Move(npc, holdAt);
                        npc.rotation = 0.07f * u * (float)Math.Sin(t * 1.3f);
                        shake = 0.6f + 4.4f * u * u;

                        if (entered)
                            Failing(npc);

                        for (int k = 0; k < ChangeSparks.Count; k++)
                        {
                            if (At(ChangeSparks[k]))
                                Spark(npc, seed + k * 7919, 0.8f + 0.7f * u, green: false);
                        }

                        Smoke(npc, 0.3f + 1.3f * u);

                        if (At(EyesOutAt))
                            EyesOut(npc);

                        if (watching)
                        {
                            Shot(npc.Center, 1f, MathHelper.Lerp(1.1f, 1.2f, Smooth(u)));
                            Cinematic.Shade(Middle(npc), 340f, 0.88f * Smooth(t / 36f));
                        }

                        if (Authority && t >= ChDarkTicks)
                            Next(npc, ChIgnite);

                        break;
                    }

                case ChIgnite:
                    {
                        float u = t / (float)ChIgniteTicks;
                        float settle = 1f - Smooth(t / 54f);
                        Move(npc, holdAt);
                        npc.rotation = 0.09f * settle * (float)Math.Sin(t * 1.7f);
                        shake = 7f * settle;

                        if (entered)
                            Ignite(npc);

                        if (At(4))
                            Screech(npc, -0.22f);

                        float into = (t - 4) / (float)ScreechLong;
                        ScreechTick(npc, into < 0f || into > 1f ? 0f : Smooth(into * 12f) * (1f - Smooth((into - 0.8f) / 0.2f)));

                        if (t < 60 && t % 9 == 2)
                            Spark(npc, seed + 31 + t * 131, 1.2f * settle + 0.4f, green: true);

                        if (watching)
                        {
                            Shot(npc.Center, 1f, MathHelper.Lerp(1.2f, 1.08f, Smooth(u)));
                            Cinematic.Shade(Middle(npc), 340f + 320f * Smooth(u), 0.88f * (1f - Smooth(t / 64f)));
                        }

                        if (Authority && t >= ChIgniteTicks)
                            Next(npc, ChHome);

                        break;
                    }

                case ChHome:
                    {
                        Move(npc, holdAt + new Vector2(0f, 4f * (float)Math.Sin(t * 0.08f)));
                        npc.rotation = npc.rotation.AngleLerp(0f, 0.2f);

                        if (entered && changeWatching && !Main.dedServ)
                        {
                            changeWatching = false;
                            PisPlayer.PanHome(npc.Center, ChHomeTicks);
                        }

                        if (Authority && t >= ChHomeTicks)
                            Changed(npc);

                        break;
                    }
            }
        }

        /// <summary>Changed for good: its spikes out, its eyes green; vanilla's AI from a fresh start.</summary>
        private void Changed(NPC npc)
        {
            changed = true;
            npc.velocity = Vector2.Zero;
            npc.rotation = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;

            // Legends: a stretch of its hover before the first of its own attacks.
            restFor = MoveRestMost;
            Go(npc, Scene.None, 0);
        }
    }
}
