using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The island's shore and its underside (LighthouseIsland): the fisherman's house on its stilts
    /// and the pier out from it, his skiff tied at its end and the sloop riding at anchor past it;
    /// the reef on the shelf under the water; the spikes the rock hangs down in, and the chains from
    /// it to the floor; what grows on it and lies about.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        // ------------------------------------------------------------ the fisherman's house

        /// <summary>The fisherman's: boards along the floor, a palm rail, wood mended in palm, boards under the ceiling.</summary>
        private static readonly Scheme IsleHutScheme = new Scheme
        {
            Dado = WallID.Planked, DadoH = 2,
            Rail = WallID.PalmWood,
            Field = WallID.Wood,
            Mottle = WallID.PalmWood, Mottling = 0.18,
            Frieze = WallID.Planked,
        };

        /// <summary>
        /// The fisherman's house: one room of palm boards on stilts over the water's edge, a door at
        /// either end, a porch along its west side with a flight of boards up to it from the sand,
        /// steps from its east door down to the pier; its roof blue shingle, its chimney smoking.
        /// In it his bed, his sea chest, a crate, a chair at the fire, a keg; a picture of the sea
        /// over the chest; a window at either end of the back wall. Things to sit on and no table.
        /// </summary>
        private void IsleHut()
        {
            Suite s = IslePalm;
            const int floor = HutFloor;
            const int ceil = floor - 7;
            var r = new Room(HutX0 + 1, HutX1 - 1, ceil + 1, floor);

            foreach (int x in new[] { HutX0 + 1, HutX0 + 6, HutX0 + 12, HutX0 + 18, HutX1 - 1, HutX0 - 7, HutX0 - 3, HutX1 + 1 })
                IslePost(x, floor + 1);
            IslePost(HutX1 + 3, floor + 3);

            Box(HutX0, floor, HutX1, floor, TileID.PalmWood);
            Box(HutX0, ceil, HutX1, ceil, TileID.PalmWood);
            Box(HutX0, ceil + 1, HutX0, floor - 1, TileID.PalmWood);
            Box(HutX1, ceil + 1, HutX1, floor - 1, TileID.PalmWood);
            Hollow(r.X0, r.Top, r.X1, r.Floor - 1, WallID.Wood);
            Registers(r, IsleHutScheme);

            Window(HutX0 + 3, HutX0 + 4, floor - 4, floor - 5, 0, WallID.PalmWood, NoPaint);
            Window(HutX1 - 4, HutX1 - 3, floor - 4, floor - 5, 0, WallID.PalmWood, NoPaint);

            foreach (int x in new[] { HutX0, HutX1 })
            {
                Air(x, floor - 3, x, floor - 1);
                c.Place(x, floor - 1, TileID.ClosedDoor, s.Door);
            }

            Pitch(HutX0 - 2, HutX1 + 2, ceil - 1, 2, TileID.BlueDynastyShingles, NoPaint, WallID.Wood);

            // The hearth: the fire against a breast of brick, the stack up through the roof, the
            // chimney pot on it smoking.
            int hx = HutX0 + 15;
            Back(hx - 1, r.Top, hx + 3, r.Floor - 1, WallID.GrayBrick);
            c.Place(hx, r.Stand, TileID.Fireplace);
            int stack = ceil - 8;
            Box(hx, stack, hx + 2, ceil - 1, TileID.GrayBrick);
            if (c.Place(hx, stack - 1, TileID.Chimney))
                isle.Smoke?.Invoke(hx, stack - 1);

            c.Place(HutX0 + 2, r.Stand, TileID.Beds, s.Bed, 1);
            c.PlaceChest(HutX0 + 7, r.Stand, TileID.Containers, s.Chest, Roll(Loot.Sea));
            c.Place(HutX0 + 9, r.Stand, TileID.FishingCrate);
            c.Place(HutX0 + 7, r.Stand - 3, TileID.Painting3X2, 26);
            c.Place(HutX0 + 12, r.Stand, TileID.Chairs, s.Chair, 1);
            c.Place(HutX0 + 19, r.Stand, TileID.Kegs);
            c.Place(HutX0 + 21, r.Stand, TileID.FishingCrate);
            Hang(HutX0 + 6, r.Top, TileID.HangingLanterns, s.Lantern);
            Hang(HutX0 + 13, r.Top, TileID.HangingLanterns, s.Lantern);

            // The porch, railed, and the flight up to it; the steps down to the pier.
            for (int x = HutX0 - 7; x < HutX0; x++)
            {
                c.Platform(x, floor, s.Platform);
                for (int y = floor - 2; y < floor; y++)
                    c.Wall(x, y, WallID.WoodenFence);
            }

            IsleTreads(HutX0 - 12, floor + 4, 5, 1, s.Platform);
            IsleTreads(HutX1 + 4, floor + 3, 4, -1, s.Platform);
        }

        /// <summary>A flight of boards (<see cref="PlatformStair"/>), the sand and rock it would cut through taken out of its way first.</summary>
        private void IsleTreads(int x, int floor, int n, int dir, int style)
        {
            for (int i = 0; i < n; i++)
                for (int y = floor - i - 3; y <= floor - i; y++)
                    if (IsleNatural(c.TypeAt(x + i * dir, y)))
                        c.Erase(x + i * dir, y);

            PlatformStair(x, floor, n, dir, style);
        }

        /// <summary>A post of timber from this row down to whatever it stands on.</summary>
        private void IslePost(int x, int from)
        {
            for (int y = from; y < from + 48 && !c.Solid(x, y); y++)
                c.Block(x, y, TileID.WoodenBeam);
        }

        // ------------------------------------------------------------ the pier

        /// <summary>
        /// The pier: a deck of boards on posts from the foot of the steps out over the reef, its
        /// bollards, its two lamps, a crate, a keg and a barrel of the fisherman's stores.
        /// </summary>
        private void IslePier()
        {
            for (int x = PierX0; x <= PierX1; x++)
                c.Block(x, PierDeck, TileID.BorealWood);

            foreach (int x in new[] { PierX0 + 1, PierX0 + 6, PierX0 + 12, PierX0 + 18, PierX0 + 24, PierX0 + 30, PierX1 })
                IslePost(x, PierDeck + 1);

            const int stand = PierDeck - 1;
            Bollard(PierX0 + 3, stand);
            Bollard(PierX0 + 21, stand);
            Bollard(PierX1 - 2, stand);
            c.Place(PierX0 + 9, stand, TileID.Lampposts);
            c.Place(PierX0 + 29, stand, TileID.Lampposts);
            c.Place(PierX0 + 13, stand, TileID.FishingCrate);
            c.Place(PierX0 + 15, stand, TileID.Kegs);
            c.PlaceChest(PierX0 + 25, stand, TileID.Containers, 5, Roll(Loot.Sea));
        }

        // ------------------------------------------------------------ the boats

        private static Cut IsleMirror(Cut cut) => cut switch
        {
            Grace.Cut.DownLeft => Grace.Cut.DownRight,
            Grace.Cut.DownRight => Grace.Cut.DownLeft,
            Grace.Cut.UpLeft => Grace.Cut.UpRight,
            Grace.Cut.UpRight => Grace.Cut.UpLeft,
            _ => cut,
        };

        /// <summary>
        /// The skiff, the fisherman's rowing boat, tied at the pier's end: twelve long, her bow
        /// toward <paramref name="dir"/>, in section as the ships of the capital are drawn: her
        /// bottom boards and her posts, the far side of her planking behind, blue, a white strake
        /// along her top; a crate in her. Dry inside: the sea is let back only where it can reach.
        /// </summary>
        private void IsleSkiff(int x0, int dir)
        {
            const int len = 12;
            int X(int u) => dir > 0 ? x0 + u : x0 + len - 1 - u;
            Cut M(Cut cut) => dir > 0 ? cut : IsleMirror(cut);

            for (int u = 0; u < len; u++)
            {
                bool end = u == 0 || u == len - 1;
                bool turn = u == 1 || u == len - 2;
                int top = end ? -3 : -2;
                int bottom = end ? -1 : turn ? 0 : 1;

                for (int y = top; y <= bottom; y++)
                {
                    byte paint = y <= -2 ? White : PaintID.BluePaint;

                    if (end || turn || y == bottom)
                    {
                        c.Block(X(u), y, TileID.BorealWood, paint);
                    }
                    else
                    {
                        c.Erase(X(u), y);
                        c.Wall(X(u), y, WallID.Planked, paint);
                    }
                }
            }

            Cut(X(0), -3, M(Grace.Cut.DownLeft));
            Cut(X(0), -1, M(Grace.Cut.UpLeft));
            Cut(X(1), 0, M(Grace.Cut.UpLeft));
            Cut(X(len - 2), 0, M(Grace.Cut.UpRight));
            Cut(X(len - 1), -1, M(Grace.Cut.UpRight));
            Cut(X(len - 1), -3, M(Grace.Cut.DownRight));

            c.Place(dir > 0 ? X(4) : X(5), 0, TileID.FishingCrate);
        }

        /// <summary>The sloop's hull, by row: white over the deck, deck boards, a white strake, red, black at the waterline.</summary>
        private static byte IsleSloopPaint(int y, int deck) =>
            y < deck ? White : y == deck ? NoPaint : y == deck + 1 ? White : y == 0 ? Black : LightRedPaint;

        /// <summary>
        /// The sloop, riding at anchor past the pier, her bow toward the island at column
        /// <paramref name="bow"/>: twenty-three long, a hull of red under a white strake, black at
        /// the waterline; a cabin on her deck, white, a door aft and a light forward, her chest and
        /// a lantern in it; one mast, the mainsail on its boom and the jib to the bowsprit set; her
        /// anchor chain down from the bow to the shelf, the anchor lying on it.
        /// </summary>
        private void IsleSloop(int bow)
        {
            const int len = 23, deck = -3;
            int X(int u) => bow + len - 1 - u;

            int Bottom(int u)
            {
                if (u < 3)
                    return u - 1;
                if (u <= 16)
                    return 2;
                return 2 - (int)Math.Round(5 * Math.Pow((u - 16) / 6.0, 1.5));
            }

            for (int u = 0; u < len; u++)
            {
                int top = u == len - 1 ? deck - 2 : u == 0 ? deck - 1 : deck;
                for (int y = top; y <= Bottom(u); y++)
                    c.Block(X(u), y, y <= deck ? TileID.BorealWood : TileID.WoodBlock, IsleSloopPaint(y, deck));
            }

            for (int u = 0; u < len; u++)
            {
                int b = Bottom(u);
                if (u + 1 < len && Bottom(u + 1) > b)
                    Cut(X(u), b, Grace.Cut.UpRight);
                if (u > 0 && Bottom(u - 1) > b)
                    Cut(X(u), b, Grace.Cut.UpLeft);
            }

            Cut(X(len - 1), deck - 2, Grace.Cut.DownLeft);
            Cut(X(0), deck - 1, Grace.Cut.DownRight);

            for (int u = 1; u < len - 1; u++)
                c.Wall(X(u), deck - 1, WallID.WoodenFence);

            // The cabin.
            int c0 = X(12), c1 = X(6);
            Box(c0, deck - 6, c1, deck - 1, TileID.WoodBlock, White);
            Hollow(c0 + 1, deck - 5, c1 - 1, deck - 1, WallID.Planked);
            Box(c0 - 1, deck - 6, c1 + 1, deck - 6, TileID.BorealWood);
            Cut(c0 - 1, deck - 6, Grace.Cut.UpLeft);
            Cut(c1 + 1, deck - 6, Grace.Cut.UpRight);
            Air(c1, deck - 3, c1, deck - 1);
            c.Place(c1, deck - 1, TileID.ClosedDoor, IslePalm.Door);
            Glaze(c0, deck - 4, c0, deck - 3);
            Back(c0 + 2, deck - 4, c0 + 3, deck - 3, WallID.Glass);
            c.PlaceChest(c0 + 1, deck - 1, TileID.Containers, IslePalm.Chest, Roll(Loot.Sea));
            c.Place(c0 + 3, deck - 1, TileID.FishingCrate);
            Hang(c0 + 4, deck - 5, TileID.HangingLanterns, IslePalm.Lantern);

            // The mast, the boom, the mainsail.
            int mx = X(15);
            int head = deck - 25;
            for (int y = head - 1; y < deck; y++)
                c.Block(mx, y, TileID.WoodenBeam);

            int boom = deck - 7, clew = X(2);
            for (int x = mx + 1; x <= clew; x++)
                c.Block(x, boom, TileID.BorealWood, NoPaint, x == clew ? Grace.Cut.Full : Grace.Cut.Half);

            for (int y = head; y < boom; y++)
            {
                double t = (y - head) / (double)(boom - 1 - head);
                int reach = (int)Math.Round(t * (clew - mx - 1) + 1.2 * Math.Sin(t * Math.PI));

                for (int x = mx + 1; x <= Math.Min(clew, mx + 1 + reach); x++)
                    if (c.TypeAt(x, y) < 0)
                        c.Wall(x, y, WallID.Sail);
            }

            // The bowsprit and the jib.
            int stem = deck - 2;
            for (int i = 1; i <= 4; i++)
                c.Block(bow - i, stem + 1, TileID.BorealWood, NoPaint, i == 4 ? Grace.Cut.Full : Grace.Cut.Half);

            int tack = bow - 4, jibHead = head + 2;
            for (int x = tack + 1; x < mx; x++)
            {
                double t = (x - tack) / (double)(mx - tack);
                int stay = (int)Math.Round(stem + (jibHead - stem) * t);
                int foot = (int)Math.Round(stem + (deck - 3 - stem) * t);

                for (int y = stay; y <= foot; y++)
                    if (c.TypeAt(x, y) < 0 && c.WallAt(x, y) == 0)
                        c.Wall(x, y, WallID.Sail);
            }

            // Her anchor: the chain down from the hawse, the anchor on the shelf.
            int ax = bow + 1;
            int from = Bottom(len - 2) + 1;
            int g = c.FirstSolidDown(ax, from, from + 60);
            if (g == int.MinValue)
                return;

            for (int y = from; y < g - 3; y++)
                if (c.TypeAt(ax, y) < 0)
                    c.Block(ax, y, TileID.Chain);

            c.Block(ax - 1, g - 1, TileID.IronBrick, NoPaint, Grace.Cut.DownRight);
            c.Block(ax, g - 1, TileID.IronBrick);
            c.Block(ax + 1, g - 1, TileID.IronBrick, NoPaint, Grace.Cut.DownLeft);
            c.Block(ax, g - 2, TileID.IronBrick);
            c.Block(ax, g - 3, TileID.IronBrick, NoPaint, Grace.Cut.Half);
        }

        // ------------------------------------------------------------ the reef

        /// <summary>
        /// Low mounds of coralstone on the shelf and on the floors of the arch and the cave, where
        /// the reef has built itself up: a row or two over the sand, their shoulders sloped.
        /// </summary>
        private void IsleMounds()
        {
            foreach (var (mx, half, high) in new[] { (-26, 2, 1), (2, 2, 1), (27, 2, 1), (237, 2, 1), (243, 2, 2), (266, 2, 1), (284, 1, 1), (298, 3, 2), (307, 2, 1) })
            {
                var tops = new int[half * 2 + 1];

                for (int dx = -half; dx <= half; dx++)
                {
                    int x = mx + dx;
                    tops[dx + half] = int.MaxValue;
                    int g = c.FirstSolidDown(x, 1, 60);
                    if (g == int.MinValue || g < 3 || c.TypeAt(x, g - 1) >= 0)
                        continue;

                    int rise = Math.Max(1, (int)Math.Round(high * (1 - Math.Abs(dx) / (double)(half + 1)) + 0.4));
                    for (int y = g - rise; y < g; y++)
                        c.Block(x, y, TileID.Coralstone);
                    tops[dx + half] = g - rise;
                }

                for (int dx = -half; dx <= half; dx++)
                {
                    int top = tops[dx + half];
                    if (top == int.MaxValue)
                        continue;

                    bool lowLeft = dx == -half || tops[dx + half - 1] > top;
                    bool lowRight = dx == half || tops[dx + half + 1] > top;

                    if (lowLeft && lowRight)
                        Cut(mx + dx, top, Grace.Cut.Half);
                    else if (lowLeft)
                        Cut(mx + dx, top, Grace.Cut.DownLeft);
                    else if (lowRight)
                        Cut(mx + dx, top, Grace.Cut.DownRight);
                }
            }
        }

        /// <summary>
        /// The living reef, wherever the ground under the water is whole and free: beds of kelp on
        /// the sand where the water is deep enough for it, coral, shells and starfish, an anemone or
        /// a cup sponge now and then (the sea's own, SeaLife).
        /// </summary>
        private void IsleReef()
        {
            int seed = c.Next(1, 1 << 20);

            for (int x = IsleX0 + 2; x <= IsleX1 - 2; x++)
            {
                int g = c.FirstSolidDown(x, 1, 80);
                if (g == int.MinValue || g < 3 || c.TypeAt(x, g - 1) >= 0 || c.CutAt(x, g) != Grace.Cut.Full)
                    continue;

                int t = c.TypeAt(x, g);
                bool sand = t == TileID.Sand;
                if (!sand && t != TileID.Coralstone && t != TileID.Stone && t != TileID.Granite && t != TileID.GreenMoss && t != TileID.HardenedSand)
                    continue;

                if (sand && isle.Kelp != null && g >= 7 && Noise(x, 0, 7.0, seed) > 0.56 && c.OneIn(2))
                {
                    isle.Kelp(x, g, c.Next(3, g - 2));
                    continue;
                }

                int roll = c.Next(100);
                if (roll < 20)
                    c.Place(x, g - 1, TileID.Coral);
                else if (roll < 30 && (sand || t == TileID.Coralstone))
                    c.Place(x, g - 1, TileID.BeachPiles, IsleShell());
                else if (roll < 37)
                    IsleLife(x, g, sand);
            }
        }

        /// <summary>An anemone on the sand, a cup sponge on rock as often as not, two wide: the column to the right whole and free as well.</summary>
        private void IsleLife(int x, int g, bool sand)
        {
            if (isle.Life == null)
                return;

            int kind = sand || c.OneIn(2) ? isle.Anemone : isle.CupSponge;
            if (kind < 0)
                return;

            if (c.TypeAt(x + 1, g - 1) >= 0 || c.TypeAt(x + 1, g - 2) >= 0 || c.TypeAt(x, g - 2) >= 0
                || !c.Solid(x + 1, g) || c.CutAt(x + 1, g) != Grace.Cut.Full || c.TypeAt(x + 1, g) == TileID.Platforms)
                return;

            isle.Life(x, g - 1, kind, c.Next(0, 2));
        }

        /// <summary>Which of the beach's shells, at the game's own odds (VastOceanBuild.ShellStyle).</summary>
        private int IsleShell()
        {
            int style = c.Next(2);

            if (c.OneIn(10))
                style = 2;
            if (c.OneIn(10))
                style = 3;
            if (c.OneIn(50))
                style = 4;

            return style;
        }

        // ------------------------------------------------------------ underneath

        /// <summary>The columns the chains come down at: under the headland, and under the plateau's east end.</summary>
        private static readonly int[] IsleChainAt = { 70, 176 };

        /// <summary>
        /// The underside: spikes of rock hanging down from it every few columns, longer where it
        /// is deepest, each narrowing to its point and leaning a little; none where a chain comes
        /// down.
        /// </summary>
        private void IsleUnderside()
        {
            int seed = c.Next(1, 1 << 20);
            int x = -10 + c.Next(0, 4);

            while (x < 314)
            {
                int bottom = IsleBottomAt(x);
                bool clear = Array.TrueForAll(IsleChainAt, cx => Math.Abs(x - cx) >= 9);

                if (bottom != int.MinValue && clear)
                {
                    double deep = Math.Clamp((bottom - 20) / 86.0, 0.0, 1.0);
                    int len = 6 + (int)Math.Round((6 + 16 * deep) * (0.4 + 0.6 * Hash01(x, 1, seed)));
                    double half = 1.5 + (1.0 + 1.5 * deep) * Hash01(x, 2, seed);
                    double lean = (Hash01(x, 3, seed) - 0.5) * 0.4;
                    IsleSpike(x, bottom, len, half, lean, seed);
                }

                x += 5 + c.Next(0, 9);
            }
        }

        /// <summary>A spike of rock down from row <paramref name="from"/>, joined to the rock over it wherever the underside rises.</summary>
        private void IsleSpike(int x, int from, int len, double half, double lean, int seed)
        {
            for (int s = 1; s <= len; s++)
            {
                double f = s / (double)len;
                double mid = x + 0.5 + lean * len * f * f;
                double r = Math.Max(0.4, half * Math.Pow(1 - f, 0.85)) + 0.5 * (Noise(x, s, 3.0, seed + 13) - 0.5);
                int y = from + s;

                for (int px = (int)Math.Floor(mid - r - 1); px <= (int)Math.Ceiling(mid + r + 1); px++)
                {
                    if (Math.Abs(px + 0.5 - mid) > r || c.TypeAt(px, y) >= 0)
                        continue;

                    c.Block(px, y, IsleStone(px, y, seed));

                    for (int up = y - 1; up > y - 12 && c.TypeAt(px, up) < 0; up--)
                        c.Block(px, up, IsleStone(px, up, seed));
                }
            }
        }

        /// <summary>
        /// The chains: from an iron plate in the rock at each of <see cref="IsleChainAt"/>, straight
        /// down through the sea to the floor, and an anchor on the floor at the foot of each.
        /// </summary>
        private void IsleChains()
        {
            foreach (int x in IsleChainAt)
            {
                int y = IsleBottomAt(x);
                if (y == int.MinValue)
                    continue;

                while (c.Solid(x, y + 1))
                    y++;

                for (int dx = -1; dx <= 1; dx++)
                    c.Block(x + dx, y, TileID.IronBrick);

                int from = y + 1, to = from;
                while (to - from < isle.Deepest && c.InBounds(x, to) && c.TypeAt(x, to) < 0)
                    to++;

                if (!c.InBounds(x, to) || c.TypeAt(x, to) < 0)
                    continue;

                for (int yy = from; yy < to - 10; yy++)
                    c.Block(x, yy, TileID.Chain);

                IsleAnchor(x, to);
                c.Mark("Isle.Anchor", x - 3, to - 10, 7, 10);
            }
        }

        /// <summary>A great anchor of iron, rusted, lying on the floor at row <paramref name="floor"/>: its arms and flukes, its shank, its stock, the ring the chain is made fast to.</summary>
        private void IsleAnchor(int x, int floor)
        {
            const byte rust = PaintID.BrownPaint;
            int y = floor - 1;

            for (int dx = -3; dx <= 3; dx++)
                c.Block(x + dx, y, TileID.IronBrick, rust, dx == -3 ? Grace.Cut.UpLeft : dx == 3 ? Grace.Cut.UpRight : Grace.Cut.Full);

            c.Block(x - 3, y - 1, TileID.IronBrick, rust, Grace.Cut.DownRight);
            c.Block(x + 3, y - 1, TileID.IronBrick, rust, Grace.Cut.DownLeft);

            for (int yy = y - 7; yy < y; yy++)
                c.Block(x, yy, TileID.IronBrick, rust);

            for (int dx = -3; dx <= 3; dx++)
                c.Block(x + dx, y - 8, TileID.IronBrick, rust);

            c.Block(x, y - 9, TileID.IronBrick, rust, Grace.Cut.Half);
        }

        // ------------------------------------------------------------ what grows and lies about

        /// <summary>
        /// The palms on the beach, grown as the game grows them; grass and flowers on the turf
        /// wherever nothing stands, a mossy stone or two on the saddle; sea oats and shells on the
        /// sand (the game's large piles will not lie on sand); and on the ledge at the back of the
        /// sea cave, a chest someone left there and the bones by it.
        /// </summary>
        private void IsleGreen()
        {
            if (isle.Palm != null)
            {
                foreach (int x in new[] { StairX1 + 5, StairX1 + 11 })
                {
                    int g = c.FirstSolidDown(x, -20, 10);
                    if (g == int.MinValue)
                        continue;

                    for (int dx = -1; dx <= 1; dx++)
                        if (c.TypeAt(x + dx, g) == TileID.Sand)
                            c.Block(x + dx, g, TileID.Sand);

                    isle.Palm(x, g);
                }
            }

            // Mossy stones where the saddle lies level.
            for (int x = 72; x <= PlateauX0 - 4; x += 7)
            {
                int g = c.FirstSolidDown(x + 1, -70, 0);
                if (g != int.MinValue && c.TypeAt(x + 1, g) == TileID.Grass && IsleLevel(x, x + 2, g, TileID.Grass))
                    c.Place(x, g - 1, TileID.LargePiles2, c.Pick(14, 15, 16));
            }

            for (int x = 64; x <= PlateauX1; x++)
            {
                int g = c.FirstSolidDown(x, -70, 0);
                if (g == int.MinValue || c.TypeAt(x, g) != TileID.Grass || c.TypeAt(x, g - 1) >= 0 || c.CutAt(x, g) != Grace.Cut.Full)
                    continue;

                if (!c.OneIn(3))
                    c.Place(x, g - 1, TileID.Plants, c.OneIn(5) ? c.Next(6, 11) : c.Next(0, 6));
            }

            for (int x = StairX1 + 1; x < HutX0; x++)
            {
                int g = c.FirstSolidDown(x, -20, 0);
                if (g == int.MinValue || c.TypeAt(x, g) != TileID.Sand || c.TypeAt(x, g - 1) >= 0 || c.CutAt(x, g) != Grace.Cut.Full)
                    continue;

                int roll = c.Next(10);
                if (roll < 3)
                    c.Place(x, g - 1, TileID.SeaOats);
                else if (roll < 4)
                    c.Place(x, g - 1, TileID.BeachPiles, IsleShell());
            }

            // The ledge in the sea cave.
            if (IsleLevel(32, 36, -2, TileID.Stone))
            {
                c.PlaceChest(32, -3, TileID.Containers, 0, Roll(Loot.Sea));
                c.Place(34, -3, TileID.LargePiles2, 9);
            }
        }

        /// <summary>Ground made whole and level at row <paramref name="g"/> across [x0, x1], of this kind, with room over it: false if something not of the ground is in the way.</summary>
        private bool IsleLevel(int x0, int x1, int g, ushort type)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = g - 3; y < g; y++)
                    if (c.TypeAt(x, y) >= 0 && !IsleNatural(c.TypeAt(x, y)))
                        return false;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = g - 3; y < g; y++)
                    c.Erase(x, y);

                c.Block(x, g, type);
                if (!c.Solid(x, g + 1))
                    c.Block(x, g + 1, type == TileID.Grass ? TileID.Dirt : type == TileID.Sand ? TileID.HardenedSand : type);
            }

            return true;
        }

        /// <summary>
        /// Vines: strands of green from the undersides of the rock over the water, the arch's head,
        /// the cave's roof, the crags' overhangs; and under the water, weed hanging from the
        /// island's underside where it is shallow.
        /// </summary>
        private void IsleVines()
        {
            byte[] green = { PaintID.DeepGreenPaint, PaintID.DeepGreenPaint, PaintID.GreenPaint, NoPaint, PaintID.DeepLimePaint };

            for (int x = IsleX0 + 1; x < IsleX1; x++)
            {
                for (int y = IsleY0 + 1; y < 40; y++)
                {
                    int t = c.TypeAt(x, y);
                    if (t != TileID.Stone && t != TileID.Granite && t != TileID.GreenMoss)
                        continue;
                    if (c.TypeAt(x, y + 1) >= 0 || !c.OneIn(y < 0 ? 5 : 4))
                        continue;

                    bool wet = y >= 0;
                    int len = wet ? c.Next(2, 8) : c.Next(2, 7);
                    byte paint = green[c.Next(0, green.Length)];

                    for (int i = 1; i <= len; i++)
                    {
                        int yy = y + i;
                        if (!wet && yy >= 0 || c.TypeAt(x, yy) >= 0 || c.TypeAt(x, yy + 1) >= 0)
                            break;

                        c.Block(x, yy, TileID.VineRope, paint);
                    }
                }
            }
        }
    }
}
