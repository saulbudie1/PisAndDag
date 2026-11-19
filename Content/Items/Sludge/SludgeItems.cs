using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Sludge
{
    /// <summary>A block of the sludge pocket, as an item (Tiles.Sludge): what mining one gives, and what puts one down.</summary>
    public abstract class SludgeBlockItem : ModItem
    {
        protected abstract int Places { get; }

        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 100;

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(Places);
            Item.width = 16;
            Item.height = 16;
            Item.value = Item.sellPrice(copper: 10);
        }
    }

    public class AcidicMud : SludgeBlockItem { protected override int Places => ModContent.TileType<Tiles.Sludge.AcidicMud>(); }

    public class StainedCobblestone : SludgeBlockItem { protected override int Places => ModContent.TileType<Tiles.Sludge.StainedCobblestone>(); }
}
