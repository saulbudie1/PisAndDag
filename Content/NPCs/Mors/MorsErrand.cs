using System.IO;
using PisAndDag.Content.Items.LoreItem;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mors
{
    /// <summary>
    /// The errand.
    ///
    /// On the ash hill he asks the player to look for a pouch he set down somewhere
    /// above. It is out there: one skeleton in the caves has been carrying it since
    /// whenever, because nothing in this era finishes and that includes the man it
    /// was packed for and the man carrying it (§XIV).
    ///
    /// Bring it back and he does not remember asking. That is the whole of it. There
    /// is no reward, he will not take the pouch, and nothing in the world marks that
    /// it happened. The player is left holding a full pouch of berries that have not
    /// gone off in nine hundred years, which is the piece they were actually given.
    ///
    /// Three flags, all world-scoped: the world is what is carrying this, not the
    /// character who happened to be standing there.
    /// </summary>
    public class MorsErrand : ModSystem
    {
        /// <summary>The asking happened, whichever way it was answered.</summary>
        public static bool Asked;

        /// <summary>The player said yes. The errand proper; declining leaves the pouch out there anyway.</summary>
        public static bool Accepted;

        /// <summary>The pouch has been put into this world. Once, ever.</summary>
        public static bool Dropped;

        /// <summary>It has been shown to him, and he has said what he says.</summary>
        public static bool Returned;

        public override void ClearWorld()
        {
            Asked = false;
            Accepted = false;
            Dropped = false;
            Returned = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Asked)
                tag["MorsAsked"] = true;

            if (Accepted)
                tag["MorsAccepted"] = true;

            if (Dropped)
                tag["MorsPouchDropped"] = true;

            if (Returned)
                tag["MorsPouchReturned"] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Asked = tag.ContainsKey("MorsAsked");
            Accepted = tag.ContainsKey("MorsAccepted");
            Dropped = tag.ContainsKey("MorsPouchDropped");
            Returned = tag.ContainsKey("MorsPouchReturned");
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Asked);
            writer.Write(Accepted);
            writer.Write(Dropped);
            writer.Write(Returned);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Asked = reader.ReadBoolean();
            Accepted = reader.ReadBoolean();
            Dropped = reader.ReadBoolean();
            Returned = reader.ReadBoolean();
        }
    }

    /// <summary>
    /// Puts the pouch on a skeleton, once per world.
    ///
    /// Gated on MorsPresence.Stage rather than on his having asked, because the stage
    /// is set by the side that runs the world and a line of dialogue is not: in
    /// multiplayer a client hearing him ask would never reach the server, and the
    /// pouch would never drop. It also reads better this way round. The player can
    /// find it before he mentions it, and hand it over the moment he does.
    /// </summary>
    public class MorsErrandDrop : GlobalNPC
    {
        /// <summary>One skeleton in this many is the one that has been carrying it.</summary>
        private const int Odds = 20;

        public override void OnKill(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (MorsErrand.Dropped || MorsPresence.Stage < 1)
                return;

            if (npc.type < 0 || npc.type >= NPCID.Sets.Skeletons.Length || !NPCID.Sets.Skeletons[npc.type])
                return;

            if (!Main.rand.NextBool(Odds))
                return;

            MorsErrand.Dropped = true;

            Item.NewItem(npc.GetSource_Loot(), (int)npc.position.X, (int)npc.position.Y,
                npc.width, npc.height, ModContent.ItemType<PouchOfBerries>());

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }
    }
}
