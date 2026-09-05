using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A mote of light circling the Game Changing Orb on a flattened ring, so the orbit
    /// reads as a three-dimensional halo rather than a flat circle. Its whole path is
    /// computed from LifetimeCompletion rather than integrated from velocity.
    ///
    /// Particles draw above tiles, so a mote can never truly pass behind the orb. The
    /// depth is faked instead: on the far half of the ring it shrinks, dims and pulls in
    /// tight to the sprite, which reads as distance.
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

        /// <summary>Pixels out from the orb's centre. Roughly hugs a 16px tile.</summary>
        private const float OrbitRadius = 13f;

        /// <summary>Vertical squash. Lower is a shallower viewing angle on the ring.</summary>
        private const float OrbitFlatten = 0.45f;

        /// <summary>Turns completed over the mote's life.</summary>
        private const float Revolutions = 1f;

        /// <summary>How much nearer the front of the ring swings. Pure perspective.</summary>
        private const float DepthRadiusBulge = 0.14f;

        private Vector2 anchor;
        private float startAngle;
        private float radiusScale;
        private int direction;
        private float baseScale;
        private Color baseColor;

        /// <summary>0 at the back of the ring, 1 at the front. Drives size and brightness.</summary>
        private float depth;

        public OrbLight(Vector2 anchor, int lifetime, float scale)
        {
            this.anchor = anchor;
            Position = anchor;

            // The path is set directly in Update, so the handler must not also push it.
            Velocity = Vector2.Zero;

            Lifetime = lifetime;
            baseScale = scale;
            Scale = scale;

            startAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            direction = Main.rand.NextBool() ? 1 : -1;

            // Slight per-mote variance so they don't all trace one identical ring.
            radiusScale = Main.rand.NextFloat(0.85f, 1.15f);

            baseColor = Palette[Main.rand.Next(Palette.Length)];
            Color = baseColor;
        }

        /// <summary>Emits one mote circling a tile's centre.</summary>
        public static void Spawn(Vector2 anchor)
        {
            PADParticleHandler.SpawnParticle(new OrbLight(
                anchor,
                Main.rand.Next(90, 130),            // one unhurried lap, ~1.5-2.2s
                Main.rand.NextFloat(0.5f, 0.85f)));
        }

        public override void Update()
        {
            float t = LifetimeCompletion;
            float angle = startAngle + direction * t * MathHelper.TwoPi * Revolutions;

            // Screen Y grows downward, so a positive sine puts the mote below centre,
            // which is the near side of the ring.
            float facing = MathF.Sin(angle);
            depth = facing * 0.5f + 0.5f;

            float radius = OrbitRadius * radiusScale * (1f + facing * DepthRadiusBulge);

            Position = anchor + new Vector2(
                MathF.Cos(angle) * radius,
                facing * radius * OrbitFlatten);

            // Point the triangle outward, away from the orb. The sprite's apex faces up
            // at zero rotation, hence the quarter turn.
            Rotation = angle + MathHelper.PiOver2;

            // Near motes are larger, far ones shrink back toward the sprite.
            Scale = baseScale * MathHelper.Lerp(0.6f, 1.15f, depth);

            // Fade in and out across the mote's life so nothing pops into existence
            // mid-orbit; square-rooted so it holds lit for most of the lap.
            float lifeFade = MathF.Sqrt(MathF.Sin(t * MathHelper.Pi));

            // Far side is dimmer. Together with the shrink this is what sells "behind".
            float brightness = MathHelper.Lerp(0.3f, 1f, depth);

            Color = baseColor * (brightness * lifeFade);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            // The halo only really shows on the near half — a far mote is a small hard
            // point, which is another depth cue for free.
            float halo = 0.45f * depth;
            if (halo > 0.02f)
            {
                spriteBatch.Draw(texture, drawPos, null, Color * halo, Rotation, origin,
                    Scale * 2.2f, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(texture, drawPos, null, Color, Rotation, origin,
                Scale, SpriteEffects.None, 0f);
        }
    }
}
