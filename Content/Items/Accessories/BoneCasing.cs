using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Bone Casing, by request: "15 min cool down or by respawn: Gives a ribcage shaped shield over
    /// the entire body, wont disappear until you die. ribcage has 100HP, every attack on the player
    /// will be absorbed by this and it acts like a dark night's shield from clash royale. Even if
    /// its a huge damage attack, it will still leave the player standing and just breaks the
    /// ribcage. Obtained 1/4 chance dropped when beating Skeletron." Then: "Change bone Casing
    /// cooldown to 1.5 mins".
    ///
    /// Worn, a cage of ribs stands around its wearer with 100 life of its own, and takes every hit
    /// in their place: what the hit would have taken off them (after their defense), off the ribs
    /// instead. The hit that breaks the ribs is taken whole, however big; what is left of it goes
    /// nowhere, as the shield in the game named does it. Then Splintered: a minute and a half before
    /// the ribs stand again, or until their wearer next comes back from death. Hurt but not broken,
    /// the ribs keep what they have left (saved with the player), and nothing mends them but breaking
    /// and the wait, or a respawn, which brings a fresh cage.
    ///
    /// What it takes, and what it leaves, are in <see cref="BoneCasingPlayer"/>. Where it comes from:
    /// Skeletron, 1 in 4, in its bag in expert and master (<see cref="BoneCasingSource"/>). Mine:
    /// Green rarity, as Skeletron's other drops; its pictures are stand-ins (PlaceholderTextures.md).
    /// </summary>
    public class BoneCasing : ModItem
    {
        /// <summary>One in this many Skeletrons (or their bags) gives one.</summary>
        public const int DropChance = 4;

        /// <summary>Beside the mod's other accessories in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 1);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            BoneCasingPlayer casing = player.GetModPlayer<BoneCasingPlayer>();
            casing.Worn = true;
            casing.Shown |= !hideVisual;
        }

        // Recipes stay commented out for now; none was asked for. It drops from Skeletron (below).
    }

    /// <summary>Skeletron's half of where it comes from: 1 in 4, in classic only, the way vanilla drops what is in its bag.</summary>
    public class BoneCasingSource : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.SkeletronHead)
                npcLoot.Add(ItemDropRule.ByCondition(new Conditions.NotExpert(), ModContent.ItemType<BoneCasing>(), BoneCasing.DropChance));
        }
    }

    /// <summary>The expert and master half: Skeletron's bag, 1 in 4.</summary>
    public class BoneCasingBag : GlobalItem
    {
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            if (item.type == ItemID.SkeletronBossBag)
                itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<BoneCasing>(), BoneCasing.DropChance));
        }
    }

    /// <summary>
    /// The ribs: how much is left of them, what they take, when they break and when they stand again.
    ///
    /// What they take: every hit through the game's Player.Hurt that something dealt: an enemy, a
    /// shot, any boss of the mod's own. Not what the world does by itself (the game's
    /// PlayerDeathReason.ByOther: a fall, lava, spikes and the other blocks that hurt, being turned
    /// to stone), and nothing that is not a hit (burning, poison and the other harm a second, a
    /// drowning). And not a hit from another player: the game works those out on the striker's
    /// machine, where no dodge of anyone's is tried, the game's own included.
    ///
    /// How: a hit that can be dodged comes to <see cref="ConsumableDodge"/>, after every dodge of the
    /// game's own has had its try (the Black Belt's, the Brain of Confusion's, the shadow dodge), so
    /// the ribs never pay for a hit one of those would have turned. A hit that cannot be dodged
    /// (HurtModifiers.Dodgeable false: the drains of the Wall, of Queen Slime's grip and of the
    /// slimes' in Legends, and Foigh's blows that go through whatever would turn them) is
    /// cancelled where its damage is known (<see cref="ModifyHurt"/>). Either way the ribs lose
    /// what the hit would have taken off their wearer, and their wearer gets the time the game
    /// gives after a hit that lands, before the next can (Player.Hurt's own: two thirds of a
    /// second, a third of one for a hit of 1, twice as long with the Cross Necklace, kept per kind
    /// of hit as the game keeps it), so a touching enemy wears the ribs down as fast as it would
    /// have worn them, and no faster. Their wearer is not hurt, so their life goes on mending as it
    /// was.
    ///
    /// Only the wearer's own machine decides; what is left is sent to the others (a
    /// <see cref="PADPacket.BoneCasing"/> packet each time it changes, through SendClientChanges),
    /// and each machine shows the blow, the break and the ribs standing again as it hears of them.
    /// </summary>
    public class BoneCasingPlayer : ModPlayer
    {
        /// <summary>The ribs' life, whole.</summary>
        public const int Whole = 100;

        /// <summary>How long the flash of a blow lasts, in ticks.</summary>
        public const int FlashTime = 12;

        /// <summary>How long the ribs take to stand, in ticks, once they are whole again.</summary>
        public const int FormTime = 36;

        /// <summary>What is left of the ribs' life: 0 while they mend (Splintered). Saved.</summary>
        public int Ribs = Whole;

        /// <summary>Worn this tick (BoneCasing.UpdateAccessory).</summary>
        public bool Worn;

        /// <summary>Worn this tick in a slot that shows (the slot's eye open).</summary>
        public bool Shown;

        /// <summary>Worn, and seen, when everything worn last had its say (PostUpdateEquips); held across the tick.</summary>
        private bool wearing;
        private bool showing;

        /// <summary>Ticks left of the last blow's flash, and of the ribs coming together. Every machine keeps its own.</summary>
        private int flash;
        private int forming;

        /// <summary>Which way the last blow pushed (its HitDirection): the way the bone flies.</summary>
        private int lastDirection;

        /// <summary>Whether the ribs stand around a wearer now and take the next hit.</summary>
        public bool Standing => wearing && Ribs > 0;

        public override void ResetEffects()
        {
            Worn = false;
            Shown = false;
        }

        public override void PostUpdateEquips()
        {
            wearing = Worn;
            showing = Shown;

            // The ribs' buff: no time on it, only what is left of them (BoneCasingBuff). Put on by
            // the wearer's machine, and the game shows it to the others.
            if (Player.whoAmI == Main.myPlayer && Standing)
                Player.AddBuff(ModContent.BuffType<BoneCasingBuff>(), 2);
        }

        public override void PostUpdate()
        {
            if (flash > 0)
                flash--;

            if (forming > 0)
                forming--;

            // The wait over: whole again. Only the wearer's machine decides; the rest hear of it.
            if (Player.whoAmI == Main.myPlayer && Ribs <= 0 && !Player.dead && !Player.HasBuff(ModContent.BuffType<Splintered>()))
                SetRibs(Whole, 0);
        }

        /// <summary>A fresh cage at every respawn, the wait or no wait ("or by respawn").</summary>
        public override void OnRespawn()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            Player.ClearBuff(ModContent.BuffType<Splintered>());
            SetRibs(Whole, 0);
        }

        /// <summary>A hit that can be dodged, after the game's own dodges have had their try.</summary>
        public override bool ConsumableDodge(Player.HurtInfo info)
        {
            if (!Standing || !Takes(info.DamageSource))
                return false;

            Take(info);
            return true;
        }

        /// <summary>
        /// A hit that cannot be dodged never comes to ConsumableDodge. The ribs take it once its
        /// damage is known, the last thing before it would land, by cancelling it.
        /// </summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (!modifiers.Dodgeable && Player.whoAmI == Main.myPlayer && Standing && Takes(modifiers.DamageSource))
                modifiers.ModifyHurtInfo += TakeUndodgeable;
        }

        private void TakeUndodgeable(ref Player.HurtInfo info)
        {
            if (info.Cancelled || !Standing)
                return;

            Take(info);
            info.Cancelled = true;
        }

        /// <summary>Everything something dealt; not what the world does by itself (PlayerDeathReason.ByOther).</summary>
        private static bool Takes(PlayerDeathReason reason) => reason == null || reason.SourceOtherIndex < 0;

        /// <summary>The ribs take the hit: all of it off them, none of it off their wearer.</summary>
        private void Take(Player.HurtInfo info)
        {
            GiveHitTime(info);

            int left = Math.Max(0, Ribs - info.Damage);

            if (left == 0)
                Player.AddBuff(ModContent.BuffType<Splintered>(), Splintered.Duration);

            SetRibs(left, info.HitDirection);
        }

        /// <summary>
        /// The time Player.Hurt gives after a hit that lands, before the next can, as it would have
        /// given it for this one: per kind of hit (the hit's cooldown counter), for a hit of 1 half as
        /// long, with the Cross Necklace (longInvince) twice as long, and a hit from another player
        /// eight ticks.
        /// </summary>
        private void GiveHitTime(Player.HurtInfo info)
        {
            int time = info.Damage == 1 ? (Player.longInvince ? 40 : 20) : (Player.longInvince ? 80 : 40);

            switch (info.CooldownCounter)
            {
                case ImmunityCooldownID.General:
                    Player.immune = true;
                    Player.immuneTime = info.PvP ? 8 : time;
                    break;

                case ImmunityCooldownID.TileContactDamage:
                case ImmunityCooldownID.Bosses:
                case ImmunityCooldownID.WrongBugNet:
                case ImmunityCooldownID.Lava:
                    Player.hurtCooldowns[info.CooldownCounter] = time;
                    break;
            }
        }

        /// <summary>What is left of the ribs, changed on this machine: shown here, and the others told (SendClientChanges).</summary>
        private void SetRibs(int ribs, int direction)
        {
            int before = Ribs;
            Ribs = ribs;
            lastDirection = direction;
            Changed(before, ribs, direction);
        }

        /// <summary>
        /// What every machine shows when the ribs change: a blow (a crack of bone, a spray of it the
        /// way the hit pushed, the cage shaken and lit up, and the life it took, in bone white); the
        /// break (the cage bursting into its pieces with a skeleton's fall); the ribs standing
        /// again (bone dust drawn in, the cage growing into place, the rattle of a skeleton
        /// rising). Nothing on a server, nothing for a cage not worn.
        /// </summary>
        public void Changed(int before, int after, int direction)
        {
            if (Main.dedServ || before == after || !wearing)
                return;

            Vector2 centre = Player.Center;

            if (after > before)
            {
                forming = FormTime;
                flash = 0;

                SoundEngine.PlaySound(SoundID.DD2_SkeletonSummoned with { Volume = 0.55f, Pitch = 0.2f }, centre);

                for (int i = 0; i < 24; i++)
                {
                    Vector2 from = Main.rand.NextVector2CircularEdge(36f, 44f);
                    Dust dust = Dust.NewDustPerfect(centre + from, DustID.Bone, -from * 0.08f, 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                    dust.noGravity = true;
                }

                return;
            }

            int taken = before - after;
            CombatText.NewText(Player.Hitbox, BoneWhite, taken, false, true);

            if (after > 0)
            {
                flash = FlashTime;

                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.9f, PitchVariance = 0.25f }, centre);

                for (int i = 0; i < 8; i++)
                {
                    Vector2 velocity = new Vector2(direction * Main.rand.NextFloat(1f, 3.5f), Main.rand.NextFloat(-2.5f, 0.5f));
                    Dust.NewDustPerfect(centre + Main.rand.NextVector2Circular(12f, 18f), DustID.Bone, velocity, 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                }

                return;
            }

            // Broken.
            flash = 0;
            forming = 0;

            SoundEngine.PlaySound(SoundID.NPCDeath2 with { Volume = 1f }, centre);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.7f, Pitch = -0.4f }, centre);

            for (int i = 0; i < 30; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(4.5f, 4.5f) + new Vector2(direction * 1.5f, -1.5f);
                Dust.NewDustPerfect(centre + Main.rand.NextVector2Circular(14f, 20f), DustID.Bone, velocity, 0, default, Main.rand.NextFloat(0.9f, 1.5f));
            }

            BoneCasingShards.Burst(Player, direction);
        }

        /// <summary>The colour of the life the ribs take, as numbers over their wearer.</summary>
        public static readonly Color BoneWhite = new Color(236, 226, 196);

        // ---- the cage, as the draw layers see it -------------------------------------------

        /// <summary>Whether to draw the cage at all: worn, seen, standing, and the wearer alive.</summary>
        public bool Drawn => showing && Standing && !Player.dead && !Player.ghost;

        /// <summary>Which of the three pictures: whole, cracked (two thirds left or less), broken through (a third or less).</summary>
        public int Stage => Ribs > Whole * 2 / 3 ? 0 : Ribs > Whole / 3 ? 1 : 2;

        /// <summary>The flash of the last blow, 1 at its start, 0 when it is over.</summary>
        public float Flash => flash / (float)FlashTime;

        /// <summary>How far the cage has come together, 0 at its start, 1 when it stands.</summary>
        public float Formed => 1f - forming / (float)FormTime;

        /// <summary>Which way the last blow pushed.</summary>
        public int LastDirection => lastDirection;

        // ---- saving and the network ---------------------------------------------------------

        public override void SaveData(TagCompound tag)
        {
            if (Ribs != Whole)
                tag["ribs"] = Ribs;
        }

        public override void LoadData(TagCompound tag)
        {
            Ribs = tag.ContainsKey("ribs") ? Math.Clamp(tag.GetInt("ribs"), 0, Whole) : Whole;
        }

        public override void CopyClientState(ModPlayer targetCopy)
        {
            ((BoneCasingPlayer)targetCopy).Ribs = Ribs;
        }

        public override void SendClientChanges(ModPlayer clientPlayer)
        {
            if (((BoneCasingPlayer)clientPlayer).Ribs != Ribs)
                Send(-1, Player.whoAmI, true);
        }

        /// <summary>On joining: told as it is, nothing shown for it.</summary>
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            Send(toWho, fromWho, false);
        }

        private void Send(int toWho, int fromWho, bool show)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)PADPacket.BoneCasing);
            packet.Write((byte)Player.whoAmI);
            packet.Write((byte)Ribs);
            packet.Write((sbyte)Math.Sign(lastDirection));
            packet.Write(show);
            packet.Send(toWho, fromWho);
        }

        /// <summary>
        /// What is left of a player's ribs. To the server from that player's own machine (it speaks
        /// for nobody else), and on from there to every other.
        /// </summary>
        public static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            int who = reader.ReadByte();
            int ribs = Math.Clamp((int)reader.ReadByte(), 0, Whole);
            int direction = reader.ReadSByte();
            bool show = reader.ReadBoolean();

            if (Main.netMode == NetmodeID.Server)
                who = whoAmI;

            if (who < 0 || who >= Main.maxPlayers)
                return;

            BoneCasingPlayer casing = Main.player[who].GetModPlayer<BoneCasingPlayer>();
            int before = casing.Ribs;
            casing.Ribs = ribs;
            casing.lastDirection = direction;

            if (Main.netMode == NetmodeID.Server)
                casing.Send(-1, who, show);
            else if (show)
                casing.Changed(before, ribs, direction);
        }
    }

    /// <summary>
    /// The pieces the ribs break into: three of their own pictures (BoneCasingShard1 to 3, in
    /// Content/Gores, which tModLoader takes as gores by their folder's name), thrown up and out
    /// the way the last blow pushed, falling and lying as the game's gores do. Every machine throws
    /// its own.
    /// </summary>
    public static class BoneCasingShards
    {
        private const int Pieces = 7;

        public static void Burst(Player player, int direction)
        {
            Mod mod = ModContent.GetInstance<PisAndDag>();
            int[] kinds =
            {
                mod.Find<ModGore>("BoneCasingShard1").Type,
                mod.Find<ModGore>("BoneCasingShard2").Type,
                mod.Find<ModGore>("BoneCasingShard3").Type,
            };

            for (int i = 0; i < Pieces; i++)
            {
                Vector2 at = player.Center + Main.rand.NextVector2Circular(12f, 18f) - new Vector2(6f);
                Vector2 velocity = new Vector2(direction * Main.rand.NextFloat(0.5f, 3f) + Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4.5f, -1.5f));
                Gore.NewGore(player.GetSource_FromThis(), at, velocity, kinds[i % kinds.Length], 1f);
            }
        }
    }

    /// <summary>
    /// The cage, in two halves around its wearer: the far side of the ribs and the spine behind
    /// everything of theirs but their wings and what hangs on their back (before the body's skin),
    /// the near side and the breastbone over everything, as the game draws its Ice Barrier.
    /// One picture of a player's frame's size (BoneCasingRibs.png: the far side, then the near,
    /// each whole, cracked and broken through), centred on the body the way the Ice Barrier is,
    /// turned with its wearer. Lit where its wearer stands; whiter for a moment after a blow, and
    /// shaken a pixel from side to side, first the way the blow pushed; at its start, growing into
    /// place and fading in.
    /// </summary>
    public abstract class BoneCasingLayer : PlayerDrawLayer
    {
        private static Asset<Texture2D> ribs;

        protected abstract int Side { get; }

        public override void Load()
        {
            if (!Main.dedServ)
                ribs = ModContent.Request<Texture2D>("PisAndDag/Content/Items/Accessories/BoneCasingRibs");
        }

        public override void Unload()
        {
            ribs = null;
        }

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && !drawInfo.headOnlyRender && drawInfo.drawPlayer.GetModPlayer<BoneCasingPlayer>().Drawn;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (ribs == null)
                return;

            Player player = drawInfo.drawPlayer;
            BoneCasingPlayer casing = player.GetModPlayer<BoneCasingPlayer>();
            Texture2D texture = ribs.Value;
            Rectangle frame = texture.Frame(2, 3, Side, casing.Stage);

            // The body's middle, as the game finds it for the Ice Barrier (DrawPlayer_35_IceBarrier).
            Vector2 at = new Vector2(
                (int)(drawInfo.Position.X - Main.screenPosition.X - player.bodyFrame.Width / 2 + player.width / 2),
                (int)(drawInfo.Position.Y - Main.screenPosition.Y + player.height - player.bodyFrame.Height + 4))
                + player.bodyPosition + new Vector2(player.bodyFrame.Width / 2, player.bodyFrame.Height / 2);

            float flash = casing.Flash;

            if (flash > 0.5f)
                at.X += (casing.LastDirection < 0 ? -1 : 1) * ((int)(flash * FlashShakes) % 2 == 0 ? 1 : -1);

            float formed = casing.Formed;
            float scale = MathHelper.Lerp(1.25f, 1f, Ease(formed));

            Color light = Lighting.GetColor((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
            Color colour = Color.Lerp(light, Color.White, flash * 0.85f) * formed;

            drawInfo.DrawDataCache.Add(new DrawData(texture, at, frame, colour, 0f, frame.Size() / 2f, scale, drawInfo.playerEffect, 0f));
        }

        /// <summary>How many times the cage shakes from side to side while a blow's flash is in its first half.</summary>
        private const float FlashShakes = 12f;

        private static float Ease(float t) => 1f - (1f - t) * (1f - t);
    }

    /// <summary>The far side of the ribs and the spine, behind their wearer.</summary>
    public class BoneCasingBack : BoneCasingLayer
    {
        protected override int Side => 0;

        public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Skin);
    }

    /// <summary>The near side of the ribs and the breastbone, over everything, as the Ice Barrier is.</summary>
    public class BoneCasingFront : BoneCasingLayer
    {
        protected override int Side => 1;

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.IceBarrier);
    }
}
