using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// Which place this is, and where its file is kept.
    ///
    /// A subworld (Subworld) is a world file like any other, read and written by the game's
    /// own code, with one line of the mod's in it: the name of the place it is. That line is
    /// what makes a world a subworld (<see cref="Current"/>), read when the world is loaded
    /// and written whenever it is saved; a world without it is a world.
    ///
    /// The file is made the first time somebody goes there (<see cref="Make"/>) and kept
    /// from then on, beside the world it belongs to and out of the game's list of worlds
    /// (the list is read from the top of the Worlds folder only):
    ///
    ///   Worlds/PisAndDag_Subworlds/[the world's own id]/[the place].wld
    ///
    /// so every world has its own of each place, and deleting that folder puts the world's
    /// places back as they were first laid out. The game's daily backups keep them like any
    /// world. A place whose layout has been changed since (Subworld.Layout, 2 and up) has
    /// the number in its file's name, [the place].2.wld: a new file, so the place is made
    /// the new way the next time it is gone to, and the old file is left beside it.
    ///
    /// While somebody is in one, the world it belongs to is remembered here, and the door
    /// they came through (<see cref="Home"/>, <see cref="HomeDoor"/>): in memory only. A
    /// game left from inside a subworld comes back in the world, at its own spawn.
    ///
    /// A place has its world's flags, and what is done in one is done in the world
    /// (SubworldFlags, which says what they are). Into a place they go in memory, with
    /// whoever goes (<see cref="Remember"/>). Back, they go by a file beside the place's,
    /// written whenever the place is saved (<see cref="FlagsFile"/>) and read whenever the
    /// world is loaded, so that a game left from inside a place loses nothing either.
    ///
    /// Single player only, for now: nothing here is sent to anybody.
    /// </summary>
    public class SubworldKeeper : ModSystem
    {
        /// <summary>The line in a subworld's file that says which place it is.</summary>
        private const string SaveKey = "PADSubworld";

        private const string Folder = "PisAndDag_Subworlds";

        /// <summary>
        /// Beside a world's places: the flags as they were in whichever place was saved
        /// last, for the world to take up when it is next loaded. Each writing of it has a
        /// mark of its own, and the world keeps the mark of the last one it took
        /// (<see cref="TakenKey"/>, in the world's own file): the same writing is not taken
        /// twice, and one the world took and then was not saved with is taken again.
        /// </summary>
        private const string FlagsFile = "Flags.dat";
        private const string MarkKey = "PADMark";
        private const string TakenKey = "PADPlaceFlagsTaken";

        /// <summary>
        /// The smallest a place can be, each way. The game takes two hundred off a world's
        /// width, and off its height, for where a Teleportation Potion may put whoever
        /// drinks one (Player.TeleportationPotion, 1.4.4: maxTilesX less 200 across, and
        /// Main.UnderworldLayer, which is maxTilesY less 200, down), and gives what is left
        /// to its random as a range: less than nothing there, and it throws.
        /// </summary>
        private const int Least = 200;

        /// <summary>Rows from a subworld's bottom edge to its surface line, and to its rock line: both under anything that can be seen.</summary>
        private const int SurfaceFromBottom = 35;
        private const int RockFromBottom = 28;

        /// <summary>The game's own mark on a world made by this version of it (WorldFile.CreateMetadata).</summary>
        private const ulong GeneratorVersion = 1198295875585UL;

        private static readonly Dictionary<string, Subworld> known = new();

        /// <summary>The place the loaded world is, or null in a world.</summary>
        public static Subworld Current { get; private set; }

        public static bool Active => Current != null;

        /// <summary>While in a subworld: the world it was come into from, to go back to. Null if it was come into any other way.</summary>
        internal static WorldFileData Home;

        /// <summary>And the door stepped through there, its top left tile: (-1, -1) for none.</summary>
        internal static Point16 HomeDoor = new Point16(-1, -1);

        /// <summary>
        /// The flags of the world that was left, as they stood when it was (SubworldFlags):
        /// for the place gone to (<see cref="Remember"/>), until it has them.
        /// </summary>
        private static TagCompound carried;

        /// <summary>
        /// In a place: its flags are its world's (it was come into from its world, and
        /// given them). Only then are they written out for the world to take back: a place
        /// that was not given them (its file as it is first made, or one come into some
        /// other way than from its world) has nothing of the world's to hand it.
        /// </summary>
        private static bool given;

        /// <summary>In a world: the mark of the last writing of its places' flags it took.</summary>
        private static string taken;

        /// <summary>Set by hand (/subworld rules): the rules of subworlds are off until the game is closed, for building in one.</summary>
        internal static bool RulesLifted;

        /// <summary>Places to lay out afresh the next time they are gone to (/subworld remake).</summary>
        internal static readonly HashSet<string> Remake = new();

        /// <summary>
        /// Files of places the game could not load, with no backup beside them for it to
        /// offer: each as it was when it failed (<see cref="Failed"/>). The same file is not
        /// handed to the game twice: it would fail the same way, and the door would lead
        /// nowhere but the game's "load failed" for good. A file changed since, by hand or by
        /// anything else, is tried like any other.
        /// </summary>
        private static readonly Dictionary<string, string> failed = new();

        internal static void Add(Subworld place)
        {
            known[place.FullName] = place;
        }

        /// <summary>The place of this name ("PisAndDag/EmptyHall"), or null.</summary>
        public static Subworld Find(string fullName)
        {
            return fullName != null && known.TryGetValue(fullName, out Subworld place) ? place : null;
        }

        public static IEnumerable<Subworld> All => known.Values;

        public override void Unload()
        {
            known.Clear();
            Remake.Clear();

            lock (failed)
                failed.Clear();

            Current = null;
            RulesLifted = false;
            given = false;
            taken = null;
            ForgetHome();
        }

        private static void ForgetHome()
        {
            Home = null;
            HomeDoor = new Point16(-1, -1);
            carried = null;
        }

        /// <summary>The world is about to be left for a place: its flags, kept for there. Asked in the world, before it is saved.</summary>
        internal static void Remember()
        {
            carried = SubworldFlags.Take();
        }

        // ---- which place this is ---------------------------------------------------------------

        /// <summary>Run whenever a world is about to be loaded or made: none, until its file says otherwise.</summary>
        public override void ClearWorld()
        {
            Current = null;
            given = false;
            taken = null;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Current != null)
            {
                tag[SaveKey] = Current.FullName;

                // Whenever a place is saved, its flags are left for its world.
                HandBack();
            }
            else if (taken != null)
            {
                tag[TakenKey] = taken;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Current = tag.ContainsKey(SaveKey) ? Find(tag.GetString(SaveKey)) : null;
            taken = tag.ContainsKey(TakenKey) ? tag.GetString(TakenKey) : null;
        }

        /// <summary>
        /// After every system has read the world's file. A subworld come into from a world
        /// has that world's flags, in place of its own file's. A world takes up what its
        /// places left for it, and has nothing behind it to go back to.
        /// </summary>
        public override void PostWorldLoad()
        {
            if (Current == null)
            {
                ForgetHome();
                TakeBack();
                return;
            }

            if (Home == null)
                return;

            // Given once, as the place is first come into. Loaded again from inside (the
            // game's own "load backup"), the place's file has them already, with whatever
            // has been done here since: those are not put back to what the world had.
            if (carried != null)
            {
                SubworldFlags.Give(carried, true);
                carried = null;
            }

            given = true;
        }

        // ---- what a place hands back to its world ----------------------------------------------

        /// <summary>The folder a world keeps its places in. Asked while the world is the one loaded.</summary>
        private static string FolderFor(WorldFileData world)
        {
            string id = world.UseGuidAsMapName ? world.UniqueId.ToString() : Main.worldID.ToString();

            return Path.Combine(Main.WorldPath, Folder, id);
        }

        /// <summary>
        /// In a place that has its world's flags: they are written beside its file as they
        /// stand, under a mark of their own, whole or not at all (to another name first,
        /// then put in the file's place). Asked whenever the place is saved, and once more
        /// as it is left by its door. Whatever goes wrong is the place's loss of this one
        /// writing and no more: client.log has it.
        /// </summary>
        internal static void HandBack()
        {
            if (Current == null || !given)
                return;

            try
            {
                string folder = Path.GetDirectoryName(Main.ActiveWorldFileData?.Path);

                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    return;

                TagCompound flags = SubworldFlags.Take();

                flags[MarkKey] = Guid.NewGuid().ToString("N");

                string path = Path.Combine(folder, FlagsFile);
                string fresh = path + ".new";

                lock (handing)
                {
                    TagIO.ToFile(flags, fresh, true);
                    File.Move(fresh, path, true);
                }
            }
            catch (Exception e)
            {
                ModContent.GetInstance<SubworldKeeper>().Mod.Logger.Warn("Subworlds: could not leave the place's flags for its world.", e);
            }
        }

        private static readonly object handing = new();

        /// <summary>
        /// In a world just loaded: what its places left for it, if it has not had it, is
        /// taken up (SubworldFlags.Give, which only ever adds to a world). The mark of it is
        /// saved with the world from then on.
        /// </summary>
        private static void TakeBack()
        {
            try
            {
                WorldFileData world = Main.ActiveWorldFileData;

                if (world == null)
                    return;

                string path = Path.Combine(FolderFor(world), FlagsFile);

                if (!File.Exists(path))
                    return;

                TagCompound flags;

                lock (handing)
                    flags = TagIO.FromFile(path, true);

                string mark = flags.ContainsKey(MarkKey) ? flags.GetString(MarkKey) : null;

                if (string.IsNullOrEmpty(mark) || mark == taken)
                    return;

                SubworldFlags.Give(flags, false);
                taken = mark;
            }
            catch (Exception e)
            {
                ModContent.GetInstance<SubworldKeeper>().Mod.Logger.Warn("Subworlds: could not read what the world's places left for it.", e);
            }
        }

        // ---- its file --------------------------------------------------------------------------

        /// <summary>
        /// Where this world keeps this place. By the world's own id, as the game names its map
        /// files (a world from before worlds had one goes by its number). Asked while the world
        /// is the one loaded.
        /// </summary>
        internal static string PathFor(Subworld place, WorldFileData world)
        {
            string name = place.Layout > 1 ? place.Name + "." + place.Layout : place.Name;

            return Path.Combine(FolderFor(world), name + ".wld");
        }

        /// <summary>
        /// The place's file is there, whole: the game's half (.wld) and tModLoader's (.twld),
        /// which is written last and has the line that makes it a subworld. One without the
        /// other is a making that was cut short, and is made again over what is there.
        /// </summary>
        internal static bool Made(string path)
        {
            return File.Exists(path) && File.Exists(Path.ChangeExtension(path, ".twld"));
        }

        /// <summary>The game could not load this place's file, and had no backup of it: remembered as it is now.</summary>
        internal static void Failed(string path)
        {
            lock (failed)
                failed[path] = Stamp(path);
        }

        /// <summary>
        /// This file has already failed to load, and is the same file still: there is nothing
        /// of the place to keep, and it is made again over what is there (the game keeps the
        /// file that failed beside the new one, as .bak).
        /// </summary>
        internal static bool FailedAsItIs(string path)
        {
            lock (failed)
                return failed.TryGetValue(path, out string was) && was == Stamp(path);
        }

        /// <summary>Both halves of a place's file, by size and by when each was last written.</summary>
        private static string Stamp(string path)
        {
            var world = new FileInfo(path);
            var mods = new FileInfo(Path.ChangeExtension(path, ".twld"));

            return (world.Exists ? world.Length + " " + world.LastWriteTimeUtc.Ticks : "none")
                + " | " + (mods.Exists ? mods.Length + " " + mods.LastWriteTimeUtc.Ticks : "none");
        }

        /// <summary>
        /// Makes a place's file: a world of its size, cleared, laid out by the place itself,
        /// and saved, the way the game makes a world (WorldGen.do_worldGenCallBack: clear,
        /// generate, save) with the place's <see cref="Subworld.Build"/> for the generating.
        /// Nothing is loaded when this runs (the world has been saved and let go), and it is
        /// not the main thread's. Whatever it leaves in memory, the load that follows replaces.
        ///
        /// The file is the world's in everything but size: its name, its difficulty. Its
        /// number and id are its own, so beds and maps there are its own.
        /// </summary>
        internal static void Make(Subworld place, WorldFileData world, string path)
        {
            if (place.Width < Least || place.Height < Least || place.Width >= Main.tile.Width || place.Height >= Main.tile.Height)
                throw new InvalidOperationException($"{place.FullName} is {place.Width} by {place.Height}: a subworld is at least {Least} each way, and no bigger than the game has room for.");

            var data = new WorldFileData(path, false)
            {
                Name = world.Name,
                GameMode = world.GameMode,
                CreationTime = DateTime.Now,
                Metadata = FileMetadata.FromCurrentSettings(FileType.World),
                WorldGeneratorVersion = GeneratorVersion,
                UniqueId = Guid.NewGuid(),
            };

            data.SetSeed(world.SeedText);
            data.SetWorldSize(place.Width, place.Height);

            Main.ActiveWorldFileData = data;
            Main.WorldFileMetadata = data.Metadata;

            Main.maxTilesX = place.Width;
            Main.maxTilesY = place.Height;

            // Sizes the world to those two, empties it, and tells every system so.
            WorldGen.clearWorld();

            Main.worldName = data.Name;
            Main.worldID = Guid.NewGuid().GetHashCode() & int.MaxValue;

            // The surface line under everything that can be seen: the game lights whatever is
            // over it as open air under the sky, and draws no cave behind it.
            Main.worldSurface = place.Height - SurfaceFromBottom;
            Main.rockLayer = place.Height - RockFromBottom;

            Point spawn = place.Spawn;

            Main.spawnTileX = spawn.X;
            Main.spawnTileY = spawn.Y;

            // A still sky: what the last world had is otherwise what is written.
            Main.windSpeedTarget = 0f;
            Main.windSpeedCurrent = 0f;
            Main.numClouds = 40;
            Main.cloudBGActive = 0f;

            // The line that makes it a subworld goes into the file with it.
            Current = place;

            bool was = WorldGen.gen;

            WorldGen.gen = true;

            try
            {
                place.Build();
            }
            finally
            {
                WorldGen.gen = was;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Day, a quarter past eight, nothing going on: where every new world starts.
            WorldFile.SaveWorld(false, true);

            if (!Made(path))
                throw new IOException($"The game did not write {path}, or not all of it.");

            lock (failed)
                failed.Remove(path);
        }
    }
}
