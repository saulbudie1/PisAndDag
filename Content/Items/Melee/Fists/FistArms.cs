using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee.Fists
{
    /// <summary>
    /// One of the player's own arm pictures (the arm map, asked: "look thru arm map: figure out what arm pos do what
    /// like bent, extend, you can rotate them and stretch and warp them to give a dynamic effect"): a cell of the game's
    /// 40 by 56 player sheet (the skin's arm, its undershirt, its shirt and hand, and every body armour's composite
    /// sheet, all laid out alike), and where its hand is in it, facing right.
    /// </summary>
    public readonly struct ArmFrame
    {
        public readonly Point Cell;
        public readonly Vector2 Hand;

        public ArmFrame(int column, int row, float handX, float handY)
        {
            Cell = new Point(column, row);
            Hand = new Vector2(handX, handY);
        }

        public Rectangle Rect => new Rectangle(Cell.X * 40, Cell.Y * 56, 40, 56);

        /// <summary>The hand from the shoulder, facing right, in pixels.</summary>
        public Vector2 Reach => Hand - FistArms.Shoulder;

        /// <summary>Whether it is one of the cells the game turns about the shoulder for SetCompositeArm (column 7 or 8).</summary>
        public bool Composite => Cell.X >= 7;
    }

    /// <summary>
    /// An arm as a fist poses it: which picture, turned how far about the shoulder (clockwise, as if facing right),
    /// drawn how much longer along the picture's width and how much narrower down its height (the stretch). Laid over the
    /// game's own arm by FistArmLayers; the hand's place worked out the same way here.
    /// </summary>
    public struct ArmPose
    {
        public ArmFrame Frame;
        public float Turn;
        public Vector2 Scale;

        public ArmPose(ArmFrame frame, float turn, Vector2 scale)
        {
            Frame = frame;
            Turn = turn;
            Scale = scale;
        }

        /// <summary>The picture turned so that its hand points along <paramref name="angle"/> (facing right, clockwise from ahead), stretched by <paramref name="scale"/>.</summary>
        public static ArmPose Aimed(ArmFrame frame, float angle, Vector2 scale)
        {
            Vector2 reach = frame.Reach * scale;
            return new ArmPose(frame, angle - (float)Math.Atan2(reach.Y, reach.X), scale);
        }

        /// <summary>The hand from the shoulder, facing right, turned and stretched.</summary>
        public Vector2 HandFromShoulder => (Frame.Reach * Scale).RotatedBy(Turn);
    }

    /// <summary>
    /// The arm map, measured off the game's own pictures (Player_0_7, _8, _9: the hand's middle in each cell; the shoulder
    /// the front arm hangs from, (15, 28) in its cell, where the game turns it): which pictures the fists use, and what
    /// each one is. Angles from the shoulder, facing right, clockwise from ahead.
    ///
    /// Row 0 is the arm held out, the frames the game swings an item with: hanging (2,0), raised behind (3,0), up and out
    /// at -38 degrees (4,0), straight out a little low at 28 (5,0), down and out at 49 (6,0), the longest. Row 1 is the arm
    /// bent at the elbow, the walk's: the hand at the hip (3,1), behind (4,1), forward (5,1), further forward (6,1), a
    /// guard. Column 7 is the arm the game turns for SetCompositeArmFront, hanging, a little shorter each row down (full,
    /// three quarters, a quarter, folded: 11, 10, 8, 6 pixels to the hand); column 8 the back arm's; rows 2 and 3 of
    /// columns 2 to 6 are slivers of the back arm, most of it hidden by the body.
    /// </summary>
    public static class FistArms
    {
        /// <summary>The front arm's shoulder in its cell, facing right: where the game turns it.</summary>
        public static readonly Vector2 Shoulder = new Vector2(15f, 28f);

        public static readonly ArmFrame Hanging = new ArmFrame(2, 0, 12.5f, 39.2f);
        public static readonly ArmFrame RaisedBehind = new ArmFrame(3, 0, 10.5f, 19.8f);
        public static readonly ArmFrame UpAndOut = new ArmFrame(4, 0, 25f, 20.2f);
        public static readonly ArmFrame StraightOut = new ArmFrame(5, 0, 27.2f, 34.5f);
        public static readonly ArmFrame DownAndOut = new ArmFrame(6, 0, 24.7f, 39.3f);
        public static readonly ArmFrame BentAtHip = new ArmFrame(3, 1, 14.9f, 34.9f);
        public static readonly ArmFrame BentBehind = new ArmFrame(4, 1, 12.9f, 34.9f);
        public static readonly ArmFrame BentForward = new ArmFrame(5, 1, 16.9f, 34.9f);
        public static readonly ArmFrame Guard = new ArmFrame(6, 1, 18.9f, 34.9f);

        /// <summary>
        /// The shoulder in the world, as the game draws the arm from it: the player's middle, three up (five behind for the
        /// front arm, six ahead and two down for the back), and the walk's bob, the game's own sum for its arms
        /// (PlayerDrawLayers.DrawPlayer_28_ArmOverItemComposite and DrawPlayer_12_SkinComposite_BackArmShirt).
        /// </summary>
        public static Vector2 ShoulderAt(Player player, bool back)
        {
            Vector2 centre = player.RotatedRelativePoint(player.MountedCenter, true, true);
            int frame = player.bodyFrame.Height > 0 ? player.bodyFrame.Y / player.bodyFrame.Height : 0;
            Vector2 walk = frame >= 0 && frame < Main.OffsetsPlayerHeadgear.Length ? Main.OffsetsPlayerHeadgear[frame] : new Vector2(0f, 2f);
            walk.Y -= 2f;

            Vector2 offset = back ? new Vector2(6f * player.direction, -1f * player.gravDir) : new Vector2(-5f * player.direction, -3f * player.gravDir);
            return centre + offset + new Vector2(0f, walk.Y * player.gravDir);
        }

        /// <summary>Where a posed arm's hand is in the world.</summary>
        public static Vector2 HandAt(Player player, bool back, ArmPose pose)
        {
            Vector2 up = pose.HandFromShoulder;
            return ShoulderAt(player, back) + new Vector2(up.X * player.direction, up.Y * player.gravDir).RotatedBy(player.bodyRotation);
        }
    }
}
