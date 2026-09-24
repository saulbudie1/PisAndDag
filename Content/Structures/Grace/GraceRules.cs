#if !PREVIEW
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The kingdom keeps to itself.
    ///
    /// Nothing comes into being in it on its own: no enemy, no critter, no wave of an
    /// invasion, anywhere inside its bounds or in the sky straight over it. What lives there
    /// is put there on purpose.
    ///
    /// Nobody arrives in it but on foot: a teleport of any kind (rod, potion, pylon,
    /// teleporter, conch, wormhole, hook) that would set a player down inside its bounds does
    /// not happen, and a Teleportation Potion rolls again for somewhere else. And nobody makes
    /// a bed in it their home: a bed inside it will not take a spawn point, and one taken
    /// before does not hold.
    ///
    /// Bosses that come for a player by their own clock, and anything a player summons or
    /// wires up, are not "on its own" and are left alone.
    /// </summary>
    public class GraceRules : ModSystem
    {
        /// <summary>How many times a Teleportation Potion looks again when it lands in the kingdom.</summary>
        private const int PotionTries = 12;

        /// <summary>Set when the last teleport asked for was refused.</summary>
        private static bool refused;

        public override void Load()
        {
            On_Player.Teleport += NoTeleportIn;
            On_Player.TeleportationPotion += RollElsewhere;
            On_Player.CheckSpawn += NoBedInside;
        }

        /// <summary>This world cell is inside the kingdom's bounds.</summary>
        public static bool Inside(int i, int j)
        {
            Rectangle a = GraceWorld.Area;
            return a.Width > 0 && a.Contains(i, j);
        }

        /// <summary>This world cell is inside the kingdom's bounds or in the sky over them: nothing spawns here.</summary>
        public static bool Quiet(int i, int j)
        {
            Rectangle a = GraceWorld.Area;
            return a.Width > 0 && i >= a.Left && i < a.Right && j < a.Bottom;
        }

        /// <summary>A player's body set down at this top-left world position would be inside the kingdom.</summary>
        private static bool LandsInside(Player player, Vector2 at)
        {
            Rectangle a = GraceWorld.Area;
            if (a.Width <= 0)
                return false;

            var body = new Rectangle((int)at.X, (int)at.Y, player.width, player.height);
            var kingdom = new Rectangle(a.X * 16, a.Y * 16, a.Width * 16, a.Height * 16);
            return body.Intersects(kingdom);
        }

        private static void NoTeleportIn(On_Player.orig_Teleport orig, Player self, Vector2 newPos, int style, int extraInfo)
        {
            if (LandsInside(self, newPos))
            {
                refused = true;
                return;
            }

            orig(self, newPos, style, extraInfo);
        }

        private static void RollElsewhere(On_Player.orig_TeleportationPotion orig, Player self)
        {
            for (int i = 0; i < PotionTries; i++)
            {
                refused = false;
                orig(self);
                if (!refused)
                    return;
            }
        }

        private static bool NoBedInside(On_Player.orig_CheckSpawn orig, int x, int y) => !Inside(x, y) && orig(x, y);
    }

    /// <summary>The spawn half of <see cref="GraceRules"/>.</summary>
    public class GraceQuiet : GlobalNPC
    {
        /// <summary>A player standing in the kingdom has nothing spawn anywhere round them.</summary>
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            Point at = player.Center.ToTileCoordinates();
            if (GraceRules.Quiet(at.X, at.Y))
                maxSpawns = 0;
        }

        /// <summary>
        /// And nothing spawns in the kingdom for a player standing outside it. An empty pool is
        /// a spawn that does not happen: tModLoader's ChooseSpawn then returns nothing and the
        /// game's SpawnNPC gives up on that spot, vanilla's own choices included.
        /// </summary>
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (GraceRules.Quiet(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY))
                pool.Clear();
        }
    }
}
#endif
