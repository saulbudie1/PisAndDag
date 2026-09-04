using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
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
            Item.shoot = Item.shoot = ModContent.ProjectileType<Content.Projectiles.Ranged.Handswitch_Proj>(); ;
            Item.shootSpeed = 15f;                    
            Item.autoReuse = false;
            Item.noMelee = true;  
            Item.scale = 0.8f;

        }
        public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        { 
            player.statLife -= (int)(Math.Pow(player.statLifeMax2, 0.25f) * 2.7f);
            if(player.statLife < 0)
            {

                player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("A hand touches "+player.name+"'s shoulder"), (int)(Math.Pow(player.statLifeMax2, 0.25f) * 2.7) , 0);
            } 
            player.PIS().AddScreenShake(3.2f,1);

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
