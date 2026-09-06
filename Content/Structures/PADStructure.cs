using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Where a stamp position sits relative to the structure's own box.
    /// </summary>
    public enum StructureAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        CenterLeft,
        Center,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    /// <summary>
    /// One palette entry: what a number in the CSV actually places.
    /// </summary>
    public readonly struct StructureTile
    {
        public readonly int Type;
        public readonly int Style;
        public readonly bool IsChest;

        /// <summary>
        /// True for anything that needs the terrain finished before it can go down —
        /// chests, anvils, tables, anything with a footprint that must rest on solid
        /// ground. Stamping runs column by column, so a chest placed at the same time as
        /// the blocks would be asked to stand on a floor that does not exist yet and
        /// would simply be rejected. Deferred entries are placed in a second sweep.
        /// </summary>
        public readonly bool Deferred;

        private StructureTile(int type, int style, bool isChest, bool deferred)
        {
            Type = type;
            Style = style;
            IsChest = isChest;
            Deferred = deferred;
        }

        /// <summary>A plain block, named directly by its TileID.</summary>
        public static StructureTile Of(int tileType, int style = 0) =>
            new StructureTile(tileType, style, false, false);

        /// <summary>
        /// Multi-tile furniture — an anvil, a table, a workbench. Placed after the
        /// terrain, through WorldGen.PlaceObject so it frames itself correctly.
        /// </summary>
        public static StructureTile Object(int tileType, int style = 0) =>
            new StructureTile(tileType, style, false, true);

        /// <summary>
        /// Resolve the tile type AND place style from the item that places it.
        ///
        /// Much safer than naming a TileID and a style number by hand: variants like
        /// beams, chests and furniture all share one tile ID and differ only by style,
        /// and those numbers are undocumented. Asking the item removes the guesswork —
        /// ItemID.RichMahoganyBeam knows both, so nothing here can be off by one.
        /// </summary>
        public static StructureTile FromItem(int itemID)
        {
            Item sample = ContentSamples.ItemsByType[itemID];
            return new StructureTile(sample.createTile, sample.placeStyle, false, false);
        }

        /// <summary>Multi-tile furniture, resolved from the item that places it.</summary>
        public static StructureTile ObjectFromItem(int itemID)
        {
            Item sample = ContentSamples.ItemsByType[itemID];
            return new StructureTile(sample.createTile, sample.placeStyle, false, true);
        }

        /// <summary>A chest, resolved from the item that places it.</summary>
        public static StructureTile ChestFromItem(int itemID)
        {
            Item sample = ContentSamples.ItemsByType[itemID];
            return new StructureTile(sample.createTile, sample.placeStyle, true, true);
        }
    }

    /// <summary>
    /// A structure loaded from a pair of CSV grids: one for tiles, one for walls.
    ///
    /// The numbers in the CSV are NOT tile IDs. They are palette indices resolved at
    /// registration time, so the data file stays readable and never has to change when a
    /// tile class is renamed or a vanilla ID is swapped out.
    ///
    /// Two values are reserved in both grids:
    ///   0  leave whatever the world already has here — this is what lets a structure
    ///      blend into its surroundings instead of stamping a rectangular hole
    ///   1  clear this cell to nothing (the hollow interior of a room)
    /// Everything from 2 upward is a palette lookup.
    /// </summary>
    public class PADStructure
    {
        public const int Untouched = 0;
        public const int Air = 1;

        /// <summary>Tile grid, indexed [x, y].</summary>
        public readonly int[,] Shape;

        /// <summary>Wall grid, same dimensions as <see cref="Shape"/>.</summary>
        public readonly int[,] Walls;

        public readonly Dictionary<int, StructureTile> TilePalette;

        /// <summary>Wall palette: index to WallID.</summary>
        public readonly Dictionary<int, int> WallPalette;

        public int Width => Shape.GetLength(0);

        public int Height => Shape.GetLength(1);

        public PADStructure(int[,] shape, int[,] walls,
            Dictionary<int, StructureTile> tilePalette, Dictionary<int, int> wallPalette)
        {
            Shape = shape;
            Walls = walls;
            TilePalette = tilePalette ?? new Dictionary<int, StructureTile>();
            WallPalette = wallPalette ?? new Dictionary<int, int>();
        }
    }
}
