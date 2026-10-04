using PisAndDag.Content.Tiles;
using Terraria;
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
    /// game's one place for putting a tile down (WorldGen.PlaceTile). A tombstone asks the
    /// one for setting a thing of several tiles down whole (TileObject.Place). The water,
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
    /// NOTHING SPAWNS, and THE CLOCK STANDS STILL: time, the world's own slow changes and
    /// its events all at rest (tModLoader's time rate, as Journey's freeze has it), so no
    /// night comes, nothing rains or falls, nobody moves in and nothing grows.
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

        /// <summary>Nor a thing of several tiles set down whole. A tombstone that is refused lies where it fell until it is gone, as one does that finds no room.</summary>
        private static bool NothingIsSetDown(On_TileObject.orig_Place orig, TileObject toBePlaced)
        {
            if (Kept && !WorldGen.gen)
                return false;

            return orig(toBePlaced);
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

    /// <summary>A player in a subworld builds nothing and digs nothing.</summary>
    public class SubworldRulesPlayer : ModPlayer
    {
        public override void ResetEffects()
        {
            if (SubworldRules.Kept)
                Player.noBuilding = true;
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
