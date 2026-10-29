using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- his hands' moves, the rest (LegendsSkeletron.HandMoves has the first seven) ------------------------------------
    //
    //     WHIP         the hands shake; then one tears the other's arm off at the shoulder and cracks it like a whip
    //                  at the player (hard; not through crowd control); it goes back on
    //     BOWLING      the hands take his skull, swing it back and bowl it along the ground at the player: a strike
    //     SEALS        hand signs, five, quick; smoke round the player, and skulls out of it that come for them
    //     SWOOP        a hand comes down round in a curve to pick the player up; caught, it pinches them by the head
    //                  (thumb and finger, an ok), throws them up, and the other hand tries to punch them on the way
    //     PURPLE       open hands raised high either side, glowing purple; down they come from both sides; caught, the
    //                  player is brought to his mouth and burned point blank with a Diabolist's fire
    //   his own three:
    //     GUNS         finger guns: pew, pew, bones out of the fingertips; the smoke blown off them, and a twirl
    //     SNAP         a snap of the fingers, and bone spikes burst up out of the ground in a line along to the player
    //     RPS          rock, paper, scissors: jan, ken, pon; rock comes down on them, paper pushes a wall of air at
    //                  them, scissors snips where they stand

    public partial class LegendsSkeletron
    {
        // ---- held ---------------------------------------------------------------------------------------------------------

        /// <summary>The player his hands hold, held there this tick (on every machine with a screen; their own moves them), and their camera close.</summary>
        private void HoldGrabbed(NPC npc, Vector2 at)
        {
            if (grabbed < 0 || grabbed >= Main.maxPlayers)
                return;

            Player held = Main.player[grabbed];

            if (!held.active || held.dead)
            {
                if (Authority)
                {
                    grabbed = -1;
                    npc.netUpdate = true;
                }

                return;
            }

            if (!Main.dedServ)
                held.GetModPlayer<SkeletronPlayer>().Hold(at);

            if (grabbed == Main.myPlayer)
            {
                PisPlayer.BlendCamera(Vector2.Lerp(npc.Center, held.Center, 0.6f), 0.85f);
                Cinematic.Frame();
                Cinematic.Lean(Math.Max(1f, 1.32f / Math.Max(1f, Main.GameZoomTarget)), 0.08f);
            }
        }

        /// <summary>
        /// The held player let go, thrown (on their own machine). The Everclear Mirror's copy, if it is
        /// the one held, on its owner's, as if it were the one at the keyboard (MimicSystem.AlsoOnCopy).
        /// </summary>
        private void LetGo(Vector2 velocity)
        {
            ThrowHeld(velocity);
            MimicSystem.AlsoOnCopy(() => ThrowHeld(velocity));
        }

        /// <summary><see cref="LetGo"/>, for whoever is this machine's player for the moment.</summary>
        private void ThrowHeld(Vector2 velocity)
        {
            if (grabbed == Main.myPlayer && grabbed >= 0)
                Main.LocalPlayer.GetModPlayer<SkeletronPlayer>().Throw(velocity);
        }

        /// <summary>
        /// The server's: whoever this hand has hold of, or -1: its box where it is and where it is going this tick (so the grab
        /// comes before its touch would make them immune), a little larger. Not a hand a parry turned.
        /// </summary>
        private static int Catch(NPC hand, Vector2 going, int inflate)
        {
            if (hand == null || SkeletronHandParry.Spent(hand))
                return -1;

            // Where it is going this tick, and along its way there, as wide as it is and a little more.
            Rectangle ahead = new Rectangle((int)(going.X - hand.width * 0.5f), (int)(going.Y - hand.height * 0.5f), hand.width, hand.height);
            ahead.Inflate(inflate, inflate);

            float reach = Math.Max(hand.width, hand.height) + inflate * 2f;
            bool moving = Vector2.DistanceSquared(hand.Center, going) > 1f;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                float ignored = 0f;

                if (!p.active || p.dead || p.immune)
                    continue;

                if (p.Hitbox.Intersects(ahead) || moving && Collision.CheckAABBvLineCollision(p.position, p.Size, hand.Center, going, reach, ref ignored))
                    return i;
            }

            return -1;
        }

        /// <summary>The server's: caught; on from the tick after this one at <paramref name="beatFrom"/>.</summary>
        private void Caught(NPC npc, int who, int beatFrom, Vector2 at)
        {
            grabbed = who;
            mark2 = at;
            moveTimer = beatFrom - 1;
            npc.netUpdate = true;
        }

        // =================================================================================================================
        // WHIP
        // =================================================================================================================

        private const int WhipShake = 30;
        private const int WhipRip = 66;
        private const int WhipTorn = 80;
        private const int WhipLimp = 10;
        private const int WhipWind = 94;
        private const int WhipCrack = 118;
        private const int WhipCracked = 134;
        private const int WhipBack = 144;
        private const int WhipOn = 164;
        private const int WhipEnd = 174;

        /// <summary>
        /// The torn-off arm as a whip, from the holder's hand at its shoulder end: its two bones (upper arm, forearm), each
        /// at its own angle and each its own length, never stretched. Torn off as it hung, it drops limp and sways under
        /// the holder; swung back and up behind him, the forearm trailing; then over the top and down through them, the
        /// upper arm leading and the forearm lagging until it snaps through straight at the end (the crack); then a kick
        /// back off them.
        /// </summary>
        private void WhipBones(NPC npc, float t, Vector2 handle, float dir, float other, out Vector2 elbow, out Vector2 wrist)
        {
            float upper = SkeletronRig.UpperArm * npc.scale;
            float lower = SkeletronRig.Forearm * npc.scale;
            float hang = MathHelper.PiOver2;
            float wound = MathHelper.PiOver2 + dir * 0.8f * MathHelper.Pi;
            float trail = wound - dir * 0.7f;

            // Down through where they stood, from where the holder drives it (so the crack ends on them), over the top:
            // unwound so it turns the long way round, over his head.
            float strike = (mark - WhipStrikeSpot(dir)).ToRotation();

            while ((strike - wound) * dir < 0f)
                strike += dir * MathHelper.TwoPi;

            while ((strike - wound) * dir > MathHelper.TwoPi)
                strike -= dir * MathHelper.TwoPi;

            float a1;
            float a2;

            if (t < WhipWind)
            {
                float sway = 0.18f * (float)Math.Sin((t - WhipTorn) * 0.35f);
                a1 = hang + other * 0.12f + sway;
                a2 = hang + sway * 1.8f;

                // Just torn off: from the way it hung on him, dropping limp.
                if (t < WhipTorn + WhipLimp)
                {
                    Vector2 shoulder = ShoulderOf(npc, other);
                    Vector2 restWrist = SkeletronRig.WristFromMiddle(Rest(npc, other), RestAngle(npc, other), other > 0f, npc.scale);
                    Vector2 restElbow = SkeletronRig.Elbow(shoulder, restWrist, other, npc.scale);
                    float u = Ease((t - WhipTorn) / WhipLimp);

                    a1 = (restElbow - shoulder).ToRotation().AngleLerp(a1, u);
                    a2 = (restWrist - restElbow).ToRotation().AngleLerp(a2, u);
                }
            }
            else if (t < WhipCrack)
            {
                float u = (t - WhipWind) / (WhipCrack - WhipWind);
                a1 = MathHelper.Lerp(hang, wound, EaseOut(u));
                a2 = MathHelper.Lerp(hang, trail, Ease(u));
            }
            else if (t < WhipCracked)
            {
                float u = (t - WhipCrack + 1) / (WhipCracked - WhipCrack);
                a1 = MathHelper.Lerp(wound, strike, Ease(u));
                a2 = MathHelper.Lerp(trail, strike, u * u * u);
            }
            else
            {
                // Off them and settling: kicked back up off them, and down again.
                float u = Math.Min(1f, (t - WhipCracked) / (WhipBack - WhipCracked));
                a1 = strike;
                a2 = strike - dir * 0.35f * (float)Math.Sin(u * MathHelper.Pi) * (1f - u);
            }

            elbow = handle + a1.ToRotationVector2() * upper;
            wrist = elbow + a2.ToRotationVector2() * lower;
        }

        /// <summary>Where the holder drives the whip to: the whip's full length just reaches past them from there.</summary>
        private Vector2 WhipStrikeSpot(float dir) => mark + new Vector2(-dir * 150f, -160f);

        /// <summary>
        /// The holder's hand on its path, the whip's handle once it has it: from the handshake up to the other arm's
        /// shoulder, where it takes hold and tears it off; up and out with it; back over his head; then driven at them.
        /// </summary>
        private Vector2 WhipHandle(NPC npc, float t, float side, float other, float dir)
        {
            Vector2 shook = npc.Center + new Vector2(side * 22f, 130f);
            Vector2 shoulder = ShoulderOf(npc, other);
            Vector2 lifted = shoulder + new Vector2(other * 40f, -40f);
            Vector2 back = npc.Center + new Vector2(-dir * 70f, -50f);

            if (t < WhipTorn)
                return Vector2.Lerp(shook, shoulder, Ease((t - WhipRip) / (WhipTorn - WhipRip - 2)));

            if (t < WhipWind)
                return Vector2.Lerp(shoulder, lifted, EaseOut((t - WhipTorn) / (WhipWind - WhipTorn)));

            if (t < WhipCrack)
                return Vector2.Lerp(lifted, back, Ease((t - WhipWind) / (WhipCrack - WhipWind)));

            return Vector2.Lerp(back, WhipStrikeSpot(dir), EaseOut((t - WhipCrack + 1) / (WhipCracked - WhipCrack)));
        }

        private void Whip(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            float other = -side;

            if (t < WhipWind)
                npc.velocity *= 0.85f;
            else
                Hover(npc, target, new Vector2(0f, -260f), 0.03f, 4f);

            if (At(WhipWind))
                Fix(npc, target.Center);

            float dir = mark.X >= npc.Center.X ? 1f : -1f;
            Vector2 meet = npc.Center + new Vector2(0f, 130f);
            float pump = t >= WhipShake && t < WhipRip ? 16f * (float)Math.Sin((t - WhipShake) / 12f * MathHelper.TwoPi) : 0f;

            Aim holder;
            Aim whip;

            if (t < WhipRip)
            {
                // Shaking hands.
                holder = Aim.At(meet + new Vector2(side * 22f, pump), SkeletronRig.AngleFor(new Vector2(-side, 0.15f)), SkeletronRig.Grab, 0.3f);
                whip = Aim.At(meet + new Vector2(other * 22f, pump), SkeletronRig.AngleFor(new Vector2(-other, 0.15f)), SkeletronRig.Grab, 0.3f);
                holder.Hurt = whip.Hurt = 0f;
            }
            else if (t < WhipBack)
            {
                // The holder, placed on its path (its own arm off his body as far as it takes): to the other shoulder,
                // taking hold of the arm there and tearing it off, then the whip's handle.
                Vector2 handle = WhipHandle(npc, t, side, other, dir);
                Vector2 toward = t < WhipTorn ? ShoulderOf(npc, other) - handle : t < WhipCrack ? new Vector2(0f, 1f) : new Vector2(dir, 0.6f);

                holder = Aim.At(handle, SkeletronRig.AngleFor(toward), t < WhipTorn - 6 ? SkeletronRig.Grab : SkeletronRig.Clutch, 1f);
                holder.Turn = 0.4f;
                holder.Hurt = 0f;

                if (t < WhipTorn)
                {
                    // Let go of, it hangs from him as ever, about to lose its arm.
                    whip = Aim.At(Rest(npc, other), RestAngle(npc, other), SkeletronRig.Relaxed, 0.2f);
                    whip.Hurt = 0f;
                }
                else
                {
                    WhipBones(npc, t, handle, dir, other, out Vector2 elbow, out Vector2 wrist);
                    Vector2 way = wrist - elbow;
                    float angle = SkeletronRig.AngleFor(way);
                    Vector2 middle = wrist - (SkeletronRig.Wrist - SkeletronRig.HandMiddle).RotatedBy(angle) * npc.scale;
                    bool cracking = t >= WhipCrack;

                    whip = Aim.At(middle, angle, cracking ? SkeletronRig.Spread : SkeletronRig.Relaxed, 1f);
                    whip.Turn = 1f;
                    whip.HasRoot = true;
                    whip.Root = handle;
                    whip.Arm = ArmStyle.Whip;
                    whip.Bend = elbow;

                    if (cracking && t < WhipCracked + 2)
                    {
                        whip.Blow = true;
                        whip.Hurt = 2f;
                        whip.Push = new Vector2(dir * 12f, -4f);
                    }
                    else
                    {
                        whip.Hurt = 0f;
                    }
                }
            }
            else
            {
                // Back on: the arm's top carried home to his shoulder in the holder's hand, the hand going back to its spot.
                Vector2 home = Vector2.Lerp(WhipStrikeSpot(dir), ShoulderOf(npc, other), Ease((t - WhipBack) / (WhipOn - WhipBack)));

                holder = t < WhipOn
                    ? Aim.At(home, SkeletronRig.AngleFor(ShoulderOf(npc, other) - home), SkeletronRig.Clutch, 0.4f, 30f)
                    : Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.2f);
                holder.Hurt = 0f;

                whip = Aim.At(Rest(npc, other), RestAngle(npc, other), SkeletronRig.Relaxed, 0.2f);
                whip.Hurt = 0f;

                if (t < WhipOn)
                {
                    whip.HasRoot = true;
                    whip.Root = HandAt(npc, side);
                }
            }

            aims[IndexOf(side)] = holder;
            aims[IndexOf(other)] = whip;

            if (At(WhipShake))
                Sound(SkeletronSounds.Handshake, meet);

            if (At(WhipTorn))
            {
                Vector2 shoulder = ShoulderOf(npc, other);
                Sound(SoundID.NPCHit2 with { Pitch = -0.2f }, shoulder);
                Sound(SkeletronSounds.Clack with { Pitch = -0.3f }, shoulder);

                if (!Main.dedServ)
                {
                    for (int i = 0; i < 16; i++)
                        Dust.NewDustPerfect(shoulder, i % 3 == 0 ? DustID.Blood : DustID.Bone, Main.rand.NextVector2Circular(5f, 5f), 0, default, 1.3f);
                }
            }

            if (At(WhipWind))
                Sound(SkeletronSounds.Swoop, npc.Center);

            // The crack in its sound comes a third of a second (20 ticks, 16 of his slowed clock) in: started so it lands
            // with the tip.
            if (At(WhipCracked - (int)(20 * Pace)))
                Sound(SkeletronSounds.WhipCrack with { Volume = 1f }, HandAt(npc, other));

            if (At(WhipCracked) && !LegendsSkeletronHand.Turned(HandOn(other)))
            {
                Vector2 tip = HandAt(npc, other);
                Sound(SoundID.Item153 with { Pitch = -0.2f, Volume = 0.9f }, tip);

                if (!Main.dedServ)
                {
                    PADParticleHandler.SpawnParticle(new ParryRing(tip, 6f, 110f, 14, Color.White));
                    PADParticleHandler.SpawnParticle(new BloomGeneric(tip, Vector2.Zero, new Color(255, 250, 230), 0.7f, 10));
                    EmpressWarp.Ripple(tip, 20f, 22f, 12f, 20);
                    Shake(tip, 9f, 14);
                }
            }

            if (At(WhipOn))
                Sound(SkeletronSounds.Clack, ShoulderOf(npc, other));

            if (Authority && t >= WhipEnd)
                End(npc);
        }

        // =================================================================================================================
        // BOWLING
        // =================================================================================================================

        private const int BowlLift = 30;
        private const int BowlSwing = 58;
        private const int BowlRoll = 72;
        private const int BowlBack = 136;
        private const int BowlEnd = 176;
        private const float BowlSpeed = 21f;
        private const float BowlRunUp = 560f;

        /// <summary>His skull's radius, as it rolls; and how far from his middle the ball of him hurts (less: it can be jumped).</summary>
        private static float Round(NPC npc) => 48f * npc.scale;
        private const float BowlHurt = 34f;

        /// <summary>Rolling along the ground, bowled.</summary>
        private bool Bowled() => move == Move.Bowling && moveTimer >= BowlRoll && moveTimer < BowlBack;

        /// <summary>The ground under a point of the roll: the floor near the player's feet level, or that level with none.</summary>
        private float RollFloor(float x) => GroundOr(x, 30f, mark2.Y - 160f, 320f);

        private void Bowling(NPC npc, Player target)
        {
            float t = moveClock;
            float dir = mark.X >= npc.Center.X ? 1f : -1f;
            float r = Round(npc);

            // Where it all runs: fixed as the hands take hold, the floor under the player then (or a line at their feet).
            if (At(28))
            {
                Fix(npc, target.Center);
                float floor = GroundOr(target.Center.X, 40f, target.Center.Y, 600f);
                FixSecond(npc, new Vector2(target.Center.X - (target.Center.X >= npc.Center.X ? 1f : -1f) * BowlRunUp, floor));
            }

            Vector2 release = new Vector2(mark2.X, RollFloor(mark2.X) - r);
            Vector2 top = release + new Vector2(-dir * 80f, -200f);

            if (t < BowlLift)
            {
                npc.velocity *= 0.85f;
            }
            else if (t < BowlSwing)
            {
                Approach(npc, top, 0.14f, 20f);
                npc.rotation -= dir * 0.06f * Pace;
            }
            else if (t < BowlRoll)
            {
                float u = EaseIn((t - BowlSwing) / (float)(BowlRoll - BowlSwing));
                Place(npc, SkeletronRig.Curve(top, new Vector2(top.X + dir * 20f, release.Y + 40f), release, u));
                npc.rotation += dir * 0.2f * Pace;
            }
            else if (t < BowlBack)
            {
                // Rolled along the ground at them.
                float rolled = t - BowlRoll;
                float slow = rolled > 44 ? 1f - (rolled - 44) / 30f : 1f;
                float x = release.X + dir * BowlSpeed * (rolled - Math.Max(0f, rolled - 44) * (1f - slow) * 0.5f);
                Place(npc, new Vector2(x, RollFloor(x) - r));
                npc.rotation += dir * BowlSpeed * slow / r * Pace;
                headHurt = 1.8f;

                // Into a wall: stopped dead (the server says).
                if (Authority && Collision.SolidCollision(npc.Center + new Vector2(dir * (r + 6f) - 4f, -12f), 8, 24))
                {
                    pick = 1;
                    moveTimer = BowlBack - 1;
                    npc.netUpdate = true;
                }

                if (!Main.dedServ && Main.GameUpdateCount % 2 == 0)
                {
                    Vector2 contact = new Vector2(npc.Center.X, npc.Center.Y + r);
                    Dust dust = Dust.NewDustPerfect(contact, DustID.Smoke, new Vector2(-dir * Main.rand.NextFloat(1f, 4f), -Main.rand.NextFloat(0.5f, 2f)), 100, default, 1.4f);
                    dust.noGravity = true;
                    Dust.NewDustPerfect(contact, DustID.Bone, new Vector2(-dir * 3f, -2f), 0, default, 1f);
                }
            }
            else
            {
                Approach(npc, target.Center + new Vector2(0f, -280f), 0.08f, 12f);
                npc.rotation = MathHelper.WrapAngle(npc.rotation) * 0.85f;
            }

            // His hands. One bowls: its fingers hooked into an eye socket of his skull, it swings him back and up and down
            // through to the floor, lets go and follows through high, pointing after him; the other out behind for balance.
            // Both stay there at the line as he rolls away from them (their arms come off him), and come back after.
            float bowler = actSide;

            for (int k = 0; k < 2; k++)
            {
                float s = SideOf(k);

                if (HandOn(s) == null)
                    continue;

                Aim a;
                Vector2 line = release + new Vector2(0f, r);

                if (t < BowlRoll)
                {
                    if (s == bowler)
                    {
                        Vector2 eye = SkeletronRig.Eye(s < 0f, npc.Center, npc.rotation, npc.scale, 0f);
                        Vector2 grip = eye + new Vector2(0f, -40f).RotatedBy(npc.rotation) * npc.scale;
                        a = Aim.At(grip, npc.rotation, SkeletronRig.Bowl, t < 16 ? 0.3f : 1f, 40f);
                        a.Turn = t < 16 ? 0.3f : 1f;
                    }
                    else if (t < BowlLift)
                    {
                        // Steadying him from the side as the bowler takes hold.
                        a = Aim.At(npc.Center + new Vector2(s * 66f, 10f), SkeletronRig.AngleFor(new Vector2(-s, 0.25f)), SkeletronRig.Grab, 0.3f);
                    }
                    else
                    {
                        // Out behind, for balance, opening as the swing comes through.
                        Vector2 balance = npc.Center + new Vector2(-dir * 170f, -40f);
                        a = Aim.At(balance, SkeletronRig.AngleFor(new Vector2(-dir, -0.3f)), SkeletronRig.Spread, 0.2f, 30f);
                    }
                }
                else if (t < BowlBack)
                {
                    if (s == bowler)
                    {
                        // The follow-through: on up past where it let go, pointing after him.
                        Vector2 high = line + new Vector2(dir * 120f, -190f);
                        a = Aim.At(high, SkeletronRig.AngleFor(new Vector2(dir, -0.55f)), SkeletronRig.Point, 0.25f, 30f);
                    }
                    else
                    {
                        Vector2 back = line + new Vector2(-dir * 170f, -120f);
                        a = Aim.At(back, SkeletronRig.AngleFor(new Vector2(-dir, -0.2f)), SkeletronRig.Spread, 0.2f, 30f);
                    }
                }
                else
                {
                    a = Aim.At(Rest(npc, s), RestAngle(npc, s), Hanging(s), 0.12f, 30f);
                }

                a.Hurt = 0f;
                aims[k] = a;
            }

            if (At(BowlRoll))
            {
                Sound(SkeletronSounds.BowlingRoll, npc.Center);

                if (!Main.dedServ)
                {
                    LegendsKingSlime.GroundDust(npc.Center.X - 60f, npc.Center.X + 60f, npc.Center.Y + r, 0.8f);
                    Shake(npc.Center, 5f, 10);
                }
            }

            if (At(BowlBack) && pick == 1)
            {
                Sound(SkeletronSounds.Thud, npc.Center);
                Shake(npc.Center, 7f, 14);
            }

            if (Authority && t >= BowlEnd)
                End(npc);
        }

        /// <summary>His skull, bowled, hit someone (on their machine): a strike, the pins (his bones) flying.</summary>
        private void BowlingStrike(NPC npc, Player target)
        {
            if (target.whoAmI != Main.myPlayer || moveTimer < BowlRoll || moveTimer >= BowlBack)
                return;

            SoundEngine.PlaySound(SkeletronSounds.Strike, target.Center);
            Main.LocalPlayer.PIS().BumpScreenShake(10f, 18);

            int bone = SkeletronSystem.Bone[redHat ? 1 : 0];

            if (bone <= 0)
                return;

            for (int i = 0; i < 10; i++)
            {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-6f, 6f), -Main.rand.NextFloat(5f, 11f));
                Gore.NewGore(npc.GetSource_FromAI(), target.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f), velocity, bone);
            }
        }

        // =================================================================================================================
        // SEALS
        // =================================================================================================================

        private const int SealsFrom = 24;
        private const int SealEvery = 15;
        private const int SealCount = 5;
        private const int SealsFix = 90;
        private const int SealsPoof = 99;
        private const int SealsRest = 119;
        private const int SealsEnd = 150;
        private const float SealsRing = 380f;

        /// <summary>The five seals: each hand's pose, where it is from between them, and its turn from straight up.</summary>
        private static readonly (SkeletronRig.Pose a, Vector2 at, float turn, SkeletronRig.Pose b, Vector2 bAt, float bTurn)[] SealPoses =
        {
            (SkeletronRig.Point, new Vector2(12f, 0f), 0f, SkeletronRig.Point, new Vector2(-12f, 0f), 0f),
            (SkeletronRig.Horns, new Vector2(0f, -18f), -0.3f, SkeletronRig.Fist, new Vector2(0f, 16f), MathHelper.Pi),
            (SkeletronRig.Okay, new Vector2(20f, 0f), -0.5f, SkeletronRig.Scissors, new Vector2(-20f, 0f), 0.5f),
            (SkeletronRig.Pinch, new Vector2(10f, -10f), -0.9f, SkeletronRig.Point, new Vector2(-10f, 10f), 0.9f),
            (SkeletronRig.Spread, new Vector2(0f, -20f), 0f, SkeletronRig.Flat, new Vector2(0f, 18f), 0f),
        };

        private void Seals(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            Vector2 between = npc.Center + new Vector2(0f, 118f);

            npc.velocity *= 0.85f;
            eyesWant = t >= SealsFrom && t < SealsPoof ? 1f : 0f;
            eyeColour = new Color(120, 200, 255);

            if (At(SealsFix))
                Fix(npc, target.Center);

            for (int k = 0; k < 2; k++)
            {
                float s = SideOf(k);
                bool first = s == side;
                Aim a;

                if (t < SealsFrom)
                {
                    a = Aim.At(between + new Vector2(s * 26f, 0f), MathHelper.Pi, SkeletronRig.Flat, 0.3f);
                }
                else if (t < SealsPoof)
                {
                    int seal = Math.Min(SealCount - 1, (int)((t - SealsFrom) / SealEvery));
                    var pose = SealPoses[seal];
                    Vector2 at = first ? pose.at : pose.bAt;

                    a = Aim.At(between + new Vector2(at.X * side, at.Y), MathHelper.Pi + side * (first ? pose.turn : pose.bTurn), first ? pose.a : pose.b, 0.5f);
                    a.Shape = 0.6f;
                    a.Turn = 0.5f;
                }
                else if (t < SealsRest)
                {
                    Vector2 toward = (mark - between).SafeNormalize(Vector2.UnitY);
                    a = Aim.At(between + new Vector2(s * 60f, 0f), SkeletronRig.AngleFor(toward), SkeletronRig.Point, 0.3f);
                }
                else
                {
                    a = Aim.At(Rest(npc, s), RestAngle(npc, s), Hanging(s), 0.15f);
                }

                a.Hurt = 0f;
                aims[k] = a;
            }

            for (int seal = 0; seal < SealCount; seal++)
            {
                if (!At(SealsFrom + seal * SealEvery))
                    continue;

                Sound(SkeletronSounds.Seal(seal), between);

                if (!Main.dedServ)
                {
                    PADParticleHandler.SpawnParticle(new CrownGlint(between, 50f + seal * 8f, 16, new Color(180, 220, 255)));

                    for (int i = 0; i < 8; i++)
                    {
                        Dust dust = Dust.NewDustPerfect(between + Main.rand.NextVector2Circular(20f, 20f), DustID.Smoke, Main.rand.NextVector2Circular(2f, 2f), 120, default, 1.2f);
                        dust.noGravity = true;
                    }
                }
            }

            // Poof: smoke round them, and skulls out of it.
            if (At(SealsPoof))
            {
                Sound(SkeletronSounds.Poof, mark);
                Sound(SkeletronSounds.Summon, mark);

                for (int i = 0; i < 8; i++)
                {
                    // Round them over the top only, from one side to the other: never out of the floor.
                    Vector2 at = mark + (MathHelper.Pi + i * MathHelper.Pi / 7f).ToRotationVector2() * SealsRing;

                    if (!Main.dedServ)
                    {
                        for (int k = 0; k < 4; k++)
                            PADParticleHandler.SpawnParticle(new DustFog(at + Main.rand.NextVector2Circular(14f, 14f), Main.rand.NextVector2Circular(1.5f, 1.5f), new Color(200, 200, 210), Main.rand.Next(40, 70), Main.rand.NextFloat(0.8f, 1.3f), 0.5f));
                    }

                    Shoot(npc, at, Vector2.Zero, ModContent.ProjectileType<SkeletronSkull>(), Damage(npc, 14f, 12f), SkeletronSkull.Summoned, 30 + i * 3);
                }
            }

            if (Authority && t >= SealsEnd)
                End(npc);
        }

        /// <summary>The seals: a sign drawn in the air between his hands, a line more with each, bright as each is made.</summary>
        private void DrawSeals(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < SealsFrom || t >= SealsPoof + 6)
                return;

            Vector2 between = npc.Center + new Vector2(0f, 118f);
            int made = Math.Min(SealCount, (int)((t - SealsFrom) / SealEvery) + 1);
            float since = (t - SealsFrom) % SealEvery;
            float flash = 1f - since / (float)SealEvery;
            Color ink = new Color(150, 210, 255);
            float r = 40f + made * 10f;

            SkeletronRig.SoftRing(spriteBatch, between, r, ink * (0.25f + 0.3f * flash), 4f, 36);

            for (int i = 0; i < made; i++)
            {
                float a = -MathHelper.PiOver2 + i * MathHelper.TwoPi * 2f / 5f;
                float b = -MathHelper.PiOver2 + (i + 1) * MathHelper.TwoPi * 2f / 5f;
                SkeletronRig.Beam(spriteBatch, between + a.ToRotationVector2() * r, between + b.ToRotationVector2() * r, ink * (0.35f + 0.3f * flash), 4f);
            }

            SkeletronRig.Glow(spriteBatch, between, ink * (0.3f * flash), 50f);
        }

        // =================================================================================================================
        // SWOOP
        // =================================================================================================================

        private const int SwoopFix = 20;
        private const int SwoopGo = 26;
        private const int SwoopDone = 54;
        private const int SwoopMissEnd = 80;
        private const int SwoopHold = 100;
        private const int SwoopPinch = 108;
        private const int SwoopThrow = 150;
        private const int SwoopLetGo = 154;
        private const int SwoopCombo = 158;
        private const int SwoopPunch = 172;
        private const int SwoopRest = 190;
        private const int SwoopEnd = 216;

        /// <summary>The swoop's curve: from high on its side, down through where they stood, up the other side.</summary>
        private void SwoopCurve(float side, out Vector2 start, out Vector2 low, out Vector2 end)
        {
            start = mark + new Vector2(side * 430f, -240f);
            low = mark + new Vector2(0f, 110f);
            end = mark + new Vector2(-side * 430f, -210f);
        }

        private void Swoop(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            float other = -side;
            float puncherSide = HandOn(other) != null ? other : side;

            // (Not while he holds them: over them, he would only climb with them.)
            if (t < SwoopHold)
                Hover(npc, target, new Vector2(0f, -300f), 0.03f, 4f);
            else
                npc.velocity *= 0.9f;

            if (t < SwoopFix)
                Track(target.Center);

            if (At(SwoopFix))
                Fix(npc, target.Center);

            SwoopCurve(side, out Vector2 start, out Vector2 low, out Vector2 end);
            NPC grabber = HandOn(side);
            Aim a;

            if (t < SwoopGo)
            {
                a = Aim.At(start, SkeletronRig.AngleFor(low - start), SkeletronRig.Pose.Lerp(SkeletronRig.Spread, SkeletronRig.Grab, Ease(t / 20f)), 0.2f, 26f);
                a.Hurt = 0f;
            }
            else if (t < SwoopDone)
            {
                float u = Smoother((t - SwoopGo) / (float)(SwoopDone - SwoopGo));
                Vector2 here = SkeletronRig.Curve(start, low, end, u);
                Vector2 ahead = SkeletronRig.Curve(start, low, end, Math.Min(1f, u + 0.03f));

                a = Aim.At(here, SkeletronRig.AngleFor(ahead - here), SkeletronRig.Grab, 1f);
                a.Turn = 0.4f;
                a.Blow = true;
                a.Hurt = 0.8f;

                // Whoever the open hand meets is taken (the server's).
                if (Authority && grabbed < 0)
                {
                    int who = Catch(grabber, here, 18);

                    if (who >= 0)
                        Caught(npc, who, SwoopHold, Main.player[who].Center);
                }
            }
            else if (t < SwoopHold)
            {
                a = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.15f);
                a.Hurt = 0f;

                if (Authority && t >= SwoopMissEnd)
                    End(npc);
            }
            else if (t < SwoopThrow)
            {
                // Up by his face, the player pinched by the head.
                a = Aim.At(npc.Center + new Vector2(side * 150f, 30f), 0f, t < SwoopPinch - 2 ? SkeletronRig.Grab : SkeletronRig.Pinch, 0.15f);
                a.Shape = 0.4f;
                a.Hurt = 0f;
                a.Shake = t >= SwoopPinch ? 1.2f : 0f;
            }
            else if (t < SwoopRest)
            {
                // Flicked up and let go.
                float up = EaseOut((t - SwoopThrow) / 6f);
                a = Aim.At(npc.Center + new Vector2(side * 150f, 30f - 90f * up), MathHelper.Pi * up, SkeletronRig.Okay, 0.6f);
                a.Hurt = 0f;

                if (t > SwoopLetGo + 6)
                {
                    a.Goal = Rest(npc, side);
                    a.Gain = 0.15f;
                    a.Angle = RestAngle(npc, side);
                }
            }
            else
            {
                a = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.15f);
                a.Hurt = 0f;
            }

            aims[IndexOf(side)] = a;

            // Held: where the pinch is.
            if (t >= SwoopHold && t < SwoopLetGo && grabbed >= 0)
            {
                Vector2 pinch = PinchOf(npc, side);
                HoldGrabbed(npc, pinch + new Vector2(0f, 18f));
            }

            if (At(SwoopGo))
                Sound(SkeletronSounds.Swoop, start);

            if (At(SwoopPinch) && grabbed >= 0)
                Sound(SkeletronSounds.Pinch, HandAt(npc, side));

            if (At(SwoopPinch + 8) && grabbed >= 0)
                Sound(SkeletronSounds.Cackle, npc.Center);

            // Thrown up, and where they will be by the time the punch comes.
            if (At(SwoopLetGo) && grabbed >= 0)
            {
                Player held = Main.player[grabbed];
                Vector2 throwAt = new Vector2(held.Center.X >= npc.Center.X ? 6f : -6f, -17f);

                LetGo(throwAt);
                Sound(SoundID.Item1 with { Pitch = -0.5f }, held.Center);

                // Where they will be when the punch comes (every machine's guess until the server's is heard).
                const int Ahead = 18;
                Vector2 there = held.Center + new Vector2(throwAt.X * Ahead, throwAt.Y * Ahead + 0.5f * 0.4f * Ahead * Ahead);
                Fix(npc, there);
            }

            if (Authority && At(SwoopLetGo + 6))
            {
                grabbed = -1;
                npc.netUpdate = true;
            }

            // The punch at them in the air (the other hand, or this one alone).
            if (t >= SwoopCombo && t < SwoopRest && HandOn(puncherSide) != null && (puncherSide != side || t > SwoopLetGo + 4))
            {
                Vector2 from = npc.Center + new Vector2(puncherSide * 150f, 60f);
                Vector2 way = (mark - from).SafeNormalize(Vector2.UnitY);
                float p = EaseIn((t - SwoopCombo) / (float)(SwoopPunch - SwoopCombo));
                Aim punch = Aim.At(Vector2.Lerp(from, mark + way * 60f, p), SkeletronRig.AngleFor(way), SkeletronRig.Fist, 1f);
                punch.Turn = 0.5f;
                punch.Blow = t <= SwoopPunch + 6;
                punch.Hurt = t <= SwoopPunch + 6 ? 1.6f : 0f;
                punch.Push = way * 10f + new Vector2(0f, -6f);

                // Punched: home under him, not back across his face.
                if (t > SwoopPunch + 6)
                {
                    punch = Aim.At(Rest(npc, puncherSide), RestAngle(npc, puncherSide), SkeletronRig.Relaxed, 0.2f, 24f);
                    punch.Hurt = 0f;
                }

                aims[IndexOf(puncherSide)] = punch;
            }

            if (At(SwoopPunch - 4))
                Sound(SkeletronSounds.Swoop with { Volume = 0.6f, Pitch = 0.3f }, mark);

            if (Authority && t >= SwoopEnd)
                End(npc);
        }

        /// <summary>Where the pinch of a hand of his is (thumb and forefinger), as it is asked to be posed now.</summary>
        private Vector2 PinchOf(NPC npc, float side)
        {
            Aim a = aims[IndexOf(side)];
            Vector2 middle = HandAt(npc, side);
            Vector2 wrist = SkeletronRig.WristFromMiddle(middle, a.Angle, side > 0f, 1f);
            return SkeletronRig.PinchPoint(SkeletronRig.Pinch, wrist, a.Angle, side > 0f, 1f);
        }

        /// <summary>The swoop's way, shown before it comes: a dotted curve through where they stood.</summary>
        private void DrawSwoopPath(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < SwoopFix || t >= SwoopDone)
                return;

            SwoopCurve(actSide, out Vector2 start, out Vector2 low, out Vector2 end);
            float shown = Ease((t - SwoopFix) / 6f) * (t >= SwoopGo ? 1f - (t - SwoopGo) / (float)(SwoopDone - SwoopGo) : 1f);
            Color line = new Color(255, 236, 200);

            for (int i = 0; i < 24; i++)
            {
                float u = i / 23f;
                Vector2 at = SkeletronRig.Curve(start, low, end, u);
                float twinkle = 0.6f + 0.4f * (float)Math.Sin(t * 0.3f - i * 0.6f);
                SkeletronRig.Hot(spriteBatch, at, line * (0.45f * shown * twinkle), 5f);
            }
        }

        // =================================================================================================================
        // PURPLE
        // =================================================================================================================

        private const int PurpleFix = 40;
        private const int PurpleSwoop = 44;
        private const int PurpleMeet = 66;
        private const int PurpleMissEnd = 96;
        private const int PurpleHold = 100;
        private const int PurpleIgnite = 110;
        private const int PurpleFire = 140;

        /// <summary>Held on through the bolt's burst on them (it gets there in about 8 ticks, and burns a while), by request.</summary>
        private const int PurpleLetGo = 172;
        private const int PurpleEnd = 200;

        private static readonly Color Violet = new Color(170, 70, 255);

        private void Purple(NPC npc, Player target)
        {
            float t = moveClock;

            if (t < PurpleFix)
                Track(target.Center);

            if (At(PurpleFix))
                Fix(npc, target.Center);

            if (t < PurpleHold)
                Hover(npc, target, new Vector2(0f, -320f), 0.03f, 4f);
            else
                npc.velocity *= 0.9f;

            bool caught = t >= PurpleHold && grabbed >= 0;
            Vector2 mouth = MouthOf(npc);
            Vector2 held = caught ? Vector2.Lerp(mark2, mouth + new Vector2(0f, 50f), Ease((t - PurpleHold) / 30f)) : mark;

            if (caught && t >= PurpleHold + 4)
            {
                // Open on them, and shut again as they are let go.
                float shut = 1f - Ease((t - PurpleLetGo) / 10f);
                jawWant = 26f * Ease((t - PurpleHold - 4) / 12f) * shut;
                liftWant = 10f * Ease((t - PurpleHold - 4) / 12f) * shut;
            }

            if (caught && t >= PurpleIgnite && t < PurpleLetGo)
            {
                flameWant = 1f;
                eyesWant = 1f;
                eyeColour = Violet;
            }

            for (int k = 0; k < 2; k++)
            {
                float s = SideOf(k);

                if (HandOn(s) == null)
                    continue;

                Vector2 high = mark + new Vector2(s * 330f, -300f);
                Aim a;

                if (t < PurpleSwoop)
                {
                    float charge = t / (float)PurpleSwoop;
                    a = Aim.At(high, SkeletronRig.AngleFor(new Vector2(-s * 0.5f, 1f)), SkeletronRig.Spread, 0.12f, 20f);
                    a.Glow = Violet * (0.5f + 0.5f * charge);
                    a.GlowReach = 2f + 3f * charge;
                    a.Shake = 1.5f * charge;
                    a.Hurt = 0f;
                }
                else if (t < PurpleMeet)
                {
                    float u = EaseIn((t - PurpleSwoop + 1) / (float)(PurpleMeet - PurpleSwoop));
                    Vector2 here = SkeletronRig.Curve(high, mark + new Vector2(s * 150f, -40f), mark + new Vector2(s * 34f, 0f), u);
                    a = Aim.At(here, SkeletronRig.AngleFor(new Vector2(-s, 0.4f)), SkeletronRig.Grab, 1f);
                    a.Glow = Violet;
                    a.GlowReach = 5f;
                    a.Blow = true;
                    a.Joint = true;
                    a.Hurt = 1f;

                    if (Authority && grabbed < 0)
                    {
                        int who = Catch(HandOn(s), here, 16);

                        if (who >= 0)
                            Caught(npc, who, PurpleHold, Main.player[who].Center);
                    }
                }
                else if (caught && t < PurpleLetGo)
                {
                    a = Aim.At(held + new Vector2(s * 40f, 0f), SkeletronRig.AngleFor(new Vector2(-s, 0f)), SkeletronRig.Clutch, 0.4f);
                    a.Glow = Violet;
                    a.GlowReach = 4f;
                    a.Hurt = 0f;
                }
                else
                {
                    a = Aim.At(Rest(npc, s), RestAngle(npc, s), Hanging(s), 0.15f);
                    a.Hurt = 0f;
                }

                aims[k] = a;
            }

            if (caught && t < PurpleLetGo)
                HoldGrabbed(npc, held);

            if (At(0))
                Sound(SkeletronSounds.PurpleCharge, npc.Center);

            if (!Main.dedServ && t < PurpleSwoop && Main.GameUpdateCount % 3 == 0)
            {
                for (int k = 0; k < 2; k++)
                {
                    NPC h = HandOn(SideOf(k));

                    if (h == null)
                        continue;

                    Vector2 from = h.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                    PADParticleHandler.SpawnParticle(new SoftGlow(from, (h.Center - from) * 0.08f, Violet, 0.12f, 18));
                }
            }

            if (At(PurpleSwoop))
                Sound(SkeletronSounds.Swoop with { Pitch = -0.2f }, mark);

            // Missed: the hands meet where they were, in a burst of it.
            if (At(PurpleMeet) && grabbed < 0)
            {
                bool broken = Authority ? brokenIn == serial : LegendsSkeletronHand.Broken(npc);

                if (!broken)
                {
                    if (!Main.dedServ)
                    {
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f }, mark);
                        SoundEngine.PlaySound(SkeletronSounds.Thud, mark);
                        PADParticleHandler.SpawnParticle(new ParryRing(mark, 10f, 130f, 18, Violet));
                        PADParticleHandler.SpawnParticle(new BloomGeneric(mark, Vector2.Zero, Violet, 0.9f, 18));
                        EmpressWarp.Ripple(mark, 14f, 26f, 9f, 22);
                        Shake(mark, 7f, 14);

                        for (int i = 0; i < 30; i++)
                        {
                            Dust dust = Dust.NewDustPerfect(mark, DustID.Shadowflame, Main.rand.NextVector2Circular(9f, 9f), 0, default, Main.rand.NextFloat(1.2f, 1.8f));
                            dust.noGravity = true;
                        }
                    }

                    Shoot(npc, mark, Vector2.Zero, ModContent.ProjectileType<SkeletronBlast>(), Damage(npc, 18f, 15f), 110f, SkeletronBlast.Purple);
                }
            }

            if (Authority && grabbed < 0 && t >= PurpleMissEnd && t < PurpleHold)
            {
                End(npc);
                return;
            }

            if (caught && At(PurpleIgnite))
                Sound(SkeletronSounds.Ignite, npc.Center);

            // Point blank: the Diabolist's fire, out of his mouth into them.
            if (caught && At(PurpleFire))
            {
                Vector2 way = (held - mouth).SafeNormalize(Vector2.UnitY);
                int bolt = Shoot(npc, mouth, way * 10f, ProjectileID.InfernoHostileBolt, Damage(npc, 22f, 19f), held.X - 6f, held.Y - 6f);

                // Bursting on them when it gets there (the game's bolt waits to be past its point both ways, which one going
                // straight down never is); its death makes the burst.
                if (bolt >= 0 && bolt < Main.maxProjectiles)
                {
                    Main.projectile[bolt].timeLeft = (int)(Vector2.Distance(mouth, held) / 10f) + 1;
                    Main.projectile[bolt].netUpdate = true;
                }

                if (!Main.dedServ)
                {
                    Cinematic.Flash(new Color(255, 120, 40), 0.25f * Near(mouth));
                    Shake(mouth, 6f, 12);
                }
            }

            if (caught && At(PurpleLetGo))
                LetGo(new Vector2(mark2.X >= npc.Center.X ? 10f : -10f, -9f));

            if (Authority && At(PurpleLetGo + 4))
            {
                grabbed = -1;
                npc.netUpdate = true;
            }

            if (Authority && t >= PurpleEnd)
                End(npc);
        }

        /// <summary>The purple: where the hands will meet, marked, pulsing faster as they come.</summary>
        private void DrawPurple(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t >= PurpleMeet)
                return;

            float p = t / (float)PurpleMeet;
            float pulse = 0.6f + 0.4f * (float)Math.Sin(t * (0.15f + 0.5f * p));

            SkeletronRig.SoftRing(spriteBatch, mark, 120f * (1.2f - 0.4f * p), Violet * (0.3f * pulse * p), 6f, 40);
            SkeletronRig.Glow(spriteBatch, mark, Violet * (0.15f * p), 120f);
        }

        // =================================================================================================================
        // GUNS
        // =================================================================================================================

        private const int GunsFirst = 24;
        private const int GunsEvery = 10;
        private const int GunsShots = 9;
        private const int GunsBlow = 114;
        private const int GunsSmoke = 120;
        private const int GunsTwirl = 140;
        private const int GunsRest = 156;
        private const int GunsEnd = 176;

        /// <summary>Which hand fires this shot: three each, turn about (one alone fires all).</summary>
        private float Shooter(int shot)
        {
            float side = actSide;
            return (shot / 3) % 2 == 0 || HandOn(-side) == null ? side : -side;
        }

        private void Guns(NPC npc, Player target)
        {
            float t = moveClock;

            Hover(npc, target, new Vector2(0f, -280f), 0.03f, 4f);

            if (t >= GunsSmoke - 2 && t < GunsSmoke + 18)
                jawWant = 10f;

            for (int k = 0; k < 2; k++)
            {
                float s = SideOf(k);

                if (HandOn(s) == null)
                    continue;

                Vector2 spot = npc.Center + new Vector2(s * 170f, 50f);
                Vector2 way = (target.Center - HandAt(npc, s)).SafeNormalize(new Vector2(s, 0f));
                Aim a;

                if (t < GunsBlow)
                {
                    a = Aim.At(spot, SkeletronRig.AngleFor(way), SkeletronRig.Gun, 0.2f);
                    a.Turn = 0.3f;
                    a.Hurt = 0.6f;

                    // The kick of the last shot this hand fired.
                    for (int shot = 0; shot < GunsShots; shot++)
                    {
                        int when = GunsFirst + shot * GunsEvery;

                        if (Shooter(shot) == s && t >= when && t < when + 6)
                        {
                            float kick = 1f - (t - when) / 6f;
                            a.Goal -= way * 16f * kick;
                            a.Angle += -Math.Sign(way.X == 0f ? s : way.X) * 0.35f * kick;
                            a.Gain = 0.5f;
                            a.Turn = 0.6f;
                        }
                    }
                }
                else if (t < GunsRest)
                {
                    float twirl = t >= GunsTwirl ? MathHelper.TwoPi * Ease((t - GunsTwirl) / 16f) : 0f;
                    a = Aim.At(npc.Center + new Vector2(s * 86f, -24f), MathHelper.Pi + s * twirl, SkeletronRig.Gun, 0.2f);
                    a.Turn = t >= GunsTwirl ? 1f : 0.25f;
                    a.Hurt = 0f;
                }
                else
                {
                    a = Aim.At(Rest(npc, s), RestAngle(npc, s), Hanging(s), 0.15f);
                    a.Hurt = 0f;
                }

                aims[k] = a;
            }

            for (int shot = 0; shot < GunsShots; shot++)
            {
                if (!At(GunsFirst + shot * GunsEvery))
                    continue;

                float s = Shooter(shot);
                Vector2 tip = TipOf(npc, s, SkeletronRig.Digit.Index);
                Vector2 way = (target.Center - tip).SafeNormalize(new Vector2(s, 0f));

                Sound(SkeletronSounds.Pew, tip);

                if (!Main.dedServ)
                    MuzzleFlash.Spawn(tip, way);

                Shoot(npc, tip, way.RotatedBy(Main.rand.NextFloat(-0.05f, 0.05f)) * 13f, ModContent.ProjectileType<BoneBullet>(), Damage(npc, 14f, 12f), 0f, redHat ? 1f : 0f);
            }

            if (At(GunsSmoke))
                Sound(SkeletronSounds.BlowSmoke, npc.Center);

            if (!Main.dedServ && t >= GunsSmoke && t < GunsSmoke + 20 && Main.GameUpdateCount % 4 == 0)
            {
                for (int k = 0; k < 2; k++)
                {
                    if (HandOn(SideOf(k)) != null)
                        MuzzleSmoke.Spawn(TipOf(npc, SideOf(k), SkeletronRig.Digit.Index), new Vector2(0f, -1f), 2);
                }
            }

            if (Authority && t >= GunsEnd)
                End(npc);
        }

        // =================================================================================================================
        // SNAP
        // =================================================================================================================

        private const int SnapFix = 10;
        private const int SnapAt = 34;
        private const int SnapRelax = 60;
        private const int SnapEnd = 120;
        private const float SpikeGap = 52f;
        private const int SpikesMost = 18;

        /// <summary>How many spikes, from below his hand along to past the player.</summary>
        private int SpikeCount => Math.Min(SpikesMost, (int)Math.Ceiling((Math.Abs(mark.X - mark2.X) + 280f) / SpikeGap));

        /// <summary>Where the i-th spike comes up: on the ground there (or a line at the player's feet, with none).</summary>
        private Vector2 SpikeAt(int i)
        {
            float dir = mark.X >= mark2.X ? 1f : -1f;
            float x = mark2.X + dir * (80f + SpikeGap * i);
            return new Vector2(x, GroundOr(x, 8f, mark.Y - 60f, 420f));
        }

        private void Snap(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;

            Hover(npc, target, new Vector2(0f, -280f), 0.03f, 4f);

            if (At(SnapFix))
            {
                Fix(npc, target.Center);
                FixSecond(npc, HandAt(npc, side));
                Sound(SkeletronSounds.GroundCrack, target.Center);
            }

            Vector2 way = (target.Center - HandAt(npc, side)).SafeNormalize(new Vector2(side, 0f));
            Aim a;

            if (t < SnapAt)
            {
                a = Aim.At(npc.Center + new Vector2(side * 150f, 30f), SkeletronRig.AngleFor(way), SkeletronRig.SnapReady, 0.2f);
                a.Hurt = 0.6f;
                a.Shake = t > SnapFix ? 0.8f : 0f;
            }
            else if (t < SnapRelax)
            {
                a = Aim.At(npc.Center + new Vector2(side * 150f, 30f), SkeletronRig.AngleFor(way), SkeletronRig.SnapDone, 0.2f);
                a.Shape = t < SnapAt + 3 ? 1f : 0.2f;
                a.Hurt = 0.6f;
            }
            else
            {
                a = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.15f);
                a.Hurt = 0f;
            }

            aims[IndexOf(side)] = a;

            if (At(SnapAt))
            {
                Vector2 tip = TipOf(npc, side, SkeletronRig.Digit.Middle);
                Sound(SkeletronSounds.Snap, tip);

                if (!Main.dedServ)
                {
                    PADParticleHandler.SpawnParticle(new ParryRing(tip, 4f, 60f, 12, new Color(255, 245, 220)));
                    PADParticleHandler.SpawnParticle(new CrownGlint(tip, 46f, 12, Color.White));
                    Shake(tip, 3f, 6);
                }

                for (int i = 0; i < SpikeCount; i++)
                    Shoot(npc, SpikeAt(i), Vector2.Zero, ModContent.ProjectileType<BoneSpike>(), Damage(npc, 18f, 15f), 4 + 3 * i, redHat ? 1f : 0f);
            }

            if (Authority && t >= SnapEnd)
                End(npc);
        }

        /// <summary>Before the snap: cracks running along the ground where the spikes will come, one after another.</summary>
        private void DrawCrackLine(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < SnapFix || t >= SnapAt + 6)
                return;

            int shown = Math.Min(SpikeCount, (int)((t - SnapFix) / 1.4f) + 1);
            Color crack = new Color(28, 20, 14);

            for (int i = 0; i < shown; i++)
            {
                Vector2 at = SpikeAt(i);
                float jag = (i % 2 == 0 ? 1f : -1f) * 3f;

                SkeletronRig.Line(spriteBatch, at + new Vector2(-16f, -1f), at + new Vector2(0f, -1f + jag), crack, 2f);
                SkeletronRig.Line(spriteBatch, at + new Vector2(0f, -1f + jag), at + new Vector2(16f, -1f), crack, 2f);
                SkeletronRig.Glow(spriteBatch, at, new Color(255, 230, 190) * 0.18f, 18f);
            }
        }

        // =================================================================================================================
        // RPS
        // =================================================================================================================

        private const int RpsJan = 9;
        private const int RpsKen = 27;
        private const int RpsPon = 45;
        private const int RpsGo = 58;
        private const int RpsStrike = 68;
        private const int RpsEnd = 110;

        private const byte Rock = 1;
        private const byte Paper = 2;
        private const byte Scissors = 3;

        /// <summary>The paper's wall of air comes a little below their middle: low enough to jump.</summary>
        private const float PaperLow = 12f;

        private void Rps(NPC npc, Player target)
        {
            float t = moveClock;
            float side = actSide;
            float dirX = mark.X >= npc.Center.X ? 1f : -1f;

            Hover(npc, target, new Vector2(0f, -400f), 0.03f, 4f);

            if (t < RpsPon)
                Track(target.Center);

            // Jan, ken, pon.
            if (At(RpsJan))
                Sound(SkeletronSounds.Jan, HandAt(npc, side));

            if (At(RpsKen))
                Sound(SkeletronSounds.Ken, HandAt(npc, side));

            if (At(RpsPon))
            {
                Sound(SkeletronSounds.Pon, HandAt(npc, side));

                if (Authority)
                {
                    pick = (byte)Main.rand.Next(1, 4);
                    Fix(npc, target.Center);
                }
            }

            SkeletronRig.Pose shown = pick == Paper ? SkeletronRig.Paper : pick == Scissors ? SkeletronRig.Scissors : SkeletronRig.Fist;
            Aim a;

            if (t < RpsPon)
            {
                // Shaken in time: down on every beat.
                float phase = (t % 18) / 18f;
                float drop = 24f * (float)Math.Sin(MathHelper.Pi * phase);
                a = Aim.At(mark + new Vector2(0f, -260f + drop), 0f, SkeletronRig.Fist, 0.3f);
                a.Hurt = 0f;
            }
            else if (t < RpsGo)
            {
                a = Aim.At(mark + new Vector2(0f, -240f), 0f, shown, 0.3f);
                a.Shape = 0.6f;
                a.Hurt = 0f;
                a.Shake = 1f;
            }
            else if (pick == Paper)
            {
                Vector2 from = mark + new Vector2(-dirX * 260f, PaperLow);

                if (t < RpsStrike - 2)
                    a = Aim.At(from, MathHelper.Pi, SkeletronRig.Flat, 0.35f);
                else if (t < RpsStrike + 14)
                    a = Aim.At(from + new Vector2(dirX * 120f * EaseOut((t - RpsStrike + 2) / 14f), 0f), MathHelper.Pi, SkeletronRig.Flat, 0.6f);
                else
                    a = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.15f);

                a.Hurt = 0f;
            }
            else if (pick == Scissors)
            {
                if (t < RpsStrike + 10)
                {
                    a = Aim.At(mark + new Vector2(side * 90f, -110f), SkeletronRig.AngleFor(new Vector2(-side, 1f)), t < RpsStrike + 2 ? SkeletronRig.Scissors : SkeletronRig.ScissorsShut, 0.3f);
                    a.Shape = t < RpsStrike + 2 ? 0.3f : 1f;
                }
                else
                {
                    a = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.15f);
                }

                a.Hurt = 0f;
            }
            else
            {
                // Rock: straight down onto them.
                float floor = GroundOr(mark.X, 20f, mark.Y, 400f);
                Vector2 over = mark + new Vector2(0f, -240f);
                Vector2 down = new Vector2(mark.X, floor - 30f);

                if (t < RpsStrike)
                {
                    a = Aim.At(Vector2.Lerp(over, down, EaseIn((t - RpsGo + 1) / (float)(RpsStrike - RpsGo))), 0f, SkeletronRig.Fist, 1f);
                    a.Blow = true;
                    a.Hurt = 1.6f;
                }
                else if (t < RpsStrike + 16)
                {
                    // Down: still a blow the tick it lands.
                    a = Aim.At(down, 0f, SkeletronRig.Fist, 1f);
                    a.Blow = (int)t == RpsStrike;
                    a.Hurt = (int)t == RpsStrike ? 1.6f : 0f;
                }
                else
                {
                    a = Aim.At(Rest(npc, side), RestAngle(npc, side), Hanging(side), 0.15f);
                    a.Hurt = 0f;
                }
            }

            aims[IndexOf(side)] = a;

            // Each, its blow.
            if (At(RpsGo) && pick == Scissors)
                Shoot(npc, mark, Vector2.Zero, ModContent.ProjectileType<SnipCut>(), Damage(npc, 20f, 17f), 12f);

            if (At(RpsStrike))
            {
                if (pick == Paper)
                {
                    Vector2 from = mark + new Vector2(-dirX * 260f, PaperLow);
                    Sound(SkeletronSounds.Whack, from);
                    Shoot(npc, from, new Vector2(dirX * 9f, 0f), ModContent.ProjectileType<PalmWave>(), Damage(npc, 15f, 12f));
                }
                else if (pick == Scissors)
                {
                    Sound(SkeletronSounds.Snip, mark);
                }
                else if (!LegendsSkeletronHand.Turned(HandOn(side)))
                {
                    float floor = GroundOr(mark.X, 20f, mark.Y, 400f);
                    Vector2 hit = new Vector2(mark.X, floor);

                    if (!Main.dedServ)
                    {
                        SoundEngine.PlaySound(SkeletronSounds.Thud, hit);
                        GroundWave.Slam(hit, 30f, 24, 20f, 30);
                        LegendsKingSlime.GroundDust(hit.X - 50f, hit.X + 50f, hit.Y, 0.9f);
                        Shake(hit, 9f, 18);
                    }

                    Shoot(npc, hit + new Vector2(0f, -20f), Vector2.Zero, ModContent.ProjectileType<SkeletronBlast>(), Damage(npc, 20f, 17f), 96f, SkeletronBlast.Bone);
                }
            }

            if (Authority && t >= RpsEnd)
                End(npc);
        }

        /// <summary>The reveal: a glint off the hand as it shows what it is.</summary>
        private void DrawRps(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < RpsPon || t >= RpsGo)
                return;

            NPC h = HandOn(actSide);

            if (h == null)
                return;

            float flash = 1f - (t - RpsPon) / (float)(RpsGo - RpsPon);
            SkeletronRig.Glint(spriteBatch, h.Center + new Vector2(20f, -30f), Color.White * flash, 70f * flash, t * 0.1f);
            SkeletronRig.Glow(spriteBatch, h.Center, new Color(255, 240, 200) * (0.3f * flash), 70f);
        }
    }
}
