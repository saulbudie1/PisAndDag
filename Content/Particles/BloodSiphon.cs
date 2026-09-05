using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Blood torn out of a struck enemy and drawn into the wielder. It stores an offset
    /// into the player's hitbox rather than a fixed world point, so it keeps tracking
    /// while the player moves, and it accelerates on the way in so the arrival snaps.
    /// </summary>
    public class BloodSiphon : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        // Shares the droplet sheet — no second texture needed.
        public override string Texture => "PisAndDag/Content/Particles/BloodDroplet";

        private readonly int playerIndex;
        private readonly Vector2 hitboxOffset;
        private Color initialColor;

        /// <param name="hitboxOffset">Where inside the player's box to land, measured
        /// from player.position so it stays correct as the player moves.</param>
        public BloodSiphon(Vector2 position, Player destination, Vector2 hitboxOffset,
            Vector2 velocity, int lifetime, float scale, Color color)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = scale;
            initialColor = color;
            Color = color;
            playerIndex = destination.whoAmI;
            this.hitboxOffset = hitboxOffset;
        }

        /// <summary>Tears a spray out of <paramref name="source"/> and pulls it into the player.</summary>
        public static void SpawnDrain(NPC source, Player destination, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                // Leave the enemy from somewhere on its body, not one point.
                Vector2 start = source.Center
                    + Main.rand.NextVector2Circular(source.width * 0.35f, source.height * 0.35f);

                // Land anywhere inside the player's hitbox.
                Vector2 landing = new Vector2(
                    Main.rand.NextFloat(destination.width),
                    Main.rand.NextFloat(destination.height));

                // Fling outward first, roughly toward the player but scattered, so the
                // spray blooms before it gathers. The homing in Update does the rest.
                Vector2 launch = (destination.Center - source.Center)
                    .SafeNormalize(Vector2.UnitX)
                    .RotatedByRandom(0.85)
                    * Main.rand.NextFloat(2f, 5.5f);

                PADParticleHandler.SpawnParticle(new BloodSiphon(
                    start, destination, landing, launch,
                    Main.rand.Next(26, 40),
                    Main.rand.NextFloat(0.5f, 0.9f),
                    color));
            }
        }

        public override void Update()
        {
            Player player = Main.player[playerIndex];

            if (!player.active || player.dead)
            {
                Kill();
                return;
            }

            Vector2 target = player.position + hitboxOffset;
            Vector2 toTarget = target - Position;

            // Close enough to have landed.
            if (toTarget.Length() < 8f)
            {
                Kill();
                return;
            }

            // Slow drift at first, whipping in by the end — an even speed reads as a
            // projectile rather than something being pulled.
            float speed = MathHelper.Lerp(2.5f, 16f, LifetimeCompletion);
            Velocity = Vector2.Lerp(Velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.22f);

            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            // Holds its colour almost the whole way, then goes on the last few ticks.
            float fade = LifetimeCompletion * LifetimeCompletion;
            fade *= fade;
            Color = Color.Lerp(initialColor, Color.Transparent, fade);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            // Stretched along travel, harder than the falling droplet — these are being
            // yanked, so they should read as threads rather than beads.
            float stretch = MathHelper.Clamp(Velocity.Length() / 12f, 0f, 1f) * 2.2f;
            Vector2 drawScale = new Vector2(1f - stretch * 0.3f, 1f + stretch) * Scale;

            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            spriteBatch.Draw(texture, Position - Main.screenPosition, null,
                Color, Rotation, texture.Size() * 0.5f,
                drawScale, SpriteEffects.None, 0f);
        }
    }
}
