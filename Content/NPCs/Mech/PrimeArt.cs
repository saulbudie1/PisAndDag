using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// Skeletron Prime's colours and the pictures made for it in code. Its element (by the user's lore): nuclear, the
    /// green #bbff00.
    ///
    ///   GreenOf      vanilla's own picture with everything red in it turned to that green (its eyes: the only red in
    ///                its head's picture), each pixel's whiteness kept, so its eyes' hot middles stay hot;
    ///   GlowGreenOf  vanilla's red glow (its eyes') in that green, its light kept;
    ///   WhiteOf      any picture as its shape alone, white (to be drawn in any colour): its impact frames' cut-outs;
    ///   Wedge        a hard white spike (PrimeWedge.png, made in code: scratchpad prime/art.py), its point at its left;
    ///   Ray          a shaft of light (PrimeRay.png, made in code: scratchpad prime/ray2.py): a thin bright core from its
    ///                left end, a faint haze either side, finer streaks of their own lengths within it, widening and dying
    ///                away, nothing with an edge;
    ///   Chute        a parachute (PrimeChute.png, the same): what its cannon's nukes come down under in Legends.
    ///
    /// Each made the first time it is drawn, on the main thread, and kept; if a picture cannot be read back, the one it
    /// was to be made from is used as it is. Client only.
    /// </summary>
    internal static class PrimeArt
    {
        /// <summary>Its element: nuclear (#bbff00).</summary>
        public static readonly Color Nuclear = new(187, 255, 0);

        /// <summary>Its green toward white: the hot middle of its light.</summary>
        public static readonly Color NuclearHot = new(236, 255, 176);

        /// <summary>Its green, dark: the far end of its light.</summary>
        public static readonly Color NuclearDeep = new(84, 140, 6);

        /// <summary>The red its eyes and its arms' lamps are before it changes.</summary>
        public static readonly Color Red = new(255, 40, 34);
        public static readonly Color RedHot = new(255, 196, 180);

        private static readonly Dictionary<Texture2D, Texture2D> green = new(), glowGreen = new(), white = new();
        private static Asset<Texture2D> wedge, ray, chute;

        // Both read at once the first time they are asked for (while drawing: the main thread), not in the background:
        // its impact frames last a fifth of a second, and are not to be drawn without them.
        public static Texture2D Wedge => (wedge ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Mech/PrimeWedge", AssetRequestMode.ImmediateLoad)).Value;

        public static Texture2D Ray => (ray ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Mech/PrimeRay", AssetRequestMode.ImmediateLoad)).Value;

        /// <summary>The parachute its cannon's nukes come down under (PrimeChute.png, made in code as its other pictures): its cords' ends along its bottom edge.</summary>
        public static Texture2D Chute => (chute ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Mech/PrimeChute", AssetRequestMode.ImmediateLoad)).Value;

        public static void Unload()
        {
            List<Texture2D> made = new();

            foreach (Dictionary<Texture2D, Texture2D> kept in new[] { green, glowGreen, white })
            {
                foreach (KeyValuePair<Texture2D, Texture2D> pair in kept)
                {
                    // What could not be made is the picture it was to be made from: not ours to throw away.
                    if (pair.Value != null && pair.Value != pair.Key)
                        made.Add(pair.Value);
                }

                kept.Clear();
            }

            wedge = ray = chute = null;

            Main.QueueMainThreadAction(() =>
            {
                foreach (Texture2D t in made)
                    t.Dispose();
            });
        }

        private static Texture2D Made(Dictionary<Texture2D, Texture2D> kept, Texture2D source, Func<Color, Color> turn)
        {
            if (kept.TryGetValue(source, out Texture2D made))
                return made;

            made = source;

            try
            {
                Color[] pixels = new Color[source.Width * source.Height];
                source.GetData(pixels);

                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = turn(pixels[i]);

                made = new Texture2D(Main.instance.GraphicsDevice, source.Width, source.Height);
                made.SetData(pixels);
            }
            catch (Exception)
            {
                made = source;
            }

            kept[source] = made;
            return made;
        }

        /// <summary>A picture with what is red in it turned to its green (#bbff00), whiteness kept. Drawing only.</summary>
        public static Texture2D GreenOf(Texture2D source) => Made(green, source, c =>
        {
            // Red: more red than green or blue by a little (its eyes are all of that in its head's picture).
            if (c.A == 0 || c.R <= c.G + 8 || c.R <= c.B + 8)
                return c;

            int pale = Math.Min(c.G, c.B);
            int red = c.R - pale;
            return new Color(
                Math.Min(255, pale + red * Nuclear.R / 255),
                Math.Min(255, pale + red * Nuclear.G / 255),
                Math.Min(255, pale + red * Nuclear.B / 255), c.A);
        });

        /// <summary>A red glow in its green, each pixel's light kept, its palest to white. Drawing only.</summary>
        public static Texture2D GlowGreenOf(Texture2D red) => Made(glowGreen, red, c =>
        {
            if (c.A == 0 && c.R == 0 && c.G == 0 && c.B == 0)
                return c;

            float light = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f;
            float pale = Math.Min(c.G, c.B) / Math.Max(1f, c.R);
            Color to = Color.Lerp(Nuclear, Color.White, (float)Math.Pow(MathHelper.Clamp(pale, 0f, 1f), 1.4f)) * light;
            return new Color(to.R, to.G, to.B, c.A);
        });

        /// <summary>A picture's shape alone, white (its colour multiplied by its alpha already, as the batch wants). Drawing only.</summary>
        public static Texture2D WhiteOf(Texture2D source) => Made(white, source, c => new Color(c.A, c.A, c.A, c.A));

        /// <summary>A colour added on as light in an alpha-blended batch (no alpha: it adds).</summary>
        public static Color Lit(Color colour) => new(colour.R, colour.G, colour.B, (byte)0);
    }
}
