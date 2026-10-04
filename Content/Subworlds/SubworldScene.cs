using Microsoft.Xna.Framework;
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
    /// world a few hundred tiles each way that is all of it. So, in a subworld:
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
    ///   - and four things with no hook are stopped by detour (<see cref="SubworldSceneSystem"/>).
    /// </summary>
    public class SubworldScene : ModSceneEffect
    {
        /// <summary>The game's own name for the Underworld's haze (Player.UpdateBiomes).</summary>
        private const string Haze = "HeatDistortion";

        public override SceneEffectPriority Priority => SceneEffectPriority.Environment;

        /// <summary>The place's music, or its silence (Subworld.NoMusic). -1 is "no say", for a world.</summary>
        public override int Music => Subworld.Current?.Music ?? -1;

        public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle => ModContent.GetInstance<SubworldSky>();

        /// <summary>
        /// Behind the full screen map: the game's own picture of open sky, in the sky's colour.
        /// The game would put the Underworld's there (Main.DrawMapFullscreenBackground: it
        /// counts rows up from the floor for that as well).
        /// </summary>
        public override string MapBackground => "Terraria/Images/MapBG1";

        public override bool IsSceneEffectActive(Player player) => Subworld.Active;

        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (!isActive)
                return;

            player.ManageSpecialBiomeVisuals(Haze, false, player.Center);
            player.ZoneUnderworldHeight = false;
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
    ///     Underworld band starts 330 rows up from the floor, which in a world this small
    ///     is above the top of it;
    ///   - the Underworld's red light on every open tile under the surface line
    ///     (TileLightScanner.ApplyHellLight);
    ///   - and three achievements given for where a player is: at the bottom of the world,
    ///     at the top of it, in the Underworld (AchievementsHelper.HandleSpecialEvent). A
    ///     place a few hundred rows high is under the game's line for the Underworld nearly
    ///     everywhere, and its top and bottom rows are the game's top and bottom of the
    ///     world.
    /// </summary>
    public class SubworldSceneSystem : ModSystem
    {
        /// <summary>The game's numbers for those three (Player.BordersMovement, Player.UpdateBiomes).</summary>
        private const int RockBottom = 10;
        private const int IntoOrbit = 11;
        private const int GettingHot = 14;

        public override void Load()
        {
            On_AchievementsHelper.HandleSpecialEvent += NotForBeingHere;

            if (Main.dedServ)
                return;

            On_Main.DrawUnderworldBackground += NoUnderworldBehind;
            On_Main.DrawUnderworldBackgroudLayer += NoUnderworldLayer;
            On_Main.DrawBackground += NoCaveBehind;
            On_TileLightScanner.ApplyHellLight += NoUnderworldLight;
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
    }
}
