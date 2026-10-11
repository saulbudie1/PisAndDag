using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// What keeps him from being cut down in a few seconds by whatever hits often and
    /// hard. By request ("he can do something so shit dont just shred him ... zenith and
    /// last prism lk shredding him tf out. (special parry? damage cap??/more flash step
    /// sudden cc bypass(0.2s recover time)?)", and all three of those were picked; "his
    /// second phase is basically a sekiro boss"):
    ///
    ///   THE CAP, in all three phases. He cannot lose more than eight thousandths of his
    ///   life in a second, whatever hits him: so no phase of his is over in much under
    ///   three quarters of a minute. Two seconds' worth can be taken at once, so that a
    ///   hard blow now and then lands whole; with nothing left to take he turns
    ///   everything aside, and goes on doing so until half a second's worth can be
    ///   taken again, so that what lands on him is a blow and not a trickle. What burns
    ///   or bleeds him is not a hit, and comes out of the same. What could have been
    ///   taken from him and was not is kept, up to five seconds' worth, and can be
    ///   taken while he is broken.
    ///
    ///   HIS POSTURE. Everything he turns aside wears at it: what is shot at him (no
    ///   more than nine a second of that), a swing met, and most of all his own sword
    ///   parried; but not what is called up to fight for them (Foigh.Called). Let alone,
    ///   it comes back. Worn through, he is broken: down on a knee with the sword down
    ///   for three seconds, answering nothing; the first blow on him is a riposte, and
    ///   none gets him up.
    ///
    ///   THE HOLDING PARRY, from his second phase. Against anything that keeps coming (a
    ///   blade that passes through him again and again, a beam, a magazine emptied at
    ///   him) one roll is made a second for all of it, not one for each shot: two times
    ///   in three (four in five in his third phase) he turns the whole of it aside for
    ///   that second, in whatever move he is in.
    ///
    ///   THE RUN BEHIND IT, from his second phase ("he can start to run forward after
    ///   parrying goes on for more than 2.5 secs"): held at it that long, he runs at
    ///   whoever it is with the sword up across his front, nothing of theirs landing
    ///   while he does, and cuts.
    ///
    ///   THE BREAK-OUT, from his second phase. Losing life about as fast as he can lose
    ///   it, he is gone from under it: a flash step to behind them, a fifth of a second,
    ///   and a cut up that cannot be turned aside.
    ///
    /// WHO DECIDES. The cap and what is turned aside are worked out on the machine of
    /// whoever's hit it is, where the game works every hit of a player's out; the server
    /// keeps his posture, says when he runs and when he breaks out, and gives back to
    /// his life anything taken from it beyond what the cap allows.
    /// </summary>
    public partial class Foigh
    {
        // ============================================================
        // THE CAP
        // ============================================================

        /// <summary>The share of his whole life that can be taken from him in a second, by request: his second phase (36 hundredths of it) no shorter than 45 seconds.</summary>
        public const float CapRate = 0.008f;

        /// <summary>
        /// Seconds' worth of that which can be taken at once (so that one hard blow every
        /// other second is not clipped); and seconds' worth more that are kept when that
        /// is full, by request, to be taken only while he is broken.
        /// </summary>
        public const float FlowSeconds = 2f;
        public const float BankSeconds = 5f;

        /// <summary>
        /// Once nothing more can be taken from him he turns everything aside until this
        /// much of a second's worth can be again: so that what gets through him is a
        /// blow, and not a trickle of ones.
        /// </summary>
        public const float CapOpens = 0.5f;

        // ---- each machine's own, from the strikes it sees land on him ----

        /// <summary>What can be taken from him now; less than nothing before this machine has begun to count (he is on the road, or in a passage).</summary>
        private float flow = -1f;

        /// <summary>What has been kept beyond that.</summary>
        private float bank;

        /// <summary>What strikes have taken from him since this machine last looked (HitEffect).</summary>
        private int taken;

        /// <summary>Nothing more could be taken from him, and not enough can be again yet (CapOpens).</summary>
        private bool shut;

        /// <summary>His life as this machine's last tick left it.</summary>
        private int lifeWas;

        // ---- where the world is decided only ----

        /// <summary>What has been taken from him beyond the cap and is not yet a whole point of life to give back; and life given back that the others have not been told of.</summary>
        private float overOwed;
        private bool backUntold;

        /// <summary>Whether what has been kept can be taken: broken, or down on a knee from a parry.</summary>
        private bool BankOpen => State == Move.Broken || State == Move.Stagger;

        /// <summary>The most a hit landing now can take from him.</summary>
        public float Allowed => flow < 0f ? float.MaxValue : Math.Max(0f, flow + (BankOpen ? bank : 0f) - taken);

        /// <summary>He answers nothing: broken, down on a knee, or his sword just thrown aside.</summary>
        private bool Unguarded => State == Move.Broken || State == Move.Stagger || State == Move.Reel;

        /// <summary>Nothing more can be taken from him yet, and he is on his feet: whatever comes is turned aside.</summary>
        private bool Capped => !Unguarded && flow >= 0f && (shut || Allowed < 1f);

        /// <summary>
        /// A strike has landed on him, on this machine as on every other: what it took is
        /// counted against the cap. Believed only as far as his life has gone down since
        /// this machine's last tick: the game calls this for the look of a hit as well,
        /// with no strike behind it (a javelin stuck in him, every half second:
        /// Projectile.AI, 1.4.4).
        /// </summary>
        public override void HitEffect(NPC.HitInfo hit)
        {
            if (!Fighting || Passing || flow < 0f)
                return;

            int gone = lifeWas - NPC.life - taken;

            taken += Math.Clamp(hit.Damage, 0, Math.Max(0, gone));
        }

        /// <summary>Asked on the machine working a hit out, before it lands: no more than the cap allows.</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            if (Fighting && !Passing && flow >= 0f)
                modifiers.SetMaxDamage(Math.Max(1, (int)Allowed));
        }

        /// <summary>
        /// Every tick, on every machine: what the strikes of the last tick took comes off
        /// what could be taken (off what is kept too, while he is broken), and a tick's
        /// worth more is let in, less what is burning or bleeding him, which is not a hit
        /// and comes out of the same. Where the world is decided, what was taken from him
        /// beyond that is his again: two machines' hits in one tick, something that asks
        /// nobody, or a burn that is more than the cap by itself.
        /// </summary>
        private void Allowance()
        {
            // The road is not a fight, and a passage is not his to lose: it begins again full after either.
            if (!Fighting || Passing)
            {
                flow = -1f;
                bank = 0f;
                taken = 0;
                shut = false;
                overOwed = 0f;
                lifeWas = NPC.life;
                lostSum = 0;
                Array.Clear(lostRing, 0, lostRing.Length);
                return;
            }

            float second = NPC.lifeMax * CapRate;
            float most = second * FlowSeconds;

            if (flow < 0f)
            {
                flow = most;
                bank = 0f;
            }

            int struck = taken;
            float left = struck;
            float over = 0f;

            taken = 0;

            if (left > 0f)
            {
                float fromFlow = Math.Min(flow, left);

                flow -= fromFlow;
                left -= fromFlow;

                if (left > 0f && BankOpen)
                {
                    float fromBank = Math.Min(bank, left);

                    bank -= fromBank;
                    left -= fromBank;
                }

                // Beyond all there was: his again (below), and not his loss.
                if (left > 0f)
                {
                    over += left;
                    struck -= (int)left;
                }
            }

            if (flow < 1f)
                shut = true;

            // What burns or bleeds him, in points of life a second (NPC.lifeRegen is in
            // halves of one, and the game has taken this tick's already: its buffs come
            // before its AI in NPC.UpdateNPC, 1.4.4).
            float burns = Math.Max(0, -NPC.lifeRegen) * 0.5f;

            if (burns > second)
            {
                over += (burns - second) / 60f;
                burns = second;
            }

            // Given back in whole points, where the world is decided: what is less than
            // one is kept until there is one.
            if (Authority && over > 0f)
            {
                overOwed += over;

                int back = (int)overOwed;

                if (back > 0 && NPC.life > 0)
                {
                    overOwed -= back;
                    NPC.life = Math.Min(NPC.lifeMax, NPC.life + back);
                    backUntold = true;
                }
            }

            // Told no oftener than it can matter: every strike is told as it lands, and this only mends the sum.
            if (backUntold && Main.GameUpdateCount % 6 == 0)
            {
                backUntold = false;
                NPC.netUpdate = true;
            }

            // What he has lost to strikes over the last second, for the break-out: on
            // his feet. What lands on him while he is down is theirs to land, and he
            // does not get up and answer it.
            int stood = Unguarded ? 0 : struck;

            lostSum += stood - lostRing[lostAt];
            lostRing[lostAt] = stood;
            lostAt = (lostAt + 1) % lostRing.Length;

            flow += (second - burns) / 60f;

            if (flow > most)
            {
                bank = Math.Min(second * BankSeconds, bank + flow - most);
                flow = most;
            }

            if (shut && flow >= second * CapOpens)
                shut = false;

            lifeWas = NPC.life;
        }

        // ============================================================
        // HIS POSTURE
        // ============================================================

        /// <summary>Worn through at this.</summary>
        public const float PostureFull = 100f;

        /// <summary>
        /// What wears at it: what is shot at him and turned aside, no more than this much
        /// in a second in all (a stream held off, by how hard each thing in it hits; a
        /// shot struck out of the air by itself); a swing met; his own sword parried.
        /// </summary>
        private const float PostureStream = 9f;
        private const float PostureStruck = 4f;
        private const float PostureMet = 6f;
        private const float PostureParried = 25f;

        /// <summary>Ticks with nothing added before it begins to come back, and how fast it does.</summary>
        private const int PostureHolds = 90;
        private const float PostureBack = 12f / 60f;

        /// <summary>Broken: for this long (M.BrokenTicks); and nothing wears at it then, or for this long after.</summary>
        private const int PostureDeaf = 60;

        /// <summary>How worn it is, 0 to 100: the server's, and sent with him.</summary>
        private float posture;

        // ---- where the world is decided only ----

        private int postureQuiet;
        private float streamLeft = PostureStream;
        private int postureDeaf;
        private float postureSent;

        /// <summary>How worn his posture is, 0 to 1.</summary>
        public float Posture => MathHelper.Clamp(posture / PostureFull, 0f, 1f);

        /// <summary>Worn at by this much, the server's: by what is shot at him, no faster than shots wear at it.</summary>
        private void Wear(float amount, bool stream)
        {
            // (Nothing while he is broken, or for a second after: postureDeaf.)
            if (!Authority || !Fighting || Passing || postureDeaf > 0)
                return;

            if (stream)
            {
                amount = Math.Min(amount, streamLeft);
                streamLeft -= amount;
            }

            if (amount <= 0f)
                return;

            posture = Math.Min(PostureFull, posture + amount);
            postureQuiet = 0;
        }

        /// <summary>Every tick of the fight, the server's: it comes back when it is let alone, and worn through he is broken.</summary>
        private void PostureTick()
        {
            streamLeft = Math.Min(PostureStream, streamLeft + PostureStream / 60f);

            if (postureDeaf > 0)
                postureDeaf--;

            if (State != Move.Broken)
            {
                if (posture >= PostureFull && CanBreak)
                {
                    Break();
                    return;
                }

                if (++postureQuiet > PostureHolds && posture > 0f)
                    posture = Math.Max(0f, posture - PostureBack);
            }

            // Sent when it has moved enough to be seen, and not every tick.
            if (Math.Abs(posture - postureSent) >= 2f && Main.GameUpdateCount % 6 == 0)
            {
                postureSent = posture;
                NPC.netUpdate = true;
            }
        }

        /// <summary>Whether he can be broken this tick: in the fight, the sword in his hand (thrown, it waits until he has it again), and not between two places.</summary>
        private bool CanBreak => Fighting && !Passing && hasSword && State != Move.Flash;

        /// <summary>
        /// A passage begins, the server's: his posture is whole again for his next phase,
        /// nobody has anything on him, and he is open to no riposte in it.
        /// </summary>
        private void Settle()
        {
            posture = 0f;
            postureSent = 0f;
            postureQuiet = 0;
            postureDeaf = 0;
            streamLeft = PostureStream;
            pressedBy = -1;

            NPC.GetGlobalNPC<PADStaggerNPC>().Wake(NPC);
        }

        /// <summary>Worn through, the server's: whoever he has hold of is let go, and he is down.</summary>
        private void Break()
        {
            Release();

            close = false;
            waitingOut = false;
            posture = 0f;
            postureSent = 0f;
            postureDeaf = M.BrokenTicks + PostureDeaf;
            pressedBy = -1;

            // Open to the riposte, as anything staggered is; and it does not wake him.
            NPC.GetGlobalNPC<PADStaggerNPC>().Reel(NPC, M.BrokenTicks, false);

            Go(Move.Broken);
        }

        /// <summary>Down on a knee, the sword down, for as long as it lasts.</summary>
        private void Broken()
        {
            if (fresh)
                NPC.velocity.X = -facing * ReelBack * 0.6f;

            Fall();
            NPC.velocity.X *= 0.85f;

            if (Authority && now >= M.BrokenTicks)
                Rest(16, 28);
        }

        // ============================================================
        // THE HOLDING PARRY: on the machine of whoever's shots they are
        // ============================================================

        /// <summary>
        /// Hits that would have landed, counted: at this many it is a stream; it counts no
        /// higher than this; and it runs down by one when more than this many ticks go by
        /// with none. So what hits him four times a second or more is something that
        /// keeps coming, and anything slower is answered a shot at a time.
        /// </summary>
        private const int StreamAt = 3;
        private const int PressureMost = 8;
        private const int PressureEbbs = 15;

        /// <summary>A roll for a stream stands this long.</summary>
        private const int VerdictTicks = 60;

        /// <summary>The server is told of what has been turned aside no more often than this; and a spark is shown no more often than this.</summary>
        private const int TellEvery = 8;
        private const int SparkEvery = 3;

        /// <summary>What a thing turned aside wears at his posture by: its damage over this, no less and no more than these.</summary>
        private const float WearPer = 60f;
        private const float WearLeast = 0.5f;
        private const float WearMost = 4f;

        // ---- each machine's own, of its own player's shots ----

        private int pressure;
        private int pressureEbb;
        private int verdictLeft;
        private bool verdictHeld;

        /// <summary>Of a second's hard shots held off, one is still to go back at whoever sent them.</summary>
        private bool returnDue;
        private float owedWear;
        private Vector2 owedAt;
        private int tellWait;
        private int sparkWait;

        /// <summary>Whether this machine's player has a stream on him.</summary>
        private bool Streaming => phase >= 2 && pressure >= StreamAt;

        /// <summary>One more hit of theirs that would have landed.</summary>
        private void Press()
        {
            pressure = Math.Min(PressureMost, pressure + 1);
            pressureEbb = PressureEbbs;
        }

        /// <summary>The roll for this second of the stream: made when the last one has run out.</summary>
        private bool StreamHeld()
        {
            if (verdictLeft <= 0)
            {
                verdictHeld = Answers(ShotsStruck);
                verdictLeft = VerdictTicks;
                returnDue = verdictHeld;
            }

            return verdictHeld;
        }

        /// <summary>
        /// Turned aside, on the machine whose hit it was. A thing that stops at what it
        /// hits is struck out of the air and gone at the end of its tick (FoighMark);
        /// anything else is through with him for as long as a hit of it would have been.
        /// It wears at his posture by how hard it hits. Of a stream of hard shots
        /// (<paramref name="stream"/>), one in each second held goes back at whoever sent
        /// them, as one struck out of the air by itself may, while his sword is free to
        /// send it: so that emptying a magazine at him is no safer than a shot at a time.
        /// </summary>
        private void Deflect(Projectile projectile, FoighMark mark, bool stream)
        {
            Vector2 at = Vector2.Clamp(projectile.Center, NPC.TopLeft, NPC.BottomRight);

            // Turned aside is not missed (Foigh.Reach).
            swingLanded = true;

            if (projectile.penetrate == 1)
            {
                mark.Dead = true;
                projectile.alpha = 255;

                if (stream && returnDue && CanReact && Kind(projectile) == Shot.Hard)
                {
                    returnDue = false;

                    ParryEffects.Spark(at);
                    Tell(Word.Parried, at, projectile.velocity * projectile.MaxUpdates, projectile.type, true);
                    return;
                }
            }
            else
            {
                ShareShot(NPC, projectile, 0);
            }

            Sparked(at, MathHelper.Clamp(projectile.damage / WearPer, WearLeast, WearMost));
        }

        /// <summary>Something of this machine's player's turned aside here: the spark, and what the server is owed for it.</summary>
        private void Sparked(Vector2 at, float wear)
        {
            owedWear += wear;
            owedAt = at;

            if (sparkWait > 0)
                return;

            sparkWait = SparkEvery;
            Ward(at);
        }

        /// <summary>Every tick, on a machine with a player: the count runs down, the roll runs out, and the server is told what has been turned aside.</summary>
        private void Holds()
        {
            if (pressure > 0 && --pressureEbb < 0)
            {
                pressure--;
                pressureEbb = PressureEbbs;
            }

            if (verdictLeft > 0)
                verdictLeft--;

            if (sparkWait > 0)
                sparkWait--;

            if (wardSound > 0)
                wardSound--;

            if (tellWait > 0)
                tellWait--;

            if (owedWear <= 0f || tellWait > 0)
                return;

            Tell(Word.Hold, owedAt, default, (int)Math.Round(owedWear * 10f));

            owedWear = 0f;
            tellWait = TellEvery;
        }

        // ============================================================
        // HELD AT IT, AND THE RUN BEHIND IT: where the world is decided
        // ============================================================

        /// <summary>Holds this far apart are one holding; and held at it this long he runs at them (by request: two and a half seconds).</summary>
        private const int PressGap = 30;
        private const int ChargeAfter = 150;

        /// <summary>The run: its speed is his second phase's (SecondRunSpeed), gained this fast; in reach of them at this; and no longer than M.ChargeLongest.</summary>
        private const float ChargeAccel = 0.75f;
        private const float ChargeReach = 92f;

        /// <summary>Who has kept something on him, since which update of the game without a gap, and the last.</summary>
        private int pressedBy = -1;
        private uint pressedSince;
        private uint pressedLast;

        /// <summary>Whether somebody has something on him now.</summary>
        private bool Pressed => pressedBy >= 0 && Main.GameUpdateCount - pressedLast <= PressGap;

        /// <summary>A player's machine has turned something of theirs aside for him: his posture, how long it has gone on, and the sword up if he is free to put it up.</summary>
        private void HeldOff(int from, float wear)
        {
            Wear(wear, true);

            uint tick = Main.GameUpdateCount;

            if (pressedBy != from || tick - pressedLast > PressGap)
            {
                pressedBy = from;
                pressedSince = tick;
            }

            pressedLast = tick;

            if (CanReact)
                Guarded(from);
        }

        /// <summary>
        /// Every tick of the fight, the server's: held at it for two and a half seconds,
        /// and on his feet with nothing else begun, he runs at them.
        /// </summary>
        private void PressTick(Player target)
        {
            if (phase < 2 || !Pressed || Main.GameUpdateCount - pressedSince < ChargeAfter)
                return;

            if (!hasSword || !grounded || (State != Move.Walk && State != Move.Guard && State != Move.Clash))
                return;

            Player who = Main.player[pressedBy];

            if (!who.active || who.dead)
            {
                pressedBy = -1;
                return;
            }

            TargetIndex = who.whoAmI;
            NPC.target = who.whoAmI;

            waitingOut = false;

            FaceThem(who);
            Go(Move.Charge);
        }

        /// <summary>
        /// At them with the sword up across his front. In reach, the blow down and the cut
        /// up (and on into the hand's leap), or his first move's three cuts; against a
        /// wall, or run long enough, he walks again.
        /// </summary>
        private void Charge(Player target)
        {
            Fall();

            if (target != null && Math.Abs(target.Center.X - NPC.Center.X) > 10f)
                Face(target.Center.X > NPC.Center.X ? 1 : -1);

            NPC.velocity.X = Toward(NPC.velocity.X, facing * SecondRunSpeed, ChargeAccel);

            if (!Authority)
                return;

            if (target == null || target.dead || (now > 2 && NPC.collideX) || now >= M.ChargeLongest)
            {
                // (The next run needs as long again, from here.)
                pressedSince = Main.GameUpdateCount;
                Rest(20, 34);
                return;
            }

            if (Math.Abs(target.Center.X - NPC.Center.X) > ChargeReach)
                return;

            pressedSince = Main.GameUpdateCount;

            // Under one who is over his head. Off the ground: up after them, if there is
            // room beside them. Stood on something, or with no room: the run is over,
            // and he thinks again at once (what he has for one over his head is his to
            // pick from: Choose).
            if (Math.Abs(target.Center.Y - NPC.Center.Y) > MeleeAbove)
            {
                if (Aloft(target) && SkyTo(target, false))
                    last = Move.Hang;
                else
                    Rest(8, 14);

                return;
            }

            close = true;
            FaceThem(target);

            if (Main.rand.NextBool())
            {
                last = Move.Raise;
                Go(Move.Hew);
            }
            else
            {
                last = Move.One;
                Go(Move.One);
            }
        }

        // ============================================================
        // THE BREAK-OUT: where the world is decided
        // ============================================================

        /// <summary>
        /// Losing this share of his life in a second (the cap is CapRate) he breaks out
        /// of it, no sooner than this many ticks after the last time; and from where he
        /// lands, this many ticks (by request: a fifth of a second) before the cut.
        /// </summary>
        private const float BreakoutAt = 0.005f;
        private const int BreakoutEvery = 360;
        public const int BreakoutWait = 12;

        /// <summary>What strikes took from him in each of the last sixty ticks, and all of it.</summary>
        private readonly int[] lostRing = new int[60];
        private int lostAt;
        private int lostSum;

        /// <summary>The update of the game he last broke out in.</summary>
        private uint brokeOutAt;

        /// <summary>Whether the move he is in is one he can break out of: on his feet, nobody in his hand, not already thrown off his balance.</summary>
        private bool CanBreakOut
        {
            get
            {
                if (phase < 2 || !hasSword || !grounded || held >= 0)
                    return false;

                switch (State)
                {
                    case Move.Flash:
                    case Move.Grab:
                    case Move.Lift:
                    case Move.Spit:
                    case Move.Spitted:
                    case Move.Fling:
                    case Move.Crouch:
                    case Move.Spring:
                    case Move.Caught:
                    case Move.Pierce:
                    case Move.Hoist:
                    case Move.Reel:
                    case Move.Stagger:
                    case Move.Broken:
                        return false;

                    // In the air, where he is not his own to move; and the cut that throws them up, which he follows.
                    case Move.TwoAir:
                    case Move.FourAir:
                    case Move.FourFall:
                    case Move.MillEnd:
                    case Move.Upcut:
                    case Move.Hang:
                    case Move.Smite:
                    case Move.Drop:
                        return false;

                    // With his hook out, or somebody on it.
                    case Move.HookCast:
                    case Move.Haul:
                    case Move.Winch:
                        return false;

                    // (The one that is the break-out itself.)
                    case Move.Behind:
                        return Arg < 0.5f;

                    default:
                        return Fighting && !Passing;
                }
            }
        }

        /// <summary>
        /// Every tick of the fight, the server's: losing life about as fast as he can
        /// lose it, he is gone from under it. To behind whoever he fights (in front of
        /// them with no room behind), and the cut up from there; up beside one who is
        /// off the ground, and the blow down.
        /// </summary>
        private void BreakoutTick(Player target)
        {
            if (target == null || target.dead || lostSum < NPC.lifeMax * BreakoutAt || !CanBreakOut)
                return;

            if (brokeOutAt != 0 && Main.GameUpdateCount - brokeOutAt < BreakoutEvery)
                return;

            // Tried again in half a second if there is nowhere to go this tick.
            brokeOutAt = Main.GameUpdateCount - BreakoutEvery + 30;

            if (Aloft(target))
            {
                if (!SkyTo(target, false))
                    return;
            }
            else
            {
                int side = target.Center.X >= NPC.Center.X ? 1 : -1;

                bool behind = Stand(target.Center.X + side * BehindGap, target.Bottom.Y, out Vector2 feet)
                    && Math.Abs(feet.X - target.Center.X) >= 24f;

                if (!behind && !(Stand(target.Center.X - side * FrontGap, target.Bottom.Y, out feet) && Math.Abs(feet.X - target.Center.X) >= 24f))
                    return;

                TargetIndex = target.whoAmI;
                NPC.target = target.whoAmI;

                anchor = Feet;
                last = Move.Behind;
                FlashTo(feet, Move.Behind, 1f);
            }

            // The star from the moment he goes (the step itself wears it from its next tick).
            NPC.PAD().CCBypass = true;

            brokeOutAt = Math.Max(1u, Main.GameUpdateCount);
            close = false;
            waitingOut = false;

            lostSum = 0;
            Array.Clear(lostRing, 0, lostRing.Length);
        }
    }
}
