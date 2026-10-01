using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// Visus: a long-hafted spear with an eye set in its head, from the Eye of Cthulhu, every time
    /// (VisusDrop: each player's own off the kill in classic, in the treasure bag in expert and
    /// master).
    ///
    /// Left click thrusts (VisusThrust): 52 damage, 7 tiles of reach, a 45-tick thrust, one at a
    /// time (no auto reuse), and every thrust carries its user 2 tiles forward. Right click raises
    /// it overhead and brings it down (VisusChop): triple damage, a 60-tick swing, and the next
    /// one not until 2 seconds after it ends (VisusPose.Rest).
    ///
    /// The item itself hits nothing and is never drawn in use (noMelee, noUseGraphic): the two
    /// projectiles are the weapon, and VisusLayer draws it in the hands, from its level or its
    /// diagonal sheet. The layered sources of its art are in Tools/Art/Visus.
    /// </summary>
    public class Visus : ModItem
    {
        public const int Damage = 52;
        public const int UseTicks = 45;
        public const float Knockback = 6.5f;

        /// <summary>The blow from overhead: how long it takes, and how much harder it hits.</summary>
        public const int ChopTicks = 60;
        public const float ChopDamage = 3f;
        public const float ChopKnockback = 1.5f;

        /// <summary>How long after a blow ends before the next can be struck: 2 seconds.</summary>
        public const int RestTicks = 120;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 68;
            Item.height = 68;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = UseTicks;
            Item.useAnimation = UseTicks;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<VisusThrust>();
            Item.shootSpeed = 1f;

            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 1);
        }

        public override bool AltFunctionUse(Player player) => true;

        /// <summary>
        /// One at a time: not while a thrust or a blow is still out (the blow outlasts its use
        /// time by the ticks it stood still on what it hit). The blow not until the last one has
        /// rested. altFunctionUse is already 2 here on a right click (ItemCheck_Inner sets it before
        /// it asks).
        /// </summary>
        public override bool CanUseItem(Player player)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<VisusThrust>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<VisusChop>()] > 0)
                return false;

            return player.altFunctionUse != 2 || player.GetModPlayer<VisusPose>().Rest <= 0;
        }

        // The blow takes ChopTicks where a thrust takes UseTicks: 45 x (60 / 45) truncates to exactly
        // 60 at base speed, and melee speed shortens both alike.
        public override float UseTimeMultiplier(Player player) => player.altFunctionUse == 2 ? ChopTicks / (float)UseTicks : 1f;

        public override float UseAnimationMultiplier(Player player) => player.altFunctionUse == 2 ? ChopTicks / (float)UseTicks : 1f;

        /// <summary>
        /// Sends out the thrust, or the blow, with how long the use lasts (ai[1]), so either runs
        /// exactly as long as the item is in use, melee speed and all. The blow is given which way
        /// it faces (ai[0]).
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            float duration = Math.Max(1, player.itemAnimationMax);

            if (player.altFunctionUse == 2)
            {
                float facing = velocity.X < 0f ? -1f : velocity.X > 0f ? 1f : player.direction;

                Projectile.NewProjectile(source, player.MountedCenter, new Vector2(facing, 0f), ModContent.ProjectileType<VisusChop>(),
                    (int)(damage * ChopDamage), knockback * ChopKnockback, player.whoAmI, facing, duration);

                return false;
            }

            Projectile.NewProjectile(source, player.MountedCenter, velocity.SafeNormalize(new Vector2(player.direction, 0f)),
                ModContent.ProjectileType<VisusThrust>(), damage, knockback, player.whoAmI, 0f, duration);

            return false;
        }
    }

    /// <summary>
    /// Where Visus comes from: the Eye of Cthulhu, every time. In classic, one for every player
    /// who fought it, each their own (PerPlayerDrop); in expert and master, in the treasure bag
    /// (VisusBag), as vanilla gives its own boss weapons.
    /// </summary>
    public class VisusDrop : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.EyeofCthulhu;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type != NPCID.EyeofCthulhu)
                return;

            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(new PerPlayerDrop(ModContent.ItemType<Visus>(), 1, 1));
            npcLoot.Add(classic);
        }
    }

    /// <summary>The expert and master half of VisusDrop: the Eye's treasure bag.</summary>
    public class VisusBag : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.EyeOfCthulhuBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            if (item.type == ItemID.EyeOfCthulhuBossBag)
                itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Visus>()));
        }
    }
}
