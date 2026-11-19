using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// The Destroyer's pictures: its eyes' glow in its colour, made from vanilla's own red glow (TextureAssets.Dest) in code
    /// the first time it is drawn (each pixel's light kept, its colour turned to #7fd4d1, its hottest to white); and the
    /// soft lines and glows its beams, bolts and warnings are drawn with. Its pulse laser's beam is drawn on the half-size
    /// canvas (PADPixelCanvas, the two-pixel grid: the user likes it so); its warnings, bolts, lightning and eye trails are
    /// drawn smooth, straight into the world's batch (by request: "not everything need pixelation"), added on as light
    /// (<see cref="Lit"/>). Client only.
    /// </summary>
    internal static class DestroyerArt
    {
        /// <summary>Its colour: lightning and energy (by the user's lore).</summary>
        public static readonly Color Teal = new(127, 212, 209);

        /// <summary>Its colour, lighter, toward white: the hot edge of its light.</summary>
        public static readonly Color TealHot = new(196, 246, 242);

        /// <summary>The yellow its body glows in its storm (by request: "body glows a bright yellow").</summary>
        public static readonly Color Yellow = new(255, 226, 92);

        /// <summary>The red its charge runs from its tail to its head in (by request: "glow spark red").</summary>
        public static readonly Color ChargeRed = new(255, 52, 36);

        private static readonly Texture2D[] tealGlow = new Texture2D[3];

        /// <summary>Its glow could not be made (vanilla's picture not to be read): vanilla's red is drawn instead.</summary>
        private static readonly bool[] failed = new bool[3];

        private static Asset<Texture2D> line, soft, hot;

        public static Texture2D Line => (line ??= ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine")).Value;

        public static Texture2D Soft => (soft ??= ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft")).Value;

        public static Texture2D Hot => (hot ??= ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowHot")).Value;

        public static void Unload()
        {
            System.Collections.Generic.List<Texture2D> made = new(tealGlow);
            made.AddRange(recoloured.Values);
            Array.Clear(tealGlow);
            recoloured.Clear();
            Array.Clear(failed);
            line = soft = hot = null;

            Main.QueueMainThreadAction(() =>
            {
                foreach (Texture2D t in made)
                    t?.Dispose();
            });
        }

        private static readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> recoloured = new();

        /// <summary>Any red glow made in its colour (as <see cref="TealGlow"/>), kept once made; the red itself if it cannot be read. Drawing only.</summary>
        public static Texture2D TealOf(Texture2D red)
        {
            if (recoloured.TryGetValue(red, out Texture2D made))
                return made;

            made = Recolour(red) ?? red;
            recoloured[red] = made;
            return made;
        }

        private static Texture2D Recolour(Texture2D red)
        {
            Color[] pixels = new Color[red.Width * red.Height];

            try
            {
                red.GetData(pixels);
            }
            catch (Exception)
            {
                return null;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];

                if (c.A == 0 && c.R == 0 && c.G == 0 && c.B == 0)
                    continue;

                float light = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f;
                float white = Math.Min(c.G, c.B) / Math.Max(1f, c.R);
                Color to = Color.Lerp(Teal, Color.White, (float)Math.Pow(MathHelper.Clamp(white, 0f, 1f), 1.4f)) * light;
                pixels[i] = new Color(to.R, to.G, to.B, c.A);
            }

            Texture2D texture = new(Main.instance.GraphicsDevice, red.Width, red.Height);
            texture.SetData(pixels);
            return texture;
        }

        /// <summary>Its eyes' glow in its colour, for its head (0), a body segment (1) or its tail (2). Drawing only (the main thread).</summary>
        public static Texture2D TealGlow(int which)
        {
            if (tealGlow[which] != null)
                return tealGlow[which];

            Texture2D red = TextureAssets.Dest[which].Value;

            if (failed[which])
                return red;

            Color[] pixels = new Color[red.Width * red.Height];

            try
            {
                red.GetData(pixels);
            }
            catch (Exception)
            {
                failed[which] = true;
                return red;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];

                if (c.A == 0 && c.R == 0 && c.G == 0 && c.B == 0)
                    continue;

                float light = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f;
                float white = Math.Min(c.G, c.B) / Math.Max(1f, c.R);
                Color to = Color.Lerp(Teal, Color.White, (float)Math.Pow(MathHelper.Clamp(white, 0f, 1f), 1.4f)) * light;
                pixels[i] = new Color(to.R, to.G, to.B, c.A);
            }

            Texture2D made = new(Main.instance.GraphicsDevice, red.Width, red.Height);
            made.SetData(pixels);
            tealGlow[which] = made;
            return made;
        }

        // ---- on the canvas (world places less Main.screenPosition, as the canvas asks) -----------------------------------

        /// <summary>A soft line from one place to another, this wide (its soft edge in it).</summary>
        public static void DrawLine(SpriteBatch batch, Vector2 from, Vector2 to, float width, Color colour)
        {
            Vector2 way = to - from;
            float length = way.Length();

            if (length < 0.5f || width <= 0f)
                return;

            Texture2D t = Line;
            batch.Draw(t, from - Main.screenPosition, null, colour, way.ToRotation(), new Vector2(0f, t.Height * 0.5f),
                new Vector2(length / t.Width, width / t.Height), SpriteEffects.None, 0f);
        }

        /// <summary>A soft glow, this wide across.</summary>
        public static void DrawGlow(SpriteBatch batch, Vector2 at, float wide, Color colour, bool hotCore = false)
        {
            Texture2D t = hotCore ? Hot : Soft;
            batch.Draw(t, at - Main.screenPosition, null, colour, 0f, t.Size() * 0.5f, wide / t.Width, SpriteEffects.None, 0f);
        }

        /// <summary>A stretched glow: a soft oval along a direction.</summary>
        public static void DrawOval(SpriteBatch batch, Vector2 at, float angle, float length, float wide, Color colour)
        {
            Texture2D t = Soft;
            batch.Draw(t, at - Main.screenPosition, null, colour, angle, t.Size() * 0.5f, new Vector2(length / t.Width, wide / t.Height), SpriteEffects.None, 0f);
        }

        /// <summary>A colour added on as light in the world's batch (alpha-blended, premultiplied: no alpha adds).</summary>
        public static Color Lit(Color colour) => new(colour.R, colour.G, colour.B, (byte)0);

        /// <summary>How long a warning line takes to fade once what it warned of has gone (ticks).</summary>
        public const int WarningFade = 16;

        /// <summary>
        /// A warning line, smooth, straight into the world's batch: thin, flickering, brightening to white as it is about to
        /// go (<paramref name="ready"/> 0 to 1); <paramref name="alpha"/> fades it. Full for its first three quarters, then
        /// fading to nothing at its end (by request: "start fading at 3/4 length and opacity = 0 at end").
        /// </summary>
        public static void DrawWarning(SpriteBatch batch, Vector2 from, float angle, float length, float ready, float alpha = 1f)
        {
            if (Main.dedServ || alpha <= 0.01f)
                return;

            float flick = 0.8f + 0.2f * (float)Math.Sin(Main.GameUpdateCount * 0.9f + from.X * 0.01f);
            Color colour = Color.Lerp(Teal, Color.White, ready * ready) * ((0.3f + 0.5f * ready) * flick * alpha);
            float width = 2f + 2.5f * ready;
            Vector2 dir = angle.ToRotationVector2();
            float full = length * 0.75f;

            DrawLine(batch, from, from + dir * full, width * 4f, Lit(colour * 0.3f));
            DrawLine(batch, from, from + dir * full, width, Lit(colour));

            for (int i = 0; i < FadeSteps; i++)
            {
                float a = full + (length - full) * i / FadeSteps;
                float b = full + (length - full) * (i + 1) / FadeSteps;
                float left = 1f - (i + 0.5f) / FadeSteps;
                DrawLine(batch, from + dir * a, from + dir * b, width * 4f, Lit(colour * (0.3f * left)));
                DrawLine(batch, from + dir * a, from + dir * b, width, Lit(colour * left));
            }
        }

        /// <summary>The pieces a warning's last quarter fades out over.</summary>
        private const int FadeSteps = 12;
    }
}
