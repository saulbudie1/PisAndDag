using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Projectiles.Melee;
using PisAndDag.Content.Rarities;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// Foigh's Rust Side Sword: what the player is given when they finish him (Foigh.End,
    /// Finish). His sword's rusted side (one side rusted, one clean: PAD_Lore_Planning 9.1),
    /// as a weapon of its own, by request. 600 damage, used again for as long as the button is
    /// held, and through all defense, every hit of it.
    ///
    /// It is in the hand for as long as it is the item held, on the shoulder
    /// (RustSideHoldout, which is the weapon: the item itself hits nothing and is never drawn
    /// in use). Left click cuts, a chain of five (RustSideMoves): up (a tile forward), down
    /// (half a tile), all the way round (where they stand), a smash into the ground (a tile and
    /// a half) and a thrust (two tiles). Right click leaps, rolls in the air and cuts, letting
    /// a slash go at the cursor; held or pressed again, up to three before the fall, the first
    /// two half as hard again, the third three times (RustSideSlash). Enter held three seconds,
    /// standing: the blood (BloodDrenchedSword), and the chain becomes up, down, the smash, and
    /// a leap at the nearest enemy; the button still held, the sword is driven into the ground
    /// and straight cuts open out of it (RustSideLine).
    ///
    /// One thing at a time: nothing starts while the sword is in the middle of something, and
    /// the item is in use for exactly as long as each cut takes, so it cannot be put away to get
    /// out of one (IronFlow's way).
    ///
    /// Nothing more of its own against what flies, by request ("let it be cuz its owner js a
    /// grounded guy"), and a lore piece instead (asked "a crafting recipe or lore piece?"; the
    /// lore piece: PADWorkFlow §16.59). A Lore Item (PADGlobalItem): always a favourite, taken
    /// back out of the trash and out of a chest (LoreLock), one to a player, a place on the Lore
    /// Codex's shelf, no prefix ever, and its three verses with Shift held: what it is, who held
    /// it, and what it does not say.
    /// </summary>
    public class FoighsRustSideSword : ModItem
    {
        public const int Damage = 600;

        /// <summary>The cut up's and the cut down's ticks; the others are as many times this as RustSideMoves.Length says.</summary>
        public const int UseTicks = 22;

        public const float Knockback = 7f;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // The right button held goes on into the next cut in the air, as the left held goes
            // on along the chain (the game asks for the right button to be let go between two
            // uses of an item's right click otherwise: Player.ItemCheck_ManageRightClickFeatures).
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 64;
            Item.height = 64;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = UseTicks;
            Item.useAnimation = UseTicks;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<RustSideHoldout>();
            Item.shootSpeed = 1f;

            // Blasphemous Crimson, by request ("for now only Rust side has this rarity").
            Item.rare = ModContent.RarityType<BlasphemousCrimson>();

            // A lore piece (PADGlobalItem), and worth nothing: no shop takes what is worth
            // nothing, and a shop is the one way out of the hands that LoreLock cannot undo.
            Item.value = 0;
            Item.PAD().Lore = true;
            Item.PAD().LoreText =
                "Its jagged rusted side was used for a three hundred year long pursuit of vengeance\n" +
                "The pristine side awaits a worthy foe\n" +
                "'The muscle hardens to rust, the sword bleeds with the body'";
        }

        public override bool AltFunctionUse(Player player) => true;

        /// <summary>Not until the sword is in the hand; a cut only with nothing under way; the air only while there is a next one there.</summary>
        public override bool CanUseItem(Player player)
        {
            RustSideHoldout sword = RustSideHoldout.Of(player);

            if (sword == null)
                return false;

            return player.altFunctionUse == 2 ? sword.NextAir != RustMove.Rest : sword.CanCut;
        }

        /// <summary>Every hit of it harder under the blood (by request, "every damage on sword is 1.75x"): the cuts, the slashes, the straight cuts, all of which are this damage.</summary>
        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            if (BloodDrenchedPlayer.Drenched(player))
                damage *= BloodDrenchedSword.DamageMultiplier;
        }

        // The item is in use for exactly as long as what the sword is about to do takes: a cut,
        // its share of the item's use time at the wielder's speed; the leap, its own ticks.
        //
        // Vanilla is about to divide both the use time and the animation by the wielder's speed
        // (CombinedHooks.TotalUseTimeMultiplier, TotalUseAnimationMultiplier: the animation by
        // way of the use time cut to a whole number of ticks), so each is multiplied here by
        // what makes it come out at the ticks meant (IronFlow's sums). altFunctionUse is already
        // 2 here on a right click (ItemCheck_Inner sets it before it asks).

        public override float UseTimeMultiplier(Player player)
        {
            float speed = CombinedHooks.TotalUseSpeedMultiplier(player, Item);

            return (Lasts(player, speed) + 0.5f) * speed / Item.useTime;
        }

        public override float UseAnimationMultiplier(Player player)
        {
            float speed = CombinedHooks.TotalUseSpeedMultiplier(player, Item);

            return (Lasts(player, speed) + 0.5f) / Swing(speed);
        }

        /// <summary>Ticks the cut up takes at this speed: vanilla's own sum for the item's use time.</summary>
        private int Swing(float speed) => Math.Max(1, (int)(Item.useTime * (1f / speed)));

        /// <summary>Ticks the use about to start lasts.</summary>
        private int Lasts(Player player, float speed)
        {
            RustSideHoldout sword = RustSideHoldout.Of(player);
            RustMove next = sword == null ? RustMove.Up : player.altFunctionUse == 2 ? sword.NextAir : sword.NextCut;

            if (next == RustMove.Rest)
                next = player.altFunctionUse == 2 ? RustMove.Air1 : RustMove.Up;

            if (next == RustMove.Leap && sword != null)
            {
                sword.PlanLeap(player, out _, out int ticks, out _, out _);
                return ticks;
            }

            return Math.Max(1, (int)(Swing(speed) * RustSideMoves.Length(next)));
        }

        /// <summary>
        /// Nothing is shot: the sword in the hand is told to cut, for as long as the item is in
        /// use, or to go up into the air at the cursor. A second call inside one use (the use time
        /// running out a tick before the animation does) finds the sword busy and does nothing.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            RustSideHoldout sword = RustSideHoldout.Of(player);

            if (sword == null || !sword.Idle)
                return false;

            int ticks = Math.Max(1, player.itemAnimationMax);

            if (player.altFunctionUse == 2)
                sword.StartAir(ticks, velocity);
            else
                sword.StartCut(ticks, velocity);

            return false;
        }
    }
}
