using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Generates a natural-looking graveyard on a forest shelf behind
    /// one of the ocean beaches.
    ///
    /// The graveyard is shaped into a broad, gently rolling hill rather
    /// than a perfectly flat platform. Tombstones and wooden fencing are
    /// placed afterwards and follow the resulting terrain.
    /// </summary>
    public class GraveyardPass : GenPass
    {
        // ============================================================
        // GRAVE MARKERS
        // ============================================================

        private static readonly int[] Markers =
        {
            ItemID.Tombstone,
            ItemID.GraveMarker,
            ItemID.CrossGraveMarker,
            ItemID.Headstone,
            ItemID.Gravestone,
            ItemID.Obelisk
        };


        // ============================================================
        // BASIC SETTINGS
        // ============================================================

        // Width of the graveyard, foot to foot.
        //
        // Wide is cheap now. The rise is only a few tiles, so the width
        // is what decides how gentle the slope reads — the same 5 tiles
        // over 92 columns is a mound, over 124 it is a swell in the land.
        private const int PlotWidth = 70;

        // How far inland from the beach the search begins.
        private const int InlandMargin = 24;

        // How far inland the search reaches, as a fraction of the world's
        // width. A constant would search a quarter of a Small world and a
        // tenth of a Large one, which is why the old fixed 900 behaved
        // completely differently depending on world size.
        private const float SearchFraction = 0.14f;

        // Never shorter than this, so a Small world still gets a real search.
        private const int MinSearchSpan = 260;

        // Score charged per tile of distance inland from the beach.
        //
        // Weigh it against the terrain terms to read the trade: a slope of
        // 5 costs 60, so at 3/tile the search will walk about 20 tiles
        // inland to find flatter ground and no further. Raise it to pin
        // the graveyard to the shore, lower it to let it wander.
        private const int BeachPull = 3;

        private static int SearchSpan =>
            Math.Max(
                MinSearchSpan,
                (int)(Main.maxTilesX * SearchFraction));

        // Vertical space cleared above the new terrain.
        private const int HeadRoom = 25;

        // How far below the LOWEST ground in the plot the hill is forced
        // solid. This is what swallows chasms, caves and corruption pits:
        // the fill does not stop at the first solid tile it meets, so a
        // hole under the mound is packed rather than bridged.
        private const int Foundation = 14;


        // ============================================================
        // TOMBSTONE SETTINGS
        // ============================================================

        private const int MinStones = 6;
        private const int MaxStones = 9;

        // Minimum distance between tombstones.
        private const int MinStoneSpacing = 0;

        // Maximum distance between tombstones.
        private const int MaxStoneSpacing = 4;


        // ============================================================
        // HILL SETTINGS
        // ============================================================

        // Remember: Terraria Y increases DOWNWARD, so rising means
        // subtracting.

        // How many tiles the centre of the hill is lifted ABOVE THE
        // GROUND IT STANDS ON. A height difference, not an elevation.
        //
        // Read as an elevation it made a mountain; read as a lift it
        // cannot, because no column is ever raised further than this off
        // its own ground. Five means five, on flat land and on a slope.
        private const int MinCrownRise = 5;
        private const int MaxCrownRise = 8;


        // Share of the half-width that is dead flat crown before the
        // falloff begins.
        //
        // Generous now. With only a few tiles of rise there is no corner
        // to hide, and a broad flat top is the whole point — the ground
        // should read as level with a slight lift, not as a summit.
        private const float FlatFraction = 0.10f;

        // How much of the falloff is raised cosine rather than straight
        // ramp.
        //
        // Back near Mors's half and half. Weighting the cosine made the
        // silhouette bulge, which is what read as "too curved"; the
        // straight ramp is what turns a dome into a long even rise.
        private const float DomeWeight = 0.70f;

        // Columns at each end kept clear of markers, so nothing stands on
        // the lip where the hill meets the untouched world.
        private const int EdgeMargin = 4;

        // Columns averaged at each foot to find the terrain's trend line.
        private const int FootSample = 6;

        // How big a drop at the foot still reads as ordinary terrain.
        // Above this the skirt keeps stepping outward.
        private const int BlendTolerance = 2;

        // Hard stop on the skirt, so a hill built on the lip of a ravine
        // does not walk a ramp halfway across the world.
        private const int MaxSkirt = 48;

        // Share of the plot the graves are allowed to occupy, centred on
        // the crown. Scattering them the full width leaves stones stranded
        // down the slopes; a cluster on the top reads as one graveyard.
        private const float GraveSpread = 0.50f;


        // ============================================================
        // LIVING TREES
        // ============================================================

        // How far above the hill to look for living wood. A living tree's
        // canopy sits well clear of the ground it grows from, and the
        // search only has to find ONE tile of it — the flood fill follows
        // the rest wherever it goes.
        private const int LivingTreeReach = 60;

        // Ceiling on tiles removed per tree, so a pathological fill can
        // never run away across the world.
        private const int LivingTreeCap = 40000;

        // Living wood wall. Taken from vanilla's own comparison in
        // WorldGen rather than a WallID name, since that is the number
        // the game itself tests against.
        private const int LivingWoodWall = 244;


        // ============================================================
        // BIOME PALETTE
        // ============================================================

        // What the hill is built out of. Resolved per-site from whatever
        // the surrounding land already is, so a graveyard in the snow is
        // a snow hill and one in the corruption is a corrupt hill. The
        // pass no longer cares which biome it lands in.
        private static int fillTile = TileID.Dirt;
        private static int capTile = TileID.Grass;


        // ============================================================
        // GROUND DETECTION
        // ============================================================

        // How far below a solid tile we look before believing it is the
        // ground, and how much of that has to be solid.
        private const int GroundProbe = 40;
        private const int GroundSolidMin = 18;

        // Fewest readable columns a plot needs before it is buildable at
        // all. Anything above this is bridged rather than rejected.
        private const int MinReadableColumns = 12;

        // How far past a rejected floating island the scan jumps.
        private const int IslandSkip = 20;


        // ============================================================
        // FENCE SETTINGS
        // ============================================================

        // Number of tiles inside the graveyard perimeter.
        private const int FenceInset = 2;

        // How far past the outermost tombstone the fence may reach.
        private const int FenceMargin = 5;

        // Below this the enclosure is too small to bother with — and the
        // entrance maths below needs room to pick a spot inside it.
        private const int MinFenceSpan = 14;

        // Chance that a fence section is skipped.
        //
        // This makes the fence look old and imperfect instead of
        // looking like a perfectly manufactured rectangle.
        private const int FenceGapChance = 12;

        // Minimum number of consecutive fence tiles before a possible
        // gap is allowed.
        private const int FenceMinRun = 2;

        // Length of one unbroken stretch of fence, and of the gap after
        // it.
        //
        // The old scheme skipped panels at a flat per-tile chance and cut
        // exactly one deliberate gap, which gave a nearly solid rail with
        // a few nicks in it. Building in runs instead makes the sparseness
        // structural: no stretch can exceed MaxFenceRun, and every one is
        // followed by a real gap rather than a missing tooth.
        private const int MinFenceRun = 2;
        private const int MaxFenceRun = 6;

        private const int MinFenceGap = 3;
        private const int MaxFenceGap = 8;

        // One panel in this many is plain Iron Fence rather than Wrought
        // Iron. Iron is the rarer of the two, so it reads as the odd
        // section someone replaced rather than as a second material.
        private const int IronFenceRarity = 3;


        public GraveyardPass(
            string name,
            double loadWeight)
            : base(name, loadWeight)
        {
        }


        // ============================================================
        // ENTRY POINT
        // ============================================================

        protected override void ApplyPass(
            GenerationProgress progress,
            GameConfiguration configuration)
        {
            progress.Message =
                "Preparing the graveyard";

            Log("pass started");

            // Randomly choose which beach gets priority.
            bool leftFirst =
                WorldGen.genRand.NextBool();

            if (TrySide(
                    leftFirst,
                    progress))
            {
                return;
            }

            // If that beach failed, try the opposite beach.
            if (TrySide(
                    !leftFirst,
                    progress))
            {
                return;
            }


            // --------------------------------------------------------
            // Last resort: build it anyway.
            // --------------------------------------------------------
            //
            // The graveyard is not optional scenery, it is a place the
            // player is meant to find, so "no site scored well enough" is
            // not an acceptable outcome — and a pass that silently does
            // nothing is exactly what this one kept doing. By this point
            // both shores have been walked and nothing was buildable, so
            // a spot is chosen on arithmetic alone and the terrain is
            // simply made to accept it.
            Log("both sides returned false — FORCING");

            Force(leftFirst);
        }


        /// <summary>
        /// Builds at a fixed offset inland, measuring the ground the crude
        /// way and taking whatever it gets.
        /// </summary>
        private static void Force(
            bool left)
        {
            int plotLeft =
                left
                    ? GenVars.leftBeachEnd + InlandMargin
                    : GenVars.rightBeachStart - InlandMargin - PlotWidth;

            plotLeft =
                Math.Clamp(
                    plotLeft,
                    20,
                    Main.maxTilesX - PlotWidth - 20);


            // Deepest ground anywhere in the plot, or the surface layer
            // itself if literally nothing reads.
            int level = 0;

            for (int x = plotLeft;
                 x < plotLeft + PlotWidth;
                 x++)
            {
                if (Surface(x, out int y, out _))
                    level = Math.Max(level, y);
            }

            if (level <= 0)
                level = (int)Main.worldSurface - 20;


            ResolvePalette(TileID.Dirt);

            Shave(
                plotLeft,
                level);


            Log(
                $"FORCED at x={plotLeft}..{plotLeft + PlotWidth} " +
                $"level={level} stones={lastStonesPlaced}");
        }


        // ============================================================
        // SEARCH ONE BEACH
        // ============================================================

        private static bool TrySide(
            bool left,
            GenerationProgress progress)
        {
            int start;

            if (left)
            {
                start =
                    GenVars.leftBeachEnd +
                    InlandMargin;
            }
            else
            {
                start =
                    GenVars.rightBeachStart -
                    InlandMargin -
                    PlotWidth;
            }

            int step =
                left ? 1 : -1;


            int bestX = -1;
            int bestLevel = 0;
            int bestScore = int.MinValue;
            int bestMaterial = TileID.Dirt;

            int considered = 0;
            int surveyed = 0;


            // --------------------------------------------------------
            // Search the entire region.
            // --------------------------------------------------------

            for (int offset = 0;
                 offset < SearchSpan;
                 offset++)
            {
                int plotLeft =
                    start +
                    offset * step;


                if (plotLeft < 20 ||
                    plotLeft + PlotWidth >=
                    Main.maxTilesX - 20)
                {
                    continue;
                }


                progress.Set(
                    offset /
                    (float)SearchSpan);


                considered++;


                if (!Survey(
                        plotLeft,
                        out int level,
                        out int score,
                        out int material))
                {
                    continue;
                }


                surveyed++;


                // Distance from the shore, charged per tile.
                //
                // Without this the score was pure terrain quality, and
                // since nothing rejects any more, it simply hunted for the
                // flattest ground in the whole search range — which on a
                // Large world is deep inland. The first run built a
                // perfectly good graveyard 1,171 tiles from the water.
                //
                // At BeachPull the charge dwarfs the terrain terms, so a
                // rougher site near the sea always beats a perfect one
                // far from it, which is what "near the beach" has to mean.
                score -= offset * BeachPull;


                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = plotLeft;
                    bestLevel = level;
                    bestMaterial = material;
                }
            }


            Log(
                $"side={(left ? "left" : "right")} " +
                $"start={start} span={SearchSpan} " +
                $"leftBeachEnd={GenVars.leftBeachEnd} " +
                $"rightBeachStart={GenVars.rightBeachStart} " +
                $"maxTilesX={Main.maxTilesX} " +
                $"considered={considered} surveyed={surveyed} " +
                $"bestX={bestX} bestLevel={bestLevel} " +
                $"inland={(bestX == -1 ? -1 : Math.Abs(bestX - start))} " +
                $"bestScore={bestScore} bestMaterial={bestMaterial}");


            // Only reachable if every column off this beach was outside
            // the world. The caller then tries the other shore.
            if (bestX == -1)
                return false;


            // --------------------------------------------------------
            // Build the actual graveyard.
            // --------------------------------------------------------

            ResolvePalette(
                bestMaterial);

            Shave(
                bestX,
                bestLevel);


            Log(
                $"BUILT at x={bestX}..{bestX + PlotWidth} level={bestLevel} " +
                $"fill={fillTile} cap={capTile} stones={lastStonesPlaced}");


            return true;
        }


        // ============================================================
        // LOGGING
        // ============================================================

        /// <summary>
        /// Writes to the tModLoader client log.
        ///
        /// Worldgen runs once and leaves no other trace, so a pass that
        /// does nothing is invisible from inside the game. These lines are
        /// the difference between knowing why and guessing.
        ///
        /// Find them in:
        ///   Documents\My Games\Terraria\tModLoader\Logs\client.log
        /// searching for "[Graveyard]".
        /// </summary>
        private static void Log(
            string message)
        {
            ModContent.GetInstance<PisAndDag>()?.Logger
                .Info("[Graveyard] " + message);
        }


        /// <summary>How many markers the last Plant call actually placed.</summary>
        private static int lastStonesPlaced;

        /// <summary>
        /// Plot indices of the outermost markers the last Plant placed.
        /// Inverted (min &gt; max) when nothing was placed at all.
        /// </summary>
        private static int lastStoneMin = int.MaxValue;
        private static int lastStoneMax = int.MinValue;


        // ============================================================
        // SURVEY
        // ============================================================

        /// <summary>
        /// Measures a plot. Reports the level to build the hill from, how
        /// good the site is, and what the local land is made of.
        ///
        /// Nothing here rejects on biome any more, and that is the whole
        /// fix. The old version required 55% dirt-or-grass, under 20%
        /// "forbidden" tile, a slope under 18 AND not one wet column in
        /// sixty-four — four conditions ANDed over a coastline, where sand
        /// runs far inland and puddles are everywhere. Most worlds had no
        /// plot that passed all four, so the pass silently built nothing.
        ///
        /// Now every site is buildable and the score only decides which is
        /// BEST. The only thing that can still fail is not finding ground
        /// at all, which means the column is off the edge of the world.
        /// </summary>
        private static bool Survey(
            int plotLeft,
            out int level,
            out int score,
            out int material)
        {
            level = 0;
            score = 0;
            material = TileID.Dirt;


            int highest =
                int.MaxValue;

            int lowest =
                int.MinValue;

            int wetColumns = 0;
            int unreadable = 0;

            // Tally of what the surrounding land is made of, so the hill
            // can be built to match it.
            Dictionary<int, int> materials =
                new Dictionary<int, int>();


            int measured = 0;


            for (int x = plotLeft;
                 x < plotLeft + PlotWidth;
                 x++)
            {
                // A column with no findable ground no longer kills the
                // plot. It used to, and that is the remaining reason the
                // pass came up empty on Large worlds: one column over a
                // deep cavern, a cliff, or a shaft anywhere in sixty-four
                // rejected the whole site, and on a big coastline there
                // are plenty of those. The column is simply not counted.
                if (!Surface(
                        x,
                        out int y,
                        out int type))
                {
                    unreadable++;
                    continue;
                }


                measured++;


                if (Main.tile[
                        x,
                        Math.Max(0, y - 1)]
                    .LiquidAmount > 0)
                {
                    wetColumns++;
                }


                materials.TryGetValue(
                    type,
                    out int seen);

                materials[type] =
                    seen + 1;


                highest =
                    Math.Min(highest, y);

                lowest =
                    Math.Max(lowest, y);
            }


            // The one thing that can still fail: not a single column in
            // the plot had readable ground. That is a site hanging over
            // open air, and there is nothing to build on.
            if (measured < MinReadableColumns)
                return false;


            // The LOWEST ground, not the average. Building up from the
            // deepest point means the mound clears every neighbouring
            // column, so it is always a hill and never a shelf cut into
            // the side of one.
            level = lowest;


            int slope =
                lowest - highest;


            // --------------------------------------------------------
            // Which tile the local land is mostly made of.
            // --------------------------------------------------------

            int best = 0;

            foreach (KeyValuePair<int, int> entry in materials)
            {
                if (entry.Value > best)
                {
                    best = entry.Value;
                    material = entry.Key;
                }
            }


            // --------------------------------------------------------
            // Score. Preferences, not requirements.
            // --------------------------------------------------------

            score = 1000;


            // Flatter ground needs less earth moved, so it looks more
            // like a hill that was always there.
            score -= slope * 12;


            // Water still costs, because a mound raised through a pond
            // leaves the pond sitting against its flank.
            score -= wetColumns * 25;


            // Columns that could not be read are a cliff or a shaft the
            // hill will have to bridge. Survivable, but not preferred.
            score -= unreadable * 40;


            // A site the local biome already agrees with looks best, but
            // this is now worth a nudge rather than a veto.
            if (material == TileID.Grass ||
                material == TileID.Dirt)
            {
                score += 150;
            }


            return true;
        }


        // ============================================================
        // BIOME PALETTE
        // ============================================================

        /// <summary>
        /// Decides what the hill is built from, given what the land around
        /// it already is.
        ///
        /// Fill is the body of the mound and cap is the single surface
        /// layer. They differ wherever a biome's ground is soil under a
        /// living skin — dirt under grass, mud under jungle grass — and
        /// match wherever it is not, like sand or snow.
        /// </summary>
        private static void ResolvePalette(
            int material)
        {
            switch (material)
            {
                case TileID.Sand:
                case TileID.HardenedSand:
                case TileID.Sandstone:
                    fillTile = TileID.Sand;
                    capTile = TileID.Sand;
                    break;

                case TileID.Ebonsand:
                    fillTile = TileID.Ebonsand;
                    capTile = TileID.Ebonsand;
                    break;

                case TileID.Crimsand:
                    fillTile = TileID.Crimsand;
                    capTile = TileID.Crimsand;
                    break;

                case TileID.Pearlsand:
                    fillTile = TileID.Pearlsand;
                    capTile = TileID.Pearlsand;
                    break;

                case TileID.SnowBlock:
                case TileID.IceBlock:
                    fillTile = TileID.SnowBlock;
                    capTile = TileID.SnowBlock;
                    break;

                case TileID.Mud:
                case TileID.JungleGrass:
                    fillTile = TileID.Mud;
                    capTile = TileID.JungleGrass;
                    break;

                case TileID.MushroomGrass:
                    fillTile = TileID.Mud;
                    capTile = TileID.MushroomGrass;
                    break;

                // The infections keep their soil and change their skin,
                // which is exactly how vanilla spreads them.
                case TileID.Ebonstone:
                case TileID.CorruptGrass:
                    fillTile = TileID.Dirt;
                    capTile = TileID.CorruptGrass;
                    break;

                case TileID.Crimstone:
                case TileID.CrimsonGrass:
                    fillTile = TileID.Dirt;
                    capTile = TileID.CrimsonGrass;
                    break;

                case TileID.Pearlstone:
                case TileID.HallowedGrass:
                    fillTile = TileID.Dirt;
                    capTile = TileID.HallowedGrass;
                    break;

                case TileID.Stone:
                case TileID.Marble:
                case TileID.Granite:
                    fillTile = TileID.Stone;
                    capTile = TileID.Stone;
                    break;

                case TileID.Ash:
                    fillTile = TileID.Ash;
                    capTile = TileID.Ash;
                    break;

                default:
                    fillTile = TileID.Dirt;
                    capTile = TileID.Grass;
                    break;
            }
        }


        // ============================================================
        // FIND SURFACE
        // ============================================================

        /// <summary>
        /// Finds the first solid terrain tile from the sky downward.
        ///
        /// This deliberately searches down toward the rock layer instead
        /// of assuming the terrain is close to Main.worldSurface.
        /// </summary>
        private static bool Surface(
            int x,
            out int y,
            out int type)
        {
            int top = 50;

            int bottom =
                Math.Min(
                    Main.maxTilesY - 40,
                    (int)GenVars.rockLayer);


            for (y = top;
                 y < bottom;
                 y++)
            {
                Tile tile =
                    Main.tile[x, y];


                if (!tile.HasTile)
                    continue;


                if (!Main.tileSolid[
                        tile.TileType])
                {
                    continue;
                }


                if (tile.IsActuated)
                    continue;


                // Starting the scan at the top of the sky means the first
                // solid tile in a column can be a floating island, and a
                // plot that measures one would try to build a graveyard in
                // mid-air. Real ground keeps going; an island is a slab
                // with nothing under it. Counting what is below separates
                // them without needing to know how high islands sit.
                if (!Grounded(x, y))
                {
                    y += IslandSkip;
                    continue;
                }


                type =
                    tile.TileType;

                return true;
            }


            y = 0;
            type = -1;

            return false;
        }


        /// <summary>
        /// Whether the solid tile at (x, y) has real ground under it
        /// rather than being the crust of a floating island.
        /// </summary>
        private static bool Grounded(
            int x,
            int y)
        {
            int solid = 0;

            int limit =
                Math.Min(
                    Main.maxTilesY - 2,
                    y + GroundProbe);


            for (int probe = y;
                 probe < limit;
                 probe++)
            {
                Tile tile =
                    Main.tile[x, probe];


                if (tile.HasTile &&
                    Main.tileSolid[
                        tile.TileType])
                {
                    solid++;
                }
            }


            return solid >= GroundSolidMin;
        }


        // ============================================================
        // BUILD TERRAIN
        // ============================================================

        private static void Shave(
            int plotLeft,
            int level)
        {
            // --------------------------------------------------------
            // Fell any living tree standing here, whole.
            // --------------------------------------------------------
            //
            // A living tree is one connected mass that reaches far above
            // and below the ground it grows from, so building through it
            // removed the middle and left the canopy and the lower trunk
            // hanging in the air with a wedge of hill driven through
            // them. Half a tree looks far worse than no tree.

            FellLivingTrees(plotLeft, level);


            // --------------------------------------------------------
            // Generate the stepped hill.
            // --------------------------------------------------------

            GenerateHillProfile(
                plotLeft,
                level,
                out int[] heights,
                out bool[] plantable);


            // --------------------------------------------------------
            // Modify terrain.
            // --------------------------------------------------------

            for (int i = 0;
                 i < PlotWidth;
                 i++)
            {
                int x =
                    plotLeft + i;

                int surface =
                    heights[i];


                // ----------------------------------------------------
                // Clear above the new surface.
                // ----------------------------------------------------

                int clearTop =
                    Math.Max(
                        10,
                        surface - HeadRoom);


                for (int y = clearTop;
                     y < surface;
                     y++)
                {
                    Tile tile =
                        Main.tile[x, y];


                    tile.ClearTile();
                    tile.LiquidAmount = 0;
                }


                // ----------------------------------------------------
                // Fill underneath, unconditionally.
                // ----------------------------------------------------
                //
                // The old loop stopped at the first solid tile it met,
                // which bridged a chasm with a one-tile crust and left the
                // hole underneath. Packing straight down to a fixed
                // foundation instead is what lets the graveyard land on
                // broken corruption ground: pits, crevices and the caves
                // under them are simply filled in, and what is left is a
                // solid hill of whatever that biome is made of.

                int floor =
                    Math.Min(
                        Main.maxTilesY - 20,
                        level + Foundation);


                for (int y = surface;
                     y <= floor;
                     y++)
                {
                    Tile tile =
                        Main.tile[x, y];


                    tile.HasTile = true;
                    tile.TileType = (ushort)fillTile;
                    tile.Slope =
                        SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.LiquidAmount = 0;
                }


                // ----------------------------------------------------
                // Skin it in the local biome's surface tile.
                // ----------------------------------------------------

                Tile cap =
                    Main.tile[x, surface];


                cap.HasTile = true;
                cap.TileType = (ushort)capTile;
                cap.Slope =
                    SlopeType.Solid;
                cap.IsHalfBlock = false;
                cap.LiquidAmount = 0;
            }


            // --------------------------------------------------------
            // Walk the feet out until they meet the world.
            // --------------------------------------------------------
            //
            // The arc's baseline is taken from the ground at the plot
            // edges, but the ground a few tiles further out can be well
            // below that — so the hill still ended in a visible ledge.
            // This keeps stepping down past the plot until the drop is
            // small enough to read as ordinary terrain.

            int skirtLeft =
                BlendSkirt(
                    plotLeft - 1,
                    heights[0],
                    -1,
                    level);

            int skirtRight =
                BlendSkirt(
                    plotLeft + PlotWidth,
                    heights[PlotWidth - 1],
                    1,
                    level);


            // --------------------------------------------------------
            // Frame the modified region.
            // --------------------------------------------------------

            int frameLeft =
                Math.Max(
                    1,
                    plotLeft - skirtLeft - 2);

            int frameRight =
                Math.Min(
                    Main.maxTilesX - 2,
                    plotLeft + PlotWidth + skirtRight + 1);


            int frameTop =
                Math.Max(
                    10,
                    level - HeadRoom - 2);

            int frameBottom =
                Math.Min(
                    Main.maxTilesY - 2,
                    level + Foundation + 2);


            for (int x = frameLeft;
                 x <= frameRight;
                 x++)
            {
                for (int y = frameTop;
                     y <= frameBottom;
                     y++)
                {
                    WorldGen.SquareTileFrame(
                        x,
                        y);
                }
            }


            // --------------------------------------------------------
            // Tombstones.
            // --------------------------------------------------------

            // The statue goes down BEFORE the graves. It is one object in
            // the whole graveyard and wants the crown; the markers are
            // many and can go anywhere. Placing it second would mean
            // fighting whatever a tombstone happened to occupy.
            PlaceAngel(
                plotLeft,
                heights,
                plantable);


            Plant(
                plotLeft,
                heights,
                plantable);


            // --------------------------------------------------------
            // Fence.
            // --------------------------------------------------------

            BuildFence(
                plotLeft,
                heights);
        }


        // ============================================================
        // HILL PROFILE
        // ============================================================

        /// <summary>
        /// A hill: each column lifted from its own natural ground toward a
        /// flat crown, by a bell that is 1 in the middle and 0 at the ends.
        ///
        ///                    ________________
        ///                  /                  \
        ///           ______/                    \______
        ///      ____/                                   \____
        ///
        /// Two earlier shapes were wrong in instructive ways.
        ///
        /// A rolling profile around the AVERAGE ground could carve a dip
        /// instead of a rise. Terraces measured up from the LOWEST ground
        /// were worse: I claimed that guaranteed clearing the land around
        /// it, which is only true when the rise exceeds the plot's slope
        /// range. It often did not, so the "hill" sat BELOW its neighbours
        /// and HeadRoom then excavated the higher ground above it — a
        /// quarry, not a hill.
        ///
        /// Lifting off ground[i] fixes that by construction. The bell is
        /// an AMOUNT added to each column's own height, so no column can
        /// end up below where the land already was, and none can be
        /// raised more than the lift. The ends need no special blending
        /// either: the bell reaches 0 there, so they ARE the untouched
        /// ground.
        ///
        /// Past the flat crown the falloff is half straight ramp and half
        /// raised cosine, lifted from MorsMonumentPass. Pure cosine bulges
        /// into a dome; pure linear creases where the slope meets the
        /// plateau and again where it meets the ground.
        /// </summary>
        private static void GenerateHillProfile(
            int plotLeft,
            int level,
            out int[] heights,
            out bool[] plantable)
        {
            heights = new int[PlotWidth];
            plantable = new bool[PlotWidth];


            // --------------------------------------------------------
            // Read the land as it actually is.
            // --------------------------------------------------------

            int[] ground =
                new int[PlotWidth];

            for (int i = 0; i < PlotWidth; i++)
            {
                ground[i] =
                    Surface(plotLeft + i, out int y, out _)
                        ? y
                        : level;
            }


            // --------------------------------------------------------
            // The trend of the land, as a straight line.
            // --------------------------------------------------------
            //
            // This is the part the last two attempts were missing.
            //
            // Lifting each column off its own ground made a hill that
            // inherited every notch and ledge in the terrain under it —
            // the battlements. Lerping toward the plot's highest point
            // instead made a mountain. The answer is neither: take the
            // ground at the two ENDS, run a straight line between them,
            // and build the hill on that.
            //
            // The line meets real ground at both feet, so the hill blends
            // exactly. Between them it is perfectly smooth, so nothing of
            // the old surface shows through the top.

            float leftGround =
                AverageGround(ground, 0, FootSample);

            float rightGround =
                AverageGround(ground, PlotWidth - FootSample, PlotWidth);


            // --------------------------------------------------------
            // Raise the arc off that line.
            // --------------------------------------------------------

            int crownLift =
                WorldGen.genRand.Next(
                    MinCrownRise,
                    MaxCrownRise + 1);

            float middle =
                (PlotWidth - 1) * 0.5f;


            for (int i = 0; i < PlotWidth; i++)
            {
                float along =
                    i / (float)(PlotWidth - 1);

                float baseline =
                    leftGround +
                    (rightGround - leftGround) * along;


                // 0 at the centre, 1 at either end.
                float t =
                    Math.Abs(i - middle) / middle;

                float shape;

                if (t <= FlatFraction)
                {
                    shape = 1f;
                }
                else
                {
                    float u =
                        (t - FlatFraction) /
                        (1f - FlatFraction);

                    float straight = 1f - u;

                    float eased =
                        0.5f +
                        0.5f * MathF.Cos(u * MathF.PI);

                    shape =
                        DomeWeight * eased +
                        (1f - DomeWeight) * straight;
                }


                int raised =
                    (int)Math.Round(baseline - crownLift * shape);


                // Never dig. Where the old ground already stood higher
                // than the arc, it is left exactly as it was — which is
                // also what keeps the feet flush with the world.
                heights[i] =
                    Math.Min(raised, ground[i]);
            }


            // --------------------------------------------------------
            // No step taller than one tile.
            // --------------------------------------------------------
            //
            // The arc is smooth; the clamp is here for where it was
            // pinned back to a ragged patch of original ground. Two
            // passes, one from each end, each only ever RAISING a column
            // its neighbour outran — lowering could dig below the ground
            // again, which is the thing this shape exists to avoid.

            for (int i = 1; i < PlotWidth; i++)
            {
                if (heights[i] < heights[i - 1] - 1)
                    heights[i] = heights[i - 1] - 1;
            }


            for (int i = PlotWidth - 2; i >= 0; i--)
            {
                if (heights[i] < heights[i + 1] - 1)
                    heights[i] = heights[i + 1] - 1;
            }


            // --------------------------------------------------------
            // Mark where a two-wide marker can stand.
            // --------------------------------------------------------

            for (int i = 0; i < PlotWidth; i++)
            {
                bool hasPartner =
                    i + 1 < PlotWidth &&
                    heights[i + 1] == heights[i];

                bool clearOfEdge =
                    i >= EdgeMargin &&
                    i < PlotWidth - EdgeMargin;

                plantable[i] =
                    hasPartner && clearOfEdge;
            }
        }


        /// <summary>
        /// Mean ground height over a span, so one freak column at the
        /// very edge cannot tilt the whole baseline.
        /// </summary>
        private static float AverageGround(
            int[] ground,
            int from,
            int to)
        {
            int total = 0;
            int count = 0;

            for (int i = from; i < to; i++)
            {
                if (i < 0 || i >= ground.Length)
                    continue;

                total += ground[i];
                count++;
            }

            return count == 0
                ? 0f
                : total / (float)count;
        }


        // ============================================================
        // TOMBSTONES
        // ============================================================

        /// <summary>
        /// Stands one Angel Statue on the crown.
        ///
        /// Restricted to the middle third of the plot. The crown is where
        /// the ground is flat and where the eye goes, and a memorial on
        /// the slope with its back to the graves reads as clutter rather
        /// than as the centrepiece.
        /// </summary>
        // ============================================================
        // LIVING TREES
        // ============================================================

        /// <summary>
        /// Removes every living tree that any part of the graveyard would
        /// touch — the whole tree, not just the part in the way.
        ///
        /// A living tree is a single connected mass of living wood and
        /// leaves, so a flood fill from one tile inside the plot reaches
        /// the canopy overhead and the roots below and takes the lot.
        /// Clipping it instead leaves the top floating.
        /// </summary>
        private static void FellLivingTrees(
            int plotLeft,
            int level)
        {
            int from =
                Math.Max(20, plotLeft - MaxSkirt);

            int to =
                Math.Min(Main.maxTilesX - 20, plotLeft + PlotWidth + MaxSkirt);

            int top =
                Math.Max(10, level - HeadRoom - LivingTreeReach);

            int bottom =
                Math.Min(Main.maxTilesY - 20, level + Foundation);


            HashSet<int> seen =
                new HashSet<int>();


            for (int x = from; x < to; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    if (IsLivingTree(x, y))
                        Fell(x, y, seen);
                }
            }
        }


        private static bool IsLivingTree(
            int x,
            int y)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile)
                return false;

            int type = tile.TileType;

            return type == TileID.LivingWood
                || type == TileID.LeafBlock
                || type == TileID.LivingMahogany
                || type == TileID.LivingMahoganyLeaves;
        }


        /// <summary>
        /// Flood fill outward from one living-tree tile, clearing the
        /// whole connected mass.
        ///
        /// Coordinates are packed into a single int rather than carried as
        /// points, so the frontier is a plain Stack&lt;int&gt; and nothing
        /// here needs a struct or an extra using.
        /// </summary>
        private static void Fell(
            int startX,
            int startY,
            HashSet<int> seen)
        {
            Stack<int> frontier =
                new Stack<int>();

            frontier.Push(startY * Main.maxTilesX + startX);

            int removed = 0;


            while (frontier.Count > 0 && removed < LivingTreeCap)
            {
                int key = frontier.Pop();

                int x = key % Main.maxTilesX;
                int y = key / Main.maxTilesX;


                if (x < 10 || x >= Main.maxTilesX - 10 ||
                    y < 10 || y >= Main.maxTilesY - 10)
                {
                    continue;
                }


                if (!seen.Add(key))
                    continue;


                if (!IsLivingTree(x, y))
                    continue;


                Tile tile = Main.tile[x, y];

                tile.ClearTile();
                tile.LiquidAmount = 0;

                // The hollow trunk is papered with living wood wall. 244
                // is taken from vanilla's own check in WorldGen rather
                // than from a WallID name, because that is the value the
                // game actually compares against.
                if (tile.WallType == LivingWoodWall)
                    tile.WallType = WallID.None;

                removed++;


                frontier.Push(key + 1);
                frontier.Push(key - 1);
                frontier.Push(key + Main.maxTilesX);
                frontier.Push(key - Main.maxTilesX);
            }
        }


        /// <summary>
        /// Ramps one foot of the hill outward until it is level with the
        /// land, and reports how many columns that took.
        /// </summary>
        /// <param name="startX">First column outside the plot.</param>
        /// <param name="startY">Surface height of the plot's last column.</param>
        /// <param name="step">-1 to walk left, +1 to walk right.</param>
        /// <param name="level">Reference depth for the foundation fill.</param>
        private static int BlendSkirt(
            int startX,
            int startY,
            int step,
            int level)
        {
            int y = startY;


            for (int n = 0; n < MaxSkirt; n++)
            {
                int x = startX + n * step;

                if (x < 20 || x >= Main.maxTilesX - 20)
                    return n;


                if (!Surface(x, out int ground, out _))
                    return n;


                // Ground is at or above us — the hill has run into rising
                // land and there is nothing left to blend.
                if (ground <= y)
                    return n;


                // Close enough to read as ordinary terrain.
                if (ground - y <= BlendTolerance)
                    return n;


                // One tile down per column. Anything steeper is just a
                // smaller version of the ledge being fixed.
                y++;

                ShapeColumn(x, y, level);
            }


            return MaxSkirt;
        }


        /// <summary>
        /// Clears the air above a column and packs it solid below, in the
        /// biome's own materials. The same work Shave does per column,
        /// factored out so the skirt builds ground identical to the hill's.
        /// </summary>
        private static void ShapeColumn(
            int x,
            int surface,
            int level)
        {
            int clearTop =
                Math.Max(10, surface - HeadRoom);

            for (int y = clearTop; y < surface; y++)
            {
                Tile air = Main.tile[x, y];

                air.ClearTile();
                air.LiquidAmount = 0;
            }


            int floor =
                Math.Min(
                    Main.maxTilesY - 20,
                    Math.Max(level, surface) + Foundation);

            for (int y = surface; y <= floor; y++)
            {
                Tile tile = Main.tile[x, y];

                tile.HasTile = true;
                tile.TileType = (ushort)fillTile;
                tile.Slope = SlopeType.Solid;
                tile.IsHalfBlock = false;
                tile.LiquidAmount = 0;
            }


            Tile cap = Main.tile[x, surface];

            cap.HasTile = true;
            cap.TileType = (ushort)capTile;
            cap.Slope = SlopeType.Solid;
            cap.IsHalfBlock = false;
            cap.LiquidAmount = 0;
        }


        private static void PlaceAngel(
            int plotLeft,
            int[] heights,
            bool[] plantable)
        {
            Item sample =
                ContentSamples.ItemsByType[ItemID.AngelStatue];

            if (sample.createTile <= 0)
                return;


            int from =
                (int)(PlotWidth * 0.34f);

            int to =
                (int)(PlotWidth * 0.66f);


            // A handful of tries rather than one, because a column can be
            // unusable for reasons the mask does not know about.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int index =
                    WorldGen.genRand.Next(from, to);

                if (!plantable[index])
                    continue;


                int x = plotLeft + index;
                int y = heights[index] - 1;

                if (y < 12)
                    continue;


                // A statue is two wide, so both columns have to be clear.
                if (Main.tile[x, y].HasTile ||
                    Main.tile[x + 1, y].HasTile)
                {
                    continue;
                }


                WorldGen.PlaceObject(
                    x,
                    y,
                    sample.createTile,
                    true,
                    sample.placeStyle);

                if (Main.tile[x, y].HasTile)
                    return;
            }
        }


        private static void Plant(
            int plotLeft,
            int[] heights,
            bool[] plantable)
        {
            int wanted =
                WorldGen.genRand.Next(
                    MinStones,
                    MaxStones + 1);


            List<int> placedX =
                new List<int>();


            int attempts = 0;


            while (placedX.Count < wanted &&
                   attempts < 200)
            {
                attempts++;


                // Clustered on the crown, not scattered across the whole
                // hill. Spread over the full width the stones end up
                // stranded halfway down both slopes and read as litter;
                // kept to the middle they read as one graveyard.
                int reach =
                    (int)(PlotWidth * GraveSpread * 0.5f);

                int centre = PlotWidth / 2;

                int index =
                    WorldGen.genRand.Next(
                        Math.Max(FenceInset + 2, centre - reach),
                        Math.Min(PlotWidth - FenceInset - 2, centre + reach));


                // The profile says which columns have a level partner to
                // the right. A tombstone is two tiles wide and will not
                // place across a riser, so asking the profile beats the
                // old guess of snapping to an even column.
                if (!plantable[index])
                    continue;


                int x =
                    plotLeft + index;


                // ----------------------------------------------------
                // Random spacing.
                // ----------------------------------------------------

                bool tooClose = false;


                foreach (int existingX in placedX)
                {
                    int distance =
                        Math.Abs(
                            x - existingX);


                    if (distance <
                        MinStoneSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }


                if (tooClose)
                    continue;


                int surface =
                    heights[index];


                if (surface <= 1)
                    continue;


                // ----------------------------------------------------
                // Random tombstone.
                // ----------------------------------------------------

                int item =
                    Markers[
                        WorldGen.genRand.Next(
                            Markers.Length)];


                Item sample =
                    ContentSamples.ItemsByType[
                        item];


                if (sample.createTile <= 0)
                    continue;


                // ----------------------------------------------------
                // Make sure there is room above the ground.
                // ----------------------------------------------------
                //
                // BOTH columns, because a marker is two wide. Checking
                // only the anchor let a tombstone try to grow through the
                // right-hand half of the angel statue; PlaceObject would
                // refuse and the attempt was wasted silently.

                if (Main.tile[
                        x,
                        surface - 1]
                    .HasTile)
                {
                    continue;
                }


                if (Main.tile[
                        x + 1,
                        surface - 1]
                    .HasTile)
                {
                    continue;
                }


                // ----------------------------------------------------
                // Place it.
                // ----------------------------------------------------

                WorldGen.PlaceObject(
                    x,
                    surface - 1,
                    sample.createTile,
                    true,
                    sample.placeStyle);


                if (Main.tile[
                        x,
                        surface - 1]
                    .HasTile)
                {
                    placedX.Add(x);
                }
            }


            lastStonesPlaced = placedX.Count;


            // The span the fence will wrap. Recorded as PLOT indices, not
            // world coordinates, because that is what BuildFence works in.
            lastStoneMin = int.MaxValue;
            lastStoneMax = int.MinValue;

            foreach (int stoneX in placedX)
            {
                int index = stoneX - plotLeft;

                lastStoneMin = Math.Min(lastStoneMin, index);
                lastStoneMax = Math.Max(lastStoneMax, index);
            }
        }


        // ============================================================
        // FENCE
        // ============================================================

        /// <summary>
        /// Builds an irregular wooden fence around the cemetery.
        ///
        /// The fence follows the actual hill profile rather than being
        /// placed at one constant Y coordinate.
        /// </summary>
        private static void BuildFence(
            int plotLeft,
            int[] heights)
        {
            // --------------------------------------------------------
            // Wrap the graves, not the hill.
            // --------------------------------------------------------
            //
            // The fence used to span the whole plot regardless of where
            // the markers actually landed, so a graveyard whose stones
            // clustered near the middle got a rail running off across
            // empty ground on both sides. Reading the span back from
            // Plant ties the enclosure to its contents.
            //
            // lastStoneMax is an anchor column and a marker is two wide,
            // so the far edge is one past it before the margin is added.

            int left =
                Math.Max(
                    FenceInset,
                    lastStoneMin - FenceMargin);

            int right =
                Math.Min(
                    PlotWidth - FenceInset - 1,
                    lastStoneMax + 1 + FenceMargin);


            // Nothing was planted — there is nothing to enclose.
            if (lastStoneMin > lastStoneMax ||
                right - left < MinFenceSpan)
            {
                return;
            }


            // --------------------------------------------------------
            // Decide where the entrance is.
            // --------------------------------------------------------

            int entrance =
                WorldGen.genRand.Next(
                    left + 5,
                    right - 5);


            int entranceWidth =
                WorldGen.genRand.Next(
                    2,
                    4);


            // --------------------------------------------------------
            // Left and right fence edges.
            // --------------------------------------------------------

            BuildFenceSection(
                plotLeft,
                heights,
                left,
                entrance - entranceWidth,
                false);


            BuildFenceSection(
                plotLeft,
                heights,
                entrance + entranceWidth,
                right,
                false);


            // --------------------------------------------------------
            // Small secondary gaps make it look old.
            // --------------------------------------------------------

            int gapStart = -1;
            int run = 0;


            for (int i = left;
                 i <= right;
                 i++)
            {
                if (i >= entrance - entranceWidth &&
                    i <= entrance + entranceWidth)
                {
                    continue;
                }


                run++;


                if (run >= FenceMinRun &&
                    WorldGen.genRand.NextBool(
                        FenceGapChance,
                        100))
                {
                    gapStart = i;
                    break;
                }
            }


            if (gapStart >= 0)
            {
                int gapLength =
                    WorldGen.genRand.Next(
                        1,
                        3);


                for (int i = gapStart;
                     i < gapStart + gapLength &&
                     i <= right;
                     i++)
                {
                    RemoveFence(
                        plotLeft + i,
                        heights[i]);
                }
            }


            // --------------------------------------------------------
            // Add a few short fence posts extending irregularly.
            // --------------------------------------------------------

            for (int i = left;
                 i <= right;
                 i++)
            {
                if (WorldGen.genRand.NextBool(18))
                {
                    int x =
                        plotLeft + i;

                    int y =
                        heights[i] - 1;


                    if (y > 10)
                    {
                        WorldGen.PlaceWall(
                            x,
                            y,
                            FenceWall(),
                            true);
                    }
                }
            }


            // --------------------------------------------------------
            // Frame fence.
            // --------------------------------------------------------

            for (int x = plotLeft + left;
                 x <= plotLeft + right;
                 x++)
            {
                int index =
                    x - plotLeft;

                int y =
                    heights[index];


                if (y > 10)
                {
                    WorldGen.SquareTileFrame(
                        x,
                        y - 1);
                }
            }
        }


        // ============================================================
        // FENCE SECTION
        // ============================================================

        /// <summary>
        /// Which fence wall this panel is made of: mostly Wrought Iron,
        /// occasionally plain Iron.
        ///
        /// Both are resolved through the item rather than named as a
        /// WallID, for the same reason the tombstone styles are — the
        /// wall a fence item places is an undocumented number, and asking
        /// the item is the only way to be certain of getting it right.
        /// </summary>
        private static int FenceWall()
        {
            int item =
                WorldGen.genRand.NextBool(IronFenceRarity)
                    ? ItemID.IronFence
                    : ItemID.WroughtIronFence;

            return ContentSamples.ItemsByType[item].createWall;
        }


        private static void BuildFenceSection(
            int plotLeft,
            int[] heights,
            int start,
            int end,
            bool unused)
        {
            if (end < start)
                return;


            // Runs and gaps, rather than a flat per-tile skip chance.
            // Starting mid-run by a random amount stops every section
            // beginning with a full-length stretch.
            int runLength =
                WorldGen.genRand.Next(
                    MinFenceRun,
                    MaxFenceRun + 1);

            int placedInRun =
                WorldGen.genRand.Next(runLength);

            int skipLeft = 0;


            for (int i = start;
                 i <= end;
                 i++)
            {
                if (skipLeft > 0)
                {
                    skipLeft--;
                    continue;
                }


                if (placedInRun >= runLength)
                {
                    skipLeft =
                        WorldGen.genRand.Next(
                            MinFenceGap,
                            MaxFenceGap + 1);

                    placedInRun = 0;

                    runLength =
                        WorldGen.genRand.Next(
                            MinFenceRun,
                            MaxFenceRun + 1);

                    continue;
                }


                placedInRun++;


                int x =
                    plotLeft + i;


                int y =
                    heights[i] - 1;


                if (x < 1 ||
                    x >= Main.maxTilesX - 1)
                {
                    continue;
                }


                if (y < 10 ||
                    y >= Main.maxTilesY - 1)
                {
                    continue;
                }


                // Don't overwrite a tombstone/object.
                if (Main.tile[x, y].HasTile)
                    continue;


                WorldGen.PlaceWall(
                    x,
                    y,
                    FenceWall(),
                    true);
            }
        }


        // ============================================================
        // REMOVE FENCE
        // ============================================================

        private static void RemoveFence(
    int x,
    int surface)
        {
            int y = surface - 1;

            if (x < 1 ||
                x >= Main.maxTilesX - 1)
            {
                return;
            }

            if (y < 10 ||
                y >= Main.maxTilesY - 1)
            {
                return;
            }

            Tile tile = Main.tile[x, y];

            // A fence is a WALL, not a tile.
            //
            // Both kinds have to be checked now, and by the same item
            // lookup that placed them — hardcoding one WallID here would
            // silently leave every Iron panel standing.
            int wrought =
                ContentSamples.ItemsByType[ItemID.WroughtIronFence].createWall;

            int iron =
                ContentSamples.ItemsByType[ItemID.IronFence].createWall;

            if (tile.WallType == wrought ||
                tile.WallType == iron)
            {
                tile.WallType = WallID.None;
            }
        }
    }
} 