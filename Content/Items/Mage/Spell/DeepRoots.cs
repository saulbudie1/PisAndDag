using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Spell
{
    /// <summary>
    /// Deep Roots, by request: "Roots pierce the ground and to the cursor; can go from the wall (Like blood thorn
    /// https://terraria.wiki.gg/wiki/Blood_Thorn); maybe js recolor the projectiles. take the numbers from the item
    /// and make it less so its weaker. Dropped by Finishing Dryad's Quest", the quest one of my own after the Bezoar
    /// ("A new quest you design after bezoar quest"): DryadRoots.
    ///
    /// A spell. Each cast calls up <see cref="Roots"/> roots, <see cref="Spacing"/> ticks apart, as the Blood Thorn
    /// throws its three thorns over one use (Item 4270: use time 11, animation 33). Each comes out of the ground near
    /// the cursor, as the Blood Thorn's do (Player.FindSharpTearsSpot): the blocks within 200 pixels of where a line
    /// from the caster first meets something on the way to the cursor, one with an open side toward that point if
    /// any; and where there are no blocks, out of the walls behind (what the Blood Thorn cannot do: a background wall
    /// in the same reach); and only where there is neither, out of the caster. Each points at the cursor and runs
    /// through what is in the way (DeepRoot), and leaves when it is done growing.
    ///
    /// The Blood Thorn's numbers made less (1.4.4: 34 damage, 20 mana, a use of 33 ticks for three, through 4, its
    /// thorns 0.6 to 1.1 of their length): <see cref="CastDamage"/>, <see cref="Mana"/>, <see cref="UseTime"/>, three
    /// roots, each through three (DeepRoot.Pierce), 0.5 to 0.85 of the length. Conjuration. Flora and Fauna.
    /// Its picture: the spell parchment with a root through it; the roots' (DeepRoot.png): the thorns' shape, in
    /// bark and moss, 2x2 pixels.
    /// </summary>
    public class DeepRoots : Castable
    {
        /// <summary>Roots a cast, and the ticks between them.</summary>
        public const int Roots = 3;
        public const int Spacing = 12;

        public override CastKind Kind => CastKind.Spell;
        public override int CastDamage => 22;
        public override int Mana => 15;
        public override int UseTime => 36;
        public override int Shot => ModContent.ProjectileType<DeepRoot>();
        public override string CastType => "Conjuration";
        public override string Element => "Flora and Fauna";
        public override float Knockback => 1f;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            for (int i = 0; i < Roots; i++)
            {
                // Waits in the caster unseen for its turn, then finds its place (DeepRoot.Grow).
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, Shot, damage, knockback, player.whoAmI,
                    -i * Spacing, Main.rand.NextFloat(DeepRoot.ShortScale, DeepRoot.LongScale));
            }
        }
    }
}
