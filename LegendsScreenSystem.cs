using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// The orb breaking, seen: six and a half seconds in which the world stops to be looked at.
    ///
    /// The bars come in and the camera rises until the ground sits a quarter of the way up the
    /// frame; as it rises the world turns to a poster, black shapes on flat red
    /// (<see cref="LegendsPlate"/>); "God help us" is set into the red a letter at a time; then
    /// everything goes back the way it came. The player is held still and cannot be hurt
    /// while it plays.
    ///
    /// All of it is asked of Cinematic tick by tick: the bars, the camera, the hold, and the
    /// red and the line laid over the world as its cover. So nothing can be stranded: leaving
    /// the world mid-scene only stops the asking, and all of it eases back by itself. Local to
    /// the machine that broke the orb, as the stir always was.
    /// </summary>
    public class LegendsScreenSystem : ModSystem
    {
        /// <summary>The whole scene: six and a half seconds.</summary>
        public const int Length = 390;

        /// <summary>Ticks the red takes to come in, with the camera on its way up.</summary>
        private const int RedIn = 90;

        /// <summary>When the red starts to go, and the camera starts home: both are gone by the end.</summary>
        private const int RedOutAt = 300;

        /// <summary>The bars are asked for until here, and take their own half second to go.</summary>
        private const int FrameUntil = Length - Cinematic.Bars;

        /// <summary>Where the ground line sits, as a share of the height between the bars, from the top.</summary>
        private const float GroundAt = 0.75f;

        /// <summary>Where the middle of the line sits, the same way.</summary>
        private const float LineAt = 0.3f;

        /// <summary>How tall the line is, as a share of the screen's height.</summary>
        private const float LineShare = 0.115f;

        /// <summary>When the first letter starts to show, and how far apart the letters start. A space is a letter's wait.</summary>
        private const int TextAt = 80;
        private const int LetterEvery = 10;

        /// <summary>Ticks each letter takes to come fully in.</summary>
        private const int LetterFade = 26;

        /// <summary>When the line starts to go, and how long it takes: gone well before the red.</summary>
        private const int TextOutAt = RedOutAt;
        private const int TextOut = 30;

        // ---- the line --------------------------------------------------------------
        //
        // "God help us", set in the face of the mock it was asked for in: heavy, tight,
        // letters touching. Terraria has no such font, so it is a picture
        // (Content/ExtraTextures/GodHelpUs.png): DejaVu Sans Bold at 208 px, thickened by 4,
        // tracked in by 20, word gaps at 0.4 of a space. One cell per letter: the letters
        // themselves in the top row, and in the bottom row each letter grown by 6 px, the
        // knock-out drawn in the plate's red under the black so the line still reads where
        // it crosses something black. White, premultiplied. The cell table below is that
        // sheet's, in its pixels; Left and Top place each cell within the line.

        private static Asset<Texture2D> line;

        private readonly struct Glyph
        {
            /// <summary>Which letter of the line this is, spaces counted: when it shows.</summary>
            public readonly int Index;
            public readonly int X;
            public readonly int Width;
            public readonly int Height;
            public readonly int Left;
            public readonly int Top;

            public Glyph(int index, int x, int width, int height, int left, int top)
            {
                Index = index;
                X = x;
                Width = width;
                Height = height;
                Left = left;
                Top = top;
            }
        }

        private static readonly Glyph[] Glyphs =
        {
            new Glyph(0, 2, 166, 179, 0, 3),        // G
            new Glyph(1, 170, 146, 141, 149, 41),   // o
            new Glyph(2, 318, 143, 182, 273, 0),    // d
            new Glyph(4, 463, 135, 179, 439, 0),    // h
            new Glyph(5, 600, 143, 141, 558, 41),   // e
            new Glyph(6, 745, 57, 179, 688, 0),     // l
            new Glyph(7, 804, 143, 181, 739, 41),   // p
            new Glyph(9, 949, 135, 138, 896, 44),   // u
            new Glyph(10, 1086, 124, 141, 1018, 41), // s
        };

        /// <summary>The sheet's two rows: the letters, and their knock-outs.</summary>
        private const int FillRow = 2;
        private const int RingRow = 186;

        /// <summary>The whole line, knock-outs included, in the sheet's pixels.</summary>
        private const int LineWidth = 1142;
        private const int LineHeight = 222;

        private static readonly Action<SpriteBatch> drawCover = DrawCover;

        /// <summary>Ticks into the scene; -1 when none is playing.</summary>
        private static int age = -1;

        public override void Load()
        {
            if (!Main.dedServ)
                line = ModContent.Request<Texture2D>("PisAndDag/Content/ExtraTextures/GodHelpUs");
        }

        public override void Unload()
        {
            line = null;
            age = -1;
        }

        /// <summary>Plays the scene from the start, on this machine.</summary>
        public static void Trigger()
        {
            if (!Main.dedServ)
                age = 0;
        }

        /// <summary>Whether the scene is playing.</summary>
        public static bool Running => age >= 0;

        /// <summary>How far the world has turned to the poster, 0 to 1.</summary>
        public static float Amount
        {
            get
            {
                if (age < 0)
                    return 0f;

                if (age < RedIn)
                    return Smooth(age / (float)RedIn);

                if (age < RedOutAt)
                    return 1f;

                return 1f - Smooth((age - RedOutAt) / (float)(Length - RedOutAt));
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || age < 0)
                return;

            Player player = Main.LocalPlayer;

            if (age < FrameUntil)
                Cinematic.Frame();

            if (age < RedOutAt && player.active)
                Cinematic.Look(Focus(player));

            Cinematic.Hold();
            Cinematic.Cover(drawCover);

            if (++age >= Length)
                age = -1;
        }

        public override void OnWorldUnload()
        {
            age = -1;
        }

        /// <summary>
        /// Where the camera points: far enough above the player's feet that the ground line
        /// sits at <see cref="GroundAt"/> of the height between the bars, at any zoom. With
        /// gravity turned over, the other way.
        /// </summary>
        private static Vector2 Focus(Player player)
        {
            float zoom = Math.Max(1f, Main.GameViewMatrix.Zoom.Y);
            float between = (Main.screenHeight - Cinematic.FullBarHeight * 2f) / zoom;
            Vector2 feet = player.gravDir >= 0f ? player.Bottom : player.Top;

            return feet - new Vector2(0f, (GroundAt - 0.5f) * between * player.gravDir);
        }

        /// <summary>The red with the world's shapes in it, then the line. Raw screen pixels, under the bars.</summary>
        private static void DrawCover(SpriteBatch spriteBatch)
        {
            float red = Amount;
            Texture2D picture = LegendsPlate.Picture;

            if (picture != null && red > 0f)
                spriteBatch.Draw(picture, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White * red);

            DrawLine(spriteBatch);
        }

        private static void DrawLine(SpriteBatch spriteBatch)
        {
            if (age < TextAt || line == null || !line.IsLoaded)
                return;

            Texture2D sheet = line.Value;
            float bar = Cinematic.FullBarHeight;
            float between = Main.screenHeight - bar * 2f;
            float scale = Main.screenHeight * LineShare / LineHeight;

            Vector2 origin = new Vector2((Main.screenWidth - LineWidth * scale) * 0.5f,
                bar + between * LineAt - LineHeight * scale * 0.5f).Floor();

            // Every knock-out first, then every letter, so no letter's knock-out cuts into
            // the one beside it where they touch.
            for (int pass = 0; pass < 2; pass++)
            {
                bool ring = pass == 0;

                foreach (Glyph glyph in Glyphs)
                {
                    float shown = Letter(glyph.Index);

                    if (shown <= 0.004f)
                        continue;

                    Rectangle source = new Rectangle(glyph.X, ring ? RingRow : FillRow, glyph.Width, glyph.Height);
                    Vector2 at = origin + new Vector2(glyph.Left, glyph.Top) * scale;
                    Color colour = (ring ? LegendsPlate.Red : Color.Black) * shown;

                    spriteBatch.Draw(sheet, at, source, colour, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>How far one letter is in, 0 to 1: each in slowly after the one before, all out together.</summary>
        private static float Letter(int index)
        {
            float shown = Smooth((age - TextAt - index * LetterEvery) / (float)LetterFade);
            float left = age < TextOutAt ? 1f : 1f - Smooth((age - TextOutAt) / (float)TextOut);

            return shown * left;
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
