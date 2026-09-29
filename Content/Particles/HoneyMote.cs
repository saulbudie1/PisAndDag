using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A fleck of golden light off the Queen Bee: pollen shaken off her wings, the shine
    /// thrown up out of the honey, the flecks that climb her while she is honeyed.
    ///
    /// Ember's soft dot, but it keeps its gold as it goes rather than cooling to a coal:
    /// it comes up over a few ticks, flickers, and fades late, drifting the way it was
    /// told to (up for the climb, a little down for pollen). Additive, so a cluster of
    /// them builds a glow without any one of them being bright.
    /// </summary>
    public class HoneyMote : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/Ember";

        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color born;

        /// <summary>Added to its upward speed each tick, in pixels. Negative sinks it.</summary>
        private readonly float lift;

        /// <summary>Its own flicker, so a cloud of them does not pulse in step.</summary>
        private readonly float phase;

        public HoneyMote(Vector2 position, Vector2 velocity, Color colour, float scale, int lifetime, float lift = 0.03f)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Scale = scale;
            Lifetime = Math.Max(2, lifetime);

            this.lift = lift;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity.X *= 0.96f;
            Velocity.Y = Velocity.Y * 0.96f - lift;
            Scale *= 0.99f;

            float flicker = 0.8f + 0.2f * (float)Math.Sin(Time * 0.7f + phase);
            float up = Math.Min(1f, Time / 4f);

            Color = born * ((1f - t * t * t) * flicker * up);
        }
    }
}
