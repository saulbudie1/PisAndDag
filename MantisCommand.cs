using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Mantis;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for the Iron Borne Mantis, which nothing in the world calls yet.
    ///
    ///   /mantis          it comes, twenty tiles off the way whoever typed it faces
    ///   /mantis clear    any of it about is gone
    ///
    /// A World command: run where the world is decided, the server in multiplayer.
    /// </summary>
    public class MantisCommand : ModCommand
    {
        public override CommandType Type => CommandType.World;

        public override string Command => "mantis";

        public override string Usage => "/mantis [clear]";

        public override string Description => "The Iron Borne Mantis: call it here, or clear it away.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            int type = ModContent.NPCType<IronBorneMantis>();

            if (args.Length > 0 && args[0].ToLowerInvariant() == "clear")
            {
                int gone = 0;

                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (npc.type != type)
                        continue;

                    npc.active = false;
                    gone++;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }

                caller.Reply(gone > 0 ? "The Mantis is cleared away." : "There is no Mantis about.", Color.LightGray);
                return;
            }

            Player player = caller.Player;
            Vector2 at = player.Bottom + new Vector2(player.direction * 320f, -8f);
            int who = NPC.NewNPC(player.GetSource_Misc("MantisCommand"), (int)at.X, (int)at.Y, type, 0, 0f, 0f, 0f, 0f, player.whoAmI);

            if (who < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, who);

            caller.Reply("The Iron Borne Mantis.", new Color(255, 96, 110));
        }
    }
}
