using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Summoner
{
    /// <summary>
    /// Bone Squad, by request: "a summoner weapon ... post plantera ... Summons a squad of minions
    /// using their AIs and defaults: Bone Lee, Giant Cursed Skull, Rusty Armored bones or Blue armor
    /// bones or hell armored bones x2 different variation (speed by +0.6f5), necromancer or ragged
    /// caster or diabolist (boost shoot frequency), skeleton archer. (all boost shoot speed) / All
    /// for the cost of 5.5 minions"; "texture from the game files".
    ///
    /// Each use calls the whole squad (BoneSquadMinion): Bone Lee, two Armored Bones of different
    /// kinds, a caster (one of the six), a Skeleton Archer and a Giant Cursed Skull, 5.5 minion slots
    /// in all; used again, it sends that squad away and calls a new one, picked anew (the user's
    /// answer: "One squad, re-rolled"). It cannot be used with fewer than 5.5 slots; with other
    /// minions out, as many of them go as the game sends away for any staff to make room
    /// (ItemID.Sets.StaffMinionSlotsRequired). Each skeleton hurts as the game's own does, its
    /// spells and arrows too (normal mode), as much more as this staff's damage is over Damage (its
    /// prefix), raised by summon damage.
    ///
    /// Mine: used as the game's own summoning staffs are (36 ticks with a swing, 10 mana, used again
    /// while held, the summoning sound most of them make), the Pygmy Staff's knockback of 3, Yellow
    /// and 10 gold, as the Dungeon's own spell staffs after Plantera are (the Shadowbeam Staff: Yellow,
    /// 6 gold); the squad comes up at the cursor, or at the summoner's feet when the cursor is in
    /// the ground (Hoard of Undead's). Its picture is the game's Skull, with the squad's outline
    /// round it (by request: "Skull with that green colors glow"), in the inventory, on the ground
    /// and in hand.
    /// </summary>
    public class BoneSquad : ModItem
    {
        /// <summary>The staff's damage for a squad that hurts as the game's skeletons do; about their middle (Bone Lee 90, the Giant Cursed Skull 60, the rest 35 to 85).</summary>
        public const int Damage = 60;

        /// <summary>The Pygmy Staff's.</summary>
        public const float Knockback = 3f;

        /// <summary>By request: "All for the cost of 5.5 minions".</summary>
        public const float Slots = 5.5f;

        /// <summary>The game's Skull, the vanity mask (by request).</summary>
        public override string Texture => "Terraria/Images/Item_" + ItemID.Skull;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // As the game has most of its own summoning staffs: a gamepad can summon anywhere on the screen.
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Type] = true;

            // The room the game makes for it, sending other minions away (Player.FreeUpPetsAndMinions).
            ItemID.Sets.StaffMinionSlotsRequired[Type] = Slots;
        }

        public override void SetDefaults()
        {
            // The game's Skull's size, whose picture it has.
            Item.width = 28;
            Item.height = 20;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Summon;
            Item.knockBack = Knockback;
            Item.mana = 10;

            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.noMelee = true;

            Item.buffType = ModContent.BuffType<BoneSquadBuff>();
            Item.shoot = ModContent.ProjectileType<BoneSquadBoneLee>();

            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 10);
        }

        /// <summary>The squad its owner has out: every one of theirs, whatever the kind.</summary>
        public static bool Member(Projectile projectile, int owner) =>
            projectile.active && projectile.owner == owner && projectile.ModProjectile is BoneSquadMinion;

        /// <summary>
        /// Not with fewer than 5.5 slots. Otherwise the squad out takes no slots from now until the
        /// item has been used (BoneSquadPlayer gives them back after; so does each of them every
        /// tick): it is about to go, and the game must not send other minions away for its room.
        /// </summary>
        public override bool CanUseItem(Player player)
        {
            if (player.maxMinions < Slots)
                return false;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Member(Main.projectile[i], player.whoAmI))
                    Main.projectile[i].minionSlots = 0f;
            }

            return true;
        }

        /// <summary>Where the squad comes up: the cursor, or the summoner's feet if the cursor is in the ground (a walker's room).</summary>
        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            Vector2 size = new Vector2(18f, 40f);
            position = Main.MouseWorld;

            if (Collision.SolidCollision(position - size / 2f, (int)size.X, (int)size.Y))
                position = new Vector2(player.Center.X, player.Bottom.Y - size.Y / 2f);

            velocity = Vector2.Zero;
        }

        /// <summary>
        /// The buff that keeps them; the squad out sent away; and a new one, the two Armored Bones
        /// and the caster picked here (ai[0]: only the summoner's machine uses an item, and the squad
        /// goes to everyone else with it).
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Member(Main.projectile[i], player.whoAmI))
                    Main.projectile[i].Kill();
            }

            int first = Main.rand.Next(NPCID.RustyArmoredBonesAxe, NPCID.HellArmoredBonesSword + 1);
            int second;
            do
                second = Main.rand.Next(NPCID.RustyArmoredBonesAxe, NPCID.HellArmoredBonesSword + 1);
            while (second == first);

            int caster = Main.rand.Next(NPCID.RaggedCaster, NPCID.DiabolistWhite + 1);

            Call(player, source, position, velocity, knockback, ModContent.ProjectileType<BoneSquadBoneLee>(), NPCID.BoneLee, 0);
            Call(player, source, position, velocity, knockback, ModContent.ProjectileType<BoneSquadArmored>(), first, first);
            Call(player, source, position, velocity, knockback, ModContent.ProjectileType<BoneSquadArmored>(), second, second);
            Call(player, source, position, velocity, knockback, ModContent.ProjectileType<BoneSquadCaster>(), caster, caster);
            Call(player, source, position, velocity, knockback, ModContent.ProjectileType<BoneSquadArcher>(), NPCID.SkeletonArcher, 0);
            Call(player, source, position, velocity, knockback, ModContent.ProjectileType<BoneSquadSkull>(), NPCID.GiantCursedSkull, 0);

            return false;
        }

        /// <summary>One of the squad: its NPC's own damage, as much more as the staff's is over Damage; raised by summon damage each tick by the game, as every minion's.</summary>
        private void Call(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, float knockback, int type, int kind, int ai0)
        {
            int own = (int)Math.Round(BoneSquadMinion.DamageOf(kind) * (double)Item.damage / Damage);
            int damage = (int)player.GetTotalDamage(DamageClass.Summon).ApplyTo(own);

            Projectile minion = Projectile.NewProjectileDirect(source, position, velocity, type, damage, knockback, player.whoAmI, ai0, 0f, 0f);
            minion.originalDamage = own;
        }

        /// <summary>The squad's outline round the Skull in the inventory, where and as the game draws the item (ItemSlot.Draw).</summary>
        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            Texture2D own = TextureAssets.Item[Type].Value;
            BoneSquadLook.Ring ring = BoneSquadLook.RingOf(own, frame, null, true);
            BoneSquadLook.DrawRing(spriteBatch, ring, position, frame, 0f, origin, scale, SpriteEffects.None);
        }

        /// <summary>The outline round it on the ground, where and as the game draws an item there (Main.DrawItem), as faint as the item.</summary>
        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
        {
            Texture2D own = TextureAssets.Item[Type].Value;
            Rectangle frame = new Rectangle(0, 0, own.Width, own.Height);
            Vector2 half = new Vector2(frame.Width, frame.Height) / 2f;
            Vector2 at = Item.position - Main.screenPosition + half + new Vector2((float)(Item.width / 2) - half.X, Item.height - frame.Height);

            BoneSquadLook.Ring ring = BoneSquadLook.RingOf(own, frame, null, true);
            BoneSquadLook.DrawRing(spriteBatch, ring, at, frame, rotation, half, scale, SpriteEffects.None, alphaColor.A / 255f);
        }

        // Recipes stay commented out for now. As asked: 400 Bones, a Skull, 30 Souls of Night, 30 Souls of
        // Fright and a Pygmy Staff, at a Mythril Anvil (an Orichalcum Anvil is the same tile):
        //
        // public override void AddRecipes()
        // {
        //     CreateRecipe()
        //         .AddIngredient(ItemID.Bone, 400)
        //         .AddIngredient(ItemID.Skull)
        //         .AddIngredient(ItemID.SoulofNight, 30)
        //         .AddIngredient(ItemID.SoulofFright, 30)
        //         .AddIngredient(ItemID.PygmyStaff)
        //         .AddTile(TileID.MythrilAnvil)
        //         .Register();
        // }
    }

    /// <summary>
    /// Gives the squad back its slots once its owner's item check is over: CanUseItem took them,
    /// so that the game would not send other minions away for the room of a squad about to go. If
    /// the staff was not used after all (no mana), the squad is still there, and counts again.
    /// </summary>
    public class BoneSquadPlayer : ModPlayer
    {
        public override void PostItemCheck()
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (BoneSquad.Member(projectile, Player.whoAmI))
                    projectile.minionSlots = ((BoneSquadMinion)projectile.ModProjectile).Slots;
            }
        }
    }

    /// <summary>
    /// The squad's outline round the Skull in hand: wherever the game drew the item this time
    /// (PlayerDrawLayers.HeldItem), at the same place, turn, size and flip, as faint as the item is
    /// drawn; with the player, so that a dye or a mirror does to it what it does to the item. Not
    /// on the player's afterimages.
    /// </summary>
    public class BoneSquadHeldGlow : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.heldItem != null && drawInfo.heldItem.type == ModContent.ItemType<BoneSquad>();

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Texture2D own = TextureAssets.Item[ModContent.ItemType<BoneSquad>()].Value;
            int count = drawInfo.DrawDataCache.Count;

            for (int i = 0; i < count; i++)
            {
                DrawData data = drawInfo.DrawDataCache[i];
                if (data.texture != own || data.useDestinationRectangle)
                    continue;

                Rectangle source = data.sourceRect ?? new Rectangle(0, 0, own.Width, own.Height);
                BoneSquadLook.Ring ring = BoneSquadLook.RingOf(own, source, null, true);
                BoneSquadLook.RingData(drawInfo.DrawDataCache, ring, data.position, source, data.rotation, data.origin, data.scale.X, data.effect, data.shader, data.color.A / 255f);
            }
        }
    }
}
