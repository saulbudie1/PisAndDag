using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.MegaShark;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Test spawner: "/megashark" drops the boss on the cursor.
    ///
    /// Stands in until there is a summon item. The real fight is meant to be gated behind
    /// Queen Slime, so this is a development convenience and not the intended entry point.
    /// </summary>
    public class MegasharkCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "megashark";

        public override string Usage => "/megashark";

        public override string Description => "Summons the Megashark at the cursor, for testing.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            int type = ModContent.NPCType<Megashark>();

            if (NPC.AnyNPCs(type))
            {
                caller.Reply("Megashark is already here.", Color.OrangeRed);
                return;
            }

            Vector2 where = Main.MouseWorld;

            // Spawn on the server in multiplayer so the NPC exists for everyone; the boss
            // sinks itself below this point on its first tick to stage the entrance.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            // EntitySource_DebugCommand takes a required context string — it has no
            // parameterless constructor.
            NPC.NewNPC(new Terraria.DataStructures.EntitySource_DebugCommand("megashark"),
                (int)where.X, (int)where.Y, type);

            caller.Reply("Something surfaces.", new Color(224, 18, 218));
        }
    }
}
