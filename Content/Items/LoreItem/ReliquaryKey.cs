using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{
    /// <summary>
    /// The key to the deep of the Kingdom of Grace. Carried to the gate of the lodge over the
    /// stair under the lowest street, or to the door of the chamber at the foot of the crack,
    /// it opens both for good in that world (GraceSeal, PADWorld.GraceUnsealed). It is not
    /// used up: it is a lore item, and LoreLock keeps it on the player.
    ///
    /// Nothing gives it out yet. The texture is a placeholder.
    /// </summary>
    public class ReliquaryKey : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 18;
            Item.height = 30;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Yellow;
            Item.useStyle = ItemUseStyleID.None;

            Item.PAD().Lore = true;
            Item.PAD().LoreText =
                "It turns the lock on the stair under the lowest street.\n" +
                "It was kept on a ring with nothing else on it.\n" +
                "The last door it opens is not on the stair.";
        }
    }
}
