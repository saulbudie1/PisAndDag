using System;
using Microsoft.Xna.Framework;
#if !PREVIEW
using Terraria;
using Terraria.ID;
#endif

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>What the ground is like in one column, against where the donkey's feet are.</summary>
    public enum Ground : byte
    {
        /// <summary>He can walk there: level, a step up (one tile), or a drop he could jump back up.</summary>
        Level,

        /// <summary>Up two tiles or more, as high as he can jump.</summary>
        Rise,

        /// <summary>Higher than he can jump, or a roof too low to go under.</summary>
        Wall,

        /// <summary>Down further than he could jump back up, nothing under it at all, or lava.</summary>
        Edge,

        /// <summary>Water too deep to wade.</summary>
        Water,
    }

    /// <summary>One column of the ground ahead.</summary>
    public readonly struct Column
    {
        public readonly Ground Kind;

        /// <summary>Tiles up (positive) or down (negative) from his feet to where he would stand there.</summary>
        public readonly int Rise;

        /// <summary>Tiles of liquid standing on the floor there.</summary>
        public readonly int Depth;

        public Column(Ground kind, int rise, int depth = 0)
        {
            Kind = kind;
            Rise = rise;
            Depth = depth;
        }
    }

    /// <summary>The donkey's body this tick, as the game has it.</summary>
    public struct NavBody
    {
        public Vector2 Position;
        public int Width;
        public int Height;
        public Vector2 Velocity;
        public bool Wet;
        public bool OnGround;

        public float Bottom => Position.Y + Height;
        public float CenterX => Position.X + Width * 0.5f;
    }

    /// <summary>What the rider asks of the donkey's legs this tick.</summary>
    public struct NavAsk
    {
        /// <summary>The horizontal speed wanted, signed, in pixels a tick.</summary>
        public float Speed;

        /// <summary>How much of the way to it is gone each tick.</summary>
        public float Accel;

        /// <summary>He may jump at all.</summary>
        public bool MayJump;

        /// <summary>He stops short of a wall he cannot jump, instead of going into it at whatever speed he has.</summary>
        public bool Careful;

        /// <summary>He may walk into deep water and swim, instead of stopping on the bank.</summary>
        public bool MaySwim;
    }

    /// <summary>What lies ahead, for the rider.</summary>
    public enum NavReport : byte
    {
        Clear,
        Wall,
        Edge,
        Water,
    }

    /// <summary>What the ground makes of the rider's ask: the speed he really goes for, a jump, a stroke.</summary>
    public struct NavSteer
    {
        public float Speed;
        public float Accel;

        /// <summary>Above nothing: jump now, upward at this.</summary>
        public float Jump;

        /// <summary>In deep water: the up-and-down speed that keeps him at the surface.</summary>
        public bool Swimming;
        public float Lift;

        /// <summary>Backing off a ledge for a run at it.</summary>
        public bool Backing;

        public NavReport Ahead;

        /// <summary>From his front to what is reported, in pixels.</summary>
        public float Distance;
    }

    /// <summary>What the donkey keeps between ticks: the run-up. Sent with him.</summary>
    public struct NavMemory
    {
        public bool Backing;
        public int Dir;
        public float From;
        public float Room;
        public int Ticks;
        public int Tries;
    }

    /// <summary>
    /// Triunfo's eyes and good sense: what he makes of the ground in front of him and what he
    /// does about it, by request (the donkey that stood with his nose against a three-tile bank,
    /// and the one that ran into a lake and stayed on the bottom).
    ///
    /// Every tick he reads the columns in front of him, from his front hoof out sixteen tiles,
    /// against where his feet are, walking his eye along the ground as he would walk it (a step
    /// up, a drop down), and stops at the first thing that is not just ground:
    ///
    ///   A RISE he can jump (up to ten tiles): he jumps it, taking off when the arc gets him
    ///   over the top of it just as he gets to it, and over anything higher just behind it.
    ///   Stood against one with no speed to jump it from, he backs off six tiles, still facing
    ///   it, and has a run at it.
    ///   A WALL (higher than that, a living tree, a roof too low): asked to be careful he pulls
    ///   up short of it; charging, he goes into it (the rider pays: DonaQuinn's Crash).
    ///   An EDGE (a drop he could not jump back up, a pit, lava) or DEEP WATER he is not to swim:
    ///   he jumps it if the other side is near enough for the speed he has, and if it is not,
    ///   he pulls up on the brink.
    ///   In deep water he swims: up to the surface, along it, and out with a leap at the bank.
    ///
    /// The arithmetic is vanilla's own (NPC.UpdateNPC_Inner, 1.4.4): the jump is set in the AI,
    /// gravity (0.3, or 0.2 in water) is added after it, and then he moves; in water he moves
    /// half his speed (NPC.waterMovementSpeed).
    ///
    /// Pure: nothing here touches the NPC. The donkey hands over his body and the ask, and does
    /// what comes back. The tiles are read through <see cref="Solid"/>, <see cref="Floor"/> and
    /// <see cref="Liquid"/>, which a test harness swaps for a grid of its own (PREVIEW).
    /// </summary>
    public static class DonkeyNav
    {
        // ---- vanilla's numbers (NPC.UpdateNPC_UpdateGravity, NPC..ctor) ------------------------

        public const float Gravity = 0.3f;
        public const float WetGravity = 0.2f;
        public const float WetMove = 0.5f;

        // ---- his ------------------------------------------------------------------------------

        /// <summary>The highest he jumps, in tiles, and how far over the top of it he clears it by.</summary>
        public const int HighestJump = 10;
        public const float Clearance = 14f;

        /// <summary>How far ahead he reads the ground, in columns, and how far down he looks for a floor.</summary>
        public const int Ahead = 16;
        public const int Below = 30;

        /// <summary>Water this many tiles deep or more he swims; less he wades.</summary>
        public const int DeepWater = 3;

        /// <summary>Pulling up for a brink or a wall: pixels a tick off the speed, and how far short of it he stops.</summary>
        public const float EdgeBrake = 0.25f;
        public const float EdgeMargin = 6f;

        /// <summary>The run-up: how far he backs off, how fast, the longest it takes, and how many runs before he gives up on it.</summary>
        public const float RunUp = 96f;
        public const float BackSpeed = 2.4f;
        public const float BackAccel = 0.3f;
        public const int BackLongest = 70;
        public const int MostTries = 3;

        /// <summary>A gap narrower than his own length less this he simply walks across: one end of him is always on something.</summary>
        public const float Straddle = 12f;

        /// <summary>A jump across a gap comes down this far into the far side, and looks this many columns across for one.</summary>
        public const float LandIn = 10f;
        public const int Reach = 34;

        /// <summary>The widest gap he leaps, in tiles: past that, however fast he is going, he pulls up on the brink.</summary>
        public const int LongestLeap = 14;

        /// <summary>Swimming: his feet this far under the surface, this deep before he swims at all, this fast, and this much more leap to get out.</summary>
        public const float SwimDraft = 30f;
        public const float SwimDeep = 26f;
        public const float SwimSpeed = 3.2f;
        public const float WaterLeap = 26f;

        /// <summary>A little more jump than the least that does it, and how far over a top his feet must be going onto it: the game's collision is not quite the arc flown here.</summary>
        public const float JumpMargin = 0.5f;
        public const float Skim = 3f;

        /// <summary>Ticks of going on he must have after a jump comes down, before anything he would have to jump again.</summary>
        public const int LandRoom = 3;

        /// <summary>Slower than this into the side of something he only stops against it; faster, the jump has failed.</summary>
        public const float Gentle = 1.5f;

        /// <summary>Less room behind him than this to back off into and he leaps from where he stands instead.</summary>
        public const float LeastRoom = 40f;

        /// <summary>The fastest he can leave the ground, for the highest jump.</summary>
        public static readonly float MostJump = SpeedFor(HighestJump * 16f + Clearance, Gravity);

        // ============================================================
        // THE ARC
        // ============================================================

        /// <summary>How high an arc starting upward at <paramref name="speed"/> gets, in vanilla's order (gravity before the move).</summary>
        public static float Peak(float speed, float gravity)
        {
            float y = 0f;
            float v = speed;

            for (int t = 0; t < 400; t++)
            {
                v -= gravity;

                if (v <= 0f)
                    break;

                y += v;
            }

            return y;
        }

        /// <summary>The upward speed whose arc tops out at <paramref name="height"/>.</summary>
        public static float SpeedFor(float height, float gravity)
        {
            if (height <= 0f)
                return 0f;

            float v = (float)Math.Sqrt(2f * gravity * height);

            while (Peak(v, gravity) < height)
                v += 0.02f;

            return v;
        }

        // ============================================================
        // THE GROUND
        // ============================================================

#if PREVIEW
        public static Func<int, int, bool> SolidAt;
        public static Func<int, int, bool> FloorAt;
        public static Func<int, int, int> LiquidAt;
        public static Func<int, int, bool> LavaAt;

        public static bool Solid(int x, int y) => SolidAt(x, y);
        public static bool Floor(int x, int y) => FloorAt(x, y);
        public static int Liquid(int x, int y, out bool lava)
        {
            lava = LavaAt(x, y);
            return LiquidAt(x, y);
        }
#else
        /// <summary>A tile that stops him going sideways: solid, not a platform, not switched off. Off the world counts.</summary>
        public static bool Solid(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return true;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>A tile he can stand on: anything solid, platforms too.</summary>
        public static bool Floor(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return true;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        /// <summary>How much liquid is in this tile, 0 to 255, and whether it is lava.</summary>
        public static int Liquid(int x, int y, out bool lava)
        {
            lava = false;

            if (!WorldGen.InWorld(x, y))
                return 0;

            Tile tile = Main.tile[x, y];
            int amount = tile.LiquidAmount;
            lava = amount > 0 && tile.LiquidType == LiquidID.Lava;
            return amount;
        }
#endif

        /// <summary>No tile in this column from row <paramref name="top"/> to row <paramref name="bottom"/> stops him.</summary>
        private static bool Open(int x, int top, int bottom)
        {
            for (int y = top; y <= bottom; y++)
            {
                if (Solid(x, y))
                    return false;
            }

            return true;
        }

        /// <summary>The row his feet are in: the lowest row of him.</summary>
        public static int FootRow(float bottom) => (int)Math.Floor((bottom - 1f) / 16f);

        /// <summary>How many rows he is tall.</summary>
        public static int Rows(int height) => (height + 15) / 16;

        /// <summary>
        /// One column, against feet in row <paramref name="foot"/> and a body <paramref name="rows"/>
        /// tall: whether he can walk on there, jump up onto it, or neither, and how far up or down.
        /// </summary>
        public static Column Probe(int x, int foot, int rows)
        {
            // Something at his feet: how far up before there is room for all of him.
            if (Solid(x, foot))
            {
                for (int up = 1; up <= HighestJump + 1; up++)
                {
                    if (!Open(x, foot - up - rows + 1, foot - up))
                        continue;

                    if (up <= 1)
                        return new Column(Ground.Level, up);

                    return up <= HighestJump ? new Column(Ground.Rise, up) : new Column(Ground.Wall, up);
                }

                return new Column(Ground.Wall, HighestJump + 2);
            }

            // Open at his feet but not over them: a roof he does not fit under.
            if (!Open(x, foot - rows + 1, foot - 1))
                return new Column(Ground.Wall, 0);

            // Down to the floor, counting the liquid standing on it.
            int run = 0;
            bool lava = false;

            for (int y = foot - rows + 1; y <= foot + Below; y++)
            {
                if (y > foot && Floor(x, y))
                {
                    int down = y - foot - 1;

                    if (lava)
                        return new Column(Ground.Edge, -down, run);

                    if (run >= DeepWater)
                        return new Column(Ground.Water, -down, run);

                    return down <= HighestJump ? new Column(Ground.Level, -down, run) : new Column(Ground.Edge, -down, run);
                }

                if (Liquid(x, y, out bool isLava) >= 128)
                {
                    run++;
                    lava |= isLava;
                }
                else if (!isLava)
                {
                    run = 0;
                }
                else
                {
                    lava = true;
                }
            }

            if (lava)
                return new Column(Ground.Edge, -Below, run);

            return run >= DeepWater ? new Column(Ground.Water, -Below, run) : new Column(Ground.Edge, -Below, run);
        }

        /// <summary>From his front to the near side of column <paramref name="x"/>, going <paramref name="dir"/>. Never less than nothing.</summary>
        private static float Gap(int x, int dir, float front)
        {
            float gap = dir > 0 ? x * 16f - front : front - (x + 1) * 16f;
            return Math.Max(0f, gap);
        }

        /// <summary>The first column his front goes into, going <paramref name="dir"/>.</summary>
        private static int FirstColumn(float front, int dir)
        {
            return dir > 0 ? (int)Math.Floor(front / 16f) : (int)Math.Floor((front - 0.01f) / 16f);
        }

        // ============================================================
        // DECIDING
        // ============================================================

        /// <summary>
        /// The ground's say, this tick. <paramref name="memory"/> is the run-up he is on, if any;
        /// only <paramref name="authority"/> (the server, or single player) starts or ends one, and
        /// the donkey sends it to everyone else.
        /// </summary>
        public static NavSteer Steer(ref NavMemory memory, NavBody body, NavAsk ask, bool authority)
        {
            NavSteer steer = new NavSteer
            {
                Speed = ask.Speed,
                Accel = ask.Accel,
            };

            int want = Math.Abs(ask.Speed) < 0.5f ? 0 : Math.Sign(ask.Speed);

            // In deep water nothing else matters: up, along, and out.
            if (body.Wet && Swim(body, ask, want, ref steer))
            {
                if (authority)
                    memory.Backing = false;

                return steer;
            }

            if (memory.Backing)
            {
                if (BackUp(ref memory, body, want, ref steer, authority))
                    return steer;
            }

            if (want != memory.Dir && authority)
                memory.Tries = 0;

            // Which way he is really going: his speed's, or what he is asked for from a stand.
            int dir = Math.Abs(body.Velocity.X) > 0.5f ? Math.Sign(body.Velocity.X) : want;

            if (dir == 0 || !body.OnGround)
                return steer;

            Look(ref memory, body, ask, want, dir, ref steer, authority);
            return steer;
        }

        // ============================================================
        // THE GROUND AHEAD, AS A ROW OF TOPS
        // ============================================================
        //
        // Column by column, from the one under his back to well past his front: where the top of
        // the ground is, in the world, as he would walk it. Nothing to stand on is +infinity;
        // something he can never be on top of (a wall, a roof too low) is -infinity, and so is
        // everything past it. One row, for the one donkey, read afresh every tick he is on the
        // ground and going somewhere.

        private const int ProfileLength = 6 + Ahead + Reach;
        private static readonly float[] tops = new float[ProfileLength];
        private static readonly bool[] wet = new bool[ProfileLength];
        private static int start;
        private static int frontIndex;

        /// <summary>Where the jump is going: over it and down (Landed), into a face (Hit), down before it (Short), or into nothing (Fell).</summary>
        private const int Landed = 0, Hit = 1, Short = 2, Fell = 3;

        /// <summary>What the first thing ahead that is not just ground is.</summary>
        private enum Hazard : byte
        {
            None,
            Rise,
            Wall,
            Pit,
        }

        private static void Read(NavBody body, int dir, bool maySwim)
        {
            int rows = Rows(body.Height);
            int foot = FootRow(body.Bottom);
            float front = dir > 0 ? body.Position.X + body.Width : body.Position.X;

            start = dir > 0 ? (int)Math.Floor(body.Position.X / 16f) : (int)Math.Floor((body.Position.X + body.Width - 0.01f) / 16f);
            frontIndex = Math.Max(0, (FirstColumn(front, dir) - start) * dir);

            // Under him: what he stands on, from where his feet are.
            for (int i = 0; i < frontIndex && i < ProfileLength; i++)
            {
                int x = start + dir * i;
                Column under = Probe(x, foot, rows);

                wet[i] = under.Kind == Ground.Water;

                if (under.Kind == Ground.Rise || under.Kind == Ground.Wall)
                    tops[i] = body.Bottom;
                else if (under.Kind == Ground.Level || (under.Kind == Ground.Water && maySwim))
                    tops[i] = 16f * (foot + 1 - under.Rise);
                else
                    tops[i] = float.PositiveInfinity;
            }

            // Ahead: walked, a step up or a drop down at a time.
            int walked = foot;

            for (int i = frontIndex; i < ProfileLength; i++)
            {
                int x = start + dir * i;
                Column column = Probe(x, walked, rows);

                wet[i] = column.Kind == Ground.Water;

                switch (column.Kind)
                {
                    case Ground.Level:
                    case Ground.Rise:
                        walked -= column.Rise;
                        tops[i] = 16f * (walked + 1);
                        break;

                    case Ground.Wall:
                        for (; i < ProfileLength; i++)
                        {
                            tops[i] = float.NegativeInfinity;
                            wet[i] = false;
                        }

                        return;

                    case Ground.Water when maySwim:
                        tops[i] = 16f * (walked + 1 - column.Rise);
                        break;

                    default:
                        tops[i] = float.PositiveInfinity;
                        break;
                }
            }
        }

        /// <summary>
        /// The first thing within <see cref="Ahead"/> columns of his front that is not just ground:
        /// a rise of two tiles or more, a wall, or a pit wider than he can stride. <paramref name="at"/>
        /// is its first column, in the row of tops.
        /// </summary>
        private static Hazard FirstHazard(NavBody body, out int at)
        {
            float last = body.Bottom;

            for (int i = frontIndex; i < frontIndex + Ahead && i < ProfileLength; i++)
            {
                float top = tops[i];

                if (float.IsNegativeInfinity(top))
                {
                    at = i;
                    return Hazard.Wall;
                }

                if (float.IsPositiveInfinity(top))
                {
                    int j = i;

                    while (j < ProfileLength && float.IsPositiveInfinity(tops[j]))
                        j++;

                    // Narrow enough to stride, to ground about as high: one end of him is always on something.
                    bool stride = (j - i) * 16f <= body.Width - Straddle && j < ProfileLength
                        && !float.IsNegativeInfinity(tops[j]) && Math.Abs(tops[j] - last) <= 16.5f;

                    if (stride)
                    {
                        last = tops[j];
                        i = j;
                        continue;
                    }

                    at = i;
                    return Hazard.Pit;
                }

                if (top < last - 16.5f)
                {
                    at = i;
                    return Hazard.Rise;
                }

                last = top;
            }

            at = -1;
            return Hazard.None;
        }

        /// <summary>How many columns of nothing from <paramref name="at"/> on.</summary>
        private static int PitWidth(int at)
        {
            int j = at;

            while (j < ProfileLength && float.IsPositiveInfinity(tops[j]))
                j++;

            return j - at;
        }

        /// <summary>The top of the ground in world column <paramref name="x"/>, from the row of tops; behind it, the ground he was on.</summary>
        private static float TopAt(int x, int dir, float feet)
        {
            int i = (x - start) * dir;

            if (i < 0)
                return feet;

            return i < ProfileLength ? tops[i] : float.NegativeInfinity;
        }

        /// <summary>Whether a body with its left side at <paramref name="left"/> and its feet at <paramref name="feet"/> stands on something there.</summary>
        private static bool Supported(float left, float feet, int width, int dir)
        {
            int a = (int)Math.Floor(left / 16f);
            int b = (int)Math.Floor((left + width - 0.01f) / 16f);

            for (int x = a; x <= b; x++)
            {
                float top = TopAt(x, dir, feet);

                if (!float.IsInfinity(top) && Math.Abs(top - feet) <= 1f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A jump, flown ahead of time over the row of tops: from the left side at <paramref name="left"/>
        /// and feet at <paramref name="feet"/>, going <paramref name="speed"/> and gaining
        /// <paramref name="accel"/> a tick up to <paramref name="most"/>, upward at <paramref name="jump"/>.
        /// Landed only if it comes down on column <paramref name="need"/> or past it.
        /// </summary>
        private static int Fly(float left, float feet, float speed, float accel, float most, float jump, int dir, int width, int need)
        {
            float vx = speed;
            float vy = jump;
            float x = left;
            float y = feet;
            float top = Math.Max(most, speed);

            for (int t = 1; t <= 200; t++)
            {
                vx = Math.Min(vx + accel, top);
                vy -= Gravity;

                float nx = x + dir * vx;
                float ny = y - vy;

                // The columns he was over.
                int pa = (int)Math.Floor(x / 16f);
                int pb = (int)Math.Floor((x + width - 0.01f) / 16f);

                // Sideways first: into a column whose top his feet are not over is into its side
                // (Collision.TileCollision asks where his feet were before the move). Gently, he
                // only stops against it and goes on rising, as the game has a body do; hard, the
                // jump has failed. A little over the top is asked of him, not just level with it.
                int na = (int)Math.Floor(nx / 16f);
                int nb = (int)Math.Floor((nx + width - 0.01f) / 16f);

                for (int c = na; c <= nb; c++)
                {
                    if (c >= pa && c <= pb)
                        continue;

                    float side = TopAt(c, dir, feet);

                    if (float.IsNegativeInfinity(side))
                        return Hit;

                    if (float.IsPositiveInfinity(side) || y <= side - Skim)
                        continue;

                    if (vx > Gentle)
                        return Hit;

                    nx = x;
                    vx = 0f;
                    break;
                }

                int a = (int)Math.Floor(nx / 16f);
                int b = (int)Math.Floor((nx + width - 0.01f) / 16f);
                int furthest = -1;

                for (int c = a; c <= b; c++)
                {
                    int i = (c - start) * dir;

                    if (i < 0)
                        continue;

                    if (i >= ProfileLength)
                        return Fell;

                    float ground = tops[i];

                    if (float.IsInfinity(ground) || ny <= ground)
                        continue;

                    // Below the top of it now: come down onto it from above, or gone into it.
                    if (vy > 0f || y > ground)
                        return Hit;

                    furthest = Math.Max(furthest, i);
                }

                x = nx;
                y = ny;

                if (furthest >= 0)
                {
                    if (furthest < need)
                        return Short;

                    return RoomToLand(x, vx, accel, top, dir, width) ? Landed : Hit;
                }

                if (y > feet + 16f * (Below + HighestJump))
                    return Fell;
            }

            return Fell;
        }

        /// <summary>
        /// Down, with his left side at <paramref name="left"/>: a few ticks of going on along the
        /// ground before he would be into anything he would have to jump. A landing with his nose
        /// against the next step is no landing.
        /// </summary>
        private static bool RoomToLand(float left, float vx, float accel, float top, int dir, int width)
        {
            float floor = float.PositiveInfinity;
            int a = (int)Math.Floor(left / 16f);
            int b = (int)Math.Floor((left + width - 0.01f) / 16f);

            for (int c = a; c <= b; c++)
            {
                float ground = TopAt(c, dir, float.PositiveInfinity);

                if (!float.IsInfinity(ground))
                    floor = Math.Min(floor, ground);
            }

            if (float.IsInfinity(floor))
                return true;

            float x = left;

            for (int t = 0; t < LandRoom; t++)
            {
                vx = Math.Min(vx + accel, top);
                x += dir * vx;

                int edge = dir > 0 ? (int)Math.Floor((x + width - 0.01f) / 16f) : (int)Math.Floor(x / 16f);
                float ground = TopAt(edge, dir, floor);

                if (float.IsNegativeInfinity(ground) || (!float.IsPositiveInfinity(ground) && ground < floor - 16.5f))
                    return false;
            }

            return true;
        }

        /// <summary>The least jump (and a little more, if that still lands) that gets him onto column <paramref name="need"/> or past it from here.</summary>
        private static bool Best(float left, float feet, float speed, float accel, float most, int dir, int width, int need, out float least)
        {
            for (float j = 2f; j <= MostJump + 0.001f; j += 0.25f)
            {
                if (Fly(left, feet, speed, accel, most, j, dir, width, need) == Landed)
                {
                    least = j;
                    return true;
                }
            }

            least = float.MaxValue;
            return false;
        }

        /// <summary>The jump to leave with: the least that lands, and a little more if that still lands.</summary>
        private static float WithMargin(float least, float left, float feet, float speed, float accel, float most, int dir, int width, int need)
        {
            float more = Math.Min(MostJump, least + JumpMargin);
            return Fly(left, feet, speed, accel, most, more, dir, width, need) == Landed ? more : least;
        }

        /// <summary>
        /// Whether he could get over it at all: from somewhere between here and it (for a pit, as
        /// far out over it as he is still held up), at the speed he would have there, with the
        /// biggest jump he has.
        /// </summary>
        private static bool Possible(NavBody body, int dir, Hazard hazard, int at, float gap, float speed, float accel, float most)
        {
            float reach = hazard == Hazard.Pit ? gap + body.Width - Straddle : gap;

            for (float run = 0f; run <= reach + 0.01f; run += 8f)
            {
                float left = body.Position.X + dir * run;
                float from = Math.Max(0f, speed);
                float v = Math.Min(Math.Max(most, from), (float)Math.Sqrt(from * from + 2f * accel * run));

                if (!Supported(left, body.Bottom, body.Width, dir))
                    continue;

                if (Fly(left, body.Bottom, v, accel, most, MostJump, dir, body.Width, at) == Landed)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// How far he can back off, still facing <paramref name="dir"/>, over level ground behind him:
        /// no further than <see cref="RunUp"/>, and not off a step or a brink.
        /// </summary>
        private static float Room(NavBody body, int dir)
        {
            int rows = Rows(body.Height);
            int foot = FootRow(body.Bottom);
            float back = dir > 0 ? body.Position.X : body.Position.X + body.Width;
            int x = FirstColumn(back, -dir);
            float room = Gap(x, -dir, back);

            for (int i = 0; room < RunUp && i < 12; i++, x -= dir)
            {
                Column column = Probe(x, foot, rows);

                if (column.Kind != Ground.Level || column.Rise != 0)
                    break;

                room += 16f;
            }

            return Math.Min(room, RunUp);
        }

        /// <summary>Whether backing off <paramref name="room"/> and coming again would get him over: from a stand there, gaining speed as he comes.</summary>
        private static bool RunUpWorks(NavBody body, int dir, int at, float accel, float most, float room)
        {
            if (accel <= 0f || room < 16f)
                return false;

            for (float back = 8f; back <= room + 0.01f; back += 8f)
            {
                float left = body.Position.X - dir * back;
                float v = Math.Min(most, (float)Math.Sqrt(2f * accel * (room - back)));

                if (Fly(left, body.Bottom, v, accel, most, MostJump, dir, body.Width, at) == Landed)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The ground ahead, going <paramref name="dir"/>, and what to do about the first thing on it
        /// that is not just ground: jump it when the arc that gets him over has to leave now (one
        /// tick later would not do), back off for a run at it, or pull up short of it.
        /// </summary>
        private static void Look(ref NavMemory memory, NavBody body, NavAsk ask, int want, int dir, ref NavSteer steer, bool authority)
        {
            Read(body, dir, ask.MaySwim);

            Hazard hazard = FirstHazard(body, out int at);

            if (hazard == Hazard.None)
                return;

            float front = dir > 0 ? body.Position.X + body.Width : body.Position.X;
            float gap = Gap(start + dir * at, dir, front);

            // Toward it: less than nothing while he is still going the other way.
            float speed = body.Velocity.X * dir;

            steer.Ahead = hazard == Hazard.Pit ? (wet[at] ? NavReport.Water : NavReport.Edge) : NavReport.Wall;
            steer.Distance = gap;

            if (hazard == Hazard.Wall)
            {
                if (ask.Careful)
                    PullUp(gap, dir, speed, ref steer);

                return;
            }

            // Not going that way, or not to jump: a brink is pulled up for, a rise only if careful.
            if (want != dir || !ask.MayJump)
            {
                if (hazard == Hazard.Pit || ask.Careful)
                    PullUp(gap, dir, speed, ref steer);

                return;
            }

            float accel = ask.Accel;
            float most = Math.Abs(ask.Speed);

            // Stood against a rise with no speed: back off and have a run at it.
            if (hazard == Hazard.Rise && gap <= 4f && speed < 0.8f)
            {
                if (memory.Backing)
                {
                    BackUp(ref memory, body, want, ref steer, authority);
                    return;
                }

                // The server picks between a run-up and a leap from where he stands, and he is
                // told (the run-up and the jump are both sent at once).
                if (!authority)
                    return;

                float room = Room(body, dir);

                if (memory.Tries < MostTries && room >= LeastRoom && RunUpWorks(body, dir, at, accel, most, room))
                {
                    memory.Backing = true;
                    memory.Dir = dir;
                    memory.From = body.CenterX;
                    memory.Room = room;
                    memory.Ticks = 0;
                    memory.Tries++;

                    BackUp(ref memory, body, want, ref steer, authority);
                    return;
                }

                // No room to back off into: straight up from where he stands, and over.
                if (Best(body.Position.X, body.Bottom, Math.Max(0f, speed), accel, most, dir, body.Width, at, out float leap))
                    steer.Jump = WithMargin(leap, body.Position.X, body.Bottom, Math.Max(0f, speed), accel, most, dir, body.Width, at);
                else
                    steer.Ahead = NavReport.Wall;

                return;
            }

            // Too high, too steep, too wide: as good as a wall, or a brink.
            if ((hazard == Hazard.Pit && PitWidth(at) > LongestLeap) || !Possible(body, dir, hazard, at, gap, speed, accel, most))
            {
                if (hazard == Hazard.Rise)
                    steer.Ahead = NavReport.Wall;

                if (hazard == Hazard.Pit || ask.Careful)
                    PullUp(gap, dir, speed, ref steer);

                return;
            }

            // The least jump that gets him over falls as he comes, and rises again once he is too
            // close: he goes at the bottom of it, when a tick later would want more (or could not
            // be done at all). A brink he leaves as his front gets to it. Never while he is still
            // going the other way.
            if (speed < 0f)
                return;

            float next = Math.Min(speed + accel, Math.Max(most, speed));

            if (!Best(body.Position.X, body.Bottom, speed, accel, most, dir, body.Width, at, out float now))
                return;

            float nextLeft = body.Position.X + dir * next;
            bool later = Supported(nextLeft, body.Bottom, body.Width, dir)
                && Best(nextLeft, body.Bottom, next, accel, most, dir, body.Width, at, out float then)
                && then <= now;

            if (!later || (hazard == Hazard.Pit && gap <= speed + 1f))
                steer.Jump = WithMargin(now, body.Position.X, body.Bottom, speed, accel, most, dir, body.Width, at);
        }

        /// <summary>No faster than lets him stop <see cref="EdgeMargin"/> short of something <paramref name="gap"/> ahead, braking hard if he has to.</summary>
        private static void PullUp(float gap, int dir, float speed, ref NavSteer steer)
        {
            float most = (float)Math.Sqrt(2f * EdgeBrake * Math.Max(0f, gap - EdgeMargin));

            if (steer.Speed * dir > most)
                steer.Speed = dir * most;

            if (speed > most)
                steer.Accel = Math.Max(steer.Accel, EdgeBrake);
        }

        /// <summary>
        /// The run-up: backing off still facing what he means to jump, until he has room, or meets
        /// something behind him, or is asked to stop. True while it goes on.
        /// </summary>
        private static bool BackUp(ref NavMemory memory, NavBody body, int want, ref NavSteer steer, bool authority)
        {
            int dir = memory.Dir;
            bool done = want != dir || memory.Ticks >= BackLongest || Math.Abs(body.CenterX - memory.From) >= memory.Room;

            // Nothing to back onto: a wall behind, or a brink.
            if (!done && body.OnGround)
            {
                float back = dir > 0 ? body.Position.X : body.Position.X + body.Width;
                int behind = FirstColumn(back, -dir);
                Column column = Probe(behind, FootRow(body.Bottom), Rows(body.Height));

                done = (column.Kind != Ground.Level || column.Rise != 0) && Gap(behind, -dir, back) <= 8f;
            }

            if (done)
            {
                if (authority)
                    memory.Backing = false;

                return false;
            }

            if (authority)
                memory.Ticks++;

            steer.Speed = -dir * BackSpeed;
            steer.Accel = BackAccel;
            steer.Jump = 0f;
            steer.Backing = true;
            return true;
        }

        /// <summary>
        /// In water deeper than he can stand in: kept with his feet <see cref="SwimDraft"/> under the
        /// surface, swimming the way he is asked, and leaping out at a bank he can get up. False if
        /// the water is shallow enough to wade.
        /// </summary>
        private static bool Swim(NavBody body, NavAsk ask, int want, ref NavSteer steer)
        {
            if (!Surface(body, out float surface) || body.Bottom - surface < SwimDeep)
                return false;

            steer.Swimming = true;

            float error = surface + SwimDraft - body.Bottom;
            steer.Lift = MathHelper.Clamp(error * 0.3f, -3.5f, 2.5f) - WetGravity;

            steer.Speed = want * Math.Min(Math.Abs(ask.Speed), SwimSpeed);
            steer.Accel = Math.Max(ask.Accel, 0.1f);

            if (want == 0)
                return true;

            // The bank: out with a leap once he is up at the surface and close to it.
            int rows = Rows(body.Height);
            int foot = FootRow(body.Bottom);
            float front = want > 0 ? body.Position.X + body.Width : body.Position.X;
            int x = FirstColumn(front, want);

            for (int k = 0; k < 3; k++, x += want)
            {
                Column column = Probe(x, foot, rows);

                if (column.Kind == Ground.Water || column.Kind == Ground.Edge || (column.Kind == Ground.Level && column.Rise <= 0))
                    continue;

                if (column.Kind == Ground.Wall)
                {
                    steer.Ahead = NavReport.Wall;
                    steer.Distance = Gap(x, want, front);
                    return true;
                }

                float gap = Gap(x, want, front);
                bool atSurface = body.Bottom - surface <= SwimDraft + 6f;

                if (gap <= 10f && atSurface)
                {
                    float height = body.Bottom - 16f * (foot + 1 - column.Rise);
                    float jump = SpeedFor(height + Clearance + WaterLeap, Gravity);

                    if (jump <= MostJump * 1.25f)
                    {
                        steer.Jump = jump;
                        steer.Swimming = false;
                    }
                }

                return true;
            }

            return true;
        }

        /// <summary>The top of the water over his middle, in the world; false if there is no water at his feet.</summary>
        public static bool Surface(NavBody body, out float surface)
        {
            surface = 0f;

            int x = (int)Math.Floor(body.CenterX / 16f);
            int y = FootRow(body.Bottom);
            int top = int.MinValue;
            int amount = 0;

            for (int i = 0; i < 60; i++, y--)
            {
                if (Solid(x, y))
                    break;

                int here = Liquid(x, y, out _);

                if (here <= 0)
                    break;

                top = y;
                amount = here;

                // A tile not full is the top of it.
                if (here < 255)
                    break;
            }

            if (top == int.MinValue)
                return false;

            surface = top * 16f + 16f * (1f - amount / 255f);
            return true;
        }

        // ============================================================
        // FOR THE RIDER
        // ============================================================

        /// <summary>
        /// Whether he could get from where he is to <paramref name="targetX"/> on his legs without
        /// swimming: walking his eye along the ground the whole way, up what he can jump, down what
        /// he could come back up, across what he can clear at full gallop; and whether the ground
        /// he gets to there is within three tiles of <paramref name="targetBottom"/> (where their
        /// feet are), not far under someone up on something.
        /// </summary>
        public static bool Reachable(NavBody body, float targetX, float targetBottom, float topSpeed)
        {
            int dir = Math.Sign(targetX - body.CenterX);

            if (dir == 0 || Math.Abs(targetX - body.CenterX) < body.Width * 0.5f)
                return Math.Abs(body.Bottom - targetBottom) <= 48f;

            int rows = Rows(body.Height);
            int foot = FootRow(body.Bottom);
            float front = dir > 0 ? body.Position.X + body.Width : body.Position.X;
            int x = FirstColumn(front, dir);
            int end = (int)Math.Floor(targetX / 16f);

            for (int i = 0; i < 160 && (end - x) * dir >= 0; i++)
            {
                Column column = Probe(x, foot, rows);

                switch (column.Kind)
                {
                    case Ground.Level:
                    case Ground.Rise:
                        foot -= column.Rise;
                        x += dir;
                        continue;

                    case Ground.Wall:
                        return false;
                }

                // A brink or deep water: the far side within a full gallop's jump, or not at all.
                bool crossed = false;

                for (int j = 1; j < Reach; j++)
                {
                    int far = x + dir * j;
                    Column landing = Probe(far, foot, rows);

                    if (landing.Kind == Ground.Edge || landing.Kind == Ground.Water)
                        continue;

                    if (landing.Kind == Ground.Wall)
                        break;

                    float width = j * 16f;
                    float need = 16f * landing.Rise + 2f;
                    int ticks = (int)Math.Ceiling((width + LandIn) / topSpeed);
                    float jump = (need + Gravity * ticks * (ticks + 1) / 2f) / ticks;

                    if (jump > MostJump)
                        break;

                    foot -= landing.Rise;
                    x = far + dir;
                    crossed = true;
                    break;
                }

                if (!crossed)
                    return false;
            }

            return Math.Abs(16f * (foot + 1) - targetBottom) <= 48f;
        }
    }
}
