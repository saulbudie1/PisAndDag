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
        private static int cameraFocusFrames;
        private static float cameraBlend;

        /// <summary>
        /// Blends the camera between where it would normally sit and a world point.
        ///
        /// 0 leaves the shot on the player, 1 centres it on the target, and anything
        /// between is a partial move. The blend is NOT a smoothing factor — the caller
        /// supplies the curve, which is what makes a timed pan possible rather than the
        /// exponential drift a per-frame lerp toward a fixed strength produces.
        ///
        /// Vanilla has already placed the camera on the player by the time this is
        /// applied, so the lerp starts from wherever the player actually is and the shot
        /// keeps working if they move mid-pan.
        ///
        /// Only valid for the frame it is called on. Call it every tick for as long as
        /// the shot should hold; stop calling it and the camera returns on its own.
        /// </summary>
        public static void BlendCamera(Vector2 world, float blend)
        {
            if (Main.dedServ)
                return;

            cameraFocus = world;
            cameraBlend = MathHelper.Clamp(blend, 0f, 1f);

            // Two frames of slack: AI runs on ticks and drawing does not, so a one-frame
            // window would flicker whenever a frame lands between updates.
            cameraFocusFrames = 2;
        }

        public static void ReleaseCamera() => cameraFocusFrames = 0;

        // The outro pan. Separate from the focus above because it has to keep running
        // after whatever was holding the camera is gone — the Megashark's death ends with
        // the boss no longer existing, and something still has to walk the shot home.
        private static Vector2 outroFrom;
        private static int outroFrames;
        private static int outroLength;

        /// <summary>
        /// Releases the camera from a world point back onto the player over
        /// <paramref name="frames"/> ticks, and keeps doing it whether or not anyone is
        /// still alive to ask.
        ///
        /// Unlike BlendCamera this is fire-and-forget: it is called once, it owns the
        /// camera for the whole pan, and it takes the hold off whatever had it.
        /// </summary>
        public static void PanHome(Vector2 from, int frames)
        {
            if (Main.dedServ)
                return;

            outroFrom = from;
            outroLength = Math.Max(1, frames);
            outroFrames = outroLength;

            // Whatever was holding the shot lets go now, or the two fight for the frame.
            cameraFocusFrames = 0;
        }

        /// <summary>Smoothstep. Zero slope at both ends, so the pan starts and stops softly.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

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

        /// <summary>
        /// Raises the shake, and never lowers it.
        ///
        /// AddScreenShake overwrites outright, which is fine for a one-off but wrong for
        /// anything that fires in bursts: a small effect landing during a big one replaces
        /// it, so an explosion's shake can be cut short by a spark that goes off a few
        /// ticks later. Anything that can overlap itself or something louder should come
        /// through here instead.
        /// </summary>
        public void BumpScreenShake(float power, int duration)
        {
            if (power > screenShake)
                screenShake = MathHelper.Clamp(power, 0f, 60f);

            shakeTime = Math.Max(shakeTime, duration);
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
            if (cameraFocusFrames > 0)
            {
                cameraFocusFrames--;

                Vector2 wanted = cameraFocus - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
                Main.screenPosition = Vector2.Lerp(Main.screenPosition, wanted, cameraBlend);
            }
            else if (outroFrames > 0)
            {
                outroFrames--;

                // Main.screenPosition is the player's own shot at this point, so the pan
                // eases from the held point ONTO it rather than toward a target computed
                // here — which means it still lands correctly if the player is moving.
                Vector2 held = outroFrom - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
                float t = 1f - outroFrames / (float)outroLength;

                Main.screenPosition = Vector2.Lerp(held, Main.screenPosition, Ease(t));
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
