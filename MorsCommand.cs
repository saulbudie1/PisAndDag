using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Mors;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Test spawner: "/mors" stands him at the cursor; he falls to whatever is under
    /// it. "/mors again" also forgets that this world has heard his graveyard line,
    /// so it can be seen a second time. "/mors 1" puts the world back to before the
    /// first death: no deaths counted, nothing said, no stage, and he is removed so
    /// MorsPresence decides afresh. The filled grave stays filled; tiles are not undone.
    /// </summary>
    public class MorsCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "mors";

        public override string Usage => "/mors [again | 1]";

        public override string Description => "Stands Mors at the cursor, for testing.";

        private static void SquareOne()
        {
            Content.Structures.GraveyardZone.Deaths = 0;
            MorsPresence.SaidGraveyard = false;
            MorsPresence.Stage = 0;
            Content.UI.TalkMemory.Heard.Clear();

            int type = ModContent.NPCType<MorsIdle>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type != type)
                    continue;

                npc.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            if (args.Length > 0 && args[0] == "again")
                MorsPresence.SaidGraveyard = false;

            if (args.Length > 0 && args[0] == "1")
            {
                SquareOne();
                caller.Reply("Square one.", Color.Gray);
                return;
            }

            Vector2 where = Main.MouseWorld;

            NPC.NewNPC(new Terraria.DataStructures.EntitySource_DebugCommand("mors"),
                (int)where.X, (int)where.Y, ModContent.NPCType<MorsIdle>());

            caller.Reply("He was already there.", new Color(196, 180, 152));

            // For testing the graveyard line: is this the world's graveyard at all, and
            // are you in it?
            caller.Reply(
                "Graveyard: " + (Content.Structures.GraveyardZone.Exists ? "exists" : "none in this world")
                + ", you are " + (caller.Player.GetModPlayer<Content.Structures.GraveyardPlayer>().Inside ? "inside" : "outside")
                + ", deaths " + Content.Structures.GraveyardZone.Deaths + ".",
                Color.Gray);
        }
    }
}
