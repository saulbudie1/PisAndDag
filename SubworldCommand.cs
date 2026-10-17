using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Structures;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DoorItem = PisAndDag.Content.Items.Misc.SubworldDoor;
using DoorTile = PisAndDag.Content.Tiles.SubworldDoor;
using Waygate = PisAndDag.Content.Tiles.Waygate;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for subworlds (Content/Subworlds).
    ///
    ///   /subworld                 where this is, and where its places are kept
    ///   /subworld door            gives an Unmarked Door (there is no recipe yet)
    ///   /subworld undoor          takes up the door under the cursor: nothing else can
    ///   /subworld gate            where this world's waygate is (BeachWaygate), or this place's
    ///   /subworld gate raise      puts the shrine and its waygate down on the beach now, if this
    ///                             world has none (it is looked for again at every load until then)
    ///   /subworld ungate          takes up the waygate under the cursor: nothing else can. If it
    ///                             was the beach's, its shrine stays, and another is raised the
    ///                             next time the world is loaded (or with /subworld gate raise)
    ///   /subworld enter [place]   goes to a place with no door; coming back is to the world's spawn
    ///   /subworld leave           goes back from a place
    ///   /subworld rules on|off    the rules of subworlds (nothing broken, nothing built), off for
    ///                             building in one by hand; on again when the game is closed
    ///   /subworld remake [place]  lays a place out afresh the next time it is gone to, over what
    ///                             was there (the game keeps the old file beside it as .bak)
    ///
    /// A place is named as the class is ("EmptyHall"); with one place, none need be named.
    /// </summary>
    public class SubworldCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "subworld";

        public override string Usage => "/subworld [door | undoor | gate [raise] | ungate | enter [place] | leave | rules on|off | remake [place]]";

        public override string Description => "Subworlds: where this is, a door, going and coming back by hand.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "":
                    Where(caller);
                    return;

                case "door":
                {
                    Player player = caller.Player;

                    player.QuickSpawnItem(player.GetSource_Misc("PisAndDag:SubworldCommand"), ModContent.ItemType<DoorItem>(), 1);
                    caller.Reply("An Unmarked Door. Where it is put down is where it stays: /subworld undoor takes one up.");
                    return;
                }

                case "undoor":
                {
                    Point at = (Main.MouseWorld / 16f).ToPoint();

                    caller.Reply(DoorTile.TakeUp(at.X, at.Y)
                        ? $"Took up the door at {at.X}, {at.Y}."
                        : "No door under the cursor.", Color.LightGray);
                    return;
                }

                case "gate":
                    Gate(caller, args);
                    return;

                case "ungate":
                {
                    if (Main.netMode != NetmodeID.SinglePlayer)
                    {
                        caller.Reply("Single player only.", Color.OrangeRed);
                        return;
                    }

                    Point at = (Main.MouseWorld / 16f).ToPoint();

                    if (!Waygate.IsAt(at.X, at.Y))
                    {
                        caller.Reply("No waygate under the cursor.", Color.LightGray);
                        return;
                    }

                    Waygate.TopLeft(at.X, at.Y, out int left, out int top);

                    bool beach = !Subworld.Active && BeachWaygate.Gate.X == left && BeachWaygate.Gate.Y == top;

                    Waygate.TakeUp(at.X, at.Y);

                    if (beach)
                    {
                        BeachWaygate.Gate = new Point16(-1, -1);
                        caller.Reply($"Took up the beach's waygate at {left}, {top}. Its shrine stays: break it by hand. Another is raised the next time the world is loaded, or now with /subworld gate raise.");
                    }
                    else
                    {
                        caller.Reply($"Took up the waygate at {left}, {top}.", Color.LightGray);
                    }

                    return;
                }

                case "enter":
                {
                    if (Subworld.Active)
                    {
                        caller.Reply("Already in " + Subworld.Current.Name + ". /subworld leave first.", Color.OrangeRed);
                        return;
                    }

                    Subworld place = Named(caller, args);

                    if (place == null)
                        return;

                    if (!SubworldPassage.Enter(place, new Point16(-1, -1)))
                        caller.Reply("Not now: single player only, alive, and not already on the way somewhere.", Color.OrangeRed);

                    return;
                }

                case "leave":
                    if (!Subworld.Active)
                        caller.Reply("This is a world, not a subworld.", Color.OrangeRed);
                    else if (!SubworldPassage.Leave())
                        caller.Reply("Not now: nothing to go back to, or already on the way.", Color.OrangeRed);

                    return;

                case "rules":
                    if (args.Length > 1 && (args[1].Equals("on", StringComparison.OrdinalIgnoreCase) || args[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
                        SubworldKeeper.RulesLifted = args[1].Equals("off", StringComparison.OrdinalIgnoreCase);

                    caller.Reply(SubworldKeeper.RulesLifted
                        ? "Subworld rules: OFF until the game is closed. Things can be built and broken there."
                        : "Subworld rules: on. Nothing is built or broken in a subworld.");
                    return;

                case "remake":
                {
                    Subworld place = Named(caller, args);

                    if (place == null)
                        return;

                    SubworldKeeper.Remake.Add(place.FullName);
                    caller.Reply(place.Name + " will be laid out afresh the next time it is gone to from a world.");
                    return;
                }

                default:
                    caller.Reply("Usage: " + Usage);
                    return;
            }
        }

        /// <summary>The place named, or the only one there is.</summary>
        private static Subworld Named(CommandCaller caller, string[] args)
        {
            Subworld only = null;
            int count = 0;

            foreach (Subworld place in SubworldKeeper.All)
            {
                if (args.Length > 1 && place.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase))
                    return place;

                only = place;
                count++;
            }

            if (args.Length <= 1 && count == 1)
                return only;

            string names = "";

            foreach (Subworld place in SubworldKeeper.All)
                names += (names.Length > 0 ? ", " : "") + place.Name;

            caller.Reply("Which place? " + (names.Length > 0 ? names : "there are none") + ".", Color.OrangeRed);
            return null;
        }

        /// <summary>/subworld gate [raise]: where the waygate is, here or on this world's beach; or the beach's put down now.</summary>
        private static void Gate(CommandCaller caller, string[] args)
        {
            bool raise = args.Length > 1 && args[1].Equals("raise", StringComparison.OrdinalIgnoreCase);

            if (Subworld.Active)
            {
                if (raise)
                {
                    caller.Reply("Only a world's beach has a waygate to raise.", Color.OrangeRed);
                    return;
                }

                Point door = Subworld.Current.Door;

                caller.Reply(Waygate.StandsAt(door.X, door.Y)
                    ? $"This place's waygate: {door.X}, {door.Y}{Away(caller.Player, door.X, door.Y)}."
                    : "This place's way out is not a waygate.", Color.LightGray);
                return;
            }

            Point16 gate = BeachWaygate.Gate;

            if (raise)
            {
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    caller.Reply("Single player only.", Color.OrangeRed);
                    return;
                }

                if (gate.X >= 0)
                {
                    caller.Reply($"This world has its waygate already, at {gate.X}, {gate.Y}: /subworld ungate first.", Color.OrangeRed);
                    return;
                }

                if (BeachWaygate.Raise(false))
                    caller.Reply($"Raised the shrine. Its waygate: {BeachWaygate.Gate.X}, {BeachWaygate.Gate.Y}{Away(caller.Player, BeachWaygate.Gate.X, BeachWaygate.Gate.Y)}.");
                else
                    caller.Reply("No beach on either side has room for it (client.log says so too).", Color.OrangeRed);

                return;
            }

            if (gate.X < 0)
            {
                caller.Reply("This world has no waygate yet. /subworld gate raise looks for a place for it now.", Color.LightGray);
                return;
            }

            caller.Reply($"The beach's waygate: {gate.X}, {gate.Y}{Away(caller.Player, gate.X, gate.Y)}"
                + (Waygate.StandsAt(gate.X, gate.Y) ? "." : ", but it is not there now."), Color.LightGray);
        }

        /// <summary>" (so many tiles left and up of you)", or nothing for a caller with no player.</summary>
        private static string Away(Player player, int x, int y)
        {
            if (player == null)
                return "";

            int dx = x - (int)(player.Center.X / 16f);
            int dy = y - (int)(player.Center.Y / 16f);

            return $" ({Math.Abs(dx)} {(dx < 0 ? "left" : "right")}, {Math.Abs(dy)} {(dy < 0 ? "up" : "down")} of you)";
        }

        private static void Where(CommandCaller caller)
        {
            if (Subworld.Active)
            {
                Subworld here = Subworld.Current;

                caller.Reply($"In {here.Name}: {Main.maxTilesX} by {Main.maxTilesY}, seen from {here.Left}, {here.Top} to {here.Right}, {here.Bottom}. "
                    + (SubworldKeeper.Home != null ? "Came from " + SubworldKeeper.Home.Name + "." : "Not come into from a world: no way back but Save and Exit.")
                    + " Rules " + (SubworldKeeper.RulesLifted ? "OFF." : "on."));

                caller.Reply("File: " + Main.worldPathName, Color.LightGray);
                return;
            }

            caller.Reply("In a world, not a subworld. Rules " + (SubworldKeeper.RulesLifted ? "OFF." : "on."));

            if (Main.netMode != NetmodeID.SinglePlayer || Main.ActiveWorldFileData == null)
                return;

            foreach (Subworld place in SubworldKeeper.All)
            {
                string path = SubworldKeeper.PathFor(place, Main.ActiveWorldFileData);

                caller.Reply($"{place.Name} ({place.Width} by {place.Height}): "
                    + (SubworldKeeper.Made(path) ? "made" : "not made yet")
                    + (SubworldKeeper.Remake.Contains(place.FullName) ? ", to be remade" : "")
                    + ". " + path, Color.LightGray);
            }
        }
    }
}
