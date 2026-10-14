using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A spore of blue light, shed by Mycellaneous's spore as it flies home (MycellaneousSpore) and
    /// thrown off where it leaves and where it lands. Ember's soft dot, but it keeps its blue: it comes
    /// up over a few ticks, hangs about where it was left, rises a little as the caves' spores do,
    /// flickers and goes out. Additive, so a run of them reads as a trail of light.
    /// </summary>
    public class SporeMote : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/Ember";

        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color born;

        /// <summary>Its own flicker, so a trail of them does not pulse in step.</summary>
        private readonly float phase;

        public SporeMote(Vector2 position, Vector2 velocity, Color colour, float scale, int lifetime)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Scale = scale;
            Lifetime = Math.Max(2, lifetime);

            phase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity *= 0.9f;
            Velocity.Y -= 0.012f;
            Scale *= 0.985f;

            float flicker = 0.75f + 0.25f * (float)Math.Sin(Time * 0.8f + phase);
            float up = Math.Min(1f, Time / 3f);

            Color = born * ((1f - t * t) * flicker * up);
        }
    }
}
