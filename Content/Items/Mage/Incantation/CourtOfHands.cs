using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// Hands called up out of the ground under the cursor, three at a time, for a
    /// while. Held, they follow. See <see cref="CourtOfHandsRite"/> for the rest.
    ///
    /// The cast lands on the ground nearest the cursor, not on the cursor: called at
    /// open air it comes up from the floor below, called into rock it comes up from
    /// the surface above. Nothing to stand on within reach, nothing happens, and the
    /// mana comes back.
    /// </summary>
    public class CourtOfHands : Castable
    {
        public override string Texture => "PisAndDag/Content/Items/Mage/Incantation/CourtofHands";

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 30;
        public override int Mana => 18;
        public override int UseTime => 45;
        public override bool Channel => true;
        public override int Shot => ModContent.ProjectileType<CourtOfHandsRite>();
        public override string CastType => "Conjuration";
        public override string Element => "Blood";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Green;
        }

        /// <summary>One court at a time.</summary>
        public override bool CanCast(Player player) => player.ownedProjectileCounts[Shot] == 0;

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            if (!CourtOfHandsRite.FindGround(Main.MouseWorld, out Vector2 ground))
            {
                player.statMana = System.Math.Min(player.statManaMax2, player.statMana + Mana);
                return;
            }

            Projectile.NewProjectile(source, ground, Vector2.Zero, Shot, damage, knockback, player.whoAmI);
        }
    }
}
