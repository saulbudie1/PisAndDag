using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Opens the Magic Arsenal's slots for testing.
    ///
    ///   /magicarsenal          report how many are open and how many are earned
    ///   /magicarsenal max      all ten open, whatever has been earned
    ///   /magicarsenal normal   back to the slots earned
    ///
    /// The earned count is never touched, so going back is exact. Casts left in the slots
    /// that shut are not lost: each moves to the first empty slot still open, and what does
    /// not fit is given back to the player. Max is saved with the player until put back.
    /// Runs for whoever types it; the arsenal is theirs alone.
    /// </summary>
    public class MagicArsenalCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "magicarsenal";

        public override string Usage => "/magicarsenal [max | normal]";

        public override string Description => "Open all ten Magic Arsenal slots for testing, or go back to the ones earned.";

        private static readonly Color Reply = new Color(214, 127, 45);

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            MagicArsenal arsenal = caller.Player.GetModPlayer<MagicArsenal>();

            if (args.Length == 0)
            {
                Report(caller, arsenal);
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "max":
                    arsenal.Max();
                    caller.Reply($"All {MagicArsenal.MaxSlots} slots open. /magicarsenal normal goes back to the {arsenal.Unlocked} earned.", Reply);
                    return;

                case "normal":
                    if (!arsenal.Maxed)
                    {
                        caller.Reply($"Already at the {arsenal.Unlocked} earned.", Reply);
                        return;
                    }

                    arsenal.Normal(out int moved, out int returned);

                    string what = "";

                    if (moved > 0)
                        what += $" {moved} moved into open slots.";

                    if (returned > 0)
                        what += $" {returned} given back to you, no room left in the arsenal.";

                    caller.Reply($"Back to the {arsenal.Unlocked} slots earned." + what, Reply);
                    return;

                default:
                    caller.Reply("Usage: " + Usage, Color.OrangeRed);
                    return;
            }
        }

        private static void Report(CommandCaller caller, MagicArsenal arsenal)
        {
            caller.Reply($"{arsenal.OpenSlots} of {MagicArsenal.MaxSlots} slots open, {arsenal.Unlocked} earned"
                + (arsenal.NecklaceSlot >= 0 ? ", one held open by the Necklace Of The Academy" : "")
                + (arsenal.Maxed ? ", max is on." : "."), Reply);
        }
    }
}
