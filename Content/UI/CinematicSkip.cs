using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// "E  Skip" in the bottom right corner of a cinematic (by request: "during cinematics, can you add E to skip on the
    /// corner"). A scene that can be skipped offers it every tick it runs (Offer); holding E for a moment (HoldTicks, the
    /// key's face filling as it is held, so a tap of E meant for something else never skips) asks for the skip, and the
    /// scene takes it on its next tick (Take) and jumps to its end its own way.
    ///
    /// Single player only: in a shared world a scene is everyone's, and one player cannot end it for the rest.
    /// A key already down when the offer comes up has to be let go first. Drawn over the cinematic bars, and kept off the
    /// screen by the player's own F11 (the key still works).
    ///
    ///     CinematicSkip.Offer();
    ///     if (CinematicSkip.Take()) { ...the scene, ended... }
    /// </summary>
    public static class CinematicSkip
    {
        /// <summary>Ticks E has to be held to skip; ticks the prompt takes to come up and go.</summary>
        public const int HoldTicks = 24;
        private const int FadeTicks = 16;

        /// <summary>From the screen's right and bottom edges to the prompt's, in interface pixels.</summary>
        private const float Margin = 28f;

        /// <summary>The key's face, square, and the gap from it to the word.</summary>
        private const int KeySize = 26;
        private const float Gap = 8f;

        private static readonly Color Ink = new Color(250, 246, 238);

        private static int offered;
        private static int held;
        private static bool armed;
        private static bool asked;
        private static float shown;

        /// <summary>This tick, the scene running can be skipped: the prompt up, and E listened for. Single player only.</summary>
        public static void Offer()
        {
            if (!Main.dedServ && Main.netMode == NetmodeID.SinglePlayer)
                offered = 2;
        }

        /// <summary>Whether the player has asked to skip (once: true the first time it is asked after the asking).</summary>
        public static bool Take()
        {
            bool was = asked;
            asked = false;
            return was;
        }

        /// <summary>Once a tick, after everything (the scenes have offered by then): E, held, asks.</summary>
        internal static void Update()
        {
            if (offered > 0)
                offered--;

            bool on = offered > 0;
            shown = MathHelper.Clamp(shown + (on ? 1f : -1f) / FadeTicks, 0f, 1f);

            if (!on)
            {
                held = 0;
                armed = false;
                asked = false;
                return;
            }

            bool typing = Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.gameMenu || Main.blockInput || PlayerInput.WritingText
                || Main.ingameOptionsWindow || (Main.InGameUI != null && Main.InGameUI.IsVisible);
            bool down = !typing && Main.hasFocus && Main.keyState.IsKeyDown(Keys.E);

            if (!down)
            {
                armed = true;
                held = 0;
                return;
            }

            if (!armed)
                return;

            if (++held >= HoldTicks)
            {
                asked = true;
                armed = false;
                held = 0;
            }
        }

        internal static void Clear()
        {
            offered = 0;
            held = 0;
            armed = false;
            asked = false;
            shown = 0f;
        }

        /// <summary>The prompt, in interface space: the key's face (filling as it is held) and the word.</summary>
        internal static void Draw(SpriteBatch spriteBatch)
        {
            if (shown <= 0.01f || Main.gameMenu || Main.dedServ)
                return;

            // The player's own F11 takes it off the screen, as it does everything else.
            if (Main.hideUI && !PisPlayer.HidingUI)
                return;

            float alpha = shown * shown * (3f - 2f * shown) * 0.9f;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            const string word = "Skip";
            const float scale = 0.95f;
            Vector2 wordSize = font.MeasureString(word) * scale;

            float right = Main.screenWidth - Margin;
            float bottom = Main.screenHeight - Margin;

            Vector2 wordAt = new Vector2((float)Math.Round(right - wordSize.X), (float)Math.Round(bottom - KeySize * 0.5f - wordSize.Y * 0.42f));
            Rectangle key = new Rectangle((int)(wordAt.X - Gap - KeySize), (int)(bottom - KeySize), KeySize, KeySize);

            // The key's face: dark, a pale rim, filling from the bottom as E is held.
            spriteBatch.Draw(pixel, key, Color.Black * (0.45f * alpha));

            float fill = held / (float)HoldTicks;

            if (fill > 0f)
            {
                int high = (int)Math.Round((KeySize - 2) * fill);
                spriteBatch.Draw(pixel, new Rectangle(key.X + 1, key.Bottom - 1 - high, KeySize - 2, high), Ink * (0.35f * alpha));
            }

            Color rim = Ink * (0.75f * alpha);
            spriteBatch.Draw(pixel, new Rectangle(key.X, key.Y, key.Width, 1), rim);
            spriteBatch.Draw(pixel, new Rectangle(key.X, key.Bottom - 1, key.Width, 1), rim);
            spriteBatch.Draw(pixel, new Rectangle(key.X, key.Y, 1, key.Height), rim);
            spriteBatch.Draw(pixel, new Rectangle(key.Right - 1, key.Y, 1, key.Height), rim);

            Vector2 letterSize = font.MeasureString("E") * scale;
            Vector2 letterAt = new Vector2((float)Math.Round(key.Center.X - letterSize.X * 0.5f), (float)Math.Round(key.Center.Y - letterSize.Y * 0.42f));

            Shadowed(spriteBatch, font, "E", letterAt, scale, alpha);
            Shadowed(spriteBatch, font, word, wordAt, scale, alpha);
        }

        private static void Shadowed(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 at, float scale, float alpha)
        {
            Color shadow = Color.Black * (0.5f * alpha);

            spriteBatch.DrawString(font, text, at + new Vector2(1f, 1f), shadow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, at, Ink * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Listens once a tick, clears with the world, and draws the prompt over the cinematic bars (ahead of the F11 toggle, as the boss bar is).</summary>
    public class CinematicSkipSystem : ModSystem
    {
        public override void Load()
        {
            if (!Main.dedServ)
                Cinematic.OverBars += CinematicSkip.Draw;
        }

        public override void Unload()
        {
            Cinematic.OverBars -= CinematicSkip.Draw;
            CinematicSkip.Clear();
        }

        public override void PostUpdateEverything()
        {
            if (!Main.dedServ)
                CinematicSkip.Update();
        }

        public override void OnWorldUnload() => CinematicSkip.Clear();

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Hide UI Toggle");

            if (index == -1)
                index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Cinematic Skip",
                () =>
                {
                    if (!Cinematic.BarsOverEverything)
                        CinematicSkip.Draw(Main.spriteBatch);

                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}
