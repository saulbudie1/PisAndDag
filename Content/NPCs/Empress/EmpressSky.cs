using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Structures;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// The sky while she is here, and what her coming does to the light.
    ///
    ///   THE DARKER SKY   by request, "a darker sky appears (empress is not affected by shader)":
    ///                    a sky of her own over the game's (EmpressSkyEffect), night going down
    ///                    into violet, soft ribbons of her colours right across it and stars of her
    ///                    own that come and go; the light the sky gives the world and its backgrounds
    ///                    turned down with it. Only the sky and the world's light: she is drawn
    ///                    at her own light, never through any of it.
    ///   THE FLICKER      "the light flickers, moon becomes dark and bright, sky become pitch and
    ///                    normal, (can do the same with sun)": the sky's light on the world, the
    ///                    sky itself, its stars, the moon and the sun, all put out and given back
    ///                    as she asks, a tick at a time; and the moon thrown brighter than itself.
    ///   THE HUSH         "all music and ambiance stops", and later "music and stuffs resume":
    ///                    the music pushed down to nothing while it is asked for, and the world's
    ///                    sounds with it (GraveyardSilence.Hush); let go, and the game brings the
    ///                    music back up by itself, over three seconds or so.
    ///
    /// Each is asked for every tick it should hold, by her, on every machine with a screen, and
    /// goes when she stops asking: nothing is switched on to be forgotten. The darker sky eases in
    /// and out; the flicker does not ease, since a flicker is the point.
    /// </summary>
    public static class EmpressSky
    {
        /// <summary>Ticks a request outlives the tick it was made in: the one between her update and the next draw.</summary>
        private const int Slack = 2;

        /// <summary>How much of the sky's light reaches the world under the darker sky, at its darkest.</summary>
        public const float DarkLight = 0.55f;

        /// <summary>How much of itself the moon is under the darker sky.</summary>
        public const float DarkMoon = 0.7f;

        /// <summary>The darker sky comes in over a second, and goes over a second and a half.</summary>
        private const float InRate = 1f / 60f;
        private const float OutRate = 1f / 90f;

        private static float darkWanted;
        private static int darkAsked;

        private static float lightWanted = 1f;
        private static int lightAsked;

        private static float moonWanted = 1f;
        private static float flareWanted;
        private static int moonAsked;

        private static int hushAsked;

        /// <summary>The darker sky, 0 to 1, as far as it has eased.</summary>
        public static float Darkness { get; private set; }

        /// <summary>The darker sky, this tick: how dark, 0 to 1.</summary>
        public static void Darken(float amount)
        {
            darkWanted = Math.Max(darkAsked > 0 ? darkWanted : 0f, MathHelper.Clamp(amount, 0f, 1f));
            darkAsked = Slack;
        }

        /// <summary>The sky's light on the world and the sky itself, this tick: 1 as it is, 0 pitch. No easing.</summary>
        public static void Light(float amount)
        {
            lightWanted = MathHelper.Clamp(amount, 0f, 1f);
            lightAsked = Slack;
        }

        /// <summary>The moon (and the sun), this tick: 1 as it is, 0 put out; and how much brighter than itself it is thrown, 0 to 1.</summary>
        public static void Moon(float amount, float flare = 0f)
        {
            moonWanted = MathHelper.Clamp(amount, 0f, 1f);
            flareWanted = MathHelper.Clamp(flare, 0f, 1f);
            moonAsked = Slack;
        }

        /// <summary>No music, and none of the world's sounds, this tick.</summary>
        public static void Hush()
        {
            hushAsked = Slack;
        }

        public static bool Hushed => hushAsked > 0;

        /// <summary>The sky's light, as asked, this frame.</summary>
        public static float LightNow => lightAsked > 0 ? lightWanted : 1f;

        /// <summary>The light the sky gives the world this frame: the flicker, under the darker sky.</summary>
        public static float WorldLight => LightNow * MathHelper.Lerp(1f, DarkLight, Darkness);

        /// <summary>The moon, this frame: put out or not, under the darker sky.</summary>
        public static float MoonNow => (moonAsked > 0 ? moonWanted : 1f) * MathHelper.Lerp(1f, DarkMoon, Darkness);

        public static float FlareNow => moonAsked > 0 ? flareWanted : 0f;

        /// <summary>Anything of this on at all.</summary>
        public static bool Touching => Darkness > 0f || lightAsked > 0 || moonAsked > 0;

        internal static void Tick()
        {
            float to = darkAsked > 0 ? darkWanted : 0f;
            Darkness = Darkness < to ? Math.Min(to, Darkness + InRate) : Math.Max(to, Darkness - OutRate);

            if (darkAsked > 0) darkAsked--;
            if (lightAsked > 0) lightAsked--;
            if (moonAsked > 0) moonAsked--;
            if (hushAsked > 0) hushAsked--;
        }

        internal static void Reset()
        {
            Darkness = 0f;
            darkAsked = lightAsked = moonAsked = hushAsked = 0;
        }
    }

    /// <summary>
    /// The darker sky itself: over the game's sky, its sun and moon and stars, and under everything
    /// else, as the game's own skies of the bosses are (NebulaSky, MoonLordSky): night going down
    /// into violet, soft ribbons of her colours drifting right across it, and stars of her own that
    /// come and go.
    /// </summary>
    public class EmpressSkyEffect : CustomSky
    {
        public const string Key = "PisAndDag:EmpressSky";

        private bool active;

        /// <summary>Put up (SkyManager.Activate), and not yet taken down: whether or not any of it is still to be seen.</summary>
        public bool Up => active;

        /// <summary>The dark at the top of the sky and at its foot.</summary>
        private static readonly Color Top = new Color(6, 3, 16);
        private static readonly Color Foot = new Color(38, 17, 62);

        /// <summary>Its stars: where on the screen (as shares of it), how big, how fast they come and go, and their phase.</summary>
        private static readonly Vector4[] Stars = MakeStars(46);

        /// <summary>
        /// Her colours across the sky, by request ("place the stripe better or make it loop"): three soft
        /// ribbons high up, each the whole width of the screen and on past both sides, so the end of one is
        /// never seen (each was one stretched smear, its hard ends and edge crossing the screen). How bright
        /// one is along its length, its colour and how it rises and falls come from waves that repeat every
        /// screen's width, so it drifts on for ever with no seam and no jump.
        ///
        ///     X  how high, a share of the screen from its top      Y  how thick, a share of the screen
        ///     Z  its drift, screens a second (either way)          W  a phase of its own
        /// </summary>
        private static readonly Vector4[] Ribbons =
        {
            new Vector4(0.10f, 0.15f, 0.0045f, 0f),
            new Vector4(0.21f, 0.19f, -0.0032f, 2.1f),
            new Vector4(0.32f, 0.13f, 0.0060f, 4.4f),
        };

        /// <summary>A ribbon at its brightest: this much of its colour, added.</summary>
        private const float RibbonLight = 0.15f;

        /// <summary>
        /// The upright slices a ribbon is drawn in, across the screen and a margin either side: thin
        /// enough that no step shows between two (a fraction of a shade, a pixel up or down).
        /// </summary>
        private const int RibbonSlices = 240;

        /// <summary>How far the ribbons go by for the camera's travel, as a share of it: a long way off.</summary>
        private const float Parallax = 0.03f;

        /// <summary>A wave that repeats every 1, -1 to 1.</summary>
        private static float Wave(float x) => (float)Math.Sin(MathHelper.TwoPi * x);

        /// <summary>
        /// How far round a wave this many turns a second has gone, 0 to 1: on the void's clock, which does
        /// not start again every hour as the game's does (that would jump every ribbon back once an hour).
        /// </summary>
        private static float Turned(double turnsASecond) => (float)(EmpressVoidSystem.Clock * turnsASecond % 1.0);

        private static Vector4[] MakeStars(int count)
        {
            var random = new Random(4823);
            var stars = new Vector4[count];

            for (int i = 0; i < count; i++)
            {
                stars[i] = new Vector4((float)random.NextDouble(), (float)Math.Pow(random.NextDouble(), 1.4) * 0.8f,
                    0.05f + 0.13f * (float)random.NextDouble() * (float)random.NextDouble(), (float)random.NextDouble() * 100f);
            }

            return stars;
        }

        public override void OnLoad()
        {
        }

        public override void Update(GameTime gameTime)
        {
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            // Furthest back of everything but the game's own sky, sun, moon and stars.
            if (!(maxDepth >= float.MaxValue && minDepth < float.MaxValue))
                return;

            float dark = EmpressSky.Darkness * EmpressSky.LightNow;

            if (dark <= 0.003f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int w = Main.screenWidth;
            int h = Main.screenHeight;

            // The dark, from the top down, in rows thin enough that each is a fraction of a shade from the
            // next (24 bands before: a step of two or three shades each, and they showed as stripes).
            int rows = Math.Max(24, h / 3);

            for (int i = 0; i < rows; i++)
            {
                int y0 = h * i / rows;
                int y1 = h * (i + 1) / rows;
                float t = (i + 0.5f) / rows;
                Color c = Color.Lerp(Top, Foot, t * t);

                // Not all the way: the moon still shows through it, dimly.
                spriteBatch.Draw(pixel, new Rectangle(0, y0, w, y1 - y0), c * (0.66f * dark));
            }

            float time = Main.GlobalTimeWrappedHourly;

            DrawRibbons(spriteBatch, w, h, dark);

            // Her stars, coming and going.
            if (EmpressArt.Star != null && EmpressArt.Star.IsLoaded)
            {
                Texture2D star = EmpressArt.Star.Value;

                foreach (Vector4 s in Stars)
                {
                    float twinkle = 0.5f + 0.5f * (float)Math.Sin(time * (0.8f + s.Z * 6f) + s.W);
                    twinkle *= twinkle;

                    if (twinkle < 0.05f)
                        continue;

                    Color c = EmpressArt.Additive(EmpressArt.Prism(s.W * 0.13f), 0.9f * twinkle * dark);
                    Vector2 at = new Vector2(s.X * w, s.Y * h);

                    spriteBatch.Draw(star, at, null, c, 0f, star.Size() * 0.5f, s.Z * (0.7f + 0.3f * twinkle), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>
        /// The ribbons (Ribbons): each in thin upright slices of the middle column of a soft glow, soft
        /// above and below. A slice's edges are whole pixels, each where the next one's starts, so they
        /// neither overlap (a bright line) nor part (a dark one).
        /// </summary>
        private static void DrawRibbons(SpriteBatch spriteBatch, int w, int h, float dark)
        {
            if (!EmpressArt.Ready(EmpressArt.Soft) || w <= 0 || h <= 0)
                return;

            Texture2D soft = EmpressArt.Soft.Value;
            Rectangle column = new Rectangle(soft.Width / 2, 0, 1, soft.Height);
            Color[] colours = { EmpressArt.Pink, EmpressArt.Sky, EmpressArt.Lilac };

            float margin = w * 0.06f;
            float span = w + 2f * margin;

            // Where along the ribbons' patterns the camera has got to.
            float travel = Main.screenPosition.X * Parallax / w;

            for (int i = 0; i < Ribbons.Length; i++)
            {
                Vector4 r = Ribbons[i];
                float breathe = 0.85f + 0.15f * Wave(Turned(0.08) + 0.33f * i);
                float drift = Turned(r.Z);
                float shimmer = Turned(0.033);
                float sway = Turned(0.009 * (1.0 + 0.4 * i));
                float ripple = Turned(0.05);
                float swell = Turned(0.027);
                Color from = colours[i];
                Color to = colours[(i + 1) % colours.Length];

                for (int s = 0; s < RibbonSlices; s++)
                {
                    int x0 = (int)Math.Floor(-margin + span * s / RibbonSlices);
                    int x1 = (int)Math.Floor(-margin + span * (s + 1) / RibbonSlices);

                    if (x1 <= x0)
                        continue;

                    // Across the screen, and along the ribbon's own pattern (which repeats every screen).
                    float u = (x0 + x1) * 0.5f / w;
                    float along = u + drift + travel;

                    float bright = 0.55f + 0.30f * Wave(along + r.W) + 0.15f * Wave(2f * along - shimmer + 1.7f * r.W);
                    float rise = r.X + 0.030f * Wave(u + sway + r.W) + 0.010f * Wave(3f * along + ripple);
                    float thick = h * r.Y * (0.85f + 0.15f * Wave(along + swell + 0.5f * r.W));

                    Color hue = Color.Lerp(from, to, 0.5f + 0.5f * Wave(along + r.W + 0.2f));
                    Color c = EmpressArt.Additive(hue, RibbonLight * MathHelper.Clamp(bright, 0f, 1f) * breathe * dark);

                    spriteBatch.Draw(soft, new Vector2(x0, h * rise - thick * 0.5f), column, c, 0f, Vector2.Zero,
                        new Vector2(x1 - x0, thick / soft.Height), SpriteEffects.None, 0f);
                }
            }
        }

        public override bool IsActive() => active || EmpressSky.Darkness > 0.001f;

        public override void Reset()
        {
            active = false;
        }

        public override void Activate(Vector2 position, params object[] args)
        {
            active = true;
        }

        public override void Deactivate(params object[] args)
        {
            active = false;
        }

        /// <summary>The game's clouds go as her sky comes.</summary>
        public override float GetCloudAlpha() => 1f - 0.85f * EmpressSky.Darkness;
    }

    /// <summary>
    /// Runs all of it: the requests latched once a tick, the sky put up and taken down, the music
    /// and the world's sounds pushed down, the sky's light on the world turned down (ModifySunLightColor),
    /// and the moon, the sun and the stars put out or thrown bright (by detour of the game's own
    /// drawing of them, Main.DrawSunAndMoon and Main.DrawStarsInBackground).
    /// </summary>
    public class EmpressSkySystem : ModSystem
    {
        /// <summary>How fast the music is pushed down while hushed: to nothing in a third of a second.</summary>
        private const float MusicDown = 0.08f;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            SkyManager.Instance[EmpressSkyEffect.Key] = new EmpressSkyEffect();

            On_Main.DrawSunAndMoon += SunAndMoon;
            On_Main.DrawStarsInBackground += StarsInBackground;
        }

        public override void PreUpdateEntities()
        {
            if (!Main.dedServ)
                EmpressSky.Tick();
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            // The sky, up while any of it is to be seen, down once it is all gone. (Its IsActive stays true
            // while it fades, so the game goes on drawing it; whether it has been put up is its own.)
            if (SkyManager.Instance[EmpressSkyEffect.Key] is EmpressSkyEffect sky)
            {
                bool wanted = EmpressSky.Darkness > 0f;

                if (wanted && !sky.Up)
                    SkyManager.Instance.Activate(EmpressSkyEffect.Key);
                else if (!wanted && sky.Up)
                    SkyManager.Instance.Deactivate(EmpressSkyEffect.Key);
            }

            if (!EmpressSky.Hushed)
                return;

            // The music, whatever is playing and whatever is coming in, pushed down; the game lifts it
            // again by itself (a two-hundredth a tick) once nothing pushes.
            int playing = Main.curMusic;

            if (playing > 0 && playing < Main.musicFade.Length)
                Main.musicFade[playing] = Math.Max(0f, Main.musicFade[playing] - MusicDown);

            int coming = Main.newMusic;

            if (coming > 0 && coming < Main.musicFade.Length && coming != playing)
                Main.musicFade[coming] = 0f;

            GraveyardSilence.Hush();
        }

        /// <summary>The sky's light on the world and its backgrounds: the flicker, and the darker sky.</summary>
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
        {
            if (Main.dedServ || !EmpressSky.Touching)
                return;

            float light = EmpressSky.WorldLight;

            tileColor = Scale(tileColor, light);

            // The sky (and the backgrounds, which are coloured from it) the same, and a little toward her violet.
            Color sky = Scale(backgroundColor, light);
            backgroundColor = Color.Lerp(sky, Scale(new Color(46, 26, 78), light), 0.3f * EmpressSky.Darkness);
        }

        private static Color Scale(Color c, float k) => new Color((int)(c.R * k), (int)(c.G * k), (int)(c.B * k), c.A);

        /// <summary>
        /// The moon put out, or the sun, as she asks; and the moon drawn a second time over itself as
        /// light (its colour with no alpha adds under the game's alpha blend), thrown brighter than it is.
        /// The sun's own colour does not reach the whole of it, so it is put out by what the game puts it
        /// out with under the mushroom sky.
        /// </summary>
        private static void SunAndMoon(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
        {
            float moon = EmpressSky.MoonNow;
            float flare = EmpressSky.FlareNow;

            if (moon >= 0.999f && flare <= 0.001f)
            {
                orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
                return;
            }

            orig(self, sceneArea, moonColor * moon, sunColor, Math.Max(tempMushroomInfluence, 1f - moon));

            if (flare > 0.001f && !Main.dayTime)
            {
                Color thrown = moonColor * flare;
                thrown.A = 0;

                // The sun is put out entirely in this pass; only the moon is drawn again.
                orig(self, sceneArea, thrown, sunColor, 1f);
            }
        }

        /// <summary>The stars go out with the sky.</summary>
        private static void StarsInBackground(On_Main.orig_DrawStarsInBackground orig, Main self, Main.SceneArea sceneArea, bool artificial)
        {
            if (EmpressSky.LightNow < 0.35f)
                return;

            orig(self, sceneArea, artificial);
        }

        public override void OnWorldUnload()
        {
            EmpressSky.Reset();
        }
    }
}
