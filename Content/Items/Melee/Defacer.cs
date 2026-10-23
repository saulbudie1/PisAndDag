using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// The Defacer: a mason's bush hammer, its face cut into teeth for taking the skin off stone.
    /// 36 damage. Found in a drawer: a dresser put into an underground cabin (CourtDrawersPass, every
    /// new world, and /struct update), by request (PADWorkFlow §16.98).
    ///
    /// Held down, it is lifted up and back (DefacerHoldout), slowly, and its wielder slows as it
    /// rises; let go, it comes down. Long in the haft: the head comes down 56 texels past the hands
    /// (twice what it was, by request: "defacer weapon, double range"), and the haft strikes too.
    /// Lifted all the way (<see cref="DefacerHoldout.WindTicks"/>, less
    /// with melee speed) it hits for <see cref="DefacerHoldout.FullDamage"/> times its damage, throws
    /// stone chips, and leaves what it hit Defaced for eight seconds: 10 of its defense ignored by
    /// every hit, and its name scratched out under the cursor. Let go early, it hits for less, down
    /// to <see cref="DefacerHoldout.LeastDamage"/>.
    ///
    /// Lore: §XVII, the Low Court's mark ("chipped faces on angel statues, scratched-out names on
    /// shrines"), and its followers, who "keep a drawer". The item never says whose.
    /// </summary>
    public class Defacer : ModItem
    {
        public const int Damage = 36;
        public const float Knockback = 7f;

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
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.channel = true;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<DefacerHoldout>();
            Item.shootSpeed = 1f;

            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(silver: 60);
        }

        /// <summary>One blow at a time.</summary>
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        /// <summary>The hammer is taken up facing the cursor's side; from there it is the holdout's.</summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            int facing = velocity.X < 0f ? -1 : velocity.X > 0f ? 1 : player.direction;
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, type, damage, knockback, player.whoAmI, 0f, 0f, facing);
            return false;
        }
    }
}
