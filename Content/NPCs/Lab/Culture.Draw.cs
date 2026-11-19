using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Lab
{
    public partial class Culture
    {
        // ---- how it looks ----------------------------------------------------------------------------------------------
        //
        // All of it on the two-pixel grid of the world, as every picture in the mod is: its strands are squares, six pixels
        // a side at their roots, four along their middles, two out to their tips (lit on top, dark under), one every two
        // pixels along a smooth line through the tentacle's points, swayed (Tentacle.Sway); the knot is lobes of red (CultureLobe), the outer ones faint, the inner ones dense; short
        // strands curl out of it all the time; an eye or two (CultureEye1 to 3) looks at what it hunts and blinks; and its
        // mouth (CultureMouth) shows only as it comes at something. Lit as the place is lit, a little of its red kept in the
        // dark; its eyes catch what light there is. The bestiary has its portrait (Culture.png) instead.

        private const string Art = "PisAndDag/Content/NPCs/Lab/";

        private static Asset<Texture2D> lobeArt, mouthArt;
        private static Asset<Texture2D>[] eyeArt;

        /// <summary>The lobes on CultureLobe, largest first; each eye's frame (open, half, shut, iris, one under another); the mouth's (shut, half, open).</summary>
        private static readonly Rectangle[] LobeFrames = { new(0, 0, 20, 20), new(22, 0, 16, 16), new(40, 0, 14, 14), new(56, 0, 12, 12), new(70, 0, 10, 10) };
        private static readonly Point[] EyeFrames = { new(10, 10), new(12, 8), new(14, 14) };
        private const int MouthWide = 18, MouthHigh = 16;

        /// <summary>Its reds: a strand's, a root's lit top and dark underside, its deepest (a strand's tip).</summary>
        private static readonly Color Bright = new(186, 38, 40), Pale = new(226, 84, 76), Dark = new(110, 14, 24), Deep = new(70, 8, 18);

        /// <summary>How faint the outer lobes are drawn.</summary>
        private const float HazeAlpha = 0.45f;

        private static void LoadArt()
        {
            if (Main.dedServ)
                return;

            lobeArt = ModContent.Request<Texture2D>(Art + "CultureLobe");
            mouthArt = ModContent.Request<Texture2D>(Art + "CultureMouth");
            eyeArt = new[]
            {
                ModContent.Request<Texture2D>(Art + "CultureEye1"),
                ModContent.Request<Texture2D>(Art + "CultureEye2"),
                ModContent.Request<Texture2D>(Art + "CultureEye3"),
            };
        }

        public override void ModifyHoverBoundingBox(ref Rectangle boundingBox)
        {
            boundingBox = NPC.Hitbox;
            boundingBox.Inflate(8, 8);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (NPC.IsABestiaryIconDummy)
            {
                Texture2D portrait = TextureAssets.Npc[Type].Value;
                spriteBatch.Draw(portrait, NPC.Center - screenPos, null, drawColor, 0f, portrait.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);
                return false;
            }

            if (arms == null || lobeArt == null || !lobeArt.IsLoaded || !mouthArt.IsLoaded)
                return false;

            // Not on the screen at all, tentacles and all: nothing to draw.
            Rectangle all = NPC.Hitbox;

            foreach (Tentacle t in arms)
                all = Rectangle.Union(all, t.Bounds);

            all.Inflate(48, 48);

            if (!all.Intersects(new Rectangle((int)screenPos.X, (int)screenPos.Y, Main.screenWidth, Main.screenHeight)))
                return false;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Point screen = new((int)screenPos.X, (int)screenPos.Y);
            float hurt = flash / 5f * 0.6f;

            foreach (Tentacle t in arms)
                DrawArm(spriteBatch, pixel, t, screen, hurt);

            Vector2 middle = NPC.Center + jolt;
            float rx = NPC.width * 0.375f, ry = NPC.height * 0.32f;
            int smaller = Math.Clamp((Size - Math.Min(NPC.width, NPC.height)) / 8, 0, 2);
            float reach = Math.Min(1f, Math.Min(NPC.width, NPC.height) / (float)Size + 0.25f);
            Color lit = Lit(middle, hurt);
            Texture2D lobe = lobeArt.Value;

            // The haze about it, then the knot.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Lobe l in lobes)
                {
                    if (l.Haze != (pass == 0))
                        continue;

                    float turn = l.Angle + MathF.Sin(time * 0.9f + l.Phase) * 0.35f;
                    float out_ = l.Out * (0.85f + 0.15f * MathF.Sin(time * 1.7f + l.Phase * 2f));
                    Vector2 at = middle + new Vector2(MathF.Cos(turn) * rx, MathF.Sin(turn) * ry) * out_;
                    Rectangle frame = LobeFrames[Math.Min(4, l.Kind + smaller)];
                    Color colour = l.Haze ? lit * HazeAlpha : lit;

                    spriteBatch.Draw(lobe, Snap(at, frame.Width, frame.Height, screen), frame, colour);
                }
            }

            // Its curls.
            foreach (Curl c in curls)
            {
                float turn = c.Angle + MathF.Sin(time * 0.8f + c.Phase) * 0.3f;
                Vector2 at = middle + new Vector2(MathF.Cos(turn) * rx, MathF.Sin(turn) * ry) * 0.6f;
                float way_ = turn + MathF.Sin(time * 1.1f + c.Phase) * 0.8f;
                int steps = (int)(c.Length * reach * (0.8f + 0.2f * MathF.Sin(time * 2f + c.Phase)) / 2f);

                for (int k = 0; k < steps; k++)
                {
                    way_ += c.Sense * (0.22f + 0.2f * MathF.Sin(c.Phase + k * 0.25f + time * 3f));
                    at += new Vector2(MathF.Cos(way_), MathF.Sin(way_)) * 2f;

                    if (Tentacle.InBlock(at))
                        break;

                    Color colour = k == steps - 1 ? Deep : k % 7 == 3 ? Dark : Bright;
                    Square(spriteBatch, pixel, at, 2, colour.MultiplyRGB(lit), screen);
                }
            }

            // Its mouth, as it comes at something.
            int mouth = act switch
            {
                Act.Draw => actTime < Draws / 2 ? 1 : 2,
                Act.Leap => 2,
                Act.Gather => actTime < 8 ? 1 : -1,
                _ => -1,
            };

            Vector2 toward = act == Act.Leap ? NPC.velocity.SafeNormalize(Vector2.Zero) : way;

            if (mouth >= 0)
            {
                Vector2 at = middle + new Vector2(toward.X * rx * 0.4f, toward.Y * ry * 0.4f + 2f);
                spriteBatch.Draw(mouthArt.Value, Snap(at, MouthWide, MouthHigh, screen), new Rectangle(0, mouth * MouthHigh, MouthWide, MouthHigh), lit);
            }

            // Its eyes, on what it hunts.
            Player prey = Main.player[NPC.target];
            Vector2 look = (prey.Center - middle).SafeNormalize(Vector2.Zero);
            Vector2 iris = new Vector2(MathF.Round(look.X), MathF.Round(look.Y)) * 2f;
            Color gleam = new Color(Math.Max(lit.R, (byte)150), Math.Max(lit.G, (byte)150), Math.Max(lit.B, (byte)150));

            foreach (Eye e in eyes)
            {
                if (!eyeArt[e.Kind].IsLoaded)
                    continue;

                Point size = EyeFrames[e.Kind];
                Vector2 at = middle + new Vector2(MathF.Cos(e.Angle) * rx, MathF.Sin(e.Angle) * ry) * e.Out + new Vector2(0f, MathF.Sin(time * 2f + e.Angle));
                int frame = e.Shut <= 0 ? 0 : e.Shut > 3 && e.Shut < 8 ? 2 : 1;
                Vector2 corner = Snap(at, size.X, size.Y, screen);

                spriteBatch.Draw(eyeArt[e.Kind].Value, corner, new Rectangle(0, frame * size.Y, size.X, size.Y), gleam);

                if (frame == 0)
                    spriteBatch.Draw(eyeArt[e.Kind].Value, corner + iris, new Rectangle(0, 3 * size.Y, size.X, size.Y), gleam);
            }

            return false;
        }

        /// <summary>A tentacle: a square every two pixels along a smooth line through its points, swayed; six, four, then two pixels across.</summary>
        private static void DrawArm(SpriteBatch spriteBatch, Texture2D pixel, Tentacle t, Point screen, float hurt)
        {
            int sample = 0;
            int last = Tentacle.Points - 1;

            for (int i = 0; i < last; i++)
            {
                Vector2 p0 = t.At[Math.Max(0, i - 1)], p1 = t.At[i], p2 = t.At[i + 1], p3 = t.At[Math.Min(last, i + 2)];
                float length = Vector2.Distance(p1, p2);
                int steps = Math.Max(1, (int)(length / 2f));
                Color lit = Lit(p1, hurt);

                for (int s = 0; s < steps; s++, sample++)
                {
                    float f = (i + s / (float)steps) / last;
                    Vector2 at = Vector2.CatmullRom(p0, p1, p2, p3, s / (float)steps) + t.Sway(f);
                    int wide = t.ThickAlong(f);

                    // Never over a block: what of it is in one is behind it.
                    if (Tentacle.InBlock(at))
                        continue;

                    if (wide >= 6)
                    {
                        Vector2 corner = Snap(at, 6, 6, screen);
                        spriteBatch.Draw(pixel, new Rectangle((int)corner.X, (int)corner.Y, 6, 2), Pale.MultiplyRGB(lit));
                        spriteBatch.Draw(pixel, new Rectangle((int)corner.X, (int)corner.Y + 2, 6, 2), Bright.MultiplyRGB(lit));
                        spriteBatch.Draw(pixel, new Rectangle((int)corner.X, (int)corner.Y + 4, 6, 2), Dark.MultiplyRGB(lit));
                    }
                    else if (wide == 4)
                    {
                        Vector2 corner = Snap(at, 4, 4, screen);
                        spriteBatch.Draw(pixel, new Rectangle((int)corner.X, (int)corner.Y, 4, 2), (sample % 7 == 3 ? Dark : Bright).MultiplyRGB(lit));
                        spriteBatch.Draw(pixel, new Rectangle((int)corner.X, (int)corner.Y + 2, 4, 2), Dark.MultiplyRGB(lit));
                    }
                    else
                    {
                        Square(spriteBatch, pixel, at, 2, (sample % 7 == 3 ? Dark : Bright).MultiplyRGB(lit), screen);
                    }
                }
            }

            Vector2 tip = t.Shown(last);

            if (!Tentacle.InBlock(tip))
                Square(spriteBatch, pixel, tip, 2, Deep.MultiplyRGB(Lit(tip, hurt)), screen);
        }

        /// <summary>A square of this size about this place in the world, on the world's two-pixel grid.</summary>
        private static void Square(SpriteBatch spriteBatch, Texture2D pixel, Vector2 at, int size, Color colour, Point screen)
        {
            Vector2 corner = Snap(at, size, size, screen);
            spriteBatch.Draw(pixel, new Rectangle((int)corner.X, (int)corner.Y, size, size), colour);
        }

        /// <summary>Where on the screen to draw something this big about this place in the world, its corner on the world's two-pixel grid.</summary>
        private static Vector2 Snap(Vector2 at, int wide, int high, Point screen)
        {
            int x = (int)MathF.Floor((at.X - wide * 0.5f) / 2f) * 2;
            int y = (int)MathF.Floor((at.Y - high * 0.5f) / 2f) * 2;
            return new Vector2(x - screen.X, y - screen.Y);
        }

        /// <summary>The light here; a little of its red kept in the dark; its hurt colour over it a moment when hit.</summary>
        private static Color Lit(Vector2 at, float hurt)
        {
            Color light = Lighting.GetColor(at.ToTileCoordinates());
            light = new Color(Math.Max(light.R, (byte)40), Math.Max(light.G, (byte)24), Math.Max(light.B, (byte)24));

            if (hurt > 0f)
                light = Color.Lerp(light, new Color(255, 214, 214), hurt);

            return light;
        }
    }
}
