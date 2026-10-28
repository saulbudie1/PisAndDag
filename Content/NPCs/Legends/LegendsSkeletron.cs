using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// Skeletron, reworked (by request: "Rework Skeletron's cinematics(invincible) ... Just make it fun and beautiful").
    ///
    /// IN EVERY WORLD
    ///     his body, drawn here (SkeletronRig): the new skull with its own jaw, and hands of bones, each finger of them
    ///     posed; in his own look, or the red hat's when the Clothier's death brought him (SkeletronRig)
    ///     ARRIVAL   his coming, while nothing can hurt him or be hurt by him and the player keeps moving
    ///               (LegendsSkeletron.Arrival): the old man coughs blood, his hand bursts into two arms, they hold him,
    ///               tear him down the middle and cast the halves aside, the skull comes up out of him and roars
    ///     UNHANDED  his hands gone (LegendsSkeletron.Unhanded): the jaw drops, he roars, and breathes skulls up into
    ///               the sky, which come down as they fall
    ///     death as it always was, in the new bones (SkeletronSystem.Dies)
    ///
    /// IN LEGENDS
    ///     half again the life, a fifth again the damage; his skull locked (untouchable, warded) until both hands are
    ///     gone; vanilla runs him between moves of his own, his hands' while he has them
    ///     (LegendsSkeletron.HandMoves, fifteen: twelve asked for and three more) and his own when they are gone
    ///     (LegendsSkeletron.HeadMoves, nine)
    ///
    /// The server decides (what move, where it is aimed, who is caught) and sends it with its clock (SendExtraAI); every
    /// machine plays it from those. His hands are moved by what this asks of them every tick (<see cref="Aim"/>, read by
    /// LegendsSkeletronHand), and drawn by him, with his arms, so that the whole body is drawn in one order whatever slots
    /// the game gave its parts (SkeletronSystem keeps the game from drawing the hands itself).
    /// </summary>
    public partial class LegendsSkeletron : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.SkeletronHead;

        public const float LifeScale = 1.5f;
        public const float DamageScale = 1.2f;

        internal enum Scene : byte
        {
            Fight,
            Arrival,
            Unhanded,
        }

        internal enum Move : byte
        {
            None,

            // His hands' (LegendsSkeleton.HandMoves).
            Basketball,
            Clap,
            Tornado,
            Solo,
            Combo,
            Wag,
            Flick,
            Whip,
            Bowling,
            Seals,
            Swoop,
            Purple,
            Guns,
            Snap,
            Rps,

            // His own, his hands gone (LegendsSkeletron.HeadMoves).
            Dive,
            Breath,
            Chomp,
            Inferno,
            Raise,
            Blaze,
            Spin,
            Volley,
            Jaw,
        }

        /// <summary>The first and last of each kind.</summary>
        private const Move FirstHandMove = Move.Basketball;
        private const Move LastHandMove = Move.Rps;
        private const Move FirstHeadMove = Move.Dive;
        private const Move LastHeadMove = Move.Jaw;

        // ---- pacing ---------------------------------------------------------------------------------------------------

        /// <summary>Vanilla's ticks of hovering before his first move, and between his hands' moves and his own, give or take.</summary>
        private const int FirstMove = 200;
        private const int HandsEvery = 200;
        private const int HeadEvery = 150;
        private const int HandsJitter = 50;
        private const int HeadJitter = 40;

        /// <summary>Vanilla spins when its hover count (ai[2]) reaches 800. No move is begun past this.</summary>
        private const float SpinSoon = 560f;

        /// <summary>No move at a player further off than this.</summary>
        private const float MoveRange = 2000f;

        /// <summary>His dive (his own): at most once in this many ticks.</summary>
        private const int DiveEvery = 1500;

        // ---- state: the server's, sent -----------------------------------------------------------------------------------

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>What he is in: his coming, the loss of his hands, or the fight; its beat and how far into it.</summary>
        internal Scene scene;
        internal int beat;
        internal int timer;

        /// <summary>The move under way, how far in, and every move begun counted (so his hands can tell a new one from the last).</summary>
        internal Move move;
        internal int serial;

        /// <summary>
        /// The move's clock: on by <see cref="Pace"/> a tick, so his moves play slower than the game's ticks (by request:
        /// "its better if all attacks are slowed down a bit cuz afterall we are in prehm"); the whole ticks of it, for
        /// its moments (At) and to be sent.
        /// </summary>
        internal float moveClock;

        internal int moveTimer
        {
            get => (int)moveClock;
            set => moveClock = value;
        }

        /// <summary>How fast his moves play: four fifths of the game's speed.</summary>
        internal const float Pace = 0.8f;

        /// <summary>Where a move is aimed, a second point it needs, an angle, which hand acts (-1 the left, 1 the right), and a choice it made.</summary>
        internal Vector2 mark;
        internal Vector2 mark2;
        internal float aim;
        internal sbyte actSide = 1;
        internal byte pick;

        /// <summary>The player his hands hold, or -1.</summary>
        internal int grabbed = -1;

        /// <summary>The red hat: brought by the Clothier's death (SkeletronRig).</summary>
        internal bool redHat;

        /// <summary>
        /// His coming: whether the old man is there to come out of; where the old man stood (his feet: the middle of his
        /// bottom) or, without him, where he comes up; and which way the old man faced.
        /// </summary>
        internal bool victim;
        internal Vector2 anchor;
        internal sbyte facing = 1;

        /// <summary>His hands have been, and his loss of them has been played; how cracked his skull is (his dives, 0 to 3).</summary>
        internal bool handsCame;
        internal bool unhandedDone;
        internal int cracked;

        /// <summary>A few points a move needs every machine to know (where the skeletons rise), and how many.</summary>
        internal readonly Vector2[] spots = new Vector2[4];
        internal int spotCount;

        /// <summary>The server's own: its countdown to the next move, the last two moves, and its wait before another dive.</summary>
        private int nextMove = FirstMove;
        private Move last;
        private Move lastButOne;
        private int diveWait;

        /// <summary>The server's: a move asked for by name (/skeletron), begun as soon as he can.</summary>
        private Move forced;

        /// <summary>How many times the server has fixed this move's aim (sent with it); on a client, how many of those it has heard and how many it has guessed.</summary>
        internal byte fixes;
        private int fixesHeard;
        private int fixesGuessed;

        // ---- each machine's own ----------------------------------------------------------------------------------------

        /// <summary>The last tick of the scene's beat, and of the move, this machine has played: each moment is played once.</summary>
        private int ranBeat = -1;
        private int ranTimer = -1;
        private int ranSerial = -1;
        private int ranMove = -1;

        /// <summary>His hands this tick: their slots (or -1), left and right, and how many.</summary>
        internal readonly int[] hand = { -1, -1 };
        internal int handCount;

        /// <summary>What he asks of each hand this tick: 0 the left, 1 the right.</summary>
        internal readonly Aim[] aims = new Aim[2];

        /// <summary>His face as shown: the jaw dropped, the skull lifted, the hat lifted off it, the eyes lit and on fire, as wanted and as shown.</summary>
        internal float jawWant, liftWant, hatWant, eyesWant, flameWant;
        internal float jawShown, liftShown, hatShown, eyesShown, flameShown;
        internal float faceRate = 0.3f;
        internal Color eyeColour = EyeRed;

        /// <summary>His eyes' own colour, lit, unless a move lights them otherwise.</summary>
        internal static readonly Color EyeRed = new Color(255, 70, 40);

        /// <summary>His ward (Legends, while his hands live), as shown; how big he is drawn (his pop up out of the old man); hidden; his jaw away.</summary>
        internal float wardShown;
        internal float popScale = 1f;
        internal bool hidden;
        internal bool jawAway;

        /// <summary>Where he has just been, for his afterimages when he moves fast.</summary>
        private readonly Vector2[] trail = new Vector2[8];
        private readonly float[] trailTurn = new float[8];
        private int trailCount;

        /// <summary>His name has been put up on this machine; the shot he is in has this machine's player in it.</summary>
        private bool plated;
        internal bool watching;

        /// <summary>He hurts nobody with his own skull this tick; how hard he hits when he does.</summary>
        internal bool headHarmless;
        internal float headHurt = 1f;

        /// <summary>
        /// The move (its <see cref="serial"/>) a parry broke his hands' joint blow in, where the world is decided: kept here
        /// as well as on the hands, because the hand that was parried may not have lived through it.
        /// </summary>
        internal int brokenIn = -1;

        // ---- what he asks of a hand -------------------------------------------------------------------------------------

        /// <summary>How an arm is drawn to its hand.</summary>
        internal enum ArmStyle : byte
        {
            Bent,
            Straight,
            Whip,
            None,
        }

        /// <summary>
        /// What he asks of one hand, this tick: where its middle goes and how fast; the way its fingers point and how fast
        /// it turns there; its pose and how fast it takes it; whether it hurts and how much, whether that is its blow (a
        /// parry can meet it), and both hands' (a parry meets both), or through crowd control (none can); a rim of light
        /// on it; how it is drawn (its scale, hidden, where its arm starts and how it is drawn); and a push for whoever it
        /// hits. Nothing asked: vanilla has it.
        /// </summary>
        internal struct Aim
        {
            public bool Held;
            public Vector2 Goal;
            public float Gain;
            public float Most;
            public float Angle;
            public float Turn;
            public SkeletronRig.Pose Pose;
            public float Shape;
            public float Hurt;
            public bool Blow;
            public bool Joint;
            public bool Bypass;
            public bool Reflect;
            public bool Untouchable;
            public Color Glow;
            public float GlowReach;
            public float Scale;
            public bool Hidden;
            public bool HasRoot;
            public Vector2 Root;
            public ArmStyle Arm;
            public Vector2 Bend;
            public Vector2 Push;
            public float Shake;

            /// <summary>A plain ask: there, so, at the usual pace; hurting as his hands do; drawn as ever.</summary>
            public static Aim At(Vector2 goal, float angle, SkeletronRig.Pose pose, float gain = 0.2f, float most = 22f) => new Aim
            {
                Held = true,
                Goal = goal,
                Gain = gain,
                Most = most,
                Angle = angle,
                Turn = 0.22f,
                Pose = pose,
                Shape = 0.25f,
                Hurt = 1f,
                Scale = 1f,
                Arm = ArmStyle.Bent,
            };
        }

        /// <summary>Which of his hands this is: 0 the left (on the screen), 1 the right.</summary>
        internal static int IndexOf(NPC hand) => hand.ai[0] < 0f ? 1 : 0;

        /// <summary>Which side of him a hand is on: -1 the left, 1 the right (vanilla puts ai[0] -1 on his right).</summary>
        internal static float SideOf(NPC hand) => hand.ai[0] < 0f ? 1f : -1f;

        internal static float SideOf(int index) => index == 0 ? -1f : 1f;

        internal static int IndexOf(float side) => side < 0f ? 0 : 1;

        // ---- set up ----------------------------------------------------------------------------------------------------

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            entity.lifeMax = (int)(entity.lifeMax * LifeScale);
            entity.damage = (int)(entity.damage * DamageScale);
        }

        /// <summary>
        /// Where the world is decided, as he is made: who he comes out of. The old man, if he is being cursed (vanilla has
        /// just set his ai[3]); else the Clothier dying (the red hat); else nobody: he comes up out of the ground.
        /// </summary>
        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            scene = Scene.Arrival;
            beat = 0;
            timer = -1;
            victim = false;
            anchor = npc.Center + new Vector2(0f, npc.height * 0.5f);

            NPC near = null;
            float best = 160f;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (!other.active || other.type != NPCID.OldMan && other.type != NPCID.Clothier)
                    continue;

                // Only the old man the game has just cursed into him (any other is only standing by).
                if (other.type == NPCID.OldMan && other.ai[3] != 1f)
                    continue;

                float distance = Vector2.Distance(other.Center, npc.Center);

                if (distance < best)
                {
                    best = distance;
                    near = other;
                }
            }

            if (near != null && near.type == NPCID.OldMan)
            {
                victim = true;
                anchor = near.Bottom;
                facing = (sbyte)(near.spriteDirection == 1 ? 1 : -1);

                // Facing the one who cursed him, if he can.
                Player who = Main.player[Player.FindClosest(near.position, near.width, near.height)];

                if (who.active)
                    facing = (sbyte)(who.Center.X > near.Center.X ? 1 : -1);
            }
            else
            {
                float floor = FloorBelow(npc.Center.X, 20f, npc.Center.Y - 40f);
                anchor = new Vector2(npc.Center.X, floor < npc.Center.Y + 400f ? floor : npc.Center.Y + 40f);
            }

            // The red hat: the Clothier's death brought him (the game makes him as the Clothier dies, at the Clothier or at
            // the old man, whichever its search met last).
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active && other.type == NPCID.Clothier && other.life <= 0)
                    redHat = true;
            }

            npc.Center = anchor + new Vector2(0f, -30f);
            npc.velocity = Vector2.Zero;
            npc.netUpdate = true;
        }

        // ---- the tick ------------------------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            FindHands(npc);

            for (int i = 0; i < 2; i++)
                aims[i] = default;

            headHarmless = false;
            headHurt = 1f;
            hidden = false;
            jawAway = false;
            popScale = 1f;
            faceRate = 0.3f;
            eyeColour = EyeRed;
            jawWant = liftWant = hatWant = eyesWant = flameWant = 0f;

            if (Authority && diveWait > 0)
                diveWait--;

            if (scene == Scene.Arrival)
            {
                Tick(ref timer, ref ranBeat, ref ranTimer, beat);
                RunArrival(npc);
                ranTimer = Math.Max(ranTimer, timer);
                return false;
            }

            if (scene == Scene.Unhanded)
            {
                Tick(ref timer, ref ranBeat, ref ranTimer, beat);
                RunUnhanded(npc);
                ranTimer = Math.Max(ranTimer, timer);
                return false;
            }

            if (handCount > 0)
                handsCame = true;

            // Both hands gone, in any world: the scene of it, once (the server's to begin).
            if (Authority && handsCame && handCount == 0 && !unhandedDone && npc.ai[1] != 2f && npc.ai[1] != 3f)
            {
                BeginUnhanded(npc);
                return false;
            }

            // Vanilla's first tick makes his hands: his coming has made them, or they are gone.
            npc.ai[0] = 1f;

            // The fight, on every machine (a client never ends his coming itself: the server's word does): he can be chased,
            // and his name is up for whoever has not seen it yet, near him.
            npc.chaseable = true;

            if (!plated && !Main.dedServ && Main.LocalPlayer.active && Main.LocalPlayer.Distance(npc.Center) < 4000f)
                Announce(npc);

            // Not a client's first tick of the roar he makes on its first tick (vanilla, for a client): his coming roared.
            npc.localAI[0] = 1f;

            if (!PADWorld.Legends)
            {
                npc.dontTakeDamage = false;
                return true;
            }

            // His skull is locked while his hands live (by request: "Head locked until both hands die").
            npc.dontTakeDamage = handCount > 0;

            // His own spin is a move of his when his hands are gone (HeadMoves: Spin): vanilla's count is kept short of
            // it, by a whole number of its skull volleys so they keep their time.
            if (handCount == 0 && npc.ai[1] == 0f && npc.ai[2] >= 760f)
                npc.ai[2] -= 400f;

            if (move == Move.None)
            {
                if (Authority)
                    Consider(npc);

                if (move == Move.None)
                    return true;
            }

            if (ranSerial != serial)
            {
                ranSerial = serial;
                ranMove = -1;
                FreshMove();
            }

            moveClock += Pace;
            bool vanilla = RunMove(npc);
            // (A clock pulled back by the server's word never plays a moment twice.)
            ranMove = Math.Max(ranMove, moveTimer);

            return vanilla;
        }

        /// <summary>A scene's clock, a tick on; a beat this machine has not run yet starts its moments afresh.</summary>
        private static void Tick(ref int clock, ref int ranBeat, ref int ranTimer, int beat)
        {
            if (ranBeat != beat)
            {
                ranBeat = beat;
                ranTimer = -1;
            }

            clock++;
        }

        /// <summary>In a scene: this tick is the first at or past <paramref name="when"/> in its beat, on this machine.</summary>
        internal bool Crossed(int when) => ranTimer < when && timer >= when;

        /// <summary>In a move: this tick is the first at or past <paramref name="when"/>, on this machine.</summary>
        internal bool At(int when) => ranMove < when && moveTimer >= when;

        /// <summary>In a move: this tick is the first of one of the moments <paramref name="from"/>, and every <paramref name="every"/> on.</summary>
        internal bool Each(int from, int every) => moveTimer >= from && (moveTimer - from) % every == 0 && ranMove < moveTimer;

        /// <summary>Every few ticks of a moment, from a tick on, until another: for the moments that repeat.</summary>
        internal bool Every(int from, int until, int every)
        {
            for (int when = from; when < until; when += every)
            {
                if (At(when))
                    return true;
            }

            return false;
        }

        public override void PostAI(NPC npc)
        {
            // Vanilla has him (between moves, or in his spin): the face eases back.
            float rate = faceRate;

            jawShown = MathHelper.Lerp(jawShown, jawWant, rate);
            liftShown = MathHelper.Lerp(liftShown, liftWant, rate);
            hatShown = MathHelper.Lerp(hatShown, hatWant, rate);
            eyesShown = MathHelper.Lerp(eyesShown, eyesWant, 0.15f);
            flameShown = MathHelper.Lerp(flameShown, flameWant, 0.12f);

            bool warded = PADWorld.Legends && scene == Scene.Fight && handCount > 0;
            wardShown = MathHelper.Lerp(wardShown, warded ? 1f : 0f, warded ? 0.05f : 0.2f);

            if (Main.dedServ)
                return;

            // Where he has been, for his afterimages.
            for (int i = trail.Length - 1; i > 0; i--)
            {
                trail[i] = trail[i - 1];
                trailTurn[i] = trailTurn[i - 1];
            }

            trail[0] = npc.Center;
            trailTurn[0] = npc.rotation;
            trailCount = Math.Min(trail.Length, trailCount + 1);

            // The old man's blood still runs out of where his arms come out of him (as vanilla's draw makes it).
            if (scene == Scene.Fight && !hidden)
                Bleed(npc);

            // Eyes on fire: flames off them.
            if (flameShown > 0.2f && Main.rand.NextFloat() < flameShown)
                EyeFlames(npc);
        }

        /// <summary>His hands this tick: found by their head (vanilla keeps its slot in their ai[1]).</summary>
        private void FindHands(NPC npc)
        {
            hand[0] = hand[1] = -1;
            handCount = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (!other.active || other.type != NPCID.SkeletronHand || (int)other.ai[1] != npc.whoAmI)
                    continue;

                int index = IndexOf(other);

                if (hand[index] < 0)
                {
                    hand[index] = i;
                    handCount++;
                }
            }
        }

        /// <summary>His hand on that side, or null.</summary>
        internal NPC HandOn(float side)
        {
            int index = hand[IndexOf(side)];

            if (index < 0)
                return null;

            // (Found at the start of his tick: dead since, it is none.)
            NPC found = Main.npc[index];
            return found.active && found.type == NPCID.SkeletronHand ? found : null;
        }

        /// <summary>How many hands of his a move needs.</summary>
        private static int Needs(Move which) => which switch
        {
            Move.Clap or Move.Whip or Move.Bowling or Move.Seals or Move.Purple => 2,
            >= FirstHandMove and <= LastHandMove => 1,
            _ => 0,
        };

        // ---- moves: begun, ended, aimed -----------------------------------------------------------------------------

        /// <summary>
        /// The server's: while vanilla has him hovering and no spin is near, counts down, then picks a move: one of his
        /// hands' while he has them (those that need both only while he has both), one of his own after; never one of the
        /// last two again; his dive rarely.
        /// </summary>
        private void Consider(NPC npc)
        {
            // (With no hands vanilla never spins: its count is kept short of it, PreAI.)
            if (npc.ai[1] != 0f || handCount > 0 && npc.ai[2] > SpinSoon || Main.IsItDay())
                return;

            Player target = Main.player[npc.target];

            if (!target.active || target.dead || Vector2.Distance(target.Center, npc.Center) > MoveRange)
                return;

            // Asked for by name: now, if he still has what it needs.
            if (forced != Move.None)
            {
                Move asked = forced;
                forced = Move.None;

                if (Possible(asked))
                {
                    Begin(npc, asked, target);
                    return;
                }
            }

            if (--nextMove > 0)
                return;

            Move pick = Choose(npc);

            if (pick == Move.None)
            {
                nextMove = 30;
                return;
            }

            Begin(npc, pick, target);
        }

        private static readonly Move[] TwoHands = { Move.Basketball, Move.Clap, Move.Tornado, Move.Solo, Move.Combo, Move.Wag, Move.Flick, Move.Whip, Move.Bowling, Move.Seals, Move.Swoop, Move.Purple, Move.Guns, Move.Snap, Move.Rps };
        private static readonly Move[] OneHand = { Move.Basketball, Move.Tornado, Move.Solo, Move.Combo, Move.Wag, Move.Flick, Move.Swoop, Move.Guns, Move.Snap, Move.Rps };
        private static readonly Move[] NoHands = { Move.Breath, Move.Chomp, Move.Inferno, Move.Raise, Move.Blaze, Move.Spin, Move.Volley, Move.Jaw };

        /// <summary>Whether he can do a move now: his hands' with the hands it needs, his own with none left.</summary>
        private bool Possible(Move which) => which >= FirstHeadMove ? handCount == 0 : which >= FirstHandMove && handCount >= Math.Max(1, Needs(which));

        // ---- asked for (SkeletronCommand) ------------------------------------------------------------------------------

        /// <summary>Every move's name, as it is asked for.</summary>
        internal static string[] MoveNames()
        {
            Move[] all = (Move[])Enum.GetValues(typeof(Move));
            string[] names = new string[all.Length - 1];

            for (int i = 1; i < all.Length; i++)
                names[i - 1] = all[i].ToString().ToLowerInvariant();

            return names;
        }

        /// <summary>The server's: a move asked for by name, next. Why not, or null.</summary>
        internal string Ask(string name)
        {
            if (!Enum.TryParse(name, true, out Move which) || which == Move.None)
                return $"No move \"{name}\". Moves: {string.Join(", ", MoveNames())}.";

            if (!PADWorld.Legends)
                return "His moves are Legends' only (/legends on).";

            if (which >= FirstHeadMove && handCount > 0)
                return $"{name} is one of his own: his hands must be gone first (/skeletron hands).";

            if (which < FirstHeadMove && handCount < Math.Max(1, Needs(which)))
                return $"{name} needs {Math.Max(1, Needs(which))} hand(s); he has {handCount}.";

            forced = which;
            return null;
        }

        /// <summary>What he is doing, for /skeletron.</summary>
        internal string Describe(NPC npc) =>
            $"Skeletron: {scene}{(scene == Scene.Fight ? "" : $" (beat {beat}, {timer})")}, move {move}{(move == Move.None ? "" : $" ({moveTimer})")}, " +
            $"hands {handCount}, {(redHat ? "red hat" : "his own look")}, life {npc.life}/{npc.lifeMax}{(PADWorld.Legends ? ", Legends" : "")}.";

        private Move Choose(NPC npc)
        {
            Move[] pool = handCount >= 2 ? TwoHands : handCount == 1 ? OneHand : NoHands;

            if (handCount == 0 && diveWait <= 0 && last != Move.Dive && Main.rand.NextBool(4))
            {
                diveWait = DiveEvery;
                return Move.Dive;
            }

            for (int tries = 0; tries < 20; tries++)
            {
                Move pick = pool[Main.rand.Next(pool.Length)];

                if (pick != last && pick != lastButOne)
                    return pick;
            }

            return pool[Main.rand.Next(pool.Length)];
        }

        /// <summary>The server's: a move begins, aimed at whoever is nearest now (as every machine will have it), by the hand nearer them.</summary>
        private void Begin(NPC npc, Move pick, Player target)
        {
            npc.TargetClosest(false);
            target = Target(npc);

            lastButOne = last;
            last = pick;
            move = pick;
            moveTimer = -1;
            mark = target.Center;
            mark2 = target.Center;
            aim = 0f;
            this.pick = 0;
            grabbed = -1;
            spotCount = 0;
            fixes = 0;
            serial++;

            // The acting hand: the one on the player's side of him, if he has it.
            float toward = target.Center.X >= npc.Center.X ? 1f : -1f;
            actSide = (sbyte)(HandOn(toward) != null ? toward : -toward);

            npc.netUpdate = true;

            OnBegin(npc, target);
        }

        /// <summary>Over: vanilla has him again from here, and his hands idle.</summary>
        internal void End(NPC npc)
        {
            bool hands = move >= FirstHandMove && move <= LastHandMove;

            // His spin cut short (the player gone, the day): vanilla's with it.
            if (move == Move.Spin && npc.ai[1] == 1f)
            {
                npc.ai[1] = 0f;
                npc.ai[2] = 0f;
            }

            move = Move.None;
            moveTimer = 0;
            grabbed = -1;
            npc.rotation = MathHelper.WrapAngle(npc.rotation);
            npc.reflectsProjectiles = false;

            if (!Authority)
                return;

            nextMove = hands
                ? HandsEvery + Main.rand.Next(-HandsJitter, HandsJitter + 1)
                : HeadEvery + Main.rand.Next(-HeadJitter, HeadJitter + 1);

            npc.netUpdate = true;
        }

        /// <summary>
        /// The server fixes where a move is aimed, now, and sends it. A client takes its own idea of the player meanwhile, but
        /// only until it has heard the server's (<see cref="Guessed"/>): a guess made after would undo the server's word.
        /// </summary>
        internal void Fix(NPC npc, Vector2 at)
        {
            if (Authority)
            {
                mark = at;
                fixes++;
                npc.netUpdate = true;
                return;
            }

            if (Guessed())
                mark = at;
        }

        /// <summary>A client's guess at the server's next fix of this move: whether it is still its to make (that fix not heard yet).</summary>
        internal bool Guessed() => ++fixesGuessed > fixesHeard;

        /// <summary>A client's own idea of where a move is aimed while it tracks the player, until the server has fixed it.</summary>
        internal void Track(Vector2 at)
        {
            if (Authority || fixesHeard == 0)
                mark = at;
        }

        internal Player Target(NPC npc) => Main.player[Math.Clamp(npc.target, 0, Main.maxPlayers - 1)];

        /// <summary>Plays the move under way. True: vanilla runs him this tick as well (his spin).</summary>
        private bool RunMove(NPC npc)
        {
            Player target = Target(npc);

            // The player gone, or a hand the move needs: over.
            if (Authority && (!target.active || target.dead || handCount < Needs(move) || Main.IsItDay()))
            {
                End(npc);
                return true;
            }

            // A move of one hand, that hand gone: the other takes it up.
            if (Needs(move) == 1 && HandOn(actSide) == null)
                actSide = (sbyte)-actSide;

            switch (move)
            {
                case Move.Basketball: Basketball(npc, target); break;
                case Move.Clap: Clap(npc, target); break;
                case Move.Tornado: Tornado(npc, target); break;
                case Move.Solo: Solo(npc, target); break;
                case Move.Combo: Combo(npc, target); break;
                case Move.Wag: Wag(npc, target); break;
                case Move.Flick: Flick(npc, target); break;
                case Move.Whip: Whip(npc, target); break;
                case Move.Bowling: Bowling(npc, target); break;
                case Move.Seals: Seals(npc, target); break;
                case Move.Swoop: Swoop(npc, target); break;
                case Move.Purple: Purple(npc, target); break;
                case Move.Guns: Guns(npc, target); break;
                case Move.Snap: Snap(npc, target); break;
                case Move.Rps: Rps(npc, target); break;

                case Move.Dive: Dive(npc, target); break;
                case Move.Breath: Breath(npc, target); break;
                case Move.Chomp: Chomp(npc, target); break;
                case Move.Inferno: Inferno(npc, target); break;
                case Move.Raise: Raise(npc, target); break;
                case Move.Blaze: Blaze(npc, target); break;
                case Move.Spin: return Spin(npc, target);
                case Move.Volley: Volley(npc, target); break;
                case Move.Jaw: Jaw(npc, target); break;

                default:
                    if (Authority)
                        End(npc);
                    return true;
            }

            // A hand the move does not use waits by him.
            for (int i = 0; i < 2; i++)
            {
                if (!aims[i].Held && hand[i] >= 0)
                    Idle(npc, SideOf(i));
            }

            // In a move his own spin is his move's, and his skull hurts as the move says.
            npc.damage = headHarmless ? 0 : (int)(npc.defDamage * headHurt);

            // A heartbeat, so a machine that missed a word catches up.
            if (Authority && moveTimer % 45 == 0)
                npc.netUpdate = true;

            return false;
        }

        // ---- shared --------------------------------------------------------------------------------------------------

        /// <summary>
        /// Where a hand of his hangs when it has nothing to do: vanilla's own spot for it (out to its side and well below
        /// him), so that a hand given back to vanilla is already where vanilla keeps it, and he has his old shape: arms out
        /// from under the skull, elbows up, forearms and hands hanging.
        /// </summary>
        internal static Vector2 Rest(NPC npc, float side) => npc.Center + new Vector2(side * 200f, 205f) * npc.scale;

        /// <summary>
        /// The way a hand hangs at a point: on along its forearm (from the elbow its arm would have, reaching there), and
        /// when that is downward, a little more toward straight down, as a hand's weight takes it.
        /// </summary>
        internal static float HangAngle(Vector2 shoulder, Vector2 middle, float side, float scale)
        {
            Vector2 elbow = SkeletronRig.Elbow(shoulder, middle, side, scale);
            float along = SkeletronRig.AngleFor(middle - elbow);
            float down = Math.Max(0f, (float)Math.Cos(along));

            return along.AngleLerp(0f, 0.35f * down);
        }

        /// <summary>The way a hand hangs at its rest.</summary>
        internal static float RestAngle(NPC npc, float side) => HangAngle(SkeletronRig.Shoulder(npc.Center, side, npc.scale), Rest(npc, side), side, npc.scale);

        /// <summary>A hand hanging: its fingers a little curled, slowly flexing, each hand in its own time (a look only).</summary>
        internal static SkeletronRig.Pose Hanging(float side) =>
            SkeletronRig.Pose.Lerp(SkeletronRig.Relaxed, SkeletronRig.Grab, 0.2f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.7f + side * 1.3f));

        /// <summary>A hand with nothing to do: hanging by him, a little loose, hurting nobody who does not walk into it.</summary>
        internal void Idle(NPC npc, float side)
        {
            Vector2 goal = Rest(npc, side) + new Vector2(0f, 6f * (float)Math.Sin(Main.GameUpdateCount * 0.05f + side));

            Aim idle = Aim.At(goal, RestAngle(npc, side), Hanging(side), 0.1f, 14f);
            idle.Hurt = 0.6f;
            aims[IndexOf(side)] = idle;
        }

        /// <summary>The ask for a hand, to be set this tick.</summary>
        internal ref Aim AimOf(float side) => ref aims[IndexOf(side)];

        internal static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        internal static float EaseOut(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f - (1f - t) * (1f - t);
        }

        internal static float EaseIn(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t;
        }

        /// <summary>Out past where it is going and back: a pop.</summary>
        internal static float Back(float t, float over = 1.7f)
        {
            t = MathHelper.Clamp(t, 0f, 1f) - 1f;
            return 1f + t * t * ((over + 1f) * t + over);
        }

        /// <summary>A spring settling: overshoots, rings, comes to rest at 1.</summary>
        internal static float Elastic(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);

            if (t >= 1f)
                return 1f;

            return 1f - (float)(Math.Pow(2, -9 * t) * Math.Cos(t * 13f));
        }

        internal static float Smoother(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        internal static void Approach(NPC npc, Vector2 goal, float gain, float most)
        {
            Vector2 want = (goal - npc.Center) * gain;

            if (want.Length() > most)
                want = want.SafeNormalize(Vector2.Zero) * most;

            npc.velocity = want;
        }

        /// <summary>
        /// There on the next tick. Handed to velocity, not written into position, because the move after the AI would then
        /// carry it on by whatever velocity was left.
        /// </summary>
        internal static void Place(NPC npc, Vector2 goal) => npc.velocity = goal - npc.Center;

        /// <summary>How much a thing happening here is felt on this machine's screen: in full near the player, none far off.</summary>
        internal static float Near(Vector2 at)
        {
            if (Main.dedServ)
                return 0f;

            return 1f - MathHelper.Clamp((Vector2.Distance(Main.LocalPlayer.Center, at) - 700f) / 1400f, 0f, 1f);
        }

        /// <summary>The screen shaken here, as much as it is felt.</summary>
        internal static void Shake(Vector2 at, float power, int ticks)
        {
            float near = Near(at);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(power * near, ticks);
        }

        /// <summary>A sound, here, on a machine with ears.</summary>
        internal static void Sound(in SoundStyle style, Vector2 at)
        {
            if (!Main.dedServ)
                SoundEngine.PlaySound(style, at);
        }

        /// <summary>
        /// Where a blow aimed at the player who was at <paramref name="at"/> comes down: the ground level with their feet or
        /// below, in the columns within <paramref name="half"/> of <paramref name="x"/>.
        /// </summary>
        internal static float FloorUnder(float x, float half, Vector2 at) => FloorBelow(x, half, at.Y + 17f);

        /// <summary>
        /// The top of the first ground at or below <paramref name="from"/> (world y), in the columns within
        /// <paramref name="half"/> of <paramref name="x"/>: a tile anything can stand on, platforms included. None within
        /// fifty tiles: never.
        /// </summary>
        internal static float FloorBelow(float x, float half, float from)
        {
            int left = (int)((x - half) / 16f);
            int right = (int)((x + half) / 16f);
            int top = (int)(from / 16f);

            for (int y = top; y < top + 50; y++)
            {
                for (int i = left; i <= right; i++)
                {
                    if (!WorldGen.InWorld(i, y, 2))
                        continue;

                    Tile tile = Main.tile[i, y];

                    if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        return y * 16f + (tile.IsHalfBlock ? 8f : 0f);
                }
            }

            return float.PositiveInfinity;
        }

        /// <summary>The ground under a point within <paramref name="reach"/>, or, with none, a line that far below it.</summary>
        internal static float GroundOr(float x, float half, float from, float reach)
        {
            float floor = FloorBelow(x, half, from);
            return floor - from <= reach ? floor : from + reach;
        }

        // ---- his skull, drawn and hurting --------------------------------------------------------------------------

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (scene != Scene.Fight || headHarmless || hidden)
                return false;

            // Bowled, he hurts by the ball of him rolling, not his whole box: it can be jumped.
            if (Bowled())
                return SkeletronShots.Touches(target.Hitbox, npc.Center, BowlHurt * npc.scale);

            return true;
        }

        public override bool CanHitNPC(NPC npc, NPC target) => scene == Scene.Fight && !hidden;

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (move == Move.Bowling)
                BowlingStrike(npc, target);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.IsABestiaryIconDummy || !SkeletronRig.Ready)
                return true;

            DrawBody(npc, spriteBatch, drawColor);
            return false;
        }

        // ---- sync ---------------------------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(redHat);
            bitWriter.WriteBit(victim);
            bitWriter.WriteBit(handsCame);
            bitWriter.WriteBit(unhandedDone);

            binaryWriter.Write((byte)scene);
            binaryWriter.Write((byte)beat);
            binaryWriter.Write((short)Math.Clamp(timer, -1, short.MaxValue));

            binaryWriter.Write((byte)move);
            binaryWriter.Write((short)Math.Clamp(moveTimer, -1, short.MaxValue));
            binaryWriter.Write(serial);
            binaryWriter.Write(fixes);

            binaryWriter.WriteVector2(mark);
            binaryWriter.WriteVector2(mark2);
            binaryWriter.Write(aim);
            binaryWriter.Write(actSide);
            binaryWriter.Write(pick);
            binaryWriter.Write((short)grabbed);
            binaryWriter.Write((byte)cracked);

            binaryWriter.WriteVector2(anchor);
            binaryWriter.Write(facing);

            binaryWriter.Write((byte)spotCount);

            for (int i = 0; i < spotCount; i++)
                binaryWriter.WriteVector2(spots[i]);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            redHat = bitReader.ReadBit();
            victim = bitReader.ReadBit();
            handsCame = bitReader.ReadBit();
            unhandedDone = bitReader.ReadBit();

            Scene gotScene = (Scene)binaryReader.ReadByte();
            int gotBeat = binaryReader.ReadByte();
            int gotTimer = binaryReader.ReadInt16();

            Move gotMove = (Move)binaryReader.ReadByte();
            int gotMoveTimer = binaryReader.ReadInt16();
            int gotSerial = binaryReader.ReadInt32();
            byte gotFixes = binaryReader.ReadByte();

            mark = binaryReader.ReadVector2();
            mark2 = binaryReader.ReadVector2();
            aim = binaryReader.ReadSingle();
            actSide = binaryReader.ReadSByte();
            pick = binaryReader.ReadByte();
            grabbed = binaryReader.ReadInt16();
            cracked = binaryReader.ReadByte();

            anchor = binaryReader.ReadVector2();
            facing = binaryReader.ReadSByte();

            spotCount = Math.Min(spots.Length, (int)binaryReader.ReadByte());

            for (int i = 0; i < spotCount; i++)
                spots[i] = binaryReader.ReadVector2();

            // The scene and its beat as the server has them; its clock when this one has drifted from it.
            if (gotScene != scene || gotBeat != beat)
            {
                if (gotScene != scene)
                    ranBeat = -1;

                scene = gotScene;
                beat = gotBeat;
                timer = gotTimer;
            }
            else if (Math.Abs(gotTimer - timer) > 3)
            {
                timer = gotTimer;
            }

            // A new move: begun here from the server's clock (nothing of its aim guessed or heard yet but this). The same one:
            // its clock, when drifted.
            if (gotSerial != serial)
            {
                serial = gotSerial;
                move = gotMove;
                moveTimer = gotMoveTimer;
                fixesHeard = gotFixes;
                fixesGuessed = 0;
            }
            else if (gotMove != move || Math.Abs(gotMoveTimer - moveTimer) > 3)
            {
                move = gotMove;
                moveTimer = gotMoveTimer;
            }

            fixesHeard = Math.Max(fixesHeard, gotFixes);
            fixes = gotFixes;

            AfterReceive(npc);
        }
    }
}
