using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A flash of light that swells and fades, in added light: BloomGeneric's life on a picture
    /// made by formula (Content/Graphics/GlowSoft.png, (1 - r^2)^3), which falls to nothing at its
    /// edge, so however big it is drawn no rim of a disc shows. <see cref="PADParticle.Scale"/> is
    /// the picture's share: 1 is 256 pixels across, the bright middle about half of that.
    /// </summary>
    public class SoftGlow : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color born;
        private readonly bool lights;
        private float opacity;

        public SoftGlow(Vector2 position, Vector2 velocity, Color colour, float scale, int lifetime, bool lights = true)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Scale = scale;
            Lifetime = Math.Max(2, lifetime);
            this.lights = lights;
        }

        public override void Update()
        {
            // Up fast, down slow: a flash, not a lamp turned up and down.
            float t = LifetimeCompletion;
            opacity = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            opacity *= opacity;

            Color = born * opacity;
            Velocity *= 0.95f;

            if (lights)
                Lighting.AddLight(Position, Color.R / 255f * 0.6f, Color.G / 255f * 0.6f, Color.B / 255f * 0.6f);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null)
                return;

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, 0f, texture.Size() * 0.5f,
                Scale * (0.85f + 0.15f * opacity), SpriteEffects.None, 0f);
        }
    }
}
