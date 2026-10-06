using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using PisAndDag.Content.Projectiles.Melee;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The Meat Hook in the hands: there for as long as it is the item held
    /// (BaseIdleHoldoutProjectile), pointed at the cursor, and the hook's mother. The item
    /// itself is never drawn in use; this is the gun.
    ///
    /// It draws nothing itself. Every tick, on every machine, it turns its wielder to the
    /// cursor, puts both their hands on it (VisusGrip's sums: the front hand on the grip, the
    /// back hand onto the line of the stock) and tells MeatHookPlayer how it is held;
    /// <see cref="MeatHookLayer"/> draws it from that, in the hand.
    ///
    /// THE AIM is its wielder's cursor, kept in Projectile.velocity as a length of one and
    /// sent as it turns. The shot is the item's own (MeatHook.Shoot); the gun only kicks with
    /// it, read off the item's animation, which every machine has: up and back for the first
    /// eighth of it, down again over the rest. While the item reloads (PSItem; its wielder's
    /// machine only, nobody else has the count) it is broken open: the muzzle dips and comes
    /// back up.
    ///
    /// THE HOOK is thrown from here, on its wielder's machine: a fresh press of the right
    /// button with the cursor on the world, read off the player's own controls and not through
    /// the item's use, so that it can be thrown while the gun is fired, while it reloads and
    /// with no bullet left. Whatever takes the controls away (a scene, a talk, a door's
    /// passage) takes this with them, and whatever says no item may be used
    /// (ModPlayer.CanUseItem) says so here. A second press, once the hook has held something a
    /// moment, lets go of it.
    /// </summary>
    public class MeatHookHoldOut : BaseIdleHoldoutProjectile
    {
        public const string WithoutHook = "PisAndDag/Content/Projectiles/Ranged/MeatHookHoldOut_WithoutHook";
        public const string WithHook = "PisAndDag/Content/Items/Ranged/MeatHook";

        // ---- the sprite, in its own texels (70 by 22, the gun pointing right) -----------------------

        /// <summary>The wrist of the stock, behind the trigger guard: what the front hand holds.</summary>
        public static readonly Vector2 Grip = new Vector2(12f, 13f);

        /// <summary>The end of the barrels, in the middle of them.</summary>
        public static readonly Vector2 Muzzle = new Vector2(62f, 6f);

        /// <summary>Where the chain is made fast: the tail of the hook as it lies under the barrels.</summary>
        public static readonly Vector2 Mount = new Vector2(44f, 15f);

        /// <summary>The middle of the hook as it lies there: where it is thrown from and comes back to.</summary>
        public static readonly Vector2 Stow = new Vector2(57f, 14f);

        /// <summary>The breech, on top.</summary>
        public static readonly Vector2 Breech = new Vector2(30f, 3f);

        /// <summary>The middle of the gun, for its light.</summary>
        public static readonly Vector2 Middle = new Vector2(35f, 9f);

        // ---- how it moves ------------------------------------------------------------------------------

        /// <summary>The kick: radians the muzzle rises and pixels the gun comes back, a plain shot and one fired while the hook holds.</summary>
        private const float KickAngle = 0.24f;
        private const float HookedKickAngle = 0.36f;
        private const float KickBack = 5f;
        private const float HookedKickBack = 7f;

        /// <summary>The part of a shot's animation the kick rises over.</summary>
        private const float KickRise = 0.12f;

        /// <summary>Broken open to reload: radians the muzzle dips, and the part of the reload it takes to open and to shut.</summary>
        private const float BreakAngle = 0.5f;
        private const float BreakEdge = 0.22f;

        /// <summary>The throw of the hook, and its coming home: ticks the gun shows each for, and how far.</summary>
        private const int ThrowTicks = 8;
        private const float ThrowDip = 0.1f;
        private const float ThrowBack = 3f;
        private const int SeatTicks = 6;
        private const float SeatRise = 0.06f;

        /// <summary>Ticks the hook must have held something before a second press lets go of it.</summary>
        private const int LetGoAfter = 12;

        /// <summary>The aim is sent when it has turned this far (the dot of the two, as a shortfall from 1), or after this many ticks of turning at all.</summary>
        private const float SendTurn = 0.0006f;
        private const int SendEvery = 8;

        // ---- each machine's own ------------------------------------------------------------------------

        private bool hookHome = true;
        private bool heavy;
        private bool reloading;
        private int thrown;
        private int seated;
        private Vector2 sent;
        private int unsent;

        /// <summary>The right button: 0 not yet seen up since the gun came out, 1 up, 2 down since its last press.</summary>
        private ref float Button => ref Projectile.localAI[0];

        public override string Texture => WithoutHook;

        public override int AssociatedItemID => ModContent.ItemType<MeatHook>();
        public override int IntendedProjectileType => ModContent.ProjectileType<MeatHookHoldOut>();

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.netImportant = true;

            // Never drawn as a projectile: MeatHookLayer draws the gun, in the hand.
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>This player's gun in the hand, if it is out.</summary>
        public static MeatHookHoldOut Of(Player player)
        {
            int type = ModContent.ProjectileType<MeatHookHoldOut>();

            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.owner == player.whoAmI && projectile.type == type)
                    return projectile.ModProjectile as MeatHookHoldOut;
            }

            return null;
        }

        // ---- where things are --------------------------------------------------------------------------

        /// <summary>An aim in the world as the player's own upright frame has it: facing right, gravity down.</summary>
        public static float Upright(Player player, Vector2 aim) => new Vector2(aim.X * player.direction, aim.Y * player.gravDir).ToRotation();

        /// <summary>A texel of the gun, upright, with its grip in <paramref name="hand"/> and the gun turned to <paramref name="angle"/>.</summary>
        public static Vector2 Texel(Vector2 hand, float angle, Vector2 texel)
        {
            Vector2 along = angle.ToRotationVector2();
            Vector2 down = new Vector2(-along.Y, along.X);
            Vector2 from = texel - Grip;

            return hand + along * from.X + down * from.Y;
        }

        /// <summary>A texel of the gun as this player holds it now, in the world.</summary>
        public static Vector2 World(Player player, Vector2 texel)
        {
            MeatHookPlayer pose = player.GetModPlayer<MeatHookPlayer>();
            return VisusGrip.ToWorld(player, Texel(pose.Hand, pose.Angle, texel));
        }

        /// <summary>
        /// Where the muzzle is, in the world, as a shot along <paramref name="aim"/> leaves it:
        /// the gun level with the aim, before it has kicked. The item asks, a moment before the
        /// gun itself is turned this tick.
        /// </summary>
        public static Vector2 MuzzleOf(Player player, Vector2 aim)
        {
            float angle = Upright(player, aim);
            Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, angle);

            return VisusGrip.ToWorld(player, Texel(hand, angle, Muzzle));
        }

        /// <summary>
        /// The two pixels the body rises on six frames of the walk, which the arms ride and so
        /// does what is in them: vanilla's own sum (PlayerDrawLayers.DrawPlayer_28_ArmOverItemComposite).
        /// </summary>
        public static Vector2 Walk(Player player)
        {
            Vector2[] walks = Main.OffsetsPlayerHeadgear;
            int frame = player.bodyFrame.Height > 0 ? player.bodyFrame.Y / player.bodyFrame.Height : 0;
            Vector2 walk = frame >= 0 && frame < walks.Length ? walks[frame] : new Vector2(0f, 2f);
            walk.Y -= 2f;

            return walk * player.gravDir;
        }

        /// <summary>
        /// A composite arm's rotation pointing along an upright <paramref name="angle"/>: vanilla's
        /// arm hangs at 0, so the angle less a quarter turn, mirrored for a player facing left.
        /// The right way up always: under reversed gravity vanilla turns it over itself
        /// (Player.SetCompositeArmFront and SetCompositeArmBack), as the Iron Flow's is passed.
        /// </summary>
        private static float Arm(Player player, float angle) => (player.direction == 1 ? angle : MathHelper.Pi - angle) - MathHelper.PiOver2;

        // ---- every tick --------------------------------------------------------------------------------

        public override void SafeAI()
        {
            Player owner = Owner;
            bool mine = Projectile.owner == Main.myPlayer;

            if (mine)
                Aim(owner);

            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            int facing = aim.X > 0f ? 1 : aim.X < 0f ? -1 : owner.direction;

            owner.ChangeDir(facing);
            Projectile.timeLeft = 2;

            // The hook: out or home, as this machine sees it.
            bool home = MeatHook_Hook.Of(owner) == null;

            if (home != hookHome)
            {
                hookHome = home;

                if (home)
                    seated = SeatTicks;
                else
                    thrown = ThrowTicks;
            }

            if (thrown > 0)
                thrown--;

            if (seated > 0)
                seated--;

            // ---- the pose ----

            float level = Upright(owner, aim);
            float kick = Kick(owner, out float back, out float rise);
            float open = mine ? Open(owner, aim) : 0f;
            float throwing = thrown / (float)ThrowTicks;
            float seating = seated / (float)SeatTicks;

            // Upright, up is the lesser angle: the kick raises the muzzle, the break and the throw dip it.
            float angle = level - kick * rise + open * BreakAngle + throwing * ThrowDip - seating * SeatRise;

            Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, level)
                - level.ToRotationVector2() * (kick * back + throwing * ThrowBack);

            float reach = VisusGrip.BackArmOnto(Player.CompositeArmStretchAmount.Full, hand, angle);

            owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Arm(owner, level));
            owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Arm(owner, reach));

            owner.GetModPlayer<MeatHookPlayer>().Hold(angle, hand, hookHome);

            Projectile.Center = World(owner, Middle);
            Projectile.rotation = VisusGrip.ToWorldAngle(owner, angle);

            if (mine)
                Hook(owner, aim);

            // The hook glows where it lies, once it can be thrown.
            if (!Main.dedServ && hookHome && owner.GetModPlayer<MeatHookPlayer>().Wait <= 0)
            {
                float glow = MeatHookLayer.Pulse();
                Lighting.AddLight(World(owner, Stow), 0.3f * glow, 0.05f * glow, 0.035f * glow);
            }
        }

        /// <summary>
        /// Its wielder's machine: the cursor is the aim. Kept as it is every tick, and sent when
        /// it has turned enough to be seen, or has been turning a while.
        /// </summary>
        private void Aim(Player owner)
        {
            Vector2 to = Main.MouseWorld - VisusGrip.Centre(owner);

            // On top of the player there is no way to point: the last one is kept.
            if (to.LengthSquared() > 16f)
                Projectile.velocity = Vector2.Normalize(to);
            else if (Projectile.velocity == Vector2.Zero)
                Projectile.velocity = Vector2.UnitX * owner.direction;

            if (Projectile.velocity == sent)
            {
                unsent = 0;
                return;
            }

            unsent++;

            if (1f - Vector2.Dot(Projectile.velocity, sent) < SendTurn && unsent < SendEvery)
                return;

            sent = Projectile.velocity;
            unsent = 0;
            Projectile.netUpdate = true;
        }

        /// <summary>
        /// The kick, none to all, off the item's animation: it rises over the first of it and
        /// settles over the rest. A shot fired while the hook holds something kicks harder.
        /// </summary>
        private float Kick(Player owner, out float back, out float rise)
        {
            if (owner.itemAnimation > 0 && owner.ItemAnimationJustStarted)
                heavy = MeatHook_Hook.Holds(owner);

            back = heavy ? HookedKickBack : KickBack;
            rise = heavy ? HookedKickAngle : KickAngle;

            if (owner.itemAnimation <= 0 || owner.itemAnimationMax <= 0)
                return 0f;

            float done = 1f - owner.itemAnimation / (float)owner.itemAnimationMax;

            if (done < KickRise)
                return done / KickRise;

            float settle = 1f - (done - KickRise) / (1f - KickRise);
            return settle * settle;
        }

        /// <summary>
        /// Broken open to reload, none to all: open over the first of the reload, shut over the
        /// last of it. Powder smoke out of the breech as it opens.
        /// </summary>
        private float Open(Player owner, Vector2 aim)
        {
            PSItem magazine = owner.HeldItem.PIS();
            bool now = magazine.Reloading && magazine.ReloadTime > 0;

            if (now && !reloading && !Main.dedServ)
                MuzzleSmoke.Spawn(World(owner, Breech), -Vector2.UnitY * owner.gravDir - aim * 0.4f, 2);

            reloading = now;

            if (!now)
                return 0f;

            float done = 1f - magazine.ReloadTimer / (float)magazine.ReloadTime;
            float open = Utils.GetLerpValue(0f, BreakEdge, done, true) * Utils.GetLerpValue(1f, 1f - BreakEdge, done, true);

            return open * open * (3f - 2f * open);
        }

        /// <summary>Its wielder's machine: the right button, pressed afresh, throws the hook or lets go of what it holds.</summary>
        private void Hook(Player owner, Vector2 aim)
        {
            if (!owner.controlUseTile)
            {
                Button = 1f;
                return;
            }

            if (Button != 1f)
                return;

            Button = 2f;

            // The press belongs to something else: a slot, a chest, a door, somebody to talk to.
            if (owner.mouseInterface || owner.tileInteractionHappened || Main.HoveringOverAnNPC || Main.SmartInteractShowingGenuine
                || Main.mapFullscreen || CaptureManager.Instance.Active || owner.selectedItem == 58)
                return;

            if (owner.noItems || owner.CCed || !PlayerLoader.CanUseItem(owner, owner.HeldItem))
                return;

            MeatHook_Hook hook = MeatHook_Hook.Of(owner);

            if (hook != null)
            {
                if (hook.LatchedFor >= LetGoAfter)
                    hook.LetGo();

                return;
            }

            if (owner.GetModPlayer<MeatHookPlayer>().Wait > 0)
                return;

            // From where it lies; from the middle of the one who throws it if a wall is between.
            Vector2 from = World(owner, Stow);
            Vector2 middle = VisusGrip.Centre(owner);

            if (!Collision.CanHit(middle, 0, 0, from, 0, 0))
                from = middle;

            int damage = (int)(owner.GetWeaponDamage(owner.HeldItem) * MeatHook.HookDamage);

            Projectile.NewProjectile(owner.GetSource_ItemUse(owner.HeldItem), from, aim * MeatHook_Hook.FlySpeed,
                ModContent.ProjectileType<MeatHook_Hook>(), damage, 0f, owner.whoAmI);
        }

        public override void OnKill(int timeLeft)
        {
            // Put away: not drawn again, not even for the tick it goes on.
            Owner.GetModPlayer<MeatHookPlayer>().Held = false;
        }
    }

    /// <summary>
    /// Draws the Meat Hook in the player's hands, where vanilla draws a held item behind the
    /// front arm: after the body and the back arm, before the front arm, so the hand closes
    /// over the grip. Only while MeatHookPlayer.Held.
    ///
    /// Placed from where the player is being drawn and not from where they are, so it rides
    /// the walk with the arms and is mirrored with them, left to right and under reversed
    /// gravity (IronFlowLayer's way).
    ///
    /// TWO SPRITES, one gun. The gun without its hook is the holdout's own texture. The hook
    /// as it lies under the barrels is the item's sprite less every pixel the gun has: made
    /// once, here, out of the two (they are the same size and the same gun, pixel for pixel),
    /// so it can be drawn dark while it waits, glowing when it can be thrown, and not at all
    /// while it is out. If the two are ever not the same size there is no telling the hook
    /// from the gun, and the item's whole sprite is drawn while the hook is home.
    /// </summary>
    public class MeatHookLayer : PlayerDrawLayer
    {
        private static Asset<Texture2D> gun;
        private static Asset<Texture2D> whole;

        /// <summary>The hook as it lies on the gun. Made on the first frame it is wanted.</summary>
        private static Texture2D hook;
        private static bool hookTried;

        /// <summary>The red it glows, as light added to what is behind it.</summary>
        private static readonly Color Glow = new Color(255, 52, 36, 0);

        /// <summary>How dark the hook lies while it cannot be thrown.</summary>
        private const float Waiting = 0.5f;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            gun = ModContent.Request<Texture2D>(MeatHookHoldOut.WithoutHook);
            whole = ModContent.Request<Texture2D>(MeatHookHoldOut.WithHook);
        }

        public override void Unload()
        {
            gun = null;
            whole = null;

            Texture2D made = hook;
            hook = null;
            hookTried = false;

            if (made != null)
                Main.QueueMainThreadAction(() => made.Dispose());
        }

        /// <summary>The hook's glow as it beats, the same on the gun and in the air.</summary>
        public static float Pulse() => 0.78f + 0.22f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7.5f);

        // Vanilla's own slot for an item held behind the front arm (IronFlowLayer's and VisusLayer's too).
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<MeatHookPlayer>().Held;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (gun == null || !gun.IsLoaded)
                return;

            Player player = drawInfo.drawPlayer;
            MeatHookPlayer pose = player.GetModPlayer<MeatHookPlayer>();
            Texture2D texture = gun.Value;

            // The middle the arms are drawn from, where the player is being drawn.
            Vector2 centre = drawInfo.Position + (player.MountedCenter - player.position);
            Vector2 hand = centre + new Vector2(pose.Hand.X * player.direction, pose.Hand.Y * player.gravDir) + MeatHookHoldOut.Walk(player);
            Vector2 position = (hand - Main.screenPosition).Floor();

            // Mirrored top to bottom when it points left (and back again under reversed gravity): the grip's texel goes with the mirror.
            bool flip = (player.direction < 0) != (player.gravDir < 0f);
            Vector2 grip = MeatHookHoldOut.Grip;
            Vector2 origin = flip ? new Vector2(grip.X, texture.Height - grip.Y) : grip;
            SpriteEffects effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float rotation = VisusGrip.ToWorldAngle(player, pose.Angle);

            Color light = Lighting.GetColor(MeatHookHoldOut.World(player, MeatHookHoldOut.Middle).ToTileCoordinates());

            Texture2D lying = pose.HookHome ? Hook(texture) : null;

            // No telling the hook from the gun: the item's whole sprite while the hook is home.
            if (pose.HookHome && lying == null && whole != null && whole.IsLoaded)
            {
                Texture2D both = whole.Value;
                Vector2 pivot = flip ? new Vector2(grip.X, both.Height - grip.Y) : grip;

                drawInfo.DrawDataCache.Add(new DrawData(both, position, null, light, rotation, pivot, 1f, effects));
                return;
            }

            drawInfo.DrawDataCache.Add(new DrawData(texture, position, null, light, rotation, origin, 1f, effects));

            if (lying == null)
                return;

            if (player.GetModPlayer<MeatHookPlayer>().Wait > 0)
            {
                Color dark = new Color((int)(light.R * Waiting), (int)(light.G * Waiting), (int)(light.B * Waiting), light.A);
                drawInfo.DrawDataCache.Add(new DrawData(lying, position, null, dark, rotation, origin, 1f, effects));
                return;
            }

            // Ready: the glow round it, then the hook itself, lit by its own heat as much as by the place.
            Color glow = Glow * (0.36f * Pulse());

            for (int i = 0; i < 4; i++)
            {
                Vector2 off = new Vector2(i < 2 ? -1.5f : 1.5f, i % 2 == 0 ? -1.5f : 1.5f);
                drawInfo.DrawDataCache.Add(new DrawData(lying, position + off, null, glow, rotation, origin, 1f, effects));
            }

            Color hot = new Color(Math.Max((int)light.R, 150), Math.Max((int)light.G, 96), Math.Max((int)light.B, 84), light.A);
            drawInfo.DrawDataCache.Add(new DrawData(lying, position, null, hot, rotation, origin, 1f, effects));
        }

        /// <summary>
        /// The hook as it lies under the barrels: the item's sprite with every pixel of the
        /// gun's taken out. Null when the two sprites are not the same size, or nothing of the
        /// item's is left.
        /// </summary>
        private static Texture2D Hook(Texture2D without)
        {
            if (hookTried)
                return hook != null && !hook.IsDisposed ? hook : null;

            hookTried = true;

            try
            {
                Texture2D with = ModContent.Request<Texture2D>(MeatHookHoldOut.WithHook, AssetRequestMode.ImmediateLoad).Value;

                if (with.Width != without.Width || with.Height != without.Height)
                    return null;

                int count = with.Width * with.Height;
                Color[] pixels = new Color[count];
                Color[] gunOnly = new Color[count];

                with.GetData(pixels);
                without.GetData(gunOnly);

                bool any = false;

                for (int i = 0; i < count; i++)
                {
                    if (gunOnly[i].A != 0)
                        pixels[i] = default;
                    else if (pixels[i].A != 0)
                        any = true;
                }

                if (!any)
                    return null;

                hook = new Texture2D(Main.graphics.GraphicsDevice, with.Width, with.Height);
                hook.SetData(pixels);
            }
            catch (Exception e)
            {
                hook = null;
                ModContent.GetInstance<MeatHookLayer>()?.Mod.Logger.Warn("Meat Hook: the hook could not be told from the gun in their two sprites; the item's whole sprite is drawn while the hook is home.", e);
            }

            return hook;
        }
    }
}
