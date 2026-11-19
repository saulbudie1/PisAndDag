using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A blow landing, in one call (by request, after how Calamity dresses a hit: never one thing but several at once,
    /// a flash, a ring, sparks the way it went, something that hangs). Each lays the mod's own particles down
    /// together, all through the half-size canvas (PADPixelCanvas) so they sit on the two-pixel grid:
    ///
    ///   Light    a jab, a shot, a small cut: a flash, a small ring flattened the way it went, a few sparks on
    ///   Heavy    a finisher, a slam: a bigger flash, a star, cracks, a ring and a second after it, many sparks, dust
    ///   Slash    a blade through something: the cut's line, sparks off both its ends, a flash
    ///   Ground   something driven into the ground: chips of it thrown up, dust, a low wide ring
    ///
    /// <c>way</c> is the way the blow was going (any length); <c>colour</c> the weapon's. Nothing here hurts or
    /// shakes the screen: it is only what is seen. Client only (the handler drops everything on a server).
    /// </summary>
    public static class HitFx
    {
        private static void Add(PADParticle particle)
        {
            particle.Pixelate = true;
            PADParticleHandler.SpawnParticle(particle);
        }

        private static void Sparks(Vector2 at, Vector2 way, Color colour, int many, float spread, float fast, float length)
        {
            for (int i = 0; i < many; i++)
            {
                Vector2 velocity = way.RotatedByRandom(spread) * Main.rand.NextFloat(fast * 0.45f, fast);
                Add(new SparkShard(at, velocity, colour, length * 0.4f, 3f, length * Main.rand.NextFloat(0.7f, 1.2f), Main.rand.Next(10, 18)));
            }
        }

        public static void Light(Vector2 at, Vector2 way, Color colour)
        {
            if (Main.dedServ)
                return;

            way = way.SafeNormalize(Vector2.UnitX);
            Add(new BloomGeneric(at, Vector2.Zero, colour * 0.7f, 0.3f, 8));
            Add(new HitRing(at, colour, 4f, 22f, 12, 0.45f, way.ToRotation()));
            Sparks(at, way, colour, 4, 0.6f, 7f, 20f);
        }

        public static void Heavy(Vector2 at, Vector2 way, Color colour)
        {
            if (Main.dedServ)
                return;

            way = way.SafeNormalize(Vector2.UnitX);
            Color pale = Color.Lerp(colour, Color.White, 0.5f);

            Add(new BloomGeneric(at, Vector2.Zero, colour * 0.9f, 0.7f, 12));
            Add(new ImpactStar(at, pale, 110f, 14));
            Add(new HitCrack(at, pale, 90f, 16));
            Add(new HitRing(at, pale, 8f, 54f, 16, 0.5f, way.ToRotation()));
            Add(new HitRing(at, colour, 4f, 84f, 24));
            Sparks(at, way, colour, 10, 0.9f, 11f, 34f);

            for (int i = 0; i < 3; i++)
                Add(new DustFog(at + Main.rand.NextVector2Circular(8f, 8f), way.RotatedByRandom(0.8f) * Main.rand.NextFloat(0.6f, 1.6f),
                    Color.Lerp(colour, new Color(150, 150, 150), 0.6f), Main.rand.Next(30, 46), Main.rand.NextFloat(0.4f, 0.7f), 0.28f));
        }

        public static void Slash(Vector2 at, Vector2 along, Color colour, float reach = 70f)
        {
            if (Main.dedServ)
                return;

            along = along.SafeNormalize(Vector2.UnitX);
            Add(new SlashStreak(at, along, reach, 5f, 14));
            Add(new BloomGeneric(at, Vector2.Zero, colour * 0.6f, 0.35f, 9));
            Sparks(at + along * reach * 0.4f, along, colour, 4, 0.35f, 9f, 24f);
            Sparks(at - along * reach * 0.4f, -along, colour, 2, 0.35f, 6f, 18f);
        }

        public static void Ground(Vector2 at, Color ground)
        {
            if (Main.dedServ)
                return;

            Add(new HitRing(at, Color.Lerp(ground, Color.White, 0.4f) * 0.8f, 6f, 60f, 18, 0.3f, MathHelper.PiOver2));

            for (int i = 0; i < 7; i++)
                Add(new StoneChip(at + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f), new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6.5f, -2.5f)),
                    ground, Main.rand.Next(40, 70), Main.rand.NextFloat(0.9f, 1.5f)));

            for (int i = 0; i < 4; i++)
                Add(new DustFog(at + new Vector2(Main.rand.NextFloat(-18f, 18f), -4f), new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1f, -0.2f)),
                    ground, Main.rand.Next(36, 60), Main.rand.NextFloat(0.5f, 0.9f), 0.3f));
        }
    }
}
