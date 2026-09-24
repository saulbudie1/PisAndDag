#if !PREVIEW
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// Puts the Kingdom of Grace into world generation, in three places:
    ///
    /// - vanilla's Floating Islands pass is held off the sky over the plain, so no island
    ///   hangs in the Curtain's tower or gets cut in half by the clearing over the city;
    /// - vanilla's Shimmer pass is replaced: the world's one pool goes where the Font's
    ///   shaft will come down, at a depth in the cavern layer, and the ground the capital's
    ///   underside will take is marked so no later pass builds a cabin in it;
    /// - the kingdom itself is built after Final Cleanup, last of all, so nothing vanilla
    ///   does afterwards can touch it.
    /// </summary>
    public class GraceWorldGen : ModSystem
    {
        /// <summary>True only while vanilla's Floating Islands pass runs.</summary>
        private static bool duringIslands;

        /// <summary>The world row the Shimmer's surface was given this generation.</summary>
        public static int ShimmerWorldY;

        /// <summary>How far past the plain's inland edge the islands are kept.</summary>
        private const int IslandMargin = 180;

        /// <summary>
        /// The fewest rows from the plain down to the Shimmer: the Undercity, the Long
        /// Reliquary and the Rootworks, one under the other, with ground between.
        /// </summary>
        private const int LeastDepth = 1000;

        /// <summary>How far inland of the plain the plain pass looks for the ground it has to stand over.</summary>
        private const int PlainInland = 340;

        public override void Load() => On_UnifiedRandom.Next_int_int += IslandsAwayFromGrace;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // Nothing carried over from the last world made this session.
            duringIslands = false;
            ShimmerWorldY = 0;

            int islands = tasks.FindIndex(p => p.Name.Equals("Floating Islands", StringComparison.Ordinal));
            if (islands >= 0)
                tasks[islands] = new BracketPass(tasks[islands], () => duringIslands = true, () => duringIslands = false);
            else
                Mod.Logger.Warn("World generation pass 'Floating Islands' is not in the list; islands may land over Grace.");

            int shimmer = tasks.FindIndex(p => p.Name.Equals("Shimmer", StringComparison.Ordinal));
            if (shimmer >= 0)
                tasks[shimmer] = new GraceShimmerPass(tasks[shimmer]);
            else
                Mod.Logger.Warn("World generation pass 'Shimmer' is not in the list; the Shimmer will not be under the Font.");

            int final = tasks.FindIndex(p => p.Name.Equals("Final Cleanup", StringComparison.Ordinal));
            if (final >= 0)
                tasks.Insert(final + 1, new GracePass("PAD Kingdom of Grace", 400.0));
            else
                Mod.Logger.Warn("World generation pass 'Final Cleanup' is not in the list, so the Kingdom of Grace was not added.");
        }

        /// <summary>
        /// Vanilla rolls every island's x with one call, <c>Next(maxTilesX * 0.1, maxTilesX * 0.9)</c>,
        /// twice in the pass, the same arguments both times. While that pass runs, and only for
        /// that argument pair, the roll is made over the part of the range that is not the
        /// plain or the ground just inland of it.
        /// </summary>
        private static int IslandsAwayFromGrace(On_UnifiedRandom.orig_Next_int_int orig, UnifiedRandom self, int minValue, int maxValue)
        {
            if (!duringIslands || !ExtraLargeWorld.Active
                || minValue != (int)(Main.maxTilesX * 0.1) || maxValue != (int)(Main.maxTilesX * 0.9))
                return orig(self, minValue, maxValue);

            (int left, int right) = Layout.Plain;

            if (!WorldMirror.Flipped)
            {
                int hi = Math.Min(maxValue, left - IslandMargin);
                return hi > minValue + 10 ? orig(self, minValue, hi) : orig(self, minValue, maxValue);
            }

            int lo = Math.Max(minValue, right + IslandMargin);
            return maxValue > lo + 10 ? orig(self, lo, maxValue) : orig(self, minValue, maxValue);
        }

        /// <summary>
        /// The world row the Shimmer lies at: well down in the cavern layer, at least
        /// <see cref="LeastDepth"/> under the lowest the plain can come out, and clear of the
        /// Underworld.
        /// </summary>
        public static int ShimmerRow()
        {
            int y = Math.Max((int)Main.rockLayer + 420, DeepestPlain() + LeastDepth);
            return Math.Clamp(y, (int)Main.rockLayer + 250, Main.maxTilesY - 280);
        }

        /// <summary>
        /// The lowest the plain's level can come out. The Shimmer pass runs before the
        /// plain is raised, so this reads the ground the way the plain pass will read it:
        /// the highest ground over the plain and the stretch inland of it. The pass puts
        /// the level more than twenty-four rows over that, and never higher than ten under
        /// the islands' floor or four tenths of the surface; this takes the lowest of that.
        /// </summary>
        private static int DeepestPlain()
        {
            (int left, int right) = Layout.Plain;
            bool rampOnLeft = !WorldMirror.Flipped;
            int from = rampOnLeft ? Math.Max(1, left - PlainInland) : left;
            int to = rampOnLeft ? right : Math.Min(Main.maxTilesX - 2, right + PlainInland);

            int highest = int.MaxValue;
            for (int x = from; x <= to; x += 3)
            {
                int ground = Layout.Ground(x);
                if (ground > 0 && ground < highest)
                    highest = ground;
            }

            if (highest == int.MaxValue)
                highest = (int)Main.worldSurface;

            int level = highest - 24;
            level = Math.Max(level, Layout.SkyFloor + 10);
            level = Math.Max(level, (int)(Main.worldSurface * 0.40));
            return level;
        }
    }

    /// <summary>
    /// Replaces vanilla's Shimmer pass in a Grand world. The pool is not made here; the
    /// Rootworks makes it, at the end, round the position this records. What this does is
    /// say where it will be, so the passes that keep away from the Shimmer keep away from
    /// the right place, and fence off the whole column under the plain for the capital.
    /// In any other world it runs vanilla's pass untouched.
    /// </summary>
    public class GraceShimmerPass : GenPass
    {
        private readonly GenPass vanilla;

        public GraceShimmerPass(GenPass vanilla) : base(vanilla.Name, vanilla.Weight) => this.vanilla = vanilla;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            if (!ExtraLargeWorld.Active)
            {
                vanilla.Apply(progress, config);
                return;
            }

            progress.Message = "Finding the Shimmer";

            (int left, int right) = Layout.Plain;
            int width = right - left + 1;
            int column = GraceBuilder.ShaftColumn(width, GraceSite.DefaultEdgeMargin);
            int x = WorldMirror.Flipped ? right - column : left + column;
            int y = GraceWorldGen.ShimmerRow();

            GraceWorldGen.ShimmerWorldY = y;
            GenVars.shimmerPosition = new Vector2D(x, y);

            // Everything under the plain, from over the surface to under the pool, and the
            // trenches' reach into the Mile: nothing else goes there.
            int top = Math.Max(20, (int)Main.worldSurface - 160);
            int x0 = Math.Max(20, left - 140), x1 = Math.Min(Main.maxTilesX - 20, right + 140);
            int bottom = Math.Min(Main.maxTilesY - 220, y + 80);
            GenVars.structures.AddProtectedStructure(new Rectangle(x0, top, x1 - x0, bottom - top), 0);

            GenLog.Log?.Info($"Grace: the Shimmer will lie at ({x}, {y}), under the Font's shaft.");
        }
    }

    /// <summary>The Kingdom of Grace, built.</summary>
    public class GracePass : GenPass
    {
        /// <summary>How far round the kingdom the caves are drained, under the paving.</summary>
        private const int DrainMargin = 60;

        public GracePass(string name, double weight) : base(name, weight) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Building the Kingdom of Grace";

            if (!ExtraLargeWorld.Active || GracePlainPass.PlainTop <= 0)
                return;

            (int left, int right) = Layout.Plain;
            int width = right - left + 1;
            bool flipped = WorldMirror.Flipped;
            int originX = flipped ? right : left;
            int d = flipped ? -1 : 1;
            int plainTop = GracePlainPass.PlainTop;
            int shimmerY = GraceWorldGen.ShimmerWorldY > 0 ? GraceWorldGen.ShimmerWorldY : GraceWorldGen.ShimmerRow();

            var site = new GraceSite
            {
                PlainWidth = width,
                EdgeMargin = GraceSite.DefaultEdgeMargin,
                Top = Math.Min(260, plainTop - 42),
                RockLayer = (int)Main.rockLayer - plainTop,
                ShimmerY = shimmerY - plainTop,
            };
            site.Depth = site.ShimmerY + 40;
            site.ForestStart = RampEnd(originX, d, plainTop, width);

            // Clear the footprint of furniture, trees and records before a stone is laid.
            int wl = flipped ? originX - width - 20 : originX - 10;
            GraceWorldCanvas.Prepare(new Rectangle(wl, plainTop - site.Top, width + 30, site.Top + site.Depth + 1));

            var canvas = new GraceWorldCanvas(originX, plainTop, d);

            try
            {
                GraceBuilder.Build(canvas, site);
            }
            catch (Exception e)
            {
                GenLog.Log?.Error("Grace: building the kingdom failed part way; what was built is kept.", e);
            }

            progress.Message = "Setting the stones of Grace";
            canvas.DrainUnder(plainTop + 7, DrainMargin);
            canvas.Finish();
            GraceWorld.Record(canvas);

            string failed = string.Join(", ", canvas.Failed);
            GenLog.Log?.Info($"Grace: built over x {canvas.MinX}-{canvas.MaxX}, y {canvas.MinY}-{canvas.MaxY}; " +
                $"{canvas.Placed} objects placed; would not go: {failed}.");
        }

        /// <summary>
        /// Where the way up levels out: the first plan column whose ground is within a few
        /// rows of the plain's level, a few columns on.
        /// </summary>
        private static int RampEnd(int originX, int d, int plainTop, int width)
        {
            for (int x = 0; x < Math.Min(width / 2, 800); x++)
            {
                int wx = originX + d * x;
                if (!WorldGen.InWorld(wx, plainTop, 30))
                    continue;

                for (int y = plainTop - 100; y < plainTop + 300; y++)
                {
                    Tile t = Main.tile[wx, y];
                    if (!t.HasTile || !Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType])
                        continue;

                    if (y <= plainTop + 5)
                        return x + 6;
                    break;
                }
            }

            return 230;
        }
    }
}
#endif
