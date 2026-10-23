using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    public class FairyRingSystem : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int index = tasks.FindIndex(pass => pass.Name.Equals("Final Cleanup"));

            if (index == -1)
                index = tasks.Count;

            tasks.Insert(index, new FairyRingPass("PisAndDag: The Fairy Ring", 10.0));
        }
    }

    /// <summary>
    /// Where Mycelium, Finish Them is found in a new world, by request (PADWorkFlow §16.98): a
    /// mushroom chest on the floor of the glowing mushroom caves, with a ring of small glowing
    /// mushrooms about it, the way a fairy ring stands round what it grew from. One a world, if the
    /// caves have anywhere flat enough; the Spore Zombies drop it too (VanillaLoot).
    ///
    /// A place is a stretch of mushroom grass two wide, flat, with room for a chest over it (what
    /// grows there, the small glowing mushrooms and the like, is cleared for it), under the cavern
    /// line, outside Grace's country in a Grand world, in the caves proper (enough mushroom grass
    /// round it). Every such floor in the world is found first, then they are tried in no order.
    /// Run in worldgen before the last tidying, and by /struct update in a world made before it
    /// (StructureUpdate.FairyRing); either way written in the update's ledger.
    /// </summary>
    public class FairyRingPass : GenPass
    {
        /// <summary>Floors tried, at the most, before it gives up (each one is cheap; the caves are usually found in the first few).</summary>
        private const int Tries = 600;

        /// <summary>Mushroom grass within the box about a place, at the least, for it to be the caves and not a stray patch.</summary>
        private const int Caves = 40;
        private const int Box = 14;

        public FairyRingPass(string name, double weight) : base(name, weight)
        {
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "A ring comes up in the dark";

            if (Place())
                StructureUpdate.Record(StructureUpdate.FairyRing);
        }

        /// <summary>Puts the chest and its ring in. False if nowhere would take them.</summary>
        public static bool Place()
        {
            UnifiedRandom rand = WorldGen.genRand;
            Item sample = ContentSamples.ItemsByType[ItemID.MushroomChest];
            ushort type = sample.createTile >= 0 ? (ushort)sample.createTile : TileID.Containers;
            int style = sample.placeStyle;

            int top = Math.Max((int)Main.rockLayer, (int)Main.worldSurface + 20);
            int bottom = Math.Max(top + 1, Main.UnderworldLayer - 40);

            // Every floor two wide that would take the chest.
            List<Point> floors = new();

            for (int x = Box + 10; x < Main.maxTilesX - Box - 10; x++)
            {
                if (InGraceCountry(x))
                    continue;

                for (int y = top; y < bottom; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && tile.TileType == TileID.MushroomGrass && Floor(x, y) && Floor(x + 1, y))
                        floors.Add(new Point(x, y));
                }
            }

            // Tried in no order: one picked at random from those not tried yet, each time.
            for (int t = 0; t < floors.Count && t < Tries; t++)
            {
                int pick = rand.Next(t, floors.Count);
                (floors[t], floors[pick]) = (floors[pick], floors[t]);
                Point floor = floors[t];

                if (!InTheCaves(floor.X, floor.Y))
                    continue;

                Clear(floor.X, floor.Y);
                int index = WorldGen.PlaceChest(floor.X, floor.Y - 1, type, false, style);

                if (index < 0 || Main.chest[index] == null)
                    continue;

                Fill(Main.chest[index], rand);
                Ring(floor.X, floor.Y, rand);
                return true;
            }

            return false;
        }

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

        /// <summary>Mushroom grass here, whole and flat, and two dry rows over it, empty or with only what a blade cuts growing there (never a pot: it would break open).</summary>
        private static bool Floor(int x, int y)
        {
            Tile ground = Main.tile[x, y];

            if (!ground.HasTile || ground.TileType != TileID.MushroomGrass || ground.IsActuated || ground.IsHalfBlock || ground.Slope != SlopeType.Solid)
                return false;

            for (int up = 1; up <= 2; up++)
            {
                Tile over = Main.tile[x, y - up];

                if ((over.HasTile && (!Main.tileCut[over.TileType] || over.TileType == TileID.Pots)) || over.LiquidAmount > 0)
                    return false;
            }

            return true;
        }

        /// <summary>What grows where the chest goes, out of its way, nothing dropped.</summary>
        private static void Clear(int x, int y)
        {
            for (int i = x; i <= x + 1; i++)
            {
                for (int up = 1; up <= 2; up++)
                {
                    if (Main.tile[i, y - up].HasTile)
                        WorldGen.KillTile(i, y - up, false, false, true);
                }
            }
        }

        private static bool InTheCaves(int x, int y)
        {
            int count = 0;

            for (int i = x - Box; i <= x + Box; i++)
            {
                for (int j = y - Box / 2; j <= y + Box / 2; j++)
                {
                    if (Main.tile[i, j].HasTile && Main.tile[i, j].TileType == TileID.MushroomGrass && ++count >= Caves)
                        return true;
                }
            }

            return false;
        }

        /// <summary>The incantation first; under it what anybody would leave in the caves.</summary>
        private static void Fill(Chest chest, UnifiedRandom rand)
        {
            var things = new List<(int type, int stack)>
            {
                (ModContent.ItemType<Items.Mage.Incantation.MyceliumFinishThem>(), 1),
                (ItemID.GlowingMushroom, rand.Next(10, 26)),
            };

            (int type, int least, int most)[] spare =
            {
                (ItemID.MushroomGrassSeeds, 2, 5), (ItemID.LesserHealingPotion, 1, 3), (ItemID.BottledWater, 1, 3),
                (ItemID.SilverCoin, 1, 5), (ItemID.Glowstick, 3, 8), (ItemID.Bottle, 1, 2),
            };

            var left = new List<(int type, int least, int most)>(spare);
            int count = rand.Next(2, 4);

            for (int i = 0; i < count && left.Count > 0; i++)
            {
                int pick = rand.Next(left.Count);
                things.Add((left[pick].type, rand.Next(left[pick].least, left[pick].most + 1)));
                left.RemoveAt(pick);
            }

            for (int slot = 0; slot < things.Count && slot < chest.item.Length; slot++)
            {
                chest.item[slot] ??= new Item();
                chest.item[slot].SetDefaults(things[slot].type);
                chest.item[slot].stack = Math.Clamp(things[slot].stack, 1, chest.item[slot].maxStack);
            }
        }

        /// <summary>Small glowing mushrooms round the chest, a few tiles out either side, wherever the grass takes them.</summary>
        private static void Ring(int x, int y, UnifiedRandom rand)
        {
            foreach (int dx in new[] { -8, -6, -4, 5, 7, 9 })
            {
                int cx = x + dx + rand.Next(-1, 2);

                for (int dy = -2; dy <= 2; dy++)
                {
                    int gy = y + dy;
                    Tile ground = Main.tile[cx, gy];

                    if (!ground.HasTile || ground.TileType != TileID.MushroomGrass || Main.tile[cx, gy - 1].HasTile)
                        continue;

                    WorldGen.PlaceTile(cx, gy - 1, TileID.MushroomPlants, true, false, -1, rand.Next(5));
                    break;
                }
            }
        }
    }
}
