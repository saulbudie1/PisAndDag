using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// The mirror barrier's crack, its shape (EmpressOfLight.Shield; by request: "its size the circle is still there but
    /// invisible but its freeform and it expands outwards ... make the glass actually have the fracture and repeating
    /// effect"): a crack in the world standing between her and the player, as a pane of glass struck through and not yet
    /// fallen. A spine down it, jagged, across the way to the player; branches off it either side, leaning out from its
    /// middle, forked at their ends. The panes between the spine and the branches are its shards (the world seen in them
    /// moved, and again smaller inside each, a mirror in a mirror: EmpressMirror).
    ///
    /// All of it in shares of its reach (x toward the player, y across), the same everywhere for an act's seed. It grows
    /// as one shape, outward (its scale the glass's growth), and the branches come as it grows, the middle's first.
    /// </summary>
    internal sealed class BarrierNet
    {
        /// <summary>Steps down the spine.</summary>
        public const int Steps = 16;

        /// <summary>The spine's points, top to bottom (y from -1 to 1).</summary>
        public readonly Vector2[] Spine = new Vector2[Steps + 1];

        public readonly Branch[] Branches;

        internal sealed class Branch
        {
            /// <summary>The spine point it leaves from; the side it goes (1 toward the player, -1 back toward her).</summary>
            public int Root;
            public int Side;

            /// <summary>Its points after the root, as offsets from the root (three, jagged).</summary>
            public Vector2[] Points;

            /// <summary>The two twigs it forks into at its end (offsets from its last point), or null.</summary>
            public Vector2[] Twigs;

            /// <summary>The growth it starts to show at; its own number.</summary>
            public float Born;
            public float Own;
        }

        private const int BranchCount = 14;

        private static int cachedSeed = int.MinValue;
        private static BarrierNet cached;

        public static BarrierNet Of(int seed)
        {
            if (cached == null || cachedSeed != seed)
            {
                cached = new BarrierNet(seed);
                cachedSeed = seed;
            }

            return cached;
        }

        /// <summary>How much of a branch has come at this growth (0 none, 1 whole).</summary>
        public static float Grown(Branch branch, float growth) => MathHelper.Clamp((growth - branch.Born) / 0.22f, 0f, 1f);

        private BarrierNet(int seed)
        {
            EmpressOfLight.UnifiedRandomish dice = new EmpressOfLight.UnifiedRandomish(seed ^ 0x5A17);

            for (int i = 0; i <= Steps; i++)
            {
                float y = -1f + 2f * i / Steps;
                bool end = i == 0 || i == Steps;
                Spine[i] = new Vector2((dice.Next() - 0.5f) * 0.14f, y + (end ? 0f : (dice.Next() - 0.5f) * 0.05f));
            }

            Branches = new Branch[BranchCount];
            int side = dice.Next() < 0.5f ? 1 : -1;

            for (int k = 0; k < BranchCount; k++)
            {
                // Spread down the spine, a little off evenly, never at its very ends.
                int root = Math.Clamp((int)Math.Round(1 + (Steps - 2) * (k + 0.2f + dice.Next() * 0.6f) / BranchCount), 1, Steps - 1);
                float y = Spine[root].Y;
                float outward = Math.Abs(y) < 0.12f ? (dice.Next() < 0.5f ? -1f : 1f) : Math.Sign(y);
                float lean = 0.45f + 0.7f * dice.Next();
                float length = (0.3f + 0.32f * dice.Next()) * (1f - 0.35f * Math.Abs(y));
                float way = (float)Math.Atan2(outward * Math.Sin(lean), side * Math.Cos(lean));

                Vector2[] points = new Vector2[3];
                Vector2 at = Vector2.Zero;

                for (int s = 0; s < 3; s++)
                {
                    way += (dice.Next() - 0.5f) * 0.6f;
                    at += way.ToRotationVector2() * length / 3f;
                    points[s] = at;
                }

                Vector2[] twigs = null;

                if (dice.Next() < 0.65f)
                {
                    float fork = 0.4f + 0.3f * dice.Next();
                    float twig = 0.07f + 0.07f * dice.Next();
                    twigs = new[] { (way - fork).ToRotationVector2() * twig, (way + fork).ToRotationVector2() * twig };
                }

                Branches[k] = new Branch
                {
                    Root = root,
                    Side = side,
                    Points = points,
                    Twigs = twigs,
                    Born = 0.06f + 0.55f * Math.Abs(y) + 0.18f * dice.Next(),
                    Own = dice.Next(),
                };

                side = -side;
            }

            Array.Sort(Branches, (a, b) => a.Root.CompareTo(b.Root));
        }
    }
}
