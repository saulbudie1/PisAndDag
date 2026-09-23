using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Seals
{
    /// <summary>
    /// A plain seal. Casts whichever incantation is up, and nothing else.
    ///
    /// Comes from the Clothier: his shop sells it, and Skeletron drops it (in the
    /// bag, in expert). See <see cref="SealOfAClothierSource"/>.
    /// </summary>
    public class SealOfAClothier : Catalyst
    {
        public override CastKind Kind => CastKind.Incantation;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 28;
            Item.height = 24;
            Item.rare = ItemRarityID.Green;
        }
    }

    /// <summary>Where the seal comes from: the Clothier's shop, and Skeletron, every time.</summary>
    public class SealOfAClothierSource : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType == NPCID.Clothier)
                shop.Add(ModContent.ItemType<SealOfAClothier>());
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // Classic and below only: in expert the drop is in the bag, the way vanilla
            // does it, so it does not fall twice.
            if (npc.type == NPCID.SkeletronHead)
                npcLoot.Add(ItemDropRule.ByCondition(new Conditions.NotExpert(), ModContent.ItemType<SealOfAClothier>()));
        }
    }

    /// <summary>The expert half of the drop: Skeletron's bag.</summary>
    public class SealOfAClothierBag : GlobalItem
    {
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            if (item.type == ItemID.SkeletronBossBag)
                itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<SealOfAClothier>()));
        }
    }
}
