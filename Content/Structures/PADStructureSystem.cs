using System.Collections.Generic;
using PisAndDag.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Owns the structure registry's lifetime, declares every structure's palette, and
    /// slots the generation pass into vanilla worldgen.
    ///
    /// Palettes live here rather than in the CSV so the data files stay stable: rename a
    /// tile class or swap a vanilla ID and only this file changes.
    /// </summary>
    public class PADStructureSystem : ModSystem
    {
        public const string BloodShrine = "Blood Shrine";

        /// <summary>Palette index of the shrine's chest, so the fill callback can tell it apart.</summary>
        private const int ShrineChest = 6;

        public override void Load() => PADStructures.Load();

        public override void Unload() => PADStructures.Unload();

        /// <summary>
        /// PostSetupContent, not Load — ModContent.TileType and ContentSamples are not
        /// resolvable until every mod's content is registered.
        /// </summary>
        public override void PostSetupContent()
        {
            PADStructures.Register(Mod, BloodShrine,
                "Content/Structures/BloodShrineShape.csv",
                "Content/Structures/BloodShrineWalls.csv",
                tilePalette: new Dictionary<int, StructureTile>
                {
                    // 0 = untouched and 1 = air are reserved; palettes start at 2.
                    [2] = StructureTile.Of(TileID.GrayBrick),
                    [3] = StructureTile.Of(TileID.Platforms),

                    // Object, not Of — an anvil has a footprint and must wait for the
                    // floor to exist before it can be placed on it.
                    [4] = StructureTile.Object(TileID.Anvils),

                    // FromItem resolves both tile type and place style. Beams and chests
                    // share one tile ID and differ only by an undocumented style number,
                    // so asking the item is the only way to be sure of getting it right.
                    [5] = StructureTile.FromItem(ItemID.RichMahoganyBeam),
                    [ShrineChest] = StructureTile.ChestFromItem(ItemID.LivingWoodChest),

                    // A candle stands on the floor, so it defers like the anvil does.
                    [8] = StructureTile.ObjectFromItem(ItemID.WaterCandle),
                    [9] = StructureTile.Of(TileID.IronBrick),
                },
                wallPalette: new Dictionary<int, int>
                {
                    [2] = WallID.GrayBrick,
                });
        }

        /// <summary>
        /// Loot for anything the shrine places. Kept in code so it can vary per world.
        /// </summary>
        public static void FillShrineChest(Chest chest, int paletteIndex)
        {
            if (paletteIndex != ShrineChest)
                return;

            chest.item[0].SetDefaults(ModContent.ItemType<GameChangingOrb>());
            chest.item[0].stack = 1;
        }

        /// <summary>
        /// Slots the shrine into vanilla worldgen. Position in the list matters: this has
        /// to run after the jungle exists and has been grassed, but before the passes that
        /// scatter clutter, or pots and vines end up sealed inside the walls.
        /// </summary>
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int index = tasks.FindIndex(pass => pass.Name.Equals("Micro Biomes"));

            // Fall back a step earlier if that pass is ever renamed, rather than silently
            // never generating.
            if (index == -1)
                index = tasks.FindIndex(pass => pass.Name.Equals("Living Trees"));

            if (index == -1)
            {
                Mod.Logger.Warn("Could not find a worldgen pass to attach the Blood Shrine to.");
                return;
            }

            tasks.Insert(index + 1, new BloodShrinePass("PisAndDag: Blood Shrine", 100.0));
        }
    }
}
