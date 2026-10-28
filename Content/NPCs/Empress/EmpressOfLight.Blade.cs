using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
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
    // Images of the blade behind it wherever it moves, its last eight ticks fading back (by request: "attack sword needs
    // after image").
    //
    // How it looks (by request: "use the blade/petal texture of this attack ... the slash needs more distorting effect
    // and slash effect", "Make it beautiful and epic"): the blade is one of her Sun Dance's petals of light (the game's
    // own picture, its base in her palm), its colours running through the Everlasting Rainbow, a hair of other colours
    // either side of it. As it sweeps, petals fanned behind it and a smear of rainbow filling the arc it has cut, its
    // outer edge white; the world pushed before it. Where it has swept, the cut stays a moment: a line of white light
    // along the arc its tip drew, the world either side of it slid apart along it, healing. Each sweep's end sends a
    // shockwave off the blade; the last, a whole turn, a ring of it off her, the screen shaken.
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

        /// <summary>
        /// The cut a sweep leaves in the world (each machine's own, from what it saw): about where her shoulder was, its tip's
        /// reach, the arc; when it was made; whether the last.
        /// </summary>
        private Vector2 cutAt;
        private float cutReach;
        private float cutFrom;
        private float cutThrough;
        private uint cutMade;
        private bool cutLast;

        /// <summary>
        /// Where the blade has been, the last few ticks (each machine's own, as drawn): its palm, its angle, its length; and
        /// the tick it was last kept. Drawn behind it as images of it (by request: "attack sword needs after image").
        /// </summary>
        private const int BladeImages = 8;
        private readonly Vector2[] bladePalms = new Vector2[BladeImages];
        private readonly float[] bladeAngles = new float[BladeImages];
        private readonly float[] bladeLengths = new float[BladeImages];
        private int bladeKept;
        private uint bladeKeptAt;

        /// <summary>Ticks a cut stays (the last's longer).</summary>
        private const int CutTicks = 30;
        private const int CutLastTicks = 46;

        /// <summary>Ticks of its sweep the smear trails behind the blade (the last's longer: a wheel of it).</summary>
        private const float SmearTrail = 5f;
        private const float SmearLastTrail = 8f;

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
                            EmpressWarp.Ripple(Palm(npc, !BowLeft(npc), angle), 9f, 36f, 8f, 18);
                            Sound(EmpressSounds.Slash, npc.Center, slash == BladeSlashes - 1 ? -0.3f : 0.1f * slash, 1f, 3);
                            Sound(SoundID.Item71, npc.Center, 0.2f, 0.6f);

                            if (slash == BladeSlashes - 1)
                                Scream(npc, 0.3f, 1f, rings: true);
                        }
                    }

                    // Sparks off its tip as it goes.
                    if (!Main.dedServ)
                    {
                        Vector2 palm = Palm(npc, !BowLeft(npc), angle);
                        Vector2 tip = palm + angle.ToRotationVector2() * length;
                        SparkShard.Spray(tip, Color.White, 3, 7f, 40f, 16, palette: EmpressArt.Sparks);
                    }

                    if (Authority && t >= BladeSweepTicks)
                        Next(npc, beat + 1);

                    break;

                default:
                    npc.velocity *= 0.8f;

                    if (entered && !Main.dedServ)
                        BladeCut(npc, slash);

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

        /// <summary>Her shoulder the blade's arm turns about (the arc it cuts is about this).</summary>
        private Vector2 BladeShoulder(NPC npc) => Palm(npc, !BowLeft(npc), 0f) - Vector2.UnitX * EmpressArt.ArmReach * npc.scale;

        /// <summary>
        /// A sweep done, seen: the cut it leaves (where it is now, not following her), a shockwave off the blade where it
        /// stopped; the last, a ring off her, the screen shaken, a flash.
        /// </summary>
        private void BladeCut(NPC npc, int slash)
        {
            SlashArc(slash, out float from, out float through);
            float length = slash == BladeSlashes - 1 ? BladeLast : BladeLength;

            cutAt = BladeShoulder(npc);
            cutReach = EmpressArt.ArmReach * npc.scale + length;
            cutFrom = from;
            cutThrough = through;
            cutMade = Main.GameUpdateCount;
            cutLast = slash == BladeSlashes - 1;

            float end = from + through;
            Vector2 palm = Palm(npc, !BowLeft(npc), end);
            Vector2 tip = palm + end.ToRotationVector2() * length;
            EmpressWarp.Ripple(palm, 13f, 48f, 15f, 26, tip);

            if (cutLast)
            {
                EmpressWarp.Ripple(cutAt, 24f, 110f, 24f, 44);
                Shake(cutAt, 10f, 24);
                Cinematic.Flash(Color.White, 0.22f * Near(cutAt));
                PADParticleHandler.SpawnParticle(new BloomGeneric(cutAt, Vector2.Zero, Color.White, 700f / 160f, 22));
            }
            else
            {
                Shake(tip, 4f, 10);
            }
        }

        /// <summary>How much of the cut is left (1 as it is made, 0 gone).</summary>
        private float CutLeft()
        {
            if (cutMade == 0 || act != Act.Blade || scene != Scene.Act)
                return 0f;

            float age = Main.GameUpdateCount - cutMade;
            return MathHelper.Clamp(1f - age / (cutLast ? CutLastTicks : CutTicks), 0f, 1f);
        }

        /// <summary>
        /// The part of its arc the smear fills now: from its tail to its head (the blade), and how strong. While it sweeps,
        /// the last few ticks of its way; a moment after, the tail catching up to the head as it goes.
        /// </summary>
        private bool SmearNow(out float head, out float tail, out float length, out float fade)
        {
            head = tail = length = fade = 0f;
            int slash = SlashOf(beat, out int part);

            if (slash < 0)
                return false;

            bool last = slash == BladeSlashes - 1;
            float trail = last ? SmearLastTrail : SmearTrail;
            SlashArc(slash, out float from, out float through);
            length = last ? BladeLast : BladeLength;

            if (part == PartSweep)
            {
                head = from + through * Smoother(timer / (float)BladeSweepTicks);
                tail = from + through * Smoother(Math.Max(0f, timer - trail) / BladeSweepTicks);
                fade = 1f;
            }
            else if (part == PartSweep + 1 && timer < 10)
            {
                head = from + through;
                tail = from + through * Smoother(Math.Min(BladeSweepTicks, BladeSweepTicks - trail + timer * trail / 10f) / BladeSweepTicks);
                fade = 1f - timer / 10f;
            }
            else
            {
                return false;
            }

            return Math.Abs(head - tail) > 0.005f;
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
            float hue = time / 150f;

            // Petals fanned behind it as it sweeps, each a little on in the rainbow, fainter the further back.
            if (SmearNow(out float head, out float tail, out _, out float fade))
            {
                for (int k = 16; k >= 1; k--)
                {
                    float s = 1f - k / 17f;
                    float a = MathHelper.Lerp(tail, head, s);
                    DrawPetal(spriteBatch, Palm(npc, left, a), a, length, 0.4f * s * s * fade, hue + k * 0.035f, true);
                }
            }

            // Winding: the blade drinking light, brighter to the sweep; light drawn in along it.
            float wind = 0f;

            if (part == PartWind)
            {
                wind = Ease(timer / (float)(slash == BladeSlashes - 1 ? BladeLastWind : BladeWindTicks));
                Vector2 palm0 = Palm(npc, left, freeAngleNow);
                Vector2 way = angle.ToRotationVector2();

                for (int k = 0; k < 6; k++)
                {
                    float run = ((time * 0.05f + k / 6f) % 1f);
                    float along = length * (1f - run);
                    Vector2 at = palm0 + way * along + way.RotatedBy(MathHelper.PiOver2) * (float)Math.Sin(k * 2.1f + time * 0.1f) * 26f * (1f - run);
                    EmpressArt.DrawStar(spriteBatch, at, 30f * run + 8f, time * 0.05f + k, EmpressArt.Additive(EmpressArt.Prism(k / 6f + time / 90f), 0.7f * run * wind));
                }
            }

            Vector2 palm = Palm(npc, left, freeAngleNow);

            // Its images: where it has been these last few ticks, fading back; seen as it moves (as she flies at the
            // player, as it swings), gone when it is still.
            if (bladeKeptAt != Main.GameUpdateCount)
            {
                if (Main.GameUpdateCount - bladeKeptAt > 2u)
                    bladeKept = 0;

                for (int k = BladeImages - 1; k > 0; k--)
                {
                    bladePalms[k] = bladePalms[k - 1];
                    bladeAngles[k] = bladeAngles[k - 1];
                    bladeLengths[k] = bladeLengths[k - 1];
                }

                bladePalms[0] = palm;
                bladeAngles[0] = angle;
                bladeLengths[0] = length;
                bladeKept = Math.Min(BladeImages, bladeKept + 1);
                bladeKeptAt = Main.GameUpdateCount;
            }

            for (int k = bladeKept - 1; k >= 1; k--)
            {
                // How far it has come since: the image as strong as the blade has moved, a tip's width or more.
                Vector2 tipThen = bladePalms[k] + bladeAngles[k].ToRotationVector2() * bladeLengths[k];
                Vector2 tipNow = palm + angle.ToRotationVector2() * length;
                float moved = MathHelper.Clamp((Vector2.Distance(tipThen, tipNow) + Vector2.Distance(bladePalms[k], palm)) / 60f, 0f, 1f);
                float image = (1f - k / (float)BladeImages) * moved;

                if (image > 0.01f)
                    DrawPetal(spriteBatch, bladePalms[k], bladeAngles[k], bladeLengths[k], 0.5f * image, hue + k * 0.06f, true);
            }

            DrawPetal(spriteBatch, palm, angle, length, 1f + 0.35f * wind, hue, false);

            Vector2 tip = palm + angle.ToRotationVector2() * length;
            EmpressArt.DrawStar(spriteBatch, tip, 60f + 30f * wind, time * 0.08f, EmpressArt.Additive(Color.White, 0.85f));
            EmpressArt.DrawStar(spriteBatch, tip, 34f, -time * 0.11f + MathHelper.PiOver4, EmpressArt.Additive(EmpressArt.Prism(hue), 0.7f));
            EmpressArt.DrawBloom(spriteBatch, palm, 80f + 40f * wind, EmpressArt.Additive(EmpressArt.Prism(hue + 0.15f), 0.55f));
            EmpressArt.DrawBloom(spriteBatch, palm, 36f, EmpressArt.Additive(Color.White, 0.7f));
        }

        /// <summary>
        /// One of her Sun Dance's petals (the game's own picture: its outline above, its glow fading to its point below) as
        /// a blade, its base in her palm, this long, its colours this far through the rainbow; the glow alone
        /// (<paramref name="glowOnly"/>) for the images fanned behind it. Light, added; the batch smooth.
        /// </summary>
        private static void DrawPetal(SpriteBatch spriteBatch, Vector2 palm, float angle, float length, float strength, float hue, bool glowOnly)
        {
            Asset<Texture2D> asset = TextureAssets.Projectile[ProjectileID.FairyQueenSunDance];

            if (strength <= 0.003f || length < 2f)
                return;

            if (!EmpressArt.Ready(asset))
            {
                EmpressArt.DrawBeam(spriteBatch, palm, palm + angle.ToRotationVector2() * length, BladeWidth, EmpressArt.Additive(EmpressArt.Prism(hue), 0.6f * strength));
                return;
            }

            Texture2D petal = asset.Value;
            Rectangle outline = petal.Frame(1, 2, 0, 0);
            Rectangle glow = petal.Frame(1, 2, 0, 1);
            Vector2 origin = outline.Size() * new Vector2(0.03f, 0.5f);
            float along = length / (outline.Width * 0.97f);
            float across = 0.62f * MathHelper.Clamp(length / 220f, 0.3f, 1f);
            Vector2 at = palm - Main.screenPosition;

            Color far = Main.hslToRgb(Wrap(hue + 0.3f), 1f, 0.62f);
            Color near = Main.hslToRgb(Wrap(hue + 0.15f), 1f, 0.52f);

            // Its glow, in two of the rainbow's colours, the further the wider (as the game lays its own).
            spriteBatch.Draw(petal, at, glow, EmpressArt.Additive(far, 0.6f * strength), angle, origin, new Vector2(along * 1.04f, across * 1.5f), SpriteEffects.None, 0f);
            spriteBatch.Draw(petal, at, glow, EmpressArt.Additive(near, 0.75f * strength), angle, origin, new Vector2(along * 1.02f, across * 1.22f), SpriteEffects.None, 0f);

            if (glowOnly)
                return;

            // A hair of the colours either side of it, then the petal itself, white, its glow bright from her palm.
            spriteBatch.Draw(petal, at, outline, EmpressArt.Additive(Main.hslToRgb(Wrap(hue), 1f, 0.6f), 0.35f * strength), angle + 0.012f, origin, new Vector2(along, across * 0.85f), SpriteEffects.None, 0f);
            spriteBatch.Draw(petal, at, outline, EmpressArt.Additive(Main.hslToRgb(Wrap(hue + 0.5f), 1f, 0.6f), 0.35f * strength), angle - 0.012f, origin, new Vector2(along, across * 0.85f), SpriteEffects.None, 0f);
            spriteBatch.Draw(petal, at, outline, EmpressArt.Additive(Color.White, 0.32f * strength), angle, origin, new Vector2(along, across), SpriteEffects.None, 0f);
            spriteBatch.Draw(petal, at, glow, EmpressArt.Additive(Color.White, 0.95f * Math.Min(1f, strength)), angle, origin, new Vector2(along, across * 0.85f), SpriteEffects.None, 0f);

            // A thread of white down its edge to the point, so its far end reads as sharp as its near.
            EmpressArt.DrawBeam(spriteBatch, palm, palm + angle.ToRotationVector2() * length, 2.4f, EmpressArt.Additive(Color.White, 0.7f * Math.Min(1f, strength)));
        }

        private static float Wrap(float hue) => (hue % 1f + 1f) % 1f;

        /// <summary>
        /// Over the players, no batch open: the smear of the sweep (bands of the rainbow filling the arc it has cut, brighter
        /// toward the blade and toward its edge, the edge white), and the cut left after it (a line of light along its
        /// tip's arc, thinning as it heals).
        /// </summary>
        private void DrawBladeBands(NPC npc)
        {
            if (!EmpressArt.Ready(EmpressArt.Line))
                return;

            Texture2D line = EmpressArt.Line.Value;
            float time = (float)Main.timeForVisualEffects;

            if (SmearNow(out float head, out float tail, out float length, out float fade))
            {
                Vector2 shoulder = BladeShoulder(npc);
                float inner = EmpressArt.ArmReach * npc.scale + length * 0.1f;
                float outer = EmpressArt.ArmReach * npc.scale + length;
                int steps = Math.Clamp((int)(Math.Abs(head - tail) / 0.025f) + 2, 4, 260);
                const int Bands = 8;

                for (int b = 0; b < Bands; b++)
                {
                    float q0 = b / (float)Bands;
                    float q1 = (b + 1) / (float)Bands;
                    float r0 = MathHelper.Lerp(inner, outer, q0);
                    float r1 = MathHelper.Lerp(inner, outer, q1);
                    Color c0 = Main.hslToRgb(Wrap(q0 * 0.9f + time / 160f), 1f, 0.62f);
                    Color c1 = Main.hslToRgb(Wrap(q1 * 0.9f + time / 160f), 1f, 0.62f);
                    float w0 = 0.06f + 0.4f * q0 * q0;
                    float w1 = 0.06f + 0.4f * q1 * q1;

                    Strip.Begin();

                    for (int i = 0; i <= steps; i++)
                    {
                        float s = i / (float)steps;
                        Vector2 way = MathHelper.Lerp(tail, head, s).ToRotationVector2();
                        float strength = (float)Math.Pow(s, 1.7) * fade;
                        Strip.Pair(shoulder + way * r0, shoulder + way * r1, EmpressArt.Additive(c0, w0 * strength), EmpressArt.Additive(c1, w1 * strength), s, FlatRow, FlatRow);
                    }

                    Strip.Draw(line, SamplerState.LinearClamp);
                }

                // The edge it cuts with: white, soft either side.
                Strip.Begin();

                for (int i = 0; i <= steps; i++)
                {
                    float s = i / (float)steps;
                    Vector2 way = MathHelper.Lerp(tail, head, s).ToRotationVector2();
                    Color edge = EmpressArt.Additive(Color.White, 0.95f * (float)Math.Pow(s, 1.2) * fade);
                    Strip.Pair(shoulder + way * (outer - 22f), shoulder + way * (outer + 12f), edge, edge, s);
                }

                Strip.Draw(line, SamplerState.LinearClamp);
            }

            float left = CutLeft();

            if (left > 0.003f)
            {
                int steps = Math.Clamp((int)(Math.Abs(cutThrough) / 0.02f) + 2, 4, 360);
                float width = 3f + 16f * left * left;

                for (int band = 0; band < 2; band++)
                {
                    Strip.Begin();

                    for (int i = 0; i <= steps; i++)
                    {
                        float s = i / (float)steps;
                        Vector2 way = (cutFrom + cutThrough * s).ToRotationVector2();
                        Vector2 at = cutAt + way * cutReach;

                        // Brightest where it ended, flickering along it.
                        float shine = left * (0.55f + 0.45f * s) * (0.8f + 0.2f * (float)Math.Sin(s * 40f + time * 0.3f));
                        Color colour = band == 0
                            ? EmpressArt.Additive(EmpressArt.Prism(s + time / 200f), 0.45f * shine)
                            : EmpressArt.Additive(Color.White, 0.95f * shine);
                        float half = band == 0 ? width * 2.2f : width * 0.5f;

                        Strip.Pair(at - way * half, at + way * half, colour, colour, s);
                    }

                    Strip.Draw(line, SamplerState.LinearClamp);
                }
            }
        }

        /// <summary>
        /// Over the players: where the next sweep goes, shown faint as she closes; the world bent by the blade (the air
        /// along it shimmering, drawn in about her palm as she winds, pushed before it as it sweeps) and slid apart along
        /// the cut it leaves; glints along the cut.
        /// </summary>
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
                Vector2 shoulder = BladeShoulder(npc);
                float reach = EmpressArt.ArmReach * npc.scale + length + BladeWidth * 0.5f;
                Vector2 previous = shoulder + from.ToRotationVector2() * reach;

                for (int i = 1; i <= steps; i++)
                {
                    float a = from + through * i / steps;
                    Vector2 next = shoulder + a.ToRotationVector2() * reach;
                    Color colour = Main.hslToRgb(Wrap(i / (float)steps + time / 200f), 1f, 0.7f);

                    EmpressArt.DrawLine(spriteBatch, previous, next, 10f, EmpressArt.Additive(colour, 0.18f * shown));
                    EmpressArt.DrawLine(spriteBatch, previous, next, 1.5f, EmpressArt.Additive(Color.White, 0.45f * shown));
                    previous = next;
                }

                // The world drawn in about her palm, turning, as the blade drinks the light.
                BladeNow(npc, timer, out float windAngle, out _);
                Vector2 palm0 = Palm(npc, !BowLeft(npc), windAngle);
                EmpressWarp.Lens(palm0, 220f, -0.16f * shown, 0.3f * shown * Math.Sign(through));
            }

            if (slash >= 0 || beat == BladeForm)
            {
                BladeNow(npc, timer, out float angle, out float bladeLength);
                Vector2 palm = Palm(npc, !BowLeft(npc), freeAngleNow);
                Vector2 way = angle.ToRotationVector2();
                EmpressWarp.Haze(palm, palm + way * bladeLength, 50f, 2.6f, 70f, 0.4f);

                // Sweeping: the world before it pushed on, all along it.
                if (part == PartSweep)
                {
                    SlashArc(slash, out _, out float through);
                    Vector2 ahead = way.RotatedBy(MathHelper.PiOver2 * Math.Sign(through));
                    float speed = Math.Min(1f, 0.4f + 0.6f * (float)Math.Sin(MathHelper.Pi * timer / BladeSweepTicks));
                    EmpressWarp.Kink(palm + way * bladeLength * 0.55f, way, 90f, bladeLength * 0.6f, 18f * speed, ahead);
                }
            }

            // The cut: the world either side of it slid apart along it, healing; glints along it.
            float left = CutLeft();

            if (left > 0.003f)
            {
                int pieces = Math.Clamp((int)(Math.Abs(cutThrough) * cutReach / 130f), 2, 40);
                float strength = (cutLast ? 16f : 11f) * left * left;

                for (int k = 0; k < pieces; k++)
                {
                    Vector2 a = cutAt + (cutFrom + cutThrough * k / pieces).ToRotationVector2() * cutReach;
                    Vector2 b = cutAt + (cutFrom + cutThrough * (k + 1) / pieces).ToRotationVector2() * cutReach;
                    EmpressWarp.Shear(a, b, 48f, strength * Math.Sign(cutThrough));
                }

                UnifiedRandomish dice = new UnifiedRandomish((int)cutMade);

                for (int k = 0; k < 10; k++)
                {
                    float s = dice.Next();
                    Vector2 at = cutAt + (cutFrom + cutThrough * s).ToRotationVector2() * cutReach;
                    float twinkle = Math.Max(0f, (float)Math.Sin(time * 0.15f + k * 1.9f));
                    EmpressArt.DrawStar(spriteBatch, at, 40f * left + 10f, time * 0.04f + k, EmpressArt.Additive(Color.White, 0.8f * left * twinkle));
                }
            }
        }
    }
}
