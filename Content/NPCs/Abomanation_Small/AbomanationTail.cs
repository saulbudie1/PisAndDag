using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// The end of it. Same following as a body segment, and nothing on it moves.
    ///
    /// Three pairs on top and no legs at all. Every leg on this thing belongs to the body,
    /// so the seventeen a side that AbomanationArms shares out is the whole count and the
    /// tail is the part that stopped walking.
    ///
    /// Laid out rather than scattered, unlike the body's. The body is a repeating thing
    /// and reads better for being slightly wrong segment to segment; the tail is seen once
    /// and reads better for being deliberate.
    /// </summary>
    public class AbomanationTail : ModNPC
    {
        public override string Texture => Abomanation.Folder + "Abomanation_Tail";

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };
        }

        public override void SetDefaults()
        {
            NPC.width = 48;
            NPC.height = 48;

            NPC.aiStyle = -1;
            AIType = -1;

            NPC.lifeMax = 6000;
            NPC.damage = 30;
            NPC.defense = 9;

            NPC.boss = true;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.behindTiles = true;
            NPC.alpha = 255;

            NPC.netAlways = true;
            NPC.dontCountMe = true;
            NPC.value = 0f;

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(NPC.alpha);
            writer.Write(NPC.dontTakeDamage);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            NPC.alpha = reader.ReadInt32();
            NPC.dontTakeDamage = reader.ReadBoolean();
        }

        public override bool CheckActive() => false;

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        public override void AI()
        {
            if (!AbomanationChain.Tether(NPC, ModContent.NPCType<AbomanationHead>(), ModContent.NPCType<AbomanationBody>()))
                return;

            AbomanationChain.Follow(NPC, Main.npc[AbomanationChain.Ahead(NPC)], Abomanation.TailSpacing);
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            return AbomanationChain.Touching(NPC, target, 26f);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 3; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Color colour = NPC.GetAlpha(drawColor);

            // No call to AbomanationArms.Bottom. The tail carries no legs, so there is
            // nothing to draw under the sprite and the pass starts at the sprite itself.
            Texture2D texture = TextureAssets.Npc[Type].Value;

            spriteBatch.Draw(texture, NPC.Center - screenPos, null, colour,
                NPC.rotation, texture.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);

            AbomanationArms.TopPairs(spriteBatch, NPC, screenPos, colour,
                rows: 3, rowSpan: 18f, inset: 10f);

            return false;
        }
    }
}
