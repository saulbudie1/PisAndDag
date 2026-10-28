using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee.Fists
{
    /// <summary>How a blow travels: straight out at the cursor, or up from under, sweeping.</summary>
    public enum FistArc
    {
        Straight,
        Rising,
    }

    /// <summary>
    /// One blow of a fist weapon's combo (FistWeapon.Combo). Times are the blow's whole length, before attack speed;
    /// the strike is where in it the fist arrives, 0 to 1.
    /// </summary>
    public readonly struct FistMove
    {
        public readonly FistArc Arc;

        /// <summary>The whole blow, wind-up to recovery, in ticks at normal speed.</summary>
        public readonly int Ticks;

        /// <summary>Where in the blow the fist arrives, 0 to 1. It hits from a little before this to a little after.</summary>
        public readonly float Strike;

        /// <summary>Times the weapon's damage and knockback.</summary>
        public readonly float Damage, Knockback;

        /// <summary>Times the weapon's reach.</summary>
        public readonly float Reach;

        /// <summary>
        /// How far the player steps in with it, in pixels, and back out after (FistPunch): walked, from standing on
        /// the ground only; running, jumping or flying, no step at all.
        /// </summary>
        public readonly float Step;

        /// <summary>Thrown with the other arm, the back one.</summary>
        public readonly bool BackArm;

        /// <summary>What it does to one it hits besides the knockback, as if the player faced right: an upward throw for an uppercut. Scaled by how far the NPC can be pushed.</summary>
        public readonly Vector2 Throw;

        public FistMove(FistArc arc, int ticks, float strike, float damage, float knockback, float reach = 1f,
            float step = 0f, bool backArm = false, Vector2 throwing = default)
        {
            Arc = arc;
            Ticks = Math.Max(4, ticks);
            Strike = MathHelper.Clamp(strike, 0.15f, 0.85f);
            Damage = damage;
            Knockback = knockback;
            Reach = reach;
            Step = step;
            BackArm = backArm;
            Throw = throwing;
        }

        /// <summary>The default combo: a punch, and only that.</summary>
        public static readonly FistMove[] Punch = { new FistMove(FistArc.Straight, 16, 0.45f, 1f, 1f, step: 8f) };
    }

    /// <summary>
    /// A fist weapon, by request (Corosion: "create a system like this with range, combos (default js a punch),
    /// and something that doesnt make it damage the player ifi they are too close to the enemy"). The weapon is the
    /// hand: each use throws the next blow of its <see cref="Combo"/> (a punch alone, unless it says otherwise) at the
    /// cursor, the whole arm thrown out to its <see cref="Reach"/> (the forearm past the hand its <see cref="ArmTexture"/>),
    /// as a FistPunch, and the player stepping in with it and back out (asked: "smaller range, maybe to compensate,
    /// make him walk forward and back, (doesnt affect movement when running or flying)"); using it again soon enough
    /// after a blow carries the combo on, and too late starts it over.
    ///
    /// Too close: a fist is used standing in the enemy's face, so while one is being thrown, and for a moment after
    /// it lands, what it is thrown at cannot hurt by touching (FistPlayer.CanBeHitByNPC). Mine, the terms: an enemy
    /// ahead of the player and within the reach, or one the fist has hit in the last half second, is turned away
    /// from them; a boss is not turned away, but touches for half (a fist should not be a way to stand inside the
    /// Eye of Cthulhu). Shots are never turned away.
    ///
    /// A weapon of this kind sets its numbers in <see cref="SetFistDefaults"/>, and may have a right-click of its
    /// own (<see cref="AltUse"/>).
    /// </summary>
    public abstract class FistWeapon : ModItem
    {
        /// <summary>How far a blow carries from the shoulder at full reach, in pixels.</summary>
        public abstract float Reach { get; }

        /// <summary>The blows in order. A punch, unless the weapon says otherwise.</summary>
        public virtual FistMove[] Combo => FistMove.Punch;

        /// <summary>Ticks after a blow ends in which the next carries the combo on.</summary>
        public virtual int ComboWindow => 30;

        /// <summary>What the fist looks like, pointing right, knuckles at its right edge. The item's own picture unless said.</summary>
        public virtual string FistTexture => Texture;

        /// <summary>
        /// The forearm past the hand, pointing right, laid end to end from the hand to the fist as far as the blow
        /// reaches (its height across the arm, its width one length of it). None: a band of <see cref="Reaching"/>.
        /// </summary>
        public virtual string ArmTexture => null;

        /// <summary>The colour the arm shines with as it strikes (and its band, with no picture of its own).</summary>
        public virtual Color Reaching => new Color(255, 236, 200);

        /// <summary>The swing of a blow, played as it is thrown.</summary>
        public virtual SoundStyle Swing(FistMove move, int step) => SoundID.DD2_MonkStaffSwing with { Volume = 0.55f, Pitch = 0.25f, PitchVariance = 0.15f };

        public sealed override void SetDefaults()
        {
            Item.DamageType = DamageClass.Melee;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.useTurn = false;
            Item.channel = false;
            Item.shoot = ModContent.ProjectileType<FistPunch>();
            Item.shootSpeed = 1f;
            Item.useTime = Item.useAnimation = 16;
            Item.UseSound = null;

            SetFistDefaults();
        }

        /// <summary>Size, damage, knockback, rarity, value: what SetDefaults sets for any other item. The fist's own are set already.</summary>
        public abstract void SetFistDefaults();

        /// <summary>
        /// A right-click of the weapon's own (a dash, a guard), taken straight from the mouse on the player's own machine
        /// (FistPlayer), so that it can cut into a blow. Return true if it was used; it is only asked when
        /// <see cref="HasAltUse"/> and <see cref="CanAltUse"/>.
        /// </summary>
        public virtual bool AltUse(Player player, IEntitySource source) => false;

        /// <summary>Whether the weapon has a right-click.</summary>
        public virtual bool HasAltUse => false;

        /// <summary>Whether the right-click can be used now (a cooldown).</summary>
        public virtual bool CanAltUse(Player player) => true;

        /// <summary>One of its blows has landed. On the client that threw it.</summary>
        public virtual void OnFistHit(Player player, NPC target, FistMove move, int step, NPC.HitInfo hit, Vector2 at) { }

        /// <summary>Something to see along a blow, each tick of it, on every client: what it sheds as it goes.</summary>
        public virtual void Shed(Player player, FistMove move, int step, Vector2 hand, Vector2 reach, float extension, bool striking) { }

        /// <summary>One blow at a time: the next waits for this one to be back.</summary>
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<FistPunch>()] == 0;

        /// <summary>Each blow its own length.</summary>
        public override float UseTimeMultiplier(Player player) => Length(player) / (float)Item.useTime;

        public override float UseAnimationMultiplier(Player player) => Length(player) / (float)Item.useAnimation;

        private int Length(Player player)
        {
            FistMove[] combo = Combo;
            return combo[player.GetModPlayer<FistPlayer>().NextStep(this) % combo.Length].Ticks;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            FistPlayer fists = player.GetModPlayer<FistPlayer>();
            FistMove[] combo = Combo;
            int step = fists.NextStep(this) % combo.Length;
            FistMove move = combo[step];

            Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(new Vector2(player.direction, 0f));

            // As long as the item's own use, at the player's melee speed: the punch counts its own ticks.
            int ticks = Math.Max(4, player.itemAnimationMax);

            Projectile.NewProjectile(source, player.MountedCenter, aim, type, (int)Math.Round(damage * move.Damage),
                knockback * move.Knockback, player.whoAmI, step, ticks, Type);

            fists.Threw(this, step, ticks);
            return false;
        }
    }
}
