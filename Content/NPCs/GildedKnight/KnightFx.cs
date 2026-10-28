using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// How the knight's moments look and sound, client side: the light, the sparks, the gold leaf
    /// coming off him, the dust and leaves of the willow flat. Everything gold: the kingdom's own
    /// colour (by request, "his theme is yellowish js like the kingdom"), the Sun's warm and in his
    /// second half on fire, the Grace's pale and in his second half radiant.
    /// </summary>
    internal static class KnightFx
    {
        // ---- the palette ----

        public static readonly Color Gold = new Color(255, 196, 92);
        public static readonly Color Pale = new Color(255, 240, 196);
        public static readonly Color White = new Color(255, 252, 236);
        public static readonly Color Fire = new Color(255, 142, 46);
        public static readonly Color Ember = new Color(255, 168, 64);
        public static readonly Color Deep = new Color(196, 110, 30);

        /// <summary>The light of a blade, first half and second: the Sun warm gold and then fire, the Grace pale gold and then white.</summary>
        public static Color BladeLight(int blade, bool second) =>
            blade == KnightRig.Sun ? (second ? Fire : Gold) : (second ? White : Pale);

        private static bool Off => Main.dedServ;

        private static void Spawn(PADParticle particle) => PADParticleHandler.SpawnParticle(particle);

        private static Vector2 Rand(float r) => Main.rand.NextVector2Circular(r, r);

        /// <summary>Within this of this machine's player, what shakes or lights the whole screen is felt in full; past Far, not at all.</summary>
        private const float NearDistance = 1300f;
        private const float FarDistance = 2600f;

        /// <summary>How much of something at this place reaches this machine's screen: 1 near, 0 far off.</summary>
        public static float Reach(Vector2 at)
        {
            if (Off)
                return 0f;

            float d = Vector2.Distance(Main.LocalPlayer.Center, at);
            return MathHelper.Clamp(1f - (d - NearDistance) / (FarDistance - NearDistance), 0f, 1f);
        }

        /// <summary>Near enough for the whole screen to answer to it at all.</summary>
        public static bool Near(Vector2 at) => Reach(at) > 0f;

        /// <summary>The screen shaken by something at this place, as much as reaches this machine's player.</summary>
        public static void Shake(Vector2 at, float power, int ticks)
        {
            float r = Reach(at);

            if (r > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(power * r, ticks);
        }

        /// <summary>The whole screen lit by something at this place, as much as reaches this machine's player.</summary>
        public static void Flash(Vector2 at, Color colour, float strength)
        {
            float r = Reach(at);

            if (r > 0f)
                Cinematic.Flash(colour, strength * r);
        }

        // ============================================================
        // SMALL THINGS
        // ============================================================

        /// <summary>A bloom of light, added, swelling and fading.</summary>
        public static void Bloom(Vector2 at, Color colour, float scale, int life, Vector2? velocity = null)
        {
            if (Off)
                return;

            Spawn(new SoftGlow(at, velocity ?? Vector2.Zero, colour, scale, life));
        }

        /// <summary>A star of light where something catches it.</summary>
        public static void Glint(Vector2 at, float size, int life, Color? colour = null)
        {
            if (Off)
                return;

            Spawn(new CrownGlint(at, size, life, colour ?? White));
        }

        /// <summary>Sparks thrown off a point, fanned about a way.</summary>
        public static void Sparks(Vector2 at, Vector2 way, int count, Color colour, float speed = 6f, float spread = 0.9f, bool falls = true)
        {
            if (Off)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 v = way.SafeNormalize(Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-spread, spread)) * speed * Main.rand.NextFloat(0.5f, 1.3f);
                Spawn(new SparkShard(at, v, colour, Main.rand.NextFloat(6f, 12f), Main.rand.NextFloat(1.2f, 2f), Main.rand.NextFloat(14f, 26f), Main.rand.Next(14, 26), falls));
            }
        }

        /// <summary>Flecks of fire thrown about a point.</summary>
        public static void Embers(Vector2 at, int count, float spread, float speed, Color? colour = null, float lift = 0.05f)
        {
            if (Off)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 p = at + Rand(spread);
                Vector2 v = Main.rand.NextVector2Circular(speed, speed) + new Vector2(0f, -speed * 0.4f);
                Spawn(new Particles.Ember(p, v, colour ?? Ember, Main.rand.NextFloat(0.5f, 1.1f), Main.rand.Next(26, 52), lift));
            }
        }

        /// <summary>Motes of gold light drifting up about a point.</summary>
        public static void Motes(Vector2 at, int count, float spread, Color? tint = null)
        {
            if (Off)
                return;

            for (int i = 0; i < count; i++)
                Spawn(new GraceMote(at + Rand(spread), tint ?? Gold, Main.rand.Next(60, 120)));
        }

        /// <summary>Gold leaf coming off him: thrown out, and falling slowly, turning.</summary>
        public static void Gilt(Vector2 at, int count, float spread, Vector2 throwWay)
        {
            if (Off)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 v = throwWay * Main.rand.NextFloat(0.4f, 1.2f) + Main.rand.NextVector2Circular(1.4f, 1.4f) + new Vector2(0f, -1f);
                Spawn(new GiltFlake(at + Rand(spread), v, Main.rand.Next(70, 130)));
            }
        }

        /// <summary>A ring of light opening out from a point.</summary>
        public static void Ring(Vector2 at, float from, float to, int life, Color colour, Entity follow = null)
        {
            if (Off)
                return;

            Spawn(new ParryRing(at, from, to, life, colour, follow));
        }

        /// <summary>Dust where a foot or a blow comes down on the willow flat, and its leaves stirred up.</summary>
        public static void Dust(Vector2 feet, float strength)
        {
            if (Off)
                return;

            int n = (int)(3 + 5 * strength);

            for (int i = 0; i < n; i++)
            {
                Vector2 p = feet + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f);
                Dust d = Terraria.Dust.NewDustPerfect(p, DustID.Smoke, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.1f, -0.2f)) * strength,
                    140, new Color(176, 158, 130), Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = true;
            }

            Leaves(feet, (int)(1 + 3 * strength), 2.2f * strength);
        }

        /// <summary>Fallen willow leaves (orange, yellow) thrown up off the ground and fluttering down.</summary>
        public static void Leaves(Vector2 at, int count, float speed)
        {
            if (Off)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 v = new Vector2(Main.rand.NextFloat(-1f, 1f) * speed, -Main.rand.NextFloat(0.6f, 1.4f) * speed);

                // The game's own falling leaf of the yellow willow (the flat's trees), the right
                // weight and flutter for it; now and then one of ash.
                int leaf = Main.rand.NextBool(5) ? GoreID.TreeLeaf_TreeAsh : GoreID.TreeLeaf_VanityTreeYellowWillow;
                Gore.NewGorePerfect(Terraria.Entity.GetSource_None(), at + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f), v, leaf, Main.rand.NextFloat(0.7f, 1f));
            }
        }

        // ============================================================
        // HIS CUTS
        // ============================================================

        /// <summary>A cut of his landed on someone: sparks along its way, a bloom of the blade's light, a slash mark in the air.</summary>
        public static void Cut(Vector2 at, Vector2 along, int blade, bool second, bool heavy)
        {
            if (Off)
                return;

            Color light = BladeLight(blade, second);

            Sparks(at, along, heavy ? 14 : 9, light, heavy ? 8f : 6f, 0.7f);
            Bloom(at, light * 0.8f, heavy ? 0.55f : 0.38f, 16);
            Spawn(new SlashStreak(at, along, heavy ? 46f : 34f, heavy ? 9f : 6f, 14));

            if (blade == KnightRig.Sun && second)
                Embers(at, 10, 8f, 3f);

            KnightFx.Shake(at, heavy ? 7f : 4f, heavy ? 14 : 9);
        }

        /// <summary>He is hit: gold leaf comes off where the blow landed, a few sparks off the plate.</summary>
        public static void Hit(NPC npc, int direction, bool hard)
        {
            if (Off)
                return;

            Vector2 at = npc.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-24f, 18f));

            Gilt(at, hard ? 4 : Main.rand.Next(1, 3), 8f, new Vector2(direction * 1.6f, -1.2f));

            if (hard || Main.rand.NextBool(3))
                Sparks(at, new Vector2(direction, -0.4f), hard ? 6 : 3, Gold, 4.5f, 1f);
        }

        /// <summary>He is turned aside: the clash, the ward of gold about him, sparks off the blade that was met.</summary>
        public static void Ward(GildedKnight knight)
        {
            if (Off)
                return;

            NPC npc = knight.NPC;
            KnightSounds.Play(KnightSounds.Clash, npc.Center);
            KnightSounds.Play(KnightSounds.Ward, npc.Center);

            Ring(npc.Center, 18f, 70f, 24, Gold * 0.9f, npc);
            Bloom(npc.Center, Gold * 0.5f, 0.7f, 24);
            Gilt(npc.Center + new Vector2(0f, -20f), 5, 10f, new Vector2(-knight.Faces * 1.4f, -1f));
        }

        // ============================================================
        // HIS SCENE
        // ============================================================

        /// <summary>Both swords leave their scabbards: the ring of them drawn, a glint at each mouth, a gleam run up each blade.</summary>
        public static void Drawn(GildedKnight knight)
        {
            if (Off)
                return;

            KnightRig.Frame f = KnightRig.Build(knight.Pose, knight.Feet, knight.Faces, null, knight.SunSwing, knight.GraceSwing);

            KnightSounds.Play(KnightSounds.Unsheathe1, f.SunGrip);
            KnightSounds.Play(KnightSounds.Unsheathe2, f.GraceGrip);

            Glint(f.SunGuard, 22f, 18, Gold);
            Glint(f.GraceGuard, 22f, 18, Pale);
            Sparks(f.SunGuard, f.SunTip - f.SunGuard, 6, Gold, 3.5f, 0.5f, false);
            Sparks(f.GraceGuard, f.GraceTip - f.GraceGuard, 6, Pale, 3.5f, 0.5f, false);
        }

        /// <summary>The swords crossed before his face: the light gathering where they cross, a star, a ring.</summary>
        public static void Crossed(GildedKnight knight)
        {
            if (Off)
                return;

            KnightRig.Frame f = KnightRig.Build(knight.Pose, knight.Feet, knight.Faces, null, knight.SunSwing, knight.GraceSwing);
            Vector2 at = Crossing(f);

            KnightSounds.Play(KnightSounds.Point, at);
            Glint(at, 54f, 40, White);
            Bloom(at, Gold, 0.9f, 50);
            Ring(at, 10f, 110f, 34, Gold * 0.8f);
            Motes(at, 14, 26f, Pale);

            KnightFx.Flash(at, new Color(255, 214, 140), 0.12f);
        }

        /// <summary>Where the two blades cross (or come nearest), in the world.</summary>
        public static Vector2 Crossing(KnightRig.Frame f)
        {
            Vector2 a = f.SunGrip, b = f.SunTip, c = f.GraceGrip, d = f.GraceTip;
            Vector2 r = b - a, s = d - c;
            float cross = r.X * s.Y - r.Y * s.X;

            if (Math.Abs(cross) < 0.001f)
                return (a + c) * 0.5f;

            float t = ((c.X - a.X) * s.Y - (c.Y - a.Y) * s.X) / cross;
            return a + r * MathHelper.Clamp(t, 0f, 1f);
        }

        /// <summary>The swords parted: each sweeps its arc (the trail is the drawing's), a ring of wind off him.</summary>
        public static void Parting(GildedKnight knight)
        {
            if (Off)
                return;

            KnightSounds.Play(KnightSounds.Swing, knight.NPC.Center);
            Leaves(knight.Feet, 6, 3f);
        }

        /// <summary>The Sun levelled at them: a glint runs down the blade to its point and rings there.</summary>
        public static void Pointed(GildedKnight knight)
        {
            if (Off)
                return;

            KnightRig.Frame f = KnightRig.Build(knight.Pose, knight.Feet, knight.Faces, null, knight.SunSwing, knight.GraceSwing);

            KnightSounds.Play(KnightSounds.Point, f.SunTip);
            Glint(f.SunTip, 40f, 46, White);
            Bloom(f.SunTip, Gold * 0.8f, 0.45f, 40);
            Ring(f.SunTip, 4f, 46f, 30, Gold * 0.7f);
            knight.Gleam(KnightRig.Sun, 40);
        }

        // ============================================================
        // HIS END
        // ============================================================

        /// <summary>The Sun, let fall, comes down on the ground: it rings once, a few sparks.</summary>
        public static void SwordDown(Vector2 grip, float angle)
        {
            if (Off)
                return;

            KnightSounds.Play(KnightSounds.Clash with { Volume = 0.5f, Pitch = -0.2f }, grip);
            Sparks(grip + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 40f, -Vector2.UnitY, 6, Gold, 3f, 1.2f);
            Dust(grip + new Vector2((float)Math.Cos(angle) * 30f, 4f), 0.5f);
        }

        /// <summary>A tick of his coming apart into light: gold motes and leaf lifting off him, light about him.</summary>
        public static void Dissolving(GildedKnight knight, float fade)
        {
            if (Off)
                return;

            NPC npc = knight.NPC;
            float rate = 0.3f + 1.4f * (float)Math.Sin(fade * MathHelper.Pi);

            if (Main.rand.NextFloat() < rate)
                Spawn(new GraceMote(npc.Center + new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-36f, 30f)), Pale, Main.rand.Next(70, 130)));

            if (Main.rand.NextFloat() < rate * 0.5f)
                Gilt(npc.Center + new Vector2(0f, -10f), 1, 16f, new Vector2(0f, -1.6f));

            if (Main.rand.NextFloat() < rate * 0.6f)
                Spawn(new SparkShard(npc.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-30f, 30f)), new Vector2(0f, -Main.rand.NextFloat(1.5f, 3.5f)),
                    Pale, 6f, 1.3f, 18f, Main.rand.Next(20, 40), false));

            Lighting.AddLight(npc.Center, new Vector3(1f, 0.86f, 0.55f) * (0.6f + 1.2f * fade));
        }

        /// <summary>Nothing of him left but light: a flash, a pillar of it, rays, a ring, motes rising.</summary>
        public static void Perished(GildedKnight knight)
        {
            if (Off)
                return;

            NPC npc = knight.NPC;
            Vector2 at = npc.Center;

            KnightSounds.Play(KnightSounds.Flash, at);
            KnightFx.Flash(at, new Color(255, 230, 170), 0.32f);
            Bloom(at, White, 2.2f, 70);
            Bloom(at, Gold, 1.4f, 90);
            Glint(at, 90f, 60, White);
            Ring(at, 20f, 260f, 50, Gold);
            Ring(at, 10f, 160f, 40, White * 0.7f);

            for (int i = 0; i < 40; i++)
                Spawn(new GraceMote(at + Rand(30f), i % 3 == 0 ? White : Gold, Main.rand.Next(90, 180)));

            for (int i = 0; i < 18; i++)
            {
                Vector2 v = new Vector2(0f, -1f).RotatedBy(Main.rand.NextFloat(-1.2f, 1.2f)) * Main.rand.NextFloat(3f, 9f);
                Spawn(new SparkShard(at, v, Pale, 10f, 2f, 40f, Main.rand.Next(30, 60), false));
            }

            KnightFx.Shake(at, 6f, 30);
        }
    }
}
