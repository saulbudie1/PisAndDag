using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes.Sludge;
using PisAndDag.Content.Structures;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// The sludge pocket (SludgePocket) in a world made before it, and a way to find it.
    ///
    ///   /sludgepocket          one at the Jungle's floor
    ///   /sludgepocket here     one about whoever typed it
    ///   /sludgepocket go       whoever typed it is taken there (single player)
    ///   /sludgepocket where    where it is, as the Cell Phone gives it
    ///   /sludgepocket check    what it is made of, here and in all: its cavern, how much of its water is sludge, the
    ///                          cell about whoever typed it (also written to the mod's log)
    ///   /sludgepocket fix      every cell of water inside a cavern of it made sludge
    ///
    /// A World command: run where the world is decided, the server in multiplayer.
    /// </summary>
    public class SludgeCommand : ModCommand
    {
        public override CommandType Type => CommandType.World;

        public override string Command => "sludgepocket";

        public override string Usage => "/sludgepocket [here | go | where | check | fix]";

        public override string Description => "The sludge pocket: make one at the Jungle's floor or here, go to it, say where it is, check it, or make all the water in it sludge.";

        private static readonly Color Brown = new(150, 128, 62);

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string word = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            Player player = caller.Player;

            if (word == "go" || word == "where")
            {
                if (SludgePocket.Inside == Point.Zero)
                {
                    caller.Reply("This world has no sludge pocket on record.", Color.LightGray);
                    return;
                }

                if (word == "where" || Main.netMode != NetmodeID.SinglePlayer || player == null)
                {
                    caller.Reply("The sludge pocket is " + Readout(SludgePocket.Inside) + ".", Brown);
                    return;
                }

                Vector2 to = SludgePocket.Inside.ToWorldCoordinates(8f, 16f) - new Vector2(player.width / 2f, player.height);
                player.Teleport(to, TeleportationStyleID.RodOfDiscord);
                caller.Reply("At the sludge pocket.", Brown);
                return;
            }

            if (word == "check")
            {
                Check(caller, player);
                return;
            }

            if (word == "fix")
            {
                int made = 0;

                foreach (Rectangle area in SludgePocket.Areas)
                {
                    for (int y = area.Top; y < area.Bottom; y++)
                    {
                        for (int x = area.Left; x < area.Right; x++)
                        {
                            if (!WorldGen.InWorld(x, y, 1) || ToxicSludge.Marked(x, y))
                                continue;

                            Tile tile = Main.tile[x, y];

                            if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water)
                            {
                                ToxicSludge.Mark(x, y);
                                made++;
                            }
                        }
                    }
                }

                caller.Reply($"{made} cells of water in the sludge pocket made sludge ({ToxicSludge.Count} in all).", Brown);
                Mod.Logger.Info($"/sludgepocket fix: {made} cells of water made sludge; {ToxicSludge.Count} in all.");
                return;
            }

            Point? centre = null;

            if (word == "here")
            {
                if (player == null)
                {
                    caller.Reply("'here' needs a player.", Color.LightGray);
                    return;
                }

                centre = player.Center.ToTileCoordinates();
            }
            else if (word != "")
            {
                caller.Reply("Usage: " + Usage, Color.LightGray);
                return;
            }

            if (!SludgePocket.Make(Main.rand, centre, out string why))
            {
                caller.Reply("No sludge pocket: " + why + ".", Color.OrangeRed);
                return;
            }

            caller.Reply("A sludge pocket is made, " + Readout(SludgePocket.Inside) + ". '/sludgepocket go' takes you there.", Brown);
        }

        /// <summary>What the cavern is made of, said and written to the log: the Jungle found, each cavern, its water and how much of it is sludge, and the cell about the one who asked.</summary>
        private void Check(CommandCaller caller, Player player)
        {
            void Say(string line)
            {
                caller.Reply(line, Color.LightGray);
                Mod.Logger.Info("/sludgepocket check: " + line);
            }

            (int left, int right) = SludgePocket.Jungle();
            Say($"world {Main.maxTilesX} by {Main.maxTilesY}, underworld from row {Main.UnderworldLayer}; Jungle columns {left} to {right} ({right - left} wide); "
                + $"{ToxicSludge.Count} cells of sludge in the world (spreading stops at {ToxicSludge.Most}).");

            foreach (Rectangle area in SludgePocket.Areas)
            {
                int water = 0, marked = 0, markedDry = 0, lowestWater = -1, lowestMarked = -1;

                for (int y = area.Top; y < area.Bottom; y++)
                {
                    for (int x = area.Left; x < area.Right; x++)
                    {
                        if (!WorldGen.InWorld(x, y, 1))
                            continue;

                        Tile tile = Main.tile[x, y];
                        bool wet = tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;
                        bool mark = ToxicSludge.Marked(x, y);

                        if (wet)
                        {
                            water++;
                            lowestWater = y;
                        }

                        if (mark)
                        {
                            lowestMarked = y;

                            if (wet)
                                marked++;
                            else
                                markedDry++;
                        }
                    }
                }

                Say($"cavern {area.X}, {area.Y}, {area.Width} by {area.Height}: {water} cells of water, {marked} of them sludge; {markedDry} marked with no water; "
                    + $"lowest water row {lowestWater}, lowest sludge row {lowestMarked}.");
            }

            if (player == null)
                return;

            Point at = player.Center.ToTileCoordinates();
            Point head = new Vector2(player.Center.X, player.position.Y + 8f).ToTileCoordinates();

            foreach (Point cell in new[] { head, at })
            {
                if (!WorldGen.InWorld(cell.X, cell.Y, 1))
                    continue;

                Tile tile = Main.tile[cell.X, cell.Y];
                Say($"cell {cell.X}, {cell.Y}: liquid {tile.LiquidAmount} of type {tile.LiquidType}, sludge {ToxicSludge.Marked(cell.X, cell.Y)}, "
                    + $"in a cavern {SludgePocket.InArea(cell.X, cell.Y)}; drawn to screen {Main.drawToScreen}, lighting {Lighting.Mode}.");
            }

            Say($"buffs: Overwhelming Toxicity {player.HasBuff(ModContent.BuffType<Content.Buffs.OverwhelmingToxicity>())}, Stench {player.HasBuff(ModContent.BuffType<Content.Buffs.Stench>())}.");
        }

        /// <summary>A place the way the Cell Phone gives it: feet east or west of the middle, feet below the surface.</summary>
        private static string Readout(Point at)
        {
            int across = (at.X - Main.maxTilesX / 2) * 2;
            int down = (at.Y - (int)Main.worldSurface) * 2;
            string side = across >= 0 ? $"{across}' east" : $"{-across}' west";
            string depth = down >= 0 ? $"{down}' below" : $"{-down}' above";
            return side + ", " + depth;
        }
    }
}
