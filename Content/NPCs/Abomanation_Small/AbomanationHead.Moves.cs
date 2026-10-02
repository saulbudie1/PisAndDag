using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// The moves that break the chase, and what decides between them.
    ///
    /// There was one, the rush. There are four now: the rush (over you, as it always was,
    /// or through you, which is new and the commoner), the puke, the ash, and in the
    /// second half the vent. Between them, on its own clock, the spasm. Every number is in
    /// <see cref="Abomanation"/>; every sight and sound is in AbomanationHead.Show.cs.
    ///
    /// Two rules hold all of it together.
    ///
    /// WHO DECIDES. Anything rolled is rolled on the machine that owns the animal (the
    /// server, or the one player) and sent. A client never starts a move: it waits to be
    /// told the phase. Everything after the roll is a sum every machine does the same, so
    /// the clients play the move out for themselves between packets.
    ///
    /// WHO STEERS. The head steers (AbomanationHead.Steer) wherever turning the long way
    /// round is fine. Where the shape matters (the curve up out of the ground, the crawl,
    /// the turn back under) it is driven: the heading is turned the one way it should go,
    /// at a set rate, and nothing is left to which side happened to be shorter. Tight
    /// turns are only ever asked for under the ground, where the body cannot be seen
    /// making them, and only once all of the body is under it (Abomanation.LaySeekTurn).
    /// </summary>
    public partial class AbomanationHead
    {
        // ------------------------------------------------------------------ phases ----
        //
        // After the whip's (4 to 6) and the end's (7).

        /// <summary>The puke: getting under where it will come up, and coming up. <see cref="stage"/> says how far along.</summary>
        private const int PukeSeek = 8;

        /// <summary>The puke: standing on its neck, choking.</summary>
        private const int PukeChoke = 9;

        /// <summary>The puke: the three heaves.</summary>
        private const int PukeSpew = 10;

        /// <summary>Lying down: getting to where it comes out. <see cref="stage"/> says how far along.</summary>
        private const int LayOut = 11;

        /// <summary>Lying down: the crawl along the surface, toward you.</summary>
        private const int LayCrawl = 12;

        /// <summary>Lying down: stopped, and throwing ash or venting. <see cref="layKind"/> says which.</summary>
        private const int LayAct = 13;

        /// <summary>Going back under after the puke, or after lying down.</summary>
        private const int Sinking = 14;

        // ------------------------------------------------------------------- state ----
        //
        // Synced (SendMoves): what a client needs to play the move out. Not synced: what
        // only the deciding machine reads, and what every machine works out for itself.

        /// <summary>The kind of rush: 0 over you, 1 through you. Synced.</summary>
        private int rushKind;

        /// <summary>The rush through you: its line is set, and <see cref="passTarget"/> is the far end of it. Synced.</summary>
        private bool rushLocked;

        /// <summary>Which side of you the move is on: +1 right, -1 left. Synced.</summary>
        private int moveSide = 1;

        /// <summary>Which way the head is travelling for it: +1 right, -1 left. Synced.</summary>
        private int moveDir = 1;

        /// <summary>How far through the part of the move it is in. Synced.</summary>
        private int stage;

        /// <summary>Lying down: 0 to throw ash, 1 to vent. Synced.</summary>
        private int layKind;

        /// <summary>The ground line under the head while it lies, in world pixels, smoothed. Synced.</summary>
        private float layLine;

        /// <summary>How far it has crawled, in pixels. Synced.</summary>
        private float crawled;

        /// <summary>Which way the head faces, in radians, where that is not simply the way it is moving: choking, heaving, lying. Synced.</summary>
        private float facing;

        /// <summary>Which way it turns to go back under: +1 clockwise on screen, -1 the other. Synced.</summary>
        private int sinkTurn = 1;

        /// <summary>Where you were when the move began. The ground is looked for from this height. Synced.</summary>
        private Vector2 moveAim;

        /// <summary>Ticks left of the spasm, or 0. Synced.</summary>
        private int spasm;

        // ---- the deciding machine's own ----

        /// <summary>What it will do next, and after how much chase.</summary>
        private int nextMove;
        private int moveGate = Abomanation.MoveGateMost;

        /// <summary>What it did last, or -1.</summary>
        private int lastMove = -1;

        /// <summary>Ticks of chase until the next spasm.</summary>
        private int spasmWait = Abomanation.SpasmFirstMost;

        /// <summary>Ticks until a vent is owed. -1: the second half has not begun.</summary>
        private int ventWait = -1;

        /// <summary>Bones in the puke now under way.</summary>
        private int pukeBones = Abomanation.PukeBonesLeast;

        /// <summary>For testing (the /abomanation command): the next move is this one, and the next rush this kind; -1 for a roll.</summary>
        private int forcedMove = -1;
        private int forcedRush = -1;

        // ---- every machine's own ----

        /// <summary>The rush through you, while it aims: it has pointed at you once; it is now getting further off; and the nearest it has been.</summary>
        private bool aligned;
        private bool slipping;
        private float closest;

        /// <summary>Heaves played out so far, here.</summary>
        private int heaved;

        /// <summary>Some of the body is in the open air. Looked at each tick of an approach (the puke's, the lying down's), which turns no tighter than Abomanation.LayWide while it is so; and given up on after Abomanation.OutMost.</summary>
        private bool bodyOut;

        // -------------------------------------------------------------------- sync ----

        private void SendMoves(BinaryWriter writer)
        {
            writer.Write(rushKind);
            writer.Write(rushLocked);
            writer.Write(moveSide);
            writer.Write(moveDir);
            writer.Write(stage);
            writer.Write(layKind);
            writer.Write(layLine);
            writer.Write(crawled);
            writer.Write(facing);
            writer.Write(sinkTurn);
            writer.WriteVector2(moveAim);
            writer.Write(spasm);
        }

        private void ReceiveMoves(BinaryReader reader)
        {
            rushKind = reader.ReadInt32();
            rushLocked = reader.ReadBoolean();
            moveSide = reader.ReadInt32();
            moveDir = reader.ReadInt32();
            stage = reader.ReadInt32();
            layKind = reader.ReadInt32();
            layLine = reader.ReadSingle();
            crawled = reader.ReadSingle();
            facing = reader.ReadSingle();
            sinkTurn = reader.ReadInt32();
            moveAim = reader.ReadVector2();
            spasm = reader.ReadInt32();
        }

        // -------------------------------------------------------- what the others read --

        /// <summary>The body is lying on the ground: the parts settle onto it (AbomanationChain.Follow).</summary>
        public bool Lying => (phase == LayCrawl || phase == LayAct) && !leaving;

        /// <summary>Which way it crawls while it lies: +1 right, -1 left.</summary>
        public int LayDir => moveDir;

        /// <summary>Lying to throw ash, and stopped: the arms are up.</summary>
        public bool Throwing => phase == LayAct && layKind == 0;

        /// <summary>Lying to vent, and stopped: swelling, venting, or settling.</summary>
        public bool AtVent => phase == LayAct && layKind == 1;

        /// <summary>The steam is going.</summary>
        public bool Venting => AtVent && attackTimer > Abomanation.VentTell
            && attackTimer <= Abomanation.VentTell + Abomanation.VentBlast;

        /// <summary>A spasm is on.</summary>
        public bool InSpasm => spasm > 0 && phase == 0;

        /// <summary>
        /// The head is holding still, or near enough: nothing is pulling the body along. A
        /// part left in the open air comes down (AbomanationChain.Follow, Abomanation.Sag).
        /// </summary>
        public bool Slack => ((phase == PukeSeek && stage >= 2) || phase == PukeChoke || phase == PukeSpew
            || phase == LayCrawl || phase == LayAct) && !leaving;

        /// <summary>The head's motion is scripted by a move, not steered.</summary>
        private bool MoveDrives => (phase == PukeSeek && stage >= 3) || phase == PukeChoke || phase == PukeSpew
            || phase == LayCrawl || phase == LayAct || phase == Sinking;

        /// <summary>The head's facing is <see cref="facing"/>, not the way it is moving.</summary>
        private bool HoldsFacing => phase == PukeChoke || phase == PukeSpew || phase == LayCrawl || phase == LayAct;

        // --------------------------------------------------------------- the clocks ----

        /// <summary>
        /// What runs under everything: the spasm counting down, and on the deciding machine
        /// the spasm's and the vent's own clocks. A spasm only ever starts in the chase,
        /// and never right on top of a move.
        /// </summary>
        private void Clocks()
        {
            if (spasm > 0)
            {
                spasm--;

                if (phase != 0)
                    spasm = 0;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Enraged)
            {
                if (ventWait < 0)
                    ventWait = Main.rand.Next(Abomanation.VentFirstLeast, Abomanation.VentFirstMost + 1);
                else if (ventWait > 0)
                    ventWait--;
            }

            if (phase != 0 || Hunting || spasm > 0)
                return;

            if (--spasmWait > 0)
                return;

            // A move is about to start: after it, then.
            if (moveGate - chargeTimer < 45 || (whipOwed > 0 && whipOwed < 45))
            {
                spasmWait = 60;
                return;
            }

            bool rage = Enraged;

            spasm = Main.rand.Next(Abomanation.SpasmLeast, Abomanation.SpasmMost + 1);
            spasmWait = Main.rand.Next(
                rage ? Abomanation.RageSpasmGapLeast : Abomanation.SpasmGapLeast,
                (rage ? Abomanation.RageSpasmGapMost : Abomanation.SpasmGapMost) + 1);

            NPC.netUpdate = true;
        }

        // ---------------------------------------------------------------- the choice ----

        /// <summary>
        /// Rolls what comes next and how long the chase runs before it. Called whenever the
        /// chase begins again. The deciding machine's.
        /// </summary>
        private void RollMove()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            bool rage = Enraged;

            moveGate = Main.rand.Next(
                rage ? Abomanation.RageMoveGateLeast : Abomanation.MoveGateLeast,
                (rage ? Abomanation.RageMoveGateMost : Abomanation.MoveGateMost) + 1);

            if (PADWorld.Legends)
                moveGate = (int)(moveGate * Abomanation.LegendsMoveGate);

            // Asked for by hand: that, and at once.
            if (forcedMove >= 0)
            {
                nextMove = forcedMove;
                forcedMove = -1;
                moveGate = 1;
                return;
            }

            float rush = Weigh(Abomanation.MoveRush, Abomanation.MoveWeightRush);
            float puke = Weigh(Abomanation.MovePuke, Abomanation.MoveWeightPuke);
            float ash = Weigh(Abomanation.MoveAsh, Abomanation.MoveWeightAsh);

            float roll = Main.rand.NextFloat(rush + puke + ash);

            nextMove = roll < rush ? Abomanation.MoveRush
                : roll < rush + puke ? Abomanation.MovePuke
                : Abomanation.MoveAsh;
        }

        private float Weigh(int move, float weight) => move == lastMove ? weight * Abomanation.MoveRepeat : weight;

        /// <summary>
        /// The chase has run its length: whatever was rolled starts, if it can from here.
        /// A vent that is owed goes ahead of anything. Nowhere to lie down, and it is one
        /// of the other two. A rush waits for the head to be below you, as it always did;
        /// the others wait for it to be under the ground (<see cref="Buried"/>); and
        /// past <see cref="Abomanation.RushGrace"/> any of them goes anyway.
        /// </summary>
        private void TryMove(Player player)
        {
            if (Enraged && ventWait == 0)
                nextMove = Abomanation.MoveVent;

            bool late = chargeTimer >= moveGate + Abomanation.RushGrace;

            if (nextMove != Abomanation.MoveRush && !late && !Buried())
                return;

            if (nextMove == Abomanation.MoveAsh || nextMove == Abomanation.MoveVent)
            {
                if (LaySite(player, out int side))
                {
                    StartLay(player, side, nextMove == Abomanation.MoveVent);
                    return;
                }

                if (nextMove == Abomanation.MoveVent)
                    ventWait = Abomanation.VentRetry;

                nextMove = Main.rand.NextBool() ? Abomanation.MovePuke : Abomanation.MoveRush;
            }

            if (nextMove == Abomanation.MovePuke)
            {
                StartPuke(player);
                return;
            }

            bool below = NPC.Center.Y > player.Center.Y;

            if (below || late)
                StartRush(player, false);
        }

        /// <summary>
        /// Under the ground, and staying there: the head in it, and either a way under or
        /// not on its way up. Where the puke and the lying down start from. Begun in the
        /// air, the body would still be strung out across the sky behind the head when the
        /// head got where it was going and stopped.
        /// </summary>
        private bool Buried()
        {
            if (!Collision.SolidCollision(NPC.position, NPC.width, NPC.height))
                return false;

            float line = GroundLine((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f));

            return NPC.velocity.Y >= 0f || (line > 0f && NPC.Center.Y - line >= Abomanation.MoveStartDepth);
        }

        /// <summary>Whether any part of the body is in the open air: touching nothing solid.</summary>
        private bool BodyInAir()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC part = Main.npc[i];

                if (!part.active || i == NPC.whoAmI || AbomanationChain.Head(part) != NPC.whoAmI)
                    continue;

                if (part.ModNPC is not AbomanationBody && part.ModNPC is not AbomanationTail)
                    continue;

                if (!Collision.SolidCollision(part.position, part.width, part.height))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Which way to turn, for <see cref="AbomanationHead.Steer"/>. On an approach, with
        /// some of the body still in the air, a long way round is made by way of down
        /// whichever side is shorter: the other way round goes up through the surface.
        /// </summary>
        private float MoveSwing(float swing, float heading)
        {
            bool approach = (phase == PukeSeek && stage == 0) || (phase == LayOut && stage < 2);

            if (!approach || !bodyOut || Math.Abs(swing) <= MathHelper.PiOver2)
                return swing;

            float down = MathHelper.WrapAngle(MathHelper.PiOver2 - heading);

            return down >= 0f ? Math.Abs(swing) : -Math.Abs(swing);
        }

        /// <summary>Sets the phase a move opens on, and clears what the chase was doing.</summary>
        private void Begin(int first)
        {
            phase = first;
            stage = 0;
            chargeTimer = 0;
            attackTimer = 0;
            passTimer = 0;
            spasm = 0;
            bodyOut = BodyInAir();
            NPC.netUpdate = true;
        }

        /// <summary>
        /// A move has finished: the whip's turn, if it is due. It used to come only off a
        /// rush; it comes off any of them now, by the same rules. In the first half a roll
        /// and a gap; in the second every second move, counted; Legends, first half, the
        /// gap alone, and second half every move owes one, paid four to six seconds on.
        /// The deciding machine's.
        /// </summary>
        private void AfterMove(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (PADWorld.Legends && Enraged)
            {
                whipOwed = Main.rand.Next(Abomanation.LegendsRageWhipWaitLeast, Abomanation.LegendsRageWhipWaitMost + 1);
                NPC.netUpdate = true;
                return;
            }

            if (!WhipReady(player))
                return;

            if (Enraged)
            {
                if (++rushesSinceWhip >= Abomanation.RageWhipEvery)
                    StartWhip();
            }
            else if (PADWorld.Legends || Main.rand.NextBool(Abomanation.WhipChance))
            {
                StartWhip();
            }
        }

        // ------------------------------------------------------------------ the rush ----

        /// <summary>Begins a rush, and rolls which kind. A far one (Legends, after a whip) is always the one through you.</summary>
        private void StartRush(Player player, bool far)
        {
            float through = Enraged ? Abomanation.RageThroughChance : Abomanation.ThroughChance;

            rushKind = far || Main.rand.NextFloat() < through ? 1 : 0;

            if (forcedRush >= 0)
            {
                rushKind = forcedRush;
                forcedRush = -1;
            }

            rushLocked = false;
            moveSide = NPC.Center.X >= player.Center.X ? 1 : -1;
            lastMove = Abomanation.MoveRush;

            Begin(1);
            farRush = far;
        }

        /// <summary>Where the rush through you turns round: off to the side it was already on, and below. Kept inside the world, like the dive.</summary>
        private Vector2 ThroughStart(Player player)
        {
            return new Vector2(
                player.Center.X + moveSide * Abomanation.ThroughSide,
                Math.Min(player.Center.Y + Abomanation.ThroughDepth, (Main.maxTilesY - 60) * 16f));
        }

        /// <summary>
        /// The rush through you, a tick of it. Until the line is set it steers at where
        /// you were (AbomanationHead.Aim); this decides when the line is set, and when the
        /// far end of it has been reached.
        ///
        /// The line is set when it is close, or when it has pointed at you and is now
        /// getting further away (you were missed: no circling back), or when it has aimed
        /// long enough. From there it does not steer at you again.
        /// </summary>
        private void Through()
        {
            // A rush this machine was told about, not one it began: what it kept from
            // the last one is no use to it.
            if (seenPhase != 2)
            {
                aligned = false;
                slipping = false;
                closest = float.MaxValue;
            }

            if (!rushLocked)
            {
                Vector2 at = Lagged;
                float distance = NPC.Distance(at);
                float swing = Math.Abs(MathHelper.WrapAngle((at - NPC.Center).ToRotation() - NPC.velocity.ToRotation()));

                if (swing < 0.3f)
                    aligned = true;

                if (aligned)
                {
                    if (distance > closest + 40f)
                        slipping = true;

                    closest = Math.Min(closest, distance);
                }

                if (distance < Abomanation.ThroughLock || slipping || attackTimer > Abomanation.ThroughAimMost)
                {
                    rushLocked = true;
                    passTarget = NPC.Center + NPC.velocity.SafeNormalize(-Vector2.UnitY)
                        * (Math.Min(distance, 400f) + Abomanation.ThroughCarry);
                    NPC.netUpdate = true;
                }

                return;
            }

            Vector2 way = NPC.velocity.SafeNormalize(-Vector2.UnitY);
            bool past = Vector2.Dot(passTarget - NPC.Center, way) <= 0f;

            if (past || NPC.Distance(passTarget) < 70f || attackTimer > Abomanation.ThroughAimMost + 80)
            {
                phase = 3;
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        // ------------------------------------------------------------------ the puke ----

        /// <summary>
        /// Begins the puke. It comes up to one side of you: ahead of you if you are
        /// running, the side it is on if you are not. Short of that place it runs outward
        /// to it, past it inward, so it always arrives travelling level.
        /// </summary>
        private void StartPuke(Player player)
        {
            float off = NPC.Center.X - player.Center.X;

            if (Math.Abs(player.velocity.X) > 1.5f)
                moveSide = player.velocity.X > 0f ? 1 : -1;
            else if (Math.Abs(off) < 48f)
                moveSide = NPC.velocity.X >= 0f ? 1 : -1;
            else
                moveSide = off > 0f ? 1 : -1;

            bool inside = off * moveSide < Abomanation.PukeOff + Math.Abs(player.velocity.X) * Abomanation.PukeLead + Abomanation.PukeCurve;

            moveDir = inside ? moveSide : -moveSide;
            moveAim = player.Center;
            lastMove = Abomanation.MovePuke;

            pukeBones = Main.rand.Next(
                PADWorld.Legends ? Abomanation.LegendsPukeBonesLeast : Abomanation.PukeBonesLeast,
                Abomanation.PukeBonesMost + 1);

            Begin(PukeSeek);
        }

        /// <summary>The run-up and the start of the curve, both just under the surface, under where it will come up. They follow you until it slows.</summary>
        private void PukePoints(Player player, out Vector2 run, out Vector2 start)
        {
            float up = player.Center.X + moveSide * (Abomanation.PukeOff + Math.Abs(player.velocity.X) * Abomanation.PukeLead);
            float startX = up - moveDir * Abomanation.PukeCurve;
            float runX = startX - moveDir * Abomanation.PukeRun;

            run = new Vector2(runX, Ground(runX) + Abomanation.PukeUnder);
            start = new Vector2(startX, Ground(startX) + Abomanation.PukeUnder);
        }

        /// <summary>Where the puke's approach is steering.</summary>
        private Vector2 PukeAim(Player player)
        {
            if (stage < 2)
            {
                PukePoints(player, out Vector2 run, out Vector2 start);

                if (stage == 1)
                    return start;

                // Near it with some of the body still in the air: not yet. Under it,
                // where the turn it is allowed has room (Abomanation.PukeDiveNear).
                if (bodyOut && NPC.Distance(run) < Abomanation.PukeDiveNear)
                    run.Y += Abomanation.PukeDive;

                return run;
            }

            // Slowing: on along the same shallow line.
            float ahead = NPC.Center.X + moveDir * 200f;
            return new Vector2(ahead, Ground(ahead) + Abomanation.PukeUnder);
        }

        /// <summary>
        /// The puke's approach, a tick of it. Stages: 0 to the run-up, 1 along it to where
        /// the curve starts, 2 slowing along the same line (the tell begins), 3 the curve
        /// up out of the ground and the rise, driven. Then the choke.
        /// </summary>
        private void PukeRise(Player player)
        {
            attackTimer++;

            bodyOut = stage < 2 && attackTimer < Abomanation.OutMost && BodyInAir();

            if (stage < 2)
            {
                PukePoints(player, out Vector2 run, out Vector2 start);

                // The run-up is not begun with any of it still in the air.
                if (NPC.Distance(stage == 0 ? run : start) < Abomanation.PukeReach && !(stage == 0 && bodyOut))
                {
                    stage++;

                    if (stage == 2)
                        attackTimer = 0;

                    NPC.netUpdate = true;
                }
                else if (attackTimer >= Abomanation.PukeSeekMost)
                {
                    // Never got there. No puke, and no whip off the back of nothing.
                    Reset();
                }

                return;
            }

            if (stage == 2)
            {
                if (attackTimer >= Abomanation.PukeCreep)
                {
                    stage = 3;
                    attackTimer = 0;
                    riseTo = Math.Min(Ground(NPC.Center.X) - Abomanation.PukeHeight, player.Center.Y - 48f);
                    NPC.netUpdate = true;
                }

                return;
            }

            // Slowing, and turning up, the short way, on a curve of its own size: speed
            // over the radius is the turn.
            float top = Abomanation.Speed * Abomanation.PukeRiseSpeed * Mettle();
            float speed = NPC.velocity.Length();

            speed = speed > top
                ? Math.Max(top, speed - Abomanation.SpeedChange)
                : Math.Min(top, speed + Abomanation.SpeedChange);

            float heading = Heading(-MathHelper.PiOver2);
            float swing = MathHelper.WrapAngle(-MathHelper.PiOver2 - heading);
            float turn = speed / Abomanation.PukeCurve;

            heading += MathHelper.Clamp(swing, -turn, turn);
            NPC.velocity = heading.ToRotationVector2() * speed;

            if ((NPC.Center.Y <= riseTo && Math.Abs(swing) < 0.5f) || attackTimer >= Abomanation.PukeRiseMost)
            {
                phase = PukeChoke;
                attackTimer = 0;
                stage = 0;
                facing = heading;
                NPC.netUpdate = true;
            }
        }

        /// <summary>Turns the face toward you, held a little above the straight line: what comes out is lobbed.</summary>
        private void Face(Player player, float rate)
        {
            Vector2 to = player.Center - NPC.Center;
            float want = to.ToRotation() + (to.X >= 0f ? -Abomanation.PukeLob : Abomanation.PukeLob);

            facing += MathHelper.Clamp(MathHelper.WrapAngle(want - facing), -rate, rate);
        }

        /// <summary>Whether this tick of the choke is one of its three catches.</summary>
        private static bool Gag(int tick) => Array.IndexOf(Abomanation.ChokeGag, tick) >= 0;

        private static int ChokeLength => PADWorld.Legends ? Abomanation.LegendsChokeTicks : Abomanation.ChokeTicks;

        /// <summary>
        /// The choke: up on its own neck, creeping, its face coming round onto you, and
        /// three times something catches and jerks the head back.
        /// </summary>
        private void Choke(Player player)
        {
            attackTimer++;

            Face(player, Abomanation.ChokeTurn);

            Vector2 way = facing.ToRotationVector2();

            NPC.velocity = NPC.velocity * 0.8f + way * (Abomanation.ChokeCreep * 0.2f);

            if (Gag(attackTimer))
                NPC.velocity -= way * Abomanation.ChokeKick;

            if (attackTimer >= ChokeLength)
            {
                phase = PukeSpew;
                attackTimer = 0;
                heaved = 0;
                NPC.netUpdate = true;
            }
        }

        /// <summary>
        /// The three heaves, on their ticks. Counted, not matched: a packet that moves the
        /// clock a tick either way neither repeats one nor loses one.
        /// </summary>
        private void Spew(Player player)
        {
            // Told about it, not begun here: none of its heaves have been played here yet.
            if (seenPhase != PukeSpew)
                heaved = 0;

            attackTimer++;

            Face(player, Abomanation.HeaveTurn);

            Vector2 way = facing.ToRotationVector2();

            NPC.velocity = NPC.velocity * 0.82f + way * 0.3f;

            while (heaved < Abomanation.HeaveAt.Length && attackTimer >= Abomanation.HeaveAt[heaved])
            {
                Heave(player, heaved);
                heaved++;
            }

            if (attackTimer >= Abomanation.HeaveAt[Abomanation.HeaveAt.Length - 1] + Abomanation.PukeAfter)
            {
                // Over forward, toward you, and down.
                Sink(Math.Cos(facing) > 0.0 ? 1 : -1, way);
            }
        }

        /// <summary>One heave: the head thrown back, the mouth wide and then shut on it, and on the deciding machine what comes out.</summary>
        private void Heave(Player player, int index)
        {
            NPC.velocity = -facing.ToRotationVector2() * Abomanation.HeaveKick;

            Gape(Abomanation.HeaveGape, Abomanation.HeaveShut);
            HeaveShow(index);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                HeaveSpit(player, index);
        }

        /// <summary>Bones in each of the three heaves, for five to eight in all.</summary>
        private static readonly int[,] BoneShare =
        {
            { 2, 1, 2 },
            { 2, 2, 2 },
            { 2, 2, 3 },
            { 3, 2, 3 },
        };

        /// <summary>
        /// What one heave throws. Bones in all three; a hand in the first and the last; a
        /// skull in the second and the last. Each heave is a clump: one bone dead on where
        /// it is aimed, the rest within SpitScatter of it. The first two are aimed at you,
        /// the last at where you are going.
        /// </summary>
        private void HeaveSpit(Player player, int index)
        {
            int share = Math.Clamp(pukeBones - Abomanation.PukeBonesLeast, 0, BoneShare.GetLength(0) - 1);
            int bones = BoneShare[share, Math.Clamp(index, 0, 2)];

            Vector2 mouth = NPC.Center + facing.ToRotationVector2() * Abomanation.ThroatReach;
            Vector2 at = player.Center;

            if (index == Abomanation.HeaveAt.Length - 1)
            {
                float lead = PADWorld.Legends ? Abomanation.LegendsSpitLead : Abomanation.SpitLead;

                at.X += player.velocity.X * Flight(at.X - mouth.X) * lead;
            }

            int bone = ModContent.ProjectileType<AbomanationBone>();

            for (int i = 0; i < bones; i++)
            {
                Spit(mouth, at, i == 0 ? 0f : Abomanation.SpitScatter, bone,
                    SpitDamage(Abomanation.BoneHit, Abomanation.BoneHitExpert), 0f, Main.rand.Next(AbomanationBone.Looks));
            }

            if (index != 1)
            {
                Spit(mouth, at, Abomanation.SpitScatter, ModContent.ProjectileType<AbomanationHand>(),
                    SpitDamage(Abomanation.HandHit, Abomanation.HandHitExpert), 0f, Main.rand.Next(AbomanationHand.Looks));
            }

            if (index != 0)
            {
                Spit(mouth, at, Abomanation.SpitScatter, ModContent.ProjectileType<AbomanationSkull>(),
                    SpitDamage(Abomanation.SkullHit, Abomanation.SkullHitExpert), 0f, 0f);
            }
        }

        /// <summary>Ticks a thrown thing is in the air to cover this much ground.</summary>
        private static float Flight(float across)
        {
            return MathHelper.Clamp(Math.Abs(across) / Abomanation.SpitPace,
                Abomanation.SpitFlightLeast, Abomanation.SpitFlightMost);
        }

        /// <summary>One thing out of the mouth, on the arc that puts it at (or near) a point after its time in the air.</summary>
        private void Spit(Vector2 from, Vector2 at, float scatter, int type, int damage, float ai0, float ai1)
        {
            Vector2 to = at + new Vector2(scatter > 0f ? Main.rand.NextFloat(-scatter, scatter) : 0f, 0f);
            float air = Flight(to.X - from.X) + Main.rand.Next(-4, 9);

            Vector2 velocity = new Vector2(
                MathHelper.Clamp((to.X - from.X) / air, -18f, 18f),
                (to.Y - from.Y) / air - 0.5f * Abomanation.SpitGravity * air);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), from, velocity, type, damage, 0f, Main.myPlayer, ai0, ai1);
        }

        /// <summary>
        /// The number a thing it throws carries. Classic or expert as the game reads it
        /// (journey, by its slider); the game's own doubling and its difficulty multiplier
        /// are applied on the hit (see Abomanation.BoneHit). Legends on top.
        /// </summary>
        private int SpitDamage(float classic, float expert)
        {
            float damage = NPC.GetAttackDamage_ForProjectiles(classic, expert);

            if (PADWorld.Legends)
                damage *= Abomanation.LegendsDamage;

            return Math.Max(1, (int)Math.Round(damage));
        }

        // ----------------------------------------------------------------- back under ----

        /// <summary>Begins the turn back into the ground, the one way, from the heading given (or the one it has).</summary>
        private void Sink(int turn, Vector2? from = null)
        {
            phase = Sinking;
            attackTimer = 0;
            stage = 0;
            sinkTurn = turn;

            if (from.HasValue)
                NPC.velocity = from.Value;

            NPC.netUpdate = true;
        }

        /// <summary>
        /// Going back under: the heading turned round to straight down, only ever the way
        /// <see cref="sinkTurn"/> says, picking up speed, and faster once it points down.
        /// Under, the chase begins again, and the whip may follow.
        /// </summary>
        private void Sinks(Player player)
        {
            attackTimer++;

            float turn = Abomanation.TurnSpeed * Abomanation.SinkTurn * (Enraged ? Abomanation.RageSpeed : 1f);
            float top = Abomanation.Speed * Mettle();

            float heading = Heading(MathHelper.PiOver2);

            heading += sinkTurn * Math.Min(turn, Left(heading, MathHelper.PiOver2, sinkTurn));

            if (Math.Sin(heading) > 0.5)
                top *= Abomanation.SinkDive;

            float speed = Math.Min(top, NPC.velocity.Length() + Abomanation.SpeedChange);

            NPC.velocity = heading.ToRotationVector2() * speed;

            bool under = Collision.SolidCollision(NPC.position, NPC.width, NPC.height);
            bool deep = NPC.Center.Y > Ground(NPC.Center.X) + Abomanation.SinkDeep;

            if ((attackTimer > 24 && under && deep) || attackTimer > Abomanation.SinkMost)
            {
                Reset();
                AfterMove(player);
            }
        }

        /// <summary>
        /// How far round a heading still has to go to reach another, turning only the one
        /// way: 0 to a whole turn. A hair short of a whole turn is read as there already,
        /// or a heading a rounding error past its mark would go all the way round again.
        /// </summary>
        private static float Left(float heading, float to, int way)
        {
            float left = (way > 0 ? to - heading : heading - to) % MathHelper.TwoPi;

            if (left < 0f)
                left += MathHelper.TwoPi;

            return left > MathHelper.TwoPi * 0.975f ? 0f : left;
        }

        /// <summary>The way it is moving, in radians; standing still, the heading given.</summary>
        private float Heading(float still)
        {
            return NPC.velocity.LengthSquared() > 0.0001f ? NPC.velocity.ToRotation() : still;
        }

        /// <summary>What the halves and the mode do to its pace, as one number.</summary>
        private float Mettle()
        {
            return (PADWorld.Legends ? Abomanation.LegendsSpeed : 1f) * (Enraged ? Abomanation.RageSpeed : 1f);
        }

        /// <summary>
        /// The ground line at this world X, looked for from the height you were at when
        /// the move began, so it is the ground you are on and not the roof of something
        /// under it. The head's own height if there is no ground at all.
        /// </summary>
        private float Ground(float x)
        {
            float line = GroundLine((int)(x / 16f), (int)(moveAim.Y / 16f));

            return line > 0f ? line : NPC.Center.Y;
        }

        // ---------------------------------------------------------------- lying down ----

        /// <summary>Whether there is ground to lie on, on either side of you, and which. The side it is already going toward is tried first.</summary>
        private bool LaySite(Player player, out int side)
        {
            side = NPC.Center.X + NPC.velocity.X * 40f >= player.Center.X ? 1 : -1;

            if (LaySiteOn(player, side))
                return true;

            side = -side;

            return LaySiteOn(player, side);
        }

        /// <summary>See <see cref="Abomanation.LaySiteNear"/>.</summary>
        private static bool LaySiteOn(Player player, int side)
        {
            int playerX = (int)(player.Center.X / 16f);
            int playerY = (int)(player.Bottom.Y / 16f);
            int reach = (int)((Abomanation.LayStart + Abomanation.LayRun) / 16f) + 2;
            int deep = (int)((Abomanation.LayStart + Abomanation.LayRun + Abomanation.LayBack) / 16f);

            if (GroundLine(playerX + side * deep, playerY) <= 0f)
                return false;

            float last = -1f;

            for (int i = Abomanation.LaySiteNear; i <= reach; i += 2)
            {
                int x = playerX + side * i;
                float line = GroundLine(x, playerY);

                if (line <= 0f)
                    return false;

                if (Math.Abs(line - player.Bottom.Y) > Abomanation.LaySiteDrop * 16f)
                    return false;

                if (last > 0f && Math.Abs(line - last) > Abomanation.LaySiteStep * 16f)
                    return false;

                int row = (int)(line / 16f);

                for (int up = 1; up <= Abomanation.LaySiteAir; up++)
                {
                    if (!WorldGen.InWorld(x, row - up, 2) || WorldGen.SolidTile(x, row - up))
                        return false;
                }

                last = line;
            }

            return true;
        }

        /// <summary>Begins lying down, to throw ash or to vent.</summary>
        private void StartLay(Player player, int side, bool vent)
        {
            layKind = vent ? 1 : 0;
            moveSide = side;
            moveDir = -side;
            moveAim = player.Center;
            lastMove = vent ? Abomanation.MoveVent : Abomanation.MoveAsh;

            Begin(LayOut);
        }

        /// <summary>
        /// The two points it goes by on its way to where it comes out: one deep, out past
        /// the far end of its run under the surface; and the far end of that run.
        /// </summary>
        private void LayPoints(out Vector2 deep, out Vector2 run)
        {
            float runX = moveAim.X + moveSide * (Abomanation.LayStart + Abomanation.LayRun);
            float deepX = runX + moveSide * Abomanation.LayBack;

            deep = new Vector2(deepX, Ground(deepX) + Abomanation.LayDeep);
            run = new Vector2(runX, Ground(runX) + Abomanation.LayUnder);
        }

        /// <summary>Where lying down's approach is steering.</summary>
        private Vector2 LayAim()
        {
            if (stage < 2)
            {
                LayPoints(out Vector2 deep, out Vector2 run);
                return stage == 0 ? deep : run;
            }

            // The run in: on along just under the surface, at you.
            float ahead = NPC.Center.X + moveDir * 200f;
            return new Vector2(ahead, Ground(ahead) + Abomanation.LayUnder);
        }

        /// <summary>
        /// Gives lying down up, part way. A vent given up comes round again after
        /// VentRetry, not at the very next move: a player who keeps running would
        /// otherwise be offered nothing else.
        /// </summary>
        private void LayFails()
        {
            if (layKind == 1 && Main.netMode != NetmodeID.MultiplayerClient)
                ventWait = Abomanation.VentRetry;
        }

        /// <summary>
        /// Lying down's approach, a tick of it. Stages: 0 to the deep point, 1 up from it
        /// to the far end of the run, 2 the run in under the surface. Where the run ends
        /// the crawl begins: it is pulled up onto the ground line as it goes (Lies).
        /// </summary>
        private void LaySeeks()
        {
            attackTimer++;

            bodyOut = stage < 2 && attackTimer < Abomanation.OutMost && BodyInAir();

            if (stage < 2)
            {
                LayPoints(out Vector2 deep, out Vector2 run);

                // It does not turn up for the run with any of it still in the air: that
                // turn is a tight one.
                if (NPC.Distance(stage == 0 ? deep : run) < Abomanation.LayReach && !(stage == 0 && bodyOut))
                {
                    stage++;
                    NPC.netUpdate = true;
                }
                else if (attackTimer >= Abomanation.LaySeekMost)
                {
                    LayFails();
                    Reset();
                }

                return;
            }

            float gone = moveSide * (NPC.Center.X - moveAim.X);

            if (gone <= Abomanation.LayStart)
            {
                phase = LayCrawl;
                attackTimer = 0;
                layLine = Ground(NPC.Center.X);
                crawled = 0f;
                facing = Heading(moveDir > 0 ? 0f : MathHelper.Pi);
                NPC.netUpdate = true;
            }
            else if (attackTimer >= Abomanation.LaySeekMost + Abomanation.LayRunMost)
            {
                LayFails();
                Reset();
            }
        }

        /// <summary>
        /// The crawl, and then lying there. Driven, the way the arrival is: along at its
        /// pace, and pulled onto the ground line under the head, which is read every tick
        /// and smoothed so a step in the ground lifts it and does not jolt it. The body
        /// settles onto its own ground behind (AbomanationChain.Follow).
        /// </summary>
        private void Lies(Player player)
        {
            attackTimer++;

            bool crawling = phase == LayCrawl;

            layLine += (Ground(NPC.Center.X) - layLine) * Abomanation.LayTrack;

            float pace = crawling ? Abomanation.CrawlSpeed * (Enraged ? Abomanation.RageSpeed : 1f) : Abomanation.LayCreep;
            Vector2 want = new Vector2(moveDir * pace, (layLine - Abomanation.LayLift - NPC.Center.Y) * Abomanation.LayPull);

            NPC.velocity += (want - NPC.velocity) * 0.15f;
            crawled += Math.Abs(NPC.velocity.X);

            // The face follows the ground it is travelling over, not every jolt of it,
            // and holds where it was once it has all but stopped.
            if (NPC.velocity.LengthSquared() > 2f)
                facing = facing.AngleLerp(NPC.velocity.ToRotation(), 0.2f);

            if (crawling)
            {
                // How far ahead of the head you are, the way it is crawling. Behind it
                // (you came round it, or were there all along) is not near: it crawls on
                // until enough of it is out to be worth stopping.
                float gap = moveDir * (player.Center.X - NPC.Center.X);

                bool near = gap >= 0f && gap <= Abomanation.LayStop;
                bool passed = gap < 0f && crawled >= Abomanation.CrawlLeast;

                if (gap > Abomanation.LayLost)
                {
                    // You are gone. Nothing to do it for.
                    LayFails();
                    Sink(moveDir);
                }
                else if (near || passed || crawled >= Abomanation.ChainLength + 60f
                    || attackTimer >= Abomanation.CrawlMost)
                {
                    phase = LayAct;
                    attackTimer = 0;
                    NPC.netUpdate = true;
                }

                return;
            }

            if (layKind == 1)
                Vents();
            else
                Throws(player);
        }

        // ------------------------------------------------------------------- the ash ----

        /// <summary>Waves of ash this time.</summary>
        private int AshWaveCount => (Enraged ? Abomanation.RageAshWaves : Abomanation.AshWaves) + (PADWorld.Legends ? 1 : 0);

        /// <summary>Ticks a wave takes to run down the whole body: every arm's turn, its wind and its follow through.</summary>
        private static int AshWaveTicks => Abomanation.BottomArmsPerSide * Abomanation.AshStep
            + Abomanation.AshWind + Abomanation.AshFollow;

        /// <summary>
        /// Ticks into the wave being thrown, or -1 between waves. The arms are drawn from
        /// it and the balls are thrown from it, so the two agree on every machine.
        /// </summary>
        public int AshClock(out int wave)
        {
            wave = -1;

            if (!Throwing)
                return -1;

            int since = attackTimer - Abomanation.AshFirst;

            if (since < 0)
                return -1;

            wave = since / Abomanation.AshWave;

            if (wave >= AshWaveCount)
                return -1;

            int clock = since - wave * Abomanation.AshWave;

            return clock <= AshWaveTicks ? clock : -1;
        }

        /// <summary>Lying there throwing: the balls, on the deciding machine, and when the last wave is done, under.</summary>
        private void Throws(Player player)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int clock = AshClock(out int wave);

                if (clock >= 0)
                    ThrowAsh(player, clock, wave);
            }

            int whole = Abomanation.AshFirst + (AshWaveCount - 1) * Abomanation.AshWave + AshWaveTicks + Abomanation.AshAfter;

            if (attackTimer >= whole)
                Sink(moveDir);
        }

        /// <summary>Which side of a lying part is the upper one: 0 the left of the segment, 1 the right.</summary>
        public static int UpSide(NPC part) => Math.Sin(part.rotation) > 0.0 ? 0 : 1;

        /// <summary>Whether a point is in open air: in the world, and not in a solid tile.</summary>
        private static bool Open(Vector2 at)
        {
            int x = (int)(at.X / 16f);
            int y = (int)(at.Y / 16f);

            return WorldGen.InWorld(x, y, 2) && !WorldGen.SolidTile(x, y);
        }

        /// <summary>Whether a part is lying on the ground: within a couple of tiles of its own ground line.</summary>
        private static bool Grounded(NPC part)
        {
            float line = GroundLine((int)(part.Center.X / 16f), (int)(part.Center.Y / 16f));

            return line > 0f && Math.Abs(line - Abomanation.LayLift - part.Center.Y) < 40f;
        }

        /// <summary>
        /// The balls whose arm lets go this tick. Every arm on the upper side of every part
        /// that is lying on the ground throws one, in its turn. Up at a rolled speed, and
        /// across at whatever lands it where it is meant to come down: somewhere within
        /// AshSpread of you, or (one in AshAimed) on where you are going.
        /// </summary>
        private void ThrowAsh(Player player, int clock, int wave)
        {
            int body = ModContent.NPCType<AbomanationBody>();
            int ball = ModContent.ProjectileType<AbomanationAsh>();
            int damage = SpitDamage(Abomanation.AshHit, Abomanation.AshHitExpert);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC part = Main.npc[i];

                if (!part.active || part.type != body || AbomanationChain.Head(part) != NPC.whoAmI)
                    continue;

                int place = (int)part.ai[3];
                int up = UpSide(part);
                int count = AbomanationArms.BottomCount(place, up);

                for (int row = 0; row < count; row++)
                {
                    int arm = AbomanationArms.ArmNumber(place, up, row);

                    if (clock != arm * Abomanation.AshStep + Abomanation.AshWind + 1)
                        continue;

                    Vector2 hand = AbomanationArms.Hand(part, place, up, row, count,
                        AbomanationArms.BottomSpan, ArmSeed, AbomanationArms.RaisedRake);

                    if (!Grounded(part) || !Open(hand))
                        continue;

                    bool aimed = arm % Abomanation.AshAimed == wave % Abomanation.AshAimed;

                    float lands = player.Center.X + (aimed
                        ? player.velocity.X * 60f + Main.rand.NextFloat(-20f, 20f)
                        : Main.rand.NextFloat(-Abomanation.AshSpread, Abomanation.AshSpread));

                    float rise = Main.rand.NextFloat(Abomanation.AshRiseLeast, Abomanation.AshRiseMost);
                    float fall = player.Center.Y - hand.Y;
                    float under = rise * rise + 2f * Abomanation.SpitGravity * fall;

                    // Up, over, and down to your height: the time that takes.
                    float air = under > 0f
                        ? (rise + (float)Math.Sqrt(under)) / Abomanation.SpitGravity
                        : 2f * rise / Abomanation.SpitGravity;

                    Vector2 velocity = new Vector2(MathHelper.Clamp((lands - hand.X) / air, -12f, 12f), -rise);

                    Projectile.NewProjectile(NPC.GetSource_FromAI(), hand, velocity, ball, damage, 0f, Main.myPlayer);
                }
            }
        }

        // ------------------------------------------------------------------ the vent ----

        /// <summary>
        /// Lying there venting. The deciding machine gives it its life back in parts, evenly
        /// through the steam; the steam itself, and what it does to anyone standing in it,
        /// is each machine's own (AbomanationHead.Show.cs). Then under, and the clock set
        /// for the next.
        /// </summary>
        private void Vents()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int into = attackTimer - Abomanation.VentTell;
                int every = Math.Max(1, Abomanation.VentBlast / Abomanation.VentPulses);

                if (into > 0 && into <= Abomanation.VentBlast && into % every == every / 2)
                    VentHeals(into / every);
            }

            if (attackTimer < Abomanation.VentTell + Abomanation.VentBlast + Abomanation.VentAfter)
                return;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                ventWait = Main.rand.Next(Abomanation.VentGapLeast, Abomanation.VentGapMost + 1);

                if (PADWorld.Legends)
                    ventWait = (int)(ventWait * Abomanation.LegendsMoveGate);
            }

            Sink(moveDir);
        }

        /// <summary>
        /// One part of what the vent gives back: part <paramref name="pulse"/> of VentPulses,
        /// counted from 0. The parts are cut so they add up to the whole exactly, whatever
        /// the whole divides by. Never past the line the second half starts at.
        /// </summary>
        private void VentHeals(int pulse)
        {
            int whole = (int)(NPC.lifeMax * Abomanation.VentHeal);
            int pulses = Abomanation.VentPulses;

            pulse = Math.Clamp(pulse, 0, pulses - 1);

            int part = whole * (pulse + 1) / pulses - whole * pulse / pulses;
            int cap = (int)(NPC.lifeMax * Abomanation.RagePoint);
            int heal = Math.Min(part, cap - NPC.life);

            if (heal <= 0)
                return;

            NPC.life += heal;
            NPC.HealEffect(heal, true);
            NPC.netUpdate = true;
        }

        // ------------------------------------------------------------------ testing ----

        /// <summary>
        /// For the /abomanation command: makes the next thing it does this one, as soon as
        /// it can start it from the chase. Returns false for a word it does not know. Only
        /// the deciding machine may call it.
        /// </summary>
        public bool Force(string what)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return false;

            int move;

            switch (what)
            {
                case "rush":
                case "over":
                    move = Abomanation.MoveRush;
                    forcedRush = 0;
                    break;

                case "lunge":
                case "through":
                    move = Abomanation.MoveRush;
                    forcedRush = 1;
                    break;

                case "puke":
                    move = Abomanation.MovePuke;
                    break;

                case "ash":
                    move = Abomanation.MoveAsh;
                    break;

                case "vent":
                    move = Abomanation.MoveVent;
                    break;

                case "spasm":
                    spasmWait = 1;
                    return true;

                case "whip":
                    if (phase == 0)
                        StartWhip();
                    return true;

                default:
                    return false;
            }

            whipOwed = 0;

            if (phase == 0)
            {
                // Due now. It still starts the way it would in a fight: a rush once the
                // head is below you, the others once it is under the ground (TryMove).
                nextMove = move;
                chargeTimer = moveGate;
            }
            else
            {
                // In the middle of something: the moment it is back in the chase.
                forcedMove = move;
            }

            return true;
        }
    }
}
