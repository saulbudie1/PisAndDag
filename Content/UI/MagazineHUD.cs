using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// Small text beside the player: shots remaining, or "Reloading" while the timer runs.
    /// </summary>
    public class MagazineHUD : ModSystem
    {
        private static readonly Color ReadyColor = new Color(235, 235, 235);
        private static readonly Color ReloadingColor = new Color(214, 84, 84);

        /// <summary>Offset from the player's centre, in world pixels.</summary>
        private static readonly Vector2 Offset = new Vector2(22f, -10f);

        private const float TextScale = 0.75f;

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Magazine",
                () =>
                {
                    Draw(Main.spriteBatch);
                    return true;
                },
                // Game, not UI: this scale type applies the world zoom matrix, so text
                // drawn at (worldPos - screenPosition) lands on the player at any zoom
                // without a manual transform.
                InterfaceScaleType.Game));
        }

        private static void Draw(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;

            if (player == null || !player.active || player.dead)
                return;

            Item held = player.HeldItem;
            if (held == null || held.IsAir)
                return;

            PSItem mag = held.PIS();
            if (mag == null || !mag.HasMagazine)
                return;

            string text = mag.Reloading ? "Reloading" : mag.ShotsLeft.ToString();
            Color color = mag.Reloading ? ReloadingColor : ReadyColor;

            Vector2 position = player.Center + Offset - Main.screenPosition;

            Utils.DrawBorderString(spriteBatch, text, position, color, TextScale);
        }
    }
}
