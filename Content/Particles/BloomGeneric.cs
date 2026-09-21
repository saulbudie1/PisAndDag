using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// The ball of light at the point of impact. Calamity's StrongBloom, ported, and
    /// deliberately kept apart from SparkShard so either can be used without the
    /// other — the shards are a throw and this is a landing, and plenty of things want
    /// only one of the two.
    ///
    /// It is one curve rather than a flash and a fade. A sine over its own life opens at
    /// nothing, is over the top at the halfway mark and closes at nothing, so there is
    /// no frame where it pops into existence at full strength.
    ///
    /// Additive and untextured by anything but a falloff, so its colour is set entirely
    /// by what it is given. It is also the only particle here that lights the room; a
    /// hit that brightens the wall behind it reads as an event, and one that does not
    /// reads as a sticker.
    /// </summary>
    public class BloomGeneric : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color born;
        private readonly bool lights;

        private float opacity;

        public BloomGeneric(Vector2 position, Vector2 velocity, Color colour, float scale, int lifetime, bool lights = true)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Scale = scale;
            Lifetime = lifetime;
            this.lights = lights;

            // The sprite is round, so the angle is invisible — but rolling it stops two
            // blooms on the same frame from sharing a seam where their edges meet.
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update()
        {
            opacity = (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);

            Color = born * opacity;
            Velocity *= 0.95f;

            if (lights)
                Lighting.AddLight(Position, Color.R / 255f, Color.G / 255f, Color.B / 255f);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture is null)
                return;

            // Colour already carries the opacity and it is applied again here, so the
            // curve is squared. That is not an oversight in the original and it is not
            // one here: squared, the bloom has a hard middle and a soft edge and lands
            // like a hit, where the plain sine reads as a lamp being turned up and down.
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color * opacity,
                Rotation, texture.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
        }
    }
}
