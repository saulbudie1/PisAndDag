using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// The smoke a flame leaves as it burns out: a dark, heavy puff that carries on the
    /// way the flame was going for a moment, loses that, and rises, swelling and thinning
    /// as it goes. Drawn over what is behind it rather than added to it, so against a
    /// bright sky it shows as the soot it is and in the dark it is barely there.
    ///
    /// Its sheet is four puffs of 64 px, one above the other, stored premultiplied (white,
    /// as bright as it is thick), which is what the particles' alpha batch expects.
    /// </summary>
    public class FlameSmoke : PADParticle
    {
        public override bool SetLifetime => true;

        public override int FrameVariants => 4;

        /// <summary>The smoke's own colour at full thickness.</summary>
        private readonly Color tint;

        private readonly float thickness;
        private readonly float from;
        private readonly float to;
        private readonly float spin;
        private readonly float rise;

        /// <param name="position">Where it leaves the flame, in world pixels.</param>
        /// <param name="velocity">How it is thrown, per tick; it loses this quickly.</param>
        /// <param name="tint">Its colour at full thickness.</param>
        /// <param name="lifetime">Ticks it hangs.</param>
        /// <param name="from">Its width at the start, in pixels.</param>
        /// <param name="to">Its width at the end, in pixels.</param>
        /// <param name="thickness">How opaque it is at its thickest, 0 to 1.</param>
        public FlameSmoke(Vector2 position, Vector2 velocity, Color tint, int lifetime, float from, float to, float thickness)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;

            this.tint = tint;
            this.from = from;
            this.to = to;
            this.thickness = thickness;

            spin = Main.rand.NextFloat(0.006f, 0.014f) * (Main.rand.NextBool() ? 1f : -1f);
            rise = Main.rand.NextFloat(0.28f, 0.42f);

            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Variant = Main.rand.Next(FrameVariants);
            Scale = from / 64f;
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // The throw goes, and the heat lifts it.
            Velocity *= 0.96f;
            Position.Y -= rise;
            Rotation += spin;

            // Swells fast at first, then slowly.
            float grown = 1f - (1f - t) * (1f - t);
            Scale = MathHelper.Lerp(from, to, grown) / 64f;

            // In quickly, then a long thinning out.
            float alpha = thickness * Math.Min(1f, t / 0.12f) * (float)Math.Pow(1f - t, 1.6f);

            Color = tint * alpha;
        }
    }
}
