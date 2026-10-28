using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{
    /// <summary>
    /// The Guide's family's ledger, in the Blood Shrine's chest (PADStructureSystem.FillShrineChest; in a
    /// world made before it, GuideQuest puts it there when the world loads). Brought to him, it ends his
    /// quest (GuideQuest): the off hand opens and the Voodoo Demons come.
    ///
    /// A lore item, so LoreLock keeps it on the player. Lore Bible §XXVII: the line kept by the learned
    /// houses, and the Guide Voodoo Doll a likeness of the key. It says neither: a household's long count,
    /// many hands and one name, and a face at the end that is not finished.
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class WeatheredLedger : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Blue;
            Item.useStyle = ItemUseStyleID.None;

            Item.PAD().Lore = true;
            Item.PAD().LoreText =
                "A household's accounts, kept longer than the town has stood.\r\n" +
                "Every page in a different hand, the same name at the foot.\r\n" +
                "The last page is a face, and it is not finished.";
        }
    }
}
