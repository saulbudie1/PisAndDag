using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the eclipse (Legends act, chosen from those offered: "Eclipse Corona") ------------------------------------
    //
    //     GATHER   90   she draws the light in: the sky and the world go dark round her (for whoever is near her
    //                   arena), motes stream into her from all round, and she goes dark in front of a ring of light
    //                   wider than her wings, a sun in eclipse, its streamers turning
    //     MARK    100   a notch opens in her ring: the way out, a quarter turn or so from where the player is; a
    //                   second and two thirds to get there (a second more, by request), a high tick counting it down
    //     BURST   150   the ring is thrown out across the arena (20 px a tick), its notch with it: inside the notch
    //                   it passes by, anywhere else it hurts (65 in a normal world, 42 in an expert one, doubled as
    //                   the game doubles any hostile shot)
    //     RESTORE  60   the light comes back, and she with it
    //
    // Everything as it was after, by request: the light is only asked for while the act goes on.
    //
    // Prettier, by request ("make every attack more pretty and aesthetics with some weird brain tickling sound
    // effect"): as she gathers the light the world is drawn in toward her, turning (EmpressWarp; let go before the notch
    // opens, so the way out is seen where it is), the motes spiralling
    // in, a low hollow hum beating under it; as the notch opens, the diamond ring of an eclipse at its edges and beads of
    // light round her ring; the ring going out bends the world as it goes; bells as the light comes back.

    public partial class EmpressOfLight
    {
        private const int EclipseGather = 0;
        private const int EclipseMark = 1;
        private const int EclipseBurst = 2;
        private const int EclipseRestore = 3;

        private const int GatherTicks = 90;
        private const int MarkTicks = 100;
        private const int RingBurstTicks = 150;
        private const int RestoreTicks = 60;

        /// <summary>The ring: how fast it goes out, how long it lives (to the act's end), its notch's half width; her ring about her (wider than her wings).</summary>
        private const float RingPace = 20f;
        private const int RingLife = RingBurstTicks + RestoreTicks;
        private const float NotchHalf = 0.32f;
        private const float CoronaRadius = 230f;

        /// <summary>How dark it goes at the darkest (the sky's light on the world).</summary>
        private const float EclipseDark = 0.12f;

        private const int RingNormal = 65;
        private const int RingExpert = 42;

        /// <summary>Over the player, and the notch a quarter turn or so from them, one way or the other.</summary>
        private void PlanEclipse(NPC npc, Player target)
        {
            actAt = target.Center + new Vector2(0f, -340f);

            float toward = (target.Center - actAt).ToRotation();
            float aside = MathHelper.ToRadians(Main.rand.NextFloat(70f, 115f)) * (Main.rand.NextBool() ? 1f : -1f);
            actTurn = MathHelper.WrapAngle(toward + aside);
        }

        /// <summary>How dark it is through the act: down over the gather, held, back over the restore.</summary>
        private float EclipseLight()
        {
            return beat switch
            {
                EclipseGather => MathHelper.Lerp(1f, EclipseDark, Ease(timer / (float)GatherTicks)),
                EclipseMark or EclipseBurst => EclipseDark,
                _ => MathHelper.Lerp(EclipseDark, 1f, Ease(timer / (float)RestoreTicks)),
            };
        }

        private void RunEclipse(NPC npc, Player who, bool entered)
        {
            int t = timer;

            // Up over the player, and still.
            if (beat == EclipseGather)
                FlyTo(npc, actAt, 9f, 0.07f);
            else
                npc.velocity *= 0.85f;

            if (NearArena())
            {
                EmpressSky.Darken(1f);
                EmpressSky.Light(EclipseLight());
            }

            switch (beat)
            {
                case EclipseGather:
                    if (entered)
                    {
                        Sound(SoundID.Item165, npc.Center, -0.5f, 0.9f);
                        Sound(SoundID.DD2_EtherianPortalOpen, npc.Center, -0.6f, 0.6f);
                        Sound(EmpressSounds.Hum, npc.Center, -0.1f, 0.85f, 2);
                    }

                    // The light going out of the world: a hum falling from high to low.
                    if (t % 30 == 0)
                        Sound(SoundID.Item15, npc.Center, MathHelper.Lerp(0.5f, -0.9f, t / (float)GatherTicks), 0.45f);

                    if (!Main.dedServ)
                        Drawing(npc, t);

                    if (Authority && t >= GatherTicks)
                    {
                        actAt = npc.Center;
                        Next(npc, EclipseMark);
                    }

                    break;

                case EclipseMark:
                    if (entered)
                    {
                        Sound(SoundID.Item29, npc.Center, -0.8f, 0.8f);
                        Sound(SoundID.Item25, who.Center, 0.7f, 0.7f);
                        Sound(EmpressSounds.Tingle, npc.Center, 0.7f, 0.7f);
                        Sound(EmpressSounds.Hum, npc.Center, 0.25f, 0.5f, 2);
                    }

                    // The way out shown: a high tick counting down to the burst, climbing.
                    if (t % 20 == 10)
                        Sound(SoundID.Tink, who.Center, MathHelper.Lerp(0f, 0.9f, t / (float)MarkTicks), 0.45f);

                    if (Authority && t >= MarkTicks)
                        Next(npc, EclipseBurst);

                    break;

                case EclipseBurst:
                    if (entered)
                    {
                        Hitbox(npc, EmpressHitbox.Shape.Ring, npc.Center, 0f, Hurt(npc, RingNormal, RingExpert), RingLife);
                        actAt = npc.Center;

                        if (!Main.dedServ)
                        {
                            Sound(SoundID.Item163, npc.Center, -0.4f, 1f);
                            Sound(SoundID.Item14, npc.Center, -0.5f, 0.6f);
                            Sound(SoundID.Item14, npc.Center, -0.95f, 0.9f);
                            Sound(SoundID.Item29, npc.Center, 0.9f, 0.6f);
                            Sound(EmpressSounds.Shock, npc.Center, -0.3f, 0.9f, 2);
                            Sound(EmpressSounds.Chime, npc.Center, -0.55f, 0.55f, 2);
                            Scream(npc, -0.5f, 1f);

                            // The ring bends the world as it goes, riding with it.
                            EmpressWarp.Ripple(npc.Center, RingPace, 64f, 11f, RingBurstTicks);
                            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, Color.White, 520f / 160f, 24));
                            PADParticleHandler.SpawnParticle(new CrownGlint(npc.Center, 220f, 26, EmpressArt.Gold));
                        }
                    }

                    if (Authority && t >= RingBurstTicks)
                        Next(npc, EclipseRestore);

                    break;

                case EclipseRestore:
                    if (entered)
                    {
                        Sound(SoundID.Item4, npc.Center, -0.2f, 0.5f);
                        Sound(SoundID.Item25, npc.Center, 0.3f, 0.5f);
                        Sound(EmpressSounds.Chime, npc.Center, 0.3f, 0.6f, 2);
                    }

                    if (Authority && t >= RestoreTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The ring's hitbox asks: is this body on the ring going out (as this machine draws it), outside its notch?</summary>
        internal bool RingHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Eclipse || beat != EclipseBurst && beat != EclipseRestore)
                return false;

            int age = beat == EclipseBurst ? timer : RingBurstTicks + timer;
            return age < RingLife && EmpressHitbox.OnRing(box, actAt, RingPace * age, actTurn, NotchHalf);
        }

        /// <summary>The light drawn in: motes from all round into her.</summary>
        private void Drawing(NPC npc, int t)
        {
            if (t % 2 != 0)
                return;

            Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(700f, 700f) * Main.rand.NextFloat(0.6f, 1f);

            // In, and round: spiralling into her.
            Vector2 way = (npc.Center - from) / 28f;
            way += way.RotatedBy(MathHelper.PiOver2) * 0.6f;
            Dust dust = Dust.NewDustPerfect(from, DustID.RainbowMk2, way, 0, EmpressArt.Prism(Main.rand.NextFloat()), 1.3f);
            dust.noGravity = true;
            dust.fadeIn = 1.1f;
        }

        /// <summary>How dark she is against her ring: dark through the gather and the mark, coming back over the burst.</summary>
        private float EclipseShade()
        {
            return beat switch
            {
                EclipseGather => 0.85f * Ease(timer / (float)GatherTicks),
                EclipseMark => 0.85f,
                EclipseBurst => 0.85f * (1f - Ease(timer / 40f)),
                _ => 0f,
            };
        }

        private void DrawEclipseHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            // Her corona first, behind her: she is the dark in front of it.
            Restart(spriteBatch, true);
            DrawCorona(npc, spriteBatch);
            Restart(spriteBatch, false);

            // Arms out wide, drawing it in; down through the burst.
            float spread = beat == EclipseGather ? Ease(timer / 30f) : beat == EclipseRestore ? 1f - Ease(timer / (float)RestoreTicks) : 1f;
            float left = MathHelper.Lerp(MathHelper.Pi - 1.1f, MathHelper.Pi + 0.55f, spread);
            float right = MathHelper.Lerp(1.1f, -0.55f, spread);

            float burst = beat == EclipseBurst ? 1f - Ease(timer / 26f) : 0f;
            DrawHerActing(spriteBatch, npc, screenPos, Arms.At(left, right), burst * 0.8f, 0.25f + 0.6f * burst);

            // Dark against her ring: laid over her as a shadow (premultiplied: a colour with alpha darkens).
            float shade = EclipseShade();

            if (shade > 0.01f)
            {
                Color dark = new Color(6, 3, 16) * shade;
                EmpressArt.DrawShape(spriteBatch, npc, npc.Center, screenPos, 1f, dark, withArms: false);
                EmpressArt.DrawArmAt(spriteBatch, npc.Center - screenPos, true, left, dark, npc.scale, white: true);
                EmpressArt.DrawArmAt(spriteBatch, npc.Center - screenPos, false, right, dark, npc.scale, white: true);
            }
        }

        /// <summary>The ring going out (Strip draws it: no batch open).</summary>
        private void DrawEclipseBands(NPC npc)
        {
            if (beat != EclipseBurst && beat != EclipseRestore)
                return;

            int age = beat == EclipseBurst ? timer : RingBurstTicks + timer;

            if (age >= RingLife || !EmpressArt.Ready(EmpressArt.Line))
                return;

            float radius = RingPace * age;
            float fade = age < RingLife - 30 ? 1f : (RingLife - age) / 30f;

            DrawRingBand(actAt, radius, EmpressHitbox.RingThickness * 2.4f, actTurn, NotchHalf, 0.3f * fade);
            DrawRingBand(actAt, radius, EmpressHitbox.RingThickness, actTurn, NotchHalf, 0.95f * fade);
            DrawRingBand(actAt, radius, EmpressHitbox.RingThickness * 0.32f, actTurn, NotchHalf, 1f * fade, white: true);
        }

        /// <summary>A band round a point, this thick, its notch left open, in her colours going round.</summary>
        private static void DrawRingBand(Vector2 centre, float radius, float thick, float notchWay, float notchHalf, float strength, bool white = false)
        {
            int steps = Math.Clamp((int)(radius * 0.12f), 32, 260);
            float from = notchWay + notchHalf;
            float span = MathHelper.TwoPi - notchHalf * 2f;
            float inner = Math.Max(0f, radius - thick * 0.5f);
            float outer = radius + thick * 0.5f;

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float s = i / (float)steps;
                float a = from + span * s;
                Vector2 way = a.ToRotationVector2();

                // The band's ends come in soft at the notch.
                float end = Math.Min(1f, Math.Min(s, 1f - s) * span * radius / 60f);
                Color colour = EmpressArt.Additive(white ? Color.White : EmpressArt.Prism(s * 2f + (float)Main.timeForVisualEffects / 200f), strength * end);

                Strip.Pair(centre + way * inner, centre + way * outer, colour, colour, s);
            }

            Strip.Draw(EmpressArt.Line.Value, SamplerState.LinearClamp);
        }

        /// <summary>How much of her corona there is: up over the gather, gone early in the burst.</summary>
        private float Corona() => beat switch
        {
            EclipseGather => Ease(timer / (float)GatherTicks),
            EclipseMark => 1f,
            EclipseBurst => 1f - Ease(timer / 30f),
            _ => 0f,
        };

        /// <summary>How far open her ring's notch is.</summary>
        private float Notch() => beat == EclipseMark ? Ease(timer / 18f) : beat == EclipseBurst ? 1f : 0f;

        /// <summary>Her corona, behind her: a glow, her ring (its notch opening) and its streamers turning, longer and shorter.</summary>
        private void DrawCorona(NPC npc, SpriteBatch spriteBatch)
        {
            float corona = Corona();

            if (corona <= 0.01f)
                return;

            float time = (float)Main.timeForVisualEffects;
            Vector2 at = npc.Center;
            float notch = Notch();

            EmpressArt.DrawBloom(spriteBatch, at, CoronaRadius * 2.6f, EmpressArt.Additive(EmpressArt.Pink, 0.22f * corona));
            EmpressArt.DrawBloom(spriteBatch, at, CoronaRadius * 1.5f, EmpressArt.Additive(EmpressArt.Gold, 0.18f * corona));

            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * MathHelper.TwoPi + time * 0.004f;

                if (notch > 0f && Math.Abs(MathHelper.WrapAngle(a - actTurn)) < NotchHalf * notch)
                    continue;

                float length = (170f + 110f * (float)Math.Sin(time * 0.03f + k * 1.7f)) * corona;
                EmpressArt.DrawRay(spriteBatch, at + a.ToRotationVector2() * CoronaRadius * 0.92f, a, length, 30f, EmpressArt.Additive(EmpressArt.Prism(k / 18f), 0.5f * corona));
            }

            DrawRing(spriteBatch, at, CoronaRadius, 26f, EmpressArt.Additive(EmpressArt.Gold, 0.45f * corona), actTurn, NotchHalf * notch);
            DrawRing(spriteBatch, at, CoronaRadius, 8f, EmpressArt.Additive(Color.White, 0.95f * corona), actTurn, NotchHalf * notch);

            // Beads of light round the ring's rim, each glinting now and then, as at an eclipse's edge.
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * MathHelper.TwoPi + 0.13f * k;

                if (notch > 0f && Math.Abs(MathHelper.WrapAngle(a - actTurn)) < NotchHalf * notch)
                    continue;

                float glint = (float)Math.Sin(time * (0.05f + 0.01f * (k % 5)) + k * 1.9f);

                if (glint < 0.4f)
                    continue;

                float shine = (glint - 0.4f) / 0.6f * corona;
                Vector2 bead = at + a.ToRotationVector2() * CoronaRadius;
                EmpressArt.DrawBloom(spriteBatch, bead, 18f * shine, EmpressArt.Additive(Color.White, 0.9f * shine));
                EmpressArt.DrawStar(spriteBatch, bead, 30f * shine, 0f, EmpressArt.Additive(EmpressArt.Gold, 0.8f * shine));
            }

            // The diamond ring: as the notch opens, a brilliant point at each of its edges.
            if (notch > 0f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float a = actTurn + side * NotchHalf * notch;
                    Vector2 diamond = at + a.ToRotationVector2() * CoronaRadius;
                    float pulse = 0.85f + 0.15f * (float)Math.Sin(time * 0.3f + side);

                    EmpressArt.DrawBloom(spriteBatch, diamond, 90f * notch * pulse, EmpressArt.Additive(Color.White, 0.8f * corona));
                    EmpressArt.DrawStar(spriteBatch, diamond, 170f * notch * pulse, 0.2f * side, EmpressArt.Additive(Color.White, 0.95f * corona));
                    EmpressArt.DrawStar(spriteBatch, diamond, 90f * notch * pulse, MathHelper.PiOver4, EmpressArt.Additive(EmpressArt.Sky, 0.7f * corona));
                }
            }
        }

        /// <summary>Over the players: the way out marked, two faint lines out from the notch's sides across the arena; and the world drawn in to her as she gathers the light.</summary>
        private void DrawEclipseOver(NPC npc, SpriteBatch spriteBatch)
        {
            float notch = Notch();
            float corona = Corona();

            // The world drawn in to her as she gathers the light, turning; let go before the notch opens, so the way out is
            // seen where it is.
            if (beat == EclipseGather)
            {
                float pull = Ease(timer / 30f) * (1f - Ease((timer - (GatherTicks - 24)) / 24f));
                EmpressWarp.Lens(npc.Center, 620f * corona + 40f, -0.1f * pull, 0.55f * pull);
            }

            if (beat != EclipseMark || notch <= 0f)
                return;

            for (int side = -1; side <= 1; side += 2)
            {
                float a = actTurn + side * NotchHalf;
                EmpressArt.DrawRay(spriteBatch, npc.Center + a.ToRotationVector2() * CoronaRadius, a, 1600f * notch, 4f, EmpressArt.Additive(Color.White, 0.35f * notch));
            }
        }
    }
}
