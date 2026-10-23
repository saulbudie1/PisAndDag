using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// Her veil, in her second half in Legends. By request, in turn: "she warps the reality and bends it onto her bust and
    /// hips, so its see thru"; "no its a veil that see thru, makes the empress texture transparent, she still have her
    /// tentacles ... like deletion of pixels"; "it should be streak a cross not just a bubble"; "bigger streaks, and more
    /// angled, it moves too, sometimes covering her legs as well".
    ///
    /// Streaks of nothing right across her, where her body is not there at all, every pixel of it taken out, so what is
    /// behind her (her streamers, her wings, the sky) is seen through her: a broad one across her bust tilted one way, a
    /// broader one across her hips tilted the other, and a thin broken one between. They drift up and down her a little,
    /// their torn edges rippling, and every seven seconds the one at her hips slides down over her legs and back. Their
    /// edges are caught in her light.
    ///
    /// Only where Veil.png (Content/NPCs/Legends/Empress) allows: over her body's own two frames, the streaks take out of
    /// her only what is painted there (all of her below her face, as it comes). The glow the game lays over her dress in
    /// her second half (Skirt.png, drawn in her pulsing light a few pixels about her) is taken out with them, a little
    /// wider than they are, so nothing of it is left floating in the gaps (by request: "why is there ghastly texture on
    /// her transparent part? ... make it transparent with the streak too"). Her arms, wings and streamers are never
    /// touched.
    ///
    /// Made on the main thread from her body and the veil (each read once), every other tick (every tick while it is
    /// coming): her body's frame with the streaks' pixels gone, and the light of their torn edges: brightest on the edge
    /// itself, fading over a few pixels into her and, fainter, out into the gap, so the edge glows rather than being
    /// drawn as a hard line. Nothing of the mod's own shader: the picture itself is made with the pixels gone.
    /// </summary>
    public static class EmpressVeil
    {
        private struct Streak
        {
            /// <summary>Where it lies across her (along its normal, from her middle, at size 1), its tilt, half its thickness.</summary>
            public float Base;
            public float Tilt;
            public float Half;

            /// <summary>How far it drifts each way and how fast; how far down it slides over her legs (0: never); its own number.</summary>
            public float Drift;
            public float Speed;
            public float Phase;
            public float Legs;
            public float Seed;
            public bool Broken;
        }

        private static readonly Streak[] Streaks =
        {
            new Streak { Base = -20f, Tilt = -0.42f, Half = 8f, Drift = 5f, Speed = 0.55f, Phase = 0f, Seed = 1.3f },
            new Streak { Base = 17f, Tilt = 0.48f, Half = 10f, Drift = 6f, Speed = 0.43f, Phase = 2.1f, Legs = 40f, Seed = 4.7f },
            new Streak { Base = 0f, Tilt = -0.25f, Half = 2.5f, Drift = 9f, Speed = 0.71f, Phase = 4f, Seed = 7.9f, Broken = true },
        };

        /// <summary>Seconds between the hip streak's slides down over her legs.</summary>
        private const float LegsEvery = 7f;

        /// <summary>The span of each streak's torn edges kept, a pixel a step along it, either side of her middle.</summary>
        private const int Reach = 128;

        private static int width;
        private static int height;
        private static Color[][] frames;
        private static int[][] open;
        private static bool refused;

        private static Color[] cut;
        private static Color[] edge;
        private static Color[] blank;
        private static bool[] gone;

        /// <summary>The glow over her dress (Skirt.png, one frame the width of hers, drawn about the same middle), as read, and as cut.</summary>
        private static Color[] skirtSource;
        private static Color[] skirtCut;
        private static int skirtWidth;
        private static int skirtHeight;

        /// <summary>
        /// How much wider than the streaks the dress's glow is taken (the game draws it in four copies up to 8 pixels about
        /// her); the streaks so widened, and a row's count of them, as it is made.
        /// </summary>
        private const int SkirtReach = 8;
        private static bool[] wide;
        private static bool[] wideRows;
        private static int[] runs;

        /// <summary>The torn edges' light: how bright, a pixel further each step into her (from the edge itself), and out into the gap.</summary>
        private static readonly float[] IntoHer = { 1f, 0.5f, 0.22f, 0.08f };
        private static readonly float[] IntoGap = { 0.42f, 0.16f, 0.05f };

        /// <summary>Each pixel's step from a torn edge, as the light is laid (0: none yet); the steps' pixels, in turn.</summary>
        private static byte[] step;
        private static int[] edgeRing;
        private static int[] ring;
        private static int[] nextRing;
        private static readonly float[,] up = new float[3, Reach * 2 + 1];
        private static readonly float[,] down = new float[3, Reach * 2 + 1];

        /// <summary>Two of each, used in turn, so a picture is never changed while the last frame's drawing may still read it.</summary>
        private static readonly Texture2D[] bodies = new Texture2D[2];
        private static readonly Texture2D[] rims = new Texture2D[2];
        private static readonly Texture2D[] whites = new Texture2D[2];
        private static readonly Texture2D[] skirts = new Texture2D[2];
        private static int turn;

        private static uint madeAt = uint.MaxValue;
        private static int madeFor = -1;
        private static int madeFrame = -1;
        private static float madeForm = -1f;

        /// <summary>
        /// Her body's frame (0 her first half, 1 her second) as the veil has it now, as far as it has come
        /// (<paramref name="form"/>, 0 to 1), the lit edges, the same as white (as bright as it is opaque, for light laid
        /// over her), and the glow over her dress cut the same way (null if it could not be read: drawn whole); null (her
        /// body drawn whole) if none of it has, or the pictures cannot be read back. Each is the size of one frame: drawn
        /// with no source, about EmpressArt.BodyOrigin.
        /// </summary>
        public static Texture2D BodyNow(NPC npc, int frame, float form, out Texture2D rim, out Texture2D white, out Texture2D skirt)
        {
            rim = null;
            white = null;
            skirt = null;

            if (form <= 0f || refused || !Ready())
                return null;

            uint now = Main.GameUpdateCount;
            frame = Math.Clamp(frame, 0, frames.Length - 1);

            // Every other tick once whole (it moves slowly); every tick while it comes, or for another of her or frame.
            bool stale = now - madeAt >= 2u || now < madeAt;

            if (stale || npc.whoAmI != madeFor || frame != madeFrame || Math.Abs(form - madeForm) > 0.001f)
            {
                try
                {
                    Make(frame, form, (float)Main.timeForVisualEffects / 60f + npc.whoAmI * 3.1f);
                }
                catch
                {
                    refused = true;
                    return null;
                }

                madeAt = now;
                madeFor = npc.whoAmI;
                madeFrame = frame;
                madeForm = form;
            }

            rim = rims[turn];
            white = whites[turn];
            skirt = skirtCut != null ? skirts[turn] : null;
            return bodies[turn];
        }

        /// <summary>Her body and the veil read, once; false if they cannot be.</summary>
        private static bool Ready()
        {
            if (frames != null)
                return true;

            if (!EmpressArt.Ready(EmpressTextures.Veil))
                return false;

            try
            {
                Texture2D body = EmpressArt.Body;
                Texture2D veil = EmpressTextures.Veil.Value;

                width = body.Width;
                height = body.Height / 2;

                Color[] all = new Color[body.Width * body.Height];
                body.GetData(all);

                Color[] allowed = new Color[veil.Width * veil.Height];
                veil.GetData(allowed);

                frames = new Color[2][];
                open = new int[2][];

                for (int f = 0; f < 2; f++)
                {
                    frames[f] = new Color[width * height];
                    Array.Copy(all, f * width * height, frames[f], 0, width * height);

                    // The pixels a streak may take: hers, and painted on the veil.
                    int count = 0;
                    int[] list = new int[width * height];

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int i = y * width + x;
                            int vy = y + f * height;
                            bool painted = x < veil.Width && vy < veil.Height && allowed[vy * veil.Width + x].A > 0;

                            if (painted && frames[f][i].A > 0)
                                list[count++] = i;
                        }
                    }

                    Array.Resize(ref list, count);
                    open[f] = list;
                }

                cut = new Color[width * height];
                edge = new Color[width * height];
                blank = new Color[width * height];
                gone = new bool[width * height];
                wide = new bool[width * height];
                wideRows = new bool[width * height];
                runs = new int[Math.Max(width, height) + 1];

                // The glow over her dress, if it can be read (else it is drawn whole, as the game's).
                try
                {
                    Texture2D skirt = EmpressArt.Skirt;
                    skirtWidth = Math.Min(skirt.Width, width);
                    skirtHeight = Math.Min(skirt.Height, height);
                    Color[] read = new Color[skirt.Width * skirt.Height];
                    skirt.GetData(read);

                    skirtSource = new Color[width * height];

                    for (int y = 0; y < skirtHeight; y++)
                        Array.Copy(read, y * skirt.Width, skirtSource, y * width, skirtWidth);

                    skirtCut = new Color[width * height];
                }
                catch
                {
                    skirtSource = null;
                    skirtCut = null;
                }

                step = new byte[width * height];
                edgeRing = new int[width * height];
                ring = new int[width * height];
                nextRing = new int[width * height];
                return true;
            }
            catch
            {
                refused = true;
                frames = null;
                return false;
            }
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>0 most of the time; up to 1 for a couple of seconds once every so often: the slide down over her legs and back.</summary>
        private static float LegsDip(float t)
        {
            float u = (t % LegsEvery + LegsEvery) % LegsEvery / LegsEvery;
            return Smooth((u - 0.55f) / 0.12f) * (1f - Smooth((u - 0.8f) / 0.12f));
        }

        /// <summary>This frame's picture of her: the streaks laid across her at <paramref name="t"/> seconds, as far as the veil has come.</summary>
        private static void Make(int frame, float form, float t)
        {
            Color[] source = frames[frame];
            Array.Copy(source, cut, source.Length);
            Array.Clear(gone, 0, gone.Length);
            Array.Clear(edge, 0, edge.Length);

            float cx = width * 0.5f;
            float cy = height * 0.5f;

            // Each streak's torn edges along it, this moment: a few slow waves, now and then a long tongue.
            for (int s = 0; s < Streaks.Length; s++)
            {
                for (int a = -Reach; a <= Reach; a++)
                {
                    float k = a * 0.21f + Streaks[s].Seed;
                    float tongueUp = Math.Max(0f, (float)Math.Sin(k * 0.6f + t * 0.35f));
                    float tongueDown = Math.Max(0f, (float)Math.Sin(k * 0.5f - t * 0.3f));
                    float waveUp = Math.Max(0f, (float)Math.Sin(k * 1.7f + t * 0.9f));
                    float waveDown = Math.Max(0f, (float)Math.Sin(k * 1.9f - t * 0.8f));

                    up[s, a + Reach] = Math.Max(0f, 2.2f * waveUp * waveUp + 1.4f * (float)Math.Sin(k * 3.1f - t * 0.6f) + 3.5f * (float)Math.Pow(tongueUp, 6));
                    down[s, a + Reach] = Math.Max(0f, 2.2f * waveDown * waveDown + 1.4f * (float)Math.Sin(k * 2.7f + t * 0.7f) + 4.5f * (float)Math.Pow(tongueDown, 6));
                }
            }

            // As she makes it: each streak thin at first and thickening, and sweeping across her from one side.
            float thick = Smooth(form);
            float sweep = MathHelper.Lerp(-Reach, Reach, Math.Min(1f, form * 1.35f));

            int[] list = open[frame];

            for (int s = 0; s < Streaks.Length; s++)
            {
                Streak streak = Streaks[s];
                float cos = (float)Math.Cos(streak.Tilt);
                float sin = (float)Math.Sin(streak.Tilt);
                float offset = streak.Base + streak.Drift * (float)Math.Sin(t * streak.Speed + streak.Phase) + streak.Legs * LegsDip(t + streak.Phase);
                float half = streak.Half * thick;

                if (half <= 0.05f)
                    continue;

                for (int n = 0; n < list.Length; n++)
                {
                    int i = list[n];

                    if (gone[i])
                        continue;

                    float px = i % width - cx;
                    float py = i / width - cy;
                    float along = px * cos + py * sin;

                    if (along > sweep)
                        continue;

                    if (streak.Broken && Math.Sin(along * 0.33f + t * 1.3f) > 0.45)
                        continue;

                    int a = Math.Clamp((int)Math.Round(along), -Reach, Reach) + Reach;
                    float d = -px * sin + py * cos - offset;

                    if (d > -(half + up[s, a]) && d < half + down[s, a])
                        gone[i] = true;
                }
            }

            for (int n = 0; n < list.Length; n++)
            {
                if (gone[list[n]])
                    cut[list[n]] = Color.Transparent;
            }

            // The torn edges, lit: her pixels next to one gone are the edge itself; the light fades a pixel a step into
            // her, and (fainter) out into the gap from it.
            Array.Clear(step, 0, step.Length);
            int edges = 0;

            for (int n = 0; n < list.Length; n++)
            {
                int i = list[n];

                if (!gone[i])
                    continue;

                int x = i % width;
                int y = i / width;

                Mark(x - 1, y, true, 1, ref edges, edgeRing);
                Mark(x + 1, y, true, 1, ref edges, edgeRing);
                Mark(x, y - 1, true, 1, ref edges, edgeRing);
                Mark(x, y + 1, true, 1, ref edges, edgeRing);
            }

            Spread(edgeRing, edges, true, 1, IntoHer.Length);
            Spread(edgeRing, edges, false, 100, IntoGap.Length + 100);

            // The glow over her dress, taken out where the streaks are and a little about them.
            if (skirtCut != null)
            {
                Widen();
                Array.Copy(skirtSource, skirtCut, skirtSource.Length);

                for (int i = 0; i < skirtCut.Length; i++)
                {
                    if (wide[i])
                        skirtCut[i] = Color.Transparent;
                }
            }

            // The same as white: as bright as it is opaque.
            for (int i = 0; i < cut.Length; i++)
            {
                int a = cut[i].A;
                blank[i] = new Color(a, a, a, a);
            }

            turn = 1 - turn;

            bodies[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);
            rims[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);
            whites[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);

            bodies[turn].SetData(cut);
            rims[turn].SetData(edge);
            whites[turn].SetData(blank);

            if (skirtCut != null)
            {
                skirts[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);
                skirts[turn].SetData(skirtCut);
            }
        }

        /// <summary>
        /// The streaks widened by SkirtReach every way (a square about each gone pixel): along each row, then down each
        /// column, counting gone pixels in a window as it slides.
        /// </summary>
        private static void Widen()
        {
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                runs[0] = 0;

                for (int x = 0; x < width; x++)
                    runs[x + 1] = runs[x] + (gone[row + x] ? 1 : 0);

                for (int x = 0; x < width; x++)
                    wideRows[row + x] = runs[Math.Min(width, x + SkirtReach + 1)] - runs[Math.Max(0, x - SkirtReach)] > 0;
            }

            for (int x = 0; x < width; x++)
            {
                runs[0] = 0;

                for (int y = 0; y < height; y++)
                    runs[y + 1] = runs[y] + (wideRows[y * width + x] ? 1 : 0);

                for (int y = 0; y < height; y++)
                    wide[y * width + x] = runs[Math.Min(height, y + SkirtReach + 1)] - runs[Math.Max(0, y - SkirtReach)] > 0;
            }
        }

        /// <summary>
        /// Lights a pixel at <paramref name="mark"/> steps from a torn edge, once (and lists it for the next step): one of
        /// hers still there (<paramref name="hers"/>), or one gone (steps from 101 out into the gap).
        /// </summary>
        private static void Mark(int x, int y, bool hers, byte mark, ref int count, int[] into)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int i = y * width + x;

            if (step[i] != 0)
                return;

            if (hers ? gone[i] || cut[i].A <= 100 : !gone[i])
                return;

            step[i] = mark;
            float light = hers ? IntoHer[Math.Min(mark - 1, IntoHer.Length - 1)] : IntoGap[Math.Min(mark - 101, IntoGap.Length - 1)];
            byte v = (byte)(light * 255f);
            edge[i] = new Color(v, v, v, v);
            into[count++] = i;
        }

        /// <summary>
        /// The light laid outward from the edge a step at a time: into her (<paramref name="hers"/>) or into the gap, from
        /// step <paramref name="from"/> (the edge's own) up to <paramref name="last"/>.
        /// </summary>
        private static void Spread(int[] edgeList, int edges, bool hers, int from, int last)
        {
            int count = edges;
            int[] here = edgeList;

            for (int k = from; k < last && count > 0; k++)
            {
                int nextCount = 0;
                int[] into = here == ring ? nextRing : ring;

                for (int n = 0; n < count; n++)
                {
                    int x = here[n] % width;
                    int y = here[n] / width;
                    byte mark = (byte)(k + 1);

                    Mark(x - 1, y, hers, mark, ref nextCount, into);
                    Mark(x + 1, y, hers, mark, ref nextCount, into);
                    Mark(x, y - 1, hers, mark, ref nextCount, into);
                    Mark(x, y + 1, hers, mark, ref nextCount, into);
                }

                here = into;
                count = nextCount;
            }
        }

        /// <summary>
        /// The veil's torn edges lit, over her body as it is drawn: white, fading into her and into the gap, a breath of her
        /// colours about it, shimmering; brighter as it is made (<paramref name="flare"/>). Expects the NPCs' batch open
        /// (light is added: no alpha).
        /// </summary>
        public static void DrawRim(SpriteBatch spriteBatch, Texture2D rim, Vector2 at, float turn, Vector2 origin, float size, SpriteEffects effects, float opacity, float flare)
        {
            if (rim == null || opacity <= 0.003f)
                return;

            float time = (float)Main.timeForVisualEffects;
            float shimmer = 0.75f + 0.25f * (float)Math.Sin(time * 0.09f);

            for (int k = 0; k < 4; k++)
            {
                Vector2 off = (k * MathHelper.PiOver2 + time * 0.02f).ToRotationVector2() * size;
                spriteBatch.Draw(rim, at + off, null, EmpressArt.Additive(EmpressArt.Prism(k / 4f + time / 300f), (0.22f + 0.35f * flare) * opacity), turn, origin, size, effects, 0f);
            }

            spriteBatch.Draw(rim, at, null, EmpressArt.Additive(Color.White, (0.55f + 0.45f * flare) * shimmer * opacity), turn, origin, size, effects, 0f);
        }

        internal static void Unload()
        {
            frames = null;
            open = null;
            cut = null;
            edge = null;
            blank = null;
            gone = null;
            step = null;
            edgeRing = null;
            ring = null;
            nextRing = null;
            wide = null;
            wideRows = null;
            runs = null;
            skirtSource = null;
            skirtCut = null;
            refused = false;
            madeAt = uint.MaxValue;

            for (int i = 0; i < 2; i++)
            {
                foreach (Texture2D made in new[] { bodies[i], rims[i], whites[i], skirts[i] })
                {
                    if (made != null)
                        Main.QueueMainThreadAction(made.Dispose);
                }

                bodies[i] = null;
                rims[i] = null;
                whites[i] = null;
                skirts[i] = null;
            }
        }
    }
}
