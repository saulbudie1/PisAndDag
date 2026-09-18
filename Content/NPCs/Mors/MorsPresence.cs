using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.LoreItem;
using PisAndDag.Content.Structures;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mors
{
    /// <summary>
    /// Where Mors is, and when.
    ///
    /// He is not summoned and he does not wander in. Once the world has buried you
    /// (GraveyardZone: the first death fills the open grave) he is at the graveyard,
    /// standing beside the plot, and he stays there for as long as the story keeps
    /// him there. He is an ordinary NPC underneath, so he despawns when everyone is
    /// far away like anything else; this puts him back the moment someone comes
    /// near again, before they can see the spot, so to the player he was never gone.
    ///
    /// Stage is the milestone counter, saved with the world.
    ///
    ///   0  the graveyard, from the first death
    ///   1  the Mors Monument, from the Wall of Flesh. Not while you are looking at
    ///      the spot: take your eyes off it and he is there the next time you come
    ///      near. Certainly there. This one is not a chance meeting
    ///   2  nowhere, until somebody is carrying the pouch he asked for; then the
    ///      graveyard again, which is not where he said he would be, because he did
    ///      not say and could not have
    ///
    /// He does not know why he goes down to the hill (§XVI — it is where he set the
    /// blade down and forgot that it was holding him up). He is there once, he says
    /// the one thing he came to say, and then he is not there again.
    ///
    /// Stage 2 begins the moment the hill scene is answered (MorsTalk). From then he
    /// stands there only while he is on somebody's screen, and the first check after
    /// he is off it he is gone.
    /// </summary>
    public class MorsPresence : ModSystem
    {
        /// <summary>Which chapter of him the world is in. 0: at the graveyard. 1: at the monument.</summary>
        public static int Stage;

        /// <summary>
        /// Whether the graveyard line has been said in this world. One world, one first
        /// meeting: it belongs to the ground, not to whoever happened to be standing on it.
        /// </summary>
        public static bool SaidGraveyard;

        /// <summary>The same, for the first time he is spoken to on the ash hill.</summary>
        public static bool SaidMonument;

        /// <summary>Someone this close to his spot, in pixels, keeps him there.</summary>
        private const float NearRange = 2600f;

        /// <summary>
        /// Half a screen and then some. He is never put down inside this of a player
        /// and never taken away inside it either: both would be seen happening.
        /// </summary>
        private const float ViewX = 1100f;
        private const float ViewY = 700f;

        /// <summary>He stands this many tiles right of the last grave.</summary>
        private const int BesidePlot = 4;

        /// <summary>And this many right of where the blade was driven in, if the trees cannot be found.</summary>
        private const int BesideBlade = 5;

        /// <summary>Columns either side of the blade searched for ash trunks. The grove is well inside this.</summary>
        private const int TreeSearch = 120;

        /// <summary>Counting outward from the middle, the trunk he stands at, and how far off it he stands.</summary>
        private const int TreeFromMiddle = 2;
        private const int BesideTree = 2;

        private const int CheckEvery = 30;

        /// <summary>Set on world load so the first check ignores the view test: nobody has seen anything yet.</summary>
        private static bool justLoaded;

        public override void ClearWorld()
        {
            Stage = 0;
            SaidGraveyard = false;
            SaidMonument = false;
            justLoaded = false;
        }

        public override void OnWorldLoad()
        {
            justLoaded = true;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Stage != 0)
                tag["MorsStage"] = Stage;

            if (SaidGraveyard)
                tag["MorsSaidGraveyard"] = true;

            if (SaidMonument)
                tag["MorsSaidMonument"] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Stage = tag.ContainsKey("MorsStage") ? tag.GetInt("MorsStage") : 0;
            SaidGraveyard = tag.ContainsKey("MorsSaidGraveyard");
            SaidMonument = tag.ContainsKey("MorsSaidMonument");
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Stage);
            writer.Write(SaidGraveyard);
            writer.Write(SaidMonument);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Stage = reader.ReadInt32();
            SaidGraveyard = reader.ReadBoolean();
            SaidMonument = reader.ReadBoolean();
        }

        /// <summary>
        /// He belongs at the graveyard right now: at the start, and again once the
        /// pouch is found. Where else would he be. It is the only place he has ever
        /// been told to wait.
        /// </summary>
        public static bool AtGraveyard =>
            GraveyardZone.Exists && GraveyardZone.HasGraves
            && ((Stage == 0 && GraveyardZone.Deaths >= 1) || (Stage >= 2 && Carried()));

        /// <summary>He belongs at the monument right now.</summary>
        public static bool AtMonument => Stage == 1 && Main.hardMode;

        /// <summary>Somebody in the world has the pouch on them.</summary>
        private static bool Carried()
        {
            int pouch = ModContent.ItemType<PouchOfBerries>();

            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead && player.HasItem(pouch))
                    return true;
            }

            return false;
        }

        /// <summary>Where he stands, bottom centre, in world pixels. Zero if the ground cannot be found.</summary>
        public static Vector2 GraveyardSpot()
        {
            int x = GraveyardZone.GraveRight + BesidePlot;

            if (x < 1 || x >= Main.maxTilesX - 1)
                return Vector2.Zero;

            // The crown row is the graveyard's surface; look a little above it in case
            // the ground rises beside the plot, then down for the first thing to stand on.
            for (int y = GraveyardZone.CrownY - 8; y < GraveyardZone.CrownY + 24; y++)
            {
                if (y < 1 || y >= Main.maxTilesY - 1)
                    continue;

                if (WorldGen.SolidTile(x, y) && !WorldGen.SolidTile(x, y - 1) && !WorldGen.SolidTile(x, y - 2))
                    return new Vector2(x * 16 + 8, y * 16);
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// Where he stands on the ash hill: in among the trees, with one at his back.
        ///
        /// The monument writes no coordinates into the world file, so the trees are what
        /// finds it. They are the only ash trees anywhere near the blade's column and
        /// they mark the plateau exactly; the flat tabletop alone could be confused with
        /// any ledge in hell if the world spawn has shifted since generation.
        ///
        /// Which tree is not a choice: the third trunk out from the middle, counted the
        /// same way every time, so he is always in the same place and always inside the
        /// grove rather than out on the bare ash.
        /// </summary>
        public static Vector2 MonumentSpot()
        {
            int centre = MorsMonumentPass.CenterX;
            int roof = (int)Main.UnderworldLayer;
            int floor = Main.maxTilesY - 6;

            if (centre < TreeSearch + 2 || centre >= Main.maxTilesX - TreeSearch - 2)
                return Vector2.Zero;

            // Every ash trunk standing on the hill, nearest the middle first.
            var trunks = new List<int>();

            for (int x = centre - TreeSearch; x <= centre + TreeSearch; x++)
            {
                if (Math.Abs(x - centre) < 3)
                    continue;

                for (int y = roof; y < floor; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile || tile.TileType != TileID.TreeAsh)
                        continue;

                    trunks.Add(x);
                    break;
                }
            }

            trunks.Sort((a, b) => Math.Abs(a - centre).CompareTo(Math.Abs(b - centre)));

            int stand;

            if (trunks.Count > 0)
            {
                int tree = trunks[Math.Min(TreeFromMiddle, trunks.Count - 1)];

                // At its foot, on the outward side, so the trunk is behind him and the
                // rest of the grove is between him and the open ash.
                stand = tree + (tree < centre ? -BesideTree : BesideTree);
            }
            else
            {
                // No trees found: the tabletop beside the blade will do.
                stand = centre + BesideBlade;
            }

            return GroundAt(stand, roof, floor);
        }

        /// <summary>
        /// The top of the mound in this column, in world pixels.
        ///
        /// Searched upward from the world's floor, because the mound is solid ash all
        /// the way down and the first gap above that solid run is its surface. Looking
        /// downward instead would stop at the cave roof above the plateau.
        /// </summary>
        private static Vector2 GroundAt(int x, int roof, int floor)
        {
            if (x < 1 || x >= Main.maxTilesX - 1)
                return Vector2.Zero;

            for (int y = floor; y > roof; y--)
            {
                if (y < 3)
                    break;

                if (!WorldGen.SolidTile(x, y) && WorldGen.SolidTile(x, y + 1)
                    && !WorldGen.SolidTile(x, y - 1) && !WorldGen.SolidTile(x, y - 2))
                    return new Vector2(x * 16 + 8, (y + 1) * 16);
            }

            return Vector2.Zero;
        }

        /// <summary>One line on where he is owed and why, for the test command.</summary>
        public static string State()
        {
            Vector2 spot = AtGraveyard ? GraveyardSpot() : AtMonument ? MonumentSpot() : Vector2.Zero;

            return "stage " + Stage
                + (Main.hardMode ? ", hardmode" : ", pre-hardmode")
                + ", owed " + (AtGraveyard ? "graveyard" : AtMonument ? "monument" : "nowhere")
                + ", spot " + (spot == Vector2.Zero ? "not found" : (int)(spot.X / 16f) + ", " + (int)(spot.Y / 16f))
                + ", graveyard " + (GraveyardZone.Exists ? GraveyardZone.HasGraves ? "with graves" : "no graves" : "none")
                + ", deaths " + GraveyardZone.Deaths;
        }

        /// <summary>Is this point inside anybody's view.</summary>
        private static bool Watched(Vector2 where)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead)
                    continue;

                if (Math.Abs(player.Center.X - where.X) <= ViewX && Math.Abs(player.Center.Y - where.Y) <= ViewY)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The world tearing open is what moves him off the graveyard. Nothing announces
        /// it and nothing marks the spot he leaves.
        /// </summary>
        private static void Advance()
        {
            if (!Main.hardMode || Stage != 0)
                return;

            Stage = 1;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>
        /// Takes away a Mors standing somewhere he is no longer owed, but only while
        /// nobody can see it happen. A zero spot means he is owed nowhere at all, and
        /// then every one of him goes.
        ///
        /// This is what makes walking away from the hill final: the moment the scene
        /// is answered he belongs nowhere, and the first time the player is not
        /// looking he is not there.
        /// </summary>
        private static void LetHimGo(int type, Vector2 spot)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type != type)
                    continue;

                if (spot != Vector2.Zero && Vector2.Distance(npc.Center, spot) <= NearRange)
                    continue;

                // Never while he is on somebody's screen. Off it, he is simply not there.
                if (Watched(npc.Center))
                    continue;

                npc.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }
        }

        public override void PostUpdateNPCs()
        {
            // The server puts him there; clients receive him.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Main.GameUpdateCount % CheckEvery != 0)
                return;

            bool first = justLoaded;
            justLoaded = false;

            Advance();

            Vector2 spot = Vector2.Zero;

            if (AtGraveyard)
                spot = GraveyardSpot();
            else if (AtMonument)
                spot = MonumentSpot();

            int type = ModContent.NPCType<MorsIdle>();

            LetHimGo(type, spot);

            if (spot == Vector2.Zero)
                return;

            if (NPC.AnyNPCs(type))
                return;

            bool near = false;

            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead && Vector2.Distance(player.Center, spot) <= NearRange)
                    near = true;
            }

            if (!near)
                return;

            // Not in front of anybody. On a world load nobody has seen anything yet,
            // so he may already be standing there when the screen arrives.
            if (Watched(spot) && !first)
                return;

            NPC.NewNPC(new EntitySource_WorldEvent(), (int)spot.X, (int)spot.Y, type);
        }
    }
}
