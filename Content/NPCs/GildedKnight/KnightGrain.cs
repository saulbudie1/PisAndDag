using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// One golden grain of the knight coming apart. By request, the Guide's perish
    /// (GuideSacrifice, AshDissolve) in gold: his picture as it was painted that frame is read
    /// pixel by pixel and every filled one becomes a grain, starting exactly where it was drawn,
    /// so for a moment the cloud is still the shape of him kneeling over his sword; then the
    /// air takes it.
    ///
    /// AshDissolve's way exactly, with three things changed: the colour keeps the brightness and
    /// lays it on gold (dark plate a deep old gold, lit plate bright gold, the brightest pale);
    /// everything of it goes three times as slowly (his end does); and the grains rise a little
    /// as they go, as light would. The picture is AshDissolve's own fleck.
    /// </summary>
    public class KnightGrain : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/AshDissolve";

        public override bool SetLifetime => true;

        /// <summary>One body's worth at once, and bounded (MaxGrains): his end is not to be thinned by whatever else is about.</summary>
        public override bool Important => true;

        /// <summary>0 to 1. Dark grains give up sooner, bright ones hang on.</summary>
        private readonly float brightness;

        private readonly int id;
        private readonly Color gold;

        /// <summary>Shrink per tick, for a dark grain and for a bright one (AshDissolve's, a third as fast).</summary>
        private const float FadeDark = 0.018f;
        private const float FadeBright = 0.002f;

        /// <summary>Below this a pixel is too dark to be worth a grain.</summary>
        public const float DarkFloor = 0.06f;

        /// <summary>
        /// The most grains his body makes, and the Sun after it: together well inside the particles'
        /// limit (PADParticleHandler.ParticleLimit), so they never shut out everything else.
        /// </summary>
        public const int MaxGrains = 600;
        public const int SunGrains = 220;

        /// <summary>How long a grain lasts (AshDissolve's 105 to 145, about twice).</summary>
        public const int LifeMin = 220;
        public const int LifeMax = 290;

        public KnightGrain(Vector2 position, Vector2 velocity, float pixelBrightness, int lifetime, int index)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = Main.rand.NextFloat(0.7f, 1.3f);

            brightness = pixelBrightness;
            id = index;
            gold = Golden(pixelBrightness);
            Color = gold;
        }

        /// <summary>A pixel's brightness laid on gold: deep old gold, bright gold, pale.</summary>
        public static Color Golden(float b)
        {
            b = MathHelper.Clamp(b, 0f, 1f);

            Color deep = new Color(104, 60, 18);
            Color gold = new Color(232, 172, 70);
            Color pale = new Color(255, 238, 178);

            return b < 0.55f ? Color.Lerp(deep, gold, b / 0.55f) : Color.Lerp(gold, pale, (b - 0.55f) / 0.45f);
        }

        public override void Update()
        {
            // Dark grains go about nine times sooner than bright ones, so the dim edges of him go
            // first and his lit plate is the last thing left (AshDissolve's reason, unchanged).
            Scale = MathHelper.Clamp(Scale - (brightness < 0.1f ? FadeDark : FadeBright), 0f, 1f);

            // From how he came apart to whatever the weather is doing, slowly; and rising a little.
            float thrown = Utils.GetLerpValue(2f, 6f, Velocity.Length(), true);
            float handover = Utils.GetLerpValue(60f - thrown * 20f, 200f - thrown * 100f, Time, true);

            Vector2 drift = new Vector2(
                Main.windSpeedCurrent * MathHelper.Lerp(0.8f, 1.2f, MathF.Sin(Position.Y / 50f + id)) * 4f,
                MathF.Sin((float)Main.time / 40f + id * 0.01f) * 0.35f - 0.5f);

            Velocity = Vector2.Lerp(Velocity, drift, handover * 0.06f);

            float c = LifetimeCompletion;
            Color = gold * (1f - c * c);
        }

        // ---- turning a picture into grains ---------------------------------------------------------

        /// <summary>
        /// A picture's filled pixels as grains: <paramref name="pixels"/> (width by height, as
        /// SpriteBatch would have drawn them, premultiplied), its rows from <paramref name="top"/>
        /// to <paramref name="bottom"/> only; each pixel (x, y) placed in the world by
        /// <paramref name="place"/>. Sampled every <c>stride</c> pixels, the finest that keeps
        /// within MaxGrains. Returns how many it made.
        /// </summary>
        public static int FromPixels(Color[] pixels, int width, int height, int top, int bottom, Func<float, float, Vector2> place, Vector2 velocity, int most = MaxGrains)
        {
            if (Main.dedServ || pixels == null)
                return 0;

            top = Math.Clamp(top, 0, height);
            bottom = Math.Clamp(bottom, top, height);

            int filled = 0;

            for (int y = top; y < bottom; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (Filled(pixels[x + y * width], out _))
                        filled++;
                }
            }

            if (filled == 0)
                return 0;

            int stride = 1;

            while (filled / (stride * stride) > most && stride < 8)
                stride++;

            int made = 0;

            for (int y = top; y < bottom; y += stride)
            {
                for (int x = 0; x < width; x += stride)
                {
                    if (!Filled(pixels[x + y * width], out float b))
                        continue;

                    if (made >= most)
                        return made;

                    Vector2 at = place(x + stride * 0.5f, y + stride * 0.5f);

                    PADParticleHandler.SpawnParticle(new KnightGrain(at, velocity + Main.rand.NextVector2Circular(0.25f, 0.25f),
                        b, Main.rand.Next(LifeMin, LifeMax), made));
                    made++;
                }
            }

            return made;
        }

        /// <summary>A pixel worth a grain, and its brightness (its colour over its own cover: the picture is premultiplied).</summary>
        private static bool Filled(Color pixel, out float brightness)
        {
            brightness = 0f;

            if (pixel.A < 40)
                return false;

            brightness = MathHelper.Clamp((pixel.R + pixel.G + pixel.B) / (3f * pixel.A), 0f, 1f);
            return brightness > DarkFloor;
        }

        /// <summary>A texture's pixels, read once (GetData is a read back from the graphics card: never every frame).</summary>
        public static Color[] Read(Texture2D texture)
        {
            if (texture == null || texture.IsDisposed)
                return null;

            Color[] data = new Color[texture.Width * texture.Height];

            try
            {
                texture.GetData(data);
            }
            catch
            {
                // A picture the graphics device will not read back is not worth taking the game
                // down over: his end just loses its grains.
                return null;
            }

            return data;
        }
    }
}
