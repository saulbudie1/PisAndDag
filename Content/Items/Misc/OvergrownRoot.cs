using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// What the Dryad's second quest asks for (DryadRoots): a root cut from under one of the plants with mouths. It
    /// drops only for a player she is asking, one time in <see cref="NPCs.DryadRoots.DropChance"/>, from the Man
    /// Eater, the Snatcher and the Angry Trapper. Mine, all of it. Its picture: a gnarled cut root, still green at the
    /// cut, 2x2 pixels.
    /// </summary>
    public class OvergrownRoot : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 6;
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 22;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(copper: 50);
            Item.rare = ItemRarityID.Green;
        }
    }
}
