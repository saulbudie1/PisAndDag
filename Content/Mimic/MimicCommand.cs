using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// /mirror: why the Everclear Mirror's copy is or is not fighting, in a line (MimicSystem.Report): its weapon as it
    /// reads it, its mana, its target, how far and whether in reach and in sight.
    /// </summary>
    public class MimicCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "mirror";

        public override string Description => "Why the Everclear Mirror's copy is or isn't fighting.";

        public override void Action(CommandCaller caller, string input, string[] args) => caller.Reply(MimicSystem.Report());
    }
}
