using Microsoft.Xna.Framework;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Betsy's head for Betsy's Flames, as the reference has it: the game's own picture of her head
    /// (Gore_1081, GoreID.DD2BetsyHead, what falls when she dies) with her eyes opened and the neck
    /// taken off. The game's pixels are drawn from the game's own picture at run time; only the
    /// reference's own pixels are in the mod's folder (BetsysFlamesHead.png).
    ///
    /// Everything here is in that picture's pixels and faces the way it does, right: 98 by 108, two of
    /// them to every pixel of the art. A list of strips is {row, from, to} in threes, each strip two
    /// rows tall. Made by a script from the picture and the reference, which this rebuilds pixel for
    /// pixel; not by hand.
    /// </summary>
    public static class BetsysFlamesHeadCut
    {
        public const int Width = 98;
        public const int Height = 108;

        /// <summary>The bottom of the head behind the jaw: set on the caster's neck.</summary>
        public static readonly Vector2 Pivot = new Vector2(32f, 84f);

        /// <summary>The mouth's corner, where the jaw turns.</summary>
        public static readonly Vector2 Hinge = new Vector2(46f, 80f);

        /// <summary>The front of the upper jaw, on the line the lips close on.</summary>
        public static readonly Vector2 Tip = new Vector2(86f, 80f);

        /// <summary>Where the inside of the mouth (BetsysFlamesMaw.png) sits: its top left, its top row on the lips' line.</summary>
        public static readonly Vector2 MawAt = new Vector2(46f, 80f);

        /// <summary>The game's pixels the reference keeps, all but the jaw.</summary>
        public static readonly short[] UpperKept =
        {
            2, 6, 10, 4, 6, 14, 6, 6, 22, 8, 6, 26, 10, 8, 30, 10, 42, 50, 12, 10, 32, 12, 44, 54, 14,
            14, 34, 14, 46, 56, 14, 60, 66, 16, 16, 44, 16, 48, 58, 16, 62, 68, 18, 22, 46, 18, 50, 58,
            18, 62, 70, 20, 22, 48, 20, 50, 60, 20, 64, 72, 22, 18, 62, 22, 64, 72, 24, 18, 74, 26, 20,
            74, 28, 24, 74, 30, 26, 74, 32, 26, 74, 34, 26, 76, 36, 28, 76, 38, 28, 78, 40, 26, 78, 42,
            24, 82, 44, 24, 86, 46, 26, 88, 48, 28, 90, 50, 28, 92, 52, 26, 82, 52, 84, 94, 54, 24, 52,
            54, 54, 80, 54, 86, 94, 56, 22, 52, 56, 58, 76, 56, 78, 80, 56, 88, 94, 58, 22, 52, 58, 62,
            76, 58, 88, 94, 60, 22, 54, 60, 60, 78, 60, 80, 82, 60, 90, 94, 62, 14, 54, 62, 56, 58, 62,
            60, 78, 62, 80, 86, 62, 92, 94, 64, 14, 54, 64, 56, 58, 64, 60, 88, 66, 14, 54, 66, 56, 58,
            66, 60, 90, 68, 14, 52, 68, 54, 58, 68, 60, 70, 68, 74, 92, 70, 14, 60, 70, 62, 72, 70, 74,
            94, 72, 16, 60, 72, 62, 72, 72, 74, 94, 74, 16, 62, 74, 64, 72, 74, 74, 96, 76, 16, 62, 76,
            64, 96, 78, 16, 96, 80, 18, 38, 80, 86, 98, 82, 18, 38, 82, 86, 98, 84, 20, 40, 84, 88, 98,
            86, 20, 40, 86, 90, 98, 88, 22, 42, 88, 90, 98, 90, 22, 38, 90, 40, 44, 90, 90, 98, 92, 26,
            38, 92, 42, 46, 92, 90, 98, 94, 28, 36, 94, 38, 40, 94, 46, 48, 94, 92, 98, 96, 32, 38, 96,
            92, 98, 98, 36, 42, 98, 92, 96
        };

        /// <summary>The game's pixels the reference keeps in the jaw, which turns about the Hinge.</summary>
        public static readonly short[] JawKept =
        {
            80, 38, 86, 82, 38, 86, 84, 40, 86, 86, 40, 84, 88, 48, 82, 90, 54, 82, 92, 58, 82, 94, 62,
            82, 96, 66, 80, 98, 68, 80, 100, 70, 86, 102, 70, 86, 104, 72, 84, 106, 74, 82
        };

        /// <summary>The game's pixels under the reference's own: her dead face, shut eyes and all.</summary>
        public static readonly short[] DeadFace =
        {
            54, 52, 54, 56, 52, 58, 56, 76, 78, 58, 52, 62, 58, 76, 80, 60, 54, 60, 60, 78, 80, 62, 54,
            56, 62, 58, 60, 62, 78, 80, 64, 54, 56, 64, 58, 60, 66, 54, 56, 66, 58, 60, 68, 52, 54, 68,
            58, 60, 68, 70, 74, 70, 60, 62, 70, 72, 74, 72, 60, 62, 72, 72, 74, 74, 62, 64, 74, 72, 74,
            76, 62, 64, 90, 38, 40, 92, 38, 42, 94, 36, 38, 94, 40, 46, 96, 38, 48, 98, 42, 48, 100,
            42, 46
        };

        /// <summary>The reference's own pixels (BetsysFlamesHead.png): her eyes open, and what else it changed.</summary>
        public static readonly short[] Overlay =
        {
            54, 52, 54, 56, 52, 58, 56, 76, 78, 58, 52, 62, 58, 76, 80, 60, 54, 60, 60, 78, 80, 62, 54,
            56, 62, 58, 60, 62, 78, 80, 64, 54, 56, 64, 58, 60, 66, 54, 56, 66, 58, 60, 68, 52, 54, 68,
            58, 60, 68, 70, 74, 70, 60, 62, 70, 72, 74, 72, 60, 62, 72, 72, 74, 74, 62, 64, 74, 72, 74,
            76, 62, 64, 90, 38, 40, 92, 38, 42, 94, 36, 38, 94, 40, 46, 96, 38, 52, 98, 42, 54, 100,
            42, 56, 102, 48, 56
        };

        /// <summary>Those of the reference's own that are not over the game's: drawn with the dead face too.</summary>
        public static readonly short[] OverlayAdded =
        {
            96, 48, 52, 98, 48, 54, 100, 46, 56, 102, 48, 56
        };

        /// <summary>Those of the reference's own that glow: the eyes.</summary>
        public static readonly short[] Glow =
        {
            54, 52, 54, 56, 52, 58, 56, 76, 78, 58, 52, 62, 58, 76, 78, 60, 54, 60
        };
    }
}
