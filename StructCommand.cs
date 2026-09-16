using Microsoft.Xna.Framework;
using PisAndDag.Content.Structures;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper: stamps a structure at the cursor so you can iterate on a CSV
    /// without regenerating a world every time.
    ///
    ///   /struct                 stamp the Blood Shrine at the cursor
    ///   /struct "Blood Shrine"  stamp a named structure
    ///   /struct update          generate whatever this world is missing (StructureUpdate)
    /// </summary>
    public class StructCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "struct";

        public override string Usage => "/struct [name | update]";

        public override string Description => "Stamp a CSV structure at the cursor.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length == 1 && args[0].Equals("update", System.StringComparison.OrdinalIgnoreCase))
            {
                Update(caller);
                return;
            }

            string name = args.Length > 0
                ? string.Join(" ", args)
                : PADStructureSystem.BloodShrine;

            if (!PADStructures.Has(name))
            {
                caller.Reply($"No structure registered as \"{name}\".", Color.OrangeRed);
                return;
            }

            // Cursor position in tile coordinates.
            Point target = (Main.MouseWorld / 16f).ToPoint();

            // BottomCenter puts the structure's floor where you clicked, which is how you
            // actually want to aim a building.
            bool placed = PADStructures.Stamp(name, target, StructureAnchor.BottomCenter,
                onChestPlaced: PADStructureSystem.FillShrineChest);

            if (!placed)
            {
                caller.Reply("Stamp failed — probably too close to the world edge.", Color.OrangeRed);
                return;
            }

            // Worldgen writes tiles directly, so in multiplayer the change has to be sent.
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                Rectangle area = PADStructures.GetArea(name, target, StructureAnchor.BottomCenter);
                NetMessage.SendTileSquare(-1, area.X, area.Y, area.Width, area.Height);
            }

            caller.Reply($"Stamped \"{name}\" at {target.X}, {target.Y}.", Color.LightGreen);
        }

        /// <summary>
        /// Generates whatever structures this world was made without. Where the world
        /// is decided: singleplayer, or the server. Tiles are written directly, so in
        /// multiplayer everyone has to rejoin to see them; sending the whole world
        /// would be the same thing, slower.
        /// </summary>
        private static void Update(CommandCaller caller)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("Run this on the host.", Color.OrangeRed);
                return;
            }

            caller.Reply("Checking the world...", Color.Gray);

            foreach (string line in StructureUpdate.Run())
                caller.Reply(line, line.EndsWith("placed.") ? Color.LightGreen : line.EndsWith("present.") ? Color.Gray : Color.OrangeRed);

            if (Main.netMode == NetmodeID.Server)
                caller.Reply("Players should rejoin to see the changes.", Color.Gray);
        }
    }
}
