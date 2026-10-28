using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.GildedKnight;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for the Gilded Knight of Grace, who stands before the Curtain.
    ///
    ///   /knight          where he stands in this world: beaten or not, his post, his field
    ///   /knight summon   called back for whoever typed it, as the spur calls him: from his post on
    ///                    his field (they must be on it), or, in a world with no Curtain, from off
    ///                    along the ground to a field about them
    ///   /knight reset    back to never beaten, and any of him about gone (his post is his again)
    ///
    /// A World command: run where the world is decided, the server in multiplayer.
    /// </summary>
    public class KnightCommand : ModCommand
    {
        private static readonly Color Done = new Color(255, 214, 120);

        public override CommandType Type => CommandType.World;

        public override string Command => "knight";

        public override string Usage => "/knight [summon|reset]";

        public override string Description => "The Gilded Knight: show where he stands, call him back here, or reset him to never beaten.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "summon":
                {
                    if (KnightEncounter.Survey() && !KnightEncounter.OnField(caller.Player))
                    {
                        caller.Reply("He comes only to his field, before the Curtain. " + KnightEncounter.Describe(), Color.OrangeRed);
                        return;
                    }

                    string refused = KnightEncounter.Call(caller.Player);

                    if (refused != null)
                    {
                        caller.Reply(refused, Color.OrangeRed);
                        return;
                    }

                    caller.Reply("Armour, coming down the field.", Done);
                    return;
                }

                case "reset":
                    KnightEncounter.Reset();
                    caller.Reply("The Gilded Knight reset: never beaten, standing his watch again.", Done);
                    return;

                case "":
                    caller.Reply(KnightEncounter.Describe() + " Usage: " + Usage);
                    return;

                default:
                    caller.Reply("Usage: " + Usage, Color.OrangeRed);
                    return;
            }
        }
    }
}
