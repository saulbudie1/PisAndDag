using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Empress;
using Terraria;
using Terraria.GameContent;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A ring of light thrown out from a point and fading as it goes: hard and thin (the game's white pixel, in lengths
    /// round it), or soft and wide (the mod's SoftLine, as EmpressArt.DrawLine lays it), whole or broken into dashes that
    /// turn. The edge of a blast or of a scan (Made By Tentochi).
    /// </summary>
    public class CyberRing : PADParticle
    {
        /// <summary>Never drawn: its own draw is lines. A picture the handler can load for it.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color born;
        private readonly float from;
        private readonly float to;
        private readonly float thickness;
        private readonly float gap;
        private readonly float spin;
        private readonly int segments;
        private readonly bool soft;
        private float radius;

        /// <param name="gap">The share of each length left dark (0 a whole ring, 0.5 even dashes).</param>
        /// <param name="spin">How far it turns a tick, in radians.</param>
        public CyberRing(Vector2 at, Color colour, float from, float to, float thickness, int lifetime, int segments = 48, float gap = 0f, float spin = 0f, bool soft = false)
        {
            Position = at;
            Velocity = Vector2.Zero;
            Color = born = colour;
            Lifetime = Math.Max(2, lifetime);
            this.from = from;
            this.to = to;
            this.thickness = Math.Max(1f, thickness);
            this.gap = MathHelper.Clamp(gap, 0f, 0.95f);
            this.spin = spin;
            this.segments = Math.Max(6, segments);
            this.soft = soft;
            radius = from;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update()
        {
            float t = LifetimeCompletion;
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            radius = MathHelper.Lerp(from, to, eased);
            Color = born * (float)Math.Pow(1f - t, 1.5);
            Rotation += spin;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            if (radius < 1f)
                return;

            float step = MathHelper.TwoPi / segments;
            float lit = step * (1f - gap);

            // Thin rings fade in their width as they spread, so the far edge of a big one is a hair, not a band.
            float width = soft ? thickness : Math.Max(2f, thickness * (1f - 0.5f * LifetimeCompletion));

            for (int i = 0; i < segments; i++)
            {
                float a = Rotation + i * step;
                Vector2 p0 = Position + a.ToRotationVector2() * radius;
                Vector2 p1 = Position + (a + lit).ToRotationVector2() * radius;

                if (soft)
                    EmpressArt.DrawLine(spriteBatch, p0, p1, width, Color);
                else
                    Line(spriteBatch, p0, p1, width, Color);
            }
        }

        /// <summary>A hard line of the game's white pixel, this wide, from one world point to another.</summary>
        public static void Line(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float width, Color colour)
        {
            Vector2 way = to - from;
            float length = way.Length();

            if (length < 0.5f)
                return;

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, from - Main.screenPosition, new Rectangle(0, 0, 1, 1), colour, way.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f);
        }
    }
}
