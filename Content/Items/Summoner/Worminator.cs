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
    /// Worminator, by request: "Create new Summoner Weapon: Worminator, prehardmode dropped by giant worm 1/5
    /// chance. Summons a worm, base 3 body segment.(+1 head 1 tail) each additional minion adds 1 segment."
    /// The worm and how it moves are in Content/Projectiles/Summoner/WorminatorSegments.cs: as the Stardust Dragon
    /// is put together, through the ground as the Giant Worm goes, at twice its pace, every segment 8 damage.
    ///
    /// Used as the game's own summoning staffs are (mine, their numbers, as Hoard of Undead's: 36 ticks with a
    /// swing, 10 mana, used again while it is held, the summoning sound most of them make). The first summon
    /// makes the worm, taking one minion slot, coming up out of the ground under the cursor; each one after it
    /// puts one more segment in, before the tail, taking one more.
    ///
    /// The texture is a placeholder, the game's Worm bait (PlaceholderTextures.md).
    /// </summary>
    public class Worminator : ModItem
    {
        /// <summary>"8 damage all around": every segment's.</summary>
        public const int Damage = 8;

        /// <summary>Mine: Hoard of Undead's.</summary>
        public const float Knockback = 2f;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // As the game has most of its own summoning staffs: a gamepad can summon anywhere on the screen.
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
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

            // The body is what a summon adds, and what takes the slot: the game makes room for it by letting
            // another minion go, as it does for any summon (ProjectileID.Sets.MinionSacrificable).
            Item.buffType = ModContent.BuffType<WorminatorBuff>();
            Item.shoot = ModContent.ProjectileType<WorminatorBody>();

            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(gold: 1);
        }

        /// <summary>
        /// The buff that keeps the worm, and the worm, or one more segment of it (WorminatorSegment.Summon). Only
        /// the summoner's machine uses an item, and the segments go to everyone else with it. Their damage is worked
        /// out from the item's own each tick by the game, as every minion's.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);
            WorminatorSegment.Summon(player, source, damage, knockback, Item.damage);
            return false;
        }
    }

    /// <summary>
    /// The Worminator from the Giant Worm, one time in 5, by request ("dropped by giant worm 1/5 chance"). A
    /// Giant Worm's loot is its head's (NPCID.GiantWormHead): the head is the one of its segments that drops it.
    /// </summary>
    public class WorminatorDrop : GlobalNPC
    {
        /// <summary>One time in this many.</summary>
        public const int Chance = 5;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.GiantWormHead)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Worminator>(), Chance));
        }
    }
}
