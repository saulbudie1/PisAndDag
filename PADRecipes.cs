using System.Collections.Generic;
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
        /// <summary>
        /// Greater Mana Potion: 3 Mana Potions, 7 Glowing Mushrooms and 3 Pixie Dust, once
        /// the world is in hardmode. Made at a Placed Bottle; the Alchemy Table counts as one
        /// (vanilla's Player.AdjTiles), and because the recipe needs a bottle tModLoader gives
        /// it the Alchemy Table's chance of saving ingredients, as it does vanilla's potions.
        /// Vanilla has no recipe for it at all; this is the only one.
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
