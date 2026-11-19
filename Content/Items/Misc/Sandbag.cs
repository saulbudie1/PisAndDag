using PisAndDag.Content.NPCs.Sandbag;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// The Sandbag (by request): a Sandbag (SandbagDummy) where the cursor is, ten at most in the world; its other use
    /// (right click) takes them all away. Made from a Target Dummy and sand.
    /// </summary>
    public class Sandbag : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 50;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = false;
            Item.UseSound = SoundID.Item1;
            Item.value = Item.sellPrice(silver: 1);
            Item.rare = ItemRarityID.Blue;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return true;

            if (player.altFunctionUse == 2)
                SandbagDummy.Clear();
            else
                SandbagDummy.Place(Main.MouseWorld);

            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.TargetDummy)
                .AddIngredient(ItemID.SandBlock, 20)
                .Register();
        }
    }
}
