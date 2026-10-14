using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Boots, by request: "Boots equipable, doesnt have any affect but wear on shoes (reuse
    /// wearing texture of hermes boots)". Worn, they do nothing at all; on the feet they look as
    /// the game's Hermes Boots do (the Hermes Boots' own shoe picture, ArmorIDs.Shoe.HermesBoots),
    /// and the slot's eye hides them as it hides any worn shoe. What they are for: three of the
    /// game's boots are made from a pair at a Tinkerer's Workshop (PADRecipes).
    ///
    /// Mine: White rarity, and a fifth of the Hermes Boots' value. Their picture in the inventory
    /// is a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class Boots : ModItem
    {
        /// <summary>Beside the mod's other accessories in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 24;
            Item.accessory = true;
            Item.shoeSlot = ArmorIDs.Shoe.HermesBoots;
            Item.rare = ItemRarityID.White;
            Item.value = Item.sellPrice(silver: 20);
        }

        // Recipes stay commented out for now. As asked: 10 Silk, 5 Leather and 2 Iron Bars (the game's
        // "Any Iron Bar", so Lead Bars do too). No place was named, so anywhere:

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ItemID.Leather, 5)
                .AddRecipeGroup(RecipeGroupID.IronBar, 2)
                .Register();
        }
    }
}
