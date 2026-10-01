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
    /// on their feet, with no other fight on, she comes.
    ///
    /// If it is night or raining, the world first runs on to eight in the morning, quickly,
    /// the sky going through it, and the rain stops: by tModLoader's own time rate
    /// (ModifyTimeRate), the way the Enchanted Sundial does, so everything that happens at
    /// dawn still happens. The player is held through it with the bars in. Then she is put
    /// down off the screen on the Wasted Mile's side, on Triunfo, facing them, and the rest
    /// is hers (DonaQuinn).
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

        /// <summary>Where she is put down: this far off the player, on the Wasted Mile's side; nearer if there is no ground there.</summary>
        private static readonly float[] SpawnOut = { 1150f, 950f, 1350f, 800f };

        /// <summary>Kept clear of the Cleared Ground's ends this far, in tiles.</summary>
        private const int EdgeMargin = 30;

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
                if (OnTheClearedGround(player))
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

        /// <summary>Her and Triunfo, off the screen on the Mile's side, standing, facing the player.</summary>
        private static bool Spawn(Player player)
        {
            int side = WorldMirror.Dir;

            foreach (float far in SpawnOut)
            {
                foreach (int way in new[] { side, -side })
                {
                    float x = player.Center.X + way * far;

                    if (!Ground(x, player.Bottom.Y, 64, 48, 3, 50, out float ground))
                        continue;

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

                    int toward = Math.Sign(player.Center.X - x);
                    him.direction = toward == 0 ? -way : toward;
                    him.spriteDirection = him.direction;

                    her.netUpdate = true;
                    him.netUpdate = true;
                    return true;
                }
            }

            return false;
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
        // BY HAND (/dona, DonaQuinnCommand)
        // ============================================================

        /// <summary>
        /// The road, now, for this player, wherever they are: none of the trigger's checks of
        /// place, progress or once only. Night or rain still runs the world on to the morning
        /// first, as it would. Null when it started; why not when it did not.
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

            return "'Dame' Quilt: " + stage + ".";
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
