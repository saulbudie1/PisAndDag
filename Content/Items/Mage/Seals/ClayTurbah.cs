using PisAndDag.Content.Items.Misc;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Seals
{
    /// <summary>
    /// Clay Turbah, a seal of Harenaeum and fired clay, by request ("boosts Earth Incantation damage by 15%"): it
    /// casts whichever incantation is up, as any seal does, and one whose element is Earth (Mini Sandstorm, and any
    /// after it) hurts 15% more for it.
    ///
    /// Mine: its rank and its recipe. The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class ClayTurbah : Catalyst
    {
        /// <summary>By request: 15% more.</summary>
        public const float EarthBoost = 1.15f;

        public override CastKind Kind => CastKind.Incantation;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 26;
            Item.height = 26;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 1, silver: 60);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            if (Selected(player)?.Element == "Earth")
                damage *= EarthBoost;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(8)
                .AddIngredient(ItemID.ClayBlock, 20)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
