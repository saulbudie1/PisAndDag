using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>How a thrown player moves through the air.</summary>
    public enum RagdollStyle
    {
        /// <summary>Tumbling end over end. A hit from something swinging: the whip.</summary>
        Spin,

        /// <summary>Rolling like a barrel, the turn tied to the ground covered. A shove, a low hit.</summary>
        Roll,
    }

    /// <summary>
    /// The ragdoll: what a hit that goes past knockback immunity does to the body.
    ///
    /// Three stages, none of them under the player's control. AIRBORNE: thrown with
    /// whatever velocity the hit gave, turning as it goes — spinning or rolling by the
    /// style — with no fall damage, because being thrown is not falling; it comes off
    /// walls, floors and ceilings with less than it hit them with, until a floor takes
    /// the last of it. DOWN: it lands,
    /// slides to a stop (a roll keeps rolling until it is slow), and lies flat for
    /// <see cref="lieTicks"/>, a number the hit chooses. UP: it gets to its feet over a
    /// few ticks and control comes back.
    ///
    /// Control is taken at the source — every control flag cleared in SetControls, items
    /// refused, building refused — rather than by fighting the movement code, so the
    /// engine's own gravity, collision and water still apply and the body goes where a
    /// body would go.
    ///
    /// The turn is the vanilla fullRotation, the same thing a minecart tilts the player
    /// with, about the middle of the body. It is set every tick because vanilla zeroes
    /// it in several places of its own.
    ///
    /// Local. The client that owns the player runs this; other clients see the throw's
    /// velocity through the ordinary position sync and nothing of the turn. A packet
    /// for the rotation is a later job if it ever matters.
    /// </summary>
    public class RagdollPlayer : ModPlayer
    {
        /// <summary>How long a hit leaves you on the ground if it does not say, in ticks. A second and a half.</summary>
        public const int DefaultLie = 90;

        /// <summary>Ticks spent getting back to your feet at the end, the turn easing back to upright.</summary>
        public const int GetUpTicks = 12;

        /// <summary>Radians a tick of tumble in the air, spinning. About a turn every second and a half.</summary>
        private const float SpinRate = 0.22f;

        /// <summary>The wheel a rolling body turns as, in pixels: one radian of roll per this much ground.</summary>
        private const float RollRadius = 14f;

        /// <summary>Sideways speed kept per tick while lying, and while still rolling. A roll runs on; a body that landed flat does not.</summary>
        private const float LieFriction = 0.82f;
        private const float RollFriction = 0.965f;

        /// <summary>A roll is over when it is slower than this, pixels a tick.</summary>
        private const float RollStops = 1.2f;

        /// <summary>Longest any ragdoll may last, so nothing can ever hold a player forever.</summary>
        private const int Longest = 600;

        // ---- bouncing -----------------------------------------------------------------
        //
        // The engine stops a player dead at a tile. A body thrown at a floor does not
        // stop dead, it comes off it with less than it arrived with; so every contact is
        // read after the move — the velocity that went in against the one that came out
        // — and what the engine took away is given back, reduced, the other way.

        /// <summary>Share of the vertical speed kept coming off a floor or a ceiling.</summary>
        private const float Bounce = 0.45f;

        /// <summary>Share of the sideways speed kept coming off a wall.</summary>
        private const float WallBounce = 0.5f;

        /// <summary>Share of the sideways speed kept across a floor bounce. Skidding costs.</summary>
        private const float BounceFriction = 0.75f;

        /// <summary>A floor hit that would come back up slower than this does not come back up: it lands.</summary>
        private const float RestSpeed = 2.5f;

        /// <summary>Speed the engine may take off a body in a tick without it having hit anything.</summary>
        private const float Slack = 0.05f;

        private const int None = 0, Airborne = 1, Down = 2, Up = 3;

        private int stage;
        private RagdollStyle style;
        private float angle;
        private float spin;
        private int lieTicks;
        private int left;
        private int total;
        private Vector2 before;

        // A throw asked for mid-hurt is held until the hurt is over. Vanilla writes its
        // own knockback onto the velocity AFTER the hit hooks run, so a throw applied in
        // OnHitPlayer was overwritten by a shove of three pixels a tick a moment later.
        // PostHurt is after that. That was why nothing flew.
        private bool pending;
        private Vector2 pendingVelocity;
        private RagdollStyle pendingStyle;
        private int pendingLie;

        /// <summary>True from the hit until the player is standing again.</summary>
        public bool Ragdolled => stage != None;

        /// <summary>True while the throw still has them in the air.</summary>
        public bool Flung => stage == Airborne;

        /// <summary>
        /// The fastest the game let them fall this tick, as it stood before the throw took
        /// the limit off (PostUpdateRunSpeeds): what the limit is lifted from, tick by tick.
        /// For anything that works out where a thrown player is going (Foigh.Ahead).
        /// </summary>
        public float FallLimit { get; private set; }

        /// <summary>
        /// Throws the player. The velocity is written as given; the style says how the
        /// body turns on the way; <paramref name="lie"/> is how long it stays down after
        /// landing, in ticks. Only the owning client may call this for its own player —
        /// velocity belongs to it.
        /// </summary>
        public void Throw(Vector2 velocity, RagdollStyle style, int lie = DefaultLie)
        {
            // Nothing throws the dead: a hit that killed them is over (and a throw held over
            // would go off on them where they come back).
            if (Player.whoAmI != Main.myPlayer || Player.dead)
                return;

            pending = true;
            pendingVelocity = velocity;
            pendingStyle = style;
            pendingLie = Math.Max(0, lie);
        }

        /// <summary>The held throw goes on after the hurt has finished with the velocity.</summary>
        public override void PostHurt(Player.HurtInfo info)
        {
            if (pending && !Player.dead)
                Launch();
        }

        /// <summary>
        /// Dead, nothing of it is kept: a dead player's update stops before PostUpdate (Player.Update),
        /// so a throw held over, or one in the air, would otherwise go on where they come back.
        /// </summary>
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            Stand();
        }

        public override void OnRespawn()
        {
            Stand();
        }

        private void Launch()
        {
            pending = false;

            if (Player.mount.Active)
                Player.mount.Dismount(Player);

            Player.velocity = pendingVelocity;
            Player.fallStart = (int)(Player.position.Y / 16f);

            style = pendingStyle;
            lieTicks = pendingLie;
            stage = Airborne;
            total = 0;
            before = pendingVelocity;

            // Tumble the way it is travelling: forward over the head. On screen that is
            // clockwise when thrown right.
            float way = pendingVelocity.X != 0f ? Math.Sign(pendingVelocity.X) : Player.direction;
            spin = style == RagdollStyle.Spin ? way * SpinRate : 0f;

            // A roll starts already lying; a spin starts upright and turns from there.
            angle = style == RagdollStyle.Roll ? way * MathHelper.PiOver2 : 0f;
        }

        public override void ResetEffects()
        {
            if (stage == None)
                return;

            // Every tick, because vanilla clears these every tick.
            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void SetControls()
        {
            if (stage == None)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override bool CanUseItem(Item item) => stage == None;

        public override void PostUpdateRunSpeeds()
        {
            // No braking in the air: what threw you is what carries you. On the ground
            // the stages below do their own friction.
            if (stage != Airborne)
                return;

            Player.runSlowdown = 0f;

            // No terminal velocity on the way down. Vanilla clamps the fall to
            // maxFallSpeed every tick, which turned a downward throw into a drift and
            // the bounce off the floor into nothing. What threw you sets the speed.
            FallLimit = Player.maxFallSpeed;
            Player.maxFallSpeed = Math.Max(Player.maxFallSpeed, Math.Abs(Player.velocity.Y) + 1f);
        }

        /// <summary>What it was doing before the engine moved it and stopped it on whatever it hit.</summary>
        public override void PreUpdateMovement()
        {
            if (stage == Airborne)
                before = Player.velocity;
        }

        public override void PostUpdate()
        {
            // A throw asked for outside a hurt has nothing to wait for.
            if (pending)
                Launch();

            if (stage == None)
                return;

            if (++total > Longest || Player.dead)
            {
                Stand();
                return;
            }

            // Water takes the fall out of it. Nothing to land on, so nothing to lie on.
            if (Player.wet && stage == Airborne)
            {
                Stand();
                return;
            }

            switch (stage)
            {
                case Airborne:
                    Fly();
                    break;

                case Down:
                    Lie();
                    break;

                case Up:
                    GetUp();
                    break;
            }

            Player.fullRotation = angle;
            Player.fullRotationOrigin = new Vector2(Player.width * 0.5f, Player.height * 0.5f);
        }

        private void Fly()
        {
            // Thrown, not dropped: the landing is not a fall.
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (style == RagdollStyle.Spin)
                angle += spin;
            else
                angle += Player.velocity.X / RollRadius;

            Vector2 now = Player.velocity;

            // Contact is read as speed the engine took away, not as speed at zero: over
            // sixteen a tick the engine moves the body in sixteen pixel steps and hands
            // back the sum of the steps it managed, so a body that met the floor on the
            // third step of five still comes out with two steps of fall on it.

            // A wall: sideways speed went in and less came out.
            if (Math.Abs(now.X) < Math.Abs(before.X) - Slack)
            {
                Player.velocity.X = -before.X * WallBounce;
                spin = -spin * WallBounce;
                Puff();
            }

            // A ceiling: went up, lost some of it.
            if (before.Y < 0f && now.Y > before.Y + Slack)
            {
                Player.velocity.Y = -before.Y * Bounce;
                Puff();
                return;
            }

            // A floor: went down, stopped. Comes back up with less, or if there is not
            // enough left to come back up with, this is where it lies.
            if (before.Y > 0f && now.Y < before.Y - Slack)
            {
                float up = before.Y * Bounce;

                if (up < RestSpeed)
                {
                    Land();
                    return;
                }

                Player.velocity.Y = -up;
                Player.velocity.X *= BounceFriction;

                // It tumbles again off the floor, the way it is now going, less.
                float way = Player.velocity.X != 0f ? Math.Sign(Player.velocity.X) : Math.Sign(spin);
                float strength = MathHelper.Clamp(Math.Abs(Player.velocity.X) / 12f, 0.3f, 1f);
                spin = style == RagdollStyle.Spin ? way * SpinRate * strength : 0f;

                Puff();
            }
        }

        private void Puff()
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 6; i++)
            {
                Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Smoke,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0f), 120, default, 0.9f);

                dust.noGravity = true;
            }
        }

        private void Land()
        {
            stage = Down;
            left = lieTicks;

            if (style == RagdollStyle.Spin)
            {
                // Whichever way it is nearer to lying, it lies that way.
                float flat = MathHelper.WrapAngle(angle);
                angle = flat >= 0f ? MathHelper.PiOver2 : -MathHelper.PiOver2;
            }

            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Player.BottomLeft - new Vector2(0f, 6f), Player.width, 6, DustID.Smoke,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.2f, -0.2f), 120, default, 1.1f);

                dust.noGravity = true;
            }
        }

        private void Lie()
        {
            bool rolling = style == RagdollStyle.Roll && Math.Abs(Player.velocity.X) > RollStops;

            if (rolling)
            {
                // Still rolling: the turn follows the ground, the clock does not run.
                angle += Player.velocity.X / RollRadius;
                Player.velocity.X *= RollFriction;
                return;
            }

            Player.velocity.X *= LieFriction;

            // Settle flat on the nearest side, in case the roll stopped mid-turn.
            float flat = MathHelper.WrapAngle(angle);
            float target = flat >= 0f ? MathHelper.PiOver2 : -MathHelper.PiOver2;
            angle = MathHelper.Lerp(WrapTo(angle, target), target, 0.3f);

            if (--left <= 0)
            {
                stage = Up;
                left = GetUpTicks;
            }
        }

        private void GetUp()
        {
            Player.velocity.X *= LieFriction;

            float t = 1f - left / (float)GetUpTicks;
            float from = MathHelper.WrapAngle(angle) >= 0f ? MathHelper.PiOver2 : -MathHelper.PiOver2;
            angle = MathHelper.Lerp(from, 0f, t * t * (3f - 2f * t));

            if (--left <= 0)
                Stand();
        }

        /// <summary>
        /// On their feet at once, whatever of this was going on or still to come: for a cut,
        /// where the player is put down somewhere else and is to be standing there. Only the
        /// owning client, as with a throw.
        /// </summary>
        public void StandNow()
        {
            if (Player.whoAmI == Main.myPlayer)
                Stand();
        }

        private void Stand()
        {
            pending = false;
            stage = None;
            angle = 0f;
            spin = 0f;
            Player.fullRotation = 0f;
            Player.fullRotationOrigin = Vector2.Zero;
        }

        /// <summary>The angle, re-expressed within half a turn of the target, so a lerp between them takes the short way.</summary>
        private static float WrapTo(float angle, float target)
        {
            return target + MathHelper.WrapAngle(angle - target);
        }
    }
}
