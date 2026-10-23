using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A chip of stone knocked off something: it flies, turns over, falls, bounces on the ground
    /// twice at the most and lies there until it goes. Lit where it is (never quite black), one of
    /// three shapes (StoneChip.png, three frames of 4 by 4 one under another), in the colour it is
    /// given. The Defacer throws them.
    /// </summary>
    public class StoneChip : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override int FrameVariants => 3;

        private const float Weight = 0.24f;
        private const float MostFall = 9f;

        private readonly Color tint;
        private float spin;
        private int bounces;
        private bool resting;

        public StoneChip(Vector2 position, Vector2 velocity, Color tint, int lifetime, float scale = 1f)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = Math.Max(2, lifetime);
            Scale = scale;
            Variant = Main.rand.Next(3);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            this.tint = tint;
            spin = Main.rand.NextFloat(-0.3f, 0.3f);
        }

        public override void Update()
        {
            if (resting)
            {
                Velocity = Vector2.Zero;
                return;
            }

            Velocity.Y = Math.Min(Velocity.Y + Weight, MostFall);
            Velocity.X *= 0.99f;
            Rotation += spin;

            // The ground: off it, a little, twice; then it lies there.
            if (Velocity.Y > 0f && Collision.SolidCollision(Position + Velocity - new Vector2(1.5f), 3, 3))
            {
                if (bounces < 2 && Velocity.Y > 1.2f)
                {
                    bounces++;
                    Velocity.Y *= -0.35f;
                    Velocity.X *= 0.6f;
                    spin *= 0.5f;
                }
                else
                {
                    resting = true;
                    Velocity = Vector2.Zero;
                }
            }
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null)
                return;

            float t = LifetimeCompletion;
            float fade = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;

            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            float lit = Math.Max(0.25f, Math.Max(light.R, Math.Max(light.G, light.B)) / 255f);
            Color colour = new Color((int)(tint.R * lit), (int)(tint.G * lit), (int)(tint.B * lit)) * fade;

            Rectangle frame = texture.Frame(1, FrameVariants, 0, Variant);
            spriteBatch.Draw(texture, (Position - Main.screenPosition).Floor(), frame, colour, Rotation, frame.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
        }
    }
}
