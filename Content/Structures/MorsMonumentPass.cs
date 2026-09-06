using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles.Misc;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Slots the Mors Monument into vanilla worldgen.
    ///
    /// It runs immediately before Final Cleanup — as late as a pass reasonably can.
    /// Three reasons: the spawn point has definitely been chosen by then, so the monument
    /// can be centred under it; every ruined house exists already, so demolition catches
    /// all of them; and nothing is left to run that could flood the hill back with lava.
    /// </summary>
    public class MorsMonumentSystem : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int index = tasks.FindIndex(pass => pass.Name.Equals("Final Cleanup"));

            // Appending is a fine fallback: worse only in that a later modded pass could
            // run first, which beats not generating at all.
            if (index == -1)
                index = tasks.Count;

            tasks.Insert(index, new MorsMonumentPass("PisAndDag: Mors Monument", 120.0));
        }
    }

    /// <summary>
    /// Builds the ash hill in the Underworld directly below the world spawn: a wide, low
    /// mound 300 tiles to either side of the blade, sloping down into the hell floor at
    /// its edges, solid to bedrock under the middle, with the Blade of Mors driven into a
    /// flat tabletop at the centre and no ruined houses anywhere near it.
    /// </summary>
    public class MorsMonumentPass : GenPass
    {
        /// <summary>Half-width of the hill. The base spans Radius tiles either side.</summary>
        private const int Radius = 300;

        /// <summary>
        /// The tallest the plateau is allowed to stand above the hell floor. The peak
        /// actually used is fitted to the cave the hill sits in and is usually lower —
        /// see <see cref="FitPeak"/>.
        /// </summary>
        private const int MaxPeak = 34;

        /// <summary>Floor on that fitting, so the hill never flattens into a plain.</summary>
        private const int MinPeak = 14;

        /// <summary>
        /// Air left between the tabletop and whatever hangs above it. The hill is made
        /// short enough to leave this gap rather than the ceiling being cut away to make
        /// room — carving produces an obvious bubble around the monument.
        /// </summary>
        private const int CeilingGap = 60;

        /// <summary>
        /// Fraction of the radius that stays perfectly flat. At 0.045 with Radius 300 the
        /// tabletop is about 27 tiles wide — room for the blade and a field around it.
        /// </summary>
        private const float FlatFraction = 0.045f;

        /// <summary>Headroom cleared over the tabletop, tapering to nothing down the slopes.</summary>
        private const int PlateauClearance = 16;

        /// <summary>Thinnest the ash skin gets, out at the hill's feet.</summary>
        private const int EdgeSkin = 7;

        /// <summary>How far past the hill's foot lava is drained, in tiles.</summary>
        private const int DrainMargin = 16;

        /// <summary>Radius, in tiles, inside which every ruined house is removed.</summary>
        private const int DemolishRadius = 300;

        /// <summary>How far the fireblossoms spread from the blade.</summary>
        private const int FieldRadius = 45;

        /// <summary>Columns either side of centre kept clear of plants for the sword.</summary>
        private const int SwordClearance = 2;

        /// <summary>
        /// Everything that belongs in the Underworld naturally. Demolition works as a
        /// whitelist rather than a list of house parts: a ruined house is bricks, walls,
        /// a forge, doors, platforms, chairs, tables, chests and lamps, and naming all of
        /// those is both long and easy to leave a hole in. Naming what stays is short and
        /// cannot miss anything.
        /// </summary>
        private static readonly HashSet<int> NaturalUnderworld = new HashSet<int>
        {
            TileID.Ash,
            TileID.Hellstone,
            TileID.Obsidian,
            TileID.ImmatureHerbs,
            TileID.MatureHerbs,
            TileID.BloomingHerbs
        };

        public MorsMonumentPass(string name, double loadWeight) : base(name, loadWeight)
        {
        }

        /// <summary>The ash body runs to here — a few rows short of the world's edge.</summary>
        private static int BottomFill => Main.maxTilesY - 3;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Something is driven into the ash";

            int centerX = Math.Clamp(Main.spawnTileX, Radius + DrainMargin + 10,
                Main.maxTilesX - Radius - DrainMargin - 10);

            int baseY = FindHellFloor(centerX);
            float[] shape = BuildShape();
            int peak = FitPeak(centerX, baseY);

            progress.Set(0.1f);
            Demolish(centerX, baseY - peak);

            progress.Set(0.3f);
            DrainAndDeObsidian(centerX, baseY, peak);

            progress.Set(0.5f);
            int[] surface = RaiseHill(centerX, baseY, shape, peak);

            progress.Set(0.85f);
            BuryHellstone(centerX, baseY);

            progress.Set(0.94f);
            PlaceBlade(centerX, surface[Radius]);
            PlantField(centerX, surface);

            Rectangle area = new Rectangle(
                centerX - Radius,
                surface[Radius] - PlateauClearance,
                Radius * 2 + 1,
                BottomFill - surface[Radius] + PlateauClearance);

            GenVars.structures?.AddProtectedStructure(area, 4);

            progress.Set(1f);
        }

        /// <summary>
        /// The line the hill is built up from.
        ///
        /// Averaged across the middle of the footprint rather than sampled at one column:
        /// the hell floor is uneven, and a single freak pit or spire under spawn would
        /// otherwise decide where the whole monument sits.
        /// </summary>
        private static int FindHellFloor(int centerX)
        {
            long sum = 0;
            int samples = 0;

            for (int x = centerX - 60; x <= centerX + 60; x += 4)
            {
                int floor = NaturalFloor(x);
                if (floor < 0)
                    continue;

                sum += floor;
                samples++;
            }

            // Every column was open lava or void — pick a sane depth rather than bail.
            if (samples == 0)
                return Main.maxTilesY - 110;

            return Math.Clamp((int)(sum / samples), (int)Main.UnderworldLayer + 30, Main.maxTilesY - 70);
        }

        /// <summary>First solid tile of the hell floor in this column, or -1.</summary>
        private static int NaturalFloor(int x)
        {
            if (!WorldGen.InWorld(x, 10))
                return -1;

            int top = (int)Main.UnderworldLayer + 12;
            int bottom = Main.maxTilesY - 48;

            for (int y = top; y < bottom; y++)
            {
                if (WorldGen.SolidTile(x, y))
                    return y;
            }

            return -1;
        }

        /// <summary>
        /// How tall the mound may stand here.
        ///
        /// This is the answer to "make it lower": rather than building to a fixed height
        /// and then cutting the ceiling away to make room, measure the cave the monument
        /// has landed in and build something short enough to sit in it. The gap over the
        /// blade is then real open Underworld, not a bubble carved around the hill.
        ///
        /// The narrowest headroom across the tabletop wins, so a single stalactite is
        /// enough to keep the hill down.
        /// </summary>
        private static int FitPeak(int centerX, int baseY)
        {
            int flatHalf = (int)(Radius * FlatFraction);
            int lowest = int.MaxValue;

            for (int x = centerX - flatHalf; x <= centerX + flatHalf; x += 3)
            {
                if (!WorldGen.InWorld(x, 10))
                    continue;

                int gap = baseY - (int)Main.UnderworldLayer;

                for (int y = baseY - 1; y > (int)Main.UnderworldLayer; y--)
                {
                    if (!WorldGen.SolidTile(x, y))
                        continue;

                    gap = baseY - y;
                    break;
                }

                if (gap < lowest)
                    lowest = gap;
            }

            if (lowest == int.MaxValue)
                lowest = MaxPeak + CeilingGap;

            return Math.Clamp(lowest - CeilingGap, MinPeak, MaxPeak);
        }

        /// <summary>
        /// The mound's silhouette as a 0-1 value per column: 1 on the tabletop, 0 out at
        /// the feet.
        ///
        /// Past the flat middle it is half a straight ramp and half a raised cosine. Pure
        /// cosine bulges and reads as a dome; pure linear plants a hard crease where the
        /// slope meets the plateau and again where it meets the floor. The blend keeps a
        /// long even slant and softens both joins.
        ///
        /// The same curve drives three things — height, how deep the ash runs, and how
        /// much headroom is cleared — so all three taper together and the hill has no
        /// vertical edge anywhere.
        /// </summary>
        private static float[] BuildShape()
        {
            float[] shape = new float[Radius * 2 + 1];

            for (int i = 0; i < shape.Length; i++)
            {
                float t = Math.Abs(i - Radius) / (float)Radius;

                if (t <= FlatFraction)
                {
                    shape[i] = 1f;
                    continue;
                }

                float u = (t - FlatFraction) / (1f - FlatFraction);
                float straight = 1f - u;
                float eased = 0.5f + 0.5f * MathF.Cos(u * MathHelper.Pi);

                shape[i] = 0.5f * straight + 0.5f * eased;
            }

            return shape;
        }

        /// <summary>
        /// Clears every ruined house within DemolishRadius of the blade.
        ///
        /// Distance is measured from the blade itself and confined to the Underworld, so
        /// the cavern layer above keeps its own cabins. Chest records are dropped
        /// separately: removing a chest's tiles without clearing its entry in Main.chest
        /// leaves an invisible container behind.
        /// </summary>
        private static void Demolish(int centerX, int centerY)
        {
            int left = Math.Max(10, centerX - DemolishRadius);
            int right = Math.Min(Main.maxTilesX - 10, centerX + DemolishRadius);
            int top = (int)Main.UnderworldLayer;
            int bottom = Main.maxTilesY - 3;

            int swordTile = ModContent.TileType<BladeOfMorsInStone>();
            int radiusSquared = DemolishRadius * DemolishRadius;

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;

                    if (dx * dx + dy * dy > radiusSquared)
                        continue;

                    Tile tile = Main.tile[x, y];

                    // The Underworld has no natural background wall, so anything here is
                    // part of a house.
                    tile.WallType = 0;

                    if (!tile.HasTile)
                        continue;

                    if (tile.TileType == swordTile || NaturalUnderworld.Contains(tile.TileType))
                        continue;

                    tile.HasTile = false;
                    tile.TileType = 0;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                }
            }

            for (int c = 0; c < Main.maxChests; c++)
            {
                Chest chest = Main.chest[c];
                if (chest == null)
                    continue;

                int dx = chest.x - centerX;
                int dy = chest.y - centerY;

                if (chest.y >= top && dx * dx + dy * dy <= radiusSquared)
                    Main.chest[c] = null;
            }
        }

        /// <summary>
        /// Empties every liquid over the hill's footprint and turns any obsidian it finds
        /// back into ash.
        ///
        /// The margin is deliberately narrow now that the mound is 600 tiles across:
        /// lava lying at the foot of the slopes is Underworld scenery, and draining half
        /// of hell to be safe would cost more atmosphere than it buys.
        /// </summary>
        private static void DrainAndDeObsidian(int centerX, int baseY, int peak)
        {
            int left = centerX - Radius - DrainMargin;
            int right = centerX + Radius + DrainMargin;
            int top = baseY - peak - PlateauClearance;

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= BottomFill; y++)
                {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];
                    tile.LiquidAmount = 0;

                    if (tile.HasTile && tile.TileType == TileID.Obsidian)
                        tile.TileType = TileID.Ash;
                }
            }
        }

        /// <summary>
        /// Stamps the mound. Returns the surface line it actually produced, column by
        /// column, so the blade and the flowers can be put on the real ground rather than
        /// on where the maths said the ground would be.
        ///
        /// Two rules keep this from reading as a block dropped into hell. Off the
        /// tabletop the hill only ever builds UP — where the natural floor already stands
        /// higher than the mound would, the floor is left alone and simply skinned in ash,
        /// so the slopes grow out of the terrain instead of cutting a trench through it.
        /// And the depth of the fill tapers with the same curve as the height, so the ash
        /// is solid to bedrock under the middle and a thin crust at the feet, never a
        /// column with sheer sides.
        /// </summary>
        private static int[] RaiseHill(int centerX, int baseY, float[] shape, int peak)
        {
            int[] surface = new int[shape.Length];
            int fullDepth = BottomFill - baseY;

            for (int i = 0; i < shape.Length; i++)
            {
                int x = centerX - Radius + i;
                surface[i] = baseY;

                if (!WorldGen.InWorld(x, 10))
                    continue;

                bool plateau = shape[i] >= 1f;
                int raised = baseY - (int)MathF.Round(peak * shape[i]);

                // Roughen the slopes so the mound does not read as a formula. The
                // tabletop is deliberately left untouched — it has to stay dead flat.
                if (!plateau && shape[i] > 0.02f)
                    raised += WorldGen.genRand.Next(-1, 2);

                int top = raised;

                if (!plateau)
                {
                    int natural = NaturalFloor(x);

                    // Smaller Y is higher, so Min keeps whichever of the two stands
                    // taller — the mound where it rises, the existing floor elsewhere.
                    if (natural > 0 && natural < top)
                        top = natural;
                }

                int depth = Math.Max(EdgeSkin, (int)(fullDepth * shape[i]));
                int bottom = Math.Min(BottomFill, top + depth);

                for (int y = top; y <= bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = TileID.Ash;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.LiquidAmount = 0;
                }

                // Only the tabletop gets real headroom, and even that is a fraction of
                // what the hill is tall. Down the slopes it fades to nothing, so the
                // Underworld closes back in naturally instead of the monument sitting
                // inside a carved pocket.
                int clearance = plateau
                    ? PlateauClearance
                    : (int)(PlateauClearance * shape[i] * shape[i]);

                for (int y = top - clearance; y < top; y++)
                {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];
                    tile.HasTile = false;
                    tile.TileType = 0;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.LiquidAmount = 0;
                }

                surface[i] = top;
            }

            return surface;
        }

        /// <summary>
        /// One continuous seam of hellstone inside the hill.
        ///
        /// Built as a short random walk of overlapping discs rather than with
        /// WorldGen.TileRunner: a runner wanders, thins out and can break into islands,
        /// whereas stepping less far than the disc's own radius guarantees every lobe
        /// touches the last one. Chunky and connected, but a different shape each world.
        /// </summary>
        private static void BuryHellstone(int centerX, int baseY)
        {
            int lobes = 6 + WorldGen.genRand.Next(5);

            int x = centerX + WorldGen.genRand.Next(-8, 9);
            int y = baseY + 18;

            for (int i = 0; i < lobes; i++)
            {
                int radius = 6 + WorldGen.genRand.Next(6);
                StampOre(x, y, radius);

                // Step shorter than the radius, so consecutive lobes always overlap.
                double angle = WorldGen.genRand.NextDouble() * Math.PI * 2.0;
                int step = radius - 2;

                x = Math.Clamp(x + (int)(Math.Cos(angle) * step), centerX - 42, centerX + 42);
                y = Math.Clamp(y + (int)(Math.Sin(angle) * step), baseY + 10, baseY + 62);
            }
        }

        /// <summary>
        /// One lobe. Only ash is converted, which is what keeps the seam inside the
        /// hill: it can never spill into open air or eat the surrounding hell rock.
        /// </summary>
        private static void StampOre(int cx, int cy, int radius)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                for (int y = cy - radius; y <= cy + radius; y++)
                {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    int dx = x - cx;
                    int dy = y - cy;

                    // Squashed vertically, so the chunk reads as a seam rather than a ball.
                    if (dx * dx + (int)(dy * dy * 1.35f) > radius * radius)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || tile.TileType != TileID.Ash)
                        continue;

                    tile.TileType = TileID.Hellstone;
                }
            }
        }

        /// <summary>Drives the blade into the middle of the tabletop.</summary>
        private static void PlaceBlade(int centerX, int plateauTop)
        {
            // plateauTop is the topmost ash tile, so the rock stands on the tile above it.
            WorldGen.PlaceObject(centerX, plateauTop - 1, ModContent.TileType<BladeOfMorsInStone>());

            if (Main.tile[centerX, plateauTop - 1].HasTile)
                return;

            ModContent.GetInstance<PisAndDag>()?.Logger
                .Warn($"Mors Monument built at {centerX}, {plateauTop} but the blade would not place.");
        }

        /// <summary>
        /// Scatters fireblossoms around the blade — some fully grown, some still
        /// seedlings. Kept to FieldRadius rather than the whole mound: 600 tiles of
        /// flowers is a lawn, not a shrine.
        ///
        /// The tile ID and style come from the seed item rather than being named here.
        /// All seven herbs share three tile IDs and differ only by an undocumented frame
        /// number, so asking ItemID.FireblossomSeeds what it places is the only way to be
        /// certain of getting fireblossom and not, say, shiverthorn.
        /// </summary>
        private static void PlantField(int centerX, int[] surface)
        {
            Item seed = ContentSamples.ItemsByType[ItemID.FireblossomSeeds];
            int herbTile = seed.createTile;
            int herbStyle = seed.placeStyle;

            if (herbTile <= 0)
                return;

            for (int dx = -FieldRadius; dx <= FieldRadius; dx++)
            {
                if (Math.Abs(dx) <= SwordClearance)
                    continue;

                int x = centerX + dx;
                int plantY = surface[Radius + dx] - 1;

                if (!WorldGen.InWorld(x, plantY))
                    continue;

                if (Main.tile[x, plantY].HasTile)
                    continue;

                // Thins out toward the edge of the field, so it fades rather than stopping
                // at a hard circle.
                float density = 0.75f - 0.5f * (Math.Abs(dx) / (float)FieldRadius);
                if (WorldGen.genRand.NextFloat() > density)
                    continue;

                WorldGen.PlaceTile(x, plantY, herbTile, true, false, -1, herbStyle);

                Tile tile = Main.tile[x, plantY];

                // PlaceTile enforces vanilla's own planting rules and can decline. Ash is
                // valid ground for fireblossom, but write the tile directly if it ever
                // refuses rather than silently ending up with a bare hilltop.
                if (!tile.HasTile)
                {
                    tile.HasTile = true;
                    tile.TileType = (ushort)herbTile;
                    tile.TileFrameX = (short)(herbStyle * 18);
                    tile.TileFrameY = 0;
                }

                // Two in three have grown in; the rest are left as the planted seed.
                // All three herb tiles share the same frame layout, so promoting one is
                // just a change of tile ID.
                if (!WorldGen.genRand.NextBool(3))
                    tile.TileType = TileID.MatureHerbs;
            }
        }
    }
}
