using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The glint on the honeyed Queen Bee: streaks of pale gold sliding across her at a
    /// slant, two layers of them at different angles and speeds, and every two seconds one
    /// bright sweep, all of it in her shape and nowhere else. The way an enchanted thing
    /// shines, in light gold.
    ///
    /// There is no shader to mask with, so it is made in a target of its own, once a frame,
    /// before anything is drawn to the screen (Main.OnPreDraw, where the game prepares its
    /// own targets): the streaks are laid down over nothing, and her current frame is then
    /// drawn over them with a blend that keeps what is under her and only as much of it as
    /// she covers (the streaks times her alpha). What is left is the glint in her outline,
    /// frame sized; LegendsQueenBee draws it over her, adding, with her own turn, size and
    /// flip. One frame a draw: the frame of the first honeyed Queen Bee there is.
    ///
    /// Client only. A fault turns it off for the session and is logged once.
    /// </summary>
    public class HoneyGlint : ModSystem
    {
        /// <summary>Pale gold, near to white.</summary>
        private static readonly Color Light = new Color(255, 238, 170);

        /// <summary>
        /// The broad streaks: their slant, thickness, speed (pixels a tick), spacing and strength
        /// (how much of the light a streak adds at its middle).
        /// </summary>
        private const float BroadAngle = -0.9f;
        private const float BroadWidth = 22f;
        private const float BroadSpeed = 1.2f;
        private const float BroadSpacing = 70f;
        private const float BroadStrength = 0.65f;

        /// <summary>The fine streaks, at a shallower slant, faster.</summary>
        private const float FineAngle = -0.55f;
        private const float FineWidth = 9f;
        private const float FineSpeed = 2.3f;
        private const float FineSpacing = 46f;
        private const float FineStrength = 0.5f;

        /// <summary>The sweep: once every this many ticks, across her in this many, this bright.</summary>
        private const float SweepAngle = -0.75f;
        private const float SweepWidth = 16f;
        private const int SweepEvery = 120;
        private const int SweepTicks = 28;
        private const float SweepStrength = 1f;

        /// <summary>
        /// Adds the colour drawn, as it is. (BlendState.Additive weighs it by its own alpha first,
        /// which squares a soft streak down to a sliver: half strength would add a quarter.)
        /// </summary>
        private static readonly BlendState Add = new BlendState
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add,
        };

        /// <summary>Keeps what is under what is drawn, times its alpha, and nothing else: the mask.</summary>
        private static readonly BlendState Mask = new BlendState
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.SourceAlpha,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.SourceAlpha,
            AlphaBlendFunction = BlendFunction.Add,
        };

        private static RenderTarget2D target;

        /// <summary>A soft band, one pixel wide: dark at its edges, bright in its middle.</summary>
        private static Texture2D band;

        /// <summary>Which frame of her sheet the target holds this draw (its Y), or -1 for none.</summary>
        private static int held = -1;

        private static bool broken;

        public override void Load()
        {
            if (!Main.dedServ)
                Main.OnPreDraw += Render;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                Main.OnPreDraw -= Render;

            RenderTarget2D oldTarget = target;
            Texture2D oldBand = band;

            target = null;
            band = null;
            held = -1;
            broken = false;

            if (oldTarget != null || oldBand != null)
            {
                Main.QueueMainThreadAction(() =>
                {
                    oldTarget?.Dispose();
                    oldBand?.Dispose();
                });
            }
        }

        /// <summary>The glint for this NPC's frame, frame sized, or null if there is none for it this draw.</summary>
        public static Texture2D For(NPC npc)
        {
            if (held < 0 || npc.frame.Y != held || target == null || target.IsDisposed)
                return null;

            return target;
        }

        /// <summary>The first honeyed Queen Bee, or null.</summary>
        private static NPC Honeyed()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (npc.active && npc.type == NPCID.QueenBee && npc.TryGetGlobalNPC(out LegendsQueenBee state) && state.Honeyed)
                    return npc;
            }

            return null;
        }

        private static void Render(GameTime gameTime)
        {
            held = -1;

            if (broken || Main.gameMenu)
                return;

            NPC bee = Honeyed();

            if (bee == null)
                return;

            Main.instance.LoadNPC(NPCID.QueenBee);

            Texture2D sheet = TextureAssets.Npc[NPCID.QueenBee].Value;
            int width = sheet.Width;
            int height = sheet.Height / Main.npcFrameCount[NPCID.QueenBee];
            Rectangle frame = new Rectangle(0, bee.frame.Y, width, height);

            if (width <= 0 || height <= 0 || frame.Bottom > sheet.Height)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;
            SpriteBatch batch = Main.spriteBatch;
            RenderTargetBinding[] previous = device.GetRenderTargets();
            bool open = false;

            try
            {
                Ensure(device, width, height);

                device.SetRenderTarget(target);
                device.Clear(Color.Transparent);

                batch.Begin(SpriteSortMode.Deferred, Add, SamplerState.LinearClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Matrix.Identity);
                open = true;

                float ticks = Main.GlobalTimeWrappedHourly * 60f;
                Vector2 middle = new Vector2(width, height) * 0.5f;

                Streaks(batch, middle, width, height, BroadAngle, BroadWidth, ticks * BroadSpeed, BroadSpacing, BroadStrength);
                Streaks(batch, middle, width, height, FineAngle, FineWidth, ticks * FineSpeed, FineSpacing, FineStrength);
                Sweep(batch, middle, width, height, ticks);

                batch.End();

                // Her, over it, keeping only what she covers.
                batch.Begin(SpriteSortMode.Deferred, Mask, SamplerState.PointClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Matrix.Identity);

                batch.Draw(sheet, Vector2.Zero, frame, Color.White);

                batch.End();
                open = false;

                held = bee.frame.Y;
            }
            catch (Exception e)
            {
                broken = true;
                held = -1;
                ModContent.GetInstance<HoneyGlint>().Mod.Logger.Error("The Queen Bee's glint failed and is off until the mod reloads.", e);

                if (open)
                {
                    try { batch.End(); }
                    catch { }
                }
            }
            finally
            {
                device.SetRenderTargets(previous);

                // Binding the screen again can wipe it (FNA clears a target of this kind when it
                // is bound). Nothing is on it yet this frame; it is left as the game would have it.
                if (previous.Length == 0 && device.PresentationParameters.RenderTargetUsage == RenderTargetUsage.DiscardContents)
                    device.Clear(Color.Black);
            }
        }

        /// <summary>Evenly spaced streaks at this slant, slid along by <paramref name="travel"/> pixels, wrapping, across the frame.</summary>
        private static void Streaks(SpriteBatch batch, Vector2 middle, int width, int height, float angle, float thickness,
            float travel, float spacing, float strength)
        {
            Vector2 along = angle.ToRotationVector2();
            Vector2 across = new Vector2(-along.Y, along.X);

            // Far enough either way of the middle to cover the frame's corners, and a streak more.
            float reach = 0.5f * (Math.Abs(width * across.X) + Math.Abs(height * across.Y)) + thickness;
            int count = (int)Math.Ceiling(2f * reach / spacing) + 1;
            float period = count * spacing;
            float length = new Vector2(width, height).Length() + thickness * 2f;

            for (int k = 0; k < count; k++)
            {
                float offset = Wrap(travel + k * spacing, period) - period * 0.5f;

                Band(batch, middle + across * offset, angle, length, thickness, Light * strength);
            }
        }

        /// <summary>The bright sweep, once every SweepEvery ticks.</summary>
        private static void Sweep(SpriteBatch batch, Vector2 middle, int width, int height, float ticks)
        {
            float into = Wrap(ticks, SweepEvery);

            if (into > SweepTicks)
                return;

            Vector2 along = SweepAngle.ToRotationVector2();
            Vector2 across = new Vector2(-along.Y, along.X);

            float reach = 0.5f * (Math.Abs(width * across.X) + Math.Abs(height * across.Y)) + SweepWidth;
            float offset = MathHelper.Lerp(-reach, reach, into / SweepTicks);
            float length = new Vector2(width, height).Length() + SweepWidth * 2f;

            Band(batch, middle + across * offset, SweepAngle, length, SweepWidth, Color.White * SweepStrength);
        }

        private static void Band(SpriteBatch batch, Vector2 centre, float angle, float length, float thickness, Color colour)
        {
            batch.Draw(band, centre, null, colour, angle, new Vector2(0.5f, band.Height * 0.5f),
                new Vector2(length, thickness / band.Height), SpriteEffects.None, 0f);
        }

        private static float Wrap(float value, float period)
        {
            float wrapped = value % period;
            return wrapped < 0f ? wrapped + period : wrapped;
        }

        private static void Ensure(GraphicsDevice device, int width, int height)
        {
            if (band == null || band.IsDisposed)
            {
                const int Size = 64;
                Color[] pixels = new Color[Size];

                for (int y = 0; y < Size; y++)
                {
                    float s = (float)Math.Sin(MathHelper.Pi * (y + 0.5f) / Size);
                    float v = s * s;

                    pixels[y] = new Color(v, v, v, v);
                }

                band = new Texture2D(device, 1, Size);
                band.SetData(pixels);
            }

            if (target != null && !target.IsDisposed && target.Width == width && target.Height == height)
                return;

            target?.Dispose();
            target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        }
    }
}
