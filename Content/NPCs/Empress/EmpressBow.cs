using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// Her bow of light, in her second half in Legends (by request: "The hand on her left raise, and a bow forms out of pure
    /// light (the eventide but bigger), she uses it in her second phase"): Eventide's shape (Bow.png,
    /// Content/NPCs/Legends/Empress: the game's own picture of it enlarged two and a half times, each of its 2 by 2 blocks
    /// now 5 by 5, its string taken off, to be redrawn), drawn in the world at 0.6 of its picture's size (by request:
    /// "this size of bow is perfect", "0.6x", "keep the 5x5 block but change it ingame"): one and a half times Eventide's
    /// own, a little over half her height. Held at its grip in her hand and turned the way she aims; drawn as light: her
    /// colours about it, white through it, glints running along its limbs; its string a thread of her rainbow from tip to
    /// tip, drawn back by her other hand as she draws, her arrows nocked on it (Arrow.png: Daybreak's shape, in her light).
    ///
    /// Places on it are in its own picture's pixels (90 by 195: the bow 70 by 175, pointing right, its string on the left,
    /// in a clear margin of 10 all round, so its edges are never cut by the picture's edge as it is drawn turned), turned
    /// into the world from where her hand holds it and scaled by Size. Aimed anywhere to her left it is turned over as well
    /// (its top kept up). A new picture of another shape wants these places moved to match it; another size in the fight
    /// is Size alone.
    /// </summary>
    public static class EmpressBow
    {
        /// <summary>Its size in the world, times its picture's (a pixel of the picture is this much of a pixel of the world).</summary>
        public const float Size = 0.6f;

        /// <summary>Its picture's height (the bow's 175 and its margin above and below).</summary>
        private const float PictureHeight = 195f;

        /// <summary>Where on it her hand holds it (the middle of its riser), and where its string is tied at its two tips (the blue gems under its ears).</summary>
        public static readonly Vector2 Grip = new Vector2(62.5f, 97.5f);
        public static readonly Vector2 TopTip = new Vector2(22.5f, 48.75f);
        public static readonly Vector2 BottomTip = new Vector2(22.5f, 146.25f);

        /// <summary>
        /// Its string's middle at rest, where an arrow is nocked; and how far back (its picture's pixels) it can be drawn:
        /// as far as leaves a nocked arrow's tip just clear of the bow (an arrow is 93 from nock to tip, whatever the bow's
        /// size); her drawing hand, a little further back at her chest, holds it by a thread of light.
        /// </summary>
        public static readonly Vector2 StringMiddle = new Vector2(22.5f, 97.5f);
        public const float Pull = 98f;

        /// <summary>From the grip back to the string at rest, and to the string drawn as far as it goes, in the world (along the aim).</summary>
        public const float RestBack = (62.5f - 22.5f) * Size;
        public const float MostBack = RestBack + Pull * Size;

        /// <summary>
        /// An arrow's own picture (112 by 42, pointing right: the arrow 100 by 30 in a clear margin of 6): its nock (where the
        /// string holds it), the middle of its sun of a head, its tip.
        /// </summary>
        public static readonly Vector2 ArrowNock = new Vector2(12f, 21f);
        public static readonly Vector2 ArrowStar = new Vector2(78f, 21f);
        public const float ArrowTip = 105f;

        /// <summary>Turned over (aimed to her left), so its top stays up.</summary>
        private static bool Over(float aim) => Math.Cos(aim) < 0.0;

        /// <summary>A place on the bow, in the world, held at <paramref name="hand"/> aiming <paramref name="aim"/>.</summary>
        public static Vector2 On(Vector2 hand, float aim, Vector2 onPicture)
        {
            if (Over(aim))
                onPicture.Y = PictureHeight - onPicture.Y;

            return hand + ((onPicture - Grip) * Size).RotatedBy(aim);
        }

        /// <summary>The string's middle, drawn back <paramref name="back"/> world pixels from the grip along the aim.</summary>
        public static Vector2 StringAt(Vector2 hand, float aim, float back) => hand - aim.ToRotationVector2() * back;

        /// <summary>
        /// The bow. <paramref name="form"/>: how far it has formed out of light (0 nothing, 1 whole); <paramref name="nock"/>:
        /// where its string's middle is (at rest, or drawn back to her other hand); <paramref name="arrows"/> nocked on it,
        /// fanned <paramref name="spread"/> radians from first to last; <paramref name="twang"/>: the string still shaking
        /// after a shot (1 just loosed, going to 0); <paramref name="arrowLook"/>: the arrows' colour of hers
        /// (EmpressArt.Prism), or below 0 the mirror's glass; <paramref name="hold"/>: her drawing hand, if it is not on the
        /// string (a thread of light runs from it to the string). Expects the NPCs' batch open (or any alpha-blended batch
        /// with the game's transform), and leaves it so.
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch, Vector2 hand, float aim, float form, Vector2 nock, int arrows = 0, float spread = 0f,
            float twang = 0f, float opacity = 1f, float arrowLook = 0f, float drawn = 0f, Vector2? hold = null)
        {
            if (form <= 0.003f || opacity <= 0.003f || !EmpressArt.Ready(EmpressTextures.Bow))
                return;

            Texture2D bow = EmpressTextures.Bow.Value;
            Texture2D white = EmpressArt.WhiteOf(bow);
            Vector2 at = hand - Main.screenPosition;
            float time = (float)Main.timeForVisualEffects;
            float solid = MathHelper.Clamp((form - 0.35f) / 0.65f, 0f, 1f);
            SpriteEffects effects = Over(aim) ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // Light about it, in her colours, turning.
            EmpressArt.DrawBloom(spriteBatch, hand, 230f * Size, EmpressArt.Additive(EmpressArt.Prism(time / 240f), 0.13f * form * opacity));

            if (white != null)
            {
                for (int k = 0; k < 4; k++)
                {
                    Vector2 off = (k * MathHelper.PiOver2 + time * 0.03f).ToRotationVector2() * Math.Max(1.5f, 2.5f * Size);
                    spriteBatch.Draw(white, at + off, null, EmpressArt.Additive(EmpressArt.Prism(k / 4f + time / 200f), 0.16f * form * opacity), aim, Grip, Size, effects, 0f);
                }
            }

            // Itself: its own colours as it is made solid, white light through it.
            spriteBatch.Draw(bow, at, null, Color.White * (solid * opacity), aim, Grip, Size, effects, 0f);

            if (white != null)
            {
                float pulse = 0.1f + 0.06f * (float)Math.Sin(time * 0.07f);
                spriteBatch.Draw(white, at, null, EmpressArt.Additive(Color.White, (pulse + 0.6f * (1f - solid)) * form * opacity), aim, Grip, Size, effects, 0f);
            }

            // Glints running out along its limbs, tip and back.
            for (int g = 0; g < 2; g++)
            {
                float u = (time * 0.012f + g * 0.5f) % 1f;
                Vector2 tip = g == 0 ? TopTip : BottomTip;
                Vector2 onLimb = Vector2.Lerp(Grip + new Vector2(10f, 0f), tip + new Vector2(5f, 0f), u) + new Vector2(25f * (float)Math.Sin(u * MathHelper.Pi), 0f);
                EmpressArt.DrawStar(spriteBatch, On(hand, aim, onLimb), 26f * Math.Max(0.7f, Size), time * 0.05f + g, EmpressArt.Additive(Color.White, 0.8f * form * opacity));
            }

            // The string: a thread of her rainbow, tip to the nock to tip, shaking after a shot. It comes as the bow is
            // all but made.
            float thread = MathHelper.Clamp((form - 0.6f) / 0.4f, 0f, 1f) * opacity;
            Vector2 top = On(hand, aim, TopTip);
            Vector2 bottom = On(hand, aim, BottomTip);

            if (twang > 0.003f)
                nock += (aim + MathHelper.Pi).ToRotationVector2() * (float)Math.Sin(time * 2.1f) * 9f * twang;

            if (thread > 0.003f)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector2 off = (aim + MathHelper.PiOver2).ToRotationVector2() * (k - 1) * 1.6f;
                    Color colour = EmpressArt.Additive(EmpressArt.Prism(k / 3f + time / 160f), 0.6f * thread);
                    EmpressArt.DrawLine(spriteBatch, top + off, nock + off, 3f, colour);
                    EmpressArt.DrawLine(spriteBatch, nock + off, bottom + off, 3f, colour);
                }

                EmpressArt.DrawLine(spriteBatch, top, nock, 1.4f, EmpressArt.Additive(Color.White, 0.9f * thread));
                EmpressArt.DrawLine(spriteBatch, nock, bottom, 1.4f, EmpressArt.Additive(Color.White, 0.9f * thread));

                // Her drawing hand, where it is not on the string: a thread of light from her fingers to it, brighter the
                // further it is drawn.
                if (hold.HasValue && drawn > 0.02f && Vector2.DistanceSquared(hold.Value, nock) > 25f)
                {
                    EmpressArt.DrawBeam(spriteBatch, hold.Value, nock, 6f, EmpressArt.Additive(EmpressArt.Prism(time / 90f), 0.35f * drawn * thread));
                    EmpressArt.DrawBeam(spriteBatch, hold.Value, nock, 1.6f, EmpressArt.Additive(Color.White, 0.8f * drawn * thread));
                }

                if (drawn > 0.02f)
                {
                    EmpressArt.DrawBloom(spriteBatch, nock, 40f + 50f * drawn, EmpressArt.Additive(EmpressArt.Pink, 0.35f * drawn * thread));
                    EmpressArt.DrawStar(spriteBatch, nock, 30f + 40f * drawn, time * 0.08f, EmpressArt.Additive(Color.White, 0.6f * drawn * thread));
                }
            }

            // Arrows on it, fanned about the way she aims, glowing more the further it is drawn. Their heads lie close
            // together, their lights one over another: each the less for how many there are, so they stay arrows, not
            // one white blaze.
            float together = arrows > 1 ? 1f / (float)Math.Sqrt(arrows) : 1f;

            for (int i = 0; i < arrows; i++)
            {
                float a = aim + (arrows > 1 ? (i / (float)(arrows - 1) - 0.5f) * spread : 0f);
                DrawArrow(spriteBatch, nock, a, 1f, (0.55f + 0.45f * drawn) * opacity * thread, arrowLook < 0f ? -1f : arrowLook + i * 0.13f, 0f, null, together);
            }
        }

        /// <summary>
        /// One of her arrows of light, its nock at <paramref name="nock"/> (world), pointing <paramref name="angle"/>: her colour
        /// about it (<paramref name="hue"/>, a share round her colours; below 0 the mirror's glass: white edged in her
        /// rainbow), white at its heart, images of it trailing behind it by <paramref name="trail"/> (0 none: at rest).
        /// <paramref name="tint"/>, if given, is its colour instead of hers (a refracted arrow's own of the seven).
        /// <paramref name="headGlow"/>: how much of its head's light (less where several lie together).
        /// </summary>
        public static void DrawArrow(SpriteBatch spriteBatch, Vector2 nock, float angle, float size, float strength, float hue, float trail, Color? tint = null, float headGlow = 1f)
        {
            if (strength <= 0.003f || !EmpressArt.Ready(EmpressTextures.Arrow))
                return;

            Texture2D arrow = EmpressTextures.Arrow.Value;
            Vector2 at = nock - Main.screenPosition;
            float time = (float)Main.timeForVisualEffects;
            bool mirror = hue < 0f && !tint.HasValue;
            Color colour = tint ?? (mirror ? Color.Lerp(Color.White, EmpressArt.Sky, 0.35f) : EmpressArt.Prism(hue));
            Vector2 back = (angle + MathHelper.Pi).ToRotationVector2();
            Vector2 head = nock + angle.ToRotationVector2() * (ArrowStar.X - ArrowNock.X) * size;

            // Images of it behind it as it flies, as her lances leave.
            if (trail > 0.003f)
            {
                for (float k = 1f; k > 0f; k -= 1f / 6f)
                {
                    Vector2 off = back * 120f * k * trail;
                    spriteBatch.Draw(arrow, at + off, null, EmpressArt.Additive(mirror ? EmpressArt.Prism(k + time / 90f) : colour, 0.5f * strength * (1f - k)), angle, ArrowNock, size, SpriteEffects.None, 0f);
                }
            }

            // Her colour about it, a glow just outside its edges; the mirror's glass edged in all of them.
            for (int k = 0; k < 4; k++)
            {
                Vector2 off = (k * MathHelper.PiOver2 + angle).ToRotationVector2() * 2f * size;
                Color edge = mirror ? EmpressArt.Prism(k / 4f + time / 120f) : colour;
                spriteBatch.Draw(arrow, at + off, null, EmpressArt.Additive(edge, 0.22f * strength), angle, ArrowNock, size, SpriteEffects.None, 0f);
            }

            // Itself, in her colour: its picture's own light (white along its middle, softer to its edges), tinted. Then
            // white-hot along its middle: itself again, thinned across (its picture's middle is the arrow's line).
            spriteBatch.Draw(arrow, at, null, Color.Lerp(colour, Color.White, 0.3f) * (0.92f * strength), angle, ArrowNock, size, SpriteEffects.None, 0f);
            spriteBatch.Draw(arrow, at, null, EmpressArt.Additive(Color.White, 0.35f * strength), angle, ArrowNock, new Vector2(size, size * 0.45f), SpriteEffects.None, 0f);

            // Its sun of a head, burning.
            EmpressArt.DrawBloom(spriteBatch, head, 40f * size, EmpressArt.Additive(colour, 0.3f * strength * headGlow));
            EmpressArt.DrawStar(spriteBatch, head, 34f * size, time * 0.06f, EmpressArt.Additive(Color.White, 0.45f * strength * headGlow));
        }
    }
}
