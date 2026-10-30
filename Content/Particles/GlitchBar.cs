using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A bar of neon laid flat across the air, as a picture tears where a signal drops out: a run of the world's pixels
    /// in one colour, a few tall and many wide, that jumps sideways every few ticks, shows and hides unevenly, and goes.
    /// What a Tentochi machine leaves behind it when it moves faster than it is seen (Made By Tentochi). On the 2x2 grid.
    /// </summary>
    public class GlitchBar : PADParticle
    {
        /// <summary>Never drawn: its own draw is the game's white pixel. A picture the handler can load for it.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color born;
        private readonly float width;
        private readonly float height;
        private readonly float jump;
        private Vector2 offset;
        private bool shown = true;

        /// <param name="jump">How far it may jump sideways, either way, in world pixels.</param>
        public GlitchBar(Vector2 position, Vector2 velocity, Color colour, float width, float height, int lifetime, float jump = 6f)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Lifetime = Math.Max(2, lifetime);
            this.width = Math.Max(2f, width);
            this.height = Math.Max(2f, height);
            this.jump = jump;
        }

        public override void Update()
        {
            Velocity *= 0.86f;

            if (Time % 3 == 0)
                offset.X = Main.rand.NextFloat(-jump, jump);

            float t = LifetimeCompletion;
            Color = born * (1f - t * t);
            shown = Main.rand.NextFloat() < 0.8f - 0.4f * t;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            if (!shown)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 world = Position + offset - new Vector2(width, height) * 0.5f;
            Vector2 at = new Vector2((float)Math.Floor(world.X / 2f) * 2f, (float)Math.Floor(world.Y / 2f) * 2f) - Main.screenPosition;
            float w = Math.Max(2f, (float)Math.Round(width / 2f) * 2f);
            float h = Math.Max(2f, (float)Math.Round(height / 2f) * 2f);

            spriteBatch.Draw(pixel, at, new Rectangle(0, 0, 1, 1), Color, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);
        }
    }
}
