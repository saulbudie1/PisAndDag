using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{  
	public class Handswitch : ModItem
	{ 
        public override void SetDefaults()
		{
			Item.damage = 80;
			Item.DamageType = DamageClass.Ranged;
			Item.width = 30;
			Item.height = 20;
			Item.useTime = 10;
			Item.useAnimation = 10; 
            Item.PIS().Magazine(10, 1 * 60);
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 6;
			Item.value = Item.buyPrice(silver: 1);
			Item.rare = ItemRarityID.Blue;
			Item.UseSound = SoundID.Item41;     
            Item.shoot = ModContent.ProjectileType<Content.Projectiles.Ranged.Handswitch_Proj>();
            Item.shootSpeed = 15f;                    
            Item.autoReuse = false;
            Item.noMelee = true;  
            Item.scale = 0.8f;

        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Scales gently with max life: ~8 at 100 HP, ~12 at 500 HP.
            int lifeCost = (int)(Math.Pow(player.statLifeMax2, 0.25) * 2.7);

            player.statLife -= lifeCost;

            // Writing statLife directly skips every death check, so at <= 0 the player just
            // stands there as a 0 HP zombie. Kill them explicitly instead of calling Hurt,
            // which would only ever have fired after they were already past zero.
            if (player.statLife <= 0)
            {
                player.statLife = 0;
                player.KillMe(PlayerDeathReason.ByCustomReason("A hand touches " + player.name + "'s shoulder"), lifeCost, 0);
            }

            // Otherwise regen quietly refunds the cost between shots.
            player.lifeRegenTime = 0;

            player.PIS().AddScreenShake(3.2f, 1);

            return true;
        }
        public override Vector2? HoldoutOffset()
        {

            return new Vector2(-1.1f, 1);
        }
		//public override void AddRecipes()
		//{
		//	Recipe recipe = CreateRecipe();
		//	recipe.AddIngredient(ItemID.DirtBlock, 10);
		//	recipe.AddTile(TileID.WorkBenches);
		//	recipe.Register();
		//}
	}
}
