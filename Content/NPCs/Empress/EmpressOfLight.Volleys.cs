using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- her bow's acts (Legends, her second half) ----------------------------------------------------------------
    //
    // Each with the bow raised at the player and drawn, her other hand pulling the rainbow string back to her chest (by
    // request: "have animation of her pulling the rainbow string on that bow"); her arrows Daybreak's shape in her light
    // (EmpressArrow), images of them trailing them.
    //
    //   THE MOORED VOLLEY (by request), three to seven arrows, one at a time ("I didnt mean to shoot them all at once")
    //     AIM      24   the bow raised at the player
    //     then for each arrow:
    //     DRAW     30   one arrow nocked and the string pulled back to her chest; the way it will go (at the player as the
    //                   draw begins, a little to one side of them, sweeping across them arrow by arrow) shown to the
    //                   border, and how far its burst will reach where it sticks (by request: "Shoots 3 to 7 arrows that
    //                   travels fast with indicator")
    //     LOOSE    12   fast; it sticks in the border, and three seconds after bursts, thirty tiles of light ("it is stuck
    //                   onto the border and explode after 3 seconds of being stuck into a 30 tile radius"); the last
    //                   held 26, then over. So they burst one after another, as they were shot.
    //
    //   THE RICOCHETS (by request)
    //     AIM      20   the bow raised at the player
    //     DRAW     46   two to four arrows of glass, fanned; each one's way shown wall to wall as a mirror sends it, its
    //                   three bounces and the wall it breaks on
    //     LOOSE    30   very fast; off each wall as off a mirror, losing nothing (by request: "use mirror thru construction
    //                   line perpendicular to border, bounce on corner doesnt loose magnitude of velocity ... Bounce 3
    //                   times before fading")
    //
    //   THE LIGHTSPEED SHOT (offered, chosen), twice
    //     DRAW  70, 46  one arrow drawn slowly, a whine climbing; a thin line from it to the player, following them
    //     LOCK  30, 22  the line holds where it is, white and pulsing (two pings): it will go there
    //     FIRE     18   faster than the eye: a line of light to the border at once, a thunderclap of glass; the line
    //                   burns a moment where it went; she is thrown back by it
    //
    //   THE REFRACTION VOLLEY (offered, chosen)
    //     CAST     30   a prism of glass cast out of her other hand, between her and the player
    //     DRAW     36   the bow drawn at it
    //     LOOSE    56   the arrow into it, and through it: seven, her seven colours, fanned out of it every way, slowing to a
    //                   stop; held; then one after another a line of light from each to the player and down it, fast
    //                   (EmpressArrow, Refract); the prism cracks and is gone
    //
    //   THE RAIN OF LIGHT (offered, chosen)
    //     AIM 22, DRAW 30, LOOSE 16: five arrows straight up, out of the top of the arena
    //     three waves of 60: where they come down, columns of light, each shown first as a thin line the arena's whole
    //     height (36 ticks), then falling top to bottom (6), there a moment (18): a row of them all across the screen about
    //     the player, each its own distance from the next (6 to 11 tiles), each wave's row its own
    //   or, by turns of chance (by request: "arrow rain can have 2 types, the one you have now and the one that is kinda
    //   like radahn's great bow, 3 waves each last 2 seconds with 1 second interval", then "for radahn style i mean the
    //   columns not arrows, slanted and shot one by one with ranging intervals within 2 seconds. (ofc have indicator
    //   before)"), the starfall:
    //     AIM 22, DRAW 30, LOOSE 16: nine arrows up out of the arena, a great volley
    //     three waves of three seconds: nine columns of light, each slanted its own way (by request: "make it randomly
    //     slanted, sorta like daedalus storm bow. and it pulses right away not coming down"), crossing about where the
    //     player was as the wave began; each shown first as a thin line border to border (36 ticks), then all of it at
    //     once, a pulse of light down its whole length, fading (22); one after another at uneven times through one
    //     second of the wave (by request, then: "faster maybe the same amount for 1 second bit less slanted"), a
    //     second quiet before the next
    //
    // The arrows hurt (60 in a normal world, 40 in an expert one, doubled as the game doubles any hostile shot), the
    // moored bursts and the lightspeed shot more.

    public partial class EmpressOfLight
    {
        // ---- the moored volley -------------------------------------------------------------------------------

        private const int MoorAim = 0;
        private const int MoorDraw = 1;
        private const int MoorLoose = 2;

        private const int MoorAimTicks = 24;
        private const int MoorDrawTicks = 30;
        private const int MoorLooseTicks = 12;
        private const int MoorLastTicks = 26;

        /// <summary>How fast her arrows go; how far apart a volley's ways are about the player (radians) and its widest.</summary>
        private const float MoorSpeed = 30f;
        private const float MoorApart = 0.17f;
        private const float MoorWidest = 1.05f;

        private const int ArrowNormal = 60;
        private const int ArrowExpert = 40;

        private void PlanMoor(NPC npc, Player target)
        {
            actN = 3 + Main.rand.Next(5);
            actCount = 0;
            actTurn = (target.Center - npc.Center).ToRotation();
        }

        /// <summary>A volley's arrow i of n, about the way she aims.</summary>
        private static float Fanned(float aim, int i, int n, float apart, float widest)
        {
            if (n <= 1)
                return aim;

            float spread = Math.Min(apart * (n - 1), widest);
            return aim + (i / (float)(n - 1) - 0.5f) * spread;
        }

        private float FanOf(int n, float apart, float widest) => n <= 1 ? 0f : Math.Min(apart * (n - 1), widest);

        /// <summary>
        /// The way the moored volley's next arrow (actCount) will go: at the player now, turned to its place in the fan
        /// (the fan swept across them one way or the other, arrow by arrow). The server's, as a draw begins.
        /// </summary>
        private float MoorWay(NPC npc, Player who)
        {
            int k = actSeed % 2 == 0 ? actCount : actN - 1 - actCount;
            return Fanned((who.Center - npc.Center).ToRotation(), k, actN, MoorApart, MoorWidest);
        }

        /// <summary>The colour of the moored volley's arrow now on the string.</summary>
        private float MoorLook => actSeed % 97 / 97f + actCount * 0.13f;

        private void RunMoor(NPC npc, Player who, bool entered)
        {
            int t = timer;
            npc.velocity *= 0.86f;

            switch (beat)
            {
                case MoorAim:
                    {
                        float aim = (who.Center - npc.Center).ToRotation();
                        Aim(aim);

                        if (entered)
                            Sound(EmpressSounds.WhooshUp, npc.Center, 0.1f, 0.5f, 2);

                        if (Authority && t >= MoorAimTicks)
                        {
                            // The first arrow's way, fixed now and shown.
                            actCount = 0;
                            actTurn = MoorWay(npc, who);
                            Next(npc, MoorDraw);
                        }

                        break;
                    }

                case MoorDraw:
                    // One arrow, the string pulled back to her chest.
                    Aim(actTurn, Ease(t / 22f), 1, 0f, MoorLook);

                    if (entered)
                        Sound(EmpressSounds.Draw, npc.Center, 0.06f * actCount, 0.75f, 2);

                    if (Authority && t >= MoorDrawTicks)
                        Next(npc, MoorLoose);

                    break;

                case MoorLoose:
                    {
                        Aim(actTurn);
                        bool last = actCount >= actN - 1;

                        if (entered)
                        {
                            Loosed();
                            LooseVolley(npc, EmpressArrow.Kind.Moor, 1, MoorApart, MoorWidest, MoorSpeed, MoorLook);
                            npc.velocity -= actTurn.ToRotationVector2() * 2.5f;
                        }

                        if (Authority && t >= (last ? MoorLastTicks : MoorLooseTicks))
                        {
                            if (last)
                            {
                                EndAct(npc);
                            }
                            else
                            {
                                actCount++;
                                actTurn = MoorWay(npc, who);
                                Next(npc, MoorDraw);
                            }
                        }

                        break;
                    }
            }
        }

        /// <summary>
        /// The volley let go: on every machine its sound and its light; on the server its arrows, out of the nock at a full
        /// draw, fanned about the way she aims.
        /// </summary>
        private void LooseVolley(NPC npc, EmpressArrow.Kind kind, int n, float apart, float widest, float speed, float look)
        {
            Vector2 nock = NockFor(npc, actTurn);

            if (!Main.dedServ)
            {
                Sound(EmpressSounds.Loose, nock, 0f, 1f, 2);
                Sound(SoundID.Item5, nock, -0.3f, 0.8f);
                Sound(SoundID.Item163, nock, 0.3f, 0.5f);
                PADParticleHandler.SpawnParticle(new BloomGeneric(nock, Vector2.Zero, Color.White, 160f / 160f, 14));
                SparkShard.Spray(nock, Color.White, 8, 7f, 50f, 14, palette: EmpressArt.Sparks);
            }

            if (!Authority)
                return;

            for (int i = 0; i < n; i++)
            {
                float a = Fanned(actTurn, i, n, apart, widest);
                Arrow(npc, kind, nock, a, speed, kind == EmpressArrow.Kind.Moor ? look + i * 0.13f : i);
            }
        }

        /// <summary>One of her arrows, made by the server: its nock at <paramref name="nock"/>, going this way this fast.</summary>
        private static void Arrow(NPC npc, EmpressArrow.Kind kind, Vector2 nock, float angle, float speed, float own)
        {
            if (!Authority)
                return;

            Vector2 way = angle.ToRotationVector2();
            Projectile.NewProjectile(npc.GetSource_FromAI(), nock + way * EmpressArrow.NockBack, way * speed, ModContent.ProjectileType<EmpressArrow>(),
                Hurt(npc, ArrowNormal, ArrowExpert), 0f, Main.myPlayer, (float)kind, own, npc.whoAmI);
        }

        /// <summary>Where a line out of a point in her arena meets its border, going this way.</summary>
        private Vector2 ToWall(Vector2 from, float angle) => from + angle.ToRotationVector2() * Decagon.Reach(from, angle.ToRotationVector2(), arenaCentre, EmpressArena.Radius, out _);

        private void DrawMoorOver(NPC npc, SpriteBatch spriteBatch)
        {
            if (beat != MoorDraw)
                return;

            float shown = Ease(timer / 8f);
            float time = (float)Main.timeForVisualEffects;
            Vector2 nock = NockFor(npc, actTurn);
            Vector2 wall = ToWall(nock, actTurn);
            Color colour = EmpressArt.Prism(MoorLook);

            DrawWay(spriteBatch, nock, wall, colour, shown, time, actCount);

            // Where it will stick, and how far its burst will reach.
            EmpressArt.DrawStar(spriteBatch, wall, 60f + 14f * (float)Math.Sin(time * 0.2f + actCount), time * 0.05f, EmpressArt.Additive(Color.White, 0.8f * shown));
            DrawRing(spriteBatch, wall, EmpressArrow.BurstRadius, 3f, EmpressArt.Additive(colour, 0.16f * shown));
        }

        /// <summary>
        /// An arrow's way, shown: a thin line of its colour from where it leaves to where it goes, white down its middle,
        /// short dashes of light running out along it.
        /// </summary>
        private static void DrawWay(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color colour, float shown, float time, int i, float width = 1f)
        {
            Vector2 line = to - from;
            float length = line.Length();

            if (length < 4f || shown <= 0.003f)
                return;

            Vector2 u = line / length;
            float flicker = 0.75f + 0.25f * (float)Math.Sin(time * 0.5f + i * 1.3f);

            EmpressArt.DrawBeam(spriteBatch, from, to, 9f * width, EmpressArt.Additive(colour, 0.22f * shown * flicker));
            EmpressArt.DrawBeam(spriteBatch, from, to, 1.6f * width, EmpressArt.Additive(Color.White, 0.6f * shown));

            const float Apart = 140f;

            for (float along = (time * 9f + i * 37f) % Apart; along < length; along += Apart)
            {
                Vector2 a = from + u * along;
                Vector2 b = from + u * Math.Min(length, along + 40f);
                EmpressArt.DrawBeam(spriteBatch, a, b, 4f * width, EmpressArt.Additive(Color.White, 0.55f * shown));
            }
        }

        // ---- the ricochets --------------------------------------------------------------------------------------

        private const int RicoAim = 0;
        private const int RicoDraw = 1;
        private const int RicoLoose = 2;

        private const int RicoAimTicks = 20;
        private const int RicoDrawTicks = 46;
        private const int RicoLooseTicks = 30;

        private const float RicoSpeed = 36f;
        private const float RicoApart = 0.2f;
        private const float RicoWidest = 0.7f;

        private void PlanRicochet(NPC npc, Player target)
        {
            actN = 2 + Main.rand.Next(3);
            actTurn = (target.Center - npc.Center).ToRotation();
        }

        private void RunRicochet(NPC npc, Player who, bool entered)
        {
            int t = timer;
            npc.velocity *= 0.86f;

            switch (beat)
            {
                case RicoAim:
                    Aim((who.Center - npc.Center).ToRotation(), 0f, 0, 0f, -1f);

                    if (entered)
                        Sound(EmpressSounds.WhooshUp, npc.Center, 0.3f, 0.5f, 2);

                    if (Authority && t >= RicoAimTicks)
                    {
                        actTurn = (who.Center - npc.Center).ToRotation();
                        Next(npc, RicoDraw);
                    }

                    break;

                case RicoDraw:
                    Aim(actTurn, Ease(t / 34f), actN, FanOf(actN, RicoApart, RicoWidest), -1f);

                    if (entered)
                    {
                        Sound(EmpressSounds.Draw, npc.Center, 0.25f, 0.9f, 2);
                        Sound(EmpressSounds.Crinkle, npc.Center, 0.5f, 0.4f, 2);
                    }

                    if (Authority && t >= RicoDrawTicks)
                        Next(npc, RicoLoose);

                    break;

                case RicoLoose:
                    Aim(actTurn);

                    if (entered)
                    {
                        Loosed();
                        LooseVolley(npc, EmpressArrow.Kind.Ricochet, actN, RicoApart, RicoWidest, RicoSpeed, -1f);
                        npc.velocity -= actTurn.ToRotationVector2() * 3f;
                    }

                    if (Authority && t >= RicoLooseTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>A ricochet's way: out of a point, off three walls as off a mirror (the way it comes turned about the wall's own line square to it), to the fourth.</summary>
        private void BouncePath(Vector2 from, float angle, List<Vector2> into)
        {
            into.Clear();
            into.Add(from);

            Vector2 at = from;
            Vector2 way = angle.ToRotationVector2();

            for (int k = 0; k <= EmpressArrow.Bounces; k++)
            {
                float d = Decagon.Reach(at, way, arenaCentre, EmpressArena.Radius, out int edge);
                at += way * d;
                into.Add(at);

                Vector2 n = Decagon.Normal(edge);
                way -= 2f * Vector2.Dot(way, n) * n;

                // Off the wall a hair, so the next way out is measured from inside.
                at += way * 0.5f;
            }
        }

        private readonly List<Vector2> path = new();

        private void DrawRicochetOver(NPC npc, SpriteBatch spriteBatch)
        {
            if (beat != RicoDraw)
                return;

            float shown = Ease(timer / 12f);
            float time = (float)Main.timeForVisualEffects;
            Vector2 nock = NockFor(npc, actTurn);

            for (int i = 0; i < actN; i++)
            {
                BouncePath(nock, Fanned(actTurn, i, actN, RicoApart, RicoWidest), path);

                for (int k = 1; k < path.Count; k++)
                {
                    // Each leg fainter than the last; the glass's colours along them.
                    float leg = shown * (1f - 0.18f * (k - 1));
                    DrawWay(spriteBatch, path[k - 1], path[k], EmpressArt.Prism(time / 100f + k * 0.2f + i * 0.1f), leg, time, i * 4 + k, 0.8f);

                    if (k < path.Count - 1)
                        EmpressArt.DrawStar(spriteBatch, path[k], 44f, time * 0.08f + k, EmpressArt.Additive(Color.White, 0.75f * leg));
                    else
                        EmpressArt.DrawStar(spriteBatch, path[k], 64f, time * 0.03f, EmpressArt.Additive(EmpressArt.Pink, 0.8f * leg));
                }
            }
        }

        // ---- the lightspeed shot ------------------------------------------------------------------------------

        private const int LightAim = 0;
        private const int LightDraw = 1;
        private const int LightLock = 2;
        private const int LightFire = 3;
        private const int LightEnd = 4;

        private const int LightAimTicks = 16;
        private const int LightFireTicks = 18;
        private const int LightEndTicks = 24;

        /// <summary>Shots, and how long each is drawn and locked (the second quicker).</summary>
        private const int LightShots = 2;
        private static int LightDrawTicks(int shot) => shot == 0 ? 70 : 46;
        private static int LightLockTicks(int shot) => shot == 0 ? 30 : 22;

        /// <summary>The shot's line: how wide it hurts, and how long.</summary>
        private const float LightWidth = 44f;
        private const int LightHurts = 8;

        private const int LightNormal = 90;
        private const int LightExpert = 60;

        /// <summary>Shots fired (sent): which one this is.</summary>
        private int lightHit;

        /// <summary>Each machine's own: the aim as it follows the player through the draw.</summary>
        private float lightFollow;

        private void PlanLightspeed(NPC npc, Player target)
        {
            lightHit = 0;
            actTurn = (target.Center - npc.Center).ToRotation();
            lightFollow = actTurn;
        }

        private void RunLightspeed(NPC npc, Player who, bool entered)
        {
            int t = timer;
            npc.velocity *= 0.86f;

            float toward = (who.Center - npc.Center).ToRotation();

            switch (beat)
            {
                case LightAim:
                    lightFollow = LerpAngle(lightFollow, toward, 0.2f);
                    Aim(lightFollow, 0f, 0, 0f, 0.55f);

                    if (Authority && t >= LightAimTicks)
                        Next(npc, LightDraw);

                    break;

                case LightDraw:
                    {
                        int length = LightDrawTicks(lightHit);
                        lightFollow = LerpAngle(lightFollow, toward, 0.12f);
                        Aim(lightFollow, Ease(t / (length * 0.8f)), 1, 0f, 0.55f);

                        if (entered)
                        {
                            Sound(EmpressSounds.Charge, npc.Center, lightHit * 0.25f, 0.9f, 2);
                            Sound(EmpressSounds.Draw, npc.Center, -0.2f, 0.7f, 2);
                        }

                        if (Authority && t >= length)
                        {
                            // It will go here: fixed, and sent.
                            actTurn = toward;
                            Next(npc, LightLock);
                        }

                        break;
                    }

                case LightLock:
                    Aim(actTurn, 1f, 1, 0f, 0.55f);
                    lightFollow = actTurn;

                    if (entered || Crossed(10))
                        Sound(EmpressSounds.Lock, npc.Center, entered ? 0.2f : 0.45f, 0.9f, 3);

                    if (Authority && t >= LightLockTicks(lightHit))
                        Next(npc, LightFire);

                    break;

                case LightFire:
                    Aim(actTurn);

                    if (entered)
                        LightspeedFires(npc);

                    if (Authority && t >= LightFireTicks)
                    {
                        lightHit++;

                        if (lightHit < LightShots)
                            Next(npc, LightDraw);
                        else
                            Next(npc, LightEnd);
                    }

                    break;

                case LightEnd:
                    if (Authority && t >= LightEndTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The shot: the server's line of hurt for its first ticks; on every machine the sound, the light and the push.</summary>
        private void LightspeedFires(NPC npc)
        {
            Vector2 nock = NockFor(npc, actTurn);
            Vector2 end = ToWall(nock, actTurn);

            Loosed();
            npc.velocity -= actTurn.ToRotationVector2() * 9f;

            Hitbox(npc, EmpressHitbox.Shape.Lightspeed, nock, 0f, Hurt(npc, LightNormal, LightExpert), LightHurts + 2);

            if (Main.dedServ)
                return;

            float near = Math.Max(Near(nock), Near(end));

            Sound(EmpressSounds.Lightspeed, nock, 0f, 1f, 2);
            Sound(SoundID.Item122, nock, 0.2f, 0.7f);
            Sound(SoundID.Item14, end, -0.3f, 0.7f);

            Cinematic.Flash(Color.White, 0.25f * near);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(10f * near, 20);

            EmpressWarp.Ripple(nock, 26f, 70f, 16f, 30, end);
            PADParticleHandler.SpawnParticle(new BloomGeneric(nock, Vector2.Zero, Color.White, 300f / 160f, 18));
            PADParticleHandler.SpawnParticle(new BloomGeneric(end, Vector2.Zero, EmpressArt.Sky, 420f / 160f, 26));
            PADParticleHandler.SpawnParticle(new CrownGlint(end, 160f, 26, Color.White));
            SparkShard.Spray(end, Color.White, 20, 12f, 120f, 24, palette: EmpressArt.Sparks);

            for (int i = 0; i < 40; i++)
            {
                Vector2 at = Vector2.Lerp(nock, end, Main.rand.NextFloat());
                Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Main.rand.NextVector2Circular(3f, 3f), 0, Color.Lerp(Color.White, EmpressArt.Prism(Main.rand.NextFloat()), 0.4f), 1.4f);
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }
        }

        /// <summary>The shot's hitbox asks: is this body on its line, while it burns?</summary>
        internal bool LightspeedHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Lightspeed || beat != LightFire || timer > LightHurts)
                return false;

            NPC npc = NpcOfThis();

            if (npc == null)
                return false;

            Vector2 nock = NockFor(npc, actTurn);
            return EmpressHitbox.OnLine(box, nock, ToWall(nock, actTurn), LightWidth);
        }

        private void DrawLightspeedOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;

            if (beat == LightDraw || beat == LightLock)
            {
                bool locked = beat == LightLock;
                float aim = locked ? actTurn : lightFollow;
                Vector2 nock = NockFor(npc, aim);
                Vector2 end = ToWall(nock, aim);
                float p = locked ? 1f : Ease(timer / (float)LightDrawTicks(lightHit));
                float pulse = locked ? 0.6f + 0.4f * (float)Math.Sin(time * 0.9f) : 0.5f + 0.5f * (float)Math.Sin(time * 0.35f);

                EmpressArt.DrawBeam(spriteBatch, nock, end, locked ? 16f : 8f, EmpressArt.Additive(locked ? EmpressArt.Pink : EmpressArt.Sky, (0.12f + 0.25f * p) * pulse));
                EmpressArt.DrawBeam(spriteBatch, nock, end, locked ? 2.4f : 1.2f, EmpressArt.Additive(Color.White, (0.3f + 0.6f * p) * (locked ? pulse : 1f)));
                EmpressArt.DrawStar(spriteBatch, end, 50f + 30f * p * pulse, time * 0.07f, EmpressArt.Additive(Color.White, 0.6f * p));

                // Light gathering at the arrow's head.
                Vector2 head = nock + aim.ToRotationVector2() * (EmpressBow.ArrowStar.X - EmpressBow.ArrowNock.X);
                EmpressArt.DrawBloom(spriteBatch, head, 40f + 120f * p, EmpressArt.Additive(Color.White, 0.3f + 0.5f * p));
                EmpressArt.DrawStar(spriteBatch, head, 50f + 140f * p * pulse, time * 0.12f, EmpressArt.Additive(EmpressArt.Prism(time / 60f), 0.6f * p));
            }

            if (beat == LightFire)
            {
                Vector2 nock = NockFor(npc, actTurn);
                Vector2 end = ToWall(nock, actTurn);
                float p = timer / (float)LightFireTicks;
                float fade = 1f - p;
                float width = MathHelper.Lerp(LightWidth * 2.2f, LightWidth * 0.4f, Ease(p));

                EmpressArt.DrawBeam(spriteBatch, nock, end, width * 3f, EmpressArt.Additive(EmpressArt.Sky, 0.3f * fade));
                EmpressArt.DrawBeam(spriteBatch, nock, end, width, EmpressArt.Additive(Color.White, 0.95f * fade));
                DrawHelix(spriteBatch, nock, end, width + 14f, time, fade);

                // Where it went still burning, a moment after.
                EmpressArt.DrawBeam(spriteBatch, nock, end, 6f, EmpressArt.Additive(EmpressArt.Pink, 0.6f * fade * fade));
                EmpressWarp.Haze(nock, end, 80f, 4f * fade, 120f, 0.3f);
            }
        }

        // ---- the refraction volley ----------------------------------------------------------------------------

        private const int RefrCast = 0;
        private const int RefrDraw = 1;
        private const int RefrLoose = 2;

        private const int RefrCastTicks = 30;
        private const int RefrDrawTicks = 36;
        private const int RefrLooseTicks = 56;

        /// <summary>The arrow's flight into the prism; when the prism breaks.</summary>
        private const int RefrStrike = 6;
        private const int RefrBreak = 40;

        /// <summary>The seven: how fast they fan out of it.</summary>
        private const float RefrFan = 13f;

        private void PlanRefraction(NPC npc, Player target)
        {
            // The prism between her and the player, a little nearer them, inside the arena.
            Vector2 at = Vector2.Lerp(npc.Center, target.Center, 0.55f);
            Vector2 none = Vector2.Zero;
            actAt = Decagon.PushInside(at, arenaCentre, EmpressArena.Radius, 200f, ref none);
            actTurn = (actAt - npc.Center).ToRotation();
        }

        /// <summary>The prism: cast from her other hand over CAST, standing where it was cast to after.</summary>
        private Vector2 RefrPrism(NPC npc)
        {
            if (beat != RefrCast)
                return actAt;

            Vector2 from = Palm(npc, !BowLeft(npc), actTurn);
            float p = Ease(timer / (float)(RefrCastTicks - 6));
            return Vector2.Lerp(from, actAt, p) + new Vector2(0f, -120f * (float)Math.Sin(p * MathHelper.Pi));
        }

        private void RunRefraction(NPC npc, Player who, bool entered)
        {
            int t = timer;
            npc.velocity *= 0.86f;
            float toPrism = (actAt - npc.Center).ToRotation();

            switch (beat)
            {
                case RefrCast:
                    FreeArmAt(LerpAngle(toPrism + (BowLeft(npc) ? 1.6f : -1.6f), toPrism, Ease(t / 14f)));

                    if (entered)
                    {
                        Sound(EmpressSounds.WhooshDown, npc.Center, 0.3f, 0.6f, 2);
                        Sound(EmpressSounds.Tingle, npc.Center, 0.2f, 0.6f);
                    }

                    if (Crossed(RefrCastTicks - 6))
                        Sound(SoundID.Item27, actAt, 0.5f, 0.6f);

                    if (Authority && t >= RefrCastTicks)
                        Next(npc, RefrDraw);

                    break;

                case RefrDraw:
                    Aim(toPrism, Ease(t / 28f), 1, 0f, 0.15f);

                    if (entered)
                        Sound(EmpressSounds.Draw, npc.Center, 0.1f, 0.85f, 2);

                    if (Authority && t >= RefrDrawTicks)
                    {
                        actTurn = toPrism;
                        Next(npc, RefrLoose);
                    }

                    break;

                case RefrLoose:
                    Aim(actTurn);

                    if (entered)
                    {
                        Loosed();

                        if (!Main.dedServ)
                        {
                            Sound(EmpressSounds.Loose, npc.Center, 0.1f, 0.9f, 2);
                            Sound(SoundID.Item5, npc.Center, -0.1f, 0.7f);
                        }
                    }

                    if (Crossed(RefrStrike))
                        Refracts(npc);

                    if (Crossed(RefrBreak))
                        PrismBreaks(actAt);

                    if (Authority && t >= RefrLooseTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The arrow through the prism: seven out of it, every way, her seven colours (the server's); the plink and the fan of notes.</summary>
        private void Refracts(NPC npc)
        {
            if (Authority)
            {
                for (int k = 0; k < 7; k++)
                {
                    float a = actTurn + k * MathHelper.TwoPi / 7f;
                    Vector2 way = a.ToRotationVector2();

                    Projectile.NewProjectile(npc.GetSource_FromAI(), actAt + way * 20f, way * RefrFan, ModContent.ProjectileType<EmpressArrow>(),
                        Hurt(npc, ArrowNormal, ArrowExpert), 0f, Main.myPlayer, (float)EmpressArrow.Kind.Refract, k, npc.whoAmI);
                }
            }

            if (Main.dedServ)
                return;

            Sound(EmpressSounds.Refract, actAt, 0f, 1f, 2);
            Sound(SoundID.Item29, actAt, 0.4f, 0.6f);
            EmpressWarp.Ripple(actAt, 16f, 50f, 10f, 26);
            PADParticleHandler.SpawnParticle(new BloomGeneric(actAt, Vector2.Zero, Color.White, 300f / 160f, 18));
            PADParticleHandler.SpawnParticle(new CrownGlint(actAt, 150f, 24, Color.White));
        }

        /// <summary>The prism, its work done: glass every way.</summary>
        private static void PrismBreaks(Vector2 at)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Shatter, at, 0.3f, 0.6f);
            Sound(EmpressSounds.Shards, at, 0.3f, 0.6f, 2);
            SparkShard.Spray(at, Color.White, 18, 9f, 70f, 22, palette: EmpressArt.Sparks);
        }

        private void DrawRefractionOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Vector2 prism = RefrPrism(npc);
            float lit = beat == RefrDraw ? Ease(timer / (float)RefrDrawTicks) * 0.5f : beat == RefrLoose ? 1f : 0f;

            // The arrow's way to it, shown as she draws.
            if (beat == RefrDraw)
                DrawWay(spriteBatch, NockFor(npc, (actAt - npc.Center).ToRotation()), prism, EmpressArt.Sky, Ease(timer / 12f), time, 0);

            // The arrow in flight to it.
            if (beat == RefrLoose && timer < RefrStrike)
            {
                Vector2 nock = NockFor(npc, actTurn);
                Vector2 tipAt = Vector2.Lerp(nock, prism, timer / (float)RefrStrike);
                EmpressBow.DrawArrow(spriteBatch, tipAt - actTurn.ToRotationVector2() * EmpressArrow.NockBack, actTurn, 1f, 1f, 0.15f, 1f);
            }

            if (beat == RefrLoose && timer >= RefrBreak)
            {
                DrawPrismPieces(spriteBatch, prism, (timer - RefrBreak) / (float)(RefrLooseTicks - RefrBreak));
                return;
            }

            DrawPrism(spriteBatch, prism, (float)Math.Sin(time * 0.03f) * 0.2f + (beat == RefrCast ? timer * 0.3f : 0f), lit);
            EmpressWarp.Lens(prism, PrismHeight * 0.9f, 0.22f + 0.15f * lit, 0.1f);

            // Struck: the seven out of it.
            if (beat == RefrLoose && timer >= RefrStrike && timer < RefrStrike + 16)
            {
                float p = (timer - RefrStrike) / 16f;

                for (int k = 0; k < 7; k++)
                {
                    float a = actTurn + k * MathHelper.TwoPi / 7f;
                    EmpressArt.DrawRay(spriteBatch, prism, a, 260f * Ease(p * 2f), 40f, EmpressArt.Additive(Spectrum[k], 0.7f * (1f - p)));
                }
            }
        }

        /// <summary>A prism in pieces: glass edged in light, turning as it flies, going.</summary>
        private static void DrawPrismPieces(SpriteBatch spriteBatch, Vector2 at, float p)
        {
            float fade = 1f - p;
            UnifiedRandomish dice = new UnifiedRandomish((int)(at.X * 13f + at.Y * 7f));

            for (int i = 0; i < 12; i++)
            {
                float a = dice.Next() * MathHelper.TwoPi;
                float flight = (60f + 180f * dice.Next()) * (float)Math.Pow(p, 0.6);
                float size = 8f + 12f * dice.Next();
                float spin = (dice.Next() - 0.5f) * 12f * p + dice.Next() * MathHelper.TwoPi;
                Vector2 middle = at + a.ToRotationVector2() * flight + new Vector2(0f, 50f * p * p);

                for (int c = 0; c < 3; c++)
                {
                    Vector2 from = middle + (spin + c * MathHelper.TwoPi / 3f).ToRotationVector2() * size;
                    Vector2 to = middle + (spin + (c + 1) * MathHelper.TwoPi / 3f).ToRotationVector2() * size;
                    EmpressArt.DrawBeam(spriteBatch, from, to, 4f, EmpressArt.Additive(Spectrum[i % Spectrum.Length], 0.5f * fade));
                    EmpressArt.DrawBeam(spriteBatch, from, to, 1.4f, EmpressArt.Additive(Color.White, 0.9f * fade));
                }
            }

            EmpressArt.DrawBloom(spriteBatch, at, 160f * (1f + p), EmpressArt.Additive(Color.White, 0.5f * fade * fade));
        }

        // ---- the rain of light ----------------------------------------------------------------------------------

        private const int RainAim = 0;
        private const int RainDraw = 1;
        private const int RainLoose = 2;
        private const int RainWave = 3;
        private const int RainEnd = 6;

        private const int RainAimTicks = 22;
        private const int RainDrawTicks = 30;
        private const int RainLooseTicks = 16;
        private const int RainEndTicks = 12;

        /// <summary>Each wave: shown, falling, there; and its columns' width and how many.</summary>
        private const int RainShow = 36;
        private const int RainFall = 6;
        private const int RainStay = 18;
        private const int RainWaveTicks = RainShow + RainFall + RainStay;
        private const float RainWidth = 50f;
        private const int RainColumns = 26;
        private const float RainApartLeast = 96f;
        private const float RainApartMost = 176f;

        private const int RainNormal = 55;
        private const int RainExpert = 36;

        /// <summary>
        /// The starfall (actN 1; the upright columns 0): its waves' beats; each wave's length (the indicators begin at once,
        /// the columns fall through one second after the first is shown, a second quiet in all); its columns, how slanted
        /// (from her side, actSide), how far about the wave's middle (actAt) they may stand.
        /// </summary>
        private const int StarWave = 7;
        private const int StarWaveTicks = RainShow + 60 + 24;
        private const int StarColumns = 9;
        private const float StarSlant = 0.35f;
        private const float StarReach = 300f;

        /// <summary>A starfall column's pulse: how long it is seen, and how long of that it hurts.</summary>
        private const int StarPulse = 22;
        private const int StarHurts = 8;

        private bool Starfall => actN == 1;

        /// <summary>Is this beat one of the starfall's waves, and which.</summary>
        private static bool StarBeat(int step, out int wave)
        {
            wave = step - StarWave;
            return step >= StarWave && step < StarWave + 3;
        }

        /// <summary>Down, slanted this far (radians from straight down).</summary>
        private static Vector2 Slanted(float slant) => new Vector2((float)Math.Sin(slant), (float)Math.Cos(slant));

        /// <summary>
        /// A starfall column of a wave: the tick of the wave it falls at (uneven, one after another through one second;
        /// shown RainShow before), and its line, border to border, through a point about the wave's middle. The same on
        /// every machine (the act's seed, the wave's middle as sent).
        /// </summary>
        private void StarColumn(int wave, int k, out int falls, out Vector2 top, out Vector2 bottom)
        {
            UnifiedRandomish dice = new UnifiedRandomish(actSeed * 31 + wave * 7919 + k * 104729);
            float when = (k + 0.15f + dice.Next() * 0.7f) / StarColumns;
            falls = RainShow + (int)(when * 57f);

            // The first of each wave at the player; the rest about them, either side in turn; each slanted its own way,
            // as a storm of arrows from all over the sky crossing about one place.
            float across = k == 0 ? 0f : (k % 2 == 0 ? 1f : -1f) * (0.15f + 0.85f * dice.Next()) * StarReach;
            Vector2 through = actAt + new Vector2(across, (dice.Next() - 0.5f) * 160f);
            Vector2 way = Slanted((dice.Next() * 2f - 1f) * StarSlant);

            if (!Decagon.Inside(through, arenaCentre, EmpressArena.Radius, 40f))
                through = arenaCentre + (through - arenaCentre) * 0.9f;

            top = through - way * Decagon.Reach(through, -way, arenaCentre, EmpressArena.Radius, out _);
            bottom = through + way * Decagon.Reach(through, way, arenaCentre, EmpressArena.Radius, out _);
        }

        private void PlanRain(NPC npc, Player target)
        {
            actN = Main.rand.Next(2);
        }

        private void RunRain(NPC npc, Player who, bool entered)
        {
            int t = timer;
            npc.velocity *= 0.86f;

            switch (beat)
            {
                case RainAim:
                    Aim(-MathHelper.PiOver2 + (BowLeft(npc) ? -0.08f : 0.08f), 0f, 0, 0f, 0.35f);

                    if (entered)
                        Sound(EmpressSounds.WhooshUp, npc.Center, -0.1f, 0.5f, 2);

                    if (Authority && t >= RainAimTicks)
                        Next(npc, RainDraw);

                    break;

                case RainDraw:
                    Aim(-MathHelper.PiOver2 + (BowLeft(npc) ? -0.08f : 0.08f), Ease(t / 24f), Starfall ? 9 : 5, Starfall ? 0.8f : 0.5f, 0.35f);

                    if (entered)
                        Sound(EmpressSounds.Draw, npc.Center, -0.1f, 0.85f, 2);

                    if (Authority && t >= RainDrawTicks)
                    {
                        actTurn = -MathHelper.PiOver2 + (BowLeft(npc) ? -0.08f : 0.08f);
                        Next(npc, RainLoose);
                    }

                    break;

                case RainLoose:
                    Aim(actTurn);

                    if (entered)
                    {
                        Loosed();

                        if (!Main.dedServ)
                        {
                            Sound(EmpressSounds.Loose, npc.Center, -0.1f, 1f, 2);
                            Sound(SoundID.Item5, npc.Center, -0.4f, 0.8f);
                        }
                    }

                    if (Authority && t >= RainLooseTicks)
                    {
                        if (Starfall)
                        {
                            StarAim(npc, who);
                            Next(npc, StarWave);
                        }
                        else
                        {
                            actAt = who.Center;
                            Next(npc, RainWave);
                        }
                    }

                    break;

                case RainWave:
                case RainWave + 1:
                case RainWave + 2:
                    {
                        int wave = beat - RainWave;

                        if (entered && !Main.dedServ)
                            Sound(EmpressSounds.Tingle, npc.Center, 0.4f, 0.5f);

                        if (Crossed(RainShow))
                            RainFalls(npc, wave);

                        if (Authority && t >= RainWaveTicks)
                        {
                            actAt = who.Center;
                            Next(npc, wave < 2 ? beat + 1 : RainEnd);
                        }

                        break;
                    }

                case RainEnd:
                    if (Authority && t >= RainEndTicks)
                        EndAct(npc);

                    break;

                default:
                    if (StarBeat(beat, out int starWave))
                        RunStarfall(npc, who, entered, starWave);

                    break;
            }
        }

        /// <summary>The server's: where the next wave falls about (the player now), and the side it slants from (hers).</summary>
        private void StarAim(NPC npc, Player who)
        {
            actAt = who.Center;
            actSide = who.Center.X >= npc.Center.X ? 1 : -1;
        }

        /// <summary>A wave of the starfall: its columns falling at their times (heard, and their hurt asked of RainHits).</summary>
        private void RunStarfall(NPC npc, Player who, bool entered, int wave)
        {
            int t = timer;
            Aim(-MathHelper.PiOver2 + (BowLeft(npc) ? -0.08f : 0.08f));

            if (entered)
            {
                if (wave == 0)
                    Hitbox(npc, EmpressHitbox.Shape.Rain, arenaCentre, 0f, Hurt(npc, RainNormal, RainExpert), StarWaveTicks * 3 + 30);

                if (!Main.dedServ)
                    Sound(EmpressSounds.Tingle, actAt, 0.3f + 0.1f * wave, 0.5f);
            }

            if (!Main.dedServ)
            {
                for (int k = 0; k < StarColumns; k++)
                {
                    StarColumn(wave, k, out int falls, out Vector2 top, out Vector2 bottom);

                    if (Crossed(falls - RainShow))
                        Sound(EmpressSounds.Lock, Vector2.Lerp(top, bottom, 0.5f), 0.05f * k, 0.35f, 4);

                    if (Crossed(falls))
                    {
                        Vector2 near = ClosestOn(top, bottom, Main.LocalPlayer.Center);
                        Sound(EmpressSounds.Zap, near, 0.1f * (k % 4) - 0.1f, 0.7f, 4);
                        Sound(EmpressSounds.Beam, near, 0.2f, 0.35f, 4);
                    }
                }
            }

            if (Authority && t >= StarWaveTicks)
            {
                if (wave < 2)
                {
                    StarAim(npc, who);
                    Next(npc, beat + 1);
                }
                else
                {
                    Next(npc, RainEnd);
                }
            }
        }

        /// <summary>The point on a line nearest another.</summary>
        private static Vector2 ClosestOn(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / Math.Max(1f, ab.LengthSquared()), 0f, 1f);
            return a + ab * t;
        }

        /// <summary>A starfall column seen: its line shown, then a pulse of light down the whole of it at once, fading.</summary>
        private void DrawStarColumn(SpriteBatch spriteBatch, int k, int falls, Vector2 top, Vector2 bottom, float time)
        {
            int since = timer - (falls - RainShow);

            if (since < 0 || since > RainShow + StarPulse)
                return;

            Color colour = Main.hslToRgb((k / (float)StarColumns + time / 600f) % 1f, 1f, 0.7f);

            if (since < RainShow)
            {
                float p = since / (float)RainShow;
                float flicker = 0.6f + 0.4f * (float)Math.Sin(time * 0.7f + k * 2f);
                EmpressArt.DrawBeam(spriteBatch, top, bottom, RainWidth * 0.6f, EmpressArt.Additive(colour, 0.08f + 0.12f * p));
                EmpressArt.DrawBeam(spriteBatch, top, bottom, 2f, EmpressArt.Additive(Color.White, (0.25f + 0.5f * p) * flicker));
                return;
            }

            // The pulse: all of it at once, swelling a moment and thinning as it fades.
            float q = (since - RainShow) / (float)StarPulse;
            float fade = 1f - Ease(q);
            float swell = 1f + 0.5f * (1f - Ease(q * 3f));
            float width = RainWidth * swell * (1f - 0.5f * q);

            EmpressArt.DrawBeam(spriteBatch, top, bottom, width * 2.8f, EmpressArt.Additive(colour, 0.3f * fade));
            EmpressArt.DrawBeam(spriteBatch, top, bottom, width, EmpressArt.Additive(colour, 0.8f * fade));
            EmpressArt.DrawBeam(spriteBatch, top, bottom, width * 0.35f, EmpressArt.Additive(Color.White, 0.95f * fade));
            EmpressWarp.Haze(top, bottom, 70f, 2.6f * fade, 100f, k);
        }

        /// <summary>
        /// Where a wave's columns stand: all across the screen about where the player was as the wave began, each its own
        /// distance from the last (by request: "this should not only be 7 but all over the screen, without distance of 10
        /// tiles exactly"), 6 to 11 tiles; each wave's row laid out its own way. The same on every machine (the act's seed,
        /// the wave's middle as sent). Those outside the arena are not shown.
        /// </summary>
        private float RainX(int wave, int k)
        {
            if (rainXFor != actSeed + wave * 977 || rainXAt != actAt.X)
            {
                rainXFor = actSeed + wave * 977;
                rainXAt = actAt.X;
                UnifiedRandomish dice = new UnifiedRandomish(rainXFor);
                float x = 0f;

                for (int i = 0; i < RainColumns; i++)
                {
                    rainXs[i] = x;
                    x += MathHelper.Lerp(RainApartLeast, RainApartMost, dice.Next());
                }

                // Centred on the player, a little off by chance so no wave stands quite where the last did.
                float shift = x * 0.5f + (dice.Next() - 0.5f) * RainApartLeast;

                for (int i = 0; i < RainColumns; i++)
                    rainXs[i] += actAt.X - shift;
            }

            return rainXs[Math.Clamp(k, 0, RainColumns - 1)];
        }

        private readonly float[] rainXs = new float[RainColumns];
        private int rainXFor = int.MinValue;
        private float rainXAt = float.NaN;

        /// <summary>A wave's columns fall: the sound of them, light where they land (the server's hurt is asked of RainHits).</summary>
        private void RainFalls(NPC npc, int wave)
        {
            if (wave == 0)
                Hitbox(npc, EmpressHitbox.Shape.Rain, arenaCentre, 0f, Hurt(npc, RainNormal, RainExpert), RainWaveTicks * 3 + 30);

            if (Main.dedServ)
                return;

            for (int k = 0; k < RainColumns; k += 2)
            {
                float x = RainX(wave, k);

                if (Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out _, out float bottom))
                    Sound(EmpressSounds.Fall, new Vector2(x, Math.Min(bottom, Main.LocalPlayer.Center.Y + 200f)), 0.1f * k - 0.2f, 0.7f, 4);
            }
        }

        /// <summary>The columns' hitbox asks: is this body under one as it falls, or in one while it stands?</summary>
        internal bool RainHits(Rectangle box)
        {
            if (scene == Scene.Act && act == Act.Rain && StarBeat(beat, out int starWave))
            {
                for (int k = 0; k < StarColumns; k++)
                {
                    StarColumn(starWave, k, out int falls, out Vector2 top, out Vector2 bottom);

                    if (timer < falls || timer > falls + StarHurts)
                        continue;

                    if (EmpressHitbox.OnLine(box, top, bottom, RainWidth))
                        return true;
                }

                return false;
            }

            if (scene != Scene.Act || act != Act.Rain || beat < RainWave || beat > RainWave + 2 || timer < RainShow || timer > RainShow + RainFall + 10)
                return false;

            int wave = beat - RainWave;

            for (int k = 0; k < RainColumns; k++)
            {
                float x = RainX(wave, k);

                if (box.Right < x - RainWidth * 0.5f || box.Left > x + RainWidth * 0.5f)
                    continue;

                if (!Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                    continue;

                float front = MathHelper.Lerp(top, bottom, MathHelper.Clamp((timer - RainShow) / (float)RainFall, 0f, 1f));

                if (box.Top < front)
                    return true;
            }

            return false;
        }

        private void DrawRainOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;

            // The five arrows going up out of the top of the arena.
            if (beat == RainLoose)
            {
                Vector2 nock = NockFor(npc, actTurn);
                float flown = timer * 60f;

                int up = Starfall ? 9 : 5;

                for (int i = 0; i < up; i++)
                {
                    float a = Fanned(actTurn, i, up, 0.12f, Starfall ? 0.8f : 0.5f);
                    EmpressBow.DrawArrow(spriteBatch, nock + a.ToRotationVector2() * flown, a, 1f, 1f - timer / (float)RainLooseTicks * 0.5f, 0.35f + i * 0.13f, 1f);
                }
            }

            if (StarBeat(beat, out int starWave))
            {
                for (int k = 0; k < StarColumns; k++)
                {
                    StarColumn(starWave, k, out int falls, out Vector2 top, out Vector2 bottom);
                    DrawStarColumn(spriteBatch, k, falls, top, bottom, time);
                }

                return;
            }

            if (beat < RainWave || beat > RainWave + 2)
                return;

            int wave = beat - RainWave;
            View(out float viewLeft, out float viewRight, out float viewTop, out float viewBottom);

            for (int k = 0; k < RainColumns; k++)
            {
                float x = RainX(wave, k);

                if (x < viewLeft - 200f || x > viewRight + 200f || !Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                    continue;

                Color colour = Main.hslToRgb((k / (float)RainColumns + wave * 0.2f + time / 600f) % 1f, 1f, 0.7f);
                float seenTop = Math.Max(top, viewTop - 100f);
                float seenBottom = Math.Min(bottom, viewBottom + 100f);

                if (timer < RainShow)
                {
                    // Shown: a thin line the arena's whole height, flickering, brighter as it comes; light gathering at its top.
                    float p = timer / (float)RainShow;
                    float flicker = 0.6f + 0.4f * (float)Math.Sin(time * 0.7f + k * 2f);

                    EmpressArt.DrawBeam(spriteBatch, new Vector2(x, seenTop), new Vector2(x, seenBottom), RainWidth * 0.6f, EmpressArt.Additive(colour, 0.08f + 0.12f * p));
                    EmpressArt.DrawBeam(spriteBatch, new Vector2(x, seenTop), new Vector2(x, seenBottom), 2f, EmpressArt.Additive(Color.White, (0.25f + 0.5f * p) * flicker));
                    EmpressArt.DrawStar(spriteBatch, new Vector2(x, top + 20f), 40f + 80f * p, time * 0.05f + k, EmpressArt.Additive(Color.White, 0.6f * p));
                    continue;
                }

                // Falling, then there, then going.
                float fall = MathHelper.Clamp((timer - RainShow) / (float)RainFall, 0f, 1f);
                float stay = MathHelper.Clamp((timer - RainShow - RainFall) / (float)RainStay, 0f, 1f);
                float fade = 1f - Ease(stay);
                float front = MathHelper.Lerp(top, bottom, fall);
                Vector2 from = new Vector2(x, Math.Max(top, seenTop));
                Vector2 to = new Vector2(x, Math.Min(front, seenBottom));

                if (to.Y > from.Y)
                {
                    EmpressArt.DrawBeam(spriteBatch, from, to, RainWidth * 2.6f, EmpressArt.Additive(colour, 0.28f * fade));
                    EmpressArt.DrawBeam(spriteBatch, from, to, RainWidth, EmpressArt.Additive(colour, 0.75f * fade));
                    EmpressArt.DrawBeam(spriteBatch, from, to, RainWidth * 0.35f, EmpressArt.Additive(Color.White, 0.9f * fade));
                    EmpressWarp.Haze(from, to, 70f, 2.4f * fade, 100f, k);
                }

                if (fall < 1f)
                    EmpressArt.DrawBloom(spriteBatch, new Vector2(x, front), 140f, EmpressArt.Additive(Color.White, 0.9f));
                else if (stay < 0.4f)
                    EmpressArt.DrawBloom(spriteBatch, new Vector2(x, bottom), 220f * (1f + stay), EmpressArt.Additive(colour, 0.6f * (1f - stay / 0.4f)));
            }
        }
    }
}
