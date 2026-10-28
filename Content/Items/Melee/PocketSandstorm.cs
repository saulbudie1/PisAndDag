using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// Pocket Sandstorm, a yoyo of Harenaeum, by request ("A yoyo of 15 tile reach, duration 20 secs, Creates a vortex
    /// of sands with radius 3 tiles that damages them slowly with piercing particles"). The yoyo is
    /// PocketSandstormYoyo, the game's own yoyo in every way but the sand: a whirl of it three tiles round the yoyo,
    /// drawn in to the middle as it turns, that every sixth of a second flings a needle of sand through something
    /// inside it (PocketSandstormNeedle).
    ///
    /// Mine: its numbers. 22 damage (the Cascade, of the same rank, has 27, and no whirl), 3.5 knockback, the yoyo's
    /// top speed 14 (the Cascade's); a needle a third of the damage, through up to three, past ten defence.
    ///
    /// Placeholder pictures (PlaceholderTextures.md): the item and the yoyo.
    /// </summary>
    public class PocketSandstorm : ModItem
    {
        public const int Damage = 22;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // As the game's own yoyos.
            ItemID.Sets.Yoyo[Type] = true;
            ItemID.Sets.GamepadExtraRange[Type] = 15;
            ItemID.Sets.GamepadSmartQuickReach[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.UseSound = SoundID.Item1;
            Item.channel = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.damage = Damage;
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.knockBack = 3.5f;
            Item.crit = 4;

            Item.shoot = ModContent.ProjectileType<PocketSandstormYoyo>();
            Item.shootSpeed = 16f;

            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2, silver: 40);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
