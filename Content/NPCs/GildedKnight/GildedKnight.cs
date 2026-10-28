using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// The Gilded Knight of Grace: a knight of the old kingdom in plate gilded over, two swords
    /// as long as he is tall, standing his watch on the willow flat before the Curtain, where
    /// the road meets the wall that held.
    ///
    /// THE SCENE (GildedKnight.Scene). The first time someone comes up through the willows
    /// toward the wall, the picture fades, and comes back on him far off, close: he walks at
    /// them slowly, his swords in their scabbards at his hips, his armour ringing at every
    /// step; stops; draws both swords across himself and crosses them before his face (by
    /// request: "from side to side in x formation, left to right"); parts them; and points
    /// one slowly at them. His name; the camera goes back to them and out; the fight. After
    /// that he is not there until he is called back with what he leaves (KnightEncounter).
    ///
    /// THE FIELD. From the top of the way up to the wall's foot, a box nobody can leave and
    /// nobody can come into while it stands (BossArena, sealed): Dame Quilt's, by request.
    ///
    /// THE FIGHT (GildedKnight.Fight). His moves were learned from Rellana, Twin Moon Knight
    /// (the video the request linked, watched frame by frame): the twin cut, the blades in
    /// turn with a step each and a held last blow; the leap down onto them with a shockwave
    /// either way; crescents of light along the ground; the gilded wheel, round and round at
    /// them and a leap back; the lancet, a thrust that lets a lance of light go the length of
    /// the field; votive blades loosed one after another. At half his life, his change: the
    /// blades take fire and light, and the second half adds the great crescent, the procession
    /// of fire, the crown of blades, the long blade, and the gilded sun.
    ///
    /// Every cut of a blade in his hand can be parried (Parry): it is turned aside, and he
    /// rocks back a step and a half with a ward of gold about him; for that second nothing
    /// lands on him; then he goes on (by request).
    ///
    /// THE END (GildedKnight.End): by request, he falls to his knees first, and perishes into
    /// light.
    ///
    /// Nothing of his hurts by touch. His blades and what they let go are worked out on each
    /// machine for its own player, as Foigh's are.
    /// </summary>
    [AutoloadBossHead]
    public partial class GildedKnight : ModNPC
    {
        public override string Texture => KnightRig.Folder + "Torso";

        /// <summary>His helm, for the map and the bar.</summary>
        public override string BossHeadTexture => KnightRig.Folder + "GildedKnight_Head_Boss";

        // ============================================================
        // NUMBERS
        // ============================================================

        /// <summary>His box: the body from the soles up, the plume left out.</summary>
        public const int Wide = 34;
        public const int Tall = 78;

        /// <summary>His life: 9780 in a Classic world by request, scaled up for Expert and Master as the game scales a boss, half as much again in Legends.</summary>
        public const int LifeClassic = 9780;
        public const int LifeExpert = 15648;
        public const int LifeMaster = 23472;
        public const float LegendsLife = 1.5f;

        public const int Defense = 16;

        /// <summary>Everything he does, a quarter harder in Legends.</summary>
        public const float LegendsDamage = 1.25f;

        /// <summary>
        /// What each kind of blow does, in a Classic, an Expert and a Master world, before the game's
        /// variation. (Fifteen in a hundred less than they were first, by request: a cut was 46.)
        /// </summary>
        public static readonly int[] BladeDamage = { 39, 60, 75 };
        public static readonly int[] HeavyDamage = { 48, 71, 88 };
        public static readonly int[] ShockDamage = { 29, 43, 53 };
        public static readonly int[] CrescentDamage = { 31, 46, 56 };
        public static readonly int[] LancetDamage = { 37, 56, 70 };
        public static readonly int[] VotiveDamage = { 26, 39, 48 };
        public static readonly int[] PyreDamage = { 34, 51, 63 };
        public static readonly int[] GreatDamage = { 51, 77, 94 };
        public static readonly int[] ProcessionDamage = { 32, 48, 60 };
        public static readonly int[] LongDamage = { 43, 65, 80 };
        public static readonly int[] SunDamage = { 60, 89, 111 };

        /// <summary>The damage from one of the tables above, for the world as it is.</summary>
        public static int Damage(int[] table)
        {
            int d = table[Main.masterMode ? 2 : Main.expertMode ? 1 : 0];
            return PADWorld.Legends ? (int)Math.Round(d * LegendsDamage) : d;
        }

        /// <summary>His whole life in the world as it is.</summary>
        public static int FullLife()
        {
            int life = Main.masterMode ? LifeMaster : Main.expertMode ? LifeExpert : LifeClassic;
            return PADWorld.Legends ? (int)(life * LegendsLife) : life;
        }

        /// <summary>His change comes at this share of his life.</summary>
        public const float ChangeAt = 0.5f;

        // ============================================================
        // STATE
        // ============================================================

        public enum Act : byte
        {
            /// <summary>Put down, not yet seen: standing at his post.</summary>
            Post,

            /// <summary>His scene: the walk, the draw, the point, his name.</summary>
            Scene,

            /// <summary>In his guard between moves: stalking, waiting his moment.</summary>
            Stance,

            /// <summary>In a move (MoveId), its clock running.</summary>
            Move,

            /// <summary>Turned aside: a step back, a second of gold ward about him.</summary>
            Reel,

            /// <summary>His change at half his life.</summary>
            Change,

            /// <summary>His end.</summary>
            End,

            /// <summary>Nobody left in his field: back to his post, and gone.</summary>
            Leave,

            /// <summary>Every third parry: down on a knee for five seconds, open to the riposte.</summary>
            Stunned,
        }

        /// <summary>The moves, by number (sent).</summary>
        public enum Moves : byte
        {
            None,
            Twin3,
            Twin4,
            Twin4Delayed,
            Leap,
            Crescents,
            Crescents2,
            Wheel,
            Lancet,
            Votive3,
            Votive4,
            Votive5,
            BackStep,
            Dash,
            Great,
            Procession,
            Crown,
            Long,
            Sun,
        }

        public Act State
        {
            get => (Act)(int)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        /// <summary>Ticks in the state (and in a move, its clock, whole ticks of it).</summary>
        public int Timer
        {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        /// <summary>
        /// The clock itself, with what is between its ticks: in his moves and his change it runs at
        /// <see cref="MovePace"/>, so every pose, cut and cue of them comes that much later and the
        /// tracks are sampled smoothly between their ticks.
        /// </summary>
        public float Clock => NPC.ai[1];

        /// <summary>
        /// How fast his moves and his change play: eight tenths, by request ("Make every of his move
        /// slower by 20% so people can see the moves he make"): each a quarter as long again. His
        /// walking, his scene, being rocked, the stun and his end keep their own times.
        /// </summary>
        public const float MovePace = 0.8f;

        /// <summary>How far the clock goes in a tick in what he is doing now.</summary>
        private float Pace => State == Act.Move || State == Act.Change ? MovePace : 1f;

        public Moves MoveId
        {
            get => (Moves)(int)NPC.ai[2];
            set => NPC.ai[2] = (float)value;
        }

        public int TargetIndex
        {
            get => (int)NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        // ---- sent with him (SendExtraAI) ----

        /// <summary>Which way he faces: 1 or -1.</summary>
        private int facing = -1;

        /// <summary>His second half: from his change on.</summary>
        private bool second;

        /// <summary>The move's own number: the way the lancet goes, the ticks a leap takes in the air.</summary>
        private float arg;

        /// <summary>A leap: where it leaves from and where it lands (his feet), and how high it goes.</summary>
        private Vector2 leapFrom;
        private Vector2 leapTo;
        private float leapHeight;

        /// <summary>Ticks to wait in his guard before the next move.</summary>
        private int rest;

        /// <summary>A move to go straight into from the stance, without the wait (the crown's dash and cut).</summary>
        private Moves queued;

        /// <summary>The last two moves, so the next is not the same again.</summary>
        private Moves last1;
        private Moves last2;

        /// <summary>His field: its walls (x, world pixels), and his post at the wall's foot.</summary>
        private float fieldLeft;
        private float fieldRight;
        private Vector2 post;

        /// <summary>The blade parried, for the reel.</summary>
        private int reelBlade;

        /// <summary>The sun's place, for the gilded sun (where it comes down).</summary>
        private Vector2 sunAt;

        /// <summary>Called back (the spur): not standing at his post when his scene began, so he is not seen until the black.</summary>
        private bool called;

        // ---- every machine's own ----

        /// <summary>
        /// The furthest tick of the state this machine has played, to know when a tick of a move has
        /// just been reached (and never twice: a clock put back a tick by the server plays nothing again).
        /// </summary>
        private int was = -1;
        private Act wasState;
        private Moves wasMove;

        /// <summary>The state he was in before this one, on this machine (how he comes into it).</summary>
        private Act cameFrom;

        /// <summary>The move being played, made on this machine from its number and its arg.</summary>
        private Move move;
        private Moves moveMade;
        private float argMade;

        /// <summary>The pose he is in this tick, and the one he was in when the move began (it is gone into from there).</summary>
        public KnightRig.Pose Pose = KnightMoves.Rest;
        private KnightRig.Pose from = KnightMoves.Rest;

        /// <summary>Where the move's travel had him a tick ago.</summary>
        private Vector2 travelled;

        /// <summary>How far he has walked, in steps (the walk's gait), and the stance's.</summary>
        private float gait;

        /// <summary>The cuts he has already landed on this machine's player in this move, one bit each (so a blade lands once a cut).</summary>
        private int struck;

        /// <summary>Gone for good: the end is over.</summary>
        private bool gone;

        /// <summary>The cloth, stepped each tick on every machine that draws him.</summary>
        internal readonly KnightCloth Cape = new KnightCloth(8, 6.6f, 0.42f, 0.84f, 0.05f);
        internal readonly KnightCloth FrontFlap = new KnightCloth(3, 7f, 0.35f, 0.85f, 0.2f);
        internal readonly KnightCloth BackFlap = new KnightCloth(3, 7f, 0.35f, 0.85f, 0.2f);
        internal readonly KnightCloth Plume = new KnightCloth(5, 5.6f, 0.25f, 0.88f, 0.07f, 0.35f);

        /// <summary>How far each scabbard swings on its strap, and how fast.</summary>
        internal float SunSwing, GraceSwing;
        private float sunSwingV, graceSwingV, lastVx;

        private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>The ground between his feet.</summary>
        public Vector2 Feet => new Vector2(NPC.position.X + NPC.width * 0.5f, NPC.position.Y + NPC.height);

        public int Faces => facing;

        /// <summary>The way he is seen to face: past edge on in a turn about (a negative turn), the other way.</summary>
        public int SeenFaces => Pose.Turn < 0f ? -facing : facing;

        public bool Second => second;

        /// <summary>He is fighting: from the end of his scene to his end.</summary>
        public bool Fighting => State == Act.Stance || State == Act.Move || State == Act.Reel || State == Act.Change || State == Act.Stunned;

        /// <summary>Whether this tick of the state has just been reached, on this machine.</summary>
        private bool At(int tick) => was < tick && Timer >= tick;

        // ============================================================
        // SETUP
        // ============================================================

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);

            // Drawn from pieces: the bestiary's portrait is a picture of him standing.
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers
            {
                CustomTexturePath = KnightRig.Folder + "GildedKnight_Bestiary",
                PortraitPositionYOverride = 0f,
            };

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;

            // His arrival is his own, and his name comes up at his own moment, the bar with it.
            BossPlate.HasOwnIntro(Type);
            BossPlate.NamesItself(Type);
            BossPlate.BarWaitsForName(Type);
            BossPlate.Colours(Type, new Color(255, 214, 120), new Color(236, 226, 200));

            // His blades can be turned aside, every cut of them (by request); what that does to
            // him is his own (Turned): a step back with a ward of gold, or, every third time by
            // the parry's own count for anything on the ground, down on a knee for five seconds,
            // open to the riposte (by request: "like normal parry system"). His hits are worked
            // out here, not by the game's touch, so each asks the parry itself before it lands
            // (Strike).
            Parry.AddNPC(Type,
                npc => npc.ModNPC is GildedKnight knight && knight.CanBeTurned,
                (npc, by, away, staggers) => (npc.ModNPC as GildedKnight)?.Turned(staggers),
                flies: false,
                told: "Every cut of his blades is turned aside: he rocks back a step and a half with a ward of gold about him, and for that second nothing lands on him. Every third time he is down on a knee for five seconds, open to the riposte.",
                reach: LongReach + 160f);
        }

        public override void SetDefaults()
        {
            NPC.width = Wide;
            NPC.height = Tall;

            NPC.aiStyle = -1;

            // His life is set here for the world he is in, and nowhere else: the game leaves an
            // NPC that does nothing by touch as it finds it (as Foigh's file says).
            NPC.lifeMax = FullLife();
            NPC.damage = 0;
            NPC.defense = Defense;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.npcSlots = 10f;
            NPC.value = Item.buyPrice(gold: 8);
            NPC.netAlways = true;
            NPC.lavaImmune = true;

            // Nothing lands on him until his scene is over.
            NPC.dontTakeDamage = true;

            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = null;

            NPC.PAD().BossName = "Gilded Knight of Grace";
            NPC.PAD().BossTitle = "Who Held the Wall";
            NPC.PAD().OwnIntro = true;

            Music = -1;
            SceneEffectPriority = SceneEffectPriority.BossMedium;
        }

        /// <summary>Not asked of him as he is (see SetDefaults); if it ever is, his life is still the world's.</summary>
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = FullLife();
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("Mods.PisAndDag.NPCs.GildedKnight.Bestiary"),
            });
        }

        // He goes when the fight says, not when vanilla's despawn would.
        public override bool CheckActive() => false;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        // Nothing lands on him while he is untouchable, and nothing from anyone outside his field:
        // it is a fight between him and whoever was on it (worked out on the striker's own machine).
        public override bool? CanBeHitByItem(Player player, Item item) => NPC.dontTakeDamage || !Fights(player) ? false : null;

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (NPC.dontTakeDamage)
                return false;

            if (projectile.friendly && projectile.owner >= 0 && projectile.owner < Main.maxPlayers && !Fights(Main.player[projectile.owner]))
                return false;

            return null;
        }

        /// <summary>A blow that would kill him leaves him on one, and his end begins; it sets him to nothing itself at the last of it.</summary>
        public override bool CheckDead()
        {
            if (gone)
                return true;

            NPC.life = 1;
            NPC.dontTakeDamage = true;

            if (Authority && State != Act.End)
                Go(Act.End);

            return false;
        }

        /// <summary>Nothing burns him while nothing can hurt him.</summary>
        public override void UpdateLifeRegen(ref int damage)
        {
            if (!NPC.dontTakeDamage)
                return;

            if (NPC.lifeRegen < 0)
                NPC.lifeRegen = 0;

            damage = 0;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // What calls him back, every time (by request: "resummon with an item dropped from him").
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GildedSpur>()));

            // His sword and his sun, every time (by request: "He drops Sunburst Talisman ... and
            // Sword Of A Gilded Knight").
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Melee.SwordOfAGildedKnight>()));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Accessories.SunburstTalisman>()));
        }

        public override void BossLoot(ref int potionType)
        {
            potionType = ItemID.HealingPotion;
        }

        public override void OnKill()
        {
            if (!PADWorld.DownedGildedKnight)
            {
                PADWorld.DownedGildedKnight = true;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.WorldData);
            }
        }

        // ============================================================
        // SYNC
        // ============================================================

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((sbyte)facing);
            writer.Write(second);
            writer.Write(arg);
            writer.WriteVector2(leapFrom);
            writer.WriteVector2(leapTo);
            writer.Write(leapHeight);
            writer.Write((short)Math.Clamp(rest, short.MinValue, short.MaxValue));
            writer.Write((byte)queued);
            writer.Write((byte)last1);
            writer.Write((byte)last2);
            writer.Write(fieldLeft);
            writer.Write(fieldRight);
            writer.WriteVector2(post);
            writer.Write((byte)reelBlade);
            writer.WriteVector2(sunAt);
            writer.Write(gone);
            writer.Write(called);
            writer.Write(walkSteps);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            facing = reader.ReadSByte();
            second = reader.ReadBoolean();
            arg = reader.ReadSingle();
            leapFrom = reader.ReadVector2();
            leapTo = reader.ReadVector2();
            leapHeight = reader.ReadSingle();
            rest = reader.ReadInt16();
            queued = (Moves)reader.ReadByte();
            last1 = (Moves)reader.ReadByte();
            last2 = (Moves)reader.ReadByte();
            fieldLeft = reader.ReadSingle();
            fieldRight = reader.ReadSingle();
            post = reader.ReadVector2();
            reelBlade = reader.ReadByte();
            sunAt = reader.ReadVector2();
            gone = reader.ReadBoolean();
            called = reader.ReadBoolean();
            walkSteps = reader.ReadByte();
        }

        // ============================================================
        // BEING PUT DOWN
        // ============================================================

        /// <summary>
        /// Put down at his post, his field from <paramref name="left"/> to <paramref name="right"/>
        /// (world pixels), facing the way he will walk: where the world is decided, right after
        /// NPC.NewNPC. Standing his watch there until someone comes up the field
        /// (<paramref name="scene"/> false), or his scene at once (called back by the spur: he is
        /// not seen until the picture is black).
        /// </summary>
        public void Place(Vector2 feet, float left, float right, int faces, int target, bool scene)
        {
            post = feet;
            fieldLeft = left;
            fieldRight = right;
            facing = faces >= 0 ? 1 : -1;
            TargetIndex = target;
            called = scene;
            NPC.position = new Vector2(feet.X - NPC.width * 0.5f, feet.Y - NPC.height);

            if (scene)
            {
                MeasureWalk(Target());
                Go(Act.Scene);
            }
            else
            {
                Go(Act.Post);
                NPC.netUpdate = true;
            }
        }

        /// <summary>Someone has come up the field to him: his scene, for them (where the world is decided).</summary>
        public void Meet(int target)
        {
            if (!Authority || State != Act.Post)
                return;

            TargetIndex = target;
            MeasureWalk(Target());
            Go(Act.Scene);
        }

        /// <summary>His field, x in the world (pixels), and his post.</summary>
        public float FieldLeft => fieldLeft;
        public float FieldRight => fieldRight;
        public Vector2 Post => post;

        // ============================================================
        // THE TICK
        // ============================================================

        public override void AI()
        {
            // His field, every tick, on every machine, from his scene to his end: nobody leaves
            // it and nobody comes into it (by request, Dame Quilt's box, sealed).
            if (fieldRight > fieldLeft && State != Act.Post && State != Act.Leave && !gone)
                BossArena.Keep(fieldLeft, fieldRight, true, post.Y - FieldAbove, post.Y + FieldBelow);

            // The bar is the fight's. The music too: the world's own while he stands his watch,
            // nothing but his armour through his scene, his theme through the fight, and quiet as
            // he kneels and goes.
            NPC.boss = State != Act.Post && State != Act.Leave && !Apart;
            Music = State == Act.Post || State == Act.Leave ? -1 : State == Act.Scene || State == Act.End ? 0 : MusicID.OtherworldlyBoss1;

            // Whether anything lands on him, every tick on every machine: the game's sync of an NPC
            // does not carry it, so each machine works it out from his state as the server does.
            NPC.dontTakeDamage = gone || Untouchable(State);

            before = Pose;

            // A new state or move, on this machine: its clock starts again. (A clock the server
            // puts back a few ticks is the same move: nothing in it is played again.)
            if (State != wasState || MoveId != wasMove || Timer < was - 8)
            {
                if (State != wasState)
                    cameFrom = wasState;

                // His end from where a stun left him: kneeling already, it goes on from its first knee.
                if (State == Act.End)
                    endFrom = cameFrom == Act.Stunned ? KnightMoves.EndKnees - 60 : 0;

                was = -1;
                wasState = State;
                wasMove = MoveId;
                struck = 0;
                from = Pose;
                travelled = Vector2.Zero;

                // A turn about he had not finished in his guard is not finished after: what comes next starts from how he stands.
                turning = 0;
                Restarted();
            }

            // A client that comes back to him a long way into something (he was out of its range,
            // or its clock was put a long way back) plays what is happening now, not every cue it
            // missed in one tick.
            if (!Authority && Timer - was > CatchUp)
                was = Timer - 1;

            Player target = Target();

            switch (State)
            {
                case Act.Post:
                    Stand();
                    break;

                case Act.Scene:
                    SceneTick(target);
                    break;

                case Act.Stance:
                    StanceTick(target);
                    break;

                case Act.Move:
                    MoveTick(target);
                    break;

                case Act.Reel:
                    ReelTick(target);
                    break;

                case Act.Change:
                    ChangeTick(target);
                    break;

                case Act.End:
                    EndTick();
                    break;

                case Act.Leave:
                    LeaveTick();
                    break;

                case Act.Stunned:
                    StunnedTick(target);
                    break;
            }

            seenFacing = facing;

            KeepToField();

            // Up a step of ground as a man walks up one, rather than stopped at its foot.
            if (!NPC.noGravity && !NPC.noTileCollide && NPC.velocity.X != 0f)
                Collision.StepUp(ref NPC.position, ref NPC.velocity, NPC.width, NPC.height, ref NPC.stepSpeed, ref NPC.gfxOffY);

            Hang();
            Glow();
            Looks();

            was = Math.Max(was, Timer);
            NPC.ai[1] += Pace;
        }

        /// <summary>
        /// After his AI, whether or not it ran: a stagger that stops it (the Greatbow's) does not stop
        /// this, so his box stands through it.
        /// </summary>
        public override void PostAI()
        {
            if (fieldRight > fieldLeft && State != Act.Post && State != Act.Leave && !gone)
                BossArena.Keep(fieldLeft, fieldRight, true, post.Y - FieldAbove, post.Y + FieldBelow);
        }

        /// <summary>His head off the map and the bar once he has come apart.</summary>
        public override void BossHeadSlot(ref int index)
        {
            if (Apart || gone)
                index = -1;
        }

        /// <summary>Moves on to another state (or a move), where the world is decided.</summary>
        private void Go(Act act, Moves which = Moves.None, float with = 0f)
        {
            if (!Authority)
                return;

            // Stunned no longer, whatever he goes on to: nothing is open to a riposte at his end.
            if (State == Act.Stunned && act != Act.Stunned)
                NPC.GetGlobalNPC<PADStaggerNPC>().Wake(NPC);

            // His end is silent and his own (by request): what he let go of that is still about goes with
            // it, quietly (their ends are light, not sound).
            if (act == Act.End && State != Act.End)
            {
                foreach (Projectile projectile in Main.ActiveProjectiles)
                {
                    if (projectile.ModProjectile is KnightShot || projectile.ModProjectile is KnightProcession)
                        projectile.Kill();
                }
            }

            State = act;
            MoveId = which;
            arg = with;
            Timer = 0;

            NPC.dontTakeDamage = Untouchable(act);
            NPC.netUpdate = true;
        }

        /// <summary>Nothing lands on him at his post, in his scene, turned aside, in his change, at his end, leaving. (Stunned, everything does: the riposte above all.)</summary>
        private static bool Untouchable(Act act) => act != Act.Stance && act != Act.Move && act != Act.Stunned;

        /// <summary>The most ticks a client plays at once of what it has missed of him.</summary>
        private const int CatchUp = 6;

        /// <summary>How far his walls stop short of his field's ends: never a foot past them.</summary>
        private const float WallMargin = 24f;

        /// <summary>
        /// His field's height about his post (world pixels): who is far over or under it as it
        /// stands up (in a cave under the flat) has nothing to do with the fight. Wider than the
        /// band KnightEncounter finds a player on the field in (eighteen rows over the plain's level
        /// to ten under it), so that band is inside this one wherever the ground at his post is
        /// within twelve rows of that level: whoever brought him on is always in his field.
        /// </summary>
        private const float FieldAbove = 30 * 16f;
        private const float FieldBelow = 22 * 16f;

        /// <summary>Nothing he does takes him out of his field: whatever would carry him past a wall stops at it.</summary>
        private void KeepToField()
        {
            if (!(fieldRight > fieldLeft) || NPC.noTileCollide)
                return;

            float x = Feet.X + NPC.velocity.X;

            if ((x < fieldLeft + WallMargin && NPC.velocity.X < 0f) || (x > fieldRight - WallMargin && NPC.velocity.X > 0f))
                NPC.velocity.X = 0f;
        }

        /// <summary>His fight is over for him: kneeling and going, gone, or leaving his field. What he let go of goes with it.</summary>
        public bool Over => gone || State == Act.End || State == Act.Leave || !NPC.active;

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

        /// <summary>
        /// Whether a player is in his field (and alive). While his box stands, it says: whoever it
        /// holds, however high they go, between its walls as their own machine keeps them. Before it
        /// stands (the first tick of his scene): on the flat between his walls, not in a cave under
        /// it or high over it.
        /// </summary>
        private bool InField(Player player)
        {
            if (player == null || !player.active || player.dead)
                return false;

            if (!(fieldRight > fieldLeft))
                return true;

            bool between = player.Center.X >= fieldLeft && player.Center.X <= fieldRight;

            if (BossArena.Up && BossArena.Sealed)
                return between && player.GetModPlayer<BossArenaPlayer>().Held;

            return between && player.Bottom.Y >= post.Y - FieldAbove && player.Bottom.Y <= post.Y + FieldBelow;
        }

        /// <summary>
        /// Whether a player is in the fight: held in his box while it stands; nobody, once it is down
        /// (he is leaving, and what he let go of hurts nobody after). Asked on any machine of any
        /// player; each machine keeps every player's place in the box (BossArenaPlayer).
        /// </summary>
        internal static bool Fights(Player player)
        {
            if (player == null || !player.active)
                return false;

            return BossArena.Up && BossArena.Sealed && player.GetModPlayer<BossArenaPlayer>().Held;
        }

        /// <summary>Picks who to fight: the one he has, while they are alive in his field; else the nearest who is.</summary>
        private Player Retarget()
        {
            Player now = Target();

            if (InField(now))
                return now;

            int best = -1;
            float bestDist = float.MaxValue;

            foreach (Player player in Main.ActivePlayers)
            {
                if (!InField(player))
                    continue;

                float d = Vector2.Distance(player.Center, NPC.Center);

                if (d < bestDist)
                {
                    bestDist = d;
                    best = player.whoAmI;
                }
            }

            if (best >= 0 && best != TargetIndex && Authority)
            {
                TargetIndex = best;
                NPC.netUpdate = true;
            }

            return best >= 0 ? Main.player[best] : null;
        }

        private void Face(int way)
        {
            if (!Authority || way == 0 || way == facing)
                return;

            facing = way;
            NPC.netUpdate = true;
        }

        /// <summary>The way to them, 1 or -1, or the way he faces when they are right over him.</summary>
        private int WayTo(Player target)
        {
            if (target == null)
                return facing;

            float dx = target.Center.X - NPC.Center.X;
            return Math.Abs(dx) < 4f ? facing : (dx > 0f ? 1 : -1);
        }

        // ============================================================
        // STANDING, AND HIS CLOTH
        // ============================================================

        /// <summary>
        /// At his post, as he is found: still, his swords home, his near hand on the Sun's hilt, a
        /// breath in him, facing down the field. If nobody is anywhere near any more, he is not
        /// there either (KnightEncounter puts him back when someone comes).
        /// </summary>
        private void Stand()
        {
            KnightRig.Pose pose = KnightMoves.Rest;
            float b = (float)Math.Sin(Main.GameUpdateCount * 0.04f + NPC.whoAmI);

            pose.Nod += 0.025f * b;
            pose.Lean += 0.01f * b;
            pose.Rise = -0.4f + 0.4f * b;
            Pose = pose;

            NPC.velocity.X = 0f;
            NPC.dontTakeDamage = true;

            if (!Authority || Timer % 60 != 0)
                return;

            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead && Vector2.Distance(player.Center, NPC.Center) < KnightEncounter.GoneFrom)
                    return;
            }

            NPC.active = false;
            NPC.netSkip = -1;
            NPC.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (Main.dedServ || NPC.life <= 0)
                return;

            KnightFx.Hit(NPC, hit.HitDirection, hit.Damage > NPC.lifeMax / 60);
        }

        /// <summary>
        /// His cloth and his scabbards, every tick on every machine that draws him: the cape, the
        /// tabard's flaps and the plume step after where the frame puts their roots, the
        /// scabbards swing on their straps with how he moves.
        /// </summary>
        private void Hang()
        {
            if (Main.dedServ)
                return;

            KnightRig.Frame frame = KnightRig.Build(Pose, Feet, facing, null, SunSwing, GraceSwing);

            float ground = Feet.Y;

            // The wind through the willows, and the air he moves through.
            float wind = Main.windSpeedCurrent * 0.05f + 0.02f * (float)Math.Sin(Main.GameUpdateCount * 0.045f);
            Vector2 push = new Vector2(wind, 0f);

            // Kept to his back as he is seen: past edge on in a turn about, he is seen the other way.
            int seen = SeenFaces;

            Cape.Step(frame.CapeRoot, MathHelper.PiOver2, push, ground + 1f);
            Cape.KeepBehind(frame.CapeRoot.X, seen, 1f, 4f);
            BackFlap.Step(frame.BackRoot, MathHelper.PiOver2, push * 0.5f, ground + 1f);
            FrontFlap.Step(frame.FrontRoot, MathHelper.PiOver2, push * 0.5f, ground + 1f);
            FrontFlap.KeepOff(frame.Hips, frame.NearSole, -seen, 2f);
            FrontFlap.KeepOff(frame.Hips, frame.FarSole, -seen, 2f);
            BackFlap.KeepOff(frame.Hips, frame.NearSole, seen, 2f);
            BackFlap.KeepOff(frame.Hips, frame.FarSole, seen, 2f);
            Plume.Step(frame.PlumeRoot, frame.PlumeAngle, push * 1.6f, ground);

            float vx = NPC.velocity.X * facing;
            float ax = vx - lastVx;
            lastVx = vx;

            sunSwingV += -SunSwing * 0.05f - ax * 0.02f;
            sunSwingV *= 0.86f;
            SunSwing = MathHelper.Clamp(SunSwing + sunSwingV, -0.25f, 0.2f);
            graceSwingV += -GraceSwing * 0.06f - ax * 0.02f;
            graceSwingV *= 0.84f;
            GraceSwing = MathHelper.Clamp(GraceSwing + graceSwingV, -0.25f, 0.2f);
        }

        /// <summary>He gives off a little gold light of his own, more in his second half.</summary>
        private void Glow()
        {
            if (State == Act.Post || gone || Apart)
                return;

            Vector3 gold = new Vector3(1f, 0.78f, 0.36f) * (second ? 0.75f : 0.45f);
            Lighting.AddLight(NPC.Center, gold);
        }

        // ============================================================
        // NOBODY LEFT
        // ============================================================

        public const int LeaveTicks = 90;

        /// <summary>Nobody left in his field: he lowers his blades, and fades out where he stands; the wall keeps its watch.</summary>
        private void LeaveTick()
        {
            NPC.velocity.X = 0f;
            Pose = KnightTrack.Between(from, KnightMoves.Guard, Math.Min(1f, Timer / 30f));
            NPC.alpha = (int)MathHelper.Clamp(Timer / (float)LeaveTicks * 255f, 0f, 255f);

            if (!Authority || Timer < LeaveTicks)
                return;

            NPC.active = false;
            NPC.netSkip = -1;
            NPC.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }
    }
}
