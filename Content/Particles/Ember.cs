using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A fleck of fire. Rises a little as it goes, slows, shrinks, and cools from the
    /// colour it was born with through orange to a dull red before it is gone, the way
    /// a real ember does. Additive, so a dozen overlapping build a hot centre without
    /// any of them being bright on their own.
    ///
    /// Untextured by anything but a soft round falloff; everything about how it reads
    /// is in the colour ramp and the motion.
    /// </summary>
    public class Ember : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;

        /// <summary>The colour it starts as. The ramp below runs from here.</summary>
        private readonly Color born;

        /// <summary>How much it rises each tick, in pixels. Hot air.</summary>
        private readonly float lift;

        /// <summary>Per-ember flicker phase, so a cloud of them does not pulse in step.</summary>
        private readonly float phase;

        public Ember(Vector2 position, Vector2 velocity, Color colour, float scale, int lifetime, float lift = 0.04f)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Scale = scale;
            Lifetime = lifetime;
            this.lift = lift;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        /// <summary>Where the ramp ends: the dull red of a coal about to go out.</summary>
        private static readonly Color Cooled = new Color(120, 18, 6);

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity.X *= 0.95f;
            Velocity.Y = Velocity.Y * 0.95f - lift;

            // Shrinks on a curve that keeps it whole for most of its life, then takes it.
            Scale *= 0.975f;

            // Flicker: a small wobble on top of the fade, offset per ember.
            float flicker = 0.85f + 0.15f * (float)System.Math.Sin(Time * 0.9f + phase);

            // Cools toward the coal colour, late rather than evenly, and the alpha goes
            // on a cube so it holds its colour and then leaves rather than dimming evenly.
            Color cooled = Color.Lerp(born, Cooled, t * t);
            float alpha = (1f - t * t * t) * flicker;

            Color = cooled * alpha;
        }
    }
}
