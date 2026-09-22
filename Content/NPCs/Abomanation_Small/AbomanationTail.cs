using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// The end of it. Same following as a body segment, and bare.
    ///
    /// No limbs of any kind, above or below. Every arm on this thing belongs to the body,
    /// which makes the count AbomanationArms shares out the whole count for a side, and
    /// makes the tail the part that has nothing left on it.
    ///
    /// So its whole draw is one sprite. If it ever wants limbs again, AbomanationArms.Bottom
    /// and AbomanationArms.TopPairs both still take a segment and lay them out; nothing
    /// calls TopPairs any more.
    /// </summary>
    public class AbomanationTail : ModNPC, ICCBypassOutline
    {
        public override string Texture => Abomanation.Folder + "Abomanation_Tail";

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };
        }

        /// <summary>Legends hits harder. After vanilla's scaling, so it is on top of it.</summary>
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            if (PADWorld.Legends)
                NPC.damage = (int)(NPC.damage * Abomanation.LegendsDamage);
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

            // A segment is a boss for the map and the bestiary, but it is not a boss
            // ARRIVING. Without this BossArrivalWait reads the flag, holds the segment
            // still for the length of the universal arrival, and the head swims off
            // alone while the body sits where it was laid.
            NPC.PAD().OwnIntro = true;

            // One star for the whole animal, from the head. See AbomanationChain.Flag.
            NPC.PAD().CCQuiet = true;
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

            NPC leader = Main.npc[AbomanationChain.Ahead(NPC)];

            AbomanationChain.Flag(NPC);

            // The end. The body follows the head while it stops; from the rise on the
            // head places every part itself, along its own path and then on the loop.
            // See AbomanationHead.Die.
            if (AbomanationChain.Dying(NPC))
            {
                NPC.life = 1;

                AbomanationHead boss = AbomanationChain.Boss(NPC);

                if (boss != null && boss.BodyPlaced)
                {
                    boss.Place(NPC);
                    return;
                }

                if (boss == null || !boss.BodyFollows)
                {
                    NPC.velocity = Vector2.Zero;
                    return;
                }
            }

            // The whip: the tail is the end of a chain with momentum while the head
            // cracks, and it is the end that moves.
            if (AbomanationChain.Lashing(NPC))
            {
                AbomanationChain.Swing(NPC, leader, Abomanation.TailSpacing);
                return;
            }

            AbomanationChain.Follow(NPC, leader, Abomanation.TailSpacing);
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            if (!AbomanationChain.Lashing(NPC))
                return AbomanationChain.Touching(NPC, target, 26f);

            // Its own cooldown slot, not the general one: the crack lands through the
            // immunity an earlier brush from the head or the coil left behind. See
            // AbomanationBody.
            cooldownSlot = ImmunityCooldownID.DD2OgreKnockback;

            // Swept while it is being thrown: at that speed a point test misses.
            return AbomanationChain.Sweeping(NPC, target, 26f);
        }

        /// <summary>The lash hits harder than the body, and harder the nearer the tail: that is what is moving.</summary>
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            if (AbomanationChain.Lashing(NPC))
                modifiers.SourceDamage *= AbomanationChain.LashDamage(NPC);
        }

        /// <summary>And it throws you, shield or no shield. See AbomanationChain.Throw.</summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            if (AbomanationChain.Lashing(NPC))
                AbomanationChain.Throw(NPC, target);
        }

        /// <summary>See AbomanationBody: the directional blood is local, the dust is for everyone.</summary>
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            AbomanationChain.Slashed(NPC, player, item, hit);
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            AbomanationChain.Pierced(NPC, projectile, hit);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 3; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            DrawWhole(spriteBatch, screenPos, NPC.GetAlpha(drawColor));
            return false;
        }

        /// <summary>The sprite and nothing else. No arms above it, none under it, so there is no pass before this one and none after it.</summary>
        public void DrawWhole(SpriteBatch spriteBatch, Vector2 screenPos, Color colour)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            spriteBatch.Draw(texture, NPC.Center - screenPos, null, colour,
                NPC.rotation, texture.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);
        }

        /// <summary>One outline round the whole animal, from whichever part is drawn first. See AbomanationChain.Silhouette.</summary>
        public void DrawCCBypassOutline(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (AbomanationChain.DrawsFirst(NPC))
                AbomanationChain.Silhouette(spriteBatch, NPC, screenPos);
        }
    }
}
