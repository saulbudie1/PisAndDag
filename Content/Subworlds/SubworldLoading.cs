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
    /// THE PICTURE is one of those in the folder beside this file, LoadingArts: every
    /// picture in it (and in folders inside it), whatever it is called, so one is added by
    /// putting it there and taken out by taking it away. Which is up is drawn by lot, any
    /// of them but not the one that was up last, and drawn ahead: there is always one held
    /// ready, loaded where the time it takes is no one's (as a world is entered, and at
    /// each passage once the game has been set to its saving), so that the frame a door is
    /// tried on has nothing to load. A picture may be any size (1920 by 1080 is the size it
    /// is mostly seen at): it is drawn to cover the window whatever its shape, cut at the
    /// sides or the top and bottom rather than squeezed. LoadingFrame, beside this file,
    /// goes over it, stretched to the window's own edges: clear in the middle, black at the
    /// edges and blackest where the words and the bar are, and grained, blotted and
    /// scratched as an old print is (PlaceholderTextures.md), so any picture put under it
    /// gets the same edges with nothing else done. The frame is stored premultiplied, as
    /// the game blends: tModLoader does not do that to a mod's pictures, and one saved the
    /// plain way would have its pale marks come out white. No picture in the folder, or no
    /// frame, and that layer is left out; neither, and the screen is the plain black it was.
    ///
    /// THE TIPS are the hjson's (Mods.PisAndDag.Tips.List): every entry there with a Title
    /// and a Text is one, so a tip is added by writing it there and nowhere else. They are
    /// gone through in a shuffled order, each once before any comes round again. One time
    /// in <see cref="GameTipOneIn"/> the tip is one of the game's own instead (the ones its
    /// own loading screen has, worded exactly as it words them: GameTipsDisplay), under
    /// one title for all of them (Tips.VanillaTitle). Two of the game's are left out, as
    /// untrue with this mod in (<see cref="NotHere"/>). Like the picture, they are found
    /// ahead (as a world is entered, and as one is left), so that a door's own frame has
    /// only to turn to one.
    ///
    /// THE BAR goes by the clock, and is kept honest by the game's own word. A passage is
    /// eleven steps the game takes in one order, six of leaving a world and five of
    /// entering one, and it says which it is at in Main.statusText. Each step has a stretch
    /// of the bar as long as the step is expected to take: what it took the last time this
    /// same passage was made, and before there has been one, a reckoning from the two
    /// worlds' sizes (<see cref="Reckon"/>). Within a step the bar goes by how long the
    /// step has run against how long it was expected to: most of its stretch at the
    /// clock's own pace and the rest slower and slower, so it is not at a step's end before
    /// the game is. Seven of the steps the game also counts through, as a number and a "%"
    /// after the step's name, and in six of those the bar goes no further than the count:
    /// a step slower than was expected does not run ahead of itself (the seventh, the
    /// settling of the liquids, is counted in a way that cannot be held to:
    /// <see cref="HeldToCount"/>). The count alone is not gone by, for it is not the whole
    /// of a step (reading a great world's tiles is a quarter of "Loading world data"; the
    /// rest, under the same line at 100%, is tModLoader reading what mods keep), and four
    /// of the steps have none, one of them among the longest (what mods keep in a world,
    /// written; and tModLoader's backup of the player is as long again wherever the
    /// passage has not left it out). The bar never goes back, and is full when the player
    /// is put into the other world.
    ///
    /// All of it is up from the frame the door is tried: it comes with the passage's black
    /// and is as strong as the black is, so the world goes down into the picture and its
    /// tip, with no plain black between. The bar is empty until the world is left, and the
    /// key turns no tip until then. Client-side, and drawn from the passage's own batch.
    /// </summary>
    public static class SubworldLoading
    {
        /// <summary>What the passage is doing, as far as this is concerned.</summary>
        public enum Phase
        {
            /// <summary>The door has been tried: the screen is on its way to black, and the world still runs under it.</summary>
            Leaving,

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

        /// <summary>
        /// The names the game's own tips go by, as its loading screen asks for them
        /// (GameTipsDisplay): those for anyone, and those for whoever plays with a keyboard
        /// or with a gamepad.
        /// </summary>
        private const string GameTips = "LoadingTips_Default.";
        private const string KeyboardTips = "LoadingTips_Keyboard.";
        private const string GamepadTips = "LoadingTips_GamePad.";

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

        /// <summary>The folder the pictures are in, as the mod's own files have it, and the frame.</summary>
        private const string ArtFolder = "Content/Subworlds/LoadingArts/";
        private const string FramePath = "Content/Subworlds/LoadingFrame";

        /// <summary>
        /// What a picture's file ends in inside a built mod: tModLoader turns every .png into
        /// a .rawimg as it packs one (ContentConverters); the other is in case one was not.
        /// </summary>
        private static readonly string[] PictureEndings = { ".rawimg", ".png" };

        /// <summary>Every picture in the folder, by the name the game is asked for it by, in a settled order.</summary>
        private static readonly List<string> arts = new();

        /// <summary>The one that is up for this passage.</summary>
        private static Asset<Texture2D> art;

        /// <summary>The one held ready for the next door tried, loaded already (<see cref="HoldAPicture"/>): null if there is none to hold.</summary>
        private static Asset<Texture2D> readied;

        /// <summary>The name of the last one drawn by lot, so that the next is another.</summary>
        private static string lastArt;

        private static Asset<Texture2D> edges;

        // ---- how it looks ----------------------------------------------------------------------

        /// <summary>Seconds a tip turned to takes to come up. The first of a passage does not: it is up with the rest.</summary>
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
        /// The steps of a passage, in the order the game takes them. Leaving a world
        /// (WorldGen.SaveAndQuitCallBack): the player's map of it is written; the player's
        /// file is written, and the world's file goes into tModLoader's backup of the day
        /// (Player.SavePlayerFile_Write, then WorldFile.InternalSaveWorld before it says
        /// anything of its own; in any other saving the player's file and every map they
        /// have go into a backup of the day first, which a passage leaves out:
        /// SubworldPassage); the world is written, and what was written is checked; the
        /// game's own backups of the world are moved along; and what mods keep in a world
        /// is written (WorldIO.Save). Entering one (WorldGen.playWorldCallBack): the world
        /// before is cleared out (WorldGen.clearWorld), the tiles are read, the liquids
        /// settled, the player's map read, and the map drawn.
        /// </summary>
        internal const int MapSaved = 0;
        internal const int PlayerKept = 1;
        internal const int WorldSaved = 2;
        internal const int SaveChecked = 3;
        internal const int WorldKept = 4;
        internal const int ModsSaved = 5;
        internal const int Cleared = 6;
        internal const int WorldRead = 7;
        internal const int Settled = 8;
        internal const int MapRead = 9;
        internal const int MapDrawn = 10;
        internal const int StepCount = 11;

        /// <summary>
        /// What the game writes in Main.statusText at each step: one of its own lines
        /// (Lang.gen, by number) or, for the two steps that are tModLoader's, one of its (by
        /// key). After seven of them it puts how far through the step it is, a space, a
        /// number and a "%" (MapHelper.InternalSaveMap, WorldFile.SaveWorldTiles,
        /// ValidateWorld, LoadWorldTiles, Liquid.UpdateProgressDisplay and WorldFile.LoadWorld,
        /// MapHelper.LoadMapVersion2, WorldGen.playWorldCallBack).
        /// </summary>
        private static readonly int[] GameLine = { 66, -1, 49, 73, 50, -1, 65, 51, 27, 67, 68 };

        private static readonly string[] LoaderLine =
        {
            null, "tModLoader.BackingUpPlayerFile", null, null, null, "tModLoader.SavingModdedWorldData", null, null, null, null, null,
        };

        /// <summary>
        /// Whether the bar is held to the game's count of a step. Not to the settling's: its
        /// number is two counts under one name, the game's quick pass over the rows running
        /// it to 50 and the settling of whatever still moves taking it on to 100, so in a
        /// world with nothing left to settle it stands at half while the step runs on.
        /// </summary>
        internal static bool HeldToCount(int of) => of != Settled;

        /// <summary>
        /// How much of a step's stretch goes by at the clock's own pace; the rest is closed
        /// on slower and slower (<see cref="Creep"/>).
        /// </summary>
        internal const float EvenShare = 0.8f;

        /// <summary>
        /// How long each step of this passage is expected to take, in seconds; the sum of
        /// those before each; and the sum of all.
        /// </summary>
        private static readonly float[] expected = new float[StepCount];
        private static readonly float[] before = new float[StepCount];
        private static float whole;

        /// <summary>What each step of this passage did take, as far as it has got.</summary>
        private static readonly float[] took = new float[StepCount];

        /// <summary>The step the game is at (-1 until it has said anything), and when it got there, by the passage's clock.</summary>
        private static int step = -1;
        private static double stepBegan;

        /// <summary>The player is in the other world, and what this passage took has been kept.</summary>
        private static bool over;

        /// <summary>Runs from the moment the world is left.</summary>
        private static readonly System.Diagnostics.Stopwatch clock = new();

        /// <summary>
        /// What each step took the last time a passage was made between worlds of these two
        /// sizes (tiles left, tiles entered), this game. That is what the next one like it
        /// is expected to take: the same machine, the same player and worlds as big.
        /// </summary>
        private static readonly Dictionary<(long, long), float[]> lastTime = new();
        private static (long, long) sizes;

        /// <summary>Where the bar should be, 0 to 1: only ever raised.</summary>
        private static float toward;

        /// <summary>Where it is drawn: closing on that.</summary>
        private static float bar;

        /// <summary>Seconds the picture for the passage after the last took to load, once the world had been left: for client.log.</summary>
        internal static double PictureTook { get; private set; }

        // ---- the tip ---------------------------------------------------------------------------

        private static readonly UnifiedRandom dice = new UnifiedRandom();

        /// <summary>The mod's tips, each the key its Title and Text hang from, in the order they are gone through.</summary>
        private static readonly List<string> own = new();
        private static int ownAt;

        /// <summary>The game's.</summary>
        private static readonly List<LocalizedText> games = new();
        private static string lastGame;

        /// <summary>The tips have been found since the mod was loaded (<see cref="Gather"/>).</summary>
        private static bool gathered;

        /// <summary>One of the game's tips has been worded since the mod was loaded (<see cref="WorldEntered"/>).</summary>
        private static bool worded;

        /// <summary>The tip that is up, worded and ready.</summary>
        private static string title = "";
        private static string text = "";

        /// <summary>Seconds the tip that is up has been up.</summary>
        private static float tipAge;

        /// <summary>How much of it all shows, 0 to 1.</summary>
        private static float shown;

        private static bool keyWas;

        /// <summary>Something here threw: none of it is drawn again until the mod is loaded again. The passage goes on, plain black.</summary>
        private static bool broken;

        /// <summary>
        /// The pictures in the folder are found, by name only: none is loaded for being
        /// found (the first is when a world is entered: <see cref="WorldEntered"/>). The
        /// frame is asked for, if it is there. Client only.
        /// </summary>
        internal static void Load(Mod mod)
        {
            arts.Clear();
            arts.AddRange(Pictures(mod.GetFileNames(), mod.Name));
            art = null;
            readied = null;
            lastArt = null;
            edges = Ask(mod, FramePath);
        }

        /// <summary>
        /// The pictures among a mod's files (Mod.GetFileNames: every file packed into it, by
        /// its path with forward slashes): each one in the folder, or in a folder inside it,
        /// as the name the game knows it by, which is the mod's name, a slash, and the path
        /// with no ending. Sorted, so the order does not hang on how the mod was packed.
        /// </summary>
        internal static List<string> Pictures(IEnumerable<string> files, string modName)
        {
            var found = new List<string>();

            if (files == null)
                return found;

            foreach (string file in files)
            {
                if (string.IsNullOrEmpty(file))
                    continue;

                string path = file.Replace('\\', '/');

                if (!path.StartsWith(ArtFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (string ending in PictureEndings)
                {
                    // Something must be left for a name once the folder and the ending are off.
                    if (path.Length <= ArtFolder.Length + ending.Length || !path.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = modName + "/" + path.Substring(0, path.Length - ending.Length);

                    if (!found.Contains(name))
                        found.Add(name);

                    break;
                }
            }

            found.Sort(StringComparer.Ordinal);
            return found;
        }

        /// <summary>
        /// One of the pictures, by lot: any of them, but not the one that was up last if there
        /// is another. Null if there is none.
        /// </summary>
        internal static string Choose()
        {
            if (arts.Count == 0)
                return null;

            int at = dice.Next(arts.Count);

            if (arts.Count > 1 && arts[at] == lastArt)
                at = (at + 1 + dice.Next(arts.Count - 1)) % arts.Count;

            lastArt = arts[at];
            return lastArt;
        }

        /// <summary>
        /// Draws the next passage's picture by lot and loads it, here and now, to be held
        /// until a door is tried. The loading is a wait (the file is read, and a picture as
        /// big as the screen handed to the graphics card), so this is called only where a
        /// wait is not seen. One that will not load is struck off, with a line in
        /// client.log, and another is drawn, until one loads or there is none left: then no
        /// picture is held, and the rest of the screen is not touched by it.
        /// </summary>
        private static void HoldAPicture()
        {
            readied = null;

            // The one that is up, or was up last: not that one again, whatever is struck off meanwhile.
            string up = lastArt;

            // Every turn of this either ends it or strikes one off: never more turns than there are pictures.
            for (int left = arts.Count; left > 0; left--)
            {
                string name = Choose();

                if (name == null)
                    return;

                try
                {
                    readied = ModContent.Request<Texture2D>(name, AssetRequestMode.ImmediateLoad);
                    return;
                }
                catch (Exception e)
                {
                    readied = null;
                    arts.Remove(name);
                    lastArt = up;

                    try
                    {
                        ModContent.GetInstance<SubworldPassage>()?.Mod?.Logger?.Warn("Subworlds: the loading picture " + name + " would not load and is left out until the mod is loaded again.", e);
                    }
                    catch (Exception)
                    {
                        // Nothing to say it with.
                    }
                }
            }
        }

        /// <summary>
        /// A world has been entered. What the first door tried in it will want is made ready
        /// now, so that the door's own frame has nothing to find, load or run for the first
        /// time: the tips are found, and a picture is loaded and held if none is held
        /// already. The wait is beside a longer one of the game's own: on the same call,
        /// just before, tModLoader has every thread stand while it collects all garbage
        /// (Player.Hooks.EnterWorld: Interface.ResetData).
        /// </summary>
        internal static void WorldEntered()
        {
            if (broken)
                return;

            try
            {
                Gather();

                // The first wording of one of the game's own tips is slow, some thousandths
                // of a second: its code is the game's, and is compiled as it first runs. It
                // is run once here, for nothing, so that the first run is not on a door's frame.
                if (!worded && games.Count > 0)
                {
                    worded = true;
                    Worded(games[0]);
                }

                if (readied == null)
                    HoldAPicture();
            }
            catch (Exception e)
            {
                Break(e);
            }
        }

        /// <summary>
        /// The world has been left, and the picture that was held is on the screen: another
        /// is loaded and held for the passage after this one. The game's saving is on a
        /// thread of its own by now, and this one has nothing to do meanwhile but draw what
        /// is up already. (Nothing of the game's is touched here but the mod's own
        /// pictures: the saving thread is at work on the rest.)
        /// </summary>
        internal static void WorldLeft()
        {
            PictureTook = 0.0;

            if (broken)
                return;

            try
            {
                double from = clock.Elapsed.TotalSeconds;

                HoldAPicture();

                PictureTook = clock.Elapsed.TotalSeconds - from;
            }
            catch (Exception e)
            {
                Break(e);
            }
        }

        private static Asset<Texture2D> Ask(Mod mod, string path)
        {
            string name = mod.Name + "/" + path;

            return ModContent.HasAsset(name) ? ModContent.Request<Texture2D>(name) : null;
        }

        internal static void Unload()
        {
            arts.Clear();
            art = null;
            readied = null;
            lastArt = null;
            edges = null;
            lastTime.Clear();
            clock.Reset();
            step = -1;
            over = false;
            own.Clear();
            games.Clear();
            gathered = false;
            worded = false;
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
        /// A door has been tried. The picture held ready is this passage's, and a tip is
        /// turned to: both are up from this frame on, coming with the black. Nothing is
        /// found or loaded here. A world entered has left a picture held and the tips found
        /// (<see cref="WorldEntered"/>), and so has a world left, for the passage after it
        /// (<see cref="Begin"/>, <see cref="WorldLeft"/>); only if either is wanting is it
        /// seen to now.
        /// </summary>
        internal static void Open()
        {
            toward = 0f;
            bar = 0f;
            shown = 0f;
            art = null;

            // A key already down is not a press.
            keyWas = true;

            if (broken)
                return;

            try
            {
                if (readied == null)
                    HoldAPicture();

                // It stays the one held until the world is left (WorldLeft): a passage let
                // go of before that has not used it up.
                art = readied;

                if (!gathered)
                    Gather();

                Turn();

                // The first tip is not behind the rest: it is up with it.
                tipAge = TipComeUp;
            }
            catch (Exception e)
            {
                Break(e);
            }
        }

        /// <summary>
        /// The world is about to be left: the bar begins. <paramref name="leaving"/> and
        /// <paramref name="entering"/> are the two worlds' sizes in tiles, for sharing the bar
        /// out; 0 for one not known. <paramref name="backedUp"/> is whether tModLoader's
        /// backup of the player is in the saving, as it is in any other, or left out of it
        /// (SubworldPassage): its step is reckoned by that. The tips are found afresh here,
        /// for the turning of them during this passage and for the passage after it: the
        /// screen is at black, and the saving, which is another thread at work among the
        /// game's things, has not begun.
        /// </summary>
        internal static void Begin(long leaving, long entering, bool backedUp)
        {
            toward = 0f;
            bar = 0f;

            // A key already down is not a press.
            keyWas = true;

            Expect(leaving, entering, backedUp);
            clock.Restart();

            if (broken)
                return;

            // Whatever the game said last before this (an autosave's last line, the passage
            // in) is wiped, so that any line read from here on is this passage's own. It
            // cannot be told by looking: the lines the game gives no number with are one
            // string each, said again and again, and a new one is the old one.
            Main.statusText = string.Empty;

            try
            {
                Gather();
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
        /// Finds the tips: the mod's in the hjson, and the game's, the ones its own loading
        /// screen finds. The mod's are kept in the order they were being gone through unless
        /// the list itself has changed (another language, a reload). Done where the wait is
        /// not seen, and so not on the frame a door is tried: as a world is entered, and as
        /// one is left for a passage, which finds what the passage after it will use.
        /// Neither is a time when the game's saving is at work on another thread.
        /// </summary>
        private static void Gather()
        {
            string theirs = PlayerInput.UsingGamepad ? GamepadTips : KeyboardTips;
            var found = new List<string>();

            games.Clear();

            // One asking for all of it: each asking is the game going over every word it has
            // (LanguageManager.FindAll). And each name is tested by its letters alone. The
            // game's own filter for its tips (Lang.CreateDialogFilter) tests as the machine's
            // language has words compared, which outside English costs many times as much,
            // and for these names comes to the same.
            foreach (LocalizedText entry in Language.FindAll((key, _) =>
                OwnTitle(key) || key.StartsWith(GameTips, StringComparison.Ordinal) || key.StartsWith(theirs, StringComparison.Ordinal)))
            {
                string key = entry.Key;

                if (!OwnTitle(key))
                {
                    if (!NotHere.Contains(key))
                        games.Add(entry);

                    continue;
                }

                string id = key.Substring(0, key.Length - TitleSuffix.Length);

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

            gathered = true;
        }

        /// <summary>The name of a tip of the mod's own: its title.</summary>
        private static bool OwnTitle(string key)
        {
            return key.StartsWith(ListPrefix, StringComparison.Ordinal) && key.EndsWith(TitleSuffix, StringComparison.Ordinal);
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
        /// How long each step of a passage between worlds of these sizes is taken to last,
        /// in seconds, before one has been made: so many seconds a million tiles of the
        /// world left, or of the world entered. Measured, not worked out: the times
        /// tModLoader's own log gives for ten passages between a world of 37.8 million tiles
        /// and the hall, on one machine, with one player (5 October 2026; the workflow notes
        /// have the table). The player's step is two things under one line. One is
        /// tModLoader's backup of the player, which is that player's: as long as the maps
        /// they have, whatever the world, 3.9 seconds of the step there. It is in the step
        /// only if <paramref name="backedUp"/>; a passage leaves it out (SubworldPassage),
        /// and what is left then, besides the writing of the player's file (put at a
        /// twentieth of a second: no log has that apart from the backup), is the other
        /// thing, tModLoader's backup of the world left, which goes by the world's size.
        /// Only the first passage each way in a game goes by any of this; after it, what
        /// that one took is used (<see cref="lastTime"/>).
        /// </summary>
        internal static float[] Reckon(long leaving, long entering, bool backedUp)
        {
            float left = Math.Max(0L, leaving) / 1000000f;
            float entered = Math.Max(0L, entering) / 1000000f;

            var seconds = new float[StepCount];

            seconds[MapSaved] = 0.128f * left;
            seconds[PlayerKept] = (backedUp ? 3.9f : 0.05f) + 0.016f * left;
            seconds[WorldSaved] = 0.0183f * left;
            seconds[SaveChecked] = 0.0066f * left;
            seconds[WorldKept] = 0.0026f * left;
            seconds[ModsSaved] = 0.0968f * left;

            // The clearing is of the map of the world left, and then of the tile grid, which is as big as it ever was.
            seconds[Cleared] = 0.08f + 0.0055f * left;
            seconds[WorldRead] = 0.047f * entered;
            seconds[Settled] = 0.021f * entered;
            seconds[MapRead] = 0.053f * entered;
            seconds[MapDrawn] = 0.035f;

            return seconds;
        }

        /// <summary>The stretches of the bar for a passage between worlds of these sizes: as the last one like it went, or as reckoned.</summary>
        private static void Expect(long leaving, long entering, bool backedUp)
        {
            sizes = (leaving, entering);

            float[] from = lastTime.TryGetValue(sizes, out float[] last) ? last : Reckon(leaving, entering, backedUp);

            whole = 0f;

            for (int i = 0; i < StepCount; i++)
            {
                expected[i] = Math.Max(0f, from[i]);
                before[i] = whole;
                whole += expected[i];
                took[i] = 0f;
            }

            step = -1;
            stepBegan = 0.0;
            over = false;
        }

        /// <summary>The line the game writes for a step, in the language it is in now. Null if it has none.</summary>
        private static string Line(int of)
        {
            // A key tModLoader does not have comes back as the key, which the game never
            // writes: the step is then never seen, and its time goes to the one before.
            if (LoaderLine[of] != null)
                return Language.GetTextValue(LoaderLine[of]);

            int line = GameLine[of];

            return line >= 0 && line < Lang.gen.Length ? Lang.gen[line]?.Value : null;
        }

        /// <summary>
        /// Which step the game's status text names: -1 if none known here.
        /// <paramref name="through"/> is how far through it the game says it is, 0 to 1, from
        /// the number after the name: -1 if it gives none.
        /// </summary>
        internal static int Read(string status, out float through)
        {
            through = -1f;

            if (string.IsNullOrEmpty(status))
                return -1;

            int named = -1;
            int length = 0;

            // The longest line it begins with, in case one step's line begins another's in some language.
            for (int i = 0; i < StepCount; i++)
            {
                string line = Line(i);

                if (!string.IsNullOrEmpty(line) && line.Length > length && status.StartsWith(line, StringComparison.Ordinal))
                {
                    named = i;
                    length = line.Length;
                }
            }

            if (named >= 0 && Percent(status, length, out int percent))
                through = MathHelper.Clamp(percent / 100f, 0f, 1f);

            return named;
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

        /// <summary>
        /// How far through a step is taken to be, 0 to 1, by how long it has run and how long
        /// it was expected to: <see cref="EvenShare"/> of the way as the clock goes, and from
        /// there closing on the end at a pace that falls away, so that a step running long
        /// still moves and one running short has little left to make up. Never 1: only the
        /// game's going on to the next step ends one.
        /// </summary>
        internal static float Creep(float run, float expect)
        {
            if (expect <= 0f || run <= 0f)
                return 0f;

            float through = run / expect;

            if (through <= EvenShare)
                return through;

            float rest = 1f - EvenShare;

            return Math.Min(0.9999f, EvenShare + rest * (1f - (float)Math.Exp(-(through - EvenShare) / rest)));
        }

        /// <summary>
        /// Raises where the bar should be by the step the game is at now and how far through
        /// it, and moves the drawn bar toward that. <paramref name="now"/> is the passage's
        /// clock, in seconds.
        /// </summary>
        private static void Follow(Phase phase, float seconds, double now)
        {
            // Before the world is left the game has said nothing of this passage, and its clock has not been started.
            if (phase == Phase.Leaving)
                return;

            if (phase == Phase.Arrived)
            {
                Keep(now);
                toward = 1f;
            }
            else
            {
                int said = Read(Main.statusText, out float through);

                // The game has gone on to a later step: the one it was at took this long. A
                // line for an earlier step (a place being made says the saving's over again,
                // between the saving and the loading) is this step still.
                if (said > step)
                {
                    if (step >= 0)
                        took[step] = (float)(now - stepBegan);

                    step = said;
                    stepBegan = now;
                }

                if (step >= 0 && whole > 0f)
                {
                    // By the clock; and where the game is counting this step, no further than its count.
                    float within = Creep((float)(now - stepBegan), expected[step]);

                    if (said == step && through >= 0f && HeldToCount(step))
                        within = Math.Min(within, through);

                    toward = Math.Max(toward, Math.Min(1f, (before[step] + expected[step] * within) / whole));
                }
            }

            bar += (toward - bar) * (1f - (float)Math.Exp(-seconds * BarRate));

            if (toward - bar < 0.002f)
                bar = toward;
        }

        /// <summary>
        /// The player has been put into the other world. What the passage took is kept here
        /// and now, and not left to the next frame drawn: with Frame Skip on, the game runs
        /// the updates a long frame has put it behind by before it draws again (FNA's
        /// Game.Tick), and the ticks this is still on the screen for (the passage's
        /// settling) can be gone by then.
        /// </summary>
        internal static void Arrive()
        {
            if (clock.IsRunning)
                Keep(clock.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// The passage is over: the last step's time is known, and what every step took is
        /// kept for the next passage between worlds of these sizes. Not if the game was never
        /// heard (nothing was drawn while it worked): that would be a row of noughts.
        /// </summary>
        private static void Keep(double now)
        {
            if (over)
                return;

            over = true;

            if (step < 0)
                return;

            took[step] = (float)(now - stepBegan);
            lastTime[sizes] = (float[])took.Clone();
        }

        // ============================================================
        // EVERY FRAME
        // ============================================================

        /// <summary>
        /// Once a frame while any of the passage's black is up, inside the passage's own
        /// batch (raw window pixels). <paramref name="fade"/> is how much of all this is to
        /// show, 0 to 1: on the way to black (<see cref="Phase.Leaving"/>) as much as the
        /// black itself, and on the way out (<see cref="Phase.Arrived"/>) what is left of
        /// it; between the two it is all up, whatever is given. <paramref name="seconds"/>
        /// is the frame's length.
        /// </summary>
        internal static void Draw(SpriteBatch spriteBatch, Viewport window, Phase phase, float fade, float seconds)
        {
            if (broken)
                return;

            try
            {
                seconds = MathHelper.Clamp(seconds, 0f, 0.1f);

                if (phase == Phase.Leaving)
                {
                    // The world is not left yet, and still runs: there is nothing for the
                    // bar to go by, and the keys are the world's.
                    shown = MathHelper.Clamp(fade, 0f, 1f);
                }
                else
                {
                    // The steps are timed by a clock of their own: a frame's length as the game
                    // gives it is never more than half a second, however long the wait was, and
                    // with Frame Skip on it is so many updates' worth (FNA's Game.Tick).
                    Follow(phase, seconds, clock.Elapsed.TotalSeconds);

                    if (phase == Phase.Arrived)
                    {
                        shown = Math.Min(shown, MathHelper.Clamp(fade, 0f, 1f));
                    }
                    else
                    {
                        shown = 1f;

                        bool down = Main.hasFocus && Keyboard.GetState().IsKeyDown(NextTip);

                        if (down && !keyWas)
                            Turn();

                        keyWas = down;
                    }
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
