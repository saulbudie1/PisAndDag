using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The heralds becoming the King: blobs of slime that slide to the altar and run
    /// together into one body, which is then the King's own shape and size.
    ///
    /// Drawn as one thing, not twenty. Every blob and the growing dome are distance
    /// fields, joined with a smooth minimum, so where two touch their outlines flow
    /// into one outline instead of crossing, and a blob arriving at the mass is swallowed
    /// by its rim rather than drawn on top of it. The joined field is sampled on a
    /// two pixel grid over the altar into a small texture each frame: inside is the
    /// fill, the few pixels outside that are the rim, and the rest is nothing. The fill
    /// is a slime's blue at a slime's translucency, so what is behind shows through the
    /// way it does through the King; the rim is solid navy.
    ///
    /// The dome is sized from the King: a half ellipse as wide and as tall as he will
    /// be, and the blobs are scaled at the start so their areas add up to its area, so
    /// twenty small ones make exactly one King. It grows by area as they land.
    ///
    /// Client only. The server has nothing to draw and does not run this.
    /// </summary>
    public class SlimeMass
    {
        /// <summary>Grid step, in pixels. Two is fine enough for the rim to read.</summary>
        public const int Cell = 2;

        /// <summary>How far apart two surfaces start to pull together, in pixels.</summary>
        public const float Smooth = 9f;

        public const float RimWidth = 3f;

        public static readonly Color Fill = new Color(48, 108, 245);
        public static readonly Color Rim = new Color(12, 22, 78);
        public static readonly Color Glow = new Color(70, 140, 255);

        /// <summary>How much of the fill shows. A slime is see-through.</summary>
        public const float FillAlpha = 0.72f;

        /// <summary>The blobs still on their way. Arrived ones are area in the dome.</summary>
        private readonly List<Blob> blobs = new();

        private struct Blob
        {
            public Vector2 From;
            public Vector2 Position;
            public float Radius;
            public int Delay;
            public int Travel;
            public int Time;
            public float Phase;

            public float Progress => MathHelper.Clamp((Time - Delay) / (float)Travel, 0f, 1f);
        }

        /// <summary>Bottom centre of the dome, on the ground.</summary>
        private Vector2 altar;

        /// <summary>
        /// The finished body: an ellipse as wide as the King, standing a little into the
        /// ground so its bottom is cut flat. Half width, half height, and where its
        /// centre sits above the ground as a fraction of his height.
        /// </summary>
        private float domeA, domeB, domeLift;
        private float domeArea;

        private const float DomeHalfHeight = 0.55f;
        private const float DomeCentre = 0.45f;

        /// <summary>Area landed so far, and how long since the last landing.</summary>
        private float absorbed;
        private int pulse = 1000;
        private int tick;

        /// <summary>1 while forming, run down to 0 as the King comes out of it.</summary>
        public float Alpha = 1f;

        /// <summary>Grows a little as it fades, like a skin letting go.</summary>
        public float Spread = 1f;

        private bool completed;

        // ---- the raster --------------------------------------------------------

        private Texture2D texture;
        private Color[] pixels;
        private int cols, rows;
        private Vector2 topLeft;

        public bool Complete => blobs.Count == 0;
        public float Progress => domeArea <= 0f ? 0f : MathHelper.Clamp(absorbed / domeArea, 0f, 1f);

        /// <summary>Current dome radii, from the area landed and the bounce.</summary>
        private Vector2 DomeRadii
        {
            get
            {
                float s = (float)Math.Sqrt(Progress) * Spread;
                float ring = (float)Math.Exp(-pulse / 12f) * (float)Math.Cos(pulse * 0.5f) * 0.12f;
                float breath = 0.025f * (float)Math.Sin(tick * 0.11f);

                return new Vector2(domeA * s * (1f + ring + breath), domeB * s * (1f - ring - breath));
            }
        }

        /// <param name="ground">Bottom centre of the dome.</param>
        /// <param name="width">The King's width when he stands here.</param>
        /// <param name="height">The King's height when he stands here.</param>
        /// <param name="reach">How far either side of the altar blobs will come from.</param>
        public SlimeMass(Vector2 ground, float width, float height, float reach)
        {
            altar = ground;
            domeA = width * 0.5f;
            domeB = height * DomeHalfHeight;
            domeLift = height * DomeCentre;

            // Its area, counted rather than worked out, since the ground cuts it.
            Vector2 full = new Vector2(domeA, domeB);
            int count = 0;

            for (float y = altar.Y - height - 2f; y <= altar.Y; y += 1f)
                for (float x = altar.X - domeA - 2f; x <= altar.X + domeA + 2f; x += 1f)
                    if (DomeDistance(new Vector2(x, y), full) <= 0f)
                        count++;

            domeArea = count;

            float halfWide = reach + 80f;
            float tall = height * 1.35f + 60f;

            topLeft = new Vector2(altar.X - halfWide, altar.Y - tall);
            cols = (int)(halfWide * 2f / Cell);
            rows = (int)((tall + 12f) / Cell);
            pixels = new Color[cols * rows];
        }

        public void AddBlob(Vector2 bottom, float radius, int delay, int travel)
        {
            blobs.Add(new Blob
            {
                From = bottom,
                Position = bottom,
                Radius = radius,
                Delay = delay,
                Travel = travel,
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
            });
        }

        /// <summary>Scales every blob so their areas add up to the dome's. Twenty of them are one King.</summary>
        public void Normalise()
        {
            float total = 0f;

            foreach (Blob blob in blobs)
                total += MathHelper.Pi * blob.Radius * blob.Radius;

            if (total <= 0f)
                return;

            float k = (float)Math.Sqrt(domeArea / total);

            for (int i = 0; i < blobs.Count; i++)
            {
                Blob blob = blobs[i];
                blob.Radius *= k;
                blobs[i] = blob;
            }
        }

        public void Update()
        {
            tick++;
            pulse++;

            for (int i = blobs.Count - 1; i >= 0; i--)
            {
                Blob blob = blobs[i];
                blob.Time++;

                float t = blob.Progress;

                // Slow start, hard finish: it leans in, then is pulled the last of the way.
                float eased = MathHelper.Lerp(t * t * (3f - 2f * t), t * t, 0.5f);

                Vector2 start = new Vector2(blob.From.X, blob.From.Y - blob.Radius);
                Vector2 rest = new Vector2(altar.X, altar.Y - blob.Radius);

                blob.Position = Vector2.Lerp(start, rest, eased);
                blob.Position.Y -= (float)Math.Sin(t * MathHelper.Pi) * blob.Radius * 0.6f;

                // Once it is inside the dome's rim it is the dome. Taken in a little
                // before it reaches the centre, so the last of its slide happens under
                // the surface rather than on it.
                Vector2 radii = DomeRadii;
                bool inside = radii.X > 1f && Math.Abs(blob.Position.X - altar.X) < radii.X * 0.55f;

                if (t >= 1f || inside)
                {
                    Land(blob);
                    blobs.RemoveAt(i);
                    continue;
                }

                blobs[i] = blob;
            }

            if (!completed && Complete && absorbed > 0f)
            {
                completed = true;
                Finished();
            }

            Vector2 now = DomeRadii;

            if (now.X > 2f)
                Lighting.AddLight(new Vector2(altar.X, altar.Y - now.Y), 0.3f * Progress + 0.1f, 0.5f * Progress + 0.15f, 1f);
        }

        private void Land(Blob blob)
        {
            absorbed += MathHelper.Pi * blob.Radius * blob.Radius;
            pulse = 0;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = Main.rand.NextFloat(0.1f, 0.4f), Volume = 0.35f }, altar);

            Vector2 radii = DomeRadii;

            for (int i = 0; i < 4; i++)
            {
                Dust dust = Dust.NewDustPerfect(new Vector2(altar.X, altar.Y - radii.Y) + Main.rand.NextVector2Circular(radii.X, radii.Y),
                    DustID.t_Slime, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)), 120,
                    new Color(0, 80, 255, 100), Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = true;
            }
        }

        /// <summary>The last one is in. The whole thing lights up and throws rays.</summary>
        private void Finished()
        {
            Vector2 radii = DomeRadii;
            Vector2 heart = new Vector2(altar.X, altar.Y - radii.Y);

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.8f }, altar);

            PADParticleHandler.SpawnParticle(new BloomGeneric(heart, Vector2.Zero, Glow, radii.X / 40f, 26));
            PADParticleHandler.SpawnParticle(new BloomGeneric(heart, Vector2.Zero, Color.White, radii.X / 80f, 14, lights: false));

            Color[] palette = { Glow, new Color(160, 200, 255), Color.White };
            SparkShard.Spray(heart, Glow, 22, 9f, 44f, 26, falls: false, palette: palette);
        }

        // ---- the field ---------------------------------------------------------

        /// <summary>Distance to the body's surface, negative inside. An ellipse cut flat at the ground.</summary>
        private float DomeDistance(Vector2 p, Vector2 radii)
        {
            if (radii.X < 1f || radii.Y < 1f)
                return 1000f;

            // The centre rises with the size, so a small dome is a small dome on the
            // ground and not a small dome floating where the big one's middle will be.
            float lift = domeLift * (radii.Y / domeB);

            Vector2 q = new Vector2((p.X - altar.X) / radii.X, (p.Y - (altar.Y - lift)) / radii.Y);
            float ellipse = (q.Length() - 1f) * Math.Min(radii.X, radii.Y);

            return Math.Max(ellipse, p.Y - altar.Y);
        }

        /// <summary>Distance to a blob's surface, negative inside, with its jelly stretch.</summary>
        private static float BlobDistance(Vector2 p, in Blob blob)
        {
            float t = blob.Progress;
            float moving = (float)Math.Sin(t * MathHelper.Pi);
            float wobble = 0.08f * (float)Math.Sin(blob.Time * 0.35f + blob.Phase);

            float sx = 1f + 0.18f * moving + wobble;
            float sy = 1f - 0.12f * moving - wobble;

            Vector2 d = new Vector2((p.X - blob.Position.X) / sx, (p.Y - blob.Position.Y) / sy);
            return d.Length() - blob.Radius;
        }

        /// <summary>Smooth union of everything at a point. Surfaces closer than Smooth melt together.</summary>
        private float Field(Vector2 p, Vector2 radii)
        {
            float reach = Smooth * 4f;
            float sum = 0f;
            float nearest = 1000f;

            float dome = DomeDistance(p, radii);

            if (dome < reach)
            {
                sum += (float)Math.Exp(-dome / Smooth);
                nearest = dome;
            }

            for (int i = 0; i < blobs.Count; i++)
            {
                Blob blob = blobs[i];
                float box = blob.Radius * 1.3f + reach;

                if (Math.Abs(p.X - blob.Position.X) > box || Math.Abs(p.Y - blob.Position.Y) > box)
                    continue;

                float d = BlobDistance(p, blob);

                if (d < reach)
                {
                    sum += (float)Math.Exp(-d / Smooth);
                    nearest = Math.Min(nearest, d);
                }
            }

            if (sum <= 0f)
                return nearest;

            return -Smooth * (float)Math.Log(sum);
        }

        // ---- drawing -----------------------------------------------------------

        public void Draw(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (Alpha <= 0f)
                return;

            texture ??= new Texture2D(Main.graphics.GraphicsDevice, cols, rows);

            Vector2 radii = DomeRadii;

            // The glow first, under everything: from inside the dome, brighter the more
            // of him there is, and a flare when he is whole.
            if (radii.X > 2f)
            {
                Texture2D bloom = ModContent.Request<Texture2D>("PisAndDag/Content/Particles/BloomGeneric").Value;
                float flare = completed ? 1f + 0.4f * (float)Math.Exp(-pulse / 20f) : 1f;
                float strength = (0.35f + 0.35f * Progress) * flare * Alpha * (1f + 0.15f * (float)Math.Sin(tick * 0.2f));

                Color glow = Glow * strength;
                glow.A = 0;

                Vector2 heart = new Vector2(altar.X, altar.Y - radii.Y * 0.9f) - screenPos;
                spriteBatch.Draw(bloom, heart, null, glow, 0f, bloom.Size() * 0.5f,
                    new Vector2(radii.X * 2.8f, radii.Y * 3.6f) / bloom.Width, SpriteEffects.None, 0f);
            }

            Raster(radii);

            texture.SetData(pixels);

            spriteBatch.Draw(texture, topLeft - screenPos, null, Color.White, 0f, Vector2.Zero, Cell, SpriteEffects.None, 0f);
        }

        /// <summary>Samples the field into the pixels: fill inside, rim just outside, nothing beyond.</summary>
        private void Raster(Vector2 radii)
        {
            Color fill = Fill * (FillAlpha * Alpha);
            Color rim = Rim * Alpha;

            // The rim goes first as the King comes out, so the skin thins before it goes.
            float rimShow = MathHelper.Clamp(Alpha * 1.6f - 0.6f, 0f, 1f);
            rim *= rimShow;

            for (int y = 0; y < rows; y++)
            {
                float wy = topLeft.Y + (y + 0.5f) * Cell;

                for (int x = 0; x < cols; x++)
                {
                    float wx = topLeft.X + (x + 0.5f) * Cell;
                    float d = Field(new Vector2(wx, wy), radii);

                    Color colour;

                    if (d <= -0.5f)
                        colour = fill;
                    else if (d <= 0.5f)
                        colour = Color.Lerp(fill, rim, d + 0.5f);
                    else if (d <= RimWidth - 0.5f)
                        colour = rim;
                    else if (d <= RimWidth + 0.5f)
                        colour = rim * (RimWidth + 0.5f - d);
                    else
                        colour = Color.Transparent;

                    pixels[y * cols + x] = colour;
                }
            }
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
