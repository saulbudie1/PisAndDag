using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the prism at her heart (Legends act, her second half) ---------------------------------------------------
    //
    // By request: "Holds the prism onto her chests and shoots the 7 rays of colors right away. stays stationary but the
    // rays moves as well. gives 0.7 seconds of indicator".
    //
    //     RAISE   30   a prism of glass forms in her free hand, held to her chest
    //     SHOW    42   seven thin lines out of it, her seven colours, where the rays will go: seven tenths of a second
    //     FIRE   300   the seven at once, out to the border, turning about her, quicker as they go; she does not move
    //     BREAK   30   the prism cracks in her hand and bursts into light
    //
    // The rays hurt (55 in a normal world, 36 in an expert one, doubled as the game doubles any hostile shot).

    public partial class EmpressOfLight
    {
        private const int HeartRaise = 0;
        private const int HeartShow = 1;
        private const int HeartFire = 2;
        private const int HeartBreak = 3;

        private const int HeartRaiseTicks = 30;
        private const int HeartShowTicks = 42;
        private const int HeartFireTicks = 300;
        private const int HeartBreakTicks = 30;

        /// <summary>The seven: how wide they hurt; their turn at first, and how it gathers (radians a tick, and a tick squared).</summary>
        private const float HeartWidth = 26f;
        private const float HeartTurn = 0.0045f;
        private const float HeartGather = 0.000018f;

        private const int HeartNormal = 55;
        private const int HeartExpert = 36;

        private void PlanHeart(NPC npc, Player target)
        {
            // The first ray at the player; which way they go round.
            actTurn = (target.Center - npc.Center).ToRotation() + MathHelper.Pi / 7f;
        }

        /// <summary>Her heart: the hand at her chest (her free arm's first frame), where the prism is held.</summary>
        private Vector2 HeartAt(NPC npc) => Anchor(npc, BowLeft(npc));

        /// <summary>Ray k's way this tick: turning about her through the fire, quicker as it goes.</summary>
        private float HeartRay(int k)
        {
            float t = beat == HeartFire ? timer : beat == HeartBreak ? HeartFireTicks : 0f;
            float turned = actSide * (HeartTurn * t + HeartGather * t * t);
            return actTurn + k * MathHelper.TwoPi / 7f + turned;
        }

        private void RunHeart(NPC npc, Player who, bool entered)
        {
            int t = timer;

            // Still, through all of it: she does not move.
            npc.velocity *= beat == HeartRaise ? 0.8f : 0f;
            FreeArmFrame(1);

            // Turned to the player as the prism forms; then not again (the prism's place in her hand is kept).
            facingHeld = beat != HeartRaise;

            switch (beat)
            {
                case HeartRaise:
                    if (entered && !Main.dedServ)
                    {
                        Sound(EmpressSounds.Chime, npc.Center, 0.2f, 0.6f, 2);
                        Sound(SoundID.Item29, npc.Center, 0.5f, 0.5f);
                    }

                    if (Authority && t >= HeartRaiseTicks)
                        Next(npc, HeartShow);

                    break;

                case HeartShow:
                    if (entered)
                    {
                        Sound(EmpressSounds.Charge, npc.Center, 0.4f, 0.7f, 2);
                        Hitbox(npc, EmpressHitbox.Shape.Heart, npc.Center, 0f, Hurt(npc, HeartNormal, HeartExpert), HeartShowTicks + HeartFireTicks + 20);
                    }

                    if (Authority && t >= HeartShowTicks)
                        Next(npc, HeartFire);

                    break;

                case HeartFire:
                    if (entered && !Main.dedServ)
                    {
                        Sound(EmpressSounds.Refract, npc.Center, 0f, 1f, 2);
                        Sound(SoundID.Item163, npc.Center, -0.2f, 0.9f);
                        Sound(EmpressSounds.Beam, npc.Center, -0.3f, 0.8f, 2);
                        Shake(npc.Center, 6f, 16);
                    }

                    // A hum in the light, climbing as it quickens.
                    if (!Main.dedServ && t % 24 == 0 && t > 0)
                        Sound(SoundID.Item15, npc.Center, MathHelper.Lerp(-0.3f, 0.6f, t / (float)HeartFireTicks), 0.4f, 3);

                    if (Authority && t >= HeartFireTicks)
                        Next(npc, HeartBreak);

                    break;

                case HeartBreak:
                    if (entered)
                        PrismBreaks(HeartAt(npc));

                    if (Authority && t >= HeartBreakTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The rays' hitbox asks: is this body on any of the seven, while they burn?</summary>
        internal bool HeartHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Heart || beat != HeartFire)
                return false;

            NPC npc = NpcOfThis();

            if (npc == null)
                return false;

            Vector2 from = HeartAt(npc);

            for (int k = 0; k < 7; k++)
            {
                Vector2 way = HeartRay(k).ToRotationVector2();

                if (EmpressHitbox.OnLine(box, from + way * 24f, from + way * 3400f, HeartWidth))
                    return true;
            }

            return false;
        }

        private void DrawHeartHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float lit = beat == HeartShow ? Ease(timer / (float)HeartShowTicks) : beat == HeartFire ? 1f : 0f;
            DrawSecondHer(npc, spriteBatch, screenPos, 0.2f * lit, 0.4f + 0.4f * lit);
        }

        private void DrawHeartOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Vector2 heart = HeartAt(npc);

            if (beat == HeartShow)
            {
                // Where the seven will go: thin lines of their colours, flickering, brightening.
                float p = Ease(timer / (float)HeartShowTicks);

                for (int k = 0; k < 7; k++)
                {
                    Vector2 way = HeartRay(k).ToRotationVector2();
                    Vector2 to = ToWall(heart, HeartRay(k));
                    float flicker = 0.6f + 0.4f * (float)Math.Sin(time * 0.7f + k * 1.3f);

                    EmpressArt.DrawBeam(spriteBatch, heart + way * 24f, to, 10f, EmpressArt.Additive(Spectrum[k], 0.18f * p * flicker));
                    EmpressArt.DrawBeam(spriteBatch, heart + way * 24f, to, 1.6f, EmpressArt.Additive(Color.White, 0.55f * p));
                }
            }

            if (beat == HeartFire || beat == HeartBreak && timer < 12)
            {
                float fade = beat == HeartFire ? Math.Min(1f, timer / 5f) : 1f - timer / 12f;

                for (int k = 0; k < 7; k++)
                {
                    Vector2 way = HeartRay(k).ToRotationVector2();
                    Vector2 from = heart + way * 24f;
                    Vector2 to = ToWall(heart, HeartRay(k));
                    float pulse = 0.85f + 0.15f * (float)Math.Sin(time * 0.3f + k);

                    EmpressArt.DrawBeam(spriteBatch, from, to, HeartWidth * 3.2f, EmpressArt.Additive(Spectrum[k], 0.2f * fade * pulse));
                    EmpressArt.DrawBeam(spriteBatch, from, to, HeartWidth, EmpressArt.Additive(Spectrum[k], 0.85f * fade));
                    EmpressArt.DrawBeam(spriteBatch, from, to, HeartWidth * 0.3f, EmpressArt.Additive(Color.White, 0.55f * fade));

                    // Sparks running out along it; where it meets the border, light thrown up.
                    for (int i = 0; i < 6; i++)
                    {
                        float along = (time * 14f + i * 230f + k * 71f) % 1380f;

                        if (along > Vector2.Distance(from, to))
                            continue;

                        Vector2 spark = from + way * along;
                        EmpressArt.DrawStar(spriteBatch, spark, 28f, time * 0.05f + i, EmpressArt.Additive(Color.White, 0.7f * fade));
                    }

                    EmpressArt.DrawBloom(spriteBatch, to, 150f, EmpressArt.Additive(Spectrum[k], 0.5f * fade));
                    EmpressWarp.Haze(from, from + way * 1200f, 50f, 1.4f * fade, 150f, k * 0.9f);
                }
            }

            // The prism at her heart.
            if (beat != HeartBreak)
            {
                float lit = beat == HeartRaise ? 0.3f * Ease(timer / (float)HeartRaiseTicks) : beat == HeartShow ? 0.3f + 0.5f * Ease(timer / (float)HeartShowTicks) : 1f;
                float size = beat == HeartRaise ? Ease(timer / 20f) : 1f;

                if (size > 0.01f)
                {
                    DrawSmallPrism(spriteBatch, heart, time * 0.01f, lit, size * 0.5f);
                    EmpressWarp.Lens(heart, 70f, 0.2f * size, 0.1f);
                }
            }
            else
            {
                DrawPrismPieces(spriteBatch, heart, timer / (float)HeartBreakTicks);
            }
        }

        /// <summary>The small prism's corners, filled each time it is drawn (one array, not one a frame).</summary>
        private static readonly Vector2[] prismCorners = new Vector2[3];

        /// <summary>A prism of glass, smaller (a share of the arena's): its edges bright, a rainbow running through it.</summary>
        private static void DrawSmallPrism(SpriteBatch spriteBatch, Vector2 at, float turn, float lit, float size)
        {
            float height = PrismHeight * size;
            Vector2[] corner = prismCorners;

            for (int i = 0; i < 3; i++)
                corner[i] = at + (Up + turn + i * MathHelper.TwoPi / 3f).ToRotationVector2() * (height * 2f / 3f);

            EmpressArt.DrawBloom(spriteBatch, at, (120f + 90f * lit) * size * 1.4f, EmpressArt.Additive(EmpressArt.Sky, 0.25f + 0.35f * lit));

            for (int i = 0; i < 3; i++)
            {
                Vector2 a = corner[i];
                Vector2 b = corner[(i + 1) % 3];

                EmpressArt.DrawLine(spriteBatch, a, b, 6f, EmpressArt.Additive(EmpressArt.Sky, 0.4f + 0.3f * lit));
                EmpressArt.DrawLine(spriteBatch, a, b, 2f, EmpressArt.Additive(Color.White, 0.95f));
                EmpressArt.DrawStar(spriteBatch, a, 18f + 14f * lit, turn, EmpressArt.Additive(Color.White, 0.6f));
            }

            for (int k = 0; k < Spectrum.Length; k++)
            {
                Vector2 p = Vector2.Lerp(corner[1], corner[2], (k + 0.5f) / Spectrum.Length);
                EmpressArt.DrawLine(spriteBatch, Vector2.Lerp(corner[0], at, 0.4f), p, 2.5f, EmpressArt.Additive(Spectrum[k], 0.2f + 0.5f * lit));
            }
        }
    }
}
