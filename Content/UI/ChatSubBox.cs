using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// A small box that stands on the town chat panel's top edge, on its right, and holds
    /// something to put an item in: the reforge's slot (ReforgeBox), and anything after it
    /// that wants the same, the Guide's crafting slot say.
    ///
    ///     Rectangle box = ChatSubBox.OnPanel(panel, contentWidth, contentHeight);
    ///     ChatSubBox.Draw(spriteBatch, box);
    ///     ChatSubBox.KeepMouse(box);
    ///     Vector2 slot = ChatSubBox.Content(box);    // the slot's top-left, and the content's
    ///
    /// Drawn from ChatSubBox.png beside this file, as a nine-slice: the four 16×16 corners as
    /// they are, the edges and the middle tiled rather than stretched, so the art's 2×2 pixels
    /// stay 2×2 at any size and the PNG can be repainted without touching this. The texture is
    /// 48×48, so the margins are 16 on every side and the tiled middle is 16×16. Keep a new
    /// painting's corners inside those 16 pixels, and its edges and middle tileable.
    /// </summary>
    public static class ChatSubBox
    {
        /// <summary>The nine-slice margins: how much of each side of ChatSubBox.png is corner and edge.</summary>
        public const int Margin = 16;

        /// <summary>From the box's edge in to what it holds.</summary>
        public const int Pad = 14;

        /// <summary>How much of the box's foot the panel's top edge covers, as the name tab's is covered.</summary>
        public const int Tuck = 4;

        /// <summary>From the panel's right edge in to the box's.</summary>
        public const int Inset = 12;

        private static Asset<Texture2D> texture;

        internal static void Clear()
        {
            texture = null;
        }

        /// <summary>
        /// A box for content this big, standing on <paramref name="panel"/>'s top edge at its right.
        /// Never wider than the panel leaves room for, and always a whole number of art pixels.
        /// </summary>
        public static Rectangle OnPanel(Rectangle panel, int contentWidth, int contentHeight)
        {
            int width = Even(Math.Min(contentWidth + Pad * 2, panel.Width - Inset * 2));
            int height = Even(contentHeight + Pad * 2);

            return new Rectangle(panel.Right - Inset - width, panel.Y - height + Tuck, width, height);
        }

        /// <summary>Where what the box holds begins: its top-left, inside the padding.</summary>
        public static Vector2 Content(Rectangle box) => new Vector2(box.X + Pad, box.Y + Pad);

        /// <summary>
        /// The mouse is the interface's while it is over the box, as it is over vanilla's own
        /// panels: a click there is not also a swing of whatever is held.
        /// </summary>
        public static void KeepMouse(Rectangle box)
        {
            if (box.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface)
                Main.LocalPlayer.mouseInterface = true;
        }

        /// <summary>The box, nine-sliced from ChatSubBox.png: corners whole, edges and middle tiled.</summary>
        public static void Draw(SpriteBatch spriteBatch, Rectangle box, float alpha = 1f)
        {
            texture ??= ModContent.Request<Texture2D>("PisAndDag/Content/UI/ChatSubBox", AssetRequestMode.ImmediateLoad);

            Texture2D t = texture.Value;
            Color colour = Color.White * alpha;
            int m = Margin;
            int tileW = t.Width - m * 2;
            int tileH = t.Height - m * 2;

            if (box.Width < m * 2)
                box.Width = m * 2;

            if (box.Height < m * 2)
                box.Height = m * 2;

            int right = box.Right - m;
            int bottom = box.Bottom - m;

            spriteBatch.Draw(t, new Vector2(box.X, box.Y), new Rectangle(0, 0, m, m), colour);
            spriteBatch.Draw(t, new Vector2(right, box.Y), new Rectangle(t.Width - m, 0, m, m), colour);
            spriteBatch.Draw(t, new Vector2(box.X, bottom), new Rectangle(0, t.Height - m, m, m), colour);
            spriteBatch.Draw(t, new Vector2(right, bottom), new Rectangle(t.Width - m, t.Height - m, m, m), colour);

            for (int x = box.X + m; x < right; x += tileW)
            {
                int w = Math.Min(tileW, right - x);
                spriteBatch.Draw(t, new Vector2(x, box.Y), new Rectangle(m, 0, w, m), colour);
                spriteBatch.Draw(t, new Vector2(x, bottom), new Rectangle(m, t.Height - m, w, m), colour);
            }

            for (int y = box.Y + m; y < bottom; y += tileH)
            {
                int h = Math.Min(tileH, bottom - y);
                spriteBatch.Draw(t, new Vector2(box.X, y), new Rectangle(0, m, m, h), colour);
                spriteBatch.Draw(t, new Vector2(right, y), new Rectangle(t.Width - m, m, m, h), colour);

                for (int x = box.X + m; x < right; x += tileW)
                {
                    int w = Math.Min(tileW, right - x);
                    spriteBatch.Draw(t, new Vector2(x, y), new Rectangle(m, m, w, h), colour);
                }
            }
        }

        private static int Even(int n) => n + (n & 1);
    }
}
