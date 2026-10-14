using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// How the Bone Squad and everything of theirs is drawn, by request ("All minion proj is
    /// desaturated and opacity like hoard of undead's but lerp glow:#34eb4f,#6afc94,#16c90c,#a3de57";
    /// "texture from the game files"): the game's own pictures, a fifth of the way to grey, at 70%
    /// opacity, inside an outline one art pixel wide whose four greens go round it counter clockwise,
    /// a turn every two seconds, not lit; as Hoard of Undead's zombies are drawn
    /// (HoardOfUndeadZombie), with these colours. The squad (BoneSquadMinion), their spells and
    /// arrows, and the dust they make (BoneSquadDust); the staff keeps its own picture and gets the
    /// outline (BoneSquad).
    ///
    /// The game's pictures are drawn in art pixels of 2 by 2 texels. An NPC's sheet and the other
    /// pictures used here have them on the even grid; a dust frame has them on a grid of its own,
    /// which Grid finds.
    /// </summary>
    public static class BoneSquadLook
    {
        /// <summary>As Hoard of Undead's: "Its texture is 70% opacity, 20% desaturated".</summary>
        public const float Opacity = 0.7f, Desaturation = 0.2f;

        /// <summary>
        /// "lerp glow:#34eb4f,#6afc94,#16c90c,#a3de57", at 70% as Hoard of Undead's outline is (the
        /// user's there). The game draws premultiplied, so 70% is the whole colour times 0.7.
        /// </summary>
        public static readonly Color[] Outline =
        {
            new Color(0x34, 0xeb, 0x4f) * 0.7f, new Color(0x6a, 0xfc, 0x94) * 0.7f, new Color(0x16, 0xc9, 0x0c) * 0.7f, new Color(0xa3, 0xde, 0x57) * 0.7f,
        };

        /// <summary>A turn of the outline's colours, in seconds (Hoard of Undead's).</summary>
        public const float OutlineTurn = 2f;

        /// <summary>The outline of a picture, or of a frame of one.</summary>
        public sealed class Ring
        {
            /// <summary>Each outline art pixel's top left texel, from the frame's top left (it may be just outside it).</summary>
            public Point[] Points;

            /// <summary>What the colours go round, in texels from the frame's top left.</summary>
            public Vector2 Middle;
        }

        private static readonly Dictionary<Texture2D, Color[]> Texels = new Dictionary<Texture2D, Color[]>();

        private static readonly Dictionary<Texture2D, Texture2D> Greyer = new Dictionary<Texture2D, Texture2D>();

        private static readonly Dictionary<(Texture2D, Rectangle), Ring> Rings = new Dictionary<(Texture2D, Rectangle), Ring>();

        private static readonly Dictionary<(Texture2D, int, int), Vector2> Middles = new Dictionary<(Texture2D, int, int), Vector2>();

        /// <summary>Lets go of what was made here (the greyer pictures on the thread that draws).</summary>
        public static void Unload()
        {
            foreach (Texture2D made in Greyer.Values)
            {
                Texture2D picture = made;
                Main.QueueMainThreadAction(() => picture.Dispose());
            }

            Greyer.Clear();
            Texels.Clear();
            Rings.Clear();
            Middles.Clear();
        }

        private static Color[] TexelsOf(Texture2D own)
        {
            if (Texels.TryGetValue(own, out Color[] texels))
                return texels;

            texels = new Color[own.Width * own.Height];
            own.GetData(texels);
            Texels[own] = texels;
            return texels;
        }

        /// <summary>The picture a fifth of the way to its own grey (its brightness to the eye), each texel; the alpha kept. Made the first time it is drawn, on the thread that draws.</summary>
        public static Texture2D GreyerOf(Texture2D own)
        {
            if (Greyer.TryGetValue(own, out Texture2D made))
                return made;

            Color[] texels = TexelsOf(own);
            Color[] greyer = new Color[texels.Length];
            for (int i = 0; i < texels.Length; i++)
            {
                Color c = texels[i];
                float grey = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
                greyer[i] = new Color(
                    (int)Math.Round(c.R + (grey - c.R) * Desaturation),
                    (int)Math.Round(c.G + (grey - c.G) * Desaturation),
                    (int)Math.Round(c.B + (grey - c.B) * Desaturation),
                    (int)c.A);
            }

            made = new Texture2D(Main.instance.GraphicsDevice, own.Width, own.Height);
            made.SetData(greyer);
            Greyer[own] = made;
            return made;
        }

        /// <summary>
        /// Where a frame's art pixels lie: the one of the four 2 by 2 grids (offset 0 or 1 across and
        /// down, from the frame's top left) that cuts the fewest of them in two, the first of any as few.
        /// </summary>
        private static Point Grid(Color[] texels, int width, Rectangle source)
        {
            Point best = Point.Zero;
            int fewest = int.MaxValue;

            for (int oy = 0; oy < 2; oy++)
            {
                for (int ox = 0; ox < 2; ox++)
                {
                    int cut = 0;
                    for (int by = oy - 2; by < source.Height; by += 2)
                    {
                        for (int bx = ox - 2; bx < source.Width; bx += 2)
                        {
                            int inside = 0, filled = 0;
                            for (int dy = 0; dy < 2; dy++)
                            {
                                for (int dx = 0; dx < 2; dx++)
                                {
                                    int x = bx + dx, y = by + dy;
                                    if (x < 0 || y < 0 || x >= source.Width || y >= source.Height)
                                        continue;

                                    inside++;
                                    if (texels[(source.Y + y) * width + source.X + x].A > 0)
                                        filled++;
                                }
                            }

                            if (filled > 0 && filled < inside)
                                cut++;
                        }
                    }

                    if (cut < fewest)
                    {
                        fewest = cut;
                        best = new Point(ox, oy);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The outline of a picture's frame: every art pixel next to one that is not empty (side by
        /// side, not corner to corner) and empty itself. The colours go round the middle given (an
        /// NPC's whole sheet's: SheetMiddle), or else round the middle of the frame's own art pixels.
        /// On the even grid (an NPC's sheet, an item's picture, an arrow), or on the frame's own (a dust's).
        /// </summary>
        public static Ring RingOf(Texture2D own, Rectangle source, Vector2? middle = null, bool evenGrid = false)
        {
            if (Rings.TryGetValue((own, source), out Ring made))
                return made;

            Color[] texels = TexelsOf(own);
            int width = own.Width;
            Point grid = evenGrid ? Point.Zero : Grid(texels, width, source);

            bool Filled(int i, int j)
            {
                for (int dy = 0; dy < 2; dy++)
                {
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int x = grid.X + 2 * i + dx, y = grid.Y + 2 * j + dy;
                        if (x < 0 || y < 0 || x >= source.Width || y >= source.Height)
                            continue;

                        if (texels[(source.Y + y) * width + source.X + x].A > 0)
                            return true;
                    }
                }

                return false;
            }

            int fromI = grid.X > 0 ? -1 : 0, toI = (source.Width - 1 - grid.X) / 2;
            int fromJ = grid.Y > 0 ? -1 : 0, toJ = (source.Height - 1 - grid.Y) / 2;
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            var points = new List<Point>();

            for (int j = fromJ - 1; j <= toJ + 1; j++)
            {
                for (int i = fromI - 1; i <= toI + 1; i++)
                {
                    if (Filled(i, j))
                    {
                        minX = Math.Min(minX, grid.X + 2 * i);
                        maxX = Math.Max(maxX, grid.X + 2 * i + 1);
                        minY = Math.Min(minY, grid.Y + 2 * j);
                        maxY = Math.Max(maxY, grid.Y + 2 * j + 1);
                        continue;
                    }

                    if (Filled(i - 1, j) || Filled(i + 1, j) || Filled(i, j - 1) || Filled(i, j + 1))
                        points.Add(new Point(grid.X + 2 * i, grid.Y + 2 * j));
                }
            }

            made = new Ring
            {
                Points = points.ToArray(),
                Middle = middle ?? (minX > maxX ? new Vector2(source.Width / 2f, source.Height / 2f) : new Vector2((minX + maxX + 1) / 2f, (minY + maxY + 1) / 2f)),
            };

            Rings[(own, source)] = made;
            return made;
        }

        /// <summary>
        /// The middle of an NPC's whole sheet (Hoard of Undead's): of the art pixels of all its frames
        /// at once, so that the colours go round the same point whichever frame it is in.
        /// </summary>
        public static Vector2 SheetMiddle(Texture2D own, int frames, int frameHeight)
        {
            if (Middles.TryGetValue((own, frames, frameHeight), out Vector2 middle))
                return middle;

            Color[] texels = TexelsOf(own);
            int width = own.Width;
            int across = width / 2, down = frameHeight / 2;
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;

            for (int f = 0; f < frames; f++)
            {
                for (int j = 0; j < down; j++)
                {
                    for (int i = 0; i < across; i++)
                    {
                        bool filled = false;
                        for (int dy = 0; dy < 2 && !filled; dy++)
                            for (int dx = 0; dx < 2 && !filled; dx++)
                                filled = texels[(f * frameHeight + j * 2 + dy) * width + i * 2 + dx].A > 0;

                        if (!filled)
                            continue;

                        minX = Math.Min(minX, i);
                        maxX = Math.Max(maxX, i);
                        minY = Math.Min(minY, j);
                        maxY = Math.Max(maxY, j);
                    }
                }
            }

            middle = minX > maxX ? new Vector2(width / 2f, frameHeight / 2f) : new Vector2(minX + maxX + 1, minY + maxY + 1);
            Middles[(own, frames, frameHeight)] = middle;
            return middle;
        }

        /// <summary>The colours evenly round a turn, the last running back into the first: where a point a number of turns round is.</summary>
        public static Color Round(float turns)
        {
            float around = (turns - (float)Math.Floor(turns)) * Outline.Length;
            int i = Math.Min((int)around, Outline.Length - 1);
            return Color.Lerp(Outline[i], Outline[(i + 1) % Outline.Length], around - i);
        }

        /// <summary>
        /// Where a point of a frame is drawn, by the same numbers the frame is drawn with (as
        /// SpriteBatch lays a frame out: flipped within itself, then about its origin, scaled and turned).
        /// </summary>
        public static Vector2 Place(Vector2 point, Rectangle source, Vector2 position, float rotation, Vector2 origin, float scale, SpriteEffects effects)
        {
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
                point.X = source.Width - point.X;
            if ((effects & SpriteEffects.FlipVertically) != 0)
                point.Y = source.Height - point.Y;

            return position + ((point - origin) * scale).RotatedBy(rotation);
        }

        /// <summary>
        /// A frame of a picture as these draw it: a fifth of the way to grey, at 70% of the colour given,
        /// and its outline round it (as faint as fade says). With a batch of null it goes through
        /// Main.EntitySpriteDraw, as a projectile's own drawing does.
        /// </summary>
        public static void Draw(SpriteBatch batch, Texture2D own, Vector2 position, Rectangle source, Color colour, float rotation, Vector2 origin, float scale, SpriteEffects effects, Ring ring, float fade = 1f)
        {
            Texture2D picture = GreyerOf(own);
            if (batch == null)
                Main.EntitySpriteDraw(picture, position, source, colour * Opacity, rotation, origin, scale, effects, 0f);
            else
                batch.Draw(picture, position, source, colour * Opacity, rotation, origin, scale, effects, 0f);

            DrawRing(batch, ring, position, source, rotation, origin, scale, effects, fade);
        }

        /// <summary>A frame's outline, art pixel by art pixel, each the colour of where it is round the middle; not lit.</summary>
        public static void DrawRing(SpriteBatch batch, Ring ring, Vector2 position, Rectangle source, float rotation, Vector2 origin, float scale, SpriteEffects effects, float fade = 1f)
        {
            if (fade <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle dot = new Rectangle(0, 0, 1, 1);
            Vector2 half = new Vector2(0.5f, 0.5f);
            Vector2 middle = Place(ring.Middle, source, position, rotation, origin, scale, effects);
            float turned = Main.GlobalTimeWrappedHourly / OutlineTurn;

            foreach (Point point in ring.Points)
            {
                Vector2 spot = Place(new Vector2(point.X + 1, point.Y + 1), source, position, rotation, origin, scale, effects);
                Vector2 from = spot - middle;
                float angle = (float)Math.Atan2(-from.Y, from.X);
                Color colour = Round(turned - angle / MathHelper.TwoPi) * fade;

                if (batch == null)
                    Main.EntitySpriteDraw(pixel, spot, dot, colour, rotation, half, 2f * scale, SpriteEffects.None, 0f);
                else
                    batch.Draw(pixel, spot, dot, colour, rotation, half, 2f * scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>The same outline as draw data, for what a player holds (drawn later with the rest of the player), as faint as fade says.</summary>
        public static void RingData(List<DrawData> into, Ring ring, Vector2 position, Rectangle source, float rotation, Vector2 origin, float scale, SpriteEffects effects, int shader, float fade = 1f)
        {
            if (fade <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle dot = new Rectangle(0, 0, 1, 1);
            Vector2 half = new Vector2(0.5f, 0.5f);
            Vector2 middle = Place(ring.Middle, source, position, rotation, origin, scale, effects);
            float turned = Main.GlobalTimeWrappedHourly / OutlineTurn;

            foreach (Point point in ring.Points)
            {
                Vector2 spot = Place(new Vector2(point.X + 1, point.Y + 1), source, position, rotation, origin, scale, effects);
                Vector2 from = spot - middle;
                float angle = (float)Math.Atan2(-from.Y, from.X);
                DrawData data = new DrawData(pixel, spot, dot, Round(turned - angle / MathHelper.TwoPi) * fade, rotation, half, 2f * scale, SpriteEffects.None, 0f);
                data.shader = shader;
                into.Add(data);
            }
        }
    }

    /// <summary>Lets go of BoneSquadLook's pictures when the mod is unloaded.</summary>
    public class BoneSquadLookSystem : ModSystem
    {
        public override void Unload() => BoneSquadLook.Unload();
    }
}
