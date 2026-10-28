using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Summoner
{
    /// <summary>
    /// Jelle's Sable Helper, a summoning item of Harenaeum, by request ("Jelle's Fleshless Helper", renamed "Jelle's
    /// Sable Helper"; "Jelle be the name of arch witch, write that in"): a stoppered vial of her sand that, opened,
    /// lets out a ribbon of it to go round its owner and stand up as a ballista when a horde comes
    /// (SableConstruct). One minion slot each.
    ///
    /// Mine: its numbers (26 damage a bolt, a bolt a second, through three; knockback 3; the rest as the Vulture
    /// Staff's, held up rather than swung, as a thing opened is); who Jelle is, left to her name and the line
    /// under it (Localization).
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class JellesSableHelper : ModItem
    {
        public const int Damage = 26;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 30;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Summon;
            Item.knockBack = 3f;
            Item.mana = 10;

            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.noMelee = true;

            Item.buffType = ModContent.BuffType<JellesSableHelperBuff>();
            Item.shoot = ModContent.ProjectileType<SableConstruct>();

            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2, silver: 40);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            Projectile sand = Projectile.NewProjectileDirect(source, player.MountedCenter, Vector2.Zero, type, damage, knockback, player.whoAmI);
            sand.originalDamage = Item.damage;

            Harenaeum.Burst(player.MountedCenter + new Vector2(player.direction * 8f, -18f), new Vector2(0f, -1f), 18, 3f, 1.4f, 1f);
            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(12)
                .AddIngredient(ItemID.SandBlock, 15)
                .AddIngredient(ItemID.Glass, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
