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
    /// The first place: a hall of Gray Brick, shut on every side, and lit. An arena.
    ///
    /// 264 by 207 tiles, of which 181 by 124 can be seen (Subworld: the edges). Of what can
    /// be seen: the ground is the last forty rows, the ceiling the first thirty-six, and a
    /// wall five columns thick stands at each end, all Gray Brick; what is left between them
    /// is the hall, 171 tiles long and 48 high (fourteen bays, and two panels of wall one
    /// over the other), with no platform in it and nothing to stand on but its floor. The
    /// brick runs on past every edge that can be seen. No music. The door back stands on
    /// the floor against the left wall.
    ///
    /// BEHIND IT ALL is Stone Wall, laid as masonry and not as one flat sheet: a dado of Gray
    /// Brick three rows high along the floor, a rail of Stone Slab over it, pilasters of
    /// Stone Slab three wide from the rail to a capital under the frieze (fifteen of them,
    /// twelve columns apart, the first and the last against the two walls), a frieze of
    /// Gray Brick under the ceiling with its own rail, a course of Stone Slab across the
    /// bays where one panel ends and the next begins (twenty-four rows up: twenty rows of
    /// wall under it and twenty over), and the Stone between them patched here and there
    /// with brick. The door, three tall, stands in the dado: that band is counted into the
    /// lower panel.
    ///
    /// LIT, since a wall behind everything keeps the sky's light out: a torch on every
    /// pilaster every twelve rows from the first row over a head, a torch in the middle of
    /// every bay between two pilasters six rows out of step with those, one more on each of
    /// them under the rail at the head of the wall, and a Tiki Torch standing on the floor in
    /// the middle of every bay. Light falls off by about a tenth a tile, along and up added
    /// together, and a Tiki Torch gives its light from all three of its tiles: nowhere in
    /// the hall is further than six tiles from a flame by that count (a little over half a
    /// torch's light), but for the one column against each end wall, where it is seven
    /// between two torches of the pilaster there; and nowhere on the floor further than four
    /// (seven tenths).
    ///
    /// AT FOIGH'S END (Foigh.End) he stands under the fourth pilaster from the end wall
    /// (<see cref="EndColumn"/>) and his roar puts out every flame in the hall but the
    /// ones up that pilaster; they are lit again when it is over, and whenever the hall is
    /// loaded (<see cref="EmptyHallFlames"/>).
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

        /// <summary>
        /// The hall's length: this many bays, with a pilaster against the wall at each end of
        /// them. (Nine is the least, for a place two hundred columns wide: SubworldKeeper.Least.)
        /// </summary>
        public const int Bays = 14;

        /// <summary>
        /// Its height: this many panels of wall one over the other, each this many rows. The
        /// band along the floor that the door stands in is counted into the lowest of them.
        /// (Two is the least: with one, the place would be 183 rows high, under the two
        /// hundred a place has to be. SubworldKeeper.Least.)
        /// </summary>
        public const int Panels = 2;
        public const int PanelRows = 24;

        /// <summary>The hall itself, between its two walls and between its floor and its ceiling: columns, and rows.</summary>
        public const int Long = Bays * Bay + 3;
        public const int High = Panels * PanelRows;

        /// <summary>Up the wall, a torch on a pilaster every this many rows; the ones between the pilasters half of it out of step.</summary>
        public const int TorchRise = 12;

        /// <summary>The lowest torches on the pilasters: this many rows over the row that is stood in (so, over a head).</summary>
        public const int TorchFirst = 3;

        /// <summary>Under the head of the wall no torch stands nearer than this to the one at its head.</summary>
        private const int TorchHeadGap = 4;

        private const int DadoRows = 3;
        private const int FriezeRows = 2;

        /// <summary>What can be seen of it, and the edges the game keeps out of sight (Subworld: the edges).</summary>
        public override int Width => Margin + WallThick + Long + WallThick + FarMargin;

        public override int Height => Margin + CeilingThick + High + FloorThick + FarMargin;

        /// <summary>
        /// 5. The first was 250 rows high with ground three thick; the second, the same
        /// height with ground twenty-six thick; the third, 320 high with ground forty thick
        /// under open sky; the fourth, that size shut in and lit, its hall 307 long and 161
        /// high.
        /// </summary>
        public override int Layout => 5;

        /// <summary>The row the floor's top is: what is stood on.</summary>
        private int Floor => Bottom - FloorThick + 1;

        /// <summary>The ceiling's lowest row.</summary>
        private int Ceiling => Top + CeilingThick - 1;

        /// <summary>The hall's first and last open column, between its two walls.</summary>
        private int Inside => Left + WallThick;
        private int InsideEnd => Right - WallThick;

        /// <summary>
        /// The middle column of every pilaster, left to right: the first against the left
        /// wall (three wide as the rest are, so its middle is a column in from the wall), the
        /// rest a bay apart, and the last against the right wall.
        /// </summary>
        private int[] pilasters;

        private int[] Pilasters()
        {
            if (pilasters != null)
                return pilasters;

            int[] list = new int[Bays + 1];

            for (int i = 0; i <= Bays; i++)
                list[i] = Inside + 1 + i * Bay;

            return pilasters = list;
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
                    else if (up % PanelRows == 0)
                        wall = WallID.StoneSlab;                                   // a course across the bays, where the next panel begins
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

                // And a flame on the floor in the middle of the bay. In the one at the left
                // end that is four columns from where the dead come back, and six from the
                // door.
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

        // ============================================================
        // FOIGH'S END (Foigh.End)
        // ============================================================

        /// <summary>
        /// The pilaster Foigh goes to at his end: by request "the 4th columm at the end of the
        /// hall", counted from the end wall, the one against it the first (the door is at the
        /// other end): the fourth from the right.
        /// </summary>
        public const int EndPilaster = Bays - 3;

        /// <summary>Which way is down the hall from its end: toward the door.</summary>
        public const int DownTheHall = -1;

        /// <summary>The middle column of that pilaster.</summary>
        public int EndColumnX => Pilasters()[EndPilaster];

        /// <summary>The loaded world is the hall, and this is it.</summary>
        public static bool Here(out EmptyHall hall)
        {
            hall = Current as EmptyHall;
            return hall != null;
        }

        /// <summary>Where Foigh stands at his end: the ground between his feet, on the floor under that pilaster. False anywhere but the hall.</summary>
        public static bool EndColumn(out Vector2 feet)
        {
            feet = default;

            if (!Here(out EmptyHall hall))
                return false;

            feet = new Vector2((hall.EndColumnX + 0.5f) * 16f, hall.Floor * 16f);
            return true;
        }

        /// <summary>A lamp's sheet is three tiles of eighteen pixels to a style; a torch's unlit frames are its lit ones moved this far along, a lamp's this far.</summary>
        private const int LampTall = 54;
        private const int TorchOff = 66;
        private const int LampOff = 18;

        /// <summary>Every flame in the hall, as the tile it is known by: each torch, and each Tiki Torch (or any lamp) by its top tile.</summary>
        public void Flames(List<Point> into)
        {
            for (int x = Inside; x <= InsideEnd; x++)
            {
                for (int y = Ceiling + 1; y < Floor; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile)
                        continue;

                    if (tile.TileType == TileID.Torches || (tile.TileType == TileID.Lamps && tile.TileFrameY % LampTall == 0))
                        into.Add(new Point(x, y));
                }
            }
        }

        /// <summary>Whether the flame at this tile (a torch, or a lamp's top tile) is lit: a torch in the first half of its frames, a lamp in its first column (as the game lights them).</summary>
        public static bool Lit(Point at)
        {
            if (!WorldGen.InWorld(at.X, at.Y))
                return false;

            Tile tile = Main.tile[at.X, at.Y];

            if (!tile.HasTile)
                return false;

            if (tile.TileType == TileID.Torches)
                return tile.TileFrameX < TorchOff;

            if (tile.TileType == TileID.Lamps)
                return tile.TileFrameX < LampOff;

            return false;
        }

        /// <summary>The flame at this tile lit, or put out, as a switch does it: a torch's frame into the half it is wanted in; each of a lamp's three tiles into its lit or unlit column.</summary>
        public static void SetLit(Point at, bool lit)
        {
            if (!WorldGen.InWorld(at.X, at.Y + 2))
                return;

            Tile tile = Main.tile[at.X, at.Y];

            if (!tile.HasTile)
                return;

            if (tile.TileType == TileID.Torches)
            {
                int frame = tile.TileFrameX % TorchOff;

                tile.TileFrameX = (short)(lit ? frame : frame + TorchOff);
                return;
            }

            if (tile.TileType != TileID.Lamps)
                return;

            for (int y = 0; y < 3; y++)
            {
                Tile part = Main.tile[at.X, at.Y + y];

                if (part.HasTile && part.TileType == TileID.Lamps)
                    part.TileFrameX = (short)(lit ? 0 : LampOff);
            }
        }

        /// <summary>Every flame of the hall lit again, if the loaded world is it.</summary>
        public static void RelightHere()
        {
            if (!Here(out EmptyHall hall))
                return;

            var all = new List<Point>();
            hall.Flames(all);

            foreach (Point at in all)
            {
                if (!Lit(at))
                    SetLit(at, true);
            }
        }
    }

    /// <summary>
    /// The hall's flames, lit whenever it is loaded: Foigh's roar puts them out at his end
    /// (Foigh.End), and a place is kept as it was left (SubworldKeeper), so a hall left in the
    /// dark, or saved with him half way through his end, would be dark the next time.
    /// </summary>
    public class EmptyHallFlames : ModSystem
    {
        public override void PostWorldLoad()
        {
            EmptyHall.RelightHere();
        }
    }
}
