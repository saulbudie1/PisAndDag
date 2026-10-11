using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.NPCs.Foigh;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Foigh's Rust Side Sword in the hand: there for as long as the item is the one held (the
    /// idle holdout's own rule, BaseIdleHoldoutProjectile), and the weapon itself, as the Iron
    /// Flow's is (IronFlowHoldout, whose ways this keeps). It lies on the shoulder; it cuts the
    /// chain, goes up into the air, leaps, drives itself into the ground and is focused on
    /// (RustSideMoves has every pose, RustSideState where it is in them); the item tells it
    /// which (<see cref="StartCut"/>, <see cref="StartAir"/>), on its owner's machine, and Enter
    /// held tells it to focus (<see cref="Focus"/>).
    ///
    /// ai[0] is the move it is in and a count that goes up with every move begun, so that
    /// another machine sees the same move twice as two; ai[1] how many ticks the move takes;
    /// ai[2] the side it faces; and, sent with them, the way a cut in the air or the thrust is
    /// aimed. The ticks into a move are counted on each machine for itself, from when it hears
    /// of it.
    ///
    /// The chain is made at an enemy where there is one near (<see cref="ChainTarget"/>, by
    /// request: "smart CHAIN auto aim so when im under a boss, i dont just go out of him"):
    /// facing it, stepping no nearer than just short of it and never past it, and thrusting at
    /// it; the leap goes up at one over their head rather than out from under it.
    ///
    /// Drenched, every blow of it, and of the slashes and straight cuts it lets out, leaves the
    /// enemy bleeding (UltraBleedingGeneric).
    ///
    /// What moves its owner is done on its owner's machine (the game tells the others where
    /// they are): each cut's step, as the Iron Flow's; the jump into the air and the hanging
    /// there; the leap's flight and the fall onto the point; and, while focusing and while the
    /// sword is in the ground, nothing at all (RustSidePose holds them still). The roll over in
    /// the air is the player's whole body turned (Player.fullRotation), worked out on every
    /// machine from the move, as the pose is.
    ///
    /// It cuts along the blade (from <see cref="Inner"/> out to <see cref="Outer"/>,
    /// <see cref="Width"/> wide) over the whole of its way since last tick, turning, rolling
    /// and carried, each thing once a cut, only what its owner can see, and through all defense
    /// (by request). Its damage, knockback, critical chance and armour penetration are its
    /// owner's with the item, read again every tick; the slashes and the straight cuts it lets
    /// out take theirs from it as they leave.
    ///
    /// Drawn in the hand by RustSideLayer, from the pose this sets every tick (RustSidePose);
    /// this draws what the blade leaves behind when it is fast, and the black and red on it
    /// while it is drenched (PreDraw, behind the player).
    /// </summary>
    public class RustSideHoldout : BaseIdleHoldoutProjectile
    {
        public const string SwordTexture = "PisAndDag/Content/Items/Melee/FoighsRustSideSword";
        public const string BloodTexture = FoighRig.Folder + FoighRig.BloodSword;

        public override string Texture => SwordTexture;

        public override int AssociatedItemID => ModContent.ItemType<FoighsRustSideSword>();
        public override int IntendedProjectileType => ModContent.ProjectileType<RustSideHoldout>();

        // ---- the sprite (his sword's: FoighRig's numbers, 90 square, on the diagonal, point up and right) ----

        public const int Size = 90;

        /// <summary>The middle of the grip, where the hand holds it, in texels from the sprite's top left.</summary>
        public static readonly Vector2 Grip = new Vector2(20f, 70f);

        /// <summary>From the grip along the blade: to the point, and to the guard.</summary>
        public const float ToTip = FoighRig.GripToTip;
        public const float ToGuard = FoighRig.GripToGuard;

        /// <summary>What it cuts with: from this far out from the hand to this far (a little past the point), this wide, in pixels.</summary>
        private const float Inner = 12f, Outer = ToTip + 4f, Width = 30f;

        /// <summary>The trail: from and to this far out from the hand, over this many ticks of the blade's way, gone this many ticks after the blade slows.</summary>
        private const float TrailInner = ToGuard + 6f, TrailOuter = ToTip;
        private const float TrailTicks = 4f;
        private const float FadeTicks = 7f;

        /// <summary>Ticks after a cut ends within which the next follows it; after that, the first again.</summary>
        public const int Rhythm = 40;

        /// <summary>Ticks after a cut in the air ends within which the next can follow it.</summary>
        public const int AirGrace = 8;

        /// <summary>The fastest a step may carry its owner, pixels a tick.</summary>
        private const float StepSpeedCap = 12f;

        /// <summary>The leap up into the air (from the ground; from the air), the pop up at each next cut, and the slowest fall while hanging there, pixels a tick.</summary>
        private const float AirJump = 9.5f, AirLift = 6f, AirPop = 3.5f, HoverFall = 0.5f;

        /// <summary>The leap: the farthest enemy it goes for (to the nearest of it); how far it goes with none; how high over the line it goes, the least and the most.</summary>
        public const float LeapReach = 640f;
        public const float LeapLacking = 160f;
        private const float LeapRiseLeast = 32f, LeapRiseMost = 140f;

        /// <summary>The leap at an enemy over their head: a player this far in from its sides is under it; and they go up to this far under it (their head from its underside).</summary>
        private const float UnderInset = 4f, UnderGap = 12f;

        /// <summary>
        /// The chain's aim (by request: "smart CHAIN auto aim so when im under a boss, i dont
        /// just go out of him"): the enemy a cut is made at is one within this many pixels of
        /// its wielder's middle (to the nearest of it: eight tiles); and a step stops this many
        /// pixels short of touching it.
        /// </summary>
        public const float AimReach = 128f;
        private const float StepShort = 6f;

        /// <summary>The dive onto the point, pixels a tick.</summary>
        private const float DiveSpeed = 9f;

        /// <summary>
        /// The drive: how many straight cuts open out of the ground, within how many ticks of
        /// the first the rest are let out (at uneven ticks); how far from the blade they reach
        /// (fifteen tiles: by request, "increase radius of the slashes", from his own driven
        /// sword's ten); how near the blade, as a share of that, the point is that one not at an
        /// enemy goes through; and the least share of the line across the circle that a cut is,
        /// and how much of it is kept either side of its point.
        /// </summary>
        public const int LineCount = 12;
        private const int LineSpread = 36;
        public const float LineRadius = 240f;
        private const float LineMiddle = 0.7f;
        private const float CutShortest = 0.45f;
        private const float CutKeep = 24f;

        private static readonly Color[] SteelSparks = { new Color(255, 255, 255), new Color(214, 226, 240), new Color(255, 214, 160) };
        private static readonly Color[] BloodSparks = { new Color(255, 96, 60), new Color(255, 160, 120), new Color(206, 22, 28) };

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> line;

        private RustMove Told => (RustMove)((int)Projectile.ai[0] & 15);

        private float ToldTicks => Math.Max(1f, Projectile.ai[1]);

        private int Facing => Projectile.ai[2] < 0f ? -1 : 1;

        private readonly RustSideState state = new RustSideState();

        private bool started;

        /// <summary>ai[0] as it was last tick: a change is a move begun somewhere else.</summary>
        private float seen;

        /// <summary>The way a cut in the air is aimed, in the player's own frame, degrees: sent with the move.</summary>
        private float aim;

        /// <summary>The last cut of the chain begun (Rest after anything else): the next follows it.</summary>
        private RustMove lastCut = RustMove.Rest;

        /// <summary>The front hand this tick and last, in the player's own frame; and where the arms were drawn from last tick and the tick before, in the world.</summary>
        private Vector2 hand;
        private Vector2 handBefore;
        private Vector2 centreLast;
        private Vector2 centreBefore;

        /// <summary>How far this cut has carried its owner so far, and where they were last tick; and the farthest it may carry them, short of the enemy it is made at (their own machine only).</summary>
        private float stepped;
        private float lastX;
        private float stepCap = float.MaxValue;

        /// <summary>The dive: how high they were last tick (their own machine only).</summary>
        private float diveY;

        /// <summary>The air: cuts there since the ground was last under them, and whether the air is spent until it is (their own machine only).</summary>
        private int airCuts;
        private bool airLocked;

        /// <summary>The leap: from, to, and how high over the line between (their own machine only).</summary>
        private Vector2 leapFrom;
        private Vector2 leapTo;
        private float leapRise;

        /// <summary>The drive: where the blade went in; the tick each cut is let out at; how many are out; the ticks they open on (their own machine only).</summary>
        private Vector2 driveAt;
        private readonly int[] lineAt = new int[LineCount];
        private int linesOut;
        private readonly HashSet<int> opensAt = new HashSet<int>();

        /// <summary>Enter has been up since the chat was last open, so a hold of it now is the sword's (their own machine only).</summary>
        private bool armed;

        /// <summary>The player was turned over by this last tick.</summary>
        private bool rolled;

        /// <summary>Nothing under way: it can be told to do something.</summary>
        public bool Idle => !state.Busy;

        /// <summary>A left click is taken now: nothing under way.</summary>
        public bool CanCut => !state.Busy;

        private bool Drenched => BloodDrenchedPlayer.Drenched(Owner);

        /// <summary>The cut a left click gets now: the chain goes on within <see cref="Rhythm"/> ticks, else starts again.</summary>
        public RustMove NextCut => state.Idle > Rhythm ? RustMove.Up : RustSideMoves.After(lastCut, Drenched);

        /// <summary>The cut a right click gets now; Rest for none (the three are spent, or the air is, until they are on the ground).</summary>
        public RustMove NextAir
        {
            get
            {
                if (state.Busy)
                    return RustMove.Rest;

                if (airLocked && RustSideMoves.InAir(state.Last) && state.Idle <= AirGrace && airCuts < 3)
                    return RustSideMoves.AfterInAir(state.Last);

                return airLocked ? RustMove.Rest : RustMove.Air1;
            }
        }

        private bool Hitting => state.Cuts;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            line = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            smear = null;
            line = null;

            base.Unload();
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.noEnchantmentVisuals = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 2;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>The sword this player has in the hand, if they have one.</summary>
        public static RustSideHoldout Of(Player player)
        {
            int index = player.GetModPlayer<RustSidePose>().Sword;

            if (index < 0 || index >= Main.maxProjectiles)
                return null;

            Projectile projectile = Main.projectile[index];

            return projectile.active && projectile.owner == player.whoAmI ? projectile.ModProjectile as RustSideHoldout : null;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(aim);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            aim = reader.ReadSingle();
        }

        // ---- told what to do, on its owner's machine ---------------------------------------------

        /// <summary>
        /// The next cut of the chain, taking this many ticks, toward where <paramref name="toward"/>
        /// points (the cursor); or, with an enemy to make it at (<see cref="ChainTarget"/>), at
        /// that enemy: turned to face it, but under it or in it the cursor's way still; carried
        /// no further than <see cref="StepShort"/> short of touching it the way they face (so not
        /// at all under it or in it, and never out past it); and the thrust aimed at the nearest
        /// of it. The leap plans its own way (<see cref="PlanLeap"/>).
        /// </summary>
        internal void StartCut(int ticks, Vector2 toward)
        {
            Player player = Owner;
            RustMove move = NextCut;
            int facing = toward.X < 0f ? -1 : toward.X > 0f ? 1 : player.direction;
            float aimed = 0f;

            stepCap = float.MaxValue;

            if (move == RustMove.Leap)
            {
                PlanLeap(player, out leapTo, out int leapTicks, out facing, out leapRise);

                leapFrom = player.Center;
                ticks = leapTicks;
            }
            else
            {
                NPC target = ChainTarget(player);

                if (target != null)
                {
                    float across = target.Center.X - player.Center.X;

                    if (Math.Abs(across) > target.width * 0.5f)
                        facing = across < 0f ? -1 : 1;

                    float gap = facing > 0 ? target.position.X - (player.position.X + player.width) : player.position.X - (target.position.X + target.width);

                    stepCap = Math.Max(0f, gap - StepShort);

                    if (move == RustMove.Pierce)
                        aimed = AimAt(player, target, facing);
                }
            }

            Tell(move, ticks, facing, aimed);
        }

        /// <summary>
        /// The enemy the chain is made at, on its owner's machine: of those the game lets a
        /// weapon chase (not a critter, a town NPC or a target dummy) that are within
        /// <see cref="AimReach"/> of them (to the nearest of it) and that they can see, the one
        /// nearest the cursor (one under it before any other); of two as near it, the nearer to
        /// them. With none, the chain is cut the cursor's way, as it always was.
        /// </summary>
        public NPC ChainTarget(Player player)
        {
            Vector2 centre = player.Center;
            Vector2 cursor = Main.MouseWorld;
            NPC best = null;
            float bestCursor = float.MaxValue, bestNear = float.MaxValue;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float near = Vector2.Distance(Vector2.Clamp(centre, npc.TopLeft, npc.BottomRight), centre);

                if (near > AimReach || !Collision.CanHitLine(player.position, player.width, player.height, npc.position, npc.width, npc.height))
                    continue;

                float fromCursor = Vector2.Distance(Vector2.Clamp(cursor, npc.TopLeft, npc.BottomRight), cursor);

                if (fromCursor < bestCursor - 0.5f || (Math.Abs(fromCursor - bestCursor) <= 0.5f && near < bestNear))
                {
                    best = npc;
                    bestCursor = fromCursor;
                    bestNear = near;
                }
            }

            return best;
        }

        /// <summary>
        /// The way from the player's middle to the nearest of an enemy, in their own frame as
        /// they face (<paramref name="facing"/>), degrees: never behind them, so straight up or
        /// straight down at the most; level when their middle is in it.
        /// </summary>
        private static float AimAt(Player player, NPC target, int facing)
        {
            Vector2 centre = player.Center;
            Vector2 to = Vector2.Clamp(centre, target.TopLeft, target.BottomRight) - centre;

            if (to.LengthSquared() < 1f)
                return 0f;

            float angle = MathHelper.ToDegrees((float)Math.Atan2(to.Y * player.gravDir, to.X * facing));

            return MathHelper.Clamp(angle, -90f, 90f);
        }

        /// <summary>The next cut in the air, taking this many ticks, aimed where <paramref name="toward"/> points (the cursor).</summary>
        internal void StartAir(int ticks, Vector2 toward)
        {
            Player player = Owner;
            RustMove move = NextAir;

            if (move == RustMove.Rest)
                return;

            int facing = toward.X < 0f ? -1 : toward.X > 0f ? 1 : player.direction;

            // The aim in the player's own frame: forward is the side they face, down is down
            // whichever way the world is for them. Never behind them: they face it.
            float angle = MathHelper.ToDegrees((float)Math.Atan2(toward.Y * player.gravDir, Math.Abs(toward.X)));

            Tell(move, ticks, facing, MathHelper.Clamp(angle, -90f, 90f));
        }

        /// <summary>
        /// Where the leap goes, how long it takes, the side it faces, and how high over the
        /// straight way it goes: at the nearest enemy it can go for (one the game lets a weapon
        /// chase: not a critter, not a town NPC, not a target dummy) within
        /// <see cref="LeapReach"/> of them (to the nearest of it) that they can see. Beside it, on
        /// its near side, on the ground under it if it stands on any; but under it, it over their
        /// head, straight up at it to just under it, the way they face kept (the chain's aim:
        /// they stay under it), or, in it already, a hop where they are. With none, a way forward
        /// toward the cursor. On its owner's machine (the item asks first, for how long it will be
        /// in use: the same answer as when it starts, the same tick).
        /// </summary>
        public void PlanLeap(Player player, out Vector2 to, out int ticks, out int facing, out float rise)
        {
            Vector2 from = player.Center;
            float down = player.gravDir;
            NPC target = null;
            float best = LeapReach;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float far = Vector2.Distance(Vector2.Clamp(from, npc.TopLeft, npc.BottomRight), from);

                if (far >= best || !Collision.CanHitLine(player.position, player.width, player.height, npc.position, npc.width, npc.height))
                    continue;

                best = far;
                target = npc;
            }

            float cursorWay = Main.MouseWorld.X - from.X;
            int cursorFacing = cursorWay < 0f ? -1 : cursorWay > 0f ? 1 : player.direction;

            if (target != null && from.X > target.position.X + UnderInset && from.X < target.position.X + target.width - UnderInset
                && (target.Center.Y - from.Y) * down < 0f)
            {
                // Under it: up at it, to just under it; in it already, a hop where they are.
                float underside = down > 0f ? target.position.Y + target.height : target.position.Y;
                float y = underside + down * (player.height * 0.5f + UnderGap);
                bool hop = (y - from.Y) * down >= 0f;

                facing = cursorFacing;
                to = hop ? from : new Vector2(from.X, y);
                rise = hop ? LeapRiseLeast : 0f;
            }
            else
            {
                if (target != null)
                {
                    facing = target.Center.X >= from.X ? 1 : -1;
                    to = new Vector2(target.Center.X - facing * (target.width * 0.5f + 26f), target.Center.Y);

                    // One that stands on the ground is landed beside, on that ground.
                    if (!target.noGravity && target.velocity.Y == 0f)
                        to.Y = (down > 0f ? target.Bottom.Y : target.Top.Y) - player.height * 0.5f * down;
                }
                else
                {
                    facing = cursorFacing;
                    to = from + new Vector2(facing * LeapLacking, 0f);
                }

                rise = MathHelper.Clamp(LeapRiseLeast + Math.Abs(to.X - from.X) * 0.18f + Math.Max(0f, (from.Y - to.Y) * down) * 0.5f, LeapRiseLeast, LeapRiseMost);
            }

            ticks = RustSideMoves.LeapTicks(Vector2.Distance(from, to));
        }

        /// <summary>A move begun on its owner's machine: into ai, sent from the end of the sword's next update, and begun here now.</summary>
        private void Tell(RustMove move, int ticks, int facing, float aimed)
        {
            int count = (((int)Projectile.ai[0] >> 4) + 1) & 0xFF;

            Projectile.ai[0] = count * 16 + (int)move;
            Projectile.ai[1] = ticks;
            Projectile.ai[2] = facing;
            aim = aimed;

            // Not netUpdate: the item tells it this from the player's own update, and the game
            // forgets that one at the start of every update of a projectile, before it looks
            // (Projectile.Update). This is the one the game itself sets from outside one.
            Projectile.netUpdate2 = true;

            seen = Projectile.ai[0];
            Start();
        }

        /// <summary>A move begun, on every machine: the pose's, and what is done or seen as it begins.</summary>
        private void Start()
        {
            RustMove move = Told;
            Player player = Owner;

            state.Begin(move, ToldTicks, aim);

            if (move == RustMove.Rest)
                return;

            lastCut = move == RustMove.Up || move == RustMove.Down || move == RustMove.Spin || move == RustMove.Smash || move == RustMove.Pierce || move == RustMove.Leap
                ? move : RustMove.Rest;

            stepped = 0f;
            lastX = player.position.X;

            Projectile.ResetLocalNPCHitImmunity();

            if (Projectile.owner == Main.myPlayer)
                Begun(player, move);

            if (!Main.dedServ)
                Seen(player, move);
        }

        /// <summary>What a move does to its owner as it begins, on their machine.</summary>
        private void Begun(Player player, RustMove move)
        {
            float down = player.gravDir;

            switch (move)
            {
                case RustMove.Air1:
                case RustMove.Air2:
                case RustMove.Air3:
                    if (move == RustMove.Air1)
                        airCuts = 0;

                    airLocked = true;
                    airCuts++;

                    float up = move != RustMove.Air1 ? AirPop : player.velocity.Y == 0f ? AirJump : AirLift;

                    if (player.velocity.Y * down > -up)
                        player.velocity.Y = -up * down;

                    break;

                case RustMove.Dive:
                    player.velocity.X *= 0.5f;
                    break;

                case RustMove.Drive:
                    driveAt = new Vector2(At(player, hand).X, down > 0f ? player.Bottom.Y : player.Top.Y);
                    Uneven(lineAt, LineSpread);
                    linesOut = 0;
                    opensAt.Clear();
                    player.velocity.X = 0f;
                    break;
            }
        }

        /// <summary>Gone (put away, taken from the hand): the player is not left turned over.</summary>
        public override void OnKill(int timeLeft)
        {
            if (rolled)
                Owner.fullRotation = 0f;
        }

        // ---- every tick --------------------------------------------------------------------------

        public override void SafeAI()
        {
            Player player = Owner;
            bool mine = Projectile.owner == Main.myPlayer;

            Projectile.timeLeft = 2;

            if (!started)
            {
                started = true;
                seen = Projectile.ai[0];
                centreLast = VisusGrip.Centre(player);
                handBefore = hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Quarter, MathHelper.ToRadians(RustSideMoves.RestArm));
            }
            else if (Projectile.ai[0] != seen)
            {
                seen = Projectile.ai[0];
                Start();
            }

            if (mine)
            {
                Item item = player.HeldItem;

                Projectile.damage = player.GetWeaponDamage(item);
                Projectile.knockBack = player.GetWeaponKnockback(item);
                Projectile.CritChance = player.GetWeaponCrit(item);
                Projectile.ArmorPenetration = player.GetWeaponArmorPenetration(item);

                Focus(player);
                Aim(player);
            }

            // The aim as it is now (its owner's, or as last heard from them).
            if (state.Busy && RustSideMoves.InAir(state.Move))
                state.SetAim(aim);

            RustMove move = state.Move;
            float before = state.Progress;

            // (The player has moved already this tick: where they were last tick is what was kept then.)
            handBefore = hand;
            centreBefore = centreLast;
            centreLast = VisusGrip.Centre(player);

            state.Tick();

            float progress = state.Progress;

            if (state.Busy)
                player.ChangeDir(Facing);

            Pose(player, move, progress);

            if (state.Busy)
            {
                if (mine)
                    Moves(player, move, before, progress);

                if (!Main.dedServ)
                    Moments(player, move, before, progress);
            }
            else if (mine && player.velocity.Y == 0f)
            {
                // On the ground again: the air is theirs again.
                airLocked = false;
                airCuts = 0;
            }

            if (!Main.dedServ)
                Burn(player, move, progress);

            if (state.Finish())
                Ended(player, move, mine);
        }

        /// <summary>Where the sword is held this tick, the arms on it, the body turned over, and what the layer draws.</summary>
        private void Pose(Player player, RustMove move, float progress)
        {
            Player.CompositeArmStretchAmount reach = Stretch(state.Reach);
            float arm = MathHelper.ToRadians(state.Arm);
            float blade = MathHelper.ToRadians(state.Blade);

            hand = VisusGrip.FrontHand(reach, arm);

            // The arm as it is the right way up: vanilla turns it over itself under reversed
            // gravity (Player.SetCompositeArmFront). The other hand on the grip below the first
            // where the sword is swung with both.
            player.SetCompositeArmFront(true, reach, ArmRotation(player, arm));

            if (state.Busy && RustSideMoves.TwoHands(move, progress))
                player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotation(player, VisusGrip.BackArmOnto(Player.CompositeArmStretchAmount.Full, hand, blade)));

            // Turned over: the whole of the player's picture, about their middle.
            float roll = RollWorld(player, state.Roll);

            if (roll != 0f)
            {
                player.fullRotation = roll;
                player.fullRotationOrigin = player.Size * 0.5f;
                rolled = true;
            }
            else if (rolled)
            {
                player.fullRotation = 0f;
                rolled = false;
            }

            RustSidePose pose = player.GetModPlayer<RustSidePose>();
            pose.Held = true;
            pose.Blade = blade;
            pose.Hand = hand;
            pose.Sword = Projectile.whoAmI;
            pose.Blood = Drenched ? 1f : state.Busy && move == RustMove.Focus ? Focused(progress) : 0f;

            bool hitting = Hitting;

            // On the blade while it cuts (what vanilla cuts grass under), in the hand otherwise.
            Projectile.Center = hitting ? Middle(player) : At(player, hand);
            Projectile.velocity = Vector2.Zero;
            Projectile.direction = Projectile.spriteDirection = player.direction;
            Projectile.noEnchantmentVisuals = !hitting;
        }

        private static Player.CompositeArmStretchAmount Stretch(RustReach reach) => reach switch
        {
            RustReach.Full => Player.CompositeArmStretchAmount.Full,
            RustReach.ThreeQuarters => Player.CompositeArmStretchAmount.ThreeQuarters,
            RustReach.Quarter => Player.CompositeArmStretchAmount.Quarter,
            _ => Player.CompositeArmStretchAmount.None,
        };

        /// <summary>A composite arm's rotation pointing along an upright angle: the angle less a quarter turn, mirrored for a player facing left (IronFlow's, the Meat Hook's).</summary>
        private static float ArmRotation(Player player, float angle) => (player.direction == 1 ? angle : MathHelper.Pi - angle) - MathHelper.PiOver2;

        /// <summary>The roll in the world, radians: forward for the side they face, the other way under reversed gravity (the world is turned over for them).</summary>
        private static float RollWorld(Player player, float degrees) => degrees == 0f ? 0f : MathHelper.ToRadians(degrees) * player.direction * player.gravDir;

        /// <summary>A point in the player's own frame, in the world, turned with them as they roll.</summary>
        private Vector2 At(Player player, Vector2 upright) => At(player, VisusGrip.Centre(player), upright, RollWorld(player, state.Roll));

        private static Vector2 At(Player player, Vector2 centre, Vector2 upright, float roll)
        {
            Vector2 off = new Vector2(upright.X * player.direction, upright.Y * player.gravDir);

            return centre + (roll == 0f ? off : off.RotatedBy(roll));
        }

        /// <summary>A point on the blade this tick, this far from the hand.</summary>
        private Vector2 OnBlade(Player player, float along) => At(player, hand + MathHelper.ToRadians(state.Blade).ToRotationVector2() * along);

        private Vector2 Middle(Player player) => OnBlade(player, (ToGuard + ToTip) * 0.5f);

        /// <summary>How far the red has come up the blade, focusing, 0 to 1: none while it is raised, all of it by the end.</summary>
        private static float Focused(float progress) => MathHelper.Clamp((progress - RustSideMoves.FocusRaisedBy) / (1f - RustSideMoves.FocusRaisedBy), 0f, 1f);

        // ---- on its owner's machine: what moves them -------------------------------------------

        private void Moves(Player player, RustMove move, float before, float progress)
        {
            float down = player.gravDir;

            switch (move)
            {
                case RustMove.Up:
                case RustMove.Down:
                case RustMove.Smash:
                case RustMove.Pierce:
                    Step(player, move, progress);
                    break;

                case RustMove.Air1:
                case RustMove.Air2:
                case RustMove.Air3:
                    if (RustSideMoves.Hovers(move, progress) && player.velocity.Y * down > HoverFall)
                        player.velocity.Y = HoverFall * down;

                    player.fallStart = (int)(player.position.Y / 16f);

                    float release = RustSideMoves.Release(move);

                    if (before < release && progress >= release)
                        Release(player, move);

                    break;

                case RustMove.Leap:
                    Fly(player, before, progress);
                    player.fallStart = (int)(player.position.Y / 16f);
                    break;

                case RustMove.Dive:
                    player.fallStart = (int)(player.position.Y / 16f);

                    // Down on something: the point goes in. The game keeps nothing of a player's
                    // speed from the tick before (Player.oldVelocity is never set, 1.4.4), so it is
                    // last tick's push down that tells: stopped by the game (no fall left) or not
                    // moved down at all. Looked at before this tick's push, which would hide it.
                    if (state.Timer > 1f && (player.velocity.Y == 0f || Math.Abs(player.position.Y - diveY) < 0.5f))
                    {
                        Tell(RustMove.Drive, RustSideMoves.DriveTicks, Facing, 0f);
                        break;
                    }

                    diveY = player.position.Y;
                    player.velocity.X *= 0.85f;

                    if (player.velocity.Y * down < DiveSpeed)
                        player.velocity.Y = DiveSpeed * down;

                    break;

                case RustMove.Drive:
                    player.GetModPlayer<RustSidePose>().Still = 2;

                    while (linesOut < LineCount && state.Timer >= lineAt[linesOut] + 1)
                        Line(player, linesOut++);

                    break;

                case RustMove.Focus:
                    player.GetModPlayer<RustSidePose>().Still = 2;
                    break;
            }
        }

        /// <summary>
        /// The step a cut carries its owner (IronFlowHoldout's, Visus's thrust's): where they
        /// should be by the end of the next tick, along the ground, on a curve with nothing
        /// moving at either end, and the speed that gets them there. It measures where they
        /// really went, so friction and a wall come out in the next tick's sum. By request the
        /// steps are so far and no farther ("go forward 1 tile"), so with no key held the speed
        /// is set whatever it was, slowing them as well as carrying them, and they stop where the
        /// step ends; walking or running themselves, it is set only when it is more than they
        /// already have forward (the Iron Flow's way: a cut never holds anyone back).
        /// </summary>
        private void Step(Player player, RustMove move, float progress)
        {
            int facing = Facing;

            stepped += (player.position.X - lastX) * facing;
            lastX = player.position.X;

            float reach = Math.Min(RustSideMoves.Step(move, out float from, out float length), stepCap);

            if (reach < 1f || length <= 0f || progress < from || player.mount.Active || player.grappling[0] >= 0)
                return;

            bool walking = player.controlLeft || player.controlRight;

            // Ground friction takes this off before the player moves, when nothing is held.
            float friction = player.velocity.Y == 0f && !walking ? player.runSlowdown : 0f;

            if (progress > from + length)
            {
                // The step done: no more of it, on the ground, than to stop where it ends.
                if (!walking && player.velocity.Y == 0f && player.velocity.X * facing > 0f && stepped >= reach - 0.5f)
                    player.velocity.X = 0f;

                return;
            }

            float next = reach * Easing.Smoother((progress + 1f / state.Duration - from) / length);
            float speed = MathHelper.Clamp(next - stepped + friction, 0f, StepSpeedCap);

            if (!walking || speed > player.velocity.X * facing)
                player.velocity.X = speed * facing;
        }

        /// <summary>
        /// The leap's flight: from where it began to where it lands, an arc <see cref="leapRise"/>
        /// over the straight line between, over the part of the move between leaving the ground
        /// and arriving. The speed that puts them where they should be next tick, less the fall
        /// the game adds before it moves them; walls stop them as walls do. Arrived, they go no
        /// further along: the cut down is made where they landed (or, in the air, they fall from
        /// there).
        /// </summary>
        private void Fly(Player player, float before, float progress)
        {
            const float span = RustSideMoves.LeapArrives - RustSideMoves.LeapOff;

            if (before <= RustSideMoves.LeapArrives && progress > RustSideMoves.LeapArrives)
            {
                player.velocity.X = 0f;
                return;
            }

            if (progress < RustSideMoves.LeapOff || progress > RustSideMoves.LeapArrives)
                return;

            float next = Math.Min(1f, ((state.Timer + 1f) / state.Duration - RustSideMoves.LeapOff) / span);
            float rise = leapRise * 4f * next * (1f - next);
            Vector2 want = Vector2.Lerp(leapFrom, leapTo, next) - new Vector2(0f, rise * player.gravDir) - player.Center;

            want.Y -= player.gravity * player.gravDir;

            if (want.Length() > 24f)
                want *= 24f / want.Length();

            player.velocity = want;
        }

        /// <summary>A cut in the air lets its slash go: at the aim, from the blade, its share of the sword's damage; red if the sword is drenched.</summary>
        private void Release(Player player, RustMove move)
        {
            float angle = MathHelper.ToRadians(state.Aim);
            Vector2 way = new Vector2((float)Math.Cos(angle) * player.direction, (float)Math.Sin(angle) * player.gravDir);
            int kind = move == RustMove.Air3 ? RustSideSlash.Great : RustSideSlash.Plain;
            int damage = (int)Math.Round(Projectile.damage * RustSideMoves.SlashShare(move));

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), player.Center + way * 30f, way * RustSideSlash.SpeedOf(kind), ModContent.ProjectileType<RustSideSlash>(),
                damage, Projectile.knockBack, Projectile.owner, kind, Drenched ? 1f : 0f);
        }

        /// <summary>
        /// Ticks for as many things as <paramref name="at"/> holds: the first at nothing and the
        /// rest anywhere within <paramref name="spread"/> ticks of it, no two at one, in order
        /// (his own driven sword's way: Foigh.Uneven).
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
        /// One straight cut out of the ground, its owner's: across the fifteen tiles about the
        /// blade. Every other one through an enemy there (each in turn), anywhere on its body;
        /// any other through anywhere over the ground within ten and a half tiles of the blade,
        /// as likely in one place as in any other.
        /// </summary>
        private void Line(Player player, int i)
        {
            List<NPC> near = new List<NPC>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.CanBeChasedBy(Projectile) && Vector2.Distance(Vector2.Clamp(driveAt, npc.TopLeft, npc.BottomRight), driveAt) < LineRadius)
                    near.Add(npc);
            }

            Vector2 through;

            if (near.Count > 0 && i % 2 == 0)
            {
                NPC npc = near[(i / 2) % near.Count];

                through = npc.Center + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * npc.width, Main.rand.NextFloat(-0.4f, 0.4f) * npc.height);
            }
            else
            {
                float far = LineRadius * LineMiddle * (float)Math.Sqrt(Main.rand.NextFloat());
                float round = Main.rand.NextFloat(MathHelper.Pi);

                through = driveAt + new Vector2((float)Math.Cos(round) * far, -(float)Math.Sin(round) * far * player.gravDir);
            }

            // The way it lies, and the line across the circle that way through the point; its own
            // length of that, wherever along it so long as the point is on it.
            float turn = Main.rand.NextFloat(MathHelper.Pi);
            Vector2 along = turn.ToRotationVector2();
            Vector2 off = through - driveAt;
            float on = Vector2.Dot(off, along);
            float across = (float)Math.Sqrt(Math.Max(0f, LineRadius * LineRadius - off.LengthSquared() + on * on));

            float length;
            float start;

            if (across < CutKeep * 2f)
            {
                // A point at (or past) the circle's edge: a cut of its own length through it.
                length = CutKeep * 4f;
                start = on - length * 0.5f;
            }
            else
            {
                length = 2f * across * Main.rand.NextFloat(CutShortest, 1f);
                float keep = Math.Min(CutKeep, length * 0.5f);
                start = MathHelper.Clamp(Main.rand.NextFloat(on + keep - length, on - keep), -across, across - length);
            }

            int now = (int)state.Timer;
            int shows = ShowsFor(now);

            RustSideLine.Let(Projectile, through + along * (start - on + length * 0.5f), turn, length * 0.5f, shows,
                Main.rand.Next(RustSideLine.HangsLeast, RustSideLine.HangsMost + 1), Projectile.damage, Projectile.knockBack);
        }

        /// <summary>The ticks a cut let out now shows for, from the fewest to the most a cut may, on no tick another opens on where that can be.</summary>
        private int ShowsFor(int now)
        {
            List<int> free = new List<int>();

            for (int s = RustSideLine.ShowsLeast; s <= RustSideLine.ShowsMost; s++)
            {
                if (!opensAt.Contains(now + s))
                    free.Add(s);
            }

            int shows = free.Count > 0 ? free[Main.rand.Next(free.Count)] : Main.rand.Next(RustSideLine.ShowsLeast, RustSideLine.ShowsMost + 1);

            opensAt.Add(now + shows);
            return shows;
        }

        /// <summary>
        /// A cut in the air follows the cursor, on its owner's machine, while they roll and wind
        /// (they have leapt since it was begun, and what was ahead of them may now be below):
        /// from where they are, in the side they face, until the cut itself begins, and then it
        /// is held. The other machines are told when it has moved more than a little.
        /// </summary>
        private void Aim(Player player)
        {
            if (!state.Busy || !RustSideMoves.InAir(state.Move) || state.Progress >= RustSideMoves.CutFrom(state.Move))
                return;

            Vector2 toward = Main.MouseWorld - player.Center;
            float angle = MathHelper.Clamp(MathHelper.ToDegrees((float)Math.Atan2(toward.Y * player.gravDir, Math.Abs(toward.X))), -90f, 90f);

            if (Math.Abs(angle - aim) > 2f)
            {
                aim = angle;
                Projectile.netUpdate2 = true;
            }
        }

        /// <summary>
        /// Enter held, on its owner's machine: the focus begun, standing on the ground with the
        /// sword at rest and nothing on the sword; let go of, or anything that keeps it from
        /// going on, and it is over with nothing.
        /// </summary>
        private void Focus(Player player)
        {
            bool enter = EnterHeld();
            bool can = BloodDrenchedPlayer.CanDrench(player) && player.velocity.Y == 0f && !player.mount.Active && player.grappling[0] < 0 && !player.dead;

            if (state.Busy && state.Move == RustMove.Focus)
            {
                if (!enter || !can)
                    Tell(RustMove.Rest, 1, Facing, 0f);

                return;
            }

            if (!state.Busy && enter && can)
                Tell(RustMove.Focus, RustSideMoves.FocusTicks, player.direction, 0f);
        }

        /// <summary>
        /// Enter down, the game's window in front, nothing being typed into, and no Alt with it
        /// (Alt and Enter is the window's); and let go of since the chat was last open, so the
        /// press that sends a line in the chat does not go on to focus.
        /// </summary>
        private bool EnterHeld()
        {
            bool alt = Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt);
            bool down = Main.keyState.IsKeyDown(Keys.Enter) && !alt && Main.hasFocus;
            bool typing = Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.gameMenu || Main.blockInput || PlayerInput.WritingText
                || Main.ingameOptionsWindow || (Main.InGameUI != null && Main.InGameUI.IsVisible);

            if (typing)
                armed = false;
            else if (!down)
                armed = true;

            return down && armed && !typing;
        }

        /// <summary>A move run to its end: what follows.</summary>
        private void Ended(Player player, RustMove move, bool mine)
        {
            switch (move)
            {
                case RustMove.Leap:
                    // The button still held: down onto the point, or, on the ground already, in.
                    if (mine && player.controlUseItem && player.altFunctionUse != 2 && !player.dead)
                    {
                        if (player.velocity.Y == 0f)
                            Tell(RustMove.Drive, RustSideMoves.DriveTicks, Facing, 0f);
                        else
                            Tell(RustMove.Dive, RustSideMoves.DiveTicks, Facing, 0f);
                    }

                    break;

                case RustMove.Focus:
                    if (mine)
                        BloodDrenchedPlayer.Drench(player);

                    if (!Main.dedServ)
                        DrenchedLooks(player);

                    break;
            }
        }

        // ---- what it hits ------------------------------------------------------------------------

        public override bool? CanDamage() => Hitting ? null : false;

        /// <summary>The blade from the guard's side to past the point, with the hand here and the body rolled this far, in the world.</summary>
        private static void Edge(Player player, Vector2 centre, Vector2 at, float blade, float roll, out Vector2 from, out Vector2 to)
        {
            Vector2 u = blade.ToRotationVector2();

            from = At(player, centre, at + u * Inner, roll);
            to = At(player, centre, at + u * Outer, roll);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Hitting)
                return false;

            Player player = Owner;
            Vector2 centre = VisusGrip.Centre(player);
            float collision = 0f;

            // Last tick's blade to this one's: turned, rolled and carried, a step every 12 degrees
            // or 16 pixels or less, so nothing between is stepped over.
            float turned = Math.Max(Math.Abs(state.Blade - state.PreviousBlade), Math.Abs(state.Roll - state.PreviousRoll));
            float carried = Vector2.Distance(centre, centreBefore) + Vector2.Distance(hand, handBefore);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(turned / 12f, carried / 16f)));

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;

                Edge(player, Vector2.Lerp(centreBefore, centre, k), Vector2.Lerp(handBefore, hand, k),
                    MathHelper.ToRadians(MathHelper.Lerp(state.PreviousBlade, state.Blade, k)),
                    RollWorld(player, MathHelper.Lerp(state.PreviousRoll, state.Roll, k)), out Vector2 from, out Vector2 to);

                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, Width, ref collision))
                    return true;
            }

            return false;
        }

        public override void CutTiles()
        {
            if (!Hitting)
                return;

            Player player = Owner;

            Edge(player, VisusGrip.Centre(player), hand, MathHelper.ToRadians(state.Blade), RollWorld(player, state.Roll), out Vector2 from, out Vector2 to);

            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(from, to, Width, DelegateMethods.CutTiles);
        }

        /// <summary>Through all defense (by request, "attacks ignore defense"); knocked away from its owner, the smash and the drive harder.</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.ScalingArmorPenetration += 1f;
            modifiers.HitDirectionOverride = target.Center.X < Owner.Center.X ? -1 : 1;

            if (state.Move == RustMove.Smash || state.Move == RustMove.Drive || state.Move == RustMove.Leap)
                modifiers.Knockback *= 1.5f;
            else if (state.Move == RustMove.Pierce)
                modifiers.Knockback *= 1.25f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Drenched, every blow of it bleeds (by request: "any attack in blood drenched sword
            // causes a version of ultra bleeding"): where the blow is worked out, which tells the world.
            if (Drenched)
                UltraBleedingGeneric.Cut(target);

            if (Main.dedServ)
                return;

            Player player = Owner;
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);
            bool blood = Drenched;

            // The way the blade is going: across itself, as it turns; along itself, thrusting.
            float blade = MathHelper.ToRadians(state.Blade);
            float turning = Math.Sign(state.Blade - state.PreviousBlade);
            Vector2 u = blade.ToRotationVector2();
            Vector2 upright = RustSideMoves.Thrusts(state.Move) ? u : new Vector2(-u.Y, u.X) * (turning == 0f ? 1f : turning);
            Vector2 travel = At(player, VisusGrip.Centre(player), upright, RollWorld(player, state.Roll)) - VisusGrip.Centre(player);

            travel = travel.SafeNormalize(Vector2.UnitX * player.direction);

            Color[] sparks = blood ? BloodSparks : SteelSparks;

            for (int i = 0; i < 5; i++)
            {
                float length = Main.rand.NextFloat(14f, 26f);

                PADParticleHandler.SpawnParticle(new SparkShard(at, travel.RotatedByRandom(0.7f) * Main.rand.NextFloat(2.5f, 6f),
                    sparks[Main.rand.Next(sparks.Length)], length * 0.5f, 14f, length, Main.rand.Next(12, 18)));
            }

            if (blood)
            {
                BloodSlash.Pair(target.Center, new Vector2(-travel.Y, travel.X), 34f + Math.Min(target.width, target.height) * 0.3f, 5f, 16);
                BloodDroplet.SpawnBurst(at, travel, 8, 40f);
            }

            bool heavy = state.Move == RustMove.Smash || state.Move == RustMove.Leap || state.Move == RustMove.Drive;

            if (player.whoAmI == Main.myPlayer)
                player.PIS().BumpScreenShake(heavy ? 5f : 2.5f, heavy ? 10 : 6);
        }

        // ---- what is seen and heard, on every machine with a screen ------------------------------

        /// <summary>A move begun.</summary>
        private void Seen(Player player, RustMove move)
        {
            switch (move)
            {
                case RustMove.Air1:
                case RustMove.Air2:
                case RustMove.Air3:
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = move == RustMove.Air3 ? -0.3f : 0.15f, Volume = 0.8f }, player.Center);

                    // Kicked off the air or the ground.
                    for (int i = 0; i < 6; i++)
                    {
                        PADParticleHandler.SpawnParticle(new WindStreak(player.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(2f, 4f) * player.gravDir), Main.rand.NextFloat(14f, 26f), 3f, Main.rand.Next(8, 13)));
                    }

                    break;

                case RustMove.Drive:
                    Driven(player);
                    break;

                case RustMove.Focus:
                    SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Pitch = -0.5f, Volume = 0.8f }, player.Center);
                    break;
            }
        }

        /// <summary>The moments of a move under way: the blade fast (its sound), the smash into the ground, the thrust out, the leap off the ground and down onto it, the focus's pulse.</summary>
        private void Moments(Player player, RustMove move, float before, float progress)
        {
            RustSideMoves.Fast(move, out float fast, out _);

            if (before < fast && progress >= fast)
                Swung(player, move);

            switch (move)
            {
                case RustMove.Smash:
                    if (before < RustSideMoves.SmashLands && progress >= RustSideMoves.SmashLands)
                        Landed(player, OnBlade(player, ToTip), 1f);

                    break;

                case RustMove.Pierce:
                    if (progress >= RustSideMoves.PierceFrom && progress <= 0.58f)
                    {
                        Vector2 tip = OnBlade(player, ToTip);
                        Vector2 way = (tip - At(player, hand)).SafeNormalize(Vector2.UnitX * player.direction);

                        PADParticleHandler.SpawnParticle(new WindStreak(tip - way * Main.rand.NextFloat(0f, 40f) + new Vector2(-way.Y, way.X) * Main.rand.NextFloat(-10f, 10f),
                            way * Main.rand.NextFloat(4f, 8f), Main.rand.NextFloat(22f, 40f), 3f, Main.rand.Next(8, 12)));
                    }

                    if (before < 0.5f && progress >= 0.5f)
                        PADParticleHandler.SpawnParticle(new CrownGlint(OnBlade(player, ToTip), 46f, 16, Drenched ? Foigh.Cinder : Color.White));

                    break;

                case RustMove.Leap:
                    if (before < RustSideMoves.LeapOff && progress >= RustSideMoves.LeapOff)
                    {
                        SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = 0.3f, Volume = 0.7f }, player.Center);

                        for (int i = 0; i < 8; i++)
                        {
                            PADParticleHandler.SpawnParticle(new WindStreak(player.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f),
                                new Vector2(-player.direction * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(1f, 3f) * player.gravDir), Main.rand.NextFloat(16f, 30f), 3f, Main.rand.Next(8, 14)));
                        }
                    }

                    if (before < RustSideMoves.LeapLands && progress >= RustSideMoves.LeapLands)
                        Landed(player, OnBlade(player, ToTip), 0.6f);

                    break;

                case RustMove.Dive:
                    if (Main.GameUpdateCount % 2 == 0)
                    {
                        PADParticleHandler.SpawnParticle(new WindStreak(player.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), -20f * player.gravDir),
                            new Vector2(0f, -Main.rand.NextFloat(3f, 6f) * player.gravDir), Main.rand.NextFloat(24f, 40f), 3f, Main.rand.Next(8, 12)));
                    }

                    break;

                case RustMove.Focus:
                    // A pulse every three quarters of a second, deeper as the red comes up.
                    int now = (int)state.Timer;

                    if (now > 0 && now % 45 == 0 && progress < 1f)
                        SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.6f - 0.2f * Focused(progress), Volume = 0.45f }, player.Center);

                    break;
            }
        }

        /// <summary>The blade going fast: heard.</summary>
        private void Swung(Player player, RustMove move)
        {
            switch (move)
            {
                case RustMove.Up:
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.1f }, player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.2f, Volume = 0.7f }, player.Center);
                    break;

                case RustMove.Down:
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.15f }, player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.4f, Volume = 0.7f }, player.Center);
                    break;

                case RustMove.Spin:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.9f }, player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.3f, Volume = 0.8f }, player.Center);
                    break;

                case RustMove.Smash:
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.6f, Volume = 1f }, player.Center);
                    break;

                case RustMove.Pierce:
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Pitch = -0.1f, Volume = 0.8f }, player.Center);
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.4f }, player.Center);
                    break;

                case RustMove.Leap:
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, player.Center);
                    break;

                case RustMove.Drive:
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f, Volume = 0.8f }, player.Center);
                    break;
            }
        }

        /// <summary>The blade into the ground at <paramref name="tip"/>: the ground heaved, dust, sparks off it, heard; and, drenched, scorched. Shaken, for whoever is near.</summary>
        private void Landed(Player player, Vector2 tip, float hard)
        {
            GroundWave.Slam(tip, 8f, (int)(4 + 4 * hard), 4f + 3f * hard);

            SparkShard.Spray(tip, Foigh.SteelColour, (int)(6 + 6 * hard), 5f, 20f, 14, falls: true, palette: Drenched ? BloodSparks : SteelSparks);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.3f, Volume = 0.9f * hard + 0.1f }, tip);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 0.5f * hard }, tip);

            if (Drenched)
            {
                for (int i = 0; i < 2; i++)
                {
                    float slant = MathHelper.ToRadians(Main.rand.NextFloat(12f, 40f)) * (i == 0 ? 1f : -1f);

                    BloodSlash.Pair(tip + new Vector2(Main.rand.NextFloat(-10f, 10f), -Main.rand.NextFloat(10f, 30f)),
                        new Vector2((float)Math.Cos(slant), (float)Math.Sin(slant)), Main.rand.NextFloat(50f, 90f) * hard + 20f, 5f, Main.rand.Next(18, 26));
                }

                for (int i = 0; i < 6; i++)
                {
                    PADParticleHandler.SpawnParticle(new BurnSmoke(tip + Main.rand.NextVector2Circular(10f, 6f), new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.5f, 1.8f)),
                        Foigh.Soot, Foigh.BurnRed, Main.rand.Next(26, 44), Main.rand.NextFloat(10f, 16f), Main.rand.NextFloat(28f, 42f), Main.rand.NextFloat(0.7f, 0.95f)));
                }
            }

            Shake(tip, 4f + 4f * hard, 10 + (int)(8 * hard));
        }

        /// <summary>The drive: driven in, all its weight; the ground heaved further; the ring of where the straight cuts will come shown in red.</summary>
        private void Driven(Player player)
        {
            Vector2 at = driveAt != Vector2.Zero && Projectile.owner == Main.myPlayer ? driveAt : new Vector2(OnBlade(player, 0f).X, player.gravDir > 0f ? player.Bottom.Y : player.Top.Y);

            GroundWave.Slam(at, 10f, 10, 8f);

            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Pitch = -0.4f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.6f, Volume = 1f }, at);

            PADParticleHandler.SpawnParticle(new ParryRing(at, 20f, LineRadius, 24, Foigh.BurnRed));
            PADParticleHandler.SpawnParticle(new ParryRing(at, 12f, LineRadius * 0.7f, 18, Foigh.TrailRed));

            for (int i = 0; i < 3; i++)
            {
                float slant = MathHelper.ToRadians(Main.rand.NextFloat(-60f, 60f));

                BloodSlash.Pair(at + new Vector2(Main.rand.NextFloat(-16f, 16f), -Main.rand.NextFloat(6f, 30f) * player.gravDir), new Vector2((float)Math.Cos(slant), (float)Math.Sin(slant)),
                    Main.rand.NextFloat(40f, 80f), 6f, Main.rand.Next(18, 28));
            }

            for (int i = 0; i < 10; i++)
            {
                PADParticleHandler.SpawnParticle(new Ember(at + Main.rand.NextVector2Circular(16f, 6f), new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f) * player.gravDir),
                    Foigh.Cinder, Main.rand.NextFloat(0.45f, 0.85f), Main.rand.Next(18, 34)));
            }

            Shake(at, 9f, 20);
        }

        /// <summary>The focus over: the blood on. A ring of red off them, cuts crossing about them, black and red off the blade all at once.</summary>
        private void DrenchedLooks(Player player)
        {
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 0.6f }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Pitch = -0.5f, Volume = 0.8f }, player.Center);

            PADParticleHandler.SpawnParticle(new ParryRing(player.Center, 24f, 150f, 26, Foigh.BurnRed, player));
            PADParticleHandler.SpawnParticle(new ParryRing(player.Center, 16f, 100f, 20, Foigh.TrailRed, player));

            for (int i = 0; i < 4; i++)
            {
                float slant = MathHelper.PiOver4 * i + Main.rand.NextFloat(-0.3f, 0.3f);

                BloodSlash.Pair(player.Center + Main.rand.NextVector2Circular(8f, 10f), slant.ToRotationVector2(), Main.rand.NextFloat(34f, 52f), 5f, Main.rand.Next(16, 24));
            }

            BloodDroplet.SpawnBurst(player.Center, -Vector2.UnitY * player.gravDir, 14, 80f);

            Vector2 guard = OnBlade(player, ToGuard);
            Vector2 tip = OnBlade(player, ToTip);

            for (int i = 0; i < 14; i++)
            {
                Vector2 at = Vector2.Lerp(guard, tip, Main.rand.NextFloat());

                PADParticleHandler.SpawnParticle(new BurnSmoke(at, Main.rand.NextVector2Circular(2.4f, 2.4f), Foigh.Soot, Foigh.BurnRed, Main.rand.Next(28, 46),
                    Main.rand.NextFloat(9f, 15f), Main.rand.NextFloat(26f, 40f), Main.rand.NextFloat(0.75f, 1f)));

                if (i % 2 == 0)
                    PADParticleHandler.SpawnParticle(new Ember(at, Main.rand.NextVector2Circular(2.5f, 2.5f), Foigh.Cinder, Main.rand.NextFloat(0.45f, 0.8f), Main.rand.Next(18, 32)));
            }

            Shake(player.Center, 5f, 14);
        }

        /// <summary>This machine's own player shaken, if they are near enough to feel it.</summary>
        private static void Shake(Vector2 at, float power, int ticks)
        {
            Player me = Main.LocalPlayer;

            if (me == null || !me.active || Vector2.Distance(me.Center, at) > 900f)
                return;

            me.PIS().BumpScreenShake(power, ticks);
        }

        /// <summary>
        /// Every tick, on a machine with a screen: drenched, black off the whole length of the
        /// blade, in tatters, lifted and left behind as it moves; red in it; sparks; and its
        /// light (Foigh's own burning sword's: Smoulder). Focusing, the same coming on as the red
        /// comes up the blade.
        /// </summary>
        private void Burn(Player player, RustMove move, float progress)
        {
            float flame = Drenched ? 1f : state.Busy && move == RustMove.Focus ? Focused(progress) : 0f;

            if (flame <= 0.02f || Vector2.Distance(player.Center, Main.LocalPlayer.Center) > 1700f)
                return;

            Vector2 guard = OnBlade(player, ToGuard);
            Vector2 tip = OnBlade(player, ToTip);
            Vector2 along = tip - guard;
            bool cutting = state.Busy && Hitting;
            Vector2 back = new Vector2(-player.direction * 0.5f, -1f * player.gravDir);

            for (int i = 0; i < (cutting ? 3 : 1); i++)
            {
                if (Main.rand.NextFloat() > flame)
                    continue;

                Vector2 at = guard + along * Main.rand.NextFloat(0.06f, 1f) + Main.rand.NextVector2Circular(3f, 3f);

                PADParticleHandler.SpawnParticle(new BurnSmoke(at, back * Main.rand.NextFloat(0.4f, 1.3f) + player.velocity * 0.2f, Foigh.Soot, Foigh.BurnRed,
                    Main.rand.Next(cutting ? 34 : 24, cutting ? 56 : 40), Main.rand.NextFloat(8f, 14f), Main.rand.NextFloat(22f, 36f), Main.rand.NextFloat(0.75f, 1f)));
            }

            if (Main.GameUpdateCount % 2 == 0 || cutting)
            {
                Vector2 at = guard + along * Main.rand.NextFloat(0.1f, 1f);

                PADParticleHandler.SpawnParticle(new BloodFume(at, back * Main.rand.NextFloat(0.2f, 0.8f), Main.rand.NextFloat(0.05f, 0.1f) * (cutting ? 1.5f : 1f) * flame, Main.rand.Next(14, 26)));
            }

            if (Main.GameUpdateCount % 4 == 0)
            {
                Vector2 at = guard + along * Main.rand.NextFloat(0.1f, 1f);

                PADParticleHandler.SpawnParticle(new Ember(at, new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.4f, 1.8f) * player.gravDir), Foigh.Cinder,
                    Main.rand.NextFloat(0.4f, 0.75f), Main.rand.Next(18, 32)));
            }

            Lighting.AddLight(guard + along * 0.5f, 0.55f * flame, 0.07f * flame, 0.06f * flame);
        }

        // ---- what it leaves behind -----------------------------------------------------------------

        /// <summary>The hand at this point of a move, in the player's own frame.</summary>
        private Vector2 HandAt(RustMove move, float progress) =>
            VisusGrip.FrontHand(Stretch(RustSideMoves.Reach(move, progress)), MathHelper.ToRadians(RustSideMoves.Arm(move, progress, state.Aim)));

        public override bool PreDraw(ref Color lightColor)
        {
            if (smear == null || line == null || !smear.IsLoaded || !line.IsLoaded)
                return false;

            Player player = Owner;
            RustMove move = state.Move;
            bool drenched = Drenched;
            float flame = drenched ? 1f : state.Busy && move == RustMove.Focus ? Focused(state.Progress) : 0f;

            float head = 0f, tail = 0f, fade = 0f, from = 0f;
            bool trailed = false;

            if (state.Busy)
            {
                RustSideMoves.Fast(move, out from, out float to);

                float now = state.Progress;

                if (to > from && now > from)
                {
                    // All of it while the blade is fast, gone a few ticks after it slows.
                    fade = MathHelper.Clamp(1f - (state.Timer - to * state.Duration) / FadeTicks, 0f, 1f);
                    head = Math.Min(now, to);
                    tail = Math.Max(from, head - TrailTicks / state.Duration);
                    trailed = fade > 0f;
                }
            }

            if (trailed)
                DrawShadows(player, move, head, from, fade, drenched);

            if (!trailed && flame <= 0.01f)
                return false;

            Main.spriteBatch.End();

            if (flame > 0.01f)
                DrawBurning(player, flame);

            if (trailed && head - tail > 0.004f)
            {
                if (RustSideMoves.Thrusts(move))
                    DrawThrust(player, move, head, from, fade, drenched);
                else
                    DrawTrail(player, move, head, tail, fade, drenched);
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);

            return false;
        }

        /// <summary>Three shadows of the sword along the way it came, each a tick behind the last, in its own light: steel, or red.</summary>
        private void DrawShadows(Player player, RustMove move, float head, float from, float fade, bool drenched)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            bool flip = (player.direction < 0) != (player.gravDir < 0f);
            Vector2 centre = VisusGrip.Centre(player);
            Color tint = drenched ? new Color(255, 90, 70, 0) : new Color(220, 230, 255, 0);

            for (int i = 3; i >= 1; i--)
            {
                float p = head - i / state.Duration;

                if (p < from)
                    continue;

                float roll = RollWorld(player, RustSideMoves.Roll(move, p));
                float a = MathHelper.ToRadians(RustSideMoves.Blade(move, p, state.Aim));
                Vector2 at = At(player, centre, HandAt(move, p), roll) - Main.screenPosition;

                Color light = tint * (0.3f * (1f - i / 4f) * fade);

                RustSideLayer.Place(VisusGrip.ToWorldAngle(player, a) + roll, flip, out float turn, out Vector2 origin, out SpriteEffects effects);
                Main.EntitySpriteDraw(texture, at, null, light, turn, origin, 1f, effects, 0);
            }
        }

        /// <summary>Steps along the trail.</summary>
        private const int Steps = 24;

        // Laid out again every frame it is drawn, only ever on the main thread.
        private static readonly Vector2[] TrailIn = new Vector2[Steps + 1];
        private static readonly Vector2[] TrailOut = new Vector2[Steps + 1];
        private static readonly Vector2[] TrailEdge = new Vector2[Steps + 1];
        private static readonly float[] TrailStrength = new float[Steps + 1];

        /// <summary>
        /// The trail: a band from past the guard to the point over the blade's way from
        /// <paramref name="head"/> back to <paramref name="tail"/>, strongest at the head. First
        /// dark, so that it shows on a bright sky; then steel over that, as light; then a white
        /// line along the point's own path. Drenched, his bloodied sword's: the dark all but black
        /// and thicker, the light over it red, and the line along the point's path red and bright.
        /// </summary>
        private void DrawTrail(Player player, RustMove move, float head, float tail, float fade, bool drenched)
        {
            Vector2 centre = VisusGrip.Centre(player);

            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                float p = MathHelper.Lerp(head, tail, k);
                float roll = RollWorld(player, RustSideMoves.Roll(move, p));

                Vector2 u = MathHelper.ToRadians(RustSideMoves.Blade(move, p, state.Aim)).ToRotationVector2();
                Vector2 grip = HandAt(move, p);

                TrailIn[i] = At(player, centre, grip + u * TrailInner, roll);
                TrailOut[i] = At(player, centre, grip + u * TrailOuter, roll);
                TrailEdge[i] = At(player, centre, grip + u * (TrailOuter - 9f), roll);
                TrailStrength[i] = (float)Math.Pow(1f - k, 1.3f) * fade;
            }

            float red = drenched ? 1f : 0f;
            Color shade = Color.Lerp(Foigh.DuskColour, Foigh.Soot, red);
            Color metal = Color.Lerp(Foigh.SteelColour, Foigh.BurnRed, red);
            Color edge = Color.Lerp(Color.White, Foigh.TrailRed, red);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dark = new Color(shade.R, shade.G, shade.B, 255) * ((0.4f + 0.5f * red) * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], dark, dark, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color bright = new Color(metal.R, metal.G, metal.B, 0) * ((0.44f + 0.02f * red) * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], bright, bright, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color pale = new Color(edge.R, edge.G, edge.B, 0) * ((0.85f + 0.15f * red) * TrailStrength[i]);
                Strip.Pair(TrailEdge[i], TrailOut[i], pale, pale, i / (float)Steps);
            }
            Strip.Draw(line.Value);
        }

        /// <summary>The thrust: a lance of light from where the point was drawn back to to where it is, brightest at the point, and a dark hair under it.</summary>
        private void DrawThrust(Player player, RustMove move, float head, float from, float fade, bool drenched)
        {
            Vector2 centre = VisusGrip.Centre(player);
            Vector2 back = At(player, centre, HandAt(move, from) + MathHelper.ToRadians(RustSideMoves.Blade(move, from, state.Aim)).ToRotationVector2() * TrailOuter, 0f);
            Vector2 point = At(player, centre, HandAt(move, head) + MathHelper.ToRadians(RustSideMoves.Blade(move, head, state.Aim)).ToRotationVector2() * (TrailOuter + 10f), 0f);
            Vector2 along = point - back;
            float length = along.Length();

            if (length < 4f)
                return;

            along /= length;

            Vector2 across = new Vector2(-along.Y, along.X);
            Color glow = drenched ? Foigh.BurnRed : Foigh.SteelColour;
            Color core = drenched ? Foigh.TrailRed : Color.White;

            void Band(float half, Color colour)
            {
                const int steps = 16;

                Strip.Begin();

                for (int i = 0; i <= steps; i++)
                {
                    float k = i / (float)steps;
                    Color here = colour * (fade * (float)Math.Pow(k, 1.5f));
                    Vector2 at = back + along * (length * k);

                    Strip.Pair(at - across * half, at + across * half, here, here, 0.5f);
                }

                Strip.Draw(line.Value);
            }

            Band(4f, new Color(Foigh.DuskColour.R, Foigh.DuskColour.G, Foigh.DuskColour.B, 255) * 0.35f);
            Band(12f, new Color(glow.R, glow.G, glow.B, 0) * 0.45f);
            Band(3f, new Color(core.R, core.G, core.B, 0) * 0.95f);
        }

        /// <summary>
        /// The black and the red on the blade itself, drenched (Foigh's own burning sword's:
        /// DrawBurning): a wide faint red about it; soot standing off its upper side in tongues
        /// that never hold still; the blade red hot under it. Focusing, it comes on as the red
        /// comes up the blade, from the guard.
        /// </summary>
        private void DrawBurning(Player player, float flame)
        {
            Vector2 guard = OnBlade(player, ToGuard);
            Vector2 tip = OnBlade(player, ToTip);
            Vector2 along = tip - guard;
            float length = along.Length();

            if (length < 8f)
                return;

            along /= length;

            // The side of the blade the soot stands off: its upper one, or behind them if it is upright.
            Vector2 off = new Vector2(along.Y, -along.X);

            if (off.Y > 0.05f || (Math.Abs(off.Y) <= 0.05f && off.X * player.direction > 0f))
                off = -off;

            const int steps = 22;
            float time = Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI;

            // How far up the blade it has come: all of it, drenched; as much as the red has, focusing.
            float up = Drenched ? 1f : flame;

            float Reach(float k) => MathHelper.Clamp((up - k) / 0.08f + 1f, 0f, 1f);

            Strip.Begin();
            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Vector2 at = guard + along * (length * k);
                float thin = MathHelper.Clamp(Math.Min(k / 0.08f, (1f - k) / 0.05f), 0f, 1f);
                Color glow = new Color(Foigh.BurnRed.R, Foigh.BurnRed.G, Foigh.BurnRed.B, 0) * (0.28f * flame * (0.5f + 0.5f * thin) * Reach(k));

                Strip.Pair(at - off * 13f, at + off * 22f, glow, glow, 0.5f);
            }
            Strip.Draw(line.Value);

            Strip.Begin();
            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Vector2 at = guard + along * (length * k);
                float tongue = 0.5f + 0.5f * (float)(Math.Sin(i * 1.9f + time) * Math.Sin(i * 0.63f - time * 0.61f));
                float thin = MathHelper.Clamp(Math.Min(k / 0.08f, (1f - k) / 0.05f), 0f, 1f);
                Color soot = new Color(Foigh.Soot.R, Foigh.Soot.G, Foigh.Soot.B, 255) * (0.62f * flame * thin * (0.55f + 0.45f * tongue) * Reach(k));

                Strip.Pair(at - off * 3f, at + off * (9f + 9f * tongue), soot, soot, 0.5f);
            }
            Strip.Draw(line.Value);

            float flicker = 0.8f + 0.2f * (float)Math.Sin(time * 2.3f);

            Strip.Begin();
            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Vector2 at = guard + along * (length * k);
                float patch = 0.6f + 0.4f * (float)Math.Sin(i * 1.3f - time * 0.9f);
                Color red = new Color(Foigh.BurnRed.R, Foigh.BurnRed.G, Foigh.BurnRed.B, 0) * (0.55f * flame * flicker * patch * Reach(k));

                Strip.Pair(at - off * 7f, at + off * 7f, red, red, 0.5f);
            }
            Strip.Draw(line.Value);
        }
    }

    /// <summary>
    /// The Rust Side Sword as the player holds it this tick, for RustSideLayer to draw: set by
    /// the sword's own AI on every machine, cleared at the start of every tick, so it is drawn
    /// exactly while the sword is there; and which projectile the sword is, for the item to find
    /// it by. And, on its owner's machine, the player held still while they focus and while the
    /// sword is in the ground (<see cref="Still"/>: ticks of it left, set by the sword every
    /// tick it wants it; the game asks for the keys and for the movement before the sword's own
    /// update comes round again, so it outlasts the tick).
    /// </summary>
    public class RustSidePose : ModPlayer
    {
        public bool Held;

        /// <summary>The blade's angle, in the player's own frame, radians.</summary>
        public float Blade;

        /// <summary>The front hand, in the player's own frame.</summary>
        public Vector2 Hand;

        /// <summary>The sword's place among the projectiles; less than none before there has been one.</summary>
        public int Sword = -1;

        /// <summary>How much of the sword's bloodied side is drawn over its rusted one, 0 to 1.</summary>
        public float Blood;

        /// <summary>Ticks left of the player held still.</summary>
        public int Still;

        public override void ResetEffects()
        {
            Held = false;
        }

        /// <summary>Held still: no key of theirs moves them or uses anything.</summary>
        public override void SetControls()
        {
            if (Still <= 0)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlThrow = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override void PreUpdateMovement()
        {
            if (Still > 0)
                Player.velocity.X = 0f;
        }

        public override void PostUpdate()
        {
            if (Still > 0)
                Still--;
        }
    }

    /// <summary>
    /// Draws the Rust Side Sword in the player's hand, where vanilla draws a held item behind
    /// the front arm (IronFlowLayer's slot and sums: placed from where the player is being
    /// drawn, riding the walk as the arm does). Its rusted side; its bloodied side over that as
    /// far as RustSidePose.Blood says. Turned over with the player as they roll: the game turns
    /// everything drawn of them (Player.fullRotation).
    /// </summary>
    public class RustSideLayer : PlayerDrawLayer
    {
        private static Asset<Texture2D> sword;
        private static Asset<Texture2D> blood;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            sword = ModContent.Request<Texture2D>(RustSideHoldout.SwordTexture);
            blood = ModContent.Request<Texture2D>(RustSideHoldout.BloodTexture);
        }

        public override void Unload()
        {
            sword = null;
            blood = null;
        }

        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<RustSidePose>().Held;

        /// <summary>
        /// How to draw the sprite held at <paramref name="rotation"/> in the world, mirrored
        /// across its blade when <paramref name="flip"/>. The blade already leans an eighth of a
        /// turn up, so it turns an eighth less than it is held; mirrored top to bottom it leans
        /// an eighth down instead, and the grip's texel goes with the mirror (IronFlowLayer.Place).
        /// </summary>
        public static void Place(float rotation, bool flip, out float turn, out Vector2 origin, out SpriteEffects effects)
        {
            Vector2 grip = RustSideHoldout.Grip;

            origin = flip ? new Vector2(grip.X, RustSideHoldout.Size - grip.Y) : grip;
            turn = flip ? rotation - MathHelper.PiOver4 : rotation + MathHelper.PiOver4;
            effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (sword == null)
                return;

            Player player = drawInfo.drawPlayer;
            RustSidePose pose = player.GetModPlayer<RustSidePose>();

            Vector2 centre = drawInfo.Position + (player.MountedCenter - player.position);

            Vector2[] walks = Main.OffsetsPlayerHeadgear;
            int frame = player.bodyFrame.Height > 0 ? player.bodyFrame.Y / player.bodyFrame.Height : 0;
            Vector2 walk = frame >= 0 && frame < walks.Length ? walks[frame] : new Vector2(0f, 2f);
            walk.Y -= 2f;

            Vector2 hand = centre + new Vector2(pose.Hand.X * player.direction, pose.Hand.Y * player.gravDir) + walk * player.gravDir;
            Vector2 position = (hand - Main.screenPosition).Floor();

            bool flip = (player.direction < 0) != (player.gravDir < 0f);
            float rotation = VisusGrip.ToWorldAngle(player, pose.Blade);

            // Lit where most of it is: the middle of the blade.
            Vector2 middle = VisusGrip.ToWorld(player, pose.Hand + pose.Blade.ToRotationVector2() * 50f);
            Color light = Lighting.GetColor(middle.ToTileCoordinates());

            Place(rotation, flip, out float turn, out Vector2 origin, out SpriteEffects effects);

            if (pose.Blood < 1f || blood == null || !blood.IsLoaded)
                drawInfo.DrawDataCache.Add(new DrawData(sword.Value, position, null, light, turn, origin, 1f, effects));

            if (pose.Blood > 0f && blood != null && blood.IsLoaded)
                drawInfo.DrawDataCache.Add(new DrawData(blood.Value, position, null, light * Math.Min(1f, pose.Blood), turn, origin, 1f, effects));
        }
    }
}
