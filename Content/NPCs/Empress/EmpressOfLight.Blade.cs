using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the blade of everlasting rainbow (Legends act, her second half; offered, chosen) ------------------------
    //
    // By choice: "One of her hand shapes into a blade of everlasting rainbow and slashes".
    //
    //     FORM    40   her free hand reshapes into a blade of light, the Everlasting Rainbow's colours running out of her
    //                  palm, longer and longer to thirty tiles, with a long ringing shing
    //     three times:
    //     CLOSE   26   she flies at the player, the blade trailing behind her
    //     WIND    20   she stops, the blade drawn back, and where it will sweep is shown, its arc in her colours
    //     SWEEP   10   swept through, images of it left behind, a whoosh with a ringing edge
    //     REST    12
    //     the third a whole turn about her, the blade longer, and longer drawn back (36): out of its reach, or under it
    //     GONE    24   the blade drawn back into her hand, light falling off it
    //
    // The blade hurts as it sweeps (70 in a normal world, 46 in an expert one, doubled as the game doubles any hostile
    // shot). Where each sweep goes is the server's, fixed as she stops and sent; only then is it shown.

    public partial class EmpressOfLight
    {
        private const int BladeForm = 0;
        private const int BladeGone = 13;

        private const int BladeFormTicks = 40;
        private const int BladeCloseTicks = 26;
        private const int BladeWindTicks = 20;
        private const int BladeLastWind = 36;
        private const int BladeSweepTicks = 10;
        private const int BladeRestTicks = 12;
        private const int BladeGoneTicks = 24;

        private const int BladeSlashes = 3;

        /// <summary>The blade: its length (thirty tiles), the last sweep's; how wide it hurts; half the sweep's arc.</summary>
        private const float BladeLength = 480f;
        private const float BladeLast = 540f;
        private const float BladeWidth = 36f;
        private const float BladeArc = 1.7f;

        private const int BladeNormal = 70;
        private const int BladeExpert = 46;

        private void PlanBlade(NPC npc, Player target)
        {
            actTurn = (target.Center - npc.Center).ToRotation();
        }

        /// <summary>Which slash this beat is of (0 to 2), and which part of it (0 close, 1 wind, 2 sweep, 3 rest); -1 if none.</summary>
        private static int SlashOf(int step, out int part)
        {
            part = -1;

            if (step < 1 || step >= BladeGone)
                return -1;

            part = (step - 1) % 4;
            return (step - 1) / 4;
        }

        private const int PartClose = 0;
        private const int PartWind = 1;
        private const int PartSweep = 2;

        /// <summary>A slash's sweep: from where, through how much (its way round given by the sign).</summary>
        private void SlashArc(int slash, out float from, out float through)
        {
            float way = (slash % 2 == 0 ? 1f : -1f) * actSide;

            if (slash == BladeSlashes - 1)
            {
                from = actTurn - way * MathHelper.PiOver2;
                through = way * MathHelper.TwoPi;
                return;
            }

            from = actTurn - way * BladeArc;
            through = way * BladeArc * 2f;
        }

        /// <summary>The blade's angle and length at a tick of a beat.</summary>
        private void BladeNow(NPC npc, float t, out float angle, out float length)
        {
            int slash = SlashOf(beat, out int part);

            if (slash < 0)
            {
                // Formed held out at the player; gone, at its last place.
                angle = beat == BladeGone ? actTurn : actTurn + (BowLeft(npc) ? 0.4f : -0.4f);
                length = beat == BladeForm ? BladeLength * Ease(t / BladeFormTicks) : BladeLength * (1f - Ease(t / BladeGoneTicks));
                return;
            }

            SlashArc(slash, out float from, out float through);
            length = slash == BladeSlashes - 1 ? BladeLast : BladeLength;

            angle = part switch
            {
                PartClose => from,
                PartWind => from,
                PartSweep => from + through * Smoother(t / BladeSweepTicks),
                _ => from + through,
            };
        }

        private void RunBlade(NPC npc, Player who, bool entered)
        {
            int t = timer;
            BladeNow(npc, t, out float angle, out _);

            // Held where the act has it as it winds and sweeps; eased there as she flies in.
            FreeArmAt(angle, snap: SlashOf(beat, out int held) >= 0 && held != PartClose);

            if (beat == BladeForm)
            {
                npc.velocity *= 0.85f;

                if (entered && !Main.dedServ)
                {
                    Sound(EmpressSounds.Blade, npc.Center, 0f, 1f, 2);
                    Sound(SoundID.Item29, npc.Center, 0.7f, 0.5f);
                }

                if (Authority && t >= BladeFormTicks)
                {
                    actTurn = (who.Center - npc.Center).ToRotation();
                    Next(npc, 1);
                }

                return;
            }

            if (beat == BladeGone)
            {
                npc.velocity *= 0.85f;

                if (entered && !Main.dedServ)
                    Sound(EmpressSounds.Blade, npc.Center, 0.4f, 0.6f, 2);

                if (Authority && t >= BladeGoneTicks)
                    EndAct(npc);

                return;
            }

            int slash = SlashOf(beat, out int part);
            float length = slash == BladeSlashes - 1 ? BladeLast : BladeLength;

            switch (part)
            {
                case PartClose:
                    {
                        // At them, the blade trailing: to where they will be well inside its reach. (The last stays where it is.)
                        if (slash < BladeSlashes - 1)
                        {
                            Vector2 toward = (who.Center - npc.Center).SafeNormalize(Vector2.UnitX);
                            FlyTo(npc, who.Center - toward * length * 0.55f, 20f, 0.14f);
                        }
                        else
                        {
                            npc.velocity *= 0.85f;
                        }

                        if (entered && !Main.dedServ)
                            Sound(EmpressSounds.WhooshDown, npc.Center, 0.1f * slash, 0.6f, 2);

                        if (Authority && t >= BladeCloseTicks)
                        {
                            // Where the sweep goes: fixed now, sent, and shown.
                            actTurn = (who.Center - npc.Center).ToRotation();
                            Next(npc, beat + 1);
                        }

                        break;
                    }

                case PartWind:
                    npc.velocity *= 0.7f;

                    if (entered && !Main.dedServ)
                        Sound(EmpressSounds.Charge, npc.Center, 0.6f + 0.1f * slash, 0.5f, 2);

                    if (Authority && t >= (slash == BladeSlashes - 1 ? BladeLastWind : BladeWindTicks))
                        Next(npc, beat + 1);

                    break;

                case PartSweep:
                    // Planted as she sweeps: the arc shown in the wind is the arc it cuts.
                    npc.velocity *= 0.5f;

                    if (entered)
                    {
                        if (slash == 0)
                            Hitbox(npc, EmpressHitbox.Shape.Blade, npc.Center, 0f, Hurt(npc, BladeNormal, BladeExpert), (BladeCloseTicks + BladeLastWind + BladeSweepTicks + BladeRestTicks) * BladeSlashes + 30);

                        if (!Main.dedServ)
                        {
                            Sound(EmpressSounds.Slash, npc.Center, slash == BladeSlashes - 1 ? -0.3f : 0.1f * slash, 1f, 3);
                            Sound(SoundID.Item71, npc.Center, 0.2f, 0.6f);

                            if (slash == BladeSlashes - 1)
                                Scream(npc, 0.3f, 1f, rings: true);
                        }
                    }

                    if (Authority && t >= BladeSweepTicks)
                        Next(npc, beat + 1);

                    break;

                default:
                    npc.velocity *= 0.8f;

                    if (Authority && t >= BladeRestTicks)
                        Next(npc, slash == BladeSlashes - 1 ? BladeGone : beat + 1);

                    break;
            }
        }

        /// <summary>The blade's hitbox asks: is this body on it as it sweeps (between last tick's place and this one's)?</summary>
        internal bool BladeHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Blade || SlashOf(beat, out int part) < 0 || part != PartSweep)
                return false;

            NPC npc = NpcOfThis();

            if (npc == null)
                return false;

            BladeNow(npc, timer, out float now, out float length);
            BladeNow(npc, Math.Max(0, timer - 1), out float before, out _);
            bool left = !BowLeft(npc);

            for (int k = 0; k <= 4; k++)
            {
                float a = MathHelper.Lerp(before, now, k / 4f);
                Vector2 palm = Palm(npc, left, a);

                if (EmpressHitbox.OnLine(box, palm, palm + a.ToRotationVector2() * length, BladeWidth))
                    return true;
            }

            return false;
        }

        private void DrawBladeHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float bright = beat == BladeForm ? 0.3f * Ease(timer / 20f) : 0f;
            DrawSecondHer(npc, spriteBatch, screenPos, bright, 0.35f);

            // The blade, out of her free hand, over her.
            Restart(spriteBatch, true);
            DrawTheBlade(npc, spriteBatch);
            Restart(spriteBatch, false);
        }

        private void DrawTheBlade(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            bool left = !BowLeft(npc);
            BladeNow(npc, timer, out float angle, out float length);

            if (length < 4f)
                return;

            int slash = SlashOf(beat, out int part);

            // Images of it left behind as it sweeps.
            if (part == PartSweep || part == PartSweep + 1 && timer < 6)
            {
                for (int k = 1; k <= 6; k++)
                {
                    float back = part == PartSweep ? timer - k * 0.6f : BladeSweepTicks - k * 0.6f + timer * 0.2f;
                    SlashArc(slash, out float from, out float through);
                    float a = from + through * Smoother(Math.Max(0f, back) / BladeSweepTicks);
                    float fade = (1f - k / 7f) * (part == PartSweep + 1 ? 1f - timer / 6f : 1f);
                    Vector2 palm = Palm(npc, left, freeAngleNow);

                    DrawBladeLight(spriteBatch, palm, a, length, 0.35f * fade, time + k * 4f);
                }
            }

            DrawBladeLight(spriteBatch, Palm(npc, left, freeAngleNow), angle, length, 1f, time);
        }

        /// <summary>
        /// The blade: bands of the Everlasting Rainbow's colours side by side down its length, white down its middle,
        /// narrowing to its point, a star at its tip.
        /// </summary>
        private static void DrawBladeLight(SpriteBatch spriteBatch, Vector2 palm, float angle, float length, float strength, float time)
        {
            Vector2 way = angle.ToRotationVector2();
            Vector2 across = new Vector2(-way.Y, way.X);
            Vector2 tip = palm + way * length;
            Vector2 shoulderOfIt = palm + way * length * 0.82f;

            EmpressArt.DrawBeam(spriteBatch, palm, shoulderOfIt, BladeWidth * 2.4f, EmpressArt.Additive(Color.White, 0.12f * strength));

            for (int k = 0; k < 7; k++)
            {
                float off = (k - 3) * 4.5f;
                Color colour = Main.hslToRgb(((k / 7f + time / 140f) % 1f + 1f) % 1f, 1f, 0.66f);

                EmpressArt.DrawLine(spriteBatch, palm + across * off, shoulderOfIt + across * off * 0.8f, 5f, EmpressArt.Additive(colour, 0.75f * strength));
                EmpressArt.DrawLine(spriteBatch, shoulderOfIt + across * off * 0.8f, tip, 4f * (1f - Math.Abs(k - 3) / 4f) + 1f, EmpressArt.Additive(colour, 0.6f * strength));
            }

            EmpressArt.DrawBeam(spriteBatch, palm, tip, 3f, EmpressArt.Additive(Color.White, 0.95f * strength));
            EmpressArt.DrawStar(spriteBatch, tip, 70f * strength + 20f, time * 0.08f, EmpressArt.Additive(Color.White, 0.9f * strength));
            EmpressArt.DrawBloom(spriteBatch, palm, 70f, EmpressArt.Additive(EmpressArt.Pink, 0.6f * strength));
        }

        /// <summary>Over the players: where the next sweep goes, shown faint as she closes; the air along the blade shimmering.</summary>
        private void DrawBladeOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            int slash = SlashOf(beat, out int part);

            if (slash >= 0 && part == PartWind)
            {
                // Where it will sweep, fixed now: its arc, brightening to the sweep.
                float shown = Ease(timer / 8f) * (0.6f + 0.4f * Ease(timer / (float)(slash == BladeSlashes - 1 ? BladeLastWind : BladeWindTicks)));
                SlashArc(slash, out float from, out float through);
                float length = slash == BladeSlashes - 1 ? BladeLast : BladeLength;
                int steps = (int)(Math.Abs(through) / 0.08f);

                // The tip's whole reach: the arm turns about its shoulder, the blade out of the palm along it, its
                // edge half its width further.
                bool left = !BowLeft(npc);
                Vector2 shoulder = Palm(npc, left, 0f) - Vector2.UnitX * EmpressArt.ArmReach * npc.scale;
                float reach = EmpressArt.ArmReach * npc.scale + length + BladeWidth * 0.5f;
                Vector2 previous = shoulder + from.ToRotationVector2() * reach;

                for (int i = 1; i <= steps; i++)
                {
                    float a = from + through * i / steps;
                    Vector2 next = shoulder + a.ToRotationVector2() * reach;
                    Color colour = Main.hslToRgb(((i / (float)steps + time / 200f) % 1f + 1f) % 1f, 1f, 0.7f);

                    EmpressArt.DrawLine(spriteBatch, previous, next, 10f, EmpressArt.Additive(colour, 0.18f * shown));
                    EmpressArt.DrawLine(spriteBatch, previous, next, 1.5f, EmpressArt.Additive(Color.White, 0.45f * shown));
                    previous = next;
                }
            }

            if (slash >= 0 || beat == BladeForm)
            {
                BladeNow(npc, timer, out float angle, out float bladeLength);
                Vector2 palm = Palm(npc, !BowLeft(npc), freeAngleNow);
                EmpressWarp.Haze(palm, palm + angle.ToRotationVector2() * bladeLength, 40f, 1.6f, 70f, 0.4f);
            }
        }
    }
}
