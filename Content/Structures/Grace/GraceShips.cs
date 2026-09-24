using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The ships in the dry basin, drawn the way the rest of the capital is drawn: in section.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        /// <summary>
        /// A ship sitting on her keel. <paramref name="x0"/> is her leftmost hull column,
        /// <paramref name="len"/> her length, <paramref name="keel"/> the row her keel rests on,
        /// <paramref name="dir"/> the way her bow points. A great ship has three masts, a
        /// forecastle and guns in her hold; a small one has one mast.
        ///
        /// In section: the hull's curve two planks thick with a copper bottom, the hold under the
        /// main deck, the far bulwark with its rail drawn behind the deck, a sterncastle with the
        /// master's cabin in it and glazed stern windows, a forecastle, the bowsprit rising off
        /// the stem, masts with their yards and sails set, a jib to the bowsprit, a crow's nest,
        /// the colours at the yardarm, lanterns under the stern and the anchor down on the stones.
        /// </summary>
        private void Ship(int x0, int keel, int len, int dir, bool great)
        {
            int depth = great ? 8 : 6;
            int deck = keel - depth;                 // the main deck
            int aft = great ? 10 : 7;                // the sterncastle, from the stern
            int fore = great ? 7 : 0;                // the forecastle, back from the bow
            int aftDeck = deck - 6;                  // the sterncastle's deck
            int foreDeck = deck - 4;                 // the forecastle's deck

            int X(int u) => dir > 0 ? x0 + u : x0 + len - 1 - u;
            Cut M(Cut cut) => dir > 0 ? cut : cut switch
            {
                Grace.Cut.DownLeft => Grace.Cut.DownRight,
                Grace.Cut.DownRight => Grace.Cut.DownLeft,
                Grace.Cut.UpLeft => Grace.Cut.UpRight,
                Grace.Cut.UpRight => Grace.Cut.UpLeft,
                _ => cut,
            };

            // The line of her bottom: flat under the middle, rising to a steep stern and in a long
            // curve to the stem.
            int Bottom(int u)
            {
                int stern = Math.Max(0, 4 - u);
                double t = (u - (len - 10)) / 9.0;
                int bow = t > 0 ? (int)Math.Round((depth - 1) * Math.Pow(t, 1.6)) : 0;
                return keel - Math.Max(stern, bow);
            }

            // ---- the hull: two planks thick, copper under, the hold hollowed out behind
            for (int u = 0; u < len; u++)
            {
                int x = X(u), b = Bottom(u);
                bool end = u == 0 || u == len - 1;
                int top = u < aft ? aftDeck : fore > 0 && u >= len - fore ? foreDeck : deck;
                if (u == len - 1)
                    top = (fore > 0 ? foreDeck : deck) - 2;          // the stem head

                for (int y = top; y <= b; y++)
                {
                    bool skin = y >= b - 1 || end;
                    if (!skin)
                        continue;
                    c.Block(x, y, y == b ? TileID.CopperPlating : TileID.WoodBlock);
                }
            }

            // The hold: planked behind, from the bottom planks up to the main deck.
            for (int u = 1; u < len - 1; u++)
            {
                int x = X(u), b = Bottom(u);
                for (int y = deck + 1; y < b - 1; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.Planked, NoPaint);
                }
            }

            // ---- the decks
            for (int u = 1; u < len - 1; u++)
                c.Block(X(u), deck, TileID.BorealWood);
            int hatch = great ? len / 2 - 4 : len / 2 - 1;
            for (int u = hatch; u < hatch + 3; u++)
                c.Platform(X(u), deck, Suite.Wood.Platform);

            // The far bulwark, drawn behind the main deck, and its rail.
            for (int u = aft; u < len - Math.Max(1, fore); u++)
            {
                c.Wall(X(u), deck - 1, WallID.Planked, NoPaint);
                c.Wall(X(u), deck - 2, WallID.Planked, NoPaint);
                c.Wall(X(u), deck - 3, WallID.WoodenFence, NoPaint);
            }

            // ---- the sterncastle: the master's cabin, its deck over it overhanging the stern
            for (int u = 1; u < aft - 1; u++)
            {
                int x = X(u);
                for (int y = aftDeck + 1; y < deck; y++)
                {
                    c.Erase(x, y);
                    c.Wall(x, y, WallID.Wood, NoPaint);
                }
            }
            for (int y = aftDeck + 1; y < deck; y++)
                c.Block(X(aft - 1), y, TileID.WoodBlock);
            for (int u = -1; u < aft; u++)
                c.Block(X(u), aftDeck, TileID.DynastyWood);
            c.Reshape(X(-1), aftDeck, M(Grace.Cut.UpLeft));

            // Stern windows in the transom, gold over and under them; a window in the cabin's
            // back wall; a door out onto the main deck.
            c.Block(X(0), deck - 4, TileID.Glass);
            c.Block(X(0), deck - 3, TileID.Glass);
            c.Block(X(0), deck - 5, TileID.AncientGoldBrick, NoPaint);
            c.Block(X(0), deck - 2, TileID.AncientGoldBrick, NoPaint);
            int wu = aft / 2;
            for (int u = wu - 1; u <= wu; u++)
                for (int y = deck - 4; y <= deck - 3; y++)
                    c.Wall(X(u), y, WallID.Glass, NoPaint);
            for (int y = deck - 3; y < deck; y++)
                c.Erase(X(aft - 1), y);
            c.Place(X(aft - 1), deck - 1, TileID.ClosedDoor, Suite.Wood.Door);

            // In the cabin: the master's table, his chest, a lantern.
            if (great)
                c.Place(dir > 0 ? X(2) : X(4), deck - 1, TileID.Tables, Suite.Wood.Table);
            Stash(dir > 0 ? X(aft - 4) : X(aft - 3), deck - 1, Loot.Sea, Suite.Wood);
            HangAt(X(aft / 2), deck - 2, TileID.HangingLanterns, Suite.Wood.Lantern);

            // The taffrail round the sterncastle deck, and lanterns hung under the overhang.
            for (int u = -1; u < aft; u++)
                c.Wall(X(u), aftDeck - 1, WallID.WoodenFence, NoPaint);
            Hang(X(-1), aftDeck + 1, TileID.HangingLanterns, Suite.Wood.Lantern);

            // ---- the forecastle
            if (fore > 0)
            {
                for (int u = len - fore; u < len - 1; u++)
                {
                    int x = X(u);
                    for (int y = foreDeck + 1; y < deck; y++)
                    {
                        c.Erase(x, y);
                        c.Wall(x, y, WallID.Planked, NoPaint);
                    }
                    c.Block(x, foreDeck, TileID.DynastyWood);
                    c.Wall(x, foreDeck - 1, WallID.WoodenFence, NoPaint);
                }
                c.Reshape(X(len - fore), foreDeck, M(Grace.Cut.UpLeft));
            }

            // The stem head carries a figure of gold.
            int stemTop = (fore > 0 ? foreDeck : deck) - 2;
            c.Block(X(len - 1), stemTop - 1, TileID.AncientGoldBrick, NoPaint, M(Grace.Cut.DownRight));

            // ---- the bowsprit: a spar rising off the stem
            int sprit = great ? 6 : 4;
            for (int i = 1; i <= sprit; i++)
                c.Platform(X(len - 1 + i), stemTop - i, Suite.Wood.Platform, M(Grace.Cut.DownLeft));
            int tipU = len - 1 + sprit, tipY = stemTop - sprit;

            // ---- the masts
            if (great)
            {
                int main = len / 2, foreMast = len - 5, mizzen = 4;
                int foreTop = Mast(X(foreMast), Bottom(foreMast) - 2, deck - 25, 4, 4, dir, false);
                Mast(X(main), Bottom(main) - 2, deck - 30, 5, 5, dir, true);
                Mast(X(mizzen), aftDeck - 1, aftDeck - 16, 3, 3, dir, false);
                Jib(foreMast, foreTop, tipU, tipY, foreDeck - 3, X);
            }
            else
            {
                int main = len / 2 + 1;
                int top = Mast(X(main), Bottom(main) - 2, deck - 22, 4, 5, dir, true);
                Jib(main, top, tipU, tipY, deck - 4, X);
            }

            // ---- the hold: stores aft of the mainmast, guns forward of it, the lamp
            int mainU = great ? len / 2 : len / 2 + 1;
            int stand = keel - 2;
            for (int u = 5; u + 1 < mainU; u += 3)
            {
                int k = (u / 3) % 3;
                int lx = dir > 0 ? X(u) : X(u + 1);
                if (Bottom(u) != keel || Bottom(u + 1) != keel)
                    continue;
                if (k == 0)
                    c.Place(lx, stand, TileID.Kegs, 0);
                else if (k == 1)
                    Stash(lx, stand, Loot.Sea, Suite.Wood);
                else
                    c.Place(lx, stand, TileID.FishingCrate, 0);
            }
            if (great)
            {
                int gu = mainU + 2;
                c.Place(dir > 0 ? X(gu) : X(gu + 3), stand, TileID.Cannon, 0, dir);
            }
            HangAt(X(mainU - 3), deck + 1, TileID.HangingLanterns, Suite.Wood.Lantern);

            // ---- the anchor, down on the stones off her bow, its chain to the hawse
            int ax = X(len + 1);
            int ground = keel;
            for (int dx = -1; dx <= 1; dx++)
                for (int y = ground - 2; y <= ground; y++)
                    if (c.TypeAt(ax + dx, y) == TileID.Ash)
                        c.Erase(ax + dx, y);
            if (c.TypeAt(ax, ground) < 0 && c.TypeAt(ax - 1, ground) < 0 && c.TypeAt(ax + 1, ground) < 0)
            {
                c.Block(ax - 1, ground, TileID.IronBrick, NoPaint, Grace.Cut.DownRight);
                c.Block(ax, ground, TileID.IronBrick);
                c.Block(ax + 1, ground, TileID.IronBrick, NoPaint, Grace.Cut.DownLeft);
                c.Block(ax, ground - 1, TileID.IronBrick);
                c.Block(ax, ground - 2, TileID.IronBrick, NoPaint, Grace.Cut.Half);
                for (int y = stemTop - 1; y < ground - 2; y++)
                    if (c.TypeAt(ax, y) < 0)
                        c.Block(ax, y, TileID.Chain);
            }
        }

        /// <summary>
        /// A mast from <paramref name="foot"/> up to <paramref name="top"/> at column
        /// <paramref name="x"/>: a crow's nest under the truck if it is the main mast, a topsail
        /// yard and a course yard, both sails set on them and billowing, sheets of rope from the
        /// course yard's arms down to the rail, the colours at the topsail yardarm. Returns the
        /// row of the topsail yard, for the jib.
        /// </summary>
        private int Mast(int x, int foot, int top, int topHalf, int courseHalf, int dir, bool main)
        {
            for (int y = top; y <= foot; y++)
                if (c.TypeAt(x, y) < 0)
                    c.Block(x, y, TileID.WoodenBeam);

            int nest = top + 3;
            int topYard = main ? nest + 3 : top + 2;
            int courseYard = topYard + (main ? 9 : 8);
            int courseH = main ? 8 : 7;

            // The crow's nest.
            if (main)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (dx != 0)
                        c.Platform(x + dx, nest, Suite.Wood.Platform);
                    c.Wall(x + dx, nest - 1, WallID.WoodenFence, NoPaint);
                    c.Wall(x + dx, nest - 2, WallID.WoodenFence, NoPaint);
                }
            }

            // The yards: thin spars of boreal, a whole block at each arm to take the rigging.
            foreach (var (row, half) in new[] { (topYard, topHalf), (courseYard, courseHalf) })
            {
                for (int dx = -half; dx <= half; dx++)
                    if (dx != 0)
                        c.Block(x + dx, row, TileID.BorealWood, NoPaint, Math.Abs(dx) == half ? Grace.Cut.Full : Grace.Cut.Half);
            }

            // The sails: canvas walls, a tile narrower than their yards at the head, full at the
            // belly, drawn in at the foot.
            Sail(x, topYard + 1, courseYard - 1, topHalf - 1);
            Sail(x, courseYard + 1, courseYard + courseH, courseHalf - 1);

            // Sheets from the course yardarms down toward the rail.
            foreach (int dx in new[] { -courseHalf, courseHalf })
                for (int y = courseYard + 1; y <= courseYard + courseH + 2; y++)
                    if (c.TypeAt(x + dx, y) < 0)
                        c.Block(x + dx, y, TileID.Rope);

            // The colours at the aft topsail yardarm.
            if (main)
                Hang(x - dir * topHalf, topYard + 1, TileID.Banners, 3);

            return topYard;
        }

        /// <summary>A square sail hung from a yard: rows <paramref name="y0"/> to <paramref name="y1"/>, half-width at the head as given.</summary>
        private void Sail(int x, int y0, int y1, int half)
        {
            int h = y1 - y0 + 1;
            for (int y = y0; y <= y1; y++)
            {
                int k = y - y0;
                int w = half + (k >= 1 && k < h - 1 ? 1 : 0);
                for (int dx = -w; dx <= w; dx++)
                    c.Wall(x + dx, y, WallID.Sail, NoPaint);
            }
        }

        /// <summary>
        /// The jib: a triangle of canvas under the forestay, from the foremast just under its
        /// topsail yard to the bowsprit's tip, its foot running back from the tip to a clew over
        /// the rail at <paramref name="clewY"/>.
        /// </summary>
        private void Jib(int mastU, int mastY, int tipU, int tipY, int clewY, Func<int, int> X)
        {
            int head = mastY + 2;
            int clewU = mastU + 2;
            for (int u = clewU; u < tipU; u++)
            {
                double t = (u - mastU) / (double)(tipU - mastU);
                int stay = (int)Math.Round(head + (tipY - head) * t);
                double f = (tipU - u) / (double)(tipU - clewU);
                int foot = (int)Math.Round(tipY + (clewY - tipY) * f);
                for (int y = stay; y <= foot; y++)
                    if (c.WallAt(X(u), y) == 0 && c.TypeAt(X(u), y) < 0)
                        c.Wall(X(u), y, WallID.Sail, NoPaint);
            }
        }
    }
}
