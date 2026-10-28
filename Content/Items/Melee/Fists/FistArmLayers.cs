using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee.Fists
{
    /// <summary>
    /// The player's own arms, posed by a fist (FistPlayer.Front, FistPlayer.Back; set by FistPunch): what the game draws
    /// for each arm, every piece of it (skin, undershirt, shirt, hand, body armour, glove, their dyes, an armour's glow),
    /// taken as it is laid down and turned to another picture of the arm map (FistArms), another turn and a stretch, the
    /// shoulder kept where it was. While the arm strikes, two faint copies of it part way between last tick's pose and
    /// this one are laid under it, in the weapon's colour and adding light (the smear).
    ///
    /// Done between two marks round each of the game's own layers that lay the arm down (the back arm with the skin, the
    /// front arm over the held item): what was added between them, cut from the same cell as the game's arm (or that
    /// cell's glow, a sheet lower), is the arm.
    /// </summary>
    public static class FistArmLayers
    {
        /// <summary>How far down a composite sheet its glow is: a sheet's height of arm cells (4 rows of 56).</summary>
        private const int GlowRows = 224;

        /// <summary>The smear: how far between last tick's pose and this one each copy is, and how much of it shows.</summary>
        private static readonly (float k, float alpha)[] Copies = { (0.3f, 0.3f), (0.65f, 0.5f) };

        /// <summary>
        /// The arm's pieces laid down since <paramref name="from"/>, posed: <paramref name="armRect"/> is the cell the game
        /// cut them from (the way to know them from the rest).
        /// </summary>
        public static void Pose(ref PlayerDrawSet drawInfo, int from, Rectangle armRect, bool back, ArmPose pose, ArmPose last, bool smear, Color smearColour)
        {
            List<DrawData> cache = drawInfo.DrawDataCache;
            Player player = drawInfo.drawPlayer;
            bool flipX = (drawInfo.playerEffect & SpriteEffects.FlipHorizontally) != 0;
            bool flipY = (drawInfo.playerEffect & SpriteEffects.FlipVertically) != 0;
            int sx = flipX ? -1 : 1, sy = flipY ? -1 : 1;
            bool wasComposite = armRect.X / Math.Max(1, armRect.Width) >= 7;
            bool copies = smear && drawInfo.shadow == 0f;

            int end = cache.Count;
            List<DrawData> under = null;

            for (int i = from; i < end; i++)
            {
                DrawData d = cache[i];

                if (!d.sourceRect.HasValue)
                    continue;

                Rectangle r = d.sourceRect.Value;

                if (r.X != armRect.X || r.Width != armRect.Width || r.Height != armRect.Height)
                    continue;

                int glow = r.Y - armRect.Y;

                if (glow != 0 && glow != GlowRows)
                    continue;

                cache[i] = Posed(d, player, pose, glow, back, wasComposite, flipX, sx, sy);

                if (!copies || glow != 0)
                    continue;

                foreach ((float k, float alpha) in Copies)
                {
                    DrawData ghost = Posed(d, player, Between(last, pose, k), 0, back, wasComposite, flipX, sx, sy);
                    Color c = smearColour * alpha;
                    c.A = 0;
                    ghost.color = c;
                    ghost.shader = 0;
                    (under ??= new List<DrawData>()).Add(ghost);
                }
            }

            if (under != null)
                cache.InsertRange(from, under);
        }

        /// <summary>The arm part way (<paramref name="k"/>) from <paramref name="last"/> to <paramref name="now"/>: now's picture, its hand turned and its stretch eased between.</summary>
        public static ArmPose Between(ArmPose last, ArmPose now, float k)
        {
            float a0 = last.HandFromShoulder.ToRotation();
            float a1 = now.HandFromShoulder.ToRotation();
            float turn = MathHelper.WrapAngle(a1 - a0);
            return ArmPose.Aimed(now.Frame, a0 + turn * k, Vector2.Lerp(last.Scale, now.Scale, k));
        }

        private static DrawData Posed(DrawData d, Player player, ArmPose pose, int glow, bool back, bool wasComposite, bool flipX, int sx, int sy)
        {
            Rectangle rect = pose.Frame.Rect;
            rect.Y += glow;
            d.sourceRect = rect;
            d.rotation = player.bodyRotation + pose.Turn * sx * sy;
            d.scale = pose.Scale;

            if (back)
            {
                // The front arm's pictures on the back arm: their shoulder put on the back arm's.
                d.origin = new Vector2(flipX ? 40f - FistArms.Shoulder.X : FistArms.Shoulder.X, FistArms.Shoulder.Y);
            }
            else if (wasComposite && !pose.Frame.Composite)
            {
                // The game nudges its turning arm a pixel out and down; these are not that arm.
                d.position -= new Vector2(sx, sy);
            }

            return d;
        }
    }

    /// <summary>Marks where the back arm starts to be laid down (with the skin).</summary>
    public class FistBackArmStart : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Skin);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => drawInfo.drawPlayer.GetModPlayer<FistPlayer>().BackPosed;

        protected override void Draw(ref PlayerDrawSet drawInfo) =>
            drawInfo.drawPlayer.GetModPlayer<FistPlayer>().BackFrom = drawInfo.DrawDataCache.Count;
    }

    /// <summary>The back arm, posed, once it is down.</summary>
    public class FistBackArmEnd : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Skin);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => drawInfo.drawPlayer.GetModPlayer<FistPlayer>().BackPosed;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            FistPlayer fists = drawInfo.drawPlayer.GetModPlayer<FistPlayer>();

            if (fists.BackFrom < 0 || !drawInfo.usesCompositeTorso)
                return;

            FistArmLayers.Pose(ref drawInfo, fists.BackFrom, drawInfo.compBackArmFrame, true, fists.Back, fists.BackLast, fists.BackSmear, fists.Smear);
            fists.BackFrom = -1;
        }
    }

    /// <summary>Marks where the front arm starts to be laid down (over the held item).</summary>
    public class FistFrontArmStart : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => drawInfo.drawPlayer.GetModPlayer<FistPlayer>().FrontPosed;

        protected override void Draw(ref PlayerDrawSet drawInfo) =>
            drawInfo.drawPlayer.GetModPlayer<FistPlayer>().FrontFrom = drawInfo.DrawDataCache.Count;
    }

    /// <summary>The front arm, posed, once it is down.</summary>
    public class FistFrontArmEnd : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => drawInfo.drawPlayer.GetModPlayer<FistPlayer>().FrontPosed;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            FistPlayer fists = drawInfo.drawPlayer.GetModPlayer<FistPlayer>();

            if (fists.FrontFrom < 0 || !drawInfo.usesCompositeTorso)
                return;

            FistArmLayers.Pose(ref drawInfo, fists.FrontFrom, drawInfo.compFrontArmFrame, false, fists.Front, fists.FrontLast, fists.FrontSmear, fists.Smear);
            fists.FrontFrom = -1;
        }
    }
}
