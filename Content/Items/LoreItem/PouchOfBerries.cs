using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{
    /// <summary>
    /// The pouch from the errand (MorsErrand). Off a skeleton in the caves, because
    /// whoever was carrying it up there never finished carrying it.
    ///
    /// It does nothing and it is worth nothing. It is a lore item, so LoreLock keeps
    /// it on the player, which is the joke: the one person entitled to take it back
    /// will not have it.
    /// </summary>
    public class PouchOfBerries : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Blue;
            Item.useStyle = ItemUseStyleID.None;

            Item.PAD().Lore = true;
            Item.PAD().LoreText =
                "Carried for someone who was not going to finish the walk.\r\n" +
                "The hand that packed it did not come back for it.\r\n" +
                "The berries are still good.";
        }
    }
}
