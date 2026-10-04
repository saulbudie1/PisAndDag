using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// The Iron Flow: a hooked sword with a crescent for a guard, made at an anvil from 12 Iron
    /// or Lead Bars. 25 damage.
    ///
    /// It is in the hand for as long as it is the item held, hanging by the leg
    /// (IronFlowHoldout, which is the weapon: the item itself hits nothing and is never drawn
    /// in use). Left click cuts with it, for as long as the button is held: down, up, then all
    /// the way round, and each carries its user forward: a block, half a block, a block and a
    /// half. The first two take 24 ticks each, the third 36. Right click parries (Parry,
    /// ParryPlayer): a fresh press every time, 42 ticks whatever the wielder's speed, of which
    /// the 7th to the 14th turn aside what comes at their front: a solid shot breaks; an enemy
    /// takes a cut's damage and is pushed back, every second or third parry of it staggers it,
    /// and the first hit on a staggered one does three times its damage (Parry has all of it).
    ///
    /// One thing at a time: nothing starts while the sword is in the middle of a cut or a
    /// parry, and the item is in use for exactly as long as either takes, so it cannot be put
    /// away to get out of one.
    /// </summary>
    public class IronFlow : ModItem
    {
        public const int Damage = 25;
        public const int UseTicks = 24;
        public const float Knockback = 5f;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
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

            Item.shoot = ModContent.ProjectileType<IronFlowHoldout>();
            Item.shootSpeed = 1f;

            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 20);
        }

        public override bool AltFunctionUse(Player player) => true;

        /// <summary>Not until the sword is in the hand, and not while it is in the middle of something.</summary>
        public override bool CanUseItem(Player player)
        {
            IronFlowHoldout sword = IronFlowHoldout.Of(player);
            return sword != null && sword.Idle;
        }

        // The item is in use for exactly as long as what the sword is about to do takes: a cut,
        // the item's own use time at the wielder's speed; the third cut, the circle, half as
        // long again; the parry, its own ticks whatever that speed.
        //
        // Vanilla is about to divide both the use time and the animation by the wielder's speed
        // (CombinedHooks.TotalUseTimeMultiplier, TotalUseAnimationMultiplier: the animation by
        // way of the use time cut to a whole number of ticks), so each is multiplied here by
        // what makes it come out at the ticks meant. The half is so that cutting to a whole
        // number lands on that number. altFunctionUse is already 2 here on a right click
        // (ItemCheck_Inner sets it before it asks).

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

        /// <summary>Ticks one of the first two cuts takes at this speed: vanilla's own sum for the item's use time.</summary>
        private int Swing(float speed) => Math.Max(1, (int)(Item.useTime * (1f / speed)));

        /// <summary>Ticks the use about to start lasts.</summary>
        private int Lasts(Player player, float speed)
        {
            if (player.altFunctionUse == 2)
                return ParryTiming.CurvedSword.Total;

            IronFlowHoldout sword = IronFlowHoldout.Of(player);
            bool circle = sword != null && sword.Idle && sword.NextCut == FlowMove.Circle;

            return circle ? Math.Max(1, (int)(Swing(speed) * IronFlowMoves.CircleLength)) : Swing(speed);
        }

        /// <summary>
        /// Nothing is shot: the sword in the hand is told to cut, for as long as the item is in
        /// use (melee speed and all), or to parry. Facing the cursor, as vanilla has already
        /// turned the player. A second call inside one use (the use time running out a tick
        /// before the animation does) finds the sword busy and does nothing.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            IronFlowHoldout sword = IronFlowHoldout.Of(player);

            if (sword == null || !sword.Idle)
                return false;

            int facing = velocity.X < 0f ? -1 : velocity.X > 0f ? 1 : player.direction;

            if (player.altFunctionUse == 2)
                sword.StartParry(facing);
            else
                sword.StartCut(Math.Max(1, player.itemAnimationMax), facing);

            return false;
        }

        //public override void AddRecipes()
        //{
        //    CreateRecipe()
        //        .AddRecipeGroup(RecipeGroupID.IronBar, 12)
        //        .AddTile(TileID.Anvils)
        //        .Register();
        //}
    }
}
