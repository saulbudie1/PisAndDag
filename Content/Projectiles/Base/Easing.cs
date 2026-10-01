using System;
using Microsoft.Xna.Framework;

namespace PisAndDag.Content.Projectiles.Base
{
    /// <summary>The shapes a piece of an <see cref="Easing"/> curve can move along.</summary>
    public enum Ease : byte
    {
        Linear,
        SineIn,
        SineOut,
        SineInOut,

        /// <summary>Slow, then faster and faster: t to the degree.</summary>
        PolyIn,

        /// <summary>Fast, then slower and slower.</summary>
        PolyOut,

        PolyInOut,

        /// <summary>Very fast, then all but still: a thing let go of and caught.</summary>
        ExpOut,
    }

    /// <summary>
    /// One piece of an <see cref="Easing"/> curve: from <see cref="Start"/> in the animation's progress
    /// until the next piece starts, it goes from <see cref="From"/> to From + <see cref="Shift"/>
    /// along its easing.
    /// </summary>
    public readonly struct Piece
    {
        public readonly Ease Ease;
        public readonly float Start;
        public readonly float From;
        public readonly float Shift;
        public readonly int Degree;

        public Piece(Ease ease, float start, float from, float shift, int degree = 1)
        {
            Ease = ease;
            Start = start;
            From = from;
            Shift = shift;
            Degree = Math.Max(1, degree);
        }
    }

    /// <summary>
    /// A motion written as pieces, the way Calamity writes its swings (CurveSegment and
    /// PiecewiseAnimation in its MathematicalUtils): lift slowly, hang, come down hard, as one
    /// piece each. Each piece starts where the one before it ended, or does not, and the jump is
    /// the author's to make; nothing here smooths it. This is its own code, not theirs.
    ///
    /// Not called Curve: FNA has a Microsoft.Xna.Framework.Curve, and every file here uses that
    /// namespace.
    /// </summary>
    public static class Easing
    {
        /// <summary>Where the curve is at <paramref name="progress"/>, 0 to 1 (clamped).</summary>
        public static float At(float progress, Piece[] pieces)
        {
            if (pieces == null || pieces.Length == 0)
                return 0f;

            progress = MathHelper.Clamp(progress, 0f, 1f);

            int i = pieces.Length - 1;
            while (i > 0 && progress < pieces[i].Start)
                i--;

            Piece piece = pieces[i];
            float end = i + 1 < pieces.Length ? pieces[i + 1].Start : 1f;
            float local = end <= piece.Start ? 1f : (progress - piece.Start) / (end - piece.Start);

            return piece.From + Apply(piece.Ease, local, piece.Degree) * piece.Shift;
        }

        /// <summary><paramref name="t"/>, 0 to 1 (clamped), eased.</summary>
        public static float Apply(Ease ease, float t, int degree = 1)
        {
            t = MathHelper.Clamp(t, 0f, 1f);

            switch (ease)
            {
                case Ease.SineIn:
                    return 1f - (float)Math.Cos(t * MathHelper.PiOver2);
                case Ease.SineOut:
                    return (float)Math.Sin(t * MathHelper.PiOver2);
                case Ease.SineInOut:
                    return (1f - (float)Math.Cos(t * MathHelper.Pi)) * 0.5f;
                case Ease.PolyIn:
                    return (float)Math.Pow(t, degree);
                case Ease.PolyOut:
                    return 1f - (float)Math.Pow(1f - t, degree);
                case Ease.PolyInOut:
                    return t < 0.5f
                        ? (float)Math.Pow(2f, degree - 1) * (float)Math.Pow(t, degree)
                        : 1f - (float)Math.Pow(-2f * t + 2f, degree) * 0.5f;
                case Ease.ExpOut:
                    return t >= 1f ? 1f : 1f - (float)Math.Pow(2f, -10f * t);
                default:
                    return t;
            }
        }

        /// <summary>Smootherstep: 0 and 1 with nothing moving at either end.</summary>
        public static float Smoother(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * t * (t * (6f * t - 15f) + 10f);
        }
    }
}
