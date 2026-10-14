using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

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
    /// THE FIGHT. The moves below are his first phase's, and every one of them is his
    /// second's too. He does nothing by touch. Everything off the sword goes through
    /// armour, and is varied the way vanilla varies any hit.
    ///
    ///   WALKING. Slowly, at them, the sword dragged behind him. While he walks he answers
    ///   what is sent at him (Foigh.React): a swing of theirs met with his own and them thrown
    ///   back, a shot struck out of the air and sometimes sent back, a spell stepped out of
    ///   the way of.
    ///   ONE. Wound up over his head: down, up, a thrust, and a leap back with the sword
    ///   coming round after him.
    ///   TWO. A leap onto them, point first, the higher the further off they are. On them,
    ///   he goes straight on into something else; on nothing, the sword is in the ground for
    ///   two seconds and he pulls at it.
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
    /// wear the star; nor, in his second phase, the cut up out of the ground or the blow in
    /// the air, which throw them whatever shield they have, and wear it for that. (The cut
    /// up in the hand's move is another, and can be.)
    ///
    /// WHAT KEEPS HIM STANDING (Foigh.Hold), by request. In every phase no more than eight
    /// thousandths of his life can be taken from him in a second, whatever hits him or
    /// burns him: at that, he turns everything aside until half a second's worth can be
    /// taken again. He has a posture (with no bar: by request it is not shown): what he
    /// turns aside wears at it, his own sword parried most of all; let alone it comes
    /// back; worn through, he is broken, down on a knee for three seconds, answering
    /// nothing, and what could have been taken from him before and was not can be taken
    /// then. From his second phase, what keeps coming at him (a blade that goes on
    /// cutting, a beam, a magazine) is answered whole, a second at a time, in whatever
    /// move he is in; held at that for two and a half seconds he runs at whoever it is
    /// behind his sword, and cuts; and losing life about as fast as he can lose it, he
    /// is gone from under it, a flash step to behind them and a cut up that no parry
    /// turns. What is called up to fight for them (Foigh.Called) he turns aside two times
    /// in three, four in five in his third phase, in whatever move he is in, and what
    /// lands of it can take no more than half of what the cap allows in a second; when
    /// what lands of it has taken nine tenths of all that allows in three seconds, he
    /// curls up in the air, shaking, nothing landing on him, and bursts into straight
    /// cuts across the tiles about him (THE BALL, Foigh.Wild), no oftener than every ten
    /// seconds: in his first phase white, across eighteen tiles, cutting down four in nine
    /// of what they go through of it; in his second white, across twenty-three, six in
    /// nine; in his third red and black, across twenty-seven, all of it. Before he bursts,
    /// a roar drawn in to him, in red and black.
    ///
    /// THE SECOND PHASE begins when two thirds of his life are left (Foigh.Passage): he
    /// stops, the picture fades, the player is put down seventy tiles off, and the camera
    /// goes over to him on a knee before his sword, which stands upright in the ground with
    /// both his hands on its handle. He spits blood, looks up, says two lines, heaves the
    /// sword up and tears it out of the ground, points it at the sky, runs a stone along
    /// it, says a third, and the picture fades back to the player. From then on the sword
    /// is its sharp side (another picture, and the damage given for "after").
    ///
    /// IN HIS SECOND PHASE, by request: whatever is sent at him he answers two times in
    /// three, of every kind. From far off he mostly walks on; he leaps onto them more
    /// often, from further, and as high as they are (forty-six tiles, with the room for
    /// it). Up close he waits a second or two between attacks, the sword's four moves
    /// are a quarter faster and carry him a third further, and he has more:
    ///
    ///   THE HAND. The sword swung up, and a run at them with it up; the blow down, and
    ///   the cut up; down on a knee, and a leap at them, forward and up, with the bottom
    ///   hand out (as far as ten tiles and as high as five, in half a second; the yellow
    ///   star: it can be got out of the way of, not turned aside). On nothing he comes
    ///   down empty and is a moment getting over it. With them by the throat: down, the
    ///   camera in close, they are pulled in against him, the sword is pushed slowly
    ///   through them and raised until it points up and ahead with them on it, and they
    ///   are flung off it.
    ///   THE LATE CUT. Sunk low with the sword at his hip, a pale line from him through
    ///   them; he is gone along it and standing past them with the sword out, his back to
    ///   them; and a moment later the whole line opens. It can be turned aside (at the
    ///   moment it opens, whichever way they face), jumped, or run out past the end of.
    ///   THE MILL. The sword brought over and levelled at them, and he comes on turning,
    ///   it out at arm's length, three times round and gaining ground, the way he set
    ///   off; out of the last turn, up, and a blow down as he lands. It can be turned
    ///   aside.
    ///   THE CUT UP. A run at them with the sword dragged in sparks; it is torn forward
    ///   through the ground and up, and whoever it lands on is thrown into the air,
    ///   whatever shield they have (the yellow star, from the run on: it can be got out of
    ///   the way of, not turned aside); and he is up where they are going before they
    ///   are (IN THE AIR), and they are his to strike down there whatever grace the cut
    ///   left them.
    ///   IN THE AIR. A flash step to beside where a player who is off the ground, or
    ///   over his head, will be as his sword comes down; a moment hanging there with no
    ///   weight, the sword going up over his head; a blow all the way round under him
    ///   that sends them down; and he falls after it. Too near the ground for that blow,
    ///   it is the mill's last, down onto the ground with him: so one who jumped is met
    ///   where they come down. The yellow star, from the moment he goes. On its own
    ///   against anyone off the ground, and as the end of the cut up.
    ///
    /// WHAT REACHES THEM (Foigh.Reach), from his second phase, by request: a hook in his
    /// bottom hand, taken out in the passage with nothing said of it, and thrown: he is
    /// pulled along its chain to one it bites on the ground, and one it bites off the
    /// ground is pulled in to him; either way the sword comes down as they meet. Two
    /// cuts of pale wind let go at them wherever they are (his second phase only). His
    /// knives oftener, and five at once in a fan. A hit of his on one who hangs from a
    /// hook lets it go, and they throw no other for a second; a hit on one who is off
    /// the ground shuts their wings and rocket boots for two (FoighGripPlayer). And he
    /// reads them: a swing that missed, something held on him, a reload, a hook of
    /// theirs biting, each answered as often as he answers anything.
    ///
    /// THE THIRD PHASE begins when three tenths of his life are left (Foigh.Third): he
    /// stops, the picture fades, and comes back on him, on his feet and shaking, bent over
    /// his sword, which is in the ground before him. He says a line, tears the sword out,
    /// holds it up at the sky, turns it on himself and runs himself through; tears it out
    /// again, and throws the blood off it; says two more; and is behind the player, their
    /// wings in his hand, and cuts them off. A fourth line, and the picture fades back to
    /// the player. From then on the sword is bloodied (a third picture) and burns black
    /// and red; every blow of it leaves a wound that bleeds (Ultra Bleeding, as many as
    /// three at once); and nobody flies, by wings, a booster or a mount, for as long as
    /// he is there. By request his defence is doubled, and whatever is sent at him he
    /// answers four times in five. What he fights with is his second phase's, all of it
    /// (but its wind), half as fast again, with half a second to a second between moves
    /// (Foigh.Wild), and more:
    ///
    ///   THE THREE CUTS. Where he stands, the blade flaring first: a cut down, a cut up,
    ///   and a blow down in both hands, three quarters of a second apart; and each is
    ///   let go of as it is made and goes on through the air, a crescent of what the
    ///   sword burns with (FoighSlash), at them, wherever they are: along the ground,
    ///   up at one on the ceiling, down at one below. The third is the largest. Each is
    ///   half his sword's blow and leaves its wound; each can be jumped, or turned
    ///   aside, and so can the blade itself by one who is near enough to be cut by it.
    ///
    ///   WILDER (Foigh.Wild), by request. A cut that ends with them out of its reach is
    ///   let go at them. The wheel: up off the ground, turning, three of the great cuts
    ///   let go from up there. The leap that comes down on nobody: the sword driven
    ///   deeper, and straight cuts open out of the ground one after another, each a
    ///   line through the ten tiles about the blade, shown a moment before it opens
    ///   (FoighLine). The sword lingers before it comes down, or does not come down and
    ///   he is behind them. And what he has learned of whoever he fights weighs what he
    ///   picks.
    ///
    /// He is drawn from seven pictures, not a sheet (FoighRig), and every movement is poses
    /// at moments (FoighMoves): the blade that is drawn is the blade that hits.
    ///
    /// Partial: this file is the body of it (numbers, moves, sync, hooks); .Fight is the
    /// fight, .React what he does about what is sent at him, .Hold what keeps him from
    /// being cut down in a few seconds (the cap, his posture, the holding parry, the run
    /// behind it, the break-out), .Reach what reaches them from his second phase on
    /// (the wind, the hook, what he reads of them), .Scene the road, .Passage the way
    /// into his second phase, .Third the way into his third and what is its own in it,
    /// .Wild his third phase's speed and what is wilder in it (and the ball), .Called
    /// what is called up to fight for them, .Draw the drawing.
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

        /// <summary>None was given: a little under the Moon Lord's. In his third phase, by request, twice that.</summary>
        public const int Defense = 40;
        public const int ThirdDefense = 2;

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

        /// <summary>Where his second phase begins (Foigh.Passage), and his third (Foigh.Third): shares of his life.</summary>
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

            /// <summary>From his second phase: held at turning something aside for two and a half seconds, a run at whoever it is with the sword up across his front (Foigh.Hold).</summary>
            Charge,

            /// <summary>The one who drank: the hand, nothing in it, up off their feet, the sword up through them, held there, flung off.</summary>
            Grab,
            Miss,
            Lift,
            Spit,
            Spitted,
            Fling,

            /// <summary>
            /// His second phase's own. The sword swung up; the run at them with it up; the
            /// blow down and the cut up; down on a knee; the leap forward and up with the
            /// hand out. With somebody in the hand: the rest of the way down with them;
            /// pulled in, and the sword pushed slowly through them; held up on it, pointing
            /// up and ahead (and flung off it: Fling). With nobody: Miss.
            /// </summary>
            Raise,
            Rush,
            Hew,
            Crouch,
            Spring,
            Caught,
            Pierce,
            Hoist,

            /// <summary>The late cut: sunk low, looking along the line (the step along it is a Flash); and past them, still, until the line has opened, and the sword let down.</summary>
            Poise,
            Wake,

            /// <summary>The mill: the sword levelled at them; three times round, gaining ground; and up out of the last turn into a blow down.</summary>
            Level,
            Mill,
            MillEnd,

            /// <summary>The cut up: the run with the sword dragged; and the blade torn forward through the ground and up.</summary>
            Scrape,
            Upcut,

            /// <summary>In the air (the step up to them is a Flash): hanging beside them with no weight; the blow down; falling after it; and down.</summary>
            Hang,
            Smite,
            Drop,
            Alight,

            /// <summary>His third phase's own. Three cuts where he stands, down, up, and down in both hands: each let go of, and gone on through the air (FoighSlash).</summary>
            Rend,

            /// <summary>His second phase's own (Foigh.Reach). Two cuts where he stands, down and up: each let go of as a cut of wind (FoighSlash, its pale kind).</summary>
            Gale,

            /// <summary>
            /// From his second phase (Foigh.Reach). The hook thrown from his bottom hand;
            /// pulled along its chain to whoever it has, on the ground; whoever it has, off
            /// the ground, pulled in to him; the blow that meets them; and nothing on it,
            /// taken in again.
            /// </summary>
            HookCast,
            Haul,
            Winch,
            Reap,
            HookMiss,

            /// <summary>
            /// His third phase's own (Foigh.Wild). Down, as for his fourth move; and up off
            /// the ground, turning with the sword out, a great cut let go each time it
            /// comes round. (He falls after it as after the blow in the air: Drop.)
            /// </summary>
            Soar,
            Wheel,

            /// <summary>
            /// In any phase, what fights for them answered (Foigh.Wild): down, up off the
            /// ground a little way, curled up in the air with nothing landing on him, and
            /// burst open into straight cuts all about him. (He falls after it as after the
            /// wheel: Drop.)
            /// </summary>
            Curl,

            /// <summary>His sword turned aside; down on a knee.</summary>
            Reel,
            Stagger,

            /// <summary>His posture worn through: down on a knee with the sword down for three seconds, answering nothing (Foigh.Hold).</summary>
            Broken,

            // ---- two thirds of his life left: the passage into his second phase ----

            /// <summary>Stopped where he is, whatever he was doing: the picture goes to black.</summary>
            Spent,

            /// <summary>Down on a knee before his sword, upright in the ground, both hands on its handle: blood, a look up, and two lines, each answered.</summary>
            Knee,

            /// <summary>The sword heaved at and torn out of the ground, and up at the sky; then a stone along it.</summary>
            Whet,

            /// <summary>The third line, which goes by itself.</summary>
            Oath,

            /// <summary>The picture back to the player, and it goes on. (The end of both passages.)</summary>
            Resume,

            // ---- three tenths of his life left: the passage into his third phase (it begins as the other does: Spent) ----

            /// <summary>On his feet, bent over his sword, which is in the ground before him, both hands on its handle: shaking, blood, and a line, answered.</summary>
            Shudder,

            /// <summary>The sword out of the ground and up at the sky; turned on himself; and through him.</summary>
            Gut,

            /// <summary>Torn out again, and the blood thrown off it; two lines, which go by themselves.</summary>
            Bled,

            /// <summary>Behind them (the step there is a Flash): their wings in his hand, pulled, and cut; a fourth line.</summary>
            Sever,

            /// <summary>At nothing: rocked by the blow; and, in the hall, gone to the end of it (Foigh.End).</summary>
            Dying,

            /// <summary>Nobody left to fight: he stands, and is gone.</summary>
            Leave,

            // ---- at nothing: the rest of his end (Foigh.End), from Dying ----

            /// <summary>The picture to black and back, the player before him, and the camera over to him where he stands (in the hall, at its end): before his roar, by request.</summary>
            Shown,

            /// <summary>His long roar; in the hall, its flames out but the ones over him.</summary>
            Roar,

            /// <summary>Down onto his knees and his hands, the sword let fall.</summary>
            Fold,

            /// <summary>His head down, shaking and bleeding: the player walks up to him.</summary>
            Bowed,

            /// <summary>His head up to them: his first line, laughing, and his coughs.</summary>
            Laugh,

            /// <summary>His second line, and their answer: yes or no.</summary>
            Asked,

            /// <summary>Yes: "I knew it", his coughs, and the rest of it.</summary>
            Knew,

            /// <summary>No: his line about their mouth.</summary>
            Mouth,

            /// <summary>His line about the choir.</summary>
            Choir,

            /// <summary>Up on his knees, his back straight: his last line, and their choice, Finish or Spare.</summary>
            Plead,

            /// <summary>Finish: the sword taken up off the ground, and the walk up to him.</summary>
            Finish,

            /// <summary>The sword through him; rigid; his look at them; "Thank you".</summary>
            Thrust,

            /// <summary>The blade out of him and the body cast down; smoke, ash, and gone; the sword theirs.</summary>
            Cast,

            /// <summary>Spare: the sword taken up and thrown behind them; they walk away; he is left there.</summary>
            Spare,

            /// <summary>His last line, at nobody.</summary>
            Scorn,

            /// <summary>Down on his face; the picture to the loading screen as they leave the place.</summary>
            Slump,
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

        /// <summary>
        /// The move's clock as it stands, fractions and all. A tick of his is a tick of the
        /// game's, but for the moves his second phase quickens (Tempo): those run a
        /// quarter faster than that, and Timer is then the whole ticks of it gone by.
        /// </summary>
        private float Clock => NPC.ai[1];

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

        /// <summary>Which phase he is in: the first; from the moment two thirds of his life are left, the second; from three tenths, the third.</summary>
        private int phase = 1;

        // ---- where the world is decided only -------------------------------------------

        /// <summary>The leap he is in has landed on somebody.</summary>
        private bool landedHit;

        /// <summary>The move he is in was picked from up close: in his second phase he waits longer after it.</summary>
        private bool close;

        /// <summary>The wait he is in is that longer one: a swing of theirs that he meets does not cut it short.</summary>
        private bool waitingOut;

        /// <summary>The cut up he is in has thrown this player into the air (their own machine's word), or nobody.</summary>
        private int lifted = -1;

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

        /// <summary>The same two, as the move's clock had them (Clock): what he is drawn by, and his blade worked out by, between whole ticks.</summary>
        private float clockWas;
        private float clock;

        /// <summary>A move whose ticks have gone back by more than this has been begun again.</summary>
        private const int Again = 3;

        /// <summary>On the ground, as the last tick's move left him.</summary>
        private bool grounded;

        private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>The road is behind him: from the first step of the fight to the end of it, the passages into his second phase and his third with it.</summary>
        public bool Fighting => State >= Move.Walk && State < Move.Dying;

        /// <summary>
        /// In one of the two passages, into his second phase or his third: from the moment
        /// he stops to the last tick of the picture coming back, the step he takes in the
        /// middle of the second of them with it.
        /// </summary>
        public bool Passing => (State >= Move.Spent && State <= Move.Sever) || (State == Move.Flash && then == Move.Sever);

        /// <summary>Which phase he is in: 1, 2 or 3.</summary>
        public int Phase => phase;

        /// <summary>
        /// He has cut the wings off whoever fights him: from the cut itself, for as long
        /// as he is there. Each machine works it out from what he is doing as it sees
        /// him, so that they are gone on the tick his sword is seen to come down.
        /// </summary>
        public bool HasCut => phase >= 3 && State != Move.Spent && State != Move.Shudder && State != Move.Gut && State != Move.Bled
            && !(State == Move.Flash && then == Move.Sever) && !(State == Move.Sever && Timer < M.SeverCuts);

        /// <summary>The update of the game in which one of him last had wings cut; none yet at nothing.</summary>
        private static uint cutSeen;

        /// <summary>
        /// Nobody flies: one of him, in his third phase, has cut the wings off whoever
        /// fights him, and is still there (PisPlayer reads this where it says whether
        /// wings, boosters and flying mounts do anything; the wings themselves are not
        /// drawn: FoighScenePlayer). True for two updates after he was last seen so, on
        /// every machine by itself: a player's update comes before his.
        /// </summary>
        public static bool WingsCut => cutSeen != 0 && Main.GameUpdateCount - cutSeen <= 2;

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
                told: "His sword is turned aside and he reels; every third time he is down on a knee, open to the riposte. Not the thrown sword, the hand, the cut up out of the ground or the blow in the air.");
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
        /// A blow that would kill him leaves him on one, and his end begins (Foigh.End: Fell),
        /// which sets him to nothing itself at the last of it (Perish).
        /// </summary>
        public override bool CheckDead()
        {
            if (gone)
                return true;

            Fell();
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
            writer.Write((byte)phase);
            writer.Write((byte)MathHelper.Clamp((float)Math.Round(posture * 2f), 0f, 200f));
            writer.Write((short)hooked);

            // (How long the sword lingers in the move he is in; or 255, that it is a feint. Foigh.Wild.)
            writer.Write((byte)(feint ? 255 : Math.Clamp(linger, 0, 254)));
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
            phase = reader.ReadByte();
            posture = reader.ReadByte() * 0.5f;
            hooked = reader.ReadInt16();

            int wile = reader.ReadByte();

            feint = wile == 255;
            linger = feint ? 0 : wile;
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

        /// <summary>Nothing lands on him on the road, while he is between two places, curled up (the ball), in a passage, at his end, or with nobody left.</summary>
        private bool Untouchable => State <= Move.Begin || State == Move.Flash || State == Move.Curl || Passing || Ending || State == Move.Leave;

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

            // His third phase: whether the sword will linger in this one, or it is a feint (Foigh.Wild).
            Wiles(move);

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

            /// <summary>The passage into his second phase: his two lines answered; his third read.</summary>
            Answered,
            Sworn,

            /// <summary>His cut up landed on this player, and has thrown them into the air.</summary>
            Lifted,

            /// <summary>The passage into his third phase: his first line answered; his second and third read; his fourth read.</summary>
            Witnessed,
            Weighed,
            Shorn,

            /// <summary>Something of this player's has been turned aside on their machine since it last said so (a stream held off, or more than the cap allows): where, and by how much it wears at his posture, in tenths.</summary>
            Hold,

            /// <summary>His hook has bitten this player: whether they were stood on the ground.</summary>
            Hooked,

            /// <summary>This player has done something he reads (Foigh.Reach): which.</summary>
            Read,

            /// <summary>What fights for this player has taken too much from him (Foigh.Wild): he owes them the ball.</summary>
            Swarmed,

            /// <summary>His end (Foigh.End): a part of it answered, read or walked on this player's machine: which part, and which answer.</summary>
            Last,
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

                case Word.Answered when State == Move.Knee && from == TargetIndex:
                    Go(Move.Whet);
                    break;

                case Word.Sworn when State == Move.Oath && from == TargetIndex:
                    Go(Move.Resume);
                    break;

                case Word.Witnessed when State == Move.Shudder && from == TargetIndex:
                    Go(Move.Gut);
                    break;

                case Word.Weighed when State == Move.Bled && Timer >= M.BledLooks && from == TargetIndex:
                    StepToSever(by);
                    break;

                case Word.Shorn when State == Move.Sever && Timer >= M.SeverCuts && from == TargetIndex:
                    Go(Move.Resume);
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

                case Word.Clash when Fighting:
                    Wear(PostureMet, false);

                    if (CanReact)
                        Clashed(by);
                    break;

                case Word.Parried when Fighting:
                    // (Out of what shots can wear at it in a second: a gun's worth of them is a stream by another name.)
                    Wear(PostureStruck, true);

                    if (CanReact)
                        Guarded(from);

                    if (flag)
                        SendBack(by, a, b, n);
                    break;

                case Word.Hold when Fighting && !Passing:
                    HeldOff(from, MathHelper.Clamp(n / 10f, 0f, 60f));
                    break;

                case Word.Hooked when State == Move.HookCast && Timer >= M.HookLets:
                    Bitten(by, flag);
                    break;

                case Word.Read when Fighting && !Passing && phase >= 2:
                    Read(by, n);
                    break;

                case Word.Swarmed:
                    Swarming(from);
                    break;

                case Word.Lifted when State == Move.Upcut:
                    lifted = from;
                    break;

                // (For the part he is in, and no other: a word late for one is not the next one's.)
                case Word.Last when Ending && from == TargetIndex && n == (int)State:
                    EndAnswered(flag);
                    break;
            }
        }

        // ============================================================
        // AI
        // ============================================================

        public override void AI()
        {
            now = Timer;
            clock = Clock;

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
                clockWas = Math.Max(0f, clock - Tempo);
                owed = 0f;
                lingered = 0;
            }

            seen = State;
            grounded = NPC.velocity.Y == 0f;

            Player target = Target();

            if (Authority && Lost(target))
                return;

            bool road = State <= Move.Begin;

            NPC.dontTakeDamage = Untouchable;

            // His third phase: twice the defence, by request.
            NPC.defense = phase >= 3 ? Defense * ThirdDefense : Defense;

            // What the last tick's strikes took, against what can be taken (Foigh.Hold).
            Allowance();

            // Quiet on the road and after; the fight's own from its first step.
            Music = Fighting ? FightMusic : 0;

            // Behind the tiles while the blade is in the ground, or he is (on his knees, at his end), so the ground hides what is in it.
            NPC.behindTiles = BladeInGround || EndLow;

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
                Holds();
                Drinks();
                Reads();
                Swarms();
                Calls();
                Tow();
                LeanIn();

                if (liftedMe > 0)
                    liftedMe--;
            }

            if (TargetIsHere && (State <= Move.Begin || Passing || Ending))
            {
                // A part of the road (or of a passage, or of his end) begun in the middle of
                // this tick has its first tick still to come: the scene is asked for (it must
                // be, every tick), on that part's clock, which is at nothing.
                int kept = now;

                if (State != seen)
                    now = 0;

                if (Ending)
                    EndScene();
                else if (Passing)
                    PassageScene();
                else
                    Scene();

                now = kept;
            }

            Remember();

            // Nobody flies while he is here with wings cut: said again every tick he is (WingsCut).
            if (HasCut)
                cutSeen = Math.Max(1u, Main.GameUpdateCount);

            // And from his second phase a hit of his takes their hook and their flight for a moment (PastFirst).
            SeenLater();

            was = now;
            clockWas = clock;

            // On by a tick; or, in a move his second phase quickens, by a tick and a quarter,
            // and his third by a tick and a half (the move he is in now: one begun in the
            // middle of this tick goes by its own). Not while the sword lingers (Foigh.Wild).
            if (!Lingers())
                NPC.ai[1] += Tempo;
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
        /// By hand (/foigh second): he is left two thirds of his life, less one, so that his
        /// second phase begins as it does in the fight, on his next tick. Null when it is
        /// done; why not when it is not.
        /// </summary>
        internal static string ToSecond()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.ModNPC is not Foigh foigh)
                    continue;

                if (!foigh.Fighting)
                    return foigh.State <= Move.Begin ? "The fight has not begun: he is still on the road." : "It is over.";

                if (foigh.phase > 1)
                    return "He is in his second phase already.";

                npc.life = Math.Max(1, (int)(npc.lifeMax * SecondAt) - 1);
                npc.netUpdate = true;
                return null;
            }

            return "He is not here.";
        }

        /// <summary>
        /// By hand (/foigh third): he is left three tenths of his life, less one, so that
        /// his third phase begins as it does in the fight, on his next tick. From his
        /// first phase he is taken to be in his second already, its passage passed over
        /// (his sword is its sharp side at once): or both would be played, one after the
        /// other. Null when it is done; why not when it is not.
        /// </summary>
        internal static string ToThird()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.ModNPC is not Foigh foigh)
                    continue;

                if (!foigh.Fighting)
                    return foigh.State <= Move.Begin ? "The fight has not begun: he is still on the road." : "It is over.";

                if (foigh.phase > 2)
                    return "He is in his third phase already.";

                if (foigh.Passing)
                    return "He is between two phases: when the picture is back.";

                foigh.phase = 2;
                npc.life = Math.Max(1, (int)(npc.lifeMax * ThirdAt) - 1);
                npc.netUpdate = true;
                return null;
            }

            return "He is not here.";
        }

        /// <summary>
        /// Nobody to fight: the one he had is gone or dead. On the road he goes with them; in
        /// the fight he turns to whoever is nearest and alive, and with nobody he leaves. At
        /// his end, with nobody to see it, he is down on his face and gone (Foigh.End).
        /// </summary>
        private bool Lost(Player target)
        {
            if (target != null && !target.dead)
                return false;

            if (State == Move.Leave)
                return false;

            if (Ending)
            {
                if (State != Move.Cast && State != Move.Slump)
                    Go(Move.Slump);

                return false;
            }

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
