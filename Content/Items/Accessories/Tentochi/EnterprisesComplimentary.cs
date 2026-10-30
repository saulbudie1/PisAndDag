using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Enterprise's Complimentary (by request: "Allows the player to have a thin 2 pixel wide ray that is pointed at the
    /// cursor from the center of the player, allowing for more accurate shots when holding ranged weapons. Dropped by
    /// crates."). Worn, a ranged weapon held: the laser (TentochiSight). Out of any fishing crate, 1 in 10 (TentochiSources).
    /// Its picture: a rail-mounted laser module, drawn by code (Tentochi's red band, a ruby lens).
    /// </summary>
    public class EnterprisesComplimentary : TentochiItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 18;
            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 4);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<TentochiPlayer>().Sight = true;
        }
    }
}
