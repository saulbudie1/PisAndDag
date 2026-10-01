using System.Collections.Generic;
using PisAndDag.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{
    /// <summary>
    /// What the Guide gives the player in his first words (GuideIntro): a lore item, so
    /// LoreLock keeps it on them. The lore is the user's own, in five verses split where its
    /// commas fall, with the sea at whichever end of this world it is at
    /// (<see cref="GuideIntro.SeaEast"/>, read each time the tooltip is drawn, so it is right
    /// in whatever world it is read in).
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class PieceOfPaper : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.White;

            Item.PAD().Lore = true;
            Item.PAD().LoreText = Text(false);
        }

        /// <summary>Runs before PADGlobalItem's (ItemLoader.ModifyTooltips calls the ModItem first), which reads the lore.</summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Item.PAD().LoreText = Text(GuideIntro.SeaEast);
        }

        private static string Text(bool seaEast)
        {
            string sea = seaEast ? "east" : "west";
            string land = seaEast ? "west" : "east";

            return "To the " + sea + " is the sea,\n" +
                   "where you can find heroes, burials and sand,\n" +
                   "to the " + land + " is the vast jungle and iceland,\n" +
                   "shielding a colossal kingdom prosperous,\n" +
                   "or at least what I remember.";
        }
    }
}
