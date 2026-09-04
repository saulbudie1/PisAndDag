using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{  
	public class BladeOfMors : ModItem
	{
        private int ReleaseTimer;
        private float Meterlogy;
        private bool Full = false;
        public override void SetDefaults()
        {
            Item.damage = 30;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = 10000;
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            float max = 100f;
            if (Meterlogy > max) { Meterlogy = max; }
            float value = Meterlogy / max;
            string a = value.ToString("F1") + "% of death meter filled"; 
        }
        public override bool? UseItem(Player player)
        {


            return base.UseItem(player);
        }
        public override bool CanUseItem(Player player)
        {
            return ReleaseTimer <= 0;
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
