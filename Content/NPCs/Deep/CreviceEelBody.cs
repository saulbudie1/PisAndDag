using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Biomes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Deep
{
    /// <summary>
    /// A piece of a Hole Dweller's body (<see cref="CreviceEelHead"/>), the last of them its tail:
    /// something to be hit, its life the head's (NPC.realLife), put each tick where the head says
    /// it lies. ai[0] is the head's slot, ai[1] which piece (1 the first after the head), ai[2]
    /// the head's own number (CreviceEelHead.Id): without its head, it is gone. Drawn with the
    /// head, not by itself, with its lights (CreviceEelBodyGlow); its picture holds two pieces of
    /// body and the tail, each 32 long and 36 high, the body 30 to 32 thick with its crest, the
    /// tail narrowing to its tip. Its hitbox fits that (<see cref="HitSize"/>, the tail's
    /// <see cref="TailSize"/>), each overlapping the next along the body (20 apart).
    /// </summary>
    public class CreviceEelBody : ModNPC, IDeepNative
    {
        /// <summary>Of the deep: never pressed (and its life is the head's in any case).</summary>
        public int SafeTo => VastOceanWeight.Deepest;

        /// <summary>A piece's hitbox, square, as thick as the body is under its crest; the tail's, as thick as it is about its middle.</summary>
        public const int HitSize = 28;
        public const int TailSize = 20;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 3;

            // The head stands for the whole eel in the bestiary.
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        }

        public override void SetDefaults()
        {
            NPC.width = HitSize;
            NPC.height = HitSize;
            NPC.lifeMax = CreviceEelHead.Life;
            NPC.damage = CreviceEelHead.BodyBite;
            NPC.defense = CreviceEelHead.Armour;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.waterMovementSpeed = 1f;
            NPC.npcSlots = 0f;
            NPC.dontCountMe = true;
            NPC.HitSound = CreviceEelHead.Struck;
            NPC.DeathSound = null;
        }

        public override void AI()
        {
            int slot = (int)NPC.ai[0];
            NPC head = slot >= 0 && slot < Main.maxNPCs ? Main.npc[slot] : null;

            if (head == null || !head.active || head.ModNPC is not CreviceEelHead eel || eel.Id != (int)NPC.ai[2])
            {
                NPC.life = 0;
                NPC.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);

                return;
            }

            int size = (int)NPC.ai[1] >= CreviceEelHead.Segments ? TailSize : HitSize;
            NPC.width = size;
            NPC.height = size;
            NPC.Center = eel.Piece((int)NPC.ai[1], out float facing);
            NPC.velocity = Vector2.Zero;
            NPC.rotation = facing;
            NPC.alpha = head.alpha;
            NPC.realLife = slot;
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 3; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 1.5f, -1f);
        }

        /// <summary>Gone with its head, never by itself.</summary>
        public override bool CheckActive() => false;

        /// <summary>Drawn with the head.</summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;
    }
}
