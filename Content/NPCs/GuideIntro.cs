using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items.LoreItem;
using PisAndDag.Content.Items.Mage.Seals;
using PisAndDag.Content.NPCs.DonaQuinn;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Structures;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Guide's first words, once, in a world made fresh.
    ///
    /// The first time anybody walks in a world that was generated with this in the mod, ten
    /// seconds run (<see cref="CountTicks"/>), and nothing spawns round them meanwhile (the
    /// Stillness buff, and <see cref="GuideIntroQuiet"/> where spawns are decided). Then,
    /// once they are on their feet, their screen fades to black; under the black the Guide
    /// is put down a little way off on ground he can walk (on the side he was on), the
    /// picture comes back up on the two of them, and he walks up, stops a few blocks off
    /// (<see cref="StopAt"/>) and faces them. Then he talks: a locked scene on the Dialogue
    /// panel, no way out but through. He gives them the Piece of Paper, asks what they
    /// specialise in, and gives them the kit for the answer (GuideIntroPlayer.Kit). Then the
    /// bars go, the camera comes home and the world is theirs.
    ///
    /// World state, saved: <see cref="Pending"/>, true from generation until the scene is
    /// finished, so a world from before this, or a world that has had it, never plays it. A
    /// scene broken off (the player left, fell, or the Guide went) leaves it owed, and it
    /// starts over the next time somebody walks.
    ///
    /// The server (or single player) decides: who walked, the count, where the Guide is put
    /// down, when he is there, and that it is done. Everything that is seen is the player's
    /// own machine's (<see cref="GuideIntroPlayer"/>). The Guide's walk is his own AI on every
    /// machine while the scene has him (<see cref="GuideIntroGuide"/>).
    /// </summary>
    public class GuideIntro : ModSystem
    {
        /// <summary>Where the scene has got to, as the world knows it.</summary>
        public enum Beat : byte
        {
            /// <summary>Nothing under way: owed, and waiting for somebody to walk.</summary>
            None,

            /// <summary>Somebody walked: the ten seconds run, and the world is still round them.</summary>
            Counting,

            /// <summary>The ten seconds are up: their screen fades as soon as they are on their feet.</summary>
            Ready,

            /// <summary>Under their black, the Guide has been put down near them, and walks up.</summary>
            Coming,

            /// <summary>He is there, facing them, and the talk is on their screen.</summary>
            Arrived,
        }

        /// <summary>What the player's machine tells the world.</summary>
        public enum Message : byte
        {
            /// <summary>Their screen is black: put him down.</summary>
            Dark,

            /// <summary>They have answered him: the scene is over, for good.</summary>
            Done,

            /// <summary>The scene came apart on their screen: it is owed again.</summary>
            Abort,
        }

        private const string SaveKey = "PADGuideIntro";

        /// <summary>Saved. True from the world's generation until the scene has been through to the end.</summary>
        public static bool Pending;

        /// <summary>Where it has got to. Sent.</summary>
        public static Beat Now;

        /// <summary>The player it is for, while one is: -1 otherwise. Sent.</summary>
        public static int Target = -1;

        /// <summary>The Guide it has, from when he is put down: -1 otherwise. Sent.</summary>
        public static int Guide = -1;

        /// <summary>
        /// The sea is at the world's east (right) end, for the Piece of Paper. Read where the
        /// world is decided, from what the world already keeps (<see cref="FindSeaEast"/>). Sent.
        /// </summary>
        public static bool SeaEast;

        private static int ticks;

        /// <summary>Ten seconds from the first walk to the fade.</summary>
        public const int CountTicks = 600;

        /// <summary>A player who does not come to their feet (GuideIntroPlayer.Standing) in this long after the count is not waited for: it is owed again.</summary>
        private const int ReadyLongest = 1200;

        /// <summary>A walk that has not got there in this long has got as far as it is going to.</summary>
        private const int WalkLongest = 600;

        /// <summary>Where he stops: three and a half blocks between their middles.</summary>
        public const float StopAt = 56f;

        /// <summary>Within this much of the stop, and still, he is there.</summary>
        private const float ThereSlack = 4f;

        /// <summary>How far off he is put down, in tiles between their middles: the first of these he can walk from.</summary>
        private static readonly float[] ComeFrom = { 15f, 13f, 17f, 11f, 19f, 9f, 7f };

        /// <summary>Rows above and below the player's feet that ground for him is looked for in.</summary>
        private const int GroundReach = 10;

        /// <summary>A walk is a step up of one tile, or a drop of up to four, from one column to the next.</summary>
        private const int StepUpRows = 1;
        private const int StepDownRows = 4;

        public override void ClearWorld()
        {
            Pending = false;
            Now = Beat.None;
            Target = -1;
            Guide = -1;
            SeaEast = false;
            ticks = 0;

            GuideIntroPlayer.Forget();
        }

        /// <summary>A world made now owes the scene.</summary>
        public override void PostWorldGen()
        {
            Pending = true;
            SeaEast = FindSeaEast();
        }

        /// <summary>
        /// After every system's LoadWorldData (WorldFile.LoadWorld: OnWorldLoad, then WorldIO.Load,
        /// then PostWorldLoad), so WorldMirror has its saved side by now.
        /// </summary>
        public override void PostWorldLoad()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                SeaEast = FindSeaEast();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            // Presence is the value: a world without the key has had it, or is from before it.
            if (Pending)
                tag[SaveKey] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Pending = tag.ContainsKey(SaveKey);
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Pending);
            writer.Write((byte)Now);
            writer.Write((short)Target);
            writer.Write((short)Guide);
            writer.Write(SeaEast);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Pending = reader.ReadBoolean();
            Now = (Beat)reader.ReadByte();
            Target = reader.ReadInt16();
            Guide = reader.ReadInt16();
            SeaEast = reader.ReadBoolean();
        }

        /// <summary>
        /// Which end the sea is at. In a Grand world the plan's own (WorldMirror: the ocean end).
        /// In any other, the Dungeon's end: vanilla puts the Jungle across the world from the
        /// Dungeon every time (WorldGen's Reset pass sets jungleOriginX by dungeonSide), and both
        /// ends of such a world are sea. Good during generation from the Dungeon pass on (the
        /// Blood Shrine is built on this side: BloodShrinePass), and in play.
        /// </summary>
        internal static bool FindSeaEast()
        {
            if (ExtraLargeWorld.Active)
                return WorldMirror.Flipped;

            return Main.dungeonX >= Main.maxTilesX / 2;
        }

        /// <summary>Tells every client where it has got to.</summary>
        private static void Sync()
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        // ============================================================
        // THE WORLD'S SIDE
        // ============================================================

        /// <summary>The scene has this player: the count, or the scene itself. Nothing spawns round them.</summary>
        public static bool Keeps(Player player) => Pending && Now != Beat.None && Target == player.whoAmI;

        /// <summary>The Guide the scene has, if he is still there.</summary>
        public static NPC TheGuide()
        {
            if (Guide < 0 || Guide >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[Guide];
            return npc.active && npc.type == NPCID.Guide ? npc : null;
        }

        /// <summary>The scene has this Guide: put down, walking up, or standing there talking.</summary>
        public static bool Holds(NPC npc)
        {
            if (!Pending || (Now != Beat.Coming && Now != Beat.Arrived) || npc.whoAmI != Guide || Target < 0 || Target >= Main.maxPlayers)
                return false;

            return Main.player[Target].active;
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !Pending)
                return;

            // Owed in the world, it is owed in the world's other places too (a subworld has
            // the world's flags), and is not paid there: the Guide is the world's.
            if (Subworlds.Subworld.Active)
                return;

            if (Now == Beat.None)
            {
                foreach (Player player in Main.ActivePlayers)
                {
                    if (Walking(player))
                    {
                        Target = player.whoAmI;
                        Go(Beat.Counting);
                        return;
                    }
                }

                return;
            }

            Player them = Target >= 0 && Target < Main.maxPlayers ? Main.player[Target] : null;

            if (them == null || !them.active || them.dead || them.ghost)
            {
                Reset();
                return;
            }

            ticks++;

            switch (Now)
            {
                case Beat.Counting:
                    if (ticks >= CountTicks)
                        Go(Beat.Ready);
                    break;

                case Beat.Ready:
                    if (ticks >= ReadyLongest)
                        Reset();
                    break;

                case Beat.Coming:
                {
                    NPC guide = TheGuide();

                    if (guide == null)
                    {
                        Reset();
                        break;
                    }

                    if (There(guide, them) || ticks >= WalkLongest)
                    {
                        guide.netUpdate = true;
                        Go(Beat.Arrived);
                        break;
                    }

                    // The other machines walk him too; this keeps them honest.
                    if (ticks % 30 == 0)
                        guide.netUpdate = true;

                    break;
                }

                case Beat.Arrived:
                    if (TheGuide() == null)
                        Reset();
                    break;
            }
        }

        /// <summary>On their feet and going somewhere: a walk.</summary>
        private static bool Walking(Player player)
        {
            return !player.dead && !player.ghost && player.velocity.Y == 0f && Math.Abs(player.velocity.X) > 0.5f;
        }

        /// <summary>At the stop, or past it, still, and on the ground (on a slope the game leaves a little of the fall on him).</summary>
        private static bool There(NPC guide, Player them)
        {
            return Math.Abs(guide.Center.X - them.Center.X) <= StopAt + ThereSlack
                && Math.Abs(guide.velocity.X) < 0.1f
                && (guide.velocity.Y == 0f || guide.collideY);
        }

        private static void Go(Beat beat)
        {
            Now = beat;
            ticks = 0;
            Sync();
        }

        /// <summary>Broken off: still owed, nobody's, and the Guide his own again.</summary>
        private static void Reset()
        {
            Now = Beat.None;
            Target = -1;
            Guide = -1;
            ticks = 0;
            Sync();
        }

        /// <summary>
        /// Under the black: the Guide put down on ground he can walk to them from, on the side he
        /// was on (he comes from where he was), as near the planned distance as there is one;
        /// failing that, anywhere at that distance there is ground; failing that, beside them;
        /// failing that, where they stand. A world with no Guide gets one.
        /// </summary>
        private static void Bring(Player them)
        {
            int index = NPC.FindFirstNPC(NPCID.Guide);

            if (index < 0)
            {
                index = NPC.NewNPC(new EntitySource_WorldEvent(), (int)them.Center.X, (int)them.Bottom.Y, NPCID.Guide);

                if (index < 0 || index >= Main.maxNPCs)
                {
                    Reset();
                    return;
                }
            }

            NPC guide = Main.npc[index];
            int prefer = guide.Center.X < them.Center.X ? -1 : 1;

            Vector2 feet = Spot(them, guide.width, guide.height, prefer);

            guide.Bottom = feet;
            guide.velocity = Vector2.Zero;

            // Whatever he was doing (sitting, talking, going home) is over: he stands, and walks.
            guide.ai[0] = 0f;
            guide.ai[1] = 0f;
            guide.ai[2] = 0f;
            guide.ai[3] = 0f;

            int toward = them.Center.X >= guide.Center.X ? 1 : -1;
            guide.direction = toward;
            guide.spriteDirection = toward;
            guide.netUpdate = true;

            Guide = index;

            // Him first, then the news: a client has him where he is before it is told he is there.
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);

            Go(Beat.Coming);
        }

        /// <summary>Where his feet go. See <see cref="Bring"/>.</summary>
        private static Vector2 Spot(Player them, int width, int height, int prefer)
        {
            foreach (bool needWalk in new[] { true, false })
            {
                foreach (int way in new[] { prefer, -prefer })
                {
                    foreach (float tiles in ComeFrom)
                    {
                        float x = them.Center.X + way * tiles * 16f;

                        if (!DonaQuinnEncounter.Ground(x, them.Bottom.Y, width, height, 1, GroundReach, out float ground))
                            continue;

                        if (needWalk && !Walkable(x, ground, them.Center.X + way * StopAt, height))
                            continue;

                        return new Vector2(x, ground);
                    }
                }
            }

            foreach (int way in new[] { prefer, -prefer })
            {
                float x = them.Center.X + way * StopAt;

                if (DonaQuinnEncounter.Ground(x, them.Bottom.Y, width, height, 1, GroundReach, out float ground))
                    return new Vector2(x, ground);
            }

            return them.Bottom;
        }

        /// <summary>
        /// From feet at (<paramref name="fromX"/>, <paramref name="bottom"/>) to
        /// <paramref name="toX"/> at a walk: every column on the way has ground a step up or
        /// at most four down from the last, room over it for someone this tall, and no water
        /// or lava in that room.
        /// </summary>
        private static bool Walkable(float fromX, float bottom, float toX, int height)
        {
            int from = (int)(fromX / 16f);
            int to = (int)(toX / 16f);
            int dir = Math.Sign(to - from);

            if (dir == 0)
                return true;

            int row = (int)(bottom / 16f);
            int rows = (int)Math.Ceiling(height / 16f);

            for (int i = from + dir; i != to + dir; i += dir)
            {
                if (i < 1 || i >= Main.maxTilesX - 1)
                    return false;

                int found = -1;

                for (int j = row - StepUpRows; j <= row + StepDownRows; j++)
                {
                    if (Floor(i, j) && Room(i, j - rows, j - 1))
                    {
                        found = j;
                        break;
                    }
                }

                if (found < 0)
                    return false;

                row = found;
            }

            return true;
        }

        /// <summary>Something to stand on: a solid tile or a platform.</summary>
        private static bool Floor(int i, int j)
        {
            if (!WorldGen.InWorld(i, j, 1))
                return false;

            Tile tile = Main.tile[i, j];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        /// <summary>Rows <paramref name="top"/> to <paramref name="bottom"/> of this column: nothing solid, no liquid.</summary>
        private static bool Room(int i, int top, int bottom)
        {
            for (int j = top; j <= bottom; j++)
            {
                if (!WorldGen.InWorld(i, j, 1))
                    return false;

                Tile tile = Main.tile[i, j];

                if (tile.LiquidAmount > 0)
                    return false;

                if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    return false;
            }

            return true;
        }

        // ============================================================
        // THE PLAYER'S MACHINE TELLS THE WORLD
        // ============================================================

        /// <summary>From the player's machine to wherever the world is decided: straight there in single player, by packet from a client.</summary>
        internal static void Tell(Message message)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Heard(message, Main.myPlayer);
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = ModContent.GetInstance<GuideIntro>().Mod.GetPacket();
            packet.Write((byte)PADPacket.GuideIntro);
            packet.Write((byte)message);
            packet.Send();
        }

        /// <summary>On the server: a message from <paramref name="from"/>.</summary>
        internal static void HandlePacket(BinaryReader reader, int from)
        {
            Message message = (Message)reader.ReadByte();

            if (Main.netMode == NetmodeID.Server)
                Heard(message, from);
        }

        /// <summary>Taken only from the player the scene is for.</summary>
        private static void Heard(Message message, int from)
        {
            if (!Pending || from != Target || Target < 0 || Target >= Main.maxPlayers)
                return;

            switch (message)
            {
                case Message.Dark:
                    if (Now == Beat.Ready)
                        Bring(Main.player[from]);
                    break;

                case Message.Done:
                    if (Now == Beat.Coming || Now == Beat.Arrived)
                    {
                        Pending = false;
                        Reset();
                    }
                    break;

                case Message.Abort:
                    if (Now != Beat.None)
                        Reset();
                    break;
            }
        }

        // ============================================================
        // BY HAND (/guideintro, GuideIntroCommand)
        // ============================================================

        /// <summary>Owed again, from the start: any scene under way ended, the Guide his own again.</summary>
        internal static void Again()
        {
            Pending = true;
            Reset();
        }

        /// <summary>Where it stands in this world, in a line.</summary>
        internal static string Describe()
        {
            if (!Pending)
                return "The Guide's first words: had, or this world is from before them.";

            string who = Target >= 0 && Target < Main.maxPlayers && Main.player[Target].active ? Main.player[Target].name : "nobody";

            return Now switch
            {
                Beat.None => "The Guide's first words: owed, ten seconds after somebody first walks.",
                Beat.Counting => $"The Guide's first words: owed to {who}, in {Math.Max(0, (CountTicks - ticks) / 60 + 1)} s.",
                Beat.Ready => $"The Guide's first words: owed to {who}, as soon as they are on their feet.",
                _ => $"The Guide's first words: being said to {who} now.",
            };
        }
    }

    /// <summary>
    /// The scene, on the screen of the player it is for: held still from the fade out to the
    /// last answer, with the bars in; the fade (Cinematic.Fade); under the black the player
    /// tidied (off any mount, hook or rope) and the world told to put him down, and the camera
    /// over to the two of them, so that the picture comes back up on him walking up; facing
    /// him from then on; and the talk, which is the user's own lines, one at a time, each moved
    /// on by "...", and his question at the end with the four answers in the colours of the
    /// four pillars. A Solar answer, a Vortex, a Nebula and a Stardust.
    ///
    /// A world that gives the scene up (the player fell or left, the Guide went) is let go of at
    /// once, wherever it had got to: everything here is asked for every tick and eases back by
    /// itself when it stops being asked for.
    /// </summary>
    public class GuideIntroPlayer : ModPlayer
    {
        private enum Step
        {
            None,
            Waiting,
            FadeOut,
            Dark,
            FadeIn,
            Walk,
            Talk,

            /// <summary>Finished or given up on this machine: nothing until the world has heard.</summary>
            Over,
        }

        private const int FadeOutTicks = 30;
        private const int FadeInTicks = 30;

        /// <summary>The tick of the black the player is tidied and the world told: by then the black is on screen.</summary>
        private const int CutAt = 2;

        /// <summary>The black lasts at least this long, and is given up on after this long without him.</summary>
        private const int DarkTicks = 24;
        private const int DarkLongest = 240;

        /// <summary>Under the black, the camera is on the two of them this long before the picture comes back (PisPlayer's spring takes about a third of it).</summary>
        private const int SettleTicks = 18;

        /// <summary>Once he is there, this long before he speaks.</summary>
        private const int TalkAfter = 20;

        /// <summary>A Guide missing for this long (his packet can be behind the news) is gone.</summary>
        private const int MissingLongest = 60;

        /// <summary>The shot: halfway between them, and a little up, so both are in it.</summary>
        private const float ShotLift = 24f;

        // ---- his lines (the user's, as written) ---------------------------------------------

        private const int PaperLine = 4;
        private const int LastLine = 5;

        private static string Line(int i, NPC guide) => i switch
        {
            0 => "Oh " + Main.LocalPlayer.name + ", you finally woke up.",
            1 => "I actually don't know where you came from but, welcome!",
            2 => "My name is " + guide.GivenName + " and I shall help you throughout the journey.",
            3 => Language.GetTextValue("Mods.PisAndDag.Hints.GuideHut"),
            4 => "But first, take this.",
            _ => "What do you specialize in?",
        };

        private const string Continue = "...";

        // ---- the four answers, in the pillars' colours (sampled from the fragments) ---------

        private static readonly string[] Specialities = { "Close Combat", "Range Management", "Witchcraft", "Behavioral Conditioning" };

        private static readonly Color[] PillarColours =
        {
            new Color(254, 158, 35),  // Solar
            new Color(0, 242, 170),   // Vortex
            new Color(190, 30, 209),  // Nebula
            new Color(0, 174, 238),   // Stardust
        };

        /// <summary>What he gives for each answer: item and how many.</summary>
        private static (int Type, int Stack)[] Kit(int speciality) => speciality switch
        {
            0 => new (int, int)[] { (ItemID.WoodYoyo, 1), (ItemID.BladeofGrass, 1), (ItemID.Bacon, 5) },
            1 => new (int, int)[] { (ItemID.FlintlockPistol, 1), (ItemID.GoldBow, 1), (ItemID.WoodenArrow, 100), (ItemID.MusketBall, 100) },
            2 => new (int, int)[] { (ModContent.ItemType<SealOfAClothier>(), 1), (ItemID.DiamondStaff, 1), (ItemID.ManaCrystal, 3) },
            _ => new (int, int)[] { (ItemID.SlimeStaff, 1), (ItemID.BlandWhip, 1), (ItemID.DogTail, 1), (ItemID.HermesBoots, 1) },
        };

        // ---- where it has got to on this machine --------------------------------------------

        private static Step step;
        private static int stepTicks;
        private static float blackness;
        private static bool onThem;
        private static int line;
        private static bool paperGiven;
        private static int arrivedFor;
        private static int settled;
        private static int missing;

        /// <summary>Who the panel was opened on, so it can be shut after the world has let go of him (Guide is -1 by then).</summary>
        private static int speaker = -1;

        /// <summary>A world left mid-scene takes nothing of it into the next one (GuideIntro.ClearWorld).</summary>
        internal static void Forget()
        {
            step = Step.None;
            stepTicks = 0;
            blackness = 0f;
            onThem = false;
            line = 0;
            paperGiven = false;
            arrivedFor = 0;
            settled = 0;
            missing = 0;
            speaker = -1;
        }

        /// <summary>From the fade out to the last answer: theirs to watch, not to play.</summary>
        internal static bool Held => step >= Step.FadeOut && step <= Step.Talk;

        private bool Mine => Player.whoAmI == Main.myPlayer && !Main.dedServ;

        public override void PreUpdate()
        {
            if (!Mine)
                return;

            bool ours = GuideIntro.Pending && GuideIntro.Target == Player.whoAmI && GuideIntro.Now >= GuideIntro.Beat.Ready;

            if (step == Step.Over)
            {
                if (!ours)
                    Forget();

                return;
            }

            if (!ours)
            {
                if (step != Step.None)
                    Let();

                return;
            }

            // Dead: the panel will not stay up for them (Dialogue.Draw), and the world will give
            // it up as soon as it hears. Given up here at once, so nothing tries to put it back.
            if (Player.dead)
            {
                if (step != Step.None)
                    GiveUp();

                return;
            }

            stepTicks++;

            NPC guide = GuideIntro.TheGuide();

            switch (step)
            {
                case Step.None:
                    if (GuideIntro.Now == GuideIntro.Beat.Ready)
                        Next(Step.Waiting);
                    else
                        GiveUp();
                    break;

                case Step.Waiting:
                    // Never forced: held in water they would drown, and taken off a hook or a
                    // rope they would fall. Not on their feet in time, the world gives it up
                    // (ReadyLongest) and it is owed again from the next walk.
                    if (Standing())
                    {
                        EndChat();
                        Next(Step.FadeOut);
                    }
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
                    {
                        Tidy();
                        GuideIntro.Tell(GuideIntro.Message.Dark);
                    }

                    if (GuideIntro.Now >= GuideIntro.Beat.Coming && guide != null)
                    {
                        // He is down: the camera goes over to the two of them while nothing can
                        // be seen, and has settled there before the picture comes back.
                        onThem = true;

                        if (++settled >= SettleTicks && stepTicks >= DarkTicks)
                            Next(Step.FadeIn);
                    }
                    else if (stepTicks >= DarkLongest)
                    {
                        GiveUp();
                    }
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
                    arrivedFor = GuideIntro.Now == GuideIntro.Beat.Arrived ? arrivedFor + 1 : 0;

                    if (arrivedFor >= TalkAfter && guide != null)
                    {
                        Next(Step.Talk);
                        line = 0;
                        Converse(guide);
                    }
                    break;

                case Step.Talk:
                    // The box went down under him (something else took it): put it back.
                    if (guide != null && !Dialogue.Speaking(guide))
                        Converse(guide);
                    break;
            }

            if (step == Step.Over || step == Step.None)
                return;

            // His packet can be a little behind the news; gone for longer than that, he is gone.
            if (step >= Step.FadeIn)
            {
                missing = guide == null ? missing + 1 : 0;

                if (missing > MissingLongest)
                {
                    GiveUp();
                    return;
                }
            }

            if (blackness > 0f)
                Cinematic.Fade(blackness);

            if (Held)
            {
                Cinematic.Hold();
                Cinematic.Frame();
            }

            if (onThem && guide != null)
                PisPlayer.BlendCamera((Player.Center + guide.Center) * 0.5f - new Vector2(0f, ShotLift), 1f);
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

        /// <summary>On their feet: on the ground, dry, off any hook or rope.</summary>
        private bool Standing()
        {
            return Player.velocity.Y == 0f && !Player.wet && Player.grapCount == 0 && !Player.pulley;
        }

        /// <summary>As the fade begins: out of any chat, with him or anybody. None opens again until it is over (GuideIntroQuiet.CanChat).</summary>
        private void EndChat()
        {
            if (Player.talkNPC < 0)
                return;

            Player.SetTalkNPC(-1, false);
            Main.npcChatText = "";
        }

        /// <summary>Under the black: off any mount, hook or rope, and stopped.</summary>
        private void Tidy()
        {
            if (Player.mount.Active)
                Player.mount.Dismount(Player);

            Player.RemoveAllGrapplingHooks();
            Player.pulley = false;
            Player.velocity.X = 0f;
        }

        /// <summary>He says the line it is at, with its answers. The paper comes with its line, once.</summary>
        private static void Converse(NPC guide)
        {
            if (line == PaperLine)
                GivePaper();

            speaker = guide.whoAmI;
            Dialogue.Converse(guide, Line(line, guide), Dialogue.DefaultLettersPerTick, Answers());
        }

        /// <summary>"..." under every line but the last; the four answers under that.</summary>
        private static TalkOption[] Answers()
        {
            if (line < LastLine)
            {
                int at = line;
                return new[] { new TalkOption(Continue, npc => Say(at + 1, npc)) { Remembered = false } };
            }

            var answers = new TalkOption[Specialities.Length];

            for (int i = 0; i < answers.Length; i++)
            {
                int which = i;
                answers[i] = new TalkOption(Specialities[i], _ => Choose(which)) { Remembered = false, Colour = PillarColours[i] };
            }

            return answers;
        }

        /// <summary>The next line, in the box already up.</summary>
        private static void Say(int i, NPC guide)
        {
            line = i;

            if (line == PaperLine)
                GivePaper();

            Dialogue.Scene(Line(line, guide), Answers());
        }

        /// <summary>The Piece of Paper, unless they already carry it (a scene broken off after it, and begun again).</summary>
        private static void GivePaper()
        {
            if (paperGiven)
                return;

            paperGiven = true;

            Player me = Main.LocalPlayer;
            int paper = ModContent.ItemType<PieceOfPaper>();

            if (me.HasItem(paper) || me.GetModPlayer<LoreLock>().CodexHolds(paper))
                return;

            me.QuickSpawnItem(me.GetSource_GiftOrReward("PisAndDag:GuideIntro"), paper, 1);
        }

        /// <summary>
        /// Their answer: the kit for it, the box down, the world told it is done, and the scene
        /// lets go. Only while the scene is still theirs as this machine knows it: one the world
        /// has given up (and will play again) gives nothing now.
        /// </summary>
        private static void Choose(int speciality)
        {
            Player me = Main.LocalPlayer;

            bool still = GuideIntro.Pending && GuideIntro.Target == me.whoAmI && GuideIntro.Now == GuideIntro.Beat.Arrived;

            if (!still)
            {
                Let();
                step = Step.Over;
                return;
            }

            foreach ((int type, int stack) in Kit(speciality))
                me.QuickSpawnItem(me.GetSource_GiftOrReward("PisAndDag:GuideIntro"), type, stack);

            Dialogue.Close();
            GuideIntro.Tell(GuideIntro.Message.Done);

            step = Step.Over;
            blackness = 0f;
            onThem = false;
        }

        /// <summary>It came apart on this screen: the world told, everything let go.</summary>
        private static void GiveUp()
        {
            GuideIntro.Tell(GuideIntro.Message.Abort);
            Let();
            step = Step.Over;
        }

        /// <summary>Everything this scene asked for, let go: the panel shut if it is still his, and the rest eases back by itself.</summary>
        private static void Let()
        {
            if (speaker >= 0 && speaker < Main.maxNPCs && Dialogue.Speaking(Main.npc[speaker]))
                Dialogue.Close();

            Forget();
        }

        // ---- the hold, beyond what Cinematic.Hold takes: the inventory and the rest -----

        public override void SetControls()
        {
            if (!Mine || !Held)
                return;

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
        }

        /// <summary>Still round them through the count and the scene; and, from the cut, facing him.</summary>
        public override void PostUpdate()
        {
            if (!Mine)
                return;

            if (GuideIntro.Keeps(Player))
                Player.AddBuff(ModContent.BuffType<Stillness>(), 2);

            if (!onThem || !Held)
                return;

            NPC guide = GuideIntro.TheGuide();

            if (guide == null || Math.Abs(guide.Center.X - Player.Center.X) < 4f)
                return;

            Player.direction = Math.Sign(guide.Center.X - Player.Center.X);
        }
    }

    /// <summary>
    /// The Guide while the scene has him: no AI of his own, nothing can hurt him, and he walks
    /// up to the player at an unhurried pace, a step at a time where the ground steps (StepUp),
    /// a hop where something stops him; slows into the stop, and faces them. Run on every
    /// machine, so each sees him walk; the server's is the one that counts.
    /// </summary>
    public class GuideIntroGuide : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Guide;

        /// <summary>
        /// His walk: pixels a tick at most, how quickly he gets up to it, how early he slows (a
        /// share of the distance left), the least he slows to, and how near the stop is there.
        /// </summary>
        private const float WalkSpeed = 1.4f;
        private const float WalkAccel = 0.08f;
        private const float Brake = 0.06f;
        private const float LastSteps = 0.3f;
        private const float StopSlack = 1f;

        /// <summary>Pressed against something this long: a hop, this hard (about four tiles).</summary>
        private const int HopAfter = 6;
        private const float HopSpeed = 6.5f;

        /// <summary>The scene had him last tick: when it lets go, so does the cover.</summary>
        private bool held;

        private int pressed;

        /// <summary>No chatting to him while the scene has him (vanilla's right-click, Main.HoverOverNPCs): what he says then is the scene's.</summary>
        public override bool? CanChat(NPC npc) => GuideIntro.Holds(npc) ? false : null;

        public override bool PreAI(NPC npc)
        {
            if (!GuideIntro.Holds(npc))
            {
                if (held)
                {
                    held = false;
                    pressed = 0;
                    npc.dontTakeDamage = false;
                }

                return true;
            }

            held = true;
            npc.dontTakeDamage = true;

            Player them = Main.player[GuideIntro.Target];
            float dx = them.Center.X - npc.Center.X;
            int toward = dx >= 0f ? 1 : -1;
            float left = Math.Abs(dx) - GuideIntro.StopAt;

            float want = GuideIntro.Now == GuideIntro.Beat.Coming && left > StopSlack
                ? toward * MathHelper.Clamp(left * Brake, LastSteps, WalkSpeed)
                : 0f;

            float vx = npc.velocity.X;

            if (Math.Abs(want - vx) <= WalkAccel)
                vx = want;
            else
                vx += Math.Sign(want - vx) * WalkAccel;

            npc.velocity.X = vx;

            if (vx != 0f && npc.velocity.Y >= 0f)
                Collision.StepUp(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY, 1, false, 0);

            pressed = want != 0f && npc.collideX && npc.velocity.Y == 0f ? pressed + 1 : 0;

            if (pressed > HopAfter)
            {
                pressed = 0;
                npc.velocity.Y = -HopSpeed;

                if (Main.netMode == NetmodeID.Server)
                    npc.netUpdate = true;
            }

            npc.direction = toward;
            npc.spriteDirection = toward;

            return false;
        }
    }

    /// <summary>
    /// Nothing new spawns round the player the scene is for, from the first walk to the last
    /// answer; and while it holds them, they chat to nobody (vanilla's right-click on a
    /// townsperson, Main.HoverOverNPCs, and the smart interact: both on their own machine).
    /// </summary>
    public class GuideIntroQuiet : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (!GuideIntro.Keeps(player))
                return;

            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }

        public override bool? CanChat(NPC npc) => GuideIntroPlayer.Held ? false : null;
    }
}
