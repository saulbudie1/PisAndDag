using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// Mini Sandstorm, an Earth incantation of Harenaeum, by request ("js a spawn a sand storm/tornado that is smaller
    /// (check how the normal vanilla one looks), it shoots npc upwards with falling damage, (check heavy and light
    /// enemies) flying enemies arent affect much apart from damage"); asked, it drifts after enemies.
    ///
    /// A tornado of sand stood on the ground under the cursor (MiniSandstormTornado): the game's own sandnado, smaller,
    /// twelve tiles high at most, for six seconds, going after the nearest enemy along the ground. What is in it is
    /// hurt, and lifted, the lighter the higher, and thrown out of its top to fall: the fall hurts it when it lands
    /// (MiniSandstormFall). What will not be pushed (a boss, a worm, anything the game will not knock back) and what
    /// flies is hurt and nothing else.
    ///
    /// The ground nearest the cursor, as the Court of Hands finds it; none within reach, nothing happens and the
    /// mana comes back. One at a time: another cast lets the last one die down.
    ///
    /// Mine: its numbers (18 damage, every 24 ticks to each thing in it; 30 mana; 40 ticks and 20 more), and its
    /// recipe, at the Makeshift Altar as the other incantations of the hand are made.
    /// </summary>
    public class MiniSandstorm : Castable
    {
        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 18;
        public override int Mana => 30;
        public override int UseTime => 40;
        public override int ReuseDelay => 20;
        public override float Knockback => 0f;
        public override int Shot => ModContent.ProjectileType<MiniSandstormTornado>();
        public override string CastType => "Conjuration";
        public override string Element => "Earth";

        public override Terraria.Audio.SoundStyle? CastSound => SoundID.Item82 with { Volume = 0.55f, Pitch = 0.3f };

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            if (!CourtOfHandsRite.FindGround(Main.MouseWorld, out Vector2 ground))
            {
                // No ground under the cursor: nothing cast, the mana given back as the seal spent it (its cost, less
                // whatever makes magic cheaper).
                player.statMana = Math.Min(player.statManaMax2, player.statMana + player.GetManaCost(player.HeldItem));
                return;
            }

            MiniSandstormTornado.DieDownAll(player.whoAmI);
            Projectile.NewProjectile(source, ground, Vector2.Zero, Shot, damage, 0f, player.whoAmI);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(10)
                .AddIngredient(ItemID.SandBlock, 25)
                .AddTile(ModContent.TileType<Content.Tiles.MakeshiftAltar>())
                .Register();
        }
    }
}
