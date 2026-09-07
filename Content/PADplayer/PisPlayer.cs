using Microsoft.Xna.Framework;
using PisAndDag.Content.Items;
using PisAndDag.Content.Particles;
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

        // Camera focus. Static because it belongs to the view, not to any one player —
        // whoever is looking at the screen is the local player by definition.
        private static Vector2 cameraFocus;
        private static int cameraFocusTime;
        private static float cameraFocusStrength;

        /// <summary>
        /// Pulls the camera toward a world point for a while. Call it repeatedly with a
        /// short duration to track something that moves, or once with a long one to hold
        /// on a fixed spot.
        /// </summary>
        public static void FocusCamera(Vector2 world, int ticks, float strength = 0.8f)
        {
            if (Main.dedServ)
                return;

            cameraFocus = world;
            cameraFocusTime = ticks;
            cameraFocusStrength = MathHelper.Clamp(strength, 0f, 1f);
        }

        public static void ReleaseCamera() => cameraFocusTime = 0;

        /// <summary>
        /// Keybind handling. ProcessTriggers only runs for the local player, so nothing
        /// here needs a whoAmI check.
        ///
        /// Each bind gets its own guard clause rather than an early return for the whole
        /// method — a bare `if (!Reload.JustPressed) return;` at the top would silently
        /// swallow every bind added after it.
        /// </summary>
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (PADKeybinds.Reload != null && PADKeybinds.Reload.JustPressed)
                TryReload();

            if (PADKeybinds.Lightning != null && PADKeybinds.Lightning.JustPressed)
                ThrowLightning();
        }

        /// <summary>
        /// Manual reload. StartReload refuses a full or already-reloading magazine, so
        /// mashing the key does nothing and the sound only plays on a real reload.
        /// </summary>
        private void TryReload()
        {
            Item held = Player.HeldItem;
            if (held == null || held.IsAir)
                return;

            if (held.PIS().StartReload())
                SoundEngine.PlaySound(SoundID.Item149, Player.Center);
        }

        /// <summary>
        /// Arcs from the player to the cursor.
        ///
        /// Visual only — the particle deals no damage and is never synced, so in
        /// multiplayer only the player who pressed the key sees it. Fire it from wherever
        /// a hit is already being resolved if you want it to mean something.
        /// </summary>
        private void ThrowLightning()
        {
            Vector2 target = Main.MouseWorld;

            // Firing at your own feet gives a zero-length arc with nothing to displace.
            if (Vector2.DistanceSquared(Player.Center, target) < 16f * 16f)
                return;

            Lightning.Spawn(Player.Center, target, new Color(120, 235, 255), width: 26f,
                lifetime: 18, forks: 2);

            // The vanilla lightning bug zap, by path rather than by SoundID constant —
            // three variants, so repeated casts do not sound identical.
            SoundStyle zap = new SoundStyle("Terraria/Sounds/Custom/dd2_lightning_bug_zap_" + Main.rand.Next(3))
            {
                Volume = 0.5f,
                PitchVariance = 0.25f,
                MaxInstances = -1
            };

            SoundEngine.PlaySound(zap, Player.Center);
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
            // Focus first, shake second, so the shake reads as camera wobble on top of
            // wherever the camera is pointed rather than fighting it.
            if (cameraFocusTime > 0)
            {
                cameraFocusTime--;

                Vector2 wanted = cameraFocus - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
                Main.screenPosition = Vector2.Lerp(Main.screenPosition, wanted, cameraFocusStrength * 0.12f);
            }

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
