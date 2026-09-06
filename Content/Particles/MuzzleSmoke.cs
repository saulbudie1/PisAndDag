using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Powder smoke coughed out of the barrel: drifts along the shot, slows, swells and
    /// thins out. Uses the handler's default draw — no CustomDraw needed.
    /// </summary>
    public class MuzzleSmoke : PADParticle
    {
        public override bool SetLifetime => true;

        private static readonly Color SmokeColor = new Color(118, 112, 106);

        private float spin;

        public MuzzleSmoke(Vector2 position, Vector2 velocity, int lifetime, float scale)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = scale;

            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.05f, 0.05f);

            Color = SmokeColor;
        }

        /// <summary>A small puff at the muzzle, thrown along the barrel with spread.</summary>
        public static void Spawn(Vector2 position, Vector2 direction, int count = 3)
        {
            Vector2 forward = direction.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < count; i++)
            {
                // A narrow cone, not a puff ball — smoke follows the bullet out.
                float spread = MathHelper.ToRadians(Main.rand.NextFloat(-16f, 16f));

                PADParticleHandler.SpawnParticle(new MuzzleSmoke(
                    position + Main.rand.NextVector2Circular(3f, 3f),
                    forward.RotatedBy(spread) * Main.rand.NextFloat(1.2f, 3.4f),
                    Main.rand.Next(22, 38),
                    Main.rand.NextFloat(0.35f, 0.6f)));
            }
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // Loses momentum fast, then hangs and expands the way real smoke does.
            Velocity *= 0.88f;
            Velocity.Y -= 0.012f;

            Scale += 0.018f;
            Rotation += spin;

            // Fades late rather than evenly, so the puff stays readable then goes.
            Color = Color.Lerp(SmokeColor, Color.Transparent, t * t);
        }
    }
}
