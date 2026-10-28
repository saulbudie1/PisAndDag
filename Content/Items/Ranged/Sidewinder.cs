using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Projectiles.Ranged;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// Sidewinder, a bow of Harenaeum (suggested, and taken: "Sidewinder:Yes"): its arrows go side to side as a
    /// snake goes over sand, and one that comes down on the ground goes into it, under it to the nearest enemy
    /// within 25 tiles, and comes up under that as a spike of sand (SidewinderArrow, SidewinderSpike). Every arrow it
    /// shoots is its own (mine): what is nocked gives its damage and is spent, but leaves as the Sidewinder's.
    ///
    /// Mine: its numbers (24 damage, a little under the Molten Fury's 29 of the same rank, for the spike; 24 ticks;
    /// the spike half as much again as the arrow).
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class Sidewinder : ModItem
    {
        public const int Damage = 24;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 42;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = 2f;

            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 8.5f;
            Item.useAmmo = AmmoID.Arrow;

            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2, silver: 40);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            type = ModContent.ProjectileType<SidewinderArrow>();
        }

        public override Vector2? HoldoutOffset() => new Vector2(-2f, 0f);

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
