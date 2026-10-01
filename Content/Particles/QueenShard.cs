using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A splinter of the Queen Slime's wings, when they break: thrown off, it tumbles, falls,
    /// glances off the ground once or twice and fades. Six kinds, one to a 16 x 16 frame
    /// (QueenShard.png), lavender-white with an iridescent end; lit by the world like anything
    /// solid, never quite black, and now and then catching the light.
    /// </summary>
    public class QueenShard : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override int FrameVariants => 6;

        private float spin;
        private int bounces;

        /// <summary>A little of the light it catches, as it turns: 0 to 1.</summary>
        private float glint;

        public QueenShard(Vector2 position, Vector2 velocity, int lifetime, float scale = 1f)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = Math.Max(2, lifetime);
            Scale = scale;
            Variant = Main.rand.Next(6);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.32f, 0.32f);
            Color = Color.White;
        }

        public override void Update()
        {
            Velocity.X *= 0.985f;
            Velocity.Y = Math.Min(Velocity.Y + 0.21f, 9f);
            Rotation += spin;

            // Off the ground: a glance, weaker each time, and then it lies there.
            if (Velocity.Y > 0f && bounces < 3)
            {
                Tile tile = Framing.GetTileSafely((int)(Position.X / 16f), (int)((Position.Y + 4f) / 16f));

                if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                {
                    bounces++;
                    Velocity.Y *= -0.32f;
                    Velocity.X *= 0.6f;
                    spin *= 0.5f;

                    if (bounces == 3)
                        Velocity = Vector2.Zero;
                }
            }

            glint = Math.Max(0f, (float)Math.Sin(Rotation * 2f + Time * 0.05f) - 0.8f) * 5f;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null)
                return;

            Rectangle frame = texture.Frame(1, FrameVariants, 0, Variant);
            float fade = 1f - Math.Max(0f, (LifetimeCompletion - 0.7f) / 0.3f);

            if (fade <= 0f)
                return;

            float light = (float)Math.Sqrt(Lighting.Brightness((int)(Position.X / 16f), (int)(Position.Y / 16f)));
            light = MathHelper.Clamp(light, 0.5f, 1f);

            Vector2 at = Position - Main.screenPosition;

            spriteBatch.Draw(texture, at, frame, Color.White * (light * fade), Rotation, frame.Size() * 0.5f, Scale, SpriteEffects.None, 0f);

            // The light it catches, added: a white with no alpha adds under this batch's blend.
            if (glint > 0f)
            {
                Color add = new Color(255, 240, 255, 0) * (glint * 0.6f * fade);
                spriteBatch.Draw(texture, at, frame, add, Rotation, frame.Size() * 0.5f, Scale * 1.1f, SpriteEffects.None, 0f);
            }
        }
    }
}
