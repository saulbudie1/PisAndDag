using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.NPCs.Empress;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// What Made By Tentochi puts on the wearer's screen:
    ///
    /// <list type="bullet">
    /// <item>The Calamity Module's meter, under the wearer's feet (in the world's scale, so it stays by them at any zoom): a
    /// frame of dark steel, twenty cells filling in their red; full, it pulses and says which key lets it go; let go, it
    /// burns near white and runs down over the five seconds.</item>
    /// <item>While adrenaline runs, the screen's edges in their red, lines across the screen as an old monitor's, and now
    /// and then a band torn across it.</item>
    /// <item>A flash over the whole screen when something of theirs goes off near (<see cref="Flash"/>): white, cooling to
    /// their red.</item>
    /// </list>
    ///
    /// Drawn for the player at the keyboard only.
    /// </summary>
    public class TentochiHUD : ModSystem
    {
        private static float flash;
        private static float flashFrom;

        /// <summary>A flash over the screen, this strong (0 to 1); a stronger one replaces a weaker, never the other way.</summary>
        public static void Flash(float strength)
        {
            if (Main.dedServ || strength <= flash)
                return;

            flash = flashFrom = MathHelper.Clamp(strength, 0f, 1f);
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            flash = Math.Max(0f, flash - 0.035f);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int bars = layers.FindIndex(layer => layer.Name == "Vanilla: Resource Bars");

            if (bars < 0)
                bars = layers.Count;

            layers.Insert(bars, new LegacyGameInterfaceLayer("PisAndDag: Tentochi Screen", DrawScreen, InterfaceScaleType.UI));
            layers.Insert(bars, new LegacyGameInterfaceLayer("PisAndDag: Tentochi Meter", DrawMeter, InterfaceScaleType.Game));
        }

        // ---- over the screen ------------------------------------------------------------------------------------------

        private static bool DrawScreen()
        {
            Player player = Main.LocalPlayer;

            if (Main.gameMenu || !player.active)
                return true;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);
            // The screen as this layer sees it: the game has already divided its size by the UI scale for a UI layer
            // (GameInterfaceLayer.Draw, PlayerInput.SetZoom_UI), and draws it back up by that scale (Main.UIScaleMatrix).
            float w = Main.screenWidth;
            float h = Main.screenHeight;
            TentochiPlayer t = player.GetModPlayer<TentochiPlayer>();

            if (t.Surging && !player.dead)
            {
                float time = Main.GlobalTimeWrappedHourly;
                float beat = 0.75f + 0.25f * (float)Math.Sin(time * 7f);
                float edge = Math.Min(w, h) * 0.22f;

                if (EmpressArt.Ready(EmpressArt.Line))
                {
                    // The edges in their red: the soft line's middle laid along each edge, half of it on the screen.
                    Texture2D line = EmpressArt.Line.Value;
                    Color red = TentochiFX.Glow(TentochiFX.Red, 0.55f * beat);
                    float across = edge * 2f / line.Height;
                    sb.Draw(line, new Vector2(0f, 0f), null, red, 0f, new Vector2(0f, line.Height * 0.5f), new Vector2(w / line.Width, across), SpriteEffects.None, 0f);
                    sb.Draw(line, new Vector2(0f, h), null, red, 0f, new Vector2(0f, line.Height * 0.5f), new Vector2(w / line.Width, across), SpriteEffects.None, 0f);
                    sb.Draw(line, new Vector2(0f, 0f), null, red, MathHelper.PiOver2, new Vector2(0f, line.Height * 0.5f), new Vector2(h / line.Width, across), SpriteEffects.None, 0f);
                    sb.Draw(line, new Vector2(w, 0f), null, red, MathHelper.PiOver2, new Vector2(0f, line.Height * 0.5f), new Vector2(h / line.Width, across), SpriteEffects.None, 0f);
                }

                // Lines across, as an old monitor's, faint.
                Color scan = Color.Black * 0.1f;

                for (float y = (time * 40f) % 4f; y < h; y += 4f)
                    sb.Draw(pixel, new Vector2(0f, y), one, scan, 0f, Vector2.Zero, new Vector2(w, 1f), SpriteEffects.None, 0f);

                // Now and then a band torn across the screen, red or cyan.
                int tear = (int)(time * 12f);

                if (tear % 9 == 0)
                {
                    float y = (tear * 7919 % 1000) / 1000f * h;
                    Color colour = TentochiFX.Glow(tear % 2 == 0 ? TentochiFX.Red : TentochiFX.Cyan, 0.25f);
                    sb.Draw(pixel, new Vector2(0f, y), one, colour, 0f, Vector2.Zero, new Vector2(w, 2f + tear % 3 * 2f), SpriteEffects.None, 0f);
                }
            }

            if (flash > 0.01f)
            {
                float share = flash / Math.Max(0.01f, flashFrom);
                Color colour = Color.Lerp(TentochiFX.Red, Color.White, share * share);
                sb.Draw(pixel, Vector2.Zero, one, colour * (0.65f * flash), 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);
            }

            return true;
        }

        // ---- the meter ------------------------------------------------------------------------------------------------

        public const int Cells = 20;
        public const float CellWidth = 2f;
        public const float CellGap = 1f;
        public const float BarHeight = 4f;

        private static bool DrawMeter()
        {
            Player player = Main.LocalPlayer;

            if (Main.gameMenu || !player.active || player.dead || MimicSystem.Is(player))
                return true;

            TentochiPlayer t = player.GetModPlayer<TentochiPlayer>();

            if (!t.Module)
                return true;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);
            float time = Main.GlobalTimeWrappedHourly;

            float inner = Cells * CellWidth + (Cells - 1) * CellGap;
            Vector2 foot = player.Bottom + new Vector2(0f, player.gfxOffY + 12f) - Main.screenPosition;
            Vector2 corner = new Vector2((float)Math.Floor(foot.X - inner * 0.5f), (float)Math.Floor(foot.Y));

            // The frame: dark steel, a hair of their red along its top, cut corners.
            Rectangle frame = new Rectangle((int)corner.X - 3, (int)corner.Y - 3, (int)inner + 6, (int)BarHeight + 6);
            sb.Draw(pixel, new Vector2(frame.X + 2, frame.Y), one, new Color(22, 22, 30) * 0.9f, 0f, Vector2.Zero, new Vector2(frame.Width - 4, frame.Height), SpriteEffects.None, 0f);
            sb.Draw(pixel, new Vector2(frame.X, frame.Y + 2), one, new Color(22, 22, 30) * 0.9f, 0f, Vector2.Zero, new Vector2(frame.Width, frame.Height - 4), SpriteEffects.None, 0f);
            sb.Draw(pixel, new Vector2(frame.X + 2, frame.Y), one, TentochiFX.Red * 0.8f, 0f, Vector2.Zero, new Vector2(frame.Width - 8, 1f), SpriteEffects.None, 0f);
            sb.Draw(pixel, new Vector2(frame.Right - 5, frame.Y), one, TentochiFX.Cyan * 0.7f, 0f, Vector2.Zero, new Vector2(3f, 1f), SpriteEffects.None, 0f);

            float share;
            Color fill;
            bool full = false;

            if (t.Surging)
            {
                int left = TimeLeft(player);
                share = left / (float)TentochiPlayer.AdrenalineTicks;
                fill = Color.Lerp(TentochiFX.Hot, Color.White, 0.5f + 0.5f * (float)Math.Sin(time * 20f));
            }
            else
            {
                share = t.Adrenaline / TentochiPlayer.AdrenalineMax;
                full = share >= 1f;
                fill = full ? Color.Lerp(TentochiFX.Red, TentochiFX.Hot, 0.5f + 0.5f * (float)Math.Sin(time * 6f)) : TentochiFX.Red;
            }

            int lit = (int)Math.Floor(share * Cells);
            float partial = share * Cells - (float)Math.Floor(share * Cells);

            for (int i = 0; i < Cells; i++)
            {
                Vector2 cell = corner + new Vector2(i * (CellWidth + CellGap), 0f);
                Color colour = i < lit ? fill : new Color(52, 22, 32);

                // The cell filling now, by its share.
                if (i == lit && !t.Surging && partial > 0.05f)
                    colour = Color.Lerp(new Color(52, 22, 32), TentochiFX.Red, partial);

                sb.Draw(pixel, cell, one, colour, 0f, Vector2.Zero, new Vector2(CellWidth, BarHeight), SpriteEffects.None, 0f);
            }

            if (full || t.Surging)
            {
                // Its light, and the word.
                float glow = t.Surging ? 0.6f : 0.35f + 0.25f * (float)Math.Sin(time * 6f);
                if (EmpressArt.Ready(EmpressArt.Line))
                {
                    Texture2D line = EmpressArt.Line.Value;
                    sb.Draw(line, corner + new Vector2(-4f, BarHeight * 0.5f), null, TentochiFX.Glow(TentochiFX.Red, glow), 0f,
                        new Vector2(0f, line.Height * 0.5f), new Vector2((inner + 8f) / line.Width, 14f / line.Height), SpriteEffects.None, 0f);
                }

                string text = t.Surging
                    ? Language.GetTextValue("Mods.PisAndDag.Tentochi.Surging", (TimeLeft(player) / 60f).ToString("0.0"))
                    : Language.GetTextValue("Mods.PisAndDag.Tentochi.Ready", TentochiItem.KeyName(PADKeybinds.Adrenaline));
                DynamicSpriteFont font = FontAssets.ItemStack.Value;
                Vector2 size = font.MeasureString(text) * 0.75f;
                Vector2 at = new Vector2((float)Math.Floor(foot.X - size.X * 0.5f), corner.Y + BarHeight + 4f);
                Utils.DrawBorderStringFourWay(sb, font, text, at.X, at.Y, t.Surging ? Color.White : TentochiFX.Hot, new Color(40, 6, 14), Vector2.Zero, 0.75f);
            }

            return true;
        }

        /// <summary>The adrenaline left, in ticks: what is left of the buff.</summary>
        private static int TimeLeft(Player player)
        {
            int index = player.FindBuffIndex(ModContent.BuffType<Buffs.AdrenalineRush>());
            return index >= 0 ? player.buffTime[index] : 0;
        }
    }
}
