using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items.LoreItem;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// Dona Quinn, the Horseback Fool, on Triunfo her donkey. In game she is 'Dame' Quilt (the
    /// quotes are hers, by request); in her own lines she says it straight, Dame Quilt.
    ///
    /// She stops the player on the Cleared Ground (the field in front of the Wasted Mile
    /// where every tree is a stump), once the Eye has been seen off by Pugream and before
    /// the world goes hard. If it is night or raining the world is run on to eight in the
    /// morning first, clear. The world stops, the box comes up with a black face in it, and
    /// she introduces herself; whatever the player answers, she calls them a coward and
    /// comes with the lance (DonaQuinnEncounter starts all of this).
    ///
    /// THE FIGHT. 1500 life in every world, flat; 10 defence; she does nothing by touch.
    /// Anything off the lance goes through armour (it ignores defence) and is varied the way
    /// vanilla varies any hit (Main.DamageVar), the same in every world. Four things she does:
    ///
    ///   THE CHARGE. At them on Triunfo, the lance couched and aimed at them as she comes: 30
    ///   at the slowest a strike counts, 67 flat out, by how fast he is going. She rides on
    ///   until she is well past where they are now, pulls up, Triunfo rears with his front
    ///   legs going round, turns round to face them, and she comes again.
    ///   THE THROW. She stops, draws back and throws the lance at them; it is heavy, comes down
    ///   in an arc and goes into the ground point first (52, DonaLanceThrown). Then Triunfo runs
    ///   them down without it (15 to 40 by his speed, ordinary damage), and she rides back for
    ///   it and pulls it out of the ground as she goes by.
    ///   THE SWING. She comes in close (the crowd-control tell, CCBypass, from the moment she
    ///   turns for them) and swings the lance up and then down, like the Visus's chop: 40, and
    ///   whatever shield they have, the upswing throws them fifteen tiles up and twenty away,
    ///   the downswing flattens them, and either way they are down a second after they land
    ///   (RagdollPlayer).
    ///   THE CRASH. A charge that ends in a wall Triunfo cannot jump (a cliff, a living tree)
    ///   ends in it: 100 off her, flat, and "Ouchh!".
    ///
    /// Triunfo has sense about the ground (DonkeyNav): he jumps what he can, backs off for a
    /// run at a bank, leaps gaps, pulls up at cliffs and deep water, and swims. Now and then
    /// she shouts something, over her head, not in a box (Bark). Triunfo shares her life:
    /// hitting him hits her.
    ///
    /// THE BOX. From the road until she is thrown, the fight is boxed in (BossArena): two
    /// walls nobody sees, half the Cleared Ground apart, their middle halfway between the
    /// player and where she was put down. The player cannot walk past one, their camera will
    /// not show past one, and inside them nothing is built, broken or blown up (Boss Active).
    /// She and Triunfo are not held by it: a charge still carries her on past a player with
    /// their back to a wall, off their screen, and she comes back.
    ///
    /// AT ONE. She cannot be killed by a blow. When one would, she is left on one and
    /// Triunfo rears again and shakes her off; the lance goes up end over end and comes
    /// down somewhere (DonaLanceGore); she goes over the front and comes down in a heap
    /// that settles face down, the way DonnLyingdownRef.png has her: arm out in front and
    /// a little down, head down. Triunfo bolts. She bleeds a little and shakes now and
    /// then. The player walks up to her on their own, the camera close; she lifts her
    /// head and begs, and the two answers sit in the box's two corners.
    ///
    /// FINISH. A cut, like the King's but smaller, and she is gone; she leaves a locket.
    /// SPARE. She gets up, still bleeding, still shaking now and then, and talks; then she
    /// stays where she is, looking at the player or at the ground, turning about now and
    /// then as if she had a mind of her own, until nobody can see her, and is gone. Either
    /// way it never happens again in that world. Triunfo comes back and stands by her, or
    /// by where she was.
    ///
    /// THE PIECES. Drawn as a rig, not a sheet (DonaRig): the riding body, the arm, the
    /// lance, the head and the hair, or the four standing pieces; the head turns a little
    /// toward the player, and only ever on the side she faces: to look at someone behind
    /// her, the whole of her turns round.
    ///
    /// Partial: this file is the body of it (numbers, phases, sync, hooks); .Fight is the
    /// saddle, .Fall is everything from the fall on, .Scene is what the player is shown
    /// and asked, .Draw is the drawing.
    /// </summary>
    [AutoloadBossHead]
    public partial class DonaQuinn : ModNPC
    {
        public override string Texture => DonaRig.Folder + "Idle";

        /// <summary>Her face and hair, for the map. Made from her own Idle_Face and Idle_Hair.</summary>
        public override string BossHeadTexture => DonaRig.Folder + "DonaQuinn_Head_Boss";

        // ============================================================
        // NUMBERS
        // ============================================================

        /// <summary>The same in every world: no difficulty, no player count, no Legends grows it.</summary>
        public const int Life = 1500;

        public const int Defense = 10;

        /// <summary>The lance in a charge, through armour: at the slowest a strike counts, and flat out. Varied as vanilla varies any hit.</summary>
        public const int LanceLeast = 30;
        public const int LanceMost = 67;

        /// <summary>The thrown lance and the swing, through armour; Triunfo running them down, ordinary, slowest to flat out.</summary>
        public const int ThrowDamage = 52;
        public const int SwingDamage = 40;
        public const int RamLeast = 15;
        public const int RamMost = 40;

        /// <summary>Off her, flat, for a charge into a wall.</summary>
        public const int CrashDamage = 100;

        /// <summary>How fast she talks: quick. She is excited.</summary>
        public const float Pace = 1.1f;

        /// <summary>Above the middle of her body, where her barks sit.</summary>
        private const float BarkAbove = 34f;

        // ============================================================
        // PHASES
        // ============================================================

        public enum Phase
        {
            /// <summary>On the road: the world stopped, the box up, her name.</summary>
            Intro,

            /// <summary>The player has answered. "Coward!" and the lance comes down.</summary>
            Challenge,

            /// <summary>At them, flat out, the lance couched.</summary>
            Charge,

            /// <summary>Past them; pulling up.</summary>
            Brake,

            /// <summary>Triunfo up on his hind legs, front legs going round.</summary>
            Rear,

            /// <summary>Flipped round to face them: a beat.</summary>
            Turn,

            /// <summary>A breath, and the next thing she does is picked.</summary>
            Ready,

            /// <summary>Stopped, drawing back, and the lance thrown.</summary>
            Wind,

            /// <summary>Without it: Triunfo runs them down.</summary>
            Ram,

            /// <summary>Back for the lance, and out of the ground with it.</summary>
            Fetch,

            /// <summary>In close for the swing.</summary>
            Close,

            /// <summary>The lance swung up, and down.</summary>
            Swing,

            /// <summary>Into a wall.</summary>
            Crash,

            /// <summary>Somewhere she can throw from, at a player she cannot get to.</summary>
            Range,

            /// <summary>At one: Triunfo rears and shakes her off.</summary>
            Buck,

            /// <summary>Off, through the air, and down in a heap.</summary>
            Thrown,

            /// <summary>Face down. The player is walked over to her.</summary>
            Lying,

            /// <summary>Head up, begging: Spare, or Finish.</summary>
            Plead,

            /// <summary>Spared: up off the ground.</summary>
            GetUp,

            /// <summary>Spared: talking.</summary>
            Talk,

            /// <summary>Spared: left alone, until nobody is looking.</summary>
            Free,

            /// <summary>Finished.</summary>
            Finish,

            /// <summary>The player fell, or went: she rides off.</summary>
            Leave,
        }

        // ai[0] Phase · ai[1] ticks in it · ai[2] Triunfo's index + 1 · ai[3] the player she stopped

        public Phase State
        {
            get => (Phase)(int)NPC.ai[0];
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

        private int DonkeyIndex
        {
            get => (int)NPC.ai[2] - 1;
            set => NPC.ai[2] = value + 1;
        }

        public int TargetIndex
        {
            get => (int)NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        // ---- sent with her (SendExtraAI) -----------------------------------------------

        /// <summary>Which way the charge goes, decided when it starts and kept.</summary>
        private int chargeDir = 1;

        /// <summary>Where the charge is past the player, x in the world: she pulls up there.</summary>
        private float passX;

        /// <summary>Which way she faces off the donkey.</summary>
        private int facing = 1;

        /// <summary>The lance is in her hand (not thrown and still in the ground somewhere), and where it is if not.</summary>
        private bool hasLance = true;
        private float lanceX;

        /// <summary>
        /// The middle of her box, x in the world: halfway between the player and where she was
        /// put down, as they stood then. None while it is 0.
        /// </summary>
        private float boxX;

        /// <summary>What she was doing last tick: a change is the first tick of the new phase, wherever it was decided.</summary>
        private Phase seen = (Phase)(-1);

        /// <summary>
        /// Whether this is the first tick this machine has seen of the phase it is in: true on
        /// the tick after the server moved her on, and on a client the tick the news arrives.
        /// Read through the tick; set once at its start.
        /// </summary>
        private bool fresh;

        /// <summary>Riding phases: drawn on Triunfo, hitbox on her seat.</summary>
        public bool Mounted => State <= Phase.Buck || State == Phase.Leave;

        /// <summary>
        /// Drawn in the saddle: while she rides, and for the one picture between being thrown
        /// and this machine starting the throw (BeginThrow, on her first tick of Thrown), so
        /// she is never seen for a frame standing up on his back.
        /// </summary>
        public bool InSaddle => Mounted || (State == Phase.Thrown && seen != Phase.Thrown);

        private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        // ============================================================
        // SETUP
        // ============================================================

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.MPAllowedEnemies[Type] = true;

            // She is drawn from pieces; the bestiary's portrait would be a girl with no arms.
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };

            // She names herself, in her own words, at her own moment, and the bar comes up
            // with it (Scene: BossIntro.PlayNameOnly when her name is out).
            BossPlate.HasOwnIntro(Type);
            BossPlate.NamesItself(Type);
            BossPlate.BarWaitsForName(Type);

            // Her face in the box: the standing face and hair, the head's part of the canvas.
            DialoguePortrait.Register(Type, new[] { DonaRig.Folder + "Idle_Face", DonaRig.Folder + "Idle_Hair" },
                new Rectangle(12, 12, 22, 20));

            DialoguePortrait.Name(Type, null, "the Horseback Fool");

            // The tab says 'Dame' Quilt; she says it without the quotes, and that is when she is named.
            DialoguePortrait.SpokenAs(Type, "Dame Quilt");
        }

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 40;

            NPC.aiStyle = -1;
            NPC.lifeMax = Life;
            NPC.damage = 0;
            NPC.defense = Defense;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.npcSlots = 5f;
            NPC.value = 0f;
            NPC.netAlways = true;

            // Nothing lands on her until she has said who she is and come at them.
            NPC.dontTakeDamage = true;

            NPC.HitSound = SoundID.FemaleHit;
            NPC.DeathSound = null;

            NPC.PAD().BossName = "'Dame' Quilt";
            NPC.PAD().BossTitle = "The Horseback Fool";
            NPC.PAD().OwnIntro = true;

            Music = MusicID.Boss1;
            SceneEffectPriority = SceneEffectPriority.BossLow;
        }

        /// <summary>Flat, by request: whatever the world, whoever is in it.</summary>
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = Life;
        }

        // She goes when the fight says, not when vanilla's despawn would.
        public override bool CheckActive() => false;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        /// <summary>
        /// A blow that would kill her leaves her on one, and Triunfo throws her. Only the cut
        /// at the end, which sets her to nothing itself, gets through.
        /// </summary>
        public override bool CheckDead()
        {
            if (State == Phase.Finish && cut)
                return true;

            NPC.life = 1;
            NPC.dontTakeDamage = true;

            if (Authority && Mounted && State != Phase.Buck && State != Phase.Leave)
            {
                State = Phase.Buck;
                NPC.netUpdate = true;
            }

            return false;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LocketOfABookCrazedGirl>()));
        }

        /// <summary>No potions off her. It was not that kind of fight.</summary>
        public override void BossLoot(ref int potionType)
        {
            potionType = ItemID.None;
        }

        public override void OnKill()
        {
            DonaQuinnEncounter.Settle(DonaQuinnEncounter.Finished);

            // He comes back to where she was.
            Donkey()?.ComeBack(NPC.Center.X);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        /// <summary>Nothing burns or poisons her while nothing can hurt her: the scenes are not a fight.</summary>
        public override void UpdateLifeRegen(ref int damage)
        {
            if (!NPC.dontTakeDamage)
                return;

            if (NPC.lifeRegen < 0)
                NPC.lifeRegen = 0;

            damage = 0;
        }

        /// <summary>
        /// One swing, one blow. A sword that catches her and then Triunfo a moment later in the
        /// same swing would take her life twice, since his is hers; so whichever of the two a
        /// swing lands on, the other is out of its reach for the rest of it, as vanilla keeps
        /// the one that was hit (NPC.immune, for as long as the swing has left).
        /// </summary>
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            ShareSwing(Donkey()?.NPC, player);
        }

        /// <summary>The same for anything thrown, fired or blown up: one shot, one blow, whichever of the two it reaches.</summary>
        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            ShareShot(Donkey()?.NPC, projectile);
        }

        internal static void ShareSwing(NPC other, Player player)
        {
            if (other == null || player.whoAmI < 0 || player.whoAmI >= other.immune.Length)
                return;

            other.immune[player.whoAmI] = Math.Max(other.immune[player.whoAmI], player.itemAnimation);
        }

        /// <summary>
        /// The other of the two made immune to this projectile as if it had just hit them too,
        /// the way Projectile.Damage makes the one it hit (1.4.4): by its own id's timer, by its
        /// own timer for each NPC, or for a shot that goes through things, the owner's ten
        /// ticks on the NPC. Never shortens a wait already running.
        /// </summary>
        internal static void ShareShot(NPC other, Projectile projectile)
        {
            if (other == null || !other.active)
                return;

            int i = other.whoAmI;
            bool lasting = projectile.penetrate != 1 || projectile.appliesImmunityTimeOnSingleHits;

            if (projectile.usesIDStaticNPCImmunity)
            {
                if (lasting)
                {
                    uint until = Main.GameUpdateCount + (uint)projectile.idStaticNPCHitCooldown;
                    uint[] waits = Projectile.perIDStaticNPCImmunity[projectile.type];
                    waits[i] = Math.Max(waits[i], until);
                }
            }
            else if (projectile.usesLocalNPCImmunity && projectile.localNPCHitCooldown != -2)
            {
                int wait = projectile.localNPCImmunity[i];

                if (wait != -1)
                    projectile.localNPCImmunity[i] = projectile.localNPCHitCooldown == -1 ? -1 : Math.Max(wait, projectile.localNPCHitCooldown);
            }
            else if (lasting && projectile.owner >= 0 && projectile.owner < other.immune.Length)
            {
                other.immune[projectile.owner] = Math.Max(other.immune[projectile.owner], 10);
            }
        }

        // ============================================================
        // SYNC
        // ============================================================

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((sbyte)chargeDir);
            writer.Write(passX);
            writer.Write((sbyte)facing);
            writer.Write(lookingDown);
            writer.Write(hasLance);
            writer.Write(lanceX);
            writer.Write(boxX);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            chargeDir = reader.ReadSByte();
            passX = reader.ReadSingle();
            facing = reader.ReadSByte();
            lookingDown = reader.ReadBoolean();
            hasLance = reader.ReadBoolean();
            lanceX = reader.ReadSingle();
            boxX = reader.ReadSingle();
        }

        // ============================================================
        // WHO
        // ============================================================

        /// <summary>Triunfo, if he is still there.</summary>
        public TriunfoTheDonkey Donkey()
        {
            int i = DonkeyIndex;

            if (i < 0 || i >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[i];
            return npc.active && npc.ModNPC is TriunfoTheDonkey donkey ? donkey : null;
        }

        /// <summary>The player she stopped, alive or not; null if they have left.</summary>
        public Player Target()
        {
            int i = TargetIndex;

            if (i < 0 || i >= Main.maxPlayers)
                return null;

            Player player = Main.player[i];
            return player.active ? player : null;
        }

        /// <summary>Whether this machine is the one whose player she stopped: the one that sees the scenes.</summary>
        public bool TargetIsHere => !Main.dedServ && TargetIndex == Main.myPlayer;

        /// <summary>
        /// Her box, its middle here: said once, by whoever put her down (DonaQuinnEncounter),
        /// and sent with her.
        /// </summary>
        internal void Box(float middle)
        {
            boxX = middle;
            NPC.netUpdate = true;
        }

        /// <summary>The server (or single player) moves her on.</summary>
        private void Go(Phase phase)
        {
            if (!Authority)
                return;

            State = phase;
            NPC.netUpdate = true;
        }

        // ============================================================
        // AI
        // ============================================================

        public override void AI()
        {
            TriunfoTheDonkey donkey = Donkey();

            // She never rides on without the donkey she came with. Gone from under her as she
            // is thrown, she falls anyway; at any other time she goes too.
            if (Mounted && donkey == null)
            {
                if (Authority)
                {
                    if (State == Phase.Buck)
                        Go(Phase.Thrown);
                    else
                        Vanish();
                }

                return;
            }

            // The bar is the fight's: up while she fights, gone from the fall.
            bool fighting = State < Phase.Buck || State == Phase.Leave;
            NPC.boss = fighting;

            // The music: hers through the fight, nothing at all from the fall to the end of
            // it, and the world's own once she is left alone.
            Music = fighting ? MusicID.Boss1
                : State == Phase.Free ? -1
                : 0;

            fresh = seen != State;
            seen = State;

            // Her box, for as long as it is a fight: asked for every tick, on every machine,
            // and gone by itself the tick after she stops asking (thrown, riding off, gone).
            if (boxX > 0f && State < Phase.Buck)
                BossArena.Keep(boxX - DonaQuinnEncounter.BoxHalf, boxX + DonaQuinnEncounter.BoxHalf);

            if (Mounted)
                Ride(donkey);
            else
                Fallen(donkey);

            Timer++;

            Visuals(donkey);

            if (TargetIsHere)
                Scene(donkey);
        }

        /// <summary>
        /// Gone, on every machine, and Triunfo with her if he is under her or out of sight; if
        /// somebody can see him he stays, and goes on his own when they cannot.
        /// </summary>
        private void Vanish()
        {
            if (!Authority)
                return;

            TriunfoTheDonkey donkey = Donkey();

            if (donkey != null && (donkey.Carrying || !Watched(donkey.NPC.Center)))
                donkey.Vanish();

            NPC.active = false;
            NPC.netSkip = -1;
            NPC.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        /// <summary>Whether anybody alive could see this point right now.</summary>
        public static bool Watched(Vector2 where)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead && Math.Abs(player.Center.X - where.X) <= 1100f && Math.Abs(player.Center.Y - where.Y) <= 700f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether this player's world is holding its breath for her: nothing new spawns
        /// round them (DonaQuinnQuiet). From the road to "Coward!", and from the fall on.
        /// </summary>
        public bool Quiets(Player player)
        {
            if (player.whoAmI != TargetIndex)
                return false;

            return State == Phase.Intro || State == Phase.Challenge
                || (State >= Phase.Buck && State <= Phase.Talk) || State == Phase.Finish;
        }

        /// <summary>The player's answer has arrived where the world is decided. See DonaQuinnEncounter.Answer.</summary>
        internal void Answered(DonaQuinnEncounter.Choice choice)
        {
            if (!Authority)
                return;

            switch (choice)
            {
                case DonaQuinnEncounter.Choice.Fight when State == Phase.Intro:
                    Go(Phase.Challenge);
                    break;

                case DonaQuinnEncounter.Choice.Spare when State == Phase.Plead:
                    DonaQuinnEncounter.Settle(DonaQuinnEncounter.Spared);
                    Go(Phase.GetUp);
                    break;

                case DonaQuinnEncounter.Choice.Finish when State == Phase.Plead:
                    DonaQuinnEncounter.Settle(DonaQuinnEncounter.Finished);
                    Go(Phase.Finish);
                    break;

                case DonaQuinnEncounter.Choice.Done when State == Phase.Talk:
                    Go(Phase.Free);
                    break;

                case DonaQuinnEncounter.Choice.Arrived when State == Phase.Lying:
                    Go(Phase.Plead);
                    break;
            }
        }
    }
}
