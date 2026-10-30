using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// The HEATR's field, drawn over its wearer: a thin outline two pixels out from their
    /// silhouette, fading in, with hexagons rising in it, all faint (<see cref="HEATRField"/> has
    /// its picture), worked out from what has been drawn of the body so far each time they are
    /// drawn, and drawn as the body is, so it turns and scales with them as the game does all of a
    /// player.
    ///
    /// How the body is found: the game gathers everything it draws for a player in a list
    /// (PlayerDrawSet.DrawDataCache, emptied for each player) layer by layer, in a fixed order,
    /// and turns and draws all of it after the last layer. A marker before the first of the body's
    /// layers (<see cref="HEATRMarkBody"/>, after the debuffs and auras drawn behind) notes where
    /// the body begins in it; markers about the layers that are not the body (a mount, the item
    /// held, wings, balloons, a rope's pulley: <see cref="HEATRMark"/>) note the stretches to
    /// leave out; and this layer, after the arm over the item and the gloves, before the
    /// projectiles held over the arm, takes what lies between: the skin, the clothes, the armour,
    /// the accessories worn on the body, the M.U.P.R.A.L's lamp. Everything drawn behind the body
    /// and everything over it (the debuffs, the ice barrier, the beetles) is left out.
    ///
    /// Each piece is mapped back onto its texture pixel by pixel (<see cref="HEATRField.Cover"/>),
    /// a texture's alpha read once and kept (<see cref="Alpha"/>); the picture is a texture of
    /// each player's, written each time they are drawn. While a player's body is drawn just as it
    /// was the time before (each piece's texture, part of it, place, turn, scale and mirroring:
    /// <see cref="Last"/>), it is not mapped again, and while it covers the same pixels the
    /// field's shape about it is not worked out again (HEATRField.Kept): only its hexagons, rising. Not drawn for an afterimage, a head on
    /// the map, a player the game hides, nor when the HEATR's slot hides its picture; faded as the
    /// body is (invisibility, the blinking after a hit: as faint as the least faded piece of the
    /// body). Its light is its own, not the world's: as faint in the dark as in the day.
    /// </summary>
    public class HEATRShell : PlayerDrawLayer
    {
        /// <summary>Textures larger than this (texels) are not read, and so are not the body's; and at most this many are kept.</summary>
        private const int MostTexels = 1 << 20;
        private const int MostKept = 384;

        // Where the body begins in the list this time it is drawn, and the stretches of it that are not the body.
        private static int start = -1;
        private static int skipFrom = -1;
        private static readonly List<Point> skips = new List<Point>();

        private static readonly Color[] pixels = new Color[HEATRField.Width * HEATRField.Height];

        /// <summary>
        /// One piece of the body as drawn: its texture, the part of it, where (from the picture's
        /// corner), turned, about what, scaled and mirrored. Alike only if all of that is.
        /// </summary>
        private readonly struct Piece
        {
            public readonly Texture2D Texture;
            public readonly Rectangle Source;
            public readonly Vector2 Position;
            public readonly float Rotation;
            public readonly Vector2 Origin;
            public readonly float ScaleX;
            public readonly float ScaleY;
            public readonly SpriteEffects Effect;

            public Piece(Texture2D texture, Rectangle source, Vector2 position, float rotation, Vector2 origin, float scaleX, float scaleY, SpriteEffects effect)
            {
                Texture = texture;
                Source = source;
                Position = position;
                Rotation = rotation;
                Origin = origin;
                ScaleX = scaleX;
                ScaleY = scaleY;
                Effect = effect;
            }

            public bool Alike(in Piece other) => ReferenceEquals(Texture, other.Texture) && Source == other.Source && Position == other.Position && Rotation == other.Rotation
                && Origin == other.Origin && ScaleX == other.ScaleX && ScaleY == other.ScaleY && Effect == other.Effect;
        }

        /// <summary>Each player's body as last drawn (its pieces, and the pixels they cover), and the field's shape about it: while the body is drawn just so again, neither is worked out again.</summary>
        private sealed class Last
        {
            public readonly List<Piece> Pieces = new List<Piece>();
            public readonly bool[] Body = new bool[HEATRField.Width * HEATRField.Height];
            public readonly HEATRField.Kept Field = new HEATRField.Kept();
        }

        private static Last[] lasts;
        private static readonly List<Piece> drawing = new List<Piece>();

        private static bool Alike(List<Piece> now, List<Piece> before)
        {
            if (now.Count != before.Count)
                return false;

            for (int i = 0; i < now.Count; i++)
            {
                if (!now[i].Alike(before[i]))
                    return false;
            }

            return true;
        }

        /// <summary>Each player's picture, made the first time they are drawn in it.</summary>
        private static Texture2D[] pictures;

        /// <summary>Textures' alpha, a byte a texel, read once each.</summary>
        private static readonly Dictionary<Texture2D, byte[]> alphas = new Dictionary<Texture2D, byte[]>();
        private static Color[] reading = Array.Empty<Color>();

        public override void Load()
        {
            pictures = new Texture2D[Main.player.Length];
            lasts = new Last[Main.player.Length];
        }

        public override void Unload()
        {
            Texture2D[] going = pictures;
            pictures = null;
            lasts = null;
            drawing.Clear();
            alphas.Clear();
            reading = Array.Empty<Color>();

            if (going != null && !Main.dedServ)
            {
                Main.QueueMainThreadAction(() =>
                {
                    foreach (Texture2D picture in going)
                        picture?.Dispose();
                });
            }
        }

        public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.ProjectileOverArm);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => Shows(drawInfo);

        /// <summary>The field is drawn over this player this time: the HEATR worn and its picture shown, and the player drawn whole, as themselves.</summary>
        public static bool Shows(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && !drawInfo.headOnlyRender && !drawInfo.hideEntirePlayer && drawInfo.drawPlayer.GetModPlayer<HEATRPlayer>().Shown;

        /// <summary>The body begins here in the list.</summary>
        internal static void Begin(int at)
        {
            start = at;
            skipFrom = -1;
            skips.Clear();
        }

        /// <summary>A stretch that is not the body begins here in the list, and ends here.</summary>
        internal static void SkipFrom(int at) => skipFrom = at;

        internal static void SkipTo(int at)
        {
            if (skipFrom >= 0 && at > skipFrom)
                skips.Add(new Point(skipFrom, at));

            skipFrom = -1;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            List<DrawData> drawn = drawInfo.DrawDataCache;
            int from = start;
            start = -1;

            if (from < 0 || from > drawn.Count || pictures == null || lasts == null || player.whoAmI < 0 || player.whoAmI >= pictures.Length)
                return;

            // The picture's corner: the body's frame's, as the game puts it (DrawPlayer_12_Skin_Composite), less the margins.
            int left = (int)(drawInfo.Position.X - Main.screenPosition.X - player.bodyFrame.Width / 2 + player.width / 2)
                + (int)Math.Floor(player.bodyPosition.X) - HEATRField.Side;
            int top = (int)(drawInfo.Position.Y - Main.screenPosition.Y + player.height - player.bodyFrame.Height + 4f)
                + (int)Math.Floor(player.bodyPosition.Y) - HEATRField.Above;

            float seen = 0f;
            drawing.Clear();

            for (int i = from; i < drawn.Count; i++)
            {
                if (Skipped(i))
                    continue;

                DrawData piece = drawn[i];
                Texture2D texture = piece.texture;

                if (texture == null || piece.ignorePlayerRotation || piece.color.A == 0 || Mimic.MimicOutline.IsOutline(texture))
                    continue;

                if (Alpha(texture) == null)
                    continue;

                Rectangle source = piece.sourceRect ?? texture.Bounds;

                if (source.Width <= 0 || source.Height <= 0)
                    continue;

                Vector2 position;
                float scaleX, scaleY;

                if (piece.useDestinationRectangle)
                {
                    position = new Vector2(piece.destinationRectangle.X, piece.destinationRectangle.Y);
                    scaleX = piece.destinationRectangle.Width / (float)source.Width;
                    scaleY = piece.destinationRectangle.Height / (float)source.Height;
                }
                else
                {
                    position = piece.position;
                    scaleX = piece.scale.X;
                    scaleY = piece.scale.Y;
                }

                drawing.Add(new Piece(texture, source, position - new Vector2(left, top), piece.rotation, piece.origin, scaleX, scaleY, piece.effect));
                seen = Math.Max(seen, piece.color.A / 255f);
            }

            Last last = lasts[player.whoAmI] ??= new Last();

            // The body as it is drawn this time: unless it is drawn just as last time.
            if (!Alike(drawing, last.Pieces))
            {
                Array.Clear(last.Body, 0, last.Body.Length);
                last.Pieces.Clear();

                foreach (Piece piece in drawing)
                {
                    HEATRField.Cover(Alpha(piece.Texture), piece.Texture.Width, piece.Source, piece.Position + new Vector2(left, top), piece.Rotation, piece.Origin,
                        piece.ScaleX, piece.ScaleY, (piece.Effect & SpriteEffects.FlipHorizontally) != 0, (piece.Effect & SpriteEffects.FlipVertically) != 0, left, top, last.Body);
                    last.Pieces.Add(piece);
                }
            }

            if (seen <= 0f || !HEATRField.Paint(last.Body, Main.GlobalTimeWrappedHourly, pixels, last.Field))
                return;

            Texture2D picture = pictures[player.whoAmI];

            if (picture == null || picture.IsDisposed)
            {
                picture = new Texture2D(Main.instance.GraphicsDevice, HEATRField.Width, HEATRField.Height);
                pictures[player.whoAmI] = picture;
            }

            picture.SetData(pixels);

            drawn.Add(new DrawData(picture, new Vector2(left, top), null, Color.White * seen, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f));
        }

        /// <summary>One of the fields' pictures (the Everclear Mirror's copy's gold outline leaves them out).</summary>
        internal static bool IsField(Texture2D texture)
        {
            if (texture == null || pictures == null)
                return false;

            foreach (Texture2D p in pictures)
            {
                if (ReferenceEquals(p, texture))
                    return true;
            }

            return false;
        }

        private static bool Skipped(int index)
        {
            foreach (Point skip in skips)
            {
                if (index >= skip.X && index < skip.Y)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A texture's alpha, read the first time it is asked for and kept; none for one that cannot
        /// be read so (drawn to as the game runs, or not plain colour) or is too large.
        /// </summary>
        internal static byte[] Alpha(Texture2D texture)
        {
            if (texture.IsDisposed)
            {
                alphas.Remove(texture);
                return null;
            }

            if (alphas.TryGetValue(texture, out byte[] alpha))
                return alpha;

            if (texture is RenderTarget2D || texture.Format != SurfaceFormat.Color)
                return null;

            int count = texture.Width * texture.Height;

            if (count <= 0 || count > MostTexels)
                return null;

            if (alphas.Count >= MostKept)
                alphas.Clear();

            if (reading.Length < count)
                reading = new Color[count];

            texture.GetData(reading, 0, count);
            alpha = new byte[count];

            for (int i = 0; i < count; i++)
                alpha[i] = reading[i].A;

            alphas[texture] = alpha;
            return alpha;
        }
    }

    /// <summary>Before the first of the body's layers (after the debuffs and auras drawn behind it): where the body begins in what is drawn.</summary>
    public class HEATRMarkBody : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.WebbedDebuffBack, PlayerDrawLayers.LeinforsHairShampoo);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => HEATRShell.Shows(drawInfo);

        protected override void Draw(ref PlayerDrawSet drawInfo) => HEATRShell.Begin(drawInfo.DrawDataCache.Count);
    }

    /// <summary>
    /// Marks one side of a layer that is not the body (drawn only when that layer is, and right
    /// beside it, as the game does with a layer's children), so the field leaves it out.
    /// </summary>
    public abstract class HEATRMark : PlayerDrawLayer
    {
        /// <summary>The layer that is not the body.</summary>
        protected abstract PlayerDrawLayer Layer { get; }

        /// <summary>Before it (where it begins), or after it (where it ends).</summary>
        protected abstract bool Before { get; }

        public override Position GetDefaultPosition() => Before ? new BeforeParent(Layer) : new AfterParent(Layer);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => HEATRShell.Shows(drawInfo);

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (Before)
                HEATRShell.SkipFrom(drawInfo.DrawDataCache.Count);
            else
                HEATRShell.SkipTo(drawInfo.DrawDataCache.Count);
        }
    }

    public class HEATRMarkMountFrom : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.MountFront;
        protected override bool Before => true;
    }

    public class HEATRMarkMountTo : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.MountFront;
        protected override bool Before => false;
    }

    public class HEATRMarkItemFrom : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.HeldItem;
        protected override bool Before => true;
    }

    public class HEATRMarkItemTo : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.HeldItem;
        protected override bool Before => false;
    }

    public class HEATRMarkWingsFrom : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Wings;
        protected override bool Before => true;
    }

    public class HEATRMarkWingsTo : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Wings;
        protected override bool Before => false;
    }

    public class HEATRMarkBalloonFrom : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.BalloonAcc;
        protected override bool Before => true;
    }

    public class HEATRMarkBalloonTo : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.BalloonAcc;
        protected override bool Before => false;
    }

    public class HEATRMarkPulleyFrom : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Pulley;
        protected override bool Before => true;
    }

    public class HEATRMarkPulleyTo : HEATRMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Pulley;
        protected override bool Before => false;
    }
}
