using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// A line said out loud in the middle of something, not in a box.
    ///
    /// The Dialogue panel is for talking to someone. A bark is someone talking at the
    /// world while they are busy: it comes up over their head, a few words, faintly
    /// lit, and goes on its own. Nothing to answer, nothing held, and it follows the
    /// speaker wherever they move while it is up. One at a time per speaker: a new line
    /// replaces the one still showing.
    ///
    /// It fades in over <see cref="FadeIn"/> ticks while rising a few pixels into place,
    /// sits for as long as the words need, and fades out over <see cref="FadeOut"/>. The
    /// glow is a soft warm halo laid round the letters before them, breathing slowly,
    /// with a dark rim under it so the words read against a bright sky as well as in a
    /// cave.
    ///
    /// Drawn in the world, in the speaker's own draw (<see cref="Draw"/>), above a point
    /// the speaker gives (their head, wherever their rig has it). Local: nothing here is
    /// sent, and each machine rolls its own lines.
    ///
    /// (Dame Quilt, who spoke this way, now speaks at the foot of the screen instead: Taunt. This stays for
    /// whoever wants a line over a head.)
    /// </summary>
    public static class Bark
    {
        /// <summary>Ticks to come up, and to go.</summary>
        public const int FadeIn = 14;
        public const int FadeOut = 22;

        /// <summary>How long a line stays up at least, and per letter on top of that.</summary>
        private const int HoldBase = 70;
        private const int HoldPerLetter = 3;

        /// <summary>How far it rises into place as it comes up, in pixels.</summary>
        private const float Rise = 8f;

        private const float TextScale = 0.95f;

        /// <summary>The words: a warm cream. The halo: gold, faint.</summary>
        private static readonly Color Ink = new Color(255, 242, 214);
        private static readonly Color Halo = new Color(255, 196, 92);

        /// <summary>The halo's strength at full, and how much it breathes either way of that.</summary>
        private const float HaloStrength = 0.16f;
        private const float HaloBreath = 0.05f;

        private sealed class Line
        {
            public int Type;
            public string Text;
            public Color Colour;
            public int Age;
            public int Life;
        }

        /// <summary>By the speaker's whoAmI.</summary>
        private static readonly Dictionary<int, Line> lines = new();

        /// <summary>
        /// This NPC says this. <paramref name="colour"/> tints the words (cream if null);
        /// <paramref name="hold"/> is how long it sits at full, in ticks (worked out from its
        /// length if not given).
        /// </summary>
        public static void Say(NPC npc, string text, Color? colour = null, int hold = -1)
        {
            if (Main.dedServ || npc == null || !npc.active || string.IsNullOrEmpty(text))
                return;

            if (hold < 0)
                hold = HoldBase + HoldPerLetter * text.Length;

            lines[npc.whoAmI] = new Line
            {
                Type = npc.type,
                Text = text,
                Colour = colour ?? Ink,
                Age = 0,
                Life = FadeIn + hold + FadeOut,
            };
        }

        /// <summary>Whether this NPC has a line up right now.</summary>
        public static bool Busy(NPC npc) => npc != null && lines.TryGetValue(npc.whoAmI, out Line line) && line.Type == npc.type;

        /// <summary>Takes this NPC's line down at once.</summary>
        public static void Hush(NPC npc)
        {
            if (npc != null)
                lines.Remove(npc.whoAmI);
        }

        private static readonly List<int> done = new();

        /// <summary>Once a tick: lines age, and the finished ones and the ones whose speaker is gone come down.</summary>
        internal static void Update()
        {
            done.Clear();

            foreach (KeyValuePair<int, Line> pair in lines)
            {
                NPC npc = Main.npc[pair.Key];
                Line line = pair.Value;

                line.Age++;

                if (line.Age >= line.Life || !npc.active || npc.type != line.Type)
                    done.Add(pair.Key);
            }

            foreach (int key in done)
                lines.Remove(key);
        }

        internal static void Clear() => lines.Clear();

        /// <summary>
        /// This NPC's line, if it has one, with its foot centred on <paramref name="above"/>
        /// (a world point: the top of their head, or a little over it). From the speaker's
        /// PostDraw, inside the world's sprite batch.
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch, NPC npc, Vector2 above, Vector2 screenPos)
        {
            if (npc == null || !lines.TryGetValue(npc.whoAmI, out Line line) || line.Type != npc.type)
                return;

            float alpha;
            float lift;

            if (line.Age < FadeIn)
            {
                float t = line.Age / (float)FadeIn;
                alpha = t * t * (3f - 2f * t);
                lift = Rise * (1f - alpha);
            }
            else if (line.Age > line.Life - FadeOut)
            {
                float t = (line.Life - line.Age) / (float)FadeOut;
                alpha = MathHelper.Clamp(t, 0f, 1f);
                lift = 0f;
            }
            else
            {
                alpha = 1f;
                lift = 0f;
            }

            if (alpha <= 0.01f)
                return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(line.Text) * TextScale;

            // Foot on the point, centred, whole pixels so the letters do not swim.
            Vector2 topLeft = above - screenPos + new Vector2(-size.X * 0.5f, -size.Y + lift);
            topLeft = new Vector2((float)Math.Round(topLeft.X), (float)Math.Round(topLeft.Y));

            Vector2 scale = new Vector2(TextScale);

            // The dark rim, one pixel round, under everything.
            Color rim = Color.Black * (0.55f * alpha);

            for (int i = 0; i < 4; i++)
            {
                Vector2 off = new Vector2(i == 0 ? -1 : i == 1 ? 1 : 0, i == 2 ? -1 : i == 3 ? 1 : 0);
                spriteBatch.DrawString(font, line.Text, topLeft + off, rim, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            // The halo: the words again, faint and warm, eight times round at two pixels out.
            float breath = HaloStrength + HaloBreath * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + npc.whoAmI);
            Color glow = Halo * (breath * alpha);
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 off = new Vector2(2f, 0f).RotatedBy(MathHelper.TwoPi * i / 8f);
                spriteBatch.DrawString(font, line.Text, topLeft + off, glow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.DrawString(font, line.Text, topLeft, line.Colour * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Ages the barks once a tick, and clears them with the world.</summary>
    public class BarkSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            if (!Main.dedServ)
                Bark.Update();
        }

        public override void OnWorldUnload() => Bark.Clear();

        public override void Unload() => Bark.Clear();
    }
}
