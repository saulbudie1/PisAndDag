using System;
using System.IO;
using PisAndDag.Content.Subworlds;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The vent on the Wasted Mile (LabVent), by request: "in the middle of the Mile". As near the middle of the Mile's span
    /// (Layout.Mile) as there is ground level for its two columns with nothing over it for its four rows, trying further and
    /// further either way; if nowhere, in the middle, the ground there made level for it. Only where there is a Mile: the
    /// extra large world (ExtraLargeWorld).
    ///
    /// WHEN. In a new world, as the last of its making (PostWorldGen). In a world made before there was one, the first time
    /// it is loaded, before anybody stands in it. Once: the place is saved with the world (<see cref="Vent"/>) and sent to
    /// every client. Written straight into the tiles, as the waygate's shrine is (BeachWaygate), the ground under it framed
    /// again after, and in multiplayer the square of it sent.
    /// </summary>
    public class MileVent : ModSystem
    {
        /// <summary>The vent's top left tile, or (-1, -1) if the world has none yet.</summary>
        public static Point16 Vent = new Point16(-1, -1);

        /// <summary>Rows from the top of the world the walk down each column starts at.</summary>
        private const int SkyTop = 20;

        public override void ClearWorld()
        {
            Vent = new Point16(-1, -1);
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Vent.X >= 0)
            {
                tag["PADMileVentX"] = (int)Vent.X;
                tag["PADMileVentY"] = (int)Vent.Y;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Vent = tag.ContainsKey("PADMileVentX") && tag.ContainsKey("PADMileVentY")
                ? new Point16(tag.GetInt("PADMileVentX"), tag.GetInt("PADMileVentY"))
                : new Point16(-1, -1);
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Vent.X);
            writer.Write(Vent.Y);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Vent = new Point16(reader.ReadInt16(), reader.ReadInt16());
        }

        public override void PostWorldGen()
        {
            Raise();
        }

        public override void PostWorldLoad()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || Subworld.Active)
                return;

            // One put down two wide, before it was one: its second column taken away.
            if (Vent.X >= 0)
            {
                if (LabVent.TidyOldWidth(Vent.X, Vent.Y))
                    ModContent.GetInstance<MileVent>().Mod.Logger.Info($"Mile vent: its second column taken away at {Vent.X + 1}, {Vent.Y}, now that it is one wide.");

                return;
            }

            Raise();
        }

        /// <summary>Puts the vent down on this world's Mile, if it has one; says so in client.log either way.</summary>
        internal static bool Raise()
        {
            Mod mod = ModContent.GetInstance<MileVent>().Mod;

            if (!ExtraLargeWorld.Active)
                return false;

            (int left, int right) = Layout.Mile;
            int middle = (left + right) / 2;
            int x = -1, floor = -1;

            for (int step = 0; step <= (right - left) / 2 - LabVent.Wide - 2 && x < 0; step++)
            {
                for (int sign = -1; sign <= 1 && x < 0; sign += 2)
                {
                    int at = middle + sign * step;

                    if (Fits(at, out int g))
                    {
                        x = at;
                        floor = g;
                    }
                }
            }

            bool levelled = false;

            if (x < 0)
            {
                x = middle;
                floor = Math.Min(Ground(x), Ground(x + 1));

                if (floor < 0)
                {
                    mod.Logger.Warn("Mile vent: no ground in the middle of the Mile. It will be looked for again the next time the world is loaded.");
                    return false;
                }

                levelled = true;
            }

            Build(x, floor);
            Vent = new Point16(x, floor - LabVent.Tall);

            mod.Logger.Info($"Mile vent: put down at {Vent.X}, {Vent.Y}{(levelled ? ", the ground levelled for it" : "")}, the Mile being columns {left} to {right}.");

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);

            return true;
        }

        /// <summary>The first solid row from the top in this column, or -1.</summary>
        private static int Ground(int x)
        {
            if (!WorldGen.InWorld(x, SkyTop))
                return -1;

            int lowest = Math.Min(Main.maxTilesY - 50, (int)Main.worldSurface + 120);

            for (int y = SkyTop; y < lowest; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    return y;
            }

            return -1;
        }

        /// <summary>The ground is level and whole under these two columns, and nothing at all is over it, nor water, for the vent's height and a row more; and the columns either side are clear over it too.</summary>
        private static bool Fits(int x, out int floor)
        {
            floor = Ground(x);

            if (floor < 0 || Ground(x + 1) != floor)
                return false;

            for (int c = x; c < x + LabVent.Wide; c++)
            {
                Tile ground = Main.tile[c, floor];

                if (ground.IsHalfBlock || ground.Slope != SlopeType.Solid)
                    return false;
            }

            for (int c = x - 1; c <= x + LabVent.Wide; c++)
            {
                for (int y = floor - LabVent.Tall - 1; y < floor; y++)
                {
                    Tile tile = Main.tile[c, y];

                    if (tile.HasTile || tile.LiquidAmount > 0)
                        return false;
                }
            }

            return true;
        }

        /// <summary>The vent on its ground: the ground under it made whole and level (of what is under it, or ash), what is over it cleared, then the vent.</summary>
        private static void Build(int x, int floor)
        {
            for (int c = x; c < x + LabVent.Wide; c++)
            {
                // Down to the ground, filled; over it, cleared.
                for (int y = floor; y < floor + 40 && WorldGen.InWorld(c, y); y++)
                {
                    Tile tile = Main.tile[c, y];

                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    {
                        tile.Slope = SlopeType.Solid;
                        tile.IsHalfBlock = false;

                        if (y > floor)
                            break;

                        continue;
                    }

                    tile.ClearTile();
                    tile.HasTile = true;
                    tile.TileType = TileID.Ash;
                    tile.LiquidAmount = 0;
                }

                for (int y = floor - LabVent.Tall - 1; y < floor; y++)
                {
                    Tile tile = Main.tile[c, y];

                    tile.ClearTile();
                    tile.LiquidAmount = 0;
                }
            }

            LabVent.Put(x, floor - LabVent.Tall, (ushort)ModContent.TileType<LabVent>());

            for (int c = x - 1; c <= x + LabVent.Wide; c++)
                WorldGen.SquareTileFrame(c, floor, true);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x - 1, floor - LabVent.Tall - 1, LabVent.Wide + 2, LabVent.Tall + 3);
        }
    }
}
