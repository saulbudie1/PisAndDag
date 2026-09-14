using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{  
	public class MeleeDecider    : ModItem
	{
        private int ReleaseTimer;
        private float Meterlogy;
        private bool Full = false;
        public override void SetDefaults()
        {  
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20; 
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing; 
            Item.value = 0;
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.AbigailCry;
            Item.autoReuse = true;
        } 
        public override bool? UseItem(Player player)
        {


            return base.UseItem(player);
        }
        public override bool CanUseItem(Player player)
        {
            return ReleaseTimer <= 0;
        } 
    }
}
