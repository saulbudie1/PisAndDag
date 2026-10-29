using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Spell
{
    /// <summary>
    /// Boulder Catapult, by request: "throw boulder at cursor with gravity and once it hits a gorund, it acts like a
    /// normal boulder; prehardmode, slow use time."
    ///
    /// A spell (the staff casts it). Each cast throws a boulder from the caster's middle, where the game puts the shot
    /// of every staff (Player.ItemCheck_Shoot: RotatedRelativePoint(MountedCenter)), on an arc that comes down on
    /// the cursor (<see cref="CatapultBoulder.Launch"/>: the flight is longer the further the throw, so a near one
    /// is a lob and a far one a high, long arc; never further than the game lets a cursor reach, 60 tiles across
    /// and 37 up or down). It hurts what it hits in the air; where it first comes down on the ground it rolls on
    /// as the game's own boulders roll (aiStyle 25, 1.4.4: it gathers speed to 7 pixels a tick, bounces a little
    /// off a drop, steers off a wall it has stopped against, sets off pressure plates, and breaks on a wall it runs
    /// into), hurting whatever it rolls over, never its caster.
    ///
    /// Slow: a cast every <see cref="UseTime"/> ticks. Conjuration: it calls a thing up. Earth.
    /// Mine: its numbers (a pre-Hardmode spell's), and where it comes from: written at the Makeshift Altar as the
    /// other spells are, with a Boulder, stone, a Book and topaz (<see cref="AddRecipes"/>, commented out as the
    /// mod's own recipes are). Its picture: the spell parchment with a boulder in a sling.
    /// </summary>
    public class BoulderCatapult : Castable
    {
        public override CastKind Kind => CastKind.Spell;
        public override int CastDamage => 58;
        public override int Mana => 20;

        /// <summary>Slow: just under a second a cast.</summary>
        public override int UseTime => 55;

        public override int Shot => ModContent.ProjectileType<CatapultBoulder>();
        public override string CastType => "Conjuration";
        public override string Element => "Earth";
        public override float Knockback => 8f;

        /// <summary>The creak of the throw, in place of the chime.</summary>
        public override SoundStyle? CastSound => SoundID.Item1 with { Pitch = -0.6f, Volume = 0.9f };

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            Vector2 target = Main.MouseWorld;
            player.LimitPointToPlayerReachableArea(ref target);

            Vector2 from = position;

            // The game's place for the shot, the caster's middle as it is turned (a mount or a turned player can move it);
            // if that is inside a block, the middle as it stands.
            if (Collision.SolidCollision(from - new Vector2(CatapultBoulder.Size * 0.5f), CatapultBoulder.Size, CatapultBoulder.Size))
                from = player.MountedCenter;

            Projectile.NewProjectile(source, from, CatapultBoulder.Launch(from, target), Shot, damage, knockback, player.whoAmI);
        }

        // Written at the Makeshift Altar as the other spells are: a Boulder, 25 Stone Blocks, a Book and 5 Topaz.
        // Commented out, as the mod's own recipes are.
        //
        // public override void AddRecipes()
        // {
        //     CreateRecipe()
        //         .AddIngredient(ItemID.Boulder)
        //         .AddIngredient(ItemID.StoneBlock, 25)
        //         .AddIngredient(ItemID.Book)
        //         .AddIngredient(ItemID.Topaz, 5)
        //         .AddTile(ModContent.TileType<Content.Tiles.MakeshiftAltar>())
        //         .Register();
        // }
    }
}
