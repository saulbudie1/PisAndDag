using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Items.Ranged;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The Last Grain's hourglass on the screen, by request ("create small hour glass UI in the corner of the screen
    /// bottom right"): while the gun is in hand, a glass in the bottom right corner with the player's sand in it
    /// (LastGrainPlayer), what is above falling through the neck in a thread onto a heap below; the upper sand's
    /// face dips in the middle as it drains, the heap below peaks. Turned over, it turns over, half a turn in a
    /// quarter of a second. Empty, its frame glows a dull red now and then, to say it wants turning.
    ///
    /// The frame and glass are a picture (LastGrainGlass, a placeholder: PlaceholderTextures.md); the sand is drawn
    /// pixel by pixel over the same shape (<see cref="HalfWidth"/>, the picture's own rows).
    /// </summary>
    public class LastGrainUI : ModSystem
    {
        /// <summary>For each of the picture's 36 rows, how many pixels the inside of the glass reaches either side of its middle; -1 where there is no inside.</summary>
        private static readonly int[] HalfWidth = { -1, -1, -1, 6, 7, 8, 8, 8, 8, 7, 7, 6, 5, 4, 3, 2, 1, 1, 1, 1, 2, 3, 4, 5, 6, 7, 7, 8, 8, 8, 8, 7, 6, -1, -1, -1 };

        /// <summary>The upper bulb's rows, the neck's, the lower bulb's.</summary>
        private const int TopFrom = 3, TopTo = 16, NeckFrom = 17, NeckTo = 18, BottomFrom = 19, BottomTo = 32;

        /// <summary>The picture's size, and how big it is drawn.</summary>
        private const int Width = 22, Height = 36;
        private const float Scale = 2f;

        /// <summary>From the bottom right corner of the screen to the glass's middle, in screen pixels.</summary>
        private static readonly Vector2 FromCorner = new Vector2(-64f, -104f);

        private const string GlassTexture = "PisAndDag/Content/UI/LastGrainGlass";

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer("PisAndDag: The Last Grain", () =>
            {
                Draw(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.UI));
        }

        private static void Draw(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;

            if (player == null || !player.active || player.dead || Main.gameMenu || player.HeldItem?.ModItem is not TheLastGrain)
                return;

            LastGrainPlayer glass = player.GetModPlayer<LastGrainPlayer>();
            Texture2D frame = ModContent.Request<Texture2D>(GlassTexture).Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            Vector2 centre = new Vector2(Main.screenWidth / Main.UIScale, Main.screenHeight / Main.UIScale) + FromCorner;

            // Turning: the glass as it was before it was turned, turned by up to half a turn.
            float turn = 0f;
            int top = glass.Top;

            if (glass.Turning > 0)
            {
                float k = 1f - glass.Turning / (float)LastGrainPlayer.TurnTicks;
                turn = MathHelper.Pi * (k * k * (3f - 2f * k));
                top = glass.TurnedFrom;
            }

            // A shadow behind it, so it reads on any ground.
            Texture2D soft = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft").Value;
            spriteBatch.Draw(soft, centre, null, Color.Black * 0.35f, 0f, soft.Size() * 0.5f, 0.42f, SpriteEffects.None, 0f);

            DrawSand(spriteBatch, pixel, centre, turn, top, glass.Turning <= 0);

            Color tint = Color.White;

            if (glass.Empty && glass.Turning <= 0)
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f);
                tint = Color.Lerp(Color.White, new Color(255, 130, 110), 0.55f * pulse);
            }

            spriteBatch.Draw(frame, centre, null, tint, turn, new Vector2(Width * 0.5f, Height * 0.5f), Scale, SpriteEffects.None, 0f);

            // Hovered, it says what it is.
            Rectangle hover = new Rectangle((int)(centre.X - Width), (int)(centre.Y - Height), Width * 2, Height * 2);

            if (hover.Contains(Main.MouseScreen.ToPoint()))
            {
                player.mouseInterface = true;
                Main.instance.MouseText(glass.Empty
                    ? Terraria.Localization.Language.GetTextValue("Mods.PisAndDag.UI.LastGrain.Empty")
                    : Terraria.Localization.Language.GetTextValue("Mods.PisAndDag.UI.LastGrain.Running", (int)Math.Ceiling(glass.Top / 60f)));
            }
        }

        /// <summary>
        /// The sand: what is above, from the neck up, its face dipping in the middle; what is below, from the bottom
        /// up, heaped in the middle; and between them, while it runs, the thread. In the picture's own pixels, turned
        /// with it.
        /// </summary>
        private static void DrawSand(SpriteBatch spriteBatch, Texture2D pixel, Vector2 centre, float turn, int top, bool running)
        {
            int topCells = 0, bottomCells = 0;

            for (int y = TopFrom; y <= TopTo; y++)
                topCells += 2 * HalfWidth[y];

            for (int y = BottomFrom; y <= BottomTo; y++)
                bottomCells += 2 * HalfWidth[y];

            float share = MathHelper.Clamp(top / (float)LastGrainPlayer.Full, 0f, 1f);
            int above = (int)Math.Round(share * topCells);
            int below = (int)Math.Round((1f - share) * bottomCells);

            // Above: from the neck up; the last row's sand at its sides, the middle drained first.
            for (int y = TopTo; y >= TopFrom && above > 0; y--)
            {
                int row = 2 * HalfWidth[y];
                int here = Math.Min(row, above);
                above -= here;

                for (int i = 0; i < here; i++)
                {
                    // From the outside in.
                    int k = i / 2;
                    int x = i % 2 == 0 ? 11 - HalfWidth[y] + k : 10 + HalfWidth[y] - k;
                    Cell(spriteBatch, pixel, centre, turn, x, y, Shade(x, y));
                }
            }

            // Below: from the bottom up; the last row's sand in its middle, heaped.
            for (int y = BottomTo; y >= BottomFrom && below > 0; y--)
            {
                int row = 2 * HalfWidth[y];
                int here = Math.Min(row, below);
                below -= here;

                for (int i = 0; i < here; i++)
                {
                    // From the middle out.
                    int k = i / 2;
                    int x = i % 2 == 0 ? 10 - k : 11 + k;
                    Cell(spriteBatch, pixel, centre, turn, x, y, Shade(x, y));
                }
            }

            // The thread, through the neck and down onto the heap, while there is sand to fall.
            if (running && top > 0)
            {
                int heapTop = BottomTo;

                for (int y = BottomTo, left = (int)Math.Round((1f - share) * bottomCells); y >= BottomFrom && left > 0; y--)
                {
                    left -= 2 * HalfWidth[y];
                    heapTop = y;
                }

                bool flick = (int)(Main.GlobalTimeWrappedHourly * 30f) % 2 == 0;

                for (int y = NeckFrom; y < heapTop; y++)
                    Cell(spriteBatch, pixel, centre, turn, flick ? 10 : 11, y, Harenaeum.Sand[3]);
            }
        }

        /// <summary>Sand's shade where it lies: lighter to the left, as the glass is lit.</summary>
        private static Color Shade(int x, int y)
        {
            int k = (x * 7 + y * 13) % 5;
            Color c = x < 9 ? Harenaeum.Sand[3] : x > 12 ? Harenaeum.Sand[1] : Harenaeum.Sand[2];
            return k == 0 ? Color.Lerp(c, Harenaeum.PaleGold, 0.35f) : c;
        }

        /// <summary>One of the picture's pixels, at <paramref name="x"/>, <paramref name="y"/>, turned with the glass.</summary>
        private static void Cell(SpriteBatch spriteBatch, Texture2D pixel, Vector2 centre, float turn, int x, int y, Color colour)
        {
            Vector2 local = new Vector2(x + 0.5f - Width * 0.5f, y + 0.5f - Height * 0.5f) * Scale;
            Vector2 at = centre + local.RotatedBy(turn);
            spriteBatch.Draw(pixel, at, new Rectangle(0, 0, 1, 1), colour, turn, new Vector2(0.5f, 0.5f), Scale, SpriteEffects.None, 0f);
        }
    }
}
