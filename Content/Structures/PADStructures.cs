using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Loads CSV structures once and stamps them into the world on demand.
    /// </summary>
    public static class PADStructures
    {
        private static Dictionary<string, PADStructure> structures;

        internal static void Load() => structures = new Dictionary<string, PADStructure>();

        internal static void Unload() => structures = null;

        public static bool Has(string name) => structures != null && structures.ContainsKey(name);

        public static PADStructure Get(string name) =>
            structures != null && structures.TryGetValue(name, out PADStructure s) ? s : null;

        /// <summary>
        /// Reads a shape/wall CSV pair and stores it under <paramref name="name"/>.
        /// Call from a ModSystem's PostSetupContent — palettes use ModContent.TileType,
        /// which is not resolvable any earlier.
        /// </summary>
        public static void Register(Mod mod, string name, string shapePath, string wallPath,
            Dictionary<int, StructureTile> tilePalette, Dictionary<int, int> wallPalette)
        {
            int[,] shape = LoadGrid(mod, shapePath);
            if (shape == null)
                return;

            int[,] walls = LoadGrid(mod, wallPath);

            // A wall grid is optional, but a mismatched one is a data bug worth shouting
            // about rather than silently half-applying.
            if (walls != null &&
                (walls.GetLength(0) != shape.GetLength(0) || walls.GetLength(1) != shape.GetLength(1)))
            {
                mod.Logger.Warn($"Structure \"{name}\": wall grid is "
                    + $"{walls.GetLength(0)}x{walls.GetLength(1)} but the shape grid is "
                    + $"{shape.GetLength(0)}x{shape.GetLength(1)}. Walls ignored.");
                walls = null;
            }

            walls ??= new int[shape.GetLength(0), shape.GetLength(1)];

            structures[name] = new PADStructure(shape, walls, tilePalette, wallPalette);
        }

        /// <summary>
        /// Parses a CSV of integers into an [x, y] grid. Rows shorter than the first are
        /// padded rather than throwing, and both Windows and Unix line endings are
        /// accepted — a file saved on the wrong platform should not break worldgen.
        /// </summary>
        private static int[,] LoadGrid(Mod mod, string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            byte[] bytes;
            try
            {
                bytes = mod.GetFileBytes(path);
            }
            catch (Exception e)
            {
                mod.Logger.Warn($"Structure CSV \"{path}\" could not be read: {e.Message}");
                return null;
            }

            if (bytes == null)
            {
                mod.Logger.Warn($"Structure CSV \"{path}\" is missing from the mod.");
                return null;
            }

            string[] lines = Encoding.UTF8.GetString(bytes)
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
                return null;

            int height = lines.Length;
            int width = lines[0].Split(',').Length;
            int[,] grid = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                string[] cells = lines[y].Split(',');

                for (int x = 0; x < width && x < cells.Length; x++)
                {
                    if (int.TryParse(cells[x].Trim(), out int value))
                        grid[x, y] = value;
                }
            }

            return grid;
        }

        /// <summary>
        /// Turns a stamp position and anchor into the structure's top-left corner.
        /// </summary>
        public static Point GetCorner(PADStructure structure, Point position, StructureAnchor anchor)
        {
            int x = position.X;
            int y = position.Y;
            int w = structure.Width;
            int h = structure.Height;

            switch (anchor)
            {
                case StructureAnchor.TopCenter: x -= w / 2; break;
                case StructureAnchor.TopRight: x -= w; break;

                case StructureAnchor.CenterLeft: y -= h / 2; break;
                case StructureAnchor.Center: x -= w / 2; y -= h / 2; break;
                case StructureAnchor.CenterRight: x -= w; y -= h / 2; break;

                case StructureAnchor.BottomLeft: y -= h; break;
                case StructureAnchor.BottomCenter: x -= w / 2; y -= h; break;
                case StructureAnchor.BottomRight: x -= w; y -= h; break;
            }

            return new Point(x, y);
        }

        /// <summary>
        /// The area a structure would occupy — use it with StructureMap.CanPlace before
        /// stamping, and to reserve the ground afterwards.
        /// </summary>
        public static Rectangle GetArea(string name, Point position, StructureAnchor anchor)
        {
            PADStructure s = Get(name);
            if (s == null)
                return Rectangle.Empty;

            Point corner = GetCorner(s, position, anchor);
            return new Rectangle(corner.X, corner.Y, s.Width, s.Height);
        }

        /// <summary>
        /// Writes the structure into the world.
        /// </summary>
        /// <param name="onTilePlaced">
        /// Optional callback per placed tile: world x, world y, palette index.
        /// </param>
        /// <param name="onChestPlaced">
        /// Optional callback per placed chest: the Chest and its palette index. Loot
        /// belongs here rather than in the CSV, so it can be randomised per world.
        /// </param>
        public static bool Stamp(string name, Point position, StructureAnchor anchor,
            Action<int, int, int> onTilePlaced = null,
            Action<Chest, int> onChestPlaced = null)
        {
            PADStructure s = Get(name);
            if (s == null)
            {
                ModContent.GetInstance<PisAndDag>()?.Logger
                    .Warn($"Tried to stamp unknown structure \"{name}\".");
                return false;
            }

            Point corner = GetCorner(s, position, anchor);

            // Bounds-check both corners before touching anything, so a structure that
            // runs off the edge fails cleanly instead of half-placing.
            if (!WorldGen.InWorld(corner.X, corner.Y, 10)
                || !WorldGen.InWorld(corner.X + s.Width, corner.Y + s.Height, 10))
            {
                return false;
            }

            // TWO SWEEPS, and the order matters.
            //
            // Stamping runs column by column, top to bottom. A chest at (8, 12) would
            // therefore be placed before the floor at (8, 14) that it needs to stand on,
            // and the game would simply refuse it. Everything with a footprint waits
            // until the terrain is complete.
            for (int x = 0; x < s.Width; x++)
            {
                for (int y = 0; y < s.Height; y++)
                {
                    int worldX = corner.X + x;
                    int worldY = corner.Y + y;

                    ApplyWall(s, s.Walls[x, y], worldX, worldY);
                    ApplyTerrain(s, s.Shape[x, y], worldX, worldY, onTilePlaced);
                }
            }

            for (int x = 0; x < s.Width; x++)
            {
                for (int y = 0; y < s.Height; y++)
                {
                    ApplyObject(s, s.Shape[x, y], corner.X + x, corner.Y + y,
                        onTilePlaced, onChestPlaced);
                }
            }

            return true;
        }

        /// <summary>
        /// Sweep one: clears the cell and lays plain blocks. Deferred entries are only
        /// cleared here, leaving a hole for sweep two to fill.
        /// </summary>
        private static void ApplyTerrain(PADStructure s, int value, int worldX, int worldY,
            Action<int, int, int> onTilePlaced)
        {
            if (value == PADStructure.Untouched)
                return;

            // Everything but "untouched" starts by clearing, so a structure never
            // inherits half of whatever was buried there.
            Main.tile[worldX, worldY].ClearTile();

            // ClearTile leaves liquid behind. Without this a shrine placed in the jungle
            // fills with the water that was already sitting in the ground.
            Main.tile[worldX, worldY].LiquidAmount = 0;

            if (value == PADStructure.Air)
                return;

            if (!s.TilePalette.TryGetValue(value, out StructureTile entry) || entry.Deferred)
                return;

            // mute: true keeps worldgen silent.
            WorldGen.PlaceTile(worldX, worldY, entry.Type, mute: true, forced: true,
                plr: -1, style: entry.Style);

            onTilePlaced?.Invoke(worldX, worldY, value);
        }

        /// <summary>
        /// Sweep two: chests and furniture, now that there is ground to stand on.
        /// </summary>
        private static void ApplyObject(PADStructure s, int value, int worldX, int worldY,
            Action<int, int, int> onTilePlaced, Action<Chest, int> onChestPlaced)
        {
            if (value == PADStructure.Untouched || value == PADStructure.Air)
                return;

            if (!s.TilePalette.TryGetValue(value, out StructureTile entry) || !entry.Deferred)
                return;

            if (entry.IsChest)
            {
                // PlaceChest has its own call and hands back an index, so the caller can
                // fill it.
                int index = WorldGen.PlaceChest(worldX, worldY, (ushort)entry.Type,
                    notNearOtherChests: false, style: entry.Style);

                if (index >= 0 && index < Main.chest.Length && Main.chest[index] != null)
                    onChestPlaced?.Invoke(Main.chest[index], value);

                return;
            }

            // PlaceObject, not PlaceTile — it reads TileObjectData, so a two-wide anvil
            // frames itself properly instead of leaving a broken half-tile.
            WorldGen.PlaceObject(worldX, worldY, entry.Type, mute: true, style: entry.Style);

            onTilePlaced?.Invoke(worldX, worldY, value);
        }

        private static void ApplyWall(PADStructure s, int value, int worldX, int worldY)
        {
            if (value == PADStructure.Untouched)
                return;

            if (value == PADStructure.Air)
            {
                Main.tile[worldX, worldY].WallType = 0;
                return;
            }

            // Same reason as the tile pass: standing water ruins an interior.
            Main.tile[worldX, worldY].LiquidAmount = 0;

            if (!s.WallPalette.TryGetValue(value, out int wallType))
                return;

            WorldGen.PlaceWall(worldX, worldY, wallType, mute: true);
        }
    }
}
