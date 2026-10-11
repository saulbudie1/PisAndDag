using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items
{
    /// <summary>
    /// The game's own items, with this mod's numbers in place of the game's. Set after the
    /// game's own (ItemLoader.SetDefaults is the last thing Item.SetDefaults does), so all
    /// that reads an item's damage reads these: the tooltip, every hit, a prefix (put on
    /// top of this), and the item kept as the type's sample (Item.OriginalDamage).
    ///
    /// The Zenith: 150 damage where the game gives it 190, by request ("zenith ok, 150").
    /// Nothing else of it changes: the swords it throws, how many, how often one can hit
    /// the same enemy again. A fifth less damage a second against anything unarmoured,
    /// more than that through armour; the sums against Foigh's Rust Side Sword are in
    /// PAD_Item_Plan. Separate from PSItem, which stays as it is.
    /// </summary>
    public class VanillaBalance : GlobalItem
    {
        /// <summary>The Zenith's damage (the game's own is 190).</summary>
        public const int ZenithDamage = 150;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.Zenith;

        public override void SetDefaults(Item item)
        {
            if (item.type == ItemID.Zenith)
                item.damage = ZenithDamage;
        }
    }
}
