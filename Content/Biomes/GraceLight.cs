using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The sun over the Kingdom of Grace, as the Grace filter and the motes need it.
    ///
    /// THE SUN. Where it is (<see cref="Sun"/>, toward it, in the world), what colour its light
    /// is (golden through the day, amber low in the morning and evening), and how much day
    /// there is (<see cref="Day"/>: none at night, fading in over the first few hours' light
    /// and out over the last). It crosses the sky as vanilla's does, rising on the left.
    ///
    /// THE LIGHT MAP. Every frame the filter is in use, how much of the sun reaches every tile
    /// on screen and round it, and through what, a texel a tile, handed to the filter as its
    /// second image. Worked out by carrying the light down from the sun's side of the view a
    /// row at a time (a column at a time when the sun is low), each tile taking what the tile
    /// on its sun side had, times what it lets through:
    ///
    ///   a solid block        nothing, unless it is glass
    ///   a platform, a table  most of it
    ///   anything else placed nearly all of it
    ///   glass (a wall, where no block stands in front of it)
    ///                        its own colour: stained glass throws coloured light into the
    ///                        halls behind it, the rainbow glass a rainbow
    ///   water                most of it
    ///
    /// Walls do not stop it: in the world a back wall is behind the room, not between the
    /// sun and the floor. A window does more: a pane of glass in a back wall with nothing in
    /// front of it is where the sun comes into the room from behind, so it gives light of its
    /// own, in its colour, which goes on the way the sun goes. So a hall is lit in slanting
    /// shafts from its windows and nowhere else, a colonnade throws bars of light and shade,
    /// and the shadow of a tower lies across the court. The source row is between two tiles
    /// when the sun is not straight up, so each row blends two, and a shaft's edge softens the
    /// further it falls.
    ///
    /// THE WARM LIGHT. By day on the plain the game's own sunlight, what it lights the tiles
    /// with, goes warm and golden (<see cref="ModifySunLightColor"/>), coming in over a
    /// second. This needs no shader: with Grace.xnb missing, the plain is still gold, if the
    /// sky is not.
    ///
    /// THE MOTES. While the local player is on the plain in daylight, a few motes a second hang
    /// in the air where the sun is (GraceMote), found by walking toward the sun through the
    /// tiles (<see cref="Lit"/>), which needs neither the map nor the filter.
    ///
    /// Client only; nothing here is saved or sent.
    /// </summary>
    public class GraceLight : ModSystem
    {
        /// <summary>Tiles round the view the map also covers, and how many more toward the sun, so a tower off the top of the screen still throws its shadow on it.</summary>
        private const int Margin = 8;
        private const int SunMargin = 44;

        /// <summary>The map's sides are rounded up to this many texels, so it is not made again every time the view is a tile wider.</summary>
        private const int Block = 32;

        /// <summary>The sun's colour at noon and low in the sky.</summary>
        private static readonly Vector3 Noon = new Vector3(1.00f, 0.86f, 0.56f);
        private static readonly Vector3 Low = new Vector3(1.00f, 0.60f, 0.28f);

        /// <summary>Toward the sun, in the world, y down.</summary>
        public static Vector2 Sun { get; private set; } = -Vector2.UnitY;

        /// <summary>How much day there is, 0 to 1.</summary>
        public static float Day { get; private set; }

        /// <summary>The sun's colour.</summary>
        public static Vector3 Colour { get; private set; } = Noon;

        /// <summary>Where vanilla draws the sun, as a share of the screen (off it before dawn and after dusk).</summary>
        private static Vector2 sunOnScreen;

        private static readonly Texture2D[] maps = new Texture2D[2];
        private static int current;

        private static Vector3[] pass;
        private static Vector3[] source;
        private static Vector3[] lit;
        private static Color[] pixels;

        /// <summary>The map this frame: its top-left tile and its size in tiles.</summary>
        private static int mapX, mapY, mapWidth, mapHeight;

        public override void Unload()
        {
            Texture2D a = maps[0], b = maps[1];
            maps[0] = maps[1] = null;
            pass = source = lit = null;
            pixels = null;

            if (a != null || b != null)
            {
                Main.QueueMainThreadAction(() =>
                {
                    a?.Dispose();
                    b?.Dispose();
                });
            }
        }

        // ------------------------------------------------------------------------------ the sun

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static void UpdateSun()
        {
            if (!Main.dayTime)
            {
                Day = 0f;
                Sun = -Vector2.UnitY;
                Colour = Low;
                sunOnScreen = new Vector2(0.5f, -1f);
                return;
            }

            // 0 at dawn, 1 at dusk.
            float f = (float)(Main.time / Main.dayLength);

            Day = Smooth(f / 0.07f) * Smooth((1f - f) / 0.07f);

            // Up and to the left in the morning, straight up at noon, up and to the right in
            // the evening, never lower than a quarter turn less a little from straight up.
            float tilt = MathHelper.Lerp(-1.15f, 1.15f, f);
            Sun = new Vector2((float)Math.Sin(tilt), -(float)Math.Cos(tilt));

            float low = Math.Abs(f - 0.5f) * 2f;
            Colour = Vector3.Lerp(Noon, Low, low * low);

            // Vanilla's sun: across the screen and a little past each side over the day, high
            // at noon, lower toward either end.
            float sunX = f * (Main.screenWidth + 2f * 50f) - 50f;
            float sunY = 180f + 250f * low * low;
            sunOnScreen = new Vector2(sunX / Math.Max(1, Main.screenWidth), sunY / Math.Max(1, Main.screenHeight));
        }

        // ------------------------------------------------------------------ what a tile lets by

        /// <summary>How bright a window is, as a source: the sun through the glass behind the room.</summary>
        private const float WindowLight = 0.85f;

        /// <summary>The colour a pane of this wall lets the sun through in; false if it is not glass at all.</summary>
        private static bool Glass(ushort wall, int x, int y, out Vector3 tint)
        {
            switch (wall)
            {
                case WallID.Glass:
                    tint = new Vector3(0.92f, 0.92f, 0.88f);
                    return true;
                case WallID.PurpleStainedGlass:
                    tint = new Vector3(0.72f, 0.36f, 1.00f);
                    return true;
                case WallID.YellowStainedGlass:
                    tint = new Vector3(1.00f, 0.90f, 0.36f);
                    return true;
                case WallID.BlueStainedGlass:
                    tint = new Vector3(0.36f, 0.56f, 1.00f);
                    return true;
                case WallID.GreenStainedGlass:
                    tint = new Vector3(0.40f, 1.00f, 0.46f);
                    return true;
                case WallID.RedStainedGlass:
                    tint = new Vector3(1.00f, 0.30f, 0.30f);
                    return true;
                case WallID.OrangeStainedGlass:
                    tint = new Vector3(1.00f, 0.60f, 0.26f);
                    return true;
                case WallID.GoldStarryGlassWall:
                    tint = new Vector3(1.00f, 0.86f, 0.46f);
                    return true;
                case WallID.BlueStarryGlassWall:
                    tint = new Vector3(0.46f, 0.62f, 1.00f);
                    return true;
                case WallID.RainbowStainedGlass:
                {
                    // A hue a pane, running across the window.
                    float hue = ((x * 0.13f + y * 0.07f) % 1f + 1f) % 1f;
                    tint = Main.hslToRgb(hue, 0.9f, 0.62f).ToVector3();
                    return true;
                }
                default:
                    if (WallID.Sets.Transparent[wall])
                    {
                        tint = new Vector3(0.85f);
                        return true;
                    }

                    tint = Vector3.One;
                    return false;
            }
        }

        /// <summary>
        /// What of the sun this tile lets by, and in what colour; and what it gives off itself
        /// (<paramref name="source"/>): a pane of glass in a back wall with nothing standing in
        /// front of it is a window, and the sun comes through it into the room from behind, in
        /// its colour, and goes on the way the sun goes. That is where the shafts in the halls
        /// come from.
        /// </summary>
        private static Vector3 Passes(int x, int y, out Vector3 source)
        {
            source = Vector3.Zero;

            if (!WorldGen.InWorld(x, y))
                return Vector3.One;

            Tile tile = Main.tile[x, y];

            if (tile.HasTile && !tile.IsActuated)
            {
                ushort type = tile.TileType;

                // A block of glass is solid, and a window all the same.
                if (type == TileID.Glass)
                    return new Vector3(0.9f, 0.92f, 0.95f);

                if (Main.tileSolid[type] && !Main.tileSolidTop[type])
                    return Vector3.Zero;

                if (Main.tileSolidTop[type])
                    return new Vector3(0.72f);

                return new Vector3(0.92f);
            }

            Vector3 through = Vector3.One;

            if (tile.WallType > 0 && Glass(tile.WallType, x, y, out Vector3 tint))
            {
                through = tint;
                source = tint * WindowLight;
            }

            if (tile.LiquidAmount > 0)
                through *= tile.LiquidType == LiquidID.Water ? new Vector3(0.82f, 0.9f, 0.95f) : new Vector3(0.6f);

            return through;
        }

        /// <summary>
        /// How much of the sun gets to <paramref name="world"/>, as the map would have it: walked
        /// toward the sun a tile at a time for up to 48 tiles, through whatever is in the way,
        /// taking the brightest of the sky at the end and every window on the way. Its
        /// brightness, 0 to 1.
        /// </summary>
        public static float Lit(Vector2 world)
        {
            Vector3 carried = Vector3.One;
            float best = 0f;
            Vector2 at = world;

            for (int i = 0; i < 48; i++)
            {
                Vector3 through = Passes((int)(at.X / 16f), (int)(at.Y / 16f), out Vector3 source);
                Vector3 window = carried * source;

                best = Math.Max(best, (window.X + window.Y + window.Z) / 3f);
                carried *= through;

                if (carried.X + carried.Y + carried.Z < 0.03f)
                    return best;

                at += Sun * 16f;
            }

            return Math.Max(best, (carried.X + carried.Y + carried.Z) / 3f);
        }

        // ------------------------------------------------------------------------ the light map

        /// <summary>The view's top-left and size in the world, as ScreenShaderData works them out for uScreenResolution.</summary>
        private static void View(out Vector2 topLeft, out Vector2 size)
        {
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            Vector2 screen = new Vector2(Main.screenWidth, Main.screenHeight);

            size = screen / zoom;
            topLeft = Main.screenPosition + screen * 0.5f * (Vector2.One - Vector2.One / zoom);
        }

        private static void BuildMap(Vector2 topLeft, Vector2 size)
        {
            int x0 = (int)Math.Floor(topLeft.X / 16f) - Margin;
            int y0 = (int)Math.Floor(topLeft.Y / 16f) - Margin;
            int x1 = (int)Math.Ceiling((topLeft.X + size.X) / 16f) + Margin;
            int y1 = (int)Math.Ceiling((topLeft.Y + size.Y) / 16f) + Margin;

            // Further toward the sun, so what stands off the screen on its side still shades it.
            if (Sun.Y < 0f)
                y0 -= SunMargin;
            else
                y1 += SunMargin;

            if (Sun.X < -0.2f)
                x0 -= SunMargin;
            else if (Sun.X > 0.2f)
                x1 += SunMargin;

            int width = (x1 - x0 + Block - 1) / Block * Block;
            int height = (y1 - y0 + Block - 1) / Block * Block;

            mapX = x0;
            mapY = y0;
            mapWidth = width;
            mapHeight = height;

            int count = width * height;

            if (pass == null || pass.Length < count)
            {
                pass = new Vector3[count];
                source = new Vector3[count];
                lit = new Vector3[count];
                pixels = new Color[count];
            }

            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    int k = j * width + i;
                    pass[k] = Passes(x0 + i, y0 + j, out source[k]);
                }
            }

            if (Math.Abs(Sun.Y) >= Math.Abs(Sun.X))
                CarryDown(width, height);
            else
                CarryAcross(width, height);
        }

        /// <summary>The sun mostly overhead (or under): the light carried a row at a time from the sun's side.</summary>
        private static void CarryDown(int width, int height)
        {
            int step = Sun.Y < 0f ? 1 : -1;
            int first = step > 0 ? 0 : height - 1;

            // How far along the row toward the sun the light on the next row came from.
            float shift = Sun.X / Math.Abs(Sun.Y);
            int whole = (int)Math.Floor(shift);
            float part = shift - whole;

            for (int n = 0; n < height; n++)
            {
                int j = first + n * step;
                int from = j - step;

                for (int i = 0; i < width; i++)
                {
                    Vector3 incoming;

                    if (n == 0)
                        incoming = Vector3.One;
                    else
                    {
                        int a = i + whole;
                        Vector3 left = a >= 0 && a < width ? lit[from * width + a] : Vector3.One;
                        Vector3 right = a + 1 >= 0 && a + 1 < width ? lit[from * width + a + 1] : Vector3.One;
                        incoming = Vector3.Lerp(left, right, part);
                    }

                    int k = j * width + i;
                    lit[k] = Vector3.Max(incoming * pass[k], source[k]);
                }
            }
        }

        /// <summary>The sun low: the light carried a column at a time from the sun's side.</summary>
        private static void CarryAcross(int width, int height)
        {
            int step = Sun.X < 0f ? 1 : -1;
            int first = step > 0 ? 0 : width - 1;

            float shift = Sun.Y / Math.Abs(Sun.X);
            int whole = (int)Math.Floor(shift);
            float part = shift - whole;

            for (int n = 0; n < width; n++)
            {
                int i = first + n * step;
                int from = i - step;

                for (int j = 0; j < height; j++)
                {
                    Vector3 incoming;

                    if (n == 0)
                        incoming = Vector3.One;
                    else
                    {
                        int a = j + whole;
                        Vector3 above = a >= 0 && a < height ? lit[a * width + from] : Vector3.One;
                        Vector3 below = a + 1 >= 0 && a + 1 < height ? lit[(a + 1) * width + from] : Vector3.One;
                        incoming = Vector3.Lerp(above, below, part);
                    }

                    int k = j * width + i;
                    lit[k] = Vector3.Max(incoming * pass[k], source[k]);
                }
            }
        }

        /// <summary>Puts the map in a texture, upside down if the screen is (reversed gravity), and hands it to the filter.</summary>
        private static void Upload(ScreenShaderData shader, Vector2 topLeft, Vector2 size)
        {
            GraphicsDevice device = Main.instance.GraphicsDevice;

            current = 1 - current;
            Texture2D map = maps[current];

            if (map == null || map.Width != mapWidth || map.Height != mapHeight)
            {
                map?.Dispose();
                map = maps[current] = new Texture2D(device, mapWidth, mapHeight);
            }

            bool flipped = Main.LocalPlayer.gravDir < 0f;

            for (int j = 0; j < mapHeight; j++)
            {
                int row = flipped ? mapHeight - 1 - j : j;

                for (int i = 0; i < mapWidth; i++)
                {
                    Vector3 l = lit[j * mapWidth + i];
                    pixels[row * mapWidth + i] = new Color(l.X, l.Y, l.Z, 1f);
                }
            }

            map.SetData(pixels, 0, mapWidth * mapHeight);

            // Where the map's top-left is, in world pixels from the view's top-left, as the
            // shader measures: under reversed gravity the screen is upside down, so it is the
            // map's bottom edge, measured from the top of the flipped view.
            Vector2 offset = new Vector2(mapX * 16f - topLeft.X, mapY * 16f - topLeft.Y);

            if (flipped)
                offset.Y = topLeft.Y + size.Y - (mapY + mapHeight) * 16f;

            shader.UseImage(map, 0, SamplerState.LinearClamp);
            shader.UseImageOffset(offset);
        }

        /// <summary>Every frame the filter is in use: the sun, and the light map. Before the filter is drawn.</summary>
        public override void PostDrawTiles()
        {
            if (Main.dedServ || !GraceBiome.FilterReady)
                return;

            Filter filter = Filters.Scene[GraceBiome.FilterKey];

            if (filter == null || !filter.IsInUse())
                return;

            UpdateSun();

            ScreenShaderData shader = filter.GetShader();
            Vector2 sun = Main.LocalPlayer.gravDir < 0f ? new Vector2(Sun.X, -Sun.Y) : Sun;

            shader.UseColor(Colour);
            shader.UseSecondaryColor(sunOnScreen.X, sunOnScreen.Y, Day);
            shader.UseDirection(sun);

            if (Day <= 0f && maps[current] != null)
                return;

            View(out Vector2 topLeft, out Vector2 size);
            BuildMap(topLeft, size);
            Upload(shader, topLeft, size);
        }

        // ------------------------------------------------------------------------ the warm light

        /// <summary>How far the plain's warm sunlight has come in, 0 to 1: a second to come in when the player comes onto the plain, and to go when they leave.</summary>
        private static float warmth;

        /// <summary>
        /// What the sun does to the tiles on the plain, as the game lights them: warmer. Needs no
        /// shader. The sky is left blue here: the filter turns blue sky to gold, and a sky already
        /// half warmed reads as grey to it.
        /// </summary>
        private static readonly Vector3 WarmTiles = new Vector3(1.08f, 0.96f, 0.74f);

        private static Color Tinted(Color colour, Vector3 by, float amount)
        {
            Vector3 c = colour.ToVector3();
            Vector3 warm = Vector3.Min(c * by, Vector3.One);
            return new Color(Vector3.Lerp(c, warm, amount)) { A = colour.A };
        }

        /// <summary>By day on the plain, the sunlight the game lights the tiles with goes warm and golden.</summary>
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
        {
            if (warmth <= 0f || !Main.dayTime)
                return;

            tileColor = Tinted(tileColor, WarmTiles, warmth * Day);
        }

        // ------------------------------------------------------------------------------ the motes

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            Player player = Main.LocalPlayer;
            bool here = player.active && !player.dead && player.InModBiome<GraceBiome>();

            warmth = MathHelper.Clamp(warmth + (here ? 1f : -1f) / 60f, 0f, 1f);

            if (!here)
                return;

            UpdateSun();

            if (Day < 0.2f)
                return;

            View(out Vector2 topLeft, out Vector2 size);

            for (int k = 0; k < 2; k++)
            {
                if (Main.rand.NextFloat() > 0.3f * Day)
                    continue;

                Vector2 at = topLeft + new Vector2(Main.rand.NextFloat(size.X), Main.rand.NextFloat(size.Y));
                Point cell = at.ToTileCoordinates();

                if (!WorldGen.InWorld(cell.X, cell.Y) || !GraceBiome.Covers(cell.X, cell.Y))
                    continue;

                Tile tile = Main.tile[cell.X, cell.Y];

                if (tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated)
                    continue;

                float light = Lit(at);

                if (light < 0.55f)
                    continue;

                Color tint = new Color(Vector3.Lerp(new Vector3(1f, 0.9f, 0.62f), Colour, 0.5f));
                PADParticleHandler.SpawnParticle(new GraceMote(at, tint, Main.rand.Next(160, 280)));
            }
        }
    }
}
