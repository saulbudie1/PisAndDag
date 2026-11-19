using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Sandbag
{
    /// <summary>
    /// The Sandbag (by request: "an item that spawn a dummy npc like the superdummy from calamity, name it 'Sandbag', 2
    /// frames NPC, frame 1 is normal idle, frame 2 is struck which is the thing being punched in the middle, maximum 10 NPCs
    /// spawn; as tall as normal dummy"). A heavy bag of sand on the ground, as tall as the Target Dummy (fifty pixels, its
    /// two-pixel grid), drawn in code: idle, and struck (bent in the middle, the side it was hit from caved in; turned to
    /// face the blow). It never dies: its life is all but endless and given back every tick, so every hit shows its number
    /// and homing things and minions go for it (an immortal one they would not). It hurts nobody, has no bar, never leaves.
    ///
    /// Placed and cleared by the item (Items.Misc.Sandbag): in a world of one, there and then; in multiplayer, by the
    /// server, asked by packet (PADPacket.Sandbag).
    /// </summary>
    public class SandbagDummy : ModNPC
    {
        /// <summary>The most there may be in the world at once (by request).</summary>
        public const int Most = 10;

        /// <summary>How long it shows struck after a blow.</summary>
        private const int StruckTicks = 12;

        public static readonly SoundStyle Thump = new("PisAndDag/Content/Sounds/Sandbag/SandbagHit")
        {
            Volume = 0.8f,
            PitchVariance = 0.25f,
            MaxInstances = 4,
        };

        private ref float Struck => ref NPC.localAI[0];

        /// <summary>The way the last blow went (1: from its left, so struck on its left).</summary>
        private ref float From => ref NPC.localAI[1];

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 2;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        }

        public override void SetDefaults()
        {
            NPC.width = 22;
            NPC.height = 46;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 9999999;
            NPC.HitSound = null;
            NPC.DeathSound = null;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.value = 0f;
            NPC.npcSlots = 0f;
            NPC.netAlways = true;
        }

        public override void AI()
        {
            NPC.velocity.X = 0f;
            NPC.life = NPC.lifeMax;
            NPC.timeLeft = NPC.activeTime;

            if (Struck > 0f)
                Struck--;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        public override bool CheckDead()
        {
            NPC.life = NPC.lifeMax;
            return false;
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            Struck = StruckTicks;
            From = hit.HitDirection >= 0 ? 1f : -1f;

            if (Main.dedServ)
                return;

            if (NPC.soundDelay <= 0)
            {
                SoundEngine.PlaySound(Thump, NPC.Center);
                NPC.soundDelay = 5;
            }

            // A little sand shaken out of it on the side it was hit.
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = NPC.Center + new Vector2(-From * NPC.width * 0.4f, Main.rand.NextFloat(-8f, 10f));
                Dust dust = Dust.NewDustPerfect(at, DustID.Sand, new Vector2(-From * Main.rand.NextFloat(0.5f, 1.8f), Main.rand.NextFloat(-1.5f, 0.5f)), 0, default, Main.rand.NextFloat(0.8f, 1.1f));
                dust.noGravity = false;
            }
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.frame.Y = Struck > 0f ? frameHeight : 0;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            frame.Height = texture.Height / Main.npcFrameCount[Type];

            // Its foot on the ground (a pixel into it); struck from its right, its picture turned to match.
            Vector2 at = NPC.Bottom - screenPos + new Vector2(0f, 2f + NPC.gfxOffY);
            SpriteEffects flip = Struck > 0f && From < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(texture, at, frame, NPC.GetAlpha(drawColor), 0f, new Vector2(frame.Width * 0.5f, frame.Height), 1f, flip, 0f);
            return false;
        }

        // ---- placed and cleared ------------------------------------------------------------------------------------------

        private enum Ask : byte
        {
            Place,
            Clear,
        }

        /// <summary>Sandbags in the world now.</summary>
        public static int Count() => NPC.CountNPCS(ModContent.NPCType<SandbagDummy>());

        /// <summary>One where the player points (it drops to the ground), unless there are ten already. The asking player's.</summary>
        public static void Place(Vector2 at)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = ModContent.GetInstance<global::PisAndDag.PisAndDag>().GetPacket();
                packet.Write((byte)PADPacket.Sandbag);
                packet.Write((byte)Ask.Place);
                packet.Write(at.X);
                packet.Write(at.Y);
                packet.Send();
                return;
            }

            Spawn(at);
        }

        /// <summary>All of them taken away. The asking player's.</summary>
        public static void Clear()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = ModContent.GetInstance<global::PisAndDag.PisAndDag>().GetPacket();
                packet.Write((byte)PADPacket.Sandbag);
                packet.Write((byte)Ask.Clear);
                packet.Send();
                return;
            }

            Remove();
        }

        /// <summary>The server's side of it.</summary>
        public static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            var ask = (Ask)reader.ReadByte();

            if (ask == Ask.Place)
            {
                Vector2 at = new(reader.ReadSingle(), reader.ReadSingle());

                if (Main.netMode == NetmodeID.Server)
                    Spawn(at);
            }
            else if (Main.netMode == NetmodeID.Server)
            {
                Remove();
            }
        }

        private static void Spawn(Vector2 at)
        {
            if (Count() >= Most)
                return;

            int type = ModContent.NPCType<SandbagDummy>();
            int height = ContentSamples.NpcsByNetId[type].height;
            NPC.NewNPC(new EntitySource_Misc("Sandbag"), (int)at.X, (int)at.Y + height / 2, type);
        }

        private static void Remove()
        {
            int type = ModContent.NPCType<SandbagDummy>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type != type)
                    continue;

                npc.life = 0;
                npc.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }
        }
    }
}
