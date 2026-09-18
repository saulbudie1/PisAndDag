using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Turns the lock on §XI's second crime on and off, for testing.
    ///
    /// "/archmageshimmer true" gives Shimmer back what it does. "/archmageshimmer false"
    /// takes it away again and leaves the liquid lying about everywhere as water. With no
    /// argument it reports which way round the world currently is.
    ///
    /// This is a test command and nothing else. Nothing in the game sets the flag yet, and
    /// §XI deliberately leaves open whether the lock can be opened at all.
    /// </summary>
    public class ArchmageShimmerCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "archmageshimmer";

        public override string Usage => "/archmageshimmer [true | false]";

        public override string Description => "Whether Shimmer does anything, for testing.";

        private static readonly Color Working = new Color(196, 168, 232);
        private static readonly Color Water = new Color(150, 150, 150);

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            // The flag is world state. A client setting its own copy would be telling
            // itself a story the server does not agree with.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            if (args.Length == 0)
            {
                Report(caller);
                return;
            }

            string word = args[0].ToLowerInvariant();

            bool want = word switch
            {
                "true" or "on" or "1" => true,
                "false" or "off" or "0" => false,
                _ => PADWorld.ArchmageShimmer,
            };

            if (word is not ("true" or "on" or "1" or "false" or "off" or "0"))
            {
                caller.Reply("Usage: /archmageshimmer [true | false]", Color.OrangeRed);
                return;
            }

            PADWorld.ArchmageShimmer = want;

            // Brings every client's copy in line. Inert in singleplayer.
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);

            Report(caller);
        }

        private static void Report(CommandCaller caller)
        {
            if (PADWorld.ArchmageShimmer)
            {
                caller.Reply("It asks again.", Working);
                return;
            }

            caller.Reply("It is water.", Water);
        }
    }
}
