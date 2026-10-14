using System.Collections.Generic;
using PisAndDag.Content.Items.Accessories;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Recipe edits for vanilla items: the ones this mod adds (AddRecipes), and the ones it
    /// takes away (PostAddRecipes).
    ///
    /// Taking away runs in PostAddRecipes, not AddRecipes: vanilla recipes exist by
    /// AddRecipes time, but other mods' recipes do not. Running last means a Megashark
    /// recipe added by any mod gets disabled too, not just the vanilla one.
    /// </summary>
    public class PADRecipes : ModSystem
    {
        /// <summary>"Any Gold Bar": Gold or Platinum. The game has no such group (RecipeGroupID has iron alone).</summary>
        public const string AnyGoldBar = "PisAndDag:AnyGoldBar";

        /// <summary>"Any Copper Bar": Copper or Tin.</summary>
        public const string AnyCopperBar = "PisAndDag:AnyCopperBar";

        /// <summary>
        /// The two groups, named as the game names its "Any Iron Bar" (Lang.misc[37], "Any", and the
        /// item's name). For the Trumpet's recipe ("any gold 3, any iron 6, any copper 14 at anvil").
        /// </summary>
        public override void AddRecipeGroups()
        {
            RecipeGroup.RegisterGroup(AnyGoldBar, new RecipeGroup(
                () => Lang.misc[37].Value + " " + Lang.GetItemNameValue(ItemID.GoldBar),
                ItemID.GoldBar, ItemID.PlatinumBar));

            RecipeGroup.RegisterGroup(AnyCopperBar, new RecipeGroup(
                () => Lang.misc[37].Value + " " + Lang.GetItemNameValue(ItemID.CopperBar),
                ItemID.CopperBar, ItemID.TinBar));
        }

        /// <summary>
        /// Greater Mana Potion: 3 Mana Potions, 7 Glowing Mushrooms and 3 Pixie Dust, once
        /// the world is in hardmode. Made at a Placed Bottle; the Alchemy Table counts as one
        /// (vanilla's Player.AdjTiles), and because the recipe needs a bottle tModLoader gives
        /// it the Alchemy Table's chance of saving ingredients, as it does vanilla's potions.
        /// Vanilla has no recipe for it at all; this is the only one.
        ///
        /// Then five more of the game's own items the game gives no recipe, by request:
        ///   Cloud in a Bottle: 25 Clouds and a Bottle, anywhere ("cloud in a bottle anywhere").
        ///   Whoopie Cushion: 3 Poo and 2 Silk. No place was named, so anywhere too.
        ///   Ice Skates: a pair of the mod's Boots, 4 Iron Bars and 20 Ice Blocks.
        ///   Hermes Boots: Boots and 50 Clouds.
        ///   Water Walking Boots: Boots, 50 Gel and a Water Bucket.
        /// The three boots at a Tinkerer's Workshop, as asked. The Iron Bars are the game's "Any
        /// Iron Bar" (RecipeGroupID.IronBar), so Lead Bars do too. The Water Bucket goes, bucket
        /// and all, as it does in the game's own recipes that take one.
        /// </summary>
        public override void AddRecipes()
        {
            Recipe.Create(ItemID.GreaterManaPotion)
                .AddIngredient(ItemID.ManaPotion, 3)
                .AddIngredient(ItemID.GlowingMushroom, 7)
                .AddIngredient(ItemID.PixieDust, 3)
                .AddTile(TileID.Bottles)
                .AddCondition(Condition.Hardmode)
                .Register();

            Recipe.Create(ItemID.CloudinaBottle)
                .AddIngredient(ItemID.Cloud, 25)
                .AddIngredient(ItemID.Bottle)
                .Register();

            Recipe.Create(ItemID.WhoopieCushion)
                .AddIngredient(ItemID.PoopBlock, 3)
                .AddIngredient(ItemID.Silk, 2)
                .Register();

            int boots = ModContent.ItemType<Boots>();

            Recipe.Create(ItemID.IceSkates)
                .AddIngredient(boots)
                .AddRecipeGroup(RecipeGroupID.IronBar, 4)
                .AddIngredient(ItemID.IceBlock, 20)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            Recipe.Create(ItemID.HermesBoots)
                .AddIngredient(boots)
                .AddIngredient(ItemID.Cloud, 50)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            Recipe.Create(ItemID.WaterWalkingBoots)
                .AddIngredient(boots)
                .AddIngredient(ItemID.Gel, 50)
                .AddIngredient(ItemID.WaterBucket)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }

        /// <summary>
        /// Items this mod removes from crafting entirely. Add an ItemID here and every
        /// recipe producing it stops working — no other change needed.
        /// </summary>
        private static readonly HashSet<int> Uncraftable = new HashSet<int>
        {
            ItemID.Megashark,
        };

        public override void PostAddRecipes()
        {
            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];

                // HasResult, not createItem.type: it accounts for recipe groups and is the
                // accessor tModLoader actually maintains.
                foreach (int type in Uncraftable)
                {
                    if (recipe.HasResult(type))
                    {
                        recipe.DisableRecipe();
                        break;
                    }
                }
            }
        }
    }
}
