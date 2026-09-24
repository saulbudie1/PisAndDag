using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// Places the Makeshift Altar (Content/Tiles/MakeshiftAltar), the crafting station for
    /// incantations and spells. Made at a Work Bench from 15 Bone, 10 of any wood and a
    /// Book. The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class MakeshiftAltar : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            // Sets useStyle, useTime, useAnimation, autoReuse, consumable and createTile.
            Item.DefaultToPlaceableTile(ModContent.TileType<Content.Tiles.MakeshiftAltar>());

            Item.width = 46;
            Item.height = 34;
            Item.value = Item.sellPrice(silver: 20);
            Item.rare = ItemRarityID.Blue;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Bone, 15)
                .AddRecipeGroup(RecipeGroupID.Wood, 10)
                .AddIngredient(ItemID.Book)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
