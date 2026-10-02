using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Steam let go under pressure: thrown hard the way it came out, which it loses in a
    /// few ticks, then lifting and spreading as it thins. Pale, and lit by the light where
    /// it hangs, but never darker than a floor of its own: steam shows in the dark beside
    /// the thing that is hot enough to make it.
    ///
    /// Drawn over what is behind it, in the handler's ordinary pass. It has no sheet of
    /// its own: it wears FlameSmoke's four puffs (64 px, stored premultiplied, which is
    /// what that pass wants) in its own colour.
    /// </summary>
    public class SteamPuff : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/FlameSmoke";

        public override bool SetLifetime => true;

        public override int FrameVariants => 4;

        /// <summary>How much of its own colour it keeps where there is no light.</summary>
        private const float Floor = 0.42f;

        private readonly Color tint;
        private readonly float thickness;
        private readonly float from;
        private readonly float to;
        private readonly float spin;
        private readonly float lift;

        /// <param name="position">Where it leaves, in world pixels.</param>
        /// <param name="velocity">How it is thrown, per tick. It keeps little of it.</param>
        /// <param name="tint">Its colour at full thickness.</param>
        /// <param name="lifetime">Ticks it hangs.</param>
        /// <param name="from">Its width at the start, in pixels.</param>
        /// <param name="to">Its width at the end, in pixels.</param>
        /// <param name="thickness">How opaque it is at its thickest, 0 to 1.</param>
        public SteamPuff(Vector2 position, Vector2 velocity, Color tint, int lifetime, float from, float to, float thickness)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;

            this.tint = tint;
            this.from = from;
            this.to = to;
            this.thickness = thickness;

            spin = Main.rand.NextFloat(0.008f, 0.02f) * (Main.rand.NextBool() ? 1f : -1f);
            lift = Main.rand.NextFloat(0.03f, 0.06f);

            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Variant = Main.rand.Next(FrameVariants);
            Scale = from / 64f;
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // The throw goes almost at once; what is left is hot air, and rises.
            Velocity *= 0.88f;
            Velocity.Y -= lift;
            Rotation += spin;

            // Opens fast, then slowly.
            float grown = 1f - (1f - t) * (1f - t);
            Scale = MathHelper.Lerp(from, to, grown) / 64f;

            // In at once, then a long thinning.
            float alpha = thickness * Math.Min(1f, t / 0.08f) * (float)Math.Pow(1f - t, 1.4f);

            Point at = Position.ToTileCoordinates();
            Vector3 light = WorldGen.InWorld(at.X, at.Y) ? Lighting.GetColor(at).ToVector3() : Vector3.One;

            light = Vector3.Max(light, new Vector3(Floor));

            Color = new Color(tint.ToVector3() * light) * alpha;
        }
    }
}
