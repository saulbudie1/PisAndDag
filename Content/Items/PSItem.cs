using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
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

        /// <summary>True for weapons that use the magazine system at all.</summary>
        public bool HasMagazine => MagCount > 0;

        public bool Reloading => ReloadTimer > 0;

        public int ShotsLeft => MagCount - ShotFired;

        public void Magazine(int mag, int reloadtime)
        {
            MagCount = mag;
            ReloadTime = reloadtime;

        }

        /// <summary>
        /// Begins a reload. Does nothing on a full magazine or one already reloading, so
        /// it is safe to call from a keybind held down.
        /// </summary>
        public bool StartReload()
        {
            if (!HasMagazine || Reloading || ShotFired <= 0)
                return false;

            ReloadTimer = ReloadTime;
            return true;
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
        /// <summary>
        /// UpdateInventory, not HoldItem — HoldItem only ticks the item currently in hand,
        /// so swapping weapons mid-reload used to freeze the timer until you switched back.
        /// </summary>
        public override void UpdateInventory(Item item, Player player)
        {
            if (ReloadTimer <= 0)
                return;

            ReloadTimer--;

            // Reset the count when the reload FINISHES. Doing it at the start made the
            // tooltip read a full magazine for the whole reload.
            if (ReloadTimer <= 0)
                ShotFired = 0;
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
        /// <summary>
        /// True for anything that fires bullets — vanilla or modded, this mod's or not.
        /// The muzzle effects hang off this rather than a per-weapon opt-in, so every gun
        /// in the game gets them for free.
        /// </summary>
        private static bool IsBulletGun(Item item) =>
            item.useAmmo == AmmoID.Bullet && item.DamageType == DamageClass.Ranged;

        /// <summary>
        /// Where the muzzle sits on a gun's sprite.
        ///   X = fraction of the sprite's width, forward from the hand
        ///   Y = pixels perpendicular to the barrel (negative is up when facing right)
        ///
        /// Scaling by sprite width handles a BIG gun, but not a differently SHAPED one.
        /// A pistol's barrel ends near the middle of its sprite; a rifle's ends at the
        /// very front because the stock fills the back half. One ratio cannot serve both,
        /// so anything that looks wrong gets an entry here instead of a new constant.
        /// </summary>
        private static readonly Dictionary<int, Vector2> MuzzleOverrides = new Dictionary<int, Vector2>
        {
            // Long guns: the barrel runs almost the whole sprite.
            [ItemID.Megashark] = new Vector2(0.95f, -2f),
            [ItemID.Minishark] = new Vector2(0.95f, -2f),
            [ItemID.ChainGun] = new Vector2(0.95f, -1f),
            [ItemID.SDMG] = new Vector2(0.95f, -2f),
            [ItemID.VenusMagnum] = new Vector2(0.9f, -1f),
            [ItemID.TacticalShotgun] = new Vector2(0.92f, 0f),
            [ItemID.Gatligator] = new Vector2(0.92f, 0f),

            // Short guns: barrel ends much sooner.
            [ItemID.Handgun] = new Vector2(0.7f, 0f),
            [ItemID.PhoenixBlaster] = new Vector2(0.7f, 0f),
            [ItemID.Revolver] = new Vector2(0.75f, 0f),
        };

        /// <summary>Used by any gun without an override. Suits a mid-length weapon.</summary>
        private static readonly Vector2 DefaultMuzzle = new Vector2(0.85f, 0f);

        /// <summary>
        /// Shoot's `position` is the player's hand, not the end of the gun — it carries no
        /// information about the sprite at all, which is why effects spawned there appear
        /// to come out of the player's chest.
        /// </summary>
        private static Vector2 BarrelTip(Item item, Player player, Vector2 position, Vector2 direction)
        {
            // item.width is the dropped-item hitbox and only approximates the sprite, so
            // prefer the actual texture when it has finished loading.
            float length = item.width;

            Asset<Texture2D> texture = TextureAssets.Item[item.type];
            if (texture != null && texture.IsLoaded)
                length = texture.Value.Width;

            if (!MuzzleOverrides.TryGetValue(item.type, out Vector2 spec))
                spec = DefaultMuzzle;

            // Perpendicular to the shot, mirrored with facing: Terraria flips a held gun
            // vertically when aiming left, so a barrel drawn above centre stays above
            // centre on screen and the offset has to follow it.
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * player.direction;

            return position
                + direction * (length * item.scale * spec.X)
                + perpendicular * (spec.Y * item.scale);
        }

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Decoration only, so never on a server.
            if (!Main.dedServ && IsBulletGun(item))
            {
                Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
                Vector2 muzzle = BarrelTip(item, player, position, direction);

                MuzzleFlash.Spawn(muzzle, direction);
                MuzzleSmoke.Spawn(muzzle, direction);
            }

            if (MagCount != 0)
            {

                ShotFired++;

                // Magazine empty — start the reload. ShotFired is deliberately NOT reset
                // here; UpdateInventory clears it when the timer runs out.
                if (ShotFired >= MagCount)
                    ReloadTimer = ReloadTime;

                return true;
            }
            else
            {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);

            }
        } 
    }
}
