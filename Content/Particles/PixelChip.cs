using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A square of light or of matter, a few world pixels across, set on the 2x2 grid the game's pictures are drawn on:
    /// the scatter of a machine (Made By Tentochi), a fleck of neon or of hot metal. Slows, may fall, and in its last
    /// third blinks out unevenly, as a dying pixel does, rather than fading smooth.
    /// </summary>
    public class PixelChip : PADParticle
    {
        /// <summary>Never drawn: its own draw is a square of the game's white pixel. A picture the handler can load for it.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => additive;

        private readonly Color born;
        private readonly float size;
        private readonly float gravity;
        private readonly float drag;
        private readonly bool additive;
        private bool shown = true;

        public PixelChip(Vector2 position, Vector2 velocity, Color colour, float size, int lifetime, float gravity = 0f, bool additive = true, float drag = 0.92f)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Lifetime = Math.Max(2, lifetime);
            this.size = Math.Max(2f, size);
            this.gravity = gravity;
            this.drag = drag;
            this.additive = additive;
        }

        public override void Update()
        {
            Velocity *= drag;
            Velocity.Y += gravity;

            float t = LifetimeCompletion;
            Color = t < 0.6f ? born : born * (1f - (t - 0.6f) / 0.4f);

            // Its last third: on and off, unevenly.
            shown = t < 0.66f || Main.rand.NextFloat() < 0.7f - (t - 0.66f);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            if (!shown)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // On the world's grid: a picture's pixel is two of the world's.
            Vector2 at = new Vector2((float)Math.Floor(Position.X / 2f) * 2f, (float)Math.Floor(Position.Y / 2f) * 2f) - Main.screenPosition;

            float side = (float)Math.Round(size / 2f) * 2f;
            spriteBatch.Draw(pixel, at - new Vector2(side * 0.5f), new Rectangle(0, 0, 1, 1), Color, 0f, Vector2.Zero, side, SpriteEffects.None, 0f);
        }
    }
}
