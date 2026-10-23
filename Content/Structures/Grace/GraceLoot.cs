using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>What a chest was kept for, and so what is still in it.</summary>
    public enum Loot { Ruin, Store, Military, Home, Scholar, Sea, Chapel, Royal, Crypt }

    /// <summary>
    /// The chests of the capital and what was left in them. A chest is made of what the house
    /// could afford (plain wood and barrels in the streets and the Undercity, marble and
    /// dynasty wood in the good rooms, gold in the court), and it holds what the room was for:
    /// arrows in the barracks, grain and ale in the cellars, books in the libraries, rope and
    /// shot on the ships, coin and stones where the house slept.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>One line of a loot table: the item, how many at least and at most, and how likely against the rest.</summary>
        private readonly struct Line
        {
            public readonly int Item, Min, Max, Weight;

            public Line(int item, int min, int max, int weight = 10)
            {
                Item = item;
                Min = min;
                Max = max;
                Weight = weight;
            }
        }

        /// <summary>How many slots a chest of each kind fills, at least and at most.</summary>
        private static readonly Dictionary<Loot, (int min, int max)> Slots = new Dictionary<Loot, (int, int)>
        {
            [Loot.Ruin] = (2, 5), [Loot.Store] = (3, 6), [Loot.Military] = (3, 7), [Loot.Home] = (3, 6),
            [Loot.Scholar] = (3, 6), [Loot.Sea] = (3, 7), [Loot.Chapel] = (3, 6), [Loot.Royal] = (5, 10),
            [Loot.Crypt] = (3, 6),
        };

        private static readonly Dictionary<Loot, Line[]> Tables = new Dictionary<Loot, Line[]>
        {
            [Loot.Ruin] = new[]
            {
                new Line(ItemID.Torch, 3, 12), new Line(ItemID.Rope, 10, 40), new Line(ItemID.Gel, 3, 12),
                new Line(ItemID.Mushroom, 1, 4), new Line(ItemID.Bottle, 1, 4), new Line(ItemID.Cobweb, 5, 25),
                new Line(ItemID.Bone, 2, 9), new Line(ItemID.LesserHealingPotion, 1, 2, 6), new Line(ItemID.WoodenArrow, 10, 30),
                new Line(ItemID.CopperCoin, 15, 90, 12), new Line(ItemID.SilverCoin, 1, 4, 5), new Line(ItemID.Wood, 5, 25),
                new Line(ItemID.OldShoe, 1, 1, 3), new Line(ItemID.TinCan, 1, 1, 3), new Line(ItemID.Glowstick, 2, 8),
                new Line(ItemID.Leather, 1, 3, 4), new Line(ItemID.BowlofSoup, 1, 1, 3), new Line(ItemID.ClayPot, 1, 2, 3),
            },
            [Loot.Store] = new[]
            {
                new Line(ItemID.Hay, 10, 50), new Line(ItemID.Wood, 10, 40), new Line(ItemID.Gel, 5, 15),
                new Line(ItemID.BottledWater, 2, 6), new Line(ItemID.BowlofSoup, 1, 3), new Line(ItemID.Ale, 1, 4),
                new Line(ItemID.Apple, 1, 4), new Line(ItemID.Peach, 1, 3), new Line(ItemID.Grapes, 1, 3),
                new Line(ItemID.Rope, 20, 60), new Line(ItemID.Torch, 5, 20), new Line(ItemID.IronBar, 2, 7, 6),
                new Line(ItemID.LeadBar, 2, 7, 6), new Line(ItemID.CopperCoin, 20, 99), new Line(ItemID.SilverCoin, 1, 9, 7),
                new Line(ItemID.Silk, 2, 8, 6), new Line(ItemID.Leather, 1, 4, 5), new Line(ItemID.EmptyBucket, 1, 2, 4),
            },
            [Loot.Military] = new[]
            {
                new Line(ItemID.WoodenArrow, 20, 70), new Line(ItemID.FlamingArrow, 10, 35), new Line(ItemID.MusketBall, 20, 70),
                new Line(ItemID.ThrowingKnife, 10, 35), new Line(ItemID.Shuriken, 10, 35), new Line(ItemID.Grenade, 3, 9, 7),
                new Line(ItemID.Bomb, 2, 6, 6), new Line(ItemID.LesserHealingPotion, 2, 5), new Line(ItemID.HealingPotion, 1, 2, 5),
                new Line(ItemID.IronskinPotion, 1, 2, 6), new Line(ItemID.ArcheryPotion, 1, 2, 5), new Line(ItemID.RegenerationPotion, 1, 2, 4),
                new Line(ItemID.Rope, 15, 40), new Line(ItemID.Torch, 5, 15), new Line(ItemID.IronBar, 3, 9, 6),
                new Line(ItemID.SilverCoin, 5, 35, 9), new Line(ItemID.Ale, 1, 3, 5),
                new Line(ItemID.Spear, 1, 1, 2), new Line(ItemID.IronBroadsword, 1, 1, 2), new Line(ItemID.IronBow, 1, 1, 2),
                new Line(ItemID.FlintlockPistol, 1, 1, 1), new Line(ItemID.IronHelmet, 1, 1, 2), new Line(ItemID.IronChainmail, 1, 1, 1),
                new Line(ItemID.IronGreaves, 1, 1, 2), new Line(ItemID.Shackle, 1, 1, 2),
            },
            [Loot.Home] = new[]
            {
                new Line(ItemID.Apple, 1, 4), new Line(ItemID.Peach, 1, 3), new Line(ItemID.Grapes, 1, 3),
                new Line(ItemID.CookedFish, 1, 3), new Line(ItemID.BowlofSoup, 1, 3), new Line(ItemID.Ale, 1, 3),
                new Line(ItemID.Bottle, 1, 5), new Line(ItemID.Candle, 1, 3), new Line(ItemID.Torch, 3, 12),
                new Line(ItemID.Book, 1, 3, 6), new Line(ItemID.Silk, 2, 6, 6), new Line(ItemID.Leather, 1, 3, 4),
                new Line(ItemID.CopperCoin, 20, 99), new Line(ItemID.SilverCoin, 1, 8, 8), new Line(ItemID.LesserHealingPotion, 1, 3),
                new Line(ItemID.ClayPot, 1, 2, 4), new Line(ItemID.Sunflower, 1, 2, 3), new Line(ItemID.Daybloom, 1, 5, 4),
                new Line(ItemID.CopperWatch, 1, 1, 1), new Line(ItemID.Umbrella, 1, 1, 1),
            },
            [Loot.Scholar] = new[]
            {
                new Line(ItemID.Book, 3, 12, 14), new Line(ItemID.Candle, 1, 4), new Line(ItemID.Glowstick, 3, 10),
                new Line(ItemID.Lens, 1, 4, 6), new Line(ItemID.FallenStar, 1, 4, 6), new Line(ItemID.LesserManaPotion, 2, 5),
                new Line(ItemID.NightOwlPotion, 1, 2, 6), new Line(ItemID.ShinePotion, 1, 2, 6), new Line(ItemID.SpelunkerPotion, 1, 2, 5),
                new Line(ItemID.CalmingPotion, 1, 2, 4), new Line(ItemID.SilverCoin, 3, 25, 9), new Line(ItemID.Feather, 1, 4, 5),
                new Line(ItemID.Compass, 1, 1, 1), new Line(ItemID.DepthMeter, 1, 1, 1), new Line(ItemID.GoldWatch, 1, 1, 1),
                new Line(ItemID.WizardHat, 1, 1, 1),
            },
            [Loot.Sea] = new[]
            {
                new Line(ItemID.Rope, 20, 70, 12), new Line(ItemID.Cannonball, 10, 40), new Line(ItemID.WoodenCrate, 1, 2, 6),
                new Line(ItemID.IronCrate, 1, 1, 2), new Line(ItemID.FishingPotion, 1, 3), new Line(ItemID.SonarPotion, 1, 2, 6),
                new Line(ItemID.CratePotion, 1, 2, 5), new Line(ItemID.ApprenticeBait, 3, 12), new Line(ItemID.JourneymanBait, 1, 5, 6),
                new Line(ItemID.Seashell, 1, 4), new Line(ItemID.Starfish, 1, 3), new Line(ItemID.Coral, 1, 4),
                new Line(ItemID.CookedFish, 1, 3), new Line(ItemID.CookedShrimp, 1, 3, 6), new Line(ItemID.Sake, 1, 3, 6),
                new Line(ItemID.Ale, 1, 3, 6), new Line(ItemID.SilverCoin, 5, 40, 9), new Line(ItemID.Compass, 1, 1, 2),
                new Line(ItemID.Sextant, 1, 1, 1), new Line(ItemID.FishermansGuide, 1, 1, 1), new Line(ItemID.WoodFishingPole, 1, 1, 2),
                new Line(ItemID.TreasureMap, 1, 1, 2), new Line(ItemID.Harpoon, 1, 1, 1),
            },
            [Loot.Chapel] = new[]
            {
                new Line(ItemID.Candle, 2, 7, 12), new Line(ItemID.HealingPotion, 1, 3), new Line(ItemID.LesserHealingPotion, 2, 5),
                new Line(ItemID.RegenerationPotion, 1, 2), new Line(ItemID.BottledWater, 2, 7), new Line(ItemID.FallenStar, 1, 4),
                new Line(ItemID.Book, 1, 3), new Line(ItemID.SilverCoin, 5, 40, 9), new Line(ItemID.GoldCoin, 1, 1, 3),
                new Line(ItemID.Daybloom, 2, 6, 6), new Line(ItemID.Moonglow, 1, 4, 5), new Line(ItemID.CalmingPotion, 1, 2, 5),
                new Line(ItemID.Robe, 1, 1, 1),
            },
            [Loot.Royal] = new[]
            {
                new Line(ItemID.GoldCoin, 1, 6, 16), new Line(ItemID.SilverCoin, 20, 99, 10), new Line(ItemID.Topaz, 1, 4),
                new Line(ItemID.Amethyst, 1, 4), new Line(ItemID.Sapphire, 1, 3), new Line(ItemID.Emerald, 1, 3),
                new Line(ItemID.Ruby, 1, 3), new Line(ItemID.Diamond, 1, 2, 6), new Line(ItemID.Amber, 1, 3, 6),
                new Line(ItemID.GoldBar, 3, 10), new Line(ItemID.PlatinumBar, 3, 10), new Line(ItemID.HealingPotion, 2, 4),
                new Line(ItemID.RegenerationPotion, 1, 2), new Line(ItemID.SwiftnessPotion, 1, 2), new Line(ItemID.IronskinPotion, 1, 2),
                new Line(ItemID.Candle, 1, 4), new Line(ItemID.Book, 1, 3, 5), new Line(ItemID.GoldenDelight, 1, 1, 4),
                new Line(ItemID.Steak, 1, 2, 4), new Line(ItemID.GoldCrown, 1, 1, 2), new Line(ItemID.PlatinumCrown, 1, 1, 1),
                new Line(ItemID.GoldWatch, 1, 1, 2), new Line(ItemID.PlatinumWatch, 1, 1, 1), new Line(ItemID.WhitePearl, 1, 2, 3),
                new Line(ItemID.BlackPearl, 1, 1, 2), new Line(ItemID.PinkPearl, 1, 1, 1), new Line(ItemID.TopHat, 1, 1, 2),
                new Line(ItemID.TuxedoShirt, 1, 1, 2), new Line(ItemID.TuxedoPants, 1, 1, 2), new Line(ItemID.Kimono, 1, 1, 1),
            },
            [Loot.Crypt] = new[]
            {
                new Line(ItemID.Bone, 5, 25, 14), new Line(ItemID.Cobweb, 5, 20), new Line(ItemID.Candle, 1, 4),
                new Line(ItemID.GoldCoin, 1, 3, 8), new Line(ItemID.SilverCoin, 5, 40), new Line(ItemID.Topaz, 1, 2, 5),
                new Line(ItemID.Ruby, 1, 1, 4), new Line(ItemID.Amethyst, 1, 2, 5), new Line(ItemID.GoldBar, 1, 4, 5),
                new Line(ItemID.AncientGoldHelmet, 1, 1, 1), new Line(ItemID.AncientIronHelmet, 1, 1, 1), new Line(ItemID.Book, 1, 2, 4),
            },
        };

        /// <summary>Whatever is left in a chest of this kind: a few different things, no line twice.</summary>
        private List<(int type, int stack)> Roll(Loot kind)
        {
            var table = Tables[kind];
            var (lo, hi) = Slots[kind];
            int n = c.Next(lo, hi + 1);
            var taken = new bool[table.Length];
            var items = new List<(int, int)>();

            for (int k = 0; k < n; k++)
            {
                int total = 0;
                for (int i = 0; i < table.Length; i++)
                    if (!taken[i])
                        total += table[i].Weight;
                if (total <= 0)
                    break;

                int roll = c.Next(total);
                for (int i = 0; i < table.Length; i++)
                {
                    if (taken[i])
                        continue;
                    roll -= table[i].Weight;
                    if (roll >= 0)
                        continue;

                    taken[i] = true;
                    items.Add((table[i].Item, c.Next(table[i].Min, table[i].Max + 1)));
                    break;
                }
            }

            return items;
        }

        /// <summary>
        /// The chest a room of this wealth keeps things in: plain wood, a barrel or boreal in the
        /// streets and the stores, marble, dynasty or pearlwood in the good rooms, gold or crystal
        /// at court; and whatever the kind of the room calls for over that.
        /// </summary>
        private (ushort type, int style) ChestFor(Loot kind, Suite s)
        {
            switch (kind)
            {
                case Loot.Ruin:
                    return (TileID.Containers, c.Pick(0, 0, 5, 6, 15));
                case Loot.Crypt:
                    return (TileID.Containers, c.Pick(41, 41, 50));
                case Loot.Sea:
                    return (TileID.Containers, c.Pick(31, 31, 5, 33));
                case Loot.Chapel:
                    return (TileID.Containers, c.Pick(47, 9, 51));
                case Loot.Royal:
                    return c.OneIn(3) ? (TileID.Containers2, c.Pick(1, 1, 0)) : (TileID.Containers, 1);
            }

            if (s == Suite.Gold)
                return c.OneIn(3) ? (TileID.Containers2, 1) : (TileID.Containers, 1);
            if (s == Suite.Marble)
                return (TileID.Containers, c.Pick(51, 51, 28, 9, 50));
            return c.OneIn(5) ? (TileID.Containers2, 16) : (TileID.Containers, c.Pick(0, 0, 5, 33));
        }

        /// <summary>A chest of the room's wealth at (x, stand), stocked for what the room was. False if it would not go.</summary>
        private bool Stash(int x, int stand, Loot kind, Suite s)
        {
            var (type, style) = ChestFor(kind, s);
            return c.PlaceChest(x, stand, type, style, Roll(kind));
        }

        /// <summary>The Parade Sabre (PADWorkFlow §16.98).</summary>
        private static int ParadeSabreItem => ModContent.ItemType<global::PisAndDag.Content.Items.Melee.ParadeSabre>();

        /// <summary>One in this many armoury chests after the first keeps a Parade Sabre too.</summary>
        private const int ParadeSabreOdds = 4;

        /// <summary>Whether a Parade Sabre has been laid in an armoury chest yet in this kingdom.</summary>
        private bool paradeLaid;

        /// <summary>
        /// An armoury's chest, at the start of its row of stands: what the racks did not hold
        /// (military loot), and the Parade Sabre on top: always in the first armoury chest the
        /// kingdom lays, one in <see cref="ParadeSabreOdds"/> after it ("beautiful and worse than
        /// what is outside the walls", as GraceHighWard has its armoury). False if it would not go,
        /// and then the sabre waits for the next.
        /// </summary>
        private bool StashArmoury(int x, int stand, Suite s)
        {
            var (type, style) = ChestFor(Loot.Military, s);
            var items = Roll(Loot.Military);
            bool sabre = !paradeLaid || c.OneIn(ParadeSabreOdds);

            if (sabre)
                items.Insert(0, (ParadeSabreItem, 1));

            if (!c.PlaceChest(x, stand, type, style, items))
                return false;

            paradeLaid |= sabre;
            return true;
        }

        /// <summary>What a room of this theme kept in its chests.</summary>
        private static Loot LootFor(Theme t, Suite s)
        {
            switch (t)
            {
                case Theme.Barracks:
                case Theme.Armoury:
                case Theme.Guard:
                case Theme.Dormitory:
                    return Loot.Military;
                case Theme.Store:
                    return Loot.Store;
                case Theme.Chapel:
                    return Loot.Chapel;
                case Theme.Library:
                case Theme.Study:
                case Theme.School:
                    return Loot.Scholar;
                case Theme.Bedroom:
                case Theme.Hall:
                    return s == Suite.Gold ? Loot.Royal : Loot.Home;
                default:
                    return Loot.Home;
            }
        }
    }
}
