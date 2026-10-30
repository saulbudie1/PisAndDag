using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Empress;
using Terraria;
using Terraria.GameContent;

namespace PisAndDag.Content.Graphics
{
    /// <summary>
    /// Glass breaking as light: the Empress's mirror crack as it bursts (EmpressOfLight.Crack), and the Everclear Mirror's
    /// copy as it breaks (MimicSystem.Shatter). By request ("remake the glass smash effect of phase 1 Empress of Light like
    /// this? Check calamity's code but make it ours and more beautiful"), after the Devourer of Gods' rift as Calamity draws
    /// it (DoGTeleportRift: a star of light growing at a point, up and down longer than across, a ring closing on it, the
    /// crack going in deeper three times with sparks and a flash; then the burst: cracks thrown out of the point, sparks,
    /// lights, flashes, a great bloom, the screen shaken). Here none of its textures, shaders or code: the mod's own glow
    /// textures (GlowHot, GlowSoft, GlowStar, SoftLine, Streak, as EmpressArt draws them), and these, made the mod's own:
    ///
    ///   - **glass, not lightning:** the cracks run straight and turn sharp now and then, some fork, and between each two,
    ///     twice, a crack across, as a struck pane's web (Calamity's wander as lightning does);
    ///   - **a prism's light:** the long cross split into the rainbow along each side; each ring of the burst split into
    ///     three of its colours, a little apart, red outermost, white where they meet; the cracks' glow in the colours
    ///     going round; the halo in the colours, slower to go than the white;
    ///   - **a lens:** small stars and bars of light along the cross, as a lens throws ghosts of a light along its flare;
    ///   - **its heart a crystal:** two stars of light across each other, turning apart, in the white-hot flash;
    ///   - **glass to the end:** flecks thrown out slower than the sparks, falling a little, glinting as they turn; for the
    ///     Everclear Mirror, its own glass in triangles, turning over as they fly (narrower as they turn edge on), flashing
    ///     white each time they face the light.
    ///
    /// Before it breaks (the Empress's crack), the glass cracked along those same cracks, clean (by request: "remove these
    /// sectioned outlines js a pure clean glass break"): thin white lines with a faint glow, the web across them
    /// opening as it strains, glints running out along them (<see cref="DrawCrack"/>); sparkles home along them as it
    /// mends (<see cref="DrawHomecoming"/>).
    ///
    /// Everything is worked out from its seed and its age (ticks since it burst), not kept: it is the same on every
    /// machine for the same seed, and can be drawn wherever it is wanted (over the Empress's moved shards, or with the
    /// world when there are none) without being drawn twice. Measured in world pixels from its reach (the Empress's
    /// crack, 512; the mirror's copy, 190). Drawn in the game's premultiplied blend, its light added (colours with no
    /// alpha), into whatever sprite batch is open. Client only.
    /// </summary>
    public static class PrismShatter
    {
        /// <summary>Whose light: hers (the rainbow) or the Everclear Mirror's (its glass, and the copy's gold).</summary>
        public enum Look
        {
            Empress,
            Mirror,
        }

        /// <summary>How long the burst is drawn for, in ticks.</summary>
        public const int Life = 80;

        /// <summary>How long a pulse (a crack going in deeper) is drawn for.</summary>
        public const int PulseLife = 40;

        // ---- colours ---------------------------------------------------------------------------------------------------

        /// <summary>The Everclear Mirror's colours round: its glass (pale sky, white), the copy's outline's two golds, pale cyan.</summary>
        private static readonly Color[] MirrorWheel =
        {
            new Color(205, 238, 255), new Color(255, 214, 92), new Color(240, 250, 255), new Color(140, 225, 255), new Color(255, 236, 170),
        };

        /// <summary>One of its colours, going round: a whole turn every 1 of <paramref name="t"/>.</summary>
        public static Color Hue(Look look, float t)
        {
            if (look == Look.Empress)
                return EmpressArt.Prism(t);

            float u = (t % 1f + 1f) % 1f * MirrorWheel.Length;
            int i = (int)u;
            return Color.Lerp(MirrorWheel[i % MirrorWheel.Length], MirrorWheel[(i + 1) % MirrorWheel.Length], u - i);
        }

        private static Color Light(Color colour, float strength) => EmpressArt.Additive(colour, MathHelper.Clamp(strength, 0f, 1f));

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        /// <summary>Fast out, slow in to its end (a cube).</summary>
        private static float Out(float x)
        {
            x = Clamp01(x);
            float k = 1f - x;
            return 1f - k * k * k;
        }

        // ---- the burst ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The burst at <paramref name="centre"/>, <paramref name="age"/> ticks after it (nothing before 0 or after
        /// <see cref="Life"/>): the flash and its crystal heart, the cross and its lens ghosts, the spikes, the cracks and
        /// their web, the rings, the sparks and the flecks of glass. <paramref name="cracked"/>: its cracks were already
        /// there (<see cref="DrawCrack"/>, the same seed and reach), so they flare where they are rather than run out.
        /// </summary>
        public static void DrawBurst(SpriteBatch spriteBatch, Vector2 centre, int seed, float age, float reach, Look look, float strength = 1f, bool cracked = false)
        {
            if (age < 0f || age >= Life || reach <= 0f || strength <= 0f)
                return;

            float R = reach;
            var dice = new EmpressOfLight.UnifiedRandomish(seed);

            // The middle: a flash over the whole of the screen for a moment; a white-hot heart cut as a crystal (two
            // stars of light across each other, turning apart); a halo of the colours, slower to go; the rainbow round
            // its rim.
            float flash = Square(Clamp01(1f - age / 14f));
            float halo = Square(Clamp01(1f - age / 50f));

            EmpressArt.DrawBloom(spriteBatch, centre, R * 4f, Light(Color.White, 0.18f * Square(Clamp01(1f - age / 10f)) * strength));
            DrawHot(spriteBatch, centre, R * (0.5f + 0.35f * Out(age / 6f)), Light(Color.White, flash * strength));
            EmpressArt.DrawStar(spriteBatch, centre, R * (0.7f + 0.5f * Out(age / 8f)), age * 0.01f, Light(Color.White, 0.9f * flash * strength));
            EmpressArt.DrawStar(spriteBatch, centre, R * (0.7f + 0.5f * Out(age / 8f)) * 0.7f, MathHelper.PiOver4 - age * 0.014f, Light(Color.White, 0.9f * flash * strength));
            EmpressArt.DrawBloom(spriteBatch, centre, R * (1f + 0.9f * Out(age / 30f)), Light(Hue(look, age / 90f), 0.5f * halo * strength));

            for (int i = 0; i < 5; i++)
            {
                float o = R * 0.06f * (1f + age / 12f);
                float a = i * MathHelper.TwoPi / 5f + age * 0.04f;
                EmpressArt.DrawBloom(spriteBatch, centre + a.ToRotationVector2() * o, R * 0.38f, Light(Hue(look, i / 5f), 0.45f * flash * strength));
            }

            // The cross: a long thin flare up and down, a shorter one across, the rainbow split along each side of it;
            // ghosts of the lens along it, a small star and a bar of light each.
            float cross = (float)Math.Pow(Clamp01(1f - age / 42f), 1.6);

            if (cross > 0f)
            {
                float grow = 0.55f + 0.45f * Out(age / 5f);
                float w = R * 0.04f * (1f - 0.5f * age / 42f);

                for (int arm = 0; arm < 2; arm++)
                {
                    float armLength = arm == 0 ? 2.5f : 1.6f;

                    for (int sign = 1; sign >= -1; sign -= 2)
                    {
                        float way = (arm == 0 ? -MathHelper.PiOver2 : 0f) + (sign > 0 ? 0f : MathHelper.Pi);
                        float length = R * armLength * grow;
                        Vector2 along = way.ToRotationVector2();
                        Vector2 across = new Vector2(-along.Y, along.X);

                        EmpressArt.DrawRay(spriteBatch, centre, way, length, w * 3.2f, Light(Hue(look, 0.15f + arm * 0.4f), 0.45f * cross * strength));
                        EmpressArt.DrawRay(spriteBatch, centre + across * w * 1.1f, way, length * 0.92f, w * 0.9f, Light(Hue(look, 0f), 0.35f * cross * strength));
                        EmpressArt.DrawRay(spriteBatch, centre - across * w * 1.1f, way, length * 0.92f, w * 0.9f, Light(Hue(look, 0.6f), 0.35f * cross * strength));
                        EmpressArt.DrawRay(spriteBatch, centre, way, length, w * 0.9f, Light(Color.White, 0.95f * cross * strength));

                        for (int g = 0; g < 3; g++)
                        {
                            float share = g == 0 ? 0.34f : g == 1 ? 0.58f : 0.8f;
                            Vector2 at = centre + along * length * share;
                            Vector2 half = along * R * 0.045f;

                            EmpressArt.DrawStar(spriteBatch, at, R * 0.06f * (1.2f - share), age * 0.03f + g, Light(Color.White, 0.55f * cross * strength));
                            EmpressArt.DrawLine(spriteBatch, at - half, at + half, w * 2f, Light(Hue(look, g * 0.3f + arm * 0.5f), 0.5f * cross * strength));
                        }
                    }
                }
            }

            // The spikes: thin rays of every length out of the middle (four of them long), coloured, white down each,
            // turning a little as they go.
            for (int i = 0; i < Spikes; i++)
            {
                float way = dice.Next() * MathHelper.TwoPi;
                float r = dice.Next();
                float length = R * (0.3f + 0.95f * r * r);

                if (i < 4)
                    length = R * (1.4f + 0.6f * dice.Next());

                float width = R * (0.01f + 0.022f * dice.Next());
                float life = 24f + 18f * dice.Next();
                float spin = (dice.Next() - 0.5f) * 0.008f;
                float fade = Square(Clamp01(1f - age / life));

                if (fade <= 0f)
                    continue;

                float turned = way + spin * age;
                float now = length * Out(age / 5f) * (1f + 0.15f * age / life);

                EmpressArt.DrawRay(spriteBatch, centre, turned, now, width * 2.4f, Light(Hue(look, i * 0.13f), 0.5f * fade * strength));
                EmpressArt.DrawRay(spriteBatch, centre, turned, now * 0.9f, width, Light(Color.White, 0.8f * fade * strength));
            }

            // The cracks: the glass broken out of the middle, running out at once, then closing thin; the web across
            // them a moment after, fainter.
            Cracks cracks = Cracks.Of(seed, R);
            float shown = cracked ? 1f : Out(age / 7f);
            float closing = (float)Math.Pow(Clamp01(1f - Math.Max(0f, age - 8f) / 30f), 1.5);

            if (closing > 0f)
            {
                for (int j = 0; j < cracks.Runs.Length; j++)
                {
                    Crack crack = cracks.Runs[j];
                    DrawRun(spriteBatch, centre, crack.Points, shown, R * 0.026f * closing, Hue(look, j * 0.09f + crack.Hue * 0.3f), closing * strength, 0.9f);

                    if (crack.Branch != null)
                    {
                        float branchShown = Clamp01((shown * (crack.Points.Length - 1) - crack.BranchAt) / (crack.Branch.Length - 1));

                        if (branchShown > 0f)
                            DrawRun(spriteBatch, centre, crack.Branch, branchShown, R * 0.014f * closing, Hue(look, j * 0.09f + 0.5f), closing * strength, 0.8f);
                    }
                }

                foreach (Web web in cracks.Webs)
                {
                    float webShown = Clamp01((shown - web.Along) * 3f);

                    if (webShown > 0f)
                        DrawRun(spriteBatch, centre, web.Points, webShown, R * 0.008f * closing + 0.5f, Hue(look, web.Ring * 1.7f + web.Along), 0.75f * closing * strength, 0.6f);
                }
            }

            // The rings: two waves out of it, one after the other, thinning as they go, each split as a prism splits
            // light: three of the colours a little apart, outermost to inmost, white where they meet.
            for (int k = 0; k < 2; k++)
            {
                float p = (age - (k == 0 ? 0f : 7f)) / 30f;

                if (p <= 0f || p >= 1f)
                    continue;

                float radius = R * (0.15f + 1.45f * Out(p));
                float width = R * 0.035f * (1f - p) + 2f;
                float st = 0.6f * (float)Math.Pow(1f - p, 1.4) * strength * (k == 0 ? 1f : 0.7f);
                float spread = R * 0.03f * (0.4f + p);

                DrawCircle(spriteBatch, centre, radius + spread, width, Light(Hue(look, 0f + k * 0.3f), st * 0.8f));
                DrawCircle(spriteBatch, centre, radius, width, Light(Hue(look, 0.2f + k * 0.3f), st * 0.8f));
                DrawCircle(spriteBatch, centre, radius - spread, width, Light(Hue(look, 0.6f + k * 0.3f), st * 0.8f));
                DrawCircle(spriteBatch, centre, radius, Math.Max(1f, width * 0.3f), Light(Color.White, st * 0.6f));
            }

            // The sparks: thrown out hard, slowing, drawn out behind as they go, fading on a cube.
            for (int i = 0; i < Sparks; i++)
            {
                float way = dice.Next() * MathHelper.TwoPi;
                float speed0 = R * (0.045f + 0.065f * dice.Next());
                float life = 22f + 24f * dice.Next();
                Color hue = dice.Next() < 0.3f ? Color.White : Hue(look, dice.Next());

                if (age >= life)
                    continue;

                float slowed = (float)Math.Pow(0.9, age);
                float distance = speed0 * (1f - slowed) / 0.1f;
                float speed = speed0 * slowed;
                Vector2 dir = way.ToRotationVector2();
                Vector2 head = centre + dir * distance;
                Vector2 tail = head - dir * (4f + speed * 5f);
                float fade = 1f - (float)Math.Pow(age / life, 3);
                float width = R * 0.012f * (1f - age / life) + 1.2f;

                EmpressArt.DrawLine(spriteBatch, tail, head, width * 2.5f, Light(hue, 0.55f * fade * strength));
                EmpressArt.DrawLine(spriteBatch, tail, head, width, Light(Color.White, 0.85f * fade * strength));
            }

            // The glass: flecks thrown out slower, falling a little, turning and glinting as they turn.
            for (int i = 0; i < Flecks; i++)
            {
                float way = dice.Next() * MathHelper.TwoPi;
                float speed0 = R * (0.02f + 0.045f * dice.Next());
                float life = 50f + 30f * dice.Next();
                float size = R * (0.025f + 0.03f * dice.Next());
                float phase = dice.Next() * MathHelper.TwoPi;
                float rate = 0.25f + 0.3f * dice.Next();
                Color hue = Hue(look, dice.Next());

                if (age >= life)
                    continue;

                float distance = speed0 * (1f - (float)Math.Pow(0.95, age)) / 0.05f;
                Vector2 at = centre + way.ToRotationVector2() * distance + new Vector2(0f, 0.025f * age * age);
                float twinkle = Math.Max(0f, (float)Math.Sin(age * rate + phase));
                float fade = 1f - Square(age / life);

                if (twinkle > 0.05f)
                    EmpressArt.DrawStar(spriteBatch, at, size * (0.6f + 0.8f * twinkle), age * 0.05f + phase, Light(Color.White, 0.9f * twinkle * fade * strength));

                EmpressArt.DrawBloom(spriteBatch, at, size * 0.8f, Light(hue, 0.5f * fade * strength));
            }
        }

        private const int Spikes = 30;
        private const int Sparks = 44;
        private const int Flecks = 36;

        // ---- the glass cracked, before it breaks -------------------------------------------------------------------

        /// <summary>
        /// The glass cracked, before it breaks (by request: "remove these sectioned outlines js a pure clean glass break"):
        /// its cracks, the burst's own (the same seed and reach, so it breaks along them), run out from the middle as far as
        /// <paramref name="shown"/> (0 to 1), thin and clean: a white line, a faint glow either side of it, a little of the
        /// colours in the glow; the web across between them, the inner as the cracks pass it, the outer as it strains
        /// (<paramref name="strain"/>, 0 to 1), the lines brightening and widening a little with it; now and then a glint
        /// running out along a crack. <paramref name="clock"/>: ticks, the act's own (the same on every machine).
        /// </summary>
        public static void DrawCrack(SpriteBatch spriteBatch, Vector2 centre, int seed, float reach, float shown, float strain, float clock, Look look, float strength = 1f)
        {
            if (shown <= 0.001f || reach <= 0f || strength <= 0.001f)
                return;

            Cracks cracks = Cracks.Of(seed, reach);
            float core = (0.55f + 0.4f * strain) * strength;
            float glow = (0.12f + 0.22f * strain) * strength;
            float width = 1.3f + 0.9f * strain;

            for (int j = 0; j < cracks.Runs.Length; j++)
            {
                Crack crack = cracks.Runs[j];
                Color tint = Color.Lerp(Color.White, Hue(look, j * 0.09f + crack.Hue * 0.3f), 0.35f);
                DrawClean(spriteBatch, centre, crack.Points, shown, width, tint, glow, core);

                if (crack.Branch != null)
                {
                    float branchShown = Clamp01((shown * (crack.Points.Length - 1) - crack.BranchAt) / (crack.Branch.Length - 1));

                    if (branchShown > 0f)
                        DrawClean(spriteBatch, centre, crack.Branch, branchShown, width * 0.75f, tint, glow, core * 0.85f);
                }

                // A glint running out along it, once every hundred ticks or so, each crack at its own time.
                float run = (clock * 0.01f + crack.Hue) % 1f;
                float along = run * shown;

                if (along > 0.02f)
                {
                    float shine = (float)Math.Sin(MathHelper.Pi * run);
                    Vector2 at = centre + crack.At(along);
                    EmpressArt.DrawStar(spriteBatch, at, (12f + 10f * strain) * shine, clock * 0.05f + j, Light(Color.White, (0.35f + 0.5f * strain) * shine * strength));
                    EmpressArt.DrawBloom(spriteBatch, at, (10f + 6f * strain) * shine, Light(Hue(look, j * 0.13f + clock / 300f), 0.5f * shine * strength));
                }
            }

            foreach (Web web in cracks.Webs)
            {
                float opens = Clamp01((shown - web.Along) * 3f);

                if (web.Ring > 0.5f)
                    opens *= Clamp01(strain * 2.5f - 0.2f);

                if (opens > 0f)
                    DrawClean(spriteBatch, centre, web.Points, opens, width * 0.7f, Color.White, glow * 0.8f, core * 0.75f);
            }
        }

        /// <summary>
        /// As it mends (<paramref name="mend"/>, 0 to 1): sparkles running home along its cracks, from their ends to the
        /// middle, two on each, one after the other, each its own colour.
        /// </summary>
        public static void DrawHomecoming(SpriteBatch spriteBatch, Vector2 centre, int seed, float reach, float mend, float clock, Look look, float strength = 1f)
        {
            float left = 1f - Smooth(mend);

            if (left <= 0.01f || reach <= 0f)
                return;

            Cracks cracks = Cracks.Of(seed, reach);

            for (int j = 0; j < cracks.Runs.Length; j++)
            {
                Crack crack = cracks.Runs[j];

                for (int s = 0; s < 2; s++)
                {
                    float run = Clamp01(mend * 1.6f - s * 0.3f);

                    if (run >= 0.999f)
                        continue;

                    Vector2 at = centre + crack.At(1f - Smooth(run));
                    EmpressArt.DrawStar(spriteBatch, at, 22f * left, clock * 0.08f + j, Light(Color.White, 0.9f * left * strength));
                    EmpressArt.DrawBloom(spriteBatch, at, 18f * left, Light(Hue(look, j / (float)cracks.Runs.Length + s * 0.3f), 0.7f * left * strength));
                }
            }
        }

        /// <summary>A crack as far as it has run, clean: the colour's faint glow either side, white down the middle, a little thinner to its end.</summary>
        private static void DrawClean(SpriteBatch spriteBatch, Vector2 centre, Vector2[] points, float shown, float width, Color tint, float glow, float core)
        {
            int n = points.Length - 1;
            float upto = shown * n;

            for (int k = 0; k < n && k < upto; k++)
            {
                float share = Math.Min(1f, upto - k);
                Vector2 a = points[k];
                Vector2 b = a + (points[k + 1] - a) * share;
                float w = width * (1f - 0.45f * k / n);

                EmpressArt.DrawLine(spriteBatch, centre + a, centre + b, w * 4f, Light(tint, glow));
                EmpressArt.DrawLine(spriteBatch, centre + a, centre + b, Math.Max(1f, w), Light(Color.White, core));
            }
        }

        /// <summary>Smoothstep: no jolt at either end.</summary>
        private static float Smooth(float x)
        {
            x = Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // ---- as it strains -------------------------------------------------------------------------------------------

        /// <summary>
        /// The glass straining (<paramref name="charge"/> 0 to 1): a star of light at the middle, up and down longer than
        /// across, growing, its colour turning; a ring of the colours closing in on it.
        /// </summary>
        public static void DrawCharge(SpriteBatch spriteBatch, Vector2 centre, float charge, float time, float reach, Look look, float strength = 1f)
        {
            float c = 0.5f - 0.5f * (float)Math.Cos(MathHelper.Pi * Clamp01(charge));

            if (c <= 0.001f || reach <= 0f)
                return;

            float R = reach;
            float w = R * 0.025f * c + 1f;

            for (int arm = 0; arm < 2; arm++)
            {
                float armLength = arm == 0 ? 1.7f : 1.1f;

                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    float way = (arm == 0 ? -MathHelper.PiOver2 : 0f) + (sign > 0 ? 0f : MathHelper.Pi);
                    float length = R * armLength * c;

                    EmpressArt.DrawRay(spriteBatch, centre, way, length, w * 2.6f, Light(Hue(look, time / 120f + arm * 0.5f), 0.5f * c * strength));
                    EmpressArt.DrawRay(spriteBatch, centre, way, length, w, Light(Color.White, 0.7f * c * strength));
                    EmpressArt.DrawRay(spriteBatch, centre, way, length * 0.8f, w * 0.8f, Light(Hue(look, time / 120f + 0.3f), 0.6f * c * strength));
                }
            }

            DrawHot(spriteBatch, centre, R * 0.2f * c, Light(Color.White, 0.9f * c * strength));
            EmpressArt.DrawBloom(spriteBatch, centre, R * 0.7f * c, Light(Hue(look, time / 90f), 0.35f * c * strength));

            // The ring closing on it, the colours going round it the other way.
            float radius = R * (2.3f - 2.1f * c);
            float width = R * 0.03f + 2f;
            int steps = 96;

            for (int s = 0; s < steps; s++)
            {
                Vector2 a = centre + (s / (float)steps * MathHelper.TwoPi).ToRotationVector2() * radius;
                Vector2 b = centre + ((s + 1) / (float)steps * MathHelper.TwoPi).ToRotationVector2() * radius;

                EmpressArt.DrawLine(spriteBatch, a, b, width, Light(Hue(look, s / (float)steps * 2f - time / 60f), 0.5f * c * strength));
                EmpressArt.DrawLine(spriteBatch, a, b, 1.5f, Light(Color.White, 0.4f * c * strength));
            }
        }

        /// <summary>
        /// The crack going in deeper as it strains, <paramref name="age"/> ticks after (nothing after
        /// <see cref="PulseLife"/>): a flash at the middle, spikes, sparks out of it, a ring.
        /// </summary>
        public static void DrawPulse(SpriteBatch spriteBatch, Vector2 centre, int seed, float age, float reach, Look look, float strength = 1f)
        {
            if (age < 0f || age >= PulseLife || reach <= 0f)
                return;

            float R = reach;
            var dice = new EmpressOfLight.UnifiedRandomish(seed * 13 + 5);
            float flash = Square(Clamp01(1f - age / 12f));

            DrawHot(spriteBatch, centre, R * 0.6f, Light(Color.White, 0.8f * flash * strength));

            for (int i = 0; i < 14; i++)
            {
                float way = dice.Next() * MathHelper.TwoPi + age * 0.004f;
                float length = R * (0.4f + 0.8f * Square(dice.Next()));
                float width = R * (0.015f + 0.02f * dice.Next());
                float fade = Square(Clamp01(1f - age / (18f + 10f * dice.Next())));
                float now = length * Out(age / 4f);

                EmpressArt.DrawRay(spriteBatch, centre, way, now, width * 2.2f, Light(Hue(look, i * 0.17f), 0.45f * fade * strength));
                EmpressArt.DrawRay(spriteBatch, centre, way, now * 0.85f, width, Light(Color.White, 0.75f * fade * strength));
            }

            for (int i = 0; i < 16; i++)
            {
                float way = dice.Next() * MathHelper.TwoPi;
                float speed0 = R * (0.05f + 0.06f * dice.Next());
                float life = 18f + 16f * dice.Next();
                Color hue = Hue(look, dice.Next());

                if (age >= life)
                    continue;

                float slowed = (float)Math.Pow(0.88, age);
                Vector2 dir = way.ToRotationVector2();
                Vector2 head = centre + dir * (speed0 * (1f - slowed) / 0.12f);
                Vector2 tail = head - dir * (3f + speed0 * slowed * 4f);
                float fade = 1f - (float)Math.Pow(age / life, 3);

                EmpressArt.DrawLine(spriteBatch, tail, head, 4f, Light(hue, 0.5f * fade * strength));
                EmpressArt.DrawLine(spriteBatch, tail, head, 1.6f, Light(Color.White, 0.8f * fade * strength));
            }

            float p = age / 24f;

            if (p < 1f)
            {
                float radius = R * (0.1f + 0.9f * Out(p));
                int steps = 48;

                for (int s = 0; s < steps; s++)
                {
                    Vector2 a = centre + (s / (float)steps * MathHelper.TwoPi).ToRotationVector2() * radius;
                    Vector2 b = centre + ((s + 1) / (float)steps * MathHelper.TwoPi).ToRotationVector2() * radius;
                    EmpressArt.DrawLine(spriteBatch, a, b, R * 0.03f * (1f - p) + 1.5f, Light(Hue(look, s / (float)steps + age / 40f), 0.45f * (float)Math.Pow(1f - p, 1.5) * strength));
                }
            }
        }

        // ---- the Everclear Mirror's own glass ----------------------------------------------------------------------------

        private const int Panes = 16;

        /// <summary>
        /// Triangles of the mirror's glass thrown out of it, <paramref name="age"/> ticks after it broke: each turning
        /// over as it flies (seen narrower as it turns edge on), falling, its face catching the light and flashing white
        /// as it turns to it, its edges bright. Drawn straight to the device (triangles: no sprite batch may be open; the
        /// caller ends its batch before and begins it again after), then <see cref="DrawGlassEdges"/> inside a batch.
        /// </summary>
        public static void DrawGlassPanes(Vector2 centre, int seed, float age, float reach, Look look, float strength = 1f)
        {
            ForEachPane(centre, seed, age, reach, look, strength, (a, b, c, fill, edge, shine, size, turn, _) =>
            {
                Strip.Begin();
                Strip.Pair(a, b, fill, Color.Lerp(fill, edge, 0.4f), 0f);
                Strip.Pair(c, c, fill * 0.6f, fill * 0.6f, 1f);
                Strip.Draw(TextureAssets.MagicPixel.Value, SamplerState.PointClamp);
            });
        }

        /// <summary>The panes' edges and the flash of the light they catch (inside a sprite batch).</summary>
        public static void DrawGlassEdges(SpriteBatch spriteBatch, Vector2 centre, int seed, float age, float reach, Look look, float strength = 1f)
        {
            ForEachPane(centre, seed, age, reach, look, strength, (a, b, c, fill, edge, shine, size, turn, fade) =>
            {
                EmpressArt.DrawLine(spriteBatch, a, b, 2f, edge);
                EmpressArt.DrawLine(spriteBatch, b, c, 2f, edge);
                EmpressArt.DrawLine(spriteBatch, c, a, 2f, edge);

                if (shine > 0.3f)
                    EmpressArt.DrawStar(spriteBatch, a, size * 1.4f * shine, turn, Light(Color.White, 0.8f * shine * fade * strength));
            });
        }

        private delegate void PaneAction(Vector2 a, Vector2 b, Vector2 c, Color fill, Color edge, float shine, float size, float turn, float fade);

        private static void ForEachPane(Vector2 centre, int seed, float age, float reach, Look look, float strength, PaneAction action)
        {
            if (age < 0f || age >= Life || reach <= 0f)
                return;

            float R = reach;
            var dice = new EmpressOfLight.UnifiedRandomish(seed * 3 + 11);
            Span<Vector2> corners = stackalloc Vector2[3];

            for (int i = 0; i < Panes; i++)
            {
                float way = dice.Next() * MathHelper.TwoPi;
                float speed0 = R * (0.03f + 0.06f * dice.Next());
                float size = R * (0.04f + 0.06f * dice.Next());
                float spin = (dice.Next() - 0.5f) * 0.3f;
                float tumble = 0.12f + 0.18f * dice.Next();
                float phase = dice.Next() * MathHelper.TwoPi;
                float life = 40f + 25f * dice.Next();

                for (int k = 0; k < 3; k++)
                {
                    float a = k * MathHelper.TwoPi / 3f + (dice.Next() - 0.5f) * 1.2f;
                    float r = size * (0.6f + 0.6f * dice.Next());
                    corners[k] = a.ToRotationVector2() * r;
                }

                Color hue = Hue(look, dice.Next());
                float start = R * 0.05f * dice.Next();

                if (age >= life)
                    continue;

                float distance = speed0 * (1f - (float)Math.Pow(0.94, age)) / 0.06f + start;
                Vector2 at = centre + way.ToRotationVector2() * distance + new Vector2(0f, 0.05f * age * age);
                float turn = way + spin * age;
                float facing = (float)Math.Cos(age * tumble + phase);
                float fade = 1f - Square(age / life);
                float shine = (float)Math.Pow(Math.Abs(facing), 8);
                float fill = 0.16f + 0.55f * shine;

                Vector2 Place(Vector2 corner) => at + new Vector2(corner.X * facing, corner.Y).RotatedBy(turn);

                Color fillColour = Light(hue, fill * fade * strength);
                Color edgeColour = Light(Color.White, (0.45f + 0.5f * shine) * fade * strength);
                action(Place(corners[0]), Place(corners[1]), Place(corners[2]), fillColour, edgeColour, shine, size, turn, fade);
            }
        }

        // ---- drawing helpers -----------------------------------------------------------------------------------------

        private static float Square(float x) => x * x;

        /// <summary>The white-hot ball (GlowHot), this many world pixels across its bright middle.</summary>
        private static void DrawHot(SpriteBatch spriteBatch, Vector2 at, float across, Color colour)
        {
            if (!EmpressArt.Ready(EmpressArt.Hot))
            {
                EmpressArt.DrawBloom(spriteBatch, at, across, colour);
                return;
            }

            Texture2D hot = EmpressArt.Hot.Value;
            spriteBatch.Draw(hot, at - Main.screenPosition, null, colour, 0f, hot.Size() * 0.5f, across / (hot.Width * 0.5f), SpriteEffects.None, 0f);
        }

        /// <summary>A ring of light, this wide, in eighty lines.</summary>
        private static void DrawCircle(SpriteBatch spriteBatch, Vector2 centre, float radius, float width, Color colour)
        {
            const int steps = 80;
            Vector2 previous = centre + new Vector2(radius, 0f);

            for (int s = 1; s <= steps; s++)
            {
                Vector2 next = centre + (s / (float)steps * MathHelper.TwoPi).ToRotationVector2() * radius;
                EmpressArt.DrawLine(spriteBatch, previous, next, width, colour);
                previous = next;
            }
        }

        /// <summary>
        /// A crack as far as it has run (<paramref name="shown"/>, 0 to 1 along its points), thinning to its end: its
        /// colour wide, white down the middle.
        /// </summary>
        private static void DrawRun(SpriteBatch spriteBatch, Vector2 centre, Vector2[] points, float shown, float width, Color hue, float strength, float white)
        {
            int n = points.Length - 1;
            float upto = shown * n;

            for (int k = 0; k < n && k < upto; k++)
            {
                float share = Math.Min(1f, upto - k);
                Vector2 a = points[k];
                Vector2 b = a + (points[k + 1] - a) * share;
                float taper = (float)Math.Pow(1f - k / (float)n, 0.85);
                float w = width * taper + 1.2f;

                EmpressArt.DrawLine(spriteBatch, centre + a, centre + b, w * 2.8f, Light(hue, 0.7f * strength));
                EmpressArt.DrawLine(spriteBatch, centre + a, centre + b, Math.Max(1f, w * 0.7f), Light(Color.White, white * strength));
            }
        }

        // ---- the cracks, made once for each seed and reach ----------------------------------------------------------------

        private sealed class Crack
        {
            public Vector2[] Points;
            public Vector2[] Branch;
            public int BranchAt;
            public float Hue;

            /// <summary>How far along it each point is, as a share of its length.</summary>
            public float[] Share;

            /// <summary>The point this share of the way along it.</summary>
            public Vector2 At(float share)
            {
                share = Clamp01(share);

                for (int k = 1; k < Points.Length; k++)
                {
                    if (Share[k] >= share)
                    {
                        float span = Share[k] - Share[k - 1];
                        float f = span > 1e-5f ? (share - Share[k - 1]) / span : 1f;
                        return Vector2.Lerp(Points[k - 1], Points[k], f);
                    }
                }

                return Points[Points.Length - 1];
            }
        }

        private sealed class Web
        {
            public Vector2[] Points;
            public float Ring;
            public float Along;
        }

        private sealed class Cracks
        {
            private const int Count = 16;
            private static readonly Dictionary<(int, int), Cracks> made = new();

            public readonly Crack[] Runs = new Crack[Count];
            public readonly List<Web> Webs = new();

            public static Cracks Of(int seed, float reach)
            {
                var key = (seed, (int)reach);

                if (made.TryGetValue(key, out Cracks cracks))
                    return cracks;

                if (made.Count > 24)
                    made.Clear();

                cracks = new Cracks(seed, reach);
                made[key] = cracks;
                return cracks;
            }

            /// <summary>
            /// As glass breaks: straight runs out of the middle, a turn after each, mostly slight, now and then sharp
            /// (never back toward the middle); some forked; between each two neighbours, twice, a crack across with a
            /// kink in it (the web of a struck pane).
            /// </summary>
            private Cracks(int seed, float R)
            {
                var dice = new EmpressOfLight.UnifiedRandomish(seed * 7 + 3);
                float turn = dice.Next() * MathHelper.TwoPi;
                var points = new List<Vector2>();

                for (int j = 0; j < Count; j++)
                {
                    float way = turn + (j + (dice.Next() - 0.5f) * 0.6f) * MathHelper.TwoPi / Count;
                    float reach = R * (0.75f + 0.45f * dice.Next());
                    points.Clear();
                    points.Add(Vector2.Zero);
                    Vector2 at = Vector2.Zero;
                    float going = way;
                    float run = 0f;

                    while (run < reach)
                    {
                        float step = R * (0.07f + 0.09f * dice.Next());
                        going += (dice.Next() - 0.5f) * 0.22f;

                        if (dice.Next() < 0.3f)
                            going += (dice.Next() - 0.5f) * 0.9f;

                        going = way + MathHelper.Clamp(MathHelper.WrapAngle(going - way), -0.7f, 0.7f);
                        at += going.ToRotationVector2() * step;
                        run += step;
                        points.Add(at);
                    }

                    var crack = new Crack { Points = points.ToArray() };
                    crack.Share = new float[crack.Points.Length];

                    for (int k = 1; k < crack.Points.Length; k++)
                        crack.Share[k] = crack.Share[k - 1] + Vector2.Distance(crack.Points[k - 1], crack.Points[k]);

                    for (int k = 1; k < crack.Points.Length; k++)
                        crack.Share[k] /= Math.Max(1f, crack.Share[crack.Points.Length - 1]);

                    if (dice.Next() < 0.6f && crack.Points.Length > 4)
                    {
                        crack.BranchAt = 1 + (int)(dice.Next() * (crack.Points.Length - 3));
                        Vector2 b = crack.Points[crack.BranchAt];
                        float bway = way + (0.45f + 0.45f * dice.Next()) * (dice.Next() < 0.5f ? 1f : -1f);
                        var branch = new List<Vector2> { b };
                        int steps = 2 + (int)(dice.Next() * 3);

                        for (int k = 0; k < steps; k++)
                        {
                            bway += (dice.Next() - 0.5f) * 0.5f;
                            b += bway.ToRotationVector2() * R * (0.05f + 0.07f * dice.Next());
                            branch.Add(b);
                        }

                        crack.Branch = branch.ToArray();
                    }

                    crack.Hue = dice.Next();
                    Runs[j] = crack;
                }

                for (int j = 0; j < Count; j++)
                {
                    Vector2[] first = Runs[j].Points;
                    Vector2[] second = Runs[(j + 1) % Count].Points;

                    for (int ring = 0; ring < 2; ring++)
                    {
                        float share = ring == 0 ? 0.3f : 0.6f;

                        if (dice.Next() < 0.25f)
                            continue;

                        int k1 = Math.Clamp((int)Math.Round(share * (first.Length - 1) + (dice.Next() - 0.5f) * 2f, MidpointRounding.ToEven), 1, first.Length - 1);
                        int k2 = Math.Clamp((int)Math.Round(share * (second.Length - 1) + (dice.Next() - 0.5f) * 2f, MidpointRounding.ToEven), 1, second.Length - 1);
                        Vector2 a = first[k1];
                        Vector2 b = second[k2];
                        Vector2 middle = (a + b) * 0.5f;
                        float length = middle.Length();

                        if (length < 1f)
                            length = 1f;

                        float bulge = (dice.Next() - 0.3f) * 0.12f * length;
                        middle += middle / length * bulge;

                        Webs.Add(new Web { Points = new[] { a, middle, b }, Ring = share, Along = k1 / (float)(first.Length - 1) });
                    }
                }
            }
        }
    }
}
