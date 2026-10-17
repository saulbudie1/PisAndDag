using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// A place of the mod's own, apart from the world: a small world kept in a file of its
    /// own beside the world it belongs to, gone into through a door and come back from
    /// through another (SubworldKeeper, SubworldPassage). Written from nothing against the game
    /// itself: the mod needs no other mod for it.
    ///
    /// One of these is what such a place is: how big, how it is laid out the first time
    /// anybody goes there, where its door stands. Any class of this kind in the mod is
    /// found and kept by itself, like an item or a tile.
    ///
    /// What is true of every one of them unless it says otherwise (SubworldRules,
    /// SubworldScene, SubworldFlags, and the members under "what may be done here"):
    /// nothing in it can be broken or built (<see cref="Untouched"/>); nothing spawns
    /// (<see cref="Spawns"/>); the clock stands still, at a quarter past eight in the
    /// morning (<see cref="ClockRuns"/>); it is never the sea, and nothing is drawn behind
    /// it but the sky (<see cref="IsSea"/>); all of it is lit as the surface is
    /// (<see cref="SurfaceLine"/>). And what is true of every one of them whatever it says:
    /// nobody who dies in it leaves a grave; no meteor falls on it; a player weighs at any
    /// height in it what they weigh on its ground; there is no music unless it names some;
    /// and its flags are its world's, hard mode and every boss beaten among them, so that
    /// what is done in it is done in the world.
    ///
    /// THE EDGES. The game keeps a player 41 tiles in from a world's left and top edges and
    /// 42 from its right and bottom ones, and the camera with them, so that much of every
    /// side is never seen or stood in. What can be seen and reached is columns 41 to
    /// Width - 43 and rows 41 to Height - 43.
    /// </summary>
    public abstract class Subworld : ModType
    {
        /// <summary>The place the loaded world is, or null in a world (SubworldKeeper).</summary>
        public static Subworld Current => SubworldKeeper.Current;

        /// <summary>The loaded world is a subworld.</summary>
        public static bool Active => SubworldKeeper.Current != null;

        /// <summary>The game's own margin: columns and rows this near the left and top edges are never seen.</summary>
        public const int Margin = 41;

        /// <summary>And this near the right and bottom ones.</summary>
        public const int FarMargin = 42;

        /// <summary>In tiles, edges and all.</summary>
        public abstract int Width { get; }
        public abstract int Height { get; }

        /// <summary>The first and last column, and row, that can be seen and reached.</summary>
        public int Left => Margin;
        public int Right => Width - FarMargin - 1;
        public int Top => Margin;
        public int Bottom => Height - FarMargin - 1;

        /// <summary>
        /// Where the way back stands: the top left tile of its door (SubworldDoor, two wide
        /// and three tall) or of its waygate (Waygate, two by two). Whoever comes in is put
        /// down in front of it.
        /// </summary>
        public abstract Point Door { get; }

        /// <summary>
        /// Where somebody who dies here comes back: the tile their feet are on. The game
        /// wants the three columns over it clear, door and all, or it digs them out; so
        /// beside the door, by default, and not in it. A place whose way back is a waygate
        /// says where.
        /// </summary>
        public virtual Point Spawn => new Point(Door.X + SubworldDoor.Wide + 1, Door.Y + SubworldDoor.Tall);

        /// <summary>The game's own number for no music at all (what it plays with the music turned down to nothing).</summary>
        public const int NoMusic = 0;

        /// <summary>
        /// What plays here: nothing, unless a place names something (MusicID, or a track of
        /// the mod's). Whatever it is, it is in force over the game's own choice, which
        /// would be the Underworld's: to the game, anything this near a world's floor is the
        /// Underworld.
        /// </summary>
        public virtual int Music => NoMusic;

        /// <summary>
        /// The number of this place's layout: 1 to begin with. Raise it when
        /// <see cref="Build"/> changes and places already made should be laid out the new
        /// way. A place is kept in a file named for its layout (SubworldKeeper.PathFor), so a
        /// new number is a new file, made the next time the place is gone to; the old file is
        /// left where it lies, with whatever was in it.
        /// </summary>
        public virtual int Layout => 1;

        /// <summary>
        /// Lays the place out. Run once, when its file is first made, on a world already
        /// cleared and sized, away from the main thread: write tiles, and nothing else.
        /// </summary>
        public abstract void Build();

        // ---- what may be done here ---------------------------------------------------------

        /// <summary>
        /// The rules of subworlds hold here: nothing broken or built, nothing poured, no
        /// actuator fitted (SubworldRules). A place whose ground may be worked as a world's
        /// is says no; what it keeps of its own all the same is <see cref="KeepsTile"/>.
        /// </summary>
        public virtual bool Untouched => true;

        /// <summary>
        /// The tile here is one the place keeps of its own, whatever else may be done in it:
        /// never broken, swapped, hammered, fitted with an actuator or blown up. Asked only
        /// where the place is not <see cref="Untouched"/>, and not while the rules are lifted.
        /// </summary>
        public virtual bool KeepsTile(int x, int y) => false;

        /// <summary>The game spawns here, as it would anywhere (SubworldQuiet holds it back otherwise).</summary>
        public virtual bool Spawns => false;

        /// <summary>
        /// Time passes here: the clock, the weather, the moon, the world's slow changes and its
        /// events, as in any world. The clock is the world's: it comes in with whoever comes,
        /// and goes back with them (SubworldFlags), so that a night spent here is a night gone
        /// in the world. Where it stands still it stands at a quarter past eight in the morning,
        /// and going there and back is no time at all.
        /// </summary>
        public virtual bool ClockRuns => false;

        /// <summary>
        /// The game may take this place for the sea, as it takes the two ends of a world
        /// (Player.ZoneBeach, by WorldGen.oceanDepths), and draw the sea's backdrop and its map
        /// behind it. Otherwise it is never the sea, and nothing is drawn behind it but the sky
        /// (SubworldScene).
        /// </summary>
        public virtual bool IsSea => false;

        /// <summary>
        /// The place's water is laid still, and the game's quick settling of water is not run in
        /// it (Liquid.QuickWater: on every loading of a world, WorldFile.LoadWorld, and when too
        /// much water is moving at once, Liquid.StartPanic). That settling looks along each cell
        /// of water's row, through the water, to the next solid tile (Liquid.SettleWaterAt), so it
        /// takes as long as the water is wide times as wide: across a sea thousands of columns
        /// wide, many minutes. The water still moves as the game moves it, cell by cell.
        /// </summary>
        public virtual bool StillWater => false;

        /// <summary>
        /// The row the game's surface line is on, and its rock line (SubworldKeeper.Make).
        /// By default both under anything that can be seen, 35 and 28 rows up from the bottom
        /// edge: the game lights whatever is over the surface line as open air under the sky.
        /// </summary>
        public virtual double SurfaceLine => Height - 35;
        public virtual double RockLine => Height - 28;

        protected sealed override void Register()
        {
            SubworldKeeper.Add(this);
        }

        // ---- for laying one out ------------------------------------------------------------

        /// <summary>A block of one kind of tile, corners included.</summary>
        protected static void Fill(int left, int top, int right, int bottom, ushort type)
        {
            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    tile.ClearTile();
                    tile.HasTile = true;
                    tile.TileType = type;
                }
            }
        }

        /// <summary>
        /// A thing of several tiles, set down whole with its top left at (<paramref name="left"/>,
        /// <paramref name="top"/>): each of its tiles a frame of its sheet, 18 pixels apart,
        /// as the game lays such sheets out.
        /// </summary>
        protected static void Stand(ushort type, int left, int top, int wide, int tall)
        {
            for (int x = 0; x < wide; x++)
            {
                for (int y = 0; y < tall; y++)
                {
                    if (!WorldGen.InWorld(left + x, top + y))
                        continue;

                    Tile tile = Main.tile[left + x, top + y];

                    tile.ClearTile();
                    tile.HasTile = true;
                    tile.TileType = type;
                    tile.TileFrameX = (short)(x * 18);
                    tile.TileFrameY = (short)(y * 18);
                }
            }
        }
    }
}
