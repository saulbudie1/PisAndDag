using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The signs on the way in. Four of them on the Cleared Ground just before the
    /// Mile, in the order they are read walking toward it, five to seventeen tiles
    /// apart, on whatever the ground is there. Somebody put them up; nobody says who.
    ///
    /// A sign cannot strike a word through, so the crossed-out "It" is hemmed with
    /// dashes and the correction is in red, which is as close as the text tags get.
    /// </summary>
    public class MileSignsPass : GenPass
    {
        /// <summary>Farthest from the Mile first.</summary>
        private static readonly string[] Texts =
        {
            "Proceed at your own risk",
            "I̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶t̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶̶ [c/E02020:they] is hungry",
            "DO NOT ENTER!",
            "god bless their poor souls",
        };

        /// <summary>Tiles from the Mile's edge to the last sign.</summary>
        private const int Clear = 6;

        private const int GapLeast = 5;
        private const int GapMost = 17;

        public MileSignsPass(string name, double weight) : base(name, weight) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Putting up the signs";

            if (!ExtraLargeWorld.Active)
                return;

            // Logical: the Mile starts at MileStart and the way in is from below it.
            int at = Layout.MileStart - Clear;

            for (int i = Texts.Length - 1; i >= 0; i--)
            {
                Place(WorldMirror.X(at), Texts[i]);
                at -= WorldGen.genRand.Next(GapLeast, GapMost + 1);
            }
        }

        /// <summary>A sign standing on the ground at this column, or within a few of it.</summary>
        private static void Place(int x, string text)
        {
            for (int tryX = 0; tryX <= 6; tryX++)
            {
                // Out from the column both ways: 0, +1, -1, +2, -2...
                int dx = (tryX + 1) / 2 * (tryX % 2 == 0 ? 1 : -1);
                int column = x + dx;
                int ground = Ground(column);

                if (ground < 0)
                    continue;

                // A sign is two wide and two tall, set on the tile above the ground.
                if (!WorldGen.PlaceObject(column, ground - 1, TileID.Signs, true))
                    continue;

                int sign = Sign.ReadSign(column, ground - 1, true);

                if (sign >= 0)
                    Sign.TextSign(sign, text);

                return;
            }
        }

        /// <summary>World Y of the first solid tile in a column, from the sky down.</summary>
        private static int Ground(int x)
        {
            if (!WorldGen.InWorld(x, 10, 10))
                return -1;

            for (int y = (int)(Main.worldSurface * 0.35); y < Main.worldSurface + 60; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    return y;
            }

            return -1;
        }
    }
}
