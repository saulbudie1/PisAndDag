using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the light chainsaw (Legends act) -------------------------------------------------------------------------
    //
    // By request: "she makes a light chainsaw (... bascially photon ripper in calamitymod) and runs it to player,
    // but slowly while her vanilla attacks (except dash and tele) are still running."
    //
    //     CONJURE   44   she stops and holds both hands out at the player; a blade of light grows out of them, a
    //                    band of white with her colours round its edge, and a chain of prism teeth (spikes of light,
    //                    leaning back as a chain's do) running round it
    //     DRIVE     ...  vanilla fights on (its bolts, its lances, its sun dance, its rainbow) while she comes at the
    //                    player behind the saw (3.9 px a tick: half again as fast as at first, by request), turning it
    //                    after them; the blade grows out over the first two seconds from 14 tiles to its full reach,
    //                    37 and a half tiles (by request: "extend ... to max length of 50 tiles", then "maybe max length
    //                    is only 9/12 of this"), turning no faster at its tip
    //                    than 14 px a tick; its dash is skipped for the next move of its cycle, and its dart to the
    //                    player between moves is not made. Two of vanilla's moves, or eleven seconds, whichever is
    //                    first. Heard as a chainsaw (the game's own): its motor, two notes high and low by turns,
    //                    climbing as the blade grows, a rev now and then, the whine of it growing, and her scream as
    //                    it reaches its length.
    //     GONE      30   the teeth are flung off the blade, spinning, and it goes out, winding down.
    //
    // The blade hurts (60 in a normal world, 40 in an expert one, doubled by the game as its shots are); it is
    // drawn over the players, as a held thing of light should be.
    //
    // Prettier, by request ("make every attack more pretty and aesthetics with some weird brain tickling sound
    // effect"): the air along the blade shimmering and whirled at its tip (EmpressWarp); faint images of the blade where
    // it was a moment ago as it swings; a glint running out along it; built up to with the shimmer played backwards,
    // wavering in the air as it goes, glass flying as it goes out.
    //
    // As the Photon Ripper is made (by request: "check ... Photon Ripper code to see how to make a good chainsaw"): the
    // whole saw shivering where it is drawn, a pixel or so every frame; its chain of teeth running round the bar fast,
    // each tooth trailing a streak of its colour; prism teeth thrown out round it in a long squashed loop, out along one
    // side and back along the other, each trailing a long rainbow ribbon; sparks of every colour streaming off it toward
    // its tip.

    public partial class EmpressOfLight
    {
        /// <summary>The saw is out, with vanilla fighting on (Scene.None), and how long, and which way it points.</summary>
        private bool sawing;
        private int sawTimer;
        private float sawAngle;

        /// <summary>Vanilla moves finished since the saw came out (the server's count).</summary>
        private int sawDone;

        /// <summary>Ticks left of the saw going out once it is done (drawn only).</summary>
        private int sawFade;

        /// <summary>How fast the blade is swinging, smoothed (each machine's own, for the images it leaves).</summary>
        private float sawSwing;
        private const int SawFadeTicks = 30;

        private const int ConjureTicks = 44;
        private const int SawMostTicks = 660;
        private const int SawMoves = 2;

        /// <summary>
        /// The blade: from the hands this long as it is conjured, this long at its full reach (37.5 tiles), grown out
        /// over this long; this wide; how fast she comes; how fast it turns after the player (and how fast its tip
        /// may go round at the most, so the long blade sweeps no faster than the short one could be dodged).
        /// </summary>
        private const float SawLength = 230f;
        private const float SawReach = 600f;
        private const int SawExtendTicks = 120;
        private const float SawWidth = 36f;
        private const float SawPace = 3.9f;
        private const float SawTurn = 0.035f;
        private const float SawTipMost = 14f;

        /// <summary>How long the blade is now: as conjured, out to its full reach over the drive's first two seconds, and so as it goes out.</summary>
        private float SawLengthNow => MathHelper.Lerp(SawLength, SawReach, Ease(sawTimer / (float)SawExtendTicks));

        private const int SawNormal = 60;
        private const int SawExpert = 40;

        /// <summary>How far apart her hands are on the haft, as angles either side of the blade's.</summary>
        private const float SawGrip = 0.16f;

        private void PlanSaw(NPC npc, Player target)
        {
            sawAngle = (target.Center - npc.Center).ToRotation();
            sawDone = 0;
            sawTimer = 0;
        }

        /// <summary>CONJURE: the saw grows in her hands. Then vanilla is let go, the saw out.</summary>
        private void RunSawConjure(NPC npc, Player who, bool entered)
        {
            int t = timer;

            npc.velocity *= 0.88f;
            sawAngle = TurnToward(sawAngle, (who.Center - npc.Center).ToRotation(), 0.08f);

            if (entered)
            {
                Sound(SoundID.Item162, npc.Center, -0.2f, 0.8f);
                Sound(SoundID.Item23, npc.Center, -0.3f, 0.9f);

                // Built up to the blade's full making: the shimmer played backwards, quickened to the conjuring's length.
                Sound(EmpressSounds.Swell, npc.Center, 1f, 0.6f, 2);
                Sound(EmpressSounds.Tingle, npc.Center, -0.1f, 0.5f);
            }

            // The motor catching, revving up as the blade grows.
            if (t % 8 == 0)
                Sound(SoundID.Item22, npc.Center, MathHelper.Lerp(-0.6f, 0.3f, t / (float)ConjureTicks), 0.6f);

            if (!Main.dedServ && t % 4 == 0)
            {
                SawBlade(npc, Ease(t / (float)ConjureTicks), out Vector2 from, out Vector2 to);
                Vector2 at = Vector2.Lerp(from, to, Main.rand.NextFloat());
                Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Main.rand.NextVector2Circular(2f, 2f), 0, EmpressArt.Prism(Main.rand.NextFloat()), 1f);
                dust.noGravity = true;
            }

            if (Authority && t >= ConjureTicks)
            {
                EndAct(npc);

                // Back to vanilla, already past its dart to the player.
                npc.ai[1] = 11f;

                sawing = true;
                sawTimer = 0;
                sawDone = 0;

                Hitbox(npc, EmpressHitbox.Shape.Saw, npc.Center, 0f, Hurt(npc, SawNormal, SawExpert), SawMostTicks + 60);
            }
        }

        /// <summary>Before vanilla, while the saw is out: no dart to the player between moves.</summary>
        private void SawBeforeVanilla(NPC npc)
        {
            if (npc.ai[0] == 1f && npc.ai[1] < 11f)
                npc.ai[1] = 11f;
        }

        /// <summary>After vanilla, while the saw is out: no dash; the slow drive at the player; the turn; the count.</summary>
        private void SawAfterVanilla(NPC npc, bool chose, bool done)
        {
            Player who = TargetOf(npc);

            if (Authority)
            {
                if (chose && (npc.ai[0] == 8f || npc.ai[0] == 9f))
                {
                    // Its dash, skipped for the next move of its cycle (two dashes never come together in it).
                    int next = Cycle[(int)npc.ai[2] % Cycle.Length];
                    npc.ai[0] = next == 8 ? 2f : next;
                    npc.ai[2]++;
                    npc.netUpdate = true;
                }

                if (done)
                    sawDone++;

                bool change = chose && npc.ai[0] == 10f;

                if (sawDone >= SawMoves || sawTimer >= SawMostTicks || change || who == null)
                {
                    sawing = false;
                    sawFade = SawFadeTicks;
                    npc.netUpdate = true;
                    return;
                }
            }

            sawTimer++;

            if (who == null)
                return;

            // On at them behind the saw, whatever vanilla's move would have her do; the long blade turned no
            // faster at its tip than the short one.
            Vector2 toward = (who.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            npc.velocity = Vector2.Lerp(npc.velocity, toward * SawPace, 0.08f);

            float was = sawAngle;
            sawAngle = TurnToward(sawAngle, toward.ToRotation(), Math.Min(SawTurn, SawTipMost / SawLengthNow));
            sawSwing = MathHelper.Lerp(sawSwing, MathHelper.WrapAngle(sawAngle - was), 0.3f);

            if (!Main.dedServ)
                SawSounds(npc);

            // Sparks of every colour streaming off it toward its tip (the Photon Ripper's).
            if (!Main.dedServ)
            {
                SawBlade(npc, 1f, out Vector2 haft, out Vector2 point);
                Vector2 way = sawAngle.ToRotationVector2();

                for (int i = 0; i < 2; i++)
                {
                    Vector2 at = Vector2.Lerp(haft, point, Main.rand.NextFloat()) + way.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1f, 1f) * SawWidth * 0.6f;
                    Dust spark = Dust.NewDustPerfect(at, DustID.RainbowMk2, way * Main.rand.NextFloat(4f, 9f) + Main.rand.NextVector2Circular(1.5f, 1.5f), 0,
                        Main.hslToRgb((sawTimer / 40f + Main.rand.NextFloat(-0.1f, 0.1f)) % 1f, 0.95f, 0.8f), Main.rand.NextFloat(0.9f, 1.25f));
                    spark.noGravity = true;
                }
            }

            if (!Main.dedServ && Main.rand.NextBool(2))
            {
                SawBlade(npc, 1f, out _, out Vector2 tip);
                Dust dust = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(12f, 12f), DustID.RainbowMk2,
                    sawAngle.ToRotationVector2().RotatedByRandom(0.9) * Main.rand.NextFloat(3f, 8f), 0, EmpressArt.Prism(Main.rand.NextFloat()), 1.1f);
                dust.noGravity = true;
                dust.fadeIn = 0.9f;
            }
        }

        private static float TurnToward(float from, float to, float most) => from + MathHelper.Clamp(MathHelper.WrapAngle(to - from), -most, most);

        /// <summary>
        /// The saw heard (every tick it is out, on every machine with ears): its motor, the game's chainsaw, two notes
        /// high and low by turns, climbing as the blade grows; a rev now and then; the whine of it growing; and her
        /// scream as it reaches its length.
        /// </summary>
        private void SawSounds(NPC npc)
        {
            float grown = Ease(sawTimer / (float)SawExtendTicks);
            SawBlade(npc, 1f, out Vector2 from, out Vector2 to);
            Vector2 middle = Vector2.Lerp(from, to, 0.5f);

            if (sawTimer % 12 == 0)
                Sound(SoundID.Item22, middle, (sawTimer / 12 % 2 == 0 ? -0.35f : 0.25f) + 0.35f * grown, 0.75f);

            if (sawTimer % 70 == 35)
                Sound(SoundID.Item23, middle, Main.rand.NextFloat(-0.25f, 0.45f), 0.8f);

            if (sawTimer < SawExtendTicks && sawTimer % 15 == 0)
                Sound(SoundID.Item15, to, MathHelper.Lerp(-0.6f, 0.8f, sawTimer / (float)SawExtendTicks), 0.45f);

            if (sawTimer == SawExtendTicks)
            {
                Sound(SoundID.Item23, middle, 0.5f, 1f);
                Sound(EmpressSounds.Tingle, to, 0.4f, 0.6f);
                Sound(EmpressSounds.Shock, middle, 0.3f, 0.35f, 2);
                Scream(npc, -0.2f, 0.8f);
            }

            // The air about the blade wavering, a little higher as it grows.
            if (sawTimer % 140 == 70)
                Sound(EmpressSounds.Wobble, middle, -0.3f + 0.4f * grown, 0.35f, 2);
        }

        /// <summary>The saw going out: the motor winding down, its teeth ringing off it.</summary>
        private void SawGoesOut(NPC npc)
        {
            SawBlade(npc, 1f, out Vector2 from, out Vector2 to);
            Vector2 middle = Vector2.Lerp(from, to, 0.5f);

            Sound(SoundID.Item23, middle, -0.7f, 0.6f);
            Sound(SoundID.Item27, middle, 0.6f, 0.6f);
            Sound(SoundID.Item4, middle, 0.3f, 0.5f);
            Sound(EmpressSounds.Shards, middle, 0.4f, 0.6f, 2);
            Sound(EmpressSounds.WhooshDown, middle, 0f, 0.5f, 2);
        }

        /// <summary>The blade, grown this far (0 to 1): from between her hands out along its way.</summary>
        private void SawBlade(NPC npc, float grown, out Vector2 from, out Vector2 to)
        {
            Vector2 left = Palm(npc, true, sawAngle + SawGrip);
            Vector2 right = Palm(npc, false, sawAngle - SawGrip);

            from = Vector2.Lerp(left, right, 0.5f) + sawAngle.ToRotationVector2() * 6f;
            to = from + sawAngle.ToRotationVector2() * SawLengthNow * MathHelper.Clamp(grown, 0f, 1f);
        }

        /// <summary>The saw's hitbox asks: is this body on the blade?</summary>
        internal bool SawHits(Rectangle box)
        {
            if (!sawing)
                return false;

            NPC npc = NpcOfThis();

            if (npc == null)
                return false;

            SawBlade(npc, 1f, out Vector2 from, out Vector2 to);
            return EmpressHitbox.OnLine(box, from, to, SawWidth);
        }

        /// <summary>This Empress's NPC (the global knows its own only through the world).</summary>
        private NPC NpcOfThis()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.HallowBoss && Of(i) == this)
                    return Main.npc[i];
            }

            return null;
        }

        // ---- drawing --------------------------------------------------------------------------------------------

        /// <summary>Her, the saw held out in both hands (or vanilla's arms as it goes out).</summary>
        private void DrawSawing(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (sawing || scene == Scene.Act && act == Act.Saw)
            {
                // The haft held in both hands, the blade out the way it points.
                DrawHerActing(spriteBatch, npc, screenPos, Arms.At(sawAngle + SawGrip, sawAngle - SawGrip), 0f, 0.3f);
                return;
            }

            EmpressArt.DrawHer(spriteBatch, npc, screenPos, EmpressArt.Look.Of(npc));
        }

        /// <summary>The saw: its blade, its edge, its running teeth; or its teeth flying as it goes out.</summary>
        private void DrawSawOver(NPC npc, SpriteBatch spriteBatch)
        {
            float grown = scene == Scene.Act && act == Act.Saw ? Ease(timer / (float)ConjureTicks) : 1f;
            float alpha = sawFade > 0 && !sawing ? sawFade / (float)SawFadeTicks : 1f;

            SawDrawn(npc, grown, out Vector2 from, out Vector2 to);

            float length = Vector2.Distance(from, to);

            if (length < 2f)
                return;

            float time = (float)Main.timeForVisualEffects;
            Vector2 along = sawAngle.ToRotationVector2();
            Vector2 across = along.RotatedBy(MathHelper.PiOver2);
            float hum = 0.85f + 0.15f * (float)Math.Sin(time * 0.6f);

            // Images of the blade where it was a moment ago, as it swings.
            if (sawing && Math.Abs(sawSwing) > 0.002f)
            {
                for (int g = 1; g <= 3; g++)
                {
                    float back = sawAngle - sawSwing * 4f * g;
                    Vector2 end = from + back.ToRotationVector2() * length;
                    EmpressArt.DrawLine(spriteBatch, from, end, SawWidth * 0.9f, EmpressArt.Additive(EmpressArt.Prism(time / 120f + g * 0.2f), 0.14f * alpha / g));
                }
            }

            // The blade: a glow of her colours, a band of light, two rails of white down its sides, a white heart; its
            // ends rounded.
            EmpressArt.DrawBeam(spriteBatch, from, to, SawWidth * 2.4f, EmpressArt.Additive(EmpressArt.Prism(time / 120f), 0.26f * alpha * hum));
            EmpressArt.DrawBeam(spriteBatch, from, to, SawWidth * 1.05f, EmpressArt.Additive(EmpressArt.Sky, 0.5f * alpha));
            EmpressArt.DrawBeam(spriteBatch, from + across * SawWidth * 0.3f, to + across * SawWidth * 0.3f, 3f, EmpressArt.Additive(Color.White, 0.6f * alpha));
            EmpressArt.DrawBeam(spriteBatch, from - across * SawWidth * 0.3f, to - across * SawWidth * 0.3f, 3f, EmpressArt.Additive(Color.White, 0.6f * alpha));
            EmpressArt.DrawBeam(spriteBatch, from, to, SawWidth * 0.36f, EmpressArt.Additive(Color.White, 0.95f * alpha));

            // A glint running out along it, again and again; the air along it shimmering, whirled at its tip.
            Vector2 glint = Vector2.Lerp(from, to, time * 0.025f % 1f);
            EmpressArt.DrawStar(spriteBatch, glint, 56f * alpha, time * 0.07f, EmpressArt.Additive(Color.White, 0.8f * alpha));
            EmpressArt.DrawBloom(spriteBatch, glint, 44f, EmpressArt.Additive(EmpressArt.Pink, 0.45f * alpha));

            EmpressWarp.Haze(from, to, 72f, 2.4f * alpha, 90f, 0.3f);
            EmpressWarp.Lens(to, 90f, -0.07f * alpha, 0.55f * alpha * (float)Math.Sin(time * 0.05f));

            if (sawing || grown > 0.99f && sawFade == 0)
            {
                // Its edge all round, each side a little off the heart, the colours running down it.
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 off = across * side * SawWidth * 0.5f;
                    int pieces = Math.Clamp((int)(length / 24f), 10, 40);

                    for (int k = 0; k < pieces; k++)
                    {
                        Vector2 a = from + along * (length * k / pieces) + off;
                        Vector2 b = from + along * (length * (k + 1) / pieces) + off;
                        EmpressArt.DrawLine(spriteBatch, a, b, 5f, EmpressArt.Additive(EmpressArt.Prism(k / (float)pieces + time / 50f * side), 0.85f * alpha));
                    }
                }

                // The teeth: spikes of prism light run round the blade's edge, out along one side, round its end and
                // back along the other, each leaning back from the way it runs, as a chain's teeth do.
                float half = SawWidth * 0.5f + 3f;
                float perimeter = 2f * length + MathHelper.Pi * half;
                int teeth = Math.Clamp((int)(perimeter / 30f), 20, 70);
                float run = time * 13f;

                for (int k = 0; k < teeth; k++)
                {
                    float s = ((k * perimeter / teeth + run) % perimeter + perimeter) % perimeter;
                    Vector2 at = Around(from, along, across, length, half, s, out Vector2 travel, out Vector2 outward);
                    Vector2 tooth = (outward - travel * 0.55f).SafeNormalize(outward);
                    float way = tooth.ToRotation();
                    Color colour = Main.hslToRgb(((k / (float)teeth + time / 90f) % 1f + 1f) % 1f, 0.95f, 0.7f);

                    // A streak of its colour behind it, as it runs.
                    EmpressArt.DrawLine(spriteBatch, at - travel * 26f, at, 5f, EmpressArt.Additive(colour, 0.45f * alpha));
                    EmpressArt.DrawRay(spriteBatch, at - tooth * 3f, way, 26f, 13f, EmpressArt.Additive(colour, 0.95f * alpha));
                    EmpressArt.DrawRay(spriteBatch, at - tooth * 2f, way, 17f, 4f, EmpressArt.Additive(Color.White, 0.85f * alpha));
                    EmpressArt.DrawBloom(spriteBatch, at, 10f, EmpressArt.Additive(Color.White, 0.45f * alpha));
                }

                // The prism teeth thrown round it: their heads, turning as they go (their ribbons: DrawSawBands).
                for (int k = 0; k < SawLoopers; k++)
                {
                    float u = time / SawLoopTicks + k / (float)SawLoopers;
                    Vector2 at = SawLoop(from, along, across, length, u);
                    Vector2 next = SawLoop(from, along, across, length, u + 0.002f);
                    float turn = (next - at).ToRotation() + MathHelper.Pi * (u % 1f) * 2f;
                    Color colour = Main.hslToRgb(((k / (float)SawLoopers + time / 160f) % 1f + 1f) % 1f, 0.95f, 0.75f);

                    EmpressArt.DrawBloom(spriteBatch, at, 46f, EmpressArt.Additive(colour, 0.55f * alpha));
                    EmpressArt.DrawRay(spriteBatch, at, turn, 22f, 14f, EmpressArt.Additive(Color.White, 0.95f * alpha));
                    EmpressArt.DrawRay(spriteBatch, at, turn + MathHelper.Pi, 22f, 14f, EmpressArt.Additive(colour, 0.95f * alpha));
                    EmpressArt.DrawStar(spriteBatch, at, 30f, turn, EmpressArt.Additive(Color.White, 0.8f * alpha));
                }
            }
            else if (sawFade > 0)
            {
                // Going out: the teeth flung off it, spinning away.
                float p = 1f - alpha;

                int count = Math.Clamp((int)(length / 12f), 24, 70);

                for (int k = 0; k < count; k++)
                {
                    float s = k / (float)count;
                    float side = k % 2 == 0 ? 1f : -1f;
                    Vector2 flung = across * side * (SawWidth * 0.5f + 120f * p * (0.6f + 0.4f * (k * 7 % 5) / 5f));
                    Vector2 at = Vector2.Lerp(from, to, s) + flung;
                    float way = (across * side).ToRotation() + p * 6f * side + k;

                    EmpressArt.DrawRay(spriteBatch, at, way, 22f * alpha + 6f, 10f, EmpressArt.Additive(EmpressArt.Prism(s), alpha));
                }
            }

            // The tip, where it bites.
            EmpressArt.DrawBloom(spriteBatch, to, 60f, EmpressArt.Additive(EmpressArt.Pink, 0.45f * alpha * hum));
        }

        /// <summary>The prism teeth thrown round the saw: how many, and ticks for one to go round.</summary>
        private const int SawLoopers = 6;
        private const float SawLoopTicks = 70f;

        /// <summary>How far a prism tooth's loop goes out from the blade's line at the most (the Photon Ripper's 45).</summary>
        private const float SawLoopWide = 46f;

        /// <summary>
        /// The blade as drawn: as it is, shivering a pixel or so where it is drawn, a little differently every frame (the
        /// Photon Ripper's vibration; its hurting is where it is).
        /// </summary>
        private void SawDrawn(NPC npc, float grown, out Vector2 from, out Vector2 to)
        {
            SawBlade(npc, grown, out from, out to);
            float time = (float)Main.timeForVisualEffects;
            Vector2 shiver = new Vector2((float)Math.Sin(time * 7.31f), (float)Math.Cos(time * 9.17f + 1.3f)) * 1.4f;
            from += shiver;
            to += shiver;
        }

        /// <summary>
        /// A prism tooth's place on its loop round the saw, <paramref name="u"/> of the way round (as the Photon Ripper's
        /// teeth go): out along one side to the tip and back along the other, squashed to a long loop, never further from
        /// the blade's line than SawLoopWide, never behind her hands.
        /// </summary>
        private static Vector2 SawLoop(Vector2 from, Vector2 along, Vector2 across, float length, float u)
        {
            float a = MathHelper.TwoPi * u - MathHelper.PiOver2;
            float outward = ((float)Math.Sin(a) * 0.5f + 0.5f) * length;
            float side = MathHelper.Clamp((float)Math.Cos(a) * 0.25f * length, -SawLoopWide, SawLoopWide);
            return from + along * outward + across * side;
        }

        /// <summary>
        /// The prism teeth's ribbons (no batch open): behind each, a long tapering ribbon of the rainbow along the way it
        /// has come round the loop, white at its head.
        /// </summary>
        private void DrawSawBands(NPC npc)
        {
            if (!EmpressArt.Ready(EmpressArt.Line))
                return;

            float grown = scene == Scene.Act && act == Act.Saw ? Ease(timer / (float)ConjureTicks) : 1f;
            float alpha = sawFade > 0 && !sawing ? sawFade / (float)SawFadeTicks : 1f;

            if (!(sawing || grown > 0.99f && sawFade == 0))
                return;

            SawDrawn(npc, grown, out Vector2 from, out Vector2 to);
            float length = Vector2.Distance(from, to);

            if (length < 40f)
                return;

            Texture2D line = EmpressArt.Line.Value;
            float time = (float)Main.timeForVisualEffects;
            Vector2 along = sawAngle.ToRotationVector2();
            Vector2 across = along.RotatedBy(MathHelper.PiOver2);
            const int Trail = 24;

            for (int k = 0; k < SawLoopers; k++)
            {
                float head = time / SawLoopTicks + k / (float)SawLoopers;
                float hue = k / (float)SawLoopers;

                Strip.Begin();

                for (int j = Trail; j >= 0; j--)
                {
                    float u = head - j * 0.005f;
                    Vector2 at = SawLoop(from, along, across, length, u);
                    Vector2 ahead = SawLoop(from, along, across, length, u + 0.002f);
                    Vector2 normal = (ahead - at).SafeNormalize(across).RotatedBy(MathHelper.PiOver2);
                    float left = 1f - j / (float)Trail;
                    float width = 24f * left + 1f;
                    Color colour = Color.Lerp(Color.White, Main.hslToRgb(((hue + j * 0.03f + time / 160f) % 1f + 1f) % 1f, 0.95f, 0.55f), 0.35f + 0.5f * (1f - left));
                    colour = EmpressArt.Additive(colour, 0.85f * left * alpha);

                    Strip.Pair(at - normal * width * 0.5f, at + normal * width * 0.5f, colour, colour, left);
                }

                Strip.Draw(line, SamplerState.LinearClamp);
            }
        }

        /// <summary>
        /// A point a distance <paramref name="s"/> round a blade's edge (a long round-ended band): out along one side,
        /// round the far end, back along the other; and there, the way round it runs and the way out from the blade.
        /// </summary>
        private static Vector2 Around(Vector2 from, Vector2 along, Vector2 across, float length, float half, float s, out Vector2 travel, out Vector2 outward)
        {
            float end = MathHelper.Pi * half;

            if (s < length)
            {
                travel = along;
                outward = across;
                return from + along * s + across * half;
            }

            s -= length;

            if (s < end)
            {
                float a = s / half;
                travel = along.RotatedBy(-a);
                outward = across.RotatedBy(-a);
                return from + along * length + outward * half;
            }

            s -= end;
            travel = -along;
            outward = -across;
            return from + along * (length - s) - across * half;
        }
    }
}
