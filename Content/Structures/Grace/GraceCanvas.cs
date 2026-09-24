using System;
using System.Collections.Generic;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// How a block sits in its cell, named for the side the hammer takes away.
    ///
    /// DownLeft: the top falls away to the left, the top-left corner is gone, the solid
    /// half is the lower right. DownRight: the mirror of that. UpLeft: the underside rises
    /// to the left, the bottom-left corner is gone, the solid half is the upper right.
    /// UpRight: the mirror of that. Half is a half block.
    ///
    /// Terraria names its slopes the other way, for the corner that stays solid, so its
    /// SlopeDownLeft is our DownRight. The world canvas makes the change; nothing that
    /// builds needs to know it.
    /// </summary>
    public enum Cut : byte
    {
        Full = 0,
        DownLeft = 1,
        DownRight = 2,
        UpLeft = 3,
        UpRight = 4,
        Half = 8,
    }

    /// <summary>
    /// Everything the Kingdom of Grace is built through.
    ///
    /// Coordinates are the plan's, not the world's: x counts from the inland edge of the
    /// Grace Plain toward the end of the world, and y counts down from the level of the
    /// plain (0 is the top row of ground, negative is sky). The world canvas turns that
    /// into tiles, and in a mirrored world it turns it the other way round, slopes and
    /// facings included; nothing that builds ever has to know which way the world came out.
    ///
    /// Nothing here touches Terraria, so the same builders can be run outside the game
    /// against a canvas that only records what they did.
    /// </summary>
    public abstract class Canvas
    {
        // ---- blocks ----

        public abstract bool InBounds(int x, int y);

        public abstract void Block(int x, int y, ushort type, byte paint = 0, Cut cut = Cut.Full);

        public abstract void Erase(int x, int y);

        /// <summary>The tile type here, or -1 for none.</summary>
        public abstract int TypeAt(int x, int y);

        /// <summary>A solid block is here: something a player stands on.</summary>
        public abstract bool Solid(int x, int y);

        public abstract Cut CutAt(int x, int y);

        public abstract void Reshape(int x, int y, Cut cut);

        /// <summary>Paints whatever tile is here, block or object.</summary>
        public abstract void Paint(int x, int y, byte paint);

        /// <summary>The paint on the tile here, 0 for none.</summary>
        public abstract byte PaintOf(int x, int y);

        /// <summary>The paint on the wall here, 0 for none.</summary>
        public abstract byte WallPaintOf(int x, int y);

        /// <summary>A platform of the given style: something to stand on that can be dropped through.</summary>
        public abstract void Platform(int x, int y, int style, Cut cut = Cut.Full);

        // ---- walls ----

        public abstract void Wall(int x, int y, ushort wall, byte paint = 0);

        public abstract void EraseWall(int x, int y);

        public abstract int WallAt(int x, int y);

        // ---- rooms, as the game counts them ----

        /// <summary>Something here closes a room: any solid tile, doors, platforms and trapdoors included.</summary>
        public abstract bool Blocks(int x, int y);

        /// <summary>The wall here is one a house can be made with: anything a player could have put up.</summary>
        public abstract bool HouseWall(int x, int y);

        // ---- liquid ----

        public abstract void Liquid(int x, int y, byte amount, short kind);

        /// <summary>How much liquid stands in this cell, 0 to 255.</summary>
        public abstract byte LiquidAt(int x, int y);

        // ---- wiring ----

        /// <summary>A length of red wire through this tile.</summary>
        public abstract void Wire(int x, int y);

        /// <summary>An actuator on the block here, so a signal down the wire turns it on and off.</summary>
        public abstract void Actuate(int x, int y);

        // ---- things ----

        /// <summary>
        /// A piece of furniture, a statue, a lamp: anything placed as one object.
        /// <paramref name="x"/> is its leftmost column and <paramref name="bottom"/> its
        /// lowest row, in plan coordinates. <paramref name="dir"/> is +1 to face the end of
        /// the world, -1 to face inland. False if the object would not go.
        /// </summary>
        public abstract bool Place(int x, int bottom, ushort type, int style = 0, int dir = 1);

        /// <summary>Width and height, in tiles, of an object of this type and style.</summary>
        public abstract (int w, int h) Size(ushort type, int style = 0);

        /// <summary>A readable sign or stone at x (left column), bottom row, with this text on it.</summary>
        public abstract bool Sign(int x, int bottom, ushort type, int style, string text);

        /// <summary>
        /// A chest of this type and style at x (left column), bottom row, with these items in its
        /// first slots, in order. False if the chest would not go.
        /// </summary>
        public abstract bool PlaceChest(int x, int bottom, ushort type, int style, IReadOnlyList<(int type, int stack)> items);

        /// <summary>
        /// A tree of this type grown from the ground tile at (x, ground), its trunk
        /// <paramref name="height"/> tall, or as tall as the game would grow it if that is 0.
        /// If <paramref name="bark"/> is not 0 the trunk and its roots are painted with it; the
        /// crown and the leafy branches are left their own colour.
        /// </summary>
        public abstract bool Tree(int x, int ground, ushort type, int height, byte bark = 0);

        /// <summary>Somewhere a later piece of code needs to find again. Saved with the world.</summary>
        public abstract void Mark(string name, int x, int y, int w, int h);

        // ---- what the builders did ----

        /// <summary>The builders have had their hands on this cell: laid, cut out, reshaped, walled or wetted it.</summary>
        public abstract bool Built(int x, int y);

        /// <summary>The tile in this cell is one the builders put there, and nobody has taken it out since.</summary>
        public abstract bool Laid(int x, int y);

        /// <summary>
        /// Puts out every torch, candle, lamp, lantern, candelabra and chandelier with a cell
        /// in the rectangle, the way its own switch would: the fitting stays, dark.
        /// </summary>
        public abstract void Snuff(int x0, int y0, int x1, int y1);

        /// <summary>
        /// A light fitting's frame once it is put out, or the frame as it is if it is out
        /// already or is not a light. The dark frames sit beside the lit ones on each sheet.
        /// </summary>
        protected static short Unlit(int type, short frameX)
        {
            switch (type)
            {
                case TileID.Torches:
                    return frameX < 66 ? (short)(frameX + 66) : frameX;
                case TileID.Candles:
                    return frameX < 18 ? (short)(frameX + 18) : frameX;
                case TileID.Chandeliers:
                    return frameX % 108 < 54 ? (short)(frameX + 54) : frameX;
                case TileID.HangingLanterns:
                case TileID.Lamps:
                    return frameX % 36 < 18 ? (short)(frameX + 18) : frameX;
                case TileID.Candelabras:
                    return frameX % 72 < 36 ? (short)(frameX + 36) : frameX;
                default:
                    return frameX;
            }
        }

        // ---- dice ----

        public abstract int Next(int max);

        public int Next(int min, int max) => max <= min ? min : min + Next(max - min);

        public bool OneIn(int n) => n <= 1 || Next(n) == 0;

        public double NextDouble() => Next(1 << 24) / (double)(1 << 24);

        public T Pick<T>(params T[] options) => options[Next(options.Length)];

        // ---- reading the ground ----

        /// <summary>The first solid row at or below <paramref name="from"/> in this column, or int.MinValue.</summary>
        public int FirstSolidDown(int x, int from, int to)
        {
            for (int y = from; y <= to; y++)
            {
                if (Solid(x, y))
                    return y;
            }

            return int.MinValue;
        }
    }
}
