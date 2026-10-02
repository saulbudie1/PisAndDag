using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Structures;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Items.Summoner;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
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
    /// The fight is a chase, broken every few seconds by one of four things. It goes for
    /// you, slightly off centre (<see cref="Abomanation.Stray"/>) so a straight line never
    /// quite works; then it rushes (dives, turns around a long way down, and comes back up
    /// over you or through you), or brings up what is in it, or lies down on the hill and
    /// throws ash, or, hurt, lies down and vents. A whip may follow any of them. In
    /// between, it hurts. The chase, the rush and the whip are in this file; the rest is
    /// in AbomanationHead.Moves.cs, and everything that is only seen or heard in
    /// AbomanationHead.Show.cs.
    ///
    /// It is called up a long way off (<see cref="Summon"/>) and comes to you through the
    /// surface in a slow wave with the camera on it; the fight starts when the camera is
    /// back on you. Its loot is under ModifyNPCLoot and BossLoot.
    /// </summary>
    [AutoloadBossHead]
    public partial class AbomanationHead : ModNPC
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

        /// <summary>-1 arriving, 0 chasing, 1 diving, 2 rushing up, 3 falling back, 4 whip run in, 5 whip loop, 6 whip crack, 7 the end; 8 and up, the moves in AbomanationHead.Moves.cs. The move, not the half of the fight: that is <see cref="Enraged"/>.</summary>
        private int phase;

        /// <summary>
        /// Which way the loop turns: +1 clockwise on screen, -1 antic  lockwise. The tail
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

        /// <summary>Legends, first half: how long the gap has to be this time, rolled after each whip. Synced.</summary>
        private int whipGapNeed = Abomanation.LegendsWhipGapLeast;

        /// <summary>Legends, second half: ticks until the whip owed for the last rush, or 0 for none owed. Synced.</summary>
        private int whipOwed;

        /// <summary>Legends, second half: ticks until the look after a whip, or 0 for none coming. Synced.</summary>
        private int lookAfterWhip;

        /// <summary>Legends: this dive and rush are the fast one, after a whip found you far off. Synced.</summary>
        private bool farRush;

        /// <summary>Where the player has been, the last RushLag ticks, so a rush can aim at where they were. Local.</summary>
        private readonly Vector2[] trace = new Vector2[Abomanation.RushLag + 1];
        private int traceAt;
        private bool traced;

        /// <summary>Whether the second half has been announced here. Local: each machine roars once on seeing the health cross.</summary>
        private bool roared;

        /// <summary>Whether the arrival's screech has gone off here. Local: every machine runs the arrival.</summary>
        private bool screeched;

        /// <summary>Ticks left with the mouth held wide for a screech or a roar. Local, like the rest of the face.</summary>
        private int roarFace;

        // ---- the end. See Die(). All synced. ----

        /// <summary>Which part of the end it is at: 0 stopping, 1 rising, 2 looping, 3 eating, 4 gone.</summary>
        private int dieStep;

        /// <summary>Pieces eaten so far. Indexes DiePiece.</summary>
        private int eaten;

        /// <summary>Ticks left with the mouth shut after a bite. Local: what it bit is synced, the face is not.</summary>
        private int chomp;

        /// <summary>Set for the one tick the real death is allowed through CheckDead.</summary>
        private bool finishing;

        /// <summary>World Y the head climbs to before it loops: the height the loop will sit at.</summary>
        private float riseTo;

        /// <summary>
        /// The loop. The mouth is where the head stopped rising and where the lap
        /// starts and ends: the tail ends up one link ahead of it. The loop hangs off
        /// it to one side (loopSide, +1 right, -1 left), which is also the way the head
        /// goes round it. loopCentre never moves. lapped is how far the head has gone
        /// round on the lap. Eating: remain is how much body is left, head centre to
        /// tail centre, which sizes the loop (remain plus the mouth round); headAngle
        /// is where the head is on it, and stepFrom where it was for the last bite.
        /// Synced; every part places itself from these.
        /// </summary>
        private Vector2 mouth;
        private Vector2 loopCentre;
        private int loopSide = 1;
        private float lapped;
        private float remain;
        private float headAngle;
        private float stepFrom;

        /// <summary>
        /// The head's path since it began to rise, oldest first, on this machine. The
        /// body is threaded along it by distance, so every part goes exactly where the
        /// head went. Seeded from where the body is when the rise starts, so nothing
        /// jumps. Local: it is built from the head's position, which is synced.
        /// </summary>
        private readonly List<Vector2> trail = new List<Vector2>();

        /// <summary>Where the camera is looking during the end, eased. Local.</summary>
        private Vector2 look;
        private bool looking;

        /// <summary>The second half of the fight: health at or under <see cref="Abomanation.RagePoint"/>. Read off health, so it needs no sync.</summary>
        public bool Enraged => NPC.life <= NPC.lifeMax * Abomanation.RagePoint;

        /// <summary>Ticks into the arrival.</summary>
        private int arriveTimer;

        /// <summary>Which way it is travelling while arriving: +1 right, -1 left.</summary>
        private int arriveDir;

        /// <summary>The ground line under the head while arriving, in world pixels, smoothed. The wave is centred on it.</summary>
        private float arriveBase;

        /// <summary>Ticks of chase since the last move. The next starts once this reaches the gate rolled for it (moveGate).</summary>
        private int chargeTimer;

        /// <summary>Ticks the current attack has taken. A watchdog, nothing else.</summary>
        private int attackTimer;

        /// <summary>World Y the rush climbs to, fixed at the moment it turns around.</summary>
        private float rushTop;

        /// <summary>
        /// The small thing it is after, or -1. Bunnies and squirrels on the Mile, the
        /// ones that called it, are eaten before the player is looked at: it goes
        /// straight through each, and only when there are none left in reach does it
        /// turn on you. Synced, so every client steers toward the same animal.
        /// </summary>
        private int prey = -1;
        private int preyLook;

        /// <summary>Moves finished since the last whip (they were all rushes once, hence the name). The second half whips on every second one. Synced.</summary>
        private int rushesSinceWhip;

        /// <summary>Set once the player is too far to bother with. Synced.</summary>
        private bool leaving;

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
            Main.npcFrameCount[Type] = Abomanation.HeadFrames;

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
            NPC.PAD().BossName = "Amalgaermis";
            NPC.PAD().BossTitle = "The starved hundred who merged into one.";
            NPC.PAD().OwnIntro = true;
        }

        /// <summary>
        /// Health per difficulty, written rather than scaled, for the reason Megashark
        /// gives: the automatic expert scaling doubles whatever SetDefaults left behind,
        /// so a number picked in one place lands somewhere else in the other modes.
        /// </summary>
        private static int LifeForMode()
        {
            int life = Main.masterMode ? 36000 : Main.expertMode ? 26000 : 16000;
            return PADWorld.Legends ? (int)(life * Abomanation.LegendsLife) : life;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = LifeForMode();
            NPC.life = NPC.lifeMax;

            // After vanilla's own difficulty scaling, so it is on top of it.
            if (PADWorld.Legends)
                NPC.damage = (int)(NPC.damage * Abomanation.LegendsDamage);
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
            writer.Write(whipGapNeed);
            writer.Write(whipOwed);
            writer.Write(lookAfterWhip);
            writer.Write(farRush);
            writer.Write(prey);
            writer.Write(leaving);
            writer.Write(rushesSinceWhip);
            writer.Write(dieStep);
            writer.Write(eaten);
            writer.Write(riseTo);
            writer.WriteVector2(mouth);
            writer.WriteVector2(loopCentre);
            writer.Write(loopSide);
            writer.Write(lapped);
            writer.Write(remain);
            writer.Write(headAngle);
            writer.Write(stepFrom);

            SendMoves(writer);

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
            whipGapNeed = reader.ReadInt32();
            whipOwed = reader.ReadInt32();
            lookAfterWhip = reader.ReadInt32();
            farRush = reader.ReadBoolean();
            prey = reader.ReadInt32();
            leaving = reader.ReadBoolean();
            rushesSinceWhip = reader.ReadInt32();
            dieStep = reader.ReadInt32();
            eaten = reader.ReadInt32();
            riseTo = reader.ReadSingle();
            mouth = reader.ReadVector2();
            loopCentre = reader.ReadVector2();
            loopSide = reader.ReadInt32();
            lapped = reader.ReadSingle();
            remain = reader.ReadSingle();
            headAngle = reader.ReadSingle();
            stepFrom = reader.ReadSingle();
            ReceiveMoves(reader);
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

            // The held face runs out on its own clock, whatever the head is doing: going
            // too, or it would dig away with its mouth still wide. Where it was held for
            // something that ends in a bite (Gape), the mouth shuts as it runs out.
            if (roarFace > 0 && --roarFace == 0 && snapAfter > 0)
            {
                chomp = snapAfter;
                snapAfter = 0;
            }

            // Nobody left to chase, or nobody close enough to be worth it. Dig, and take
            // the whole body off the board once it is deep enough that nothing is going
            // to see it go. Latched: once it has turned to go, it goes.
            if (!leaving && phase >= 0 && phase != DyingPhase && NPC.Distance(player.Center) > Abomanation.LeaveRange)
            {
                leaving = true;
                NPC.netUpdate = true;
            }

            if (!player.active || player.dead || leaving)
            {
                Leave();
                return;
            }

            NPC.localAI[1]++;

            if (chomp > 0)
                chomp--;

            if (Enraged && !roared)
            {
                roared = true;

                // Not over its own end: a blow that takes it from the first half to
                // nothing in one goes straight to that.
                if (phase != DyingPhase)
                    RageRoar();
            }

            Trace(player);

            if (phase == DyingPhase)
            {
                Notice();
                Die();
                return;
            }

            if (phase == -1)
            {
                // Its own arrival is a shot too.
                Cinematic.Frame();
                Arrive(player);
            }
            else
            {
                Hunt();
                Clocks();
                Advance(player);

                // The loop and the straight part of the crack drive the head themselves,
                // in Advance, and so do the parts of the new moves whose shape matters.
                // Steering would fight them.
                if (!Driving)
                    Steer(Aim(player));

                // Once the head is chasing again the first link follows where the head
                // is actually going, not where it was sent.
                if (phase == 6 && !Driving && NPC.velocity.LengthSquared() > 0.01f)
                    whipHeading = NPC.velocity.ToRotation();
            }

            // What this machine has not yet shown of where the fight has got to: a phase
            // begun, a spasm begun. Looked for here, after the tick's own changes and
            // before the face is read, so it is the same whether this machine made the
            // change or was sent it, and a mouth shut by a phase ending shuts on the very
            // tick it was last wide.
            Notice();

            // Choking, heaving and lying down it faces where it is told to. Everywhere
            // else, the way it is going.
            if (HoldsFacing)
                NPC.rotation = facing + MathHelper.PiOver2;
            else if (NPC.velocity.LengthSquared() > 0.01f)
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;

            Show();

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
        /// The state machine. Every transition of the rush and the whip is a position it
        /// reached rather than a timer running out, so an attack cannot fire from the wrong
        /// place. The timer only decides when a move is allowed to start, and which move
        /// was rolled (AbomanationHead.Moves.cs).
        ///
        /// Everything that is a DECISION (which move, which kind of rush, whether a whip
        /// follows) is made on the machine that owns the animal and sent. A client plays
        /// out what it is told and never starts anything itself.
        /// </summary>
        private void Advance(Player player)
        {
            switch (phase)
            {
                case 0:
                    chargeTimer++;
                    whipGap++;

                    // Not while there is something small to eat. The player waits.
                    if (Hunting)
                    {
                        chargeTimer = 0;
                        break;
                    }

                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        break;

                    // Legends, second half: the whip owed for the last move, once its
                    // wait is up, and nothing else starts while one is owed; and the look
                    // after a whip, which is a rush at once, the fast one, if you have got
                    // far away.
                    if (PADWorld.Legends)
                    {
                        if (whipOwed > 0)
                        {
                            if (--whipOwed == 0)
                                StartWhip();

                            break;
                        }

                        if (lookAfterWhip > 0 && --lookAfterWhip == 0
                            && Enraged && NPC.Distance(player.Center) > Abomanation.LegendsFarRush)
                        {
                            StartRush(player, true);
                            break;
                        }
                    }

                    // The chase has run as long as was rolled for it: the move that was
                    // rolled with it starts, if it can from where the head is. A rush still
                    // waits for the head to be below you, which is what a dive is, and
                    // past RushGrace goes anyway: a player who keeps it above them forever
                    // still gets attacked, just late.
                    if (chargeTimer >= moveGate)
                        TryMove(player);

                    break;

                case 1:
                    attackTimer++;

                    float turnAt = rushKind == 1 ? ThroughStart(player).Y : DiveTarget(player);

                    if (NPC.Center.Y >= turnAt - 16f)
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
                        rushLocked = false;
                        aligned = false;
                        slipping = false;
                        closest = float.MaxValue;
                        NPC.netUpdate = true;

                        // Its tell is its voice, played where this machine sees the phase
                        // begin (Entered).
                    }
                    break;

                case 2:
                    attackTimer++;

                    if (rushKind == 1)
                    {
                        Through();
                    }
                    else if (NPC.Center.Y <= rushTop)
                    {
                        phase = 3;
                        attackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 3:
                    attackTimer++;

                    // Spent once it is back under the ground, or once it is a long way
                    // below you. Waiting only on the depth would leave it falling for
                    // twenty seconds if you flew up while it came down.
                    bool back = attackTimer > 30 && Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

                    if (back || NPC.Center.Y >= player.Center.Y + Abomanation.DiveDepth * 0.5f)
                    {
                        Reset();

                        // A finished move is the only place the whip comes from.
                        AfterMove(player);
                    }
                    break;

                case 4:
                    attackTimer++;

                    // Ordinary chasing, straight at you, until it is close enough to
                    // loop. Nothing scripted about the approach: same speed, same turn
                    // rate, so it reads as the fight rather than a cutscene starting.
                    //
                    // Capped. A worm steering at a target that keeps moving can circle it
                    // for as long as you care to run, never quite inside the distance;
                    // that was the endless loop. Past WhipRunIn it loops from wherever it
                    // is if you are anywhere near, and gives the whip up if you are not.
                    if (NPC.Distance(player.Center) <= Abomanation.WhipNear * 16f)
                        Loop(player);
                    else if (attackTimer >= Abomanation.WhipRunIn)
                    {
                        if (NPC.Distance(player.Center) <= Abomanation.WhipRange)
                            Loop(player);
                        else
                            Reset();
                    }
                    break;

                case 5:
                    attackTimer++;

                    // Untouchable from the loop to the end of the crack. Tether carries
                    // it down the chain. The whip still hurts: contact reads Arriving,
                    // not this.
                    NPC.dontTakeDamage = true;

                    // Round once. The body follows it into the circle — the coil — the
                    // way it follows everything, and that is the tell. Then it holds the
                    // coil, creeping round it at a crawl, for a second more: time to
                    // read it and get out of the way before it lets go.
                    bool coiled = attackTimer >= Abomanation.WhipLoopTicks;
                    float pace = coiled ? Abomanation.WhipLoopSpeed * Abomanation.WhipHoldPace : Abomanation.WhipLoopSpeed;

                    whipHeading += whipSense * pace / Abomanation.WhipLoopRadius;
                    NPC.velocity = whipHeading.ToRotationVector2() * pace;

                    if (attackTimer >= Abomanation.WhipLoopTicks + Abomanation.WhipHold)
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

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            whipGapNeed = Main.rand.Next(Abomanation.LegendsWhipGapLeast, Abomanation.LegendsWhipGapMost + 1);

                            if (PADWorld.Legends && Enraged)
                                lookAfterWhip = Abomanation.LegendsAfterWhip;
                        }
                    }
                    break;

                case PukeSeek:
                    PukeRise(player);
                    break;

                case PukeChoke:
                    Choke(player);
                    break;

                case PukeSpew:
                    Spew(player);
                    break;

                case LayOut:
                    LaySeeks();
                    break;

                case LayCrawl:
                case LayAct:
                    Lies(player);
                    break;

                case Sinking:
                    Sinks(player);
                    break;
            }

            // Anything that has taken this long has got itself stuck on geometry. Drop it
            // and go back to chasing rather than leaving the fight hanging on a corner.
            int timeout = phase >= PukeSeek ? Abomanation.MoveTimeout
                : phase >= 4 ? Abomanation.WhipTimeout
                : Abomanation.RushTimeout;

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

        /// <summary>Eating itself. The player is held, nothing hurts anyone.</summary>
        public bool Dying => phase == DyingPhase;

        private const int DyingPhase = 7;

        // The end's steps. The body follows the head while it stops; from the rise on
        // the head places every part itself. See Die.
        private const int StepStop = 0, StepRise = 1, StepLoop = 2, StepEat = 3, StepGone = 4;

        /// <summary>While dying: the body still follows the head (stopping).</summary>
        public bool BodyFollows => Dying && dieStep == StepStop;

        /// <summary>While dying: the head places every part. They call <see cref="Place"/> and do nothing else.</summary>
        public bool BodyPlaced => Dying && dieStep >= StepRise;

        /// <summary>The mouth's angle about the loop's centre. The loop hangs off the mouth to one side, so this is fixed.</summary>
        private float MouthAngle => loopSide > 0 ? MathHelper.Pi : 0f;

        /// <summary>The loop's radius while it is being eaten: what is left, plus the mouth, round. Shrinks a link a bite.</summary>
        private float Radius => (remain + Abomanation.DieMouth) / MathHelper.TwoPi;

        /// <summary>A point on the loop at this angle about its centre, and the heading of the loop there, the way the head goes.</summary>
        private void OnLoop(float angle, float radius, out Vector2 at, out float heading)
        {
            at = loopCentre + angle.ToRotationVector2() * radius;
            heading = angle + loopSide * MathHelper.PiOver2;
        }

        /// <summary>
        /// Where a part this far back along the chain sits, and which way it faces.
        /// While the head rises and loops it is threaded along the head's path; once
        /// the loop is closed it is on the loop, which by then is the same line, this
        /// far behind the head round it. The loop is sized to what is left, so the
        /// body is always closed up behind the head and the last piece is always at
        /// its mouth. The body faces along itself, the way the head goes.
        /// </summary>
        public void Place(NPC part)
        {
            float back = AbomanationChain.Along((int)part.ai[3]);

            part.velocity = Vector2.Zero;

            if (dieStep >= StepEat)
            {
                float radius = Radius;

                OnLoop(headAngle - loopSide * back / radius, radius, out Vector2 slot, out float heading);
                part.Center = slot;
                part.rotation = heading + MathHelper.PiOver2;
                return;
            }

            if (trail.Count == 0)
                Seed();

            TrailPoint(back, out Vector2 at, out float facing);
            part.Center = at;
            part.rotation = facing;
        }

        /// <summary>
        /// The point this far back along the head's path from where the head is now,
        /// and the facing of the path there, toward the head. Past the start of the
        /// path, the start.
        /// </summary>
        private void TrailPoint(float back, out Vector2 at, out float facing)
        {
            float gone = 0f;

            for (int i = trail.Count - 1; i > 0; i--)
            {
                Vector2 near = trail[i];
                Vector2 far = trail[i - 1];
                float step = Vector2.Distance(near, far);

                if (gone + step >= back)
                {
                    float t = step > 0.0001f ? (back - gone) / step : 0f;
                    at = Vector2.Lerp(near, far, t);
                    facing = (near - far).ToRotation() + MathHelper.PiOver2;
                    return;
                }

                gone += step;
            }

            at = trail[0];
            facing = trail.Count > 1
                ? (trail[1] - trail[0]).ToRotation() + MathHelper.PiOver2
                : NPC.rotation;
        }

        /// <summary>
        /// The path starts as the body is: tail first, up the chain to the head. So the
        /// first tick of the rise threads every part onto exactly where it already was.
        /// </summary>
        private void Seed()
        {
            trail.Clear();

            for (int place = Abomanation.BodySegments; place >= 0; place--)
            {
                NPC part = AbomanationChain.PartAt(NPC.whoAmI, place);

                if (part != null)
                    trail.Add(part.Center);
            }

            if (trail.Count == 0)
                trail.Add(NPC.Center + (NPC.rotation + MathHelper.PiOver2).ToRotationVector2() * Abomanation.ChainLength);

            trail.Add(NPC.Center);
        }

        /// <summary>The head has moved: the path grows, and is trimmed to what the body can use.</summary>
        private void Record()
        {
            if (Vector2.DistanceSquared(trail[trail.Count - 1], NPC.Center) > 0.25f)
                trail.Add(NPC.Center);

            float keep = Abomanation.ChainLength + 200f;
            float total = 0f;

            for (int i = trail.Count - 1; i > 0; i--)
            {
                total += Vector2.Distance(trail[i], trail[i - 1]);

                if (total > keep)
                {
                    trail.RemoveRange(0, i - 1);
                    break;
                }
            }
        }

        /// <summary>True while the head's motion is scripted: the loop, the straight part of the run, and the driven parts of the newer moves.</summary>
        private bool Driving => phase == 5 || (phase == 6 && attackTimer <= Abomanation.WhipRunStraight) || MoveDrives;

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
            int gap = Enraged ? Abomanation.RageWhipGap
                : PADWorld.Legends ? whipGapNeed
                : Abomanation.WhipGap;

            return whipGap >= gap && NPC.Distance(player.Center) <= Abomanation.WhipRange;
        }

        /// <summary>Calls the whip. Everything it needs is decided later, at the loop.</summary>
        private void StartWhip()
        {
            rushesSinceWhip = 0;
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

            // The tell (the star, the note, and its call) is given where this machine
            // sees the loop begin: AbomanationHead.Show.cs, Entered. A client that is
            // told about the loop rather than reaching it gets the tell all the same.
        }

        /// <summary>Lets it go. From here the body is a chain with momentum and the head just runs.</summary>
        private void Crack()
        {
            whipSerial++;
            phase = 6;
            attackTimer = 0;

            // The throw's sound is played where this machine sees it begin (Entered).
            // The crack itself comes later, from the tail.
            NPC.netUpdate = true;
        }

        /// <summary>Back to the chase, from anything. On the deciding machine the next move, and how long before it, are rolled here.</summary>
        private void Reset()
        {
            phase = 0;
            stage = 0;
            chargeTimer = 0;
            attackTimer = 0;
            rushTop = 0f;
            farRush = false;
            rushLocked = false;
            NPC.dontTakeDamage = false;
            RollMove();
            NPC.netUpdate = true;
        }

        /// <summary>
        /// Where the player has been. Written every tick; <see cref="Lagged"/> reads
        /// the oldest. Started full of where they are now, so the first rush of a
        /// fight has something to aim at.
        /// </summary>
        private void Trace(Player player)
        {
            if (!traced)
            {
                for (int i = 0; i < trace.Length; i++)
                    trace[i] = player.Center;

                traced = true;
            }

            traceAt = (traceAt + 1) % trace.Length;
            trace[traceAt] = player.Center;
        }

        /// <summary>Where the player was RushLag ticks ago. Where a rush goes.</summary>
        private Vector2 Lagged => trace[(traceAt + 1) % trace.Length];

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

            // The screech: last, before the camera comes home. Its wave stands still for
            // it, so it stays up out of the ash for the whole of it instead of going back
            // under halfway through, and it rears a little higher and settles again. The
            // clock the wave runs on is the arrival's less the screech, so it picks up
            // exactly where it stopped. The same sum on every machine.
            int screechAt = Abomanation.ScreechAt;
            int into = arriveTimer - screechAt;
            bool screeching = into >= 0 && into < Abomanation.ScreechTicks;
            float clock = arriveTimer - Math.Clamp(into, 0, Abomanation.ScreechTicks);

            if (screeching && !screeched && into < 20)
            {
                screeched = true;
                Screech();
            }

            // Starts at the trough, which is where Summon put it.
            float theta = clock * MathHelper.TwoPi / Abomanation.ArrivePeriod + MathHelper.PiOver2;
            float wave = (float)Math.Sin(theta) * Abomanation.ArriveWave;
            float slope = screeching ? 0f : (float)Math.Cos(theta) * Abomanation.ArriveWave * MathHelper.TwoPi / Abomanation.ArrivePeriod;
            float rear = screeching ? Abomanation.ScreechRear * (float)Math.Sin(MathHelper.Pi * into / Abomanation.ScreechTicks) : 0f;

            NPC.velocity.X = arriveDir * Abomanation.ArriveSpeed;
            NPC.velocity.Y = slope + (arriveBase + wave - rear - NPC.Center.Y) * 0.1f;

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

                // The fight starts: what it will do first, and when it will first hurt.
                RollMove();

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    spasmWait = Main.rand.Next(Abomanation.SpasmFirstLeast, Abomanation.SpasmFirstMost + 1);

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

        /// <summary>
        /// The screech at the end of the arrival: its own voice (Abomanation.ScreechSound),
        /// the screen taking it (Cinematic.Roar, at full strength: the camera is on it and
        /// nothing else is happening), and the mouth wide.
        /// </summary>
        private void Screech()
        {
            roarFace = Abomanation.ScreechTicks;

            if (Main.dedServ)
                return;

            Cinematic.Roar(NPC, Abomanation.ScreechTicks, 1f, Abomanation.ScreechSound);
        }

        /// <summary>
        /// The second half, announced: low, long and loud. Two voices of the vanilla roar
        /// at once, the deep one an octave down (its two seconds drawn out to four) and
        /// the one it has always had under it, both heard at full strength wherever the
        /// player is; the screen takes it lightly (Cinematic.Roar at RageRoarStrength),
        /// and the mouth is held wide for all four seconds.
        ///
        /// Unlimited instances on both, or a roar already playing (every rush has one)
        /// would have the new one refused: vanilla's roar lets only one play at a time.
        /// </summary>
        private void RageRoar()
        {
            roarFace = Abomanation.RageRoarTicks;

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = Abomanation.RageDeepPitch, PitchVariance = 0f, Volume = 1f, MaxInstances = 0 });
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = Abomanation.RagePitch, PitchVariance = 0f, Volume = 0.75f, MaxInstances = 0 });

            Cinematic.Roar(NPC, Abomanation.RageRoarTicks, Abomanation.RageRoarStrength, quiet: true);
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
                    // Under you, for the rush over you; off to one side and not so deep,
                    // for the one through you.
                    return rushKind == 1 ? ThroughStart(player) : new Vector2(player.Center.X, DiveTarget(player));

                case 2:
                    // Where you were a third of a second ago, not where you are. Stand
                    // still and it is the same thing. Over you: a point above that. Through
                    // you: that, and once the line is set, the far end of the line.
                    if (rushKind == 1)
                        return rushLocked ? passTarget : Lagged;

                    return new Vector2(Lagged.X, rushTop);

                case 3:
                    // Unsteered anyway; this only keeps the rotation pointing the way it
                    // is actually going.
                    return NPC.Center + Vector2.UnitY * 200f;

                case 4:
                    // Straight at you, and nothing else. No stray, no pass by: this is
                    // the one time it is actually coming for you.
                    return player.Center;

                case PukeSeek:
                    return PukeAim(player);

                case LayOut:
                    return LayAim();

                default:
                    // Straight through the animal, no pass by: it is a mouthful, not a fight.
                    if (Hunting)
                        return Main.npc[prey].Center;

                    return PassAim(player);
            }
        }

        // ---- prey ------------------------------------------------------------

        private static bool IsPrey(NPC npc) =>
            npc.active && npc.life > 0
            && (npc.type == NPCID.Bunny || npc.type == NPCID.Squirrel || npc.type == NPCID.SquirrelRed);

        private bool Hunting => phase == 0 && prey >= 0 && prey < Main.maxNPCs && IsPrey(Main.npc[prey]);

        /// <summary>
        /// Picks the nearest small animal in reach, now and then, and eats the one it
        /// is on. The pick is the server's; the bite is too, and vanilla's death does
        /// the rest on every client.
        /// </summary>
        private void Hunt()
        {
            if (phase != 0)
                return;

            if (Main.netMode != NetmodeID.MultiplayerClient && ++preyLook >= Abomanation.PreyLook)
            {
                preyLook = 0;

                int nearest = -1;
                float best = Abomanation.PreyRange * Abomanation.PreyRange;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];

                    if (!IsPrey(npc))
                        continue;

                    float d = NPC.DistanceSQ(npc.Center);

                    if (d < best)
                    {
                        best = d;
                        nearest = i;
                    }
                }

                if (nearest != prey)
                {
                    prey = nearest;
                    NPC.netUpdate = true;
                }
            }

            if (!Hunting)
                return;

            NPC animal = Main.npc[prey];

            // Boxes, or near enough: at its speed a bunny can fall between one tick's
            // box and the next.
            if (!NPC.Hitbox.Intersects(animal.Hitbox) && NPC.Distance(animal.Center) > NPC.width * 0.9f)
                return;

            Devour(animal);
        }

        /// <summary>One bite. Blood the way it is going, a wet crunch, and the animal is gone.</summary>
        private void Devour(NPC animal)
        {
            if (!Main.dedServ)
            {
                Vector2 way = NPC.velocity.SafeNormalize(Vector2.UnitY);
                Particles.BloodDroplet.SpawnBurst(animal.Center, way, 18, 40f);
                Particles.BloodDroplet.SpawnBurst(animal.Center, -way, 8, 60f);

                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.35f, Volume = 0.9f }, animal.Center);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                animal.StrikeInstantKill();

            chomp = Abomanation.ChompTicks;
            prey = -1;
            NPC.netUpdate = true;
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
        /// What the move it is in does to its pace, before the mode and the half are
        /// applied: top speed, and how hard it may turn.
        ///
        /// Turn rates above the ordinary are only ever given under the ground. A worm
        /// that corners like that in the open is a homing missile; under the ash nobody
        /// sees the corner, only where it comes up.
        /// </summary>
        private void Pace(ref float top, ref float turn, bool inTiles, Vector2 aim)
        {
            switch (phase)
            {
                case 0:
                    // The spasm: both together, so it slows on the circle it was already on.
                    if (InSpasm)
                    {
                        float hold = MathHelper.Lerp(1f, Abomanation.SpasmSlow, SpasmGrip);

                        top *= hold;
                        turn *= hold;
                    }
                    break;

                case 1:
                    if (rushKind == 1)
                    {
                        top *= Abomanation.ThroughDive;
                        turn *= Abomanation.ThroughDive;
                    }
                    break;

                case 2:
                    // The rush through you: hard round, down there, until it is pointing
                    // at where you were. Then it corners like any rush.
                    if (rushKind == 1 && !rushLocked && inTiles && NPC.velocity.LengthSquared() > 0.01f)
                    {
                        float off = Math.Abs(MathHelper.WrapAngle((aim - NPC.Center).ToRotation() - NPC.velocity.ToRotation()));

                        if (off > 0.15f)
                            turn = Abomanation.TurnSpeed * Abomanation.ThroughTurn;
                    }
                    break;

                case PukeSeek:
                    if (stage < 2)
                    {
                        // Getting there, quicker the longer it takes: a player who runs
                        // is overtaken, not followed. Hard round under the ground, but
                        // not while any of it is still in the air (Abomanation.LaySeekTurn).
                        float chase = Math.Min(Abomanation.PukeChaseMax, Abomanation.PukeChase + attackTimer * Abomanation.PukeChaseGain);

                        top *= chase;
                        turn = Abomanation.TurnSpeed * chase
                            * (bodyOut ? Abomanation.LayWide : inTiles ? Abomanation.PukeSeekTurn : 1f);
                    }
                    else
                    {
                        // There: slowing, along the same shallow line. The tell begins.
                        top *= Abomanation.PukeRiseSpeed;
                        turn = Abomanation.TurnSpeed * Abomanation.PukeSeekTurn;
                    }
                    break;

                case LayOut:
                    if (stage < 2)
                    {
                        // The same way the puke gets where it is going.
                        float chase = Math.Min(Abomanation.LayChaseMax, Abomanation.LayChase + attackTimer * Abomanation.LayChaseGain);

                        top *= chase;
                        turn = Abomanation.TurnSpeed * chase
                            * (inTiles && !bodyOut ? Abomanation.LaySeekTurn : Abomanation.LayWide);
                    }
                    else
                    {
                        // The run in, at the chase's own pace: the ash heaves over it.
                        turn = Abomanation.TurnSpeed * (inTiles ? Abomanation.LaySeekTurn : Abomanation.LayWide);
                    }
                    break;
            }
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

            bool inTiles = Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

            float top = phase == 2 ? Abomanation.RushSpeed : Abomanation.Speed;

            // The whip's run in is ordinary chasing, so it corners like ordinary chasing.
            float turn = phase == 0 || phase == 4 ? Abomanation.TurnSpeed : Abomanation.TurnSpeed * 1.5f;

            // What the move it is in does to both: the spasm's slowing, and the newer
            // moves' own paces. Nothing, for the chase, the rush over you and the whip.
            Pace(ref top, ref turn, inTiles, aim);

            if (PADWorld.Legends)
            {
                top *= Abomanation.LegendsSpeed;

                if (farRush && (phase == 1 || phase == 2))
                    top *= Abomanation.LegendsFarRushBoost;
            }

            // The whip's run in gets faster the longer it goes on, both numbers together
            // so the turning circle holds. Keeping away from the whip only makes it
            // arrive harder.
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

            // Which way round, where a move has a say in it. Nothing, almost always.
            swing = MoveSwing(swing, heading);

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
            // And the hunt: a bunny on a platform is a bunny, and it is had.
            bool swims = Abomanation.Fly
                || inTiles
                || phase >= 2
                || Hunting
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

        /// <summary>
        /// Digs down and, once it is far enough under, takes the whole worm off the board.
        /// Removing the head alone would leave sixteen segments to die one per tick down
        /// the chain, in view, with nothing to follow.
        /// </summary>
        private void Leave()
        {
            // Whatever it was in the middle of, it is not doing it any more: nothing goes
            // on steaming, throwing or lying on the hill behind a head that has gone.
            if (phase >= PukeSeek)
                phase = 0;

            spasm = 0;

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
        /// Expert and master: a treasure bag for every player who fought it (AmalgaermisBag),
        /// with the essence and Joint Arms of the Defiled in it. Classic: the essence itself,
        /// 20 to 25 for every player who fought it, and the whip, one each, every stack each
        /// player's own (PerPlayerDrop). All of it falls where PreKill moved the head.
        /// </summary>
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<AmalgaermisBag>()));

            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(new PerPlayerDrop(ModContent.ItemType<EssenceOfTheFouled>(),
                EssenceOfTheFouled.PerKillLeast, EssenceOfTheFouled.PerKillMost));
            classic.OnSuccess(new PerPlayerDrop(ModContent.ItemType<JointArmsOfTheDefiled>(), 1, 1));
            npcLoot.Add(classic);
        }

        /// <summary>Healing Potions, not the Lesser ones vanilla gives a modded boss.</summary>
        public override void BossLoot(ref int potionType)
        {
            potionType = ItemID.HealingPotion;
        }

        /// <summary>
        /// Drops fall from the part of the animal nearest the nearest player, not from
        /// the head. The head is wherever the last move left it, often deep in the
        /// ground or off the far side of the screen; the body is long enough that some
        /// piece of it is usually right by you. Runs before the loot is rolled, so
        /// moving the head onto that segment is enough: every drop reads the head's
        /// position.
        /// </summary>
        // -------------------------------------------------------------------- the end --

        /// <summary>
        /// Its life running out does not kill it. It is held at one, made untouchable,
        /// and the end begins; the real death comes from Finish, and only then.
        /// </summary>
        public override bool CheckDead()
        {
            if (finishing)
                return true;

            if (phase != DyingPhase)
            {
                phase = DyingPhase;
                dieStep = StepStop;
                eaten = 0;
                attackTimer = 0;
                lapped = 0f;
                remain = Abomanation.ChainLength;
                trail.Clear();
                looking = false;
                NPC.netUpdate = true;
            }

            NPC.life = 1;
            NPC.dontTakeDamage = true;

            // The blow that did this usually landed on a segment, and vanilla copied the
            // head's life onto that segment before calling this. Left at zero, the part
            // behind it reads it as dead next tick and the whole body cascades off.
            // Every part is put back to one here, in the same call, before any of them
            // gets a tick.
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active && AbomanationChain.Head(other) == NPC.whoAmI
                    && (other.ModNPC is AbomanationBody || other.ModNPC is AbomanationTail))
                {
                    other.life = 1;
                    other.dontTakeDamage = true;
                }
            }

            return false;
        }

        /// <summary>
        /// The end, a tick at a time. Held at one life throughout; nothing here can be
        /// interrupted, and the player is held and looked away from until the head goes.
        ///
        /// STOP: it coasts to a halt while the camera arrives, the body following.
        /// RISE: it turns to face straight up and climbs, out of the ground if it is in
        /// it, until it is in open air tall enough for the loop and at the loop's
        /// height. From here the body no longer follows: it is threaded along the
        /// head's own path, so it comes up after the head exactly where the head went.
        /// LOOP: from where it stopped, the mouth, it goes once round a circle the size
        /// of itself plus one link, slowing at the end, and arrives back at the mouth
        /// with its tail one link ahead of it, in its mouth, and the whole body on the
        /// circle behind it, closed, with no gap anywhere. EAT: each piece sits in its
        /// mouth while the mouth opens, it shuts, the piece bursts, and it steps forward
        /// one link, mouth shut, to be against the next one (DiePiece, in quarters), the body closing up behind it
        /// and the loop, sized to what is left, drawing in by the link that is gone.
        /// So it winds in on itself, the tail always in its mouth, the body always at
        /// its neck. The sits and the steps get shorter. The last piece is its own
        /// neck. GONE: a beat, and the head comes apart, and that is the death.
        /// </summary>
        private void Die()
        {
            NPC.life = 1;
            NPC.dontTakeDamage = true;

            Cinematic.Frame();
            Cinematic.Hold();
            Watch();

            attackTimer++;

            switch (dieStep)
            {
                case StepStop:
                    NPC.velocity *= 0.93f;

                    if (attackTimer >= Abomanation.DieStop)
                    {
                        riseTo = LoopHeight();
                        NPC.velocity = Vector2.Zero;
                        Seed();
                        Step(StepRise);
                    }
                    break;

                case StepRise:
                {
                    if (trail.Count == 0)
                        Seed();

                    float heading = NPC.rotation - MathHelper.PiOver2;
                    float turn = MathHelper.WrapAngle(-MathHelper.PiOver2 - heading);

                    heading += MathHelper.Clamp(turn, -Abomanation.DieRiseTurn, Abomanation.DieRiseTurn);

                    NPC.velocity = Vector2.Zero;
                    NPC.Center += heading.ToRotationVector2() * Abomanation.DieRiseSpeed;
                    NPC.rotation = heading + MathHelper.PiOver2;
                    Record();

                    if (Math.Abs(turn) <= Abomanation.DieRiseTurn && NPC.Center.Y <= riseTo)
                    {
                        // Here is the mouth. The loop hangs off it to whichever side
                        // has the most air, and the head sets off round it, upward.
                        mouth = NPC.Center;
                        loopSide = LoopSide();
                        loopCentre = mouth + new Vector2(loopSide * Abomanation.DieLoopRadius, 0f);
                        lapped = 0f;
                        NPC.rotation = 0f;
                        Step(StepLoop);
                    }
                    break;
                }

                case StepLoop:
                {
                    if (trail.Count == 0)
                        Seed();

                    float radius = Abomanation.DieLoopRadius;
                    float left = Abomanation.DieLoop - lapped;
                    float ease = MathHelper.Clamp(left / Abomanation.DieLapEase, 0f, 1f);
                    float speed = Abomanation.DieLapSpeed * MathHelper.Lerp(Abomanation.DieLapCrawl, 1f, ease);

                    lapped = Math.Min(Abomanation.DieLoop, lapped + speed);

                    float angle = MouthAngle + loopSide * lapped / radius;

                    NPC.velocity = Vector2.Zero;
                    NPC.Center = loopCentre + angle.ToRotationVector2() * radius;
                    NPC.rotation = angle + loopSide * MathHelper.PiOver2 + MathHelper.PiOver2;
                    Record();

                    if (lapped >= Abomanation.DieLoop)
                    {
                        NPC.Center = mouth;
                        NPC.rotation = 0f;
                        eaten = 0;
                        remain = Abomanation.ChainLength;
                        headAngle = MouthAngle;
                        stepFrom = MouthAngle;
                        Step(StepEat);
                    }
                    break;
                }

                case StepEat:
                {
                    NPC part = AbomanationChain.NextToGo(NPC.whoAmI);

                    if (part == null)
                    {
                        Step(StepGone);
                        break;
                    }

                    int place = (int)part.ai[3];
                    int whole = Abomanation.DiePiece[Math.Min(eaten, Abomanation.DiePiece.Length - 1)];
                    int move = whole / 4;
                    int hold = whole - move;

                    // What is left with this piece still on, and once it is gone: the
                    // loop is sized from it. The head steps forward one link, as the
                    // loop is now, to be against the next piece.
                    float with = AbomanationChain.Along(place);
                    float without = AbomanationChain.AlongBefore(place);
                    float link = with - without;
                    float step = loopSide * link / ((with + Abomanation.DieMouth) / MathHelper.TwoPi);

                    NPC.velocity = Vector2.Zero;

                    if (attackTimer <= hold)
                    {
                        // The mouth opens over the hold (see Mouth), and over the last
                        // few ticks the head goes forward into the piece.
                        remain = with;
                        headAngle = stepFrom;

                        float bite = MathHelper.Clamp((attackTimer - (hold - Abomanation.DieBiteTicks)) / (float)Abomanation.DieBiteTicks, 0f, 1f);
                        bite = MathHelper.SmoothStep(0f, 1f, bite);

                        Sit(bite);
                    }
                    else
                    {
                        // The mouth shuts: the piece comes apart on the first tick of
                        // it. Then forward one link to the next piece, mouth shut, the
                        // body closing up behind and the loop drawing in by the link
                        // that is gone, settling back from the bite on the way.
                        if (attackTimer == hold + 1)
                            Eat(part);

                        float t = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((attackTimer - hold) / (float)move, 0f, 1f));

                        remain = MathHelper.Lerp(with, without, t);
                        headAngle = stepFrom + step * t;
                        Sit(1f - t);

                        if (attackTimer >= hold + move)
                        {
                            remain = without;
                            headAngle = stepFrom + step;
                            stepFrom = headAngle;
                            Sit(0f);
                            eaten++;
                            attackTimer = 0;
                            NPC.netUpdate = true;
                        }
                    }

                    Drip();
                    break;
                }

                default:
                    NPC.velocity = Vector2.Zero;
                    Sit(0f);
                    Drip();

                    if (attackTimer >= Abomanation.DieLast)
                        Finish();
                    break;
            }
        }

        /// <summary>
        /// The head on the loop at <see cref="headAngle"/>, facing along it, this far
        /// into a bite: lunged forward along the loop and tipped a little.
        /// </summary>
        private void Sit(float bite)
        {
            OnLoop(headAngle, Radius, out Vector2 at, out float heading);

            NPC.Center = at + heading.ToRotationVector2() * (Abomanation.DieLunge * bite);
            NPC.rotation = heading + MathHelper.PiOver2 + loopSide * Abomanation.DieBiteNod * bite;
        }

        private void Step(int next)
        {
            dieStep = next;
            attackTimer = 0;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// The camera during the end: on the head while it stops and rises, then on the
        /// loop, eased, so it climbs with the head and settles as the loop closes rather
        /// than swinging round after the head.
        /// </summary>
        private void Watch()
        {
            Vector2 at = dieStep >= StepLoop ? loopCentre : NPC.Center;

            if (!looking)
            {
                look = NPC.Center;
                looking = true;
            }

            look = Vector2.Lerp(look, at, Abomanation.DieLook);
            Cinematic.Look(look);
        }

        /// <summary>
        /// The height the loop will sit at: in the first open air above the head that
        /// is tall enough for it, as low as it can be over that air's floor. If the head
        /// is already in air that tall, its own height, kept off the floor and the roof.
        /// </summary>
        private float LoopHeight()
        {
            int x = (int)(NPC.Center.X / 16f);
            int y = (int)(NPC.Center.Y / 16f);

            float least = Abomanation.DieLoopRadius + Abomanation.DieClearance;

            if (x < 10 || x > Main.maxTilesX - 10)
                return NPC.Center.Y - least;

            y = Math.Clamp(y, 10, Main.maxTilesY - 10);

            // Up from the head, run by run of open air, until one is tall enough.
            for (int tries = 0; tries < 40 && y > 10; tries++)
            {
                int floor;

                if (WorldGen.SolidTile(x, y))
                {
                    // In the ground: up out of it, and its top is the floor.
                    while (y > 10 && WorldGen.SolidTile(x, y))
                        y--;

                    floor = y + 1;
                }
                else
                {
                    // In the air: the floor is the first solid tile below.
                    floor = y;

                    while (floor < Main.maxTilesY - 10 && !WorldGen.SolidTile(x, floor))
                        floor++;
                }

                int roof = y;

                while (roof > 10 && !WorldGen.SolidTile(x, roof))
                    roof--;

                // roof is the first solid tile above the run, or the top of the map.
                int tall = floor - roof - 1;

                if (tall >= Abomanation.DieRoom || roof <= 10)
                {
                    float floorLine = floor * 16f;
                    float roofLine = (roof + 1) * 16f;
                    float lowest = floorLine - least;
                    float highest = roofLine + least;

                    if (highest > lowest)
                        return lowest;

                    return MathHelper.Clamp(NPC.Center.Y, highest, lowest);
                }

                y = roof;
            }

            return NPC.Center.Y - least;
        }

        /// <summary>
        /// Which side of the mouth the loop goes on: the one with fewer solid tiles
        /// inside where it would be. A tie goes toward the player, so it is between the
        /// two of you.
        /// </summary>
        private int LoopSide()
        {
            int right = Blocked(1);
            int left = Blocked(-1);

            if (right != left)
                return right < left ? 1 : -1;

            Player player = Main.player[NPC.target];

            return player.Center.X >= NPC.Center.X ? 1 : -1;
        }

        /// <summary>Solid tiles sampled inside the loop's square on that side of the head.</summary>
        private int Blocked(int side)
        {
            float span = Abomanation.DieLoopRadius * 2f;
            int count = 0;

            for (float dx = 8f; dx < span; dx += Abomanation.DieSideStep * 16f)
            {
                for (float dy = -Abomanation.DieLoopRadius; dy <= Abomanation.DieLoopRadius; dy += Abomanation.DieSideStep * 16f)
                {
                    int x = (int)((NPC.Center.X + side * dx) / 16f);
                    int y = (int)((NPC.Center.Y + dy) / 16f);

                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                    {
                        count++;
                        continue;
                    }

                    if (WorldGen.SolidTile(x, y))
                        count++;
                }
            }

            return count;
        }

        /// <summary>Blood from the mouth while it eats. A drop every few ticks, falling from just in front of the head.</summary>
        private void Drip()
        {
            if (Main.dedServ || attackTimer % Abomanation.DieDripEvery != 0)
                return;

            float heading = NPC.rotation - MathHelper.PiOver2;
            Vector2 at = NPC.Center + heading.ToRotationVector2() * (NPC.height * 0.45f)
                + heading.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-8f, 8f);
            Vector2 way = new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(0.4f, 1.6f));

            PADParticleHandler.SpawnParticle(new BloodDroplet(at, way,
                Main.rand.Next(30, 50), Main.rand.NextFloat(0.6f, 1.1f), Abomanation.Blood));
        }

        /// <summary>A part comes apart under the head and is gone.</summary>
        private void Eat(NPC part)
        {
            AbomanationChain.Burst(part, Abomanation.DieBurst, Abomanation.DieShake, Abomanation.DieShakeTicks);

            // A death, each one, never quite the same twice.
            SoundEngine.PlaySound(SoundID.NPCDeath1 with
            {
                Pitch = Main.rand.NextFloat(-Abomanation.DieRoarVary, Abomanation.DieRoarVary),
                PitchVariance = 0f,
                Volume = Main.rand.NextFloat(0.9f, 1.1f),
            }, part.Center);

            part.life = 0;
            part.active = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, part.whoAmI);
        }

        /// <summary>The head comes apart, and then it is allowed to die.</summary>
        private void Finish()
        {
            AbomanationChain.Burst(NPC, Abomanation.DieBurstHead, Abomanation.DieShakeHead, Abomanation.DieShakeHeadTicks);

            // The last of it: a roar, low and loud, and the ground with it.
            SoundEngine.PlaySound(SoundID.Roar with
            {
                Pitch = Abomanation.DieRoarPitch,
                PitchVariance = 0f,
                Volume = Abomanation.DieRoarVolume,
                MaxInstances = 1,
            }, NPC.Center);

            if (!Main.dedServ)
            {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, new Vector2(0f, -1f),
                    Abomanation.DieRoarShake, Abomanation.DieRoarShakeRate, Abomanation.DieRoarShakeTicks,
                    Abomanation.DieRoarShakeReach, "Abomanation"));
            }

            // Through the front door, so the loot, the downed flag and every other hook
            // run exactly as they would for an ordinary death.
            finishing = true;
            NPC.dontTakeDamage = false;
            NPC.life = 0;
            NPC.HitEffect(0, 10.0);
            NPC.checkDead();
        }

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

        /// <summary>See AbomanationBody: the directional blood is local, the dust is for everyone.</summary>
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            AbomanationChain.Slashed(NPC, player, item, hit);
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            AbomanationChain.Pierced(NPC, projectile, hit);
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

            Flinch(NPC.Center);
        }

        /// <summary>
        /// A bite on the player: the mouth shuts on it. Landed in a rush, it feeds:
        /// a twentieth of its life back, and in the second half no further than the
        /// line the second half starts at.
        /// </summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            chomp = Abomanation.ChompTicks;

            if (phase != 2)
                return;

            int cap = Enraged ? (int)(NPC.lifeMax * Abomanation.RagePoint) : NPC.lifeMax;
            int feed = Math.Min((int)(NPC.lifeMax * Abomanation.RushFeed), cap - NPC.life);

            if (feed <= 0)
                return;

            NPC.life += feed;
            NPC.HealEffect(feed, true);
            NPC.netUpdate = true;
        }

        /// <summary>
        /// Which face. Wide through a screech or a roar; shut for a moment after a bite;
        /// wide while it rushes up, while it is above ground arriving, and while it
        /// rises to die; a little open near the player or an animal it is after; neutral
        /// otherwise. Eating itself, each piece runs the four in order, neutral to shut,
        /// and shut is the chomp.
        /// </summary>
        private int Mouth()
        {
            if (Dying)
            {
                switch (dieStep)
                {
                    case StepRise:
                        return Abomanation.MouthWide;
                    case StepLoop:
                        return Abomanation.DieLoop - lapped < Abomanation.MouthNear ? Abomanation.MouthOpen : Abomanation.MouthNeutral;
                    case StepEat:
                    {
                        int whole = Abomanation.DiePiece[Math.Min(eaten, Abomanation.DiePiece.Length - 1)];
                        int move = whole / 4;
                        int hold = whole - move;

                        if (attackTimer > hold)
                            return Abomanation.MouthShut;

                        return Math.Min(Abomanation.MouthWide, (attackTimer - 1) * 3 / Math.Max(1, hold));
                    }
                    case StepGone:
                        return Abomanation.MouthShut;
                    default:
                        return Abomanation.MouthNeutral;
                }
            }

            // Screeching or roaring: wide open, whatever else is going on.
            if (roarFace > 0)
                return Abomanation.RoarMouth;

            if (chomp > 0)
                return Abomanation.MouthShut;

            if (phase == 2 || (Arriving && NPC.Center.Y < arriveBase))
                return Abomanation.MouthWide;

            // The newer moves. Wide and shut are the heave's and the snap's (roarFace and
            // chomp, above); these are what the mouth does between them.
            switch (phase)
            {
                case PukeSeek:
                    // Coming up with something in its throat.
                    return stage >= 2 ? Abomanation.MouthOpen : Abomanation.MouthNeutral;

                case PukeChoke:
                {
                    // Working at it: open on each catch, and worrying at it between.
                    foreach (int gag in Abomanation.ChokeGag)
                    {
                        if (attackTimer >= gag && attackTimer < gag + 7)
                            return Abomanation.MouthOpen;
                    }

                    return Abomanation.MouthNeutral;
                }

                case PukeSpew:
                case LayCrawl:
                case LayAct:
                    return Abomanation.MouthOpen;
            }

            float near = Abomanation.MouthNear + NPC.width * 0.5f;

            if (Hunting && NPC.Distance(Main.npc[prey].Center) < near)
                return Abomanation.MouthOpen;

            if (NPC.target >= 0 && NPC.target < Main.maxPlayers && NPC.Distance(Main.player[NPC.target].Center) < near)
                return Abomanation.MouthOpen;

            return Abomanation.MouthNeutral;
        }

        public override void FindFrame(int frameHeight)
        {
            int face = Mouth();

            NPC.frame.Y = face * frameHeight;

            // Wide to shut is a bite, and a bite throws sparks and blood. Read off the
            // frame itself, here, so whatever shuts the mouth, it shows. Not for the
            // bestiary's copy of it.
            if (!NPC.IsABestiaryIconDummy)
                Snapped(face);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;

            if (frame.Height <= 0)
                frame = texture.Frame(1, Abomanation.HeadFrames, 0, 0);

            // Shaken with the rest of it, and swollen as what it brings up reaches it.
            Vector2 at = NPC.Center - screenPos + Shiver(NPC);
            float scale = NPC.scale * Swell(-1);

            spriteBatch.Draw(texture, at, frame, NPC.GetAlpha(drawColor),
                NPC.rotation, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            // What is hot in it, added over its own flesh.
            Color glow = Glow(-1);

            if (glow != Color.Transparent && NPC.alpha <= 0 && !NPC.IsABestiaryIconDummy)
            {
                spriteBatch.Draw(texture, at, frame, glow,
                    NPC.rotation, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
