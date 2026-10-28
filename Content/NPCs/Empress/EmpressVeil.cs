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
            /// <summary>
            /// Where it lies across her (along its normal, from her middle, at size 1), or the place it is held over
            /// (Anchored: its middle always through that place, as it turns); its tilt, how far and how fast the tilt
            /// sways; half its thickness.
            /// </summary>
            public float Base;
            public bool Anchored;
            public float AnchorX;
            public float AnchorY;
            public float Tilt;
            public float TiltSway;
            public float TiltSpeed;
            public float Half;

            /// <summary>How far it drifts each way and how fast; how far down it slides over her legs (0: never); its own number.</summary>
            public float Drift;
            public float Speed;
            public float Phase;
            public float Legs;
            public float Seed;
            public bool Broken;

            /// <summary>How ragged its torn edges are (1: the waves and tongues at their full reach; thinner streaks less).</summary>
            public float Rag;

            /// <summary>
            /// One that goes with another (its index; -1: its own): along its tilt, this far past it, the distance
            /// breathing by ApartSway (a streak spread into two, coming together and parting again).
            /// </summary>
            public int Follow;
            public float Apart;
            public float ApartSway;
            public float ApartSpeed;
        }

        /// <summary>
        /// Nine thin streaks (by request: "it can be 3, 4 thinner variations streaks but ... please cover the red square
        /// ... it can rotate, moves, spread into 2 but please still cover the red square. (move slowly tho dont transport
        /// too fast)", then a sketch of what is wanted and not: thin lines across her at gentle slants, crossing now and
        /// then, her body seen between them, the red places always under one; never steep, never all flat): one held over
        /// each of the two places BodyPhase2.png marks in red (her chest, her hips), each turning a little and drifting a
        /// little about it, never so far that the place shows, each with a thinner one beside it parting from it and coming
        /// back (the hip one sliding down over her legs now and then); a broken one and a thin one across her waist; one
        /// slanting from her waist across her thighs; a pair low on her legs. Their edges only a little ragged. All slow:
        /// a drift takes half a minute; each tips up and down through a fixed range, never steep (by request: "the
        /// transparent can shift to rotate up or down but keep it in a range, slowly"), a sway taking forty seconds or more.
        /// </summary>
        private static readonly Streak[] Streaks =
        {
            new Streak { Anchored = true, AnchorX = -0.5f, AnchorY = -18.5f, Tilt = 0.1f, TiltSway = 0.2f, TiltSpeed = 0.16f, Half = 3f, Drift = 0.6f, Speed = 0.21f, Phase = 0f, Seed = 1.3f, Rag = 0.18f, Follow = -1 },
            new Streak { Follow = 0, Apart = 8f, ApartSway = 2.5f, ApartSpeed = 0.17f, TiltSway = 0.06f, TiltSpeed = 0.23f, Half = 1.1f, Phase = 1.1f, Seed = 2.9f, Rag = 0.15f },
            new Streak { Base = -2f, Tilt = -0.1f, TiltSway = 0.2f, TiltSpeed = 0.14f, Half = 1.3f, Drift = 2.5f, Speed = 0.19f, Phase = 4f, Seed = 7.9f, Broken = true, Rag = 0.15f, Follow = -1 },
            new Streak { Anchored = true, AnchorX = 0.5f, AnchorY = 12.5f, Tilt = -0.22f, TiltSway = 0.2f, TiltSpeed = 0.13f, Half = 6f, Drift = 0.4f, Speed = 0.17f, Phase = 2.1f, Seed = 4.7f, Rag = 0.18f, Follow = -1 },
            new Streak { Follow = 3, Apart = 12f, ApartSway = 3f, ApartSpeed = 0.14f, TiltSway = 0.07f, TiltSpeed = 0.19f, Half = 1.4f, Phase = 3.3f, Legs = 26f, Seed = 6.1f, Rag = 0.15f },
            new Streak { Base = 4f, Tilt = 0.16f, TiltSway = 0.18f, TiltSpeed = 0.12f, Half = 1f, Drift = 2f, Speed = 0.15f, Phase = 5.2f, Seed = 3.7f, Rag = 0.12f, Follow = -1 },
            new Streak { Base = 22f, Tilt = 0.45f, TiltSway = 0.1f, TiltSpeed = 0.09f, Half = 1.2f, Drift = 4f, Speed = 0.13f, Phase = 0.7f, Seed = 8.3f, Rag = 0.12f, Follow = -1 },
            new Streak { Base = 56f, Tilt = -0.08f, TiltSway = 0.14f, TiltSpeed = 0.11f, Half = 1.3f, Drift = 3f, Speed = 0.16f, Phase = 2.6f, Seed = 5.5f, Rag = 0.15f, Follow = -1 },
            new Streak { Follow = 7, Apart = 6f, ApartSway = 2f, ApartSpeed = 0.2f, TiltSway = 0.05f, TiltSpeed = 0.21f, Half = 0.9f, Phase = 4.4f, Seed = 9.1f, Rag = 0.12f },
        };

        /// <summary>Seconds between the hip's thinner streak's slides down over her legs (each way taking a second and a half).</summary>
        private const float LegsEvery = 11f;

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

        /// <summary>The streaks themselves, white where they have taken her (for her change: EmpressArt fills them, fading).</summary>
        private static Color[] fill;
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
        private static readonly float[,] up = new float[Streaks.Length, Reach * 2 + 1];
        private static readonly float[,] down = new float[Streaks.Length, Reach * 2 + 1];

        /// <summary>Each streak's tilt and where it lies, this frame (a following one is placed after the one it follows).</summary>
        private static readonly float[] tiltNow = new float[Streaks.Length];
        private static readonly float[] offsetNow = new float[Streaks.Length];

        /// <summary>
        /// Her second half's own body (BodyPhase2.png, in place of the game's second frame while the veil is on her), and
        /// the places it marks in red, which the streaks must always cover: never drawn (taken out wherever a streak has
        /// come), and each held under its streak (Anchored) by its middle. Which red place each pixel is (0 none, 1 the
        /// upper, 2 the lower), and the head and shoulders laid back over her (HeadAndShoulderForPhase2.png) are kept
        /// out of the streaks' reach.
        /// </summary>
        private static byte[] marked;
        public static bool PhaseTwoBody { get; private set; }

        /// <summary>The second body as last made, veiled (for her images as she dashes), if her own second body is in use.</summary>
        public static Texture2D Current => PhaseTwoBody && madeFrame == 1 && Main.GameUpdateCount - madeAt < 4u ? bodies[turn] : null;

        /// <summary>Two of each, used in turn, so a picture is never changed while the last frame's drawing may still read it.</summary>
        private static readonly Texture2D[] bodies = new Texture2D[2];
        private static readonly Texture2D[] rims = new Texture2D[2];
        private static readonly Texture2D[] whites = new Texture2D[2];
        private static readonly Texture2D[] fills = new Texture2D[2];
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
        public static Texture2D BodyNow(NPC npc, int frame, float form, out Texture2D rim, out Texture2D white, out Texture2D skirt) =>
            BodyNow(npc, frame, form, out rim, out white, out skirt, out _);

        /// <summary>As above, and the streaks themselves, white where they have taken her (<paramref name="streaks"/>).</summary>
        public static Texture2D BodyNow(NPC npc, int frame, float form, out Texture2D rim, out Texture2D white, out Texture2D skirt, out Texture2D streaks)
        {
            rim = null;
            white = null;
            skirt = null;
            streaks = null;

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
            streaks = fills[turn];
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

                // Her second half's own body, and her head and shoulders kept over it, if both are there and her size.
                Color[] second = null;
                Color[] over = null;
                PhaseTwoBody = false;
                marked = null;

                if (EmpressArt.Ready(EmpressTextures.BodySecond) && EmpressArt.Ready(EmpressTextures.HeadOver))
                {
                    Texture2D two = EmpressTextures.BodySecond.Value;
                    Texture2D head = EmpressTextures.HeadOver.Value;

                    if (two.Width == body.Width && two.Height == body.Height && head.Width == body.Width && head.Height == body.Height)
                    {
                        second = new Color[two.Width * two.Height];
                        two.GetData(second);
                        over = new Color[head.Width * head.Height];
                        head.GetData(over);
                        PhaseTwoBody = true;
                        marked = new byte[width * height];
                    }
                }

                for (int f = 0; f < 2; f++)
                {
                    bool own = f == 1 && PhaseTwoBody;
                    frames[f] = new Color[width * height];
                    Array.Copy(own ? second : all, f * width * height, frames[f], 0, width * height);

                    // The pixels a streak may take: hers, and painted on the veil (her own second body: all of it but
                    // what her head and shoulders are laid back over, and its red places always).
                    int count = 0;
                    int[] list = new int[width * height];

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int i = y * width + x;
                            int vy = y + f * height;
                            Color c = frames[f][i];

                            if (c.A == 0)
                                continue;

                            bool take;

                            if (own)
                            {
                                bool red = c.R > 200 && c.G < 60 && c.B < 60;

                                if (red)
                                {
                                    marked[i] = (byte)(y < height / 2 - 4 ? 1 : 2);

                                    // Never seen red, even before a streak has come over it: her own colour from the
                                    // nearest of her above or below it.
                                    frames[f][i] = Nearest(second, f * height, x, y);
                                }

                                take = red || over[vy * width + x].A == 0;
                            }
                            else
                            {
                                take = x < veil.Width && vy < veil.Height && allowed[vy * veil.Width + x].A > 0;
                            }

                            if (take)
                                list[count++] = i;
                        }
                    }

                    Array.Resize(ref list, count);
                    open[f] = list;
                }

                cut = new Color[width * height];
                edge = new Color[width * height];
                blank = new Color[width * height];
                fill = new Color[width * height];
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

        /// <summary>The nearest of her pixels not marked red, up or down the same column of a frame (from row <paramref name="top"/> of the sheet).</summary>
        private static Color Nearest(Color[] sheet, int top, int x, int y)
        {
            for (int k = 1; k < height; k++)
            {
                foreach (int yy in new[] { y - k, y + k })
                {
                    if (yy < 0 || yy >= height)
                        continue;

                    Color c = sheet[(top + yy) * width + x];

                    if (c.A > 0 && !(c.R > 200 && c.G < 60 && c.B < 60))
                        return c;
                }
            }

            return Color.Transparent;
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
            return Smooth((u - 0.55f) / 0.14f) * (1f - Smooth((u - 0.8f) / 0.14f));
        }

        /// <summary>Where each streak lies and how it tilts at <paramref name="t"/> seconds (in index order: a following one after its own).</summary>
        private static void Place(float t)
        {
            for (int s = 0; s < Streaks.Length; s++)
            {
                Streak streak = Streaks[s];
                float sway = streak.TiltSway * (float)Math.Sin(t * streak.TiltSpeed + streak.Phase * 1.7f);

                if (streak.Follow >= 0 && streak.Follow < s)
                {
                    tiltNow[s] = tiltNow[streak.Follow] + sway;
                    offsetNow[s] = offsetNow[streak.Follow] + streak.Apart + streak.ApartSway * (float)Math.Sin(t * streak.ApartSpeed + streak.Phase)
                        + streak.Legs * LegsDip(t + streak.Phase);
                    continue;
                }

                float tilt = streak.Tilt + sway;
                float drift = streak.Drift * (float)Math.Sin(t * streak.Speed + streak.Phase) + streak.Legs * LegsDip(t + streak.Phase);
                tiltNow[s] = tilt;

                // Held over its place: its middle through the place's middle, however it turns.
                offsetNow[s] = streak.Anchored
                    ? -streak.AnchorX * (float)Math.Sin(tilt) + streak.AnchorY * (float)Math.Cos(tilt) + drift
                    : streak.Base + drift;
            }
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

                    up[s, a + Reach] = Streaks[s].Rag * Math.Max(0f, 2.2f * waveUp * waveUp + 1.4f * (float)Math.Sin(k * 3.1f - t * 0.6f) + 3.5f * (float)Math.Pow(tongueUp, 6));
                    down[s, a + Reach] = Streaks[s].Rag * Math.Max(0f, 2.2f * waveDown * waveDown + 1.4f * (float)Math.Sin(k * 2.7f + t * 0.7f) + 4.5f * (float)Math.Pow(tongueDown, 6));
                }
            }

            // As she makes it: each streak thin at first and thickening, and sweeping across her from one side.
            float thick = Smooth(form);
            float sweep = MathHelper.Lerp(-Reach, Reach, Math.Min(1f, form * 1.35f));

            int[] list = open[frame];
            Place(t);

            for (int s = 0; s < Streaks.Length; s++)
            {
                Streak streak = Streaks[s];
                float cos = (float)Math.Cos(tiltNow[s]);
                float sin = (float)Math.Sin(tiltNow[s]);
                float offset = offsetNow[s];
                float half = streak.Half * thick;

                // Never so thin, once made, that the place it is held over shows past its edge (its red pixels all
                // within it, a pixel to spare).
                if (streak.Anchored && marked != null && frame == 1 && PhaseTwoBody && form >= 0.999f)
                {
                    byte place = (byte)(s == 0 ? 1 : 2);

                    for (int n = 0; n < list.Length; n++)
                    {
                        int i = list[n];

                        if (marked[i] != place)
                            continue;

                        float d = Math.Abs(-(i % width - cx) * sin + (i / width - cy) * cos - offset);
                        half = Math.Max(half, d + 1f);
                    }
                }

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

            // The red places: never seen. Taken out as the streaks come over her, whatever else.
            if (marked != null && frame == 1 && PhaseTwoBody)
            {
                for (int n = 0; n < list.Length; n++)
                {
                    int i = list[n];

                    if (marked[i] != 0 && form >= 0.45f)
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

            // The same as white: as bright as it is opaque. And the streaks, white where they took her.
            for (int i = 0; i < cut.Length; i++)
            {
                int a = cut[i].A;
                blank[i] = new Color(a, a, a, a);
                fill[i] = gone[i] ? Color.White : Color.Transparent;
            }

            turn = 1 - turn;

            bodies[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);
            rims[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);
            whites[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);

            bodies[turn].SetData(cut);
            rims[turn].SetData(edge);
            whites[turn].SetData(blank);
            fills[turn] ??= new Texture2D(Main.graphics.GraphicsDevice, width, height);
            fills[turn].SetData(fill);

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
            fill = null;
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
            marked = null;
            PhaseTwoBody = false;
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
