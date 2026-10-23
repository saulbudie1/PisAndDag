using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles.SeaFloor;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Vast Ocean's floor, by request ("Maybe deepen the sendiment too, add ocasional rocks
    /// and ice pillars, pointy and curl in clusters", "there should also be mountains and hills
    /// in the sendiments and abnormalties and a deep trench hole with caverns", "mountains can
    /// rise from section 12-11 to section 10 or 9", "with clear path cave ways", "even smaller
    /// rocks boulders too", "can use mud as floor too", "and clay too", "geyser field too with
    /// its equivelent ground, actually do research and improve the ocean floor", "skeletal
    /// remains are good additions", "and obsidians flow too ig? DO YOUR RESEARCH"): laid as the
    /// place's file is made (VastOceanBuild), from the world's seed, before the water is poured
    /// in. Drawn from what is known of the deep sea floor (the notes for section 16.87 have
    /// the sources), each thing made of the game's nearest tile.
    ///
    /// THE SEDIMENT. Where the floor is deep, under the depth at which shells dissolve (the
    /// carbonate compensation depth, <see cref="Ccd"/>: half way down the 11th section), it is
    /// red clay in some stretches and brown mud in others (the deep sea's pelagic clay, bright
    /// red to chocolate brown, and its oozes), beds of the two in turn under it, then mudstone,
    /// what they harden to, with thin beds of sandstone through it, and old bones turned to
    /// fossil here and there; where it is higher, on the shoulders and flat tops of the
    /// mountains, it is pale sand, the ooze of shells that has not dissolved. Under all of it
    /// is basalt (the game's granite, darkest of its rocks, with paler stone in it), the rock
    /// every ocean floor is made of, <see cref="VastOcean.Sediment"/> rows under the dunes'
    /// lowest at the bottom of the world.
    ///
    /// THE GROUND. Long low dunes (VastOcean.Dune) and hills of sediment over them; five ranges
    /// of seamounts, volcanoes that never reached the surface, standing up out of it along the
    /// width, each a main peak with two or three lesser ones about it, jagged along their ridges
    /// and crags: their feet in the 12th section, or the 11th where they stand on a hill, and
    /// their summits brought to a row in the 9th or the 10th, the tallest in the 9th. The
    /// tallest has a crater at its summit with vents in it (as Axial Seamount has its caldera
    /// and its vent fields); another is a guyot, its top cut flat long ago, sand and coral
    /// mounds on it.
    ///
    /// THE CAVES, "clear path cave ways", and since by request ("also more sophisticated cave
    /// system please.") a network in each range (VastOceanFloor.Caves), and since ("Make cavern
    /// even deeper and more maze like") a maze, going down into the range's root: a great
    /// cavern where the rock is deepest and sixteen to sixty-four more hollows where it is
    /// thickest, of five kinds (halls, domes, galleries, grottos, pockets), and knots between
    /// them where only ways meet, joined by ways that wind back and forth, narrow, middling or
    /// wide, with loops all through them and dead ends off them, some forking; four to six
    /// mouths on the water, at least one on either side; dripstone in the hollows; the eels'
    /// crevices off them, and in the big rooms their dens and rocks left floating. The trench's
    /// caverns and the sinkholes' hollows as before. Every hollow and way has a wall behind it,
    /// by request ("also you didnt put walls for the caves"), of the rock it was cut from. Every
    /// way is wide enough to swim through.
    ///
    /// THE TRENCH, two thousand five hundred columns or so right of the platform: a cut down
    /// through the floor, two hundred columns and more across at its mouth between two rims,
    /// narrowing and wandering as it goes down <see cref="VastOcean.TrenchDeep"/> rows under
    /// the dunes' lowest, its walls jagged basalt with ledges standing out of them, silt in the
    /// bowl at its foot (a trench gathers the fine sediment that slides down into it). Caverns
    /// run off its walls at five depths either side, some joined to the one under them.
    ///
    /// THE VOLCANIC ROCK. Lava under the sea is basalt, chilled at once by the water: it piles
    /// up in pillows a skin of glass round each (the game's obsidian), with broken glass between
    /// them (its ash); where it came faster it ran out in tongues, glass on top. Lava rich in
    /// silica, the kind obsidian is made of, comes up under the sea rarely, and builds domes
    /// spiny with glass and a breccia round their feet (as Havre did in 2012, at about a
    /// kilometre down): one such dome stands at the foot of a range, a tongue of obsidian run
    /// from it.
    ///
    /// THE VENTS, "geyser field ... with its equivelent ground": black smokers, chimneys of
    /// metal sulfide (the game's lead, copper and iron ores) on a mound of their own fallen
    /// pieces, black smoke pouring from their glowing mouths, the sediment round them stained
    /// orange with iron, white mats of bacteria on the mound, giant tube worms and mussels where
    /// the warm water reaches; and a field of white smokers of the other kind, tall white
    /// chimneys of carbonate (the game's marble) on a terrace of green mantle rock, pouring
    /// clear water, as at Lost City.
    ///
    /// THE SEEPS, where gas comes up through the mud: pavements and mounds of carbonate rock
    /// the bacteria leave, gas hydrate, an ice that burns (the game's ice), bulging from the
    /// mud, mats of white and orange bacteria, beds of mussels; mud volcanoes, cones of mud with
    /// a crater at the top; and fields of pockmarks, shallow pits the gas has blown in the mud.
    ///
    /// THE NODULES. On the red clay, fields of the dark lumps that grow there over millions of
    /// years about a shark's tooth or a shell, lying loose on top of it (the game's lead and
    /// iron ore: manganese and iron is what they are, with nickel, copper and cobalt in them).
    ///
    /// THE PILLARS, "ocasional rocks and ice pillars, pointy and curl in clusters": now and
    /// then on the open floor, a cluster of ice or of rock (granite, or stone), by turns: three
    /// to nine spires, the one in the middle the tallest, each narrowing to a point and bending
    /// as it goes up, the outer ones most, now and then over into a hook. And here and there
    /// lone rocks lying half buried; and all over, by request ("even smaller rocks boulders
    /// too"), pebbles and small boulders, alone and in little groups (stones dropped by ice that
    /// melted on its way over, as the deep sea has).
    ///
    /// THE REMAINS, "skeletal remains are good additions": the bones of whales where they sank,
    /// a skeleton lasting decades with mats of bacteria and mussels on it; the long skeleton of
    /// something far bigger than any whale; and in the sediment, old bones turned to fossil,
    /// one whole skeleton of a sea reptile lying half out of it.
    ///
    /// THE ODD THINGS, "abnormalties", not of the sea as it is known: gardens of glowing mosses
    /// on outcrops of rock (the game's own, each its own light, slowly creeping over the stone
    /// next to them as its mosses do); geodes broken open, lined with one gem; rows of standing
    /// stones half in the sediment; and sinkholes going down into it to a hollow at their foot.
    ///
    /// THE LIFE, by request ("ambiance tiles for extra ambiances, like smoke/gas emitting things
    /// and Coral reefs and sponges", "be sure to also create particles for those ambience tiles
    /// too"): the floor's own living things and its vents' mouths (SeaLife), each where its
    /// kind lives (<see cref="PlantLife"/>): giant tube worms about the black smokers, seep tube
    /// worms and gas cracks at the seeps, reefs of cold-water coral on the coral mounds and the
    /// guyot, corals and sponges on the ranges' ledges, sea pens and xenophyophores over the
    /// plains; what each gives off (smoke, bubbles, flecks, the octocorals' light) is
    /// SeaLifeEffects'.
    ///
    /// Written straight into the tiles, as a world's generating writes ground. Sand, silt and
    /// shell that would have nothing under them (and fall, the first time the game looks at
    /// them) are made firm or taken away before the water is poured in (<see cref="Settle"/>).
    /// What is put down as an object (the life, stalactites, loose gems, bone worms, mussels)
    /// goes down by the game's own hand once the water is in (<see cref="Furnish"/>).
    /// </summary>
    public static partial class VastOceanFloor
    {
        /// <summary>What was laid, and where, for the preview's log.</summary>
        public static readonly List<(string what, int x, int y)> Log = new List<(string, int, int)>();

        /// <summary>Each range as laid, for the preview's checks: its middle column, its summit's column and row, and the row it was brought to.</summary>
        public static readonly List<(int middle, int summitX, int summitRow, int peakRow)> Summits = new List<(int, int, int, int)>();

        /// <summary>The tunnels cut through the ranges and off the trench, each from end to end, for the preview's checks.</summary>
        public static readonly List<(string what, Vector2 from, Vector2 to)> Ways = new List<(string, Vector2, Vector2)>();

        // ---- the materials -------------------------------------------------------------------

        private const ushort Rock = TileID.Stone;
        private const ushort Dark = TileID.Granite;

        /// <summary>The sediment's layers, rows under its top: sand, hardened sand, sandstone; rock under that.</summary>
        private const int SandRows = 6;
        private const int HardRows = 28;
        private const int SandstoneRows = 150;

        /// <summary>The glowing mosses (the game's own, each its own light): krypton, xenon, argon, neon, helium.</summary>
        private static readonly ushort[] Glows = { TileID.KryptonMoss, TileID.XenonMoss, TileID.ArgonMoss, TileID.VioletMoss, TileID.RainbowMoss };

        // ---- the plan --------------------------------------------------------------------------

        private static int seed;

        /// <summary>The sediment's top in each column (the dunes and the hills), how high a range stands over it there (0 where none), and the ground's top, the two together.</summary>
        private static int[] baseTop;
        private static float[] mount;
        private static int[] top;

        private readonly struct Peak
        {
            public readonly float X, High, Half;

            public Peak(float x, float high, float half)
            {
                X = x;
                High = high;
                Half = half;
            }
        }

        private sealed class Range
        {
            public int Middle;
            public float High;
            public float Half;
            public int PeakRow;

            /// <summary>Its top cut flat at its row (a guyot); and whether it is the tallest, with the crater.</summary>
            public bool Guyot, Tallest;
            public readonly List<Peak> Peaks = new List<Peak>();
            public readonly List<(float x, float high, float half)> Crags = new List<(float, float, float)>();
        }

        private static readonly List<Range> ranges = new List<Range>();

        /// <summary>The trench: its middle column, half its width at its mouth, the row of the floor at its middle, and the rows it is cut from and to, with its wandering middle on each.</summary>
        private static int trenchX;
        private static float trenchHalf;
        private static int trenchMouth;
        private static int trenchTop, trenchFoot;
        private static float[] trenchMid;

        /// <summary>Where loose gems are to lie (on the gem lining of a geode), put down once the water is in.</summary>
        private static readonly List<Point> gemSpots = new List<Point>();

        /// <summary>Where tube worms (the game's coral) and mussels and clams (its shells) are to stand, put down once the water is in: at the vents, the seeps, the whale falls and on the coral mounds.</summary>
        private static readonly List<Point> wormSpots = new List<Point>();
        private static readonly List<Point> shellSpots = new List<Point>();

        /// <summary>
        /// Where the floor's own living things and vents go once the water is in (<see cref="PlantLife"/>):
        /// each live black smoker's mouth and each white chimney's top; the vent fields, the seeps,
        /// the coral mounds (and the guyot's top) and the nodule fields by their middles and half
        /// widths; the mud volcanoes' craters and the pockmarks still letting out gas.
        /// </summary>
        private static readonly List<Point> ventMouths = new List<Point>();
        private static readonly List<Point> whiteTops = new List<Point>();
        private static readonly List<(int x, int half)> ventFields = new List<(int, int)>();
        private static readonly List<(int x, int half)> seeps = new List<(int, int)>();
        private static readonly List<(int x, int half)> reefs = new List<(int, int)>();
        private static readonly List<(int x, int half)> noduleFields = new List<(int, int)>();
        private static readonly List<(int x, int half)> craters = new List<(int, int)>();
        private static readonly List<(int x, int half)> pocks = new List<(int, int)>();

        private static int Left => Subworld.Margin + 60;
        private static int Right => VastOcean.Wide - Subworld.FarMargin - 60;

        /// <summary>The trench's middle column, for the preview.</summary>
        public static int TrenchX => trenchX;

        /// <summary>The sediment's top in a column (the dunes and the hills, under any range), for the preview.</summary>
        public static int SedimentTop(int x) => baseTop[x];

        // ---- laying it ---------------------------------------------------------------------------

        /// <summary>
        /// Lays the floor: the ground; the odd things, the rocks and the pillars on it; the trench
        /// and the caves cut out of it last, so that nothing stands in them; its faces worn to
        /// slopes; what would fall made firm. Returns the top of the ground in each column, the
        /// first solid row from the surface down, for what is put on it after.
        /// </summary>
        public static int[] Lay(VastOcean.Dune dune, int worldSeed)
        {
            seed = worldSeed;
            Log.Clear();
            Summits.Clear();
            Ways.Clear();
            gemSpots.Clear();
            wormSpots.Clear();
            shellSpots.Clear();
            ventMouths.Clear();
            whiteTops.Clear();
            ventFields.Clear();
            seeps.Clear();
            reefs.Clear();
            noduleFields.Clear();
            craters.Clear();
            pocks.Clear();
            Planted.Clear();
            taken.Clear();
            Hollows.Clear();
            Lairs.Clear();
            walled.Clear();
            tunnels.Clear();
            rooms.Clear();
            DenRooms.Clear();
            DenLairs.Clear();
            Drips.Clear();
            FloatingRocks.Clear();
            RoomWays.Clear();
            denned.Clear();
            walling = NoWalls;

            var rand = new UnifiedRandom(worldSeed ^ 0x0F100F);

            Plan(dune, rand);
            Ground();
            Crown(rand);
            Oddities(rand);
            Rocks(rand);
            Boulders(rand);
            Pillars(rand);
            Trench(rand);
            RangeCaves(new UnifiedRandom(worldSeed ^ 0x0CA7E5));
            Dripstones(new UnifiedRandom(worldSeed ^ 0x0D819));
            Dens(new UnifiedRandom(worldSeed ^ 0x0DE45));
            Crevices(new UnifiedRandom(worldSeed ^ 0x0EE15));
            FinishWalls();
            Smooth();
            Settle();
            Floaters();

            int[] tops = Tops();

            foreach (Range range in ranges)
            {
                int summitX = range.Middle;

                for (int x = Math.Max(0, range.Middle - (int)(range.Half * 1.8f)); x <= Math.Min(VastOcean.Wide - 1, range.Middle + (int)(range.Half * 1.8f)); x++)
                {
                    if (tops[x] < tops[summitX])
                        summitX = x;
                }

                Summits.Add((range.Middle, summitX, tops[summitX], range.PeakRow));
            }

            return tops;
        }

        // ---- the plan: where everything goes ----------------------------------------------

        private static void Plan(VastOcean.Dune dune, UnifiedRandom rand)
        {
            int w = VastOcean.Wide;
            baseTop = new int[w];
            mount = new float[w];
            top = new int[w];
            ranges.Clear();

            // The trench, right of the platform and well clear of it.
            trenchX = (int)(w * 0.69f) + rand.Next(-120, 121);
            trenchHalf = 100f + rand.Next(0, 16);

            // The hills of sediment over the dunes: a long rise here, a mound there; none in
            // the trench's mouth; and the trench's two rims.
            var hills = new List<(float x, float high, float half)>();
            int hillCount = 16 + rand.Next(8);

            for (int i = 0; i < hillCount; i++)
            {
                float x = rand.Next(Left, Right);
                float high = 30f + rand.Next(0, 141);
                float half = high * (2.2f + 1.8f * rand.NextFloat());

                if (Math.Abs(x - trenchX) < trenchHalf + half)
                    continue;

                hills.Add((x, high, half));
            }

            hills.Add((trenchX - trenchHalf - 70f, 60f + rand.Next(0, 31), 95f));
            hills.Add((trenchX + trenchHalf + 70f, 60f + rand.Next(0, 31), 95f));

            for (int x = 0; x < w; x++)
            {
                float rise = 0f;

                foreach (var (hx, high, half) in hills)
                {
                    float u = (x - hx) / half;

                    if (u > -1f && u < 1f)
                        rise += high * (1f - u * u) * (1f - u * u);
                }

                // Where hills run together, they round off under three hundred rows: the
                // sediment's top, and so every range's foot, in the 12th section or the 11th.
                if (rise > 240f)
                    rise = 240f + 60f * (1f - (float)Math.Exp(-(rise - 240f) / 60f));

                baseTop[x] = VastOcean.FloorAt(x, dune) - (int)rise;
            }

            // The ranges: one in each of five stretches of the width, none near the trench; the
            // tallest summit brought to a row in the 9th section, the others to rows in the 9th
            // or the 10th.
            float[] at = { 0.11f, 0.29f, 0.45f, 0.60f, 0.86f };
            int tallest = rand.Next(at.Length);
            int guyot = (tallest + 1 + rand.Next(at.Length - 1)) % at.Length;

            for (int i = 0; i < at.Length; i++)
            {
                float middle = w * (at[i] + (rand.NextFloat() - 0.5f) * 0.05f);

                if (Math.Abs(middle - trenchX) < 1100f)
                    middle = trenchX + (middle < trenchX ? -1100f : 1100f);

                int c = (int)MathHelper.Clamp(middle, Left + 900, Right - 900);
                float sections = i == tallest ? 8.3f + 0.25f * rand.NextFloat() : 8.6f + 1.1f * rand.NextFloat();
                int peakRow = VastOcean.Surface + (int)(VastOcean.SectionRows * sections);
                float high = baseTop[c] - peakRow;
                var range = new Range { Middle = c, High = high, Half = high * (0.6f + 0.35f * rand.NextFloat()), PeakRow = peakRow, Tallest = i == tallest, Guyot = i == guyot };

                range.Peaks.Add(new Peak(c, high, range.Half));

                int lesser = 2 + rand.Next(2);

                for (int k = 0; k < lesser; k++)
                {
                    float side = k % 2 == 0 ? -1f : 1f;
                    float off = range.Half * (0.35f + 0.35f * rand.NextFloat()) * side;

                    range.Peaks.Add(new Peak(c + off, high * (0.45f + 0.35f * rand.NextFloat()), range.Half * (0.35f + 0.25f * rand.NextFloat())));
                }

                // Crags along the upper ridges: sharp teeth a few columns wide.
                int crags = 10 + rand.Next(10);

                for (int k = 0; k < crags; k++)
                    range.Crags.Add((c + (rand.NextFloat() * 2f - 1f) * range.Half * 0.85f, 8f + 30f * rand.NextFloat(), 3f + 9f * rand.NextFloat()));

                ranges.Add(range);
            }

            // Each range as it stands, then brought up or down as a whole so that its summit is
            // at its row exactly (the jagging and the crags lift it by a sixth or so). The guyot
            // is brought to seventy rows over its row and then cut off flat at it.
            foreach (Range range in ranges)
            {
                int x0 = Math.Max(0, (int)(range.Middle - range.Half * 1.8f));
                int x1 = Math.Min(w - 1, (int)(range.Middle + range.Half * 1.8f));
                var rise = new float[x1 - x0 + 1];
                int target = range.Guyot ? range.PeakRow - 70 : range.PeakRow;

                for (int x = x0; x <= x1; x++)
                    rise[x - x0] = RangeHigh(range, x);

                float lo = 0.2f, hi = 3f;

                for (int step = 0; step < 40; step++)
                {
                    float k = (lo + hi) * 0.5f;

                    if (Summit(rise, x0, k) < target)
                        hi = k;
                    else
                        lo = k;
                }

                for (int x = x0; x <= x1; x++)
                {
                    float m = rise[x - x0] * lo;

                    if (range.Guyot)
                        m = Math.Min(m, baseTop[x] - range.PeakRow);

                    mount[x] = Math.Max(mount[x], m);
                }
            }

            for (int x = 0; x < w; x++)
                top[x] = baseTop[x] - (int)mount[x];

            trenchMouth = baseTop[trenchX];
        }

        /// <summary>The highest row a range reaches (the least) with its height taken <paramref name="k"/> times.</summary>
        private static int Summit(float[] rise, int x0, float k)
        {
            int best = int.MaxValue;

            for (int i = 0; i < rise.Length; i++)
                best = Math.Min(best, baseTop[x0 + i] - (int)(rise[i] * k));

            return best;
        }

        /// <summary>How high a range stands over the sediment in this column: the highest of its peaks there, jagged.</summary>
        private static float RangeHigh(Range range, int x)
        {
            float best = 0f;

            foreach (Peak p in range.Peaks)
            {
                float u = Math.Abs(x - p.X) / p.Half;

                if (u >= 1f)
                    continue;

                float h = p.High * (float)Math.Pow(1f - Math.Pow(u, 1.25), 1.8);
                best = Math.Max(best, h);
            }

            if (best <= 0f)
                return 0f;

            // Jagged along its ridges: two scales of sharp-crested noise.
            best *= 1f + 0.12f * Ridged(x * 0.021f, seed + 11) + 0.05f * Ridged(x * 0.07f, seed + 12);

            // And the crags on its upper part.
            if (best > 0.35f * range.High)
            {
                foreach (var (cx, high, half) in range.Crags)
                {
                    float d = Math.Abs(x - cx);

                    if (d < half)
                        best += high * (float)Math.Pow(1f - d / half, 1.3);
                }
            }

            return Math.Max(0f, best);
        }

        // ---- the ground ------------------------------------------------------------------

        /// <summary>
        /// The carbonate compensation depth: half way down the 11th section. Over it the shells of
        /// the plankton reach the floor and lie there as a pale ooze; under it they dissolve on
        /// the way down, and only clay and mud are left.
        /// </summary>
        public static int Ccd => VastOcean.SectionTop(11) + VastOcean.SectionRows / 2;

        /// <summary>
        /// The sediment laid down at this cell: pale ooze (the game's sand) over the compensation
        /// depth, the line between wavering a few rows; red clay or mud under it, by stretch
        /// (<see cref="RedClayAt"/>).
        /// </summary>
        private static ushort Ooze(int x, int y) => y < Ccd + (int)(6f * Noise(x / 30f, seed + 97)) ? TileID.Sand : RedClayAt(x, y) ? TileID.ClayBlock : TileID.Mud;

        /// <summary>
        /// This cell is in a stretch of red clay, not mud: long stretches of each, a few hundred to
        /// a thousand columns, where they meet tongues of each running into the other, slanting
        /// down, so that there is no straight seam between them.
        /// </summary>
        private static bool RedClayAt(int x, int y) => Noise(x / 900f, seed + 91) + 0.35f * Noise(x / 150f, seed + 92) + 0.3f * Noise((x + 2.5f * y) / 40f, seed + 98) < -0.05f;

        /// <summary>The top of the sediment in this column is red clay.</summary>
        private static bool RedClay(int x) => RedClayAt(x, baseTop[x]);

        /// <summary>
        /// The ground, column by column: a range's basalt over the sediment, a skin of whatever
        /// sediment lies at its height where its side is gentle, and its root going down through
        /// the sediment twice as deep as it stands over it; the sediment in its beds under its own
        /// top (<see cref="Bed"/>); basalt under the sediment; and the basalt the trench is cut in,
        /// under a skin of sediment, reaching out from it the farther the deeper, its edge ragged.
        /// </summary>
        private static void Ground()
        {
            int w = VastOcean.Wide, h = VastOcean.High;

            for (int x = 0; x < w; x++)
            {
                int b = baseTop[x];
                int t = top[x];
                int root = mount[x] >= 1f ? (int)(2f * mount[x]) + 4 : 0;
                int slope = Math.Abs(top[Math.Min(w - 1, x + 3)] - top[Math.Max(0, x - 3)]);
                bool gentle = slope <= 6;
                float fromTrench = Math.Abs(x - trenchX) - trenchHalf;
                ushort skin = Ooze(x, t);

                for (int y = Math.Max(1, t); y < h; y++)
                {
                    ushort type;

                    if (y < b)
                    {
                        int under = y - t;
                        type = gentle && under < 2 ? skin : gentle && under < 4 ? (skin == TileID.Sand ? TileID.HardenedSand : skin) : Basalt(x, y);
                    }
                    else
                    {
                        int d = y - b;

                        if (d < root || d >= SandstoneRows || d >= SandRows && fromTrench < 70f + 0.9f * d + 18f * Noise(y / 13f, seed + 36))
                            type = Basalt(x, y);
                        else
                            type = Bed(x, y, d);
                    }

                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = type;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.TileColor = 0;
                    tile.LiquidAmount = 0;
                }
            }
        }

        /// <summary>
        /// The sediment <paramref name="d"/> rows under its top, at this cell: what was laid down
        /// there (<see cref="Ooze"/>) for the first six; pale ooze hardened, then sandstone, under
        /// that; clay or mud with beds of the other through it, a row to a few thick, then
        /// mudstone, what they harden to, with now and then a thin bed of sandstone (sand carried
        /// down by a slide from somewhere higher); and here and there, from ten rows down, old
        /// bones turned to fossil.
        /// </summary>
        private static ushort Bed(int x, int y, int d)
        {
            ushort laid = Ooze(x, y);

            if (d < SandRows)
                return laid;

            if (d >= 10 && d < SandstoneRows - 10 && Noise2(x * 0.22f, y * 0.33f, seed + 95) > 0.8f)
                return TileID.FossilOre;

            if (laid == TileID.Sand)
                return d < HardRows ? TileID.HardenedSand : TileID.Sandstone;

            // The beds bend a little along the floor, and come at no set spacing.
            float warp = 3f * Noise(x / 40f, seed + 93);

            if (d < HardRows)
                return Noise((d + warp) / 3.2f, seed + 96) > 0.4f ? (laid == TileID.ClayBlock ? TileID.Mud : TileID.ClayBlock) : laid;

            return Noise((d + warp) / 2.2f, seed + 94) > 0.72f ? TileID.Sandstone : TileID.Mudstone;
        }

        /// <summary>Basalt: the game's granite, with paler stone through it in patches here and there.</summary>
        private static ushort Basalt(int x, int y) => Noise2(x * 0.045f, y * 0.06f, seed + 21) + 0.3f * Noise2(x * 0.16f, y * 0.2f, seed + 22) > 0.62f ? Rock : Dark;

        // ---- the trench ----------------------------------------------------------------------

        /// <summary>
        /// The trench: from over its mouth between the rims down to <see cref="VastOcean.TrenchDeep"/>
        /// rows under the floor's lowest; at its mouth <see cref="trenchHalf"/> either side of a
        /// middle that wanders as it goes down, opening out into the rims over it and narrowing to
        /// a third of that under it, each wall jagged on its own; a bowl at its foot, silted. Then
        /// its caverns.
        /// </summary>
        private static void Trench(UnifiedRandom rand)
        {
            int mouth = Math.Min(baseTop[(int)(trenchX - trenchHalf)], baseTop[(int)(trenchX + trenchHalf)]) - 70;
            int foot = VastOcean.FloorLowest + VastOcean.TrenchDeep;
            float phase = rand.NextFloat() * MathHelper.TwoPi;
            float phase2 = rand.NextFloat() * MathHelper.TwoPi;
            const int bowl = 34;

            trenchTop = mouth;
            trenchFoot = foot;
            trenchMid = new float[foot - mouth + 1];

            for (int y = mouth; y <= foot; y++)
            {
                float f = (float)(y - mouth) / (foot - mouth);
                float half = trenchHalf * (1f - 0.66f * (float)Math.Pow(f, 0.9));

                if (y < trenchMouth)
                    half += (trenchMouth - y) * 0.9f;

                if (y > foot - bowl)
                {
                    float v = (float)(y - (foot - bowl)) / bowl;
                    half *= (float)Math.Sqrt(Math.Max(0.0, 1.0 - v * v));
                }

                float middle = trenchX + 24f * (float)Math.Sin(y / 77f + phase) + 10f * (float)Math.Sin(y / 31f + phase2);
                float left = middle - half - 9f * Noise(y / 9f, seed + 31) - 5f * Noise(y / 3.3f, seed + 32);
                float right = middle + half + 9f * Noise(y / 9f, seed + 33) + 5f * Noise(y / 3.3f, seed + 34);

                trenchMid[y - mouth] = middle;

                for (int x = (int)Math.Ceiling(left); x <= (int)right; x++)
                    Open(x, y);
            }

            // Ledges: shelves of rock standing out from its walls here and there, flat on top and
            // rounded under, none reaching more than a third of the way across.
            for (int s = -1; s <= 1; s += 2)
            {
                int ledges = 4 + rand.Next(4);

                for (int i = 0; i < ledges; i++)
                {
                    int y = (int)MathHelper.Lerp(trenchMouth + 10, foot - 60, rand.NextFloat());
                    int wall = WallAt(y, s);

                    if (wall < 0)
                        continue;

                    float across = Math.Abs(wall - trenchMid[y - trenchTop]) * 2f;
                    float reach = Math.Min(14f + 16f * rand.NextFloat(), across / 3f);
                    float thick = 5f + 6f * rand.NextFloat();
                    int salt = rand.Next(1000);

                    for (int dx = -4; dx <= (int)reach; dx++)
                    {
                        float u = Math.Max(0f, dx) / reach;
                        int under = (int)(thick * (float)Math.Sqrt(Math.Max(0f, 1f - u * u)) + 1.5f * Noise(dx / 2.5f, seed + salt));

                        for (int dy = 0; dy <= under; dy++)
                            Set(wall - s * dx, y + dy, Basalt(wall - s * dx, y + dy));
                    }

                    Log.Add(("trench ledge", wall, y));
                }
            }

            // Silt in the bowl at its foot: what drifts down settles there.
            for (int x = (int)(trenchX - trenchHalf - 60); x <= trenchX + trenchHalf + 60; x++)
            {
                int y = FirstSolid(x, foot - 120, VastOcean.High - 2);

                if (y < 0 || y < foot - 50)
                    continue;

                int deep = 4 + (int)(3f * (Noise(x / 7f, seed + 35) * 0.5f + 0.5f));

                for (int k = 0; k < deep && y + k < VastOcean.High - 1; k++)
                    Set(x, y + k, TileID.Silt);
            }

            // The caverns off its walls, at five depths, either side; their far ends kept by side.
            // A wall behind all of them, of the rock they are cut from (not behind the trench's
            // own water, which they start in: only what was rock has one).
            float[] depths = { 0.18f, 0.34f, 0.5f, 0.66f, 0.82f };
            var ends = new[] { new List<Vector2>(), new List<Vector2>() };

            walling = WallByRock;

            for (int i = 0; i < depths.Length; i++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    if (rand.Next(5) == 0)
                        continue;

                    int y = (int)MathHelper.Lerp(trenchMouth + 30, foot - 40, depths[i]) + rand.Next(-18, 19);
                    int wall = WallAt(y, s);

                    if (wall < 0)
                        continue;

                    int length = 90 + rand.Next(0, 151);
                    var path = new List<Vector2> { new Vector2(wall - s * 12, y) };
                    int steps = 4 + rand.Next(3);

                    for (int k = 1; k <= steps; k++)
                        path.Add(new Vector2(wall + s * length * k / steps, y + rand.Next(-26, 27)));

                    Tunnel(path, 3.2f + 1.3f * rand.NextFloat(), rand);

                    // A chamber or two along it, one in three a garden of the glowing mosses.
                    int chambers = 1 + rand.Next(2);

                    for (int k = 0; k < chambers; k++)
                    {
                        Vector2 c = path[1 + rand.Next(path.Count - 1)];
                        float rx = 14f + 16f * rand.NextFloat(), ry = 9f + 9f * rand.NextFloat();

                        Chamber(c.X, c.Y, rx, ry);
                        Hollows.Add((Hollow.Grotto, (int)c.X, (int)c.Y, rx, ry));

                        if (rand.Next(3) == 0)
                            Moss((int)(c.X - rx - 3), (int)(c.Y - ry - 3), (int)(c.X + rx + 3), (int)(c.Y + ry + 3), Glows[rand.Next(Glows.Length)]);

                        Log.Add(("trench chamber", (int)c.X, (int)c.Y));
                    }

                    ends[s < 0 ? 0 : 1].Add(path[path.Count - 1]);
                    Ways.Add(("trench cavern", path[0], path[path.Count - 1]));
                    Log.Add(("trench cavern", wall, y));
                }
            }

            // Some caverns joined to the one under them, the same side, at their far ends.
            foreach (List<Vector2> side in ends)
            {
                for (int i = 0; i + 1 < side.Count; i++)
                {
                    if (rand.Next(2) == 0)
                        continue;

                    Vector2 a = side[i], b = side[i + 1];
                    Vector2 mid = (a + b) * 0.5f + new Vector2(rand.Next(-30, 31), 0f);

                    Tunnel(new List<Vector2> { a, mid, b }, 2.8f, rand);
                    Ways.Add(("trench join", a, b));
                }
            }

            walling = NoWalls;

            Log.Add(("trench", trenchX, foot));
        }

        /// <summary>The trench's wall on one side (-1 left, 1 right) at this row: its first solid column out from the trench's middle there, or -1.</summary>
        private static int WallAt(int y, int side)
        {
            if (y < trenchTop || y > trenchFoot)
                return -1;

            int middle = (int)trenchMid[y - trenchTop];

            if (Main.tile[middle, y].HasTile)
                return -1;

            for (int k = 1; k < (int)trenchHalf * 3; k++)
            {
                int x = middle + side * k;

                if (x < 1 || x >= VastOcean.Wide - 1)
                    return -1;

                if (Main.tile[x, y].HasTile)
                    return x;
            }

            return -1;
        }

        // ---- the ways: tunnels, chambers, discs ---------------------------------------------

        /// <summary>
        /// A tunnel along a path (a smooth curve through its points, Catmull-Rom), cut a disc at
        /// a time half a tile apart or less, its width breathing a little along it but never under
        /// the radius given less half a tile. Its legs are cut into pieces no longer than
        /// twenty-four tiles first: a long leg beside a short one would throw the curve far out
        /// past the short one's end and leave walls standing across it.
        /// </summary>
        private static void Tunnel(List<Vector2> path, float radius, UnifiedRandom rand)
        {
            if (path.Count < 2)
                return;

            int salt = rand.Next(1000);
            var even = new List<Vector2> { path[0] };

            for (int i = 0; i + 1 < path.Count; i++)
            {
                int parts = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(path[i], path[i + 1]) / 24f));

                for (int k = 1; k <= parts; k++)
                    even.Add(Vector2.Lerp(path[i], path[i + 1], (float)k / parts));
            }

            path = even;
            tunnels.Add(even);

            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 p0 = path[Math.Max(0, i - 1)], p1 = path[i], p2 = path[i + 1], p3 = path[Math.Min(path.Count - 1, i + 2)];
                float length = Math.Max(Vector2.Distance(p1, p2), Math.Max(Vector2.Distance(p0, p2), Vector2.Distance(p1, p3)) * 0.5f);
                int steps = Math.Max(1, (int)(length * 2f));

                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    Vector2 at = Vector2.CatmullRom(p0, p1, p2, p3, t);
                    float r = radius + 0.5f * Noise((i * 100 + s) / 23f, seed + salt);

                    Disc(at.X, at.Y, r);
                }
            }
        }

        /// <summary>Every tunnel cut, its points as cut through (<see cref="Tunnel"/>), for where the ways come into the rooms (VastOceanFloor.Caves, the dens).</summary>
        private static readonly List<List<Vector2>> tunnels = new List<List<Vector2>>();

        private static void Chamber(float cx, float cy, float rx, float ry)
        {
            for (int x = (int)(cx - rx - 2); x <= (int)(cx + rx + 2); x++)
            {
                for (int y = (int)(cy - ry - 2); y <= (int)(cy + ry + 2); y++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    float edge = 1f + 0.12f * Noise2(x * 0.2f, y * 0.2f, seed + 41);

                    if (dx * dx + dy * dy < edge * edge)
                        Open(x, y);
                }
            }
        }

        private static void Disc(float cx, float cy, float r)
        {
            for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
            {
                for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;

                    if (dx * dx + dy * dy <= r * r)
                        Open(x, y);
                }
            }
        }

        // ---- the summits -------------------------------------------------------------------

        /// <summary>
        /// The tallest range's crater, a bowl cut into its summit with a field of vents on its
        /// floor, as a volcano under the sea often has (Axial Seamount, its caldera and the vents
        /// in it); and the guyot's flat top, coral mounds on it.
        /// </summary>
        private static void Crown(UnifiedRandom rand)
        {
            foreach (Range range in ranges)
            {
                if (range.Tallest)
                    Crater(range, rand);
                else if (range.Guyot)
                    GuyotTop(range, rand);
            }
        }

        /// <summary>The column of a range's summit: its highest ground within a third of its half width of its middle, taken over twenty columns so a crag's tooth is not it.</summary>
        private static int SummitColumn(Range range)
        {
            int c = range.Middle, best = c;
            float least = float.MaxValue;

            for (int x = c - (int)(range.Half / 3f); x <= c + (int)(range.Half / 3f); x++)
            {
                float around = 0f;

                for (int k = -10; k <= 10; k++)
                    around += top[Math.Clamp(x + k, 0, VastOcean.Wide - 1)];

                if (around < least)
                {
                    least = around;
                    best = x;
                }
            }

            return best;
        }

        /// <summary>
        /// The crater on the tallest summit: a bowl sixteen to twenty-one rows deep with a floor of
        /// ash, its rim all round at the summit's row (the summit built out to carry it where the
        /// peak is narrower than the crater, its outer side falling away from the rim, steeper as
        /// it goes, until it meets the peak's own); a field of vents on its floor.
        /// </summary>
        private static void Crater(Range range, UnifiedRandom rand)
        {
            int c = SummitColumn(range);
            int half = 18 + rand.Next(8);
            int depth = 16 + rand.Next(6);
            int rim = Math.Max(top[c], range.PeakRow);
            int floor = rim + depth;
            const int reach = 80;

            for (int dx = -half - reach; dx <= half + reach; dx++)
            {
                int x = c + dx;
                int a = Math.Abs(dx);
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                if (a <= half)
                {
                    // The bowl: cut down to where the summit stands over it, built up to it where
                    // the peak falls away under it; its floor the broken glass a sea volcano sheds
                    // (the game's ash).
                    int bowl = floor - (int)(depth * (float)Math.Pow(a / (float)half, 2.2));

                    for (int y = g; y < bowl; y++)
                        Open(x, y);

                    for (int y = bowl; y < g; y++)
                        Set(x, y, Basalt(x, y));

                    Set(x, bowl, TileID.Ash);
                    Set(x, bowl + 1, TileID.Ash);
                }
                else
                {
                    // Its outer side, built up where the peak falls away faster.
                    int o = a - half;
                    int outer = rim + (int)(1.1f * o + 0.04f * o * o);

                    for (int y = outer; y < g; y++)
                        Set(x, y, Basalt(x, y));
                }
            }

            Taken(c - half - reach, c + half + reach);
            Log.Add(("crater", c, floor));

            // Its vents: a small field on its floor, nothing of it over the rim (a chimney on the
            // bowl's side would stand over it).
            HydrothermalField(c, half - 5, 3, 3 + rand.Next(2), depth - 4, rim, rand);
        }

        /// <summary>The guyot's flat top: low mounds of coral on its pale ooze, as on seamounts the world over.</summary>
        private static void GuyotTop(Range range, UnifiedRandom rand)
        {
            int x0 = range.Middle - (int)range.Half, x1 = range.Middle + (int)range.Half;
            int mounds = 0;

            for (int x = x0; x <= x1 && mounds < 6; x += 12 + rand.Next(18))
            {
                if (top[x] != range.PeakRow || top[x - 6] != range.PeakRow || top[x + 6] != range.PeakRow)
                    continue;

                CoralMound(x, rand);
                mounds++;
            }

            // The reef on it: over the flat of its top, the columns where the ground is at its row.
            int left = int.MaxValue, right = int.MinValue;

            for (int x = Math.Max(Left, x0); x <= Math.Min(Right, x1); x++)
            {
                if (top[x] == range.PeakRow)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }

            if (right > left)
                reefs.Add(((left + right) / 2, (right - left) / 2));

            Log.Add(("guyot", range.Middle, range.PeakRow));
        }

        // ---- the odd things -----------------------------------------------------------------

        private static void Oddities(UnifiedRandom rand)
        {
            // On the ranges: a black smoker field on the side of one that has neither the crater
            // nor the flat top; a field of white chimneys on another; pillow lavas at their feet,
            // tongues of lava down their sides; the obsidian dome; coral mounds on their sand.
            Range vented = ranges.Find(r => !r.Tallest && !r.Guyot);
            int fieldHalf = 34 + rand.Next(16);
            int x = SpotFlank(rand, fieldHalf + 18, 30f, 220f, vented);

            if (x < 0)
                x = SpotFlank(rand, fieldHalf + 18, 20f, 300f, null);

            if (x >= 0)
                HydrothermalField(x, fieldHalf, 9 + rand.Next(7), 5 + rand.Next(4), 26 + rand.Next(16), VastOcean.Surface + 1, rand);

            x = SpotFlank(rand, 40, 40f, 240f, null);

            if (x >= 0)
                LostCity(x, rand);

            // The pillows where the side is gentle, at the foot mostly: on a steep side they would
            // hang out over it.
            for (int i = 0; i < 3; i++)
            {
                x = SpotFlank(rand, 36, 2f, 120f, null, null, 0.35f);

                if (x >= 0)
                    PillowField(x, rand);
            }

            // The tongues of lava and the dome take the columns they run down to, not only their own.
            for (int i = 0; i < 2; i++)
            {
                int length = 50 + rand.Next(60);

                x = SpotFlank(rand, 8, 60f, 220f, null, c => Downhill(c) > 0 ? (c - 8, c + length + 8) : (c - length - 8, c + 8));

                if (x >= 0)
                    LavaFlow(x, Downhill(x), length, false, rand);
            }

            int domeHalf = 18 + rand.Next(12), domeFlow = 40 + rand.Next(35);

            x = SpotFlank(rand, domeHalf + 14, 1f, 50f, null, c => Downhill(c) > 0 ? (c - domeHalf - 14, c + domeHalf + domeFlow + 8) : (c - domeHalf - domeFlow - 8, c + domeHalf + 14));

            if (x >= 0)
                SilicicDome(x, domeHalf, domeFlow, rand);

            for (int i = 0, made = 0; i < 400 && made < 6; i++)
            {
                int cx = rand.Next(Left + 20, Right - 20);

                if (mount[cx] < 40f || Main.tile[cx, top[cx]].TileType != TileID.Sand || Math.Abs(top[cx - 6] - top[cx + 6]) > 3 || !Free(cx - 12, cx + 12))
                    continue;

                Taken(cx - 12, cx + 12);
                CoralMound(cx, rand);
                made++;
            }

            // On the plains: the seeps, the mud volcanoes and the pockmarks, the nodules on the red
            // clay, the remains, and the odd things.
            for (int i = 0; i < 2; i++)
            {
                x = Spot(rand, 40);

                if (x >= 0)
                    ColdSeep(x, rand);
            }

            for (int i = 0; i < 3; i++)
            {
                x = Spot(rand, 56);

                if (x >= 0)
                    MudVolcano(x, rand);
            }

            for (int i = 0; i < 3; i++)
            {
                x = Spot(rand, 90);

                if (x >= 0)
                    Pockmarks(x, rand);
            }

            for (int i = 0; i < 4; i++)
            {
                x = Spot(rand, 150, RedClay);

                if (x >= 0)
                    NoduleField(x, rand);
            }

            for (int i = 0; i < 3; i++)
            {
                x = Spot(rand, 70);

                if (x >= 0)
                    WhaleFall(x, rand);
            }

            x = Spot(rand, 130);

            if (x >= 0)
                Serpent(x, rand);

            for (int i = 0; i < 2; i++)
            {
                x = Spot(rand, 40);

                if (x >= 0)
                    FossilBed(x, rand);
            }

            for (int i = 0; i < 6; i++)
            {
                x = Spot(rand, 40);

                if (x >= 0)
                    Garden(x, rand);
            }

            for (int i = 0; i < 3; i++)
            {
                x = Spot(rand, 20);

                if (x >= 0)
                    Geode(x, rand);
            }

            for (int i = 0; i < 2; i++)
            {
                x = Spot(rand, 40);

                if (x >= 0)
                    StandingStones(x, rand);
            }

            for (int i = 0; i < 3; i++)
            {
                x = Spot(rand, 30);

                if (x >= 0)
                    Sinkhole(x, rand);
            }
        }

        /// <summary>A column of open floor (away from the trench, the ranges and what is already laid), where the ground lies near level this many columns either side, and that <paramref name="want"/> takes if given; -1 if none was found.</summary>
        private static int Spot(UnifiedRandom rand, int half, Func<int, bool> want = null)
        {
            for (int tries = 0; tries < 300; tries++)
            {
                int x = rand.Next(Left + half + 4, Right - half - 4);

                if (Math.Abs(x - trenchX) < trenchHalf + 200f || mount[x - half] >= 1f || mount[x] >= 1f || mount[x + half] >= 1f)
                    continue;

                if (Math.Abs(top[x - half] - top[x + half]) > half / 4 + 3)
                    continue;

                if (want != null && !want(x))
                    continue;

                if (Taken(x - half - 10, x + half + 10))
                    continue;

                return x;
            }

            return -1;
        }

        /// <summary>
        /// A column on a range's side (of the one given, or any), where it stands between
        /// <paramref name="least"/> and <paramref name="most"/> rows over the sediment, its ground
        /// falling no more than <paramref name="steep"/> rows a column over this many either side;
        /// -1 if none was found. It takes those columns, or the ones <paramref name="takes"/> gives
        /// for it (a tongue of lava takes the columns it runs down to), and ten more either side.
        /// </summary>
        private static int SpotFlank(UnifiedRandom rand, int half, float least, float most, Range only, Func<int, (int from, int to)> takes = null, float steep = 1f)
        {
            for (int tries = 0; tries < 400; tries++)
            {
                Range range = only ?? ranges[rand.Next(ranges.Count)];
                int side = rand.Next(2) == 0 ? -1 : 1;
                int x = range.Middle + side * (int)(range.Half * (0.15f + 1.15f * rand.NextFloat()));

                if (x - half < Left || x + half > Right || mount[x] < least || mount[x] > most)
                    continue;

                if (Math.Abs(top[x - half] - top[x + half]) > 2 * half * steep)
                    continue;

                var (from, to) = takes != null ? takes(x) : (x - half, x + half);

                if (from < Left || to > Right || Taken(from - 10, to + 10))
                    continue;

                return x;
            }

            return -1;
        }

        /// <summary>Which way is down a range's side here: away from the middle of the range nearest it.</summary>
        private static int Downhill(int x)
        {
            Range near = ranges[0];

            foreach (Range r in ranges)
            {
                if (Math.Abs(r.Middle - x) < Math.Abs(near.Middle - x))
                    near = r;
            }

            return x >= near.Middle ? 1 : -1;
        }

        /// <summary>No odd thing lies in these columns (asked, not taken).</summary>
        private static bool Free(int from, int to)
        {
            foreach (var (a, b) in taken)
            {
                if (from <= b && to >= a)
                    return false;
            }

            return true;
        }

        /// <summary>The odd things laid so far, by the columns they take: none is laid over another. True if these columns are already taken; if not, they are now.</summary>
        private static bool Taken(int from, int to)
        {
            if (!Free(from, to))
                return true;

            taken.Add((from, to));
            return false;
        }

        /// <summary>The odd things laid so far, by the columns they take.</summary>
        private static readonly List<(int from, int to)> taken = new List<(int, int)>();

        // ---- the vents ----------------------------------------------------------------------

        /// <summary>
        /// A field of black smokers: a mound of sulfide, the pieces of fallen chimneys and the ore
        /// grown through them (the game's lead, iron, copper and silver ores, for the pyrite,
        /// chalcopyrite and sphalerite they are), chimneys standing on it, the live ones pouring
        /// black smoke (the sulfide's own colour) and faintly glowing at the mouth (water over
        /// 350 degrees glows), the dead ones mottled red with iron; white mats of bacteria on the
        /// mound; the sediment round it stained orange with iron; tube worms and mussels about it.
        /// <paramref name="half"/> wide either side, the mound <paramref name="moundHigh"/> high,
        /// the tallest chimney <paramref name="tallest"/>; nothing solid of it over the row
        /// <paramref name="ceiling"/>.
        /// </summary>
        private static void HydrothermalField(int cx, int half, int moundHigh, int chimneys, int tallest, int ceiling, UnifiedRandom rand)
        {
            int salt = rand.Next(1000);

            // The mound, on the ground as it lies.
            for (int dx = -half; dx <= half; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                float u = dx / (float)half;
                int rise = (int)Math.Round(moundHigh * Math.Pow(Math.Max(0f, 1f - u * u), 0.8) + 1.2f * Noise(x / 3f, seed + salt));

                for (int y = Math.Max(g - rise, ceiling); y < g + 3; y++)
                    Set(x, y, Sulfide(x, y, salt));
            }

            // The sediment round it, stained with iron (the game's copper ore is the colour of rust).
            for (int dx = -half - 18; dx <= half + 18; dx++)
            {
                if (Math.Abs(dx) <= half)
                    continue;

                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                float near = 1f - (Math.Abs(dx) - half) / 18f;

                if (g < 0)
                    continue;

                for (int y = g; y < g + 2; y++)
                {
                    if (Hash(x, y, seed + salt + 3) < 0.75f * near)
                        Set(x, y, TileID.Copper);
                }
            }

            // The chimneys: the first, in the middle, the tallest and live.
            for (int k = 0; k < chimneys; k++)
            {
                int dx = k == 0 ? rand.Next(-half / 4, half / 4 + 1) : rand.Next(-half * 3 / 4, half * 3 / 4 + 1);
                int x = cx + dx;
                int foot = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (foot < 0)
                    continue;

                bool live = k == 0 || rand.Next(3) != 0;
                int high = Math.Min(k == 0 ? tallest : 5 + rand.Next(Math.Max(1, tallest * 2 / 3)), foot - ceiling - 2);

                if (high >= 4)
                    Chimney(x, foot, high, live, rand);
            }

            // White mats over a third of the mound's top; worms and mussels about it.
            Mats(cx - half, cx + half, PaintID.WhitePaint, 0.35f, salt + 5);

            for (int dx = -half - 6; dx <= half + 6; dx += 2)
            {
                if (rand.Next(3) != 0)
                    continue;

                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g > 0)
                    (rand.Next(2) == 0 ? wormSpots : shellSpots).Add(new Point(x, g - 1));
            }

            ventFields.Add((cx, half));
            Log.Add(("vent field", cx, FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2)));
        }

        /// <summary>Sulfide: mostly the game's lead ore (pyrite and pyrrhotite are dark), iron, copper (chalcopyrite) and silver (sphalerite) through it.</summary>
        private static ushort Sulfide(int x, int y, int salt)
        {
            float h = Hash(x, y, seed + salt);

            return h < 0.55f ? TileID.Lead : h < 0.72f ? TileID.Iron : h < 0.86f ? TileID.Copper : TileID.Silver;
        }

        /// <summary>
        /// One chimney from its foot up: three wide at the bottom, two higher up, one at the top,
        /// leaning a little as it goes (each step of the lean joined to the one under it by a
        /// side, not a corner), a flange standing out now and then; a live one glowing at its
        /// mouth, its black smoke coming from the mouth's own tile put on it once the water is in;
        /// a dead one red with iron.
        /// </summary>
        private static void Chimney(int x, int foot, int high, bool live, UnifiedRandom rand)
        {
            int salt = rand.Next(1000);
            int mouthX = x, mouthY = foot;
            int last = x;

            for (int i = 0; i < high; i++)
            {
                int y = foot - 1 - i;
                float f = i / (float)high;
                int from = f < 0.25f ? -1 : 0, to = f < 0.6f ? 1 : 0;
                int at = x + (int)Math.Round(1.5f * Noise(y / 7f, seed + salt));
                float h = Hash(x, y, seed + salt + 1);
                ushort type = live ? (h < 0.7f ? TileID.Lead : h < 0.85f ? TileID.Iron : TileID.Copper) : (h < 0.55f ? TileID.Copper : TileID.Iron);

                for (int dx = from; dx <= to; dx++)
                    Set(at + dx, y, type);

                if (at != last)
                    Set(last, y, type);

                // A flange: a lip standing out to one side, where the water once spread under it.
                if (i > 3 && i < high - 2 && i % 5 == 0 && rand.Next(2) == 0)
                {
                    int side = rand.Next(2) == 0 ? -1 : 1;
                    int edge = side < 0 ? at + from : at + to;

                    Set(edge + side, y, type);

                    if (rand.Next(2) == 0)
                        Set(edge + 2 * side, y, type);
                }

                last = at;
                mouthX = at;
                mouthY = y;
            }

            if (!live)
                return;

            // The mouth glows: water over 350 degrees does. Over it, once the water is in, the
            // mouth's own tile, its black smoke rising from it (SeaLife, SeaLifeEffects).
            Set(mouthX, mouthY, TileID.LavaMoss);
            ventMouths.Add(new Point(mouthX, mouthY));
        }

        /// <summary>
        /// A field of white chimneys of the other kind (as at Lost City, on a terrace of the
        /// Atlantis Massif): a terrace cut back into the range's side and built out over it, of
        /// mantle rock green with serpentine (the game's granite painted deep green); on it tall
        /// slender chimneys of carbonate (its marble), white, thick at their feet and narrowing,
        /// flanges standing out from them, each split at its top into pinnacles; broken pieces of
        /// them about their feet. They pour clear water: no smoke.
        /// </summary>
        private static void LostCity(int cx, UnifiedRandom rand)
        {
            const int half = 32, blend = 10;
            int salt = rand.Next(1000);
            int level = FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2);

            // The terrace, level across the field and blended into the side at its ends.
            for (int dx = -half - blend; dx <= half + blend; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                float edge = Math.Max(0f, (Math.Abs(dx) - half) / (float)blend);
                int want = (int)Math.Round(MathHelper.Lerp(level + 1.2f * Noise(x / 5f, seed + salt + 3), g, edge));

                for (int y = g; y < want; y++)
                    Open(x, y);

                for (int y = want; y < Math.Max(g, want) + 4; y++)
                {
                    Set(x, y, Dark);
                    Paint(x, y, PaintID.DeepGreenPaint);
                }
            }

            int count = 7 + rand.Next(5);

            for (int k = 0; k < count; k++)
            {
                int x = cx + (k == 0 ? rand.Next(-6, 7) : rand.Next(-half + 3, half - 2));
                int foot = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (foot < 0)
                    continue;

                int high = k == 0 ? 42 + rand.Next(14) : 10 + rand.Next(26);
                int split = high - 5 - rand.Next(4);
                int pinnacles = 2 + rand.Next(2);
                int middle = pinnacles / 2;
                var tips = new int[pinnacles];

                for (int p = 0; p < pinnacles; p++)
                    tips[p] = p == middle ? high - split : high - split - 1 - rand.Next(3);

                int last = x, crown = x;

                for (int i = 0; i < high; i++)
                {
                    int y = foot - 1 - i;
                    float f = i / (float)high;

                    if (i < split)
                    {
                        int at = x + (int)Math.Round(1.2f * Noise(y / 9f, seed + salt + k));
                        int from = f < 0.3f ? -1 : 0, to = f < 0.65f ? 1 : 0;

                        for (int dx = from; dx <= to; dx++)
                            Set(at + dx, y, TileID.Marble);

                        if (at != last)
                            Set(last, y, TileID.Marble);

                        // Flanges: ledges standing out to one side or to both, every few rows.
                        if (i > 4 && i % 6 == 0 && rand.Next(3) != 0)
                        {
                            int reach = 1 + rand.Next(2);
                            int side = rand.Next(3) - 1;

                            for (int r = 1; r <= reach; r++)
                            {
                                if (side <= 0)
                                    Set(at + from - r, y, TileID.Marble);

                                if (side >= 0)
                                    Set(at + to + r, y, TileID.Marble);
                            }
                        }

                        last = at;
                        crown = at;
                    }
                    else if (i == split)
                    {
                        // The crown the pinnacles stand on, across all of them.
                        for (int px = crown - middle * 2; px <= crown + (pinnacles - 1 - middle) * 2; px++)
                            Set(px, y, TileID.Marble);
                    }
                    else
                    {
                        for (int p = 0; p < pinnacles; p++)
                        {
                            if (i - split < tips[p])
                                Set(crown + (p - middle) * 2, y, TileID.Marble);
                        }
                    }
                }

                // Its middle pinnacle's top, the tallest, where its water pours out.
                whiteTops.Add(new Point(crown, foot - high));
            }

            // Broken pieces of chimney lying on the terrace.
            for (int dx = -half; dx <= half; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g >= level - 2 && g <= level + 2 && Hash(x, g, seed + salt + 9) < 0.14f)
                    Set(x, g - 1, TileID.Marble);
            }

            Log.Add(("white chimneys", cx, level));
        }

        // ---- the seeps ----------------------------------------------------------------------

        /// <summary>
        /// A cold seep, where gas comes up through the mud: mats of white and orange bacteria on
        /// the mud; a pavement of carbonate the bacteria leave (the game's marble) in slabs in the
        /// mud's top, a few small mounds and chimneys of it; gas hydrate (an ice that burns: the
        /// game's ice) bulging from the mud in low mounds, a skin of the mud still over one side of
        /// each, orange mats on the rest; mussel beds and tube worms; and a pockmark beside it.
        /// </summary>
        private static void ColdSeep(int cx, UnifiedRandom rand)
        {
            int half = 18 + rand.Next(8);
            int salt = rand.Next(1000);

            // The mats first: what is laid over them after is bare.
            Mats(cx - half - 6, cx + half + 6, PaintID.WhitePaint, 0.35f, salt + 3);
            Mats(cx - half - 6, cx + half + 6, PaintID.OrangePaint, 0.18f, salt + 4);

            for (int dx = -half; dx <= half; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0 || Noise(x / 4f, seed + salt) < -0.1f)
                    continue;

                // A slab: the top row of the mud turned to carbonate, raised a row here and there.
                Set(x, g, TileID.Marble);

                if (Noise(x / 2.5f, seed + salt + 1) > 0.45f)
                    Set(x, g - 1, TileID.Marble);
            }

            int mounds = 2 + rand.Next(3);

            for (int k = 0; k < mounds; k++)
            {
                int x = cx + rand.Next(-half + 3, half - 2);
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                int high = 3 + rand.Next(5), w = 1 + rand.Next(3);

                for (int dx = -w; dx <= w; dx++)
                {
                    int rise = high - Math.Abs(dx) * high / (w + 1);

                    for (int y = g - rise; y < g; y++)
                        Set(x + dx, y, TileID.Marble);
                }
            }

            int hydrates = 2 + rand.Next(3);

            for (int k = 0; k < hydrates; k++)
            {
                int x = cx + rand.Next(-half + 4, half - 3);
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                int w = 2 + rand.Next(3), high = 1 + rand.Next(3);
                int covered = rand.Next(2) == 0 ? -1 : 1;

                for (int dx = -w; dx <= w; dx++)
                {
                    float u = dx / (float)(w + 1);
                    int rise = (int)Math.Round(high * Math.Sqrt(1f - u * u));

                    // White, as it is (the game's ice, painted).
                    for (int y = g - rise; y < g + 2; y++)
                    {
                        Set(x + dx, y, TileID.IceBlock);
                        Paint(x + dx, y, PaintID.WhitePaint);
                    }

                    // The mud still over one side of it; mats on what shows.
                    if (Math.Sign(dx) == covered)
                        Set(x + dx, g - rise - 1, Ooze(x + dx, g));
                    else if (rand.Next(3) == 0)
                        Paint(x + dx, g - rise, PaintID.OrangePaint);
                }
            }

            for (int dx = -half; dx <= half; dx += 2)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g > 0 && rand.Next(3) == 0)
                    shellSpots.Add(new Point(x, g - 1));
                else if (g > 0 && rand.Next(5) == 0)
                    wormSpots.Add(new Point(x, g - 1));
            }

            Pockmark(cx + (rand.Next(2) == 0 ? -1 : 1) * (half + 10), 5 + rand.Next(5), 3 + rand.Next(3), rand);
            seeps.Add((cx, half));
            Log.Add(("cold seep", cx, FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2)));
        }

        /// <summary>
        /// A mud volcano: a cone of the grey mud it brings up from deep under the floor (the
        /// game's silt), steep, a crater at its top with white mats about it, tongues of the mud
        /// run out over the floor at its foot.
        /// </summary>
        private static void MudVolcano(int cx, UnifiedRandom rand)
        {
            int half = 12 + rand.Next(13);
            int high = 8 + rand.Next(9);
            int g0 = FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2);
            int crater = 2 + rand.Next(3);

            for (int dx = -half - 22; dx <= half + 22; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                float u = Math.Abs(dx) / (float)half;
                int rise = u < 1f ? (int)Math.Round(high * Math.Pow(1f - u, 1.15)) : 0;

                // The crater at its top, deepest in its middle.
                if (Math.Abs(dx) <= crater)
                    rise -= Math.Min(rise - 1, 4 - Math.Abs(dx));

                // The mud flows at its foot, a row or two thick on the ground as it lies.
                if (u >= 1f && Noise(x / 6f, seed + cx) > -0.2f && Math.Abs(dx) - half < 10 + (int)(12f * (Noise(dx / 9f, seed + cx + 1) * 0.5f + 0.5f)))
                    rise = 1 + (Math.Abs(dx) - half < 6 ? 1 : 0);

                if (rise <= 0)
                    continue;

                // The cone stands on the ground at its middle, filled down to the ground under it
                // wherever that dips, and a row thick at least wherever it rises.
                int surface = u < 1f ? Math.Min(g0 - rise, g - 1) : g - rise;

                for (int y = surface; y < g; y++)
                    Set(x, y, TileID.Silt);
            }

            Mats(cx - crater - 4, cx + crater + 4, PaintID.WhitePaint, 0.5f, cx);
            craters.Add((cx, crater));
            Log.Add(("mud volcano", cx, g0));
        }

        /// <summary>A field of pockmarks: three to seven pits the gas has blown in the mud.</summary>
        private static void Pockmarks(int cx, UnifiedRandom rand)
        {
            int n = 3 + rand.Next(5);

            for (int k = 0; k < n; k++)
                Pockmark(cx + rand.Next(-80, 81), 5 + rand.Next(10), 3 + rand.Next(6), rand);

            Log.Add(("pockmarks", cx, top[cx]));
        }

        /// <summary>One pockmark: a round pit with steep sides and a flat floor, a mat on its floor one time in three and a crust of carbonate one time in three.</summary>
        private static void Pockmark(int cx, int r, int depth, UnifiedRandom rand)
        {
            int floorKind = rand.Next(3);

            // One with a mat or a crust is still letting out gas.
            if (floorKind < 2)
                pocks.Add((cx, Math.Max(1, r / 3)));

            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                float u = Math.Abs(dx) / (float)r;
                int d = (int)Math.Round(depth * (1f - (float)Math.Pow(u, 3)));

                for (int y = g; y < g + d; y++)
                    Open(x, y);

                if (d >= depth - 1)
                {
                    if (floorKind == 0)
                        Paint(x, g + d, PaintID.WhitePaint);
                    else if (floorKind == 1 && Hash(x, g, seed + 103) < 0.6f)
                        Set(x, g + d, TileID.Marble);
                }
            }
        }

        /// <summary>A field of nodules on the red clay: one or two tiles of the dark ore lying loose on it here and there over a few hundred columns.</summary>
        private static void NoduleField(int cx, UnifiedRandom rand)
        {
            int half = 70 + rand.Next(80);

            for (int x = cx - half; x <= cx + half; x++)
            {
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0 || Main.tile[x, g].TileType != TileID.ClayBlock || Hash(x, g, seed + 101) > 0.3f)
                    continue;

                ushort type = rand.Next(7) == 0 ? TileID.Iron : TileID.Lead;

                Set(x, g - 1, type);

                if (rand.Next(5) == 0 && FirstSolid(x + 1, VastOcean.Surface + 1, VastOcean.High - 2) == g)
                {
                    Set(x + 1, g - 1, type);
                    x++;
                }
            }

            noduleFields.Add((cx, half));
            Log.Add(("nodules", cx, FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2)));
        }

        // ---- the volcanic rock ---------------------------------------------------------------

        /// <summary>
        /// A pile of pillow lava: basalt in rounded pillows (the game's granite), each with its own
        /// skin of glass (its obsidian) where the water chilled it, so that every pillow shows in
        /// the pile; broken glass in the gaps between them (its ash). The pillows are dropped one
        /// at a time about the middle, each rolling off the pile while the ground beside it is
        /// lower, then coming to rest on the highest of what is under its middle, so that they
        /// heap up into a low mound and not a tower.
        /// </summary>
        private static void PillowField(int cx, UnifiedRandom rand)
        {
            var pillows = new List<List<Point>>();
            int n = 8 + rand.Next(9);
            int spread = 14 + rand.Next(9);

            for (int k = 0; k < n; k++)
            {
                int x = cx + (int)Math.Round(spread * (rand.NextFloat() - rand.NextFloat()));
                float rx = 2.5f + 2f * rand.NextFloat(), ry = 1.6f + rand.NextFloat();
                int half = (int)(rx / 2f);

                for (int step = 0; step < 40 && Math.Abs(x - cx) < spread + 10; step++)
                {
                    int here = Support(x, half), left = Support(x - 1, half), right = Support(x + 1, half);

                    if (left > here && left >= right)
                        x--;
                    else if (right > here)
                        x++;
                    else
                        break;
                }

                int g = Support(x, half);

                if (g == int.MaxValue)
                    continue;

                pillows.Add(Pillow(x + 0.5f, g - ry + 0.7f, rx, ry));
            }

            Rinds(pillows);
            Hyaloclastite(cx - spread - 14, cx + spread + 14);
            Log.Add(("pillow lava", cx, FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2)));
        }

        /// <summary>The highest solid row under these columns, <paramref name="half"/> either side of <paramref name="x"/>; int.MaxValue if there is none.</summary>
        private static int Support(int x, int half)
        {
            int g = int.MaxValue;

            for (int dx = -half; dx <= half; dx++)
            {
                int under = FirstSolid(x + dx, VastOcean.Surface + 1, VastOcean.High - 2);

                if (under >= 0)
                    g = Math.Min(g, under);
            }

            return g;
        }

        /// <summary>One pillow: the cells of an ellipse that are still open (it rests on what is under it, never in it), laid as basalt; its cells.</summary>
        private static List<Point> Pillow(float cx, float cy, float rx, float ry)
        {
            var cells = new List<Point>();

            for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
            {
                for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;

                    if (dx * dx + dy * dy > 1f || x < 1 || x >= VastOcean.Wide - 1 || y <= VastOcean.Surface || y >= VastOcean.High - 1 || Main.tile[x, y].HasTile)
                        continue;

                    Set(x, y, Dark);
                    cells.Add(new Point(x, y));
                }
            }

            return cells;
        }

        /// <summary>The glass skin on chilled lava: each pillow's cells on its own edge (against the water, the ground or another pillow) made obsidian.</summary>
        private static void Rinds(List<List<Point>> pillows)
        {
            foreach (List<Point> cells in pillows)
            {
                var own = new HashSet<Point>(cells);

                foreach (Point p in cells)
                {
                    if (!own.Contains(new Point(p.X - 1, p.Y)) || !own.Contains(new Point(p.X + 1, p.Y)) || !own.Contains(new Point(p.X, p.Y - 1)) || !own.Contains(new Point(p.X, p.Y + 1)))
                        Main.tile[p.X, p.Y].TileType = TileID.Obsidian;
                }
            }
        }

        /// <summary>
        /// Broken glass (the game's ash) in the gaps between the pillows: the open cells under each
        /// column's top in the pile with the pile on both sides of them close by and under them
        /// (not the water under an overhang at its edge), and a notch between two pillows open
        /// only upward.
        /// </summary>
        private static void Hyaloclastite(int x0, int x1)
        {
            for (int x = x0; x <= x1; x++)
            {
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                for (int y = g + 1; y < g + 12 && y < VastOcean.High - 2; y++)
                {
                    if (!Main.tile[x, y].HasTile && Near(x, y, -1, 0, 3) && Near(x, y, 1, 0, 3) && Near(x, y, 0, 1, 4))
                        Set(x, y, TileID.Ash);
                }

                if (Main.tile[x - 1, g - 1].HasTile && Main.tile[x + 1, g - 1].HasTile)
                    Set(x, g - 1, TileID.Ash);
            }
        }

        /// <summary>A tile within this many cells of this one, one way (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
        private static bool Near(int x, int y, int dx, int dy, int cells)
        {
            for (int k = 1; k <= cells; k++)
            {
                int nx = x + dx * k, ny = y + dy * k;

                if (nx >= 0 && nx < VastOcean.Wide && ny >= 0 && ny < VastOcean.High && Main.tile[nx, ny].HasTile)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A tongue of lava run down a range's side (lava that came faster than the pillows' does):
        /// a sheet two to four rows thick lying on the ground, its top a crust of glass with ropy
        /// wrinkles in it, a few pillows at its toe; it stops where the ground before it rises.
        /// <paramref name="obsidian"/>: the glassy kind, from the silica-rich dome, pale under its
        /// crust.
        /// </summary>
        private static void LavaFlow(int cx, int dir, int length, bool obsidian, UnifiedRandom rand)
        {
            int salt = rand.Next(1000);
            int toeX = cx, lowest = -1;
            ushort core = obsidian ? Rock : Dark;

            for (int k = 0; k < length; k++)
            {
                int x = cx + dir * k;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0 || x < Left || x > Right)
                    break;

                // The ground before it rises over where it has come down to: it pools and stops.
                if (lowest >= 0 && g < lowest - 6)
                    break;

                lowest = Math.Max(lowest, g);

                int thick = 2 + (int)(1.5f * (Noise(k / 8f, seed + salt) * 0.5f + 0.5f)) + (k > length - 12 ? 1 : 0);

                for (int y = g - thick; y < g; y++)
                    Set(x, y, y <= g - thick + 1 ? TileID.Obsidian : core);

                // Ropy wrinkles: a fold of glass standing a row higher every few columns.
                if (k % 4 == 1 && Noise(k / 3f, seed + salt + 1) > 0f)
                    Set(x, g - thick - 1, TileID.Obsidian);

                toeX = x;
            }

            var pillows = new List<List<Point>>();

            for (int p = 0; p < 4; p++)
            {
                int px = toeX + dir * (p * 4 - 2 + rand.Next(0, 3));
                int pg = FirstSolid(px, VastOcean.Surface + 1, VastOcean.High - 2);
                float ry = 1.4f + rand.NextFloat();
                float rx = 2f + 1.5f * rand.NextFloat();

                if (pg > 0)
                    pillows.Add(Pillow(px + 0.5f, pg - ry + 0.7f, rx, ry));
            }

            Rinds(pillows);
            Log.Add((obsidian ? "obsidian flow" : "lava flow", cx, FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2)));
        }

        /// <summary>
        /// A dome of silica-rich lava, the kind obsidian is made of, rare under the sea (Havre, in
        /// 2012, let out fifteen lavas and domes of it from fourteen vents, 880 to 1280 metres
        /// down): pale rock (the game's stone) banded with glass, standing on the ground at its
        /// middle and filled down through the skin of sediment wherever the ground under it is
        /// lower, its top rough with spines of obsidian, a breccia of broken pieces round its
        /// foot, and an obsidian flow <paramref name="flow"/> long run out from its side down the
        /// range's.
        /// </summary>
        private static void SilicicDome(int cx, int half, int flow, UnifiedRandom rand)
        {
            int high = 14 + rand.Next(9);
            int salt = rand.Next(1000);
            int g0 = FirstSolid(cx, VastOcean.Surface + 1, VastOcean.High - 2);

            for (int dx = -half; dx <= half; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                float u = dx / (float)half;
                int face = g0 - (int)Math.Round(high * Math.Pow(Math.Max(0f, 1f - u * u), 0.7));

                // Where the range's side stands over its face, the side.
                if (g < 0 || g <= face)
                    continue;

                for (int y = face; y < g + 4; y++)
                {
                    // Flow banding: bands of glass through it, bent as it swelled.
                    bool band = (y + (int)(3f * Noise(x / 7f, seed + salt))) % 6 < 2;

                    Set(x, y, band ? TileID.Obsidian : Rock);
                }
            }

            // Spines of glass on its top, a few rows up, jagged.
            int spines = 6 + rand.Next(5);

            for (int k = 0; k < spines; k++)
            {
                int x = cx + rand.Next(-half * 2 / 3, half * 2 / 3 + 1);
                int foot = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                int tall = 4 + rand.Next(9);
                int last = x;

                for (int i = 0; i < tall; i++)
                {
                    int at = x + (int)Math.Round(0.8f * Noise((foot - i) / 4f, seed + salt + k));

                    Set(at, foot - 1 - i, TileID.Obsidian);

                    if (at != last)
                        Set(last, foot - 1 - i, TileID.Obsidian);

                    if (i < tall / 3)
                        Set(at + 1, foot - 1 - i, TileID.Obsidian);

                    last = at;
                }
            }

            // The breccia apron: broken pieces of it lying round its foot.
            for (int dx = -half - 14; dx <= half + 14; dx++)
            {
                if (Math.Abs(dx) <= half - 2)
                    continue;

                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                float h = Hash(x, g, seed + salt + 7);

                if (g < 0 || h > 0.45f)
                    continue;

                ushort piece = h < 0.18f ? TileID.Obsidian : h < 0.33f ? Rock : TileID.Ash;

                Set(x, g - 1, piece);

                if (h < 0.08f)
                    Set(x, g - 2, piece);
            }

            int dir = Downhill(cx);

            LavaFlow(cx + dir * (half - 2), dir, flow, true, rand);
            Log.Add(("obsidian dome", cx, g0));
        }

        // ---- the remains ----------------------------------------------------------------------

        /// <summary>
        /// The skeleton of something far bigger than any whale, lying in long coils over the floor
        /// and through it: its spine, its ribs over its front half, a long skull with its jaw
        /// fallen open, teeth standing in it; mud drifted over it in places.
        /// </summary>
        private static void Serpent(int cx, UnifiedRandom rand)
        {
            int dir = rand.Next(2) == 0 ? -1 : 1;
            int length = 150 + rand.Next(60);
            int head = cx + dir * length / 2;
            float amp = 3f + 4f * rand.NextFloat(), wave = 14f + 8f * rand.NextFloat();
            var spine = new int[length + 1];

            for (int k = 0; k <= length; k++)
            {
                int x = head - dir * k;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                float taper = k < length * 0.8f ? 1f : 1f - (k - length * 0.8f) / (length * 0.2f);
                int y = g - 2 - (int)Math.Round(amp * taper * Math.Sin(k / wave * MathHelper.Pi));

                spine[k] = y;
                Set(x, y, TileID.BoneBlock);

                if (k < length * 0.85f)
                    Set(x, y + 1, TileID.BoneBlock);

                // A spine on each vertebra, shorter toward the tail.
                if (k % 3 == 0)
                {
                    int spike = k < length * 0.6f ? 3 : 2;

                    for (int i = 1; i <= spike; i++)
                        Set(x, y - i, TileID.BoneBlock);
                }

                // Ribs over the front half, down to the floor or into it.
                if (k > 24 && k < length / 2 && k % 4 == 0)
                {
                    int bottom = Math.Max(g, y + 6);
                    int prev = x;

                    for (int ry = y + 2; ry <= bottom; ry++)
                    {
                        float t = (ry - y - 2f) / Math.Max(1, bottom - y - 2);
                        int rx = x + dir * (int)Math.Round(3f * Math.Sin(t * MathHelper.Pi));

                        if (rx != prev)
                            Set(prev, ry, TileID.BoneBlock);

                        Set(rx, ry, TileID.BoneBlock);
                        prev = rx;
                    }
                }
            }

            // The skull: long, its upper jaw over the floor, its lower jaw fallen open on it, teeth in both.
            int hx = head;
            int hg = FirstSolid(hx, VastOcean.Surface + 1, VastOcean.High - 2);

            for (int dx = 0; dx <= 22; dx++)
            {
                int x = hx + dir * dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                int upper = dx < 8 ? 8 : 8 - (dx - 8) / 3;
                int topRow = hg - 9 - (dx < 6 ? 2 : 0);

                for (int y = topRow; y < topRow + Math.Max(2, upper / 2); y++)
                    Set(x, y, TileID.BoneBlock);

                // The lower jaw, along the floor.
                Set(x, g - 1, TileID.BoneBlock);

                // Teeth, down from the upper jaw and up from the lower, every other column.
                if (dx > 6 && dx % 2 == 0)
                {
                    Set(x, topRow + Math.Max(2, upper / 2), TileID.BoneBlock);
                    Set(x, g - 2, TileID.BoneBlock);
                }
            }

            // Its eye.
            Open(hx + dir * 4, hg - 8);
            Open(hx + dir * 5, hg - 8);

            // Mud drifted over it in places.
            for (int k = 30; k < length; k += 23 + rand.Next(20))
            {
                for (int j = 0; j < 8; j++)
                {
                    int x = head - dir * (k + j);
                    int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                    for (int y = g - 2; y < g; y++)
                        Set(x, y, TileID.Mud);
                }
            }

            // The same crowd as on a whale's bones: mats here and there, bone worms on the spine,
            // clams and mussels on the floor beside it.
            int salt = rand.Next(1000);

            for (int dx = -length / 2 - 6; dx <= length / 2 + 6; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                bool bone = Main.tile[x, g].TileType == TileID.BoneBlock;

                if (Noise(x / 3f, seed + salt) < -0.35f)
                    Paint(x, g, PaintID.WhitePaint);

                if (bone && rand.Next(6) == 0)
                    wormSpots.Add(new Point(x, g - 1));
                else if (!bone && rand.Next(5) == 0)
                    shellSpots.Add(new Point(x, g - 1));
            }

            Log.Add(("serpent", cx, spine[length / 2]));
        }

        /// <summary>
        /// A skeleton of a sea reptile long turned to fossil (the game's fossil ore, bones in stone),
        /// lying half out of the sediment: its long-snouted skull, its spine and short ribs, four
        /// paddles and a tail bent down at its end, the stone it lay in round it in patches (the
        /// game's desert fossil).
        /// </summary>
        private static void FossilBed(int cx, UnifiedRandom rand)
        {
            int dir = rand.Next(2) == 0 ? -1 : 1;
            int length = 36 + rand.Next(18);
            int x0 = cx - dir * length / 2;

            for (int k = 0; k <= length; k++)
            {
                int x = x0 + dir * k;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);
                int y = g;

                // The tail bends down into the sediment at its end.
                if (k < 8)
                    y += (8 - k) / 2;

                Set(x, y, TileID.FossilOre);

                if (k > 8 && k < length - 10 && k % 3 == 0)
                {
                    Set(x, y + 1, TileID.FossilOre);
                    Set(x, y + 2, TileID.FossilOre);
                    Set(x, y - 1, TileID.FossilOre);
                }

                // The matrix about it.
                if (Hash(x, y, seed + 111) < 0.4f)
                    Set(x, y + 3, TileID.DesertFossil);
            }

            // Four paddles, two at the shoulders and two at the hips, reaching back.
            foreach (float at in new[] { 0.62f, 0.66f, 0.3f, 0.34f })
            {
                int k = (int)(length * at);
                int x = x0 + dir * k;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                for (int i = 1; i <= 4; i++)
                    Set(x - dir * i, g + i / 2, TileID.FossilOre);
            }

            // The skull: long and low, a snout running on from it.
            int hx = x0 + dir * length;
            int hg = FirstSolid(hx, VastOcean.Surface + 1, VastOcean.High - 2);

            for (int dx = 0; dx < 11; dx++)
            {
                int x = hx + dir * dx;
                int rows = dx < 4 ? 3 : 1;

                for (int i = 0; i < rows; i++)
                    Set(x, hg - 1 + i, TileID.FossilOre);
            }

            Log.Add(("fossil", cx, top[cx]));
        }

        // ---- the coral -----------------------------------------------------------------------

        /// <summary>A mound of coral (the game's coralstone), low and rounded, coral standing on it: the reefs that grow on seamounts in the dark without the sun.</summary>
        private static void CoralMound(int cx, UnifiedRandom rand)
        {
            int half = 4 + rand.Next(7);
            int high = 2 + rand.Next(5);

            for (int dx = -half; dx <= half; dx++)
            {
                int x = cx + dx;
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                float u = dx / (float)(half + 1);
                int rise = (int)Math.Round(high * Math.Sqrt(1f - u * u));

                for (int y = g - rise; y < g + 1; y++)
                    Set(x, y, TileID.Coralstone);

                if (rand.Next(2) == 0)
                    wormSpots.Add(new Point(x, g - rise - 1));
            }

            reefs.Add((cx, half + 2));
            Log.Add(("coral mound", cx, top[cx]));
        }

        /// <summary>Mats of bacteria: the top tile of the ground in these columns painted, this share of them, in patches.</summary>
        private static void Mats(int x0, int x1, byte paint, float share, int salt)
        {
            for (int x = x0; x <= x1; x++)
            {
                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g >= 0 && Noise(x / 3f, seed + salt) < share * 2f - 1f)
                    Paint(x, g, paint);
            }
        }

        /// <summary>A garden of the glowing mosses: a few outcrops of rock out of the sediment, their faces green, blue, pink, violet or many coloured.</summary>
        private static void Garden(int x, UnifiedRandom rand)
        {
            ushort moss = Glows[rand.Next(Glows.Length)];
            int outcrops = 3 + rand.Next(4);

            for (int k = 0; k < outcrops; k++)
            {
                int cx = x + rand.Next(-30, 31);
                int ground = top[cx];
                int high = 4 + rand.Next(9);
                int half = 3 + rand.Next(5);

                for (int dx = -half; dx <= half; dx++)
                {
                    float u = (float)dx / (half + 1);
                    int rise = (int)(high * (1f - u * u) + Noise(dx / 2f, seed + k + 51));

                    for (int y = ground - rise; y < ground + 3; y++)
                        Set(cx + dx, y, Rock);
                }

                Moss(cx - half - 1, ground - high - 2, cx + half + 1, ground + 2, moss);
            }

            Log.Add(("moss garden", x, top[x]));
        }

        /// <summary>The faces of rock in a box that are open to the water turned to a glowing moss.</summary>
        private static void Moss(int x0, int y0, int x1, int y1, ushort moss)
        {
            for (int x = Math.Max(1, x0); x <= Math.Min(VastOcean.Wide - 2, x1); x++)
            {
                for (int y = Math.Max(1, y0); y <= Math.Min(VastOcean.High - 2, y1); y++)
                {
                    Tile t = Main.tile[x, y];

                    if (!t.HasTile || t.TileType != Rock && t.TileType != Dark)
                        continue;

                    if (!Main.tile[x - 1, y].HasTile || !Main.tile[x + 1, y].HasTile || !Main.tile[x, y - 1].HasTile || !Main.tile[x, y + 1].HasTile)
                        t.TileType = moss;
                }
            }
        }

        /// <summary>
        /// The bones of a whale, lying on its belly where it sank: its skull on the floor at one
        /// end, the long lower jaw out in front of it; its spine raised over the floor on its ribs,
        /// which arch down from it to the floor, and coming down to the floor toward the tail; the
        /// tail's end buried in a drift of the sediment. A rib every four columns, so a body can
        /// swim in among them. A whale's bones feed a crowd of their own for decades: white mats
        /// of bacteria on them and on the sediment about them, worms that bore into the bone
        /// standing on the spine (the game's coral), clams and mussels round them.
        /// </summary>
        private static void WhaleFall(int x, UnifiedRandom rand)
        {
            int dir = rand.Next(2) == 0 ? -1 : 1;
            int length = 70 + rand.Next(31);
            int head = x + dir * length / 2;
            int cage = (int)(length * 0.62f);
            int rise = 9 + rand.Next(4);

            // The spine, from behind the skull to the tail: raised over the ribcage, coming down after it.
            var spine = new int[length + 1];

            for (int k = 14; k <= length; k++)
            {
                int sx = head - dir * k;
                float f = k <= cage ? 1f : 1f - MathHelper.SmoothStep(0f, 1f, (float)(k - cage) / (length - cage));
                int high = (int)Math.Round(rise * f * (0.75f + 0.25f * (float)Math.Sin(Math.Min(1f, (k - 14f) / 12f) * MathHelper.PiOver2)));
                int y = top[sx] - 1 - high;

                spine[k] = y;
                Set(sx, y, TileID.BoneBlock);
                Set(sx, y + 1, TileID.BoneBlock);

                // A knob on each vertebra.
                if (k % 3 == 0 && k < length - 4)
                    Set(sx, y - 1, TileID.BoneBlock);

                // Under the spine where it comes down, the gap to the floor filled: it lies on the sand.
                if (high <= 2)
                {
                    for (int fill = y + 2; fill < top[sx]; fill++)
                        Set(sx, fill, TileID.BoneBlock);
                }
            }

            // The ribs: from the spine down to the floor, bowed forward, a rib every four columns.
            for (int k = 17; k <= cage; k += 4)
            {
                int sx = head - dir * k;
                int from = spine[k] + 2;
                int to = top[sx];
                int prevX = sx;

                for (int y = from; y < to; y++)
                {
                    float t = (float)(y - from) / Math.Max(1, to - from);
                    int rx = sx + dir * (int)Math.Round(2.6f * (float)Math.Sin(t * MathHelper.Pi));

                    // Every step joined to the last by a side, not a corner.
                    if (rx != prevX)
                        Set(prevX, y, TileID.BoneBlock);

                    Set(rx, y, TileID.BoneBlock);
                    prevX = rx;
                }
            }

            // The skull at the head: a long dome of bone on the floor, and the lower jaw running
            // on out in front of it a little open.
            int ground = top[head];

            for (int dx = 0; dx <= 13; dx++)
            {
                int sx = head + dir * dx;
                int dome = (int)Math.Round(7f * (float)Math.Sqrt(Math.Max(0f, 1f - (dx - 5f) * (dx - 5f) / 72f)));
                int floorHere = top[sx];

                for (int y = floorHere - 2 - dome; y < floorHere - 2; y++)
                    Set(sx, y, TileID.BoneBlock);

                // Its jaw, along the floor under it.
                Set(sx, floorHere - 1, TileID.BoneBlock);
            }

            for (int dx = 14; dx <= 20; dx++)
                Set(head + dir * dx, top[head + dir * dx] - 1 - (dx > 17 ? 1 : 0), TileID.BoneBlock);

            // Its eye, a hollow in the dome.
            Open(head + dir * 7, ground - 6);
            Open(head + dir * 8, ground - 6);
            Open(head + dir * 7, ground - 5);

            // The tail's end under a drift of the sediment it lies on.
            for (int k = length - 12; k <= length; k++)
            {
                int sx = head - dir * k;
                int drift = 2 + (int)(2f * (float)Math.Sin((k - (length - 12)) / 12f * MathHelper.Pi));

                for (int y = top[sx] - drift; y < top[sx]; y++)
                {
                    if (!Main.tile[sx, y].HasTile)
                        Set(sx, y, Ooze(sx, top[sx]));
                }
            }

            // Its crowd: mats on a third of its bones and on the sediment about it, bone worms on
            // the spine here and there, clams and mussels on the floor among the ribs and round it.
            int salt = rand.Next(1000);
            int first = Math.Min(head + dir * 20, head - dir * length) - 8, last = Math.Max(head + dir * 20, head - dir * length) + 8;

            for (int sx = first; sx <= last; sx++)
            {
                int g = FirstSolid(sx, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                bool bone = Main.tile[sx, g].TileType == TileID.BoneBlock;

                if (Noise(sx / 3f, seed + salt) < (bone ? -0.3f : -0.1f))
                    Paint(sx, g, PaintID.WhitePaint);

                // The floor under the raised bones as well.
                if (g < top[sx] && Noise(sx / 3f, seed + salt + 1) < -0.1f)
                    Paint(sx, top[sx], PaintID.WhitePaint);

                if (bone && rand.Next(4) == 0)
                    wormSpots.Add(new Point(sx, g - 1));

                if (!Main.tile[sx, top[sx] - 1].HasTile && rand.Next(4) == 0)
                    shellSpots.Add(new Point(sx, top[sx] - 1));
            }

            Log.Add(("whale fall", x, top[x]));
        }

        /// <summary>A geode: a shell of granite half in the sediment, broken open along its top, lined with one gem; loose gems lie on its floor.</summary>
        private static void Geode(int x, UnifiedRandom rand)
        {
            ushort[] gems = { TileID.Amethyst, TileID.Topaz, TileID.Sapphire, TileID.Emerald, TileID.Ruby, TileID.Diamond };
            ushort gem = gems[rand.Next(gems.Length)];
            float r = 8f + 5f * rand.NextFloat();
            int cy = top[x] - (int)(r * 0.35f);
            int salt = rand.Next(1000);

            // Its hollow walled with its gem (the game's gem walls, as its own gem caves have).
            walling = GemWall(gem);

            for (int dx = -(int)r - 1; dx <= (int)r + 1; dx++)
            {
                for (int dy = -(int)r - 1; dy <= (int)r + 1; dy++)
                {
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.6f * Noise2(dx * 0.5f, dy * 0.5f, seed + salt);

                    if (d > r)
                        continue;

                    if (d > r - 1.8f)
                        Set(x + dx, cy + dy, Dark);
                    else if (d > r - 3.4f)
                        Set(x + dx, cy + dy, gem);
                    else
                    {
                        // Over the sediment's top it was water: a wall behind it all the same, set
                        // here, as the cutting puts one only where there was rock.
                        Open(x + dx, cy + dy);

                        if (Main.tile[x + dx, cy + dy].WallType == 0 && cy + dy > VastOcean.Surface && x + dx > 0 && x + dx < VastOcean.Wide - 1 && cy + dy < VastOcean.High - 1)
                        {
                            Tile inside = Main.tile[x + dx, cy + dy];
                            inside.WallType = walling;
                            walled.Add((cy + dy) * VastOcean.Wide + x + dx);
                        }
                    }
                }
            }

            walling = NoWalls;

            // Broken open along its top, the break ragged.
            int open = (int)(r * 0.6f);

            for (int dx = -open; dx <= open; dx++)
            {
                int down = (int)(r * 0.35f + 2.5f * Noise(dx / 1.6f, seed + salt + 1));

                for (int dy = -(int)r - 2; dy <= -down; dy++)
                    Open(x + dx, cy + dy);
            }

            // Its floor: the lowest open cell of each column in it, over the gem.
            for (int dx = -(int)r + 3; dx <= (int)r - 3; dx++)
            {
                for (int y = cy + (int)r; y > cy - (int)r; y--)
                {
                    if (!Main.tile[x + dx, y].HasTile && Main.tile[x + dx, y + 1].HasTile && Main.tile[x + dx, y + 1].TileType == gem)
                    {
                        if (rand.Next(3) == 0)
                            gemSpots.Add(new Point(x + dx, y));

                        break;
                    }
                }
            }

            Log.Add(("geode", x, cy));
        }

        /// <summary>
        /// A row of standing stones half in the sand: tall stones three wide, their tops worn,
        /// one pair of them with a stone laid over them, one fallen and lying on its side.
        /// </summary>
        private static void StandingStones(int x, UnifiedRandom rand)
        {
            int count = 5 + rand.Next(3);
            int gap = 9 + rand.Next(3);
            int x0 = x - count * gap / 2;
            int lintel = rand.Next(count - 1);
            int fallen = rand.Next(count);
            int salt = rand.Next(1000);

            for (int k = 0; k < count; k++)
            {
                int sx = x0 + k * gap;
                int ground = Math.Min(top[sx], Math.Min(top[sx + 1], top[sx + 2]));

                // The fallen one: a slab on its side across the sand.
                if (k == fallen && k != lintel && k != lintel + 1)
                {
                    int lie = 8 + rand.Next(5);

                    for (int dx = 0; dx < lie; dx++)
                    {
                        int fx = sx - 2 + dx;
                        int g = top[fx];

                        for (int y = g - 3 + (dx == 0 || dx == lie - 1 ? 1 : 0); y < g + 1; y++)
                            Set(fx, y, Rock);
                    }

                    continue;
                }

                int high = 13 + rand.Next(9);

                if (k == lintel || k == lintel + 1)
                    high = 18;

                Stone(sx, ground, high, salt + k);

                if (k == lintel)
                {
                    int nx = sx + gap;
                    int ny = Math.Min(top[nx], Math.Min(top[nx + 1], top[nx + 2]));
                    int lintelRow = Math.Min(ground, ny) - high - 1;

                    // The pair the same height: the lower foot's stone carried up to the other's.
                    Stone(sx, ground, ground - lintelRow - 1, salt + k);
                    Stone(nx, ny, ny - lintelRow - 1, salt + k + 1);

                    for (int lx = sx - 1; lx <= nx + 3; lx++)
                    {
                        Set(lx, lintelRow, Rock);
                        Set(lx, lintelRow - 1, Rock);
                    }

                    k++;
                }
            }

            Log.Add(("standing stones", x, top[x]));
        }

        /// <summary>One standing stone three wide from four rows under its ground up, its top worn round.</summary>
        private static void Stone(int x, int ground, int high, int salt)
        {
            for (int dx = 0; dx < 3; dx++)
            {
                int worn = dx == 1 ? 0 : 1 + (int)(Hash(dx, salt) * 2f);

                for (int y = ground - high + worn; y < ground + 4; y++)
                    Set(x + dx, y, Rock);
            }
        }

        /// <summary>A sinkhole: a round hole down through the sediment, opening out into a hollow at its foot.</summary>
        private static void Sinkhole(int x, UnifiedRandom rand)
        {
            int depth = 30 + rand.Next(51);
            float r = 5f + 6f * rand.NextFloat();
            int y0 = top[x];

            // Walled from the ground's top down (what stands on the ground over it is cut away
            // with no wall behind it).
            for (int y = y0 - 2; y < y0 + depth; y++)
            {
                float rr = r + 1.5f * Noise(y / 5f, seed + 71);

                walling = y >= y0 ? WallByRock : NoWalls;

                for (int dx = -(int)rr; dx <= (int)rr; dx++)
                    Open(x + dx, y);
            }

            walling = WallByRock;

            Chamber(x, y0 + depth, r * 2.2f, r * 1.3f);
            walling = NoWalls;
            Hollows.Add((Hollow.Grotto, x, y0 + depth, r * 2.2f, r * 1.3f));

            Log.Add(("sinkhole", x, y0));
        }

        // ---- the rocks and the pillars --------------------------------------------------------

        /// <summary>"Ocasional rocks": now and then over the sediment, a lone rock lying half buried, of stone or of granite.</summary>
        private static void Rocks(UnifiedRandom rand)
        {
            int count = (Right - Left) / 60;

            for (int i = 0; i < count; i++)
            {
                int x = rand.Next(Left + 10, Right - 10);

                if (Math.Abs(x - trenchX) < trenchHalf + 120f || mount[x] >= 1f || !Free(x - 8, x + 8))
                    continue;

                ushort type = rand.Next(3) == 0 ? Dark : Rock;
                int half = 1 + rand.Next(4);
                int high = 2 + rand.Next(5);
                int ground = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                // Not over a hole.
                if (ground < 0 || ground > top[x] + 2)
                    continue;

                for (int dx = -half; dx <= half; dx++)
                {
                    float u = (float)dx / (half + 1);
                    int rise = (int)Math.Round(high * Math.Sqrt(1f - u * u) + 0.8f * Noise((x + dx) / 1.7f, seed + 81));

                    for (int y = ground - rise; y < ground + 2; y++)
                        Set(x + dx, y, type);
                }

                Log.Add(("rock", x, ground));
            }
        }

        /// <summary>
        /// "Even smaller rocks boulders too": pebbles and small boulders all over the sediment
        /// and the gentler sides of the ranges, one to three tiles most of them, a few a little
        /// bigger, some alone and some in little groups; stone mostly, granite now and then.
        /// Laid on the ground as it is, never down a hole, never in the trench, never on an odd
        /// thing.
        /// </summary>
        private static void Boulders(UnifiedRandom rand)
        {
            int tries = (Right - Left) / 14;

            for (int i = 0; i < tries; i++)
            {
                int x = rand.Next(Left + 10, Right - 10);
                int group = rand.Next(6) == 0 ? 3 + rand.Next(4) : 1;

                for (int k = 0; k < group; k++)
                {
                    int bx = k == 0 ? x : x + rand.Next(-7, 8);

                    if (Math.Abs(bx - trenchX) < trenchHalf + 120f || mount[bx] > PillarsUpTo || !Free(bx - 3, bx + 3))
                        continue;

                    int ground = FirstSolid(bx, VastOcean.Surface + 1, VastOcean.High - 2);

                    // On the ground, not down a hole nor on a steep side.
                    if (ground < 0 || ground > top[bx] + 2 || Math.Abs(top[bx + 2] - top[bx - 2]) > 4)
                        continue;

                    ushort type = rand.Next(4) == 0 ? Dark : Rock;
                    int size = rand.Next(10);

                    if (size < 5)
                    {
                        // A pebble: one tile, or two side by side.
                        Set(bx, ground - 1, type);

                        if (size < 2)
                            Set(bx + 1, ground - 1, type);
                    }
                    else if (size < 9)
                    {
                        // A small boulder: two or three across, two high in the middle, sunk a row.
                        int w = 2 + rand.Next(2);

                        for (int dx = 0; dx < w; dx++)
                        {
                            Set(bx + dx, ground, type);
                            Set(bx + dx, ground - 1, type);
                        }

                        Set(bx + w / 2, ground - 2, type);

                        if (w == 3 && rand.Next(2) == 0)
                            Set(bx + rand.Next(2), ground - 2, type);
                    }
                    else
                    {
                        // A boulder a little bigger: four or five across, three high, rounded.
                        int w = 4 + rand.Next(2);

                        for (int dx = 0; dx < w; dx++)
                        {
                            float u = (dx + 0.5f) / w * 2f - 1f;
                            int high = 1 + (int)Math.Round(2f * Math.Sqrt(1f - u * u));

                            for (int y = ground - high; y <= ground; y++)
                                Set(bx + dx, y, type);
                        }
                    }

                    Log.Add(("boulder", bx, ground));
                }
            }
        }

        /// <summary>
        /// Clusters of spires, ice and rock by turns, "ocasional ... pointy and curl in clusters":
        /// sixteen of them over the whole width, on the open floor, a spire or two of a
        /// cluster at a range's foot perhaps (where a range stands no more than
        /// <see cref="PillarsUpTo"/> rows over the sediment), none in the trench nor among the
        /// odd things. The first spire in the cluster's middle and the tallest.
        /// </summary>
        private static void Pillars(UnifiedRandom rand)
        {
            const int clusters = 16;
            int placed = 0;

            for (int tries = 0; placed < clusters && tries < 400; tries++)
            {
                int x = rand.Next(Left + 40, Right - 40);
                int count = 3 + rand.Next(7);
                int spread = 12 + count * 2;

                if (Math.Abs(x - trenchX) < trenchHalf + 120f || mount[x] >= 1f || !Free(x - spread - 12, x + spread + 12))
                    continue;

                Taken(x - spread - 12, x + spread + 12);

                // Ice and rock by turns.
                bool ice = placed++ % 2 == 0;
                ushort stone = ice ? TileID.IceBlock : rand.Next(3) == 0 ? Rock : Dark;
                float lean = (rand.NextFloat() * 2f - 1f) * 0.5f;

                for (int k = 0; k < count; k++)
                {
                    // The first in its middle, the tallest; the rest about it.
                    int dx = k == 0 ? 0 : rand.Next(-spread, spread + 1);
                    int sx = x + dx;
                    int ground = FirstSolid(sx, VastOcean.Surface + 1, VastOcean.High - 2);

                    // Not down a hole, nor high on a range, nor on a steep side.
                    if (ground < 0 || ground > top[sx] + 6 || mount[sx] > PillarsUpTo || Math.Abs(top[sx + 3] - top[sx - 3]) > 6)
                        continue;

                    // The middle ones the tallest.
                    float near = 1f - Math.Min(1f, Math.Abs(dx) / (spread + 2f));
                    float high = (k == 0 ? 50f + 25f * rand.NextFloat() : 10f + 25f * near + 25f * rand.NextFloat() * near) * (ice ? 1.15f : 1f);
                    float width = 1.6f + 2.4f * rand.NextFloat() + high / 28f;

                    // Curling: away from the cluster's middle, more for the outer ones, now and then over into a hook.
                    float curl = lean + Math.Sign(dx) * (0.25f + 0.6f * (1f - near)) * (rand.Next(4) == 0 ? 2.2f : 1f);

                    Spire(sx, ground + 2, high, width, curl, stone);
                }

                Log.Add((ice ? "ice pillars" : "rock pillars", x, top[x]));
            }
        }

        /// <summary>How high on a range's side a cluster of spires may stand, in rows over the sediment.</summary>
        private const float PillarsUpTo = 150f;

        /// <summary>
        /// One spire from its foot up: a path that starts straight up and bends as it goes
        /// (more toward its tip), laid half a tile at a time as a disc that narrows to a point.
        /// </summary>
        private static void Spire(int x, int foot, float high, float width, float curl, ushort type)
        {
            float px = x + 0.5f, py = foot;
            float angle = -MathHelper.PiOver2;
            float length = high * 1.1f;
            int steps = (int)(length * 2f);

            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                float r = Math.Max(1.0f, width * (float)Math.Pow(1f - t, 0.85));

                for (int dx = -(int)r - 1; dx <= (int)r + 1; dx++)
                {
                    for (int dy = -(int)r - 1; dy <= (int)r + 1; dy++)
                    {
                        float fx = (int)px + dx + 0.5f - px, fy = (int)py + dy + 0.5f - py;

                        if (fx * fx + fy * fy <= r * r)
                            Set((int)px + dx, (int)py + dy, type);
                    }
                }

                angle += curl * 2.2f * t / steps * MathHelper.PiOver2;
                px += 0.5f * (float)Math.Cos(angle);
                py += 0.5f * (float)Math.Sin(angle);
            }
        }

        // ---- finishing ----------------------------------------------------------------------

        /// <summary>
        /// Every face of the new ground worn to the game's slopes (Tile.SmoothSlope: a step's edge
        /// sloped, a lone bump halved), in the band the floor is in, so that ridges, spires and
        /// cave walls rise and fall in slopes and not in stairs.
        /// </summary>
        private static void Smooth()
        {
            int highest = Highest();

            for (int x = 2; x < VastOcean.Wide - 2; x++)
            {
                for (int y = Math.Max(highest - 4, VastOcean.Surface + 2); y < VastOcean.High - 2; y++)
                {
                    Tile t = Main.tile[x, y];

                    if (!t.HasTile || !Main.tileSolid[t.TileType])
                        continue;

                    if (!Main.tile[x, y - 1].HasTile || !Main.tile[x, y + 1].HasTile || !Main.tile[x - 1, y].HasTile || !Main.tile[x + 1, y].HasTile)
                        Tile.SmoothSlope(x, y, false, false);
                }
            }
        }

        /// <summary>
        /// What the game would let fall (sand, silt, shell: TileID.Sets.Falling) with nothing
        /// solid under it: sand and shell made hardened sand, silt taken away. From the bottom up,
        /// so that what was over a cell put right is seen with it under it.
        /// </summary>
        public static void Settle()
        {
            int highest = Highest();

            for (int x = 1; x < VastOcean.Wide - 1; x++)
            {
                for (int y = VastOcean.High - 3; y >= Math.Max(highest - 4, VastOcean.Surface + 1); y--)
                {
                    Tile t = Main.tile[x, y];

                    if (!t.HasTile || !TileID.Sets.Falling[t.TileType])
                        continue;

                    Tile under = Main.tile[x, y + 1];

                    if (under.HasTile && Main.tileSolid[under.TileType] && !Main.tileSolidTop[under.TileType])
                        continue;

                    if (t.TileType == TileID.Silt)
                        Open(x, y);
                    else
                        t.TileType = TileID.HardenedSand;
                }
            }
        }

        /// <summary>The highest row of the ground anywhere (the least), from the plan.</summary>
        private static int Highest()
        {
            int highest = VastOcean.High;

            for (int x = 0; x < VastOcean.Wide; x++)
                highest = Math.Min(highest, top[x]);

            // The spires and the outcrops stand over the plan's ground, never more than this.
            return highest - 90;
        }

        /// <summary>The first solid row from the surface down in each column, as it is now.</summary>
        private static int[] Tops()
        {
            var tops = new int[VastOcean.Wide];

            for (int x = 0; x < VastOcean.Wide; x++)
                tops[x] = Math.Max(0, FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2));

            return tops;
        }

        /// <summary>
        /// What goes down as an object once the water is in: the floor's own living things and
        /// vents where each lives (<see cref="PlantLife"/>, put down by <paramref name="plant"/>;
        /// none if it is null); bone worms (the game's coral) on the bones, and mussels and clams
        /// (its shells) where the vents, the seeps and the bones have them; under the ground's top
        /// (in the caves, the trench's caverns and the sinkholes' hollows), stalactites under
        /// rock, ice and sandstone, and stalagmites on rock (the game's own, WorldGen.PlaceTight,
        /// as its caves have them): under <see cref="CeilingDrips"/> of the ceilings' cells and
        /// on <see cref="FloorDrips"/> of the floors'; and in the geodes, loose gems on their
        /// floors.
        /// </summary>
        public static void Furnish(int[] tops, Planter plant)
        {
            var rand = new UnifiedRandom(seed ^ 0x57A1AC);

            if (plant != null)
                PlantLife(plant, new UnifiedRandom(seed ^ 0x11FE));

            // Coral stands on any whole solid tile with water over it, and so does a shell
            // (WorldGen.PlaceTile; kept on every ground the floor has, sulfide, carbonate and
            // bone among them, when the game frames it again: tried in the preview).
            foreach (Point p in wormSpots)
                Stand(p, TileID.Coral, 0);

            foreach (Point p in shellSpots)
                Stand(p, TileID.BeachPiles, Mussel(rand));

            // The dripstone the game hangs and stands, by request ("need more of these dripstone
            // ambience objects in caves"): under nearly half of every ceiling, on nearly a fifth
            // of every floor; none in an eel's crevice, which is kept clear for it, nor on or under
            // a floating rock, which floats.
            var crevice = new HashSet<int>();

            foreach (var (lx, ly, dx, dy, length) in Lairs)
            {
                int px = dy == 0 ? 0 : 1, py = dy == 0 ? 1 : 0;

                for (int k = -1; k <= length + 1; k++)
                {
                    for (int j = -1; j <= 2; j++)
                        crevice.Add((ly + dy * k + py * j) * VastOcean.Wide + lx + dx * k + px * j);
                }
            }

            foreach (var (fx, fy, _) in FloatingRocks)
            {
                crevice.Add((fy - 1) * VastOcean.Wide + fx);
                crevice.Add((fy + 1) * VastOcean.Wide + fx);
            }

            for (int x = Left; x < Right; x++)
            {
                for (int y = tops[x] + 3; y < VastOcean.High - 4; y++)
                {
                    Tile here = Main.tile[x, y];

                    if (here.HasTile || crevice.Contains(y * VastOcean.Wide + x))
                        continue;

                    Tile over = Main.tile[x, y - 1];
                    Tile under = Main.tile[x, y + 1];

                    if (Hangs(over) && !under.HasTile)
                    {
                        if (rand.NextFloat() < CeilingDrips)
                            WorldGen.PlaceTight(x, y);
                    }
                    else if (Hangs(under) && !over.HasTile && rand.NextFloat() < FloorDrips)
                    {
                        WorldGen.PlaceTight(x, y);
                    }
                }
            }

            foreach (Point p in gemSpots)
            {
                Tile under = Main.tile[p.X, p.Y + 1];

                if (!Main.tile[p.X, p.Y].HasTile && under.HasTile && IsGem(under.TileType))
                    WorldGen.PlaceTile(p.X, p.Y, TileID.ExposedGems, true, false, -1, GemStyle(under.TileType));
            }
        }

        // ---- the life on it --------------------------------------------------------------

        /// <summary>Puts one of the floor's own things (SeaLife) down at the cell it goes by (its bottom row, its middle column), with this picture; true if it went. The place hands in the game's own (SeaLifeTile.Plant); the preview its own.</summary>
        public delegate bool Planter(int x, int y, SeaLife.Kind kind, int style);

        /// <summary>Each thing put down, for the preview: what, which picture, and the cell it went by.</summary>
        public static readonly List<(SeaLife.Kind kind, int style, int x, int y)> Planted = new List<(SeaLife.Kind, int, int, int)>();

        /// <summary>
        /// The floor's own life put down, each where its kind lives (the notes for section 16.87
        /// have the sources):
        ///   - on each live black smoker's mouth, the mouth's tile (its smoke), and on each white
        ///     chimney's top, the white one's (its shimmer);
        ///   - about each field of black smokers, giant tube worms in their clusters;
        ///   - at each seep, cracks letting out gas, bushes of seep tube worms, an anemone or two;
        ///     in each mud volcano's crater a crack or two, and one in each pockmark still letting
        ///     gas out;
        ///   - on each coral mound and over the guyot's flat top, a reef: the cold-water corals
        ///     most, bubblegum and black coral, sea lilies, sponges, anemones;
        ///   - on the ranges' sides, wherever a ledge is level enough, the same here and there,
        ///     bamboo coral among them;
        ///   - over the plains of clay and mud, sea pens and xenophyophores most, glass sponges,
        ///     tube anemones; on the pale ooze over the compensation depth, more sponges; more
        ///     xenophyophores among the nodules;
        ///   - in the trench, sparse, sea pens, anemones, sponges and xenophyophores.
        /// Each only where the ground is whole and level under all of it and nothing is in its
        /// way (<see cref="Plant"/>).
        /// </summary>
        private static void PlantLife(Planter plant, UnifiedRandom rand)
        {
            foreach (Point m in ventMouths)
            {
                Whole(m.X, m.Y);
                Plant(plant, SeaLife.Kind.VentMouth, m.X, m.Y - 1, 0);
            }

            foreach (Point t in whiteTops)
            {
                Whole(t.X, t.Y);
                Plant(plant, SeaLife.Kind.WhiteVentMouth, t.X, t.Y - 1, 0);
            }

            foreach (var (x, half) in ventFields)
                Scatter(plant, rand, x, half + 16, 3 + rand.Next(3), r => SeaLife.Kind.GiantTubeWorms);

            foreach (var (x, half) in seeps)
            {
                Scatter(plant, rand, x, half, 2 + rand.Next(3), r => SeaLife.Kind.SeepVent);
                Scatter(plant, rand, x, half, 1 + rand.Next(3), r => SeaLife.Kind.SeepTubeWorms);
                Scatter(plant, rand, x, half, rand.Next(3), r => SeaLife.Kind.DeepAnemone, r => r.Next(2));
            }

            foreach (var (x, half) in craters)
                Scatter(plant, rand, x, half, 1 + rand.Next(2), r => SeaLife.Kind.SeepVent);

            foreach (var (x, half) in pocks)
                Scatter(plant, rand, x, half, 1, r => SeaLife.Kind.SeepVent);

            foreach (var (x, half) in reefs)
                Scatter(plant, rand, x, half, 1 + half / 4, Reef, Rocky);

            // The ranges' sides: a try every four columns or so of each, most on ground too steep.
            foreach (Range range in ranges)
            {
                int tries = (int)(range.Half / 4f);

                for (int k = 0; k < tries; k++)
                {
                    int x = range.Middle + (rand.Next(2) == 0 ? -1 : 1) * (int)(range.Half * (0.1f + 1.2f * rand.NextFloat()));

                    // Not among the vents, the white chimneys or the rest laid on a side: their own live there.
                    if (x < Left || x > Right || mount[x] < 15f || !Free(x - 3, x + 3))
                        continue;

                    SeaLife.Kind kind = Flank(rand);

                    PlantNear(plant, kind, x, 4, VastOcean.Surface + 1, Rocky(rand, kind));
                }
            }

            // The plains, a try every fourteen columns or so, four in five of them.
            for (int x = Left + 20; x < Right - 20; x += 8 + rand.Next(13))
            {
                if (mount[x] >= 1f || Math.Abs(x - trenchX) < trenchHalf + 150f || !Free(x - 2, x + 2) || rand.Next(5) == 0)
                    continue;

                int g = FirstSolid(x, VastOcean.Surface + 1, VastOcean.High - 2);

                if (g < 0)
                    continue;

                bool ooze = Main.tile[x, g].TileType == TileID.Sand;
                SeaLife.Kind kind = ooze ? OozeLife(rand) : Plain(rand);

                PlantNear(plant, kind, x, 3, VastOcean.Surface + 1, kind == SeaLife.Kind.DeepAnemone && !ooze ? 2 : rand.Next(SeaLife.Styles(kind)));
            }

            foreach (var (x, half) in noduleFields)
                Scatter(plant, rand, x, half, half / 20, r => SeaLife.Kind.Xenophyophore);

            // The trench: from its mouth down, the first ground in each column, its ledges and its bowl.
            for (int x = (int)(trenchX - trenchHalf - 20); x <= trenchX + trenchHalf + 20; x += 6 + rand.Next(8))
            {
                if (rand.Next(10) >= 3)
                    continue;

                int roll = rand.Next(100);
                SeaLife.Kind kind = roll < 35 ? SeaLife.Kind.SeaPen : roll < 65 ? SeaLife.Kind.Xenophyophore : roll < 85 ? SeaLife.Kind.DeepAnemone : SeaLife.Kind.GlassSponge;

                PlantNear(plant, kind, x, 3, trenchTop, kind == SeaLife.Kind.DeepAnemone ? 2 : rand.Next(SeaLife.Styles(kind)));
            }
        }

        /// <summary>Tries to put down so many of a kind within this many columns of a column, eight tries for each (each one looking two columns either side of where it falls).</summary>
        private static void Scatter(Planter plant, UnifiedRandom rand, int x, int half, int count, Func<UnifiedRandom, SeaLife.Kind> kind, Func<UnifiedRandom, SeaLife.Kind, int> style = null)
        {
            for (int made = 0, tries = 0; made < count && tries < count * 8; tries++)
            {
                SeaLife.Kind k = kind(rand);
                int s = style != null ? style(rand, k) : rand.Next(SeaLife.Styles(k));

                if (PlantNear(plant, k, x + rand.Next(-half, half + 1), 2, VastOcean.Surface + 1, s))
                    made++;
            }
        }

        private static void Scatter(Planter plant, UnifiedRandom rand, int x, int half, int count, Func<UnifiedRandom, SeaLife.Kind> kind, Func<UnifiedRandom, int> style)
            => Scatter(plant, rand, x, half, count, kind, (r, k) => style(r));

        /// <summary>A reef's: the cold-water corals most.</summary>
        private static SeaLife.Kind Reef(UnifiedRandom rand)
        {
            int roll = rand.Next(100);

            return roll < 34 ? SeaLife.Kind.ColdWaterCoral : roll < 48 ? SeaLife.Kind.BubblegumCoral : roll < 60 ? SeaLife.Kind.BlackCoral
                : roll < 70 ? SeaLife.Kind.SeaLily : roll < 80 ? SeaLife.Kind.CupSponge : roll < 88 ? SeaLife.Kind.GlassSponge
                : roll < 96 ? SeaLife.Kind.DeepAnemone : SeaLife.Kind.BambooCoral;
        }

        /// <summary>A range's side's: the octocorals more.</summary>
        private static SeaLife.Kind Flank(UnifiedRandom rand)
        {
            int roll = rand.Next(100);

            return roll < 22 ? SeaLife.Kind.BubblegumCoral : roll < 42 ? SeaLife.Kind.BambooCoral : roll < 60 ? SeaLife.Kind.BlackCoral
                : roll < 76 ? SeaLife.Kind.ColdWaterCoral : roll < 86 ? SeaLife.Kind.SeaLily : roll < 94 ? SeaLife.Kind.CupSponge
                : SeaLife.Kind.GlassSponge;
        }

        /// <summary>The clay and mud plains': sea pens and xenophyophores most.</summary>
        private static SeaLife.Kind Plain(UnifiedRandom rand)
        {
            int roll = rand.Next(100);

            return roll < 34 ? SeaLife.Kind.SeaPen : roll < 64 ? SeaLife.Kind.Xenophyophore : roll < 74 ? SeaLife.Kind.GlassSponge
                : roll < 86 ? SeaLife.Kind.DeepAnemone : roll < 92 ? SeaLife.Kind.SeaLily : SeaLife.Kind.CupSponge;
        }

        /// <summary>The pale ooze's, over the compensation depth: sponges more.</summary>
        private static SeaLife.Kind OozeLife(UnifiedRandom rand)
        {
            int roll = rand.Next(100);

            return roll < 22 ? SeaLife.Kind.SeaPen : roll < 44 ? SeaLife.Kind.CupSponge : roll < 62 ? SeaLife.Kind.GlassSponge
                : roll < 76 ? SeaLife.Kind.DeepAnemone : roll < 90 ? SeaLife.Kind.Xenophyophore : SeaLife.Kind.SeaLily;
        }

        /// <summary>The picture of one on hard ground: an anemone on rock is one of the two that are not the tube anemone.</summary>
        private static int Rocky(UnifiedRandom rand, SeaLife.Kind kind) => kind == SeaLife.Kind.DeepAnemone ? rand.Next(2) : rand.Next(SeaLife.Styles(kind));

        /// <summary>
        /// One thing put down with its middle column here, on the first ground in that column from
        /// <paramref name="from"/> down: only if the ground is level under all of it (the first
        /// ground in each of its columns in the same row, each of a kind the game lets a thing
        /// stand on) and nothing is in the cells it takes. A tile of the ground under it worn to a
        /// slope or a half (the edge of a step, where the ground was smoothed) is made whole first,
        /// as the game wants it whole. True if it went.
        /// </summary>
        private static bool Plant(Planter plant, SeaLife.Kind kind, int x, int from, int style)
        {
            int wide = SeaLife.Wide(kind), tall = SeaLife.Tall(kind);
            int x0 = x - SeaLife.OriginX(kind);

            if (x0 < Left || x0 + wide > Right)
                return false;

            int g = FirstSolid(x, from, VastOcean.High - 2);

            // The floor's own ground, never anything over the 8th section (the island's underside,
            // found first from the surface down in its columns, by report of the preview: a sea
            // pen put on it).
            if (g < 0 || g < VastOcean.SectionTop(8))
                return false;

            for (int dx = 0; dx < wide; dx++)
            {
                Tile ground = Main.tile[x0 + dx, g];

                if (!ground.HasTile || !Main.tileSolid[ground.TileType] || Main.tileSolidTop[ground.TileType] || Main.tileNoAttach[ground.TileType])
                    return false;

                for (int dy = 1; dy <= tall; dy++)
                {
                    if (Main.tile[x0 + dx, g - dy].HasTile)
                        return false;
                }
            }

            for (int dx = 0; dx < wide; dx++)
                Whole(x0 + dx, g);

            if (!plant(x, g - 1, kind, style))
                return false;

            Planted.Add((kind, style, x, g - 1));
            return true;
        }

        /// <summary>The same, tried at the column given and then a column at a time either side of it, out to <paramref name="reach"/>: on the first that takes it.</summary>
        private static bool PlantNear(Planter plant, SeaLife.Kind kind, int x, int reach, int from, int style)
        {
            for (int k = 0; k <= reach * 2; k++)
            {
                int dx = (k + 1) / 2 * (k % 2 == 0 ? 1 : -1);

                if (Plant(plant, kind, x + dx, from, style))
                    return true;
            }

            return false;
        }

        /// <summary>This tile made whole again (the top of a chimney, worn to a half or a slope with the rest), to carry its vent's tile.</summary>
        private static void Whole(int x, int y)
        {
            if (x < 1 || x >= VastOcean.Wide - 1 || y <= VastOcean.Surface || y >= VastOcean.High - 1)
                return;

            Tile t = Main.tile[x, y];

            if (t.HasTile)
            {
                t.Slope = SlopeType.Solid;
                t.IsHalfBlock = false;
            }
        }

        /// <summary>
        /// One thing stood on the ground by a spot: on the first solid tile in its column from six
        /// rows over it down to eight under (the ground may have been cut or worn since the spot
        /// was taken), or failing that in the column either side, where that tile is whole and
        /// the cell over it empty; left out if none will take it.
        /// </summary>
        private static void Stand(Point p, ushort type, int style)
        {
            for (int k = 0; k < 3; k++)
            {
                int x = p.X + (k == 0 ? 0 : k == 1 ? 1 : -1);
                int g = FirstSolid(x, p.Y - 6, p.Y + 8);

                if (g < 0 || x < Left || x > Right)
                    continue;

                Tile ground = Main.tile[x, g];

                if (ground.Slope != SlopeType.Solid || ground.IsHalfBlock || Main.tile[x, g - 1].HasTile)
                    continue;

                WorldGen.PlaceTile(x, g - 1, type, true, false, -1, style);

                if (Main.tile[x, g - 1].HasTile && Main.tile[x, g - 1].TileType == type)
                    return;
            }
        }

        /// <summary>A living mussel bed's shells, by the game's beach shells (Tiles_324): its sea shell mostly, a lightning whelk or a tulip shell now and then (snails crowd the vents as well); never a starfish.</summary>
        private static int Mussel(UnifiedRandom rand)
        {
            int roll = rand.Next(10);

            return roll < 7 ? 0 : roll < 9 ? 2 : 3;
        }

        /// <summary>Whole ground the game hangs a stalactite from (or stands one on): rock, granite, a glowing moss, ice, sandstone, hardened sand.</summary>
        private static bool Hangs(Tile t) => t.HasTile && t.Slope == SlopeType.Solid && !t.IsHalfBlock
            && (t.TileType == Rock || t.TileType == Dark || Main.tileMoss[t.TileType] || t.TileType == TileID.IceBlock || t.TileType == TileID.Sandstone || t.TileType == TileID.HardenedSand);

        private static bool IsGem(ushort type) => type == TileID.Amethyst || type == TileID.Topaz || type == TileID.Sapphire || type == TileID.Emerald || type == TileID.Ruby || type == TileID.Diamond;

        /// <summary>The style of the loose gem that goes with the gem in the rock (Tiles_178, a column each: amethyst, topaz, sapphire, emerald, ruby, diamond, amber).</summary>
        private static int GemStyle(ushort type) => type == TileID.Amethyst ? 0 : type == TileID.Topaz ? 1 : type == TileID.Sapphire ? 2 : type == TileID.Emerald ? 3 : type == TileID.Ruby ? 4 : 5;

        // ---- the water ----------------------------------------------------------------------

        /// <summary>
        /// The sea poured in: every cell from the surface row down that holds nothing solid, full
        /// (the surface row half, as the game leaves its own sea): the open water, the caves, the
        /// trench and the holes; nothing over the surface.
        /// </summary>
        public static void Water()
        {
            for (int x = 0; x < VastOcean.Wide; x++)
            {
                for (int y = VastOcean.Surface; y < VastOcean.High; y++)
                {
                    Tile t = Main.tile[x, y];

                    if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    {
                        t.LiquidAmount = 0;
                        continue;
                    }

                    t.LiquidType = LiquidID.Water;
                    t.LiquidAmount = (byte)(y == VastOcean.Surface ? 127 : 255);
                }
            }
        }

        // ---- cells ------------------------------------------------------------------------------

        private static void Set(int x, int y, ushort type)
        {
            if (x < 1 || y <= VastOcean.Surface || x >= VastOcean.Wide - 1 || y >= VastOcean.High - 1)
                return;

            Tile t = Main.tile[x, y];
            t.HasTile = true;
            t.TileType = type;
            t.Slope = SlopeType.Solid;
            t.IsHalfBlock = false;
            t.TileColor = 0;
            t.LiquidAmount = 0;
        }

        /// <summary>The cell emptied of its tile; and, while caves are being cut (<see cref="walling"/>), a wall put behind it if it was rock (<see cref="WallFor"/>).</summary>
        private static void Open(int x, int y)
        {
            if (x < 1 || y <= VastOcean.Surface || x >= VastOcean.Wide - 1 || y >= VastOcean.High - 1)
                return;

            Tile t = Main.tile[x, y];

            if (walling != NoWalls && t.HasTile)
            {
                t.WallType = walling == WallByRock ? WallFor(t.TileType, x, y) : walling;
                walled.Add(y * VastOcean.Wide + x);
            }

            t.HasTile = false;
            t.Slope = SlopeType.Solid;
            t.IsHalfBlock = false;
            t.TileColor = 0;
        }

        /// <summary>The tile in this cell painted, if there is one.</summary>
        private static void Paint(int x, int y, byte paint)
        {
            if (x < 1 || y <= VastOcean.Surface || x >= VastOcean.Wide - 1 || y >= VastOcean.High - 1)
                return;

            Tile t = Main.tile[x, y];

            if (t.HasTile)
                t.TileColor = paint;
        }

        private static int FirstSolid(int x, int from, int to)
        {
            if (x < 0 || x >= VastOcean.Wide)
                return -1;

            for (int y = Math.Max(0, from); y <= Math.Min(VastOcean.High - 1, to); y++)
            {
                Tile t = Main.tile[x, y];

                if (t.HasTile && Main.tileSolid[t.TileType])
                    return y;
            }

            return -1;
        }

        // ---- noise --------------------------------------------------------------------------------

        private static float Hash(int i, int salt)
        {
            unchecked
            {
                uint h = (uint)i * 374761393u + (uint)salt * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static float Hash(int x, int y, int salt) => Hash(unchecked(x * 73856093 ^ y * 19349663), salt);

        /// <summary>Smooth noise along a line, -1 to 1.</summary>
        private static float Noise(float t, int salt)
        {
            int i = (int)Math.Floor(t);
            float f = t - i;
            float s = f * f * (3f - 2f * f);

            return MathHelper.Lerp(Hash(i, salt), Hash(i + 1, salt), s) * 2f - 1f;
        }

        /// <summary>Smooth noise over the plane, -1 to 1.</summary>
        private static float Noise2(float x, float y, int salt)
        {
            int i = (int)Math.Floor(x), j = (int)Math.Floor(y);
            float fx = x - i, fy = y - j;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            float a = MathHelper.Lerp(Hash(i, j, salt), Hash(i + 1, j, salt), sx);
            float b = MathHelper.Lerp(Hash(i, j + 1, salt), Hash(i + 1, j + 1, salt), sx);

            return MathHelper.Lerp(a, b, sy) * 2f - 1f;
        }

        /// <summary>Noise with sharp crests, -1 to 1.</summary>
        private static float Ridged(float t, int salt) => 1f - 2f * Math.Abs(Noise(t, salt));
    }
}
