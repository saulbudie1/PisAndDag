using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A cut. One bright line that snaps to full length, hangs, then wipes itself out from
    /// the trailing end — with a bloom around it so it reads as light, not paint.
    ///
    /// This replaced three scaled MagicPixel bars, and the reason it looks different is
    /// not the colours or the animation, both of which are kept: it is the texture and
    /// the blend. A 1x1 pixel stretched to 26px wide is a hard-edged rectangle, and a
    /// hard-edged rectangle cannot look like light whatever colour it is. SlashStreak.png
    /// is a soft white mask — gaussian across, tapered along — and it is drawn with
    /// BlendState.Additive, so the three layers brighten where they overlap instead of
    /// covering each other. That is the whole Calamity trick, from ThinEndedLine and
    /// BloomLine; the animation here is this mod's own.
    /// </summary>
    public class SlashStreak : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;
        public override bool UseCustomDraw => true;

        /// <summary>A cutscene beat. Never dropped for budget.</summary>
        public override bool Important => true;

        /// <summary>
        /// How far along the cut the leading end is by the time it has fully extended, as
        /// a fraction of the life.
        ///
        /// Short on purpose: the line is supposed to arrive rather than be drawn, so the
        /// head covers the whole diagonal in about a fifth of a second and everything
        /// after that is the tail catching up.
        /// </summary>
        private const float HeadSpan = 0.12f;

        /// <summary>When the trailing end sets off. The gap between the two IS the line.</summary>
        private const float TailDelay = 0.16f;

        /// <summary>Where the fade begins, as a fraction of the life.</summary>
        private const float FadeFrom = 0.75f;

        /// <summary>
        /// A short spike of brightness as the head lands, so the moment of impact reads
        /// as a flash rather than the line simply reaching its end.
        /// </summary>
        private const float FlashPeak = 0.14f;
        private const float FlashWidth = 0.10f;
        private const float FlashGain = 0.9f;

        private const float BloomWidthScale = 2.6f;
        private const float MidWidthScale = 1.0f;
        private const float CoreWidthScale = 0.30f;

        private static readonly Color GlowColour = new Color(90, 170, 255);
        private static readonly Color MidColour = new Color(190, 230, 255);

        private readonly Vector2 along;
        private readonly float reach;
        private readonly float width;
        private readonly NPC follow;

        /// <param name="centre">Midpoint of the cut, in world coordinates.</param>
        /// <param name="along">Direction of the cut. Normalised here.</param>
        /// <param name="reach">Half-length: the cut runs this far each side of the centre.</param>
        /// <param name="width">Thickness of the middle layer; bloom and core scale off it.</param>
        /// <param name="lifetime">Ticks on screen.</param>
        /// <param name="follow">
        /// If given, the cut stays centred on this NPC while it lives, so a target that
        /// shifts under it carries the line with it rather than leaving it hanging in air.
        /// Same idea as Calamity's SlashThrough sticking to its NPC.
        /// </param>
        public SlashStreak(Vector2 centre, Vector2 along, float reach, float width, int lifetime, NPC follow = null)
        {
            Position = centre;
            this.along = along.SafeNormalize(Vector2.UnitX);
            this.reach = reach;
            this.width = width;
            this.follow = follow;
            Lifetime = lifetime;
            Color = Color.White;
        }

        public override void Update()
        {
            if (follow != null && follow.active)
                Position = follow.Center;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            float t = LifetimeCompletion;

            // The head reaches the far end within a fifth of a second; the tail then
            // chases it down the same line. The line IS the gap between them, so it snaps
            // into existence at full length and then shortens itself out of existence.
            float head = Ease(MathHelper.Clamp(t / HeadSpan, 0f, 1f));
            float tail = Ease(MathHelper.Clamp((t - TailDelay) / (1f - TailDelay), 0f, 1f));

            Vector2 from = Position - along * reach;
            Vector2 to = Position + along * reach;

            Vector2 a = Vector2.Lerp(from, to, tail);
            Vector2 b = Vector2.Lerp(from, to, head);

            Vector2 line = b - a;
            float length = line.Length();
            if (length < 1f)
                return;

            // Dims over the last stretch as well, so it thins AND fades instead of being
            // switched off on one frame.
            float alpha = 1f - Ease(MathHelper.Clamp((t - FadeFrom) / (1f - FadeFrom), 0f, 1f));

            // Impact flash: a narrow bump centred on the moment the head arrives.
            float flashT = (t - FlashPeak) / FlashWidth;
            float flash = 1f + FlashGain * MathHelper.Max(0f, 1f - flashT * flashT);

            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            float rotation = line.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = texture.Size() * 0.5f;

            // Three passes, widest and dimmest underneath. Under additive blend these sum:
            // the core burns to white down the middle, the mid tints the shoulders, and
            // the bloom is the halo that makes it read as light on the surroundings.
            // The bloom runs a touch longer and the core a touch shorter, so the ends
            // soften outward instead of all three tips landing on the same pixel.
            Draw(spriteBatch, texture, mid, rotation, origin, length * 1.05f, width * BloomWidthScale,
                GlowColour * (0.55f * alpha));
            Draw(spriteBatch, texture, mid, rotation, origin, length, width * MidWidthScale,
                MidColour * (0.85f * alpha));
            Draw(spriteBatch, texture, mid, rotation, origin, length * 0.97f, width * CoreWidthScale,
                Color.White * (alpha * flash));
        }

        private static void Draw(SpriteBatch spriteBatch, Texture2D texture, Vector2 at, float rotation,
            Vector2 origin, float length, float width, Color colour)
        {
            // The texture is tall; its height becomes the line's length and its width the
            // line's thickness. Scaling each axis independently is what lets one mask
            // serve a needle and a bloom.
            Vector2 scale = new Vector2(width / texture.Width, length / texture.Height);

            spriteBatch.Draw(texture, at, null, colour, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
