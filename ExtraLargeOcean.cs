using System;
using log4net;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace PisAndDag
{
    /// <summary>
    /// The ocean at the edges of an extra large world, twice over.
    ///
    /// Vanilla's ocean is tuned to one set of numbers and they all agree with each
    /// other. The water runs about 240 tiles in from the edge, the sand stops at about
    /// 350, and WorldGen.beachDistance is 380, which is the line past which nothing in
    /// the game calls it the ocean any more: not the biome, not the sharks, not the
    /// sand it draws. Widening the water without moving that line would leave a seam
    /// two hundred tiles out to sea where the ocean quietly stops being one.
    ///
    /// beachDistance and oceanDistance are readonly in this build and the runtime is
    /// allowed to bake a readonly static into every method that reads it, so writing
    /// to them would have some of the game agreeing and the rest not. The reads are
    /// replaced instead, in the methods where the answer changes what the player sees.
    /// Player.ZoneBeach is WorldGen.oceanDepths, so the one edit there carries the
    /// biome itself: the music, the fishing, the pylon, the shops, the background.
    ///
    /// Width comes from a handful of plain numbers set at the top of world generation
    /// and read by the first pass, so they are doubled in between, in PreWorldGen.
    ///
    /// Depth is one function. TuneOceanDepth is handed how many tiles out from the
    /// shore it has come and answers with how much further down to cut; the answer is
    /// steep for the first fifteen tiles and shallow after. Halving the number it is
    /// given stretches that same shelf over twice the distance, which arrives at twice
    /// the depth without changing its shape. The floor is the only thing held back:
    /// the engine stops calling water an ocean below WorldGen.oceanLevel, so the shelf
    /// flattens rather than cut past it.
    ///
    /// None of this touches a small, medium or large world.
    ///
    /// A place that is all sea (Subworld.IsSea: the Vast Ocean, eighteen times wider than the
    /// game's sea can reach) answers to both with its own width: every column of it is in
    /// reach of an end, so to everything here that asks it is the sea from end to end. What
    /// the game measures from the ends with numbers of its own is SubworldSea's.
    /// </summary>
    public class ExtraLargeOcean : ModSystem
    {
        /// <summary>Longer, and deeper, by this.</summary>
        public const int Twice = 2;

        /// <summary>
        /// The ocean's own margin at the bottom. The carve is allowed to come this
        /// close to oceanLevel and no closer, which leaves room for the fifteen to
        /// twenty tiles of slop the beach pass adds on top of whatever depth it asked
        /// for.
        /// </summary>
        private const double FloorMargin = 20.0;

        /// <summary>
        /// What WorldGen.beachDistance answers to in this world. Ordinary sizes get
        /// vanilla's own number back, so nothing changes for them.
        /// </summary>
        public static int BeachReach;

        /// <summary>The same for WorldGen.oceanDistance.</summary>
        public static int OceanReach;

        private static ILog log;

        public override void Load()
        {
            log = Mod.Logger;
            SetReach();

            On_WorldGen.TuneOceanDepth += Deepen;
            On_WorldGen.oceanDepths += OnlyOneOcean;

            // The biome, and everything that asks the biome. This one matters most.
            IL_WorldGen.oceanDepths += Widen;

            // Where the ocean is, to everything that has an opinion about it.
            IL_WorldGen.oceanCave += Widen;
            IL_WorldGen.TileRunner += Widen;
            IL_WorldGen.UpdateWorld_OvergroundTile += Widen;
            IL_WorldGen.SpreadGrass += Widen;
            IL_WorldGen.KillTile_MakeTileDust += Widen;
            IL_Player.MagicConch += Widen;

            // What grows on a beach.
            IL_WorldGen.PlantAlch += Widen;
            IL_WorldGen.PlaceCatTail += Widen;
            IL_WorldGen.SeaOatWaterCheck += Widen;
            IL_WorldGen.OasisPlantWaterCheck += Widen;
            IL_WorldGen.PlaceOasisPlant += Widen;
            IL_WorldGen.IsPalmOasisTree += Widen;

            // What lives in it.
            IL_NPC.SpawnNPC += Widen;

            // What it looks like, on the ground and on the map.
            IL_TileDrawing.MakeExtraPreparations += Widen;
            IL_TileDrawing.GetTileDrawData += Widen;
            IL_MapHelper.GetTileBaseOption += Widen;
        }

        public override void Unload() => log = null;

        public override void OnWorldLoad() => SetReach();

        /// <summary>
        /// Called after world generation has set its beach numbers and before the
        /// first pass reads them. Everything here is a distance from the edge of the
        /// world in tiles, and every one of them doubles: the sand, the water, the
        /// spread on both, the extra the dungeon and the jungle sides get, and the
        /// distances at which lakes, holes, caves and the evil biome are told to keep
        /// away from a beach.
        /// </summary>
        public override void PreWorldGen()
        {
            SetReach();

            if (!ExtraLargeWorld.Active)
                return;

            GenVars.beachBordersWidth *= Twice;
            GenVars.beachSandRandomCenter *= Twice;
            GenVars.beachSandRandomWidthRange *= Twice;
            GenVars.beachSandDungeonExtraWidth *= Twice;
            GenVars.beachSandJungleExtraWidth *= Twice;

            GenVars.oceanWaterStartRandomMin *= Twice;
            GenVars.oceanWaterStartRandomMax *= Twice;
            GenVars.oceanWaterForcedJungleLength *= Twice;

            GenVars.evilBiomeBeachAvoidance *= Twice;
            GenVars.lakesBeachAvoidance *= Twice;
            GenVars.smallHolesBeachAvoidance *= Twice;
            GenVars.surfaceCavesBeachAvoidance *= Twice;
            GenVars.surfaceCavesBeachAvoidance2 *= Twice;

            log?.Info($"Extra Large world size: ocean widened to {GenVars.oceanWaterStartRandomMin}-{GenVars.oceanWaterStartRandomMax} tiles of water, sand to about {GenVars.beachSandRandomCenter}.");
        }

        private static void SetReach()
        {
            int times = ExtraLargeWorld.Active ? Twice : 1;

            BeachReach = WorldGen.beachDistance * times;
            OceanReach = WorldGen.oceanDistance * times;
        }

        // ------------------------------------------------------------------- the edits

        private static int Beach(int vanilla) => Sea ? Main.maxTilesX : BeachReach;

        private static int Ocean(int vanilla) => Sea ? Main.maxTilesX : OceanReach;

        /// <summary>In a place that is all sea (the notes over this class).</summary>
        private static bool Sea => Content.Subworlds.Subworld.Current?.IsSea == true;

        /// <summary>
        /// Every read of beachDistance and oceanDistance in this method answers with
        /// the reach of this world's ocean instead. The instruction is left where it
        /// is and the answer converted on the way out, so anything branching to that
        /// line still lands on it.
        /// </summary>
        private static void Widen(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            int found = 0;

            while (cursor.TryGotoNext(MoveType.After,
                i => i.MatchLdsfld(typeof(WorldGen), nameof(WorldGen.beachDistance))))
            {
                cursor.EmitDelegate<Func<int, int>>(Beach);
                found++;
            }

            cursor.Index = 0;

            while (cursor.TryGotoNext(MoveType.After,
                i => i.MatchLdsfld(typeof(WorldGen), nameof(WorldGen.oceanDistance))))
            {
                cursor.EmitDelegate<Func<int, int>>(Ocean);
                found++;
            }

            if (found == 0)
                log?.Warn($"Extra Large world size: {il.Method?.Name} reads neither beachDistance nor oceanDistance any more. The ocean will stop short there.");
        }

        /// <summary>
        /// An Extra Large world has one ocean and the other end is the Grace Plain, so
        /// the half of oceanDepths that answers for the far edge has to stop answering.
        /// Without this the plain's outer seven hundred tiles read as beach: ocean music,
        /// ocean fishing, sharks over dry ground.
        /// </summary>
        private static bool OnlyOneOcean(On_WorldGen.orig_oceanDepths orig, int x, int y)
        {
            bool ocean = orig(x, y);

            if (!ocean || !ExtraLargeWorld.Active)
                return ocean;

            return !Content.Structures.WorldMirror.OnGraceSide(x);
        }

        /// <summary>
        /// Twice as deep, over twice the distance. The shelf keeps vanilla's shape --
        /// steep off the sand, levelling out, one last drop at the far end -- because
        /// it is vanilla's own answer, asked half as fast.
        /// </summary>
        private static double Deepen(On_WorldGen.orig_TuneOceanDepth orig, int step, double depth, bool gentle)
        {
            if (!ExtraLargeWorld.Active)
                return orig(step, depth, gentle);

            double deeper = orig(step / Twice, depth, gentle);
            double floor = WorldGen.oceanLevel - Main.worldSurface - FloorMargin;

            return floor > 0.0 && deeper > floor ? floor : deeper;
        }
    }
}
