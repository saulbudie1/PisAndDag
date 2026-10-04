using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Golem's fists, parried. A part of a boss, and so never parried by the rules
    /// (ParryRules.BossLimbs): the mod speaks for these two itself (Parry.AddNPC), which is
    /// asked before the rules are.
    ///
    /// Only the punch can be parried: the fist in flight at a player (its AI's third state,
    /// NPC.ai[0] at 2). Not the fist at the Golem's side, winding up there, or on its way
    /// back.
    ///
    /// A parried punch takes the parry's blow as anything does, and is then sent back the
    /// way it came, turned about, into the Golem. When it gets there it takes a seventh of
    /// its own full life and the Golem a twenty-first of the Golem's, whatever their armour,
    /// and whether or not anything else can hurt the Golem then (it cannot be hurt while its
    /// head is on: this can). Seven punches parried break a fist; both fists broken that
    /// way have cost the Golem fourteen of its twenty-one parts.
    ///
    /// Never staggered, by request, and so never open to the riposte; not pushed either: it
    /// is sent home instead.
    ///
    /// How it goes back is the game's own doing. The fist's AI (NPC.AI_047_GolemFist, read
    /// from the 1.4.4 assembly) flies it to its place on the Golem in its first state
    /// (ai[0] at 0) and sets it down there the tick it is within reach, its speed that tick
    /// exactly the way left to go: that tick is the one it lands on. All this does is put it
    /// in that state early, turn it about while it flies, and strike when it lands.
    ///
    /// The strikes are made where the world is decided and sent on; what is seen and heard
    /// is each machine's own, off the same landing as it sees it.
    ///
    /// A Golem below full life is one the game makes faster, fists and all; while its head
    /// is on that is kept from following from this (GolemPace).
    /// </summary>
    public class GolemFistParry : GlobalNPC
    {
        /// <summary>What it costs when the fist gets back: the fist its full life over this, the Golem its full life over this.</summary>
        public const int FistShare = 7;
        public const int CoreShare = 21;

        /// <summary>The fist's AI states (NPC.ai[0]): at the Golem or going back to it; the punch in flight.</summary>
        private const float GoingHome = 0f;
        private const float Punching = 2f;

        /// <summary>
        /// How near its place the fist's next step has to end for this to be the tick it
        /// lands, in pixels. The game sets it down exactly; any other tick it ends 12 short
        /// or more.
        /// </summary>
        private const float SetDown = 6f;

        /// <summary>How far off the landing still shakes this machine's screen, in pixels.</summary>
        private const float Felt = 1400f;

        private const string Told = "a parry strikes it and sends it back into the Golem, turned about. When it gets there it loses a seventh of its life and the Golem a twenty-first of its own. Never staggered.";

        /// <summary>Parried and on its way back into the Golem. Synced.</summary>
        private bool sentBack;

        /// <summary>On a client: this trip's landing has been shown already, and the server has yet to say it is over.</summary>
        private bool shown;

        public bool SentBack => sentBack;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.GolemFistLeft || entity.type == NPCID.GolemFistRight;
        }

        public override void SetStaticDefaults()
        {
            Parry.AddNPC(NPCID.GolemFistLeft, CanBeTurned, Turned, reels: false, told: Told);
            Parry.AddNPC(NPCID.GolemFistRight, CanBeTurned, Turned, reels: false, told: Told);
        }

        /// <summary>The Golem these fists are the fists of: the game keeps which (NPC.golemBoss).</summary>
        private static bool Core(out NPC core)
        {
            int index = NPC.golemBoss;

            core = index >= 0 && index < Main.maxNPCs ? Main.npc[index] : null;

            return core != null && core.active && core.type == NPCID.Golem && core.life > 0;
        }

        /// <summary>The fist's place on the Golem this tick, as its own AI works it out.</summary>
        private static Vector2 Home(NPC fist, NPC core)
        {
            Vector2 home = core.Center + core.velocity + new Vector2(0f, -9f * fist.scale);

            home.X += (fist.type == NPCID.GolemFistLeft ? -84f : 78f) * fist.scale;

            return home;
        }

        /// <summary>The way the game points a fist going home, by its speed: the left fist along it, the right fist against it.</summary>
        private static float Pointed(NPC fist)
        {
            Vector2 speed = fist.type == NPCID.GolemFistLeft ? fist.velocity : -fist.velocity;

            return (float)Math.Atan2(speed.Y, speed.X);
        }

        /// <summary>Only the punch in flight.</summary>
        private static bool CanBeTurned(NPC npc)
        {
            return npc.ai[0] == Punching && Core(out _);
        }

        /// <summary>
        /// Parried, where it is simulated: back to the state that flies it home, and marked.
        /// Its AI gives it its speed home on its next tick; until then it keeps the speed it
        /// had, the other way.
        /// </summary>
        private static void Turned(NPC npc, Player by, Vector2 away, bool staggers)
        {
            if (!npc.TryGetGlobalNPC(out GolemFistParry fist) || !Core(out NPC core))
                return;

            fist.sentBack = true;
            fist.shown = false;

            npc.ai[0] = GoingHome;
            npc.ai[1] = 0f;
            npc.noTileCollide = true;
            npc.velocity = (Home(npc, core) - npc.Center).SafeNormalize(-away) * npc.velocity.Length();

            // Whoever parried it has had a hand in what it does to the Golem: the game marks
            // every part of the Golem for them at once.
            npc.PlayerInteraction(by.whoAmI);

            npc.netUpdate = true;
        }

        public override void PostAI(NPC npc)
        {
            if (!sentBack)
                return;

            // The Golem's life as it really is, before anything here strikes it: its AI and
            // its fists' are shown a full one while its head is on (GolemPace).
            GolemPace.Restore();

            // The Golem is gone, or the fist is out again without having landed: nothing to land on.
            if (!Core(out NPC core) || npc.ai[0] >= Punching)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    sentBack = false;
                    npc.netUpdate = true;
                }

                return;
            }

            Vector2 home = Home(npc, core);

            if (Vector2.DistanceSquared(npc.Center + npc.velocity, home) > SetDown * SetDown)
            {
                // Still on its way: knuckles first, half a turn from how the game points a
                // fist going home. Worked out from its speed and not added to what it has,
                // so a tick its AI does not run (something else has stopped it) turns it no
                // further.
                if (!shown && npc.velocity != Vector2.Zero)
                    npc.rotation = Pointed(npc) + MathHelper.Pi;

                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // The server strikes and says when it is over; this machine only shows it, once.
                if (!shown)
                {
                    shown = true;
                    Landing(home, core);
                }

                return;
            }

            sentBack = false;
            npc.netUpdate = true;

            Landing(home, core);

            int way = home.X < core.Center.X ? 1 : -1;

            Strike(core, Math.Max(1, core.lifeMax / CoreShare), way);
            Strike(npc, Math.Max(1, npc.lifeMax / FistShare), -way);
        }

        /// <summary>
        /// A flat loss of life, armour or no, and whether or not it can be hurt otherwise
        /// (NPC.StrikeNPC asks neither): made here and sent to everyone.
        /// </summary>
        private static void Strike(NPC npc, int damage, int direction)
        {
            if (!npc.active || npc.life <= 0)
                return;

            NPC.HitInfo hit = new NPC.HitInfo
            {
                Damage = damage,
                HitDirection = direction,
                Knockback = 0f,
                DamageType = DamageClass.Default,
            };

            npc.StrikeNPC(hit, false, true);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendStrikeNPC(npc, hit);
        }

        /// <summary>The fist into the Golem, as it is seen and heard: a thump, a ring, the temple's stone off both.</summary>
        private static void Landing(Vector2 at, NPC core)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.45f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 0.7f }, at);

            PADParticleHandler.SpawnParticle(new ParryRing(at, 10f, 130f, 20, ParryEffects.Steel));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, ParryEffects.Steel * 0.6f, 0.7f, 14));
            SparkShard.Spray(at, ParryEffects.Steel, 14, 9f, 30f, 18, falls: true);

            Vector2 off = (at - core.Center).SafeNormalize(-Vector2.UnitY);

            for (int i = 0; i < 26; i++)
            {
                Vector2 speed = off.RotatedByRandom(1.3f) * Main.rand.NextFloat(2f, 9f);
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(22f, 22f), DustID.t_Lihzahrd, speed, 0, default, Main.rand.NextFloat(1.1f, 1.8f));

                dust.noGravity = i % 3 == 0;
            }

            Player local = Main.LocalPlayer;

            if (local != null && local.active && !local.dead && Vector2.DistanceSquared(local.Center, at) < Felt * Felt)
                local.PIS().BumpScreenShake(7f, 14);
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(sentBack);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            sentBack = bitReader.ReadBit();

            // Over, by the server's word: the next trip's landing is a new one to show.
            if (!sentBack)
                shown = false;
        }
    }
}
