using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// What Amalgaermis leaves when it is done eating itself. A crafting material: the
    /// incantations are written with it at the Makeshift Altar (Court of Hands takes 20,
    /// Everhunger 29, each with a Book).
    ///
    /// Twenty to twenty-five for every player who fought it: in classic straight off the
    /// kill, each player's own (AbomanationHead.ModifyNPCLoot); in expert and master in the
    /// treasure bag (AmalgaermisBag). The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class EssenceOfTheFouled : ModItem
    {
        /// <summary>The fewest one kill gives each player.</summary>
        public const int PerKillLeast = 20;

        /// <summary>The most one kill gives each player.</summary>
        public const int PerKillMost = 25;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 25;
        }

        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 22;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(silver: 1);
            Item.rare = ItemRarityID.Green;
        }
    }
}
