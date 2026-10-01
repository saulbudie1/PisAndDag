using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The flame of the Flamethrower and the Elf Melter (both fire ProjectileID.Flames),
    /// drawn as fire rather than as vanilla's sprite: a burning jet that leaves the nozzle
    /// thin, blue at the root and white-yellow, and widens as it goes into orange billows
    /// with red edges and soot round them, rising as it cools, and ends in dark smoke that
    /// carries on and climbs after the flame is gone. Embers come off it, it lights what
    /// it passes, the nozzle flares as each burst leaves it, and what it hits throws sparks.
    ///
    /// What the flame does is vanilla's and is left alone: its reach, its speed, its
    /// damage, its hitbox that grows as it goes, what it sets on fire. Only vanilla's own
    /// AI (AI_193_Flamethrower) is taken over, because all it does besides keeping the
    /// flame's clock is throw dust, and the dust is what this replaces; the clock is kept
    /// here exactly as vanilla keeps it.
    ///
    /// HOW IT IS DRAWN. Each flame is one burst of fuel. Vanilla draws a burst as a streak
    /// back along its path, which is what joins one burst to the next into a stream; this
    /// does the same, as a row of billows along the streak. Each billow is given the age
    /// of the fuel at that point (younger toward the nozzle) and everything about it is
    /// read off that age: its size, its colour, how far it has risen, how thick the soot
    /// round it is. Where each billow sits across the stream, how it is turned and which
    /// of the four noise frames it is, comes from the flame's identity, so every machine
    /// draws the same fire and it does not boil from frame to frame.
    ///
    /// Three layers a billow, drawn in this order: the soot (over what is behind, dark),
    /// the outer flame (red, large) and the inner flame (the ramp's colour, smaller), both
    /// adding most of their light and covering a little of what is behind them so the fire
    /// keeps its colour against a bright sky; and, while the fuel is young, a hot core that
    /// only adds. Designed against a model of these blend sums before it was written.
    ///
    /// ai[0] 1 is vanilla's cold (blue) flame, and is drawn in blue.
    ///
    /// Textures: FlameBillow.png beside this file, and the FlameSmoke particle's sheet. Both
    /// four frames of 64 px, one above the other, white, stored premultiplied.
    /// </summary>
    public class FlamethrowerFlames : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.Flames;

        // ---- vanilla's clock (AI_193_Flamethrower) ------------------------------------------

        /// <summary>Updates the flame lives before it slows, and the updates it slows over (three updates a tick).</summary>
        private const int Burn = 60;
        private const int Dying = 12;
        private const int Life = Burn + Dying;

        // ---- the look -------------------------------------------------------------------------

        /// <summary>Space between billows along a burst's streak, in pixels.</summary>
        private const float Step = 8f;

        /// <summary>The streak reaches back this many updates' travel (vanilla's).</summary>
        private const int StreakUpdates = 20;

        /// <summary>How far the fire rises by the time it is spent, in pixels (on the square of its age).</summary>
        private const float Rise = 60f;

        /// <summary>How far a billow may sit off the line of the stream: at the nozzle, and more by the end.</summary>
        private const float SpreadNozzle = 4f;
        private const float SpreadEnd = 34f;

        /// <summary>A billow's width at the nozzle, and what it grows by.</summary>
        private const float SizeNozzle = 14f;
        private const float SizeGrowth = 150f;

        /// <summary>How much of what is behind the flame layers cover, per unit of their light.</summary>
        private const float Cover = 0.45f;

        /// <summary>The colour of the fire over the fuel's age (0 to 1), and how strong it is there.</summary>
        private static readonly (float at, Vector3 colour, float strength)[] Ramp =
        {
            (0.00f, new Vector3(0.45f, 0.60f, 1.00f), 0.00f),
            (0.05f, new Vector3(0.55f, 0.70f, 1.00f), 0.55f),
            (0.12f, new Vector3(1.00f, 0.95f, 0.80f), 1.00f),
            (0.30f, new Vector3(1.00f, 0.78f, 0.35f), 1.00f),
            (0.50f, new Vector3(1.00f, 0.50f, 0.14f), 0.90f),
            (0.72f, new Vector3(0.85f, 0.24f, 0.06f), 0.60f),
            (0.90f, new Vector3(0.45f, 0.10f, 0.03f), 0.25f),
            (1.00f, new Vector3(0.20f, 0.04f, 0.01f), 0.00f),
        };

        /// <summary>The same for vanilla's cold flame.</summary>
        private static readonly (float at, Vector3 colour, float strength)[] ColdRamp =
        {
            (0.00f, new Vector3(0.50f, 0.65f, 1.00f), 0.00f),
            (0.05f, new Vector3(0.65f, 0.85f, 1.00f), 0.55f),
            (0.12f, new Vector3(0.85f, 0.97f, 1.00f), 1.00f),
            (0.30f, new Vector3(0.45f, 0.80f, 1.00f), 1.00f),
            (0.50f, new Vector3(0.25f, 0.55f, 1.00f), 0.90f),
            (0.72f, new Vector3(0.14f, 0.30f, 0.85f), 0.60f),
            (0.90f, new Vector3(0.06f, 0.12f, 0.45f), 0.25f),
            (1.00f, new Vector3(0.02f, 0.04f, 0.20f), 0.00f),
        };

        /// <summary>The red the outer flame is pulled toward, and the pale gold of the hot core.</summary>
        private static readonly Vector3 Edge = new Vector3(0.90f, 0.18f, 0.04f);
        private static readonly Vector3 ColdEdge = new Vector3(0.10f, 0.25f, 0.95f);
        private static readonly Vector3 Core = new Vector3(1.00f, 0.97f, 0.85f);
        private static readonly Vector3 ColdCore = new Vector3(0.90f, 0.98f, 1.00f);

        /// <summary>The soot's colour at full thickness, and the smoke's.</summary>
        private static readonly Vector3 Soot = new Vector3(0.05f, 0.035f, 0.03f);
        private static readonly Color Smoke = new Color(19, 16, 15);
        private static readonly Color ColdSmoke = new Color(16, 18, 26);

        private static Asset<Texture2D> billow;
        private static Asset<Texture2D> soot;

        /// <summary>The four noise frames, 64 px each, one above the other.</summary>
        private static readonly Rectangle[] Frames =
        {
            new Rectangle(0, 0, 64, 64),
            new Rectangle(0, 64, 64, 64),
            new Rectangle(0, 128, 64, 64),
            new Rectangle(0, 192, 64, 64),
        };

        /// <summary>No more billows than this to a burst, whatever has been done to its speed.</summary>
        private const int MostBillows = 48;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            billow = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Ranged/FlameBillow");
            soot = ModContent.Request<Texture2D>("PisAndDag/Content/Particles/FlameSmoke");
        }

        public override void Unload()
        {
            billow = null;
            soot = null;
        }

        // ---- ai -------------------------------------------------------------------------------

        /// <summary>Vanilla's clock, as vanilla keeps it, and the fire's leavings in place of its dust.</summary>
        public override bool PreAI(Projectile projectile)
        {
            projectile.localAI[0]++;

            if (projectile.localAI[0] >= Life)
                projectile.Kill();

            if (projectile.localAI[0] >= Burn)
                projectile.velocity *= 0.95f;

            if (!Main.dedServ)
                Leavings(projectile);

            return false;
        }

        /// <summary>The flare at the nozzle as a burst leaves it, embers, the smoke it sheds as it dies, and its light.</summary>
        private static void Leavings(Projectile projectile)
        {
            int age = (int)projectile.localAI[0];
            float t = age / (float)Life;
            bool cold = projectile.ai[0] == 1f;
            (Vector3 colour, float strength) = Sample(cold ? ColdRamp : Ramp, t);

            Vector2 heading = projectile.velocity.SafeNormalize(Vector2.UnitX);

            // The nozzle flares as the burst leaves it: a hot blue root and the orange round it.
            if (age == 1)
            {
                Vector2 nozzle = projectile.Center;

                PADParticleHandler.SpawnParticle(new BloomGeneric(nozzle, heading * 0.8f,
                    cold ? new Color(150, 200, 255) : new Color(170, 200, 255), 0.16f, 9, false));
                PADParticleHandler.SpawnParticle(new BloomGeneric(nozzle + heading * 6f, heading * 1.2f,
                    cold ? new Color(90, 160, 255) : new Color(255, 150, 60), 0.3f, 12));
            }

            // Embers, from the part of the flame that is burning hardest.
            if (t > 0.15f && t < 0.85f && Main.rand.NextFloat() < 0.06f)
            {
                Vector2 at = Lifted(projectile.Center, t) + Main.rand.NextVector2Circular(10f + 20f * t, 10f + 20f * t);
                Vector2 thrown = projectile.velocity * 3f * 0.3f + Main.rand.NextVector2Circular(1.5f, 1.5f) - new Vector2(0f, 1f);

                PADParticleHandler.SpawnParticle(new Ember(at, thrown,
                    cold ? new Color(170, 220, 255) : new Color(255, 190, 90), Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(30, 60), 0.05f));
            }

            // The smoke it sheds as it burns out, which outlives it.
            if (age == 45 || age == 54 || age == 63)
            {
                float side = (Main.rand.NextFloat() * 2f - 1f) * (SpreadNozzle + SpreadEnd * t);
                Vector2 at = Lifted(projectile.Center, t) + new Vector2(-heading.Y, heading.X) * side;
                Vector2 thrown = projectile.velocity * 3f * 0.12f + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1f));

                PADParticleHandler.SpawnParticle(new FlameSmoke(at, thrown, cold ? ColdSmoke : Smoke, Main.rand.Next(70, 111),
                    90f, 240f, 0.42f));
            }

            // It lights what it passes.
            if (strength > 0f)
            {
                Vector3 light = colour * strength * 1.1f;
                Lighting.AddLight(Lifted(projectile.Center, t), light.X, light.Y, light.Z);
            }
        }

        /// <summary>Where the fire of a given age hangs over the line it was thrown along: hot air rises.</summary>
        private static Vector2 Lifted(Vector2 at, float t) => at - new Vector2(0f, Rise * t * t);

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            bool cold = projectile.ai[0] == 1f;

            for (int i = 0; i < 5; i++)
            {
                Vector2 thrown = projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(1.2f) * Main.rand.NextFloat(1.5f, 4f)
                    - new Vector2(0f, 1f);

                PADParticleHandler.SpawnParticle(new Ember(target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f),
                    thrown, cold ? new Color(170, 220, 255) : new Color(255, 200, 100), Main.rand.NextFloat(0.7f, 1.1f), Main.rand.Next(24, 44), 0.06f));
            }
        }

        // ---- drawn ----------------------------------------------------------------------------

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            Texture2D fire = billow?.Value;
            Texture2D smoke = soot?.Value;

            if (fire != null)
                DrawFire(projectile, fire, smoke);

            return false;
        }

        private static void DrawFire(Projectile projectile, Texture2D fire, Texture2D smoke)
        {
            float age = projectile.localAI[0];

            if (age >= Life)
                return;

            bool cold = projectile.ai[0] == 1f;
            var ramp = cold ? ColdRamp : Ramp;

            float speed = projectile.velocity.Length();
            Vector2 heading = projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 across = new Vector2(-heading.Y, heading.X);
            float along = heading.ToRotation();

            // Vanilla's streak: back along the path, as far as the last twenty updates took it.
            float streak = speed * Math.Min(age, StreakUpdates);
            int count = Math.Clamp((int)(streak / Step), 1, MostBillows);

            float ticks = Main.GlobalTimeWrappedHourly * 60f;
            int id = projectile.identity;

            Vector2 origin = new Vector2(32f, 32f);

            // Worked out once, drawn in three passes so the soot is under all the flame.
            Span<Billow> billows = stackalloc Billow[count + 1];

            for (int k = 0; k <= count; k++)
            {
                float back = streak * k / count;
                float fuel = speed > 0.01f ? Math.Max(0f, age - back / speed) : age;
                float t = fuel / Life;

                float side = (Hash(id, k * 5) * 2f - 1f) * (SpreadNozzle + SpreadEnd * t);
                Vector2 at = projectile.Center - heading * back + across * side;

                billows[k] = new Billow
                {
                    T = t,
                    At = Lifted(at, t),
                    Turn = Hash(id, k * 5 + 1) * MathHelper.TwoPi,
                    Spin = (Hash(id, k * 5 + 2) < 0.5f ? -1f : 1f) * MathHelper.Lerp(0.03f, 0.08f, Hash(id, k * 5 + 3)),
                    Frame = (int)(Hash(id, k * 5 + 4) * 4f) & 3,
                    Size = MathHelper.Lerp(0.8f, 1.2f, Hash(id + 7919, k)),
                };
            }

            // The soot round the older flame, over what is behind it.
            if (smoke != null)
            {
                for (int k = 0; k <= count; k++)
                {
                    Billow b = billows[k];
                    float soot = SootAt(b.T);

                    if (soot <= 0f)
                        continue;

                    float scale = Width(b.T) * 1.15f * b.Size / 64f;
                    Color colour = new Color(Soot.X * soot, Soot.Y * soot, Soot.Z * soot, soot);

                    Main.EntitySpriteDraw(smoke, b.At - new Vector2(0f, 10f * b.T) - Main.screenPosition, Frames[b.Frame], colour,
                        b.Turn + ticks * b.Spin * 0.5f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            for (int k = 0; k <= count; k++)
            {
                Billow b = billows[k];
                (Vector3 colour, float strength) = Sample(ramp, b.T);

                if (strength <= 0f)
                    continue;

                float width = Width(b.T) * b.Size / 64f;
                float stretch = 1f + 0.9f * Math.Max(0f, 1f - b.T / 0.35f);
                float turn = b.Turn + ticks * b.Spin;
                Vector2 at = b.At - Main.screenPosition;

                // Outer: redder, larger, dimmer.
                Vector3 outer = Vector3.Lerp(colour, cold ? ColdEdge : Edge, 0.55f);
                float k1 = 0.22f * strength;
                Main.EntitySpriteDraw(fire, at, Frames[b.Frame], Light(outer, k1, Cover * k1), along + turn * 0.35f, origin,
                    new Vector2(width * stretch, width), SpriteEffects.None, 0f);

                // Inner: the ramp's own colour, smaller, brighter, turning the other way.
                float k2 = 0.30f * strength;
                Main.EntitySpriteDraw(fire, at, Frames[(b.Frame + 2) & 3], Light(colour, k2, Cover * k2), along - turn * 0.35f, origin,
                    new Vector2(width * 0.62f * stretch, width * 0.62f), SpriteEffects.None, 0f);

                // The hot core, while the fuel is young: light only.
                if (b.T < 0.5f)
                {
                    float k3 = 0.28f * strength * (1f - b.T / 0.5f);
                    Main.EntitySpriteDraw(fire, at, Frames[(b.Frame + 1) & 3], Light(cold ? ColdCore : Core, k3, 0f), along, origin,
                        new Vector2(width * 0.4f * stretch, width * 0.32f), SpriteEffects.None, 0f);
                }
            }
        }

        private struct Billow
        {
            public float T;
            public Vector2 At;
            public float Turn;
            public float Spin;
            public int Frame;
            public float Size;
        }

        /// <summary>A premultiplied colour: this much light of this colour, covering this much of what is behind.</summary>
        private static Color Light(Vector3 colour, float amount, float cover) =>
            new Color(colour.X * amount, colour.Y * amount, colour.Z * amount, cover);

        /// <summary>A billow's width in pixels, for the fuel's age.</summary>
        private static float Width(float t) => SizeNozzle + SizeGrowth * (1f - (float)Math.Pow(1f - t, 1.5f));

        /// <summary>How thick the soot round the flame is, for the fuel's age.</summary>
        private static float SootAt(float t)
        {
            if (t < 0.35f)
                return 0f;

            float soot = Math.Min(1f, (t - 0.35f) / 0.4f) * 0.6f;

            if (t > 0.85f)
                soot *= Math.Max(0f, 1f - (t - 0.85f) / 0.15f);

            return soot;
        }

        /// <summary>The ramp's colour and strength at this age.</summary>
        private static (Vector3 colour, float strength) Sample((float at, Vector3 colour, float strength)[] ramp, float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);

            for (int i = 0; i < ramp.Length - 1; i++)
            {
                var a = ramp[i];
                var b = ramp[i + 1];

                if (t <= b.at)
                {
                    float u = (t - a.at) / (b.at - a.at);
                    return (Vector3.Lerp(a.colour, b.colour, u), MathHelper.Lerp(a.strength, b.strength, u));
                }
            }

            return (ramp[ramp.Length - 1].colour, 0f);
        }

        /// <summary>A number from 0 to 1 that is always the same for the same two numbers.</summary>
        private static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
