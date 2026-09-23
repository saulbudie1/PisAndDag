using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A line of wind: a long thin smear of pale air crossing the shot, for the moments
    /// the wind has to be seen and not only felt in the trees. EyeFinale throws them
    /// across the screen around Pugream while the gale is on.
    ///
    /// Drawn along its own velocity, so a gust that lifts it tilts it. It comes in and
    /// goes out on a sine over its life, never at full strength for more than an
    /// instant, and it wavers a little in height as it goes. Faint on purpose: one is
    /// nothing, and the effect is the many.
    /// </summary>
    public class WindStreak : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        /// <summary>Pale, a little cold.</summary>
        private static readonly Color Air = new Color(222, 230, 242);

        /// <summary>At its strongest, this much of it shows.</summary>
        private const float Strength = 0.28f;

        private readonly float length;
        private readonly float thickness;
        private readonly float phase;

        /// <param name="length">Drawn length, in world pixels.</param>
        /// <param name="thickness">Drawn thickness, in world pixels. The texture's soft edge is most of it.</param>
        public WindStreak(Vector2 position, Vector2 velocity, float length, float thickness, int lifetime)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = Math.Max(2, lifetime);
            Color = Air;
            Scale = 1f;

            this.length = length;
            this.thickness = thickness;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update()
        {
            Velocity.Y += (float)Math.Sin(Time * 0.15f + phase) * 0.05f;
            Rotation = Velocity.ToRotation();
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null || texture.Width == 0 || texture.Height == 0)
                return;

            float opacity = (float)Math.Sin(MathHelper.Pi * LifetimeCompletion) * Strength;
            Vector2 scale = new Vector2(length / texture.Width, thickness / texture.Height);

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Air * opacity, Rotation, texture.Size() * 0.5f,
                scale, SpriteEffects.None, 0f);
        }
    }
}
