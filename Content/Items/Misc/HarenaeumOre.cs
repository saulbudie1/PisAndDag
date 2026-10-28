using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// Harenaeum Ore, the item: what a vein of it gives (Tiles.HarenaeumOre), smelted into Harenaeum Bars (three
    /// to a bar, with a block of sand). Placeable, as ores are. Placeholder picture (PlaceholderTextures.md).
    /// </summary>
    public class HarenaeumOre : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 100;

            // Among the materials, with the game's own ores of its rank.
            ItemID.Sets.SortingPriorityMaterials[Type] = 58;
        }

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.HarenaeumOre>());
            Item.width = 16;
            Item.height = 16;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(silver: 15);
        }
    }

    /// <summary>
    /// Harenaeum Bar: three Harenaeum Ore and a block of sand, at a Furnace. What the desert set is made of
    /// (Harenaeum). Not placeable. Placeholder picture (PlaceholderTextures.md).
    /// </summary>
    public class HarenaeumBar : ModItem
    {
        /// <summary>Ore to a bar.</summary>
        public const int Ore = 3;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 25;
            ItemID.Sets.SortingPriorityMaterials[Type] = 59;
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(silver: 50);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumOre>(Ore)
                .AddIngredient(ItemID.SandBlock)
                .AddTile(TileID.Furnaces)
                .Register();
        }
    }
}
