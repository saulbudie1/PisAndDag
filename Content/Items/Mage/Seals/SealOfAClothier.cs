using Terraria.ID;

namespace PisAndDag.Content.Items.Mage.Seals
{
    /// <summary>A plain seal. Casts whichever incantation is up, and nothing else.</summary>
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
}
