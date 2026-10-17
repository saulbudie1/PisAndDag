using System;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Vast Ocean laid out (VastOcean, whose notes say what it is): the floor
    /// (VastOceanFloor), the water, the platform and its waygate, the island (VastOceanIsland),
    /// and what lies on the floor. Run once, as the place's file is made, with the world's tiles
    /// cleared and sized.
    ///
    /// Everything on the floor is put down by the game's own hand (WorldGen.PlaceTile,
    /// WorldGen.PlaceSmallPile, WorldGen.PlaceObject), as its own generating puts down its piles,
    /// so each is framed and anchored the game's way; what will not go where it is tried is left
    /// out, and counted (<see cref="Counts"/>). The rest is written straight into the tiles, as
    /// a world's generating writes ground. No seaweed: the floor is far under the last of the
    /// sun's light, where nothing grows that needs it; and none of the beach's coral, the deep
    /// having its own (VastOceanFloor.PlantLife).
    ///
    /// Nothing here asks for the mod's types: the waygate's and the lighthouse lens's are handed
    /// in, so that the preview can lay the place out with the game alone.
    /// </summary>
    public static class VastOceanBuild
    {
        /// <summary>What was put down, and what would not go (all of it, and of each kind at <see cref="MissedFrom"/> plus the kind's own): for the preview's log.</summary>
        public static readonly int[] Counts = new int[20];

        public const int Drifts = 0, Shells = 1, Small = 2, SmallWide = 3, Large = 4, Missed = 5, MissedFrom = 6;

        // The game's own piles that a sea would have on its floor, by their frames on its sheets
        // (Tiles_185, Tiles_186, Tiles_187, 1.4.4), looked at one by one.

        /// <summary>Small piles one wide (Tiles_185, first row): grey stones, pale bones, broken marble, drifts of sand.</summary>
        private static readonly int[] SmallOne = { 0, 1, 2, 3, 4, 5, 12, 13, 14, 15, 16, 17, 18, 19, 66, 67, 68, 69, 70, 71, 73, 74, 78, 79, 80, 81 };

        /// <summary>Of <see cref="SmallOne"/>, the drifts of sand: the game keeps them only on sand (WorldGen.CheckPile).</summary>
        private static bool SandOnly(int style) => style >= 54 && style <= 59 || style >= 73 && style <= 76;

        /// <summary>Ground a drift of sand may lie on, as the game counts it (WorldGen.CheckPile).</summary>
        private static bool Sandy(int type) => TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.HardenedSand[type] || TileID.Sets.Conversion.Sandstone[type];

        /// <summary>Small piles two wide (Tiles_185, second row): grey stones, pale bones, broken wood.</summary>
        private static readonly int[] SmallTwo = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 31, 32 };

        /// <summary>Large piles (Tiles_186): bones and skulls, rubble, a helm and a sword left in the stones, wreckage of wood.</summary>
        private static readonly int[] LargeOne = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 22, 23, 24 };

        /// <summary>
        /// Large piles (Tiles_187): a skull and its bones, and from its second row (a style past
        /// 34 is drawn from there: TileDrawing.GetTileDrawData) a ball of marble and the broken
        /// marble and gold of a fallen hall.
        /// </summary>
        private static readonly int[] LargeTwo = { 9, 41, 42, 43, 44, 45, 46 };

        /// <summary>
        /// Lays the place out. <paramref name="gateType"/>: the waygate's tile (0 for none);
        /// <paramref name="plant"/>: what puts the floor's own living things and vents down (the
        /// game's own, SeaLifeTile.Plant; null for none); <paramref name="lensType"/>: the
        /// lighthouse's lens (LighthouseLens; 0 for none).
        /// </summary>
        public static void Lay(VastOcean.Dune dune, int seed, UnifiedRandom rand, ushort gateType, VastOceanFloor.Planter plant, ushort lensType = 0)
        {
            Array.Clear(Counts);

            // The ground, its first solid row in each column.
            int[] floor = VastOceanFloor.Lay(dune, seed);

            ShellDrifts(floor, rand);
            VastOceanFloor.Water();
            Platform();

            if (gateType != 0)
                Tiles.Waygate.Put(VastOcean.GateLeft, VastOcean.GateTop, gateType);

            // The island, on the sea once it is poured, before anything is put on the floor where
            // its chains come down.
            VastOceanIsland.Build(seed, plant, lensType);

            // What the floor's own places have first (the vents' worms, the seeps' mussels), then
            // what lies anywhere.
            VastOceanFloor.Furnish(floor, plant);
            Floor(floor, rand);
        }

        /// <summary>
        /// Drifts of shell in the sand's top, here and there: low mounds of Shell Pile, two rows
        /// deep at their middle; as many to a stretch of floor as when the sea was 759 wide, 14
        /// to 19 to that width. Only in sand, and only sand is made shell: the sand that is left
        /// has ground under it (VastOceanFloor.Settle), so the shell has too.
        /// </summary>
        private static void ShellDrifts(int[] floor, UnifiedRandom rand)
        {
            int count = (14 + rand.Next(6)) * VastOcean.Wide / 759;

            for (int i = 0; i < count; i++)
            {
                int middle = rand.Next(VastOcean.Wide / 16, VastOcean.Wide - VastOcean.Wide / 16);
                int half = rand.Next(2, 6);

                if (Main.tile[middle, floor[middle]].TileType != TileID.Sand)
                    continue;

                for (int x = middle - half; x <= middle + half; x++)
                {
                    int deep = Math.Abs(x - middle) <= half / 2 ? 2 : 1;

                    for (int y = floor[x]; y < floor[x] + deep; y++)
                    {
                        Tile tile = Main.tile[x, y];

                        if (tile.HasTile && tile.TileType == TileID.Sand)
                            tile.TileType = TileID.ShellPile;
                    }
                }

                Counts[Drifts]++;
            }
        }

        /// <summary>The top of the ground in this column is whole and level: something can be put down on it.</summary>
        private static bool Flat(int[] floor, int x)
        {
            Tile top = Main.tile[x, floor[x]];

            return top.Slope == SlopeType.Solid && !top.IsHalfBlock;
        }

        /// <summary>The ground's top here is level with its neighbours' within a row: on the ground, not on the tip of a spire or a lone rock.</summary>
        private static bool Even(int[] floor, int x) => Math.Abs(floor[x - 1] - floor[x]) <= 1 && Math.Abs(floor[x + 1] - floor[x]) <= 1;

        /// <summary>
        /// What the ground's top is here: the sediment (sand, shell, hardened sand, clay, mud,
        /// mudstone), rock (stone, granite), silt, or something else (moss, ice, bone, ash, glass,
        /// ore, carbonate, coral, a gem), on which nothing is put; and nothing on ground that is
        /// painted (a mat of bacteria, the green rock of the white chimneys), so that it shows.
        /// </summary>
        private static int Ground(int[] floor, int x)
        {
            Tile top = Main.tile[x, floor[x]];
            ushort type = top.TileType;

            if (top.TileColor != 0)
                return OnOther;

            if (type == TileID.Sand || type == TileID.ShellPile || type == TileID.HardenedSand || type == TileID.ClayBlock || type == TileID.Mud || type == TileID.Mudstone)
                return OnSediment;

            if (type == TileID.Stone || type == TileID.Granite)
                return OnRock;

            return type == TileID.Silt ? OnSilt : OnOther;
        }

        private const int OnSediment = 0, OnRock = 1, OnSilt = 2, OnOther = 3;

        /// <summary>The platform: every tile the place says it has (VastOcean.PlatformTile), with no water in it.</summary>
        private static void Platform()
        {
            for (int x = VastOcean.Middle - VastOcean.DeckHalf - 1; x <= VastOcean.Middle + VastOcean.DeckHalf + 2; x++)
            {
                for (int y = VastOcean.DeckTop - 8; y < VastOcean.FloorLowest - VastOcean.Dunes; y++)
                {
                    ushort type = VastOcean.PlatformTile(x, y);

                    if (type == 0)
                        continue;

                    Tile tile = Main.tile[x, y];

                    tile.ClearTile();
                    tile.HasTile = true;
                    tile.TileType = type;
                    tile.LiquidAmount = 0;
                }
            }

            // The deck's two ends worn down to a slope.
            Tile leftEnd = Main.tile[VastOcean.Middle - VastOcean.DeckHalf, VastOcean.DeckTop];
            Tile rightEnd = Main.tile[VastOcean.Middle + VastOcean.DeckHalf + 1, VastOcean.DeckTop];

            leftEnd.Slope = SlopeType.SlopeDownRight;
            rightEnd.Slope = SlopeType.SlopeDownLeft;
        }

        /// <summary>
        /// What lies on the floor, a column at a time across all of it that can be seen, where
        /// its top is level and even with its neighbours' and nothing stands on it yet: now and
        /// then a large pile where the ground is flat for three, a small one, a shell or a
        /// starfish. The piles on the sediment, the rock or the silt in the trench; the shells
        /// and the starfish on the sediment, and under the compensation depth
        /// (VastOceanFloor.Ccd), where an empty shell dissolves, starfish only. Each put down
        /// where it goes; past a thing two or three wide, the next column is the first after it.
        /// </summary>
        private static void Floor(int[] floor, UnifiedRandom rand)
        {
            int first = Subworld.Margin + 1;
            int last = VastOcean.Wide - Subworld.FarMargin - 2;

            for (int x = first; x <= last; x++)
            {
                int y = floor[x] - 1;
                int roll = rand.Next(1000);

                // Nothing is put down on a slope or a half block (the game would not hold it
                // there), on the tip of a spire or a lone rock, on moss, ice, bone or ash, nor
                // where something already stands.
                if (!Flat(floor, x) || !Even(floor, x) || Main.tile[x, y].HasTile)
                    continue;

                int ground = Ground(floor, x);

                if (ground == OnOther)
                    continue;

                // Large piles, four level columns in a hundred, on ground flat across their
                // three: this column and the two after it (the game puts one down about its
                // middle column), none of them yet lain on.
                if (roll < 40 && x + 2 <= last && floor[x + 1] == floor[x] && floor[x + 2] == floor[x]
                    && Flat(floor, x + 1) && Flat(floor, x + 2) && Ground(floor, x + 1) != OnOther && Ground(floor, x + 2) != OnOther
                    && !Main.tile[x + 1, y].HasTile && !Main.tile[x + 2, y].HasTile)
                {
                    if (rand.Next(3) == 0)
                        WorldGen.PlaceTile(x + 1, y, TileID.LargePiles2, true, false, -1, LargeTwo[rand.Next(LargeTwo.Length)]);
                    else
                        WorldGen.PlaceTile(x + 1, y, TileID.LargePiles, true, false, -1, LargeOne[rand.Next(LargeOne.Length)]);

                    if (Took(x + 1, y, TileID.LargePiles, TileID.LargePiles2, Large))
                    {
                        x += 2;
                        continue;
                    }
                }

                // Small piles two wide, six in a hundred.
                if (roll >= 40 && roll < 100 && x + 1 <= last && floor[x + 1] == floor[x] && Flat(floor, x + 1) && Ground(floor, x + 1) != OnOther && !Main.tile[x + 1, y].HasTile)
                {
                    WorldGen.PlaceSmallPile(x, y, SmallTwo[rand.Next(SmallTwo.Length)], 1, TileID.SmallPiles);

                    if (Took(x, y, TileID.SmallPiles, TileID.SmallPiles, SmallWide))
                    {
                        x += 1;
                        continue;
                    }
                }

                // Small piles one wide, ten in a hundred. Drifts of sand only on sand: on shell the
                // game takes them up the first time it frames them again (WorldGen.CheckPile wants
                // sand, hardened sand or sandstone under styles 54 to 59 and 73 to 76).
                if (roll >= 100 && roll < 200)
                {
                    int style = SmallOne[rand.Next(SmallOne.Length)];

                    while (SandOnly(style) && !Sandy(Main.tile[x, floor[x]].TileType))
                        style = SmallOne[rand.Next(SmallOne.Length)];

                    WorldGen.PlaceSmallPile(x, y, style, 0, TileID.SmallPiles);
                    Took(x, y, TileID.SmallPiles, TileID.SmallPiles, Small);
                    continue;
                }

                // A shell or a starfish, fourteen in a hundred, on the sediment; under the
                // compensation depth a starfish.
                if (roll >= 200 && roll < 340 && ground == OnSediment)
                {
                    WorldGen.PlaceTile(x, y, TileID.BeachPiles, true, false, -1, floor[x] >= VastOceanFloor.Ccd ? 1 : ShellStyle(rand));
                    Took(x, y, TileID.BeachPiles, TileID.BeachPiles, Shells);
                    continue;
                }

            }
        }

        /// <summary>
        /// Which of the beach's shells (Tiles_324, a row each): a sea shell or a starfish, half
        /// and half; one time in ten a lightning whelk, one in ten a tulip shell, one in fifty a
        /// junonia. The game's own odds for its beaches (WorldGen.RollRandomSeaShellStyle,
        /// 1.4.4, which is private), rolled here on this place's own random.
        /// </summary>
        private static int ShellStyle(UnifiedRandom rand)
        {
            int style = rand.Next(2);

            if (rand.Next(10) == 0)
                style = 2;

            if (rand.Next(10) == 0)
                style = 3;

            if (rand.Next(50) == 0)
                style = 4;

            return style;
        }

        /// <summary>The tile put down at this cell is one of these: counted under <paramref name="counter"/>; or it would not go, and that is counted.</summary>
        private static bool Took(int x, int y, ushort a, ushort b, int counter)
        {
            Tile tile = Main.tile[x, y];

            if (tile.HasTile && (tile.TileType == a || tile.TileType == b))
            {
                Counts[counter]++;
                return true;
            }

            Counts[Missed]++;
            Counts[MissedFrom + counter]++;
            return false;
        }
    }
}
