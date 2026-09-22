using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// A held moment: the screen narrows, the camera goes somewhere else and the player
    /// waits.
    ///
    /// Anything that wants one asks for it EVERY TICK it wants it — <see cref="Frame"/>
    /// for the letterbox, <see cref="Look"/> for the camera, <see cref="Hold"/> for the
    /// player — and stops asking when it is done. Nothing is switched on or off, so a boss that dies, despawns or loses its
    /// packet mid-scene cannot leave the camera stuck on a patch of dirt or the player
    /// stuck in place: the moment it stops asking, everything eases back.
    ///
    /// Client-side. The thing asking runs its AI on every client (bosses do), so every
    /// client asks for itself and nothing needs syncing.
    /// </summary>
    public static class Cinematic
    {
        /// <summary>Ticks the camera takes to reach what it is looking at, and to come back. A second and a half.</summary>
        public const int Pan = 90;

        /// <summary>Ticks the bars take to come in, and to go. Half a second: they should arrive before the shot does.</summary>
        public const int Bars = 30;

        /// <summary>What the screen narrows to. 21:9, from whatever it is: on a 21:9 monitor the bars are nothing, which is right.</summary>
        public const float Wide = 19f / 9f;

        private static Vector2 focus;

        // Requests are made during one update (by NPC AI, which runs after the players)
        // and read during the next (by the player, who runs before the NPCs). So each
        // is latched once a tick: what was asked for last tick is what holds this tick.
        // One tick of lag, invisible.
        private static bool lookAsked;
        private static bool holdAsked;
        private static bool frameAsked;
        private static bool holding;

        /// <summary>0 on the player, 1 on the focus. Eases between them.</summary>
        private static float weight;

        /// <summary>0 open, 1 fully letterboxed. Eases between them.</summary>
        private static float frame;

        /// <summary>Point the camera at this, this tick.</summary>
        public static void Look(Vector2 at)
        {
            focus = at;
            lookAsked = true;
        }

        /// <summary>Keep the local player still and unarmed, this tick.</summary>
        public static void Hold()
        {
            holdAsked = true;
        }

        /// <summary>Narrow the screen to 21:9, this tick. Black bars top and bottom, over the world, under the interface, and shown whether or not the interface is.</summary>
        public static void Frame()
        {
            frameAsked = true;
        }

        /// <summary>Whether the bars are on screen at all, including on their way out.</summary>
        public static bool Framed => frame > 0f;

        /// <summary>
        /// Whether the player is being held right now. Also true while the camera is
        /// still on its way back: moving before you can see yourself is worse than
        /// waiting a second and a half.
        /// </summary>
        public static bool Holding => holding || weight > 0f;

        /// <summary>Whether the camera is anywhere but on the player, including on its way back.</summary>
        public static bool Watching => weight > 0f;

        internal static void Tick()
        {
            float step = 1f / Pan;

            weight = lookAsked
                ? Math.Min(1f, weight + step)
                : Math.Max(0f, weight - step);

            frame = frameAsked
                ? Math.Min(1f, frame + 1f / Bars)
                : Math.Max(0f, frame - 1f / Bars);

            holding = holdAsked;

            lookAsked = false;
            holdAsked = false;
            frameAsked = false;
        }

        /// <summary>Height of one bar right now, in screen pixels. Zero when the screen is already this wide.</summary>
        internal static int BarHeight()
        {
            if (frame <= 0f)
                return 0;

            float full = Math.Max(0f, (Main.screenHeight - Main.screenWidth / Wide) * 0.5f);

            return (int)Math.Round(full * MathHelper.SmoothStep(0f, 1f, frame));
        }

        /// <summary>
        /// The bars, in raw screen pixels. Called from inside an open sprite batch with
        /// no transform on it; both draw paths below arrange that.
        /// </summary>
        internal static void DrawBars(SpriteBatch spriteBatch)
        {
            int height = BarHeight();

            if (height <= 0)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, height), Color.Black);
            spriteBatch.Draw(pixel, new Rectangle(0, Main.screenHeight - height, Main.screenWidth, height), Color.Black);
        }

        /// <summary>Where the screen's top-left should be, given where vanilla put it.</summary>
        internal static Vector2 Place(Vector2 vanilla)
        {
            if (weight <= 0f)
                return vanilla;

            Vector2 target = focus - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;

            return Vector2.Lerp(vanilla, target, MathHelper.SmoothStep(0f, 1f, weight));
        }
    }

    /// <summary>Runs the easing once a tick, moves the screen after vanilla has placed it, and draws the bars.</summary>
    public class CinematicSystem : ModSystem
    {
        // The bars have to sit over the whole world and under the interface, and they
        // have to show when the interface is hidden. There is no one place in the draw
        // that gives all three: everything after the screen filters is inside vanilla's
        // "if (!hideUI)", and everything before them gets colour graded, which on the
        // Mile would lift the black. So, two places, one of which draws on any frame:
        //
        //   interface shown  -> an interface layer at index 0, first thing drawn, so
        //                       every real element lands on top of it
        //   interface hidden -> Main.OnPostDraw, after everything, which with no
        //                       interface is the same place
        //
        // Both are after the filters, both are raw screen pixels.

        public override void Load()
        {
            if (!Main.dedServ)
                Main.OnPostDraw += DrawBarsOverEverything;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                Main.OnPostDraw -= DrawBarsOverEverything;
        }

        /// <summary>First thing in the update, before the players: latch last tick's requests.</summary>
        public override void PreUpdateEntities()
        {
            Cinematic.Tick();
        }

        public override void ModifyScreenPosition()
        {
            Main.screenPosition = Cinematic.Place(Main.screenPosition);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (!Cinematic.Framed)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer("PisAndDag: Letterbox", () =>
            {
                Cinematic.DrawBars(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.None));
        }

        private static void DrawBarsOverEverything(GameTime gameTime)
        {
            // Only when the interface path did not run this frame.
            if (!Main.hideUI || Main.gameMenu || !Cinematic.Framed)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            Cinematic.DrawBars(Main.spriteBatch);

            Main.spriteBatch.End();
        }
    }

    /// <summary>The player's half of a held moment: no movement, no items, and nothing gets to kill them meanwhile.</summary>
    public class CinematicPlayer : ModPlayer
    {
        public override void ResetEffects()
        {
            if (!Cinematic.Holding)
                return;

            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void SetControls()
        {
            if (!Cinematic.Holding)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override bool CanUseItem(Item item) => !Cinematic.Holding;

        public override void PostUpdateEquips()
        {
            if (!Cinematic.Holding)
                return;

            // A player who cannot move or fight should not be killable by a passing
            // zombie while they watch. Two ticks, renewed while held, gone the tick after.
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
        }
    }
}
