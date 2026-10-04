using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// What the parry looks and sounds like, all of it: white and steel, never the warning
    /// yellow, which is kept for one thing (CCBypass). Nothing here is drawn from a picture of
    /// its own: stars, shards, blooms, the roar's ring, a cut, the ground's own dust.
    ///
    ///   A shot turned aside        <see cref="Spark"/>     a star, a bloom, shards that fall
    ///   An enemy turned aside      <see cref="Clash"/>     the spark, a ring, shards thrown the way it is sent
    ///   Pushed back                <see cref="Skid"/>      the ground's dust off its feet, or streaks in the air
    ///   Staggered                  <see cref="Break"/>     a ring off the whole of it, a star over its head, a deeper note
    ///   While it is                <see cref="Tell"/>      three glints going round over its head; <see cref="Tint"/> on its body
    ///   The first hit on it then   <see cref="Riposte"/>   a cut of light through it, two rings, a star, shards the way it is thrown
    ///
    /// Client side all of it: the particle handler and the sound engine do nothing on a
    /// server, and every entry bails there first.
    /// </summary>
    public static class ParryEffects
    {
        public static readonly Color Steel = new Color(214, 226, 240);
        private static readonly Color Pale = new Color(184, 211, 222);
        private static readonly Color[] Sparks = { new Color(255, 255, 255), new Color(214, 226, 240), new Color(184, 211, 222) };

        /// <summary>Ticks between the glints over a staggered enemy's head, and how many go round.</summary>
        private const int GlintEvery = 3;
        private const int Glints = 3;

        /// <summary>How fast they go round: radians a tick.</summary>
        private const float HaloTurn = 0.13f;

        /// <summary>
        /// A shot turned aside, at the point it happened: steel on steel, a star of light, shards
        /// thrown off it.
        /// </summary>
        public static void Spark(Vector2 at)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.4f, PitchVariance = 0.12f, Volume = 0.85f }, at);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.3f, PitchVariance = 0.12f, Volume = 0.55f }, at);

            PADParticleHandler.SpawnParticle(new CrownGlint(at, 46f, 14, Steel));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Steel * 0.6f, 0.42f, 12));
            SparkShard.Spray(at, Steel, 9, 6f, 26f, 15, falls: true, palette: Sparks);
        }

        /// <summary>
        /// An enemy turned aside: the spark, a ring going out from it, and a fan of shards
        /// thrown <paramref name="away"/>, the way the enemy is sent.
        /// </summary>
        public static void Clash(Vector2 at, Vector2 away)
        {
            if (Main.dedServ)
                return;

            Spark(at);

            PADParticleHandler.SpawnParticle(new ParryRing(at, 6f, 62f, 16, Steel));

            Vector2 way = away.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < 8; i++)
            {
                float length = Main.rand.NextFloat(20f, 34f);

                PADParticleHandler.SpawnParticle(new SparkShard(at, way.RotatedByRandom(0.6f) * Main.rand.NextFloat(5f, 12f),
                    Sparks[Main.rand.Next(Sparks.Length)], length * 0.5f, 16f, length, Main.rand.Next(12, 19), falls: true));
            }
        }

        /// <summary>
        /// Staggered, this tick: a ring off the whole of it, a star over its head, shards off
        /// the top of it, and a deeper note under the clash.
        /// </summary>
        public static void Break(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 middle = npc.Center;
            float size = Math.Max(npc.width, npc.height);

            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.35f, PitchVariance = 0.08f, Volume = 0.8f }, middle);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.3f, Volume = 0.5f }, middle);

            PADParticleHandler.SpawnParticle(new ParryRing(middle, size * 0.3f, size * 0.9f + 46f, 22, Steel, npc));
            PADParticleHandler.SpawnParticle(new CrownGlint(Crown(npc), 34f, 16, Steel));

            for (int i = 0; i < 8; i++)
            {
                Vector2 up = new Vector2(Main.rand.NextFloat(-1f, 1f), -1f).SafeNormalize(-Vector2.UnitY);
                float length = Main.rand.NextFloat(14f, 24f);

                PADParticleHandler.SpawnParticle(new SparkShard(npc.Top + new Vector2(Main.rand.NextFloat(-npc.width, npc.width) * 0.4f, 0f),
                    up * Main.rand.NextFloat(3f, 7f), Sparks[Main.rand.Next(Sparks.Length)], length * 0.5f, 14f, length,
                    Main.rand.Next(14, 22), falls: true));
            }
        }

        /// <summary>
        /// Each tick it is staggered: glints going round over its head, one lit every few
        /// ticks, each a third of the way round from the last. <paramref name="clock"/> is
        /// anything that counts up once a tick for this enemy.
        /// </summary>
        public static void Tell(NPC npc, int clock)
        {
            if (Main.dedServ || clock % GlintEvery != 0)
                return;

            int which = clock / GlintEvery % Glints;
            float angle = clock * HaloTurn + which * MathHelper.TwoPi / Glints;
            float wide = Math.Max(10f, npc.width * 0.5f);

            // A flat ring, seen from the side: wide across, a third as deep.
            Vector2 at = Crown(npc) + new Vector2((float)Math.Cos(angle) * wide, (float)Math.Sin(angle) * wide * 0.3f);

            PADParticleHandler.SpawnParticle(new CrownGlint(at, 16f, 12, which == 0 ? Color.White : Steel));
        }

        /// <summary>
        /// The light on a staggered enemy's body, on whatever draws with the colour the game
        /// hands it: beating toward steel, twice a second.
        /// </summary>
        public static Color Tint(Color drawn, int clock)
        {
            float beat = 0.5f + 0.5f * (float)Math.Sin(clock * (MathHelper.TwoPi / 30f));
            return Color.Lerp(drawn, new Color(Steel.R, Steel.G, Steel.B, drawn.A), 0.18f + 0.22f * beat);
        }

        /// <summary>
        /// The first hit on a staggered enemy, as it lands: a cut of light through it the way
        /// it is thrown, two rings, a star, a bloom, and shards that way.
        /// </summary>
        public static void Riposte(NPC npc, int hitDirection)
        {
            if (Main.dedServ)
                return;

            Vector2 middle = npc.Center;
            float size = Math.Max(npc.width, npc.height);
            int way = hitDirection < 0 ? -1 : 1;

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.15f, PitchVariance = 0.08f, Volume = 0.9f }, middle);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.15f, Volume = 0.75f }, middle);

            // Up and through, the way it goes.
            Vector2 along = new Vector2(way, -0.42f).SafeNormalize(Vector2.UnitX);

            PADParticleHandler.SpawnParticle(new SlashStreak(middle, along, size * 0.9f + 22f, 9f, 18, npc));
            PADParticleHandler.SpawnParticle(new ParryRing(middle, size * 0.25f, size + 92f, 24, Color.White, npc));
            PADParticleHandler.SpawnParticle(new ParryRing(middle, 4f, size * 0.6f + 40f, 15, Steel, npc));
            // Quick: it is thrown, and the light should be where the blow landed while it is still there.
            PADParticleHandler.SpawnParticle(new CrownGlint(middle, 64f + size * 0.4f, 9, Color.White));
            PADParticleHandler.SpawnParticle(new BloomGeneric(middle, Vector2.Zero, Steel * 0.7f, 0.62f, 10));

            for (int i = 0; i < 14; i++)
            {
                float length = Main.rand.NextFloat(24f, 42f);

                PADParticleHandler.SpawnParticle(new SparkShard(middle + Main.rand.NextVector2Circular(size * 0.25f, size * 0.25f),
                    along.RotatedByRandom(0.7f) * Main.rand.NextFloat(7f, 15f), Sparks[Main.rand.Next(Sparks.Length)],
                    length * 0.5f, 18f, length, Main.rand.Next(14, 22), falls: true));
            }
        }

        /// <summary>
        /// Pushed back, this tick, by <paramref name="step"/>: on the ground, the ground's own
        /// dust off its feet, rolling the way it came from; in the air, a pale streak left
        /// behind it now and then.
        /// </summary>
        public static void Skid(NPC npc, Vector2 step)
        {
            if (Main.dedServ)
                return;

            float speed = step.Length();

            if (speed < 1.2f)
                return;

            Point under = new Vector2(npc.Center.X, npc.Bottom.Y + 4f).ToTileCoordinates();

            if (!npc.noGravity && Stands(under))
            {
                if (!Main.rand.NextBool(2))
                    return;

                Color ground = GroundWave.FogColour(under.X, under.Y);
                Vector2 at = new Vector2(npc.Center.X - Math.Sign(step.X) * npc.width * 0.5f + Main.rand.NextFloat(-4f, 4f), npc.Bottom.Y - Main.rand.NextFloat(0f, 6f));
                Vector2 push = new Vector2(step.X * Main.rand.NextFloat(0.25f, 0.5f), -Main.rand.NextFloat(0.3f, 1.3f));

                PADParticleHandler.SpawnParticle(new DustFog(at, push, ground, Main.rand.Next(36, 60),
                    Main.rand.NextFloat(0.45f, 0.75f), Main.rand.NextFloat(0.26f, 0.38f)));
                return;
            }

            if (!Main.rand.NextBool(3))
                return;

            Vector2 from = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
            float streak = MathHelper.Clamp(speed * 3.5f, 12f, 30f);

            PADParticleHandler.SpawnParticle(new SparkShard(from, -step * 0.25f, Pale * 0.7f, streak * 0.6f, 10f, streak,
                Main.rand.Next(8, 13), spine: false));
        }

        /// <summary>Over the middle of its head: where the star and the glints go.</summary>
        private static Vector2 Crown(NPC npc) => new Vector2(npc.Center.X, npc.position.Y - 8f);

        /// <summary>Whether this tile is ground to stand on.</summary>
        private static bool Stands(Point tile)
        {
            if (!WorldGen.InWorld(tile.X, tile.Y, 2))
                return false;

            Tile t = Main.tile[tile.X, tile.Y];
            return t.HasUnactuatedTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);
        }
    }
}
