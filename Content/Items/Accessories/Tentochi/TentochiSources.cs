using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Where Made By Tentochi comes from. Enterprise's Complimentary out of crates, by request; the rest, none being asked
    /// for, mine: what washes up with a fisher's crate, and what the Cyborg ("cyborg is of that origin") keeps to sell.
    ///
    /// <list type="bullet">
    /// <item>Any fishing crate: Enterprise's Complimentary 1 in 10, Exo Air I 1 in 15.</item>
    /// <item>A hardmode crate as well: Back Rocket 1 in 25, Calamity Module 1 in 25, Adam Kit I 1 in 30, Exo Air II 1 in 30.</item>
    /// <item>The Cyborg sells all six, at their price.</item>
    /// </list>
    /// </summary>
    public class TentochiCrates : GlobalItem
    {
        public const int ComplimentaryChance = 10;
        public const int ExoAirIChance = 15;
        public const int RocketChance = 25;
        public const int ModuleChance = 25;
        public const int KitChance = 30;
        public const int ExoAirIIChance = 30;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
            entity.type > ItemID.None && entity.type < ItemID.Sets.IsFishingCrate.Length && ItemID.Sets.IsFishingCrate[entity.type];

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<EnterprisesComplimentary>(), ComplimentaryChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<ExoAirI>(), ExoAirIChance));

            if (!ItemID.Sets.IsFishingCrateHardmode[item.type])
                return;

            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<BackRocket>(), RocketChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<CalamityModule>(), ModuleChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<AdamKitI>(), KitChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<ExoAirII>(), ExoAirIIChance));
        }
    }

    /// <summary>The Cyborg's stock of his makers' things: all six, at their price.</summary>
    public class TentochiCyborg : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType != NPCID.Cyborg)
                return;

            shop.Add<EnterprisesComplimentary>();
            shop.Add<ExoAirI>();
            shop.Add<ExoAirII>();
            shop.Add<BackRocket>();
            shop.Add<CalamityModule>();
            shop.Add<AdamKitI>();
        }
    }
}
