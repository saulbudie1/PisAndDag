using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Projectiles.Base;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// 'Dame' Quilt in the saddle: the road; the charge; the throw, Triunfo running them down
    /// and the ride back for the lance; the swing; going into a wall; being parried; pulling
    /// up, the rear, the turn round; and the fall that ends it. She is the rider and Triunfo the legs: every tick
    /// she says where she wants to go (<see cref="ReinsFor"/>) and he goes there, with what sense
    /// he has of the ground (DonkeyNav). What she does next is the server's to pick
    /// (<see cref="NextAttack"/>); what lands on a player, each player's own machine finds, as
    /// anything that hurts by touch is found.
    /// </summary>
    public partial class DonaQuinn
    {
        // ============================================================
        // THE FIGHT'S NUMBERS
        // ============================================================

        /// <summary>How far past the player, where they are now, she rides before she pulls up.</summary>
        private const float PassBeyond = 300f;

        /// <summary>No charge goes on longer than this, whatever it has or has not reached.</summary>
        private const int ChargeLongest = 480;

        /// <summary>Pulling up: pixels a tick off the speed. From flat out, about two thirds of a second.</summary>
        private const float BrakeDecel = 0.18f;

        /// <summary>Up on his hind legs this long; a beat this long after he flips round; a breath this long before what comes next.</summary>
        private const int RearTicks = 66;
        private const int TurnTicks = 10;
        private const int ReadyTicks = 16;

        /// <summary>From "Coward!" to the first charge.</summary>
        private const int ChallengeTicks = 90;

        /// <summary>Shaken this long before she comes off.</summary>
        private const int BuckThrowAt = 30;

        /// <summary>Nobody answering the road for this long is an answer.</summary>
        private const int IntroTimeout = 3600;

        /// <summary>A player further than this has gone, as far as she is concerned.</summary>
        private const float GoneFar = 4000f;

        /// <summary>Riding off, she is gone the moment nobody sees her after this, and regardless after the second.</summary>
        private const int LeaveFirst = 60;
        private const int LeaveLongest = 1500;

        /// <summary>Stuck this many ticks against something, or pulled up at it, and that way is given up.</summary>
        private const int GivesUpAfter = 12;

        // ---- the road ----------------------------------------------------------------

        /// <summary>When on the road each line comes, and when Triunfo rears as her name goes up.</summary>
        private const int FirstLineAt = 20;
        private const int SecondLineAt = 110;
        private const int RevealRearAt = SecondLineAt + 26;
        private const int RevealRearTicks = 66;

        // ---- the charge --------------------------------------------------------------

        /// <summary>
        /// The lance, right-facing (0 upright, a quarter turn forward is level): aimed at them as
        /// she comes, between a little over level and well down; low and ahead when there is
        /// nobody in front to aim at.
        /// </summary>
        private const float AimHigh = MathHelper.PiOver2 - 0.1f;
        private const float AimLow = MathHelper.PiOver2 + 0.45f;
        private const float Couched = MathHelper.PiOver2 + 0.2f;

        /// <summary>A strike counts with the lance anywhere from here to here, and at this speed or more.</summary>
        private const float CouchedFrom = MathHelper.PiOver2 - 0.35f;
        private const float CouchedTo = MathHelper.PiOver2 + 0.65f;
        private const float StrikeSpeed = 2f;

        /// <summary>The part of the lance that strikes: from this far in front of the fist to this far past the point, this thick.</summary>
        private const float StrikeFrom = 10f;
        private const float StrikePast = 10f;
        private const float StrikeWidth = 24f;

        /// <summary>How hard a strike knocks the player along (a shield stops this; it is ordinary knockback).</summary>
        private const float StrikeKnockback = 7.5f;

        // ---- the throw, the ram, the fetch ---------------------------------------------

        /// <summary>The throw: drawn back, let go at this tick, and on to the ram at the end.</summary>
        private const int WindTicks = 56;
        private const int ReleaseAt = 32;

        /// <summary>She throws at players this near and no nearer, this far and no further, and this far up or down at most.</summary>
        private const float ThrowNearest = 160f;
        private const float ThrowFurthest = 640f;
        private const float ThrowHighest = 360f;

        /// <summary>At a player she cannot get to and cannot throw at from here: she rides to this far from them, for this long at most.</summary>
        private const float ThrowSpot = 320f;
        private const int RangeLongest = 180;

        /// <summary>Triunfo hits at this speed or more, and knocks them along this hard.</summary>
        private const float RamFrom = 2f;
        private const float RamKnockback = 8f;

        /// <summary>Back for the lance: how fast, how hard he gets going, how near she has to be to take it out of the ground.</summary>
        private const float FetchSpeed = 4.8f;
        private const float FetchAccel = 0.15f;
        private const float PluckReach = 24f;

        /// <summary>
        /// If she cannot get to it (it went over a cliff, into a lake she cannot get out of), a
        /// fetch this long, or this long without it at all, and it is in her hand again anyway.
        /// </summary>
        private const int FetchLongest = 480;
        private const int LanceGone = 900;

        // ---- the swing ----------------------------------------------------------------

        /// <summary>In close: how fast, how hard, how long she will try, and where they have to be for the swing.</summary>
        private const float CloseSpeed = 4.6f;
        private const float CloseAccel = 0.2f;
        private const int CloseLongest = 180;
        private const float SwingBrake = 0.3f;
        private const float SwingNear = 16f;
        private const float SwingFar = 70f;
        private const float SwingHigh = 70f;

        /// <summary>Her fist over Triunfo's hooves, riding (DonaRig: the riding fist on the saddle).</summary>
        private const float FistHeight = 45f;

        /// <summary>The swing, in ticks, and where in it the lance goes up and comes down (shares of it).</summary>
        private const int SwingTicks = 84;
        private const float UpFrom = 0.18f;
        private const float UpTo = 0.36f;
        private const float DownFrom = 0.50f;
        private const float DownTo = 0.66f;

        /// <summary>A sweep still lands this far (a share of the swing) past its end, catching the last of its movement.</summary>
        private const float SwingLate = 0.02f;

        /// <summary>
        /// The swing, as the Visus's chop is written (Easing): lowered until the point is by the
        /// ground in front, up hard over her head, a hang, down harder, and back to couched.
        /// Degrees, right-facing, as the lance turns.
        /// </summary>
        private static readonly Piece[] SwingCurve =
        {
            new Piece(Ease.PolyOut, 0f, 107f, 15f, 2),
            new Piece(Ease.PolyIn, UpFrom, 122f, -157f, 2),
            new Piece(Ease.SineInOut, UpTo, -35f, -13f),
            new Piece(Ease.PolyIn, DownFrom, -48f, 168f, 3),
            new Piece(Ease.PolyOut, DownTo, 120f, -20f, 2),
        };

        /// <summary>The lance only hits on its way through the front of her: from just behind upright down to the ground.</summary>
        private static readonly float SweepFrom = MathHelper.ToRadians(-5f);
        private static readonly float SweepTo = MathHelper.ToRadians(140f);

        /// <summary>The part of the lance that hits in the swing: from this far in front of the fist to this far past the point, this thick.</summary>
        private const float SwingFrom = 8f;
        private const float SwingPast = 6f;
        private const float SwingWidth = 22f;

        /// <summary>
        /// Thrown by it, the same every time, away from her (by request): up, fifteen tiles at the
        /// top and twenty along by the time they come down to where they were (the player's 0.4
        /// gravity), tumbling; or down, flat on the ground and rolling. A second on the ground
        /// once they stop, either way.
        /// </summary>
        private static readonly Vector2 FlungUp = new Vector2(4.62f, -13.86f);
        private static readonly Vector2 FlungDown = new Vector2(3.5f, 5f);
        private const int FlungLie = 60;

        // ---- the crash -----------------------------------------------------------------

        /// <summary>Into a wall: this long before she gathers herself, Triunfo up on his hind legs for the first part of it.</summary>
        private const int CrashTicks = 54;
        private const int CrashRear = 24;

        // ---- parried -------------------------------------------------------------------

        /// <summary>Turned aside and not staggered: this long before she gathers herself. Staggered, it is the parry's own time (Parry.StaggerTicks).</summary>
        private const int TurnedTicks = 40;

        /// <summary>Staggered, Triunfo is up on his hind legs for this much of it at the start.</summary>
        private const int ReelRear = 30;

        /// <summary>
        /// Thrown back: how fast Triunfo leaves, the way he came, and the hop he leaves with.
        /// At the rate he pulls up (BrakeDecel) that speed is spent over six tiles, what a
        /// parry pushes anything back (Parry.PushPixels).
        /// </summary>
        private const float ReelPush = 5.9f;
        private const float ReelHop = 3f;

        // ============================================================
        // THE RIDING POSE: worked out on every machine, never sent
        // ============================================================

        private float lanceTurn;
        private float armTurn;
        private float headTilt;
        private float lean;

        /// <summary>The lance in her hand last tick, as drawn: it comes back pointing down, out of the ground.</summary>
        private bool hadLance = true;

        /// <summary>The swing's lance, this tick and last, worked out in the AI so the lance drawn is the lance that hits.</summary>
        private float swingNow;
        private float swingPrev;

        /// <summary>The hair's lag behind the head, in pixels, and where the nape was last tick.</summary>
        private float hairLag;
        private float lastNapeY;

        /// <summary>Ticks before she may shout again, and the last thing she shouted.</summary>
        private int barkWait;
        private int lastBark = -1;

        // ---- the server's ----------------------------------------------------------------

        /// <summary>What she did last, so nothing but a charge comes twice running; ticks since she let go of the lance.</summary>
        private Phase lastAttack = Phase.Charge;
        private int lanceGone;

        // ---- each machine's, for its own player ------------------------------------------

        /// <summary>This ram, this swing, has already landed on this machine's player.</summary>
        private bool rammed;
        private bool swung;

        /// <summary>
        /// The lance of this charge has already landed on this machine's player: it is done
        /// with them until she charges again. It neither hits them a second time nor can be
        /// parried: what has gone through them is not turned aside.
        /// </summary>
        private bool lanced;

        private static readonly string[] Barks =
        {
            "The grrreat Dame Quilt shall be victorious!",
            "Take that you simpleton!",
            "How do you like that? Also your hair is atrocious!",
            "Ouchh!",
        };

        private const int Victorious = 0;
        private const int TakeThat = 1;
        private const int YourHair = 2;
        private const int Ouch = 3;

        // ============================================================
        // RIDING
        // ============================================================

        private void Ride(TriunfoTheDonkey donkey)
        {
            NPC.noGravity = true;
            NPC.noTileCollide = true;

            PlaceOnSaddle(donkey);

            Player target = Target();
            bool lost = target == null || target.dead || Vector2.Distance(target.Center, NPC.Center) > GoneFar;

            if (Authority && !hasLance)
                lanceGone++;

            switch (State)
            {
                case Phase.Intro:
                    NPC.dontTakeDamage = true;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Timer >= IntroTimeout)
                        Go(Phase.Challenge);
                    break;

                case Phase.Challenge:
                    NPC.dontTakeDamage = true;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Timer >= ChallengeTicks)
                        Go(Phase.Charge);
                    break;

                case Phase.Charge:
                case Phase.Ram:
                    NPC.dontTakeDamage = false;

                    if (fresh)
                    {
                        rammed = false;
                        lanced = false;

                        // Committed: at them, and on past them.
                        if (Authority && target != null)
                        {
                            chargeDir = Toward(target, donkey);
                            NPC.netUpdate = true;
                        }

                        if (State == Phase.Charge)
                            BarkMaybe(0.35f, Victorious);
                    }

                    if (lost)
                    {
                        Go(Phase.Leave);
                        break;
                    }

                    RideThrough(target, donkey);
                    break;

                case Phase.Brake:
                    NPC.dontTakeDamage = false;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Math.Abs(donkey.NPC.velocity.X) < 0.25f && Timer > 4)
                    {
                        if (!hasLance)
                            Go(Phase.Fetch);
                        else if (donkey.Swimming)
                            FaceThem(target, donkey);
                        else
                            Go(Phase.Rear);
                    }
                    break;

                case Phase.Rear:
                    NPC.dontTakeDamage = false;

                    if (fresh)
                        BarkMaybe(0.3f, Victorious, TakeThat, YourHair);

                    if (lost)
                        Go(Phase.Leave);
                    else if (Timer >= RearTicks)
                        FaceThem(target, donkey);
                    break;

                case Phase.Turn:
                    NPC.dontTakeDamage = false;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Timer >= TurnTicks)
                        Go(Phase.Ready);
                    break;

                case Phase.Ready:
                    NPC.dontTakeDamage = false;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Timer >= ReadyTicks && Authority)
                        Go(NextAttack(target, donkey));
                    break;

                case Phase.Wind:
                    NPC.dontTakeDamage = false;

                    if (fresh && Authority && target != null)
                    {
                        chargeDir = Toward(target, donkey);
                        NPC.netUpdate = true;
                    }

                    if (lost)
                    {
                        Go(Phase.Leave);
                        break;
                    }

                    if (Timer == ReleaseAt && hasLance)
                        Release(target, donkey);

                    if (Timer >= WindTicks)
                        Go(Phase.Ram);
                    break;

                case Phase.Fetch:
                    NPC.dontTakeDamage = false;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Authority)
                        Fetch(donkey);
                    break;

                case Phase.Close:
                    NPC.dontTakeDamage = false;

                    // The tell, from the moment she turns to come in for it.
                    NPC.PAD().CCBypass = true;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Authority)
                        Close(target, donkey);
                    break;

                case Phase.Swing:
                    NPC.dontTakeDamage = false;

                    if (fresh)
                    {
                        swung = false;
                        swingNow = SwingAngle(0f);
                    }

                    swingPrev = swingNow;
                    swingNow = SwingAngle(Timer / (float)SwingTicks);

                    // Up to the last tick the downswing can still land.
                    if (Timer / (float)SwingTicks <= DownTo + SwingLate)
                        NPC.PAD().CCBypass = true;

                    SwingSounds(donkey);

                    if (lost)
                        Go(Phase.Leave);
                    else if (Timer >= SwingTicks)
                        FaceThem(target, donkey);
                    break;

                case Phase.Range:
                    NPC.dontTakeDamage = false;

                    if (lost)
                        Go(Phase.Leave);
                    else if (Authority)
                        InRange(target, donkey);
                    break;

                case Phase.Crash:
                    NPC.dontTakeDamage = false;

                    if (fresh)
                        Rebound(donkey);

                    if (lost)
                        Go(Phase.Leave);
                    else if (Timer >= CrashTicks)
                        FaceThem(target, donkey);
                    break;

                case Phase.Reel:
                    NPC.dontTakeDamage = false;

                    if (fresh)
                        Recoil(donkey);

                    // Staggered, the first hit on her or on him wakes her (PADStaggerNPC):
                    // she is no longer reeling, and she gathers herself at once.
                    if (lost)
                        Go(Phase.Leave);
                    else if (Authority && (Timer >= (reelDeep ? Parry.StaggerTicks : TurnedTicks)
                        || (reelDeep && Timer > 1 && !NPC.GetGlobalNPC<PADStaggerNPC>().Reeling)))
                        FaceThem(target, donkey);
                    break;

                case Phase.Buck:
                    NPC.dontTakeDamage = true;
                    NPC.life = 1;

                    if (Timer >= BuckThrowAt)
                        Go(Phase.Thrown);
                    break;

                case Phase.Leave:
                    NPC.dontTakeDamage = true;

                    if (fresh)
                    {
                        if (Authority)
                        {
                            // Away from them, or back toward the Mile if there is no them.
                            chargeDir = target != null
                                ? -Math.Sign(target.Center.X - donkey.NPC.Center.X)
                                : Structures.WorldMirror.Dir;

                            if (chargeDir == 0)
                                chargeDir = -donkey.Facing;

                            NPC.netUpdate = true;
                        }

                        // They fell to her. She says so.
                        if (target != null && target.dead && !Main.dedServ)
                            Shout(Victorious);
                    }

                    if (Authority && ((Timer >= LeaveFirst && !Watched(NPC.Center)) || Timer >= LeaveLongest))
                        Vanish();
                    break;
            }

            LanceStrike(donkey);
            SwingStrike(donkey);
            RamStrike(donkey);
        }

        /// <summary>
        /// A charge or a ram, the server's say: into a wall is a crash; on past where the player
        /// is now (not where they were, so one running on ahead is not let off) is the end of it;
        /// so is the longest a charge goes, or Triunfo stopped and getting no further.
        /// </summary>
        private void RideThrough(Player target, TriunfoTheDonkey donkey)
        {
            if (!Authority)
                return;

            // Only a charge goes into a wall: Triunfo running them down without her lance is
            // careful, and pulls up at one.
            if (State == Phase.Charge && donkey.Crashed)
            {
                Crash();
                return;
            }

            passX = target.Center.X + chargeDir * PassBeyond;
            bool past = (donkey.NPC.Center.X - passX) * chargeDir >= 0f;

            if (past || Timer >= ChargeLongest || donkey.Stuck > GivesUpAfter)
                Go(Phase.Brake);
        }

        /// <summary>Round to face them if they are behind her (Turn), or straight on (Ready). The server's.</summary>
        private void FaceThem(Player target, TriunfoTheDonkey donkey)
        {
            if (!Authority)
                return;

            int toward = target != null ? Math.Sign(target.Center.X - donkey.NPC.Center.X) : 0;

            if (toward == 0)
                toward = donkey.Facing;

            chargeDir = toward;
            Go(toward != donkey.Facing ? Phase.Turn : Phase.Ready);
        }

        /// <summary>Which way they are from him; his own way if they are right on top of him.</summary>
        private static int Toward(Player target, TriunfoTheDonkey donkey)
        {
            float dx = target.Center.X - donkey.NPC.Center.X;
            return Math.Abs(dx) < 8f ? donkey.Facing : Math.Sign(dx);
        }

        /// <summary>
        /// What next, the server's pick. Without the lance, back for it. Where Triunfo's legs will
        /// not take her to them (a cliff, a wall too high, a lake between, up on something out of
        /// his reach), the throw if they are in range; if not, to somewhere she can throw from;
        /// if there is no throwing at them at all (too far above or below), she rears and shouts
        /// and waits for them. Otherwise nothing but a charge comes twice running: after a charge,
        /// now and then the swing (if they are not above or below the lance), now and then the
        /// throw.
        /// </summary>
        private Phase NextAttack(Player target, TriunfoTheDonkey donkey)
        {
            if (!hasLance)
                return Phase.Fetch;

            if (target == null)
                return Phase.Charge;

            float dist = Math.Abs(target.Center.X - donkey.NPC.Center.X);
            bool level = Math.Abs(target.Center.Y - NPC.Center.Y) <= ThrowHighest;
            bool canThrow = level && dist >= ThrowNearest && dist <= ThrowFurthest;
            bool canReach = DonkeyNav.Reachable(donkey.Body, target.Center.X, target.Bottom.Y, TriunfoTheDonkey.TopSpeed);
            bool canSwing = Math.Abs(target.Center.Y - (donkey.NPC.Bottom.Y - FistHeight)) <= SwingHigh;

            Phase pick;

            if (!canReach)
            {
                pick = canThrow ? Phase.Wind : level ? Phase.Range : Phase.Rear;
            }
            else if (lastAttack != Phase.Charge)
            {
                pick = Phase.Charge;
            }
            else
            {
                float roll = Main.rand.NextFloat();
                pick = roll < 0.35f && canSwing ? Phase.Close : roll < 0.65f && canThrow ? Phase.Wind : Phase.Charge;
            }

            lastAttack = pick;
            return pick;
        }

        /// <summary>To a throw's distance from a player she cannot get to, the server's say: there, or as near as Triunfo gets in the time, she turns to them.</summary>
        private void InRange(Player target, TriunfoTheDonkey donkey)
        {
            float dist = Math.Abs(target.Center.X - donkey.NPC.Center.X);
            bool there = dist >= ThrowNearest + 40f && dist <= ThrowFurthest - 80f;

            if (there || Timer >= RangeLongest || donkey.Stuck > GivesUpAfter)
                FaceThem(target, donkey);
        }

        /// <summary>
        /// Her hitbox on her seat, and her speed his. The game moves her by her velocity after
        /// this, and him by his after his own AI, so if he has already gone this tick she is
        /// put where he was and moved on by the same amount: either way the two end the tick
        /// together.
        /// </summary>
        private void PlaceOnSaddle(TriunfoTheDonkey donkey)
        {
            Vector2 hooves = new Vector2(donkey.NPC.Center.X, donkey.NPC.Bottom.Y);

            if (donkey.NPC.whoAmI < NPC.whoAmI)
                hooves -= donkey.NPC.velocity;

            DonaRig.Pose pose = DonaRig.Riding(hooves, donkey.Facing, donkey.Picture, lean, armTurn, lanceTurn, headTilt);

            Resize(24, 40);
            NPC.Center = pose.MiddleAt;
            NPC.velocity = donkey.NPC.velocity;
            NPC.direction = donkey.Facing;
            NPC.spriteDirection = donkey.Facing;
            facing = donkey.Facing;
        }

        /// <summary>Her hitbox to this size, her feet where they were.</summary>
        private void Resize(int width, int height)
        {
            if (NPC.width == width && NPC.height == height)
                return;

            Vector2 bottom = NPC.Bottom;
            NPC.width = width;
            NPC.height = height;
            NPC.Bottom = bottom;
        }

        /// <summary>What she asks of Triunfo this tick. Read by him, on every machine, from what is synced.</summary>
        internal Reins ReinsFor(TriunfoTheDonkey donkey)
        {
            Reins reins = new Reins
            {
                Accel = TriunfoTheDonkey.Acceleration,
                MayJump = true,
                Careful = true,
                MaySwim = true,
            };

            Player target = Target();
            int toward = target != null && Math.Abs(target.Center.X - donkey.NPC.Center.X) > 8f
                ? Math.Sign(target.Center.X - donkey.NPC.Center.X)
                : 0;

            switch (State)
            {
                case Phase.Intro:
                    reins.Speed = 0f;
                    reins.Accel = 0.2f;
                    reins.Facing = toward;
                    reins.Rear = Timer >= RevealRearAt && Timer < RevealRearAt + RevealRearTicks;
                    break;

                case Phase.Challenge:
                    reins.Speed = 0f;
                    reins.Accel = 0.2f;
                    reins.Facing = toward;
                    break;

                case Phase.Charge:
                    // Flat out, and not careful: a wall he cannot jump, he goes into.
                    reins.Speed = chargeDir * TriunfoTheDonkey.TopSpeed;
                    reins.Facing = chargeDir;
                    reins.Careful = false;
                    reins.MaySwim = false;
                    break;

                case Phase.Ram:
                    // Flat out without her, and he pulls up at a wall.
                    reins.Speed = chargeDir * TriunfoTheDonkey.TopSpeed;
                    reins.Facing = chargeDir;
                    reins.MaySwim = false;
                    break;

                case Phase.Range:
                {
                    // To a throw's distance from them, on her side of them.
                    float dx = 0f;

                    if (target != null)
                    {
                        float side = donkey.NPC.Center.X >= target.Center.X ? 1f : -1f;
                        dx = target.Center.X + side * ThrowSpot - donkey.NPC.Center.X;
                    }

                    int way = Math.Abs(dx) > 8f ? Math.Sign(dx) : 0;

                    reins.Speed = way * Math.Min(FetchSpeed, 1f + (float)Math.Sqrt(2f * FetchAccel * Math.Abs(dx)));
                    reins.Accel = FetchAccel;
                    reins.Facing = way;
                    reins.MaySwim = false;
                    break;
                }

                case Phase.Brake:
                case Phase.Ready:
                    reins.Speed = 0f;
                    reins.Accel = BrakeDecel;
                    break;

                case Phase.Swing:
                    // Pulled up hard, so the lance comes round in front of them and not over them.
                    reins.Speed = 0f;
                    reins.Accel = SwingBrake;
                    break;

                case Phase.Rear:
                    reins.Speed = 0f;
                    reins.Accel = BrakeDecel;
                    reins.Rear = true;
                    break;

                case Phase.Turn:
                case Phase.Wind:
                    reins.Speed = 0f;
                    reins.Accel = BrakeDecel;
                    reins.Facing = chargeDir;
                    break;

                case Phase.Fetch:
                {
                    // At the lance, slowing as he gets to it so she can reach down for it.
                    float dx = lanceX - donkey.NPC.Center.X;
                    int way = Math.Abs(dx) > 4f ? Math.Sign(dx) : 0;

                    reins.Speed = way * Math.Min(FetchSpeed, 1f + (float)Math.Sqrt(2f * FetchAccel * Math.Abs(dx)));
                    reins.Accel = FetchAccel;
                    reins.Facing = way;
                    break;
                }

                case Phase.Close:
                {
                    // Up to them, slowing to stop with them at the end of the lance.
                    float dx = target != null ? target.Center.X - donkey.NPC.Center.X : 0f;
                    float room = Math.Max(0f, Math.Abs(dx) - SwingFar * 0.55f);

                    reins.Speed = Math.Sign(dx) * Math.Min(CloseSpeed, (float)Math.Sqrt(2f * CloseAccel * room));
                    reins.Accel = CloseAccel;
                    reins.Facing = chargeDir;
                    break;
                }

                case Phase.Crash:
                    reins.Speed = 0f;
                    reins.Accel = BrakeDecel;
                    reins.Rear = Timer < CrashRear;
                    break;

                case Phase.Reel:
                    // Thrown back with what the parry gave him (Recoil), pulling up; staggered, up on his hind legs first.
                    reins.Speed = 0f;
                    reins.Accel = BrakeDecel;
                    reins.Rear = reelDeep && Timer < ReelRear;
                    break;

                case Phase.Buck:
                    reins.Speed = 0f;
                    reins.Accel = BrakeDecel;
                    reins.Buck = true;
                    break;

                case Phase.Leave:
                    reins.Speed = chargeDir * TriunfoTheDonkey.TopSpeed;
                    reins.Facing = chargeDir;
                    break;
            }

            return reins;
        }

        // ============================================================
        // THE LANCE IN A CHARGE
        // ============================================================

        /// <summary>
        /// The point of the lance against this machine's own player, as anything that hurts by
        /// touch is checked: each machine for its own player, sent on from there by Hurt. Only
        /// held forward and moving; the shaft from just past her fist to a little past the point,
        /// thick. Thirty at the slowest a strike counts and sixty-seven flat out, by how fast
        /// Triunfo is going, through armour, varied the way vanilla varies any hit, not grown by
        /// the world's difficulty. Once a charge: having landed, it is done with them (lanced).
        /// </summary>
        private void LanceStrike(TriunfoTheDonkey donkey)
        {
            if (Main.dedServ || State != Phase.Charge || !hasLance || lanced || donkey.Backing)
                return;

            // Forward: backing off for a run at a bank is not a charge.
            float speed = donkey.NPC.velocity.X * donkey.Facing;

            if (speed < StrikeSpeed || lanceTurn < CouchedFrom || lanceTurn > CouchedTo)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            DonaRig.Pose pose = RidingPose(donkey);
            Vector2 along = (pose.Tip - pose.FistAt).SafeNormalize(Vector2.UnitX);
            Vector2 from = pose.FistAt + along * StrikeFrom;
            Vector2 to = pose.Tip + along * StrikePast;

            // The parry first: the lance met at their arc is turned aside and she with it
            // (Turned); and for the ticks after one, it does not touch them.
            ParryPlayer parry = me.GetModPlayer<ParryPlayer>();

            if (parry.Spares(NPC) || parry.MeetsLine(NPC, from, to, StrikeWidth))
                return;

            if (me.immune)
                return;

            float point = 0f;

            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, from, to, StrikeWidth, ref point))
                return;

            float hard = MathHelper.Clamp((speed - StrikeSpeed) / (TriunfoTheDonkey.TopSpeed - StrikeSpeed), 0f, 1f);
            int damage = Main.DamageVar(MathHelper.Lerp(LanceLeast, LanceMost, hard), -me.luck);

            if (!Pierce(me, "DonaLance", damage, chargeDir, StrikeKnockback))
                return;

            // Landed: this charge's lance is through with them. It is long and she rides on
            // past, so it is still across their arc for some ticks after it hits; without
            // this a parry pressed after the hit would turn aside what had already gone
            // through them.
            lanced = true;

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f, Volume = 0.9f }, pose.Tip);
            me.PIS().BumpScreenShake(4f + 3f * hard, 10);

            BarkMaybe(0.7f, TakeThat, YourHair);
        }

        /// <summary>A hit on this machine's player that goes through their armour (Player.Hurt with all of their defence ignored). True if it landed.</summary>
        private static bool Pierce(Player me, string death, int damage, int direction, float knockback)
        {
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText." + death, me.name));

            return me.Hurt(reason, damage, direction, false, false, ImmunityCooldownID.General, true, 0f, 1f, knockback) > 0;
        }

        /// <summary>The lance aimed at them as she comes: from her fist to the middle of them, if they are in front of her and near enough to aim at.</summary>
        private float Aim(TriunfoTheDonkey donkey)
        {
            Player target = Target();

            if (target == null || target.dead)
                return Couched;

            DonaRig.Pose pose = RidingPose(donkey);
            Vector2 d = target.Center - pose.FistAt;
            float ahead = d.X * donkey.Facing;

            if (ahead < 8f || ahead > 480f)
                return Couched;

            float angle = (float)Math.Atan2(ahead, -d.Y);
            return MathHelper.Clamp(angle - lean, AimHigh, AimLow);
        }

        /// <summary>Her riding pose as it is drawn right now: on Triunfo's hooves as he is drawn.</summary>
        private DonaRig.Pose RidingPose(TriunfoTheDonkey donkey)
        {
            return DonaRig.Riding(donkey.HoovesAt, donkey.Facing, donkey.Picture, lean, armTurn, lanceTurn, headTilt);
        }

        // ============================================================
        // THE THROW, THE RAM, THE FETCH
        // ============================================================

        /// <summary>The thrown lance, if it is anywhere: hers by her index (ai[0]), on every machine.</summary>
        private Projectile ThrownLance()
        {
            int type = ModContent.ProjectileType<DonaLanceThrown>();

            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.type == type && (int)projectile.ai[0] == NPC.whoAmI)
                    return projectile;
            }

            return null;
        }

        /// <summary>Whether a thrown lance of hers still has anyone coming back for it: she is riding, in the fight, without it.</summary>
        public bool WantsLance => Mounted && State < Phase.Buck && !hasLance;

        /// <summary>The thrown lance is in the ground, here. The server's word, sent.</summary>
        internal void LanceDown(float x)
        {
            lanceX = x;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// The lance goes, on every machine at the same tick: out of her hand, with a grunt of
        /// effort. The server throws it (DonaLanceThrown), from where its point is in her hand,
        /// on the arc that brings it down on them.
        /// </summary>
        private void Release(Player target, TriunfoTheDonkey donkey)
        {
            DonaRig.Pose pose = RidingPose(donkey);
            hasLance = false;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.6f, Volume = 1f }, pose.FistAt);
                SoundEngine.PlaySound(SoundID.FemaleHit with { Pitch = 0.4f, Volume = 0.6f }, pose.FistAt);
            }

            if (!Authority || target == null)
                return;

            Vector2 velocity = DonaLanceThrown.Aim(pose.Tip, target);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), pose.Tip, velocity, ModContent.ProjectileType<DonaLanceThrown>(),
                0, 0f, Main.myPlayer, NPC.whoAmI);

            lanceX = target.Center.X;
            lanceGone = 0;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// Back for the lance, the server's say: out of the ground as she goes by it. If she cannot
        /// get to it (Triunfo stopped short of a cliff, the lake too deep to get out of), or it is
        /// gone, or it has been too long, she has it again regardless.
        /// </summary>
        private void Fetch(TriunfoTheDonkey donkey)
        {
            Projectile lance = ThrownLance();
            bool planted = lance != null && lance.ai[1] == 1f;

            if (lance != null && Math.Abs(lance.Center.X - lanceX) > 16f)
            {
                lanceX = lance.Center.X;
                NPC.netUpdate = true;
            }

            if (planted && Math.Abs(donkey.NPC.Center.X - lance.Center.X) <= PluckReach
                && Math.Abs(donkey.NPC.Bottom.Y - lance.Center.Y) <= 96f)
            {
                Pluck(lance);
                return;
            }

            if (lance == null || Timer >= FetchLongest || lanceGone >= LanceGone || donkey.Stuck > GivesUpAfter * 4)
                Pluck(lance);
        }

        /// <summary>In her hand again: the one in the ground gone, every machine told, and she pulls up.</summary>
        private void Pluck(Projectile lance)
        {
            hasLance = true;
            lanceGone = 0;
            lance?.Kill();

            NPC.netUpdate = true;
            Go(Phase.Brake);
        }

        /// <summary>
        /// Triunfo against this machine's own player, while he runs them down: his body and the
        /// head out in front of it, at speed. Fifteen at the slowest and forty flat out, an
        /// ordinary hit (their armour counts), once a ram.
        /// </summary>
        private void RamStrike(TriunfoTheDonkey donkey)
        {
            if (Main.dedServ || State != Phase.Ram || rammed || donkey.Backing)
                return;

            float speed = donkey.NPC.velocity.X * donkey.Facing;

            if (speed < RamFrom)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            Rectangle body = donkey.NPC.Hitbox;

            if (donkey.Facing < 0)
                body.X -= 12;

            body.Width += 12;

            // The parry first, as with the lance: Triunfo met at their arc is turned aside.
            ParryPlayer parry = me.GetModPlayer<ParryPlayer>();

            if (parry.Spares(NPC) || parry.MeetsBox(NPC, body, donkey.NPC.velocity))
                return;

            if (me.immune || !body.Intersects(me.Hitbox))
                return;

            float hard = MathHelper.Clamp((speed - RamFrom) / (TriunfoTheDonkey.TopSpeed - RamFrom), 0f, 1f);
            int damage = Main.DamageVar(MathHelper.Lerp(RamLeast, RamMost, hard), -me.luck);

            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText.DonaTrampled", me.name));

            if (me.Hurt(reason, damage, donkey.Facing, false, false, ImmunityCooldownID.General, true, 0f, 0f, RamKnockback) <= 0)
                return;

            rammed = true;

            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.5f, Volume = 1f }, me.Center);
            me.PIS().BumpScreenShake(3f + 3f * hard, 10);
        }

        // ============================================================
        // THE SWING
        // ============================================================

        /// <summary>
        /// In close, the server's say: round to them if they get behind her, and the swing once
        /// they are at the end of the lance, in front of her, not far over or under her fist.
        /// Given up (and on to the next thing) if it takes too long or Triunfo cannot get there.
        /// </summary>
        private void Close(Player target, TriunfoTheDonkey donkey)
        {
            int toward = Toward(target, donkey);

            if (toward != chargeDir)
            {
                chargeDir = toward;
                NPC.netUpdate = true;
            }

            DonaRig.Pose pose = RidingPose(donkey);
            float ahead = (target.Center.X - donkey.NPC.Center.X) * donkey.Facing;

            if (donkey.Facing == chargeDir && ahead >= SwingNear && ahead <= SwingFar && Math.Abs(target.Center.Y - pose.FistAt.Y) <= SwingHigh)
                Go(Phase.Swing);
            else if (Timer >= CloseLongest || donkey.Stuck > GivesUpAfter)
                Go(Phase.Ready);
        }

        /// <summary>The lance's turn at <paramref name="progress"/> through the swing.</summary>
        private static float SwingAngle(float progress)
        {
            return MathHelper.ToRadians(Easing.At(progress, SwingCurve));
        }

        /// <summary>Her arm with the lance: up over her head with it, down after it.</summary>
        private static float SwingArm(float lance)
        {
            return MathHelper.Clamp((lance - MathHelper.PiOver2) * 0.45f, -0.9f, 0.35f);
        }

        /// <summary>Back as it goes up over her, into it as it comes down.</summary>
        private static float SwingLean(float lance)
        {
            return 0.12f * (float)Math.Sin(lance - MathHelper.PiOver2);
        }

        /// <summary>The two rushes of air, as the lance starts up and as it starts down.</summary>
        private void SwingSounds(TriunfoTheDonkey donkey)
        {
            if (Main.dedServ)
                return;

            if (Timer == (int)(UpFrom * SwingTicks) || Timer == (int)(DownFrom * SwingTicks))
            {
                bool down = Timer >= DownFrom * SwingTicks;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = down ? -0.25f : -0.55f, Volume = 1f }, RidingPose(donkey).FistAt);
            }
        }

        /// <summary>
        /// The swing against this machine's own player: only while the lance goes up or comes
        /// down through the front of her, checked at every few degrees of the way it moved this
        /// tick so a fast lance cannot pass over anyone between two ticks. Once a swing.
        /// </summary>
        private void SwingStrike(TriunfoTheDonkey donkey)
        {
            if (Main.dedServ || State != Phase.Swing || swung || !hasLance)
                return;

            float p = Timer / (float)SwingTicks;
            bool up = swingNow < swingPrev && p >= UpFrom && p <= UpTo + SwingLate;
            bool down = swingNow > swingPrev && p >= DownFrom && p <= DownTo + SwingLate;

            if (!up && !down)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead || me.immune)
                return;

            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(swingNow - swingPrev) / MathHelper.ToRadians(4f)));

            for (int i = 1; i <= steps; i++)
            {
                float lance = MathHelper.Lerp(swingPrev, swingNow, i / (float)steps);

                if (lance < SweepFrom || lance > SweepTo)
                    continue;

                DonaRig.Pose pose = DonaRig.Riding(donkey.HoovesAt, donkey.Facing, donkey.Picture, lean, SwingArm(lance), lance, headTilt);
                Vector2 along = (pose.Tip - pose.FistAt).SafeNormalize(Vector2.UnitY);
                float point = 0f;

                if (!Collision.CheckAABBvLineCollision(me.position, me.Size, pose.FistAt + along * SwingFrom, pose.Tip + along * SwingPast, SwingWidth, ref point))
                    continue;

                Swept(me, up);
                return;
            }
        }

        /// <summary>
        /// Hit by the swing: forty through armour, and, if it landed, thrown the way the lance was
        /// going (RagdollPlayer: straight onto their velocity, so no shield turns it).
        /// </summary>
        private void Swept(Player me, bool up)
        {
            swung = true;

            int damage = Main.DamageVar(SwingDamage, -me.luck);

            // A swing that killed them throws nothing.
            if (!Pierce(me, "DonaSwept", damage, facing, 0f) || me.dead)
                return;

            Vector2 fling = up ? new Vector2(facing * FlungUp.X, FlungUp.Y) : new Vector2(facing * FlungDown.X, FlungDown.Y);
            me.GetModPlayer<RagdollPlayer>().Throw(fling, up ? RagdollStyle.Spin : RagdollStyle.Roll, FlungLie);

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.8f, Volume = 1f }, me.Center);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.4f, Volume = 0.8f }, me.Center);
            me.PIS().BumpScreenShake(up ? 6f : 8f, 14);

            BarkMaybe(0.6f, TakeThat, YourHair);
        }

        // ============================================================
        // THE CRASH
        // ============================================================

        /// <summary>
        /// Into a wall, the server's: a hundred off her, flat, struck on her the game's own way
        /// (her defence does not come into it) and everyone told. Into the crash first, so that if
        /// it leaves her on nothing, CheckDead's buck is what she is in.
        /// </summary>
        private void Crash()
        {
            if (!Authority)
                return;

            Go(Phase.Crash);

            NPC.HitInfo hit = new NPC.HitInfo
            {
                Damage = CrashDamage,
                HitDirection = -chargeDir,
                Knockback = 0f,
                DamageType = DamageClass.Default,
            };

            NPC.StrikeNPC(hit, false, true);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendStrikeNPC(NPC, hit);
        }

        /// <summary>The crash as it is seen, on every machine: Triunfo off the wall, her head thrown back, dirt, the thump, and "Ouchh!".</summary>
        private void Rebound(TriunfoTheDonkey donkey)
        {
            donkey.NPC.velocity = new Vector2(-chargeDir * 2.6f, -3f);

            if (Authority)
                donkey.NPC.netUpdate = true;

            headTilt = -0.45f;
            lean = -0.22f;

            if (Main.dedServ)
                return;

            Shout(Ouch);

            Vector2 wall = new Vector2(chargeDir > 0 ? donkey.NPC.Right.X : donkey.NPC.Left.X, donkey.NPC.Center.Y);

            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.8f, Volume = 1f }, wall);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f, Volume = 0.8f }, wall);

            for (int i = 0; i < 18; i++)
            {
                Dust dust = Dust.NewDustDirect(wall - new Vector2(6f, 24f), 12, 48, DustID.Dirt,
                    -chargeDir * Main.rand.NextFloat(0.5f, 3f), Main.rand.NextFloat(-3f, 0.5f), 60, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = false;
            }

            if (Vector2.Distance(Main.LocalPlayer.Center, wall) < 1000f)
                Main.LocalPlayer.PIS().BumpScreenShake(7f, 16);
        }

        // ============================================================
        // PARRIED
        // ============================================================

        /// <summary>
        /// Whether a parry turns her aside as she is: in the charge, with Triunfo running them
        /// down, or pulling up from either (a parry's word takes a tick or two to get to the
        /// server, and she may be past them by then). Never the swing, which wears the star.
        /// </summary>
        public bool CanBeTurned => Mounted && !NPC.dontTakeDamage
            && (State == Phase.Charge || State == Phase.Ram || State == Phase.Brake);

        /// <summary>
        /// Parried, the server's: thrown back (Reel); and staggered, if this is the parry that
        /// does it (every third, for her as for anything on the ground), which
        /// leaves the next hit on her a riposte. She has her own state for it, so her AI is not
        /// stopped: only open (PADStaggerNPC.Reel, not held). The parry's blow is not here: it
        /// is a hit of the player's, landed on their machine before this.
        /// </summary>
        private void Turned(bool staggers)
        {
            if (!Authority || !CanBeTurned)
                return;

            reelDeep = staggers;
            Go(Phase.Reel);

            if (staggers)
                NPC.GetGlobalNPC<PADStaggerNPC>().Reel(NPC, Parry.StaggerTicks, false);
        }

        /// <summary>
        /// Thrown back, as it is seen, on every machine: Triunfo off the way he came with a
        /// hop, pulling up as he goes; her head thrown back; the dirt his hooves take with
        /// them, and the thump.
        /// </summary>
        private void Recoil(TriunfoTheDonkey donkey)
        {
            donkey.NPC.velocity = new Vector2(-chargeDir * ReelPush, -ReelHop);

            if (Authority)
                donkey.NPC.netUpdate = true;

            headTilt = -0.45f;
            lean = -0.22f;

            if (Main.dedServ)
                return;

            Vector2 hooves = new Vector2(donkey.NPC.Center.X, donkey.NPC.Bottom.Y);

            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.6f, Volume = 0.8f }, hooves);

            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustDirect(hooves - new Vector2(28f, 6f), 56, 8, DustID.Dirt,
                    chargeDir * Main.rand.NextFloat(0.5f, 2.5f), Main.rand.NextFloat(-2.5f, -0.3f), 60, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = false;
            }
        }

        // ============================================================
        // HER VOICE
        // ============================================================

        /// <summary>Now and then, one of these, at the foot of the screen (Taunt); not if she has just said something.</summary>
        private void BarkMaybe(float chance, params int[] lines)
        {
            if (Main.dedServ || barkWait > 0 || lines.Length == 0 || Main.rand.NextFloat() >= chance)
                return;

            int pick = lines[Main.rand.Next(lines.Length)];

            // Not the same thing twice running, if there is anything else to say.
            if (pick == lastBark && lines.Length > 1)
                pick = lines[(Array.IndexOf(lines, pick) + 1 + Main.rand.Next(lines.Length - 1)) % lines.Length];

            Shout(pick);
        }

        /// <summary>
        /// This one, now: a line at the foot of the screen, underlined in her gold, gone by itself (Taunt, by request:
        /// "Fix Dame Quilt to have this kinda trash talk system"), for whoever is near enough to hear her.
        /// </summary>
        private void Shout(int line)
        {
            if (Main.dedServ)
                return;

            Taunt.Say(NPC, Barks[line], VoiceGold, VoiceGoldFar, VoiceReach);
            lastBark = line;
            barkWait = 300;
        }

        // ============================================================
        // THE RIDING POSE, EASED
        // ============================================================

        /// <summary>
        /// What she is doing with herself in the saddle: the lance upright at rest and on the
        /// road, waved about on the rear; aimed at them in a charge; drawn back and thrown; swung
        /// up and down (set outright, the lance drawn being the one that hits); the hand reaching
        /// down for it as she rides back to it; a lean into the gallop; the head turning a little
        /// toward the player, on her own side only, like Mors's; and the hair a beat behind the
        /// head.
        /// </summary>
        private void RidingVisuals(TriunfoTheDonkey donkey)
        {
            float t = Main.GlobalTimeWrappedHourly * 60f;
            float speed = Math.Abs(donkey.NPC.velocity.X) / TriunfoTheDonkey.TopSpeed;

            // Back in her hand, it comes up pointing down, out of the ground.
            if (hasLance && !hadLance)
                lanceTurn = MathHelper.Pi - 0.35f;

            hadLance = hasLance;

            float lanceWant = 0.04f * (float)Math.Sin(t * 0.05f);
            float armWant = 0f;
            float leanWant = 0f;
            float lanceRate = 0.12f;
            float armRate = 0.15f;
            bool set = false;

            bool rearing = DonaRig.Reared(donkey.Picture);

            switch (State)
            {
                case Phase.Charge:
                    lanceWant = Aim(donkey) + 0.03f * (float)Math.Sin(t * 0.4f);
                    armWant = 0.1f;
                    leanWant = 0.1f * speed;
                    lanceRate = 0.1f;
                    break;

                case Phase.Ram:
                    // The reins hand out ahead, leaning into it.
                    armWant = 0.3f + 0.05f * (float)Math.Sin(t * 0.5f);
                    leanWant = 0.14f * speed;
                    break;

                case Phase.Fetch:
                {
                    float near = 1f - MathHelper.Clamp(Math.Abs(lanceX - donkey.NPC.Center.X) / 160f, 0f, 1f);
                    armWant = 0.2f + 0.55f * near;
                    leanWant = 0.08f + 0.12f * near;
                    break;
                }

                case Phase.Close:
                    lanceWant = MathHelper.PiOver2 + 0.3f;
                    armWant = 0.12f;
                    leanWant = 0.06f * speed;
                    break;

                case Phase.Swing:
                    lanceTurn = swingNow;
                    armTurn = SwingArm(swingNow);
                    leanWant = SwingLean(swingNow);
                    set = true;
                    break;

                case Phase.Wind:
                    WindPose(donkey, out lanceWant, out armWant, out lanceRate);
                    armRate = lanceRate;
                    leanWant = -0.06f;
                    break;

                case Phase.Crash:
                    lanceWant = -0.5f + 0.4f * (float)Math.Sin(t * 0.6f);
                    armWant = -0.3f + 0.3f * (float)Math.Sin(t * 0.8f + 1f);
                    leanWant = -0.12f;
                    lanceRate = 0.2f;
                    break;

                case Phase.Reel:
                    if (reelDeep && !rearing)
                    {
                        // Staggered, once he is down again: slumped over his neck, the lance drooping.
                        lanceWant = MathHelper.PiOver2 + 0.6f;
                        armWant = 0.45f;
                        leanWant = 0.16f;
                        lanceRate = 0.08f;
                    }
                    else
                    {
                        // The lance knocked up out of line and waving, as it is off a wall.
                        lanceWant = -0.5f + 0.4f * (float)Math.Sin(t * 0.6f);
                        armWant = -0.3f + 0.3f * (float)Math.Sin(t * 0.8f + 1f);
                        leanWant = -0.12f;
                        lanceRate = 0.2f;
                    }
                    break;

                case Phase.Brake:
                    lanceWant = 0.15f;
                    leanWant = -0.08f;
                    break;

                case Phase.Buck:
                    lanceWant = -0.6f + 0.5f * (float)Math.Sin(t * 0.9f);
                    armWant = -0.5f + 0.4f * (float)Math.Sin(t * 1.1f + 1f);
                    leanWant = 0.15f * (float)Math.Sin(t * 1.3f);
                    lanceRate = 0.3f;
                    break;

                case Phase.Leave:
                    lanceWant = 0.1f;
                    leanWant = 0.08f * speed;
                    break;

                default:
                    if (rearing)
                    {
                        // Waved over her head, the whole time he is up.
                        lanceWant = -0.4f + 0.2f * (float)Math.Sin(t * 0.35f);
                        armWant = -0.35f;
                    }
                    break;
            }

            if (!set)
            {
                lanceTurn += (lanceWant - lanceTurn) * lanceRate;
                armTurn += (armWant - armTurn) * armRate;
            }

            lean += (leanWant - lean) * 0.12f;

            // The head. On her own side only: she never looks round behind her in the
            // saddle; to look the other way the whole of her and Triunfo turns.
            float tiltWant = 0f;
            Player target = Target();
            DonaRig.Pose pose = RidingPose(donkey);

            if (rearing)
            {
                tiltWant = -0.2f;
            }
            else if (target != null && !target.dead)
            {
                Vector2 d = target.Center - pose.NapeAt;

                if (Math.Sign(d.X) == donkey.Facing && d.Length() < 900f)
                    tiltWant = MathHelper.Clamp((float)Math.Atan2(d.Y, Math.Abs(d.X)), -0.35f, 0.25f);
            }

            if (State == Phase.Buck)
                tiltWant = 0.3f * (float)Math.Sin(t * 1.7f);

            // Staggered and down off his hind legs: her head hangs.
            if (State == Phase.Reel && reelDeep && !rearing)
                tiltWant = 0.25f;

            headTilt += (tiltWant - headTilt) * (State == Phase.Buck ? 0.3f : 0.07f);

            // The hair a beat behind: it lifts a little when the head drops and sinks when it rises.
            float napeY = pose.NapeAt.Y;
            float rise = lastNapeY == 0f ? 0f : napeY - lastNapeY;
            lastNapeY = napeY;
            hairLag += (MathHelper.Clamp(-rise * 0.7f, -2f, 2f) - hairLag) * 0.35f;
        }

        /// <summary>
        /// The throw, by the clock: drawn back over her shoulder with the arm cocked, then the arm
        /// comes over and the lance with it, pointing the way it will leave; after it goes, the
        /// hand follows through.
        /// </summary>
        private void WindPose(TriunfoTheDonkey donkey, out float lance, out float arm, out float rate)
        {
            float launch = LaunchAngle(donkey);

            if (Timer < ReleaseAt - 8)
            {
                lance = launch - 1f;
                arm = -1.1f;
                rate = 0.14f;
            }
            else if (Timer <= ReleaseAt)
            {
                lance = launch;
                arm = 0.35f;
                rate = 0.45f;
            }
            else
            {
                lance = launch;
                arm = 0.55f;
                rate = 0.2f;
            }
        }

        /// <summary>The way the lance will leave her hand, right-facing: along the arc that brings it down on them.</summary>
        private float LaunchAngle(TriunfoTheDonkey donkey)
        {
            Player target = Target();

            if (target == null)
                return MathHelper.PiOver4;

            Vector2 v = DonaLanceThrown.Aim(RidingPose(donkey).Tip, target);
            return MathHelper.Clamp((float)Math.Atan2(v.X * donkey.Facing, -v.Y) - lean, 0.2f, MathHelper.PiOver2 + 0.3f);
        }
    }
}
