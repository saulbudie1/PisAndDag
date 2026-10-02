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
    /// With a word after it, and the worm already here, it makes the worm do that one
    /// thing next: rush (over you), lunge (through you), puke, ash, vent, spasm, whip. So
    /// each can be looked at without waiting for it to be rolled.
    ///
    /// Stands in for testing, same as /megashark.
    /// </summary>
    public class AbomanationCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "abomanation";

        public override string Usage => "/abomanation [rush|lunge|puke|ash|vent|spasm|whip]";

        public override string Description => "Calls the Abomanation, for testing; or, once it is here, makes it do one move next.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            if (args.Length > 0)
            {
                Force(caller, args[0].ToLowerInvariant());
                return;
            }

            if (NPC.AnyNPCs(ModContent.NPCType<AbomanationHead>()))
            {
                caller.Reply("It is already here.", Color.OrangeRed);
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

        /// <summary>Tells the worm that is here to do one thing next.</summary>
        private static void Force(CommandCaller caller, string what)
        {
            int head = NPC.FindFirstNPC(ModContent.NPCType<AbomanationHead>());

            if (head < 0 || !(Main.npc[head].ModNPC is AbomanationHead boss))
            {
                caller.Reply("It is not here. /abomanation on its own calls it.", Color.OrangeRed);
                return;
            }

            if (boss.Force(what))
                caller.Reply("Next: " + what + ".", new Color(190, 40, 40));
            else
                caller.Reply("It does not know that one. rush, lunge, puke, ash, vent, spasm, whip.", Color.OrangeRed);
        }
    }
}
