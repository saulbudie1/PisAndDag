using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A star of light where something catches it: a cross of long rays, a shorter
    /// cross turned half a quarter, and a white middle, opening and closing over its
    /// life and turning a little as it does. The King's crown gets one as he settles
    /// into his size.
    ///
    /// Drawn with SparkShard's spindle, which is already the shape of one ray.
    /// </summary>
    public class CrownGlint : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/SparkShard";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        /// <summary>The long rays at their longest, in world pixels.</summary>
        private readonly float size;

        /// <param name="size">Length of the long rays at their longest, in world pixels.</param>
        public CrownGlint(Vector2 position, float size, int lifetime, Color colour)
        {
            Position = position;
            Velocity = Vector2.Zero;
            Lifetime = Math.Max(2, lifetime);
            Color = colour;
            Scale = 1f;
            Rotation = Main.rand.NextFloat(-0.15f, 0.15f);

            this.size = size;
        }

        public override void Update()
        {
            Rotation += 0.02f;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null || texture.Width == 0 || texture.Height == 0)
                return;

            // Sharp in and sharp out, over the top in the middle.
            float open = (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);
            open *= open;

            if (open <= 0.01f)
                return;

            Vector2 at = Position - Main.screenPosition;
            float length = size * open;

            // The spindle's canvas is mostly clear, so it is drawn wider than the light in it.
            float width = Math.Max(6f, size * 0.4f) * open;

            Ray(spriteBatch, texture, at, Color * open, Rotation, length, width);
            Ray(spriteBatch, texture, at, Color * open, Rotation + MathHelper.PiOver2, length * 0.8f, width);
            Ray(spriteBatch, texture, at, Color * (0.6f * open), Rotation + MathHelper.PiOver4, length * 0.45f, width * 0.7f);
            Ray(spriteBatch, texture, at, Color * (0.6f * open), Rotation - MathHelper.PiOver4, length * 0.45f, width * 0.7f);
            Ray(spriteBatch, texture, at, Color.White * open, Rotation, width * 0.9f, width * 0.9f);
        }

        /// <summary>One spindle, this long and this wide in world pixels, turned to this angle.</summary>
        private static void Ray(SpriteBatch spriteBatch, Texture2D texture, Vector2 at, Color colour, float rotation, float length, float width)
        {
            Vector2 scale = new Vector2(width / texture.Width, length / texture.Height);

            spriteBatch.Draw(texture, at, null, colour, rotation, texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }
}
