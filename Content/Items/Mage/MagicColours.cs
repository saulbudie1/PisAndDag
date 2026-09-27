using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.UI.Chat;

namespace PisAndDag.Content.Items.Mage
{
    /// <summary>
    /// What the magic words are coloured. One table here rather than a colour beside
    /// every place a name is printed, so an element reads the same in a tooltip, in the
    /// arsenal and anywhere else it is named.
    ///
    /// Each entry is a fill and a stroke. Vanilla's own text shadow is always black, so
    /// anything with a coloured stroke is drawn by hand: the shadow pass in the stroke
    /// colour, then the text over it.
    ///
    /// An element can also have a stroke that is not one colour (<see cref="StrokeRamps"/>):
    /// it runs from its first colour at the bottom of the letters to its last at the top,
    /// all of them showing at once. A sprite is drawn in one colour, so that stroke is
    /// drawn a row of each letter at a time, each row in its own colour.
    /// </summary>
    public static class MagicColours
    {
        /// <summary>Spell or incantation, the word itself.</summary>
        public static readonly Dictionary<CastKind, (Color fill, Color stroke)> Kinds = new()
        {
            [CastKind.Spell] = (new Color(0x38, 0x46, 0x2b), new Color(0xda, 0x72, 0x10)),
            [CastKind.Incantation] = (new Color(0xd6, 0x5a, 0x51), new Color(0x5d, 0x7b, 0xa2)),
        };

        /// <summary>Where it was drawn from. An element with no entry draws in the plain tooltip colour.</summary>
        public static readonly Dictionary<string, (Color fill, Color stroke)> Elements = new()
        {
            ["Flame"] = (new Color(0x79, 0x26, 0x20), new Color(0xd7, 0x7f, 0x2d)),
            ["Blood"] = (new Color(0xff, 0x42, 0x42), new Color(0x68, 0x03, 0x03)),
            ["Frost"] = (new Color(0x9f, 0xe2, 0xff), new Color(0x1d, 0x3b, 0x7a)),
            ["Flora and Fauna"] = (new Color(0x0f, 0x33, 0x11), new Color(0x1d, 0x9f, 0x49)),
            // Its stroke is the ramp below. This one is the ramp's middle, for anything that
            // can only take one colour.
            ["Conquest"] = (new Color(0x7a, 0x3a, 0x24), new Color(0xc8, 0xa5, 0x5b)),
        };

        /// <summary>
        /// Elements whose stroke is a ramp: the first colour at the bottom of the letters,
        /// the last at the top, blended in between. Two or more colours.
        /// </summary>
        public static readonly Dictionary<string, Color[]> StrokeRamps = new()
        {
            ["Conquest"] = new[] { new Color(0xbd, 0xab, 0x51), new Color(0xc8, 0xa5, 0x5b), new Color(0xdb, 0xd8, 0x8f) },
        };

        /// <summary>The colours of an element, or null if it has none of its own.</summary>
        public static (Color fill, Color stroke)? Of(string element) =>
            Elements.TryGetValue(element, out (Color fill, Color stroke) found) ? found : null;

        /// <summary>An element's stroke ramp, bottom to top, or null if its stroke is one colour.</summary>
        public static Color[] RampOf(string element) =>
            element != null && StrokeRamps.TryGetValue(element, out Color[] ramp) ? ramp : null;

        /// <summary>
        /// Text with a coloured outline. The shadow pass is what makes the outline. Given a
        /// <paramref name="ramp"/> (bottom to top), the outline is that ramp instead of
        /// <paramref name="stroke"/>; plain text only then, no colour codes.
        /// </summary>
        public static void Stroked(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 at,
            Color fill, Color stroke, Vector2 scale, float rotation = 0f, Vector2 origin = default, Color[] ramp = null)
        {
            if (ramp is { Length: > 1 })
            {
                StrokedRamp(spriteBatch, font, text, at, fill, ramp, scale, rotation, origin);
                return;
            }

            ChatManager.DrawColorCodedStringShadow(spriteBatch, font, text, at, stroke, rotation, origin, scale, -1f, 2f);
            ChatManager.DrawColorCodedString(spriteBatch, font, text, at, fill, rotation, origin, scale, -1f, false);
        }

        /// <summary>Width of a string at this scale, for putting the next one after it.</summary>
        public static float Width(DynamicSpriteFont font, string text, Vector2 scale) =>
            ChatManager.GetStringSize(font, text, scale).X;

        // ---- a stroke that is a ramp ------------------------------------------------
        //
        // The letters are laid out exactly as vanilla lays out plain text: ChatManager
        // draws it a word at a time, a space's width between words, and each word goes
        // through DynamicSpriteFont's own layout (kerning, character spacing, the glyph's
        // offset in its cell). Both passes here use that one layout, so the fill sits on
        // the outline the way it does on every other line.

        /// <summary>Vanilla's outline, ChatManager.ShadowDirections: the text again, this far left, right, up and down.</summary>
        private static readonly Vector2[] Around = { -Vector2.UnitX, Vector2.UnitX, -Vector2.UnitY, Vector2.UnitY };

        private const float Spread = 2f;

        /// <summary>One letter, laid out.</summary>
        private readonly struct Letter
        {
            public readonly Texture2D Texture;
            public readonly Rectangle Glyph;

            /// <summary>Where its word starts, from the start of the text. Not turned: ChatManager does not turn it either.</summary>
            public readonly Vector2 Word;

            /// <summary>Its top left inside the word, scaled, before the word's turn.</summary>
            public readonly Vector2 In;

            public Letter(Texture2D texture, Rectangle glyph, Vector2 word, Vector2 inWord)
            {
                Texture = texture;
                Glyph = glyph;
                Word = word;
                In = inWord;
            }
        }

        private static readonly List<Letter> letters = new();

        private static void StrokedRamp(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 at,
            Color fill, Color[] ramp, Vector2 scale, float rotation, Vector2 origin)
        {
            Lay(font, text, scale);

            // How far the ink runs, top to bottom, plus the outline's reach at each end: the
            // ramp is spread over exactly that, so all of it shows.
            float top = float.MaxValue;
            float bottom = float.MinValue;

            foreach (Letter letter in letters)
            {
                if (letter.Glyph.Width <= 0 || letter.Glyph.Height <= 0)
                    continue;

                float y = letter.Word.Y + letter.In.Y;
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y + letter.Glyph.Height * scale.Y);
            }

            if (top > bottom)
                return;

            top -= Spread;
            bottom += Spread;
            float span = Math.Max(1f, bottom - top);

            // DynamicSpriteFont's own placement of a glyph: its offset scaled, the origin
            // taken off, the whole turned, then moved to where the word starts.
            Matrix turn = Matrix.CreateTranslation(-origin.X * scale.X, -origin.Y * scale.Y, 0f) * Matrix.CreateRotationZ(rotation);

            foreach (Vector2 direction in Around)
            {
                Vector2 offset = direction * Spread;

                foreach (Letter letter in letters)
                {
                    for (int row = 0; row < letter.Glyph.Height; row++)
                    {
                        Vector2 local = letter.In + new Vector2(0f, row * scale.Y);

                        // 0 at the top of the ink, 1 at the bottom; the ramp runs the other way.
                        float down = (letter.Word.Y + local.Y + 0.5f * scale.Y + offset.Y - top) / span;

                        spriteBatch.Draw(letter.Texture, Vector2.Transform(local, turn) + at + letter.Word + offset,
                            new Rectangle(letter.Glyph.X, letter.Glyph.Y + row, letter.Glyph.Width, 1),
                            Ramp(ramp, 1f - down), rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    }
                }
            }

            // The fill takes the same slow pulse vanilla gives every tooltip line.
            Color ink = ChatManager.WaveColor(fill);

            foreach (Letter letter in letters)
            {
                spriteBatch.Draw(letter.Texture, Vector2.Transform(letter.In, turn) + at + letter.Word, letter.Glyph,
                    ink, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>Lays plain text out into <see cref="letters"/>, line by line and word by word, as ChatManager does.</summary>
        private static void Lay(DynamicSpriteFont font, string text, Vector2 scale)
        {
            letters.Clear();

            float space = font.MeasureString(" ").X;
            string[] lines = text.Split('\n');
            float y = 0f;

            for (int l = 0; l < lines.Length; l++)
            {
                if (l > 0)
                    y += font.LineSpacing * scale.Y;

                string[] words = lines[l].Split(' ');
                float x = 0f;

                for (int w = 0; w < words.Length; w++)
                {
                    if (w > 0)
                        x += space * scale.X;

                    LayWord(font, words[w], new Vector2(x, y), scale);
                    x += font.MeasureString(words[w]).X * scale.X;
                }
            }
        }

        /// <summary>One word, the way DynamicSpriteFont.InternalDraw places its glyphs with no flip.</summary>
        private static void LayWord(DynamicSpriteFont font, string word, Vector2 start, Vector2 scale)
        {
            Vector2 pen = Vector2.Zero;
            bool first = true;

            foreach (char c in word)
            {
                if (c == '\r')
                    continue;

                if (!font.SpriteCharacters.TryGetValue(c, out DynamicSpriteFont.SpriteCharacterData data))
                    data = font.DefaultCharacterData;

                Vector3 kerning = data.Kerning;

                if (first)
                    kerning.X = Math.Max(kerning.X, 0f);
                else
                    pen.X += font.CharacterSpacing * scale.X;

                pen.X += kerning.X * scale.X;

                letters.Add(new Letter(data.Texture, data.Glyph, start,
                    pen + new Vector2(data.Padding.X * scale.X, data.Padding.Y * scale.Y)));

                pen.X += (kerning.Y + kerning.Z) * scale.X;
                first = false;
            }
        }

        /// <summary>A colour along a ramp: 0 is its first colour, 1 its last.</summary>
        private static Color Ramp(Color[] ramp, float along)
        {
            float at = MathHelper.Clamp(along, 0f, 1f) * (ramp.Length - 1);
            int i = Math.Min((int)at, ramp.Length - 2);
            return Color.Lerp(ramp[i], ramp[i + 1], at - i);
        }
    }
}
