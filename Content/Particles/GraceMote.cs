using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A mote hanging in the sun over the Kingdom of Grace: pollen, dust, something off the
    /// willows. It drifts on nothing much, sinks a little, turns over, catching the light and
    /// losing it, and comes and goes slowly. GraceLight puts them only where the sun reaches.
    ///
    /// Added light, a soft dot (GraceMote.png, white, as opaque as it is bright at the middle).
    /// </summary>
    public class GraceMote : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;

        private readonly Color tint;
        private readonly float size;
        private readonly float phase;
        private readonly float swayX;
        private readonly float swayY;

        public GraceMote(Vector2 position, Color tint, int lifetime)
        {
            Position = position;
            Lifetime = lifetime;
            this.tint = tint;

            size = Main.rand.NextFloat(0.18f, 0.42f);
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayX = Main.rand.NextFloat(0.012f, 0.03f);
            swayY = Main.rand.NextFloat(0.018f, 0.04f);

            Velocity = new Vector2(Main.rand.NextFloat(-0.18f, 0.18f), Main.rand.NextFloat(-0.05f, 0.1f));
            Scale = size;
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // It wanders: a slow loop on top of its drift.
            Position += new Vector2((float)Math.Sin(Time * swayX + phase) * 0.22f, (float)Math.Cos(Time * swayY + phase) * 0.12f);

            // In and out slowly, and it catches the light as it turns.
            float come = (float)Math.Sin(t * MathHelper.Pi);
            float catches = 0.55f + 0.45f * (float)Math.Sin(Time * 0.11f + phase * 3f);

            Color = tint * (come * catches * 0.9f);
            Scale = size * (0.85f + 0.15f * catches);
        }
    }
}
