using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Abomanation_Small;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Test spawner: "/abomanation" calls the worm the way the offering does, minus the
    /// offering. It comes from a long way off, through the ground, with the camera on it;
    /// the cursor plays no part. See AbomanationHead.Summon.
    ///
    /// Stands in for testing, same as /megashark.
    /// </summary>
    public class AbomanationCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "abomanation";

        public override string Usage => "/abomanation";

        public override string Description => "Calls the Abomanation, for testing.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (NPC.AnyNPCs(ModContent.NPCType<AbomanationHead>()))
            {
                caller.Reply("It is already here.", Color.OrangeRed);
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            int made = AbomanationHead.Summon(caller.Player, new EntitySource_DebugCommand("abomanation"));

            if (made < 0)
            {
                caller.Reply("No room for it.", Color.OrangeRed);
                return;
            }

            caller.Reply("The ground moves, a long way off.", new Color(190, 40, 40));
        }
    }
}
