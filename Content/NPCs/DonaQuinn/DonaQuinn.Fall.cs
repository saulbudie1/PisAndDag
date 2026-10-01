using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// Everything from the moment Triunfo throws her: the flight, the landing, lying there,
    /// getting up if she is spared, standing about after, and the cut if she is not.
    ///
    /// THE FALL. She is a body thrown, not an animation played. She leaves the saddle up
    /// and forward with what the donkey gave her, pitching forward; how long she will be in
    /// the air is worked out as she goes (the arc run forward until it meets the ground), and
    /// her turn is set so she comes down just past face down, head the way she was facing,
    /// the pose in DonnLyingdownRef.png. She comes off the ground with less than she hit it
    /// with, skids, and is knocked back onto her front, the turn easing onto the floor the
    /// last little way. Her head and arm are not bolted on: each hangs off the body on a
    /// loose spring, so when the body stops dead they carry on and swing back.
    ///
    /// The body's path is the server's and is sent; the turns are worked out on every
    /// machine from it, which is all they need to be.
    ///
    /// AFTER. From the first landing she bleeds now and then, a few small drops, and while
    /// she lies there a little of it gathers under her; and now and then she shakes.
    /// </summary>
    public partial class DonaQuinn
    {
        // ============================================================
        // THE FALL'S NUMBERS
        // ============================================================

        private const float Gravity = 0.32f;
        private const float MaxFall = 12f;

        /// <summary>The throw: forward and up, and how much of Triunfo's own speed she keeps.</summary>
        private static readonly Vector2 ThrowVelocity = new Vector2(3f, -5.4f);
        private const float ThrowCarry = 0.3f;

        /// <summary>
        /// She is turned to come down this far past face down, so the landing knocks her back
        /// onto it rather than her settling into it like a plank.
        /// </summary>
        private const float OverTurn = 0.35f;

        /// <summary>Coming off the floor: a landing faster than this bounces, keeping this share; the skid keeps this share sideways.</summary>
        private const float BounceFrom = 3.2f;
        private const float Bounce = 0.3f;
        private const float Skid = 0.55f;
        private const float WallBounce = 0.35f;

        /// <summary>On the ground, the turn settles onto the floor on this spring.</summary>
        private const float SettlePull = 0.14f;
        private const float SettleDamp = 0.72f;

        /// <summary>Still this long and she is lying there. Longest the throw may last regardless.</summary>
        private const int StillTicks = 20;
        private const int ThrownLongest = 300;

        /// <summary>
        /// She begs when the player's walk up to her is done (DonaScenePlayer tells the server),
        /// or after this long regardless.
        /// </summary>
        private const int PleadLatest = 720;

        /// <summary>A machine that hears of the throw later than this into it (one that joined late) finds her down already.</summary>
        private const int LateThrow = 10;

        /// <summary>Getting up, in ticks; the longest the talking may go on; staying about.</summary>
        private const int GetUpTicks = 60;
        private const int TalkLongest = 14400;
        private const int UnseenTicks = 60;

        /// <summary>The cut lands as the finish starts; then she is still; then she fades; then she is gone.</summary>
        private const int FadeAt = 100;
        private const int FadeTicks = 70;

        /// <summary>
        /// The head and the arm lying, on top of the body's quarter turn, as DonnLyingdownRef.png
        /// has them: the head upright (a quarter turn back), the arm reaching out 64 degrees from
        /// hanging, a little down. Begging, the head comes up 15 degrees from there, no more (by
        /// request). Dead, it drops 20 and the arm sags a little.
        /// </summary>
        private static readonly float LieHead = MathHelper.ToRadians(-90f);
        private static readonly float PleadHead = MathHelper.ToRadians(-105f);
        private static readonly float LieArm = MathHelper.ToRadians(-154f);
        private static readonly float DeadHead = MathHelper.ToRadians(-70f);
        private static readonly float DeadArm = MathHelper.ToRadians(-150f);

        /// <summary>Standing, how far she looks down at the ground.</summary>
        private const float GroundLook = 0.45f;

        /// <summary>The pool under her, at its widest, in pixels.</summary>
        private const float PoolWidest = 30f;

        // ============================================================
        // STATE: worked out on every machine
        // ============================================================

        /// <summary>Her body's turn, right-facing: 0 upright, a quarter turn forward is face down.</summary>
        private float body;
        private float spin;
        private float lastSpin;

        private float armRel;
        private float armVel;
        private float headRel;
        private float headVel;

        private int still;
        private bool landed;

        /// <summary>Where the body, the arm and the head were when she started getting up: they come in from there.</summary>
        private float upFromBody;
        private float upFromArm;
        private float upFromHead;

        /// <summary>Set the moment before the cut takes her life: the one death CheckDead lets through.</summary>
        private bool cut;

        /// <summary>Ticks of the shake left, and until the next; until the next drops of blood.</summary>
        private int shake;
        private int nextShake = 120;
        private int nextBleed = 40;

        private Vector2 poolAt;
        private float pool;
        private float poolFade = 1f;

        /// <summary>
        /// Standing about: looking at the ground rather than at anyone (the server's, sent with
        /// her), and until that changes (the server's own count).
        /// </summary>
        private bool lookingDown;
        private int lookFor = 180;

        private int unseen;

        /// <summary>Until she next turns about of her own accord (the server's).</summary>
        private int turnAbout = 420;

        // ============================================================
        // OFF THE DONKEY
        // ============================================================

        private void Fallen(TriunfoTheDonkey donkey)
        {
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.dontTakeDamage = true;
            NPC.life = Math.Max(1, NPC.life);

            Player target = Target();
            bool gone = target == null || target.dead;

            switch (State)
            {
                case Phase.Thrown:
                    if (fresh)
                    {
                        if (Timer <= LateThrow)
                            BeginThrow(donkey);
                        else
                            Down();
                    }

                    Tumble(airborneArm: true);

                    if (Authority && (still >= StillTicks || Timer >= ThrownLongest))
                        Go(Phase.Lying);
                    break;

                case Phase.Lying:
                    if (fresh)
                        Normalise();

                    Tumble(airborneArm: false);

                    // The walk up's end moves her on (Answered: Arrived); this, if it never comes.
                    if (Authority)
                    {
                        if (gone)
                            Go(Phase.Free);
                        else if (Timer >= PleadLatest)
                            Go(Phase.Plead);
                    }
                    break;

                case Phase.Plead:
                    Tumble(airborneArm: false);

                    if (Authority && gone)
                        Go(Phase.Free);
                    break;

                case Phase.GetUp:
                    if (fresh)
                    {
                        Normalise();
                        upFromBody = body;
                        upFromArm = armRel;
                        upFromHead = headRel;

                        if (Authority)
                            donkey?.ComeBack(NPC.Center.X);
                    }

                    GetUp();

                    if (Authority && Timer >= GetUpTicks)
                        Go(Phase.Talk);
                    break;

                case Phase.Talk:
                    // Up, she turns to the one who spared her, if they are not in front of her already.
                    if (!gone)
                        Turn(target);

                    Stand();

                    if (Authority && (Timer >= TalkLongest || gone))
                        Go(Phase.Free);
                    break;

                case Phase.Free:
                    if (body > 0.2f)
                    {
                        // Left lying when the player went: she gets herself up first. Until
                        // the server's word of it arrives, a client leaves her where she is.
                        Tumble(airborneArm: false);

                        if (Authority)
                            Go(Phase.GetUp);

                        break;
                    }

                    Stand();
                    Wander();

                    if (Authority)
                    {
                        unseen = Watched(NPC.Center) ? 0 : unseen + 1;

                        if (unseen >= UnseenTicks)
                            Vanish();
                    }
                    break;

                case Phase.Finish:
                    if (fresh)
                        Cut();

                    Tumble(airborneArm: false);

                    if (Timer >= FadeAt)
                        NPC.alpha = (int)MathHelper.Clamp((Timer - FadeAt) * 255f / FadeTicks, 0f, 255f);

                    if (Authority && Timer >= FadeAt + FadeTicks)
                    {
                        cut = true;
                        NPC.life = 0;
                        NPC.checkDead();

                        // checkDead sends nothing itself; this is how vanilla's own despawn
                        // tells the clients (NPC.CheckActive: life 0, then SyncNPC).
                        if (Main.netMode == NetmodeID.Server && !NPC.active)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
                    }
                    break;
            }
        }

        /// <summary>
        /// The first tick of the throw, on every machine: off the saddle from where she sat,
        /// with her lance away on its own, a cry, and the turn worked out to land her face down.
        /// </summary>
        private void BeginThrow(TriunfoTheDonkey donkey)
        {
            DonaRig.Pose seat = donkey != null ? RidingPose(donkey) : default;
            int d = donkey?.Facing ?? facing;

            facing = d;

            Resize(24, 24);

            if (donkey != null)
                NPC.Center = seat.MiddleAt;

            Vector2 carry = donkey != null ? donkey.NPC.velocity * ThrowCarry : Vector2.Zero;
            NPC.velocity = new Vector2(d * ThrowVelocity.X, ThrowVelocity.Y) + carry;

            // From where the saddle had her: tipped back on the rear.
            body = DonaRig.RearTilt + lean;
            armRel = -MathHelper.PiOver2;
            headRel = headTilt;
            armVel = 0f;
            headVel = 0f;

            int ticks = AirTicks();
            spin = (MathHelper.PiOver2 + OverTurn - body) / ticks;
            lastSpin = spin;

            landed = false;
            still = 0;

            // Nothing has stopped her yet: the saddle had no collision of its own to leave these set.
            NPC.collideX = false;
            NPC.collideY = false;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.FemaleHit with { Pitch = 0.25f }, NPC.Center);

                // The lance goes up out of her hand, if it is in her hand: thrown, it is in the
                // ground somewhere, and fades there (DonaLanceThrown).
                if (donkey != null && hasLance)
                    DonaLanceGore.Throw(NPC, seat, d);
            }

            if (Authority)
                donkey?.Bolt(d);
        }

        /// <summary>
        /// For a machine that only hears of the throw when it is long over: no flight, no
        /// cry, no second lance; she is already down, face down, and lies there.
        /// </summary>
        private void Down()
        {
            Resize(24, 24);

            body = MathHelper.PiOver2;
            spin = 0f;
            lastSpin = 0f;
            headRel = LieHead;
            armRel = LieArm;
            headVel = 0f;
            armVel = 0f;
            landed = true;
            still = 0;
            poolAt = new Vector2(NPC.Center.X + facing * 6f, NPC.Bottom.Y);
        }

        /// <summary>How long the arc she is on now runs before it meets something to land on.</summary>
        private int AirTicks()
        {
            Vector2 at = NPC.position;
            Vector2 v = NPC.velocity;

            for (int t = 1; t <= 120; t++)
            {
                v.Y = Math.Min(v.Y + Gravity, MaxFall);
                at += v;

                if (v.Y > 0f && Collision.SolidCollision(at, NPC.width, NPC.height))
                    return Math.Max(t, 16);
            }

            return 120;
        }

        /// <summary>
        /// One tick of a body on its own: what the last move did to it (a floor, a wall), the
        /// bounce and skid, gravity, the turn (free in the air, settling on the ground), and
        /// the loose head and arm. Lying still is the same thing with nothing left to do.
        ///
        /// What the last move did is read the way vanilla leaves it (NPC.UpdateCollision):
        /// oldVelocity is what she was moving at going into it, collideY / collideX say a floor
        /// or a wall cut it short, and velocity is only the part of it that was left, the gap
        /// she closed, not nothing. So the landing is told by the flag and measured by the
        /// speed she came down at, never by the gap.
        /// </summary>
        private void Tumble(bool airborneArm)
        {
            Resize(24, 24);

            bool onFloor = NPC.collideY && NPC.oldVelocity.Y > 0f;

            if (NPC.collideX && Math.Abs(NPC.oldVelocity.X) > 0.5f)
                NPC.velocity.X = -NPC.oldVelocity.X * WallBounce;

            if (onFloor)
            {
                // On it: whatever was left of the fall went on closing the gap.
                NPC.velocity.Y = 0f;

                if (NPC.oldVelocity.Y > 1.5f)
                    Land(NPC.oldVelocity.Y);

                NPC.velocity.X *= 0.82f;

                // The turn finds the floor: the nearest way of lying face down.
                float rest = RestTurn();
                spin = spin * SettleDamp + (rest - body) * SettlePull;

                still = Math.Abs(rest - body) < 0.03f && Math.Abs(spin) < 0.01f && Math.Abs(NPC.velocity.X) < 0.2f
                    ? still + 1
                    : 0;
            }
            else
            {
                spin *= 0.998f;
                still = 0;
            }

            body += spin;

            // The loose parts: each pulled toward where it wants to be and thrown the other
            // way by any sudden change in how the body is turning.
            float jolt = spin - lastSpin;
            lastSpin = spin;

            float headWant;
            float armWant;

            if (!landed)
            {
                float t = Main.GlobalTimeWrappedHourly * 60f;
                headWant = 0.25f;
                armWant = airborneArm ? -2.3f + 0.5f * (float)Math.Sin(t * 0.35f) : LieArm;
            }
            else if (State == Phase.Finish && Timer > 0)
            {
                headWant = DeadHead;
                armWant = DeadArm;
            }
            else if (State == Phase.Plead)
            {
                headWant = PleadHead;
                armWant = LieArm;
            }
            else
            {
                headWant = LieHead;
                armWant = LieArm;
            }

            Swing(headWant, armWant, jolt);

            NPC.velocity.Y = Math.Min(NPC.velocity.Y + Gravity, MaxFall);
        }

        /// <summary>A landing: up off the floor if it was hard enough, the skid, dust, and the first time, a cry and blood.</summary>
        private void Land(float speed)
        {
            if (speed > BounceFrom)
                NPC.velocity.Y = -speed * Bounce;

            NPC.velocity.X *= Skid;
            spin *= 0.35f;

            if (!Main.dedServ)
            {
                for (int i = 0; i < (int)(speed * 2f); i++)
                {
                    Dust dust = Dust.NewDustDirect(new Vector2(NPC.position.X, NPC.Bottom.Y - 4f), NPC.width, 4, DustID.Dirt,
                        Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f), 60, default, Main.rand.NextFloat(0.9f, 1.3f));
                    dust.noGravity = false;
                }

                SoundEngine.PlaySound(SoundID.Dig with { Volume = MathHelper.Clamp(speed / 8f, 0.3f, 1f), Pitch = -0.4f }, NPC.Bottom);

                if (!landed)
                {
                    SoundEngine.PlaySound(SoundID.FemaleHit, NPC.Center);
                    BloodDroplet.SpawnBurst(NPC.Center, new Vector2(-facing * 0.5f, -1f), 6, 30f);

                    if (Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 900f)
                        Main.LocalPlayer.PIS().BumpScreenShake(3f, 10);
                }
            }

            if (!landed)
                poolAt = new Vector2(NPC.Center.X + facing * 6f, NPC.Bottom.Y);

            landed = true;
        }

        /// <summary>The nearest way of lying face down to the turn she has now.</summary>
        private float RestTurn()
        {
            float laps = (float)Math.Round((body - MathHelper.PiOver2) / MathHelper.TwoPi);
            return MathHelper.PiOver2 + laps * MathHelper.TwoPi;
        }

        /// <summary>Takes the whole turns off her turn, once she is lying, so later easing never unwinds them.</summary>
        private void Normalise()
        {
            float laps = (float)Math.Round((body - MathHelper.PiOver2) / MathHelper.TwoPi);
            body -= laps * MathHelper.TwoPi;
            landed = true;
        }

        /// <summary>The loose head and arm, each on a spring, kicked by a jolt in the body's turn.</summary>
        private void Swing(float headWant, float armWant, float jolt)
        {
            headVel += (headWant - headRel) * 0.16f - headVel * 0.28f - jolt * 0.9f;
            headRel = MathHelper.Clamp(headRel + headVel, -2.2f, 1.3f);

            armVel += (armWant - armRel) * 0.12f - armVel * 0.22f - jolt * 1.2f;
            armRel += armVel;
        }

        // ============================================================
        // UP, AND STANDING ABOUT
        // ============================================================

        /// <summary>Up off the ground: the body turned back upright, rising onto her feet, a little wobble at the top.</summary>
        private void GetUp()
        {
            float u = MathHelper.Clamp(Timer / (float)GetUpTicks, 0f, 1f);

            // Over the top a little and back: up a touch too fast, steadied.
            const float c1 = 1.4f;
            const float c3 = c1 + 1f;
            float w = u - 1f;
            float back = 1f + c3 * w * w * w + c1 * w * w;

            body = MathHelper.PiOver2 * (1f - back);

            // Her hitbox stands up with her, halfway through.
            if (u >= 0.5f)
                Resize(20, 44);

            NPC.velocity.X = 0f;
            NPC.velocity.Y = Math.Min(NPC.velocity.Y + Gravity, MaxFall);

            // The arm comes in to her side and the head round to whoever is there as she comes
            // up, eased, not swung on the springs: she is getting up, not being thrown about.
            // Eased as she holds them, against the world, not against her body: the body comes
            // up faster than they do, and turned with it the head would be thrown back.
            float e = u * u * (3f - 2f * u);

            armRel = MathHelper.Lerp(upFromBody + upFromArm, 0f, e) - body;
            headRel = MathHelper.Lerp(upFromBody + upFromHead, LookAt(Nearest()), e) - body;
            armVel = 0f;
            headVel = 0f;
        }

        /// <summary>On her feet: still, upright, head on whoever is there or on the ground.</summary>
        private void Stand()
        {
            Resize(20, 44);

            body += (0f - body) * 0.2f;
            NPC.velocity.X = 0f;
            NPC.velocity.Y = Math.Min(NPC.velocity.Y + Gravity, MaxFall);

            float t = Main.GlobalTimeWrappedHourly * 60f;
            float head = lookingDown && State == Phase.Free ? GroundLook : LookAt(Nearest());

            Swing(head, 0.04f * (float)Math.Sin(t * 0.03f), 0f);
        }

        /// <summary>The nearest living player within 45 blocks of her, or null.</summary>
        private Player Nearest()
        {
            Player near = null;
            float best = 720f;

            foreach (Player player in Main.ActivePlayers)
            {
                float dist = Vector2.Distance(player.Center, NPC.Center);

                if (!player.dead && dist < best)
                {
                    best = dist;
                    near = player;
                }
            }

            return near;
        }

        /// <summary>
        /// Her head on this player: a neck rather than a swivel, a little up or down. Only in
        /// front of her: she never looks back over her shoulder (by request); to look at
        /// someone behind her, the whole of her turns round (Turn). Nobody, or nobody in front:
        /// straight ahead.
        /// </summary>
        private float LookAt(Player near)
        {
            if (near == null)
                return 0f;

            Vector2 nape = DonaRig.OnFoot(VisualMiddle(), facing, body, armRel, headRel).NapeAt;
            Vector2 d = near.Center - nape;

            if (Math.Sign(d.X) != facing)
                return 0f;

            return MathHelper.Clamp((float)Math.Atan2(d.Y, Math.Max(1f, Math.Abs(d.X))), -0.35f, 0.3f);
        }

        /// <summary>
        /// The whole of her round to face this player, body and head together, if they are
        /// behind her (the server's; sent with her). A plain turn, there and then, as Triunfo's.
        /// </summary>
        private void Turn(Player toward)
        {
            if (!Authority || toward == null || Math.Abs(toward.Center.X - NPC.Center.X) < 12f)
                return;

            int way = Math.Sign(toward.Center.X - NPC.Center.X);

            if (way != facing)
            {
                facing = way;
                NPC.netUpdate = true;
            }
        }

        /// <summary>
        /// Left alone (the server's, sent with her): now looking at whoever is nearest, turned
        /// to face them; now at the ground, when now and then she turns about of her own accord,
        /// as if she had a mind of her own.
        /// </summary>
        private void Wander()
        {
            if (!Authority)
                return;

            if (--lookFor <= 0)
            {
                lookingDown = !lookingDown;
                lookFor = Main.rand.Next(lookingDown ? 120 : 180, lookingDown ? 260 : 360);
                NPC.netUpdate = true;
            }

            if (!lookingDown)
            {
                Turn(Nearest());
                return;
            }

            if (--turnAbout <= 0)
            {
                facing = -facing;
                turnAbout = Main.rand.Next(300, 660);
                NPC.netUpdate = true;
            }
        }

        // ============================================================
        // THE FINISH
        // ============================================================

        /// <summary>
        /// The cut: a line of light through her, the King's but smaller, and the blood thrown
        /// along it both ways; her head drops. CheckDead lets her go at the end of the fade.
        /// </summary>
        private void Cut()
        {
            if (Main.dedServ)
                return;

            Vector2 along = new Vector2(facing, 1f).SafeNormalize(Vector2.UnitY);

            PADParticleHandler.SpawnParticle(new SlashStreak(NPC.Center, along, 30f, 14f, 26, NPC));
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 1f, Pitch = -0.6f }, NPC.Center);
            Main.LocalPlayer.PIS().BumpScreenShake(5f, 16);

            Vector2 across = along.RotatedBy(MathHelper.PiOver2);
            BloodDroplet.SpawnBurst(NPC.Center, across, 9, 26f);
            BloodDroplet.SpawnBurst(NPC.Center, -across, 6, 26f);

            for (int i = 0; i < 14; i++)
            {
                Vector2 at = NPC.Center + along * Main.rand.NextFloat(-18f, 18f);
                Dust.NewDustPerfect(at, DustID.Blood, across * Main.rand.NextFloat(-3f, 3f), 0, default, Main.rand.NextFloat(1f, 1.5f));
            }

            headVel += 0.25f;
            spin += 0.04f;
            pool = Math.Max(pool, 14f);
        }

        // ============================================================
        // AFTER THE FALL: blood, and the shakes
        // ============================================================

        /// <summary>Once she has landed: now and then a few drops, and while she lies there, a little pool; now and then a shake.</summary>
        private void Aftermath()
        {
            if (!landed || State == Phase.Finish && NPC.alpha > 0)
                return;

            if (shake > 0)
                shake--;
            else if (--nextShake <= 0)
            {
                shake = 16;
                nextShake = Main.rand.Next(100, 230);
            }

            bool lying = State == Phase.Thrown || State == Phase.Lying || State == Phase.Plead || State == Phase.Finish;

            if (!lying && pool > 0f)
                poolFade = Math.Max(0f, poolFade - 1f / 1200f);

            if (--nextBleed > 0)
                return;

            nextBleed = Main.rand.Next(70, 150);

            if (lying)
                pool = Math.Min(PoolWidest, pool + Main.rand.NextFloat(1.5f, 3f));

            if (Main.dedServ)
                return;

            DonaRig.Pose pose = DonaRig.OnFoot(VisualMiddle(), facing, body, armRel, headRel);
            Vector2 from = lying ? pose.NapeAt + new Vector2(facing * 4f, 6f) : pose.MiddleAt + new Vector2(-facing * 6f, -4f);
            int life = lying ? Main.rand.Next(8, 14) : Main.rand.Next(10, 13);

            for (int i = 0; i < Main.rand.Next(1, 3); i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(from + Main.rand.NextVector2Circular(3f, 3f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.6f, 0.4f)),
                    life, Main.rand.NextFloat(0.4f, 0.6f), new Color(140, 8, 12)));
            }
        }

        /// <summary>The shake, now: a few pixels side to side, dying away, and a twitch of the turn.</summary>
        private Vector2 ShakeOffset() => shake > 0
            ? new Vector2((float)Math.Sin(shake * 2.4f) * 1.6f * (shake / 16f), 0f)
            : Vector2.Zero;

        private float ShakeTurn() => shake > 0 ? 0.025f * (float)Math.Sin(shake * 3.1f) * (shake / 16f) : 0f;

        /// <summary>
        /// Her middle, as drawn: the hitbox's middle in the air, and on the ground the right
        /// height over the ground for how she is turned: lying, as the reference has her (on
        /// the hand under her chin); upright, on her soles; and part way, never lower than the
        /// lowest corner of her body reaches, so that getting up she comes up onto the toe of
        /// her boot instead of through the floor.
        /// </summary>
        private Vector2 VisualMiddle()
        {
            if (State == Phase.Thrown && !landed)
                return NPC.Center;

            float upright = Math.Abs((float)Math.Cos(body));
            float over = Math.Abs((float)Math.Sin(body));

            float height = Math.Max(MathHelper.Lerp(DonaRig.LieHeight, DonaRig.StandHeight, upright * upright),
                DonaRig.BodyHalfWidth * over + DonaRig.BodyHalfHeight * upright);

            return new Vector2(NPC.Center.X, NPC.Bottom.Y + NPC.gfxOffY - height);
        }
    }
}
