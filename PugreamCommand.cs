using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Pugream;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Test command for Pugream and the Eye.
    ///
    ///   /pugream          stands him at the cursor, as he is on the shore
    ///   /pugream eye      puts the world back to before the Eye's first defeat, so the
    ///                     next one plays the scene; a live Eye is left one hit from it
    ///   /pugream state    where he is owed, and why
    ///
    /// Host only: the world is the server's.
    /// </summary>
    public class PugreamCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "pugream";

        public override string Usage => "/pugream [eye | state]";

        public override string Description => "Stands Pugream at the cursor, or resets the Eye's first defeat, for testing.";

        private static readonly Color Road = new Color(196, 180, 152);

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (what == "state")
            {
                caller.Reply(PugreamPresence.State(), Color.Gray);
                return;
            }

            if (what == "eye")
            {
                ForgetTheEye(caller);
                return;
            }

            Vector2 where = Main.MouseWorld;

            NPC.NewNPC(new Terraria.DataStructures.EntitySource_DebugCommand("pugream"),
                (int)where.X, (int)where.Y, ModContent.NPCType<PugreamStanding>());

            caller.Reply("Pugream.", Road);
            caller.Reply(PugreamPresence.State(), Color.Gray);
        }

        private static void ForgetTheEye(CommandCaller caller)
        {
            NPC.downedBoss1 = false;
            PugreamPresence.Stage = 0;
            PugreamPresence.BeachSide = 0;

            int standing = ModContent.NPCType<PugreamStanding>();
            bool eye = false;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == standing)
                {
                    npc.active = false;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
                else if (npc.type == NPCID.EyeofCthulhu && npc.life > EyeFinaleEye.Hold + 1)
                {
                    npc.life = EyeFinaleEye.Hold + 1;
                    npc.netUpdate = true;
                    eye = true;
                }
            }

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);

            caller.Reply(eye
                ? "The Eye has never been beaten here. The one up is a hit away from it."
                : "The Eye has never been beaten here. Summon one and bring it down.", Color.Gray);
        }
    }
}
