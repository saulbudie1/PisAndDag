using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Structures;
using PisAndDag.Content.Combat;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// The Abomanation. A worm that lives in the sand and comes up through it.
    ///
    /// Modelled on Calamity's Desert Scourge, but hand written rather than lifted: the
    /// Scourge's movement is one long chain of branches that reads the same variable under
    /// four names, and none of the six things asked for here are reachable from outside
    /// it. This is the same fight expressed as one target point and one steering rule, so
    /// the six knobs in <see cref="Abomanation"/> are the whole behaviour.
    ///
    /// Three parts:
    ///   the head steers and owns the health pool,
    ///   fifteen body segments follow it and carry the arms,
    ///   the tail closes the chain.
    ///
    /// The fight is a chase with one interruption. It goes for you, slightly off centre
    /// (<see cref="Abomanation.Stray"/>) so a straight line never quite works; every ten
    /// seconds spent under you or off to one side it dives, turns around a long way down,
    /// and comes straight back up through the ground you are standing on.
    ///
    /// It is called up a long way off (<see cref="Summon"/>) and comes to you through the
    /// surface in a slow wave with the camera on it; the fight starts when the camera is
    /// back on you. No loot table yet.
    /// </summary>
    [AutoloadBossHead]
    public class AbomanationHead : ModNPC
    {
        public override string Texture => Abomanation.Folder + "Abomanation_Head";

        /// <summary>
        /// The marker on the map edge.
        ///
        /// Both halves are needed and they do different jobs. ModNPC.Register only calls
        /// AddBossHeadTexture when the class carries [AutoloadBossHead]; this says which
        /// file it loads. The default would be Texture + "_Head_Boss", which works out to
        /// Abomanation_Head_Head_Boss, so without this override the attribute asks for a
        /// file that is not there and the mod fails to load rather than quietly going
        /// without an icon.
        /// </summary>
        public override string BossHeadTexture => Abomanation.Folder + "Abomanation_Head_Boss";

        // ------------------------------------------------------------------ state -----
        //
        // Synced by hand in SendExtraAI. Everything here is the attack, and a client that
        // guessed at it would send the body somewhere the server never went. The head's own
        // ai[] is unused; the segments carry theirs, and tML syncs that for free.

        /// <summary>-1 arriving, 0 chasing, 1 diving, 2 rushing up, 3 falling back, 4 whip run in, 5 whip loop, 6 whip crack. The move, not the half of the fight: that is <see cref="Enraged"/>.</summary>
        private int phase;

        /// <summary>
        /// Which way the loop turns: +1 clockwise on screen, -1 anticlockwise. The tail
        /// comes off the coil on the OTHER side, so this is chosen to put the lash on
        /// the side you are on.
        /// </summary>
        private int whipSense;

        /// <summary>
        /// The head's heading through the loop and the run, radians. Integrated here
        /// tick by tick rather than read off the velocity, so the loop closes on exactly
        /// the heading it opened on and the run goes straight out along it.
        /// </summary>
        private float whipHeading;

        /// <summary>
        /// Counts cracks. The segments each remember the serial they set their swing up
        /// for, and set up again when it changes, so every crack starts from the coil
        /// the loop just made and never from the last one's leftovers.
        /// </summary>
        private int whipSerial;

        /// <summary>Ticks since the last whip ended. Gates the next.</summary>
        private int whipGap;

        /// <summary>Whether the second half has been announced here. Local: each machine roars once on seeing the health cross.</summary>
        private bool roared;

        /// <summary>The second half of the fight: health at or under <see cref="Abomanation.RagePoint"/>. Read off health, so it needs no sync.</summary>
        public bool Enraged => NPC.life <= NPC.lifeMax * Abomanation.RagePoint;

        /// <summary>Ticks into the arrival.</summary>
        private int arriveTimer;

        /// <summary>Which way it is travelling while arriving: +1 right, -1 left.</summary>
        private int arriveDir;

        /// <summary>The ground line under the head while arriving, in world pixels, smoothed. The wave is centred on it.</summary>
        private float arriveBase;

        /// <summary>Ticks spent in a position worth punishing. Fires the dive at <see cref="Abomanation.RushGate"/>.</summary>
        private int chargeTimer;

        /// <summary>Ticks the current attack has taken. A watchdog, nothing else.</summary>
        private int attackTimer;

        /// <summary>World Y the rush climbs to, fixed at the moment it turns around.</summary>
        private float rushTop;

        private bool spawnedBody;

        /// <summary>The point on the far side of the player it is currently committed to reaching.</summary>
        private Vector2 passTarget;

        /// <summary>Ticks left on that commitment. Zero means it is aiming at the player again.</summary>
        private int passTimer;

        /// <summary>
        /// Rolled once at spawn and synced. Every arm sprite on the whole worm is dealt
        /// from this one number, so a fight's worth of choices costs one int on the wire
        /// and no two summons look the same. Read by the segments off the head.
        /// </summary>
        public int ArmSeed;

        // ---------------------------------------------------------------- defaults ----

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void Unload()
        {
            AbomanationArms.Unload();
        }

        public override void SetDefaults()
        {
            NPC.width = 56;
            NPC.height = 56;

            NPC.aiStyle = -1;
            AIType = -1;

            NPC.lifeMax = LifeForMode();
            NPC.damage = 40;
            NPC.defense = 4;

            NPC.npcSlots = 12f;
            NPC.boss = true;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;

            // Drawn behind tiles, because it spends most of the fight inside them. Without
            // this it swims over the top of the sand like a sticker on the screen.
            NPC.behindTiles = true;

            // Comes up out of nothing. The whole body fades in together, a segment at a
            // time, from AbomanationChain.Tether.
            NPC.alpha = 255;

            NPC.netAlways = true;
            NPC.value = Item.buyPrice(gold: 2);

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;

            // The bar and the name. OwnIntro: the arrival is its own (Arrive), so the
            // universal pan and hold stay out, and the name's flight is fired from there.
            NPC.PAD().BossName = "Abomanation";
            NPC.PAD().BossTitle = "It came up under the dunes.";
            NPC.PAD().OwnIntro = true;
        }

        /// <summary>
        /// Health per difficulty, written rather than scaled, for the reason Megashark
        /// gives: the automatic expert scaling doubles whatever SetDefaults left behind,
        /// so a number picked in one place lands somewhere else in the other modes.
        /// </summary>
        private static int LifeForMode()
        {
            return Main.masterMode ? 12000 : Main.expertMode ? 9000 : 6000;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = LifeForMode();
            NPC.life = NPC.lifeMax;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Desert
            });
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(phase);
            writer.Write(chargeTimer);
            writer.Write(attackTimer);
            writer.Write(rushTop);
            writer.Write(spawnedBody);
            writer.Write(ArmSeed);
            writer.WriteVector2(passTarget);
            writer.Write(passTimer);
            writer.Write(arriveTimer);
            writer.Write(arriveDir);
            writer.Write(arriveBase);
            writer.Write(whipSense);
            writer.Write(whipHeading);
            writer.Write(whipSerial);
            writer.Write(whipGap);

            // Not in the vanilla packet, and the arrival depends on it. The segments
            // send theirs the same way.
            writer.Write(NPC.dontTakeDamage);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            phase = reader.ReadInt32();
            chargeTimer = reader.ReadInt32();
            attackTimer = reader.ReadInt32();
            rushTop = reader.ReadSingle();
            spawnedBody = reader.ReadBoolean();
            ArmSeed = reader.ReadInt32();
            passTarget = reader.ReadVector2();
            passTimer = reader.ReadInt32();
            arriveTimer = reader.ReadInt32();
            arriveDir = reader.ReadInt32();
            arriveBase = reader.ReadSingle();
            whipSense = reader.ReadInt32();
            whipHeading = reader.ReadSingle();
            whipSerial = reader.ReadInt32();
            whipGap = reader.ReadInt32();
            NPC.dontTakeDamage = reader.ReadBoolean();
        }

        public override bool CheckActive() => false;

        // --------------------------------------------------------------------- AI -----

        public override void AI()
        {
            if (NPC.target < 0 || NPC.target == Main.maxPlayers
                || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
            {
                NPC.TargetClosest(true);
            }

            Player player = Main.player[NPC.target];

            if (!spawnedBody && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnBody();

            if (NPC.alpha > 0)
            {
                NPC.alpha -= 42;
                if (NPC.alpha < 0)
                    NPC.alpha = 0;
            }

            // Nobody left to chase. Dig, and take the whole body off the board once it is
            // deep enough that nothing is going to see it go.
            if (!player.active || player.dead)
            {
                Leave();
                return;
            }

            NPC.localAI[1]++;

            if (Enraged && !roared)
            {
                roared = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = Abomanation.RagePitch, PitchVariance = 0f, Volume = 1.2f }, NPC.Center);
            }

            if (phase == -1)
            {
                Arrive(player);
            }
            else
            {
                Advance(player);

                // The loop and the straight part of the crack drive the head themselves,
                // in Advance. Steering would fight them.
                if (!Driving)
                    Steer(Aim(player));

                // Once the head is chasing again the first link follows where the head
                // is actually going, not where it was sent.
                if (phase == 6 && !Driving && NPC.velocity.LengthSquared() > 0.01f)
                    whipHeading = NPC.velocity.ToRotation();
            }

            if (NPC.velocity.LengthSquared() > 0.01f)
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;

            // The one thing worth a packet on its own: a direction change the client's own
            // copy of this AI would not have predicted.
            if (!NPC.justHit
                && ((NPC.velocity.X > 0f) != (NPC.oldVelocity.X > 0f)
                    || (NPC.velocity.Y > 0f) != (NPC.oldVelocity.Y > 0f)))
            {
                NPC.netUpdate = true;
            }
        }

        /// <summary>
        /// The state machine. Four states, and every transition is a position it reached
        /// rather than a timer running out, so the attack cannot fire from the wrong place.
        /// The timer only decides when it is allowed to start.
        /// </summary>
        private void Advance(Player player)
        {
            switch (phase)
            {
                case 0:
                    chargeTimer++;
                    whipGap++;

                    // The timer runs all the time so the cadence is a cadence, and the
                    // POSITION decides when the attack is allowed to go: from below, which
                    // is what a dive is. Gating the counter instead, which is what this did
                    // and what Desert Scourge does, ties how often you are attacked to how
                    // much of its loop happens to be spent underneath you, and with the
                    // steering turned into arcs that came out at one rush every twenty five
                    // seconds instead of ten, unevenly.
                    //
                    // The doubled gate is the way out: a player who keeps it above them
                    // forever still gets attacked, just late.
                    bool below = NPC.Center.Y > player.Center.Y;

                    if (chargeTimer >= Abomanation.RushGate
                        && (below || chargeTimer >= Abomanation.RushGate * 2))
                    {
                        phase = 1;
                        chargeTimer = 0;
                        attackTimer = 0;
                        passTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1:
                    attackTimer++;

                    if (NPC.Center.Y >= DiveTarget(player) - 16f)
                    {
                        // Measured from where it actually turned around, not from the
                        // player, so Distance means what it says: this far up, from here.
                        rushTop = Math.Max(NPC.Center.Y - Abomanation.Distance * 16f, 320f);

                        // No snap to straight up here any more. It used to need one,
                        // because the old steering took a second to turn around and sank
                        // two hundred pixels doing it, which ate the whole of Distance. The
                        // angular steering banks into the climb in about forty ticks and
                        // still tops out over your head, and a head that flips its velocity
                        // in one tick is exactly the thing this boss was doing wrong.
                        phase = 2;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar, player.Center);
                        NPC.netUpdate = true;
                    }
                    break;

                case 2:
                    attackTimer++;
                    Breach();

                    if (NPC.Center.Y <= rushTop)
                    {
                        phase = 3;
                        attackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                default:
                    attackTimer++;

                    // Spent once it is back under the ground, or once it is a long way
                    // below you. Waiting only on the depth would leave it falling for
                    // twenty seconds if you flew up while it came down.
                    bool back = attackTimer > 30 && Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

                    if (back || NPC.Center.Y >= player.Center.Y + Abomanation.DiveDepth * 0.5f)
                    {
                        Reset();

                        // A finished rush is the only place the whip is rolled from.
                        int chance = Enraged ? Abomanation.RageWhipChance : Abomanation.WhipChance;

                        if (WhipReady(player) && Main.rand.NextBool(chance))
                            StartWhip();
                    }
                    break;

                case 4:
                    attackTimer++;

                    // Ordinary chasing, straight at you, until it is close enough to
                    // loop. Nothing scripted about the approach: same speed, same turn
                    // rate, so it reads as the fight rather than a cutscene starting.
                    if (NPC.Distance(player.Center) <= Abomanation.WhipNear * 16f)
                        Loop(player);
                    break;

                case 5:
                    attackTimer++;

                    // Untouchable from the loop to the end of the crack. Tether carries
                    // it down the chain. The whip still hurts: contact reads Arriving,
                    // not this.
                    NPC.dontTakeDamage = true;

                    // Round once. The body follows it into the circle — the coil — the
                    // way it follows everything, and that is the tell.
                    whipHeading += whipSense * Abomanation.WhipLoopSpeed / Abomanation.WhipLoopRadius;
                    NPC.velocity = whipHeading.ToRotationVector2() * Abomanation.WhipLoopSpeed;

                    if (attackTimer >= Abomanation.WhipLoopTicks)
                        Crack();
                    break;

                case 6:
                    attackTimer++;
                    NPC.dontTakeDamage = true;

                    // Straight out of the coil, along the heading it looped back to. The
                    // body is a chain with momentum from here (AbomanationChain.Swing)
                    // and comes after it on its own. After WhipRunStraight the head is
                    // steering again and the body is still swinging behind it.
                    if (Driving)
                        NPC.velocity = whipHeading.ToRotationVector2() * Abomanation.WhipRunSpeed;

                    // The crack, on the tick the tail is fastest.
                    if (attackTimer == Abomanation.WhipCrackAt)
                        SoundEngine.PlaySound(SoundID.Item153, NPC.Center);

                    if (attackTimer >= Abomanation.WhipRunTicks)
                    {
                        Reset();
                        whipGap = 0;
                    }
                    break;
            }

            // Anything that has taken this long has got itself stuck on geometry. Drop it
            // and go back to chasing rather than leaving the fight hanging on a corner.
            int timeout = phase >= 4 ? Abomanation.WhipTimeout : Abomanation.RushTimeout;

            if (phase != 0 && attackTimer > timeout)
                Reset();
        }

        // ------------------------------------------------------------------- whip ----

        /// <summary>True while the body is being thrown. What the segments read.</summary>
        public bool Cracking => phase == 6;

        /// <summary>True from the loop to the end of the crack: the whole whip.</summary>
        public bool Whipping => phase == 5 || phase == 6;

        /// <summary>True while it is arriving, which is the one time it may not touch you.</summary>
        public bool Arriving => phase == -1;

        /// <summary>True while the head's motion is scripted: the loop, and the straight part of the run.</summary>
        private bool Driving => phase == 5 || (phase == 6 && attackTimer <= Abomanation.WhipRunStraight);

        /// <summary>Which crack this is. The segments set their swing up afresh when it changes.</summary>
        public int WhipSerial => whipSerial;

        /// <summary>The head's heading through the crack, radians. The first link is pulled toward the reverse of it.</summary>
        public float RunHeading => whipHeading;

        /// <summary>
        /// How much of each link's turning speed survives a tick, right now. Full while
        /// the arc is being thrown, then falling off from WhipSettleAt over
        /// WhipSettleOver, so the recoil is one swing back and not a rope in the wind.
        /// </summary>
        public float LashDamping
        {
            get
            {
                float t = MathHelper.Clamp((attackTimer - Abomanation.WhipSettleAt) / (float)Abomanation.WhipSettleOver, 0f, 1f);
                return MathHelper.Lerp(Abomanation.WhipDamping, Abomanation.WhipSettleDamping, t);
            }
        }

        private bool WhipReady(Player player)
        {
            return whipGap >= (Enraged ? Abomanation.RageWhipGap : Abomanation.WhipGap)
                && NPC.Distance(player.Center) <= Abomanation.WhipRange;
        }

        /// <summary>Calls the whip. Everything it needs is decided later, at the loop.</summary>
        private void StartWhip()
        {
            phase = 4;
            attackTimer = 0;
            passTimer = 0;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// Begins the loop, and picks which way round.
        ///
        /// The tail comes off the coil on the side the loop did NOT turn toward, so the
        /// loop turns away from you: you on the left of its line and it circles right,
        /// and the lash takes the left. Dead ahead — within a tile and a half of the
        /// line either way — it circles down, so the lash comes over the top rather than
        /// under the ground where nothing can see it.
        /// </summary>
        private void Loop(Player player)
        {
            whipHeading = NPC.velocity.LengthSquared() > 0.01f
                ? NPC.velocity.ToRotation()
                : (player.Center - NPC.Center).ToRotation();

            Vector2 heading = whipHeading.ToRotationVector2();
            Vector2 to = player.Center - NPC.Center;

            // Which side of the line you are on, signed. Positive is the side the
            // heading reaches by turning clockwise on screen.
            float across = heading.X * to.Y - heading.Y * to.X;

            if (Math.Abs(across) < 24f)
                whipSense = heading.X >= 0f ? 1 : -1;      // the sweep goes up
            else
                whipSense = across > 0f ? -1 : 1;           // the sweep goes your way

            phase = 5;
            attackTimer = 0;
            NPC.netUpdate = true;

            // The tell: the star and the note, here, half a second of coil before the
            // body turns yellow. The segments are quiet; this is the animal's one.
            CCBypass.Warn(NPC);
        }

        /// <summary>Lets it go. From here the body is a chain with momentum and the head just runs.</summary>
        private void Crack()
        {
            whipSerial++;
            phase = 6;
            attackTimer = 0;

            // The throw. The crack itself comes later, from the tail.
            SoundEngine.PlaySound(SoundID.Item152, NPC.Center);
            NPC.netUpdate = true;
        }

        private void Reset()
        {
            phase = 0;
            chargeTimer = 0;
            attackTimer = 0;
            rushTop = 0f;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        // ---------------------------------------------------------------- arrival ----

        /// <summary>
        /// The arrival. Called up <see cref="Abomanation.ArriveDistance"/> tiles off, it
        /// swims toward you along the ground in a wave, crest out of the ash, trough
        /// under it, at <see cref="Abomanation.ArriveSpeed"/>, and nothing can touch it
        /// or be touched by it. The camera goes out to it, sits, the name rises, and the
        /// camera comes home; on that tick the chase begins from wherever it has got to,
        /// which the numbers put about twenty tiles from you.
        ///
        /// The wave is driven, not steered: the velocity IS the wave's derivative plus a
        /// pull toward where the wave says it should be, so it holds the shape exactly
        /// and the body strings out along it. The ground line it rides is read under the
        /// head every tick and smoothed, so a ridge lifts the whole wave rather than
        /// putting a step in it.
        ///
        /// Runs everywhere. The camera work is local to each client, the motion is the
        /// same sum on every machine, and phase, timer, direction and base are synced
        /// for anyone who joins part way.
        /// </summary>
        private void Arrive(Player player)
        {
            arriveTimer++;

            // Untouchable, both ways. Tether copies this to every segment, and
            // AbomanationChain.Touching refuses contact for anything carrying it.
            NPC.dontTakeDamage = true;

            float ground = GroundLine((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f));

            if (ground > 0f)
                arriveBase += (ground - arriveBase) * 0.05f;

            // Starts at the trough, which is where Summon put it.
            float theta = arriveTimer * MathHelper.TwoPi / Abomanation.ArrivePeriod + MathHelper.PiOver2;
            float wave = (float)Math.Sin(theta) * Abomanation.ArriveWave;
            float slope = (float)Math.Cos(theta) * Abomanation.ArriveWave * MathHelper.TwoPi / Abomanation.ArrivePeriod;

            NPC.velocity.X = arriveDir * Abomanation.ArriveSpeed;
            NPC.velocity.Y = slope + (arriveBase + wave - NPC.Center.Y) * 0.1f;

            if (NPC.soundDelay == 0)
            {
                NPC.soundDelay = 18;
                SoundEngine.PlaySound(SoundID.WormDig, NPC.Center);
            }

            if (!Main.dedServ)
                ArriveCamera();

            if (arriveTimer >= Abomanation.ArriveTicks)
            {
                phase = 0;
                chargeTimer = 0;
                attackTimer = 0;
                passTimer = 0;
                NPC.dontTakeDamage = false;
                NPC.netUpdate = true;
            }
        }

        /// <summary>
        /// Out to the head over ArrivePanOut, held on it through the look and the whole of
        /// the name's flight, home over ArrivePanBack. Eased at both ends of each leg; a
        /// linear pan starts and stops dead. The name is fired by hand at the end of the
        /// look, unless BossPlate has already fired it for us on seeing the head centred.
        /// </summary>
        private void ArriveCamera()
        {
            int panOut = Abomanation.ArrivePanOut;
            int homeAt = Abomanation.ArriveTicks - Abomanation.ArrivePanBack;

            float blend;

            if (arriveTimer < panOut)
                blend = Ease(arriveTimer / (float)panOut);
            else if (arriveTimer < homeAt)
                blend = 1f;
            else
                blend = 1f - Ease((arriveTimer - homeAt) / (float)Abomanation.ArrivePanBack);

            PisPlayer.BlendCamera(NPC.Center, blend);

            if (arriveTimer == panOut + Abomanation.ArriveLook && !BossIntro.Running)
                BossIntro.PlayNameOnly(NPC.whoAmI);
        }

        /// <summary>Smoothstep. Zero slope at both ends.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Brings it up: a head placed <see cref="Abomanation.ArriveDistance"/> tiles from
        /// the player, in a trough of its wave under the ground line, already travelling
        /// toward them, in the arrival phase. The body follows on the first tick.
        ///
        /// The side is the one with more room. On the Mile that means more of the Mile,
        /// so it arrives along the siege ground rather than out of the forest; anywhere
        /// else it means away from the nearer edge of the world. Only ever runs on the
        /// machine that owns NPCs.
        /// </summary>
        public static int Summon(Player player, IEntitySource source)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return -1;

            int playerX = (int)(player.Center.X / 16f);
            int side = ArriveSide(playerX);

            int tileX = playerX + side * Abomanation.ArriveDistance;
            tileX = Math.Clamp(tileX, 20, Main.maxTilesX - 20);

            float ground = GroundLine(tileX, (int)(player.Center.Y / 16f));

            if (ground <= 0f)
                ground = player.Center.Y;

            Vector2 at = new Vector2(tileX * 16f + 8f, ground + Abomanation.ArriveWave);

            int made = NPC.NewNPC(source, (int)at.X, (int)at.Y, ModContent.NPCType<AbomanationHead>());

            if (made < 0 || made >= Main.maxNPCs)
                return -1;

            NPC npc = Main.npc[made];

            // NewNPC's Y argument is the BOTTOM of the hitbox, not the middle.
            npc.Center = at;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.UnitX * -side * Abomanation.ArriveSpeed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            if (npc.ModNPC is AbomanationHead head)
            {
                head.phase = -1;
                head.arriveTimer = 0;
                head.arriveDir = -side;
                head.arriveBase = ground;
            }

            SoundEngine.PlaySound(SoundID.Roar, player.Center);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, made);

            return made;
        }

        /// <summary>+1 or -1: the side of the player with more room to arrive from.</summary>
        private static int ArriveSide(int playerX)
        {
            if (ExtraLargeWorld.Active)
            {
                (int left, int right) = Layout.Mile;

                if (playerX >= left && playerX <= right)
                    return playerX - left >= right - playerX ? -1 : 1;
            }

            return playerX > Main.maxTilesX / 2 ? -1 : 1;
        }

        /// <summary>
        /// The ground line in a column, as a world Y: the top edge of the first solid
        /// tile. Walks up out of anything solid first, so a start point inside the ground
        /// finds the surface above it rather than the floor of whatever it is in. -1 if
        /// there is nothing to find.
        /// </summary>
        public static float GroundLine(int tileX, int fromTileY)
        {
            if (tileX < 10 || tileX > Main.maxTilesX - 10)
                return -1f;

            int y = Math.Clamp(fromTileY, 10, Main.maxTilesY - 10);

            for (int i = 0; i < 400 && y > 10; i++)
            {
                if (!WorldGen.SolidTile(tileX, y))
                    break;

                y--;
            }

            for (int i = 0; i < 800 && y < Main.maxTilesY - 10; i++)
            {
                if (WorldGen.SolidTile(tileX, y))
                    return y * 16f;

                y++;
            }

            return -1f;
        }

        /// <summary>
        /// How deep the dive goes, kept inside the world.
        ///
        /// Unclamped, a fight in the underworld sends it past the bottom of the map, where
        /// there are no tiles to turn around in and nothing to hit, and it does not come
        /// back until the watchdog fires twenty seconds later.
        /// </summary>
        private static float DiveTarget(Player player)
        {
            return Math.Min(player.Center.Y + Abomanation.DiveDepth, (Main.maxTilesY - 60) * 16f);
        }

        /// <summary>Where it is trying to be this tick.</summary>
        private Vector2 Aim(Player player)
        {
            switch (phase)
            {
                case 1:
                    return new Vector2(player.Center.X, DiveTarget(player));

                case 2:
                    return new Vector2(player.Center.X, rushTop);

                case 3:
                    // Unsteered anyway; this only keeps the rotation pointing the way it
                    // is actually going.
                    return NPC.Center + Vector2.UnitY * 200f;

                case 4:
                    // Straight at you, and nothing else. No stray, no pass by: this is
                    // the one time it is actually coming for you.
                    return player.Center;

                default:
                    return PassAim(player);
            }
        }

        /// <summary>
        /// Where it is going during the chase, which is not at you.
        ///
        /// Aiming at the player's centre right up to the moment of contact is ordinary worm
        /// AI and it is why this sat on your head: the target never stops being reachable,
        /// so the thing never stops closing. A worm should go THROUGH and come back.
        ///
        /// So once it is inside <see cref="Abomanation.PassTrigger"/> it stops looking at
        /// you and locks a point on the far side, down the line it is already travelling,
        /// somewhere between <see cref="Abomanation.MinPass"/> and
        /// <see cref="Abomanation.MaxPass"/> tiles past wherever you were. It holds that
        /// until it arrives, and only then looks for you again. The overshoot is rolled per
        /// pass, so the distance it barrels on for is never the same twice.
        ///
        /// Measured against a moving player this takes it from 28 passes in two and a half
        /// minutes to 17, and from 1179 pixels at its furthest to 1445: fewer, longer runs
        /// with a real turn at each end, instead of a constant hover.
        /// </summary>
        private Vector2 PassAim(Player player)
        {
            Vector2 straight = player.Center + StrayOffset();

            // Mid run. Hold the point until it is reached, or until the lock times out,
            // which only happens if something got in the way.
            if (passTimer > 0)
            {
                passTimer--;

                if (NPC.Distance(passTarget) > 60f)
                    return passTarget;

                passTimer = 0;
            }

            // Close enough to commit to a run. Measured from where it IS along where it is
            // GOING, so the overshoot is always ahead of it and never a turn on the spot.
            if (NPC.velocity.LengthSquared() > 1f && NPC.Distance(straight) < Abomanation.PassTrigger)
            {
                float beyond = NPC.Distance(straight)
                    + Main.rand.NextFloat(Abomanation.MinPass, Abomanation.MaxPass) * 16f;

                passTarget = NPC.Center + NPC.velocity.SafeNormalize(Vector2.UnitY) * beyond;
                passTimer = Abomanation.PassHold;
                NPC.netUpdate = true;

                return passTarget;
            }

            return straight;
        }

        /// <summary>
        /// How far off your centre it is actually driving.
        ///
        /// Two sines at rates that do not divide into each other, so the offset wanders
        /// over the whole circle instead of tracing a closed loop. A single sine, or two
        /// at a shared rate, gives an orbit, and an orbit is a pattern you can stand still
        /// and wait out.
        /// </summary>
        private Vector2 StrayOffset()
        {
            // Hurt, it stops missing on purpose.
            if (Abomanation.Stray <= 0f || Enraged)
                return Vector2.Zero;

            float t = NPC.localAI[1] * 0.013f;
            float seed = NPC.whoAmI * 0.7f;

            return new Vector2(
                (float)Math.Sin(t * 1.7f + seed),
                (float)Math.Sin(t * 1.1f + seed * 1.6f)) * Abomanation.Stray;
        }

        /// <summary>
        /// The steering. One rule, and it is an angular one: swing the heading toward where
        /// it wants to go at a capped rate, and change the speed only along that heading.
        ///
        /// That cap is the whole fix for the thing that looked wrong. Steering by pushing
        /// each axis of the velocity toward a target velocity, which is what this used to
        /// do and what vanilla's worms do, works right up until the target points the
        /// opposite way. Then both axes cross zero on the same tick: the head is briefly
        /// stationary, reverses along the line it arrived on, and every segment behind it,
        /// which only knows to sit a fixed distance behind the one in front, piles into the
        /// turning point and flips. Seventeen bodies turning 180 degrees on the spot.
        ///
        /// Rotating the heading instead cannot do that. The speed is never touched by the
        /// turn, so the worm always has somewhere to be going, and a target directly behind
        /// it becomes a half circle of radius Speed / TurnSpeed rather than a stop. That is
        /// the U turn.
        ///
        /// It only applies where the worm can dig. In open air it does not steer at all:
        /// it keeps the momentum it left the ground with and falls back in. The rush is
        /// the one attack allowed to fly, both halves of it.
        /// </summary>
        private void Steer(Vector2 aim)
        {
            // Note there is no special case for phase 3 here any more. It used to fall
            // ballistically, killing the X and adding gravity, and that had the same fault
            // as the old steering in a different place: a rush that went up nearly straight
            // arrives at the top of its arc with both components at nothing, so the head
            // hung there at a twentieth of a pixel a tick and flipped from pointing up to
            // pointing down in one tick, with the body still climbing into it. Measured
            // over twelve fights it was the only remaining moment the worm stopped.
            //
            // It now turns over the top like anything else, aiming straight down rather
            // than at you, so it still cannot chase on the way back. Once it is pointing
            // down the swing is small, the open air rule below takes over, and the rest of
            // the descent is an ordinary fall.

            float top = phase == 2 ? Abomanation.RushSpeed : Abomanation.Speed;

            // The whip's run in is ordinary chasing, so it corners like ordinary chasing.
            float turn = phase == 0 || phase == 4 ? Abomanation.TurnSpeed : Abomanation.TurnSpeed * 1.5f;

            // ...that gets faster the longer it goes on, both numbers together so the
            // turning circle holds. Keeping away from the whip only makes it arrive
            // harder.
            if (phase == 4)
            {
                float chase = Math.Min(Abomanation.WhipChaseMax, 1f + attackTimer * Abomanation.WhipChaseGain);
                top *= chase;
                turn *= chase;
            }

            // The second half is quicker, both numbers together so the circle it turns
            // in stays the same size.
            if (Enraged)
            {
                top *= Abomanation.RageSpeed;
                turn *= Abomanation.RageSpeed;
            }

            Vector2 want = (aim - NPC.Center).SafeNormalize(Vector2.UnitY);

            // No velocity is no heading, and nothing to rotate. Point it where it wants to
            // go and let the speed build from a standstill.
            if (NPC.velocity.LengthSquared() < 0.01f)
            {
                NPC.velocity = want * Abomanation.SpeedChange;
                return;
            }

            float heading = NPC.velocity.ToRotation();

            // Signed, wrapped to a half turn either way, so the worm always goes round the
            // short side. Exactly behind wraps to +Pi rather than to zero, which is what
            // stops a target dead astern from being read as no turn at all.
            float swing = MathHelper.WrapAngle(want.ToRotation() - heading);

            bool inTiles = Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

            // It only steers where it can dig. Out in open air it is a thrown thing: it
            // keeps the momentum it left the ground with, gravity takes it, and it goes
            // back into the sand nose first. The U turn happens down there.
            //
            // There used to be a `Math.Abs(swing) > TurnGate` clause here, put in so a
            // reversal begun above ground could finish above ground. That was wrong. It
            // let the worm bank in mid air, which is the one thing a worm cannot do, and
            // it made the surface a place the fight could live in rather than a place the
            // fight passes through.
            //
            // The rush is the exception and the only one, both halves of it: phase 2 going
            // up and phase 3 coming over the top. That attack is supposed to leave the
            // ground and is powered the whole way.
            // The whip lives on the surface, all three parts of it, so it is powered
            // there too.
            bool swims = Abomanation.Fly
                || inTiles
                || phase >= 2
                || NPC.Distance(aim) > Abomanation.AirSwimDistance;

            if (!swims)
            {
                // Gravity, and nothing else. No cap on the fall, no drag on the
                // sideways half, no floor under either: whatever it was doing when it left
                // the sand is what it keeps, and the arc falls out of that. See AirGravity.
                NPC.velocity.Y += Abomanation.AirGravity;

                return;
            }

            if (NPC.soundDelay == 0)
            {
                NPC.soundDelay = (int)MathHelper.Clamp(NPC.Distance(aim) / 40f, 10f, 20f);
                SoundEngine.PlaySound(SoundID.WormDig, NPC.Center);
            }

            // Speed along the heading, never across it. This is the half that keeps a turn
            // from costing anything.
            float speed = NPC.velocity.Length();

            if (speed < top)
                speed = Math.Min(top, speed + Abomanation.SpeedChange);
            else if (speed > top)
                speed = Math.Max(top, speed - Abomanation.SpeedChange);

            NPC.velocity = (heading + MathHelper.Clamp(swing, -turn, turn)).ToRotationVector2() * speed;
        }

        /// <summary>Sand off the head while it is coming up, so the rush has a tell above ground.</summary>
        private void Breach()
        {
            if (Main.dedServ || NPC.alpha > 0)
                return;

            for (int i = 0; i < 2; i++)
            {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Sand,
                    NPC.velocity.X * 0.2f, NPC.velocity.Y * 0.2f, 100, default, 1.6f);

                Main.dust[dust].noGravity = true;
            }
        }

        /// <summary>
        /// Digs down and, once it is far enough under, takes the whole worm off the board.
        /// Removing the head alone would leave sixteen segments to die one per tick down
        /// the chain, in view, with nothing to follow.
        /// </summary>
        private void Leave()
        {
            NPC.velocity.Y += 0.4f;

            if (NPC.velocity.Y > 24f)
                NPC.velocity.Y = 24f;

            NPC.velocity.X *= 0.96f;

            if (NPC.velocity.LengthSquared() > 0.01f)
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;

            if (NPC.Center.Y <= Main.rockLayer * 16.0 + 1200.0)
                return;

            int head = ModContent.NPCType<AbomanationHead>();
            int body = ModContent.NPCType<AbomanationBody>();
            int tail = ModContent.NPCType<AbomanationTail>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active && (other.type == head || other.type == body || other.type == tail))
                    other.active = false;
            }
        }

        /// <summary>
        /// Builds the chain, once, on whichever machine decides things.
        ///
        /// Each segment is told who is in front of it, who the head is, where along the
        /// body it sits, and which way round its sprite is drawn. The place is what picks
        /// its texture and seeds its arms, so the five body sprites cycle in order rather
        /// than being rolled per segment.
        ///
        /// They are also PLACED, which is the part Desert Scourge does not do. It spawns
        /// every segment on the head's exact centre and lets them string themselves out
        /// over the following second, so the boss arrives as a ball of sixteen overlapping
        /// bodies that unrolls in front of you. Here the chain is laid out before anything
        /// is drawn: straight down from the head to begin with, bending a little further
        /// with each segment, so what appears is a worm already in the ground with only
        /// its head at the surface.
        ///
        /// The step between segments is the same straight line distance their follow code
        /// holds them at, so on the first tick there is nothing for it to correct and the
        /// shape does not twitch.
        /// </summary>
        private void SpawnBody()
        {
            spawnedBody = true;

            // One roll for every arm on the animal. SendExtraAI carries it to the clients,
            // and the head is where the segments look it up.
            ArmSeed = Main.rand.Next(int.MaxValue);

            int previous = NPC.whoAmI;
            int bodyType = ModContent.NPCType<AbomanationBody>();
            int tailType = ModContent.NPCType<AbomanationTail>();

            Vector2 at = NPC.Center;

            // Straight down, then bending. PiOver2 is down, because +Y is down. The lean
            // is rolled so two spawns in the same spot do not lie in the same shape.
            //
            // An arriving worm is laid the other way: trailing back along the ground it
            // is about to travel, bending down into it, so the first thing that surfaces
            // is a head with a body behind it rather than a head with a body under it.
            float angle = MathHelper.PiOver2;
            float bend = Main.rand.NextBool() ? Abomanation.BuriedCurve : -Abomanation.BuriedCurve;

            if (phase == -1)
            {
                angle = arriveDir > 0 ? MathHelper.Pi : 0f;
                bend = arriveDir > 0 ? -Abomanation.BuriedCurve : Abomanation.BuriedCurve;
            }

            for (int i = 0; i <= Abomanation.BodySegments; i++)
            {
                bool last = i == Abomanation.BodySegments;

                float step = i == 0 ? Abomanation.HeadSpacing
                    : last ? Abomanation.TailSpacing
                    : Abomanation.BodySpacing;

                at += angle.ToRotationVector2() * step;
                angle += bend;

                int made = NPC.NewNPC(NPC.GetSource_FromAI(), (int)at.X, (int)at.Y,
                    last ? tailType : bodyType, NPC.whoAmI);

                if (made < 0 || made >= Main.maxNPCs)
                    continue;

                NPC segment = Main.npc[made];

                // NewNPC's Y argument is the BOTTOM of the hitbox, not the middle, so the
                // position it just worked out is half a segment off. Writing Center says
                // what was meant and cannot be read the other way.
                segment.Center = at;

                // Pointing at whatever is in front of it, so the first frame drawn is the
                // shape this loop built rather than sixteen segments all facing right.
                segment.rotation = (Main.npc[previous].Center - at).ToRotation() + MathHelper.PiOver2;

                AbomanationChain.Link(segment, previous, NPC.whoAmI, i, !last && Main.rand.NextBool());

                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, made);

                previous = made;
            }

            // The head faces the way the body is not, and is already moving that way, so
            // the steering has a heading to bend rather than a standstill to start from.
            if (phase == -1)
            {
                NPC.velocity = Vector2.UnitX * arriveDir * Abomanation.ArriveSpeed;
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
            }
            else
            {
                NPC.rotation = 0f;
                NPC.velocity = -Vector2.UnitY * Abomanation.Speed * 0.5f;
            }

            NPC.netUpdate = true;
        }

        // ------------------------------------------------------------------- loot -----

        /// <summary>
        /// Drops fall from the part of the animal nearest the nearest player, not from
        /// the head. The head is wherever the last move left it, often deep in the
        /// ground or off the far side of the screen; the body is long enough that some
        /// piece of it is usually right by you. Runs before the loot is rolled, so
        /// moving the head onto that segment is enough: every drop reads the head's
        /// position.
        /// </summary>
        public override bool PreKill()
        {
            Player near = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)];

            Vector2 best = NPC.Center;
            float bestDistance = Vector2.Distance(NPC.Center, near.Center);

            int body = ModContent.NPCType<AbomanationBody>();
            int tail = ModContent.NPCType<AbomanationTail>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (!other.active || (other.type != body && other.type != tail) || AbomanationChain.Head(other) != NPC.whoAmI)
                    continue;

                float distance = Vector2.Distance(other.Center, near.Center);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other.Center;
                }
            }

            NPC.Center = best;

            return true;
        }

        // ------------------------------------------------------------------- draw -----

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            return AbomanationChain.Touching(NPC, target, 34f);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 4; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);

            if (NPC.life <= 0)
            {
                for (int i = 0; i < 20; i++)
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            spriteBatch.Draw(texture, NPC.Center - screenPos, null, NPC.GetAlpha(drawColor),
                NPC.rotation, texture.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
