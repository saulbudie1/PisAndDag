using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Exo Air II (by request: "An exoskeleton attached to leg that increase run speed by 12 mph; allow dash that of 18
    /// blocks"). As Exo Air I, more: 12 mph, 18 tiles, a heavier frame. Not with Exo Air I. Made from Exo Air I at a
    /// Tinkerer's Workshop with 10 Souls of Flight and 5 each of Light and Night; out of hardmode crates, and the Cyborg's
    /// (TentochiSources).
    /// </summary>
    [AutoloadEquip(EquipType.Shoes)]
    public class ExoAirII : TentochiItem
    {
        /// <summary>Its frame's slot among the game's shoes, or -1 before it is loaded.</summary>
        public static int ShoeSlot { get; private set; } = -1;

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            ShoeSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Shoes);
        }

        public override void Unload() => ShoeSlot = -1;

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 25);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<TentochiPlayer>().Exo = 2;
            player.GetModPlayer<AccessoryDash>().Wear(AccessoryDash.Kind.ExoAirII);
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) =>
            !(equippedItem.type == ModContent.ItemType<ExoAirI>() || incomingItem.type == ModContent.ItemType<ExoAirI>());

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<ExoAirI>()
                .AddIngredient(ItemID.SoulofFlight, 10)
                .AddIngredient(ItemID.SoulofLight, 5)
                .AddIngredient(ItemID.SoulofNight, 5)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
