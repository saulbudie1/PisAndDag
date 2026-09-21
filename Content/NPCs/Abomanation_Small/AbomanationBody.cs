using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
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
    public class AbomanationBody : ModNPC, ICCBypassOutline
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

            AbomanationChain.Flag(NPC);

            float spacing = leader.type == ModContent.NPCType<AbomanationHead>()
                ? Abomanation.HeadSpacing
                : Abomanation.BodySpacing;

            // The whip: while the head cracks, the body is a chain with momentum and
            // swings after it rather than following.
            if (AbomanationChain.Lashing(NPC))
            {
                AbomanationChain.Swing(NPC, leader, spacing);
                return;
            }

            AbomanationChain.Follow(NPC, leader, spacing);
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            if (!AbomanationChain.Lashing(NPC))
                return AbomanationChain.Touching(NPC, target, 26f);

            // Its own cooldown slot rather than the general one, so the lash lands
            // through the immunity a hit from the head or the coil left a moment ago.
            // The general immunity was hiding the throw entirely on anyone the head had
            // just brushed: the crack came inside their forty ticks and nothing landed.
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

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 3; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);
        }

        /// <summary>This segment's place in the chain. 0 is the one behind the head.</summary>
        private int Place => (int)NPC.ai[3];

        /// <summary>
        /// The head's arm seed, which every segment deals its sprites from.
        ///
        /// Read off the head rather than kept here, because it has to be the same number
        /// for the whole animal and the head is the one part guaranteed to exist while any
        /// of the rest does. Zero if the head has gone, which only matters for the frame
        /// or two before Tether takes this segment off the board.
        /// </summary>
        private int ArmSeed
        {
            get
            {
                int head = AbomanationChain.Head(NPC);

                if (head < 0 || head >= Main.maxNPCs)
                    return 0;

                return Main.npc[head].ModNPC is AbomanationHead boss ? boss.ArmSeed : 0;
            }
        }

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
            DrawWhole(spriteBatch, screenPos, NPC.GetAlpha(drawColor));
            return false;
        }

        /// <summary>
        /// The segment, complete, in one colour at one screen position. The ordinary
        /// draw and the outline both come through here, so the outline is of exactly
        /// what is drawn.
        /// </summary>
        public void DrawWhole(SpriteBatch spriteBatch, Vector2 screenPos, Color colour)
        {
            int place = Place;

            Texture2D texture = Sprite(place);

            SpriteEffects flip = AbomanationChain.Flipped(NPC)
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

            AbomanationArms.Bottom(spriteBatch, NPC, screenPos, colour, place,
                leftCount: AbomanationArms.BottomCount(place, 0),
                rightCount: AbomanationArms.BottomCount(place, 1),
                span: AbomanationArms.BottomSpan, swing: true, armSeed: ArmSeed);

            // Mirrored or not, rolled once at spawn and carried in ai[0] so it survives to
            // every client and holds for the whole fight. Five sprites over fifteen
            // segments means each one is seen three times; mirroring half of them on a
            // coin means the repeat is much harder to catch, and it costs nothing because
            // the flesh reads the same either way round.
            //
            // The arms are drawn outside this call and are NOT mirrored with it. They have
            // their own handedness and a left arm has to stay a left arm.
            spriteBatch.Draw(texture, NPC.Center - screenPos, null, colour,
                NPC.rotation, texture.Size() * 0.5f, NPC.scale, flip, 0f);

            AbomanationArms.TopScattered(spriteBatch, NPC, screenPos, colour, seed: place);
        }

        /// <summary>
        /// The tell is one outline round the whole animal, not a ring round this piece:
        /// the first part the draw loop reaches draws it for all of them, under
        /// everything. See AbomanationChain.Silhouette.
        /// </summary>
        public void DrawCCBypassOutline(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (AbomanationChain.DrawsFirst(NPC))
                AbomanationChain.Silhouette(spriteBatch, NPC, screenPos);
        }
    }
}
