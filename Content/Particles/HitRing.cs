using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A ring thrown out from a blow (after Calamity's PulseRing and DirectionalPulseRing, looked at for what they do):
    /// from one radius to another, fast at first and settling, fading as it goes; flattened along the way the blow went
    /// if asked (<c>squish</c> under 1), so that it reads as a shock seen edge on. Sizes in world pixels. Added light,
    /// drawn through the half-size canvas.
    /// </summary>
    public class HitRing : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;
        public override bool UseCustomDraw => true;

        /// <summary>Where the middle of the band is in the picture, as a share of its half-width.</summary>
        private const float Band = 0.8f;

        private readonly float from, to, squish;
        private readonly Color born;
        private float radius, opacity;

        public HitRing(Vector2 position, Color colour, float from, float to, int lifetime, float squish = 1f, float along = 0f)
        {
            Position = position;
            Color = born = colour;
            this.from = from;
            this.to = to;
            this.squish = squish;
            Rotation = along;
            Lifetime = Math.Max(2, lifetime);
            Pixelate = true;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;
            float eased = 1f - (float)Math.Pow(1f - t, 4);
            radius = MathHelper.Lerp(from, to, eased);
            opacity = (float)Math.Cos(t * MathHelper.PiOver2);
            Color = born * opacity;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture is null)
                return;

            float scale = radius / (texture.Width * 0.5f * Band);
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation, texture.Size() * 0.5f,
                new Vector2(scale * squish, scale), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// The star of light where a heavy blow lands (after Calamity's ImpactParticle): thin spikes crossed, turning
    /// against each other, out and in again over its life. <c>size</c> is the long spikes' whole length in world
    /// pixels. Added light, through the half-size canvas.
    /// </summary>
    public class ImpactStar : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;
        public override bool UseCustomDraw => true;

        private readonly float size, spin;

        public ImpactStar(Vector2 position, Color colour, float size, int lifetime, float spin = 0.08f)
        {
            Position = position;
            Color = colour;
            this.size = size;
            this.spin = spin;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Lifetime = Math.Max(2, lifetime);
            Pixelate = true;
        }

        public override void Update() => Rotation += spin;

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture is null)
                return;

            // Out and in: fast out (the first third), slower in.
            float t = LifetimeCompletion;
            float shown = t < 0.3f ? (float)Math.Sin(t / 0.3f * MathHelper.PiOver2) : (float)Math.Cos((t - 0.3f) / 0.7f * MathHelper.PiOver2);
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 at = Position - Main.screenPosition;
            Vector2 longOne = new Vector2(size / texture.Width, 0.9f) * shown;
            Vector2 shortOne = longOne * new Vector2(0.6f, 0.7f);

            spriteBatch.Draw(texture, at, null, Color, Rotation, origin, longOne, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, at, null, Color, Rotation + MathHelper.PiOver2, origin, longOne, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, at, null, Color * 0.8f, -Rotation + MathHelper.PiOver4, origin, shortOne, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, at, null, Color * 0.8f, -Rotation - MathHelper.PiOver4, origin, shortOne, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// Cracks of light struck out from a blow (after Calamity's CrackParticle): one of three, snapped out to its size
    /// and fading where it is. <c>size</c> is its whole width in world pixels. Added light, through the half-size
    /// canvas.
    /// </summary>
    public class HitCrack : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;
        public override bool UseCustomDraw => true;
        public override int FrameVariants => 3;

        private readonly float size;
        private readonly Color born;

        public HitCrack(Vector2 position, Color colour, float size, int lifetime, float? turned = null)
        {
            Position = position;
            Color = born = colour;
            this.size = size;
            Rotation = turned ?? Main.rand.NextFloat(MathHelper.TwoPi);
            Variant = Main.rand.Next(3);
            Lifetime = Math.Max(2, lifetime);
            Pixelate = true;
        }

        public override void Update()
        {
            Color = born * (float)Math.Cos(LifetimeCompletion * MathHelper.PiOver2);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture is null)
                return;

            Rectangle frame = texture.Frame(1, FrameVariants, 0, Variant);
            float out_ = 1f - (float)Math.Pow(1f - Math.Min(1f, LifetimeCompletion * 4f), 3);
            float scale = size / frame.Width * MathHelper.Lerp(0.35f, 1f, out_);
            spriteBatch.Draw(texture, Position - Main.screenPosition, frame, Color, Rotation, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }
}
