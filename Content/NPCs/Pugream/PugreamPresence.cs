using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Structures;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Pugream
{
    /// <summary>
    /// Where Pugream is, and when, after the Eye.
    ///
    /// Before the Eye has been seen off the way EyeFinale does it, he is nowhere. After,
    /// he is on the shore: the sea on the side of the world he was standing on when he
    /// took the shot, a few steps up the sand from the water, looking out at it. He is
    /// there until somebody has spoken to him once, and then the first time nobody is
    /// looking he is not.
    ///
    /// Stage is the milestone, saved with the world, like every other one in the book.
    ///
    ///   0  not yet. The Eye has not been beaten this way, or not at all
    ///   1  on the shore, waiting to be found
    ///   2  spoken to. Gone as soon as he is off everyone's screen
    ///
    /// Placed the way Mors is (MorsPresence): he is an ordinary NPC underneath and
    /// despawns when everyone is far off; this puts him back when somebody comes near,
    /// before they can see the spot, so to them he never left.
    ///
    /// HIS FIRE, by request ("he is by a fire place 6 blocks away from him(destroy any
    /// ambientt object)"): a Campfire up the sand behind him, with six columns clear between
    /// the one he stands on and the nearest of its three. Laid the first time he is put down
    /// on the shore, unseen as he is, and only that once (<see cref="FireLaid"/>, saved with
    /// the world): taken away, it is not laid again. Where it goes, the game's ambient
    /// objects are taken up first (its piles, shells, sea oats and what a blade cuts), and
    /// between him and it as well; the ground under it is made level, sand put into what is
    /// lower; and if the spot will not take it (a tree in it, something built, ground too
    /// far from flat) the next column further up the sand is tried, three more at most, and
    /// failing those it is tried again the next time he is put down. Neither he nor his fire
    /// is ever put on the waygate's shrine (BeachWaygate).
    ///
    /// The server decides. Stage 2 is set by whoever talks to him, on their own machine,
    /// the same as Mors's hill; in multiplayer the server does not hear of it, and he
    /// stays on the shore for everyone else.
    /// </summary>
    public class PugreamPresence : ModSystem
    {
        public static int Stage;

        /// <summary>Which sea: -1 the left edge of the world, 1 the right. 0 before there is one.</summary>
        public static int BeachSide;

        /// <summary>Somebody this close to the spot keeps him there.</summary>
        private const float NearRange = 2600f;

        /// <summary>He is never put down, or taken away, inside this of anyone.</summary>
        private const float ViewX = 1100f;
        private const float ViewY = 700f;

        private const int CheckEvery = 30;

        /// <summary>Columns in from the world's edge before the search starts: the edge itself is not walkable.</summary>
        private const int EdgeMargin = 50;

        /// <summary>How far up the sand from the water's edge he stands, in tiles, and how much further to look for footing.</summary>
        private const int Inland = 8;
        private const int InlandSearch = 30;

        /// <summary>Rows from the top of the world the shore search starts at. Nothing floats over the sea.</summary>
        private const int SkyTop = 10;

        /// <summary>Columns clear between the one he stands on and his fire's nearest, by request; and how many further up the sand it may go if that will not do.</summary>
        public const int FireGap = 6;
        private const int FireFurther = 3;

        /// <summary>The fire has been laid in this world (it is not laid twice), and where its middle stands, on the ground's top row.</summary>
        public static bool FireLaid;
        public static Point FireAt;

        private static bool justLoaded;

        private static bool searched;
        private static int searchedSide;
        private static Vector2 spot;

        public override void ClearWorld()
        {
            Stage = 0;
            BeachSide = 0;
            FireLaid = false;
            FireAt = Point.Zero;
            justLoaded = false;
            searched = false;
            spot = Vector2.Zero;
        }

        public override void OnWorldLoad()
        {
            justLoaded = true;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Stage != 0)
                tag["PugreamStage"] = Stage;

            if (BeachSide != 0)
                tag["PugreamBeach"] = BeachSide;

            if (FireLaid)
            {
                tag["PugreamFireX"] = FireAt.X;
                tag["PugreamFireY"] = FireAt.Y;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Stage = tag.ContainsKey("PugreamStage") ? tag.GetInt("PugreamStage") : 0;
            BeachSide = tag.ContainsKey("PugreamBeach") ? tag.GetInt("PugreamBeach") : 0;
            FireLaid = tag.ContainsKey("PugreamFireX") && tag.ContainsKey("PugreamFireY");
            FireAt = FireLaid ? new Point(tag.GetInt("PugreamFireX"), tag.GetInt("PugreamFireY")) : Point.Zero;
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write((byte)Stage);
            writer.Write((sbyte)BeachSide);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Stage = reader.ReadByte();
            BeachSide = reader.ReadSByte();
        }

        /// <summary>
        /// The Eye is beaten and he was standing at this x, in world pixels. He goes to
        /// the nearer sea. The caller sends the world data.
        /// </summary>
        public static void SawTheEye(float x)
        {
            if (Stage == 0)
                Stage = 1;

            BeachSide = x < Main.maxTilesX * 8f ? -1 : 1;
            searched = false;
        }

        /// <summary>He belongs on the shore right now.</summary>
        public static bool Owed => Stage == 1 && BeachSide != 0;

        /// <summary>
        /// Where he stands, bottom centre, in world pixels. Zero if there is no shore to
        /// be found on either side. Searched once per world and remembered: the sea does
        /// not move.
        /// </summary>
        public static Vector2 Spot()
        {
            if (searched && searchedSide == BeachSide)
                return spot;

            searched = true;
            searchedSide = BeachSide;
            spot = Shore(BeachSide);

            if (spot == Vector2.Zero)
                spot = Shore(-BeachSide);

            return spot;
        }

        /// <summary>
        /// Walks in from the world's edge on this side, over the water, to the first
        /// column that is dry at the top; then Inland further up the sand to the first
        /// place a man can stand.
        /// </summary>
        private static Vector2 Shore(int side)
        {
            if (side == 0)
                return Vector2.Zero;

            int edge = side < 0 ? EdgeMargin : Main.maxTilesX - 1 - EdgeMargin;
            int inward = -side;
            int bottom = Math.Min(Main.maxTilesY - 20, (int)Main.worldSurface + 60);
            bool water = false;

            for (int i = 0; i < Main.maxTilesX / 3; i++)
            {
                int x = edge + inward * i;

                if (!Surface(x, bottom, out _, out bool wet))
                    continue;

                if (wet)
                {
                    water = true;
                    continue;
                }

                // Land before any sea: this side has no shore to stand on.
                if (!water)
                    return Vector2.Zero;

                for (int j = Inland; j < Inland + InlandSearch; j++)
                {
                    int sx = x + inward * j;

                    if (!Surface(sx, bottom, out int y, out bool damp) || damp)
                        continue;

                    // Not on the waygate's shrine, and not where his fire would be on it.
                    if (OnShrine(sx, y, inward))
                        continue;

                    if (EyeFinale.Standable(sx, y))
                        return new Vector2(sx * 16 + 8, y * 16);
                }

                return Vector2.Zero;
            }

            return Vector2.Zero;
        }

        /// <summary>Standing here, he or his fire (as far up the sand as it may go) would be on the waygate's shrine.</summary>
        private static bool OnShrine(int x, int y, int inward)
        {
            Rectangle shrine = BeachWaygate.Footprint(2);

            if (shrine.IsEmpty)
                return false;

            for (int i = 0; i <= FireGap + 3 + FireFurther; i++)
            {
                if (shrine.Contains(x + inward * i, y - 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// His fire, the first time he is put down here (the notes over this class): a
        /// Campfire up the sand from <paramref name="at"/> (his feet), six columns clear
        /// between them, or as near that as will take it.
        /// </summary>
        private static void LayFire(Vector2 at)
        {
            if (FireLaid)
                return;

            int x = (int)(at.X / 16f);
            int ground = (int)(at.Y / 16f);
            int inward = at.X < Main.maxTilesX * 8f ? 1 : -1;

            for (int further = 0; further <= FireFurther; further++)
            {
                int middle = x + inward * (FireGap + 2 + further);

                if (TryFire(x, ground, middle, inward))
                    return;
            }
        }

        /// <summary>
        /// The fire with its middle column here, if the ground will take it: the ground made
        /// ready and the fire laid. False if it will not, found before anything is touched; and
        /// false if the game will not put the fire down after all, the ground left made ready.
        /// </summary>
        private static bool TryFire(int x, int ground, int middle, int inward)
        {
            int[] top = new int[3];

            for (int i = 0; i < 3; i++)
            {
                if (!GroundNear(middle - 1 + i, ground, out top[i]))
                    return false;
            }

            int floor = Math.Min(top[0], Math.Min(top[1], top[2]));
            int lowest = Math.Max(top[0], Math.Max(top[1], top[2]));

            if (lowest - floor > 2)
                return false;

            Rectangle shrine = BeachWaygate.Footprint(1);

            // Its two rows over the ground: nothing in them but what can be taken up, and no water.
            for (int cx = middle - 1; cx <= middle + 1; cx++)
            {
                for (int cy = floor - 2; cy < floor; cy++)
                {
                    Tile tile = Main.tile[cx, cy];

                    if (tile.LiquidAmount > 0 || (tile.HasTile && !Ambient(tile.TileType)) || shrine.Contains(cx, cy))
                        return false;
                }
            }

            // The ambient objects taken up: where it goes, a column either side of it, and between
            // him and it, whatever stands on each column's ground (four rows of it: the tallest
            // pile is two), each thing whole.
            int from = Math.Min(x + inward, middle - 2);
            int to = Math.Max(x + inward, middle + 2);
            int high = Math.Min(floor, ground);
            int low = Math.Max(lowest, ground);

            for (int cx = from; cx <= to; cx++)
            {
                int on = cx >= middle - 1 && cx <= middle + 1 ? top[cx - (middle - 1)] : GroundNear(cx, ground, out int near) ? near : -1;

                if (on < 0)
                    continue;

                high = Math.Min(high, on);
                low = Math.Max(low, on);

                for (int cy = on - 4; cy < on; cy++)
                {
                    Tile tile = Main.tile[cx, cy];

                    if (tile.HasTile && Ambient(tile.TileType))
                        BeachWaygate.Uproot(cx, cy, tile.TileType);
                }
            }

            high -= 4;

            // The ground under it level: what is lower filled up with what it is made of.
            for (int i = 0; i < 3; i++)
            {
                int cx = middle - 1 + i;
                ushort made = Main.tile[cx, top[i]].TileType;

                for (int cy = floor; cy < top[i]; cy++)
                {
                    Tile fill = Main.tile[cx, cy];

                    fill.ClearTile();
                    fill.HasTile = true;
                    fill.TileType = made;
                }

                Tile surface = Main.tile[cx, floor];

                surface.Slope = SlopeType.Solid;
                surface.IsHalfBlock = false;
            }

            WorldGen.RangeFrame(from - 3, high - 1, to + 3, low + 1);

            if (!WorldGen.PlaceObject(middle, floor - 1, TileID.Campfire, true, 0))
                return false;

            FireLaid = true;
            FireAt = new Point(middle, floor);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, from - 3, high - 1, to - from + 7, low - high + 3);

            return true;
        }

        /// <summary>The top of the ground in this column within a few rows of his feet: something to stand on, dry.</summary>
        private static bool GroundNear(int x, int ground, out int row)
        {
            for (int y = ground - 6; y <= ground + 6; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.LiquidAmount > 0)
                    break;

                if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    row = y;
                    return true;
                }
            }

            row = -1;
            return false;
        }

        /// <summary>The game's ambient objects, and what grows on a beach by itself and a blade cuts: taken up for his fire. Not trees.</summary>
        private static bool Ambient(int type)
        {
            if (type == TileID.Trees || type == TileID.PalmTree || type == TileID.Pots)
                return false;

            return Main.tileCut[type] || type == TileID.SmallPiles || type == TileID.LargePiles || type == TileID.LargePiles2
                || type == TileID.BeachPiles || type == TileID.SeaOats;
        }

        /// <summary>The first thing down this column from the sky: water, or something to stand on.</summary>
        private static bool Surface(int x, int bottom, out int row, out bool wet)
        {
            row = 0;
            wet = false;

            if (x < 1 || x >= Main.maxTilesX - 1)
                return false;

            for (int y = SkyTop; y < bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.LiquidAmount > 0)
                {
                    row = y;
                    wet = true;
                    return true;
                }

                if (EyeFinale.Floor(x, y))
                {
                    row = y;
                    return true;
                }
            }

            return false;
        }

        /// <summary>One line, for the test command.</summary>
        public static string State()
        {
            Vector2 at = Owed ? Spot() : Vector2.Zero;

            return "stage " + Stage
                + ", sea " + (BeachSide < 0 ? "left" : BeachSide > 0 ? "right" : "none")
                + ", eye " + (NPC.downedBoss1 ? "beaten" : "not beaten")
                + ", spot " + (at == Vector2.Zero ? "none" : (int)(at.X / 16f) + ", " + (int)(at.Y / 16f))
                + ", fire " + (FireLaid ? FireAt.X + ", " + FireAt.Y : "not laid");
        }

        private static bool Watched(Vector2 where)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead && Math.Abs(player.Center.X - where.X) <= ViewX && Math.Abs(player.Center.Y - where.Y) <= ViewY)
                    return true;
            }

            return false;
        }

        /// <summary>Takes away a Pugream who is not owed where he stands, but only while nobody can see it happen.</summary>
        private static void LetHimGo(int type, Vector2 at)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type != type)
                    continue;

                if (at != Vector2.Zero && Vector2.Distance(npc.Center, at) <= NearRange)
                    continue;

                if (Watched(npc.Center))
                    continue;

                npc.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }
        }

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // His shore is the world's. One of the world's other places (a subworld) has the
            // world's story and no sea: he is not looked for there.
            if (Subworlds.Subworld.Active)
                return;

            if (Main.GameUpdateCount % CheckEvery != 0)
                return;

            bool first = justLoaded;
            justLoaded = false;

            int type = ModContent.NPCType<PugreamStanding>();

            // Not while the Eye's scene is on: he is in it, somewhere else.
            Vector2 at = Owed && !EyeFinale.SceneRunning ? Spot() : Vector2.Zero;

            LetHimGo(type, at);

            if (at == Vector2.Zero || NPC.AnyNPCs(type))
                return;

            bool near = false;

            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead && Vector2.Distance(player.Center, at) <= NearRange)
                    near = true;
            }

            if (!near)
                return;

            if (Watched(at) && !first)
                return;

            int index = NPC.NewNPC(new EntitySource_WorldEvent(), (int)at.X, (int)at.Y, type);

            // NewNPC sends nothing; a man standing still is never streamed. Say so.
            if (index >= 0 && index < Main.maxNPCs)
                Main.npc[index].netUpdate = true;

            // His fire, as unseen as he is, the first time.
            LayFire(at);
        }
    }
}
