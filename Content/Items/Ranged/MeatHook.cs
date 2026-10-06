using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Ranged;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// The Meat Hook: a double-barrelled shotgun with a hook on a chain slung under it.
    /// 22 damage a pellet, 8 pellets a shell, 2 shells and then 1.2 seconds to reload.
    ///
    /// It is in the hands for as long as it is the item held (MeatHookHoldOut, which is the
    /// gun as it is seen: the item itself is never drawn in use).
    ///
    /// LEFT CLICK fires a shell: the Onyx Blaster's spread without its bolt, and more of it.
    /// Eight of whatever bullet is loaded for the one that is spent, from the middle of the
    /// one who fires as every gun of the game's does, so that nothing standing against them
    /// is shot past. Two of the eight keep near the line; the rest scatter up to
    /// <see cref="Cone"/> degrees either side, some quicker than others.
    ///
    /// RIGHT CLICK throws the hook (MeatHook_Hook; the gun in the hand throws it, not the
    /// item's use, so it goes out while the gun fires, reloads or is empty). It flies 50
    /// tiles at the most and comes back from a tile. What it sinks into, it holds: a small
    /// enemy is dragged to the one who threw it, and anything else drags them to it
    /// (MeatHookRules.Hauled). It can be thrown again 3 seconds after it comes back if it
    /// caught something, 5 if it did not.
    ///
    /// WHILE THE HOOK HOLDS AN ENEMY every pellet fired does double damage
    /// (<see cref="HookedDamage"/>), at whatever it hits.
    ///
    /// The magazine, the reload, the count beside the player and the flash and smoke at the
    /// muzzle are PSItem's, as for every gun; this adds to the last two.
    /// </summary>
    public class MeatHook : ModItem
    {
        public const int Damage = 22;
        public const int UseTicks = 28;
        public const float Knockback = 7f;
        public const float ShootSpeed = 8f;

        /// <summary>Pellets a shell.</summary>
        public const int Pellets = 8;

        /// <summary>Shells before it is broken open, and the ticks that takes.</summary>
        public const int Shells = 2;
        public const int ReloadTicks = 72;

        /// <summary>What a pellet's damage is multiplied by while the hook is in an enemy.</summary>
        public const float HookedDamage = 2f;

        /// <summary>What the hook's own hit does, of the gun's damage.</summary>
        public const float HookDamage = 1.5f;

        /// <summary>Degrees either side of the aim: most of the pellets, and the two that keep near the line.</summary>
        private const float Cone = 9f;
        private const float TightCone = 2.5f;
        private const int TightPellets = 2;

        /// <summary>A pellet's speed, of the shot's: the slowest and the quickest.</summary>
        private const float Slowest = 0.82f;
        private const float Quickest = 1.12f;

        /// <summary>The part of the sprite's width PSItem puts its flash at (its DefaultMuzzle).</summary>
        private const float FlashReach = 0.85f;

        private static readonly Color[] Sparks = { new Color(255, 236, 190), new Color(255, 190, 110), new Color(255, 132, 60) };
        private static readonly Color[] HookedSparks = { new Color(255, 214, 150), new Color(255, 120, 64), new Color(240, 60, 40) };

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 70;
            Item.height = 22;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = UseTicks;
            Item.useAnimation = UseTicks;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            // The shotgun's, lower.
            Item.UseSound = SoundID.Item36 with { Pitch = -0.35f, PitchVariance = 0.08f };

            // What is loaded decides what flies; this only has to be something.
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = ShootSpeed;
            Item.useAmmo = AmmoID.Bullet;

            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 25);

            Item.PIS().Magazine(Shells, ReloadTicks);
        }

        /// <summary>
        /// The flash and the smoke are PSItem's, which puts them 0.85 of the item's sprite out
        /// from the point the shot is given, along it: right for a gun the game draws from the
        /// middle of the player, and not for one held by its grip, whose muzzle is a little
        /// above that line. So the point given is moved to where that sum lands on the muzzle
        /// of the gun as it is held. Nothing is shot from it (<see cref="Shoot"/> shoots from
        /// the middle, itself).
        /// </summary>
        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);

            // PSItem's own sum for the length: the sprite's width once it is loaded, the item's until then.
            float length = Item.width;
            Asset<Texture2D> texture = TextureAssets.Item[Item.type];

            if (texture != null && texture.IsLoaded)
                length = texture.Value.Width;

            position = MeatHookHoldOut.MuzzleOf(player, aim) - aim * (length * Item.scale * FlashReach);
        }

        /// <summary>
        /// Every pellet is shot here, and nothing is left for the game to shoot: all of them at
        /// twice the damage while the hook holds something. Only where the one who fires is.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            float speed = velocity.Length();

            // Where the game shoots every gun from. (position is not that: see ModifyShootStats.)
            Vector2 from = player.RotatedRelativePoint(player.MountedCenter, false, true);

            bool hooked = MeatHook_Hook.Holds(player);
            int dealt = hooked ? (int)Math.Round(damage * HookedDamage) : damage;

            for (int i = 0; i < Pellets; i++)
            {
                float cone = i < TightPellets ? TightCone : Cone;
                float turn = MathHelper.ToRadians(Main.rand.NextFloat(-cone, cone));
                Vector2 shot = aim.RotatedBy(turn) * speed * Main.rand.NextFloat(Slowest, Quickest);

                Projectile.NewProjectile(source, from, shot, type, dealt, knockback, player.whoAmI);
            }

            Blast(player, MeatHookHoldOut.MuzzleOf(player, aim), aim, hooked);
            return false;
        }

        /// <summary>
        /// On top of PSItem's flash and smoke: a second, bigger flash, more smoke, a cone of
        /// sparks, the heavier shotgun's sound under the item's own, and the screen shaken. A
        /// shot fired while the hook holds is more of all of it, redder, with a thump.
        /// </summary>
        private static void Blast(Player player, Vector2 muzzle, Vector2 aim, bool hooked)
        {
            if (Main.dedServ)
                return;

            PADParticleHandler.SpawnParticle(new MuzzleFlash(muzzle, aim, hooked ? 2.1f : 1.6f));
            MuzzleSmoke.Spawn(muzzle, aim, hooked ? 7 : 5);

            Color[] palette = hooked ? HookedSparks : Sparks;
            int sparks = hooked ? 16 : 11;

            for (int i = 0; i < sparks; i++)
            {
                Vector2 thrown = aim.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-24f, 24f))) * Main.rand.NextFloat(6f, 17f);

                PADParticleHandler.SpawnParticle(new SparkShard(muzzle + Main.rand.NextVector2Circular(3f, 3f), thrown,
                    palette[Main.rand.Next(palette.Length)], 12f, 20f, Main.rand.NextFloat(22f, 36f), Main.rand.Next(9, 16), falls: true));
            }

            SoundEngine.PlaySound(SoundID.Item38 with { Pitch = -0.45f, PitchVariance = 0.06f, Volume = 0.6f }, muzzle);

            if (hooked)
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.25f, Volume = 0.5f }, muzzle);

            player.PIS().BumpScreenShake(hooked ? 8.5f : 5f, hooked ? 8 : 5);
        }

        //public override void AddRecipes()
        //{
        //    CreateRecipe()
        //        .AddIngredient(ItemID.Shotgun)
        //        .AddIngredient(ItemID.GrapplingHook)
        //        .AddIngredient(ItemID.Chain, 10)
        //        .AddIngredient(ItemID.SoulofNight, 10)
        //        .AddTile(TileID.MythrilAnvil)
        //        .Register();
        //}
    }
}
