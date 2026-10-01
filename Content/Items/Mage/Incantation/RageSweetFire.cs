using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Projectiles.Magic;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// Rage! Sweet Fire. Asks the fire to rage with the caster a while: the Sweet Fire buff for
    /// <see cref="Duration"/> ticks, during which every fire hit of theirs deals a quarter more
    /// (what fire is, and the number, are FireAffinity's and SweetFire's), and a ring of fire
    /// goes round them (SweetFireAura). Hurts nothing itself.
    ///
    /// Enchantment on the chart of schools: it works on the one it is said over. Flame: the
    /// element fire is drawn as. An incantation because a seal asks directly.
    ///
    /// It will not go off again while more than <see cref="RecastWindow"/> ticks of the last
    /// one are left, as the other two do, so holding the button does not pour mana into a buff
    /// that is already up.
    ///
    /// Written at the Makeshift Altar: a Lava Bucket, 15 Souls of Fright, 50 Living Fire Blocks
    /// and a Book. Placeholder art: see PlaceholderTextures.md.
    /// </summary>
    public class RageSweetFire : Castable
    {
        /// <summary>Thirty seconds.</summary>
        public const int Duration = 30 * 60;

        /// <summary>Five seconds.</summary>
        public const int RecastWindow = 5 * 60;

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 0;
        public override int Mana => 40;
        public override int UseTime => 30;
        public override int Shot => ModContent.ProjectileType<SweetFireCall>();
        public override string CastType => "Enchantment";
        public override string Element => "Flame";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 4);
        }

        public override bool CanCast(Player player)
        {
            int index = player.FindBuffIndex(ModContent.BuffType<SweetFire>());
            return index < 0 || player.buffTime[index] <= RecastWindow;
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            player.AddBuff(ModContent.BuffType<SweetFire>(), Duration);
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, Shot, 0, 0f, player.whoAmI);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.LavaBucket)
                .AddIngredient(ItemID.SoulofFright, 15)
                .AddIngredient(ItemID.LivingFireBlock, 50)
                .AddIngredient(ItemID.Book)
                .AddTile(ModContent.TileType<MakeshiftAltar>())
                .Register();
        }
    }
}
