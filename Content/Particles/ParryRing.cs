using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A ring of light thrown out from where steel met steel: the roar's ring
    /// (Content/Combat/RoarRing, a soft white band, torn here and there), drawn additively in
    /// the colour given, round, opening fast and slowing as it goes, thinning to nothing by
    /// the time it stops. Drawn twice, a third of a turn apart, so one copy's tears are filled
    /// by the other's band, and turned to a different angle every time. What a parry leaves
    /// in the air, and bigger, what a riposte does.
    ///
    /// Measured in world pixels, like SparkShard: a radius it starts at and one it ends at.
    /// </summary>
    public class ParryRing : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Combat/RoarRing";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        /// <summary>Where the middle of the band sits, as a share of the texture's half-width (Cinematic.RingBand, HoneyRipple's).</summary>
        private const float Band = 0.78f;

        private readonly float from;
        private readonly float to;
        private readonly Color colour;
        private readonly Entity follow;
        private readonly Vector2 offset;

        /// <param name="from">Its radius when it starts, in world pixels.</param>
        /// <param name="to">Its radius when it is gone, in world pixels.</param>
        /// <param name="follow">If given, it stays where it was on this thing while the thing moves.</param>
        public ParryRing(Vector2 position, float from, float to, int lifetime, Color colour, Entity follow = null)
        {
            Position = position;
            Velocity = Vector2.Zero;
            Lifetime = Math.Max(2, lifetime);
            Color = colour;
            Scale = 1f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            this.from = from;
            this.to = to;
            this.colour = colour;
            this.follow = follow;
            offset = follow != null ? position - follow.Center : Vector2.Zero;
        }

        public override void Update()
        {
            if (follow != null && follow.active)
                Position = follow.Center + offset;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null || texture.Width == 0)
                return;

            float t = LifetimeCompletion;

            // Out fast, slowing: most of the way there in the first third of its life.
            float gone = 1f - (1f - t) * (1f - t) * (1f - t);
            float radius = MathHelper.Lerp(from, to, gone);

            // In over two ticks, then away on a square: bright while it is small.
            float fade = (1f - t) * (1f - t) * Math.Min(1f, Time / 2f);

            if (fade <= 0f || radius <= 0f)
                return;

            float scale = radius / (texture.Width * 0.5f * Band);

            // Twice, a third of a turn apart: the band is torn, and one copy's tears are the other's band.
            Color light = colour * (fade * 0.75f);

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, light, Rotation, texture.Size() * 0.5f,
                scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, light, Rotation + MathHelper.TwoPi / 3f, texture.Size() * 0.5f,
                scale, SpriteEffects.None, 0f);
        }
    }
}
