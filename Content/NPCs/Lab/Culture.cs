using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Lab
{
    /// <summary>
    /// The Culture, by request ("Create a carrion like NPCs with tentacles like it with hooking and dragging physics and
    /// blood dripping; amorphous masses of tentacles with eyes ... dragging and moving wildly takes knock back unlike any
    /// npcs (each part being shot takes back but the mass doesnt) optimize it too; it has 6 tentacles to drag its mass and
    /// should both fit a 4 tile tall hallway and squeeze thru a 1 tile tall path. move too fast for its mass and js jump to
    /// close ranged attack; bit 300hp base tho; 50 damage base"; it lives in the Abandoned Lab, one or two at a time;
    /// "Culture"; "gore that dissolves itself thats it"; "carrion dont have to be super detailed; can js be red shades";
    /// and, from Carrion's own first quarter hour, "thin slender and fast").
    ///
    ///   its mass       a small knot of red: a soft haze of it about a denser core, thin strands curling out of it all
    ///                  the time, an eye or two in it (three kinds), a mouth that shows only as it comes at something.
    ///                  Thirty-two pixels each way, but it changes its shape to the way it is going: flattened down to
    ///                  one tile high to get through a crawlspace, thinned to one tile wide up a shaft. Nothing knocks it
    ///                  back.
    ///   its six        hooks (by request: "make it actually anchor faraway and pull it in; think of tentacles like hooks
    ///   tentacles      physics thats pulling it"): up to three at a time thrown out ahead, as far as they will go
    ///                  within thirteen tiles, onto whatever is solid or any wall behind; each takes hold with a yank and
    ///                  reels the mass in
    ///                  toward where it holds, the further the harder, up walls and over ceilings; on one alone it swings
    ///                  on it as on a rope; on none it falls. A hook it has come up to or gone past it lets go of and
    ///                  throws again, ahead (Tentacle). Leaping is the one time it jumps instead.
    ///                  Shot, a tentacle is thrown back where it was hit, and a hold near its tip may be shot loose; the
    ///                  shot hurts the Culture as a shot on its mass would, and is spent as one would be.
    ///   its way        through the lab by the way to whoever it hunts (CulturePath: a map of the lab's open cells, the
    ///                  steps from each to them, made again every third of a second), straight at them once they are in
    ///                  sight.
    ///   its leap       in sight and within nine tiles: it draws itself in a moment, lets go of everything and throws
    ///                  itself at them, mouth open; then pulls itself together and comes on again.
    ///   blood          dripping off it as it goes, more as it is hurt; and when it dies it comes apart into pieces
    ///                  (CultureGore) that dissolve where they fall. Nothing else.
    ///
    /// The lab is single player, as every place is; it runs the same anywhere, and is kept to one machine's view of it.
    /// </summary>
    public partial class Culture : ModNPC
    {
        public const int Life = 300;
        public const int Hurts = 50;

        /// <summary>Its mass, as it usually is, in pixels; and the shapes it takes to get through, roundest first, flattened along the way it goes (and the Ball, below, last of all).</summary>
        private const int Size = 32;

        private static readonly Point[] Wide = { new(32, 32), new(42, 24), new(52, 18), new(60, 14), new(24, 42), new(18, 52), new(14, 60) };
        private static readonly Point[] Tall = { new(32, 32), new(24, 42), new(18, 52), new(14, 60), new(42, 24), new(52, 18), new(60, 14) };

        /// <summary>How fast it crawls (by request, a fifth slower than it was: 8.6), and squeezing through (35% slower than that 8.6), and how fast it gets there; how fast it falls; how fast it leaps.</summary>
        private const float Crawl = 6.88f;
        private const float Squeezing = 5.59f;
        private const float Grip = 0.16f;

        /// <summary>How hard its hooks reel it in (how much of the way to the speed they would have it go, a tick), and the yank as one takes hold.</summary>
        private const float Reels = 0.14f;
        private const float Yank = 1.6f;
        private const float Gravity = 0.32f;
        private const float Falls = 12f;
        private const float Leaps = 15f;

        /// <summary>The leap: from how near (pixels), how long it draws itself in, how long it flies, how long it takes to pull itself together, and how long before the next.</summary>
        private const float LeapFrom = 144f;
        private const int Draws = 12;
        private const int Flies = 24;
        private const int Gathers = 20;
        private const int LeapRest = 80;

        private enum Act { Crawl, Draw, Leap, Gather }

        private Act act;
        private int actTime;
        private int leapRest = LeapRest;

        private Tentacle[] arms;
        private Vector2 way;
        private float time;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            LoadArt();

            NPCID.Sets.NPCBestiaryDrawModifiers drawn = new() { Scale = 0.8f, PortraitScale = 0.9f };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawn);
        }

        public override void SetDefaults()
        {
            NPC.width = Size;
            NPC.height = Size;
            NPC.lifeMax = Life;
            NPC.damage = Hurts;
            NPC.defense = 8;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.aiStyle = -1;
            NPC.value = 0f;
            NPC.HitSound = SoundID.NPCHit13;
            NPC.DeathSound = SoundID.NPCDeath12;
            NPC.npcSlots = 2f;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.PisAndDag.NPCs.Culture.Bestiary"));
        }

        /// <summary>Only where the lab puts it (CulturePath.Spawn): never by the game's own spawning.</summary>
        public override float SpawnChance(NPCSpawnInfo spawnInfo) => 0f;

        public override bool? CanFallThroughPlatforms() => way.Y > 0.35f || act == Act.Leap;

        // ---- each tick -----------------------------------------------------------------------------------------------

        public override void AI()
        {
            time += 1f / 60f;

            if (arms == null)
                Grow();

            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active || Main.GameUpdateCount % 30 == 0)
                NPC.TargetClosest(false);

            Player prey = Main.player[NPC.target];
            NPC.timeLeft = Math.Max(NPC.timeLeft, 600);

            way = CulturePath.Toward(NPC, prey);
            Shape(way);

            int holding = 0;

            foreach (Tentacle t in arms)
                if (t.State == Tentacle.Hold.Holding)
                    holding++;

            bool seen = Collision.CanHitLine(NPC.position, NPC.width, NPC.height, prey.position, prey.width, prey.height);
            float apart = Vector2.Distance(prey.Center, NPC.Center);

            if (leapRest > 0)
                leapRest--;

            actTime++;

            switch (act)
            {
                case Act.Crawl:
                case Act.Gather:
                    Move(holding);
                    Hold(prey);

                    if (act == Act.Gather && actTime >= Gathers)
                        Begin(Act.Crawl);

                    if (act == Act.Crawl && leapRest <= 0 && seen && apart < LeapFrom && !prey.dead)
                        Begin(Act.Draw);

                    break;

                case Act.Draw:
                    NPC.velocity *= 0.8f;

                    if (actTime >= Draws)
                    {
                        // Everything let go of, and at them: where they will be in a moment.
                        foreach (Tentacle t in arms)
                            t.LetGo(16);

                        Vector2 at = prey.Center + prey.velocity * 6f;
                        NPC.velocity = (at - NPC.Center).SafeNormalize(Vector2.UnitX * NPC.direction) * Leaps;
                        SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.35f, Volume = 0.9f }, NPC.Center);
                        Begin(Act.Leap);
                    }

                    break;

                case Act.Leap:
                    NPC.velocity *= 0.985f;
                    Bleed(2);

                    if (actTime >= Flies || NPC.collideX || NPC.collideY)
                    {
                        leapRest = LeapRest + Main.rand.Next(40);
                        Begin(Act.Gather);
                    }

                    break;
            }

            if (NPC.velocity.X != 0f)
                NPC.direction = NPC.spriteDirection = NPC.velocity.X > 0f ? 1 : -1;

            Writhe(prey);
            Shot();
            Bleed(NPC.life < NPC.lifeMax / 2 ? 2 : 1);
            Settle();
            KeepOut();
        }

        /// <summary>
        /// Never into the safe room (LabPlan.SafeRoom; by request: "Cultures can't enter"): the lamp over its door
        /// (LabUVLamp) it will not pass. Coming at it, it is turned back, recoiling, out of any leap; ever in it, it is put
        /// out of it, west, the way its door is.
        /// </summary>
        private void KeepOut()
        {
            if (!CulturePath.InLab)
                return;

            var s = LabPlan.SafeRoom;
            Rectangle safe = new Rectangle(s.X0 * 16, s.Y0 * 16, (s.X1 - s.X0 + 1) * 16, (s.Y1 - s.Y0 + 1) * 16);

            if (NPC.Hitbox.Intersects(safe))
            {
                NPC.position.X = safe.Left - NPC.width - 4;
                NPC.velocity.X = -2f;
                return;
            }

            Rectangle next = NPC.Hitbox;
            next.Offset((int)Math.Ceiling(NPC.velocity.X), (int)Math.Ceiling(NPC.velocity.Y));
            next.Inflate(4, 4);

            if (!next.Intersects(safe))
                return;

            NPC.velocity.X = -Math.Abs(NPC.velocity.X) * 0.4f - 1.2f;
            NPC.velocity.Y *= 0.5f;
            jolt.X -= 4f;

            if (act == Act.Leap || act == Act.Draw)
                Begin(Act.Gather);
        }

        private void Begin(Act next)
        {
            act = next;
            actTime = 0;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// Its crawl. Wedged in a squeeze, it holds by the walls on both sides and goes the way it wants. Hooked, it is
        /// reeled in: toward its holds, each pulling the harder the further off it is and the more it is ahead, steered a
        /// little the way it wants; on two or more, as fast as it crawls; on one, a little less, and it swings on that one
        /// as on a rope, never further from it than the rope it has let out, the rope taken in as it comes. Hooked to
        /// nothing, it falls, steering a little.
        /// </summary>
        private void Move(int holding)
        {
            // Squeezed flat, thin or balled up in a gap, it is wedged against the walls on both sides: it holds by them.
            bool wedged = NPC.width <= 18 || NPC.height <= 18;

            if (wedged)
            {
                NPC.velocity = Vector2.Lerp(NPC.velocity, way * Squeezing, Grip);

                if (NPC.velocity.Length() > Squeezing)
                    NPC.velocity = Vector2.Normalize(NPC.velocity) * Squeezing;
            }
            else if (holding >= 1)
            {
                Vector2 pull = Vector2.Zero;

                foreach (Tentacle t in arms)
                {
                    if (t.State != Tentacle.Hold.Holding)
                        continue;

                    Vector2 to = t.Anchor - NPC.Center;
                    float d = to.Length();

                    if (d < 1f)
                        continue;

                    float ahead = Math.Clamp(Vector2.Dot(to / d, way) + 0.3f, 0.1f, 1.3f);
                    pull += to / d * ahead * (0.3f + d / Tentacle.Reach);
                }

                Vector2 reel = (pull.SafeNormalize(way) * 0.75f + way * 0.25f).SafeNormalize(way);
                float top = holding >= 2 ? Crawl : Crawl * 0.85f;

                NPC.velocity = Vector2.Lerp(NPC.velocity, reel * top, Reels);

                if (holding == 1)
                {
                    // On one alone: it hangs from it, and swings, on the rope it has let out.
                    NPC.velocity.Y += Gravity * 0.35f;

                    foreach (Tentacle t in arms)
                    {
                        if (t.State != Tentacle.Hold.Holding)
                            continue;

                        Vector2 to = t.Anchor - (NPC.Center + NPC.velocity);
                        float d = to.Length();

                        if (d > t.Rope && d > 1f)
                        {
                            Vector2 n = to / d;
                            float outward = -Vector2.Dot(NPC.velocity, n);

                            if (outward > 0f)
                                NPC.velocity += n * outward;
                        }
                    }
                }

                if (NPC.velocity.Length() > top)
                    NPC.velocity = Vector2.Normalize(NPC.velocity) * top;
            }
            else
            {
                NPC.velocity.Y += Gravity;
                NPC.velocity.X = Math.Clamp(NPC.velocity.X + way.X * 0.15f, -Crawl * 0.5f, Crawl * 0.5f);
            }

            // Every rope taken in as far as the mass has come.
            foreach (Tentacle t in arms)
                if (t.State == Tentacle.Hold.Holding)
                    t.Rope = Math.Min(t.Rope, Vector2.Distance(t.Anchor, NPC.Center));

            if (NPC.velocity.Length() > Falls)
                NPC.velocity = Vector2.Normalize(NPC.velocity) * Falls;
        }

        /// <summary>
        /// Its shape for the way it is going: the roundest that has room here and a little further on, flattened along the
        /// way it goes. In a hall four tiles high it stays round (eased off a ceiling or a wall to be, if it must); through a
        /// crawlspace a tile high it goes flat, in line with the gap; up a shaft a tile wide, thin; and where none of those
        /// has room, in a gap a tile wide that turns and turns again, balled up a tile across (Ball). Its knot is drawn where
        /// it was and springs after it (jolt), so that none of this shows as a jump.
        /// </summary>
        private void Shape(Vector2 toward)
        {
            bool flat = Math.Abs(toward.X) >= Math.Abs(toward.Y);
            Point[] shapes = flat ? Wide : Tall;
            Vector2 across = flat ? Vector2.UnitY : Vector2.UnitX;
            Vector2 ahead = toward * 10f + NPC.velocity;

            // Rounder a little to one side (off a ceiling, a wall): eased that way, three pixels a tick.
            foreach (Point s in shapes)
            {
                if (Fits(s, NPC.Center, ahead, flat, out _))
                    break;

                if (s.X <= 18 || s.Y <= 18)
                    continue;

                bool eased = false;

                foreach (float shift in Shifts)
                {
                    if (!Fits(s, NPC.Center + across * shift, ahead, flat, out _))
                        continue;

                    Vector2 step = across * Math.Clamp(shift, -3f, 3f);

                    if (!Collision.SolidCollision(NPC.position + step, NPC.width, NPC.height))
                        NPC.position += step;

                    eased = true;
                    break;
                }

                if (eased)
                    break;
            }

            Vector2 middle = NPC.Center;

            foreach (Point s in shapes)
            {
                if (Fits(s, middle, ahead, flat, out Vector2 at))
                {
                    Become(s, middle, at);
                    return;
                }
            }

            // None of its shapes has room: balled up, if a tile's room is here and the next step on.
            if (Fits(Ball, middle, toward * 10f, flat, out Vector2 tight))
                Become(Ball, middle, tight);
        }

        /// <summary>This shape, about here; the knot drawn where it was if it was moved more than a little, and springing after it.</summary>
        private void Become(Point s, Vector2 middle, Vector2 at)
        {
            if (s.X == NPC.width && s.Y == NPC.height && at == middle)
                return;

            NPC.width = s.X;
            NPC.height = s.Y;
            NPC.Center = at;

            if (Vector2.DistanceSquared(middle, at) > 9f)
                jolt += middle - at;
        }

        /// <summary>Its smallest: balled up, a little under a tile across, for a gap a tile wide that turns.</summary>
        private static readonly Point Ball = new(14, 14);

        /// <summary>How far to one side and the other it looks for room to be rounder.</summary>
        private static readonly float[] Shifts = { 6f, -6f, 12f, -12f };

        /// <summary>
        /// Room for this shape about this middle (<paramref name="at"/>), and a little further on. Going along a row
        /// (<paramref name="flatWay"/>), a flat shape (or the ball) is tried in line with the row of tiles it is in or the
        /// one above or below; going up or down, a thin one (or the ball) with the column: where the gap it makes for is.
        /// </summary>
        private static bool Fits(Point s, Vector2 middle, Vector2 ahead, bool flatWay, out Vector2 at)
        {
            // In line across the way it goes only: flat along a row, thin up a column. (Flat going up has no room ahead.)
            bool rowed = s.Y <= 18 && flatWay;
            bool columned = s.X <= 18 && !flatWay;
            Vector2 half = new Vector2(s.X, s.Y) * 0.5f;
            at = middle;

            for (int k = 0; k < 3; k++)
            {
                if (k > 0 && !rowed && !columned)
                    break;

                float lean = k == 0 ? 0f : k == 1 ? -8f : 8f;
                Vector2 here = middle;

                if (rowed)
                    here.Y = InLine(middle.Y + lean);

                if (columned)
                    here.X = InLine(middle.X + lean);

                if (Collision.SolidCollision(here - half, s.X, s.Y))
                    continue;

                Vector2 there = here + ahead;

                if (rowed)
                    there.Y = here.Y;

                if (columned)
                    there.X = here.X;

                if (Collision.SolidCollision(there - half, s.X, s.Y))
                    continue;

                at = here;
                return true;
            }

            return false;
        }

        /// <summary>The middle of the row (or column) of tiles this is in.</summary>
        private static float InLine(float v) => MathF.Floor(v / 16f) * 16f + 8f;
    }
}
