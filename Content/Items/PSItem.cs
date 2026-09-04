using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items
{
    public class PSItem : GlobalItem
    {
        public int MagCount;
        public int ShotFired = 0;
        public int ReloadTimer;
        public int ReloadTime;
        public override bool InstancePerEntity => true;
        public void Magazine(int mag, int reloadtime)
        {
            MagCount = mag;
            ReloadTime = reloadtime;

        }
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (MagCount != 0)
            {

                tooltips.Add(new TooltipLine(Mod, "mag", (MagCount - ShotFired)+" shots left before reloading"));
                float value = ReloadTime / 60f;  
                string timeText; 
                if (Math.Abs(value % 1f) < 0.001f)
                { 
                    timeText = ((int)value).ToString();
                }
                else 
                { 
                    timeText = value.ToString("F1");
                } 
                string unit = value == 1f ? "second" : "seconds"; 
                tooltips.Add(new TooltipLine(Mod, "Reload", $"Item takes {timeText} {unit} to reload"));
            }
            // && binds tighter than ||, so the old condition read
            //   (!WingsAllowed && item.wingSlot != -1) || item.type == EmpressFlightBooster
            // and the booster always showed the line regardless of the flag. The per-item
            // WingsAllowed field was also never set true by anything — the real state lives
            // on PisPlayer, so read it from there.
            bool grounded = !Main.LocalPlayer.PIS().AllowedWings;
            if (grounded && (item.wingSlot != -1 || item.type == ItemID.EmpressFlightBooster))
            {
                tooltips.Add(new TooltipLine(Mod, "wing", "'However, you are bounded to the ground'"));
            }
        } 
        
        public override void UpdateAccessory(Item item, Player player, bool hideVisual)
        {
            if (!player.PIS().AllowedWings && item.wingSlot!=-1)
            {
                player.wingTime = 0;
                player.wingTimeMax = 0;
                player.noFallDmg = false;
                player.gravity = Player.defaultGravity;
            }
            if (!player.PIS().AllowedWings && item.type == ItemID.EmpressFlightBooster)
            {
                player.wingTime = 0;
                player.wingTimeMax = 0;
                player.noFallDmg = false;
                
            }
        } 
        public override void HoldItem(Item item, Player player)
        {
            if (ReloadTimer > 0)
                ReloadTimer--;
        }
        public override void SetDefaults(Item item)
        {
            if(item.type == ItemID.EmpressFlightBooster )
            {
                item.accessory = false;
            }
            if (item.type == ItemID.CandyCornRifle)
            {
                item.useAnimation = 8;
                item.useTime = 8;
                Magazine(33, 70);
            }
            if (item.type == ItemID.DartRifle)
            {
                Magazine(16, 70);
            }
            if (item.type == ItemID.DartPistol)
            { 
                Magazine(9, 70);
            }
            if (item.type == ItemID.VenusMagnum)
            {
                item.useAnimation = 7;
                item.useTime = 7;
                Magazine(44, 80);
            }
            if (item.type == ItemID.Uzi)
            {
                item.useAnimation = 7;
                item.useTime = 7;
                Magazine(32, 60);
            } 
            if (item.type == ItemID.OnyxBlaster)
            {
                item.useAnimation = 30;
                item.useTime =30;
                Magazine(6, 60);
            }
            if (item.type == ItemID.Gatligator)
            {
                item.useAnimation = 6;
                item.useTime = 6;
                Magazine(65, 100);
            }
            if (item.type == ItemID.SDMG)
            {
                item.useAnimation = 4;
                item.useTime = 4;
                Magazine(100, 140);
            }
            if (item.type == ItemID.ChainGun)
            {
                item.useAnimation = 3;
                item.useTime = 3;
                Magazine(100, 200);
            }
            if (item.type == ItemID.Megashark)
            {
                item.useAnimation = 6;
                item.useTime = 6;
                Magazine(98, 160);
            }
            if (item.type == ItemID.QuadBarrelShotgun)
            {
                item.useAnimation = 30;
                item.useTime = 30;
                Magazine(4, 90);
            }
            if (item.type == ItemID.RedRyder)
            {
                item.useAnimation = 20;
                item.useTime = 20;
                Magazine(2, 60);
            }
            if (item.type == ItemID.Revolver)
            {
                Magazine(6, 60);
            }
            if (item.type == ItemID.Minishark)
            {
                item.useAnimation = 6;
                item.useTime = 6;
                Magazine(76, 160);
            }
            if (item.type == ItemID.Blowgun)
            {
                item.useAnimation = 20;
                item.useTime = 20;
                Magazine(3, 90);
            }
            if (item.type == ItemID.Blowpipe)
            {
                item.useAnimation = 20;
                item.useTime = 20;
                Magazine(3, 90);
            }
            if (item.type == ItemID.PhoenixBlaster)
            {
                Magazine(6, 60);
            }
            if (item.type == ItemID.Handgun)
            {
                Magazine(6, 90);
            }
            if (item.type == ItemID.TacticalShotgun)
            {
                item.useAnimation = 25;
                item.useTime = 25;
                Magazine(5, 90);
            }
            if (item.type == ItemID.NailGun)
            {
                Magazine(25, 90);
            }
        }
        public override bool CanUseItem(Item item, Player player)
        {
            if (MagCount != 0)
            {
                return ReloadTimer <= 0;
            }
            return base.CanUseItem(item, player);
        }
        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (MagCount != 0)
            {

                ShotFired++;

                // If magazine reached 6 shots → start reload
                if (ShotFired >= MagCount)
                {
                    ShotFired = 0;
                    ReloadTimer = ReloadTime; // 1.5 seconds (60 ticks = 1 second)
                     
                }

                return true;
            }
            else
            {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);

            }
        } 
    }
}
