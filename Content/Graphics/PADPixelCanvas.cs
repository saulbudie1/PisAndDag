using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Graphics
{
    /// <summary>
    /// The half-size canvas (after Calamity's PixelationManager, looked at for how it goes about it; by request:
    /// "yeah 1 and 4"): two pictures half the screen's size, painted before each frame is drawn and laid over the world
    /// at twice their size with no smoothing, so that whatever is painted on them (a soft glow, a ring, a bent band)
    /// comes out in squares two pixels a side, on the world's own two-pixel grid, as the mod's pictures are.
    ///
    /// What goes on it:
    ///   particles    any with <see cref="PADParticle.Pixelate"/> set (the handler leaves those to this);
    ///   fire         all of <see cref="PADFire"/>;
    ///   anything     <see cref="Queue"/>, from a PreDraw, every frame it should show: the draw is done on the canvas
    ///                the next time it is painted, a frame later, at world places less Main.screenPosition as any draw
    ///                is.
    ///
    /// One picture for what is laid over the world (alpha blend), one for what adds its light (additive), since the two
    /// cannot share one. Each is the screen and a margin about it, its corner on an even world pixel, so the grid does
    /// not swim as the camera moves; it is laid down where that corner is by the time the frame is drawn, through the
    /// game's own view (zoom, and the flip under reversed gravity). Presented at the dust layer, under the mod's other
    /// particles (PADParticleSystem). Nothing is painted, or laid down, while nothing asks for it. Client only.
    /// </summary>
    public class PADPixelCanvas : ModSystem
    {
        /// <summary>World pixels of margin about the screen, each side (even).</summary>
        private const int Margin = 32;

        /// <summary>What is added to what is behind it, as it is: the additive picture holds its light already multiplied out.</summary>
        private static readonly BlendState AddAsIs = new BlendState
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
        };

        private static RenderTarget2D over, light;
        private static readonly List<Action<SpriteBatch>> queuedOver = new(), queuedLight = new();

        /// <summary>The world place of the pictures' corner when they were last painted, and whether each has anything on it.</summary>
        private static Vector2 corner;
        private static bool hasOver, hasLight;
        private static bool broken;

        public override void Load()
        {
            if (!Main.dedServ)
                Main.OnPreDraw += Paint;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                Main.OnPreDraw -= Paint;

            RenderTarget2D a = over, b = light;
            over = light = null;
            queuedOver.Clear();
            queuedLight.Clear();
            hasOver = hasLight = broken = false;

            if (a != null || b != null)
                Main.QueueMainThreadAction(() =>
                {
                    a?.Dispose();
                    b?.Dispose();
                });
        }

        /// <summary>
        /// Something to draw on the canvas the next time it is painted (once: ask again every frame it should show). The
        /// batch is open; draw at world places less Main.screenPosition.
        /// </summary>
        public static void Queue(Action<SpriteBatch> draw, bool additive = false)
        {
            if (Main.dedServ || draw == null || broken)
                return;

            List<Action<SpriteBatch>> list = additive ? queuedLight : queuedOver;

            // Nothing piles up if the canvas is not being painted (a menu).
            if (list.Count < 512)
                list.Add(draw);
        }

        private static void Paint(GameTime time)
        {
            hasOver = hasLight = false;

            if (broken || Main.gameMenu)
            {
                queuedOver.Clear();
                queuedLight.Clear();
                return;
            }

            bool particles = PADParticleHandler.AnyPixelated();
            bool fire = PADFire.Any;

            if (!particles && !fire && queuedOver.Count == 0 && queuedLight.Count == 0)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;
            SpriteBatch batch = Main.spriteBatch;
            RenderTargetBinding[] previous = device.GetRenderTargets();
            bool open = false;

            try
            {
                int wide = (Main.screenWidth + Margin * 2) / 2 + 1, high = (Main.screenHeight + Margin * 2) / 2 + 1;
                Ensure(device, ref over, wide, high);
                Ensure(device, ref light, wide, high);

                // The corner on an even world pixel; what is drawn at a screen place lands at half of (that place and
                // how far the screen is from the corner).
                corner = new Vector2((float)Math.Floor(Main.screenPosition.X / 2f) * 2f - Margin, (float)Math.Floor(Main.screenPosition.Y / 2f) * 2f - Margin);
                Vector2 shift = Main.screenPosition - corner;
                Matrix toCanvas = Matrix.CreateTranslation(shift.X, shift.Y, 0f) * Matrix.CreateScale(0.5f, 0.5f, 1f);

                for (int pass = 0; pass < 2; pass++)
                {
                    bool additive = pass == 1;
                    List<Action<SpriteBatch>> queued = additive ? queuedLight : queuedOver;

                    device.SetRenderTarget(additive ? light : over);
                    device.Clear(Color.Transparent);

                    batch.Begin(SpriteSortMode.Deferred, additive ? BlendState.Additive : BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, toCanvas);
                    open = true;

                    if (particles)
                        PADParticleHandler.DrawPixelated(batch, additive);

                    // Fire (PADFire): its smoke and gas over the world, its flames and sparks into the light.
                    if (fire)
                        PADFire.Draw(batch, additive);

                    foreach (Action<SpriteBatch> draw in queued)
                        draw(batch);

                    batch.End();
                    open = false;
                }

                hasOver = hasLight = true;
            }
            catch (Exception e)
            {
                broken = true;
                ModContent.GetInstance<PADPixelCanvas>().Mod.Logger.Error("The half-size canvas failed; what was to be drawn on it is not drawn until the mod reloads.", e);

                if (open)
                {
                    try { batch.End(); }
                    catch { }
                }
            }
            finally
            {
                queuedOver.Clear();
                queuedLight.Clear();
                device.SetRenderTargets(previous);

                // As KnightCanvas: binding the screen again can wipe it; nothing is on it yet this frame.
                if (previous.Length == 0 && device.PresentationParameters.RenderTargetUsage == RenderTargetUsage.DiscardContents)
                    device.Clear(Color.Black);
            }
        }

        private static void Ensure(GraphicsDevice device, ref RenderTarget2D target, int wide, int high)
        {
            if (target != null && !target.IsDisposed && target.Width == wide && target.Height == high)
                return;

            target?.Dispose();
            target = new RenderTarget2D(device, wide, high, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        /// <summary>Lays the canvas over the world, twice its size, unsmoothed. No batch may be open.</summary>
        internal static void Present(SpriteBatch batch)
        {
            if (Main.dedServ || broken || (!hasOver && !hasLight) || over == null || light == null)
                return;

            Vector2 at = corner - Main.screenPosition;

            if (hasOver)
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                batch.Draw(over, at, null, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
                batch.End();
            }

            if (hasLight)
            {
                batch.Begin(SpriteSortMode.Deferred, AddAsIs, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                batch.Draw(light, at, null, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
                batch.End();
            }
        }
    }
}
