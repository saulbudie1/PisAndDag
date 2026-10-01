using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// A boss does not fight while its name is being read.
    ///
    /// For the length of the universal arrival — pan out, plate, pan home — the boss
    /// is untouchable, touches nothing, and runs none of its AI. It passes the time
    /// one of two ways: flyers HOVER up to a spot above the player and sit there,
    /// facing them (the Eye, the Twins, the Bee, the Duke, the Empress); everything
    /// else HOLDS where it stands (the Moon Lord with nothing loaded, Plantera on her
    /// hooks, a worm in the ground). On the tick the camera is home, it starts.
    ///
    /// Bosses that animate their own arrival are exempt — BossPlate.ArrivalTicks is 0
    /// for them — and so is anything registered with BossPlate.NoWait (the Wall, whose
    /// approach is the point). The parts of a boss — hands, arms, hooks, segments —
    /// hold whenever any boss is waiting, so nothing swings while its owner stands
    /// still.
    ///
    /// Worms are the exception to holding: a worm held is a worm in a heap, because
    /// its body only strings out behind a head that moves. So a worm keeps its AI
    /// through the wait, head and segments, and is merely slowed to a crawl and made
    /// harmless. And a leader that appears while its own fight is already underway —
    /// the Eater of Worlds throws off a new head every time it is cut — gets no wait
    /// at all: the fight has been introduced once.
    ///
    /// The wait is measured from the boss's first tick, but the hold does not start
    /// until the boss has SETTLED: its first tick always runs, because that is where
    /// it builds itself (a worm's body, Skeletron's hands, Golem's), and vanilla's own
    /// fade-ins run to the end — the Duke rising out of the water, the Cultist
    /// materialising, Golem's alpha, the Empress's three-second entrance — since a
    /// boss frozen at alpha 255 is an empty screen. The Moon Lord spawns its hands
    /// and head a second in, so it settles for that second. A part gets a moment
    /// too, to fly from where it was spawned to where it belongs (Skeletron's hands
    /// are spawned on his face). None of this delays the resumption: the clock runs
    /// through the settling, and every boss starts on the tick the camera is home.
    ///
    /// Runs on server and clients alike, from the boss's own first tick, so it needs
    /// no message: the arrival's length is a function of the type, and everyone can
    /// work it out for themselves.
    /// </summary>
    public class BossArrivalWait : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // ---- settling ----------------------------------------------------------------

        /// <summary>AI ticks a boss gets before the hold, unless Setup says more. One: the build-yourself tick.</summary>
        private const int SetupDefault = 1;

        /// <summary>A boss still fading in (alpha above 0) keeps its AI this long at most; then it is held as it is.</summary>
        private const int SettleCap = 120;

        /// <summary>AI ticks a part gets before it holds, to reach the spot it belongs at, unless PartSetup says otherwise.</summary>
        private const int PartSetupDefault = 30;

        /// <summary>Bosses whose first tick is not enough. The Moon Lord's hands and head come at tick 60; the Empress takes 180 to arrive.</summary>
        private static readonly Dictionary<int, int> Setup = new()
        {
            { NPCID.MoonLordCore, 61 },
            { NPCID.HallowBoss, 180 },
        };

        /// <summary>
        /// Parts that need more or less than the default. Skeletron's hands drift out
        /// slowly (0.07 a tick) and do not swing for five seconds; Prime's arms spring
        /// out fast; Golem's are spawned where they belong and his fists punch early.
        /// </summary>
        private static readonly Dictionary<int, int> PartSetup = new()
        {
            { NPCID.SkeletronHand, 90 },
            { NPCID.PrimeCannon, 60 }, { NPCID.PrimeSaw, 60 }, { NPCID.PrimeVice, 60 }, { NPCID.PrimeLaser, 60 },
            { NPCID.GolemHead, 1 }, { NPCID.GolemFistLeft, 1 }, { NPCID.GolemFistRight, 1 }, { NPCID.GolemHeadFree, 1 },
        };

        // ---- hover -------------------------------------------------------------------

        /// <summary>Where a flyer sits: this far above the player's centre.</summary>
        private const float HoverHeight = 270f;

        /// <summary>The Twins sit either side of that spot, this far apart from it.</summary>
        private const float HoverSide = 190f;

        private const float HoverMaxSpeed = 14f;

        /// <summary>Speed asked for per pixel of distance, so the approach eases in.</summary>
        private const float HoverGain = 0.09f;

        /// <summary>How fast the velocity turns toward what is asked. Lower is heavier.</summary>
        private const float HoverAccel = 0.10f;

        private const float BobHeight = 10f;
        private const float BobRate = 0.05f;

        // ---- who does what -----------------------------------------------------------

        /// <summary>Flyers: rise to the spot above the player and wait there.</summary>
        private static readonly HashSet<int> Hovers = new()
        {
            NPCID.EyeofCthulhu, NPCID.Retinazer, NPCID.Spazmatism,
            NPCID.QueenBee, NPCID.DukeFishron, NPCID.HallowBoss,
        };

        /// <summary>Faces the player by rotation, the way vanilla turns an eye.</summary>
        private static readonly HashSet<int> Eyes = new()
        {
            NPCID.EyeofCthulhu, NPCID.Retinazer, NPCID.Spazmatism,
        };

        /// <summary>Faces the player by spriteDirection; the value is what vanilla sets it to for direction 1.</summary>
        private static readonly Dictionary<int, int> Facers = new()
        {
            { NPCID.QueenBee, 1 },
            { NPCID.DukeFishron, -1 },
        };

        /// <summary>Which side of the hover spot a type takes. Unlisted is dead centre.</summary>
        private static readonly Dictionary<int, float> Sides = new()
        {
            { NPCID.Retinazer, -1f },
            { NPCID.Spazmatism, 1f },
        };

        /// <summary>
        /// Not bosses in their own right, but they fight: they hold whenever any boss is
        /// waiting. The Moon Lord's head and hands carry the boss flag and are listed
        /// anyway, so they follow the core rather than keeping a clock of their own.
        /// </summary>
        private static readonly HashSet<int> Parts = new()
        {
            NPCID.ServantofCthulhu,
            NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail,
            NPCID.SkeletronHand,
            NPCID.PrimeCannon, NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeLaser,
            NPCID.TheDestroyerBody, NPCID.TheDestroyerTail, NPCID.Probe,
            NPCID.GolemHead, NPCID.GolemFistLeft, NPCID.GolemFistRight, NPCID.GolemHeadFree,
            NPCID.PlanterasHook, NPCID.PlanterasTentacle,
            NPCID.Creeper,
            NPCID.DetonatingBubble, NPCID.Sharkron, NPCID.Sharkron2,
            NPCID.MoonLordHead, NPCID.MoonLordHand, NPCID.MoonLordFreeEye, NPCID.MoonLordLeechBlob,
            NPCID.CultistBossClone,
            NPCID.CultistDragonHead, NPCID.CultistDragonBody1, NPCID.CultistDragonBody2,
            NPCID.CultistDragonBody3, NPCID.CultistDragonBody4, NPCID.CultistDragonTail,
            NPCID.AncientLight, NPCID.AncientDoom,
            NPCID.QueenSlimeMinionBlue, NPCID.QueenSlimeMinionPink, NPCID.QueenSlimeMinionPurple,
            NPCID.MartianSaucer, NPCID.MartianSaucerTurret, NPCID.MartianSaucerCannon,
        };

        /// <summary>Leads a fight without vanilla's boss flag.</summary>
        private static readonly HashSet<int> Heads = new()
        {
            NPCID.EaterofWorldsHead,
        };

        /// <summary>Leaders that keep their AI through the wait, slowed, instead of holding.</summary>
        private static readonly HashSet<int> Worms = new()
        {
            NPCID.EaterofWorldsHead, NPCID.TheDestroyer,
        };

        /// <summary>Their segments: they follow the head and must keep following.</summary>
        private static readonly HashSet<int> WormParts = new()
        {
            NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail,
            NPCID.TheDestroyerBody, NPCID.TheDestroyerTail,
        };

        /// <summary>Fastest a waiting worm moves, in pixels a tick. Enough to string out, not enough to arrive.</summary>
        private const float WormCrawl = 2.6f;

        /// <summary>Which leader a part belongs to, for telling whether a fight is still alive.</summary>
        private static readonly Dictionary<int, int> Family = new()
        {
            { NPCID.EaterofWorldsBody, NPCID.EaterofWorldsHead }, { NPCID.EaterofWorldsTail, NPCID.EaterofWorldsHead },
            { NPCID.TheDestroyerBody, NPCID.TheDestroyer }, { NPCID.TheDestroyerTail, NPCID.TheDestroyer },
            { NPCID.Probe, NPCID.TheDestroyer },
        };

        /// <summary>
        /// Leader types whose arrival has finished and whose fight is still alive. A
        /// leader of one of these types that appears now is a piece of that fight, not
        /// a new one, and gets no wait. Kept by BossArrivalWaitSystem.
        /// </summary>
        internal static readonly HashSet<int> Underway = new();

        // ---- state -------------------------------------------------------------------

        /// <summary>True on the tick after any boss was waiting. What the parts read.</summary>
        internal static bool AnyWaiting;

        /// <summary>AI ticks seen since the wait was decided (a part: since it began holding).</summary>
        private int ticks;

        /// <summary>Ticks of wait left; -1 until the first AI tick decides it. Runs from that tick, settling included.</summary>
        private int left = -1;

        /// <summary>dontTakeDamage is borrowed for the wait and given back after.</summary>
        private bool holding;
        private bool wasUntouchable;

        public bool Waiting => left > 0;

        /// <summary>
        /// For a leader: from its spawn until the camera is home. Waiting, or not yet
        /// ticked at all, when the length of its wait is still to be decided. (A part
        /// never decides one, so ask its leader.)
        /// </summary>
        public bool Arriving => left != 0;

        private static bool Leads(NPC npc) => (npc.boss || Heads.Contains(npc.type)) && !Parts.Contains(npc.type);

        /// <summary>The leader type a part belongs to; a leader is its own.</summary>
        internal static int LeaderOf(int type) => Family.TryGetValue(type, out int leader) ? leader : type;

        /// <summary>Part of a boss's fight without being flagged a boss: its parts, its minions, a worm's head and segments (the lists above).</summary>
        internal static bool OfAFight(int type) => Parts.Contains(type) || Heads.Contains(type) || WormParts.Contains(type);

        public override bool PreAI(NPC npc)
        {
            if (Parts.Contains(npc.type))
                return PartAI(npc);

            if (!Leads(npc))
                return true;

            if (left < 0)
            {
                left = Underway.Contains(npc.type) ? 0 : BossPlate.ArrivalTicks(npc);
                ticks = 0;
            }

            if (left == 0)
            {
                Underway.Add(npc.type);
                return true;
            }

            if (--left <= 0)
            {
                // Home. The boss gets this tick.
                Give(npc);
                Underway.Add(npc.type);
                return true;
            }

            if (Settling(npc))
            {
                ticks++;
                return true;
            }

            ticks++;
            Take(npc);

            // A worm keeps moving; PostAI reins it in.
            if (Worms.Contains(npc.type))
                return true;

            if (Hovers.Contains(npc.type) && Target(npc, out Player player))
                Hover(npc, player);
            else
                Hold(npc);

            return false;
        }

        /// <summary>Still building itself or fading in: its own AI keeps running.</summary>
        private bool Settling(NPC npc)
        {
            int setup = Setup.TryGetValue(npc.type, out int s) ? s : SetupDefault;

            if (ticks < setup)
                return true;

            return npc.alpha > 0 && ticks < SettleCap;
        }

        private bool PartAI(NPC npc)
        {
            if (!AnyWaiting)
            {
                Give(npc);
                ticks = 0;
                return true;
            }

            // A segment follows its head. Untouchable, but never still.
            if (WormParts.Contains(npc.type))
            {
                Take(npc);
                return true;
            }

            int setup = PartSetup.TryGetValue(npc.type, out int p) ? p : PartSetupDefault;

            if (ticks++ < setup)
                return true;

            Take(npc);
            Hold(npc);
            return false;
        }

        /// <summary>A waiting worm's head crawls: its own AI, capped.</summary>
        public override void PostAI(NPC npc)
        {
            if (!holding || !Worms.Contains(npc.type))
                return;

            float speed = npc.velocity.Length();

            if (speed > WormCrawl)
                npc.velocity *= WormCrawl / speed;
        }

        /// <summary>Nothing touches the player while its owner is being introduced.</summary>
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !holding;

        // ---- the two idles -----------------------------------------------------------

        private void Hover(NPC npc, Player player)
        {
            float side = Sides.TryGetValue(npc.type, out float s) ? s : 0f;
            float bob = MathF.Sin(ticks * BobRate) * BobHeight;

            Vector2 goal = player.Center + new Vector2(side * HoverSide, -HoverHeight + bob);
            Vector2 to = goal - npc.Center;
            float dist = to.Length();

            Vector2 want = dist < 1f ? Vector2.Zero : to / dist * Math.Min(HoverMaxSpeed, dist * HoverGain);

            npc.velocity = Vector2.Lerp(npc.velocity, want, HoverAccel);

            Face(npc, player);
        }

        /// <summary>Stands still. Gravity still applies to anything that has it.</summary>
        private static void Hold(NPC npc)
        {
            npc.velocity.X = 0f;

            if (npc.noGravity)
                npc.velocity.Y = 0f;
        }

        private static void Face(NPC npc, Player player)
        {
            Vector2 to = player.Center - npc.Center;

            if (Eyes.Contains(npc.type))
            {
                npc.rotation = to.ToRotation() - MathHelper.PiOver2;
                return;
            }

            if (to.X != 0f)
                npc.direction = to.X > 0f ? 1 : -1;

            if (Facers.TryGetValue(npc.type, out int sign))
                npc.spriteDirection = npc.direction * sign;
        }

        private static bool Target(NPC npc, out Player player)
        {
            npc.TargetClosest(false);

            player = npc.HasValidTarget && npc.HasPlayerTarget ? Main.player[npc.target] : null;

            return player != null;
        }

        // ---- untouchable, and back -------------------------------------------------

        private void Take(NPC npc)
        {
            if (holding)
                return;

            holding = true;
            wasUntouchable = npc.dontTakeDamage;
            npc.dontTakeDamage = true;
        }

        private void Give(NPC npc)
        {
            if (!holding)
                return;

            holding = false;
            npc.dontTakeDamage = wasUntouchable;
        }
    }


    /// <summary>
    /// Decides, once a tick before any NPC runs, whether some boss is waiting — so the
    /// parts all read one answer rather than whatever their owner's slot happened to
    /// say when they came round. A part therefore starts one tick after its owner.
    /// </summary>
    public class BossArrivalWaitSystem : ModSystem
    {
        private static readonly HashSet<int> alive = new();

        public override void PreUpdateNPCs()
        {
            bool any = false;
            alive.Clear();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.GetGlobalNPC<BossArrivalWait>().Waiting)
                    any = true;

                alive.Add(BossArrivalWait.LeaderOf(npc.type));
            }

            BossArrivalWait.AnyWaiting = any;

            // A fight is underway only while something of it is still here.
            BossArrivalWait.Underway.RemoveWhere(type => !alive.Contains(type));
        }

        public override void OnWorldLoad()
        {
            BossArrivalWait.AnyWaiting = false;
            BossArrivalWait.Underway.Clear();
        }

        public override void OnWorldUnload()
        {
            BossArrivalWait.AnyWaiting = false;
            BossArrivalWait.Underway.Clear();
        }
    }
}
