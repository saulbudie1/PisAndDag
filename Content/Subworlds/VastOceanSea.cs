using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Vast Ocean's own rules, past what every place has (Subworld, SubworldRules):
    ///
    /// ITS WATER CANNOT BE CARRIED OFF, by request ("water un buckettable"). The game takes
    /// water up with three things (Player.ItemCheck_UseBuckets, 1.4.4): the Empty Bucket, the
    /// Super Absorbant Sponge and the Ultra Absorbant Sponge. Here none of the three can be
    /// used at all, and the one who tries is told so over their head, once a second at
    /// most. Pouring is as anywhere.
    ///
    /// WHAT SPAWNS. The game spawns here as it does on a beach, but it can only put a thing
    /// down where it finds ground under the spot it rolls, and here the ground is far under
    /// the water: so in open water this puts down the sea's own creatures itself
    /// (<see cref="VastOceanSpawns"/>), and the game is let spawn only on sand under water (its
    /// sea branch, NPC.SpawnNPC: water, sand, and the sea's columns and rows) and wherever an
    /// invasion comes. Nothing spawns on the platform or on anything dry, but for an
    /// invasion's. From the 7th section down nothing spawns at all, by request (the Forevernight
    /// Zone "(no NPC spawn)", and under the 7th "nothing spawns (I will put my own creatures
    /// here)"): not the game's, not an invasion's, not these. The floor is in the 12th, so the
    /// game's crabs and snails no longer walk it.
    /// </summary>
    public class VastOceanBuckets : GlobalItem
    {
        /// <summary>Ticks between two tellings.</summary>
        private const int TellEvery = 60;

        private static uint toldAt;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.EmptyBucket || entity.type == ItemID.SuperAbsorbantSponge || entity.type == ItemID.UltraAbsorbantSponge;
        }

        public override bool CanUseItem(Item item, Player player)
        {
            if (SubworldKeeper.Current is not VastOcean)
                return true;

            if (player.whoAmI == Main.myPlayer && !Main.dedServ && Main.GameUpdateCount - toldAt >= TellEvery)
            {
                toldAt = Main.GameUpdateCount;
                CombatText.NewText(player.Hitbox, new Color(160, 200, 220), Language.GetTextValue("Mods.PisAndDag.Subworlds.Waygate.Uncarried"));
            }

            return false;
        }
    }

    /// <summary>The game's spawning in the Vast Ocean: on the floor's sand, and for invasions, and nowhere else; and what it was going to allow, for the open water's.</summary>
    public class VastOceanSpawnRules : GlobalNPC
    {
        /// <summary>The game's spawn rate and most spawns for each player, as it last worked them out (after every mod had its say before this one).</summary>
        internal static readonly int[] Rate = new int[Main.maxPlayers];
        internal static readonly int[] Most = new int[Main.maxPlayers];

        /// <summary>And its ranges, in tiles: where a spawn may be, and the nearer box it may not.</summary>
        internal static readonly Point[] Range = new Point[Main.maxPlayers];
        internal static readonly Point[] Safe = new Point[Main.maxPlayers];

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (SubworldKeeper.Current is not VastOcean)
                return;

            Rate[player.whoAmI] = spawnRate;
            Most[player.whoAmI] = maxSpawns;
        }

        public override void EditSpawnRange(Player player, ref int spawnRangeX, ref int spawnRangeY, ref int safeRangeX, ref int safeRangeY)
        {
            if (SubworldKeeper.Current is not VastOcean)
                return;

            Range[player.whoAmI] = new Point(spawnRangeX, spawnRangeY);
            Safe[player.whoAmI] = new Point(safeRangeX, safeRangeY);
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (SubworldKeeper.Current is not VastOcean)
                return;

            // From the 7th section down nothing, an invasion's or any.
            if (spawnInfo.SpawnTileY >= VastOcean.NoSpawnsFrom)
            {
                pool.Clear();
                return;
            }

            if (spawnInfo.Invasion)
                return;

            // The game's own sea: water over sand.
            if (spawnInfo.Water && Main.tileSand[spawnInfo.SpawnTileType])
                return;

            pool.Clear();
        }
    }

    /// <summary>
    /// The sea's creatures, put down in open water where the game cannot put them: around each
    /// player, off the screen as the game puts its own (its spawn box, less its safe box), at
    /// its rate and up to its most, and only where the floor is too far under the player for
    /// the game to find it. What comes depends on how deep:
    ///
    ///   AT THE SURFACE, as the game's sea has it (NPC.SpawnNPC) but for its seagulls, which
    ///   are not wanted here ("There is also seaguls spawning too"): one time in ten a sea
    ///   turtle or, by request ("dolphins are spawn in groups of 3-5"), a pod of three to five
    ///   dolphins together. In the rain, now and then a flying fish over the water.
    ///   IN THE SUNLIT WATER, the first four fifths of the 1st section (179 rows): the game's
    ///   sea, its odds kept as near as they go with nothing to stand on: a seahorse (gold, at the
    ///   game's odds for a golden critter) one time in thirty, a squid one in eighteen, else a
    ///   pink jellyfish.
    ///   IN THE TWILIGHT, to near the end of the 2nd section (420 rows): squid one in ten,
    ///   jellyfish of the game's caves (blue;
    ///   in hard mode half of them green) one in three, in hard mode an angler fish one in
    ///   twelve, else pink jellyfish.
    ///   IN THE DARK under that, to the end of the 6th section: the caves' jellyfish, mostly;
    ///   in hard mode angler fish one in six; squid one in eight.
    ///   SHARKS in the 2nd and 3rd sections only, by request ("sharks spawns in group of 1-3 in
    ///   section 2-3"): there, one time in six, before anything else, one to three together.
    ///   FROM THE 7TH DOWN: nothing; but the eels put in their crevices in the caves, from
    ///   the 8th down (CreviceEelSpawns).
    ///
    /// The members of a pod or a pack are put down in open water near the first, off the
    /// screen as it is; a pod of dolphins that has not room for three is not put down.
    ///
    /// All of them the game's own, by request ("Sharks and sea creatures spawn"); the cave
    /// jellyfish and the angler fish are the game's creatures of deep dark water, and are
    /// mine to put here.
    /// </summary>
    public class VastOceanSpawns : ModSystem
    {
        /// <summary>
        /// Rows under the surface the sunlit water goes to, and the twilight: four fifths of a
        /// section, and fifteen eighths (120 and 280 rows when a section was 149, kept as shares
        /// of a section when the sea was made half as deep again).
        /// </summary>
        private const int SunlitRows = VastOcean.SectionRows * 4 / 5;
        private const int TwilightRows = VastOcean.SectionRows * 15 / 8;

        /// <summary>Rows under the surface sharks come in: the 2nd and 3rd sections.</summary>
        private const int SharksFrom = VastOcean.SectionRows;
        private const int SharksTo = 3 * VastOcean.SectionRows;

        /// <summary>Tries for a spot of open water, each spawn; and for each other member of a pod or a pack.</summary>
        private const int Tries = 30;
        private const int MemberTries = 12;

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || SubworldKeeper.Current is not VastOcean)
                return;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead || player.ghost)
                    continue;

                int rate = VastOceanSpawnRules.Rate[player.whoAmI];
                int most = VastOceanSpawnRules.Most[player.whoAmI];

                // The game has not worked them out for this player (it skips the dead and the
                // calmed): nothing spawns that it would not have.
                if (rate <= 0 || most <= 0 || player.nearbyActiveNPCs >= most)
                    continue;

                if (!Main.rand.NextBool(rate))
                    continue;

                Point range = VastOceanSpawnRules.Range[player.whoAmI];
                Point safe = VastOceanSpawnRules.Safe[player.whoAmI];

                if (range.X <= 0 || range.Y <= 0)
                    continue;

                int px = (int)(player.Center.X / 16f);
                int py = (int)(player.Center.Y / 16f);

                // All of its box in the 7th section or under: nothing is put down there.
                if (py - range.Y >= VastOcean.NoSpawnsFrom)
                    continue;

                TrySpawn(px, py, range, safe);
            }
        }

        private static void TrySpawn(int px, int py, Point range, Point safe)
        {
            for (int attempt = 0; attempt < Tries; attempt++)
            {
                int x = px + Main.rand.Next(-range.X, range.X + 1);
                int y = py + Main.rand.Next(-range.Y, range.Y + 1);

                if (Seen(x, y, px, py, safe) || !Room(x, y))
                    continue;

                int depth = y - VastOcean.Surface;

                // Near enough the surface, and the surface over it open: what lives on it, as
                // the game's sea has it (an air row found over the water, within fifty).
                if (depth < 50 && OpenSurface(x) && SurfaceThing(x, px, py, safe))
                    return;

                int type = Deep(depth);

                NPC.NewNPC(Source, x * 16 + 8, y * 16 + 16, type);

                // A shark comes with none, one or two more.
                if (type == NPCID.Shark)
                    Pack(x, y, Main.rand.Next(3), px, py, safe);

                return;
            }
        }

        /// <summary>Inside the player's safe box: on the screen, where nothing is put down.</summary>
        private static bool Seen(int x, int y, int px, int py, Point safe)
        {
            return x > px - safe.X && x < px + safe.X && y > py - safe.Y && y < py + safe.Y;
        }

        /// <summary>A spot something may be put down in: open water, in the world, over the 7th section.</summary>
        private static bool Room(int x, int y)
        {
            return y < VastOcean.NoSpawnsFrom && WorldGen.InWorld(x, y, Subworld.Margin + 2) && OpenWater(x, y);
        }

        /// <summary>The rest of a pack of sharks, near the first (at <paramref name="x"/>, <paramref name="y"/>) and in the sections sharks come in.</summary>
        private static void Pack(int x, int y, int more, int px, int py, Point safe)
        {
            for (int member = 0; member < more; member++)
            {
                for (int attempt = 0; attempt < MemberTries; attempt++)
                {
                    int mx = x + Main.rand.Next(-8, 9);
                    int my = y + Main.rand.Next(-4, 5);
                    int depth = my - VastOcean.Surface;

                    if (depth < SharksFrom || depth >= SharksTo || Seen(mx, my, px, py, safe) || !Room(mx, my))
                        continue;

                    NPC.NewNPC(Source, mx * 16 + 8, my * 16 + 16, NPCID.Shark);
                    break;
                }
            }
        }

        /// <summary>
        /// A pod of three to five dolphins along the surface about this column, each at least
        /// three columns from the others, each where the surface is open and off the screen;
        /// false, and none put down, if there is not room for three.
        /// </summary>
        private static bool Pod(int x, int px, int py, Point safe)
        {
            int water = VastOcean.Surface;
            int count = Main.rand.Next(3, 6);
            int[] columns = new int[count];
            int found = 0;

            for (int attempt = 0; attempt < MemberTries * count && found < count; attempt++)
            {
                // The column found first, then others about it.
                int mx = attempt == 0 ? x : x + Main.rand.Next(-12, 13);
                bool apart = true;

                for (int i = 0; i < found; i++)
                {
                    if (System.Math.Abs(columns[i] - mx) < 3)
                        apart = false;
                }

                if (!apart || Seen(mx, water + 2, px, py, safe) || !WorldGen.InWorld(mx, water, Subworld.Margin + 2) || !OpenSurface(mx))
                    continue;

                columns[found++] = mx;
            }

            if (found < 3)
                return false;

            for (int i = 0; i < found; i++)
                NPC.NewNPC(Source, columns[i] * 16 + 8, (water + 2) * 16, NPCID.Dolphin);

            return true;
        }

        private static readonly EntitySource_SpawnNPC Source = new EntitySource_SpawnNPC("PisAndDag:VastOcean");

        /// <summary>A body of open water here: this cell and every one about it full of water and empty of anything solid.</summary>
        private static bool OpenWater(int x, int y)
        {
            for (int i = x - 1; i <= x + 1; i++)
            {
                for (int j = y - 1; j <= y + 1; j++)
                {
                    Tile tile = Main.tile[i, j];

                    if (tile.LiquidAmount < 200 || tile.LiquidType != LiquidID.Water || WorldGen.SolidTile(i, j))
                        return false;
                }
            }

            return true;
        }

        /// <summary>The surface of this column is open: water under it, and air over it with nothing in it.</summary>
        private static bool OpenSurface(int x)
        {
            int water = VastOcean.Surface;

            return OpenWater(x, water + 2) && Main.tile[x, water - 1].LiquidAmount == 0
                && !WorldGen.SolidTile(x, water - 1) && !WorldGen.SolidTile(x, water - 2) && !WorldGen.SolidTile(x, water - 3);
        }

        /// <summary>
        /// Something of the surface's put down over this column, at the game's odds, where it is
        /// off the screen (the spot found for it was, deeper down); false if nothing was.
        /// </summary>
        private static bool SurfaceThing(int x, int px, int py, Point safe)
        {
            int water = VastOcean.Surface;

            if (Main.raining && Main.rand.NextBool(8))
            {
                int over = water - 3 - Main.rand.Next(4);

                if (Seen(x, over, px, py, safe))
                    return false;

                NPC.NewNPC(Source, x * 16 + 8, over * 16, NPCID.FlyingFish);
                return true;
            }

            if (Main.rand.NextBool(10))
            {
                if (Main.rand.NextBool())
                {
                    if (Seen(x, water + 2, px, py, safe))
                        return false;

                    NPC.NewNPC(Source, x * 16 + 8, (water + 2) * 16, NPCID.SeaTurtle);
                    return true;
                }

                return Pod(x, px, py, safe);
            }

            return false;
        }

        /// <summary>What comes this deep, rolled.</summary>
        private static int Deep(int depth)
        {
            bool hard = Main.hardMode;

            if (depth >= SharksFrom && depth < SharksTo && Main.rand.NextBool(6))
                return NPCID.Shark;

            if (depth < SunlitRows)
            {
                if (Main.rand.NextBool(30))
                    return Main.player[Main.myPlayer].RollLuck(NPC.goldCritterChance) == 0 ? NPCID.GoldSeahorse : NPCID.Seahorse;

                if (Main.rand.NextBool(18))
                    return NPCID.Squid;

                return NPCID.PinkJellyfish;
            }

            int caveJelly = hard && Main.rand.NextBool() ? NPCID.GreenJellyfish : NPCID.BlueJellyfish;

            if (depth < TwilightRows)
            {
                if (Main.rand.NextBool(10))
                    return NPCID.Squid;

                if (Main.rand.NextBool(3))
                    return caveJelly;

                if (hard && Main.rand.NextBool(12))
                    return NPCID.AnglerFish;

                return NPCID.PinkJellyfish;
            }

            if (hard && Main.rand.NextBool(6))
                return NPCID.AnglerFish;

            if (Main.rand.NextBool(8))
                return NPCID.Squid;

            return caveJelly;
        }
    }
}
