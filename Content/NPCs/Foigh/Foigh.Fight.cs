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

        /// <summary>The leap onto them: the ground it covers a tick (which says how long it takes), the least and most ticks, the furthest he will try, and the hardest he will jump.</summary>
        private const float LeapPace = 9f;
        private const int LeapLeast = 34;
        private const int LeapMost = 56;
        private const float LeapFurthest = 480f;
        private const float LeapUpMost = MaxSpeed;

        /// <summary>His feet come down this far short of them: it is the point that comes down on them.</summary>
        private const float LeapShort = 40f;

        /// <summary>A leap that has not come down by now has gone wrong somewhere: he is put back on his feet.</summary>
        private const int LeapLongest = 200;

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

        /// <summary>At nothing: ticks down on his knee, and the last of them he fades over.</summary>
        private const int DyingTicks = 150;
        private const int DyingFade = 50;

        /// <summary>Nobody left: ticks he stands before he is gone.</summary>
        private const int LeaveTicks = 150;

        // ============================================================
        // EACH MACHINE'S OWN
        // ============================================================

        /// <summary>Set once he has gone from Dying: the blow that ends him is his own.</summary>
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

        /// <summary>Where his feet were a tick ago.</summary>
        private Vector2 feetWas;
        private bool groundedWas = true;

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

        /// <summary>Back to walking, with this long before his next move.</summary>
        private void Rest(int least, int most)
        {
            if (!Authority)
                return;

            rest = Main.rand.Next(least, most + 1);
            Go(Move.Walk);
        }

        /// <summary>How far a movement's steps carry him this tick.</summary>
        private float Stepped(M.Step[] steps) => facing * (M.Advance(steps, now + 1) - M.Advance(steps, now));

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

            switch (State)
            {
                case Move.Walk: Walk(target); break;
                case Move.One: One(target); break;

                case Move.TwoGather: TwoGather(target); break;
                case Move.TwoAir: TwoAir(); break;
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
                case Move.Guard: Guard(); break;
                case Move.Clash: Clash(); break;

                case Move.Grab: Grab(); break;
                case Move.Miss: Still(M.MissTicks, 30, 44); break;
                case Move.Lift: Holding(M.LiftTicks, Move.Spit); break;
                case Move.Spit: Holding(M.SpitUp, Move.Spitted); break;
                case Move.Spitted: Holding(M.SpittedTicks, Move.Fling); break;
                case Move.Fling: Fling(); break;

                case Move.Reel: Reel(); break;
                case Move.Stagger: Stagger(); break;
                case Move.Dying: Dying(); break;
                case Move.Leave: Leave(); break;
            }

            if (Authority && drank > 0)
            {
                drank--;
                Punish();
            }
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

        /// <summary>One of these by its weight; the one he did last is a quarter as likely.</summary>
        private Move Pick(params (Move Move, float Weight)[] of)
        {
            float all = 0f;

            for (int i = 0; i < of.Length; i++)
            {
                if (of[i].Move == last && of[i].Move != Move.Knives)
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

            bool near = far <= MeleeRange && d.Y > -MeleeAbove && d.Y < MeleeBelow;
            bool high = d.Y < -HighOver;
            float leapWeight = far < LeapFurthest ? 2f : 0f;

            Move pick;

            if (near)
                pick = Pick((Move.One, 3f), (Move.Three, 3f), (Move.TwoGather, 2f), (Move.FourGather, 2f));
            else if (high)
                pick = Pick((Move.Knives, 4f), (Move.FourGather, 2.5f), (Move.TwoGather, leapWeight));
            else
                pick = Pick((Move.Knives, 5.5f), (Move.Behind, 2.5f), (Move.TwoGather, leapWeight));

            last = pick;
            FaceThem(target);

            switch (pick)
            {
                case Move.Knives:
                    Go(Move.Knives, Main.rand.Next(100) < ThreeKnives ? 3f : 1f);
                    break;

                case Move.Behind:
                    StepBehind(target);
                    break;

                default:
                    Go(pick);
                    break;
            }
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

            if (now < M.OneLeap + 2)
            {
                NPC.velocity.X = Stepped(M.OneSteps);
            }
            else if (At(M.OneLeap + 2))
            {
                NPC.velocity = new Vector2(-facing * BackSpeed, -BackUp);
            }
            else if (grounded)
            {
                NPC.velocity.X *= 0.72f;
            }
            else
            {
                NPC.velocity.X = -facing * BackSpeed;
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
        /// comes down on them, over a time that grows with how far it is.
        /// </summary>
        private void Launch(Player target)
        {
            Vector2 feet = Feet;
            Vector2 to = target != null ? new Vector2(target.Center.X, target.Bottom.Y) : feet + new Vector2(facing * 200f, 0f);

            int ticks = (int)MathHelper.Clamp(Math.Abs(to.X - feet.X) / LeapPace, LeapLeast, LeapMost);

            if (target != null)
                to.X += target.velocity.X * ticks * 0.35f;

            float dx = MathHelper.Clamp(to.X - feet.X, -LeapFurthest - 80f, LeapFurthest + 80f);

            dx -= Math.Sign(dx) * Math.Min(LeapShort, Math.Abs(dx) * 0.5f);

            // Up to them if they are over him; never down after them.
            float dy = Math.Min(0f, to.Y - feet.Y);
            float up = Math.Max(dy / ticks - Gravity * ticks * 0.5f, -LeapUpMost);

            leap = new Vector2(dx / ticks, up);
            above = false;
            landedHit = false;

            Face(dx >= 0f ? 1 : -1);
            Go(Move.TwoAir, ticks);
        }

        private void TwoAir()
        {
            // The first tick of it, on every machine: off the ground.
            if (fresh)
                NPC.velocity = leap;

            Fall();

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
            FoighRig.Frame at = FoighRig.Build(PoseOf(State, now), Feet, facing);

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

            NPC.velocity.X = there ? NPC.velocity.X * 0.5f : Toward(NPC.velocity.X, facing * RunSpeed, RunAccel);

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

            int count = Math.Max(1, (int)Arg);

            for (int k = 0; k < count; k++)
            {
                if (At(k * M.KnifeEvery))
                    FaceThem(target);

                if (Authority && At(M.KnifeAt(k)))
                    ThrowKnife(target, false);
            }

            if (!Authority || now < M.KnifeLength(count))
                return;

            // After three, now and then, he comes after them.
            if (count >= 3 && target != null && Math.Abs(target.Center.X - NPC.Center.X) < LeapFurthest && Main.rand.Next(100) < LeapAfterKnives)
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

        /// <summary>A knife out of one of his hands, the server's: at where they will be, half of where they are going allowed for.</summary>
        private void ThrowKnife(Player target, bool nearHand)
        {
            FoighRig.Frame at = FoighRig.Build(PoseOf(State, now), Feet, facing);
            Vector2 hand = nearHand ? at.NearFist : at.FarFist;

            Vector2 to = target != null ? target.Center : hand + new Vector2(facing * 300f, 0f);

            if (target != null)
                to += target.velocity * (Vector2.Distance(hand, to) / KnifeSpeed) * 0.5f;

            Vector2 way = (to - hand).SafeNormalize(new Vector2(facing, 0f));

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

            // The hand that follows wears the star from the moment he goes.
            if (then == Move.Grab)
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

            FaceThem(them);

            if (then != Move.TwoAir)
            {
                Go(then, thenArg);
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

            if (!Authority || now < M.BehindTicks)
                return;

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
            Vector2 hand = FoighRig.Build(PoseOf(State, now), Feet, facing).FarFist;
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
                case Move.Fling when now < M.FlingLets:
                    middle = Vector2.Lerp(frame.Guard, frame.Tip, along);
                    turn = frame.Blade + MathHelper.Pi;
                    return true;

                default:
                    return false;
            }
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
                grip.Hold(NPC.whoAmI, middle, turn);
        }

        /// <summary>The sword up through this machine's player: once, through armour, whatever they had just been hit by.</summary>
        private void Impale()
        {
            if (Main.dedServ || held != Main.myPlayer)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead)
                return;

            int damage = Main.DamageVar(SwordDamage(), -me.luck);

            FoighGripPlayer.Through(me, "FoighSpitted", damage, facing);

            me.PIS().BumpScreenShake(9f, 16);
            Cinematic.Flash(new Color(255, 232, 220), 0.12f);
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
                    case Move.Behind:
                        return true;

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
        // THE END OF IT
        // ============================================================

        private void Dying()
        {
            Fall();
            NPC.velocity.X *= 0.8f;

            if (now >= DyingTicks - DyingFade)
                NPC.alpha = (int)MathHelper.Clamp((now - (DyingTicks - DyingFade)) * 255f / DyingFade, 0f, 255f);

            if (!Authority || now < DyingTicks)
                return;

            // Nothing off him yet, not the potions and hearts the game drops for any boss, nor
            // its line in the chat: what he leaves waits for how it ends (the lore's plan).
            gone = true;
            NPC.PAD().GivesNothing = true;
            NPC.life = 0;
            NPC.checkDead();

            // checkDead sends nothing itself; this is how vanilla's own despawn tells the
            // clients (NPC.CheckActive: life 0, then SyncNPC).
            if (Main.netMode == NetmodeID.Server && !NPC.active)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

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
                    return M.Pulling(t, Arg);

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
                    return M.Knife(t, Math.Max(1, (int)Arg));

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

                case Move.Grab:
                    return M.Grab.Sample(t);

                case Move.Miss:
                    return M.Miss.Sample(t);

                case Move.Lift:
                    return M.Lift.Sample(t);

                case Move.Spit:
                    return M.Spit.Sample(t);

                case Move.Spitted:
                    return M.Spitted(t);

                case Move.Fling:
                    return M.Fling.Sample(t);

                case Move.Reel:
                    return (reel ??= M.Reel(M.Drag)).Sample(t);

                case Move.Stagger:
                    return M.Kneeling(t);

                case Move.Dying:
                    return M.Fallen(t);

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
            if (!hasSword || !Cuts(now, out float power, out int cut))
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

            float before = Math.Max(0f, now - 1f);
            FoighRig.Pose first = PoseOf(State, before);
            FoighRig.Pose final = PoseOf(State, now);

            float moved = Math.Abs(final.Sword - first.Sword) + Math.Abs(final.Turn - first.Turn) * 1.5f + Math.Abs(final.NearArm - first.NearArm);
            int steps = (int)MathHelper.Clamp((float)Math.Ceiling(moved / MathHelper.ToRadians(5f)), 1f, 10f);

            for (int i = 1; i <= steps; i++)
            {
                float share = i / (float)steps;
                FoighRig.Pose at = PoseOf(State, MathHelper.Lerp(before, now, share));

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

                if (me.immune)
                    continue;

                float point = 0f;

                if (!Collision.CheckAABBvLineCollision(me.position, me.Size, blade.Guard, to, BladeWidth, ref point))
                    continue;

                Land(me, power, cut, blade, key);
                return;
            }
        }

        private void Land(Player me, float power, int cut, FoighRig.Frame blade, int key)
        {
            int direction = me.Center.X > NPC.Center.X ? 1 : me.Center.X < NPC.Center.X ? -1 : facing;
            int damage = Main.DamageVar(SwordDamage() * power, -me.luck);

            bool thrust = State == Move.One && cut == 3;
            float knock = power > 1f ? KnockHard : thrust ? KnockThrust : KnockCut;

            if (!Pierce(me, "FoighSword", damage, direction, knock))
                return;

            struck = key;

            if (State == Move.TwoAir)
                Tell(Word.Struck);

            HitLooks(me, power, blade);
        }

        /// <summary>A hit on this machine's player that goes through their armour (Player.Hurt with all of their defence ignored). True if it landed.</summary>
        internal static bool Pierce(Player me, string death, int damage, int direction, float knockback)
        {
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText." + death, me.name));

            return me.Hurt(reason, damage, direction, false, false, ImmunityCooldownID.General, true, 0f, 1f, knockback) > 0;
        }
    }
}
