using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// What a rider asks of the donkey this tick. Dona Quinn hands one over every tick she
    /// is in the saddle (<see cref="DonaQuinn.ReinsFor"/>); the donkey does the rest.
    /// </summary>
    public struct Reins
    {
        /// <summary>The horizontal speed wanted, signed, in pixels a tick.</summary>
        public float Speed;

        /// <summary>How much of the way to it is gone each tick.</summary>
        public float Accel;

        /// <summary>Which way to face: 1 right, -1 left, 0 as it is. A change is a flip.</summary>
        public int Facing;

        /// <summary>Up on the hind legs, the front ones going round.</summary>
        public bool Rear;

        /// <summary>Reared and shaking, hard: throwing her.</summary>
        public bool Buck;

        /// <summary>Jump whatever can be jumped rather than stopping at it.</summary>
        public bool MayJump;

        /// <summary>Pull up short of a wall rather than go into it at whatever speed he has (a charge is not careful).</summary>
        public bool Careful;

        /// <summary>Walk into deep water and swim rather than stop on the bank.</summary>
        public bool MaySwim;
    }

    /// <summary>
    /// Triunfo, Dona Quinn's donkey. A body of its own, and the legs of the fight.
    ///
    /// A separate NPC from her, by request, but not a separate life: while she is on him
    /// he shares hers (realLife), so a blow to the donkey is a blow to Dona Quinn and the
    /// one bar counts both. He cannot die of it; she is the one that falls.
    ///
    /// He goes where she asks (<see cref="Reins"/>): up to 37 miles an hour by the
    /// stopwatch (7.24 pixels a tick, vanilla's own 216000 / 42240 the other way round),
    /// reached in a second and a half from standing; up single steps the way anything that
    /// walks does; up on his hind legs with the front ones going round; and round to face
    /// the other way, which is a plain flip, her with him.
    ///
    /// He has sense about the ground (<see cref="DonkeyNav"/>, by request): he jumps what he
    /// can (ten tiles), timed to clear it; stood against a bank with no run at it he backs off
    /// and has one; he leaps a gap he can clear and pulls up at one he cannot, or at deep water
    /// or lava; and in deep water he swims, and leaps out at the bank. What he cannot get over
    /// he tells her about, and a charge goes into a wall regardless (she pays: Crash).
    ///
    /// When she comes off he bolts, the other way, a long way, and stands there. When it
    /// is over he comes back at a walk and stands by her, or by where she was.
    ///
    /// His pictures (<see cref="DonaRig"/>): frame 0 standing; 1 to 7 a run, stepped by the
    /// ground covered; 9 to 14 the gallop past 4.2 pixels a tick; 15 off the ground; 16 to
    /// 19 reared. While she rides he is drawn by her, under her, so the two are always
    /// in step whatever order the game updates them in.
    /// </summary>
    public class TriunfoTheDonkey : ModNPC
    {
        public override string Texture => DonaRig.Folder + "TriunfoTheDonkey";

        // ============================================================
        // NUMBERS
        // ============================================================

        /// <summary>37 miles an hour on the stopwatch: Main.DrawInfoAccs shows speed as pixels a tick times 216000 / 42240.</summary>
        public const float TopSpeed = 37f * 42240f / 216000f;

        /// <summary>From standing to the top in a second and a half.</summary>
        public const float Acceleration = TopSpeed / 90f;

        /// <summary>A walk, for coming back.</summary>
        public const float WalkSpeed = 1.6f;

        /// <summary>Into a wall at this speed or more is a crash, not a bump.</summary>
        public const float CrashSpeed = 3f;

        /// <summary>Past this he gallops; under it he runs.</summary>
        private const float GallopFrom = 4.2f;

        /// <summary>Pixels of ground covered per picture of the run, and of the gallop.</summary>
        private const float RunStride = 7f;
        private const float GallopStride = 26f;

        /// <summary>Ticks a picture of the rear is held, and of the buck.</summary>
        private const int RearTicks = 5;
        private const int BuckTicks = 3;

        /// <summary>How far the buck throws him about, in pixels.</summary>
        private const float BuckShake = 3f;

        /// <summary>How long he bolts for, and how far he stops off her when he comes back.</summary>
        private const int BoltTicks = 150;
        private const float StandOff = 56f;

        // ============================================================
        // STATE
        // ============================================================

        public enum Mode
        {
            /// <summary>Under her: going where the reins say.</summary>
            Carry,

            /// <summary>She is off. Away, flat out.</summary>
            Bolt,

            /// <summary>Stopped, a long way off, until he is wanted.</summary>
            Wait,

            /// <summary>Walking back to her, or to where she was.</summary>
            Return,

            /// <summary>Standing by.</summary>
            Stand,
        }

        // ai[0] rider's index + 1 (0 none) · ai[1] Mode · ai[2] ticks in it · ai[3] where he is going, x in the world

        public int RiderIndex
        {
            get => (int)NPC.ai[0] - 1;
            set => NPC.ai[0] = value + 1;
        }

        public Mode State
        {
            get => (Mode)(int)NPC.ai[1];
            private set
            {
                NPC.ai[1] = (int)value;
                NPC.ai[2] = 0f;
            }
        }

        private int Timer
        {
            get => (int)NPC.ai[2];
            set => NPC.ai[2] = value;
        }

        /// <summary>Where he walks to when he comes back to a place rather than to her.</summary>
        public float Destination
        {
            get => NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        // ---- the picture: worked out on every machine from what is synced ----------------

        /// <summary>The frame of the sheet he shows.</summary>
        public int Picture { get; private set; }

        private float stride;
        private int pictureTicks;

        private bool rearing;
        private bool bucking;

        /// <summary>Ticks he has been stuck against something with nowhere to go.</summary>
        public int Stuck { get; private set; }

        // ---- his sense of the ground (DonkeyNav) ---------------------------------------------

        /// <summary>The run-up he is on, if any: the server's, sent with him.</summary>
        private NavMemory memory;

        /// <summary>What is ahead that he will not simply go over, and how far, as of his last look.</summary>
        public NavReport Ahead { get; private set; }
        public float AheadAt { get; private set; }

        /// <summary>In deep water, swimming; backing off for a run at something.</summary>
        public bool Swimming { get; private set; }
        public bool Backing => memory.Backing;

        /// <summary>
        /// He has just gone into a wall at speed: the game stopped him dead (collideX) going
        /// faster than <see cref="CrashSpeed"/>, dry, and what stopped him is higher than he can
        /// jump (a step he misjudged only stops him; that is a bump). Read fresh: it is the last
        /// move's.
        /// </summary>
        public bool Crashed
        {
            get
            {
                if (NPC.wet || !NPC.collideX || Math.Abs(NPC.oldVelocity.X) < CrashSpeed)
                    return false;

                int way = Math.Sign(NPC.oldVelocity.X);
                float front = way > 0 ? NPC.position.X + NPC.width : NPC.position.X;
                int x = (int)Math.Floor((front + way * 0.5f) / 16f);

                return DonkeyNav.Probe(x, DonkeyNav.FootRow(NPC.Bottom.Y), DonkeyNav.Rows(NPC.height)).Kind == Ground.Wall;
            }
        }

        /// <summary>His body as his sense of the ground takes it.</summary>
        public NavBody Body => new NavBody
        {
            Position = NPC.position,
            Width = NPC.width,
            Height = NPC.height,
            Velocity = NPC.velocity,
            Wet = NPC.wet,
            OnGround = Grounded(),
        };

        /// <summary>Which way he faces: 1 right, -1 left. A change is a flip, there and then.</summary>
        public int Facing => NPC.direction >= 0 ? 1 : -1;

        /// <summary>Where his hooves are drawn this frame: the foot of the hitbox, the step smoothing, the buck.</summary>
        public Vector2 HoovesAt
        {
            get
            {
                Vector2 at = new Vector2(NPC.Center.X, NPC.Bottom.Y + NPC.gfxOffY) + BuckOffset();
                return new Vector2((float)Math.Round(at.X), (float)Math.Round(at.Y));
            }
        }

        /// <summary>
        /// Whether the rider is on him and drawing him. Hers is the word on that: on a client
        /// the news that she is off can come a tick or two before his own, and he must not go
        /// undrawn in between.
        /// </summary>
        public bool Carrying => State == Mode.Carry && Rider()?.ModNPC is DonaQuinn dona && dona.InSaddle;

        /// <summary>The rider, if she is still there.</summary>
        public NPC Rider()
        {
            int i = RiderIndex;

            if (i < 0 || i >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[i];
            return npc.active && npc.ModNPC is DonaQuinn ? npc : null;
        }

        // ============================================================
        // SETUP
        // ============================================================

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = DonaRig.Frames;

            // He belongs to her fight; the bestiary would only show a donkey on its own.
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };
        }

        public override void SetDefaults()
        {
            // The body, rump to the root of the neck, hooves to the back: the head and the
            // tail hang outside it, as they do on anything with four legs.
            NPC.width = 64;
            NPC.height = 48;

            NPC.aiStyle = -1;
            NPC.lifeMax = DonaQuinn.Life;
            NPC.life = DonaQuinn.Life;
            NPC.damage = 0;
            NPC.defense = DonaQuinn.Defense;
            NPC.knockBackResist = 0f;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.npcSlots = 0f;
            NPC.value = 0f;
            NPC.netAlways = true;

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = null;
        }

        /// <summary>Hers, not his: the difficulty does not grow it, as it does not grow hers.</summary>
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = DonaQuinn.Life;
        }

        // He goes when the fight says, not when vanilla's despawn would.
        public override bool CheckActive() => false;

        // Never the one that dies: she is (realLife). Anything that zeroes him on his own is refused.
        public override bool CheckDead()
        {
            NPC.life = Math.Max(1, NPC.life);
            return false;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        // Her bar is the bar.
        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        /// <summary>His burns are her burns while he carries her (realLife); none while nothing can hurt either.</summary>
        public override void UpdateLifeRegen(ref int damage)
        {
            if (!NPC.dontTakeDamage)
                return;

            if (NPC.lifeRegen < 0)
                NPC.lifeRegen = 0;

            damage = 0;
        }

        /// <summary>One swing, one blow: see DonaQuinn.OnHitByItem.</summary>
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            DonaQuinn.ShareSwing(Rider(), player);
        }

        /// <summary>One shot, one blow: see DonaQuinn.ShareShot.</summary>
        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            DonaQuinn.ShareShot(Rider(), projectile);
        }

        // ============================================================
        // AI
        // ============================================================

        public override void AI()
        {
            NPC rider = Rider();
            bool carrying = State == Mode.Carry && rider != null;

            // While she is on him, her life is his. Kept every tick, on every machine,
            // rather than trusted to arrive: a blow on a client is taken off her there too.
            if (carrying)
            {
                NPC.realLife = rider.whoAmI;
                NPC.life = Math.Max(1, rider.life);
                NPC.lifeMax = rider.lifeMax;
                NPC.dontTakeDamage = rider.dontTakeDamage;
            }
            else
            {
                NPC.realLife = -1;
                NPC.dontTakeDamage = true;
                NPC.life = NPC.lifeMax;
            }

            rearing = false;
            bucking = false;

            switch (State)
            {
                case Mode.Carry:
                    if (rider == null)
                    {
                        // She is gone from under him without getting off. He goes too.
                        Vanish();
                        return;
                    }

                    Reins reins = ((DonaQuinn)rider.ModNPC).ReinsFor(this);
                    Move(reins);

                    if (reins.Facing != 0)
                        Face(reins.Facing);

                    rearing = reins.Rear || reins.Buck;
                    bucking = reins.Buck;
                    break;

                case Mode.Bolt:
                    Move(Alone(Facing * TopSpeed, Acceleration * 1.5f));

                    if (Main.netMode != NetmodeID.MultiplayerClient && (Timer >= BoltTicks || Stuck > 30))
                        Switch(Mode.Wait);
                    break;

                case Mode.Wait:
                    Move(Alone(0f, 0.2f));
                    break;

                case Mode.Return:
                {
                    float to = WhereTo(rider);
                    float dx = to - NPC.Center.X;

                    if (Math.Abs(dx) > 6f)
                        Face(Math.Sign(dx));

                    // Slows into it rather than stopping dead.
                    float speed = MathHelper.Clamp(dx * 0.05f, -WalkSpeed, WalkSpeed);
                    Move(Alone(speed, 0.08f));

                    if (Main.netMode != NetmodeID.MultiplayerClient && (Math.Abs(dx) < 4f || Stuck > 60 || Timer > 1800))
                        Switch(Mode.Stand);
                    break;
                }

                case Mode.Stand:
                {
                    Move(Alone(0f, 0.15f));

                    float at = rider != null ? rider.Center.X : Destination;

                    if (Math.Abs(at - NPC.Center.X) > 10f)
                        Face(Math.Sign(at - NPC.Center.X));
                    break;
                }
            }

            Timer++;

            Animate();

            // Left on his own when it is all over: gone the moment nobody is looking.
            if (Main.netMode != NetmodeID.MultiplayerClient && rider == null && (State == Mode.Stand || State == Mode.Wait))
            {
                unseen = DonaQuinn.Watched(NPC.Center) ? 0 : unseen + 1;

                if (unseen >= 60)
                    Vanish();
            }
        }

        private int unseen;

        /// <summary>Where he walks back to: beside her if she is there, where she was if not.</summary>
        private float WhereTo(NPC rider)
        {
            if (rider == null)
                return Destination;

            // Stops short of her on whichever side he is coming from.
            float side = NPC.Center.X >= rider.Center.X ? 1f : -1f;
            return rider.Center.X + side * StandOff;
        }

        /// <summary>The server changes what he is doing; every client hears of it.</summary>
        private void Switch(Mode mode)
        {
            State = mode;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// She is off him: he turns and goes, the other way from the way she went.
        /// The server's call.
        /// </summary>
        public void Bolt(int awayFrom)
        {
            NPC.realLife = -1;
            Face(-awayFrom);
            Switch(Mode.Bolt);
        }

        /// <summary>Back he comes, to her, or to <paramref name="x"/> if she is not there. The server's call.</summary>
        public void ComeBack(float x)
        {
            if (State == Mode.Return || State == Mode.Stand)
                return;

            Destination = x;
            Switch(Mode.Return);
        }

        /// <summary>Gone, on every machine.</summary>
        public void Vanish()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            NPC.active = false;
            NPC.netSkip = -1;
            NPC.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        // ============================================================
        // MOVING
        // ============================================================

        /// <summary>Reins for when nobody is riding him: careful, and he may swim.</summary>
        private static Reins Alone(float speed, float accel) => new Reins
        {
            Speed = speed,
            Accel = accel,
            MayJump = true,
            Careful = true,
            MaySwim = true,
        };

        /// <summary>
        /// Toward the speed asked by the rate asked, as the ground lets him: his sense of it
        /// (DonkeyNav) may ask for less, a jump, a stroke, or a run-up; single steps he takes the
        /// way anything that walks does (Collision.StepUp). The jump and the run-up are sent at
        /// once from the server, so nobody sees him walk into the bank first.
        /// </summary>
        private void Move(Reins reins)
        {
            bool authority = Main.netMode != NetmodeID.MultiplayerClient;
            bool wasBacking = memory.Backing;

            NavBody body = Body;
            NavAsk ask = new NavAsk
            {
                Speed = reins.Speed,
                Accel = reins.Accel,
                MayJump = reins.MayJump,
                Careful = reins.Careful,
                MaySwim = reins.MaySwim,
            };

            NavSteer steer = DonkeyNav.Steer(ref memory, body, ask, authority);

            float vx = NPC.velocity.X;

            if (Math.Abs(steer.Speed - vx) <= steer.Accel)
                vx = steer.Speed;
            else
                vx += Math.Sign(steer.Speed - vx) * steer.Accel;

            NPC.velocity.X = vx;

            // Single steps, the way anything that walks takes them.
            if (!steer.Swimming && steer.Jump <= 0f)
                Collision.StepUp(ref NPC.position, ref NPC.velocity, NPC.width, NPC.height, ref NPC.stepSpeed, ref NPC.gfxOffY, 1, false, 0);

            bool wantsOn = Math.Abs(reins.Speed) > 0.5f;

            if (steer.Jump > 0f)
            {
                NPC.velocity.Y = -steer.Jump;

                if (authority)
                    NPC.netUpdate = true;
            }
            else if (steer.Swimming)
            {
                NPC.velocity.Y = steer.Lift;
            }
            else if (NPC.collideX && body.OnGround && wantsOn && steer.Ahead == NavReport.Clear && !steer.Backing && reins.MayJump)
            {
                // Held by something his eye did not make out (a slope, a half tile): a hop of a tile.
                NPC.velocity.Y = -DonkeyNav.SpeedFor(16f + DonkeyNav.Clearance, DonkeyNav.Gravity);
            }

            if (authority && memory.Backing != wasBacking)
                NPC.netUpdate = true;

            Ahead = steer.Ahead;
            AheadAt = steer.Distance;
            Swimming = steer.Swimming;

            // Stopped against it, or pulled up at it, while asked to go on.
            bool blocked = wantsOn && !steer.Backing && body.OnGround
                && (NPC.collideX || (steer.Ahead != NavReport.Clear && steer.Distance <= DonkeyNav.EdgeMargin + 6f && Math.Abs(NPC.velocity.X) < 0.4f));

            Stuck = blocked ? Stuck + 1 : 0;
        }

        /// <summary>
        /// On something: not rising or falling (the game zeroes that on a floor), or stopped by a
        /// floor in the last move, or a floor right under his hooves (on a slope the game leaves
        /// a little of the fall on him).
        /// </summary>
        private bool Grounded()
        {
            if (NPC.velocity.Y == 0f || (NPC.collideY && NPC.oldVelocity.Y >= 0f))
                return true;

            if (NPC.velocity.Y < 0f)
                return false;

            int y = (int)((NPC.Bottom.Y + 2f) / 16f);
            int left = (int)((NPC.position.X + 2f) / 16f);
            int right = (int)((NPC.position.X + NPC.width - 2f) / 16f);

            for (int x = left; x <= right; x++)
            {
                if (DonkeyNav.Floor(x, y))
                    return true;
            }

            return false;
        }

        /// <summary>Faces this way: a plain flip, no turning in between.</summary>
        private void Face(int way)
        {
            way = way >= 0 ? 1 : -1;

            NPC.direction = way;
            NPC.spriteDirection = way;
        }

        // ============================================================
        // THE PICTURE
        // ============================================================

        /// <summary>
        /// Which picture: reared, off the ground, standing, running or galloping. The run
        /// and the gallop step by the ground covered rather than by the clock, so his legs
        /// never skate however fast he is going.
        /// </summary>
        private void Animate()
        {
            float speed = Math.Abs(NPC.velocity.X);

            // Swimming he paddles: his legs go as they do on the ground.
            bool onGround = NPC.velocity.Y == 0f || Swimming;

            // Backing off, the legs go the other way.
            bool backward = NPC.velocity.X != 0f && Math.Sign(NPC.velocity.X) != Facing;

            if (rearing)
            {
                int hold = bucking ? BuckTicks : RearTicks;

                if (!DonaRig.Reared(Picture))
                {
                    Picture = DonaRig.RearFirst;
                    pictureTicks = 0;
                }
                else if (++pictureTicks >= hold)
                {
                    pictureTicks = 0;
                    Picture = DonaRig.RearFirst + (Picture - DonaRig.RearFirst + 1) % DonaRig.RearCount;
                }

                return;
            }

            if (!onGround)
            {
                Picture = DonaRig.InAir;
                return;
            }

            if (speed < 0.15f)
            {
                Picture = DonaRig.Standing;
                stride = 0f;
                return;
            }

            bool gallop = speed >= GallopFrom;
            int first = gallop ? DonaRig.GallopFirst : DonaRig.RunFirst;
            int count = gallop ? DonaRig.GallopCount : DonaRig.RunCount;
            float length = gallop ? GallopStride : RunStride;

            if (Picture < first || Picture >= first + count)
            {
                Picture = first;
                stride = 0f;
            }

            stride += speed;

            while (stride >= length)
            {
                stride -= length;
                Picture = first + (Picture - first + (backward ? count - 1 : 1)) % count;

                // A hoof down at a gallop kicks a little of the ground up.
                if (gallop && !Main.dedServ && Main.rand.NextBool(2))
                    Kick();
            }
        }

        /// <summary>Dust off the hooves.</summary>
        private void Kick()
        {
            Vector2 at = new Vector2(NPC.Center.X - Facing * Main.rand.NextFloat(0f, 26f), NPC.Bottom.Y - 2f);
            Dust dust = Dust.NewDustPerfect(at, DustID.Dirt, new Vector2(-Facing * Main.rand.NextFloat(0.5f, 1.8f), -Main.rand.NextFloat(0.3f, 1.4f)),
                80, default, Main.rand.NextFloat(0.8f, 1.15f));
            dust.noGravity = false;
        }

        /// <summary>The buck's shake: big, quick and never the same twice, but the same on every machine for the same tick.</summary>
        private Vector2 BuckOffset()
        {
            if (!bucking)
                return Vector2.Zero;

            float t = Main.GameUpdateCount;
            float x = (float)(Math.Sin(t * 2.1f) * 0.6f + Math.Sin(t * 5.3f + 1.7f) * 0.4f);
            float y = (float)(Math.Sin(t * 3.7f + 0.4f) * 0.6f + Math.Sin(t * 7.9f) * 0.4f);

            return new Vector2(x * BuckShake, y * BuckShake * 0.6f);
        }

        // ============================================================
        // SYNC
        // ============================================================

        /// <summary>The run-up, so every machine sees him back off at the same moment.</summary>
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(memory.Backing);
            writer.Write((sbyte)memory.Dir);
            writer.Write(memory.From);
            writer.Write(memory.Room);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            memory.Backing = reader.ReadBoolean();
            memory.Dir = reader.ReadSByte();
            memory.From = reader.ReadSingle();
            memory.Room = reader.ReadSingle();
        }

        // ============================================================
        // DRAW
        // ============================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Under her, he is hers to draw.
            if (Carrying)
                return false;

            DrawBody(spriteBatch, screenPos, NPC.GetAlpha(drawColor));
            return false;
        }

        /// <summary>The donkey, at his hooves, in his picture, the way he faces.</summary>
        public void DrawBody(SpriteBatch spriteBatch, Vector2 screenPos, Color light)
        {
            Texture2D sheet = TextureAssets.Npc[Type].Value;

            DonaRig.Piece(spriteBatch, sheet, DonaRig.Frame(Picture), HoovesAt, DonaRig.Hooves, DonaRig.SheetWidth,
                0f, Facing, light, screenPos);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            // A blow on him comes off her (realLife), and NPC.StrikeNPC then copies her life
            // into his own before it lets her CheckDead put hers back to one. A blow that would
            // have killed her so leaves him on nothing, and the next update takes anything on
            // nothing out of the world (NPC.UpdateNPC_Inner, before AI) without a word: he would
            // be gone, and she with him, no fall at all. He is never on less than one.
            NPC.life = Math.Max(1, NPC.life);

            if (Main.dedServ)
                return;

            for (int i = 0; i < 3; i++)
            {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 1.5f, -1f, 0, default, 0.9f);
            }
        }
    }
}
