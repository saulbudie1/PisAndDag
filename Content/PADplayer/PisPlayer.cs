using Microsoft.Xna.Framework;
using PisAndDag.Content.Items;
using PisAndDag.Content.Particles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    public class PisPlayer:ModPlayer
    {
        public bool AllowedWings;

        /// <summary>
        /// What is left of a wing's flight time once the Empress has been beaten.
        ///
        /// The same fraction does both jobs: it scales whatever the wings claim, and it
        /// scales the ceiling — so the cap is three fifths of the best wings in the game
        /// rather than three fifths of whatever a modded pair asserts about itself.
        /// </summary>
        private const float FlightFraction = 3f / 5f;

        /// <summary>Fallback ceiling, for the impossible case of no readable wing stats.</summary>
        private const int SolarFlightFallback = 180;
        public float screenShake = 0f;
        public int shakeTime = 0;

        // ---- Chlorophyte Rifle combo ------------------------------------------------
        //
        // This lives on PisPlayer and not PADPlayer for one reason: PADPlayer is wiped by
        // ResetEffects every tick, and a combo that survives less than a frame is not a
        // combo. Same rule as the camera and the shake — state that has to span ticks
        // belongs on this side of the split.

        /// <summary>Consecutive rifle rounds that have connected.</summary>
        public int ChloCombo;

        /// <summary>Ticks left before the streak lapses on its own.</summary>
        public int ChloComboTimer;

        /// <summary>True once the streak is long enough that the next shot bursts.</summary>
        public bool ChloArmed;

        /// <summary>Hits needed to arm the burst.</summary>
        public const int ChloComboRequired = 5;

        /// <summary>How long a streak survives without a hit. Three seconds.</summary>
        public const int ChloComboDecay = 180;

        /// <summary>Damage added per stack, and the number of stacks that count.</summary>
        public const float ChloDamagePerStack = 0.1f;
        public const int ChloMaxStacks = 5;

        /// <summary>
        /// Extra damage the current streak is worth, as a multiplier on the base.
        /// Capped, so a long streak stops paying before it runs away.
        /// </summary>
        public float ChloDamageBonus =>
            1f + Math.Min(ChloCombo, ChloMaxStacks) * ChloDamagePerStack;

        /// <summary>A round connected. Called from PADGlobalProjectile on the owner only.</summary>
        public void ChloRegisterHit()
        {
            ChloCombo++;
            ChloComboTimer = ChloComboDecay;

            if (ChloCombo >= ChloComboRequired)
                ChloArmed = true;
        }

        /// <summary>
        /// Streak over. Every path that ends one funnels through here rather than zeroing
        /// the fields at four call sites — the reload path being missed is exactly how the
        /// reload sound came to only play on a manual reload.
        /// </summary>
        public void ChloBreakCombo()
        {
            ChloCombo = 0;
            ChloComboTimer = 0;
            ChloArmed = false;
        }

        /// <summary>Consumes the armed state. Returns true if this shot should burst.</summary>
        public bool ChloConsumeBurst()
        {
            if (!ChloArmed)
                return false;

            ChloBreakCombo();
            return true;
        }

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
        /// mashing the key does nothing.
        ///
        /// The sound is NOT played here. It used to be, which is exactly why an automatic
        /// reload was silent: emptying a magazine starts its reload from PSItem.Shoot,
        /// which never came through this method. The sound now belongs to the reload
        /// itself, inside PSItem, so every way of starting one is audible.
        /// </summary>
        private void TryReload()
        {
            Item held = Player.HeldItem;
            if (held == null || held.IsAir)
                return;

            held.PIS().StartReload(Player);
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

        /// <summary>
        /// The one and only way flight is ever granted.
        ///
        /// Wings, the booster and flying mounts are all inert until the Empress of Light
        /// falls; after that they work, but on the leash below. Written every frame rather
        /// than latched, so the flag can never survive into a world where it is not earned.
        /// </summary>
        public override void ResetEffects()
        {
            AllowedWings = PADWorld.FlightUnlocked;
        }

        public override void UpdateDead()
        {
            AllowedWings = PADWorld.FlightUnlocked;
            ChloBreakCombo();
        }

        /// <summary>
        /// Trims flight time down to the mod's allowance.
        ///
        /// PostUpdateMiscEffects rather than PostUpdateEquips, because every accessory
        /// bonus has landed by then — Calamity applies its own flight multiplier in this
        /// same hook, and capping ahead of those would leave the ceiling behind.
        /// </summary>
        public override void PostUpdateMiscEffects()
        {
            if (!AllowedWings || Player.wingTimeMax <= 0)
                return;

            int ceiling = (int)(SolarFlightTime() * FlightFraction);

            Player.wingTimeMax = Math.Min((int)(Player.wingTimeMax * FlightFraction), ceiling);

            // The charge has to come down with the maximum too, or swapping to worse wings
            // mid-air leaves the player flying on a tank the new pair never had.
            if (Player.wingTime > Player.wingTimeMax)
                Player.wingTime = Player.wingTimeMax;
        }

        /// <summary>
        /// The best flight time the game currently offers, read at runtime.
        ///
        /// Read rather than hardcoded so the ceiling still means "the best wings there
        /// are" if something rebalances them — Calamity rewrites a dozen wing stat lines
        /// on load, and a literal 180 here would quietly stop matching its Solar Wings.
        /// </summary>
        private static int SolarFlightTime()
        {
            int slot = ContentSamples.ItemsByType[ItemID.WingsSolar].wingSlot;
            WingStats[] stats = ArmorIDs.Wing.Sets.Stats;

            return slot >= 0 && slot < stats.Length ? stats[slot].FlyTime : SolarFlightFallback;
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

        /// <summary>
        /// Grounds flying mounts on the same terms as wings.
        ///
        /// The Legends check this used to sit behind is gone: wings are blocked in every
        /// world (PSItem does it unconditionally), so leaving a UFO flying outside Legends
        /// was a hole in the rule rather than a feature of it. Once the Empress is down
        /// AllowedWings is true and nothing here fires.
        /// </summary>
        public override void PreUpdateMovement()
        {
            if (AllowedWings || !Player.mount.Active)
                return;

            if (Player.mount.FlyTime != 0)
                Player.mount.Dismount(Player);
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

            TickChloCombo();
        }

        /// <summary>
        /// Ages the rifle combo and drops it when the rifle is not in hand.
        ///
        /// The weapon-swap check is here rather than on the item because a ModItem stops
        /// receiving hooks the moment it leaves the hotbar slot in use — the streak would
        /// simply freeze and still be waiting when the rifle came back out.
        /// </summary>
        private void TickChloCombo()
        {
            if (ChloCombo <= 0 && !ChloArmed)
                return;

            Item held = Player.HeldItem;
            bool holdingRifle = held != null && !held.IsAir
                && held.ModItem is Content.Items.Ranged.Chlorophyte_Rifle;

            if (!holdingRifle)
            {
                ChloBreakCombo();
                return;
            }

            // Reloading deliberately does NOT end the streak.
            //
            // It was going to. It cannot: the rifle holds three rounds and the burst
            // wants five consecutive hits, so a streak that died at every reload could
            // never reach five and the burst would be unreachable by arithmetic. The
            // decay timer below already stops a streak surviving a lull, which is the
            // part that actually mattered.

            if (--ChloComboTimer <= 0)
                ChloBreakCombo();
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
