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
    /// </summary>
    public static class MagicColours
    {
        /// <summary>Spell or incantation, the word itself.</summary>
        public static readonly Dictionary<CastKind, (Color fill, Color stroke)> Kinds = new()
        {
            [CastKind.Spell] = (new Color(200, 170, 255), Color.Black),
            [CastKind.Incantation] = (new Color(0xd6, 0x5a, 0x51), new Color(0x85, 0xaa, 0xdb)),
        };

        /// <summary>Where it was drawn from. An element with no entry draws in the plain tooltip colour.</summary>
        public static readonly Dictionary<string, (Color fill, Color stroke)> Elements = new()
        {
            ["Flame"] = (new Color(0x79, 0x26, 0x20), new Color(0xd7, 0x7f, 0x2d)),
            ["Blood"] = (new Color(0xff, 0x42, 0x42), new Color(0x68, 0x03, 0x03)),
        };

        /// <summary>The colours of an element, or null if it has none of its own.</summary>
        public static (Color fill, Color stroke)? Of(string element) =>
            Elements.TryGetValue(element, out (Color fill, Color stroke) found) ? found : null;

        /// <summary>Text with a coloured outline. The shadow pass is what makes the outline.</summary>
        public static void Stroked(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 at,
            Color fill, Color stroke, Vector2 scale, float rotation = 0f, Vector2 origin = default)
        {
            ChatManager.DrawColorCodedStringShadow(spriteBatch, font, text, at, stroke, rotation, origin, scale, -1f, 2f);
            ChatManager.DrawColorCodedString(spriteBatch, font, text, at, fill, rotation, origin, scale, -1f, false);
        }

        /// <summary>Width of a string at this scale, for putting the next one after it.</summary>
        public static float Width(DynamicSpriteFont font, string text, Vector2 scale) =>
            ChatManager.GetStringSize(font, text, scale).X;
    }
}
