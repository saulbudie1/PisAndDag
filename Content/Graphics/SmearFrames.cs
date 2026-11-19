using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PisAndDag.Content.Graphics
{
    /// <summary>
    /// Smear frames, as a hand-drawn swing has them (by request: "and add a smear frame system too"): while something
    /// sweeps fast, the space it has just swept through is drawn as a solid shape of its own colour, thick where it
    /// is and thinning back along its path, its leading edge a bright line. Not a glow over it: the thing smeared out
    /// across where it has been.
    ///
    /// Whatever uses it keeps one, and while the thing sweeps <see cref="Add"/>s where its two ends are (its tip, and
    /// the end it turns about), as often as it likes (twice a tick reads smoothly); when it stops, <see cref="Fade"/>s
    /// it a few a tick; and draws it under the thing (<see cref="Draw"/>, no sprite batch open), so that the thing
    /// itself is the head of its own smear. World places; each its size if the thing is drawn bigger or smaller as it
    /// goes (the leading edge is as thick as that).
    ///
    /// Drawn as two bands (Strip): the swept shape, from where the tip was to part of the way back toward where its
    /// other end was, solid at the newest and gone at the oldest; and the edge, a thin line along where the tip was,
    /// added light. Every corner on the world's two-pixel grid. Client only.
    /// </summary>
    public sealed class SmearFrames
    {
        private readonly struct Frame
        {
            public readonly Vector2 Tip, Base;
            public readonly float Size;

            public Frame(Vector2 tip, Vector2 @base, float size)
            {
                Tip = tip;
                Base = @base;
                Size = size;
            }
        }

        private readonly List<Frame> frames = new();

        /// <summary>How many frames it keeps, newest first; older ones fall off the end.</summary>
        public int Most = 20;

        public int Count => frames.Count;

        /// <summary>Where the thing is now: its tip, the end it turns about, how big it is drawn.</summary>
        public void Add(Vector2 tip, Vector2 turnsAbout, float size = 1f)
        {
            frames.Insert(0, new Frame(tip, turnsAbout, size));

            while (frames.Count > Most)
                frames.RemoveAt(frames.Count - 1);
        }

        /// <summary>The oldest <paramref name="many"/> gone: call each tick once it has stopped sweeping.</summary>
        public void Fade(int many = 2)
        {
            for (int i = 0; i < many && frames.Count > 0; i++)
                frames.RemoveAt(frames.Count - 1);
        }

        public void Clear() => frames.Clear();

        private static Vector2 Snap(Vector2 at) => new((float)Math.Floor(at.X / 2f) * 2f, (float)Math.Floor(at.Y / 2f) * 2f);

        /// <summary>
        /// The smear. <paramref name="body"/>: the swept shape's colour, laid over what is behind it (its alpha is its
        /// strength at the newest). <paramref name="edge"/>: the leading edge's, added (alpha 0). <paramref name="reach"/>:
        /// how far back from the tip toward the other end the shape goes, 0 to 1. <paramref name="edgeWidth"/>: the
        /// edge's half thickness at size 1, in pixels. No sprite batch may be open.
        /// </summary>
        public void Draw(Color body, Color edge, float reach = 0.55f, float edgeWidth = 1.5f)
        {
            if (Main.dedServ || frames.Count < 2)
                return;

            Texture2D white = TextureAssets.MagicPixel.Value;
            int n = frames.Count;

            // The swept shape: solid at the newest, gone at the oldest; thinner at its inner end.
            Strip.Begin();

            for (int i = 0; i < n; i++)
            {
                float shown = 1f - i / (float)(n - 1);
                shown *= shown;
                Frame f = frames[i];
                Vector2 inner = Vector2.Lerp(f.Tip, f.Base, reach * (0.35f + 0.65f * shown));
                Strip.Pair(Snap(f.Tip), Snap(inner), body * shown, body * (0.2f * shown), 0.5f);
            }

            Strip.Draw(white, SamplerState.PointClamp);

            // The leading edge: along where the tip was, thick and bright at the newest.
            Strip.Begin();

            for (int i = 0; i < n; i++)
            {
                float shown = 1f - i / (float)(n - 1);
                Frame f = frames[i];
                Vector2 path = frames[Math.Max(0, i - 1)].Tip - frames[Math.Min(n - 1, i + 1)].Tip;
                Vector2 across = path.LengthSquared() < 0.01f ? Vector2.UnitY : Vector2.Normalize(new Vector2(-path.Y, path.X));
                float half = Math.Max(1f, edgeWidth * f.Size * (0.4f + 0.6f * shown));
                Strip.Pair(Snap(f.Tip - across * half), Snap(f.Tip + across * half), edge * shown, edge * shown, 0.5f);
            }

            Strip.Draw(white, SamplerState.PointClamp);
        }
    }
}
