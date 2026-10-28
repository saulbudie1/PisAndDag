using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A grain of blown sand (Dancing Sand's spray, DancingSandGust): thrown fast, drawn as a short streak along
    /// the way it goes, the longer the faster; slowing as it flies and wavering side to side, and where it gives
    /// out, turning about in little loops and settling, as sand does in the air (the dance). Off the ground it
    /// bounces back weak and scattered, and drops. Lit where it is, never quite black; gone over its last third.
    /// The game's one white pixel (MagicPixel), stretched.
    /// </summary>
    public class SandGrain : PADParticle
    {
        public override string Texture => "Terraria/Images/MagicPixel";
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        private readonly Color tint;
        private readonly float phase;
        private readonly float swirl;
        private bool grounded;

        public SandGrain(Vector2 position, Vector2 velocity, Color tint, int lifetime, float size)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = Math.Max(2, lifetime);
            Scale = size;

            this.tint = tint;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            swirl = Main.rand.NextFloat(0.08f, 0.2f) * (Main.rand.NextBool() ? 1f : -1f);
        }

        public override void Update()
        {
            float speed = Velocity.Length();

            if (grounded)
            {
                Velocity.X *= 0.9f;
                Velocity.Y = Math.Min(Velocity.Y + 0.15f, 3f);

                // Lies where it lands, on the ground, not in it.
                if (Collision.SolidCollision(Position + Velocity - new Vector2(1f), 2, 2))
                    Velocity = Vector2.Zero;

                return;
            }

            // Slowing as it flies, wavering; slow enough, it turns about in loops and starts to settle.
            Velocity *= 0.965f;
            Velocity = Velocity.RotatedBy(Math.Sin(Time * 0.45f + phase) * 0.05f);

            if (speed < 5f)
            {
                Velocity = Velocity.RotatedBy(swirl * (1f - speed / 5f) * 2.5f);
                Velocity.Y += 0.04f;
            }

            // The ground: back off it, weak and scattered, and down.
            if (Collision.SolidCollision(Position - new Vector2(1f), 2, 2))
            {
                grounded = true;
                Velocity = -Velocity.RotatedByRandom(0.9f) * 0.15f;
            }
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D pixel = PADParticleHandler.GetTexture(Type);

            if (pixel == null)
                return;

            float t = LifetimeCompletion;
            float fade = t < 0.66f ? 1f : 1f - (t - 0.66f) / 0.34f;

            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            float lit = Math.Max(0.3f, Math.Max(light.R, Math.Max(light.G, light.B)) / 255f);
            Color colour = new Color((int)(tint.R * lit), (int)(tint.G * lit), (int)(tint.B * lit)) * fade;

            float speed = Velocity.Length();
            float length = MathHelper.Clamp(speed * 0.7f, Scale, 12f);

            spriteBatch.Draw(pixel, Position - Main.screenPosition, new Rectangle(0, 0, 1, 1), colour, Velocity.ToRotation(),
                new Vector2(0.5f, 0.5f), new Vector2(length, Scale), SpriteEffects.None, 0f);
        }
    }
}
