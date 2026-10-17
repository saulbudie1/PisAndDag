using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// What holds in a subworld. The first four hold in every one that does not say
    /// otherwise (Subworld: what may be done here); the rest in every one, whatever it says.
    ///
    /// NOTHING IS BROKEN. No tile, no furniture, no wall: not by a pick, an axe or a hammer,
    /// not by anything that explodes, not by a block swapped for it, not by whatever else
    /// asks the game to break a tile. Asked the game's own way twice over:
    ///
    ///   - where the game itself decides whether a tile can be broken at all
    ///     (WorldGen.CheckTileBreakability, the first thing WorldGen.KillTile asks), the
    ///     answer here is its own "no, and say nothing of it": no sound, no dust, no cracks.
    ///     That is a detour, on a method far too long for the game to have folded into the
    ///     places it is called from;
    ///   - and tModLoader's own hooks say the same (the tiles' and walls' classes below), so
    ///     that the rule holds by them alone.
    ///
    /// NOTHING IS BUILT. A player in one cannot place, dig or pour: the game's own switch for
    /// that (Player.noBuilding, what Creative Shock sets), so a tool is swung and does
    /// nothing. Without it a block could be put down that nothing could ever take up again.
    /// The same goes for what puts a tile down with no player's hand in it. A ball of sand,
    /// a block of ice from a rod, the dirt of a bomb, a coil of rope, a web: all ask the
    /// game's one place for putting a tile down (WorldGen.PlaceTile). A thing of several
    /// tiles asks the one for setting such a thing down whole (TileObject.Place). The water,
    /// lava and honey of a bomb or a rocket ask the one for those (WorldGen.PlaceLiquid).
    /// And no actuator is fitted to anything (WorldGen.PlaceActuator, as the Kingdom of
    /// Grace has it for its own kept blocks, GraceWorld): a block switched off is a block
    /// walked through, which for a floor is as good as broken. All four answer no here, by
    /// detour. Not while a place is being laid out (WorldGen.gen), whose own laying out may
    /// use them.
    ///
    /// Not stopped, having no one place to stop them at and breaking nothing: paint and its
    /// scraping off, wire, and what turns one kind of tile into another.
    ///
    /// A place whose ground may be worked (Subworld.Untouched is false: the Vast Ocean) has
    /// none of this, but for what it keeps of its own (Subworld.KeepsTile), asked tile by
    /// tile the same two ways: that is never broken, swapped, hammered, fitted with an
    /// actuator or blown up. A door (SubworldDoor) and a waygate (Waygate) are never broken
    /// anywhere, in a subworld or out of one.
    ///
    /// NOBODY LEAVES A GRAVE. Whoever dies in one drops no stone: the game is not let throw
    /// one (Player.DropTombstone, the one place it does, by detour). Their coins fall, and
    /// the death is told and counted, as anywhere.
    ///
    /// NOTHING SPAWNS, and THE CLOCK STANDS STILL: time, the world's own slow changes and
    /// its events all at rest (tModLoader's time rate, as Journey's freeze has it), so no
    /// night comes, nothing rains or falls, nobody moves in and nothing grows. A place that
    /// says otherwise (Subworld.Spawns, Subworld.ClockRuns) has the game's own.
    ///
    /// NOBODY MOVES IN, whatever its clock, by request ("can we disable npcs moving in cuz
    /// this hobo tryna find a stay at our prestigious light house"). The game's daily round
    /// of who may come (Main.UpdateTime_SpawnTownNPCs: the next townsperson wanted, a pet or
    /// a slime come to stay, the old man at the dungeon's door while its curse holds, a
    /// house found for whoever has none) is not run in one, by detour; the one wanted is
    /// forgotten before the world's tiles are looked over for a room to put them in
    /// (WorldGen.prioritizedTownNPCType, read by WorldGen.TrySpawningTownNPC); and the
    /// travelling merchant does not come (WorldGen.SpawnTravelNPC, by detour). Whoever is
    /// already there, of the town or acting as if they were, is sent away, not killed: gone
    /// from the place, nothing dropped, nothing told (<see cref="SubworldTownsfolk"/>).
    ///
    /// NO METEOR FALLS on any of them, whatever its clock: the game drops one at a random
    /// column onto whatever it finds there (WorldGen.dropMeteor, its one place for that,
    /// by detour), and a place is too small to lose a piece of to it.
    ///
    /// A PLAYER WEIGHS THE SAME AT ANY HEIGHT. The game makes one lighter near a world's top,
    /// and a place's top is always near (SubworldRulesPlayer): that is taken off again.
    ///
    /// The first two are lifted by hand, for building in one (/subworld rules).
    /// </summary>
    public class SubworldRules : ModSystem
    {
        /// <summary>This world is a subworld whose rules hold (Subworld.Untouched), and they have not been lifted.</summary>
        public static bool Kept => Subworld.Active && Subworld.Current.Untouched && !SubworldKeeper.RulesLifted;

        /// <summary>
        /// Nothing is to be done to this tile: the whole place is kept, or this is a tile the
        /// place keeps of its own (Subworld.KeepsTile). Not while the rules are lifted.
        /// </summary>
        public static bool KeptAt(int x, int y)
        {
            if (!Subworld.Active || SubworldKeeper.RulesLifted)
                return false;

            return Subworld.Current.Untouched || Subworld.Current.KeepsTile(x, y);
        }

        /// <summary>The game's own answer for "cannot be broken, and nothing is made of the attempt" (WorldGen.KillTile returns on it at once).</summary>
        private const int NeverAndSilently = 2;

        public override void Load()
        {
            On_WorldGen.CheckTileBreakability += NothingBreaks;
            On_WorldGen.PlaceTile += NothingIsPut;
            On_TileObject.Place += NothingIsSetDown;
            On_WorldGen.PlaceLiquid += NothingIsPoured;
            On_WorldGen.PlaceActuator += NothingIsFitted;
            On_Player.DropTombstone += NoGrave;
            On_WorldGen.dropMeteor += NoMeteor;
            On_Main.UpdateTime_SpawnTownNPCs += NobodyIsWanted;
            On_WorldGen.SpawnTravelNPC += NoMerchantComes;
        }

        /// <summary>The game's round of who may move in, not run in a place; and nobody waiting to.</summary>
        private static void NobodyIsWanted(On_Main.orig_UpdateTime_SpawnTownNPCs orig)
        {
            if (Subworld.Active)
            {
                WorldGen.prioritizedTownNPCType = 0;
                return;
            }

            orig();
        }

        /// <summary>The travelling merchant does not come to a place.</summary>
        private static void NoMerchantComes(On_WorldGen.orig_SpawnTravelNPC orig)
        {
            if (Subworld.Active)
                return;

            orig();
        }

        /// <summary>
        /// Before the world's tiles are looked over (WorldGen.UpdateWorld, where a room found
        /// for the townsperson wanted is given them): in a place, nobody is wanted, whatever
        /// the last world left wanting.
        /// </summary>
        public override void PreUpdateWorld()
        {
            if (Subworld.Active)
                WorldGen.prioritizedTownNPCType = 0;
        }

        /// <summary>Everything in a subworld that keeps it all, and what a place keeps of its own; and a door or a waygate, wherever it stands.</summary>
        private static int NothingBreaks(On_WorldGen.orig_CheckTileBreakability orig, int x, int y)
        {
            if (KeptAt(x, y) || SubworldDoor.IsAt(x, y) || Waygate.IsAt(x, y))
                return NeverAndSilently;

            return orig(x, y);
        }

        /// <summary>No tile is put down in a subworld that keeps it all, or over one a place keeps, whatever asks: the game's own "it could not be placed".</summary>
        private static bool NothingIsPut(On_WorldGen.orig_PlaceTile orig, int i, int j, int Type, bool mute, bool forced, int plr, int style)
        {
            if (KeptAt(i, j) && !WorldGen.gen)
                return false;

            return orig(i, j, Type, mute, forced, plr, style);
        }

        /// <summary>Nor a thing of several tiles set down whole.</summary>
        private static bool NothingIsSetDown(On_TileObject.orig_Place orig, TileObject toBePlaced)
        {
            if (Kept && !WorldGen.gen)
                return false;

            return orig(toBePlaced);
        }

        /// <summary>
        /// Nobody who dies in a subworld leaves a stone there, lifted rules or no. The game
        /// throws one from here and nowhere else (Player.KillMe's last act, on the machine
        /// the world is decided on); nothing else of a death is in it.
        /// </summary>
        private static void NoGrave(On_Player.orig_DropTombstone orig, Player self, long coinsOwned, NetworkText deathText, int hitDirection)
        {
            if (Subworld.Active)
                return;

            orig(self, coinsOwned, deathText, hitDirection);
        }

        /// <summary>Nor any liquid.</summary>
        private static bool NothingIsPoured(On_WorldGen.orig_PlaceLiquid orig, int x, int y, byte liquidType, byte amount)
        {
            if (Kept && !WorldGen.gen)
                return false;

            return orig(x, y, liquidType, amount);
        }

        /// <summary>Nor an actuator fitted: the player keeps it.</summary>
        private static bool NothingIsFitted(On_WorldGen.orig_PlaceActuator orig, int i, int j)
        {
            if (KeptAt(i, j) && !WorldGen.gen)
                return false;

            return orig(i, j);
        }

        /// <summary>No meteor falls on a place, whatever its clock; the game, having picked tonight for one, simply has none.</summary>
        private static void NoMeteor(On_WorldGen.orig_dropMeteor orig)
        {
            if (Subworld.Active)
                return;

            orig();
        }

        public override void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate)
        {
            if (!Subworld.Active || Subworld.Current.ClockRuns)
                return;

            timeRate = 0.0;
            tileUpdateRate = 0.0;
            eventUpdateRate = 0.0;
        }
    }

    /// <summary>
    /// Nobody of a town stays in a place (SubworldRules: nobody moves in): a townsperson, a
    /// town's pet or slime, the old man, a merchant, or one acting as a townsperson does, is
    /// sent away by whoever runs the place (the server, or the one player's own game), not
    /// killed: nothing dropped and nothing told. Its own moves are not run meanwhile.
    /// </summary>
    public class SubworldTownsfolk : GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            if (!Subworld.Active || !npc.townNPC && !NPCID.Sets.ActsLikeTownNPC[npc.type])
                return true;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }

            return false;
        }
    }

    /// <summary>No tile a subworld keeps is broken, blown up, hammered into another shape or swapped for another.</summary>
    public class SubworldTiles : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !SubworldRules.KeptAt(i, j);

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (SubworldRules.KeptAt(i, j))
                fail = true;
        }

        public override bool CanExplode(int i, int j, int type) => !SubworldRules.KeptAt(i, j);

        public override bool Slope(int i, int j, int type) => !SubworldRules.KeptAt(i, j);

        public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !SubworldRules.KeptAt(i, j);
    }

    /// <summary>Nor any wall, where a place keeps it all.</summary>
    public class SubworldWalls : GlobalWall
    {
        public override void KillWall(int i, int j, int type, ref bool fail)
        {
            if (SubworldRules.Kept)
                fail = true;
        }

        public override bool CanExplode(int i, int j, int type) => !SubworldRules.Kept;
    }

    /// <summary>
    /// A player in a subworld builds nothing and digs nothing; and weighs, at any height in
    /// it, what they weigh on its ground.
    /// </summary>
    public class SubworldRulesPlayer : ModPlayer
    {
        public override void ResetEffects()
        {
            if (SubworldRules.Kept)
                Player.noBuilding = true;
        }

        /// <summary>
        /// The game thins a player's gravity near a world's top, by rows counted from its
        /// surface line (Player.Update, 1.4.4: <see cref="Thinned"/>). A place puts that line
        /// near its bottom, so that all of it is lit as open air; by the game's count, then,
        /// the head of a low place is the edge of space (in the Empty Hall, its top twelve
        /// rows). The share is taken off again here: the first thing a mod is asked after
        /// the game has put it on, and before anything has been moved by it. A gravity of
        /// nothing (what the game gives one who is webbed, or Distorted) stays nothing.
        /// </summary>
        public override void PreUpdateBuffs()
        {
            if (!Subworld.Active)
                return;

            float share = Thinned(Player);

            if (share < 1f)
                Player.gravity /= share;
        }

        /// <summary>
        /// The share of their weight the game leaves a player where they are: its own sum
        /// (Player.Update, 1.4.4). How far their top is under row sixty (and a little more
        /// in a wide world), over a sixth of the surface line; never under a quarter, never
        /// over one. In a world of the upside down seed, over all of the surface line, and
        /// never under a tenth.
        /// </summary>
        internal static float Thinned(Player player)
        {
            float wide = Main.maxTilesX / 4200f;

            wide *= wide;

            float under = player.position.Y / 16f - (60f + 10f * wide);
            float share = (float)(under / (Main.worldSurface / 6.0));

            if (Main.remixWorld)
            {
                share = (float)(under / (Main.worldSurface / 1.0));

                if (share < 0.1f)
                    share = 0.1f;
            }
            else if (share < 0.25f)
            {
                share = 0.25f;
            }

            if (share > 1f)
                share = 1f;

            return share;
        }
    }

    /// <summary>Nothing spawns in a subworld, but where the place says so (Subworld.Spawns).</summary>
    public class SubworldQuiet : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (!Subworld.Active || Subworld.Current.Spawns)
                return;

            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }
    }
}
