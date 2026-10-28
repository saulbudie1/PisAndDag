using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Summoner
{
    /// <summary>
    /// Scorpius Servus, a summoning staff of Harenaeum, by request: a scorpion's tail of nine plates grown from the
    /// small of the back, that strikes over the shoulder at what comes within 13 tiles and poisons it; another tail
    /// for each summoned ("Does add extra tail"). The tail is ScorpiusTail, one minion slot.
    ///
    /// Mine: its numbers (18 damage, a little under the Imp Staff's 21 of the same rank, for the poison; knockback 2;
    /// the rest as the Vulture Staff's); the tail comes out of the owner's back where they stand, not at the
    /// cursor, with a shake of sand off it.
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class ScorpiusServus : ModItem
    {
        public const int Damage = 18;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 34;
            Item.height = 34;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Summon;
            Item.knockBack = 2f;
            Item.mana = 10;

            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.noMelee = true;

            Item.buffType = ModContent.BuffType<ScorpiusServusBuff>();
            Item.shoot = ModContent.ProjectileType<ScorpiusTail>();

            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2, silver: 40);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            Projectile tail = Projectile.NewProjectileDirect(source, player.MountedCenter, Vector2.Zero, type, damage, knockback, player.whoAmI);
            tail.originalDamage = Item.damage;

            Harenaeum.Burst(player.MountedCenter + new Vector2(-player.direction * 6f, 4f), new Vector2(-player.direction, -1f), 16, 3.5f, 1.2f, 1f);
            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(12)
                .AddIngredient(ItemID.AntlionMandible, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
