using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Recipe edits for vanilla items.
    ///
    /// PostAddRecipes, not AddRecipes: vanilla recipes exist by AddRecipes time, but other
    /// mods' recipes do not. Running last means a Megashark recipe added by any mod gets
    /// disabled too, not just the vanilla one.
    /// </summary>
    public class PADRecipes : ModSystem
    {
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
