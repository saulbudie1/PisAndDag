using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// Every number the Abomanation is made of, in one place.
    ///
    /// The six at the top are the ones asked for by name. They are static and writable
    /// rather than const so they can be turned at runtime — a command, a debug panel, a
    /// second difficulty — without a rebuild.
    ///
    /// Read <see cref="Speed"/> as a speed and <see cref="TurnSpeed"/> as a turn rate,
    /// because that is what they are. Desert Scourge's own code calls its acceleration
    /// "speed" and keeps the actual travel cap in a separate local, which is why nothing
    /// in that file does what its name says. Here Speed is pixels per tick travelled and
    /// TurnSpeed is pixels per tick the velocity is allowed to swing by, per tick. Raising
    /// Speed alone makes it faster; raising TurnSpeed alone makes it corner harder.
    /// </summary>
    public static class Abomanation
    {
        public const string Folder = "PisAndDag/Content/NPCs/Abomanation_Small/";

        // ------------------------------------------------------------- the six knobs --

        /// <summary>
        /// Ignore the ground entirely. False is the desert worm: it swims through tiles,
        /// and when it breaks into open air near you it arcs and falls like a thrown
        /// thing. True makes it a flier that steers the same everywhere, which is what
        /// you want for an arena fight with no sand in it.
        /// </summary>
        public static bool Fly = false;

        /// <summary>How fast it travels, in world pixels per tick. 9 is about 10 tiles a second.</summary>
        public static float Speed = 9f;

        /// <summary>
        /// How hard it corners, in pixels per tick of velocity change per tick. Low turns
        /// it into a train on rails; high lets it follow you round a corner. Above about
        /// a third of Speed it stops reading as a worm and starts reading as a homing
        /// missile.
        /// </summary>
        public static float TurnSpeed = 0.08f;

        /// <summary>
        /// How far off the player it aims, in world pixels. At 0 it drives at your exact
        /// centre, which is both easy to sidestep and impossible to dodge at close range.
        /// The offset wanders rather than orbits (see AbomanationHead.StrayOffset), so
        /// there is no circle to learn.
        /// </summary>
        public static float Stray = 140f;

        /// <summary>Travel speed during the rush straight up, in pixels per tick.</summary>
        public static float RushSpeed = 15f;

        /// <summary>
        /// How far the rush climbs from the point it turned around, in TILES.
        ///
        /// It turns around at <see cref="DiveDepth"/> below you, so this number has to
        /// beat DiveDepth/16 for the rush to reach you at all. The default clears it by
        /// twelve tiles, which puts the head out of the sand and above your head.
        /// </summary>
        public static int Distance = 62;

        // --------------------------------------------------------------- the rest -----

        /// <summary>Body segments between the head and the tail.</summary>
        public const int BodySegments = 15;

        /// <summary>Distinct body textures, cycled in order along the body.</summary>
        public const int BodyTextures = 5;

        /// <summary>
        /// Bottom arms on ONE side. The body segments are the only thing that carries any,
        /// so this is the whole count for that side of the creature.
        ///
        /// Seventeen over fifteen segments averages just over one each, so most segments
        /// carry one, a few carry two or three and several carry none. That unevenness is
        /// the point: a fixed number per segment reads as a machine.
        /// </summary>
        public const int BottomArmsPerSide = 14;

        /// <summary>Most one segment may carry on one side.</summary>
        public const int BottomArmsPerSegment = 3;

        /// <summary>
        /// Centre to centre, head to first body segment.
        ///
        /// The head's sprite reaches 36 pixels behind its centre and a body segment's
        /// reaches 24 in front of its own, so this buys a 16 pixel overlap. It needs to be
        /// more than the body's own 10, because the head is 56 wide against the body's 48
        /// and the step between them shows as a notch in the neck at anything looser.
        /// </summary>
        public const float HeadSpacing = 44f;

        /// <summary>Centre to centre, body segment to body segment. Under the sprite height, so they overlap.</summary>
        public const float BodySpacing = 42f;

        /// <summary>Centre to centre, last body segment to tail.</summary>
        public const float TailSpacing = 46f;

        /// <summary>How far below the player it digs before turning around, in world pixels.</summary>
        public const float DiveDepth = 800f;

        /// <summary>Ticks of chasing before the rush. Only counts while it is below you or well off to the side.</summary>
        public const int RushGate = 600;

        /// <summary>Ticks an attack is allowed to take before it is abandoned and the chase resumes.</summary>
        public const int RushTimeout = 3 * 60;

        /// <summary>
        /// Past this distance it keeps swimming even in open air, so a worm crossing the
        /// map to reach you does not flop through the sky one parabola at a time.
        /// </summary>
        public const float AirSwimDistance = 1000f;

        /// <summary>
        /// Inside this, it stops steering and commits to the pass. See AbomanationHead.Steer:
        /// without it the worm parks inside you and never leaves.
        /// </summary>
        public const float CommitDistance = 300f;

        /// <summary>
        /// What <see cref="TurnSpeed"/> is multiplied by while the velocity is pointing
        /// away from where it wants to go, which is the one moment a worm needs to turn
        /// faster than it otherwise does.
        /// </summary>
        public const float TurnBoost = 3f;
    }

    /// <summary>
    /// The limbs. Drawn by the segments, owned by nobody.
    ///
    /// The arm sprites all run bottom to top: the narrow nub at the bottom of the canvas
    /// is where the limb meets the body, and the mass at the top is the far end. So the
    /// origin is the bottom of the sprite, an unrotated arm points along its own -Y, and
    /// a quarter turn puts it out to one side. Everything below is that one fact applied
    /// twice, once per side.
    ///
    /// Positions are in SPRITE pixels measured from the segment's centre, and are scaled
    /// by NPC.scale at draw time. Angles are in radians and are measured from "forward",
    /// meaning along the body toward the head, so a positive angle rakes an arm toward
    /// the tail on the right side and toward the head on the left.
    /// </summary>
    public static class AbomanationArms
    {
        // ------------------------------------------------------------ bottom arms -----

        /// <summary>
        /// How far out from the centre line the bottom arms are rooted.
        ///
        /// A body segment is 48 wide, so 16 puts the root 8 pixels inside its edge and the
        /// arm comes out from under the mass rather than off the side of it. Further out
        /// and the root sits on the silhouette, which reads as stuck on.
        /// </summary>
        public const float BottomInset = 16f;

        /// <summary>Half the span the bottom arms are spread over along a body segment.</summary>
        public const float BottomSpan = 14f;

        /// <summary>Radians the bottom arms are raked back toward the tail at rest.</summary>
        public const float BottomRake = 0.30f;

        /// <summary>Radians either side of the rake that a swinging arm travels.</summary>
        public const float Swing = 0.38f;

        /// <summary>Radians per second of the swing.</summary>
        public const float SwingSpeed = 8.2f;

        /// <summary>
        /// Phase added per SEGMENT LENGTH down the body. This is the whole trick: give
        /// every leg the same phase and the whole worm beats as one paddle, which reads as
        /// a mistake. Offset them by how far down the body they sit and the beat travels,
        /// which is what a centipede does and what the eye is looking for.
        ///
        /// Measured along the body rather than counted per row, so the wave stays
        /// continuous across segments that carry different numbers of legs.
        /// </summary>
        public const float SwingWave = 0.9f;

        // --------------------------------------------------------------- top arms -----

        /// <summary>How far out from the centre line the top arms are rooted, at most.</summary>
        public const float TopInset = 11f;

        /// <summary>Half the span the top arms are scattered over, along the body.</summary>
        public const float TopSpan = 13f;

        /// <summary>Radians the top arms are raked back toward the tail. Same meaning as <see cref="BottomRake"/>.</summary>
        public const float TopRake = 0.45f;

        /// <summary>Radians of scatter on a top arm's angle.</summary>
        public const float TopJitter = 0.50f;

        // ---------------------------------------------------------------- assets ------
        //
        // Requested on first draw rather than in SetStaticDefaults. Draw code never runs
        // on a server, so this is the one place the request can be made without also
        // having to guard for a machine that has no graphics device.

        private static Asset<Texture2D> bottomR, bottomL, top1, top2, top3R, top3L;

        // Origins are the attachment nub, read off the sprites pixel by pixel, not the
        // middle of the canvas. The nub is off-centre on every one of them except
        // Arm_On_Top_2, so a nominal bottom-centre origin would plant four of the six
        // arms two or three pixels outside the body.
        private static readonly Vector2 BottomOriginR = new Vector2(5f, 30f);
        private static readonly Vector2 BottomOriginL = new Vector2(9f, 30f);
        private static readonly Vector2 Top1Origin = new Vector2(3f, 24f);
        private static readonly Vector2 Top2Origin = new Vector2(5f, 24f);
        private static readonly Vector2 Top3OriginR = new Vector2(5f, 30f);
        private static readonly Vector2 Top3OriginL = new Vector2(9f, 30f);

        private static Asset<Texture2D> Get(ref Asset<Texture2D> slot, string name)
        {
            // ImmediateLoad rather than the default async: an arm requested during a draw
            // is wanted by that draw. Asynchronously it is not there for the first frame
            // or two, which is a worm that grows its legs after it arrives.
            return slot ??= ModContent.Request<Texture2D>(Abomanation.Folder + name, AssetRequestMode.ImmediateLoad);
        }

        /// <summary>
        /// Pulls all six in at load time, off the draw path entirely. Called from
        /// AbomanationBody.SetStaticDefaults; Get still works on its own if it is not.
        /// </summary>
        public static void Load()
        {
            if (Main.dedServ)
                return;

            Get(ref bottomR, "Arm_On_Bottom_1");
            Get(ref bottomL, "Arm_On_Bottom_1_L");
            Get(ref top1, "Arm_On_Top_1");
            Get(ref top2, "Arm_On_Top_2");
            Get(ref top3R, "Arm_On_Top_3");
            Get(ref top3L, "Arm_On_Top_3_L");
        }

        /// <summary>Called from AbomanationHead.Unload. Static asset handles outlive a mod reload otherwise.</summary>
        public static void Unload()
        {
            bottomR = bottomL = top1 = top2 = top3R = top3L = null;
            counts = null;
        }

        // ------------------------------------------------------------- how many -------
        //
        // How the legs are shared out along the body. Built once, read by every segment.

        private static int[][] counts;

        // Two seeds, so the two sides are not mirror images of each other, and picked
        // rather than arbitrary: most seeds share seventeen arms out as ones and twos and
        // never once use the three a segment is allowed. These two each put a three
        // somewhere and leave three or four segments bare, which is the whole range asked
        // for actually being used.
        private const int LeftSeed = 2;
        private const int RightSeed = 11;

        /// <summary>
        /// Shares <paramref name="total"/> arms out over <paramref name="segments"/>
        /// segments, none of them carrying more than <paramref name="most"/>.
        ///
        /// Every slot a segment could fill goes into a bag, the bag is shuffled, and the
        /// first <paramref name="total"/> slots drawn are the arms. Drawing from a fixed
        /// bag is what makes the count come out exactly right: rolling each segment its
        /// own number would average what was asked for and land on it almost never, and
        /// the cap falls out of the bag holding only <paramref name="most"/> slots per
        /// segment rather than needing a check that could spin.
        ///
        /// The shuffle is the same hash the arms are scattered with, so this is stable
        /// across frames, across saves and across machines with nothing to sync.
        /// </summary>
        private static int[] Spread(int segments, int total, int most, int seed)
        {
            int[] bag = new int[segments * most];

            for (int i = 0; i < bag.Length; i++)
                bag[i] = i / most;

            for (int i = bag.Length - 1; i > 0; i--)
            {
                int j = (int)(Hash(seed, i) % (uint)(i + 1));
                int swap = bag[i];
                bag[i] = bag[j];
                bag[j] = swap;
            }

            int[] made = new int[segments];

            for (int i = 0; i < total && i < bag.Length; i++)
                made[bag[i]]++;

            return made;
        }

        /// <summary>Bottom arms on one side of one body segment. 0 to <see cref="Abomanation.BottomArmsPerSegment"/>.</summary>
        public static int BottomCount(int place, int side)
        {
            counts ??= new[]
            {
                Spread(Abomanation.BodySegments, Abomanation.BottomArmsPerSide, Abomanation.BottomArmsPerSegment, LeftSeed),
                Spread(Abomanation.BodySegments, Abomanation.BottomArmsPerSide, Abomanation.BottomArmsPerSegment, RightSeed),
            };

            int[] row = counts[side & 1];

            return place >= 0 && place < row.Length ? row[place] : 0;
        }

        // ----------------------------------------------------------------- noise ------
        //
        // A hash rather than Main.rand, because the scatter has to be the same every
        // frame and the same on every machine. Rolling it once and keeping it in a field
        // would survive the frames but not the network: a client builds its own copy of
        // the NPC and would roll its own arms. A hash of the segment index is free, has
        // no state to sync, and gives the same answer everywhere forever.

        private static uint Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13;
                h *= 2246822519u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A stable number in [0, 1) for this segment and slot.</summary>
        public static float Hash01(int a, int b) => (Hash(a, b) & 0xFFFFFFu) / 16777216f;

        /// <summary>A stable whole number in [0, n) for this segment and slot.</summary>
        public static int HashInt(int a, int b, int n) => n <= 1 ? 0 : (int)(Hash(a, b) % (uint)n);

        // ------------------------------------------------------------------ draw ------

        private static void One(SpriteBatch spriteBatch, Asset<Texture2D> asset, Vector2 origin,
            NPC npc, Vector2 screenPos, Color colour, Vector2 local, float localAngle)
        {
            if (asset is null || !asset.IsLoaded)
                return;

            Texture2D texture = asset.Value;

            // The offset is in the segment's own frame, so it turns with the segment. A
            // world-space offset would leave the legs standing still while the body rolled.
            Vector2 world = npc.Center + local.RotatedBy(npc.rotation) * npc.scale;

            spriteBatch.Draw(texture, world - screenPos, null, colour,
                npc.rotation + localAngle, origin, npc.scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The legs. Only the body segments have any; the tail does not call this.
        ///
        /// The two sides are given their counts separately rather than as a number of
        /// pairs, because they come out of <see cref="BottomCount"/> and are deliberately
        /// lopsided: a segment can carry two on one side and none on the other.
        ///
        /// However many a side has, they are spread evenly over <paramref name="span"/>
        /// either side of the segment's middle. Spread rather than scattered: one leg
        /// sits in the middle, two straddle it, three make a row, and none of the three
        /// can land on top of another.
        ///
        /// <paramref name="place"/> is how far down the body this segment sits. It only
        /// reaches the swing phase.
        /// </summary>
        public static void Bottom(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour,
            int place, int leftCount, int rightCount, float span, bool swing)
        {
            for (int side = 0; side < 2; side++)
            {
                int count = side == 0 ? leftCount : rightCount;

                if (count <= 0)
                    continue;

                // -1 is the left of the segment, +1 the right. A quarter turn times this
                // points the arm out that way: rotating "up" by +PiOver2 gives right, by
                // -PiOver2 gives left.
                float sign = side == 0 ? -1f : 1f;
                bool left = side == 0;

                for (int row = 0; row < count; row++)
                {
                    float y = count == 1 ? 0f : MathHelper.Lerp(-span, span, row / (float)(count - 1));

                    float angle = sign * (MathHelper.PiOver2 + BottomRake);

                    if (swing)
                    {
                        // Distance down the whole worm, in segment lengths, so a leg near
                        // the back of one segment is very nearly in phase with a leg near
                        // the front of the next.
                        float along = place + y / Abomanation.BodySpacing;

                        float phase = Main.GlobalTimeWrappedHourly * SwingSpeed
                            + along * SwingWave
                            + side * MathHelper.Pi;

                        angle += sign * (float)Math.Sin(phase) * Swing;
                    }

                    One(spriteBatch,
                        left ? Get(ref bottomL, "Arm_On_Bottom_1_L") : Get(ref bottomR, "Arm_On_Bottom_1"),
                        left ? BottomOriginL : BottomOriginR,
                        npc, screenPos, colour,
                        new Vector2(sign * BottomInset, y), angle);
                }
            }
        }

        /// <summary>
        /// The growths on the back. One or two per segment, scattered, and dead still
        /// relative to the body: they are part of the mass, not limbs it is walking on.
        ///
        /// A second one is always put on the opposite side from the first, because two
        /// rolls of the same hash land on top of each other often enough to notice.
        /// </summary>
        public static void TopScattered(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour, int seed)
        {
            int count = 1 + HashInt(seed, 1, 2);
            float first = Hash01(seed, 2) < 0.5f ? -1f : 1f;

            for (int i = 0; i < count; i++)
            {
                float sign = i == 0 ? first : -first;

                Vector2 local = new Vector2(
                    sign * TopInset * (0.55f + 0.45f * Hash01(seed, 10 + i)),
                    MathHelper.Lerp(-TopSpan, TopSpan, Hash01(seed, 20 + i)));

                float angle = sign * (MathHelper.PiOver2 + TopRake)
                    + (Hash01(seed, 30 + i) - 0.5f) * TopJitter;

                TopOne(spriteBatch, npc, screenPos, colour, local, angle, sign < 0f, HashInt(seed, 40 + i, 3));
            }
        }

        /// <summary>
        /// The tail's growths. <paramref name="rows"/> pairs, evenly spaced, fixed: the
        /// tail is the one part of this that is meant to look deliberate.
        /// </summary>
        public static void TopPairs(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour,
            int rows, float rowSpan, float inset)
        {
            for (int row = 0; row < rows; row++)
            {
                float y = rows == 1 ? 0f : MathHelper.Lerp(-rowSpan, rowSpan, row / (float)(rows - 1));

                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? -1f : 1f;
                    float angle = sign * (MathHelper.PiOver2 + TopRake);

                    TopOne(spriteBatch, npc, screenPos, colour,
                        new Vector2(sign * inset, y), angle, side == 0, row % 3);
                }
            }
        }

        /// <summary>
        /// One top arm. <paramref name="which"/> picks between the three sprites; only
        /// the third is handed, so the other two are drawn as they are on both sides.
        /// </summary>
        private static void TopOne(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour,
            Vector2 local, float angle, bool left, int which)
        {
            switch (which)
            {
                case 0:
                    One(spriteBatch, Get(ref top1, "Arm_On_Top_1"), Top1Origin, npc, screenPos, colour, local, angle);
                    break;

                case 1:
                    One(spriteBatch, Get(ref top2, "Arm_On_Top_2"), Top2Origin, npc, screenPos, colour, local, angle);
                    break;

                default:
                    One(spriteBatch,
                        left ? Get(ref top3L, "Arm_On_Top_3_L") : Get(ref top3R, "Arm_On_Top_3"),
                        left ? Top3OriginL : Top3OriginR,
                        npc, screenPos, colour, local, angle);
                    break;
            }
        }
    }

    /// <summary>
    /// What every segment behind the head does, which is the same thing: sit a fixed
    /// distance behind whatever is in front of it, pointing at it.
    ///
    /// ai[1] is the segment ahead, ai[2] is the head, and BOTH are stored one higher than
    /// the index they mean, so 0 can be read as "not linked yet". Desert Scourge stores
    /// them raw and then tests them with <c>ai[1] &gt; 0f</c>, which quietly means a worm
    /// whose head landed in NPC slot zero has no leader and no realLife. It is rare and
    /// it is still wrong. <see cref="Ahead"/> and <see cref="Head"/> do the decoding.
    ///
    /// ai[3] is this segment's place in the chain, which picks its texture and seeds its
    /// arms.
    /// </summary>
    public static class AbomanationChain
    {
        /// <summary>The NPC index this segment follows, or -1 while it is unlinked.</summary>
        public static int Ahead(NPC npc) => (int)npc.ai[1] - 1;

        /// <summary>The head's NPC index, or -1 while it is unlinked.</summary>
        public static int Head(NPC npc) => (int)npc.ai[2] - 1;

        /// <summary>Writes the links. <paramref name="ahead"/> and <paramref name="head"/> are plain NPC indices.</summary>
        public static void Link(NPC npc, int ahead, int head, int place)
        {
            npc.ai[1] = ahead + 1;
            npc.ai[2] = head + 1;
            npc.ai[3] = place;
            npc.realLife = head;
        }

        /// <summary>
        /// Drags the segment up behind its leader and points it that way.
        ///
        /// Written to position rather than velocity on purpose. A follower that steered
        /// would lag, and fifteen lagging followers turn a worm into an accordion; moving
        /// it to exactly the right place every tick is what keeps the body rigid at any
        /// speed the head can reach.
        /// </summary>
        public static void Follow(NPC npc, NPC leader, float spacing)
        {
            Vector2 delta = leader.Center - npc.Center;
            float distance = delta.Length();

            if (distance < 0.0001f)
                return;

            npc.rotation = delta.ToRotation() + MathHelper.PiOver2;

            npc.velocity = Vector2.Zero;
            npc.position += delta * ((distance - spacing) / distance);
        }

        /// <summary>
        /// Keeps a segment's health, damage immunity and fade in step with the head, and
        /// takes it off the board when the head is gone.
        ///
        /// The leader is checked by type as well as by index, because an NPC slot is
        /// reused the moment its occupant dies. A segment holding a stale index would
        /// otherwise start following whatever bat spawned into that slot next.
        /// </summary>
        /// <returns>False when the segment has just been removed and its AI should stop.</returns>
        public static bool Tether(NPC npc, int headType, int bodyType)
        {
            int head = Head(npc);
            int ahead = Ahead(npc);

            if (head >= 0)
                npc.realLife = head;

            bool gone = !NPC.AnyNPCs(headType) || ahead < 0 || ahead >= Main.maxNPCs;

            if (!gone)
            {
                NPC check = Main.npc[ahead];
                gone = !check.active || check.life <= 0 || (check.type != headType && check.type != bodyType);
            }

            if (gone)
            {
                npc.life = 0;
                npc.HitEffect(0, 10.0);
                npc.checkDead();
                npc.active = false;
                return false;
            }

            NPC leader = Main.npc[ahead];

            if (npc.life > leader.life)
                npc.life = leader.life;

            npc.dontTakeDamage = leader.dontTakeDamage;
            npc.canDisplayBuffs = leader.canDisplayBuffs;

            // The whole body surfaces together. Following the leader's alpha rather than
            // the head's means the fade runs down the body a segment at a time.
            if (leader.alpha < 128)
            {
                npc.alpha -= 42;
                if (npc.alpha < 0)
                    npc.alpha = 0;
            }
            else
            {
                npc.alpha = leader.alpha;
            }

            return true;
        }

        /// <summary>
        /// The narrow contact test every segment shares. The hitbox has to be wide enough
        /// to be shot at; this is what decides whether it actually touched you, and it is
        /// a circle because the sprite is one.
        /// </summary>
        public static bool Touching(NPC npc, Player target, float radius)
        {
            if (npc.alpha > 0)
                return false;

            Rectangle box = target.Hitbox;

            float nearest = Vector2.Distance(npc.Center, box.TopLeft());
            nearest = Math.Min(nearest, Vector2.Distance(npc.Center, box.TopRight()));
            nearest = Math.Min(nearest, Vector2.Distance(npc.Center, box.BottomLeft()));
            nearest = Math.Min(nearest, Vector2.Distance(npc.Center, box.BottomRight()));

            return nearest <= radius * npc.scale;
        }
    }
}
