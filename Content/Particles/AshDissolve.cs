using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// One fleck of a body coming apart.
    ///
    /// The effect these build is a sprite dissolving into drifting ash: the NPC's own
    /// texture is read pixel by pixel and every filled pixel becomes one of these,
    /// starting exactly where that pixel was drawn. For the first moment the cloud still
    /// reads as the shape of whatever died, and then the wind takes it.
    ///
    /// Colour throws away hue and keeps only brightness, which is the whole trick — a
    /// cloud in the corpse's original colours looks like the sprite has been shattered,
    /// where grey reads as the thing having burnt. The silhouette and the shading survive
    /// either way, because brightness is what carried them.
    /// </summary>
    public class AshDissolve : PADParticle
    {
        public override bool SetLifetime => true;

        /// <summary>0 to 1. Dark flecks give up quickly, bright ones hang on.</summary>
        private readonly float brightness;

        private readonly int id;

        /// <summary>How grey the ash is at its brightest. Ash is never white.</summary>
        private const float AshCeiling = 0.67f;

        /// <summary>Shrink per tick, for a dark fleck and for a bright one.</summary>
        private const float FadeDark = 0.055f;
        private const float FadeBright = 0.006f;

        /// <summary>Below this a pixel is too dark to be worth a particle at all.</summary>
        public const float DarkFloor = 0.06f;

        public AshDissolve(Vector2 position, Vector2 velocity, float pixelBrightness, int lifetime, int index)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = Main.rand.NextFloat(0.7f, 1.3f);

            brightness = pixelBrightness;
            id = index;

            float grey = pixelBrightness * AshCeiling;
            Color = new Color(grey, grey, grey, 1f);
        }

        public override void Update()
        {
            // Dark flecks vanish about nine times faster than bright ones, so the dim
            // edges of the silhouette go first and its lit core is the last thing left.
            // Doing it the other way round, or evenly, loses the shape immediately.
            Scale = MathHelper.Clamp(Scale - (brightness < 0.1f ? FadeDark : FadeBright), 0f, 1f);

            // Hands over from however the body was moving to whatever the weather is
            // doing, and takes longer to hand over the faster it was thrown. The per-fleck
            // phase offsets are what stop a thousand of these moving as one sheet.
            float thrown = Utils.GetLerpValue(6f, 16f, Velocity.Length(), true);
            float handover = Utils.GetLerpValue(25f - thrown * 10f, 80f - thrown * 45f, Time, true);

            Vector2 drift = new Vector2(
                Main.windSpeedCurrent * MathHelper.Lerp(0.8f, 1.2f, MathF.Sin(Position.Y / 50f + id)) * 20f,
                MathF.Sin((float)Main.time / 20f + id * 0.01f) * 3f - 1f);

            Velocity = Vector2.Lerp(Velocity, drift, handover * 0.16f);

            Color = Color * (1f - LifetimeCompletion * LifetimeCompletion);
        }

        // ---- turning a sprite into ash --------------------------------------

        /// <summary>
        /// Pixel data per NPC texture, read once.
        ///
        /// Texture2D.GetData pulls from the GPU and is far too slow to run per death.
        /// The data never changes, so one read per type for the session is plenty.
        /// </summary>
        private static readonly Dictionary<int, Color[]> pixelCache = new Dictionary<int, Color[]>();

        /// <summary>Ceiling on flecks from one body, so a dissolve cannot flood the budget.</summary>
        private const int MaxFlecks = 420;

        private const int LifeMin = 105;
        private const int LifeMax = 145;

        /// <summary>
        /// Turns an NPC's current frame into a cloud of ash sitting exactly where it was
        /// drawn, and returns how many flecks it made.
        ///
        /// Reads the sprite sheet directly rather than screenshotting the NPC to a render
        /// target the way a boss-scale version would. For anything drawn plainly — which
        /// a town NPC is — the sheet IS what appears on screen, and this needs no render
        /// target, no shader and no extra draw pass. A boss with custom draw code, glow
        /// masks or afterimages would need the screenshot; this one does not.
        /// </summary>
        public static int FromNPC(NPC npc, Vector2 velocity)
        {
            if (Main.dedServ || npc == null || !npc.active)
                return 0;

            Texture2D sheet = Sheet(npc.type);

            if (sheet == null)
                return 0;

            Rectangle frame = npc.frame;

            if (frame.Width <= 0 || frame.Height <= 0)
                frame = new Rectangle(0, 0, sheet.Width, sheet.Height);

            if (!pixelCache.TryGetValue(npc.type, out Color[] pixels))
            {
                pixels = new Color[sheet.Width * sheet.Height];

                try
                {
                    sheet.GetData(pixels);
                }
                catch
                {
                    // A texture the graphics device will not read back is not worth
                    // taking the game down over; the death just loses its dissolve.
                    return 0;
                }

                pixelCache[npc.type] = pixels;
            }

            // Sampled coarser the bigger the sprite is, so a large body becomes a sparser
            // cloud rather than a thousand flecks the handler would refuse anyway.
            int stride = Stride(frame);

            // Bottom-centre, which is where a walking sprite's feet are. Anchoring at the
            // top-left instead would float the cloud above whatever died.
            Vector2 topLeft = npc.Bottom - new Vector2(frame.Width * 0.5f, frame.Height);

            bool flipped = npc.spriteDirection == 1;
            int made = 0;

            for (int x = 0; x < frame.Width; x += stride)
            {
                for (int y = 0; y < frame.Height; y += stride)
                {
                    int sheetX = frame.X + x;
                    int sheetY = frame.Y + y;

                    if (sheetX >= sheet.Width || sheetY >= sheet.Height)
                        continue;

                    Color pixel = pixels[sheetX + sheetY * sheet.Width];

                    if (pixel.A < 40)
                        continue;

                    float brightness = (pixel.R + pixel.G + pixel.B) / 765f;

                    if (brightness <= DarkFloor)
                        continue;

                    if (made >= MaxFlecks)
                        return made;

                    // The sprite is mirrored on screen when it faces the other way, so the
                    // cloud has to be mirrored too or it starts as a reversed silhouette.
                    float drawX = flipped ? frame.Width - 1 - x : x;

                    AshDissolve ash = new AshDissolve(
                        topLeft + new Vector2(drawX, y),
                        velocity + Main.rand.NextVector2Circular(0.4f, 0.4f),
                        brightness,
                        Main.rand.Next(LifeMin, LifeMax),
                        made);

                    PADParticleHandler.SpawnParticle(ash);
                    made++;
                }
            }

            return made;
        }

        private static int Stride(Rectangle frame)
        {
            int area = frame.Width * frame.Height;

            if (area > 12000)
                return 3;

            if (area > 4000)
                return 2;

            return 1;
        }

        private static Texture2D Sheet(int type)
        {
            if (type < 0 || type >= TextureAssets.Npc.Length)
                return null;

            // Requesting it immediately rather than assuming: an NPC type nothing has
            // drawn yet this session may not have had its sheet loaded.
            if (!TextureAssets.Npc[type].IsLoaded)
                Main.instance.LoadNPC(type);

            return TextureAssets.Npc[type].IsLoaded ? TextureAssets.Npc[type].Value : null;
        }
    }
}
