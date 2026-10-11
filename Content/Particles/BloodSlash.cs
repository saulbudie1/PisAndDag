using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A cut of a bloodied sword, hanging in the air after it: a hair of red light that is
    /// there at once and thins away from its trailing end; or its black twin, laid beside
    /// it, which is a little slower to come and the last to go. One line each. Cuts cross
    /// where whoever lays them down crosses them.
    ///
    /// The red is light, added to what is there. The black is drawn over what is behind
    /// it, as soot is, so it shows on a bright wall and is nothing in the dark. Both are
    /// the cut's own picture (SlashStreak.png: a soft white mask, tapered along its
    /// length), and the way a line comes and goes is SlashStreak's.
    /// </summary>
    public class BloodSlash : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/SlashStreak";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => !dark;

        /// <summary>How far through its life the leading end has got to the far end of the cut.</summary>
        private const float HeadSpan = 0.12f;

        /// <summary>When the trailing end sets off after it: the red, and the black (later).</summary>
        private const float TailDelay = 0.16f;
        private const float DarkTailDelay = 0.38f;

        /// <summary>Where it begins to go, as a share of its life.</summary>
        private const float FadeFrom = 0.7f;

        private static readonly Color Glow = new Color(150, 8, 14);
        private static readonly Color Mid = new Color(232, 30, 34);
        private static readonly Color Core = new Color(255, 196, 168);

        private readonly Vector2 along;
        private readonly float reach;
        private readonly float width;
        private readonly bool dark;

        /// <param name="centre">The middle of the cut, in world pixels.</param>
        /// <param name="along">The way it lies.</param>
        /// <param name="reach">How far it runs each side of its middle.</param>
        /// <param name="width">How thick the red is at its middle; the black is a little thicker.</param>
        /// <param name="lifetime">Ticks it hangs.</param>
        /// <param name="dark">The black twin, not the red.</param>
        public BloodSlash(Vector2 centre, Vector2 along, float reach, float width, int lifetime, bool dark = false)
        {
            Position = centre;
            this.along = along.SafeNormalize(Vector2.UnitX);
            this.reach = reach;
            this.width = width;
            this.dark = dark;
            Lifetime = lifetime;
            Color = Color.White;
        }

        /// <summary>
        /// A cut and its twin: the red, and the black a few pixels to one side of it,
        /// hanging half as long again.
        /// </summary>
        public static void Pair(Vector2 centre, Vector2 along, float reach, float width, int lifetime)
        {
            Vector2 way = along.SafeNormalize(Vector2.UnitX);
            Vector2 aside = new Vector2(-way.Y, way.X) * (width * 0.5f + 2f) * (Main.rand.NextBool() ? 1f : -1f);

            PADParticleHandler.SpawnParticle(new BloodSlash(centre + aside, way, reach * 0.96f, width, lifetime + lifetime / 2, true));
            PADParticleHandler.SpawnParticle(new BloodSlash(centre, way, reach, width, lifetime));
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null)
                return;

            float t = LifetimeCompletion;
            float delay = dark ? DarkTailDelay : TailDelay;

            // The leading end is at the far end almost at once; the trailing end comes after
            // it down the same line. What is between the two is the cut.
            float head = Smooth(t / HeadSpan);
            float tail = Smooth((t - delay) / (1f - delay));

            Vector2 from = Position - along * reach;
            Vector2 to = Position + along * reach;

            Vector2 a = Vector2.Lerp(from, to, tail);
            Vector2 b = Vector2.Lerp(from, to, head);

            Vector2 line = b - a;
            float length = line.Length();

            if (length < 1f)
                return;

            float alpha = 1f - Smooth((t - FadeFrom) / (1f - FadeFrom));

            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            float rotation = line.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = texture.Size() * 0.5f;

            if (dark)
            {
                // Soot: nothing of its own colour, and what is behind it darker by this much.
                Draw(spriteBatch, texture, mid, rotation, origin, length, width * 1.5f, Color.Black * (0.78f * alpha));
                return;
            }

            Draw(spriteBatch, texture, mid, rotation, origin, length * 1.05f, width * 2.6f, Glow * (0.6f * alpha));
            Draw(spriteBatch, texture, mid, rotation, origin, length, width, Mid * (0.9f * alpha));
            Draw(spriteBatch, texture, mid, rotation, origin, length * 0.97f, width * 0.3f, Core * alpha);
        }

        private static void Draw(SpriteBatch spriteBatch, Texture2D texture, Vector2 at, float rotation, Vector2 origin, float length, float width, Color colour)
        {
            // The picture is tall: its height is the cut's length, its width the cut's thickness.
            Vector2 scale = new Vector2(width / texture.Width, length / texture.Height);

            spriteBatch.Draw(texture, at, null, colour, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
