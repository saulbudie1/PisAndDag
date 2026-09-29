using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A ring going out across a surface of honey, seen nearly edge on: the roar's ring
    /// (Content/Combat/RoarRing, a soft white band) squashed flat, drawn additively in
    /// the colour given, opening fast and slowing as it goes, gone by the time it stops.
    /// What the Queen Bee leaves on the pool she comes out of and goes into.
    /// </summary>
    public class HoneyRipple : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Combat/RoarRing";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        /// <summary>Where the middle of the band sits, as a share of the texture's half-width (Cinematic.RingBand).</summary>
        private const float Band = 0.78f;

        /// <summary>How flat it is seen: a surface looked across, not down on.</summary>
        private const float Flatten = 0.26f;

        private readonly float from;
        private readonly float to;
        private readonly Color colour;

        /// <param name="from">Its radius when it starts, in world pixels.</param>
        /// <param name="to">Its radius when it is gone, in world pixels.</param>
        public HoneyRipple(Vector2 position, float from, float to, int lifetime, Color colour)
        {
            Position = position;
            Velocity = Vector2.Zero;
            Lifetime = Math.Max(2, lifetime);
            Color = colour;
            Scale = 1f;

            this.from = from;
            this.to = to;
            this.colour = colour;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null || texture.Width == 0)
                return;

            float t = LifetimeCompletion;
            float out_ = 1f - (1f - t) * (1f - t);
            float radius = MathHelper.Lerp(from, to, out_);
            float fade = (1f - t) * Math.Min(1f, Time / 3f);

            if (fade <= 0f || radius <= 0f)
                return;

            float scale = radius / (texture.Width * 0.5f * Band);

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, colour * fade, 0f, texture.Size() * 0.5f,
                new Vector2(scale, scale * Flatten), SpriteEffects.None, 0f);
        }
    }
}
