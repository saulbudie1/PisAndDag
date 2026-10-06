using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// Skeletron touches nothing while he is being introduced, in any world: not the
    /// player, not a townsperson, not with his head and not with his hands. From his
    /// spawn until the camera is home (BossArrivalWait.Arriving, the head's; the hands
    /// ask their head). Before this his hands, which are given a moment to fly out from
    /// his face, could strike on their way.
    /// </summary>
    public class SkeletronArrival : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
            entity.type == NPCID.SkeletronHead || entity.type == NPCID.SkeletronHand;

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !Arriving(npc);

        public override bool CanHitNPC(NPC npc, NPC target) => !Arriving(npc);

        internal static bool Arriving(NPC npc)
        {
            NPC head = npc.type == NPCID.SkeletronHead ? npc : LegendsSkeletronHand.HeadOf(npc);

            return head != null && head.GetGlobalNPC<BossArrivalWait>().Arriving;
        }
    }

    /// <summary>
    /// Skeletron in Legends: half again the life, a fifth again the damage, head, hands
    /// and skulls, and four moves of his own between vanilla's.
    ///
    /// Vanilla still runs him: the hover with its skulls and swinging hands, the spin, the
    /// day. Every seven seconds or so of hovering, and never with a spin close, the server
    /// gives him a move instead (Consider), and for its length his own AI or his hands'
    /// is set aside:
    ///
    ///     CLAP     both hands go to either side of the player and lock there; a second
    ///              to get out from between them; they meet
    ///     SMASH    both hands go up over the player, wide apart, and hold; they come
    ///              together over them, knuckles touching, and come down as one with twice
    ///              their weight; the ground they hit, level with the player's feet or
    ///              below, heaves as it does under the King Slime's slam
    ///     BREATH   he goes up over the player and opens his jaw; six skulls come out and
    ///              up; each draws the line it will take, and takes it
    ///     DIVE     his hands gone, and rarely: he circles out to a place off the player,
    ///              any side of them, from which a line through them meets something solid;
    ///              the path he will take is marked all the way to it; he spins, and
    ///              comes; whoever is in his way dies, and what he hits takes a fifteenth
    ///              of his life from him
    ///
    /// The server picks the move and where it is aimed (Mark) and sends them with its
    /// clock (SendExtraAI); every machine plays it from those. The hands read their head.
    /// </summary>
    public class LegendsSkeletron : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.SkeletronHead;

        public const float LifeScale = 1.5f;
        public const float DamageScale = 1.2f;

        internal enum Move : byte
        {
            None,
            Clap,
            Smash,
            Breath,
            Dive,
        }

        // ---- pacing ----------------------------------------------------------------

        /// <summary>Hovering ticks before his first move, and between moves, give or take.</summary>
        private const int FirstMove = 360;
        private const int MoveEvery = 420;
        private const int MoveJitter = 60;

        /// <summary>Vanilla spins when its hover count (ai[2]) reaches 800. No move is begun past this.</summary>
        private const float SpinSoon = 560f;

        /// <summary>The dive: at most once in this many ticks, and then one time in DiveChance.</summary>
        private const int DiveEvery = 1800;
        private const int DiveChance = 3;

        /// <summary>No move at a player further off than this.</summary>
        private const float MoveRange = 2000f;

        // ---- the clap ----------------------------------------------------------------

        internal const int ClapReach = 36;
        internal const int ClapLock = 60;
        internal const int ClapShut = 12;
        internal const int ClapRebound = 30;
        internal const int ClapTicks = ClapReach + ClapLock + ClapShut + ClapRebound;

        /// <summary>How far either side of the player the hands lock, and how far apart their middles are when they meet.</summary>
        internal const float ClapSpread = 230f;
        internal const float ClapMeet = 22f;

        // ---- the smash ---------------------------------------------------------------

        internal const int SmashRaise = 40;
        internal const int SmashHang = 24;
        internal const int SmashJoin = 14;
        internal const int SmashDrop = 44;
        internal const int SmashRest = 36;
        internal const int SmashTicks = SmashRaise + SmashHang + SmashJoin + SmashDrop + SmashRest;

        /// <summary>The tick the hands have come together, and the tick they are let fall.</summary>
        internal const int SmashJoined = SmashRaise + SmashHang + SmashJoin;

        /// <summary>
        /// Where the hands hang: this far either side of the player, and this high over them.
        /// Then how far either side of the player's middle each is once they have come
        /// together: half a hand (52 wide), so they touch.
        /// </summary>
        internal const float SmashSpread = 180f;
        internal const float SmashHeight = 330f;
        internal const float SmashTouch = 26f;

        // ---- the breath --------------------------------------------------------------

        private const int BreathRise = 50;
        private const int BreathOpen = 20;
        private const int Skulls = 6;
        private const int SkullEvery = 6;
        private const int BreathOut = Skulls * SkullEvery;
        private const int BreathClose = 24;
        private const int BreathTicks = BreathRise + BreathOpen + BreathOut + BreathClose;

        /// <summary>How high over the player he breathes from, and how far his jaw drops.</summary>
        private const float BreathHeight = 300f;
        private const float JawDrop = 16f;

        /// <summary>How fast the skulls come out, and across how much of the sky above him, each side of straight up.</summary>
        private const float SkullSpeed = 9f;
        private const float SkullFan = 1.05f;

        // ---- the dive ----------------------------------------------------------------

        private const int DiveRise = 60;
        private const int DiveMark = 75;
        private const int DiveFall = 45;
        private const int DiveRest = 60;
        private const int DiveTicks = DiveRise + DiveMark + DiveFall + DiveRest;

        /// <summary>How far off the player he dives from, whichever side.</summary>
        private const float DiveFrom = 520f;
        private const float DiveAccel = 2f;
        private const float DiveMaxFall = 38f;

        /// <summary>How far past the player his path is looked along for something to hit (fifty tiles), a step at a time.</summary>
        private const float DiveReach = 800f;
        private const float PathStep = 4f;

        /// <summary>Tries at a side to dive from before he settles for straight down.</summary>
        private const int DiveTries = 16;

        /// <summary>The share of his life the ground takes from him.</summary>
        private const int SelfDamageShare = 15;

        /// <summary>How wide the marked path is, against his own width.</summary>
        private const float PathWidth = 1.4f;

        /// <summary>The path's two colours, swung between: slowly as he winds up, fast as he comes.</summary>
        private static readonly Color PathRed = new Color(255, 45, 35);
        private static readonly Color PathOrange = new Color(255, 150, 30);

        // ---- state -----------------------------------------------------------------------

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>The move under way, how far in, and where it is aimed: the server's, sent.</summary>
        internal Move move;
        internal int moveTimer;
        internal Vector2 mark;

        /// <summary>Counts every move begun, so a hand can tell a new one from the last.</summary>
        internal int serial;

        /// <summary>The server's countdowns: to the next move, and until a dive may come again.</summary>
        private int nextMove = FirstMove;
        private int diveWait;
        private Move last;

        /// <summary>
        /// The dive's line: the way he goes along it (the server's, sent as an angle). Then,
        /// as this machine found it from the mark: how far past the mark his middle stops,
        /// whether it is against something (or only the end of the path, in the air),
        /// where his rim meets it and which way its face looks. And whether he is there.
        /// </summary>
        private float diveAngle = MathHelper.PiOver2;
        private bool pathFound;
        private float stop;
        private bool strikes;
        private Vector2 contact;
        private Vector2 facing;
        private bool landed;

        /// <summary>Where he was, off the player, when the dive began: the arc he circles out along starts there.</summary>
        private bool riseKnown;
        private float riseAngle;
        private float riseDistance;

        /// <summary>Whether this machine has shown the hands' blow in the smash under way: the first hand down shows it for both.</summary>
        internal bool smashStruck;

        /// <summary>
        /// The move (its <see cref="serial"/>) a parry broke the hands' blow in, where the
        /// world is decided: kept here as well as on the hands, because the hand that was
        /// parried may not have lived through it (LegendsSkeletronHand.Break, Broken).
        /// </summary>
        internal int brokenIn = -1;

        /// <summary>The last stage of the move under way this machine has made a sound for.</summary>
        private int heard;

        internal static Asset<Texture2D> HeadTexture;
        internal static Asset<Texture2D> JawTexture;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            HeadTexture = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Legends/SkeletronHead");
            JawTexture = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Legends/SkeletronJaw");
        }

        public override void Unload()
        {
            HeadTexture = null;
            JawTexture = null;
        }

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            entity.lifeMax = (int)(entity.lifeMax * LifeScale);
            entity.damage = (int)(entity.damage * DamageScale);
        }

        /// <summary>How many of his hands are still on him.</summary>
        internal static int Hands(NPC head)
        {
            int count = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (npc.active && npc.type == NPCID.SkeletronHand && (int)npc.ai[1] == head.whoAmI)
                    count++;
            }

            return count;
        }

        // ---- the fight ---------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (!PADWorld.Legends || SkeletronArrival.Arriving(npc))
                return true;

            if (Authority && diveWait > 0)
                diveWait--;

            if (move == Move.None)
            {
                if (Authority)
                    Consider(npc);

                if (move == Move.None)
                    return true;
            }

            moveTimer++;

            switch (move)
            {
                case Move.Clap:
                    Clap(npc);
                    return true;

                case Move.Smash:
                    Smash(npc);
                    return true;

                case Move.Breath:
                    Breath(npc);
                    return false;

                case Move.Dive:
                    Dive(npc);
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The server's: while vanilla has him hovering and no spin is near, counts down,
        /// then picks a move. Both hands on: the clap, the smash or the breath, never the
        /// same twice running. Otherwise the breath, and with no hands at all, now and
        /// then the dive.
        /// </summary>
        private void Consider(NPC npc)
        {
            if (npc.ai[1] != 0f || npc.ai[2] > SpinSoon)
                return;

            Player target = Main.player[npc.target];

            if (!target.active || target.dead || Vector2.Distance(target.Center, npc.Center) > MoveRange)
                return;

            if (--nextMove > 0)
                return;

            int hands = Hands(npc);
            Move pick;

            if (hands >= 2)
            {
                pick = (Move)(Main.rand.Next(3) + (int)Move.Clap);

                if (pick == last)
                    pick = (Move)((int)Move.Clap + ((int)pick - (int)Move.Clap + 1 + Main.rand.Next(2)) % 3);
            }
            else if (hands == 0 && diveWait <= 0 && last != Move.Dive && Main.rand.NextBool(DiveChance))
            {
                pick = Move.Dive;
                diveWait = DiveEvery;
            }
            else
            {
                pick = Move.Breath;
            }

            Begin(npc, pick, target);
        }

        private void Begin(NPC npc, Move pick, Player target)
        {
            move = pick;
            last = pick;
            moveTimer = 0;
            mark = target.Center;
            serial++;
            Fresh();

            if (pick == Move.Dive)
                diveAngle = ChooseWay(npc, target);

            npc.netUpdate = true;
        }

        /// <summary>What each machine keeps for itself about a move, cleared for a new one.</summary>
        private void Fresh()
        {
            heard = 0;
            landed = false;
            pathFound = false;
            riseKnown = false;
            smashStruck = false;
        }

        /// <summary>Over: vanilla has him again from here.</summary>
        private void End(NPC npc)
        {
            move = Move.None;
            moveTimer = 0;

            if (!Authority)
                return;

            nextMove = MoveEvery + Main.rand.Next(-MoveJitter, MoveJitter + 1);
            npc.netUpdate = true;
        }

        /// <summary>The server fixes where a move is aimed, once, and sends it; the others fix their own in the meantime.</summary>
        private void Fix(NPC npc, Player target)
        {
            mark = target.Center;

            if (Authority)
                npc.netUpdate = true;
        }

        private Player Target(NPC npc) => Main.player[npc.target];

        // ---- the clap: the hands move themselves (LegendsSkeletronHand) -------------------

        private void Clap(NPC npc)
        {
            if (moveTimer == ClapReach)
                Fix(npc, Target(npc));

            if (!Main.dedServ)
            {
                if (heard < 1 && moveTimer >= ClapReach)
                {
                    heard = 1;
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.6f, Volume = 1f }, mark);
                }

                // The line they will close along, in bone dust, while there is time to leave it.
                if (moveTimer >= ClapReach && moveTimer < ClapReach + ClapLock && moveTimer % 3 == 0)
                {
                    Vector2 at = mark + new Vector2(Main.rand.NextFloat(-ClapSpread, ClapSpread), 0f);
                    Dust dust = Dust.NewDustPerfect(at, DustID.Bone, Vector2.Zero, 100, default, 1.1f);
                    dust.noGravity = true;
                }

                if (heard < 2 && moveTimer >= ClapReach + ClapLock + ClapShut)
                {
                    heard = 2;

                    // Not of a clap a parry has broken: the hands never met.
                    if (!LegendsSkeletronHand.Broken(npc))
                    {
                        Impact(mark, 10f, 22);

                        for (int i = 0; i < 26; i++)
                        {
                            Dust.NewDustPerfect(mark + Main.rand.NextVector2Circular(20f, 30f), DustID.Bone,
                                Main.rand.NextVector2Circular(6f, 6f), 0, default, Main.rand.NextFloat(1f, 1.6f));
                        }
                    }
                }
            }

            if (moveTimer >= ClapTicks)
                End(npc);
        }

        // ---- the smash: the hands move themselves too ------------------------------------

        private void Smash(NPC npc)
        {
            if (moveTimer == SmashRaise)
                Fix(npc, Target(npc));

            if (!Main.dedServ && heard < 1 && moveTimer >= SmashRaise)
            {
                heard = 1;
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.5f, Volume = 1f }, mark);
            }

            // Knuckles meeting over them: the knock, and bone chipped off where they touch.
            if (!Main.dedServ && heard < 2 && moveTimer >= SmashJoined)
            {
                heard = 2;

                Vector2 meet = mark + new Vector2(0f, -SmashHeight);

                SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = 0.1f, Volume = 1f }, meet);

                for (int i = 0; i < 12; i++)
                {
                    Dust.NewDustPerfect(meet + Main.rand.NextVector2Circular(8f, 20f), DustID.Bone,
                        Main.rand.NextVector2Circular(4f, 4f), 0, default, Main.rand.NextFloat(1f, 1.4f));
                }
            }

            if (moveTimer >= SmashTicks)
                End(npc);
        }

        // ---- the breath ------------------------------------------------------------------------

        private void Breath(NPC npc)
        {
            Player target = Target(npc);

            if (Authority && (!target.active || target.dead))
            {
                End(npc);
                return;
            }

            int t = moveTimer;

            // Up over them, and held there, following, while he breathes.
            if (t < BreathRise + BreathOpen + BreathOut)
                Approach(npc, target.Center + new Vector2(0f, -BreathHeight), 0.12f, 16f);
            else
                npc.velocity *= 0.9f;

            npc.rotation *= 0.85f;

            if (!Main.dedServ && heard < 1 && t >= BreathRise)
            {
                heard = 1;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 0.8f }, npc.Center);
            }

            int breathed = t - BreathRise - BreathOpen;

            if (breathed >= 0 && breathed < BreathOut && breathed % SkullEvery == 0)
                Exhale(npc, breathed / SkullEvery);

            if (t >= BreathTicks)
                End(npc);
        }

        /// <summary>How open his jaw is, 0 to 1.</summary>
        private float JawOpen
        {
            get
            {
                if (move != Move.Breath)
                    return 0f;

                int t = moveTimer;

                if (t < BreathRise)
                    return 0f;

                if (t < BreathRise + BreathOpen)
                    return Ease((t - BreathRise) / (float)BreathOpen);

                if (t < BreathRise + BreathOpen + BreathOut)
                    return 1f;

                return 1f - Ease((t - BreathRise - BreathOpen - BreathOut) / (float)BreathClose);
            }
        }

        /// <summary>His mouth, where the jaw parts from the skull, in the world.</summary>
        private static Vector2 Mouth(NPC npc) => npc.Center + new Vector2(0f, 38f * npc.scale).RotatedBy(npc.rotation);

        /// <summary>One skull out of his mouth and up, fanned across the sky over him: the server's to make.</summary>
        private static void Exhale(NPC npc, int index)
        {
            Vector2 mouth = Mouth(npc);

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.2f, Volume = 0.8f, MaxInstances = 6 }, mouth);

                for (int i = 0; i < 6; i++)
                {
                    Dust dust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(10f, 6f), DustID.Bone,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f)), 80, default, 1.1f);
                    dust.noGravity = true;
                }
            }

            if (!Authority)
                return;

            float angle = -MathHelper.PiOver2 + MathHelper.Lerp(-SkullFan, SkullFan, index / (float)(Skulls - 1));
            int damage = (int)(npc.GetAttackDamage_ForProjectiles(17f, 17f) * DamageScale);

            Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, angle.ToRotationVector2() * SkullSpeed,
                ModContent.ProjectileType<SkeletronBreathSkull>(), damage, 0f, Main.myPlayer, index);
        }

        // ---- the dive ------------------------------------------------------------------------

        private void Dive(NPC npc)
        {
            Player target = Target(npc);
            int t = moveTimer;
            Vector2 way = diveAngle.ToRotationVector2();

            if (t < DiveRise)
            {
                if (Authority && (!target.active || target.dead))
                {
                    End(npc);
                    return;
                }

                // Round the player to where he dives from, along an arc about them, never
                // through them; out or in to that distance as he goes.
                if (!riseKnown)
                {
                    riseKnown = true;

                    Vector2 off = npc.Center - target.Center;

                    riseAngle = off == Vector2.Zero ? -MathHelper.PiOver2 : off.ToRotation();
                    riseDistance = MathHelper.Clamp(off.Length(), 200f, 900f);
                }

                float p = Ease(t / (float)DiveRise);
                float from = (-way).ToRotation();
                float angle = riseAngle + MathHelper.WrapAngle(from - riseAngle) * p;
                float distance = MathHelper.Lerp(riseDistance, DiveFrom, p);

                Approach(npc, target.Center + angle.ToRotationVector2() * distance, 0.3f, 48f);
                npc.rotation *= 0.9f;
            }
            else if (t < DiveRise + DiveMark)
            {
                if (t == DiveRise)
                {
                    Fix(npc, target);
                    Found(npc);
                }

                // A machine told of the dive late finds the path the first tick it can.
                if (!pathFound)
                    Found(npc);

                // Where he will come from, fixed, and turning faster and faster.
                Place(npc, mark - way * DiveFrom);

                float wind = (t - DiveRise) / (float)DiveMark;
                npc.rotation += MathHelper.Lerp(0.05f, 0.6f, wind * wind);

                if (!Main.dedServ)
                {
                    if (heard < 1)
                    {
                        heard = 1;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1f }, npc.Center);
                    }

                    if ((t - DiveRise) % 8 == 0)
                        SoundEngine.PlaySound(SoundID.Item7 with { Pitch = -0.5f + wind, Volume = 0.7f, MaxInstances = 3 }, npc.Center);
                }
            }
            else if (t < DiveRise + DiveMark + DiveFall && !landed)
            {
                if (!pathFound)
                    Found(npc);

                npc.rotation += 0.6f;

                // Down the line, faster each tick, and held to it: whatever took him off it
                // is taken back.
                Vector2 start = mark - way * DiveFrom;
                Vector2 off = npc.Center - start;
                float along = Vector2.Dot(off, way);
                Vector2 drift = off - way * along;
                float speed = Math.Min(Math.Max(0f, Vector2.Dot(npc.velocity, way)) + DiveAccel, DiveMaxFall);
                float end = DiveFrom + stop;

                if (along + speed >= end)
                {
                    speed = Math.Max(0f, end - along);
                    landed = true;
                }

                npc.velocity = way * speed - drift;

                // Whoever is in his way, on their own machine: no hit, no immunity, dead.
                if (!Main.dedServ)
                    Crush(npc);

                // Against something: it heaves, and takes its share of him. At the end of
                // an empty path he only stops.
                if (landed && strikes)
                    Landing(npc);
            }
            else
            {
                // In it, and still.
                npc.velocity = Vector2.Zero;
                npc.rotation *= 0.8f;
            }

            if (t >= DiveTicks)
                End(npc);
        }

        /// <summary>His size, as the ball he is taken for when the path is found: half his width.</summary>
        private static float Radius(NPC npc) => npc.width * 0.5f;

        /// <summary>The path from the mark along his way, found on this machine.</summary>
        private void Found(NPC npc)
        {
            pathFound = true;
            strikes = FindPath(mark, diveAngle.ToRotationVector2(), Radius(npc), out stop, out contact, out facing);
        }

        /// <summary>
        /// The server's: which way he dives, as an angle. Any side of the player, the sides
        /// tried at random, the first taken from which a line through them meets something
        /// solid within DiveReach past them. None: straight down, as before.
        /// </summary>
        private static float ChooseWay(NPC npc, Player target)
        {
            for (int i = 0; i < DiveTries; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 way = angle.ToRotationVector2();
                Vector2 start = target.Center - way * DiveFrom;

                if (!WorldGen.InWorld((int)(start.X / 16f), (int)(start.Y / 16f), 10))
                    continue;

                if (FindPath(target.Center, way, Radius(npc), out _, out _, out _))
                    return angle;
            }

            return MathHelper.PiOver2;
        }

        /// <summary>
        /// Along <paramref name="way"/> from the player at <paramref name="mark"/>, where
        /// his middle stops: taken for a ball of <paramref name="radius"/>, stepped along the
        /// line from a little short of the mark, stopped the step before it first touches
        /// something solid once it has been clear. Whatever it passes through to reach the
        /// player does not stop it; he goes through blocks, and the blow is aimed at them.
        /// Never clear round the player (wedged in a tunnel): he stops on them. Nothing to
        /// hit within DiveReach past them: false, and he stops at the end of the path.
        /// </summary>
        /// <param name="stop">How far past the mark his middle stops.</param>
        /// <param name="contact">Where his rim meets what he hits, on its face.</param>
        /// <param name="facing">Which way that face looks, out of it.</param>
        internal static bool FindPath(Vector2 mark, Vector2 way, float radius, out float stop, out Vector2 contact, out Vector2 facing)
        {
            bool clear = false;
            float lastClear = 0f;

            for (float d = -radius; d <= DiveReach; d += PathStep)
            {
                Vector2 centre = mark + way * d;

                if (!WorldGen.InWorld((int)(centre.X / 16f), (int)(centre.Y / 16f), 4))
                    break;

                if (!Touching(centre, radius, way, out Vector2 near))
                {
                    clear = true;
                    lastClear = d;
                    continue;
                }

                if (clear)
                {
                    stop = lastClear;
                    contact = near;
                    facing = (centre - near).SafeNormalize(-way);
                    return true;
                }

                if (d >= radius)
                {
                    Touching(mark, radius, way, out near);

                    stop = 0f;
                    contact = near;
                    facing = (mark - near).SafeNormalize(-way);
                    return true;
                }
            }

            stop = DiveReach;
            contact = mark + way * (DiveReach + radius);
            facing = -way;
            return false;
        }

        /// <summary>
        /// Whether a ball of <paramref name="radius"/> at <paramref name="centre"/> touches
        /// a solid tile, and the nearest point of one to its middle. Platforms and the like
        /// only from above, coming down onto them.
        /// </summary>
        private static bool Touching(Vector2 centre, float radius, Vector2 way, out Vector2 near)
        {
            int left = (int)((centre.X - radius) / 16f);
            int right = (int)((centre.X + radius) / 16f);
            int top = (int)((centre.Y - radius) / 16f);
            int bottom = (int)((centre.Y + radius) / 16f);

            float best = radius * radius;
            bool touching = false;
            near = centre;

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 2))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                        continue;

                    float tileTop = y * 16f + (tile.IsHalfBlock ? 8f : 0f);
                    float tileBottom = y * 16f + 16f;

                    if (Main.tileSolidTop[tile.TileType])
                    {
                        // A floor to stand on and nothing more: only its top, only from above.
                        if (way.Y <= 0f || centre.Y >= tileTop)
                            continue;

                        tileBottom = tileTop;
                    }

                    Vector2 point = new Vector2(
                        MathHelper.Clamp(centre.X, x * 16f, x * 16f + 16f),
                        MathHelper.Clamp(centre.Y, tileTop, tileBottom));

                    float distance = Vector2.DistanceSquared(centre, point);

                    if (distance < best)
                    {
                        best = distance;
                        near = point;
                        touching = true;
                    }
                }
            }

            return touching;
        }

        /// <summary>The local player, if the skull's path this tick covers them.</summary>
        private static void Crush(NPC npc)
        {
            Player me = Main.LocalPlayer;

            if (!me.active || me.dead || me.ghost)
                return;

            float ignored = 0f;

            bool caught = npc.Hitbox.Intersects(me.Hitbox)
                || Collision.CheckAABBvLineCollision(me.position, new Vector2(me.width, me.height), npc.Center,
                    npc.Center + npc.velocity, npc.width, ref ignored);

            if (!caught)
                return;

            me.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.DeathText.UnderTheSkull", me.name)),
                9999.0, npc.direction, false);
        }

        /// <summary>He hits it: it heaves, floor, wall or ceiling, and it takes its share of him (the server's to take).</summary>
        private void Landing(NPC npc)
        {
            if (!Main.dedServ)
            {
                float half = npc.width * 0.5f;

                if (facing.Y < 0f && Math.Abs(facing.Y) >= Math.Abs(facing.X))
                {
                    GroundWave.Slam(contact, half, 44, 28f, 40);
                    LegendsKingSlime.GroundDust(contact.X - half, contact.X + half, contact.Y, 1f);
                }
                else
                {
                    GroundWave.Strike(contact, facing, half, 44, 28f, 40);
                }

                Impact(contact, 16f, 40);
                SoundEngine.PlaySound(SoundID.NPCDeath2 with { Pitch = -0.3f, Volume = 1f }, contact);

                Vector2 across = new Vector2(-facing.Y, facing.X);

                for (int i = 0; i < 40; i++)
                {
                    Dust.NewDustPerfect(contact + across * Main.rand.NextFloat(-half, half) + facing * Main.rand.NextFloat(0f, 30f),
                        DustID.Bone, across * Main.rand.NextFloat(-7f, 7f) + facing * Main.rand.NextFloat(2f, 8f), 0, default,
                        Main.rand.NextFloat(1.1f, 1.8f));
                }
            }

            if (!Authority)
                return;

            NPC.HitInfo hit = new NPC.HitInfo
            {
                Damage = Math.Max(1, npc.lifeMax / SelfDamageShare),
                HitDirection = 0,
            };

            npc.StrikeNPC(hit, false, true);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendStrikeNPC(npc, in hit, -1);
        }

        // ---- shared ------------------------------------------------------------------------

        /// <summary>
        /// Where a blow aimed at the player who was at <paramref name="mark"/> comes down:
        /// the ground level with their feet or below, in the columns within
        /// <paramref name="half"/> of <paramref name="x"/>.
        /// </summary>
        internal static float FloorUnder(float x, float half, Vector2 mark) => FloorBelow(x, half, mark.Y + 17f);

        /// <summary>
        /// The top of the first ground at or below <paramref name="from"/> (world y), in the
        /// columns within <paramref name="half"/> of <paramref name="x"/>: a tile anything can
        /// stand on, platforms included. None within fifty tiles: never.
        /// </summary>
        internal static float FloorBelow(float x, float half, float from)
        {
            int left = (int)((x - half) / 16f);
            int right = (int)((x + half) / 16f);
            int top = (int)(from / 16f);

            for (int y = top; y < top + 50; y++)
            {
                for (int i = left; i <= right; i++)
                {
                    if (!WorldGen.InWorld(i, y, 2))
                        continue;

                    Tile tile = Main.tile[i, y];

                    if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        return y * 16f + (tile.IsHalfBlock ? 8f : 0f);
                }
            }

            return float.PositiveInfinity;
        }

        /// <summary>A blow landing: its sound, and the screen shaken for a player near enough to feel it.</summary>
        internal static void Impact(Vector2 at, float shake, int shakeTicks)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.45f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.9f, Volume = 1f }, at);

            float near = 1f - MathHelper.Clamp((Vector2.Distance(Main.LocalPlayer.Center, at) - 600f) / 1200f, 0f, 1f);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(shake * near, shakeTicks);
        }

        internal static void Approach(NPC npc, Vector2 goal, float gain, float most)
        {
            Vector2 want = (goal - npc.Center) * gain;

            if (want.Length() > most)
                want = want.SafeNormalize(Vector2.Zero) * most;

            npc.velocity = want;
        }

        /// <summary>
        /// There on the next tick. Handed to velocity, not written into position, because
        /// the move after the AI would then carry it on by whatever velocity was left.
        /// </summary>
        internal static void Place(NPC npc, Vector2 goal) => npc.velocity = goal - npc.Center;

        internal static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // ---- drawn -----------------------------------------------------------------------------

        /// <summary>
        /// The dive's path, under him. And his jaw: while it is open he is drawn here, the
        /// skull without it (SkeletronHead) and the jaw dropped below (SkeletronJaw), both
        /// on vanilla's frame and placed as vanilla places it, so shut, it is vanilla.
        /// </summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!PADWorld.Legends)
                return true;

            if (move == Move.Dive && moveTimer >= DiveRise && !landed && pathFound)
                DrawPath(npc, spriteBatch, screenPos);

            float open = JawOpen;

            if (open <= 0f || npc.frame.Y != 0 || HeadTexture == null || JawTexture == null || !HeadTexture.IsLoaded || !JawTexture.IsLoaded)
                return true;

            Main.instance.LoadNPC(npc.type);

            Texture2D head = HeadTexture.Value;
            Texture2D jaw = JawTexture.Value;

            // Vanilla's placement for him (Main.DrawNPCDirect_Inner): the frame's middle at his
            // middle across and four under his feet less half a frame down, turned about it.
            int frameHeight = TextureAssets.Npc[npc.type].Value.Height / Main.npcFrameCount[npc.type];
            Vector2 origin = new Vector2(head.Width / 2, frameHeight / 2);
            Vector2 at = new Vector2(npc.position.X + npc.width / 2,
                npc.position.Y + npc.height - frameHeight * npc.scale + 4f + origin.Y * npc.scale + npc.gfxOffY) - screenPos;

            Color color = npc.GetAlpha(npc.GetNPCColorTintedByBuffs(drawColor));
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Rectangle frame = new Rectangle(0, 0, head.Width, frameHeight);

            spriteBatch.Draw(head, at, frame, color, npc.rotation, origin, npc.scale, flip, 0f);

            Vector2 drop = new Vector2(0f, JawDrop * open * npc.scale).RotatedBy(npc.rotation);

            spriteBatch.Draw(jaw, at + drop, frame, color, npc.rotation, origin, npc.scale, flip, 0f);

            return false;
        }

        /// <summary>
        /// The band he will come down, from him to what he hits (or the end of an empty path):
        /// see-through, its colour swinging between red and orange, slowly as he winds up and
        /// faster as the fall comes, fastest while he falls.
        /// </summary>
        private void DrawPath(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 way = diveAngle.ToRotationVector2();

            Vector2 start = npc.Center;
            Vector2 end = mark + way * (stop + (strikes ? Radius(npc) : 0f));
            float length = Vector2.Dot(end - start, way);

            if (length <= 0f)
                return;

            // The swing's phase: its rate climbs from 0.15 to 0.6 a tick over the wind-up,
            // then 0.9 while he falls, summed so the colour never jumps.
            int t = moveTimer - DiveRise;
            float phase = t < DiveMark
                ? 0.15f * t + 0.45f * t * t / (2f * DiveMark)
                : 0.15f * DiveMark + 0.45f * DiveMark / 2f + 0.9f * (t - DiveMark);
            float swing = 0.5f + 0.5f * (float)Math.Sin(phase);
            float fade = Ease(t / 12f);

            Color hue = Color.Lerp(PathRed, PathOrange, swing);
            Color edge = hue * ((0.18f + 0.12f * swing) * fade);
            Color fill = hue * ((0.05f + 0.04f * swing) * fade);

            float width = npc.width * PathWidth;
            float rotation = way.ToRotation();
            Vector2 across = new Vector2(-way.Y, way.X) * (width * 0.5f - 1f);
            Rectangle dot = new Rectangle(0, 0, 1, 1);
            Vector2 middle = new Vector2(0f, 0.5f);

            // MagicPixel's first pixel, stretched along the line: the band, and a line down each side.
            spriteBatch.Draw(pixel, start - screenPos, dot, fill, rotation, middle, new Vector2(length, width), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, start + across - screenPos, dot, edge, rotation, middle, new Vector2(length, 2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, start - across - screenPos, dot, edge, rotation, middle, new Vector2(length, 2f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Harmless while he gets into place for a dive: the blow is the dive, not his skull
        /// passing over the player on its way round them.
        /// </summary>
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) =>
            !PADWorld.Legends || move != Move.Dive || moveTimer >= DiveRise + DiveMark;

        // ---- sync --------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)move);
            binaryWriter.Write((short)Math.Min(moveTimer, short.MaxValue));
            binaryWriter.WriteVector2(mark);
            binaryWriter.Write(serial);
            binaryWriter.Write(diveAngle);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            Move got = (Move)binaryReader.ReadByte();
            int timer = binaryReader.ReadInt16();
            Vector2 at = binaryReader.ReadVector2();
            int number = binaryReader.ReadInt32();
            float angle = binaryReader.ReadSingle();

            if (number != serial)
            {
                serial = number;
                move = got;
                moveTimer = timer;
                Fresh();
            }
            else if (got != move || Math.Abs(timer - moveTimer) > 4)
            {
                move = got;
                moveTimer = timer;
            }

            mark = at;
            diveAngle = angle;

            // The path of a dive, found again from the server's mark and way.
            if (move == Move.Dive && moveTimer >= DiveRise && !landed)
                Found(npc);
        }
    }

    /// <summary>
    /// Skeletron's hands in Legends: his numbers, and his two moves that are theirs
    /// (LegendsSkeletron: the clap and the smash), played from their head's move and clock.
    /// Given back to vanilla after, idle, their swing counted from nothing. While they are
    /// out further than their arms reach, the arms are drawn out to meet them (FillArm).
    ///
    /// A hand parried in the blow of one of those moves (SkeletronHandParry, which asks
    /// <see cref="InMove"/> and <see cref="Striking"/>) is out of the move: it is thrown
    /// back, and this moves it no more until the move is over. The other goes with it, and
    /// what the blow would have shown and sounded as it landed is left out
    /// (<see cref="Broken"/>).
    /// </summary>
    public class LegendsSkeletronHand : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.SkeletronHand;

        /// <summary>Whether a move has them, and which one (the head's serial).</summary>
        private bool held;
        private int seen = -1;

        /// <summary>This hand's ground for a smash, and whether it has hit it.</summary>
        private float floorY = float.PositiveInfinity;
        private bool landed;

        private const float SmashAccel = 1.8f;
        private const float SmashMaxFall = 28f;

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            entity.lifeMax = (int)(entity.lifeMax * LegendsSkeletron.LifeScale);
            entity.damage = (int)(entity.damage * LegendsSkeletron.DamageScale);
        }

        /// <summary>The head a hand belongs to (vanilla keeps its index in ai[1]), or null.</summary>
        internal static NPC HeadOf(NPC hand)
        {
            int index = (int)hand.ai[1];

            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC head = Main.npc[index];

            return head.active && head.type == NPCID.SkeletronHead ? head : null;
        }

        /// <summary>
        /// Whether a move of Legends has this hand (its head's clap or smash), and which: the
        /// head's count of its moves. Read off the head, as every machine has it.
        /// </summary>
        internal static bool InMove(NPC hand, out int serial)
        {
            serial = -1;

            if (!PADWorld.Legends)
                return false;

            NPC head = HeadOf(hand);

            if (head == null || !head.TryGetGlobalNPC(out LegendsSkeletron state))
                return false;

            if (state.move != LegendsSkeletron.Move.Clap && state.move != LegendsSkeletron.Move.Smash)
                return false;

            serial = state.serial;
            return true;
        }

        /// <summary>
        /// Whether that move is in its blow for this hand: the clap as the hands shut, the
        /// smash as they come down and until this one is down. Not their way to their places,
        /// the wait there, their coming together over the player, or anything after.
        /// </summary>
        internal static bool Striking(NPC hand)
        {
            if (!PADWorld.Legends)
                return false;

            NPC head = HeadOf(hand);

            if (head == null || !head.TryGetGlobalNPC(out LegendsSkeletron state))
                return false;

            int t = state.moveTimer;

            switch (state.move)
            {
                case LegendsSkeletron.Move.Clap:
                    return t >= LegendsSkeletron.ClapReach + LegendsSkeletron.ClapLock
                        && t < LegendsSkeletron.ClapReach + LegendsSkeletron.ClapLock + LegendsSkeletron.ClapShut;

                case LegendsSkeletron.Move.Smash:
                    return t >= LegendsSkeletron.SmashJoined && t < LegendsSkeletron.SmashJoined + LegendsSkeletron.SmashDrop
                        && !(hand.TryGetGlobalNPC(out LegendsSkeletronHand own) && own.landed);

                default:
                    return false;
            }
        }

        /// <summary>
        /// A hand of his has been parried in the blow of the move that has it, where the world
        /// is decided: its head keeps that too (the hand may be broken by it).
        /// </summary>
        internal static void Break(NPC hand)
        {
            NPC head = HeadOf(hand);

            if (head != null && head.TryGetGlobalNPC(out LegendsSkeletron state))
                state.brokenIn = state.serial;
        }

        /// <summary>
        /// Whether a parry has broken the blow of the move that has this head's hands, as this
        /// machine knows it: his head was told so (<see cref="Break"/>), a hand of his is out
        /// of the move for it, or one was turned aside by this machine's own player a moment
        /// ago (a client knows of its own player's parry before the server has said what came
        /// of it, and the hands go on as they were until then). What the blow would have
        /// shown and sounded is left out. Nothing on a server.
        /// </summary>
        internal static bool Broken(NPC head)
        {
            if (Main.dedServ || head == null)
                return false;

            if (head.TryGetGlobalNPC(out LegendsSkeletron state) && state.brokenIn == state.serial)
                return true;

            ParryPlayer parry = Main.LocalPlayer.GetModPlayer<ParryPlayer>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC hand = Main.npc[i];

                if (!hand.active || hand.type != NPCID.SkeletronHand || (int)hand.ai[1] != head.whoAmI)
                    continue;

                if (SkeletronHandParry.Spent(hand) || parry.Spares(hand))
                    return true;
            }

            return false;
        }

        public override bool PreAI(NPC npc)
        {
            if (!PADWorld.Legends)
                return true;

            NPC head = HeadOf(npc);
            LegendsSkeletron state = head?.GetGlobalNPC<LegendsSkeletron>();

            bool ours = state != null
                && (state.move == LegendsSkeletron.Move.Clap || state.move == LegendsSkeletron.Move.Smash);

            if (!ours)
            {
                if (held)
                {
                    // Idle, the swing counted from nothing: straight back to hanging off him.
                    held = false;
                    npc.ai[2] = 0f;
                    npc.ai[3] = 0f;
                    npc.netUpdate = true;
                }

                return true;
            }

            held = true;

            if (seen != state.serial)
            {
                seen = state.serial;
                landed = false;
                floorY = float.PositiveInfinity;
            }

            // Parried out of this move: thrown back, and no further part in it (SkeletronHandParry moves it).
            if (SkeletronHandParry.Spent(npc))
                return false;

            if (state.move == LegendsSkeletron.Move.Clap)
                Clap(npc, head, state);
            else
                Smash(npc, head, state);

            return false;
        }

        /// <summary>Which side of the player this hand takes: the side of him it hangs on (vanilla puts ai[0] -1 on his right).</summary>
        private static float Side(NPC hand) => hand.ai[0] < 0f ? 1f : -1f;

        private static void Clap(NPC npc, NPC head, LegendsSkeletron state)
        {
            float side = Side(npc);
            int t = state.moveTimer;
            Player target = Main.player[head.target];

            // Palms in, toward the middle.
            npc.rotation = npc.rotation.AngleLerp(side * MathHelper.PiOver2, 0.2f);

            if (t < LegendsSkeletron.ClapReach)
            {
                LegendsSkeletron.Approach(npc, target.Center + new Vector2(side * LegendsSkeletron.ClapSpread, 0f), 0.2f, 20f);
                return;
            }

            if (t < LegendsSkeletron.ClapReach + LegendsSkeletron.ClapLock)
            {
                // Locked on either side of where they were; trembling, harder as it comes.
                float wait = (t - LegendsSkeletron.ClapReach) / (float)LegendsSkeletron.ClapLock;
                Vector2 shake = Main.rand.NextVector2Circular(1f, 1f) * (1f + 3f * wait);

                LegendsSkeletron.Place(npc, state.mark + new Vector2(side * LegendsSkeletron.ClapSpread, 0f) + shake);
                return;
            }

            if (t < LegendsSkeletron.ClapReach + LegendsSkeletron.ClapLock + LegendsSkeletron.ClapShut)
            {
                float p = (t - LegendsSkeletron.ClapReach - LegendsSkeletron.ClapLock + 1) / (float)LegendsSkeletron.ClapShut;
                float apart = MathHelper.Lerp(LegendsSkeletron.ClapSpread, LegendsSkeletron.ClapMeet, p * p * p);

                LegendsSkeletron.Place(npc, state.mark + new Vector2(side * apart, 0f));
                return;
            }

            float back = LegendsSkeletron.Ease((t - LegendsSkeletron.ClapReach - LegendsSkeletron.ClapLock - LegendsSkeletron.ClapShut)
                / (float)LegendsSkeletron.ClapRebound);

            LegendsSkeletron.Place(npc, state.mark + new Vector2(side * (LegendsSkeletron.ClapMeet + 60f * back), -30f * back));
        }

        private void Smash(NPC npc, NPC head, LegendsSkeletron state)
        {
            float side = Side(npc);
            int t = state.moveTimer;
            Player target = Main.player[head.target];

            // Fingers down.
            npc.rotation = npc.rotation.AngleLerp(0f, 0.2f);

            if (t < LegendsSkeletron.SmashRaise)
            {
                LegendsSkeletron.Approach(npc, target.Center + new Vector2(side * LegendsSkeletron.SmashSpread, -LegendsSkeletron.SmashHeight), 0.16f, 20f);
                return;
            }

            float over = state.mark.Y - LegendsSkeletron.SmashHeight;

            // Where it comes down: beside the other, over the player's middle.
            float x = state.mark.X + side * LegendsSkeletron.SmashTouch;

            // The ground it will hit: level with the player's feet, or below. Found again
            // each tick until it falls, so a mark fixed or sent a tick late is followed.
            if (t < LegendsSkeletron.SmashJoined || floorY == float.PositiveInfinity)
                floorY = LegendsSkeletron.FloorUnder(x, npc.width * 0.3f, state.mark);

            if (t < LegendsSkeletron.SmashRaise + LegendsSkeletron.SmashHang)
            {
                // Held wide, trembling.
                LegendsSkeletron.Place(npc, new Vector2(state.mark.X + side * LegendsSkeletron.SmashSpread, over)
                    + Main.rand.NextVector2Circular(1.5f, 1.5f));

                if (!Main.dedServ && Main.rand.NextBool(3))
                {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Bone, 0f, 1f, 100, default, 1f);
                    dust.noGravity = false;
                }

                return;
            }

            if (t < LegendsSkeletron.SmashJoined)
            {
                // Together over them, quickening, until the knuckles touch.
                float p = (t - LegendsSkeletron.SmashRaise - LegendsSkeletron.SmashHang + 1) / (float)LegendsSkeletron.SmashJoin;
                float apart = MathHelper.Lerp(LegendsSkeletron.SmashSpread, LegendsSkeletron.SmashTouch, p * p * p);

                LegendsSkeletron.Place(npc, new Vector2(state.mark.X + side * apart, over));
                return;
            }

            if (t < LegendsSkeletron.SmashJoined + LegendsSkeletron.SmashDrop && !landed)
            {
                npc.velocity.X = x - npc.Center.X;
                npc.velocity.Y = Math.Min(npc.velocity.Y + SmashAccel, SmashMaxFall);

                float bottom = npc.position.Y + npc.height;

                if (bottom + npc.velocity.Y >= floorY)
                {
                    npc.velocity.Y = floorY - bottom;
                    landed = true;
                    Landing(npc, x, state);
                }

                return;
            }

            npc.velocity = Vector2.Zero;
        }

        /// <summary>
        /// The hands hit the ground: it heaves, as it does under the King Slime's slam. The
        /// first down on this machine shows the blow for the pair, from their middle and
        /// the width of both; the second only chips its bone. A hand alone shows its own.
        /// </summary>
        private void Landing(NPC npc, float x, LegendsSkeletron state)
        {
            // Nothing of a smash a parry has broken: on the machine that parried it, the hands
            // may come down before the server has thrown them back.
            if (Main.dedServ || Broken(HeadOf(npc)))
                return;

            float half = npc.width * 0.5f;
            Vector2 bottom = new Vector2(x, floorY);

            if (!state.smashStruck)
            {
                state.smashStruck = true;

                bool pair = Sibling(npc) != null;
                float middle = pair ? state.mark.X : x;
                float width = pair ? LegendsSkeletron.SmashTouch + half : half;
                Vector2 blow = new Vector2(middle, floorY);

                GroundWave.Slam(blow, width, 30, 22f, 36);
                LegendsKingSlime.GroundDust(middle - width, middle + width, floorY, 0.9f);
                LegendsSkeletron.Impact(blow, 12f, 26);
            }

            for (int i = 0; i < 16; i++)
            {
                Dust.NewDustPerfect(bottom + new Vector2(Main.rand.NextFloat(-half, half), -Main.rand.NextFloat(0f, 16f)),
                    DustID.Bone, new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-6f, -1f)), 0, default,
                    Main.rand.NextFloat(1f, 1.5f));
            }
        }

        /// <summary>The other hand on the same head, or null.</summary>
        internal static NPC Sibling(NPC hand)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (i != hand.whoAmI && other.active && other.type == NPCID.SkeletronHand && (int)other.ai[1] == (int)hand.ai[1])
                    return other;
            }

            return null;
        }

        /// <summary>
        /// Harmless on the way to their places and while they wait there: the blow is the
        /// clap, and in the smash their meeting and the fall, not the hands passing through
        /// the player to get into position.
        ///
        /// And the two strike as one: with one of them turned aside by this machine's
        /// player's parry, the other cannot touch that player either, for as long as the
        /// first cannot. Asked here, of what that machine itself knows, so that it holds
        /// from the tick of the parry, before the server has said the blow is broken. And
        /// on that tick itself, whichever of the two the game asks first: with both on the
        /// player at once, the one their parry does not face (and so is not about to turn
        /// aside) cannot touch them if the other is about to be turned aside.
        /// </summary>
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (!PADWorld.Legends || !held)
                return true;

            NPC other = Sibling(npc);

            if (other != null)
            {
                ParryPlayer parry = target.GetModPlayer<ParryPlayer>();

                if (parry.Spares(other) || (parry.AboutToTurn(other) && !parry.AboutToTurn(npc)))
                    return false;
            }

            NPC head = HeadOf(npc);
            LegendsSkeletron state = head?.GetGlobalNPC<LegendsSkeletron>();

            if (state == null)
                return true;

            int t = state.moveTimer;

            return state.move switch
            {
                LegendsSkeletron.Move.Clap => t >= LegendsSkeletron.ClapReach + LegendsSkeletron.ClapLock,
                LegendsSkeletron.Move.Smash => t >= LegendsSkeletron.SmashRaise + LegendsSkeletron.SmashHang,
                _ => true,
            };
        }

        /// <summary>Twice the blow while it comes down on them.</summary>
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (!PADWorld.Legends || !held || landed)
                return;

            NPC head = HeadOf(npc);
            LegendsSkeletron state = head?.GetGlobalNPC<LegendsSkeletron>();

            if (state == null || state.move != LegendsSkeletron.Move.Smash)
                return;

            int t = state.moveTimer;

            if (t >= LegendsSkeletron.SmashJoined && t < LegendsSkeletron.SmashJoined + LegendsSkeletron.SmashDrop)
            {
                modifiers.SourceDamage *= 2f;
            }
        }

        // ---- the arm ---------------------------------------------------------------------------

        /// <summary>
        /// Vanilla draws each arm as two bones laid from the wrist toward his shoulder
        /// (Main.DrawNPCDirect, before any PreDraw): a length that reaches him from where
        /// a hand hangs of itself, not from where these moves take it. So while a move has
        /// a hand, the bones that would be missing are laid from the end of vanilla's two
        /// to the shoulder, over them and under the hand.
        /// </summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (held && PADWorld.Legends)
                FillArm(npc, spriteBatch, screenPos);

            return true;
        }

        private static void FillArm(NPC hand, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            NPC head = HeadOf(hand);

            if (head == null)
                return;

            Texture2D bone = TextureAssets.BoneArm.Value;
            float side = hand.ai[0];

            Vector2 middle = new Vector2(head.position.X + head.width / 2, head.position.Y + head.height / 2);

            // Vanilla's two, as it lays them: 92 toward a point by his side, the first bone
            // there, 46 on, then 60 toward his shoulder, the second bone there.
            Vector2 at = new Vector2(hand.position.X + hand.width * 0.5f - 5f * side, hand.position.Y + 20f);

            Vector2 to = middle + new Vector2(-200f * side, 130f) - at;
            float along = 92f / Math.Max(1f, to.Length());
            at += to * along;
            at += to * along / 2f;

            to = middle + new Vector2(-50f * side, 80f) - at;
            along = 60f / Math.Max(1f, to.Length());
            at += to * along;

            Vector2 end = at + to.SafeNormalize(Vector2.UnitY) * (bone.Height * 0.5f);
            Vector2 shoulder = middle + new Vector2(-50f * side, 80f);
            Vector2 gap = shoulder - end;
            float length = gap.Length();

            // Reached, or past: nothing missing.
            if (length < 8f || Vector2.Dot(gap, to) <= 0f)
                return;

            Vector2 way = gap / length;
            float rotation = way.ToRotation() - MathHelper.PiOver2;
            int bones = (int)Math.Ceiling(length / (bone.Height * 0.8f));
            Vector2 origin = new Vector2(bone.Width * 0.5f, bone.Height * 0.5f);

            for (int i = 0; i < bones; i++)
            {
                Vector2 centre = end + way * (length * (i + 0.5f) / bones);
                Color light = Lighting.GetColor((int)(centre.X / 16f), (int)(centre.Y / 16f));

                spriteBatch.Draw(bone, centre - screenPos, null, light, rotation, origin, 1f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>His skulls, a fifth again as hard in Legends, like the rest of him.</summary>
    public class LegendsSkeletronSkulls : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.Skull;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!PADWorld.Legends)
                return;

            if (source is EntitySource_Parent parent && parent.Entity is NPC npc && npc.type == NPCID.SkeletronHead)
                projectile.damage = (int)Math.Round(projectile.damage * LegendsSkeletron.DamageScale);
        }
    }

    /// <summary>
    /// A skull out of Skeletron's mouth (LegendsSkeletron, the breath): it goes up and
    /// slows, is turned at the player, draws the line it will take, and takes it, fast
    /// and straight, through anything. Harmless until it goes. The skulls go one after
    /// another, in the order they came out.
    ///
    /// ai[0] is its place in the breath, ai[1] the way it will go, ai[2] 1 once that is
    /// decided. The way is decided where the skull was made (its owner: the server, or
    /// the one player) and sent.
    /// </summary>
    public class SkeletronBreathSkull : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Skull;

        /// <summary>Slowing, then turned, then the line held this long, then gone this fast.</summary>
        private const int Rise = 28;
        private const int Stagger = 5;
        private const int Tell = 42;
        private const float DashSpeed = 24f;

        /// <summary>How long the drawn line is.</summary>
        private const float LineLength = 1800f;

        /// <summary>Each skull's eyes, and its line: yellow, blue, red, as the sheet has them.</summary>
        private static readonly Color[] Lines = { new Color(255, 220, 80), new Color(100, 180, 255), new Color(255, 70, 70) };

        private int Index => (int)Projectile.ai[0];
        private bool Aimed => Projectile.ai[2] == 1f;
        private int AimAt => Rise + Index * Stagger;
        private int Time => (int)Projectile.localAI[0];

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)LineLength;
        }

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;

            int t = Time;
            Projectile.frame = Math.Abs(Index) % 3;
            Projectile.alpha = Math.Max(0, Projectile.alpha - 25);

            if (t < AimAt)
            {
                Projectile.velocity *= 0.93f;
                Projectile.rotation = Projectile.velocity.X * 0.05f;
                return;
            }

            if (t == AimAt && Projectile.owner == Main.myPlayer)
            {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];

                // Not quite all at one point: a little each side, so they cross the ground there.
                float spread = (Index - 2.5f) * 0.06f;
                Projectile.ai[1] = (target.Center - Projectile.Center).ToRotation() + spread;
                Projectile.ai[2] = 1f;
                Projectile.netUpdate = true;
            }

            if (t < AimAt + Tell)
            {
                Projectile.velocity *= 0.8f;
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);
                return;
            }

            if (!Aimed)
                return;

            if (t == AimAt + Tell && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 0.7f, MaxInstances = 6 }, Projectile.Center);

            Projectile.velocity = Projectile.ai[1].ToRotationVector2() * DashSpeed;
            Projectile.rotation = Projectile.velocity.X * 0.02f;
        }

        /// <summary>Only once it goes.</summary>
        public override bool CanHitPlayer(Player target) => Aimed && Time >= AimAt + Tell;

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D skull = TextureAssets.Projectile[Type].Value;
            int frameHeight = skull.Height / Main.projFrames[Type];
            Rectangle frame = new Rectangle(0, Projectile.frame * frameHeight, skull.Width, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            float shown = 1f - Projectile.alpha / 255f;
            int t = Time;

            // The line it will take, faint and then sure, until it has gone down it.
            if (Aimed && t < AimAt + Tell + 4)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                float sure = MathHelper.Clamp((t - AimAt) / (float)Tell, 0f, 1f);
                Color line = Lines[Projectile.frame] * (0.2f + 0.5f * sure) * shown;

                Main.spriteBatch.Draw(pixel, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 1, 1), line, Projectile.ai[1],
                    new Vector2(0f, 0.5f), new Vector2(LineLength, 2f + 2f * sure), SpriteEffects.None, 0f);
            }

            // Behind it, where it has been, once it goes.
            if (t >= AimAt + Tell)
            {
                for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
                {
                    Vector2 was = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;

                    Main.spriteBatch.Draw(skull, was - Main.screenPosition, frame, lightColor * (0.4f * fade * shown), Projectile.rotation,
                        origin, Projectile.scale, SpriteEffects.None, 0f);
                }
            }

            Main.spriteBatch.Draw(skull, Projectile.Center - Main.screenPosition, frame, lightColor * shown, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

    }
}
