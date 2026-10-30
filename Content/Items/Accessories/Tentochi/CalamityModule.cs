using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Calamity Module (by request: "Allow player to store adrenaline by 100/30 point a sec; max 100; upon reaching 100 there
    /// is a button that activate Adrenaline mode for 5 seconds basically multiply all damage by 2.5x (homage to calamity)").
    /// Worn: the meter (TentochiPlayer.Fill, drawn under the wearer by TentochiHUD), and its key (PADKeybinds.Adrenaline,
    /// X unless rebound) letting it go (AdrenalineRush). Getting hurt empties it ("any damage reset adrenaline"), and a hit
    /// taken while it is full does half damage ("full adrenaline and gets hit half damage taken"): TentochiPlayer.ModifyHurt,
    /// OnHurt. Out of hardmode crates, and the Cyborg's (TentochiSources).
    /// </summary>
    public class CalamityModule : TentochiItem
    {
        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 26;
            Item.accessory = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 40);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<TentochiPlayer>().Module = true;
        }

        protected override string KeyLine() => Language.GetTextValue("Mods.PisAndDag.Tentochi.AdrenalineKey", KeyName(PADKeybinds.Adrenaline));

        protected override int KeyLineAfter => 0;
    }
}
