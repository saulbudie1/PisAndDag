using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Accessories;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// The Everclear Mirror's copy, outlined in gold (by request: "give only the body a Golden Outline"): one of the
    /// game's pixels (two of the screen's) all round its body, behind it, so it is told from its owner at a glance.
    ///
    /// Only the body: the skin, the clothes, the armour and what is worn on the body. Not the weapon it holds, its wings,
    /// a balloon, a mount or a rope's pulley. The body is found as the HEATR's field finds its wearer's (HEATRShell):
    /// the game gathers what it draws for a player in a list (PlayerDrawSet.DrawDataCache), layer by layer; a marker
    /// before the body's first layer notes where it begins, markers about the layers that are not the body note the
    /// stretches to leave out, and this layer, after the arm over the item, takes the pieces between, maps each back to
    /// the pixels it covers (HEATRField.Cover), and draws the gold where the body is not and lies within two pixels of
    /// it. The gold goes into the list where the body begins (a place kept there, unseen, by the marker, and filled
    /// here: nothing else's place in the list moves, the HEATR's field among them), so it is under the body and the
    /// weapon, and turns and scales with the copy as the game does all of a player. The HEATR's field, if the copy
    /// wears one, is not body to it, nor the gold to the field. Two golds: the bright one next to the body, a deeper one
    /// outside it. Not drawn for its afterimages, a head on the map, nor when the game hides it; faded as the body is.
    /// </summary>
    public class MimicOutline : PlayerDrawLayer
    {
        /// <summary>The gold next to the body, and the deeper gold outside it.</summary>
        public static readonly Color Inner = new Color(255, 214, 92);
        public static readonly Color Outer = new Color(196, 132, 28);

        private const int W = HEATRField.Width;
        private const int H = HEATRField.Height;

        // Where the body begins in the list this time the copy is drawn, and the stretches of it that are not the body.
        private static int start = -1;
        private static int skipFrom = -1;
        private static readonly List<Point> skips = new List<Point>();

        private static readonly bool[] body = new bool[W * H];
        private static readonly Color[] pixels = new Color[W * H];
        private static Texture2D picture;

        public override void Unload()
        {
            Texture2D going = picture;
            picture = null;

            if (going != null && !Main.dedServ)
                Main.QueueMainThreadAction(() => going.Dispose());
        }

        public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.ProjectileOverArm);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => Shows(drawInfo);

        /// <summary>The copy, drawn whole, as itself (not an afterimage or a head on the map).</summary>
        public static bool Shows(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && !drawInfo.headOnlyRender && !drawInfo.hideEntirePlayer && MimicSystem.Is(drawInfo.drawPlayer);

        /// <summary>The gold's own picture (the HEATR's field leaves it out).</summary>
        internal static bool IsOutline(Texture2D texture) => texture != null && ReferenceEquals(texture, picture);

        internal static void Begin(int at)
        {
            start = at;
            skipFrom = -1;
            skips.Clear();
        }

        internal static void SkipFrom(int at) => skipFrom = at;

        internal static void SkipTo(int at)
        {
            if (skipFrom >= 0 && at > skipFrom)
                skips.Add(new Point(skipFrom, at));

            skipFrom = -1;
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

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            List<DrawData> drawn = drawInfo.DrawDataCache;
            int from = start;
            start = -1;

            if (from < 0 || from >= drawn.Count)
                return;

            // The picture's corner: the body's frame's, as the game puts it (DrawPlayer_12_Skin_Composite), less the
            // margins (as HEATRShell).
            int left = (int)(drawInfo.Position.X - Main.screenPosition.X - player.bodyFrame.Width / 2 + player.width / 2)
                + (int)Math.Floor(player.bodyPosition.X) - HEATRField.Side;
            int top = (int)(drawInfo.Position.Y - Main.screenPosition.Y + player.height - player.bodyFrame.Height + 4f)
                + (int)Math.Floor(player.bodyPosition.Y) - HEATRField.Above;

            Array.Clear(body, 0, body.Length);
            float seen = 0f;

            for (int i = from; i < drawn.Count; i++)
            {
                if (Skipped(i))
                    continue;

                DrawData piece = drawn[i];
                Texture2D texture = piece.texture;

                if (texture == null || piece.ignorePlayerRotation || piece.color.A == 0 || HEATRShell.IsField(texture))
                    continue;

                byte[] alpha = HEATRShell.Alpha(texture);

                if (alpha == null)
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

                HEATRField.Cover(alpha, texture.Width, source, position, piece.rotation, piece.origin, scaleX, scaleY,
                    (piece.effect & SpriteEffects.FlipHorizontally) != 0, (piece.effect & SpriteEffects.FlipVertically) != 0, left, top, body);
                seen = Math.Max(seen, piece.color.A / 255f);
            }

            if (seen <= 0f)
                return;

            // The gold: where the body is not, next to it (a pixel away, across or up and down) the bright one; a pixel
            // further (two across or up and down, or one on a corner) the deeper one.
            bool any = false;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Color c = Color.Transparent;

                    if (!body[i])
                    {
                        if (At(x - 1, y) || At(x + 1, y) || At(x, y - 1) || At(x, y + 1))
                            c = Inner;
                        else if (At(x - 2, y) || At(x + 2, y) || At(x, y - 2) || At(x, y + 2)
                            || At(x - 1, y - 1) || At(x + 1, y - 1) || At(x - 1, y + 1) || At(x + 1, y + 1))
                            c = Outer;
                    }

                    any |= c.A > 0;
                    pixels[i] = c;
                }
            }

            if (!any)
                return;

            if (picture == null || picture.IsDisposed)
                picture = new Texture2D(Main.instance.GraphicsDevice, W, H);

            picture.SetData(pixels);

            // Under the body (and the weapon over it): in the place kept where the body begins in the list.
            drawn[from] = new DrawData(picture, new Vector2(left, top), null, Color.White * seen, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        private static bool At(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && body[y * W + x];
    }

    /// <summary>Before the first of the body's layers: where the copy's body begins in what is drawn.</summary>
    public class MimicOutlineBody : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.WebbedDebuffBack, PlayerDrawLayers.LeinforsHairShampoo);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => MimicOutline.Shows(drawInfo);

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            // A place for the gold, unseen until it is filled (MimicOutline.Draw), so it goes under the body.
            MimicOutline.Begin(drawInfo.DrawDataCache.Count);
            drawInfo.DrawDataCache.Add(new DrawData(Terraria.GameContent.TextureAssets.MagicPixel.Value, Vector2.Zero, new Rectangle(0, 0, 1, 1),
                Color.Transparent, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f));
        }
    }

    /// <summary>One side of a layer that is not the copy's body (right beside it), so its outline leaves it out.</summary>
    public abstract class MimicOutlineMark : PlayerDrawLayer
    {
        protected abstract PlayerDrawLayer Layer { get; }

        protected abstract bool Before { get; }

        public override Position GetDefaultPosition() => Before ? new BeforeParent(Layer) : new AfterParent(Layer);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => MimicOutline.Shows(drawInfo);

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (Before)
                MimicOutline.SkipFrom(drawInfo.DrawDataCache.Count);
            else
                MimicOutline.SkipTo(drawInfo.DrawDataCache.Count);
        }
    }

    public class MimicOutlineMountFrom : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.MountFront;
        protected override bool Before => true;
    }

    public class MimicOutlineMountTo : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.MountFront;
        protected override bool Before => false;
    }

    public class MimicOutlineItemFrom : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.HeldItem;
        protected override bool Before => true;
    }

    public class MimicOutlineItemTo : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.HeldItem;
        protected override bool Before => false;
    }

    public class MimicOutlineWingsFrom : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Wings;
        protected override bool Before => true;
    }

    public class MimicOutlineWingsTo : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Wings;
        protected override bool Before => false;
    }

    public class MimicOutlineBalloonFrom : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.BalloonAcc;
        protected override bool Before => true;
    }

    public class MimicOutlineBalloonTo : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.BalloonAcc;
        protected override bool Before => false;
    }

    public class MimicOutlinePulleyFrom : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Pulley;
        protected override bool Before => true;
    }

    public class MimicOutlinePulleyTo : MimicOutlineMark
    {
        protected override PlayerDrawLayer Layer => PlayerDrawLayers.Pulley;
        protected override bool Before => false;
    }
}
