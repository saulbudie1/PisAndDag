using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The first place, and nothing in it yet: ground of Gray Brick twenty-six tiles deep
    /// under open sky, with no music, and the door back standing on it at the left end.
    ///
    /// 400 by 250 tiles, of which 317 by 167 can be seen and walked (Subworld: the edges).
    /// The ground is the last twenty-six rows that can be seen, and runs the whole 400, so
    /// a screen wider than the hall sees no end to it. Only its top few rows are lit: brick
    /// lets the sky's light no further down than earth does. The door stands in the first
    /// two columns that can be seen: its left side is the left side of the screen.
    /// </summary>
    public class EmptyHall : Subworld
    {
        /// <summary>The ground: this many rows of brick.</summary>
        public const int FloorThick = 26;

        public override int Width => 400;

        public override int Height => 250;

        /// <summary>2: the ground was three rows thick in the first.</summary>
        public override int Layout => 2;

        /// <summary>The row the floor's top is: what is stood on.</summary>
        private int Floor => Bottom - FloorThick + 1;

        public override Point Door => new Point(Left, Floor - SubworldDoor.Tall);

        public override void Build()
        {
            Fill(0, Floor, Width - 1, Bottom, TileID.GrayBrick);

            Stand((ushort)ModContent.TileType<SubworldDoor>(), Door.X, Door.Y, SubworldDoor.Wide, SubworldDoor.Tall);
        }
    }
}
