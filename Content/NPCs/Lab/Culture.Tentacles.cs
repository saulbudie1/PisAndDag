using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.NPCs.Lab
{
    /// <summary>
    /// One of a Culture's six tentacles: a chain of points, each pulled toward the last as a rope is (Verlet), its root on
    /// the mass. Free, its tip feels about, wildly; sent out, its tip whips to a place on a wall, a floor or a ceiling and
    /// takes hold there; holding, it pulls the mass after it until the mass has gone past it or it is stretched too far,
    /// and lets go. Shot, the point that was hit is thrown the way the shot was going, and the rest of the chain after
    /// it: the mass does not move for it.
    ///
    /// Over all that, its sway (by request: "make it sway more madly; research on how carrion sway"; Carrion's monster
    /// is a rope of springs with a wave run down it, the wave the stronger the slacker the rope): two waves run along
    /// it across its length, one out and one back, at its own time; free, from nothing at the root to most at the tip;
    /// held, most at its middle and nothing at either end, more and in more kinks the longer it is drawn out; and now
    /// and then a spasm, a point of it thrown and the waves twice and a half as wide for a moment. The sway is where it
    /// is drawn and where it is hit (Shown), not where the rope is.
    /// </summary>
    public sealed class Tentacle
    {
        public enum Hold { Free, Reaching, Holding }

        /// <summary>Points along it, root first.</summary>
        public const int Points = 12;

        /// <summary>How far one reaches for a hold, and how far it lets itself be stretched before it lets go.</summary>
        public const float Reach = 208f;
        public const float Stretch = 1.2f;

        /// <summary>How fast its tip whips out to a hold, a tick; how near it must come to have it; how long it tries.</summary>
        private const float Whip = 26f;
        private const float Grips = 9f;
        private const int Tries = 20;

        /// <summary>A length of it relaxed, between two points; and the least and most a length is drawn out to.</summary>
        private const float Relaxed = 5.5f;
        private const float Shortest = 4.5f;
        private const float Longest = 22f;

        public readonly Vector2[] At = new Vector2[Points];
        private readonly Vector2[] was = new Vector2[Points];

        public Hold State;
        public Vector2 Anchor;

        /// <summary>Its own way out of the mass (radians), its thickness at the root (pixels), and its own time.</summary>
        public float Angle;
        public int Thick;
        public float Phase;

        /// <summary>Holding: how much rope it lets out (pixels from the middle of the mass to its hold, no more), taken in as the mass comes on; and that it took hold this tick.</summary>
        public float Rope;
        public bool JustHeld;

        /// <summary>A spasm: one at its height, falling away a tick at a time.</summary>
        public float Spasm;
        private float now;

        /// <summary>Ticks before it may reach for a hold again; ticks it has been reaching for this one.</summary>
        public int Rest;
        private int reaching;

        /// <summary>Its bounds this tick, for a quick look at what could have hit it.</summary>
        public Rectangle Bounds;

        public Tentacle(Vector2 root, float angle, int thick, float phase)
        {
            Angle = angle;
            Thick = thick;
            Phase = phase;

            for (int k = 0; k < Points; k++)
                At[k] = was[k] = root + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * k * Relaxed;
        }

        public Vector2 Tip => At[Points - 1];

        public void LetGo(int rest)
        {
            State = Hold.Free;
            Rest = rest;
        }

        public void ReachFor(Vector2 anchor)
        {
            State = Hold.Reaching;
            Anchor = anchor;
            Rope = float.MaxValue;
            reaching = 0;
        }

        /// <summary>
        /// A tick of it: the root on the mass; the tip at its hold, on its way to one, or feeling about (<paramref name="feel"/>:
        /// where it wants to be, free); every point kept its length from the next, four times over.
        /// </summary>
        public void Update(Vector2 root, Vector2 feel, float time)
        {
            if (Rest > 0)
                Rest--;

            now = time;
            Spasm *= 0.9f;

            if (State == Hold.Reaching && ++reaching > Tries)
                LetGo(6);

            At[0] = root;
            was[0] = root;

            Vector2 tipTo = State == Hold.Free ? feel : Anchor;
            float span = Vector2.Distance(root, tipTo);
            float length = State == Hold.Free ? Relaxed : Math.Clamp(span / (Points - 1), Shortest, Longest);

            // Moved by what moved it last tick, a little less; free ones sag a little.
            for (int k = 1; k < Points; k++)
            {
                Vector2 velocity = (At[k] - was[k]) * 0.86f;
                was[k] = At[k];
                At[k] += velocity + new Vector2(0f, State == Hold.Free ? 0.12f : 0.03f);
            }

            // A spasm: a point along it thrown, and its sway the wider a moment.
            if (Main.rand.NextBool(State == Hold.Free ? 45 : 100))
            {
                Spasm = 1f;
                At[Main.rand.Next(3, Points - 1)] += Main.rand.NextVector2Circular(1f, 1f) * (State == Hold.Free ? 8f : 5f);
            }

            // Its tip: held where it holds; whipped toward its hold; or drawn toward where it is feeling, writhing.
            switch (State)
            {
                case Hold.Holding:
                    At[Points - 1] = Anchor;
                    break;

                case Hold.Reaching:
                {
                    Vector2 to = Anchor - At[Points - 1];
                    float d = to.Length();

                    if (d <= Grips)
                    {
                        At[Points - 1] = Anchor;
                        State = Hold.Holding;
                        JustHeld = true;
                    }
                    else
                    {
                        At[Points - 1] += to / d * Math.Min(Whip, d);
                    }

                    break;
                }

                default:
                {
                    Vector2 writhe = new Vector2(MathF.Sin(time * 5.3f + Phase * 2f), MathF.Cos(time * 3.7f + Phase)) * (4.5f + 3f * Spasm);
                    At[Points - 1] += (feel - At[Points - 1]) * 0.22f + writhe;
                    break;
                }
            }

            bool pinned = State != Hold.Free;

            for (int pass = 0; pass < 4; pass++)
            {
                for (int k = 0; k < Points - 1; k++)
                {
                    Vector2 a = At[k], b = At[k + 1];
                    Vector2 d = b - a;
                    float now = d.Length();

                    if (now < 0.001f)
                        continue;

                    float off = (now - length) / now;
                    bool rootEnd = k == 0;
                    bool tipEnd = k + 1 == Points - 1 && pinned;

                    if (rootEnd && tipEnd)
                        continue;

                    if (rootEnd)
                        At[k + 1] -= d * off;
                    else if (tipEnd)
                        At[k] += d * off;
                    else
                    {
                        At[k] += d * off * 0.5f;
                        At[k + 1] -= d * off * 0.5f;
                    }
                }
            }

            // A little wave along a held one, so that it lives.
            if (State == Hold.Holding)
            {
                Vector2 along = Anchor - root;
                float l = along.Length();

                if (l > 1f)
                {
                    Vector2 across = new Vector2(-along.Y, along.X) / l;

                    for (int k = 2; k < Points - 2; k++)
                        At[k] += across * MathF.Sin(time * 5f + Phase + k * 0.7f) * 0.35f;
                }
            }

            // Let go of and still drawn out (more than twice as long as it is relaxed): it snaps back to the mass, every
            // point a third of the way in a tick, over whatever is in the way (by request, no tentacle left stretched:
            // "making it stretch which is not apply to a rubbery bandy tentacle physics").
            bool snapping = false;

            if (State == Hold.Free)
            {
                float run = 0f;

                for (int k = 0; k < Points - 1; k++)
                    run += Vector2.Distance(At[k], At[k + 1]);

                if (run > (Points - 1) * Relaxed * 2f)
                {
                    snapping = true;

                    for (int k = 1; k < Points; k++)
                        At[k] = root + (At[k] - root) * 0.66f;
                }
            }

            // Kept out of what is solid: a point that has gone into a block slides along it (keeps what of its move takes
            // it into no block, across or up and down), else goes back to where it was this tick (but for a tip on its
            // way to a hold, or at it; and one snapping back).
            for (int k = 1; k < Points && !snapping; k++)
            {
                if (k == Points - 1 && State != Hold.Free)
                    continue;

                if (!InBlock(At[k]) || InBlock(was[k]))
                    continue;

                Vector2 across = new Vector2(At[k].X, was[k].Y), upDown = new Vector2(was[k].X, At[k].Y);
                At[k] = !InBlock(across) ? across : !InBlock(upDown) ? upDown : was[k];
            }

            float minX = At[0].X, maxX = minX, minY = At[0].Y, maxY = minY;

            for (int k = 1; k < Points; k++)
            {
                minX = Math.Min(minX, At[k].X);
                maxX = Math.Max(maxX, At[k].X);
                minY = Math.Min(minY, At[k].Y);
                maxY = Math.Max(maxY, At[k].Y);
            }

            int pad = Thick + 6 + 24;
            Bounds = new Rectangle((int)minX - pad, (int)minY - pad, (int)(maxX - minX) + pad * 2, (int)(maxY - minY) + pad * 2);
        }

        /// <summary>
        /// Shot at point <paramref name="k"/>: it and the points after it thrown the way the shot was going, less the further
        /// on; a hold near the tip may be knocked loose.
        /// </summary>
        public bool Struck(int k, Vector2 push)
        {
            for (int i = Math.Max(1, k - 1); i < Points; i++)
            {
                float share = i < k ? 0.5f : 1f / (1f + (i - k) * 0.35f);
                At[i] += push * share;
            }

            if (State != Hold.Free && k >= Points - 4 && Main.rand.NextBool(2))
            {
                LetGo(20);
                return true;
            }

            return false;
        }

        /// <summary>Whether this place is in a solid block.</summary>
        public static bool InBlock(Vector2 at)
        {
            int x = (int)(at.X / 16f), y = (int)(at.Y / 16f);
            return WorldGen.InWorld(x, y) && WorldGen.SolidTile(x, y);
        }

        /// <summary>How thick it is this far along it (0 its root, 1 its tip), in pixels: its root's thickness, then four, then two.</summary>
        public int ThickAlong(float f) => f < 0.22f ? Thick : f < 0.6f ? 4 : 2;

        /// <summary>How thick it is at point <paramref name="k"/>.</summary>
        public int ThickAt(int k) => ThickAlong(k / (float)(Points - 1));

        /// <summary>Point <paramref name="k"/> where it is drawn and hit: the rope's point, swayed.</summary>
        public Vector2 Shown(int k) => At[k] + Sway(k / (float)(Points - 1));

        /// <summary>
        /// Its sway this far along it (0 its root, 1 its tip): across the line from its root to its tip, two waves running
        /// along it, one out from the root and one back, as wide as its hold has it, the wider in a spasm.
        /// </summary>
        public Vector2 Sway(float f)
        {
            Vector2 along = Tip - At[0];
            float l = along.Length();
            Vector2 across = l > 1f ? new Vector2(-along.Y, along.X) / l : new Vector2(-MathF.Sin(Angle), MathF.Cos(Angle));

            float wide, reach, kinks, speed;

            switch (State)
            {
                case Hold.Holding:
                    wide = 2.5f + Math.Min(l, Reach * Stretch) * 0.025f;
                    reach = MathF.Sin(MathF.PI * f);
                    kinks = 1.2f + l / 110f;
                    speed = 9f;
                    break;

                case Hold.Reaching:
                    wide = 3f;
                    reach = f;
                    kinks = 1.5f;
                    speed = 14f;
                    break;

                default:
                    wide = 6f;
                    reach = MathF.Pow(f, 0.7f);
                    kinks = 1.1f;
                    speed = 11f;
                    break;
            }

            wide *= 1f + 1.5f * Spasm;
            float a = f * kinks * MathHelper.TwoPi;
            float wave = MathF.Sin(a - now * speed + Phase) * 0.65f + MathF.Sin(a * 2.3f + now * speed * 0.7f + Phase * 1.7f) * 0.35f;
            return across * (wave * wide * reach);
        }
    }
}
