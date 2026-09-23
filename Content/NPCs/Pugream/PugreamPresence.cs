using System;
using System.IO;
using Microsoft.Xna.Framework;
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

        private static bool justLoaded;

        private static bool searched;
        private static int searchedSide;
        private static Vector2 spot;

        public override void ClearWorld()
        {
            Stage = 0;
            BeachSide = 0;
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
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Stage = tag.ContainsKey("PugreamStage") ? tag.GetInt("PugreamStage") : 0;
            BeachSide = tag.ContainsKey("PugreamBeach") ? tag.GetInt("PugreamBeach") : 0;
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

                    if (EyeFinale.Standable(sx, y))
                        return new Vector2(sx * 16 + 8, y * 16);
                }

                return Vector2.Zero;
            }

            return Vector2.Zero;
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
                + ", spot " + (at == Vector2.Zero ? "none" : (int)(at.X / 16f) + ", " + (int)(at.Y / 16f));
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
        }
    }
}
