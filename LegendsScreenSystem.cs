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

            // sin(progress * pi) is 0 at both ends and 1 in the middle, so one curve
            // gives the fade in, the hold and the fade back out. Same shape the orbiting
            // motes use, for the same reason.
            float intensity = MathF.Sin(progress * MathHelper.Pi); 

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
