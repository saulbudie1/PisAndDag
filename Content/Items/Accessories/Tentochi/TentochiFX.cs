using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Made By Tentochi (by request: "an engineer corp from elsewhere that has crazy cybernetic implants and weapons; cyborg
    /// is of that origin", its colour #d62747, "inspired by cyberpunk so make it like that"): the look of everything of
    /// theirs, in one place. Their red; its hot white; the cyan a picture splits into beside it, as a screen does when its
    /// colours slip; amber for metal heated. Light laid in squares on the 2x2 grid (PixelChip), bars torn across the air
    /// (GlitchBar), rings thrown out hard or soft (CyberRing), the mod's sparks and smoke.
    ///
    /// Every one here draws nothing on a server and asks the particle system whether there is room first.
    /// </summary>
    public static class TentochiFX
    {
        /// <summary>Their red: #d62747, by request.</summary>
        public static readonly Color Red = new Color(214, 39, 71);

        /// <summary>Their red at its hottest, nearly white.</summary>
        public static readonly Color Hot = new Color(255, 196, 206);

        /// <summary>The colour a picture splits into beside their red.</summary>
        public static readonly Color Cyan = new Color(64, 232, 255);

        /// <summary>Metal heated: brake sparks, a blast's edge.</summary>
        public static readonly Color Amber = new Color(255, 176, 72);

        /// <summary>A light to add under the game's alpha blend: the colour, this strong, with no alpha.</summary>
        public static Color Glow(Color colour, float strength)
        {
            Color c = colour * strength;
            c.A = 0;
            return c;
        }

        private static bool Room(int count) => !Main.dedServ && PADParticleHandler.FreeSlots > count;

        private static float R(float min, float max) => Main.rand.NextFloat(min, max);

        private static Color Split(int i) => i % 3 == 0 ? Cyan : i % 3 == 1 ? Red : Hot;

        /// <summary>
        /// A point by the wearer's soles: so far along (world x) and so far up from the soles toward the head, whichever
        /// way their gravity is (turned over, the soles are at the top of them).
        /// </summary>
        public static Vector2 Soles(Player player, float along, float up) =>
            (player.gravDir == -1f ? player.Top : player.Bottom) + new Vector2(along, -up * player.gravDir);

        // ---- the exoskeleton's dash (Exo Air I and II, AccessoryDash) --------------------------------------------

        /// <summary>A dash begun: the jets at the ankles lit, sparks thrown back along the ground, the air where they stood torn.</summary>
        public static void DashStart(Player player, int dir, bool second)
        {
            if (!Room(40))
                return;

            Vector2 feet = Soles(player, -dir * 4f, 6f);
            PADParticleHandler.SpawnParticle(new SoftGlow(feet, Vector2.Zero, Red, second ? 0.45f : 0.35f, 14));
            PADParticleHandler.SpawnParticle(new SoftGlow(player.Center, Vector2.Zero, Cyan * 0.5f, 0.3f, 8, lights: false));

            for (int i = 0; i < (second ? 14 : 10); i++)
            {
                Vector2 velocity = new Vector2(-dir * R(4f, 11f), R(-2.6f, 0.4f) * player.gravDir);
                PADParticleHandler.SpawnParticle(new SparkShard(feet, velocity, i % 3 == 0 ? Hot : Red, 6f, 1.6f, 14f, Main.rand.Next(10, 16), falls: true));
            }

            for (int i = 0; i < (second ? 8 : 5); i++)
            {
                Vector2 at = player.Center + new Vector2(R(-10f, 10f), R(-22f, 22f));
                PADParticleHandler.SpawnParticle(new GlitchBar(at, Vector2.Zero, Split(i), R(10f, 28f), 2f, Main.rand.Next(10, 18), 4f));
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2 at = player.position + new Vector2(R(0f, player.width), R(0f, player.height));
                PADParticleHandler.SpawnParticle(new PixelChip(at, new Vector2(-dir * R(1f, 4f), R(-1.5f, 1.5f)), Split(i), 2f, Main.rand.Next(14, 24)));
            }

            SoundEngine.PlaySound(TentochiSounds.Dash with { Pitch = second ? -0.08f : 0.05f }, player.Center);
        }

        /// <summary>A dash going: the jets burning back from the feet, the air behind tearing, squares of light shed.</summary>
        public static void DashTick(Player player, int dir, int tick, bool second)
        {
            if (!Room(12))
                return;

            Vector2 feet = Soles(player, -dir * 6f, 8f);
            PADParticleHandler.SpawnParticle(new SparkShard(feet, new Vector2(-dir * R(5f, 9f), R(-0.6f, 0.6f)), tick % 2 == 0 ? Hot : Red, 5f, 2f, 12f, 8));

            if (tick % 2 == 0)
            {
                Vector2 at = player.Center + new Vector2(-dir * R(6f, 24f), R(-20f, 18f));
                PADParticleHandler.SpawnParticle(new GlitchBar(at, Vector2.Zero, Split(tick), R(8f, 30f), 2f, Main.rand.Next(8, 14), 3f));
            }

            Vector2 chip = player.Center + new Vector2(-dir * R(4f, 14f), R(-18f, 18f));
            PADParticleHandler.SpawnParticle(new PixelChip(chip, new Vector2(-dir * R(0.5f, 2f), 0f), Split(tick + 1), 2f, Main.rand.Next(10, 20)));

            float strength = second ? 0.55f : 0.45f;
            Lighting.AddLight(player.Center, Red.R / 255f * strength, Red.G / 255f * strength, Red.B / 255f * strength);
        }

        /// <summary>A dash spent: sparks off the soles as they brake, a ring of light gone out from them.</summary>
        public static void DashStop(Player player, int dir)
        {
            if (!Room(12))
                return;

            for (int i = 0; i < 6; i++)
            {
                Vector2 velocity = new Vector2(dir * R(2f, 5f), R(-3f, -0.5f) * player.gravDir);
                PADParticleHandler.SpawnParticle(new SparkShard(Soles(player, dir * 6f, 2f), velocity, i % 2 == 0 ? Amber : Hot, 4f, 1.4f, 9f, Main.rand.Next(10, 16), falls: true));
            }

            PADParticleHandler.SpawnParticle(new CyberRing(player.Center, Red * 0.8f, 6f, 34f, 2f, 16, 24, 0.4f, 0.05f));
        }

        // ---- the Empress' Blessing's dash (AccessoryDash) ---------------------------------------------------------

        /// <summary>Her blessing's dash begun: a flash of her light, a ring of it, the wearer gone into it.</summary>
        public static void BlessingStart(Player player, int dir)
        {
            if (!Room(30))
                return;

            PADParticleHandler.SpawnParticle(new SoftGlow(player.Center, Vector2.Zero, Color.White * 0.8f, 0.5f, 12));
            PADParticleHandler.SpawnParticle(new SoftGlow(player.Center, Vector2.Zero, EmpressArt.Prism(Main.rand.NextFloat()), 0.7f, 22));
            PADParticleHandler.SpawnParticle(new CyberRing(player.Center, EmpressArt.Pink * 0.8f, 8f, 70f, 14f, 18, 40, 0f, 0f, soft: true));

            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * MathHelper.TwoPi;
                Color colour = EmpressArt.Sparks[i % EmpressArt.Sparks.Length];
                PADParticleHandler.SpawnParticle(new SparkShard(player.Center, angle.ToRotationVector2() * R(3f, 7f), colour, 6f, 2f, 16f, Main.rand.Next(14, 22)));
            }

            SoundEngine.PlaySound(EmpressSounds.Blink with { Volume = 0.7f }, player.Center);
        }

        /// <summary>Her blessing's dash going: a ribbon of her colours laid behind, and her glints in it.</summary>
        public static void BlessingTick(Player player, int dir, int tick)
        {
            if (!Room(8))
                return;

            for (int band = -1; band <= 1; band++)
            {
                Vector2 at = player.Center + new Vector2(-dir * 6f, band * 9f);
                Color colour = EmpressArt.Prism(tick / 14f + band * 0.2f) * (band == 0 ? 0.9f : 0.55f);
                PADParticleHandler.SpawnParticle(new PrismStreak(at, Vector2.Zero, 0f, 46f, band == 0 ? 14f : 8f, colour, 24));
            }

            if (tick % 3 == 0)
            {
                Vector2 at = player.Center + new Vector2(-dir * R(4f, 20f), R(-16f, 16f));
                PADParticleHandler.SpawnParticle(new CrownGlint(at, R(10f, 18f), 18, EmpressArt.Sparks[tick % EmpressArt.Sparks.Length]));
            }

            Color light = EmpressArt.Prism(tick / 14f);
            Lighting.AddLight(player.Center, light.R / 255f * 0.7f, light.G / 255f * 0.7f, light.B / 255f * 0.7f);
        }

        /// <summary>Her blessing's dash spent: the wearer back out of her light, glints scattering.</summary>
        public static void BlessingStop(Player player, int dir)
        {
            if (!Room(12))
                return;

            for (int i = 0; i < 8; i++)
            {
                Vector2 velocity = new Vector2(dir * R(1f, 4f), R(-3f, 3f));
                PADParticleHandler.SpawnParticle(new SparkShard(player.Center, velocity, EmpressArt.Sparks[i % EmpressArt.Sparks.Length], 4f, 1.6f, 10f, Main.rand.Next(12, 18)));
            }

            PADParticleHandler.SpawnParticle(new CrownGlint(player.Center, 26f, 20, Color.White));
            SoundEngine.PlaySound(EmpressSounds.Tingle with { Volume = 0.6f }, player.Center);
        }

        // ---- the Back Rocket (BackRocket, TentochiRocket) ---------------------------------------------------------

        /// <summary>A rocket off the back: the tube's flash, smoke thrown out of it, sparks.</summary>
        public static void Launch(Vector2 at, Vector2 velocity)
        {
            if (!Room(14))
                return;

            Vector2 back = -Vector2.Normalize(velocity);
            PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, Amber, 0.22f, 10));

            for (int i = 0; i < 4; i++)
                PADParticleHandler.SpawnParticle(new FlameSmoke(at, back.RotatedByRandom(0.9f) * R(0.6f, 2f), new Color(90, 88, 96), Main.rand.Next(26, 40), 10f, 26f, 0.5f));

            for (int i = 0; i < 5; i++)
                PADParticleHandler.SpawnParticle(new SparkShard(at, back.RotatedByRandom(0.8f) * R(2f, 5f), i % 2 == 0 ? Amber : Hot, 4f, 1.4f, 8f, Main.rand.Next(8, 12)));

            SoundEngine.PlaySound(TentochiSounds.Launch, at);
        }

        /// <summary>A rocket's blast: a flash, a hard ring and a soft one, sparks and squares of light out, smoke.</summary>
        public static void Blast(Vector2 at)
        {
            if (!Room(36))
                return;

            PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, Amber, 0.55f, 16));
            PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, Red, 0.8f, 22));
            PADParticleHandler.SpawnParticle(new CyberRing(at, Hot, 6f, 52f, 3f, 14, 32));
            PADParticleHandler.SpawnParticle(new CyberRing(at, Red * 0.7f, 4f, 64f, 18f, 18, 32, 0f, 0f, soft: true));

            for (int i = 0; i < 12; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * R(4f, 10f);
                PADParticleHandler.SpawnParticle(new SparkShard(at, velocity, i % 3 == 0 ? Hot : i % 3 == 1 ? Amber : Red, 6f, 1.8f, 16f, Main.rand.Next(12, 18), falls: true));
            }

            for (int i = 0; i < 10; i++)
                PADParticleHandler.SpawnParticle(new PixelChip(at, Main.rand.NextVector2Circular(5f, 5f), Split(i), 2f, Main.rand.Next(16, 28), 0.12f));

            for (int i = 0; i < 5; i++)
                PADParticleHandler.SpawnParticle(new FlameSmoke(at + Main.rand.NextVector2Circular(8f, 8f), Main.rand.NextVector2Circular(1.5f, 1.5f), new Color(70, 64, 72), Main.rand.Next(34, 50), 16f, 46f, 0.55f));

            SoundEngine.PlaySound(TentochiSounds.Blast, at);
        }

        // ---- the Calamity Module (TentochiPlayer) -----------------------------------------------------------------

        /// <summary>Adrenaline let go: a pulse out of the wearer, the air about them torn.</summary>
        public static void Surge(Player player)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(TentochiSounds.Surge, player.Center);

            if (!Room(30))
                return;

            PADParticleHandler.SpawnParticle(new SoftGlow(player.Center, Vector2.Zero, Red, 0.9f, 24));
            PADParticleHandler.SpawnParticle(new CyberRing(player.Center, Hot, 10f, 120f, 3f, 22, 40, 0.35f, 0.04f));
            PADParticleHandler.SpawnParticle(new CyberRing(player.Center, Red * 0.8f, 10f, 150f, 22f, 26, 40, 0f, 0f, soft: true));

            for (int i = 0; i < 12; i++)
            {
                Vector2 at = player.Center + new Vector2(R(-40f, 40f), R(-36f, 36f));
                PADParticleHandler.SpawnParticle(new GlitchBar(at, Vector2.Zero, Split(i), R(12f, 40f), i % 4 == 0 ? 4f : 2f, Main.rand.Next(12, 22), 8f));
            }
        }

        /// <summary>Adrenaline running: heat coming off the wearer in red, and now and then a tear in the air by them.</summary>
        public static void SurgeTick(Player player, int tick)
        {
            if (!Room(8))
                return;

            if (tick % 2 == 0)
            {
                Vector2 at = player.position + new Vector2(R(0f, player.width), R(player.height * 0.3f, player.height));
                PADParticleHandler.SpawnParticle(new Ember(at, new Vector2(R(-0.4f, 0.4f), R(-1.6f, -0.6f)), Red, R(0.5f, 0.9f), Main.rand.Next(20, 34), 0.03f));
            }

            if (tick % 7 == 0)
            {
                Vector2 at = player.Center + new Vector2(R(-22f, 22f), R(-26f, 26f));
                PADParticleHandler.SpawnParticle(new GlitchBar(at, Vector2.Zero, Split(tick), R(8f, 24f), 2f, Main.rand.Next(6, 12), 5f));
            }

            Lighting.AddLight(player.Center, 0.55f, 0.08f, 0.14f);
        }

        // ---- Adam Kit I (TentochiPlayer.Detonate) -----------------------------------------------------------------

        /// <summary>
        /// The kit let go: a flash to whiten the screen near it, a hard ring out to the blast's edge and soft ones after it,
        /// light thrown out every way, hot squares and smoke, heat rising after. <paramref name="reach"/>: the blast's
        /// edge, in world pixels.
        /// </summary>
        public static void Detonation(Vector2 at, float reach)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(TentochiSounds.Detonate, at);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.4f }, at);

            if (Main.LocalPlayer.active && Vector2.Distance(Main.LocalPlayer.Center, at) < 1800f)
            {
                Main.LocalPlayer.PIS().BumpScreenShake(22f, 40);
                TentochiHUD.Flash(Vector2.Distance(Main.LocalPlayer.Center, at) < reach * 1.5f ? 1f : 0.45f);
            }

            if (!Room(160))
                return;

            PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, Color.White, 2.2f, 18));
            PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, Red, 3.2f, 40));
            PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, Amber, 1.4f, 30));

            PADParticleHandler.SpawnParticle(new CyberRing(at, Hot, 8f, reach, 6f, 26, 72));
            PADParticleHandler.SpawnParticle(new CyberRing(at, Red, 8f, reach * 1.15f, 48f, 34, 72, 0f, 0f, soft: true));
            PADParticleHandler.SpawnParticle(new CyberRing(at, Cyan * 0.8f, 20f, reach * 0.9f, 3f, 30, 48, 0.5f, 0.03f));
            PADParticleHandler.SpawnParticle(new CyberRing(at, Red * 0.5f, reach * 0.6f, reach * 1.7f, 2f, 44, 96, 0.7f, -0.02f));

            for (int i = 0; i < 44; i++)
            {
                Vector2 velocity = (i / 44f * MathHelper.TwoPi + R(-0.06f, 0.06f)).ToRotationVector2() * R(12f, 26f);
                Color colour = i % 4 == 0 ? Color.White : i % 4 == 1 ? Hot : i % 4 == 2 ? Amber : Red;
                PADParticleHandler.SpawnParticle(new SparkShard(at, velocity, colour, 10f, 2.6f, 40f, Main.rand.Next(16, 26)));
            }

            for (int i = 0; i < 60; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(14f, 14f);
                PADParticleHandler.SpawnParticle(new PixelChip(at, velocity, i % 5 == 0 ? Cyan : i % 2 == 0 ? Amber : Red, i % 6 == 0 ? 4f : 2f, Main.rand.Next(30, 60), 0.18f, true, 0.94f));
            }

            for (int i = 0; i < 18; i++)
            {
                float angle = R(0f, MathHelper.TwoPi);
                Vector2 bar = at + angle.ToRotationVector2() * R(reach * 0.2f, reach * 0.9f);
                PADParticleHandler.SpawnParticle(new GlitchBar(bar, Vector2.Zero, Split(i), R(16f, 60f), i % 3 == 0 ? 4f : 2f, Main.rand.Next(14, 30), 10f));
            }

            for (int i = 0; i < 14; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * R(2f, 6f);
                PADParticleHandler.SpawnParticle(new FlameSmoke(at + velocity * 6f, velocity, new Color(52, 46, 54), Main.rand.Next(60, 90), 30f, 110f, 0.6f));
            }

            for (int i = 0; i < 20; i++)
            {
                Vector2 spot = at + Main.rand.NextVector2Circular(reach * 0.5f, reach * 0.4f);
                PADParticleHandler.SpawnParticle(new Ember(spot, new Vector2(R(-0.6f, 0.6f), R(-2.4f, -0.8f)), i % 2 == 0 ? Amber : Red, R(0.7f, 1.2f), Main.rand.Next(40, 70), 0.03f));
            }
        }
    }

    /// <summary>
    /// Their sounds, made here by formula, nothing recorded (Content/Sounds/Tentochi):
    ///
    ///   Dash       a servo kick and a jet's short roar, a rising digital chirp on top
    ///   Launch     a tube's thump, a hiss away
    ///   Blast      a short hard crack, a burst of noise falling off, a low thump
    ///   Ready      two quick tones, up, as a device says it is ready
    ///   Surge      a heartbeat under a rising synthetic whine, cut sharp
    ///   Detonate   a charge's whine cut off, a great low boom, digital crackle in its tail
    /// </summary>
    internal static class TentochiSounds
    {
        private const string Folder = "PisAndDag/Content/Sounds/Tentochi/";

        public static readonly SoundStyle Dash = new SoundStyle(Folder + "Dash") { PitchVariance = 0.06f, Volume = 0.75f };
        public static readonly SoundStyle Launch = new SoundStyle(Folder + "Launch") { PitchVariance = 0.1f, Volume = 0.6f, MaxInstances = 4 };
        public static readonly SoundStyle Blast = new SoundStyle(Folder + "Blast") { PitchVariance = 0.12f, Volume = 0.7f, MaxInstances = 4 };
        public static readonly SoundStyle Ready = new SoundStyle(Folder + "Ready") { Volume = 0.55f };
        public static readonly SoundStyle Surge = new SoundStyle(Folder + "Surge") { Volume = 0.8f };
        public static readonly SoundStyle Detonate = new SoundStyle(Folder + "Detonate") { Volume = 1f };
    }
}
