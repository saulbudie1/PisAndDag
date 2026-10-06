using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// The Iron Flow in the hand: there for as long as the item is the one held (the idle
    /// holdout's own rule, BaseIdleHoldoutProjectile), and the weapon itself. It hangs, it
    /// cuts down, up and all the way round, it parries (IronFlowMoves has every pose of each,
    /// IronFlowState where it is in them); the item tells it which (<see cref="StartCut"/>,
    /// <see cref="StartParry"/>), on its owner's machine.
    ///
    /// ai[0] is the move it is in and a count that goes up with every move begun, so that
    /// another machine sees the same move twice as two; ai[1] how many ticks the move takes;
    /// ai[2] the side it faces. The ticks into the move are counted on each machine for
    /// itself, from when it hears of it.
    ///
    /// The cuts come in threes, down, up, round, each begun from where the last left the
    /// blade; left more than <see cref="Rhythm"/> ticks, or parried between, they start over.
    /// Each carries its owner forward a little (IronFlowMoves.Step, as Visus's thrust does:
    /// <see cref="Step"/>).
    ///
    /// It cuts along the blade (a line from the guard to the point, <see cref="Width"/> wide)
    /// over the whole of the blade's sweep since last tick, each thing once a cut, and only
    /// what its owner can see (ownerHitCheck). Its damage, knockback, critical chance and
    /// armour penetration are its owner's with the item, read again every tick: it outlives
    /// whatever they were when it appeared.
    ///
    /// The parry itself is not here: this starts one (ParryPlayer.Begin, with the item as
    /// what parries) and shows it.
    ///
    /// Drawn in the hand by IronFlowLayer, from the pose this sets every tick (IronFlowPose);
    /// this draws only what the blade leaves behind when it is fast (PreDraw, behind the
    /// player): three pale shadows of itself and a band of its own pink.
    /// </summary>
    public class IronFlowHoldout : BaseIdleHoldoutProjectile
    {
        public const string SwordTexture = "PisAndDag/Content/Items/Melee/IronFlow";

        public override string Texture => SwordTexture;

        public override int AssociatedItemID => ModContent.ItemType<IronFlow>();
        public override int IntendedProjectileType => ModContent.ProjectileType<IronFlowHoldout>();

        // ---- the sprite (IronFlow.png: the sword on the diagonal, the point up and to the right) ----

        /// <summary>The sprite is this many texels square.</summary>
        public const int Size = 64;

        /// <summary>The middle of the handle, where the hand holds it: in texels from the sprite's top left.</summary>
        public static readonly Vector2 Grip = new Vector2(11f, 53f);

        /// <summary>What it cuts with: from this far out from the hand to this far, this wide, in pixels.</summary>
        private const float Inner = 12f, Outer = 66f, Width = 38f;

        /// <summary>The middle of the hooked blade, out from the hand.</summary>
        private const float Middle = 46f;

        /// <summary>The trail: from and to this far out from the hand, over this many ticks of the blade's way, gone this many ticks after the blade slows.</summary>
        private const float TrailInner = 26f, TrailOuter = 68f;
        private const float TrailTicks = 3.5f;
        private const float FadeTicks = 6f;

        /// <summary>Ticks after a cut ends within which the next cut follows it; after that, the first again.</summary>
        public const int Rhythm = 40;

        /// <summary>The fastest a cut may carry its owner, pixels a tick.</summary>
        private const float StepSpeedCap = 4f;

        private static readonly Color Pink = new Color(245, 147, 86);
        private static readonly Color Pale = new Color(247, 190, 119);
        private static readonly Color Dusk = new Color(148, 71, 30);
        private static readonly Color[] Sparks = { new Color(252, 183, 109), new Color(255, 209, 130), new Color(217, 211, 165) };

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> line;

        private FlowMove Told => (FlowMove)((int)Projectile.ai[0] & 7);

        private float ToldTicks => Math.Max(1f, Projectile.ai[1]);

        private int Facing => Projectile.ai[2] < 0f ? -1 : 1;

        private readonly IronFlowState state = new IronFlowState();

        private bool started;

        /// <summary>ai[0] as it was last tick: a change is a move begun somewhere else.</summary>
        private float seen;

        /// <summary>The last cut begun (Rest after a parry): the next follows it.</summary>
        private FlowMove lastCut = FlowMove.Rest;

        /// <summary>The front hand this tick, in the player's own frame.</summary>
        private Vector2 hand;

        /// <summary>How far this cut has carried its owner so far, and where they were last tick (their own machine only).</summary>
        private float stepped;
        private float lastX;

        /// <summary>Nothing under way: it can be told to do something.</summary>
        public bool Idle => !state.Busy;

        /// <summary>The cut a left click gets now.</summary>
        public FlowMove NextCut => state.Idle > Rhythm ? FlowMove.Down : IronFlowMoves.After(lastCut);

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
        public static IronFlowHoldout Of(Player player)
        {
            int index = player.GetModPlayer<IronFlowPose>().Sword;

            if (index < 0 || index >= Main.maxProjectiles)
                return null;

            Projectile projectile = Main.projectile[index];

            return projectile.active && projectile.owner == player.whoAmI ? projectile.ModProjectile as IronFlowHoldout : null;
        }

        // ---- told what to do, on its owner's machine ---------------------------------------------

        /// <summary>The next cut, taking this many ticks.</summary>
        internal void StartCut(int ticks, int facing)
        {
            Begin(NextCut, ticks, facing);
        }

        /// <summary>
        /// The parry: its own ticks, and the player's side of it started with it, with the
        /// sword itself as what parries: an enemy it turns aside takes a cut's damage.
        /// </summary>
        internal void StartParry(int facing)
        {
            ParryTiming timing = ParryTiming.CurvedSword;

            Begin(FlowMove.Parry, timing.Total, facing);
            Owner.GetModPlayer<ParryPlayer>().Begin(timing, facing, Owner.HeldItem);
        }

        private void Begin(FlowMove move, int ticks, int facing)
        {
            int count = (((int)Projectile.ai[0] >> 3) + 1) & 0xFF;

            Projectile.ai[0] = count * 8 + (int)move;
            Projectile.ai[1] = ticks;
            Projectile.ai[2] = facing;

            // Sent from the end of the sword's next update. Not netUpdate: the item tells it
            // this from the player's own update, and the game forgets that one at the start
            // of every update of a projectile, before it looks (Projectile.Update). This is
            // the one the game itself sets from outside one (a fishing bobber, reeled in).
            Projectile.netUpdate2 = true;

            seen = Projectile.ai[0];
            Start();
        }

        private void Start()
        {
            FlowMove move = Told;

            state.Begin(move, ToldTicks);
            lastCut = move == FlowMove.Parry ? FlowMove.Rest : move;

            stepped = 0f;
            lastX = Owner.position.X;

            Projectile.ResetLocalNPCHitImmunity();
        }

        /// <summary>Gone in the middle of a parry (put away, taken from the hand): the parry goes with it.</summary>
        public override void OnKill(int timeLeft)
        {
            if (Projectile.owner == Main.myPlayer && state.Busy && state.Move == FlowMove.Parry)
                Owner.GetModPlayer<ParryPlayer>().Cancel();
        }

        public override void SafeAI()
        {
            Player player = Owner;

            Projectile.timeLeft = 2;

            if (!started)
            {
                started = true;
                seen = Projectile.ai[0];
            }
            else if (Projectile.ai[0] != seen)
            {
                seen = Projectile.ai[0];

                if (Told != FlowMove.Rest)
                    Start();
            }

            if (Projectile.owner == Main.myPlayer)
            {
                Item item = player.HeldItem;

                Projectile.damage = player.GetWeaponDamage(item);
                Projectile.knockBack = player.GetWeaponKnockback(item);
                Projectile.CritChance = player.GetWeaponCrit(item);
                Projectile.ArmorPenetration = player.GetWeaponArmorPenetration(item);
            }

            FlowMove move = state.Move;
            float before = state.Progress;

            state.Tick();

            float progress = state.Progress;

            if (state.Busy)
                player.ChangeDir(Facing);

            Player.CompositeArmStretchAmount reach = state.Bent ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full;
            float arm = MathHelper.ToRadians(state.Arm);
            float blade = MathHelper.ToRadians(state.Blade);

            hand = VisusGrip.FrontHand(reach, arm);

            // The arm as it is the right way up: vanilla turns it over itself under reversed
            // gravity (Player.SetCompositeArmFront).
            player.SetCompositeArmFront(true, reach, (player.direction == 1 ? arm : MathHelper.Pi - arm) - MathHelper.PiOver2);

            IronFlowPose pose = player.GetModPlayer<IronFlowPose>();
            pose.Held = true;
            pose.Blade = blade;
            pose.Hand = hand;
            pose.Sword = Projectile.whoAmI;

            bool hitting = Hitting;

            // On the blade while it cuts (what vanilla cuts grass under, and where a flask's
            // drops show), in the hand otherwise.
            Projectile.Center = VisusGrip.ToWorld(player, hand + blade.ToRotationVector2() * (hitting ? Middle : 0f));
            Projectile.velocity = Vector2.Zero;
            Projectile.direction = Projectile.spriteDirection = player.direction;
            Projectile.noEnchantmentVisuals = !hitting;

            if (state.Busy)
            {
                if (Projectile.owner == Main.myPlayer)
                    Step(player, move, progress);

                if (!Main.dedServ)
                    Sounds(move, before, progress);
            }

            state.Finish();
        }

        /// <summary>
        /// The step a cut carries its owner. Where they should be by the end of the next tick,
        /// along the ground, on a curve with nothing moving at either end, and the speed that
        /// gets them there, set only when it is more than they already have forward: a cut never
        /// slows a run and never holds anyone back. It measures where they really went, so
        /// ground friction, a held key and a wall all come out in the next tick's sum, and a
        /// wall that ends does not fling anyone (the speed is capped). Visus's thrust's own.
        /// </summary>
        private void Step(Player player, FlowMove move, float progress)
        {
            int facing = Facing;

            stepped += (player.position.X - lastX) * facing;
            lastX = player.position.X;

            float reach = IronFlowMoves.Step(move, out float from, out float length);

            if (reach < 1f || length <= 0f || progress < from || progress > from + length || player.mount.Active || player.grappling[0] >= 0)
                return;

            float next = reach * Easing.Smoother((progress + 1f / state.Duration - from) / length);

            // Ground friction takes this off before the player moves, when nothing is held.
            float friction = player.velocity.Y == 0f ? player.runSlowdown : 0f;
            float speed = Math.Min(next - stepped + friction, StepSpeedCap);

            if (speed > player.velocity.X * facing)
                player.velocity.X = speed * facing;
        }

        private void Sounds(FlowMove move, float before, float progress)
        {
            IronFlowMoves.Fast(move, out float from, out float to);

            if (before >= from || progress < from)
                return;

            SoundStyle swing = move switch
            {
                FlowMove.Up => SoundID.Item1 with { Pitch = 0.15f },
                FlowMove.Circle => SoundID.Item1 with { Pitch = -0.25f, Volume = 1.1f },
                FlowMove.Parry => SoundID.Item1 with { Pitch = 0.5f, Volume = 0.7f },
                _ => SoundID.Item1 with { Pitch = -0.05f },
            };

            SoundEngine.PlaySound(swing, Projectile.Center);
        }

        // ---- what it hits ------------------------------------------------------------------------

        public override bool? CanDamage() => Hitting ? null : false;

        /// <summary>The blade at this angle, in the world: from the guard to the point.</summary>
        private void Edge(Player player, float degrees, out Vector2 from, out Vector2 to)
        {
            Vector2 u = MathHelper.ToRadians(degrees).ToRotationVector2();

            from = VisusGrip.ToWorld(player, hand + u * Inner);
            to = VisusGrip.ToWorld(player, hand + u * Outer);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Hitting)
                return false;

            Player player = Owner;
            float collision = 0f;

            // Last tick's angle to this one's, a step every 12 degrees or less, so nothing
            // between is stepped over.
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(state.Blade - state.PreviousBlade) / 12f));

            for (int i = 0; i <= steps; i++)
            {
                Edge(player, MathHelper.Lerp(state.PreviousBlade, state.Blade, i / (float)steps), out Vector2 from, out Vector2 to);

                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, Width, ref collision))
                    return true;
            }

            return false;
        }

        public override void CutTiles()
        {
            if (!Hitting)
                return;

            Edge(Owner, state.Blade, out Vector2 from, out Vector2 to);

            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(from, to, Width, DelegateMethods.CutTiles);
        }

        /// <summary>What it hits is knocked away from its owner: the circle goes behind them too.</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.Center.X < Owner.Center.X ? -1 : 1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            Player player = Owner;
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);

            // The way the blade is going: across itself, down in front for the cut down and the
            // circle, up for the cut up.
            Vector2 u = MathHelper.ToRadians(state.Blade).ToRotationVector2();
            Vector2 travel = new Vector2(-u.Y * player.direction, u.X * player.gravDir) * (state.Move == FlowMove.Up ? -1f : 1f);

            for (int i = 0; i < 4; i++)
            {
                float length = Main.rand.NextFloat(14f, 24f);

                PADParticleHandler.SpawnParticle(new SparkShard(at, travel.RotatedByRandom(0.7f) * Main.rand.NextFloat(2.5f, 5.5f),
                    Sparks[Main.rand.Next(Sparks.Length)], length * 0.5f, 14f, length, Main.rand.Next(12, 18)));
            }
        }

        // ---- what it leaves behind -----------------------------------------------------------------

        /// <summary>The hand at this point of a move, in the player's own frame.</summary>
        private static Vector2 HandAt(FlowMove move, float progress) =>
            VisusGrip.FrontHand(IronFlowMoves.Bent(move, progress) ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full,
                MathHelper.ToRadians(IronFlowMoves.Arm(move, progress)));

        public override bool PreDraw(ref Color lightColor)
        {
            if (!state.Busy)
                return false;

            FlowMove move = state.Move;
            IronFlowMoves.Fast(move, out float from, out float to);

            float now = state.Progress;

            if (now <= from)
                return false;

            // All of it while the blade is fast, gone a few ticks after it slows.
            float fade = MathHelper.Clamp(1f - (state.Timer - to * state.Duration) / FadeTicks, 0f, 1f);

            if (fade <= 0f)
                return false;

            Player player = Owner;
            float head = Math.Min(now, to);
            float tail = Math.Max(from, head - TrailTicks / state.Duration);

            DrawShadows(player, move, head, from, fade);

            if (smear != null && line != null && head - tail > 0.004f)
            {
                Main.spriteBatch.End();
                DrawTrail(player, move, head, tail, fade);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                    Main.Rasterizer, null, Main.Transform);
            }

            return false;
        }

        /// <summary>Three shadows of the sword along the way it came, each a tick behind the last, in its own light.</summary>
        private void DrawShadows(Player player, FlowMove move, float head, float from, float fade)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            bool flip = (player.direction < 0) != (player.gravDir < 0f);

            for (int i = 3; i >= 1; i--)
            {
                float p = head - i / state.Duration;

                if (p < from)
                    continue;

                float a = MathHelper.ToRadians(IronFlowMoves.Blade(move, p));
                Vector2 at = VisusGrip.ToWorld(player, HandAt(move, p)) - Main.screenPosition;

                Color light = new Color(255, 218, 170, 0) * (0.3f * (1f - i / 4f) * fade);

                IronFlowLayer.Place(VisusGrip.ToWorldAngle(player, a), flip, out float turn, out Vector2 origin, out SpriteEffects effects);
                Main.EntitySpriteDraw(texture, at, null, light, turn, origin, 1f, effects, 0);
            }
        }

        /// <summary>Steps along the trail.</summary>
        private const int Steps = 22;

        // Laid out again every frame it is drawn, only ever on the main thread.
        private static readonly Vector2[] TrailIn = new Vector2[Steps + 1];
        private static readonly Vector2[] TrailOut = new Vector2[Steps + 1];
        private static readonly Vector2[] TrailEdge = new Vector2[Steps + 1];
        private static readonly float[] TrailStrength = new float[Steps + 1];

        /// <summary>
        /// The trail: a band from the guard to the point over the blade's way from
        /// <paramref name="head"/> back to <paramref name="tail"/>, strongest at the head.
        /// First dark, so that it shows on a bright sky; then its own pink over that, as light;
        /// then a paler line along the point's own path.
        /// </summary>
        private void DrawTrail(Player player, FlowMove move, float head, float tail, float fade)
        {
            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                float p = MathHelper.Lerp(head, tail, k);

                Vector2 u = MathHelper.ToRadians(IronFlowMoves.Blade(move, p)).ToRotationVector2();
                Vector2 grip = HandAt(move, p);

                TrailIn[i] = VisusGrip.ToWorld(player, grip + u * TrailInner);
                TrailOut[i] = VisusGrip.ToWorld(player, grip + u * TrailOuter);
                TrailEdge[i] = VisusGrip.ToWorld(player, grip + u * (TrailOuter - 8f));
                TrailStrength[i] = (float)Math.Pow(1f - k, 1.3f) * fade;
            }

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dark = new Color(Dusk.R, Dusk.G, Dusk.B, 255) * (0.45f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], dark, dark, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color light = new Color(Pink.R, Pink.G, Pink.B, 0) * (0.5f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], light, light, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color pale = new Color(Pale.R, Pale.G, Pale.B, 0) * (0.85f * TrailStrength[i]);
                Strip.Pair(TrailEdge[i], TrailOut[i], pale, pale, i / (float)Steps);
            }
            Strip.Draw(line.Value);
        }
    }

    /// <summary>
    /// The Iron Flow as the player holds it this tick, for IronFlowLayer to draw: set by the
    /// sword's own AI on every machine, cleared at the start of every tick, so it is drawn
    /// exactly while the sword is there. And which projectile the sword is, for the item to
    /// find it by (IronFlowHoldout.Of).
    /// </summary>
    public class IronFlowPose : ModPlayer
    {
        public bool Held;

        /// <summary>The blade's angle, in the player's own frame, radians.</summary>
        public float Blade;

        /// <summary>The front hand, in the player's own frame.</summary>
        public Vector2 Hand;

        /// <summary>The sword's place among the projectiles; less than none before there has been one.</summary>
        public int Sword = -1;

        public override void ResetEffects()
        {
            Held = false;
        }
    }

    /// <summary>
    /// Draws the Iron Flow in the player's hand, where vanilla draws a held item behind the
    /// front arm: after the body and the back arm, before the front arm, so the hand closes
    /// over the handle. Only while IronFlowPose.Held.
    ///
    /// Placed from where the player is being drawn rather than from where they are, so it is
    /// in the hand in the inventory's picture of them as well; and it rides the two pixels
    /// the body rises on six frames of the walk, as the arm does (the same sum as vanilla's
    /// for the arm, PlayerDrawLayers.DrawPlayer_28_ArmOverItemComposite).
    /// </summary>
    public class IronFlowLayer : PlayerDrawLayer
    {
        private static Asset<Texture2D> sword;

        public override void Load()
        {
            if (!Main.dedServ)
                sword = ModContent.Request<Texture2D>(IronFlowHoldout.SwordTexture);
        }

        public override void Unload()
        {
            sword = null;
        }

        // Vanilla's own slot for an item held behind the front arm (VisusLayer's too).
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<IronFlowPose>().Held;

        /// <summary>
        /// How to draw the sprite held at <paramref name="rotation"/> in the world, mirrored
        /// across its blade when <paramref name="flip"/>. The blade already leans an eighth of
        /// a turn up, so it turns an eighth less than it is held; mirrored top to bottom it
        /// leans an eighth down instead, and the grip's texel goes with the mirror (PolearmArt's
        /// rule).
        /// </summary>
        public static void Place(float rotation, bool flip, out float turn, out Vector2 origin, out SpriteEffects effects)
        {
            Vector2 grip = IronFlowHoldout.Grip;

            origin = flip ? new Vector2(grip.X, IronFlowHoldout.Size - grip.Y) : grip;
            turn = flip ? rotation - MathHelper.PiOver4 : rotation + MathHelper.PiOver4;
            effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (sword == null)
                return;

            Player player = drawInfo.drawPlayer;
            IronFlowPose pose = player.GetModPlayer<IronFlowPose>();

            // The middle the arms are drawn from, where the player is being drawn.
            Vector2 centre = drawInfo.Position + (player.MountedCenter - player.position);

            // The walk: vanilla's own sum for the body and the arms.
            Vector2[] walks = Main.OffsetsPlayerHeadgear;
            int frame = player.bodyFrame.Height > 0 ? player.bodyFrame.Y / player.bodyFrame.Height : 0;
            Vector2 walk = frame >= 0 && frame < walks.Length ? walks[frame] : new Vector2(0f, 2f);
            walk.Y -= 2f;

            Vector2 hand = centre + new Vector2(pose.Hand.X * player.direction, pose.Hand.Y * player.gravDir) + walk * player.gravDir;
            Vector2 position = (hand - Main.screenPosition).Floor();

            bool flip = (player.direction < 0) != (player.gravDir < 0f);
            float rotation = VisusGrip.ToWorldAngle(player, pose.Blade);

            // Lit where most of it is: the middle of the blade.
            Vector2 middle = VisusGrip.ToWorld(player, pose.Hand + pose.Blade.ToRotationVector2() * 40f);
            Color light = Lighting.GetColor(middle.ToTileCoordinates());

            Place(rotation, flip, out float turn, out Vector2 origin, out SpriteEffects effects);

            drawInfo.DrawDataCache.Add(new DrawData(sword.Value, position, null, light, turn, origin, 1f, effects));
        }
    }
}
