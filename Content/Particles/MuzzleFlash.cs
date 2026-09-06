using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// The bright stab of light at the barrel. Lives about five ticks — long enough to
    /// register, short enough that rapid fire reads as a flicker rather than a glow.
    /// </summary>
    public class MuzzleFlash : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        private static readonly Color FlashColor = new Color(255, 226, 158);

        private float baseScale;

        public MuzzleFlash(Vector2 position, Vector2 direction, float scale)
        {
            Position = position;

            // Static: over five ticks the muzzle has not meaningfully moved, and a
            // flash that drifts looks like a projectile.
            Velocity = Vector2.Zero;

            Lifetime = 5;
            baseScale = scale;
            Scale = scale;

            // The sprite's long axis is horizontal, so it aligns with the shot without
            // a quarter-turn offset.
            Rotation = direction.ToRotation();

            Color = FlashColor;
        }

        public static void Spawn(Vector2 position, Vector2 direction)
        {
            PADParticleHandler.SpawnParticle(new MuzzleFlash(
                position,
                direction.SafeNormalize(Vector2.UnitX),
                Main.rand.NextFloat(0.75f, 1.1f)));
        }

        public override void Update()
        {
            float fade = 1f - LifetimeCompletion;

            Scale = baseScale * (0.35f + fade * 0.65f);
            Color = FlashColor * fade;

            Lighting.AddLight(Position, 0.9f * fade, 0.72f * fade, 0.34f * fade);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            // Wide dim pass for the bloom, tight bright pass for the core.
            spriteBatch.Draw(texture, drawPos, null, Color * 0.5f, Rotation, origin,
                Scale * 1.8f, SpriteEffects.None, 0f);

            spriteBatch.Draw(texture, drawPos, null, Color, Rotation, origin,
                Scale, SpriteEffects.None, 0f);
        }
    }
}
