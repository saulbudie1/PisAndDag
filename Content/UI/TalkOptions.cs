using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.UI.Chat;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// One clickable answer under an NPC's dialogue.
    /// </summary>
    public sealed class TalkOption
    {
        /// <summary>What the line says. Chat tags work, so an icon can lead it.</summary>
        public readonly string Label;

        /// <summary>Whether to offer it right now. Null means always.</summary>
        public readonly Func<Player, NPC, bool> When;

        /// <summary>What happens when it is clicked. Usually a TalkOptions.Say.</summary>
        public readonly Action<NPC> Click;

        /// <summary>
        /// False for a line that is not really a line — "Leave" — so it is never marked
        /// as heard and never goes grey.
        /// </summary>
        public bool Remembered = true;

        /// <summary>
        /// Its own colour, in place of the gold: pulsed the way the gold is (Main.mouseTextColor),
        /// and still grey once heard. Null means gold.
        /// </summary>
        public Color? Colour;

        /// <summary>Label parsed once, on first draw.</summary>
        internal TextSnippet[] Snippets;

        public TalkOption(string label, Action<NPC> click, Func<Player, NPC, bool> when = null)
        {
            Label = label;
            Click = click;
            When = when;
        }
    }

    /// <summary>
    /// Extra answers under a vanilla NPC's dialogue — a third, fourth, fifth button, drawn
    /// by hand because tModLoader routes SetChatButtons to the ModNPC and nowhere else:
    ///
    ///     public static void SetChatButtons(ref string button, ref string button2)
    ///         =&gt; Main.LocalPlayer.TalkNPC?.ModNPC?.SetChatButtons(ref button, ref button2);
    ///
    /// Register(npcType, option) offers a line whenever the player talks to that NPC and
    /// the option's When says yes. Clicking usually calls Say(text, followUps): the NPC's
    /// dialogue becomes text and the follow-ups become the only options. Say(text) with no
    /// follow-ups ends the branch — no more options until the conversation moves on. It
    /// moves on when the player talks to someone else, or when the dialogue text changes
    /// under us (vanilla's own Happiness / Shop buttons, a re-talk), at which point the
    /// registered options are offered again.
    ///
    /// Looks are Main.DrawNPCChatButtons: MouseText font, gold, swells on hover, ticks on
    /// entering and leaving. Lines stack under the dialogue box's bottom-right corner.
    /// A line the player has clicked before is grey instead of gold — still clickable,
    /// just marked as heard. Heard lines are remembered per world (TalkMemory).
    /// Clicks go through PADMouse, not Main.mouseLeftRelease / IgnoreMouseInterface — see
    /// there for why neither can be used from this late a layer with the inventory shut.
    /// </summary>
    public static class TalkOptions
    {
        // ---- the dialogue box, from Main.GUIChatDrawInner (1.4.4) ----------------------
        //
        //   Draw(ChatBack, (screenWidth / 2 - ChatBack.Width() / 2, 100), (0, 0, W, (lines + 1) * 30))
        //   Draw(ChatBack, (..., 100 + (lines + 1) * 30),                (0, H - 30, W, 30))
        //
        // so it runs from y = 100 to 100 + (lines + 1) * 30 + 30, centred, texture-wide.

        private const int PanelTop = 100;
        private const int PanelLineHeight = 30;
        private const int PanelFootHeight = 30;

        /// <summary>Vanilla wraps chat at this width, over at most this many lines.</summary>
        private const int ChatWrapWidth = 460;
        private const int ChatWrapMaxLines = 10;

        // ---- where the lines sit, relative to that box --------------------------------

        /// <summary>Gap between the box's bottom edge and the first line.</summary>
        private const int GapBelowPanel = 15;

        /// <summary>How far in from the box's right edge the lines' right edges sit.</summary>
        private const int RightInset = 4;

        /// <summary>Space between stacked lines.</summary>
        internal const int RowGap = 4;

        /// <summary>Vanilla's chat buttons are 0.9. The swell is a touch under vanilla's 1.2.</summary>
        public const float BaseScale = 0.9f;
        private const float HoverScale = 1.1f;

        /// <summary>Slack around a line, so its edge is not a pixel hunt.</summary>
        private const int HitPadding = 4;

        /// <summary>
        /// Heard lines: this much of mouseTextColor on every channel. 0.6 of the pulse's
        /// 250 is the same (150, 150, 150) grey as "Hold Shift for lore".
        /// </summary>
        private const float HeardGrey = 0.6f;

        private static readonly Dictionary<int, List<TalkOption>> registry = new();

        /// <summary>Which NPC the current conversation is with. -1 when none.</summary>
        private static int talkingTo = -1;

        /// <summary>
        /// The options Say put up, replacing the registered ones. Null means "registered".
        /// Empty means "none" — the branch is over.
        /// </summary>
        private static List<TalkOption> replaced;

        /// <summary>What Say set the dialogue to; if the text drifts from it, the conversation moved on.</summary>
        private static string saidText;

        /// <summary>Index of the hovered line last frame, for the tick.</summary>
        private static int hovered = -1;

        /// <summary>Offer this line whenever the player talks to this kind of NPC.</summary>
        public static void Register(int npcType, TalkOption option)
        {
            if (!registry.TryGetValue(npcType, out List<TalkOption> list))
                registry[npcType] = list = new List<TalkOption>();

            list.Add(option);
        }

        /// <summary>Mod unload. Statics do not clear themselves.</summary>
        public static void Clear()
        {
            registry.Clear();
            scratch.Clear();
            replaced = null;
            saidText = null;
            talkingTo = -1;
            hovered = -1;
        }

        /// <summary>
        /// The NPC says this, and these become the only answers. None means the branch is
        /// over: the player gets no more lines until the conversation moves on.
        /// </summary>
        public static void Say(string text, params TalkOption[] followUps)
        {
            Main.npcChatText = text;
            saidText = text;
            replaced = new List<TalkOption>(followUps);
        }

        /// <summary>
        /// The tag for an item icon in a label, sized so it is 1:1 pixels at the scale the
        /// lines are drawn — the size the item is on the cursor. Ends in a space.
        /// </summary>
        public static string Icon(int itemType)
        {
            return "[" + PADIconTag.Name + "/s" + (1f / BaseScale).ToString("0.###", CultureInfo.InvariantCulture)
                + ":" + itemType + "] ";
        }

        /// <summary>The NPC the player is talking to, or null.</summary>
        private static NPC Talking()
        {
            Player player = Main.LocalPlayer;

            if (player == null || player.talkNPC < 0 || !Main.npc.IndexInRange(player.talkNPC))
                return null;

            NPC npc = Main.npc[player.talkNPC];

            return npc.active ? npc : null;
        }

        /// <summary>The lines to offer this frame, after keeping the conversation state honest.</summary>
        private static List<TalkOption> Current(Player player, NPC npc, List<TalkOption> into)
        {
            into.Clear();

            if (npc.whoAmI != talkingTo)
            {
                talkingTo = npc.whoAmI;
                replaced = null;
                saidText = null;
            }

            // Vanilla changed the line under us — Happiness, Shop, a re-talk. Whatever
            // branch we were in is gone; back to what is registered.
            if (replaced != null && Main.npcChatText != saidText)
            {
                replaced = null;
                saidText = null;
            }

            if (replaced != null)
            {
                into.AddRange(replaced);
                return into;
            }

            if (!registry.TryGetValue(npc.type, out List<TalkOption> options))
                return into;

            foreach (TalkOption option in options)
            {
                if (option.When == null || option.When(player, npc))
                    into.Add(option);
            }

            return into;
        }

        /// <summary>
        /// Should any lines be on screen? Vanilla's own guard for drawing the chat panel:
        /// an open chest, an open shop, or the reforge menu each suppress it. Empty text
        /// covers the repair bench too — it clears the dialogue while it is up.
        /// </summary>
        private static bool ChatVisible(Player player)
        {
            return !string.IsNullOrEmpty(Main.npcChatText)
                && player.chest == -1
                && Main.npcShop == 0
                && !Main.InReforgeMenu;
        }

        private static readonly List<TalkOption> scratch = new();

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            Player player = Main.LocalPlayer;
            NPC npc = Talking();

            if (npc == null)
            {
                talkingTo = -1;
                replaced = null;
                saidText = null;
                hovered = -1;
                return;
            }

            if (!ChatVisible(player))
            {
                hovered = -1;
                return;
            }

            // Their face on the chat box's corner, lines or no lines.
            DialoguePortrait.DrawOnChat(spriteBatch, npc);

            List<TalkOption> options = Current(player, npc, scratch);

            if (options.Count == 0)
            {
                hovered = -1;
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            // The box's height comes from how many lines the dialogue wrapped to. This is
            // the same call tModLoader's chat panel makes (Main.TextDisplayCache:
            // AmountOfLines = WordwrapStringSmart(...).Count), so the count is the panel's
            // own. The colour only tints snippets; it has no say in where the wrap falls.
            int lines = Utils.WordwrapStringSmart(Main.npcChatText, Color.White, font,
                ChatWrapWidth, ChatWrapMaxLines).Count;

            if (lines < 1)
                lines = 1;

            int panelWidth = TextureAssets.ChatBack.Width();
            int panelRight = Main.screenWidth / 2 - panelWidth / 2 + panelWidth;
            int panelBottom = PanelTop + (lines + 1) * PanelLineHeight + PanelFootHeight;

            hovered = DrawOptionLines(spriteBatch, options, npc, panelRight - RightInset,
                panelBottom + GapBelowPanel, hovered, TalkMemory.Heard);
        }

        /// <summary>
        /// The lines themselves: stacked down from <paramref name="top"/>, right edges on
        /// <paramref name="right"/>, gold or heard-grey, swelling on hover, clicking
        /// through PADMouse. Shared with Dialogue, which puts the same lines under its
        /// own panel with a memory of its own. Returns which line is hovered now; pass it
        /// back next frame so the enter/leave tick is right.
        /// </summary>
        internal static int DrawOptionLines(SpriteBatch spriteBatch, List<TalkOption> options, NPC npc,
            float right, float top, int hoveredBefore, HashSet<string> heard)
        {
            float y = top;
            int nowHovered = -1;

            for (int i = 0; i < options.Count; i++)
            {
                bool clicked = DrawOne(spriteBatch, options[i], npc, right, 1f, y, nowHovered == -1, heard,
                    out bool over, out float height);

                if (over)
                    nowHovered = i;

                y += height + RowGap;

                // The click may have replaced the list under this loop.
                if (clicked)
                    break;
            }

            // Vanilla ticks on both entering and leaving a chat button, not just entering.
            if (nowHovered != hoveredBefore)
                SoundEngine.PlaySound(SoundID.MenuTick);

            return nowHovered;
        }

        /// <summary>
        /// Two lines on one row, one in each bottom corner of a box: the first with its left
        /// edge on <paramref name="left"/>, the second with its right edge on
        /// <paramref name="right"/>. For a choice that is a fork rather than a list. Same
        /// looks, hover and click as <see cref="DrawOptionLines"/>; returns which is hovered.
        /// </summary>
        internal static int DrawOptionCorners(SpriteBatch spriteBatch, List<TalkOption> options, NPC npc,
            float left, float right, float top, int hoveredBefore, HashSet<string> heard)
        {
            int nowHovered = -1;

            for (int i = 0; i < options.Count && i < 2; i++)
            {
                bool clicked = DrawOne(spriteBatch, options[i], npc, i == 0 ? left : right, i == 0 ? 0f : 1f, top,
                    nowHovered == -1, heard, out bool over, out _);

                if (over)
                    nowHovered = i;

                if (clicked)
                    break;
            }

            if (nowHovered != hoveredBefore)
                SoundEngine.PlaySound(SoundID.MenuTick);

            return nowHovered;
        }

        /// <summary>
        /// One line: measured, hovered, drawn, clicked. It sits with the point
        /// <paramref name="alignX"/> of the way along its width (0 its left edge, 1 its right)
        /// on <paramref name="anchorX"/>, its top on <paramref name="top"/>. Only takes the
        /// hover when <paramref name="mayHover"/>, so two lines never both swell. True when it
        /// was clicked; <paramref name="height"/> is its height at rest.
        /// </summary>
        private static bool DrawOne(SpriteBatch spriteBatch, TalkOption option, NPC npc, float anchorX, float alignX,
            float top, bool mayHover, HashSet<string> heard, out bool over, out float height)
        {
            Player player = Main.LocalPlayer;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            // Gold, hovered or not — what vanilla's chat buttons are: GUIChatDrawInner sets
            // color2 = (num, num / 1.1, num / 2, num) before DrawNPCChatButtons, and
            // hovering changes their size, not their colour.
            Color gold = new Color(Main.mouseTextColor, (int)(Main.mouseTextColor / 1.1f),
                Main.mouseTextColor / 2, Main.mouseTextColor);

            // Same pulse, no colour: a line already heard.
            int greyLevel = (int)(Main.mouseTextColor * HeardGrey);
            Color grey = new Color(greyLevel, greyLevel, greyLevel, Main.mouseTextColor);

            // A line with a colour of its own wears it in the gold's place, on the same pulse.
            Color colour = gold;

            if (option.Colour is Color own)
            {
                float pulse = Main.mouseTextColor / 255f;
                colour = new Color((int)(own.R * pulse), (int)(own.G * pulse), (int)(own.B * pulse), Main.mouseTextColor);
            }

            string key = TalkMemory.Key(npc.type, option);

            if (option.Snippets == null)
            {
                // Colour here is a placeholder: the draw passes ignoreColors, so every
                // snippet takes the colour of the moment.
                option.Snippets = ChatManager.ParseMessage(option.Label, Color.White).ToArray();
                ChatManager.ConvertNormalSnippets(option.Snippets);
            }

            // Measured through the snippet path, so a leading icon counts: its
            // UniqueDraw reports its box when asked for size.
            Vector2 baseSize = ChatManager.GetStringSize(font, option.Snippets, new Vector2(BaseScale));
            height = baseSize.Y;

            // Placed by its anchor, centre anchored so the hover swell grows outward from
            // the middle rather than past the border.
            Vector2 centre = new Vector2(anchorX - baseSize.X * alignX + baseSize.X * 0.5f, top + baseSize.Y * 0.5f);

            // Screen and mouse are both in UI space here: the layer is
            // InterfaceScaleType.UI and PlayerInput.SetZoom_UI has already divided
            // Main.mouseX/Y and Main.screenWidth/Height by Main.UIScale.
            Rectangle box = new Rectangle(
                (int)(centre.X - baseSize.X * 0.5f) - HitPadding,
                (int)(centre.Y - baseSize.Y * 0.5f) - HitPadding,
                (int)baseSize.X + HitPadding * 2,
                (int)baseSize.Y + HitPadding * 2);

            over = mayHover && box.Contains(Main.mouseX, Main.mouseY) && PADMouse.Steering;

            Vector2 scale = new Vector2(BaseScale);
            Vector2 drawSize = baseSize;

            if (over)
            {
                // mouseInterface stops the click from also firing whatever the player
                // is holding; releaseUseItem stops a held button carrying over.
                player.mouseInterface = true;
                player.releaseUseItem = false;

                scale *= HoverScale;
                drawSize = ChatManager.GetStringSize(font, option.Snippets, scale);
            }

            // No origin trick: ChatManager applies origin to letters but hands a
            // UniqueDraw snippet its raw position, so an origin would split an icon
            // from its words. Top-left placed by hand keeps them together.
            Vector2 topLeft = centre - drawSize * 0.5f;

            // Shadow stays black rather than vanilla's hover-brown, on purpose: black
            // is the one colour a snippet can recognise as "this is the shadow pass".
            ChatManager.DrawColorCodedStringShadow(spriteBatch, font, option.Snippets, topLeft,
                Color.Black, 0f, Vector2.Zero, scale);
            ChatManager.DrawColorCodedString(spriteBatch, font, option.Snippets, topLeft,
                option.Remembered && heard.Contains(key) ? grey : colour, 0f, Vector2.Zero, scale,
                out _, -1f, ignoreColors: true);

            if (!over || !PADMouse.Pressed)
                return false;

            PADMouse.Consume();
            SoundEngine.PlaySound(SoundID.MenuTick);

            if (option.Remembered)
                heard.Add(key);

            option.Click?.Invoke(npc);
            return true;
        }
    }

    /// <summary>
    /// Which lines have been clicked in this world. Saved with the world, not the
    /// character: what has been asked is part of the world's timeline, like every other
    /// flag here, so a new world starts with every line gold and a second character in
    /// an old one sees what was already asked. Keyed by NPC type and label rather than
    /// by object, so a label that appears under two NPCs is two entries.
    /// </summary>
    public class TalkMemory : ModSystem
    {
        private const string SaveKey = "PADTalkHeard";

        public static readonly HashSet<string> Heard = new();

        public static string Key(int npcType, TalkOption option)
        {
            return npcType + "|" + option.Label;
        }

        public override void ClearWorld() => Heard.Clear();

        public override void SaveWorldData(TagCompound tag)
        {
            if (Heard.Count > 0)
                tag[SaveKey] = new List<string>(Heard);
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Heard.Clear();

            if (tag.ContainsKey(SaveKey))
                Heard.UnionWith(tag.GetList<string>(SaveKey));
        }
    }

    /// <summary>Hangs the answer lines, and the mod UI that lives with them, off the last interface layer.</summary>
    public class TalkOptionSystem : ModSystem
    {
        public override void Unload()
        {
            TalkOptions.Clear();
            Dialogue.Clear();
            DialoguePortrait.Clear();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Before Mouse Text rather than after Inventory: Mouse Text is last, so this
            // lands above the inventory AND above the chat panel. It is where Calamity
            // hangs its enchant UI for the same reason.
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Talk Options",
                () =>
                {
                    // Once, before anything reads it. Everything below takes its click
                    // from this.
                    PADMouse.Tick();

                    // Never both up: the bench clears the dialogue while it is open, and
                    // the lines need the dialogue. One layer serves both.
                    TalkOptions.Draw(Main.spriteBatch);
                    Dialogue.Draw(Main.spriteBatch);
                    MegasharkRepairUI.Draw(Main.spriteBatch);

                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}
