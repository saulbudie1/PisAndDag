using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// A mouth let out of the chest that goes back for what it bit until it is too
    /// small to. See <see cref="EverHungerHead"/> for the rest.
    ///
    /// It goes level, toward whichever side the cursor is on, not at the cursor.
    /// </summary>
    public class EverHunger : Castable
    {
        public override string Texture => "PisAndDag/Content/Items/Mage/Incantation/EverHunger";

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 51;
        public override int Mana => 18;
        public override int UseTime => 30;
        public override int Shot => ModContent.ProjectileType<EverHungerHead>();
        public override string CastType => "Conjuration";
        public override string Element => "Blood";
        public override float Speed => EverHungerHead.Speed;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Green;
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            int dir = Math.Sign(Main.MouseWorld.X - player.Center.X);

            if (dir == 0)
                dir = player.direction;

            player.ChangeDir(dir);

            Vector2 chest = player.MountedCenter + new Vector2(dir * 10f, -4f * player.gravDir);

            Projectile.NewProjectile(source, chest, new Vector2(dir * EverHungerHead.Speed, 0f), Shot, damage, knockback, player.whoAmI);
        }
    }
}
