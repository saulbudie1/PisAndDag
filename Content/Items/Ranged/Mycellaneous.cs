using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Ranged;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// Mycellaneous, by request: "a bow that any arrow hit an enemy will generate the player's life by
    /// either 2,3 or 5 on crit. 13 damage, 20 use time. higher shootspeed than wooden bow. Glowing
    /// Mushroom x50, wooden bow"; and then: "add the cool particle for  mycellaneous that cones back to
    /// the player".
    ///
    /// A bow: 13 damage every 20 ticks, its arrows at <see cref="ShootSpeed"/> (the game's Wooden Bow
    /// shoots at 6.1). Every arrow it shoots that hits an enemy sends a spore of light home to its
    /// shooter carrying <see cref="HealLow"/> or <see cref="HealHigh"/> life, <see cref="HealCrit"/> on
    /// a critical hit (<see cref="MycellaneousArrow"/>); the life lands when the spore reaches them, as
    /// the game's own life-stealing orbs do (<see cref="MycellaneousSpore"/>). An enemy: not a critter,
    /// not a target dummy.
    ///
    /// Mine: no knockback, as the Wooden Bow; not fired again by holding the button, as it is not; Blue,
    /// 20 silver to sell; a little of the Glowing Mushroom's light about whoever holds it. Its picture is
    /// a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class Mycellaneous : ModItem
    {
        /// <summary>How fast its arrows leave it: a bit faster than the Wooden Bow's 6.1.</summary>
        public const float ShootSpeed = 7.5f;

        /// <summary>Life back for a hit: either of these, evenly.</summary>
        public const int HealLow = 2;
        public const int HealHigh = 3;

        /// <summary>Life back for a critical hit.</summary>
        public const int HealCrit = 5;

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 44;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = 13;
            Item.knockBack = 0f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.useAmmo = AmmoID.Arrow;
            Item.shootSpeed = ShootSpeed;
            Item.UseSound = SoundID.Item5;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 20);
        }

        /// <summary>Shot as the game shoots it, and marked: its hits give life back.</summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            int index = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].TryGetGlobalProjectile(out MycellaneousArrow arrow))
                arrow.Heals = true;

            return false;
        }

        /// <summary>The Glowing Mushroom's blue, faint, about the one holding it.</summary>
        public override void HoldItem(Player player)
        {
            Lighting.AddLight(player.Center, 0.05f, 0.16f, 0.4f);
        }

        // Recipes stay commented out for now. As asked: 50 Glowing Mushrooms and a Wooden Bow. No place
        // was named, so anywhere:
        //
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GlowingMushroom, 50)
                .AddIngredient(ItemID.WoodenBow)
                .Register();
        }
    }

    /// <summary>
    /// An arrow from Mycellaneous: on hitting an enemy it sends the life home (MycellaneousSpore), thrown
    /// on the way the arrow was going, a little to either side and up. Only on the shooter's machine,
    /// where their hits are worked out; the game sends the spore on to everyone from there.
    /// </summary>
    public class MycellaneousArrow : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>Arrows only: whatever arrow the bow is loaded with.</summary>
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => lateInstantiation && entity.arrow;

        /// <summary>Shot from Mycellaneous.</summary>
        public bool Heals;

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!Heals || projectile.owner != Main.myPlayer)
                return;

            // An enemy: not a critter, not a target dummy.
            if (target.friendly || target.immortal || target.CountsAsACritter || target.lifeMax <= 5)
                return;

            Player player = Main.player[projectile.owner];

            if (!player.active || player.dead)
                return;

            int life = hit.Crit ? Mycellaneous.HealCrit : Main.rand.Next(Mycellaneous.HealLow, Mycellaneous.HealHigh + 1);

            Vector2 onward = projectile.velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 thrown = onward.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(3.5f, 5.5f) - Vector2.UnitY * 1.5f;

            Projectile.NewProjectile(projectile.GetSource_OnHit(target), projectile.Center, thrown,
                ModContent.ProjectileType<MycellaneousSpore>(), 0, 0f, projectile.owner, life);
        }
    }
}
