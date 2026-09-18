using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    public class GreatbowOfAPilgrim : ModItem
    { 
        public const int ShootDelay = 32;
         

        public const int ArrowShootTime = 40;

        public const int MaxChargeTime = 300;

        public const float ArrowTargetingRange = 1100f;

        public const float MaxChargeDamageBoost = 3.5f;

        public const float LightningDamageFactor = 0.36f;

        public const float ChargeLightningCreationThreshold = 0.8f;
         
        public override void SetDefaults()
        {
            Item.width = 138;
            Item.height = 176;
            Item.damage = 256;
            Item.DamageType = DamageClass.Ranged;
            Item.useAnimation = Item.useTime = ArrowShootTime; // 40
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 13f;
            Item.useAmmo = AmmoID.Arrow;
            Item.useTurn = true;
            Item.rare = ItemRarityID.Red; 
        }

        // Shoot via the projectile only
        public override bool CanShoot(Player player) => false;
          
    }
}
