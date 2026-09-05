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
                // CanFly() asks whether this mount is CAPABLE of flight. The old test used
                // FlyTime, which is the REMAINING flight counter — ResetFlightTime refills
                // it from horizontal velocity, so it reads full while you stand on the
                // ground and often zero in mid-air. That is why mounting after a jump
                // slipped straight past the check.
                //
                // CanHover covers mounts that hold altitude without burning flight time.
                // Drop that half of the condition if you want hovering mounts allowed.
                if (Player.mount.Active && !AllowedWings
                    && (Player.mount.CanFly() || Player.mount.CanHover()))
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
