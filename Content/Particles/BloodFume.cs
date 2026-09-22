using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// What comes up out of the ground where the Court of Hands is called: a slow,
    /// dark red vapour, more glow than smoke. Rises, drifts, swells a little and goes.
    /// Additive on the bloom sprite, so a column of them reads as a lit haze rather
    /// than a stack of discs.
    ///
    /// Three colours, rolled at birth, so the haze is not one flat red.
    /// </summary>
    public class BloodFume : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";
        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;

        public static readonly Color[] Palette =
        {
            new Color(0x68, 0x03, 0x03),
            new Color(0xb1, 0x25, 0x16),
            new Color(0xbd, 0x00, 0x26),
        };

        private readonly Color born;
        private readonly float spin;
        private readonly float drift;

        public BloodFume(Vector2 position, Vector2 velocity, float scale, int lifetime)
        {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = lifetime;
            Color = born = Palette[Main.rand.Next(Palette.Length)];
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.03f, 0.03f);
            drift = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // Slows as it climbs and wanders sideways a little, like warm air.
            Velocity.Y *= 0.97f;
            Velocity.X = Velocity.X * 0.95f + (float)System.Math.Sin(Time * 0.15f + drift) * 0.03f;

            Scale *= 1.012f;
            Rotation += spin;

            // In fast, out slow.
            float alpha = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            Color = born * (alpha * 0.8f);
        }
    }
}
