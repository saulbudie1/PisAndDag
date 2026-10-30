using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Adam Kit I (by request: "increase defense by 15 but lower general speed by 10% (even flight); Press a button to Self
    /// Detonate (450 damage to self + cannot heal buff forgot the name) 25000 damage to other NPCs in a radius of 16 tiles").
    /// Worn: 15 defense (its own, Item.defense); a tenth off every speed, as the HEATR takes its share (TopSpeed.Slow: the top
    /// speed running and mounted, and the jump's speed, which caps every climb, wings' among them); a light at the chest
    /// (TentochiKitCore); and its key (PADKeybinds.Detonate, K unless rebound): TentochiPlayer.Detonate, then Potion Sickness.
    /// Out of hardmode crates, and the Cyborg's (TentochiSources).
    /// </summary>
    public class AdamKitI : TentochiItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.buyPrice(gold: 50);
            Item.defense = TentochiPlayer.KitDefense;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            TentochiPlayer tentochi = player.GetModPlayer<TentochiPlayer>();
            tentochi.Kit = true;
            tentochi.KitShown = !hideVisual;
            TopSpeed.Slow(player, 1f - TentochiPlayer.KitSpeed);
        }

        protected override string KeyLine() => Language.GetTextValue("Mods.PisAndDag.Tentochi.DetonateKey", KeyName(PADKeybinds.Detonate));

        protected override int KeyLineAfter => 0;
    }
}
