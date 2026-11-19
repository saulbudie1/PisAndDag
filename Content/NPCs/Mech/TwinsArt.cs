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
    /// The Twins' pictures made or kept in code: their round shield (TwinsShield.png, made in code: scratchpad
    /// twins/art/icons.py; white, its strength in its alpha, for the half-size canvas), and any picture turned to fire
    /// (each pixel's light kept and run through fire's colours: dark to ember red, then orange, yellow, white), for the
    /// Retinazer rolled into a fireball (by request: "modify texture to make it look bright"). Client only.
    /// </summary>
    internal static class TwinsArt
    {
        /// <summary>Their elements, bright: what their light is drawn in (#c2380e and #3ea637 are their dark ends).</summary>
        public static readonly Color FireBright = new(255, 122, 44);
        public static readonly Color ToxicBright = new(120, 240, 92);

        private static Asset<Texture2D> shield;
        private static readonly Dictionary<Texture2D, Texture2D> fired = new();

        public static Texture2D Shield => (shield ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Mech/TwinsShield")).Value;

        public static Color Bright(NPC npc) => Twins.IsRet(npc) ? FireBright : ToxicBright;

        public static void Unload()
        {
            List<Texture2D> made = new(fired.Values);
            fired.Clear();
            shield = null;

            Main.QueueMainThreadAction(() =>
            {
                foreach (Texture2D t in made)
                    t?.Dispose();
            });
        }

        /// <summary>A picture turned to fire, kept once made; null if it cannot be read (then it is only lit over). Drawing only.</summary>
        public static Texture2D FireOf(Texture2D source)
        {
            if (fired.TryGetValue(source, out Texture2D made))
                return made;

            made = null;

            try
            {
                Color[] pixels = new Color[source.Width * source.Height];
                source.GetData(pixels);

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];

                    if (c.A == 0)
                        continue;

                    float light = (0.45f * c.R + 0.4f * c.G + 0.15f * c.B) / 255f;
                    Color to = light < 0.3f ? Color.Lerp(new Color(96, 16, 8), new Color(214, 58, 14), light / 0.3f)
                        : light < 0.6f ? Color.Lerp(new Color(214, 58, 14), new Color(255, 150, 40), (light - 0.3f) / 0.3f)
                        : Color.Lerp(new Color(255, 150, 40), new Color(255, 246, 200), Math.Min(1f, (light - 0.6f) / 0.32f));

                    float a = c.A / 255f;
                    pixels[i] = new Color((int)(to.R * a), (int)(to.G * a), (int)(to.B * a), c.A);
                }

                made = new Texture2D(Main.instance.GraphicsDevice, source.Width, source.Height);
                made.SetData(pixels);
            }
            catch (Exception)
            {
                made = null;
            }

            fired[source] = made;
            return made;
        }
    }
}
