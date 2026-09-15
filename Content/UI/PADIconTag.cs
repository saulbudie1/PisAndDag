using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// An item sprite in a line of text, standing on the baseline like a glyph.
    ///
    ///     [padi:320]        Shark Fin, 1:1 pixels at text scale 1 (cursor size)
    ///     [padi/s1.3:320]   1.3× that
    ///     [padi/c:320]      fitted between the cap line and the baseline instead
    ///     [padi/t6a9c9c:320] tinted (hex RGB); default is the sprite's own colours
    ///     [padi:SharkFin]   by name
    ///
    /// Whatever the options say, a sprite is never drawn taller than MaxLineFraction of
    /// the line: a fin is left at 1:1, a sword is shrunk until it fits. Smaller is never
    /// scaled up.
    ///
    /// Calamity's ChatTags shape: ITagHandler + ILoadable, self-registering. Skips drawing
    /// when the colour is black — that is the shadow pass (vanilla's ItemSnippet does the
    /// same).
    /// </summary>
    public sealed class PADIconTag : ITagHandler, ILoadable
    {
        public const string Name = "padi";

        /// <summary>
        /// Tallest a sprite may draw, as a fraction of the line height. Standing on the
        /// baseline (0.63 of the line down), a sprite this tall pokes 0.17 of a line above
        /// the line's top — under TalkOptions' RowGap, so it never touches the row above.
        /// </summary>
        public const float MaxLineFraction = 0.8f;

        public void Load(Mod mod)
        {
            ChatManager.Register<PADIconTag>(Name);
        }

        public void Unload()
        {
            // Nothing to undo: tModLoader rebuilds ChatManager's handler table on reload.
            FontMetrics.Clear();
        }

        public TextSnippet Parse(string text, Color baseColor = default, string options = null)
        {
            if (!int.TryParse(text, out int type) && !ItemID.Search.TryGetId(text, out type))
                return new TextSnippet(text);

            if (type <= 0 || type >= ItemLoader.ItemCount)
                return new TextSnippet(text);

            float size = 1f;
            bool fitCaps = false;
            Color tint = Color.White;

            if (options != null)
            {
                foreach (string option in options.Split(','))
                {
                    if (option.Length == 0)
                        continue;

                    if (option[0] == 'c')
                        fitCaps = true;
                    else if (option[0] == 's'
                        && float.TryParse(option.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                        size = MathHelper.Clamp(s, 0.25f, 4f);
                    else if (option[0] == 't' && option.Length == 7
                        && int.TryParse(option.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                        tint = new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                }
            }

            return new Snippet(type, size, fitCaps, tint);
        }

        private sealed class Snippet : TextSnippet
        {
            private readonly int type;

            /// <summary>Multiplier on the sprite's own pixels (or on the cap-fit, with c).</summary>
            private readonly float sizeMult;

            /// <summary>Fit the sprite between the cap line and the baseline instead.</summary>
            private readonly bool fitCaps;

            /// <summary>Multiplied into the sprite. White leaves its own colours alone.</summary>
            private readonly Color tint;

            public Snippet(int type, float sizeMult, bool fitCaps, Color tint)
            {
                this.type = type;
                this.sizeMult = sizeMult;
                this.fitCaps = fitCaps;
                this.tint = tint;

                // What the tag reads as where it cannot be drawn (copied text, the log).
                Text = "[" + Lang.GetItemNameValue(type) + "]";
                DeleteWhole = true;
            }

            public override bool UniqueDraw(bool justCheckingString, out Vector2 size, SpriteBatch spriteBatch,
                Vector2 position = default, Color color = default, float scale = 1f)
            {
                // tModLoader passes baseScale × snippet scale here (its ChatManager patch),
                // so this is the scale the neighbouring words are drawn at.
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                FontMetrics.Caps(font, out float capTop, out float baseline);

                Texture2D texture = null;
                Rectangle frame = new Rectangle(0, 0, 16, 16);

                if (!Main.dedServ)
                {
                    Main.instance.LoadItem(type);
                    texture = TextureAssets.Item[type].Value;
                    frame = Main.itemAnimations[type]?.GetFrame(texture) ?? texture.Frame();
                }

                // 1:1 with the sprite's own pixels at text scale 1 — what the item looks
                // like sitting on the cursor — or, with c, exactly as tall as a capital.
                float draw = fitCaps
                    ? (baseline - capTop) * scale / frame.Height
                    : scale;

                draw *= sizeMult;

                // The ceiling. Applied after the options so it always wins: a sprite
                // taller than its share of the line is shrunk to fit, whatever /s asked.
                float tallest = font.LineSpacing * scale * MaxLineFraction;

                if (frame.Height * draw > tallest)
                    draw = tallest / frame.Height;

                float width = frame.Width * draw;
                float height = frame.Height * draw;

                size = new Vector2(width, font.LineSpacing * scale);

                if (justCheckingString || color == Color.Black || texture == null)
                    return true;

                // Standing on the baseline, where the letters stand. The line's top is
                // `position`; the baseline is `baseline` glyph-pixels below it, scaled.
                Vector2 centre = position + new Vector2(width * 0.5f, baseline * scale - height * 0.5f);

                spriteBatch.Draw(texture, centre, frame, tint, 0f, frame.Size() * 0.5f, draw,
                    SpriteEffects.None, 0f);

                return true;
            }

            public override float GetStringLength(DynamicSpriteFont font)
            {
                // Only the wrap code asks; it wants an advance at scale 1. Close enough to
                // use the line height, since the wrap only needs to know roughly how wide
                // this is.
                return font.LineSpacing * sizeMult * Scale;
            }
        }

        /// <summary>
        /// Cap line and baseline of a font, in glyph pixels at scale 1, down from the line's
        /// top. DynamicSpriteFont never exposes them (MeasureString's height is always
        /// LineSpacing), but each character's private SpriteCharacterData has Glyph (atlas
        /// rect) and Padding (pen offset, XNA's "cropping"): for 'A', cap line = Padding.Y,
        /// baseline = Padding.Y + Glyph.Height. Read once by reflection; measured-off-a-
        /// screenshot fallback if that fails or looks wrong.
        /// </summary>
        private static class FontMetrics
        {
            private const float FallbackCapTop = 0.05f;
            private const float FallbackBaseline = 0.63f;

            private static readonly Dictionary<DynamicSpriteFont, (float capTop, float baseline)> cache = new();

            public static void Clear() => cache.Clear();

            public static void Caps(DynamicSpriteFont font, out float capTop, out float baseline)
            {
                if (!cache.TryGetValue(font, out (float capTop, float baseline) caps))
                {
                    caps = Measure(font);
                    cache[font] = caps;
                }

                capTop = caps.capTop;
                baseline = caps.baseline;
            }

            private static (float capTop, float baseline) Measure(DynamicSpriteFont font)
            {
                float line = font.LineSpacing;
                (float capTop, float baseline) fallback = (line * FallbackCapTop, line * FallbackBaseline);

                try
                {
                    FieldInfo field = typeof(DynamicSpriteFont).GetField("_spriteCharacters",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (field?.GetValue(font) is not IDictionary characters || !characters.Contains('A'))
                        return fallback;

                    object data = characters['A'];
                    Type type = data.GetType();

                    Rectangle glyph = (Rectangle)type.GetField("Glyph").GetValue(data);
                    Rectangle padding = (Rectangle)type.GetField("Padding").GetValue(data);

                    float capTop = padding.Y;
                    float baseline = padding.Y + glyph.Height;

                    // A capital sits inside its line and is a good fraction of it tall.
                    // Anything else means the fields do not mean what this expects.
                    bool plausible = capTop >= 0f
                        && baseline > capTop
                        && baseline <= line
                        && baseline - capTop >= line * 0.3f;

                    return plausible ? (capTop, baseline) : fallback;
                }
                catch
                {
                    return fallback;
                }
            }
        }
    }
}
