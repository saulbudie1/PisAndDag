using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// What a boss says in the middle of its fight (by request: "a system that is only a line and underlined like
    /// elden ring and it auto skips after a while depending on the text").
    ///
    /// One line at the foot of the screen, over the boss bar and its name: no box, no name, no portrait, nothing to
    /// answer and nothing to click. The words in a pale cream on a soft shadow; under them a thin rule in the
    /// speaker's colours (one in the middle, another at the ends), brightest in the middle and gone at its ends,
    /// drawn out from the middle as the words come up. It stays as long as its words take to read (HoldBase, and
    /// HoldPerLetter more for every letter, within HoldLeast and HoldMost), and fades. A new line takes the place of
    /// one still up, that one going quickly; a speech of several lines says them one after another.
    ///
    /// Drawn ahead of "Vanilla: Hide UI Toggle", as the boss bar is (BossPlate), so it stays through a cutscene's
    /// hiding of the interface; the player's own F11 still takes it away. Only for whoever is near enough to hear
    /// it when it is said. Local: nothing is sent; each machine says it as its own copy of the fight comes to it.
    ///
    ///     Taunt.Say(NPC, "Be still.", EmpressArt.Pink, EmpressArt.Sky);
    ///     Taunt.Speech(NPC, new[] { "Not broken yet.", "Then enough play." }, EmpressArt.Pink);
    /// </summary>
    public static class Taunt
    {
        /// <summary>Ticks to come up, and to go; and to go when another line takes its place.</summary>
        public const int FadeIn = 12;
        public const int FadeOut = 24;
        private const int Leave = 10;

        /// <summary>Ticks between one line of a speech going and the next coming.</summary>
        private const int Between = 8;

        /// <summary>How long a line stays up: at least this, this more for every letter, and never more than this.</summary>
        private const int HoldBase = 60;
        private const int HoldPerLetter = 4;
        private const int HoldLeast = 100;
        private const int HoldMost = 420;

        /// <summary>How far whoever speaks can be heard, if they say nothing of it (in pixels).</summary>
        public const float DefaultReach = 2400f;

        /// <summary>The rule's height off the foot of the screen: clear of the boss bar and the name over it.</summary>
        private const float RuleFromBottom = 116f;

        /// <summary>The words' foot over the rule; the rule past the words at each end; how thick it is.</summary>
        private const float TextOverRule = 2f;
        private const float RuleOverhang = 56f;
        private const int RuleThickness = 2;

        /// <summary>How much of the rule, from its middle, is at full strength before it starts to go.</summary>
        private const float RuleFull = 0.5f;

        /// <summary>How far the words rise into place as they come up, in pixels.</summary>
        private const float Rise = 6f;

        private const float TextScale = 1f;

        /// <summary>A line is never wider than this much of the screen: a long one is drawn smaller.</summary>
        private const float WidestShare = 0.9f;

        /// <summary>The words: a pale cream, the same for everyone. The colour is the speaker's, in the rule.</summary>
        private static readonly Color Ink = new Color(250, 246, 238);

        private sealed class Line
        {
            public string Text;
            public Color Near;
            public Color Far;
            public int Speaker = -1;
            public int SpeakerType;
            public int Age;
            public int Hold;

            /// <summary>
            /// Ticks it has been going since another took its place, or -1; and how strong it was when that began, how far
            /// its rule had drawn out and how far below its place it still was (it goes from there, as it was).
            /// </summary>
            public int Leaving = -1;
            public float LeftAt;
            public float LeftGrown;
            public float LeftLift;

            public int Life => FadeIn + Hold + FadeOut;
        }

        private static Line current;

        /// <summary>Lines still going because others took their place (more than one, if lines come fast).</summary>
        private static readonly List<Line> leaving = new();

        /// <summary>The rest of a speech, to come after the line up now, and the wait before the next of it.</summary>
        private static readonly Queue<Line> next = new();
        private static int between;

        /// <summary>How long a line of these words stays up at full (its fades not counted).</summary>
        public static int HoldFor(string text) => Math.Clamp(HoldBase + HoldPerLetter * (text?.Length ?? 0), HoldLeast, HoldMost);

        /// <summary>How long these lines take, said one after another, from the first coming up to the last gone.</summary>
        public static int TicksFor(string[] lines)
        {
            int total = 0;

            if (lines == null)
                return 0;

            foreach (string text in lines)
                total += FadeIn + HoldFor(text) + FadeOut + Between;

            return total;
        }

        /// <summary>Whether anything is being said right now.</summary>
        public static bool Speaking => current != null || next.Count > 0;

        /// <summary>
        /// <paramref name="speaker"/> says this, in its colours: <paramref name="near"/> in the middle of the rule,
        /// <paramref name="far"/> at its ends (the same if not given). Heard within <paramref name="reach"/> of them
        /// (anywhere, if it is not more than nothing). False if this machine's player is too far off to hear.
        /// </summary>
        public static bool Say(NPC speaker, string text, Color near, Color? far = null, float reach = DefaultReach)
            => Speech(speaker, new[] { text }, near, far, reach);

        /// <summary>As <see cref="Say"/>, a speech of several lines, one after another; it takes the place of whatever was being said.</summary>
        public static bool Speech(NPC speaker, string[] lines, Color near, Color? far = null, float reach = DefaultReach)
        {
            if (Main.dedServ || Main.gameMenu || lines == null || lines.Length == 0)
                return false;

            if (speaker != null && reach > 0f && Vector2.Distance(Main.LocalPlayer.Center, speaker.Center) > reach)
                return false;

            List<Line> speech = new();

            foreach (string text in lines)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                speech.Add(new Line
                {
                    Text = text,
                    Near = near,
                    Far = far ?? near,
                    Speaker = speaker?.whoAmI ?? -1,
                    SpeakerType = speaker?.type ?? 0,
                    Hold = HoldFor(text),
                });
            }

            // Nothing to say: whatever was being said goes on.
            if (speech.Count == 0)
                return false;

            next.Clear();
            between = 0;

            foreach (Line line in speech)
                next.Enqueue(line);

            // What was up goes quickly, from as strong as it was; the first of these comes up at once.
            StandDown();
            current = next.Dequeue();
            return true;
        }

        /// <summary>Whatever this speaker is saying, taken down now (all of it, if no speaker is given).</summary>
        public static void Hush(NPC speaker = null)
        {
            // Whose the speech is: the line up now, or (between two of its lines) the next.
            Line theirs = current ?? (next.Count > 0 ? next.Peek() : null);

            if (theirs == null)
                return;

            if (speaker != null && (theirs.Speaker != speaker.whoAmI || theirs.SpeakerType != speaker.type))
                return;

            next.Clear();
            StandDown();
        }

        /// <summary>Everything gone at once (the world closing).</summary>
        internal static void Clear()
        {
            current = null;
            leaving.Clear();
            next.Clear();
            between = 0;
        }

        /// <summary>The line up now goes, quickly, from as strong as it is.</summary>
        private static void StandDown()
        {
            if (current == null)
                return;

            float strength = Strength(current);

            if (strength > 0.01f)
            {
                current.LeftGrown = Grown(current);
                current.LeftLift = Lift(current, strength);
                current.LeftAt = strength;
                current.Leaving = 0;
                leaving.Add(current);
            }

            current = null;
        }

        /// <summary>Once a tick: lines age, the finished go, and the next line of a speech comes when its turn is.</summary>
        internal static void Update()
        {
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                if (++leaving[i].Leaving >= Leave)
                    leaving.RemoveAt(i);
            }

            if (current != null && ++current.Age >= current.Life)
            {
                current = null;
                between = Between;
            }

            if (current == null && next.Count > 0 && --between <= 0)
                current = next.Dequeue();
        }

        /// <summary>How strong a line is now: up, at full, going; or going because another took its place.</summary>
        private static float Strength(Line line)
        {
            if (line.Leaving >= 0)
                return line.LeftAt * MathHelper.Clamp(1f - line.Leaving / (float)Leave, 0f, 1f);

            if (line.Age < FadeIn)
                return Smooth(line.Age / (float)FadeIn);

            int left = line.Life - line.Age;

            return left < FadeOut ? MathHelper.Clamp(left / (float)FadeOut, 0f, 1f) : 1f;
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>How far the rule has drawn out from the middle (0 to 1): over twice the words' coming up; as it was, once going.</summary>
        private static float Grown(Line line) => line.Leaving >= 0 ? line.LeftGrown : Smooth(line.Age / (FadeIn * 2f));

        /// <summary>How far below their place the words still are as they come up (at this strength); as they were, once going.</summary>
        private static float Lift(Line line, float strength)
        {
            if (line.Leaving >= 0)
                return line.LeftLift;

            return line.Age < FadeIn ? Rise * (1f - strength) : 0f;
        }

        // ============================================================
        // DRAW
        // ============================================================

        internal static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            // A cutscene's hiding of the interface leaves it up; the player's own F11 does not.
            if (Main.hideUI && !PisPlayer.HidingUI)
                return;

            // Someone talking in the panel has the foot of the screen (until it has quite gone).
            if (Dialogue.Showing)
                return;

            foreach (Line line in leaving)
                DrawLine(spriteBatch, line);

            if (current != null)
                DrawLine(spriteBatch, current);
        }

        private static void DrawLine(SpriteBatch spriteBatch, Line line)
        {
            float alpha = Strength(line);

            if (alpha <= 0.01f)
                return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float scale = TextScale;
            Vector2 size = font.MeasureString(line.Text) * scale;

            float widest = Main.screenWidth * WidestShare - 2f * RuleOverhang;

            if (size.X > widest && size.X > 0f)
            {
                scale *= widest / size.X;
                size *= widest / size.X;
            }

            float middle = Main.screenWidth * 0.5f;
            float ruleY = (float)Math.Round(Main.screenHeight - RuleFromBottom);

            // Coming up, it rises into place; going, it stays where it was.
            float lift = Lift(line, alpha);

            Vector2 topLeft = new Vector2(middle - size.X * 0.5f, ruleY - TextOverRule - size.Y + lift);
            topLeft = new Vector2((float)Math.Round(topLeft.X), (float)Math.Round(topLeft.Y));

            DrawRule(spriteBatch, line, middle, ruleY, size.X * 0.5f + RuleOverhang, alpha);
            DrawWords(spriteBatch, font, line.Text, topLeft, scale, alpha);
        }

        /// <summary>The rule: from the middle out, its two colours, gone at its ends; a faint light about it and a shadow under it.</summary>
        private static void DrawRule(SpriteBatch spriteBatch, Line line, float middle, float y, float half, float alpha)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Drawn out from the middle as the line comes up (and left as it was if it goes before it is out).
            float reach = half * (0.15f + 0.85f * Grown(line));

            if (reach < 2f)
                return;

            const int step = 2;
            int from = (int)Math.Floor(middle - reach);
            int to = (int)Math.Ceiling(middle + reach);
            int top = (int)y;

            for (int x = from; x < to; x += step)
            {
                float u = Math.Abs((x + step * 0.5f - middle) / reach);

                if (u >= 1f)
                    continue;

                // Full through its middle, then going smoothly to nothing at the end.
                float strength = u <= RuleFull ? 1f : Smooth((1f - u) / (1f - RuleFull));
                float a = strength * alpha;

                if (a <= 0.004f)
                    continue;

                Color colour = Color.Lerp(line.Near, line.Far, Smooth(u));

                // Its light, either side of it: added, not laid over.
                Color glow = colour * (0.28f * a);
                glow.A = 0;
                spriteBatch.Draw(pixel, new Rectangle(x, top - 1, step, 1), glow);
                spriteBatch.Draw(pixel, new Rectangle(x, top + RuleThickness, step, 1), glow);

                // A shadow under that, so it reads on a bright sky.
                spriteBatch.Draw(pixel, new Rectangle(x, top + RuleThickness + 1, step, 1), Color.Black * (0.3f * a));

                spriteBatch.Draw(pixel, new Rectangle(x, top, step, RuleThickness), colour * a);
            }
        }

        /// <summary>The words, on a soft shadow: a dark rim close round them and a fainter one further out.</summary>
        private static void DrawWords(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 at, float scale, float alpha)
        {
            Vector2 size = new Vector2(scale);

            Color far = Color.Black * (0.16f * alpha);

            for (int i = 0; i < 8; i++)
            {
                Vector2 off = new Vector2(2f, 0f).RotatedBy(MathHelper.TwoPi * i / 8f);
                spriteBatch.DrawString(font, text, at + off, far, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            }

            Color near = Color.Black * (0.45f * alpha);

            spriteBatch.DrawString(font, text, at + new Vector2(-1f, 0f), near, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, at + new Vector2(1f, 0f), near, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, at + new Vector2(0f, -1f), near, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, at + new Vector2(0f, 1f), near, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, at + new Vector2(0f, 2f), near, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);

            spriteBatch.DrawString(font, text, at, Ink * alpha, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Ages the lines once a tick, clears them with the world, and hangs them off the interface ahead of the F11 toggle, as the boss bar is.</summary>
    public class TauntSystem : ModSystem
    {
        // On a frame the bars are drawn over everything (her coming hides the interface), they would cover the words
        // drawn by the layer below: those frames the words are drawn after the bars instead (Cinematic.OverBars).
        public override void Load()
        {
            if (!Main.dedServ)
                Cinematic.OverBars += Taunt.Draw;
        }

        public override void PostUpdateEverything()
        {
            if (!Main.dedServ)
                Taunt.Update();
        }

        public override void OnWorldUnload() => Taunt.Clear();

        public override void Unload()
        {
            Cinematic.OverBars -= Taunt.Draw;
            Taunt.Clear();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Hide UI Toggle");

            // As the boss bar does: Mouse Text is always there, if the toggle is ever renamed.
            if (index == -1)
                index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Taunt",
                () =>
                {
                    if (!Cinematic.BarsOverEverything)
                        Taunt.Draw(Main.spriteBatch);

                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}
