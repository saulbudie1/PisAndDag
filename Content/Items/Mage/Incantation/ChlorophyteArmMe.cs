using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// Chlorophyte, Arm Me. Asks the jungle to arm the caster a while: the Armed buff for
    /// <see cref="Duration"/> ticks, during which every bullet they fire of lower rarity than
    /// a Chlorophyte Bullet comes out a Chlorophyte Bullet, keeping its own damage, speed
    /// and knockback (the rules are in Armed and ArmedRounds). Hurts nothing itself.
    ///
    /// Transmutation on the chart of schools: it turns one thing into another. Flora and
    /// Fauna: drawn from living things. An incantation because a seal asks directly.
    ///
    /// It will not go off again while more than <see cref="RecastWindow"/> ticks of the last
    /// one are left, as Forest, Nurture Me does, so holding the button does not pour mana
    /// into a buff that is already up.
    ///
    /// Sold by the Witch Doctor once Plantera is down (<see cref="ChlorophyteArmMeSource"/>).
    /// Placeholder art: see PlaceholderTextures.md.
    /// </summary>
    public class ChlorophyteArmMe : Castable
    {
        /// <summary>Twenty seconds.</summary>
        public const int Duration = 20 * 60;

        /// <summary>Five seconds.</summary>
        public const int RecastWindow = 5 * 60;

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 0;
        public override int Mana => 30;
        public override int UseTime => 30;
        public override int Shot => ModContent.ProjectileType<ArmMeCall>();
        public override string CastType => "Transmutation";
        public override string Element => "Flora and Fauna";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Lime;
        }

        public override bool CanCast(Player player)
        {
            int index = player.FindBuffIndex(ModContent.BuffType<Armed>());
            return index < 0 || player.buffTime[index] <= RecastWindow;
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            player.AddBuff(ModContent.BuffType<Armed>(), Duration);
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, Shot, 0, 0f, player.whoAmI);
        }
    }

    /// <summary>Where it comes from: the Witch Doctor's shop, once Plantera has been beaten.</summary>
    public class ChlorophyteArmMeSource : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType == NPCID.WitchDoctor)
                shop.Add(ModContent.ItemType<ChlorophyteArmMe>(), Condition.DownedPlantera);
        }
    }
}
