using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Empress;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// The line of a Terraprisma blade's opening cut (PrismCut) as it hangs before it bursts: it arrives from the blade's
    /// waiting place to where it came out in three ticks; then hangs, thin at both ends, a white core inside the blade's
    /// colour going round through the Empress's, flickering; growing wider and brighter as it nears its burst, stars
    /// glinting along it at the last. On the half-size canvas, as the rest of the burst is.
    /// </summary>
    public class PrismCutLine : PADParticle
    {
        /// <summary>Never drawn as a sprite: its draw is EmpressArt's lines. A picture the handler can load for it.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;
        public override bool Important => true;

        /// <summary>Ticks it takes to reach its full length.</summary>
        private const float Arrives = 3f;

        private readonly Vector2 from, to;
        private readonly Color hue;

        public PrismCutLine(Vector2 from, Vector2 to, Color hue, int lifetime)
        {
            Position = from;
            Velocity = Vector2.Zero;
            this.from = from;
            this.to = to;
            this.hue = hue;
            Color = hue;
            Lifetime = Math.Max(2, lifetime);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            float head = Math.Min(1f, (Time + 1f) / Arrives);
            float charge = LifetimeCompletion;
            float flicker = 0.85f + 0.15f * (float)Math.Sin(Time * 1.7f);
            float width = 9f * (0.7f + 0.7f * charge * charge);
            float length = Vector2.Distance(from, to);
            int pieces = Math.Clamp((int)(length / 24f), 4, 48);
            float turn = Main.GlobalTimeWrappedHourly * 0.8f;

            for (int i = 0; i < pieces; i++)
            {
                float a = i / (float)pieces, b = Math.Min(head, (i + 1) / (float)pieces);

                if (a >= head)
                    break;

                float middle = (a + b) * 0.5f;
                float taper = (float)Math.Sqrt(Math.Sin(MathHelper.Pi * middle));
                Color colour = Color.Lerp(hue, EmpressArt.Prism(middle * 1.5f + turn), 0.5f);
                Vector2 start = Vector2.Lerp(from, to, a), end = Vector2.Lerp(from, to, b);

                EmpressArt.DrawLine(spriteBatch, start, end, width * 2.6f * taper, colour * (0.3f * flicker));
                EmpressArt.DrawLine(spriteBatch, start, end, width * taper, colour * (0.75f * flicker));
            }

            EmpressArt.DrawBeam(spriteBatch, from, Vector2.Lerp(from, to, head), width * 0.35f, Color.White * (0.85f * flicker));

            // At the last, stars glinting along it as it gathers to burst.
            if (charge > 0.5f)
            {
                float glint = (charge - 0.5f) * 2f;

                for (int k = 0; k < 4; k++)
                {
                    float u = (k * 0.37f + Time * 0.05f) % 1f;
                    EmpressArt.DrawStar(spriteBatch, Vector2.Lerp(from, to, u), 22f * glint, Time * 0.1f + k, Color.White * (0.8f * glint));
                }
            }
        }
    }
}
