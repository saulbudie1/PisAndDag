using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.UI;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The extra answer line under the Mechanic's dialogue, offering to look at a Dented
    /// Megashark.
    ///
    /// This is drawn by hand rather than added as a third chat button, because a mod
    /// cannot add a chat button to a VANILLA npc at all. tModLoader's NPCLoader routes
    /// that hook to the ModNPC and nowhere else:
    ///
    ///     public static void SetChatButtons(ref string button, ref string button2)
    ///         =&gt; Main.LocalPlayer.TalkNPC?.ModNPC?.SetChatButtons(ref button, ref button2);
    ///
    /// There is no GlobalNPC.SetChatButtons to override — only PreChatButtonClicked and
    /// OnChatButtonClicked, which react to the two buttons vanilla already drew. So the
    /// line has to be ours.
    ///
    /// How it LOOKS is lifted from Main.DrawNPCChatButtons: same font, same 1.2× swell on
    /// hover, same gold-on-brown while hovered, same tick on entering and on leaving, same
    /// centred draw so the swell grows outward instead of shoving the line sideways. It is
    /// drawn smaller than the real buttons on purpose — it is an aside, not a third one —
    /// and it hangs off the dialogue box's bottom-right corner rather than the button row.
    /// How it takes a CLICK is not vanilla's — see PADMouse for why neither
    /// Main.mouseLeftRelease nor PlayerInput.IgnoreMouseInterface can be used here.
    /// </summary>
    public static class MechanicRepairPrompt
    {
        private const string PromptText = "About this dented Megashark...";

        // ---- the dialogue box, from Main.GUIChatDrawInner (1.4.4) ----------------------
        //
        //   spriteBatch.Draw(TextureAssets.ChatBack.Value,
        //       new Vector2(screenWidth / 2 - ChatBack.Width() / 2, 100f),
        //       new Rectangle(0, 0, ChatBack.Width(), (amountOfLines + 1) * 30), ...);
        //   spriteBatch.Draw(TextureAssets.ChatBack.Value,
        //       new Vector2(screenWidth / 2 - ChatBack.Width() / 2, 100 + (amountOfLines + 1) * 30),
        //       new Rectangle(0, ChatBack.Height() - 30, ChatBack.Width(), 30), ...);
        //
        // So the box is the ChatBack texture, centred, from y = 100 down to
        // 100 + (lines + 1) * 30 + 30. Its width is the texture's, read at runtime rather
        // than copied as a number.

        private const int PanelTop = 100;
        private const int PanelLineHeight = 30;
        private const int PanelFootHeight = 30;

        /// <summary>Vanilla wraps chat at this width, over at most this many lines.</summary>
        private const int ChatWrapWidth = 460;
        private const int ChatWrapMaxLines = 10;

        // ---- where the line sits, relative to that box -------------------------------
        //
        // These two are the whole position. The hit box is measured from the same anchor,
        // so it cannot drift away from the text.

        /// <summary>Gap between the box's bottom edge and the top of the line.</summary>
        private const int PromptGapBelowPanel = 18;

        /// <summary>
        /// How far in from the box's right edge the line's right edge sits. Zero puts the
        /// last glyph flush with the border.
        /// </summary>
        private const int PromptRightInset = 8;

        /// <summary>
        /// Vanilla's chat buttons are 0.9; this is eight-tenths of that. The hover swell is
        /// vanilla's 1.2×.
        /// </summary>
        private const float BaseScale = 0.8f;
        private const float HoverScale = 1.2f;

        /// <summary>Slack around the text, so the edge of the line is not a pixel hunt.</summary>
        private const int HitPadding = 4;

        /// <summary>Tracks hover across frames so the tick only plays on a change.</summary>
        private static bool hovering;

        /// <summary>True when the player is carrying something for the Mechanic to see.</summary>
        public static bool CarryingDented(Player player)
        {
            int dented = ModContent.ItemType<DentedMegashark>();

            // 58 rather than the whole array: 58 is vanilla's scratch slot for the item on
            // the cursor, and everything past it is armour and accessories.
            for (int i = 0; i < 58; i++)
            {
                Item item = player.inventory[i];

                if (item != null && !item.IsAir && item.type == dented)
                    return true;
            }

            return false;
        }

        /// <summary>The Mechanic the player is currently talking to, or null.</summary>
        public static NPC TalkingMechanic()
        {
            Player player = Main.LocalPlayer;

            if (player == null || player.talkNPC < 0 || !Main.npc.IndexInRange(player.talkNPC))
                return null;

            NPC npc = Main.npc[player.talkNPC];

            return npc.active && npc.type == NPCID.Mechanic ? npc : null;
        }

        /// <summary>
        /// Should the line be on screen?
        ///
        /// The chat-visible conditions are vanilla's own, from the guard that decides
        /// whether to draw the chat panel at all: an open chest, an open shop, or the
        /// reforge menu each suppress it.
        /// </summary>
        private static bool ShouldShow()
        {
            if (MegasharkRepairUI.Open)
                return false;

            Player player = Main.LocalPlayer;

            return TalkingMechanic() != null
                && !string.IsNullOrEmpty(Main.npcChatText)
                && player.chest == -1
                && Main.npcShop == 0
                && !Main.InReforgeMenu
                && CarryingDented(player);
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.dedServ || !ShouldShow())
            {
                hovering = false;
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            // The box's height comes from how many lines the dialogue wrapped to. This is
            // the same call tModLoader's chat panel makes (Main.TextDisplayCache in
            // Main.cs.patch: AmountOfLines = WordwrapStringSmart(...).Count), so the count
            // is the panel's own, not a lookalike. The colour only tints snippets; it has
            // no say in where the wrap falls.
            int lines = Utils.WordwrapStringSmart(Main.npcChatText, Color.White, font,
                ChatWrapWidth, ChatWrapMaxLines).Count;

            if (lines < 1)
                lines = 1;

            int panelWidth = TextureAssets.ChatBack.Width();
            int panelRight = Main.screenWidth / 2 - panelWidth / 2 + panelWidth;
            int panelBottom = PanelTop + (lines + 1) * PanelLineHeight + PanelFootHeight;

            Vector2 scale = new Vector2(BaseScale);

            // GetStringSize returns the size ALREADY multiplied by the scale it is given
            // (LayoutSnippets does font.MeasureString(line) * scale), so this is the drawn
            // extent in pixels, not a nominal size waiting to be scaled.
            Vector2 size = ChatManager.GetStringSize(font, PromptText, scale);

            // Right-aligned to the box: the line's last glyph sits on the border.
            Vector2 topLeft = new Vector2(
                panelRight - PromptRightInset - size.X,
                panelBottom + PromptGapBelowPanel);

            // Screen and mouse are both in UI space here: the layer is InterfaceScaleType.UI
            // and PlayerInput.SetZoom_UI has already divided Main.mouseX/Y AND
            // Main.screenWidth/Height by Main.UIScale (SetZoom_Scaled(1f / Main.UIScale)).
            // So a raw comparison is correct at any UI scale — no conversion, and none
            // wanted.
            Rectangle box = new Rectangle(
                (int)topLeft.X - HitPadding,
                (int)topLeft.Y - HitPadding,
                (int)size.X + HitPadding * 2,
                (int)size.Y + HitPadding * 2);

            bool over = box.Contains(Main.mouseX, Main.mouseY) && PADMouse.Steering;

            if (over)
            {
                // mouseInterface is what stops the click from also firing whatever the
                // player is holding, and releaseUseItem stops a held button carrying over.
                Main.LocalPlayer.mouseInterface = true;
                Main.LocalPlayer.releaseUseItem = false;
                scale *= HoverScale;
            }

            // Vanilla ticks on both entering and leaving a chat button, not just entering.
            if (over != hovering)
                SoundEngine.PlaySound(SoundID.MenuTick);

            hovering = over;

            // Gold while hovered, the pulsing tone vanilla gives its shop button; plain
            // chat colour otherwise. The shadow warms to brown on hover exactly as the
            // real buttons do.
            Color text = over
                ? new Color(Main.mouseTextColor, (int)(Main.mouseTextColor / 1.1f), Main.mouseTextColor / 2, Main.mouseTextColor)
                : new Color(Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor);

            Color shadow = over ? Color.Brown : Color.Black;

            // Vanilla's arithmetic, unchanged: position = topLeft + size * 0.5,
            // origin = size * 0.5. Drawn about its own centre so the hover swell grows
            // outward instead of pushing the line past the border.
            Vector2 centre = topLeft + size * 0.5f;
            Vector2 origin = size * 0.5f;

            ChatManager.DrawColorCodedStringShadow(spriteBatch, font, PromptText, centre,
                shadow, 0f, origin, scale);
            ChatManager.DrawColorCodedString(spriteBatch, font, PromptText, centre,
                text, 0f, origin, scale);

            if (over && PADMouse.Pressed)
                Click();
        }

        private static void Click()
        {
            NPC mechanic = TalkingMechanic();

            if (mechanic == null)
                return;

            PADMouse.Consume();
            SoundEngine.PlaySound(SoundID.MenuTick);

            // Show clears the dialogue and opens the inventory — the tinkerer's move.
            MegasharkRepairUI.Show(mechanic.whoAmI);
        }
    }
}
