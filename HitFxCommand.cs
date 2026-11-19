using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for the hit kit (HitFx) and the half-size canvas (PADPixelCanvas).
    ///
    ///   /hitfx            all four at the cursor, side by side: light, heavy, slash, ground
    ///   /hitfx light      (or heavy, slash, ground) that one at the cursor, going the way the player faces
    ///
    /// Nothing is hurt; it is only what is seen. On the player's own machine.
    /// </summary>
    public class HitFxCommand : ModCommand
    {
        private static readonly Color Gold = new Color(255, 206, 110);

        public override CommandType Type => CommandType.Chat;

        public override string Command => "hitfx";

        public override string Usage => "/hitfx [light|heavy|slash|ground]";

        public override string Description => "Shows the hit kit's effects at the cursor, for testing.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            Vector2 at = Main.MouseWorld;
            Vector2 way = new Vector2(caller.Player.direction, 0f);
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "light":
                    HitFx.Light(at, way, Gold);
                    break;

                case "heavy":
                    HitFx.Heavy(at, way, Gold);
                    break;

                case "slash":
                    HitFx.Slash(at, new Vector2(way.X, -0.5f), Gold);
                    break;

                case "ground":
                    HitFx.Ground(at, new Color(150, 130, 100));
                    break;

                default:
                    HitFx.Light(at + new Vector2(-180f, 0f), way, Gold);
                    HitFx.Heavy(at + new Vector2(-60f, 0f), way, Gold);
                    HitFx.Slash(at + new Vector2(70f, 0f), new Vector2(way.X, -0.5f), Gold);
                    HitFx.Ground(at + new Vector2(190f, 0f), new Color(150, 130, 100));
                    break;
            }
        }
    }
}
