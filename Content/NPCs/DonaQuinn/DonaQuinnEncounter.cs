using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.Pugream;
using PisAndDag.Content.Structures;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// When and where Dona Quinn stops the player, and that she only ever does it once.
    ///
    /// The conditions: the world is the Grand one (it is the only one with a Cleared Ground),
    /// the world has not gone hard, the Eye has been seen off the way EyeFinale does it
    /// (Pugream is owed his shore: PugreamPresence.Stage), and she has not been spared or
    /// finished in this world. Then the first time a player stands on the Cleared Ground,
    /// on their feet, with no other fight on, near enough to her place on it (HER PLACE,
    /// below: the middle of her ground, where it was made flat for her, or the flattest of
    /// the Cleared Ground in a world made before there was any) for her to be put down there
    /// off their screen, she comes.
    ///
    /// If it is night or raining, the world first runs on to eight in the morning, quickly,
    /// the sky going through it, and the rain stops: by tModLoader's own time rate
    /// (ModifyTimeRate), the way the Enchanted Sundial does, so everything that happens at
    /// dawn still happens. The player is held through it with the bars in. Then she is put
    /// down at her place, off their screen, on Triunfo, facing them; the
    /// fight is boxed in round the two of them (<see cref="BoxHalf"/>, BossArena); and the
    /// rest is hers (DonaQuinn).
    ///
    /// Stage is the world's, saved and sent: 0 never met, 1 met, 2 spared, 3 finished. At 2
    /// or more she never comes again. A fight broken off (the player fell, or went) leaves
    /// it at 1 and she can come again, a minute later at the soonest.
    ///
    /// The server (or single player) decides all of it. The player's answers come back to
    /// it as a packet (<see cref="Answer"/>).
    /// </summary>
    public class DonaQuinnEncounter : ModSystem
    {
        public const int NeverMet = 0;
        public const int Met = 1;
        public const int Spared = 2;
        public const int Finished = 3;

        /// <summary>What the player can answer her, as the server hears it.</summary>
        public enum Choice : byte
        {
            /// <summary>Either answer on the road: it is a fight.</summary>
            Fight,

            Spare,
            Finish,

            /// <summary>Spared, she has said her piece and the box is down.</summary>
            Done,

            /// <summary>The walk up to her is over: the player is standing by her, facing her.</summary>
            Arrived,
        }

        /// <summary>Saved with the world. See the class.</summary>
        public static int Stage;

        public static bool Over => Stage >= Spared;

        /// <summary>The player she is coming for, while she is: -1 otherwise. Sent.</summary>
        public static int Target = -1;

        /// <summary>The world is being run on to the morning. Sent, so every machine runs its clock as fast.</summary>
        public static bool Skipping;

        private static double skipRate = 1.0;

        /// <summary>A skip begun in the day has to go through a night before it can stop.</summary>
        private static bool needNight;

        private static bool spawned;
        private static int waited;
        private static int cooldown;

        /// <summary>Eight in the morning: three and a half hours after the day's half past four.</summary>
        private const double EightAM = 3.5 * 3600.0;

        /// <summary>The run to the morning takes about this many ticks, however far it is.</summary>
        private const double SkipTicks = 150.0;

        /// <summary>
        /// Where she is put down when it cannot be her place (nobody has been near it:
        /// /dona; or there is no standing there): this far off the player, on the Wasted Mile's
        /// side; nearer if there is no ground there.
        /// </summary>
        private static readonly float[] SpawnOut = { 1150f, 950f, 1350f, 800f };

        /// <summary>Kept clear of the Cleared Ground's ends this far, in tiles.</summary>
        private const int EdgeMargin = 30;

        /// <summary>
        /// Half of her box, in pixels: the box is always half the Cleared Ground wide, a quarter
        /// of it either side of the point halfway between the player and where she was put down.
        /// </summary>
        public const float BoxHalf = (Layout.ClearedEnd - Layout.ClearedStart) * 16f / 4f;

        /// <summary>After a broken-off fight, this long before she can come again.</summary>
        private const int Cooldown = 3600;

        public override void ClearWorld()
        {
            Stage = NeverMet;
            Target = -1;
            Skipping = false;
            skipRate = 1.0;
            needNight = false;
            spawned = false;
            waited = 0;
            cooldown = 0;

            ForgetSurvey();
            DonaScenePlayer.Forget();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Stage != NeverMet)
                tag["DonaQuinnStage"] = Stage;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Stage = tag.ContainsKey("DonaQuinnStage") ? tag.GetInt("DonaQuinnStage") : NeverMet;
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write((byte)Stage);
            writer.Write((short)Target);
            writer.Write(Skipping);
            writer.Write(skipRate);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Stage = reader.ReadByte();
            Target = reader.ReadInt16();
            Skipping = reader.ReadBoolean();
            skipRate = reader.ReadDouble();
        }

        /// <summary>Tells every client the world's news: the stage, the skip, the target.</summary>
        private static void Sync()
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>She was spared, or finished. Never again in this world.</summary>
        public static void Settle(int stage)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || Stage >= stage)
                return;

            Stage = stage;
            Sync();
        }

        // ============================================================
        // THE MORNING
        // ============================================================

        public override void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate)
        {
            if (Skipping)
                timeRate = skipRate;
        }

        /// <summary>Ticks of clock from now to the next eight in the morning, or to eight today if it is not yet eight.</summary>
        private static double UntilEight()
        {
            if (Main.dayTime)
                return Main.time < EightAM ? EightAM - Main.time : Main.dayLength - Main.time + Main.nightLength + EightAM;

            return Main.nightLength - Main.time + EightAM;
        }

        private static void StartSkip()
        {
            needNight = !(Main.dayTime && Main.time < EightAM);
            skipRate = Math.Max(60.0, UntilEight() / SkipTicks);
            Skipping = true;
        }

        /// <summary>The skip stops at eight, set exactly, once it has been through any night it had to.</summary>
        private static void TickSkip()
        {
            if (!Main.dayTime)
                needNight = false;

            if (Main.raining)
            {
                Main.StopRain();
                Main.SyncRain();
            }

            if (!needNight && Main.dayTime && Main.time >= EightAM)
            {
                Main.time = EightAM;
                Skipping = false;
                skipRate = 1.0;
                Sync();
                return;
            }

            // The clients' skies keep up with the server's.
            if (Main.GameUpdateCount % 10 == 0)
                Sync();
        }

        // ============================================================
        // THE ROAD
        // ============================================================

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Target >= 0)
            {
                Running();
                return;
            }

            if (cooldown > 0)
            {
                cooldown--;
                return;
            }

            if (Over || Main.hardMode || PugreamPresence.Stage < 1 || !ExtraLargeWorld.Active)
                return;

            if (Main.GameUpdateCount % 30 != 0 || AnyBoss() || NPC.AnyNPCs(ModContent.NPCType<DonaQuinn>()))
                return;

            foreach (Player player in Main.ActivePlayers)
            {
                if (OnTheClearedGround(player) && ByTheFlattest(player))
                {
                    Begin(player);
                    return;
                }
            }
        }

        /// <summary>On the Cleared Ground, at the surface, on their feet, alive, dry.</summary>
        private static bool OnTheClearedGround(Player player)
        {
            if (player.dead || player.ghost || player.wet || player.velocity.Y != 0f)
                return false;

            (int left, int right) = Layout.Cleared;
            int x = (int)(player.Center.X / 16f);
            int y = (int)(player.Center.Y / 16f);

            return x >= left + EdgeMargin && x <= right - EdgeMargin && y <= Main.worldSurface;
        }

        private static bool AnyBoss()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.boss)
                    return true;
            }

            return false;
        }

        private static void Begin(Player player)
        {
            Target = player.whoAmI;
            spawned = false;
            waited = 0;

            if (!Main.dayTime || Main.raining)
                StartSkip();

            if (Main.raining)
            {
                Main.StopRain();
                Main.SyncRain();
            }

            Sync();
        }

        /// <summary>The road is on: the morning first, then her; and when she is gone, it is over.</summary>
        private static void Running()
        {
            Player player = Main.player[Target];

            if (Skipping)
            {
                TickSkip();
                return;
            }

            if (!spawned)
            {
                if (!player.active || player.dead)
                {
                    End();
                    return;
                }

                if (Spawn(player))
                {
                    spawned = true;

                    if (Stage < Met)
                        Stage = Met;

                    Sync();
                }
                else if (++waited > 300)
                {
                    // Nowhere for a donkey to stand: not today.
                    End();
                }

                return;
            }

            // Fair weather for the length of it.
            if (Main.raining)
            {
                Main.StopRain();
                Main.SyncRain();
            }

            if (!NPC.AnyNPCs(ModContent.NPCType<DonaQuinn>()))
                End();
        }

        private static void End()
        {
            Target = -1;
            spawned = false;
            Skipping = false;
            skipRate = 1.0;
            cooldown = Cooldown;
            Sync();
        }

        /// <summary>
        /// Her and Triunfo, off the screen, standing, facing the player: at her place if the
        /// player is by it, and otherwise out on the Mile's side of them.
        /// </summary>
        private static bool Spawn(Player player)
        {
            if (FlatSpot(player, out float flatX, out float flatGround))
                return PutDown(player, flatX, flatGround, Math.Sign(flatX - player.Center.X));

            int side = WorldMirror.Dir;

            foreach (float far in SpawnOut)
            {
                foreach (int way in new[] { side, -side })
                {
                    float x = player.Center.X + way * far;

                    if (!Ground(x, player.Bottom.Y, 64, 48, 3, 50, out float ground))
                        continue;

                    return PutDown(player, x, ground, way);
                }
            }

            return false;
        }

        /// <summary>
        /// The two of them made, here, and her box said: its middle halfway between the player
        /// and this, as they stand now. False if the world has no room for them.
        /// </summary>
        private static bool PutDown(Player player, float x, float ground, int way)
        {
            IEntitySource source = new EntitySource_WorldEvent();

            int dona = NPC.NewNPC(source, (int)x, (int)ground, ModContent.NPCType<DonaQuinn>(), 0, 0f, 0f, 0f, player.whoAmI);

            if (dona < 0 || dona >= Main.maxNPCs)
                return false;

            int donkey = NPC.NewNPC(source, (int)x, (int)ground, ModContent.NPCType<TriunfoTheDonkey>(), dona + 1, dona + 1);

            if (donkey < 0 || donkey >= Main.maxNPCs)
            {
                Main.npc[dona].active = false;
                return false;
            }

            NPC her = Main.npc[dona];
            NPC him = Main.npc[donkey];

            her.ai[2] = donkey + 1;
            her.ai[3] = player.whoAmI;
            her.target = player.whoAmI;

            (her.ModNPC as DonaQuinn)?.Box((player.Center.X + x) * 0.5f);

            int toward = Math.Sign(player.Center.X - x);
            him.direction = toward == 0 ? -way : toward;
            him.spriteDirection = him.direction;

            her.netUpdate = true;
            him.netUpdate = true;
            return true;
        }

        /// <summary>
        /// Somewhere something <paramref name="width"/> by <paramref name="height"/> can stand
        /// with its middle at this x, within <paramref name="reach"/> rows of
        /// <paramref name="nearY"/>: the first row down where it fits in the air, dry, with
        /// ground under at least <paramref name="need"/> of the columns under its middle part.
        /// A donkey (64 by 48, three) for where she comes from; a player (two of them, one) for
        /// where the walk up to her starts.
        /// </summary>
        internal static bool Ground(float x, float nearY, int width, int height, int need, int reach, out float ground)
        {
            ground = 0f;

            int from = Math.Max(10, (int)(nearY / 16f) - reach);
            int to = Math.Min(Main.maxTilesY - 10, (int)(nearY / 16f) + reach);
            float half = width * 0.5f;

            if (x < half + 32f || x > Main.maxTilesX * 16f - half - 32f)
                return false;

            for (int j = from; j < to; j++)
            {
                float bottom = j * 16f;
                Vector2 box = new Vector2(x - half, bottom - height);

                if (Collision.SolidCollision(box, width, height))
                    continue;

                // Something under it: under its middle part, the feet, not its ends.
                int under = 0;
                float feet = Math.Max(0f, half - 8f);

                for (int i = (int)((x - feet) / 16f); i <= (int)((x + feet) / 16f); i++)
                {
                    Tile tile = Main.tile[i, j];

                    if (tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        under++;
                }

                if (under >= need && !Collision.WetCollision(box, width, height))
                {
                    ground = bottom;
                    return true;
                }
            }

            return false;
        }

        // ============================================================
        // HER PLACE
        // ============================================================
        //
        // She is put down at her place on the Cleared Ground, and comes when a player is near
        // enough to that for it to be off their screen.
        //
        // In a world made with her ground (PADWorld.CalmGround: ClearedGroundPass shapes the
        // middle of the Cleared Ground for her, and makes the middle of that flat) the place is
        // the middle of it, so that she is met on the flat, and her box, half the Cleared Ground
        // wide, lies on her ground (Layout.CalmHalf is as wide as that takes): the flat, and at
        // its ends what they reach of the edges, which are as vanilla made them, hills, ponds
        // and cracks, because the user asked for those there. In a world made before there was
        // any, it is where the Cleared Ground is flattest. The names below say "flat" for both.
        //
        // Either is looked for once in a session (not saved; the server's), the first time it is
        // wanted, from where that player stands: a walk along the ground they are on, the whole
        // length of the Cleared Ground, up what rises and down what drops, under whatever hangs
        // over it; then a column is asked how far the flat ground runs either side of it. The
        // flattest is the column with the most on its shorter side. The middle is the column
        // nearest the middle of the Cleared Ground that is not the bottom of a hole.
        //
        // Flat ground, from a column: ground with head room over it and no water on it, stepping
        // FlatStep rows at most from one column to the next; and, where the flattest is looked
        // for, within FlatBand rows of where it is at that column (her ground rolls further than
        // that, by design, and is not asked to). A small hole is stepped over, lip to lip: one
        // such as ClearedGroundPass leaves on the flat, no wider than its HoleWide and no
        // deeper than its HoleDeep under the lip it is stepped from, and no more of them than its
        // HolesMost. A living tree in the way is passed (a stump counts; so does one still
        // standing, which a charge then ends in): its columns are not ground and do not end the
        // run, for TreeMost of them.

        /// <summary>She comes when a player is this near her place, and is put down on it if they are this far from it or further; else this far from them, on its side of them.</summary>
        private const float MeetFurthest = 1350f;
        private const float MeetNearest = 1150f;

        /// <summary>A player who was by it when the road began may be this much further off by the time she is put down.</summary>
        private const float MeetSlack = 160f;

        private const int FlatBand = 6;
        private const int FlatStep = 2;

        /// <summary>A living tree's wood this far over the ground, or less, is a tree standing on it.</summary>
        private const int StumpReach = 14;
        private const int TreeMost = 40;

        /// <summary>Rows of air over ground for it to be somewhere to stand.</summary>
        private const int HeadRoom = 3;

        /// <summary>The place is kept this many tiles in from either end: the margin, and the distance she is met from, so that a player coming in at an end meets her on it.</summary>
        private const int FlatEdge = EdgeMargin + (int)(MeetFurthest / 16f);

        /// <summary>The ground a walk starts from is looked for this many rows under a player's feet.</summary>
        private const int SeedReach = 3;

        /// <summary>This many times a player on the Cleared Ground has had nothing under them to start the walk from (half a second apart), and she comes as she did before there was a place: anywhere on it.</summary>
        private const int SurveyTries = 20;

        /// <summary>She is put down within this many tiles of where she was meant to be, the nearest of them that Triunfo can stand on.</summary>
        private const int Nudge = 6;

        private const byte FlatGround = 0;
        private const byte Water = 1;
        private const byte Tree = 2;
        private const byte Low = 3;

        /// <summary>Looked for already: found, or given up on.</summary>
        private static bool surveyed;
        private static int surveyTries;

        /// <summary>Her place: its column in the world, -1 if there is none. Its row is the walk's (<see cref="groundRow"/>).</summary>
        private static int flatColumn = -1;

        /// <summary>How far the flat ground runs from it, toward the Cleared Ground's start and toward the Mile, in tiles.</summary>
        private static int flatBack;
        private static int flatOn;

        /// <summary>The walk: the ground's row and what it is, for every column of the Cleared Ground, counted from its start.</summary>
        private static int[] groundRow;
        private static byte[] groundKind;

        private static int Span => Layout.ClearedEnd - Layout.ClearedStart;

        /// <summary>The world's column this many in from the Cleared Ground's start.</summary>
        private static int Column(int k) => WorldMirror.X(Layout.ClearedStart) + WorldMirror.Dir * k;

        /// <summary>How many columns in from the Cleared Ground's start this pixel is.</summary>
        private static int Along(float x) => ((int)(x / 16f) - WorldMirror.X(Layout.ClearedStart)) * WorldMirror.Dir;

        /// <summary>The middle of her place's column, x in the world, in pixels.</summary>
        private static float FlatX => flatColumn * 16f + 8f;

        private static void ForgetSurvey()
        {
            surveyed = false;
            surveyTries = 0;
            flatColumn = -1;
            flatBack = 0;
            flatOn = 0;
            groundRow = null;
            groundKind = null;
        }

        /// <summary>
        /// Near enough to her place for her to be put down on it off their screen. Not yet, if
        /// it has not been looked for and cannot be from where they stand; anywhere, if there
        /// is no such place.
        /// </summary>
        private static bool ByTheFlattest(Player player)
        {
            if (!Survey(player))
            {
                if (++surveyTries < SurveyTries)
                    return false;

                // Nowhere under anybody to start the walk from, time after time: she is not
                // kept waiting on it.
                ModContent.GetInstance<PisAndDag>().Logger.Info(
                    "Dona Quinn: nothing to start the walk of the Cleared Ground from; she comes anywhere on it.");

                surveyed = true;
            }

            return flatColumn < 0 || Math.Abs(player.Center.X - FlatX) <= MeetFurthest;
        }

        /// <summary>
        /// Where she is put down for a player who is by her place: on it, if that is off
        /// their screen. If they are too near it for that, the same distance she is always put
        /// down at, on its side of them (the Mile's side if they are standing on it), and if
        /// there is no standing there, on their other side. Within a few tiles of that, the
        /// nearest place Triunfo can stand. False if they are not by it, or there is no standing
        /// at any of those.
        /// </summary>
        private static bool FlatSpot(Player player, out float x, out float ground)
        {
            x = 0f;
            ground = 0f;

            if (!Survey(player) || flatColumn < 0)
                return false;

            float to = FlatX - player.Center.X;

            if (Math.Abs(to) > MeetFurthest + MeetSlack)
                return false;

            int toIt = to != 0f ? Math.Sign(to) : WorldMirror.Dir;
            float far = MathHelper.Clamp(Math.Abs(to), MeetNearest, MeetFurthest);

            if (Stand(player.Center.X + toIt * far, out x, out ground))
                return true;

            // Only with the place itself too near them: either side of them is then by it.
            return Math.Abs(to) < MeetNearest && Stand(player.Center.X - toIt * far, out x, out ground);
        }

        /// <summary>
        /// Somewhere for Triunfo within a few tiles of this x, on the walk's ground: with ground
        /// under three of his four columns, as anywhere else she is put down; and if the ground
        /// there steps too often for that, under two of them.
        /// </summary>
        private static bool Stand(float want, out float x, out float ground)
        {
            x = 0f;
            ground = 0f;

            for (int need = 3; need >= 2; need--)
            {
                for (int n = 0; n <= Nudge * 2; n++)
                {
                    // 0, 1, -1, 2, -2, ... tiles off it.
                    float at = want + ((n + 1) / 2) * (n % 2 == 1 ? 16f : -16f);
                    int k = Along(at);

                    if (k < 0 || k > Span || groundKind[k] != FlatGround)
                        continue;

                    if (Ground(at, groundRow[k] * 16f, 64, 48, need, FlatStep + 2, out ground))
                    {
                        x = at;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Her place, looked for if it has not been: from the ground under this
        /// player's feet. True once it has been looked for, whether or not there is one. False
        /// while it cannot be (they are not on the Cleared Ground, or there is nothing under
        /// their feet to start the walk from: a platform, a living tree, a ledge's edge), and
        /// then it is looked for again the next time it is wanted.
        /// </summary>
        private static bool Survey(Player player)
        {
            if (surveyed)
                return true;

            if (!ExtraLargeWorld.Active)
                return false;

            int seed = Along(player.Center.X);

            if (seed < 0 || seed > Span)
                return false;

            int x = Column(seed);
            int feet = (int)((player.Bottom.Y - 8f) / 16f);
            int under = -1;

            for (int y = feet; y <= feet + SeedReach; y++)
            {
                if (IsGround(x, y))
                {
                    under = y;
                    break;
                }
            }

            if (under < 0 || TreeColumn(x, under))
                return false;

            int span = Span;
            int[] row = new int[span + 1];
            byte[] kind = new byte[span + 1];

            int at = Walk(seed, under, row, kind);
            int last = at;

            for (int k = seed + 1; k <= span; k++)
                last = Walk(k, last, row, kind);

            last = at;

            for (int k = seed - 1; k >= 0; k--)
                last = Walk(k, last, row, kind);

            int best = -1, bestShort = -1, bestBoth = -1, back = 0, on = 0;
            bool calm = PADWorld.CalmGround;

            if (calm)
            {
                // The middle of her ground, which is the middle of the Cleared Ground: 0, 1,
                // -1, 2, -2, ... columns off it, the first that is flat ground and has as much
                // of it either side as the widest hole is wide, so is not the bottom of one.
                int middle = span / 2;

                for (int n = 0; n <= span && best < 0; n++)
                {
                    int c = middle + ((n + 1) / 2) * (n % 2 == 1 ? 1 : -1);

                    if (c < FlatEdge || c > span - FlatEdge || kind[c] != FlatGround)
                        continue;

                    int a = Reach(row, kind, c, -1, int.MaxValue);
                    int b = Reach(row, kind, c, 1, int.MaxValue);

                    if (Math.Min(a, b) < ClearedGroundPass.HoleWide)
                        continue;

                    best = c;
                    back = a;
                    on = b;
                }
            }
            else
            {
                for (int c = FlatEdge; c <= span - FlatEdge; c++)
                {
                    if (kind[c] != FlatGround)
                        continue;

                    int a = Reach(row, kind, c, -1, FlatBand);
                    int b = Reach(row, kind, c, 1, FlatBand);
                    int least = Math.Min(a, b);

                    if (least > bestShort || (least == bestShort && a + b > bestBoth))
                    {
                        best = c;
                        bestShort = least;
                        bestBoth = a + b;
                        back = a;
                        on = b;
                    }
                }
            }

            groundRow = row;
            groundKind = kind;
            flatColumn = best < 0 ? -1 : Column(best);
            flatBack = back;
            flatOn = on;
            surveyed = true;

            ModContent.GetInstance<PisAndDag>().Logger.Info(best < 0
                ? "Dona Quinn: no flat ground on the Cleared Ground; she comes anywhere on it."
                : calm
                    ? $"Dona Quinn: her place is the middle of her ground, x {flatColumn} (row {row[best]}): {back} tiles of flat ground toward the Cleared Ground's start, {on} toward the Mile."
                    : $"Dona Quinn: the Cleared Ground is flattest at x {flatColumn} (row {row[best]}): {back} tiles of flat ground toward its start, {on} toward the Mile.");

            return true;
        }

        /// <summary>One column of the walk, from the row of the one before: a living tree is passed at that row; else the ground there, and what it is.</summary>
        private static int Walk(int k, int from, int[] row, byte[] kind)
        {
            int x = Column(k);

            if (TreeColumn(x, from))
            {
                row[k] = from;
                kind[k] = Tree;
                return from;
            }

            int at = Follow(x, from);

            row[k] = at;
            kind[k] = KindAt(x, at);
            return at;
        }

        /// <summary>
        /// The ground in this column for a walker who stood at row <paramref name="from"/> in the
        /// one before: the top of what rises in front of their feet, however high, and otherwise
        /// the first ground at or under their feet, however far down.
        /// </summary>
        private static int Follow(int x, int from)
        {
            int y = from;

            if (IsGround(x, y - 1))
            {
                y--;

                while (y > 10 && IsGround(x, y - 1))
                    y--;

                return y;
            }

            int bottom = Main.maxTilesY - 10;

            while (y < bottom && !IsGround(x, y))
                y++;

            return y;
        }

        /// <summary>What the ground at this row is to stand on: under something low, under water, or ground.</summary>
        private static byte KindAt(int x, int row)
        {
            for (int y = row - HeadRoom; y < row; y++)
            {
                if (IsGround(x, y))
                    return Low;
            }

            for (int y = row - HeadRoom; y < row; y++)
            {
                if (WorldGen.InWorld(x, y) && Main.tile[x, y].LiquidAmount > 0)
                    return Water;
            }

            return FlatGround;
        }

        /// <summary>
        /// How many columns the flat ground runs from this one, this way (-1 toward the Cleared
        /// Ground's start, 1 toward the Mile), keeping within <paramref name="band"/> rows of
        /// where it is at this one.
        /// </summary>
        private static int Reach(int[] row, byte[] kind, int c, int way, int band)
        {
            int span = row.Length - 1;
            int last = row[c];
            int trees = 0;
            int holes = 0;
            int got = 0;

            for (int k = c + way, n = 1; k >= 0 && k <= span; k += way, n++)
            {
                if (kind[k] == Tree)
                {
                    if (++trees > TreeMost)
                        break;

                    continue;
                }

                if (kind[k] != FlatGround)
                    break;

                int step = row[k] - last;

                if (step > FlatStep)
                {
                    // A drop of more than a step: a small hole is stepped over, to its far lip.
                    int lip = holes < ClearedGroundPass.HolesMost ? FarLip(row, kind, k, way, last) : -1;

                    if (lip < 0)
                        break;

                    holes++;
                    n += Math.Abs(lip - k);
                    k = lip;
                }
                else if (step < -FlatStep)
                {
                    // A rise of more than a step: the far side of a small hole that was
                    // walked down into, if the lip behind is as high as this one.
                    if (holes >= ClearedGroundPass.HolesMost || !OutOfHole(row, kind, k, way, c))
                        break;

                    holes++;
                }

                if (Math.Abs(row[k] - row[c]) > band)
                    break;

                trees = 0;
                last = row[k];
                got = n;
            }

            return got;
        }

        /// <summary>
        /// The far lip of a small hole that starts at this column: the first column past it
        /// that is back within a step of the lip it was stepped from, which stands at
        /// <paramref name="lip"/>. -1 if what starts here is no small hole: wider than
        /// ClearedGroundPass.HoleWide, deeper than its HoleDeep under that lip, or with water,
        /// a living tree or a low roof in it.
        /// </summary>
        private static int FarLip(int[] row, byte[] kind, int k, int way, int lip)
        {
            int span = row.Length - 1;

            for (int m = k, wide = 0; m >= 0 && m <= span && wide <= ClearedGroundPass.HoleWide; m += way, wide++)
            {
                if (kind[m] != FlatGround)
                    return -1;

                int under = row[m] - lip;

                if (Math.Abs(under) <= FlatStep)
                    return m;

                if (under < 0 || under > ClearedGroundPass.HoleDeep)
                    return -1;
            }

            return -1;
        }

        /// <summary>
        /// This column stands more than a step over the one walked from: true if that is the
        /// far lip of a small hole, one whose near side was gentle enough to be walked down.
        /// Looked for back the way the walk came, no further than the column it began at: a
        /// lip within a step of this one, no more than ClearedGroundPass.HoleWide columns
        /// behind, and nothing between the two deeper than its HoleDeep under that lip.
        /// </summary>
        private static bool OutOfHole(int[] row, byte[] kind, int k, int way, int c)
        {
            int bottom = row[k];

            for (int m = k - way, wide = 0; (m - c) * way >= 0 && wide <= ClearedGroundPass.HoleWide; m -= way, wide++)
            {
                if (kind[m] != FlatGround)
                    return false;

                if (Math.Abs(row[m] - row[k]) <= FlatStep)
                    return bottom - row[m] <= ClearedGroundPass.HoleDeep;

                if (row[m] < row[k])
                    return false;

                bottom = Math.Max(bottom, row[m]);
            }

            return false;
        }

        /// <summary>A living tree stands in this column at this row: its wood, its leaves or its heartwood, from a stump's height over the row to a step under it.</summary>
        private static bool TreeColumn(int x, int row)
        {
            for (int y = row - StumpReach; y <= row + FlatStep; y++)
            {
                if (!WorldGen.InWorld(x, y))
                    continue;

                Tile tile = Main.tile[x, y];

                if ((tile.HasTile && LivingTree(tile.TileType)) || tile.WallType == WallID.LivingWoodUnsafe || tile.WallType == WallID.LivingWood)
                    return true;
            }

            return false;
        }

        private static bool LivingTree(ushort type)
        {
            return type == TileID.LivingWood || type == TileID.LeafBlock
                || type == TileID.LivingMahogany || type == TileID.LivingMahoganyLeaves;
        }

        /// <summary>Ground: a solid block that is not a platform, not a made thing (a door is solid too) and not a living tree. As ClearedGroundPass has it.</summary>
        private static bool IsGround(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return false;

            Tile tile = Main.tile[x, y];

            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]
                && !Main.tileFrameImportant[tile.TileType] && !LivingTree(tile.TileType);
        }

        // ============================================================
        // BY HAND (/dona, DonaQuinnCommand)
        // ============================================================

        /// <summary>
        /// The road, now, for this player, wherever they are: none of the trigger's checks of
        /// place, progress or once only. Night or rain still runs the world on to the morning
        /// first, as it would. She is put down at her place if they are by it, and off
        /// on the Mile's side of them if not; boxed in either way. Null when it started; why not
        /// when it did not.
        /// </summary>
        internal static string Summon(Player player)
        {
            if (player == null || !player.active || player.dead)
                return "There is nobody standing to stop.";

            if (Target >= 0 || NPC.AnyNPCs(ModContent.NPCType<DonaQuinn>()))
                return "She is already on the road.";

            Begin(player);
            return null;
        }

        /// <summary>
        /// Everything about her in this world back to the start: a road under way ended, her and
        /// Triunfo gone, never met, and her name unheard (her face black and "???" again).
        /// </summary>
        internal static void Reset()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.ModNPC is not DonaQuinn && npc.ModNPC is not TriunfoTheDonkey)
                    continue;

                npc.active = false;
                npc.netSkip = -1;
                npc.life = 0;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }

            Stage = NeverMet;
            Target = -1;
            Skipping = false;
            skipRate = 1.0;
            needNight = false;
            spawned = false;
            waited = 0;
            cooldown = 0;

            ForgetSurvey();

            DialogueIntroductions.Known.Remove(DialogueIntroductions.Key(ModContent.NPCType<DonaQuinn>()));
            DonaScenePlayer.Forget();

            Sync();
        }

        /// <summary>Where she stands in this world, in a line.</summary>
        internal static string Describe()
        {
            string stage = Stage switch
            {
                NeverMet => "never met",
                Met => "met, nothing settled",
                Spared => "spared",
                _ => "finished",
            };

            if (Target >= 0)
                stage += Skipping ? ", the morning coming before she does" : ", on the road now";
            else if (cooldown > 0)
                stage += $", back in {cooldown / 60 + 1} s at the soonest";

            string place = !surveyed ? "not looked for yet"
                : flatColumn < 0 ? "none found"
                : $"x {flatColumn}, {flatBack} tiles of flat ground toward the Cleared Ground's start and {flatOn} toward the Mile";

            if (PADWorld.CalmGround)
            {
                // What the world says was made flat, not what a world made today would have.
                int middle = (Layout.ClearedStart + Layout.ClearedEnd) / 2;

                (int left, int right) = WorldMirror.Span(Layout.CalmStart, Layout.CalmEnd);
                (int flatLeft, int flatRight) = WorldMirror.Span(middle - PADWorld.CalmFlat, middle + PADWorld.CalmFlat);

                return "'Dame' Quilt: " + stage + $". Her ground: x {left}-{right}, made flat from x {flatLeft} to {flatRight}. Her place, the middle of it: " + place + ".";
            }

            return "'Dame' Quilt: " + stage + ". The flattest of the Cleared Ground: " + place
                + ". No ground was made for her in this world: it was made before there was any.";
        }

        // ============================================================
        // ANSWERS
        // ============================================================

        /// <summary>
        /// The player's answer, from their own machine, to wherever the world is decided:
        /// straight to her in single player, by packet from a client.
        /// </summary>
        public static void Answer(NPC npc, Choice choice)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                (npc.ModNPC as DonaQuinn)?.Answered(choice);
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = ModContent.GetInstance<PisAndDag>().GetPacket();
            packet.Write((byte)PADPacket.DonaQuinn);
            packet.Write((byte)choice);
            packet.Write((byte)npc.whoAmI);
            packet.Send();
        }

        /// <summary>On the server: an answer from <paramref name="from"/>, taken only from the player she stopped.</summary>
        internal static void HandlePacket(BinaryReader reader, int from)
        {
            Choice choice = (Choice)reader.ReadByte();
            int index = reader.ReadByte();

            if (Main.netMode != NetmodeID.Server || index < 0 || index >= Main.maxNPCs)
                return;

            NPC npc = Main.npc[index];

            if (npc.active && npc.ModNPC is DonaQuinn dona && dona.TargetIndex == from)
                dona.Answered(choice);
        }
    }

    /// <summary>
    /// The player's side of her scenes: stopped dead when she comes; held, with the bars in,
    /// while the morning comes; turned to face her while a scene is on (FaceToward); and,
    /// when she is down, the walk up to her (Approach).
    ///
    /// THE WALK UP is a cut from play into a scene, done as a fade (Cinematic.Fade): a beat
    /// on her where she fell; the picture goes to black; under the black the player is put
    /// down on her head's side a few steps off, on their feet, facing her, and the camera
    /// goes in close on the two of them with the bars in; the picture comes back up on that,
    /// and the player walks the rest of the way, slowly, on their own, and stops a few
    /// blocks off her (StopAt), facing her, so that the two of them face each other. The
    /// server is told they are there (Choice.Arrived), and she begs.
    ///
    /// From the first moment of it: no weapon, no building, nothing able to touch them, off
    /// any mount and hook, and a walk whatever they have on their feet.
    /// </summary>
    public class DonaScenePlayer : ModPlayer
    {
        // ---- the walk up -----------------------------------------------------------------

        /// <summary>The beat on her, the fade out, the black, the fade in: ticks.</summary>
        private const int WatchTicks = 40;
        private const int FadeOutTicks = 24;
        private const int DarkTicks = 14;
        private const int FadeInTicks = 30;

        /// <summary>The tick of the black the player is moved on: by then the black is on screen.</summary>
        private const int CutAt = 3;

        /// <summary>Put down this far off her middle on her head's side, the nearest of these with ground to stand on.</summary>
        private static readonly float[] PutDownAt = { 176f, 160f, 192f, 144f, 208f, 128f, 112f, 96f };

        /// <summary>Where the walk ends: four blocks off her middle.</summary>
        public const float StopAt = 64f;

        /// <summary>The walk: pixels a tick at most (a run is three), and how quickly it gets up to that.</summary>
        private const float WalkPace = 1.6f;
        private const float WalkAccel = 0.06f;

        /// <summary>The walk lets go this short of the stop: the last of it is them slowing down.</summary>
        private const float Coast = 6f;

        /// <summary>A walk that has not got there in this long has got as far as it is going to.</summary>
        private const int WalkLongest = 360;

        private enum Step
        {
            None,
            Watch,
            FadeOut,
            Dark,
            FadeIn,
            Walk,
            There,
        }

        private static int approachTo = -1;
        private static int approachSide = 1;
        private static int approachAsked;

        private static Step step;
        private static int stepTicks;
        private static float blackness;
        private static bool reported;

        /// <summary>From the cut on: the player has been put down by her, and the shot is the close one.</summary>
        public static bool PastCut { get; private set; }

        /// <summary>The picture is all black right now: anything that has to jump, jumps now.</summary>
        public static bool AllBlack => blackness >= 1f;

        /// <summary>The walk is done: they are standing by her, facing her.</summary>
        public static bool There => step == Step.There;

        /// <summary>The walk up has started and is not done.</summary>
        public static bool Busy => step != Step.None && step != Step.There;

        // ---- facing her ------------------------------------------------------------------

        private static int faceTo = -1;
        private static int faceAsked;

        // ---- the road --------------------------------------------------------------------

        /// <summary>Whether the road was on for this player last tick: its first tick stops them dead.</summary>
        private bool wasStopped;

        private int stuck;

        /// <summary>
        /// The walk up to her, this tick: asked every tick by her scene for as long as it wants
        /// it, and gone when it stops asking. The player ends up on <paramref name="side"/> of
        /// her (the side her head is), facing her.
        /// </summary>
        public static void Approach(NPC npc, int side)
        {
            if (npc.whoAmI != approachTo)
            {
                ResetApproach();
                approachSide = side >= 0 ? 1 : -1;
            }

            approachTo = npc.whoAmI;
            approachAsked = 2;
        }

        /// <summary>This machine's player turns to face her, this tick.</summary>
        public static void FaceToward(NPC npc)
        {
            faceTo = npc.whoAmI;
            faceAsked = 2;
        }

        private static void ResetApproach()
        {
            step = Step.None;
            stepTicks = 0;
            blackness = 0f;
            reported = false;
            PastCut = false;
        }

        /// <summary>A world left mid-scene takes nothing of it into the next one (DonaQuinnEncounter.ClearWorld).</summary>
        internal static void Forget()
        {
            ResetApproach();
            approachTo = -1;
            approachAsked = 0;
            faceTo = -1;
            faceAsked = 0;
        }

        private static NPC Her(int index) => index >= 0 && index < Main.maxNPCs && Main.npc[index].active ? Main.npc[index] : null;

        /// <summary>The walk up has hold of this player.</summary>
        private bool Approaching => Player.whoAmI == Main.myPlayer && step != Step.None && Her(approachTo) != null;

        public override void PreUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            bool stopped = DonaQuinnEncounter.Target == Player.whoAmI;

            // The world stops: so do they.
            if (stopped && !wasStopped)
                Player.velocity.X = 0f;

            wasStopped = stopped;

            // The morning, before she is there: held, bars in.
            if (stopped && DonaQuinnEncounter.Skipping)
            {
                Cinematic.Hold();
                Cinematic.Frame();
            }

            if (faceAsked > 0)
                faceAsked--;

            TickApproach();
        }

        /// <summary>The walk up, a tick on: where it has got to, the fade it asks for, the cut.</summary>
        private void TickApproach()
        {
            NPC her = Her(approachTo);

            if (approachAsked <= 0 || her == null)
            {
                if (step != Step.None)
                    ResetApproach();

                approachTo = -1;
                return;
            }

            approachAsked--;
            stepTicks++;

            switch (step)
            {
                case Step.None:
                    Next(Step.Watch);
                    break;

                case Step.Watch:
                    if (stepTicks >= WatchTicks)
                        Next(Step.FadeOut);
                    break;

                case Step.FadeOut:
                    blackness = Smooth(stepTicks / (float)FadeOutTicks);

                    if (stepTicks >= FadeOutTicks)
                    {
                        blackness = 1f;
                        Next(Step.Dark);
                    }
                    break;

                case Step.Dark:
                    blackness = 1f;

                    if (stepTicks == CutAt)
                        PutDown(her);

                    if (stepTicks >= DarkTicks)
                        Next(Step.FadeIn);
                    break;

                case Step.FadeIn:
                    blackness = 1f - Smooth(stepTicks / (float)FadeInTicks);

                    if (stepTicks >= FadeInTicks)
                    {
                        blackness = 0f;
                        Next(Step.Walk);
                    }
                    break;

                case Step.Walk:
                    if (Arrived(her) || stepTicks >= WalkLongest)
                        Next(Step.There);
                    break;

                case Step.There:
                    if (!reported)
                    {
                        reported = true;
                        DonaQuinnEncounter.Answer(her, DonaQuinnEncounter.Choice.Arrived);
                    }
                    break;
            }

            if (blackness > 0f)
                Cinematic.Fade(blackness);
        }

        private static void Next(Step to)
        {
            step = to;
            stepTicks = 0;
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Within the coast of the stop, or past it, coming from her head's side.</summary>
        private bool Arrived(NPC her)
        {
            float stopX = her.Center.X + approachSide * StopAt;
            return (Player.Center.X - stopX) * approachSide <= Coast;
        }

        /// <summary>
        /// Under the black: the player put down on her head's side, on their feet, facing her,
        /// off any mount, hook or rope, the right way up. The nearest place to the planned
        /// distance that has ground for them near her own height; on her other side if there
        /// is none on that one; and where there is none at all, they stay where they are and
        /// walk from there.
        /// </summary>
        private void PutDown(NPC her)
        {
            PastCut = true;

            int side = approachSide;

            if (!FindFeet(her, side, out Vector2 feet))
            {
                side = -side;

                if (!FindFeet(her, side, out feet))
                    return;
            }

            approachSide = side;

            if (Player.mount.Active)
                Player.mount.Dismount(Player);

            Player.RemoveAllGrapplingHooks();
            Player.pulley = false;
            Player.gravDir = 1f;

            Player.Bottom = feet;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fallStart2 = Player.fallStart;
            Player.direction = -side;

            // Where they are now, for everyone else, without waiting for the next time it goes.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Player.whoAmI);
        }

        /// <summary>A place for the player's feet on this side of her, the nearest of PutDownAt that has one.</summary>
        private bool FindFeet(NPC her, int side, out Vector2 feet)
        {
            foreach (float off in PutDownAt)
            {
                float x = her.Center.X + side * off;

                if (DonaQuinnEncounter.Ground(x, her.Bottom.Y, Player.width, Player.height, 1, 8, out float ground))
                {
                    feet = new Vector2(x, ground);
                    return true;
                }
            }

            feet = Vector2.Zero;
            return false;
        }

        public override void ResetEffects()
        {
            if (!Approaching)
                return;

            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void SetControls()
        {
            if (!Approaching)
            {
                stuck = 0;
                return;
            }

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlThrow = false;
            Player.controlInv = false;

            if (step != Step.Walk)
            {
                stuck = 0;
                return;
            }

            // Toward her, from her head's side.
            if (approachSide < 0)
                Player.controlRight = true;
            else
                Player.controlLeft = true;

            // Something in the way that a step will not take: over it.
            stuck = stepTicks > 12 && Math.Abs(Player.velocity.X) < 0.3f && Player.velocity.Y == 0f ? stuck + 1 : 0;

            if (stuck > 8)
                Player.controlJump = true;
        }

        /// <summary>A walk, whatever is on their feet: vanilla's own run, held to a walking pace.</summary>
        public override void PostUpdateRunSpeeds()
        {
            if (!Approaching || step != Step.Walk)
                return;

            Player.maxRunSpeed = WalkPace;
            Player.accRunSpeed = WalkPace;
            Player.runAcceleration = WalkAccel;
        }

        public override bool CanUseItem(Item item) => !Approaching;

        public override void PostUpdateEquips()
        {
            if (!Approaching)
                return;

            // Nothing may touch them while they are not theirs to move.
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            Player.immuneNoBlink = true;
            Player.immuneAlpha = 0;
        }

        /// <summary>Facing her: asked for by the scene, and all through the walk up but the walking itself.</summary>
        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            NPC her = faceAsked > 0 ? Her(faceTo) : null;

            if (her == null && Approaching && step != Step.Walk)
                her = Her(approachTo);

            if (her == null || Math.Abs(her.Center.X - Player.Center.X) < 4f)
                return;

            Player.direction = Math.Sign(her.Center.X - Player.Center.X);
        }
    }

    /// <summary>Nothing new spawns round a player while she has them stopped, or while she is down.</summary>
    public class DonaQuinnQuiet : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            bool quiet = DonaQuinnEncounter.Target == player.whoAmI && DonaQuinnEncounter.Skipping;

            if (!quiet)
            {
                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (npc.ModNPC is DonaQuinn dona && dona.Quiets(player))
                    {
                        quiet = true;
                        break;
                    }
                }
            }

            if (!quiet)
                return;

            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }
    }
}
