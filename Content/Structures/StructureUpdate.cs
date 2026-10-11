using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items;
using PisAndDag.Content.Tiles;
using PisAndDag.Content.Tiles.Misc;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Brings an older world up to date: whichever of the mod's structures it is
    /// missing get generated into it now. "/struct update".
    ///
    /// A world made before a structure existed has no record of it, so presence is
    /// read off the world itself, one test per structure:
    ///   Shoreline Graveyard   GraveyardZone.Exists (it saves its own rectangle)
    ///   Mors Monument         a Blade of Mors in Stone tile anywhere
    ///   Blood Shrine          a chest holding the Orb, the Orb placed as a tile, or
    ///                         Legends already on (the Orb was broken, so it was here)
    ///   The Way Down          its own record (WayDownState), saved with the world; made
    ///                         last, so that it keeps off a graveyard made just before it
    /// Anything this places is also written to a ledger saved with the world, so a
    /// structure the player has since dismantled is not put back a second time.
    ///
    /// The passes are the same ones worldgen runs. Two things worldgen would have
    /// given them are supplied by hand: the beach bounds the graveyard measures from
    /// (GenVars is empty outside worldgen, so they are read off the sand), and a
    /// reframe afterwards, since nothing else will frame tiles written directly.
    /// </summary>
    public class StructureUpdate : ModSystem
    {
        private const string LedgerKey = "PADStructuresPlaced";

        /// <summary>Structures placed by an update, by name.</summary>
        private static readonly HashSet<string> placed = new();

        public const string Graveyard = "Shoreline Graveyard";
        public const string Monument = "Mors Monument";
        public const string Shrine = "Blood Shrine";
        public const string WayDown = "The Way Down";

        public override void ClearWorld() => placed.Clear();

        public override void SaveWorldData(TagCompound tag)
        {
            if (placed.Count > 0)
                tag[LedgerKey] = new List<string>(placed);
        }

        public override void LoadWorldData(TagCompound tag)
        {
            placed.Clear();

            if (tag.ContainsKey(LedgerKey))
                placed.UnionWith(tag.GetList<string>(LedgerKey));
        }

        // ---- presence -----------------------------------------------------------------

        public static bool HasGraveyard() => placed.Contains(Graveyard) || GraveyardZone.Exists;

        public static bool HasWayDown() => placed.Contains(WayDown) || WayDownState.Made;

        public static bool HasMonument()
        {
            if (placed.Contains(Monument))
                return true;

            int blade = ModContent.TileType<BladeOfMorsInStone>();

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && tile.TileType == blade)
                        return true;
                }
            }

            return false;
        }

        public static bool HasShrine()
        {
            if (placed.Contains(Shrine) || PADWorld.Legends)
                return true;

            int orbItem = ModContent.ItemType<GameChangingOrb>();
            int orbTile = ModContent.TileType<Game_Changing_Orb>();

            foreach (Chest chest in Main.chest)
            {
                if (chest == null)
                    continue;

                foreach (Item item in chest.item)
                {
                    if (item != null && !item.IsAir && item.type == orbItem)
                        return true;
                }
            }

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && tile.TileType == orbTile)
                        return true;
                }
            }

            return false;
        }

        // ---- the update -----------------------------------------------------------------

        /// <summary>Runs every missing pass. Returns one line per structure for the caller to print.</summary>
        public static List<string> Run()
        {
            List<string> report = new();
            bool anything = false;

            if (HasShrine())
            {
                report.Add(Shrine + ": present.");
            }
            else
            {
                anything |= Apply(new BloodShrinePass("PisAndDag: Blood Shrine", 100.0), Shrine, report);
            }

            if (HasMonument())
            {
                report.Add(Monument + ": present.");
            }
            else
            {
                anything |= Apply(new MorsMonumentPass("PisAndDag: Mors Monument", 120.0), Monument, report);
            }

            if (HasGraveyard())
            {
                report.Add(Graveyard + ": present.");
            }
            else
            {
                MeasureBeaches();
                anything |= Apply(new GraveyardPass("PisAndDag: Shoreline Graveyard", 80.0), Graveyard, report);
            }

            // Last: it keeps off whatever the passes above have just made.
            if (HasWayDown())
            {
                report.Add(WayDown + ": present.");
            }
            else
            {
                anything |= MakeWayDown(report);
            }

            if (anything)
                ReframeWorld();

            return report;
        }

        private static bool Apply(GenPass pass, string name, List<string> report)
        {
            try
            {
                pass.Apply(new GenerationProgress(), null);
                placed.Add(name);
                report.Add(name + ": placed.");
                return true;
            }
            catch (System.Exception e)
            {
                ModContent.GetInstance<PisAndDag>().Logger.Error("Structure update failed for " + name, e);
                report.Add(name + ": failed, see the log.");
                return false;
            }
        }

        /// <summary>
        /// The way down finds its own way, and finds none rather than cut through anything
        /// (TheWayDownPass.Make): only a way made is put in the ledger.
        /// </summary>
        private static bool MakeWayDown(List<string> report)
        {
            try
            {
                if (!TheWayDownPass.Make(new GenerationProgress()))
                {
                    report.Add(WayDown + ": no way could be found that keeps clear of everything and can be walked down; see the log.");
                    return false;
                }

                placed.Add(WayDown);
                report.Add(WayDown + ": placed.");
                return true;
            }
            catch (System.Exception e)
            {
                ModContent.GetInstance<PisAndDag>().Logger.Error("Structure update failed for " + WayDown, e);
                report.Add(WayDown + ": failed, see the log.");
                return false;
            }
        }

        /// <summary>
        /// The graveyard keeps inland of GenVars.leftBeachEnd / rightBeachStart, which
        /// only exist during worldgen. Read them off the world: the beach is where the
        /// surface is sand, and it ends at the first column that is not.
        /// </summary>
        private static void MeasureBeaches()
        {
            GenVars.leftBeachEnd = 0;
            GenVars.rightBeachStart = Main.maxTilesX;

            for (int x = 40; x < Main.maxTilesX / 2; x++)
            {
                if (!SandyColumn(x))
                {
                    GenVars.leftBeachEnd = x;
                    break;
                }
            }

            for (int x = Main.maxTilesX - 40; x > Main.maxTilesX / 2; x--)
            {
                if (!SandyColumn(x))
                {
                    GenVars.rightBeachStart = x;
                    break;
                }
            }
        }

        /// <summary>The first solid tile under the sky in this column is sand, or the column is water down to sand.</summary>
        private static bool SandyColumn(int x)
        {
            int limit = (int)Main.worldSurface + 40;

            for (int y = 20; y < limit; y++)
            {
                Tile tile = Main.tile[x, y];

                // Palms and shells are not the ground; the first solid tile is.
                if (!tile.HasTile || !Main.tileSolid[tile.TileType])
                    continue;

                return tile.TileType == TileID.Sand || tile.TileType == TileID.HardenedSand || tile.TileType == TileID.ShellPile;
            }

            return false;
        }

        /// <summary>
        /// Tiles written straight into Main.tile carry no frame. Worldgen never frames
        /// them either; the game does it as they are drawn, but only for what changed
        /// through its own placement calls. Frame the lot: a few seconds, once.
        /// </summary>
        private static void ReframeWorld()
        {
            for (int x = 1; x < Main.maxTilesX - 1; x++)
            {
                for (int y = 1; y < Main.maxTilesY - 1; y++)
                {
                    // What WorldGen.RangeFrame does per tile, over the whole world.
                    if (Main.tile[x, y].HasTile)
                        WorldGen.TileFrame(x, y);

                    if (Main.tile[x, y].WallType > 0)
                        Framing.WallFrame(x, y);
                }
            }
        }
    }
}
