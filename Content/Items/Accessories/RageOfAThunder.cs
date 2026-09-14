using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{ 
    public class RageOfAThunder : ModItem, ILocalizedModType
    {
        public new string LocalizationCategory => "Items.Accessories";
        int auraCounter = 0;

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 26;
            Item.value = 0;
            Item.rare = ItemRarityID.Yellow; 
            Item.accessory = true; 
            Item.expert = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.PAD().SurgingCore(damage: 350, arcs: 5,range:500, rate: 15, thickness: 45, throughWalls: true, colour: Color.DarkRed);
        }
         
    }
}
