using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// The light of every waygate on screen (Waygate), drawn after the tiles and before
    /// anything that moves, so the player stands in front of it: a ball of white and gold
    /// over its socket, breathing; rays turning out of it, the long ones slowly, the short
    /// ones a little faster, each its own length and its own way round; three rings of light
    /// wound about it; motes drawn in to it out of the air; and a pool of light on the stone
    /// under it. All of it added to what is behind it, as light is, and all of it worked out
    /// from the clock alone: nothing is kept from one frame to the next but which gate the
    /// cursor is on. Closer, it is brighter and its motes come faster; under the cursor
    /// brighter still.
    ///
    /// The gates are found by looking, every frame, over the screen and as far past its
    /// edges as the longest ray reaches: a tile read per cell, which is nothing.
    ///
    /// Two textures of mine beside this file (PlaceholderTextures.md): WaygateLight, a round
    /// falloff of white, and WaygateRay, a ray of white bright at its root and gone at its
    /// tip. Both are stored premultiplied, as the game blends, and drawn the game's way for
    /// light: blended as usual with no alpha of their own (<see cref="Lit"/>), which adds
    /// them to what is behind. tModLoader does not premultiply a mod's pictures itself
    /// (ImageIO.ToRaw only clears the colour of what is wholly clear).
    /// </summary>
    public class WaygateRadiance : ModSystem
    {
        /// <summary>How far past the screen to look for a gate, in tiles: the longest ray.</summary>
        private const int Reach = 18;

        /// <summary>Over the middle of its two tiles, this many pixels up, and rising and falling by Bob.</summary>
        private const float Lift = 5f;
        private const float Bob = 1.5f;

        private const int ShortRays = 12;
        private const int LongRays = 4;
        private const int Rings = 3;
        private const int RingBeads = 16;
        private const int Motes = 26;

        /// <summary>Warm white and gold: the light of it.</summary>
        private static readonly Color Gold = new Color(255, 214, 150);
        private static readonly Color Pale = new Color(255, 238, 205);

        private static Asset<Texture2D> light;
        private static Asset<Texture2D> ray;

        /// <summary>The gate under the cursor this frame (Waygate.MouseOver), its top left tile; (-1, -1) for none.</summary>
        internal static Point16 Hovered = new Point16(-1, -1);

        private static Point16 hoveredDrawn = new Point16(-1, -1);

        /// <summary>The slow swell of every gate's light, about 0.92 to 1: their glow, and the light they give the tiles (Waygate.ModifyLight).</summary>
        public static float Breath => 0.96f + 0.04f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.1f);

        public override void Load()
        {
            if (Main.dedServ)
                return;

            light = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateLight");
            ray = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateRay");
        }

        public override void Unload()
        {
            light = null;
            ray = null;
        }

        public override void PostUpdateEverything()
        {
            // What the cursor was on is read by the next frame's drawing, and asked again each
            // update (Waygate.MouseOver comes in the player's update, before this).
            hoveredDrawn = Hovered;
            Hovered = new Point16(-1, -1);
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || light == null || !light.IsLoaded || !ray.IsLoaded)
                return;

            int type = ModContent.TileType<Waygate>();

            int left = Math.Max(0, (int)(Main.screenPosition.X / 16f) - Reach);
            int top = Math.Max(0, (int)(Main.screenPosition.Y / 16f) - Reach);
            int right = Math.Min(Main.maxTilesX - 1, (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + Reach);
            int bottom = Math.Min(Main.maxTilesY - 1, (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + Reach);

            bool begun = false;

            try
            {
                for (int x = left; x <= right; x++)
                {
                    for (int y = top; y <= bottom; y++)
                    {
                        Tile tile = Main.tile[x, y];

                        if (!tile.HasTile || tile.TileType != type || tile.TileFrameX != 0 || tile.TileFrameY != 0)
                            continue;

                        if (!begun)
                        {
                            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                            begun = true;
                        }

                        DrawGate(Main.spriteBatch, x, y);
                    }
                }
            }
            finally
            {
                if (begun)
                    Main.spriteBatch.End();
            }
        }

        /// <summary>The middle of the light of the gate whose top left tile this is, in world pixels.</summary>
        public static Vector2 Heart(int left, int top)
        {
            float t = Main.GlobalTimeWrappedHourly;

            return new Vector2((left + Waygate.Wide * 0.5f) * 16f, (top + Waygate.Tall * 0.5f) * 16f - Lift + Bob * (float)Math.Sin(t * 1.3f + left));
        }

        private static void DrawGate(SpriteBatch batch, int left, int top)
        {
            Texture2D glow = light.Value;
            Texture2D beam = ray.Value;

            float t = Main.GlobalTimeWrappedHourly;
            Vector2 heart = Heart(left, top);
            Vector2 at = heart - Main.screenPosition;
            int seed = left * 7919 + top * 104729;

            // Nearer, stronger: full within eight tiles of the player, its least past twenty-four.
            float distance = Vector2.Distance(Main.LocalPlayer.Center, heart) / 16f;
            float near = 1f - MathHelper.Clamp((distance - 8f) / 16f, 0f, 1f);
            bool hovered = hoveredDrawn.X == left && hoveredDrawn.Y == top;
            float strength = Breath * (0.78f + 0.22f * near) * (hovered ? 1.18f : 1f);

            Vector2 glowOrigin = glow.Size() * 0.5f;
            float glowSize = glow.Width;

            // A pool of light on the stone it stands over.
            Vector2 floor = new Vector2((left + Waygate.Wide * 0.5f) * 16f, (top + Waygate.Tall) * 16f - 3f) - Main.screenPosition;
            batch.Draw(glow, floor, null, Lit(Gold, 0.42f * strength), 0f, glowOrigin, new Vector2(78f, 16f) / glowSize, SpriteEffects.None, 0f);

            // The air about it, lit.
            batch.Draw(glow, at, null, Lit(Gold, 0.30f * strength), 0f, glowOrigin, 150f / glowSize, SpriteEffects.None, 0f);
            batch.Draw(glow, at, null, Lit(Gold, 0.38f * strength), 0f, glowOrigin, 64f / glowSize, SpriteEffects.None, 0f);

            // The rays: from the heart outward, the root bright and the tip gone.
            Vector2 beamOrigin = new Vector2(beam.Width * 0.5f, beam.Height);

            for (int i = 0; i < ShortRays + LongRays; i++)
            {
                bool longRay = i >= ShortRays;
                float h1 = Hash(seed + i * 31);
                float h2 = Hash(seed + i * 57 + 11);
                float h3 = Hash(seed + i * 89 + 23);

                int count = longRay ? LongRays : ShortRays;
                int index = longRay ? i - ShortRays : i;

                float turn = (longRay ? 0.05f : 0.11f) * (index % 2 == 0 ? 1f : -1f) * (0.6f + 0.8f * h1);
                float angle = MathHelper.TwoPi * (index + 0.5f * h2) / count + t * turn + (longRay ? 0.4f : 0f);

                float swell = 0.5f + 0.5f * (float)Math.Sin(t * (0.7f + 0.9f * h3) + h1 * 20f);
                float length = (longRay ? 150f + 70f * h2 : 70f + 60f * h3) * (0.75f + 0.25f * swell);
                float width = longRay ? 7f + 3f * h1 : 3f + 3f * h2;
                float alpha = (longRay ? 0.32f : 0.20f) * (0.55f + 0.45f * swell) * strength;

                batch.Draw(beam, at, null, Lit(Pale, alpha), angle + MathHelper.PiOver2, beamOrigin,
                    new Vector2(width / beam.Width, length / beam.Height), SpriteEffects.None, 0f);
            }

            // Rings of light wound about it, each tilted its own way and turning.
            for (int r = 0; r < Rings; r++)
            {
                float tilt = r * MathHelper.Pi / Rings + t * 0.25f * (r % 2 == 0 ? 1f : -1f);
                float spin = t * (1.6f + 0.5f * r) * (r == 1 ? -1f : 1f);
                float across = 16f + 3f * r;
                float through = 5f + 1.5f * r;

                for (int b = 0; b < RingBeads; b++)
                {
                    float phase = spin + MathHelper.TwoPi * b / RingBeads;

                    // The leading bead brightest, the rest trailing off behind it: a turning tail.
                    float tail = (b / (float)RingBeads);
                    float bead = (1f - tail) * (1f - tail);

                    Vector2 onRing = new Vector2((float)Math.Cos(phase) * across, (float)Math.Sin(phase) * through).RotatedBy(tilt);

                    batch.Draw(glow, at + onRing, null, Lit(Pale, 0.55f * bead * strength), 0f, glowOrigin, (3.5f + 2f * bead) / glowSize, SpriteEffects.None, 0f);
                }
            }

            // Motes drawn in to it, each on a curve of its own, coming and going.
            float pace = (1f + 0.6f * near) * (hovered ? 1.5f : 1f);

            for (int m = 0; m < Motes; m++)
            {
                float h1 = Hash(seed + 1000 + m * 13);
                float h2 = Hash(seed + 2000 + m * 17);
                float h3 = Hash(seed + 3000 + m * 19);

                float period = 3f + 3f * h1;
                float life = Frac(t * pace / period + h2);
                float from = 40f + 70f * h3;
                float radius = from * (float)Math.Pow(1f - life, 1.4f);
                float angle = MathHelper.TwoPi * h1 + life * (2.2f + 2f * h2) * (m % 2 == 0 ? 1f : -1f);

                Vector2 mote = at + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle) * 0.8f) * radius;
                float fade = (float)Math.Sin(life * MathHelper.Pi);

                batch.Draw(glow, mote, null, Lit(Pale, 0.75f * fade * strength), 0f, glowOrigin, (2.5f + 2.5f * h3) / glowSize, SpriteEffects.None, 0f);
            }

            // The heart of it: white, and whiter in the middle.
            float pulse = 1f + 0.06f * (float)Math.Sin(t * 2.3f + seed);

            batch.Draw(glow, at, null, Lit(Pale, 0.85f * strength), 0f, glowOrigin, 30f * pulse / glowSize, SpriteEffects.None, 0f);
            batch.Draw(glow, at, null, Lit(Color.White, 0.95f * strength), 0f, glowOrigin, 15f * pulse / glowSize, SpriteEffects.None, 0f);
            batch.Draw(glow, at, null, Lit(Color.White, strength), 0f, glowOrigin, 7f / glowSize, SpriteEffects.None, 0f);
        }

        /// <summary>A colour at this strength, with no alpha: drawn blended as usual over what is behind it, it is added to it.</summary>
        private static Color Lit(Color colour, float strength)
        {
            strength = MathHelper.Clamp(strength, 0f, 1f);

            return new Color((byte)(colour.R * strength), (byte)(colour.G * strength), (byte)(colour.B * strength), (byte)0);
        }

        /// <summary>A number from 0 to 1 that is always the same for the same seed.</summary>
        private static float Hash(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static float Frac(float v) => v - (float)Math.Floor(v);
    }
}
