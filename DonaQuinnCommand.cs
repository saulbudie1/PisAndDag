using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.DonaQuinn;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for Dona Quinn ('Dame' Quilt in game), who comes once a world and only on the Cleared Ground.
    ///
    ///   /dona          where she stands in this world
    ///   /dona summon   her road, now, for whoever typed it, wherever they are (night or rain
    ///                  still runs on to the morning first)
    ///   /dona reset    back to never met: any road ended, her and Triunfo gone, her name unheard
    ///
    /// A World command: run where the world is decided, the server in multiplayer, so it works
    /// from any player's chat. Her name heard is each machine's own in multiplayer, and a reset
    /// there forgets it only on the server's side.
    /// </summary>
    public class DonaQuinnCommand : ModCommand
    {
        private static readonly Color Done = new Color(255, 196, 92);

        public override CommandType Type => CommandType.World;

        public override string Command => "dona";

        public override string Usage => "/dona [summon|reset]";

        public override string Description => "'Dame' Quilt: show where she stands, summon her here, or reset her to never met.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "summon":
                {
                    string refused = DonaQuinnEncounter.Summon(caller.Player);

                    if (refused != null)
                    {
                        caller.Reply(refused, Color.OrangeRed);
                        return;
                    }

                    caller.Reply(DonaQuinnEncounter.Skipping
                        ? "The world runs on to eight in the morning; then she comes."
                        : "Someone is coming down the road.", Done);
                    return;
                }

                case "reset":
                    DonaQuinnEncounter.Reset();
                    caller.Reply("'Dame' Quilt reset: never met, her name unheard.", Done);
                    return;

                case "":
                    caller.Reply(DonaQuinnEncounter.Describe() + " Usage: " + Usage);
                    return;

                default:
                    caller.Reply("Usage: " + Usage, Color.OrangeRed);
                    return;
            }
        }
    }
}
