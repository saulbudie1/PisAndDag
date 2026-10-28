using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// The Last Grain, a revolver of Harenaeum with an hourglass in its frame, by request: "Right click to flip the
    /// hour glass that lasts for 10 seconds, in that 10 seconds: you can shoot good bullets, but if it runs out you
    /// have to flip it a gain (not automatically), during run out, you shoot it but its normal ass bullets that does
    /// miniscule damage and you are slowed by 30% walk, run, flight, (fall stays and jump stays). It can use the
    /// compressed sand. create small hour glass UI in the corner of the screen bottom right". Asked: a real hourglass
    /// (flipped early, what has fallen is all there is to fall); the compressed sand a blast from the muzzle with
    /// each shot, hurting what is near it.
    ///
    /// The hourglass is the player's (LastGrainPlayer): it runs whether the gun is held or not, and shows in the
    /// bottom right corner while it is (LastGrainUI). With sand still falling, a shot is the gun's whole damage
    /// and a blast of packed sand off the muzzle (LastGrainBlast); with none, a tenth of it, and no blast, and while
    /// the gun is held its holder goes at seven tenths of their speed walking, running and flying.
    ///
    /// Mine: its numbers (30 damage, a little over the Revolver's 20, a little under the Phoenix Blaster's 32, both
    /// near its rank; 22 ticks, one shot a click, as a revolver is; the blast half the shot's damage, 3.5 tiles out);
    /// the hourglass is empty when the gun is first had, so the first thing done with it is to turn it over.
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class TheLastGrain : ModItem
    {
        public const int Damage = 30;

        /// <summary>By request: minuscule. Mine: a tenth.</summary>
        public const float EmptyShare = 0.1f;

        /// <summary>The blast's share of the shot. Mine.</summary>
        public const float BlastShare = 0.5f;

        /// <summary>From the middle of the holder to the muzzle, along the aim.</summary>
        public const float Barrel = 30f;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 38;
            Item.height = 24;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.UseSound = null;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = 3f;
            Item.crit = 6;

            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 11f;
            Item.useAmmo = AmmoID.Bullet;

            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2, silver: 70);
        }

        public override bool AltFunctionUse(Player player) => true;

        /// <summary>A right-click turns the glass over: a short use of its own (held up), using no ammunition.</summary>
        public override bool CanUseItem(Player player)
        {
            Item.useStyle = player.altFunctionUse == 2 ? ItemUseStyleID.HoldUp : ItemUseStyleID.Shoot;
            return true;
        }

        /// <summary>The turn is quicker than a shot: 18 ticks to its 22, whatever the gun's prefix makes of both.</summary>
        public override float UseTimeMultiplier(Player player) => player.altFunctionUse == 2 ? 18f / 22f : 1f;

        public override float UseAnimationMultiplier(Player player) => player.altFunctionUse == 2 ? 18f / 22f : 1f;

        public override bool CanConsumeAmmo(Item ammo, Player player) => player.altFunctionUse != 2;

        /// <summary>The glass turns over with no bullets in the gun.</summary>
        public override bool NeedsAmmo(Player player) => player.altFunctionUse != 2;

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2 && player.whoAmI == Main.myPlayer)
                player.GetModPlayer<LastGrainPlayer>().Flip();

            return null;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzle = player.MountedCenter + aim * Barrel + new Vector2(aim.Y, -aim.X) * player.direction * 3f;

            if (Collision.CanHit(player.MountedCenter, 0, 0, muzzle, 0, 0))
                position = muzzle;

            if (player.GetModPlayer<LastGrainPlayer>().Empty)
            {
                damage = Math.Max(1, (int)(damage * EmptyShare));
                knockback *= 0.3f;
            }
        }

        /// <summary>
        /// The bullet, and with it the muzzle's own projectile (LastGrainBlast), which every machine runs: the packed sand
        /// and the sound of a full shot, or (ai[0] 1) only the dry crack and a wisp of an empty one.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
                return false;

            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            bool empty = player.GetModPlayer<LastGrainPlayer>().Empty;

            Projectile.NewProjectile(source, position, aim, ModContent.ProjectileType<LastGrainBlast>(), empty ? 0 : (int)(damage * BlastShare),
                knockback * 1.6f, player.whoAmI, empty ? 1f : 0f);

            return true;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-2f, 0f);

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(14)
                .AddIngredient(ItemID.Glass, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
