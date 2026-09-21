using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// Nothing small lives on the Mile. Every critter is struck from the spawn pool
    /// when the spawn point is inside the biome: bunnies, birds, butterflies, fireflies,
    /// the gold ones, all of them. Enemies are untouched.
    ///
    /// The check is on the spawn tile, not on the player, because the pool is built
    /// for a point up to sixty tiles from the player and the biome edge is a hard line.
    ///
    /// Statue critters and anything placed by NPC.NewNPC do not go through the pool
    /// and are not affected. The summon (AbomanationSummon) is the one exception that
    /// matters and it is handled there: a released critter is what wakes the worm.
    /// </summary>
    public class WastedMileCritters : GlobalNPC
    {
        private static readonly List<int> Struck = new List<int>();

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (!WastedMileBiome.Covers(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY))
                return;

            Struck.Clear();

            foreach (KeyValuePair<int, float> entry in pool)
            {
                if (IsCritter(entry.Key))
                    Struck.Add(entry.Key);
            }

            foreach (int type in Struck)
                pool.Remove(type);
        }

        private static bool IsCritter(int type)
        {
            return NPCID.Sets.CountsAsCritter[type] || NPCID.Sets.TownCritter[type];
        }
    }
}
