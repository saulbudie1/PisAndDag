using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// The Destroyer's Probes in Legends (the user's "orbs": "they are literally Probes ... change probe ai"), in its colour.
    ///
    ///   FREE       each circles the player at its own distance (16 to 25 tiles), keeping off the others, facing them;
    ///              every two and a half to four seconds a warned bolt (DestroyerBolt, held at it)
    ///   CHARGED    zapped by its master's storm (within 3 tiles of its body): it flares, fires at once, and runs faster
    ///              for a second and a half
    ///   SEEKING    in its master's second phase three in four of them, as they come, look for another to fly into
    ///   QUEUED     paired, and numbered in the order they paired; circling on, a faint crackle between them, till their turn:
    ///              fifteen ticks after the pair before them (by request: "each pair of colliding orbs assigned an ascending
    ///              number and each one flies to a new position that is 30 tick lag", then "interval between each of them the
    ///              send is 15 tick only")
    ///   PAIRED     on its turn the pair flies at a new place by the player (where the player is then), swooping in from
    ///              either side, its lightning crackling between them, and they burst into each other 45 ticks later: a
    ///              great burst of its lightning (ProbeBlast). So the bursts come one after another, fifteen ticks apart,
    ///              following the player (by request: "2 flies into each other creating a huge explosion with lightning
    ///              sparks", and "switch to 75% of the orbs clashing in phase 2")
    ///
    /// The server decides (who pairs, where they meet, when they fire); every machine moves them alike (SendExtraAI).
    /// Not where it is the Mechdusa's (NPC.IsMechQueenUp), nor outside Legends: vanilla's.
    /// </summary>
    public class LegendsProbe : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Probe;

        internal enum State : byte
        {
            Free,
            Charged,
            Seeking,
            Queued,
            Paired,
        }

        internal State state;
        internal int partner = -1;
        private Vector2 meet;
        private int clock;
        private int shootIn = -1;
        private bool rolled;
        private float orbit;

        /// <summary>Its pair's number (in the order pairs were made), the ticks till its pair's turn, and where it was when it went.</summary>
        internal int order;
        private int waitFor;
        private Vector2 start;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        private static bool Reworked => PADWorld.Legends && !NPC.IsMechQueenUp;

        private const int ChargedTicks = 90;

        /// <summary>The share of its Probes that look for another to fly into, in its second phase.</summary>
        private const float ClashShare = 0.75f;
        private const int SeekTicks = 300;
        private const float BlastRadius = 9f * 16f;

        /// <summary>Ticks between one pair's turn and the next's; ticks a pair takes to fly into each other.</summary>
        internal const int Lag = 15;
        private const int FlyTicks = 45;

        public override bool PreAI(NPC npc)
        {
            if (!Reworked)
                return true;

            if (npc.target < 0 || npc.target >= Main.maxPlayers || clock % 20 == 0)
                npc.TargetClosest(false);

            Player p = Main.player[npc.target];
            clock++;

            if (!p.active || p.dead)
            {
                npc.velocity.Y -= 0.2f;
                npc.velocity *= 0.98f;

                if (npc.timeLeft > 60)
                    npc.timeLeft = 60;

                return false;
            }

            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);

            if (Authority && !rolled)
            {
                rolled = true;
                orbit = Main.rand.NextFloat(MathHelper.TwoPi);
                shootIn = Main.rand.Next(90, 200);

                if (Destroyer.AnyInPhase2() && Main.rand.NextFloat() < ClashShare)
                {
                    state = State.Seeking;
                    clock = 0;
                }

                npc.netUpdate = true;
            }

            switch (state)
            {
                case State.Paired:
                    RunPaired(npc, p);
                    break;

                case State.Queued:
                    RunQueued(npc, p);
                    break;

                case State.Charged:
                    Circle(npc, p, 11f, 0.5f);

                    if (Authority && clock >= ChargedTicks)
                    {
                        state = State.Free;
                        clock = 0;
                        npc.netUpdate = true;
                    }

                    Shoot(npc, p, 14);
                    break;

                case State.Seeking:
                    Circle(npc, p, 7f, 0.32f);
                    Shoot(npc, p, 36);

                    if (Authority && clock >= SeekTicks)
                    {
                        state = State.Free;
                        clock = 0;
                        npc.netUpdate = true;
                    }

                    break;

                default:
                    Circle(npc, p, 7f, 0.32f);
                    Shoot(npc, p, 36);
                    break;
            }

            Face(npc, state == State.Paired ? meet : p.Center);

            if (state == State.Queued || state == State.Paired)
                clock = Math.Min(clock, short.MaxValue - 1);
            Lighting.AddLight(npc.Center, 0.25f, 0.45f, 0.45f);
            return false;
        }

        /// <summary>Round the player at its own distance, keeping off the others.</summary>
        private void Circle(NPC npc, Player p, float top, float pull)
        {
            orbit += 0.008f * (npc.whoAmI % 2 == 0 ? 1f : -1f);
            float radius = 260f + npc.whoAmI % 5 * 34f;
            Vector2 want = p.Center + orbit.ToRotationVector2() * radius;
            npc.velocity += (want - npc.Center).SafeNormalize(Vector2.Zero) * pull;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (i == npc.whoAmI || !other.active || other.type != NPCID.Probe)
                    continue;

                Vector2 off = npc.Center - other.Center;
                float d = off.Length();

                if (d < 60f && d > 0.01f)
                    npc.velocity += off / d * 0.15f;
            }

            if (npc.velocity.Length() > top)
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * top;

            npc.velocity *= 0.985f;
        }

        /// <summary>A warned bolt now and then (sooner when charged). The server's.</summary>
        private void Shoot(NPC npc, Player p, int warn)
        {
            if (!Authority || shootIn < 0)
                return;

            if (--shootIn > 0)
                return;

            shootIn = state == State.Charged ? Main.rand.Next(60, 90) : Main.rand.Next(150, 231);

            if (Vector2.Distance(npc.Center, p.Center) < 1400f)
                DestroyerShots.Bolt(npc, npc, p, warn, DestroyerShots.ProbeBoltDamage(npc));
        }

        /// <summary>Faces what it looks at, as vanilla turns a Probe.</summary>
        private static void Face(NPC npc, Vector2 at)
        {
            Vector2 d = at - npc.Center;

            if (d.X > 0f)
            {
                npc.spriteDirection = 1;
                npc.rotation = (float)Math.Atan2(d.Y, d.X);
            }
            else if (d.X < 0f)
            {
                npc.spriteDirection = -1;
                npc.rotation = (float)Math.Atan2(d.Y, d.X) + MathHelper.Pi;
            }
        }

        /// <summary>Its partner, if they are still a pair.</summary>
        private bool Partner(NPC npc, out NPC other)
        {
            other = partner >= 0 && partner < Main.maxNPCs ? Main.npc[partner] : null;
            return other != null && other.active && other.type == NPCID.Probe && other.TryGetGlobalNPC(out LegendsProbe them)
                && (them.state == State.Paired || them.state == State.Queued) && them.partner == npc.whoAmI;
        }

        private void Unpair(NPC npc)
        {
            if (!Authority)
                return;

            state = State.Free;
            partner = -1;
            clock = 0;
            npc.netUpdate = true;
        }

        /// <summary>Waiting its pair's turn: circling on, a faint crackle between them; on its turn (the server's, the lower of the two), away.</summary>
        private void RunQueued(NPC npc, Player p)
        {
            Circle(npc, p, 7f, 0.32f);

            if (!Partner(npc, out NPC other))
            {
                Unpair(npc);
                return;
            }

            if (waitFor > 0)
                waitFor--;

            if (npc.whoAmI < partner && !Main.dedServ && clock % 10 == 0)
                Lightning.Spawn(npc.Center, other.Center, DestroyerArt.Teal * 0.45f, 3f, 6, 0);

            if (Authority && npc.whoAmI < partner && waitFor <= 0)
            {
                // A new place: where the player is now, a little off.
                Vector2 meetAt = p.Center + Main.rand.NextVector2Circular(3f * 16f, 2f * 16f);
                LegendsProbe them = other.GetGlobalNPC<LegendsProbe>();

                foreach ((NPC who, LegendsProbe probe) in new[] { (npc, this), (other, them) })
                {
                    probe.state = State.Paired;
                    probe.meet = meetAt;
                    probe.start = who.Center;
                    probe.clock = 0;
                    who.netUpdate = true;
                }
            }
        }

        /// <summary>Flying into its partner at their new place, swooping in from either side, lightning between them; the burst when they meet.</summary>
        private void RunPaired(NPC npc, Player p)
        {
            if (!Partner(npc, out NPC other))
            {
                Unpair(npc);
                Circle(npc, p, 7f, 0.32f);
                return;
            }

            // Where it is to be next tick: on from where it went toward the place, quickening into it, bowed out to its side.
            float x = Math.Min(1f, (clock + 1f) / FlyTicks);
            Vector2 across = meet - start;
            Vector2 side = across.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * (npc.whoAmI < partner ? 1f : -1f);
            float bow = Math.Min(90f, across.Length() * 0.3f);
            Vector2 next = Vector2.Lerp(start, meet, x * x) + side * bow * (float)Math.Sin(MathHelper.Pi * x);
            npc.velocity = next - npc.Center;

            // The lower of the two draws what is between them.
            if (npc.whoAmI < partner && !Main.dedServ)
            {
                float charge = Math.Min(1f, clock / (float)FlyTicks);

                if (clock % 3 == 0)
                    Lightning.Spawn(npc.Center, other.Center, DestroyerArt.Teal, 5f + 6f * charge, 8, 0);

                if (clock % 3 == 0)
                {
                    PADParticleHandler.SpawnParticle(new BloomGeneric(meet, Vector2.Zero, DestroyerArt.Teal * (0.25f + 0.45f * charge), 0.3f + 0.3f * charge, 6) { Pixelate = true });
                    PADParticleHandler.SpawnParticle(new Ember(meet, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)), DestroyerArt.TealHot, 0.5f, 18) { Pixelate = true });
                }
            }

            if (Authority && npc.whoAmI < partner && clock >= FlyTicks)
            {
                DestroyerShots.Blast(npc, meet, BlastRadius, DestroyerShots.BlastDamage(npc));
                other.StrikeInstantKill();
                npc.StrikeInstantKill();
            }
        }

        /// <summary>Zapped by its master's storm: it flares, fires at once, runs faster a while. The server's.</summary>
        internal void Overcharge(NPC npc)
        {
            if (!Reworked || state == State.Paired || state == State.Queued || state == State.Charged)
                return;

            state = State.Charged;
            clock = 0;
            shootIn = 1;
            npc.netUpdate = true;
        }

        /// <summary>Two seeking Probes made a pair, numbered, to go on their turn (in so many ticks). The server's (LegendsProbeSystem).</summary>
        internal static void Pair(NPC a, LegendsProbe pa, NPC b, LegendsProbe pb, int number, int wait)
        {
            pa.state = pb.state = State.Queued;
            pa.partner = b.whoAmI;
            pb.partner = a.whoAmI;
            pa.order = pb.order = number;
            pa.waitFor = pb.waitFor = wait;
            pa.clock = pb.clock = 0;
            a.netUpdate = b.netUpdate = true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!Reworked)
                return;

            Vector2 at = npc.Center;
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.2f + npc.whoAmI);
            float bright = state switch
            {
                State.Charged => 1.2f,
                State.Paired => 0.8f + 0.7f * Math.Min(1f, clock / (float)FlyTicks),
                State.Queued => 0.75f,
                _ => 0.6f,
            };

            Color hue = state == State.Charged ? Color.Lerp(DestroyerArt.Teal, DestroyerArt.Yellow, 0.4f) : DestroyerArt.Teal;

            DestroyerArt.DrawGlow(spriteBatch, at, 44f * bright, DestroyerArt.Lit(hue * (0.35f * pulse * bright)));
            DestroyerArt.DrawGlow(spriteBatch, at, 12f, DestroyerArt.Lit(DestroyerArt.TealHot * (0.6f * bright)), hotCore: true);
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)state);
            binaryWriter.Write((short)partner);
            binaryWriter.Write(meet.X);
            binaryWriter.Write(meet.Y);
            binaryWriter.Write((short)Math.Min(clock, short.MaxValue));
            binaryWriter.Write((short)shootIn);
            binaryWriter.Write(rolled);
            binaryWriter.Write(orbit);
            binaryWriter.Write((short)order);
            binaryWriter.Write((short)Math.Clamp(waitFor, 0, short.MaxValue));
            binaryWriter.Write(start.X);
            binaryWriter.Write(start.Y);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            state = (State)binaryReader.ReadByte();
            partner = binaryReader.ReadInt16();
            meet = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
            clock = binaryReader.ReadInt16();
            shootIn = binaryReader.ReadInt16();
            rolled = binaryReader.ReadBoolean();
            orbit = binaryReader.ReadSingle();
            order = binaryReader.ReadInt16();
            waitFor = binaryReader.ReadInt16();
            start = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }
    }

    /// <summary>
    /// Pairs seeking Probes, nearest with nearest, every few ticks; numbers each pair in order, and gives it its turn fifteen
    /// ticks after the last pair's. The count starts again once no pair is left. The server's.
    /// </summary>
    public class LegendsProbeSystem : ModSystem
    {
        private readonly List<int> seeking = new();

        /// <summary>The last pair's number, and the tick (Main.GameUpdateCount) the next pair's turn may come at the soonest.</summary>
        private int numbered;
        private uint nextTurn;

        public override void ClearWorld()
        {
            numbered = 0;
            nextTurn = 0;
        }

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !PADWorld.Legends || NPC.IsMechQueenUp || Main.GameUpdateCount % 10 != 0)
                return;

            seeking.Clear();
            bool anyPair = false;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (npc.active && npc.type == NPCID.Probe && npc.TryGetGlobalNPC(out LegendsProbe probe)
                    && (probe.state == LegendsProbe.State.Queued || probe.state == LegendsProbe.State.Paired))
                    anyPair = true;
            }

            if (!anyPair)
                numbered = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (npc.active && npc.type == NPCID.Probe && npc.TryGetGlobalNPC(out LegendsProbe probe) && probe.state == LegendsProbe.State.Seeking)
                    seeking.Add(i);
            }

            while (seeking.Count >= 2)
            {
                NPC a = Main.npc[seeking[0]];
                int best = 1;
                float bestD = float.MaxValue;

                for (int k = 1; k < seeking.Count; k++)
                {
                    float d = Vector2.DistanceSquared(a.Center, Main.npc[seeking[k]].Center);

                    if (d < bestD)
                    {
                        bestD = d;
                        best = k;
                    }
                }

                NPC b = Main.npc[seeking[best]];
                uint now = Main.GameUpdateCount;
                uint turn = Math.Max(now + LegendsProbe.Lag, nextTurn);
                nextTurn = turn + LegendsProbe.Lag;
                LegendsProbe.Pair(a, a.GetGlobalNPC<LegendsProbe>(), b, b.GetGlobalNPC<LegendsProbe>(), ++numbered, (int)(turn - now));
                seeking.RemoveAt(best);
                seeking.RemoveAt(0);
            }
        }
    }
}
