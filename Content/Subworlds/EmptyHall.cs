using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The first place: a long hall of Gray Brick, shut on every side, and lit. An arena.
    ///
    /// 400 by 320 tiles, of which 317 by 237 can be seen (Subworld: the edges). Of what can
    /// be seen: the ground is the last forty rows, the ceiling the first thirty-six, and a
    /// wall five columns thick stands at each end, all Gray Brick; what is left between them
    /// is the hall, 307 tiles long and 161 high, with no platform in it and nothing to stand
    /// on but its floor. The brick runs on past every edge that can be seen, so a screen
    /// wider than the hall sees no end to it. No music. The door back stands on the floor
    /// against the left wall.
    ///
    /// BEHIND IT ALL is Stone Wall, laid as masonry and not as one flat sheet: a dado of Gray
    /// Brick three rows high along the floor, a rail of Stone Slab over it, pilasters of
    /// Stone Slab three wide from the rail to a capital under the frieze (one every twelve
    /// columns from the hall's middle, and one against the wall at each end), a frieze of
    /// Gray Brick under the ceiling with its own rail, a course of Stone Slab every
    /// twenty-four rows up the height of it, and the Stone between them patched here and
    /// there with brick.
    ///
    /// LIT, since a wall behind everything keeps the sky's light out: a torch on every
    /// pilaster every twelve rows from the first row over a head, a torch in the middle of
    /// every bay between two pilasters six rows out of step with those, one more on each of
    /// them under the rail at the head of the wall, and a Tiki Torch standing on the floor in
    /// the middle of every bay but the two at the ends (at the left one the door is, and
    /// whoever comes back to life here stands; the right one is kept the same: those two
    /// have a torch low on the wall instead). Light falls off by about a tenth a tile,
    /// along and up added together, and a Tiki Torch gives its light from all three of its
    /// tiles: nowhere in the hall is further than six tiles from a flame by that count (a
    /// little over half a torch's light), and nowhere on the floor further than four (seven
    /// tenths).
    /// </summary>
    public class EmptyHall : Subworld
    {
        /// <summary>The ground: this many rows of brick.</summary>
        public const int FloorThick = 40;

        /// <summary>The ceiling: this many rows of brick, of those that can be seen.</summary>
        public const int CeilingThick = 36;

        /// <summary>The wall at each end: this many columns of brick, of those that can be seen.</summary>
        public const int WallThick = 5;

        /// <summary>From the middle of one pilaster to the middle of the next.</summary>
        public const int Bay = 12;

        /// <summary>Up the wall, a torch on a pilaster every this many rows; the ones between the pilasters half of it out of step.</summary>
        public const int TorchRise = 12;

        /// <summary>The lowest torches on the pilasters: this many rows over the row that is stood in (so, over a head).</summary>
        public const int TorchFirst = 3;

        /// <summary>In the two bays at the ends, with no Tiki Torch, the torch low on the wall: as high as a Tiki Torch's head.</summary>
        public const int TorchLow = 2;

        /// <summary>Under the head of the wall no torch stands nearer than this to the one at its head.</summary>
        private const int TorchHeadGap = 4;

        /// <summary>A pilaster counted from the middle is left out if it would stand nearer than this to the one against an end wall.</summary>
        private const int EndBayLeast = 8;

        private const int DadoRows = 3;
        private const int FriezeRows = 2;
        private const int CourseEvery = 24;

        public override int Width => 400;

        public override int Height => 320;

        /// <summary>
        /// 4. The first was 250 rows high with ground three thick; the second, the same
        /// height with ground twenty-six thick; the third, 320 high with ground forty thick
        /// under open sky.
        /// </summary>
        public override int Layout => 4;

        /// <summary>The row the floor's top is: what is stood on.</summary>
        private int Floor => Bottom - FloorThick + 1;

        /// <summary>The ceiling's lowest row.</summary>
        private int Ceiling => Top + CeilingThick - 1;

        /// <summary>The hall's first and last open column, between its two walls.</summary>
        private int Inside => Left + WallThick;
        private int InsideEnd => Right - WallThick;

        /// <summary>The middle column of the hall: a pilaster stands on it, and the rest are counted from it both ways.</summary>
        private int Middle => (Inside + InsideEnd) / 2;

        /// <summary>
        /// The middle column of every pilaster, left to right: one against each end wall
        /// (three wide as the rest are, so its middle is a column in from the wall), and
        /// between them every twelfth column from the hall's middle.
        /// </summary>
        private int[] pilasters;

        private int[] Pilasters()
        {
            if (pilasters != null)
                return pilasters;

            var list = new List<int>();
            int first = Inside + 1;
            int last = InsideEnd - 1;

            list.Add(first);

            for (int x = first + EndBayLeast; x <= last - EndBayLeast; x++)
            {
                if ((x - Middle) % Bay == 0)
                    list.Add(x);
            }

            list.Add(last);

            return pilasters = list.ToArray();
        }

        /// <summary>Against the left wall, on the floor.</summary>
        public override Point Door => new Point(Inside, Floor - SubworldDoor.Tall);

        public override void Build()
        {
            // The shell: the ground as it was; the ceiling, and everything over it; a wall at
            // each end, and everything outside it.
            Fill(0, Floor, Width - 1, Bottom, TileID.GrayBrick);
            Fill(0, 0, Width - 1, Ceiling, TileID.GrayBrick);
            Fill(0, Ceiling + 1, Inside - 1, Floor - 1, TileID.GrayBrick);
            Fill(InsideEnd + 1, Ceiling + 1, Width - 1, Floor - 1, TileID.GrayBrick);

            Masonry();
            Lights();

            Stand((ushort)ModContent.TileType<SubworldDoor>(), Door.X, Door.Y, SubworldDoor.Wide, SubworldDoor.Tall);
        }

        /// <summary>How far a column is from the middle of the nearest pilaster.</summary>
        private int FromPilaster(int x)
        {
            int least = int.MaxValue;

            foreach (int middle in Pilasters())
                least = Math.Min(least, Math.Abs(x - middle));

            return least;
        }

        /// <summary>The wall behind the hall, in registers from the floor up.</summary>
        private void Masonry()
        {
            int top = Ceiling + 1;
            int high = Floor - top;

            for (int x = Inside; x <= InsideEnd; x++)
            {
                int off = FromPilaster(x);

                for (int y = top; y < Floor; y++)
                {
                    int up = Floor - 1 - y;      // rows over the row that is stood in
                    int down = y - top;          // rows under the ceiling

                    ushort wall;

                    if (up < DadoRows || down < FriezeRows)
                        wall = WallID.GrayBrick;
                    else if (up == DadoRows || down == FriezeRows)
                        wall = WallID.StoneSlab;
                    else if (off <= 1 || (down == FriezeRows + 1 && off <= 2))
                        wall = WallID.StoneSlab;                                   // a pilaster, and its capital a tile wider
                    else if ((up - DadoRows) % CourseEvery == 0 && up < high - FriezeRows - 4)
                        wall = WallID.StoneSlab;                                   // a course across the bays
                    else
                        wall = Patched(x, y) ? WallID.GrayBrick : WallID.Stone;

                    Tile tile = Main.tile[x, y];
                    tile.WallType = wall;
                }
            }
        }

        /// <summary>
        /// Whether the Stone here is patched with brick: about one tile in five, in lumps a
        /// few tiles across, and the same lumps every time the hall is laid out (it is
        /// worked out from the column and the row alone).
        /// </summary>
        private static bool Patched(int x, int y)
        {
            // Smooth noise: four corners of a five tile grid, blended.
            const int cell = 5;

            int gx = x / cell;
            int gy = y / cell;
            float fx = (x % cell) / (float)cell;
            float fy = (y % cell) / (float)cell;

            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Lump(gx, gy);
            float b = Lump(gx + 1, gy);
            float c = Lump(gx, gy + 1);
            float d = Lump(gx + 1, gy + 1);

            float value = MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);

            return value > 0.7f;
        }

        private static float Lump(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);

                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;

                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>The torches on the wall, and the Tiki Torches on the floor.</summary>
        private void Lights()
        {
            int[] on = Pilasters();

            for (int i = 0; i < on.Length; i++)
            {
                // Up every pilaster, from over a head.
                Column(on[i], TorchFirst);

                if (i + 1 == on.Length)
                    break;

                // And up the middle of the bay between it and the next, half a rise out of
                // step with them.
                int bay = (on[i] + on[i + 1]) / 2;

                Column(bay, TorchFirst + TorchRise / 2);

                // A flame on the floor in the middle of a bay: not in the two at the ends
                // (the left one is where the door is and where the dead come back; the
                // right one is kept the same). Those have one low on their wall instead,
                // as high as a Tiki Torch's head is.
                if (i == 0 || i + 2 == on.Length)
                    Torch(bay, Floor - 1 - TorchLow);
                else
                    Stand(TileID.Lamps, bay, Floor - 3, 1, 3);
            }
        }

        /// <summary>
        /// Torches up one column of the wall: every twelve rows from this many over the row
        /// that is stood in, and one at the head of the wall, under its rail, so the top of
        /// the hall is no darker than the rest of it.
        /// </summary>
        private void Column(int x, int first)
        {
            int head = Ceiling + 1 + FriezeRows + 1;

            for (int up = first; ; up += TorchRise)
            {
                int y = Floor - 1 - up;

                if (y < head + TorchHeadGap)
                    break;

                Torch(x, y);
            }

            Torch(x, head);
        }

        /// <summary>A torch on the wall behind it, lit, as the game frames one that hangs on a wall.</summary>
        private static void Torch(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return;

            Tile tile = Main.tile[x, y];

            if (tile.HasTile)
                return;

            tile.HasTile = true;
            tile.TileType = TileID.Torches;
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;
        }
    }
}
