using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for the Guide's first words, which a world has once, the first time
    /// anybody walks in it after it is made.
    ///
    ///   /guideintro         where they stand in this world
    ///   /guideintro reset   owed again: ten seconds after somebody next walks, from the start
    ///
    /// A World command: run where the world is decided, the server in multiplayer, so it works
    /// from any player's chat.
    /// </summary>
    public class GuideIntroCommand : ModCommand
    {
        private static readonly Color Done = new Color(255, 196, 92);

        public override CommandType Type => CommandType.World;

        public override string Command => "guideintro";

        public override string Usage => "/guideintro [reset]";

        public override string Description => "The Guide's first words: show where they stand in this world, or make them owed again.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "reset":
                    GuideIntro.Again();
                    caller.Reply("The Guide's first words are owed again: walk, and ten seconds later he comes.", Done);
                    return;

                case "":
                    caller.Reply(GuideIntro.Describe() + " Usage: " + Usage);
                    return;

                default:
                    caller.Reply("Usage: " + Usage, Color.OrangeRed);
                    return;
            }
        }
    }
}
