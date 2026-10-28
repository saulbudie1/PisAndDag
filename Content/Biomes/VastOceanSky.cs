using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Accessories;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Liquid;
using Terraria.GameContent.Shaders;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// How the Vast Ocean looks, by request ("Just make it feel vast and give it multiple
    /// shaders"; then, its depth in twelve sections, "Draw so that from section 1- section4,
    /// water brightness is gradiently lowered to dark navy and from section 5-6 its to black.
    /// section 1-3 have rays of sunshines and moonlights(1/3 of the length) piericng the water.
    /// animating the whole thing. Surface water also have some waves sometimes(just animation)
    /// Make it overall pretty and after section 6, it is Thalassophobia."): the deep water behind
    /// everything, the light in it in front, the haze over its horizon, waves on its surface, and
    /// five screen shaders of the game's own, all of it keyed to how far under the surface the
    /// screen's middle is (VastOcean's sections).
    ///
    /// THE WATER BEHIND. The game draws its water twice (LiquidRenderer.DrawNormalLiquids,
    /// 1.4.4): once behind everything, whole, and once over everything, the tiles and the
    /// players too, at six tenths everywhere else (its DEFAULT_OPACITY for water) and here at
    /// 45 hundredths, the water clearer (by request: "a more natural, pristine blue", "you can
    /// change its properties however you like as long as its looks good"; <see cref="SeaWater"/>);
    /// each as it is lit, so that where no light reaches the water it is black. Between the two,
    /// at the game's own layer for it (RenderLayers.BackgroundWater, by detour of the overlays'
    /// drawing, in the Vast Ocean only), the sea itself is drawn over the whole screen, from the
    /// clock and the camera alone, nothing kept from frame to frame; under the water as much
    /// brighter than it is to be seen as the game's water over it takes away (<see cref="ThroughWater"/>):
    ///   - the water's colour, row by row, by request ("Change the water color to a more natural,
    ///     pristine blue almost green like. some thing royal and luxurious"): at the surface a
    ///     clear blue going green, bright, the game's own water lit by the sun showing through
    ///     it, then bluer and deeper section by section, through sapphire to a royal blue, a
    ///     dark navy at the end of the 4th, and black within 48 rows under that, all the way
    ///     down, the Abyssal Trench too, so that nothing is seen there but what is lit; dimmer
    ///     by night over the 5th section, as the sky is, the dark under it the same at any hour;
    ///   - snow of the sea, flecks sinking slowly through the light of the first four
    ///     sections; in the dark from the 2nd to the 6th, here and there one that glows blue
    ///     and green and fades and glows;
    ///   - now and then, far off in the blue of the first two, the shadow of something very
    ///     large going by: a shark the size of a ship, and once in a long while a shadow the
    ///     shape of the Duke (the game's own pictures of them, darkened, NPC 65 and 370);
    ///   - in the 7th, a few last flecks, too faint to be sure of, and now and then something
    ///     vast between them and the eye: no shape, only the flecks going out where it passes,
    ///     a long way across, and coming back after; under the 7th, nothing at all;
    ///   - over the surface, a haze along the horizon, pale by day, that the sea's far
    ///     islands fade into.
    ///
    /// THE LIGHT IN THE WATER, drawn in front of everything, the game's water too, at its layer
    /// for that (RenderLayers.ForegroundWater): it is in the water between the eye and all else.
    ///   - about the rays, the light the water gives back: under the sun a fan of it opening
    ///     down into the sea, gold near the surface going pale and fading into the sea's colour
    ///     the deeper and the further to the side, with a glare where it comes in; the moon's
    ///     silver and faint (<see cref="DrawLightFan"/>);
    ///   - under the surface, the light the moving waves gather, a net of bright lines shifting
    ///     all the while, gold under the sun (<see cref="DrawCaustics"/>);
    ///   - the sun's rays by day, the moon's by night, as a swimmer sees them: thin shafts
    ///     slanting down the way the light comes in from where the sun (or moon) is in the sky,
    ///     bent by the water toward straight down, all of them that way within a degree or so,
    ///     spread along the surface under it, each lit for a moment as the waves gather the
    ///     light, shimmering, then gone and another lit elsewhere, brighter in patches that
    ///     drift; brightest near the surface, a soft glow about each; little sparkles in them;
    ///     none under the platform, and none past ground more than three tiles thick; the sun's
    ///     gold, through the first three sections, deeper gold as it is low, the moon's a third
    ///     as long (<see cref="DrawRays"/>, <see cref="LightStops"/>);
    ///   - motes drifting in the water, glittering where the light is (<see cref="DrawSunMotes"/>);
    ///   - glints coming and going along the surface, the light caught by the waves
    ///     (<see cref="DrawGlints"/>).
    ///
    /// THE WAVES. Once in a while a set of waves along a stretch of the surface where it can be
    /// seen: a few crests a dozen tiles apart, given to the game's own water as the ripples it
    /// makes when anything falls in (WaterShaderData.QueueRipple), so they move the water itself,
    /// and then left to it to carry off and let die down; nothing where the game's waves are
    /// turned off.
    ///
    /// THE SHADERS, the game's own passes of its screen effect (ScreenShaderData, as the
    /// Lunar monoliths and the desert's haze have them; no shader of the mod's own):
    ///   PisAndDag:VastDepth    FilterMiniTower, the sea's colour (a clear blue-green near the surface,
    ///                          deep blue by the end of the 4th section): the screen sinks into
    ///                          it as it goes down, an eighth at the surface, more than half by
    ///                          the end of the 4th section, near three quarters by the 6th;
    ///   PisAndDag:VastGold     FilterMiniTower, the colour of old gold: over the water by
    ///                          day, faint, and stronger as the sun is low;
    ///   PisAndDag:VastShimmer  FilterHeatDistortion, the game's heat haze on its own noise:
    ///                          the screen wavers as through water, under the surface only,
    ///                          and twice as hard from the 7th section; not at all for whoever
    ///                          has turned the game's heat distortion off;
    ///   PisAndDag:VastMurk     FilterMiniTower, near black: from the 7th section the screen
    ///                          all but goes, what little is lit drowned in the dark;
    ///   PisAndDag:VastSilt     FilterBlizzardForeground, the game's blizzard in the colours of
    ///                          silt: from the 7th, a murk drifting slowly across everything.
    /// While a lit M.U.P.R.A.L's light can be on the screen (by request: "murpal needs to be able
    /// to shine at this depth bruh", "at every depth"), the murk is cut to 15 hundredths of
    /// itself, the silt to a quarter and the depths' tint, deep down, to half, so that what it
    /// lights is seen; eased in over a third of a second and out over most of one. What its
    /// light does not reach is as dark as ever: nothing there is lit.
    ///
    /// The light textures are the waygate's (WaygateLight, WaygateRay) and two made by formula
    /// beside this file: the fan (VastOceanFan) and the light under the surface, a loop of frames
    /// (VastOceanCaustics).
    /// </summary>
    public class VastOceanSky : ModSystem
    {
        public const string DepthKey = "PisAndDag:VastDepth";
        public const string GoldKey = "PisAndDag:VastGold";
        public const string ShimmerKey = "PisAndDag:VastShimmer";
        public const string MurkKey = "PisAndDag:VastMurk";
        public const string SiltKey = "PisAndDag:VastSilt";

        /// <summary>
        /// The colour the depths tint to (a clear blue-green in the light near the surface, going to the
        /// deep blue by the end of the 4th section), the old gold of the light over the water, the
        /// murk, and the silt's two.
        /// </summary>
        private static readonly Vector3 ShallowTint = new Vector3(0.06f, 0.46f, 0.56f);
        private static readonly Vector3 DeepTint = new Vector3(0.07f, 0.22f, 0.60f);
        private static readonly Vector3 GoldTint = new Vector3(1.00f, 0.82f, 0.55f);
        private static readonly Vector3 MurkTint = new Vector3(0.02f, 0.05f, 0.13f);
        private static readonly Vector3 SiltLight = new Vector3(0.10f, 0.15f, 0.18f);
        private static readonly Vector3 SiltDark = new Vector3(0.02f, 0.04f, 0.06f);

        /// <summary>Strips the water's colour is drawn in, this many pixels high.</summary>
        private const int Strip = 3;

        /// <summary>
        /// How much of what is behind it the game's water drawn over everything hides, here
        /// (LiquidRenderer.DEFAULT_OPACITY for water, set while the Vast Ocean is the place and put
        /// back after: <see cref="ClearWater"/>); everywhere else six tenths.
        /// </summary>
        internal const float SeaWater = 0.45f;

        /// <summary>What is drawn under the water is drawn this much brighter than it is to be seen: the game's water drawn over it leaves 55 hundredths of it.</summary>
        internal const float ThroughWater = 1f / (1f - SeaWater);

        /// <summary>
        /// How far under the line of the water the light lying on the surface is drawn, in pixels
        /// (its middle): the glints, the path of light and the whitecaps, by request ("can you make
        /// feather more accumulated at the surface?", then "theres shines on the surface u can
        /// clearly see a seperation of gray and purple dark"). They were drawn on the line itself,
        /// half over the water, where the game's waves do not carry anything: its water's shader
        /// (FilterWaterDistortion, read in the game's ScreenShader) moves a pixel only where that
        /// pixel or the one it is moved to is water. Over a trough they stayed where the water had
        /// been, with the dark of the sky under them, while a copy went down with the water. In
        /// the water's top pixels the waves carry them, up on a crest and down in a trough.
        /// Nothing is drawn over the line of the water at all (<see cref="WaterTop"/>).
        /// </summary>
        private const float Sunk = 3.5f;

        /// <summary>The first row of the screen's pixels the game's water covers at the surface: the surface row is half full, so its water begins at that row's middle.</summary>
        private static int WaterTop() => (int)Math.Ceiling((VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y - 0.5f);

        private static bool ready;
        private static Asset<Texture2D> light;
        private static Asset<Texture2D> ray;

        /// <summary>The fan of light the water gives back about the rays, and the light the waves gather under the surface, a loop of frames (<see cref="CausticFrames"/>).</summary>
        private static Asset<Texture2D> fan;
        private static Asset<Texture2D> caustics;

        /// <summary>Where the silt's noise has drifted to.</summary>
        private static Vector2 siltDrift;

        /// <summary>How far the depths' dark has given way to a lit M.U.P.R.A.L on the screen, 0 to 1 (<see cref="Visuals"/>).</summary>
        private static float lamp;

        /// <summary>The pass of a far shadow being drawn, and the column of the sea it goes by about (where the camera was as it began).</summary>
        private static int shadowPass = -1;
        private static float shadowAbout;

        /// <summary>
        /// The water's colour as it is to be seen, by day, by rows under the surface, and how much
        /// of the game's own water behind it it hides; between two, it is drawn between. By the
        /// surface the game's water lit by the sun shows through, bright; then, by request ("Change
        /// the water color to a more natural, pristine blue almost green like. some thing royal and
        /// luxurious"), a clear blue going green, bluer and deeper section by section through
        /// sapphire to a royal blue, a dark navy at the end of the 4th, and from there to black
        /// within <see cref="DarkWithin"/> rows, black all the way down, the Abyssal Trench too (by
        /// request: "you can still kinda see too clearly withh murpal thingi want it to be full
        /// darkness and just light up the visible parts", "i dont like the sillhouette concenpt",
        /// "but please make it dark"; the deep was near black, and the trench a little more navy
        /// again, by an earlier request, so that with a lamp's light holding the murk back the
        /// unlit rock stood black against it). (The brightest that can be seen through the game's
        /// water over it is 55 hundredths of white: a channel past 140 is drawn as bright as it can be.)
        /// </summary>
        private static readonly (float rows, Color colour, float cover)[] Water =
        {
            (0f, new Color(22, 134, 100), 0.85f),
            (6f, new Color(20, 124, 112), 0.95f),
            (12f, new Color(18, 116, 118), 1f),
            (24f, new Color(14, 102, 128), 1f),
            (60f, new Color(10, 84, 132), 1f),
            (VastOcean.SectionRows, new Color(8, 64, 128), 1f),
            (2 * VastOcean.SectionRows, new Color(6, 44, 112), 1f),
            (3 * VastOcean.SectionRows, new Color(5, 28, 90), 1f),
            (VastOcean.NavyAt - VastOcean.Surface, new Color(4, 14, 56), 1f),
            (VastOcean.NavyAt - VastOcean.Surface + DarkWithin, new Color(0, 0, 0), 1f),
        };

        /// <summary>How many rows under the end of the 4th section the water's colour takes to go from dark navy to black.</summary>
        private const int DarkWithin = 48;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            Filters.Scene[DepthKey] = new Filter(new ScreenShaderData("FilterMiniTower").UseColor(DeepTint).UseOpacity(0f), EffectPriority.High);
            Filters.Scene[GoldKey] = new Filter(new ScreenShaderData("FilterMiniTower").UseColor(GoldTint).UseOpacity(0f), EffectPriority.Medium);
            Filters.Scene[ShimmerKey] = new Filter(new ScreenShaderData("FilterHeatDistortion").UseImage("Images/Misc/noise").UseIntensity(0f), EffectPriority.High);
            Filters.Scene[MurkKey] = new Filter(new ScreenShaderData("FilterMiniTower").UseColor(MurkTint).UseOpacity(0f), EffectPriority.VeryHigh);
            Filters.Scene[SiltKey] = new Filter(new ScreenShaderData("FilterBlizzardForeground").UseColor(SiltLight).UseSecondaryColor(SiltDark)
                .UseImage("Images/Misc/noise").UseIntensity(0.4f).UseOpacity(0f), EffectPriority.VeryHigh);

            light = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateLight");
            ray = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateRay");
            fan = ModContent.Request<Texture2D>("PisAndDag/Content/Biomes/VastOceanFan");
            caustics = ModContent.Request<Texture2D>("PisAndDag/Content/Biomes/VastOceanCaustics");

            On_OverlayManager.Draw += Layers;

            ready = true;
        }

        public override void Unload()
        {
            ClearWater(false);
            ready = false;
            light = null;
            ray = null;
            fan = null;
            caustics = null;
        }

        // ---- the shaders, and the waves --------------------------------------------------------

        /// <summary>
        /// Every tick, from the biome (VastOceanBiome.SpecialVisuals): each shader on or off,
        /// and how strong, by how far under the surface the screen's middle is; and the waves.
        /// Off, the game fades a shader out by itself.
        /// </summary>
        internal static void Visuals(bool inOcean)
        {
            if (!ready)
                return;

            ClearWater(SubworldKeeper.Current is VastOcean);

            float depth = ScreenDepth();
            float under = inOcean ? Smooth(-4f, 10f, depth) : 0f;
            float day = Daylight();

            // The murk comes on over the forty rows about the top of the 7th section.
            float murk = inOcean ? Smooth(-20f, 20f, depth - (VastOcean.MurkFrom - VastOcean.Surface)) : 0f;

            // A lit M.U.P.R.A.L whose light can be on the screen holds the depths' dark back:
            // quick to, as eyes take to a light, slower from, as they take to the dark again.
            float lampTo = inOcean && MupralLight.LitOnScreen() ? 1f : 0f;
            lamp = lampTo > lamp ? Math.Min(lampTo, lamp + 1f / 20f) : Math.Max(lampTo, lamp - 1f / 50f);

            float navy = Smooth(0f, VastOcean.NavyAt - VastOcean.Surface, depth);
            float black = Smooth(VastOcean.NavyAt - VastOcean.Surface, VastOcean.BlackAt - VastOcean.Surface, depth);
            float tint = under * (0.12f + 0.44f * navy + 0.18f * black) * (1f - 0.6f * murk) * (1f - 0.5f * lamp * navy);
            Vector3 tintColour = Vector3.Lerp(ShallowTint, DeepTint, navy);
            float gold = inOcean ? (1f - under) * day * (0.07f + 0.09f * SunLow()) : 0f;
            float shimmer = Main.UseHeatDistortion ? under * (0.55f + 0.35f * Smooth(0f, 120f, depth) + 0.9f * murk) : 0f;
            float murkOpacity = 0.82f * murk * (1f - 0.85f * lamp);
            float silt = 0.32f * murk * (1f - 0.75f * lamp);

            if (silt > 0.002f && !Main.gamePaused && Main.hasFocus)
                siltDrift = new Vector2((siltDrift.X + 0.0011f) % 10f, (siltDrift.Y + 0.0004f) % 10f);

            Drive(DepthKey, tint, s => s.UseColor(tintColour).UseOpacity(tint));
            Drive(GoldKey, gold, s => s.UseOpacity(gold));
            Drive(ShimmerKey, shimmer, s => s.UseIntensity(shimmer));
            Drive(MurkKey, murkOpacity, s => s.UseOpacity(murkOpacity));
            Drive(SiltKey, silt, s => s.UseOpacity(silt).UseImageOffset(siltDrift));

            if (inOcean)
                Waves(Rough());
        }

        /// <summary>The game's own opacity for its water over everything, as it was before the Vast Ocean set its own; less than 0 while it is not set.</summary>
        private static float landWater = -1f;

        /// <summary>
        /// The game's water over everything clearer while the Vast Ocean is the place: the game's
        /// opacity for it (LiquidRenderer.DEFAULT_OPACITY for water, six tenths, the one number it
        /// draws all its water over everything at) set to <see cref="SeaWater"/>, and what it was
        /// put back when the place is left, and when the mod is unloaded.
        /// </summary>
        private static void ClearWater(bool here)
        {
            float[] opacity = LiquidRenderer.DEFAULT_OPACITY;

            if (opacity == null || opacity.Length == 0)
                return;

            if (here)
            {
                if (landWater < 0f)
                    landWater = opacity[0];

                opacity[0] = SeaWater;
            }
            else if (landWater >= 0f)
            {
                opacity[0] = landWater;
                landWater = -1f;
            }
        }

        private static void Drive(string key, float strength, Action<ScreenShaderData> set)
        {
            Filter filter = Filters.Scene[key];

            if (filter == null)
                return;

            bool on = strength > 0.002f;

            if (on)
            {
                set(filter.GetShader());

                if (!filter.IsActive())
                    Filters.Scene.Activate(key, Main.LocalPlayer.Center);
            }
            else if (filter.IsActive())
            {
                filter.Deactivate();
            }
        }

        /// <summary>The last push of a set of waves given to the water (its set, twice, and the push), so that each is given once.</summary>
        private static int lastPush = -1;

        /// <summary>Where the set being given lies along the surface (its middle, in world pixels), and which way it runs.</summary>
        private static float setMiddle;
        private static int setWay;

        /// <summary>
        /// A set of waves once in a while, by request ("sometimes the water have seizures and dont
        /// die down completely ... either make it always active or once in a while", "i prefer once
        /// in a while"; it was a swell pushed into the water every twelfth of a second for four
        /// seconds, every twelve, which stood in humps and never quite settled): in calm weather
        /// about once a minute, one set in four going by flat; the rougher the weather
        /// (<see cref="Rough"/>) the oftener, every fifteen seconds at the roughest, and none flat.
        /// Three to five crests along one stretch of the surface on the screen (as asked: "maybe
        /// wave js at one sections"), the highest in the middle of the set, fourteen tiles apart
        /// (nine at the roughest), a trough halfway between each two; given to the game's own
        /// water twice, a quarter of a second apart and a quarter of the way on the second time,
        /// which sets them running one way (with the wind in rough weather), and then left to it:
        /// the game's water carries them off and lets them die down, as it does the rings round a
        /// splash. And in rain, rings where the drops land (<see cref="RainOnTheWater"/>).
        /// </summary>
        private static void Waves(float rough)
        {
            float surfaceY = VastOcean.Surface * 16f + 8f;

            if (surfaceY < Main.screenPosition.Y - 64f || surfaceY > Main.screenPosition.Y + Main.screenHeight + 64f)
                return;

            if (Filters.Scene["WaterDistortion"]?.GetShader() is not WaterShaderData water)
                return;

            RainOnTheWater(water, surfaceY);

            float every = MathHelper.Lerp(55f, 15f, rough);
            float t = Main.GlobalTimeWrappedHourly;
            int set = (int)(t / every);
            float into = t - set * every;
            int push = into < 0.25f ? 0 : into < 0.5f ? 1 : -1;
            int key = set * 2 + push;

            if (push < 0 || key == lastPush)
                return;

            float h = Hash(set * 41 + 13);

            // A set going by flat; or the second push of one whose first was never given.
            if (h < 0.25f * (1f - rough) || push == 1 && lastPush != set * 2)
            {
                lastPush = key;
                return;
            }

            if (push == 0)
            {
                setMiddle = Main.screenPosition.X + Main.screenWidth * (0.25f + 0.5f * Hash(set * 59 + 7));
                setWay = rough > 0.3f && Math.Abs(Main.windSpeedCurrent) > 0.1f ? Math.Sign(Main.windSpeedCurrent) : Hash(set * 67 + 5) < 0.5f ? -1 : 1;
            }

            lastPush = key;

            int crests = 3 + (int)Math.Round(2f * rough + Hash(set * 71 + 3));
            float apart = MathHelper.Lerp(14f, 9f, rough) * 16f;
            float strength = Math.Min(1f, (0.55f + 0.25f * h) * (1f + 0.6f * rough));
            Vector2 size = Vector2.Lerp(new Vector2(150f, 34f), new Vector2(190f, 44f), rough);
            float shift = push * apart * 0.25f * setWay;

            // Each crest with a trough halfway to the next, as much down as the crest is up, so the
            // water is left level when they have gone (by request: "why are there blue lines here").
            for (int k = 0; k < crests; k++)
            {
                float crest = setMiddle + (k - (crests - 1) * 0.5f) * apart + shift;
                float envelope = (float)Math.Sin((k + 0.5f) / crests * MathHelper.Pi);

                water.QueueRipple(new Vector2(crest, surfaceY), strength * envelope, size, RippleShape.Circle, 0f);

                if (k < crests - 1)
                    water.QueueRipple(new Vector2(crest + apart * 0.5f, surfaceY), -strength * envelope, size, RippleShape.Circle, 0f);
            }
        }

        /// <summary>
        /// Rain on the sea where its surface is on the screen: each tick a ring or a few in the
        /// game's water where drops land, up to six a tick in the heaviest rain; none on the
        /// platform. Each pushes the water up or down, as likely one as the other, so that they
        /// leave it level as the sets of waves do (<see cref="Waves"/>). (The game puts a splash of its
        /// own where each of its drops hits water, Rain.Update.)
        /// </summary>
        private static void RainOnTheWater(WaterShaderData water, float surfaceY)
        {
            if (!Main.raining || Main.cloudAlpha < 0.05f)
                return;

            float drops = 6f * Smooth(0.05f, 0.8f, Main.cloudAlpha);
            int count = (int)drops + (Main.rand.NextFloat() < drops - (int)drops ? 1 : 0);

            for (int i = 0; i < count; i++)
            {
                float x = Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth);
                int column = (int)(x / 16f);

                if (!WorldGen.InWorld(column, VastOcean.Surface) || WorldGen.SolidTile(column, VastOcean.Surface))
                    continue;

                float wide = Main.rand.NextFloat(14f, 26f);
                float push = Main.rand.NextFloat(0.15f, 0.3f) * (Main.rand.NextBool() ? 1f : -1f);

                water.QueueRipple(new Vector2(x, surfaceY), push, new Vector2(wide, wide * 0.5f), RippleShape.Circle, 0f);
            }
        }

        // ---- the water behind --------------------------------------------------------------------

        /// <summary>
        /// The overlays of two of the game's own layers, in the Vast Ocean: the sea behind
        /// (RenderLayers.BackgroundWater, drawn right after the game's water behind everything),
        /// and the light in it in front (RenderLayers.ForegroundWater, drawn right after the
        /// game's water over everything): the rays and their sparkles, and the glints on the
        /// surface, are in the water between the eye and all else.
        /// </summary>
        private static void Layers(On_OverlayManager.orig_Draw orig, OverlayManager self, SpriteBatch spriteBatch, RenderLayers layer, bool beginSpriteBatch)
        {
            bool behind = layer == RenderLayers.BackgroundWater;

            if ((behind || layer == RenderLayers.ForegroundWater) && !beginSpriteBatch && ReferenceEquals(self, Overlays.Scene) && ready
                && SubworldKeeper.Current is VastOcean && light != null && light.IsLoaded && ray.IsLoaded)
            {
                // The game's batch is open here, in world space with the camera's zoom, sampling the
                // nearest texel (Main.DefaultSamplerState): the light's pictures are drawn stretched,
                // so they are sampled smoothly, and the game's batch is begun again as it was. In
                // front, the fan of light and the sun's shafts hide some of what is under them as well
                // as adding to it (Veil), so the order they are drawn in matters: drawn in the order
                // given (SpriteSortMode.Deferred), the fan first, the rays over it.
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

                try
                {
                    float day = Daylight();

                    if (behind)
                    {
                        DrawWater(spriteBatch, day);
                        DrawShadows(spriteBatch);
                        DrawSnow(spriteBatch, day);

                        // Over the sea, the water the lamps' rays pass, lit to the pixel (MupralLight),
                        // drawn in a batch of its own: this batch ended first, so that what is in it
                        // is drawn under that.
                        spriteBatch.End();
                        MupralLight.DrawLitWater();
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                    }
                    else
                    {
                        BeginLight();
                        DrawLightFan(spriteBatch, day);
                        DrawCaustics(spriteBatch, day);
                        DrawRays(spriteBatch, day);
                        DrawSunMotes(spriteBatch, day);
                        DrawPath(spriteBatch, day);
                        DrawGlints(spriteBatch, day);
                        DrawFoam(spriteBatch, day, Rough());
                        DrawFlash(spriteBatch);
                        VastOceanMotes.DrawGlow(spriteBatch);

                        // The schools' own light and their glints (NPCs/VastOcean: VastOceanSchools).
                        NPCs.Pseudo.PseudoNPCs.DrawGlow(spriteBatch);
                        NPCs.Ocean.VastOceanSchools.DrawGlints(spriteBatch);

                        // The bubbles are the game's own pictures of them, laid over what is behind
                        // them as they are (not added to it as light), pixel for pixel as the game
                        // draws its own (Main.DefaultSamplerState), in the order they rose.
                        spriteBatch.End();
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                        VastOceanMotes.DrawBubbles(spriteBatch);
                    }
                }
                finally
                {
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                }
            }

            orig(self, spriteBatch, layer, beginSpriteBatch);
        }

        /// <summary>
        /// The water's colour, and the haze over the horizon, strip by strip down the screen; the
        /// strip the line of the water crosses split there (<see cref="WaterTop"/>), so that none of
        /// the water's colour is over the game's water, where its waves would leave it behind.
        /// </summary>
        private static void DrawWater(SpriteBatch batch, float day)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float night = 0.30f + 0.70f * day;
            Color haze = Color.Lerp(Main.ColorOfTheSkies, Color.White, 0.30f);
            int top = WaterTop();

            for (int sy = 0; sy < Main.screenHeight; sy += Strip)
            {
                // How much of the strip is over the water.
                int over = Math.Clamp(top - sy, 0, Strip);

                if (over > 0)
                {
                    // The haze over the horizon: thickest at the waterline, gone sixteen rows up.
                    float above = (Main.screenPosition.Y + sy + over * 0.5f) / 16f - (VastOcean.Surface + 0.5f);
                    float thick = Smooth(-16f, 0f, above) * 0.42f;

                    if (thick > 0.004f)
                        batch.Draw(pixel, new Rectangle(0, sy, Main.screenWidth, over), haze * thick);
                }

                if (over == Strip)
                    continue;

                float rows = Math.Max(0f, (Main.screenPosition.Y + sy + (over + Strip) * 0.5f) / 16f - (VastOcean.Surface + 0.5f));

                WaterAt(rows, out Color colour, out float cover);

                // Over the 5th section the water goes with the sky; the dark under it is the same at
                // any hour. Drawn as much brighter as the game's water over it will take away.
                float lit = MathHelper.Lerp(night, 1f, Smooth(VastOcean.NavyAt - VastOcean.Surface, VastOcean.BlackAt - VastOcean.Surface, rows)) * ThroughWater;
                Color drawn = new Color(Math.Min(255, (int)(colour.R * lit)), Math.Min(255, (int)(colour.G * lit)), Math.Min(255, (int)(colour.B * lit)));

                batch.Draw(pixel, new Rectangle(0, sy + over, Main.screenWidth, Strip - over), drawn * cover);
            }
        }

        /// <summary>
        /// The sea's light at this depth (rows under the surface) as it is seen: its colour by day
        /// and by night, as <see cref="DrawWater"/> lays it (before it is brightened for the game's
        /// water over it). What a bubble in the water catches (VastOceanMotes); white over the water.
        /// </summary>
        internal static Vector3 SeaGlow(float rows)
        {
            if (rows < 0f)
                return Vector3.One;

            WaterAt(rows, out Color colour, out _);

            float night = 0.30f + 0.70f * Daylight();
            float lit = MathHelper.Lerp(night, 1f, Smooth(VastOcean.NavyAt - VastOcean.Surface, VastOcean.BlackAt - VastOcean.Surface, rows));

            return colour.ToVector3() * lit;
        }

        private static void WaterAt(float rows, out Color colour, out float cover)
        {
            for (int i = 1; i < Water.Length; i++)
            {
                if (rows <= Water[i].rows)
                {
                    float t = (rows - Water[i - 1].rows) / (Water[i].rows - Water[i - 1].rows);

                    colour = Color.Lerp(Water[i - 1].colour, Water[i].colour, t);
                    cover = MathHelper.Lerp(Water[i - 1].cover, Water[i].cover, t);
                    return;
                }
            }

            colour = Water[^1].colour;
            cover = 1f;
        }

        /// <summary>
        /// Far off in the blue, now and then, something very large going by: a shark the size
        /// of a ship, once in a long while the Duke, each a darker shape on the dark, slower
        /// across the screen than the camera, as what is far away is.
        /// </summary>
        private static void DrawShadows(SpriteBatch batch)
        {
            const float Period = 80f;

            float t = Main.GlobalTimeWrappedHourly;
            int pass = (int)(t / Period);
            float along = t / Period - pass;

            float h1 = Hash(pass * 31 + 7);
            float h2 = Hash(pass * 57 + 3);
            float h3 = Hash(pass * 91 + 1);

            // One pass in three goes by empty.
            if (h3 < 0.33f)
                return;

            bool duke = h3 > 0.92f;
            int type = duke ? NPCID.DukeFishron : NPCID.Shark;
            float rows = VastOcean.SectionRows * (0.6f + 1.0f * h1);
            int dir = h2 < 0.5f ? -1 : 1;

            // Out of sight unless the screen is in the blue of the first two sections, not the
            // shallows and not the dark (a fifth to half a section down, to near the middle of the 3rd).
            float cameraRows = ScreenDepth();
            float seen = Smooth(0.2f * VastOcean.SectionRows, 0.47f * VastOcean.SectionRows, cameraRows)
                * (1f - Smooth(1.75f * VastOcean.SectionRows, 2.2f * VastOcean.SectionRows, cameraRows));
            float fade = Smooth(0f, 0.12f, along) * (1f - Smooth(0.88f, 1f, along));
            float strength = 0.34f * seen * fade;

            if (strength < 0.01f)
                return;

            Main.instance.LoadNPC(type);
            Texture2D body = TextureAssets.Npc[type].Value;
            int frames = Math.Max(1, Main.npcFrameCount[type]);
            int frameHigh = body.Height / frames;
            int frame = (int)(t * (duke ? 5f : 7f)) % frames;

            // Across three thousand pixels about where the camera was as it began, at a third of
            // the camera's pace sideways and half of it up and down: the sea is too wide to keep it
            // to one place in it.
            if (pass != shadowPass)
            {
                shadowPass = pass;
                shadowAbout = Main.screenPosition.X + Main.screenWidth * 0.5f;
            }

            float worldX = shadowAbout - 1500f * dir + 3000f * dir * along;
            float worldY = (VastOcean.Surface + rows) * 16f;
            Vector2 centre = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 at = new Vector2(
                (worldX - Main.screenPosition.X - centre.X) * 0.35f + centre.X,
                (worldY - Main.screenPosition.Y - centre.Y) * 0.5f + centre.Y);

            float scale = duke ? 2.6f : 5.5f + 1.5f * h1;
            Rectangle source = new Rectangle(0, frame * frameHigh, body.Width, frameHigh);
            SpriteEffects facing = dir > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            batch.Draw(body, at, source, new Color(0, 2, 6) * strength, 0f, source.Size() * 0.5f, scale, facing, 0f);
        }

        /// <summary>How many shafts of the sun's can be lit at once over a screen's width and 900 pixels more, at the most, and of the moon's: most of them under it (<see cref="Sparse"/>).</summary>
        private const int SunShafts = 60;
        private const int MoonShafts = 28;

        /// <summary>
        /// How many of the shafts there are, far to the side of the sun or the moon, for as many as
        /// under it: under it, all that are lit; far off, about one in seven (<see cref="Beneath"/>).
        /// </summary>
        private const float Sparse = 0.15f;

        /// <summary>
        /// How far from the way the light goes a shaft may lean, in radians either way: a degree;
        /// and how far it wavers, half a degree (by request: "little variation is fine but this too
        /// much"). They fanned out from a point over the light, so that across a wide screen one at
        /// its edge leaned some forty degrees further out than one under the light.
        /// </summary>
        private const float Lean = 0.0175f;
        private const float Waver = 0.008f;

        /// <summary>How much of the water's colour behind it a sun's shaft hides, at its brightest (<see cref="Veil"/>).</summary>
        private const float SunCover = 0.65f;

        /// <summary>
        /// The sun's rays by day, the moon's by night, as a swimmer sees them, by request ("make the
        /// rays directional to the sun? and actually realisitc? thinner rays would work better and
        /// shinnings too. little sparkles and the rays disappear and reappear like reali life
        /// snorkeling"; then "this ray physics too out of place, i need to be directional to the
        /// moon/sun", "with a spread on the surface"): thin shafts slanting down the way the light
        /// comes into the water (<see cref="Slant"/>), every one of them that way within a degree
        /// (<see cref="Lean"/>), each from its own place along the surface, most of them where the
        /// light comes in under the sun or the moon; each lit for a moment as the waves over it
        /// gather the light, shimmering as they move, then gone, and another lit somewhere else;
        /// brighter in patches that drift, where the swell gathers more of it (<see cref="Gathered"/>);
        /// a soft glow about each thin one, the light it scatters; now and then a broad soft one;
        /// motes catching the light in them (<see cref="Sparkle"/>). None under the platform: its
        /// shadow; none past ground too thick for the light, nor where something standing out of
        /// the water keeps it off (<see cref="LightStops"/>). Brightest over their first forty to
        /// ninety rows, where the water has taken the least of the light; the sun's reach through
        /// the first three sections, keeping half their light to near their end, gold, a deeper
        /// gold as the sun is low (by request, after a picture of sunlight in the sea: "use better
        /// color too"); the moon's a third as long, pale, as bright as the moon is full. The deeper
        /// the eye, the more it makes of what light there is: seen from the end of the 3rd, two
        /// and a half times as bright.
        /// </summary>
        private static void DrawRays(SpriteBatch batch, float day)
        {
            float surfaceY = (VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y;

            // Nothing on the screen is in reach of them.
            if (surfaceY > Main.screenHeight || surfaceY + VastOcean.SunRayRows * 16f < 0f)
                return;

            float eye = 1f + 1.5f * Smooth(0f, VastOcean.SunRayRows, ScreenDepth());
            float slant = Slant();
            float under = Overhead();
            // Gold, deeper gold as the sun is low (the picture's: "use better color too"); laid over
            // the clear blue of the sea as a veil, so that it is seen gold and not only paler.
            Color sun = Color.Lerp(new Color(255, 196, 104), new Color(255, 170, 80), SunLow());

            // The sun's by day, as bright as the sky (its clouds and its hour in that); the moon's by night.
            Shafts(batch, surfaceY, slant, SunShafts, 11, VastOcean.SunRayRows * 16f, (Main.dayTime ? day : 0f) * eye, sun, 0.34f, under, SunCover);
            Shafts(batch, surfaceY, slant, MoonShafts, 37, VastOcean.MoonRayRows * 16f, Moonlight() * eye, new Color(170, 190, 235), 0.16f, under, 0f);
        }

        /// <summary>
        /// Which way the light goes down through the water, in radians from straight down: the way
        /// it comes down to the water (<see cref="AirSlant"/>) bent by the water toward straight
        /// down (Snell's law, air to water, 1 to 1.333), so that even the light of a sun on the
        /// horizon goes down no more than 48.6 degrees off. As the day begins it leans right as it
        /// goes down (a sun in the left of the sky), as it ends left; the night the same.
        /// </summary>
        private static float Slant() => (float)Math.Asin(Math.Sin(AirSlant()) / 1.333);

        /// <summary>
        /// Which way the light comes down to the water, in radians from straight down: the game's
        /// sun rises at the left of its sky, is over the middle at noon and sets at the right, and
        /// its moon by night the same: level from the left as it rises, straight down at the
        /// middle of the day (or night), level from the right as it sets.
        /// </summary>
        private static float AirSlant()
        {
            double through = Main.time / (Main.dayTime ? Main.dayLength : Main.nightLength);

            return (float)((through - 0.5) * Math.PI);
        }

        /// <summary>
        /// The shafts of one light (the sun's or the moon's): each lit for a moment somewhere, then
        /// another; most of them where the light comes in under where it is in the sky
        /// (<paramref name="under"/>, the screen's pixels), few far to the side, by request
        /// ("anything below the sun or moon shines more rays than the rest is sparse"): a shaft
        /// rooted far from under it is lit less often, down to <see cref="Sparse"/> of the time, and
        /// less brightly, fading in and out rather than blinking as the screen moves past it
        /// (<see cref="Beneath"/>). Each goes down from its root the way the light does, as far as
        /// the light goes there (<see cref="LightStops"/>).
        /// </summary>
        private static void Shafts(SpriteBatch batch, float surfaceY, float slant, int count, int salt, float length, float strength, Color colour, float bright, float under, float cover)
        {
            if (strength < 0.01f || surfaceY + length * (float)Math.Cos(slant) < 0f)
                return;

            Texture2D beam = ray.Value;

            // The ray's picture is brightest at its root (its bottom row) and falls off to its tip:
            // over its lowest three eighths near evenly to a half, and over the rest from there to
            // nothing. Those three eighths are laid along the first 85 hundredths of a shaft and the
            // rest along the remainder, so that a shaft keeps half its light to near its end, and
            // fades out there.
            int halfway = beam.Height * 5 / 8;
            Rectangle body = new Rectangle(0, halfway, beam.Width, beam.Height - halfway);
            Rectangle tip = new Rectangle(0, 0, beam.Width, halfway);

            float t = Main.GlobalTimeWrappedHourly;
            float lean = (float)Math.Tan(slant);

            // The light that can be seen comes in from where the light seen at the top of the water
            // does to where the light at the screen's foot does, and 450 pixels past either (the
            // broadest shaft, and its glow). Each shaft has a stretch of the surface of its own, fixed
            // in the water, as many of them to a screen's width and 900 pixels more as there are
            // shafts. (They were spread along a stretch as wide as the screen and more, each put back
            // the other side as the screen moved past it, out of sight; but that stretch was the wider
            // the more the light leaned, and widened as the sun went over, and a shaft went as many
            // times as far as the screen is stretches from the world's edge: some 90 pixels a second
            // across the water at the island, standing still.)
            float seenTop = Math.Max(0f, -surfaceY);
            float seenFoot = Main.screenHeight - surfaceY;
            float from = Math.Min(seenTop * lean, seenFoot * lean) - 450f;
            float to = Math.Max(seenTop * lean, seenFoot * lean) + Main.screenWidth + 450f;
            float stretch = (Main.screenWidth + 900f) / count;
            int firstStretch = (int)Math.Floor((Main.screenPosition.X + from - 18f) / stretch);
            int lastStretch = (int)Math.Floor((Main.screenPosition.X + to + 18f) / stretch);

            for (int i = firstStretch; i <= lastStretch; i++)
            {
                // Each lights a shaft for a moment somewhere along its stretch, then somewhere else
                // along it, at its own pace.
                float clock = t / (1.6f + 2.8f * Hash(i * 17 + salt)) + Hash(i * 23 + salt * 5);
                int turn = (int)Math.Floor(clock);
                float u = clock - turn;
                int seed = i * 7919 + turn * 104729 + salt * 31;

                float h1 = Hash(seed), h2 = Hash(seed + 1), h3 = Hash(seed + 2), h4 = Hash(seed + 3), h5 = Hash(seed + 4);

                // One turn in four lights nothing.
                if (h4 < 0.25f)
                    continue;

                // Where it comes in: somewhere along its stretch for its moment, fixed in the water,
                // sliding a little as the waves go.
                float x = (i + h1) * stretch + 36f * (u - 0.5f) * (h2 < 0.5f ? -1f : 1f) - Main.screenPosition.X;

                if (x < from || x > to)
                    continue;

                // Lit as often as it comes in near to under the light, and as brightly: all under
                // it, few far off; a shaft at the edge of that fades in or out as the screen moves.
                float beneath = Beneath(x, under, Main.screenWidth * 0.26f);
                float h6 = Hash(seed + 5);
                float kept = Smooth(h6 - 0.08f, h6 + 0.08f, Sparse + (1f - Sparse) * beneath);

                if (kept < 0.01f)
                    continue;

                // Lit quickly, fading more slowly; shimmering all the while as the waves move over it;
                // brighter where the swell over it gathers more of the light.
                float lit = Smooth(0f, 0.18f, u) * (1f - Smooth(0.42f, 1f, u));
                float shimmer = 0.72f + 0.28f * (float)(Math.Sin(t * (5f + 4f * h2) + h1 * 31f) * Math.Sin(t * (2.3f + 1.7f * h3) + h2 * 17f));
                bool broad = h5 > 0.82f;
                float glow = bright * (0.45f + 0.55f * h3) * strength * lit * shimmer * Gathered(x + Main.screenPosition.X, t) * (broad ? 0.3f : 1f)
                    * kept * (0.6f + 0.4f * beneath);

                if (glow < 0.004f)
                    continue;

                // Under the platform, its shadow.
                int column = (int)Math.Floor((x + Main.screenPosition.X) / 16f);

                if (WorldGen.InWorld(column, VastOcean.Surface) && WorldGen.SolidTile(column, VastOcean.Surface))
                    continue;

                // Down the way the light goes, within a degree, wavering a little; as far as its
                // light reaches, and no further than the light goes there.
                float angle = slant + Lean * (2f * h3 - 1f) + Waver * (float)Math.Sin(t * (0.6f + 0.4f * h1) + h5 * 9f);
                float reach = length * (0.85f + 0.15f * h5);
                float cut = Math.Min(reach, LightStops(column) / (float)Math.Cos(angle));

                if (cut < 16f)
                    continue;

                // Mostly thin, half a tile to a couple of tiles; now and then a broad soft one.
                float wide = broad ? 70f + 80f * h1 : 9f + 30f * h2 * h2;

                Vector2 root = new Vector2(x, surfaceY);
                Vector2 down = new Vector2(-(float)Math.Sin(angle), (float)Math.Cos(angle));
                float rotation = MathHelper.Pi + angle;

                // The shaft (hiding so much of the water's colour behind it: <paramref name="cover"/>);
                // about a thin one a soft glow, the light it scatters; and over its first forty to
                // ninety rows more of it, where the water has taken the least of the light.
                Beam(batch, beam, body, tip, root, down, rotation, wide, reach, cut, cover > 0f ? Veil(colour, glow, cover) : Lit(colour, glow));

                if (!broad)
                    Beam(batch, beam, body, tip, root, down, rotation, Math.Max(wide * 3.5f, 36f), reach, cut, Lit(colour, glow * 0.18f));

                float first = (40f + 50f * h3) * 16f;

                Beam(batch, beam, body, tip, root, down, rotation, wide, first, Math.Min(cut, first), Lit(colour, glow * 0.6f));

                // Motes catching its light: now and then a sparkle somewhere along what of it is seen.
                float near = Math.Max(0f, -surfaceY / down.Y);
                float far = Math.Min(cut, (Main.screenHeight - surfaceY) / down.Y);

                for (int k = 0; k < 3 && far > near; k++)
                {
                    float twinkleClock = t / (0.45f + 0.6f * Hash(seed + 10 + k)) + Hash(seed + 20 + k);
                    int twinkleTurn = (int)Math.Floor(twinkleClock);
                    float v = twinkleClock - twinkleTurn;

                    if (v > 0.3f)
                        continue;

                    int mote = seed * 31 + k * 977 + twinkleTurn * 7717;
                    float along = near + (far - near) * Hash(mote);
                    float aside = wide * 0.35f * (Hash(mote + 1) * 2f - 1f);
                    Vector2 at = root + down * along + new Vector2(down.Y, -down.X) * aside;
                    float twinkle = (float)Math.Sin(v / 0.3f * MathHelper.Pi);

                    Sparkle(batch, at, colour, glow * 3f * twinkle * (1f - 0.7f * along / reach), 2.5f + 3f * Hash(mote + 2));
                }
            }
        }

        /// <summary>
        /// A shaft from its root, as long as <paramref name="reach"/> makes it but no further than
        /// <paramref name="cut"/>: the ray's picture, its body along 85 hundredths of the reach and
        /// its tip along the rest; cut short, only as much of the picture as is that near its root
        /// (its bottom rows), so that the shaft ends where its light is stopped.
        /// </summary>
        private static void Beam(SpriteBatch batch, Texture2D beam, Rectangle body, Rectangle tip, Vector2 root, Vector2 down, float rotation, float wide, float reach, float cut, Color colour)
        {
            float bodyLength = reach * 0.85f;
            float bodyShown = Math.Min(cut, bodyLength);

            if (bodyShown <= 0f)
                return;

            int rows = Math.Max(1, (int)Math.Round(body.Height * bodyShown / bodyLength));

            batch.Draw(beam, root, new Rectangle(body.X, body.Bottom - rows, body.Width, rows), colour, rotation, new Vector2(beam.Width * 0.5f, rows),
                new Vector2(wide / beam.Width, bodyShown / rows), SpriteEffects.None, 0f);

            float tipShown = Math.Min(cut, reach) - bodyLength;

            if (tipShown <= 0f)
                return;

            int tipRows = Math.Max(1, (int)Math.Round(tip.Height * tipShown / (reach - bodyLength)));

            batch.Draw(beam, root + down * bodyLength, new Rectangle(tip.X, tip.Bottom - tipRows, tip.Width, tipRows), colour, rotation, new Vector2(beam.Width * 0.5f, tipRows),
                new Vector2(wide / beam.Width, tipShown / tipRows), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// How much of the light the swell over this column of the sea gathers into the water under
        /// it, 0.3 to 1.3 times as much: two long slow waves across each other, drifting, so that the
        /// shafts come in bright patches with dimmer water between, moving on.
        /// </summary>
        private static float Gathered(float worldX, float t)
        {
            float swell = 0.5f + 0.5f * (float)(Math.Sin(worldX / 230f + t * 0.35f) * Math.Sin(worldX / 520f - t * 0.21f + 1.3f));

            return 0.3f + Smooth(0f, 1f, swell);
        }

        // ---- where the light is stopped -------------------------------------------------------------

        /// <summary>
        /// How thick, in tiles, solid ground must be, both across and up and down, to stop the
        /// light, by request ("rays shining thru tiles is kinda gay, dont shine thru thick chunk
        /// like these, most can shine thru is 3 tiles ( like the beams)"): the light goes through
        /// anything three tiles thick or less, a post, a deck, a ledge, a hanging rock, and stops at
        /// the first tile with four or more of solid ground through it both ways (<see cref="Thick"/>).
        /// </summary>
        private const int Stops = 4;

        /// <summary>How far back toward the light, in tiles along it, what stands out of the water is looked for: it shades the water behind it.</summary>
        private const int AirReach = 48;

        /// <summary>How many tiles may be looked at in a frame to bring what was reckoned before up to date (what was never reckoned, or must go deeper, is reckoned at once).</summary>
        private const int LooksPerFrame = 60000;

        /// <summary>How far the light may have turned, in radians, and how many frames may have gone by, before what was reckoned for a column is reckoned again.</summary>
        private const float SlantKept = 0.005f;
        private const int FramesKept = 150;

        /// <summary>
        /// For each column of the surface: how far under it, in pixels, the light coming in there
        /// goes before ground too thick for it (0 where something standing out of the water keeps
        /// the light off it there; past as deep as was reckoned, not stopped); how deep that was
        /// reckoned; for which slant of the light; in which frame; in which world.
        /// </summary>
        private static float[] stopped;
        private static float[] reckoned;
        private static float[] reckonedSlant;
        private static uint[] reckonedAt;
        private static int reckonedWorld;

        /// <summary>This frame's: the light's slant in the water and over it, the tangent of the first, how far under the surface what is seen goes (no further than the light goes), the frame, and how many more tiles may be looked at.</summary>
        private static float lightSlant, lightAir, lightLean, lightDeep;
        private static uint lightFrame;
        private static int looksLeft;

        /// <summary>Once a frame, before the light in the water is drawn: what <see cref="LightStops"/> reckons by.</summary>
        private static void BeginLight()
        {
            lightFrame++;
            lightSlant = Slant();
            lightAir = AirSlant();
            lightLean = (float)Math.Tan(lightSlant);
            lightDeep = MathHelper.Clamp(Main.screenPosition.Y + Main.screenHeight - (VastOcean.Surface + 0.5f) * 16f, 0f, VastOcean.SunRayRows * 16f);
            looksLeft = LooksPerFrame;

            if (stopped == null || stopped.Length != Main.maxTilesX || reckonedWorld != Main.worldID)
            {
                stopped = new float[Main.maxTilesX];
                reckoned = new float[Main.maxTilesX];
                reckonedSlant = new float[Main.maxTilesX];
                reckonedAt = new uint[Main.maxTilesX];
                reckonedWorld = Main.worldID;
                Array.Fill(reckonedSlant, float.NaN);
            }
        }

        /// <summary>
        /// How far under the surface, in pixels, the light coming in at this column goes before it
        /// is stopped by ground too thick for it (<see cref="Stops"/>): 0 where something standing
        /// out of the water keeps it off the water there (looked for as far as <see cref="AirReach"/>
        /// tiles back toward the light); past the deepest seen, as good as never. Reckoned along
        /// the light from the column's middle, and kept: reckoned again once the light has turned
        /// a little, or now and then (the ground may have changed), and on down as more of the
        /// depths come into sight. The sun's shafts, the fan of light about them, the light under
        /// the surface and the motes are all drawn only where it reaches.
        /// </summary>
        private static float LightStops(int column)
        {
            if (stopped == null || column < 0 || column >= stopped.Length)
                return float.PositiveInfinity;

            bool known = !float.IsNaN(reckonedSlant[column]);

            if (!known || looksLeft > 0 && (Math.Abs(reckonedSlant[column] - lightSlant) > SlantKept || lightFrame - reckonedAt[column] > FramesKept))
                Reckon(column);
            else if (reckoned[column] < lightDeep && float.IsPositiveInfinity(stopped[column]))
                Deepen(column);

            return stopped[column];
        }

        /// <summary>Where the light comes into the water at this column: the middle of its surface tile, on the line of the water.</summary>
        private static Vector2 RootOf(int column) => new Vector2(column * 16f + 8f, (VastOcean.Surface + 0.5f) * 16f);

        /// <summary>What the light does at this column, reckoned afresh: first back up toward the light, over the water; then down into it, as deep as is seen.</summary>
        private static void Reckon(int column)
        {
            reckonedSlant[column] = lightSlant;
            reckonedAt[column] = lightFrame;

            float air = AirReach * 16f;

            if (Through(RootOf(column), new Vector2((float)Math.Sin(lightAir), -(float)Math.Cos(lightAir)), air) < air)
            {
                stopped[column] = 0f;
                reckoned[column] = float.PositiveInfinity;
                return;
            }

            stopped[column] = float.PositiveInfinity;
            reckoned[column] = 0f;
            Deepen(column);
        }

        /// <summary>What the light does at this column, reckoned on down from as deep as it was to as deep as is seen.</summary>
        private static void Deepen(int column)
        {
            float from = reckoned[column];
            float cos = (float)Math.Cos(lightSlant);
            float more = (lightDeep - from) / cos;

            if (more <= 0f)
                return;

            Vector2 down = new Vector2(-(float)Math.Sin(lightSlant), cos);
            float along = Through(RootOf(column) + down * (from / cos), down, more);

            if (along < more)
                stopped[column] = from + along * cos;

            reckoned[column] = lightDeep;
        }

        /// <summary>
        /// How far, in pixels, from a point along a way (a unit) the first tile of ground too thick
        /// for the light is, up to <paramref name="most"/>: that, if there is none, or if the way
        /// leaves the world first. Every tile the way passes through, in turn, each counted off
        /// this frame's share.
        /// </summary>
        private static float Through(Vector2 from, Vector2 way, float most)
        {
            float sx = from.X / 16f, sy = from.Y / 16f;
            int x = (int)Math.Floor(sx), y = (int)Math.Floor(sy);
            int stepX = way.X > 0f ? 1 : -1, stepY = way.Y > 0f ? 1 : -1;
            float eachX = way.X != 0f ? Math.Abs(1f / way.X) : float.PositiveInfinity;
            float eachY = way.Y != 0f ? Math.Abs(1f / way.Y) : float.PositiveInfinity;
            float nextX = way.X > 0f ? (x + 1 - sx) * eachX : way.X < 0f ? (sx - x) * eachX : float.PositiveInfinity;
            float nextY = way.Y > 0f ? (y + 1 - sy) * eachY : way.Y < 0f ? (sy - y) * eachY : float.PositiveInfinity;
            float end = most / 16f, at = 0f;

            while (at <= end && x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY)
            {
                looksLeft--;

                if (Thick(x, y))
                    return Math.Min(at * 16f, most);

                if (nextX < nextY)
                {
                    at = nextX;
                    nextX += eachX;
                    x += stepX;
                }
                else
                {
                    at = nextY;
                    nextY += eachY;
                    y += stepY;
                }
            }

            return most;
        }

        /// <summary>Whether this tile is in ground too thick for the light: solid, with solid ground <see cref="Stops"/> tiles or more through it across and as many up and down.</summary>
        private static bool Thick(int x, int y)
        {
            if (!Solid(x, y))
                return false;

            int across = 1;

            for (int k = 1; k < Stops && Solid(x - k, y); k++)
                across++;
            for (int k = 1; across < Stops && Solid(x + k, y); k++)
                across++;

            if (across < Stops)
                return false;

            int high = 1;

            for (int k = 1; k < Stops && Solid(x, y - k); k++)
                high++;
            for (int k = 1; high < Stops && Solid(x, y + k); k++)
                high++;

            return high >= Stops;
        }

        /// <summary>Whether this tile stops what moves: there, not switched off, solid and not a platform. (A wooden beam is not solid, nor a stalactite.)</summary>
        private static bool Solid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[x, y];

            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        // ---- the light about the rays ---------------------------------------------------------------

        /// <summary>
        /// The fan of light about the rays, three over each other: each the fan's picture
        /// (VastOceanFan) laid from the surface under the light down the way the light goes, so
        /// long along it (sections), so wide at its far end (screen widths), in its colour by day and
        /// by night, so strong, and so much of the sea's own colour behind it hidden by it (as the
        /// sun's light hides the water's colour looking toward it: <see cref="Veil"/>). Leaning with
        /// the light, it is sheared, not turned, so that its top lies along the surface whatever the
        /// hour (turned, its broad top tipped up out of the water on one side when the sun was low:
        /// "why is this gradiant golden thing side way"). The first
        /// gold, short and narrow, hiding near all of the sea's colour where it is brightest; the
        /// second a paler gold, longer and wider; the third the sea's own clear blue going green,
        /// longest and widest and faintest, only added: the water gives back the light the deeper
        /// and the further from the rays the bluer, darkening into the sea's own colour.
        /// </summary>
        private static readonly (float length, float wide, Color day, Color night, float strength, float cover)[] FanLayers =
        {
            (0.35f, 0.50f, new Color(255, 186, 92), new Color(214, 226, 255), 0.60f, 0.95f),
            (0.80f, 0.95f, new Color(250, 214, 140), new Color(140, 168, 228), 0.20f, 0.45f),
            (1.60f, 1.60f, new Color(40, 170, 205), new Color(60, 100, 168), 0.12f, 0f),
        };

        /// <summary>The fan's picture at its brightest, as <see cref="FanAt"/> reckons it (the picture itself is made to reach 1 there).</summary>
        private const float FanPeak = 0.925f;

        /// <summary>The caustics' picture: so many frames of this height one under another, a loop, played this many a second.</summary>
        private const int CausticFrames = 16;
        private const int CausticHigh = 48;
        private const float CausticPace = 7f;

        /// <summary>
        /// The light the water gives back about the rays, by request ("Can you also make the water
        /// more light gradient around the rays? use better color too", after a picture of sunlight
        /// in a green sea; "make it mesmerizingly beautiful"): under the sun by day, a fan of its
        /// light opening down into the sea the way the light goes, gold near the surface going
        /// green and fading into the sea's colour the deeper and the further to the side
        /// (<see cref="FanLayers"/>), breathing as the swell over it gathers more of the light or
        /// less; where it comes in, a glare along the surface and a hot heart in it. By night the
        /// moon's, silver, fainter. None under the platform (its shadow: as much less as the
        /// surface over the light is covered), and none where the light does not reach
        /// (<see cref="LightStops"/>): past ground too thick for it, it is cut off along the line
        /// of the light, as a shadow is.
        /// </summary>
        private static void DrawLightFan(SpriteBatch batch, float day)
        {
            if (fan == null || !fan.IsLoaded)
                return;

            float surfaceY = (VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y;
            float longest = FanLayers[^1].length * VastOcean.SectionRows * 16f;

            if (surfaceY > Main.screenHeight + 200f || surfaceY + longest < 0f)
                return;

            bool byDay = Main.dayTime;
            float under = Overhead();
            float strength = (byDay ? day : 0.45f * Moonlight()) * Open(under, Main.screenWidth * 0.3f);

            if (strength < 0.01f)
                return;

            Texture2D picture = fan.Value;
            Vector2 apex = new Vector2(under, surfaceY);
            float slant = Slant();
            float low = SunLow();
            float t = Main.GlobalTimeWrappedHourly;
            float breathe = 0.88f + 0.12f * (float)(Math.Sin(t * 0.7f) * Math.Sin(t * 0.43f + 1.7f));

            // Each layer as deep as it goes along the light, every row of its picture moved aside
            // the way the light leans by as far as it is under the surface; only where the light
            // reaches, cut exactly along it and across it (LayAlongLight); all three in one go.
            BeginLaying();

            // Where the light is stopped worked out once, as deep as the longest layer is seen.
            Shape(Math.Min(FanLayers[^1].length * VastOcean.SectionRows * 16f * (float)Math.Cos(slant), Main.screenHeight - surfaceY));

            for (int i = 0; i < FanLayers.Length; i++)
            {
                var (length, wide, dayColour, nightColour, share, cover) = FanLayers[i];
                Color colour = byDay ? Color.Lerp(dayColour, new Color(255, 190, 104), i == 0 ? low : 0.5f * low) : nightColour;
                Color veil = Veil(colour, share * strength * breathe, cover);
                float deep = length * VastOcean.SectionRows * 16f * (float)Math.Cos(slant);
                float half = wide * Main.screenWidth * 0.5f;

                LayPicture(apex.X - half, apex.X + half, deep, veil);
                LayAlongLight(apex.X - half, apex.X + half, surfaceY, 0f, deep, false, 0f);
            }

            DrawLaid(batch, picture, SamplerState.LinearClamp);

            // Where it comes in: a glare along the surface under the light, and a hot heart in it.
            Texture2D dot = light.Value;
            Vector2 middle = dot.Size() * 0.5f;
            Color glare = byDay ? Color.Lerp(new Color(255, 232, 170), new Color(255, 200, 116), low) : new Color(214, 226, 255);
            Color heart = byDay ? Color.Lerp(new Color(255, 246, 216), new Color(255, 214, 140), low) : new Color(226, 234, 255);

            batch.Draw(dot, apex, null, Lit(glare, 0.32f * strength * breathe), 0f, middle, new Vector2(Main.screenWidth * 0.5f / dot.Width, 80f / dot.Height), SpriteEffects.None, 0f);
            batch.Draw(dot, apex, null, Lit(heart, 0.50f * strength), 0f, middle, new Vector2(200f / dot.Width, 26f / dot.Height), SpriteEffects.None, 0f);
        }

        // ---- the light laid exactly along it -------------------------------------------------------

        /// <summary>
        /// The pieces of light being laid, as corners and the triangles between them, drawn straight
        /// into the picture with the game's own sprite effect as its batch leaves it
        /// (<see cref="DrawLaid"/>): the fan's three layers, and the light the waves gather under the
        /// surface. By request ("when moving these rays are super shaking and why is it so blocky?"):
        /// they were laid a row of their pictures at a time, 4 to 20 pixels high, each row cut where
        /// the light is stopped at the edge of the last whole pixel of the picture short of it (a
        /// sprite's part of its picture is in whole pixels), and a pixel of the fan's picture is 7
        /// to 24 of the screen's. Those pixels go with the picture, which is where the sun is on the
        /// screen, and the shadows with the world: as the screen moved, every cut jumped from one
        /// pixel's edge to the next, and its edge was steps. Laid now as pieces whose corners are
        /// exactly where the light is stopped, the picture's place at each reckoned to the fraction
        /// (<see cref="LayAlongLight"/>): a shadow's edge is a straight line along the light, and
        /// moves with the world a pixel at a time as the screen does.
        /// </summary>
        private static VertexPositionColorTexture[] laidCorners;
        private static short[] laidPieces;
        private static int laidCount, laidPieceCount;

        /// <summary>The most corners laid for one picture in a frame (the triangles' corners are counted in a short); past it, nothing more is laid.</summary>
        private const int MostLaid = 32000;

        /// <summary>
        /// Where the picture being laid is at a point (along the surface, where the light there came
        /// into the water, and how deep under it, the screen's pixels), as a fraction of it across,
        /// <see cref="layU"/> + <see cref="layUAlong"/> along + <see cref="layUDeep"/> deep, and down,
        /// <see cref="layV"/> + <see cref="layVDeep"/> deep; and where the surface is on the screen.
        /// </summary>
        private static float layU, layUAlong, layUDeep, layV, layVDeep, laySurface;

        /// <summary>
        /// The colour of what is being laid: <see cref="layColour"/> all over, or for the light under
        /// the surface (<see cref="layShaded"/>) its colour and strength by how near to under the sun
        /// or the moon each corner is (<see cref="CausticColour"/>).
        /// </summary>
        private static Color layColour, layWarm, layCool;
        private static bool layShaded;
        private static float layUnder, layStrength, layShare;

        /// <summary>A frame's laying begun: nothing laid.</summary>
        private static void BeginLaying()
        {
            laidCorners ??= new VertexPositionColorTexture[MostLaid];
            laidPieces ??= new short[MostLaid * 3];
            laidCount = 0;
            laidPieceCount = 0;
        }

        /// <summary>A picture to be laid from <paramref name="left"/> to <paramref name="right"/> along the surface, its top at the surface and its foot <paramref name="deep"/> under it, all of it in this colour.</summary>
        private static void LayPicture(float left, float right, float deep, Color colour)
        {
            layU = -left / (right - left);
            layUAlong = 1f / (right - left);
            layUDeep = 0f;
            layV = 0f;
            layVDeep = 1f / deep;
            layColour = colour;
            layShaded = false;
        }

        /// <summary>
        /// The picture set to be laid (<see cref="LayPicture"/>), from <paramref name="left"/> to
        /// <paramref name="right"/> along the surface (screen pixels, where the light came into the
        /// water) and from <paramref name="top"/> to <paramref name="foot"/> under it, every row of
        /// it moved aside as far as the light leans at that depth (sheared, not turned): where it is
        /// on the screen, and only where the light reaches, exactly (<see cref="Shape"/>), and not
        /// under anything on the surface if <paramref name="platform"/>. In pieces along the foot of
        /// the lit water (or pieces <paramref name="step"/> pixels along the surface, for a colour
        /// that changes along it): each piece's sides along the light, its foot where the light is
        /// stopped, a corner on a side where the next piece's foot meets it, so that the two meet
        /// corner to corner, with no crack and no overlap between them.
        /// </summary>
        private static void LayAlongLight(float left, float right, float surfaceY, float top, float foot, bool platform, float step)
        {
            laySurface = surfaceY;

            // What of it is on the screen: from its top or the screen's, to its foot or the screen's.
            float d0 = Math.Max(top, -surfaceY);
            float d1 = Math.Min(foot, Main.screenHeight - surfaceY);

            if (d1 <= d0)
                return;

            // The light seen there came in between these along the surface: the screen's width, and
            // as far aside as the light leans over the depths seen.
            float from = Math.Max(left, Math.Min(d0 * lightLean, d1 * lightLean));
            float to = Math.Min(right, Main.screenWidth + Math.Max(d0 * lightLean, d1 * lightLean));

            if (to <= from)
                return;

            Shape(d1);

            // This picture's share of the foot: as far along as it goes, no deeper than it and the
            // screen go, and at the surface under anything on it if so asked.
            partCount = 0;

            for (int k = 0; k < footCount; k++)
            {
                float a = Math.Max(from, footFrom[k]), b = Math.Min(to, footTo[k]);

                if (b > a)
                    AddPart(a, b, FootAt(k, a), FootAt(k, b), d1, platform);
            }

            for (int k = 0; k < partCount; k++)
            {
                float before = k > 0 && partTo[k - 1] == partFrom[k] ? partDeepTo[k - 1] : d0;
                float after = k + 1 < partCount && partFrom[k + 1] == partTo[k] ? partDeepFrom[k + 1] : d0;

                LayPart(partFrom[k], partTo[k], d0, partDeepFrom[k], partDeepTo[k], before, after, step);
            }
        }

        /// <summary>How deep the light is stopped at this point along the surface, in this piece of its foot.</summary>
        private static float FootAt(int k, float x) => Between(footFrom[k], footTo[k], footDeepFrom[k], footDeepTo[k], x);

        /// <summary>The depth at <paramref name="x"/> on the straight line from (<paramref name="a"/>, <paramref name="da"/>) to (<paramref name="b"/>, <paramref name="db"/>); either end exactly at it.</summary>
        private static float Between(float a, float b, float da, float db, float x)
        {
            if (x <= a || da == db)
                return da;
            if (x >= b)
                return db;

            return da + (db - da) * (x - a) / (b - a);
        }

        /// <summary>A stretch of a picture's foot from <paramref name="a"/> to <paramref name="b"/> (as deep as <paramref name="da"/> and <paramref name="db"/> at either end), no deeper than <paramref name="deepest"/>; under anything on the surface none of it, if <paramref name="platform"/>.</summary>
        private static void AddPart(float a, float b, float da, float db, float deepest, bool platform)
        {
            // Where a slanting stretch passes the deepest, cut there: past it, the deepest.
            if ((da > deepest) != (db > deepest) && da != db)
            {
                float x = a + (deepest - da) * (b - a) / (db - da);

                if (x > a && x < b)
                {
                    AddPart(a, x, Math.Min(da, deepest), deepest, deepest, platform);
                    AddPart(x, b, deepest, Math.Min(db, deepest), deepest, platform);
                    return;
                }
            }

            da = Math.Min(da, deepest);
            db = Math.Min(db, deepest);

            if (platform)
            {
                // Under each column with something on the surface, the light never comes in.
                float screenX = Main.screenPosition.X;
                int first = (int)Math.Floor((a + screenX) / 16f), last = (int)Math.Floor((b + screenX) / 16f);
                float at = a;

                for (int column = first; column <= last; column++)
                {
                    if (!WorldGen.InWorld(column, VastOcean.Surface) || !WorldGen.SolidTile(column, VastOcean.Surface))
                        continue;

                    float s = Math.Max(at, column * 16f - screenX), e = Math.Min(b, column * 16f + 16f - screenX);

                    if (e <= s)
                        continue;

                    if (s > at)
                        Append(at, s, Between(a, b, da, db, at), Between(a, b, da, db, s));

                    Append(s, e, 0f, 0f);
                    at = e;
                }

                if (at < b)
                    Append(at, b, Between(a, b, da, db, at), db);

                return;
            }

            Append(a, b, da, db);
        }

        /// <summary>A stretch added to this picture's foot, joined to the one before it if they meet level at one depth.</summary>
        private static void Append(float a, float b, float da, float db)
        {
            if (b <= a)
                return;

            if (partCount > 0 && partTo[partCount - 1] == a && da == db && partDeepFrom[partCount - 1] == da && partDeepTo[partCount - 1] == da)
            {
                partTo[partCount - 1] = b;
                return;
            }

            if (partCount == partFrom.Length)
            {
                Array.Resize(ref partFrom, partCount * 2);
                Array.Resize(ref partTo, partCount * 2);
                Array.Resize(ref partDeepFrom, partCount * 2);
                Array.Resize(ref partDeepTo, partCount * 2);
            }

            partFrom[partCount] = a;
            partTo[partCount] = b;
            partDeepFrom[partCount] = da;
            partDeepTo[partCount] = db;
            partCount++;
        }

        /// <summary>A picture's own stretches of the foot of the lit water, in order along the surface (<see cref="LayAlongLight"/>).</summary>
        private static float[] partFrom = new float[64], partTo = new float[64], partDeepFrom = new float[64], partDeepTo = new float[64];
        private static int partCount;

        /// <summary>
        /// A stretch of the foot from <paramref name="xLeft"/> to <paramref name="xRight"/> along the
        /// surface, the light reaching from <paramref name="top"/> down to <paramref name="footLeft"/>
        /// and <paramref name="footRight"/> at either end: laid in one piece, or pieces
        /// <paramref name="step"/> pixels along the surface each, on lines fixed in the world; the
        /// first given a corner on its left side where the stretch before ends
        /// (<paramref name="footBefore"/>), the last on its right where the one after does.
        /// </summary>
        private static void LayPart(float xLeft, float xRight, float top, float footLeft, float footRight, float footBefore, float footAfter, float step)
        {
            // None of it under the top; where it slants up past the top, from there.
            if (footLeft <= top && footRight <= top)
                return;

            if (footLeft < top || footRight < top)
            {
                float x = xLeft + (top - footLeft) * (xRight - xLeft) / (footRight - footLeft);

                if (footLeft < top)
                {
                    xLeft = x;
                    footLeft = top;
                    footBefore = top;
                }
                else
                {
                    xRight = x;
                    footRight = top;
                    footAfter = top;
                }
            }

            float a = xLeft;

            while (a < xRight)
            {
                float b = xRight;

                if (step > 0f)
                {
                    float line = (float)(Math.Floor((a + Main.screenPosition.X) / step) + 1.0) * step - Main.screenPosition.X;

                    if (line > a && line < xRight)
                        b = line;
                }

                LayPiece(a, b, top, Between(xLeft, xRight, footLeft, footRight, a), Between(xLeft, xRight, footLeft, footRight, b),
                    a == xLeft ? footBefore : top, b == xRight ? footAfter : top);
                a = b;
            }
        }

        /// <summary>
        /// One piece, from <paramref name="xLeft"/> to <paramref name="xRight"/> along the surface,
        /// from <paramref name="top"/> down to <paramref name="footLeft"/> at its left and
        /// <paramref name="footRight"/> at its right: its corners round it (top left, top right, one on
        /// its right side at <paramref name="rightAt"/> if that is between its top and foot there,
        /// bottom right, bottom left, one on its left side at <paramref name="leftAt"/> likewise), a
        /// triangle from a corner in its middle to each side, turned as the game's sprites are.
        /// </summary>
        private static void LayPiece(float xLeft, float xRight, float top, float footLeft, float footRight, float leftAt, float rightAt)
        {
            if (laidCount + 7 > MostLaid || xRight <= xLeft)
                return;

            int middle = laidCount;

            Corner((xLeft + xRight) * 0.5f, top + (footLeft + footRight - 2f * top) * 0.25f);

            int first = laidCount;

            Corner(xLeft, top);
            Corner(xRight, top);

            if (footRight > top)
            {
                if (rightAt > top && rightAt < footRight)
                    Corner(xRight, rightAt);

                Corner(xRight, footRight);
            }

            if (footLeft > top)
            {
                Corner(xLeft, footLeft);

                if (leftAt > top && leftAt < footLeft)
                    Corner(xLeft, leftAt);
            }

            int sides = laidCount - first;

            for (int k = 0; k < sides; k++)
            {
                laidPieces[laidPieceCount++] = (short)middle;
                laidPieces[laidPieceCount++] = (short)(first + k);
                laidPieces[laidPieceCount++] = (short)(first + (k + 1) % sides);
            }
        }

        // ---- where the light is stopped, exactly ----------------------------------------------------

        /// <summary>
        /// The foot of the lit water this frame (<see cref="Shape"/>), in pieces in order along the
        /// surface and meeting end to end, each from <see cref="footFrom"/> to <see cref="footTo"/>
        /// (screen pixels along the surface, where the light came in) and as deep at either end as
        /// <see cref="footDeepFrom"/> and <see cref="footDeepTo"/>: level along the top of a tile the
        /// light falls on, slanting along the side of one it runs into (the side toward the light,
        /// upright on the screen); at the surface where something standing out of the water keeps
        /// the light off; as deep as was asked where nothing stops it.
        /// </summary>
        private static float[] footFrom = new float[64], footTo = new float[64], footDeepFrom = new float[64], footDeepTo = new float[64];
        private static int footCount;

        /// <summary>The stretches of the surface whose light is not yet stopped as the foot is worked out, in order; and room to work the next in.</summary>
        private static float[] openFrom = new float[32], openTo = new float[32], nextFrom = new float[32], nextTo = new float[32];
        private static int openCount;

        /// <summary>Which of a row's tiles are thick (<see cref="Thick"/>), as the row is looked at.</summary>
        private static bool[] thickRow = new bool[256];

        /// <summary>The frame the foot was worked out in, and how deep.</summary>
        private static uint shapedFrame = uint.MaxValue;
        private static float shapedDeep;

        /// <summary>The pieces of the foot put in order along the surface.</summary>
        private static int[] footOrder = new int[64];

        /// <summary>
        /// The foot of the lit water worked out, if it was not already this frame as deep: over the
        /// stretch of the surface whose light can be seen on the screen, from as far over the water as
        /// the light is looked back along (<see cref="AirReach"/>) down to <paramref name="deep"/>,
        /// row by row of tiles. Over the water, a thick tile (<see cref="Thick"/>) in the light's way
        /// as it comes down at its slant in the air keeps it off the water there altogether; in the
        /// water, the light is stopped where it first meets one: on its top, or past its corner on its
        /// side toward the light (each row's tops first, as they are higher than its sides). Exactly,
        /// not a column of the surface at a time: by request ("why is it so blocky?"), the light falls
        /// past a tile's corner beside it, and stops on its top, to the pixel.
        /// </summary>
        private static void Shape(float deep)
        {
            if (shapedFrame == lightFrame && deep <= shapedDeep)
                return;

            shapedFrame = lightFrame;
            shapedDeep = deep;
            footCount = 0;
            openCount = 1;
            openFrom[0] = Math.Min(0f, deep * lightLean) - 1f;
            openTo[0] = Main.screenWidth + Math.Max(0f, deep * lightLean) + 1f;

            float screenX = Main.screenPosition.X;
            float line = (VastOcean.Surface + 0.5f) * 16f;

            // Over the water, from the surface's row up as far as the light is looked back along.
            float airLean = (float)Math.Tan(lightAir);
            float highest = AirReach * 16f * (float)Math.Cos(lightAir);

            for (int row = VastOcean.Surface; row >= 0 && openCount > 0; row--)
            {
                float low = Math.Max(0f, line - (row + 1) * 16f);
                float high = line - row * 16f;

                if (low > highest)
                    break;

                float near = Math.Min(low * airLean, high * airLean), far = Math.Max(low * airLean, high * airLean);
                int firstTile = (int)Math.Floor((openFrom[0] + screenX + near) / 16f);
                int lastTile = (int)Math.Floor((openTo[openCount - 1] + screenX + far) / 16f);

                if (!MarkRow(row, firstTile, lastTile, near, far, screenX, true))
                    continue;

                for (int k = 0; k <= lastTile - firstTile && openCount > 0; k++)
                {
                    if (!thickRow[k])
                        continue;

                    int tile = firstTile + k;

                    Close(tile * 16f - screenX - far, tile * 16f + 16f - screenX - near, 0f, 0f);
                }
            }

            // In the water, row by row down.
            float lean = lightLean;

            for (int row = VastOcean.Surface + 1; row < Main.maxTilesY && openCount > 0; row++)
            {
                float top = row * 16f - line;

                if (top >= deep)
                    break;

                float near = Math.Min(top * lean, (top + 16f) * lean), far = Math.Max(top * lean, (top + 16f) * lean);
                int firstTile = (int)Math.Floor((openFrom[0] + screenX - far) / 16f);
                int lastTile = (int)Math.Floor((openTo[openCount - 1] + screenX - near) / 16f);
                int tiles = lastTile - firstTile + 1;

                if (!MarkRow(row, firstTile, lastTile, near, far, screenX, false))
                    continue;

                // The tops, at the row's top: a tile's from where the light at that depth crosses its left edge to its right.
                for (int k = 0; k < tiles && openCount > 0; k++)
                {
                    if (!thickRow[k])
                        continue;

                    float a = (firstTile + k) * 16f - screenX + top * lean;

                    Close(a, a + 16f, top, top);
                }

                // The sides toward the light, down the row: what passed a tile's corner runs into its side.
                if (lean != 0f)
                {
                    for (int k = 0; k < tiles && openCount > 0; k++)
                    {
                        if (!thickRow[k])
                            continue;

                        float a = (firstTile + k) * 16f - screenX + top * lean;

                        if (lean > 0f)
                            Close(a + 16f, a + 16f + 16f * lean, top, top + 16f);
                        else
                            Close(a + 16f * lean, a, top + 16f, top);
                    }
                }
            }

            // What is open still is not stopped as deep as was asked.
            for (int k = 0; k < openCount; k++)
                AddFoot(openFrom[k], openTo[k], deep, deep);

            OrderFeet();
        }

        /// <summary>
        /// Which of a row's tiles under the light still open are thick (<see cref="Thick"/>), into
        /// <see cref="thickRow"/> (from <paramref name="firstTile"/>, as far as <paramref name="lastTile"/>);
        /// whether any. The light still open at this row crosses it from <paramref name="near"/> to
        /// <paramref name="far"/> pixels to the side of where it came in (as the row is in the air or
        /// the water, to one side or the other).
        /// </summary>
        private static bool MarkRow(int row, int firstTile, int lastTile, float near, float far, float screenX, bool air)
        {
            int tiles = lastTile - firstTile + 1;

            if (tiles <= 0)
                return false;

            if (tiles > thickRow.Length)
                thickRow = new bool[Math.Max(tiles, thickRow.Length * 2)];

            Array.Clear(thickRow, 0, tiles);

            bool any = false;

            for (int k = 0; k < openCount; k++)
            {
                int a = (int)Math.Floor((openFrom[k] + screenX + (air ? near : -far)) / 16f);
                int b = (int)Math.Floor((openTo[k] + screenX + (air ? far : -near)) / 16f);

                any |= MarkThick(row, Math.Max(a, firstTile), Math.Min(b, lastTile), firstTile);
            }

            return any;
        }

        /// <summary>
        /// Which of a row's tiles from <paramref name="from"/> to <paramref name="to"/> are thick
        /// (<see cref="Thick"/>), marked in <see cref="thickRow"/> from <paramref name="origin"/>; whether
        /// any. Looked at one tile in <see cref="Stops"/>: a thick tile has that many solid across
        /// through it, so every run of them has one of those in it; a run found is followed along,
        /// and if it is that many across or more its tiles are looked at up and down. (Most rows the
        /// light crosses are open water, and are passed over the faster.)
        /// </summary>
        private static bool MarkThick(int row, int from, int to, int origin)
        {
            if (to < from)
                return false;

            bool any = false;
            int lowest = from - Stops, highest = to + Stops;
            int followed = int.MinValue;
            int start = from - (Stops - 1);
            int over = ((start % Stops) + Stops) % Stops;

            // From the first multiple of Stops at or past the start, every Stops tiles.
            for (int x = over == 0 ? start : start + Stops - over; x <= to + Stops - 1; x += Stops)
            {
                if (x <= followed || !Solid(x, row))
                    continue;

                int a = x, b = x;

                while (a - 1 >= lowest && Solid(a - 1, row))
                    a--;
                while (b + 1 <= highest && Solid(b + 1, row))
                    b++;

                followed = b;

                if (b - a + 1 < Stops)
                    continue;

                // Across enough: thick if as many are solid up and down through it.
                for (int t = Math.Max(a, from); t <= Math.Min(b, to); t++)
                {
                    int high = 1;

                    for (int k = 1; k < Stops && Solid(t, row - k); k++)
                        high++;
                    for (int k = 1; high < Stops && Solid(t, row + k); k++)
                        high++;

                    if (high >= Stops)
                    {
                        thickRow[t - origin] = true;
                        any = true;
                    }
                }
            }

            return any;
        }

        /// <summary>
        /// The light still open between <paramref name="a"/> and <paramref name="b"/> along the
        /// surface stopped, as deep as <paramref name="da"/> at a and <paramref name="db"/> at b and in
        /// a straight line between: that is its foot there.
        /// </summary>
        private static void Close(float a, float b, float da, float db)
        {
            if (b <= a || b <= openFrom[0] || a >= openTo[openCount - 1])
                return;

            int next = 0;

            for (int k = 0; k < openCount; k++)
            {
                float p = openFrom[k], q = openTo[k];
                float s = Math.Max(p, a), e = Math.Min(q, b);

                if (e <= s)
                {
                    Keep(ref next, p, q);
                    continue;
                }

                AddFoot(s, e, Between(a, b, da, db, s), Between(a, b, da, db, e));

                if (p < s)
                    Keep(ref next, p, s);
                if (e < q)
                    Keep(ref next, e, q);
            }

            (openFrom, nextFrom) = (nextFrom, openFrom);
            (openTo, nextTo) = (nextTo, openTo);
            openCount = next;
        }

        /// <summary>A stretch kept open, into the list being made.</summary>
        private static void Keep(ref int next, float p, float q)
        {
            if (next == nextFrom.Length)
            {
                Array.Resize(ref nextFrom, next * 2);
                Array.Resize(ref nextTo, next * 2);
                Array.Resize(ref openFrom, next * 2);
                Array.Resize(ref openTo, next * 2);
            }

            nextFrom[next] = p;
            nextTo[next] = q;
            next++;
        }

        private static void AddFoot(float a, float b, float da, float db)
        {
            if (footCount == footFrom.Length)
            {
                Array.Resize(ref footFrom, footCount * 2);
                Array.Resize(ref footTo, footCount * 2);
                Array.Resize(ref footDeepFrom, footCount * 2);
                Array.Resize(ref footDeepTo, footCount * 2);
            }

            footFrom[footCount] = a;
            footTo[footCount] = b;
            footDeepFrom[footCount] = da;
            footDeepTo[footCount] = db;
            footCount++;
        }

        /// <summary>The foot's pieces put in order along the surface, and those that meet level at one depth, or along one slant, made one.</summary>
        private static void OrderFeet()
        {
            if (footOrder.Length < footCount)
                footOrder = new int[footFrom.Length];

            for (int k = 0; k < footCount; k++)
                footOrder[k] = k;

            // In order of where each begins (few, and mostly in order already).
            for (int k = 1; k < footCount; k++)
            {
                int item = footOrder[k];
                float key = footFrom[item];
                int j = k - 1;

                while (j >= 0 && footFrom[footOrder[j]] > key)
                {
                    footOrder[j + 1] = footOrder[j];
                    j--;
                }

                footOrder[j + 1] = item;
            }

            // Into the lists for the open stretches (free again now), joined where they meet alike.
            int count = 0;

            for (int k = 0; k < footCount; k++)
            {
                int i = footOrder[k];
                float a = footFrom[i], b = footTo[i], da = footDeepFrom[i], db = footDeepTo[i];

                if (count > 0 && nextTo[count - 1] == a && openTo[count - 1] == da && Alike(nextFrom[count - 1], openFrom[count - 1], a, da, b, db))
                {
                    nextTo[count - 1] = b;
                    openTo[count - 1] = db;
                    continue;
                }

                if (count == nextFrom.Length)
                {
                    Array.Resize(ref nextFrom, count * 2);
                    Array.Resize(ref nextTo, count * 2);
                    Array.Resize(ref openFrom, count * 2);
                    Array.Resize(ref openTo, count * 2);
                }

                nextFrom[count] = a;
                openFrom[count] = da;
                nextTo[count] = b;
                openTo[count] = db;
                count++;
            }

            for (int k = 0; k < count; k++)
            {
                footFrom[k] = nextFrom[k];
                footDeepFrom[k] = openFrom[k];
                footTo[k] = nextTo[k];
                footDeepTo[k] = openTo[k];
            }

            footCount = count;
            openCount = 0;
        }

        /// <summary>Whether a piece of the foot from (<paramref name="a0"/>, <paramref name="d0"/>) to (<paramref name="a1"/>, <paramref name="d1"/>) goes on in a straight line to (<paramref name="b"/>, <paramref name="db"/>): level at one depth, or along one slant.</summary>
        private static bool Alike(float a0, float d0, float a1, float d1, float b, float db)
        {
            if (d0 == d1)
                return db == d1;

            float slope = (d1 - d0) / (a1 - a0);
            float onward = (db - d1) / (b - a1);

            return Math.Abs(onward - slope) <= 1e-4f * Math.Max(1f, Math.Abs(slope));
        }

        /// <summary>A corner at this point (along the surface, where the light there came into the water; deep under it): on the screen as far aside as the light leans there, the picture's place at it, its colour.</summary>
        private static void Corner(float along, float deep)
        {
            float x = along - deep * lightLean;

            laidCorners[laidCount++] = new VertexPositionColorTexture(
                new Vector3(x, laySurface + deep, 0f),
                layShaded ? CausticColour(x) : layColour,
                new Vector2(layU + layUAlong * along + layUDeep * deep, layV + layVDeep * deep));
        }

        /// <summary>The light under the surface at this column of the screen: gold near to under the sun, a pale blue away from it, and stronger the nearer (<see cref="DrawCaustics"/>).</summary>
        private static Color CausticColour(float x)
        {
            float beneath = Beneath(x, layUnder, Main.screenWidth * 0.24f);

            return Lit(Color.Lerp(layCool, layWarm, beneath), layStrength * (0.18f + 0.82f * beneath) * layShare);
        }

        /// <summary>
        /// What has been laid, drawn now over what the batch has drawn so far, with this picture
        /// sampled so: the batch ended first (as it ends, its blending, sampling and the camera's
        /// transform are set and its effect applied, SpriteBatch.PrepRenderState, whether or not it
        /// had anything to draw), and begun again after as it was (<see cref="Layers"/>).
        /// </summary>
        private static void DrawLaid(SpriteBatch batch, Texture2D picture, SamplerState sampler)
        {
            if (laidPieceCount == 0)
                return;

            batch.End();

            try
            {
                GraphicsDevice device = batch.GraphicsDevice;

                device.Textures[0] = picture;
                device.SamplerStates[0] = sampler;
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, laidCorners, 0, laidCount, laidPieces, 0, laidPieceCount / 3);
            }
            finally
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            }
        }

        /// <summary>
        /// The surface from under it, by request ("maybe even throw in a shader or effect for
        /// surface"; drawn, no shader of the mod's own): the light the moving waves gather into the
        /// water just under them, a net of bright lines shifting all the while (VastOceanCaustics,
        /// a loop of frames made by working out where the light goes through a moving surface,
        /// each frame faded into the next), in a band under the surface drifting slowly the way
        /// the wind blows; bright and gold under the sun, a pale blue glimmer away from it, silver
        /// under the moon; none under the platform, nor where the light does not come into the
        /// water (<see cref="LightStops"/>).
        /// </summary>
        private static void DrawCaustics(SpriteBatch batch, float day)
        {
            const float Band = 40f;
            const float Segment = 32f;

            if (caustics == null || !caustics.IsLoaded)
                return;

            float surfaceY = (VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y;

            if (surfaceY + Band < 0f || surfaceY > Main.screenHeight)
                return;

            bool byDay = Main.dayTime;
            float strength = byDay ? day : 0.5f * Moonlight();

            if (strength < 0.02f)
                return;

            Texture2D picture = caustics.Value;
            float t = Main.GlobalTimeWrappedHourly;
            float clock = t * CausticPace;
            int frame = (int)Math.Floor(clock) % CausticFrames;
            int next = (frame + 1) % CausticFrames;
            float blend = clock - (float)Math.Floor(clock);
            float low = SunLow();

            // The picture fixed in the water, drifting along it slowly the way the wind blows, a
            // pixel of it to a pixel of the screen across, its frame's height to the band's; from
            // the water's first row down, none of it over the water (WaterTop). Only where the light
            // comes into the water and as deep into the band as it goes, cut exactly along the light
            // (LayAlongLight), in pieces a segment long, each corner as gold and as bright as it is
            // near to under the sun (or the moon); this frame faded into the next.
            float drift = t * (6f + 10f * Rough()) * (Main.windSpeedCurrent < 0f ? -1f : 1f);
            float bandTop = WaterTop() - surfaceY;

            layShaded = true;
            layUnder = Overhead();
            layStrength = strength;
            layWarm = byDay ? Color.Lerp(new Color(255, 236, 178), new Color(255, 200, 120), low) : new Color(200, 216, 255);
            layCool = byDay ? new Color(150, 228, 245) : new Color(120, 150, 210);
            float shift = (Main.screenPosition.X + drift) / picture.Width;

            layU = shift - (float)Math.Floor(shift);
            layUAlong = 1f / picture.Width;
            layUDeep = -lightLean / picture.Width;
            layVDeep = CausticHigh / Band / picture.Height;

            BeginLaying();

            for (int k = 0; k < 2; k++)
            {
                layV = ((k == 0 ? frame : next) * CausticHigh - bandTop * CausticHigh / Band) / picture.Height;
                layShare = k == 0 ? 1f - blend : blend;
                LayAlongLight(float.NegativeInfinity, float.PositiveInfinity, surfaceY, bandTop, bandTop + Band, true, Segment);
            }

            layShaded = false;
            DrawLaid(batch, picture, SamplerState.LinearWrap);
        }

        /// <summary>
        /// Motes in the water catching the light, by request ("make it mesmerizingly beautiful",
        /// after a picture of sunlight in the sea glittering in what drifts in it): in each cell of a
        /// grid fixed in the water, three in four of them, a speck drifting and sinking slowly, lit
        /// as brightly as the fan of light is where it is (<see cref="FanAt"/>, as its second layer
        /// is drawn), glimmering, now and then flashing; gold near the surface, a pale clear blue
        /// further down; silver by the moon. None where the light is not, nor under the platform.
        /// </summary>
        private static void DrawSunMotes(SpriteBatch batch, float day)
        {
            const float Cell = 30f;

            float surfaceY = (VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y;
            float length = FanLayers[1].length * VastOcean.SectionRows * 16f;

            if (surfaceY > Main.screenHeight || surfaceY + length < 0f)
                return;

            bool byDay = Main.dayTime;
            float under = Overhead();
            float strength = (byDay ? day : 0.5f * Moonlight()) * Open(under, Main.screenWidth * 0.3f);

            if (strength < 0.02f)
                return;

            Texture2D dot = light.Value;
            Vector2 middle = dot.Size() * 0.5f;
            float t = Main.GlobalTimeWrappedHourly;
            float slant = Slant();
            Vector2 apex = new Vector2(under, surfaceY);
            Vector2 down = new Vector2(-(float)Math.Sin(slant), (float)Math.Cos(slant));
            float wide = FanLayers[1].wide * Main.screenWidth;
            Color near = byDay ? Color.Lerp(new Color(255, 226, 150), new Color(255, 196, 110), SunLow()) : new Color(200, 216, 255);
            Color far = byDay ? new Color(160, 225, 245) : new Color(120, 150, 220);

            float top = (VastOcean.Surface + 1) * 16f;
            int firstX = (int)Math.Floor(Main.screenPosition.X / Cell) - 1;
            int lastX = (int)Math.Ceiling((Main.screenPosition.X + Main.screenWidth) / Cell) + 1;
            int firstY = (int)Math.Floor(Math.Max(Main.screenPosition.Y, top) / Cell) - 1;
            int lastY = (int)Math.Ceiling(Math.Min(Main.screenPosition.Y + Main.screenHeight, (VastOcean.Surface + 0.5f) * 16f + length) / Cell) + 1;

            for (int cx = firstX; cx <= lastX; cx++)
            {
                for (int cy = firstY; cy <= lastY; cy++)
                {
                    int seed = cx * 7919 + cy * 6271;
                    float h1 = Hash(seed), h2 = Hash(seed + 3), h3 = Hash(seed + 5);

                    if (h3 < 0.25f)
                        continue;

                    float sink = (t * (2f + 3f * h2) + h1 * Cell) % Cell;
                    Vector2 world = new Vector2(cx * Cell + h1 * Cell + 7f * (float)Math.Sin(t * (0.3f + 0.3f * h2) + h3 * 11f), cy * Cell + sink);

                    if (world.Y < top)
                        continue;

                    Vector2 at = world - Main.screenPosition;
                    float lit = FanAt(at - apex, down, length, wide, out float along);

                    if (lit < 0.03f)
                        continue;

                    // Nor where the light does not reach.
                    float depth = at.Y - surfaceY;

                    if (depth >= LightStops((int)Math.Floor((at.X + Main.screenPosition.X + depth * lightLean) / 16f)))
                        continue;

                    float glimmer = 0.5f + 0.5f * (float)Math.Sin(t * (1.1f + 1.7f * h2) + h1 * 40f);
                    float flash = (float)Math.Pow(Math.Max(0.0, Math.Sin(t * (0.37f + 0.5f * h3) + h2 * 23f)), 24.0);
                    float brightness = strength * lit * (0.25f + 0.55f * glimmer + 1.2f * flash);

                    if (brightness < 0.01f)
                        continue;

                    Color colour = Color.Lerp(near, far, Smooth(0.1f, 0.8f, along));

                    batch.Draw(dot, at, null, Lit(colour, brightness), 0f, middle, (1.4f + 1.6f * h1) / dot.Width, SpriteEffects.None, 0f);

                    if (flash > 0.3f)
                        Sparkle(batch, at, colour, 0.8f * brightness, 3f + 3f * h2);
                }
            }
        }

        /// <summary>
        /// How bright the fan of light is at a point (given from its apex), 0 to 1, as its picture
        /// is drawn this long down the light (<paramref name="down"/>), sheared along it, and this
        /// wide at its far end (VastOceanFan.png, made by the same formula); and how far along it the
        /// point is, 0 to 1.
        /// </summary>
        private static float FanAt(Vector2 fromApex, Vector2 down, float length, float wide, out float along)
        {
            along = fromApex.Y / (length * down.Y);

            if (along <= 0f || along >= 1f)
                return 0f;

            float across = (fromApex.X + fromApex.Y * down.X / down.Y) / (wide * 0.5f);
            float half = 0.40f + 0.60f * (float)Math.Pow(along, 0.85);
            float u = across / half;
            float lengthwise = Smooth(0f, 0.02f, along) * (float)Math.Pow(1f - along, 1.5) * (0.25f + 0.75f * (float)Math.Exp(-2.8f * along));

            return Math.Min(1f, (float)Math.Exp(-1.8f * u * u) * lengthwise / FanPeak);
        }

        /// <summary>
        /// How much of the surface over this stretch of the screen (its middle, so wide) is open
        /// water, not under the platform or the island: 0 to 1, each column for as much of it as is
        /// in the stretch, so that it changes smoothly as the screen moves (it was nine columns
        /// across it, and the light about the rays went brighter and dimmer by ninths as each went
        /// on or off the island).
        /// </summary>
        private static float Open(float screenX, float wide)
        {
            if (wide <= 0f)
                return 1f;

            float a = Main.screenPosition.X + screenX - wide * 0.5f;
            float b = a + wide;
            float open = 0f;

            for (int column = (int)Math.Floor(a / 16f); column * 16f < b; column++)
            {
                float overlap = Math.Min(b, column * 16f + 16f) - Math.Max(a, column * 16f);

                if (overlap > 0f && (!WorldGen.InWorld(column, VastOcean.Surface) || !WorldGen.SolidTile(column, VastOcean.Surface)))
                    open += overlap;
            }

            return MathHelper.Clamp(open / wide, 0f, 1f);
        }

        /// <summary>
        /// The light on the water: glints coming and going along the surface where it is on the
        /// screen, as the waves catch the sun, white and gold by day, golden as it is low; by night
        /// the moon's, pale. Most of them under the sun or the moon, as the light off a sea runs
        /// along a path toward the eye, few elsewhere (by request: "anything below the sun or moon
        /// shines more rays than the rest is sparse"); by night that path is narrower and the rest
        /// of the sea all but dark: the moon's path ("5, excellent"). None on the platform, nor in
        /// the shadow of what stands out of the water (<see cref="LightStops"/>). In front
        /// of the game's water, in its top pixels (<see cref="Sunk"/>), so that they sit on its
        /// surface and its waves carry them; flatter than other sparkles, their spikes up and down
        /// shorter, as light off water is, so that little of one is over the water.
        /// </summary>
        private static void DrawGlints(SpriteBatch batch, float day)
        {
            const float Cell = 18f;

            float surfaceY = (VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y;

            if (surfaceY < -24f || surfaceY > Main.screenHeight + 24f)
                return;

            bool byDay = Main.dayTime;
            float strength = byDay ? day : 0.45f * Moonlight();

            if (strength < 0.02f)
                return;

            Color colour = byDay ? Color.Lerp(new Color(255, 248, 228), new Color(255, 205, 140), SunLow()) : new Color(190, 210, 255);
            float t = Main.GlobalTimeWrappedHourly;
            float under = Overhead();
            float path = Main.screenWidth * (byDay ? 0.2f : 0.12f);
            float sparse = byDay ? 0.25f : 0.08f;
            float most = byDay ? 0.6f : 0.55f;
            int first = (int)Math.Floor(Main.screenPosition.X / Cell) - 1;
            int across = (int)(Main.screenWidth / Cell) + 3;

            for (int c = first; c < first + across; c++)
            {
                float clock = t / (0.5f + 0.9f * Hash(c * 7349 + 78)) + Hash(c * 7349 + 77) * 3f;
                int turn = (int)Math.Floor(clock);
                float u = clock - turn;

                if (u > 0.3f)
                    continue;

                int s = c * 131 + turn * 7919;
                float x = c * Cell + Hash(s + 1) * Cell;
                float beneath = Beneath(x - Main.screenPosition.X, under, path);

                if (Hash(s) > most * (sparse + (1f - sparse) * beneath))
                    continue;

                int column = (int)(x / 16f);

                // None on the platform, nor in the shadow of what stands out of the water.
                if (!WorldGen.InWorld(column, VastOcean.Surface) || WorldGen.SolidTile(column, VastOcean.Surface) || LightStops(column) <= 0f)
                    continue;

                float twinkle = (float)Math.Sin(u / 0.3f * MathHelper.Pi);
                float big = Hash(s + 3) > 0.9f ? 1.8f : 1f;
                Vector2 at = new Vector2(x - Main.screenPosition.X, surfaceY + Sunk - 0.5f + 3f * Hash(s + 2));
                float brighter = byDay ? 0.55f + 0.45f * beneath : 0.4f + 0.9f * beneath;

                Sparkle(batch, at, colour, strength * twinkle * (0.55f + 0.45f * Hash(s + 4)) * brighter, (2.5f + 2.5f * Hash(s + 5)) * big, 0.4f);
            }
        }

        /// <summary>
        /// Under the sun or the moon, a soft band of its light lying on the surface, shimmering, as
        /// wide as its path of glints (<see cref="DrawGlints"/>): faint by day, the moon's path by night.
        /// In the water's top pixels, as the glints are (<see cref="Sunk"/>).
        /// </summary>
        private static void DrawPath(SpriteBatch batch, float day)
        {
            float surfaceY = (VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y;

            if (surfaceY < -24f || surfaceY > Main.screenHeight + 24f)
                return;

            bool byDay = Main.dayTime;
            float strength = byDay ? 0.10f * day : 0.24f * Moonlight();

            if (strength < 0.01f)
                return;

            Texture2D dot = light.Value;
            Color colour = byDay ? Color.Lerp(new Color(255, 246, 225), new Color(255, 205, 140), SunLow()) : new Color(185, 205, 255);
            float under = Overhead();
            float wide = Main.screenWidth * (byDay ? 0.2f : 0.12f) * 2.4f;
            float t = Main.GlobalTimeWrappedHourly;
            float shimmer = 0.85f + 0.15f * (float)Math.Sin(t * 1.7f) * (float)Math.Sin(t * 0.63f + 1.1f);
            Vector2 origin = dot.Size() * 0.5f;

            // A wide faint band, and a narrower brighter one in it, nearer the line of the water.
            batch.Draw(dot, new Vector2(under, surfaceY + Sunk + 1.5f), null, Lit(colour, strength * shimmer), 0f, origin, new Vector2(wide / dot.Width, 12f / dot.Height), SpriteEffects.None, 0f);
            batch.Draw(dot, new Vector2(under, surfaceY + Sunk), null, Lit(colour, strength * 0.8f * shimmer), 0f, origin, new Vector2(wide * 0.4f / dot.Width, 6f / dot.Height), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Whitecaps in rough weather (<see cref="Rough"/>, by request: "4, Yes you can make it
        /// animate better"): along the surface where it is on the screen, here and there a crest
        /// breaking white for a moment and going, more of them and bigger the rougher it is, none on
        /// the platform; from the biggest, a little spray thrown off downwind, falling back into
        /// the sea. White by day, however dark the storm (white against the sea, not as bright as
        /// a sunlit day), grey by night (it has only the moon's light), drawn as light. The crest
        /// and the flecks about it in the water's top pixels (<see cref="Sunk"/>), so that the
        /// game's waves carry them; the spray goes up from there into the air.
        /// </summary>
        private static void DrawFoam(SpriteBatch batch, float day, float rough)
        {
            const float Cell = 40f;
            const float Fall = 420f;

            if (rough < 0.12f)
                return;

            float surfaceY = (VastOcean.Surface + 0.5f) * 16f - Main.screenPosition.Y;

            if (surfaceY < -80f || surfaceY > Main.screenHeight + 24f)
                return;

            Texture2D dot = light.Value;
            Vector2 origin = dot.Size() * 0.5f;
            Color white = new Color(235, 242, 248);
            float lit = Main.dayTime ? Math.Max(day, 0.45f) : 0.6f * Moonlight() + 0.12f;
            float t = Main.GlobalTimeWrappedHourly;
            int way = Math.Abs(Main.windSpeedCurrent) > 0.05f ? Math.Sign(Main.windSpeedCurrent) : 1;
            int first = (int)Math.Floor(Main.screenPosition.X / Cell) - 1;
            int across = (int)(Main.screenWidth / Cell) + 3;

            for (int c = first; c < first + across; c++)
            {
                // Each place breaks now and then, at its own pace: white for the first part of its turn.
                float period = 1.4f + 2.2f * Hash(c * 7919 + 3);
                float clock = t / period + Hash(c * 7919 + 4);
                int turn = (int)Math.Floor(clock);
                float u = clock - turn;

                if (u > 0.45f)
                    continue;

                int s = c * 977 + turn * 7717;

                if (Hash(s) > 0.9f * rough)
                    continue;

                float x = c * Cell + Hash(s + 1) * Cell;
                int column = (int)(x / 16f);

                if (!WorldGen.InWorld(column, VastOcean.Surface) || WorldGen.SolidTile(column, VastOcean.Surface))
                    continue;

                float life = u / 0.45f;
                float bloom = Smooth(0f, 0.15f, life) * (1f - Smooth(0.5f, 1f, life));
                float big = (0.5f + Hash(s + 2)) * (0.7f + 0.6f * rough);
                float strength = 1.1f * bloom * lit;
                Vector2 at = new Vector2(x - Main.screenPosition.X, surfaceY + Sunk);

                // The crest breaking: a flat streak of white on the surface, and flecks about it.
                batch.Draw(dot, at, null, Lit(white, strength), 0f, origin, new Vector2((18f + 26f * big) / dot.Width, 4.5f / dot.Height), SpriteEffects.None, 0f);

                for (int k = 0; k < 3; k++)
                {
                    Vector2 fleck = at + new Vector2((Hash(s + 5 + k) - 0.5f) * 26f * big, -1f - 1.5f * Hash(s + 8 + k));

                    batch.Draw(dot, fleck, null, Lit(white, strength * 0.8f), 0f, origin, (2f + 1.5f * Hash(s + 11 + k)) / dot.Width, SpriteEffects.None, 0f);
                }

                // The spray: thrown up and downwind as it breaks, falling back.
                if (big < 1.1f)
                    continue;

                float age = life * 0.45f * period;

                for (int k = 0; k < 5; k++)
                {
                    float speed = 0.5f + rough;
                    float vx = way * (40f + 80f * Hash(s + 20 + k)) * speed;
                    float vy = -(60f + 80f * Hash(s + 30 + k)) * speed;
                    float y = at.Y + vy * age + 0.5f * Fall * age * age;

                    if (y > at.Y + 2f)
                        continue;

                    Vector2 drop = new Vector2(at.X + vx * age, y);
                    float fade = 1f - Smooth(0.3f, 0.75f, age);

                    batch.Draw(dot, drop, null, Lit(white, strength * fade), 0f, origin, (1.6f + 1.4f * Hash(s + 40 + k)) / dot.Width, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>
        /// Lightning through the water, one of the storm's looks (by request: "4, Yes you can make it
        /// animate better"): as the game flashes its sky (Main.lightning), the sea is lit pale with
        /// it, the most just under the surface, less and less to nothing by the end of the 2nd section;
        /// from the water's first row down, none of it over the water (<see cref="WaterTop"/>).
        /// </summary>
        private static void DrawFlash(SpriteBatch batch)
        {
            const int Band = 12;

            float flash = Main.lightning;

            if (flash < 0.01f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color pale = new Color(200, 218, 255);

            for (int sy = Math.Max(0, WaterTop()); sy < Main.screenHeight; sy += Band)
            {
                float rows = Math.Max(0f, (Main.screenPosition.Y + sy + Band * 0.5f) / 16f - (VastOcean.Surface + 0.5f));
                float reach = 1f - Smooth(0f, 2f * VastOcean.SectionRows, rows);

                if (reach < 0.005f)
                    break;

                batch.Draw(pixel, new Rectangle(0, sy, Main.screenWidth, Band), Lit(pale, flash * 0.3f * reach));
            }
        }

        /// <summary>
        /// A sparkle: a point of light, and four thin spikes from it fading as they go, those up and
        /// down so many times as long as those to the sides (<paramref name="tall"/>); stronger than
        /// full, its point drawn again as often as it takes.
        /// </summary>
        private static void Sparkle(SpriteBatch batch, Vector2 at, Color colour, float strength, float size, float tall = 1f)
        {
            if (strength < 0.01f)
                return;

            Texture2D dot = light.Value;
            Texture2D beam = ray.Value;
            Vector2 root = new Vector2(beam.Width * 0.5f, beam.Height);
            Vector2 spike = new Vector2(1.3f / beam.Width, size * 1.8f / beam.Height);

            for (float left = strength; left > 0.004f; left -= 1f)
                batch.Draw(dot, at, null, Lit(colour, Math.Min(left, 1f)), 0f, dot.Size() * 0.5f, size / dot.Width, SpriteEffects.None, 0f);

            // Turned a quarter at a time from pointing up: up, right, down, left.
            for (int side = 0; side < 4; side++)
                batch.Draw(beam, at, null, Lit(colour, strength * 0.85f), side * MathHelper.PiOver2, root, side % 2 == 0 ? new Vector2(spike.X, spike.Y * tall) : spike, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The sea's snow: a fleck in each cell of a grid fixed in the water, sinking. In the light
        /// of the first four sections, flecks; from the 2nd to the 6th, here and there one that
        /// glows; in the 7th, the last of them, too faint to be sure of, and the vast thing that
        /// puts them out where it passes (<see cref="Passing"/>); under that, none.
        /// </summary>
        private static void DrawSnow(SpriteBatch batch, float day)
        {
            const float Cell = 56f;
            const float Pace = 0.75f;

            float cameraRows = ScreenDepth();

            if (cameraRows < -20f || cameraRows > VastOcean.QuietFrom - VastOcean.Surface + 60f)
                return;

            Texture2D glow = light.Value;
            Vector2 origin = glow.Size() * 0.5f;
            float t = Main.GlobalTimeWrappedHourly;

            float murkTop = VastOcean.MurkFrom - VastOcean.Surface;
            float quietTop = VastOcean.QuietFrom - VastOcean.Surface;
            float navy = VastOcean.NavyAt - VastOcean.Surface;
            float black = VastOcean.BlackAt - VastOcean.Surface;

            float originX = Main.screenPosition.X * Pace;
            float originY = Main.screenPosition.Y * Pace;
            int firstX = (int)Math.Floor(originX / Cell) - 1;
            int firstY = (int)Math.Floor(originY / Cell) - 1;
            int across = (int)(Main.screenWidth / Cell) + 3;
            int down = (int)(Main.screenHeight / Cell) + 3;

            for (int cx = firstX; cx < firstX + across; cx++)
            {
                for (int cy = firstY; cy < firstY + down; cy++)
                {
                    int seed = cx * 7349 + cy * 1931;
                    float h1 = Hash(seed);
                    float h2 = Hash(seed + 17);
                    float h3 = Hash(seed + 41);

                    if (h3 < 0.35f)
                        continue;

                    float sink = (t * (4f + 6f * h2) + h1 * Cell) % Cell;
                    Vector2 at = new Vector2(
                        cx * Cell + h1 * Cell + 6f * (float)Math.Sin(t * 0.6f + h2 * 7f) - originX,
                        cy * Cell + sink - originY);

                    // How deep this fleck is, as the screen row it falls on is.
                    float rows = (Main.screenPosition.Y + at.Y) / 16f - VastOcean.Surface;

                    if (rows < 2f || rows > quietTop)
                        continue;

                    if (rows >= murkTop)
                    {
                        // The 7th: the last flecks, and the dark that passes over them.
                        // Faint as they are, and fainter for the murk over the screen there (VastMurk).
                        float last = 0.60f * (1f - Smooth(quietTop - 50f, quietTop, rows)) * Smooth(murkTop - 20f, murkTop + 20f, rows);

                        if (h3 > 0.6f && last > 0.005f && !Passing(at, t))
                            Glow(batch, glow, at, origin, new Color(120, 140, 150), last, (3f + 2f * h1) / glow.Width);

                        continue;
                    }

                    float inLight = (1f - Smooth(navy * 0.25f, navy, rows)) * (0.35f + 0.65f * day);
                    bool glows = h3 > 0.93f && rows > VastOcean.SectionRows && rows < black;

                    if (glows)
                    {
                        float pulse = 0.5f + 0.5f * (float)Math.Sin(t * (0.8f + h2) + h1 * 20f);
                        Color hue = Color.Lerp(new Color(80, 200, 255), new Color(110, 255, 190), h2);
                        float fade = Smooth(VastOcean.SectionRows, VastOcean.SectionRows + 60f, rows) * (1f - Smooth(black - 120f, black, rows));

                        Glow(batch, glow, at, origin, hue, 0.55f * pulse * fade, (5f + 4f * h1) / glow.Width);
                    }
                    else if (inLight > 0.02f)
                    {
                        Glow(batch, glow, at, origin, new Color(200, 220, 230), 0.32f * inLight, (2.5f + 2f * h1) / glow.Width);
                    }
                }
            }
        }

        /// <summary>
        /// Something vast passing between the last flecks and the eye, in the 7th section: once
        /// in every forty seconds or so, a long dark shape, three screens across and a third of one
        /// high, crossing the screen slowly; a fleck behind it is not seen. Nothing of it is drawn.
        /// </summary>
        private static bool Passing(Vector2 at, float t)
        {
            const float Period = 41f;

            int pass = (int)(t / Period);
            float along = t / Period - pass;

            if (Hash(pass * 23 + 9) < 0.3f)
                return false;

            int dir = Hash(pass * 71 + 2) < 0.5f ? -1 : 1;
            float length = Main.screenWidth * 3f;
            float tall = Main.screenHeight * (0.28f + 0.14f * Hash(pass * 13 + 4));
            float middleX = Main.screenWidth * 0.5f + dir * (along - 0.5f) * (Main.screenWidth * 2f + length);
            float middleY = Main.screenHeight * (0.3f + 0.4f * Hash(pass * 37 + 6)) + 30f * (float)Math.Sin(t * 0.2f);

            // An eye-shaped body: deepest in its middle, tapering to both ends.
            float u = (at.X - middleX) / (length * 0.5f);

            if (u < -1f || u > 1f)
                return false;

            float half = tall * 0.5f * (1f - u * u);

            return Math.Abs(at.Y - middleY) < half;
        }

        // ---- reckoning ---------------------------------------------------------------------------

        /// <summary>Rows under the water's surface of the screen's middle; less than nothing over it.</summary>
        internal static float ScreenDepth()
        {
            return (Main.screenPosition.Y + Main.screenHeight * 0.5f) / 16f - (VastOcean.Surface + 0.5f);
        }

        /// <summary>The sky's light as it was before a flash of lightning whitened it (<see cref="Daylight"/>).</summary>
        private static float steadyDaylight = 1f;

        /// <summary>
        /// How light the sky is, 0 to 1: the game's own sky colour, by its brightness, with a flash
        /// of lightning taken out of it (the game whitens its sky by the flash, each colour toward
        /// white by Main.lightning, Main.SetBackColor), and through the height of a flash as it was:
        /// the flash is the sea's own (<see cref="DrawFlash"/>), not the sun's rays coming on.
        /// </summary>
        internal static float Daylight()
        {
            Color sky = Main.ColorOfTheSkies;
            float light = MathHelper.Clamp((sky.R * 0.3f + sky.G * 0.59f + sky.B * 0.11f) / 255f, 0f, 1f);
            float flash = Main.lightning;

            if (flash <= 0f)
                steadyDaylight = light;
            else if (flash < 0.9f)
                steadyDaylight = MathHelper.Clamp((light - flash) / (1f - flash), 0f, 1f);

            return steadyDaylight;
        }

        /// <summary>How much of the light the rain's clouds let through, 0.1 to 1: the game darkens its sky by nine tenths of their cover (Main.SetBackColor).</summary>
        private static float Clear() => 1f - 0.9f * Main.cloudAlpha * Main.atmo;

        /// <summary>
        /// The moon's light, 0 to 1, by night only: as full as the moon is (a quarter of it at the
        /// new moon), through the rain's clouds, coming on over the first twentieth of the night and
        /// going over its last. (By the sky's brightness alone a dark day of rain would pass for night.)
        /// </summary>
        private static float Moonlight()
        {
            if (Main.dayTime)
                return 0f;

            float through = (float)(Main.time / Main.nightLength);
            float full = (1f + (float)Math.Cos(Main.moonPhase * MathHelper.PiOver4)) * 0.5f;

            return Clear() * (0.25f + 0.75f * full) * Smooth(0f, 0.05f, through) * (1f - Smooth(0.95f, 1f, through));
        }

        /// <summary>
        /// Where the sun (by day) or the moon (by night) is across the screen, in the screen's pixels
        /// as the sea is drawn: placed as the game places them (Main.DrawSunAndMoon: its middle a
        /// picture's width off the left of the screen as it rises, a picture's width off the right
        /// as it sets, between by the hour), and brought from the sky, which is not zoomed, into the
        /// zoomed view of the world.
        /// </summary>
        private static float Overhead()
        {
            Texture2D body = Main.dayTime ? TextureAssets.Sun.Value : TextureAssets.Moon[Utils.Clamp(Main.moonType, 0, TextureAssets.Moon.Length - 1)].Value;
            double through = Main.time / (Main.dayTime ? Main.dayLength : Main.nightLength);
            float x = (float)(through * (Main.screenWidth + body.Width * 2)) - body.Width;
            float zoom = Math.Max(Main.GameViewMatrix.Zoom.X, 0.01f);

            return (x - Main.screenWidth * 0.5f) / zoom + Main.screenWidth * 0.5f;
        }

        /// <summary>How near to under the sun or the moon this screen column is: 1 right under it, falling away to nothing some way off to the side (a bell this wide).</summary>
        private static float Beneath(float x, float under, float wide)
        {
            float u = (x - under) / wide;

            return (float)Math.Exp(-u * u);
        }

        /// <summary>How rough the sea is, 0 to 1: by the wind the most, and by the rain (VastOceanAmbience hears it so too).</summary>
        internal static float Rough()
        {
            float windy = Smooth(0.15f, 0.75f, Math.Abs(Main.windSpeedCurrent));
            float rainy = Main.raining ? Smooth(0.05f, 0.7f, Main.cloudAlpha) : 0f;

            return MathHelper.Clamp(0.8f * windy + 0.45f * rainy, 0f, 1f);
        }

        /// <summary>How low the sun is, by day: 1 at its rising and its setting, nothing through the middle of the day.</summary>
        private static float SunLow()
        {
            if (!Main.dayTime)
                return 0f;

            float through = (float)(Main.time / Main.dayLength);

            return 1f - Smooth(0f, 0.2f, through) * (1f - Smooth(0.8f, 1f, through));
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);

            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// A light's picture added under the water, as bright as it is to be seen through the
        /// game's water over it (<see cref="ThroughWater"/>): drawn a second time where once is
        /// not enough.
        /// </summary>
        private static void Glow(SpriteBatch batch, Texture2D texture, Vector2 at, Vector2 origin, Color colour, float strength, float scale)
        {
            for (strength *= ThroughWater; strength > 0.004f; strength -= 1f)
                batch.Draw(texture, at, null, Lit(colour, Math.Min(strength, 1f)), 0f, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// A colour at this strength that both adds itself and hides so much of what is behind it
        /// (0 to 1 of the strength): drawn blended as usual (the colour premultiplied), what is
        /// behind goes toward the colour as well as brighter, as a strong light hides what it shines through.
        /// </summary>
        private static Color Veil(Color colour, float strength, float cover)
        {
            strength = MathHelper.Clamp(strength, 0f, 1f);

            return new Color((byte)(colour.R * strength), (byte)(colour.G * strength), (byte)(colour.B * strength), (byte)(255 * strength * MathHelper.Clamp(cover, 0f, 1f)));
        }

        /// <summary>A colour at this strength with no alpha: drawn blended as usual, it is added to what is behind it.</summary>
        private static Color Lit(Color colour, float strength)
        {
            strength = MathHelper.Clamp(strength, 0f, 1f);

            return new Color((byte)(colour.R * strength), (byte)(colour.G * strength), (byte)(colour.B * strength), (byte)0);
        }

        private static float Hash(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
