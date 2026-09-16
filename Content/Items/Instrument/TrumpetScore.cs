using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Instrument
{
    /// <summary>
    /// The trumpet playing itself: "/trumpet ode to joy" while it is in hand.
    ///
    /// A score is phrases of notes, each note a band, an octave and a length in
    /// ticks. Within a phrase the button stays down and the cursor slides from band
    /// to band, the way a player would drag; a phrase ends with the button up. A
    /// repeated note is re-struck with a short lift, since sliding to the band you
    /// are already on would not sound again.
    ///
    /// It is played through the same path as a hand: the cursor is actually moved to
    /// the band (Mouse.SetPosition, so you can watch it), and Trumpet reads a press
    /// from Pressed as if it were the button. Moving the mouse yourself, opening the
    /// inventory, or putting the trumpet away ends it; the hand is always in charge.
    ///
    /// Only public-domain tunes go here.
    /// </summary>
    public static class TrumpetAutoplay
    {
        public readonly struct Note
        {
            public readonly int Band;      // 0..11, -1 for a rest
            public readonly int Octave;    // 0 low, 1 high
            public readonly int Ticks;

            public Note(int band, int octave, int ticks)
            {
                Band = band;
                Octave = octave;
                Ticks = ticks;
            }
        }

        /// <summary>Ticks of silence between two of the same note in one phrase, so the second one strikes.</summary>
        private const int Restrike = 4;

        /// <summary>How far the mouse may stray from where it was put before the hand is judged to have taken over.</summary>
        private const int TakeOverPixels = 28;

        public static bool Active { get; private set; }

        /// <summary>What Trumpet reads as the button, and which one.</summary>
        internal static bool Pressed { get; private set; }
        internal static int Octave { get; private set; }
        internal static int Band { get; private set; } = -1;

        private static List<List<Note>> score;
        private static int phrase, note, left, gap;

        /// <summary>Ticks after moving the cursor before its position is trusted; the input reads a frame behind.</summary>
        private static int settle;
        private static Point placed;
        private static int mouseX;

        // ---- the scores ----------------------------------------------------------------
        //
        // Written as text, one tune per string:
        //
        //   E E F G | G F E D | C:2 ~ Eh:1.5 D:.5 D:2
        //
        //   E        a note, one beat, low octave (left click)
        //   Eh       the same note high (right click)          C#h is C sharp, high
        //   E:2      held for two beats;  E:.5  half a beat;  E:1.5  a beat and a half
        //   ~        a rest of one beat;  ~:2  two beats
        //   |        end of phrase: lift, breathe, start the next press
        //   bpm=112  anywhere in the string sets the tempo (default 110)
        //   key=C    the key the tune is written in (default C); key=F# and key=Bb work
        //   minor    the tune is written in the minor (default major); "major" says so
        //   swing    half beats play long-short (jazz); "straight" turns it back off
        //
        // Beats become ticks at the tempo. "/trumpet play <score>" takes the same text.
        //
        // The key and mode are only used to reshape a tune on request: "/trumpet ode to
        // joy minor" lowers the third, sixth and seventh of the key, so a major tune
        // turns minor (and a minor one major with "major"); "+3" or "-2" after a name
        // moves the whole tune that many semitones, and "high" an octave up.

        private const int DefaultBpm = 110;

        /// <summary>Key of a score that names none: C, band 3 of the twelve.</summary>
        private const int DefaultKey = 3;

        /// <summary>Key and mode a score declared, from the last Parse.</summary>
        public static int ParsedKey { get; private set; } = DefaultKey;
        public static bool ParsedMinor { get; private set; }

        /// <summary>Beats in each phrase of the last Parse, as written, for "/trumpet check".</summary>
        public static List<float> PhraseBeats { get; } = new();

        private static readonly Dictionary<string, string> Scores = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ode to joy"] =
                "bpm=112 " +
                "E E F G G F E D C C D E E:1.5 D:.5 D:2 | " +
                "E E F G G F E D C C D E D:1.5 C:.5 C:2",

            ["saints"] =
                "bpm=120 " +
                "C E F G:4 | C E F G:4 | C E F G:2 E:2 C:2 E:2 D:4 | " +
                "E E D:2 C:3 C E:2 G:2 G G:3 F:4 | E F G:2 E:2 C:2 D:2 C:4",
        };

        public static IEnumerable<string> Names
        {
            get
            {
                foreach (string k in Scores.Keys) yield return k;
                foreach (string k in MelodyFiles.Names()) yield return k;
                foreach (string k in TrumpetSongbook.Local().Keys) yield return k;
                foreach (string k in TuneFiles.Names()) yield return k;
            }
        }

        /// <summary>
        /// Reads score text into phrases. Returns null, with a reason, if a token
        /// does not parse, so a typo in "/trumpet play" is reported rather than played.
        /// </summary>
        public static List<List<Note>> Parse(string text, out string error)
        {
            error = null;
            var phrases = new List<List<Note>>();
            var current = new List<Note>();
            int bpm = DefaultBpm;
            ParsedKey = DefaultKey;
            ParsedMinor = false;
            bool swing = false;
            float at = 0f; // beats into the phrase, for swing
            PhraseBeats.Clear();

            // Any whitespace splits, so a pasted block with line breaks or tabs reads the same.
            string[] toks = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            for (int t = 0; t < toks.Length; t++)
            {
                string tok = toks[t];

                if (tok == "|")
                {
                    if (current.Count > 0) { phrases.Add(current); PhraseBeats.Add(at); }
                    current = new List<Note>();
                    at = 0f;
                    continue;
                }

                if (tok.Equals("minor", StringComparison.OrdinalIgnoreCase)) { ParsedMinor = true; continue; }
                if (tok.Equals("major", StringComparison.OrdinalIgnoreCase)) { ParsedMinor = false; continue; }
                if (tok.Equals("swing", StringComparison.OrdinalIgnoreCase)) { swing = true; continue; }
                if (tok.Equals("straight", StringComparison.OrdinalIgnoreCase)) { swing = false; continue; }

                // Key, same forgiveness as tempo: key=F#, key = Bb, key:G.
                if (tok.StartsWith("key", StringComparison.OrdinalIgnoreCase))
                {
                    string k = tok[3..].TrimStart('=', ':');

                    if (k.Length == 0 && t + 1 < toks.Length)
                    {
                        k = toks[++t].TrimStart('=', ':');

                        if (k.Length == 0 && t + 1 < toks.Length)
                            k = toks[++t];
                    }

                    int root = NoteIndex(k);

                    if (root < 0)
                    {
                        error = "bad key \"" + tok + " " + k + "\"";
                        return null;
                    }

                    ParsedKey = root;
                    continue;
                }

                // Tempo, forgiving: bpm=105, bpm = 105, bpm:105, bpm 105.
                if (tok.StartsWith("bpm", StringComparison.OrdinalIgnoreCase))
                {
                    string num = tok[3..].TrimStart('=', ':');

                    if (num.Length == 0 && t + 1 < toks.Length)
                    {
                        num = toks[++t].TrimStart('=', ':');

                        if (num.Length == 0 && t + 1 < toks.Length)
                            num = toks[++t];
                    }

                    if (!int.TryParse(num, out bpm) || bpm < 20 || bpm > 300)
                    {
                        error = "bad tempo \"" + tok + " " + num + "\"";
                        return null;
                    }
                    continue;
                }

                float beats = 1f;
                string head = tok;
                int colon = tok.IndexOf(':');

                if (colon >= 0)
                {
                    head = tok[..colon];

                    if (!float.TryParse(tok[(colon + 1)..], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out beats) || beats <= 0f)
                    {
                        error = "bad length in \"" + tok + "\"";
                        return null;
                    }
                }

                // Swing: a pair of half beats plays long-short, two thirds and one third
                // of the beat, the way a jazz tune is meant to be read. Judged by where
                // in the phrase the note falls, so a rest on the beat still swings the
                // eighth after it.
                float length = beats;

                if (swing && beats == 0.5f)
                {
                    float inBeat = at - (float)Math.Floor(at);
                    length = inBeat < 0.25f ? 2f / 3f : 1f / 3f;
                }

                at += beats;
                int ticks = Math.Max(1, (int)Math.Round(length * 3600f / bpm));

                if (head == "~")
                {
                    current.Add(new Note(-1, 0, ticks));
                    continue;
                }

                int octave = 0;

                if (head.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                {
                    octave = 1;
                    head = head[..^1];
                }

                int band = NoteIndex(head);

                if (band < 0)
                {
                    error = "no note called \"" + tok + "\"";
                    return null;
                }

                current.Add(new Note(band, octave, ticks));
            }

            if (current.Count > 0) { phrases.Add(current); PhraseBeats.Add(at); }

            if (phrases.Count == 0)
            {
                error = "nothing to play";
                return null;
            }

            return phrases;
        }

        /// <summary>Band of a note name: A, A#, Bb, c#; -1 if it is not one. Flats read as the sharp below.</summary>
        private static int NoteIndex(string name)
        {
            if (name.Length == 2 && (name[1] == 'b' || name[1] == 'B') && (name[0] is (>= 'a' and <= 'g') or (>= 'A' and <= 'G')))
            {
                int natural = Array.IndexOf(Trumpet.Labels, char.ToUpperInvariant(name[0]).ToString());
                return natural < 0 ? -1 : (natural + 11) % 12;
            }

            return Array.IndexOf(Trumpet.Labels, name.ToUpperInvariant());
        }

        /// <summary>
        /// Reshapes a parsed score in place. <paramref name="mode"/> 1 makes it major,
        /// -1 minor, 0 leaves it: the third, sixth and seventh of the key (from the
        /// score's key= and minor/major, C major if unsaid) move a semitone, other
        /// notes stay. Then every note moves <paramref name="semitones"/>, folding back
        /// an octave where the horn runs out.
        /// </summary>
        private static void Reshape(List<List<Note>> phrases, int mode, int semitones)
        {
            bool toMinor = mode < 0 && !ParsedMinor;
            bool toMajor = mode > 0 && ParsedMinor;

            for (int p = 0; p < phrases.Count; p++)
            {
                for (int i = 0; i < phrases[p].Count; i++)
                {
                    Note n = phrases[p][i];

                    if (n.Band < 0)
                        continue;

                    int idx = n.Octave * Trumpet.Bands + n.Band;
                    int degree = ((n.Band - ParsedKey) % 12 + 12) % 12;

                    if (toMinor && (degree == 4 || degree == 9 || degree == 11)) idx--;
                    if (toMajor && (degree == 3 || degree == 8 || degree == 10)) idx++;

                    idx += semitones;

                    int top = Trumpet.Bands * Trumpet.Octaves;
                    while (idx < 0) idx += Trumpet.Bands;
                    while (idx >= top) idx -= Trumpet.Bands;

                    phrases[p][i] = new Note(idx % Trumpet.Bands, idx / Trumpet.Bands, n.Ticks);
                }
            }
        }

        // ---- driving --------------------------------------------------------------------

        /// <summary>
        /// Starts a tune. <paramref name="octaveShift"/> of 1 plays every note an octave up
        /// (the right button), so a low tune can be heard high without a second score.
        /// </summary>
        public static bool Start(string name, int octaveShift, out string error, int mode = 0, int semitones = 0)
        {
            if (!TryGetText(name, out string text))
            {
                error = "no tune called \"" + name + "\"";
                return false;
            }

            return StartText(text, octaveShift, out error, mode, semitones);
        }

        /// <summary>Score text by tune name: built-in, then the mod's Melody folder, then the songbook, then the Tunes folder.</summary>
        public static bool TryGetText(string name, out string text)
            => Scores.TryGetValue(name, out text)
                || MelodyFiles.TryRead(name, out text)
                || TrumpetSongbook.Local().TryGetValue(name, out text)
                || TuneFiles.TryRead(name, out text);

        /// <summary>
        /// Plays score text directly; see the notation above. <paramref name="mode"/> 1
        /// plays it major, -1 minor, 0 as written; <paramref name="semitones"/> moves it.
        /// </summary>
        public static bool StartText(string text, int octaveShift, out string error, int mode = 0, int semitones = 0)
        {
            List<List<Note>> parsed = Parse(text, out error);

            if (parsed == null)
                return false;

            score = parsed;

            if (mode != 0 || semitones != 0)
                Reshape(score, mode, semitones);

            if (octaveShift != 0)
            {
                for (int p = 0; p < score.Count; p++)
                {
                    for (int i = 0; i < score[p].Count; i++)
                    {
                        Note n = score[p][i];
                        int o = Math.Clamp(n.Octave + octaveShift, 0, Trumpet.Octaves - 1);
                        score[p][i] = new Note(n.Band, o, n.Ticks);
                    }
                }
            }

            phrase = 0;
            note = 0;
            left = 0;
            gap = 0;
            Active = true;
            Pressed = false;
            Band = -1;
            mouseX = PlayerInput.MouseX;
            placed = new Point(PlayerInput.MouseX, PlayerInput.MouseY);
            settle = 3;
            return true;
        }

        public static void Stop()
        {
            Active = false;
            Pressed = false;
            Band = -1;
            score = null;
        }

        /// <summary>Called by Trumpet.HoldItem each tick before it reads the buttons.</summary>
        internal static void Tick(Player player)
        {
            if (!Active)
                return;

            // The hand took over: the mouse is not where it was put, or the interface is up.
            Point now = new Point(PlayerInput.MouseX, PlayerInput.MouseY);

            if (settle > 0)
                settle--;
            else if (Math.Abs(now.X - placed.X) > TakeOverPixels || Math.Abs(now.Y - placed.Y) > TakeOverPixels
                || Main.playerInventory)
            {
                Stop();
                return;
            }

            if (gap > 0)
            {
                gap--;
                Pressed = false;
                return;
            }

            if (left <= 0)
            {
                // Next note, or next phrase, or done.
                if (phrase >= score.Count)
                {
                    Stop();
                    return;
                }

                List<Note> current = score[phrase];

                if (note >= current.Count)
                {
                    // Phrase over: lift, breathe.
                    phrase++;
                    note = 0;
                    gap = Restrike * 2;
                    Pressed = false;
                    return;
                }

                Note n = current[note];
                bool sameAgain = note > 0 && current[note - 1].Band == n.Band && current[note - 1].Octave == n.Octave;

                if (sameAgain && Pressed)
                {
                    // Lift for a moment so the repeat strikes, then come back to this note.
                    gap = Restrike;
                    Pressed = false;
                    return;
                }

                left = n.Ticks;
                Band = n.Band;
                Octave = n.Octave;
                Pressed = n.Band >= 0;
                note++;

                if (n.Band >= 0)
                    Place(n.Band);
            }

            left--;
        }

        /// <summary>Puts the real cursor in the middle of a band, so the ruler and the eye agree with the ear.</summary>
        private static void Place(int band)
        {
            int h = Math.Max(PlayerInput.RealScreenHeight, 1);
            float bandH = h / (float)Trumpet.Bands;
            int y = (int)(h - (band + 0.5f) * bandH);

            placed = new Point(mouseX, y);
            settle = 3;
            Mouse.SetPosition(mouseX, y);
        }
    }

    /// <summary>
    /// Tunes as text files, one per tune, in the tModLoader save folder:
    ///
    ///     Documents\My Games\Terraria\tModLoader\PisAndDag\Tunes\animal jazz.txt
    ///
    /// The file is the score notation, line breaks allowed. "/trumpet animal jazz"
    /// finds it by file name. This exists because the chat box trims what you type
    /// to one line's width, so a whole tune can never be typed or pasted in at once;
    /// a file has no such limit and can be edited in Notepad.
    /// </summary>
    public static class TuneFiles
    {
        public static string Folder => Path.Combine(Main.SavePath, "PisAndDag", "Tunes");

        public static IEnumerable<string> Names()
        {
            if (!Directory.Exists(Folder))
                yield break;

            foreach (string f in Directory.GetFiles(Folder, "*.txt"))
                yield return Path.GetFileNameWithoutExtension(f);
        }

        public static bool TryRead(string name, out string text)
        {
            text = null;

            if (!Directory.Exists(Folder))
                return false;

            foreach (string f in Directory.GetFiles(Folder, "*.txt"))
            {
                if (Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    text = File.ReadAllText(f);
                    return true;
                }
            }

            return false;
        }

        public static void Write(string name, string text)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, name + ".txt"), text);
        }
    }

    /// <summary>
    /// Tunes shipped inside the mod: every .txt in Content/Items/Instrument/Melody is
    /// packed into the .tmod and plays by its file name, so "/trumpet saints" finds
    /// Melody/saints.txt. Drop a file in, rebuild, and it is there. Same notation as
    /// TrumpetAutoplay; line breaks allowed. Only tunes we are free to ship go here.
    /// </summary>
    public static class MelodyFiles
    {
        private const string Folder = "Content/Items/Instrument/Melody/";

        private static Mod Mod => ModContent.GetInstance<PisAndDag>();

        public static IEnumerable<string> Names()
        {
            List<string> files = Mod?.GetFileNames();
            if (files == null)
                yield break;

            foreach (string f in files)
            {
                if (f.StartsWith(Folder, StringComparison.OrdinalIgnoreCase)
                    && f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    && f.IndexOf('/', Folder.Length) < 0)
                    yield return Path.GetFileNameWithoutExtension(f);
            }
        }

        public static bool TryRead(string name, out string text)
        {
            text = null;

            foreach (string f in Names())
            {
                if (!f.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                byte[] bytes = Mod.GetFileBytes(Folder + f + ".txt");
                if (bytes == null)
                    return false;

                text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Tunes this character has learned, by name, as score text. Saved with the
    /// character: a songbook is the player's, like the codex. "/trumpet learn" writes
    /// one, "/trumpet forget" removes it, and any name here plays like a built-in.
    /// </summary>
    public class TrumpetSongbook : ModPlayer
    {
        private const string SaveKey = "PADTrumpetSongs";

        public readonly Dictionary<string, string> Songs = new(StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, string> Local()
        {
            Player p = Main.LocalPlayer;
            return p != null && p.active ? p.GetModPlayer<TrumpetSongbook>().Songs : new Dictionary<string, string>();
        }

        public override void SaveData(Terraria.ModLoader.IO.TagCompound tag)
        {
            if (Songs.Count == 0)
                return;

            var names = new List<string>();
            var texts = new List<string>();

            foreach (var kv in Songs)
            {
                names.Add(kv.Key);
                texts.Add(kv.Value);
            }

            tag[SaveKey + "Names"] = names;
            tag[SaveKey + "Texts"] = texts;
        }

        public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
        {
            Songs.Clear();

            if (!tag.ContainsKey(SaveKey + "Names"))
                return;

            var names = tag.GetList<string>(SaveKey + "Names");
            var texts = tag.GetList<string>(SaveKey + "Texts");

            for (int i = 0; i < names.Count && i < texts.Count; i++)
                Songs[names[i]] = texts[i];
        }
    }
     
    public class TrumpetCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "trumpet";

        public override string Usage => "/trumpet [tune [high] [minor|major] [+N|-N] | play <score> | learn <name> [= <score>] | add <part> | save | forget <name> | check <name> | folder | stop]";

        private static string draftName;
        private static readonly StringBuilder draft = new();

        private static bool Fail(string why, out string error)
        {
            error = why;
            return false;
        }

        public override string Description => "Has the trumpet in your hand play a tune.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string name = string.Join(" ", args).Trim();

            if (name.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                TrumpetAutoplay.Stop();
                Trumpet.Silence();
                caller.Reply("Stopped.", Color.Gray);
                return;
            }

            if (name.Length == 0)
            {
                caller.Reply("Tunes: " + string.Join(", ", TrumpetAutoplay.Names)
                    + ". Add high, minor, major, +3 or -2 after a name. Or /trumpet play E E F G | ... "
                    + "(h for high, :2 for beats, ~ rest, | phrase). /trumpet learn NAME = SCORE keeps one.", Color.Gray);
                return;
            }

            // "/trumpet check NAME": count the beats in every bar and point at the odd ones.
            if (name.StartsWith("check ", StringComparison.OrdinalIgnoreCase))
            {
                string title = name[6..].Trim();

                if (!TrumpetAutoplay.TryGetText(title, out string text))
                {
                    caller.Reply("No tune called \"" + title + "\".", Color.OrangeRed);
                    return;
                }

                if (TrumpetAutoplay.Parse(text, out string why) == null)
                {
                    caller.Reply(why + ".", Color.OrangeRed);
                    return;
                }

                var beats = TrumpetAutoplay.PhraseBeats;
                var odd = new StringBuilder();
                int good = 0;

                for (int i = 0; i < beats.Count; i++)
                {
                    if (Math.Abs(beats[i] - 4f) < 0.01f) { good++; continue; }
                    if (odd.Length > 0) odd.Append(", ");
                    odd.Append("bar ").Append(i + 1).Append(" = ").Append(beats[i].ToString("0.##"));
                }

                caller.Reply(beats.Count + " bars, " + good + " of four beats"
                    + (odd.Length > 0 ? ". Not four: " + odd + "." : ". All even.")
                    + " Quarter = 1, eighth = :.5, dotted quarter = :1.5, half = :2, tied notes add up.", Color.Gray);
                return;
            }

            if (name.Equals("folder", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(TuneFiles.Folder);
                caller.Reply("Tune files go in " + TuneFiles.Folder + " as NAME.txt; /trumpet NAME plays one.", Color.Gray);
                return;
            }

            // A draft built up a line at a time, since the chat box trims a long line.
            if (name.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
            {
                if (draftName == null)
                {
                    caller.Reply("No draft. /trumpet learn NAME first.", Color.OrangeRed);
                    return;
                }

                draft.Append(' ').Append(name[4..].Trim());
                caller.Reply("Added. " + draft.Length + " characters so far; /trumpet save when done.", Color.Gray);
                return;
            }

            if (name.Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                if (draftName == null)
                {
                    caller.Reply("No draft. /trumpet learn NAME first.", Color.OrangeRed);
                    return;
                }

                string text = draft.ToString().Trim();

                if (TrumpetAutoplay.Parse(text, out string why) == null)
                {
                    caller.Reply(why + ". The draft is kept; fix with more /trumpet add, or /trumpet learn NAME to start over.", Color.OrangeRed);
                    return;
                }

                TrumpetSongbook.Local()[draftName] = text;
                TuneFiles.Write(draftName, text);
                caller.Reply("Learned \"" + draftName + "\", and wrote it to the Tunes folder. /trumpet " + draftName, new Color(196, 180, 152));
                draftName = null;
                draft.Clear();
                return;
            }

            if (name.StartsWith("learn ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = name[6..];
                int eq = rest.IndexOf('=');

                if (eq <= 0)
                {
                    // No score on the line: open a draft under this name.
                    draftName = rest.Trim();
                    draft.Clear();
                    caller.Reply("Drafting \"" + draftName + "\". /trumpet add <part of the score>, then /trumpet save.", new Color(196, 180, 152));
                    return;
                }

                string title = rest[..eq].Trim();
                string text = rest[(eq + 1)..].Trim();

                if (TrumpetAutoplay.Parse(text, out string why) == null)
                {
                    caller.Reply(why + ".", Color.OrangeRed);
                    return;
                }

                TrumpetSongbook.Local()[title] = text;
                caller.Reply("Learned \"" + title + "\". /trumpet " + title, new Color(196, 180, 152));
                return;
            }

            if (name.StartsWith("forget ", StringComparison.OrdinalIgnoreCase))
            {
                string title = name[7..].Trim();
                caller.Reply(TrumpetSongbook.Local().Remove(title) ? "Forgot \"" + title + "\"." : "Never knew \"" + title + "\".", Color.Gray);
                return;
            }

            if (caller.Player.HeldItem?.ModItem is not Trumpet)
            {
                caller.Reply("Hold the trumpet first.", Color.OrangeRed);
                return;
            }

            bool ok;
            string error;

            if (name.StartsWith("play ", StringComparison.OrdinalIgnoreCase))
            {
                ok = TrumpetAutoplay.StartText(name[5..], 0, out error);
            }
            else
            {
                // Trailing words shape the tune, any order: "high" an octave up, "low"
                // as written, "minor" / "major" the mode, "+3" / "-2" semitones.
                int shift = 0, mode = 0, semis = 0;

                while (name.Length > 0)
                {
                    int sp = name.LastIndexOf(' ');
                    string last = sp < 0 ? name : name[(sp + 1)..];

                    if (last.Equals("high", StringComparison.OrdinalIgnoreCase)) shift = 1;
                    else if (last.Equals("low", StringComparison.OrdinalIgnoreCase)) shift = 0;
                    else if (last.Equals("minor", StringComparison.OrdinalIgnoreCase)) mode = -1;
                    else if (last.Equals("major", StringComparison.OrdinalIgnoreCase)) mode = 1;
                    else if ((last[0] == '+' || last[0] == '-') && int.TryParse(last, out int n)) semis = Math.Clamp(n, -12, 12);
                    else break;

                    if (sp < 0) { name = ""; break; }
                    name = name[..sp].Trim();
                }

                ok = name.Length > 0
                    ? TrumpetAutoplay.Start(name, shift, out error, mode, semis)
                    : Fail("which tune", out error);
            }

            if (!ok)
            {
                caller.Reply(error + ". Tunes: " + string.Join(", ", TrumpetAutoplay.Names), Color.OrangeRed);
                return;
            }

            caller.Reply("Move the mouse to take it back.", new Color(196, 180, 152));
        }
    }
}
