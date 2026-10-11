using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Black smoke with red in it: what comes off a blade that burns black. A puff of
    /// soot, thick enough to be seen against a lit wall, over a wider and fainter one of
    /// red light, so that against the dark, where soot alone is nothing, it is still a
    /// black shape with a red edge.
    ///
    /// It wears FlameSmoke's four puffs (stored already multiplied by their own alpha,
    /// as this pass blends) and goes as FlameSmoke goes: thrown, slowed, lifted, swelling
    /// fast and then slowly, in quickly and a long thinning out.
    /// </summary>
    public class BurnSmoke : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/FlameSmoke";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        public override int FrameVariants => 4;

        /// <summary>The red under the soot: this much of the soot's own strength, and this much wider.</summary>
        private const float RimShare = 0.5f;
        private const float RimWider = 1.25f;

        private readonly Color soot;
        private readonly Color rim;

        private readonly float thickness;
        private readonly float from;
        private readonly float to;
        private readonly float spin;
        private readonly float rise;

        /// <summary>How much of it there is this tick, 0 to 1.</summary>
        private float shown;

        /// <param name="soot">The black of it.</param>
        /// <param name="rim">The red in it.</param>
        /// <param name="from">How wide it is born, in pixels.</param>
        /// <param name="to">How wide it has swollen to by the end.</param>
        /// <param name="thickness">How thick the soot is at its thickest, 0 to 1.</param>
        public BurnSmoke(Vector2 position, Vector2 velocity, Color soot, Color rim, int lifetime, float from, float to, float thickness)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;

            this.soot = soot;
            this.rim = rim;
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
            shown = MathHelper.Clamp(thickness * Math.Min(1f, t / 0.12f) * (float)Math.Pow(1f - t, 1.6f), 0f, 1f);

            Color = new Color(soot.R, soot.G, soot.B, 255) * shown;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null || shown <= 0f)
                return;

            Rectangle frame = texture.Frame(1, FrameVariants, 0, Variant);
            Vector2 at = Position - Main.screenPosition;
            Vector2 origin = frame.Size() * 0.5f;

            // The red in it: light (no alpha of its own: it covers nothing), a little wider
            // than the soot, and under it.
            spriteBatch.Draw(texture, at, frame, new Color(rim.R, rim.G, rim.B, 0) * (RimShare * shown), Rotation, origin,
                Scale * RimWider, SpriteEffects.None, 0f);

            // The soot, over what is behind it.
            spriteBatch.Draw(texture, at, frame, Color, Rotation, origin, Scale, SpriteEffects.None, 0f);
        }
    }
}
