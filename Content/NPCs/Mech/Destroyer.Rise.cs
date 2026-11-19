using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its rising -------------------------------------------------------------------------------------------------------
    //
    //     GATHER   130   it lies deep under the ground, far off; the camera goes there, the bars come in, the player is
    //                    held; the ground shakes, dust thrown up off it, harder and louder
    //     BURST     40   it bursts up out of the ground and slows, twenty-six tiles over it, a little toward the player;
    //                    its body comes up out of the ground after it, along the very way it came
    //     TURN      40   its head comes round onto the player, along a curve: its neck bends, it does not snap
    //     ROAR     220   it roars (MechRoar; no rings: the screen dims about it and shakes, steam and sparks out of its
    //                    mouth, its lightning crackling over its head), and its name comes up
    //     DIVE      56   it rears, and dives, curving toward the player, back into the ground; the camera walks home
    //
    // A little slower than it was (by request: "Make everything a bit slower").
    //
    // Skipped (E held, single player): straight to its dive's end, its name up, the camera home.

    public partial class Destroyer
    {
        private const int RiseGather = 0;
        private const int RiseBurst = 1;
        private const int RiseTurn = 2;
        private const int RiseRoar = 3;
        private const int RiseDive = 4;

        private const int GatherTicks = 130;
        private const int BurstTicks = 40;
        private const int TurnTicks = 40;
        private const int RoarTicks = 220;
        private const int DiveTicks = 56;

        /// <summary>Ticks into its dive the camera lets go and walks home, and how long it takes.</summary>
        private const int DiveHome = 36;
        private const int HomeTicks = 96;

        /// <summary>How far off to one side it rises; how deep it lies first; how high over the ground it comes (tiles).</summary>
        private const int Away = 95;
        private const int Deep = 45;
        private const int Tall = 26;

        /// <summary>Where it comes out of the ground; where it lies before; the top of its rising; where it turns to look; the end of its dive.</summary>
        private Vector2 emerge, start, apex, look, dive;

        /// <summary>The way from where it rises to the player, along the ground (-1 or 1).</summary>
        private float towardPlayer;

        /// <summary>This machine's player was in the shot last tick: the camera is walked home for them.</summary>
        private bool riseWatching;

        /// <summary>The bend its neck comes round by as it turns onto the player, and the way it then faces (along the end of that curve).</summary>
        private Vector2 NeckBend => apex + new Vector2(0f, -3f * 16f);

        private Vector2 LookWay => (look - NeckBend).SafeNormalize(Vector2.UnitX);

        /// <summary>Lays out where it will rise and puts it there; the scene begins. The server's (or a single player's).</summary>
        private void BeginRise(NPC npc)
        {
            npc.TargetClosest(false);
            Player p = Target(npc);

            if (p == null)
                return;

            int px = (int)(p.Center.X / 16f), py = (int)(p.Center.Y / 16f);

            // Toward the middle of the world near its edges; either side otherwise.
            int side = px < Away + 120 ? 1 : px > Main.maxTilesX - Away - 120 ? -1 : Main.rand.NextBool() ? 1 : -1;
            int x = Math.Clamp(px + side * Away, 60, Main.maxTilesX - 60);
            int ground = Ground(x, py);
            float toward = -side;

            emerge = new Vector2(x * 16 + 8, ground * 16);
            start = emerge + new Vector2(0f, Deep * 16f);
            start.Y = Math.Min(start.Y, (Main.maxTilesY - 40) * 16f);
            apex = emerge + new Vector2(toward * 5f * 16f, -Tall * 16f);
            apex.Y = Math.Max(apex.Y, 60 * 16f);
            look = apex + new Vector2(toward * 4f * 16f, -2f * 16f);
            dive = emerge + new Vector2(toward * 16f * 16f, 34f * 16f);
            dive.Y = Math.Min(dive.Y, (Main.maxTilesY - 40) * 16f);

            npc.Center = start;
            npc.velocity = Vector2.Zero;
            npc.rotation = 0f;
            npc.alpha = 0;
            npc.dontTakeDamage = true;

            Go(npc, Scene.Rise, RiseGather);
        }

        /// <summary>The ground at a column near a row: the first solid tile going down from well above it with open air over it; under the row if there is none.</summary>
        private static int Ground(int x, int near)
        {
            for (int y = Math.Max(20, near - 45); y < Math.Min(Main.maxTilesY - 40, near + 60); y++)
            {
                if (!Solid(x, y))
                    continue;

                bool open = true;

                for (int k = 1; k <= 8 && open; k++)
                    open = !Solid(x, y - k);

                if (open)
                    return y;
            }

            return Math.Min(Main.maxTilesY - 60, near + 4);
        }

        private static bool Solid(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        private static bool Solid(Vector2 world) => Solid((int)(world.X / 16f), (int)(world.Y / 16f));

        private void RunRise(NPC npc, bool entered)
        {
            int t = timer;
            Player p = Target(npc);
            towardPlayer = look.X >= emerge.X ? 1f : -1f;
            Vector2 toPlayer = p != null ? p.Center - npc.Center : new Vector2(towardPlayer, 0f);
            bool watching = Watching(emerge) || Watching(npc.Center);

            // E held in the corner (single player): straight to its dive's end.
            if (watching && step < RiseDive)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    npc.Center = dive;
                    npc.velocity = Vector2.Zero;
                    ShowName(npc);

                    if (!Main.dedServ)
                        PisPlayer.PanHome(Main.LocalPlayer.Center, 30);

                    Fight(npc);
                    return;
                }
            }

            switch (step)
            {
                case RiseGather:
                    {
                        Move(npc, start, new Vector2(0f, -1f));

                        if (watching)
                            Shot(emerge + new Vector2(0f, -10f * 16f), Smooth(t / 90f), MathHelper.Lerp(1f, 1.08f, Smooth(t / (float)GatherTicks)));

                        Tremor(emerge, t / (float)GatherTicks);

                        if (Authority && t >= GatherTicks)
                            Next(npc, RiseBurst);

                        break;
                    }

                case RiseBurst:
                    {
                        float u = 1f - (float)Math.Pow(1f - Math.Min(1f, (t + 1f) / BurstTicks), 2.4f);
                        Move(npc, Bezier(start, emerge + new Vector2(0f, -10f * 16f), apex, u));

                        if (watching)
                            Shot(Vector2.Lerp(emerge, npc.Center, 0.5f), 1f, MathHelper.Lerp(1.08f, 1.12f, u));

                        Breach(npc, 1.4f);

                        if (Authority && t >= BurstTicks)
                            Next(npc, RiseTurn);

                        break;
                    }

                case RiseTurn:
                    {
                        // Its head comes round onto the player along a curve, its neck bending after it.
                        float u = Smooth((t + 1f) / TurnTicks);
                        Move(npc, Bezier(apex, NeckBend, look, u));

                        if (watching)
                            Shot(Vector2.Lerp(emerge, npc.Center, 0.55f), 1f, 1.12f);

                        HeadGlow(npc, 0.4f + 0.6f * u);

                        if (Authority && t >= TurnTicks)
                            Next(npc, RiseRoar);

                        break;
                    }

                case RiseRoar:
                    {
                        // It shakes with its roar, its head on the player.
                        float voice = Smooth(t / 12f) * (1f - Smooth((t - RoarTicks + 50) / 50f));
                        Move(npc, look, LookWay);
                        shake = 2.5f * voice;

                        if (entered)
                            Bellow(npc, MechRoar.Sound, 1.35f);

                        BellowTick(npc, voice);

                        if (t == 24)
                            ShowName(npc);

                        if (watching)
                            Shot(Vector2.Lerp(emerge, npc.Center, 0.6f), 1f, MathHelper.Lerp(1.12f, 1.2f, Smooth(t / 40f)));

                        HeadGlow(npc, 1f + 0.6f * voice);

                        if (Authority && t >= RoarTicks)
                            Next(npc, RiseDive);

                        break;
                    }

                case RiseDive:
                    {
                        float u = (float)Math.Pow(Math.Min(1f, (t + 1f) / DiveTicks), 1.7f);
                        float toward = towardPlayer;
                        Vector2 at = Bezier(look, look + LookWay * 6f * 16f, emerge + new Vector2(toward * 10f * 16f, -8f * 16f), dive, u);
                        Move(npc, at);

                        Breach(npc, 1.1f);

                        if (t < DiveHome)
                        {
                            if (watching)
                                Shot(Vector2.Lerp(emerge, npc.Center, 0.5f), 1f, 1.12f);

                            riseWatching = watching;
                        }
                        else if (t == DiveHome && riseWatching && !Main.dedServ)
                        {
                            PisPlayer.PanHome(Vector2.Lerp(emerge, npc.Center, 0.5f), HomeTicks);
                            riseWatching = false;
                        }

                        if (Authority && t >= DiveTicks)
                            Fight(npc);

                        break;
                    }
            }
        }

        /// <summary>Its name comes up (once).</summary>
        private void ShowName(NPC npc)
        {
            if (named)
                return;

            named = true;

            if (!Main.dedServ)
                BossIntro.PlayNameOnly(npc.whoAmI, DestroyerSystem.Plate);
        }

        /// <summary>The scene over: vanilla's AI takes it from here, heading for the player.</summary>
        private void Fight(NPC npc)
        {
            npc.dontTakeDamage = false;
            npc.chaseable = true;
            Player p = Target(npc);
            npc.velocity = p != null ? (p.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 12f : new Vector2(0f, 12f);
            ShowName(npc);
            Go(npc, Scene.None, 0);
        }
    }
}
