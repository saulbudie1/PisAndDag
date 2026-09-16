using System.IO;
using Microsoft.Xna.Framework;
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
    /// Stage is the milestone counter, saved with the world. 0 is the graveyard.
    /// Raising it is what moves him on; nothing here does that yet.
    /// </summary>
    public class MorsPresence : ModSystem
    {
        /// <summary>Which chapter of him the world is in. 0: at the graveyard.</summary>
        public static int Stage;

        /// <summary>
        /// Whether the graveyard line has been said in this world. One world, one first
        /// meeting: it belongs to the ground, not to whoever happened to be standing on it.
        /// </summary>
        public static bool SaidGraveyard;

        /// <summary>Someone this close to his spot, in pixels, keeps him there.</summary>
        private const float NearRange = 2600f;

        /// <summary>He is only put back while nobody is close enough to watch it happen.</summary>
        private const float SeenRange = 1100f;

        /// <summary>He stands this many tiles right of the last grave.</summary>
        private const int BesidePlot = 4;

        private const int CheckEvery = 30;

        /// <summary>Set on world load so the first check ignores SeenRange: nobody has seen anything yet.</summary>
        private static bool justLoaded;

        public override void ClearWorld()
        {
            Stage = 0;
            SaidGraveyard = false;
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
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Stage = tag.ContainsKey("MorsStage") ? tag.GetInt("MorsStage") : 0;
            SaidGraveyard = tag.ContainsKey("MorsSaidGraveyard");
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Stage);
            writer.Write(SaidGraveyard);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Stage = reader.ReadInt32();
            SaidGraveyard = reader.ReadBoolean();
        }

        /// <summary>He belongs at the graveyard right now.</summary>
        public static bool AtGraveyard =>
            Stage == 0 && GraveyardZone.Exists && GraveyardZone.HasGraves && GraveyardZone.Deaths >= 1;

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

        public override void PostUpdateNPCs()
        {
            // The server puts him there; clients receive him.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Main.GameUpdateCount % CheckEvery != 0)
                return;

            bool first = justLoaded;
            justLoaded = false;

            if (!AtGraveyard)
                return;

            int type = ModContent.NPCType<MorsIdle>();

            if (NPC.AnyNPCs(type))
                return;

            Vector2 spot = GraveyardSpot();

            if (spot == Vector2.Zero)
                return;

            bool near = false;
            bool seen = false;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead)
                    continue;

                float dist = Vector2.Distance(player.Center, spot);

                if (dist <= NearRange)
                    near = true;

                if (dist <= SeenRange)
                    seen = true;
            }

            if (!near || (seen && !first))
                return;

            NPC.NewNPC(new EntitySource_WorldEvent(), (int)spot.X, (int)spot.Y, type);
        }
    }
}
