using System.Collections.Generic;
using PisAndDag.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{
    /// <summary>
    /// What the Guide gives the player in his first words (GuideIntro): a lore item, so
    /// LoreLock keeps it on them. Read each time the tooltip is drawn, so it is right in
    /// whatever world it is read in, with the sea at whichever end of this world it is at
    /// (<see cref="GuideIntro.SeaEast"/>).
    ///
    /// In a Grand world it is his map of the planned country, in the order it is walked from
    /// where the player woke up, every line of it true of the ground (PAD_World_Plan.md §3):
    /// toward the sea, the Blood Shrine at the edge of the spawn's forest (BloodShrinePass),
    /// the desert, the world's evil, the Dungeon and its old man, the graveyard hill behind the
    /// beach with its open grave (GraveyardPass); the other way, the snow, the jungle, the
    /// Cleared Ground's stumps (GraceCleanupPass), the Wasted Mile's ash, and the Curtain's
    /// white stone with the kingdom behind it. What he could not have seen (the Ball under the
    /// beach, the Sealed Pocket, the blade under the spawn) he does not mention. The user's own
    /// words carry it: the sea "where you can find heroes, burials and sand", "the vast jungle
    /// and iceland, shielding a colossal kingdom prosperous, or at least what I remember".
    ///
    /// In any other world it is the user's text as written, in five verses split at its commas.
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
            Item.PAD().LoreText = Text(false, false);
        }

        /// <summary>Runs before PADGlobalItem's (ItemLoader.ModifyTooltips calls the ModItem first), which reads the lore.</summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Item.PAD().LoreText = Text(GuideIntro.SeaEast, ExtraLargeWorld.Active);
        }

        private static string Text(bool seaEast, bool grand)
        {
            string sea = seaEast ? "east" : "west";
            string land = seaEast ? "west" : "east";

            if (!grand)
            {
                return "To the " + sea + " is the sea,\n" +
                       "where you can find heroes, burials and sand,\n" +
                       "to the " + land + " is the vast jungle and iceland,\n" +
                       "...\n" +
                       "or at least what I remember.";
            }

            return "To the " + sea + " is the sea, where you can find heroes,\n" +
                   "burials and sand. On the way there is\n" +
                   "a little hut, where the trees round here give out,\n" +
                   "the desert, then a stretch of ground gone sick,\n" +
                   "an old keep with an old man at its door,\n" +
                   "To the " + land + " is the vast iceland and a restless jungle\n" +
                   "then a field where every tree is a stump,\n" +
                   "then a mile of ash where you can hear atrocities,\n" +
                   "and a white wall taller than any tree ever grew,\n" +
                   "shielding a colossal kingdom prosperous,\n" +
                   "or at least that's what I remember lol.";
        }
    }
}
