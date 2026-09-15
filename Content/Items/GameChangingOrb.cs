using System.Collections.Generic;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items
{
    public class GameChangingOrb : ModItem
    {
        public override void SetDefaults()
        {
            // Sets useStyle, useTime, useAnimation, autoReuse, consumable and createTile
            // in one call — the standard way to make an item place a tile.
            Item.DefaultToPlaceableTile(ModContent.TileType<Game_Changing_Orb>());

            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Pink;

            // Worthless on purpose: vanilla will not buy a worthless item, which closes
            // the one hole LoreLock cannot undo (dragging it onto a shop slot).
            Item.value = 0;

            // Lore Bible §XXVIII. What it is, who held it, who found the other use, and
            // the gap — what answered is the one thing nobody wrote down.
            Item.PAD().Lore = true;
            Item.PAD().LoreText =
                "The old kingdom kept it under glass and called it Grace.\n" +
                "Cut by the masons to hold a blessing.\n" +
                "Those who judge gods found the other thing it could do.\n" +
                "No one wrote down what answered.";
        }

        /// <summary>
        /// Once the world is in Legends mode the orb is spent — there is nothing left for
        /// it to change, so it cannot be placed again. Returning false here stops the
        /// swing outright, which also means the item is never consumed.
        /// </summary>
        public override bool CanUseItem(Player player)
        {
            return !PADWorld.Legends;
        }

        /// <summary>
        /// An item that silently does nothing when clicked reads as a bug, so say why.
        /// </summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (PADWorld.Legends)
            {
                tooltips.Add(new TooltipLine(Mod, "LegendsSpent",
                    "This world's essence has already been stirred"));
            }
        }

        // No recipe yet — say what it should be made from and I'll add it.
        //public override void AddRecipes()
        //{
        //    CreateRecipe()
        //        .AddIngredient(ItemID.SoulofNight, 10)
        //        .AddTile(TileID.Anvils)
        //        .Register();
        //}
    }
}
