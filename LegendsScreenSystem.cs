using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Drives the Legends screen filter: fades it in, holds it, fades it back out, then
    /// switches it off. Registration lives in the Mod class; this only animates it.
    /// </summary>
    public class LegendsScreenSystem : ModSystem
    {
        public const string FilterKey = "PisAndDag:Legends";

        /// <summary>
        /// Set by the Mod class once the effect has actually loaded. Everything here is a
        /// no-op until then, so a missing or uncompiled .xnb cannot crash the game — it
        /// just means no filter.
        /// </summary>
        internal static bool Registered;

        /// <summary>What everything behind the player turns into.</summary>
        private static readonly Vector3 BehindColor = new Vector3(0.80f, 0.04f, 0.06f);

        /// <summary>What solid things turn into.</summary>
        private static readonly Vector3 SolidColor = Vector3.Zero;

        /// <summary>Progress at which the effect has finished ramping up to full.</summary>
        private const float RampIn = 0.3f;

        /// <summary>Progress at which it starts easing back down.</summary>
        private const float RampOut = 0.7f;

        private static int timer;
        private static int duration;

        /// <summary>Runs the whole fade over the given number of ticks.</summary>
        public static void Trigger(int ticks)
        {
            if (Main.dedServ || !Registered || ticks <= 0)
                return;

            duration = ticks;
            timer = ticks;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || !Registered)
                return;

            if (timer <= 0)
            {
                if (Filters.Scene[FilterKey].IsActive())
                    Filters.Scene.Deactivate(FilterKey);

                return;
            }

            timer--;

            // 0 at the start, 1 at the end.
            float progress = 1f - (float)timer / duration;

            // A trapezoid with eased corners: ramp up to full by RampIn, sit at full
            // until RampOut, then ease back down. Unlike a sine, which only touches 1
            // for an instant at the midpoint, this actually holds the effect at maximum
            // for the middle 40% of its life.
            float intensity;

            if (progress < RampIn)
            {
                // SmoothStep is cubic, so it leaves 0 and arrives at 1 with no visible
                // corner — a plain linear ramp reads as a hard switch-on.
                intensity = MathHelper.SmoothStep(0f, 1f, progress / RampIn);
            }
            else if (progress < RampOut)
            {
                intensity = 1f;
            }
            else
            {
                intensity = MathHelper.SmoothStep(1f, 0f, (progress - RampOut) / (1f - RampOut));
            }

            if (!Filters.Scene[FilterKey].IsActive())
                Filters.Scene.Activate(FilterKey);

            Filters.Scene[FilterKey].GetShader()
                .UseIntensity(intensity)
                .UseColor(BehindColor)
                .UseSecondaryColor(SolidColor);
        }

        public override void OnWorldUnload()
        {
            // Leaving a world mid-effect would otherwise strand the filter on.
            timer = 0;

            if (!Main.dedServ && Registered && Filters.Scene[FilterKey].IsActive())
                Filters.Scene.Deactivate(FilterKey);
        }
    }
}
