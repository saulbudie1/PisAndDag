using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>Where the King's arrival is. None before it has begun, Done after.</summary>
    public enum KingRite : byte
    {
        None,

        /// <summary>The heralds are on their way: over the ground, and out of the sky.</summary>
        Gather,

        /// <summary>The camera goes to the pile. Anyone not in it yet is thrown in.</summary>
        Approach,

        /// <summary>The pile settles, then bounces as one, three times, higher each time.</summary>
        Stack,

        /// <summary>They are pulled into the middle of themselves.</summary>
        Fuse,

        /// <summary>He comes out of it, all at once, and wobbles into shape.</summary>
        Burst,

        /// <summary>He roars (Cinematic.Roar) and his name comes up.</summary>
        Roar,

        /// <summary>Held on him while the name settles. Then the fight, and the camera walks home.</summary>
        Hold,

        Done,
    }

    /// <summary>
    /// The parts of the King's arrival that both he and his heralds need: the ground,
    /// the throw, the pile, and a roll of the dice that comes up the same on every
    /// machine.
    ///
    /// THE GROUND. The heralds are not walked by tile collision: twenty slimes pathing
    /// over real terrain strand half of themselves behind the first hill. They are
    /// thrown instead, one hop at a time, and every hop is aimed at real ground: the
    /// surface is looked up where the hop will land, and the arc is made high enough to
    /// clear the highest ground between. So they go up and down with the land, over
    /// bumps and across dips, and never through the air in a straight line.
    ///
    /// THE PILE. Twenty places, stacked the way slimes would stack: a row of six on the
    /// ground, five on those, and so up to two on top. Given out bottom row first,
    /// middle outward, in the order the heralds arrive, so the pile grows up from the
    /// ground.
    /// </summary>
    public static class SlimeRite
    {
        // ============================================================
        // THE THROW
        // ============================================================

        /// <summary>Gravity on a thrown herald, pixels a tick a tick. A slime's own, near enough.</summary>
        public const float Gravity = 0.3f;

        /// <summary>The fastest anything falls.</summary>
        public const float MaxFall = 13f;

        /// <summary>
        /// The velocity that throws something from one point to another over a top.
        /// Both points and the top are world Y, down positive; the top has to be above
        /// both. Worked from the rise and the fall under Gravity, and the fall capped at
        /// MaxFall as the heralds' is, so it lands where it was aimed rather than
        /// wherever a guessed speed happens to put it.
        /// </summary>
        public static Vector2 Launch(Vector2 from, Vector2 to, float top, out float ticks)
        {
            top = Math.Min(top, Math.Min(from.Y, to.Y) - 2f);

            float rise = from.Y - top;
            float drop = to.Y - top;

            float up = (float)Math.Sqrt(2f * rise / Gravity);

            ticks = Math.Max(1f, up + FallTicks(drop));

            return new Vector2((to.X - from.X) / ticks, -Gravity * up);
        }

        /// <summary>Ticks to fall this far from standing still, speeding up under Gravity until MaxFall and then no faster.</summary>
        public static float FallTicks(float drop)
        {
            if (drop <= 0f)
                return 0f;

            // How far it falls before it stops getting faster.
            float capped = MaxFall * MaxFall / (2f * Gravity);

            if (drop <= capped)
                return (float)Math.Sqrt(2f * drop / Gravity);

            return MaxFall / Gravity + (drop - capped) / MaxFall;
        }

        // ============================================================
        // THE GROUND
        // ============================================================

        /// <summary>
        /// The top of the ground in this column, near this height: the first thing a
        /// slime could land on, looking down from a little above. Water counts; a slime
        /// sits on water as well as it sits on anything.
        ///
        /// Started a way above the height given, and if that is inside a hill, moved up
        /// out of it first, so a hop toward a rise finds the top of the rise rather than
        /// a cave under it. <paramref name="water"/> false turns the water away: for
        /// somewhere the King has to stand once it is all over.
        /// </summary>
        public static bool Ground(float worldX, float nearY, out float groundY, bool water = true)
        {
            groundY = nearY;

            int x = (int)(worldX / 16f);

            if (x < 2 || x >= Main.maxTilesX - 2)
                return false;

            int near = (int)(nearY / 16f);
            int y = Math.Max(3, near - 14);
            int last = Math.Min(Main.maxTilesY - 4, near + 24);

            // Out of the hill first, if the start is in one.
            int climb = 0;

            while (y > 3 && Blocks(x, y) && climb < 30)
            {
                y--;
                climb++;
            }

            for (; y <= last; y++)
            {
                Tile tile = Framing.GetTileSafely(x, y);

                if (tile.LiquidAmount > 0 && Framing.GetTileSafely(x, y - 1).LiquidAmount == 0 && !Blocks(x, y - 1))
                {
                    // Water where only ground will do: no ground here, not a floor further
                    // down under the water.
                    if (!water)
                        return false;

                    // Liquid fills a tile from the bottom.
                    groundY = y * 16f + (255 - tile.LiquidAmount) / 255f * 16f;
                    return true;
                }

                if (Floor(x, y) && !Blocks(x, y - 1))
                {
                    groundY = y * 16f;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The highest ground under a hop, from one x to another, followed column by
        /// column so a long slope is traced rather than guessed from its ends.
        /// </summary>
        public static float Highest(float fromX, float toX, float nearY)
        {
            float highest = nearY;
            float follow = nearY;

            int a = (int)(Math.Min(fromX, toX) / 16f);
            int b = (int)(Math.Max(fromX, toX) / 16f);

            for (int x = a; x <= b; x++)
            {
                if (!Ground(x * 16f + 8f, follow, out float y))
                    continue;

                follow = y;
                highest = Math.Min(highest, y);
            }

            return highest;
        }

        /// <summary>Something to stand on: solid, or a platform.</summary>
        public static bool Floor(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        /// <summary>Something in the way: solid, and not a platform.</summary>
        public static bool Blocks(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>Nothing solid straight down this column, from one height to another. Open sky to fall through.</summary>
        public static bool Open(float worldX, float fromY, float toY)
        {
            int x = (int)(worldX / 16f);
            int a = Math.Max(3, (int)(fromY / 16f));
            int b = Math.Min(Main.maxTilesY - 4, (int)(toY / 16f));

            for (int y = a; y <= b; y++)
            {
                if (Blocks(x, y))
                    return false;
            }

            return true;
        }

        // ============================================================
        // THE PILE
        // ============================================================

        /// <summary>Slimes in each row of the pile, from the ground up.</summary>
        private static readonly int[] Rows = { 6, 5, 4, 3, 2 };

        /// <summary>Places in the pile.</summary>
        public const int Slots = 20;

        /// <summary>How far apart they sit, side to side and one row on the next. Close enough to squash into each other.</summary>
        public const float StepX = 24f;
        public const float StepY = 14f;

        /// <summary>The row a place is in, 0 on the ground.</summary>
        public static int Row(int slot)
        {
            slot = Math.Clamp(slot, 0, Slots - 1);

            for (int row = 0; row < Rows.Length; row++)
            {
                if (slot < Rows[row])
                    return row;

                slot -= Rows[row];
            }

            return Rows.Length - 1;
        }

        /// <summary>
        /// Where the slime in this place has its feet, bottom centre. Each row is filled
        /// from the middle outward, alternating sides, so the pile grows as a pile.
        /// </summary>
        public static Vector2 Place(Vector2 altar, int slot)
        {
            slot = Math.Clamp(slot, 0, Slots - 1);

            int row = 0;
            int index = slot;

            while (row < Rows.Length && index >= Rows[row])
            {
                index -= Rows[row];
                row++;
            }

            row = Math.Min(row, Rows.Length - 1);

            int count = Rows[row];

            // Middle outward: 0 goes just left of centre, 1 just right, 2 further left...
            int step = index / 2 + 1;
            float centred = count % 2 == 0
                ? (index % 2 == 0 ? -(step - 0.5f) : step - 0.5f)
                : (index == 0 ? 0f : (index % 2 == 1 ? -(index + 1) / 2 : index / 2));

            return new Vector2(altar.X + centred * StepX, altar.Y - row * StepY);
        }

        /// <summary>The top of the pile, with a slime on it.</summary>
        public static float Top(Vector2 altar) => altar.Y - (Rows.Length - 1) * StepY - 26f;

        /// <summary>The middle of the pile, where they all go in the end.</summary>
        public static Vector2 Core(Vector2 altar) => new Vector2(altar.X, altar.Y - StepY * 2f - 10f);

        /// <summary>How wide the pile is.</summary>
        public const float Width = 6 * StepX + 12f;

        /// <summary>Rows in the pile.</summary>
        public static int RowCount => Rows.Length;

        // ============================================================
        // THE STACK: settle, three bounces as one, a crouch
        // ============================================================
        //
        // Every slime in the pile runs the same bounce, each row a little behind the
        // row under it, so the push goes up through the pile the way it would through
        // anything soft: the bottom leaves the ground first and the top last. Each
        // bounce is a squash on the ground, a hop, and the landing is the next squash.
        // Higher each time. The last landing does not let go: it stays down, crouched,
        // for the Fuse.
        //
        //     SETTLE     24   sat, breathing
        //     BOUNCE x3  22   7 on the ground, 15 in the air
        //     ROW LAG     2   per row, so 8 for the top of a five-row pile
        //     CROUCH      8   down and held
        //                106

        public const int StackSettle = 24;
        public const int BounceTicks = 22;
        public const int BounceGround = 7;
        public const int RowLag = 2;
        public const int Bounces = 3;
        public const int Crouch = 8;

        /// <summary>The whole Stack. The top row is four rows behind the bottom one.</summary>
        public const int StackTicks = StackSettle + Bounces * BounceTicks + 4 * RowLag + Crouch;

        /// <summary>How high each bounce goes, and how hard the pile squashes on the ground before it.</summary>
        private static readonly float[] BounceHeight = { 10f, 17f, 26f };
        private static readonly float[] BounceSquash = { 0.12f, 0.17f, 0.23f };

        /// <summary>How far down the crouch at the end goes.</summary>
        private const float CrouchDepth = 0.3f;

        /// <summary>A slime's squash, crouched at the end of the Stack: where the Fuse takes it from.</summary>
        public static Vector2 Crouched => new Vector2(1f + CrouchDepth, 1f - CrouchDepth * 0.85f);

        /// <summary>
        /// One slime's bounce on this tick of the Stack: how far off its place it is, and
        /// its squash (x wider, y shorter when squashed). Pure, so every machine and the
        /// King's own sound cues agree on it.
        /// </summary>
        public static void Bounce(int t, int row, out float lift, out Vector2 squash)
        {
            lift = 0f;
            squash = Vector2.One;

            int local = t - StackSettle - row * RowLag;

            if (local < 0)
            {
                // Sat, breathing, each row out of step with the next.
                float breath = 0.025f * (float)Math.Sin(t * 0.2f + row * 0.9f);
                squash = new Vector2(1f + breath, 1f - breath);
                return;
            }

            int bounce = local / BounceTicks;

            if (bounce >= Bounces)
            {
                // The last landing, and it stays down.
                int since = local - Bounces * BounceTicks;
                float down = Math.Min(1f, (since + 1) / 4f);
                float shiver = 0.015f * (float)Math.Sin(t * 1.3f + row);

                squash = Vector2.Lerp(Vector2.One, Crouched, down) + new Vector2(shiver, -shiver);
                return;
            }

            int tick = local % BounceTicks;

            if (tick < BounceGround)
            {
                // On the ground: squashed, most at the middle of the contact.
                float q = BounceSquash[bounce] * (float)Math.Sin(MathHelper.Pi * (tick + 1) / (BounceGround + 1));
                squash = new Vector2(1f + q, 1f - q);
                return;
            }

            // In the air: up and down on a parabola, drawn out when fast and round at the top.
            int air = BounceTicks - BounceGround;
            float u = (tick - BounceGround + 1) / (float)(air + 1);
            float height = BounceHeight[bounce];

            lift = height * 4f * u * (1f - u);

            float stretch = 0.14f * Math.Abs(1f - 2f * u) * (height / BounceHeight[Bounces - 1]);
            squash = new Vector2(1f - stretch * 0.6f, 1f + stretch);
        }

        /// <summary>
        /// Which bounce the bottom row lands from on this tick, 1 to 3, or 0 if none: the
        /// moment the King's sound and shake go with.
        /// </summary>
        public static int Landing(int t)
        {
            int local = t - StackSettle;

            if (local <= 0 || local % BounceTicks != 0)
                return 0;

            int landed = local / BounceTicks;

            return landed >= 1 && landed <= Bounces ? landed : 0;
        }

        // ============================================================
        // THE FUSE: into the middle
        // ============================================================

        /// <summary>Ticks for the pile to go into itself. The nearest start last, so they all arrive together.</summary>
        public const int FuseTicks = 14;

        /// <summary>The most any one of them waits before it goes.</summary>
        public const int FuseStagger = 3;

        /// <summary>After the last is in: the core, alone, a moment, before he comes out of it.</summary>
        public const int FuseHold = 10;

        /// <summary>
        /// How far into the middle one slime is on this tick of the Fuse, 0 to 1, from
        /// how far out it started.
        /// </summary>
        public static float Drawn(int t, float distance)
        {
            int wait = (int)Math.Round(FuseStagger * (1f - MathHelper.Clamp(distance / 90f, 0f, 1f)));
            return MathHelper.Clamp((t - wait) / (float)(FuseTicks - FuseStagger), 0f, 1f);
        }

        // ============================================================
        // THE DICE
        // ============================================================

        /// <summary>
        /// A number from 0 to 1 that depends only on its two arguments. Every machine
        /// works a herald's hops out for itself, and this is what makes them all work
        /// out the same hops.
        /// </summary>
        public static float Roll(int seed, int n)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)(n + 1) * 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;

                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>A roll between two numbers.</summary>
        public static float Roll(int seed, int n, float low, float high) => low + (high - low) * Roll(seed, n);
    }
}
