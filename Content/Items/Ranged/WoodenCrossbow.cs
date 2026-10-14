using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// Wooden Crossbow, by request: "12 damage, 40 use time. Woodx 15, iron bar 1 at work bench, higher
    /// shootspeed than wooden bow."
    ///
    /// Shoots arrows, as a bow does, slower and harder than the game's Wooden Bow (4 damage every 30
    /// ticks, its arrows at 6.1; Item.SetDefaults, 39): 12 damage every 40, the arrows at
    /// <see cref="ShootSpeed"/>. Held out level, as the game's repeaters are.
    ///
    /// Mine: 3 knockback (the Wooden Bow has none), not fired again by holding the button (the Wooden
    /// Bow is not either), White, 2 silver to sell; its sound the game's ballista tower's shot, higher
    /// and softer, a small crossbow's. Its picture is a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class WoodenCrossbow : ModItem
    {
        /// <summary>How fast its arrows leave it: half as fast again as the Wooden Bow's 6.1.</summary>
        public const float ShootSpeed = 9f;

        public override void SetDefaults()
        {
            Item.width = 52;
            Item.height = 32;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = 14;
            Item.knockBack = 5f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 40;
            Item.useAnimation = 40;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.useAmmo = AmmoID.Arrow;
            Item.shootSpeed = ShootSpeed;
            Item.UseSound = SoundID.DD2_BallistaTowerShot with { Volume = 0.55f, Pitch = 0.35f };
            Item.rare = ItemRarityID.White;
            Item.value = Item.sellPrice(silver: 2);
        }

        /// <summary>
        /// The game draws a held gun from 10 pixels in front of the point it holds it at
        /// (Main.DrawPlayerItemPos); this one from 6 behind, so it is held at the trigger and the stock
        /// comes back to the shoulder.
        /// </summary>
        public override Vector2? HoldoutOffset() => new Vector2(-6f, 0f);
         
         public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(RecipeGroupID.Wood, 15)
                .AddRecipeGroup(RecipeGroupID.IronBar)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
