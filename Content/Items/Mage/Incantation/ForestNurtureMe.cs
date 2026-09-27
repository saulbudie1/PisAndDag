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
    /// Forest, Nurture Me. Asks the forest to keep the caster a while: the Nurtured buff for
    /// <see cref="Duration"/> ticks (its numbers are in Nurtured), a shield that grows round
    /// them, and light that runs about it. Hurts nothing.
    ///
    /// Abjuration on the chart of schools: it protects. Flora and Fauna: drawn from living
    /// things. An incantation because a seal asks the ground directly.
    ///
    /// It will not go off again while more than <see cref="RecastWindow"/> ticks of the last
    /// one are left, so holding the button does not pour mana into a buff that is already up;
    /// asked again inside that window, the buff goes back to full and the shield pulses.
    ///
    /// Sold by the Dryad (<see cref="ForestNurtureMeSource"/>). Placeholder art: see
    /// PlaceholderTextures.md.
    /// </summary>
    public class ForestNurtureMe : Castable
    {
        /// <summary>Thirty seconds.</summary>
        public const int Duration = 30 * 60;

        /// <summary>Five seconds.</summary>
        public const int RecastWindow = 5 * 60;

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 0;
        public override int Mana => 25;
        public override int UseTime => 30;
        public override int Shot => ModContent.ProjectileType<NurtureCall>();
        public override string CastType => "Abjuration";
        public override string Element => "Flora and Fauna";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Blue;
        }

        public override bool CanCast(Player player)
        {
            int index = player.FindBuffIndex(ModContent.BuffType<Nurtured>());
            return index < 0 || player.buffTime[index] <= RecastWindow;
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            player.AddBuff(ModContent.BuffType<Nurtured>(), Duration);
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, Shot, 0, 0f, player.whoAmI);
        }
    }

    /// <summary>Where it comes from: the Dryad's shop, always.</summary>
    public class ForestNurtureMeSource : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType == NPCID.Dryad)
                shop.Add(ModContent.ItemType<ForestNurtureMe>());
        }
    }
}
