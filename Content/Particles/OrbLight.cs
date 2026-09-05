using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A mote of light that lifts out of the Game Changing Orb, circles it slowly, and
    /// settles back down. Its whole path is computed from LifetimeCompletion rather than
    /// integrated from velocity, so the rise, the orbit and the descent are one curve.
    /// </summary>
    public class OrbLight : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        // It is light, so it should add to what is behind it rather than cover it.
        public override bool UseAdditiveBlend => true;

        private static readonly Color[] Palette =
        {
            new Color(214, 54, 210),
            new Color(148, 61, 109),
            new Color(209, 84, 134),
        };

        /// <summary>Pixels above the orb at the top of the arc.</summary>
        private const float MaxRise = 26f;

        /// <summary>Pixels out from the orb at the widest point of the orbit.</summary>
        private const float MaxRadius = 14f;

        /// <summary>Turns completed over the mote's life. Under 1 keeps it unhurried.</summary>
        private const float Revolutions = 0.85f;

        /// <summary>Vertical squash, so the orbit reads as a ring seen edge-on.</summary>
        private const float OrbitFlatten = 0.4f;

        private Vector2 anchor;
        private float startAngle;
        private int direction;
        private Color baseColor;

        public OrbLight(Vector2 anchor, int lifetime, float scale)
        {
            this.anchor = anchor;
            Position = anchor;

            // The path is set directly in Update, so the handler must not also be
            // pushing this around.
            Velocity = Vector2.Zero;

            Lifetime = lifetime;
            Scale = scale;

            startAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            direction = Main.rand.NextBool() ? 1 : -1;

            baseColor = Palette[Main.rand.Next(Palette.Length)];
            Color = baseColor;
        }

        /// <summary>Emits one mote from a tile's centre.</summary>
        public static void Spawn(Vector2 anchor)
        {
            PADParticleHandler.SpawnParticle(new OrbLight(
                anchor,
                Main.rand.Next(100, 140),          // ~1.7-2.3s, the middle second of which is the orbit
                Main.rand.NextFloat(0.5f, 0.9f)));
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // sin(t * pi) is 0 at both ends and 1 in the middle. One curve gives the
            // whole motion: it lifts, hangs at the top, and comes back down — and the
            // same value swings the orbit outward and reels it in.
            float arc = MathF.Sin(t * MathHelper.Pi);

            float rise = arc * MaxRise;
            float radius = arc * MaxRadius;
            float angle = startAngle + direction * t * MathHelper.TwoPi * Revolutions;

            Position = anchor + new Vector2(
                MathF.Cos(angle) * radius,
                -rise + MathF.Sin(angle) * radius * OrbitFlatten);

            // Point the triangle along the way it is travelling.
            Rotation = angle + MathHelper.PiOver2 * direction;

            // Square-rooting the same curve brightens it quickly and holds it lit for
            // most of the flight, instead of only glowing at the apex.
            Color = baseColor * MathF.Sqrt(arc);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            // Wide and dim for the halo, then tight and bright for the core. Two draws
            // of one texture is far cheaper than a blur and reads better than either alone.
            spriteBatch.Draw(texture, drawPos, null, Color * 0.4f, Rotation, origin,
                Scale * 2.2f, SpriteEffects.None, 0f);

            spriteBatch.Draw(texture, drawPos, null, Color, Rotation, origin,
                Scale, SpriteEffects.None, 0f);
        }
    }
}
