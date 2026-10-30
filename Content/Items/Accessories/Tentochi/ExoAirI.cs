using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Exo Air I (by request: "An exoskeleton attached to leg that increase run speed by 7 mph; allow dash that of 13
    /// blocks"). Worn: 7 mph more top speed on foot (TentochiPlayer), the 13 tile dash (AccessoryDash), and its frame on the
    /// legs (ExoAirI_Shoes, the game's shoe layer; its lights ExoAirI_ShoesGlow, TentochiExoGlow). Not with Exo Air II.
    /// Out of any fishing crate, 1 in 15, and the Cyborg's (TentochiSources).
    /// </summary>
    [AutoloadEquip(EquipType.Shoes)]
    public class ExoAirI : TentochiItem
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
            Item.width = 26;
            Item.height = 30;
            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 6);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            TentochiPlayer tentochi = player.GetModPlayer<TentochiPlayer>();

            if (tentochi.Exo < 1)
                tentochi.Exo = 1;

            player.GetModPlayer<AccessoryDash>().Wear(AccessoryDash.Kind.ExoAirI);
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) =>
            !(equippedItem.type == ModContent.ItemType<ExoAirII>() || incomingItem.type == ModContent.ItemType<ExoAirII>());
    }
}
