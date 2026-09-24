namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The ground the capital is given, measured once and handed to every builder.
    /// All of it in plan coordinates: x from the inland edge of the plain, y down from
    /// its level.
    /// </summary>
    public sealed class GraceSite
    {
        /// <summary>How wide the plain is, to the edge of the world.</summary>
        public int PlainWidth = 1800;

        /// <summary>Where the way up from the Mile levels out: the first column of flat ground.</summary>
        public int ForestStart = 230;

        /// <summary>The last columns of the world, which nobody can walk into.</summary>
        public int EdgeMargin = DefaultEdgeMargin;

        /// <summary>
        /// The edge margin every real world is built with. Worldgen needs it long before
        /// the site is measured, to know where the Font's shaft will come down.
        /// </summary>
        public const int DefaultEdgeMargin = 44;

        /// <summary>How far above the plain anything is built.</summary>
        public int Top = 260;

        /// <summary>How far below the plain anything is built.</summary>
        public int Depth = 1250;

        /// <summary>Where the rock layer starts, below the plain.</summary>
        public int RockLayer = 670;

        /// <summary>The surface of the Shimmer, below the plain. The pool is built around it.</summary>
        public int ShimmerY = 1080;

        /// <summary>Only the preview uses this: the ground at the foot of the ramp.</summary>
        public int RampFoot = 90;

        public static GraceSite Preview() => new GraceSite();
    }
}
