using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{ 
    public class DentedMegashark : ModItem
    { 
        public override string Texture => "Terraria/Images/Item_" + ItemID.Megashark;

        // ---- how much worse ---------------------------------------------------------

        private const int Damage = 22;                  // vanilla 25
        private const int UseTicks = 9;                 // vanilla 7, this mod's Megashark 6
        private const float ShootSpeed = 8f;            // vanilla 10
        private const float Knockback = 0.6f;           // vanilla 1
        private const int MagSize = 80;                  
        private const int ReloadTicks = 3* 60 +30;             
         
        private const float SpreadDegrees = 6f;
         
        private const float JamChance = 0.08f;
         
        private const int JamTicks = 60;
         
        private int jamTimer;

        public override void SetDefaults()
        {
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.autoReuse = true;
            Item.useAnimation = UseTicks;
            Item.useTime = UseTicks;
            Item.width = 50;
            Item.height = 18;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = Damage;
            Item.knockBack = Knockback;
            Item.shootSpeed = ShootSpeed;
            Item.noMelee = true;
             
            Item.shoot = 1;
            Item.useAmmo = AmmoID.Bullet;             
            Item.UseSound = SoundID.Item11 with { Pitch = -0.22f, Volume = 0.9f };

            Item.value = Item.buyPrice(gold: 12);
            Item.rare = ItemRarityID.LightRed;

            Item.PIS().Magazine(MagSize, ReloadTicks);
        }
         
        public override Color? GetAlpha(Color lightColor)
        {
            return new Color(
                (int)(lightColor.R * 0.78f),
                (int)(lightColor.G * 0.70f),
                (int)(lightColor.B * 0.60f),
                lightColor.A);
        }

        public override bool CanUseItem(Player player)
        {
            return jamTimer <= 0;
        }

        public override void UpdateInventory(Player player)
        {
            if (jamTimer > 0)
                jamTimer--;
        }

        /// <summary>The bent barrel: every round leaves a little off the line.</summary>
        public override void ModifyShootStats(Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            velocity = velocity.RotatedByRandom(MathHelper.ToRadians(SpreadDegrees));
        }
         
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (Main.rand.NextFloat() < JamChance)
                Jam(player);

            return true;
        }

        private void Jam(Player player)
        {
            jamTimer = JamTicks;

            if (Main.dedServ || player.whoAmI != Main.myPlayer)
                return;

            SoundEngine.PlaySound(SoundID.Item149 with { Pitch = 0.35f, Volume = 0.55f }, player.Center);
             
            CombatText.NewText(player.Hitbox, new Color(170, 150, 120), "*clunk*");
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "DentedFlavour", "'Pulled from the wreck. It still fires, mostly.'")
            { OverrideColor = new Color(170, 150, 120) });

            tooltips.Add(new TooltipLine(Mod, "DentedJam",
                $"Bent barrel: rounds stray up to {SpreadDegrees:0}°, and the action jams now and then"));
        }

        public override Vector2? HoldoutOffset()
        { 
            return null;
        }
    }
}
