using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes.Sludge;
using PisAndDag.Content.Tiles.Sludge;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using Plan = PisAndDag.Content.Structures.SludgePocketPlan;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The Sump in the world: a cavern of toxic sludge at the bottom of the Jungle, two thirds as wide as the Jungle is
    /// (by request: "a sub biome at the bottom of the jungle where the poison and toxic sludge is"; "this pocket should
    /// be 2/3 as wide as the jungle; think sunken sea under the desert"). One a world, laid at the end of worldgen
    /// (after everything else, so nothing after it settles its lake or covers its works), or by "/sludgepocket" in a
    /// world made before it (SludgeCommand).
    ///
    /// Where: the Jungle is found off the world itself (the columns of the caverns that are mostly mud). The cavern is
    /// centred under it, its canvas's foot a few rows over the Underworld, as high as its width asks
    /// (SludgePocketPlan.HighFor) unless the caverns are not that deep. A little to either side, or a little higher,
    /// if that will not do.
    ///
    /// How: what the plan says, cell by cell, onto the world. Chests where anything changes are first taken up whole
    /// (what is in them, their kind, their name) and set down again on free floor in the cavern. Open cells cleared (a
    /// mud wall behind them and the shell where there was none), the shell and the halftone set to Acidic Mud and
    /// Stained Cobblestone, the lake full of water and every cell of it marked as sludge (ToxicSludge); the works
    /// written frame by frame (they stand on what the plan gave them), the shut barrels placed as chests and stocked;
    /// the ways in dug on past the canvas where the plan ran out of it. Then framed, and in multiplayer sent to
    /// everyone in pieces.
    ///
    /// Kept with the world: where each cavern is (for the biome, SumpBiome, which also counts its blocks) and a place
    /// in the last one to stand (SludgeCommand's "go").
    /// </summary>
    public class SludgePocket : ModSystem
    {
        /// <summary>A place in the last cavern made to stand, on dry floor (tiles); (0, 0) if none was made in this world.</summary>
        public static Point Inside;

        /// <summary>Each cavern's extent (tiles).</summary>
        public static readonly List<Rectangle> Areas = new();

        private const string InsideKey = "SludgePocketInside", AreasKey = "SludgePocketAreas";

        /// <summary>Rows kept between the canvas's foot and the Underworld's top.</summary>
        private const int AboveUnderworld = 4;

        /// <summary>Whether a tile is in a cavern of it.</summary>
        public static bool InArea(int x, int y)
        {
            foreach (Rectangle area in Areas)
                if (area.Contains(x, y))
                    return true;

            return false;
        }

        public override void ClearWorld()
        {
            Inside = Point.Zero;
            Areas.Clear();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Inside != Point.Zero)
                tag[InsideKey] = new int[] { Inside.X, Inside.Y };

            if (Areas.Count > 0)
            {
                int[] all = new int[Areas.Count * 4];

                for (int i = 0; i < Areas.Count; i++)
                {
                    all[i * 4] = Areas[i].X;
                    all[i * 4 + 1] = Areas[i].Y;
                    all[i * 4 + 2] = Areas[i].Width;
                    all[i * 4 + 3] = Areas[i].Height;
                }

                tag[AreasKey] = all;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Inside = Point.Zero;
            Areas.Clear();

            if (tag.TryGet(InsideKey, out int[] at) && at.Length == 2)
                Inside = new Point(at[0], at[1]);

            if (tag.TryGet(AreasKey, out int[] all))
                for (int i = 0; i + 3 < all.Length; i += 4)
                    Areas.Add(new Rectangle(all[i], all[i + 1], all[i + 2], all[i + 3]));
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write((short)Areas.Count);

            foreach (Rectangle area in Areas)
            {
                writer.Write(area.X);
                writer.Write(area.Y);
                writer.Write(area.Width);
                writer.Write(area.Height);
            }
        }

        public override void NetReceive(BinaryReader reader)
        {
            Areas.Clear();
            int count = reader.ReadInt16();

            for (int i = 0; i < count; i++)
                Areas.Add(new Rectangle(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
        }

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            tasks.Add(new SludgePocketPass("PisAndDag: The Sump", 12.0));
            totalWeight += 12.0;
        }

        // ---- where ---------------------------------------------------------------------------------------------------

        /// <summary>The Jungle's span, from the caverns: the longest run of columns that are mostly mud. (0, 0) if there is none.</summary>
        public static (int Left, int Right) Jungle()
        {
            int top = Math.Max((int)Main.rockLayer, (int)Main.worldSurface + 10);
            int bottom = Main.UnderworldLayer - 10;

            if (bottom <= top + 20)
                return (0, 0);

            bool[] jungle = new bool[Main.maxTilesX];
            int acidic = ModContent.TileType<AcidicMud>(), stained = ModContent.TileType<StainedCobblestone>();

            for (int x = 40; x < Main.maxTilesX - 40; x++)
            {
                int mud = 0, solid = 0;

                for (int y = top; y < bottom; y += 2)
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile || !Main.tileSolid[tile.TileType])
                        continue;

                    solid++;

                    if (tile.TileType == TileID.Mud || TileID.Sets.Conversion.JungleGrass[tile.TileType] || tile.TileType == TileID.Hive
                        || tile.TileType == acidic || tile.TileType == stained)
                        mud++;
                }

                jungle[x] = solid > 0 && mud * 3 >= solid;
            }

            // The longest run, forgiving gaps of up to 30 columns (a cave, a temple, a strip of stone).
            int bestLeft = 0, bestRight = 0, runLeft = -1, lastIn = -1000;

            for (int x = 40; x < Main.maxTilesX - 40; x++)
            {
                if (!jungle[x])
                    continue;

                if (x - lastIn > 30)
                    runLeft = x;

                lastIn = x;

                if (x - runLeft > bestRight - bestLeft)
                {
                    bestLeft = runLeft;
                    bestRight = x;
                }
            }

            return bestRight - bestLeft > 150 ? (bestLeft, bestRight) : (0, 0);
        }

        /// <summary>What the ground holds at a cell, as the plan sorts it.</summary>
        private static Plan.Ground Read(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 20))
                return Plan.Ground.Kept;

            Tile tile = Main.tile[x, y];

            if (tile.WallType == WallID.LihzahrdBrickUnsafe)
                return Plan.Ground.Kept;

            if (!tile.HasTile)
                return Plan.Ground.Air;

            ushort type = tile.TileType;

            switch (type)
            {
                case TileID.LihzahrdBrick:
                case TileID.LihzahrdAltar:
                case TileID.Larva:
                case TileID.DemonAltar:
                case TileID.ShadowOrbs:
                    return Plan.Ground.Kept;

                // Plain chests are moved out of its way (Lay); anything else that holds things is kept.
                case TileID.Containers:
                case TileID.Containers2:
                    return Plan.Ground.Air;
            }

            if (Main.tileContainer[type])
                return Plan.Ground.Kept;

            // Anything another mod (or this one) set up whole, a cavern's works among them: never through it.
            if (type >= TileID.Count && Main.tileFrameImportant[type])
                return Plan.Ground.Kept;

            if (!Main.tileSolid[type] || Main.tileSolidTop[type])
                return Plan.Ground.Air;

            switch (type)
            {
                case TileID.Mud:
                case TileID.Dirt:
                case TileID.ClayBlock:
                case TileID.Silt:
                case TileID.Slush:
                case TileID.Sand:
                case TileID.Ash:
                case TileID.MushroomGrass:
                    return Plan.Ground.Soft;

                case TileID.Stone:
                case TileID.Ebonstone:
                case TileID.Crimstone:
                case TileID.Pearlstone:
                case TileID.Granite:
                case TileID.Marble:
                case TileID.Sandstone:
                case TileID.HardenedSand:
                    return Plan.Ground.Hard;
            }

            if (TileID.Sets.Conversion.JungleGrass[type] || TileID.Sets.Grass[type])
                return Plan.Ground.Soft;

            return type == ModContent.TileType<AcidicMud>() ? Plan.Ground.Soft
                : type == ModContent.TileType<StainedCobblestone>() ? Plan.Ground.Hard
                : Plan.Ground.Other;
        }

        // ---- making it ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Makes a cavern: under the Jungle's middle, at its floor, or centred on <paramref name="centre"/> if given
        /// (as wide as the Jungle asks either way; a cavern 300 wide if there is no Jungle to ask). False, and why in
        /// <paramref name="why"/>, if nowhere would take it.
        /// </summary>
        public static bool Make(UnifiedRandom rand, Point? centre, out string why)
        {
            (int left, int right) = Jungle();

            if (right == 0 && centre == null)
            {
                why = "no Jungle found";
                return false;
            }

            int cavernWide = right > 0 ? (right - left) * 2 / 3 : 300;
            cavernWide = Math.Min(cavernWide, Main.maxTilesX - 160 - 2 * Plan.MarginFor(cavernWide));
            int margin = Plan.MarginFor(cavernWide);
            int foot = Main.UnderworldLayer - AboveUnderworld;
            int roof = Math.Max((int)Main.rockLayer, (int)Main.worldSurface + 30);
            int cavernHigh = Math.Min(Plan.HighFor(cavernWide), foot - roof - 2 * margin);

            if (cavernWide < 100 || cavernHigh < 40)
            {
                why = $"no room for it ({cavernWide} by {cavernHigh})";
                return false;
            }

            int W = cavernWide + 2 * margin, H = cavernHigh + 2 * margin;
            List<Point> corners = new();

            if (centre is Point c)
            {
                corners.Add(new Point(Math.Clamp(c.X - W / 2, 45, Main.maxTilesX - 45 - W), Math.Clamp(c.Y - H / 2, roof, foot - H)));
            }
            else
            {
                int middle = (left + right) / 2 - W / 2;

                foreach (int rise in new[] { 0, 30, 60 })
                    foreach (int shift in new[] { 0, -40, 40, -80, 80 })
                        corners.Add(new Point(Math.Clamp(middle + shift, 45, Main.maxTilesX - 45 - W), Math.Max(roof, foot - H - rise)));
            }

            why = "nowhere would take it";

            foreach (Point corner in corners)
            {
                Plan.Pocket p = Plan.Make((x, y) => Read(corner.X + x, corner.Y + y), rand.Next(), cavernWide, cavernHigh, corner.X, corner.Y);

                if (!p.Fine)
                {
                    why = p.Why;
                    continue;
                }

                Lay(p, corner, rand);
                return true;
            }

            return false;
        }

        private static void Lay(Plan.Pocket p, Point corner, UnifiedRandom rand)
        {
            ushort mud = (ushort)ModContent.TileType<AcidicMud>();
            ushort cobble = (ushort)ModContent.TileType<StainedCobblestone>();

            // Chests where anything changes: taken up whole, to be set down again in the cavern.
            List<(ushort Type, int Style, string Name, Item[] Items)> moved = new();

            for (int i = 0; i < Main.chest.Length; i++)
            {
                Chest chest = Main.chest[i];

                if (chest == null || !Touched(p, corner, chest.x, chest.y, 2, 2))
                    continue;

                Tile at = Main.tile[chest.x, chest.y];

                if (!at.HasTile || at.TileType != TileID.Containers && at.TileType != TileID.Containers2)
                    continue;

                Item[] items = new Item[chest.item.Length];

                for (int k = 0; k < items.Length; k++)
                    items[k] = chest.item[k]?.Clone() ?? new Item();

                moved.Add((at.TileType, at.TileFrameX / 36, chest.name, items));

                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                        Main.tile[chest.x + dx, chest.y + dy].ClearTile();

                Main.chest[i] = null;
            }

            // Whatever stood whole where anything changes goes next (a pot, a plant, a crystal), so that no half of one is left.
            for (int y = 0; y < p.High; y++)
            {
                for (int x = 0; x < p.Wide; x++)
                {
                    if (p.Cells[x, y] == Plan.Cell.Same)
                        continue;

                    Tile tile = Main.tile[corner.X + x, corner.Y + y];

                    if (tile.HasTile && Main.tileFrameImportant[tile.TileType])
                        WorldGen.KillTile(corner.X + x, corner.Y + y, noItem: true);
                }
            }

            for (int y = 0; y < p.High; y++)
            {
                for (int x = 0; x < p.Wide; x++)
                {
                    Plan.Cell cell = p.Cells[x, y];

                    if (cell == Plan.Cell.Same)
                        continue;

                    int wx = corner.X + x, wy = corner.Y + y;
                    Tile tile = Main.tile[wx, wy];

                    switch (cell)
                    {
                        case Plan.Cell.Open:
                            tile.ClearTile();
                            tile.LiquidAmount = 0;
                            ToxicSludge.Unmark(wx, wy);
                            break;

                        case Plan.Cell.Mud:
                        case Plan.Cell.Cobble:
                            tile.ResetToType(cell == Plan.Cell.Mud ? mud : cobble);
                            ToxicSludge.Unmark(wx, wy);
                            break;

                        case Plan.Cell.Sludge:
                            tile.ClearTile();
                            tile.LiquidType = LiquidID.Water;
                            tile.LiquidAmount = 255;
                            ToxicSludge.Mark(wx, wy);
                            break;
                    }

                    if (tile.WallType == WallID.None && (cell == Plan.Cell.Open || cell == Plan.Cell.Sludge || Near(p, x, y)))
                        tile.WallType = WallID.MudUnsafe;
                }
            }

            int barrel = ModContent.TileType<RustedBarrel>(), pipe = ModContent.TileType<RustedPipe>(), vat = ModContent.TileType<SludgeVat>();

            foreach (Plan.Thing thing in p.Things)
            {
                if (thing.Kind == Plan.Kind.Chest)
                    continue;

                ushort type = (ushort)(thing.Kind == Plan.Kind.Barrel ? barrel : thing.Kind == Plan.Kind.Pipe ? pipe : vat);

                for (int cy = 0; cy < thing.Tall; cy++)
                {
                    for (int cx = 0; cx < thing.Wide; cx++)
                    {
                        Tile tile = Main.tile[corner.X + thing.X + cx, corner.Y + thing.Y + cy];
                        tile.HasTile = true;
                        tile.TileType = type;
                        tile.Slope = SlopeType.Solid;
                        tile.IsHalfBlock = false;
                        tile.TileFrameX = (short)(thing.Style * thing.Wide * 18 + cx * 18);
                        tile.TileFrameY = (short)(cy * 18);
                    }
                }
            }

            foreach (Plan.Thing thing in p.Things)
            {
                if (thing.Kind != Plan.Kind.Chest)
                    continue;

                int index = WorldGen.PlaceChest(corner.X + thing.X, corner.Y + thing.Y + 1, TileID.Containers, false, 5);

                if (index >= 0 && Main.chest[index] != null)
                    Stock(Main.chest[index], rand);
            }

            // The chests taken up, set down on the floor left free.
            int spare = 0;

            foreach (var (type, style, name, items) in moved)
            {
                int index = -1;

                while (index < 0 && spare < p.Spare.Count)
                {
                    var (sx, sy) = p.Spare[spare++];
                    index = WorldGen.PlaceChest(corner.X + sx, corner.Y + sy + 1, type, false, style);
                }

                if (index < 0 || Main.chest[index] == null)
                {
                    ModContent.GetInstance<PisAndDag>().Logger.Warn("The Sump: a chest in its way could not be set down again; what was in it is gone.");
                    continue;
                }

                Chest chest = Main.chest[index];
                chest.name = name ?? "";

                for (int k = 0; k < items.Length && k < chest.item.Length; k++)
                    chest.item[k] = items[k];
            }

            // The ways in, dug on from where the plan's canvas stopped them, until they open into a cave.
            Rectangle area = new(corner.X - 2, corner.Y - 2, p.Wide + 4, p.High + 4);

            foreach (var (x, y, ax, ay) in p.Ends)
                area = Rectangle.Union(area, Dig(corner.X + x, corner.Y + y, ax, ay, rand));

            // No lava anywhere about it (the ways in open into the caves round it, and lava run into the lake would
            // crust it over); then framed, a little past all it touched.
            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava)
                        tile.LiquidAmount = 0;

                    WorldGen.TileFrame(x, y, true, true);
                    Framing.WallFrame(x, y, true);
                }
            }

            if (p.Inside.X >= 0)
                Inside = new Point(corner.X + p.Inside.X, corner.Y + p.Inside.Y);

            Areas.Add(new Rectangle(corner.X + p.Left, corner.Y + p.Top, p.Right - p.Left + 1, p.Bottom - p.Top + 1));

            int lake = 0;

            for (int y = 0; y < p.High; y++)
                for (int x = 0; x < p.Wide; x++)
                    if (p.Cells[x, y] == Plan.Cell.Sludge)
                        lake++;

            ModContent.GetInstance<PisAndDag>().Logger.Info($"The Sump made: canvas at {corner.X}, {corner.Y}, {p.Wide} by {p.High}; cavern {Areas[^1]}; "
                + $"lake from row {corner.Y + p.Level}, {lake} cells; {ToxicSludge.Count} cells of sludge in the world; {moved.Count} chests moved.");

            if (Main.netMode == NetmodeID.Server && !WorldGen.generatingWorld)
            {
                const int Piece = 150, Rows = 16;

                for (int y = area.Top; y < area.Bottom; y += Rows)
                    for (int x = area.Left; x < area.Right; x += Piece)
                        NetMessage.SendTileSquare(-1, x, y, Math.Min(Piece, area.Right - x), Math.Min(Rows, area.Bottom - y));

                NetMessage.SendData(MessageID.WorldData);
            }
        }

        /// <summary>Whether anything changes under an object w by h at (x, y) in the world.</summary>
        private static bool Touched(Plan.Pocket p, Point corner, int x, int y, int w, int h)
        {
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    int cx = x + dx - corner.X, cy = y + dy - corner.Y;

                    if (cx >= 0 && cy >= 0 && cx < p.Wide && cy < p.High && p.Cells[cx, cy] != Plan.Cell.Same)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A way in, on from the canvas's edge: rising, wandering a little, five cells across, until it comes out into
        /// open ground (or meets something kept, or has gone a hundred and twenty cells). What it dug, as a rectangle.
        /// </summary>
        private static Rectangle Dig(int x, int y, float ax, float ay, UnifiedRandom rand)
        {
            float px = x, py = y;
            int left = x, right = x, top = y, bottom = y;

            for (int step = 0; step < 120; step++)
            {
                float turn = (rand.NextFloat() - 0.5f) * 0.45f;
                float nx = ax + turn * -ay, ny = ay + turn * ax;
                float length = (float)Math.Sqrt(nx * nx + ny * ny);
                ax = nx / length;
                ay = Math.Min(-0.25f, ny / length);
                px += ax;
                py += ay;

                int cx = (int)Math.Round(px), cy = (int)Math.Round(py);

                if (!WorldGen.InWorld(cx, cy, 30) || Read(cx, cy) == Plan.Ground.Kept)
                    break;

                // Out into a cave: most of what is about it already open.
                int open = 0;

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (Read(cx + dx, cy + dy) == Plan.Ground.Air)
                            open++;

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (dx * dx + dy * dy > 5 || Read(cx + dx, cy + dy) == Plan.Ground.Kept)
                            continue;

                        Tile tile = Main.tile[cx + dx, cy + dy];

                        if (tile.HasTile && (Main.tileFrameImportant[tile.TileType] || Main.tileContainer[tile.TileType]))
                            continue;

                        tile.ClearTile();
                    }
                }

                left = Math.Min(left, cx);
                right = Math.Max(right, cx);
                top = Math.Min(top, cy);
                bottom = Math.Max(bottom, cy);

                if (open >= 6 && step > 1)
                    break;
            }

            return new Rectangle(left - 4, top - 4, right - left + 9, bottom - top + 9);
        }

        /// <summary>Whether a shell or halftone cell is near the cavern (within the shell's depth), so that it has a wall behind it too.</summary>
        private static bool Near(Plan.Pocket p, int x, int y)
        {
            for (int dy = -Plan.ShellDepth; dy <= Plan.ShellDepth; dy += Plan.ShellDepth)
            {
                for (int dx = -Plan.ShellDepth; dx <= Plan.ShellDepth; dx += Plan.ShellDepth)
                {
                    int nx = x + dx, ny = y + dy;

                    if (nx >= 0 && ny >= 0 && nx < p.Wide && ny < p.High && (p.Cells[nx, ny] == Plan.Cell.Open || p.Cells[nx, ny] == Plan.Cell.Sludge))
                        return true;
                }
            }

            return false;
        }

        /// <summary>What a shut barrel holds: what a worker left, and what the Jungle put in after.</summary>
        private static void Stock(Chest chest, UnifiedRandom rand)
        {
            List<(int Type, int Stack)> stock = new()
            {
                (ItemID.HealingPotion, rand.Next(2, 5)),
                (ItemID.GillsPotion, rand.Next(1, 3)),
                (ItemID.Wire, rand.Next(20, 61)),
                (ItemID.Bomb, rand.Next(3, 9)),
                (ItemID.JungleSpores, rand.Next(6, 15)),
                (ItemID.GoldCoin, rand.Next(2, 6)),
            };

            if (rand.NextBool(3))
                stock.Insert(0, (ItemID.Bezoar, 1));

            if (rand.NextBool(2))
                stock.Add((ItemID.Wrench, 1));

            for (int i = 0; i < stock.Count && i < chest.item.Length; i++)
            {
                chest.item[i].SetDefaults(stock[i].Type);
                chest.item[i].stack = Math.Min(stock[i].Stack, chest.item[i].maxStack);
            }
        }
    }

    /// <summary>The worldgen pass for it: last of all.</summary>
    public class SludgePocketPass : GenPass
    {
        public SludgePocketPass(string name, double weight) : base(name, weight)
        {
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Something settles under the Jungle";

            if (!SludgePocket.Make(WorldGen.genRand, null, out string why))
                ModContent.GetInstance<PisAndDag>().Logger.Info("The Sump was not made: " + why);
        }
    }
}
