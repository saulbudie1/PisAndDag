using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Tiles;
using PisAndDag.Content.UI;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// A waygate's question, by request: right clicking on it "give you two choices:
    /// Traverse the mysterious gateway? "Yes" "No"". Asked the way Elden Ring asks: a dark
    /// band across the middle of the screen, a little under it, faint at both ends, a hair of
    /// gold over and under it; the question in it, and the two answers under the question,
    /// the one under the cursor lit. Yes goes through (SubworldPassage.ThroughWaygate); No, or
    /// walking away, or anything else opened over the game, lets it go. The words are the
    /// hjson's (Mods.PisAndDag.Subworlds.Waygate).
    ///
    /// Only the machine whose player asked draws it and answers it. Its click is its own,
    /// taken from the button as the mod's other late interface takes it (PADMouse: the edge
    /// read from the button itself, so nothing that spends the game's shared one can spend
    /// it), and while the cursor is on the band nothing in hand is used.
    /// </summary>
    public class WaygatePrompt : ModSystem
    {
        /// <summary>Further than this from the gate, in tiles, and the question is let go.</summary>
        private const float WalkedAway = 12f;

        /// <summary>The band: this wide and this high, in the interface's own pixels, its middle this far down the screen.</summary>
        private const float BandWide = 620f;
        private const float BandHigh = 116f;
        private const float BandDown = 0.60f;

        /// <summary>Frames to come up in.</summary>
        private const float FadeIn = 10f;

        private const float QuestionScale = 1.15f;
        private const float AnswerScale = 1.05f;

        private static readonly Color Parchment = new Color(232, 222, 196);
        private static readonly Color Lamp = new Color(255, 214, 150);
        private static readonly Color Ash = new Color(150, 142, 128);

        private static readonly Point16 Nothing = new Point16(-1, -1);

        /// <summary>The gate asked about, its top left tile: (-1, -1) while nothing is asked.</summary>
        private static Point16 asked = Nothing;

        private static float shown;
        private static int hovered = -1;
        private static bool wasDown;

        /// <summary>A question is up.</summary>
        public static bool Asking => asked.X >= 0;

        public override void Unload()
        {
            asked = Nothing;
        }

        public override void ClearWorld()
        {
            asked = Nothing;
        }

        /// <summary>The waygate whose top left tile this is has been right clicked, on this machine: its question.</summary>
        public static void Open(int left, int top)
        {
            if (Main.dedServ || SubworldPassage.Travelling)
                return;

            if (asked.X == left && asked.Y == top)
                return;

            asked = new Point16(left, top);
            shown = 0f;
            hovered = -1;

            // The button that is down now is not an answer.
            wasDown = Main.mouseLeft;

            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        /// <summary>Lets the question go, with the game's own sound for a closing, or none.</summary>
        private static void Close(bool sound)
        {
            asked = Nothing;
            hovered = -1;

            if (sound)
                SoundEngine.PlaySound(SoundID.MenuClose);
        }

        /// <summary>The question goes by itself when it no longer stands: the gate gone, the player gone from it, anything opened over the game.</summary>
        public override void PostUpdateEverything()
        {
            if (!Asking || Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead || me.ghost || SubworldPassage.Travelling || !Waygate.StandsAt(asked.X, asked.Y))
            {
                Close(false);
                return;
            }

            Vector2 heart = new Vector2(asked.X + Waygate.Wide * 0.5f, asked.Y + Waygate.Tall * 0.5f) * 16f;

            if (Vector2.Distance(me.Center, heart) > WalkedAway * 16f
                || Main.playerInventory || Main.mapFullscreen || Main.ingameOptionsWindow || Main.inFancyUI
                || Main.editSign || Main.editChest || Main.drawingPlayerChat || me.talkNPC >= 0 || me.chest >= 0)
            {
                Close(true);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer("PisAndDag: Waygate", () =>
            {
                Draw(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.UI));
        }

        /// <summary>
        /// The band, the question and the answers, and the answer taken if one is clicked.
        /// Screen and mouse are both in the interface's own pixels here (the layer is
        /// InterfaceScaleType.UI: PlayerInput.SetZoom_UI has divided them by Main.UIScale).
        /// </summary>
        private static void Draw(SpriteBatch batch)
        {
            bool down = Main.mouseLeft;
            bool pressed = down && !wasDown;

            wasDown = down;

            if (!Asking)
                return;

            shown = Math.Min(1f, shown + 1f / FadeIn);

            float ease = shown * shown * (3f - 2f * shown);
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            Vector2 middle = new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * BandDown);
            Rectangle band = new Rectangle((int)(middle.X - BandWide * 0.5f), (int)(middle.Y - BandHigh * 0.5f), (int)BandWide, (int)BandHigh);

            DrawBand(batch, band, ease);

            string question = Language.GetTextValue("Mods.PisAndDag.Subworlds.Waygate.Question");
            string yes = Language.GetTextValue("Mods.PisAndDag.Subworlds.Waygate.Yes");
            string no = Language.GetTextValue("Mods.PisAndDag.Subworlds.Waygate.No");

            Vector2 questionAt = new Vector2(middle.X, band.Y + BandHigh * 0.32f);
            Utils.DrawBorderString(batch, question, questionAt, Parchment * ease, QuestionScale, 0.5f, 0.5f);

            // The two answers, a third of the band apart, under the question.
            float answersY = band.Y + BandHigh * 0.72f;
            Vector2[] at = { new Vector2(middle.X - BandWide * 0.17f, answersY), new Vector2(middle.X + BandWide * 0.17f, answersY) };
            string[] words = { yes, no };

            int over = -1;

            for (int i = 0; i < 2; i++)
            {
                Vector2 size = font.MeasureString(words[i]) * AnswerScale;
                Rectangle box = new Rectangle((int)(at[i].X - size.X * 0.5f) - 18, (int)(at[i].Y - size.Y * 0.5f) - 6, (int)size.X + 36, (int)size.Y + 12);

                if (box.Contains(Main.mouseX, Main.mouseY) && PADMouse.Steering)
                    over = i;
            }

            if (over != hovered && over >= 0)
                SoundEngine.PlaySound(SoundID.MenuTick);

            hovered = over;

            for (int i = 0; i < 2; i++)
            {
                bool lit = i == hovered;

                if (lit)
                    DrawLitUnder(batch, at[i], font.MeasureString(words[i]).X * AnswerScale, ease);

                Utils.DrawBorderString(batch, words[i], at[i], (lit ? Lamp : Ash) * ease, AnswerScale * (lit ? 1.06f : 1f), 0.5f, 0.5f);
            }

            Player me = Main.LocalPlayer;

            if (band.Contains(Main.mouseX, Main.mouseY))
            {
                me.mouseInterface = true;
                me.releaseUseItem = false;
            }

            // Not taken until it has come up: a click on the gate that opened it is not an answer.
            if (!pressed || hovered < 0 || shown < 1f)
                return;

            Main.mouseLeftRelease = false;

            int gateLeft = asked.X;
            int gateTop = asked.Y;
            bool go = hovered == 0;

            Close(!go);

            if (go)
                SubworldPassage.ThroughWaygate(gateLeft, gateTop);
        }

        /// <summary>The band: black, faint at both ends, with a hair of gold over and under it, faint at the ends as well.</summary>
        private static void DrawBand(SpriteBatch batch, Rectangle band, float ease)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            const int Steps = 48;

            float step = band.Width / (float)Steps;

            for (int i = 0; i < Steps; i++)
            {
                // From either end in toward the middle: a smooth rise, flat across the middle half.
                float across = (i + 0.5f) / Steps;
                float edge = Math.Min(across, 1f - across) * 4f;
                float strength = MathHelper.Clamp(edge, 0f, 1f);

                strength = strength * strength * (3f - 2f * strength);

                int x = band.X + (int)(i * step);
                int w = (int)((i + 1) * step) - (int)(i * step);

                batch.Draw(pixel, new Rectangle(x, band.Y, w, band.Height), Color.Black * (0.78f * strength * ease));
                batch.Draw(pixel, new Rectangle(x, band.Y, w, 1), Lamp * (0.55f * strength * ease));
                batch.Draw(pixel, new Rectangle(x, band.Bottom - 1, w, 1), Lamp * (0.55f * strength * ease));
            }
        }

        /// <summary>Under the answer the cursor is on: a short line of gold, faint at its ends.</summary>
        private static void DrawLitUnder(SpriteBatch batch, Vector2 word, float wordWide, float ease)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float wide = wordWide + 30f;
            const int Steps = 16;
            float step = wide / Steps;
            float y = word.Y + 13f;

            for (int i = 0; i < Steps; i++)
            {
                float across = (i + 0.5f) / Steps;
                float strength = MathHelper.Clamp(Math.Min(across, 1f - across) * 3f, 0f, 1f);
                int x = (int)(word.X - wide * 0.5f + i * step);

                batch.Draw(pixel, new Rectangle(x, (int)y, (int)Math.Ceiling(step), 2), Lamp * (0.8f * strength * ease));
            }
        }
    }
}
