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
    /// Mycelium, Finish Them: a ring of glowing caps called up out of the ground under the cursor
    /// (FairyRing), for ten seconds. Spores rise in it and hurt what stands there, every half second.
    /// Whatever dies in it goes down into the ground (what is left of it sinks, FairyRingSoil), and a
    /// cap comes up where it lay (FairyRingFruit) and puffs spores at the next thing that comes near.
    ///
    /// The ground nearest the cursor, as the Court of Hands finds it (CourtOfHandsRite.FindGround);
    /// none within reach, nothing happens and the mana comes back. One ring at a time: another cast
    /// lets the last one wither.
    ///
    /// The incantation for the first days the checklist asked for: a chest in the mushroom caves
    /// (FairyRingPass, every new world, and /struct update), and anything killed in the mushroom caves
    /// underground, one in <see cref="DropChance"/> (VanillaLoot). Lore: §XXVIII, "the only place where things
    /// finish"; §XXIV, the element is the place it was drawn from.
    /// </summary>
    public class MyceliumFinishThem : Castable
    {
        /// <summary>One in this many kills in the mushroom caves underground drops it (by request: "1/8").</summary>
        public const int DropChance = 8;

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 14;
        public override int Mana => 24;
        public override int UseTime => 40;
        public override int ReuseDelay => 10;
        public override float Knockback => 0f;
        public override int Shot => ModContent.ProjectileType<FairyRing>();
        public override string CastType => "Conjuration";
        public override string Element => "Fungal";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 40);
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            if (!CourtOfHandsRite.FindGround(Main.MouseWorld, out Vector2 ground))
            {
                player.statMana = Math.Min(player.statManaMax2, player.statMana + Mana);
                return;
            }

            FairyRing.WitherAll(player.whoAmI);
            Projectile.NewProjectile(source, ground, Vector2.Zero, Shot, damage, 0f, player.whoAmI);
        }
    }
}
