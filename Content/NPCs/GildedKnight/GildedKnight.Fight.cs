using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// The fight: his guard between moves (stalking them, waiting his moment), which move he
    /// picks for where they are, each move played from its track (KnightMoves) with the steps
    /// and leaps it carries him, his blades against each machine's own player, the parry, and
    /// the turn into his second half.
    /// </summary>
    public partial class GildedKnight
    {
        // ============================================================
        // NUMBERS
        // ============================================================

        /// <summary>Within this of them he cuts; beyond Far he reaches for them from where he is.</summary>
        private const float Near = 190f;
        private const float Far = 520f;

        /// <summary>
        /// The way he likes to stand from them, between moves: walks in from further until Keep + 30,
        /// inside the reach of the first cut of his twin cuts (129 pixels), and eases back from
        /// nearer than TooClose.
        /// </summary>
        private const float Keep = 90f;
        private const float TooClose = 70f;

        /// <summary>His stalk, pixels a tick; and a step back.</summary>
        private const float StalkSpeed = 1.35f;
        private const float BackSpeed = 0.8f;

        /// <summary>
        /// Ticks between moves, first half and second: one to three seconds of walking at them in his
        /// guard (by request: "leave some walking time for him to approach between attacks").
        /// </summary>
        private const int RestLeast = 60, RestMost = 180;
        private const int SecondRestLeast = 60, SecondRestMost = 150;

        /// <summary>A blade is this thick where it cuts.</summary>
        private const float BladeWidth = 14f;

        /// <summary>Knockback of a cut and of a heavy one.</summary>
        private const float KnockCut = 6f;
        private const float KnockHeavy = 9f;

        /// <summary>The gilded sun and the long blade, no oftener than this (ticks), and not before so long into his second half.</summary>
        private const int SunEvery = 1500;
        private const int LongEvery = 720;

        /// <summary>When each was last done, as Main.GameUpdateCount (this machine's; only the authority's matters).</summary>
        private uint sunLast;
        private uint longLast;

        /// <summary>The ticks a turn about takes, and how far into it he is (drawing only).</summary>
        private int turning;

        /// <summary>The way this machine saw him face at the end of the last tick: a turn the server makes in his guard is seen turning on every machine.</summary>
        private int seenFacing;

        // ============================================================
        // THE MOVES BY NUMBER
        // ============================================================

        /// <summary>The move for a number and its arg: the same on every machine.</summary>
        private static Move Make(Moves id, float with)
        {
            switch (id)
            {
                case Moves.Twin3: return KnightMoves.TwinCut(3, false);
                case Moves.Twin4: return KnightMoves.TwinCut(4, false);
                case Moves.Twin4Delayed: return KnightMoves.TwinCut(4, true);
                case Moves.Leap: return KnightMoves.LeapSlam((int)Math.Max(20f, with));
                case Moves.Crescents: return KnightMoves.Crescents(false);
                case Moves.Crescents2: return KnightMoves.Crescents(true);
                case Moves.Wheel: return KnightMoves.Wheel();
                case Moves.Lancet: return KnightMoves.Lancet(with);
                case Moves.Votive3: return KnightMoves.Votive(3);
                case Moves.Votive4: return KnightMoves.Votive(4);
                case Moves.Votive5: return KnightMoves.Votive(5);
                case Moves.BackStep: return KnightMoves.BackStep();
                case Moves.Dash: return KnightMoves.Dash(Math.Max(40f, with));
                case Moves.Great: return KnightMoves.GreatCrescent();
                case Moves.Procession: return KnightMoves.Procession();
                case Moves.Crown: return KnightMoves.Crown(7);
                case Moves.Long: return KnightMoves.LongBlade();
                case Moves.Sun: return KnightMoves.GildedSun();
                default: return KnightMoves.TwinCut(3, false);
            }
        }

        /// <summary>The move he is in, made once on this machine for its number and arg.</summary>
        private Move Current()
        {
            if (move == null || moveMade != MoveId || argMade != arg)
            {
                move = Make(MoveId, arg);
                moveMade = MoveId;
                argMade = arg;
            }

            return move;
        }

        /// <summary>The pose of the move he is in at this time on its clock (gone into from where he was over its first ticks).</summary>
        private KnightRig.Pose PoseAt(float clock)
        {
            Move m = Current();
            KnightRig.Pose p = m.Track.Sample(clock);

            return clock < 6f ? KnightTrack.Between(from, p, clock / 6f) : p;
        }

        // ============================================================
        // THE GUARD
        // ============================================================

        /// <summary>
        /// Between moves: facing them, stalking them in to where he likes to stand, easing back
        /// if they are on top of him; when his rest is out, the next move.
        /// </summary>
        private void StanceTick(Player target)
        {
            target = Retarget();

            if (target == null)
            {
                if (Authority)
                    Go(Act.Leave);
                return;
            }

            NPC.noGravity = false;
            NPC.noTileCollide = false;

            // His change, the moment it is due.
            if (!second && NPC.life <= NPC.lifeMax * ChangeAt)
            {
                Go(Act.Change);
                return;
            }

            int way = WayTo(target);

            if (way != facing && Authority)
                Face(way);

            // Turned about in his guard (here, or by the server and told): seen narrowing edge on
            // and opening again. (A move that faces him about snaps; the tick's end notes it.)
            if (facing != seenFacing && seenFacing != 0)
                turning = KnightMoves.AboutTicks;

            float dx = Math.Abs(target.Center.X - NPC.Center.X);
            float speed = 0f;

            if (dx > Keep + 30f)
                speed = StalkSpeed * facing;
            else if (dx < TooClose)
                speed = -BackSpeed * facing;

            // Against his own wall he stands, rather than walking on the spot.
            if (speed != 0f && fieldRight > fieldLeft)
            {
                float x = Feet.X + speed;

                if ((x < fieldLeft + WallMargin && speed < 0f) || (x > fieldRight - WallMargin && speed > 0f))
                    speed = 0f;
            }

            NPC.velocity.X = speed;

            if (Math.Abs(speed) > 0.01f)
            {
                float before = gait;
                gait += Math.Abs(speed) / KnightMoves.GuardStride * Math.Sign(speed * facing);

                if (KnightMoves.Footfall(before, gait))
                    Step();
            }

            KnightRig.Pose stance = Math.Abs(speed) > 0.01f ? KnightMoves.GuardWalk(gait) : Breathing();

            // Into it from what he was doing: up off his knee after a stun more slowly.
            float into = cameFrom == Act.Stunned ? 28f : 10f;
            Pose = Timer < into ? KnightTrack.Between(from, stance, Timer / into) : stance;

            if (turning > 0)
            {
                Pose = KnightMoves.About(Pose, 1f - turning / (float)KnightMoves.AboutTicks);
                turning--;
            }

            if (!Authority)
                return;

            if (queued != Moves.None)
            {
                Moves next = queued;
                queued = Moves.None;
                Begin(next, target);
                return;
            }

            if (Timer >= rest)
                Begin(Choose(target), target);
        }

        /// <summary>His guard at rest: the blades held, a breath in him.</summary>
        private KnightRig.Pose Breathing()
        {
            KnightRig.Pose pose = KnightMoves.Guard;
            float b = (float)Math.Sin(Main.GameUpdateCount * 0.05f);

            pose.Lean += 0.015f * b;
            pose.Nod += 0.02f * b;
            pose.SunAngle += 0.03f * b;
            pose.GraceAngle -= 0.03f * b;
            pose.Rise = -0.6f + 0.6f * b;
            return pose;
        }

        /// <summary>Back to his guard, with this long before his next move.</summary>
        private void Rest(int least, int most)
        {
            if (!Authority)
                return;

            rest = second ? Main.rand.Next(SecondRestLeast, SecondRestMost + 1) : Main.rand.Next(least, most + 1);
            Go(Act.Stance);
        }

        // ============================================================
        // CHOOSING
        // ============================================================

        /// <summary>
        /// The next move for where they are: from close, his cuts; from further, the leap, the
        /// crescents, the lancet, the blades; from far, what reaches. Never the same as either of
        /// the last two if there is anything else. His second half adds its own.
        /// </summary>
        private Moves Choose(Player target)
        {
            float dx = Math.Abs(target.Center.X - NPC.Center.X);
            float dy = target.Center.Y - NPC.Center.Y;
            bool above = dy < -120f;

            var bag = new System.Collections.Generic.List<(Moves m, float w)>();

            void Add(Moves m, float w)
            {
                if (w <= 0f)
                    return;

                if (m == last1)
                    w *= 0.15f;
                else if (m == last2)
                    w *= 0.45f;

                bag.Add((m, w));
            }

            bool sunReady = second && Main.GameUpdateCount - sunLast > SunEvery;
            bool longReady = second && Main.GameUpdateCount - longLast > LongEvery;

            if (dx < Near && !above)
            {
                Add(Moves.Twin3, 3f);
                Add(Moves.Twin4, 2.5f);
                Add(Moves.Twin4Delayed, 2.2f);
                Add(Moves.Wheel, 1.6f);
                Add(Moves.BackStep, 0.9f);
                Add(Moves.Crescents2, 0.7f);

                if (second)
                {
                    Add(Moves.Great, 2.2f);
                    Add(Moves.Procession, 0.8f);
                }
            }
            else if (dx < Far)
            {
                Add(Moves.Leap, above ? 4f : 2.6f);
                Add(Moves.Crescents, 1.6f);
                Add(Moves.Crescents2, 1.6f);
                Add(Moves.Lancet, 1.8f);
                Add(Moves.Votive4, 1.4f);
                Add(Moves.Wheel, 1.2f);
                Add(Moves.Dash, 1.6f);

                if (second)
                {
                    Add(Moves.Great, 1.4f);
                    Add(Moves.Procession, 2f);
                    Add(Moves.Crown, 1.6f);

                    if (longReady)
                        Add(Moves.Long, 2.2f);

                    if (sunReady)
                        Add(Moves.Sun, 1.8f);
                }
            }
            else
            {
                Add(Moves.Lancet, 2.2f);
                Add(Moves.Votive5, 1.8f);
                Add(Moves.Crescents2, 1.6f);
                Add(Moves.Leap, 2f);
                Add(Moves.Dash, 2.4f);

                if (second)
                {
                    Add(Moves.Procession, 2f);
                    Add(Moves.Crown, 1.8f);

                    if (longReady)
                        Add(Moves.Long, 2.4f);

                    if (sunReady)
                        Add(Moves.Sun, 2.6f);
                }
            }

            float total = 0f;

            foreach ((Moves m, float w) in bag)
                total += w;

            float pick = Main.rand.NextFloat() * total;

            foreach ((Moves m, float w) in bag)
            {
                pick -= w;

                if (pick <= 0f)
                    return m;
            }

            return bag.Count > 0 ? bag[^1].m : Moves.Twin3;
        }

        /// <summary>
        /// Starts a move at them: its arg worked out for where they are now (the lancet's way,
        /// the leap's arc, the dash's length), where the world is decided.
        /// </summary>
        private void Begin(Moves next, Player target)
        {
            Face(WayTo(target));

            float with = 0f;
            Vector2 feet = Feet;

            switch (next)
            {
                case Moves.Leap:
                {
                    // Where they will be when he comes down: ahead of them by how they are going.
                    Vector2 aim = target.Bottom + target.velocity * (KnightMoves.LeapCrouch + 30f) * 0.6f;
                    aim.X = MathHelper.Clamp(aim.X, Math.Min(fieldLeft, fieldRight - 1f) + 40f, Math.Max(fieldRight, fieldLeft + 1f) - 40f);

                    float land = LandingAt(aim.X, Math.Min(aim.Y, feet.Y), out bool found);

                    // Nowhere to come down there (they are high over open air): half way, or where he stands.
                    if (!found)
                    {
                        aim.X = (aim.X + feet.X) * 0.5f;
                        land = LandingAt(aim.X, feet.Y, out found);
                    }

                    if (!found)
                    {
                        aim.X = feet.X;
                        land = feet.Y;
                    }

                    float dist = Math.Abs(aim.X - feet.X);

                    leapFrom = feet;
                    leapTo = new Vector2(aim.X, land);
                    leapHeight = MathHelper.Clamp(70f + dist * 0.32f + Math.Max(0f, feet.Y - leapTo.Y) * 0.8f, 90f, 260f);
                    with = MathHelper.Clamp(24f + dist / 14f, 28f, 48f);
                    break;
                }

                case Moves.Lancet:
                    with = AimAt(target);
                    break;

                case Moves.Dash:
                    with = MathHelper.Clamp(Math.Abs(target.Center.X - NPC.Center.X) - 90f, 80f, 300f);
                    break;

                case Moves.Sun:
                    sunLast = Main.GameUpdateCount;
                    sunAt = target.Bottom;
                    break;

                case Moves.Long:
                    longLast = Main.GameUpdateCount;
                    break;
            }

            // The crown is followed straight away by a dash and a twin cut.
            if (next == Moves.Crown)
                queued = Moves.Dash;
            else if (next == Moves.Dash && queued == Moves.None && last1 == Moves.Crown)
                queued = Moves.Twin4;

            last2 = last1;
            last1 = next;

            Go(Act.Move, next, with);
        }

        /// <summary>The way from his near shoulder to their middle, as a blade angle facing the way he faces.</summary>
        private float AimAt(Player target)
        {
            Vector2 shoulder = Feet + new Vector2(0f, -60f);
            Vector2 d = target.Center - shoulder;

            return (float)Math.Atan2(d.Y, Math.Max(8f, d.X * facing));
        }

        /// <summary>
        /// Where he can land about a point (world x): the first ground, looking down from twelve rows
        /// over <paramref name="above"/> (a world y) for at most forty-eight, under any of the columns
        /// his body covers, with room over it across all of them for him to stand (five rows clear);
        /// and whether there was any. The top of a bank is found, not a row inside it, nor a ledge
        /// with a cliff beside it in his way.
        /// </summary>
        private static float LandingAt(float x, float above, out bool found)
        {
            int left = (int)((x - Wide * 0.5f) / 16f);
            int right = (int)((x + Wide * 0.5f - 1f) / 16f);
            int y0 = Math.Max(8, (int)(above / 16f) - 12);

            for (int y = y0; y < y0 + 48 && y < Main.maxTilesY - 2; y++)
            {
                if (!WorldGen.InWorld(left, y, 2) || !WorldGen.InWorld(right, y, 2))
                    break;

                bool ground = false;

                for (int c = left; c <= right && !ground; c++)
                {
                    Tile tile = Main.tile[c, y];
                    ground = tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
                }

                if (!ground || !Clear(left, right, y - 5, y - 1))
                    continue;

                found = true;
                return y * 16f;
            }

            found = false;
            return above;
        }

        /// <summary>Nothing solid in these columns over these rows (platforms are not in the way).</summary>
        internal static bool Clear(int left, int right, int top, int bottom)
        {
            for (int c = left; c <= right; c++)
            {
                for (int r = top; r <= bottom; r++)
                {
                    if (!WorldGen.InWorld(c, r, 2))
                        return false;

                    Tile t = Main.tile[c, r];

                    if (t.HasTile && !t.IsActuated && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                        return false;
                }
            }

            return true;
        }

        /// <summary>The top of the ground under a point (a world y), looking down at most thirty tiles; and whether there was any.</summary>
        private static float GroundUnder(Vector2 at, out bool found)
        {
            int x = (int)(at.X / 16f);
            int y0 = Math.Max(1, (int)(at.Y / 16f) - 2);

            for (int y = y0; y < y0 + 32 && y < Main.maxTilesY - 1; y++)
            {
                if (!WorldGen.InWorld(x, y, 2))
                    break;

                Tile tile = Main.tile[x, y];

                if (tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                {
                    found = true;
                    return y * 16f;
                }
            }

            found = false;
            return at.Y;
        }

        // ============================================================
        // A MOVE
        // ============================================================

        /// <summary>
        /// In a move: the pose off its track, the steps it carries him (or the arc of a leap),
        /// its cues as each tick of them comes, his blades against this machine's player while
        /// they cut; at its end, back to his guard (or straight into what was queued).
        /// </summary>
        private void MoveTick(Player target)
        {
            Move m = Current();
            float clock = Timer;

            Pose = PoseAt(clock);

            Travel(m, clock);

            foreach ((int Tick, Cue What) cue in m.Cues)
            {
                if (At(cue.Tick))
                    Cued(cue.What, target);
            }

            Strike(m, clock);

            if (!Authority)
                return;

            // The lancet's line follows them until it locks: the way it goes is decided then.
            if (MoveId == Moves.Lancet && target != null && Timer < KnightMoves.LancetLock && Timer % 4 == 0)
            {
                arg = AimAt(target);
                NPC.netUpdate = true;
            }

            bool done = Timer >= m.Ticks;

            // His change cannot wait for the end of a long move, but a cut is let finish.
            if (!second && NPC.life <= NPC.lifeMax * ChangeAt && Timer >= m.Done)
                done = true;

            if (done)
                Rest(RestLeast, RestMost);
        }

        /// <summary>
        /// The ground a move carries him over this tick: the steps along its track, or, in a leap,
        /// the arc from where he left to where he lands, ignoring what is in the way.
        /// </summary>
        private void Travel(Move m, float clock)
        {
            if (MoveId == Moves.Leap)
            {
                int up = KnightMoves.LeapCrouch;
                int air = (int)Math.Max(20f, arg);

                if (clock >= up && clock <= up + air)
                {
                    float s = (clock - up) / air;
                    Vector2 feet = Vector2.Lerp(leapFrom, leapTo, s) + new Vector2(0f, -4f * leapHeight * s * (1f - s));
                    Vector2 next = NPC.position;

                    NPC.noGravity = true;
                    NPC.noTileCollide = true;
                    airborne = true;
                    NPC.position = new Vector2(feet.X - NPC.width * 0.5f, feet.Y - NPC.height);
                    NPC.velocity = (NPC.position - next) * 0.0f;

                    if (feet.X != leapTo.X)
                        facing = leapTo.X > leapFrom.X ? 1 : -1;

                    return;
                }

                NPC.noGravity = false;
                NPC.noTileCollide = false;
                airborne = false;
            }

            Vector2 go = m.Track.Travel(clock);
            float dx = (go.X - travelled.X) * facing;
            travelled = go;

            // Kept in his field.
            float x = Feet.X + dx;

            if (fieldRight > fieldLeft)
            {
                if (x < fieldLeft + 24f && dx < 0f) dx = 0f;
                if (x > fieldRight - 24f && dx > 0f) dx = 0f;
            }

            NPC.velocity.X = dx;
        }

        // ============================================================
        // HIS BLADES AGAINST THIS MACHINE'S PLAYER
        // ============================================================

        /// <summary>Whether a blade of his cuts now, or is a breath from it: what the parry asks before it turns anything aside.</summary>
        public bool CanBeTurned => TurnedBlade() >= 0;

        /// <summary>
        /// How late a parry's word may come to the server and still be believed, in ticks: it was
        /// made on a screen a round trip behind the server's (at 100 ms, six ticks), against a cut
        /// that screen saw live. Elsewhere it is asked on the screen that made it, a tick's grace.
        /// </summary>
        private const int ParryLate = 16;

        /// <summary>
        /// The blade a parry would turn aside now, or -1: a cut of the move he is in that is live, a
        /// breath from it, or (asked on the server) just over. Of two that might be meant, the one
        /// nearest now; of two live together, the later begun. (Every move goes on well past its
        /// last cut, so a word that comes late finds him still in it.)
        /// </summary>
        private int TurnedBlade()
        {
            if (State != Act.Move || move == null || moveMade != MoveId)
                return -1;

            int late = Main.netMode == NetmodeID.Server ? ParryLate : 1;
            int blade = -1;
            float best = float.MaxValue;
            int bestFrom = int.MinValue;

            foreach (Strike s in move.Strikes)
            {
                if (Timer < s.From - 3 || Timer > s.To + late)
                    continue;

                float off = Timer < s.From ? s.From - Timer : Timer > s.To ? Timer - s.To : 0f;

                if (off < best || (off == best && s.From > bestFrom))
                {
                    best = off;
                    bestFrom = s.From;
                    blade = s.Blade;
                }
            }

            return blade;
        }

        /// <summary>
        /// Every live cut of the move against this machine's own player: the blade from its grip
        /// to a little past its point, at every few degrees of the way it moved since the last
        /// tick, so a fast blade cannot pass over anyone between two ticks. The parry first; then
        /// the hit, once a cut.
        /// </summary>
        private void Strike(Move m, float clock)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            // Only on someone in the fight: nothing of his reaches past his walls.
            if (me == null || !me.active || me.dead || !Fights(me))
                return;

            for (int k = 0; k < m.Strikes.Length; k++)
            {
                Strike s = m.Strikes[k];

                if (!s.Live(clock) || (struck & (1 << k)) != 0)
                    continue;

                ParryPlayer parry = me.GetModPlayer<ParryPlayer>();

                if (parry.Spares(NPC))
                    return;

                float before = Math.Max(s.From, clock - 1f);
                KnightRig.Pose first = PoseAt(before);
                KnightRig.Pose final = PoseAt(clock);

                float swept = Math.Abs((s.Blade == KnightRig.Sun ? final.SunAngle - first.SunAngle : final.GraceAngle - first.GraceAngle))
                    + Math.Abs(final.Turn - first.Turn) * 1.5f;
                int steps = (int)MathHelper.Clamp((float)Math.Ceiling(swept / MathHelper.ToRadians(6f)), 1f, 12f);

                for (int i = 1; i <= steps; i++)
                {
                    float at = MathHelper.Lerp(before, clock, i / (float)steps);
                    KnightRig.Frame frame = KnightRig.Build(PoseAt(at), Feet, facing, null, SunSwing, GraceSwing);

                    Vector2 grip = s.Blade == KnightRig.Sun ? frame.SunGrip : frame.GraceGrip;
                    Vector2 guard = s.Blade == KnightRig.Sun ? frame.SunGuard : frame.GraceGuard;
                    Vector2 tip = s.Blade == KnightRig.Sun ? frame.SunTip : frame.GraceTip;
                    Vector2 along = (tip - guard).SafeNormalize(Vector2.UnitX);
                    Vector2 to = tip + along * 4f;

                    // The long blade comes down with all its length: it cuts where its light reaches.
                    if (MoveId == Moves.Long && k == 0)
                    {
                        float length = LongLength(at);

                        if (length > KnightRig.GripToTip)
                            to = LongEnd(frame, length, out _);
                    }

                    // The parry first: the blade met at their arc is turned aside, and he with it.
                    if (parry.MeetsLine(NPC, grip, to, BladeWidth))
                    {
                        struck |= 1 << k;
                        return;
                    }

                    if (me.immune)
                        continue;

                    float point = 0f;

                    if (!Collision.CheckAABBvLineCollision(me.position, me.Size, guard, to, BladeWidth, ref point))
                        continue;

                    if (Land(me, s, frame, along))
                        struck |= 1 << k;

                    break;
                }
            }
        }

        /// <summary>A cut lands on this machine's player: the hurt (their armour counts), and the look of it. True if it landed.</summary>
        private bool Land(Player me, Strike s, KnightRig.Frame frame, Vector2 along)
        {
            int direction = me.Center.X > NPC.Center.X ? 1 : me.Center.X < NPC.Center.X ? -1 : facing;
            int damage = Main.DamageVar(Damage(s.Heavy ? HeavyDamage : BladeDamage), -me.luck);

            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText.GildedKnightBlade", me.name));

            if (me.Hurt(reason, damage, direction, false, false, ImmunityCooldownID.General, true, 0f, 0f, s.Heavy ? KnockHeavy : KnockCut) <= 0)
                return false;

            KnightFx.Cut(me.Center, along, s.Blade, second, s.Heavy);
            return true;
        }

        // ============================================================
        // TURNED ASIDE
        // ============================================================

        /// <summary>
        /// A cut of his parried (where the world is decided): he rocks back a step and a half, and
        /// for a second nothing lands on him (by request: "parried makes him step back with
        /// invincibility for 1 sec before continuing").
        /// </summary>
        public void Turned(bool staggers)
        {
            if (!Authority)
                return;

            int blade = TurnedBlade();

            if (blade < 0)
                return;

            reelBlade = blade;
            queued = Moves.None;

            if (staggers)
            {
                // Every third parry (the parry's count, kept where the world is decided): stunned,
                // open to the riposte for as long. His own state shows it; his AI is not stopped.
                NPC.GetGlobalNPC<PADStaggerNPC>().Reel(NPC, KnightMoves.StunTicks, false);
                Go(Act.Stunned);
                return;
            }

            Go(Act.Reel);
        }

        /// <summary>
        /// Stunned: rocked back and down on a knee, swaying, for five seconds, or until the riposte
        /// wakes him (PADStaggerNPC: the first hit on him is three times as hard and ends it). Then
        /// up into his guard, slowly, and on.
        /// </summary>
        private void StunnedTick(Player target)
        {
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            OutOfTheGround();

            KnightTrack track = KnightMoves.Stun;
            float clock = Timer;
            KnightRig.Pose p = track.Sample(clock);

            // Swaying where he kneels.
            if (clock > KnightMoves.StunDown)
            {
                float s = (float)Math.Sin((clock - KnightMoves.StunDown) * 0.05f + NPC.whoAmI);
                p.Lean += 0.035f * s;
                p.Nod += 0.045f * s;
            }

            Pose = clock < 3f ? KnightTrack.Between(from, p, clock / 3f) : p;

            Vector2 go = track.Travel(clock);
            NPC.velocity.X = (go.X - travelled.X) * facing;
            travelled = go;

            foreach ((int Tick, Cue What) cue in KnightMoves.StunCues)
            {
                if (At(cue.Tick))
                    Cued(cue.What, target);
            }

            if (!Authority || Timer < 4)
                return;

            // Up when it wears off, or at the riposte, which wakes him.
            if (!NPC.GetGlobalNPC<PADStaggerNPC>().Reeling || Timer >= KnightMoves.StunTicks + 4)
            {
                rest = 50;
                Go(Act.Stance);
            }
        }

        /// <summary>The reel: the step back off the parried blade, the ward of gold, then his guard again.</summary>
        private void ReelTick(Player target)
        {
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            OutOfTheGround();

            Move m = Reeling();
            float clock = Timer;
            KnightRig.Pose p = m.Track.Sample(clock);

            Pose = clock < 3f ? KnightTrack.Between(from, p, clock / 3f) : p;

            Vector2 go = m.Track.Travel(clock);
            float dx = (go.X - travelled.X) * facing;
            travelled = go;

            float x = Feet.X + dx;

            if (fieldRight > fieldLeft && (x < fieldLeft + 24f || x > fieldRight - 24f))
                dx = 0f;

            NPC.velocity.X = dx;

            foreach ((int Tick, Cue What) cue in m.Cues)
            {
                if (At(cue.Tick))
                    Cued(cue.What, target);
            }

            if (At(0))
                KnightFx.Ward(this);

            if (Authority && Timer >= KnightMoves.ReelTicks)
            {
                rest = 30;
                Go(Act.Stance);
            }
        }

        // ============================================================
        // A FOOT COMES DOWN
        // ============================================================

        /// <summary>In the air of a leap on this machine (the arc ignores the ground), until he is down or out of it.</summary>
        private bool airborne;

        /// <summary>
        /// A leap cut short in the air (turned aside, or his end) lets him fall from where the arc
        /// had him, which may be inside a bank it passed over: up out of it, a little a tick, before
        /// he falls. (Only then: standing on a slope is not being in the ground.)
        /// </summary>
        private void OutOfTheGround()
        {
            if (!airborne)
                return;

            if (!Collision.SolidCollision(NPC.position, NPC.width, NPC.height))
            {
                airborne = false;
                return;
            }

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.velocity.Y = 0f;
            NPC.position.Y -= 6f;
        }

        private void Step()
        {
            if (Main.dedServ)
                return;

            // (His end is without a sound: by request.)
            if (State != Act.End)
                KnightSounds.Step(Feet);

            KnightFx.Dust(Feet, 0.6f);
        }
    }
}
