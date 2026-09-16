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

        // ---- camera --------------------------------------------------------------------
        //
        // Static because it belongs to the view, not to any one player — whoever is
        // looking at the screen is the local player by definition.
        //
        // Two layers. Callers describe the SHOT, every tick, with whatever curve they
        // like: a world point, and how far toward it the camera should sit. Underneath,
        // the camera itself is a critically damped spring chasing that shot — so whatever
        // the callers hand it (an eased pan, a hard hold on a boss that is dashing, a
        // curve with a kink in it, a shot that starts sixteen pixels off where vanilla had
        // the camera) the thing on screen never changes speed inside a single frame. And
        // when a hold ends, however it ends, the camera walks home instead of snapping.

        private static Vector2 cameraFocus;
        private static int cameraFocusFrames;
        private static float cameraBlend;

        /// <summary>Where the camera actually is (top-left) while this code is driving it.</summary>
        private static Vector2 cam;
        private static Vector2 camVelocity;

        /// <summary>True when the previous frame was ours, so the next shot starts from cam.</summary>
        private static bool driving;

        /// <summary>
        /// Time constant of the spring, in seconds. About five frames: short enough that
        /// an authored pan keeps the shape that was written for it, long enough to take
        /// the corner off a kink and the jitter out of a target that moves in steps.
        /// </summary>
        private const float SmoothTime = 0.08f;

        /// <summary>Longest walk home when a hold ends without asking for one.</summary>
        private const int ReleaseFrames = 40;

        /// <summary>Pixels of walk home per frame, which sets how long a short one takes.</summary>
        private const float ReleaseSpeed = 10f;

        /// <summary>
        /// Blends the camera between where it would normally sit and a world point.
        ///
        /// 0 leaves the shot on the player, 1 centres it on the target, and anything
        /// between is a partial move. The blend is NOT a smoothing factor — the caller
        /// supplies the curve, which is what makes a timed pan possible rather than the
        /// exponential drift a per-frame lerp toward a fixed strength produces. The
        /// smoothing that IS applied (see ModifyScreenPosition) is a fixed, short spring
        /// on top of the result, the same for every caller.
        ///
        /// Only valid for the frame it is called on. Call it every tick for as long as
        /// the shot should hold; stop calling it and the camera walks home on its own.
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

        // The walk home. Separate from the focus above because it has to keep running
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
        /// Fire-and-forget: called once, it owns the camera for the whole pan and takes
        /// the hold off whatever had it. If the camera is ours at the moment of the call
        /// the walk starts from where it actually is, not from the point given, so there
        /// is no cut between the hold and the walk.
        /// </summary>
        public static void PanHome(Vector2 from, int frames)
        {
            if (Main.dedServ)
                return;

            outroFrom = driving ? cam : from - Half;
            outroLength = Math.Max(1, frames);
            outroFrames = outroLength;

            // Whatever was holding the shot lets go now, or the two fight for the frame.
            cameraFocusFrames = 0;
        }

        /// <summary>True while a walk home is running.</summary>
        public static bool WalkingHome => outroFrames > 0;

        private static Vector2 Half => new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;

        /// <summary>Smoothstep. Zero slope at both ends, so the pan starts and stops softly.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Critically damped spring toward a target: Unity's SmoothDamp, whose polynomial
        /// is the exp() approximation from Game Programming Gems 4. Never overshoots, and
        /// its output is smooth in velocity no matter what the target does.
        /// </summary>
        private static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 velocity,
            float smoothTime, float dt)
        {
            float omega = 2f / smoothTime;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            Vector2 change = current - target;
            Vector2 temp = (velocity + omega * change) * dt;

            velocity = (velocity - omega * temp) * exp;

            Vector2 output = target + (change + temp) * exp;

            // The approximation can cross the target by a hair at large steps; pin it.
            if (Vector2.Dot(target - current, output - target) > 0f)
            {
                output = target;
                velocity = Vector2.Zero;
            }

            return output;
        }

        // ---- interface -----------------------------------------------------------------

        private static int hideUIFrames;
        private static bool hidingUI;
        private static bool uiWasHidden;

        /// <summary>
        /// True while the interface is hidden by a sequence rather than by the player's
        /// own F11. The boss bar reads this to stay up through a cutscene and still go
        /// away when the player actually asks for no UI.
        /// </summary>
        public static bool HidingUI => hidingUI;

        /// <summary>
        /// Hides the interface — hotbar, bars, minimap, boss bar, cursor: exactly what F11
        /// hides — for as long as this keeps being called. Per tick, like BlendCamera, so
        /// a sequence that dies mid-way cannot strand the player without a UI. Gives the
        /// player's own F11 state back when it lets go, and stays hidden through a walk
        /// home that began while hidden, so the UI comes back with the camera.
        /// </summary>
        public static void HideUI()
        {
            if (!Main.dedServ)
                hideUIFrames = 2;
        }

        internal static void TickUI()
        {
            bool wanted = hideUIFrames > 0 || (hidingUI && WalkingHome);

            if (hideUIFrames > 0)
                hideUIFrames--;

            if (wanted)
            {
                if (!hidingUI)
                {
                    hidingUI = true;
                    uiWasHidden = Main.hideUI;
                }

                Main.hideUI = true;
            }
            else if (hidingUI)
            {
                hidingUI = false;
                Main.hideUI = uiWasHidden;
            }
        }

        /// <summary>World unload: hand everything back, or the next world inherits it.</summary>
        internal static void ResetView()
        {
            cameraFocusFrames = 0;
            outroFrames = 0;
            driving = false;
            camVelocity = Vector2.Zero;
            hideUIFrames = 0;

            if (hidingUI)
            {
                hidingUI = false;
                Main.hideUI = uiWasHidden;
            }
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

            if (PADKeybinds.Wind != null && PADKeybinds.Wind.JustPressed)
                StepWind();
        }

        private void StepWind() => WindTest.Step(Player);

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
        /// <summary>
        /// Runs once per drawn frame, from Main.DoDraw_UpdateCameraPosition, after vanilla
        /// has put the camera on the player and before it truncates the result to whole
        /// pixels. Main.screenPosition on entry is therefore vanilla's own shot.
        /// </summary>
        public override void ModifyScreenPosition()
        {
            Vector2 live = Main.screenPosition;
            bool drove = driving;
            driving = false;

            // Focus first, shake second, so the shake reads as camera wobble on top of
            // wherever the camera is pointed rather than fighting it.
            if (cameraFocusFrames > 0)
            {
                cameraFocusFrames--;
                outroFrames = 0;

                Vector2 wanted = Vector2.Lerp(live, cameraFocus - Half, cameraBlend);

                // Picks up from wherever the camera was last frame — ours if we had it,
                // vanilla's if not — so the spring never starts with a jump in it.
                if (!drove)
                {
                    cam = live;
                    camVelocity = Vector2.Zero;
                }

                cam = SmoothDamp(cam, wanted, ref camVelocity, SmoothTime, 1f / 60f);
                Main.screenPosition = cam;
                driving = true;
            }
            else
            {
                // A hold that ended without asking to be walked home gets walked home
                // anyway — from where the camera is, at a fixed speed, so a hold that had
                // already eased itself back onto the player costs nothing and one that
                // was cut off mid-boss takes as long as the distance deserves.
                if (drove && outroFrames == 0)
                {
                    float distance = Vector2.Distance(cam, live);

                    if (distance > 1.5f)
                    {
                        outroFrom = cam;
                        outroLength = (int)MathHelper.Clamp(distance / ReleaseSpeed, 10f, ReleaseFrames);
                        outroFrames = outroLength;
                    }
                }

                if (outroFrames > 0)
                {
                    outroFrames--;

                    // Eases from the held point ONTO vanilla's live shot rather than toward
                    // a target computed here, so it still lands if the player is moving.
                    float t = 1f - outroFrames / (float)outroLength;
                    cam = Vector2.Lerp(outroFrom, live, Ease(t));
                    Main.screenPosition = cam;

                    // Ours until the last frame of the walk, and not one frame longer —
                    // or the frame after would read as another hold ending.
                    driving = outroFrames > 0;
                }
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

    /// <summary>
    /// Ticks the interface hold and hands the view back on unload. A ModSystem because
    /// PostUpdateEverything is the one place that runs after every NPC and system has had
    /// its say in a tick, which is when the keep-alives for this tick are all in.
    /// </summary>
    public class ViewSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            if (!Main.dedServ)
                PisPlayer.TickUI();
        }

        public override void OnWorldUnload()
        {
            if (!Main.dedServ)
                PisPlayer.ResetView();
        }
    }
}
