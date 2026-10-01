using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// What the Guide says besides vanilla's lines. Everything else about him, his help
    /// button and his happiness report included, is vanilla's own.
    ///
    /// The first time anybody talks to him after the orb is broken, he says what he felt
    /// (PADWorld.GuideSpokeOfLegends); after that it is one of his lines. Otherwise, one
    /// chat in <see cref="PilgrimChance"/> is the man with the bow, and one in
    /// <see cref="HintChance"/> of the rest is a hint: how incantations and spells are kept
    /// and cast, how they are written, in a world that has one, the kingdom (east or west,
    /// across the world from the sea), and, before the orb is broken, the hut on the trail
    /// toward the sea.
    /// The lines are under Mods.PisAndDag.Hints.
    /// </summary>
    public class GuideHints : GlobalNPC
    {
        /// <summary>One chat in this many is the Pilgrim line.</summary>
        public const int PilgrimChance = 40;

        /// <summary>One chat in this many, of the ones left, is a hint.</summary>
        public const int HintChance = 3;

        private const string Key = "Mods.PisAndDag.Hints.";

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Guide;

        public override void GetChat(NPC npc, ref string chat)
        {
            if (PADWorld.Legends && !PADWorld.GuideSpokeOfLegends)
            {
                Spoke();
                chat = Language.GetTextValue(Key + "GuideLegends");
                return;
            }

            if (Main.rand.NextBool(PilgrimChance))
            {
                chat = Language.GetTextValue(Key + "GuidePilgrim");
                return;
            }

            if (!Main.rand.NextBool(HintChance))
                return;

            var lines = new List<string> { "GuideArsenal", "GuideAltar" };

            // Only where there is a kingdom to find: Grace is built in Grand worlds alone.
            if (Structures.Grace.GraceWorld.Area.Width > 0)
                lines.Add("GuideKingdom");

            if (PADWorld.Legends)
                lines.Add("GuideLegends");
            else
                lines.Add("GuideHut");

            string line = lines[Main.rand.Next(lines.Count)];

            // The kingdom is at the far end from the sea, whichever end that is (WorldMirror).
            chat = line == "GuideKingdom"
                ? Language.GetTextValue(Key + line, GuideIntro.SeaEast ? "west" : "east")
                : Language.GetTextValue(Key + line);
        }

        /// <summary>He has said it: here, and on the server, which tells everyone else.</summary>
        private void Spoke()
        {
            PADWorld.GuideSpokeOfLegends = true;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = Mod.GetPacket();
                packet.Write((byte)PADPacket.GuideSpokeOfLegends);
                packet.Send();
            }
        }

        /// <summary>On the server: a player has heard the line. It is not said first again.</summary>
        internal static void Heard()
        {
            if (Main.netMode != NetmodeID.Server || PADWorld.GuideSpokeOfLegends)
                return;

            PADWorld.GuideSpokeOfLegends = true;
            NetMessage.SendData(MessageID.WorldData);
        }
    }
}
