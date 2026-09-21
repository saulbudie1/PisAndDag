using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Abomanation_Small;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Test spawner: "/abomanation" puts the worm at the cursor.
    ///
    /// Stands in until there is a summon item, same as /megashark. The head builds its own
    /// body on its first tick, so one NPC is all this has to place.
    /// </summary>
    public class AbomanationCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "abomanation";

        public override string Usage => "/abomanation";

        public override string Description => "Summons the Abomanation at the cursor, for testing.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            int type = ModContent.NPCType<AbomanationHead>();

            if (NPC.AnyNPCs(type))
            {
                caller.Reply("It is already here.", Color.OrangeRed);
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            Vector2 where = Main.MouseWorld;

            NPC.NewNPC(new Terraria.DataStructures.EntitySource_DebugCommand("abomanation"),
                (int)where.X, (int)where.Y, type);

            caller.Reply("The sand moves.", new Color(190, 40, 40));
        }
    }
}
