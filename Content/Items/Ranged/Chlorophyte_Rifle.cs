using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// A rifle grown through with chlorophyte.
    ///
    /// The ordinary shot is a single high-velocity round. Landing
    /// <see cref="PisPlayer.ChloComboRequired"/> consecutive hits arms the weapon, and the
    /// next pull scatters ten chlorophyte bullets instead — slower, spread, and the charge
    /// is spent in the act.
    ///
    /// Every round it fires is vanilla. The spill and the purification are attached by
    /// PADGlobalProjectile, which reads the spawn source and flags anything this gun fired,
    /// so stock projectiles pick up the rifle's behaviour with no custom class involved.
    /// </summary>
    public class Chlorophyte_Rifle : ModItem
    {
        /// <summary>Rounds in the burst.</summary>
        private const int BurstCount = 10;

        /// <summary>
        /// Largest angle, in degrees, a burst round may deviate from the aim line.
        ///
        /// Each round rolls its own offset inside this. Stepping them evenly across the
        /// arc instead produces a fan — ten rounds in a neat row, which reads as a magic
        /// spread rather than a gun. Random inside a cone is what makes it a shotgun.
        /// </summary>
        private const float BurstSpreadDegrees = 6f;

        /// <summary>Speed spread across the burst, as a fraction of the base.</summary>
        private const float BurstSpeedMin = 0.5f;
        private const float BurstSpeedMax = 0.75f;

        /// <summary>
        /// How much faster the ordinary round travels than a burst round.
        ///
        /// It is a high-velocity bullet and should read as one: the burst is a close-range
        /// payoff, the single shot is the reach.
        /// </summary>
        private const float PrimarySpeedScale = 1.9f;

        private const int MagSize = 3;
        private const int ReloadTicks = 90;

        public override void SetDefaults()
        {
            Item.damage = 82;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 30;
            Item.height = 20;
            Item.useTime = 25;
            Item.useAnimation = 25;

            Item.PIS().Magazine(MagSize, ReloadTicks);

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(silver: 1);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item41;
            Item.useAmmo = AmmoID.Bullet;

            // Only has to be non-zero for Shoot to be called at all — Shoot spawns every
            // round itself and ignores the type it is handed, so ammo cannot substitute
            // a musket ball and take the spill with it.
            Item.shoot = ProjectileID.BulletHighVelocity;
            Item.shootSpeed = 15f;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.scale = 0.8f;
        } 

        /// <summary>
        /// Fires everything by hand and always returns false.
        ///
        /// The two paths need different damage, and Shoot cannot change the damage of a
        /// round the caller fires — it receives the value by copy. Handing the caller
        /// nothing to fire is what allows the streak bonus to land on the single shot and
        /// deliberately not on the burst.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            PisPlayer pis = player.PIS();

            if (pis.ChloConsumeBurst())
            {
                FireBurst(pis, player, source, position, velocity, damage, knockback);
                 
            }
            else
                FirePrimary(pis, player, source, position, velocity, damage, knockback);

            return false;
        }

        /// <summary>The ordinary round: one high-velocity bullet, carrying the streak bonus.</summary>
        private static void FirePrimary(PisPlayer pis, Player player,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int damage, float knockback)
        {
            pis.AddScreenShake(3.2f, 1);

            int boosted = (int)Math.Round(damage * pis.ChloDamageBonus);

            Projectile.NewProjectile(source, position, velocity * PrimarySpeedScale,
                ProjectileID.BulletHighVelocity, boosted, knockback, player.whoAmI);
        }

        /// <summary>
        /// The payoff: ten chlorophyte bullets scattered inside a cone.
        ///
        /// These fire at base damage on purpose. The streak bonus belongs to the single
        /// shot that earned it; ten homing rounds already are the reward, and stacking a
        /// multiplier on top of ten of them multiplies the wrong number.
        /// </summary>
        private static void FireBurst(PisPlayer pis, Player player,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int damage, float knockback)
        {
            pis.AddScreenShake(7.5f, 6);
            SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.35f, Volume = 0.9f }, player.Center);

            for (int i = 0; i < BurstCount; i++)
            {
                float degrees = Main.rand.NextFloat(-BurstSpreadDegrees, BurstSpreadDegrees);
                float speedScale = Main.rand.NextFloat(BurstSpeedMin, BurstSpeedMax);

                Vector2 shot = velocity.RotatedBy(MathHelper.ToRadians(degrees)) * speedScale;

                // Passing `source` on is what makes the burst work: PADGlobalProjectile
                // reads the firing item off it, so these rounds spill and purify exactly
                // as the ordinary shot does.
                Projectile.NewProjectile(source, position, shot,
                    ProjectileID.ChlorophyteBullet, damage, knockback, player.whoAmI);
            }
        }

        public override void HoldItem(Player player)
        {

            player.scope = true;
            // A green glow off the growth while the rifle is out. Client-side dressing.
            if (!Main.dedServ)
                Lighting.AddLight(player.Center, 0.18f, 0.38f, 0.12f);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            PisPlayer pis = Main.LocalPlayer.PIS();

            tooltips.Add(new TooltipLine(Mod, "ChloSpill",
                "Rounds spray chlorophyte on impact, purifying the ground it settles across"));

            tooltips.Add(new TooltipLine(Mod, "ChloCombo",
                $"{PisPlayer.ChloComboRequired} consecutive hits charge the next shot into a scattering burst")
            { OverrideColor = new Color(154, 206, 108) });

            if (pis.ChloArmed)
            {
                tooltips.Add(new TooltipLine(Mod, "ChloArmed", "CHARGED")
                { OverrideColor = new Color(226, 250, 158) });
            }
            else if (pis.ChloCombo > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "ChloStacks",
                    $"Streak: {pis.ChloCombo}/{PisPlayer.ChloComboRequired}  (+{(pis.ChloDamageBonus - 1f) * 100f:0}% damage)")
                { OverrideColor = new Color(154, 206, 108) });
            }
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-1.1f, 1);
        }

        //public override void AddRecipes()
        //{
        //	Recipe recipe = CreateRecipe();
        //	recipe.AddIngredient(ItemID.ChlorophyteBar, 12);
        //	recipe.AddTile(TileID.MythrilAnvil);
        //	recipe.Register();
        //}
    }
}
