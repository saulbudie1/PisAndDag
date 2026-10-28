using System;
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

        /// <summary>The Life Crystals in the shrine's chest.</summary>
        private const int ShrineLifeCrystals = 5;

        /// <summary>The bones in it: this many at the least and at the most.</summary>
        private const int ShrineBonesLeast = 20;
        private const int ShrineBonesMost = 25;

        /// <summary>How many other things lie in it beside the Orb and the Life Crystals, the bones among them.</summary>
        private const int ShrineOthers = 7;

        /// <summary>
        /// What those other things are picked from: the thing, and how many of it at the
        /// least and at the most. Six of these go in with the bones, no two the same.
        /// </summary>
        private static readonly (int item, int least, int most)[] ShrineSpare =
        {
            (ItemID.Torch, 15, 40), (ItemID.Rope, 30, 70), (ItemID.Glowstick, 10, 25),
            (ItemID.LesserHealingPotion, 3, 6), (ItemID.RecallPotion, 1, 3), (ItemID.IronskinPotion, 1, 2),
            (ItemID.RegenerationPotion, 1, 2), (ItemID.SwiftnessPotion, 1, 2), (ItemID.MusketBall, 40, 90),
            (ItemID.WoodenArrow, 40, 90), (ItemID.ThrowingKnife, 20, 50), (ItemID.Shuriken, 20, 50),
            (ItemID.Bomb, 3, 8), (ItemID.IronBar, 5, 12), (ItemID.LeadBar, 5, 12),
            (ItemID.SilverCoin, 20, 70), (ItemID.Cobweb, 10, 30), (ItemID.Candle, 1, 3),
        };

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
        ///
        /// The chest holds the Orb, five Life Crystals, and seven other things: twenty to
        /// twenty-five bones and six of <see cref="ShrineSpare"/>, no two the same, in no
        /// order, so past the first two slots it differs from one world to the next.
        /// </summary>
        public static void FillShrineChest(Chest chest, int paletteIndex)
        {
            if (paletteIndex != ShrineChest)
                return;

            int slot = 0;

            Put(chest, slot++, ModContent.ItemType<GameChangingOrb>(), 1);
            Put(chest, slot++, ModContent.ItemType<Items.LoreItem.WeatheredLedger>(), 1);
            Put(chest, slot++, ItemID.LifeCrystal, ShrineLifeCrystals);

            // The Guide's quest is the hut seawards and what is in it (GuideQuest).
            NPCs.GuideQuest.LedgerPlaced = true;

            // WorldGen.genRand makes itself if it is not there, so this is as good from
            // /struct in a running world as it is from the pass.
            var rand = WorldGen.genRand;
            var others = new List<(int item, int stack)>();
            var spare = new List<(int item, int least, int most)>(ShrineSpare);

            while (others.Count < ShrineOthers - 1 && spare.Count > 0)
            {
                int pick = rand.Next(spare.Count);

                others.Add((spare[pick].item, rand.Next(spare[pick].least, spare[pick].most + 1)));
                spare.RemoveAt(pick);
            }

            others.Insert(rand.Next(others.Count + 1), (ItemID.Bone, rand.Next(ShrineBonesLeast, ShrineBonesMost + 1)));

            foreach ((int item, int stack) in others)
                Put(chest, slot++, item, stack);
        }

        /// <summary>One thing in one slot of a chest, never more of it than a stack holds.</summary>
        private static void Put(Chest chest, int slot, int type, int stack)
        {
            if (slot < 0 || slot >= chest.item.Length)
                return;

            chest.item[slot] ??= new Item();
            chest.item[slot].SetDefaults(type);
            chest.item[slot].stack = Math.Clamp(stack, 1, chest.item[slot].maxStack);
        }

        /// <summary>
        /// Slots the shrine into vanilla worldgen. Position in the list matters: this has
        /// to run once the spawn point is chosen (the shrine goes at the end of the forest
        /// round it) and the surface is grassed and planted, so that what it clears away
        /// is already there to clear.
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

            // After the shrine, and for the same reason it is here at all: the graveyard
            // levels ground and clears whatever is standing on it, so it has to run once
            // the surface, its grass and its trees are final. Put it before them and the
            // tree pass plants saplings straight through the markers.
            tasks.Insert(index + 2, new GraveyardPass("PisAndDag: Shoreline Graveyard", 80.0));
        }
    }
}
