using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for the Guide's quest (the little hut and his ledger), which opens the off hand and
    /// lets the Voodoo Demons come.
    ///
    ///   /guidequest         where it stands in this world
    ///   /guidequest done    done, as if the ledger were read: the off hand opens, the demons come
    ///   /guidequest reset   owed again
    ///
    /// A World command: run where the world is decided, the server in multiplayer.
    /// </summary>
    public class GuideQuestCommand : ModCommand
    {
        private static readonly Color Done = new Color(255, 196, 92);

        public override CommandType Type => CommandType.World;

        public override string Command => "guidequest";

        public override string Usage => "/guidequest [done|reset]";

        public override string Description => "The Guide's quest: show where it stands in this world, finish it, or make it owed again.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            switch (args.Length > 0 ? args[0].ToLowerInvariant() : "")
            {
                case "done":
                    GuideQuest.Done = true;

                    if (Terraria.Main.netMode == Terraria.ID.NetmodeID.Server)
                        Terraria.NetMessage.SendData(Terraria.ID.MessageID.WorldData);

                    caller.Reply("The Guide's quest is done: the off hand is open and the Voodoo Demons come.", Done);
                    return;

                case "reset":
                    GuideQuest.Reset();
                    caller.Reply("The Guide's quest is owed again: the off hand is shut and the Voodoo Demons stay away.", Done);
                    return;

                case "":
                    caller.Reply(GuideQuest.Describe() + " Usage: " + Usage);
                    return;

                default:
                    caller.Reply("Usage: " + Usage, Color.OrangeRed);
                    return;
            }
        }
    }
}
