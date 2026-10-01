using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The Queen Slime's slimes (blue, pink and purple), in every world. Each lasts thirty
    /// seconds and then pops by itself, and gives nothing but the sight of it: no items, no
    /// money, no hearts (PADGlobalNPC.KillForNothing). Its last second and a half tells it
    /// coming: it shines, slowly and then faster and brighter, and sheds sparkles. Her death
    /// pops every one of them at once, the same way (LegendsQueenSlime.KillHerSlimes).
    ///
    /// The server counts and pops. It tells every client first (SyncNPC), so that when the
    /// kill arrives the client knows it for a pop and plays one. A client keeps its own count
    /// for the tell, put right by the server's (SendExtraAI).
    /// </summary>
    public class QueenSlimeMinions : GlobalNPC
    {
        /// <summary>Thirty seconds.</summary>
        public const int Lifetime = 1800;

        /// <summary>How long before it pops it shows it: a second and a half.</summary>
        private const int TellTicks = 90;

        /// <summary>How fast its shine beats as the tell starts, and as it pops: radians a tick.</summary>
        private const float BeatFrom = 0.15f;
        private const float BeatTo = 0.9f;

        /// <summary>Ticks it has been here.</summary>
        private int age;

        /// <summary>It is popping, not being killed: set by the server just before, and sent.</summary>
        private bool popped;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => Hers(entity.type);

        /// <summary>One of her slimes. Vanilla makes these for her and nothing else.</summary>
        public static bool Hers(int type) =>
            type == NPCID.QueenSlimeMinionBlue || type == NPCID.QueenSlimeMinionPink || type == NPCID.QueenSlimeMinionPurple;

        public override void PostAI(NPC npc)
        {
            age++;

            if (!Main.dedServ)
                Tell(npc);

            if (Main.netMode != NetmodeID.MultiplayerClient && age >= Lifetime)
                Expire(npc);
        }

        /// <summary>
        /// It pops, and gives nothing. The server's (or a single player's): every client is
        /// told it is a pop, then sent the kill, and shows it.
        /// </summary>
        public static void Expire(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !npc.active || npc.life <= 0)
                return;

            if (!npc.TryGetGlobalNPC(out QueenSlimeMinions slime))
                return;

            slime.popped = true;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);

            PADGlobalNPC.KillForNothing(npc);
        }

        // ---- the tell ------------------------------------------------------------------------------

        /// <summary>Ticks into its tell: 0 until the last second and a half.</summary>
        private int Telling => Math.Max(0, age - (Lifetime - TellTicks));

        /// <summary>
        /// Its shine, 0 to 1, beating faster and faster: the beat's rate climbs from BeatFrom to
        /// BeatTo over the tell, and this is the phase that rate adds up to.
        /// </summary>
        private float Shine()
        {
            int s = Math.Min(Telling, TellTicks);
            float climb = (BeatTo - BeatFrom) / TellTicks;
            float phase = BeatFrom * s + 0.5f * climb * s * s;

            return 0.5f - 0.5f * (float)Math.Cos(phase);
        }

        /// <summary>How far through its tell it is, 0 to 1.</summary>
        private float TellDone() => MathHelper.Clamp(Telling / (float)TellTicks, 0f, 1f);

        /// <summary>Its light, and sparkles off it, more and more.</summary>
        private void Tell(NPC npc)
        {
            if (Telling <= 0)
                return;

            float done = TellDone();

            Lighting.AddLight(npc.Center, Colour(npc.type).ToVector3() * ((0.2f + 0.6f * done) * (0.4f + 0.6f * Shine())));

            if (Main.rand.NextFloat() < 0.1f + 0.45f * done)
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, Shard(npc.type), 0f, -1f, 0, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = true;
                dust.velocity *= 0.4f;
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (Telling <= 0)
                return;

            drawColor = Color.Lerp(drawColor, Color.White, (0.2f + 0.6f * TellDone()) * Shine());
        }

        // ---- the pop -------------------------------------------------------------------------------

        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            if (Main.dedServ || npc.life > 0 || !popped)
                return;

            Vector2 at = npc.Center;
            Color colour = Colour(npc.type);
            int shard = Shard(npc.type);

            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f, Volume = 0.4f, MaxInstances = 4 }, at);

            for (int i = 0; i < 16; i++)
            {
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f), shard,
                    Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 5.5f), 0, default, Main.rand.NextFloat(0.9f, 1.3f));

                dust.noGravity = Main.rand.NextBool();
            }

            for (int i = 0; i < 6; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();

                PADParticleHandler.SpawnParticle(new SlimeGlob(at + way * 6f, way * Main.rand.NextFloat(2.5f, 5f) - new Vector2(0f, 1.5f),
                    Main.rand.NextFloat(0.4f, 0.7f), 50, colour));
            }

            SparkShard.Spray(at, Color.White, 6, 5f, 30f, 14, palette: QueenSlimeArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, colour, 90f / 160f, 14));
        }

        /// <summary>Its colour, for its light and its gel.</summary>
        private static Color Colour(int type) => type switch
        {
            NPCID.QueenSlimeMinionBlue => QueenSlimeArt.Blue,
            NPCID.QueenSlimeMinionPink => QueenSlimeArt.Pink,
            _ => QueenSlimeArt.Violet,
        };

        /// <summary>The crystal dust of its colour.</summary>
        private static int Shard(int type) => type switch
        {
            NPCID.QueenSlimeMinionBlue => DustID.BlueCrystalShard,
            NPCID.QueenSlimeMinionPink => DustID.PinkCrystalShard,
            _ => DustID.PurpleCrystalShard,
        };

        // ---- sync ----------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(popped);
            binaryWriter.Write((short)Math.Min(age, short.MaxValue));
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            popped = bitReader.ReadBit();

            int told = binaryReader.ReadInt16();

            if (Math.Abs(told - age) > 6)
                age = told;
        }
    }
}
