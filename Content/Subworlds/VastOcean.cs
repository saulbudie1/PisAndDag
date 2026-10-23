using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles;
using PisAndDag.Content.Tiles.Lighthouse;
using PisAndDag.Content.Tiles.SeaFloor;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Vast Ocean, by request: "a subworld where it is just the ocean, take the thrice as
    /// deep as the deepest place of the ocean in normal world, with a platform on the
    /// surface, water un buckettable, and sendiments are sand, shelfishes and the ambient
    /// objects ... Sharks and sea creatures spawn, can summon duke fishron. All functions
    /// that can be done at the beach can be done here." Gone to through the waygate on the
    /// beach (Waygate, BeachWaygate), and come back from through the one on its platform.
    ///
    /// HOW DEEP. The game digs its seas in the beach pass (WorldGen's AddGenPasses, the
    /// lambda that calls WorldGen.TuneOceanDepth, 1.4.4): from where the water starts, 220
    /// to 259 columns in from the edge, or 275 on one side of the world, a column at a time
    /// toward the edge, each a little deeper than the last by a roll of 10 to 19 times a step
    /// that shrinks with the count, and one world in four has one sea sloped the other way,
    /// shallow at first and steep at the end ("Florida"). Water runs from the surface row to
    /// three quarters of the depth less three. At the deepest column anybody can reach (41
    /// in from the edge), with every roll at 19, on the 275 side and sloped the steep way,
    /// that is 149 rows of water: the deepest the game can make its sea. (In twenty thousand
    /// worlds rolled as the game rolls them the deepest was 119, and in half of them 69 or
    /// less.) It was made thrice that, 447; asked for again ("Make it 12x wider and 4x deeper
    /// than now, that means it is 12x deeper than deepest part of the normal world beach",
    /// then "i mean 18x wider"), twelve times 149, 1788; and again ("Also make the ocean 1.5x as
    /// deep, keep the 12 sections tho"), half as deep again in its twelve sections: half again
    /// 1788 is 2682, twelve sections of 223 and a half rows, and a section is whole rows, so
    /// twelve of 224 (six rows past the half). From this sea's surface row to its sand, at its
    /// deepest, is <see cref="Deepest"/>, 2688 rows.
    ///
    /// HOW IT IS LAID OUT. 13662 columns by 3607 rows: eighteen times the 759 it was made with,
    /// which was the widest a place could be with all of it the sea by the game's own count.
    /// The game calls a column the sea only within 380 of either end (WorldGen.oceanDepths, and
    /// fishing), and Duke Fishron loses his temper over a player more than 400 from both
    /// (NPC.AI_069_DukeFishron); here every one of those counts is answered with the whole
    /// width (ExtraLargeOcean, SubworldSea), so all of it is the sea still. The water's surface
    /// is row <see cref="Surface"/>, half full as the game leaves its own; over it, 358 rows of
    /// open sky to the top that can be seen (twice the 179 it had, by request: "raise the roof
    /// by double so we can have some head space for a boss fight"); under it the water, and
    /// under that the floor (VastOceanFloor): long low dunes of sand, their lowest
    /// <see cref="Deepest"/> rows down and nowhere more than <see cref="Dunes"/> rows over that,
    /// hills of sediment over them, five ranges of mountains standing up out of it into the
    /// 10th and the 9th sections, and a trench going down <see cref="TrenchDeep"/> rows under
    /// the dunes' lowest; and the ground under it all, <see cref="Sediment"/> rows of it under
    /// the dunes' lowest, to the bottom (by request: "Maybe deepen the sendiment too"; it was
    /// 73). A world this size is past the game's own grid of tiles for a Large world; it is
    /// grown to fit as the place is made or loaded (ExtraLargeWorld).
    ///
    /// THE GAME'S LINES. The surface line is 70 rows under the water's surface, so the water
    /// near the top is the surface to the game and the rest is under it: a bobber on the
    /// surface fishes the sea (Projectile.FishingCheck: under the surface line and in the sea's
    /// columns), and Duke Fishron fights a player near the surface and rages at one gone down
    /// past it, as he does in a world's own sea. The rock line is past the bottom of the world,
    /// so that the game's line for the sea (oceanDepths: halfway between the two, and forty
    /// more) falls under the lowest row that can be seen: the whole of it, to the trench's foot,
    /// is the sea, and the game spawns its own sea's creatures on its floor. Its line for the
    /// Underworld (two hundred rows over the bottom) is in the trench, a hundred and ten rows
    /// over its foot; water under it does not dry up in a subworld, and none of the game's
    /// Underworld is drawn or lit there (SubworldScene). And the game turns its sky to space toward a
    /// world's top, by rows counted from it, more of them the wider the world
    /// (Main.UpdateAtmosphereTransparencyToSkyColor: 65, and ten times the square of the width
    /// over 4200; here 171), and back to the sky over a fifth of the surface line's rows (94):
    /// with the middle of the screen over row 171 the sky is the game's space, black, its stars
    /// out by day; from row 265 down, the 134 rows over the water, it is the open sky. A player
    /// weighs the same at any height in it (SubworldRules).
    ///
    /// THE PLATFORM, in the middle, on the water: a deck of old stone two rows over the
    /// surface and forty-two long, its body under the water ragged and green with moss at
    /// the waterline, four piers of brick going down into the dark, the deepest 52 rows, with
    /// coral grown on them; on the deck a dais of marble two steps high with the waygate on
    /// it, and two broken columns. The place keeps all of it (<see cref="KeepsTile"/>): none
    /// of it can be broken. Whoever dies here comes back on the deck.
    ///
    /// THE ISLAND, from 982 to 1312 columns left of the middle, by request ("1000- 1300 tiles to
    /// the left is an island floating in the sea, around 300 tile wide, having a large light house
    /// on the rock"): a mass of rock floating in the sea, its underside hanging into the water in
    /// spikes, two chains going down from it to anchors on the floor; on it a lighthouse of red
    /// and white brick raised on a plinth of white stone, five storeys and every one fitted out,
    /// its lens turning in the lantern and its beams over the sea at night; its oil house; a
    /// fisherman's house on stilts, a pier, two boats (VastOceanIsland,
    /// GraceBuilder.BuildLighthouseIsland, LighthouseLens, LighthouseBeam). None of it is kept but
    /// the lens, which cannot be broken.
    ///
    /// THE FLOOR: its ground, its caves, its trench, its pillars, its odd things and its life
    /// are VastOceanFloor's, whose notes say what they are. None of its blocks can be broken
    /// (<see cref="KeepsTile"/>). In its sand, drifts of shell (Shell
    /// Pile); on its sediment, shells and starfish lying on it (under the depth at which shells
    /// dissolve, starfish only); and on its sediment and its rock, the game's own piles of what
    /// the sea takes: stones, bones and skulls, wreckage of wood, the broken marble and gold of
    /// some fallen hall. No seaweed and none of the beach's coral (VastOceanBuild).
    ///
    /// THE DEPTHS, by request ("divide the depth into 12 section"): twelve sections of
    /// <see cref="SectionRows"/> rows each (224; 149, the deepest the game can make its own sea,
    /// until it was made half as deep again), counted down from the surface row. Asked for,
    /// section by section ("after section 2" read as from the third on, and so for the rest):
    ///   - from the 3rd, a light's spread is halved; from the 5th, a light lights itself and
    ///     nothing round it, and it is pitch dark (VastOceanLight);
    ///   - the water's colour falls to dark navy over the first four, and to black over the 5th
    ///     and 6th; the sun's rays reach down through the first three, the moon's a third as far;
    ///     now and then waves along the surface (VastOceanSky);
    ///   - from the 7th, a murk over the screen that is very hard to see through, and sound and
    ///     music as heard in a dive; from the 8th, silence (VastOceanSky, VastOceanSound);
    ///   - from the 7th nothing spawns (VastOceanSea): the 7th to the 9th are a biome of their
    ///     own, the Forevernight Zone, the 10th to the 12th another, the Abyssal Trench (read as
    ///     the two not sharing the 10th);
    ///   - from the 8th, what anybody puts down there shakes and breaks in 1.3 seconds
    ///     (VastOceanPressure);
    ///   - from the 6th, the water's weight on whoever is in it: life taken every second, more
    ///     the deeper, and from the 9th imploded, but as deep as what they have on holds against
    ///     it (VastOceanWeight); and a Gills Potion wears off faster the deeper (VastOceanGills);
    ///   - from the 10th, long low songs of whales and rocks knocking far under, heard through the
    ///     silence (VastOceanAmbience).
    ///
    /// WHAT IS DONE HERE, as on a beach (the members under "what may be done here"): the
    /// ground may be worked (but for the floor's own blocks from the 8th section down, which
    /// the place keeps: <see cref="KeepsTile"/>), the game spawns, the clock runs and is the
    /// world's, and the game takes it for the sea. What is the place's own besides is in VastOceanSea (the water
    /// that cannot be carried off, the creatures of open water) and VastOceanBiome (the
    /// biome, its light and its shaders), and the depths' (above).
    /// </summary>
    public class VastOcean : Subworld
    {
        /// <summary>Eighteen times 759, the width it was made with (the notes over this class).</summary>
        public const int Wide = 13662;

        /// <summary>The sky, the water to the dunes' lowest, and the ground under that.</summary>
        public const int High = Surface + Deepest + Sediment;

        /// <summary>
        /// The ground under the dunes' lowest point, to the bottom of the world: room for the
        /// trench (<see cref="TrenchDeep"/>), its foot ninety rows from the bottom and forty-seven
        /// over the lowest row that can be seen (<see cref="Subworld.FarMargin"/>).
        /// </summary>
        public const int Sediment = TrenchDeep + 90;

        /// <summary>How far the trench goes down under the dunes' lowest point (VastOceanFloor).</summary>
        public const int TrenchDeep = 430;

        /// <summary>
        /// The row the water's surface is in: the first row of water, half full. Over it 358 rows
        /// of sky can be seen, from the first row the game shows (<see cref="Subworld.Margin"/>).
        /// </summary>
        public const int Surface = Margin + 358;

        /// <summary>From the surface row to the first row of sand at the dunes' lowest (the trench and the holes in the floor go deeper): twelve sections of 224 rows, half as deep again as twelve times the deepest the game can make its sea (the notes over this class).</summary>
        public const int Deepest = 2688;

        /// <summary>The floor's top at its lowest; the dunes rise no more than <see cref="Dunes"/> rows over it.</summary>
        public const int FloorLowest = Surface + Deepest;
        public const int Dunes = 24;

        /// <summary>The middle column: the platform and the waygate are on it.</summary>
        public const int Middle = Wide / 2;

        /// <summary>
        /// The game's surface line, 70 rows under the water's surface; its rock line past the
        /// bottom of the world, where it puts the game's line for the sea (oceanDepths: halfway
        /// between the two, and forty more) forty rows over the bottom.
        /// </summary>
        public const int SurfaceLineRow = Surface + 70;
        public const int RockLineRow = 2 * (High - 80) - SurfaceLineRow;

        // ---- the depths ------------------------------------------------------------------------

        /// <summary>The sea's depth in twelve sections, each this many rows (224: half as many again as the deepest the game can make its own sea, 149, and a half row).</summary>
        public const int Sections = 12;
        public const int SectionRows = Deepest / Sections;

        /// <summary>The first row of each section, 1 to 12, counted down from the surface row.</summary>
        public static int SectionTop(int section) => Surface + (section - 1) * SectionRows;

        /// <summary>The section a row is in: 0 over the water, 1 to 12 in it, and 12 for the dunes' lowest and under (the trench).</summary>
        public static int SectionOf(float row) => row < Surface ? 0 : Math.Min(Sections, 1 + (int)((row - Surface) / SectionRows));

        /// <summary>From here down a light spreads half as far (the 3rd section).</summary>
        public const int HalvedLightFrom = Surface + 2 * SectionRows;

        /// <summary>From here down a light lights its own cell and no other, and it is pitch dark (the 5th).</summary>
        public const int OwnLightFrom = Surface + 4 * SectionRows;

        /// <summary>The water dark navy here (the end of the 4th), and black here (the end of the 6th).</summary>
        public const int NavyAt = Surface + 4 * SectionRows;
        public const int BlackAt = Surface + 6 * SectionRows;

        /// <summary>The sun's rays reach this far down from the surface (three sections), the moon's a third of it.</summary>
        public const int SunRayRows = 3 * SectionRows;
        public const int MoonRayRows = SunRayRows / 3;

        /// <summary>From here down the murk, and sound as in a dive; the Forevernight Zone; nothing spawns (the 7th).</summary>
        public const int MurkFrom = Surface + 6 * SectionRows;
        public const int ForevernightFrom = MurkFrom;
        public const int NoSpawnsFrom = MurkFrom;

        /// <summary>From here down silence, and what is put down breaks (the 8th).</summary>
        public const int QuietFrom = Surface + 7 * SectionRows;
        public const int CrumbleFrom = QuietFrom;

        /// <summary>From here down the Abyssal Trench (the 10th).</summary>
        public const int AbyssalTrenchFrom = Surface + 9 * SectionRows;

        /// <summary>The row a player's middle is in, in this place; -1 anywhere else.</summary>
        public static float RowOf(Player player) => SubworldKeeper.Current is VastOcean ? player.Center.Y / 16f : -1f;

        // ---- the platform ----------------------------------------------------------------------

        /// <summary>The deck: its top row two over the water's surface row, and this far either side of the middle.</summary>
        public const int DeckTop = Surface - 2;
        public const int DeckHalf = 20;

        /// <summary>The waygate's top left tile: on the dais, its two columns the middle and the one after it.</summary>
        public const int GateLeft = Middle;
        public const int GateTop = DeckTop - 2 - Waygate.Tall;

        /// <summary>
        /// The piers: the first of their three columns, from the middle, and how far each goes
        /// down under the deck's body. Laid either side of the line between the waygate's two
        /// columns, as everything on the platform is: a column this far left of it has its
        /// twin as far right.
        /// </summary>
        private static readonly int[] PierAt = { -15, -6, 5, 14 };
        private static readonly int[] PierDepth = { 34, 52, 46, 38 };
        private const int PierWide = 3;

        /// <summary>The deck's body: its lowest row, under the water.</summary>
        private const int BodyBottom = Surface + 4;

        /// <summary>The lowest row any pier can reach, its broken end and all.</summary>
        private const int PierLowest = BodyBottom + 52 + 2;

        /// <summary>Every tile the platform could have, for the keeping (<see cref="KeepsTile"/>).</summary>
        private static readonly Rectangle PlatformArea = new Rectangle(Middle - DeckHalf, DeckTop - 6, DeckHalf * 2 + 2, PierLowest - (DeckTop - 6) + 1);

        public override int Width => Wide;
        public override int Height => High;

        /// <summary>9: the Signal Pistol in the lighthouse's watch room (LighthouseTower). Before it: the ranges' caves deeper and a maze, with the eels' dens and rocks floating in their big rooms (VastOceanFloor.Caves), 8; a network of caves in each range, walled, with dripstone and the eels' crevices, 7; the lighthouse island (VastOceanIsland), 6; the floor of hills, mountains, caves and a trench, its ground deeper (VastOceanFloor), 5; half as deep again, its sections 224 rows, 4; twice the sky over it, 3; eighteen times as wide and four times as deep as the first, 2.</summary>
        public override int Layout => 9;

        public override Point Door => new Point(GateLeft, GateTop);

        /// <summary>On the deck, left of the dais, clear of it and of the broken column.</summary>
        public override Point Spawn => new Point(Middle - 6, DeckTop);

        /// <summary>The game's own for the sea, by day and by night; nothing at all from the 8th section down.</summary>
        public override int Music => !Main.gameMenu && RowOf(Main.LocalPlayer) >= QuietFrom ? NoMusic : Main.dayTime ? MusicID.Ocean : MusicID.OceanNight;

        public override bool Untouched => false;

        public override bool Spawns => true;

        public override bool ClockRuns => true;

        public override bool IsSea => true;

        /// <summary>Its water is laid still: the game's quick settling of it would take many minutes (Subworld.StillWater).</summary>
        public override bool StillWater => true;

        public override double SurfaceLine => SurfaceLineRow;
        public override double RockLine => RockLineRow;

        /// <summary>
        /// What the place keeps of its own, that nobody can break: all of the platform; and, by
        /// request ("Blocks on the sediment cannot be broken"), from the 8th section down
        /// (<see cref="GroundKeptFrom"/>: the floor, its mountains, caves and trench all lie
        /// under it) every block the place was made with: a solid one not put down by hand
        /// (VastOceanPressure.PutDownAt), which crumbles there and must be broken to. What lies
        /// on the floor and is not a block (its life, shells, piles, dripstone, the island's
        /// chains) may be broken as before, and the walls.
        /// </summary>
        public override bool KeepsTile(int x, int y)
        {
            if (PlatformArea.Contains(x, y) && PlatformTile(x, y) != 0)
                return true;

            if (y < GroundKeptFrom || !WorldGen.InWorld(x, y))
                return false;

            Tile tile = Main.tile[x, y];

            return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && !Biomes.VastOceanPressure.PutDownAt(x, y);
        }

        /// <summary>From here down the place's own blocks are kept (<see cref="KeepsTile"/>): the 8th section, where whatever is put down crumbles.</summary>
        public const int GroundKeptFrom = CrumbleFrom;

        // ---- the floor -------------------------------------------------------------------------

        /// <summary>The floor's top row in this column: <see cref="FloorLowest"/> less the dunes, which are this world's own (its seed).</summary>
        public static int FloorAt(int x, Dune dune) => FloorLowest - dune.Height(x);

        /// <summary>
        /// The dunes: four long swells of their own lengths and phases, added and brought down
        /// so that the lowest point of the whole floor is exactly <see cref="FloorLowest"/>.
        /// </summary>
        public sealed class Dune
        {
            private readonly float[] length = new float[4];
            private readonly float[] phase = new float[4];
            private readonly float[] weight = { 1f, 0.55f, 0.3f, 0.15f };
            private readonly float low, high;

            public Dune(int seed)
            {
                var rand = new UnifiedRandom(seed);
                float[] lengths = { 260f, 120f, 55f, 23f };

                for (int i = 0; i < 4; i++)
                {
                    length[i] = lengths[i] * (0.8f + 0.4f * rand.NextFloat());
                    phase[i] = rand.NextFloat() * MathHelper.TwoPi;
                }

                low = float.MaxValue;
                high = float.MinValue;

                for (int x = 0; x < Wide; x++)
                {
                    float v = Raw(x);
                    low = Math.Min(low, v);
                    high = Math.Max(high, v);
                }
            }

            private float Raw(int x)
            {
                float v = 0f;

                for (int i = 0; i < 4; i++)
                    v += weight[i] * (float)Math.Sin(x * MathHelper.TwoPi / length[i] + phase[i]);

                return v;
            }

            /// <summary>Rows over the lowest point, 0 to <see cref="Dunes"/>.</summary>
            public int Height(int x)
            {
                float v = (Raw(x) - low) / Math.Max(0.001f, high - low);

                return (int)Math.Round(v * Dunes);
            }
        }

        // ---- the platform, cell by cell ---------------------------------------------------------

        /// <summary>
        /// The tile the platform has here, or 0 for none. One rule for laying it out and for
        /// keeping it, and the same in every world: it has nothing of the seed in it.
        /// </summary>
        public static ushort PlatformTile(int x, int y)
        {
            int dx = x - Middle;

            // The deck: two rows of old stone, the top one slab.
            if (y == DeckTop && Across(dx, DeckHalf))
                return TileID.StoneSlab;

            if (y == DeckTop + 1 && Across(dx, DeckHalf))
                return Weathered(x, y, TileID.GrayBrick);

            // The piers, from under the deck down into the dark, their ends broken off.
            if (y > DeckTop + 1)
            {
                for (int p = 0; p < PierAt.Length; p++)
                {
                    if (dx < PierAt[p] || dx >= PierAt[p] + PierWide)
                        continue;

                    int end = BodyBottom + PierDepth[p];

                    if (y > end + 2)
                        break;

                    // The last rows broken, more of each the lower it is.
                    if (y >= end && Hash(x, y * 3) < 0.5f + 0.25f * (y - end))
                        break;

                    // A band of slab every seven rows; coral grown over the brick, more the deeper.
                    if ((y - BodyBottom) % 7 == 0)
                        return TileID.StoneSlab;

                    float depth = (y - Surface) / (float)(PierLowest - Surface);

                    if (y > Surface + 3 && Hash(x * 5, y) < 0.06f + 0.4f * depth)
                        return TileID.Coralstone;

                    return Weathered(x, y, TileID.GrayBrick);
                }
            }

            // Its body, under the water: narrowing a column or two a row, ragged at the bottom.
            if (y > DeckTop + 1 && y <= BodyBottom)
            {
                int row = y - (DeckTop + 1);
                int half = DeckHalf - (row <= 2 ? row : 2 + (row - 2) * 2);

                if (y == BodyBottom && Hash(x, y) < 0.35f)
                    return 0;

                if (Across(dx, half))
                    return Weathered(x, y, y <= Surface + 1 ? TileID.GrayBrick : TileID.StoneSlab);
            }

            // The dais, two steps of marble, the waygate on the upper one.
            if (y == DeckTop - 1 && Across(dx, 3))
                return TileID.MarbleBlock;

            if (y == DeckTop - 2 && Across(dx, 2))
                return TileID.MarbleBlock;

            // Two broken columns on the deck, of marble on a foot of slab: the left one the taller.
            if ((dx == -11 && y >= DeckTop - 6 || dx == -10 && y >= DeckTop - 5) && y < DeckTop)
                return y == DeckTop - 1 ? TileID.StoneSlab : TileID.MarbleBlock;

            if ((dx == 12 && y >= DeckTop - 4 || dx == 11 && y >= DeckTop - 3) && y < DeckTop)
                return y == DeckTop - 1 ? TileID.StoneSlab : TileID.MarbleBlock;

            return 0;
        }

        /// <summary>This far either side of the line between the waygate's two columns: from the middle column less it to the middle column plus it plus one.</summary>
        private static bool Across(int dx, int half) => dx >= -half && dx <= half + 1;

        /// <summary>Brick, green with moss at the waterline and a little way either side of it.</summary>
        private static ushort Weathered(int x, int y, ushort type)
        {
            int fromWater = Math.Abs(y - Surface);

            if (fromWater <= 2 && Hash(x * 3, y * 7) < 0.55f - 0.15f * fromWater)
                return TileID.GreenMossBrick;

            return type;
        }

        /// <summary>A number from 0 to 1 that is always the same for the same cell.</summary>
        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // ---- laying it out ----------------------------------------------------------------------

        public override void Build()
        {
            int seed = Main.ActiveWorldFileData?.Seed ?? 0;

            VastOceanBuild.Lay(new Dune(seed), seed, new UnifiedRandom(seed ^ 0x5EA5EA), (ushort)ModContent.TileType<Waygate>(), SeaLifeTile.Plant,
                (ushort)ModContent.TileType<LighthouseLens>());
        }
    }
}
