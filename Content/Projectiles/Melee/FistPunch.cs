using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Melee.Fists;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// One blow of a fist weapon (FistWeapon): ai[0] the step of the combo it is, ai[1] its length in ticks, ai[2] the
    /// weapon's item type; its velocity is only the way it was aimed.
    ///
    /// The whole arm throws it (asked: "move the whole arm not js the fist as a projectile"), and the arm is the player's
    /// own, posed from the arm map (asked: "figure out what arm pos do what like bent, extend, you can rotate them and
    /// stretch and warp them"; FistArms, laid over the game's arm by FistArmLayers). A straight blow from the front arm
    /// starts from the guard (the walk's forward-bent arm turned up, the fist before the chest), cocks it a little and
    /// squashes it, throws it out straight at the cursor stretched long and thin, springs it back to its length as it
    /// lands, and folds it back to the guard. A blow from the back arm comes out from behind the body the same way and
    /// goes back behind it (asked: "bottom arm doesnt have to show"): the back arm is otherwise the game's own, hidden,
    /// and while it throws, the front arm keeps the guard. An uppercut drops the arm down and out and sweeps it up through
    /// straight out to up and out, longest at the middle of the sweep. While the arm strikes, faint copies of it part way
    /// back to last tick's pose trail it in the weapon's colour (the smear). Past the hand the weapon's forearm runs on
    /// unbroken to the fist at the end of the reach (FistWeapon.ArmTexture); the fist grows from the hand as it goes out.
    ///
    /// It hits anything on the line from the shoulder to the fist while it strikes (or that the fist swept through on
    /// the way up), each NPC once a blow. On a hit the blow stops dead for a few ticks (the weight of it), told to the
    /// other machines; the screen shakes a little; what it hit is held off the player for a while (FistPlayer).
    ///
    /// The step (asked: "make him walk forward and back, (doesnt affect movement when running or flying)"): from standing,
    /// the player walks in as the arm goes out and back out as it comes in, the game's own walking (legs and all), to
    /// where the step wants them and no further, and back to where they began. Running, jumping, a mount, or being
    /// knocked about ends it where it is.
    /// </summary>
    public class FistPunch : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        private int Step => (int)Projectile.ai[0];
        private int Ticks => Math.Max(4, (int)Projectile.ai[1]);
        private int WeaponType => (int)Projectile.ai[2];

        /// <summary>Ticks it has run, less the ticks it stood still on a hit. Told to the other machines with the stop.</summary>
        private float Time
        {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        /// <summary>Ticks left of the stop on a hit.</summary>
        private int stop;

        /// <summary>The fist's place last tick, for what it swept through; its last two places and turns, for its smear.</summary>
        private Vector2 lastFist, sweptFrom;
        private readonly Vector2[] fistTrail = new Vector2[2];
        private readonly float[] fistTrailTurn = new float[2];
        private readonly float[] fistTrailScale = new float[2];
        private int fistTrailCount;
        private bool started;

        /// <summary>The step: whether this blow walks the player (decided as it is thrown), where they began, and the speed it last gave them.</summary>
        private bool stepping;
        private float stepFrom;
        private float stepSet;

        /// <summary>How thick the line it hits along is.</summary>
        private const float Thickness = 18f;

        /// <summary>Ticks a hit stops it for. Mine.</summary>
        private const int HitStop = 4;

        /// <summary>The guard's turn: the walk's forward-bent arm (FistArms.Guard) turned up so the fist is before the chest. Mine, from the preview.</summary>
        private const float GuardTurn = -0.55f;

        /// <summary>Where an uppercut's sweep starts, under the line ahead, and where it ends, over it.</summary>
        private static readonly float RiseFrom = MathHelper.ToRadians(45f), RiseTo = MathHelper.ToRadians(-80f);

        /// <summary>How small the fist is at the hand, before it goes out.</summary>
        private const float FistAtHand = 0.55f;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 2;

            // Drawn with its player only, over the arm that throws it (not again among the other projectiles).
            Projectile.hide = true;
            DrawHeldProjInFrontOfHeldItemAndArms = true;
        }

        public FistWeapon Weapon => WeaponType > 0 && WeaponType < ItemLoader.ItemCount
            ? ContentSamples.ItemsByType[WeaponType].ModItem as FistWeapon : null;

        private FistMove Move(FistWeapon weapon)
        {
            FistMove[] combo = weapon.Combo;
            return combo[Math.Clamp(Step, 0, combo.Length - 1)];
        }

        /// <summary>How far through the blow, 0 to 1.</summary>
        private float Progress => MathHelper.Clamp(Time / Ticks, 0f, 1f);

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float WindEnd(float strike) => strike * 0.45f;

        private static float HoldEnd(float strike) => Math.Min(0.92f, strike + 0.18f);

        /// <summary>
        /// How far out the fist is at <paramref name="p"/>, 0 at the hand to 1 at full reach, a little under 0 in the
        /// wind-up: back, out fast, held, back again.
        /// </summary>
        public static float Extension(float p, float strike)
        {
            float windEnd = WindEnd(strike), holdEnd = HoldEnd(strike);

            if (p < windEnd)
                return -0.18f * (float)Math.Sin(p / windEnd * MathHelper.PiOver2);

            if (p < strike)
            {
                float t = (p - windEnd) / (strike - windEnd);
                t = 1f - (1f - t) * (1f - t) * (1f - t);
                return MathHelper.Lerp(-0.18f, 1f, t);
            }

            if (p < holdEnd)
                return 1f;

            float r = (p - holdEnd) / (1f - holdEnd);
            return 1f - r * r * (3f - 2f * r);
        }

        /// <summary>Whether it hits at <paramref name="p"/>: from the last stretch of its way out to the end of its hold.</summary>
        public static bool Striking(float p, float strike) => p > strike * 0.8f && p < HoldEnd(strike);

        /// <summary>
        /// How far into its step the blow is at <paramref name="p"/>, 0 to 1: still through the wind-up, in as the arm goes
        /// out, held, back out as it comes in.
        /// </summary>
        public static float StepAt(float p, float strike)
        {
            float windEnd = WindEnd(strike), holdEnd = HoldEnd(strike);

            if (p <= windEnd)
                return 0f;

            if (p < strike)
                return Smooth((p - windEnd) / (strike - windEnd));

            if (p < holdEnd)
                return 1f;

            return 1f - Smooth((p - holdEnd) / (1f - holdEnd));
        }

        /// <summary>The way the blow goes at <paramref name="p"/>, in the world: the aim for a straight one; from under, forward, up to over the head for a rising one.</summary>
        private float Angle(FistMove move, float p, int facing)
        {
            float aim = Projectile.velocity.ToRotation();

            if (move.Arc == FistArc.Straight)
                return aim;

            float t = Smooth((p - WindEnd(move.Strike)) / (HoldEnd(move.Strike) - WindEnd(move.Strike)));
            float a = MathHelper.Lerp(RiseFrom, RiseTo, t);
            return facing > 0 ? a : MathHelper.Pi - a;
        }

        /// <summary>A world angle as if the player faced right and stood the right way up.</summary>
        private static float Upright(Player player, float world)
        {
            float upright = player.direction == 1 ? world : MathHelper.Pi - world;
            return player.gravDir == -1f ? -upright : upright;
        }

        // ---- the arms ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The striking arm's pose at <paramref name="p"/>, the blow going along <paramref name="aim"/> (as if facing
        /// right), and whether it smears; null while the back arm is behind the body, the game's own.
        /// </summary>
        private static ArmPose? Striker(FistMove move, float p, float aim, out bool smear)
        {
            float windEnd = WindEnd(move.Strike), holdEnd = HoldEnd(move.Strike);
            smear = false;

            if (move.Arc == FistArc.Rising)
            {
                if (p < windEnd)
                {
                    // Dropped down and out, loaded: squashed.
                    float t = Smooth(p / windEnd);
                    return ArmPose.Aimed(FistArms.DownAndOut, MathHelper.Lerp(0.6f, RiseFrom + 0.2f, t),
                        Vector2.Lerp(Vector2.One, new Vector2(0.86f, 1.08f), t));
                }

                if (p < holdEnd)
                {
                    // The sweep: down and out, straight out, up and out as it rises past each; longest at its middle.
                    float swing = (p - windEnd) / (holdEnd - windEnd);
                    float s = (float)Math.Sin(MathHelper.Pi * swing);
                    ArmFrame frame = aim > 0.45f ? FistArms.DownAndOut : aim > -0.35f ? FistArms.StraightOut : FistArms.UpAndOut;
                    smear = swing > 0.1f && swing < 0.85f;
                    return ArmPose.Aimed(frame, aim, new Vector2(1f + 0.3f * s, 1f - 0.13f * s));
                }

                // Back down to the guard.
                float r = Smooth((p - holdEnd) / (1f - holdEnd));

                if (r < 0.5f)
                    return ArmPose.Aimed(FistArms.UpAndOut, MathHelper.Lerp(aim, -0.6f, r * 2f), new Vector2(MathHelper.Lerp(1f, 0.85f, r * 2f), 1f));

                return new ArmPose(FistArms.Guard, MathHelper.Lerp(GuardTurn - 0.5f, GuardTurn, (r - 0.5f) * 2f), Vector2.One);
            }

            if (p < windEnd)
            {
                // The back arm waits behind the body; the front arm cocks its guard, squashed.
                if (move.BackArm)
                    return null;

                float t = Smooth(p / windEnd);
                return new ArmPose(FistArms.Guard, GuardTurn - 0.25f * t, Vector2.Lerp(Vector2.One, new Vector2(0.85f, 1.08f), t));
            }

            if (p < move.Strike)
            {
                // Out: straight at the cursor, stretched long and thin as it goes.
                float t = (p - windEnd) / (move.Strike - windEnd);
                float e = 1f - (1f - t) * (1f - t) * (1f - t);
                smear = t > 0.2f;
                return ArmPose.Aimed(FistArms.StraightOut, aim, new Vector2(MathHelper.Lerp(0.75f, 1.32f, e), MathHelper.Lerp(1.06f, 0.86f, e)));
            }

            if (p < holdEnd)
            {
                // Landed: springing back to its own length.
                float t = (p - move.Strike) / (holdEnd - move.Strike);
                float k = (1f - t) * (1f - t);
                smear = t < 0.35f;
                return ArmPose.Aimed(FistArms.StraightOut, aim, new Vector2(1f + 0.32f * k * (float)Math.Cos(t * 7f), 1f - 0.14f * k));
            }

            // In again: the back arm shortening behind the body; the front arm to the guard.
            float b = Smooth((p - holdEnd) / (1f - holdEnd));

            if (move.BackArm)
                return b < 0.75f ? ArmPose.Aimed(FistArms.StraightOut, aim + 0.3f * b, new Vector2(MathHelper.Lerp(1f, 0.6f, b / 0.75f), 1f)) : null;

            if (b < 0.5f)
                return ArmPose.Aimed(FistArms.StraightOut, aim + 0.35f * b, new Vector2(MathHelper.Lerp(1f, 0.78f, b * 2f), 1f));

            return new ArmPose(FistArms.Guard, MathHelper.Lerp(GuardTurn + 0.3f, GuardTurn, (b - 0.5f) * 2f), Vector2.One);
        }

        /// <summary>The front arm while the back arm throws: its guard up, breathing a little with the blow.</summary>
        private static ArmPose Guarding(float p) => new ArmPose(FistArms.Guard, GuardTurn + 0.06f * (float)Math.Sin(p * MathHelper.TwoPi), Vector2.One);

        /// <summary>
        /// Where the arm and fist are at <paramref name="p"/>: the shoulder, the hand, the fist, the way the blow goes in
        /// the world, the striking arm's pose (null: behind the body, the fist not out).
        /// </summary>
        private void Pose(Player player, FistWeapon weapon, FistMove move, float p, out Vector2 shoulder, out Vector2 hand,
            out Vector2 fist, out float angle, out ArmPose? striker, out bool smear)
        {
            int facing = Projectile.velocity.X >= 0f ? 1 : -1;
            angle = Angle(move, p, facing);
            striker = Striker(move, p, Upright(player, angle), out smear);
            shoulder = FistArms.ShoulderAt(player, move.BackArm);

            if (!striker.HasValue)
            {
                hand = fist = shoulder;
                return;
            }

            hand = FistArms.HandAt(player, move.BackArm, striker.Value);

            // The forearm runs on from the hand, the way the blow goes, to the reach.
            Vector2 dir = angle.ToRotationVector2();
            float reach = weapon.Reach * move.Reach * Math.Max(0f, Extension(p, move.Strike));
            float handOut = Vector2.Dot(hand - shoulder, dir);
            fist = hand + dir * Math.Max(0f, reach - handOut);
        }

        // ---- the blow -----------------------------------------------------------------------------------------------

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            FistWeapon weapon = Weapon;

            if (!player.active || player.dead || player.CCed || weapon == null)
            {
                Projectile.Kill();
                return;
            }

            FistMove move = Move(weapon);
            int facing = Projectile.velocity.X >= 0f ? 1 : -1;

            if (!started)
            {
                started = true;

                if (!Main.dedServ)
                    SoundEngine.PlaySound(weapon.Swing(move, Step), player.Center);

                // The step in and out: only from standing on the ground.
                stepping = Projectile.owner == Main.myPlayer && move.Step > 0f && Afoot(player);
                stepFrom = player.position.X;
            }

            if (stop > 0)
                stop--;
            else
                Time++;

            if (Time >= Ticks)
            {
                Projectile.Kill();
                return;
            }

            float p = Progress;

            Projectile.timeLeft = 2;
            player.ChangeDir(facing);
            player.heldProj = Projectile.whoAmI;
            player.itemTime = Math.Max(player.itemTime, 2);
            player.itemAnimation = Math.Max(player.itemAnimation, 2);

            if (stepping)
                StepWith(player, move, p, facing);

            Pose(player, weapon, move, p, out Vector2 shoulder, out Vector2 hand, out Vector2 fist, out float angle, out ArmPose? striker, out bool smear);

            // The arms: posed for the drawing (FistArmLayers); the back arm left to the game while it is behind the body.
            FistPlayer fists = player.GetModPlayer<FistPlayer>();
            fists.Smear = weapon.Reaching;

            if (move.BackArm)
            {
                if (striker.HasValue)
                    fists.PoseBack(striker.Value, smear);

                fists.PoseFront(Guarding(p), false);
            }
            else
                fists.PoseFront(striker.Value, smear);

            // And as near as the game's own turning arm can come, for anything else that asks where the hand is.
            float armRotation = VisusGrip.ArmRotation(player, Upright(player, angle));
            Player.CompositeArmStretchAmount stretch = Extension(p, move.Strike) > 0.6f ? Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.Quarter;

            if (move.BackArm)
                player.SetCompositeArmBack(striker.HasValue, stretch, armRotation);
            else
                player.SetCompositeArmFront(true, stretch, armRotation);

            if (lastFist == Vector2.Zero)
                lastFist = fist;

            Projectile.Center = fist;
            Projectile.rotation = angle;
            Projectile.spriteDirection = facing;

            // The fist's smear: its last two places, kept while it strikes.
            if (smear && striker.HasValue)
            {
                fistTrail[1] = fistTrail[0];
                fistTrailTurn[1] = fistTrailTurn[0];
                fistTrailScale[1] = fistTrailScale[0];
                fistTrail[0] = lastFist;
                fistTrailTurn[0] = Projectile.oldRot[0];
                fistTrailScale[0] = FistScale(Extension(Math.Max(0f, p - 1f / Ticks), move.Strike));
                fistTrailCount = Math.Min(2, fistTrailCount + 1);
            }
            else
                fistTrailCount = 0;

            Projectile.oldRot[0] = angle;

            bool striking = Striking(p, move.Strike);

            if (Projectile.owner == Main.myPlayer)
                fists.Guard(facing, weapon.Reach * move.Reach);

            if (striker.HasValue)
                weapon.Shed(player, move, Step, hand, fist, Extension(p, move.Strike), striking);

            if (striking)
                Lighting.AddLight(fist, weapon.Reaching.ToVector3() * 0.35f);

            sweptFrom = lastFist;
            lastFist = fist;
        }

        /// <summary>How big the fist is, from the hand (small) to full reach.</summary>
        private static float FistScale(float extension) => MathHelper.Lerp(FistAtHand, 1f, MathHelper.Clamp(extension, 0f, 1f));

        // ---- the step -------------------------------------------------------------------------------------------------

        /// <summary>
        /// Standing on the ground: not on a mount or a hook, not moving, not holding a way to go or jump. The only time a
        /// blow steps (asked: running and flying are left alone).
        /// </summary>
        private static bool Afoot(Player player) =>
            player.velocity.Y == 0f && !player.controlLeft && !player.controlRight && !player.controlJump
            && Math.Abs(player.velocity.X) < 1.6f && !player.mount.Active && player.grappling[0] < 0 && !player.pulley;

        /// <summary>
        /// The step, walked: from where the player has got to toward where the step wants them next tick, the game's own
        /// walking carrying them (its slowdown put on first, as it takes it off before it moves them), so their legs walk
        /// it and walls and slopes stop it as they would. Running, jumping, a mount, or something else moving them (a
        /// knock) ends it where it is.
        /// </summary>
        private void StepWith(Player player, FistMove move, float p, int facing)
        {
            if (player.velocity.Y != 0f || player.controlLeft || player.controlRight || player.controlJump || player.mount.Active
                || Math.Abs(player.velocity.X) > Math.Abs(stepSet) + 2f)
            {
                stepping = false;
                return;
            }

            float done = (player.position.X - stepFrom) * facing;
            float want = move.Step * StepAt(MathHelper.Clamp(p + 1f / Ticks, 0f, 1f), move.Strike);
            float speed = MathHelper.Clamp(want - done, -4f, 4f) * facing;

            stepSet = Math.Abs(speed) < 0.05f ? 0f : speed + Math.Sign(speed) * player.runSlowdown;
            player.velocity.X = stepSet;
        }

        // ---- hitting ----------------------------------------------------------------------------------------------------

        public override bool? CanDamage()
        {
            FistWeapon weapon = Weapon;
            return weapon != null && Striking(Progress, Move(weapon).Strike) ? null : false;
        }

        public override bool? CanCutTiles() => false;

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            FistWeapon weapon = Weapon;

            if (weapon == null)
                return false;

            FistMove move = Move(weapon);
            Vector2 shoulder = FistArms.ShoulderAt(player, move.BackArm);
            float point = 0f;

            // The line out to the fist; and, rising, what the fist swept through since last tick.
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), shoulder, Projectile.Center, Thickness, ref point))
                return true;

            return move.Arc == FistArc.Rising
                && Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), sweptFrom, Projectile.Center, Thickness * 1.4f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Knocked the way the blow goes.
            modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Main.player[Projectile.owner];
            FistWeapon weapon = Weapon;

            if (weapon == null)
                return;

            FistMove move = Move(weapon);
            int facing = Projectile.velocity.X >= 0f ? 1 : -1;

            // The stop, told to the other machines so the arm is where it is here.
            stop = HitStop;
            Projectile.netUpdate = true;
            player.GetModPlayer<FistPlayer>().Struck(target);

            // A throw (an uppercut's), by how far this one can be pushed: nothing that will not be, nothing at all for a
            // boss or a part of one, less the bigger it is.
            if (move.Throw != Vector2.Zero && target.knockBackResist > 0f && !FistPlayer.Heavy(target))
            {
                float size = MathHelper.Clamp((float)Math.Sqrt(900f / Math.Max(1f, target.width * target.height)), 0.45f, 1.15f);
                float weight = MathHelper.Clamp(target.knockBackResist, 0f, 1f) * size;
                Vector2 throwing = new Vector2(move.Throw.X * facing, move.Throw.Y * player.gravDir) * (0.35f + 0.65f * weight);
                Items.Misc.Harenaeum.Launch(target, throwing);
            }

            Vector2 at = Vector2.Clamp(Projectile.Center, target.Hitbox.TopLeft(), target.Hitbox.BottomRight());

            if (Main.myPlayer == Projectile.owner)
                player.PIS().BumpScreenShake(move.Damage >= 2f ? 6f : 2.5f, move.Damage >= 2f ? 10 : 5);

            weapon.OnFistHit(player, target, move, Step, hit, at);
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Time);
            writer.Write((byte)Math.Clamp(stop, 0, 255));
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Time = reader.ReadSingle();
            stop = reader.ReadByte();
        }

        // ---- drawing ----------------------------------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            FistWeapon weapon = Weapon;

            if (weapon == null || !ModContent.HasAsset(weapon.FistTexture))
                return false;

            FistMove move = Move(weapon);
            float p = Progress;
            Pose(player, weapon, move, p, out _, out Vector2 hand, out Vector2 at, out float angle, out ArmPose? striker, out _);

            // Behind the body with the back arm: nothing out.
            if (!striker.HasValue)
                return false;

            Texture2D fist = ModContent.Request<Texture2D>(weapon.FistTexture).Value;
            Vector2 dir = angle.ToRotationVector2();
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Vector2 origin = new Vector2(fist.Width * 0.75f, fist.Height * 0.5f);

            // The fist and forearm stretch with the arm as it strikes; the fist grows from the hand as it goes out.
            float stretch = 1f + (striker.Value.Scale.X - 1f) * 0.6f;
            float thin = 1f / (float)Math.Sqrt(Math.Max(0.5f, stretch));
            float grown = FistScale(Extension(p, move.Strike));
            Vector2 fistScale = new Vector2(grown * stretch, grown * thin) * Projectile.scale;

            // The fist's smear: where it was, faint, adding light.
            for (int i = fistTrailCount - 1; i >= 0; i--)
            {
                Color ghost = weapon.Reaching * (i == 0 ? 0.35f : 0.18f);
                ghost.A = 0;
                float s = fistTrailScale[i];
                Main.EntitySpriteDraw(fist, fistTrail[i] - Main.screenPosition, null, ghost, fistTrailTurn[i], origin,
                    new Vector2(s * stretch, s * thin) * Projectile.scale, flip, 0);
            }

            // The forearm, from the hand to the back of the fist, unbroken: the weapon's picture laid end to end along
            // it, or a band of its colour if it has none.
            Vector2 wrist = at - dir * (fist.Width * 0.75f - 2f) * fistScale.X;
            float length = Vector2.Dot(wrist - hand, dir);

            if (length > 1f)
            {
                if (weapon.ArmTexture != null && ModContent.HasAsset(weapon.ArmTexture))
                {
                    Texture2D arm = ModContent.Request<Texture2D>(weapon.ArmTexture).Value;

                    for (float done = 0f; done < length; done += arm.Width)
                    {
                        int width = (int)Math.Min(arm.Width, Math.Ceiling(length - done));
                        Vector2 from = hand + dir * done;
                        Color lit = Lighting.GetColor((from + dir * width * 0.5f).ToTileCoordinates());
                        Main.EntitySpriteDraw(arm, from - Main.screenPosition, new Rectangle(0, 0, width, arm.Height), lit, angle,
                            new Vector2(0f, arm.Height * 0.5f), new Vector2(Projectile.scale, Projectile.scale * thin), flip, 0);
                    }
                }
                else
                {
                    Color band = weapon.Reaching * 0.8f;
                    Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, hand - Main.screenPosition, new Rectangle(0, 0, 1, 1), band, angle,
                        new Vector2(0f, 0.5f), new Vector2(length, 6f), SpriteEffects.None, 0);
                }

                // As it strikes, a shine run out along it to the fist.
                if (Striking(p, move.Strike))
                {
                    Color shine = weapon.Reaching * 0.45f;
                    shine.A = 0;
                    Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, hand - Main.screenPosition, new Rectangle(0, 0, 1, 1), shine, angle,
                        new Vector2(0f, 0.5f), new Vector2(length, 2f), SpriteEffects.None, 0);
                }
            }

            // The fist, at the end of it, as the light falls on it.
            Color fistLit = Lighting.GetColor(at.ToTileCoordinates());
            Main.EntitySpriteDraw(fist, at - Main.screenPosition, null, fistLit, angle, origin, fistScale, flip, 0);

            return false;
        }
    }
}
