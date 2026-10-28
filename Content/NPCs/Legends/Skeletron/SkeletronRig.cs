using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// Skeletron's body as the mod draws it, in every world (by request: new hands "so it can have more dexterity",
    /// "dissect the finger textures to use and rotate them", and the new skull with a jaw of its own), in both of his
    /// looks: his own, and the red hat (by request, "remake for the counter part variation": the hands the user drew are
    /// the red hat's, recoloured here into his own bone; the skull the user drew is his own, recoloured here into the red
    /// hat's, the hat lifted off the game's own red-hat skull onto it).
    ///
    /// THE HAND. One picture for both: the left hand is the right one mirrored (by request, "LR hand use same texture js
    /// mirror"). The palm (HandBareNoFinger) with its knuckles, and five digits on top of it, each cut into its three
    /// bones (FingerAtlas, cut from FullFingers by the build script, three pieces a digit, and the first bone again as
    /// Fingers draws it, closed off at the knuckle, for a finger curled under). Every picture is laid out on the same 56
    /// by 76 canvas, wrist at the top, fingers down, thumb on the right; so a point of the hand is a point of that canvas,
    /// and is put in the world by the hand's wrist, its angle (0: fingers down), its mirroring and its scale.
    ///
    /// A digit is posed by three numbers (<see cref="Finger"/>): its splay (the whole digit turned at its knuckle, in the
    /// picture's plane), its bend (folded toward the palm, away from the viewer: its bones shortened as they turn away,
    /// and the finger leaning in toward the thumb as it folds; curled further, only its first bone is seen, as Fingers
    /// draws it, shorter and drawn back onto the knuckles the further it curls, slanted in: by request, "the fist doesnt
    /// fully take away all the finger, it rotates them inwards and gives it a slightly slanted effect") and its hook (each
    /// bone turned on from the one before it, in the plane: a claw, a flick held back, a pinch, the thumb wrapped round).
    ///
    /// THE HEAD. The skull (Upper) and the jaw (Jaw), on the same 80 by 102 canvas as his body. Opening the mouth drops
    /// the jaw; the skull can lift off it too (by request, "seperate upper skull for spitting skulls"); what is opened
    /// between them is the mouth (SkeletronMouth), stretched to fit. The red hat sits on the skull and goes with it.
    ///
    /// THE ARMS. The game's own arm bone (Images/Arm_Bone his, Images/Arm_Bone_3 the red hat's, copied as SkeletronArmBone),
    /// one picture for the upper arm and one for the forearm as the game draws him, from his shoulder to the wrist, bent at
    /// the elbow; more of them, end to end, when a hand is further off than an arm reaches.
    /// </summary>
    internal static class SkeletronRig
    {
        internal const string Folder = "PisAndDag/Content/NPCs/Legends/Skeletron/";

        /// <summary>His two looks: 0 his own, 1 the red hat.</summary>
        private static readonly Asset<Texture2D>[] PalmTexture = new Asset<Texture2D>[2];
        private static readonly Asset<Texture2D>[] FingerAtlas = new Asset<Texture2D>[2];
        private static readonly Asset<Texture2D>[] UpperTexture = new Asset<Texture2D>[2];
        private static readonly Asset<Texture2D>[] JawTexture = new Asset<Texture2D>[2];
        private static readonly Asset<Texture2D>[] BoneTexture = new Asset<Texture2D>[2];
        private static readonly Asset<Texture2D>[] SpikeTexture = new Asset<Texture2D>[2];
        private static Asset<Texture2D> HatTexture;
        private static Asset<Texture2D> MouthTexture;
        private static Asset<Texture2D> GlowTexture;
        private static Asset<Texture2D> HotTexture;
        private static Asset<Texture2D> LineTexture;
        private static Asset<Texture2D> StarTexture;
        private static Asset<Texture2D> OldManTexture;

        internal static void Load()
        {
            if (Main.dedServ)
                return;

            for (int red = 0; red < 2; red++)
            {
                string look = red == 1 ? "Red_Hat_" : "";

                PalmTexture[red] = ModContent.Request<Texture2D>(Folder + look + "Skeletron_HandBareNoFinger");
                FingerAtlas[red] = ModContent.Request<Texture2D>(Folder + look + "SkeletronFingerAtlas");
                UpperTexture[red] = ModContent.Request<Texture2D>(Folder + look + "Skeletron_Upper");
                JawTexture[red] = ModContent.Request<Texture2D>(Folder + look + "Skeletron_Jaw");
                BoneTexture[red] = ModContent.Request<Texture2D>(Folder + look + "SkeletronArmBone");
                SpikeTexture[red] = ModContent.Request<Texture2D>(Folder + look + "SkeletronSpike");
            }

            HatTexture = ModContent.Request<Texture2D>(Folder + "Red_Hat_Skeletron_Hat");
            MouthTexture = ModContent.Request<Texture2D>(Folder + "SkeletronMouth");
            GlowTexture = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft");
            HotTexture = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowHot");
            LineTexture = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
            StarTexture = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowStar");
            OldManTexture = ModContent.Request<Texture2D>(Folder + "Skeletron_OldMan");
        }

        internal static void Unload()
        {
            for (int red = 0; red < 2; red++)
            {
                PalmTexture[red] = null;
                FingerAtlas[red] = null;
                UpperTexture[red] = null;
                JawTexture[red] = null;
                BoneTexture[red] = null;
                SpikeTexture[red] = null;
            }

            HatTexture = null;
            MouthTexture = null;
            GlowTexture = null;
            HotTexture = null;
            LineTexture = null;
            StarTexture = null;
            OldManTexture = null;
        }

        /// <summary>Everything this draws with has arrived (on a server, never).</summary>
        internal static bool Ready
        {
            get
            {
                if (Main.dedServ)
                    return false;

                for (int red = 0; red < 2; red++)
                {
                    if (!Loaded(PalmTexture[red]) || !Loaded(FingerAtlas[red]) || !Loaded(UpperTexture[red]) || !Loaded(JawTexture[red])
                        || !Loaded(BoneTexture[red]) || !Loaded(SpikeTexture[red]))
                        return false;
                }

                return Loaded(HatTexture) && Loaded(MouthTexture) && Loaded(GlowTexture) && Loaded(HotTexture) && Loaded(LineTexture) && Loaded(StarTexture);
            }
        }

        private static bool Loaded(Asset<Texture2D> asset) => asset != null && asset.IsLoaded;

        private static int Look(bool red) => red ? 1 : 0;

        /// <summary>The spike out of the ground, in his look (the skull's colours).</summary>
        internal static Texture2D Spike(bool red) => Ready ? SpikeTexture[Look(red)].Value : null;

        /// <summary>The jaw on its own, in his look (thrown, as a boomerang).</summary>
        internal static Texture2D Jaw(bool red) => Ready ? JawTexture[Look(red)].Value : null;

        /// <summary>
        /// The old man his coming tears out of (Skeletron_OldMan: the game's Images/NPC_37 copied as it is, all its frames,
        /// so it can be repainted for the scene without touching the old man elsewhere); null until it has arrived.
        /// </summary>
        internal static Texture2D OldMan => Loaded(OldManTexture) ? OldManTexture.Value : null;

        /// <summary>The old man picture's frames, one above the next (the game's 21).</summary>
        internal const int OldManFrames = 21;

        /// <summary>One of his arm bones, in his look (a bullet of bone, a pin).</summary>
        internal static Texture2D Bone(bool red) => Ready ? BoneTexture[Look(red)].Value : null;

        // ============================================================================================================
        // THE HAND
        // ============================================================================================================

        /// <summary>The hand canvas: the wrist (where the arm meets it), and the hand's middle (where the NPC is).</summary>
        internal static readonly Vector2 Wrist = new Vector2(21f, 3f);
        internal static readonly Vector2 HandMiddle = new Vector2(22f, 30f);
        internal static readonly Vector2 PalmMiddle = new Vector2(22f, 20f);

        internal enum Digit : byte
        {
            Thumb,
            Index,
            Middle,
            Ring,
            Pinky,
        }

        /// <summary>
        /// A bone of a digit in the atlas: where it is there, the joint it turns about (in its own picture), and, on the
        /// hand's canvas at rest, that joint and the next one (its far end).
        /// </summary>
        private readonly struct Piece
        {
            public readonly Rectangle Rect;
            public readonly Vector2 Pivot;
            public readonly Vector2 From;
            public readonly Vector2 To;

            public Piece(Rectangle rect, Vector2 pivot, Vector2 from, Vector2 to)
            {
                Rect = rect;
                Pivot = pivot;
                From = from;
                To = to;
            }
        }

        /// <summary>
        /// The atlas, as the build script cut it (skel/build_assets.py, the same layout in both looks): four pieces a
        /// digit, thumb to little finger: its three bones, and its first bone closed off at the knuckle (folded away).
        /// </summary>
        private static readonly Piece[] Pieces =
        {
            new Piece(new Rectangle(2, 2, 14, 18), new Vector2(5.0f, 1.0f), new Vector2(45.0f, 13.0f), new Vector2(50.0f, 28.0f)),
            new Piece(new Rectangle(18, 2, 10, 10), new Vector2(4.0f, 0.0f), new Vector2(50.0f, 28.0f), new Vector2(52.0f, 36.0f)),
            new Piece(new Rectangle(30, 2, 8, 8), new Vector2(4.0f, 0.0f), new Vector2(52.0f, 36.0f), new Vector2(52.0f, 44.0f)),
            new Piece(new Rectangle(40, 2, 12, 16), new Vector2(5.0f, 1.0f), new Vector2(45.0f, 13.0f), new Vector2(47.0f, 28.0f)),

            new Piece(new Rectangle(54, 2, 10, 18), new Vector2(6.0f, 1.0f), new Vector2(42.0f, 39.0f), new Vector2(42.0f, 54.0f)),
            new Piece(new Rectangle(66, 2, 8, 12), new Vector2(5.0f, 0.0f), new Vector2(43.0f, 54.0f), new Vector2(42.0f, 64.0f)),
            new Piece(new Rectangle(76, 2, 8, 12), new Vector2(4.0f, 0.0f), new Vector2(42.0f, 64.0f), new Vector2(43.0f, 76.0f)),
            new Piece(new Rectangle(2, 22, 10, 18), new Vector2(6.0f, 1.0f), new Vector2(42.0f, 39.0f), new Vector2(41.0f, 56.0f)),

            new Piece(new Rectangle(14, 22, 8, 16), new Vector2(5.0f, 1.0f), new Vector2(29.0f, 43.0f), new Vector2(28.0f, 56.0f)),
            new Piece(new Rectangle(24, 22, 8, 12), new Vector2(4.0f, 0.0f), new Vector2(28.0f, 56.0f), new Vector2(28.0f, 66.0f)),
            new Piece(new Rectangle(34, 22, 8, 10), new Vector2(3.0f, 0.0f), new Vector2(27.0f, 66.0f), new Vector2(27.0f, 76.0f)),
            new Piece(new Rectangle(44, 22, 8, 16), new Vector2(5.0f, 1.0f), new Vector2(29.0f, 43.0f), new Vector2(28.0f, 58.0f)),

            new Piece(new Rectangle(54, 22, 10, 16), new Vector2(6.0f, 1.0f), new Vector2(18.0f, 41.0f), new Vector2(16.0f, 54.0f)),
            new Piece(new Rectangle(66, 22, 8, 12), new Vector2(3.0f, 0.0f), new Vector2(15.0f, 54.0f), new Vector2(16.0f, 64.0f)),
            new Piece(new Rectangle(76, 22, 8, 10), new Vector2(4.0f, 0.0f), new Vector2(16.0f, 64.0f), new Vector2(16.0f, 74.0f)),
            new Piece(new Rectangle(2, 42, 10, 16), new Vector2(6.0f, 1.0f), new Vector2(18.0f, 41.0f), new Vector2(16.0f, 56.0f)),

            new Piece(new Rectangle(14, 42, 10, 18), new Vector2(4.0f, 1.0f), new Vector2(4.0f, 37.0f), new Vector2(5.0f, 52.0f)),
            new Piece(new Rectangle(26, 42, 8, 12), new Vector2(3.0f, 0.0f), new Vector2(3.0f, 52.0f), new Vector2(5.0f, 62.0f)),
            new Piece(new Rectangle(36, 42, 8, 10), new Vector2(3.0f, 0.0f), new Vector2(3.0f, 62.0f), new Vector2(4.0f, 72.0f)),
            new Piece(new Rectangle(46, 42, 10, 18), new Vector2(4.0f, 1.0f), new Vector2(4.0f, 37.0f), new Vector2(5.0f, 54.0f)),
        };

        /// <summary>
        /// A digit's pose: turned at its knuckle (Splay), folded toward the palm (Bend, 0 to 1: from CurledFrom on, seen
        /// end on), and its second and third bones each turned on from the one before (Hook at the middle joint, Tip at
        /// the last). Every bone keeps its length: a digit is posed only by turning its bones at their joints.
        /// </summary>
        internal struct Finger
        {
            public float Splay;
            public float Bend;
            public float Hook;
            public float Tip;

            public Finger(float splay, float bend, float hook, float tip)
            {
                Splay = splay;
                Bend = bend;
                Hook = hook;
                Tip = tip;
            }

            public static Finger Lerp(Finger a, Finger b, float t) =>
                new Finger(MathHelper.Lerp(a.Splay, b.Splay, t), MathHelper.Lerp(a.Bend, b.Bend, t), MathHelper.Lerp(a.Hook, b.Hook, t),
                    MathHelper.Lerp(a.Tip, b.Tip, t));
        }

        /// <summary>
        /// The pose of a whole hand: its five digits, thumb first; and which side of it is shown (Front: 0 its back, as he
        /// usually shows it; 1 its palm, the picture mirrored and the curled fingers folded over the palm in front of it;
        /// turned over at half way).
        /// </summary>
        internal struct Pose
        {
            public Finger Thumb;
            public Finger Index;
            public Finger Middle;
            public Finger Ring;
            public Finger Pinky;
            public float Front;

            public Pose(Finger thumb, Finger index, Finger middle, Finger ring, Finger pinky, float front = 0f)
            {
                Thumb = thumb;
                Index = index;
                Middle = middle;
                Ring = ring;
                Pinky = pinky;
                Front = front;
            }

            public Finger this[int digit]
            {
                readonly get => digit switch
                {
                    0 => Thumb,
                    1 => Index,
                    2 => Middle,
                    3 => Ring,
                    _ => Pinky,
                };
                set
                {
                    switch (digit)
                    {
                        case 0: Thumb = value; break;
                        case 1: Index = value; break;
                        case 2: Middle = value; break;
                        case 3: Ring = value; break;
                        default: Pinky = value; break;
                    }
                }
            }

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                t = MathHelper.Clamp(t, 0f, 1f);

                return new Pose(Finger.Lerp(a.Thumb, b.Thumb, t), Finger.Lerp(a.Index, b.Index, t), Finger.Lerp(a.Middle, b.Middle, t),
                    Finger.Lerp(a.Ring, b.Ring, t), Finger.Lerp(a.Pinky, b.Pinky, t), MathHelper.Lerp(a.Front, b.Front, t));
            }

            /// <summary>This pose with one digit set otherwise.</summary>
            public readonly Pose With(Digit digit, Finger finger)
            {
                Pose copy = this;
                copy[(int)digit] = finger;
                return copy;
            }
        }

        private static Finger F(float splay, float bend, float hook = 0f) => new Finger(splay, bend, hook, hook);

        private static Finger F(float splay, float bend, float hook, float tip) => new Finger(splay, bend, hook, tip);

        private static readonly Finger Folded = F(0f, 1f);
        private static readonly Finger Straight = F(0f, 0f);

        /// <summary>The thumb in a fist: down the side of the hand and wrapped in under the curled fingers.</summary>
        private static readonly Finger Tucked = F(0.3f, 0.15f, 0.55f);

        /// <summary>The hand as drawn: everything straight.</summary>
        internal static readonly Pose Open = new Pose(Straight, Straight, Straight, Straight, Straight);

        /// <summary>Hanging off him, a little curled and a little apart.</summary>
        internal static readonly Pose Relaxed = new Pose(F(0f, 0f, 0.06f), F(-0.04f, 0.1f), F(0f, 0.12f), F(0.04f, 0.12f), F(0.08f, 0.1f));

        /// <summary>Every digit out and apart.</summary>
        internal static readonly Pose Spread = new Pose(F(-0.35f, 0f), F(-0.12f, 0f), F(0f, 0f), F(0.12f, 0f), F(0.3f, 0f));

        internal static readonly Pose Fist = new Pose(Tucked, Folded, Folded, Folded, Folded);

        internal static readonly Pose Point = new Pose(Tucked, Straight, Folded, Folded, Folded);

        /// <summary>
        /// The forefinger raised, the palm shown, the other fingers folded down over it and the thumb out (by request, the
        /// user's sketch: "it can even be mirror and fingers go infront to imitate a raised pointer"): wagging "no".
        /// </summary>
        internal static readonly Pose Wag = new Pose(F(-0.1f, 0f, 0.05f), Straight, Folded, Folded, Folded, 1f);

        /// <summary>Every digit out and apart, the palm shown: paper.</summary>
        internal static readonly Pose Paper = new Pose(F(-0.25f, 0f), F(-0.14f, 0f), F(0f, 0f), F(0.14f, 0f), F(0.34f, 0f), 1f);

        /// <summary>By request: "raise middle finger".</summary>
        internal static readonly Pose Bird = new Pose(Tucked, Folded, Straight, Folded, Folded);

        /// <summary>Thumb and forefinger meeting in a ring, the rest out: "ok".</summary>
        internal static readonly Pose Okay = new Pose(F(-0.6f, 0f, 0.4f), F(-0.5f, 0f, -1.25f), F(0.02f, 0f), F(0.14f, 0f), F(0.28f, 0f));

        /// <summary>
        /// Thumb and forefinger pinching, the rest folded in: what holds a player by the head. By request, "the pinch hand
        /// can open the thumb a bit more then shoot right up creating a right angle; index curved into 3 sections".
        /// </summary>
        internal static readonly Pose Pinch = new Pose(F(-1.25f, 0f, 1.57f, -0.1f), F(-1.1f, 0f, -1.2f), Folded, Folded, Folded);

        /// <summary>The forefinger held back by the thumb, ready to flick; and let go.</summary>
        internal static readonly Pose FlickReady = new Pose(F(-0.6f, 0f, 0.4f), F(-0.5f, 0f, -1.25f), F(0f, 0f, 0.08f), F(0.05f, 0f, 0.08f), F(0.1f, 0f, 0.08f));
        internal static readonly Pose FlickOut = new Pose(F(0.2f, 0f, -0.2f), F(0.25f, 0f, 0.15f), F(0f, 0f), F(0f, 0f), F(0f, 0f));

        /// <summary>Thumb and middle finger pressed, ready to snap; and snapped.</summary>
        internal static readonly Pose SnapReady = new Pose(F(0.35f, 0f), F(0f, 0.9f), F(-0.45f, 0.2f, -0.3f), Folded, Folded);
        internal static readonly Pose SnapDone = new Pose(F(-0.3f, 0f, 0.1f), F(0f, 0.9f), Folded, Folded, Folded);

        /// <summary>A finger gun: forefinger out, thumb up.</summary>
        internal static readonly Pose Gun = new Pose(F(-0.55f, 0f, -0.05f), Straight, Folded, Folded, Folded);

        /// <summary>Two fingers out and apart (scissors), and together (scissors shut).</summary>
        internal static readonly Pose Scissors = new Pose(Tucked, F(-0.22f, 0f), F(0.12f, 0f), Folded, Folded);
        internal static readonly Pose ScissorsShut = new Pose(Tucked, F(-0.03f, 0f), F(0.02f, 0f), Folded, Folded);

        /// <summary>Forefinger and little finger out: rock on.</summary>
        internal static readonly Pose Horns = new Pose(Tucked, F(-0.1f, 0f), Folded, Folded, F(0.15f, 0f));

        /// <summary>A bowling grip: two fingers hooked into an eye socket of his skull, the rest folded, the thumb tucked.</summary>
        internal static readonly Pose Bowl = new Pose(Tucked, F(-0.05f, 0f, 0.55f, 0.65f), F(0.05f, 0f, 0.55f, 0.65f), Folded, Folded);

        /// <summary>Half curled to take hold of something.</summary>
        internal static readonly Pose Grab = new Pose(F(0.3f, 0f, 0.35f), F(0f, 0.35f, 0.2f), F(0f, 0.35f, 0.15f), F(0f, 0.35f, 0.1f), F(0f, 0.35f, 0.05f));

        /// <summary>Taking hold: fingers hooked right round.</summary>
        internal static readonly Pose Clutch = new Pose(F(0.45f, 0f, 0.5f), F(0.1f, 0.55f, 0.45f), F(0.05f, 0.55f, 0.4f), F(0f, 0.55f, 0.35f), F(-0.05f, 0.55f, 0.3f));

        /// <summary>A flat hand, fingers together.</summary>
        internal static readonly Pose Flat = new Pose(F(-0.15f, 0f), F(0.03f, 0f), F(0f, 0f), F(-0.03f, 0f), F(-0.06f, 0f));

        /// <summary>Strumming: a pick held between thumb and forefinger, the rest curled.</summary>
        internal static readonly Pose Strum = new Pose(F(-1.15f, 0f, 1.57f, 0f), F(-1.05f, 0f, -1.3f), F(0f, 0.7f), F(0f, 0.75f), F(0f, 0.8f));

        // ---- where things are ---------------------------------------------------------------------------------------

        /// <summary>The angle for a hand whose fingers point this way (0: down).</summary>
        internal static float AngleFor(Vector2 way) => way == Vector2.Zero ? 0f : way.ToRotation() - MathHelper.PiOver2;

        /// <summary>The way a hand's fingers point at this angle.</summary>
        internal static Vector2 WayOf(float angle) => (angle + MathHelper.PiOver2).ToRotationVector2();

        /// <summary>A point of the hand's canvas, in the world.</summary>
        internal static Vector2 Canvas(Vector2 canvas, Vector2 wrist, float angle, bool mirror, float scale)
        {
            Vector2 local = canvas - Wrist;

            if (mirror)
                local.X = -local.X;

            return wrist + local.RotatedBy(angle) * scale;
        }

        /// <summary>Where the wrist is for a hand whose middle (the NPC's centre) is here.</summary>
        internal static Vector2 WristFromMiddle(Vector2 middle, float angle, bool mirror, float scale)
        {
            Vector2 local = Wrist - HandMiddle;

            if (mirror)
                local.X = -local.X;

            return middle + local.RotatedBy(angle) * scale;
        }

        /// <summary>A digit bent this far is curled under: seen end on.</summary>
        private const float CurledFrom = 0.6f;

        /// <summary>How far a digit folded right in is drawn back onto the knuckles (canvas pixels).</summary>
        private const float Tuck = 5f;

        /// <summary>How far each digit leans in toward the thumb, folded (the thumb's own way is its pose's).</summary>
        private static readonly float[] Slant = { 0f, -0.22f, -0.30f, -0.38f, -0.46f };

        /// <summary>One bone to draw: which piece, at which joint, turned how, and whether folded back past its joint.</summary>
        private struct DigitBone
        {
            public int Piece;
            public Vector2 Joint;
            public float Turn;
            public bool Back;

            public DigitBone(int piece, Vector2 joint, float turn, bool back)
            {
                Piece = piece;
                Joint = joint;
                Turn = turn;
                Back = back;
            }
        }

        /// <summary>
        /// A digit on the hand's canvas, posed: where its tip is, and its bones to draw, first drawn first (up to three).
        /// Rigid: every bone keeps its length and is only turned at its joint. Shown from the back, a curled digit is its
        /// first bone end on (the short piece), drawn back onto the knuckles and slanted in toward the thumb; shown from the
        /// palm, its first bone end on at the knuckle and its middle bone folded back over the palm in front of it.
        /// </summary>
        private static int Chain(int digit, Finger finger, bool front, DigitBone[] bones, out Vector2 tip)
        {
            Piece p0 = Pieces[digit * 4];
            Piece p1 = Pieces[digit * 4 + 1];
            Piece p2 = Pieces[digit * 4 + 2];
            Piece shortened = Pieces[digit * 4 + 3];

            float bend = MathHelper.Clamp(finger.Bend, 0f, 1f);
            bool curled = bend >= CurledFrom;

            // Leaning in toward the thumb as it folds (shown from the back).
            float lean = front ? 0f : Slant[digit] * MathHelper.SmoothStep(0f, 1f, Math.Min(1f, bend / 0.8f));

            float a0 = finger.Splay + lean;
            float a1 = a0 + finger.Hook + 0.5f * lean;
            float a2 = a1 + finger.Tip + 0.5f * lean;

            if (curled && !front)
            {
                float by = MathHelper.Clamp((bend - CurledFrom) / (1f - CurledFrom), 0f, 1f);
                Vector2 knuckle = shortened.From + new Vector2(0f, -Tuck * by).RotatedBy(finger.Splay);

                tip = knuckle + (shortened.To - shortened.From).RotatedBy(a0);
                bones[0] = new DigitBone(digit * 4 + 3, knuckle, a0, false);
                return 1;
            }

            if (curled)
            {
                Vector2 v = p1.To - p1.From;

                tip = shortened.From + new Vector2(v.X, -v.Y).RotatedBy(a1);
                bones[0] = new DigitBone(digit * 4 + 1, shortened.From, a1, true);
                bones[1] = new DigitBone(digit * 4 + 3, shortened.From, a0, false);
                return 2;
            }

            Vector2 j0 = p0.From;
            Vector2 j1 = j0 + (p0.To - p0.From).RotatedBy(a0);
            Vector2 j2 = j1 + (p1.To - p1.From).RotatedBy(a1);

            tip = j2 + (p2.To - p2.From).RotatedBy(a2);
            bones[0] = new DigitBone(digit * 4, j0, a0, false);
            bones[1] = new DigitBone(digit * 4 + 1, j1, a1, false);
            bones[2] = new DigitBone(digit * 4 + 2, j2, a2, false);
            return 3;
        }

        [ThreadStatic]
        private static DigitBone[] chainBones;

        private static DigitBone[] ChainBones => chainBones ??= new DigitBone[3];

        /// <summary>The tip of a digit, posed, in the world.</summary>
        internal static Vector2 Tip(Digit digit, in Pose pose, Vector2 wrist, float angle, bool mirror, float scale)
        {
            bool front = Facing(pose, ref mirror);
            Chain((int)digit, pose[(int)digit], front, ChainBones, out Vector2 tip);
            return Canvas(tip, wrist, angle, mirror, scale);
        }

        /// <summary>
        /// Which side of the hand a pose shows: its palm (true) once it is turned more than half way, the picture then
        /// mirrored the other way (a turn of the wrist, all at once: no bone is ever squashed).
        /// </summary>
        private static bool Facing(in Pose pose, ref bool mirror)
        {
            bool palm = pose.Front >= 0.5f;

            if (palm)
                mirror = !mirror;

            return palm;
        }

        /// <summary>Between the tips of the thumb and the forefinger, in the world: what a pinch or a ring holds.</summary>
        internal static Vector2 PinchPoint(in Pose pose, Vector2 wrist, float angle, bool mirror, float scale) =>
            (Tip(Digit.Thumb, pose, wrist, angle, mirror, scale) + Tip(Digit.Index, pose, wrist, angle, mirror, scale)) * 0.5f;

        // ---- drawn -------------------------------------------------------------------------------------------------

        /// <summary>
        /// The hand, posed, its wrist at <paramref name="wrist"/> (world), turned by <paramref name="angle"/> (0: fingers
        /// down), the left one mirrored. In the colour given (the light where it is, and whatever tints it).
        /// </summary>
        internal static void DrawHand(SpriteBatch spriteBatch, bool red, Vector2 wrist, float angle, bool mirror, float scale, in Pose pose, Color colour)
        {
            if (!Ready)
                return;

            Texture2D palm = PalmTexture[Look(red)].Value;
            Texture2D atlas = FingerAtlas[Look(red)].Value;
            bool front = Facing(pose, ref mirror);
            Vector2 screen = Main.screenPosition;

            Vector2 palmOrigin = mirror ? new Vector2(palm.Width - Wrist.X, Wrist.Y) : Wrist;
            spriteBatch.Draw(palm, wrist - screen, null, colour, angle, palmOrigin, scale, mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

            // Little finger first, the thumb last, over the curled fingers it wraps.
            DigitBone[] bones = ChainBones;

            for (int digit = 4; digit >= 0; digit--)
            {
                int count = Chain(digit, pose[digit], front, bones, out _);

                for (int i = 0; i < count; i++)
                    DrawPiece(spriteBatch, atlas, bones[i], wrist, angle, mirror, scale, colour, screen);
            }
        }

        /// <summary>A bone at its joint, turned; folded back past the joint toward the wrist, the picture flipped end for end.</summary>
        private static void DrawPiece(SpriteBatch spriteBatch, Texture2D atlas, DigitBone bone, Vector2 wrist, float angle, bool mirror, float scale,
            Color colour, Vector2 screen)
        {
            Piece piece = Pieces[bone.Piece];
            Vector2 at = Canvas(bone.Joint, wrist, angle, mirror, scale);
            Vector2 origin = new Vector2(mirror ? piece.Rect.Width - piece.Pivot.X : piece.Pivot.X, bone.Back ? piece.Rect.Height - piece.Pivot.Y : piece.Pivot.Y);
            float rotation = angle + (mirror ? -bone.Turn : bone.Turn);
            SpriteEffects effects = (mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None) | (bone.Back ? SpriteEffects.FlipVertically : SpriteEffects.None);

            spriteBatch.Draw(atlas, at - screen, piece.Rect, colour, rotation, origin, scale, effects, 0f);
        }

        /// <summary>
        /// The hand's outline in a colour of light: eight copies a little out from it, adding to what is under them (the
        /// same way as the mod's warning outline, CCBypass.Outline). Drawn before the hand, so only the rim shows.
        /// </summary>
        internal static void DrawHandRim(SpriteBatch spriteBatch, bool red, Vector2 wrist, float angle, bool mirror, float scale, in Pose pose, Color light, float reach)
        {
            Color glow = light;
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * reach;
                DrawHand(spriteBatch, red, wrist + offset, angle, mirror, scale, pose, glow);
            }
        }

        // ============================================================================================================
        // THE ARMS
        // ============================================================================================================

        /// <summary>
        /// The length of each of an arm's two bones, at scale 1 (one bone picture each), and how far a picture runs past
        /// each end of its bone, so the knobs at the elbow overlap and no gap shows.
        /// </summary>
        internal const float UpperArm = 116f;
        internal const float Forearm = 116f;
        internal const float BoneOverlap = 4f;

        /// <summary>
        /// Where the elbow is, for an arm from <paramref name="shoulder"/> to <paramref name="wrist"/>: bent out to its
        /// side (<paramref name="outward"/>, -1 left, 1 right) and down; straight when the hand is out of reach.
        /// </summary>
        internal static Vector2 Elbow(Vector2 shoulder, Vector2 wrist, float outward, float scale) =>
            Elbow(shoulder, wrist, new Vector2(outward, 0.7f), scale);

        /// <summary>Where the elbow is, bent toward <paramref name="prefer"/>.</summary>
        internal static Vector2 Elbow(Vector2 shoulder, Vector2 wrist, Vector2 prefer, float scale)
        {
            float upper = UpperArm * scale;
            float lower = Forearm * scale;
            Vector2 d = wrist - shoulder;
            float length = d.Length();

            if (length >= (upper + lower) * 0.995f || length < 1f)
                return shoulder + d * (upper / (upper + lower));

            // How far along the line the elbow is, and how far off it (the law of cosines).
            float along = (length * length + upper * upper - lower * lower) / (2f * length);
            float off = (float)Math.Sqrt(Math.Max(0f, upper * upper - along * along));

            Vector2 way = d / length;
            Vector2 normal = new Vector2(-way.Y, way.X);

            if (Vector2.Dot(normal, prefer) < 0f)
                normal = -normal;

            return shoulder + way * along + normal * off;
        }

        /// <summary>How far an arm reaches, at scale 1: a little short of its two bones end to end, so it is never quite straight.</summary>
        internal const float Reach = (UpperArm + Forearm) * 0.97f;

        /// <summary>
        /// An arm's joints for a hand at <paramref name="wrist"/>: where it starts, and its elbow. It starts at the
        /// shoulder; when the hand is further off than the arm reaches, the arm comes away from the body and goes with the
        /// hand, its top in the air short of the shoulder (by request: "only 2 segments of arm, dont have to stay rigid to
        /// the body"; the game's own arms never meet his skull either).
        /// </summary>
        internal static void ArmJoints(Vector2 shoulder, Vector2 wrist, Vector2 prefer, float scale, out Vector2 top, out Vector2 elbow)
        {
            Vector2 d = wrist - shoulder;
            float length = d.Length();
            float reach = Reach * scale;

            top = length > reach ? wrist - d / length * reach : shoulder;
            elbow = Elbow(top, wrist, prefer, scale);
        }

        /// <summary>An arm: its two bones, upper arm and forearm, each one picture of the game's arm bone, in the light where it is.</summary>
        internal static void DrawArm(SpriteBatch spriteBatch, bool red, Vector2 shoulder, Vector2 wrist, float outward, float scale, Color tint, float alpha = 1f) =>
            DrawArm(spriteBatch, red, shoulder, wrist, new Vector2(outward, 0.7f), scale, tint, alpha);

        /// <summary>An arm, its elbow bent toward <paramref name="prefer"/>.</summary>
        internal static void DrawArm(SpriteBatch spriteBatch, bool red, Vector2 shoulder, Vector2 wrist, Vector2 prefer, float scale, Color tint, float alpha = 1f)
        {
            if (!Ready)
                return;

            ArmJoints(shoulder, wrist, prefer, scale, out Vector2 top, out Vector2 elbow);

            DrawBones(spriteBatch, red, top, elbow, scale, tint, alpha, 1e6f);
            DrawBones(spriteBatch, red, elbow, wrist, scale, tint, alpha, 1e6f);
        }

        /// <summary>
        /// Bones laid end to end from one point to another, one picture to a bone of an arm's length (more when the
        /// length takes more), each stretched along to fit; as wide as the game draws them.
        /// </summary>
        internal static void DrawBones(SpriteBatch spriteBatch, bool red, Vector2 from, Vector2 to, float scale, Color tint, float alpha = 1f, float span = UpperArm)
        {
            if (!Ready)
                return;

            Texture2D bone = BoneTexture[Look(red)].Value;
            Vector2 d = to - from;
            float length = d.Length();

            if (length < 2f)
                return;

            float each = span * scale;
            int count = Math.Max(1, (int)Math.Round(length / each));
            float piece = length / count;
            float rotation = d.ToRotation() - MathHelper.PiOver2;
            Vector2 origin = bone.Size() * 0.5f;

            // Each picture a little longer than its share, so the knobs overlap and no gap shows between them.
            Vector2 stretch = new Vector2(scale, (piece + 2f * BoneOverlap * scale) / bone.Height);

            for (int i = 0; i < count; i++)
            {
                Vector2 middle = from + d * ((i + 0.5f) / count);
                Color light = Lighting.GetColor((int)(middle.X / 16f), (int)(middle.Y / 16f));
                Color colour = Multiply(light, tint) * alpha;

                spriteBatch.Draw(bone, middle - Main.screenPosition, null, colour, rotation, origin, stretch, SpriteEffects.None, 0f);
            }
        }

        /// <summary>A point along a quadratic curve from a to c, pulled toward b.</summary>
        internal static Vector2 Curve(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return a * (u * u) + b * (2f * u * t) + c * (t * t);
        }

        // ============================================================================================================
        // THE HEAD
        // ============================================================================================================

        /// <summary>The head canvas's middle, and where the mouth parts on it (the skull's teeth, the jaw's top).</summary>
        internal static readonly Vector2 HeadMiddle = new Vector2(40f, 51f);
        private const float MouthTop = 66f;
        private const float MouthBottom = 84f;
        private const float MouthLeft = 18f;
        private const float MouthRight = 62f;

        /// <summary>The hat's top-left on the head canvas (the game's red-hat frame, less where the skull sits in it).</summary>
        private static readonly Vector2 HatCorner = new Vector2(-16f, -14f);

        /// <summary>His eye sockets on the canvas.</summary>
        internal static readonly Vector2 LeftEye = new Vector2(21f, 38.7f);
        internal static readonly Vector2 RightEye = new Vector2(59.9f, 38.5f);

        /// <summary>
        /// Cracks his own blows put in his skull (the dive, when it hits): three, one more each time, each a line on the
        /// skull's canvas and its branch.
        /// </summary>
        private static readonly Vector2[][] Cracks =
        {
            new[] { new Vector2(33f, 4f), new Vector2(36f, 11f), new Vector2(33f, 17f), new Vector2(37f, 24f), new Vector2(35f, 29f) },
            new[] { new Vector2(36f, 11f), new Vector2(42f, 15f), new Vector2(46f, 14f) },
            new[] { new Vector2(64f, 12f), new Vector2(58f, 18f), new Vector2(61f, 25f), new Vector2(55f, 31f) },
            new[] { new Vector2(58f, 18f), new Vector2(52f, 17f) },
            new[] { new Vector2(10f, 22f), new Vector2(16f, 27f), new Vector2(13f, 32f), new Vector2(19f, 36f) },
            new[] { new Vector2(47f, 56f), new Vector2(51f, 61f), new Vector2(48f, 66f), new Vector2(52f, 71f) },
        };

        /// <summary>
        /// The head: the mouth behind (when it is open), the jaw dropped by <paramref name="jaw"/> pixels (or gone), the
        /// skull lifted by <paramref name="lift"/>, the red hat on it lifted by <paramref name="hatLift"/> more, all turned
        /// by <paramref name="rotation"/> about its middle at <paramref name="centre"/> (world). Cracks to
        /// <paramref name="cracked"/> (0 to 3).
        /// </summary>
        internal static void DrawHead(SpriteBatch spriteBatch, bool red, Vector2 centre, float rotation, float scale, float jaw, float lift, Color colour,
            bool withJaw = true, float hatLift = 0f, int cracked = 0, bool hat = true)
        {
            if (!Ready)
                return;

            Texture2D upper = UpperTexture[Look(red)].Value;
            Texture2D lower = JawTexture[Look(red)].Value;
            Vector2 screen = Main.screenPosition;
            Vector2 origin = HeadMiddle;

            if (jaw + lift > 6f || !withJaw)
                DrawMouth(spriteBatch, centre, rotation, scale, withJaw ? jaw : 6f, lift, colour);

            if (withJaw)
                spriteBatch.Draw(lower, centre + new Vector2(0f, jaw * scale).RotatedBy(rotation) - screen, null, colour, rotation, origin, scale, SpriteEffects.None, 0f);

            Vector2 skullAt = centre + new Vector2(0f, -lift * scale).RotatedBy(rotation);
            spriteBatch.Draw(upper, skullAt - screen, null, colour, rotation, origin, scale, SpriteEffects.None, 0f);

            if (cracked > 0)
                DrawCracks(spriteBatch, skullAt, rotation, scale, cracked, colour);

            if (red && hat)
            {
                Texture2D cap = HatTexture.Value;
                Vector2 capAt = centre + new Vector2(0f, -(lift + hatLift) * scale).RotatedBy(rotation);

                spriteBatch.Draw(cap, capAt - screen, null, colour, rotation, HeadMiddle - HatCorner, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>The red hat alone (flown off, or on the way to him), its middle where the skull's middle would be.</summary>
        internal static void DrawHat(SpriteBatch spriteBatch, Vector2 skullCentre, float rotation, float scale, Color colour)
        {
            if (!Ready)
                return;

            spriteBatch.Draw(HatTexture.Value, skullCentre - Main.screenPosition, null, colour, rotation, HeadMiddle - HatCorner, scale, SpriteEffects.None, 0f);
        }

        /// <summary>What is seen between the skull and the jaw when they part: his mouth, stretched to the gap.</summary>
        private static void DrawMouth(SpriteBatch spriteBatch, Vector2 centre, float rotation, float scale, float jaw, float lift, Color colour)
        {
            Texture2D mouth = MouthTexture.Value;

            float top = MouthTop - lift;
            float bottom = MouthBottom + jaw;
            Vector2 middle = new Vector2((MouthLeft + MouthRight) * 0.5f, (top + bottom) * 0.5f) - HeadMiddle;
            Vector2 size = new Vector2(MouthRight - MouthLeft, bottom - top);
            Vector2 stretch = new Vector2(size.X / mouth.Width, size.Y / mouth.Height) * scale;

            spriteBatch.Draw(mouth, centre + middle.RotatedBy(rotation) * scale - Main.screenPosition, null, colour, rotation,
                mouth.Size() * 0.5f, stretch, SpriteEffects.None, 0f);
        }

        /// <summary>The cracks in the skull, to so many (each counts two lines: a crack and its branch).</summary>
        private static void DrawCracks(SpriteBatch spriteBatch, Vector2 skullAt, float rotation, float scale, int cracked, Color colour)
        {
            Color dark = Multiply(new Color(28, 26, 16), colour);
            dark.A = 255;
            int lines = Math.Min(Cracks.Length, cracked * 2);

            for (int i = 0; i < lines; i++)
            {
                Vector2[] crack = Cracks[i];

                for (int k = 0; k < crack.Length - 1; k++)
                {
                    Vector2 a = skullAt + (crack[k] - HeadMiddle).RotatedBy(rotation) * scale;
                    Vector2 b = skullAt + (crack[k + 1] - HeadMiddle).RotatedBy(rotation) * scale;

                    Line(spriteBatch, a, b, dark, 2f * scale);
                }
            }
        }

        /// <summary>Where his mouth is open, in the world: between the skull's teeth and the jaw's, as far apart as they are.</summary>
        internal static Vector2 Mouth(Vector2 centre, float rotation, float scale, float jaw, float lift)
        {
            float y = (MouthTop - lift + MouthBottom + jaw) * 0.5f;
            return centre + new Vector2(0f, y - HeadMiddle.Y).RotatedBy(rotation) * scale;
        }

        /// <summary>One of his eyes in the world, the skull lifted by <paramref name="lift"/>.</summary>
        internal static Vector2 Eye(bool left, Vector2 centre, float rotation, float scale, float lift)
        {
            Vector2 eye = (left ? LeftEye : RightEye) - HeadMiddle + new Vector2(0f, -lift);
            return centre + eye.RotatedBy(rotation) * scale;
        }

        /// <summary>Where a shoulder of his is (a hand's arm starts there): under him and out to that side, not turned with him.</summary>
        internal static Vector2 Shoulder(Vector2 centre, float side, float scale) => centre + new Vector2(side * 30f, 44f) * scale;

        /// <summary>The skull and jaw's rim in a colour of light (his ward while his hands live, a glow).</summary>
        internal static void DrawHeadRim(SpriteBatch spriteBatch, bool red, Vector2 centre, float rotation, float scale, float jaw, float lift, Color light, float reach,
            bool withJaw = true, float hatLift = 0f)
        {
            Color glow = light;
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * reach;
                DrawHead(spriteBatch, red, centre + offset, rotation, scale, jaw, lift, glow, withJaw, hatLift);
            }
        }

        // ============================================================================================================
        // LIGHT
        // ============================================================================================================

        /// <summary>A soft glow, added to what is under it.</summary>
        internal static void Glow(SpriteBatch spriteBatch, Vector2 world, Color colour, float radius)
        {
            if (!Ready || radius <= 0f)
                return;

            Texture2D glow = GlowTexture.Value;
            Color light = colour;
            light.A = 0;

            spriteBatch.Draw(glow, world - Main.screenPosition, null, light, 0f, glow.Size() * 0.5f, radius * 2f / glow.Width, SpriteEffects.None, 0f);
        }

        /// <summary>A small hot glow (a coal, an eye lit): bright at its heart, added.</summary>
        internal static void Hot(SpriteBatch spriteBatch, Vector2 world, Color colour, float radius)
        {
            if (!Ready || radius <= 0f)
                return;

            Texture2D hot = HotTexture.Value;
            Color light = colour;
            light.A = 0;

            spriteBatch.Draw(hot, world - Main.screenPosition, null, light, 0f, hot.Size() * 0.5f, radius * 2f / hot.Width, SpriteEffects.None, 0f);
        }

        /// <summary>A four-pointed glint, added.</summary>
        internal static void Glint(SpriteBatch spriteBatch, Vector2 world, Color colour, float size, float rotation = 0f)
        {
            if (!Ready || size <= 0f)
                return;

            Texture2D star = StarTexture.Value;
            Color light = colour;
            light.A = 0;

            spriteBatch.Draw(star, world - Main.screenPosition, null, light, rotation, star.Size() * 0.5f, size / star.Width, SpriteEffects.None, 0f);
        }

        /// <summary>A soft line of light from one point to another, <paramref name="width"/> across, added.</summary>
        internal static void Beam(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color colour, float width)
        {
            if (!Ready)
                return;

            Texture2D line = LineTexture.Value;
            Vector2 d = to - from;
            float length = d.Length();

            if (length < 1f)
                return;

            Color light = colour;
            light.A = 0;

            spriteBatch.Draw(line, from - Main.screenPosition, null, light, d.ToRotation(), new Vector2(0f, line.Height * 0.5f),
                new Vector2(length / line.Width, width / line.Height), SpriteEffects.None, 0f);
        }

        /// <summary>A hard line, laid over: a pixel stretched.</summary>
        internal static void Line(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color colour, float width)
        {
            Vector2 d = to - from;
            float length = d.Length();

            if (length < 0.5f)
                return;

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, from - Main.screenPosition, new Rectangle(0, 0, 1, 1), colour, d.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f);
        }

        /// <summary>A ring of hard line, in so many pieces.</summary>
        internal static void Ring(SpriteBatch spriteBatch, Vector2 centre, float radius, Color colour, float width, int pieces = 48, float start = 0f, float sweep = MathHelper.TwoPi)
        {
            if (radius <= 0.5f)
                return;

            Vector2 last = centre + start.ToRotationVector2() * radius;

            for (int i = 1; i <= pieces; i++)
            {
                Vector2 next = centre + (start + sweep * i / pieces).ToRotationVector2() * radius;
                Line(spriteBatch, last, next, colour, width);
                last = next;
            }
        }

        /// <summary>A ring (or an arc of one) of soft light, added.</summary>
        internal static void SoftRing(SpriteBatch spriteBatch, Vector2 centre, float radius, Color colour, float width, int pieces = 40, float start = 0f, float sweep = MathHelper.TwoPi)
        {
            if (radius <= 1f)
                return;

            Vector2 last = centre + start.ToRotationVector2() * radius;

            for (int i = 1; i <= pieces; i++)
            {
                Vector2 next = centre + (start + sweep * i / pieces).ToRotationVector2() * radius;
                Beam(spriteBatch, last, next, colour, width);
                last = next;
            }
        }

        /// <summary>Two colours multiplied, as light falls on a tint.</summary>
        internal static Color Multiply(Color a, Color b) =>
            new Color(a.R * b.R / 255, a.G * b.G / 255, a.B * b.B / 255, a.A * b.A / 255);

        /// <summary>A colour as light: no alpha, so it adds to what it is drawn over.</summary>
        internal static Color Additive(Color colour, float strength = 1f)
        {
            Color light = colour * strength;
            light.A = 0;
            return light;
        }
    }
}
