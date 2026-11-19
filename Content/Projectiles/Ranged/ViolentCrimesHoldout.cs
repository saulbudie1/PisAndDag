using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using PisAndDag.Content.Projectiles.Melee;
using ReLogic.Content;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Capture;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// Violent Crimes in the hands: there for as long as it is the item held (BaseIdleHoldoutProjectile), pointed at the
    /// cursor, and everything the gun does (the item itself is never used).
    ///
    /// THE BUTTONS are read here, on its holder's machine, off the player's own controls (as the Meat Hook's hook is),
    /// so that each can be held: whatever takes the controls away (a scene, a talk, the inventory under the cursor)
    /// takes these with them, and a right press that belongs to something else (a chest, a door, somebody to talk to)
    /// is not the gun's. The first button down has the gun till it is let go.
    ///
    ///   RIGHT   FLAME for five seconds (a flame every 6 ticks), then LASER: a flame laser on the tick it turns over
    ///           and every 60 after.
    ///   LEFT    GAS for five seconds (a cloud every 6 ticks), then THROW: its holder thrown toward the cursor on the
    ///           tick it turns over and every 60 after, 30 ticks a throw (finished even if the button is let go),
    ///           chlorine left along the way. Thrown, they face back the way they came, the gun's mouth behind them:
    ///           it is the jet that throws them.
    ///
    /// A Gel as each hold begins and every half second of it; none, and it stops till the button is pressed again.
    ///
    /// WHAT IS TOLD: its aim (Projectile.velocity, a length of one), what it is doing (ai[0]), the tick of the hold
    /// (ai[1]: its holder's count, the others counting on from it), the tick the present laser or throw began on
    /// (ai[2]), and the way of the throw. Every machine poses the gun and its holder from those, throws its holder
    /// (ViolentCrimesPlayer, so nobody sees them dragged), and makes what is only seen and heard; only its holder's
    /// machine makes the shots.
    ///
    /// It draws nothing itself: ViolentCrimesLayer draws it in the hands, from what ViolentCrimesPlayer is told here.
    /// </summary>
    public class ViolentCrimesHoldout : BaseIdleHoldoutProjectile
    {
        public const string Sprite = "PisAndDag/Content/Items/Ranged/ViolentCrimes";
        public const string GlowSprite = "PisAndDag/Content/Items/Ranged/ViolentCrimesGlow";

        // ---- the sprite, in its own texels (80 by 40, the gun pointing right) ------------------------------------------

        /// <summary>
        /// The top of the rear grip, under the body: what the front hand holds. (Its two tanks are forward of it, over and
        /// under the barrel, so that held they stand clear of their holder's head; the short stock lies on the shoulder.)
        /// </summary>
        public static readonly Vector2 Grip = new(11f, 25f);

        /// <summary>The middle of the nozzle's mouth.</summary>
        public static readonly Vector2 Muzzle = new(79f, 19f);

        /// <summary>The pilot light, under the mouth.</summary>
        public static readonly Vector2 Pilot = new(78f, 29f);

        /// <summary>The middle of the gun, for its light.</summary>
        public static readonly Vector2 Middle = new(40f, 19f);

        // ---- how it moves ---------------------------------------------------------------------------------------------------

        /// <summary>A laser's kick: radians the mouth rises, pixels the gun comes back.</summary>
        private const float KickAngle = 0.2f;
        private const float KickBack = 6f;

        /// <summary>The aim is sent when it has turned this far (the dot of the two, as a shortfall from 1), or after this many ticks of turning at all.</summary>
        private const float SendTurn = 0.0006f;
        private const int SendEvery = 8;

        public static readonly SoundStyle Hiss = new("PisAndDag/Content/Sounds/Mech/TwinsGasLeak")
        {
            Volume = 0.5f,
            MaxInstances = 2,
        };

        public enum Work : byte
        {
            None,
            Flame,
            Laser,
            Gas,
            Throw,
        }

        private Work Now
        {
            get => (Work)(int)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        /// <summary>The tick of the hold this is (0 on its first).</summary>
        private ref float Held => ref Projectile.ai[1];

        /// <summary>The tick of the hold the present laser or throw began on.</summary>
        private ref float Round => ref Projectile.ai[2];

        /// <summary>The way of the present throw (a length of one).</summary>
        private Vector2 throwWay = Vector2.UnitX;

        // ---- each machine's own ------------------------------------------------------------------------------------------

        /// <summary>Which button has the gun (its holder's machine): 0 none, 1 left, 2 right.</summary>
        private int button;

        /// <summary>The right button: 0 not yet seen up since the gun came out, 1 up, 2 down and the gun's, 3 down and something else's.</summary>
        private int right;

        /// <summary>Out of Gel: nothing till every button is up again.</summary>
        private bool dry;

        private Vector2 sent;
        private int unsent;

        private float shown;
        private bool posed;
        private float kick;

        private Work seenWork;
        private float seenRound = -1f;
        private SlotId hiss = SlotId.Invalid;
        private bool hissFading;

        public override string Texture => Sprite;

        public override int AssociatedItemID => ModContent.ItemType<ViolentCrimes>();
        public override int IntendedProjectileType => ModContent.ProjectileType<ViolentCrimesHoldout>();

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

            // Never drawn as a projectile: ViolentCrimesLayer draws the gun, in the hands.
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(throwWay.X);
            writer.Write(throwWay.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            throwWay = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        // ---- where things are --------------------------------------------------------------------------------------------

        /// <summary>A texel of the gun, upright, with its grip in <paramref name="hand"/> and the gun turned to <paramref name="angle"/>.</summary>
        public static Vector2 Texel(Vector2 hand, float angle, Vector2 texel)
        {
            Vector2 along = angle.ToRotationVector2();
            Vector2 down = new(-along.Y, along.X);
            Vector2 from = texel - Grip;

            return hand + along * from.X + down * from.Y;
        }

        /// <summary>A texel of the gun as this player holds it now, in the world.</summary>
        public static Vector2 World(Player player, Vector2 texel)
        {
            ViolentCrimesPlayer pose = player.GetModPlayer<ViolentCrimesPlayer>();
            return VisusGrip.ToWorld(player, Texel(pose.Hand, pose.Angle, texel));
        }

        /// <summary>The gun's mouth in the world, if this player has it in their hands.</summary>
        public static bool TryMuzzle(Player player, out Vector2 mouth)
        {
            mouth = default;

            if (!player.GetModPlayer<ViolentCrimesPlayer>().Held)
                return false;

            mouth = World(player, Muzzle);
            return true;
        }

        /// <summary>
        /// A composite arm's rotation pointing along an upright <paramref name="angle"/> (the Meat Hook's sum): vanilla's
        /// arm hangs at 0, so the angle less a quarter turn, mirrored for a player facing left.
        /// </summary>
        private static float Arm(Player player, float angle) => (player.direction == 1 ? angle : MathHelper.Pi - angle) - MathHelper.PiOver2;

        // ---- every tick --------------------------------------------------------------------------------------------------

        public override void SafeAI()
        {
            Player owner = Owner;
            bool mine = Projectile.owner == Main.myPlayer;
            Projectile.timeLeft = 2;

            if (mine)
            {
                Aim(owner);
                Decide(owner);
            }

            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            int tick = (int)Held;
            int since = tick - (int)Round;
            bool flying = Now == Work.Throw && since >= 0 && since < ViolentCrimes.ThrowTicks;

            // What has begun since this machine last looked. (Its holder's sees a laser or a throw on the tick it begins;
            // another machine is told a tick or a few later, and sees it then.)
            bool turned = Now != seenWork;
            bool fired = (Now == Work.Laser || Now == Work.Throw) && Round != seenRound && since >= 0 && since <= 6;
            Work was = seenWork;
            seenWork = Now;
            seenRound = Now == Work.Laser || Now == Work.Throw ? Round : -1f;

            if (fired && Now == Work.Laser)
                kick = 1f;

            Vector2 pointing = Pose(owner, aim, flying);

            if (flying)
                owner.GetModPlayer<ViolentCrimesPlayer>().Thrust(throwWay * ViolentCrimes.ThrowSpeed(since), since == 0);

            if (mine)
                Shoot(owner, pointing, tick, since, flying, fired);

            if (!Main.dedServ)
                Seen(owner, pointing, tick, since, flying, turned, was, fired);

            // The hold counts on (every machine, between what it is told).
            if (Now != Work.None)
                Held++;
        }

        /// <summary>
        /// Its holder's machine: the cursor is the aim. Kept as it is every tick, and sent when it has turned enough to be
        /// seen, or has been turning a while (the Meat Hook's way).
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

        /// <summary>Its holder's machine: the buttons, and from them what the gun is doing this tick.</summary>
        private void Decide(Player owner)
        {
            // What takes the controls away takes the gun's buttons with them.
            bool free = !(owner.mouseInterface || Main.mapFullscreen || CaptureManager.Instance.Active || owner.selectedItem == 58
                || owner.noItems || owner.CCed || !PlayerLoader.CanUseItem(owner, owner.HeldItem));

            // The right button is the gun's only from a press that began free of everything else.
            if (!owner.controlUseTile)
                right = 1;
            else if (right == 1)
                right = free && !owner.tileInteractionHappened && !Main.HoveringOverAnNPC && !Main.SmartInteractShowingGenuine ? 2 : 3;

            bool leftDown = owner.controlUseItem && free;
            bool rightDown = owner.controlUseTile && right == 2 && free;

            if (dry && !owner.controlUseItem && !owner.controlUseTile)
                dry = false;

            int tick = (int)Held;

            // A throw under way is finished, whatever the buttons do.
            bool committed = Now == Work.Throw && tick - (int)Round < ViolentCrimes.ThrowTicks;

            if (button == 1 && !leftDown && !committed)
                button = 0;
            else if (button == 2 && !rightDown)
                button = 0;

            if (button == 0 && !dry)
            {
                button = rightDown ? 2 : leftDown ? 1 : 0;

                if (button != 0)
                {
                    Held = 0f;
                    Round = 0f;
                    tick = 0;
                }
            }

            if (button == 0 || dry)
            {
                Rest();
                return;
            }

            // A Gel as it begins, and every half second it works.
            if (tick % ViolentCrimes.GelEvery == 0 && !committed && !owner.PickAmmo(owner.HeldItem, out _, out _, out _, out _, out _))
            {
                dry = true;
                button = 0;
                Rest();

                if (!Main.dedServ)
                    SoundEngine.PlaySound(Twins.Lighter with { Volume = 0.5f, Pitch = -0.5f }, owner.Center);

                return;
            }

            bool second = tick >= ViolentCrimes.SwitchTicks;
            Work now = button == 2 ? (second ? Work.Laser : Work.Flame) : (second ? Work.Throw : Work.Gas);

            if (now != Now)
            {
                Now = now;
                Projectile.netUpdate = true;
            }

            // Its laser, its throw: on the tick it turns over, and every sixty after.
            if (second && (tick - ViolentCrimes.SwitchTicks) % ViolentCrimes.Every == 0)
            {
                Round = tick;

                if (now == Work.Throw)
                {
                    Vector2 to = Main.MouseWorld - owner.MountedCenter;
                    throwWay = to.LengthSquared() > 16f ? Vector2.Normalize(to) : Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
                }

                Projectile.netUpdate = true;
            }
        }

        private void Rest()
        {
            if (Now == Work.None)
                return;

            Now = Work.None;
            Held = 0f;
            Round = 0f;
            Projectile.netUpdate = true;
        }

        /// <summary>
        /// The gun and its holder posed (every machine): pointed along the aim, or back along a throw's way; its holder
        /// turned with it; a laser's kick; both hands on it. The way it points, in the world.
        /// </summary>
        private Vector2 Pose(Player owner, Vector2 aim, bool flying)
        {
            float to = (flying ? -throwWay : aim).ToRotation();
            shown = posed ? shown.AngleLerp(to, 0.45f) : to;
            posed = true;

            Vector2 pointing = shown.ToRotationVector2();
            int facing = pointing.X > 0.02f ? 1 : pointing.X < -0.02f ? -1 : owner.direction;
            owner.ChangeDir(facing);

            kick *= 0.82f;

            float tremble = Now switch
            {
                Work.Flame => 0.012f * MathF.Sin(Main.GlobalTimeWrappedHourly * 47f),
                Work.Gas => 0.008f * MathF.Sin(Main.GlobalTimeWrappedHourly * 61f),
                Work.Throw when flying => 0.03f * MathF.Sin(Main.GlobalTimeWrappedHourly * 73f),
                _ => 0f,
            };

            float level = MeatHookHoldOut.Upright(owner, pointing);

            // Upright, up is the lesser angle: the kick raises the mouth.
            float angle = level - kick * KickAngle + tremble;

            Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, level)
                - level.ToRotationVector2() * (kick * KickBack + (flying ? 3f : 0f));

            float reach = VisusGrip.BackArmOnto(Player.CompositeArmStretchAmount.Full, hand, angle);

            owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Arm(owner, level));
            owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Arm(owner, reach));

            owner.GetModPlayer<ViolentCrimesPlayer>().Hold(angle, hand);

            Projectile.Center = World(owner, Middle);
            Projectile.rotation = VisusGrip.ToWorldAngle(owner, angle);
            return pointing;
        }

        /// <summary>Its holder's machine: what the gun throws this tick.</summary>
        private void Shoot(Player owner, Vector2 pointing, int tick, int since, bool flying, bool fired)
        {
            if (Now == Work.None)
                return;

            Item gun = owner.HeldItem;
            int damage = owner.GetWeaponDamage(gun);
            float knockback = owner.GetWeaponKnockback(gun, gun.knockBack);
            IEntitySource source = owner.GetSource_ItemUse(gun);

            // From its mouth; from the middle of its holder if a wall is between.
            Vector2 mouth = World(owner, Muzzle);
            Vector2 middle = VisusGrip.Centre(owner);

            if (!Collision.CanHit(middle, 0, 0, mouth, 0, 0))
                mouth = middle;

            switch (Now)
            {
                case Work.Flame:
                    if (tick % ViolentCrimes.FlameEvery == 0)
                        Projectile.NewProjectile(source, mouth, pointing * ViolentCrimes.FlameSpeed, ModContent.ProjectileType<ViolentCrimesFlame>(), damage, knockback, owner.whoAmI);

                    break;

                case Work.Laser:
                    if (fired)
                        Projectile.NewProjectile(source, mouth, pointing, ModContent.ProjectileType<ViolentCrimesLaser>(), (int)(damage * ViolentCrimes.LaserDamage), 6f, owner.whoAmI);

                    break;

                case Work.Gas:
                    if (tick % ViolentCrimes.GasEvery == 0)
                    {
                        MakeRoom(owner);
                        Projectile.NewProjectile(source, mouth, pointing.RotatedByRandom(0.2f) * Main.rand.NextFloat(8f, 13f) + owner.velocity * 0.3f,
                            ModContent.ProjectileType<ViolentCrimesGas>(), (int)(damage * ViolentCrimes.GasDamage), 5f, owner.whoAmI);
                    }

                    break;

                case Work.Throw:
                    // Chlorine out of its mouth, behind its holder as they fly.
                    if (flying && since % 3 == 0)
                        Projectile.NewProjectile(source, mouth, pointing.RotatedByRandom(0.3f) * Main.rand.NextFloat(1.5f, 4.5f),
                            ModContent.ProjectileType<ViolentCrimesChlorine>(), Math.Max(1, (int)(damage * ViolentCrimes.ChlorineDamage)), 0f, owner.whoAmI);

                    break;
            }
        }

        /// <summary>No more clouds of its gas than <see cref="ViolentCrimes.MostGas"/> to a holder: the oldest is let fade for the next.</summary>
        private static void MakeRoom(Player owner)
        {
            int type = ModContent.ProjectileType<ViolentCrimesGas>();
            int count = 0;
            Projectile oldest = null;

            foreach (Projectile cloud in Main.ActiveProjectiles)
            {
                if (cloud.type != type || cloud.owner != owner.whoAmI || cloud.ModProjectile is not ViolentCrimesGas gas || !gas.Unlit || cloud.timeLeft <= 40)
                    continue;

                count++;

                if (oldest == null || cloud.timeLeft < oldest.timeLeft)
                    oldest = cloud;
            }

            if (count >= ViolentCrimes.MostGas && oldest != null)
            {
                oldest.timeLeft = 40;
                oldest.netUpdate = true;
            }
        }

        // ---- seen and heard (this machine's) ---------------------------------------------------------------------------------

        private void Seen(Player owner, Vector2 pointing, int tick, int since, bool flying, bool turned, Work was, bool fired)
        {
            Vector2 mouth = World(owner, Muzzle);
            Vector2 pilot = World(owner, Pilot);
            ViolentCrimesPlayer pose = owner.GetModPlayer<ViolentCrimesPlayer>();
            bool near = PADFire.Near(mouth, 200f);

            // Its pilot light, always.
            if (near && Main.GameUpdateCount % 3 == 0)
                PADFire.Flame(pilot, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.5f) + owner.velocity * 0.4f, Main.rand.NextFloat(7f, 10f), Main.rand.Next(8, 13), FirePalette.Fire, 0.04f, 0.6f);

            Lighting.AddLight(pilot, 0.3f, 0.17f, 0.05f);

            if (turned)
                Turned(owner, mouth, pointing, was);

            HissOn(mouth);

            float build = Now == Work.Flame || Now == Work.Gas ? MathHelper.Clamp((tick - ViolentCrimes.SwitchTicks * 0.5f) / (ViolentCrimes.SwitchTicks * 0.5f), 0f, 1f) : 0f;
            pose.Glow = Now switch
            {
                Work.None => 0f,
                Work.Flame or Work.Gas => 0.25f + 0.75f * build * (0.6f + 0.4f * MathF.Sin(tick * (0.12f + 0.3f * build))),
                Work.Laser => 0.5f + 0.5f * Math.Min(1f, since / (float)ViolentCrimes.Every),
                _ => flying ? 1f : 0.5f,
            };
            pose.Flash = Math.Max(0f, pose.Flash - 0.07f);

            if (!near)
                return;

            switch (Now)
            {
                case Work.Flame:
                    // The root of its stream: quick small flames straight out of its mouth, so it never gaps there.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 way = pointing.RotatedByRandom(0.05f);
                        PADFire.Flame(mouth + way * Main.rand.NextFloat(14f), way * Main.rand.NextFloat(15f, 24f) + owner.velocity * 0.5f, Main.rand.NextFloat(14f, 25f), Main.rand.Next(7, 12),
                            FirePalette.Fire, 0f, 0.6f, 0, Main.rand.NextBool());
                    }

                    if (tick % 30 == 0)
                        SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.8f, PitchVariance = 0.1f, MaxInstances = 3 }, mouth);

                    // It runs hot as its laser nears.
                    if (Main.rand.NextFloat() < build * 0.5f)
                        PADFire.Spark(mouth, pointing.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 6f) + owner.velocity, Main.rand.Next(12, 24));

                    Lighting.AddLight(mouth, 1.1f, 0.55f, 0.14f);
                    break;

                case Work.Gas:
                    if (tick % 2 == 0)
                        PADFire.Gas(mouth + pointing * Main.rand.NextFloat(10f), pointing.RotatedByRandom(0.25f) * Main.rand.NextFloat(3f, 7f) + owner.velocity * 0.5f, Main.rand.NextFloat(20f, 34f), Main.rand.Next(20, 35), 0.6f);

                    break;

                case Work.Laser:
                    {
                        // Its mouth heats for the next.
                        float charge = MathHelper.Clamp((since - 20f) / (ViolentCrimes.Every - 20f), 0f, 1f);

                        if (charge > 0f)
                        {
                            PADFire.Burn(mouth, 4f + 8f * charge, 0.4f + 1.3f * charge, FirePalette.Fire, smokes: false);
                            Lighting.AddLight(mouth, 1.1f * charge, 0.55f * charge, 0.14f * charge);
                        }

                        if (fired)
                        {
                            SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { Volume = 0.75f, Pitch = 0.25f, MaxInstances = 2 }, mouth);
                            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = -0.3f, MaxInstances = 2 }, mouth);
                            PADFire.Puff(mouth, pointing, 50f, FirePalette.Fire, 1f, 0.45f);

                            if (Projectile.owner == Main.myPlayer)
                                owner.PIS().BumpScreenShake(4f, 8);
                        }

                        break;
                    }

                case Work.Throw:
                    if (fired)
                    {
                        SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 2 }, mouth);
                        SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.7f, Pitch = 0.2f, MaxInstances = 2 }, mouth);

                        // The jet as it opens: thrown out the way the mouth points.
                        for (int i = 0; i < 10; i++)
                            PADFire.Gas(mouth, pointing.RotatedByRandom(0.45f) * Main.rand.NextFloat(5f, 14f), Main.rand.NextFloat(26f, 46f), Main.rand.Next(24, 44), 0.8f);

                        if (Projectile.owner == Main.myPlayer)
                            owner.PIS().BumpScreenShake(5f, 10);
                    }

                    if (flying)
                    {
                        for (int i = 0; i < 2; i++)
                            PADFire.Gas(mouth, pointing.RotatedByRandom(0.2f) * Main.rand.NextFloat(6f, 12f), Main.rand.NextFloat(22f, 38f), Main.rand.Next(18, 32), 0.7f);

                        // The air it goes through.
                        if (Main.rand.NextBool(2))
                            PADParticleHandler.SpawnParticle(new WindStreak(owner.Center + Main.rand.NextVector2Circular(30f, 30f), -throwWay * Main.rand.NextFloat(4f, 9f), Main.rand.NextFloat(36f, 80f),
                                Main.rand.NextFloat(1.5f, 2.5f), Main.rand.Next(10, 18)));

                        Lighting.AddLight(mouth, 0.2f, 0.6f, 0.2f);
                    }
                    else if (tick % 5 == 0)
                    {
                        // Between throws: it leaks.
                        PADFire.Gas(mouth, pointing * Main.rand.NextFloat(0.5f, 2f) + new Vector2(0f, -0.3f), Main.rand.NextFloat(14f, 22f), Main.rand.Next(18, 30), 0.5f);
                    }

                    break;
            }
        }

        /// <summary>What it is doing has changed: what is seen and heard as it does.</summary>
        private void Turned(Player owner, Vector2 mouth, Vector2 pointing, Work was)
        {
            ViolentCrimesPlayer pose = owner.GetModPlayer<ViolentCrimesPlayer>();

            switch (Now)
            {
                case Work.Gas:
                    hiss = SoundEngine.PlaySound(Hiss, mouth);
                    hissFading = false;
                    break;

                case Work.Laser when was == Work.Flame:
                    // It turns over: a click, a spit of sparks, the gun flashing.
                    SoundEngine.PlaySound(Twins.Lighter with { Volume = 0.9f, Pitch = 0.3f }, mouth);
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = 0.4f }, mouth);
                    pose.Flash = 1f;

                    for (int i = 0; i < 8; i++)
                        PADFire.Spark(mouth, pointing.RotatedByRandom(0.8f) * Main.rand.NextFloat(3f, 9f), Main.rand.Next(14, 28));

                    break;

                case Work.Throw when was == Work.Gas:
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.2f }, mouth);
                    pose.Flash = 1f;
                    break;
            }

            if (was == Work.Gas && Now != Work.Gas)
                hissFading = true;
        }

        /// <summary>Its hiss follows its mouth, and dies away once it stops spraying.</summary>
        private void HissOn(Vector2 mouth)
        {
            if (!hiss.IsValid)
                return;

            if (!SoundEngine.TryGetActiveSound(hiss, out ActiveSound sound))
            {
                hiss = SlotId.Invalid;
                hissFading = false;
                return;
            }

            sound.Position = mouth;

            if (!hissFading)
                return;

            sound.Volume -= 0.08f;

            if (sound.Volume <= 0.02f)
            {
                sound.Stop();
                hiss = SlotId.Invalid;
                hissFading = false;
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Put away: its hiss with it.
            if (!Main.dedServ && hiss.IsValid && SoundEngine.TryGetActiveSound(hiss, out ActiveSound sound))
                sound.Stop();

            hiss = SlotId.Invalid;
            Owner.GetModPlayer<ViolentCrimesPlayer>().Drop();
        }
    }

    /// <summary>
    /// Violent Crimes' holder: how the gun is held this tick (told by the gun in the hands on every machine, drawn by
    /// ViolentCrimesLayer), and the throw (the gun says each tick how fast and which way; they are moved here, just
    /// before the game moves them, on every machine, as the Meat Hook's drag is).
    /// </summary>
    public class ViolentCrimesPlayer : ModPlayer
    {
        // ---- the pose -------------------------------------------------------------------------------------------------------

        private int heldFor;

        /// <summary>The gun is in the hands (it said so this tick or the last: whichever of it and what asks is run first).</summary>
        public bool Held => heldFor > 0;

        /// <summary>The gun's angle, upright.</summary>
        public float Angle;

        /// <summary>The front hand, where the gun's grip is, upright.</summary>
        public Vector2 Hand;

        /// <summary>How brightly its lights burn over their own (0 to 1), and the flash as it turns over (1, dying).</summary>
        public float Glow, Flash;

        /// <summary>The gun in the hands says how it is held this tick.</summary>
        public void Hold(float angle, Vector2 hand)
        {
            heldFor = 2;
            Angle = angle;
            Hand = hand;
        }

        /// <summary>Put away: not drawn again, not even for the tick it goes on.</summary>
        public void Drop()
        {
            heldFor = 0;
            Glow = 0f;
            Flash = 0f;
        }

        public override void ResetEffects()
        {
            if (heldFor > 0)
                heldFor--;
        }

        // ---- the throw ------------------------------------------------------------------------------------------------------

        private Vector2 thrust;
        private int thrustFor;
        private bool thrustBegins;

        /// <summary>Thrown this way, this fast, for the tick to come.</summary>
        public void Thrust(Vector2 velocity, bool begins)
        {
            thrust = velocity;
            thrustFor = 1;
            thrustBegins |= begins;
        }

        public override void UpdateDead()
        {
            heldFor = 0;
            thrustFor = 0;
            thrustBegins = false;
        }

        public override void PreUpdateMovement()
        {
            if (thrustFor <= 0)
                return;

            thrustFor--;

            // As the game does for a grapple, on the machine whose player it is: off what is ridden, and off every hook.
            if (thrustBegins && Player.whoAmI == Main.myPlayer)
            {
                if (Player.mount.Active)
                    Player.mount.Dismount(Player);

                Player.RemoveAllGrapplingHooks();
            }

            thrustBegins = false;

            // No fall is counted in a throw.
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.velocity = thrust;
            Player.pulley = false;
            Player.armorEffectDrawShadow = true;

            // Down through platforms, as a grapple takes its player.
            if (Player.velocity.Y > 0f)
                Player.GoingDownWithGrapple = true;
        }
    }

    /// <summary>
    /// Draws Violent Crimes in the player's hands, where vanilla draws a held item behind the front arm (the Meat Hook's
    /// layer's way: placed from where the player is being drawn, so it rides the walk with the arms and is mirrored with
    /// them). Over it, its lights (the mouth, the pilot, the two gauges, the lamp: ViolentCrimesGlow.png) unlit by the
    /// place, brighter as it works; and a flash of the whole gun as it turns over to its laser or its throw.
    /// </summary>
    public class ViolentCrimesLayer : PlayerDrawLayer
    {
        private static Asset<Texture2D> gun, lights;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            gun = ModContent.Request<Texture2D>(ViolentCrimesHoldout.Sprite);
            lights = ModContent.Request<Texture2D>(ViolentCrimesHoldout.GlowSprite);
        }

        public override void Unload()
        {
            gun = null;
            lights = null;
        }

        // Vanilla's own slot for an item held behind the front arm (the Meat Hook's too).
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<ViolentCrimesPlayer>().Held;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (gun == null || !gun.IsLoaded)
                return;

            Player player = drawInfo.drawPlayer;
            ViolentCrimesPlayer pose = player.GetModPlayer<ViolentCrimesPlayer>();
            Texture2D texture = gun.Value;

            // The middle the arms are drawn from, where the player is being drawn.
            Vector2 centre = drawInfo.Position + (player.MountedCenter - player.position);
            Vector2 hand = centre + new Vector2(pose.Hand.X * player.direction, pose.Hand.Y * player.gravDir) + MeatHookHoldOut.Walk(player);
            Vector2 position = (hand - Main.screenPosition).Floor();

            // Mirrored top to bottom when it points left (and back again under reversed gravity): the grip's texel goes with the mirror.
            bool flip = (player.direction < 0) != (player.gravDir < 0f);
            Vector2 grip = ViolentCrimesHoldout.Grip;
            Vector2 origin = flip ? new Vector2(grip.X, texture.Height - grip.Y) : grip;
            SpriteEffects effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float rotation = VisusGrip.ToWorldAngle(player, pose.Angle);

            Color light = Lighting.GetColor(ViolentCrimesHoldout.World(player, ViolentCrimesHoldout.Middle).ToTileCoordinates());
            drawInfo.DrawDataCache.Add(new DrawData(texture, position, null, light, rotation, origin, 1f, effects));

            // As it turns over: the whole gun, added on as light, dying.
            if (pose.Flash > 0.02f)
            {
                int k = (int)(150f * pose.Flash);
                drawInfo.DrawDataCache.Add(new DrawData(texture, position, null, new Color(k, k, k, 0), rotation, origin, 1f, effects));
            }

            if (lights == null || !lights.IsLoaded)
                return;

            Texture2D lit = lights.Value;

            // Its lights: their own colour whatever the place's light; and, as it works, a glow of them about them.
            drawInfo.DrawDataCache.Add(new DrawData(lit, position, null, Color.White, rotation, origin, 1f, effects));

            if (pose.Glow > 0.02f)
            {
                int k = (int)(110f * pose.Glow);
                Color glow = new(k, k, k, 0);

                for (int i = 0; i < 4; i++)
                {
                    Vector2 off = new(i < 2 ? -2f : 2f, i % 2 == 0 ? -2f : 2f);
                    drawInfo.DrawDataCache.Add(new DrawData(lit, position + off, null, glow, rotation, origin, 1f, effects));
                }
            }
        }
    }
}
