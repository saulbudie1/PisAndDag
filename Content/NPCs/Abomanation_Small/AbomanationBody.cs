using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// One of the fifteen. It does not think: it follows whatever is in front of it and
    /// carries its legs.
    ///
    /// One class for all five body sprites rather than five classes. A ModNPC's texture is
    /// bound to its type, so five classes would mean five NPC types, five bestiary entries
    /// to hide, five sets of defaults to keep in step, and the head choosing between them
    /// at spawn. Instead the sprite is picked in the draw call from ai[3], which is the
    /// segment's place in the chain, so the five cycle in order down the body and the
    /// order is a property of the chain rather than of the spawn loop.
    /// </summary>
    public class AbomanationBody : ModNPC
    {
        public override string Texture => Abomanation.Folder + "Abomanation_Body_1";

        private static Asset<Texture2D>[] sprites;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            // One boss, one bestiary entry, and it belongs to the head.
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };

            // Both the five body sprites and the six arms, here rather than on first draw.
            if (!Main.dedServ)
            {
                AbomanationArms.Load();

                for (int i = 0; i < Abomanation.BodyTextures; i++)
                    Sprite(i);
            }
        }

        public override void Unload()
        {
            sprites = null;
        }

        public override void SetDefaults()
        {
            NPC.width = 48;
            NPC.height = 48;

            NPC.aiStyle = -1;
            AIType = -1;

            // The head owns the health. realLife points every segment at it, so this
            // number is only ever the ceiling on the clamp in AbomanationChain.Tether.
            NPC.lifeMax = 6000;
            NPC.damage = 30;
            NPC.defense = 7;

            NPC.boss = true;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.behindTiles = true;
            NPC.alpha = 255;

            NPC.netAlways = true;

            // Off the enemy count and off the loot: it is a piece of something, not a
            // thing in its own right.
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
            if (!AbomanationChain.Tether(NPC, ModContent.NPCType<AbomanationHead>(), Type))
                return;

            NPC leader = Main.npc[AbomanationChain.Ahead(NPC)];

            float spacing = leader.type == ModContent.NPCType<AbomanationHead>()
                ? Abomanation.HeadSpacing
                : Abomanation.BodySpacing;

            AbomanationChain.Follow(NPC, leader, spacing);
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

        /// <summary>This segment's place in the chain. 0 is the one behind the head.</summary>
        private int Place => (int)NPC.ai[3];

        private static Texture2D Sprite(int place)
        {
            sprites ??= new Asset<Texture2D>[Abomanation.BodyTextures];

            int i = place % Abomanation.BodyTextures;

            if (i < 0)
                i += Abomanation.BodyTextures;

            return (sprites[i] ??= ModContent.Request<Texture2D>(
                Abomanation.Folder + "Abomanation_Body_" + (i + 1), AssetRequestMode.ImmediateLoad)).Value;
        }

        /// <summary>
        /// Everything, in one pass, in order: legs, then the segment over the top of them,
        /// then the growths on its back.
        ///
        /// Drawn here rather than letting vanilla draw the sprite and hanging the arms off
        /// PreDraw and PostDraw, because vanilla flips the sprite horizontally when
        /// spriteDirection is 1. The body is symmetrical enough that the flip is invisible
        /// on it and very visible on the arms, which are not. Owning the whole draw means
        /// there is one transform and nothing to keep in step with it.
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Color colour = NPC.GetAlpha(drawColor);
            int place = Place;

            AbomanationArms.Bottom(spriteBatch, NPC, screenPos, colour, place,
                leftCount: AbomanationArms.BottomCount(place, 0),
                rightCount: AbomanationArms.BottomCount(place, 1),
                span: AbomanationArms.BottomSpan, swing: true);

            Texture2D texture = Sprite(place);

            spriteBatch.Draw(texture, NPC.Center - screenPos, null, colour,
                NPC.rotation, texture.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);

            AbomanationArms.TopScattered(spriteBatch, NPC, screenPos, colour, seed: place);

            return false;
        }
    }
}
