using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    public class PisPlayer:ModPlayer
    {
        public bool AllowedWings;
        public float screenShake = 0f; 
        public int shakeTime = 0;
        public override void ResetEffects()
        {
            AllowedWings = false;

        }
        public override void UpdateDead()
        {
            AllowedWings = false;
        }
        public void AddScreenShake(float power, int duration)
        {
            screenShake = MathHelper.Clamp(power, 0f, 60f); // cap max so it doesn’t get insane
            shakeTime = duration;
        } 

        public override void PreUpdateMovement()
        { 
            // Check if mounted and the mount can fly
            if (Player.mount.Active && !AllowedWings)
            {
                var mountData = Player.mount;
                 
                if (mountData.FlyTime!=0  )
                { 
                    Player.mount.Dismount(Player);
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
