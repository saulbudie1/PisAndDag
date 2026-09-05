using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Items.Melee
{  
	public class BladeOfMors : ModItem
	{
        private const float MeterMax = 100f;
         
        private const float MaxDropletsPerTick = 1.35f;
         
        private const float ConeHalfAngle = 25f;

        private static readonly Color BloodColor = new Color(117, 9, 24);
         
        private static readonly Color SiphonColor = new Color(128, 0, 60);

        private int ReleaseTimer;

        private float Meterlogy = 0f;

        private bool Full = false;
         
        private float dropletBudget;
        public override void SetDefaults()
        {
            Item.damage = 30;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = 10000;
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
            Item.useTurn = true;
            Item.autoReuse = true;
        }
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (PADWorld.Legends)
            {
                float percent = MathHelper.Clamp(Meterlogy / MeterMax * 100f, 0f, 100f);


                tooltips.Add(new TooltipLine(Mod, "DeathMeter", "Upon hitting an enemy, the blade charges with dark energy\r\nOnce fully charged, it can unleash a devastating attack"));
                tooltips.Add(new TooltipLine(Mod, "DeathMeter",
                    percent.ToString("F1") + "% of death meter filled"));
                
            }
        }
        public override bool? UseItem(Player player)
        {


            return base.UseItem(player);
        }
        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (PADWorld.Legends)
            {
                Meterlogy += Main.rand.NextFloat(1.5f, 3f) * (hit.Crit ? 1.5f : 1f);

                if (Meterlogy > MeterMax)
                    Meterlogy = MeterMax;

                Full = Meterlogy >= MeterMax;

                // Pull blood out of the target and into the wielder — the meter filling,
                // made visible at the moment it happens.
                if (!Main.dedServ)
                    BloodSiphon.SpawnDrain(target, player, Main.rand.Next(5, 9), SiphonColor);
            }
        }
        /// <summary>
        /// Runs every tick while the blade is held. Bleeds blood from the player in
        /// proportion to the charge, so the meter is readable without opening a tooltip.
        /// </summary>
        public override void HoldItem(Player player)
        {
            if (PADWorld.Legends)
            {
                // Purely decorative and client-side — never run it on a server.
                if (Main.dedServ)
                    return;

                float charge = MathHelper.Clamp(Meterlogy / MeterMax, 0f, 1f);
                if (charge <= 0f)
                {
                    dropletBudget = 0f;
                    return;
                }

                // Squared, so a nearly-empty blade barely weeps and a full one gushes.
                // Swap for plain `charge` if you want the ramp to feel linear.
                dropletBudget += charge * charge * MaxDropletsPerTick;

                while (dropletBudget >= 1f)
                {
                    dropletBudget -= 1f;

                    // Near the centre, with just enough jitter that the two cones don't
                    // emerge from a single pixel.
                    Vector2 position = player.Center + Main.rand.NextVector2Circular(4f, 6f);

                    // Straight out to one side or the other, within 20 degrees of level —
                    // so each side is a 40 degree wedge, 20 up and 20 down.
                    int side = Main.rand.NextBool() ? 1 : -1;
                    float spread = MathHelper.ToRadians(Main.rand.NextFloat(-ConeHalfAngle, ConeHalfAngle));
                    Vector2 velocity = new Vector2(side, 0f).RotatedBy(spread)
                        * Main.rand.NextFloat(1.4f, 3.6f);

                    // Weightless: it drifts out and hangs rather than raining down.
                    PADParticleHandler.SpawnParticle(new BloodDroplet(
                        position,
                        velocity,
                        Main.rand.Next(30, 55),
                        Main.rand.NextFloat(0.4f, 0.7f) * (0.6f + charge * 0.5f),
                        BloodColor,
                        affectedByGravity: false));
                }

                // A faint red wash on the player sells the charge even between droplets.
                Lighting.AddLight(player.Center, BloodColor.ToVector3() * 0.45f * charge);
            }
        }

        public override bool CanUseItem(Player player)
        {
            return ReleaseTimer <= 0;
        }
         
        public override void SaveData(TagCompound tag)
        {
            if (Meterlogy > 0f)
                tag["Meterlogy"] = Meterlogy;
        }

        public override void LoadData(TagCompound tag)
        { 
            Meterlogy = tag.GetFloat("Meterlogy");
            Full = Meterlogy >= MeterMax;
        }
        //public override void AddRecipes()
        //{
        //	Recipe recipe = CreateRecipe();
        //	recipe.AddIngredient(ItemID.DirtBlock, 10);
        //	recipe.AddTile(TileID.WorkBenches);
        //	recipe.Register();
        //}
    }
}
