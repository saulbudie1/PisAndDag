using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the aurora curtains (Legends act, chosen from those offered: "Aurora Curtains") -----------------------------
    //
    //     CALL    50   she raises her arms; the curtains stir faint at both sides of the arena, and the way through
    //                  is shown where the player is: two thin lines, a little light between
    //     CLOSE   60   the curtains sweep in from both sides, the whole height of the arena, to the edges of the gap
    //     HOLD   240   the gap drifts one way and back, and narrows (from 28 tiles to 16); stay in it
    //     OPEN    50   the curtains draw back to the sides and are gone
    //
    // A curtain hurts wherever it hangs, top to bottom (55 in a normal world, 36 in an expert one, doubled as the game
    // doubles any hostile shot). Folds of light one over another up the arena, each a wavering green foot rising
    // through blue to violet, rays up from it; brighter toward the gap. Gone after, by request, as everything of her
    // acts.
    //
    // Prettier, by request ("make every attack more pretty and aesthetics with some weird brain tickling sound
    // effect"): the air along the curtains' inner edges shimmering (EmpressWarp); sparkles drifting up through them;
    // whispers as they sweep in and draw back, bells as they go, glass tinkling and a slow wavering while they hold.

    public partial class EmpressOfLight
    {
        private const int AuroraCall = 0;
        private const int AuroraClose = 1;
        private const int AuroraHold = 2;
        private const int AuroraOpen = 3;

        private const int CallTicks = 50;
        private const int CurtainCloseTicks = 60;
        private const int HoldTicksAurora = 240;
        private const int OpenTicks = 50;

        /// <summary>The gap: how wide as it closes and at the last, how far it drifts each way, how long a drift takes.</summary>
        private const float GapWide = 440f;
        private const float GapNarrow = 256f;
        private const float GapDrift = 330f;
        private const float DriftTicks = 260f;

        private const int CurtainNormal = 55;
        private const int CurtainExpert = 36;

        private static readonly Color AuroraFoot = new Color(90, 255, 150);
        private static readonly Color AuroraMid = new Color(120, 220, 255);
        private static readonly Color AuroraTop = new Color(230, 110, 255);

        private float ArenaLeft => arenaCentre.X - EmpressArena.Radius;
        private float ArenaRight => arenaCentre.X + EmpressArena.Radius;

        /// <summary>The gap where the player is (well in from the sides: its whole swing, both ways), drifting one way first, from some way into its swing.</summary>
        private void PlanAurora(NPC npc, Player target)
        {
            float margin = GapWide * 0.5f + GapDrift * 2f + 80f;
            float x = ArenaLeft + margin < ArenaRight - margin ? MathHelper.Clamp(target.Center.X, ArenaLeft + margin, ArenaRight - margin) : arenaCentre.X;

            actAt = new Vector2(x, arenaCentre.Y);
            actSide = Main.rand.NextBool() ? 1 : -1;
            actTurn = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        /// <summary>The gap's middle and half its width, this tick.</summary>
        private void Gap(out float middle, out float half)
        {
            float hold = beat == AuroraHold ? timer : beat == AuroraOpen ? HoldTicksAurora : 0f;
            float drift = GapDrift * actSide * ((float)Math.Sin(MathHelper.TwoPi * hold / DriftTicks + actTurn) - (float)Math.Sin(actTurn));

            middle = actAt.X + drift;
            half = MathHelper.Lerp(GapWide, GapNarrow, Ease(hold / HoldTicksAurora)) * 0.5f;
        }

        /// <summary>How far in the curtains have come, 0 (at the sides) to 1 (at the gap's edges).</summary>
        private float CurtainsIn() => beat switch
        {
            AuroraCall => 0f,
            AuroraClose => Ease(timer / (float)CurtainCloseTicks),
            AuroraHold => 1f,
            _ => 1f - Ease(timer / (float)OpenTicks),
        };

        /// <summary>Where the curtains' inner edges are: the left one's right edge and the right one's left edge.</summary>
        private void CurtainEdges(out float leftEdge, out float rightEdge)
        {
            Gap(out float middle, out float half);
            float into = CurtainsIn();

            leftEdge = MathHelper.Lerp(ArenaLeft, middle - half, into);
            rightEdge = MathHelper.Lerp(ArenaRight, middle + half, into);
        }

        private void RunAurora(NPC npc, Player who, bool entered)
        {
            int t = timer;
            Gap(out float middle, out _);

            // Over the gap, high in the arena, her arms up.
            FlyTo(npc, new Vector2(middle, arenaCentre.Y - EmpressArena.Radius * 0.62f), 10f, 0.06f);

            switch (beat)
            {
                case AuroraCall:
                    if (entered)
                    {
                        Sound(SoundID.Item165, npc.Center, 0.3f, 0.8f);
                        Sound(SoundID.Item4, who.Center, -0.4f, 0.6f);
                        Sound(SoundID.Item25, who.Center, 0.7f, 0.5f);
                        Sound(EmpressSounds.Hum, who.Center, 0.55f, 0.55f, 2);
                    }

                    if (Authority && t >= CallTicks)
                        Next(npc, AuroraClose);

                    break;

                case AuroraClose:
                    if (entered)
                    {
                        Hitbox(npc, EmpressHitbox.Shape.Curtains, npc.Center, 0f, Hurt(npc, CurtainNormal, CurtainExpert), CurtainCloseTicks + HoldTicksAurora + OpenTicks + 10);

                        if (!Main.dedServ)
                        {
                            Sound(SoundID.Item163, who.Center, 0.2f, 0.8f);
                            Sound(SoundID.Item9, who.Center + new Vector2(-600f, 0f), -0.7f, 0.8f);
                            Sound(SoundID.Item9, who.Center + new Vector2(600f, 0f), -0.5f, 0.8f);
                            Sound(SoundID.Item15, who.Center, -0.6f, 0.6f);
                            Sound(EmpressSounds.WhooshUp, who.Center + new Vector2(-500f, 0f), -0.2f, 0.8f, 3);
                            Sound(EmpressSounds.WhooshUp, who.Center + new Vector2(500f, 0f), 0.1f, 0.8f, 3);
                            Sound(EmpressSounds.Shock, who.Center, -0.5f, 0.4f, 2);
                            Scream(npc, 0.1f, 0.75f);
                        }
                    }

                    if (Authority && t >= CurtainCloseTicks)
                        Next(npc, AuroraHold);

                    break;

                case AuroraHold:
                    if (t % 40 == 0)
                        Sound(SoundID.Item15, who.Center, -0.2f + 0.1f * (t / 40), 0.35f);

                    // The curtains shimmering: high glints now and then, either side; glass tinkling between; a slow wavering.
                    if (!Main.dedServ && t % 25 == 12)
                        Sound(Main.rand.NextBool() ? SoundID.Item4 : SoundID.Item29, who.Center + new Vector2(Main.rand.NextBool() ? -300f : 300f, 0f), Main.rand.NextFloat(0.4f, 0.9f), 0.25f);

                    if (!Main.dedServ && t % 25 == 0)
                        Sound(EmpressSounds.Tingle, who.Center + new Vector2(Main.rand.NextBool() ? -260f : 260f, Main.rand.NextFloat(-120f, 120f)), Main.rand.NextFloat(-0.2f, 0.6f), 0.35f);

                    if (!Main.dedServ && t % 110 == 30)
                        Sound(EmpressSounds.Wobble, who.Center, 0.1f * (t / 110), 0.3f, 2);

                    if (Authority && t >= HoldTicksAurora)
                        Next(npc, AuroraOpen);

                    break;

                case AuroraOpen:
                    if (entered)
                    {
                        Sound(SoundID.Item9, who.Center, -0.4f, 0.6f);
                        Sound(SoundID.Item4, who.Center, 0.4f, 0.5f);
                        Sound(EmpressSounds.WhooshDown, who.Center, 0.1f, 0.7f, 2);
                        Sound(EmpressSounds.Chime, who.Center, 0.5f, 0.4f, 2);
                    }

                    if (Authority && t >= OpenTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The curtains' hitbox asks: is this body in either curtain?</summary>
        internal bool CurtainsHit(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Aurora || beat == AuroraCall)
                return false;

            CurtainEdges(out float leftEdge, out float rightEdge);

            // A little in from the curtains' drawn edges, so a grazing pixel of light does not count.
            return box.Left < leftEdge - 6f || box.Right > rightEdge + 6f;
        }

        private void DrawAuroraHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float up = beat == AuroraCall ? Ease(timer / 26f) : beat == AuroraOpen ? 1f - Ease(timer / (float)OpenTicks) : 1f;
            float left = MathHelper.Lerp(MathHelper.Pi - 1.1f, MathHelper.Pi + 1.1f, up);
            float right = MathHelper.Lerp(1.1f, -1.1f, up);

            DrawHerActing(spriteBatch, npc, screenPos, Arms.At(left, right), 0f, 0.35f + 0.2f * up);
        }

        // ---- the curtains drawn ---------------------------------------------------------------------------------
        //
        // A curtain is the arena's whole height (285 tiles) and the screen shows a fifth of it, so its look is made at
        // the screen's size: a faint wash all the way up and down (nowhere in it looks safe), and over it folds, one
        // every 45 tiles up, each a bright green foot wavering across it, its light rising from it through blue to
        // violet and gone before the next; rays up from each foot, flickering; the folds rising slowly. Brighter
        // toward the gap. Only what is on the screen is drawn.

        /// <summary>The folds: one every this far up; how far up a fold's light goes; how fast they rise (px a tick).</summary>
        private const float FoldHeight = 720f;
        private const float FoldReach = 640f;
        private const float FoldRise = 0.35f;

        /// <summary>The soft line's brightest row (EmpressArt.Line): a band laid along it takes its vertex colours alone.</summary>
        private const float FlatRow = 16.5f / 32f;

        /// <summary>The arena's top (it lies with an edge along its top).</summary>
        private float ArenaTop => arenaCentre.Y - EmpressArena.Radius * Decagon.Apothem;

        /// <summary>The part of the world on the screen, with a margin.</summary>
        private static void Seen(out float left, out float right, out float top, out float bottom)
        {
            Vector2 middle = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 half = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f / Math.Max(Main.GameViewMatrix.Zoom.X, 0.25f) + new Vector2(200f);

            left = middle.X - half.X;
            right = middle.X + half.X;
            top = middle.Y - half.Y;
            bottom = middle.Y + half.Y;
        }

        /// <summary>How bright a curtain is at x: rippling across, brighter toward its inner edge.</summary>
        private static float Shine(float x, float inner, float time)
        {
            float edge = Math.Abs(x - inner);
            float ripple = 0.72f + 0.16f * (float)Math.Sin(x * 0.011f + time * 0.045f) + 0.12f * (float)Math.Sin(x * 0.027f - time * 0.07f);

            return MathHelper.Clamp(ripple * (1f + 1.4f * (float)Math.Exp(-edge / 70f)), 0f, 2f);
        }

        /// <summary>Where fold k's bright foot is at x, wavering; the folds counted from the arena's top, rising.</summary>
        private float FoldFoot(int k, float x, float time)
        {
            float line = ArenaTop + k * FoldHeight - time * FoldRise;
            return line + 34f * (float)Math.Sin(x * 0.0047f + time * 0.018f + k * 1.9f) + 16f * (float)Math.Sin(x * 0.013f - time * 0.031f + k);
        }

        /// <summary>The folds that show between these heights.</summary>
        private void Folds(float time, float seenTop, float seenBottom, out int first, out int last)
        {
            float rise = time * FoldRise;

            first = (int)Math.Floor((seenTop - 80f - ArenaTop + rise) / FoldHeight);
            last = Math.Min(first + 6, (int)Math.Ceiling((seenBottom + FoldReach + 50f - ArenaTop + rise) / FoldHeight));
        }

        /// <summary>A number from 0 to 1 for a place and a fold, the same every frame.</summary>
        private static float Hash(float x, int k)
        {
            double v = Math.Sin(x * 12.9898 + k * 78.233) * 43758.5453;
            return (float)(v - Math.Floor(v));
        }

        /// <summary>How strongly the curtains show: faint as she calls them, full, going as they open.</summary>
        private float CurtainStrength() => beat == AuroraCall ? 0.25f * Ease(timer / (float)CallTicks) : beat == AuroraOpen ? 1f - Ease(timer / (float)OpenTicks) : 1f;

        /// <summary>Where the curtains' inner edges are drawn: stirring at the sides as she calls them, then where they are.</summary>
        private void DrawnEdges(out float leftEdge, out float rightEdge)
        {
            CurtainEdges(out leftEdge, out rightEdge);

            if (beat == AuroraCall)
            {
                leftEdge = ArenaLeft + 160f;
                rightEdge = ArenaRight - 160f;
            }
        }

        /// <summary>The two curtains' light (Strip draws it: no batch open).</summary>
        private void DrawAuroraBands(NPC npc)
        {
            if (!EmpressArt.Ready(EmpressArt.Line))
                return;

            DrawnEdges(out float leftEdge, out float rightEdge);
            Seen(out float seenLeft, out float seenRight, out float seenTop, out float seenBottom);

            float strength = CurtainStrength();
            float time = (float)Main.timeForVisualEffects;

            Curtain(Math.Max(ArenaLeft, seenLeft), Math.Min(leftEdge, seenRight), leftEdge, strength, time, seenTop, seenBottom);
            Curtain(Math.Max(rightEdge, seenLeft), Math.Min(ArenaRight, seenRight), rightEdge, strength, time, seenTop, seenBottom);
        }

        /// <summary>One curtain's light from x to x (what is seen of it), its inner edge at <paramref name="inner"/>.</summary>
        private void Curtain(float from, float to, float inner, float strength, float time, float seenTop, float seenBottom)
        {
            if (to - from < 4f || strength <= 0.003f)
                return;

            int steps = Math.Clamp((int)((to - from) / 14f), 2, 240);

            // The wash.
            Strip.Begin();

            int laid = 0;

            for (int i = 0; i <= steps; i++)
            {
                float x = MathHelper.Lerp(from, to, i / (float)steps);

                if (!Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                    continue;

                float a = Math.Max(top, seenTop);
                float b = Math.Max(a, Math.Min(bottom, seenBottom));
                float shine = Shine(x, inner, time) * strength;
                Color wash = EmpressArt.Additive(Color.Lerp(AuroraTop, AuroraMid, 0.5f + 0.5f * (float)Math.Sin(x * 0.004f + time * 0.01f)), 0.13f * shine);

                Strip.Pair(new Vector2(x, a), new Vector2(x, b), wash, wash, i / (float)steps, FlatRow, FlatRow);
                laid++;
            }

            if (laid >= 2)
                Strip.Draw(EmpressArt.Line.Value, SamplerState.LinearClamp);

            // The folds: the foot's short fall below it, then up from it, green, blue, violet, gone.
            Folds(time, seenTop, seenBottom, out int first, out int last);

            Color foot = EmpressArt.Additive(AuroraFoot, 0.78f);
            Color mid = EmpressArt.Additive(AuroraMid, 0.42f);
            Color crown = EmpressArt.Additive(AuroraTop, 0.26f);

            for (int k = first; k <= last; k++)
            {
                FoldBand(k, from, to, steps, inner, strength, time, 0f, 30f, foot, Color.Transparent);
                FoldBand(k, from, to, steps, inner, strength, time, -150f, 0f, mid, foot);
                FoldBand(k, from, to, steps, inner, strength, time, -380f, -150f, crown, mid);
                FoldBand(k, from, to, steps, inner, strength, time, -FoldReach, -380f, Color.Transparent, crown);
            }
        }

        /// <summary>
        /// One band of fold k across a curtain, from <paramref name="up"/> to <paramref name="down"/> about its foot (up is
        /// less), its colour at each, kept inside the arena.
        /// </summary>
        private void FoldBand(int k, float from, float to, int steps, float inner, float strength, float time, float up, float down, Color upColour, Color downColour)
        {
            Strip.Begin();

            int laid = 0;

            for (int i = 0; i <= steps; i++)
            {
                float x = MathHelper.Lerp(from, to, i / (float)steps);

                if (!Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                    continue;

                float foot = FoldFoot(k, x, time);
                float y1 = foot + up;
                float y2 = foot + down;
                float a = MathHelper.Clamp(y1, top, bottom);
                float b = MathHelper.Clamp(y2, top, bottom);
                float shine = Shine(x, inner, time) * strength;

                Color first = Color.Lerp(upColour, downColour, (a - y1) / (y2 - y1)) * shine;
                Color second = Color.Lerp(upColour, downColour, (b - y1) / (y2 - y1)) * shine;

                Strip.Pair(new Vector2(x, a), new Vector2(x, b), first, second, i / (float)steps, FlatRow, FlatRow);
                laid++;
            }

            if (laid >= 2)
                Strip.Draw(EmpressArt.Line.Value, SamplerState.LinearClamp);
        }

        /// <summary>Over the curtains: their rays, their bright inner edges, and the way through shown as she calls them.</summary>
        private void DrawAuroraOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Gap(out float middle, out float half);

            if (beat == AuroraCall)
            {
                float show = Ease(timer / 20f);

                for (int side = -1; side <= 1; side += 2)
                {
                    float x = middle + side * half;

                    if (Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                    {
                        for (float y = top; y < bottom; y += 48f)
                            EmpressArt.DrawLine(spriteBatch, new Vector2(x, y), new Vector2(x, Math.Min(bottom, y + 26f)), 3f, EmpressArt.Additive(AuroraFoot, 0.7f * show));
                    }
                }
            }

            DrawnEdges(out float leftEdge, out float rightEdge);
            Seen(out float seenLeft, out float seenRight, out float seenTop, out float seenBottom);
            float strength = CurtainStrength();

            FoldRays(spriteBatch, Math.Max(ArenaLeft, seenLeft), Math.Min(leftEdge, seenRight), leftEdge, strength, time, seenTop, seenBottom);
            FoldRays(spriteBatch, Math.Max(rightEdge, seenLeft), Math.Min(ArenaRight, seenRight), rightEdge, strength, time, seenTop, seenBottom);

            if (beat == AuroraCall)
                return;

            // The inner edges: lines of light down the whole height, the air along them shimmering.
            foreach (float x in new[] { leftEdge, rightEdge })
            {
                if (!Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                    continue;

                EmpressArt.DrawLine(spriteBatch, new Vector2(x, top), new Vector2(x, bottom), 22f, EmpressArt.Additive(AuroraMid, 0.35f * strength));
                EmpressArt.DrawLine(spriteBatch, new Vector2(x, top), new Vector2(x, bottom), 5f, EmpressArt.Additive(Color.White, 0.8f * strength));

                float from = Math.Max(top, seenTop);
                float to = Math.Min(bottom, seenBottom);

                if (to > from)
                    EmpressWarp.Haze(new Vector2(x, from), new Vector2(x, to), 90f, 3f * strength, 220f, x * 0.01f);
            }

            // Sparkles drifting up through the curtains.
            DrawAuroraSparkles(spriteBatch, Math.Max(ArenaLeft, seenLeft), Math.Min(leftEdge, seenRight), strength, time, seenTop, seenBottom);
            DrawAuroraSparkles(spriteBatch, Math.Max(rightEdge, seenLeft), Math.Min(ArenaRight, seenRight), strength, time, seenTop, seenBottom);
        }

        /// <summary>Sparkles in a curtain's seen part, drifting up slowly and glinting, each with its place across it.</summary>
        private void DrawAuroraSparkles(SpriteBatch spriteBatch, float from, float to, float strength, float time, float seenTop, float seenBottom)
        {
            const float Every = 64f;

            if (to - from < 4f || strength <= 0.01f)
                return;

            float height = seenBottom - seenTop;

            for (float x0 = (float)Math.Floor(from / Every) * Every; x0 < to; x0 += Every)
            {
                for (int n = 0; n < 3; n++)
                {
                    float h = Hash(x0, n + 40);
                    float x = x0 + h * Every;

                    if (x < from || x > to || !Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                        continue;

                    float y = seenBottom - ((Hash(x0 + 3.7f, n) * height + time * (0.4f + 0.5f * h)) % height);

                    if (y < top || y > bottom)
                        continue;

                    float glint = 0.5f + 0.5f * (float)Math.Sin(time * (0.06f + 0.05f * h) + h * 30f);
                    Color colour = Color.Lerp(AuroraFoot, AuroraTop, Hash(x0 + 9.1f, n));

                    EmpressArt.DrawStar(spriteBatch, new Vector2(x, y), 20f * glint + 6f, time * 0.02f + h * 6f, EmpressArt.Additive(Color.White, 0.75f * glint * strength));
                    EmpressArt.DrawBloom(spriteBatch, new Vector2(x, y), 16f, EmpressArt.Additive(colour, 0.6f * strength));
                }
            }
        }

        /// <summary>Rays up from each fold's foot, one about every 22 px, flickering: green, then blue going up, a violet tip.</summary>
        private void FoldRays(SpriteBatch spriteBatch, float from, float to, float inner, float strength, float time, float seenTop, float seenBottom)
        {
            const float Every = 22f;

            if (to - from < 4f || strength <= 0.003f)
                return;

            Folds(time, seenTop, seenBottom, out int first, out int last);

            for (int k = first; k <= last; k++)
            {
                for (float x0 = (float)Math.Floor(from / Every) * Every; x0 < to; x0 += Every)
                {
                    float h = Hash(x0, k);
                    float x = x0 + h * Every * 0.7f;

                    if (x < from || x > to || !Decagon.Upright(x, arenaCentre, EmpressArena.Radius, out float top, out float bottom))
                        continue;

                    float foot = FoldFoot(k, x, time);
                    float height = 220f + 340f * Hash(x0 + 7.31f, k);
                    float flicker = 0.5f + 0.5f * (float)Math.Sin(time * (0.05f + 0.07f * h) + h * 40f);
                    float shine = Shine(x, inner, time) * strength * flicker;
                    float width = 3f + 4f * h;

                    float low = MathHelper.Clamp(foot - 4f, top, bottom);
                    float mid = MathHelper.Clamp(foot - height * 0.45f, top, bottom);

                    if (low - mid > 1f)
                        EmpressArt.DrawLine(spriteBatch, new Vector2(x, low), new Vector2(x, mid), width, EmpressArt.Additive(AuroraFoot, 0.3f * shine));

                    float rest = Math.Min(height * 0.55f, mid - top);
                    float tip = Math.Min(height * 0.9f, mid - top);

                    if (rest > 1f)
                        EmpressArt.DrawRay(spriteBatch, new Vector2(x, mid), Up, rest, width, EmpressArt.Additive(AuroraMid, 0.3f * shine));

                    if (tip > 1f)
                        EmpressArt.DrawRay(spriteBatch, new Vector2(x, mid), Up, tip, width * 1.6f, EmpressArt.Additive(AuroraTop, 0.12f * shine));
                }
            }
        }
    }
}
