using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{
    /// <summary>
    /// What Dona Quinn leaves if she is finished rather than spared (DonaQuinn's loot). One
    /// defence, worn as an accessory, and a lore item: LoreLock keeps it on the player.
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class LocketOfABookCrazedGirl : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 30;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Blue;
            Item.accessory = true;
            Item.defense = 1;

            Item.PAD().Lore = true;
            Item.PAD().LoreText =
                "A rusted gold plated locket, with paper folded small inside.\n" +
                "\"Dear my lovely daughter, I know you will be a shining knight one day.\"\n" +
                "It was read until the creases went soft.";
        }
    }
}
