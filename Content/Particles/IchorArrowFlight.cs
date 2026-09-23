using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Pugream's arrow on the night of the Eye (EyeFinale). Not a projectile: it hurts
    /// nothing and is sent nowhere; the Eye dies on the server's word, on the tick this
    /// lands, and this is what everyone watches land.
    ///
    /// It does not fly so much as arrive. Given where it leaves the bow, where it is
    /// going and the tick it gets there, it is placed along that line each tick, and
    /// the scene tells it every tick where the Eye is now, so whatever the Eye does in
    /// the last half second the arrow is in it on the tick of the hit. After that it
    /// carries on at the speed it hit with, through and out the far side, and is gone.
    ///
    /// Vanilla's ichor arrow, unlit, with the look of something burning in flight: a
    /// thin streak of gold back along the way it came, a few ghosts of itself in the
    /// streak, and a halo. The glow is drawn with no alpha in the ordinary batch, which
    /// adds it, so the one texture needs no second pass.
    /// </summary>
    public class IchorArrowFlight : PADParticle
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IchorArrow;

        public override bool Important => true;
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        /// <summary>Ticks it flies on after the hit, fading.</summary>
        private const int Carry = 14;

        /// <summary>Positions kept for the streak and the ghosts.</summary>
        private const int TrailLength = 8;

        private static readonly Color Gold = new Color(255, 206, 90);

        private readonly Vector2 from;
        private readonly int arrive;
        private Vector2 aim;

        /// <summary>How far it went last tick. Its heading, and after the hit, its velocity.</summary>
        private Vector2 step;

        private readonly Vector2[] trail = new Vector2[TrailLength];
        private int trailCount;

        /// <summary>The way it is going, as a unit vector.</summary>
        public Vector2 Heading => step.SafeNormalize(Vector2.UnitX);

        /// <param name="arrive">Ticks from now until it is at <paramref name="aim"/>. One is this tick.</param>
        public IchorArrowFlight(Vector2 from, Vector2 aim, int arrive)
        {
            Position = from;
            Velocity = Vector2.Zero;
            Color = Color.White;
            Scale = 1f;

            this.from = from;
            this.aim = aim;
            this.arrive = Math.Max(1, arrive);

            Lifetime = this.arrive + Carry;
            step = (aim - from) / this.arrive;
            Rotation = step.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>Where it lands, now. Only means anything before it gets there.</summary>
        public void Aim(Vector2 at) => aim = at;

        private float Opacity() => Time <= arrive ? 1f : MathHelper.Clamp(1f - (Time - arrive) / (float)Carry, 0f, 1f);

        public override void Update()
        {
            for (int i = TrailLength - 1; i > 0; i--)
                trail[i] = trail[i - 1];

            trail[0] = Position;
            trailCount = Math.Min(trailCount + 1, TrailLength);

            if (Time <= arrive)
            {
                // Velocity is nothing while it is being placed, so the handler has not
                // moved it; this does.
                Vector2 next = Vector2.Lerp(from, aim, Time / (float)arrive);
                step = next - Position;
                Position = next;

                // Through it, at the speed it hit with. From here the handler moves it.
                if (Time == arrive)
                    Velocity = step;
            }
            else
            {
                Velocity *= 0.985f;
                step = Velocity;
            }

            // The sprite points up the texture, so a quarter turn onto its heading.
            if (step.LengthSquared() > 0.01f)
                Rotation = step.ToRotation() + MathHelper.PiOver2;

            float fade = Opacity();

            Lighting.AddLight(Position, Gold.ToVector3() * 0.8f * fade);

            if (fade > 0.2f && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(Position, DustID.Ichor, -step * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    0, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null)
                return;

            float fade = Opacity();

            if (fade <= 0f)
                return;

            Vector2 origin = texture.Size() * 0.5f;
            int count = Math.Min(trailCount, TrailLength);

            // The streak: gold, and a hairline of white down it, from the oldest point kept
            // to where it is now.
            if (count > 0)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Vector2 tail = trail[count - 1];
                Vector2 line = Position - tail;
                float length = line.Length();

                if (length > 1f)
                {
                    float angle = line.ToRotation();
                    Vector2 start = tail - Main.screenPosition;
                    Rectangle one = new Rectangle(0, 0, 1, 1);

                    Color glow = Gold * (0.55f * fade);
                    glow.A = 0;
                    spriteBatch.Draw(pixel, start, one, glow, angle, new Vector2(0f, 0.5f), new Vector2(length, 3f), SpriteEffects.None, 0f);

                    Color core = Color.White * (0.6f * fade);
                    core.A = 0;
                    spriteBatch.Draw(pixel, start, one, core, angle, new Vector2(0f, 0.5f), new Vector2(length, 1f), SpriteEffects.None, 0f);
                }
            }

            // Ghosts of it in the streak, fainter the further back.
            for (int i = 1; i < count; i += 2)
            {
                Color ghost = Gold * (0.35f * (1f - i / (float)TrailLength) * fade);
                ghost.A = 0;
                spriteBatch.Draw(texture, trail[i] - Main.screenPosition, null, ghost, Rotation, origin, Scale, SpriteEffects.None, 0f);
            }

            Vector2 at = Position - Main.screenPosition;

            spriteBatch.Draw(texture, at, null, Color.White * fade, Rotation, origin, Scale, SpriteEffects.None, 0f);

            Color halo = Gold * (0.5f * fade);
            halo.A = 0;
            spriteBatch.Draw(texture, at, null, halo, Rotation, origin, Scale * 1.25f, SpriteEffects.None, 0f);
        }
    }
}
