using Microsoft.Xna.Framework;
using PisAndDag.Content.Items;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    public class PisPlayer:ModPlayer
    {
        public bool AllowedWings;
        public float screenShake = 0f; 
        public int shakeTime = 0;

        /// <summary>
        /// Manual reload. StartReload refuses a full or already-reloading magazine, so
        /// mashing the key does nothing and the sound only plays on a real reload.
        /// </summary>
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!PADKeybinds.Reload.JustPressed)
                return;

            Item held = Player.HeldItem;
            if (held == null || held.IsAir)
                return;

            if (held.PIS().StartReload())
                SoundEngine.PlaySound(SoundID.Item149, Player.Center);
        }

        public override void ResetEffects()
        {
            if (PADWorld.Legends)
            {

                AllowedWings = false;
            }

        }
        public override void UpdateDead()
        {
            if (PADWorld.Legends)
            {

                AllowedWings = false;
            }
        }
        public void AddScreenShake(float power, int duration)
        {
            screenShake = MathHelper.Clamp(power, 0f, 60f); // cap max so it doesn’t get insane
            shakeTime = duration;
        }

        public override void PreUpdateMovement()
        {
            if (PADWorld.Legends)
            {
                // Check if mounted and the mount can fly
                if (Player.mount.Active && !AllowedWings)
                {
                    var mountData = Player.mount;

                    if (mountData.FlyTime != 0)
                    {
                        Player.mount.Dismount(Player);
                    }
                }
            }
        }
        public override void PostUpdate()
        {
            if (shakeTime > 0)
            {
                shakeTime--;
            }
            else
            { 
                screenShake *= 0.90f;
            } 
        }
        public override void ModifyScreenPosition()
        {
            if (screenShake > 0f)
            { 
                Main.screenPosition += new Vector2(
                    Main.rand.NextFloat(-screenShake, screenShake),
                    Main.rand.NextFloat(-screenShake, screenShake)
                );
            } 
        }
    }
}
