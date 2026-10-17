using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Deep
{
    /// <summary>
    /// A crevice an eel lives in (VastOceanFloor.Crevices cuts them): a straight burrow into the
    /// rock, two cells across, <see cref="Length"/> long. (<see cref="X"/>, <see cref="Y"/>) is its
    /// back, the deepest cell of it; the other cell across is the next row down for one that runs
    /// across, the next column right for one that runs up or down; (<see cref="Dx"/>,
    /// <see cref="Dy"/>) is the way out of it.
    /// </summary>
    public readonly struct EelLair
    {
        public readonly int X, Y, Dx, Dy, Length;

        public EelLair(int x, int y, int dx, int dy, int length)
        {
            X = x;
            Y = y;
            Dx = dx;
            Dy = dy;
            Length = length;
        }

        /// <summary>The crevice's middle line at this many cells out from its back (0, its back cell's middle), in world pixels: between its two cells across.</summary>
        public Vector2 Point(float k)
        {
            return Dy == 0
                ? new Vector2((X + Dx * k) * 16f + 8f, (Y + 1) * 16f)
                : new Vector2((X + 1) * 16f, (Y + Dy * k) * 16f + 8f);
        }

        /// <summary>Its mouth's cell's middle: where a lurking eel's head is.</summary>
        public Vector2 Mouth => Point(Length - 1);

        public bool Same(EelLair other) => X == other.X && Y == other.Y && Dx == other.Dx && Dy == other.Dy;
    }

    /// <summary>
    /// Finding the eels' crevices in the world as it is, the game's tiles read: the same
    /// shape VastOceanFloor.Crevices cuts (<see cref="At"/>).
    /// </summary>
    public static class EelLairs
    {
        /// <summary>The shortest and the longest a crevice may be to be one.</summary>
        public const int Shortest = 10;
        public const int Longest = 26;

        /// <summary>Rock, as a crevice is made of it: a solid tile (a slope is rock; a platform is not); and anything off the world.</summary>
        public static bool Rock(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return true;

            Tile t = Main.tile[x, y];
            return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
        }

        /// <summary>
        /// A crevice whose back is (<paramref name="x"/>, <paramref name="y"/>), running out the way
        /// given: two rows of rock six across behind its back; then, cell by cell out from it, its
        /// two cells open with two cells of rock either side of them, for <see cref="Shortest"/>
        /// to <see cref="Longest"/> cells; then its two cells open for two more, where the rock
        /// either side gives out (its mouth). Its length, out to where the rock gives out.
        /// </summary>
        public static bool At(int x, int y, int dx, int dy, out int length)
        {
            length = 0;
            int px = dy == 0 ? 0 : 1, py = dy == 0 ? 1 : 0;

            for (int k = -2; k <= -1; k++)
            {
                for (int j = -2; j <= 3; j++)
                {
                    if (!Rock(x + dx * k + px * j, y + dy * k + py * j))
                        return false;
                }
            }

            int run = 0;

            for (int k = 0; k <= Longest + 1; k++)
            {
                int cx = x + dx * k, cy = y + dy * k;

                if (Rock(cx, cy) || Rock(cx + px, cy + py))
                    return false;

                if (!Rock(cx - px, cy - py) || !Rock(cx - 2 * px, cy - 2 * py) || !Rock(cx + 2 * px, cy + 2 * py) || !Rock(cx + 3 * px, cy + 3 * py))
                    break;

                run = k + 1;
            }

            if (run < Shortest || run > Longest)
                return false;

            for (int k = run; k <= run + 1; k++)
            {
                int cx = x + dx * k, cy = y + dy * k;

                if (Rock(cx, cy) || Rock(cx + px, cy + py))
                    return false;
            }

            length = run;
            return true;
        }

        /// <summary>The four ways out a crevice can run.</summary>
        private static readonly Point[] Outs = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };

        /// <summary>Every crevice whose back is in this box, into the list.</summary>
        public static void Find(int x0, int y0, int x1, int y1, List<EelLair> into)
        {
            for (int y = Math.Max(2, y0); y <= Math.Min(Main.maxTilesY - 3, y1); y++)
            {
                for (int x = Math.Max(2, x0); x <= Math.Min(Main.maxTilesX - 3, x1); x++)
                {
                    if (Rock(x, y))
                        continue;

                    foreach (Point o in Outs)
                    {
                        // Cheap first: rock right behind it, and its other cell across open.
                        if (!Rock(x - o.X, y - o.Y))
                            continue;

                        if (At(x, y, o.X, o.Y, out int length))
                            into.Add(new EelLair(x, y, o.X, o.Y, length));
                    }
                }
            }
        }
    }

    /// <summary>
    /// The eels put in their crevices, where the world is decided (the server, or the one
    /// player's own game): for each player in the Vast Ocean from the 8th section down, every
    /// eight to twenty seconds, if fewer than <see cref="MostNear"/> eels are within
    /// <see cref="NearTiles"/> tiles of them, one is put in a crevice
    /// (<see cref="EelLairs"/>) no eel has, within <see cref="ReachTiles"/> tiles of them and out
    /// of their sight, lurking there (CreviceEelHead). The crevices are found once for each
    /// stretch of forty by forty tiles and kept, and looked at again before one is used.
    /// </summary>
    public class CreviceEelSpawns : ModSystem
    {
        public const int MostNear = 2;
        public const int NearTiles = 150;
        public const int ReachTiles = 110;

        /// <summary>Out of a player's sight: farther than this across, or this up or down.</summary>
        private const int UnseenAcross = 66;
        private const int UnseenUp = 40;

        private const int Stretch = 40;

        private static readonly Dictionary<long, List<EelLair>> found = new Dictionary<long, List<EelLair>>();
        private static readonly int[] wait = new int[Main.maxPlayers];

        public override void OnWorldLoad() => found.Clear();

        public override void OnWorldUnload() => found.Clear();

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || SubworldKeeper.Current is not VastOcean)
                return;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead || player.ghost)
                    continue;

                if (--wait[player.whoAmI] > 0)
                    continue;

                wait[player.whoAmI] = Main.rand.Next(8 * 60, 20 * 60 + 1);

                if (VastOcean.RowOf(player) >= VastOcean.SectionTop(8))
                    TryPut(player);
            }
        }

        private static List<EelLair> In(int sx, int sy)
        {
            long key = (long)sy << 32 | (uint)sx;

            if (!found.TryGetValue(key, out List<EelLair> list))
            {
                list = new List<EelLair>();
                EelLairs.Find(sx * Stretch, sy * Stretch, sx * Stretch + Stretch - 1, sy * Stretch + Stretch - 1, list);
                found[key] = list;
            }

            return list;
        }

        private static void TryPut(Player player)
        {
            int headType = ModContent.NPCType<CreviceEelHead>();
            int near = 0;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == headType && npc.Distance(player.Center) < NearTiles * 16f)
                    near++;
            }

            if (near >= MostNear)
                return;

            int px = (int)(player.Center.X / 16f), py = (int)(player.Center.Y / 16f);
            var choices = new List<EelLair>();

            for (int sy = (py - ReachTiles) / Stretch; sy <= (py + ReachTiles) / Stretch; sy++)
            {
                for (int sx = (px - ReachTiles) / Stretch; sx <= (px + ReachTiles) / Stretch; sx++)
                {
                    if (sx < 0 || sy < 0)
                        continue;

                    foreach (EelLair lair in In(sx, sy))
                    {
                        Vector2 mouth = lair.Mouth / 16f;
                        float dx = Math.Abs(mouth.X - px), dy = Math.Abs(mouth.Y - py);

                        if (dx * dx + dy * dy > ReachTiles * ReachTiles || dx <= UnseenAcross && dy <= UnseenUp)
                            continue;

                        if (!Taken(lair, headType))
                            choices.Add(lair);
                    }
                }
            }

            while (choices.Count > 0)
            {
                int i = Main.rand.Next(choices.Count);
                EelLair lair = choices[i];

                choices.RemoveAt(i);

                // The world may have changed since it was found (it can be dug).
                if (!EelLairs.At(lair.X, lair.Y, lair.Dx, lair.Dy, out int length))
                    continue;

                lair = new EelLair(lair.X, lair.Y, lair.Dx, lair.Dy, length);
                Vector2 mouth = lair.Mouth;
                int index = NPC.NewNPC(new Terraria.DataStructures.EntitySource_SpawnNPC("PisAndDag:CreviceEel"), (int)mouth.X, (int)mouth.Y + 12, headType);

                if (index < Main.maxNPCs && Main.npc[index].ModNPC is CreviceEelHead eel)
                    eel.Settle(lair);

                return;
            }
        }

        /// <summary>An eel lives in this crevice already.</summary>
        private static bool Taken(EelLair lair, int headType)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == headType && npc.ModNPC is CreviceEelHead eel && eel.Lives(lair))
                    return true;
            }

            return false;
        }
    }
}
