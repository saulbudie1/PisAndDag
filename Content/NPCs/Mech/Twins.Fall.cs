using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- the first of them to fall --------------------------------------------------------------------------------------
    //
    // By request: "Death of the first one: no zoom no pan, just expo shorter spark interval for 3 sec and black smoke coming
    // out and fall down to the ground, at 1 HP, doesnt move and invincible and not dealling damage", and asked after: "Spark
    // 3s in mid air then falls with big impact".
    //
    //     SPARK   180   held at one life, untouchable, harmless: it stops where it is and hangs there, trembling, its eye
    //                   guttering; sparks of its colour off it, each sooner after the last (34 ticks, then 0.8 of that each
    //                   time, down to three), and black smoke out of it, more and more
    //     DROP     ~    it falls, tumbling, smoke trailing, and hits the ground hard: rock and dust thrown out to both sides
    //                   along the ground, a thud that shakes the screen
    //     (DOWN)        it lies there on its side, smoking, now and then a spark, till its twin's end comes for it
    //
    // Its twin fights on meanwhile, vanilla's way. If its twin goes (flies off with the day), it goes too.

    public partial class Twins
    {
        private const int FallSpark = 0;
        private const int FallDrop = 1;

        private const int SparkTicks = 180;

        /// <summary>The ground under it, in world pixels (worked out by the server as it begins to fall).</summary>
        private float groundY;

        /// <summary>Where it lies, and how it lies (its eye to one side and a little down).</summary>
        private Vector2 restAt;
        private float restRot;

        /// <summary>The way it tumbles as it falls, and lies (-1 or 1).</summary>
        private int spin = 1;

        /// <summary>This machine has seen it land.</summary>
        private bool landed;

        /// <summary>Its eye's middle sits this far over the ground when it lies there (a little sunk in).</summary>
        private const float LieHigh = 50f;

        /// <summary>Where its centre is when it lies on the ground (its eye's middle that far over it).</summary>
        private float RestY(NPC npc) => groundY - LieHigh + (npc.Center.Y - EyeCentre(npc).Y);

        /// <summary>Ticks into SPARK its sparks come: each gap 0.8 of the last, from 34 down to three.</summary>
        private static readonly List<int> SparkTimes = MakeSparkTimes();

        private static List<int> MakeSparkTimes()
        {
            List<int> times = new();
            float gap = 34f;
            int t = 8;

            while (t < SparkTicks)
            {
                times.Add(t);
                t += Math.Max(3, (int)Math.Round(gap));
                gap *= 0.8f;
            }

            return times;
        }

        internal bool Ending => ending;

        private void BeginFall(NPC npc)
        {
            if (waiting)
            {
                waiting = false;
                npc.ai[0] = 3f;
            }

            seed = Main.rand.Next(int.MaxValue);
            spin = Main.rand.NextBool() ? 1 : -1;
            landed = false;
            Go(npc, Scene.Fall, FallSpark);
        }

        private void RunFall(NPC npc, bool entered)
        {
            int t = timer;

            switch (step)
            {
                case FallSpark:
                    {
                        float u = t / (float)SparkTicks;

                        // Still: what way it had, gone in a moment; its eye sagging a little toward the ground.
                        npc.velocity *= 0.8f;

                        if (npc.velocity.LengthSquared() < 0.02f)
                            npc.velocity = Vector2.Zero;

                        npc.rotation = npc.rotation.AngleLerp(LookAlong(new Vector2(spin * 0.35f, 1f)), 0.012f);
                        shake = 0.4f + 2f * u * u;

                        if (entered)
                            Failing(npc);

                        for (int k = 0; k < SparkTimes.Count; k++)
                        {
                            if (SparkTimes[k] == t)
                                Spark(npc, seed + k * 7919, 0.7f + 0.6f * u);
                        }

                        Smoke(npc, 0.3f + 1.9f * u);
                        EyeLight(npc, 0.9f * (1f - 0.5f * u), flicker: 0.2f + 0.6f * u);

                        if (Authority && t >= SparkTicks)
                        {
                            groundY = Ground(npc);
                            Next(npc, FallDrop);
                        }

                        break;
                    }

                case FallDrop:
                    {
                        if (entered)
                            Dropping(npc);

                        float vy = Math.Min(npc.velocity.Y + 0.6f, 24f);
                        npc.velocity = new Vector2(spin * 0.8f, vy);
                        npc.rotation += spin * 0.045f;
                        Smoke(npc, 1.6f, falling: true);

                        // On the ground: held there (every machine), and the server lays it down.
                        float rest = RestY(npc);

                        if (npc.Center.Y + npc.velocity.Y >= rest)
                        {
                            npc.velocity = new Vector2(0f, rest - npc.Center.Y);

                            if (Authority)
                            {
                                restAt = new Vector2(npc.Center.X, rest);
                                restRot = LookAlong(new Vector2(spin, 0.45f));
                                Go(npc, Scene.Down, 0);
                            }
                        }

                        break;
                    }
            }
        }

        /// <summary>
        /// The ground under it: the highest solid top (or platform) in the columns under its eye, near its middle. Under
        /// a chasm, as far as two hundred and sixty tiles down; the world's floor if nothing.
        /// </summary>
        private static float Ground(NPC npc)
        {
            int x0 = (int)(npc.Center.X / 16f);
            int y0 = (int)(EyeCentre(npc).Y / 16f) + 2;
            int bottom = Math.Min(Main.maxTilesY - 12, y0 + 260);
            int best = bottom;

            for (int x = x0 - 2; x <= x0 + 2; x++)
            {
                for (int y = Math.Max(10, y0); y < best; y++)
                {
                    if (!WorldGen.InWorld(x, y, 2))
                        break;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                    {
                        best = y;
                        break;
                    }
                }
            }

            return best * 16f;
        }

        // ---- lying there ----------------------------------------------------------------------------------------------------

        private void RunDown(NPC npc, bool entered, NPC other, Twins twin)
        {
            // Its twin gone (off with the day, or its player): it goes too, with nothing.
            if (other == null || !other.active)
            {
                if (Authority)
                {
                    npc.active = false;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }

                return;
            }

            if (entered && !landed)
            {
                landed = true;
                Impact(npc);
            }

            if (twin.scene == Scene.Finale)
            {
                FollowFinale(npc, other, twin);
                return;
            }

            Lie(npc, timer);
        }

        /// <summary>On its side on the ground: a bounce off it and a roll into place, then still, smoking.</summary>
        private void Lie(NPC npc, int t)
        {
            float bounce = t < 24 ? -16f * Math.Abs((float)Math.Sin(t * MathHelper.Pi / 12f)) * (1f - t / 24f) : 0f;
            Move(npc, restAt + new Vector2(0f, bounce));
            npc.rotation = npc.rotation.AngleLerp(restRot, t < 24 ? 0.16f : 0.3f);

            if (t < 12)
                shake = 3f * (1f - t / 12f);

            Smoke(npc, 0.3f);

            if (Main.rand.NextBool(170))
                Spark(npc, Main.rand.Next(), 0.5f);

            EyeLight(npc, 0.25f, flicker: 0.8f);
        }
    }
}
