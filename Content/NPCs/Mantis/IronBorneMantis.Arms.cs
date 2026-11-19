using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mantis
{
    public partial class IronBorneMantis
    {
        // ---- its arms -------------------------------------------------------------------------------------------------
        //
        // A puppet's, by request ("anchor the arm to shoulder puppet style ... instead of just going rotated, make it
        // goes in 3D (squeeze it to fit perspective, make the rectangle a trapezoid ...) to make it look like its
        // reaching onto the player eyes").
        //
        // Each arm is four places in three dimensions, from the shoulder: its shoulder (always the middle), its two
        // elbows and its blade's point; across, down, and toward whoever is looking (z). They are kept as if it faced
        // right and turned over when it does not. Each tick the blade's point is put where the state wants it (folded,
        // raised a notch at a time, along the cut, left in, folded again) and the elbows are found from it (reaching
        // back from the point and forward from the shoulder, each piece kept its length: FABRIK), starting from where
        // they were, so they move as joints do.
        //
        // Drawn: each piece is a band of slices along its length (Strip), each slice put where its place in three
        // dimensions is seen from in front, the nearer the bigger (View), and as wide as the piece's picture is there
        // times that: a piece coming at the viewer is a trapezoid, its near end wide, and foreshortens as it turns
        // toward them. Nearer is brighter and further darker.
        //
        // By the second sketch ("its still js 2d rotatingg i meant make it Realllly dynamic": the middle piece a
        // trapezoid, the blade a crescent across the whole picture), the eye is close (Eye), so that depth changes a
        // piece's size up to four times over, and a blow is not a point led about but poses in depth, each piece
        // given its own way to point, turned from one pose to the next (Blend): pieces turn toward the viewer and
        // away, shortening and swelling as they do. A blow ends on the place it fixed on, back in the world's plane.
        //
        // The blade is flipped over its length as it needs to be (Limb.BladeSide): its edge leads a cut, and lies to
        // the femur when folded. A cut leaves smear frames (SmearFrames: the swept shape in the blade's steel, its
        // leading edge bright) and its last few poses, faint, after it.

        /// <summary>The pieces' lengths between their pivots, shoulder to point (their pictures' own).</summary>
        private static readonly float[] Lengths = { 52f, 76f, 94f };

        /// <summary>How far a cut reaches from the shoulder: thirteen tiles.</summary>
        public const float Reach = 13f * 16f;

        /// <summary>How far in front of the world's plane the eye is: the smaller, the stronger the perspective.</summary>
        private const float Eye = 190f;

        /// <summary>The body's picture: its side, the gap under its feet in it; its shoulder and its mouth in each picture, from its feet, facing right.</summary>
        private const int Side = 192;
        private const float FootGap = 4f;

        private static readonly Point[] Shoulders =
        {
            new(23, -140), new(23, -136), new(23, -138), new(23, -136), new(23, -136), new(23, -138), new(23, -136), new(27, -145), new(27, -121),
            new(34, -98), new(24, -127), new(16, -142), new(8, -149), new(1, -152),
        };

        private static readonly Point[] Mouths =
        {
            new(42, -142), new(42, -138), new(42, -140), new(42, -138), new(42, -138), new(42, -140), new(42, -138), new(46, -145), new(46, -121),
            new(48, -95), new(44, -129), new(34, -148), new(27, -159), new(22, -170),
        };

        /// <summary>Each piece's picture: where its pivots are along it, and across it.</summary>
        private static readonly (int In, int Out, int Across)[] Pivots = { (10, 62, 12), (12, 88, 16), (12, 106, 36) };

        private static Asset<Texture2D>[] armArt;

        private static void LoadArt()
        {
            if (Main.dedServ)
                return;

            armArt = new Asset<Texture2D>[3];

            for (int i = 0; i < 3; i++)
                armArt[i] = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Mantis/IronBorneMantisArm" + (i + 1));
        }

        private sealed class Limb
        {
            public readonly Vector3[] At = new Vector3[4];
            public Vector3 Point;
            public readonly Vector3[] Start = new Vector3[4];

            /// <summary>The blade's smear frames while it cuts (SmearFrames), and its last few poses, drawn after it faint.</summary>
            public readonly SmearFrames Smear = new() { Most = 22 };
            public readonly List<Vector3[]> Ghosts = new();

            /// <summary>
            /// Which way the blade's picture lies across it: 1 as drawn, -1 flipped over (by request: "you know you can
            /// flip the blaade right?"). Cutting, its edge leads, and stays so until the arm folds; folded, its edge is
            /// to the femur, as a mantis folds its blade. Where its tip was last tick, as it is seen.
            /// </summary>
            public float BladeSide = 1f;
            public bool SideKept;
            public Vector2 LastTip;
        }

        private Limb[] limbs;

        /// <summary>Three ways to point (as if it faced right: across, down, toward the viewer), made an arm.</summary>
        private static Vector3[] Dirs(Vector3 one, Vector3 two, Vector3 three)
        {
            Vector3[] at = new Vector3[4];
            at[1] = Toward(one) * Lengths[0];
            at[2] = at[1] + Toward(two) * Lengths[1];
            at[3] = at[2] + Toward(three) * Lengths[2];
            return at;
        }

        /// <summary>Between two arms: each piece pointed between the ways it points in the one and the other, its length kept.</summary>
        private static Vector3[] Blend(Vector3[] a, Vector3[] b, float u)
        {
            u = MathHelper.Clamp(u, 0f, 1f);
            Vector3[] at = new Vector3[4];

            for (int i = 0; i < 3; i++)
            {
                Vector3 from = Toward(a[i + 1] - a[i]);
                Vector3 way = Vector3.Lerp(from, Toward(b[i + 1] - b[i]), u);
                at[i + 1] = at[i] + (way.LengthSquared() < 0.0004f ? from : Vector3.Normalize(way)) * Lengths[i];
            }

            return at;
        }

        private static float Smooth(float u)
        {
            u = MathHelper.Clamp(u, 0f, 1f);
            return u * u * (3f - 2f * u);
        }

        /// <summary>
        /// An arm folded, as the second sketch has it idle: down from the shoulder, up and forward, and the blade hung
        /// down from that, turned a little away so that it is seen short. The far one a little further on, and behind.
        /// </summary>
        private static Vector3[] Folded(int k) => k == 0
            ? Dirs(new Vector3(0.3f, 0.93f, 0.2f), new Vector3(0.75f, -0.66f, 0.2f), new Vector3(0.14f, 0.9f, -0.4f))
            : Dirs(new Vector3(0.42f, 0.88f, -0.22f), new Vector3(0.78f, -0.62f, -0.2f), new Vector3(0.2f, 0.88f, -0.42f));

        /// <summary>The arm reaching a place: its point on it, its elbows up and toward the viewer (a pierce's the more, so the arm tapers away to its point).</summary>
        private static Vector3[] Reaching(Vector3 mark, bool pierce)
        {
            Vector3[] at = new Vector3[4];
            at[1] = Toward(mark + new Vector3(0f, -60f, pierce ? 150f : 40f)) * Lengths[0];
            at[2] = at[1] + Toward(mark - at[1] + new Vector3(0f, -30f, pierce ? 90f : 20f)) * Lengths[1];
            at[3] = mark;
            Solve(at, mark);
            return at;
        }

        /// <summary>
        /// A striking arm at <paramref name="t"/> of its blow's clock (as if it faced right, from the shoulder), and
        /// whether it is cutting then.
        ///
        ///   the slash    readied: swung back behind the shoulder, the blade hung behind it and away. The cut: the arm
        ///                comes down and straight out at the viewer, the middle piece a trapezoid, and the blade is
        ///                swept across in front of them, from pointing back, through pointing at their eyes, to
        ///                pointing on ahead, a crescent over the whole of the picture; then it drops back into the
        ///                world onto the place it fixed on, goes on a little past it and up, and is held.
        ///   the pierce   readied: coiled, its elbow out at the viewer, the blade pointing where it will go. The blow:
        ///                out straight onto the place it fixed on, the arm in a line, its near end big and its point
        ///                small. Held there, shaking.
        /// </summary>
        private Vector3[] StrikePose(Limb limb, int k, float t, out bool cutting)
        {
            Blow blow = Now;
            bool pierce = State == Act.Pierce;
            Vector2 to = fixedOn == Vector2.Zero ? new Vector2(150f, 60f) : new Vector2((fixedOn.X - Shoulder.X) * facing, fixedOn.Y - Shoulder.Y);
            Vector3 mark = new Vector3(to, 0f);
            cutting = false;

            Vector3[] ready = pierce
                ? Dirs(new Vector3(-0.3f, 0.35f, 0.9f), new Vector3(-0.7f, -0.15f, 0.35f), new Vector3(0.85f, 0.05f, -0.5f))
                : Dirs(new Vector3(-0.35f, 0.9f, 0.2f), new Vector3(-0.65f, -0.7f, -0.2f), new Vector3(-0.3f, 0.9f, -0.35f));

            if (t < blow.Winds)
            {
                Vector3[] readied = Blend(limb.Start, ready, Notched(t / blow.Winds, 3));

                // Fixed on: held there, shaking with it.
                if (t >= blow.Fixes)
                    readied[3] += new Vector3((float)Math.Sin(t * 2.9f) * 2f, (float)Math.Cos(t * 3.7f) * 2f, 0f);

                return readied;
            }

            Vector3[] end = Reaching(mark, pierce);
            Vector3[] past = pierce ? end : Reaching(mark + new Vector3(16f, -44f, 0f), false);

            if (t < blow.Winds + blow.Cuts)
            {
                float u = (t - blow.Winds) / blow.Cuts;
                cutting = true;

                if (pierce)
                    return Blend(ready, end, (float)Math.Pow(u, 1.4));

                // Down and out at the viewer; the blade from pointing back, across in front of them, to pointing on ahead.
                Vector3 down = new Vector3(0f, 0.5f, 0.85f), out_ = new Vector3(0.1f, 0.1f, 1f);
                Vector3[] back = Dirs(down, out_, new Vector3(-0.95f, 0.1f, 0.3f));
                Vector3[] ahead = Dirs(down, out_, new Vector3(0.95f, 0.1f, 0.25f));

                if (u < 0.3f)
                    return Blend(ready, back, Smooth(u / 0.3f));

                if (u < 0.72f)
                    return Blend(back, ahead, (u - 0.3f) / 0.42f);

                return Blend(ahead, end, Smooth((u - 0.72f) / 0.28f));
            }

            if (t < blow.Winds + blow.Cuts + blow.Holds)
            {
                float held = (t - blow.Winds - blow.Cuts) / blow.Holds;
                cutting = t < blow.Winds + blow.Cuts + 3;

                if (pierce)
                    return Reaching(mark + new Vector3((float)Math.Sin(t * 4.1f) * 2.5f * (1f - held), 0f, 0f), true);

                float on = Math.Min(1f, held * 3f);
                return Blend(end, past, 1f - (1f - on) * (1f - on));
            }

            return Blend(past, Folded(k), Notched((t - blow.Winds - blow.Cuts - blow.Holds) / blow.Folds, 3));
        }

        /// <summary>From 0 to 1 in so many notches: a move, a stop, a move.</summary>
        private static float Notched(float x, int notches)
        {
            x = MathHelper.Clamp(x, 0f, 1f) * notches;
            float whole = Math.Min(notches - 1, (float)Math.Floor(x));
            float m = MathHelper.Clamp((x - whole) / 0.55f, 0f, 1f);
            return (whole + m * m * (3f - 2f * m)) / notches;
        }

        /// <summary>Every tick, on every machine that draws: each blade's point where the state has it, and its elbows after it.</summary>
        private void UpdateArms(Player prey)
        {
            if (Main.dedServ)
                return;

            if (limbs == null)
            {
                limbs = new[] { new Limb(), new Limb() };

                for (int k = 0; k < 2; k++)
                {
                    Vector3[] folded = Folded(k);
                    Array.Copy(folded, limbs[k].At, 4);
                    limbs[k].Point = folded[3];
                }
            }

            float t = Timer * Pace;

            for (int k = 0; k < 2; k++)
            {
                Limb limb = limbs[k];
                Vector3[] folded = Folded(k);
                Vector3 rest = folded[3];
                bool cutting = false, resting = false;

                bool posed = false;

                if (Striking && k == Arm)
                {
                    if (Timer <= 0)
                        Array.Copy(limb.At, limb.Start, 4);

                    // Half a tick back first, while it cuts, so its smear is laid down twice a tick.
                    if (t >= Now.Winds && t <= Now.Winds + Now.Cuts + 3)
                    {
                        Vector3[] half = StrikePose(limb, k, t - 0.5f, out bool was);

                        if (was)
                        {
                            Array.Copy(half, limb.At, 4);
                            limb.Smear.Add(View(limb.At[3], out float halfSize), View(limb.At[2], out _), halfSize);
                            limb.Ghosts.Insert(0, (Vector3[])half.Clone());
                        }
                    }

                    Array.Copy(StrikePose(limb, k, t, out cutting), limb.At, 4);
                    limb.Point = limb.At[3];
                    posed = true;
                }
                else
                {
                    Vector3 want = rest;

                    if (State == Act.Arrive && picture == 13)
                    {
                        // The roar: both thrown wide, one toward the viewer and one away.
                        want = k == 0 ? new Vector3(96f, -70f, 110f) : new Vector3(60f, -120f, -70f);
                    }
                    else if (State == Act.Land || State == Act.Crouch)
                    {
                        want = rest + new Vector3(14f, 16f, 0f);
                    }
                    else if (State == Act.Air)
                    {
                        want = rest + new Vector3(-10f, -20f, 0f);
                    }
                    else if ((Main.GameUpdateCount + k * 47) % 110 < 9)
                    {
                        // Now and then a notch, as a thing does that is checking itself.
                        want = rest + new Vector3(0f, -8f, 0f);
                    }

                    Vector3 off = want - limb.Point;
                    float far = off.Length();
                    limb.Point = far <= 9f ? want : limb.Point + off / far * 9f;
                    resting = Vector3.Distance(limb.Point, rest) < 16f;
                }

                if (!posed)
                {
                    // Folded, its elbows go back to their fold; and the point's elbows found from it.
                    if (resting)
                        for (int i = 1; i <= 2; i++)
                            limb.At[i] = Vector3.Lerp(limb.At[i], folded[i], 0.35f);

                    Solve(limb.At, limb.Point);
                }

                // The blade's smear frames while it cuts, and its last poses; gone a step at a time after.
                if (cutting)
                {
                    limb.Smear.Add(View(limb.At[3], out float size), View(limb.At[2], out _), size);
                    limb.Ghosts.Insert(0, (Vector3[])limb.At.Clone());
                }
                else
                {
                    limb.Smear.Fade(3);

                    if (limb.Ghosts.Count > 0)
                        limb.Ghosts.RemoveAt(limb.Ghosts.Count - 1);
                }

                while (limb.Ghosts.Count > 3)
                    limb.Ghosts.RemoveAt(limb.Ghosts.Count - 1);

                // Which way its blade lies: cutting, its edge leading; kept so to the end of the blow; else to the femur.
                bool striking = Striking && k == Arm && t < Now.Winds + Now.Cuts + Now.Holds;
                Vector2 tip = View(limb.At[3], out _), bladeBase = View(limb.At[2], out _);
                Vector2 bladeWay = tip - bladeBase;
                bladeWay = bladeWay.LengthSquared() < 0.01f ? new Vector2(facing, 0f) : Vector2.Normalize(bladeWay);
                Vector2 edgeSide = new Vector2(-bladeWay.Y, bladeWay.X) * facing;

                if (!striking)
                    limb.SideKept = false;

                if (cutting)
                {
                    // Its edge where it is going whenever it goes fast; kept so after.
                    Vector2 moved = tip - limb.LastTip;

                    if (moved.Length() > 8f)
                    {
                        limb.BladeSide = Vector2.Dot(edgeSide, moved) >= 0f ? 1f : -1f;
                        limb.SideKept = true;
                    }
                }
                else if (!limb.SideKept)
                {
                    float toFemur = Vector2.Dot(edgeSide, View(limb.At[1], out _) - (tip + bladeBase) * 0.5f);

                    if (Math.Abs(toFemur) > 6f)
                        limb.BladeSide = toFemur >= 0f ? 1f : -1f;
                }

                limb.LastTip = tip;
            }
        }

        /// <summary>
        /// The elbows, from where the point is to be: back from the point and forward from the shoulder, each piece
        /// kept its length, a few times over. Out of its reach, the arm is straight at it.
        /// </summary>
        private static void Solve(Vector3[] at, Vector3 point)
        {
            float all = Lengths[0] + Lengths[1] + Lengths[2];
            at[0] = Vector3.Zero;

            if (point.Length() >= all - 0.5f)
            {
                Vector3 way = Vector3.Normalize(point);
                at[1] = way * Lengths[0];
                at[2] = at[1] + way * Lengths[1];
                at[3] = at[2] + way * Lengths[2];
                return;
            }

            for (int pass = 0; pass < 8; pass++)
            {
                at[3] = point;

                for (int i = 2; i >= 1; i--)
                    at[i] = at[i + 1] + Toward(at[i] - at[i + 1]) * Lengths[i];

                at[0] = Vector3.Zero;

                for (int i = 1; i <= 3; i++)
                    at[i] = at[i - 1] + Toward(at[i] - at[i - 1]) * Lengths[i - 1];
            }
        }

        private static Vector3 Toward(Vector3 v) => v.LengthSquared() < 0.0001f ? Vector3.UnitY : Vector3.Normalize(v);

        /// <summary>A place of an arm's as it is seen: in the world, and how many times its size (the nearer, the bigger).</summary>
        private Vector2 View(Vector3 at, out float size)
        {
            size = Eye / (Eye - Math.Min(at.Z, Eye - 45f));
            return Shoulder + new Vector2(at.X * facing, at.Y) * size;
        }

        /// <summary>An arm's blade as it is seen: from its last elbow to its point (a pierce: from its first, the arm being in a line), and how wide.</summary>
        private void BladeLine(int k, bool whole, out Vector2 from, out Vector2 to, out float wide)
        {
            if (limbs == null)
            {
                from = to = Shoulder;
                wide = 1f;
                return;
            }

            from = View(limbs[k].At[whole ? 1 : 2], out _);
            to = View(limbs[k].At[3], out float size);
            wide = 20f * size;
        }

        // ---- how it looks ----------------------------------------------------------------------------------------------

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D body = TextureAssets.Npc[Type].Value;
            SpriteEffects flip = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (NPC.IsABestiaryIconDummy)
            {
                spriteBatch.Draw(body, NPC.Bottom - screenPos, new Rectangle(0, 0, Side, Side), drawColor, 0f, new Vector2(Side * 0.5f, Side - FootGap), NPC.scale, SpriteEffects.None, 0f);
                return false;
            }

            bool arms = limbs != null && armArt != null && armArt[0].IsLoaded && armArt[1].IsLoaded && armArt[2].IsLoaded;

            // An arm is behind the body while it is away from the viewer: the far one as it hangs, either raised back.
            bool Behind(int k) => k == 1 ? limbs[k].At[2].Z <= 30f : limbs[k].At[2].Z < -30f;

            if (arms)
            {
                spriteBatch.End();

                for (int k = 1; k >= 0; k--)
                    if (Behind(k))
                        DrawWhole(k);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            }

            // Where it has fixed on, until it cuts: a dotted line from its shoulder.
            if (Striking && fixedOn != Vector2.Zero && Timer * Pace < Now.Winds)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Vector2 from = Shoulder, way = fixedOn - from;
                float far = way.Length();
                way /= Math.Max(1f, far);

                for (float d = Main.GlobalTimeWrappedHourly * 40f % 12f; d < far; d += 12f)
                {
                    Vector2 at = from + way * d;
                    at = new Vector2((float)Math.Floor(at.X / 2f) * 2f, (float)Math.Floor(at.Y / 2f) * 2f) - screenPos;
                    spriteBatch.Draw(pixel, at, new Rectangle(0, 0, 2, 2), new Color(255, 70, 80) * 0.7f);
                }
            }

            Vector2 feet = new Vector2((float)Math.Floor(Feet.X / 2f) * 2f, (float)Math.Floor(Feet.Y / 2f) * 2f + NPC.gfxOffY);
            spriteBatch.Draw(body, feet - screenPos, new Rectangle(0, picture * Side, Side, Side), drawColor, 0f, new Vector2(Side * 0.5f, Side - FootGap), NPC.scale, flip, 0f);

            if (arms)
            {
                spriteBatch.End();

                for (int k = 1; k >= 0; k--)
                    if (!Behind(k))
                        DrawWhole(k);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            }

            return false;
        }

        /// <summary>An arm and its blade's smear: the smear frames under, then its last poses faint, then the arm. No batch may be open.</summary>
        private void DrawWhole(int k)
        {
            Limb limb = limbs[k];

            if (limb.Smear.Count > 1)
            {
                Vector3 light = Vector3.Lerp(Lighting.GetColor(Shoulder.ToTileCoordinates()).ToVector3(), Vector3.One, 0.5f);
                Color body = new Color(new Vector3(206f, 212f, 226f) / 255f * light) * 0.85f;
                limb.Smear.Draw(body, new Color(255, 236, 240, 0), State == Act.Pierce ? 0.2f : 0.6f, 1.6f);
            }

            for (int g = limb.Ghosts.Count - 1; g >= 1; g--)
                DrawArm(limb.Ghosts[g], k, 2, 0.5f - g * 0.12f, limb.BladeSide);

            DrawArm(limb.At, k, 0, 1f, limb.BladeSide);
        }

        /// <summary>
        /// An arm in a pose: its pieces from <paramref name="from"/> (0 all three, 2 the blade alone), each a band of
        /// slices along its picture, every slice where its place is seen and as wide as the picture is there times how
        /// near it is; the blade flipped over its length when <paramref name="side"/> is -1. No batch may be open.
        /// </summary>
        private void DrawArm(Vector3[] pose, int k, int from, float shown, float side)
        {
            const int Slices = 12;

            for (int piece = from; piece < 3; piece++)
            {
                Texture2D art = armArt[piece].Value;
                (int pivotIn, int pivotOut, int across) = Pivots[piece];
                Vector3 a = pose[piece], b = pose[piece + 1];

                // Which way the piece lies as it is seen; under it is its picture's underside (the blade's: as it is flipped).
                Vector2 seenA = View(a, out _), seenB = View(b, out _);
                Vector2 along = seenB - seenA;
                along = along.LengthSquared() < 0.01f ? new Vector2(facing, 0f) : Vector2.Normalize(along);
                Vector2 under = new Vector2(-along.Y, along.X) * facing * (piece == 2 ? side : 1f);

                Strip.Begin();

                for (int s = 0; s <= Slices; s++)
                {
                    float x = art.Width * s / (float)Slices;
                    Vector3 at = Vector3.Lerp(a, b, (x - pivotIn) / (pivotOut - pivotIn));
                    Vector2 seen = View(at, out float size);

                    // Lit as the place is; the far arm in the body's shade; nearer brighter, further darker.
                    Vector3 light = Lighting.GetColor(seen.ToTileCoordinates()).ToVector3();
                    float depth = MathHelper.Clamp(1f + at.Z / 360f, 0.55f, 1.25f) * (k == 1 ? 0.72f : 1f);
                    Color colour = new Color(Vector3.Min(light * depth, Vector3.One)) * shown;

                    Strip.Pair(seen - under * across * size, seen + under * (art.Height - across) * size, colour, colour, x / art.Width);
                }

                Strip.Draw(art, SamplerState.PointClamp);
            }
        }
    }
}
