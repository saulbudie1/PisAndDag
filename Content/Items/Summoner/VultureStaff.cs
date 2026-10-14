using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Summoner
{
    /// <summary>
    /// Vulture Staff, by request: "New summoner weapon: Vulture Staff: Summon minion vultures to fight for you
    /// (modify vultures textures) idle: sit on ur 2 shoulders and stack into 2 towers. I guess like finch staff but
    /// you can use your creativity to make it more interesting." The vulture is VultureMinion, one minion slot each.
    ///
    /// Mine: its numbers (12 damage, a little over the Finch Staff's 7 and the Slime Staff's 8, the game's
    /// first two; knockback 3; the rest as Hoard of Undead's: 36 ticks with a swing, 10 mana, used again while
    /// it is held, the summoning sound most staffs make); the vulture comes out where the cursor is and flies to
    /// its owner's shoulder. Nothing gives it yet: the request names no source (RELEASE_TODO), and one way it
    /// could come is written out below, left off.
    ///
    /// The texture is a placeholder: the Vulture's own perched frame, cut to the item (PlaceholderTextures.md).
    /// </summary>
    public class VultureStaff : ModItem
    {
        /// <summary>Mine.</summary>
        public const int Damage = 18;

        /// <summary>Mine.</summary>
        public const float Knockback = 3f;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // As the game has most of its own summoning staffs: a gamepad can summon anywhere on the screen.
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 38;
            Item.height = 38;

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

            Item.buffType = ModContent.BuffType<VultureStaffBuff>();
            Item.shoot = ModContent.ProjectileType<VultureMinion>();

            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(gold: 1);
        }

        /// <summary>
        /// The buff that keeps them, and a vulture where the cursor is, flying to its shoulder (VultureMinion.Returning).
        /// Its damage is worked out from the item's own each tick by the game, as every minion's.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            Projectile vulture = Projectile.NewProjectileDirect(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI, VultureMinion.Returning);
            vulture.originalDamage = Item.damage;

            return false;
        }
    }

    // One way it could come, left off until the user decides (RELEASE_TODO): from Vultures, one time in 25, as
    // the Hoard of Undead comes from zombies. To use it, take the comment marks off this class and add
    // "using Terraria.GameContent.ItemDropRules;" above.

    public class VultureStaffDrop : GlobalNPC
    {
        public const int Chance = 40;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.Vulture)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<VultureStaff>(), Chance));
        }
    }
}
