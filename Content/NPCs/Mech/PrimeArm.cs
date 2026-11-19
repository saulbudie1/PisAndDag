using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// One of Skeletron Prime's arms (cannon, saw, vice, laser), reworked with its head (<see cref="Prime"/>).
    ///
    ///   ITS LIMB        Vanilla draws two bones from each hand a set way toward its head, so the arm seems to stretch
    ///                   as the hand flies off (by request: "make it so that the limb length stay the same, because
    ///                   skeletron stretches it"; asked after: free-flying, a rigid limb). Here each arm is one rigid
    ///                   limb: its hand, and two bones in a line behind it, the length of it never changing. It flies
    ///                   free of its head as vanilla's hand does (vanilla's AI, its reach, its attacks), and lies along
    ///                   the way its hand points, its turning eased so the whole of it swings and does not snap. A
    ///                   lamp on its collar (red; green at its head's end), a jet's fire at its far end.
    ///   STRUCK DOWN     (by request: "each arm: non cinematic: at 1HP, invincible no damage no ai: falls down onto the
    ///                   ground, make it so that the limb is laid flat".) The blow that would kill it is refused: held
    ///                   at one life, untouchable, harmless, without its AI, its lamp going out, it falls, turning
    ///                   flat as it goes, lands (dust to both sides, a clank), and lies flat on the ground till its
    ///                   head's end comes for it. No camera. If its head goes (off with the day), it goes too.
    ///   ITS HEAD'S      Its coming: out from behind its head, its lamp lit, pointed at the sky, firing (FollowEnter).
    ///   SCENES          Its end: it stirs where it lies, lifts, flies to its head's mouth and covers it with its hand
    ///                   (FollowFinale); there it is drawn by its head, over it.
    ///   IN LEGENDS      Three times its life. Vanilla's, and between stretches of that an attack of its own
    ///                   (PrimeArm.Legends); and its part in what all its arms stop for (PrimeArm.Group).
    ///
    /// Not where it is the Mechdusa's (NPC.IsMechQueenUp): vanilla's, all of it. The server decides its fall; every
    /// machine plays it, and its head's scenes, from what it is told.
    /// </summary>
    public partial class PrimeArm : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => Prime.IsArm(entity.type);

        private enum State : byte
        {
            Up,
            Fall,
            Down,
        }

        private State state;

        /// <summary>Ticks in its state (fallen, lying).</summary>
        private int timer;

        /// <summary>Where it lies (its middle as the game has it), and the way it lies: 1 its tool to the left, -1 to the right.</summary>
        private Vector2 restAt;
        private int spin = 1;

        private bool ending;

        /// <summary>A blow that would kill it, dealt on this machine (a client), the server not yet answered.</summary>
        private int heldFor;
        private const int HeldTicks = 120;

        /// <summary>Made untouchable by this machine, and so to be given back.</summary>
        private bool shielded;

        // ---- only seen (this machine's) -----------------------------------------------------------------------------------

        internal float shake;
        private Vector2 drawShift;

        /// <summary>This machine has seen it land.</summary>
        private bool landed;

        /// <summary>Its lamp, 0 dark to 1 lit, and whether it is green (its head's end) or red.</summary>
        private float lamp;
        private float lampTo = 1f;
        private bool lampGreen;

        /// <summary>Not drawn at all (behind its head, before it comes out).</summary>
        private bool hidden;

        /// <summary>How far back along it from its hand it is drawn (coming out from behind its head, the rest is not yet out).</summary>
        private float shownBack = float.MaxValue;

        /// <summary>Drawn by its head, over it (its hand on its head's mouth).</summary>
        private bool front;

        /// <summary>Moved by a scene or its fall this tick, not by vanilla.</summary>
        private bool driven;

        /// <summary>Its jet burns this tick.</summary>
        private bool thrust;

        /// <summary>Its turn, eased after vanilla's AI.</summary>
        private float eased;
        private bool easedSet;

        /// <summary>Where it began its flight to its head, and how it was turned (this machine's).</summary>
        private Vector2 flyFrom;
        private float flyTurn;
        private bool flying;

        /// <summary>This machine has seen it struck.</summary>
        private bool struck;

        /// <summary>
        /// The step of its head's scene it last followed, and the furthest that step's clock has been seen (this
        /// machine's): what happens at a tick happens as the clock passes it (<see cref="At"/>), so a clock set on a
        /// little skips nothing and one set back repeats nothing.
        /// </summary>
        private int seenStep = -1;
        private int seenTick = -1;
        private int clock;

        internal bool Up => state == State.Up;
        internal bool Ending => ending;
        internal bool Front => front;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        // ---- its shape ----------------------------------------------------------------------------------------------------
        //
        // Measured off vanilla's pictures: each hand a round cap (rows 0 to 18) over a collar (20 to 30) over its tool,
        // 38 or 40 wide; the cannon 76 high, the saw 78, the vice 70, the laser 62. The bone (Arm_Bone_2) 26 by 98. A
        // hand turned 0 points its tool straight down; its limb runs the other way: a bone tucked 12 into its cap, a
        // second lapping the first by 16.

        private const float BoneLong = 98f;
        private const float CapTuck = 12f;
        private const float ElbowLap = 16f;

        /// <summary>How far down a hand's picture its lamp sits (on its collar).</summary>
        private const float LampRow = 25f;

        /// <summary>How far over the ground its middle line is when it lies flat (its cap a little sunk in).</summary>
        private const float LieHigh = 15f;

        /// <summary>How much of the way to vanilla's turn it goes each tick.</summary>
        private const float TurnShare = 0.14f;

        /// <summary>How near its place on its head's mouth it is drawn by its head, over it, as it flies there.</summary>
        private const float FrontNear = 320f;

        internal static float HandHigh(int type) => type switch
        {
            NPCID.PrimeCannon => 76f,
            NPCID.PrimeSaw => 78f,
            NPCID.PrimeVice => 70f,
            _ => 62f,
        };

        /// <summary>The way a hand turned so points its tool.</summary>
        internal static Vector2 ToolWay(float rotation) => new(-MathF.Sin(rotation), MathF.Cos(rotation));

        /// <summary>The turn that points a hand's tool a way.</summary>
        internal static float TurnOf(Vector2 toolWay) => MathF.Atan2(toolWay.Y, toolWay.X) - MathHelper.PiOver2;

        /// <summary>The middle of its hand's picture: what it is drawn about, and turns about.</summary>
        internal static Vector2 Pivot(NPC npc) =>
            new(npc.Center.X, npc.position.Y + npc.height + 4f - HandHigh(npc.type) * npc.scale * 0.5f + npc.gfxOffY);

        /// <summary>From its hand's middle back to the middle of a bone (0 the nearer), at its own size.</summary>
        private static float BoneBack(int type, int which) =>
            HandHigh(type) * 0.5f + BoneLong * 0.5f - CapTuck + which * (BoneLong - ElbowLap);

        /// <summary>From its hand's middle back to its limb's far end.</summary>
        internal static float TailBack(int type) => BoneBack(type, 1) + BoneLong * 0.5f;

        /// <summary>Its tool's end.</summary>
        internal static Vector2 Tip(NPC npc) => Pivot(npc) + ToolWay(npc.rotation) * HandHigh(npc.type) * 0.5f * npc.scale;

        /// <summary>Its limb's far end: its jet.</summary>
        internal static Vector2 Tail(NPC npc) => Pivot(npc) - ToolWay(npc.rotation) * TailBack(npc.type) * npc.scale;

        /// <summary>The middle of its whole limb.</summary>
        internal static Vector2 LimbMiddle(NPC npc) => (Tip(npc) + Tail(npc)) * 0.5f;

        private static Vector2 LampAt(NPC npc) => Pivot(npc) - ToolWay(npc.rotation) * (HandHigh(npc.type) * 0.5f - LampRow) * npc.scale;

        /// <summary>To this place this tick, its hand's middle, by its velocity (the game moves it after its AI).</summary>
        private static void MovePivot(NPC npc, Vector2 to) => npc.velocity = to - Pivot(npc);

        /// <summary>Its head, if it is here.</summary>
        internal static bool HeadOf(NPC arm, out NPC head, out Prime prime)
        {
            head = null;
            prime = null;
            int i = (int)arm.ai[1];

            if (i < 0 || i >= Main.maxNPCs)
                return false;

            NPC h = Main.npc[i];

            if (!h.active || h.type != NPCID.SkeletronPrime || !h.TryGetGlobalNPC(out prime))
                return false;

            head = h;
            return true;
        }

        // ---- the AI -------------------------------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            hidden = false;
            front = false;
            driven = false;
            thrust = false;
            shownBack = float.MaxValue;
            shake = 0f;
            hurts = false;
            hurtScale = 1f;
            beam = false;
            jaw = -1;
            rev = Math.Max(0f, rev - 0.05f);
            charge = Math.Max(0f, charge - 0.06f);

            if (NPC.IsMechQueenUp)
            {
                Unshield(npc);
                return true;
            }

            bool haveHead = HeadOf(npc, out NPC head, out Prime prime);

            // As vanilla's AI turns its picture (the cannon and the vice the other way about), whoever moves it.
            npc.spriteDirection = -(int)npc.ai[0];

            if (heldFor > 0)
            {
                if (state != State.Up)
                    heldFor = 0;
                else
                    heldFor--;
            }

            // Its head's coming: moved by it.
            if (haveHead && state == State.Up && prime.Now == Prime.Scene.Enter)
            {
                Shield(npc);
                driven = true;
                Follow(npc, head, prime);
                FollowEnter(npc, head, prime);
                seenTick = Math.Max(seenTick, clock);
                Seen(npc);
                return false;
            }

            flying = flying && haveHead && prime.Now == Prime.Scene.Finale;

            if (state == State.Up)
            {
                // Its head gone: vanilla's AI, which ends it.
                if (!haveHead)
                {
                    Unshield(npc);
                    return true;
                }

                // Untouchable while held, and on a client not yet told of its head's coming.
                if (heldFor > 0 || !Authority && !prime.Started)
                    Shield(npc);
                else
                    Unshield(npc);

                lampTo = 1f;
                lampGreen = false;
                thrust = true;

                // Legends: its part in what all its arms stop for, or an attack of its own; or vanilla's, till the next.
                bool moved = Legends(npc, head, prime);
                Seen(npc);
                return !moved;
            }

            // Struck down: held at one life, untouchable, harmless, without its AI.
            Shield(npc);
            driven = true;
            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);
            Prime.StripBuffs(npc);

            if (!haveHead)
            {
                Gone(npc);
                return false;
            }

            if (prime.Now == Prime.Scene.Finale && prime.Step >= Prime.FinStir && (state == State.Down || prime.Step >= Prime.FinFly))
            {
                Follow(npc, head, prime);
                FollowFinale(npc, head, prime);
                seenTick = Math.Max(seenTick, clock);
            }
            else if (state == State.Fall)
                RunFall(npc);
            else
                Lie(npc);

            Seen(npc);
            timer++;
            return false;
        }

        /// <summary>
        /// What Legends has it do this tick, if anything but vanilla's: its part in its head's attack of all its arms,
        /// or its own attack (which may begin here, on the server). True if it was moved here: vanilla's AI is not to run.
        /// </summary>
        private bool Legends(NPC npc, NPC head, Prime prime)
        {
            bool on = PADWorld.Legends && prime.Started;

            if (on && prime.Now == Prime.Scene.Group)
            {
                // What it was at of its own is dropped for it; vanilla's count kept for after.
                if (Authority && act != Act.None)
                    DropAct(npc, prime);
                else if (Authority && !inGroup)
                    savedCount = npc.ai[3];

                inGroup = true;
                driven = true;
                npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);
                Follow(npc, head, prime);
                FollowGroup(npc, head, prime);
                seenTick = Math.Max(seenTick, clock);
                return true;
            }

            if (inGroup)
                LeaveGroup(npc);

            if (!on || prime.Now != Prime.Scene.None || heldFor > 0)
            {
                if (act != Act.None && Authority)
                {
                    DropAct(npc, prime);
                    BackToVanilla(npc);
                }

                return false;
            }

            if (act == Act.None)
                LegendsBetween(npc, head, prime);

            if (act == Act.None)
                return false;

            driven = true;
            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);
            RunAct(npc, head, prime);
            seenTick = Math.Max(seenTick, clock);

            if (act != Act.None)
                actT++;

            return true;
        }

        /// <summary>Its head's step and clock this tick, for what happens at a tick of it; and where it was, and how turned, as the step began.</summary>
        private void Follow(NPC npc, NPC head, Prime prime)
        {
            int at = (int)prime.Now * 100 + prime.Step;

            if (at != seenStep)
            {
                seenStep = at;
                seenTick = -1;
                stepFrom = Pivot(npc);
                stepTurn = npc.rotation;
            }

            clock = prime.ArmClock(head, npc);
        }

        /// <summary>Its head's clock passes this tick of its step now (once, on this machine; not one long past).</summary>
        private bool At(int tick) => seenTick < tick && clock >= tick && clock - tick <= Prime.Late;

        public override void PostAI(NPC npc)
        {
            if (NPC.IsMechQueenUp)
                return;

            // Vanilla sets its turn afresh each tick (toward its rest, or onto the player), and jumps from one to the
            // other: eased here, so its whole limb swings round.
            if (driven || !easedSet)
            {
                eased = npc.rotation;
                easedSet = true;
                return;
            }

            eased = eased.AngleLerp(npc.rotation, TurnShare);
            npc.rotation = eased;
        }

        private void Shield(NPC npc)
        {
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            shielded = true;
        }

        private void Unshield(NPC npc)
        {
            if (!shielded)
                return;

            shielded = false;
            npc.dontTakeDamage = false;
            npc.chaseable = true;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) =>
            NPC.IsMechQueenUp || state == State.Up && heldFor <= 0 && (!driven || hurts);

        public override void FindFrame(NPC npc, int frameHeight)
        {
            if (NPC.IsMechQueenUp)
                return;

            // Struck down, its saw does not turn nor its vice work (till its head's end wakes it).
            if (state != State.Up && !flying && !front)
            {
                npc.frame.Y = 0;
                npc.frameCounter = 0;
                return;
            }

            // Legends: its vice's jaws as its attack has them (shut, or open); its saw as quick as its two pictures can show.
            if (npc.type == NPCID.PrimeVice && jaw >= 0)
                npc.frame.Y = jaw * frameHeight;
            else if (npc.type == NPCID.PrimeSaw && rev > 0.3f)
                npc.frame.Y = (int)(Main.GameUpdateCount % 2) * frameHeight;
        }

        // ---- struck down --------------------------------------------------------------------------------------------------

        /// <summary>The blow that would kill it is refused, and it falls; with its head gone it dies as vanilla's does.</summary>
        public override bool CheckDead(NPC npc)
        {
            if (NPC.IsMechQueenUp || ending || !HeadOf(npc, out _, out _))
                return true;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (state == State.Up)
                {
                    heldFor = HeldTicks;
                    shielded = true;
                }

                npc.life = 1;
                npc.dontTakeDamage = true;
                npc.active = true;
                return false;
            }

            if (state == State.Up)
                BeginFall(npc);

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;
            return false;
        }

        /// <summary>Handed back to vanilla to die (its head's end: the blast throws its pieces).</summary>
        internal void End(NPC npc)
        {
            if (ending)
                return;

            ending = true;
            npc.DeathSound = null;
            npc.dontTakeDamage = false;
            npc.life = 0;
            npc.checkDead();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        /// <summary>Its head gone (off with the day, or its player): it goes too, as vanilla's does, on every machine.</summary>
        private void Gone(NPC npc)
        {
            ending = true;
            npc.life = -1;
            npc.HitEffect();
            npc.active = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        /// <summary>It is struck down: where it will lie is worked out, and it falls. The server's.</summary>
        internal void BeginFall(NPC npc)
        {
            if (state != State.Up)
                return;

            // Whatever of Legends it was at is dropped: nothing held, nothing firing.
            if (act != Act.None || inGroup)
                DropAct(npc, HeadOf(npc, out _, out Prime prime) ? prime : null);

            // The way it will lie: its tool to the side it already leans.
            spin = ToolWay(npc.rotation).X <= 0f ? 1 : -1;

            float x = npc.Center.X + MathHelper.Clamp(npc.velocity.X * 8f, -90f, 90f);
            float fromPivot = npc.Center.Y - Pivot(npc).Y;
            restAt = new Vector2(x, Ground(npc, x) - LieHigh * npc.scale + fromPivot);

            state = State.Fall;
            timer = 0;
            landed = false;
            struck = false;
            heldFor = 0;
            npc.netUpdate = true;
        }

        /// <summary>
        /// The ground it will lie on, in world pixels: the highest top (solid, or a platform) under any part of it as it
        /// will lie, from its own height down; under a chasm, as far as two hundred and sixty tiles; the world's floor if
        /// nothing. A column it is inside of counts only if its top is near (three tiles): it does not climb a wall.
        /// </summary>
        private float Ground(NPC npc, float x)
        {
            float tip = x + -spin * HandHigh(npc.type) * 0.5f * npc.scale;
            float tail = x + spin * TailBack(npc.type) * npc.scale;
            int x0 = (int)(Math.Min(tip, tail) / 16f), x1 = (int)(Math.Max(tip, tail) / 16f);
            int y0 = Math.Max(10, (int)(Pivot(npc).Y / 16f));
            int bottom = Math.Min(Main.maxTilesY - 12, y0 + 260);
            int best = bottom;

            for (int tx = x0; tx <= x1; tx++)
            {
                if (!WorldGen.InWorld(tx, y0, 12))
                    continue;

                if (Standable(tx, y0))
                {
                    // Inside the ground here: up to its top, if that is near.
                    int y = y0;

                    while (y > y0 - 4 && Standable(tx, y - 1))
                        y--;

                    if (!Standable(tx, y - 1))
                        best = Math.Min(best, y);

                    continue;
                }

                for (int y = y0 + 1; y < best; y++)
                {
                    if (Standable(tx, y))
                    {
                        best = y;
                        break;
                    }
                }
            }

            return best * 16f;
        }

        private static bool Standable(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        private float Flat => spin * MathHelper.PiOver2;

        /// <summary>It falls: turning flat as it goes, its lamp guttering out, smoke after it; onto the ground, where the server lays it down.</summary>
        private void RunFall(NPC npc)
        {
            int t = timer;

            if (!struck)
            {
                // Knocked up a little by the blow that did it.
                struck = true;
                npc.velocity.Y = Math.Min(npc.velocity.Y, 0f) - 3f;
                StruckFx(npc);
            }

            npc.rotation = npc.rotation.AngleLerp(Flat, 0.05f + 0.1f * Math.Min(1f, t / 30f));
            npc.velocity = new Vector2((restAt.X - npc.Center.X) * 0.1f, Math.Min(npc.velocity.Y + 0.55f, 22f));

            lampGreen = false;
            lampTo = t < 36 && Prime.Chance(npc.whoAmI * 97 + t / 3) > t / 36f ? 1f : 0f;
            FallSmoke(npc);

            if (npc.Center.Y + npc.velocity.Y >= restAt.Y)
            {
                npc.velocity.Y = restAt.Y - npc.Center.Y;

                if (Authority)
                {
                    state = State.Down;
                    timer = -1;
                    npc.netUpdate = true;
                }
            }
        }

        /// <summary>Flat on the ground: a bounce off it, then still; a wisp of smoke, now and then a spark.</summary>
        private void Lie(NPC npc)
        {
            int t = timer;

            if (!landed)
            {
                landed = true;
                LandFx(npc);
            }

            float bounce = t < 20 ? -9f * Math.Abs(MathF.Sin(t * MathHelper.Pi / 10f)) * (1f - t / 20f) : 0f;
            npc.velocity = restAt + new Vector2(0f, bounce) - npc.Center;
            npc.rotation = npc.rotation.AngleLerp(Flat, 0.35f);

            if (t < 10)
                shake = 2f * (1f - t / 10f);

            lampTo = 0f;
            LieFx(npc);
        }

        // ---- its head's coming (Prime.Enter) ------------------------------------------------------------------------------

        private void FollowEnter(NPC npc, NPC head, Prime prime)
        {
            int step = prime.Step, t = prime.ArmClock(head, npc);
            int type = npc.type;
            bool upper = Prime.Upper(type);
            float s = head.scale;
            Vector2 middle = head.Center;
            Vector2 outAt = Prime.OutSpot(type) * s, upAt = Prime.UpSpot(type) * s;
            float outTurn = TurnOf(outAt), upTurn = TurnOf(-Vector2.UnitY);

            lampGreen = false;
            lampTo = 0f;

            // Behind its head till its pair's turn.
            if (step < Prime.EnPairA || step == Prime.EnPairA && !upper)
            {
                hidden = true;
                npc.rotation = outTurn;
                MovePivot(npc, middle);
                return;
            }

            thrust = true;

            if (step == Prime.EnPairA || step == Prime.EnPairB && !upper)
            {
                // Out from behind it, hand first, a little past its place and back.
                float u = Math.Min(1f, (t + 1f) / Prime.PairOut);
                float v = 1f + 2.1f * MathF.Pow(u - 1f, 3f) + 1.1f * MathF.Pow(u - 1f, 2f);
                npc.rotation = outTurn;
                MovePivot(npc, middle + outAt * v);

                // Only what has come out past its head's middle is drawn.
                shownBack = outAt.Length() * v - 14f;
                hidden = shownBack < 0f;
                return;
            }

            if (step == Prime.EnPairB)
            {
                npc.rotation = outTurn;
                MovePivot(npc, middle + outAt);
                return;
            }

            if (step == Prime.EnOn)
            {
                int at = Prime.LampFirst + Prime.LampEvery * Prime.LampTurn(type);
                npc.rotation = outTurn;
                MovePivot(npc, middle + outAt);

                if (At(at))
                    LampFx(npc);

                lampTo = t >= at ? 1f : 0f;
                return;
            }

            lampTo = 1f;

            if (step == Prime.EnAim)
            {
                float u = Prime.Smooth((t + 1f) / Prime.EnAimTicks);
                npc.rotation = outTurn.AngleLerp(upTurn, u);
                MovePivot(npc, middle + Vector2.Lerp(outAt, upAt, u));
                return;
            }

            // Pointed at the sky, firing: kicked back down by each shot.
            float kick = 0f;

            foreach (int volley in Prime.Volleys)
            {
                if (At(volley))
                    FireFx(npc);

                if (t >= volley)
                    kick = Math.Max(kick, 18f * MathF.Exp(-(t - volley) / 5f));
            }

            npc.rotation = upTurn;
            MovePivot(npc, middle + upAt + new Vector2(0f, kick));
        }

        // ---- its head's end (Prime.Finale) --------------------------------------------------------------------------------

        private void FollowFinale(NPC npc, NPC head, Prime prime)
        {
            int step = prime.Step, t = prime.ArmClock(head, npc);
            float half = HandHigh(npc.type) * 0.5f * npc.scale;

            lampGreen = true;

            if (step == Prime.FinStir)
            {
                // It stirs where it lies: shaking, its lamp blinking on green; then it lifts and hangs, its hand
                // turning toward its head.
                lampTo = t < 10 ? 0f : t < Prime.LiftAt ? (t / 5 % 2 == 0 ? 1f : 0.15f) : 1f;

                if (At(10))
                    WakeFx(npc);

                float lift = Prime.Smooth((t - Prime.LiftAt) / (float)(Prime.FinStirTicks - Prime.LiftAt));

                if (t < Prime.LiftAt)
                {
                    shake = 0.6f + 3.4f * (t / (float)Prime.LiftAt) * (t / (float)Prime.LiftAt);

                    if (t % 8 == 3)
                        RattleFx(npc);
                }
                else
                {
                    thrust = true;

                    if (At(Prime.LiftAt))
                        LiftFx(npc);
                }

                Vector2 at = restAt + new Vector2(0f, -Prime.StirHigh * lift + 3f * lift * MathF.Sin(t * 0.2f));
                npc.velocity = at - npc.Center;

                float toHead = TurnOf((prime.Mouth(head) - Pivot(npc)).SafeNormalize(Vector2.UnitY));
                npc.rotation = Flat.AngleLerp(toHead, lift * 0.55f);
                return;
            }

            lampTo = 1f;
            thrust = true;

            if (step == Prime.FinFly)
            {
                if (!flying)
                {
                    flying = true;
                    flyFrom = Pivot(npc);
                    flyTurn = npc.rotation;
                    FlyFx(npc);
                }

                // From where it hung to its place on its head's mouth, quicker and quicker, a little out to one side
                // of the straight way; there at the last tick.
                prime.CoverPose(head, npc.type, half, 0f, out Vector2 to, out float turn);
                float u = Math.Min(1f, (t + 1f) / Prime.FinFlyTicks);
                float e = MathF.Pow(u, 2.2f);
                Vector2 way = to - flyFrom;
                Vector2 side = new Vector2(-way.Y, way.X).SafeNormalize(Vector2.Zero) * Prime.SideOf(npc.type);
                Vector2 at = Vector2.Lerp(flyFrom, to, e) + side * MathF.Sin(e * MathHelper.Pi) * Math.Min(150f, way.Length() * 0.22f);

                MovePivot(npc, at);
                npc.rotation = flyTurn.AngleLerp(turn, Prime.Smooth(u * 1.15f));
                TrailFx(npc);

                // Drawn by its head, over it, once it is near it (far off, its head may not be drawn at all).
                front = Vector2.DistanceSquared(at, to) < FrontNear * FrontNear;
                return;
            }

            // Its hand on its head's mouth: thrown back by what is behind it, and forced down again.
            front = true;
            flying = true;
            float push = step == Prime.FinDark ? Prime.PushAt(t, npc.type) : 0f;
            prime.CoverPose(head, npc.type, half, push, out Vector2 pivot, out float covered);
            MovePivot(npc, pivot);
            npc.rotation = covered;
        }

        // ---- what is seen and heard ---------------------------------------------------------------------------------------

        private static readonly Color Soot = new(16, 14, 14);

        /// <summary>Once a tick, whatever moves it: its lamp eased, its jet's fire, its lamp's light.</summary>
        private void Seen(NPC npc)
        {
            if (Main.dedServ)
                return;

            lamp += (lampTo - lamp) * 0.35f;

            if (hidden)
                return;

            if (lamp > 0.05f)
            {
                Color c = lampGreen ? PrimeArt.Nuclear : PrimeArt.Red;
                Vector2 at = LampAt(npc);
                Lighting.AddLight(at, c.R / 255f * 0.5f * lamp, c.G / 255f * 0.5f * lamp, c.B / 255f * 0.5f * lamp);
            }

            // Not while its far end is behind its head's face (out in its coming's X, all four ends are): dust is drawn
            // over every NPC, so its fire would sit on the skull.
            if (thrust && shownBack > TailBack(npc.type) * npc.scale && Prime.Seen(npc.Center, 300f) && !BehindFace(npc))
            {
                // As vanilla's own: fire at its limb's far end, blown back off it.
                Vector2 back = -ToolWay(npc.rotation);
                Dust dust = Dust.NewDustPerfect(Tail(npc) + Main.rand.NextVector2Circular(5f, 5f), lampGreen ? DustID.CursedTorch : DustID.Torch,
                    back * Main.rand.NextFloat(1f, 3f) + npc.velocity * 0.25f, 100, default, Main.rand.NextFloat(1.5f, 2.3f));
                dust.noGravity = true;
            }
        }

        /// <summary>How near its head's middle its far end must be to be behind its face.</summary>
        private const float FaceReach = 64f;

        /// <summary>Its limb's far end is behind its head's face.</summary>
        private static bool BehindFace(NPC npc) =>
            HeadOf(npc, out NPC head, out _) && Vector2.DistanceSquared(Tail(npc), Prime.Middle(head)) < FaceReach * FaceReach * head.scale * head.scale;

        private void StruckFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 300f))
                return;

            Vector2 at = LimbMiddle(npc);
            Vector2 way = ToolWay(npc.rotation);

            for (int i = 0; i < 3; i++)
            {
                Vector2 on = Vector2.Lerp(Tip(npc), Tail(npc), Main.rand.NextFloat());
                Prime.Arc(on, on + Main.rand.NextVector2Circular(26f, 26f), PrimeArt.Red, Main.rand.NextFloat(8f, 12f), Main.rand.Next(12, 18), Main.rand.Next(2));
            }

            Prime.Add(new BloomGeneric(LampAt(npc), Vector2.Zero, PrimeArt.Red * 0.8f, 0.5f, 12));

            for (int i = 0; i < 8; i++)
            {
                Vector2 on = Vector2.Lerp(Tip(npc), Tail(npc), Main.rand.NextFloat());
                Prime.Add(new BurnSmoke(on, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.6f, 1.8f)), Soot, new Color(150, 44, 18),
                    Main.rand.Next(40, 70), 12f, 40f, 0.9f));
            }

            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustPerfect(at + way * Main.rand.NextFloat(-90f, 90f), DustID.Torch, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.4f);
                dust.noGravity = Main.rand.NextBool(3);
            }

            Prime.Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.5f }, at);
            Prime.Sound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.2f }, at);
            Prime.Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.45f, Pitch = -0.5f }, at);
            Prime.Shake(at, 5f, 14);
        }

        private void FallSmoke(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 300f) || Main.rand.NextFloat() > 0.8f * Prime.Room)
                return;

            Vector2 on = Vector2.Lerp(Pivot(npc), Tail(npc), Main.rand.NextFloat());
            Prime.Add(new BurnSmoke(on, npc.velocity * 0.1f + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.3f, 1f)), Soot, new Color(150, 44, 18),
                Main.rand.Next(36, 60), 10f, 34f, 0.85f));
        }

        /// <summary>It lands flat: dust and stone out to both sides along the ground, the ground's own dust, a clank. Not a ring: two spouts.</summary>
        private void LandFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 400f))
                return;

            float ground = Pivot(npc).Y + LieHigh * npc.scale;
            Vector2 middle = new(LimbMiddle(npc).X, ground);
            float reach = (TailBack(npc.type) + HandHigh(npc.type) * 0.5f) * 0.5f * npc.scale;
            Color dust = new(118, 104, 92);
            float room = Prime.Room;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 spout = new Vector2(side * 0.8f, -1f).SafeNormalize(-Vector2.UnitY);
                Vector2 end = middle + new Vector2(side * reach, 0f);

                for (int i = 0; i < (int)(7 * room); i++)
                    Prime.Add(new StoneChip(end + new Vector2(side * Main.rand.NextFloat(0f, 20f), -4f), spout.RotatedByRandom(0.45f) * Main.rand.NextFloat(3f, 8f),
                        Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0f, 0.4f)), Main.rand.Next(40, 70), Main.rand.NextFloat(0.7f, 1.1f)));

                for (int i = 0; i < (int)(8 * room); i++)
                    Prime.Add(new DustFog(middle + new Vector2(side * Main.rand.NextFloat(0f, reach), -6f), new Vector2(side * Main.rand.NextFloat(0.8f, 3.6f), -Main.rand.NextFloat(0.2f, 1f)),
                        dust * 0.8f, Main.rand.Next(40, 70), Main.rand.NextFloat(0.5f, 0.9f), 0.3f));
            }

            int y0 = (int)(ground / 16f);

            for (int i = 0; i < 14; i++)
            {
                int tx = (int)((middle.X + Main.rand.NextFloat(-reach, reach)) / 16f), ty = y0 + Main.rand.Next(0, 2);

                if (!WorldGen.InWorld(tx, ty, 2) || !Main.tile[tx, ty].HasTile)
                    continue;

                int d = WorldGen.KillTile_MakeTileDust(tx, ty, Main.tile[tx, ty]);

                if (d >= 0 && d < Main.maxDust)
                    Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 5f));
            }

            Prime.Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.65f }, middle, 8);
            Prime.Sound(SoundID.DD2_OgreGroundPound with { Volume = 0.6f, Pitch = 0.1f }, middle, 8);
            Prime.Sound(SoundID.DeerclopsRubbleAttack with { Volume = 0.5f, Pitch = -0.1f }, middle, 8);
            Prime.Shake(middle, 9f, 18);
        }

        private void LieFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 300f))
                return;

            if (Main.rand.NextBool(22))
            {
                Vector2 on = Vector2.Lerp(Pivot(npc), Tail(npc), Main.rand.NextFloat());
                Prime.Add(new MuzzleSmoke(on, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 0.9f)), Main.rand.Next(30, 46), Main.rand.NextFloat(0.2f, 0.32f)));
            }

            if (Main.rand.NextBool(210))
            {
                Vector2 on = Vector2.Lerp(Tip(npc), Tail(npc), Main.rand.NextFloat());
                Prime.Arc(on, on + Main.rand.NextVector2Circular(18f, 14f), PrimeArt.Red, 8f, 12, 1);
                Prime.Sound(SoundID.Item93 with { Volume = 0.3f, Pitch = Main.rand.NextFloat(-0.2f, 0.3f) }, on);
            }
        }

        /// <summary>Its lamp comes on (its head's coming): a beep, a red glint.</summary>
        private static void LampFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = LampAt(npc);
            Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.Red * 0.9f, 0.45f, 14));
            Prime.Add(new BloomGeneric(at, Vector2.Zero, Color.White * 0.6f, 0.16f, 8));
            Prime.Sound(Prime.BeepSound with { Pitch = 0.08f * Prime.LampTurn(npc.type) }, at, 6);
        }

        /// <summary>It fires at the sky: a flash at its tool's end, a streak of red up out of it, smoke; each tool its own sound.</summary>
        private static void FireFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 tip = Tip(npc);
            Vector2 up = ToolWay(npc.rotation);

            Prime.Add(new BloomGeneric(tip, Vector2.Zero, PrimeArt.Red * 0.9f, 0.9f, 10));
            Prime.Add(new BloomGeneric(tip, Vector2.Zero, PrimeArt.RedHot * 0.8f, 0.4f, 6));

            // The shot: gone up off the top of the screen in a moment.
            PADParticleHandler.SpawnParticle(new SparkShard(tip + up * 20f, up * 30f, PrimeArt.Red, 70f, 7f, 220f, 18, falls: false, spine: true, drawOut: 0.6f));

            for (int i = 0; i < 4; i++)
                PADParticleHandler.SpawnParticle(new SparkShard(tip, up.RotatedByRandom(0.5f) * Main.rand.NextFloat(5f, 12f), PrimeArt.RedHot, 14f, 3f, 40f, Main.rand.Next(10, 16), falls: true));

            for (int i = 0; i < 4; i++)
                Prime.Add(new MuzzleSmoke(tip + up * 6f, up.RotatedByRandom(0.7f) * Main.rand.NextFloat(0.6f, 2.2f), Main.rand.Next(26, 44), Main.rand.NextFloat(0.3f, 0.5f)));

            Lighting.AddLight(tip, 1.4f, 0.4f, 0.3f);

            switch (npc.type)
            {
                case NPCID.PrimeCannon:
                    Prime.Sound(SoundID.Item61 with { Volume = 0.9f, Pitch = -0.3f }, tip, 8);
                    break;

                case NPCID.PrimeLaser:
                    Prime.Sound(SoundID.Item33 with { Volume = 0.8f, Pitch = -0.1f }, tip, 8);
                    break;

                case NPCID.PrimeVice:
                    Prime.Sound(SoundID.Item11 with { Volume = 0.8f, Pitch = -0.5f }, tip, 8);
                    break;

                default:
                    Prime.Sound(SoundID.Item11 with { Volume = 0.8f, Pitch = -0.25f }, tip, 8);
                    break;
            }
        }

        /// <summary>Its lamp comes on green where it lies: a beep, lower; a spark of its head's green.</summary>
        private static void WakeFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 400f))
                return;

            Vector2 at = LampAt(npc);
            Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.9f, 0.5f, 16));
            Prime.Arc(at, at + Main.rand.NextVector2Circular(30f, 24f), PrimeArt.Nuclear, 9f, 14, 1);
            Prime.Sound(Prime.BeepSound with { Pitch = -0.35f }, at, 6);
        }

        private void RattleFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 400f))
                return;

            Vector2 on = Vector2.Lerp(Tip(npc), Tail(npc), Main.rand.NextFloat());
            Prime.Sound(SoundID.NPCHit4 with { Volume = 0.3f, Pitch = Main.rand.NextFloat(-0.6f, -0.2f) }, on);

            int x = (int)(on.X / 16f), y = (int)((Pivot(npc).Y + LieHigh * npc.scale + 4f) / 16f);

            if (WorldGen.InWorld(x, y, 2) && Main.tile[x, y].HasTile)
            {
                int d = WorldGen.KillTile_MakeTileDust(x, y, Main.tile[x, y]);

                if (d >= 0 && d < Main.maxDust)
                    Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f));
            }

            if (Main.rand.NextBool(2))
                Prime.Arc(on, on + Main.rand.NextVector2Circular(22f, 18f), PrimeArt.Nuclear, 8f, 12, 0);
        }

        /// <summary>It lifts off the ground: dust blown off under it, a servo's whir, its jet catching.</summary>
        private void LiftFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 400f))
                return;

            float ground = Pivot(npc).Y + LieHigh * npc.scale;
            Vector2 middle = new(LimbMiddle(npc).X, ground);

            for (int i = 0; i < (int)(10 * Prime.Room); i++)
                Prime.Add(new DustFog(middle + new Vector2(Main.rand.NextFloat(-100f, 100f), -4f), new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.2f, 0.9f)),
                    new Color(118, 104, 92) * 0.75f, Main.rand.Next(36, 60), Main.rand.NextFloat(0.5f, 0.8f), 0.28f));

            Prime.Sound(SoundID.Mech with { Volume = 0.8f, Pitch = -0.3f }, middle);
            Prime.Sound(SoundID.Item24 with { Volume = 0.5f, Pitch = -0.4f }, middle);
        }

        private static void FlyFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 500f))
                return;

            Prime.Sound(SoundID.DD2_BallistaTowerShot with { Volume = 0.7f, Pitch = -0.2f }, npc.Center, 6);
            Prime.Sound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.3f }, npc.Center, 6);
        }

        /// <summary>As it flies to its head: its head's green left behind its jet.</summary>
        private static void TrailFx(NPC npc)
        {
            if (Main.dedServ || !Prime.Seen(npc.Center, 300f))
                return;

            Vector2 tail = Tail(npc);

            if (Main.rand.NextFloat() < 0.7f * Prime.Room)
                Prime.Add(new HoneyMote(tail + Main.rand.NextVector2Circular(6f, 6f), -npc.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(16, 28), 0.01f));

            Lighting.AddLight(tail, 0.45f, 0.62f, 0f);
        }

        // ---- drawing ------------------------------------------------------------------------------------------------------

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (NPC.IsMechQueenUp || npc.IsABestiaryIconDummy)
                return true;

            if (hidden || front)
                return false;

            // Its shake, only seen: drawn off where it is, put back in PostDraw (which always follows).
            if (shake > 0.05f)
            {
                drawShift = Main.rand.NextVector2Circular(shake, shake);
                npc.position += drawShift;
            }

            DrawLimb(npc, spriteBatch, screenPos, state == State.Up);
            return false;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            npc.position -= drawShift;
            drawShift = Vector2.Zero;

            // Legends: what its own attack warns with.
            if (act != Act.None && state == State.Up && !NPC.IsMechQueenUp && !npc.IsABestiaryIconDummy)
                DrawOwnMarks(npc, spriteBatch);
        }

        /// <summary>
        /// Its limb as it is: its two bones (the far one first, each lit by where it is, as vanilla lights its own), its
        /// hand over them (with the faint copies of where it was that vanilla draws behind the mechanical bosses, if
        /// asked), the laser's own glow, its lamp.
        /// </summary>
        internal void DrawLimb(NPC npc, SpriteBatch batch, Vector2 screenPos, bool trailing)
        {
            Main.instance.LoadNPC(npc.type);
            Texture2D hand = TextureAssets.Npc[npc.type].Value;
            Texture2D bone = TextureAssets.BoneArm2.Value;

            // Its laser's lens: red, as vanilla's; in Legends its head's green (as its lasers are: by request), and at its head's end.
            bool greenLens = npc.type == NPCID.PrimeLaser && (PADWorld.Legends || lampGreen);

            if (greenLens)
                hand = PrimeArt.GreenOf(hand);

            float s = npc.scale, turn = npc.rotation;
            Vector2 way = ToolWay(turn);
            Vector2 pivot = Pivot(npc);
            Vector2 half = npc.frame.Size() * 0.5f;
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int k = 1; k >= 0; k--)
            {
                float back = BoneBack(npc.type, k) * s;

                if (back > shownBack)
                    continue;

                Vector2 at = pivot - way * back;
                Color lit = npc.GetAlpha(Lighting.GetColor((int)(at.X / 16f), (int)(at.Y / 16f)));
                batch.Draw(bone, at - screenPos, null, lit, turn, bone.Size() * 0.5f, s, SpriteEffects.None, 0f);
            }

            Color colour = npc.GetAlpha(npc.GetNPCColorTintedByBuffs(Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f))));

            if (trailing)
            {
                for (int k = Math.Min(9, npc.oldPos.Length - 1); k >= 1; k -= 2)
                    batch.Draw(hand, npc.oldPos[k] + (pivot - npc.position) - screenPos, npc.frame, colour * ((10 - k) / 20f), turn, half, s, flip, 0f);
            }

            batch.Draw(hand, pivot - screenPos, npc.frame, colour, turn, half, s, flip, 0f);

            if (npc.type == NPCID.PrimeLaser && lamp > 0.3f)
                batch.Draw(greenLens ? PrimeArt.GlowGreenOf(TextureAssets.BoneLaser.Value) : TextureAssets.BoneLaser.Value, pivot - screenPos, npc.frame, new Color(200, 200, 200, 0) * lamp, turn, half, s, flip, 0f);

            DrawLegendsOver(npc, batch, screenPos, hand, pivot, way, flip);
            DrawLamp(npc, batch, screenPos);
        }

        /// <summary>Its lamp: four pixels of its colour on its collar, a soft light about them.</summary>
        internal void DrawLamp(NPC npc, SpriteBatch batch, Vector2 screenPos)
        {
            Vector2 at = LampAt(npc) - screenPos;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new(0, 0, 1, 1);
            Color c = lampGreen ? PrimeArt.Nuclear : PrimeArt.Red;
            Color hot = lampGreen ? PrimeArt.NuclearHot : PrimeArt.RedHot;

            // Its glass, dark, whether it is lit or not.
            batch.Draw(pixel, at, one, new Color(34, 20, 22), npc.rotation, new Vector2(0.5f), 6f * npc.scale, SpriteEffects.None, 0f);

            if (lamp <= 0.03f)
                return;

            batch.Draw(pixel, at, one, Color.Lerp(c, hot, 0.35f) * lamp, npc.rotation, new Vector2(0.5f), 4f * npc.scale, SpriteEffects.None, 0f);

            Vector2 world = at + Main.screenPosition;
            DestroyerArt.DrawGlow(batch, world, 30f * npc.scale, PrimeArt.Lit(c * (0.55f * lamp)));
            DestroyerArt.DrawGlow(batch, world, 12f * npc.scale, PrimeArt.Lit(hot * (0.7f * lamp)), hotCore: true);
        }

        /// <summary>
        /// Its limb's shape alone, in one colour (its head's end: against its light, and in its impact frames):
        /// <paramref name="white"/> from its pictures' shapes made white (any colour), or else its own pictures drawn
        /// black.
        /// </summary>
        internal void DrawShape(NPC npc, SpriteBatch batch, Vector2 screenPos, Color colour, bool white, Vector2 shift)
        {
            Texture2D hand = TextureAssets.Npc[npc.type].Value;
            Texture2D bone = TextureAssets.BoneArm2.Value;

            if (white)
            {
                hand = PrimeArt.WhiteOf(hand);
                bone = PrimeArt.WhiteOf(bone);
            }

            float s = npc.scale, turn = npc.rotation;
            Vector2 way = ToolWay(turn);
            Vector2 pivot = Pivot(npc) + shift - screenPos;
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int k = 1; k >= 0; k--)
                batch.Draw(bone, pivot - way * BoneBack(npc.type, k) * s, null, colour, turn, bone.Size() * 0.5f, s, SpriteEffects.None, 0f);

            batch.Draw(hand, pivot, npc.frame, colour, turn, npc.frame.Size() * 0.5f, s, flip, 0f);
        }

        // ---- told across the network --------------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)state);
            binaryWriter.Write((short)Math.Clamp(timer, 0, short.MaxValue));
            binaryWriter.Write((sbyte)spin);
            binaryWriter.Write(restAt.X);
            binaryWriter.Write(restAt.Y);
            SendLegends(binaryWriter);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            State was = state;
            state = (State)binaryReader.ReadByte();
            timer = binaryReader.ReadInt16();
            spin = binaryReader.ReadSByte();
            restAt = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
            ReceiveLegends(binaryReader);

            if (state == State.Fall && was != State.Fall)
            {
                landed = false;
                struck = false;
            }
        }
    }
}
