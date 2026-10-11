using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// His third phase, faster and wilder. By request: "His attacks in phase 3 must also
    /// be much faster and wilder and beast like"; "His slashes are more frequent. like he
    /// has a move where he leap up, and turn his body and throw big slashes"; "his jump
    /// and plunge blade down attack from phase 1 but now instead of struggling to pull it
    /// out he plunge it deeper and it creates AOE slashes in a 10 tiles radius circle";
    /// and, of what was offered and picked: half as fast again, with half a second to a
    /// second between moves; delays and feints; and that he learns whoever he fights.
    /// And later: "for his plunge aoe move in 3rd phase I was thinking about the straight
    /// slashes stuff tho, like this plunge ... try to make it again but do damage"; and of
    /// minions ("20 minions ... its shredding him"), "if he takes too much minion damage
    /// within a time frame, he curl up into a ball and explode into 20 tile radius slashes
    /// aoe"; and, "slashes aoe must always be straight cuts", "they dont start and end at
    /// the same time, they have variations ranges and different duration, start and ends",
    /// and "should not go thru a same point".
    ///
    /// HALF AS FAST AGAIN. The sword's moves on his feet run a tick and a half to the
    /// game's tick (Tempo, Wild): the four his second phase had at a tick and a quarter,
    /// and with them the blow down and the cut up, the late cut, the mill, the cut up out
    /// of the ground and his knives. Not what goes by his weight or is timed against
    /// something of a player's: anything in the air, the hand, the hook and its blow,
    /// the three cuts let go of (which are spaced for a parry), the step behind them.
    /// And he waits half a second to a second between moves, wherever the last was
    /// picked from (Rest).
    ///
    /// A CUT LETS GO. A cut of his that ends with them out of its reach, ahead of him,
    /// goes on at them through the air (FoighSlash, as the three cuts' first two), no
    /// oftener than every half second.
    ///
    /// THE WHEEL. Down as for his fourth move, and up: a tile short of twelve, with no
    /// weight at the top of it, turning with the sword out. Each time it comes round to
    /// ahead of him, three times, one of the great cuts is let go off the blade at them
    /// wherever they are (straight down, at one under him). Then he falls.
    ///
    /// DRIVEN DEEPER. The leap that comes down on nobody does not leave the sword stuck:
    /// he comes up over it and drives it in, and from that tick, at uneven ticks,
    /// fourteen straight cuts open out of the ground (FoighLine): each a line across the
    /// ten tiles about the blade, at any slant, of a length of its own, shown a moment of
    /// its own before it opens and hanging a moment of its own after; every third through
    /// wherever they stand as it is shown. When the last has done cutting he tears the
    /// sword out as he did.
    ///
    /// LINGERS AND FEINTS. The sword up, in his first move, his third, or the hand's
    /// blow down: now and then it stays up a while longer than it should before it
    /// comes down (the move's own clock stands still: every machine holds it the
    /// same); and now and then, in the first two, it does not come down at all, and he
    /// is behind them. Neither comes unannounced. The blade flares as he begins a move
    /// it will do either in (the hand's: as its sword is swung up); and where it stays
    /// up it flares again, and his eye lights, a third of a second before it comes
    /// down, and he looks for them again as it does.
    ///
    /// THE BALL, in all three phases. What is called up to fight for them (a minion, a
    /// sentry, and what those shoot) is turned aside at his usual rate and has a cap of its
    /// own (Foigh.Called); when what lands of it has taken nine tenths of all that cap lets
    /// it take in three seconds, he answers it: as soon as he is free to (on his feet,
    /// walking or with his sword up), down and up off the ground a little way, curled up in
    /// the air with nothing landing on him, shaking; and he bursts open into straight cuts
    /// (FoighLine): sixteen lines across the tiles about him, at any slant, each through a
    /// point of its own within three fifths of that of him and of a length of its own;
    /// every fourth through whoever it is for, as they are then. They are let out at uneven
    /// ticks while he is curled up, each shown a moment of its own; the first opens as he
    /// bursts, and the rest one after another after it. Each cuts down whatever it goes
    /// through that was called up to fight for them, and any part of the Stardust Dragon
    /// with the whole of it. No oftener than every ten seconds. By phase, by request:
    ///
    ///   HIS FIRST    white, "slashes not red black blood"; across eighteen tiles; cutting
    ///                down four times in nine ("normal chance").
    ///   HIS SECOND   white; across twenty-three tiles; six times in nine ("1.5x phase 1").
    ///   HIS THIRD    "red and black blood", his burning sword's; across twenty-seven
    ///                tiles; every time ("3x phase 1" is twelve in nine).
    ///
    /// White is steel: what he gathers is air drawn in to him and its light white; he
    /// bursts in a white flash and a ring of steel; the cuts are white (FoighLine).
    ///
    /// And before he bursts, in every phase, by request ("before ball makes him a reverse
    /// roar effect in red and black"): a roar the other way about (Cinematic.Roar, drawn
    /// in): rings of black with red in them close in on him from off the screen, faster
    /// as they come, the screen darkening and shaking more and more, until he bursts.
    ///
    /// WHAT HE LEARNS, from his second phase on, of whoever he fights, and uses in his
    /// third: how much of what he does is parried, how much of the time they are off
    /// the ground, and how much of the time they are out of his reach. The more he is
    /// parried, the oftener the sword lingers or is a feint, and the more of what he
    /// picks up close cannot be parried. The more they are in the air, the more he
    /// picks what goes up after them. The more they keep off, the less he walks.
    ///
    /// WHO DECIDES. The server all of it. A linger is sent with him (how long; or that
    /// it is a feint, for the blade to flare everywhere), and the clock stands still on
    /// every machine by itself. What their minions take from him is counted on the
    /// machine of whoever's they are, where the game works their hits out, and told to
    /// the server (Word.Swarmed), which decides whether he curls up.
    /// </summary>
    public partial class Foigh
    {
        // ============================================================
        // NUMBERS
        // ============================================================

        /// <summary>The moves his third phase quickens run this much of their own clock to the game's tick (yours: half as fast again).</summary>
        public const float ThirdTempo = 1.5f;

        /// <summary>Between moves, in his third phase (yours: half a second to a second). No wait a move asks for is longer than the most of it.</summary>
        private const int ThirdWaitLeast = 30;
        private const int ThirdWaitMost = 60;

        /// <summary>From far off he walks on less (it is 9.5 in his second phase), and thinks again sooner.</summary>
        private const float ThirdWalksOn = 5f;
        private const int ThirdWalkOnLeast = 20;
        private const int ThirdWalkOnMost = 36;

        /// <summary>
        /// The leap back at the end of the first move, at that tempo: lower and faster,
        /// so that he is down again by the tick the move has him down, as far back as
        /// his second phase's took him.
        /// </summary>
        private const float ThirdBackSpeed = 9.2f;
        private const float ThirdBackUp = 4.7f;

        /// <summary>A cut lets go at one further off than this from his middle; and no oftener than this.</summary>
        private const float LetGoBeyond = 150f;
        private const int LetGoEvery = 30;

        /// <summary>
        /// The wheel: how likely he is to begin it, beside what else he may pick, up
        /// close, from further off and at one over his head; from how far off; the room
        /// he needs over his head to begin it; and how fast he leaves the ground (it
        /// falls away to nothing over the ticks he rises for).
        /// </summary>
        private const float WheelNear = 1.2f;
        private const float WheelFar = 2.5f;
        private const float WheelAbove = 3.5f;
        private const float WheelReach = 800f;
        private const float WheelRoom = 130f;
        private const float WheelUp = MaxSpeed;

        /// <summary>
        /// Driven deeper: how many cuts open out of the ground, and within how many ticks
        /// of the first the rest are let out (at uneven ticks, no two at one); how far from
        /// the blade they reach (ten tiles, by request); how near the blade, as a share of
        /// that, the point is that each goes through (over the ground); every how many goes
        /// through wherever they stand; and the ticks he stays over the sword once the last
        /// has done cutting.
        /// </summary>
        private const int LineCount = 14;
        private const int LineSpread = 40;
        private const float LineRadius = 160f;
        private const float LineMiddle = 0.7f;
        private const int LineAtThem = 3;
        private const int DriveAfter = 5;

        /// <summary>
        /// Every straight cut of his, of both moves (Cut): the least share it is of the line
        /// across the circle it is let out in, there; and how much of it is kept either side
        /// of the point it goes through.
        /// </summary>
        private const float CutShortest = 0.45f;
        private const float CutKeep = 24f;

        /// <summary>
        /// The ball: the window what fights for them is counted over, in ticks; the share
        /// of what its own cap lets it take in that (Foigh.Called) that is too much; the
        /// least time between one ball and the next, and between two words from one
        /// machine; how many straight cuts it bursts into; how far from him they reach (by
        /// request eighteen tiles in his first phase, twenty-three in his second, and
        /// twenty-seven in his third); how near
        /// him, as a share of that, the point is that each goes through; every how many
        /// goes through whoever it is for; how many ticks before he bursts the first is let
        /// out (as many as a cut may show for, so that it opens as he bursts), the rest at
        /// uneven ticks after it until he does; and how fast he leaves the ground (it falls
        /// away to nothing over the ticks he rises for).
        /// </summary>
        private const int SwarmWindow = 180;
        private const float SwarmShare = 0.9f;
        private const int SwarmEvery = 600;
        private const int SwarmTellEvery = 60;
        private const int BurstLines = 16;
        private const float BurstReachFirst = 18f * 16f;
        private const float BurstReachSecond = 23f * 16f;
        private const float BurstReachThird = 27f * 16f;
        private const float BurstMiddle = 0.6f;
        private const int BurstAtThem = 4;
        private const int BurstLead = FoighLine.ShowsMost;
        private const float CurlUp = 9f;

        /// <summary>How far from him the ball's cuts reach, in the phase he is in.</summary>
        private float BurstReach => phase >= 3 ? BurstReachThird : phase == 2 ? BurstReachSecond : BurstReachFirst;

        /// <summary>The ball is white in the phase he is in (his first two), not his burning sword's red and black.</summary>
        private bool BallPale => phase < 3;

        /// <summary>How strong the roar drawn in to him before he bursts is (Cinematic.Roar: 1 as a boss's roar).</summary>
        private const float BallRoar = 1f;

        /// <summary>
        /// The sword lingers: of a hundred of the moves it can linger in, this many, and
        /// this many more by how much of what he does is parried; for this long; and the
        /// blade flares again this many ticks before it comes down (so the least it
        /// lingers is no less than that). And is a feint: this many, and this many more.
        /// </summary>
        private const int LingerOdds = 15;
        private const int LingerMore = 45;
        private const int LingerLeast = 20;
        private const int LingerMost = 34;
        private const int LingerTell = 20;
        private const int FeintOdds = 8;
        private const int FeintMore = 27;

        /// <summary>
        /// What he learns: each thing he sees counts for this much of what he takes them
        /// to do, and all he saw before for the rest. And what it comes to at the most:
        /// how many times as likely what goes up after one in the air is, what closes
        /// with one who keeps off, and what no parry turns.
        /// </summary>
        private const float LearnRate = 0.15f;
        private const float SkyBias = 1.5f;
        private const float CloseBias = 1f;
        private const float StarBias = 1f;

        // ============================================================
        // SENT WITH HIM
        // ============================================================

        /// <summary>
        /// The move he is in holds this many ticks where its sword is up (LingerAt),
        /// before it goes on. (The hand's blow down: settled as its sword is swung up,
        /// and kept through the run at them.)
        /// </summary>
        private int linger;

        /// <summary>
        /// The move he is in is a feint: at the tick its sword is up, he is gone. (Sent
        /// as a linger of 255, for the blade to flare on every machine; only where the
        /// world is decided is anything done about it.)
        /// </summary>
        private bool feint;

        // ---- where the world is decided only ----

        /// <summary>What he has learned of whoever he fights, each 0 to 1: how much of what he does is parried; how much of the time they are off the ground; how much of the time they are out of his reach.</summary>
        private float knowsParry;
        private float knowsAloft;
        private float knowsFar;

        /// <summary>The cut the sword was in last tick, of the moves that let go, and the move that was; and ticks before one may let go again.</summary>
        private int cutWas;
        private Move cutIn;
        private int letGoWait;

        /// <summary>
        /// Driven deeper: where the blade went into the ground, as it was driven in (the
        /// middle of the cuts out of it, all of them); the tick each cut is let out at,
        /// counted from DriveBursts; how many are out; and the tick he tears the sword out
        /// at, once all of them are.
        /// </summary>
        private Vector2 drivenAt;
        private readonly int[] lineAt = new int[LineCount];
        private int linesOut;
        private int drivenEnds;

        /// <summary>
        /// The ball: where his middle was as the first of its cuts was let out (the middle of
        /// all of them); the tick each is let out at, counted from BurstLead before he
        /// bursts; and how many are out.
        /// </summary>
        private Vector2 burstMiddle;
        private readonly int[] burstAt = new int[BurstLines];
        private int burstOut;

        /// <summary>The ticks of the move the cuts it has let out so far open on (ShowsFor).</summary>
        private readonly List<int> opensAt = new List<int>();

        /// <summary>A ball is owed (a machine has said its minions took too much), and for whom; and the update of the game the last one began on.</summary>
        private bool swarmDue;
        private int swarmBy = -1;
        private uint swarmLast;

        // ---- each machine's own ----

        /// <summary>
        /// Ticks the move he is in has been held where its sword is up; the move's clock
        /// as it stood then; and whether the tick this machine dealt with last was one
        /// of them.
        /// </summary>
        private int lingered;
        private float heldAt;
        private bool hangs;

        /// <summary>
        /// What this machine's player's minions (and sentries, and what those shoot) have
        /// taken from him: in each of the last SwarmWindow ticks, where in that the next
        /// goes, the sum of it, what this tick has taken so far, and ticks before this
        /// machine may say so again.
        /// </summary>
        private readonly int[] swarmRing = new int[SwarmWindow];
        private int swarmAt;
        private int swarmSum;
        private int swarmNow;
        private int swarmWait;

        // ============================================================
        // HALF AS FAST AGAIN
        // ============================================================

        /// <summary>
        /// Whether this move is one of those his third phase quickens: all his second
        /// phase does, and the rest of the sword's moves on his feet.
        /// </summary>
        private static bool Wild(Move move)
        {
            switch (move)
            {
                case Move.Raise:
                case Move.Hew:
                case Move.Poise:
                case Move.Wake:
                case Move.Level:
                case Move.Mill:
                case Move.Upcut:
                case Move.Knives:
                    return true;

                default:
                    return Quickened(move);
            }
        }

        /// <summary>How much faster the mill carries him, so that three turns of it go as far as they did.</summary>
        private float MillQuick => phase >= 3 ? ThirdTempo : 1f;

        // ============================================================
        // A CUT LETS GO
        // ============================================================

        /// <summary>Whether the sword's cuts in this move go on through the air when they find nobody.</summary>
        private static bool LetsGo(Move move)
        {
            switch (move)
            {
                case Move.One:
                case Move.Three:
                case Move.ThreeRound:
                case Move.FourRound:
                case Move.Behind:
                case Move.Hew:
                case Move.MillEnd:
                case Move.Upcut:
                case Move.Reap:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Every tick of his third phase, where the world is decided: the tick a cut of
        /// the sword's is over, with whoever he fights ahead of him and out of its reach,
        /// it is let go at them. (Over in the move it was made in: a cut that was ended
        /// for him, turned aside or stopped for a passage, lets nothing go.)
        /// </summary>
        private void LetGoTick(Player target)
        {
            if (letGoWait > 0)
                letGoWait--;

            int cut = 0;

            // (He has his sword in every move this is so of. And a move begun this tick has no cut yet: its clock is still the last one's.)
            if (phase >= 3 && State == seen && LetsGo(State))
                Cuts(clock, out _, out cut);

            int over = cutWas;
            Move made = cutIn;

            cutWas = cut;
            cutIn = State;

            // (Nothing was a cut below his third phase: there is nothing to be over.)
            if (over == 0 || cut == over || State != made || letGoWait > 0 || target == null || target.dead)
                return;

            Vector2 off = target.Center - NPC.Center;

            if (off.Length() <= LetGoBeyond || off.X * facing < 0f)
                return;

            letGoWait = LetGoEvery;
            Cast(target, 0, FoighSlash.Plain);
        }

        // ============================================================
        // THE WHEEL
        // ============================================================

        /// <summary>Whether he would begin it from here: near enough, and the room over his head.</summary>
        private bool Wheels(Player target)
        {
            if (phase < 3 || target == null || target.dead || Vector2.Distance(target.Center, NPC.Center) > WheelReach)
                return false;

            float room = NPC.height + WheelRoom;

            return Headroom(Feet, room) >= room;
        }

        /// <summary>Down, as for his fourth move: and up.</summary>
        private void Soar(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            if (Authority && now >= M.FourCrouch)
            {
                FaceThem(target);
                Go(Move.Wheel);
            }
        }

        /// <summary>
        /// Up, slower and slower, and then with no weight, turning. Before the sword
        /// comes round to ahead of him he looks for them; as it does, where the world is
        /// decided, one of the great cuts is let go at them. Then he falls (Drop).
        /// </summary>
        private void Wheel(Player target)
        {
            NPC.velocity.X *= 0.8f;
            NPC.velocity.Y = now < M.WheelRises ? -WheelUp * (1f - now / (float)M.WheelRises) : NPC.velocity.Y * 0.5f;

            foreach (int tick in M.WheelCasts)
            {
                if (At(tick - 3))
                    FaceThem(target);
            }

            if (!Authority)
                return;

            foreach (int tick in M.WheelCasts)
            {
                if (At(tick))
                    WheelCast(target);
            }

            if (now >= M.WheelTicks)
                Go(Move.Drop);
        }

        /// <summary>
        /// One of the wheel's, the server's: a great cut from the middle of the blade as
        /// it comes round, at them wherever they are; and at one it cannot be sent at
        /// from up there, under him or at his back, straight down (SlashWay). Under the
        /// wheel is not somewhere to stand.
        /// </summary>
        private void WheelCast(Player target)
        {
            FoighRig.Frame at = FoighRig.Build(PoseOf(State, clock), Feet, facing);
            Vector2 from = Vector2.Lerp(at.Guard, at.Tip, 0.5f);

            // With a wall where it would begin there is nowhere for it to go.
            if (Collision.SolidCollision(from - new Vector2(5f), 10, 10))
                return;

            Vector2 way = SlashWay(target, from, FoighSlash.GreatTall, FoighSlash.GreatSpeed, true);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), from, way * FoighSlash.GreatSpeed, ModContent.ProjectileType<FoighSlash>(), 0, 0f, Main.myPlayer, NPC.whoAmI, FoighSlash.Great);
        }

        // ============================================================
        // DRIVEN DEEPER
        // ============================================================

        /// <summary>
        /// The sword in the ground after a leap that came down on nobody, in his third
        /// phase: up over it, and driven in; where the world is decided, from that tick the
        /// cuts out of the ground let out one by one, at uneven ticks; and when the last
        /// has done cutting, he tears it out as he did (by DriveTicks at the latest).
        /// </summary>
        private void Driven(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.6f;

            if (!Authority)
                return;

            if (At(M.DriveBursts))
            {
                drivenAt = DrivenAt();
                Uneven(lineAt, LineSpread);
                linesOut = 0;
                drivenEnds = 0;
                opensAt.Clear();
            }

            while (now >= M.DriveBursts && linesOut < LineCount && now >= M.DriveBursts + lineAt[linesOut])
                Line(target, linesOut++);

            if ((now > M.DriveBursts && linesOut >= LineCount && now >= drivenEnds) || now >= M.DriveTicks)
                Go(Move.TwoFree, Arg);
        }

        /// <summary>
        /// Ticks for as many things as <paramref name="at"/> holds, the first at nothing and
        /// the rest anywhere within <paramref name="spread"/> ticks of it, no two at one, in
        /// order.
        /// </summary>
        private static void Uneven(int[] at, int spread)
        {
            int[] ticks = new int[spread - 1];

            for (int i = 0; i < ticks.Length; i++)
                ticks[i] = i + 1;

            for (int i = 0; i < at.Length - 1; i++)
            {
                int j = Main.rand.Next(i, ticks.Length);
                (ticks[i], ticks[j]) = (ticks[j], ticks[i]);
            }

            at[0] = 0;
            Array.Copy(ticks, 0, at, 1, at.Length - 1);
            Array.Sort(at);
        }

        /// <summary>
        /// The ticks a cut let out now shows for: as few as a cut may, or as makes it open
        /// no sooner than <paramref name="from"/> (a tick of the move), to as many as a cut
        /// may; and of those, on no tick another of this move's cuts opens on (there is always
        /// one: the cuts are let out at ticks of their own, and none let out before this one
        /// can open as late as it can).
        /// </summary>
        private int ShowsFor(int from)
        {
            int fewest = Math.Max(FoighLine.ShowsLeast, from - now);
            int free = 0;

            for (int s = fewest; s <= FoighLine.ShowsMost; s++)
            {
                if (!opensAt.Contains(now + s))
                    free++;
            }

            int pick = Main.rand.Next(Math.Max(1, free));
            int shows = FoighLine.ShowsMost;

            for (int s = fewest; s <= FoighLine.ShowsMost; s++)
            {
                if (free > 0 && opensAt.Contains(now + s))
                    continue;

                if (pick-- == 0)
                {
                    shows = s;
                    break;
                }
            }

            opensAt.Add(now + shows);
            return shows;
        }

        /// <summary>Where the blade goes into the ground, as he is now.</summary>
        private Vector2 DrivenAt()
        {
            FoighRig.Frame at = FoighRig.Build(PoseOf(State, clock), Feet, facing);

            return new Vector2(MathHelper.Lerp(at.Grip.X, at.Tip.X, 0.45f), Feet.Y);
        }

        /// <summary>
        /// One cut out of the ground, the server's: across the ten tiles about the blade
        /// (Cut). The first and every third after it goes through them where they stand now
        /// (OnThem), if that is within the ten tiles; any other through anywhere over the
        /// ground within seven of the blade, as likely in one place as in any other.
        /// </summary>
        private void Line(Player target, int i)
        {
            Vector2 middle = drivenAt;
            Vector2 through;

            if (i % LineAtThem == 0 && target != null && !target.dead && Vector2.Distance(target.Center, middle) < LineRadius)
            {
                through = OnThem(target);
            }
            else
            {
                float far = LineRadius * LineMiddle * (float)Math.Sqrt(Main.rand.NextFloat());
                float round = Main.rand.NextFloat(MathHelper.Pi);

                through = middle + new Vector2((float)Math.Cos(round) * far, -(float)Math.Sin(round) * far);
            }

            int shows = Cut(middle, LineRadius, through, 0, false);

            // He tears the sword out once the last has done cutting.
            drivenEnds = Math.Max(drivenEnds, now + shows + FoighLine.Opens - 1 + DriveAfter);
        }

        /// <summary>
        /// A point on them, anywhere in the middle of their body (four fifths of it each way):
        /// a cut through it is through them, and the cuts at them do not all go through one
        /// point (by request, "should not go thru a same point").
        /// </summary>
        private static Vector2 OnThem(Player target)
        {
            return target.Center + new Vector2(Main.rand.NextFloat(-1f, 1f) * target.width * 0.4f, Main.rand.NextFloat(-1f, 1f) * target.height * 0.4f);
        }

        /// <summary>
        /// One straight cut, the server's (FoighLine): at any slant, through
        /// <paramref name="through"/>, on the line that way across the circle of
        /// <paramref name="radius"/> about <paramref name="middle"/>; of a length of its own
        /// (from a little under half of that line to all of it), wherever along it so long as
        /// the point is on it; shown ticks of its own, opening no sooner than
        /// <paramref name="from"/> (a tick of the move) and on a tick none of the move's
        /// others does (ShowsFor), and hanging ticks of its own; and the ball's
        /// (<paramref name="ball"/>) cuts down what fights for them this many times in nine
        /// (<paramref name="downs"/>), white or not (<paramref name="pale"/>) (FoighLine).
        /// The ticks it shows for.
        /// </summary>
        private int Cut(Vector2 middle, float radius, Vector2 through, int from, bool ball, bool pale = false, int downs = 0)
        {
            // The way it lies, and the line across the circle that way through the point: as
            // far each side of the point on it nearest the middle as the circle goes.
            float turn = Main.rand.NextFloat(MathHelper.Pi);
            Vector2 along = turn.ToRotationVector2();
            Vector2 off = through - middle;
            float on = Vector2.Dot(off, along);
            float across = (float)Math.Sqrt(Math.Max(0f, radius * radius - off.LengthSquared() + on * on));

            // Its own length of that, and where along it: wherever the point it goes
            // through is on it, with a little of it either side where there is room.
            float length = 2f * across * Main.rand.NextFloat(CutShortest, 1f);
            float keep = Math.Min(CutKeep, length * 0.5f);
            float start = MathHelper.Clamp(Main.rand.NextFloat(on + keep - length, on - keep), -across, across - length);
            int shows = ShowsFor(from);

            FoighLine.Let(NPC, through + along * (start - on + length * 0.5f), turn, length * 0.5f, shows, Main.rand.Next(FoighLine.HangsLeast, FoighLine.HangsMost + 1), ball, pale, downs);
            return shows;
        }

        // ============================================================
        // THE BALL
        // ============================================================

        /// <summary>Something of this machine's player's has landed on him (OnHitByProjectile): counted, if it fights for them.</summary>
        private void Swarmed(Projectile projectile, int damageDone)
        {
            if (projectile.owner == Main.myPlayer && damageDone > 0 && projectile.IsMinionOrSentryRelated)
                swarmNow += damageDone;
        }

        /// <summary>What is too much in the window: nine tenths of what its own cap lets what fights for them take in it (Foigh.Called).</summary>
        private float SwarmFrom => NPC.lifeMax * CapRate * CalledShare * (SwarmWindow / (float)CalledWindow) * SwarmShare;

        /// <summary>
        /// Every tick, on a machine with a player: what their minions took from him this
        /// tick goes into the window, and the oldest tick of it out. Too much in it, and
        /// the server is told (and the window begins again empty).
        /// </summary>
        private void Swarms()
        {
            if (swarmWait > 0)
                swarmWait--;

            swarmSum += swarmNow - swarmRing[swarmAt];
            swarmRing[swarmAt] = swarmNow;
            swarmAt = (swarmAt + 1) % SwarmWindow;
            swarmNow = 0;

            // (Nothing lands on him on the road or in a passage: the window empties itself by then.)
            if (swarmWait > 0 || swarmSum < SwarmFrom)
                return;

            swarmWait = SwarmTellEvery;
            swarmSum = 0;
            Array.Clear(swarmRing, 0, SwarmWindow);

            Tell(Word.Swarmed);
        }

        /// <summary>A machine says its player's minions have taken too much, where the world is decided: a ball is owed, unless one began too lately.</summary>
        private void Swarming(int from)
        {
            if (!Fighting || Passing || (swarmLast != 0 && Main.GameUpdateCount - swarmLast < SwarmEvery))
                return;

            swarmDue = true;
            swarmBy = from;
        }

        /// <summary>
        /// Every tick of the fight, where the world is decided: a ball owed, and he is free
        /// for it (on his feet, walking or with his sword up), he curls up, facing whoever
        /// it is for. One owed as a passage begins is not owed after it.
        /// </summary>
        private void SwarmTick()
        {
            if (!swarmDue)
                return;

            if (!Fighting || Passing)
            {
                swarmDue = false;
                return;
            }

            if (!CanReact || !grounded)
                return;

            swarmDue = false;
            swarmLast = Math.Max(1u, Main.GameUpdateCount);

            Player by = swarmBy >= 0 && swarmBy < Main.maxPlayers ? Main.player[swarmBy] : null;

            if (by != null && by.active && !by.dead)
            {
                TargetIndex = by.whoAmI;
                NPC.target = by.whoAmI;
                FaceThem(by);
            }

            close = false;
            waitingOut = false;
            Go(Move.Curl);
        }

        /// <summary>
        /// Down, and up off the ground a little way, slower and slower, and then with no
        /// weight, curled up and shaking; nothing lands on him meanwhile (Untouchable).
        /// Where the world is decided, his cuts are let out one by one while he is curled
        /// up, at uneven ticks, to open from the tick he bursts on. He looks for them again
        /// just before he bursts. Then he falls (Drop).
        /// </summary>
        private void Curl(Player target)
        {
            NPC.velocity.X *= 0.8f;

            if (now < M.CurlLeaps)
                Fall();
            else if (now < M.CurlLeaps + M.CurlRises)
                NPC.velocity.Y = -CurlUp * (1f - (now - M.CurlLeaps) / (float)M.CurlRises);
            else
                NPC.velocity.Y *= 0.5f;

            if (At(M.CurlBursts - 3))
                FaceThem(target);

            if (!Authority)
                return;

            if (At(M.CurlBursts - BurstLead))
            {
                burstMiddle = NPC.Center;
                Uneven(burstAt, BurstLead);
                burstOut = 0;
                opensAt.Clear();
            }

            while (now >= M.CurlBursts - BurstLead && burstOut < BurstLines && now >= M.CurlBursts - BurstLead + burstAt[burstOut])
                Burst(target, burstOut++);

            if (now >= M.CurlTicks)
                Go(Move.Drop);
        }

        /// <summary>
        /// One of the ball's cuts, the server's: across the tiles about him the phase he is
        /// in has it reach (Cut), opening no sooner than he bursts (the first, let out as
        /// long before as any cut shows, just as he does); white or not, and as often
        /// cutting down, as that phase has it. The first and every fourth after it goes
        /// through whoever it is for where they are now (OnThem), if that is within its
        /// reach; any other through anywhere within three fifths of that of him, as likely
        /// in one place as in any other: none through one point with another but by chance.
        /// </summary>
        private void Burst(Player target, int i)
        {
            Vector2 middle = burstMiddle;
            Vector2 through;

            if (i % BurstAtThem == 0 && target != null && !target.dead && Vector2.Distance(target.Center, middle) < BurstReach)
            {
                through = OnThem(target);
            }
            else
            {
                float far = BurstReach * BurstMiddle * (float)Math.Sqrt(Main.rand.NextFloat());

                through = middle + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * far;
            }

            Cut(middle, BurstReach, through, M.CurlBursts, true, BallPale, FoighLine.CutsDown(phase));
        }

        // ============================================================
        // LINGERS AND FEINTS
        // ============================================================

        /// <summary>The tick of this move at which the sword is up and can stay up; none, in a move that has no such tick.</summary>
        private static float LingerAt(Move move)
        {
            switch (move)
            {
                case Move.One: return M.OneHold;
                case Move.Three: return M.ThreeHold;
                case Move.Hew: return M.HewHold;
                default: return -1f;
            }
        }

        /// <summary>Whether a move with the sword up in it can be a feint: the two of his first phase's that a player has known longest.</summary>
        private static bool Feints(Move move) => move == Move.One || move == Move.Three;

        /// <summary>
        /// A move is begun, where the world is decided (Go): in his third phase, whether
        /// its sword will linger, and how long, or whether it is a feint. By how much of
        /// what he does is parried.
        /// </summary>
        private void Wiles(Move move)
        {
            // The hand's blow down: settled as its sword was swung up, and kept through
            // the run at them, so that the blade can flare from the first of it.
            if (move == Move.Rush || move == Move.Hew)
                return;

            linger = 0;
            feint = false;

            Move sword = move == Move.Raise ? Move.Hew : move;

            if (phase < 3 || LingerAt(sword) < 0f)
                return;

            int roll = Main.rand.Next(100);
            int feints = Feints(sword) ? FeintOdds + (int)Math.Round(FeintMore * knowsParry) : 0;
            int lingers = LingerOdds + (int)Math.Round(LingerMore * knowsParry);

            if (roll < feints)
                feint = true;
            else if (roll < feints + lingers)
                linger = Main.rand.Next(LingerLeast, LingerMost + 1);
        }

        /// <summary>
        /// Whether the move's clock stands still this tick, on every machine: the sword
        /// up, a tick short of where it would come down, and not yet up its time.
        /// </summary>
        private bool SwordHangs
        {
            get
            {
                if (linger <= 0 || lingered >= linger || State != seen)
                    return false;

                float at = LingerAt(State);

                return at >= 0f && clock < at && clock + Tempo >= at;
            }
        }

        /// <summary>How far the move's clock goes on this tick: by its tempo, or not at all.</summary>
        private float ClockStep => SwordHangs ? 0f : Tempo;

        /// <summary>
        /// Every tick, on every machine, as the move's clock is about to go on: whether
        /// it stands still instead. It does for as many ticks as the move lingers. A
        /// third of a second before it goes on the blade flares again, and his eye
        /// lights; and as it does go on he looks for them (gone round behind him while it
        /// stayed up, they are not behind it).
        /// </summary>
        private bool Lingers()
        {
            hangs = SwordHangs;

            if (!hangs)
                return false;

            lingered++;
            heldAt = clock;

            if (!Main.dedServ && linger - lingered == LingerTell - 1)
            {
                Flare();
                Glint(frame.EyeAt, Eye, 28f);
            }

            if (lingered >= linger)
                FaceThem(Target());

            return true;
        }

        /// <summary>
        /// Ticks behind this one that the move's clock stood still through. (The tick
        /// this machine dealt with last, if it was one of them, is not behind it yet:
        /// what it holds is the next.)
        /// </summary>
        private int Stood => Math.Max(0, lingered - (hangs ? 1 : 0));

        /// <summary>
        /// The move's clock as it was this many ticks of the game ago: back by its tempo,
        /// but for the ticks it stood still. (What his blade leaves behind it is laid
        /// along where it has been: Foigh.TrailPoints.)
        /// </summary>
        private float ClockAgo(float ago)
        {
            float tempo = Tempo;
            int stood = Stood;

            if (stood <= 0)
                return clock - ago * tempo;

            // Ticks since it went on again: none, while it stands.
            float since = Math.Max(0f, (clock - heldAt) / tempo);

            if (ago <= since)
                return clock - ago * tempo;

            if (ago <= since + stood)
                return heldAt;

            return heldAt - (ago - since - stood) * tempo;
        }

        /// <summary>
        /// The sword is up, in a move that is a feint, where the world is decided: it
        /// does not come down. He is behind them (and with nowhere to stand there, it
        /// comes down after all). True if he has gone.
        /// </summary>
        private bool Feinted(Player target)
        {
            if (!Authority || !feint || !At((int)LingerAt(State)))
                return false;

            if (target == null || target.dead)
                return false;

            int side = target.Center.X >= NPC.Center.X ? 1 : -1;

            if (!Stand(target.Center.X + side * BehindGap, target.Bottom.Y, out Vector2 feet) || Math.Abs(feet.X - target.Center.X) < 24f)
                return false;

            anchor = Feet;
            last = Move.Behind;
            FlashTo(feet, Move.Behind);
            return true;
        }

        // ============================================================
        // WHAT HE LEARNS
        // ============================================================

        /// <summary>One more thing seen of them, toward what he takes them to do.</summary>
        private static float Learned(float known, bool seen) => known + ((seen ? 1f : 0f) - known) * LearnRate;

        /// <summary>His sword has been parried (Turned), or a move of his has ended without it (Rest), from his second phase.</summary>
        private void LearnParry(bool parried)
        {
            if (phase >= 2)
                knowsParry = Learned(knowsParry, parried);
        }

        /// <summary>He is picking his next move, as he does from his second phase (Choose): where they are.</summary>
        private void LearnPlace(bool aloft, bool far)
        {
            knowsAloft = Learned(knowsAloft, aloft);
            knowsFar = Learned(knowsFar, far);
        }

        /// <summary>
        /// What he picks from, in his third phase, by what he has learned: what goes up
        /// after one in the air counts for more the more they are in it; what closes
        /// with them, or reaches them, the more they keep off, and walking on the less;
        /// what no parry turns, the more he is parried. Anywhere else, as it was given.
        /// </summary>
        private (Move Move, float Weight)[] Swayed(bool near, params (Move Move, float Weight)[] of)
        {
            if (phase < 3)
                return of;

            float sky = 1f + SkyBias * knowsAloft;
            float closes = 1f + CloseBias * knowsFar;
            float star = 1f + StarBias * knowsParry;

            for (int i = 0; i < of.Length; i++)
            {
                switch (of[i].Move)
                {
                    case Move.Hang:
                    case Move.HookCast:
                    case Move.Soar:
                        of[i].Weight *= sky;
                        break;
                }

                switch (of[i].Move)
                {
                    case Move.Walk:
                        of[i].Weight /= closes;
                        break;

                    case Move.TwoGather:
                    case Move.Poise:
                    case Move.HookCast:
                    case Move.Rend:
                    case Move.Soar:
                    case Move.Knives:
                        if (!near)
                            of[i].Weight *= closes;
                        break;

                    // The hand (it begins with the sword swung up) and the cut up out of the ground: the star's.
                    case Move.Raise:
                    case Move.Scrape:
                        of[i].Weight *= star;
                        break;
                }
            }

            return of;
        }

        // ============================================================
        // WHAT IT LOOKS AND SOUNDS LIKE
        // ============================================================

        /// <summary>Every tick, on a machine with a screen: the wheel, the sword driven deeper, and the ball.</summary>
        private void WildLooks(Vector2 feet)
        {
            switch (State)
            {
                case Move.Soar:
                    if (At(M.FourCrouch - 4)) Puff(feet, 6, 2.4f, 0.8f);
                    break;

                case Move.Wheel:
                    if (At(1)) Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, -0.5f, 1f);

                    for (int turn = 0; turn < M.WheelCasts.Length; turn++)
                    {
                        if (At(turn * M.WheelTurn + 2)) Whirl();
                        if (At(M.WheelCasts[turn] - 6)) Flare();
                    }

                    if (now % 3 == 0)
                        LeaveGhost(0.4f);
                    break;

                case Move.TwoStuck when phase >= 3:
                    if (At(M.DriveSets))
                    {
                        Flare();
                        Sound(SoundID.Item71, NPC.Center, -0.6f, 1f);
                    }

                    if (At(M.DriveBursts))
                    {
                        Vector2 at = new Vector2(MathHelper.Lerp(frame.Grip.X, frame.Tip.X, 0.45f), feet.Y);

                        Impact(at, 2.4f);
                        Shake(7f, 16);
                        Sound(SoundID.Item14, at, -0.5f, 0.8f);
                        Sound(SoundID.DD2_SonicBoomBladeSlash, at, -0.6f, 1f);
                        Puff(at, 12, 4f);

                        // The ground about it lit from under, as far as the cuts will go.
                        PADParticleHandler.SpawnParticle(new ParryRing(at, 12f, LineRadius, 18, BurnRed * 0.35f));
                    }

                    // The blade lit where it is in the ground until he tears it out.
                    if (now >= M.DriveBursts)
                        Lighting.AddLight(new Vector2(MathHelper.Lerp(frame.Grip.X, frame.Tip.X, 0.45f), feet.Y - 6f), 0.8f, 0.08f, 0.06f);
                    break;

                case Move.Curl:
                    if (At(M.CurlLeaps))
                    {
                        Puff(feet, 8, 2.6f);
                        Sound(SoundID.DoubleJump, NPC.Center, -0.6f, 0.9f);

                        // A roar drawn in to him, in red and black, in every phase: in from the
                        // leap, and the last of it gone into him as he bursts (by request).
                        Cinematic.Roar(NPC, M.CurlBursts - M.CurlLeaps, BallRoar, quiet: true, rim: BurnRed, inward: true);
                    }

                    if (now > M.CurlLeaps && now < M.CurlBursts)
                        Gathers();

                    // The blade before he bursts: in his first two phases a light along its
                    // steel; in his third, what it burns with.
                    if (At(M.CurlBursts - 20))
                    {
                        if (BallPale)
                        {
                            Gleams();
                        }
                        else
                        {
                            Flare();
                            Sound(SoundID.DD2_BetsyFlameBreath, NPC.Center, -0.8f, 0.7f);
                        }

                        Glint(frame.EyeAt, Eye, 30f);
                    }

                    if (At(M.CurlBursts))
                        Bursts();
                    break;
            }
        }

        /// <summary>
        /// Curled up: what burns off his sword drawn in to him from all round, and a red
        /// light at his middle, stronger by the tick. White, in his first two phases: the
        /// air drawn in to him, and a white light.
        /// </summary>
        private void Gathers()
        {
            Vector2 middle = NPC.Center;
            float k = MathHelper.Clamp((now - M.CurlLeaps) / (float)(M.CurlBursts - M.CurlLeaps), 0f, 1f);

            if (BallPale)
            {
                if (now % 2 == 0)
                {
                    Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(70f, 130f);

                    PADParticleHandler.SpawnParticle(new WindStreak(middle + off, -off * Main.rand.NextFloat(0.05f, 0.08f), Main.rand.NextFloat(18f, 34f) * (0.6f + 0.6f * k), 3f, Main.rand.Next(14, 20)));
                }

                if (now % 3 == 0)
                    PADParticleHandler.SpawnParticle(new BloomGeneric(middle, Vector2.Zero, Steel * (0.2f + 0.3f * k), 0.4f + 0.8f * k, 4));

                Lighting.AddLight(middle, 0.7f * k, 0.75f * k, 0.8f * k);
                return;
            }

            if (now % 2 == 0)
            {
                Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(70f, 130f);

                PADParticleHandler.SpawnParticle(new BloodFume(middle + off, -off * 0.05f, Main.rand.NextFloat(0.08f, 0.14f) * (0.5f + k), Main.rand.Next(16, 24)));
            }

            if (now % 3 == 0)
            {
                Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(50f, 100f);

                PADParticleHandler.SpawnParticle(new Ember(middle + off, -off * 0.06f, Cinder, Main.rand.NextFloat(0.5f, 0.85f), Main.rand.Next(14, 20), 0f));
                PADParticleHandler.SpawnParticle(new BloomGeneric(middle, Vector2.Zero, BurnRed * (0.25f + 0.35f * k), 0.4f + 0.8f * k, 4));
            }

            Lighting.AddLight(middle, 0.9f * k, 0.1f * k, 0.08f * k);
        }

        /// <summary>
        /// The ball bursts, on a machine with a screen: heard and felt, a flash, and a ring
        /// of red going out as far as the cuts will. White, in his first two phases: a
        /// white flash, a ring of steel, and the air thrown out from him with sparks in it.
        /// </summary>
        private void Bursts()
        {
            Vector2 middle = NPC.Center;

            Sound(SoundID.Item14, middle, -0.4f, 1f);
            Sound(SoundID.DD2_SonicBoomBladeSlash, middle, -0.5f, 1f);
            Sound(SoundID.Item71, middle, -0.3f, 1f);
            Shake(9f, 22);

            if (BallPale)
            {
                if (Near())
                    Cinematic.Flash(new Color(236, 244, 255), 0.12f);

                PADParticleHandler.SpawnParticle(new BloomGeneric(middle, Vector2.Zero, Steel * 0.6f, 1.6f, 14));
                PADParticleHandler.SpawnParticle(new ParryRing(middle, 24f, BurstReach, 30, Steel * 0.35f));

                for (int i = 0; i < 16; i++)
                {
                    Vector2 way = (MathHelper.TwoPi * i / 16f).ToRotationVector2();

                    PADParticleHandler.SpawnParticle(new WindStreak(middle + way * 14f, way * Main.rand.NextFloat(6f, 10f), Main.rand.NextFloat(34f, 56f), 4f, Main.rand.Next(12, 18)));
                }

                SparkShard.Spray(middle, Steel, 14, 7f, 26f, 18, falls: true, palette: Sparks);
                return;
            }

            if (Near())
                Cinematic.Flash(new Color(170, 16, 20), 0.12f);

            PADParticleHandler.SpawnParticle(new BloomGeneric(middle, Vector2.Zero, BurnRed * 0.6f, 1.6f, 14));
            PADParticleHandler.SpawnParticle(new ParryRing(middle, 24f, BurstReach, 30, BurnRed * 0.35f));

            for (int i = 0; i < 16; i++)
            {
                Vector2 way = (MathHelper.TwoPi * i / 16f).ToRotationVector2();

                PADParticleHandler.SpawnParticle(new BurnSmoke(middle + way * 10f, way * Main.rand.NextFloat(2f, 4.5f), Soot, BurnRed,
                    Main.rand.Next(26, 40), Main.rand.NextFloat(12f, 18f), Main.rand.NextFloat(34f, 50f), Main.rand.NextFloat(0.75f, 0.95f)));
            }
        }

        /// <summary>The blade, curled up, before he bursts white: a light along the whole of its steel at once, and heard.</summary>
        private void Gleams()
        {
            Vector2 along = frame.Tip - frame.Guard;

            Sound(SoundID.Item71, frame.Guard + along * 0.5f, -0.5f, 0.6f);
            Sound(SoundID.Tink, frame.Tip, 0.4f, 0.4f);
            Glint(frame.Tip, Steel, 34f, 14);

            for (int i = 0; i < 4; i++)
                SparkShard.Spray(frame.Guard + along * (0.25f + 0.25f * i), Steel, 2, 3f, 14f, 10, palette: Sparks);
        }

        /// <summary>What going into one of them looks and sounds like, the tick this machine sees it.</summary>
        private void WildEntered(Vector2 feet)
        {
            // A move its sword will linger in, or not come down in: the blade flares as he begins it.
            if ((linger > 0 || feint) && (State == Move.One || State == Move.Three || State == Move.Raise))
                Flare();

            switch (State)
            {
                case Move.Soar:
                    Sound(SoundID.Item71, NPC.Center, -0.5f, 0.7f);
                    Glint(frame.EyeAt, Eye, 26f);
                    break;

                case Move.Wheel:
                    Sound(SoundID.DoubleJump, NPC.Center, -0.5f, 0.9f);
                    Puff(feet, 9, 3.2f);
                    break;

                case Move.Curl:
                    Sound(SoundID.Item71, NPC.Center, -0.7f, 0.8f);
                    Glint(frame.EyeAt, Eye, 26f);
                    break;
            }
        }
    }
}
