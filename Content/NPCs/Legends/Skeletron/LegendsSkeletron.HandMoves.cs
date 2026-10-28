using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- his hands' moves (Legends, while he has them) ----------------------------------------------------------------
    //
    // By request, twelve, and "3 more of my own: cartoonish but epic, bone-related, with sounds". This file has the
    // shared parts and the first seven; LegendsSkeletron.HandMoves2 the rest.
    //
    //     BASKETBALL   a skull spat into one hand; bounced twice off the ground, the hand coming on slowly, the head
    //                  still; a fadeaway jump shot that comes down hard
    //     CLAP         the hands either side of the player, locked; they meet, and the shock of it goes up
    //     TORNADO      a middle finger raised; then the arms spun round him like a propeller, and he comes
    //     SOLO         one hand up close with the middle finger, the other on an air guitar; his skull opens and spits
    //                  skulls in time to the riff; rock on
    //     COMBO        a punch, a backhand, and the fists bumped together where the player is (fists, fingers hidden)
    //     WAG          a forefinger raised and wagged, no, no, no; he turns aside whatever is shot at him meanwhile; the
    //                  other hand slaps
    //     FLICK        a hand by the player, a finger held back by the thumb, a line where it will go; nothing stops
    //                  it (the yellow), and it throws them

    public partial class LegendsSkeletron
    {
        // ---- shared -----------------------------------------------------------------------------------------------------

        /// <summary>A shot's damage, as his own skulls are reckoned, a fifth again (Legends).</summary>
        internal static int Damage(NPC npc, float normal, float expert) => Math.Max(1, (int)(npc.GetAttackDamage_ForProjectiles(normal, expert) * DamageScale));

        /// <summary>The server makes a shot of his; nothing elsewhere.</summary>
        internal int Shoot(NPC npc, Vector2 at, Vector2 velocity, int type, int damage, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f)
        {
            if (!Authority)
                return -1;

            return Projectile.NewProjectile(npc.GetSource_FromAI(), at, velocity, type, damage, 0f, Main.myPlayer, ai0, ai1, ai2);
        }

        /// <summary>The server fixes a move's second point now, and sends it (a client guesses it until heard, as <see cref="Fix"/>).</summary>
        internal void FixSecond(NPC npc, Vector2 at)
        {
            if (Authority)
            {
                mark2 = at;
                fixes++;
                npc.netUpdate = true;
                return;
            }

            if (Guessed())
                mark2 = at;
        }

        /// <summary>He hangs about over the player while his hands work.</summary>
        private static void Hover(NPC npc, Player target, Vector2 offset, float gain = 0.04f, float most = 6f) => Approach(npc, target.Center + offset, gain, most);

        /// <summary>His mouth now, in the world.</summary>
        internal Vector2 MouthOf(NPC npc) => SkeletronRig.Mouth(npc.Center, npc.rotation, npc.scale, jawShown, liftShown);

        internal static Vector2 ShoulderOf(NPC npc, float side) => SkeletronRig.Shoulder(npc.Center, side, npc.scale);

        /// <summary>Where a hand of his is now (or where it would hang, if it is not here).</summary>
        internal Vector2 HandAt(NPC npc, float side) => HandOn(side)?.Center ?? Rest(npc, side);

        /// <summary>The tip of a digit of a hand of his, posed as asked this tick.</summary>
        internal Vector2 TipOf(NPC npc, float side, SkeletronRig.Digit digit)
        {
            Aim a = aims[IndexOf(side)];
            Vector2 middle = HandAt(npc, side);
            Vector2 wrist = SkeletronRig.WristFromMiddle(middle, a.Angle, side > 0f, a.Scale <= 0f ? 1f : a.Scale);
            return SkeletronRig.Tip(digit, a.Pose, wrist, a.Angle, side > 0f, a.Scale <= 0f ? 1f : a.Scale);
        }

        /// <summary>A hand hanging back by him, harmless, while the other works.</summary>
        private void Wait(NPC npc, float side)
        {
            if (HandOn(side) == null)
                return;

            Idle(npc, side);
            aims[IndexOf(side)].Hurt = 0f;
        }

        /// <summary>A sweep of an open hand across where the player was, from its own side to the other: where it is, and the way it is going.</summary>
        private static Vector2 Sweep(Vector2 at, float side, float u, out Vector2 way)
        {
            Vector2 start = at + new Vector2(side * 300f, -100f);
            Vector2 low = at + new Vector2(0f, 40f);
            Vector2 end = at + new Vector2(-side * 300f, -40f);

            Vector2 here = SkeletronRig.Curve(start, low, end, u);
            Vector2 ahead = SkeletronRig.Curve(start, low, end, Math.Min(1f, u + 0.02f));
            way = (ahead - here).SafeNormalize(new Vector2(-side, 0f));
            return here;
        }

        /// <summary>What a move needs fixed as it begins (the server's).</summary>
        private void OnBegin(NPC npc, Player target)
        {
            switch (move)
            {
                case Move.Seals:
                    aim = Main.rand.NextFloat(MathHelper.TwoPi);
                    break;

                case Move.Dive:
                    aim = ChooseWay(npc, target);
                    break;

                case Move.Blaze:
                    aim = (target.Center - npc.Center).ToRotation();
                    break;

                case Move.Spin:
                    npc.ai[1] = 1f;
                    npc.ai[2] = 0f;
                    break;
            }
        }

        /// <summary>A hand of his hit someone (on their machine): what the move makes of it.</summary>
        internal void HandHit(NPC npc, NPC hand, Player target)
        {
            if (target.whoAmI != Main.myPlayer)
                return;

            switch (move)
            {
                case Move.Combo:
                case Move.Swoop:
                case Move.Rps:
                    SoundEngine.PlaySound(SkeletronSounds.Punch with { Volume = 1f }, target.Center);
                    Main.LocalPlayer.PIS().BumpScreenShake(7f, 12);
                    break;

                case Move.Whip:
                    Main.LocalPlayer.PIS().BumpScreenShake(9f, 14);
                    break;
            }
        }

        // ---- where his shots and his reaches are (asked by them) -----------------------------------------------------

        /// <summary>Whether one of his reaches touches this box this tick (SkeletronHitbox): an arm spun, the flick's fingertip.</summary>
        internal bool HitArea(NPC npc, int kind, Rectangle box)
        {
            switch (kind)
            {
                case (int)SkeletronHitbox.ArmLeft:
                case (int)SkeletronHitbox.ArmRight:
                    {
                        // (Not as they go back to rest: hands and arms harmless then.)
                        if (move != Move.Tornado || moveTimer >= TornadoEnd - 14)
                            return false;

                        float side = kind == (int)SkeletronHitbox.ArmLeft ? -1f : 1f;
                        NPC h = HandOn(side);

                        return h != null && SkeletronShots.Touches(box, ShoulderOf(npc, side), h.Center, 26f);
                    }

                case (int)SkeletronHitbox.FlickTip:
                    {
                        if (move != Move.Flick || moveTimer < FlickHitAt || moveTimer >= FlickHitAt + FlickHitTicks)
                            return false;

                        return SkeletronShots.Touches(box, HandAt(npc, actSide) + FlickWay() * 56f, 44f);
                    }
            }

            return false;
        }

        /// <summary>One of his reaches hit someone (on their machine).</summary>
        internal void AreaHit(NPC npc, int kind, Player target)
        {
            if (target.whoAmI != Main.myPlayer)
                return;

            if (kind == (int)SkeletronHitbox.FlickTip)
            {
                target.velocity = FlickWay() * 16f + new Vector2(0f, -5f);
                target.fallStart = (int)(target.position.Y / 16f);
                Main.LocalPlayer.PIS().BumpScreenShake(8f, 14);
            }
        }

        // ---- under and over him: what a move draws ---------------------------------------------------------------------

        private void DrawMoveUnder(NPC npc, SpriteBatch spriteBatch)
        {
            if (scene != Scene.Fight)
                return;

            switch (move)
            {
                case Move.Clap: DrawClap(npc, spriteBatch); break;
                case Move.Swoop: DrawSwoopPath(npc, spriteBatch); break;
                case Move.Snap: DrawCrackLine(npc, spriteBatch); break;
                case Move.Dive: DrawDivePath(npc, spriteBatch); break;
                case Move.Raise: DrawRisings(npc, spriteBatch); break;
            }
        }

        private void DrawMoveOver(NPC npc, SpriteBatch spriteBatch)
        {
            if (scene != Scene.Fight)
                return;

            switch (move)
            {
                case Move.Flick: DrawFlickLine(npc, spriteBatch); break;
                case Move.Wag: DrawWag(npc, spriteBatch); break;
                case Move.Seals: DrawSeals(npc, spriteBatch); break;
                case Move.Purple: DrawPurple(npc, spriteBatch); break;
                case Move.Rps: DrawRps(npc, spriteBatch); break;
                case Move.Solo: DrawSolo(npc, spriteBatch); break;
            }
        }

        // =================================================================================================================
        // BASKETBALL
        // =================================================================================================================

        private const int BallTake = 30;
        private const int BallCarry = 10;
        private const int BallPush1 = 40;
        private const int BallFall = 10;
        private const int BallRise = 12;
        private const int BallDribble = BallFall + BallRise;
        private const int BallDribbles = 3;
        private const int BallFade = 110;
        private const int BallRelease = 124;
        private const int BallAfter = 40;

        /// <summary>
        /// How far his skull's middle is from the middle of a hand palming it: the hand over it, fingers spread down over
        /// the crown (or under it, fingertips up, for the shot).
        /// </summary>
        private float BallGrip(NPC npc) => Round(npc) + 28f;

        /// <summary>The shot's flight, in the game's ticks, worked out as it is let go; and the tick of his clock it lands.</summary>
        private float ballFlight;
        private int ballLands = int.MaxValue;
        private Vector2 ballFrom;
        private Vector2 ballVelocity;

        /// <summary>The pull on his skull in flight, a tick.</summary>
        private const float BallGravity = 0.35f;

        private void Basketball(NPC npc, Player target)
        {
            float t = moveClock;
            float r = Round(npc);

            // His skull is the ball (by request: "Basketball move use the head itself"): palmed off the top by a hand,
            // carried down to the floor, dribbled at them and crossed over from hand to hand, and shot at them in a high
            // fadeaway arc that comes down on them hard; then up off the floor, back to his hands.
            if (At(BallTake))
            {
                FixSecond(npc, npc.Center);
                Fix(npc, target.Center);
            }

            float dir = BallDir();

            if (t < BallTake)
            {
                npc.velocity *= 0.85f;
                headHurt = 1f;
            }
            else if (t < BallRelease)
            {
                // In his hands: where the ball is, his skull is.
                Vector2 at = BallOn(npc, t);
                Place(npc, at);
                npc.rotation = 0.35f * (float)Math.Sin((at.X - mark2.X) / r);
                headHurt = 1.2f;
            }
            else if (t < ballLands)
            {
                // The shot: up and over, turning, down on them. (Its arc worked out here if this machine missed its start.)
                if (ballFlight <= 0f)
                    LaunchBall(npc);

                float flown = Math.Min((t - BallRelease) / Pace, ballFlight);
                Place(npc, ballFrom + ballVelocity * flown + new Vector2(0f, 0.5f * BallGravity * flown * flown));
                npc.rotation += dir * 0.25f * Pace;
                headHurt = 1.8f;
            }
            else
            {
                // Up off the floor, and back to hanging over them.
                Approach(npc, target.Center + new Vector2(0f, -260f), 0.06f, 9f);
                npc.rotation = MathHelper.WrapAngle(npc.rotation) * 0.88f;
                headHurt = 1f;
            }

            // The shot let go: aimed where they are now, at the floor under them; how long it flies, from how far.
            if (At(BallRelease))
            {
                Fix(npc, target.Center);
                LaunchBall(npc);
                Sound(SkeletronSounds.Fadeaway, ballFrom);
            }

            if (!Main.dedServ && t >= BallRelease && t < ballLands && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(r, r), DustID.Bone, -npc.velocity * 0.15f, 120, default, 1.2f);
                dust.noGravity = true;
            }

            // Down on them: bone and a shock round where it lands, and he bounces up off it.
            if (At(ballLands))
            {
                Vector2 hit = npc.Center + new Vector2(0f, r);

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SkeletronSounds.Thud, hit);
                    SoundEngine.PlaySound(SkeletronSounds.SelfCrack with { Volume = 0.7f, Pitch = 0.3f }, hit);

                    for (int i = 0; i < 26; i++)
                    {
                        Vector2 speed = Main.rand.NextVector2Circular(8f, 8f) + new Vector2(0f, -3f);
                        Dust.NewDustPerfect(hit, DustID.Bone, speed, 0, default, Main.rand.NextFloat(1.1f, 1.7f));
                        PADParticleHandler.SpawnParticle(new StoneChip(hit, speed * 0.6f, new Color(214, 206, 170), Main.rand.Next(40, 70), 1.3f));
                    }

                    GroundWave.Slam(hit, 24f, 16, 16f, 24);
                    Shake(hit, 8f, 16);
                }

                Shoot(npc, hit, Vector2.Zero, ModContent.ProjectileType<SkeletronBlast>(), Damage(npc, 30f, 26f), 110f, SkeletronBlast.Bone);
                npc.velocity = new Vector2(-dir * 3f, -9f);
            }

            // Both hands down on the floor with it (their arms off him as far as it takes), dribbling it at them, crossed
            // over from one hand to the other; the shot, a fadeaway, the shooting hand's wrist left hanging; then home.
            for (int k = 0; k < 2; k++)
            {
                float s = SideOf(k);

                if (HandOn(s) == null)
                    continue;

                Aim a = Aim.At(BallHand(npc, s, t, out float angle, out SkeletronRig.Pose pose, out bool exact), angle, pose, exact ? 1f : 0.3f, 40f);
                a.Turn = exact ? 1f : 0.3f;
                a.Hurt = t >= BallTake && t < BallRelease ? 0.6f : 0f;
                aims[k] = a;
            }

            // Off the floor: the bounce, its sound and a small shock where it hits.
            for (int k = 0; k < BallDribbles; k++)
            {
                int push = BallPush1 + k * BallDribble;

                if (At(push))
                    Sound(SoundID.Item1 with { Pitch = -0.4f + 0.1f * k, Volume = 0.6f }, npc.Center);

                if (At(push + BallFall))
                {
                    Vector2 floor = BallBounce(npc, k) + new Vector2(0f, r);
                    Sound(SkeletronSounds.Dribble with { Pitch = -0.3f + k * 0.12f }, floor);

                    if (!Main.dedServ)
                    {
                        for (int i = 0; i < 14; i++)
                            Dust.NewDustPerfect(floor, DustID.Bone, new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(1f, 4f)), 0, default, 1.3f);

                        PADParticleHandler.SpawnParticle(new ParryRing(floor, 10f, 90f, 14, new Color(240, 228, 190)));
                        Shake(floor, 4f, 10);
                    }

                    Shoot(npc, floor, Vector2.Zero, ModContent.ProjectileType<SkeletronBlast>(), Damage(npc, 10f, 9f), 60f, SkeletronBlast.Bone);
                }
            }

            if (At(BallTake))
                Sound(SkeletronSounds.Clack, npc.Center);

            if (Authority && ballLands != int.MaxValue && t >= ballLands + BallAfter)
                End(npc);
        }

        /// <summary>The way the dribble goes: from where his skull was palmed toward where they were then.</summary>
        private float BallDir() => mark.X >= mark2.X ? 1f : -1f;

        /// <summary>His skull let go for the shot: from where it is on the fingertips, an arc down onto the floor under them.</summary>
        private void LaunchBall(NPC npc)
        {
            float r = Round(npc);
            ballFrom = BallOn(npc, BallRelease);
            Vector2 to = new Vector2(mark.X, BallGround(mark.X) - r);
            Vector2 d = to - ballFrom;

            ballFlight = MathHelper.Clamp(d.Length() / 15f, 26f, 46f);
            ballVelocity = new Vector2(d.X / ballFlight, d.Y / ballFlight - 0.5f * BallGravity * ballFlight);
            ballLands = BallRelease + (int)Math.Ceiling(ballFlight * Pace);
        }

        /// <summary>The hand that palms it first, and the one it is crossed over to (the same with one hand).</summary>
        private float BallFirst => actSide;

        private float BallSecond(NPC npc) => HandOn(-actSide) != null ? -actSide : actSide;

        /// <summary>Which hand has it on a dribble: the first two off the first hand, the crossover caught by the second.</summary>
        private float BallFrom(NPC npc, int k) => k < 2 ? BallFirst : BallSecond(npc);

        private float BallTo(NPC npc, int k) => k < 1 ? BallFirst : BallSecond(npc);

        /// <summary>The floor it is dribbled on, near a point (or a line at the player's feet with none).</summary>
        private float BallGround(float x) => GroundOr(x, 20f, mark.Y, 400f);

        /// <summary>Where along the floor the dribbling is, this tick: from under where he was on toward them, stopping short.</summary>
        private float BallX(NPC npc, float t)
        {
            float dir = BallDir();
            float from = mark2.X;
            float to = mark.X - dir * 190f;

            if ((to - from) * dir < 0f)
                to = from;

            return MathHelper.Lerp(from, to, Ease((t - BallTake) / (BallFade - BallTake)));
        }

        /// <summary>A dribbling hand's spot: palm down over the floor, either side of the line, his skull under it.</summary>
        private Vector2 DribbleSpot(NPC npc, float s, float t)
        {
            float x = BallX(npc, t) + s * 60f;
            return new Vector2(x, BallGround(x) - Round(npc) - 40f - BallGrip(npc));
        }

        /// <summary>The floor a dribble bounces on (his skull's middle there): between the hand it leaves and the hand it comes up to.</summary>
        private Vector2 BallBounce(NPC npc, int k)
        {
            int hit = BallPush1 + k * BallDribble + BallFall;
            float x = (DribbleSpot(npc, BallFrom(npc, k), hit).X + DribbleSpot(npc, BallTo(npc, k), hit).X) * 0.5f;
            return new Vector2(x, BallGround(x) - Round(npc));
        }

        /// <summary>
        /// Where one hand goes, turned and posed, on this tick of the move; and whether it is placed exactly (when it holds
        /// his skull, so his skull sits in it).
        /// </summary>
        private Vector2 BallHand(NPC npc, float s, float t, out float angle, out SkeletronRig.Pose pose, out bool exact)
        {
            float dir = BallDir();
            float first = BallFirst;
            float shooter = BallSecond(npc);
            float grip = BallGrip(npc);
            exact = false;

            if (t < BallTake)
            {
                // Over the top of his skull, fingers spread down over it; the other hand waits.
                if (s == first)
                {
                    angle = 0f;
                    pose = SkeletronRig.Pose.Lerp(SkeletronRig.Spread, SkeletronRig.Clutch, Ease((t - 16f) / 12f));
                    return npc.Center + new Vector2(0f, -grip);
                }

                angle = RestAngle(npc, s);
                pose = Hanging(s);
                return Rest(npc, s);
            }

            if (t >= BallRelease + 24f)
            {
                angle = RestAngle(npc, s);
                pose = SkeletronRig.Relaxed;
                return Rest(npc, s);
            }

            if (t >= BallFade)
            {
                // The shot: the shooter up and back with it on its fingertips, the other hand at its side guiding it;
                // let go, the shooter's wrist drops, fingers after it.
                Vector2 low = DribbleSpot(npc, shooter, BallFade);
                float up = EaseOut(Math.Min(1f, (t - BallFade) / (BallRelease - BallFade)));
                Vector2 shot = low + new Vector2(-dir * 110f, -200f) * up + new Vector2(0f, 2f * grip) * Ease(Math.Min(1f, (t - BallFade) / 6f));

                if (s == shooter)
                {
                    // Turned up from palm down, his skull rolled round onto its fingertips the way it is going.
                    float upright = MathHelper.WrapAngle(SkeletronRig.AngleFor(new Vector2(dir * 0.35f, -1f)));
                    exact = t < BallRelease;
                    angle = t < BallRelease ? MathHelper.Lerp(0f, upright, Ease(Math.Min(1f, (t - BallFade) / 6f))) : SkeletronRig.AngleFor(new Vector2(dir, 0.9f));
                    pose = t < BallRelease ? SkeletronRig.Spread : SkeletronRig.Relaxed;
                    return shot;
                }

                angle = SkeletronRig.AngleFor(new Vector2(dir, -0.2f));
                pose = SkeletronRig.Flat;
                return t < BallRelease ? shot + new Vector2(-dir * 70f, -grip) : shot + new Vector2(-dir * 100f, 40f);
            }

            // Which hand has it now: the first until the crossover comes up into the second.
            int crossed = BallPush1 + BallDribble + BallFall;
            float holding = t < crossed ? first : shooter;
            Vector2 spot = DribbleSpot(npc, s, t);

            if (t < BallPush1 && s == first)
            {
                // Carried down from over where he was, palming it, to the floor.
                exact = true;
                angle = 0f;
                pose = SkeletronRig.Clutch;
                return Vector2.Lerp(mark2 + new Vector2(0f, -grip), spot, Ease((t - BallTake) / BallCarry));
            }

            // The dribble: the hand that pushes goes down with it and back; the hand it comes up to rises to meet it.
            for (int k = 0; k < BallDribbles; k++)
            {
                int push = BallPush1 + k * BallDribble;

                if (s == BallFrom(npc, k) && t >= push - 2 && t < push + 8)
                    spot.Y += 22f * (float)Math.Sin(MathHelper.Pi * (t - push + 2) / 10f);

                if (s == BallTo(npc, k) && t >= push + BallFall && t < push + BallDribble)
                    spot.Y -= 14f * (float)Math.Sin(MathHelper.Pi * (t - push - BallFall) / BallRise);
            }

            exact = s == holding;
            angle = 0f;
            pose = s == holding ? SkeletronRig.Clutch : SkeletronRig.Spread;
            return spot;
        }

        /// <summary>Where his skull's middle is, held, dribbled or crossed over, on a tick before the shot.</summary>
        private Vector2 BallOn(NPC npc, float t)
        {
            for (int k = 0; k < BallDribbles; k++)
            {
                int push = BallPush1 + k * BallDribble;

                if (t >= push && t < push + BallDribble)
                {
                    Vector2 floor = BallBounce(npc, k);
                    Vector2 left = HandBall(npc, BallFrom(npc, k), push);
                    Vector2 caught = HandBall(npc, BallTo(npc, k), push + BallDribble);

                    return t < push + BallFall
                        ? Vector2.Lerp(left, floor, EaseIn((t - push) / BallFall))
                        : Vector2.Lerp(floor, caught, EaseOut((t - push - BallFall) / BallRise));
                }
            }

            float holding = t >= BallFade ? BallSecond(npc) : t < BallPush1 + BallDribble + BallFall ? BallFirst : BallSecond(npc);
            return HandBall(npc, holding, t);
        }

        /// <summary>Where a given hand would hold his skull on a tick: toward its fingers.</summary>
        private Vector2 HandBall(NPC npc, float s, float t)
        {
            Vector2 hand = BallHand(npc, s, t, out float angle, out _, out _);
            return hand + SkeletronRig.WayOf(angle) * BallGrip(npc);
        }

        /// <summary>The velocity that takes a thing from one point to another in an arc under this gravity, in a time to suit the distance.</summary>
        internal static Vector2 Lob(Vector2 from, Vector2 to, float gravity)
        {
            Vector2 d = to - from;
            float time = MathHelper.Clamp(d.Length() / 15f, 26f, 46f);
            return new Vector2(d.X / time, d.Y / time - 0.5f * gravity * time);
        }

        // =================================================================================================================
        // CLAP
        // =================================================================================================================

        private const int ClapReach = 34;
        private const int ClapLock = 92;
        private const int ClapMeet = 102;
        private const int ClapEnd = 130;
        private const float ClapSpread = 240f;
        private const float ClapTouch = 30f;

        private void Clap(NPC npc, Player target)
        {
            float t = moveClock;
            Hover(npc, target, new Vector2(0f, -300f));

            if (t < ClapReach)
                Track(target.Center);

            if (At(ClapReach))
            {
                Fix(npc, target.Center);
                Sound(SoundID.NPCHit2 with { Pitch = -0.6f }, mark);
                Sound(SkeletronSounds.Rattle with { Volume = 0.6f }, mark);
            }

            for (int k = 0; k < 2; k++)
            {
                float side = SideOf(k);

                if (HandOn(side) == null)
                    continue;

                Aim a = Aim.At(mark + new Vector2(side * ClapSpread, 0f), SkeletronRig.AngleFor(new Vector2(-side * 0.15f, -1f)), SkeletronRig.Flat, 0.2f);
                a.Hurt = 0f;

                if (t >= ClapReach && t < ClapLock)
                {
                    float wait = (t - ClapReach) / (float)(ClapLock - ClapReach);
                    a.Gain = 1f;
                    a.Shake = 1f + 3f * wait;
                }
                else if (t >= ClapLock && t < ClapMeet)
                {
                    float p = (t - ClapLock + 1) / (float)(ClapMeet - ClapLock);
                    a.Goal = mark + new Vector2(side * MathHelper.Lerp(ClapSpread, ClapTouch, p * p * p), 0f);
                    a.Gain = 1f;
                    a.Blow = true;
                    a.Joint = true;
                    a.Hurt = 1.3f;
                }
                else if (t >= ClapMeet)
                {
                    float back = Ease((t - ClapMeet) / (float)(ClapEnd - ClapMeet));
                    a.Goal = mark + new Vector2(side * (ClapTouch + 70f * back), -40f * back);
                    a.Gain = 1f;
                }

                aims[k] = a;
            }

            // They meet: the shock of it goes up (not of a clap a parry broke: the hands never met).
            if (At(ClapMeet))
            {
                bool broken = Authority ? brokenIn == serial : LegendsSkeletronHand.Broken(npc);

                if (!broken)
                {
                    if (!Main.dedServ)
                    {
                        SoundEngine.PlaySound(SkeletronSounds.Whack with { Volume = 1f, Pitch = -0.2f }, mark);
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.8f }, mark);
                        EmpressWarp.Ripple(mark, 16f, 26f, 10f, 22);
                        PADParticleHandler.SpawnParticle(new ParryRing(mark, 10f, 120f, 16, new Color(240, 228, 190)));
                        Shake(mark, 10f, 20);

                        for (int i = 0; i < 26; i++)
                            Dust.NewDustPerfect(mark + Main.rand.NextVector2Circular(16f, 30f), DustID.Bone, Main.rand.NextVector2Circular(7f, 7f), 0, default, Main.rand.NextFloat(1f, 1.6f));
                    }

                    Shoot(npc, mark, new Vector2(0f, -ShockColumn.Speed), ModContent.ProjectileType<ShockColumn>(), Damage(npc, 16f, 14f));
                }
            }

            if (Authority && t >= ClapEnd)
                End(npc);
        }

        /// <summary>While they are locked: the way the shock will go, faint, pulsing; and the line they will close along.</summary>
        private void DrawClap(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < ClapReach || t >= ClapMeet + 4)
                return;

            float p = MathHelper.Clamp((t - ClapReach) / (float)(ClapLock - ClapReach), 0f, 1f);
            float pulse = 0.6f + 0.4f * (float)Math.Sin(t * (0.2f + 0.4f * p));
            Color bone = new Color(240, 228, 190);

            SkeletronRig.Beam(spriteBatch, mark + new Vector2(0f, 20f), mark + new Vector2(0f, -720f), bone * (0.12f * pulse * (0.4f + p)), 110f);
            SkeletronRig.Line(spriteBatch, mark + new Vector2(-ClapSpread, 0f), mark + new Vector2(ClapSpread, 0f), bone * (0.2f + 0.3f * p), 1f + p);
        }

        // =================================================================================================================
        // TORNADO
        // =================================================================================================================

        private const int TornadoTaunt = 44;
        private const int TornadoSpin = 150;
        private const int TornadoDown = 40;
        private const int TornadoEnd = TornadoTaunt + TornadoSpin + TornadoDown;
        private const float TornadoReach = 224f;

        /// <summary>How far his arms have gone round, this many ticks into the spin: quicker and quicker, then a steady whirl.</summary>
        private static float Spun(float ticks)
        {
            if (ticks <= 0f)
                return 0f;

            if (ticks <= 30f)
                return 0.05f * ticks + 0.25f * ticks * ticks / 60f;

            if (ticks <= TornadoSpin)
                return 0.3f * ticks - 3.75f;

            // Slowing to a stop after it.
            float end = 0.3f * TornadoSpin - 3.75f;
            float u = MathHelper.Clamp((ticks - TornadoSpin) / TornadoDown, 0f, 1f);
            return end + 0.3f * TornadoDown * (u - u * u / 2f);
        }

        private void Tornado(NPC npc, Player target)
        {
            float t = moveClock;
            float spinning = t - TornadoTaunt;

            if (t < TornadoTaunt)
                Hover(npc, target, new Vector2(0f, -240f), 0.03f, 4f);
            else if (t < TornadoTaunt + TornadoSpin)
                Approach(npc, target.Center + new Vector2(0f, -150f), 0.03f, 4.5f);
            else
                npc.velocity *= 0.9f;

            eyesWant = t >= TornadoTaunt && t < TornadoTaunt + TornadoSpin ? 0.8f : 0f;

            if (At(6))
                Sound(SkeletronSounds.Cackle, npc.Center);

            // Both birds up: a power chord on it.
            if (At(22))
                Sound(SkeletronSounds.Stab with { Volume = 0.8f }, npc.Center);

            if (At(TornadoTaunt))
            {
                Sound(SkeletronSounds.Tornado, npc.Center);

                for (int k = 0; k < 2; k++)
                {
                    if (hand[k] >= 0)
                        Shoot(npc, npc.Center, Vector2.Zero, ModContent.ProjectileType<SkeletronHitbox>(), Damage(npc, 16f, 14f), npc.whoAmI, k == 0 ? SkeletronHitbox.ArmLeft : SkeletronHitbox.ArmRight, serial);
                }
            }

            for (int k = 0; k < 2; k++)
            {
                float side = SideOf(k);

                if (HandOn(side) == null)
                    continue;

                Aim a;

                if (t < TornadoTaunt)
                {
                    // Up beside him, flipping the bird.
                    Vector2 up = npc.Center + new Vector2(side * 150f, -150f);
                    a = Aim.At(up, MathHelper.Pi, SkeletronRig.Bird, 0.15f);
                    a.Hurt = 0.6f;
                    a.Shake = t > 10 ? 1f : 0f;
                }
                else if (t < TornadoEnd - 14)
                {
                    float angle = (side < 0f ? MathHelper.Pi : 0f) + Spun(spinning);
                    Vector2 shoulder = ShoulderOf(npc, side);
                    Vector2 at = shoulder + angle.ToRotationVector2() * TornadoReach * npc.scale;

                    // Onto the circle from the taunt, a few ticks' glide; then held to it.
                    float gain = spinning < 8 ? 0.35f : t < TornadoTaunt + TornadoSpin ? 1f : 0.4f;
                    a = Aim.At(at, SkeletronRig.AngleFor(at - shoulder), SkeletronRig.Bird, gain, spinning < 8 ? 40f : 22f);
                    a.Turn = 1f;
                    a.Hurt = 1.2f;
                    a.Arm = ArmStyle.Straight;
                }
                else
                {
                    a = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.18f);
                    a.Hurt = 0f;
                }

                aims[k] = a;
            }

            // The wind off him.
            if (!Main.dedServ && t >= TornadoTaunt && t < TornadoTaunt + TornadoSpin && Main.GameUpdateCount % 2 == 0)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 at = npc.Center + angle.ToRotationVector2() * Main.rand.NextFloat(80f, 240f);
                Vector2 round = (angle + MathHelper.PiOver2).ToRotationVector2();

                PADParticleHandler.SpawnParticle(new WindStreak(at, round * 14f, Main.rand.NextFloat(40f, 90f), 2.5f, 12));

                Dust dust = Dust.NewDustPerfect(at, DustID.Bone, round * 8f, 120, default, 1.1f);
                dust.noGravity = true;
            }

            if (Authority && t >= TornadoEnd)
                End(npc);
        }

        // =================================================================================================================
        // SOLO
        // =================================================================================================================

        private const int SoloSet = 30;
        private const int SoloOpen = 46;
        private const int SoloFirstShot = 50;
        private const int SoloLastShot = 190;
        private const int SoloShotEvery = 10;
        private const int SoloEnding = 196;
        private const int SoloEnd = 236;

        /// <summary>How far down his head is banged this tick (on this machine), so the next tick moves it only by the change.</summary>
        private float soloBang;

        private void Solo(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            float other = -side;

            Hover(npc, target, new Vector2(0f, -280f), 0.025f, 3f);

            // The skull open, in time; shut as it ends.
            float open = t < SoloSet ? 0f : t < SoloOpen ? EaseOut((t - SoloSet) / 16f) : t < SoloEnding + 30 ? 1f : 1f - Ease((t - SoloEnding - 30) / 10f);
            float since = (t - SoloFirstShot) % SoloShotEvery;
            float kick = t >= SoloFirstShot && t <= SoloLastShot + 4 ? 1f - since / (float)SoloShotEvery : 0f;

            liftWant = (24f + 4f * kick) * open;
            jawWant = (20f + 4f * kick) * open;
            hatWant = 10f * open;
            eyesWant = open;
            faceRate = 0.6f;

            // His head banging with every strum, harder in the finish: thrown down on the beat and coming back up (on top of
            // his hover, which would undo a push of his speed by the next tick).
            float bang = kick > 0f ? 16f * kick * kick
                : t >= SoloEnding && t < SoloEnding + 30 ? 26f * (float)Math.Pow(1f - (t - SoloEnding) % 10f / 10f, 2f) : 0f;
            npc.position.Y += bang - soloBang;
            soloBang = bang;

            // The middle finger, close by him.
            Aim bird = Aim.At(npc.Center + new Vector2(side * 82f, 24f), MathHelper.Pi, SkeletronRig.Bird, 0.25f);
            bird.Hurt = 0.6f;
            bird.Shake = 0.8f;

            if (t >= SoloEnd - 14)
            {
                bird.Goal = Rest(npc, side);
                bird.Pose = SkeletronRig.Relaxed;
                bird.Angle = 0f;
                bird.Gain = 0.15f;
            }

            aims[IndexOf(side)] = bird;

            // The air guitar, strummed in time; rock on at the end.
            if (HandOn(other) != null)
            {
                Aim guitar = Aim.At(npc.Center + new Vector2(other * 96f, 84f + 16f * kick), SkeletronRig.AngleFor(new Vector2(side * 0.7f, 0.75f)), SkeletronRig.Strum, 0.4f);
                guitar.Hurt = 0.6f;

                if (t >= SoloEnding)
                {
                    guitar.Goal = npc.Center + new Vector2(other * 120f, -70f);
                    guitar.Angle = MathHelper.Pi;
                    guitar.Pose = SkeletronRig.Horns;
                    guitar.Gain = 0.25f;
                    guitar.Shake = 1.5f;
                }

                if (t >= SoloEnd - 14)
                {
                    guitar.Goal = Rest(npc, other);
                    guitar.Pose = SkeletronRig.Relaxed;
                    guitar.Angle = 0f;
                    guitar.Gain = 0.15f;
                }

                aims[IndexOf(other)] = guitar;
            }

            // The bird up close by him: a power chord on it, before the solo.
            if (At(16))
                Sound(SkeletronSounds.Stab with { Volume = 0.8f }, npc.Center);

            if (At(SoloOpen))
                Sound(SkeletronSounds.Riff1, npc.Center);

            if (At(SoloOpen + 50))
                Sound(SkeletronSounds.Riff2, npc.Center);

            if (At(SoloOpen + 100))
                Sound(SkeletronSounds.Riff3, npc.Center);

            if (At(SoloEnding))
            {
                Sound(SkeletronSounds.RiffEnd, npc.Center);

                if (!Main.dedServ)
                {
                    EmpressWarp.Ripple(npc.Center, 14f, 30f, 8f, 26);
                    Shake(npc.Center, 5f, 20);
                }
            }

            // A skull spat out with every strum, fanned, homing.
            for (int shot = SoloFirstShot; shot <= SoloLastShot; shot += SoloShotEvery)
            {
                if (!At(shot))
                    continue;

                int index = (shot - SoloFirstShot) / SoloShotEvery;
                Vector2 mouth = MouthOf(npc);
                float fan = (index % 5 - 2) * 0.16f;
                Vector2 velocity = (target.Center - mouth).SafeNormalize(Vector2.UnitY).RotatedBy(fan) * 9f;

                Shoot(npc, mouth, velocity, ModContent.ProjectileType<SkeletronSkull>(), Damage(npc, 13f, 11f), SkeletronSkull.Spat, index);

                if (!Main.dedServ)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Dust dust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(12f, 4f), DustID.Bone, velocity * 0.4f + Main.rand.NextVector2Circular(2f, 2f), 80, default, 1.1f);
                        dust.noGravity = true;
                    }
                }
            }

            if (Authority && t >= SoloEnd)
                End(npc);
        }

        /// <summary>The solo: notes coming off the guitar hand with every strum, his eyes lit with it.</summary>
        private void DrawSolo(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < SoloFirstShot || t > SoloEnding + 30)
                return;

            NPC guitar = HandOn(-actSide);

            if (guitar == null)
                return;

            float since = (t - SoloFirstShot) % SoloShotEvery;
            float flash = 1f - since / (float)SoloShotEvery;
            Color note = Color.Lerp(new Color(255, 80, 60), new Color(255, 220, 120), ((int)t / 10) % 2);

            SkeletronRig.Glow(spriteBatch, guitar.Center + new Vector2(0f, 20f), note * (0.35f * flash), 60f);
            SkeletronRig.SoftRing(spriteBatch, guitar.Center + new Vector2(0f, 20f), 20f + 40f * (1f - flash), note * (0.4f * flash), 4f, 24);
        }

        // =================================================================================================================
        // COMBO
        // =================================================================================================================

        private const int ComboPunch = 24;
        private const int ComboHit = 34;
        private const int ComboRecoil = 42;
        private const int ComboWhack = 54;
        private const int ComboWhackHit = 66;
        private const int ComboBump = 78;
        private const int ComboBumpHit = 96;
        private const int ComboRest = 110;
        private const int ComboEnd = 136;
        private const int ComboEndOne = 104;

        private void Combo(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            bool both = handCount >= 2;
            float whacker = both ? -side : side;

            Hover(npc, target, new Vector2(0f, -290f), 0.04f, 5f);

            if (At(ComboPunch))
                Fix(npc, target.Center);

            if (At(ComboRecoil))
                FixSecond(npc, target.Center);

            // THE PUNCH.
            Vector2 start = npc.Center + new Vector2(side * 130f, 40f);
            Vector2 toward = (mark - start).SafeNormalize(new Vector2(side, 0f));
            Vector2 end = mark + toward * 80f;
            end.Y = Math.Min(end.Y, FloorBelow(end.X, 20f, mark.Y - 40f) - 30f);

            if (t < ComboWhack || !both)
            {
                Aim punch;

                if (t < ComboPunch)
                {
                    Vector2 aimAt = target.Center;
                    punch = Aim.At(start - (aimAt - start).SafeNormalize(Vector2.UnitY) * 30f, SkeletronRig.AngleFor(aimAt - start), SkeletronRig.Fist, 0.3f);
                    punch.Hurt = 0.6f;
                    punch.Shake = 1f;
                }
                else if (t < ComboRecoil)
                {
                    float p = EaseIn((t - ComboPunch) / (float)(ComboHit - ComboPunch));
                    punch = Aim.At(Vector2.Lerp(start, end, p), SkeletronRig.AngleFor(toward), SkeletronRig.Fist, 1f);
                    punch.Turn = 0.6f;
                    punch.Blow = true;
                    punch.Hurt = 1.4f;
                    punch.Push = toward * 10f + new Vector2(0f, -3f);
                }
                else
                {
                    punch = Aim.At(end - toward * 70f, SkeletronRig.AngleFor(toward), SkeletronRig.Fist, 0.25f);
                    punch.Hurt = 0f;
                }

                if (t < ComboWhack || both)
                    aims[IndexOf(side)] = punch;
            }

            if (At(ComboHit) && !LegendsSkeletronHand.Turned(HandOn(side)))
            {
                Sound(SkeletronSounds.Punch, end);

                if (!Main.dedServ)
                {
                    PADParticleHandler.SpawnParticle(new ParryRing(end, 6f, 60f, 12, new Color(255, 240, 200)));
                    Shake(end, 4f, 8);
                }
            }

            // THE BACKHAND: the other hand (or the same, alone), across where they were.
            if (t >= ComboRecoil && t < ComboBump)
            {
                Aim whack;

                if (t < ComboWhack)
                {
                    Vector2 from = Sweep(mark2, whacker, 0f, out _);
                    whack = Aim.At(from, SkeletronRig.AngleFor(new Vector2(-whacker, 0.3f)), SkeletronRig.Flat, 0.3f);
                    whack.Hurt = 0f;
                }
                else
                {
                    float u = Smoother((t - ComboWhack) / (float)(ComboBump - ComboWhack));
                    Vector2 at = Sweep(mark2, whacker, u, out Vector2 way);
                    whack = Aim.At(at, SkeletronRig.AngleFor(way), SkeletronRig.Flat, 1f);
                    whack.Turn = 0.5f;
                    whack.Blow = true;
                    whack.Hurt = 1.3f;
                    whack.Push = new Vector2(-whacker * 10f, -4f);
                }

                aims[IndexOf(whacker)] = whack;
            }

            if (At(ComboWhackHit) && !LegendsSkeletronHand.Turned(HandOn(whacker)))
                Sound(SkeletronSounds.Whack, mark2);

            // THE FIST BUMP: both fists together where they are now.
            if (both && At(ComboBump))
                Fix(npc, target.Center);

            if (both && t >= ComboBump)
            {
                for (int k = 0; k < 2; k++)
                {
                    float s = SideOf(k);
                    Aim bump;

                    if (t < ComboRest)
                    {
                        float p = MathHelper.Clamp((t - ComboBump) / (float)(ComboBumpHit - ComboBump), 0f, 1f);
                        float apart = MathHelper.Lerp(220f, 26f, p * p * p);

                        // Glided out to either side of them first (from wherever the punch and the backhand left them), then shut.
                        bump = Aim.At(mark + new Vector2(s * apart, 0f), SkeletronRig.AngleFor(new Vector2(-s, 0f)), SkeletronRig.Fist,
                            t < ComboBump + 8 ? 0.35f : 1f, t < ComboBump + 8 ? 40f : 22f);
                        bump.Turn = 0.4f;

                        // (Gliding out to either side first: through them, maybe, and harmless till it shuts.)
                        bool shutting = t >= ComboBump + 8 && t < ComboBumpHit + 4;
                        bump.Blow = shutting;
                        bump.Joint = true;
                        bump.Hurt = shutting ? 1.5f : 0f;
                    }
                    else
                    {
                        bump = Aim.At(Rest(npc, s), RestAngle(npc, s), Hanging(s), 0.15f);
                        bump.Hurt = 0f;
                    }

                    aims[k] = bump;
                }

                if (At(ComboBumpHit))
                {
                    bool broken = Authority ? brokenIn == serial : LegendsSkeletronHand.Broken(npc);

                    if (!broken)
                    {
                        if (!Main.dedServ)
                        {
                            SoundEngine.PlaySound(SkeletronSounds.Bump, mark);
                            PADParticleHandler.SpawnParticle(new CrownGlint(mark + new Vector2(0f, -30f), 80f, 24, new Color(255, 250, 220)));
                            PADParticleHandler.SpawnParticle(new ParryRing(mark, 8f, 90f, 14, new Color(240, 228, 190)));
                            Shake(mark, 6f, 12);
                        }

                        Shoot(npc, mark, Vector2.Zero, ModContent.ProjectileType<SkeletronBlast>(), Damage(npc, 14f, 12f), 64f, SkeletronBlast.Bone);
                    }
                }
            }
            else if (!both && t >= ComboBump)
            {
                Aim back = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.15f);
                back.Hurt = 0f;
                aims[IndexOf(side)] = back;
            }

            if (Authority && t >= (both ? ComboEnd : ComboEndOne))
                End(npc);
        }

        // =================================================================================================================
        // WAG
        // =================================================================================================================

        private const int WagFrom = 24;
        private const int WagUntil = 120;
        private const int WagEnd = 140;
        private const int WagSlapFrom = 60;
        private const int WagSlap = 80;
        private const int WagSlapHit = 92;
        private const int WagSlapEnd = 104;

        private void Wag(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            float other = -side;
            bool wagging = t >= WagFrom && t < WagUntil;

            Hover(npc, target, new Vector2(0f, -280f), 0.03f, 4f);

            // No, no, no: and whatever is shot at the finger meanwhile turned back (his skull, locked, the game never asks).

            float swing = wagging ? 0.5f * (float)Math.Sin((t - WagFrom) * 0.25f) : 0f;
            Aim finger = Aim.At(npc.Center + new Vector2(side * 130f, -70f), MathHelper.Pi + swing, SkeletronRig.Wag, 0.2f);
            finger.Turn = 0.5f;
            finger.Hurt = 0.6f;
            finger.Reflect = wagging;

            if (t >= WagEnd - 16)
            {
                finger.Goal = Rest(npc, side);
                finger.Angle = 0f;
                finger.Pose = SkeletronRig.Relaxed;
                finger.Gain = 0.15f;
                finger.Turn = 0.2f;
            }

            aims[IndexOf(side)] = finger;

            // Tsk at each end of the wag.
            for (int k = 0; k < 8; k++)
            {
                int when = WagFrom + (int)Math.Round(6.283f + 12.566f * k);

                if (when < WagUntil && At(when))
                    Sound(SkeletronSounds.Tsk with { Pitch = k % 2 == 0 ? 0f : 0.12f }, npc.Center);
            }

            // And the other hand slaps.
            if (HandOn(other) != null)
            {
                if (At(WagSlapFrom))
                    FixSecond(npc, target.Center);

                Aim slap;

                if (t < WagSlapFrom)
                {
                    Idle(npc, other);
                    slap = aims[IndexOf(other)];
                    slap.Hurt = 0f;
                }
                else if (t < WagSlap)
                {
                    slap = Aim.At(Sweep(mark2, other, 0f, out _), SkeletronRig.AngleFor(new Vector2(-other, 0.3f)), SkeletronRig.Flat, 0.3f);
                    slap.Hurt = 0f;
                }
                else if (t < WagSlapEnd)
                {
                    float u = Smoother((t - WagSlap) / (float)(WagSlapEnd - WagSlap));
                    Vector2 at = Sweep(mark2, other, u, out Vector2 way);
                    slap = Aim.At(at, SkeletronRig.AngleFor(way), SkeletronRig.Flat, 1f);
                    slap.Turn = 0.5f;
                    slap.Blow = true;
                    slap.Hurt = 1.3f;
                    slap.Push = new Vector2(-other * 10f, -4f);
                }
                else
                {
                    slap = Aim.At(Rest(npc, other), RestAngle(npc, other), Hanging(other), 0.15f);
                    slap.Hurt = 0f;
                }

                aims[IndexOf(other)] = slap;

                if (At(WagSlapHit) && !LegendsSkeletronHand.Turned(HandOn(other)))
                    Sound(SkeletronSounds.Whack, mark2);
            }

            if (Authority && t >= WagEnd)
                End(npc);
        }

        /// <summary>The wag: a shimmer round him and the finger while what is shot at him is turned back.</summary>
        private void DrawWag(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < WagFrom || t >= WagUntil)
                return;

            float fade = Ease((t - WagFrom) / 10f) * (1f - Ease((t - WagUntil + 10) / 10f));
            float pulse = 0.7f + 0.3f * (float)Math.Sin(t * 0.5f);
            Color ward = new Color(230, 240, 255);

            NPC finger = HandOn(actSide);

            if (finger == null)
                return;

            SkeletronRig.SoftRing(spriteBatch, finger.Center, 62f * finger.scale, ward * (0.3f * fade * pulse), 6f, 36);
            SkeletronRig.Glow(spriteBatch, TipOf(npc, actSide, SkeletronRig.Digit.Index), ward * (0.5f * fade * pulse), 18f);
        }

        // =================================================================================================================
        // FLICK
        // =================================================================================================================

        private const int FlickCharge = 32;
        private const int FlickTrack = 62;
        private const int FlickHitAt = 72;
        private const int FlickHitTicks = 8;
        private const int FlickEnd = 110;

        private void Flick(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            float fromSide = target.Center.X >= npc.Center.X ? -1f : 1f;

            Hover(npc, target, new Vector2(0f, -320f), 0.03f, 4f);

            // Fixed: them, and the spot beside them the finger flicks from (its way from there to them: FlickWay).
            if (At(FlickTrack))
            {
                Fix(npc, target.Center);
                FixSecond(npc, target.Center + new Vector2(fromSide * 118f, -60f));
            }

            Vector2 spot = t < FlickTrack ? target.Center + new Vector2(fromSide * 118f, -60f) : mark2;
            Vector2 way = t >= FlickTrack ? FlickWay() : (target.Center - HandAt(npc, side)).SafeNormalize(new Vector2(-fromSide, 0f));
            Aim a = Aim.At(spot, SkeletronRig.AngleFor(way), SkeletronRig.FlickReady, t < FlickCharge ? 0.18f : 0.3f);
            a.Hurt = 0f;

            if (t >= FlickCharge && t < FlickHitAt)
            {
                a.Bypass = true;
                a.Shake = 0.8f;
            }
            else if (t >= FlickHitAt && t < FlickHitAt + FlickHitTicks)
            {
                a.Goal = mark2 + way * 70f;
                a.Gain = 1f;
                a.Pose = SkeletronRig.FlickOut;
                a.Shape = 1f;
                a.Turn = 1f;
                a.Bypass = true;
                a.Hurt = 1.1f;
                a.Push = way * 16f + new Vector2(0f, -5f);
            }
            else if (t >= FlickHitAt + FlickHitTicks)
            {
                a.Goal = Rest(npc, side);
                a.Angle = RestAngle(npc, side);
                a.Pose = SkeletronRig.Relaxed;
                a.Gain = 0.12f;
            }

            aims[IndexOf(side)] = a;

            if (At(FlickCharge))
                Sound(SkeletronSounds.FlickCharge, spot);

            if (At(FlickHitAt))
            {
                Shoot(npc, npc.Center, Vector2.Zero, ModContent.ProjectileType<SkeletronHitbox>(), Damage(npc, 18f, 15f), npc.whoAmI, SkeletronHitbox.FlickTip, serial);

                if (!Main.dedServ)
                {
                    Vector2 tip = mark2 + way * 60f;
                    SoundEngine.PlaySound(SkeletronSounds.Flick, tip);
                    PADParticleHandler.SpawnParticle(new SlashStreak(tip + way * 40f, way, 70f, 10f, 12));
                    PADParticleHandler.SpawnParticle(new ParryRing(tip, 6f, 70f, 12, CCBypass.Warning));
                    EmpressWarp.Ripple(tip, 14f, 20f, 7f, 18);
                    Shake(tip, 6f, 12);
                }
            }

            if (Authority && t >= FlickEnd)
                End(npc);
        }

        /// <summary>The way the flick goes, once fixed: from the spot beside them to them.</summary>
        private Vector2 FlickWay() => (mark - mark2).SafeNormalize(Vector2.UnitX);

        /// <summary>The flick's tell: a yellow line from the finger through them, the way they will go, and a ring where it lands.</summary>
        private void DrawFlickLine(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < FlickCharge || t >= FlickHitAt + 2)
                return;

            NPC finger = HandOn(actSide);

            if (finger == null)
                return;

            Player target = Target(npc);
            Vector2 to = t < FlickTrack ? target.Center : mark;
            Vector2 from = finger.Center;
            Vector2 way = (to - from).SafeNormalize(Vector2.UnitX);
            Vector2 end = to + way * 150f;

            float p = MathHelper.Clamp((t - FlickCharge) / (float)(FlickHitAt - FlickCharge), 0f, 1f);
            float pulse = 0.6f + 0.4f * (float)Math.Sin(t * (0.3f + 0.6f * p));
            Color warn = CCBypass.Warning;

            SkeletronRig.Beam(spriteBatch, from + way * 40f, end, warn * (0.25f + 0.35f * p * pulse), 6f + 6f * p);

            // The arrowhead.
            Vector2 back = -way * 26f;
            SkeletronRig.Line(spriteBatch, end, end + back.RotatedBy(0.5f), warn * (0.5f + 0.4f * pulse), 3f);
            SkeletronRig.Line(spriteBatch, end, end + back.RotatedBy(-0.5f), warn * (0.5f + 0.4f * pulse), 3f);

            SkeletronRig.Ring(spriteBatch, to, 44f * (1.3f - 0.3f * p), warn * (0.35f + 0.3f * pulse), 2f, 36);
        }
    }
}
