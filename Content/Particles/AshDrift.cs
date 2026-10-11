using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// One fleck of a body gone to ash that lies a while before it goes: Foigh's, at his end
    /// (Foigh.End), by request "turns into a pile of ash and fly away (check guide fade)".
    ///
    /// As AshDissolve (the Guide's, GuideSacrifice) it starts where one pixel of the body was
    /// drawn, as grey as that pixel was bright. Unlike it, it does not go with the wind at
    /// once: it falls by its weight onto the ground under it and lies there with the rest, a
    /// heap where the body was; then the wind lifts it (from <c>lifts</c>, which the one who
    /// makes them sets so that the side the wind comes from goes first) and takes it away,
    /// unevenly, fading as it goes. The wind is its own, given: a place may have none (the
    /// hall has no sky).
    /// </summary>
    public class AshDrift : PADParticle
    {
        /// <summary>AshDissolve's picture: a soft fleck four pixels across.</summary>
        public override string Texture => "PisAndDag/Content/Particles/AshDissolve";

        public override bool SetLifetime => true;

        /// <summary>Below this a pixel is too dark to be worth a fleck (as AshDissolve).</summary>
        public const float DarkFloor = AshDissolve.DarkFloor;

        /// <summary>How grey the ash is at its brightest (as AshDissolve: ash is never white).</summary>
        private const float AshCeiling = 0.67f;

        /// <summary>Its weight, a tick, and the fastest it falls.</summary>
        private const float Weight = 0.16f;
        private const float Fastest = 3.5f;

        /// <summary>Ticks the wind takes to have all of it.</summary>
        private const float Caught = 36f;

        private readonly float ground;
        private readonly int lifts;
        private readonly Vector2 wind;
        private readonly int id;
        private readonly Color ash;
        private readonly float size;

        /// <summary>
        /// A fleck where a pixel this bright was drawn; the ground it lies on (its middle,
        /// on the heap); the tick of its life the wind lifts it at, the wind, and how long it
        /// lives in all; and its number among the rest, for its own flutter.
        /// </summary>
        public AshDrift(Vector2 position, float pixelBrightness, float ground, int lifts, Vector2 wind, int lifetime, int index)
        {
            Position = position;
            Velocity = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.3f, 0.1f));
            Lifetime = Math.Max(lifetime, lifts + 2);
            size = Main.rand.NextFloat(0.5f, 0.8f);
            Scale = size;

            this.ground = ground;
            this.lifts = lifts;
            this.wind = wind;
            id = index;

            float grey = MathHelper.Clamp(pixelBrightness, 0f, 1f) * AshCeiling;

            ash = new Color(grey, grey, grey, 1f);
            Color = ash;
        }

        public override void Update()
        {
            if (Time < lifts)
            {
                // Down onto the heap, and still there.
                if (Position.Y < ground)
                {
                    Velocity.X *= 0.9f;
                    Velocity.Y = Math.Min(Velocity.Y + Weight, Fastest);
                }

                if (Position.Y >= ground)
                {
                    Position.Y = ground;
                    Velocity = Vector2.Zero;
                }

                return;
            }

            // The wind: from lying still to its speed, each fleck a little faster or slower
            // than the next and fluttering on its own.
            float since = Time - lifts;
            float k = MathHelper.Clamp(since / Caught, 0f, 1f);
            float own = 0.75f + 0.25f * (1f + MathF.Sin(id * 1.7f));

            Vector2 gust = new Vector2(wind.X * own, wind.Y * own + MathF.Sin(since * 0.15f + id * 0.37f) * 0.6f);

            Velocity = Vector2.Lerp(Velocity, gust, 0.04f + 0.12f * k);

            float left = 1f - since / Math.Max(1f, Lifetime - lifts);

            Color = ash * MathHelper.Clamp(left * 1.5f, 0f, 1f);
            Scale = size * MathHelper.Lerp(0.6f, 1f, MathHelper.Clamp(left, 0f, 1f));
        }
    }
}
