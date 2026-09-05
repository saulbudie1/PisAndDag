using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A falling droplet that stretches into a streak as it picks up speed, then fades
    /// away late rather than evenly. Replaces the hand-rolled DustID.Blood loops.
    /// </summary>
    public class BloodDroplet : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        private Color initialColor;
        private float spin;

        /// <summary>
        /// False makes the droplet hang in the air and coast to a stop instead of falling.
        /// Used by the charge aura, where blood should drift outward rather than rain down.
        /// </summary>
        public bool AffectedByGravity = true;

        public BloodDroplet(Vector2 position, Vector2 velocity, int lifetime, float scale, Color color,
            bool affectedByGravity = true)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = scale;
            initialColor = color;
            Color = color;
            AffectedByGravity = affectedByGravity;
            spin = Main.rand.NextFloat(-0.04f, 0.04f);
        }

        /// <summary>Convenience spawn: a cone of droplets, as used on hit.</summary>
        public static void SpawnBurst(Vector2 position, Vector2 direction, int count, float spreadDegrees = 22f)
        {
            Vector2 baseDir = direction.SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < count; i++)
            {
                float offset = MathHelper.ToRadians(Main.rand.NextFloat(-spreadDegrees, spreadDegrees));

                // Droplets near the centre of the cone travel fastest, so the spray
                // reads as a jet rather than an even fan.
                float centreness = 1f - Math.Abs(offset) / MathHelper.ToRadians(spreadDegrees);
                float speed = MathHelper.Lerp(1.5f, 7f, centreness) * Main.rand.NextFloat(0.7f, 1.2f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    position,
                    baseDir.RotatedBy(offset) * speed,
                    Main.rand.Next(28, 46),
                    Main.rand.NextFloat(0.5f, 0.95f),
                    new Color(150, 8, 12)));
            }
        }

        public override void Update()
        {
            if (AffectedByGravity)
            {
                // Air drag sideways, gravity down, terminal velocity so streaks stay sane.
                Velocity.X *= 0.965f;
                Velocity.Y = MathHelper.Clamp(Velocity.Y + 0.42f, -18f, 18f);
            }
            else
            {
                // Weightless: coasts outward along its spawn direction and settles.
                Velocity *= 0.94f;
            }

            Scale *= 0.985f;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2 + spin * Time;

            // Cubed completion holds the droplet visible for most of its life, then
            // drops it quickly. A linear fade makes blood look like fog.
            float fade = LifetimeCompletion * LifetimeCompletion * LifetimeCompletion;
            Color = Color.Lerp(initialColor, Color.Transparent, fade);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            // Stretch along travel direction once moving fast — the thing Dust cannot do.
            float speed = Velocity.Length();
            float stretch = MathHelper.Clamp(speed / 14f, 0f, 1f) * 1.6f;
            Vector2 drawScale = new Vector2(1f - stretch * 0.25f, 1f + stretch) * Scale;

            // Sample the light where the droplet is, so blood in a dark cave stays dark.
            float brightness = (float)Math.Pow(
                Lighting.Brightness((int)(Position.X / 16f), (int)(Position.Y / 16f)), 0.4);
            brightness = MathHelper.Clamp(brightness, 0.25f, 1f);

            // Preloaded by the handler at PostSetupContent — a dictionary lookup, not an
            // asset request, and it can't hand back an empty path the way Texture could.
            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            spriteBatch.Draw(texture, Position - Main.screenPosition, null,
                Color * brightness, Rotation, texture.Size() * 0.5f,
                drawScale, SpriteEffects.None, 0f);
        }
    }
}
