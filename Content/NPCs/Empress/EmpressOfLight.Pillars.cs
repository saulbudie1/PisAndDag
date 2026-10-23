using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the laser pillars (Legends act) ---------------------------------------------------------------------------
    //
    // By request: "She raise one hand, and drag it down and laser pillars shot down with intervals of 0.2 seconds and
    // distance of 6 tiles runs from either left to right or vice versa depending on which side the player is to the
    // empress", seen in the clip asked for: each pillar first a thin line, then a beam down the whole height, the
    // row of them running across; "make it colorful tho for night". And, by request, "the surrounding distortion effect
    // like in the video": in the clip a light falls down each line, and everything beside it is kinked sideways as it
    // passes; the lines and the air about them shimmer (EmpressWarp).
    //
    //     RAISE   34   the hand toward the player goes up, filling with light, the world about it drawn in, turning
    //     DRAG    14   and is dragged down, the light thrown out of it
    //     SWEEP   ...  from the edge of the screen on her side across toward the player's side, one pillar every 12
    //                  ticks (0.2 s), 6 tiles (96 px) apart: each a thin line of its colour for half a second, the air
    //                  along it shimmering, and down it a falling light from the top of the screen to its foot, the
    //                  world beside it kinked as it passes (it reaches the foot as the beam comes); then the beam, shot
    //                  down the arena's whole height in 5 ticks (the world kinked at its head), held 15, gone over 14:
    //                  its colour, its neighbours' colours thin at its edges, light running down it, the air either side
    //                  rippling, a wave of it thrown out either way as it fires; each the next colour of the rainbow,
    //                  full colours for the night; on to the arena's edge. Heard: each falling light, each beam's glass
    //                  laser and the game's high and low by turns, a shockwave under every third, a wavering in the air.
    //     REST    20
    //
    // A beam hurts while it is whole (from its 3rd tick to its 20th): 50 in a normal world, 32 in an expert one,
    // doubled by the game as any hostile shot's is.

    public partial class EmpressOfLight
    {
        private const int PillarRaise = 0;
        private const int PillarDrag = 1;
        private const int PillarSweep = 2;
        private const int PillarRest = 3;

        private const int RaiseTicks = 34;
        private const int DragTicks = 14;
        private const int PillarRestTicks = 20;

        /// <summary>One pillar every this many ticks, this far apart; the line before each, and the beam.</summary>
        private const int PillarEvery = 12;
        private const float PillarApart = 96f;
        private const int PillarWarn = 30;
        private const int PillarShoot = 5;
        private const int PillarHold = 15;
        private const int PillarGone = 14;
        private const int PillarMost = 64;

        /// <summary>Half a beam's width (what hurts).</summary>
        private const float PillarHalf = 22f;

        private const int PillarNormal = 50;
        private const int PillarExpert = 32;

        private static int PillarLife => PillarWarn + PillarShoot + PillarHold + PillarGone;

        /// <summary>
        /// The row: from the edge of the screen on her side (as the player sees it: by request, "start from all the way
        /// from the edge of the screen not from empress's center") across toward the player's side, to the arena's edge
        /// ("it should end at the arena's edge"). A server, which has no screen, takes a wide one's.
        /// </summary>
        private void PlanPillars(NPC npc, Player target)
        {
            float half = Main.dedServ ? 1000f : Main.screenWidth * 0.5f / Math.Max(Main.GameViewMatrix.Zoom.X, 0.5f);
            float start = target.Center.X - actSide * (half - 24f);

            // Not outside the arena: the row starts at its edge if the screen's edge is past it.
            start = actSide > 0 ? Math.Max(start, ArenaLeft + 48f) : Math.Min(start, ArenaRight - 48f);

            int count = 0;

            for (int k = 0; k < PillarMost; k++)
            {
                if (!Decagon.Upright(start + actSide * PillarApart * k, arenaCentre, EmpressArena.Radius, out _, out _))
                    break;

                count++;
            }

            actAt = new Vector2(start, npc.Center.Y);
            actN = Math.Max(count, 1);
        }

        private float PillarX(int k) => actAt.X + actSide * PillarApart * k;

        /// <summary>The arena's top and bottom where pillar k stands.</summary>
        private bool PillarSpan(int k, out float top, out float bottom) => Decagon.Upright(PillarX(k), arenaCentre, EmpressArena.Radius, out top, out bottom);

        private void RunPillars(NPC npc, Player who, bool entered)
        {
            int t = timer;
            Vector2 hover = new Vector2(npc.Center.X, who.Center.Y - 360f);

            // She hangs where she is through it, a little over the player's height.
            FlyTo(npc, beat == PillarRaise ? hover : npc.Center + npc.velocity, 6f, 0.05f);
            npc.velocity *= 0.94f;

            switch (beat)
            {
                case PillarRaise:
                    if (entered)
                    {
                        Sound(SoundID.Item162, npc.Center, 0.2f, 0.75f);
                        Sound(SoundID.Item25, npc.Center, 0.6f, 0.5f);

                        // Built up to the drag: the shimmer played backwards, quickened to end as the hand comes down.
                        Sound(EmpressSounds.Swell, npc.Center, 0.85f, 0.7f, 2);
                    }

                    if (Authority && t >= RaiseTicks)
                        Next(npc, PillarDrag);

                    break;

                case PillarDrag:
                    if (entered && !Main.dedServ)
                    {
                        Sound(SoundID.Item163, npc.Center, -0.1f, 0.8f);
                        Sound(SoundID.Item14, npc.Center, -0.9f, 0.5f);
                        Sound(SoundID.Item9, npc.Center, 0.5f, 0.6f);
                        Sound(EmpressSounds.WhooshDown, npc.Center, 0.2f, 0.7f, 2);
                        Sound(EmpressSounds.Tingle, npc.Center, 0.4f, 0.55f);
                        Scream(npc, -0.1f, 0.8f);

                        Vector2 palm = Palm(npc, actSide < 0, PillarHand());
                        PADParticleHandler.SpawnParticle(new BloomGeneric(palm, Vector2.Zero, Color.White, 160f / 160f, 14));
                        EmpressWarp.Ripple(palm, 9f, 40f, 9f, 30);
                    }

                    if (Authority && t >= DragTicks)
                        Next(npc, PillarSweep);

                    break;

                case PillarSweep:
                    {
                        // One hitbox for the row: it asks, each tick, which beams are whole (PillarsHit).
                        if (entered)
                            Hitbox(npc, EmpressHitbox.Shape.Pillars, npc.Center, 0f, Hurt(npc, PillarNormal, PillarExpert), (actN - 1) * PillarEvery + PillarLife + 10);

                        // The air wavering about the beams, heard: a glass tone beating against itself, now and then.
                        if (!Main.dedServ && t % 110 == 10)
                            Sound(EmpressSounds.Wobble, Main.LocalPlayer.Center, -0.15f + 0.1f * (t / 110 % 3), 0.32f, 2);

                        for (int k = 0; k < actN; k++)
                        {
                            if (Main.dedServ)
                                break;

                            if (Crossed(k * PillarEvery))
                                PillarWarns(k);

                            if (Crossed(k * PillarEvery + PillarWarn))
                                PillarFires(k);
                        }

                        if (Authority && t >= (actN - 1) * PillarEvery + PillarLife)
                            Next(npc, PillarRest);

                        break;
                    }

                case PillarRest:
                    if (Authority && t >= PillarRestTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The pillars' hitbox asks: is this body in a beam while it is whole (its 3rd tick to its 20th)?</summary>
        internal bool PillarsHit(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Pillars || beat != PillarSweep)
                return false;

            for (int k = 0; k < actN; k++)
            {
                int beam = timer - k * PillarEvery - PillarWarn;

                if (beam < 3 || beam >= PillarShoot + PillarHold || !PillarSpan(k, out float top, out float bottom))
                    continue;

                Rectangle whole = new Rectangle((int)(PillarX(k) - PillarHalf), (int)top, (int)(PillarHalf * 2f), (int)(bottom - top));

                if (whole.Intersects(box))
                    return true;
            }

            return false;
        }

        /// <summary>A pillar shot down: its sounds, rising up the row; a wave thrown out either side of it; sparks off it.</summary>
        private void PillarFires(int k)
        {
            if (!PillarHeard(k, out Vector2 at))
                return;

            float rise = actN > 1 ? k / (float)(actN - 1) : 0f;

            // High and low by turns, the row climbing as it goes: the game's laser, her glass one over it; a shockwave
            // under every third, and a sparkle on every fourth.
            Sound(SoundID.Item12, at, (k % 2 == 0 ? -0.55f : 0.25f) + 0.6f * rise, 0.42f, 6);
            Sound(EmpressSounds.Zap, at, (k % 2 == 0 ? -0.3f : 0.2f) + 0.4f * rise, 0.42f, 5);

            if (k % 2 == 1)
                Sound(SoundID.Item9, at, 0.6f + 0.3f * rise, 0.25f);

            if (k % 3 == 0)
                Sound(EmpressSounds.Shock, at, 0.35f, 0.3f, 3);

            if (k % 4 == 2)
                Sound(EmpressSounds.Tingle, at, 0.5f + 0.4f * rise, 0.35f);

            // The air either side of it pushed out as it comes down: two walls of bending going out from it.
            float x = PillarX(k);
            View(out _, out _, out float viewTop, out float viewBottom);

            if (PillarSpan(k, out float top, out float bottom))
            {
                float from = Math.Max(top, viewTop - 100f);
                float to = Math.Min(bottom, viewBottom + 100f);

                if (to > from)
                {
                    EmpressWarp.Ripple(new Vector2(x, from), 13f, 42f, 9f, 30, new Vector2(x, to));

                    Color hue = PillarHue(k);

                    for (int i = 0; i < 10; i++)
                    {
                        Vector2 spark = new Vector2(x + Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(from, to));
                        Dust dust = Dust.NewDustPerfect(spark, DustID.RainbowMk2, new Vector2(Main.rand.NextFloat(2.5f, 6f) * (Main.rand.NextBool() ? 1f : -1f), Main.rand.NextFloat(-1.5f, 1.5f)),
                            0, Color.Lerp(hue, Color.White, 0.3f), Main.rand.NextFloat(0.9f, 1.4f));
                        dust.noGravity = true;
                        dust.fadeIn = 1.1f;
                    }
                }
            }
        }

        /// <summary>A pillar's line coming: its light falling down it, heard as a falling star.</summary>
        private void PillarWarns(int k)
        {
            if (PillarHeard(k, out Vector2 at))
                Sound(EmpressSounds.Drop, at, 0.15f + 0.25f * (k % 3) / 2f, 0.2f, 4);
        }

        /// <summary>Where a pillar is heard from on this machine: where it crosses its player's height (a beam the arena's height tall is heard where it passes them).</summary>
        private bool PillarHeard(int k, out Vector2 at)
        {
            at = Vector2.Zero;

            if (Main.dedServ || !PillarSpan(k, out float top, out float bottom))
                return false;

            at = new Vector2(PillarX(k), MathHelper.Clamp(Main.LocalPlayer.Center.Y, top, bottom));
            return true;
        }

        /// <summary>The raised hand's angle: up for the raise, dragged down through the drag, held low and out over the sweep.</summary>
        private float PillarHand()
        {
            float outward = actSide > 0 ? 0f : MathHelper.Pi;
            float low = outward + (actSide > 0 ? 0.7f : -0.7f);
            float high = Up + (actSide > 0 ? 0.25f : -0.25f);

            return beat switch
            {
                PillarRaise => LerpAngle(outward + (actSide > 0 ? 1.1f : -1.1f), high, Ease(timer / 16f)),
                PillarDrag => LerpAngle(high, low, (float)Math.Pow(Math.Clamp(timer / (float)DragTicks, 0f, 1f), 1.6)),
                PillarSweep => low,
                _ => LerpAngle(low, outward + (actSide > 0 ? 1.1f : -1.1f), Ease(timer / (float)PillarRestTicks)),
            };
        }

        private void DrawPillarsHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            // The hand on the player's side does it; the other hangs at her side.
            float hand = PillarHand();
            float hangs = actSide > 0 ? MathHelper.Pi - 1.1f : 1.1f;
            float left = actSide > 0 ? hangs : hand;
            float right = actSide > 0 ? hand : hangs;

            float bright = beat == PillarDrag ? 0.3f * (1f - timer / (float)DragTicks) : 0f;
            DrawHerActing(spriteBatch, npc, screenPos, Arms.At(left, right), bright, 0.4f);
        }

        /// <summary>
        /// Pillar k's colour: the rainbow in its order, one step a pillar, from where the seed starts it; full colours
        /// (not her pale ones), as asked: "make it colorful tho for night". <paramref name="shift"/> a little round the
        /// rainbow from it, for its edges.
        /// </summary>
        private Color PillarHue(int k, float shift = 0f) => Main.hslToRgb(((k / 7f + actSeed % 97 / 97f + shift) % 1f + 1f) % 1f, 1f, 0.62f);

        /// <summary>The raised hand's light, and the row of pillars: lines with their falling lights, then beams.</summary>
        private void DrawPillarsOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;

            if (beat == PillarRaise || beat == PillarDrag)
                DrawPillarHand(npc, spriteBatch, time);

            if (beat != PillarSweep)
                return;

            View(out _, out _, out float viewTop, out float viewBottom);

            for (int k = 0; k < actN; k++)
            {
                int age = timer - k * PillarEvery;

                if (age < 0 || age >= PillarLife || !PillarSpan(k, out float top, out float bottom))
                    continue;

                if (age < PillarWarn)
                    DrawPillarLine(spriteBatch, k, age, top, bottom, viewTop, viewBottom, time);
                else
                    DrawPillarBeam(spriteBatch, k, age - PillarWarn, top, bottom, viewTop, viewBottom, time);
            }
        }

        /// <summary>The hand filling: her colours and white at the palm, motes of light circling in to it, the world about it drawn in, turning.</summary>
        private void DrawPillarHand(NPC npc, SpriteBatch spriteBatch, float time)
        {
            Vector2 palm = Palm(npc, actSide < 0, PillarHand());
            float fill = beat == PillarRaise ? Ease(timer / (float)RaiseTicks) : 1f - timer / (float)DragTicks;

            EmpressArt.DrawBloom(spriteBatch, palm, 90f * fill, EmpressArt.Additive(EmpressArt.Prism(time / 60f), 0.6f * fill));
            EmpressArt.DrawBloom(spriteBatch, palm, 34f * fill, EmpressArt.Additive(Color.White, fill));
            EmpressArt.DrawStar(spriteBatch, palm, 70f * fill, time * 0.05f, EmpressArt.Additive(Color.White, 0.6f * fill));
            EmpressArt.DrawStar(spriteBatch, palm, 44f * fill, -time * 0.08f + MathHelper.PiOver4, EmpressArt.Additive(EmpressArt.Pink, 0.5f * fill));

            // Seven motes, one of each colour, spiralling in.
            for (int i = 0; i < 7; i++)
            {
                float a = time * 0.09f + i * MathHelper.TwoPi / 7f;
                float r = (110f - 70f * fill) * (0.8f + 0.2f * (float)Math.Sin(time * 0.2f + i));
                Vector2 mote = palm + a.ToRotationVector2() * r;
                Color colour = Main.hslToRgb(i / 7f, 1f, 0.68f);

                EmpressArt.DrawBloom(spriteBatch, mote, 16f, EmpressArt.Additive(colour, 0.7f * fill));
                EmpressArt.DrawStar(spriteBatch, mote, 14f, a, EmpressArt.Additive(Color.White, 0.6f * fill));
            }

            EmpressWarp.Lens(palm, 120f * fill + 20f, 0.16f * fill, 0.7f * fill);
        }

        /// <summary>
        /// A pillar's line: thin, flickering, coming up, the air along it shimmering; and down it its light falling, from
        /// the top of the screen to its foot over the line's time, as in the clip: a streak with stars along it, the
        /// world beside it kinked as it passes.
        /// </summary>
        private void DrawPillarLine(SpriteBatch spriteBatch, int k, int age, float top, float bottom, float viewTop, float viewBottom, float time)
        {
            float x = PillarX(k);
            Color hue = PillarHue(k);
            float up = Ease(age / 12f);
            float flicker = 0.65f + 0.35f * (float)Math.Sin(age * 1.7f + k);

            EmpressArt.DrawLine(spriteBatch, new Vector2(x, top), new Vector2(x, bottom), 3f, EmpressArt.Additive(hue, 0.75f * up * flicker));
            EmpressArt.DrawLine(spriteBatch, new Vector2(x, top), new Vector2(x, bottom), 14f, EmpressArt.Additive(hue, 0.12f * up));

            float seenTop = Math.Max(top, viewTop - 80f);
            float seenBottom = Math.Min(bottom, viewBottom + 80f);

            if (seenBottom > seenTop)
                EmpressWarp.Haze(new Vector2(x, seenTop), new Vector2(x, seenBottom), 30f, 1.6f * up, 150f, k * 1.7f);

            // The falling light: quickening a little as it falls.
            float fall = (float)Math.Pow(age / (float)PillarWarn, 0.85);
            float head = MathHelper.Lerp(viewTop - 60f, viewBottom + 60f, fall);

            if (head < top || head > bottom)
                return;

            Vector2 at = new Vector2(x, head);
            float strength = Math.Min(1f, age / 4f);

            EmpressArt.DrawRay(spriteBatch, at, Up, 210f, 16f, EmpressArt.Additive(hue, 0.55f * strength));
            EmpressArt.DrawRay(spriteBatch, at, Up, 150f, 4f, EmpressArt.Additive(Color.White, 0.9f * strength));

            for (int i = 0; i < 3; i++)
            {
                float twinkle = 0.6f + 0.4f * (float)Math.Sin(time * 0.4f + i * 2.1f + k);
                Vector2 star = at + new Vector2(0f, -36f - i * 44f);
                EmpressArt.DrawStar(spriteBatch, star, 24f * twinkle * (1f - i * 0.22f), 0f, EmpressArt.Additive(Color.White, 0.75f * strength * twinkle));
            }

            EmpressArt.DrawBloom(spriteBatch, at, 50f, EmpressArt.Additive(hue, 0.7f * strength));
            EmpressArt.DrawBloom(spriteBatch, at, 18f, EmpressArt.Additive(Color.White, strength));
            EmpressArt.DrawStar(spriteBatch, at, 44f, time * 0.05f + k, EmpressArt.Additive(Color.White, 0.85f * strength));

            // The world beside it kinked as it passes, all of the row the same way (against the way the row runs).
            EmpressWarp.Kink(at, Vector2.UnitY, 170f, 48f, 13f * strength, new Vector2(-actSide, 0f));
        }

        /// <summary>
        /// A beam: shot down, held, gone. Its glow, its colour, its white heart; its neighbours' colours thin at its edges;
        /// light running down it; at its head, as it comes, the world kinked; the air either side rippling while it holds.
        /// </summary>
        private void DrawPillarBeam(SpriteBatch spriteBatch, int k, int beam, float top, float bottom, float viewTop, float viewBottom, float time)
        {
            float x = PillarX(k);
            Color hue = PillarHue(k);
            float reach = Ease(Math.Min(1f, (beam + 1) / (float)PillarShoot));
            float fade = beam < PillarShoot + PillarHold ? 1f : 1f - (beam - PillarShoot - PillarHold) / (float)PillarGone;
            float width = MathHelper.Lerp(0.3f, 1f, fade);
            Vector2 from = new Vector2(x, top);
            Vector2 to = new Vector2(x, MathHelper.Lerp(top, bottom, reach));

            EmpressArt.DrawLine(spriteBatch, from, to, 120f * width, EmpressArt.Additive(hue, 0.3f * fade));
            EmpressArt.DrawLine(spriteBatch, from, to, 46f * width, EmpressArt.Additive(hue, 0.95f * fade));
            EmpressArt.DrawLine(spriteBatch, from, to, 9f * width, EmpressArt.Additive(Color.White, 0.9f * fade));

            // Its edges: the colours either side of its own, thin, as light split by glass.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 off = new Vector2(side * (26f * width + 3f), 0f);
                EmpressArt.DrawLine(spriteBatch, from + off, to + off, 3f * width + 1f, EmpressArt.Additive(PillarHue(k, side * 0.09f), 0.6f * fade));
            }

            // Light running down it where it is seen: bright slips going down fast.
            float seenTop = Math.Max(top, viewTop - 80f);
            float seenBottom = Math.Min(to.Y, viewBottom + 80f);
            float seen = seenBottom - seenTop;

            if (seen > 20f)
            {
                const int Slips = 5;
                float gap = (viewBottom - viewTop + 160f) / Slips;

                for (int i = 0; i < Slips; i++)
                {
                    float y = viewTop - 80f + ((time * 26f + i * gap + k * 53f) % (gap * Slips));

                    if (y < seenTop || y > seenBottom)
                        continue;

                    EmpressArt.DrawLine(spriteBatch, new Vector2(x, y - 70f), new Vector2(x, y + 10f), 14f * width, EmpressArt.Additive(Color.White, 0.4f * fade));
                }

                // The air either side of it rippling while it holds, fading as it goes.
                EmpressWarp.Haze(new Vector2(x, seenTop), new Vector2(x, seenBottom), 150f, 3.8f * fade, 170f, k * 1.3f);
            }

            // Its head as it comes down: the world kinked there as the falling light kinked it.
            if (beam < PillarShoot && reach < 0.999f)
                EmpressWarp.Kink(to, Vector2.UnitY, 220f, 70f, 22f, new Vector2(actSide, 0f));

            // Where it lands, and where it starts.
            if (reach > 0.99f)
            {
                float flare = fade * (beam < PillarShoot + 4 ? 1.4f : 1f);
                EmpressArt.DrawBloom(spriteBatch, to, 120f * flare, EmpressArt.Additive(hue, 0.5f * fade));
                EmpressArt.DrawStar(spriteBatch, to, 90f * flare, 0f, EmpressArt.Additive(Color.White, 0.7f * fade));
            }

            EmpressArt.DrawBloom(spriteBatch, from, 80f * fade, EmpressArt.Additive(hue, 0.4f * fade));
            EmpressArt.DrawStar(spriteBatch, from, 70f * fade, time * 0.03f, EmpressArt.Additive(Color.White, 0.6f * fade));
        }
    }
}
