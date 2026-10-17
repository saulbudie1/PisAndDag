using System;
using Microsoft.Xna.Framework;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// The HEATR's field as a picture, worked out afresh each time its wearer is drawn from where
    /// their body is (<see cref="HEATRShell"/>), by request: "Create a force field Over the player
    /// like that one rick and morty thing where it goes over the body, make it have a hexagon
    /// texture going up tho so its futuristic"; then, with the Rover Drive's shield for its look,
    /// "no i want it built with effect not with pixel", "but make it like: thin outline faded
    /// inwards, which is a larger size of the silhouette(distance from outline to nearest pixel
    /// is 2, effect is hexagon flown upwarrds", "all of this is faint", "Choose a red/crimson
    /// fades into pink color please", and, of smoothness, "maybe not most of ur effects are
    /// pixelated anyways".
    ///
    /// A pixel a pixel, as the game's own (not the 2x2 of the mod's art), smooth: worked out from
    /// how far each pixel is from the body, exactly (<see cref="Distances"/>), not from the
    /// body's steps, so its outline runs smooth about the body, rounding its corners, and fades
    /// at its edge. On a picture <see cref="Width"/> by <see cref="Height"/> set about the body's
    /// frame (<see cref="Side"/>, <see cref="Above"/>):
    ///   - a thin outline <see cref="Off"/> pixels out from the nearest pixel of the body (middle to
    ///     middle), about a pixel thick, crimson (<see cref="Crimson"/>);
    ///   - its light fading in from it, over the gap and onto the body, turning pink as it fades
    ///     (<see cref="Pink"/>; <see cref="FadeIn"/> pixels to a fall of nearly two thirds);
    ///   - hexagons in it, rising (<see cref="HexAcross"/> pixels across, <see cref="Rise"/> pixels
    ///     a second), between the two colours, plainer near the outline, fainter over the body,
    ///     brightening and dimming a little;
    ///   - a gap it closes in (between an arm and the side) filled, so there is no ring in it;
    ///   - all of it faint: the body shows through, and what is behind.
    /// Premultiplied, as the game draws (alpha blending): mostly added light, dimming what is
    /// behind it a little.
    ///
    /// Only arithmetic here (<see cref="Cover"/>, <see cref="Paint"/>; its shape kept for each body
    /// while the body is as it was, <see cref="Kept"/>): what the game draws and where is
    /// <see cref="HEATRShell"/>'s.
    /// </summary>
    public static class HEATRField
    {
        /// <summary>The body's frame, as the game draws a player's body.</summary>
        public const int FrameWidth = 40;
        public const int FrameHeight = 56;

        /// <summary>
        /// The picture's corner from the corner of the body's frame, in pixels: out to the side, and
        /// up (and as much below). Room for what is worn that reaches out of the frame (a tall hat,
        /// a long nose on a mask), and for the outline about it.
        /// </summary>
        public const int Side = 16;
        public const int Above = 16;

        /// <summary>The picture, in pixels.</summary>
        public const int Width = FrameWidth + Side * 2;
        public const int Height = FrameHeight + Above * 2;

        /// <summary>How far out from the body the outline is, in pixels (middle to middle), and how thick it is about there.</summary>
        public const float Off = 2f;
        private const float Thick = 0.55f;

        /// <summary>Pixels in from the outline its light falls to about a third.</summary>
        public const float FadeIn = 2.5f;

        /// <summary>The hexagons' width, flat side to flat side, and how wide their lines are, in pixels.</summary>
        public const float HexAcross = 9f;
        private const float LineWide = 1.1f;

        /// <summary>Pixels the hexagons rise a second.</summary>
        public const float Rise = 8f;

        // Colours, 0 to 1, and how much of each is added (light) and how much it dims what is behind.
        private static readonly Vector3 Crimson = new Vector3(0.88f, 0.10f, 0.26f);
        private static readonly Vector3 Pink = new Vector3(1f, 0.46f, 0.72f);
        private const float RimLight = 0.62f;
        private const float RimDim = 0.24f;
        private const float FadeLight = 0.32f;
        private const float FadeDim = 0.12f;
        private const float HexLight = 0.30f;
        private const float HexDim = 0.04f;

        /// <summary>sqrt(3), and the slope of a hexagon's slanted sides (a unit vector).</summary>
        private const float Root3 = 1.7320508f;
        private static readonly Vector2 Slant = new Vector2(0.5f, Root3 / 2f);

        /// <summary>Farther than anything in the picture, squared.</summary>
        private const float Far = 1e6f;

        // Worked out one at a time: the game draws its players one by one.
        private static readonly float[] outside = new float[Width * Height];
        private static readonly float[] inside = new float[Width * Height];
        private static readonly float[] line = new float[Math.Max(Width, Height)];
        private static readonly float[] lineOut = new float[Math.Max(Width, Height)];
        private static readonly float[] bounds = new float[Math.Max(Width, Height) + 1];
        private static readonly int[] lows = new int[Math.Max(Width, Height)];
        private static readonly bool[] reached = new bool[Width * Height];
        private static readonly int[] queue = new int[Width * Height];

        /// <summary>
        /// Marks the pixels of the picture that a drawn sprite covers: every pixel whose middle falls
        /// on a texel of it that is not see-through, as the game's SpriteBatch puts it there (FNA:
        /// turned about its origin by its rotation, scaled, its texture mirrored by its flips). The
        /// picture's corner is at (left, top) on the screen.
        /// </summary>
        /// <param name="alpha">The texture's alpha, a byte a texel, row by row.</param>
        /// <param name="textureWidth">The texture's width.</param>
        /// <param name="source">The part of the texture drawn.</param>
        /// <param name="position">Where it is drawn (the origin goes there).</param>
        /// <param name="scaleX">How much wider it is drawn than it is (more than naught).</param>
        /// <param name="scaleY">And how much taller.</param>
        public static void Cover(byte[] alpha, int textureWidth, Rectangle source, Vector2 position, float rotation, Vector2 origin,
            float scaleX, float scaleY, bool flipX, bool flipY, int left, int top, bool[] body)
        {
            if (source.Width <= 0 || source.Height <= 0 || scaleX <= 0f || scaleY <= 0f)
                return;

            float cos = (float)Math.Cos(rotation);
            float sin = (float)Math.Sin(rotation);

            // Where its corners fall, and the pixels between them on the picture.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            for (int corner = 0; corner < 4; corner++)
            {
                float cx = ((corner & 1) * source.Width - origin.X) * scaleX;
                float cy = ((corner >> 1) * source.Height - origin.Y) * scaleY;
                float x = position.X + cos * cx - sin * cy;
                float y = position.Y + sin * cx + cos * cy;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            int fromX = Math.Max(0, (int)Math.Floor(minX) - left);
            int toX = Math.Min(Width - 1, (int)Math.Ceiling(maxX) - left);
            int fromY = Math.Max(0, (int)Math.Floor(minY) - top);
            int toY = Math.Min(Height - 1, (int)Math.Ceiling(maxY) - top);

            for (int py = fromY; py <= toY; py++)
            {
                float dy = top + py + 0.5f - position.Y;

                for (int px = fromX; px <= toX; px++)
                {
                    int at = py * Width + px;

                    if (body[at])
                        continue;

                    float dx = left + px + 0.5f - position.X;

                    // Back onto the sprite: unturned, unscaled, from its corner.
                    float qx = (cos * dx + sin * dy) / scaleX + origin.X;
                    float qy = (-sin * dx + cos * dy) / scaleY + origin.Y;

                    if (qx < 0f || qy < 0f || qx >= source.Width || qy >= source.Height)
                        continue;

                    int tx = (int)qx;
                    int ty = (int)qy;

                    if (flipX)
                        tx = source.Width - 1 - tx;

                    if (flipY)
                        ty = source.Height - 1 - ty;

                    int column = source.X + tx;
                    int texel = (source.Y + ty) * textureWidth + column;

                    if (column >= 0 && column < textureWidth && texel >= 0 && texel < alpha.Length && alpha[texel] > 0)
                        body[at] = true;
                }
            }
        }

        /// <summary>
        /// What of the field stays while the body under it stays as it was (its shape): the body, the
        /// pixels it lights, and in each the outline's and the fade's light and how much it dims what
        /// is behind, and how strongly the hexagons show there. Kept for one body (each player's,
        /// <see cref="HEATRShell"/>), so that while the body is as it was only the hexagons, rising,
        /// are worked out again.
        /// </summary>
        public sealed class Kept
        {
            internal readonly bool[] Body = new bool[Width * Height];
            internal bool Made;
            internal readonly int[] Lit = new int[Width * Height];
            internal int LitCount;
            internal readonly Vector3[] Light = new Vector3[Width * Height];
            internal readonly float[] Dim = new float[Width * Height];
            internal readonly float[] Within = new float[Width * Height];
            internal readonly float[] Fading = new float[Width * Height];
        }

        /// <summary>How far past the body (pixels) its shape is worked out: past the furthest the field lights (<see cref="Off"/> and a pixel and a half), with a pixel to spare.</summary>
        private const int Margin = 5;

        /// <summary>
        /// The field over a body covering these pixels (<see cref="Cover"/>), at this time, into a
        /// picture of <see cref="Width"/> by <see cref="Height"/> pixels; its shape kept in
        /// <paramref name="kept"/> and worked out again only when the body is not as it was. False if
        /// there is no body to be over.
        /// </summary>
        public static bool Paint(bool[] body, float seconds, Color[] into, Kept kept)
        {
            if (!kept.Made || !body.AsSpan().SequenceEqual(kept.Body))
            {
                body.AsSpan().CopyTo(kept.Body);
                kept.Made = Shape(kept);
            }

            if (!kept.Made)
                return false;

            Array.Clear(into, 0, into.Length);

            // The hexagons, rising.
            float rise = seconds * Rise;
            float breathe = 0.85f + 0.15f * (float)Math.Sin(seconds * MathHelper.TwoPi / 2.6f);
            Vector3 hexColour = Vector3.Lerp(Pink, Crimson, 0.4f);

            for (int k = 0; k < kept.LitCount; k++)
            {
                int at = kept.Lit[k];
                int x = at % Width, y = at / Width;

                float fromSide = HexEdge((x + 0.5f) / HexAcross, (y + 0.5f + rise) / HexAcross) * HexAcross;
                float hex = (1f - Smooth(LineWide * 0.35f, LineWide, fromSide)) * breathe * kept.Within[k] * kept.Fading[k];

                Vector3 light = kept.Light[k] + hexColour * (hex * HexLight);
                float alpha = kept.Dim[k] + hex * HexDim;

                into[at] = new Color(Byte(light.X), Byte(light.Y), Byte(light.Z), Byte(alpha));
            }

            return true;
        }

        /// <summary>
        /// The field's shape about the body in <paramref name="kept"/>: worked out over the body's
        /// box and <see cref="Margin"/> about it (all further out is too far from the body to be lit,
        /// and connected to the picture's edge). False if there is no body.
        /// </summary>
        private static bool Shape(Kept kept)
        {
            bool[] body = kept.Body;
            int minX = Width, minY = Height, maxX = -1, maxY = -1;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!body[y * Width + x])
                        continue;

                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            kept.LitCount = 0;

            if (maxX < 0)
                return false;

            int x0 = Math.Max(0, minX - Margin), x1 = Math.Min(Width - 1, maxX + Margin);
            int y0 = Math.Max(0, minY - Margin), y1 = Math.Min(Height - 1, maxY + Margin);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * Width + x;
                    outside[i] = body[i] ? 0f : Far;
                    inside[i] = body[i] ? Far : 0f;
                }
            }

            // How far each pixel is from the body, and each of the body's from what is not it (squared, middle to middle).
            Distances(outside, x0, x1, y0, y1);
            Distances(inside, x0, x1, y0, y1);

            // How far out from the body each pixel is: past its edge, the distance to it; on it,
            // 0 at its edge pixels and less in from them. So the outline lies where this is Off.
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * Width + x;
                    outside[i] = body[i] ? 1f - (float)Math.Sqrt(inside[i]) : (float)Math.Sqrt(outside[i]);
                }
            }

            // A gap it closes in: not reached from the box's edge (and so from the picture's)
            // without crossing the outline. Filled.
            int head = 0, tail = 0;

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    reached[y * Width + x] = false;

            for (int x = x0; x <= x1; x++)
            {
                Reach(x, y0, ref tail);
                Reach(x, y1, ref tail);
            }

            for (int y = y0 + 1; y < y1; y++)
            {
                Reach(x0, y, ref tail);
                Reach(x1, y, ref tail);
            }

            while (head < tail)
            {
                int at = queue[head++];
                int x = at % Width, y = at / Width;

                if (x > x0) Reach(x - 1, y, ref tail);
                if (x < x1) Reach(x + 1, y, ref tail);
                if (y > y0) Reach(x, y - 1, ref tail);
                if (y < y1) Reach(x, y + 1, ref tail);
            }

            // Its light that stays: the outline, and its light fading in from it, crimson turning pink.
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int at = y * Width + x;
                    float from = outside[at];

                    if (from > Off && !reached[at])
                        from = 1f;

                    if (from >= Off + 1.5f)
                        continue;

                    float off = (from - Off) / Thick;
                    float rim = (float)Math.Exp(-off * off);
                    float within = MathHelper.Clamp(Off + 0.5f - from, 0f, 1f);
                    float fade = (float)Math.Exp(-Math.Max(0f, Off - from) / FadeIn) * within;
                    Vector3 fading = Vector3.Lerp(Pink, Crimson, fade);

                    int k = kept.LitCount++;
                    kept.Lit[k] = at;
                    kept.Light[k] = Crimson * (rim * RimLight) + fading * (fade * FadeLight);
                    kept.Dim[k] = rim * RimDim + fade * FadeDim;
                    kept.Within[k] = within;
                    kept.Fading[k] = 0.45f + 0.55f * (float)Math.Exp(-Math.Max(0f, Off - from) / (FadeIn * 2f));
                }
            }

            return true;
        }

        /// <summary>
        /// Every value in the box from (<paramref name="x0"/>, <paramref name="y0"/>) to
        /// (<paramref name="x1"/>, <paramref name="y1"/>) (both kept) set to its squared distance
        /// (middle to middle) from the nearest pixel in the box that is 0, exactly: down every row,
        /// then along every column (Felzenszwalb and Huttenlocher's distance transform, a lower
        /// envelope of parabolas).
        /// </summary>
        private static void Distances(float[] values, int x0, int x1, int y0, int y1)
        {
            int wide = x1 - x0 + 1, high = y1 - y0 + 1;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = 0; x < wide; x++)
                    line[x] = values[y * Width + x0 + x];

                Envelope(wide);

                for (int x = 0; x < wide; x++)
                    values[y * Width + x0 + x] = lineOut[x];
            }

            for (int x = x0; x <= x1; x++)
            {
                for (int y = 0; y < high; y++)
                    line[y] = values[(y0 + y) * Width + x];

                Envelope(high);

                for (int y = 0; y < high; y++)
                    values[(y0 + y) * Width + x] = lineOut[y];
            }
        }

        /// <summary>The one-way transform of <see cref="line"/> (n long) into <see cref="lineOut"/>.</summary>
        private static void Envelope(int n)
        {
            int k = 0;
            lows[0] = 0;
            bounds[0] = float.MinValue;
            bounds[1] = float.MaxValue;

            for (int q = 1; q < n; q++)
            {
                float s = Meet(q, lows[k]);

                while (s <= bounds[k])
                {
                    k--;
                    s = Meet(q, lows[k]);
                }

                k++;
                lows[k] = q;
                bounds[k] = s;
                bounds[k + 1] = float.MaxValue;
            }

            k = 0;

            for (int q = 0; q < n; q++)
            {
                while (bounds[k + 1] < q)
                    k++;

                int low = lows[k];
                lineOut[q] = (q - low) * (q - low) + line[low];
            }
        }

        /// <summary>Where the parabolas from q and from p meet.</summary>
        private static float Meet(int q, int p) => (line[q] + q * q - (line[p] + p * p)) / (2f * (q - p));

        private static void Reach(int x, int y, ref int tail)
        {
            int at = y * Width + x;

            if (!reached[at] && outside[at] > Off)
            {
                reached[at] = true;
                queue[tail++] = at;
            }
        }

        /// <summary>
        /// How far a point (in hexagon widths) is from the nearest side of the hexagon it is in:
        /// 0 on a side, a half in the middle. Hexagons standing on a point, in rows a half apart
        /// across and sqrt(3)/2 down.
        /// </summary>
        private static float HexEdge(float px, float py)
        {
            // The nearer of the two lattices' middles.
            float ax = Mod(px, 1f) - 0.5f, ay = Mod(py, Root3) - Root3 / 2f;
            float bx = Mod(px - 0.5f, 1f) - 0.5f, by = Mod(py - Root3 / 2f, Root3) - Root3 / 2f;
            bool nearA = ax * ax + ay * ay < bx * bx + by * by;
            float lx = Math.Abs(nearA ? ax : bx), ly = Math.Abs(nearA ? ay : by);

            return 0.5f - Math.Max(lx * Slant.X + ly * Slant.Y, lx);
        }

        private static float Mod(float value, float by) => value - by * (float)Math.Floor(value / by);

        private static float Smooth(float from, float to, float value)
        {
            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);

            return t * t * (3f - 2f * t);
        }

        private static byte Byte(float value) => (byte)MathHelper.Clamp(value * 255f + 0.5f, 0f, 255f);
    }
}
