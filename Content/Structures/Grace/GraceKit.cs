using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The mason's hand tools: fills, walls, arches, domes, stairs. Every builder is made
    /// of these and nothing else, so the look of the capital is decided here.
    ///
    /// Coordinates inclusive at both ends, plan orientation (see <see cref="Canvas"/>).
    /// </summary>
    public sealed partial class GraceBuilder
    {
        private readonly Canvas c;
        private readonly GraceSite site;

        private GraceBuilder(Canvas canvas, GraceSite site)
        {
            c = canvas;
            this.site = site;
        }

        public static void Build(Canvas canvas, GraceSite site) => new GraceBuilder(canvas, site).Run();

        // ------------------------------------------------------------ materials

        // Paint. White makes marble into the colour of the place; gray is the ash.
        private const byte White = PaintID.WhitePaint;
        private const byte Gray = PaintID.GrayPaint;
        private const byte NoPaint = 0;

        // ------------------------------------------------------------ fills

        private void Box(int x0, int y0, int x1, int y1, ushort type, byte paint = NoPaint)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    c.Block(x, y, type, paint);
        }

        private void Air(int x0, int y0, int x1, int y1)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    c.Erase(x, y);
        }

        private void Back(int x0, int y0, int x1, int y1, ushort wall, byte paint = NoPaint)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    c.Wall(x, y, wall, paint);
        }

        private void NoBack(int x0, int y0, int x1, int y1)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    c.EraseWall(x, y);
        }

        /// <summary>A room: air inside, a wall behind it, nothing else.</summary>
        private void Hollow(int x0, int y0, int x1, int y1, ushort wall, byte paint = NoPaint)
        {
            Air(x0, y0, x1, y1);
            Back(x0, y0, x1, y1, wall, paint);
        }

        private void Cut(int x, int y, Cut cut)
        {
            if (c.TypeAt(x, y) >= 0)
                c.Reshape(x, y, cut);
        }

        // ------------------------------------------------------------ edges

        /// <summary>
        /// Takes the square corners off the top of a run of blocks by the hammer's rule
        /// (<see cref="TopCorner"/>): the steps of a stair and the ends of a broad top are
        /// sloped, anything two wide or less is left square.
        /// </summary>
        private void Dress(int x0, int y0, int x1, int y1)
        {
            var cuts = new System.Collections.Generic.List<(int x, int y, Cut cut)>();

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (!Mass(x, y))
                        continue;

                    Cut cut = TopCorner(x, y);
                    if (cut != Grace.Cut.Full)
                        cuts.Add((x, y, cut));
                }
            }

            foreach (var (x, y, cut) in cuts)
                c.Reshape(x, y, cut);
        }

        // ------------------------------------------------------------ arches

        /// <summary>
        /// An opening with a round head: [x0, x1] wide, standing on <paramref name="sill"/>
        /// (the first open row above it), straight sides up to <paramref name="spring"/>,
        /// then a half ellipse <paramref name="rise"/> high. Everything inside is taken out;
        /// the steps of the curve are cut so the head reads round.
        /// </summary>
        private void ArchOpening(int x0, int x1, int sill, int spring, int rise, ushort backWall = 0, byte wallPaint = NoPaint, bool keepWall = false)
        {
            double cx = (x0 + x1) / 2.0;

            for (int x = x0; x <= x1; x++)
            {
                int top = HeadTop(x, x0, x1, spring, rise);

                for (int y = top; y <= sill; y++)
                {
                    c.Erase(x, y);

                    if (backWall != 0)
                        c.Wall(x, y, backWall, wallPaint);
                    else if (!keepWall)
                        c.EraseWall(x, y);
                }
            }

            // The soffit: a step up toward the crown is cut under the block that makes it.
            for (int x = x0; x <= x1; x++)
            {
                int top = HeadTop(x, x0, x1, spring, rise);
                int leftTop = x > x0 ? HeadTop(x - 1, x0, x1, spring, rise) : int.MaxValue;
                int rightTop = x < x1 ? HeadTop(x + 1, x0, x1, spring, rise) : int.MaxValue;

                if (x < cx && rightTop < top && c.Solid(x, top - 1))
                    Cut(x, top - 1, Grace.Cut.UpRight);
                if (x > cx && leftTop < top && c.Solid(x, top - 1))
                    Cut(x, top - 1, Grace.Cut.UpLeft);
            }
        }

        /// <summary>
        /// The first open row of an arch's head in column <paramref name="x"/>. Measured from
        /// the middle of the column to the middle of the span, so the two halves of every
        /// arch are mirror images and the piers between arches come out even on both sides.
        /// </summary>
        private static int HeadTop(int x, int x0, int x1, int spring, int rise)
        {
            double cx = (x0 + x1) / 2.0;
            double hw = (x1 - x0 + 1) / 2.0;
            double u = (x - cx) / hw;
            return spring - (int)Math.Round(rise * Math.Sqrt(Math.Max(0, 1 - u * u)));
        }

        // ------------------------------------------------------------ domes

        /// <summary>
        /// A dome over [cx - r, cx + r], standing on <paramref name="baseY"/> (the row under
        /// its drum), <paramref name="h"/> tall, a shell <paramref name="shell"/> thick. The
        /// inside is left open with the wall given, the outside cut to a curve.
        /// </summary>
        private void Dome(int cx, int baseY, int r, int h, int shell, ushort type, byte paint, ushort insideWall = 0, byte insidePaint = NoPaint, bool lit = true)
        {
            int ri = Math.Max(1, r - shell);
            int hi = Math.Max(1, h - shell);

            for (int x = cx - r; x <= cx + r; x++)
            {
                double u = (x - cx) / (double)(r + 0.5);
                int outer = baseY - (int)Math.Round(h * Math.Sqrt(Math.Max(0, 1 - u * u)));

                double ui = (x - cx) / (double)(ri + 0.5);
                int inner = Math.Abs(x - cx) <= ri ? baseY - (int)Math.Round(hi * Math.Sqrt(Math.Max(0, 1 - ui * ui))) : baseY + 1;

                for (int y = outer; y <= baseY; y++)
                {
                    if (y >= inner)
                    {
                        c.Erase(x, y);
                        if (insideWall != 0)
                            c.Wall(x, y, insideWall, insidePaint);
                    }
                    else
                    {
                        c.Block(x, y, type, paint);
                    }
                }
            }

            // A lamp under the crown, so the inside is not a black cup.
            if (lit && insideWall != 0 && hi >= 3)
                Hang(cx, baseY - hi, TileID.HangingLanterns, Suite.Marble.Lantern);

            // Outer curve dressed; inner curve dressed.
            for (int x = cx - r; x <= cx + r; x++)
            {
                int top = TopOf(x, baseY - h - 2, baseY);

                if (top == int.MinValue)
                    continue;

                bool leftLower = !c.Solid(x - 1, top);
                bool rightLower = !c.Solid(x + 1, top);

                if (x < cx && leftLower && c.Solid(x + 1, top))
                    Cut(x, top, Grace.Cut.DownLeft);
                else if (x > cx && rightLower && c.Solid(x - 1, top))
                    Cut(x, top, Grace.Cut.DownRight);
            }
        }

        /// <summary>The highest solid row in this column between the two rows given, or int.MinValue.</summary>
        private int TopOf(int x, int from, int to)
        {
            for (int y = from; y <= to; y++)
            {
                if (c.Solid(x, y))
                    return y;
            }

            return int.MinValue;
        }

        // ------------------------------------------------------------ roofs and spires

        /// <summary>
        /// A pitched roof over [x0, x1] resting on <paramref name="eave"/>: rises one row per
        /// <paramref name="run"/> columns to a ridge, solid underneath, slopes on the steps.
        /// </summary>
        private void Pitch(int x0, int x1, int eave, int run, ushort type, byte paint, ushort under = 0, byte underPaint = NoPaint)
        {
            int mid = (x0 + x1) / 2;

            for (int x = x0; x <= x1; x++)
            {
                int d = Math.Min(x - x0, x1 - x);
                int top = eave - d / Math.Max(1, run);

                for (int y = top; y <= eave; y++)
                {
                    bool skin = y <= top + 1;
                    if (skin)
                        c.Block(x, y, type, paint);
                    else
                    {
                        c.Erase(x, y);
                        if (under != 0)
                            c.Wall(x, y, under, underPaint);
                    }
                }
            }

            for (int x = x0; x <= x1; x++)
            {
                int top = TopOf(x, eave - (x1 - x0), eave);
                if (top == int.MinValue)
                    continue;
                if (x < mid && !c.Solid(x - 1, top) && c.Solid(x + 1, top))
                    Cut(x, top, Grace.Cut.DownLeft);
                else if (x > mid && !c.Solid(x + 1, top) && c.Solid(x - 1, top))
                    Cut(x, top, Grace.Cut.DownRight);
            }
        }

        /// <summary>A needle: a stepped cone <paramref name="w"/> wide at the base, tapering to one tile over <paramref name="h"/> rows.</summary>
        private void Spire(int cx, int baseY, int w, int h, ushort type, byte paint)
        {
            int half = w / 2;

            for (int i = 0; i < h; i++)
            {
                int y = baseY - i;
                int r = (int)Math.Round(half * (1.0 - i / (double)h));

                for (int x = cx - r; x <= cx + r; x++)
                    c.Block(x, y, type, paint);
            }

            for (int i = 0; i < h; i++)
            {
                int y = baseY - i;
                int r = (int)Math.Round(half * (1.0 - i / (double)h));

                if (r >= 1)
                {
                    if (!c.Solid(cx - r, y - 1))
                        Cut(cx - r, y, Grace.Cut.DownLeft);
                    if (!c.Solid(cx + r, y - 1))
                        Cut(cx + r, y, Grace.Cut.DownRight);
                }
            }
        }

        /// <summary>
        /// A hollow needle: the same cone as <see cref="Spire"/> but only a skin of
        /// <paramref name="skin"/> blocks, the inside a wall with a lamp in it, so a broad
        /// spire is not a black wedge against the sky.
        /// </summary>
        private void Needle(int cx, int baseY, int w, int h, ushort type, byte paint, ushort wall, byte wallPaint, int skin = 1)
        {
            Spire(cx, baseY, w, h, type, paint);
            int half = w / 2;

            for (int i = 0; i < h; i++)
            {
                int y = baseY - i;
                int r = (int)Math.Round(half * (1.0 - i / (double)h));
                int ri = r - skin - (i < skin ? r : 0);

                for (int x = cx - ri; x <= cx + ri; x++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, wall, wallPaint);
                }
            }

            // The lamp hangs from the top of the hollow, where the needle closes over it.
            for (int i = h - 1; i > skin; i--)
            {
                int y = baseY - i;
                int r = (int)Math.Round(half * (1.0 - i / (double)h));
                if (r - skin < 0)
                    continue;

                if (c.TypeAt(cx, y) < 0 && c.TypeAt(cx, y + 1) < 0 && c.Solid(cx, y - 1))
                    Hang(cx, y, TileID.HangingLanterns, Suite.Marble.Lantern);
                break;
            }
        }

        // ------------------------------------------------------------ battlements

        /// <summary>Merlons along the top of a wall whose walk is row <paramref name="walk"/>.</summary>
        private void Merlons(int x0, int x1, int walk, int w, int gap, int h, ushort type, byte paint)
        {
            int period = w + gap;

            for (int x = x0; x <= x1; x++)
            {
                if ((x - x0) % period >= w)
                    continue;

                for (int y = walk - h; y < walk; y++)
                    c.Block(x, y, type, paint);
            }

            Dress(x0, walk - h, x1, walk - 1);
        }

        // ------------------------------------------------------------ stairs

        /// <summary>
        /// A flight of stairs in stone: <paramref name="n"/> steps from the floor at
        /// (<paramref name="x"/>, <paramref name="floor"/>) going up toward <paramref name="dir"/>
        /// (+1 east, -1 west), each tread one row higher and one column on, cut smooth.
        /// Solid underneath down to the floor.
        /// </summary>
        private void Flight(int x, int floor, int n, int dir, ushort type, byte paint)
        {
            for (int i = 0; i < n; i++)
            {
                int xx = x + i * dir;
                int top = floor - i;

                for (int y = top; y <= floor; y++)
                    c.Block(xx, y, type, paint);

                Cut(xx, top, dir > 0 ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
            }
        }

        /// <summary>
        /// A gentle stair in half steps: rises <paramref name="rise"/> rows over twice as many
        /// columns, starting from the floor at (<paramref name="x"/>, <paramref name="floor"/>)
        /// toward <paramref name="dir"/>. Solid under every step, so it reads as a flight of
        /// stone, and every other column a half block so the foot never meets a riser.
        /// </summary>
        private void Ramp(int x, int floor, int rise, int dir, ushort type, byte paint)
        {
            for (int k = 1; k <= rise * 2; k++)
            {
                int xx = x + (k - 1) * dir;
                int full = k / 2;
                bool half = k % 2 == 1;
                int top = floor - full - (half ? 1 : 0);

                for (int y = top; y <= floor; y++)
                    c.Block(xx, y, type, paint);

                if (half)
                    Cut(xx, top, Grace.Cut.Half);
            }
        }

        /// <summary>Platforms laid as a stair: n treads rising toward dir. Walkable up and down, and through.</summary>
        private void PlatformStair(int x, int floor, int n, int dir, int style)
        {
            for (int i = 0; i < n; i++)
            {
                int xx = x + i * dir;
                int y = floor - i;
                c.Platform(xx, y, style, dir > 0 ? Grace.Cut.DownLeft : Grace.Cut.DownRight);
            }
        }

        // ------------------------------------------------------------ hanging things

        /// <summary>Something that hangs: its top row is <paramref name="top"/>, just under what holds it.</summary>
        private bool Hang(int x, int top, ushort type, int style = 0)
        {
            var (w, h) = c.Size(type, style);
            return c.Place(x, top + h - 1, type, style);
        }

        // ------------------------------------------------------------ columns

        /// <summary>A column of marble drums from <paramref name="y0"/> to <paramref name="y1"/>. Stands in front of the wall; you walk through it.</summary>
        private void Column(int x, int y0, int y1, ushort type = TileID.MarbleColumn, byte paint = White)
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
            {
                if (c.TypeAt(x, y) < 0)
                    c.Block(x, y, type, paint);
            }
        }
    }
}
