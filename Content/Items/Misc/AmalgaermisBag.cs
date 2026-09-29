using PisAndDag.Content.Items.Summoner;
using PisAndDag.Content.NPCs.Abomanation_Small;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// Amalgaermis's treasure bag, one for every player who fought it, in expert and master
    /// (AbomanationHead.ModifyNPCLoot). It holds the essence, Joint Arms of the Defiled and
    /// the kill's coins. The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class AmalgaermisBag : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.BossBag[Type] = true;

            // A pre-hardmode boss's bag: vanilla rolls no developer items out of these.
            ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;

            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults()
        {
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.width = 26;
            Item.height = 28;
            Item.rare = ItemRarityID.Purple;
            Item.expert = true;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<EssenceOfTheFouled>(), 1,
                EssenceOfTheFouled.PerKillLeast, EssenceOfTheFouled.PerKillMost));

            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<JointArmsOfTheDefiled>()));

            // In expert the kill's money goes in the bag rather than on the ground.
            itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<AbomanationHead>()));
        }
    }
}
