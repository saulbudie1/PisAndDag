using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Deep
{
    /// <summary>
    /// THE CREVICE EEL, by request ("Create a worm ai, Eel enemy, (no gravity, dont go thru
    /// tiles) living in these caverns, if player goes deep they jump out and slither thru
    /// the caves to you. research eels habits"); the Crevice Eel, a name of mine, then the
    /// Hole Dweller ("rename it: Hole Dwellers"), and the Crevice Eel again ("Rename it to
    /// Crevice Eel"), its code and files keeping that name throughout. Made bigger to look
    /// menacing ("make the eel kinda bigger too"), its pictures drawn again in 2x2 pixels,
    /// in the look of the gulper eel pointed at (CreviceEelHead, CreviceEelBody and their
    /// Glow pictures; the bestiary's eel is drawn from them, moving:
    /// <see cref="DrawBestiary"/>). What is known of morays and congers (the notes for
    /// section 16.92 have the sources): they live in holes and crevices of the rock
    /// (congers in wrecks too, and down to 500 metres and deeper), resting there by day
    /// with the head out; the mouth opens and shuts the while, which is how they breathe,
    /// pushing water out through small gill openings; they see poorly and hunt by smell, at
    /// night; their teeth are jagged, and a second pair of jaws in the throat pulls prey
    /// down. Mine, from that: it strikes from its crevice at what comes near and goes back
    /// into it tail first; it smells blood from farther; its bite leaves the bitten
    /// bleeding; and in the caves, where it is always night, it hunts whenever a player is
    /// there.
    ///
    /// <b>The body.</b> A head (this) and <see cref="Segments"/> pieces after it, the last a
    /// tail (<see cref="CreviceEelBody"/>), each a creature of its own for being hit (the head's
    /// life is theirs, NPC.realLife) but put each tick where the head has been: the head keeps
    /// the way it came (its trail, a point every <see cref="TrailStep"/> pixels), and each piece
    /// lies along that way behind it, the first <see cref="NeckGap"/> pixels back, then one every
    /// <see cref="Spacing"/>. No gravity; the head does not go through rock (the game's own
    /// collision), and the body only ever lies where the head has been, so it does not either.
    /// The head draws the whole eel, tail first, each piece with its lights. The head's middle
    /// (where it is, its hitbox's middle) is at its jaws, not its picture's middle
    /// (<see cref="HeadPivot"/>), so that what bites is the mouth; its neck reaches back to the
    /// first piece. Its hitboxes fit its pictures since they grew, by request ("remember to
    /// resize his ingame hitbox to make it smooth"): the head <see cref="HitSize"/> square, as
    /// thick as the head is at its jaws, each piece of body CreviceEelBody.HitSize (the tail
    /// CreviceEelBody.TailSize), and each overlapping the next, the head's the first piece's
    /// (<see cref="NeckGap"/>), so there is nowhere along it a blow or a bite passes through. The
    /// rock keeps out only the head's core (<see cref="CoreSize"/> square about its middle, what
    /// its whole hitbox was before), so that it still takes the ways it always took: its burrows
    /// two cells across, and the caves a head two cells across can swim (<see cref="CollideCore"/>).
    ///
    /// <b>What it does</b>, made more of a hunter since by request ("make their movement more
    /// creepy and predatorial. research eel hunting mechanic."), from what is known of morays
    /// (the notes for section 16.96 have the sources): an ambush hunter that rests in a crevice
    /// with only its head out, the mouth always working (it breathes so); that strikes in a
    /// fraction of a second, its second jaws in its throat seizing what it bites and pulling it
    /// back; that hunts by smell more than by sight, at night, through the reef and into its
    /// holes; that ties itself in a knot to tear and to hold what it has; and that will hunt in
    /// company. Mine, from that:
    ///   - LURKS in its crevice (CreviceEelSpawns puts it there), the head at the mouth, the
    ///     body in the rock behind it, the mouth working; a player within
    ///     <see cref="WatchTiles"/> tiles and in its sight, it slides its head out of the crevice
    ///     and turns it to follow them, breathing faster (<see cref="Peek"/>); nearer than
    ///     <see cref="ReadyTiles"/>, it draws back into the crevice, ready;
    ///   - COILS, drawing its head back a moment, and STRIKES at a player who comes within
    ///     <see cref="StrikeTiles"/> tiles of its head and in its sight: out of the crevice
    ///     straight at them, fast, up to <see cref="StrikeReach"/> tiles, and back into it tail
    ///     first the way it came (RECOILS), dragging whom it bit with it (CreviceEelSeized);
    ///   - HUNTS a player who has gone deep (in the Vast Ocean from the 8th section down, under
    ///     rock: four solid rows or more within forty over them) and is within
    ///     <see cref="HuntTiles"/> tiles of it by the way through the caves (<see cref="SmellTiles"/>
    ///     if they are bleeding): out of the crevice and through the caves to them by the
    ///     shortest way through the water a head its size can take (a chart of steps to them from
    ///     every cell within <see cref="ChartReach"/> tiles, worked out again every half second),
    ///     keeping to the rock where the way allows; stalking, slow, the body in long waves,
    ///     stopping now and then to taste the water, its head turning; seeing them near, it
    ///     creeps; within <see cref="LungeTiles"/> tiles, it COILS and LUNGES, its mouth wide;
    ///     having bitten, it shakes them, and RECOVERS backing off the way it came, tail first,
    ///     to come again;
    ///   - in company: one comes in at a time while the others hold off, circling at
    ///     <see cref="HoldOffTiles"/> tiles; and one that strikes or hunts wakes the others
    ///     lurking within <see cref="WakeTiles"/> tiles of it, one after another (a den,
    ///     CreviceEelSpawns);
    ///   - hit, it strikes at whoever hit it if they are near, or hunts them;
    ///   - gives up a hunt when they are out of the caves for four seconds, or farther than
    ///     <see cref="LoseTiles"/> tiles by the way through them for three, or after forty
    ///     seconds farther than thirty: goes HOME to its crevice, into it, and is gone; one with
    ///     no crevice to go to LEAVES, away from everyone, and is gone out of their sight.
    /// Its bite leaves the bitten bleeding. Its sounds are of the deep (Content/Sounds/Deep/Eel),
    /// heard through the silence. It is of the deep, and the water's weight is nothing to it
    /// (<see cref="IDeepNative"/>). Nobody within 160 tiles for five seconds: gone.
    ///
    /// The server decides what it does (or the one player's own game); every machine moves it
    /// and keeps its own trail.
    /// </summary>
    public class CreviceEelHead : ModNPC, IDeepNative
    {
        // ---- the numbers ----------------------------------------------------------------------

        /// <summary>
        /// Pieces of body after the head, the last a tail; their spacing along it (each piece's
        /// picture 32 long, so its front 12 lie under the next), and the first one's from the
        /// head's middle (close enough that its hitbox meets the head's). Behind the head's middle
        /// 210 pixels in all, to the tail's tip: within the length of the shortest crevice the floor
        /// cuts behind its mouth's middle (14 cells, VastOceanFloor.LairShortest: 13 from the
        /// mouth's middle to the back's, and half a cell).
        /// </summary>
        public const int Segments = 9;
        public const float Spacing = 20f;
        public const float NeckGap = 34f;

        /// <summary>
        /// Pieces of body past <see cref="Segments"/> it may have, none to two, rolled as it is put
        /// in its crevice, by request ("their length is randomized, base now to base + 2
        /// segment"; CreviceEelSpawns): kept in NPC.ai[2], so every machine knows it.
        /// </summary>
        public const int MostExtra = 2;

        public int Extra => Math.Clamp((int)NPC.ai[2], 0, MostExtra);

        /// <summary>Its pieces of body after the head, the last a tail.</summary>
        public int Length => Segments + Extra;

        /// <summary>How far behind the head's middle the tail's tip lies, with this many pieces of body (the tail's picture 32 long about its middle).</summary>
        public static float BehindHead(int pieces) => NeckGap + (pieces - 1) * Spacing + 16f;

        /// <summary>
        /// Whether one with this many pieces past the least fits a crevice this long, lurking: its
        /// tail's tip no further behind its head than the crevice's back and the two rows of rock
        /// behind it (as the least of them lie in the shortest crevices).
        /// </summary>
        public static bool Fits(int lairLength, int extra) => (lairLength - 1) * 16f + 40f >= BehindHead(Segments + extra);

        /// <summary>
        /// The head's hitbox, square about its jaws, as thick as the head is there (its picture, 36
        /// to 42 high about them); and its core, what the rock keeps out (what its hitbox was before
        /// it grew: a head of it fits a burrow two cells across with room to spare).
        /// </summary>
        public const int HitSize = 40;
        public const int CoreSize = 24;

        /// <summary>Where in the head's picture (one frame: 72 by 52) the head's middle is: at its jaws, 48 from its neck.</summary>
        public static readonly Vector2 HeadPivot = new Vector2(48f, 26f);

        /// <summary>
        /// Its life, by request ("base Crevice eel is 9500, each segment is +1200"; 7000 and 750
        /// before): 9500 at its least length, 1200 more for each piece past it (as the game raises
        /// a creature's life for the world's difficulty, so these: <see cref="Size"/>), one life for
        /// the whole eel, its pieces' the head's (CreviceEelBody), shown once, over the head.
        /// Provisional, for the deep: armour, the head's bite and the body's.
        /// </summary>
        public const int Life = 9500;
        public const int LifeEachPiece = 1200;
        public const int Armour = 16;
        public const int Bite = 85;
        public const int BodyBite = 55;

        /// <summary>Tiles: strikes at a player this near and seen; goes this far out of its crevice at most striking.</summary>
        public const float StrikeTiles = 7f;
        public const float StrikeReach = 8f;

        /// <summary>
        /// Lurking: tiles within which it watches a player it sees, its head out of the crevice
        /// (its jaws this many pixels past the mouth's middle, turned toward them by as much as
        /// <see cref="LookMost"/>); and nearer than which it draws back into the crevice, ready,
        /// its jaws this many pixels inside.
        /// </summary>
        public const float WatchTiles = 22f;
        public const float ReadyTiles = 11f;
        public const float PeekOut = 26f;
        public const float DrawBack = 12f;
        public const float LookMost = 0.85f;

        /// <summary>Tiles, by the way through the caves: hunts a player gone deep this near; this near if they bleed; loses them farther.</summary>
        public const int HuntTiles = 60;
        public const int SmellTiles = 90;
        public const int LoseTiles = 100;

        /// <summary>Tiles: lunges at a player this near, and seen; creeps, seeing them, this near.</summary>
        public const float LungeTiles = 10f;
        public const float CloseTiles = 18f;

        /// <summary>Tiles: waiting its turn, it holds off this far from the prey; it wakes others lurking this near.</summary>
        public const float HoldOffTiles = 13f;
        public const float WakeTiles = 48f;

        /// <summary>Pixels a tick: striking; hunting far off (more than forty steps by the way), stalking, creeping; lunging; going back into its crevice; backing off; circling; going home.</summary>
        public const float StrikeSpeed = 12.5f;
        public const float CruiseSpeed = 4.8f;
        public const float StalkSpeed = 3.4f;
        public const float CreepSpeed = 2.2f;
        public const float LungeSpeed = 13f;
        public const float BackSpeed = 4.5f;
        public const float BackOffSpeed = 2.8f;
        public const float CircleSpeed = 2.4f;
        public const float HomeSpeed = 4f;

        /// <summary>Pixels a tick it draws back along its own body, coiling to lunge.</summary>
        public const float CoilBackSpeed = 1.4f;

        /// <summary>
        /// Ticks: coiling before a strike, and before a lunge; striking; lunging; shaking what it
        /// has bitten; backing off after; and stalking, a stop of <see cref="PauseTicks"/> in
        /// every <see cref="StalkBeat"/>.
        /// </summary>
        public const int CoilTicks = 8;
        public const int LungeCoilTicks = 12;
        public const int StrikeTicks = 14;
        public const int LungeTicks = 18;
        public const int ShakeTicks = 16;
        public const int BackOffTicks = 30;
        public const int StalkBeat = 150;
        public const int PauseTicks = 40;

        /// <summary>Pixels: the body's waves, at the tail, swimming; their length along it.</summary>
        public const float WaveMost = 5f;
        public const float WaveLength = 140f;

        /// <summary>Of the deep: never pressed.</summary>
        public int SafeTo => VastOceanWeight.Deepest;

        public static readonly SoundStyle Snap = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Snap") { MaxInstances = 3, PitchVariance = 0.15f, Volume = 0.8f };
        public static readonly SoundStyle Rush = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Rush") { MaxInstances = 2, PitchVariance = 0.12f, Volume = 0.7f };
        public static readonly SoundStyle Struck = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Hit") { MaxInstances = 4, PitchVariance = 0.2f, Volume = 0.6f };
        public static readonly SoundStyle Dying = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Death") { MaxInstances = 1, Volume = 0.9f };

        private enum Mode : byte
        {
            Lurk,
            Coil,
            Strike,
            Recoil,
            Hunt,
            Lunge,
            Recover,
            Home,
            Leave,
        }

        private Mode mode;
        private int timer;
        private int prey = -1;
        private EelLair lair;
        private bool lairKnown;

        /// <summary>What it coils for: a strike (from its crevice) or a lunge (hunting).</summary>
        private Mode coilInto = Mode.Strike;

        /// <summary>Where its head was as it began to coil.</summary>
        private Vector2 coilFrom;

        /// <summary>Its lunge ended on the prey: it shakes them before it backs off.</summary>
        private bool bit;

        /// <summary>Which way it circles, holding off (1 or -1).</summary>
        private sbyte side = 1;

        /// <summary>Lurking, it sees a player near and watches them (its breath quicker).</summary>
        private bool watching;

        /// <summary>Woken by another of its kind near it striking or hunting: ticks till it comes out too, and after whom (where it is decided).</summary>
        private int waking;
        private int wakePrey = -1;

        /// <summary>A player worth hunting noticed, lurking: ticks it waits yet, and who (where it is decided); 0, none.</summary>
        private int patience;
        private int patiencePrey = -1;

        /// <summary>The body's waves: how big now (pixels at the tail), and how far along they have run.</summary>
        private float waveAmp, wavePhase;

        /// <summary>The trail's length when the head is at its crevice's mouth: what it recoils to.</summary>
        private float lurkLength;

        /// <summary>Ticks: with nobody near; with the prey lost or out of the caves; since the hunt began farther than thirty.</summary>
        private int alone, lost, outside;

        /// <summary>Hit since last it chose what to do; and gone into its crevice for good.</summary>
        private bool provoked, inside;

        private bool bodied;

        /// <summary>The body's pieces, matched to this head by this (NPC.ai[3]), so that a piece never takes up with a head that has since had its slot.</summary>
        public int Id => (int)NPC.ai[3];

        public bool Lives(EelLair other) => lairKnown && lair.Same(other);

        /// <summary>
        /// What it has seized it lets go (CreviceEelSeized): its head two and a half tiles from its
        /// crevice's mouth, coming back (what it bit pulled up to the hole and no further, not
        /// pressed against the rock about it), lurking, or gone into it.
        /// </summary>
        public bool LetsGo => mode == Mode.Lurk || mode == Mode.Recoil && lairKnown && Vector2.Distance(NPC.Center, lair.Mouth) < 40f || mode == Mode.Home && inside;

        // ---- set up -----------------------------------------------------------------------------

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 3;

            // In the bestiary the eel is drawn from its own pictures, swimming (DrawBestiary): about
            // its middle, which the bestiary puts at the middle of its box; 180 long, so 72 across
            // the list's box, and 180 of the portrait's 193.
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers
            {
                Scale = 0.4f,
                PortraitScale = 1f,
            });
        }

        public override void Load()
        {
            On_NPC.GetTileCollisionParameters += CollideCore;
        }

        /// <summary>
        /// For a Crevice Eel's head, the box the game keeps out of the rock (NPC.UpdateCollision,
        /// and its slopes) is its core, <see cref="CoreSize"/> square about its middle, not its
        /// hitbox; every other creature's is as the game has it.
        /// </summary>
        private static void CollideCore(On_NPC.orig_GetTileCollisionParameters orig, NPC self, out Vector2 cPosition, out int cWidth, out int cHeight)
        {
            orig(self, out cPosition, out cWidth, out cHeight);

            if (self.ModNPC is CreviceEelHead)
            {
                cPosition = self.Center - new Vector2(CoreSize / 2f);
                cWidth = CoreSize;
                cHeight = CoreSize;
            }
        }

        public override void SetDefaults()
        {
            NPC.width = HitSize;
            NPC.height = HitSize;
            NPC.lifeMax = Life;
            NPC.damage = Bite;
            NPC.defense = Armour;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.waterMovementSpeed = 1f;
            NPC.netAlways = true;
            NPC.npcSlots = 1f;
            NPC.value = Item.buyPrice(silver: 50);
            NPC.HitSound = Struck;
            NPC.DeathSound = Dying;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Ocean,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,
                new FlavorTextBestiaryInfoElement("Mods.PisAndDag.NPCs.CreviceEelHead.Bestiary"),
            });
        }

        /// <summary>Put in a crevice, lurking: the head at its mouth, the body along it into the rock (CreviceEelSpawns, on the machine that decides).</summary>
        internal void Settle(EelLair at)
        {
            lair = at;
            lairKnown = true;
            mode = Mode.Lurk;
            timer = -30;
            Lay();
            NPC.Center = lair.Mouth;
            NPC.velocity = Vector2.Zero;
            NPC.rotation = (float)Math.Atan2(lair.Dy, lair.Dx);
            NPC.netUpdate = true;
        }

        /// <summary>The trail laid along the crevice from its back to its mouth, as if the head had come that way.</summary>
        private void Lay()
        {
            ClearTrail();

            for (float k = 0f; k <= lair.Length - 1; k += TrailStep / 16f)
                Push(lair.Point(k));

            Push(lair.Mouth);
            lurkLength = trailLength;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)mode);
            writer.Write(timer);
            writer.Write((short)prey);
            writer.Write((byte)coilInto);
            writer.Write(bit);
            writer.Write(side);
            writer.Write(lairKnown);

            if (lairKnown)
            {
                writer.Write((short)lair.X);
                writer.Write((short)lair.Y);
                writer.Write((sbyte)lair.Dx);
                writer.Write((sbyte)lair.Dy);
                writer.Write((byte)lair.Length);
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            mode = (Mode)reader.ReadByte();
            timer = reader.ReadInt32();
            prey = reader.ReadInt16();
            coilInto = (Mode)reader.ReadByte();
            bit = reader.ReadBoolean();
            side = reader.ReadSByte();
            bool known = reader.ReadBoolean();

            if (known)
            {
                var got = new EelLair(reader.ReadInt16(), reader.ReadInt16(), reader.ReadSByte(), reader.ReadSByte(), reader.ReadByte());
                bool fresh = !lairKnown || !lair.Same(got) || trailCount == 0;

                if (fresh)
                    way.Clear();

                lair = got;
                lairKnown = true;

                // A head first heard of lurking: its body laid in its crevice here too.
                if (fresh && mode == Mode.Lurk)
                    Lay();
            }
        }

        // ---- the trail --------------------------------------------------------------------------

        /// <summary>Points kept of where the head has been, this far apart: 400 tiles of it.</summary>
        private const int TrailCap = 1600;
        private const float TrailStep = 4f;

        private readonly Vector2[] trail = new Vector2[TrailCap];
        private int trailStart, trailCount;
        private float trailLength;

        /// <summary>The i-th point of the trail, the oldest first.</summary>
        private Vector2 TrailAt(int i) => trail[(trailStart + i) % TrailCap];

        private void ClearTrail()
        {
            trailStart = 0;
            trailCount = 0;
            trailLength = 0f;
        }

        private void Push(Vector2 p)
        {
            if (trailCount > 0)
                trailLength += Vector2.Distance(TrailAt(trailCount - 1), p);

            if (trailCount == TrailCap)
            {
                trailLength -= Vector2.Distance(TrailAt(0), TrailAt(1));
                trailStart = (trailStart + 1) % TrailCap;
                trailCount--;
            }

            trail[(trailStart + trailCount) % TrailCap] = p;
            trailCount++;
        }

        /// <summary>Where the head is now, onto the trail: a point every <see cref="TrailStep"/> pixels along the way from the last.</summary>
        private void Record(Vector2 at)
        {
            if (trailCount == 0)
            {
                Push(at);
                return;
            }

            Vector2 last = TrailAt(trailCount - 1);
            float d = Vector2.Distance(last, at);

            if (d < TrailStep)
                return;

            int steps = (int)(d / TrailStep);

            for (int k = 1; k <= steps; k++)
                Push(Vector2.Lerp(last, at, k * TrailStep / d));
        }

        /// <summary>Back along the trail this far, the newest of it taken off: the way an eel goes back into its crevice, tail first.</summary>
        private void Rewind(float distance)
        {
            while (distance > 0f && trailCount > 1)
            {
                Vector2 a = TrailAt(trailCount - 1), b = TrailAt(trailCount - 2);
                float d = Vector2.Distance(a, b);

                if (d > distance)
                {
                    trail[(trailStart + trailCount - 1) % TrailCap] = Vector2.Lerp(a, b, distance / d);
                    trailLength -= distance;
                    return;
                }

                trailCount--;
                trailLength -= d;
                distance -= d;
            }
        }

        // ---- the body along it ------------------------------------------------------------------

        private readonly Vector2[] placed = new Vector2[Segments + MostExtra + 1];
        private readonly float[] turned = new float[Segments + MostExtra + 1];

        /// <summary>
        /// The way the head faces as it is drawn: from where its neck lies on its trail (as far
        /// back as the head is long from its jaws, <see cref="HeadPivot"/>) to its jaws; so it turns
        /// about its neck, by request ("rotate it by the neck"), the jaws going ahead and the neck
        /// staying on the way the body goes, as each piece of the body lies.
        /// </summary>
        private float headFacing;
        private uint placedOn = uint.MaxValue;
        private Vector2 placedFrom;

        /// <summary>Where a piece of the body lies now (1 the first after the head, <see cref="Length"/> the tail), and which way it faces (toward the head).</summary>
        public Vector2 Piece(int index, out float facing)
        {
            Place();
            index = Math.Clamp(index, 1, Length);
            facing = turned[index];
            return placed[index];
        }

        /// <summary>
        /// Every piece along the way from the head's middle back through its trail; where the
        /// trail gives out, on straight the way its oldest part was going (or straight behind
        /// the head, with none).
        /// </summary>
        private void Place()
        {
            if (placedOn == Main.GameUpdateCount && placedFrom == NPC.Center)
                return;

            placedOn = Main.GameUpdateCount;
            placedFrom = NPC.Center;

            Vector2 behind = -NPC.rotation.ToRotationVector2();
            Vector2 a = NPC.Center;
            int i = trailCount - 1;
            float walked = 0f;

            for (int piece = 1; piece <= Length; piece++)
            {
                float want = NeckGap + (piece - 1) * Spacing;

                while (true)
                {
                    Vector2 b;

                    if (i >= 0)
                    {
                        b = TrailAt(i);
                    }
                    else
                    {
                        if (trailCount >= 2)
                            behind = (TrailAt(0) - TrailAt(1)).SafeNormalize(behind);

                        b = a + behind * 4000f;
                    }

                    float d = Vector2.Distance(a, b);

                    if (d < 0.01f)
                    {
                        i--;
                        continue;
                    }

                    if (walked + d >= want)
                    {
                        placed[piece] = Vector2.Lerp(a, b, (want - walked) / d);
                        turned[piece] = (a - b).ToRotation();
                        break;
                    }

                    walked += d;
                    a = b;
                    i--;
                }
            }

            // Swimming, waves run down the body from the neck, growing to the tail, as an eel's
            // do (each piece put out to its side by the wave there, and turned with it).
            if (waveAmp > 0.05f)
            {
                float whole = BehindHead(Length);
                float k = MathHelper.TwoPi / WaveLength;

                float Off(float s) => waveAmp * Math.Clamp(s / whole, 0f, 1f) * (float)Math.Sin(wavePhase - k * s);

                float before = 0f;

                // From the second piece: the first is held at the neck, where its hitbox and the
                // head's only just meet (NeckGap).
                for (int piece = 2; piece <= Length; piece++)
                {
                    float s = NeckGap + (piece - 1) * Spacing;
                    float facing = turned[piece];
                    Vector2 left = new Vector2(-(float)Math.Sin(facing), (float)Math.Cos(facing));

                    // No more than three pixels out from the piece before, so that their hitboxes
                    // still meet (the tail's, the smallest, by four).
                    float off = MathHelper.Clamp(Off(s), before - 3f, before + 3f);

                    // None where the rock is close beside it that way (in a crevice, a narrow way).
                    Vector2 probe = placed[piece] + left * Math.Sign(off) * (16f + Math.Abs(off));

                    if (EelLairs.Rock((int)(probe.X / 16f), (int)(probe.Y / 16f)))
                    {
                        before = 0f;
                        continue;
                    }

                    placed[piece] += left * off;
                    turned[piece] = facing + (float)Math.Atan2(Off(s - 10f) - Off(s + 10f), 20f);
                    before = off;
                }
            }

            Vector2 neck = Back(HeadPivot.X);
            headFacing = Vector2.DistanceSquared(NPC.Center, neck) > 0.01f ? (NPC.Center - neck).ToRotation() : NPC.rotation;
        }

        /// <summary>The point this far back from the head's middle along its trail (on straight past its oldest part, as the pieces are laid).</summary>
        private Vector2 Back(float want)
        {
            Vector2 behind = -NPC.rotation.ToRotationVector2();
            Vector2 a = NPC.Center;
            int i = trailCount - 1;
            float walked = 0f;

            while (true)
            {
                Vector2 b;

                if (i >= 0)
                {
                    b = TrailAt(i);
                }
                else
                {
                    if (trailCount >= 2)
                        behind = (TrailAt(0) - TrailAt(1)).SafeNormalize(behind);

                    b = a + behind * 4000f;
                }

                float d = Vector2.Distance(a, b);

                if (d < 0.01f)
                {
                    i--;
                    continue;
                }

                if (walked + d >= want)
                    return Vector2.Lerp(a, b, (want - walked) / d);

                walked += d;
                a = b;
                i--;
            }
        }

        // ---- the chart of the way through the caves ---------------------------------------------

        /// <summary>Tiles about its goal a chart reaches.</summary>
        public const int ChartReach = 96;
        private const int ChartSide = ChartReach * 2 + 1;

        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private short[] chart;
        private static int[] chartQueue;

        /// <summary>Room for the head at each cell of the chart being made (0 not looked at yet, 1 room, 2 none): each looked at once.</summary>
        private static byte[] chartRoom;
        private Point chartGoal = new Point(int.MinValue, int.MinValue);
        private int chartAge;

        /// <summary>Room for the head here: the two by two tiles from this one, open.</summary>
        private static bool Room(int x, int y) => !EelLairs.Rock(x, y) && !EelLairs.Rock(x + 1, y) && !EelLairs.Rock(x, y + 1) && !EelLairs.Rock(x + 1, y + 1);

        /// <summary>The cell (two by two tiles from it) a point is in the middle of, near enough.</summary>
        internal static Point CellOf(Vector2 world) => new Point((int)Math.Round(world.X / 16f) - 1, (int)Math.Round(world.Y / 16f) - 1);

        private static Vector2 MiddleOf(int x, int y) => new Vector2((x + 1) * 16f, (y + 1) * 16f);

        /// <summary>Steps from this cell to the chart's goal; -1 not reached (or off the chart), -2 no room.</summary>
        private short Steps(int x, int y)
        {
            int i = x - chartGoal.X + ChartReach, j = y - chartGoal.Y + ChartReach;

            if (chart == null || i < 0 || j < 0 || i >= ChartSide || j >= ChartSide)
                return -1;

            return chart[j * ChartSide + i];
        }

        /// <summary>
        /// A chart of the way to this place through the water, from everywhere within
        /// <see cref="ChartReach"/> tiles of it: the steps from each cell with room for the head
        /// (two by two tiles open), a wave out from the place, the eight ways, never cutting a
        /// corner of rock. The place itself may have no room (a player against the rock): the
        /// nearest cell about it that has, within three.
        /// </summary>
        private void Chart(Vector2 goal)
        {
            chart ??= new short[ChartSide * ChartSide];
            chartQueue ??= new int[ChartSide * ChartSide];
            chartRoom ??= new byte[ChartSide * ChartSide];
            Array.Fill(chart, (short)-1);
            Array.Clear(chartRoom);

            Point g = CellOf(goal);
            chartGoal = g;
            chartAge = 0;

            int start = -1;

            for (int r = 0; r <= 3 && start < 0; r++)
            {
                for (int dy = -r; dy <= r && start < 0; dy++)
                {
                    for (int dx = -r; dx <= r && start < 0; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && Room(g.X + dx, g.Y + dy))
                            start = (dy + ChartReach) * ChartSide + dx + ChartReach;
                    }
                }
            }

            if (start < 0)
                return;

            int head = 0, tail = 0;
            chart[start] = 0;
            chartQueue[tail++] = start;

            while (head < tail)
            {
                int c = chartQueue[head++];
                int cx = c % ChartSide, cy = c / ChartSide;
                short next = (short)(chart[c] + 1);

                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];

                    if (nx < 0 || ny < 0 || nx >= ChartSide || ny >= ChartSide)
                        continue;

                    int n = ny * ChartSide + nx;

                    if (chart[n] != -1)
                        continue;

                    if (!Charted(nx, ny))
                    {
                        chart[n] = -2;
                        continue;
                    }

                    if (k >= 4 && (!Charted(nx - DX[k], ny) || !Charted(nx, ny - DY[k])))
                        continue;

                    chart[n] = next;
                    chartQueue[tail++] = n;
                }
            }
        }

        /// <summary>Room for the head at this cell of the chart being made (counted from its corner; off it, looked at as any cell).</summary>
        private bool Charted(int i, int j)
        {
            int wx = chartGoal.X - ChartReach + i, wy = chartGoal.Y - ChartReach + j;

            if (i < 0 || j < 0 || i >= ChartSide || j >= ChartSide)
                return Room(wx, wy);

            int c = j * ChartSide + i;

            if (chartRoom[c] == 0)
                chartRoom[c] = (byte)(Room(wx, wy) ? 1 : 2);

            return chartRoom[c] == 1;
        }

        /// <summary>
        /// How near the rock the cell (two by two tiles from it) keeps: none against it (rock in
        /// the twelve tiles round it, a corner the head would catch on); else, of the twenty tiles
        /// round those, how many are rock, up to three (a wall a tile off).
        /// </summary>
        private static int Hug(int x, int y)
        {
            for (int dy = -1; dy <= 2; dy++)
            {
                for (int dx = -1; dx <= 2; dx++)
                {
                    if ((dx == 0 || dx == 1) && (dy == 0 || dy == 1))
                        continue;

                    if (EelLairs.Rock(x + dx, y + dy))
                        return 0;
                }
            }

            int rock = 0;

            for (int dy = -2; dy <= 3; dy++)
            {
                for (int dx = -2; dx <= 3; dx++)
                {
                    if (dx >= -1 && dx <= 2 && dy >= -1 && dy <= 2)
                        continue;

                    if (EelLairs.Rock(x + dx, y + dy) && ++rock >= 3)
                        return 3;
                }
            }

            return rock;
        }

        /// <summary>
        /// The way to go from where the head is, by the chart: toward the cell three steps on down
        /// it, of the cells as near the goal the one a tile off the rock if there is one
        /// (<see cref="Hug"/>: so that it keeps to the walls where the way allows, as a moray goes
        /// through the reef, and does not catch on them); the head's steps to the goal. False if
        /// the head is not on the chart (nor within two cells of it).
        /// </summary>
        private bool Way(out Vector2 toward, out int steps) => Way(NPC.Center, out toward, out steps);

        /// <summary>The same from this place.</summary>
        private bool Way(Vector2 from, out Vector2 toward, out int steps)
        {
            Point at = CellOf(from);
            steps = Steps(at.X, at.Y);
            toward = from;

            if (steps < 0)
            {
                int best = int.MaxValue;
                Point near = at;

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        short s = Steps(at.X + dx, at.Y + dy);

                        if (s >= 0 && s + Math.Abs(dx) + Math.Abs(dy) < best)
                        {
                            best = s + Math.Abs(dx) + Math.Abs(dy);
                            near = new Point(at.X + dx, at.Y + dy);
                        }
                    }
                }

                if (best == int.MaxValue)
                    return false;

                at = near;
                steps = Steps(at.X, at.Y);
            }

            for (int hop = 0; hop < 3; hop++)
            {
                Point next = at;
                short here = Steps(at.X, at.Y), least = here;
                int hugged = -1;

                for (int k = 0; k < 8; k++)
                {
                    int nx = at.X + DX[k], ny = at.Y + DY[k];
                    short s = Steps(nx, ny);

                    if (s < 0 || s >= here)
                        continue;

                    int hug = Hug(nx, ny);

                    if (s < least || s == least && hug > hugged)
                    {
                        least = s;
                        hugged = hug;
                        next = new Point(nx, ny);
                    }
                }

                if (next == at)
                    break;

                at = next;
            }

            toward = MiddleOf(at.X, at.Y);
            return true;
        }

        // ---- what it does -----------------------------------------------------------------------

        /// <summary>A player gone deep: in the Vast Ocean from the 8th section down, under rock (four solid rows or more within forty over them).</summary>
        internal static bool GoneDeep(Player player)
        {
            if (SubworldKeeper.Current is not VastOcean || VastOcean.RowOf(player) < VastOcean.SectionTop(8))
                return false;

            int x = (int)(player.Center.X / 16f);
            int y = (int)(player.position.Y / 16f) - 1;
            int run = 0;

            for (int k = 0; k < 40; k++)
            {
                if (EelLairs.Rock(x, y - k))
                {
                    if (++run >= 4)
                        return true;
                }
                else
                {
                    run = 0;
                }
            }

            return false;
        }

        private static bool Fair(int index) => index >= 0 && index < Main.maxPlayers && Main.player[index].active && !Main.player[index].dead && !Main.player[index].ghost;

        /// <summary>
        /// It sees them: no rock on the straight line from its head's middle to theirs, looked at
        /// every four pixels. (The game's own test, Collision.CanHitLine, looks at the rows about
        /// the line as well, and so finds the walls of a crevice two cells across in the way of
        /// any line out of it.)
        /// </summary>
        private bool Sees(Player player) => Sees(NPC.Center, player);

        /// <summary>No rock on the straight line from this point to their middle, looked at every four pixels.</summary>
        private static bool Sees(Vector2 a, Player player)
        {
            Vector2 b = player.Center;
            int steps = (int)(Vector2.Distance(a, b) / 4f);

            for (int s = 1; s < steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)s / steps);

                if (EelLairs.Rock((int)(p.X / 16f), (int)(p.Y / 16f)))
                    return false;
            }

            return true;
        }

        private bool Deciding => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>What it does next, where that is decided (elsewhere it is heard of); coming out after someone, it wakes the others near (<see cref="Wake"/>).</summary>
        private void Become(Mode next, int target = -2)
        {
            if (!Deciding)
                return;

            mode = next;
            timer = 0;

            if (target != -2)
                prey = target;

            NPC.netUpdate = true;

            if (next is Mode.Coil or Mode.Strike or Mode.Hunt)
                Wake();
        }

        /// <summary>Coils, then strikes (from its crevice) or lunges (hunting).</summary>
        private void CoilFor(Mode then, int target = -2)
        {
            if (!Deciding)
                return;

            coilInto = then;
            Become(Mode.Coil, target);
        }

        /// <summary>
        /// The others of its kind lurking within <see cref="WakeTiles"/> tiles of it, not yet
        /// woken, wake: each comes out after the same player, if it is worth hunting, one after
        /// another, a half to nearly two seconds on (where it is decided).
        /// </summary>
        private void Wake()
        {
            if (!Fair(prey))
                return;

            foreach (NPC other in Main.ActiveNPCs)
            {
                if (other.whoAmI == NPC.whoAmI || other.ModNPC is not CreviceEelHead eel || eel.mode != Mode.Lurk || eel.waking > 0)
                    continue;

                if (other.DistanceSQ(NPC.Center) > WakeTiles * WakeTiles * 256f)
                    continue;

                eel.waking = Main.rand.Next(25, 111);
                eel.wakePrey = prey;
            }
        }

        public override void AI()
        {
            // The first tick, where it is decided: who it is, and its body.
            if (Deciding && NPC.ai[3] == 0f)
            {
                NPC.ai[3] = Main.rand.Next(1, 1 << 24);
                side = (sbyte)(Main.rand.Next(2) == 0 ? 1 : -1);
                NPC.netUpdate = true;
            }

            if (Deciding && !bodied && NPC.ai[3] != 0f)
            {
                bodied = true;
                int bodyType = ModContent.NPCType<CreviceEelBody>();

                for (int i = 1; i <= Length; i++)
                {
                    int s = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, bodyType, NPC.whoAmI, NPC.whoAmI, i, NPC.ai[3]);

                    if (s < Main.maxNPCs)
                    {
                        Main.npc[s].realLife = NPC.whoAmI;
                        Main.npc[s].netUpdate = true;
                    }
                }
            }

            Size();
            timer++;

            // Only on its way home is it going into its crevice, or fading there.
            if (mode != Mode.Home)
            {
                inside = false;
                NPC.alpha = 0;
            }

            // Where the head is now, onto the way it has come; but not when it is going back
            // along that way (recoiling, coiling, backing off), or lying in its crevice.
            if (mode is Mode.Strike or Mode.Hunt or Mode.Lunge or Mode.Home or Mode.Leave || mode == Mode.Recover && Shaking)
                Record(NPC.Center);

            // Nobody near: gone.
            if (Deciding)
            {
                bool someone = false;

                foreach (Player player in Main.ActivePlayers)
                {
                    if (!player.dead && player.Distance(NPC.Center) < 160 * 16f)
                        someone = true;
                }

                alone = someone ? 0 : alone + 1;

                if (alone > 300)
                {
                    Vanish();
                    return;
                }
            }

            switch (mode)
            {
                case Mode.Lurk:
                    Lurk();
                    break;
                case Mode.Coil:
                    Coil();
                    break;
                case Mode.Strike:
                    Strike();
                    break;
                case Mode.Recoil:
                    Recoil();
                    break;
                case Mode.Hunt:
                    Hunt();
                    break;
                case Mode.Lunge:
                    Lunge();
                    break;
                case Mode.Recover:
                    Recover();
                    break;
                case Mode.Home:
                    Home();
                    break;
                default:
                    Leave();
                    break;
            }

            provoked = false;

            // Its head's core never left in the rock: where it is put straight onto its own way
            // back (recoiling, coiling, backing off), that way's points joined by straight lines can
            // cut a corner, and the game's own test lets a box already in a tile go on into it.
            if (!OpenAt(NPC.Center, CoreSize / 2f - 1f))
                NPC.Center = Unstick(NPC.Center);

            // The body's waves: as it swims out in the water, the faster the bigger, up to
            // WaveMost at the tail; none in a crevice (lurking, coiling there, recoiling, gone in).
            bool free = mode is Mode.Hunt or Mode.Leave || mode == Mode.Home && !inside || mode == Mode.Recover && Shaking;
            float wave = free ? WaveMost * Math.Min(1f, NPC.velocity.Length() / 4f) : 0f;

            waveAmp = mode is Mode.Lurk or Mode.Recoil || inside ? 0f : MathHelper.Lerp(waveAmp, wave, 0.06f);
            wavePhase = (wavePhase + 0.1f + NPC.velocity.Length() * 0.05f) % MathHelper.TwoPi;

            // Facing away from its body, a little ahead where it is going.
            Place();
            Vector2 ahead = NPC.Center + NPC.velocity * 4f - placed[1];

            if (ahead.LengthSquared() > 1f)
                NPC.rotation = ahead.ToRotation();
        }

        /// <summary>
        /// Its life made up to its length, once, on every machine as it hears of its length
        /// (NPC.ai[2]): as much more than <see cref="Life"/> as its pieces past the least ask for,
        /// in the same proportion as the game has already raised its life for the world's
        /// difficulty; still whole if it was whole.
        /// </summary>
        private void Size()
        {
            if (sized)
                return;

            sized = true;

            if (Extra <= 0)
                return;

            bool whole = NPC.life >= NPC.lifeMax;
            NPC.lifeMax = (int)Math.Round(NPC.lifeMax * (double)(Life + LifeEachPiece * Extra) / Life);

            if (whole)
                NPC.life = NPC.lifeMax;
        }

        private bool sized;

        /// <summary>Gone, nothing dropped (the server tells everyone).</summary>
        private void Vanish()
        {
            NPC.active = false;
            NPC.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        /// <summary>
        /// In its crevice: at the mouth, swaying a little in and out with its breath; a player
        /// within <see cref="WatchTiles"/> tiles and in its sight from the mouth, its head out and
        /// turned after them (<see cref="Peek"/>), slowly; one nearer than
        /// <see cref="ReadyTiles"/>, drawn back in, ready (on every machine, from what it knows of
        /// the players). Where it is decided: woken, out after whom woke it; a player within
        /// <see cref="StrikeTiles"/> tiles and seen, or one who hit it within twelve, a strike;
        /// one worth hunting, a hunt.
        /// </summary>
        private void Lurk()
        {
            NPC.velocity = Vector2.Zero;

            if (!lairKnown)
            {
                Become(Mode.Leave);
                return;
            }

            Vector2 axis = new Vector2(lair.Dx, lair.Dy);
            int near = Nearest(out float nearest);
            Player p = near >= 0 ? Main.player[near] : null;
            bool seen = p != null && nearest < WatchTiles * 16f && Sees(Eye, p);
            Vector2 pose;

            watching = seen;

            if (!seen)
                pose = lair.Mouth + axis * (float)Math.Sin((Main.GameUpdateCount + NPC.whoAmI * 31) * 0.05f) * 2f;
            else if (nearest < ReadyTiles * 16f)
                pose = lair.Mouth - axis * DrawBack;
            else
                pose = Peek(p.Center);

            // Eased toward it, never through the rock about the mouth: the head's core kept clear,
            // going along the crevice first where the straight way would cut its corner, or
            // back over its middle line, coming in.
            Vector2 next = Vector2.Lerp(NPC.Center, pose, seen ? 0.12f : 0.08f);

            if (!OpenAt(next, CoreSize / 2f - 1f))
            {
                Vector2 across = new Vector2(-axis.Y, axis.X);
                Vector2 along = NPC.Center + axis * Vector2.Dot(next - NPC.Center, axis);
                Vector2 centred = NPC.Center - across * Vector2.Dot(NPC.Center - lair.Mouth, across) * 0.25f;

                next = OpenAt(along, CoreSize / 2f - 1f) ? along : OpenAt(centred, CoreSize / 2f - 1f) ? centred : NPC.Center;
            }

            NPC.Center = next;

            if (!Deciding || timer < 0)
                return;

            if (waking > 0 && --waking == 0 && Fair(wakePrey) && HuntWorth(Main.player[wakePrey]))
            {
                Become(Mode.Hunt, wakePrey);
                return;
            }

            if (patience > 0 && --patience == 0 && Fair(patiencePrey))
            {
                Become(Mode.Hunt, patiencePrey);
                return;
            }

            // Looked about every sixth of a second for one to strike at, every half second for
            // one to hunt; at once, hit.
            if (p == null || timer % 10 != 0 && !provoked)
                return;

            if (nearest < StrikeTiles * 16f && seen || provoked && nearest < 12 * 16f)
            {
                CoilFor(Mode.Strike, near);
                return;
            }

            if (provoked)
            {
                Become(Mode.Hunt, near);
                return;
            }

            // One worth hunting: it waits a while first, watching if it can see them (a second and
            // a half to five; a third of a second to one, if they bleed), then comes out.
            if (timer % 30 == 0)
            {
                if (!HuntWorth(p))
                {
                    patience = 0;
                }
                else if (patience == 0)
                {
                    patience = p.bleed ? Main.rand.Next(20, 61) : Main.rand.Next(90, 301);
                    patiencePrey = near;
                }
            }
        }

        /// <summary>Where a lurking eel looks out from: half a tile out from its crevice's mouth, whatever its head is doing.</summary>
        private Vector2 Eye => lair.Mouth + new Vector2(lair.Dx, lair.Dy) * 8f;

        /// <summary>
        /// Its head out of the crevice and turned toward this point: its neck at the crevice's
        /// mouth, its jaws <see cref="PeekOut"/> pixels past the mouth's middle when straight out,
        /// turned toward the point as far as <see cref="LookMost"/>, and no further than leaves the
        /// head's core and its neck in open water (less, then, a quarter at a time; straight out
        /// at the least, where the crevice opens two cells wide).
        /// </summary>
        private Vector2 Peek(Vector2 toward)
        {
            Vector2 axis = new Vector2(lair.Dx, lair.Dy);
            float out0 = axis.ToRotation();
            Vector2 neck = lair.Mouth + axis * (PeekOut - HeadPivot.X);
            float want = MathHelper.Clamp(MathHelper.WrapAngle((toward - neck).ToRotation() - out0), -LookMost, LookMost);

            for (int step = 4; step > 0; step--)
            {
                Vector2 jaws = neck + (out0 + want * step / 4f).ToRotationVector2() * HeadPivot.X;

                if (OpenAt(jaws, CoreSize / 2f) && OpenAt(Vector2.Lerp(neck, jaws, 0.6f), 8f))
                    return jaws;
            }

            return lair.Mouth + axis * PeekOut;
        }

        /// <summary>The nearest place about this one, within twelve pixels, where the head's core is clear of the rock (the eight ways, a pixel further each time); this one if there is none.</summary>
        private static Vector2 Unstick(Vector2 at)
        {
            for (int r = 1; r <= 12; r++)
            {
                for (int k = 0; k < 8; k++)
                {
                    Vector2 tried = at + (k * MathHelper.PiOver4).ToRotationVector2() * r;

                    if (OpenAt(tried, CoreSize / 2f - 1f))
                        return tried;
                }
            }

            return at;
        }

        /// <summary>The way straight from here to there clear for the head's core, looked at every four pixels.</summary>
        private static bool Clear(Vector2 from, Vector2 to)
        {
            int steps = (int)(Vector2.Distance(from, to) / 4f);

            for (int i = 1; i <= steps; i++)
            {
                if (!OpenAt(Vector2.Lerp(from, to, (float)i / steps), CoreSize / 2f))
                    return false;
            }

            return true;
        }

        /// <summary>No rock in the square this far about this point.</summary>
        private static bool OpenAt(Vector2 at, float half)
        {
            int x0 = (int)Math.Floor((at.X - half) / 16f), x1 = (int)Math.Floor((at.X + half - 0.01f) / 16f);
            int y0 = (int)Math.Floor((at.Y - half) / 16f), y1 = (int)Math.Floor((at.Y + half - 0.01f) / 16f);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (EelLairs.Rock(x, y))
                        return false;
                }
            }

            return true;
        }

        /// <summary>The nearest player living, and how far.</summary>
        private int Nearest(out float distance)
        {
            int near = -1;
            distance = float.MaxValue;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead || player.ghost)
                    continue;

                float d = player.Distance(NPC.Center);

                if (d < distance)
                {
                    distance = d;
                    near = player.whoAmI;
                }
            }

            return near;
        }

        /// <summary>Gone deep and near enough by the way through the caves (farther if bleeding) to be hunted.</summary>
        private bool HuntWorth(Player player)
        {
            int reach = player.bleed ? SmellTiles : HuntTiles;

            // Lurking, reckoned from its crevice's mouth, wherever its head is turned.
            Vector2 from = mode == Mode.Lurk && lairKnown ? lair.Mouth : NPC.Center;

            if (!GoneDeep(player) || player.Distance(from) > reach * 16f)
                return false;

            Chart(player.Center);

            if (!Way(from, out _, out int steps))
                return false;

            return steps <= reach;
        }

        /// <summary>
        /// Drawing its head back a moment before it goes: from its crevice, into it, its jaws
        /// <see cref="DrawBack"/> pixels inside the mouth, over <see cref="CoilTicks"/>; hunting,
        /// back along its own body, slowing to nothing, over <see cref="LungeCoilTicks"/>. Then it
        /// strikes, or lunges (if the way straight at them is still clear for its head; else on
        /// hunting, to come nearer); the prey gone meanwhile, back into its crevice, or home.
        /// </summary>
        private void Coil()
        {
            NPC.velocity = Vector2.Zero;
            bool fromLair = coilInto == Mode.Strike && lairKnown;
            int ticks = coilInto == Mode.Strike ? CoilTicks : LungeCoilTicks;

            if (timer == 1)
            {
                coilFrom = NPC.Center;

                if (fromLair)
                {
                    Lay();
                    Rewind(DrawBack);
                }
            }

            if (fromLair)
            {
                float t = Math.Min(1f, timer / (float)ticks);
                NPC.Center = Vector2.Lerp(coilFrom, TrailAt(trailCount - 1), t * t * (3f - 2f * t));
            }
            else if (trailCount > 1)
            {
                Rewind(CoilBackSpeed);
                NPC.Center = TrailAt(trailCount - 1);
            }

            if (timer < ticks || !Deciding)
                return;

            balked = false;

            if (!Fair(prey))
            {
                Become(coilInto == Mode.Strike ? Mode.Recoil : lairKnown ? Mode.Home : Mode.Leave);
            }
            else if (coilInto == Mode.Lunge && !Clear(NPC.Center, Main.player[prey].Center))
            {
                balked = true;
                Become(Mode.Hunt);
            }
            else
            {
                Become(coilInto);
            }
        }

        /// <summary>Its last coil to lunge given up (the way not clear): it waits a second before it coils again (where it is decided).</summary>
        private bool balked;

        private void Strike()
        {
            if (!Fair(prey))
            {
                Become(Mode.Recoil);
                return;
            }

            Player p = Main.player[prey];

            if (timer == 1)
            {
                NPC.velocity = (p.Center - NPC.Center).SafeNormalize(new Vector2(lair.Dx, lair.Dy)) * StrikeSpeed;

                if (!Main.dedServ)
                    SoundEngine.PlaySound(Rush, NPC.Center);
            }
            else
            {
                Vector2 want = (p.Center - NPC.Center).SafeNormalize(NPC.velocity) * StrikeSpeed;
                NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.12f);
            }

            bool done = timer >= StrikeTicks
                || lairKnown && Vector2.Distance(NPC.Center, lair.Mouth) > StrikeReach * 16f
                || NPC.Distance(p.Center) < 24f
                || timer > 4 && NPC.velocity.Length() < 2f;

            if (done && Deciding)
            {
                if (!Main.dedServ)
                    SoundEngine.PlaySound(Snap, NPC.Center);

                Become(lairKnown ? Mode.Recoil : Mode.Hunt);
            }
        }

        private void Recoil()
        {
            NPC.velocity = Vector2.Zero;

            if (!lairKnown)
            {
                Become(Mode.Leave);
                return;
            }

            Rewind(BackSpeed);

            if (trailCount > 0)
                NPC.Center = TrailAt(trailCount - 1);

            if (trailLength <= lurkLength + 0.5f || trailCount <= 1)
            {
                Lay();
                NPC.Center = lair.Mouth;

                if (Deciding)
                {
                    Become(Mode.Lurk);
                    timer = -45;
                }
            }
        }

        /// <summary>
        /// After them through the caves by the chart (<see cref="Way"/>, keeping to the rock):
        /// stalking, slow, the body in long waves (<see cref="StalkSpeed"/>, <see cref="CruiseSpeed"/>
        /// while more than forty steps off), and every <see cref="StalkBeat"/> ticks stopping
        /// <see cref="PauseTicks"/> to taste the water, its head turning; seeing them within
        /// <see cref="CloseTiles"/>, creeping, without stopping; within <see cref="LungeTiles"/>,
        /// seen, and its turn (<see cref="MyTurn"/>), it coils to lunge; its turn not come, it
        /// holds off, circling them (<see cref="Circle"/>). Where it is decided, it gives them up
        /// as before (<see cref="LoseTiles"/>, out of the caves, too long too far).
        /// </summary>
        private void Hunt()
        {
            if (!Fair(prey))
            {
                if (Deciding)
                    Become(lairKnown ? Mode.Home : Mode.Leave);

                return;
            }

            Player p = Main.player[prey];

            if (chartAge++ >= 30 || Math.Abs(CellOf(p.Center).X - chartGoal.X) > 4 || Math.Abs(CellOf(p.Center).Y - chartGoal.Y) > 4)
                Chart(p.Center);

            bool onChart = Way(out Vector2 toward, out int steps);

            if (!onChart)
                toward = p.Center;

            float distance = NPC.Distance(p.Center);
            bool seen = distance < CloseTiles * 16f && Sees(p);
            bool turn = MyTurn(p);

            if (Deciding)
            {
                lost = !onChart || steps > LoseTiles ? lost + 1 : 0;
                outside = GoneDeep(p) ? 0 : outside + 1;

                if (lost > 180 || outside > 240 || timer > 2400 && steps > 30)
                {
                    Become(lairKnown ? Mode.Home : Mode.Leave);
                    return;
                }

                // Lunging only where the way to them is all but straight (by the chart, a head its
                // size can take it), so that it does not go at them through a gap it cannot pass.
                // And from where it will be once coiled (drawn back along its own body).
                if (timer > (balked ? 60 : 20) && distance < LungeTiles * 16f && seen && turn && onChart && steps <= distance / 16f * 1.25f + 3f
                    && Clear(Back(LungeCoilTicks * CoilBackSpeed), p.Center))
                {
                    CoilFor(Mode.Lunge);
                    return;
                }
            }

            if (!turn && seen && distance < (HoldOffTiles + 2f) * 16f)
            {
                Circle(p, distance);
                return;
            }

            if (Pausing && !seen)
            {
                // Stopped, tasting the water: the way it was going let go, the head turning a
                // little either way.
                Vector2 body = (NPC.Center - placed[1]).SafeNormalize(Vector2.UnitX);
                NPC.velocity = NPC.velocity * 0.86f + new Vector2(-body.Y, body.X) * (float)Math.Sin(timer * 0.25f) * 1.6f;
                return;
            }

            float speed = seen ? CreepSpeed : steps > 40 ? CruiseSpeed : StalkSpeed;
            float heading = (toward - NPC.Center).ToRotation() + 0.28f * (float)Math.Sin(timer * 0.16f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, heading.ToRotationVector2() * speed, 0.08f);
        }

        /// <summary>Hunting, stopped a while to taste the water (the last <see cref="PauseTicks"/> of every <see cref="StalkBeat"/>, each eel on its own beat).</summary>
        private bool Pausing => mode == Mode.Hunt && (timer + Id % StalkBeat) % StalkBeat >= StalkBeat - PauseTicks;

        /// <summary>
        /// Its turn to go in at its prey: no other of its kind after them coiling to lunge,
        /// lunging or shaking them, and none hunting them nearer them by a tile and a half.
        /// </summary>
        private bool MyTurn(Player p)
        {
            float mine = NPC.Distance(p.Center);

            foreach (NPC other in Main.ActiveNPCs)
            {
                if (other.whoAmI == NPC.whoAmI || other.ModNPC is not CreviceEelHead eel || eel.prey != prey)
                    continue;

                if (eel.mode == Mode.Coil && eel.coilInto == Mode.Lunge || eel.mode == Mode.Lunge || eel.Shaking)
                    return false;

                if (eel.mode == Mode.Hunt && other.Distance(p.Center) + 24f < mine)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Holding off, its turn not come: round its prey at <see cref="HoldOffTiles"/> tiles, one
        /// way (<see cref="side"/>) till the rock turns it back (where it is decided).
        /// </summary>
        private void Circle(Player p, float distance)
        {
            Vector2 outward = (NPC.Center - p.Center) / Math.Max(1f, distance);
            Vector2 round = new Vector2(-outward.Y, outward.X) * side;
            Vector2 want = round * CircleSpeed + outward * MathHelper.Clamp((HoldOffTiles * 16f - distance) * 0.03f, -2f, 2f);

            NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.07f);

            if (Deciding && (NPC.collideX || NPC.collideY) && timer % 15 == 0)
            {
                side = (sbyte)-side;
                NPC.netUpdate = true;
            }
        }

        /// <summary>Straight at them, fast, where they will be, its mouth wide; ended on them, it has bitten (<see cref="bit"/>).</summary>
        private void Lunge()
        {
            if (!Fair(prey))
            {
                bit = false;
                Become(Mode.Recover);
                return;
            }

            Player p = Main.player[prey];

            if (timer == 1)
            {
                Vector2 aim = p.Center + p.velocity * 6f;
                NPC.velocity = (aim - NPC.Center).SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * LungeSpeed;

                if (!Main.dedServ)
                    SoundEngine.PlaySound(Rush, NPC.Center);
            }

            bool reached = NPC.Distance(p.Center) < 30f;

            if ((timer >= LungeTicks || reached || timer > 4 && NPC.velocity.Length() < 3f) && Deciding)
            {
                if (!Main.dedServ)
                    SoundEngine.PlaySound(Snap, NPC.Center);

                bit = reached;
                Become(Mode.Recover);
            }
        }

        /// <summary>After a lunge that ended on its prey, shaking it, its head thrown from side to side.</summary>
        private bool Shaking => mode == Mode.Recover && bit && timer <= ShakeTicks;

        /// <summary>
        /// After a lunge: if it bit, shaking them (<see cref="ShakeTicks"/>); then backing off the
        /// way it came, tail first, along its own body (<see cref="BackOffTicks"/>), and a moment
        /// still; then after them again, or home.
        /// </summary>
        private void Recover()
        {
            if (Shaking)
            {
                Vector2 body = (NPC.Center - placed[1]).SafeNormalize(Vector2.UnitX);
                NPC.velocity = new Vector2(-body.Y, body.X) * (float)Math.Sin(timer * 1.2f) * 3.5f;
                return;
            }

            int back = timer - (bit ? ShakeTicks : 0);
            NPC.velocity = Vector2.Zero;

            if (back <= BackOffTicks && trailCount > 1)
            {
                Rewind(BackOffSpeed);
                NPC.Center = TrailAt(trailCount - 1);
            }

            if (back >= BackOffTicks + 6 && Deciding)
                Become(Fair(prey) ? Mode.Hunt : lairKnown ? Mode.Home : Mode.Leave);
        }

        // ---- the way home -----------------------------------------------------------------------

        /// <summary>Tiles about it a crevice is looked for in, when it cannot get to its own.</summary>
        public const int AdoptTiles = 100;

        /// <summary>
        /// The way it is following home (cells, from the first after where it was when the way was
        /// found: <see cref="EelWays"/>), the cell of it it is at, and ticks since it was found.
        /// </summary>
        private readonly List<Point> way = new List<Point>();
        private int wayAt, wayAge;

        /// <summary>Where it was two seconds ago on its way home, and how many times running it has made no headway since.</summary>
        private Vector2 wayCheck;
        private int stuckFor;

        /// <summary>A way from where its head is to this place, followed from its start; false if there is none.</summary>
        private bool WayTo(Vector2 goal)
        {
            wayAt = 0;
            wayAge = 0;
            return EelWays.Find(CellOf(NPC.Center), CellOf(goal), way);
        }

        /// <summary>
        /// Where to swim for on the way found: three cells on from the cell of it nearest the head
        /// (looked for a little back and on from the last). False if it has strayed more than three
        /// cells from the way, or there is none.
        /// </summary>
        private bool Along(out Vector2 toward)
        {
            toward = NPC.Center;
            int best = -1;
            float nearest = float.MaxValue;

            for (int i = Math.Max(0, wayAt - 4); i < Math.Min(way.Count, wayAt + 12); i++)
            {
                float d = Vector2.DistanceSquared(MiddleOf(way[i].X, way[i].Y), NPC.Center);

                if (d < nearest)
                {
                    nearest = d;
                    best = i;
                }
            }

            if (best < 0 || nearest > 48f * 48f)
                return false;

            wayAt = best;
            int ahead = Math.Min(way.Count - 1, best + 3);
            toward = MiddleOf(way[ahead].X, way[ahead].Y);
            return true;
        }

        /// <summary>The nearest free crevice it can get to (and its body fits) taken for its own, on its way home to it; false if there is none within <see cref="AdoptTiles"/> tiles.</summary>
        private bool Adopt()
        {
            if (!Deciding || !CreviceEelSpawns.Nearest(this, CellOf(NPC.Center), AdoptTiles, way, out EelLair found))
                return false;

            lair = found;
            lairKnown = true;
            inside = false;
            stuckFor = 0;
            Become(Mode.Home);
            wayAt = 0;
            wayAge = 0;
            return true;
        }

        /// <summary>
        /// Back to its crevice, into it head first, and gone, by request ("they should have smart ai
        /// to find their way back inside"): by the shortest way through the water a head its size
        /// can take, from wherever it is (<see cref="EelWays"/>), found again every two seconds, if
        /// it strays from it, or if it makes no headway; if there is no way to its own crevice, or
        /// it is stuck three times running, the nearest free crevice it can get to becomes its own
        /// (<see cref="Adopt"/>); with none, away (<see cref="Leave"/>).
        /// </summary>
        private void Home()
        {
            if (!lairKnown)
            {
                if (Deciding && !Adopt())
                    Become(Mode.Leave);

                return;
            }

            if (provoked && Deciding)
            {
                int near = Fair(NPC.lastInteraction) ? NPC.lastInteraction : Nearest(out _);

                if (Fair(near))
                {
                    Become(Mode.Hunt, near);
                    return;
                }
            }

            if (!inside && Vector2.Distance(NPC.Center, lair.Mouth) < 24f)
            {
                inside = true;
                NPC.netUpdate = true;
            }

            if (inside)
            {
                Vector2 down = lair.Point(0) - NPC.Center;
                NPC.velocity = down.SafeNormalize(Vector2.Zero) * Math.Min(3f, down.Length());

                if (down.Length() < 12f || timer > 400)
                {
                    NPC.alpha = Math.Min(255, NPC.alpha + 17);

                    if (NPC.alpha >= 255 && Deciding)
                        Vanish();
                }

                return;
            }

            // The way home: found when first wanted, every two seconds, and when it has strayed.
            if (way.Count == 0 || wayAge++ >= 120 || !Along(out Vector2 toward))
            {
                if (!WayTo(lair.Mouth) || !Along(out toward))
                {
                    toward = lair.Mouth;

                    if (Deciding && !Adopt())
                    {
                        Become(Mode.Leave);
                        return;
                    }
                }
            }

            // No headway in two seconds: the way found again; three times running, another crevice.
            if (timer % 120 == 0 && timer > 0)
            {
                if (Vector2.Distance(NPC.Center, wayCheck) < 32f)
                {
                    wayAge = 120;

                    if (++stuckFor >= 3 && Deciding)
                    {
                        stuckFor = 0;

                        if (!Adopt())
                        {
                            Become(Mode.Leave);
                            return;
                        }
                    }
                }
                else
                {
                    stuckFor = 0;
                }

                wayCheck = NPC.Center;
            }

            float heading = (toward - NPC.Center).ToRotation() + 0.3f * (float)Math.Sin(timer * 0.2f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, heading.ToRotationVector2() * HomeSpeed, 0.1f);
        }

        /// <summary>With no crevice to go to: home to one it can get to after all if there is one (looked for every two seconds), else away from whoever is nearest, and gone once nobody can see it.</summary>
        private void Leave()
        {
            if (Deciding && timer % 120 == 1 && Adopt())
                return;

            int near = Nearest(out float distance);

            if (near < 0 || distance > 70 * 16f)
            {
                if (Deciding)
                    Vanish();

                return;
            }

            Vector2 away = (NPC.Center - Main.player[near].Center).SafeNormalize(Vector2.UnitX);
            float heading = away.ToRotation() + 0.35f * (float)Math.Sin(timer * 0.22f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, heading.ToRotationVector2() * HomeSpeed, 0.08f);
        }

        // ---- hit, biting, gone ------------------------------------------------------------------

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (Deciding && NPC.life > 0)
            {
                provoked = true;

                if (mode is Mode.Lurk or Mode.Home or Mode.Leave)
                {
                    int by = Fair(NPC.lastInteraction) ? NPC.lastInteraction : Nearest(out _);

                    if (Fair(by) && mode == Mode.Lurk && NPC.Distance(Main.player[by].Center) < 12 * 16f)
                        CoilFor(Mode.Strike, by);
                    else if (Fair(by))
                        Become(Mode.Hunt, by);
                }
            }

            if (Main.dedServ)
                return;

            for (int i = 0; i < 4; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 1.5f, -1f);

            if (NPC.life <= 0)
            {
                Place();

                for (int k = 1; k <= Length; k++)
                {
                    for (int i = 0; i < 3; i++)
                        Dust.NewDust(placed[k] - new Vector2(9f), 18, 18, DustID.Blood, 0f, 0f);
                }
            }
        }

        /// <summary>
        /// Its bite (the head's only): it tears, the bitten bleeding five to ten seconds; and it
        /// seizes, the bitten held and pulled along with the head a moment, as a moray's second
        /// jaws take what it bites and pull it back (CreviceEelSeized; run on the bitten one's own
        /// machine, where their moving is decided).
        /// </summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            target.AddBuff(BuffID.Bleeding, Main.rand.Next(300, 601));
            target.GetModPlayer<CreviceEelSeized>().Seize(NPC, Id);

            if (!Main.dedServ)
                SoundEngine.PlaySound(Snap, NPC.Center);
        }

        /// <summary>Its own going: never the game's for a creature far off.</summary>
        public override bool CheckActive() => false;

        public override void FindFrame(int frameHeight)
        {
            int frame;

            switch (mode)
            {
                case Mode.Lurk:
                    // Its breath: the mouth shut, opening, open, closing, over two seconds;
                    // watching someone, over a little more than one.
                    int period = watching ? 70 : 120;
                    frame = Breath((int)((Main.GameUpdateCount + (uint)(NPC.whoAmI * 17)) % (uint)period), period);
                    break;
                case Mode.Coil:
                    // Shut as it draws back; opening as it goes.
                    frame = timer >= (coilInto == Mode.Strike ? CoilTicks : LungeCoilTicks) - 2 ? 1 : 0;
                    break;
                case Mode.Strike:
                case Mode.Lunge:
                    frame = 2;
                    break;
                case Mode.Recover:
                    frame = Shaking ? (timer / 3 % 2 == 0 ? 2 : 1) : 1;
                    break;
                case Mode.Recoil:
                    frame = 1;
                    break;
                case Mode.Hunt:
                    // Stalking, the mouth working slowly; stopped to taste the water, twice as fast.
                    int beat = Pausing ? 50 : 100;
                    frame = Breath(timer % beat, beat);
                    break;
                default:
                    frame = 0;
                    break;
            }

            NPC.frame.Y = frame * frameHeight;
        }

        /// <summary>The mouth through one breath of this many ticks: shut half of it, opening, open, closing.</summary>
        private static int Breath(int t, int period)
        {
            float f = t / (float)period;
            return f < 0.5f ? 0 : f < 0.625f ? 1 : f < 0.79f ? 2 : 1;
        }

        // ---- drawing ----------------------------------------------------------------------------

        private static Asset<Texture2D> bodyPicture, bodyLights, eyes;

        /// <summary>How bright its eyes and the lights along it are drawn over it, in the dark as in the light.</summary>
        private const float Lights = 0.55f;

        public override void Unload()
        {
            bodyPicture = null;
            bodyLights = null;
            eyes = null;
            chartQueue = null;
            chartRoom = null;
        }

        private static void Request()
        {
            bodyPicture ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Deep/CreviceEelBody");
            bodyLights ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Deep/CreviceEelBodyGlow");
            eyes ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Deep/CreviceEelHeadGlow");
        }

        /// <summary>The whole eel: its tail, its body from the tail up, its head, each with its lights over it, shining a little in the dark; in the bestiary, swimming (<see cref="DrawBestiary"/>).</summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Request();

            if (NPC.IsABestiaryIconDummy)
            {
                DrawBestiary(spriteBatch, screenPos, drawColor);
                return false;
            }

            float shown = 1f - NPC.alpha / 255f;

            Place();

            for (int k = Length; k >= 1; k--)
            {
                Color light = Lighting.GetColor((int)(placed[k].X / 16f), (int)(placed[k].Y / 16f)) * shown;
                DrawPiece(spriteBatch, placed[k] - screenPos, turned[k], k == Length ? 2 : k % 2, light, shown, NPC.scale);
            }

            DrawHead(spriteBatch, NPC.Center - screenPos, headFacing, NPC.frame, drawColor * shown, shown, NPC.scale);
            return false;
        }

        /// <summary>One piece of the body (its picture's frame: 0 and 1 bodies, 2 the tail), facing the way given (toward the head), its lights over it.</summary>
        private static void DrawPiece(SpriteBatch spriteBatch, Vector2 at, float facing, int frame, Color light, float shown, float scale)
        {
            Texture2D body = bodyPicture.Value;
            int high = body.Height / 3;
            Rectangle from = new Rectangle(0, frame * high, body.Width, high);
            Vector2 middle = new Vector2(body.Width / 2f, high / 2f);
            SpriteEffects flip = Math.Cos(facing) < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            spriteBatch.Draw(body, at, from, light, facing, middle, scale, flip, 0f);
            spriteBatch.Draw(bodyLights.Value, at, from, Color.White * Lights * shown, facing, middle, scale, flip, 0f);
        }

        /// <summary>
        /// The head, its jaws where the head is (<see cref="HeadPivot"/>), facing as given (from its
        /// neck to its jaws: <see cref="headFacing"/>), its eyes and lights over it.
        /// Turned to face left it is drawn upside down (its back still up), and the game turns a
        /// picture so drawn about the point mirrored in it (FNA flips the picture, not the point).
        /// </summary>
        private void DrawHead(SpriteBatch spriteBatch, Vector2 at, float facing, Rectangle frame, Color colour, float shown, float scale)
        {
            Texture2D head = Terraria.GameContent.TextureAssets.Npc[Type].Value;
            bool flipped = Math.Cos(facing) < 0;
            Vector2 pivot = new Vector2(HeadPivot.X, flipped ? frame.Height - HeadPivot.Y : HeadPivot.Y);
            SpriteEffects flip = flipped ? SpriteEffects.FlipVertically : SpriteEffects.None;

            spriteBatch.Draw(head, at, frame, colour, facing, pivot, scale, flip, 0f);
            spriteBatch.Draw(eyes.Value, at, frame, Color.White * Lights * shown, facing, pivot, scale, flip, 0f);
        }

        /// <summary>Pieces drawn in the bestiary after the head: five of body and the tail, 180 pixels from the snout's tip to the tail's.</summary>
        private const int BestiaryPieces = 6;

        /// <summary>
        /// The eel in the bestiary, swimming: its own pictures laid along a wave that runs down it
        /// from the head, each piece turned with the wave, its mouth working as when it lurks,
        /// its lights on; about its middle, where the bestiary puts the creature, at its scale
        /// there. As Calamity's worms are shown there, by my own reckoning.
        /// </summary>
        private void DrawBestiary(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            const float Ahead = 24f;            // the snout's tip before the head's middle
            const float Wave = 120f;            // pixels from crest to crest along the eel
            const float Height = 5f;            // pixels from its middle to a crest
            float scale = NPC.scale;
            float time = Main.GlobalTimeWrappedHourly * 3f;
            float lit = Math.Max(drawColor.R, Math.Max(drawColor.G, drawColor.B)) / 255f;   // its lights as dark as it is drawn (none on a black shape)
            float back = NeckGap + (BestiaryPieces - 1) * Spacing + bodyPicture.Value.Width / 2f;
            float headX = (Ahead + back) / 2f - Ahead;       // so that the whole eel is about the middle
            Vector2 middle = NPC.Center - screenPos;

            Vector2 Along(float s) => middle + new Vector2(headX - s, Height * (float)Math.Sin(s / Wave * MathHelper.TwoPi - time)) * scale;
            float Facing(float s) => (float)Math.Atan2(-Height * MathHelper.TwoPi / Wave * Math.Cos(s / Wave * MathHelper.TwoPi - time), 1.0);

            for (int k = BestiaryPieces; k >= 1; k--)
            {
                float s = NeckGap + (k - 1) * Spacing;
                DrawPiece(spriteBatch, Along(s), Facing(s), k == BestiaryPieces ? 2 : k % 2, drawColor, lit, scale);
            }

            int t = (int)(Main.GlobalTimeWrappedHourly * 60f) % 120;
            int frame = t < 60 ? 0 : t < 75 ? 1 : t < 95 ? 2 : 1;
            Texture2D head = Terraria.GameContent.TextureAssets.Npc[Type].Value;
            int high = head.Height / Main.npcFrameCount[Type];

            // Turned about its neck, as in the world: from where its neck lies on the wave to its jaws.
            DrawHead(spriteBatch, Along(0f), (Along(0f) - Along(HeadPivot.X)).ToRotation(), new Rectangle(0, frame * high, head.Width, high), drawColor, lit, scale);
        }
    }
}
