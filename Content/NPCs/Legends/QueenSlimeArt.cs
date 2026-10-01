using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// What the Queen Slime is drawn with that vanilla does not have, and the numbers that
    /// place vanilla's own pieces of her.
    ///
    /// Textures, in QueenSlime/ beside this file:
    ///   QueenSlimeWings.png          688 x 2304   her wings, remade: twelve 344 x 384 frames in
    ///                                             two columns of six. 0 folded, 1-4 unfolding,
    ///                                             5 spread wide (6-11, a straight flap, are no
    ///                                             longer drawn). The right wing, its root at
    ///                                             WingRoot; the left is it mirrored.
    ///   QueenSlimeWingbeat.png       1376 x 3120  one beat of her wing, bent as a big wing bends,
    ///                                             in 24 frames of 344 x 520, four across, read
    ///                                             along the rows: 0 the top, 3-7 the arm down
    ///                                             ahead of the hand, 8 the spread (Wings frame 5
    ///                                             exactly), 10 level, 14 the bottom, the tip
    ///                                             curled, 16-19 the wrist up and the hand
    ///                                             hanging, 20-23 the hand following. The right
    ///                                             wing, its root at BeatRoot.
    ///   QueenSlimeCrystalCracks.png  22 x 78      three stages of cracks over the Gelatin Crystal
    ///   QueenSlimeMorph.png          56 x 1024    sixteen 56 x 64 frames: the crystal's shape
    ///                                             going, like something liquid, into her core's
    ///   QueenSlimeMorphHalo.png      88 x 1536    the same, blurred, 16 px round each side
    ///   QueenSlimeWhite.png          360 x 1952   her whole sheet as white, for the light she
    ///                                             comes out of and dies in
    ///   QueenSlimeHalo.png           228 x 170    her standing frame, blurred, 24 px round
    /// All white ones are premultiplied white (a, a, a, a), so a colour with no alpha adds them
    /// as light under the ordinary alpha blend. Placeholders by the mod's rules
    /// (PlaceholderTextures.md); the scripts that made them are in the handoff.
    /// </summary>
    public static class QueenSlimeArt
    {
        private const string Folder = "PisAndDag/Content/NPCs/Legends/QueenSlime/";

        public static Asset<Texture2D> Wings;
        public static Asset<Texture2D> Wingbeat;
        public static Asset<Texture2D> Cracks;
        public static Asset<Texture2D> Morph;
        public static Asset<Texture2D> MorphHalo;
        public static Asset<Texture2D> White;
        public static Asset<Texture2D> Halo;

        /// <summary>A wing frame, and where its root (the shoulder) is in it.</summary>
        public const int WingWidth = 344;
        public const int WingHeight = 384;
        public static readonly Vector2 WingRoot = new Vector2(22f, 222f);

        /// <summary>Wing frames: folded, and the last of the unfolding (spread wide).</summary>
        public const int WingFolded = 0;
        public const int WingSpread = 5;

        /// <summary>A frame of the beat, where its root is in it, and how the sheet is laid out.</summary>
        public const int BeatWidth = 344;
        public const int BeatHeight = 520;
        public static readonly Vector2 BeatRoot = new Vector2(38f, 277f);
        public const int BeatFrames = 24;
        public const int BeatColumns = 4;

        /// <summary>The beat's frames: the top, and the spread (the same as WingSpread on the other sheet).</summary>
        public const int BeatTop = 0;
        public const int BeatSpread = 8;

        public const int MorphFrames = 16;
        public const int MorphWidth = 56;
        public const int MorphHeight = 64;
        public const int MorphPad = 16;

        public const int HaloPad = 24;

        /// <summary>Her frame on vanilla's sheet: 180 x 122, two columns of sixteen.</summary>
        public const int FrameWidth = 180;
        public const int FrameHeight = 122;

        /// <summary>
        /// How far up or down her core and her crown sit, by her frame, as vanilla draws them
        /// (Main.DrawNPCDirect_Inner, read off the 1.4.4 switch tables): the two differ only at
        /// frame 6.
        /// </summary>
        public static readonly float[] CoreRise = { 0, -10, 0, 10, 18, 10, -10, -14, -14, -16, -18, 20, 18, 18, 18, 18, 0, 0, 0, 0, -14, -18, -22, -18 };
        public static readonly float[] CrownRise = { 0, -10, 0, 10, 18, 10, 10, -14, -14, -16, -18, 20, 18, 18, 18, 18, 0, 0, 0, 0, -14, -18, -22, -18 };

        /// <summary>Her colours: the pink, the violet and the blue her gel shifts through.</summary>
        public static readonly Color Pink = new Color(255, 120, 230);
        public static readonly Color Violet = new Color(170, 110, 255);
        public static readonly Color Blue = new Color(90, 170, 255);

        /// <summary>Her gel, as globs: the same three, a shade lighter so a glob reads against her.</summary>
        public static readonly Color[] Gel = { new Color(255, 150, 235), new Color(190, 140, 255), new Color(120, 190, 255) };

        /// <summary>What flies off her crystal when it breaks: white, and her three.</summary>
        public static readonly Color[] Sparks = { Color.White, new Color(255, 180, 240), new Color(200, 160, 255), new Color(150, 210, 255) };

        /// <summary>Her core when she means to throw it, and thrown (Legends): the glow round it, and the heat on it.</summary>
        public static readonly Color Red = new Color(255, 40, 70);
        public static readonly Color RedHot = new Color(255, 120, 125);

        /// <summary>One of her three, going round slowly: <paramref name="t"/> in any units, a full turn every 3.</summary>
        public static Color Shade(float t)
        {
            float u = ((t % 3f) + 3f) % 3f;

            if (u < 1f)
                return Color.Lerp(Pink, Violet, u);

            if (u < 2f)
                return Color.Lerp(Violet, Blue, u - 1f);

            return Color.Lerp(Blue, Pink, u - 2f);
        }

        /// <summary>A light to add: the colour with no alpha, which the alpha blend adds.</summary>
        public static Color Additive(Color colour, float strength)
        {
            Color c = colour * strength;
            c.A = 0;
            return c;
        }

        /// <summary>
        /// Her core's red, round it and on it, wherever it is (in her, or thrown): its own shape
        /// blurred, twice, as tints of red that show on a bright sky as in the dark, a little red
        /// light, and the heat on the core. <paramref name="halo"/> is the core's blurred shape.
        /// </summary>
        public static void DrawRed(SpriteBatch spriteBatch, Texture2D halo, Rectangle haloFrame, Texture2D core, Vector2 at, float turn, Vector2 scale, float red)
        {
            Vector2 middle = haloFrame.Size() * 0.5f;

            spriteBatch.Draw(halo, at, haloFrame, Red * Math.Min(1f, 0.4f * red), turn, middle, scale * 1.9f, SpriteEffects.None, 0f);
            spriteBatch.Draw(halo, at, haloFrame, Red * Math.Min(1f, 0.75f * red), turn, middle, scale * 1.15f, SpriteEffects.None, 0f);
            spriteBatch.Draw(halo, at, haloFrame, Additive(RedHot, 0.3f * red), turn, middle, scale * 1.05f, SpriteEffects.None, 0f);
            spriteBatch.Draw(core, at, null, Additive(RedHot, 0.55f * red), turn, core.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        public static Rectangle WingFrame(int index)
        {
            index = (index % 12 + 12) % 12;
            return new Rectangle(index / 6 * WingWidth, index % 6 * WingHeight, WingWidth, WingHeight);
        }

        public static Rectangle BeatFrame(int index)
        {
            index = (index % BeatFrames + BeatFrames) % BeatFrames;
            return new Rectangle(index % BeatColumns * BeatWidth, index / BeatColumns * BeatHeight, BeatWidth, BeatHeight);
        }

        public static Rectangle MorphFrame(int index) =>
            new Rectangle(0, Math.Clamp(index, 0, MorphFrames - 1) * MorphHeight, MorphWidth, MorphHeight);

        public static Rectangle MorphHaloFrame(int index) =>
            new Rectangle(0, Math.Clamp(index, 0, MorphFrames - 1) * (MorphHeight + 2 * MorphPad), MorphWidth + 2 * MorphPad, MorphHeight + 2 * MorphPad);

        public static Rectangle CrackFrame(int stage) => new Rectangle(0, Math.Clamp(stage, 0, 2) * 26, 22, 26);

        /// <summary>Her frame, as vanilla takes it off the sheet, two pixels shaved off top and bottom.</summary>
        public static Rectangle BodyFrame(int index)
        {
            Rectangle frame = new Rectangle(index / 16 * FrameWidth, index % 16 * FrameHeight, FrameWidth, FrameHeight);
            frame.Inflate(0, -2);
            return frame;
        }

        internal static void Load()
        {
            if (Main.dedServ)
                return;

            Wings = ModContent.Request<Texture2D>(Folder + "QueenSlimeWings");
            Wingbeat = ModContent.Request<Texture2D>(Folder + "QueenSlimeWingbeat");
            Cracks = ModContent.Request<Texture2D>(Folder + "QueenSlimeCrystalCracks");
            Morph = ModContent.Request<Texture2D>(Folder + "QueenSlimeMorph");
            MorphHalo = ModContent.Request<Texture2D>(Folder + "QueenSlimeMorphHalo");
            White = ModContent.Request<Texture2D>(Folder + "QueenSlimeWhite");
            Halo = ModContent.Request<Texture2D>(Folder + "QueenSlimeHalo");
        }

        internal static void Unload()
        {
            Wings = Wingbeat = Cracks = Morph = MorphHalo = White = Halo = null;
        }
    }
}
