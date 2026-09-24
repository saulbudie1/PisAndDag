using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Fine dust hanging in the air: a soft, grainy puff that lifts slowly, sways, swells
    /// and thins away. Any colour; lit by the light where it hangs, so it is never brighter
    /// than the place it is in. Thrown up in numbers it reads as a fog of dust rolling off
    /// the ground. Drawn by the handler's default draw.
    /// </summary>
    public class DustFog : PADParticle
    {
        public override bool SetLifetime => true;

        public override int FrameVariants => 3;

        /// <summary>The dust's own colour, before the light and the fade.</summary>
        private readonly Color tint;

        /// <summary>How thick it is at its thickest, 0 to 1.</summary>
        private readonly float thickness;

        private readonly float swell;
        private readonly float sway;
        private readonly float phase;
        private readonly float spin;

        /// <param name="position">Where it starts, in world pixels.</param>
        /// <param name="velocity">How it is thrown; it slows quickly and then only drifts.</param>
        /// <param name="color">The dust's colour.</param>
        /// <param name="lifetime">Ticks it hangs.</param>
        /// <param name="scale">Its size at the start; it grows as it thins.</param>
        /// <param name="thickness">How opaque it is at its thickest.</param>
        public DustFog(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale, float thickness = 0.32f)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = scale;

            tint = color;
            this.thickness = thickness;
            swell = scale * Main.rand.NextFloat(0.8f, 1.4f) / Math.Max(1, lifetime);
            sway = Main.rand.NextFloat(0.004f, 0.012f);
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.012f, 0.012f);

            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Variant = Main.rand.Next(FrameVariants);
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // Thrown, it loses its push fast and then hangs, rising a little and swaying.
            Velocity *= 0.93f;
            Velocity.Y -= 0.01f;
            Velocity.X += (float)Math.Sin(Time * 0.045f + phase) * sway;

            Scale += swell;
            Rotation += spin;

            // In quickly, then a long thinning out.
            float fade = Math.Min(1f, t / 0.1f) * (1f - t) * (1f - t);

            Point at = Position.ToTileCoordinates();
            Color light = WorldGen.InWorld(at.X, at.Y) ? Lighting.GetColor(at) : Color.White;

            Color = tint.MultiplyRGB(light) * (thickness * fade);
        }
    }
}
