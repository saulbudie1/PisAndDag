using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using Terraria;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// Where everything is on Dona Quinn's and Triunfo's art, and the sums that put the
    /// pieces together in the world. One place for both the drawing and the AI, so the
    /// lance that is drawn is the lance that hits, and her hitbox is where her body is.
    ///
    /// Every number here was measured off the PNGs in this folder, not guessed:
    ///
    ///   TriunfoTheDonkey.png      126x1640, twenty 126x82 frames, facing right. The
    ///                             Painted Horse's layout for the first sixteen (0 stands,
    ///                             1-7 run, 9-14 gallop, 15 in the air; 8 is unused by any
    ///                             gait), and four more of its own: 16-19 reared up with
    ///                             the front legs going round.
    ///   DonaQuinnRiding_*.png     42x60, one canvas: Body (torso and the near leg,
    ///                             seated), Hand (the near arm, forearm out in front),
    ///                             Head (the face), HairBob (the hair over it).
    ///   Idle_*.png                42x60, the same canvas standing: Torso_AndLeg,
    ///                             Hand_Toppng (the arm hanging), Face, Hair.
    ///   Lance_AlsoGore.png        22x76, point up, the leather grip at rows 62-67.
    ///
    /// The saddle is not a guess either: its colours were found in every frame and the
    /// first frame's saddle fitted to each of the others. It sits at (58, 40) and moves
    /// only by whole frames: up 2 on 3, 4, 7, 8, 12 and 13, up 6 in the air (15), and on
    /// the four reared frames it is the same saddle turned 27 degrees nose up about that
    /// point and moved 1 back and 6 up. So she rises with the gallop by exactly what the
    /// art does, and leans back with the rear by exactly what the saddle does.
    ///
    /// All the art faces right. Facing left mirrors every piece about its own pivot,
    /// and every turn the right-facing rig takes goes the other way.
    ///
    /// Lying face down she is not just the standing rig turned a quarter: the head and the
    /// arm sit where DonnLyingdownRef.png has them, which is lower and further forward than
    /// the standing neck and shoulder turned with her (<see cref="LieNape"/>). The rig
    /// slides from the one to the other with how far over she is.
    /// </summary>
    public static class DonaRig
    {
        public const string Folder = "PisAndDag/Content/NPCs/DonaQuinn/";

        // ============================================================
        // TRIUNFO
        // ============================================================

        public const int SheetWidth = 126;
        public const int FrameHeight = 82;
        public const int Frames = 20;

        /// <summary>The frame point that stands on the middle of the donkey's hitbox, at its foot: between the hooves, on the ground line.</summary>
        public static readonly Vector2 Hooves = new Vector2(64f, 80f);

        public const int Standing = 0;
        public const int RunFirst = 1;
        public const int RunCount = 7;
        public const int GallopFirst = 9;
        public const int GallopCount = 6;
        public const int InAir = 15;
        public const int RearFirst = 16;
        public const int RearCount = 4;

        /// <summary>The seat of the saddle in the standing frame.</summary>
        public static readonly Vector2 Seat = new Vector2(58f, 40f);

        /// <summary>On the reared frames: the saddle turned this far about the seat, nose up, and moved this much.</summary>
        public static readonly float RearTilt = MathHelper.ToRadians(-27f);
        public static readonly Vector2 RearShift = new Vector2(-1f, -6f);

        /// <summary>How far the saddle sits from where it does standing, on this frame, not counting the rear.</summary>
        public static float SeatRise(int frame)
        {
            switch (frame)
            {
                case 3:
                case 4:
                case 7:
                case 8:
                case 12:
                case 13:
                    return -2f;
                case InAir:
                    return -6f;
                default:
                    return 0f;
            }
        }

        public static bool Reared(int frame) => frame >= RearFirst && frame < RearFirst + RearCount;

        // ============================================================
        // DONA: THE CANVAS
        // ============================================================

        public const int CanvasWidth = 42;
        public const int CanvasHeight = 60;

        // ---- riding -----------------------------------------------------------------

        /// <summary>Where she sits on the saddle, on the riding canvas.</summary>
        public static readonly Vector2 RidingSeat = new Vector2(21f, 46f);

        /// <summary>The near shoulder the riding arm turns about.</summary>
        public static readonly Vector2 RidingShoulder = new Vector2(17f, 34f);

        /// <summary>The middle of the riding fist, where the lance's grip is held.</summary>
        public static readonly Vector2 RidingFist = new Vector2(30f, 41f);

        /// <summary>The nape the riding head turns about (the standing one, two lower: the riding art sits two lower).</summary>
        public static readonly Vector2 RidingNape = new Vector2(20f, 32f);

        /// <summary>The middle of her riding body, which her hitbox is centred on.</summary>
        public static readonly Vector2 RidingMiddle = new Vector2(23f, 36f);

        // ---- on foot ----------------------------------------------------------------

        /// <summary>The middle of her standing body: what she turns about when she is thrown, and lies about.</summary>
        public static readonly Vector2 Middle = new Vector2(23f, 43f);

        /// <summary>The shoulder the hanging arm turns about.</summary>
        public static readonly Vector2 Shoulder = new Vector2(18f, 30f);

        /// <summary>
        /// The nape the head turns about: the back of the neck rather than the throat, so that
        /// lying face down with her head up the face clears the shoulders, the way the
        /// reference sketch (DonnLyingdownRef.png) has it.
        /// </summary>
        public static readonly Vector2 Nape = new Vector2(20f, 30f);

        /// <summary>
        /// Lying face down, where the head and the arm hang off the body instead, on the
        /// body's own canvas. Measured off DonnLyingdownRef.png by fitting the parts to it:
        /// the body a quarter turn over with its middle at (17, 23.5) on that picture, the
        /// head upright with its nape at (33, 27), the arm reaching out 64 degrees from
        /// hanging with its shoulder at (26.5, 24). Rebuilt from those, the parts cover the
        /// picture to within 19 of its 852 pixels, all of them round the fingers.
        /// </summary>
        public static readonly Vector2 LieNape = new Vector2(26.5f, 27f);
        public static readonly Vector2 LieShoulder = new Vector2(23.5f, 33.5f);

        /// <summary>The canvas row her soles end on.</summary>
        public const float Soles = 60f;

        /// <summary>
        /// Her middle stands this far over the ground on her feet, and this far lying flat:
        /// on the reference the hand under her chin is what touches the ground, with her
        /// middle ten and a half over it.
        /// </summary>
        public const float StandHeight = Soles - 43f;
        public const float LieHeight = 10.5f;

        /// <summary>
        /// Half the body's own picture (Idle_Torso_AndLeg.png, columns 14 to 31, rows 26 to
        /// 59, about its middle): how far its corners reach, so that turned part way over,
        /// getting up, she stands on the corner of her boot rather than through the ground.
        /// </summary>
        public const float BodyHalfWidth = 9f;
        public const float BodyHalfHeight = 17f;

        // ---- the hair -----------------------------------------------------------------

        /// <summary>
        /// The rows of each hair layer, top and bottom (exclusive), and their height in the
        /// art's own pixels (it is drawn two to a pixel). The hair sways in bands of that
        /// height, the top band still and each one under it a little further out.
        /// </summary>
        public const int HairTop = 12;
        public const int HairBottom = 32;
        public const int RidingHairTop = 14;
        public const int RidingHairBottom = 34;
        public const int HairBand = 2;

        // ---- the lance --------------------------------------------------------------

        public const int LanceWidth = 22;
        public const int LanceLength = 76;

        /// <summary>The middle of the leather grip: what the fist holds.</summary>
        public static readonly Vector2 Grip = new Vector2(11f, 65f);

        /// <summary>From the grip to the point, and to the butt, along the lance.</summary>
        public const float GripToTip = 65f;
        public const float GripToButt = 11f;

        // ============================================================
        // THE POSE
        // ============================================================

        /// <summary>
        /// Where each piece of her is this tick, in the world, and its turn. Facing is part
        /// of it, so the draw needs nothing else.
        /// </summary>
        public struct Pose
        {
            public int Facing;

            /// <summary>Where her body's own pivot is drawn (the seat riding, her middle on foot), and the body's turn.</summary>
            public Vector2 Body;
            public float BodyTurn;

            public Vector2 ShoulderAt;
            public float ArmTurn;

            public Vector2 FistAt;
            public float LanceTurn;

            public Vector2 NapeAt;
            public float HeadTurn;

            /// <summary>The middle of her, for the hitbox, the light and the bark.</summary>
            public Vector2 MiddleAt;

            /// <summary>The lance's point and butt in the world.</summary>
            public Vector2 Tip;
            public Vector2 Butt;
        }

        /// <summary>
        /// Her riding, on a donkey standing at <paramref name="hooves"/> (its hitbox's
        /// centre-bottom) facing <paramref name="facing"/>, showing <paramref name="frame"/>.
        /// <paramref name="lean"/> tips her forward (positive) on top of what the saddle does;
        /// <paramref name="arm"/>, <paramref name="lance"/> and <paramref name="head"/> are her
        /// own turns, right-facing: the lance 0 upright and a quarter turn couched forward.
        /// </summary>
        public static Pose Riding(Vector2 hooves, int facing, int frame, float lean, float arm, float lance, float head)
        {
            int d = facing >= 0 ? 1 : -1;
            Vector2 seat;
            float tilt;

            if (Reared(frame))
            {
                seat = Seat + RearShift;
                tilt = RearTilt;
            }
            else
            {
                seat = Seat + new Vector2(0f, SeatRise(frame));
                tilt = 0f;
            }

            Pose pose = new Pose
            {
                Facing = d,
                Body = hooves + new Vector2(d * (seat.X - Hooves.X), seat.Y - Hooves.Y),
                BodyTurn = d * (tilt + lean),
            };

            pose.ShoulderAt = OnRider(pose, RidingShoulder);
            pose.ArmTurn = pose.BodyTurn + d * arm;

            pose.FistAt = pose.ShoulderAt + Turn(new Vector2(d * (RidingFist.X - RidingShoulder.X),
                RidingFist.Y - RidingShoulder.Y), pose.ArmTurn);

            pose.LanceTurn = pose.BodyTurn + d * lance;
            pose.Tip = pose.FistAt + Turn(new Vector2(0f, -GripToTip), pose.LanceTurn);
            pose.Butt = pose.FistAt + Turn(new Vector2(0f, GripToButt), pose.LanceTurn);

            pose.NapeAt = OnRider(pose, RidingNape);
            pose.HeadTurn = pose.BodyTurn + d * head;

            pose.MiddleAt = OnRider(pose, RidingMiddle);
            return pose;
        }

        /// <summary>A point of the riding canvas, in the world, for this pose.</summary>
        private static Vector2 OnRider(Pose pose, Vector2 canvas)
        {
            Vector2 local = new Vector2(pose.Facing * (canvas.X - RidingSeat.X), canvas.Y - RidingSeat.Y);
            return pose.Body + Turn(local, pose.BodyTurn);
        }

        /// <summary>
        /// Her on foot, or thrown, or lying: her middle at <paramref name="middle"/>, the body
        /// turned <paramref name="body"/> (right-facing: a quarter turn forward is face down
        /// with her head the way she faces), the arm and head turned on top of that. The
        /// neck and shoulder slide from where they are standing to where they are lying
        /// (<see cref="LieNape"/>) with how far over she is.
        /// </summary>
        public static Pose OnFoot(Vector2 middle, int facing, float body, float arm, float head)
        {
            int d = facing >= 0 ? 1 : -1;

            Pose pose = new Pose
            {
                Facing = d,
                Body = middle,
                BodyTurn = d * body,
                MiddleAt = middle,
            };

            float over = Lying(body);
            Vector2 shoulder = Vector2.Lerp(Shoulder, LieShoulder, over);
            Vector2 nape = Vector2.Lerp(Nape, LieNape, over);

            pose.ShoulderAt = middle + Turn(new Vector2(d * (shoulder.X - Middle.X), shoulder.Y - Middle.Y), pose.BodyTurn);
            pose.ArmTurn = pose.BodyTurn + d * arm;

            pose.NapeAt = middle + Turn(new Vector2(d * (nape.X - Middle.X), nape.Y - Middle.Y), pose.BodyTurn);
            pose.HeadTurn = pose.BodyTurn + d * head;

            return pose;
        }

        /// <summary>How far over she is, 0 upright to 1 lying flat, for a body turned this much.</summary>
        public static float Lying(float body)
        {
            float s = (float)Math.Sin(body);
            return s * s;
        }

        /// <summary>A vector turned clockwise (on screen) by <paramref name="angle"/>.</summary>
        public static Vector2 Turn(Vector2 v, float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>
        /// One piece of art drawn with its right-facing pivot <paramref name="pivot"/> on the
        /// world point <paramref name="at"/>. Mirrored, the pivot is mirrored with it, because
        /// SpriteBatch takes the origin in the flipped picture's own pixels (MorsIdle says the
        /// same about his head).
        /// </summary>
        public static void Piece(SpriteBatch spriteBatch, Texture2D texture, Rectangle? source, Vector2 at, Vector2 pivot,
            int width, float turn, int facing, Color colour, Vector2 screenPos)
        {
            Vector2 origin = facing >= 0 ? pivot : new Vector2(width - pivot.X, pivot.Y);
            SpriteEffects flip = facing >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            spriteBatch.Draw(texture, at - screenPos, source, colour, turn, origin, 1f, flip, 0f);
        }

        /// <summary>
        /// The crowd-control tell's yellow outline (CCBypass.Outline) of one piece, placed exactly
        /// as <see cref="Piece"/> places it.
        /// </summary>
        public static void Outline(SpriteBatch spriteBatch, Texture2D texture, Rectangle? source, Vector2 at, Vector2 pivot,
            int width, float turn, int facing, Vector2 screenPos)
        {
            Vector2 origin = facing >= 0 ? pivot : new Vector2(width - pivot.X, pivot.Y);
            SpriteEffects flip = facing >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            CCBypass.Outline(spriteBatch, texture, source, at - screenPos, turn, origin, 1f, flip);
        }

        /// <summary>
        /// One band of rows of a full-canvas piece (<paramref name="top"/>, <paramref name="rows"/>
        /// high), drawn as <see cref="Piece"/> would draw the whole of it but moved along the
        /// piece's own x by <paramref name="shift"/> pixels (world right, before the turn). The
        /// hair is drawn band by band like this to sway: whole pixels, top still.
        /// </summary>
        public static void Band(SpriteBatch spriteBatch, Texture2D texture, int top, int rows, Vector2 at, Vector2 pivot,
            int width, float turn, int facing, Color colour, Vector2 screenPos, float shift)
        {
            Rectangle source = new Rectangle(0, top, width, rows);
            Vector2 origin = facing >= 0 ? new Vector2(pivot.X, pivot.Y - top) : new Vector2(width - pivot.X, pivot.Y - top);
            SpriteEffects flip = facing >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            spriteBatch.Draw(texture, at + Turn(new Vector2(shift, 0f), turn) - screenPos, source, colour, turn, origin, 1f, flip, 0f);
        }

        /// <summary>The frame of Triunfo's sheet.</summary>
        public static Rectangle Frame(int frame) => new Rectangle(0, frame * FrameHeight, SheetWidth, FrameHeight);
    }
}
