using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Spell
{
    /// <summary>
    /// The first spell. The staff goes to the caster's mouth (<see cref="ColdCallHold"/>) and
    /// five icicles leave its head one after another, a fifth of a cast apart, all on the same
    /// sine path: together they draw one period of the wave per cast. Then the staff waits
    /// <see cref="ReuseDelay"/> ticks before it will cast again. Each icicle homes, the way
    /// Calamity's projectiles do, on anything within 8 tiles, and never goes faster for it
    /// (<see cref="ColdCallIcicle"/>).
    ///
    /// Evocation on the chart of schools: it destroys. Frost: drawn from the snow.
    ///
    /// Written at the Makeshift Altar (<see cref="AddRecipes"/>). Placeholder art: see
    /// PlaceholderTextures.md.
    /// </summary>
    public class ColdCall : Castable
    {
        /// <summary>Icicles per cast.</summary>
        public const int Count = 5;

        public override CastKind Kind => CastKind.Spell;
        public override int CastDamage => 39;
        public override int Mana => 14;
        public override int UseTime => 30;
        public override int Shot => ModContent.ProjectileType<ColdCallIcicle>();
        public override string CastType => "Evocation";
        public override string Element => "Frost";
        public override float Speed => ColdCallIcicle.Speed;

        /// <summary>Low: vanilla's tooltip calls 2 "Very weak knockback".</summary>
        public override float Knockback => 2f;

        /// <summary>A wait between casts: the staff stays at the mouth and nothing leaves it.</summary>
        public override int ReuseDelay => 15;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Green;
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            Vector2 head = ColdCallHold.HeadOf(player);
            Vector2 fallback = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            float aim = (Main.MouseWorld - head).SafeNormalize(fallback).ToRotation();

            // One period of the wave per cast, however fast this cast really is: the length of
            // this use, prefixes and speed bonuses included, set before Shoot is called.
            float period = Math.Max(player.itemAnimationMax, Count);

            // The staff to the mouth for this cast, the wait after it (player.reuseDelay, set
            // with the animation and already shortened by use speed), and two ticks more, which
            // cover the gap before the next cast when the button is held. One is enough: a cast
            // that finds it still up just gives it the new length.
            int hold = ModContent.ProjectileType<ColdCallHold>();
            float ticks = (float)Math.Ceiling(period) + player.reuseDelay + 2f;
            bool held = false;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == player.whoAmI && p.type == hold)
                {
                    p.ai[0] = ticks;
                    p.netUpdate = true;
                    held = true;
                    break;
                }
            }

            if (!held)
                Projectile.NewProjectile(source, head, Vector2.Zero, hold, 0, 0f, player.whoAmI, ticks);

            for (int i = 0; i < Count; i++)
            {
                Projectile.NewProjectile(source, head, aim.ToRotationVector2() * Speed, Shot, damage, knockback,
                    player.whoAmI, -i * period / Count, aim, period);
            }
        }

        /// <summary>Written at the Makeshift Altar: 30 Ice Blocks, a Book and 5 Sapphires.</summary>
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.IceBlock, 30)
                .AddIngredient(ItemID.Book)
                .AddIngredient(ItemID.Sapphire, 5)
                .AddTile(ModContent.TileType<Content.Tiles.MakeshiftAltar>())
                .Register();
        }
    }
}
