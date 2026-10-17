using System;
using log4net;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// How a subworld looks and sounds, whichever it is: open sky and nothing behind it,
    /// its own music or none, lit as the surface is.
    ///
    /// The game takes everything within two hundred rows of a world's floor for the
    /// Underworld, and a world's two ends for the sea, by counting rows and columns. In a
    /// world a few hundred tiles each way that is most of it, or all. So, in a subworld:
    ///
    ///   - this scene is the one in force over the game's own: its music over the
    ///     Underworld's (ranked above where the game picks that, and under any boss or
    ///     event; a place with none says so with the game's own number for silence, which
    ///     is in force just the same), its backdrop, which is none, over the sea's
    ///     (<see cref="SubworldSky"/>), and its picture behind the full screen map over the
    ///     Underworld's;
    ///   - the heat haze the game puts over the Underworld is taken off again each tick, and
    ///     the player is not counted as down there or on a beach, where tModLoader lets a
    ///     scene do that (SpecialVisuals, the last thing in Player.UpdateBiomes);
    ///   - and six things with no hook are stopped by detour or edit (<see cref="SubworldSceneSystem"/>).
    ///
    /// A place that is the sea (Subworld.IsSea: the Vast Ocean) is left to the game on all
    /// three counts it has for the sea: the player on a beach where the game says so, the
    /// sea's backdrop where the game chooses it (this scene has no say on the backdrop
    /// there), and the sea's own picture behind the full screen map.
    /// </summary>
    public class SubworldScene : ModSceneEffect
    {
        /// <summary>The game's own name for the Underworld's haze (Player.UpdateBiomes).</summary>
        private const string Haze = "HeatDistortion";

        /// <summary>The game's own pictures behind the full screen map: open sky, and the sea (Main.DrawMapFullscreenBackground: MapBGs 0 and 10).</summary>
        private const string SkyMap = "Terraria/Images/MapBG1";
        private const string SeaMap = "Terraria/Images/MapBG11";

        private static bool Sea => Subworld.Current?.IsSea == true;

        public override SceneEffectPriority Priority => SceneEffectPriority.Environment;

        /// <summary>The place's music, or its silence (Subworld.NoMusic). -1 is "no say", for a world.</summary>
        public override int Music => Subworld.Current?.Music ?? -1;

        /// <summary>None at all, or, in a place that is the sea, no say: the game draws the sea's.</summary>
        public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle => Sea ? null : ModContent.GetInstance<SubworldSky>();

        /// <summary>
        /// Behind the full screen map: the game's own picture of open sky, in the sky's colour,
        /// or of the sea in a place that is the sea. The game would put the Underworld's there
        /// (Main.DrawMapFullscreenBackground: it counts rows up from the floor for that as well).
        /// </summary>
        public override string MapBackground => Sea ? SeaMap : SkyMap;

        public override bool IsSceneEffectActive(Player player) => Subworld.Active;

        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (!isActive)
                return;

            player.ManageSpecialBiomeVisuals(Haze, false, player.Center);
            player.ZoneUnderworldHeight = false;

            if (!Sea)
                player.ZoneBeach = false;
        }
    }

    /// <summary>
    /// A backdrop of nothing: no far hills, no middle, no near. The sky is drawn under any
    /// backdrop, so the sky is what is left. Only the bookkeeping the game asks of every
    /// backdrop is here: this one fades in, the rest out.
    /// </summary>
    public class SubworldSky : ModSurfaceBackgroundStyle
    {
        public override void ModifyFarFades(float[] fades, float transitionSpeed)
        {
            for (int i = 0; i < fades.Length; i++)
            {
                if (i == Slot)
                {
                    fades[i] += transitionSpeed;

                    if (fades[i] > 1f)
                        fades[i] = 1f;
                }
                else
                {
                    fades[i] -= transitionSpeed;

                    if (fades[i] < 0f)
                        fades[i] = 0f;
                }
            }
        }
    }

    /// <summary>
    /// What the game does near a world's floor that nothing lets a mod turn off, turned off
    /// in a subworld. Each of these is a method of the game's far too long for it to have
    /// folded into the places it is called from, so a detour is sure to be reached.
    ///
    ///   - the Underworld's backdrop, drawn whenever the screen's foot is within 220 rows
    ///     of the world's floor (Main.DrawUnderworldBackground, and the layers of it);
    ///   - the cave and Underworld walls drawn behind the tiles (Main.DrawBackground): its
    ///     Underworld band starts 330 rows up from the floor, which is above the top of
    ///     any place lower than that;
    ///   - the Underworld's red light on every open tile under the surface line
    ///     (TileLightScanner.ApplyHellLight);
    ///   - the Underworld's drying of water: each time the game moves a cell of water under
    ///     its line for the Underworld (Main.UnderworldLayer, maxTilesY less 200) it takes 2
    ///     of the cell's 255 away (Liquid.Update), so a sea that goes down past that line
    ///     runs out through its floor, the water over it sinking to fill what dried, and a
    ///     dark hollow is left at the bottom and the surface falls. This one is not a
    ///     detour but an edit, the line told past the bottom of the world;
    ///   - and three achievements given for where a player is: at the bottom of the world,
    ///     at the top of it, in the Underworld (AchievementsHelper.HandleSpecialEvent). A
    ///     place a few hundred rows high is under the game's line for the Underworld over
    ///     most of its height or all of it, and its top and bottom rows are the game's top
    ///     and bottom of the world.
    ///
    /// And one thing the game does with its water that is no good in a wide sea: its quick
    /// settling (Liquid.QuickWater), not run in a place whose water is laid still
    /// (<see cref="Subworld.StillWater"/>). For each cell of water it looks along the row,
    /// through the water, to the next solid tile; across the Vast Ocean's open water that is
    /// most of 13662 columns for each of some 24 million cells, and the game does it every
    /// time the place is loaded ("Settling liquids", many minutes), and again whenever too
    /// much water moves at once.
    ///
    /// And one thing that reckons from the rock line: the game's planting of herbs
    /// (WorldGen.PlantAlch, from its update of the world), not done in a place whose rock
    /// line leaves no room under it (<see cref="NoHerbsWithoutRoom"/>).
    /// </summary>
    public class SubworldSceneSystem : ModSystem
    {
        /// <summary>The game's numbers for those three (Player.BordersMovement, Player.UpdateBiomes).</summary>
        private const int RockBottom = 10;
        private const int IntoOrbit = 11;
        private const int GettingHot = 14;

        private static ILog log;

        public override void Load()
        {
            log = Mod.Logger;

            On_AchievementsHelper.HandleSpecialEvent += NotForBeingHere;

            // Water moves on a server as well, and the world is updated there.
            IL_Liquid.Update += NoUnderworldDrying;
            On_Liquid.QuickWater += NoQuickWater;
            On_WorldGen.PlantAlch += NoHerbsWithoutRoom;

            if (Main.dedServ)
                return;

            On_Main.DrawUnderworldBackground += NoUnderworldBehind;
            On_Main.DrawUnderworldBackgroudLayer += NoUnderworldLayer;
            On_Main.DrawBackground += NoCaveBehind;
            On_TileLightScanner.ApplyHellLight += NoUnderworldLight;
        }

        /// <summary>The game's quick settling of water, in a place whose water is laid still: none.</summary>
        private static void NoQuickWater(On_Liquid.orig_QuickWater orig, int verbose, int minY, int maxY)
        {
            if (Subworld.Current?.StillWater == true)
                return;

            orig(verbose, minY, maxY);
        }

        /// <summary>
        /// The game's planting of herbs, now and then from its update of the world: one time in
        /// forty it looks for a place for one from halfway between the rock line and the bottom
        /// of the world down to twenty rows over the bottom (1.4.4: "genRand.Next((int)(Main.rockLayer
        /// + Main.maxTilesY) / 2, Main.maxTilesY - 20)"). Where the rock line is within forty rows
        /// of the bottom, or past it, there is no such span, and the game throws ("minValue must
        /// be less than maxValue"): in the Vast Ocean, whose rock line is past the bottom
        /// (VastOcean.RockLineRow), and in a place with the lines every place has unless it says
        /// otherwise (Subworld.RockLine, 28 rows over the bottom). Not done in such a place: none
        /// of them has ground for herbs.
        /// </summary>
        private static void NoHerbsWithoutRoom(On_WorldGen.orig_PlantAlch orig)
        {
            if (Subworld.Active && (int)(Main.rockLayer + Main.maxTilesY) / 2 > Main.maxTilesY - 20)
                return;

            orig();
        }

        private static void NoUnderworldBehind(On_Main.orig_DrawUnderworldBackground orig, Main self, bool flat)
        {
            if (!Subworld.Active)
                orig(self, flat);
        }

        private static void NoUnderworldLayer(On_Main.orig_DrawUnderworldBackgroudLayer orig, bool flat, Vector2 screenOffset, float pushUp, int layerTextureIndex)
        {
            if (!Subworld.Active)
                orig(flat, screenOffset, pushUp, layerTextureIndex);
        }

        private static void NoCaveBehind(On_Main.orig_DrawBackground orig, Main self)
        {
            if (!Subworld.Active)
                orig(self);
        }

        private static void NoUnderworldLight(On_TileLightScanner.orig_ApplyHellLight orig, TileLightScanner self, Tile tile, int x, int y, ref Vector3 lightColor)
        {
            if (!Subworld.Active)
                orig(self, tile, x, y, ref lightColor);
        }

        private static void NotForBeingHere(On_AchievementsHelper.orig_HandleSpecialEvent orig, Player player, int eventID)
        {
            if (Subworld.Active && (eventID == RockBottom || eventID == IntoOrbit || eventID == GettingHot))
                return;

            orig(player, eventID);
        }

        public override void Unload() => log = null;

        /// <summary>
        /// Liquid.Update asks for the Underworld's line once, to dry the water under it
        /// (1.4.4: "y &gt; Main.UnderworldLayer", water only, 2 a time): the answer is passed
        /// through <see cref="UnderworldLine"/> on its way.
        /// </summary>
        private static void NoUnderworldDrying(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            int found = 0;

            while (cursor.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(Main), "get_" + nameof(Main.UnderworldLayer))))
            {
                cursor.EmitDelegate<Func<int, int>>(UnderworldLine);
                found++;
            }

            if (found != 1)
                log?.Warn($"Subworlds: the Underworld's line was asked for {found} times in Liquid.Update, not once as in 1.4.4. Water near a place's floor may dry up there.");
        }

        /// <summary>The Underworld's line, as the game's water is told it: in a subworld, under every row there is.</summary>
        private static int UnderworldLine(int line) => Subworld.Active ? int.MaxValue : line;
    }
}
