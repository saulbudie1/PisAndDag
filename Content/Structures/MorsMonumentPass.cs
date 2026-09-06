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
    /// Builds the ash hill in the Underworld directly below the world spawn: a low, broad
    /// mound 300 tiles to either side of the blade, set about halfway down the layer, ash
    /// grass over the whole surface, solid to bedrock, with the Blade of Mors driven into
    /// a flat tabletop on top.
    /// </summary>
    public class MorsMonumentPass : GenPass
    {
        /// <summary>Half-width of the hill. The base spans Radius tiles either side.</summary>
        private const int Radius = 300;

        /// <summary>
        /// Where the tabletop sits, as a fraction of the Underworld's depth: 0 is the
        /// layer's ceiling, 1 is the bottom of the world. 0.47 puts it a little under
        /// halfway down — about 94 tiles below the ceiling in a standard 200-tile
        /// Underworld.
        ///
        /// This is measured from the layer itself, NOT found by scanning for the hell
        /// floor. Two separate attempts at that scan picked out a rock ceiling or a
        /// stalactite instead of ground and pinned the monument near the roof, and no
        /// amount of shortening the mound could fix a bad starting line. Deriving the
        /// height from the layer bounds cannot be fooled by terrain, and this constant
        /// is the one knob for "higher" or "lower".
        /// </summary>
        private const float PlateauDepth = 0.47f;

        /// <summary>How far the tabletop stands above the mound's feet.</summary>
        private const int Peak = 12;

        /// <summary>
        /// Fraction of the radius that stays perfectly flat. At 0.045 with Radius 300 the
        /// tabletop is about 27 tiles wide — room for the blade and a field around it.
        /// </summary>
        private const float FlatFraction = 0.045f;

        /// <summary>How far the slopes wander from the ideal curve, in tiles.</summary>
        private const float Roughness = 2f;

        /// <summary>
        /// Distance between the roughness curve's control points. Large on purpose: this
        /// is the difference between a hill with gentle rises in it and a 600-tile
        /// sawtooth.
        /// </summary>
        private const int RoughnessSpacing = 26;

        /// <summary>
        /// How far above the mound's surface rock is stripped away.
        ///
        /// This is NOT a carved chamber, and it must stay small. The tabletop now sits
        /// well down inside the Underworld, so there is real hell cavern above it; this
        /// only knocks off whatever happens to be resting on the new surface.
        /// </summary>
        private const int Clearance = 16;

        /// <summary>
        /// Open air standing over the tabletop once the vault is cut.
        ///
        /// Lowering the mound left the Underworld's original floor hanging above it in
        /// disconnected lumps — the junk. Clearing a proper chamber removes all of it and
        /// is what makes the monument feel like it sits in a hall rather than a trench.
        /// </summary>
        private const int VaultHeight = 70;

        /// <summary>
        /// Rows of the Underworld's own roof never cut into, measured from the top of the
        /// layer. Keeps the vault from opening into the Cavern layer above.
        /// </summary>
        private const int RoofKeep = 34;

        /// <summary>
        /// Columns at each end over which the mound is feathered into hell's own ground,
        /// so it ends in a slope instead of a cut face.
        /// </summary>
        private const int EdgeBlend = 50;

        /// <summary>How far past the hill's foot lava is drained, in tiles.</summary>
        private const int DrainMargin = 16;

        /// <summary>Radius, in tiles, inside which every ruined house is removed.</summary>
        private const int DemolishRadius = 300;

        /// <summary>How far the fireblossoms spread from the blade.</summary>
        private const int FieldRadius = 90;

        /// <summary>How many ash trees stand around the blade.</summary>
        private const int TreeMinCount = 5;
        private const int TreeMaxCount = 9;

        /// <summary>
        /// Gap between one tree and the next, in tiles. Wide enough that the hilltop
        /// reads as scattered growth rather than a planted row.
        /// </summary>
        private const int TreeGapMin = 7;
        private const int TreeGapMax = 13;

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

            // The tabletop is placed by proportion of the Underworld's depth; the feet
            // then sit Peak below it. Nothing here looks at the terrain, which is the
            // whole point — see PlateauDepth.
            int hellTop = (int)Main.UnderworldLayer;
            int hellBottom = Main.maxTilesY;

            int plateauY = hellTop + (int)((hellBottom - hellTop) * PlateauDepth);
            plateauY = Math.Clamp(plateauY, hellTop + 40, BottomFill - 40);

            int baseY = plateauY + Peak;

            float[] shape = BuildShape();
            int[] surface = BuildSurface(centerX, baseY, shape);
            int[] ceiling = BuildCeiling(surface, shape, hellTop);

            progress.Set(0.1f);
            Demolish(centerX, surface[Radius]);

            progress.Set(0.3f);
            DrainAndDeObsidian(centerX, baseY);

            progress.Set(0.5f);
            Stamp(centerX, surface, ceiling);

            progress.Set(0.75f);
            GrassOver(centerX, surface);
            SlopeSurface(centerX, surface);

            progress.Set(0.88f);
            BuryHellstone(centerX, baseY);

            progress.Set(0.94f);
            PlaceBlade(centerX, surface[Radius]);

            // Trees first: they need a clear column, and a herb already standing there
            // would block one. The flowers then fill in around whatever took root.
            PlantTrees(centerX, surface);
            PlantField(centerX, surface);

            Rectangle area = new Rectangle(
                centerX - Radius,
                surface[Radius] - VaultHeight,
                Radius * 2 + 1,
                BottomFill - surface[Radius] + VaultHeight);

            GenVars.structures?.AddProtectedStructure(area, 4);

            progress.Set(1f);
        }

        /// <summary>
        /// The mound's silhouette as a 0-1 value per column: 1 on the tabletop, 0 out at
        /// the feet.
        ///
        /// Past the flat middle it is half a straight ramp and half a raised cosine. Pure
        /// cosine bulges and reads as a dome; pure linear plants a hard crease where the
        /// slope meets the plateau and again where it meets the floor. The blend keeps a
        /// long even slant and softens both joins.
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
        /// The finished surface line, one Y per column.
        ///
        /// Roughness is smooth noise, not per-column randomness. Rolling a fresh
        /// Next(-1, 2) for every column is white noise: neighbouring columns are
        /// unrelated, so the surface alternates up and down every tile and comes out as a
        /// 600-tile sawtooth. Instead a random offset is drawn every RoughnessSpacing
        /// columns and interpolated between with smoothstep, then the whole line is box
        /// blurred twice.
        ///
        /// The noise fades out approaching the tabletop and the tabletop is then forced
        /// level, because the blade needs somewhere flat to stand.
        /// </summary>
        private static int[] BuildSurface(int centerX, int baseY, float[] shape)
        {
            int count = shape.Length;
            float[] noise = NoiseLine(count, RoughnessSpacing);
            float[] line = new float[count];

            for (int i = 0; i < count; i++)
            {
                // 0 across the tabletop, full a little way down the slope.
                float fade = Math.Clamp((1f - shape[i]) * 4f, 0f, 1f);
                line[i] = baseY - Peak * shape[i] + Roughness * noise[i] * fade;
            }

            // Feather the outermost columns into whatever the Underworld is already doing
            // there. Without this the fill simply stops at Radius and leaves a cut face
            // where the ash meets hell's own ground.
            //
            // Note this is NOT the Min() blend that broke an earlier version. That applied
            // across the whole mound, and since the hell floor sits near the mound's own
            // height it won everywhere and flattened the hill. This runs only over the
            // last EdgeBlend columns, where blending is the entire point.
            for (int i = 0; i < count; i++)
            {
                int fromEdge = Math.Min(i, count - 1 - i);
                if (fromEdge >= EdgeBlend)
                    continue;

                int ground = NaturalGround(centerX - Radius + i, baseY);
                if (ground < 0)
                    continue;

                float t = 1f - fromEdge / (float)EdgeBlend;
                t = t * t * (3f - 2f * t);

                line[i] = MathHelper.Lerp(line[i], ground, t);
            }

            line = Smooth(line);
            line = Smooth(line);

            int[] surface = new int[count];
            for (int i = 0; i < count; i++)
            {
                surface[i] = shape[i] >= 1f
                    ? baseY - Peak
                    : (int)MathF.Round(line[i]);
            }

            return surface;
        }

        /// <summary>
        /// The roof cut over the mound, one Y per column.
        ///
        /// Raised to a power well below 1 rather than used straight: the hill's own curve
        /// falls away too quickly, and a vault following it would hug the mound and read
        /// as a bubble. This keeps the chamber near full height most of the way out and
        /// only closes it down near the feet, which is what makes the space feel wide.
        /// </summary>
        private static int[] BuildCeiling(int[] surface, float[] shape, int hellTop)
        {
            int count = shape.Length;
            float[] noise = NoiseLine(count, RoughnessSpacing * 2);
            int[] ceiling = new int[count];
            int roof = hellTop + RoofKeep;

            for (int i = 0; i < count; i++)
            {
                float openness = MathF.Pow(shape[i], 0.45f);
                float height = Clearance + (VaultHeight - Clearance) * openness + noise[i] * 5f;

                // Never cut into the layer's own roof, whatever the arithmetic says.
                ceiling[i] = Math.Max(roof, surface[i] - (int)height);
            }

            return ceiling;
        }

        /// <summary>
        /// Smooth noise in the range -1..1: a random value every `spacing` columns,
        /// smoothstepped between. A fresh roll per column would be white noise and comes
        /// out as a sawtooth.
        /// </summary>
        private static float[] NoiseLine(int count, int spacing)
        {
            int controlCount = count / spacing + 2;
            float[] control = new float[controlCount];
            for (int c = 0; c < controlCount; c++)
                control[c] = WorldGen.genRand.NextFloat() * 2f - 1f;

            float[] line = new float[count];
            for (int i = 0; i < count; i++)
            {
                float position = i / (float)spacing;
                int index = (int)position;
                float blend = position - index;
                blend = blend * blend * (3f - 2f * blend);

                line[i] = MathHelper.Lerp(control[index], control[index + 1], blend);
            }

            return line;
        }

        /// <summary>
        /// The top of hell's own ground in this column, or -1.
        ///
        /// The search starts just above the monument's own level, NOT at the top of the
        /// layer. The Underworld opens with a band of solid rock, so scanning from
        /// Main.UnderworldLayer returns the ROOF — the mistake that kept the monument
        /// pinned near the ceiling for several versions. Three solid tiles in a row are
        /// required so a stalactite is not mistaken for ground.
        /// </summary>
        private static int NaturalGround(int x, int baseY)
        {
            if (!WorldGen.InWorld(x, 10))
                return -1;

            int bottom = Main.maxTilesY - 48;
            int y = Math.Max((int)Main.UnderworldLayer + 30, baseY - 60);

            while (y < bottom && WorldGen.SolidTile(x, y))
                y++;

            while (y < bottom - 2)
            {
                if (WorldGen.SolidTile(x, y) && WorldGen.SolidTile(x, y + 1) && WorldGen.SolidTile(x, y + 2))
                    return y;

                y++;
            }

            return -1;
        }

        /// <summary>Five-tap box blur, clamped at the ends.</summary>
        private static float[] Smooth(float[] source)
        {
            float[] result = new float[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                float sum = 0f;
                int taken = 0;

                for (int k = -2; k <= 2; k++)
                {
                    int j = i + k;
                    if (j < 0 || j >= source.Length)
                        continue;

                    sum += source[j];
                    taken++;
                }

                result[i] = sum / taken;
            }

            return result;
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
        private static void DrainAndDeObsidian(int centerX, int baseY)
        {
            int left = centerX - Radius - DrainMargin;
            int right = centerX + Radius + DrainMargin;
            int top = baseY - Peak - VaultHeight;

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
        /// Lays the ash down.
        ///
        /// The surface line drives the TOP of the fill and nothing else. Two earlier
        /// versions got this wrong in ways worth recording:
        ///
        ///   - Tapering the BOTTOM of the fill as well put the only visible curve
        ///     underground, bulging downward, which reads as the hill upside down.
        ///   - Blending the top against the natural hell floor with Min() meant the floor
        ///     won almost everywhere, because it already sits near the mound's own height.
        ///     That flattened the surface and left, again, only the curved underside.
        /// </summary>
        private static void Stamp(int centerX, int[] surface, int[] ceiling)
        {
            for (int i = 0; i < surface.Length; i++)
            {
                int x = centerX - Radius + i;
                if (!WorldGen.InWorld(x, 10))
                    continue;

                int top = surface[i];

                for (int y = top; y <= BottomFill; y++)
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

                for (int y = ceiling[i]; y < top; y++)
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
            }
        }

        /// <summary>
        /// Turns the exposed ash into ash grass, the whole way across the mound.
        ///
        /// The tile is resolved from the seed item rather than named directly, the same
        /// trick used for the fireblossoms: ItemID.AshGrassSeeds already knows which tile
        /// it grows, so there is no ID to get wrong. If a future version stops carrying
        /// one, the hill stays bare ash rather than the pass throwing.
        /// </summary>
        private static void GrassOver(int centerX, int[] surface)
        {
            int grass = ContentSamples.ItemsByType[ItemID.AshGrassSeeds].createTile;

            if (grass <= 0)
            {
                ModContent.GetInstance<PisAndDag>()?.Logger
                    .Warn("Ash grass seeds report no tile to place; the Mors Monument is bare ash.");
                return;
            }

            for (int i = 0; i < surface.Length; i++)
            {
                int x = centerX - Radius + i;
                int y = surface[i];

                if (!WorldGen.InWorld(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.HasTile && tile.TileType == TileID.Ash)
                    tile.TileType = (ushort)grass;
            }
        }

        /// <summary>
        /// Slopes the single-tile steps in the surface so the mound reads as terrain
        /// rather than a staircase of full blocks.
        ///
        /// Only steps of exactly one tile are sloped. Anything taller is a cliff, and
        /// slicing its top block just cuts a notch out of the face. The tabletop and the
        /// ground under the blade are skipped — the sword anchors to three solid tiles and
        /// will refuse to place on a slope.
        /// </summary>
        private static void SlopeSurface(int centerX, int[] surface)
        {
            for (int i = 1; i < surface.Length - 1; i++)
            {
                int x = centerX - Radius + i;

                if (!WorldGen.InWorld(x, 10))
                    continue;

                if (Math.Abs(x - centerX) <= SwordClearance + 1)
                    continue;

                int y = surface[i];
                int left = surface[i - 1];
                int right = surface[i + 1];

                // Terraria slope 1 cuts the top-right corner, so the surface runs down to
                // the right; slope 2 cuts the top-left and runs down to the left.
                if (right == y + 1 && left <= y)
                    WorldGen.SlopeTile(x, y, 1, false);
                else if (left == y + 1 && right <= y)
                    WorldGen.SlopeTile(x, y, 2, false);
            }
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
        /// hill: it can never spill into open air, eat the surrounding hell rock, or
        /// strip the grass off the surface.
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
        /// Scatters a handful of ash trees over the hilltop.
        ///
        /// Placement alternates sides and walks outward from the blade in gaps of
        /// TreeGapMin..TreeGapMax, so the trees spread away from the monument without
        /// ever landing in a neat row or clumping into a thicket.
        /// </summary>
        private static void PlantTrees(int centerX, int[] surface)
        {
            int target = TreeMinCount + WorldGen.genRand.Next(TreeMaxCount - TreeMinCount + 1);
            int planted = 0;

            int leftX = centerX - (5 + WorldGen.genRand.Next(4));
            int rightX = centerX + (5 + WorldGen.genRand.Next(4));
            bool goRight = WorldGen.genRand.NextBool();

            // Bounded rather than a while loop: a candidate can be rejected for several
            // reasons, and there is no guarantee enough of them ever succeed.
            for (int attempt = 0; attempt < 80 && planted < target; attempt++)
            {
                int x = goRight ? rightX : leftX;
                int step = TreeGapMin + WorldGen.genRand.Next(TreeGapMax - TreeGapMin + 1);

                if (goRight)
                    rightX += step;
                else
                    leftX -= step;

                goRight = !goRight;

                int i = x - (centerX - Radius);
                if (i < 2 || i >= surface.Length - 2)
                    continue;

                if (Math.Abs(x - centerX) <= SwordClearance + 1)
                    continue;

                // Nudge along a few columns rather than giving the spot up: a tree
                // needs a flat, unsloped tile with clear space, and the exact column the
                // spacing landed on often is not one.
                for (int nudge = 0; nudge < 5; nudge++)
                {
                    int tx = x + (nudge % 2 == 0 ? nudge / 2 : -(nudge / 2 + 1));
                    int ti = tx - (centerX - Radius);

                    if (ti < 2 || ti >= surface.Length - 2)
                        continue;

                    if (Math.Abs(tx - centerX) <= SwordClearance + 1)
                        continue;

                    if (GrowAshTree(tx, surface[ti]))
                    {
                        planted++;
                        break;
                    }
                }
            }

            if (planted == 0)
            {
                ModContent.GetInstance<PisAndDag>()?.Logger
                    .Warn("Mors Monument grew no ash trees; check that GrassOver actually laid ash grass.");
            }
        }

        /// <summary>
        /// Grows one ash tree standing on the given ground tile, reporting whether it took.
        ///
        /// WorldGen.GrowTree is the WRONG call and is why the hilltop came out bare. It
        /// grows the original tree set from the ground it is handed and simply declines on
        /// ash grass. Ash trees arrived in 1.4.4 alongside a profile system, and the tree
        /// to grow is chosen by passing that profile in — not inferred from the ground.
        ///
        /// The ground tile and the tile above it are both tried, because the settings
        /// overload is documented without saying which one it wants; whichever leaves a
        /// trunk behind is the right one.
        /// </summary>
        private static bool GrowAshTree(int x, int ground)
        {
            if (!WorldGen.InWorld(x, ground) || !WorldGen.InWorld(x, ground - 1))
                return false;

            Tile floor = Main.tile[x, ground];

            // A trunk will not sit on a sloped or halved tile, and it needs its own
            // column free — the fireblossoms run after this for exactly that reason.
            if (!floor.HasTile || floor.Slope != SlopeType.Solid || floor.IsHalfBlock)
                return false;

            if (Main.tile[x, ground - 1].HasTile)
                return false;

            WorldGen.GrowTreeSettings settings = TallAsh();

            WorldGen.GrowTreeWithSettings(x, ground - 1, settings);
            if (Main.tile[x, ground - 1].HasTile)
                return true;

            WorldGen.GrowTreeWithSettings(x, ground, settings);
            return Main.tile[x, ground - 1].HasTile;
        }

        /// <summary>
        /// The stock ash tree profile with its trunk doubled.
        ///
        /// GrowTreeSettings is a struct, so reading the profile copies it and the taller
        /// heights stay local to this monument. Were it a class this would quietly make
        /// every ash tree in the world twice as tall, so it is worth being sure before
        /// touching a shared profile like this.
        /// </summary>
        private static WorldGen.GrowTreeSettings TallAsh()
        {
            WorldGen.GrowTreeSettings settings = WorldGen.GrowTreeSettings.Profiles.Tree_Ash;

            settings.TreeHeightMin *= 2;
            settings.TreeHeightMax *= 2;

            // TreeTopPaddingNeeded is left alone on purpose: it is clearance for the
            // crown sprite, not part of the trunk, and inflating it only makes the
            // grower's space check harder to pass for no visual gain.

            return settings;
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
                int ground = surface[Radius + dx];
                int plantY = ground - 1;

                if (!WorldGen.InWorld(x, plantY))
                    continue;

                if (Main.tile[x, plantY].HasTile)
                    continue;

                // A herb standing on a sloped tile floats off its corner.
                if (Main.tile[x, ground].Slope != SlopeType.Solid)
                    continue;

                // Thins out toward the edge of the field, so it fades rather than stopping
                // at a hard circle.
                float density = 0.9f - 0.45f * (Math.Abs(dx) / (float)FieldRadius);
                if (WorldGen.genRand.NextFloat() > density)
                    continue;

                WorldGen.PlaceTile(x, plantY, herbTile, true, false, -1, herbStyle);

                Tile tile = Main.tile[x, plantY];

                // PlaceTile enforces vanilla's own planting rules and can decline. Ash and
                // ash grass are both valid ground for fireblossom, but write the tile
                // directly if it ever refuses rather than ending up with a bare hilltop.
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
