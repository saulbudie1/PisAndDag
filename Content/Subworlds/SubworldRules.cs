using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// What holds in every subworld, whichever it is.
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
    /// NOBODY LEAVES A GRAVE. Whoever dies in one drops no stone: the game is not let throw
    /// one (Player.DropTombstone, the one place it does, by detour). Their coins fall, and
    /// the death is told and counted, as anywhere.
    ///
    /// NOTHING SPAWNS, and THE CLOCK STANDS STILL: time, the world's own slow changes and
    /// its events all at rest (tModLoader's time rate, as Journey's freeze has it), so no
    /// night comes, nothing rains or falls, nobody moves in and nothing grows.
    ///
    /// A PLAYER WEIGHS THE SAME AT ANY HEIGHT. The game makes one lighter near a world's top,
    /// and a place's top is always near (SubworldRulesPlayer): that is taken off again.
    ///
    /// The first two are lifted by hand, for building in one (/subworld rules).
    /// </summary>
    public class SubworldRules : ModSystem
    {
        /// <summary>This world is a subworld, and its rules have not been lifted.</summary>
        public static bool Kept => Subworld.Active && !SubworldKeeper.RulesLifted;

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
        }

        /// <summary>Everything in a subworld; and the door, wherever it stands.</summary>
        private static int NothingBreaks(On_WorldGen.orig_CheckTileBreakability orig, int x, int y)
        {
            if (Kept || SubworldDoor.IsAt(x, y))
                return NeverAndSilently;

            return orig(x, y);
        }

        /// <summary>No tile is put down in a subworld, whatever asks: the game's own "it could not be placed".</summary>
        private static bool NothingIsPut(On_WorldGen.orig_PlaceTile orig, int i, int j, int Type, bool mute, bool forced, int plr, int style)
        {
            if (Kept && !WorldGen.gen)
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
            if (Kept && !WorldGen.gen)
                return false;

            return orig(i, j);
        }

        public override void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate)
        {
            if (!Subworld.Active)
                return;

            timeRate = 0.0;
            tileUpdateRate = 0.0;
            eventUpdateRate = 0.0;
        }
    }

    /// <summary>No tile in a subworld is broken, blown up, hammered into another shape or swapped for another.</summary>
    public class SubworldTiles : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !SubworldRules.Kept;

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (SubworldRules.Kept)
                fail = true;
        }

        public override bool CanExplode(int i, int j, int type) => !SubworldRules.Kept;

        public override bool Slope(int i, int j, int type) => !SubworldRules.Kept;

        public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !SubworldRules.Kept;
    }

    /// <summary>Nor any wall.</summary>
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

    /// <summary>Nothing spawns in a subworld.</summary>
    public class SubworldQuiet : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (!Subworld.Active)
                return;

            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }
    }
}
