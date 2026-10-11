using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.Social.Steam;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// Going through a door: out of the world into a subworld, or back.
    ///
    /// The screen goes to black with the player held, and with the black, from the first of
    /// it, come a picture and a tip (SubworldLoading); then the world loaded is left the way
    /// the game's own Save and Exit leaves it (the same calls, in the same order, so it and
    /// the player are saved by the game's own code and every system is told the world is
    /// going), the other world's file is made if this is the first time it is gone to
    /// (SubworldKeeper.Make), and the other world is entered the way the game enters one picked
    /// from its list (WorldGen.playWorld). Nothing of the menu is seen meanwhile: the black
    /// stays up with the picture and the tip on it, and a bar that fills as the game's saving
    /// and loading go. Then the player is put down in front of the door on the other side
    /// and the world comes back up.
    ///
    /// The world's flags go through the door too, both ways (SubworldFlags, SubworldKeeper).
    ///
    /// The player is the same player all the way: never read back from their file. What the
    /// game keeps outside a player while one is played it puts away on the way out of a
    /// world, and on the way into one sets from what the player's file held when it was
    /// last read, which here is from before this game began. Two things are kept so: the
    /// items in no inventory (on the cursor, in the Guide's and the Goblin's slots, in
    /// Journey's research slot), and Journey's own powers (godmode and the like). Both are
    /// carried across the door by hand (<see cref="Take"/>, <see cref="Give"/>,
    /// <see cref="KeepPowers"/>), or the items would be gone and the powers put back.
    ///
    /// The world left is never emptied under a save of it. The game's autosave writes the
    /// world, and the player's map of it, on threads of their own, and its Save and Exit
    /// steps round a save in flight rather than wait for it (WorldFile.SaveWorld,
    /// MapHelper.SaveMap); at the menu that is harmless, but here the next world is loaded
    /// at once, into the same tiles and the same map. So the leaving waits for a save in
    /// flight to finish, twice over (<see cref="saveLocks"/>).
    ///
    /// One thing of that save is left out. Each time a player is saved, tModLoader first
    /// zips the player's file and every map they have into its backup of the day, squeezed
    /// as hard as its zip library can: with the map of a great world among them that is
    /// four seconds, each way through a door, with nothing to show for them. The save a
    /// passage leaves a world by goes without it (<see cref="LeaveOutTheBackup"/>). The
    /// player's file, the game's own .bak of it, the maps and the world are written as
    /// ever, and so is tModLoader's backup of the world; the player's backup of the day is
    /// made again by the next save that is not a passage's (the autosave, Save and Exit).
    ///
    /// If the place cannot be made, the world is loaded again and the player stands where
    /// they stood, with a line; client.log has why. If a world will not load at all, the
    /// game's own "load failed" screen is what comes up: the black gets out of its way.
    /// From there the game's "load backup" enters the world with the player as they are,
    /// and what was carried is given them all the same (<see cref="Arrived"/>); a place
    /// with no backup is laid out afresh the next time it is gone to (<see cref="Lost"/>).
    ///
    /// Single player only. On a server the door stays shut.
    /// </summary>
    public class SubworldPassage : ModSystem
    {
        private enum Stage
        {
            None,

            /// <summary>Going to black, the player held, the world still running.</summary>
            Out,

            /// <summary>At black: the game is saving the player and the world left.</summary>
            Saving,

            /// <summary>At black: the other world is being made, if need be, and loaded.</summary>
            Loading,

            /// <summary>There: black a little longer while the place settles, then the world comes up.</summary>
            In,
        }

        /// <summary>Ticks to black; ticks kept black on the other side; ticks back to the world.</summary>
        private const int OutTicks = 24;
        private const int SettleTicks = 15;
        private const int InTicks = 30;

        /// <summary>Frames of full black drawn before the world is left.</summary>
        private const int BlackFrames = 2;

        /// <summary>The loading screen is menu 10. Any other menu up for this many frames while a world should be loading is the game saying it could not.</summary>
        private const int LoadingMenu = 10;
        private const int LostAfter = 6;

        /// <summary>A passage dropped part way lets its black go at this much a tick.</summary>
        private const float LetGo = 1f / 20f;

        private static readonly Color LoreColour = new Color(196, 180, 152);

        private static volatile Stage stage;
        private static int ticks;
        private static int blackFrames;
        private static int lostFrames;

        /// <summary>How much of the screen is black, 0 to 1.</summary>
        private static float cover;

        /// <summary>Where to: a place, or null for the world it belongs to.</summary>
        private static Subworld toward;

        /// <summary>The world: the one being left for a place, or the one being gone back to.</summary>
        private static WorldFileData world;

        /// <summary>The place's file, when going to one.</summary>
        private static string file;

        /// <summary>The door stepped through in the world, its top left tile, when going to a place: (-1, -1) for none.</summary>
        private static Point16 from;

        /// <summary>The door to be put down in front of on the other side, its top left tile: (-1, -1) for none.</summary>
        private static Point16 arrive;

        /// <summary>The place could not be made, and this is the way back from finding that out.</summary>
        private static bool turnedBack;

        /// <summary>
        /// The four items the game keeps outside the player, across the door, and the player
        /// they were taken from. Not a passage's to forget: they are held until that player
        /// next stands in a world, however they get there (<see cref="Arrived"/>).
        /// </summary>
        private static Item[] kept;
        private static Player keptOf;

        /// <summary>
        /// The game's own locks on the writing of a world's file and of the map of it
        /// (WorldFile.IOLock, MapHelper.IOLock): each held by whichever thread is writing.
        /// Private to the game, so found by name; one that is not there is left out, and
        /// then that save is not waited for, as the game itself would not.
        /// </summary>
        private static object[] saveLocks = Array.Empty<object>();

        /// <summary>
        /// tModLoader's backups of the day are all made by one method of its own
        /// (BackupIO.RunArchiving: a player's from Player.SavePlayerFile_Write, by way of
        /// BackupIO.Player.ArchivePlayer; a world's from WorldFile.InternalSaveWorld). The
        /// detour on it (<see cref="LeaveOutTheBackup"/>) is on if this is true, and
        /// <see cref="playerBackups"/> is then the folder the players' backups go to
        /// (BackupIO.Player.PlayerBackupDir), which is how a player's is told from a
        /// world's. All of it is tModLoader's own and none of it public, so it is found by
        /// name (<see cref="HookTheBackup"/>): if any of it is not as it was, there is no
        /// detour, client.log has a line saying so, and a passage's save backs the player up
        /// as any other does.
        /// </summary>
        private static bool backupHeld;
        private static string playerBackups;

        /// <summary>The save this passage left the world by had the player's backup left out of it. Set by the saving thread; for client.log.</summary>
        private static volatile bool backupLeftOut;

        /// <summary>
        /// How a passage went as the main thread saw it, for one line in client.log when it
        /// is over (<see cref="Tell"/>): for each stage the frames drawn in it, the seconds
        /// they took, the longest wait for one, and how long the garbage collector held
        /// every thread meanwhile (the runtime's own count: GC.GetTotalPauseDuration); what
        /// the frame the door was tried on spent putting the picture and the tip up; and
        /// what the frame the world was left on spent leaving it, and then loading the next
        /// passage's picture. The clock runs from the door being tried. tModLoader's log
        /// already has when the game began each step of its saving and loading; nothing in
        /// it says what the screen did meanwhile, and a passage that stutters is otherwise
        /// only to be guessed at.
        /// </summary>
        private static readonly System.Diagnostics.Stopwatch passed = new();
        private static readonly int[] drawn = new int[(int)Stage.In + 1];
        private static readonly double[] spent = new double[(int)Stage.In + 1];
        private static readonly double[] worst = new double[(int)Stage.In + 1];
        private static readonly double[] held = new double[(int)Stage.In + 1];
        private static double lastFrame;
        private static double lastHeld;
        private static double opening;
        private static double leaving;

        /// <summary>Between the first tick of the black and the last of the world's return.</summary>
        public static bool Travelling => stage != Stage.None;

        /// <summary>
        /// The player is not theirs to move: from the door being tried until they can see
        /// where they are on the other side (the first half of the world's return, as
        /// <see cref="PostUpdateEverything"/> has it).
        /// </summary>
        internal static bool Held => stage == Stage.Out || stage == Stage.Saving || stage == Stage.Loading
            || (stage == Stage.In && (ticks - SettleTicks) * 2 < InTicks);

        public override void Load()
        {
            if (Main.dedServ)
                return;

            Main.OnPostDraw += AfterEverything;
            On_Main.UpdateAudio_DecideOnNewMusic += KeepTheMusic;
            SubworldLoading.Load(Mod);

            object world = typeof(WorldFile).GetField("IOLock", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            object map = typeof(MapHelper).GetField("IOLock", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

            if (world == null || map == null)
                Mod.Logger.Warn("Subworlds: a save lock of the game's is not where it was (WorldFile.IOLock, MapHelper.IOLock). A door used while the game is autosaving will not wait for that save.");

            saveLocks = world != null && map != null ? new[] { world, map }
                : world != null ? new[] { world }
                : map != null ? new[] { map }
                : Array.Empty<object>();

            HookTheBackup();
        }

        /// <summary>
        /// Puts the detour on tModLoader's maker of backups (<see cref="backupHeld"/>). On
        /// RunArchiving, and not on BackupIO.Player.ArchivePlayer, which is nearer and is
        /// the player's alone: that one is a few lines, small enough for the runtime to
        /// compile into the method that calls it, and where it has, a detour on it is never
        /// reached. (Tried, with the runtime's settings as tModLoader leaves them, which
        /// are .NET 8's own: a detour put on ArchivePlayer after a player had been saved
        /// 180 times did not run once in the 121 saves after. RunArchiving has a try in it,
        /// which this runtime does not compile into a caller, and a detour put on it as
        /// late, and after 480 saves, ran every time.) tModLoader takes the detour off when
        /// the mod is unloaded.
        /// </summary>
        private void HookTheBackup()
        {
            backupHeld = false;
            playerBackups = null;

            const string Longer = " A door's passage will back the player up as any other save does, and be some seconds the longer for it.";

            try
            {
                const BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                Type backups = typeof(Mod).Assembly.GetType("Terraria.ModLoader.BackupIO");
                MethodInfo run = backups?.GetMethod("RunArchiving", Hidden);
                string folder = backups?.GetNestedType("Player", BindingFlags.Public | BindingFlags.NonPublic)?.GetField("PlayerBackupDir", Hidden)?.GetValue(null) as string;

                if (run == null || string.IsNullOrEmpty(folder) || !TakesWhatItTook(run))
                {
                    Mod.Logger.Warn("Subworlds: tModLoader's backup of a player is not where it was (BackupIO.RunArchiving, BackupIO.Player.PlayerBackupDir)." + Longer);
                    return;
                }

                playerBackups = folder;

                MonoModHooks.Add(run, (Action<Action<Delegate, bool, string, string, string>, Delegate, bool, string, string, string>)LeaveOutTheBackup);

                backupHeld = true;
            }
            catch (Exception e)
            {
                backupHeld = false;
                Mod.Logger.Warn("Subworlds: no detour could be put on tModLoader's backup of a player (BackupIO.RunArchiving)." + Longer, e);
            }
        }

        /// <summary>
        /// RunArchiving as it was when this was written: given what is to write into the zip
        /// (a delegate), whether the save is in the cloud, the folder the zip goes to, the
        /// name to give it, and the path of the file saved; giving nothing back.
        /// </summary>
        private static bool TakesWhatItTook(MethodInfo run)
        {
            ParameterInfo[] takes = run.GetParameters();

            return run.ReturnType == typeof(void) && takes.Length == 5
                && typeof(Delegate).IsAssignableFrom(takes[0].ParameterType)
                && takes[1].ParameterType == typeof(bool)
                && takes[2].ParameterType == typeof(string)
                && takes[3].ParameterType == typeof(string)
                && takes[4].ParameterType == typeof(string);
        }

        /// <summary>
        /// tModLoader is about to make a backup of the day: of a player's file and maps, or
        /// of a world's file. The player's, in the save a passage leaves a world by, is not
        /// made (the notes over this class say why, and what is saved all the same); any
        /// other is made as ever. This runs on whichever thread is saving. The first of
        /// what it is given is tModLoader's own delegate, typed as any delegate: the type it
        /// is declared as names a class of its zip library, which a mod has no call to know.
        /// </summary>
        private static void LeaveOutTheBackup(Action<Delegate, bool, string, string, string> orig, Delegate write, bool isCloudSave, string dir, string name, string path)
        {
            if (stage == Stage.Saving && dir != null && dir == playerBackups)
            {
                backupLeftOut = true;
                return;
            }

            orig(write, isCloudSave, dir, name, path);
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                Main.OnPostDraw -= AfterEverything;

            saveLocks = Array.Empty<object>();
            backupHeld = false;
            playerBackups = null;
            backupLeftOut = false;
            kept = null;
            keptOf = null;
            SubworldLoading.Unload();
            Forget();
        }

        private static void Forget()
        {
            stage = Stage.None;
            passed.Reset();
            ticks = 0;
            blackFrames = 0;
            lostFrames = 0;
            cover = 0f;
            toward = null;
            world = null;
            file = null;
            from = new Point16(-1, -1);
            arrive = new Point16(-1, -1);
            turnedBack = false;
        }

        // ============================================================
        // ASKING
        // ============================================================

        /// <summary>
        /// The door whose top left tile this is has been tried, on this machine. In the world
        /// it leads to its place; in a subworld, back. False if it stays shut.
        /// </summary>
        public static bool Through(int left, int top)
        {
            if (Subworld.Active)
                return Leave(new Vector2(left + 1, top + SubworldDoor.Tall * 0.5f) * 16f);

            return Enter(SubworldDoor.LeadsTo, new Point16(left, top));
        }

        /// <summary>From the world into a place. <paramref name="door"/> is the door gone through, to come back to: (-1, -1) for none (by hand).</summary>
        public static bool Enter(Subworld place, Point16 door)
        {
            if (place == null || !CanGo() || Subworld.Active)
                return false;

            WorldFileData here = Main.ActiveWorldFileData;

            if (here == null)
                return false;

            toward = place;
            world = here;
            file = SubworldKeeper.PathFor(place, here);
            from = door;
            arrive = new Point16(place.Door.X, place.Door.Y);

            Begin(door.X >= 0 ? new Vector2(door.X + 1, door.Y + SubworldDoor.Tall * 0.5f) * 16f : Main.LocalPlayer.Center);
            return true;
        }

        /// <summary>From a subworld back to the world it was come into from.</summary>
        public static bool Leave(Vector2? heardAt = null)
        {
            if (!CanGo() || !Subworld.Active)
                return false;

            if (SubworldKeeper.Home == null)
            {
                Say("Mods.PisAndDag.Subworlds.DoorForgot");
                return false;
            }

            toward = null;
            world = SubworldKeeper.Home;
            file = null;
            from = new Point16(-1, -1);
            arrive = SubworldKeeper.HomeDoor;

            Begin(heardAt ?? Main.LocalPlayer.Center);
            return true;
        }

        private static bool CanGo()
        {
            if (Main.dedServ || stage != Stage.None || Main.gameMenu)
                return false;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead || me.ghost)
                return false;

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                Say("Mods.PisAndDag.Subworlds.DoorNotAlone");
                return false;
            }

            return true;
        }

        private static void Begin(Vector2 heardAt)
        {
            turnedBack = false;
            ticks = 0;
            blackFrames = 0;
            lostFrames = 0;
            stage = Stage.Out;

            Array.Clear(drawn);
            Array.Clear(spent);
            Array.Clear(worst);
            Array.Clear(held);
            lastFrame = 0.0;
            lastHeld = GC.GetTotalPauseDuration().TotalSeconds;
            opening = 0.0;
            leaving = 0.0;
            backupLeftOut = false;
            passed.Restart();

            // The picture and a tip are up from this frame on, with the first of the black.
            SubworldLoading.Open();
            opening = passed.Elapsed.TotalSeconds;

            SoundEngine.PlaySound(SoundID.DoorOpen, heardAt);
        }

        private static void Say(string key)
        {
            Main.NewText(Language.GetTextValue(key), LoreColour);
        }

        // ============================================================
        // WHILE THE WORLD RUNS
        // ============================================================

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            switch (stage)
            {
                case Stage.None:
                    // Let go of part way: the black goes by itself.
                    if (cover > 0f)
                        cover = Math.Max(0f, cover - LetGo);
                    break;

                case Stage.Out:
                {
                    Player me = Main.LocalPlayer;

                    // Whatever killed them was already on them when they tried the door: the dead go nowhere.
                    if (!me.active || me.dead || me.ghost)
                    {
                        Forget(keepCover: true);
                        break;
                    }

                    Cinematic.Hold();

                    ticks++;
                    cover = Smooth(ticks / (float)OutTicks);
                    break;
                }

                case Stage.In:
                {
                    ticks++;

                    if (ticks == 1)
                        SoundEngine.PlaySound(SoundID.DoorClosed, Main.LocalPlayer.Center);

                    if (ticks <= SettleTicks)
                    {
                        cover = 1f;
                        Cinematic.Hold();
                        break;
                    }

                    float back = (ticks - SettleTicks) / (float)InTicks;

                    cover = 1f - Smooth(back);

                    // Theirs again once they can see where they are.
                    if (back < 0.5f)
                        Cinematic.Hold();

                    if (back >= 1f)
                    {
                        bool line = turnedBack;

                        Tell();
                        Forget();

                        if (line)
                            Say("Mods.PisAndDag.Subworlds.DoorWall");
                    }

                    break;
                }
            }
        }

        private static void Forget(bool keepCover)
        {
            float was = cover;

            Forget();

            if (keepCover)
                cover = was;
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // ============================================================
        // AFTER EVERYTHING IS DRAWN
        // ============================================================

        /// <summary>
        /// Once a frame, in a world or at the menu, after all of it is drawn and with nothing
        /// open to draw into: the black goes over the lot, the interface included. And the
        /// world is left from here, as the game's own Save and Exit button leaves it from the
        /// draw: on the main thread, between one frame and the next.
        /// </summary>
        private static void AfterEverything(GameTime gameTime)
        {
            float black = Watch(out bool leave);

            if (black > 0f)
                Draw(black, (float)gameTime.ElapsedGameTime.TotalSeconds);

            Frame();

            if (leave)
                Go();
        }

        /// <summary>
        /// A frame has been drawn: one more for the stage the passage is in, and the wait
        /// since the frame before is that stage's too, with whatever of it the garbage
        /// collector took.
        /// </summary>
        private static void Frame()
        {
            Stage now = stage;

            if (now == Stage.None || !passed.IsRunning)
                return;

            double at = passed.Elapsed.TotalSeconds;
            double waited = at - lastFrame;
            double collecting = GC.GetTotalPauseDuration().TotalSeconds;

            lastFrame = at;
            drawn[(int)now]++;
            spent[(int)now] += waited;
            held[(int)now] += collecting - lastHeld;
            lastHeld = collecting;

            if (waited > worst[(int)now])
                worst[(int)now] = waited;
        }

        /// <summary>The passage went through and is over: how it went, in one line of client.log.</summary>
        private static void Tell()
        {
            if (!passed.IsRunning)
                return;

            passed.Stop();

            try
            {
                ModContent.GetInstance<SubworldPassage>().Mod.Logger.Info(Account());
            }
            catch (Exception)
            {
                // Only a line in a log.
            }
        }

        /// <summary>
        /// The line: where to, how long, and for each stage its frames, their seconds, the
        /// longest wait for one, and how long the garbage collector held the game in it;
        /// what the two frames that do the passage's own work spent on it; and whether
        /// tModLoader's backup of the player was left out of the saving.
        /// </summary>
        internal static string Account()
        {
            return "Subworlds: through the door to " + (toward != null ? toward.FullName : "the world") + " in " + Seconds(passed.Elapsed.TotalSeconds) + " s. "
                + "Frames drawn, the longest wait for one, and the garbage collector's share of the time: going to black, " + Frames(Stage.Out)
                + "; saving, " + Frames(Stage.Saving)
                + "; loading, " + Frames(Stage.Loading)
                + "; coming back up, " + Frames(Stage.In)
                + ". The frame the door was tried on spent " + Milliseconds(opening) + " ms putting the picture and the tip up. "
                + "The frame the world was left on spent " + Milliseconds(leaving) + " ms leaving it, and then "
                + Milliseconds(SubworldLoading.PictureTook) + " ms loading the next picture. "
                + (backupLeftOut ? "tModLoader's backup of the player was left out of the saving."
                    : backupHeld ? "tModLoader's backup of the player was not left out of the saving: the detour for it is on, and was not run."
                    : "tModLoader's backup of the player was made in the saving: there is no detour to leave it out.");
        }

        private static string Frames(Stage of)
        {
            return drawn[(int)of].ToString(CultureInfo.InvariantCulture) + " in " + Seconds(spent[(int)of]) + " s, "
                + Milliseconds(worst[(int)of]) + " ms, " + Milliseconds(held[(int)of]) + " ms";
        }

        private static string Seconds(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Milliseconds(double value) => (value * 1000.0).ToString("0", CultureInfo.InvariantCulture);

        /// <summary>
        /// What this frame makes of the passage: how black the screen is, and whether this is
        /// the frame the world is left on (the black has been drawn in full a few times over
        /// by then). A passage that is no longer this one's to finish is let go of here.
        /// </summary>
        private static float Watch(out bool leave)
        {
            leave = false;

            switch (stage)
            {
                case Stage.Out:
                case Stage.In:
                    // The world was left some other way meanwhile (Save and Exit): not this passage's any more.
                    if (Main.gameMenu)
                    {
                        Forget();
                        return 0f;
                    }

                    break;

                case Stage.Loading:
                    lostFrames = Main.gameMenu && Main.menuMode != LoadingMenu ? lostFrames + 1 : 0;

                    // The game could not load it, and is saying so: let it be seen.
                    if (lostFrames >= LostAfter)
                    {
                        Lost();
                        Forget();
                        return 0f;
                    }

                    break;
            }

            if (stage == Stage.Saving || stage == Stage.Loading)
                return 1f;

            if (stage == Stage.Out && cover >= 1f && ++blackFrames > BlackFrames)
                leave = true;

            return cover;
        }

        /// <summary>
        /// The other side would not load. If it was a place, and the game found no backup of
        /// it to offer (its own word on both: WorldGen.loadFailed, WorldGen.worldBackup),
        /// there is nothing of the place left to keep and no way on from the game's screen but
        /// back to its title: the file is remembered as it is, and the next time the place is
        /// gone to it is laid out afresh rather than failed at again. With a backup, the
        /// game's own "load backup" is the way on, and nothing is done here.
        /// </summary>
        private static void Lost()
        {
            if (toward == null || file == null)
                return;

            if (!WorldGen.loadFailed || WorldGen.loadSuccess || WorldGen.worldBackup || File.Exists(file + ".bak"))
                return;

            SubworldKeeper.Failed(file);

            ModContent.GetInstance<SubworldPassage>().Mod.Logger.Warn(
                $"Subworlds: {toward.FullName} would not load, and there is no backup of it ({file}). It will be laid out afresh the next time it is gone to.");
        }

        /// <summary>
        /// The black, over the whole window, and on it a picture, a tip and a bar
        /// (SubworldLoading): they come with the black, from the first frame of it, stay
        /// while the game saves and loads, and go again while the place settles. Measured
        /// by what is being drawn into and not by Main.screenWidth and screenHeight: at the
        /// menu the game leaves those two divided by the interface's scale
        /// (Main.PreDrawMenu), and a black of that size would stop short of the right and
        /// the bottom at any scale over 100%.
        /// </summary>
        private static void Draw(float black, float seconds)
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Viewport window = Main.graphics.GraphicsDevice.Viewport;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            try
            {
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, window.Width, window.Height), Color.Black * black);

                switch (stage)
                {
                    case Stage.None:
                    case Stage.Out:
                        // As strong as the black: up with it as the world goes down, and,
                        // if the passage is let go of part way, gone with it again.
                        SubworldLoading.Draw(spriteBatch, window, SubworldLoading.Phase.Leaving, black, seconds);
                        break;

                    case Stage.Saving:
                        SubworldLoading.Draw(spriteBatch, window, SubworldLoading.Phase.Saving, 1f, seconds);
                        break;

                    case Stage.Loading:
                        SubworldLoading.Draw(spriteBatch, window, SubworldLoading.Phase.Loading, 1f, seconds);
                        break;

                    case Stage.In:
                        // Gone by the time the world starts back up.
                        if (ticks < SettleTicks)
                            SubworldLoading.Draw(spriteBatch, window, SubworldLoading.Phase.Arrived, 1f - ticks / (float)SettleTicks, seconds);

                        break;
                }
            }
            finally
            {
                spriteBatch.End();
            }
        }

        // ============================================================
        // LEAVING, AND WHAT FOLLOWS IT
        // ============================================================

        /// <summary>
        /// The world is left: what the game's Save and Exit button does (IngameOptions.Draw),
        /// call for call, but for the window it shuts, which is not open, and with what is to
        /// happen next handed to the save as the thing to do when it is done.
        /// </summary>
        private static void Go()
        {
            // The world or its map is being written right now (the autosave): not this
            // frame. The black stays up and the world runs on, and this is asked again on the
            // next one. No save can begin between here and the leaving: the autosave is
            // started from the update, which is this thread's.
            foreach (object saving in saveLocks)
            {
                if (!Monitor.TryEnter(saving))
                    return;

                Monitor.Exit(saving);
            }

            double began = passed.Elapsed.TotalSeconds;
            bool left = false;

            try
            {
                Tidy();

                // The flags go with whoever goes: into a place in memory, and back to the
                // world by the file a place leaves it (which the saving writes again).
                if (toward != null)
                    SubworldKeeper.Remember();
                else
                    SubworldKeeper.HandBack();

                kept = Take();
                keptOf = Main.LocalPlayer;
                KeepPowers(Main.LocalPlayer);

                // The bar is shared between the saving and the loading by the two worlds'
                // sizes, and by whether tModLoader's backup of the player is in the saving.
                SubworldLoading.Begin((long)Main.maxTilesX * Main.maxTilesY,
                    toward != null ? (long)toward.Width * toward.Height : (long)world.WorldSizeX * world.WorldSizeY,
                    !backupHeld);

                stage = Stage.Saving;

                SteamedWraps.StopPlaytimeTracking();
                SystemLoader.PreSaveAndQuit();

                Main.menuMode = LoadingMenu;
                Main.gameMenu = true;

                WorldGen.SaveAndQuit(Saved);

                left = true;
                leaving = passed.Elapsed.TotalSeconds - began;
            }
            catch (Exception e)
            {
                ModContent.GetInstance<SubworldPassage>().Mod.Logger.Error("Subworlds: could not leave the world.", e);

                // Whatever failed did so before the game began to save: the save is the last
                // thing asked for here, and is not begun if the asking fails (WorldGen.SaveAndQuit
                // does nothing but hand it to another thread). So nothing has been saved or let
                // go of, and the player has everything still: back in the world, as it was.
                Main.gameMenu = false;
                kept = null;
                keptOf = null;
                Forget();
            }

            // The saving is another thread's from here, and this one has only to draw what is
            // up already: what the passage after this one will put up is made ready now, its
            // picture loaded, where the wait for it is no one's. Outside the try above, and
            // after it: nothing that goes wrong here is a leaving that failed (and nothing
            // does: it keeps its own faults).
            if (left)
                SubworldLoading.WorldLeft();
        }

        /// <summary>Whatever is open over the game is shut, and the player off any hook or rope: none of it goes through a door.</summary>
        private static void Tidy()
        {
            Player me = Main.LocalPlayer;

            me.RemoveAllGrapplingHooks();
            me.pulley = false;
            me.chest = -1;
            me.sign = -1;

            Main.editSign = false;
            Main.editChest = false;
            Main.mapFullscreen = false;
            Main.ingameOptionsWindow = false;

            if (Main.inFancyUI)
            {
                Main.inFancyUI = false;
                Main.InGameUI.SetState(null);
            }
        }

        /// <summary>
        /// The world and the player are saved and the world let go; this is the game's saving
        /// thread, not the main one, and the game is at its menu. The place's file is made if
        /// it is not there (or is to be made again), and the other world entered.
        /// </summary>
        private static void Saved()
        {
            // The save's last act is to show the title menu. Not for a frame.
            Main.menuMode = LoadingMenu;

            // Any save still being written is waited out before the world's tiles and map are
            // touched: one begun in the instant before the leaving would have been stepped
            // round by the game's own save just now, and be writing still.
            foreach (object saving in saveLocks)
            {
                lock (saving)
                {
                }
            }

            WorldFileData next = world;

            if (toward != null)
            {
                try
                {
                    if (SubworldKeeper.Remake.Contains(toward.FullName) || !SubworldKeeper.Made(file) || SubworldKeeper.FailedAsItIs(file))
                    {
                        SubworldKeeper.Make(toward, world, file);
                        SubworldKeeper.Remake.Remove(toward.FullName);
                    }

                    next = WorldFile.GetAllMetadata(file, false) ?? WorldFileData.FromInvalidWorld(file, false);

                    SubworldKeeper.Home = world;
                    SubworldKeeper.HomeDoor = from;
                }
                catch (Exception e)
                {
                    ModContent.GetInstance<SubworldPassage>().Mod.Logger.Error($"Subworlds: could not make {toward.FullName} at {file}. Going back.", e);

                    // Back where they stood, in the world.
                    next = world;
                    toward = null;
                    arrive = from;
                    turnedBack = true;
                }
            }

            lostFrames = 0;
            stage = Stage.Loading;

            Main.menuMode = LoadingMenu;
            next.SetAsActive();

            WorldGen.playWorld();
        }

        /// <summary>
        /// The local player has been put into a world (Player.Spawn, through the player's own
        /// hook below). What was kept of theirs is given back, whichever world this is and
        /// however it was got to: through the door, or, the other side having failed to load,
        /// by the game's own "load backup", which enters a world with the player as they are
        /// and not as their file has them. A player picked from the game's list is another,
        /// read from their file with what was kept in it: nothing is given to that one.
        ///
        /// Then, if this is the passage's other side, they are stood in front of the door, if
        /// it is there. Any other way into a world, nothing more is this passage's.
        /// </summary>
        internal static void Arrived(Player me)
        {
            if (kept != null && ReferenceEquals(keptOf, me))
                Give(kept);

            kept = null;
            keptOf = null;

            if (stage != Stage.Loading)
            {
                Forget();
                return;
            }

            SubworldLoading.Arrive();

            if (arrive.X >= 0 && SubworldDoor.StandsAt(arrive.X, arrive.Y))
            {
                me.Bottom = new Vector2((arrive.X + SubworldDoor.Wide * 0.5f) * 16f, (arrive.Y + SubworldDoor.Tall) * 16f);
                me.velocity = Vector2.Zero;
                me.fallStart = (int)(me.position.Y / 16f);
                me.fallStart2 = me.fallStart;
                me.oldPosition = me.position;

                Main.screenPosition = me.Center - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            }

            ticks = 0;
            cover = 1f;
            stage = Stage.In;
        }

        // ---- what the game keeps outside the player --------------------------------------------

        /// <summary>
        /// The item on the cursor, the one in Journey's research slot, the Guide's and the
        /// Goblin's: copies, in the order the game itself keeps them (Player.SaveTemporaryItemSlotContents).
        /// Leaving a world empties all four (Player.ClearPlayerTempInfo) after writing them to
        /// the player's file, and entering one fills them from what the player was last read
        /// from a file with (Player.SetPlayerDataToOutOfClassFields), which here is from
        /// before this game began.
        /// </summary>
        private static Item[] Take()
        {
            return new[]
            {
                Main.mouseItem.Clone(),
                Main.CreativeMenu.GetItemByIndex(0).Clone(),
                Main.guideItem.Clone(),
                Main.reforgeItem.Clone(),
            };
        }

        private static void Give(Item[] items)
        {
            if (items == null)
                return;

            Main.mouseItem = items[0];
            Main.CreativeMenu.SetItembyIndex(items[1], 0);
            Main.guideItem = items[2];
            Main.reforgeItem = items[3];
        }

        /// <summary>
        /// Journey's powers that are a player's own (godmode, building reach, their spawn
        /// rate): the game sets them, on the way into a world, to what the player's file
        /// said when it was read (Player.SetPlayerDataToOutOfClassFields), toggling any that
        /// differ. So what they are now is written and read back in memory, by the game's own
        /// writer and reader for them, and the file's word and the truth are the same again.
        /// </summary>
        private static void KeepPowers(Player me)
        {
            try
            {
                using var stream = new MemoryStream();

                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    CreativePowerManager.Instance.SaveToPlayer(me, writer);

                stream.Position = 0;

                using var reader = new BinaryReader(stream, Encoding.UTF8, true);

                CreativePowerManager.Instance.LoadToPlayer(me, reader, Main.curRelease);
            }
            catch (Exception e)
            {
                // The worst of it is the game's own behaviour: the powers as the file last had them.
                ModContent.GetInstance<SubworldPassage>().Mod.Logger.Warn("Subworlds: could not carry Journey's powers through the door.", e);
            }
        }

        // ---- the music ---------------------------------------------------------------------------

        /// <summary>
        /// At its menu the game picks the title's music. Through a door, what was playing
        /// plays on under the black until the other side picks its own.
        /// </summary>
        private static void KeepTheMusic(On_Main.orig_UpdateAudio_DecideOnNewMusic orig, Main self)
        {
            if (Main.gameMenu && (stage == Stage.Saving || stage == Stage.Loading))
                return;

            orig(self);
        }
    }

    /// <summary>The player's half of a passage: nothing opened over the game while it lasts, and the arrival.</summary>
    public class SubworldPassagePlayer : ModPlayer
    {
        public override void SetControls()
        {
            if (Player.whoAmI != Main.myPlayer || !SubworldPassage.Travelling)
                return;

            // These two open things, and an open thing can pause the game.
            Player.controlInv = false;
            Player.controlMap = false;

            if (!SubworldPassage.Held)
                return;

            // What Cinematic.Hold takes (CinematicPlayer), taken here as well: a hold is a
            // tick behind whoever asks for it, and nothing can ask on the other side before
            // the first tick there has been lived. In it a held key would start a step, a
            // held button the use of whatever is in hand.
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override void OnEnterWorld()
        {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ)
                return;

            SubworldPassage.Arrived(Player);

            // What the first door tried here will put on the screen is made ready now, so
            // that the door's own frame has nothing to find or load. (A passage's arrival
            // finds a picture held already: it is loaded as the world is left.) Not with
            // others in the world: there the door stays shut.
            if (Main.netMode == NetmodeID.SinglePlayer)
                SubworldLoading.WorldEntered();
        }
    }
}
