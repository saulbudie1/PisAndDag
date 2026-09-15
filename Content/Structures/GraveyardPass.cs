using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Raises a graveyard hill behind one of the ocean beaches.
    ///
    /// A real hill: sixteen to twenty-two tiles above the land, a hundred
    /// and ten wide, crown pushed off-centre so one flank is the long one,
    /// with a little noise on the slopes so no two are the same silhouette.
    /// The graves sit on the rounded crown inside an iron fence; an angel
    /// and an armoured figure keep them; a worn track climbs the seaward
    /// flank to the gate. One stone stands apart from the rest, outside
    /// the fence, facing the water. One grave is open.
    ///
    /// The whole hill and the land around it is recorded as a quiet zone
    /// (GraveyardZone) where nothing spawns.
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

        // Width of the hill, foot to foot.
        //
        // Wide, because the grade is what the eye reads. With the crown
        // sixteen to twenty tiles up, a hundred and fifty keeps the flanks
        // under one tile of rise per two columns everywhere — a slow climb
        // with a sloped tile on every riser, not a staircase.
        private const int PlotWidth = 150;

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

        private const int MinStones = 7;
        private const int MaxStones = 11;

        // Minimum distance between tombstones.
        private const int MinStoneSpacing = 0;

        // Maximum distance between tombstones.
        private const int MaxStoneSpacing = 4;


        // ============================================================
        // HILL SETTINGS
        // ============================================================

        // Remember: Terraria Y increases DOWNWARD, so rising means
        // subtracting.

        // How many tiles the crown is lifted ABOVE THE LAND'S TREND. A
        // height difference, not an elevation: read as an elevation it
        // once made a mountain. Sixteen to twenty-two is a hill you see
        // from the beach.
        private const int MinCrownRise = 16;
        private const int MaxCrownRise = 20;


        // Share of each half-width that is dead flat before the falloff
        // begins. Small: the cosine falloff starts so gently that the top
        // thirty-odd columns are level to within a tile anyway, and a
        // rounded crown reads as a hill where a shelf reads as a platform.
        private const float FlatFraction = 0.10f;

        // How much of the falloff is raised cosine rather than straight
        // ramp.
        //
        // Back near Mors's half and half. Weighting the cosine made the
        // silhouette bulge, which is what read as "too curved"; the
        // straight ramp is what turns a dome into a long even rise.
        private const float DomeWeight = 0.75f;

        // How far the crown may sit off the middle, as a fraction of the
        // width. A hill with its top dead centre reads as a drawing.
        private const float MaxSkew = 0.12f;

        // Tiles of noise laid on the flanks. Zero at the crown and at the
        // feet (see the envelope in the profile), full strength mid-slope,
        // so the graves still get level ground and the feet still meet
        // the land.
        private const float NoiseAmplitude = 1.2f;

        // Fewest columns between one riser and the next. Two means the
        // integer profile never climbs faster than one tile per two
        // columns, whatever the float curve under it is doing.
        private const int MinRun = 2;

        // Share of risers that get a sloped tile. Vanilla's own smoothing
        // pass leaves half of them square for texture; this leaves fewer,
        // because the hill is the one thing here that is meant to be
        // gentle.
        private const int SlopeChancePercent = 85;

        // Columns the ground is averaged over to find the trend the hill
        // follows. Wide enough to swallow a notch, narrow enough that a
        // real change in the land still shows through.
        private const int TrendRadius = 7;

        // Columns at each end kept clear of markers, so nothing stands on
        // the lip where the hill meets the untouched world.
        private const int EdgeMargin = 10;

        // How big a drop at the foot still reads as ordinary terrain.
        // Above this the skirt keeps stepping outward.
        private const int BlendTolerance = 2;

        // Hard stop on the skirt, so a hill built on the lip of a ravine
        // does not walk a ramp halfway across the world.
        private const int MaxSkirt = 48;

        // Columns either side of the crown the graves may occupy. An
        // absolute number, not a share of the width: the hill got wider
        // and the graveyard is meant to stay the size it was.
        private const int GraveHalfWidth = 22;

        // Where the two figures stand, in columns from the crown — one
        // seaward, one inland, facing each other across the stones.
        private const int StatueNear = 6;
        private const int StatueFar = 18;


        // ============================================================
        // LANDMARKS
        // ============================================================

        // Tiles kept between the hill and the dungeon — its entrance and
        // the whole underground body. The hill was once built against the
        // dungeon's tower, with its foundation packed into the roof.
        private const int LandmarkMargin = 60;

        // Tiles kept between the hill and the spawn point.
        private const int SpawnMargin = 40;

        // Padding asked of the StructureMap, where the other passes and
        // vanilla register what they built.
        private const int LandmarkPadding = 8;


        // ============================================================
        // LIVING TREES
        // ============================================================

        // How far beyond the ground the hill touches to look for a living
        // tree. A giant tree whose trunk stands outside the plot can still
        // hang its canopy over it, and clearing the canopy alone leaves a
        // stripped trunk standing. Finding one tile of it anywhere in this
        // reach takes the whole tree.
        private const int CanopyReach = 60;

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

        // Tallest a fence run may be, in panels stacked up from the
        // ground. Each run rolls its own height, so the rail rises and
        // falls along its length.
        private const int MaxFenceHeight = 3;

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


        // ============================================================
        // DRESSING
        // ============================================================

        // Depth of the open grave, in tiles. Three is a hole you can fall
        // in and climb out of.
        private const int OpenGraveDepth = 3;

        // Chance, per fence panel, of a cobweb hanging on it.
        private const int WebOnFenceDenominator = 6;

        // How far the lone stone stands outside the fence, down the
        // seaward flank.
        private const int LoneStoneOffset = 9;


        // ============================================================
        // EPITAPHS
        // ============================================================
        //
        // Every stone Plant() sets gets "Name - Adjective Role". The lone
        // stone outside the fence is left blank on purpose.

        // Chance, per stone, of a short second line under the name.
        // 0 turns second lines off.
        private const int EpitaphSecondLineDenominator = 3;

        // Vanilla town NPCs whose name pools are men's names, and women's.
        // The names come from vanilla's own NPC name roll.
        private static readonly int[] MenNamePools =
        {
            NPCID.Merchant,
            NPCID.ArmsDealer,
            NPCID.Guide,
            NPCID.Demolitionist,
            NPCID.Clothier,
            NPCID.Wizard,
            NPCID.DyeTrader,
            NPCID.Painter,
            NPCID.Pirate,
            NPCID.TravellingMerchant,
            NPCID.TaxCollector,
            NPCID.DD2Bartender,
            NPCID.Golfer,
        };

        private static readonly int[] WomenNamePools =
        {
            NPCID.Nurse,
            NPCID.Dryad,
            NPCID.Mechanic,
            NPCID.Steampunker,
            NPCID.PartyGirl,
            NPCID.Stylist,
            NPCID.BestiaryGirl,
            NPCID.Princess,
        };

        private static readonly string[] Adjectives =
        {
            "Loving", "Caring", "Prideful", "Devoted", "Beloved", "Faithful",
            "Gentle", "Patient", "Honest", "Humble", "Dutiful", "Cheerful",
            "Stubborn", "Quiet", "Restless", "Stern", "Weary",
        };

        private static readonly string[] MenKin = { "Husband", "Father", "Brother", "Son" };
        private static readonly string[] WomenKin = { "Wife", "Mother", "Sister", "Daughter" };

        private static readonly string[] Trades =
        {
            "Miner", "Farmer", "Fisher", "Smith", "Weaver", "Baker", "Mason",
            "Carter", "Healer", "Scribe", "Hunter", "Shepherd", "Cooper",
            "Tanner", "Brewer", "Chandler", "Sexton", "Gravedigger",
        };

        private static readonly string[] SecondLines =
        {
            "Rest now.",
            "Gone before the frost.",
            "The hill keeps them.",
            "Missed at every table.",
            "Went out with the tide.",
            "Never did learn to swim.",
            "Sleep, and do not wake.",
            "The bell rang once.",
            "Found at dawn. Buried by noon.",
            "The lamp is out.",
        };


        // ============================================================
        // QUIET ZONE
        // ============================================================

        // How far past the hill's feet the silence reaches, in tiles.
        private const int QuietMargin = 60;

        // How far above the crown and below the feet.
        private const int QuietAbove = 80;
        private const int QuietBelow = 40;


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

            int inlandStep = left ? PlotWidth / 2 : -PlotWidth / 2;
            int level = 0;

            // Walks inland until the spot is clear of landmarks. Forcing
            // is for "no good ground", never for "there is a dungeon
            // here": a world without a graveyard beats a graveyard packed
            // into the dungeon's roof.
            for (int attempt = 0; ; attempt++)
            {
                if (attempt >= 12 || plotLeft < 20 || plotLeft + PlotWidth >= Main.maxTilesX - 20)
                {
                    Log("no ground clear of landmarks for a forced graveyard — SKIPPED");
                    return;
                }

                // Deepest ground anywhere in the plot, or the surface
                // layer itself if literally nothing reads.
                level = 0;

                for (int x = plotLeft; x < plotLeft + PlotWidth; x++)
                {
                    if (Surface(x, out int y, out _))
                        level = Math.Max(level, y);
                }

                if (level <= 0)
                    level = (int)Main.worldSurface - 20;

                if (!Landmarked(plotLeft, level))
                    break;

                plotLeft += inlandStep;
            }


            ResolvePalette(TileID.Dirt);

            Shave(
                plotLeft,
                level,
                left);


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
                bestLevel,
                left);


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

        /// <summary>Plot index of the gate the last BuildFence left, or -1.</summary>
        private static int lastEntrance = -1;


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


            // Never on, in, or against anything the world already put
            // here. A landmark is not a score penalty; it is a veto.
            if (Landmarked(plotLeft, level))
                return false;


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
        // LANDMARKS
        // ============================================================

        /// <summary>
        /// Whether building here would touch something the world put
        /// here first. Three checks, from cheap to thorough:
        ///
        /// Coordinates vanilla keeps — the spawn point, the dungeon's
        /// entrance and the span of its body — each with a margin, so the
        /// hill is not merely off the landmark but away from it.
        ///
        /// The StructureMap, where the mod's own passes (the shrine, the
        /// monument) and some of vanilla's register what they built.
        ///
        /// And the tiles themselves, across everything the build would
        /// clear or fill: dungeon brick or wall, temple brick, pyramid
        /// brick. Nothing that can be a landmark is trusted to have
        /// registered itself. Living trees are deliberately NOT in this
        /// list — they are felled whole instead (FellLivingTrees).
        /// </summary>
        private static bool Landmarked(
            int plotLeft,
            int level)
        {
            int left = Math.Max(20, plotLeft - MaxSkirt);
            int right = Math.Min(Main.maxTilesX - 20, plotLeft + PlotWidth + MaxSkirt);
            int top = Math.Max(10, level - MaxCrownRise - HeadRoom - 20);
            int bottom = Math.Min(Main.maxTilesY - 20, level + Foundation + 10);
            int centre = plotLeft + PlotWidth / 2;

            // ---- coordinates -------------------------------------------------------------

            if (Math.Abs(centre - Main.spawnTileX) < PlotWidth / 2 + SpawnMargin)
                return true;

            if (Math.Abs(centre - Main.dungeonX) < PlotWidth / 2 + LandmarkMargin)
                return true;

            if (GenVars.dMaxX > GenVars.dMinX
                && right >= GenVars.dMinX - LandmarkMargin
                && left <= GenVars.dMaxX + LandmarkMargin)
            {
                return true;
            }

            // ---- what other passes registered ----------------------------------------

            if (GenVars.structures != null
                && !GenVars.structures.CanPlace(new Rectangle(left, top, right - left, bottom - top), LandmarkPadding))
            {
                return true;
            }

            // ---- what is actually in the ground --------------------------------------

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile)
                    {
                        int type = tile.TileType;

                        if (Main.tileDungeon[type]
                            || type == TileID.LihzahrdBrick
                            || type == TileID.SandstoneBrick)
                        {
                            return true;
                        }
                    }

                    int wall = tile.WallType;

                    if (wall != WallID.None
                        && (Main.wallDungeon[wall]
                            || wall == WallID.LihzahrdBrick
                            || wall == WallID.SandstoneBrick))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        // ============================================================
        // BUILD TERRAIN
        // ============================================================

        private static void Shave(
            int plotLeft,
            int level,
            bool seaOnLeft)
        {
            // Fell any living tree standing here, whole. A living tree is
            // one connected mass that reaches far above and below the
            // ground it grows from; building through it leaves the canopy
            // hanging with a wedge of hill driven through it.
            FellLivingTrees(plotLeft, level);

            // And the ordinary trees on the ground the hill and its skirts
            // will clear — whole, not topped. HeadRoom used to cut the top
            // off any tree on a skirt column and leave the trunk standing.
            FellTrees(plotLeft, level);

            GenerateHillProfile(
                plotLeft,
                level,
                out int[] heights,
                out bool[] plantable,
                out int crown);

            int crownY = int.MaxValue;

            for (int i = 0; i < PlotWidth; i++)
            {
                ShapeColumn(plotLeft + i, heights[i], level);
                crownY = Math.Min(crownY, heights[i]);
            }

            // Walk the feet out until they meet the world. The profile
            // ends on the land's trend, but the land a few tiles further
            // out can still be well below that, and a ledge there is the
            // one thing that gives the hill away.
            int skirtLeft =
                BlendSkirt(plotLeft - 1, heights[0], -1, level);

            int skirtRight =
                BlendSkirt(plotLeft + PlotWidth, heights[PlotWidth - 1], 1, level);

            // Frame everything touched. The top is taken from the CROWN,
            // not from `level` — level is the lowest ground in the plot,
            // and a hill twenty tiles above it would otherwise have its
            // whole upper half left unframed.
            int frameLeft = Math.Max(1, plotLeft - skirtLeft - 2);
            int frameRight = Math.Min(Main.maxTilesX - 2, plotLeft + PlotWidth + skirtRight + 1);
            int frameTop = Math.Max(10, crownY - HeadRoom - 2);
            int frameBottom = Math.Min(Main.maxTilesY - 2, level + Foundation + 2);

            for (int x = frameLeft; x <= frameRight; x++)
                for (int y = frameTop; y <= frameBottom; y++)
                    WorldGen.SquareTileFrame(x, y);

            // On the map for everyone after us — the same courtesy the
            // shrine and the monument extend to this pass.
            GenVars.structures?.AddProtectedStructure(
                new Rectangle(frameLeft, frameTop, frameRight - frameLeft, frameBottom - frameTop),
                LandmarkPadding);

            // ---- dressing, in an order where each thing takes what it needs first --------

            // The two figures first: they each want one spot on the crown
            // and there are only two of them. The angel on the seaward
            // side, the armoured one inland, facing it across the graves.
            int sea = seaOnLeft ? -1 : 1;

            PlaceStatue(ItemID.AngelStatue, plotLeft, heights, plantable,
                crown + sea * StatueFar, crown + sea * StatueNear);
            PlaceStatue(ItemID.ArmorStatue, plotLeft, heights, plantable,
                crown - sea * StatueNear, crown - sea * StatueFar);

            // Then the hole, so the stones can't be planted over it. Where it
            // is gets remembered: the first burial fills it.
            int openGrave = OpenGrave(plotLeft, heights, plantable, crown);

            Plant(plotLeft, heights, plantable, crown);

            BuildFence(plotLeft, heights);

            // The track climbs from the seaward foot to the gate, so it is
            // laid after the fence knows where the gate is.
            WearPath(plotLeft, heights, seaOnLeft);

            LoneStone(plotLeft, heights, plantable, seaOnLeft);

            Cobwebs(plotLeft, heights);

            // Last, once everything that needs a square block to stand on
            // is standing on one: vanilla's "Smooth World" pass ran long
            // before this one, so the risers are sloped here or not at all.
            SmoothHill(plotLeft - skirtLeft, plotLeft + PlotWidth + skirtRight);

            // ---- the silence ---------------------------------------------------------------

            GraveyardZone.Set(
                plotLeft - QuietMargin,
                plotLeft + PlotWidth + QuietMargin,
                crownY - QuietAbove,
                level + QuietBelow);

            GraveyardZone.SetGraves(
                plotLeft + crown - GraveHalfWidth,
                plotLeft + crown + GraveHalfWidth,
                crownY,
                openGrave < 0 ? -1 : plotLeft + openGrave,
                openGrave < 0 ? -1 : heights[openGrave]);
        }


        // ============================================================
        // HILL PROFILE
        // ============================================================

        /// <summary>
        /// The hill.
        ///
        ///                        _____
        ///                   ____/     \___
        ///              ____/              \____
        ///         ____/                        \______
        ///     ___/                                    \____
        ///
        /// Three things make it a hill and not a bump, a crater or a
        /// mountain — the three things earlier versions each got wrong.
        ///
        /// It is built on the land's TREND, not on the land: the ground
        /// is read, then blurred over TrendRadius columns each side, and
        /// the hill is added to that. A straight line between the feet
        /// (the last version) ignored everything between them; raw ground
        /// (the one before) reproduced every notch as a battlement.
        ///
        /// It is a LIFT off that trend, never an elevation, so nothing
        /// can be dragged up to the plot's highest point, and the feet —
        /// where the lift is zero — are the trend, which is the land.
        ///
        /// And it is allowed to cut. The old "never dig" rule meant that
        /// wherever the land already bulged above the arc the arc simply
        /// wasn't there, and on rough shore ground that was most of it.
        /// The shape is ours from foot to foot now; what was under it is
        /// packed solid by ShapeColumn either way.
        ///
        /// The crown is pushed off-centre by up to MaxSkew, so one flank
        /// is longer and shallower than the other, and two low sines are
        /// laid on the slopes — faded to nothing at the crown, where the
        /// graves need level pairs, and at the feet, where the land needs
        /// to be met exactly.
        /// </summary>
        private static void GenerateHillProfile(
            int plotLeft,
            int level,
            out int[] heights,
            out bool[] plantable,
            out int crown)
        {
            heights = new int[PlotWidth];
            plantable = new bool[PlotWidth];

            // ---- the land, then its trend ---------------------------------------------

            int[] ground = new int[PlotWidth];

            for (int i = 0; i < PlotWidth; i++)
            {
                ground[i] =
                    Surface(plotLeft + i, out int y, out _)
                        ? y
                        : level;
            }

            float[] trend = new float[PlotWidth];

            for (int i = 0; i < PlotWidth; i++)
                trend[i] = AverageGround(ground, i - TrendRadius, i + TrendRadius + 1);

            // ---- the hill's own numbers -----------------------------------------------

            int lift = WorldGen.genRand.Next(MinCrownRise, MaxCrownRise + 1);

            float skew = WorldGen.genRand.NextFloat(-MaxSkew, MaxSkew);
            float crownX = (PlotWidth - 1) * (0.5f + skew);

            crown = (int)Math.Round(crownX);

            float phaseA = WorldGen.genRand.NextFloat(MathF.PI * 2f);
            float phaseB = WorldGen.genRand.NextFloat(MathF.PI * 2f);
            float cyclesA = WorldGen.genRand.NextFloat(1.6f, 2.6f);
            float cyclesB = WorldGen.genRand.NextFloat(4f, 6f);

            float[] wanted = new float[PlotWidth];

            for (int i = 0; i < PlotWidth; i++)
            {
                float along = i / (float)(PlotWidth - 1);

                // 0 at the crown, 1 at whichever foot this column is on
                // the way to. The two halves have different lengths, which
                // is the whole of the asymmetry.
                float half = i < crownX ? crownX : (PlotWidth - 1) - crownX;
                float t = half <= 0f ? 1f : Math.Abs(i - crownX) / half;

                float shape;

                if (t <= FlatFraction)
                {
                    shape = 1f;
                }
                else
                {
                    float u = (t - FlatFraction) / (1f - FlatFraction);
                    float straight = 1f - u;
                    float eased = 0.5f + 0.5f * MathF.Cos(u * MathF.PI);

                    shape = DomeWeight * eased + (1f - DomeWeight) * straight;
                }

                // Two sines, one slow and one quicker, on an envelope that
                // is zero at the crown and the feet and one halfway down.
                float noise =
                    (MathF.Sin(along * cyclesA * MathF.PI * 2f + phaseA)
                    + 0.5f * MathF.Sin(along * cyclesB * MathF.PI * 2f + phaseB)) / 1.5f;

                float envelope = 4f * shape * (1f - shape);

                float bump = NoiseAmplitude * noise * envelope;

                // Y grows downward: lifting is subtracting.
                wanted[i] = trend[i] - lift * shape - bump;
            }

            // ---- whole tiles, one riser per MinRun columns at most ---------------------
            //
            // Rounding the curve column by column gave a staircase wherever
            // the slope passed a tile per column, and a 45° staircase is
            // what a tile game shows as a cliff. This walks out from the
            // crown instead and lets the integer profile take a step only
            // when the curve has pulled a full tile away AND the last step
            // is at least MinRun columns back. The curve is designed to
            // stay under that grade, so the lag never exceeds a tile or so;
            // where it briefly doesn't, the profile falls behind and
            // catches up on the next gentler stretch.

            heights[crown] = (int)Math.Round(wanted[crown]);

            Quantise(wanted, heights, crown, 1);
            Quantise(wanted, heights, crown, -1);

            // ---- where a two-wide object can stand ---------------------------------------

            for (int i = 0; i < PlotWidth; i++)
            {
                bool hasPartner = i + 1 < PlotWidth && heights[i + 1] == heights[i];
                bool clearOfEdge = i >= EdgeMargin && i < PlotWidth - EdgeMargin;

                plantable[i] = hasPartner && clearOfEdge;
            }
        }


        /// <summary>
        /// Walks from the crown to one foot, stepping the integer profile
        /// toward the curve one tile at a time and never sooner than
        /// MinRun columns after the last step.
        /// </summary>
        private static void Quantise(
            float[] wanted,
            int[] heights,
            int crown,
            int direction)
        {
            int lastStep = crown - MinRun * direction * 2;

            for (int i = crown + direction; i >= 0 && i < PlotWidth; i += direction)
            {
                int previous = heights[i - direction];
                float pull = wanted[i] - previous;

                bool due = Math.Abs(i - lastStep) >= MinRun;

                if (due && Math.Abs(pull) >= 1f)
                {
                    heights[i] = previous + Math.Sign(pull);
                    lastStep = i;
                }
                else
                {
                    heights[i] = previous;
                }
            }
        }


        /// <summary>
        /// Puts a sloped tile on every riser so the flanks read as slopes
        /// rather than steps. The rules are vanilla's own, from its
        /// "Smooth World" pass: a solid tile with solid ground under it,
        /// solid to one side and air to the other, is cut toward the air.
        /// Never a tile with something standing on it — a stone or statue
        /// anchors to a square block and would come loose.
        /// </summary>
        private static void SmoothHill(
            int from,
            int to)
        {
            from = Math.Max(21, from);
            to = Math.Min(Main.maxTilesX - 21, to);

            for (int x = from; x <= to; x++)
            {
                if (!Surface(x, out int y, out _) || y < 12)
                    continue;

                if (Main.tile[x, y - 1].HasTile)
                    continue;

                if (!WorldGen.SolidTile(x, y + 1))
                    continue;

                if (!WorldGen.genRand.NextBool(SlopeChancePercent, 100))
                    continue;

                bool leftSolid = WorldGen.SolidTile(x - 1, y);
                bool rightSolid = WorldGen.SolidTile(x + 1, y);

                if (rightSolid && !leftSolid)
                    WorldGen.SlopeTile(x, y, 2);
                else if (leftSolid && !rightSolid)
                    WorldGen.SlopeTile(x, y, 1);
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
                Math.Max(20, plotLeft - MaxSkirt - CanopyReach);

            int to =
                Math.Min(Main.maxTilesX - 20, plotLeft + PlotWidth + MaxSkirt + CanopyReach);

            int top =
                Math.Max(10, level - MaxCrownRise - HeadRoom - LivingTreeReach);

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


        /// <summary>
        /// Clears every ordinary tree — forest, palm, mushroom, gem, the
        /// vanity kinds — standing on ground the build will touch, from the
        /// band the hill can reach down to the foundation. Vanilla's tree
        /// trunk set, so a tree added in a later version is still a tree.
        /// </summary>
        private static void FellTrees(
            int plotLeft,
            int level)
        {
            int from = Math.Max(20, plotLeft - MaxSkirt);
            int to = Math.Min(Main.maxTilesX - 20, plotLeft + PlotWidth + MaxSkirt);
            int top = Math.Max(10, level - MaxCrownRise - HeadRoom - 40);
            int bottom = Math.Min(Main.maxTilesY - 20, level + Foundation);

            for (int x = from; x <= to; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && TileID.Sets.IsATreeTrunk[tile.TileType])
                        tile.ClearTile();
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


        /// <summary>
        /// Stands one statue on a level pair of columns somewhere in the
        /// given slice of the plot. A handful of tries rather than one,
        /// because a column can be unusable for reasons the mask does not
        /// know about.
        /// </summary>
        private static void PlaceStatue(
            int itemId,
            int plotLeft,
            int[] heights,
            bool[] plantable,
            int a,
            int b)
        {
            Item sample = ContentSamples.ItemsByType[itemId];

            if (sample.createTile <= 0)
                return;

            int from = Math.Clamp(Math.Min(a, b), EdgeMargin, PlotWidth - EdgeMargin - 1);
            int to = Math.Clamp(Math.Max(a, b) + 1, from + 1, PlotWidth - EdgeMargin);

            for (int attempt = 0; attempt < 24; attempt++)
            {
                int index = WorldGen.genRand.Next(from, to);

                if (!plantable[index])
                    continue;

                int x = plotLeft + index;
                int y = heights[index] - 1;

                if (y < 12)
                    continue;

                // Two wide, so both columns have to be clear.
                if (Main.tile[x, y].HasTile || Main.tile[x + 1, y].HasTile)
                    continue;

                WorldGen.PlaceObject(x, y, sample.createTile, true, sample.placeStyle);

                if (Main.tile[x, y].HasTile)
                {
                    // Nothing else stands on or against it.
                    for (int k = index - 1; k <= index + 2; k++)
                        if (k >= 0 && k < PlotWidth)
                            plantable[k] = false;

                    return;
                }
            }
        }


        /// <summary>
        /// One grave with nothing in it: a two-wide, three-deep hole on the
        /// crown, dirt-walled, no stone. Dug before the stones go down so
        /// none of them is planted across it.
        /// </summary>
        private static int OpenGrave(
            int plotLeft,
            int[] heights,
            bool[] plantable,
            int crown)
        {
            int reach = GraveHalfWidth;
            int centre = crown;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                int index = WorldGen.genRand.Next(centre - reach, centre + reach);

                if (index < EdgeMargin + 2 || index >= PlotWidth - EdgeMargin - 3)
                    continue;

                if (!plantable[index])
                    continue;

                int x = plotLeft + index;
                int top = heights[index];

                if (top < 12 || top + OpenGraveDepth >= Main.maxTilesY - 20)
                    continue;

                // Nothing standing on either lip.
                if (Main.tile[x, top - 1].HasTile || Main.tile[x + 1, top - 1].HasTile)
                    continue;

                for (int dx = 0; dx < 2; dx++)
                {
                    for (int dy = 0; dy < OpenGraveDepth; dy++)
                    {
                        Tile tile = Main.tile[x + dx, top + dy];

                        tile.ClearTile();
                        tile.LiquidAmount = 0;
                        tile.WallType = WallID.DirtUnsafe;
                    }
                }

                // The lips and the hole itself are off-limits to everything
                // that comes after.
                for (int k = index - 2; k <= index + 3; k++)
                    if (k >= 0 && k < PlotWidth)
                        plantable[k] = false;

                return index;
            }

            return -1;
        }


        /// <summary>
        /// A worn track from the seaward foot up to the gate: the surface
        /// tile of each column on the way swapped for clay, which reads as
        /// bare packed earth and, unlike dirt, never grows grass back over
        /// itself. The track wanders a column either way now and then so
        /// it is not a ruled line up the slope.
        /// </summary>
        private static void WearPath(
            int plotLeft,
            int[] heights,
            bool seaOnLeft)
        {
            // Nowhere to go if there is no gate.
            if (lastEntrance < 0)
                return;

            int from = seaOnLeft ? 2 : PlotWidth - 3;
            int to = lastEntrance;
            int step = seaOnLeft ? 1 : -1;

            if ((to - from) * step <= 0)
                return;

            for (int i = from; i != to; i += step)
            {
                int x = plotLeft + i;
                int y = heights[i];

                if (y < 12)
                    continue;

                Tile tile = Main.tile[x, y];

                if (!tile.HasTile || !Main.tileSolid[tile.TileType])
                    continue;

                tile.TileType = TileID.ClayBlock;

                // A second tile of width every so often, so the track has
                // a ragged edge rather than a stitched one.
                if (WorldGen.genRand.NextBool(3) && y + 1 < Main.maxTilesY - 20)
                {
                    Tile under = Main.tile[x, y + 1];

                    if (under.HasTile && Main.tileSolid[under.TileType])
                        under.TileType = TileID.ClayBlock;
                }
            }
        }


        /// <summary>
        /// The one that stands apart: a plain stone outside the fence, down
        /// the seaward flank, facing the water. No name on it — none
        /// remember his.
        /// </summary>
        private static void LoneStone(
            int plotLeft,
            int[] heights,
            bool[] plantable,
            bool seaOnLeft)
        {
            if (lastStoneMin > lastStoneMax)
                return;

            Item sample = ContentSamples.ItemsByType[ItemID.Tombstone];

            if (sample.createTile <= 0)
                return;

            int fenceEdge = seaOnLeft
                ? Math.Max(FenceInset, lastStoneMin - FenceMargin)
                : Math.Min(PlotWidth - FenceInset - 1, lastStoneMax + 1 + FenceMargin);

            int want = seaOnLeft ? fenceEdge - LoneStoneOffset : fenceEdge + LoneStoneOffset;

            // Nearest level pair to where it wants to be, searching outward.
            for (int d = 0; d < 8; d++)
            {
                foreach (int index in new[] { want - d, want + d })
                {
                    if (index < EdgeMargin || index >= PlotWidth - EdgeMargin)
                        continue;

                    if (!plantable[index])
                        continue;

                    int x = plotLeft + index;
                    int y = heights[index] - 1;

                    if (y < 12 || Main.tile[x, y].HasTile || Main.tile[x + 1, y].HasTile)
                        continue;

                    WorldGen.PlaceObject(x, y, sample.createTile, true, sample.placeStyle);

                    if (Main.tile[x, y].HasTile)
                    {
                        plantable[index] = false;
                        return;
                    }
                }
            }
        }


        /// <summary>
        /// Cobwebs: one on a fence panel here and there, and one in the
        /// crook of anything standing on the crown. Placed last, into
        /// empty tiles only, so nothing is ever webbed over.
        /// </summary>
        private static void Cobwebs(
            int plotLeft,
            int[] heights)
        {
            int wrought = ContentSamples.ItemsByType[ItemID.WroughtIronFence].createWall;
            int iron = ContentSamples.ItemsByType[ItemID.IronFence].createWall;

            for (int i = 1; i < PlotWidth - 1; i++)
            {
                int x = plotLeft + i;
                int y = heights[i] - 1;

                if (y < 12)
                    continue;

                Tile tile = Main.tile[x, y];

                bool onFence = !tile.HasTile
                    && (tile.WallType == wrought || tile.WallType == iron)
                    && WorldGen.genRand.NextBool(WebOnFenceDenominator);

                // Against a statue or a stone: the empty tile beside a
                // standing object, at ground level.
                bool againstSomething = !tile.HasTile
                    && (Standing(x - 1, y) || Standing(x + 1, y))
                    && WorldGen.genRand.NextBool(2);

                if (onFence || againstSomething)
                    WorldGen.PlaceTile(x, y, TileID.Cobweb, true);
            }
        }


        private static bool Standing(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            return tile.HasTile
                && (tile.TileType == TileID.Tombstones || tile.TileType == TileID.Statues);
        }


        // ------------------------------------------------------------
        // Epitaphs.
        // ------------------------------------------------------------
        //
        // Tombstones are sign tiles. ReadSign finds the stone's top-left
        // from the tile's frame and makes the sign record if none exists
        // (vanilla does the same when a dropped stone lands); TextSign
        // writes it. Main.sign is saved with the world and sent to clients
        // with the tile section, so nothing else is needed.

        private static void Inscribe(int x, int y)
        {
            int sign = Sign.ReadSign(x, y, true);

            if (sign >= 0)
                Sign.TextSign(sign, Epitaph());
        }


        /// <summary>"Name - Adjective Role", the role matched to the name's sex.</summary>
        private static string Epitaph()
        {
            bool man = WorldGen.genRand.NextBool();

            int[] pools = man ? MenNamePools : WomenNamePools;

            // 1.4.4's name roll is an instance method (1.4.5 made it static
            // with a type argument), so a throwaway NPC of the right type
            // does the asking. It is never spawned.
            NPC namesake = new NPC();
            namesake.SetDefaults(pools[WorldGen.genRand.Next(pools.Length)]);
            string name = namesake.getNewNPCName();

            string adjective = Adjectives[WorldGen.genRand.Next(Adjectives.Length)];

            // Half kin, half trade.
            string role;

            if (WorldGen.genRand.NextBool())
            {
                string[] kin = man ? MenKin : WomenKin;
                role = kin[WorldGen.genRand.Next(kin.Length)];
            }
            else
            {
                role = Trades[WorldGen.genRand.Next(Trades.Length)];
            }

            string text = name + " - " + adjective + " " + role;

            if (EpitaphSecondLineDenominator > 0
                && WorldGen.genRand.NextBool(EpitaphSecondLineDenominator))
            {
                text += "\n" + SecondLines[WorldGen.genRand.Next(SecondLines.Length)];
            }

            return text;
        }


        private static void Plant(
            int plotLeft,
            int[] heights,
            bool[] plantable,
            int crown)
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
                int reach = GraveHalfWidth;

                int centre = crown;

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
                    Inscribe(x, surface - 1);
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


            lastEntrance = -1;

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

            lastEntrance = entrance;


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
                        int wall = FenceWall();
                        int height = FenceHeight();

                        for (int k = 0; k < height; k++)
                        {
                            if (Main.tile[x, y - k].HasTile)
                                break;

                            WorldGen.PlaceWall(x, y - k, wall, true);
                        }
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

            // Each run has its own height, so the rail rises and falls.
            int height = FenceHeight();


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

                    height = FenceHeight();

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


                // Stacked up from the ground, stopping at the first tile
                // that is not empty air.
                int wall = FenceWall();

                for (int k = 0; k < height; k++)
                {
                    int yy = y - k;

                    if (yy < 10 || Main.tile[x, yy].HasTile)
                        break;

                    WorldGen.PlaceWall(x, yy, wall, true);
                }
            }
        }


        /// <summary>
        /// How tall a fence run is. Mostly one or two panels; a full three
        /// now and then, where someone once cared enough to build it up.
        /// </summary>
        private static int FenceHeight()
        {
            int roll = WorldGen.genRand.Next(10);

            if (roll < 4)
                return 1;

            if (roll < 8)
                return Math.Min(2, MaxFenceHeight);

            return MaxFenceHeight;
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

            // A fence is a WALL, not a tile.
            //
            // Both kinds have to be checked, and by the same item lookup
            // that placed them — hardcoding one WallID here would silently
            // leave every Iron panel standing. The whole stack goes, since
            // a run can be up to MaxFenceHeight panels tall.
            int wrought =
                ContentSamples.ItemsByType[ItemID.WroughtIronFence].createWall;

            int iron =
                ContentSamples.ItemsByType[ItemID.IronFence].createWall;

            for (int k = 0; k < MaxFenceHeight; k++)
            {
                if (y - k < 10)
                    break;

                Tile tile = Main.tile[x, y - k];

                if (tile.WallType == wrought ||
                    tile.WallType == iron)
                {
                    tile.WallType = WallID.None;
                }
            }
        }
    }


    /// <summary>
    /// The quiet. A rectangle of tiles around the graveyard, saved with the
    /// world, inside which nothing spawns — no zombies, no eyes, none of the
    /// ghosts and ravens the vanilla graveyard biome would otherwise send.
    /// The mist and the music still come; the biome is the tombstones' doing
    /// and is left alone. Only the company is kept away.
    /// </summary>
    public class GraveyardZone : ModSystem
    {
        public static int Left = -1;
        public static int Right = -1;
        public static int Top = -1;
        public static int Bottom = -1;

        /// <summary>Columns the graves occupy, and the crown's surface row.</summary>
        public static int GraveLeft = -1;
        public static int GraveRight = -1;
        public static int CrownY = -1;

        /// <summary>Left column and lip row of the open grave, or -1 once filled / never dug.</summary>
        public static int OpenGraveX = -1;
        public static int OpenGraveY = -1;

        /// <summary>
        /// Every player death in this world, from the first. Saved, and never
        /// reset: the burials stop after the first few, the count does not.
        /// </summary>
        public static int Deaths;

        public static bool Exists => Right > Left && Bottom > Top;

        public static bool HasGraves => GraveRight > GraveLeft && CrownY > 0;

        public static void SetGraves(int left, int right, int crownY, int openX, int openY)
        {
            GraveLeft = left;
            GraveRight = right;
            CrownY = crownY;
            OpenGraveX = openX;
            OpenGraveY = openY;
        }

        /// <summary>Recorded by the pass, in tile coordinates.</summary>
        public static void Set(int left, int right, int top, int bottom)
        {
            Left = Math.Max(0, left);
            Right = Math.Min(Main.maxTilesX - 1, right);
            Top = Math.Max(0, top);
            Bottom = Math.Min(Main.maxTilesY - 1, bottom);
        }

        public static bool Contains(Player player)
        {
            if (!Exists)
                return false;

            int x = (int)(player.Center.X / 16f);
            int y = (int)(player.Center.Y / 16f);

            return x >= Left && x <= Right && y >= Top && y <= Bottom;
        }

        public override void ClearWorld()
        {
            Left = Right = Top = Bottom = -1;
            GraveLeft = GraveRight = CrownY = -1;
            OpenGraveX = OpenGraveY = -1;
            Deaths = 0;
        }

        public override void SaveWorldData(Terraria.ModLoader.IO.TagCompound tag)
        {
            tag["GraveyardDeaths"] = Deaths;

            if (!Exists)
                return;

            tag["GraveyardLeft"] = Left;
            tag["GraveyardRight"] = Right;
            tag["GraveyardTop"] = Top;
            tag["GraveyardBottom"] = Bottom;
            tag["GraveyardGraveLeft"] = GraveLeft;
            tag["GraveyardGraveRight"] = GraveRight;
            tag["GraveyardCrownY"] = CrownY;
            tag["GraveyardOpenX"] = OpenGraveX;
            tag["GraveyardOpenY"] = OpenGraveY;
        }

        public override void LoadWorldData(Terraria.ModLoader.IO.TagCompound tag)
        {
            Deaths = tag.ContainsKey("GraveyardDeaths") ? tag.GetInt("GraveyardDeaths") : 0;

            if (!tag.ContainsKey("GraveyardLeft"))
                return;

            Left = tag.GetInt("GraveyardLeft");
            Right = tag.GetInt("GraveyardRight");
            Top = tag.GetInt("GraveyardTop");
            Bottom = tag.GetInt("GraveyardBottom");

            // Worlds saved before the graves were recorded simply have none
            // to bury in; the quiet still works.
            if (tag.ContainsKey("GraveyardGraveLeft"))
            {
                GraveLeft = tag.GetInt("GraveyardGraveLeft");
                GraveRight = tag.GetInt("GraveyardGraveRight");
                CrownY = tag.GetInt("GraveyardCrownY");
                OpenGraveX = tag.GetInt("GraveyardOpenX");
                OpenGraveY = tag.GetInt("GraveyardOpenY");
            }
        }

        public override void NetSend(System.IO.BinaryWriter writer)
        {
            writer.Write(Left);
            writer.Write(Right);
            writer.Write(Top);
            writer.Write(Bottom);
            writer.Write(GraveLeft);
            writer.Write(GraveRight);
            writer.Write(CrownY);
            writer.Write(OpenGraveX);
            writer.Write(OpenGraveY);
            writer.Write(Deaths);
        }

        public override void NetReceive(System.IO.BinaryReader reader)
        {
            Left = reader.ReadInt32();
            Right = reader.ReadInt32();
            Top = reader.ReadInt32();
            Bottom = reader.ReadInt32();
            GraveLeft = reader.ReadInt32();
            GraveRight = reader.ReadInt32();
            CrownY = reader.ReadInt32();
            OpenGraveX = reader.ReadInt32();
            OpenGraveY = reader.ReadInt32();
            Deaths = reader.ReadInt32();
        }
    }


    /// <summary>
    /// Nothing spawns on a player standing in the quiet. maxSpawns is what
    /// vanilla checks against the count of NPCs already near the player
    /// before rolling a spawn at all, so zero is not a low chance — it is
    /// no roll. Critters included; the place is meant to be still.
    /// </summary>
    public class GraveyardHush : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (GraveyardZone.Contains(player))
                maxSpawns = 0;
        }
    }
}

