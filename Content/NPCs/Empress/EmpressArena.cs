using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// A regular polygon lying flat about its middle, corner 0 to the right: corner k at k/sides of a
    /// turn, turning the way the screen does (y down); edge j from corner j to corner j + 1, its
    /// outward normal half a step further round. With an even number of sides the top and bottom
    /// are edges, level, and the left and right are corners. All in world pixels, from the middle;
    /// a radius is from the middle to a corner.
    /// </summary>
    internal static class Polygon
    {
        public static Vector2[] Corners(int sides)
        {
            Vector2[] corners = new Vector2[sides];

            for (int k = 0; k < sides; k++)
                corners[k] = (MathHelper.TwoPi * k / sides).ToRotationVector2();

            return corners;
        }

        public static Vector2[] Normals(int sides)
        {
            Vector2[] normals = new Vector2[sides];

            for (int j = 0; j < sides; j++)
                normals[j] = (MathHelper.TwoPi * (j + 0.5f) / sides).ToRotationVector2();

            return normals;
        }

        public static int Wrap(int k, int sides) => ((k % sides) + sides) % sides;
    }

    /// <summary>
    /// The shape of her arena: a decagon lying flat (by request: ten stars, a corner each), its top and
    /// bottom edges level, a corner to the left and a corner to the right. Edge 2 is the bottom and
    /// edge 7 the top. The apothem (middle to an edge) is the radius times cos 18°.
    /// </summary>
    internal static class Decagon
    {
        public const int Sides = 10;

        /// <summary>Her ten stars, one on each corner, in order round it: star i is corner i.</summary>
        public const int Stars = 10;

        /// <summary>Middle to an edge, for a radius of one.</summary>
        public static readonly float Apothem = (float)Math.Cos(Math.PI / Sides);

        private static readonly Vector2[] corners = Polygon.Corners(Sides);
        private static readonly Vector2[] normals = Polygon.Normals(Sides);

        public static Vector2 Corner(Vector2 centre, float radius, int k) => centre + corners[Polygon.Wrap(k, Sides)] * radius;

        public static Vector2 Normal(int edge) => normals[Polygon.Wrap(edge, Sides)];

        public static Vector2 Star(Vector2 centre, float radius, int star) => Corner(centre, radius, star);

        /// <summary>
        /// How far out a point is, as the radius of the decagon about the middle that it lies on: the
        /// same all along one edge of it, so it goes evenly across anything cut along the edges.
        /// </summary>
        public static float RadiusAt(Vector2 point, Vector2 centre)
        {
            Vector2 p = point - centre;
            float most = float.MinValue;

            for (int j = 0; j < Sides; j++)
                most = Math.Max(most, Vector2.Dot(p, normals[j]));

            return most / Apothem;
        }

        /// <summary>Whether a point is inside, at least <paramref name="margin"/> in from every edge.</summary>
        public static bool Inside(Vector2 point, Vector2 centre, float radius, float margin = 0f)
        {
            Vector2 p = point - centre;
            float limit = radius * Apothem - margin;

            for (int j = 0; j < Sides; j++)
            {
                if (Vector2.Dot(p, normals[j]) > limit)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Where an upright line at <paramref name="x"/> crosses it: its top and bottom (world y). False if the
        /// line misses it. Each edge bounds the line from above or below, the level ones not at all.
        /// </summary>
        public static bool Upright(float x, Vector2 centre, float radius, out float top, out float bottom)
        {
            float dx = x - centre.X;
            float limit = radius * Apothem;
            float upper = float.MinValue;
            float lower = float.MaxValue;

            for (int j = 0; j < Sides; j++)
            {
                Vector2 n = normals[j];
                float room = limit - n.X * dx;

                if (Math.Abs(n.Y) < 1e-4f)
                {
                    if (room < 0f)
                    {
                        top = bottom = centre.Y;
                        return false;
                    }

                    continue;
                }

                float y = room / n.Y;

                if (n.Y > 0f)
                    lower = Math.Min(lower, y);
                else
                    upper = Math.Max(upper, y);
            }

            top = centre.Y + upper;
            bottom = centre.Y + lower;
            return upper < lower;
        }

        /// <summary>Where a level line at <paramref name="y"/> crosses it: its left and right (world x). False if it misses.</summary>
        public static bool Level(float y, Vector2 centre, float radius, out float left, out float right)
        {
            float dy = y - centre.Y;
            float limit = radius * Apothem;
            float leftmost = float.MinValue;
            float rightmost = float.MaxValue;

            for (int j = 0; j < Sides; j++)
            {
                Vector2 n = normals[j];
                float room = limit - n.Y * dy;

                if (Math.Abs(n.X) < 1e-4f)
                {
                    if (room < 0f)
                    {
                        left = right = centre.X;
                        return false;
                    }

                    continue;
                }

                float x = room / n.X;

                if (n.X > 0f)
                    rightmost = Math.Min(rightmost, x);
                else
                    leftmost = Math.Max(leftmost, x);
            }

            left = centre.X + leftmost;
            right = centre.X + rightmost;
            return leftmost < rightmost;
        }

        /// <summary>How far a point is past the decagon's furthest edge: below 0 inside, by as much as it is in from the nearest edge.</summary>
        public static float Past(Vector2 point, Vector2 centre, float radius)
        {
            Vector2 p = point - centre;
            float limit = radius * Apothem;
            float most = float.MinValue;

            for (int j = 0; j < Sides; j++)
                most = Math.Max(most, Vector2.Dot(p, normals[j]) - limit);

            return most;
        }

        /// <summary>
        /// How far a line from a point inside goes the way <paramref name="dir"/> (a unit way) before it meets an edge, and
        /// which edge (the shape is convex: the nearest of the edges it is going toward). 0 for a point already outside.
        /// </summary>
        public static float Reach(Vector2 from, Vector2 dir, Vector2 centre, float radius, out int edge)
        {
            Vector2 p = from - centre;
            float limit = radius * Apothem;
            float best = float.MaxValue;
            edge = 0;

            for (int j = 0; j < Sides; j++)
            {
                float going = Vector2.Dot(dir, normals[j]);

                if (going <= 1e-5f)
                    continue;

                float d = (limit - Vector2.Dot(p, normals[j])) / going;

                if (d < best)
                {
                    best = d;
                    edge = j;
                }
            }

            return best == float.MaxValue ? 0f : Math.Max(0f, best);
        }

        /// <summary>
        /// A box's way this tick, bounced off any edge it would go past: its speed into that edge turned back, this share of
        /// it kept (a body thrown against the walls). True if it met one.
        /// </summary>
        public static Vector2 Bounce(Vector2 middle, Vector2 half, Vector2 velocity, Vector2 centre, float radius, float keep, out bool bounced)
        {
            Vector2 p = middle - centre;
            float limit = radius * Apothem;
            bounced = false;

            for (int j = 0; j < Sides; j++)
            {
                Vector2 n = normals[j];
                float room = limit - Extent(n, half) - Vector2.Dot(p, n);
                float going = Vector2.Dot(velocity, n);

                if (going <= 0f || going <= room)
                    continue;

                velocity -= (1f + keep) * going * n;
                bounced = true;
            }

            return velocity;
        }

        // ---- a body held inside ---------------------------------------------------------------------------
        //
        // A body is a box (a player's hitbox): its middle and half its size. All of it is inside when, along every
        // edge's way out, its middle plus its half-width that way is no further out than the edge.

        /// <summary>An edge whose way out points this far down or more is a floor (the bottom edge, and the two either side of it, 36° off level).</summary>
        public const float FloorNormal = 0.55f;

        /// <summary>How far a box reaches out past its middle along a way: half of it across that way.</summary>
        private static float Extent(Vector2 n, Vector2 half) => Math.Abs(n.X) * half.X + Math.Abs(n.Y) * half.Y;

        /// <summary>How far the furthest of a box is past the furthest edge: 0 or below when all of it is inside.</summary>
        public static float BoxPast(Vector2 middle, Vector2 half, Vector2 centre, float radius)
        {
            Vector2 p = middle - centre;
            float limit = radius * Apothem;
            float most = float.MinValue;

            for (int j = 0; j < Sides; j++)
                most = Math.Max(most, Vector2.Dot(p, normals[j]) + Extent(normals[j], half) - limit);

            return most;
        }

        /// <summary>
        /// A box's way this tick, held inside: whatever of <paramref name="velocity"/> would carry any of it past an
        /// edge is taken off. A wall or the roof takes it off along its way out (the box slides along it); a floor
        /// takes it off upright, as the ground does (it lands exactly where the floor is, walks up a slope, and stands
        /// still on one). <paramref name="stick"/>: it stood on the floor last tick and is not leaving it, so it keeps
        /// to it going down a slope too, rather than skipping off it. <paramref name="floored"/>: it rests on a floor.
        /// </summary>
        public static Vector2 HoldInside(Vector2 middle, Vector2 half, Vector2 velocity, Vector2 centre, float radius, bool stick, out bool floored)
        {
            Vector2 p = middle - centre;
            float limit = radius * Apothem;

            if (stick && velocity.Y >= 0f)
            {
                // Down to the floor it stands on, as far as its way along it takes it down (a slope's), and no further.
                float down = velocity.Y;

                for (int j = 0; j < Sides; j++)
                {
                    Vector2 n = normals[j];
                    float room = limit - Extent(n, half) - Vector2.Dot(p, n);

                    if (n.Y < FloorNormal || room > 1.5f)
                        continue;

                    float keep = (room - velocity.X * n.X) / n.Y;

                    if (keep > down && keep < 16f)
                        down = keep;
                }

                velocity.Y = down;
            }

            for (int pass = 0; pass < 3; pass++)
            {
                bool held = false;

                for (int j = 0; j < Sides; j++)
                {
                    Vector2 n = normals[j];

                    // How far it may still go out this way (below 0: it is already that far past).
                    float room = limit - Extent(n, half) - Vector2.Dot(p, n);
                    float going = Vector2.Dot(velocity, n);

                    if (going <= room)
                        continue;

                    if (n.Y >= FloorNormal)
                        velocity.Y -= (going - room) / n.Y;
                    else
                        velocity -= n * (going - room);

                    held = true;
                }

                if (!held)
                    break;
            }

            // On a floor: one of the lower edges right under it once it has moved.
            Vector2 after = p + velocity;
            floored = false;

            for (int j = 0; j < Sides; j++)
            {
                Vector2 n = normals[j];

                if (n.Y >= FloorNormal && limit - Extent(n, half) - Vector2.Dot(after, n) <= 1f)
                    floored = true;
            }

            return velocity;
        }

        /// <summary>A box put back inside where it is (each edge it is past moves it straight in), and whatever of the velocity was carrying it out taken away.</summary>
        public static Vector2 PushBoxInside(Vector2 middle, Vector2 half, Vector2 centre, float radius, ref Vector2 velocity)
        {
            Vector2 p = middle - centre;
            float limit = radius * Apothem;

            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;

                for (int j = 0; j < Sides; j++)
                {
                    Vector2 n = normals[j];
                    float past = Vector2.Dot(p, n) + Extent(n, half) - limit;

                    if (past <= 0f)
                        continue;

                    p -= n * past;
                    moved = true;

                    float outward = Vector2.Dot(velocity, n);

                    if (outward > 0f)
                        velocity -= n * outward;
                }

                if (!moved)
                    break;
            }

            return centre + p;
        }

        /// <summary>
        /// Where a box that jumped from <paramref name="from"/> (inside, or on an edge) to <paramref name="to"/>
        /// (outside) in one go is stopped: the furthest point along the way that keeps all of it inside (the shape is
        /// convex, so the way crosses the edge once). One standing on an edge reads a hair past it now and then (the sums
        /// are not exact to the last place): it is put back on the edge first, and the way searched from there.
        /// </summary>
        public static Vector2 StopAtWall(Vector2 from, Vector2 to, Vector2 half, Vector2 centre, float radius)
        {
            if (BoxPast(from, half, centre, radius) > 0f)
            {
                Vector2 none = Vector2.Zero;
                from = PushBoxInside(from, half, centre, radius, ref none);
            }

            float lo = 0f;
            float hi = 1f;

            for (int i = 0; i < 18; i++)
            {
                float mid = (lo + hi) * 0.5f;

                if (BoxPast(Vector2.Lerp(from, to, mid), half, centre, radius) <= 0f)
                    lo = mid;
                else
                    hi = mid;
            }

            return Vector2.Lerp(from, to, lo);
        }

        /// <summary>
        /// The point put back inside, <paramref name="margin"/> in from the edges, and whatever of the
        /// velocity was carrying it out taken away. Each edge it is past moves it straight in from that
        /// edge; a few times round settles a point past a corner as well (each push leaves it no further
        /// out past the next edge, the corners being 144°).
        /// </summary>
        public static Vector2 PushInside(Vector2 point, Vector2 centre, float radius, float margin, ref Vector2 velocity)
        {
            Vector2 p = point - centre;
            float limit = radius * Apothem - margin;

            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;

                for (int j = 0; j < Sides; j++)
                {
                    Vector2 n = normals[j];
                    float past = Vector2.Dot(p, n) - limit;

                    if (past <= 0f)
                        continue;

                    p -= n * past;
                    moved = true;

                    float outward = Vector2.Dot(velocity, n);

                    if (outward > 0f)
                        velocity -= n * outward;
                }

                if (!moved)
                    break;
            }

            return centre + p;
        }
    }

    /// <summary>The shape of her cage (by request, "hexagon of light"): a hexagon lying flat, as the decagon lies.</summary>
    internal static class Hexagon
    {
        public const int Sides = 6;

        private static readonly Vector2[] corners = Polygon.Corners(Sides);
        private static readonly Vector2[] normals = Polygon.Normals(Sides);

        public static Vector2 Corner(Vector2 centre, float radius, int k) => centre + corners[Polygon.Wrap(k, Sides)] * radius;

        public static Vector2 Normal(int edge) => normals[Polygon.Wrap(edge, Sides)];
    }

    /// <summary>
    /// The arena as it stands this tick, for everything that is not her: the players' walls and
    /// floor, her favour, and how it looks. She asks for it every
    /// tick it should stand, on every machine (her AI runs on all of them), and it is gone a tick
    /// after she stops asking: a fight that ends any way at all (a death, a despawn, the world
    /// unloading) cannot leave anybody walled in. The same shape as BossArena, which is a box; this
    /// is a decagon.
    /// </summary>
    public static class EmpressArena
    {
        /// <summary>
        /// From the middle to a corner, by request: 125 tiles in a normal world, 115 in an expert one, 100 in a
        /// master one, and 15 fewer in a Legends world (it was 150 in every world). The world's own, so it is the
        /// same on every machine.
        /// </summary>
        public static float Radius => 16f * ((Main.masterMode ? 100f : Main.expertMode ? 115f : 125f) - (PADWorld.Legends ? 15f : 0f));

        /// <summary>
        /// How far from the middle she reaches a player who is not in her scene: what lies outside (to its
        /// fringe's end, EmpressVoidSystem) and her sky. Further off, the world is as it was.
        /// </summary>
        public static float Reach => Radius + 3200f;

        /// <summary>Ticks it outlives the last tick it was asked for: the one between her update and the players' next.</summary>
        private const int Slack = 2;

        private static int asked;

        public static bool Up => asked > 0;
        public static Vector2 Centre { get; private set; }
        public static float Size { get; private set; } = Radius;

        /// <summary>Its walls hold: solid, and a floor along the bottom, for whoever is in it (EmpressArenaPlayer).</summary>
        public static bool Holds { get; private set; }

        /// <summary>Whoever is in it, or on the way to it, has her favour (Favor of an Empress): flight without end.</summary>
        public static bool Grants { get; private set; }

        // ---- how it looks, this tick ---------------------------------------------------------------
        //
        // Said by her scenes alongside Keep; whole when nobody says otherwise, so the fight between
        // her scenes needs say nothing.

        private static int lookAsked;
        private static float outline = 1f;
        private static float veil = 1f;
        private static float flare;

        /// <summary>How much of the border's line is drawn, 0 to 1: grown out of the stars as it forms.</summary>
        public static float Outline => lookAsked > 0 ? outline : 1f;

        /// <summary>How much of what lies outside is there, 0 to 1.</summary>
        public static float Veil => lookAsked > 0 ? veil : 1f;

        /// <summary>The whole border thrown white, 0 to 1: as it forms, and as it goes.</summary>
        public static float Flare => lookAsked > 0 ? flare : 0f;

        /// <summary>The arena, this tick. Asked by her every tick it should stand.</summary>
        public static void Keep(Vector2 centre, float size, bool holds, bool grants)
        {
            Centre = centre;
            Size = size;
            Holds = holds;
            Grants = grants;
            asked = Slack;
        }

        /// <summary>How it looks, this tick, where it is not whole: the line grown so far, what is outside so far, and the flare.</summary>
        public static void Appear(float line, float outside, float white)
        {
            outline = MathHelper.Clamp(line, 0f, 1f);
            veil = MathHelper.Clamp(outside, 0f, 1f);
            flare = MathHelper.Clamp(white, 0f, 1f);
            lookAsked = Slack;
        }

        public static bool Contains(Vector2 point, float margin = 0f) => Up && Decagon.Inside(point, Centre, Size, margin);

        // ---- her cage of curtains (her second half in Legends: EmpressOfLight.Cage) ---------------------------------
        //
        // A small decagon of her curtains about one player, drifting through the arena: its walls hold that player in as
        // the arena's do (EmpressArenaPlayer), and hurt whoever touches them (EmpressHitbox). Asked for every tick, as the
        // arena is, and gone a tick after nobody asks.

        private static int cageAsked;

        /// <summary>The cage stands this tick: where, how big (middle to a corner), and whose (the player it holds).</summary>
        public static bool CageUp => cageAsked > 0;
        public static Vector2 CageCentre { get; private set; }
        public static float CageSize { get; private set; }
        public static int CageFor { get; private set; } = -1;

        /// <summary>The cage, this tick, about <paramref name="who"/> (a player's index).</summary>
        public static void Cage(Vector2 centre, float size, int who)
        {
            CageCentre = centre;
            CageSize = size;
            CageFor = who;
            cageAsked = Slack;
        }

        /// <summary>First thing in the update, before the players: an arena nobody asked for last tick goes.</summary>
        internal static void Tick()
        {
            if (asked > 0)
                asked--;

            if (lookAsked > 0)
                lookAsked--;

            if (cageAsked > 0)
                cageAsked--;
        }

        internal static void Reset()
        {
            asked = 0;
            lookAsked = 0;
            cageAsked = 0;
            CageFor = -1;
            Holds = false;
            Grants = false;
        }
    }

    /// <summary>
    /// A player's side of her: what her scenes do to them (held in the cage and carried up with it,
    /// kept hovering where they are), the walls and floor of the arena once it stands, and her favour
    /// while it lasts.
    ///
    /// All of it is for this machine's own player: a player's machine moves them, and the game tells
    /// the others where they went.
    /// </summary>
    public class EmpressArenaPlayer : ModPlayer
    {
        // ---- what a scene asks of the player ---------------------------------------------------------------
        //
        // Asked for every tick by her scene, as Cinematic is; gone a tick after it stops asking, so a scene
        // that ends any way at all lets go of them.

        internal enum Held : byte
        {
            None,

            /// <summary>Their middle is put where the scene says, and they do not move: the cage has them.</summary>
            Pin,

            /// <summary>Whatever speed they had runs out, and they hang where they are: her favour holds them up.</summary>
            Hover,
        }

        private static Held asked;
        private static Vector2 askedAt;
        private static int askedFor;

        /// <summary>This machine's player is held with their middle here, this tick.</summary>
        internal static void Pin(Vector2 centre)
        {
            asked = Held.Pin;
            askedAt = centre;
            askedFor = 2;
        }

        /// <summary>This machine's player hangs where they are, this tick.</summary>
        internal static void Hover()
        {
            asked = Held.Hover;
            askedFor = 2;
        }

        internal static void TickHeld()
        {
            if (askedFor > 0 && --askedFor == 0)
                asked = Held.None;
        }

        internal static void ResetHeld()
        {
            asked = Held.None;
            askedFor = 0;
        }

        // ---- the walls -----------------------------------------------------------------------------------
        //
        // By request ("the border doesnt teleport the player back, make it impenetratable even with dash, the bottom
        // can be stood on"): for whoever is in it, the border is solid. Before the game moves them (PreUpdateMovement),
        // whatever of their speed would carry any of them through an edge is taken off (Decagon.HoldInside): they run,
        // fly, dash or are thrown into it and stop at it, sliding along it. Its lower edges (the bottom one and the two
        // either side of it) are a floor: they land on it, stand, walk and jump from it as from the ground, unhurt by
        // the fall. Anything that puts them past it in one go is undone after (PostUpdate): a short jump (a rod,
        // another mod's blink) stopped at the wall on its way out, a long one (a mirror, a potion) not made at all.
        // Nobody in it is dazzled any more; nobody gets out.

        /// <summary>
        /// Further past a wall in one tick than this is not flying, running or being thrown: it is a
        /// teleport (a mirror, a rod, a potion), and is stopped at the wall on its way out (or, going far,
        /// not made: FarOff). Nothing that moves a player a tick at a time covers this much ground in one
        /// (BossArenaPlayer.LetGo).
        /// </summary>
        public const float LetGo = 320f;

        /// <summary>
        /// A jump out that would land further from her middle than this many times the arena's reach (home, or wherever a
        /// potion sends one) is not stopped at the wall on its way but not made: stopped at the wall, it would carry them
        /// across her arena toward wherever they were going.
        /// </summary>
        public const float FarOff = 2f;

        /// <summary>The walls held them up from below this tick: they stand on her floor.</summary>
        private bool floored;

        /// <summary>
        /// Her favour reaches this far past the border too, so that one who fell, or died and came back,
        /// can fly up to her again: the arena stands in the sky. From the middle, in world pixels.
        /// </summary>
        public static float FavourReach => EmpressArena.Radius + 1800f;

        /// <summary>
        /// How long her favour is given for while the arena stands, refreshed every tick; and, once it is
        /// gone, the most the way down it leaves (a slow fall, no harm from the landing) lasts.
        /// </summary>
        public const int FavourTicks = 60 * 60 * 30;
        public const int LandingTicks = 60 * 60;

        /// <summary>In the arena: inside it when its walls came up, or come in since.</summary>
        public bool Member { get; private set; }

        private bool wasInside;
        private Vector2 lastCentre;
        private Vector2 hoverVelocity;
        private Held lastHeld;

        /// <summary>
        /// Ticks left of the way down her favour leaves one it ends on in the air: falling slowly (down held,
        /// as fast as ever) and unharmed by the landing, until they stand on something.
        /// </summary>
        private int landing;

        public override void PreUpdateMovement()
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead)
                return;

            Held now = askedFor > 0 ? asked : Held.None;

            switch (now)
            {
                case Held.Pin:
                    Player.velocity = Vector2.Zero;
                    Player.Center = askedAt;
                    Player.fallStart = (int)(Player.position.Y / 16f);
                    break;

                case Held.Hover:
                    // The speed they had, run out on its own clock rather than the game's: gravity is
                    // added to their speed every tick before this, and a hold that only slowed that
                    // would sink.
                    if (lastHeld != Held.Hover)
                        hoverVelocity = lastHeld == Held.Pin ? Vector2.Zero : Player.velocity;

                    hoverVelocity *= 0.84f;

                    if (hoverVelocity.LengthSquared() < 0.0025f)
                        hoverVelocity = Vector2.Zero;

                    Player.velocity = hoverVelocity;
                    Player.fallStart = (int)(Player.position.Y / 16f);
                    break;
            }

            lastHeld = now;

            // The walls: nothing of them goes through, whatever its speed.
            bool stood = floored;
            floored = false;

            // Thrown limp against them (her cast, EmpressHeld): bounced off, not stopped.
            EmpressHeld limp = Player.GetModPlayer<EmpressHeld>();

            if (Walled && limp.Ragdolled)
            {
                Player.velocity = Decagon.Bounce(Player.Center, Player.Size * 0.5f, Player.velocity, EmpressArena.Centre, EmpressArena.Size, 0.55f, out bool bounced);

                if (bounced)
                    limp.Bounced(Player.Center);
            }

            if (Walled && now != Held.Pin)
                Player.velocity = Decagon.HoldInside(Player.Center, Player.Size * 0.5f, Player.velocity, EmpressArena.Centre, EmpressArena.Size, stood, out floored);

            // Her cage, inside the arena's walls, the same: nothing of it goes through. (Its floor is stood on too.)
            if (Caged && now != Held.Pin)
            {
                Player.velocity = Decagon.HoldInside(Player.Center, Player.Size * 0.5f, Player.velocity, EmpressArena.CageCentre, EmpressArena.CageSize, stood, out bool cageFloor);
                floored |= cageFloor;
            }
        }

        /// <summary>Her cage of curtains holds this machine's player: it stands about them, and the arena's walls do.</summary>
        internal bool Caged => Walled && EmpressArena.CageUp && EmpressArena.CageFor == Player.whoAmI;

        /// <summary>The walls hold this machine's player: in the arena, its walls up, alive, and not in her hand (her grab has them).</summary>
        internal bool Walled => Member && EmpressArena.Up && EmpressArena.Holds && !Player.dead && !Player.GetModPlayer<EmpressHeld>().Held;

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            Favour();

            if (!EmpressArena.Up || Player.dead)
            {
                Member = false;
                wasInside = false;
                lastCentre = Player.Center;
                return;
            }

            Vector2 centre = Player.Center;
            bool inside = Decagon.Inside(centre, EmpressArena.Centre, EmpressArena.Size);

            // One counts as in it once its walls are up and they are inside them. (Before that the
            // arena is still forming round them, and they are in her cage.)
            if (!Member && EmpressArena.Holds)
                Member = inside;

            if (Walled)
            {
                Vector2 half = Player.Size * 0.5f;

                if (Decagon.BoxPast(centre, half, EmpressArena.Centre, EmpressArena.Size) > 0.5f)
                {
                    Vector2 velocity = Player.velocity;
                    float moved = Vector2.Distance(centre, lastCentre);

                    if (wasInside && moved > LetGo)
                    {
                        // A jump out in one go: a short one (a rod, a blink) stopped at the wall on its way, still; a
                        // long one not made at all, they stay where they stood. (The game's own ways home and away
                        // cannot be used in here at all: EmpressArenaItems. This is for anything else.)
                        Vector2 to = Vector2.Distance(centre, EmpressArena.Centre) > EmpressArena.Size * FarOff ? lastCentre : centre;
                        Player.Center = Decagon.StopAtWall(lastCentre, to, half, EmpressArena.Centre, EmpressArena.Size);
                        velocity = Vector2.Zero;
                    }
                    else
                    {
                        // Whatever else got any of them past it (the wall come in on them, a push the walls did not
                        // see coming): put back in where they are, the speed that took them out taken away.
                        Player.Center = Decagon.PushBoxInside(centre, half, EmpressArena.Centre, EmpressArena.Size, ref velocity);
                    }

                    Player.velocity = velocity;
                }

                // The cage, moving, comes against them: put back in where they are (its walls hurt them for it).
                if (Caged && Decagon.BoxPast(Player.Center, half, EmpressArena.CageCentre, EmpressArena.CageSize) > 0.5f)
                {
                    Vector2 velocity = Player.velocity;
                    Player.Center = Decagon.PushBoxInside(Player.Center, half, EmpressArena.CageCentre, EmpressArena.CageSize, ref velocity);
                    Player.velocity = velocity;
                }

                inside = true;

                // On her floor as on the ground: no speed up or down (the game lets a jump be made from that, and
                // walks a player as on the ground), and no fall counted to it.
                if (floored)
                {
                    Player.velocity.Y = 0f;
                    Player.fallStart = (int)(Player.position.Y / 16f);
                }
            }

            wasInside = inside;
            lastCentre = Player.Center;
        }

        /// <summary>
        /// Her favour, for this machine's player: given while the arena stands and they are in it or
        /// near enough to fly to it; and, by request ("lasts until either player die or player beats
        /// boss"), gone the moment the arena is: as her border goes at her death, or with her however
        /// else she leaves. One it ends on in the air is let down gently (landing): nobody falls out of
        /// her sky to their death, and nobody keeps her flight once she is beaten.
        /// </summary>
        private void Favour()
        {
            int type = ModContent.BuffType<FavorOfAnEmpress>();

            bool near = Member || Vector2.Distance(Player.Center, EmpressArena.Centre) <= FavourReach;
            bool given = EmpressArena.Up && EmpressArena.Grants && near && !Player.dead;

            if (given)
            {
                int index = Player.FindBuffIndex(type);

                if (index < 0)
                    Player.AddBuff(type, FavourTicks);
                else
                    Player.buffTime[index] = Math.Max(Player.buffTime[index], FavourTicks);

                landing = 0;
                return;
            }

            int left = Player.FindBuffIndex(type);

            if (left >= 0)
            {
                Player.DelBuff(left);

                if (!Player.dead && !OnGround())
                    landing = LandingTicks;
            }

            if (landing > 0 && (Player.dead || OnGround()))
                landing = 0;
            else if (landing > 0)
                landing--;
        }

        /// <summary>The way down her favour leaves, while it lasts: set after the game has cleared it, before anybody moves.</summary>
        public override void PostUpdateMiscEffects()
        {
            if (Player.whoAmI != Main.myPlayer || landing <= 0)
                return;

            // A slow fall (the game's own, as a Featherfall Potion gives it: down held falls as fast as
            // ever), and no harm from the landing however it is come to.
            Player.slowFall = true;
            Player.noFallDmg = true;
        }

        /// <summary>Stood on something, and not going up or down.</summary>
        private bool OnGround() => Player.velocity.Y == 0f && Standing();

        /// <summary>Something under their feet that holds them up, solid or a platform.</summary>
        private bool Standing()
        {
            Vector2 feet = new Vector2(Player.position.X, Player.position.Y + Player.height);
            return Collision.SolidCollision(feet, Player.width, 4, true);
        }

        public override void OnRespawn()
        {
            Member = false;
            wasInside = false;
            landing = 0;
        }
    }

    /// <summary>
    /// The game's ways home and away, not to be used inside her walls: the mirrors, the phones and their home, spawn, sea
    /// and underworld, the conches, and the Recall, Teleportation and Return potions. Each would take whoever used it
    /// out in one go, and the walls would only put them back where they stood (EmpressArenaPlayer): the potion gone for
    /// nothing, the world gone black a moment, and a second of the game's untouchability after a mirror's trip home
    /// kept (Player.Spawn gives it, and a mirror does not take it back), to be had again at every use. So they are not
    /// used at all, and a word says why. The Rods of Discord and Harmony are left as they are: a short jump, stopped
    /// at the wall.
    /// </summary>
    public class EmpressArenaItems : GlobalItem
    {
        /// <summary>Ticks between two tellings.</summary>
        private const int TellEvery = 60;

        /// <summary>The word (a refusal, of the thing: the mod's voice).</summary>
        private const string Refusal = "It finds no way out of her light.";

        private static uint toldAt;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => Away(entity.type);

        /// <summary>The game's items that take their user home, or somewhere far, in one go.</summary>
        public static bool Away(int type) => type == ItemID.MagicMirror || type == ItemID.IceMirror || type == ItemID.CellPhone
            || type == ItemID.Shellphone || type == ItemID.ShellphoneSpawn || type == ItemID.ShellphoneOcean || type == ItemID.ShellphoneHell
            || type == ItemID.MagicConch || type == ItemID.DemonConch
            || type == ItemID.RecallPotion || type == ItemID.TeleportationPotion || type == ItemID.PotionOfReturn;

        public override bool CanUseItem(Item item, Player player)
        {
            if (!player.TryGetModPlayer(out EmpressArenaPlayer arena) || !arena.Walled)
                return true;

            if (player.whoAmI == Main.myPlayer && !Main.dedServ && Main.GameUpdateCount - toldAt >= TellEvery)
            {
                toldAt = Main.GameUpdateCount;
                CombatText.NewText(player.Hitbox, EmpressArt.Sky, Refusal);
            }

            return false;
        }
    }

    /// <summary>The arena and the scene's hold on the player, latched once a tick before anybody moves, and let go of with the world.</summary>
    public class EmpressArenaSystem : ModSystem
    {
        public override void PreUpdateEntities()
        {
            EmpressArena.Tick();
            EmpressArenaPlayer.TickHeld();
        }

        public override void OnWorldUnload()
        {
            EmpressArena.Reset();
            EmpressArenaPlayer.ResetHeld();
        }
    }
}
