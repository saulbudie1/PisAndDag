using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using Terraria.Utilities;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// What is on the screen while a door's passage saves one world and loads the other
    /// (SubworldPassage draws the black and asks this for the rest): a picture, a tip, the
    /// key that turns to the next one, and a bar. Elden Ring's loading screen turned about:
    /// the bar low in the left corner, a seventh of the screen wide, and the tip low on the
    /// right, a title over a few lines, each ending at the same edge, with the key under
    /// them in the right corner.
    ///
    /// THE PICTURE is two files beside this one. LoadingArt is the picture itself, of any
    /// size (1920 by 1080 is the size it is mostly seen at), drawn to cover the window
    /// whatever its shape, cut at the sides or the top and bottom rather than squeezed.
    /// LoadingFrame goes over it, stretched to the window's own edges: clear in the
    /// middle, black at the edges and blackest where the words and the bar are, and
    /// grained, blotted and scratched as an old print is (PlaceholderTextures.md), so
    /// any picture put under it gets the same edges with nothing else done. The frame is
    /// stored premultiplied, as the game blends: tModLoader does not do that to a mod's
    /// pictures, and one saved the plain way would have its pale marks come out white.
    /// Either file gone, that layer is left out; both gone, the screen is the plain
    /// black it was.
    ///
    /// THE TIPS are the hjson's (Mods.PisAndDag.Tips.List): every entry there with a Title
    /// and a Text is one, so a tip is added by writing it there and nowhere else. They are
    /// gone through in a shuffled order, each once before any comes round again. One time
    /// in <see cref="GameTipOneIn"/> the tip is one of the game's own instead (the ones on
    /// its own loading screen, found and worded exactly as it does: GameTipsDisplay), under
    /// one title for all of them (Tips.VanillaTitle). Two of the game's are left out, as
    /// untrue with this mod in (<see cref="NotHere"/>).
    ///
    /// THE BAR is the game's own word on how far along it is. The game says what it is doing
    /// in Main.statusText, and for the long steps says how far through, as a number and a
    /// "%" that its code puts after the step's name (WorldFile.SaveWorldTiles,
    /// LoadWorldTiles, ValidateWorld, LoadWorld; Liquid.UpdateProgressDisplay;
    /// MapHelper.InternalSaveMap, LoadMapVersion2; WorldGen.playWorldCallBack). Each of
    /// those steps has a stretch of the bar; the saving and the loading share the bar by
    /// the sizes of the two worlds, so leaving a great world for a small place the bar is
    /// nearly all saving, and the other way nearly all loading. What the game had said
    /// last before the passage began is not listened to. The bar never goes back, and is
    /// full when the player is put into the other world.
    ///
    /// Nothing shows for the first quarter second, so a passage that short stays plain black.
    /// Client-side, and drawn from the passage's own batch.
    /// </summary>
    public static class SubworldLoading
    {
        /// <summary>What the passage is doing, as far as this is concerned.</summary>
        public enum Phase
        {
            /// <summary>The game is saving the player and the world left.</summary>
            Saving,

            /// <summary>The other world is being made, if need be, and loaded.</summary>
            Loading,

            /// <summary>The player is in the other world, and all this is on its way out.</summary>
            Arrived,
        }

        // ---- the hjson -------------------------------------------------------------------------

        private const string ListPrefix = "Mods.PisAndDag.Tips.List.";
        private const string TitleSuffix = ".Title";
        private const string TextSuffix = ".Text";
        private const string NextKey = "Mods.PisAndDag.Tips.Next";
        private const string GameTitleKey = "Mods.PisAndDag.Tips.VanillaTitle";

        /// <summary>One tip in this many is one of the game's own.</summary>
        public const int GameTipOneIn = 5;

        /// <summary>The key that turns to the next tip.</summary>
        public const Keys NextTip = Keys.E;

        /// <summary>
        /// The game's own tips that are not true here: wings (69) do nothing until the Empress
        /// of Light has fallen, and a Mana Flower (58) drinks its owner into Mana Poisoning.
        /// </summary>
        private static readonly HashSet<string> NotHere = new()
        {
            "LoadingTips_Default.69",
            "LoadingTips_Default.58",
        };

        // ---- the picture -----------------------------------------------------------------------

        private const string ArtPath = "Content/Subworlds/LoadingArt";
        private const string FramePath = "Content/Subworlds/LoadingFrame";

        private static Asset<Texture2D> art;
        private static Asset<Texture2D> edges;

        // ---- how it looks ----------------------------------------------------------------------

        /// <summary>Seconds of plain black before any of it shows, and seconds it takes to come up.</summary>
        private const float Wait = 0.25f;
        private const float ComeUp = 0.3f;

        /// <summary>Seconds a tip turned to takes to come up.</summary>
        private const float TipComeUp = 0.2f;

        /// <summary>How fast the bar closes on where it should be: this much of the way a second, near enough.</summary>
        private const float BarRate = 7f;

        /// <summary>The title's line is this many times a line of the tip's own.</summary>
        private const float TitleRatio = 1.4f;

        /// <summary>The most lines a tip is let run to.</summary>
        private const int MostLines = 8;

        /// <summary>
        /// Where the middle of a capital sits in one of the game's lines of text, down from
        /// the line's top, as a share of the line: the game's fonts measure a line by its
        /// spacing, most of which is under the letters.
        /// </summary>
        private const float CapMiddle = 0.36f;

        private static readonly Color TitleColour = new Color(196, 180, 152);
        private static readonly Color TextColour = new Color(226, 222, 212);
        private static readonly Color FootColour = new Color(186, 180, 168);

        /// <summary>
        /// Where everything goes in a window of a size, measured off Elden Ring's own screen
        /// as shares of it and turned about left for right. Pure numbers, so it can be
        /// checked with no window.
        /// </summary>
        internal readonly struct Frame
        {
            public readonly int Width;
            public readonly int Height;

            /// <summary>The interface's scale: the game's own for its menus (Main.PreDrawMenu).</summary>
            public readonly float Scale;

            /// <summary>The edge the tip's lines end at, the top of its title, and the most a line of it may run.</summary>
            public readonly float Right;
            public readonly float TitleTop;
            public readonly float TextWide;

            /// <summary>The line the bar and the key sit on: their middle.</summary>
            public readonly float Foot;

            /// <summary>The bar's two ends: a seventh of the window apart.</summary>
            public readonly float BarLeft;
            public readonly float BarRight;

            /// <summary>The bar's line, the filled part of it, and the marks at its ends, in pixels.</summary>
            public readonly int Thin;
            public readonly int Thick;
            public readonly int Mark;

            public Frame(int width, int height, bool keepSmall)
            {
                Width = width;
                Height = height;
                Scale = keepSmall ? 1f : Math.Max(1f, height / 900f);

                Right = width * 0.94f;
                TitleTop = height * 0.653f;
                TextWide = width * 0.4f;
                Foot = height * 0.951f;

                BarLeft = width * 0.06f;
                BarRight = BarLeft + width / 7f;

                Thin = Math.Max(1, (int)Math.Round(Scale));
                Thick = Thin * 3;
                Mark = Math.Max(9, (int)Math.Round(height * 0.014f));
            }
        }

        // ---- the bar ---------------------------------------------------------------------------

        /// <summary>
        /// A step of the game's that says how far through it is: its name (Lang.gen), and the
        /// stretch of its half of the bar that is its own.
        /// </summary>
        private readonly struct Step
        {
            public readonly int Label;
            public readonly float From;
            public readonly float To;

            public Step(int label, float from, float to)
            {
                Label = label;
                From = from;
                To = to;
            }
        }

        /// <summary>Saving: the map (66), the world's tiles (49), the check of what was written (73).</summary>
        private static readonly Step[] SavingSteps =
        {
            new Step(66, 0.00f, 0.12f),
            new Step(49, 0.12f, 0.62f),
            new Step(73, 0.62f, 0.97f),
        };

        /// <summary>
        /// Loading: the tiles (51), the liquids settling (27), the map read (67) and drawn
        /// (68). The settling is two counts under one name: the game's quick pass over the
        /// rows runs it to 50 (Liquid.QuickWater, asked for with 2), and settling whatever
        /// still moves takes it from 50 to 100 (WorldFile.LoadWorld), so in a world with
        /// nothing left to settle it stops at half.
        /// </summary>
        private static readonly Step[] LoadingSteps =
        {
            new Step(51, 0.03f, 0.55f),
            new Step(27, 0.55f, 0.72f),
            new Step(67, 0.72f, 0.84f),
            new Step(68, 0.84f, 1.00f),
        };

        /// <summary>The saving's share of the bar is never less than this, nor more than one less this.</summary>
        private const float LeastShare = 0.08f;

        private static float saveShare = 0.5f;

        /// <summary>Where the bar should be, 0 to 1: only ever raised.</summary>
        private static float toward;

        /// <summary>Where it is drawn: closing on that.</summary>
        private static float bar;

        /// <summary>
        /// The game's status text as it stood when the passage began: the last word of
        /// whatever the game did before (an autosave, the passage in), and nothing to do with
        /// this one. Not read until the game writes another, which is always a new string.
        /// </summary>
        private static string leftOver;

        // ---- the tip ---------------------------------------------------------------------------

        private static readonly UnifiedRandom dice = new UnifiedRandom();

        /// <summary>The mod's tips, each the key its Title and Text hang from, in the order they are gone through.</summary>
        private static readonly List<string> own = new();
        private static int ownAt;

        /// <summary>The game's.</summary>
        private static readonly List<LocalizedText> games = new();
        private static string lastGame;

        /// <summary>The tip that is up, worded and ready.</summary>
        private static string title = "";
        private static string text = "";

        private static float tipAge;
        private static float waited;
        private static float shown;
        private static bool keyWas;

        /// <summary>Something here threw: none of it is drawn again until the mod is loaded again. The passage goes on, plain black.</summary>
        private static bool broken;

        /// <summary>The two files of the picture are asked for, if they are there. Client only.</summary>
        internal static void Load(Mod mod)
        {
            art = Ask(mod, ArtPath);
            edges = Ask(mod, FramePath);
        }

        private static Asset<Texture2D> Ask(Mod mod, string path)
        {
            string name = mod.Name + "/" + path;

            return ModContent.HasAsset(name) ? ModContent.Request<Texture2D>(name) : null;
        }

        internal static void Unload()
        {
            art = null;
            edges = null;
            leftOver = null;
            own.Clear();
            games.Clear();
            ownAt = 0;
            lastGame = null;
            title = "";
            text = "";
            broken = false;
        }

        // ============================================================
        // A PASSAGE BEGINS
        // ============================================================

        /// <summary>
        /// The world is about to be left. <paramref name="leaving"/> and
        /// <paramref name="entering"/> are the two worlds' sizes in tiles, for sharing the bar
        /// out; 0 for one not known.
        /// </summary>
        internal static void Begin(long leaving, long entering)
        {
            toward = 0f;
            bar = 0f;
            waited = 0f;
            shown = 0f;
            leftOver = Main.statusText;

            // A key already down is not a press.
            keyWas = true;

            saveShare = leaving > 0 && entering > 0
                ? MathHelper.Clamp(leaving / (float)(leaving + entering), LeastShare, 1f - LeastShare)
                : 0.5f;

            if (broken)
                return;

            try
            {
                Gather();
                Turn();
            }
            catch (Exception e)
            {
                Break(e);
            }
        }

        private static void Break(Exception e)
        {
            broken = true;
            title = "";
            text = "";

            // Said once, and never in a way that can itself throw: this is called from a catch.
            ModContent.GetInstance<SubworldPassage>()?.Mod?.Logger?.Warn("Subworlds: the loading screen's picture, tips and bar are off until the mod is loaded again.", e);
        }

        // ============================================================
        // THE TIPS
        // ============================================================

        /// <summary>
        /// Finds the tips: the mod's in the hjson, the game's as the game finds them. The
        /// mod's are kept in the order they were being gone through unless the list itself
        /// has changed (another language, a reload).
        /// </summary>
        private static void Gather()
        {
            var found = new List<string>();

            foreach (LocalizedText entry in Language.FindAll((key, _) => key.StartsWith(ListPrefix, StringComparison.Ordinal) && key.EndsWith(TitleSuffix, StringComparison.Ordinal)))
            {
                string id = entry.Key.Substring(0, entry.Key.Length - TitleSuffix.Length);

                // A title with nothing under it is not a tip.
                if (Language.Exists(id + TextSuffix) && !string.IsNullOrWhiteSpace(entry.Value))
                    found.Add(id);
            }

            found.Sort(StringComparer.Ordinal);

            if (!SameList(found, own))
            {
                own.Clear();
                own.AddRange(found);
                Shuffle(own, null);
                ownAt = 0;
            }

            games.Clear();
            AddGameTips("LoadingTips_Default.");
            AddGameTips(PlayerInput.UsingGamepad ? "LoadingTips_GamePad." : "LoadingTips_Keyboard.");
        }

        private static void AddGameTips(string prefix)
        {
            foreach (LocalizedText tip in Language.FindAll(Lang.CreateDialogFilter(prefix)))
            {
                if (!NotHere.Contains(tip.Key))
                    games.Add(tip);
            }
        }

        /// <summary>The same tips, whatever the order.</summary>
        private static bool SameList(List<string> sorted, List<string> shuffled)
        {
            if (sorted.Count != shuffled.Count)
                return false;

            var have = new HashSet<string>(shuffled);

            foreach (string id in sorted)
            {
                if (!have.Contains(id))
                    return false;
            }

            return true;
        }

        /// <summary>A new order, in which <paramref name="notFirst"/> (the one just shown) does not come first.</summary>
        private static void Shuffle(List<string> list, string notFirst)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = dice.Next(i + 1);

                (list[i], list[j]) = (list[j], list[i]);
            }

            if (list.Count > 1 && list[0] == notFirst)
                (list[0], list[list.Count - 1]) = (list[list.Count - 1], list[0]);
        }

        /// <summary>Turns to the next tip: one of the game's one time in <see cref="GameTipOneIn"/>, else the mod's next.</summary>
        private static void Turn()
        {
            tipAge = 0f;

            if (games.Count > 0 && (own.Count == 0 || dice.Next(GameTipOneIn) == 0))
            {
                LocalizedText tip = games[dice.Next(games.Count)];

                // Not the same one of the game's twice running.
                if (games.Count > 1 && tip.Key == lastGame)
                    tip = games[(games.IndexOf(tip) + 1 + dice.Next(games.Count - 1)) % games.Count];

                lastGame = tip.Key;
                title = Language.GetTextValue(GameTitleKey);
                text = Worded(tip);
                return;
            }

            if (own.Count == 0)
            {
                title = "";
                text = "";
                return;
            }

            string id = own[ownAt];

            title = Language.GetTextValue(id + TitleSuffix);
            text = Language.GetTextValue(id + TextSuffix);

            if (++ownAt >= own.Count)
            {
                // All of them have been up: again, in another order.
                Shuffle(own, id);
                ownAt = 0;
            }
        }

        /// <summary>One of the game's tips as the game words it on its own loading screen (GameTipsDisplay's GameTip): the keys it names put in, and their pictures.</summary>
        private static string Worded(LocalizedText tip)
        {
            try
            {
                return Lang.SupportGlyphs(tip.FormatWith(Lang.CreateDialogSubstitutionObject(null)));
            }
            catch (Exception)
            {
                return tip.Value;
            }
        }

        // ============================================================
        // THE BAR
        // ============================================================

        /// <summary>
        /// How far through its half the game says it is, from its status text: the step it
        /// names and the number after the name. False if it names none of the steps known
        /// here, or gives no number: the bar then stays where it is.
        /// </summary>
        internal static bool Read(string status, bool loading, out float through)
        {
            through = 0f;

            if (string.IsNullOrEmpty(status))
                return false;

            foreach (Step step in loading ? LoadingSteps : SavingSteps)
            {
                if (step.Label >= Lang.gen.Length)
                    continue;

                string label = Lang.gen[step.Label].Value;

                if (string.IsNullOrEmpty(label) || !status.StartsWith(label, StringComparison.Ordinal))
                    continue;

                if (!Percent(status, label.Length, out int percent))
                    return false;

                through = MathHelper.Lerp(step.From, step.To, MathHelper.Clamp(percent / 100f, 0f, 1f));
                return true;
            }

            return false;
        }

        /// <summary>The whole number the game put after a step's name, before its "%".</summary>
        private static bool Percent(string status, int from, out int percent)
        {
            percent = 0;

            int at = from;

            while (at < status.Length && status[at] == ' ')
                at++;

            int digits = 0;

            while (at < status.Length && status[at] >= '0' && status[at] <= '9' && digits < 4)
            {
                percent = percent * 10 + (status[at] - '0');
                at++;
                digits++;
            }

            return digits > 0 && at < status.Length && status[at] == '%';
        }

        /// <summary>Raises where the bar should be by what the game says now, and moves the drawn bar toward it.</summary>
        private static void Follow(Phase phase, float seconds)
        {
            string said = Main.statusText;

            if (ReferenceEquals(said, leftOver))
                said = null;
            else
                leftOver = null;

            switch (phase)
            {
                case Phase.Saving:
                    if (Read(said, false, out float saved))
                        toward = Math.Max(toward, saved * saveShare);

                    break;

                case Phase.Loading:
                    // The saving is done, whatever was last heard of it.
                    toward = Math.Max(toward, saveShare);

                    if (Read(said, true, out float loaded))
                        toward = Math.Max(toward, saveShare + loaded * (1f - saveShare));

                    break;

                default:
                    toward = 1f;
                    break;
            }

            bar += (toward - bar) * (1f - (float)Math.Exp(-seconds * BarRate));

            if (toward - bar < 0.002f)
                bar = toward;
        }

        // ============================================================
        // EVERY FRAME
        // ============================================================

        /// <summary>
        /// Once a frame while the passage is at black, inside the passage's own batch (raw
        /// window pixels). <paramref name="fade"/> is how much of it is left on the way out
        /// (<see cref="Phase.Arrived"/>): 1 to 0. <paramref name="seconds"/> is the frame's
        /// length.
        /// </summary>
        internal static void Draw(SpriteBatch spriteBatch, Viewport window, Phase phase, float fade, float seconds)
        {
            if (broken)
                return;

            try
            {
                seconds = MathHelper.Clamp(seconds, 0f, 0.1f);

                Follow(phase, seconds);

                if (phase == Phase.Arrived)
                {
                    shown = Math.Min(shown, MathHelper.Clamp(fade, 0f, 1f));
                }
                else
                {
                    waited += seconds;

                    if (waited >= Wait)
                        shown = Math.Min(1f, shown + seconds / ComeUp);

                    bool down = Main.hasFocus && Keyboard.GetState().IsKeyDown(NextTip);

                    if (down && !keyWas && shown > 0f)
                        Turn();

                    keyWas = down;
                }

                if (shown <= 0f)
                    return;

                tipAge += seconds;

                Paint(spriteBatch, new Frame(window.Width, window.Height, Main.SettingDontScaleMainMenuUp), shown, Math.Min(1f, tipAge / TipComeUp));
            }
            catch (Exception e)
            {
                Break(e);
            }
        }

        private static void Paint(SpriteBatch spriteBatch, Frame frame, float all, float tip)
        {
            // ---- the picture, and its edges over it ----
            if (art != null && art.IsLoaded)
            {
                Texture2D picture = art.Value;

                // Covers the window whatever its shape: the long way cut, not squeezed.
                float cover = Math.Max(frame.Width / (float)picture.Width, frame.Height / (float)picture.Height);

                spriteBatch.Draw(picture, new Vector2(frame.Width, frame.Height) * 0.5f, null, Color.White * all, 0f,
                    new Vector2(picture.Width, picture.Height) * 0.5f, cover, SpriteEffects.None, 0f);
            }

            if (edges != null && edges.IsLoaded)
                spriteBatch.Draw(edges.Value, new Rectangle(0, 0, frame.Width, frame.Height), Color.White * all);

            DynamicSpriteFont small = FontAssets.MouseText.Value;
            DynamicSpriteFont big = FontAssets.DeathText.Value;

            float scale = frame.Scale;
            float line = small.LineSpacing * scale;

            // The title's font is the game's big one, brought down to a line a little taller than the tip's own.
            float titleScale = scale * TitleRatio * small.LineSpacing / big.LineSpacing;
            float titleLine = line * TitleRatio;

            float spread = 1.5f * scale;

            // ---- the tip: a title, and what it has to say under it, every line ending at the same edge ----
            if (title.Length > 0 || text.Length > 0)
            {
                float alpha = all * tip;
                Color shade = Color.Black * (alpha * 0.8f);

                // Wrapped by the game's own hand, which knows a key's picture from a word. It asks for the width at no scale.
                List<List<TextSnippet>> rows = text.Length > 0
                    ? Utils.WordwrapStringSmart(text, TextColour * alpha, small, Math.Max(1, (int)(frame.TextWide / scale)), MostLines)
                    : new List<List<TextSnippet>>();

                float gap = line * 0.3f;
                float tall = titleLine + gap + rows.Count * line;
                float top = frame.TitleTop;

                // A long one is moved up rather than let run into the key under it.
                float lowest = frame.Foot - line * 1.4f;

                if (top + tall > lowest)
                    top = Math.Max(line, lowest - tall);

                if (title.Length > 0)
                {
                    float wide = ChatManager.GetStringSize(big, title, new Vector2(titleScale)).X;

                    ChatManager.DrawColorCodedStringWithShadow(spriteBatch, big, title, new Vector2(frame.Right - wide, top),
                        TitleColour * alpha, shade, 0f, Vector2.Zero, new Vector2(titleScale), -1f, spread);
                }

                float y = top + titleLine + gap;

                foreach (List<TextSnippet> row in rows)
                {
                    // A row broken at a space keeps the space: it is not to push the row off its edge.
                    if (row.Count > 0 && row[row.Count - 1].GetType() == typeof(TextSnippet))
                        row[row.Count - 1].Text = row[row.Count - 1].Text.TrimEnd(' ');

                    TextSnippet[] snippets = row.ToArray();
                    float wide = ChatManager.GetStringSize(small, snippets, new Vector2(scale)).X;

                    ChatManager.DrawColorCodedStringWithShadow(spriteBatch, small, snippets, new Vector2(frame.Right - wide, y), 0f,
                        TextColour * alpha, shade, Vector2.Zero, new Vector2(scale), out _, -1f, spread);

                    y += line;
                }

                // ---- the key in its box, and what it does, in the corner under them ----
                string next = ":" + Language.GetTextValue(NextKey);
                float nextWide = ChatManager.GetStringSize(small, next, new Vector2(scale)).X;
                float between = line * 0.35f;
                int side = (int)Math.Round(line * 0.8f);

                var box = new Rectangle((int)Math.Round(frame.Right - nextWide - between - side), (int)Math.Round(frame.Foot - side * 0.5f), side, side);
                Color foot = FootColour * all;
                Color footShade = Color.Black * (all * 0.8f);

                Outline(spriteBatch, box, frame.Thin, foot);

                string letter = NextTip.ToString();
                float letterWide = small.MeasureString(letter).X;

                ChatManager.DrawColorCodedStringWithShadow(spriteBatch, small, letter,
                    new Vector2(box.X + box.Width * 0.5f, box.Y + box.Height * 0.5f), foot, footShade, 0f,
                    new Vector2(letterWide * 0.5f, small.LineSpacing * CapMiddle), new Vector2(scale * 0.78f), -1f, spread);

                ChatManager.DrawColorCodedStringWithShadow(spriteBatch, small, next,
                    new Vector2(box.Right + between, frame.Foot - line * CapMiddle), foot, footShade, 0f,
                    Vector2.Zero, new Vector2(scale), -1f, spread);
            }

            // ---- the bar: a line with a mark at each end, as much of it filled as is done, and a mark where that stops ----
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            int left = (int)Math.Round(frame.BarLeft);
            int right = (int)Math.Round(frame.BarRight);
            int middle = (int)Math.Round(frame.Foot);
            int filled = (int)Math.Round((right - left) * MathHelper.Clamp(bar, 0f, 1f));

            Color faint = TitleColour * (all * 0.4f);
            Color bright = TitleColour * all;

            spriteBatch.Draw(pixel, new Rectangle(left, middle - frame.Thin / 2, right - left, frame.Thin), faint);
            spriteBatch.Draw(pixel, new Rectangle(left, middle - frame.Mark / 2, frame.Thin, frame.Mark), bright);
            spriteBatch.Draw(pixel, new Rectangle(right - frame.Thin, middle - frame.Mark / 2, frame.Thin, frame.Mark), faint);

            if (filled > 0)
            {
                spriteBatch.Draw(pixel, new Rectangle(left, middle - frame.Thick / 2, filled, frame.Thick), bright);
                spriteBatch.Draw(pixel, new Rectangle(left + filled - frame.Thin, middle - frame.Mark / 2, frame.Thin, frame.Mark), bright);
            }
        }

        /// <summary>The four sides of a box, inside its bounds.</summary>
        private static void Outline(SpriteBatch spriteBatch, Rectangle box, int thick, Color colour)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, thick), colour);
            spriteBatch.Draw(pixel, new Rectangle(box.X, box.Bottom - thick, box.Width, thick), colour);
            spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y + thick, thick, box.Height - thick * 2), colour);
            spriteBatch.Draw(pixel, new Rectangle(box.Right - thick, box.Y + thick, thick, box.Height - thick * 2), colour);
        }
    }
}
