using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;
using Terraria.UI.Gamepad;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// A townsperson's conversation, in the mod's own panel.
    ///
    /// Vanilla's parchment box at the top of the screen goes, and the townsperson speaks from
    /// the same dark panel low on the screen that Dialogue gives everyone else: their face on
    /// the corner, a tab over the top edge with their name and what they are ("Andrew" and
    /// "the Guide", as the game names them), the line arriving a letter at a time and a click
    /// on the panel bringing the rest of it at once. Everything there is to answer stands in
    /// one list down the panel's right side, once the line has all arrived: vanilla's own
    /// buttons (Shop, the Nurse's heal and its price, the Angler's quest, Reforge, Happiness,
    /// Close, any mod's), and TalkOptions' lines with them, gold, grey once heard.
    ///
    /// None of vanilla's conversation is rewritten. Main.GUIChatDrawInner still runs every
    /// frame: it still works out every button's label (tModLoader's SetChatButtons with it),
    /// and it still does everything a click does, off the four npcChatFocus flags as it always
    /// has. What changes is only where things are and what draws them (EditChat):
    ///
    ///   - the two parchment draws and the corner item's two draws do nothing (ChatDraw);
    ///   - the text lines draw nothing and report nothing hovered (ChatLine): the panel draws
    ///     the line itself, item tags and all, and hands a hovered tag its hover and click;
    ///   - the corner item is put in the panel's text column (CornerAt), where vanilla's own
    ///     hover and click for it then find it;
    ///   - DrawNPCChatButtons is replaced by <see cref="Buttons"/>, which sets the state it set
    ///     (the four focus flags, the tick on entering and leaving, mouseInterface and
    ///     releaseUseItem, the gamepad's link points) for buttons laid out in the panel, and
    ///     draws nothing;
    ///   - the Tax Collector's coins are kept for the panel to draw beside his button (Money);
    ///   - the box a click must land in is the panel (ClickArea).
    ///
    /// The drawing happens later in the frame, from TalkOptionSystem's layer (<see cref="Draw"/>),
    /// over the layout this frame's GUIChatDrawInner made. A sign is none of this: every seam
    /// hands a sign straight to vanilla. So is a townsperson while the Dialogue panel is up:
    /// they keep vanilla's box at the top for as long as it is, as they did before this.
    ///
    /// If the method is not as expected when the mod loads (another mod has been at it), the
    /// edit is not made, a warning is logged and vanilla's box is left as it was. Every seam is
    /// a pass-through until the whole edit has gone in, so there is no half of it.
    ///
    /// The Goblin's reforge, which vanilla runs with the box cleared away, keeps the panel up
    /// with the last line on it and puts the reforge slot in a sub-box standing on the panel's
    /// top edge (ReforgeBox, ChatSubBox). A shop does the same with the panel itself: the panel
    /// stays up, grown round the shop's grid where the line was, with Close beside it (ShopBox).
    /// Both are vanilla's own code, moved; their slots wear the panel's frame (ChatSlot).
    ///
    /// The list shows six at most. With more, a triangle under it pages it down a row at a time,
    /// and another back up once it has moved; the mouse wheel over the list does the same (the
    /// hotbar is told to leave that turn of the wheel alone), and the gamepad brings a button
    /// into view by moving to it. A button out of view can be neither hovered nor focused.
    /// </summary>
    public static class TownChat
    {
        // ============================================================
        // THE PANEL
        // ============================================================

        /// <summary>Text wraps at this. Vanilla wraps at 460; this is a little under, for the column beside it.</summary>
        private const int TextWidth = 440;
        private const int MinTextWidth = 240;

        /// <summary>The answers' column, down the right side.</summary>
        private const int OptionsWidth = 236;

        /// <summary>Between the text and the answers.</summary>
        private const int ColumnGap = 28;

        /// <summary>Dialogue's insets and its place on the screen, so the two panels are one panel.</summary>
        private const int PadX = 24;
        private const int PadTop = 18;
        private const int PadBottom = 16;
        private const int BottomMargin = 118;

        /// <summary>The least room left either side of the panel on a narrow screen.</summary>
        private const int ScreenMargin = 16;

        /// <summary>TalkPanel.png's 12px ends, as Dialogue draws it.</summary>
        private const int SliceEnd = 12;

        /// <summary>Vanilla stops at ten lines at 460 wide; a narrower column gets a couple more before it stops.</summary>
        private const int MaxLines = 12;

        /// <summary>The corner item's box is fitted to this, as vanilla fits it, and stands this far under the text.</summary>
        private const int CornerSize = 32;
        private const int CornerGap = 4;

        /// <summary>Vanilla buttons swell this much on hover: TalkOptions' swell, so every answer swells alike.</summary>
        private const float HoverScale = 1.1f;

        /// <summary>Slack round a vanilla button, sideways and up and down. Half the row gap up and down, so two never overlap.</summary>
        private const int HitPadX = 4;
        private const int HitPadY = TalkOptions.RowGap / 2;

        /// <summary>The line's colour, and the tab's: Dialogue's lore colour.</summary>
        private static readonly Color TextColour = new Color(196, 180, 152);

        // ---- the list ----------------------------------------------------------------------

        /// <summary>The most rows the list shows at once.</summary>
        private const int MaxRows = 6;

        /// <summary>ChatArrow.png: two frames one over the other, at rest and hovered, each this big. Pointing down; flipped for up.</summary>
        private const int ArrowWidth = 14;
        private const int ArrowHeight = 8;

        /// <summary>Between the last row and the arrows, between the two arrows, and the slack round each for the mouse.</summary>
        private const int ArrowGap = 6;
        private const int ArrowSpacing = 10;
        private const int ArrowSlack = 4;

        /// <summary>
        /// A shop's "Shop" and savings, at the foot of the answers' column: the title at the top,
        /// ItemSlot.DrawSavings' coins 50 under it, so the lowest of it is about this far down.
        /// </summary>
        private const int TitleBlock = 62;

        // ---- the name tab, Dialogue's ----------------------------------------------------

        private const int TabGap = 6;
        private const int TabHeight = 30;
        private const int TabTuck = 4;
        private const int TabPadX = 12;
        private const float TabScale = 0.85f;
        private static readonly Color TitleColour = new Color(150, 138, 118);

        // ============================================================
        // STATE
        // ============================================================

        /// <summary>The whole edit to GUIChatDrawInner went in. Until it has, every seam hands straight back to vanilla.</summary>
        internal static bool Applied;

        private static Action<int, Color, int, string, string> vanillaButtons;

        private static Asset<Texture2D> panelTexture;

        /// <summary>GUIChatDrawInner ran for a townsperson this frame, and laid the panel out.</summary>
        private static bool chatRan;

        /// <summary>The reforge block put its slot in the sub-box this frame, and laid the panel out (ReforgeBox).</summary>
        private static bool reforgeRan;

        /// <summary>The shop block put its grid in the panel this frame, and drew the panel under it (ShopBox).</summary>
        private static bool shopRan;

        /// <summary>The panel has this frame's conversation: TalkOptions leaves its lines to it.</summary>
        public static bool HasConversation => chatRan || reforgeRan || shopRan;

        /// <summary>What the panel holds: the line, the line with the reforge over it, or a shop.</summary>
        private enum Mode
        {
            Chat,
            Reforge,
            Shop,
        }

        /// <summary>What it held the last frame it was up, for how it fades.</summary>
        private static Mode lastMode;

        private static Asset<Texture2D> arrowTexture;

        /// <summary>The first row the list shows.</summary>
        private static int scroll;

        /// <summary>The list has more rows than it shows, and where its two arrows are.</summary>
        private static bool scrolls;
        private static Rectangle arrowUp;
        private static Rectangle arrowDown;
        private static bool upHovered;
        private static bool downHovered;

        /// <summary>The list's column, for the wheel; and whether the mouse was over it the last frame drawn.</summary>
        private static Rectangle listArea;
        private static bool wheelOurs;

        /// <summary>Where a shop's "Shop" and savings go.</summary>
        private static Vector2 titleAt;

        /// <summary>The line as vanilla showed it, and who said it.</summary>
        private static string said;
        private static int saidBy = -1;

        /// <summary>The line broken to the column: the snippets of each line, and how many letters each holds.</summary>
        private static readonly List<TextSnippet[]> lines = new();
        private static readonly List<int> lineLetters = new();
        private static readonly List<bool[]> glyphs = new();
        private static int letterCount;
        private static int wrappedAt = -1;

        private static uint lineStart;

        private static float fade;

        /// <summary>Who spoke last, for the face and tab while the panel fades after them.</summary>
        private static int faceIndex = -1;
        private static int faceType = -1;

        /// <summary>The panel as last laid out, and where its text column runs.</summary>
        private static Rectangle panel;
        private static int textLeft;
        private static int textWidth = TextWidth;

        /// <summary>The answers are up: the line has all arrived. Decided where vanilla's state is set, and drawn to match.</summary>
        private static bool optionsUp;

        /// <summary>The Tax Collector's coins, where vanilla put them relative to his button.</summary>
        private static bool moneyUp;
        private static Vector2 moneyOffset;
        private static int[] moneyCoins;
        private static Vector2 vanillaOrigin;

        /// <summary>The corner item, kept rather than made new every frame as vanilla makes it.</summary>
        private static Item cornerItem;

        /// <summary>Which TalkOptions line is hovered, for the tick; and whether the reforge's Close is.</summary>
        private static int extraHovered = -1;
        private static bool leaveHovered;

        /// <summary>The link points' Up and Down are wired to the list; put back on a sign.</summary>
        private static bool linksOurs;

        private enum RowKind
        {
            /// <summary>Vanilla's first button (focusText): Shop, Heal, Quest, Help, Pet... npcChatFocus2, point 2500.</summary>
            First,

            /// <summary>Vanilla's second (focusText3): Reforge, Crafting, Decor, Hair Style... npcChatFocus3, point 2502.</summary>
            Second,

            /// <summary>A TalkOptions line.</summary>
            Extra,

            /// <summary>npcChatFocus4, point 2503.</summary>
            Happiness,

            /// <summary>npcChatFocus1, point 2501.</summary>
            Close,

            /// <summary>Close under the reforge sub-box, where vanilla has no box and so no button: Main.CloseNPCChatOrSign, as vanilla's Close.</summary>
            Leave,
        }

        private struct Row
        {
            public RowKind Kind;
            public string Label;
            public TalkOption Option;
            public Color Colour;
            public float Scale;
            public Vector2 Size;
            public Vector2 TopLeft;
            public Rectangle Hit;

            /// <summary>In the six the list shows. A row out of view has no Hit.</summary>
            public bool Shown;
        }

        private static readonly List<Row> rows = new();
        private static readonly List<TalkOption> extras = new();

        /// <summary>Mod unload. Statics do not clear themselves.</summary>
        internal static void Clear()
        {
            VanillaLinks();
            Applied = false;
            vanillaButtons = null;
            panelTexture = null;
            arrowTexture = null;
            chatRan = false;
            reforgeRan = false;
            shopRan = false;
            scroll = 0;
            scrolls = false;
            wheelOurs = false;
            said = null;
            saidBy = -1;
            lines.Clear();
            lineLetters.Clear();
            glyphs.Clear();
            letterCount = 0;
            wrappedAt = -1;
            fade = 0f;
            faceIndex = -1;
            faceType = -1;
            rows.Clear();
            extras.Clear();
            moneyCoins = null;
            cornerItem = null;
        }

        /// <summary>
        /// This frame's conversation is a townsperson's, and the panel has it. False for a sign,
        /// for nobody, and while the Dialogue panel is up: two panels in one place would be one
        /// on top of the other, so the townsperson keeps vanilla's box at the top until it goes.
        /// </summary>
        private static bool Takes()
        {
            if (!Applied || Main.gameMenu || Main.dedServ)
                return false;

            Player player = Main.LocalPlayer;

            if (player.sign > -1 || player.talkNPC < 0 || player.talkNPC >= Main.maxNPCs || !Main.npc[player.talkNPC].active)
                return false;

            return !Dialogue.Showing;
        }

        /// <summary>
        /// The reforge has the panel this frame: open, for a townsperson, with no line of vanilla's
        /// up over it. The frame Reforge is clicked counts: the box ran, but the click cleared its
        /// line. A mod that puts a line up during the reforge gets the line, and vanilla's slot.
        /// </summary>
        private static bool TakesReforge()
        {
            return ReforgeBox.Applied && Main.InReforgeMenu && (!chatRan || string.IsNullOrEmpty(Main.npcChatText)) && Takes();
        }

        /// <summary>The shop has the panel this frame: one is open, for a townsperson, with no line of vanilla's up over it.</summary>
        private static bool TakesShop()
        {
            return ShopBox.Applied && Main.npcShop > 0 && (!chatRan || string.IsNullOrEmpty(Main.npcChatText)) && Takes();
        }

        // ============================================================
        // THE LINE
        // ============================================================

        /// <summary>The line vanilla is showing this frame, from GUIChatDrawInner, as it hands it to its text cache.</summary>
        public static void Capture(string text)
        {
            if (!Takes())
                return;

            int who = Main.LocalPlayer.talkNPC;
            text = Lang.SupportGlyphs(text ?? "");

            if (text == said && who == saidBy)
                return;

            said = text;
            saidBy = who;
            wrappedAt = -1;
            lineStart = Main.GameUpdateCount;

            // A new line brings its own answers, read from the top.
            scroll = 0;

            // A panel still fading up would swallow the first words, as Dialogue finds.
            if (fade <= 0f)
                lineStart += Dialogue.FadeTicks;
        }

        /// <summary>Breaks the line to the column. Item tags and colour tags come through as vanilla's own snippets.</summary>
        private static void Wrap(int width)
        {
            if (wrappedAt == width)
                return;

            wrappedAt = width;
            lines.Clear();
            lineLetters.Clear();
            glyphs.Clear();
            letterCount = 0;

            if (string.IsNullOrEmpty(said))
                return;

            List<List<TextSnippet>> wrapped = Utils.WordwrapStringSmart(said, TextColour, FontAssets.MouseText.Value, width, MaxLines);

            foreach (List<TextSnippet> line in wrapped)
            {
                TextSnippet[] snippets = line.ToArray();
                bool[] glyph = new bool[snippets.Length];
                int letters = 0;

                for (int i = 0; i < snippets.Length; i++)
                {
                    // An icon arrives as one letter; words a letter at a time.
                    glyph[i] = snippets[i].UniqueDraw(true, out _, null);
                    letters += glyph[i] ? 1 : snippets[i].Text.Length;
                }

                lines.Add(snippets);
                lineLetters.Add(letters);
                glyphs.Add(glyph);
                letterCount += letters;
            }
        }

        /// <summary>How much of the line has arrived, in letters. At Dialogue's default pace.</summary>
        private static int Arrived()
        {
            // Unsigned: a start still in the future must read as nothing yet, not everything.
            if (Main.GameUpdateCount < lineStart)
                return 0;

            float budget = (Main.GameUpdateCount - lineStart) * Dialogue.DefaultLettersPerTick;
            return (int)Math.Min(budget, letterCount);
        }

        /// <summary>The first <paramref name="budget"/> letters of one line: whole snippets, then the start of one.</summary>
        private static TextSnippet[] Part(int line, int budget)
        {
            TextSnippet[] snippets = lines[line];
            bool[] glyph = glyphs[line];
            List<TextSnippet> part = new();

            for (int i = 0; i < snippets.Length && budget > 0; i++)
            {
                int letters = glyph[i] ? 1 : snippets[i].Text.Length;

                if (budget >= letters)
                {
                    part.Add(snippets[i]);
                    budget -= letters;
                    continue;
                }

                if (!glyph[i])
                    part.Add(snippets[i].CopyMorph(snippets[i].Text.Substring(0, budget)));

                break;
            }

            return part.ToArray();
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        /// <summary>Where the panel stands on this screen: its left edge, its width, and its text column's width.</summary>
        private static void Columns(out int x, out int width, out int text)
        {
            text = TextWidth;
            width = PadX + text + ColumnGap + OptionsWidth + PadX;

            int room = Main.screenWidth - ScreenMargin * 2;

            if (width > room)
            {
                text = Math.Max(MinTextWidth, text - (width - room));
                width = PadX + text + ColumnGap + OptionsWidth + PadX;
            }

            x = (Main.screenWidth - width) / 2;
        }

        /// <summary>
        /// Lays the panel out round what it holds and the rows: the panel's bottom stays put and it
        /// grows upwards. A shop's grid (<paramref name="content"/>) stands where the line would,
        /// and the text column is never narrower than it.
        /// </summary>
        private static void Layout(Mode mode, Point content = default)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            Columns(out int x, out int width, out textWidth);

            if (content.X > textWidth)
            {
                width += content.X - textWidth;
                textWidth = content.X;
                x = (Main.screenWidth - width) / 2;
            }

            float textHeight;

            if (mode == Mode.Shop)
            {
                textHeight = content.Y;
            }
            else
            {
                Wrap(textWidth);
                textHeight = Math.Max(1, lines.Count) * font.LineSpacing;

                if (mode == Mode.Chat && Main.npcChatCornerItem != 0)
                    textHeight += CornerGap + CornerSize;
            }

            // Six rows at most; past that the list shows a window of six, with the arrows under it.
            scrolls = rows.Count > MaxRows;
            scroll = scrolls ? Math.Clamp(scroll, 0, rows.Count - MaxRows) : 0;

            float optionsHeight = 0f;
            int shown = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                if (row.Kind == RowKind.Extra)
                {
                    row.Scale = TalkOptions.BaseScale;
                    row.Size = ChatManager.GetStringSize(font, Snippets(row.Option), new Vector2(TalkOptions.BaseScale));
                }
                else
                {
                    // A label too long for the column is drawn smaller, as vanilla squeezes one past 260.
                    Vector2 size = ChatManager.GetStringSize(font, row.Label, new Vector2(TalkOptions.BaseScale));
                    float fit = size.X > OptionsWidth ? OptionsWidth / size.X : 1f;
                    row.Scale = TalkOptions.BaseScale * fit;
                    row.Size = size * fit;
                }

                row.Shown = i >= scroll && i < scroll + MaxRows;

                if (row.Shown)
                {
                    optionsHeight += row.Size.Y + (shown > 0 ? TalkOptions.RowGap : 0);
                    shown++;
                }

                rows[i] = row;
            }

            // The window keeps the height of its tallest six, so paging never moves the panel.
            if (scrolls)
                optionsHeight = Math.Max(optionsHeight, TallestWindow()) + ArrowGap + ArrowHeight;

            if (mode == Mode.Shop)
                optionsHeight += TalkOptions.RowGap + TitleBlock;

            int height = PadTop + (int)Math.Ceiling(Math.Max(textHeight, optionsHeight)) + PadBottom;
            int y = Main.screenHeight - BottomMargin - height;

            panel = new Rectangle(x, y, width, height);
            textLeft = x + PadX;

            float left = panel.Right - PadX - OptionsWidth;
            float top = panel.Y + PadTop;
            float windowBottom = top + (scrolls ? TallestWindow() : optionsHeight);

            arrowUp = new Rectangle((int)left, (int)(windowBottom + ArrowGap), ArrowWidth, ArrowHeight);
            arrowDown = new Rectangle(arrowUp.Right + ArrowSpacing, arrowUp.Y, ArrowWidth, ArrowHeight);

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                if (row.Shown)
                {
                    row.TopLeft = new Vector2((float)Math.Floor(left), (float)Math.Floor(top));
                    row.Hit = new Rectangle((int)row.TopLeft.X - HitPadX, (int)row.TopLeft.Y - HitPadY,
                        (int)row.Size.X + HitPadX * 2, (int)row.Size.Y + HitPadY * 2);
                    top += row.Size.Y + TalkOptions.RowGap;
                }
                else
                {
                    // Out of view: nothing to hit, and a gamepad sent to it is sent towards the arrow
                    // that would bring it (and once it is current, Reveal brings it).
                    Rectangle arrow = i < scroll ? arrowUp : arrowDown;
                    row.TopLeft = new Vector2(arrow.X, arrow.Y) - row.Size * 0.5f + new Vector2(ArrowWidth, ArrowHeight) * 0.5f;
                    row.Hit = Rectangle.Empty;
                }

                rows[i] = row;
            }

            listArea = new Rectangle((int)left - HitPadX, panel.Y + PadTop - HitPadY, OptionsWidth + HitPadX * 2,
                (int)Math.Ceiling(optionsHeight) + HitPadY * 2);

            // A shop's "Shop" and savings stand at the foot of the column, level with the grid's foot.
            titleAt = new Vector2((float)Math.Floor(left), panel.Bottom - PadBottom - TitleBlock);
        }

        /// <summary>The height of the tallest six rows in a row: the list's window, whichever six it shows.</summary>
        private static float TallestWindow()
        {
            float tallest = 0f;

            for (int first = 0; first + MaxRows <= rows.Count; first++)
            {
                float height = 0f;

                for (int i = first; i < first + MaxRows; i++)
                    height += rows[i].Size.Y + (i > first ? TalkOptions.RowGap : 0);

                tallest = Math.Max(tallest, height);
            }

            return tallest;
        }

        /// <summary>A TalkOptions line's snippets, parsed as TalkOptions parses them.</summary>
        private static TextSnippet[] Snippets(TalkOption option)
        {
            if (option.Snippets == null)
            {
                option.Snippets = ChatManager.ParseMessage(option.Label, Color.White).ToArray();
                ChatManager.ConvertNormalSnippets(option.Snippets);
            }

            return option.Snippets;
        }

        private static void Add(RowKind kind, string label, Color colour)
        {
            rows.Add(new Row { Kind = kind, Label = label, Colour = colour });
        }

        /// <summary>Where the corner item's bottom-right corner goes: the foot of the text column.</summary>
        private static Vector2 CornerPosition()
        {
            Columns(out int x, out _, out int text);
            return new Vector2(x + PadX + text, Main.screenHeight - BottomMargin - PadBottom);
        }

        /// <summary>
        /// The panel laid out for the reforge, from ReforgeBox in the inventory's draw: the last
        /// line, all of it, and Close. False when the reforge is not the panel's this frame.
        /// </summary>
        internal static bool LayoutForReforge(out Rectangle at)
        {
            at = Rectangle.Empty;

            if (!TakesReforge())
                return false;

            // A line said by someone else is not this one's to show.
            if (saidBy != Main.LocalPlayer.talkNPC)
            {
                said = null;
                saidBy = Main.LocalPlayer.talkNPC;
                wrappedAt = -1;
            }

            rows.Clear();
            extras.Clear();
            Add(RowKind.Leave, Lang.inter[52].Value, Gold(Main.mouseTextColor));
            Layout(Mode.Reforge);

            reforgeRan = true;
            at = panel;
            return true;
        }

        /// <summary>
        /// The panel laid out round a shop's grid, from ShopBox at the top of the shop's block in
        /// the inventory's draw, and drawn there and then (face, tab and panel) so the grid lands on
        /// it: where the grid's top-left goes, and the "Shop" title and savings. False when the shop
        /// is not the panel's this frame.
        /// </summary>
        internal static bool LayoutForShop(Point gridSize, out Point grid, out Vector2 title)
        {
            grid = Point.Zero;
            title = Vector2.Zero;

            if (!TakesShop())
                return false;

            NPC npc = Main.LocalPlayer.TalkNPC;

            rows.Clear();
            extras.Clear();
            Add(RowKind.Leave, Lang.inter[52].Value, Gold(Main.mouseTextColor));
            Layout(Mode.Shop, gridSize);

            panelTexture ??= ModContent.Request<Texture2D>("PisAndDag/Content/UI/TalkPanel");
            DrawFrame(Main.spriteBatch, npc, panel, 1f);

            shopRan = true;
            grid = new Point(textLeft, panel.Y + PadTop);
            title = titleAt;
            return true;
        }

        private static Color Gold(int pulse) => new Color(pulse, (int)(pulse / 1.1), pulse / 2, pulse);

        // ============================================================
        // THE SEAMS — called from inside GUIChatDrawInner
        // ============================================================

        /// <summary>In place of the parchment's two draws and the corner item's two: nothing, for a townsperson.</summary>
        public static void ChatDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle? sourceRectangle,
            Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            if (Takes())
                return;

            spriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
        }

        /// <summary>In place of each text line's draw: nothing, and nothing hovered, for a townsperson. The panel draws the line.</summary>
        public static Vector2 ChatLine(SpriteBatch spriteBatch, DynamicSpriteFont font, TextSnippet[] snippets, Vector2 position,
            float rotation, Color color, Color shadowColor, Vector2 origin, Vector2 baseScale, out int hoveredSnippet,
            float maxWidth, float spread)
        {
            if (Takes())
            {
                hoveredSnippet = -1;
                return position;
            }

            return ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, snippets, position, rotation, color, shadowColor,
                origin, baseScale, out hoveredSnippet, maxWidth, spread);
        }

        /// <summary>The corner item's place, after vanilla has worked out its own: the foot of the panel's text column.</summary>
        public static Vector2 CornerAt(Vector2 vanilla)
        {
            return Takes() ? CornerPosition() : vanilla;
        }

        /// <summary>
        /// In place of Main.DrawNPCChatButtons. For a sign, that. For a townsperson, the same state
        /// it sets, for the buttons where the panel has them: read off the original, button by
        /// button, in its order.
        ///
        ///   focusText (when it has any text)  npcChatFocus2   point 2500, NPCCHAT_ButtonsLeft
        ///   Close, always                     npcChatFocus1   point 2501, NPCCHAT_ButtonsMiddle
        ///   focusText3, unless blank          npcChatFocus3   point 2502, NPCCHAT_ButtonsRight (false when blank)
        ///   Happiness, unless remixWorld      npcChatFocus4   point 2503, NPCCHAT_ButtonsRight2 (false with no report;
        ///                                                     in a remix world neither is touched, as vanilla leaves them)
        ///
        /// Hovered means the mouse on it and PlayerInput.IgnoreMouseInterface false; a hovered
        /// button sets mouseInterface and clears releaseUseItem, and every button ticks on
        /// entering and on leaving. Vanilla's click handling after this call reads only the four
        /// flags (and the click box, ClickArea), so each button does exactly what it did.
        ///
        /// While the line is still arriving the buttons are not up: none is hovered, so a click
        /// does nothing but bring the line.
        /// </summary>
        public static void Buttons(int superColor, Color chatColor, int numLines, string focusText, string focusText3)
        {
            if (!Takes())
            {
                VanillaLinks();
                vanillaButtons(superColor, chatColor, numLines, focusText, focusText3);
                return;
            }

            chatRan = true;
            moneyUp = false;
            vanillaOrigin = new Vector2(180 + (Main.screenWidth - 800) / 2, 130 + numLines * 30);

            Player player = Main.LocalPlayer;
            NPC npc = player.TalkNPC;

            bool first = !string.IsNullOrEmpty(focusText);
            bool second = !string.IsNullOrWhiteSpace(focusText3);
            bool happiness = !Main.remixWorld && player.currentShoppingSettings.HappinessReport != "";

            rows.Clear();

            if (first)
                Add(RowKind.First, focusText, chatColor);

            if (second)
                Add(RowKind.Second, focusText3, chatColor);

            // TalkOptions' lines, under vanilla's own and over Happiness and Close. Under the same
            // guard TalkOptions keeps for them: no chest, no shop, no reforge.
            extras.Clear();

            if (player.chest == -1 && Main.npcShop == 0 && !Main.InReforgeMenu)
                TalkOptions.Current(player, npc, extras);

            foreach (TalkOption option in extras)
                rows.Add(new Row { Kind = RowKind.Extra, Option = option });

            if (happiness)
                Add(RowKind.Happiness, Language.GetTextValue("UI.NPCCheckHappiness"), Gold(superColor));

            Add(RowKind.Close, Lang.inter[52].Value, Gold(superColor));

            Reveal();
            Layout(Mode.Chat);
            optionsUp = Arrived() >= letterCount;

            // Which one is under the mouse: the first in the list, so two never both are. A row out
            // of view is never it.
            int over = -1;

            if (optionsUp && !PlayerInput.IgnoreMouseInterface)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].Kind != RowKind.Extra && rows[i].Shown && rows[i].Hit.Contains(Main.mouseX, Main.mouseY))
                    {
                        over = i;
                        break;
                    }
                }
            }

            RowKind overKind = over >= 0 ? rows[over].Kind : RowKind.Extra;

            Focus(ref Main.npcChatFocus2, overKind == RowKind.First, player);

            if (first)
            {
                UILinkPointNavigator.SetPosition(2500, Centre(RowKind.First));
                UILinkPointNavigator.Shortcuts.NPCCHAT_ButtonsLeft = true;
            }

            Focus(ref Main.npcChatFocus1, overKind == RowKind.Close, player);
            UILinkPointNavigator.SetPosition(2501, Centre(RowKind.Close));
            UILinkPointNavigator.Shortcuts.NPCCHAT_ButtonsMiddle = true;

            if (!second)
            {
                Main.npcChatFocus3 = false;
                UILinkPointNavigator.Shortcuts.NPCCHAT_ButtonsRight = false;
            }
            else
            {
                Focus(ref Main.npcChatFocus3, overKind == RowKind.Second, player);
                UILinkPointNavigator.SetPosition(2502, Centre(RowKind.Second));
                UILinkPointNavigator.Shortcuts.NPCCHAT_ButtonsRight = optionsUp;
            }

            if (!Main.remixWorld)
            {
                if (!happiness)
                {
                    Main.npcChatFocus4 = false;
                    UILinkPointNavigator.Shortcuts.NPCCHAT_ButtonsRight2 = false;
                }
                else
                {
                    Focus(ref Main.npcChatFocus4, overKind == RowKind.Happiness, player);
                    UILinkPointNavigator.SetPosition(2503, Centre(RowKind.Happiness));
                    UILinkPointNavigator.Shortcuts.NPCCHAT_ButtonsRight2 = optionsUp;
                }
            }

            ListLinks();
        }

        /// <summary>One button's focus, as vanilla sets it.</summary>
        private static void Focus(ref bool focus, bool over, Player player)
        {
            if (over)
            {
                player.mouseInterface = true;
                player.releaseUseItem = false;

                if (!focus)
                    SoundEngine.PlaySound(SoundID.MenuTick);

                focus = true;
            }
            else
            {
                if (focus)
                    SoundEngine.PlaySound(SoundID.MenuTick);

                focus = false;
            }
        }

        /// <summary>
        /// The gamepad has moved to a button the list is not showing: page the list until it is.
        /// So moving down past the sixth row pages down, and up past the first pages up.
        /// </summary>
        private static void Reveal()
        {
            if (rows.Count <= MaxRows || !PlayerInput.UsingGamepadUI || UILinkPointNavigator.CurrentPage != 1003)
                return;

            RowKind kind = UILinkPointNavigator.CurrentPoint switch
            {
                2500 => RowKind.First,
                2501 => RowKind.Close,
                2502 => RowKind.Second,
                2503 => RowKind.Happiness,
                _ => RowKind.Extra,
            };

            if (kind == RowKind.Extra)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind != kind)
                    continue;

                if (i < scroll)
                    scroll = i;
                else if (i >= scroll + MaxRows)
                    scroll = i - MaxRows + 1;

                return;
            }
        }

        /// <summary>The wheel, from TownChatSystem.UpdateUI: over the list when it was last drawn, it pages the list.</summary>
        internal static void Wheel(int delta)
        {
            if (!wheelOurs || delta == 0 || !scrolls)
                return;

            int before = scroll;
            scroll = Math.Clamp(scroll - Math.Sign(delta), 0, Math.Max(0, rows.Count - MaxRows));

            if (scroll != before)
                SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private static Vector2 Centre(RowKind kind)
        {
            foreach (Row row in rows)
            {
                if (row.Kind == kind)
                    return row.TopLeft + row.Size * 0.5f;
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// The gamepad's Up and Down go down the list, in the list's order. Left and Right are
        /// left as vanilla wires them (its page rewires them every frame), so they still step
        /// through the buttons too.
        /// </summary>
        private static void ListLinks()
        {
            Span<int> order = stackalloc int[4];
            int count = 0;

            foreach (Row row in rows)
            {
                int id = row.Kind switch
                {
                    RowKind.First => 2500,
                    RowKind.Second => 2502,
                    RowKind.Happiness => 2503,
                    RowKind.Close => 2501,
                    _ => -1,
                };

                if (id >= 0 && count < order.Length)
                    order[count++] = id;
            }

            VanillaLinks();

            for (int i = 0; i < count; i++)
            {
                if (!UILinkPointNavigator.Points.TryGetValue(order[i], out UILinkPoint point))
                    continue;

                point.Up = i > 0 ? order[i - 1] : -1;
                point.Down = i < count - 1 ? order[i + 1] : -2;
            }

            linksOurs = true;
        }

        /// <summary>Up and Down as vanilla made them (UILinksInitializer): nowhere.</summary>
        private static void VanillaLinks()
        {
            if (!linksOurs)
                return;

            for (int id = 2500; id <= 2503; id++)
            {
                if (UILinkPointNavigator.Points.TryGetValue(id, out UILinkPoint point))
                {
                    point.Up = -1;
                    point.Down = -2;
                }
            }

            linksOurs = false;
        }

        /// <summary>In place of the Tax Collector's coins: kept, for the panel to draw beside his button.</summary>
        public static void Money(SpriteBatch spriteBatch, string text, float shopx, float shopy, int[] coinsArray, bool horizontal)
        {
            if (!Takes())
            {
                ItemSlot.DrawMoney(spriteBatch, text, shopx, shopy, coinsArray, horizontal);
                return;
            }

            moneyUp = true;
            moneyOffset = new Vector2(shopx, shopy) - vanillaOrigin;
            moneyCoins = coinsArray;
        }

        /// <summary>
        /// The box vanilla's click handling must find the mouse in, and that marks the mouse as
        /// the interface's while it is over it: the panel, for a townsperson.
        /// </summary>
        public static void ClickArea(ref Rectangle area)
        {
            if (!Takes())
                return;

            // The buttons are not drawn while the virtual keyboard is up on a gamepad; the panel still is.
            if (!chatRan)
            {
                chatRan = true;
                moneyUp = false;
                rows.Clear();
                extras.Clear();
                Layout(Mode.Chat);
                optionsUp = false;
            }

            area = panel;
        }

        // ============================================================
        // DRAW — from TalkOptionSystem's layer, after the frame's GUIChatDrawInner
        // ============================================================

        public static void Draw(SpriteBatch spriteBatch)
        {
            try
            {
                DrawPanel(spriteBatch);
            }
            finally
            {
                chatRan = false;
                reforgeRan = false;
                shopRan = false;
            }
        }

        private static void DrawPanel(SpriteBatch spriteBatch)
        {
            wheelOurs = false;

            if (Main.gameMenu || Main.dedServ)
                return;

            Player player = Main.LocalPlayer;
            NPC npc = HasConversation ? player.TalkNPC : null;
            bool up = npc != null && npc.active;
            Mode mode = shopRan ? Mode.Shop : reforgeRan ? Mode.Reforge : Mode.Chat;

            fade = MathHelper.Clamp(fade + (up ? 1f : -1f) / Dialogue.FadeTicks, 0f, 1f);

            if (fade <= 0f)
            {
                ForgetHover();
                return;
            }

            if (up)
            {
                faceIndex = npc.whoAmI;
                faceType = npc.type;
                lastMode = mode;
            }

            NPC face = npc;

            if (face == null && faceIndex >= 0 && Main.npc[faceIndex].active && Main.npc[faceIndex].type == faceType)
                face = Main.npc[faceIndex];

            panelTexture ??= ModContent.Request<Texture2D>("PisAndDag/Content/UI/TalkPanel");

            Rectangle box = panel;
            bool chat = up && mode == Mode.Chat;
            int arrived = chat ? Arrived() : letterCount;
            bool complete = arrived >= letterCount;

            // A click on the panel while the line is still arriving brings the rest of it at once.
            if (chat && !complete && box.Contains(Main.mouseX, Main.mouseY) && PADMouse.Steering && PADMouse.Pressed)
            {
                PADMouse.Consume();
                lineStart = 0;
                arrived = letterCount;
            }

            // Vanilla's click box is not running under the reforge or a shop, so the panel keeps the mouse itself.
            if (up && !chat && box.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface)
                player.mouseInterface = true;

            // A shop's panel went down under its grid, in the inventory's draw; the rest is the list.
            if (!(up && mode == Mode.Shop))
                DrawFrame(spriteBatch, face, box, fade);

            // A shop's grid fades with nothing: the panel goes down empty after one.
            if (!(up ? mode == Mode.Shop : lastMode == Mode.Shop))
                DrawLine(spriteBatch, box, arrived, up);

            if (!up)
            {
                ForgetHover();
                return;
            }

            if (chat && Main.npcChatCornerItem != 0)
                DrawCornerItem(spriteBatch);

            if (!chat || optionsUp)
                DrawRows(spriteBatch, npc, mode);
            else
                ForgetHover();
        }

        /// <summary>The face, the name tab under the panel's edge, and the panel: everything but what it holds.</summary>
        private static void DrawFrame(SpriteBatch spriteBatch, NPC face, Rectangle box, float alpha)
        {
            // Under the panel, so the panel's edge crosses its foot.
            DrawNameTab(spriteBatch, face, box, alpha);

            Utils.DrawSplicedPanel(spriteBatch, panelTexture.Value, box.X, box.Y, box.Width, box.Height,
                SliceEnd, SliceEnd, SliceEnd, SliceEnd, Color.White * alpha);

            DialoguePortrait.DrawOnPanel(spriteBatch, face, box, panelTexture.Value, alpha);
        }

        private static void ForgetHover()
        {
            extraHovered = -1;
            leaveHovered = false;
            upHovered = false;
            downHovered = false;
        }

        /// <summary>
        /// The line, as much of it as has arrived. A tag under the mouse gets its hover, and its
        /// click on the press, as vanilla gives it in its own box. While the panel fades out the
        /// line fades with it in the lore colour.
        /// </summary>
        private static void DrawLine(SpriteBatch spriteBatch, Rectangle box, int arrived, bool up)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 at = new Vector2(textLeft, box.Y + PadTop);
            int budget = arrived;
            TextSnippet hovered = null;
            bool fading = fade < 1f;

            for (int i = 0; i < lines.Count && budget > 0; i++)
            {
                TextSnippet[] shown = budget >= lineLetters[i] ? lines[i] : Part(i, budget);
                budget -= lineLetters[i];

                if (shown.Length > 0)
                {
                    ChatManager.DrawColorCodedStringShadow(spriteBatch, font, shown, at, Color.Black * fade, 0f, Vector2.Zero,
                        Vector2.One, -1f, 2f);
                    ChatManager.DrawColorCodedString(spriteBatch, font, shown, at, TextColour * fade, 0f, Vector2.Zero, Vector2.One,
                        out int over, -1f, ignoreColors: fading);

                    if (over > -1)
                        hovered = shown[over];
                }

                at.Y += font.LineSpacing;
            }

            if (hovered == null || !up)
                return;

            hovered.OnHover();

            if (PADMouse.Pressed)
                hovered.OnClick();
        }

        /// <summary>The corner item, where CornerAt put it, drawn as vanilla draws it: fitted to 32, its alpha and its colour.</summary>
        private static void DrawCornerItem(SpriteBatch spriteBatch)
        {
            if (cornerItem == null || cornerItem.netID != Main.npcChatCornerItem)
            {
                cornerItem = new Item();
                cornerItem.netDefaults(Main.npcChatCornerItem);
            }

            Main.GetItemDrawFrame(cornerItem.type, out Texture2D texture, out Rectangle frame);

            float scale = 1f;

            if (frame.Width > CornerSize || frame.Height > CornerSize)
                scale = frame.Width <= frame.Height ? CornerSize / (float)frame.Height : CornerSize / (float)frame.Width;

            Vector2 at = CornerPosition();

            spriteBatch.Draw(texture, at, frame, cornerItem.GetAlpha(Color.White) * fade, 0f, frame.Size(), scale, SpriteEffects.None, 0f);

            if (cornerItem.color != default)
                spriteBatch.Draw(texture, at, frame, cornerItem.GetColor(cornerItem.color) * fade, 0f, frame.Size(), scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The list: vanilla's buttons swelling on their focus flags, TalkOptions' lines through
        /// TalkOptions itself, the six in view, and the arrows under them when there are more.
        /// </summary>
        private static void DrawRows(SpriteBatch spriteBatch, NPC npc, Mode mode)
        {
            bool vanillaFocus = mode == Mode.Chat && (Main.npcChatFocus1 || Main.npcChatFocus2 || Main.npcChatFocus3 || Main.npcChatFocus4);
            int nowHovered = -1;
            bool nowLeave = false;
            bool clicked = false;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                if (!row.Shown)
                    continue;

                if (row.Kind == RowKind.Extra)
                {
                    // Left edges on the column, as the vanilla buttons above and below sit. A click
                    // may replace TalkOptions' own list, never this one, so the rest still draws;
                    // nothing after a click takes the hover.
                    clicked |= TalkOptions.DrawOne(spriteBatch, row.Option, npc, row.TopLeft.X, 0f, row.TopLeft.Y,
                        !vanillaFocus && !clicked && nowHovered == -1, TalkMemory.Heard, out bool over, out _);

                    if (over)
                        nowHovered = i;

                    continue;
                }

                bool focused = row.Kind switch
                {
                    RowKind.First => Main.npcChatFocus2,
                    RowKind.Second => Main.npcChatFocus3,
                    RowKind.Happiness => Main.npcChatFocus4,
                    RowKind.Close => Main.npcChatFocus1,
                    _ => false,
                };

                if (row.Kind == RowKind.Leave)
                {
                    focused = Leave(row);
                    nowLeave = focused;
                }

                DrawLabel(spriteBatch, row, focused);

                if (row.Kind == RowKind.First && moneyUp && moneyCoins != null)
                    ItemSlot.DrawMoney(spriteBatch, "", row.TopLeft.X + moneyOffset.X, row.TopLeft.Y + moneyOffset.Y, moneyCoins, true);
            }

            // TalkOptions ticks on entering and leaving a line, as vanilla does a button.
            if (nowHovered != extraHovered)
                SoundEngine.PlaySound(SoundID.MenuTick);

            if (nowLeave != leaveHovered)
                SoundEngine.PlaySound(SoundID.MenuTick);

            extraHovered = nowHovered;
            leaveHovered = nowLeave;

            if (!scrolls)
            {
                upHovered = false;
                downHovered = false;
                return;
            }

            bool free = !vanillaFocus && nowHovered == -1 && !clicked;

            upHovered = Arrow(spriteBatch, arrowUp, scroll > 0, true, free, upHovered, -1);
            downHovered = Arrow(spriteBatch, arrowDown, scroll + MaxRows < rows.Count, false, free && !upHovered, downHovered, 1);

            // The wheel over the list pages it, and the hotbar leaves that turn of the wheel alone
            // (tModLoader's lock, read where the hotbar would scroll). Taken in UpdateUI (Wheel).
            if (listArea.Contains(Main.mouseX, Main.mouseY) && PADMouse.Steering)
            {
                PlayerInput.LockVanillaMouseScroll("PisAndDag/TownChat");
                wheelOurs = true;
            }
        }

        /// <summary>
        /// One of the list's two arrows, if it has anywhere to go: ChatArrow.png's top frame at rest,
        /// the lower hovered, flipped to point up. Hovered as an answer is, ticking on entering and
        /// leaving; a click pages the list one row. True while hovered.
        /// </summary>
        private static bool Arrow(SpriteBatch spriteBatch, Rectangle at, bool live, bool up, bool mayHover, bool wasHovered, int step)
        {
            if (!live)
            {
                if (wasHovered)
                    SoundEngine.PlaySound(SoundID.MenuTick);

                return false;
            }

            Rectangle hit = at;
            hit.Inflate(ArrowSlack, ArrowSlack);

            bool over = mayHover && hit.Contains(Main.mouseX, Main.mouseY) && PADMouse.Steering;

            if (over != wasHovered)
                SoundEngine.PlaySound(SoundID.MenuTick);

            arrowTexture ??= ModContent.Request<Texture2D>("PisAndDag/Content/UI/ChatArrow", AssetRequestMode.ImmediateLoad);

            spriteBatch.Draw(arrowTexture.Value, new Vector2(at.X, at.Y), new Rectangle(0, over ? ArrowHeight : 0, ArrowWidth, ArrowHeight),
                Color.White * fade, 0f, Vector2.Zero, 1f, up ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);

            if (!over)
                return false;

            Player player = Main.LocalPlayer;
            player.mouseInterface = true;
            player.releaseUseItem = false;

            if (PADMouse.Pressed)
            {
                PADMouse.Consume();
                SoundEngine.PlaySound(SoundID.MenuTick);
                scroll = Math.Clamp(scroll + step, 0, Math.Max(0, rows.Count - MaxRows));
            }

            return true;
        }

        /// <summary>The reforge's or a shop's Close: hovered as vanilla hovers a button, and on a click, what vanilla's Close does.</summary>
        private static bool Leave(Row row)
        {
            if (!row.Hit.Contains(Main.mouseX, Main.mouseY) || PlayerInput.IgnoreMouseInterface)
                return false;

            Player player = Main.LocalPlayer;
            player.mouseInterface = true;
            player.releaseUseItem = false;

            if (PADMouse.Pressed)
            {
                PADMouse.Consume();
                Main.CloseNPCChatOrSign();
            }

            return true;
        }

        /// <summary>
        /// One of vanilla's buttons, in TalkOptions' look: its own colour (gold, or the coin colour
        /// vanilla gives the Nurse's price), a black shadow, swelling from its middle when focused.
        /// </summary>
        private static void DrawLabel(SpriteBatch spriteBatch, Row row, bool focused)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 scale = new Vector2(row.Scale * (focused ? HoverScale : 1f));
            Vector2 size = focused ? ChatManager.GetStringSize(font, row.Label, scale) : row.Size;
            Vector2 topLeft = row.TopLeft + row.Size * 0.5f - size * 0.5f;

            ChatManager.DrawColorCodedStringShadow(spriteBatch, font, row.Label, topLeft, Color.Black, 0f, Vector2.Zero, scale);
            ChatManager.DrawColorCodedString(spriteBatch, font, row.Label, topLeft, row.Colour, 0f, Vector2.Zero, scale,
                -1f, ignoreColors: true);
        }

        /// <summary>
        /// Who is speaking, on a tab standing up off the panel's top edge beside the face, as
        /// Dialogue has it: the name the game gives them, and what they are after it in a
        /// quieter colour ("Andrew", " the Guide"). Someone with no name of their own has one part.
        /// </summary>
        private static void DrawNameTab(SpriteBatch spriteBatch, NPC speaker, Rectangle box, float alpha)
        {
            if (speaker == null)
                return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            Label(speaker, out string name, out string title);

            float nameWidth = font.MeasureString(name).X * TabScale;
            float titleWidth = title == null ? 0f : font.MeasureString(title).X * TabScale;

            int x = DialoguePortrait.PanelFrameRight(box) + TabGap;
            int width = (int)Math.Ceiling(nameWidth + titleWidth) + TabPadX * 2;
            Rectangle tab = new Rectangle(x, box.Y - TabHeight + TabTuck, width, TabHeight);

            Utils.DrawSplicedPanel(spriteBatch, panelTexture.Value, tab.X, tab.Y, tab.Width, tab.Height,
                SliceEnd, SliceEnd, SliceEnd, SliceEnd, Color.White * alpha);

            // Centred in the part of the tab the panel does not cover.
            float lineHeight = font.MeasureString("M").Y * TabScale;
            Vector2 at = new Vector2(tab.X + TabPadX, tab.Y + (TabHeight - TabTuck - lineHeight) * 0.5f + 2f);
            at = new Vector2((float)Math.Floor(at.X), (float)Math.Floor(at.Y));

            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, name, at, TextColour * alpha, 0f,
                Vector2.Zero, new Vector2(TabScale));

            if (title != null)
            {
                ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, title, at + new Vector2(nameWidth, 0f),
                    TitleColour * alpha, 0f, Vector2.Zero, new Vector2(TabScale));
            }
        }

        /// <summary>
        /// The game's own name for them (NPC.FullName, "Andrew the Guide"), split where their
        /// given name ends. A language that puts the title first, or someone with no given name,
        /// is one part.
        /// </summary>
        private static void Label(NPC npc, out string name, out string title)
        {
            string full = npc.FullName;
            string given = npc.GivenName;
            title = null;

            if (npc.HasGivenName && full.Length > given.Length && full.StartsWith(given, StringComparison.Ordinal))
            {
                name = given;
                title = full.Substring(given.Length);
                return;
            }

            name = full;
        }

        // ============================================================
        // THE EDIT
        // ============================================================

        /// <summary>
        /// Finds every seam in Main.GUIChatDrawInner first and changes nothing until all of them
        /// are found. Matched by what the instructions do, not where they are:
        ///
        ///   callvirt TextDisplayCache::PrepareCache(text, colour)      the line: Capture after it, from its text local
        ///   ldsfld ChatBack ... callvirt SpriteBatch::Draw  (twice)     the parchment: ChatDraw
        ///   call ChatManager::DrawColorCodedStringWithShadow(TextSnippet[] ...)   the lines: ChatLine
        ///   ldsfld npcChatCornerItem ... op_Subtraction; stloc pos      the corner item's place: CornerAt after it
        ///   callvirt SpriteBatch::Draw (the next two)                   the corner item: ChatDraw
        ///   call Main::DrawNPCChatButtons                               Buttons
        ///   call ItemSlot::DrawMoney                                    Money
        ///   call PlayerInput::get_IgnoreMouseInterface; brfalse; ret; ldloca rect
        ///                                                               ClickArea(ref rect) before it, on its labels
        /// </summary>
        internal static bool Edit(ILContext il, out string missing)
        {
            Applied = false;
            missing = null;

            MethodInfo original = typeof(Main).GetMethod("DrawNPCChatButtons", BindingFlags.NonPublic | BindingFlags.Static);

            if (original == null)
            {
                missing = "Main.DrawNPCChatButtons";
                return false;
            }

            vanillaButtons = original.CreateDelegate<Action<int, Color, int, string, string>>();

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before, i => i.MatchCallvirt(out MethodReference m) && m.Name == "PrepareCache"))
                return Missing(out missing, "the text cache's PrepareCache");

            Instruction prepare = c.Next;

            if (prepare.Previous?.Previous == null || !prepare.Previous.Previous.MatchLdloc(out int textLocal))
                return Missing(out missing, "the line's local before PrepareCache");

            if (!NextBackDraw(c, out Instruction back1) || !NextBackDraw(c, out Instruction back2))
                return Missing(out missing, "the two ChatBack draws");

            if (!c.TryGotoNext(MoveType.Before, i => IsLineDraw(i)))
                return Missing(out missing, "the text lines' DrawColorCodedStringWithShadow");

            Instruction lineDraw = c.Next;
            c.Index++;

            if (!c.TryGotoNext(MoveType.After, i => i.MatchLdsfld(typeof(Main), nameof(Main.npcChatCornerItem))))
                return Missing(out missing, "npcChatCornerItem");

            int cornerLocal = -1;

            if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(Vector2), "op_Subtraction"), i => i.MatchStloc(out cornerLocal)))
                return Missing(out missing, "the corner item's position");

            Instruction cornerStore = c.Prev;

            // Both of the corner item's draws come before its hover test, or they are not its draws.
            ILCursor hover = new ILCursor(c);

            if (!hover.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(Rectangle), "Contains")))
                return Missing(out missing, "the corner item's hover test");

            if (!c.TryGotoNext(MoveType.After, i => IsSpriteDraw(i)))
                return Missing(out missing, "the corner item's draw");

            Instruction corner1 = c.Prev;

            if (!c.TryGotoNext(MoveType.After, i => IsSpriteDraw(i)) || c.Index > hover.Index)
                return Missing(out missing, "the corner item's colour draw");

            Instruction corner2 = c.Prev;

            if (!c.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(Main), "DrawNPCChatButtons")))
                return Missing(out missing, "the DrawNPCChatButtons call");

            Instruction buttons = c.Next;

            if (!c.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(ItemSlot), nameof(ItemSlot.DrawMoney))))
                return Missing(out missing, "the Tax Collector's DrawMoney");

            Instruction money = c.Next;
            int rectLocal = -1;

            if (!c.TryGotoNext(MoveType.Before,
                    i => i.MatchCall(typeof(PlayerInput), "get_IgnoreMouseInterface"),
                    i => i.MatchBrfalse(out _),
                    i => i.MatchRet(),
                    i => i.MatchLdloca(out rectLocal)))
                return Missing(out missing, "the click box's IgnoreMouseInterface test");

            Instruction ignore = c.Next;

            // Everything is there. Now the changes, none of which moves the stack.
            MethodReference chatDraw = il.Import(typeof(TownChat).GetMethod(nameof(ChatDraw)));

            foreach (Instruction draw in new[] { back1, back2, corner1, corner2 })
            {
                draw.OpCode = OpCodes.Call;
                draw.Operand = chatDraw;
            }

            lineDraw.OpCode = OpCodes.Call;
            lineDraw.Operand = il.Import(typeof(TownChat).GetMethod(nameof(ChatLine)));

            buttons.OpCode = OpCodes.Call;
            buttons.Operand = il.Import(typeof(TownChat).GetMethod(nameof(Buttons)));

            money.OpCode = OpCodes.Call;
            money.Operand = il.Import(typeof(TownChat).GetMethod(nameof(Money)));

            c.Goto(prepare, MoveType.After);
            c.Emit(OpCodes.Ldloc, il.Body.Variables[textLocal]);
            c.Emit(OpCodes.Call, typeof(TownChat).GetMethod(nameof(Capture)));

            c.Goto(cornerStore, MoveType.After);
            c.Emit(OpCodes.Ldloc, il.Body.Variables[cornerLocal]);
            c.Emit(OpCodes.Call, typeof(TownChat).GetMethod(nameof(CornerAt)));
            c.Emit(OpCodes.Stloc, il.Body.Variables[cornerLocal]);

            // On the test's labels: the buttons' block jumps here when it is skipped, and so must
            // the click box.
            c.Goto(ignore, MoveType.AfterLabel);
            c.Emit(OpCodes.Ldloca, il.Body.Variables[rectLocal]);
            c.Emit(OpCodes.Call, typeof(TownChat).GetMethod(nameof(ClickArea)));

            Applied = true;
            return true;
        }

        private static bool Missing(out string missing, string what)
        {
            missing = what;
            return false;
        }

        /// <summary>The next SpriteBatch.Draw after a load of ChatBack.</summary>
        private static bool NextBackDraw(ILCursor c, out Instruction draw)
        {
            draw = null;

            if (!c.TryGotoNext(MoveType.After, i => i.MatchLdsfld(typeof(TextureAssets), nameof(TextureAssets.ChatBack))))
                return false;

            if (!c.TryGotoNext(MoveType.After, i => IsSpriteDraw(i)))
                return false;

            draw = c.Prev;
            return true;
        }

        /// <summary>SpriteBatch.Draw(texture, position, source, colour, rotation, origin, float scale, effects, depth).</summary>
        private static bool IsSpriteDraw(Instruction i)
        {
            return i.MatchCallvirt(out MethodReference m)
                && m.DeclaringType.FullName == typeof(SpriteBatch).FullName
                && m.Name == "Draw"
                && m.Parameters.Count == 9
                && m.Parameters[6].ParameterType.FullName == typeof(float).FullName;
        }

        /// <summary>ChatManager.DrawColorCodedStringWithShadow over snippets, with a shadow colour and the hovered snippet out.</summary>
        private static bool IsLineDraw(Instruction i)
        {
            return i.MatchCall(out MethodReference m)
                && m.DeclaringType.FullName == typeof(ChatManager).FullName
                && m.Name == nameof(ChatManager.DrawColorCodedStringWithShadow)
                && m.Parameters.Count == 12
                && m.Parameters[2].ParameterType.FullName == typeof(TextSnippet[]).FullName;
        }
    }

    /// <summary>Puts TownChat's, ReforgeBox's and ShopBox's edits in, client side, and takes their state down on unload.</summary>
    public class TownChatSystem : ModSystem
    {
        public override void Load()
        {
            if (Main.dedServ)
                return;

            IL_Main.GUIChatDrawInner += EditChat;
            IL_Main.DrawInventory += EditInventory;
        }

        public override void Unload()
        {
            TownChat.Clear();
            ReforgeBox.Clear();
            ShopBox.Clear();
            ChatSubBox.Clear();
            ChatSlot.Clear();
        }

        /// <summary>The wheel's turn this update, while it is still there to read (Main clears it after the UI's update).</summary>
        public override void UpdateUI(GameTime gameTime)
        {
            if (!Main.dedServ)
                TownChat.Wheel(PlayerInput.ScrollWheelDeltaForUI);
        }

        private void EditChat(ILContext il)
        {
            try
            {
                if (!TownChat.Edit(il, out string missing))
                    Mod.Logger.Warn("Town chat panel disabled, vanilla's chat box kept: Main.GUIChatDrawInner no longer has " + missing + ".");
            }
            catch (Exception e)
            {
                TownChat.Applied = false;
                Mod.Logger.Warn("Town chat panel disabled, vanilla's chat box kept: " + e.Message);
            }
        }

        /// <summary>Two edits to one method, each whole or not at all, and neither waiting on the other.</summary>
        private void EditInventory(ILContext il)
        {
            try
            {
                if (!ReforgeBox.Edit(il, out string missing))
                    Mod.Logger.Warn("Reforge sub-box disabled, vanilla's reforge slot kept: Main.DrawInventory no longer has " + missing + ".");
            }
            catch (Exception e)
            {
                ReforgeBox.Applied = false;
                Mod.Logger.Warn("Reforge sub-box disabled, vanilla's reforge slot kept: " + e.Message);
            }

            try
            {
                if (!ShopBox.Edit(il, out string missing))
                    Mod.Logger.Warn("Shop in the chat panel disabled, vanilla's shop kept: Main.DrawInventory no longer has " + missing + ".");
            }
            catch (Exception e)
            {
                ShopBox.Applied = false;
                Mod.Logger.Warn("Shop in the chat panel disabled, vanilla's shop kept: " + e.Message);
            }
        }
    }
}
