using System;
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
    /// Hoard of Undead, by request: a summoner's weapon before hardmode that summons a zombie to
    /// fight for you (HoardOfUndeadZombie), one minion slot each, 30 damage. Dropped by any zombie,
    /// one time in 50 (HoardOfUndeadDrop).
    ///
    /// Used as the game's own summoning staffs are (mine, their numbers: 36 ticks with a swing, 10
    /// mana, used again while it is held, the summoning sound most of them make), and the zombie
    /// comes up where the cursor is, as theirs do, unless the cursor is in the ground: then at the
    /// summoner's feet (mine).
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class HoardOfUndead : ModItem
    {
        /// <summary>"Each minion projectile: 30 damage".</summary>
        public const int Damage = 30;

        /// <summary>Mine: the Slime Staff's and the Imp Staff's.</summary>
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
            Item.width = 40;
            Item.height = 40;

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

            Item.buffType = ModContent.BuffType<HoardOfUndeadBuff>();
            Item.shoot = ModContent.ProjectileType<HoardOfUndeadZombie>();

            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 1);
        }

        /// <summary>Where the zombie comes up: the cursor, or the summoner's feet if the cursor is in the ground.</summary>
        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            Vector2 size = new Vector2(HoardOfUndeadZombie.Width, HoardOfUndeadZombie.Height);
            position = Main.MouseWorld;

            if (Collision.SolidCollision(position - size / 2f, HoardOfUndeadZombie.Width, HoardOfUndeadZombie.Height))
                position = new Vector2(player.Center.X, player.Bottom.Y - size.Y / 2f);

            velocity = Vector2.Zero;
        }

        /// <summary>
        /// The buff that keeps them, and the zombie, which of the eight it looks like picked here
        /// (ai[0]; only the summoner's machine uses an item, and the zombie goes to everyone else with
        /// it). Its damage is worked out from the item's own each tick by the game, as every minion's.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            int look = Main.rand.Next(HoardOfUndeadZombie.Variants.Length);
            Projectile zombie = Projectile.NewProjectileDirect(source, position, velocity, type, damage, knockback, player.whoAmI, look, 0f, 0f);
            zombie.originalDamage = Item.damage;

            return false;
        }
    }

    /// <summary>
    /// Hoard of Undead from any zombie, one time in 50, by request ("Dropped by any zombie with a
    /// 1/50 chance"). A zombie is what the game itself counts as one (NPCID.Sets.Zombies, which a mod
    /// can add its own to) and the zombies the game names that it leaves out of that list: the
    /// Armed Torch Zombie, the Zombie Elves, the Blood Zombie, the Zombie Merman and the Maggot Zombie.
    /// Asked once for every kind of NPC, the small and big zombies' kinds among them.
    /// </summary>
    public class HoardOfUndeadDrop : GlobalNPC
    {
        /// <summary>One time in this many.</summary>
        public const int Chance = 50;

        private static readonly int[] AlsoZombies =
        {
            NPCID.ArmedTorchZombie, NPCID.ZombieElf, NPCID.ZombieElfBeard, NPCID.ZombieElfGirl,
            NPCID.BloodZombie, NPCID.ZombieMerman, NPCID.MaggotZombie,
        };

        public static bool Zombie(int type) =>
            (type >= 0 && type < NPCID.Sets.Zombies.Length && NPCID.Sets.Zombies[type]) || Array.IndexOf(AlsoZombies, type) >= 0;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (Zombie(npc.type))
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HoardOfUndead>(), Chance));
        }
    }
}
