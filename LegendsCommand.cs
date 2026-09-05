using Terraria;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for the Legends flag and its screen effect.
    ///
    ///   /legends          report the current flag
    ///   /legends on|off   set it, so an orb can be placed and broken again
    ///   /legends fx       fire the stir effect on its own, without a tile
    ///
    /// The flag is permanent by design, which makes it awkward to test: once it is set,
    /// orbs cannot be placed and breaking one does nothing. This is the way back.
    /// </summary>
    public class LegendsCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "legends";

        public override string Usage => "/legends [on|off|fx]";

        public override string Description => "Show or set the Legends world flag, or test its effect.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "on":
                    case "true":
                        PADWorld.Legends = true;
                        break;

                    case "off":
                    case "false":
                        PADWorld.Legends = false;
                        break;

                    case "fx":
                        // Runs the shake and the filter without touching a tile, so the
                        // shader can be tuned without replacing and rebreaking an orb.
                        Main.LocalPlayer.PIS().AddScreenShake(12f, 180);
                        LegendsScreenSystem.Trigger(180);

                        caller.Reply(LegendsScreenSystem.Registered
                            ? "Fired the stir effect."
                            : "Shake fired, but the screen filter is NOT registered — "
                              + "Effects/LegendsShader.xnb is missing.");
                        return;

                    default:
                        caller.Reply("Usage: " + Usage);
                        return;
                }
            }

            caller.Reply("PADWorld.Legends = " + PADWorld.Legends
                + " | filter registered: " + LegendsScreenSystem.Registered);
        }
    }
}
