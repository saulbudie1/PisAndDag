#if !PREVIEW
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ObjectData;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The canvas that writes the plan into the world.
    ///
    /// Plan x runs from the inland edge of the plain toward the end of the world; in a
    /// mirrored world that is right to left, so every x goes through <see cref="WX"/>,
    /// every slope is turned the other way, every object is placed from its far column and
    /// faces the other way. Plan y is rows below the plain's level.
    ///
    /// Blocks and walls are written straight into the tile array, the way worldgen writes
    /// them; anything with a footprint goes through TileObject placement, so it is
    /// anchored and framed the way the game would have done it.
    /// </summary>
    public sealed class GraceWorldCanvas : Canvas
    {
        private readonly int ox, oy, dir;

        /// <summary>The world rectangle every write fell inside, for the guard and for framing.</summary>
        public int MinX = int.MaxValue, MaxX = int.MinValue, MinY = int.MaxValue, MaxY = int.MinValue;

        public readonly List<(string name, Rectangle area)> Marks = new List<(string, Rectangle)>();

        /// <summary>Objects that would not go, by tile type. For the log.</summary>
        public readonly Dictionary<int, int> Failed = new Dictionary<int, int>();

        public int Placed;

        /// <summary>
        /// Every cell, by world index, that the builders last laid a kept block or wall in
        /// (<see cref="GraceWorld.Keeps"/>), and the blocks they fitted to open to a wire.
        /// The guard map is taken from these when the building is done.
        /// </summary>
        private readonly BitArray laidTiles, laidWalls;
        private readonly HashSet<int> operable = new HashSet<int>();

        /// <summary>Every cell, by world index, that holds liquid the builders poured.</summary>
        private readonly BitArray poured;

        /// <summary>
        /// Every cell, by world index, the builders have had their hands on, whatever they did
        /// there; and every cell whose tile they put there themselves. What is in neither is
        /// the ground as the world made it.
        /// </summary>
        private readonly BitArray touched, placed;

        public GraceWorldCanvas(int originX, int originY, int direction)
        {
            ox = originX;
            oy = originY;
            dir = direction >= 0 ? 1 : -1;

            laidTiles = new BitArray(Main.maxTilesX * Main.maxTilesY);
            laidWalls = new BitArray(Main.maxTilesX * Main.maxTilesY);
            poured = new BitArray(Main.maxTilesX * Main.maxTilesY);
            touched = new BitArray(Main.maxTilesX * Main.maxTilesY);
            placed = new BitArray(Main.maxTilesX * Main.maxTilesY);
        }

        private static int Cell(int wx, int wy) => wx * Main.maxTilesY + wy;

        /// <summary>The builders' last word in this world cell was a block the guard keeps.</summary>
        public bool LaidTile(int wx, int wy) => laidTiles[Cell(wx, wy)];

        /// <summary>The builders' last word in this world cell was a wall the guard keeps.</summary>
        public bool LaidWall(int wx, int wy) => laidWalls[Cell(wx, wy)];

        /// <summary>The builders fitted the block in this world cell with an actuator, to open to its wire.</summary>
        public bool Operable(int wx, int wy) => operable.Contains(Cell(wx, wy));

        public int WX(int x) => ox + dir * x;

        public int WY(int y) => oy + y;

        private void Touch(int wx, int wy)
        {
            if (wx < MinX) MinX = wx;
            if (wx > MaxX) MaxX = wx;
            if (wy < MinY) MinY = wy;
            if (wy > MaxY) MaxY = wy;
        }

        /// <summary>
        /// A plan cut as Terraria's slope, and back. Terraria names a slope for the corner
        /// that stays (SlopeType.SlopeDownLeft is solid in the lower left), the plan for the
        /// corner the hammer took, so one is the other's mirror with the same number: turned
        /// round once for the naming, and once more in a mirrored world, which puts it back.
        /// The same turn reads a world slope back into the plan.
        /// </summary>
        private Cut Turn(Cut cut)
        {
            if (dir < 0)
                return cut;

            return cut switch
            {
                Cut.DownLeft => Cut.DownRight,
                Cut.DownRight => Cut.DownLeft,
                Cut.UpLeft => Cut.UpRight,
                Cut.UpRight => Cut.UpLeft,
                _ => cut,
            };
        }

        private static void Shape(Tile t, Cut cut)
        {
            t.IsHalfBlock = cut == Cut.Half;
            t.Slope = cut == Cut.Half ? SlopeType.Solid : (SlopeType)(byte)cut;
        }

        private static Cut ShapeOf(Tile t)
        {
            if (t.IsHalfBlock)
                return Cut.Half;
            return (Cut)(byte)t.Slope;
        }

        // ------------------------------------------------------------ blocks

        public override bool InBounds(int x, int y) => WorldGen.InWorld(WX(x), WY(y), 30);

        public override void Block(int x, int y, ushort type, byte paint = 0, Cut cut = Cut.Full)
        {
            if (!InBounds(x, y))
                return;

            int wx = WX(x), wy = WY(y);
            Tile t = Main.tile[wx, wy];
            t.HasTile = true;
            t.TileType = type;
            t.IsActuated = false;
            t.TileFrameX = 0;
            t.TileFrameY = 0;
            Shape(t, Turn(cut));
            t.TileColor = paint;

            int k = Cell(wx, wy);
            if (Main.tileSolid[type] && !Main.tileSolidTop[type])
            {
                t.LiquidAmount = 0;
                poured[k] = false;
            }

            // A block laid over an actuator keeps it, and so keeps opening to its wire.
            laidTiles[k] = GraceWorld.Keeps(type);
            touched[k] = true;
            placed[k] = true;
            Touch(wx, wy);
        }

        public override void Erase(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            int wx = WX(x), wy = WY(y);
            Tile t = Main.tile[wx, wy];
            t.HasTile = false;
            t.IsActuated = false;
            t.HasActuator = false;
            t.Slope = SlopeType.Solid;
            t.IsHalfBlock = false;
            t.TileColor = 0;
            t.TileFrameX = 0;
            t.TileFrameY = 0;
            t.LiquidAmount = 0;

            int k = Cell(wx, wy);
            laidTiles[k] = false;
            poured[k] = false;
            operable.Remove(k);
            touched[k] = true;
            placed[k] = false;
            Touch(wx, wy);
        }

        public override int TypeAt(int x, int y)
        {
            if (!InBounds(x, y))
                return -1;

            Tile t = Main.tile[WX(x), WY(y)];
            return t.HasTile ? t.TileType : -1;
        }

        public override bool Solid(int x, int y)
        {
            if (!InBounds(x, y))
                return false;

            Tile t = Main.tile[WX(x), WY(y)];
            if (!t.HasTile || t.IsActuated)
                return false;

            int type = t.TileType;
            return TileID.Sets.Platforms[type]
                || Main.tileSolid[type] && !Main.tileSolidTop[type] && !Main.tileFrameImportant[type];
        }

        public override Cut CutAt(int x, int y) => InBounds(x, y) ? Turn(ShapeOf(Main.tile[WX(x), WY(y)])) : Cut.Full;

        public override void Reshape(int x, int y, Cut cut)
        {
            if (!InBounds(x, y))
                return;

            Tile t = Main.tile[WX(x), WY(y)];
            if (t.HasTile)
                Shape(t, Turn(cut));
            touched[Cell(WX(x), WY(y))] = true;
        }

        public override void Paint(int x, int y, byte paint)
        {
            if (!InBounds(x, y))
                return;

            Tile t = Main.tile[WX(x), WY(y)];
            if (t.HasTile)
                t.TileColor = paint;
            touched[Cell(WX(x), WY(y))] = true;
        }

        public override byte PaintOf(int x, int y)
        {
            if (!InBounds(x, y))
                return 0;

            Tile t = Main.tile[WX(x), WY(y)];
            return t.HasTile ? t.TileColor : (byte)0;
        }

        public override void Platform(int x, int y, int style, Cut cut = Cut.Full)
        {
            if (!InBounds(x, y))
                return;

            int wx = WX(x), wy = WY(y);
            Tile t = Main.tile[wx, wy];
            t.HasTile = true;
            t.TileType = TileID.Platforms;
            t.IsActuated = false;
            t.TileColor = 0;
            t.TileFrameX = 0;
            t.TileFrameY = (short)(style * 18);
            Shape(t, Turn(cut));

            int k = Cell(wx, wy);
            laidTiles[k] = false;
            operable.Remove(k);
            touched[k] = true;
            placed[k] = true;
            Touch(wx, wy);
        }

        // ------------------------------------------------------------ walls

        public override void Wall(int x, int y, ushort wall, byte paint = 0)
        {
            if (!InBounds(x, y))
                return;

            int wx = WX(x), wy = WY(y);
            Tile t = Main.tile[wx, wy];
            t.WallType = wall;
            t.WallColor = paint;
            laidWalls[Cell(wx, wy)] = GraceWorld.KeepsWall(wall);
            touched[Cell(wx, wy)] = true;
            Touch(wx, wy);
        }

        public override void EraseWall(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            int wx = WX(x), wy = WY(y);
            Tile t = Main.tile[wx, wy];
            t.WallType = 0;
            t.WallColor = 0;
            laidWalls[Cell(wx, wy)] = false;
            touched[Cell(wx, wy)] = true;
            Touch(wx, wy);
        }

        public override int WallAt(int x, int y) => InBounds(x, y) ? Main.tile[WX(x), WY(y)].WallType : 0;

        public override byte WallPaintOf(int x, int y)
        {
            if (!InBounds(x, y))
                return 0;

            Tile t = Main.tile[WX(x), WY(y)];
            return t.WallType != 0 ? t.WallColor : (byte)0;
        }

        // ------------------------------------------------------------ liquid and wire

        public override void Liquid(int x, int y, byte amount, short kind)
        {
            if (!InBounds(x, y))
                return;

            int wx = WX(x), wy = WY(y);
            Tile t = Main.tile[wx, wy];
            t.LiquidAmount = amount;
            t.LiquidType = kind;
            poured[Cell(wx, wy)] = amount > 0;
            touched[Cell(wx, wy)] = true;
        }

        public override byte LiquidAt(int x, int y) => InBounds(x, y) ? Main.tile[WX(x), WY(y)].LiquidAmount : (byte)0;

        public override void Wire(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            Tile t = Main.tile[WX(x), WY(y)];
            t.RedWire = true;
        }

        public override void Actuate(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            int wx = WX(x), wy = WY(y);
            Tile t = Main.tile[wx, wy];
            if (t.HasTile)
            {
                t.HasActuator = true;
                operable.Add(Cell(wx, wy));
            }
        }

        // ------------------------------------------------------------ things

        /// <summary>How far down its sheet a gun's frames go to point it level to the left: eight rows of three tiles.</summary>
        private const short CannonLeftRow = 8 * 54;

        public override (int w, int h) Size(ushort type, int style = 0)
        {
            TileObjectData data = TileObjectData.GetTileData(type, style);
            return data == null ? (1, 1) : (data.Width, data.Height);
        }

        public override bool Built(int x, int y) => InBounds(x, y) && touched[Cell(WX(x), WY(y))];

        public override bool Laid(int x, int y) => InBounds(x, y) && placed[Cell(WX(x), WY(y))] && Main.tile[WX(x), WY(y)].HasTile;

        public override void Snuff(int x0, int y0, int x1, int y1)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
            {
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                {
                    if (!InBounds(x, y))
                        continue;

                    Tile t = Main.tile[WX(x), WY(y)];
                    if (t.HasTile)
                        t.TileFrameX = Unlit(t.TileType, t.TileFrameX);
                }
            }
        }

        public override bool Place(int x, int bottom, ushort type, int style = 0, int facing = 1)
        {
            TileObjectData data = TileObjectData.GetTileData(type, style);
            int w = data?.Width ?? 1, h = data?.Height ?? 1;
            int top = bottom - h + 1;

            // The object's world-left column: in a mirrored world its plan-right column.
            int left = dir > 0 ? WX(x) : WX(x + w - 1);
            int wtop = WY(top);

            if (!WorldGen.InWorld(left, wtop, 30) || !WorldGen.InWorld(left + w - 1, wtop + h - 1, 30))
                return Fail(type);

            int worldFacing = dir > 0 ? facing : -facing;
            bool ok;

            if (data == null)
            {
                ok = WorldGen.PlaceTile(left, wtop, type, true, false, -1, style);
            }
            else
            {
                int px = left + data.Origin.X, py = wtop + data.Origin.Y;

                if (type == TileID.Containers || type == TileID.Containers2)
                    ok = WorldGen.PlaceChest(px, py, type, false, style) >= 0;
                else
                    ok = WorldGen.PlaceObject(px, py, type, true, style, 0, -1, worldFacing);
            }

            Tile foot = Main.tile[left, wtop + h - 1];
            ok = ok && foot.HasTile && foot.TileType == type;

            if (!ok)
                return Fail(type);

            // A gun has no left-facing frame of its own: its barrel is trained by the row of the
            // sheet, level to the right in the first row and level to the left in the ninth.
            if (type == TileID.Cannon && worldFacing < 0)
            {
                for (int px = left; px < left + w; px++)
                {
                    for (int py = wtop; py < wtop + h; py++)
                    {
                        Tile gun = Main.tile[px, py];
                        gun.TileFrameY += CannonLeftRow;
                    }
                }
            }

            // An object is nobody's wall: whatever it stands in is not kept.
            for (int px = left; px < left + w; px++)
            {
                for (int py = wtop; py < wtop + h; py++)
                {
                    int k = Cell(px, py);
                    laidTiles[k] = false;
                    operable.Remove(k);
                    touched[k] = true;
                    placed[k] = true;
                }
            }

            Placed++;
            Touch(left, wtop);
            Touch(left + w - 1, wtop + h - 1);
            return true;
        }

        private bool Fail(int type)
        {
            Failed.TryGetValue(type, out int n);
            Failed[type] = n + 1;
            return false;
        }

        public override bool PlaceChest(int x, int bottom, ushort type, int style, IReadOnlyList<(int type, int stack)> items)
        {
            if (!Place(x, bottom, type, style))
                return false;

            var (w, h) = Size(type, style);
            int left = dir > 0 ? WX(x) : WX(x + w - 1);
            int id = Terraria.Chest.FindChest(left, WY(bottom - h + 1));
            if (id < 0 || Main.chest[id] == null)
                return true;

            Item[] slots = Main.chest[id].item;
            int k = 0;
            foreach (var (item, stack) in items)
            {
                if (k >= slots.Length)
                    break;
                if (item <= 0 || stack <= 0)
                    continue;

                slots[k].SetDefaults(item);
                slots[k].stack = Math.Min(stack, slots[k].maxStack);
                k++;
            }

            return true;
        }

        public override bool Blocks(int x, int y)
        {
            if (!InBounds(x, y))
                return true;

            Tile t = Main.tile[WX(x), WY(y)];
            return t.HasTile && !t.IsActuated && Main.tileSolid[t.TileType];
        }

        public override bool HouseWall(int x, int y) => InBounds(x, y) && Main.wallHouse[Main.tile[WX(x), WY(y)].WallType];

        public override bool Sign(int x, int bottom, ushort type, int style, string text)
        {
            if (!Place(x, bottom, type, style))
                return false;

            var (w, h) = Size(type, style);
            int left = dir > 0 ? WX(x) : WX(x + w - 1);
            int id = Terraria.Sign.ReadSign(left, WY(bottom - h + 1), true);
            if (id >= 0)
                Terraria.Sign.TextSign(id, text);

            return true;
        }

        public override bool Tree(int x, int ground, ushort type, int height, byte bark = 0)
        {
            if (!InBounds(x, ground))
                return false;

            int wx = WX(x), wy = WY(ground);
            bool ok;

            // The game's own growth for the tree's kind, held to the height asked for.
            if (height > 0 && Growth(type, out WorldGen.GrowTreeSettings settings))
            {
                settings.TreeHeightMin = height;
                settings.TreeHeightMax = height;
                ok = WorldGen.GrowTreeWithSettings(wx, wy, settings);
            }
            else
            {
                ok = WorldGen.TryGrowingTreeByType(type, wx, wy);
            }

            if (!ok)
            {
                Fail(type);
                return false;
            }

            Touch(wx, wy - Math.Max(40, height + 16));

            // The bark: the trunk up to the tile under its crown, and the roots either side of
            // its foot. The crown and the branches keep their leaves' own colour.
            if (bark != 0)
            {
                int top = wy - 1;
                while (top > 10 && Main.tile[wx, top - 1].HasTile && Main.tile[wx, top - 1].TileType == type)
                    top--;

                for (int y = wy - 1; y > top; y--)
                {
                    Tile trunk = Main.tile[wx, y];
                    trunk.TileColor = bark;
                }

                foreach (int rx in new[] { wx - 1, wx + 1 })
                {
                    Tile root = Main.tile[rx, wy - 1];
                    if (root.HasTile && root.TileType == type)
                        root.TileColor = bark;
                }
            }

            return true;
        }

        /// <summary>How the game grows a tree of this kind, for the kinds the builders plant.</summary>
        private static bool Growth(ushort type, out WorldGen.GrowTreeSettings settings)
        {
            switch (type)
            {
                case TileID.VanityTreeYellowWillow:
                    settings = WorldGen.GrowTreeSettings.Profiles.VanityTree_Willow;
                    return true;
                case TileID.TreeAsh:
                    settings = WorldGen.GrowTreeSettings.Profiles.Tree_Ash;
                    return true;
                default:
                    settings = default;
                    return false;
            }
        }

        public override void Mark(string name, int x, int y, int w, int h)
        {
            int left = dir > 0 ? WX(x) : WX(x + w - 1);
            Marks.Add((name, new Rectangle(left, WY(y), w, h)));
        }

        public override int Next(int max) => max <= 1 ? 0 : WorldGen.genRand.Next(max);

        // ------------------------------------------------------------ before and after

        /// <summary>
        /// Clears a world rectangle of everything that has a footprint or a record behind it
        /// -- furniture, pots, chests, signs, trees, herbs -- before anything is built, so
        /// nothing the old ground held is left half cut through a wall. The wires and
        /// actuators of vanilla's traps go too, so none of them joins a circuit of ours.
        /// Blocks, walls and liquid are left for the builders.
        /// </summary>
        public static void Prepare(Rectangle area)
        {
            int x0 = Math.Max(area.Left, 10), x1 = Math.Min(area.Right, Main.maxTilesX - 10);
            int y0 = Math.Max(area.Top, 10), y1 = Math.Min(area.Bottom, Main.maxTilesY - 10);

            for (int x = x0; x < x1; x++)
            {
                for (int y = y0; y < y1; y++)
                {
                    Tile t = Main.tile[x, y];
                    t.RedWire = false;
                    t.BlueWire = false;
                    t.GreenWire = false;
                    t.YellowWire = false;
                    t.HasActuator = false;

                    if (!t.HasTile)
                        continue;

                    int type = t.TileType;
                    if (Main.tileFrameImportant[type] || type == TileID.Trees || type == TileID.PalmTree
                        || type == TileID.Cactus || TileID.Sets.IsATreeTrunk[type])
                    {
                        t.HasTile = false;
                        t.TileFrameX = 0;
                        t.TileFrameY = 0;
                    }
                }
            }

            Forget(area);
        }

        /// <summary>Drops the records of chests and signs in the area whose tiles are no longer there.</summary>
        public static void Forget(Rectangle area)
        {
            for (int i = 0; i < Main.maxChests; i++)
            {
                Chest chest = Main.chest[i];
                if (chest == null || !area.Contains(chest.x, chest.y))
                    continue;

                Tile t = Main.tile[chest.x, chest.y];
                if (!t.HasTile || !TileID.Sets.BasicChest[t.TileType])
                    Main.chest[i] = null;
            }

            for (int i = 0; i < Terraria.Sign.maxSigns; i++)
            {
                Sign sign = Main.sign[i];
                if (sign == null || !area.Contains(sign.x, sign.y))
                    continue;

                Tile t = Main.tile[sign.x, sign.y];
                if (!t.HasTile || !Main.tileSign[t.TileType])
                    Main.sign[i] = null;
            }
        }

        /// <summary>
        /// Takes out every drop of water, lava and honey the builders did not pour, from the
        /// given world row down, over everything built and a margin round it. Whatever the
        /// caves under the plain held would otherwise find its way into the streets and the
        /// galleries, and down the old tunnel into the Shimmer's room.
        /// </summary>
        public void DrainUnder(int fromY, int margin)
        {
            if (MinX > MaxX)
                return;

            int x0 = Math.Max(MinX - margin, 10), x1 = Math.Min(MaxX + margin, Main.maxTilesX - 10);
            int y0 = Math.Max(fromY, 10), y1 = Math.Min(MaxY + margin, Main.maxTilesY - 10);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Tile t = Main.tile[x, y];
                    if (t.LiquidAmount > 0 && !poured[Cell(x, y)])
                        t.LiquidAmount = 0;
                }
            }
        }

        /// <summary>
        /// Frames what was written directly: every plain block and platform in the area and
        /// every wall. Objects were framed as they were placed and are left alone.
        /// </summary>
        public void Finish()
        {
            if (MinX > MaxX)
                return;

            int x0 = Math.Max(MinX - 2, 10), x1 = Math.Min(MaxX + 2, Main.maxTilesX - 10);
            int y0 = Math.Max(MinY - 2, 10), y1 = Math.Min(MaxY + 2, Main.maxTilesY - 10);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Tile t = Main.tile[x, y];

                    if (t.HasTile && (!Main.tileFrameImportant[t.TileType] || TileID.Sets.Platforms[t.TileType]))
                        WorldGen.TileFrame(x, y, true, true);

                    if (t.WallType > 0)
                        Framing.WallFrame(x, y, true);
                }
            }

            Forget(new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
        }
    }
}
#endif
