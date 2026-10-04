using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// Places the door that leads to a subworld (Content/Tiles/SubworldDoor). Once it stands
    /// it stays: the placed door cannot be broken, and gives nothing back. No recipe yet:
    /// /subworld door gives one. The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class SubworldDoor : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            // Sets useStyle, useTime, useAnimation, autoReuse, consumable and createTile.
            Item.DefaultToPlaceableTile(ModContent.TileType<Content.Tiles.SubworldDoor>());

            Item.width = 22;
            Item.height = 34;
            Item.value = 0;
            Item.rare = ItemRarityID.Blue;
        }
    }
}
