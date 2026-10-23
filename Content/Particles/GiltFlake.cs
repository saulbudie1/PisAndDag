using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A flake of gold leaf come away from the Parade Sabre. It is thrown off, then falls slowly,
    /// swaying, turning over as leaf does: face on it is a flake and catches the light, edge on it is
    /// a hair of gold (GiltFlake.png, three frames of 4 by 4: face, edge, face turned). Lit where it
    /// is, never quite dark; at the moment it turns full face a glint of added light.
    /// </summary>
    public class GiltFlake : PADParticle
    {
        public static readonly Color Gold = new Color(226, 172, 72);
        public static readonly Color Bright = new Color(255, 236, 176);

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override int FrameVariants => 3;

        private const float Weight = 0.05f;
        private const float MostFall = 0.9f;

        private readonly float phase;
        private readonly float turnSpeed;
        private readonly float swaySpeed;
        private readonly float spin;

        /// <summary>How far round it has turned, -1 to 1: 0 is edge on, either end face on.</summary>
        private float turn;

        public GiltFlake(Vector2 position, Vector2 velocity, int lifetime)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = Math.Max(2, lifetime);
            Scale = Main.rand.NextFloat(0.8f, 1.2f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            turnSpeed = Main.rand.NextFloat(0.12f, 0.22f);
            swaySpeed = Main.rand.NextFloat(0.06f, 0.11f);
            spin = Main.rand.NextFloat(-0.08f, 0.08f);
        }

        public override void Update()
        {
            Velocity.Y = Math.Min(Velocity.Y + Weight, MostFall);
            Velocity.X *= 0.95f;
            Position.X += (float)Math.Sin(Time * swaySpeed + phase) * 0.35f;
            Rotation += spin;

            turn = (float)Math.Sin(Time * turnSpeed + phase);
            Variant = Math.Abs(turn) < 0.3f ? 1 : turn > 0f ? 0 : 2;

            // Resting on the ground it lies flat and stops.
            if (Collision.SolidCollision(Position - new Vector2(1f), 2, 2))
                Velocity = Vector2.Zero;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null)
                return;

            float t = LifetimeCompletion;
            float fade = t < 0.7f ? Math.Min(1f, Time / 4f) : 1f - (t - 0.7f) / 0.3f;
            float face = Math.Abs(turn);

            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            float lit = Math.Max(0.4f, Math.Max(light.R, Math.Max(light.G, light.B)) / 255f);
            Color gold = Color.Lerp(Gold, Bright, face * face);
            Color colour = new Color((int)(gold.R * lit), (int)(gold.G * lit), (int)(gold.B * lit)) * fade;

            Rectangle frame = texture.Frame(1, FrameVariants, 0, Variant);
            Vector2 at = (Position - Main.screenPosition).Floor();

            spriteBatch.Draw(texture, at, frame, colour, Rotation, frame.Size() * 0.5f, Scale, SpriteEffects.None, 0f);

            // Turned full face to the eye: it catches the light.
            if (face > 0.94f)
            {
                Color glint = Bright * (fade * (face - 0.94f) / 0.06f * 0.9f);
                glint.A = 0;
                spriteBatch.Draw(texture, at, frame, glint, Rotation, frame.Size() * 0.5f, Scale * 1.4f, SpriteEffects.None, 0f);
            }
        }
    }
}
