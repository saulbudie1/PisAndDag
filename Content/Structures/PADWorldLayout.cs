using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using log4net;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Somewhere for the passes to log to. A GenPass has no Mod of its own, and inside
    /// this namespace the name PisAndDag is the namespace rather than the class, so
    /// ModContent.GetInstance is more trouble than it is worth.
    /// </summary>
    public static class GenLog
    {
        public static ILog Log;
    }

    /// <summary>
    /// The plan in `PAD_World_Plan.md`, generated.
    ///
    /// Positions are written the way the plan writes them — ocean at 0, Grace at the far
    /// end — and every one of them goes out through <see cref="WorldMirror.X"/>, so half
    /// of all worlds come out the other way round.
    ///
    /// Nothing here is exact. Every edge is rolled inside a window, because a world that
    /// puts the snow at the same tile every time is a worse world than vanilla's, and
    /// the point of pinning the order was never to pin the map.
    /// </summary>
    public static class Layout
    {
        // The plan's numbers, ocean end first. These are the survey plate's bands and
        // they are the order the world is read in. See PAD_World_Plan.md §3.
        public const int OceanEnd = 760;
        public const int BallX = 950;
        public const int DungeonAt = 1575;
        public const int EvilStart = 2400;
        public const int EvilEnd = 3400;
        public const int DesertStart = 3400;
        public const int DesertEnd = 4700;
        /// <summary>
        /// Two thirds of what it was, taken off the spawn end. Six hundred and fifty
        /// tiles, starting five hundred and twenty-five east of where the player wakes
        /// up rather than two hundred — far enough that nobody opens their eyes in the
        /// cold. The jungle edge does not move, so the gap after it is still 565.
        /// </summary>
        public const int SnowStart = 6825;
        public const int SnowEnd = 7475;
        public const int JungleStart = 8040;
        public const int JungleEnd = 9440;
        public const int ClearedStart = 9440;
        public const int ClearedEnd = 10330;
        public const int MileStart = 10330;
        public const int MileEnd = 10800;
        public const int PlainStart = 10800;

        /// <summary>How far an edge is allowed to wander. Enough to matter, not enough to reorder.</summary>
        public const int Jitter = 120;

        public static int Wobble(int at, int by = Jitter) => at + WorldGen.genRand.Next(-by, by + 1);

        // ------------------------------------------------------------- the bands, placed

        public static (int left, int right) Evil => WorldMirror.Span(EvilStart, EvilEnd);

        public static (int left, int right) Desert => WorldMirror.Span(DesertStart, DesertEnd);

        public static (int left, int right) Cleared => WorldMirror.Span(ClearedStart, ClearedEnd);

        public static (int left, int right) Mile => WorldMirror.Span(MileStart, MileEnd);

        public static (int left, int right) Plain => WorldMirror.Span(PlainStart, Main.maxTilesX);

        /// <summary>The two spans nothing is allowed to grow on: the pitch, and the field.</summary>
        public static bool NothingGrowsHere(int x)
        {
            if (!ExtraLargeWorld.Active)
                return false;

            (int mileLeft, int mileRight) = Mile;
            (int plainLeft, int plainRight) = Plain;

            return (x >= mileLeft && x <= mileRight) || (x >= plainLeft && x <= plainRight);
        }

        // -------------------------------------------------------------- the second evil

        /// <summary>The evil the world was made with is on the surface; the Ball is the other one.</summary>
        public static bool BallIsCrimson => !WorldGen.crimson;

        public static ushort BallStone => (ushort)(BallIsCrimson ? TileID.Crimstone : TileID.Ebonstone);

        public static ushort BallWall => (ushort)(BallIsCrimson ? WallID.CrimstoneUnsafe : WallID.EbonstoneUnsafe);

        public static ushort BallGrass => (ushort)(BallIsCrimson ? TileID.CrimsonGrass : TileID.CorruptGrass);

        public static ushort BallVines => (ushort)(BallIsCrimson ? TileID.CrimsonVines : TileID.CorruptVines);

        public static ushort BallPlants => (ushort)(BallIsCrimson ? TileID.CrimsonPlants : TileID.CorruptPlants);

        public static ushort BallThorns => (ushort)(BallIsCrimson ? TileID.CrimsonThorns : TileID.CorruptThorns);

        public static ushort BallOre => (ushort)(BallIsCrimson ? TileID.Crimtane : TileID.Demonite);

        /// <summary>One of the four background walls of the biome, for the open parts of it.</summary>
        public static ushort BallCaveWall()
        {
            int first = BallIsCrimson ? WallID.CrimsonUnsafe1 : WallID.CorruptionUnsafe1;
            return (ushort)(first + WorldGen.genRand.Next(4));
        }

        // A Shadow Orb and a Crimson Heart are one tile with two frames, and the frame
        // is the only thing that tells them apart: WorldGen.CheckOrb reads
        // `frameX >= 36` off the tile and never looks at WorldGen.crimson, so one world
        // can hold both kinds and every orb answers for itself. TheBallPass.Orb writes
        // the frame directly; nothing here has to lie about the world's evil.

        // ------------------------------------------------------------------- the ground

        /// <summary>
        /// The first solid tile from the sky down, or -1 if this column has none.
        ///
        /// The scan starts at the highest ground anywhere in the world, which vanilla
        /// records while it is laying the terrain down. Starting there rather than at
        /// some fraction of worldSurface is what keeps floating islands out of the
        /// answer, and worldSurface itself is a long way under the tops of the hills.
        /// </summary>
        public static int Ground(int x)
        {
            if (x < 1 || x >= Main.maxTilesX - 1)
                return -1;

            int from = (int)GenVars.worldSurfaceLow - 6;

            if (from < 1 || from > Main.worldSurface)
                from = (int)(Main.worldSurface * 0.35);

            int to = Math.Min(Main.maxTilesY - 2, (int)Main.rockLayer);

            for (int y = Math.Max(1, from); y < to; y++)
            {
                Tile t = Main.tile[x, y];

                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y;
            }

            return -1;
        }

        /// <summary>
        /// The lowest a floating island is allowed to hang.
        ///
        /// Vanilla rolls an island's height and then takes Math.Min of it and
        /// worldSurfaceLow - 50, so fifty tiles of clear air over the highest ground in
        /// the world is a guarantee and the number is vanilla's rather than a guess.
        /// Anything at or below this line is ground; anything above it might be somebody
        /// else's island, and nothing here is entitled to clear it away.
        /// </summary>
        public static int SkyFloor
        {
            get
            {
                int low = (int)GenVars.worldSurfaceLow;

                if (low <= 0 || low > Main.worldSurface)
                    low = (int)(Main.worldSurface * 0.5);

                return low - 50;
            }
        }

        /// <summary>
        /// Anything standing on the ground that somebody or something put there: a fallen
        /// log, a pot, a pile, a tree, a herb, a campfire, a chest. Every one of those is
        /// a framed object, which is the one thing they all have in common and the only
        /// test that will still be right when a tile nobody has heard of turns up on the
        /// Mile.
        ///
        /// The Fallen Log is named as well, not because the test misses it but because it
        /// is the one that started this: three tiles by two of walkable timber sitting in
        /// a pocket of ash in the middle of a siege ground.
        /// </summary>
        public static bool Clutter(ushort type)
            => type == TileID.FallenLog || Main.tileFrameImportant[type];

        // ------------------------------------------------------ surfaces worth walking

        /// <summary>
        /// The two rules a made surface has to pass, applied to the heights before a
        /// single tile is laid.
        ///
        /// **One.** No column may sit more than a tile off either neighbour. Anything
        /// steeper is filled — the low side comes up, never the high side down, so the
        /// ground gains rather than loses. One sweep each way settles it exactly; it is
        /// run twice because a second look costs nothing and proves the first.
        ///
        /// **Two.** Every one-tile step that survives gets hammered. A column standing a
        /// tile proud of a neighbour is poured into a half block, so the walk across it
        /// goes 500, 500 and a half, 501 instead of 500, 501. That is the whole difference
        /// between a staircase and a hillside, and it is the same tool the player would
        /// use.
        /// </summary>
        public static void Level(int[] top)
        {
            for (int again = 0; again < 2; again++)
            {
                for (int i = 1; i < top.Length; i++)
                    top[i] = Math.Min(top[i], top[i - 1] + 1);

                for (int i = top.Length - 2; i >= 0; i--)
                    top[i] = Math.Min(top[i], top[i + 1] + 1);
            }
        }

        /// <summary>The second half of the rule above: the hammering, after the tiles are down.</summary>
        public static void Hammer(int left, int[] top)
        {
            for (int i = 0; i < top.Length; i++)
            {
                bool proud = (i > 0 && top[i] < top[i - 1]) || (i < top.Length - 1 && top[i] < top[i + 1]);

                if (!proud)
                    continue;

                int x = left + i;

                if (WorldGen.InWorld(x, top[i], 4) && WorldGen.SolidTile(x, top[i], false))
                    WorldGen.PoundTile(x, top[i]);
            }
        }

        // ------------------------------------------------------------ irregular circles

        /// <summary>
        /// Rolled once per world, so the same tile gives the same grain every time this
        /// generation asks and a different one next generation.
        /// </summary>
        public static int GrainSeed;

        /// <summary>
        /// A fixed, tile-sized wobble. Laid on top of a <see cref="Rim"/> so an edge is
        /// ragged at the scale of a pickaxe swing as well as at the scale of the shape,
        /// and so that the same tile is never counted two different ways by two passes.
        /// </summary>
        public static int Grain(int x, int y, int span)
        {
            unchecked
            {
                int h = (x * 73856093) ^ (y * 19349663) ^ GrainSeed;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return (int)((uint)h % (uint)(span * 2 + 1)) - span;
            }
        }
    }

    /// <summary>
    /// A closed radius profile that is not a circle.
    ///
    /// A handful of sine terms at whole-number frequencies, summed. Whole numbers are
    /// the point: the profile has to join up with itself after a full turn or the shape
    /// has a seam down one side where the first tile and the last tile disagree. The
    /// frequencies, amplitudes and phases are rolled per shape, so two of these next to
    /// each other are not the same lumpy circle twice.
    /// </summary>
    public sealed class Rim
    {
        private const int Steps = 1440;

        private readonly double[] radius;

        public Rim(double baseRadius, double rough, int mostLumps)
        {
            radius = new double[Steps];

            int terms = 3 + WorldGen.genRand.Next(4);
            double[] amplitude = new double[terms];
            double[] phase = new double[terms];
            int[] frequency = new int[terms];
            double total = 0.0;

            for (int k = 0; k < terms; k++)
            {
                frequency[k] = 1 + WorldGen.genRand.Next(Math.Max(1, mostLumps));
                amplitude[k] = 0.35 + WorldGen.genRand.NextDouble() * 1.3;
                phase[k] = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                total += amplitude[k];
            }

            // Held to the roughness it was asked for. Unnormalised, six terms that happen
            // to line up can put the surface through the middle of the shape, and the
            // callers all size their boxes and their contents off this number.
            for (int k = 0; k < terms; k++)
                amplitude[k] *= rough / total;

            for (int i = 0; i < Steps; i++)
            {
                double angle = i / (double)Steps * MathHelper.TwoPi;
                double sum = 0.0;

                for (int k = 0; k < terms; k++)
                    sum += amplitude[k] * Math.Sin(frequency[k] * angle + phase[k]);

                radius[i] = baseRadius * (1.0 + sum);
            }
        }

        /// <summary>The radius of this shape in the given direction. Angle in radians, any sign.</summary>
        public double At(double angle)
        {
            double turns = angle / MathHelper.TwoPi;
            turns -= Math.Floor(turns);

            int i = (int)(turns * Steps);

            if (i < 0)
                i = 0;
            if (i >= Steps)
                i = Steps - 1;

            return radius[i];
        }

        /// <summary>The radius toward a point, which is what the tile loops actually want.</summary>
        public double Toward(double dx, double dy) => At(Math.Atan2(dy, dx));

        /// <summary>The widest this shape gets, for sizing a bounding box that will hold it.</summary>
        public double Widest
        {
            get
            {
                double most = 0.0;

                for (int i = 0; i < Steps; i++)
                {
                    if (radius[i] > most)
                        most = radius[i];
                }

                return most;
            }
        }
    }

    /// <summary>
    /// A ground line that is not a random walk.
    ///
    /// Three or four sine terms at long wavelengths, summed and normalised so the whole
    /// thing stays inside the rise it was asked for. The point is the *derivative*: with
    /// a swell of four tiles over a wavelength of three hundred, the height changes by at
    /// most a fortieth of a tile per column, so a one-tile step happens every forty
    /// columns and never twice in a row. A random walk of the same amplitude steps every
    /// single column, which is exactly what the choppy pitch was.
    ///
    /// Amplitude buys character; wavelength buys silk. They are separate dials here for
    /// that reason: the Grace pitch is four tiles over six hundred, the Wasted Mile is
    /// five over sixty.
    /// </summary>
    public sealed class Swell
    {
        private readonly double[] amplitude;
        private readonly double[] phase;
        private readonly double[] wavelength;

        public Swell(double rise, int shortest, int longest)
        {
            int terms = 3 + WorldGen.genRand.Next(2);
            amplitude = new double[terms];
            phase = new double[terms];
            wavelength = new double[terms];
            double total = 0.0;

            for (int k = 0; k < terms; k++)
            {
                amplitude[k] = 0.4 + WorldGen.genRand.NextDouble();
                phase[k] = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                wavelength[k] = shortest + WorldGen.genRand.NextDouble() * (longest - shortest);
                total += amplitude[k];
            }

            for (int k = 0; k < terms; k++)
                amplitude[k] *= rise / total;
        }

        /// <summary>How far the ground stands off its level at this column. Signed.</summary>
        public double At(double x)
        {
            double sum = 0.0;

            for (int k = 0; k < amplitude.Length; k++)
                sum += amplitude[k] * Math.Sin(x / wavelength[k] * MathHelper.TwoPi + phase[k]);

            return sum;
        }
    }

    /// <summary>
    /// Runs immediately after vanilla's Reset pass, which is where every horizontal
    /// decision in the world is made and therefore the only seam worth cutting into.
    /// Overwrites the origins, and kills the ocean on the Grace side.
    /// </summary>
    public class GraceLayoutPass : GenPass
    {
        /// <summary>
        /// How much beach the Grace side keeps. Not zero, and the comment where it is
        /// used explains why at length, because zero looks obviously correct and puts
        /// world generation through the end of the tile array.
        /// </summary>
        private const int EdgeSliver = 60;

        public GraceLayoutPass(string name, double weight) : base(name, weight) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Deciding which way round the world is";

            if (!ExtraLargeWorld.Active)
                return;

            Layout.GrainSeed = WorldGen.genRand.Next(1, 1 << 30);

            // The Dungeon sits at the ocean end, where vanilla likes it. Everything else
            // keys off dungeonSide -- including the Shimmer, which vanilla sends to the
            // opposite side and which therefore lands under Grace for free.
            GenVars.dungeonSide = WorldMirror.Flipped ? 1 : -1;
            GenVars.dungeonLocation = WorldMirror.X(Layout.Wobble(Layout.DungeonAt, 150));

            (int snowL, int snowR) = WorldMirror.Span(Layout.Wobble(Layout.SnowStart), Layout.Wobble(Layout.SnowEnd));
            GenVars.snowOriginLeft = snowL;
            GenVars.snowOriginRight = snowR;

            GenVars.jungleOriginX = WorldMirror.X(Layout.Wobble((Layout.JungleStart + Layout.JungleEnd) / 2));

            // Squeeze the Grace-side beach down to a sliver. It cannot go to zero.
            //
            // The Beaches pass clamps its start against these and then scans *down the
            // column one tile past it* to find the ground, before it ever checks whether
            // it has any columns to carve. leftBeachEnd = 0 puts that scan at x = -51 and
            // rightBeachStart = maxTilesX puts it at maxTilesX + 50; both walk off the
            // end of the tile array. A sliver keeps the arithmetic in bounds and leaves
            // ten columns of water at the very edge of the world, which GracePlainPass
            // buries an hour later without being asked.
            if (WorldMirror.Flipped)
                GenVars.leftBeachEnd = EdgeSliver;
            else
                GenVars.rightBeachStart = Main.maxTilesX - EdgeSliver;

            GenLog.Log?.Info(
                $"Layout: dungeon side {GenVars.dungeonSide}, snow {snowL}-{snowR}, jungle origin {GenVars.jungleOriginX}, " +
                $"ball is {(Layout.BallIsCrimson ? "crimson" : "corruption")}.");
        }
    }

    /// <summary>
    /// A vanilla pass, run between two of our own instructions.
    ///
    /// Two of vanilla's biomes pick their own x and then spend a while arguing with
    /// themselves about the result. Neither can be told where to go, but both can be
    /// told where not to, and the fields they consult are ordinary statics. So the way
    /// to put the desert on the beach side and the evil where the plate says it is, is
    /// to set those fields on the way into the pass and put them back on the way out.
    /// Nothing is rewritten and nothing else in the world ever sees the lie.
    /// </summary>
    public class BracketPass : GenPass
    {
        private readonly GenPass inner;
        private readonly Action open;
        private readonly Action close;

        public BracketPass(GenPass inner, Action open, Action close) : base(inner.Name, inner.Weight)
        {
            this.inner = inner;
            this.open = open;
            this.close = close;
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            open?.Invoke();

            try
            {
                inner.Apply(progress, config);
            }
            finally
            {
                close?.Invoke();
            }
        }
    }

    /// <summary>
    /// Where the biomes that place themselves are allowed to land.
    ///
    /// The plate puts the desert between the evil and the spawn forest, on the ocean
    /// side, and the evil between the first forest and the desert. Vanilla puts both of
    /// them wherever the dice fall, which in a world half again as wide as anything it
    /// has seen means either of them can land in the Wasted Mile or under the capital.
    /// </summary>
    public static class BiomeBands
    {
        /// <summary>Each true only while the vanilla pass it names is running.</summary>
        private static bool duringDunes;
        private static bool duringFullDesert;
        private static bool duringCorruption;

        /// <summary>The last x handed to the evil biome. For the log, and nothing else.</summary>
        private static int lastEvilX;

        // ------------------------------------------------------- the sand and the tombs

        public static void OpenDunes() => duringDunes = true;

        public static void CloseDunes() => duringDunes = false;

        /// <summary>
        /// **Dunes is not the desert.** It is `DunesBiome` — the loose sand and the
        /// pyramids — and it rolls a point, checks it against the jungle, the middle of
        /// the world and the snow, and rolls again if it does not like it. Handing it a
        /// point inside the desert band satisfies all three on the first roll, so the
        /// sand and the tombs end up in the desert where they belong and the pass never
        /// knows it was steered. Three other passes call this and are left alone: the
        /// bracket round Dunes is what says which call this is.
        /// </summary>
        public static Point OnTheBeachSide(On_WorldGen.orig_RandomWorldPoint_int_int_int_int orig,
            int a, int b, int c, int d)
        {
            Point point = orig(a, b, c, d);

            if (!duringDunes || !ExtraLargeWorld.Active)
                return point;

            (int left, int right) = Layout.Desert;
            int inset = Math.Min(160, (right - left) / 4);

            point.X = WorldGen.genRand.Next(left + inset, right - inset);
            return point;
        }

        // ------------------------------------------------------------------ the desert

        /// <summary>
        /// The desert — surface and underground, one object — is `DesertBiome`, and it is
        /// placed by **Full Desert**, not by Dunes. Nothing Reset decided steers it. It
        /// rolls its own x:
        ///
        ///     x = maxTilesX / 2 − dungeonSide × offset,  offset ≥ maxTilesX / 16
        ///
        /// which is to say vanilla deliberately throws the desert to the side of the world
        /// *opposite* the Dungeon. In this layout the Dungeon and the desert are both at
        /// the ocean end, so vanilla's own rule is exactly what was putting the desert on
        /// top of the jungle and the snow at the other end of the world.
        ///
        /// Note for later: `DesertBiome.Place` takes a StructureMap and only ever *adds*
        /// to it — it never asks `CanPlace`. A wall built in that map does nothing here.
        /// The one thing that decides whether a placement is taken is the terrain, so the
        /// honest lever is the origin itself, and that is what the hook rewrites.
        /// </summary>
        public static void OpenFullDesert() => duringFullDesert = true;

        public static void CloseFullDesert()
        {
            duringFullDesert = false;

            if (!ExtraLargeWorld.Active)
                return;

            Rectangle where = GenVars.UndergroundDesertLocation;
            (int left, int right) = Layout.Desert;

            GenLog.Log?.Info(
                $"Desert: x {where.X}-{where.X + where.Width}, y {where.Y}-{where.Y + where.Height} (band {left}-{right}).");
        }

        /// <summary>
        /// The pass keeps its own retry loop — if the ground it was handed will not take a
        /// desert it rolls again, and after two rounds it stops checking the ground at all
        /// — so every roll comes back through here and gets a fresh x inside the band.
        /// </summary>
        public static bool DesertInItsBand(Terraria.GameContent.Biomes.On_DesertBiome.orig_Place orig,
            Terraria.GameContent.Biomes.DesertBiome self, Point origin, StructureMap structures)
        {
            if (!duringFullDesert || !ExtraLargeWorld.Active)
                return orig(self, origin, structures);

            (int left, int right) = Layout.Desert;
            int inset = Math.Min(220, (right - left) / 4);

            origin.X = WorldGen.genRand.Next(left + inset, right - inset);
            return orig(self, origin, structures);
        }

        // ------------------------------------------------------------------ the shells

        /// <summary>
        /// Shell Piles reads the two marks the Beaches pass left on the sand, scans twenty
        /// tiles either side of each and ten above, and then walks up to a hundred and
        /// twenty-five tiles further out to drop the piles.
        ///
        /// On the Grace side there is no ocean — only the sixty-tile sliver that keeps the
        /// Beaches pass's own arithmetic in bounds — so the mark sits at the very edge of
        /// the world and that scan reads straight through the end of the tile array.
        /// Vanilla only walks each beach on a coin flip, which is why this took a few
        /// worlds to show itself.
        ///
        /// Nothing else in the game reads these four, so they are simply pushed far enough
        /// in from both edges that every index the pass can form is inside the world. Any
        /// shells that land on the sliver are buried by the Grace Plain twelve passes
        /// later without being asked.
        /// </summary>
        public static void OpenShellPiles()
        {
            if (!ExtraLargeWorld.Active)
                return;

            const int Edge = 170;   // 20 for the scan, 125 for the furthest pile, slack
            const int Sky = 70;     // 10 for the scan, 50 the pass subtracts afterwards

            GenVars.shellStartXLeft = Math.Clamp(GenVars.shellStartXLeft, Edge, Main.maxTilesX - Edge);
            GenVars.shellStartXRight = Math.Clamp(GenVars.shellStartXRight, Edge, Main.maxTilesX - Edge);
            GenVars.shellStartYLeft = Math.Clamp(GenVars.shellStartYLeft, Sky, Main.maxTilesY - Sky);
            GenVars.shellStartYRight = Math.Clamp(GenVars.shellStartYRight, Sky, Main.maxTilesY - Sky);
        }

        // -------------------------------------------------------------------- the evil

        public static void OpenCorruption() => duringCorruption = true;

        public static void CloseCorruption()
        {
            duringCorruption = false;

            if (!ExtraLargeWorld.Active)
                return;

            (int left, int right) = Layout.Evil;
            GenLog.Log?.Info($"Evil biome: rolled x {lastEvilX} (band {left}-{right}).");
        }

        /// <summary>
        /// The evil biome rolls where it goes with one call —
        /// <c>genRand.Next(500, Main.maxTilesX - 500)</c> — and then argues with the
        /// result: it clamps its own left and right ends against
        /// `GenVars.evilBiomeBeachAvoidance` and throws the roll away if it overlaps the
        /// Dungeon, the middle of the world, the underground desert, the snow or the
        /// jungle. So the roll is the only clean place to steer it.
        ///
        /// The first version of this leaned on those clamps instead, by setting
        /// `evilBiomeBeachAvoidance` to the band's near edge. That **crashed vanilla**,
        /// and the way it did is worth keeping written down: the two clamps are
        /// symmetric — `left = max(left, avoid)` and `right = min(right, maxTilesX −
        /// avoid)` — so a large avoidance and a roll near the far end of the world come
        /// out with **left greater than right**. Nothing downstream checks for that, and
        /// forty lines later the pass hands the pair to `Next(min, max)` and throws
        /// ArgumentOutOfRange from inside vanilla.
        ///
        /// This hook sees every two-argument roll in the game, so it is gated three ways:
        /// only while the Corruption pass is running, only in a Grand world, and only for
        /// the one argument pair that is the world-span roll and nothing else.
        /// </summary>
        public static int EvilInItsBand(On_UnifiedRandom.orig_Next_int_int orig,
            UnifiedRandom self, int minValue, int maxValue)
        {
            if (!duringCorruption
                || !ExtraLargeWorld.Active
                || (minValue != 500 && minValue != 1000)
                || maxValue != Main.maxTilesX - minValue)
            {
                return orig(self, minValue, maxValue);
            }

            (int left, int right) = Layout.Evil;
            int inset = Math.Min(180, (right - left) / 5);

            lastEvilX = orig(self, left + inset, right - inset);
            return lastEvilX;
        }

        // ------------------------------------------------------------------- the trees

        /// <summary>
        /// A living tree is a hundred tiles of trunk and root and it does not ask what
        /// was planned for the ground it lands on. Two spans are spoken for: the pitch
        /// Grace stands on, and the field in front of it that was cleared on purpose.
        /// </summary>
        public static bool NoGiantsHere(On_WorldGen.orig_GrowLivingTree orig, int i, int j, bool patchwork)
            => !Layout.NothingGrowsHere(i) && orig(i, j, patchwork);

        public static bool NoEpicsHere(On_WorldGen.orig_GrowEpicTree orig, int i, int j)
            => !Layout.NothingGrowsHere(i) && orig(i, j);
    }

    /// <summary>
    /// The ground Grace will stand on.
    ///
    /// The capital has no shape yet, so this builds the only part of it that is already
    /// decided: a pitch of level ground at the far end of the world, wide enough for a
    /// city and flat enough to put one on.
    ///
    /// It sits over the country rather than in it. The pass finds the highest natural
    /// ground within sight of the plain and puts the pitch well above that, then fills
    /// down from the pitch until it meets whatever was already there and stops. Nothing
    /// natural is cut into — a hill under the plain is still a hill, the pitch simply
    /// closes over the top of it — and the rim comes out as the face of a plateau
    /// instead of a line of laid blocks. The way up is at the inland end, and it is
    /// broken ground rather than a slope drawn with a ruler.
    /// </summary>
    public class GracePlainPass : GenPass
    {
        /// <summary>How far over the highest ground it can see the pitch sits.</summary>
        private const int Lift = 58;
        private const int LiftVary = 26;

        /// <summary>How far past the plain the pass looks for ground to stand over.</summary>
        private const int Inland = 340;

        /// <summary>The way up, at the inland end.</summary>
        private const int RampWidth = 230;

        /// <summary>
        /// How far the pitch rolls, and how slowly. Four tiles over three to nine hundred
        /// is about a fortieth of a tile per column: enough that the ground is not a
        /// table, gentle enough that a step happens once in forty columns and gets
        /// hammered flat when it does.
        /// </summary>
        private const double Swelling = 4.0;
        private const int SwellShortest = 320;
        private const int SwellLongest = 900;

        /// <summary>Headroom cleared above the pitch, so nothing hangs over it.</summary>
        private const int Headroom = 90;

        /// <summary>The pitch is never thinner than this, even sitting straight on a hilltop.</summary>
        private const int MinSkin = 16;

        /// <summary>The seaward end, where the ocean was cut off and the water has to go.</summary>
        private const int DryShore = 110;

        public GracePlainPass(string name, double weight) : base(name, weight) { }

        /// <summary>The level top of the pitch. Read by everything that builds on it.</summary>
        public static int PlainTop { get; private set; }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Raising the ground at the end of the world";

            if (!ExtraLargeWorld.Active)
                return;

            (int left, int right) = Layout.Plain;
            left = Math.Max(1, left);
            right = Math.Min(Main.maxTilesX - 2, right);

            bool rampOnLeft = !WorldMirror.Flipped;

            PlainTop = ChooseLevel(left, right, rampOnLeft);

            int footX = rampOnLeft ? Math.Max(1, left - 4) : Math.Min(Main.maxTilesX - 2, right + 4);
            int foot = Layout.Ground(footX);

            if (foot < 0)
                foot = (int)Main.worldSurface;

            // The way up is at whichever edge faces the rest of the world, and it is as
            // long as the climb needs rather than a fixed 230.
            //
            // A smoothstep's steepest point is one and a half times its average, so a ramp
            // has to be about 1.6 times the height it climbs or it will ask for more than
            // a tile per column somewhere in the middle. Level would then quietly stretch
            // it out across the pitch to obey the rule, and the flat ground would be eaten
            // from the inland end without anyone asking for it.
            int climb = Math.Abs(PlainTop - foot);
            int roomy = Math.Max(RampWidth, (right - left) / 3);
            int rampWide = Math.Clamp((int)(climb * 1.6), RampWidth, roomy);
            int rampLeft = rampOnLeft ? left : right - rampWide;
            int rampRight = rampOnLeft ? left + rampWide : right;

            // The whole surface is worked out before a tile is laid, because the two
            // rules that make it silky -- no step over a tile, and hammer the steps that
            // are left -- are rules about the *line*, not about any one column.
            Swell swell = new Swell(Swelling, SwellShortest, SwellLongest);
            int span = right - left + 1;
            int[] top = new int[span];

            for (int i = 0; i < span; i++)
            {
                int x = left + i;

                if (x >= rampLeft && x <= rampRight)
                {
                    // The way up. A smooth curve and nothing else: the steps that used to
                    // be rolled in here are what made it read as a staircase.
                    float along = (x - rampLeft) / (float)Math.Max(1, rampRight - rampLeft);
                    float up = rampOnLeft ? along : 1f - along;
                    top[i] = (int)Math.Round(MathHelper.Lerp(foot, PlainTop, Smooth(up)));
                }
                else
                {
                    top[i] = (int)Math.Round(PlainTop + swell.At(x));
                }

                top[i] = Math.Max(20, top[i]);
            }

            Layout.Level(top);

            for (int i = 0; i < span; i++)
            {
                int x = left + i;
                progress.Set(i / (float)Math.Max(1, span - 1));

                Clear(x, top[i]);
                Fill(x, top[i], left, right);
            }

            // Last, with every tile down and framed: pour the one-tile steps flat.
            Layout.Hammer(left, top);

            GenVars.structures.AddProtectedStructure(
                new Rectangle(left, Math.Max(0, PlainTop - Headroom), right - left,
                    Math.Max(40, (int)Main.rockLayer - PlainTop + Headroom)), 6);

            GenLog.Log?.Info(
                $"Grace plain: x {left}-{right}, level at y {PlainTop}, the ground it stands on was y {foot}, ramp {rampLeft}-{rampRight} over a climb of {climb}.");
        }

        /// <summary>
        /// The one number the whole quarter hangs off. Taken from the ground rather than
        /// from worldSurface, because worldSurface is where the background stops being
        /// sky and sits well under the tops of the hills.
        /// </summary>
        private static int ChooseLevel(int left, int right, bool rampOnLeft)
        {
            int from = rampOnLeft ? Math.Max(1, left - Inland) : left;
            int to = rampOnLeft ? right : Math.Min(Main.maxTilesX - 2, right + Inland);

            int highest = int.MaxValue;

            for (int x = from; x <= to; x += 3)
            {
                int ground = Layout.Ground(x);

                if (ground > 0 && ground < highest)
                    highest = ground;
            }

            if (highest == int.MaxValue)
                highest = (int)Main.worldSurface;

            int level = highest - (Lift + WorldGen.genRand.Next(LiftVary));

            // Over the hills, and still under the floating islands. Ten tiles of daylight
            // between the pitch and the lowest anything can hang is enough that the
            // silhouette reads, and it means the pitch can never be built into one.
            level = Math.Min(level, highest - 24);
            level = Math.Max(level, Layout.SkyFloor + 10);
            level = Math.Max(level, (int)(Main.worldSurface * 0.40));

            return level;
        }

        /// <summary>Nothing hangs over the pitch. Whatever was up there was not planned.</summary>
        private static void Clear(int x, int top)
        {
            int from = Math.Max(1, top - Headroom);
            int sky = Layout.SkyFloor + 5;

            if (from < sky)
                from = sky;

            for (int y = from; y < top; y++)
            {
                Tile above = Main.tile[x, y];
                above.HasTile = false;
                above.LiquidAmount = 0;
                above.WallType = 0;
            }
        }

        /// <summary>
        /// Down from the pitch until it meets ground, and no further. The country under
        /// the plain keeps its caves, its ore and its shape; all this adds is the lid.
        /// </summary>
        private static void Fill(int x, int top, int left, int right)
        {
            // Only the end that faces the edge of the world, where the beach was cut off
            // and there is water with nowhere to go. The inland end has caves under it
            // and they are not ours to fill.
            bool seaward = WorldMirror.Flipped ? x < left + DryShore : x > right - DryShore;

            int limit = seaward
                ? (int)Main.rockLayer + 10                       // the cut-off ocean goes under here
                : (int)Main.worldSurface + 30;

            limit = Math.Min(limit, Main.maxTilesY - 4);

            int bottom = Math.Min(limit, top + MinSkin);

            if (seaward)
            {
                bottom = limit;
            }
            else
            {
                while (bottom < limit && !Standing(x, bottom))
                    bottom++;
            }

            int grassEnds = top + 1 + Layout.Grain(x, 2, 1);
            int dirtEnds = (int)Main.rockLayer - WorldGen.genRand.Next(0, 8);

            for (int y = top; y <= bottom; y++)
            {
                if (!WorldGen.InWorld(x, y, 3))
                    continue;

                Tile t = Main.tile[x, y];
                t.HasTile = true;
                t.LiquidAmount = 0;
                t.Slope = SlopeType.Solid;
                t.IsHalfBlock = false;

                if (y <= grassEnds)
                    t.TileType = (ushort)TileID.Grass;
                else if (y < dirtEnds)
                    t.TileType = (ushort)(WorldGen.genRand.NextBool(22) ? TileID.ClayBlock : TileID.Dirt);
                else
                    t.TileType = (ushort)TileID.Stone;

                // Invisible until somebody digs into it, and wrong by its absence the
                // first time a shaft is sunk through the pitch.
                if (y > top + 5)
                    t.WallType = (ushort)(y < Main.rockLayer ? WallID.DirtUnsafe : WallID.Stone);

                WorldGen.SquareTileFrame(x, y, true);
            }

            // Standing water left under the new lid would sit there for the rest of the
            // world's life.
            for (int y = bottom + 1; y <= Math.Min(limit + 6, Main.maxTilesY - 3); y++)
            {
                Tile t = Main.tile[x, y];

                if (t.LiquidAmount != 0 && !t.HasTile)
                    t.LiquidAmount = 0;
            }
        }

        /// <summary>Ground the pitch can rest on: solid, and not the surface of some water.</summary>
        private static bool Standing(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 4))
                return false;

            Tile t = Main.tile[x, y];
            return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType] && t.LiquidAmount == 0;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }

    /// <summary>
    /// The Wasted Mile: the ground the siege was fought from.
    ///
    /// No engines yet and no facility yet — that is a later pass and a later plan. What
    /// it is now is the surface: ash instead of soil, churned into ridges rather than
    /// hills, and bone left lying in it. Nothing about this is grown. It is the part of
    /// the plain that was worked on.
    /// </summary>
    public class WastedMilePass : GenPass
    {
        /// <summary>How far down the ash goes before ordinary ground resumes.</summary>
        private const int AshDepth = 26;

        /// <summary>
        /// The ridges, at three quarters of what they were. Seven tiles of random walk
        /// came out as scree; five tiles of swell over sixty to a hundred and eighty
        /// comes out as ground that has been worked and then left. It is still the
        /// roughest surface in the world — the pitch next door rolls four tiles over six
        /// hundred — but it is a surface rather than a set of steps.
        /// </summary>
        private const double Rough = 5.0;
        private const int RoughShortest = 55;
        private const int RoughLongest = 180;

        public WastedMilePass(string name, double weight) : base(name, weight) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Burning the ground in front of the wall";

            if (!ExtraLargeWorld.Active)
                return;

            (int left, int right) = WorldMirror.Span(Layout.Wobble(Layout.MileStart, 60), Layout.MileEnd);

            // The ridge line first, the whole way across, so it can be held to the
            // step-of-one rule before any ash is laid.
            Swell swell = new Swell(Rough, RoughShortest, RoughLongest);
            int span = right - left + 1;
            int[] ridge = new int[span];
            bool[] ground = new bool[span];
            int last = (int)Main.worldSurface;

            for (int i = 0; i < span; i++)
            {
                int found = Layout.Ground(left + i);
                ground[i] = found > 0;

                if (ground[i])
                    last = found;

                ridge[i] = Math.Max(20, (int)Math.Round(last + swell.At(left + i)));
            }

            Layout.Level(ridge);

            for (int i = 0; i < span; i++)
            {
                int x = left + i;
                progress.Set(i / (float)Math.Max(1, span - 1));

                if (!ground[i])
                    continue;

                // Whatever the forest left standing here goes before the ash does: a
                // fallen log buried three tiles down in a siege ground is the one thing
                // in the Mile that was nobody's decision.
                Sweep(x, ridge[i]);

                for (int y = ridge[i]; y < ridge[i] + AshDepth; y++)
                {
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    Tile t = Main.tile[x, y];
                    t.HasTile = true;
                    t.TileType = (ushort)TileID.Ash;
                    t.LiquidAmount = 0;
                    t.Slope = SlopeType.Solid;
                    t.IsHalfBlock = false;

                    if (y > ridge[i] + 3 && t.WallType != 0)
                        t.WallType = (ushort)WallID.Bone;

                    WorldGen.SquareTileFrame(x, y, true);
                }

                // Anything the churn left hanging above it goes.
                for (int y = Math.Max(1, ridge[i] - 30); y < ridge[i]; y++)
                {
                    Tile t = Main.tile[x, y];
                    t.HasTile = false;
                    t.WallType = 0;
                    t.LiquidAmount = 0;
                }
            }

            Layout.Hammer(left, ridge);

            // Bones after the hammering, so they lie on the finished ground rather than
            // on a surface that is about to move under them.
            for (int i = 0; i < span; i++)
            {
                if (ground[i])
                    Bones(left + i, ridge[i]);
            }

            GenVars.structures.AddProtectedStructure(
                new Rectangle(left, (int)Main.worldSurface - 80, right - left, 140), 4);
        }

        /// <summary>
        /// What is left in the ground afterwards.
        ///
        /// **Lying down, not standing up.** Three hundred years of weather takes everything
        /// upright off a field; what is left is long bone lying along the ground, half
        /// sunk, following whatever the ash did underneath it. So a run of three to ten
        /// tiles laid flat on the ridge, dipping into it where the ridge dips, and a few
        /// short ones broken off the ends. Nothing is planted upright any more.
        /// </summary>
        private static void Bones(int x, int ridge)
        {
            // Surface returns the AIR tile above the ash, not the ash. Every line below
            // used it as though it were the ground and every bone in the field was
            // therefore hanging one to two tiles over it. Name the two apart so the
            // mistake cannot be made twice.

            // A long bone, laid INTO the ground rather than onto it. A bone that has been
            // here three hundred years is not resting on the field, it is part of it —
            // the ash has come up around it — so it takes the first layer, or the second
            // where it has sunk further.
            if (WorldGen.genRand.NextBool(70))
            {
                int run = WorldGen.genRand.Next(3, 9);
                int depth = WorldGen.genRand.NextBool(2) ? 0 : 1;

                for (int i = 0; i < run; i++)
                {
                    // Follow the ground rather than bridging over it, so it reads as
                    // lying there rather than propped.
                    int air = Surface(x + i, ridge);

                    if (air < 0)
                        break;

                    Put(x + i, air + 1 + depth, TileID.BoneBlock);
                }
            }

            // Half-buried clumps, wider than they are tall. First layer, gappy, so the
            // ash shows through them.
            if (WorldGen.genRand.NextBool(55))
            {
                int w = WorldGen.genRand.Next(3, 8);

                for (int i = 0; i < w; i++)
                {
                    int air = Surface(x + i, ridge);

                    if (air < 0)
                        continue;

                    if (!WorldGen.genRand.NextBool(3))
                        Put(x + i, air + 1, TileID.BoneBlock);
                }
            }

            // Ordinary spoil. This one really does sit on top, and it is the one thing
            // here that should — but it was reading ridge instead of the ash, so it sat
            // wherever the ridge was expected to be rather than where it ended up.
            if (WorldGen.genRand.NextBool(18))
            {
                int air = Surface(x, ridge);

                if (air >= 0)
                    WorldGen.PlaceSmallPile(x, air, WorldGen.genRand.Next(6), 0, (ushort)TileID.SmallPiles);
            }
        }

        /// <summary>The top of the ash in this column, within a few tiles of where it was expected.</summary>
        private static int Surface(int x, int about)
        {
            for (int y = Math.Max(1, about - 8); y < about + 10; y++)
            {
                if (!WorldGen.InWorld(x, y, 10))
                    return -1;

                Tile t = Main.tile[x, y];

                if (t.HasTile && Main.tileSolid[t.TileType])
                    return y - 1;
            }

            return -1;
        }

        /// <summary>
        /// Everything on this column that was put there by some earlier pass and has no
        /// business on a siege ground: fallen logs, pots, piles, stumps, campfires. The
        /// sweep runs before the ash, so the ash never closes over one.
        /// </summary>
        private static void Sweep(int x, int ridge)
        {
            for (int y = Math.Max(1, ridge - 40); y < ridge + AshDepth; y++)
            {
                if (!WorldGen.InWorld(x, y, 10))
                    continue;

                Tile t = Main.tile[x, y];

                if (t.HasTile && Layout.Clutter(t.TileType))
                {
                    t.HasTile = false;
                    WorldGen.SquareTileFrame(x, y, true);
                }
            }
        }

        private static void Put(int x, int y, int type)
        {
            if (!WorldGen.InWorld(x, y, 10))
                return;

            Tile t = Main.tile[x, y];
            t.HasTile = true;
            t.TileType = (ushort)type;
            t.Slope = SlopeType.Solid;
            t.IsHalfBlock = false;
            WorldGen.SquareTileFrame(x, y, true);
        }
    }

    /// <summary>
    /// Two places in this world are supposed to be bare, and neither of them stays bare
    /// on its own: every pass that plants a tree, a herb, a vine or a mushroom runs long
    /// after the ground is made, and none of them ask whether anything was planned here.
    ///
    /// The Grace plain is bare because a city is going on it. The Cleared Ground is bare
    /// because an army cleared its field of fire and three hundred years of nothing
    /// finishing has kept it that way — so the timber goes but the stumps stay, which is
    /// the only line of lore in this pass and it is written in wood rather than words.
    /// </summary>
    public class GraceCleanupPass : GenPass
    {
        public GraceCleanupPass(string name, double weight) : base(name, weight) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Clearing the ground in front of the wall";

            if (!ExtraLargeWorld.Active)
                return;

            (int plainLeft, int plainRight) = Layout.Plain;
            (int clearLeft, int clearRight) = WorldMirror.Span(Layout.ClearedStart, Layout.MileEnd);

            Strip(progress, plainLeft, plainRight, stumps: false);
            Strip(progress, clearLeft, clearRight, stumps: true);
        }

        private static void Strip(GenerationProgress progress, int left, int right, bool stumps)
        {
            int top = Math.Max(1, (int)Main.worldSurface - 400);
            int bottom = Math.Min(Main.maxTilesY - 2, (int)Main.worldSurface + 40);

            for (int x = left; x <= right; x++)
            {
                progress.Set((x - left) / (float)Math.Max(1, right - left));

                for (int y = top; y <= bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    Tile t = Main.tile[x, y];

                    // Growth covers what grew; Clutter covers what was put down -- fallen
                    // logs, pots, piles, camp gear. The pitch takes neither.
                    if (!t.HasTile || !(Growth(t.TileType) || Layout.Clutter(t.TileType)))
                        continue;

                    bool keepAsStump = stumps && t.TileType == TileID.Trees && Rooted(x, y);

                    if (keepAsStump)
                        continue;

                    t.HasTile = false;
                    WorldGen.SquareTileFrame(x, y, true);
                }
            }
        }

        /// <summary>A tree tile with soil directly beneath it: the part that is left behind.</summary>
        private static bool Rooted(int x, int y)
        {
            if (!WorldGen.InWorld(x, y + 1, 10))
                return false;

            Tile below = Main.tile[x, y + 1];
            return below.HasTile && (below.TileType == TileID.Grass || below.TileType == TileID.Dirt);
        }

        /// <summary>Anything that grew here after the ground was made.</summary>
        private static bool Growth(ushort type) => type switch
        {
            TileID.Trees or TileID.PalmTree or TileID.Cactus or TileID.Vines or
            TileID.Plants or TileID.Plants2 or TileID.Sunflower or TileID.MushroomPlants or
            TileID.LargePiles or TileID.LargePiles2 or TileID.SmallPiles or
            TileID.Pots or TileID.Seaweed or
            TileID.ImmatureHerbs or TileID.MatureHerbs or TileID.BloomingHerbs => true,
            _ => false,
        };
    }

    /// <summary>
    /// Puts the mod's passes into vanilla's list, and puts two of vanilla's own passes
    /// inside brackets.
    ///
    /// The set pieces go in after Gem Caves, which is the last pass that carves caverns
    /// at any scale — registering a protected rectangle only stops the nine passes that
    /// bother to ask, so going last is the other half of not being dug through. See
    /// PAD_World_Plan.md §5a.
    /// </summary>
    public class PADWorldGen : ModSystem
    {
        public override void Load()
        {
            GenLog.Log = Mod.Logger;

            // Vanilla rolls the desert's position with this and then argues with itself
            // about the answer. Four passes call it in all; the bracket round Dunes is
            // what tells this one which call it is looking at.
            On_WorldGen.RandomWorldPoint_int_int_int_int += BiomeBands.OnTheBeachSide;

            // And the desert proper, which rolls its origin inside its own biome object
            // rather than through anything in WorldGen.
            Terraria.GameContent.Biomes.On_DesertBiome.Place += BiomeBands.DesertInItsBand;

            // And the evil, which rolls where it goes with one ordinary call to the
            // world's random. The handler's first test is a static bool, so every other
            // roll in the game pays for one branch and nothing else.
            On_UnifiedRandom.Next_int_int += BiomeBands.EvilInItsBand;

            On_WorldGen.GrowLivingTree += BiomeBands.NoGiantsHere;
            On_WorldGen.GrowEpicTree += BiomeBands.NoEpicsHere;
        }

        public override void Unload() => GenLog.Log = null;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            Insert(tasks, "Reset", new GraceLayoutPass("PAD Layout", 10f));

            // The biomes that place themselves. None of them is replaced; each is run with
            // the fields it consults set to the plate's band, and set back after.
            Wrap(tasks, "Dunes", BiomeBands.OpenDunes, BiomeBands.CloseDunes);
            Wrap(tasks, "Full Desert", BiomeBands.OpenFullDesert, BiomeBands.CloseFullDesert);
            Wrap(tasks, "Corruption", BiomeBands.OpenCorruption, BiomeBands.CloseCorruption);

            // Not a band: a pass that walks off the end of the world when one of the two
            // beaches is the sliver left at the Grace end.
            Wrap(tasks, "Shell Piles", BiomeBands.OpenShellPiles, null);

            Insert(tasks, "Gem Caves", new SealedPocketPass("PAD Sealed Pocket", 40f));
            Insert(tasks, "Gem Caves", new GracePlainPass("PAD Grace Plain", 120f));
            Insert(tasks, "Gem Caves", new TheBallPass("PAD The Ball", 220f));

            Insert(tasks, "Micro Biomes", new WastedMilePass("PAD Wasted Mile", 90f));

            // Last of ours, and after every pass that grows something. The plain and the
            // Cleared Ground are both meant to be bare and neither of them stays bare on
            // its own.
            Insert(tasks, "Final Cleanup", new GraceCleanupPass("PAD Grace Cleanup", 40f), before: true);
        }

        /// <summary>Beside the named vanilla pass, or nowhere at all, loudly.</summary>
        private void Insert(List<GenPass> tasks, string beside, GenPass pass, bool before = false)
        {
            int at = tasks.FindIndex(g => g.Name.Equals(beside, StringComparison.Ordinal));

            if (at == -1)
            {
                Mod.Logger.Warn($"World generation pass '{beside}' is not in the list, so '{pass.Name}' was not added. The world will generate without it.");
                return;
            }

            tasks.Insert(before ? at : at + 1, pass);
        }

        /// <summary>The named vanilla pass, left where it is and under its own name.</summary>
        private void Wrap(List<GenPass> tasks, string named, Action open, Action close)
        {
            int at = tasks.FindIndex(g => g.Name.Equals(named, StringComparison.Ordinal));

            if (at == -1)
            {
                Mod.Logger.Warn($"World generation pass '{named}' is not in the list, so it could not be held to its band. It will land wherever vanilla puts it.");
                return;
            }

            tasks[at] = new BracketPass(tasks[at], open, close);
        }
    }
}
