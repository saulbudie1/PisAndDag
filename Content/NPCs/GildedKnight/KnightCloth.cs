using System;
using Microsoft.Xna.Framework;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// A hanging piece of the knight's cloth (his cape, a flap of his tabard, his plume) as a
    /// chain of points: the first held where it is sewn on, the rest falling, dragging through
    /// the air and keeping their lengths (Verlet, as cloth is done in every game that does it
    /// cheaply). A little stiffness keeps each link near the line of the one before it, so it
    /// falls in a curve and not a zigzag; a plume is also held to point the way its socket does
    /// for its first link, so it rises before it falls.
    ///
    /// The chain is world pixels, stepped once a tick by whoever wears it, and laid out as a
    /// strip of the cloth's picture to be drawn (KnightCanvas). Nothing here touches the game:
    /// the previews run it as it is.
    /// </summary>
    public sealed class KnightCloth
    {
        public readonly Vector2[] Points;
        private readonly Vector2[] old;

        /// <summary>The length of each link, in pixels.</summary>
        public readonly float Segment;

        /// <summary>Pixels a tick per tick it falls; how much of its speed it keeps; how hard each link is pulled into line with the one before.</summary>
        public float Gravity;
        public float Drag;
        public float Stiffness;

        /// <summary>How hard the first link is pulled toward the way its root points (a plume rising out of its socket); 0 for cloth that hangs.</summary>
        public float RootPull;

        /// <summary>It has been laid out at least once.</summary>
        public bool Ready { get; private set; }

        public int Links => Points.Length - 1;

        public KnightCloth(int links, float segment, float gravity = 0.32f, float drag = 0.9f, float stiffness = 0.12f, float rootPull = 0f)
        {
            Points = new Vector2[links + 1];
            old = new Vector2[links + 1];
            Segment = segment;
            Gravity = gravity;
            Drag = drag;
            Stiffness = stiffness;
            RootPull = rootPull;
        }

        /// <summary>Laid out straight from the root along a way, at rest: for its first tick, and when it has been put somewhere new.</summary>
        public void Reset(Vector2 root, Vector2 way)
        {
            if (way.LengthSquared() < 0.0001f)
                way = Vector2.UnitY;

            way.Normalize();

            for (int i = 0; i < Points.Length; i++)
            {
                Points[i] = root + way * (Segment * i);
                old[i] = Points[i];
            }

            Ready = true;
        }

        /// <summary>
        /// One tick: the root to where it is sewn on now, the rest moving on by what they had,
        /// falling, pushed by <paramref name="push"/> (the wind, a blow), and kept above
        /// <paramref name="ground"/> (a y in the world). <paramref name="rootWay"/>: the way the
        /// root points (a world angle), for one with a RootPull.
        /// </summary>
        public void Step(Vector2 root, float rootWay, Vector2 push, float ground)
        {
            if (!Ready)
                Reset(root, RootPull > 0f ? new Vector2((float)Math.Cos(rootWay), (float)Math.Sin(rootWay)) : Vector2.UnitY);

            Points[0] = root;
            old[0] = root;

            for (int i = 1; i < Points.Length; i++)
            {
                Vector2 v = (Points[i] - old[i]) * Drag;
                old[i] = Points[i];
                Points[i] += v + push + new Vector2(0f, Gravity);
            }

            Vector2 rootDir = new Vector2((float)Math.Cos(rootWay), (float)Math.Sin(rootWay));

            for (int pass = 0; pass < 4; pass++)
            {
                // Each link its own length: the root's end never moves.
                for (int i = 0; i < Points.Length - 1; i++)
                {
                    Vector2 a = Points[i];
                    Vector2 b = Points[i + 1];
                    Vector2 d = b - a;
                    float length = d.Length();

                    if (length < 0.0001f)
                        continue;

                    float diff = (length - Segment) / length;

                    if (i == 0)
                    {
                        Points[i + 1] = b - d * diff;
                    }
                    else
                    {
                        Points[i] = a + d * (diff * 0.5f);
                        Points[i + 1] = b - d * (diff * 0.5f);
                    }
                }

                // The first link toward the way out of its socket.
                if (RootPull > 0f && Points.Length > 1)
                {
                    Vector2 want = Points[0] + rootDir * Segment;
                    Points[1] += (want - Points[1]) * RootPull;
                }

                // Each later link toward the line of the one before: a curve, not a zigzag.
                for (int i = 1; i < Points.Length - 1; i++)
                {
                    Vector2 back = Points[i] - Points[i - 1];
                    float length = back.Length();

                    if (length < 0.0001f)
                        continue;

                    Vector2 want = Points[i] + back * (Segment / length);
                    Points[i + 1] += (want - Points[i + 1]) * Stiffness;
                }

                // Never through the ground he stands on.
                for (int i = 1; i < Points.Length; i++)
                {
                    if (Points[i].Y > ground)
                    {
                        Points[i].Y = ground;

                        // It drags on the ground: what it had along it is mostly lost.
                        old[i].X = MathHelper.Lerp(old[i].X, Points[i].X, 0.5f);
                    }
                }
            }
        }

        /// <summary>
        /// Every point past the root kept on one side of the upright line through <paramref name="x"/>:
        /// no further toward <paramref name="side"/> (+1 or -1) than <paramref name="slack"/> past it
        /// at the top, <paramref name="slackLow"/> at the end. A cape kept behind his back, a flap in
        /// front of his legs.
        /// </summary>
        public void KeepBehind(float x, int side, float slack, float slackLow)
        {
            for (int i = 1; i < Points.Length; i++)
            {
                float share = i / (float)(Points.Length - 1);
                float most = x + side * MathHelper.Lerp(slack, slackLow, share);

                if ((Points[i].X - most) * side > 0f)
                {
                    float was = Points[i].X;
                    Points[i].X = most;
                    old[i].X += Points[i].X - was;
                }
            }
        }

        /// <summary>
        /// Every point past the root kept off the side of a line (a leg, from hip to foot) that
        /// <paramref name="side"/> says (+1: kept to the line's right, as seen walking from a to b),
        /// within the line's span: a flap pushed along by the thigh it hangs over.
        /// </summary>
        public void KeepOff(Vector2 a, Vector2 b, int side, float margin)
        {
            Vector2 ab = b - a;
            float length = ab.Length();

            if (length < 0.001f)
                return;

            Vector2 along = ab / length;
            Vector2 right = new Vector2(-along.Y, along.X) * side;

            for (int i = 1; i < Points.Length; i++)
            {
                Vector2 r = Points[i] - a;
                float s = Vector2.Dot(r, along);

                if (s < 0f || s > length)
                    continue;

                float off = Vector2.Dot(r, right);

                if (off < margin)
                {
                    Vector2 shift = right * (margin - off);
                    Points[i] += shift;
                    old[i] += shift * 0.5f;
                }
            }
        }

        /// <summary>Moves all of it by this much at once, speed and all: for a knight put somewhere else, so his cloth does not fly after him.</summary>
        public void Shift(Vector2 by)
        {
            for (int i = 0; i < Points.Length; i++)
            {
                Points[i] += by;
                old[i] += by;
            }
        }

        /// <summary>
        /// The strip's edges, for drawing: either side of each point, <paramref name="widthAt"/>
        /// of the way along it wide, the picture's first edge (its row 0) on the side of
        /// <paramref name="firstSide"/> (a world direction the first edge is toward).
        /// </summary>
        public void Edges(Func<float, float> widthAt, Vector2 firstSide, Vector2[] first, Vector2[] second) => Edges(widthAt, firstSide, 0f, first, second);

        /// <summary>
        /// The same, the strip moved off its chain toward the first side by <paramref name="lean"/>
        /// of its width (a half: the chain is its second edge): a cape whose sewn edge runs down
        /// his back and whose cloth hangs out behind it.
        /// </summary>
        public void Edges(Func<float, float> widthAt, Vector2 firstSide, float lean, Vector2[] first, Vector2[] second)
        {
            int n = Points.Length;

            for (int i = 0; i < n; i++)
            {
                Vector2 d = i == 0 ? Points[1] - Points[0] : i == n - 1 ? Points[n - 1] - Points[n - 2] : Points[i + 1] - Points[i - 1];

                if (d.LengthSquared() < 0.0001f)
                    d = Vector2.UnitY;

                d.Normalize();

                Vector2 normal = new Vector2(-d.Y, d.X);

                if (Vector2.Dot(normal, firstSide) < 0f)
                    normal = -normal;

                float width = widthAt(i / (float)(n - 1));
                Vector2 middle = Points[i] + normal * (width * lean);

                first[i] = middle + normal * (width * 0.5f);
                second[i] = middle - normal * (width * 0.5f);
            }
        }
    }
}
