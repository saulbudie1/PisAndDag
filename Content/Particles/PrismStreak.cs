using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Empress;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A streak of light left in the air where something passed, soft across, in one of the Empress's colours, thinning
    /// and fading where it hangs. Laid one a tick behind a dash, they make a ribbon of her colours (the Empress'
    /// Blessing's dash, AccessoryDash).
    /// </summary>
    public class PrismStreak : PADParticle
    {
        /// <summary>Never drawn as a sprite: its draw is EmpressArt's line. A picture the handler can load for it.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color born;
        private readonly float length;
        private readonly float width;

        /// <param name="angle">The way it lies.</param>
        public PrismStreak(Vector2 at, Vector2 velocity, float angle, float length, float width, Color colour, int lifetime)
        {
            Position = at;
            Velocity = velocity;
            Rotation = angle;
            Color = born = colour;
            Lifetime = Math.Max(2, lifetime);
            this.length = length;
            this.width = width;
        }

        public override void Update()
        {
            Velocity *= 0.9f;
            float t = LifetimeCompletion;
            Color = born * (1f - t) * (1f - t);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            float t = LifetimeCompletion;
            Vector2 half = Rotation.ToRotationVector2() * length * (0.5f + 0.25f * t);

            EmpressArt.DrawLine(spriteBatch, Position - half, Position + half, width * (1f - 0.6f * t), Color);
        }
    }
}
