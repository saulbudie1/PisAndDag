using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    public class CourtDrawersSystem : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int index = tasks.FindIndex(pass => pass.Name.Equals("Final Cleanup"));

            if (index == -1)
                index = tasks.Count;

            tasks.Insert(index, new CourtDrawersPass("PisAndDag: The Drawers", 10.0));
        }
    }

    /// <summary>
    /// The drawers: a dresser put into some of the game's underground cabins, by request (the
    /// Defacer, PADWorkFlow §16.98). Lore Bible §XVII: the Low Court's followers have no doctrine,
    /// only a debt, and "keep a drawer, carry a letter, or look away at the right moment". Nothing
    /// about the dressers says so.
    ///
    /// A cabin is a chest under the dirt line, against a house wall, on a floor of built things
    /// (planks of any wood, brick, the marble, granite and mushroom houses' blocks), outside Grace's
    /// country in a Grand world. Beside the chest, on the same built floor, the nearest three columns
    /// with room over them take a dresser of the cabin's wood: room is air or the clutter a cabin
    /// fills with (cobwebs, small rubble, a stalactite, anything a blade cuts but a pot), cleared for
    /// it; the house wall behind at least half of it (a cabin's walls are often broken through). Up
    /// to <see cref="Most"/> of them, in cabins taken in no order. What is in a drawer is what
    /// anybody keeps in one; the first of them keeps a Defacer under it, and one in
    /// <see cref="DefacerOdds"/> of the rest another. If no cabin has room for a dresser at all, the
    /// Defacer goes into a cabin's own chest instead (or, with no cabin, any chest under the dirt
    /// line), so that every world has one: the Drawers are its only source.
    ///
    /// Run in worldgen before the last tidying, and by /struct update in a world made before it
    /// (StructureUpdate.Drawers); either way written in the update's ledger, so it is done once.
    /// </summary>
    public class CourtDrawersPass : GenPass
    {
        public const int Most = 6;
        public const int DefacerOdds = 4;

        /// <summary>How far along the floor from its chest a dresser may stand, in tiles.</summary>
        private const int Reach = 14;

        public CourtDrawersPass(string name, double weight) : base(name, weight)
        {
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Somebody keeps a drawer";

            if (Place(out bool defacer) > 0 || defacer)
                StructureUpdate.Record(StructureUpdate.Drawers);
        }

        /// <summary>Puts the dressers in. Returns how many went in; <paramref name="defacerLaid"/>, whether a Defacer is in the world now.</summary>
        public static int Place(out bool defacerLaid)
        {
            UnifiedRandom rand = WorldGen.genRand;
            List<int> cabins = new();

            for (int i = 0; i < Main.chest.Length; i++)
            {
                if (Cabin(Main.chest[i]))
                    cabins.Add(i);
            }

            // In no order: a world's first drawers are not always its leftmost.
            for (int i = cabins.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                (cabins[i], cabins[j]) = (cabins[j], cabins[i]);
            }

            int placed = 0;
            bool defacer = false;

            foreach (int index in cabins)
            {
                if (placed >= Most)
                    break;

                Chest chest = Main.chest[index];

                if (chest == null || !Dresser(chest.x, chest.y + 1, rand, out Chest drawer))
                    continue;

                bool keeps = !defacer || rand.NextBool(DefacerOdds);
                Fill(drawer, keeps, rand);
                defacer |= keeps;
                placed++;
            }

            // No dresser anywhere: the hammer goes into a cabin's own chest, or any chest underground.
            if (!defacer)
                defacer = Stash(cabins, rand);

            defacerLaid = defacer;
            return placed;
        }

        /// <summary>The Defacer in the first free slot of a cabin's chest, or failing any, of a chest under the dirt line outside Grace's country.</summary>
        private static bool Stash(List<int> cabins, UnifiedRandom rand)
        {
            List<int> chests = new(cabins);

            if (chests.Count == 0)
            {
                for (int i = 0; i < Main.chest.Length; i++)
                {
                    Chest chest = Main.chest[i];

                    if (chest != null && chest.y > Main.worldSurface + 10 && chest.y < Main.UnderworldLayer - 20 && !InGraceCountry(chest.x)
                        && Main.tile[chest.x, chest.y].HasTile && Main.tile[chest.x, chest.y].TileType == TileID.Containers)
                        chests.Add(i);
                }
            }

            int start = chests.Count > 0 ? rand.Next(chests.Count) : 0;

            for (int k = 0; k < chests.Count; k++)
            {
                Chest chest = Main.chest[chests[(start + k) % chests.Count]];

                if (chest == null)
                    continue;

                for (int slot = 0; slot < chest.item.Length; slot++)
                {
                    if (chest.item[slot] != null && !chest.item[slot].IsAir)
                        continue;

                    chest.item[slot] ??= new Item();
                    chest.item[slot].SetDefaults(ModContent.ItemType<Items.Melee.Defacer>());
                    chest.item[slot].stack = 1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>A chest in a cabin: under the dirt line, in front of a house wall, on a floor of something built.</summary>
        private static bool Cabin(Chest chest)
        {
            if (chest == null)
                return false;

            int x = chest.x, y = chest.y;

            if (x < Reach + 4 || x >= Main.maxTilesX - Reach - 4 || y < Main.worldSurface + 10 || y > Main.UnderworldLayer - 20)
                return false;

            if (InGraceCountry(x))
                return false;

            Tile tile = Main.tile[x, y];

            if (!tile.HasTile || (tile.TileType != TileID.Containers && tile.TileType != TileID.Containers2))
                return false;

            if (tile.WallType == 0 || !Main.wallHouse[tile.WallType])
                return false;

            return Built(Main.tile[x, y + 2].TileType) && Main.tile[x, y + 2].HasTile;
        }

        /// <summary>Grace's plain, the Mile and the Cleared Ground in a Grand world: the kingdom keeps its own chests.</summary>
        private static bool InGraceCountry(int x)
        {
            if (!ExtraLargeWorld.Active)
                return false;

            foreach ((int left, int right) in new[] { Layout.Cleared, Layout.Mile, Layout.Plain })
            {
                if (x >= Math.Min(left, right) && x <= Math.Max(left, right))
                    return true;
            }

            return false;
        }

        /// <summary>What a cabin's floor is made of.</summary>
        private static bool Built(ushort type) => type is TileID.WoodBlock or TileID.BorealWood or TileID.RichMahogany or TileID.PalmWood
            or TileID.Ebonwood or TileID.Shadewood or TileID.GrayBrick or TileID.MarbleBlock or TileID.GraniteBlock
            or TileID.SandstoneBrick or TileID.MushroomBlock;

        /// <summary>The dresser that goes with this floor's wood or stone, as its item places it.</summary>
        private static (ushort type, int style) DresserFor(ushort floor)
        {
            int item = floor switch
            {
                TileID.BorealWood => ItemID.BorealWoodDresser,
                TileID.RichMahogany => ItemID.RichMahoganyDresser,
                TileID.PalmWood => ItemID.PalmWoodDresser,
                TileID.Ebonwood => ItemID.EbonwoodDresser,
                TileID.Shadewood => ItemID.ShadewoodDresser,
                TileID.MarbleBlock => ItemID.MarbleDresser,
                TileID.GraniteBlock => ItemID.GraniteDresser,
                TileID.SandstoneBrick => ItemID.SandstoneDresser,
                TileID.MushroomBlock => ItemID.MushroomDresser,
                _ => ItemID.Dresser,
            };

            Item sample = ContentSamples.ItemsByType[item];
            return sample.createTile == TileID.Dressers ? ((ushort)sample.createTile, sample.placeStyle) : (TileID.Dressers, 0);
        }

        /// <summary>
        /// A dresser on the floor this chest stands on (its bottom row <paramref name="stand"/>),
        /// within <see cref="Reach"/> of it, nearest first: three columns with room for it and the
        /// house wall behind every cell of it, on solid floor.
        /// </summary>
        private static bool Dresser(int chestX, int stand, UnifiedRandom rand, out Chest drawer)
        {
            drawer = null;
            int floor = stand + 1;
            (ushort type, int style) = DresserFor(Main.tile[chestX, floor].TileType);

            for (int d = 2; d <= Reach; d++)
            {
                foreach (int side in rand.NextBool() ? new[] { 1, -1 } : new[] { -1, 1 })
                {
                    int middle = side > 0 ? chestX + 1 + d : chestX - d;

                    if (!Room(middle, stand))
                        continue;

                    Clear(middle, stand);
                    int index = WorldGen.PlaceChest(middle, stand, type, false, style);

                    if (index < 0)
                        continue;

                    drawer = Main.chest[index];
                    return drawer != null;
                }
            }

            return false;
        }

        /// <summary>
        /// Three columns about <paramref name="middle"/>, two rows up from <paramref name="stand"/>: air or
        /// clutter, dry, the house wall behind at least half of it; under each column the cabin's own
        /// floor, built, solid and flat.
        /// </summary>
        private static bool Room(int middle, int stand)
        {
            int walled = 0;

            for (int x = middle - 1; x <= middle + 1; x++)
            {
                for (int y = stand - 1; y <= stand; y++)
                {
                    Tile cell = Main.tile[x, y];

                    if ((cell.HasTile && !Clutter(cell)) || cell.LiquidAmount > 0)
                        return false;

                    if (cell.WallType != 0 && Main.wallHouse[cell.WallType])
                        walled++;
                }

                Tile under = Main.tile[x, stand + 1];

                if (!under.HasTile || !Built(under.TileType) || under.IsActuated || under.IsHalfBlock || under.Slope != SlopeType.Solid)
                    return false;
            }

            return walled >= 3;
        }

        /// <summary>What a cabin fills with and a dresser can stand in the place of: cobwebs, small rubble, a stalactite, anything a blade cuts, but never a pot (it would break open).</summary>
        private static bool Clutter(Tile tile) => tile.TileType != TileID.Pots
            && (tile.TileType is TileID.Cobweb or TileID.SmallPiles or TileID.Stalactite || Main.tileCut[tile.TileType]);

        /// <summary>The clutter out of the dresser's place, nothing dropped.</summary>
        private static void Clear(int middle, int stand)
        {
            for (int x = middle - 1; x <= middle + 1; x++)
            {
                for (int y = stand - 1; y <= stand; y++)
                {
                    if (Main.tile[x, y].HasTile)
                        WorldGen.KillTile(x, y, false, false, true);
                }
            }
        }

        /// <summary>What a drawer holds: a few of the plain things anybody keeps, and under them, sometimes, the hammer.</summary>
        private static void Fill(Chest drawer, bool defacer, UnifiedRandom rand)
        {
            var things = new List<(int type, int stack)>();

            if (defacer)
                things.Add((ModContent.ItemType<Items.Melee.Defacer>(), 1));

            (int type, int least, int most)[] kept =
            {
                (ItemID.Silk, 2, 6), (ItemID.Candle, 1, 2), (ItemID.Book, 1, 2), (ItemID.Cobweb, 3, 10),
                (ItemID.Bottle, 1, 3), (ItemID.Leather, 1, 2), (ItemID.Glowstick, 2, 6), (ItemID.CopperCoin, 20, 90),
                (ItemID.SilverCoin, 1, 4), (ItemID.LesserHealingPotion, 1, 2), (ItemID.RecallPotion, 1, 1),
            };

            var left = new List<(int type, int least, int most)>(kept);
            int count = rand.Next(3, 6);

            for (int i = 0; i < count && left.Count > 0; i++)
            {
                int pick = rand.Next(left.Count);
                things.Add((left[pick].type, rand.Next(left[pick].least, left[pick].most + 1)));
                left.RemoveAt(pick);
            }

            for (int slot = 0; slot < things.Count && slot < drawer.item.Length; slot++)
            {
                drawer.item[slot] ??= new Item();
                drawer.item[slot].SetDefaults(things[slot].type);
                drawer.item[slot].stack = Math.Clamp(things[slot].stack, 1, drawer.item[slot].maxStack);
            }
        }
    }
}
