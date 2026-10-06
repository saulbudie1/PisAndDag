using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// Foigh, the Swordsman: an old man with a red eye and a sword twice his height, which he
    /// drags.
    ///
    /// THE ROAD. He stands with his back to whoever has come, his sword in the ground beside
    /// him. The picture fades; the player is put down twenty-eight tiles off and walks to
    /// twenty; the camera goes over to him; he looks back over his shoulder, turns round, and
    /// speaks (four lines, the user's own words, each answered). After the third he takes the
    /// sword out of the ground, swings it up over his head and lets it down behind him, into
    /// the stance he fights from. After the fourth his name comes up, the picture fades back
    /// to the player, and it begins.
    ///
    /// THE FIGHT, phase one (the only one there is yet: its moves run the whole of his life).
    /// He does nothing by touch. Everything off the sword goes through armour, and is varied
    /// the way vanilla varies any hit.
    ///
    ///   WALKING. Slowly, at them, the sword dragged behind him. While he walks he answers
    ///   what is sent at him (Foigh.React): a swing of theirs met with his own and them thrown
    ///   back, a shot struck out of the air and sometimes sent back, a spell stepped out of
    ///   the way of.
    ///   ONE. Wound up over his head: down, up, a thrust, and a leap back with the sword
    ///   coming round after him.
    ///   TWO. A leap onto them, point first. On them, he goes straight on into something
    ///   else; on nothing, the sword is in the ground for two seconds and he pulls at it.
    ///   THREE. The bottom hand: a blow down, held and then let go, for twice the sword's
    ///   damage; the whole of him round once with the sword out at arm's length; a knife off
    ///   the top hand.
    ///   FOUR. Up, and the sword thrown at them: it wears the yellow star, knocks them down
    ///   for a second and stays in the ground. He comes down, runs to it, takes it and goes
    ///   round twice with it.
    ///   KNIVES. From further off: one, quick; now and then three, twenty ticks apart.
    ///   BEHIND. A flash step to behind them, half a second, a cut up, and a flash step back
    ///   to where he stood.
    ///   THE ONE WHO DRANK. A healing potion drunk while he can see it: a flash step, the
    ///   bottom hand to their throat (the yellow star: it can be got out of the way of, not
    ///   turned aside), up off their feet, thrown up, the sword up through them, held, and
    ///   flung off it.
    ///
    /// His sword's blows can be parried (Parry): he reels, and on every third he is down on
    /// a knee for two seconds, open to the riposte. Not the thrown sword or the hand, which
    /// wear the star.
    ///
    /// He is drawn from seven pictures, not a sheet (FoighRig), and every movement is poses
    /// at moments (FoighMoves): the blade that is drawn is the blade that hits.
    ///
    /// Partial: this file is the body of it (numbers, moves, sync, hooks); .Fight is the
    /// fight, .React what he does about what is sent at him, .Scene the road, .Draw the
    /// drawing.
    /// </summary>
    [AutoloadBossHead]
    public partial class Foigh : ModNPC
    {
        public override string Texture => FoighRig.Folder + FoighRig.Names[FoighRig.Torso];

        public override string BossHeadTexture => FoighRig.Folder + FoighRig.Names[FoighRig.Head];

        // ============================================================
        // NUMBERS
        // ============================================================

        /// <summary>His life in a Classic, an Expert and a Master world, as given; and half as much again in Legends.</summary>
        public const int LifeClassic = 150000;
        public const int LifeExpert = 240000;
        public const int LifeMaster = 360000;
        public const float LegendsLife = 1.5f;

        /// <summary>None was given: a little under the Moon Lord's.</summary>
        public const int Defense = 40;

        /// <summary>
        /// The sword, through armour, in a Classic, an Expert and a Master world: while he has
        /// more than two thirds of his life, and after. Half as much again in Legends.
        /// </summary>
        private static readonly int[] SwordFirst = { 100, 130, 150 };
        private static readonly int[] SwordLater = { 150, 200, 230 };
        public const float LegendsSword = 1.5f;

        /// <summary>The blow down in his third move: twice the sword.</summary>
        public const float HardBlow = 2f;

        /// <summary>A knife, through armour, the same in every world; and a shot of theirs sent back.</summary>
        public const int DaggerDamage = 100;

        /// <summary>Where his second and third phases will begin: shares of his life.</summary>
        public const float SecondAt = 0.66f;
        public const float ThirdAt = 0.30f;

        /// <summary>How fast he talks: slowly.</summary>
        public const float Pace = 0.55f;

        public const int Wide = 26;
        public const int Tall = 50;

        /// <summary>His own weight: pixels a tick he gains falling, and the fastest he falls.</summary>
        public const float Gravity = 0.55f;
        public const float MaxFall = MaxSpeed;

        /// <summary>
        /// The fastest he ever moves along or up and down while tiles stop him: under a tile a
        /// tick. The game's tile collision looks one tile past where a thing is and no further
        /// (Collision.TileCollision, 1.4.4: asked directly, a box against a wall and going more
        /// than sixteen pixels a tick is let sixteen pixels into it; its own NPCs fall at ten
        /// at the most). Faster than that he could end up in a wall or under a floor.
        /// </summary>
        public const float MaxSpeed = 15.5f;

        // ============================================================
        // MOVES
        // ============================================================

        public enum Move
        {
            // ---- the road ----

            /// <summary>His back to them, the sword in the ground: until they have walked up.</summary>
            Await,

            /// <summary>The camera comes over; he looks back, and turns round.</summary>
            Turn,

            /// <summary>Three lines, each answered.</summary>
            Talk,

            /// <summary>The sword out of the ground, up, and down behind him.</summary>
            Draw,

            /// <summary>The fourth line, and Ready.</summary>
            Vow,

            /// <summary>His name; the picture back to the player.</summary>
            Begin,

            // ---- the fight ----

            /// <summary>At them, slowly, the sword dragged.</summary>
            Walk,

            /// <summary>Down, up, the thrust, the leap back.</summary>
            One,

            /// <summary>The leap onto them: down into his knees, in the air, in the ground, out of it, or landed on them.</summary>
            TwoGather,
            TwoAir,
            TwoStuck,
            TwoFree,
            TwoLanded,

            /// <summary>The blow down with the bottom hand, the turn, the knife.</summary>
            Three,
            ThreeRound,
            ThreeEnd,

            /// <summary>Up, the sword thrown, down, after it, and twice round with it.</summary>
            FourGather,
            FourAir,
            FourFall,
            FourTouch,
            FourRun,
            FourTake,
            FourRound,
            FourEnd,

            /// <summary>One knife or three.</summary>
            Knives,

            /// <summary>Gone from one place to another: what he does there is said with it.</summary>
            Flash,

            /// <summary>Behind them: the wait, the cut up.</summary>
            Behind,

            /// <summary>A shot struck out of the air; a swing met.</summary>
            Guard,
            Clash,

            /// <summary>The one who drank: the hand, nothing in it, up off their feet, the sword up through them, held there, flung off.</summary>
            Grab,
            Miss,
            Lift,
            Spit,
            Spitted,
            Fling,

            /// <summary>His sword turned aside; down on a knee.</summary>
            Reel,
            Stagger,

            /// <summary>At nothing: down, and gone.</summary>
            Dying,

            /// <summary>Nobody left to fight: he stands, and is gone.</summary>
            Leave,
        }

        // ai[0] Move · ai[1] ticks in it · ai[2] a number the move wants · ai[3] the player he fights

        public Move State
        {
            get => (Move)(int)NPC.ai[0];
            private set
            {
                NPC.ai[0] = (int)value;
                NPC.ai[1] = 0f;
            }
        }

        private int Timer
        {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        /// <summary>The move's own number: the knives to throw, the ticks a leap takes, the angle the sword went in at.</summary>
        private float Arg
        {
            get => NPC.ai[2];
            set => NPC.ai[2] = value;
        }

        public int TargetIndex
        {
            get => (int)NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        // ---- sent with him (SendExtraAI) -----------------------------------------------

        /// <summary>Which way he faces.</summary>
        private int facing = 1;

        /// <summary>The sword is in his hand: not still in the ground on the road, not thrown.</summary>
        private bool hasSword;

        /// <summary>A flash step's two ends: where his box's corner was, and where it will be.</summary>
        private Vector2 stepFrom;
        private Vector2 stepTo;

        /// <summary>What he does where a flash step ends, and that move's number.</summary>
        private Move then = Move.Walk;
        private float thenArg;

        /// <summary>Where he stood before he went behind them: where he goes back to.</summary>
        private Vector2 anchor;

        /// <summary>The speed a leap begins at.</summary>
        private Vector2 leap;

        /// <summary>The player in his hand or on his sword, or none.</summary>
        private int held = -1;

        /// <summary>Ticks of walking left before his next move.</summary>
        private int rest;

        /// <summary>Shots struck in the guard he is in.</summary>
        private int guards;

        /// <summary>The leap he is in began over them (a step up out of the way), not from the ground.</summary>
        private bool above;

        /// <summary>The move before this one, so that he does not do the same thing twice.</summary>
        private Move last = Move.Walk;

        // ---- where the world is decided only -------------------------------------------

        /// <summary>The leap he is in has landed on somebody.</summary>
        private bool landedHit;

        /// <summary>Somebody drank: ticks left in which he will make them pay for it, and who.</summary>
        private int drank;
        private int drankBy = -1;

        // ---- each machine's own ---------------------------------------------------------

        /// <summary>What he was doing last tick: a change is the first tick of the new move, wherever it was decided.</summary>
        private Move seen = (Move)(-1);

        /// <summary>
        /// Whether this is the first tick this machine has seen of the move he is in, or of
        /// that move begun again. Read through the tick; set once at its start.
        /// </summary>
        private bool fresh;

        /// <summary>The tick of the move this machine dealt with last, and the one it is dealing with: a thing that happens at a tick happens when that tick is crossed.</summary>
        private int was = -1;
        private int now;

        /// <summary>A move whose ticks have gone back by more than this has been begun again.</summary>
        private const int Again = 3;

        /// <summary>On the ground, as the last tick's move left him.</summary>
        private bool grounded;

        private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>The road is behind him: from the first step of the fight to the end of it.</summary>
        public bool Fighting => State >= Move.Walk && State < Move.Dying;

        /// <summary>Which third of his life he is in: 1, 2 or 3.</summary>
        public int Stage
        {
            get
            {
                float share = NPC.life / (float)Math.Max(1, NPC.lifeMax);
                return share > SecondAt ? 1 : share > ThirdAt ? 2 : 3;
            }
        }

        /// <summary>The ground between his feet.</summary>
        public Vector2 Feet => new Vector2(NPC.position.X + NPC.width * 0.5f, NPC.position.Y + NPC.height);

        /// <summary>Which way he faces: 1 or -1.</summary>
        public int Faces => facing;

        /// <summary>His sword is out of his hand in the fight, thrown, and he will come for it.</summary>
        public bool WantsSword => !hasSword && State >= Move.FourAir && State <= Move.FourTake;

        /// <summary>Whether this tick of the move has just been reached, on this machine.</summary>
        private bool At(int tick) => was < tick && now >= tick;

        // ============================================================
        // SETUP
        // ============================================================

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.MPAllowedEnemies[Type] = true;

            // He is drawn from pieces; the bestiary's portrait would be a chest with no head.
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;

            // The road is his own, and his name comes up at his own moment, the bar with it
            // (Scene: BossIntro.PlayNameOnly when they have said they are ready).
            BossPlate.HasOwnIntro(Type);
            BossPlate.NamesItself(Type);
            BossPlate.BarWaitsForName(Type);

            DialoguePortrait.Register(Type, FoighRig.Folder + FoighRig.Names[FoighRig.Head]);
            DialoguePortrait.Name(Type, null, "the Swordsman");

            // His sword can be turned aside, boss or not, in the moves it cuts in; what that
            // does to him is his own (Turned). His hits are worked out here, not by the game's
            // touch, so each asks the parry itself before it lands (Strike). On the ground,
            // by the parry's count: he has no weight only because he carries his own.
            Parry.AddNPC(Type,
                npc => npc.ModNPC is Foigh foigh && foigh.CanBeTurned,
                (npc, by, away, staggers) => (npc.ModNPC as Foigh)?.Turned(staggers),
                flies: false,
                told: "His sword is turned aside and he reels; every third time he is down on a knee, open to the riposte. Not the thrown sword or the hand.");
        }

        public override void SetDefaults()
        {
            NPC.width = Wide;
            NPC.height = Tall;

            NPC.aiStyle = -1;

            // His life is set here, for the world he is put down in, and nowhere else: the game
            // leaves an NPC that does nothing by touch (damage 0) as it finds it, in every
            // difficulty (NPC.ScaleStats, 1.4.4, returns before it has scaled anything or asked
            // any mod), so nothing after this changes it.
            NPC.lifeMax = FullLife();
            NPC.damage = 0;
            NPC.defense = Defense;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.npcSlots = 10f;
            NPC.value = 0f;
            NPC.netAlways = true;

            // Nothing lands on him on the road.
            NPC.dontTakeDamage = true;

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = null;

            NPC.PAD().BossName = "Foigh";
            NPC.PAD().BossTitle = "The Swordsman";
            NPC.PAD().OwnIntro = true;

            Music = 0;
            SceneEffectPriority = SceneEffectPriority.BossLow;
        }

        /// <summary>
        /// Not asked of him as he is (see SetDefaults). If something ever makes the game scale
        /// him after all, his life is still what was given: by the world's difficulty and
        /// Legends, not by how many are in it.
        /// </summary>
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = FullLife();
        }

        /// <summary>His whole life in the world as it is: as given for a Classic, an Expert and a Master world, and half as much again in Legends.</summary>
        public static int FullLife()
        {
            int life = Main.masterMode ? LifeMaster : Main.expertMode ? LifeExpert : LifeClassic;

            return PADWorld.Legends ? (int)(life * LegendsLife) : life;
        }

        /// <summary>What a blow of his sword does before the game varies it, as he is now: by the world, by Legends, by how much of his life is left.</summary>
        public int SwordDamage()
        {
            int[] table = Stage == 1 ? SwordFirst : SwordLater;
            int damage = table[Main.masterMode ? 2 : Main.expertMode ? 1 : 0];

            return PADWorld.Legends ? (int)(damage * LegendsSword) : damage;
        }

        /// <summary>
        /// Put down by anything (a command, another mod's menu): with the player he was put
        /// down for as his (NPC.NewNPC's Target, which is nobody unless whoever called it
        /// said), or the nearest, his back to them, and his sword in the ground.
        /// </summary>
        public override void OnSpawn(IEntitySource source)
        {
            if (!Authority)
                return;

            int nearest = NPC.target >= 0 && NPC.target < Main.maxPlayers && Main.player[NPC.target].active
                ? NPC.target
                : Player.FindClosest(NPC.position, NPC.width, NPC.height);

            TargetIndex = nearest;
            NPC.target = nearest;

            Player player = Main.player[nearest];

            facing = player.active && player.Center.X > NPC.Center.X ? -1 : 1;
            hasSword = false;
            NPC.netUpdate = true;
        }

        // He goes when the fight says, not when vanilla's despawn would.
        public override bool CheckActive() => false;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        /// <summary>
        /// A blow that would kill him leaves him on one: he goes down on a knee, and is gone
        /// from there (Dying), which sets him to nothing itself.
        /// </summary>
        public override bool CheckDead()
        {
            if (State == Move.Dying && gone)
                return true;

            NPC.life = 1;
            NPC.dontTakeDamage = true;

            if (Authority && State != Move.Dying)
            {
                Release();
                Go(Move.Dying);
            }

            return false;
        }

        /// <summary>Nothing burns or poisons him while nothing can hurt him: the road is not a fight.</summary>
        public override void UpdateLifeRegen(ref int damage)
        {
            if (!NPC.dontTakeDamage)
                return;

            if (NPC.lifeRegen < 0)
                NPC.lifeRegen = 0;

            damage = 0;
        }

        // ============================================================
        // SYNC
        // ============================================================

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((sbyte)facing);
            writer.Write(hasSword);
            writer.Write(above);
            writer.Write(stepFrom.X);
            writer.Write(stepFrom.Y);
            writer.Write(stepTo.X);
            writer.Write(stepTo.Y);
            writer.Write((byte)then);
            writer.Write(thenArg);
            writer.Write(anchor.X);
            writer.Write(anchor.Y);
            writer.Write(leap.X);
            writer.Write(leap.Y);
            writer.Write((short)held);
            writer.Write((short)Math.Clamp(rest, short.MinValue, short.MaxValue));
            writer.Write((byte)Math.Min(guards, 255));
            writer.Write((byte)last);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            facing = reader.ReadSByte();
            hasSword = reader.ReadBoolean();
            above = reader.ReadBoolean();
            stepFrom = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            stepTo = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            then = (Move)reader.ReadByte();
            thenArg = reader.ReadSingle();
            anchor = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            leap = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            held = reader.ReadInt16();
            rest = reader.ReadInt16();
            guards = reader.ReadByte();
            last = (Move)reader.ReadByte();
        }

        // ============================================================
        // WHO
        // ============================================================

        /// <summary>The player he fights, alive or not; null if they have left.</summary>
        public Player Target()
        {
            int i = TargetIndex;

            if (i < 0 || i >= Main.maxPlayers)
                return null;

            Player player = Main.player[i];
            return player.active ? player : null;
        }

        /// <summary>Whether this machine is the one whose player the road is for.</summary>
        public bool TargetIsHere => !Main.dedServ && TargetIndex == Main.myPlayer;

        /// <summary>Nothing lands on him on the road, while he is between two places, or once it is over.</summary>
        private bool Untouchable => State <= Move.Begin || State == Move.Flash || State == Move.Dying || State == Move.Leave;

        /// <summary>
        /// The server (or single player) moves him on. Whether anything can land on him goes
        /// with the move at once, not at his next tick: what is in the air this tick is still
        /// to be moved, and a step out of its way has to be out of its way now.
        /// </summary>
        private void Go(Move move, float arg = 0f)
        {
            if (!Authority)
                return;

            State = move;
            Arg = arg;
            NPC.dontTakeDamage = Untouchable;
            NPC.netUpdate = true;
        }

        /// <summary>Whoever he has hold of is let go of, where the world is decided.</summary>
        private void Release()
        {
            if (held < 0)
                return;

            held = -1;
            NPC.netUpdate = true;
        }

        // ============================================================
        // WORDS: from a player's own machine to where the world is decided
        // ============================================================

        public enum Word : byte
        {
            /// <summary>The road: walked up; the three lines answered; ready.</summary>
            Arrived,
            Talked,
            Ready,

            /// <summary>This player has drunk a healing potion.</summary>
            Drank,

            /// <summary>His leap came down on this player.</summary>
            Struck,

            /// <summary>A shot of this player's is about to reach him, and he will strike it out of the air.</summary>
            Guard,

            /// <summary>One is about to reach him, and he will not be there: where it is, and how it goes.</summary>
            Dodge,

            /// <summary>A swing of this player's met his sword.</summary>
            Clash,

            /// <summary>A shot of this player's was struck out of the air: where, how it went, what it was, and whether it comes back.</summary>
            Parried,
        }

        /// <summary>
        /// Something of this machine's player's, to wherever the world is decided: straight to
        /// him in single player, by packet from a client.
        /// </summary>
        internal void Tell(Word word, Vector2 a = default, Vector2 b = default, int n = 0, bool flag = false)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Heard(word, Main.myPlayer, a, b, n, flag);
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)PADPacket.Foigh);
            packet.Write((byte)word);
            packet.Write((byte)NPC.whoAmI);
            packet.Write(a.X);
            packet.Write(a.Y);
            packet.Write(b.X);
            packet.Write(b.Y);
            packet.Write(n);
            packet.Write(flag);
            packet.Send();
        }

        /// <summary>On the server: a word from <paramref name="from"/>.</summary>
        internal static void HandlePacket(BinaryReader reader, int from)
        {
            Word word = (Word)reader.ReadByte();
            int index = reader.ReadByte();
            Vector2 a = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Vector2 b = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            int n = reader.ReadInt32();
            bool flag = reader.ReadBoolean();

            if (Main.netMode != NetmodeID.Server || index < 0 || index >= Main.maxNPCs || from < 0 || from >= Main.maxPlayers)
                return;

            NPC npc = Main.npc[index];

            if (npc.active && npc.ModNPC is Foigh foigh)
                foigh.Heard(word, from, a, b, n, flag);
        }

        /// <summary>A player's word has arrived where the world is decided.</summary>
        private void Heard(Word word, int from, Vector2 a, Vector2 b, int n, bool flag)
        {
            if (!Authority)
                return;

            Player by = Main.player[from];

            if (!by.active || by.dead)
                return;

            switch (word)
            {
                case Word.Arrived when State == Move.Await && from == TargetIndex:
                    Go(Move.Turn);
                    break;

                case Word.Talked when State == Move.Talk && from == TargetIndex:
                    Go(Move.Draw);
                    break;

                case Word.Ready when State == Move.Vow && from == TargetIndex:
                    Go(Move.Begin);
                    break;

                case Word.Drank when Fighting:
                    drank = DrankTicks;
                    drankBy = from;
                    break;

                case Word.Struck when State == Move.TwoAir:
                    landedHit = true;
                    break;

                // The word came after he was down: he is not stuck after all.
                case Word.Struck when State == Move.TwoStuck && Timer <= LateStruck:
                    Go(Move.TwoLanded, Arg);
                    break;

                case Word.Guard when CanReact:
                    Guarded(from);
                    break;

                case Word.Dodge when CanReact:
                    Dodged(by, a, b);
                    break;

                case Word.Clash when CanReact:
                    Clashed(by);
                    break;

                case Word.Parried when Fighting:
                    if (CanReact)
                        Guarded(from);

                    if (flag)
                        SendBack(by, a, b, n);
                    break;
            }
        }

        // ============================================================
        // AI
        // ============================================================

        public override void AI()
        {
            now = Timer;

            // A move is new when it is another move, or the same one begun again (its ticks
            // have gone well back: a second step straight out of the first, a second shot
            // struck with the sword still up). A tick or two back is only the server's word
            // arriving a little late or early on a client.
            fresh = seen != State || now + Again < was;

            if (fresh)
            {
                Entered(seen);

                // Come into partway through (a move begun from its middle, or news that came
                // late): what its earlier ticks did is not done now.
                was = now > 3 ? now - 1 : -1;
            }

            seen = State;
            grounded = NPC.velocity.Y == 0f;

            Player target = Target();

            if (Authority && Lost(target))
                return;

            bool road = State <= Move.Begin;

            NPC.dontTakeDamage = Untouchable;

            // Quiet on the road and after; the fight's own from its first step.
            Music = Fighting ? FightMusic : 0;

            // Behind the tiles while the blade is in the ground, so the ground hides what is in it.
            NPC.behindTiles = BladeInGround;

            if (road)
                Road(target);
            else
                Fight(target);

            // Whatever the move asked for, no faster than tiles can stop (MaxSpeed).
            if (!NPC.noTileCollide)
            {
                NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -MaxSpeed, MaxSpeed);
                NPC.velocity.Y = MathHelper.Clamp(NPC.velocity.Y, -MaxSpeed, MaxSpeed);
            }

            // Moved on in the middle of the tick: what is left of the tick is not this move's
            // any more, and the new one has its own first tick to come.
            if (State == seen)
            {
                Show();

                if (!Main.dedServ)
                {
                    Strike();
                    Looks();
                }
            }

            if (!Main.dedServ)
            {
                React();
                Drinks();
            }

            if (TargetIsHere && State <= Move.Begin)
            {
                // A part of the road begun in the middle of this tick has its first tick still
                // to come: the scene is asked for (it must be, every tick), on that part's
                // clock, which is at nothing.
                int clock = now;

                if (State != seen)
                    now = 0;

                Scene();
                now = clock;
            }

            Remember();

            was = now;
            Timer++;
        }

        // ============================================================
        // BY HAND (/foigh, FoighCommand)
        // ============================================================

        /// <summary>
        /// Put down for this player, now, wherever they are: on ground they could walk to,
        /// forty-six tiles ahead of them the way they face, or nearer, or behind them if
        /// there is none ahead. Null when he is there; why not when he is not.
        /// </summary>
        internal static string Summon(Player player)
        {
            if (player == null || !player.active || player.dead)
                return "There is nobody standing to wait for.";

            if (NPC.AnyNPCs(ModContent.NPCType<Foigh>()))
                return "He is already here.";

            for (int turn = 0; turn < 2; turn++)
            {
                int way = turn == 0 ? player.direction : -player.direction;

                for (float tiles = 46f; tiles >= 26f; tiles -= 2f)
                {
                    float x = player.Center.X + way * tiles * 16f;

                    if (!DonaQuinn.DonaQuinnEncounter.Ground(x, player.Bottom.Y, Wide, Tall, 2, 12, out float ground))
                        continue;

                    if (!Collision.CanHitLine(new Vector2(x, ground - Tall), 2, 2, player.Top, 2, 2))
                        continue;

                    int index = NPC.NewNPC(new EntitySource_WorldEvent(), (int)x, (int)ground, ModContent.NPCType<Foigh>(),
                        0, 0f, 0f, 0f, player.whoAmI, player.whoAmI);

                    if (index < 0 || index >= Main.maxNPCs)
                        return "There was no room for him.";

                    return null;
                }
            }

            return "There is no level ground near enough for him to stand on.";
        }

        /// <summary>
        /// Nobody to fight: the one he had is gone or dead. On the road he goes with them; in
        /// the fight he turns to whoever is nearest and alive, and with nobody he leaves.
        /// </summary>
        private bool Lost(Player target)
        {
            if (target != null && !target.dead)
                return false;

            if (State == Move.Dying || State == Move.Leave)
                return false;

            if (Fighting)
            {
                int other = -1;
                float nearest = 3200f;

                foreach (Player player in Main.ActivePlayers)
                {
                    float far = Vector2.Distance(player.Center, NPC.Center);

                    if (!player.dead && far < nearest)
                    {
                        nearest = far;
                        other = player.whoAmI;
                    }
                }

                if (other >= 0)
                {
                    TargetIndex = other;
                    NPC.target = other;
                    NPC.netUpdate = true;
                    return false;
                }

                Release();
                Go(Move.Leave);
                return false;
            }

            Vanish();
            return true;
        }

        /// <summary>Gone, on every machine.</summary>
        private void Vanish()
        {
            if (!Authority)
                return;

            NPC.active = false;
            NPC.netSkip = -1;
            NPC.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }
    }
}
