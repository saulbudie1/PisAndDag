using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    public sealed partial class GraceBuilder
    {
        /// <summary>The Processional's paving row: twelve over the plain.</summary>
        private const int DeckY = -12;

        /// <summary>Where the causeway starts, after the stair up to it.</summary>
        private int DeckStart => Inside + 30;

        /// <summary>The canal under the Processional: its surface row, and the row of its bed.</summary>
        private const int CanalTop = -4, CanalBed = 2;

        /// <summary>
        /// The Processional. A causeway twelve tiles over the plain, carried on arches, dead
        /// straight from the stair at the foot of the Curtain to the gate of the Low City.
        /// Along it the line of the house stands in marble, every one in its own columned
        /// niche and every face intact, getting more recent as the road goes east. The last
        /// plinth is newer stone and does not match.
        ///
        /// Under it, through the arches, runs a canal: still water to half the height of the
        /// arcade, the piers standing in it, their feet seen through the water against the
        /// far wall of the arcade.
        /// </summary>
        private void Processional()
        {
            int end = LowCityX - 1;

            // ---- the stair up from the forecourt, in half steps
            Ramp(Inside + 6, 0, -DeckY, 1, TileID.MarbleBlock, White);

            // ---- the deck and the arcade under it
            Box(DeckStart, DeckY, end, DeckY + 2, TileID.MarbleBlock, White);
            Box(DeckStart, DeckY, end, DeckY, TileID.StoneSlab, White);
            Box(DeckStart, DeckY + 3, end, -1, TileID.MarbleBlock, White);

            const int bay = 12;
            int first = DeckStart + 3, lastArch = first;
            for (int x = first; x + bay - 3 <= end; x += bay)
            {
                ArchOpening(x, x + bay - 4, -1, -5, 4, WallID.StoneSlab, NoPaint);
                lastArch = x + bay - 4;
                HangAt(x + (bay - 4) / 2, -2, TileID.HangingLanterns, Suite.Marble.Lantern);

                // The cornice over each arch carries a thread of gold.
                for (int xx = x; xx <= x + bay - 4; xx++)
                    if (c.Solid(xx, DeckY + 2))
                        c.Block(xx, DeckY + 2, TileID.AncientGoldBrick, NoPaint);
            }

            Canal(first, lastArch, bay);

            // ---- the line of the house
            int count = 0;
            int total = (end - DeckStart - 8) / bay;

            for (int cx = DeckStart + 8; cx + 5 <= end; cx += bay)
            {
                count++;
                bool last = count == total;
                Niche(cx, DeckY - 1, count, total);
            }

            // Lamps between the niches, on the deck.
            for (int x = DeckStart + 2; x <= end - 2; x += bay)
                c.Place(x, DeckY - 1, TileID.Lamps, 30);
        }

        /// <summary>
        /// The canal through the arcade, from the first arch to the last. The bed is dug two
        /// rows under the plain and faced in slab; the piers stop at the water, their lower
        /// halves carried on down as a white wall behind it, each pier's foot cut to a point
        /// like a cutwater; the far wall of the arcade is grey slab, a gold line along it at
        /// the springing, and the water stands still to half the height of the arches.
        /// </summary>
        private void Canal(int x0, int x1, int bay)
        {
            // The bed, and the arcade's far wall down to it.
            for (int x = x0; x <= x1; x++)
            {
                for (int y = CanalTop; y < CanalBed; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, y >= 0 ? WallID.GrayBrick : WallID.StoneSlab, NoPaint);
                }

                c.Block(x, CanalBed, TileID.StoneSlab, NoPaint);
            }

            // The piers: the stone stops a row over the water, pointed underneath; below
            // that the pier goes on down as wall, white against the grey.
            for (int px = x0 + bay - 3; px + 2 < x1; px += bay)
            {
                for (int x = px; x <= px + 2; x++)
                    for (int y = CanalTop; y < CanalBed; y++)
                        c.Wall(x, y, WallID.MarbleBlock, White);

                Cut(px, CanalTop - 1, Grace.Cut.UpLeft);
                Cut(px + 2, CanalTop - 1, Grace.Cut.UpRight);
            }

            // A gold line along the far wall at the springing of the arches.
            for (int x = x0; x <= x1; x++)
                if (c.TypeAt(x, -5) < 0 && c.WallAt(x, -5) == WallID.StoneSlab)
                    c.Wall(x, -5, WallID.AncientGoldBrickWall, NoPaint);

            // The water.
            for (int x = x0; x <= x1; x++)
                for (int y = CanalTop; y < CanalBed; y++)
                    c.Liquid(x, y, 255, 0);

            // A few strands of vine down from the soffits, stopping short of the water.
            byte[] green = { PaintID.DeepGreenPaint, PaintID.GreenPaint, PaintID.DeepGreenPaint, NoPaint };
            for (int ax = x0; ax + bay - 4 <= x1; ax += bay)
            {
                int strands = c.Next(1, 4);
                for (int k = 0; k < strands; k++)
                {
                    int vx = ax + c.Next(0, bay - 3);
                    int soffit = CeilingOver(vx, CanalTop - 1);
                    if (soffit == int.MinValue)
                        continue;

                    byte paint = green[c.Next(0, green.Length)];
                    int len = c.Next(2, 6);
                    for (int i = 1; i <= len && soffit + i < CanalTop - 1; i++)
                    {
                        if (c.TypeAt(vx, soffit + i) >= 0)
                            break;
                        c.Block(vx, soffit + i, TileID.VineRope, paint);
                    }
                }
            }

            c.Mark("Grace.Canal", x0, CanalTop, x1 - x0 + 1, CanalBed - CanalTop);
        }

        /// <summary>
        /// One niche on the causeway: a plinth a step high, two marble columns, a lintel
        /// and a low pediment, a statue of someone in the line on the plinth and a banner
        /// down each side. <paramref name="floor"/> is the row the plinth stands on.
        /// </summary>
        private void Niche(int cx, int floor, int number, int total)
        {
            bool last = number == total;
            int top = floor - 12;

            // The niche wall behind, under the whole lintel.
            Back(cx - 5, top + 2, cx + 4, floor, last ? WallID.StoneSlab : WallID.MarbleBlock, NoPaint);

            // Plinth: one step, dressed at both ends.
            ushort plinth = last ? TileID.StoneSlab : TileID.MarbleBlock;
            byte plinthPaint = last ? NoPaint : White;
            Box(cx - 2, floor, cx + 1, floor, plinth, plinthPaint);
            Cut(cx - 2, floor, Grace.Cut.DownLeft);
            Cut(cx + 1, floor, Grace.Cut.DownRight);

            // Columns and lintel.
            Column(cx - 4, top + 2, floor);
            Column(cx + 3, top + 2, floor);
            Box(cx - 5, top, cx + 4, top + 1, TileID.MarbleBlock, White);
            Box(cx - 5, top + 1, cx + 4, top + 1, TileID.SandstoneBrick, White);

            // Pediment: three rows, stepped in, gold on the ridge.
            for (int k = 0; k < 3; k++)
                Box(cx - 4 + k, top - 1 - k, cx + 3 - k, top - 1 - k, TileID.MarbleBlock, White);
            Box(cx - 1, top - 4, cx, top - 4, TileID.AncientGoldBrick, NoPaint);
            Dress(cx - 5, top - 4, cx + 4, top);

            // The statue. The house wore crowns and armour in turn.
            int style = last ? 0 : number % 5 == 0 ? 0 : number % 2 == 0 ? 41 : 40;
            if (c.Place(cx - 1, floor - 1, TileID.Statues, style))
                PaintObject(cx - 1, floor - 3, 2, 3, last ? NoPaint : White);

            // Banners either side, hung from the lintel.
            if (!last)
            {
                Hang(cx - 5, top + 2, TileID.Banners, 3);
                Hang(cx + 4, top + 2, TileID.Banners, 3);
            }

            // Plinth inscriptions: a stone on the niche wall over the statue.
            string text = Plinths.Text(number, total);
            if (text != null && c.Sign(cx - 1, floor - 5, TileID.Signs, 0, text))
                PaintObject(cx - 1, floor - 6, 2, 2, Gray);
        }

        /// <summary>Paint every tile of an object already placed. Statues in marble, stone in gold.</summary>
        private void PaintObject(int x, int top, int w, int h, byte paint)
        {
            if (paint == NoPaint)
                return;

            for (int xx = x; xx < x + w; xx++)
                for (int yy = top; yy < top + h; yy++)
                    if (c.TypeAt(xx, yy) >= 0)
                        c.Paint(xx, yy, paint);
        }
    }

    /// <summary>
    /// What is cut into the plinths of the Processional. Each carries a name and a number
    /// (PAD_Grace_Structure_Plan.md §3.2); only the drafted lines are cut so far, and the
    /// rest of the stones wait for theirs.
    /// </summary>
    public static class Plinths
    {
        public static string Text(int number, int total)
        {
            if (number == total)
                return "Lord of Grace.";

            if (number == total - 1)
                return "Second of the name. Buried in the spring.";

            return null;
        }
    }
}
