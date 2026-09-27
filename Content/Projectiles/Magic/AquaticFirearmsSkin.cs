using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// What Aquatic Firearms' three guns are made of: each vanilla gun's own outline, and
    /// inside it a deep-sea sky, stars drifting over a dark nebula the way they drift
    /// across the boss bar (BossPlate), two layers at two speeds for depth. Round the
    /// outline, a rim and a halo that take the gun's colours when drawn.
    ///
    /// No shader. The sky is worked out on the CPU and written into one small texture per
    /// gun, at most once a tick and only while a battery is on screen; a gun sprite is a
    /// couple of thousand texels, so it costs nothing. The sky is read one texel per ART
    /// pixel of the gun (a two by two block, the way vanilla sprites are drawn), so it is
    /// exactly as blocky as the gun it fills.
    ///
    /// Built the first time it is drawn, from the textures themselves: the outline from
    /// the vanilla item sprite, so a resource pack's gun keeps its own shape; the sky from
    /// AquaticNebula.png and AquaticStars.png beside this file (both opaque, both
    /// tileable, one texel an art pixel). The rim, the halo and <see cref="Dot"/> are made
    /// once, white with their colour in their alpha, and tinted where they are drawn.
    ///
    /// Client only; made, painted and thrown away on the main thread.
    /// </summary>
    public class AquaticFirearmsSkin : ModSystem
    {
        /// <summary>How far the halo reaches past the outline, in pixels.</summary>
        public const int HaloReach = 6;

        /// <summary>Drift of the two layers, in art pixels a second. Opposite signs are the parallax, as on the boss bar.</summary>
        private const float NebulaDriftX = -5f;
        private const float NebulaDriftY = 0.8f;
        private const float StarDriftX = 9f;
        private const float StarDriftY = -1.5f;

        /// <summary>How strongly the rim covers the sky: its outer ring of art pixels, and the ring inside that.</summary>
        private const float RimOuter = 0.85f;
        private const float RimInner = 0.25f;

        /// <summary>Each gun looks at its own part of the sky, so no two show the same stars.</summary>
        private static readonly Point[] SkyOffset = { new(0, 0), new(90, 20), new(170, 40) };

        /// <summary>One gun's skin.</summary>
        public sealed class Gun
        {
            /// <summary>The sky through the outline, repainted every tick. Opaque inside, clear outside.</summary>
            public Texture2D Body;

            /// <summary>The edge of the outline. Tint it with the gun's colour.</summary>
            public Texture2D Rim;

            /// <summary>A soft glow round the outline, <see cref="HaloReach"/> wider each side. Draw it with A = 0: it is made to be added.</summary>
            public Texture2D Halo;

            /// <summary>The end of the barrel, from the sprite's centre, facing right with y down, in pixels.</summary>
            public Vector2 Muzzle;

            internal int Width, Height, ArtWidth, ArtHeight;
            internal bool[] Inside;
            internal Color[] Pixels;
        }

        /// <summary>A round glow, white, its colour in its alpha, 24 across. Draw with A = 0.</summary>
        public static Texture2D Dot { get; private set; }

        private static Gun[] guns;
        private static Color[] nebula;
        private static Color[] stars;
        private static int nebulaWidth, nebulaHeight, starsWidth, starsHeight;
        private static uint painted = uint.MaxValue;
        private static bool failed;

        /// <summary>Gun <paramref name="index"/>'s skin (0 is the bottom one), or null if there is none yet or it could not be made.</summary>
        public static Gun Get(int index) => guns != null && index >= 0 && index < guns.Length ? guns[index] : null;

        public override void Unload()
        {
            Gun[] old = guns;
            Texture2D dot = Dot;
            guns = null;
            Dot = null;
            nebula = null;
            stars = null;
            painted = uint.MaxValue;
            failed = false;

            if (old == null && dot == null)
                return;

            Main.QueueMainThreadAction(() =>
            {
                dot?.Dispose();

                if (old == null)
                    return;

                foreach (Gun gun in old)
                {
                    gun?.Body?.Dispose();
                    gun?.Rim?.Dispose();
                    gun?.Halo?.Dispose();
                }
            });
        }

        /// <summary>
        /// Brings the three skins up to this tick, building them the first time. Call it
        /// from drawing: that is on the main thread, and it means nothing is painted while
        /// no battery is in view.
        /// </summary>
        public static void Refresh()
        {
            if (Main.dedServ || failed)
                return;

            if (guns == null && !Build())
                return;

            uint now = Main.GameUpdateCount;

            if (now == painted)
                return;

            painted = now;

            for (int i = 0; i < guns.Length; i++)
                Paint(guns[i], i, now);
        }

        private static bool Build()
        {
            try
            {
                nebula = Read("PisAndDag/Content/Projectiles/Magic/AquaticNebula", out nebulaWidth, out nebulaHeight);
                stars = Read("PisAndDag/Content/Projectiles/Magic/AquaticStars", out starsWidth, out starsHeight);

                Gun[] made = new Gun[AquaticFirearmsBattery.Guns];

                for (int i = 0; i < made.Length; i++)
                    made[i] = Make(AquaticFirearmsBattery.GunTypes[i]);

                Dot = MakeDot(24);
                guns = made;
                return true;
            }
            catch (Exception e)
            {
                // Drawn plain from here on (the battery falls back to the vanilla sprite),
                // and not tried again every frame.
                failed = true;
                guns = null;
                ModContent.GetInstance<AquaticFirearmsSkin>().Mod.Logger.Warn("Aquatic Firearms: could not build the gun skins; drawing them plain.", e);
                return false;
            }
        }

        private static Color[] Read(string path, out int width, out int height)
        {
            Texture2D texture = ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad).Value;
            width = texture.Width;
            height = texture.Height;
            Color[] data = new Color[width * height];
            texture.GetData(data);
            return data;
        }

        /// <summary>Reads a vanilla gun's outline and makes its rim, halo and an empty body.</summary>
        private static Gun Make(int itemType)
        {
            Texture2D sprite = ModContent.Request<Texture2D>("Terraria/Images/Item_" + itemType, AssetRequestMode.ImmediateLoad).Value;
            int w = sprite.Width;
            int h = sprite.Height;
            Color[] source = new Color[w * h];
            sprite.GetData(source);

            Gun gun = new()
            {
                Width = w,
                Height = h,
                ArtWidth = (w + 1) / 2,
                ArtHeight = (h + 1) / 2,
            };

            int aw = gun.ArtWidth;
            int ah = gun.ArtHeight;

            // One art pixel is a two by two block; its top left texel says whether it is in.
            gun.Inside = new bool[aw * ah];

            for (int ay = 0; ay < ah; ay++)
            {
                for (int ax = 0; ax < aw; ax++)
                    gun.Inside[ay * aw + ax] = source[ay * 2 * w + ax * 2].A > 0;
            }

            bool In(int x, int y) => x >= 0 && y >= 0 && x < aw && y < ah && gun.Inside[y * aw + x];
            bool Edge(int x, int y) => In(x, y) && (!In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1));

            // The rim: the outer ring of art pixels strongly, the ring inside it faintly.
            Color[] rim = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int ax = x / 2;
                    int ay = y / 2;
                    float strength = 0f;

                    if (Edge(ax, ay))
                        strength = RimOuter;
                    else if (In(ax, ay) && (Edge(ax - 1, ay) || Edge(ax + 1, ay) || Edge(ax, ay - 1) || Edge(ax, ay + 1)))
                        strength = RimInner;

                    rim[y * w + x] = White(strength);
                }
            }

            // The halo: falling off with the distance to the nearest texel of the gun.
            int hw = w + HaloReach * 2;
            int hh = h + HaloReach * 2;
            Color[] halo = new Color[hw * hh];

            for (int y = 0; y < hh; y++)
            {
                for (int x = 0; x < hw; x++)
                {
                    float nearest = float.MaxValue;

                    for (int dy = -HaloReach; dy <= HaloReach; dy++)
                    {
                        int sy = y - HaloReach + dy;

                        if (sy < 0 || sy >= h)
                            continue;

                        for (int dx = -HaloReach; dx <= HaloReach; dx++)
                        {
                            int sx = x - HaloReach + dx;

                            if (sx < 0 || sx >= w || source[sy * w + sx].A == 0)
                                continue;

                            nearest = Math.Min(nearest, dx * dx + dy * dy);
                        }
                    }

                    float d = MathF.Sqrt(nearest);
                    float glow = d >= HaloReach ? 0f : (1f - d / HaloReach) * (1f - d / HaloReach);
                    halo[y * hw + x] = White(glow);
                }
            }

            // The muzzle: the rightmost column the gun reaches, in the middle of what is there.
            int right = -1;
            int top = 0;
            int bottom = 0;

            for (int x = w - 1; x >= 0 && right < 0; x--)
            {
                for (int y = 0; y < h; y++)
                {
                    if (source[y * w + x].A == 0)
                        continue;

                    if (right < 0)
                    {
                        right = x;
                        top = y;
                    }

                    bottom = y;
                }
            }

            gun.Muzzle = right < 0 ? Vector2.Zero : new Vector2(right + 1 - w / 2f, (top + bottom + 1) / 2f - h / 2f);

            GraphicsDevice device = Main.instance.GraphicsDevice;
            gun.Rim = new Texture2D(device, w, h);
            gun.Rim.SetData(rim);
            gun.Halo = new Texture2D(device, hw, hh);
            gun.Halo.SetData(halo);
            gun.Body = new Texture2D(device, w, h);
            gun.Pixels = new Color[w * h];
            gun.Body.SetData(gun.Pixels);
            return gun;
        }

        private static Texture2D MakeDot(int size)
        {
            Color[] data = new Color[size * size];
            float r = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float glow = d >= 1f ? 0f : (1f - d) * (1f - d);
                    data[y * size + x] = White(glow);
                }
            }

            Texture2D dot = new(Main.instance.GraphicsDevice, size, size);
            dot.SetData(data);
            return dot;
        }

        /// <summary>White at this strength, its colour already multiplied by it, as the game's blending expects.</summary>
        private static Color White(float strength)
        {
            byte v = (byte)Math.Clamp((int)MathF.Round(strength * 255f), 0, 255);
            return new Color(v, v, v, v);
        }

        /// <summary>Fills gun <paramref name="index"/>'s outline with the sky as it is at this tick.</summary>
        private static void Paint(Gun gun, int index, uint tick)
        {
            float seconds = tick / 60f;
            Point sky = SkyOffset[index % SkyOffset.Length];

            int nx = (int)(NebulaDriftX * seconds) + sky.X;
            int ny = (int)(NebulaDriftY * seconds) + sky.Y;
            int sx = (int)(StarDriftX * seconds) + sky.X * 2;
            int sy = (int)(StarDriftY * seconds) + sky.Y * 3;

            int w = gun.Width;
            int h = gun.Height;
            int aw = gun.ArtWidth;
            Color[] pixels = gun.Pixels;

            for (int ay = 0; ay < gun.ArtHeight; ay++)
            {
                for (int ax = 0; ax < aw; ax++)
                {
                    if (!gun.Inside[ay * aw + ax])
                        continue;

                    Color cloud = nebula[Wrap(ay + ny, nebulaHeight) * nebulaWidth + Wrap(ax + nx, nebulaWidth)];

                    int starX = Wrap(ax + sx, starsWidth);
                    int starY = Wrap(ay + sy, starsHeight);
                    Color star = stars[starY * starsWidth + starX];

                    // Each star twinkles on its own beat, set by where it is in the star strip.
                    float twinkle = 0.7f + 0.3f * MathF.Sin(tick * 0.09f + (starX * 12.9898f + starY * 78.233f) % MathHelper.TwoPi);

                    Color c = new(
                        Math.Min(255, cloud.R + (int)(star.R * twinkle)),
                        Math.Min(255, cloud.G + (int)(star.G * twinkle)),
                        Math.Min(255, cloud.B + (int)(star.B * twinkle)),
                        255);

                    for (int dy = 0; dy < 2; dy++)
                    {
                        int y = ay * 2 + dy;

                        if (y >= h)
                            break;

                        for (int dx = 0; dx < 2; dx++)
                        {
                            int x = ax * 2 + dx;

                            if (x < w)
                                pixels[y * w + x] = c;
                        }
                    }
                }
            }

            gun.Body.SetData(pixels);
        }

        /// <summary>Modulo that lands in [0, m) for negative numbers too.</summary>
        private static int Wrap(int v, int m) => (v % m + m) % m;
    }
}
