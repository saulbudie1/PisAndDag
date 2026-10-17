using System;
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
    /// THE HOLE DWELLER, by request ("Create a worm ai, Eel enemy, (no gravity, dont go thru
    /// tiles) living in these caverns, if player goes deep they jump out and slither thru the
    /// caves to you. research eels habits"); first the Crevice Eel, a name of mine, then renamed
    /// ("rename it: Hole Dwellers", one of them a Hole Dweller), its code and files keeping the
    /// first name. Made bigger to look menacing ("make the eel kinda bigger too"), its pictures
    /// drawn again in 2x2 pixels, in the look of the gulper eel pointed at (CreviceEelHead,
    /// CreviceEelBody and their Glow pictures; the bestiary's eel is drawn from them, moving:
    /// <see cref="DrawBestiary"/>). What is known of morays and
    /// congers (the notes for section 16.92 have the sources): they live in holes and crevices
    /// of the rock (congers in wrecks too, and down to 500 metres and deeper), resting there by
    /// day with the head out; the mouth opens and shuts the while, which is how they breathe,
    /// pushing water out through small gill openings; they see poorly and hunt by smell, at
    /// night; their teeth are jagged, and a second pair of jaws in the throat pulls prey down.
    /// Mine, from that: it strikes from its crevice at what comes near and goes back into it
    /// tail first; it smells blood from farther; its bite leaves the bitten bleeding; and in
    /// the caves, where it is always night, it hunts whenever a player is there.
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
    /// <b>What it does.</b>
    ///   - LURKS in its crevice (CreviceEelSpawns puts it there), the head at the mouth, the
    ///     body in the rock behind it, the mouth working;
    ///   - STRIKES at a player who comes within <see cref="StrikeTiles"/> tiles of its head and
    ///     in its sight: out of the crevice straight at them, up to <see cref="StrikeReach"/>
    ///     tiles, and back into it tail first the way it came (RECOILS);
    ///   - HUNTS a player who has gone deep (in the Vast Ocean from the 8th section down, under
    ///     rock: four solid rows or more within forty over them) and is within
    ///     <see cref="HuntTiles"/> tiles of it by the way through the caves (<see cref="SmellTiles"/>
    ///     if they are bleeding): out of the crevice and through the caves to them, the whole
    ///     body waving, by the shortest way through the water a head its size can take (a chart
    ///     of steps to them from every cell within <see cref="ChartReach"/> tiles, worked out
    ///     again every half second); and when it is within <see cref="LungeTiles"/> tiles of
    ///     them and sees them, LUNGES, fast, straight, its mouth open, and RECOVERS after;
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
        /// The head's hitbox, square about its jaws, as thick as the head is there (its picture, 36
        /// to 42 high about them); and its core, what the rock keeps out (what its hitbox was before
        /// it grew: a head of it fits a burrow two cells across with room to spare).
        /// </summary>
        public const int HitSize = 40;
        public const int CoreSize = 24;

        /// <summary>Where in the head's picture (one frame: 72 by 52) the head's middle is: at its jaws, 48 from its neck.</summary>
        public static readonly Vector2 HeadPivot = new Vector2(48f, 26f);

        /// <summary>Provisional, for the deep: life, armour, the head's bite and the body's.</summary>
        public const int Life = 2400;
        public const int Armour = 16;
        public const int Bite = 85;
        public const int BodyBite = 55;

        /// <summary>Tiles: strikes at a player this near and seen; goes this far out of its crevice at most striking.</summary>
        public const float StrikeTiles = 7f;
        public const float StrikeReach = 8f;

        /// <summary>Tiles, by the way through the caves: hunts a player gone deep this near; this near if they bleed; loses them farther.</summary>
        public const int HuntTiles = 60;
        public const int SmellTiles = 90;
        public const int LoseTiles = 100;

        /// <summary>Tiles: lunges at a player this near, and seen.</summary>
        public const float LungeTiles = 10f;

        /// <summary>Pixels a tick.</summary>
        public const float StrikeSpeed = 9f;
        public const float HuntSpeed = 5f;
        public const float LungeSpeed = 10.5f;
        public const float BackSpeed = 4.5f;
        public const float HomeSpeed = 4f;

        public const int StrikeTicks = 18;
        public const int LungeTicks = 22;
        public const int RecoverTicks = 30;

        /// <summary>Of the deep: never pressed.</summary>
        public int SafeTo => VastOceanWeight.Deepest;

        public static readonly SoundStyle Snap = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Snap") { MaxInstances = 3, PitchVariance = 0.15f, Volume = 0.8f };
        public static readonly SoundStyle Rush = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Rush") { MaxInstances = 2, PitchVariance = 0.12f, Volume = 0.7f };
        public static readonly SoundStyle Struck = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Hit") { MaxInstances = 4, PitchVariance = 0.2f, Volume = 0.6f };
        public static readonly SoundStyle Dying = new SoundStyle("PisAndDag/Content/Sounds/Deep/Eel/Death") { MaxInstances = 1, Volume = 0.9f };

        private enum Mode : byte
        {
            Lurk,
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
        /// For a Hole Dweller's head, the box the game keeps out of the rock (NPC.UpdateCollision,
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
            bool known = reader.ReadBoolean();

            if (known)
            {
                var got = new EelLair(reader.ReadInt16(), reader.ReadInt16(), reader.ReadSByte(), reader.ReadSByte(), reader.ReadByte());
                bool fresh = !lairKnown || !lair.Same(got) || trailCount == 0;

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

        private readonly Vector2[] placed = new Vector2[Segments + 1];
        private readonly float[] turned = new float[Segments + 1];

        /// <summary>
        /// The way the head faces as it is drawn: from where its neck lies on its trail (as far
        /// back as the head is long from its jaws, <see cref="HeadPivot"/>) to its jaws; so it turns
        /// about its neck, by request ("rotate it by the neck"), the jaws going ahead and the neck
        /// staying on the way the body goes, as each piece of the body lies.
        /// </summary>
        private float headFacing;
        private uint placedOn = uint.MaxValue;
        private Vector2 placedFrom;

        /// <summary>Where a piece of the body lies now (1 the first after the head, <see cref="Segments"/> the tail), and which way it faces (toward the head).</summary>
        public Vector2 Piece(int index, out float facing)
        {
            Place();
            index = Math.Clamp(index, 1, Segments);
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

            for (int piece = 1; piece <= Segments; piece++)
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
        private Point chartGoal = new Point(int.MinValue, int.MinValue);
        private int chartAge;

        /// <summary>Room for the head here: the two by two tiles from this one, open.</summary>
        private static bool Room(int x, int y) => !EelLairs.Rock(x, y) && !EelLairs.Rock(x + 1, y) && !EelLairs.Rock(x, y + 1) && !EelLairs.Rock(x + 1, y + 1);

        /// <summary>The cell (two by two tiles from it) a point is in the middle of, near enough.</summary>
        private static Point CellOf(Vector2 world) => new Point((int)Math.Round(world.X / 16f) - 1, (int)Math.Round(world.Y / 16f) - 1);

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
            Array.Fill(chart, (short)-1);

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

                    int wx = g.X - ChartReach + nx, wy = g.Y - ChartReach + ny;

                    if (!Room(wx, wy))
                    {
                        chart[n] = -2;
                        continue;
                    }

                    if (k >= 4 && (!Room(wx - DX[k], wy) || !Room(wx, wy - DY[k])))
                        continue;

                    chart[n] = next;
                    chartQueue[tail++] = n;
                }
            }
        }

        /// <summary>
        /// The way to go from where the head is, by the chart: toward the cell three steps on down
        /// it; the head's steps to the goal. False if the head is not on the chart (nor within two
        /// cells of it).
        /// </summary>
        private bool Way(out Vector2 toward, out int steps)
        {
            Point at = CellOf(NPC.Center);
            steps = Steps(at.X, at.Y);
            toward = NPC.Center;

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
                short least = Steps(at.X, at.Y);

                for (int k = 0; k < 8; k++)
                {
                    short s = Steps(at.X + DX[k], at.Y + DY[k]);

                    if (s >= 0 && s < least)
                    {
                        least = s;
                        next = new Point(at.X + DX[k], at.Y + DY[k]);
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
        private bool Sees(Player player)
        {
            Vector2 a = NPC.Center, b = player.Center;
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

        /// <summary>What it does next, where that is decided (elsewhere it is heard of).</summary>
        private void Become(Mode next, int target = -2)
        {
            if (!Deciding)
                return;

            mode = next;
            timer = 0;

            if (target != -2)
                prey = target;

            NPC.netUpdate = true;
        }

        public override void AI()
        {
            // The first tick, where it is decided: who it is, and its body.
            if (Deciding && NPC.ai[3] == 0f)
            {
                NPC.ai[3] = Main.rand.Next(1, 1 << 24);
                NPC.netUpdate = true;
            }

            if (Deciding && !bodied && NPC.ai[3] != 0f)
            {
                bodied = true;
                int bodyType = ModContent.NPCType<CreviceEelBody>();

                for (int i = 1; i <= Segments; i++)
                {
                    int s = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, bodyType, NPC.whoAmI, NPC.whoAmI, i, NPC.ai[3]);

                    if (s < Main.maxNPCs)
                    {
                        Main.npc[s].realLife = NPC.whoAmI;
                        Main.npc[s].netUpdate = true;
                    }
                }
            }

            timer++;

            // Only on its way home is it going into its crevice, or fading there.
            if (mode != Mode.Home)
            {
                inside = false;
                NPC.alpha = 0;
            }

            // Where the head is now, onto the way it has come; but not when it is going back
            // along that way, or lying still in its crevice.
            if (mode != Mode.Recoil && mode != Mode.Lurk)
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

            // Facing away from its body, a little ahead where it is going.
            Place();
            Vector2 ahead = NPC.Center + NPC.velocity * 4f - placed[1];

            if (ahead.LengthSquared() > 1f)
                NPC.rotation = ahead.ToRotation();
        }

        /// <summary>Gone, nothing dropped (the server tells everyone).</summary>
        private void Vanish()
        {
            NPC.active = false;
            NPC.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        private void Lurk()
        {
            NPC.velocity = Vector2.Zero;

            if (!lairKnown)
            {
                Become(Mode.Leave);
                return;
            }

            // At its mouth, swaying a little in and out with its breath.
            float sway = (float)Math.Sin((Main.GameUpdateCount + NPC.whoAmI * 31) * 0.05f) * 2f;
            NPC.Center = lair.Mouth + new Vector2(lair.Dx, lair.Dy) * sway;

            // Looked about every sixth of a second for one to strike at, every half second for
            // one to hunt; at once, hit.
            if (!Deciding || timer < 0 || timer % 10 != 0 && !provoked)
                return;

            int near = Nearest(out float nearest);

            if (near < 0)
                return;

            Player p = Main.player[near];

            if (nearest < StrikeTiles * 16f && Sees(p) || provoked && nearest < 12 * 16f)
            {
                Become(Mode.Strike, near);
                return;
            }

            if (provoked || timer % 30 == 0 && HuntWorth(p))
                Become(Mode.Hunt, near);
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

            if (!GoneDeep(player) || player.Distance(NPC.Center) > reach * 16f)
                return false;

            Chart(player.Center);

            if (!Way(out _, out int steps))
                return false;

            return steps <= reach;
        }

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
                || NPC.Distance(p.Center) < 18f
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

            Vector2 toward;
            bool onChart = Way(out toward, out int steps);

            if (!onChart)
                toward = p.Center;

            if (Deciding)
            {
                lost = !onChart || steps > LoseTiles ? lost + 1 : 0;
                outside = GoneDeep(p) ? 0 : outside + 1;

                if (lost > 180 || outside > 240 || timer > 2400 && steps > 30)
                {
                    Become(lairKnown ? Mode.Home : Mode.Leave);
                    return;
                }

                if (timer > 20 && NPC.Distance(p.Center) < LungeTiles * 16f && Sees(p))
                {
                    Become(Mode.Lunge);
                    return;
                }
            }

            // Waving as it goes, the whole body after it.
            float heading = (toward - NPC.Center).ToRotation() + 0.35f * (float)Math.Sin(timer * 0.22f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, heading.ToRotationVector2() * HuntSpeed, 0.1f);
        }

        private void Lunge()
        {
            if (!Fair(prey))
            {
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

            if ((timer >= LungeTicks || timer > 4 && NPC.velocity.Length() < 3f) && Deciding)
            {
                if (!Main.dedServ)
                    SoundEngine.PlaySound(Snap, NPC.Center);

                Become(Mode.Recover);
            }
        }

        private void Recover()
        {
            NPC.velocity *= 0.9f;

            if (timer >= RecoverTicks && Deciding)
                Become(Fair(prey) ? Mode.Hunt : lairKnown ? Mode.Home : Mode.Leave);
        }

        /// <summary>Back to its crevice through the caves, into it head first, and gone; or, if it cannot get there, away.</summary>
        private void Home()
        {
            if (!lairKnown)
            {
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

            if (chartAge++ >= 30 || chartGoal != CellOf(lair.Mouth))
                Chart(lair.Mouth);

            if (!Way(out Vector2 toward, out _))
            {
                lost++;

                if (lost > 180 && Deciding)
                    Become(Mode.Leave);

                toward = lair.Mouth;
            }

            float heading = (toward - NPC.Center).ToRotation() + 0.3f * (float)Math.Sin(timer * 0.2f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, heading.ToRotationVector2() * HomeSpeed, 0.1f);
        }

        /// <summary>Away from whoever is nearest, and gone once nobody can see it.</summary>
        private void Leave()
        {
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

                    if (Fair(by))
                        Become(mode == Mode.Lurk && NPC.Distance(Main.player[by].Center) < 12 * 16f ? Mode.Strike : Mode.Hunt, by);
                }
            }

            if (Main.dedServ)
                return;

            for (int i = 0; i < 4; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 1.5f, -1f);

            if (NPC.life <= 0)
            {
                Place();

                for (int k = 1; k <= Segments; k++)
                {
                    for (int i = 0; i < 3; i++)
                        Dust.NewDust(placed[k] - new Vector2(9f), 18, 18, DustID.Blood, 0f, 0f);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            // The bite tears: the bitten bleed, five to ten seconds.
            target.AddBuff(BuffID.Bleeding, Main.rand.Next(300, 601));

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
                    // Its breath: the mouth shut, opening, open, closing, over two seconds.
                    int t = (int)((Main.GameUpdateCount + (uint)(NPC.whoAmI * 17)) % 120);
                    frame = t < 60 ? 0 : t < 75 ? 1 : t < 95 ? 2 : 1;
                    break;
                case Mode.Strike:
                case Mode.Lunge:
                    frame = 2;
                    break;
                case Mode.Recoil:
                case Mode.Recover:
                    frame = 1;
                    break;
                case Mode.Hunt:
                    frame = timer / 20 % 3 == 0 ? 0 : 1;
                    break;
                default:
                    frame = 0;
                    break;
            }

            NPC.frame.Y = frame * frameHeight;
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

            for (int k = Segments; k >= 1; k--)
            {
                Color light = Lighting.GetColor((int)(placed[k].X / 16f), (int)(placed[k].Y / 16f)) * shown;
                DrawPiece(spriteBatch, placed[k] - screenPos, turned[k], k == Segments ? 2 : k % 2, light, shown, NPC.scale);
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
