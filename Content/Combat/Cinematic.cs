using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// A held moment: the screen narrows, the camera goes somewhere else and the player
    /// waits.
    ///
    /// Anything that wants one asks for it EVERY TICK it wants it — <see cref="Frame"/>
    /// for the letterbox, <see cref="Look"/> for the camera, <see cref="Hold"/> for the
    /// player, <see cref="Lean"/> for a zoom, <see cref="Cover"/> for something laid over
    /// the whole picture, <see cref="Fade"/> for a fade to black between play and a scene
    /// — and stops asking when it is done. Nothing
    /// is switched on or off, so a boss that dies, despawns or loses its packet
    /// mid-scene cannot leave the camera stuck on a patch of dirt or the player stuck in
    /// place: the moment it stops asking, everything eases back.
    ///
    /// The one exception is <see cref="Roar"/>, which is a moment rather than a state:
    /// called once, it runs its own length and ends.
    ///
    /// Client-side. The thing asking runs its AI on every client (bosses do), so every
    /// client asks for itself and nothing needs syncing.
    /// </summary>
    public static class Cinematic
    {
        /// <summary>Ticks the camera takes to reach what it is looking at, and to come back. A second and a half.</summary>
        public const int Pan = 90;

        /// <summary>Ticks the bars take to come in, and to go. Half a second: they should arrive before the shot does.</summary>
        public const int Bars = 30;

        /// <summary>What the screen narrows to. 21:9, from whatever it is: on a 21:9 monitor the bars are nothing, which is right.</summary>
        public const float Wide = 19f / 9f;

        private static Vector2 focus;

        // Requests are made during one update (by NPC AI, which runs after the players)
        // and read during the next (by the player, who runs before the NPCs). So each
        // is latched once a tick: what was asked for last tick is what holds this tick.
        // One tick of lag, invisible.
        private static bool lookAsked;
        private static bool holdAsked;
        private static bool frameAsked;
        private static bool holding;

        /// <summary>0 on the player, 1 on the focus. Eases between them.</summary>
        private static float weight;

        /// <summary>0 open, 1 fully letterboxed. Eases between them.</summary>
        private static float frame;

        // ---- the shade -------------------------------------------------------------
        //
        // A pool of light in the dark: everything on screen darkened except a soft
        // circle around one point in the world. Asked for every tick like the rest; the
        // strength eases toward what was asked and back to nothing when it stops being
        // asked, so a scene that ends mid-shot fades it rather than cutting it. The
        // flash is the other thing a shot sometimes needs: a colour thrown over the
        // whole screen once, additive, that dies away on its own.

        private static bool shadeAsked;
        private static Vector2 shadeWanted;
        private static float shadeRadiusWanted;
        private static float shadeStrengthWanted;

        private static Vector2 shadeAt;
        private static float shadeRadius = 1f;
        private static float shade;

        private static Color flashColour;
        private static float flash;

        /// <summary>Ticks the shade takes to come fully in or go fully out.</summary>
        public const int ShadeTicks = 24;

        /// <summary>How much of the flash is left after each tick.</summary>
        private const float FlashDecay = 0.86f;

        private static Texture2D pool;

        // ---- the cover -------------------------------------------------------------
        //
        // Something laid over the whole picture of the world: whatever the scene asking
        // for it draws, in raw screen pixels, first, so the shade, the roar and the bars
        // all land on top of it, and shown whether or not the interface is. Asked for
        // every tick like the rest, with the thing that draws it; stop asking and it is
        // gone the next tick. One at a time: the last to ask in a tick is the one drawn.

        private static Action<SpriteBatch> coverWanted;
        private static Action<SpriteBatch> cover;

        // ---- the fade ----------------------------------------------------------------
        //
        // The picture of the world gone to black, by however much is asked: the cut from
        // play into a scene (or between two shots of one), done as a fade, so that whatever
        // has to jump while nobody can see (the camera, the zoom, the player) jumps then.
        // Asked for every tick with the amount wanted, 0 to 1; the one asking does the
        // timing, so a fade can take as long as the scene wants and hang at black. Stop
        // asking and it is gone the next tick. Over the cover, under the shade, the roar
        // and the bars, and under the interface like all of them.

        private static float fadeWanted;
        private static float fade;

        // ---- the lean --------------------------------------------------------------
        //
        // The camera leaning in: a zoom on top of the player's own, never written into
        // it. Main.GameZoomTarget is a saved setting (BossIntro says why that matters),
        // so this multiplies the view matrix for the frame instead and there is nothing
        // to give back. Asked for every tick like the rest; eased toward what is asked,
        // and back to none when nothing is.

        private static bool leanAsked;
        private static float leanWanted = 1f;
        private static float leanRate = LeanRate;
        private static float lean = 1f;

        /// <summary>How much of the way to the asked lean it goes each tick.</summary>
        public const float LeanRate = 0.08f;

        // ---- the roar --------------------------------------------------------------
        //
        // One roar at a time. Torn rings of dark burst out from the one roaring, one every
        // seven ticks for as long as the voice lasts, each off the screen in a third of a
        // second and gathering speed as it goes; the screen dims a little and darkens at
        // the edges; the camera shakes, smoothly, a sum of three sines on each axis
        // rather than a random jump every frame, with one hard kick down at the start;
        // and the view punches in and eases back. Everything is shaped by the voice: in
        // over three ticks, held while fading a little, let go over the last eighteen.
        // The rings outlive the voice and finish going out on their own.

        private static int roarAge = -1;
        private static int roarLength;
        private static float roarStrength;
        private static Vector2 roarAt;
        private static float roarStart;
        private static float roarPhase;
        private static int roarNpc = -1;
        private static int roarNpcType = -1;

        /// <summary>
        /// One ring: a smoky band of dark bunched into lumps, torn into wisps along the
        /// radius at both edges, the outer ones longer. White with alpha, so it is drawn
        /// black with <c>Color.Black * dark</c>. Asked for at load, never made here.
        /// </summary>
        private static Asset<Texture2D> ring;

        /// <summary>Where the middle of the band sits in the ring's texture, as a share of its half-width.</summary>
        private const float RingBand = 0.78f;

        /// <summary>A ring every this many ticks, while the voice lasts.</summary>
        public const int RoarWaveEvery = 7;

        /// <summary>Ticks each ring takes to go out, by when it is well off the screen.</summary>
        public const int RoarWaveLife = 20;

        /// <summary>How far a ring gets, in world pixels. Off the edge of any screen.</summary>
        public const float RoarReach = 1600f;

        /// <summary>No ring is smaller than this when it sets out, in world pixels, however small the one roaring.</summary>
        private const float RoarRingStart = 90f;

        /// <summary>How hard a ring gathers speed: slow off the body, then a rush.</summary>
        private const float RoarRush = 2.6f;

        /// <summary>Each ring's fainter second band: this much further out, this much of its dark.</summary>
        private const float RoarEchoOut = 1.1f;
        private const float RoarEchoDark = 0.55f;

        /// <summary>No ring starts later than this before the voice ends: it would only be seen fading.</summary>
        private const int RoarLastWave = 8;

        /// <summary>The voice's release, at the end of its length.</summary>
        private const int RoarRelease = 18;

        /// <summary>At strength 1: the shake, in pixels; the kick down at the start; the punch in; the dim; the dark at the edges.</summary>
        public const float RoarShake = 9f;
        public const float RoarKick = 16f;
        public const float RoarPunch = 0.07f;
        public const float RoarDim = 0.14f;
        public const float RoarVignette = 0.5f;

        /// <summary>How dark a ring is at its strongest, at strength 1. The first one is louder.</summary>
        private const float RoarRingDark = 0.7f;

        /// <summary>Point the camera at this, this tick.</summary>
        public static void Look(Vector2 at)
        {
            focus = at;
            lookAsked = true;
        }

        /// <summary>Keep the local player still and unarmed, this tick.</summary>
        public static void Hold()
        {
            holdAsked = true;
        }

        /// <summary>Narrow the screen to 21:9, this tick. Black bars top and bottom, over the world, under the interface, and shown whether or not the interface is.</summary>
        public static void Frame()
        {
            frameAsked = true;
        }

        /// <summary>Whether the bars are on screen at all, including on their way out.</summary>
        public static bool Framed => frame > 0f;

        /// <summary>
        /// Darken the whole screen except a soft pool around this point, this tick.
        /// <paramref name="radius"/> is in world pixels, to where the dark is full;
        /// <paramref name="strength"/> is how dark, 0 to 1. The pool follows the point
        /// as it is asked for; the strength eases.
        /// </summary>
        public static void Shade(Vector2 world, float radius, float strength)
        {
            shadeAsked = true;
            shadeWanted = world;
            shadeRadiusWanted = Math.Max(8f, radius);
            shadeStrengthWanted = MathHelper.Clamp(strength, 0f, 1f);
        }

        /// <summary>Throw a colour over the whole screen, once. Additive; it dies away by itself.</summary>
        public static void Flash(Color colour, float strength)
        {
            flashColour = colour;
            flash = Math.Max(flash, MathHelper.Clamp(strength, 0f, 1f));
        }

        /// <summary>Whether a shade or a flash is on screen at all.</summary>
        public static bool Shaded => shade > 0.004f || flash > 0.004f;

        /// <summary>
        /// Lay something over the whole picture of the world, this tick.
        /// <paramref name="draw"/> is called once a frame, inside an open sprite batch with
        /// no transform on it (raw screen pixels), before the shade, the roar and the bars.
        /// It decides everything about what it draws, fades included.
        /// </summary>
        public static void Cover(Action<SpriteBatch> draw)
        {
            coverWanted = draw;
        }

        /// <summary>Whether something is laid over the world right now.</summary>
        public static bool Covered => cover != null;

        /// <summary>
        /// Fade the picture of the world to black by this much, 0 (none) to 1 (all), this
        /// tick. The one asking does the timing, in and out; two asking in one tick get the
        /// darker of the two.
        /// </summary>
        public static void Fade(float amount)
        {
            fadeWanted = Math.Max(fadeWanted, MathHelper.Clamp(amount, 0f, 1f));
        }

        /// <summary>Whether the picture is faded at all right now.</summary>
        public static bool Faded => fade > 0.004f;

        /// <summary>
        /// Lean the camera in (or back out to 1) by this much on top of the player's own
        /// zoom, this tick. 1 to 2. Eased: <paramref name="rate"/> is the share of the
        /// way it goes each tick, so a scene can ask for a slow creep or a snap.
        /// </summary>
        public static void Lean(float zoom, float rate = LeanRate)
        {
            leanAsked = true;
            leanWanted = MathHelper.Clamp(zoom, 1f, 2f);
            leanRate = MathHelper.Clamp(rate, 0.01f, 1f);
        }

        /// <summary>
        /// This NPC roars. Rings of dark go out from it, following it while it moves, the
        /// screen dims and shakes, the view punches in. <paramref name="length"/> is the
        /// voice, in ticks; the last rings finish going out after it. <paramref name="strength"/>
        /// scales all of it, 0 to 2. The sound is the vanilla roar unless another is given;
        /// <paramref name="quiet"/> plays none, for a roar whose sound something else
        /// already makes (vanilla's own, say). No camera: the screen stays where it is.
        ///
        /// Felt in full near the middle of the screen, less further out, and not at all
        /// past RoarFar: a roar off across the world shakes nobody's screen. The sound is
        /// positional and fades by itself.
        ///
        /// Called once, on every client that should see it (the NPC's AI runs on all of
        /// them); a new roar replaces one still going.
        /// </summary>
        public static void Roar(NPC npc, int length = 80, float strength = 1f, SoundStyle? sound = null, bool quiet = false)
        {
            if (Main.dedServ || npc == null || !npc.active)
                return;

            if (StartRoar(npc.Center, Math.Max(npc.width, npc.height) * 0.45f, length, strength, sound, quiet))
            {
                roarNpc = npc.whoAmI;
                roarNpcType = npc.type;
            }
        }

        /// <summary>A roar from a point in the world rather than from an NPC.</summary>
        public static void Roar(Vector2 at, int length = 80, float strength = 1f, SoundStyle? sound = null, bool quiet = false)
        {
            if (Main.dedServ)
                return;

            StartRoar(at, 40f, length, strength, sound, quiet);
        }

        /// <summary>Whether a roar is on, rings and all.</summary>
        public static bool Roaring => roarAge >= 0;

        /// <summary>Within this of the middle of the screen a roar is felt in full; past RoarFar, not at all. World pixels.</summary>
        public const float RoarNear = 1400f;
        public const float RoarFar = 2600f;

        /// <summary>True if the roar is on screen: false for one too far off to be felt.</summary>
        private static bool StartRoar(Vector2 at, float start, int length, float strength, SoundStyle? sound, bool quiet)
        {
            if (!quiet)
                SoundEngine.PlaySound(sound ?? SoundID.Roar, at);

            Vector2 view = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float near = 1f - Smooth((Vector2.Distance(view, at) - RoarNear) / (RoarFar - RoarNear));

            strength = MathHelper.Clamp(strength, 0f, 2f) * near;

            if (strength <= 0.01f)
                return false;

            roarAt = at;
            roarStart = Math.Max(8f, start);
            roarLength = Math.Max(RoarLastWave + 1, length);
            roarStrength = strength;
            roarPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            roarAge = 0;
            roarNpc = -1;
            roarNpcType = -1;

            return true;
        }

        /// <summary>The voice: 0 to 1, in over three ticks, sagging a third while held, out over the last eighteen.</summary>
        private static float Voice()
        {
            if (roarAge < 0 || roarAge >= roarLength)
                return 0f;

            return Math.Min(1f, roarAge / 3f) * Level(roarAge);
        }

        /// <summary>
        /// The voice at this age without its attack: how loud it is once it is going. A
        /// ring goes out as loud as this was when it left, so the first is at full and the
        /// ones sent out as the voice is let go are fainter.
        /// </summary>
        private static float Level(int age)
        {
            if (age < 0 || age >= roarLength)
                return 0f;

            float hold = 1f - 0.35f * age / roarLength;
            float release = 1f - Smooth((age - (roarLength - RoarRelease)) / (float)RoarRelease);

            return hold * release;
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The roar's shake, added to the screen after everything else has placed it.</summary>
        internal static Vector2 RoarOffset()
        {
            if (roarAge < 0 || Main.gameMenu)
                return Vector2.Zero;

            float voice = Voice();

            // In seconds, as a turn: the frequencies below are per second.
            float t = roarAge / 60f * MathHelper.TwoPi;
            float p = roarPhase;

            float x = 0.55f * (float)Math.Sin(7.1f * t + p)
                + 0.3f * (float)Math.Sin(11.3f * t + 2.1f * p)
                + 0.3f * (float)Math.Sin(3.1f * t + 0.7f + p);

            float y = 0.55f * (float)Math.Sin(8.3f * t + 1.3f * p)
                + 0.3f * (float)Math.Sin(12.7f * t + 0.4f)
                + 0.25f * (float)Math.Sin(3.7f * t + 2f * p);

            Vector2 shake = new Vector2(x, y) * (RoarShake * roarStrength * voice);

            // The kick: down hard on the first tick, and rung out.
            if (roarAge < 36)
                shake.Y += RoarKick * roarStrength * (float)Math.Exp(-roarAge / 5f) * (float)Math.Cos(roarAge * 0.85f);

            return shake;
        }

        /// <summary>How far the view is pushed in by the lean and the roar together, this frame.</summary>
        internal static float Zoom()
        {
            float zoom = lean;

            if (roarAge >= 0)
            {
                float punch = RoarPunch * Math.Min(1f, roarAge / 4f) * (float)Math.Exp(-Math.Max(0, roarAge - 4) / 12f);
                zoom *= 1f + roarStrength * (punch + 0.02f * Voice());
            }

            return zoom;
        }

        /// <summary>
        /// Whether the player is being held right now. Also true while the camera is
        /// still on its way back: moving before you can see yourself is worse than
        /// waiting a second and a half.
        /// </summary>
        public static bool Holding => holding || weight > 0f;

        /// <summary>Whether the camera is anywhere but on the player, including on its way back.</summary>
        public static bool Watching => weight > 0f;

        internal static void Tick()
        {
            float step = 1f / Pan;

            weight = lookAsked
                ? Math.Min(1f, weight + step)
                : Math.Max(0f, weight - step);

            frame = frameAsked
                ? Math.Min(1f, frame + 1f / Bars)
                : Math.Max(0f, frame - 1f / Bars);

            holding = holdAsked;

            // No easing: whoever asks for a cover, or a fade, does its own timing.
            cover = coverWanted;
            fade = fadeWanted;

            // The pool moves wherever it is asked to be; only its strength eases, so it
            // can be handed from one subject to the next without sliding between them.
            if (shadeAsked)
            {
                shadeAt = shadeWanted;
                shadeRadius = shadeRadiusWanted;
                shade = MoveToward(shade, shadeStrengthWanted, 1f / ShadeTicks);
            }
            else
            {
                shade = MoveToward(shade, 0f, 1f / ShadeTicks);
            }

            flash *= FlashDecay;

            if (flash < 0.004f)
                flash = 0f;

            float leanTo = leanAsked ? leanWanted : 1f;
            float rate = leanAsked ? leanRate : LeanRate;

            lean += (leanTo - lean) * rate;

            if (Math.Abs(leanTo - lean) < 0.0005f)
                lean = leanTo;

            if (roarAge >= 0)
            {
                // The rings follow what is roaring, for as long as it is there to follow.
                if (roarNpc >= 0)
                {
                    NPC npc = Main.npc[roarNpc];

                    if (npc.active && npc.type == roarNpcType)
                        roarAt = npc.Center;
                    else
                        roarNpc = -1;
                }

                roarAge++;

                if (roarAge > roarLength + RoarWaveLife)
                    roarAge = -1;
            }

            leanAsked = false;
            lookAsked = false;
            holdAsked = false;
            frameAsked = false;
            shadeAsked = false;
            coverWanted = null;
            fadeWanted = 0f;
        }

        private static float MoveToward(float from, float to, float step)
        {
            return from < to ? Math.Min(to, from + step) : Math.Max(to, from - step);
        }

        /// <summary>
        /// The pool's texture: black, clear in the middle and solid at the rim, made
        /// once on the first frame that needs it. Big enough that the gradient never
        /// shows a step however far it is stretched.
        /// </summary>
        private static Texture2D Pool()
        {
            if (pool != null && !pool.IsDisposed)
                return pool;

            const int size = 256;
            Color[] data = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half)) / half;
                    float a = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((r - 0.28f) / 0.72f, 0f, 1f));

                    // Premultiplied black: the colour is nothing, the alpha is the dark.
                    data[y * size + x] = new Color(0, 0, 0, (byte)(a * 255f));
                }
            }

            pool = new Texture2D(Main.graphics.GraphicsDevice, size, size);
            pool.SetData(data);

            return pool;
        }

        /// <summary>Asks for the ring's texture, in the background, so it is there before any roar.</summary>
        internal static void Load()
        {
            ring = ModContent.Request<Texture2D>("PisAndDag/Content/Combat/RoarRing");
        }

        internal static void Unload()
        {
            Texture2D oldPool = pool;
            pool = null;

            // The ring belongs to the mod's assets, which dispose it themselves.
            ring = null;

            if (oldPool != null)
                Main.QueueMainThreadAction(() => oldPool.Dispose());
        }

        /// <summary>A world left mid-roar, mid-lean or mid-cover does not take any of them into the next one.</summary>
        internal static void Reset()
        {
            roarAge = -1;
            roarNpc = -1;
            lean = 1f;
            leanAsked = false;
            cover = null;
            coverWanted = null;
            fade = 0f;
            fadeWanted = 0f;
        }

        /// <summary>
        /// The roar, in raw screen pixels, over the shade and under the bars: the dim,
        /// the dark at the edges, and the rings. The rings are placed through the world's
        /// own view matrix, like the shade's pool, so they come out of the one roaring at
        /// any zoom and move with the shake.
        /// </summary>
        private static void DrawRoar(SpriteBatch spriteBatch)
        {
            if (roarAge < 0)
                return;

            int w = Main.screenWidth;
            int h = Main.screenHeight;
            float voice = Voice();

            if (voice > 0.004f)
            {
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, w, h),
                    Color.Black * (RoarDim * roarStrength * voice));

                // The pool, stretched past the screen so its clear middle covers the
                // middle of the screen and the dark comes in from the edges and corners.
                int pw = (int)(w * 1.4f);
                int ph = (int)(h * 1.4f);

                spriteBatch.Draw(Pool(), new Rectangle((w - pw) / 2, (h - ph) / 2, pw, ph),
                    Color.White * MathHelper.Clamp(RoarVignette * roarStrength * voice, 0f, 1f));
            }

            // Still loading (it never is by the time anything roars): no rings rather than a stall.
            if (ring == null || !ring.IsLoaded)
                return;

            Texture2D texture = ring.Value;
            Vector2 centre = Vector2.Transform(roarAt - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
            float zoom = Main.GameViewMatrix.Zoom.X;
            float from = Math.Max(roarStart, RoarRingStart);
            float band = texture.Width * 0.5f * RingBand;
            float rushAll = (float)Math.Exp(RoarRush) - 1f;

            for (int k = 0; k * RoarWaveEvery <= roarLength - RoarLastWave; k++)
            {
                int age = roarAge - k * RoarWaveEvery;

                if (age < 0 || age > RoarWaveLife)
                    continue;

                float u = age / (float)RoarWaveLife;

                // Off the body slowly, then faster and faster: most of the reach is covered
                // at the end, in a rush.
                float rush = ((float)Math.Exp(RoarRush * u) - 1f) / rushAll;
                float radius = (from + (RoarReach - from) * rush) * zoom;

                // As loud as the voice was when it went out; in over two ticks, thinning
                // over the last part of its way, by when it is mostly off the screen.
                float dark = RoarRingDark * roarStrength * Level(k * RoarWaveEvery)
                    * Math.Min(1f, age / 2f) * (1f - Smooth((u - 0.6f) / 0.4f));

                if (k == 0)
                    dark *= 1.3f;

                if (dark <= 0.004f || radius < 1f)
                    continue;

                // Never quite round: a little out of true, each ring its own way.
                float wobble = 0.02f * (float)Math.Sin(age * 0.35f + k * 1.7f);
                Vector2 scale = new Vector2(1f + wobble, 1f - wobble) * (radius / band);
                float turn = k * 2.4f;

                // Each ring is two bands: the torn one, and a fainter one just outside it,
                // turned and mirrored so their lumps never line up and the gaps in one are
                // filled by the other.
                spriteBatch.Draw(texture, centre, null, Color.Black * Math.Min(1f, dark), turn,
                    texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);

                spriteBatch.Draw(texture, centre, null, Color.Black * Math.Min(1f, dark * RoarEchoDark), turn + 2.1f,
                    texture.Size() * 0.5f, scale * RoarEchoOut, SpriteEffects.FlipHorizontally, 0f);
            }
        }

        /// <summary>
        /// The shade and the flash, in raw screen pixels, under the bars. The pool is
        /// placed through the world's own view matrix, so it lands on its subject at any
        /// zoom; outside the pool's square the dark is filled flat.
        /// </summary>
        private static void DrawShade(SpriteBatch spriteBatch)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int w = Main.screenWidth;
            int h = Main.screenHeight;

            if (shade > 0.004f)
            {
                Vector2 centre = Vector2.Transform(shadeAt - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
                float radius = shadeRadius * Main.GameViewMatrix.Zoom.X;

                Rectangle square = new Rectangle((int)(centre.X - radius), (int)(centre.Y - radius),
                    (int)(radius * 2f), (int)(radius * 2f));

                Color dark = Color.White * shade;
                Color flat = Color.Black * shade;

                spriteBatch.Draw(Pool(), square, dark);

                if (square.Top > 0)
                    spriteBatch.Draw(pixel, new Rectangle(0, 0, w, Math.Min(h, square.Top)), flat);

                if (square.Bottom < h)
                    spriteBatch.Draw(pixel, new Rectangle(0, Math.Max(0, square.Bottom), w, h - Math.Max(0, square.Bottom)), flat);

                int top = Math.Max(0, square.Top);
                int bottom = Math.Min(h, square.Bottom);

                if (bottom > top)
                {
                    if (square.Left > 0)
                        spriteBatch.Draw(pixel, new Rectangle(0, top, Math.Min(w, square.Left), bottom - top), flat);

                    if (square.Right < w)
                        spriteBatch.Draw(pixel, new Rectangle(Math.Max(0, square.Right), top, w - Math.Max(0, square.Right), bottom - top), flat);
                }
            }

            if (flash > 0.004f)
            {
                // No alpha: added to what is there, so it lights rather than paints.
                Color light = flashColour * flash;
                light.A = 0;
                spriteBatch.Draw(pixel, new Rectangle(0, 0, w, h), light);
            }
        }

        /// <summary>Height of one bar once fully in, in screen pixels, for laying a shot out between them. Zero when the screen is already this wide.</summary>
        public static float FullBarHeight => Math.Max(0f, (Main.screenHeight - Main.screenWidth / Wide) * 0.5f);

        /// <summary>Height of one bar right now, in screen pixels. Zero when the screen is already this wide.</summary>
        internal static int BarHeight()
        {
            if (frame <= 0f)
                return 0;

            return (int)Math.Round(FullBarHeight * MathHelper.SmoothStep(0f, 1f, frame));
        }

        /// <summary>
        /// The cover, the fade, the shade, the roar and the bars, in that order, in raw screen pixels.
        /// Called from inside an open sprite batch with no transform on it; both draw paths
        /// below arrange that.
        /// </summary>
        internal static void DrawBars(SpriteBatch spriteBatch)
        {
            cover?.Invoke(spriteBatch);

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            if (fade > 0f)
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * fade);

            DrawShade(spriteBatch);
            DrawRoar(spriteBatch);

            int height = BarHeight();

            if (height <= 0)
                return;

            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, height), Color.Black);
            spriteBatch.Draw(pixel, new Rectangle(0, Main.screenHeight - height, Main.screenWidth, height), Color.Black);
        }

        /// <summary>Where the screen's top-left should be, given where vanilla put it.</summary>
        internal static Vector2 Place(Vector2 vanilla)
        {
            if (weight <= 0f)
                return vanilla;

            Vector2 target = focus - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;

            return Vector2.Lerp(vanilla, target, MathHelper.SmoothStep(0f, 1f, weight));
        }
    }

    /// <summary>Runs the easing once a tick, moves the screen after vanilla has placed it, and draws the bars.</summary>
    public class CinematicSystem : ModSystem
    {
        // The bars have to sit over the whole world and under the interface, and they
        // have to show when the interface is hidden. There is no one place in the draw
        // that gives all three: everything after the screen filters is inside vanilla's
        // "if (!hideUI)", and everything before them gets colour graded, which on the
        // Mile would lift the black. So, two places, one of which draws on any frame:
        //
        //   interface shown  -> an interface layer at index 0, first thing drawn, so
        //                       every real element lands on top of it
        //   interface hidden -> Main.OnPostDraw, after everything, which with no
        //                       interface is the same place
        //
        // Both are after the filters, both are raw screen pixels.

        public override void Load()
        {
            if (!Main.dedServ)
            {
                Main.OnPostDraw += DrawBarsOverEverything;
                Cinematic.Load();
            }
        }

        public override void Unload()
        {
            if (!Main.dedServ)
            {
                Main.OnPostDraw -= DrawBarsOverEverything;
                Cinematic.Unload();
            }
        }

        /// <summary>First thing in the update, before the players: latch last tick's requests.</summary>
        public override void PreUpdateEntities()
        {
            Cinematic.Tick();
        }

        public override void ModifyScreenPosition()
        {
            Main.screenPosition = Cinematic.Place(Main.screenPosition) + Cinematic.RoarOffset();
        }

        /// <summary>
        /// The lean and the roar's punch, on the frame's view matrix only. Vanilla sets
        /// the zoom from the player's own setting just before this runs, every frame, so
        /// nothing here outlives the frame or reaches the setting.
        /// </summary>
        public override void ModifyTransformMatrix(ref SpriteViewMatrix transform)
        {
            if (Main.gameMenu)
                return;

            float zoom = Cinematic.Zoom();

            if (Math.Abs(zoom - 1f) > 0.0001f)
                transform.Zoom *= zoom;
        }

        public override void OnWorldUnload()
        {
            Cinematic.Reset();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (!Cinematic.Framed && !Cinematic.Shaded && !Cinematic.Roaring && !Cinematic.Covered && !Cinematic.Faded)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer("PisAndDag: Letterbox", () =>
            {
                Cinematic.DrawBars(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.None));
        }

        private static void DrawBarsOverEverything(GameTime gameTime)
        {
            // Only when the interface path did not run this frame.
            if (!Main.hideUI || Main.gameMenu || (!Cinematic.Framed && !Cinematic.Shaded && !Cinematic.Roaring && !Cinematic.Covered && !Cinematic.Faded))
                return;

            // Linear, as the interface path is: the pool and the rings are stretched far
            // past their size, and point sampling would show every step in them.
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            Cinematic.DrawBars(Main.spriteBatch);

            Main.spriteBatch.End();
        }
    }

    /// <summary>The player's half of a held moment: no movement, no items, and nothing gets to kill them meanwhile.</summary>
    public class CinematicPlayer : ModPlayer
    {
        public override void ResetEffects()
        {
            if (!Cinematic.Holding)
                return;

            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void SetControls()
        {
            if (!Cinematic.Holding)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override bool CanUseItem(Item item) => !Cinematic.Holding;

        /// <summary>Ticks of immunity a hold keeps topped up. Renewed every tick it lasts.</summary>
        private const int CoverTicks = 2;

        /// <summary>Held last tick. The tick a hold ends is when what it did to the blink is undone.</summary>
        private bool held;

        public override void PostUpdateEquips()
        {
            if (!Cinematic.Holding)
            {
                if (held)
                    LetGo();

                return;
            }

            held = true;

            // A player who cannot move or fight should not be killable by a passing
            // zombie while they watch.
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, CoverTicks);

            // Without the blink. The game blinks an immune player by stepping immuneAlpha
            // every tick (Player.UpdateImmunity), and past halfway it does not draw the
            // player at all (Player.GetImmuneAlpha): in a held shot that is the player
            // flashing in and out, and in the Legends poster the silhouette with it.
            // immuneNoBlink holds the alpha at nothing from the next tick; it is zeroed
            // here as well because this tick's step has already been taken.
            Player.immuneNoBlink = true;
            Player.immuneAlpha = 0;
        }

        /// <summary>
        /// The hold is over. The cover it kept topped up goes with it, and immunity that runs
        /// longer (a real hit taken just before the scene) blinks again, as a hit should.
        /// </summary>
        private void LetGo()
        {
            held = false;

            if (Player.immuneTime <= CoverTicks)
            {
                Player.immune = false;
                Player.immuneTime = 0;
            }

            Player.immuneNoBlink = false;
        }
    }
}
