using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Lab
{
    /// <summary>
    /// How the Cultures find their way through the Abandoned Lab, and where they come from.
    ///
    ///   the way      one map of the lab, shared by every Culture in it: for each open cell, how many steps it is from
    ///                whoever they hunt (a breadth-first count out from their cell through every cell that is not solid;
    ///                platforms are open), made again a third of a second at a time, and only while a Culture is about.
    ///                A Culture goes downhill on it, as far ahead as it can see in a straight line, up to six cells; when
    ///                it can see them, and they are near, it goes straight at them.
    ///   the nests    LabPlan.Nests: places in the rooms, the corridor and the ducts. The first comes forty-five
    ///                seconds after one arrives, then one every one to two and a half minutes while fewer than two are
    ///                about (by request: "they should spawn rarer"); never
    ///                within forty-five tiles of them, and never where they could see it come.
    ///
    /// The lab only; nothing of this runs anywhere else.
    /// </summary>
    public class CulturePath : ModSystem
    {
        /// <summary>Ticks between one making of the map and the next; how near (pixels) and in sight it goes straight at them.</summary>
        private const int Every = 20;
        private const float Straight = 320f;

        /// <summary>How far ahead it looks along the way, in cells.</summary>
        private const int Ahead = 6;

        private const ushort Unreached = ushort.MaxValue;

        /// <summary>Most about at once; the first after (ticks); the rest after (ticks, least and most); never nearer than (tiles).</summary>
        public const int Most = 2;
        private const int First = 2700, SoonestAgain = 3600, LatestAgain = 9000;
        private const int Apart = 45;

        private static ushort[] steps;
        private static int[] queue;
        private static int wide, high;
        private static uint madeWhen;
        private static int madeFor = -1;

        private static int nextNest = First;

        public override void ClearWorld()
        {
            steps = null;
            queue = null;
            madeFor = -1;
            nextNest = First;
        }

        internal static bool InLab => SubworldKeeper.Current is AbandonedLab;

        /// <summary>In the safe room (LabPlan.SafeRoom), where none of them goes.</summary>
        internal static bool InSafe(int x, int y)
        {
            var s = LabPlan.SafeRoom;
            return x >= s.X0 && x <= s.X1 && y >= s.Y0 && y <= s.Y1;
        }

        // ---- the way -------------------------------------------------------------------------------------------------

        /// <summary>Which way it should go now, to get to them: straight, if they are near and in sight; else down the map.</summary>
        public static Vector2 Toward(NPC npc, Player prey)
        {
            Vector2 straight = (prey.Center - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);

            if (Vector2.Distance(prey.Center, npc.Center) < Straight && Collision.CanHitLine(npc.position, npc.width, npc.height, prey.position, prey.width, prey.height))
                return straight;

            if (!InLab)
                return straight;

            Make(prey);

            Point at = npc.Center.ToTileCoordinates();

            if (steps == null || !Inside(at.X, at.Y))
                return straight;

            // Its middle may be in a cell that reads as solid (a slope's edge): start from the best open one beside it.
            if (StepsAt(at.X, at.Y) == Unreached)
            {
                Point next = Downhill(at);

                if (next == at)
                    return straight;

                at = next;
            }

            Point best = Downhill(at);

            if (best == at)
                return straight;

            Point on = best;

            // Balled up in a gap a tile wide, a cell at a time; otherwise as far ahead as it can see.
            int ahead = npc.width <= 18 && npc.height <= 18 ? 1 : Ahead;

            for (int k = 1; k < ahead; k++)
            {
                Point next = Downhill(on);

                if (next == on || !Clear(npc.Center, next))
                    break;

                on = best = next;
            }

            return (best.ToWorldCoordinates() - npc.Center).SafeNormalize(straight);
        }

        /// <summary>A straight line from here to the middle of that cell with nothing solid on it.</summary>
        private static bool Clear(Vector2 from, Point cell)
        {
            Vector2 to = cell.ToWorldCoordinates();
            return Collision.CanHitLine(from - Vector2.One, 2, 2, to - Vector2.One, 2, 2);
        }

        /// <summary>The cell next to this one that is fewest steps from them (straight ones first; a corner only if both cells beside it are open); this one if none is fewer.</summary>
        private static Point Downhill(Point from)
        {
            Point best = from;
            int least = StepsAt(from.X, from.Y);

            for (int k = 0; k < 8; k++)
            {
                int dx = k switch { 0 => 1, 1 => -1, 2 => 0, 3 => 0, 4 => 1, 5 => 1, 6 => -1, _ => -1 };
                int dy = k switch { 0 => 0, 1 => 0, 2 => 1, 3 => -1, 4 => 1, 5 => -1, 6 => 1, _ => -1 };
                int x = from.X + dx, y = from.Y + dy;

                if (!Inside(x, y))
                    continue;

                if (dx != 0 && dy != 0 && (!Open(from.X + dx, from.Y) || !Open(from.X, from.Y + dy)))
                    continue;

                int s = StepsAt(x, y);

                if (s < least)
                {
                    least = s;
                    best = new Point(x, y);
                }
            }

            return best;
        }

        private static bool Inside(int x, int y) => x > 1 && y > 1 && x < wide - 2 && y < high - 2;

        private static int StepsAt(int x, int y) => steps == null ? Unreached : steps[y * wide + x];

        /// <summary>A cell a Culture can be in: anything but a solid block (platforms are open), and nowhere in the safe room.</summary>
        private static bool Open(int x, int y)
        {
            if (InSafe(x, y))
                return false;

            Tile tile = Main.tile[x, y];
            return !tile.HasTile || tile.IsActuated || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];
        }

        /// <summary>The map, made again if it is old or for someone else: every open cell's steps out from theirs.</summary>
        private static void Make(Player prey)
        {
            if (steps != null && madeFor == prey.whoAmI && Main.GameUpdateCount - madeWhen < Every)
                return;

            if (steps == null || wide != Main.maxTilesX || high != Main.maxTilesY)
            {
                wide = Main.maxTilesX;
                high = Main.maxTilesY;
                steps = new ushort[wide * high];
                queue = new int[wide * high];
            }

            madeFor = prey.whoAmI;
            madeWhen = Main.GameUpdateCount;
            Array.Fill(steps, Unreached);

            Point start = prey.Center.ToTileCoordinates();

            if (!Inside(start.X, start.Y))
                return;

            if (!Open(start.X, start.Y) && Inside(start.X, start.Y - 1))
                start.Y--;

            int head = 0, tail = 0;
            steps[start.Y * wide + start.X] = 0;
            queue[tail++] = start.Y * wide + start.X;

            while (head < tail)
            {
                int cell = queue[head++];
                int x = cell % wide, y = cell / wide;
                ushort next = (ushort)Math.Min(steps[cell] + 1, Unreached - 1);

                Visit(x + 1, y, next, ref tail);
                Visit(x - 1, y, next, ref tail);
                Visit(x, y + 1, next, ref tail);
                Visit(x, y - 1, next, ref tail);
            }
        }

        private static void Visit(int x, int y, ushort s, ref int tail)
        {
            if (!Inside(x, y))
                return;

            int cell = y * wide + x;

            if (steps[cell] != Unreached || !Open(x, y))
                return;

            steps[cell] = s;
            queue[tail++] = cell;
        }

        // ---- the nests -------------------------------------------------------------------------------------------------

        /// <summary>In the lab, while fewer than two are about: one now and then, from a nest far from them and out of their sight.</summary>
        public override void PostUpdateNPCs()
        {
            if (!InLab || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Player player = Main.player[Main.myPlayer];

            if (Main.netMode == NetmodeID.Server)
            {
                int near = Player.FindClosest(new Vector2(LabPlan.Wide * 8f, LabPlan.High * 8f), 1, 1);
                player = Main.player[near];
            }

            if (player == null || !player.active || player.dead)
                return;

            int type = ModContent.NPCType<Culture>();

            if (NPC.CountNPCS(type) >= Most)
            {
                if (nextNest < SoonestAgain)
                    nextNest = Main.rand.Next(SoonestAgain, LatestAgain);

                return;
            }

            if (--nextNest > 0)
                return;

            nextNest = Main.rand.Next(SoonestAgain, LatestAgain);

            Rectangle seen = new Rectangle((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
            seen.Inflate(160, 160);

            if (Main.netMode == NetmodeID.Server)
                seen = new Rectangle((int)player.Center.X - 1200, (int)player.Center.Y - 700, 2400, 1400);

            int start = Main.rand.Next(LabPlan.Nests.Length);

            for (int k = 0; k < LabPlan.Nests.Length; k++)
            {
                (int x, int y) = LabPlan.Nests[(start + k) % LabPlan.Nests.Length];
                Vector2 at = new Vector2(x * 16f, y * 16f);

                if (Vector2.Distance(at, player.Center) < Apart * 16f || seen.Contains(at.ToPoint()))
                    continue;

                if (!Open(x, y) || !Open(x - 1, y) || !Open(x, y - 1) || !Open(x - 1, y - 1))
                    continue;

                NPC.NewNPC(new EntitySource_SpawnNPC(), x * 16, (y + 1) * 16, type);
                return;
            }
        }
    }
}
