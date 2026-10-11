using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.DonaQuinn;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Projectiles.Base;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// The fight: what he does in each move, tick by tick, and what his sword does to whoever
    /// is in its way.
    ///
    /// The server (or the one machine there is) moves him from one move to the next. Every
    /// machine runs the move he is in for itself, from the same few numbers sent with him, so
    /// that he is in the same place doing the same thing on all of them between the server's
    /// words. And every machine works out his sword against its own player, as anything that
    /// hurts by touch is checked, and sends the hit on from there (Player.Hurt).
    ///
    /// How he holds himself in any tick of any move is asked of FoighMoves (PoseOf): the
    /// same sum for the picture, for the blade that hits, and for the trail it leaves.
    /// </summary>
    public partial class Foigh
    {
        /// <summary>The other soundtrack's second boss theme: strings and drums. One line to change.</summary>
        private const int FightMusic = MusicID.OtherworldlyBoss2;

        // ============================================================
        // NUMBERS OF THE FIGHT
        // ============================================================

        /// <summary>Walking: pixels a tick, how fast he gets up to it, and how near their middle he begins to stop (he is at rest about eight pixels nearer).</summary>
        private const float WalkSpeed = 1.15f;
        private const float WalkAccel = 0.08f;
        private const float StandOff = 62f;

        /// <summary>Within this of them, and with them no further over or under him than these, it is the sword; further, the knives and the steps.</summary>
        private const float MeleeRange = 150f;
        private const float MeleeAbove = 84f;
        private const float MeleeBelow = 70f;

        /// <summary>Well over his head: he does not walk under them, he throws.</summary>
        private const float HighOver = 150f;

        /// <summary>Ticks he walks before his first move.</summary>
        private const int FirstRest = 70;

        /// <summary>The leap back at the end of the first move: how fast back, and up.</summary>
        private const float BackSpeed = 5.6f;
        private const float BackUp = 6.9f;

        /// <summary>
        /// The leap onto them. How high it goes: onto one beside him, as high as it always
        /// did; onto one as far off as he will try for, three times that; between, by how far
        /// off they are. How long it takes follows from how high it goes. Onto one who is
        /// over him, at least this far over their feet, if he can get that high.
        /// </summary>
        private const float LeapHeight = 75f;
        private const float LeapHigher = 3f;
        private const float LeapFurthest = 480f;
        private const float LeapOver = 24f;

        /// <summary>His feet come down this far short of them: it is the point that comes down on them.</summary>
        private const float LeapShort = 40f;

        /// <summary>A leap that has not come down by now has gone wrong somewhere: he is put back on his feet.</summary>
        private const int LeapLongest = 200;

        /// <summary>
        /// From his second phase, by request ("He can jump super high"), the leap goes as
        /// high as whoever it is for: up to this (forty-six tiles), and no higher than
        /// leaves this much between his head and whatever is over him (it may top out
        /// most of a tick's rise over what was asked: LeapLift).
        /// </summary>
        private const float SuperLeap = 46f * 16f;
        private const float LeapHeadroom = 28f;

        /// <summary>The fourth move: how hard he jumps; the tick the arm is all the way back, the tick the sword leaves, the tick he is only falling.</summary>
        private const float JumpUp = 13.5f;
        private const int FourWound = 22;
        private const int FourLetsGo = 24;
        private const int FourOver = 36;

        /// <summary>After the sword: pixels a tick, how fast he gets up to it, how near its grip he takes it, and how long before he has it again regardless.</summary>
        private const float RunSpeed = 8.6f;
        private const float RunAccel = 0.6f;
        private const float TakeReach = 24f;
        private const int RunLongest = 240;

        /// <summary>A knife: pixels a tick. Fast.</summary>
        private const float KnifeSpeed = 17.5f;

        /// <summary>Of a hundred throws, this many are three knives; and after three, this many times of a hundred he comes after them.</summary>
        private const int ThreeKnives = 22;
        private const int LeapAfterKnives = 40;

        /// <summary>
        /// From his second phase, of what was picked ("knife volleys"): of a hundred
        /// throws, this many more are a fan, this many knives at once, this far apart:
        /// from the one meant for them upward at one stood on the ground, to either side
        /// of it at one off the ground. What the move's number is for one (three knives
        /// are 3, one is 1).
        /// </summary>
        private const int FanKnives = 38;
        private const int FanCount = 5;
        private static readonly float FanStep = MathHelper.ToRadians(12f);
        private const float FanArg = 5f;

        /// <summary>The turn in the third move: ticks.</summary>
        private const int RoundLength = M.ThreeSpin - M.ThreeCoil;

        /// <summary>A flash step: ticks bent into it where he stands, and ticks between the two places.</summary>
        private const int FlashGather = 3;
        private const int FlashGo = 4;
        private const int FlashTicks = FlashGather + FlashGo;

        /// <summary>Behind them: this far past their middle. In front of them: this far short of it. For their throat: this far.</summary>
        private const float BehindGap = 58f;
        private const float FrontGap = 64f;
        private const float GrabGap = 34f;

        /// <summary>Up out of the way: this far over the ground.</summary>
        private const float UpHigh = 200f;

        /// <summary>The one who drank: ticks he will still make them pay within, how near a hand has to be to close on them, and how far he sees a potion drunk.</summary>
        private const int DrankTicks = 120;
        private const float HandReach = 26f;
        private const float SeesDrink = 1600f;

        /// <summary>Flung off the sword: how fast along the way he faces, and up.</summary>
        private const float FlungAlong = 8.5f;
        private const float FlungUp = 4.5f;

        /// <summary>A word that his leap landed on somebody is still taken this many ticks after he is down.</summary>
        private const int LateStruck = 12;

        /// <summary>The blade, for a hit: this thick.</summary>
        private const float BladeWidth = 14f;

        /// <summary>
        /// How hard a blow of the sword throws whoever it lands on, as the speed the game puts
        /// on them (any enemy's touch is 4.5): a cut, the thrust, and the blow down. Standing
        /// still, that carries them about seven tiles, thirteen and twelve.
        /// </summary>
        private const float KnockCut = 5.5f;
        private const float KnockThrust = 7.5f;
        private const float KnockHard = 7f;

        /// <summary>Parried: how fast he is thrown back.</summary>
        private const float ReelBack = 5.4f;

        /// <summary>Nobody left: ticks he stands before he is gone.</summary>
        private const int LeaveTicks = 150;

        // ============================================================
        // NUMBERS OF HIS SECOND PHASE
        // ============================================================

        /// <summary>
        /// The sword's four moves in his second phase: this much faster (their own clock
        /// runs a tick and a quarter to the game's tick), and each step of them carrying
        /// him this much further. Mine: "greater speeds and ranges" is all that was asked.
        /// </summary>
        public const float SecondTempo = 1.25f;
        public const float SecondReach = 1.3f;

        /// <summary>Up close, by request, he waits a second or two between attacks.</summary>
        private const int CloseWaitLeast = 60;
        private const int CloseWaitMost = 120;

        /// <summary>From far off he mostly walks on: for this long before he thinks again.</summary>
        private const int WalkOnLeast = 40;
        private const int WalkOnMost = 70;

        /// <summary>What counts as up close is further off, as his moves reach further; and the leap onto them is tried from further (forty tiles, where it was thirty).</summary>
        private const float SecondMelee = 200f;
        private const float SecondLeapFurthest = 640f;

        /// <summary>
        /// The leap back at the end of the first move: further, and lower, so that he is
        /// down again by the tick the quicker move has him down.
        /// </summary>
        private const float SecondBackSpeed = 7.6f;
        private const float SecondBackUp = 5.7f;

        /// <summary>After the thrown sword: faster.</summary>
        private const float SecondRunSpeed = 10.6f;
        private const float SecondRunAccel = 0.75f;

        /// <summary>
        /// The run at them with the sword up: pixels a tick, how fast he gets up to it, how
        /// near their middle he stops to cut, and the longest he runs before he cuts at
        /// whatever is there.
        /// </summary>
        private const float RushSpeed = 9.5f;
        private const float RushAccel = 1.2f;
        private const float RushReach = 84f;
        private const int RushLongest = 60;

        /// <summary>
        /// The leap with the hand out. Its weight is its own, so that at its furthest it is
        /// what was asked: ten tiles forward and five up in half a second (SpringGravity
        /// and SpringOff, below). At them: where they will be when it comes down, this much
        /// of where they are going allowed for; no nearer than this, and his feet this far
        /// short of them (it is the hand that is to get there).
        /// </summary>
        private const float SpringLead = 0.7f;
        private const float SpringLeast = 40f;
        private const float SpringShort = 20f;

        /// <summary>The lowest it goes, however near they are; and over one who is over him, by this much, if that is within its five tiles.</summary>
        private const float SpringLow = 30f;
        private const float SpringOver = 20f;

        /// <summary>With somebody in his hand: how far the camera leans in on the two of them, and how fast.</summary>
        private const float HeldZoom = 1.4f;
        private const float HeldZoomRate = 0.07f;

        /// <summary>
        /// The late cut. He goes from where he stands to this far past them, and no further
        /// than this; with less than this of ground ahead of him there is nowhere to go. The
        /// line that opens is the way he went and this much more, past where he stands at
        /// the end of it, where his sword is (so that one he could not get past, their back
        /// to a wall, is on it); it lies this far over the ground, and is this thick, for a
        /// hit.
        /// </summary>
        private const float LateOver = 96f;
        private const float LateFurthest = 720f;
        private const float LateLeast = 48f;
        private const float LatePast = 24f;
        private const float LateHigh = 24f;
        private const float LateWidth = 18f;

        /// <summary>
        /// The mill: how many times round; pixels a tick he comes on at, and how fast he
        /// gets up to it; how far off he will begin it from; and the speed up, and on, that
        /// he leaves the ground at out of the last turn.
        /// </summary>
        private const int MillTurns = 3;
        private const float MillSpeed = 7.2f;
        private const float MillAccel = 1.2f;
        private const float MillReach = 380f;
        private const float MillHop = 5.2f;
        private const float MillOn = 3.2f;

        /// <summary>
        /// The cut up. The run with the sword dragged: pixels a tick, how fast he gets up
        /// to it, how near their middle he begins the cut, how near he stops running at
        /// them, and the least and the most ticks of it. Whoever the cut lands on is thrown
        /// up at this speed and a little on, lies this long when they come down by
        /// themselves, and for this many ticks his blow in the air lands on them whatever
        /// grace the cut left them.
        /// </summary>
        private const float ScrapeSpeed = 9f;
        private const float ScrapeAccel = 1f;
        private const float UpcutReach = 72f;
        private const float UpcutNear = 40f;
        private const int ScrapeLeast = 10;
        private const int ScrapeLongest = 60;
        private const float LaunchUp = 12.5f;
        private const float LaunchAlong = 1.5f;
        private const int LaunchLie = 40;
        private const int LiftedTicks = 90;

        /// <summary>
        /// In the air. A player with nothing solid this far under their feet is off the
        /// ground; he goes up after one no further off than this, along or up: to beside
        /// where they will be as his sword comes down (Ahead), this far short of their
        /// middle with his feet this far under theirs (or, with no room for that, level
        /// with them or a little over: they are stood on something; or further under: he
        /// is the taller, and beside one who hangs with their head against a ceiling there
        /// is no room for him but lower). He hangs this long (less after one he threw up
        /// himself), keeping to that place at up to this many pixels a tick until the last
        /// few ticks of it. The blow sends them down at this speed and a little on, and
        /// they lie this long where they stop.
        /// </summary>
        private const float AloftOver = 64f;
        private const float SkyReach = 720f;
        private const float SkyGap = 46f;
        private const float SkyUnder = 6f;
        private static readonly float[] SkyTries = { SkyUnder, 0f, -10f, 16f };
        private const int HangTicks = 14;
        private const int HangShort = 6;
        private const int HangCommits = 6;
        private const float HangSpeed = 9f;
        private const float SpikeDown = 15f;
        private const float SpikeAlong = 2f;
        private const int SpikeLie = 50;

        /// <summary>
        /// The blow in the air goes all the way round under him: with less than this of
        /// air under his feet it would go through the ground, and it is the mill's last
        /// blow instead, from where the sword is up, down onto the ground with him. He
        /// leaves where he hangs at the speed that has him down in this many ticks, as the
        /// sword is (by his own weight he falls this far in them from rest, less half a
        /// pixel so that he is down by then and not a tick after).
        /// </summary>
        private const float SmiteClear = 76f;
        private const int FellTicks = M.MillEndDown - M.MillEndUp;
        private const float FellFalls = Gravity * 0.5f * FellTicks * (FellTicks + 1) - 0.5f;

        // ============================================================
        // EACH MACHINE'S OWN
        // ============================================================

        /// <summary>Set once his end is over (Foigh.End, Perish): the blow that ends him is his own.</summary>
        private bool gone;

        /// <summary>The cut of his that has landed on this machine's player, or been turned aside by them: once each.</summary>
        private int struck;

        /// <summary>Where the sword points in a leap or a throw, eased, in his own frame.</summary>
        private float aimShown = 1.1f;

        /// <summary>How far he has walked, in steps, and run.</summary>
        private float gait;

        /// <summary>Ticks since a shot was last struck in the guard he is in, and how many it had struck then.</summary>
        private int since;
        private int guardsSeen;

        /// <summary>Movements that pick up where another left off: built when he goes into them.</summary>
        private FoighTrack free;
        private FoighTrack landed;
        private FoighTrack reel;
        private FoighTrack spent;
        private FoighTrack pierce;
        private FoighTrack hoist;

        /// <summary>The same, for the two that are otherwise the one who drank's: flung off the sword out of his second phase's own move, and down from its leap with nothing in his hand. None when it is the other.</summary>
        private FoighTrack flung;
        private FoighTrack empty;

        /// <summary>The cut up, from the pose he ran in; and down out of the air, from the pose he fell in.</summary>
        private FoighTrack upcut;
        private FoighTrack alight;

        /// <summary>Ticks left in which this machine's player, thrown up by his cut, is still his to strike down: his blow in the air lands on them whatever grace the cut left them.</summary>
        private int liftedMe;

        /// <summary>
        /// This machine's player, caught out of the air: where their middle was as the hand
        /// closed on them, and the ticks they have been carried since. None of it, on a
        /// machine that did not see them caught.
        /// </summary>
        private Vector2 snatchedFrom;
        private int snatched = M.SnatchTicks;

        /// <summary>What a step of the move he is in has asked for and not yet been given (Stepped): pixels along, carried into the next tick.</summary>
        private float owed;

        /// <summary>Where his feet were a tick ago.</summary>
        private Vector2 feetWas;
        private bool groundedWas = true;

        /// <summary>In a leap higher than his weight lets its speed take him: the ticks he has gone up at his fastest with his weight not let at it; and whether something over him has stopped him.</summary>
        private int risen;
        private bool stopped;

        // ============================================================
        // SMALL THINGS
        // ============================================================

        private void Fall()
        {
            NPC.velocity.Y = Math.Min(NPC.velocity.Y + Gravity, MaxFall);
        }

        private static float Toward(float from, float to, float step)
        {
            return from < to ? Math.Min(to, from + step) : Math.Max(to, from - step);
        }

        private void Face(int way)
        {
            if (!Authority || way == 0 || way == facing)
                return;

            facing = way;
            NPC.netUpdate = true;
        }

        private void FaceThem(Player target)
        {
            if (target != null && Math.Abs(target.Center.X - NPC.Center.X) > 6f)
                Face(target.Center.X > NPC.Center.X ? 1 : -1);
        }

        /// <summary>
        /// Back to walking, with this long before his next move; or, in his second phase
        /// and out of a move picked from up close, a second or two (by request).
        /// </summary>
        private void Rest(int least, int most)
        {
            if (!Authority)
                return;

            // (What he learns of them: a move of his that ended, and was not parried. Not what
            // follows his sword being turned aside, or his posture broken. Foigh.Wild.)
            if (State != Move.Reel && State != Move.Stagger && State != Move.Broken)
                LearnParry(false);

            waitingOut = phase >= 2 && close;

            // His third phase, by request: half a second to a second, wherever the move was
            // picked from; and no wait a move asks for is longer than that.
            if (phase >= 3)
                rest = waitingOut ? Main.rand.Next(ThirdWaitLeast, ThirdWaitMost + 1) : Main.rand.Next(Math.Min(least, ThirdWaitLeast), Math.Min(most, ThirdWaitMost) + 1);
            else
                rest = waitingOut ? Main.rand.Next(CloseWaitLeast, CloseWaitMost + 1) : Main.rand.Next(least, most + 1);

            close = false;

            Go(Move.Walk);
        }

        /// <summary>
        /// Whether this move is one of those his second phase quickens: the sword's four,
        /// on his feet. Not the two seconds his sword is stuck for, which were asked for as
        /// two seconds; nor anything in the air, which goes by his weight.
        /// </summary>
        private static bool Quickened(Move move)
        {
            switch (move)
            {
                case Move.One:
                case Move.TwoGather:
                case Move.TwoFree:
                case Move.TwoLanded:
                case Move.Three:
                case Move.ThreeRound:
                case Move.ThreeEnd:
                case Move.FourGather:
                case Move.FourTouch:
                case Move.FourTake:
                case Move.FourRound:
                case Move.FourEnd:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// How much of the move's clock a tick of the game is, in the move he is in: one;
        /// or in his second phase, for the moves it quickens, a quarter more; or in his
        /// third, for those and the rest of the sword's moves on his feet (Foigh.Wild),
        /// half as much again.
        /// </summary>
        private float Tempo => phase >= 3 && Wild(State) ? ThirdTempo : phase >= 2 && Quickened(State) ? SecondTempo : 1f;

        /// <summary>How far a step of the move he is in carries him, for what it is written as: all of it, or in his second phase, for the same moves, a third more.</summary>
        private float Reach => phase >= 2 && Quickened(State) ? SecondReach : 1f;

        /// <summary>How far off he will try the leap onto them from.</summary>
        private float LeapReach => phase >= 2 ? SecondLeapFurthest : LeapFurthest;

        /// <summary>
        /// How far a movement's steps carry him this tick. Where his second phase makes
        /// them longer and quicker, a step can ask for more in a tick than the fastest he
        /// goes (MaxSpeed): what it asks for beyond that is not lost but carried into the
        /// ticks after, so that the step is as long as it was written, the third more and
        /// all. (His first phase's are as they were: there the thrust loses a few pixels
        /// to the same limit.) A step is what the move's clock goes on by this tick: none,
        /// while the sword lingers (Foigh.Wild).
        /// </summary>
        private float Stepped(M.Step[] steps)
        {
            float asked = facing * Reach * (M.Advance(steps, clock + ClockStep) - M.Advance(steps, clock));

            if (Reach <= 1f)
                return asked;

            asked += owed;

            float goes = MathHelper.Clamp(asked, -MaxSpeed, MaxSpeed);

            owed = asked - goes;
            return goes;
        }

        /// <summary>
        /// Somewhere he can stand with the middle of his feet at this x, near this height:
        /// in the air with ground under it. Where there is none, a little nearer him.
        /// </summary>
        private bool Stand(float x, float nearY, out Vector2 feet)
        {
            float back = NPC.Center.X > x ? 16f : -16f;

            for (int i = 0; i < 8; i++)
            {
                if (DonaQuinnEncounter.Ground(x, nearY, NPC.width, NPC.height, 1, 10, out float ground))
                {
                    feet = new Vector2(x, ground);
                    return true;
                }

                x += back;
            }

            feet = Feet;
            return false;
        }

        /// <summary>His sword in the ground or in the air, if it is anywhere: his by his index (ai[0]), on every machine.</summary>
        private Projectile ThrownSword()
        {
            int type = ModContent.ProjectileType<FoighSwordThrown>();

            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.type == type && (int)projectile.ai[0] == NPC.whoAmI)
                    return projectile;
            }

            return null;
        }

        /// <summary>How far through a leap he is at this tick of it: from the ground, 0 to 1; from over them, the second half of it.</summary>
        private float Share(float t)
        {
            float all = Math.Max(1f, Arg);
            float share = MathHelper.Clamp(t / all, 0f, 1f);

            return above ? 0.5f + 0.5f * share : share;
        }

        /// <summary>How far the arm is drawn back, and then through the throw, at this tick of the fourth move's jump.</summary>
        private static float Wound(float t) => t <= FourWound ? t / FourWound : 1f + (t - FourWound) / 8f;

        // ============================================================
        // THE FIGHT, A TICK OF IT
        // ============================================================

        private void Fight(Player target)
        {
            NPC.noTileCollide = State == Move.Flash;

            // Two thirds of his life left, or three tenths: whatever he was doing, he stops
            // (Foigh.Passage, Foigh.Third). What is left of this tick is not that move's any more.
            if (Authority && phase < Stage && CanYield)
            {
                Yield();
                return;
            }

            switch (State)
            {
                case Move.Walk: Walk(target); break;
                case Move.One: One(target); break;

                case Move.TwoGather: TwoGather(target); break;
                case Move.TwoAir: TwoAir(); break;
                // (In his third phase it is not stuck: it is driven deeper. Foigh.Wild.)
                case Move.TwoStuck when phase >= 3: Driven(target); break;
                case Move.TwoStuck: Still(M.StuckTicks, Move.TwoFree, Arg); break;
                case Move.TwoFree: Still(M.FreeTicks, 26, 40); break;
                case Move.TwoLanded: TwoLanded(); break;

                case Move.Three: Three(); break;
                case Move.ThreeRound: Still(RoundLength, Move.ThreeEnd, 0f); break;
                case Move.ThreeEnd: ThreeEnd(target); break;

                case Move.FourGather: FourGather(target); break;
                case Move.FourAir: FourAir(target); break;
                case Move.FourFall: FourFall(); break;
                case Move.FourTouch: Still(M.TouchTicks, Move.FourRun, 0f); break;
                case Move.FourRun: FourRun(); break;
                case Move.FourTake: FourTake(); break;
                case Move.FourRound: Still(M.TwiceTicks, Move.FourEnd, 0f); break;
                case Move.FourEnd: Still(M.FourEndTicks, 40, 60); break;

                case Move.Knives: Knives(target); break;
                case Move.Flash: Flash(target); break;
                case Move.Behind: Behind(); break;
                case Move.Guard: Guard(target); break;
                case Move.Clash: Clash(); break;
                case Move.Charge: Charge(target); break;

                case Move.Grab: Grab(); break;
                case Move.Miss: Still(M.MissTicks, 30, 44); break;
                case Move.Lift: Holding(M.LiftTicks, Move.Spit); break;
                case Move.Spit: Holding(M.SpitUp, Move.Spitted); break;
                case Move.Spitted: Holding(M.SpittedTicks, Move.Fling); break;
                case Move.Fling: Fling(); break;

                case Move.Raise: Raise(target); break;
                case Move.Rush: Rush(target); break;
                case Move.Hew: Hew(); break;
                case Move.Crouch: Crouch(target); break;
                case Move.Spring: Spring(); break;
                case Move.Caught: Caught(); break;
                case Move.Pierce: Holding(M.PierceTicks, Move.Hoist); break;
                case Move.Hoist: Holding(M.HoistTicks, Move.Fling); break;

                case Move.Poise: Poise(target); break;
                case Move.Wake: Still(M.WakeTicks, 30, 50); break;

                case Move.Level: Level(target); break;
                case Move.Mill: Mill(); break;
                case Move.MillEnd: MillEnd(); break;

                case Move.Scrape: Scrape(target); break;
                case Move.Upcut: Upcut(); break;

                case Move.Hang: Hang(target); break;
                case Move.Smite: Smite(); break;
                case Move.Drop: Drop(); break;
                case Move.Alight: Still(M.AlightTicks, 30, 50); break;

                case Move.Rend: Rend(target); break;

                case Move.Gale: Gale(target); break;
                case Move.HookCast: HookCast(target); break;
                case Move.Haul: Haul(); break;
                case Move.Winch: Winch(); break;
                case Move.Reap: Reap(); break;
                case Move.HookMiss: Still(M.HookMissTicks, 22, 36); break;

                case Move.Soar: Soar(target); break;
                case Move.Wheel: Wheel(target); break;
                case Move.Curl: Curl(target); break;

                case Move.Reel: Reel(); break;
                case Move.Stagger: Stagger(); break;
                case Move.Broken: Broken(); break;

                case Move.Spent: Spent(); break;
                case Move.Knee: Knee(target); break;
                case Move.Whet: Whet(); break;
                case Move.Oath: Oath(); break;
                case Move.Resume: Resume(); break;

                case Move.Shudder: Shudder(target); break;
                case Move.Gut: Gut(); break;
                case Move.Bled: Bled(target); break;
                case Move.Sever: Sever(); break;

                case Move.Leave: Leave(); break;

                // At nothing: his end (Foigh.End).
                case Move.Dying:
                case Move.Roar:
                case Move.Fold:
                case Move.Bowed:
                case Move.Laugh:
                case Move.Asked:
                case Move.Knew:
                case Move.Mouth:
                case Move.Choir:
                case Move.Plead:
                case Move.Finish:
                case Move.Thrust:
                case Move.Cast:
                case Move.Spare:
                case Move.Scorn:
                case Move.Slump:
                    Ends();
                    break;
            }

            if (Authority && drank > 0)
            {
                drank--;
                Punish();
            }

            // His posture; held at turning something aside; losing life as fast as he can (Foigh.Hold).
            if (Authority && Fighting && !Passing)
            {
                PostureTick();
                PressTick(target);
                BreakoutTick(target);
            }

            // Out of the moves his hook is used in, nobody is on it (Foigh.Reach).
            if (Authority)
                HookTick();

            // His third phase: a cut that ends with them out of its reach is let go at them (Foigh.Wild).
            if (Authority)
                LetGoTick(target);

            // Owed the ball, and free for it (Foigh.Wild).
            if (Authority)
                SwarmTick();
        }

        /// <summary>A move he stands through: where he is, until it is over, and then the next.</summary>
        private void Still(int ticks, Move next, float arg)
        {
            Fall();
            NPC.velocity.X *= 0.6f;

            if (Authority && now >= ticks)
                Go(next, arg);
        }

        /// <summary>The same, and then back to walking.</summary>
        private void Still(int ticks, int restLeast, int restMost)
        {
            Fall();
            NPC.velocity.X *= 0.6f;

            if (Authority && now >= ticks)
                Rest(restLeast, restMost);
        }

        // ============================================================
        // WALKING, AND WHAT HE DOES NEXT
        // ============================================================

        private void Walk(Player target)
        {
            Fall();

            if (target == null)
            {
                NPC.velocity.X *= 0.8f;
                return;
            }

            float dx = target.Center.X - NPC.Center.X;

            if (Math.Abs(dx) > 10f)
                Face(dx > 0f ? 1 : -1);

            float want = grounded && Math.Abs(dx) > StandOff ? facing * WalkSpeed : 0f;

            NPC.velocity.X = Toward(NPC.velocity.X, want, WalkAccel);

            if (!Authority || !grounded)
                return;

            if (--rest > 0)
                return;

            Choose(target);
        }

        /// <summary>One of these by its weight; the one he did last is a quarter as likely (but a knife, or walking on).</summary>
        private Move Pick(params (Move Move, float Weight)[] of)
        {
            float all = 0f;

            for (int i = 0; i < of.Length; i++)
            {
                if (of[i].Move == last && of[i].Move != Move.Knives && of[i].Move != Move.Walk)
                    of[i].Weight *= 0.25f;

                all += of[i].Weight;
            }

            float roll = Main.rand.NextFloat() * all;

            for (int i = 0; i < of.Length; i++)
            {
                roll -= of[i].Weight;

                if (roll <= 0f && of[i].Weight > 0f)
                    return of[i].Move;
            }

            return of[0].Move;
        }

        /// <summary>
        /// His next move, by where they are: in reach, one of the sword's four; over his head,
        /// what he can throw, or a leap; anywhere else, the knives, the step behind them, or
        /// the leap if they are near enough for it.
        /// </summary>
        private void Choose(Player target)
        {
            Vector2 d = target.Center - NPC.Center;
            float far = Math.Abs(d.X);

            // Whatever he had been waiting out, it is over.
            waitingOut = false;

            bool second = phase >= 2;
            bool level = d.Y > -MeleeAbove && d.Y < MeleeBelow;
            bool near = far <= (second ? SecondMelee : MeleeRange) && level;
            bool high = d.Y < -HighOver;
            bool leaps = far < LeapReach;

            Move pick;

            if (!second)
            {
                float leapWeight = leaps ? 2f : 0f;

                if (near)
                    pick = Pick((Move.One, 3f), (Move.Three, 3f), (Move.TwoGather, 2f), (Move.FourGather, 2f));
                else if (high)
                    pick = Pick((Move.Knives, 4f), (Move.FourGather, 2.5f), (Move.TwoGather, leapWeight));
                else
                    pick = Pick((Move.Knives, 5.5f), (Move.Behind, 2.5f), (Move.TwoGather, leapWeight));
            }
            else
            {
                // His second phase, by request. So that it is not his first over again,
                // what is its own comes first everywhere: up close the hand, the cut up, the
                // mill and the late cut, and his first phase's four after them; from far off
                // he mostly walks on, leaps more often, seldom throws, and has the late cut
                // and the mill to close with (along the ground, at one who is about on it
                // with him); and whoever is off the ground, or over his head, he can go up
                // after. (Over his head he cannot walk to them.)
                bool aloft = Aloft(target);
                bool sky = far < SkyReach && Math.Abs(d.Y) < SkyReach;
                bool late = level && far < LateFurthest - LateOver;
                bool mills = level && far < MillReach;

                // His third phase has one more, by request: the three cuts let go of
                // (Foigh.Third). Mostly from further off, wherever they are, over his
                // head with it (each goes at them); less often up close. (Last of what
                // he picks from, and nothing in his second phase: what he rolls there is
                // as it was.)
                float rends = phase < 3 ? 0f : near ? RendNear : d.Length() < RendReach ? RendFar : 0f;

                // What reaches them wherever they have got to (Foigh.Reach), by request:
                // the wind, his second phase's own, as his third's cuts are picked; the
                // hook, at one it can get to; and his knives oftener than they were.
                bool windy = phase == 2 && d.Length() < GaleReach;
                bool hooks = Hooks(target);

                // His third phase's (Foigh.Wild): the wheel, last of what he picks from;
                // and everything he picks from weighed by what he has learned of them
                // (Swayed: in his second phase it is as it is given, and what he rolls
                // there is as it was). He learns from his second phase on.
                bool wheels = Wheels(target);

                LearnPlace(aloft || Hooked(target) || high, !near);

                if (near)
                {
                    pick = Pick(Swayed(true, (Move.Raise, 3f), (Move.Level, 3f), (Move.Scrape, 2f), (Move.Poise, 1.5f), (Move.Hang, aloft ? 3f : 0f),
                        (Move.One, 1.2f), (Move.Three, 1.2f), (Move.TwoGather, 0.8f), (Move.FourGather, 0.8f), (Move.Rend, rends),
                        (Move.Gale, windy ? GaleNear : 0f), (Move.Soar, wheels ? WheelNear : 0f)));
                }
                else if (high)
                {
                    pick = Pick(Swayed(false, (Move.Hang, sky ? 4.5f : 0f), (Move.TwoGather, leaps ? 2.5f : 0f), (Move.FourGather, 2f), (Move.Knives, KnivesAbove), (Move.Rend, rends),
                        (Move.Gale, windy ? GaleAbove : 0f), (Move.HookCast, hooks ? HookAbove : 0f), (Move.Soar, wheels ? WheelAbove : 0f)));
                }
                else
                {
                    pick = Pick(Swayed(false, (Move.Walk, phase >= 3 ? ThirdWalksOn : 9.5f), (Move.TwoGather, leaps ? 4.5f : 0f), (Move.Poise, late ? 1.5f : 0f), (Move.Level, mills ? 1.2f : 0f),
                        (Move.Hang, aloft && sky ? 2.5f : 0f), (Move.Behind, 0.8f), (Move.Knives, KnivesFar), (Move.Rend, rends),
                        (Move.Gale, windy ? GaleFar : 0f), (Move.HookCast, !hooks ? 0f : aloft ? HookAloft : HookFar), (Move.Soar, wheels ? WheelFar : 0f)));
                }

                if (pick == Move.Walk)
                {
                    // On, at them: nothing begun, and what he did last is still what he did last.
                    rest = phase >= 3 ? Main.rand.Next(ThirdWalkOnLeast, ThirdWalkOnMost + 1) : Main.rand.Next(WalkOnLeast, WalkOnMost + 1);
                    return;
                }
            }

            close = second && near;
            last = pick;
            FaceThem(target);

            switch (pick)
            {
                case Move.Knives:
                {
                    // (One roll, as it was: in his first phase what it comes to is as it was.)
                    int roll = Main.rand.Next(100);

                    Go(Move.Knives, roll < ThreeKnives ? 3f : second && roll < ThreeKnives + FanKnives ? FanArg : 1f);
                    break;
                }

                case Move.Behind:
                    StepBehind(target);
                    break;

                case Move.Hang:
                    // Up after them; or, with no room beside them, a knife.
                    if (!SkyTo(target, false))
                    {
                        last = Move.Knives;
                        Go(Move.Knives, 1f);
                    }
                    break;

                default:
                    Go(pick);
                    break;
            }
        }

        /// <summary>Whether a player is off the ground: nothing solid for a way under their feet.</summary>
        internal static bool Aloft(Player player)
        {
            return !Collision.SolidCollision(player.BottomLeft, player.width, (int)AloftOver);
        }

        // ============================================================
        // ONE
        // ============================================================

        private void One(Player target)
        {
            Fall();

            // Before the thrust he looks for them again: gone round behind him in the two
            // cuts, they are not behind the point.
            if (At(M.OneOver + 4))
                FaceThem(target);

            // The sword up, and it is a feint (his third phase's: Foigh.Wild): he is gone.
            if (Feinted(target))
                return;

            // (The leap back: by the tempo the move runs at, so that he is down as it has him down.)
            float back = phase >= 3 ? ThirdBackSpeed : phase >= 2 ? SecondBackSpeed : BackSpeed;
            float up = phase >= 3 ? ThirdBackUp : phase >= 2 ? SecondBackUp : BackUp;

            if (now < M.OneLeap + 2)
            {
                NPC.velocity.X = Stepped(M.OneSteps);
            }
            else if (At(M.OneLeap + 2))
            {
                NPC.velocity = new Vector2(-facing * back, -up);
            }
            else if (grounded)
            {
                NPC.velocity.X *= 0.72f;
            }
            else
            {
                NPC.velocity.X = -facing * back;
            }

            if (Authority && now >= M.OneTicks)
                Rest(40, 62);
        }

        // ============================================================
        // TWO
        // ============================================================

        private void TwoGather(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            if (Authority && now >= M.TwoCrouch)
                Launch(target);
        }

        /// <summary>
        /// Off the ground at them, the server's: an arc that brings his feet down a little
        /// short of where they will be (a third of where they are going), so that the point
        /// comes down on them. The further off they are, the higher it goes
        /// (<see cref="LeapHigh"/>), and the longer it takes.
        /// </summary>
        private void Launch(Player target)
        {
            Vector2 feet = Feet;
            Vector2 to = target != null ? new Vector2(target.Center.X, target.Bottom.Y) : feet + new Vector2(facing * 200f, 0f);

            // Up to them if they are over him; never down after them.
            float rise = Math.Max(0f, feet.Y - to.Y);
            float high = phase >= 2
                ? LeapHigh(Math.Abs(to.X - feet.X), rise, Math.Min(SuperLeap, Headroom(feet, SuperLeap + NPC.height + LeapHeadroom) - NPC.height - LeapHeadroom))
                : LeapHigh(Math.Abs(to.X - feet.X), rise);

            // To the top of it, and from there down to where they stand (or, with them over
            // the top of it, no further than the top).
            float fall = Math.Max(0f, high - rise);
            int ticks = Math.Max(1, (int)Math.Round(Math.Sqrt(2f * high / Gravity) + Math.Sqrt(2f * fall / Gravity)));

            // Higher than his own weight lets the fastest he goes take him (MaxSpeed): up
            // at that fastest for as long as it takes, and down no faster.
            int boost = LeapLift(LeapOff(high));

            if (boost > 0)
                ticks = boost + Coast.Ticks + FallTicks(boost * MaxSpeed + Coast.Rise - rise);

            if (target != null)
                to.X += target.velocity.X * ticks * 0.35f;

            float dx = MathHelper.Clamp(to.X - feet.X, -LeapReach - 80f, LeapReach + 80f);

            dx -= Math.Sign(dx) * Math.Min(LeapShort, Math.Abs(dx) * 0.5f);

            leap = new Vector2(dx / ticks, -LeapOff(high));
            above = false;
            landedHit = false;

            Face(dx >= 0f ? 1 : -1);
            Go(Move.TwoAir, ticks);
        }

        /// <summary>
        /// How high his feet go in a leap onto somebody this far off along the ground and
        /// this far over him: from what it always was to three times that, by how far off;
        /// and over their feet by a little, if that is higher and he can get that high.
        /// </summary>
        internal static float LeapHigh(float far, float rise) => LeapHigh(far, rise, LeapHeight * LeapHigher);

        /// <summary>The same, with the most it may be given (never less than the least it ever is).</summary>
        internal static float LeapHigh(float far, float rise, float most)
        {
            float high = LeapHeight * MathHelper.Lerp(1f, LeapHigher, MathHelper.Clamp(far / LeapFurthest, 0f, 1f));

            return MathHelper.Clamp(Math.Max(high, rise + LeapOver), LeapHeight, Math.Max(LeapHeight, most));
        }

        /// <summary>
        /// What his own weight does to the fastest he goes up (MaxSpeed), a tick at a
        /// time, as Fall does it: how far he still rises from there, and in how many
        /// ticks.
        /// </summary>
        private static readonly (float Rise, int Ticks) Coast = Coasted();

        private static (float Rise, int Ticks) Coasted()
        {
            float speed = MaxSpeed - Gravity;
            float rise = 0f;
            int ticks = 0;

            while (speed > 0f)
            {
                rise += speed;
                speed -= Gravity;
                ticks++;
            }

            return (rise, ticks);
        }

        /// <summary>
        /// A leap given more speed up than he can have (the fastest tiles can stop:
        /// MaxSpeed) goes up at that fastest, his weight not let at it, for this many
        /// ticks before it is: so that it tops out as high as the speed it was given
        /// says, or a little over. None, for a leap he can make by its speed alone (every
        /// one of his first phase's).
        /// </summary>
        internal static int LeapLift(float off)
        {
            if (off - Gravity <= MaxSpeed)
                return 0;

            float high = (off - Gravity * 0.5f) * (off - Gravity * 0.5f) / (2f * Gravity);

            return Math.Max(1, (int)Math.Ceiling((high - Coast.Rise) / MaxSpeed));
        }

        /// <summary>The ticks he takes to fall this far from rest, by his own weight and no faster than he falls (Fall).</summary>
        internal static int FallTicks(float far)
        {
            float speed = 0f;
            int ticks = 0;

            while (far > 0f && ticks < LeapLongest)
            {
                speed = Math.Min(speed + Gravity, MaxFall);
                far -= speed;
                ticks++;
            }

            return ticks;
        }

        /// <summary>
        /// How much room there is over these feet, as far up as <paramref name="furthest"/>:
        /// to the first solid thing over where he stands (a platform is not one: he goes
        /// up through those).
        /// </summary>
        private float Headroom(Vector2 feet, float furthest)
        {
            int left = (int)((feet.X - NPC.width * 0.5f) / 16f);
            int right = (int)((feet.X + NPC.width * 0.5f - 1f) / 16f);
            int from = (int)((feet.Y - 1f) / 16f);
            int to = (int)((feet.Y - furthest) / 16f);

            for (int j = from; j >= to; j--)
            {
                for (int i = left; i <= right; i++)
                {
                    if (!WorldGen.InWorld(i, j, 2))
                        return MathHelper.Clamp(feet.Y - (j + 1) * 16f, 0f, furthest);

                    Tile tile = Main.tile[i, j];

                    if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                        return MathHelper.Clamp(feet.Y - (j + 1) * 16f, 0f, furthest);
                }
            }

            return furthest;
        }

        /// <summary>
        /// The speed up he is given to top out this high over where he left the ground: he
        /// has it for the tick it is given in, loses his weight from it before he is moved,
        /// and again every tick after. At the most there is, that first tick's rise is a
        /// hair under the fastest tiles can stop (MaxSpeed).
        /// </summary>
        internal static float LeapOff(float high) => Gravity * 0.5f + (float)Math.Sqrt(2f * Gravity * high);

        private void TwoAir()
        {
            // The first tick of it, on every machine: off the ground.
            if (fresh)
                NPC.velocity = leap;

            // (A leap higher than his weight lets its speed take him: up at the fastest
            // he goes, for as long as it takes. From the ground only.)
            int boost = above ? 0 : LeapLift(-leap.Y);

            // Stopped on the way up by something over him (the game leaves him next to
            // no speed, a hair of it downward: NPC.Collision_MoveWhileDry, 1.4.4): that
            // is as high as this one goes, and his weight has him from there.
            if (fresh)
            {
                stopped = false;
                risen = 0;
            }
            else if (risen > 0 && risen < boost && NPC.velocity.Y > -1f)
            {
                stopped = true;
            }

            if (risen < boost && !stopped)
            {
                NPC.velocity.Y = -MaxSpeed;
                risen++;
            }
            else
            {
                Fall();
            }

            if (!Authority)
                return;

            if (now > 4 && grounded)
            {
                Go(landedHit ? Move.TwoLanded : Move.TwoStuck, aimShown);
                return;
            }

            if (now >= LeapLongest)
                Rest(20, 30);
        }

        /// <summary>Down on them: straight on into something else, from where the sword is, low and ahead of him.</summary>
        private void TwoLanded()
        {
            Fall();
            NPC.velocity.X *= 0.6f;

            if (!Authority || now < M.LandedTicks)
                return;

            if (Main.rand.Next(5) < 3)
            {
                // The cut up, the thrust and the leap back: his first move, from its second cut.
                last = Move.One;
                Go(Move.One);
                Timer = M.OneRest - 3;
            }
            else
            {
                last = Move.FourGather;
                Go(Move.FourRound);
            }
        }

        // ============================================================
        // THREE
        // ============================================================

        private void Three()
        {
            Fall();

            // The sword up, and it is a feint (his third phase's: Foigh.Wild): he is gone.
            if (Feinted(Target()))
                return;

            NPC.velocity.X = Stepped(M.ThreeSteps);

            if (Authority && now >= M.ThreeCoil)
                Go(Move.ThreeRound);
        }

        private void ThreeEnd(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.6f;

            if (Authority && At(M.ThreeKnife - M.ThreeSpin))
                ThrowKnife(target, true);

            if (Authority && now >= M.ThreeTicks - M.ThreeSpin)
                Rest(36, 56);
        }

        // ============================================================
        // FOUR
        // ============================================================

        private void FourGather(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            if (Authority && now >= M.FourCrouch)
            {
                FaceThem(target);
                Go(Move.FourAir);
            }
        }

        private void FourAir(Player target)
        {
            if (fresh)
                NPC.velocity = new Vector2(0f, -JumpUp);

            Fall();
            NPC.velocity.X = 0f;

            if (Authority && At(FourLetsGo) && hasSword)
                ThrowSword(target);

            if (Authority && now >= FourOver)
                Go(Move.FourFall);
        }

        /// <summary>
        /// The sword leaves his hand, the server's: from where its point is, straight at where
        /// they will be when it gets there.
        /// </summary>
        private void ThrowSword(Player target)
        {
            FoighRig.Frame at = FoighRig.Build(PoseOf(State, clock), Feet, facing);

            Vector2 to = target != null ? target.Center : at.Tip + new Vector2(facing * 200f, 200f);

            if (target != null)
                to += target.velocity * (Vector2.Distance(at.Tip, to) / FoighSwordThrown.Speed) * 0.5f;

            Vector2 way = (to - at.Tip).SafeNormalize(new Vector2(facing, 1f));

            // Never up, never straight down his own front: thrown from over them, at them.
            if (way.Y < 0.12f)
                way = new Vector2(way.X, 0.12f).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), at.Tip, way * FoighSwordThrown.Speed,
                ModContent.ProjectileType<FoighSwordThrown>(), 0, 0f, Main.myPlayer, NPC.whoAmI);

            hasSword = false;
            NPC.netUpdate = true;
        }

        private void FourFall()
        {
            Fall();

            if (Authority && now > 2 && grounded)
                Go(Move.FourTouch);

            if (Authority && now >= LeapLongest)
                Go(Move.FourTouch);
        }

        /// <summary>
        /// After it. He has it again when he is at it and it is in the ground; and regardless,
        /// if it is gone, or he cannot get to it in time.
        /// </summary>
        private void FourRun()
        {
            Fall();

            Projectile sword = ThrownSword();

            if (sword == null)
            {
                NPC.velocity.X *= 0.6f;

                if (Authority)
                {
                    hasSword = true;
                    NPC.netUpdate = true;
                    Go(Move.FourRound);
                }

                return;
            }

            float dx = FoighSwordThrown.GripOf(sword).X - NPC.Center.X;
            bool there = Math.Abs(dx) <= TakeReach;

            if (!there)
                Face(dx > 0f ? 1 : -1);

            NPC.velocity.X = there ? NPC.velocity.X * 0.5f
                : Toward(NPC.velocity.X, facing * (phase >= 2 ? SecondRunSpeed : RunSpeed), phase >= 2 ? SecondRunAccel : RunAccel);

            if (!Authority)
                return;

            if (there && grounded && FoighSwordThrown.InGround(sword))
            {
                // Turned so that it is behind his shoulder: where the hand goes for it.
                Go(Move.FourTake);
            }
            else if (now >= RunLongest)
            {
                sword.Kill();
                hasSword = true;
                NPC.netUpdate = true;
                Go(Move.FourRound);
            }
        }

        private void FourTake()
        {
            Fall();
            NPC.velocity.X *= 0.5f;

            if (!Authority || now < M.TakeTicks)
                return;

            ThrownSword()?.Kill();

            hasSword = true;
            NPC.netUpdate = true;
            Go(Move.FourRound);
        }

        // ============================================================
        // THE KNIVES
        // ============================================================

        private void Knives(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            // (A fan is one throw: all of them leave the hand at once.)
            bool fan = Fanned;
            int count = Throws;

            for (int k = 0; k < count; k++)
            {
                if (At(k * M.KnifeEvery))
                    FaceThem(target);

                if (!Authority || !At(M.KnifeAt(k)))
                    continue;

                if (!fan)
                {
                    ThrowKnife(target, false);
                    continue;
                }

                // At one stood on the ground the lowest of them is the one meant for them and
                // the rest go over it (under it there is only the floor at his own feet); at
                // one off the ground the middle one is.
                bool over = target != null && !Aloft(target);

                for (int i = 0; i < FanCount; i++)
                    ThrowKnife(target, false, (over ? i : i - (FanCount - 1) * 0.5f) * FanStep, over);
            }

            if (!Authority || now < M.KnifeLength(count))
                return;

            if (fan)
            {
                Rest(34, 54);
                return;
            }

            // After three, now and then, he comes after them.
            if (count >= 3 && target != null && Math.Abs(target.Center.X - NPC.Center.X) < LeapReach && Main.rand.Next(100) < LeapAfterKnives)
            {
                last = Move.TwoGather;
                FaceThem(target);
                Go(Move.TwoGather);
                return;
            }

            if (count >= 3)
                Rest(46, 70);
            else
                Rest(28, 48);
        }

        /// <summary>The knives he is throwing are a fan (the move's number says so).</summary>
        private bool Fanned => Arg >= FanArg - 0.5f;

        /// <summary>How many times his hand goes back and out in the throw he is in: once for one knife or a fan, three times for three.</summary>
        private int Throws => Fanned ? 1 : Math.Max(1, (int)Arg);

        /// <summary>
        /// A knife out of one of his hands, the server's: at where they will be, half of
        /// where they are going allowed for; or that far round from there (a fan's), and
        /// then, if it is to go over them, upward whichever side of him they are.
        /// </summary>
        private void ThrowKnife(Player target, bool nearHand, float turn = 0f, bool upward = false)
        {
            FoighRig.Frame at = FoighRig.Build(PoseOf(State, clock), Feet, facing);
            Vector2 hand = nearHand ? at.NearFist : at.FarFist;

            Vector2 to = target != null ? target.Center : hand + new Vector2(facing * 300f, 0f);

            if (target != null)
                to += target.velocity * (Vector2.Distance(hand, to) / KnifeSpeed) * 0.5f;

            Vector2 way = (to - hand).SafeNormalize(new Vector2(facing, 0f));

            // (Round with the clock is down for one to his right and up for one to his left.)
            if (turn != 0f)
                way = way.RotatedBy(upward && way.X >= 0f ? -turn : turn);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), hand, way * KnifeSpeed,
                ModContent.ProjectileType<FoighDagger>(), 0, 0f, Main.myPlayer, NPC.whoAmI);
        }

        // ============================================================
        // THE FLASH STEP, AND BEHIND THEM
        // ============================================================

        /// <summary>From where he is to standing here, the server's, and what he does there.</summary>
        private void FlashTo(Vector2 feet, Move next, float arg = 0f)
        {
            if (!Authority)
                return;

            stepFrom = NPC.position;
            stepTo = new Vector2(feet.X - NPC.width * 0.5f, feet.Y - NPC.height);
            then = next;
            thenArg = arg;

            Go(Move.Flash);
        }

        private void Flash(Player target)
        {
            NPC.velocity = Vector2.Zero;

            // The hand that follows wears the star from the moment he goes; and so does the blow in the air, and the break-out's cut.
            if (then == Move.Grab || then == Move.Hang || (then == Move.Behind && thenArg > 0.5f))
                NPC.PAD().CCBypass = true;

            if (now < FlashGather)
            {
                NPC.position = stepFrom;
            }
            else
            {
                float t = MathHelper.Clamp((now - FlashGather + 1) / (float)FlashGo, 0f, 1f);
                NPC.position = Vector2.Lerp(stepFrom, stepTo, Easing.Smoother(t));
            }

            if (Authority && now >= FlashTicks)
                Arrive(target);
        }

        /// <summary>There, the server's: facing them, and into what he came to do.</summary>
        private void Arrive(Player target)
        {
            NPC.position = stepTo;
            NPC.velocity = Vector2.Zero;

            Player them = then == Move.Grab ? Held() ?? target : target;

            // (Out of the late cut's step he has gone past them, and stands as it left him: his back to them.)
            if (then != Move.Wake)
                FaceThem(them);

            if (then != Move.TwoAir)
            {
                Go(then, thenArg);

                // The break-out's: the cut comes a fifth of a second after he lands, not half a second.
                if (then == Move.Behind && thenArg > 0.5f)
                    Timer = M.BehindWait - BreakoutWait;

                return;
            }

            // Up out of the way, and down on them from there.
            Vector2 feet = Feet;
            Vector2 to = them != null ? new Vector2(them.Center.X, them.Bottom.Y) : feet + new Vector2(facing * 120f, UpHigh);

            float drop = Math.Max(48f, to.Y - feet.Y);
            int ticks = (int)Math.Ceiling(Math.Sqrt(2f * drop / Gravity));

            if (them != null)
                to.X += them.velocity.X * ticks * 0.35f;

            float dx = to.X - feet.X;

            dx -= Math.Sign(dx) * Math.Min(LeapShort, Math.Abs(dx) * 0.5f);

            leap = new Vector2(MathHelper.Clamp(dx / ticks, -14f, 14f), 0.5f);
            above = true;
            landedHit = false;

            Face(dx >= 0f ? 1 : -1);
            Go(Move.TwoAir, ticks);
        }

        /// <summary>To behind them, the server's, if there is anywhere to stand there; knives, if there is not.</summary>
        private void StepBehind(Player target)
        {
            int side = target.Center.X >= NPC.Center.X ? 1 : -1;

            if (!Stand(target.Center.X + side * BehindGap, target.Bottom.Y, out Vector2 feet)
                || Math.Abs(feet.X - target.Center.X) < 24f)
            {
                last = Move.Knives;
                Go(Move.Knives, 1f);
                return;
            }

            anchor = Feet;
            FlashTo(feet, Move.Behind);
        }

        private void Behind()
        {
            Fall();
            NPC.velocity.X = Stepped(M.BehindSteps);

            // The break-out's (Foigh.Hold): it wears the star, and he stays where it leaves him.
            bool brokeOut = Arg > 0.5f;

            if (brokeOut)
                NPC.PAD().CCBypass = true;

            if (!Authority || now < M.BehindTicks)
                return;

            if (brokeOut)
            {
                Rest(22, 36);
                return;
            }

            // Back to where he stood.
            rest = Main.rand.Next(34, 54);

            if (Stand(anchor.X, anchor.Y, out Vector2 feet))
                FlashTo(feet, Move.Walk);
            else
                Go(Move.Walk);
        }

        // ============================================================
        // THE ONE WHO DRANK
        // ============================================================

        /// <summary>The player in his hand or on his sword, or the one he has gone for; null if none.</summary>
        private Player Held()
        {
            if (held < 0 || held >= Main.maxPlayers)
                return null;

            Player player = Main.player[held];
            return player.active && !player.dead ? player : null;
        }

        /// <summary>Whether he is free to go for someone who drank: on his feet with the sword, walking, or at the tail of something.</summary>
        private bool CanPunish
        {
            get
            {
                if (!hasSword || !grounded)
                    return false;

                switch (State)
                {
                    case Move.Walk:
                    case Move.Guard:
                    case Move.Clash:
                    case Move.Knives:
                    case Move.FourEnd:
                        return true;

                    case Move.One:
                        return Timer >= M.OneTicks - 10;

                    case Move.ThreeEnd:
                        return Timer >= 20;

                    case Move.TwoFree:
                        return Timer >= 22;

                    case Move.Behind:
                        return Timer >= M.BehindCut + 8;

                    default:
                        return false;
                }
            }
        }

        /// <summary>Somebody drank, and he is free: to them, the server's, with the hand out.</summary>
        private void Punish()
        {
            if (!CanPunish)
                return;

            Player who = drankBy >= 0 && drankBy < Main.maxPlayers ? Main.player[drankBy] : null;

            if (who == null || !who.active || who.dead || Vector2.Distance(who.Center, NPC.Center) > SeesDrink * 1.5f)
            {
                drank = 0;
                return;
            }

            int side = who.Center.X >= NPC.Center.X ? 1 : -1;
            Vector2 feet = new Vector2(who.Center.X - side * GrabGap, who.Bottom.Y);

            // Beside them where they are, in the air if that is where they are; in a wall, the nearest ground.
            if (Collision.SolidCollision(new Vector2(feet.X - NPC.width * 0.5f, feet.Y - NPC.height), NPC.width, NPC.height)
                && !Stand(feet.X, feet.Y, out feet))
                return;

            drank = 0;
            held = who.whoAmI;
            TargetIndex = who.whoAmI;
            NPC.target = who.whoAmI;
            last = Move.Grab;

            FlashTo(feet, Move.Grab);
        }

        private void Grab()
        {
            Fall();
            NPC.velocity.X *= 0.5f;
            NPC.PAD().CCBypass = true;

            if (!Authority)
                return;

            if (At(M.GrabCloses))
            {
                Player prey = Held();

                if (prey != null && HandOn(prey))
                {
                    Go(Move.Lift);
                    return;
                }

                Release();
            }

            if (now >= M.GrabTicks)
                Go(Move.Miss);
        }

        /// <summary>Whether the bottom hand, where it is now, is on this player.</summary>
        private bool HandOn(Player prey)
        {
            Vector2 hand = FoighRig.Build(PoseOf(State, clock), Feet, facing).FarFist;
            Rectangle body = prey.Hitbox;

            body.Inflate((int)HandReach, (int)HandReach);
            return body.Contains(hand.ToPoint());
        }

        /// <summary>Holding them: until the move is over, or there is nobody in his hand.</summary>
        private void Holding(int ticks, Move next)
        {
            Fall();
            NPC.velocity.X *= 0.5f;

            if (!Authority)
                return;

            if (Held() == null)
            {
                Release();
                Rest(30, 44);
                return;
            }

            if (now >= ticks)
                Go(next);
        }

        private void Fling()
        {
            Fall();
            NPC.velocity.X *= 0.5f;

            if (!Authority)
                return;

            // Off the sword by now, on their own machine (Carry stops asking at FlingLets).
            if (now >= M.FlingLets + 6)
                Release();

            if (now >= M.FlingTicks)
                Rest(50, 70);
        }

        /// <summary>
        /// Where the one he has hold of is, and how they lie: hanging from the bottom hand;
        /// thrown up over him as the sword comes up; on the blade, across it.
        /// </summary>
        private bool Carried(out Vector2 middle, out float turn)
        {
            middle = Vector2.Zero;
            turn = 0f;

            const float along = 0.6f;
            const int rise = M.LiftTicks - M.LiftThrows + M.SpitUp;

            switch (State)
            {
                case Move.Lift when now < M.LiftThrows:
                    middle = frame.FarFist + new Vector2(facing * 3f, 14f);
                    return true;

                case Move.Lift:
                case Move.Spit:
                {
                    float done = State == Move.Lift ? now - M.LiftThrows : M.LiftTicks - M.LiftThrows + now;
                    float u = Easing.Smoother(done / rise);

                    Vector2 from = frame.FarFist + new Vector2(facing * 3f, 14f);
                    Vector2 onBlade = Vector2.Lerp(frame.Guard, frame.Tip, along);

                    middle = Vector2.Lerp(from, onBlade, u) + new Vector2(0f, -54f * (float)Math.Sin(u * MathHelper.Pi));
                    turn = facing * u * MathHelper.PiOver2;
                    return true;
                }

                case Move.Spitted:
                case Move.Fling when now < M.FlingLets && flung == null:
                    middle = Vector2.Lerp(frame.Guard, frame.Tip, along);
                    turn = frame.Blade + MathHelper.Pi;
                    return true;

                // His second phase's own: by the throat, in the bottom hand, all the way
                // down and while he pulls them in and the sword goes through them; then on
                // the sword, near its guard, tipped back as it is raised.
                case Move.Caught:
                case Move.Pierce:
                    middle = InHand();

                    // Pulled in to it, from where the hand closed on them.
                    if (snatched + 1 < M.SnatchTicks)
                        middle = Vector2.Lerp(snatchedFrom, middle, Easing.Apply(Ease.PolyOut, (snatched + 1f) / M.SnatchTicks, 2));

                    turn = State == Move.Pierce ? -facing * 0.1f * MathHelper.Clamp(now / (float)M.PierceDraws, 0f, 1f) : 0f;
                    return true;

                case Move.Hoist:
                {
                    float u = Easing.Smoother(MathHelper.Clamp(now / 8f, 0f, 1f));

                    middle = Vector2.Lerp(InHand(), OnSword(), u);
                    turn = Tipped(u);
                    return true;
                }

                case Move.Fling when now < M.FlingLets:
                    middle = OnSword();
                    turn = Tipped(1f);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Where whoever is in his bottom hand hangs from it: their middle, under his fist at their throat.</summary>
        private Vector2 InHand() => frame.FarFist + new Vector2(facing * M.HeldBy.X, M.HeldBy.Y);

        /// <summary>Where whoever the sword has gone through is on it: near its guard.</summary>
        private Vector2 OnSword() => Vector2.Lerp(frame.Guard, frame.Tip, M.HoistAlong);

        /// <summary>
        /// How whoever is on the sword hangs on it: from leaning on him a little, as they
        /// were held, to tipped back by half of what the blade has been raised since it
        /// went through them.
        /// </summary>
        private float Tipped(float share)
        {
            float raised = MathHelper.WrapAngle(frame.Blade - (facing > 0 ? MathHelper.ToRadians(M.PierceBlade) : MathHelper.Pi - MathHelper.ToRadians(M.PierceBlade)));

            return MathHelper.Lerp(-facing * 0.1f, raised * 0.5f, share);
        }

        /// <summary>
        /// This machine's player, if it is them he has: put where he has them, every tick
        /// (FoighGripPlayer), and at the tick he lets go of them, thrown the way the sword is
        /// swung.
        /// </summary>
        private void Carry()
        {
            if (Main.dedServ || held != Main.myPlayer)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead)
                return;

            FoighGripPlayer grip = me.GetModPlayer<FoighGripPlayer>();

            if (State == Move.Fling && At(M.FlingLets))
            {
                grip.LetGo(NPC.whoAmI, new Vector2(facing * FlungAlong, -FlungUp));
                return;
            }

            if (Carried(out Vector2 middle, out float turn))
            {
                grip.Hold(NPC.whoAmI, middle, turn);

                if (snatched < M.SnatchTicks)
                    snatched++;
            }
        }

        /// <summary>The sword through this machine's player: once, through armour, whatever they had just been hit by.</summary>
        private void Impale(string death = "FoighSpitted")
        {
            if (Main.dedServ || held != Main.myPlayer)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead)
                return;

            int damage = Main.DamageVar(SwordDamage(), -me.luck);

            FoighGripPlayer.Through(me, death, damage, facing);
            Wound(me);

            me.PIS().BumpScreenShake(9f, 16);
            Cinematic.Flash(new Color(255, 232, 220), 0.12f);
        }

        // ============================================================
        // HIS SECOND PHASE'S OWN: THE HAND
        // ============================================================

        /// <summary>The sword swung up; and from there the run at them, or, with them in reach already, straight into the blow.</summary>
        private void Raise(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            if (!Authority || now < M.RaiseTicks)
                return;

            FaceThem(target);
            Go(InRush(target) ? Move.Hew : Move.Rush);
        }

        /// <summary>Whether they are near enough ahead of him for the blow down to be begun.</summary>
        private bool InRush(Player target)
        {
            return target == null || Math.Abs(target.Center.X - NPC.Center.X) <= RushReach;
        }

        /// <summary>At them, the sword up: until they are in reach of it, or there is a wall, or he has run long enough.</summary>
        private void Rush(Player target)
        {
            Fall();

            if (target != null && Math.Abs(target.Center.X - NPC.Center.X) > 10f)
                Face(target.Center.X > NPC.Center.X ? 1 : -1);

            NPC.velocity.X = Toward(NPC.velocity.X, facing * RushSpeed, RushAccel);

            if (!Authority)
                return;

            if (InRush(target) || (now > 2 && NPC.collideX) || now >= RushLongest)
                Go(Move.Hew);
        }

        /// <summary>The blow down and the cut up, a step into each; and down onto a knee.</summary>
        private void Hew()
        {
            Fall();
            NPC.velocity.X = Stepped(M.HewSteps);

            if (Authority && now >= M.HewTicks)
                Go(Move.Crouch);
        }

        /// <summary>
        /// Down on a knee. He looks for them once, as he goes down, and no more: behind
        /// him by the time he is up again, they are behind the hand. From here it wears
        /// the star.
        /// </summary>
        private void Crouch(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.6f;
            NPC.PAD().CCBypass = true;

            if (fresh)
                FaceThem(target);

            if (Authority && now >= M.CrouchTicks)
                Leap(target);
        }

        /// <summary>
        /// The leap's own weight, and the speed up it leaves the ground at to top out this
        /// high: as LeapOff, by the same sum. At its highest (SpringHigh) it is in the air
        /// for SpringTicks.
        /// </summary>
        internal const float SpringGravity = 8f * M.SpringHigh / (M.SpringTicks * M.SpringTicks);

        internal static float SpringOff(float high) => SpringGravity * 0.5f + (float)Math.Sqrt(2f * SpringGravity * high);

        /// <summary>How many ticks a leap that tops out this high is in the air, from ground to ground.</summary>
        internal static int SpringTime(float high) => Math.Max(2, (int)Math.Round(2.0 * Math.Sqrt(2f * high / SpringGravity)));

        /// <summary>
        /// Off his knee at them, the server's, the hand out: forward the way he faces and
        /// up, to bring the hand down where they will be (most of where they are going
        /// allowed for). As far as ten tiles and as high as five, by request, which takes
        /// half a second; nearer, the same leap smaller: half as high as it is long, never
        /// under two tiles, and over one who is over him if that is within its five. One
        /// who has got behind him is not turned to: he leaps the least there is, at nothing.
        /// </summary>
        private void Leap(Player target)
        {
            Vector2 feet = Feet;

            float ahead = M.SpringFar * 0.5f;
            float rise = 0f;

            if (target != null)
            {
                // Where they will be if they go on as they are going, for as long as the
                // longest of it lasts.
                float x = target.Center.X + target.velocity.X * M.SpringTicks * SpringLead;

                ahead = (x - feet.X) * facing - SpringShort;
                rise = Math.Max(0f, feet.Y - target.Bottom.Y);
            }

            ahead = MathHelper.Clamp(ahead, SpringLeast, M.SpringFar);

            float high = MathHelper.Clamp(Math.Max(ahead * 0.5f, rise + SpringOver), SpringLow, M.SpringHigh);
            int ticks = SpringTime(high);

            leap = new Vector2(facing * ahead / ticks, -SpringOff(high));
            held = -1;

            Go(Move.Spring, ticks);
        }

        /// <summary>
        /// In the air, the hand out. Whoever of his it comes on is in it (the server's
        /// word): the player he fights, anywhere on the way. Down with nobody: empty.
        /// </summary>
        private void Spring()
        {
            // The first tick of it, on every machine: off the ground.
            if (fresh)
                NPC.velocity = leap;

            NPC.velocity.Y = Math.Min(NPC.velocity.Y + SpringGravity, MaxFall);
            NPC.PAD().CCBypass = true;

            if (!Authority)
                return;

            Player prey = Target();

            if (prey != null && !prey.dead && now >= 1 && HandOn(prey))
            {
                held = prey.whoAmI;
                NPC.netUpdate = true;
                Go(Move.Caught);
                return;
            }

            if ((now > 3 && grounded) || now >= LeapLongest)
                Go(Move.Miss);
        }

        /// <summary>The rest of the way down with them, by his own weight; and on his feet, into the sword.</summary>
        private void Caught()
        {
            Fall();
            NPC.velocity.X *= 0.94f;
            NPC.PAD().CCBypass = true;

            if (!Authority)
                return;

            if (Held() == null)
            {
                Release();
                Go(Move.Miss);
                return;
            }

            if ((now >= 2 && grounded) || now >= LeapLongest)
                Go(Move.Pierce);
        }

        // ============================================================
        // HIS SECOND PHASE'S OWN: THE LATE CUT
        // ============================================================

        /// <summary>
        /// Sunk low, the sword at his hip, looking at them along the line he will take.
        /// Then, the server's: along it, to as far past them as there is ground to stand
        /// on (a flash step, and at its end he is as the cut left him: Wake). With no line
        /// to take, a wall at his nose, he lets it go.
        /// </summary>
        private void Poise(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.6f;

            if (!Authority)
                return;

            FaceThem(target);

            if (now < M.PoiseTicks)
                return;

            if (target == null || !LateLine(target, out Vector2 feet))
            {
                Rest(20, 30);
                return;
            }

            FlashTo(feet, Move.Wake);
        }

        /// <summary>
        /// Where the late cut would leave him standing, as they are now: past them by a
        /// way, the side of them he is not on, no further from him than its longest; or a
        /// little nearer, where there is no ground to stand on there. False with too
        /// little of it ahead of him to be a cut at all.
        /// </summary>
        private bool LateLine(Player target, out Vector2 feet)
        {
            Vector2 from = Feet;
            int side = target.Center.X >= NPC.Center.X ? 1 : -1;
            float along = Math.Min(Math.Abs(target.Center.X - from.X) + LateOver, LateFurthest);

            if (!Stand(from.X + side * along, from.Y, out feet))
                return false;

            return (feet.X - from.X) * side >= LateLeast;
        }

        // ============================================================
        // HIS SECOND PHASE'S OWN: THE MILL
        // ============================================================

        /// <summary>The sword brought over and levelled at them; he looks for them once more, and comes on.</summary>
        private void Level(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            if (!Authority || now < M.LevelTicks)
                return;

            FaceThem(target);
            Go(Move.Mill);
        }

        /// <summary>Round and round, the way he set off: he does not turn to one who has got past him.</summary>
        private void Mill()
        {
            Fall();
            NPC.velocity.X = Toward(NPC.velocity.X, facing * MillSpeed * MillQuick, MillAccel * MillQuick);

            if (Authority && now >= M.MillTurn * MillTurns)
                Go(Move.MillEnd);
        }

        /// <summary>
        /// Out of the last turn: up off the ground, and down with the sword as he lands.
        /// Or out of the air (Hang, too near the ground for the blow that goes all the way
        /// round; its number is then 1, or 2 after one he threw up there himself): begun
        /// from where the sword is up, and down onto the ground at the speed he was given,
        /// which has him there as the sword is.
        /// </summary>
        private void MillEnd()
        {
            bool fell = Arg > 0.5f;

            // Out of the air it sends them down as the other does, and is told as it is.
            if (fell)
                NPC.PAD().CCBypass = true;

            // The first tick of it, on every machine.
            if (fresh)
                NPC.velocity = fell ? leap : new Vector2(facing * MillOn, -MillHop);

            Fall();

            if (grounded && !fresh)
                NPC.velocity.X *= 0.6f;

            if (Authority && now >= M.MillEndTicks)
                Rest(34, 54);
        }

        // ============================================================
        // HIS SECOND PHASE'S OWN: THE CUT UP
        // ============================================================

        /// <summary>
        /// At them, the sword dragged: for a few ticks at the least, so that it is seen
        /// and heard coming; then until they are in reach of the cut, or there is a wall,
        /// or he has run long enough. It wears the star from here: whoever the cut lands
        /// on is thrown into the air, whatever shield they have.
        /// </summary>
        private void Scrape(Player target)
        {
            Fall();
            NPC.PAD().CCBypass = true;

            float off = target != null ? Math.Abs(target.Center.X - NPC.Center.X) : 0f;

            if (off > 10f)
                Face(target.Center.X > NPC.Center.X ? 1 : -1);

            // Not through them: nearly on them, he stops running.
            if (target != null && off <= UpcutNear)
                NPC.velocity.X *= 0.6f;
            else
                NPC.velocity.X = Toward(NPC.velocity.X, facing * ScrapeSpeed, ScrapeAccel);

            if (!Authority || now < ScrapeLeast)
                return;

            if (InUpcut(target) || NPC.collideX || now >= ScrapeLongest)
            {
                lifted = -1;
                Go(Move.Upcut);
            }
        }

        /// <summary>Whether they are near enough ahead of him for the cut up to be begun.</summary>
        private bool InUpcut(Player target)
        {
            return target == null || Math.Abs(target.Center.X - NPC.Center.X) <= UpcutReach;
        }

        /// <summary>
        /// The blade torn forward through the ground and up, a step into it. Whoever it
        /// threw into the air (their own machine's word: Heard), he is up after, the
        /// server's, once the sword is over his shoulder; with nobody, or no room beside
        /// them up there, he lets the sword come down behind him.
        /// </summary>
        private void Upcut()
        {
            Fall();
            NPC.velocity.X = Stepped(M.UpcutSteps);

            // The star until the cut is over, and a tick more: on into the step up, if there is one.
            if (now <= M.UpcutOver + 1)
                NPC.PAD().CCBypass = true;

            if (!Authority)
                return;

            if (lifted >= 0 && now >= M.UpcutOver)
            {
                Player up = Main.player[lifted];

                lifted = -1;

                if (up.active && !up.dead && SkyTo(up, true))
                    return;
            }

            if (now >= M.UpcutTicks)
                Rest(40, 60);
        }

        // ============================================================
        // HIS SECOND PHASE'S OWN: IN THE AIR
        // ============================================================

        /// <summary>
        /// Whether a player is hanging on a hook: one of theirs is in something (the game's
        /// own count of it is emptied at the end of each player's update, so it says nothing
        /// by the time he looks; a hook in something is at 2, Projectile.AI_007_GrapplingHooks).
        /// </summary>
        internal static bool Hooked(Player player)
        {
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.owner == player.whoAmI && projectile.aiStyle == ProjAIStyleID.Hook && projectile.ai[0] == 2f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Where a player's middle will be this many ticks on, their keys taken to stay as
        /// they are: the game's own sum of how a player goes up and down, a tick at a time
        /// and in its order (Player.Update, JumpMovement, CarpetMovement, WingMovement and
        /// DryCollision, 1.4.4), from what it keeps on the player and what it had settled
        /// for them this tick (FoighAirPlayer):
        ///
        ///   in the rising part of a jump, jump held: their speed set to a jump's each tick
        ///     of it, for as many ticks as the jump has left; and for one who jumps again
        ///     of themselves as they land (a frog's leg), the next jump when they do;
        ///   on wings, with time left on them and jump held: lifted as that pair lifts
        ///     (<see cref="WingLift"/>) from the tick the jump runs out, for as long as the
        ///     time lasts, spent as that pair spends it; hovering, for the pairs that do,
        ///     with down held as well; and after it gliding, at a third of their weight;
        ///   on rocket boots: what is left of the burst they are in, and with jump held
        ///     (pressed again in the air) every burst they have left after it;
        ///   on a flying carpet: held level, for as long as it has;
        ///   falling: their weight onto their speed each tick, whichever way down is for
        ///     them, and no faster that way than they may fall. For one who falls slowly (a
        ///     potion, an umbrella): a third of the weight, or a tenth while they hold up,
        ///     unless they hold down; and a third of the speed, or a tenth;
        ///   and stopped by whatever tiles are in the way, platforms among them unless they
        ///     hold down or are the wrong way up.
        ///
        /// So one who is stood on something stays where they are, one who jumped is where
        /// they come down, and one who flies is where their wings will have them. One he
        /// threw up himself, or anything threw (RagdollPlayer), has no keys, and no limit
        /// to how fast they fall but a slow fall's.
        ///
        /// Not made here: how they go sideways (taken as it is now, until a wall); a mount
        /// that flies or hovers and a wall they are sliding down (taken to go on as they
        /// go); a hook or a rope (taken to stay where they hang); water, honey and shimmer;
        /// a jump begun in the air; and a thrown body coming back off what it hits.
        /// </summary>
        private static Vector2 Ahead(Player target, int ticks, bool thrown)
        {
            if (!thrown && (target.pulley || Hooked(target)))
                return target.Center;

            RagdollPlayer body = target.GetModPlayer<RagdollPlayer>();
            FoighAirPlayer air = target.GetModPlayer<FoighAirPlayer>();

            // Thrown, their keys are not theirs (and on a machine that is not theirs, the throw itself may not be known of yet).
            bool tossed = thrown || body.Flung;

            Vector2 corner = target.position;
            Vector2 going = target.velocity;

            // (Down, for one whose weight has been turned over, is up.)
            int down = target.gravDir < 0f ? -1 : 1;

            Mount mount = target.mount;
            bool mounted = mount.Active;
            bool carried = !tossed && ((mounted && (mount.CanFly() || mount.CanHover())) || target.sliding);

            bool held = !tossed && target.controlJump;
            bool holdsUp = tossed ? target.tryKeepingHoveringUp : target.TryingToHoverUp;
            bool holdsDown = tossed ? target.tryKeepingHoveringDown : target.TryingToHoverDown;

            // Through platforms: the wrong way up, or on what the game lets through them; and with down held, as far as falling through goes.
            bool past = down < 0 || (mounted && (mount.Cart || mount.Type == MountID.CuteFishron || mount.Type == MountID.UFO || mount.Type == MountID.Drill
                || mount.Type == MountID.WitchBroom || mount.Type == MountID.PirateShip || mount.Type == MountID.DarkMageBook));
            bool through = past || (!tossed && target.controlDown);

            float weight = air.Weight;
            float jump = air.JumpSpeed;
            float most = body.Flung ? body.FallLimit : target.maxFallSpeed;
            bool slowly = air.FallsSlowly;

            int rising = held ? Math.Max(0, target.jump) : 0;
            bool swims = target.merman && !(mounted && mount.Cart);
            bool bounces = held && target.autoJump;

            bool winged = target.wingsLogic > 0;
            bool allowed = target.PIS().AllowedWings;
            WingLift lift = winged ? WingLift.Of(target) : default;
            float wing = winged ? target.wingTime : 0f;
            int wingMost = target.wingTimeMax;
            bool brooch = target.empressBrooch;

            // Gliding: a third of their weight and of their fall, or what their wings say (a mod's).
            float lighter = 1f / 3f, slower = 1f / 3f;
            bool glides = winged && held && ItemLoader.WingGlidingSpeeds(target, ref lighter, ref slower);

            // Rocket boots: what is left of the burst they are in, the bursts they have after it, and whether the next may fire.
            bool boots = target.rocketBoots != 0;
            int burst = Math.Max(0, target.rocketDelay);
            int bursts = boots ? target.rocketTime : 0;
            int burstsMost = boots ? target.rocketTimeMax : 0;
            bool canRocket = target.canRocket;
            bool rocketRelease = target.rocketRelease;

            bool canCarpet = target.canCarpet;
            int carpetLeft = target.carpetTime;

            // A jump in the air they have not used yet: rockets and carpet wait on it.
            bool spare = target.AnyExtraJumpUsable();

            bool stuck = target.frozen || target.webbed || target.stoned;
            int ramp = target.cartRampTime;

            for (int i = 0; i < ticks; i++)
            {
                if (!carried)
                {
                    float speed = going.Y;

                    // (A throw takes the limit off their fall, each tick from what they are doing: RagdollPlayer.)
                    float limit = tossed ? Math.Max(most, Math.Abs(speed) + 1f) : most;

                    // The mod's own rules for wings, as each tick begins: no time on them at all where flight
                    // is not theirs (PSItem), and never more than a full pair's where it is (PisPlayer).
                    if (winged)
                    {
                        if (!allowed)
                            wing = 0f;
                        else if (wing > wingMost)
                            wing = wingMost;
                    }

                    // The carpet is theirs again once they are stood on something; and never the wrong way up.
                    if (!target.carpet)
                    {
                        canCarpet = false;
                    }
                    else if (speed == 0f)
                    {
                        canCarpet = true;
                        carpetLeft = 0;
                    }

                    if (down < 0)
                        canCarpet = false;

                    // ---- The jump (Player.JumpMovement) ----
                    bool jumped = false;

                    if (held)
                    {
                        if (rising > 0)
                        {
                            // Their head against something: the jump is over. Or their speed is set again, and a tick of it is gone.
                            if (speed == 0f)
                            {
                                rising = 0;
                            }
                            else
                            {
                                speed = -jump * down;

                                if (!swims)
                                    rising--;
                            }
                        }
                        else if (bounces && speed == 0f)
                        {
                            // Stood on something, the key still down: up again, with everything they had in the air theirs again.
                            jumped = true;
                            canRocket = false;
                            rocketRelease = false;
                            spare = SpareOnLanding(target);

                            speed = -jump * down;
                            rising = air.JumpTicks;
                        }
                    }

                    // ---- The carpet (Player.CarpetMovement): with nothing else left to fly by ----
                    bool slow = slowly;

                    if (target.carpet && !mounted && !spare && rising == 0 && speed != 0f && bursts == 0 && wing == 0f)
                    {
                        if (held && canCarpet)
                        {
                            canCarpet = false;
                            carpetLeft = 300;
                        }

                        if (carpetLeft > 0 && held)
                        {
                            carpetLeft--;
                            slow = false;

                            if (down > 0 && speed > -weight)
                                speed = -(weight + 0.000001f);
                            else if (down < 0 && speed < weight)
                                speed = weight + 0.000001f;
                        }
                    }

                    // ---- Flight and fall (Player.Update) ----
                    if (((down > 0 && speed > -jump) || (down < 0 && speed < jump)) && speed != 0f)
                        canRocket = true;

                    // Stood on something with the key let go, or up again of themselves: their wings have their time again.
                    if ((speed == 0f && !held) || jumped)
                        wing = winged ? wingMost : 0f;

                    // Whether they fly this tick: jump held and time left on the wings, and either out of the rising part of a
                    // jump and moving up or down already, or (the pairs that hover) down held as well.
                    bool flies = winged && held && wing > 0f && ((rising == 0 && speed != 0f) || (lift.Anyhow && holdsDown));

                    if (stuck)
                    {
                        // Frozen, webbed, stone: their weight, the way it always is.
                        speed += weight;

                        if (speed > limit)
                            speed = limit;
                    }
                    else
                    {
                        if (flies)
                        {
                            speed = lift.After(speed * down, jump) * down;
                            wing -= lift.Spends;

                            if (brooch && wing != 0f)
                                wing = wingMost;
                        }

                        // Rocket boots under wings: their time is the wings'.
                        if (winged && boots && speed != 0f && bursts != 0)
                        {
                            int more = bursts * 6;

                            wing += more;

                            if (wing > wingMost + more)
                                wing = wingMost + more;

                            bursts = 0;
                        }

                        if (speed == 0f || jumped || brooch)
                            bursts = burstsMost;

                        // The next burst, as the last goes out: jump held (and pressed again in the air), no wings with time on them, no jump in the air unused.
                        if ((wing == 0f || !winged) && boots && held && burst == 0 && canRocket && rocketRelease && !spare)
                        {
                            if (bursts > 0)
                            {
                                bursts--;
                                burst = 10;
                            }
                            else
                            {
                                canRocket = false;
                            }
                        }

                        if (burst > 0)
                        {
                            burst--;
                            speed = WingLift.Plain.After(speed * down, jump) * down;
                        }
                        else if (!flies)
                        {
                            if (slow && !holdsDown)
                            {
                                speed += weight / (holdsUp ? 10f : 3f) * down;
                            }
                            else if (winged && held && speed > 0f)
                            {
                                // Gliding. (Moving down the world, whichever way down is for them: the game asks no more than that.)
                                if (glides)
                                {
                                    float light = weight * lighter * 3f;
                                    float slowest = limit * slower * 3f;

                                    speed += light / 3f * down;

                                    if (down > 0)
                                    {
                                        if (speed > slowest / 3f && !holdsDown)
                                            speed = slowest / 3f;
                                    }
                                    else if (speed < -slowest / 3f && !holdsUp)
                                    {
                                        speed = -slowest / 3f;
                                    }
                                }
                            }
                            else if (ramp > 0)
                            {
                                ramp--;
                            }
                            else
                            {
                                speed += weight * down;
                            }
                        }

                        // No faster down than they may fall; and for one who falls slowly, a third of it, or a tenth.
                        if (down > 0)
                        {
                            if (speed > limit)
                                speed = limit;

                            if (slow && speed > limit / 3f && !holdsDown)
                                speed = limit / 3f;

                            if (slow && speed > limit / 5f && holdsUp)
                                speed = limit / 10f;
                        }
                        else
                        {
                            if (speed < -limit)
                                speed = -limit;

                            if (slow && speed < -limit / 3f && !holdsDown)
                                speed = -limit / 3f;

                            if (slow && speed < -limit / 5f && holdsUp)
                                speed = -limit / 10f;
                        }
                    }

                    // Wings that hover, with jump and down held and time left: what is left of their speed, and at nothing once it is small.
                    if (held && holdsDown && wing > 0f)
                        speed = lift.Hovered(speed);

                    going.Y = speed;
                }

                going = Moved(ref corner, going, target.width, target.height, through, past, down, out bool bumped);

                // (Their head against something: the jump is over.)
                if (bumped && down > 0)
                    rising = 0;
            }

            return corner + target.Size * 0.5f;
        }

        /// <summary>
        /// A tick's move through the tiles, as the game makes a player's (Player.DryCollision,
        /// 1.4.4): in one, or for one faster than a tile a tick in steps of a tile, so
        /// that nothing thin is gone through. What it came to, and whether anything over
        /// them stopped it.
        /// </summary>
        private static Vector2 Moved(ref Vector2 corner, Vector2 going, int width, int height, bool through, bool past, int down, out bool bumped)
        {
            float far = going.Length();

            if (far <= 16f)
            {
                going = Collision.TileCollision(corner, going, width, height, through, past, down);
                bumped = Collision.up;
                corner += going;

                return going;
            }

            Vector2 whole = Collision.TileCollision(corner, going, width, height, through, past, down);
            Vector2 way = Vector2.Normalize(going);

            if (whole.Y == 0f)
                way.Y = 0f;

            Vector2 came = Vector2.Zero;

            bumped = false;

            while (far > 0f)
            {
                float step = Math.Min(far, 16f);

                far -= step;

                Vector2 piece = Collision.TileCollision(corner, way * step, width, height, through, past, down);

                bumped |= Collision.up;
                corner += piece;
                came += piece;
            }

            return came;
        }

        /// <summary>
        /// Whether a player will have a jump in the air to use once they have next stood on
        /// something: any they have on them (the game gives each back as they land:
        /// ExtraJumpLoader.RefreshJumps, and asks as here: Player.AnyExtraJumpUsable).
        /// </summary>
        private static bool SpareOnLanding(Player target)
        {
            if (target.blockExtraJumps)
                return false;

            foreach (ExtraJump extra in ExtraJumpLoader.OrderedJumps)
            {
                if (target.GetJumpState(extra).Enabled && extra.CanStart(target) && PlayerLoader.CanStartExtraJump(extra, target))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// What a pair of wings does to the speed of whoever flies by them, a tick of it,
        /// as the game has it (Player.WingMovement, 1.4.4, and the lines of Player.Update
        /// that decide whether it is called and what hovering does after it). Their speed
        /// is taken along their own down, so it is one sum whichever way up they are.
        /// </summary>
        private readonly struct WingLift
        {
            /// <summary>Taken off their speed downward every tick of flight; more while they are falling; more while they rise slower than this share of a jump's speed; and no faster up than this many times a jump's speed.</summary>
            private readonly float constant;
            private readonly float falling;
            private readonly float rising;
            private readonly float canAscend;
            private readonly float most;

            /// <summary>With up held: this much more off first (the jetpack, and the two that surge), and for the jetpack nothing after it.</summary>
            private readonly float surge;
            private readonly bool surgeOnly;

            /// <summary>What is left of their speed each tick they hover, jump and down held (none at 0), once more for the one pair that slows twice.</summary>
            private readonly float hover;
            private readonly bool hoverTwice;

            /// <summary>Flown with jump and down held whatever they are doing (the pairs that hover).</summary>
            public readonly bool Anyhow;

            /// <summary>How much of their time a tick of flight costs: one; two for the jetpack with up held; a half for the pairs that hover, hovering in one place.</summary>
            public readonly float Spends;

            private WingLift(float constant, float falling, float rising, float canAscend, float most, float surge, bool surgeOnly, bool anyhow, float hover, bool hoverTwice, float spends)
            {
                this.constant = constant;
                this.falling = falling;
                this.rising = rising;
                this.canAscend = canAscend;
                this.most = most;
                this.surge = surge;
                this.surgeOnly = surgeOnly;
                this.hover = hover;
                this.hoverTwice = hoverTwice;

                Anyhow = anyhow;
                Spends = spends;
            }

            /// <summary>The game's numbers where a pair has none of its own; rocket boots lift by the same.</summary>
            public static readonly WingLift Plain = new WingLift(0.1f, 0.5f, 0.1f, 0.5f, 1.5f, 0f, false, false, 0f, false, 1f);

            /// <summary>
            /// The numbers of the wings this player flies by, the game's own pairs by their
            /// numbers as it has them there, and after them whatever a mod's wings (or a
            /// mod, of any wings) say of themselves (ItemLoader.VerticalWingSpeeds, which is
            /// where the game asks).
            /// </summary>
            public static WingLift Of(Player target)
            {
                int wings = target.wingsLogic;
                bool up = target.TryingToHoverUp;
                bool down = target.TryingToHoverDown;

                float constant = 0.1f, falling = 0.5f, most = 1.5f, canAscend = 0.5f, rising = 0.1f;
                float surge = 0f;

                // The jetpack with up held is its own sum, and nothing of the rest.
                if (wings == 4 && up)
                    return new WingLift(constant, falling, rising, canAscend, most, 0.2f, true, false, 0f, false, 2f);

                if (wings == 26 || wings == 37)
                {
                    falling = 0.75f;
                    rising = 0.15f;
                    canAscend = 1f;
                    most = 2.5f;
                    constant = 0.125f;
                }

                if (wings == 8 || wings == 11 || wings == 24 || wings == 27 || wings == 22)
                    most = 1.66f;

                if (wings == 21 || wings == 12 || wings == 20 || wings == 23)
                    most = 1.805f;

                if (wings == 44)
                {
                    falling = 0.85f;
                    rising = 0.15f;
                    canAscend = 1f;
                    most = 2.75f;
                    constant = 0.125f;
                }

                if (wings == 45)
                {
                    falling = 0.95f;
                    rising = 0.15f;
                    canAscend = 1f;
                    most = 4.5f;
                }

                if ((wings == 44 || wings == 45) && up)
                    surge = 0.4f;

                if (wings == 29 || wings == 32)
                {
                    falling = 0.85f;
                    rising = 0.15f;
                    canAscend = 1f;
                    most = 3f;
                    constant = 0.135f;
                }

                if (wings == 30 || wings == 31)
                {
                    canAscend = 1f;
                    most = 2.45f;

                    if (!down)
                        constant = 0.15f;
                }

                ItemLoader.VerticalWingSpeeds(target, ref falling, ref rising, ref canAscend, ref most, ref constant);

                bool anyhow = wings == 22 || wings == 28 || wings == 30 || wings == 32 || wings == 29 || wings == 33 || wings == 35 || wings == 37 || wings == 45;
                bool slows = wings == 22 || wings == 28 || wings == 30 || wings == 31 || wings == 33 || wings == 35 || wings == 37 || wings == 45;
                bool spares = wings == 22 || wings == 28 || wings == 30 || wings == 31 || wings == 37 || wings == 45;

                float hover = !slows || target.merman ? 0f : wings == 45 ? 0.8f : 0.9f;
                float spends = spares && down && !target.controlLeft && !target.controlRight ? 0.5f : 1f;

                return new WingLift(constant, falling, rising, canAscend, most, surge, false, anyhow, hover, wings == 37, spends);
            }

            /// <summary>Their speed downward after a tick of flight, from what it was.</summary>
            public float After(float falls, float jump)
            {
                if (surge > 0f)
                {
                    falls -= surge;

                    if (falls > 0f)
                        falls -= 1f;
                    else if (falls > -jump)
                        falls -= 0.2f;

                    if (falls < -jump * 3f)
                        falls = -jump * 3f;

                    if (surgeOnly)
                        return falls;
                }

                falls -= constant;

                if (falls > 0f)
                    falls -= falling;
                else if (falls > -jump * canAscend)
                    falls -= rising;

                if (falls < -jump * most)
                    falls = -jump * most;

                return falls;
            }

            /// <summary>Their speed up or down after a tick of hovering (the game slows it as it is in the world, whichever way up they are).</summary>
            public float Hovered(float speed)
            {
                if (hover <= 0f)
                    return speed;

                speed = Slowed(speed, hover);

                return hoverTwice ? Slowed(speed, 0.92f) : speed;
            }

            private static float Slowed(float speed, float keeps)
            {
                speed *= keeps;

                return speed > -2f && speed < 1f ? 0.00001f : speed;
            }
        }

        /// <summary>
        /// Up to beside a player who is off the ground, the server's (a flash step, and at
        /// its end he hangs there: Hang). Not to where they are but to where they will be
        /// as his sword comes down (Ahead): at the top of it, for one he threw up; on the
        /// ground, for one who will have come down by then. On the side of them he is on,
        /// or with no room there the other; his feet a little under theirs, or with no
        /// room for that level with them or a little over (they are stood on something),
        /// or further under (their head is against a ceiling, and he is the taller).
        /// False, and nothing done, with no room for him beside them at all.
        /// </summary>
        private bool SkyTo(Player target, bool thrown)
        {
            int side = target.Center.X >= NPC.Center.X ? 1 : -1;

            Vector2 there = Ahead(target, FlashTicks + (thrown ? HangShort : HangTicks) + M.SmiteDown - 1, thrown);
            float bottom = there.Y + target.height * 0.5f;

            for (int turn = 0; turn < 2; turn++)
            {
                float x = there.X - side * SkyGap;

                for (int i = 0; i < SkyTries.Length; i++)
                {
                    Vector2 feet = new Vector2(x, bottom + SkyTries[i]);

                    if (!Room(feet))
                        continue;

                    TargetIndex = target.whoAmI;
                    NPC.target = target.whoAmI;

                    FlashTo(feet, Move.Hang, thrown ? 1f : 0f);
                    return true;
                }

                side = -side;
            }

            return false;
        }

        /// <summary>Whether there is room for him with his feet here: nothing solid where he would be.</summary>
        private bool Room(Vector2 feet)
        {
            return !Collision.SolidCollision(new Vector2(feet.X - NPC.width * 0.5f, feet.Y - NPC.height), NPC.width, NPC.height);
        }

        /// <summary>
        /// How much air there is under these feet, as far down as <paramref name="furthest"/>:
        /// to the first tile under him that he would stand on (a platform is one).
        /// </summary>
        private float Clear(Vector2 feet, float furthest)
        {
            int left = (int)((feet.X - NPC.width * 0.5f) / 16f);
            int right = (int)((feet.X + NPC.width * 0.5f - 1f) / 16f);
            int from = (int)(feet.Y / 16f);
            int to = (int)((feet.Y + furthest) / 16f);

            for (int j = from; j <= to; j++)
            {
                for (int i = left; i <= right; i++)
                {
                    if (!WorldGen.InWorld(i, j, 2))
                        continue;

                    Tile tile = Main.tile[i, j];

                    if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        return MathHelper.Clamp(j * 16f - feet.Y, 0f, furthest);
                }
            }

            return furthest;
        }

        /// <summary>
        /// In the air, with no weight, the sword going up over his head: he keeps to the
        /// place beside where they will be as it comes down (Ahead), on the side of them he
        /// is on, until the last few ticks of it (after one he threw up himself, all but the
        /// last). Then the blow, the server's: the one that goes all the way round under
        /// him, with air enough under his feet for it; with less, the mill's last, from
        /// where the sword is up, down onto the ground with him. It wears the star: it
        /// sends them down, whatever shield they have.
        /// </summary>
        private void Hang(Player target)
        {
            NPC.PAD().CCBypass = true;

            bool thrown = Arg > 0.5f;
            int ticks = thrown ? HangShort : HangTicks;

            if (target != null && !target.dead && now < ticks - (thrown ? 1 : HangCommits))
            {
                FaceThem(target);

                Vector2 there = Ahead(target, ticks - now + M.SmiteDown - 1, thrown);
                Vector2 want = new Vector2(there.X - facing * SkyGap, there.Y + target.height * 0.5f + SkyUnder) - Feet;
                float far = want.Length();

                NPC.velocity = far > HangSpeed ? want * (HangSpeed / far) : want;
            }
            else
            {
                NPC.velocity *= 0.5f;
            }

            if (!Authority || now < ticks)
                return;

            float clear = Clear(Feet, SmiteClear);

            if (clear >= SmiteClear)
            {
                Go(Move.Smite, Arg);
                return;
            }

            leap = new Vector2(0f, (clear - FellFalls) / FellTicks);

            // (Out of the air: 1; and after one he threw up there himself, who is still his to strike down: 2.)
            Go(Move.MillEnd, thrown ? 2f : 1f);
            Timer = M.MillEndUp - 1;
        }

        /// <summary>The blow down, in the air: no weight until the sword has come down, and then his own again.</summary>
        private void Smite()
        {
            NPC.PAD().CCBypass = true;

            if (now < M.SmiteDown)
                NPC.velocity *= 0.5f;
            else
                Fall();

            if (Authority && now >= M.SmiteTicks)
                Go(Move.Drop);
        }

        /// <summary>Falling after it, until there is ground.</summary>
        private void Drop()
        {
            Fall();
            NPC.velocity.X *= 0.9f;

            if (Authority && ((now > 1 && grounded) || now >= LeapLongest))
                Go(Move.Alight);
        }

        // ============================================================
        // TURNED ASIDE
        // ============================================================

        /// <summary>
        /// Whether a parry turns his sword aside as he is: in a move it cuts in, with it in
        /// his hand. (Loosely: a parry's word takes a tick or two to get to the server.)
        /// </summary>
        public bool CanBeTurned
        {
            get
            {
                if (!hasSword || NPC.dontTakeDamage)
                    return false;

                switch (State)
                {
                    case Move.One:
                    case Move.TwoAir:
                    case Move.Three:
                    case Move.ThreeRound:
                    case Move.FourRound:
                    case Move.Hew:
                    case Move.Mill:
                    case Move.Rend:
                    case Move.Gale:
                    case Move.Reap:
                        return true;

                    // (The break-out's wears the star.)
                    case Move.Behind:
                        return Arg <= 0.5f;

                    // (Out of the air it wears the star.)
                    case Move.MillEnd:
                        return Arg <= 0.5f;

                    // The late cut, until its line has opened; and a little after, for a word on its way.
                    case Move.Wake:
                        return Timer <= M.WakeCuts + 6;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Parried, the server's: thrown back; or, if this is the parry that does it (every
        /// third, for him as for anything on the ground), down on a knee for as long as a
        /// stagger lasts, the next hit on him a riposte. He has his own moves for both, so
        /// his AI is not stopped: only open (PADStaggerNPC.Reel, not held).
        /// </summary>
        private void Turned(bool staggers)
        {
            if (!Authority || !CanBeTurned)
                return;

            Release();
            close = false;

            // (What he learns of them: Foigh.Wild.)
            LearnParry(true);

            // His own sword turned aside wears at his posture most of all (Foigh.Hold).
            Wear(PostureParried, false);

            if (staggers)
            {
                NPC.GetGlobalNPC<PADStaggerNPC>().Reel(NPC, Parry.StaggerTicks, false);
                Go(Move.Stagger);
            }
            else
            {
                Go(Move.Reel);
            }
        }

        private void Reel()
        {
            if (fresh)
                NPC.velocity.X = -facing * ReelBack;

            Fall();
            NPC.velocity.X *= 0.9f;

            if (Authority && now >= M.ReelTicks)
                Rest(18, 30);
        }

        private void Stagger()
        {
            // Thrown back off them first, as when he only reels: he goes down a step away.
            if (fresh)
                NPC.velocity.X = -facing * ReelBack;

            Fall();
            NPC.velocity.X *= 0.85f;

            if (!Authority || now < 3)
                return;

            // Up when it is over, or at the riposte, which wakes him.
            if (!NPC.GetGlobalNPC<PADStaggerNPC>().Reeling || now >= Parry.StaggerTicks + 4)
                Rest(20, 34);
        }

        // ============================================================
        // THE END OF IT (his own end, at nothing: Foigh.End)
        // ============================================================

        /// <summary>Nobody left to fight: he stands, fades, and is gone.</summary>
        private void Leave()
        {
            Fall();
            NPC.velocity.X *= 0.8f;

            if (now >= LeaveTicks - 30)
                NPC.alpha = (int)MathHelper.Clamp((now - (LeaveTicks - 30)) * 255f / 30f, 0f, 255f);

            if (Authority && now >= LeaveTicks)
                Vanish();
        }

        // ============================================================
        // HOW HE HOLDS HIMSELF
        // ============================================================

        /// <summary>
        /// How he holds himself at this tick of this move (whole, or between two): what is
        /// drawn, what hits, and what the blade leaves behind it are all this.
        /// </summary>
        private FoighRig.Pose PoseOf(Move move, float t)
        {
            switch (move)
            {
                case Move.Await:
                    return M.Await;

                case Move.Turn:
                    return M.TurnRound.Sample(t - TurnWait);

                case Move.Talk:
                    return M.Talking;

                case Move.Draw:
                    return M.DrawSword.Sample(t);

                case Move.One:
                    return M.One.Sample(t);

                case Move.TwoGather:
                    return M.TwoGather.Sample(t);

                case Move.TwoAir:
                    return M.Plunge(Share(t), aimShown);

                case Move.TwoStuck:
                    return phase >= 3 ? M.Driving(t, Arg) : M.Pulling(t, Arg);

                case Move.TwoFree:
                    return (free ??= M.Free(Arg)).Sample(t);

                case Move.TwoLanded:
                    return (landed ??= M.Landed(Arg)).Sample(t);

                case Move.Three:
                    return M.Three.Sample(t);

                case Move.ThreeRound:
                    return M.Round(M.Three.Sample(M.ThreeCoil), MathHelper.Clamp(t / RoundLength, 0f, 1f), true);

                case Move.ThreeEnd:
                    return M.ThreeEnd.Sample(t);

                case Move.FourGather:
                    return M.FourGather.Sample(t);

                case Move.FourAir:
                    return M.Aloft(aimShown, Wound(t));

                case Move.FourFall:
                    return M.Falling(MathHelper.Clamp(NPC.velocity.Y / 14f, 0f, 1f));

                case Move.FourTouch:
                    return M.Touch.Sample(t);

                case Move.FourRun:
                    return M.Run(gait, false);

                case Move.FourTake:
                    return M.Taking(MathHelper.Clamp(t / M.TakeTicks, 0f, 1f), TakeAngle());

                case Move.FourRound:
                {
                    float turns = MathHelper.Clamp(t / M.RoundTicks, 0f, 2f);
                    float round = turns >= 2f ? 1f : turns - (float)Math.Floor(turns);

                    return M.Round(M.Drag, round, false);
                }

                case Move.FourEnd:
                    return M.FourEnd.Sample(t);

                case Move.Knives:
                    return M.Knife(t, Throws);

                case Move.Flash:
                    return M.Flash;

                case Move.Behind:
                    return M.Behind.Sample(t);

                case Move.Guard:
                {
                    FoighRig.Pose guard = M.Guard.Sample(t);
                    return guards > 0 ? M.GuardAgain(guard, guards, since) : guard;
                }

                case Move.Clash:
                    return M.Clash.Sample(t);

                case Move.Charge:
                    return M.Charging(gait);

                case Move.Grab:
                    return M.Grab.Sample(t);

                case Move.Miss:
                    return (empty ?? M.Miss).Sample(t);

                case Move.Lift:
                    return M.Lift.Sample(t);

                case Move.Spit:
                    return M.Spit.Sample(t);

                case Move.Spitted:
                    return M.Spitted(t);

                case Move.Fling:
                    return (flung ?? M.Fling).Sample(t);

                case Move.Raise:
                    return M.Raise.Sample(t);

                case Move.Rush:
                    return M.Rushing(gait);

                case Move.Hew:
                    return M.Hew.Sample(t);

                case Move.Crouch:
                    return M.Crouch.Sample(t);

                case Move.Spring:
                    return M.Springing(MathHelper.Clamp(t / Math.Max(1f, Arg), 0f, 1f));

                case Move.Caught:
                    return M.Catching(MathHelper.Clamp(NPC.velocity.Y / 9f, 0f, 1f));

                case Move.Pierce:
                    return (pierce ??= M.Pierce(M.Catching(1f))).Sample(t);

                case Move.Hoist:
                    return M.Hoisted(hoist ??= M.Hoist((pierce ?? M.Pierce(M.Catching(1f))).Sample(M.PierceTicks)), t);

                case Move.Poise:
                    return M.Poise.Sample(t);

                case Move.Wake:
                    return M.Wake.Sample(t);

                case Move.Level:
                    return M.Level.Sample(t);

                case Move.Mill:
                {
                    float turns = MathHelper.Clamp(t / M.MillTurn, 0f, MillTurns);
                    float round = turns >= MillTurns ? 1f : turns - (float)Math.Floor(turns);

                    return M.Round(M.Level.Sample(M.LevelTicks), round, false);
                }

                case Move.MillEnd:
                    return M.MillEnd.Sample(t);

                case Move.Scrape:
                    return M.Scraping(gait);

                case Move.Upcut:
                    return (upcut ??= M.Upcut(M.Scraping(0f))).Sample(t);

                case Move.Hang:
                    return M.Hanging(t / (Arg > 0.5f ? HangShort : HangTicks));

                case Move.Smite:
                    return M.Smite.Sample(t);

                case Move.Drop:
                    return M.Dropping(MathHelper.Clamp(NPC.velocity.Y / 12f, 0f, 1f));

                case Move.Alight:
                    return (alight ??= M.Alight(M.Dropping(1f))).Sample(t);

                case Move.Rend:
                    return M.Rend.Sample(t);

                case Move.Gale:
                    return M.Gale.Sample(t);

                case Move.HookCast:
                    return M.HookCast.Sample(t);

                case Move.Haul:
                    return M.Hauled;

                case Move.Winch:
                    return M.Winch.Sample(t);

                case Move.Reap:
                    return M.Reap.Sample(t);

                case Move.HookMiss:
                    return M.HookMiss.Sample(t);

                case Move.Soar:
                    return M.FourGather.Sample(t);

                case Move.Wheel:
                    return M.Wheeling(t);

                case Move.Curl:
                    return M.Curling(t);

                // (His third line, the passage into his second phase: the hook taken out while it is up.)
                case Move.Oath:
                    return M.TakingHook(Breathing(), t);

                case Move.Reel:
                    return (reel ??= M.Reel(M.Drag)).Sample(t);

                case Move.Stagger:
                case Move.Broken:
                    return M.Kneeling(t);

                case Move.Spent:
                    return (spent ??= M.Spent(M.Drag)).Sample(t);

                case Move.Knee:
                    return M.Knee(t);

                case Move.Whet:
                    return M.Whetting(t);

                case Move.Shudder:
                    return M.Shudder(t);

                case Move.Gut:
                    return M.Gutting(t);

                case Move.Bled:
                    return M.Bled.Sample(t);

                case Move.Sever:
                    return M.Severing(t);

                // (Out of the passage into his third phase: the sword out of the ground and behind him again.)
                case Move.Resume:
                    return leaving != null ? leaving.Sample(t) : Breathing();

                // At nothing: his end (Foigh.End).
                case Move.Dying:
                case Move.Roar:
                case Move.Fold:
                case Move.Bowed:
                case Move.Laugh:
                case Move.Asked:
                case Move.Knew:
                case Move.Mouth:
                case Move.Choir:
                case Move.Plead:
                case Move.Finish:
                case Move.Thrust:
                case Move.Cast:
                case Move.Spare:
                case Move.Scorn:
                case Move.Slump:
                    return EndPose(move, t);

                case Move.Walk:
                {
                    FoighRig.Pose walking = FoighTrack.Blend(Breathing(), M.Walk(gait), stride);
                    return walking;
                }

                default:
                    return Breathing();
            }
        }

        /// <summary>Standing in his stance: breathing.</summary>
        private FoighRig.Pose Breathing()
        {
            FoighRig.Pose stance = M.Drag;
            float breath = (float)Math.Sin(Main.GameUpdateCount * 0.045f + NPC.whoAmI);

            stance.Lean += 0.014f * breath;
            stance.Nod += 0.01f * breath;
            stance.FarArm += 0.02f * breath;

            return stance;
        }

        /// <summary>What his arm points at, reaching for the sword in the ground: from his shoulder to its grip.</summary>
        private float TakeAngle()
        {
            Projectile sword = ThrownSword();

            if (sword == null)
                return MathHelper.ToRadians(96f);

            Vector2 grip = FoighSwordThrown.GripOf(sword);
            Vector2 shoulder = Feet + new Vector2(-7f * facing, -33f);
            Vector2 reach = grip - shoulder;

            // A limb's own angle: from straight down, forward positive.
            return (float)Math.Atan2(reach.X * facing, reach.Y);
        }

        // ============================================================
        // THE SWORD AGAINST THEM
        // ============================================================

        /// <summary>
        /// Whether the sword cuts at this tick of the move he is in: how hard, and which cut of
        /// the move it is (a cut lands on a player once).
        /// </summary>
        private bool Cuts(float t, out float power, out int cut)
        {
            power = 1f;
            cut = 0;

            switch (State)
            {
                case Move.One:
                    if (t > M.OneHold && t <= M.OneDown + 1) cut = 1;
                    else if (t > M.OneRest && t <= M.OneUp + 3) cut = 2;
                    else if (t > M.OneChambered && t <= M.OneThrust + 4) cut = 3;
                    else if (t > M.OneLeap + 4 && t <= M.OneLeap + 22) cut = 4;
                    break;

                case Move.TwoAir:
                    if (Share(t) >= 0.42f) cut = 1;
                    break;

                case Move.Three:
                    if (t > M.ThreeHold && t <= M.ThreeDown + 1)
                    {
                        cut = 1;
                        power = HardBlow;
                    }
                    break;

                case Move.ThreeRound:
                {
                    float round = t / RoundLength;
                    int side = M.RoundCuts(round);

                    if (side < 0) cut = 2;
                    else if (side > 0 && round > 0.5f) cut = 3;
                    break;
                }

                case Move.FourRound:
                {
                    float turns = t / M.RoundTicks;
                    int turn = Math.Min(1, (int)turns);
                    float round = turns - turn;
                    int side = M.RoundCuts(round);

                    if (side < 0) cut = 1 + turn * 2;
                    else if (side > 0 && round > 0.5f) cut = 2 + turn * 2;
                    break;
                }

                case Move.Behind:
                    if (t > M.BehindWait && t <= M.BehindCut + 5) cut = 1;
                    break;

                case Move.Hew:
                    if (t > M.HewHold && t <= M.HewDown + 1) cut = 1;
                    else if (t > M.HewRest && t <= M.HewUp + 3) cut = 2;
                    break;

                case Move.Mill:
                {
                    // Each time the sword comes round to behind him or to ahead is a cut of
                    // its own, the first behind and the last ahead: six of them in three
                    // turns. (Ahead as he sets off it has not come round yet.)
                    float turns = t / M.MillTurn;
                    int pass = (int)Math.Round(turns * 2f);

                    if (pass >= 1 && pass <= MillTurns * 2 && M.RoundCuts(turns - (float)Math.Floor(turns)) != 0)
                        cut = pass;
                    break;
                }

                case Move.MillEnd:
                    if (t > M.MillEndDown - 5 && t <= M.MillEndDown + 1) cut = 1;
                    break;

                // From where the blade is coming forward under him to where it is over his head.
                case Move.Upcut:
                    if (t > M.UpcutSets + 2 && t <= M.UpcutCuts + 2) cut = 1;
                    break;

                case Move.Smite:
                    if (t > M.SmiteCuts - 1 && t <= M.SmiteDown + 2) cut = 1;
                    break;

                // The three cuts themselves, on one near enough to be cut by them.
                case Move.Rend:
                    if (t > M.RendHold && t <= M.RendDown + 1) cut = 1;
                    else if (t > M.RendCoiled && t <= M.RendUp + 3) cut = 2;
                    else if (t > M.RendHeld && t <= M.RendSmash + 1) cut = 3;
                    break;

                // The wind's two, the same.
                case Move.Gale:
                    if (t > M.GaleHold && t <= M.GaleDown + 1) cut = 1;
                    else if (t > M.GaleCoiled && t <= M.GaleUp + 3) cut = 2;
                    break;

                // The blow that meets whoever the hook brought together with him.
                case Move.Reap:
                    if (t > M.ReapHold && t <= M.ReapDown + 1) cut = 1;
                    break;
            }

            return cut != 0;
        }

        /// <summary>
        /// The sword against this machine's own player: only while it cuts, the blade from
        /// just past the guard to a little past the point, checked at every few degrees of
        /// the way it moved this tick so a fast blade cannot pass over anyone between two
        /// ticks. The parry first; then the hit, through armour, once a cut.
        /// </summary>
        private void Strike()
        {
            if (!hasSword || !Cuts(clock, out float power, out int cut))
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            int key = (int)State * 16 + cut;

            if (struck == key)
                return;

            ParryPlayer parry = me.GetModPlayer<ParryPlayer>();

            if (parry.Spares(NPC))
                return;

            // From where the move's clock stood a tick ago to where it stands: more than a
            // tick of the move, in one his second phase quickens.
            float before = MathHelper.Clamp(clockWas, 0f, clock);
            FoighRig.Pose first = PoseOf(State, before);
            FoighRig.Pose final = PoseOf(State, clock);

            float moved = Math.Abs(final.Sword - first.Sword) + Math.Abs(final.Turn - first.Turn) * 1.5f + Math.Abs(final.NearArm - first.NearArm);
            int steps = (int)MathHelper.Clamp((float)Math.Ceiling(moved / MathHelper.ToRadians(5f)), 1f, 10f);

            for (int i = 1; i <= steps; i++)
            {
                float share = i / (float)steps;
                FoighRig.Pose at = PoseOf(State, MathHelper.Lerp(before, clock, share));

                if (LaysBlade(at))
                    FoighRig.Ground(ref at);

                FoighRig.Frame blade = FoighRig.Build(at, Vector2.Lerp(feetWas, Feet, share), facing);

                Vector2 along = (blade.Tip - blade.Guard).SafeNormalize(Vector2.UnitX);
                Vector2 to = blade.Tip + along * 4f;

                // The parry first: the blade met at their arc is turned aside and he with it.
                if (parry.MeetsLine(NPC, blade.Grip, to, BladeWidth))
                {
                    struck = key;
                    return;
                }

                // (One his cut up threw into the air is his to strike down, whatever grace that cut left them.)
                if (me.immune && !Taken)
                    continue;

                float point = 0f;

                if (!Collision.CheckAABBvLineCollision(me.position, me.Size, blade.Guard, to, BladeWidth, ref point))
                    continue;

                Land(me, power, cut, blade, key);
                return;
            }
        }

        /// <summary>
        /// Whether this machine's player is his to strike down: thrown up into the air by
        /// his cut a moment ago, and this the blow that follows it (the one in the air; or,
        /// too near the ground for that, the one that ends on it).
        /// </summary>
        private bool Taken => liftedMe > 0 && ((State == Move.Smite && Arg > 0.5f) || (State == Move.MillEnd && Arg > 1.5f));

        /// <summary>Whether the blow he is in sends whoever it lands on down: the one in the air, and the one that is begun there and ends on the ground.</summary>
        private bool Spikes => State == Move.Smite || (State == Move.MillEnd && Arg > 0.5f);

        private void Land(Player me, float power, int cut, FoighRig.Frame blade, int key)
        {
            int direction = me.Center.X > NPC.Center.X ? 1 : me.Center.X < NPC.Center.X ? -1 : facing;
            int damage = Main.DamageVar(SwordDamage() * power, -me.luck);

            bool thrust = State == Move.One && cut == 3;
            bool up = State == Move.Upcut;
            bool down = Spikes;

            // Up into the air, or down out of it: thrown (RagdollPlayer), not knocked.
            float knock = up || down ? 0f : power > 1f ? KnockHard : thrust ? KnockThrust : KnockCut;

            if (Taken)
            {
                // It lands, whatever they had just been hit by (FoighGripPlayer.Through).
                FoighGripPlayer.Through(me, "FoighSmite", damage, facing);
            }
            else if (!Pierce(me, down ? "FoighSmite" : "FoighSword", damage, direction, knock))
            {
                return;
            }

            struck = key;

            Wound(me);

            if (State == Move.TwoAir)
                Tell(Word.Struck);

            // A blow that killed them throws nothing.
            if (up && !me.dead)
            {
                me.GetModPlayer<RagdollPlayer>().Throw(new Vector2(facing * LaunchAlong, -LaunchUp), RagdollStyle.Spin, LaunchLie);

                // His, now, to go up after: the server is told, and this machine remembers whose they are.
                liftedMe = LiftedTicks;
                Tell(Word.Lifted);

                LiftLooks(me);
            }
            else if (down)
            {
                if (!me.dead)
                    me.GetModPlayer<RagdollPlayer>().Throw(new Vector2(facing * SpikeAlong, SpikeDown), RagdollStyle.Spin, SpikeLie);

                liftedMe = 0;

                SpikeLooks(me);
            }

            HitLooks(me, power, blade);
        }

        /// <summary>A hit on this machine's player that goes through their armour (Player.Hurt with all of their defence ignored). True if it landed.</summary>
        internal static bool Pierce(Player me, string death, int damage, int direction, float knockback)
        {
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText." + death, me.name));

            if (me.Hurt(reason, damage, direction, false, false, ImmunityCooldownID.General, true, 0f, 1f, knockback) <= 0)
                return false;

            // From his second phase a hit of his lets go their hook and shuts their wings for a moment.
            me.GetModPlayer<FoighGripPlayer>().Struck();
            return true;
        }
    }
}
