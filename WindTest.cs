using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Test wind. "/wind" steps it, "/wind 0.5" sets it. (The O bind that stepped it is the off hand's now: PADKeybinds.OffHand.)
    ///
    /// Vanilla's own numbers: 0.4 is where it calls a day windy, 0.85 is the most it
    /// ever blows. Current and target both, or the weather drifts it straight back.
    /// Surface only: there is no wind underground, so nothing would show. Singleplayer
    /// or host; the server owns the weather. Always says in chat what it did.
    /// </summary>
    public static class WindTest
    {
        private static readonly float[] Steps = { 0f, 0.25f, 0.5f, 0.85f, -0.25f, -0.5f, -0.85f };

        private static readonly Color SeaColour = new Color(96, 132, 168);

        public static void Step(Player player)
        {
            if (!Allowed(player))
                return;

            int at = 0;
            float now = Main.windSpeedTarget;

            for (int i = 0; i < Steps.Length; i++)
            {
                if (Math.Abs(Steps[i] - now) < 0.01f)
                {
                    at = (i + 1) % Steps.Length;
                    break;
                }
            }

            Apply(Steps[at]);
        }

        public static void Set(Player player, float wind)
        {
            if (!Allowed(player))
                return;

            Apply(MathHelper.Clamp(wind, -1f, 1f));
        }

        private static bool Allowed(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                Main.NewText("The host owns the weather.", Color.OrangeRed);
                return false;
            }

            if (player.Center.Y > Main.worldSurface * 16f)
            {
                Main.NewText("No wind down here.", Color.Gray);
                return false;
            }

            return true;
        }

        private static void Apply(float wind)
        {
            Main.windSpeedTarget = wind;
            Main.windSpeedCurrent = wind;

            float strength = Math.Abs(wind);
            string band = strength < 0.12f ? "still" : strength < 0.4f ? "breeze" : strength < 0.7f ? "windy" : "gale";
            string way = wind == 0f ? "" : wind > 0f ? ", blowing right" : ", blowing left";

            Main.NewText("Wind " + wind.ToString("0.00") + " (" + band + way + ")", SeaColour);
        }
    }

    public class WindCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "wind";

        public override string Usage => "/wind [value]";

        public override string Description => "Steps the wind for testing, or sets it to a value from -1 to 1.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length > 0 && float.TryParse(args[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float wind))
            {
                WindTest.Set(caller.Player, wind);
                return;
            }

            WindTest.Step(caller.Player);
        }
    }
}
