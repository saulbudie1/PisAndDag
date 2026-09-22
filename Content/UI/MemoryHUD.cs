using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.Localization;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The casts in the Magic Arsenal of the kind the held catalyst fires, as a column
    /// of icons on the left, the one that is up in the ember frame. Only while a
    /// catalyst is in the hand: with a sword out there is nothing to choose, so nothing
    /// is shown. Same slot art as the arsenal window, smaller.
    ///
    /// The column stacks upward from the first memorised, so the Up key, which moves
    /// to the next, moves the light up the column.
    /// </summary>
    public class MemoryHUD : ModSystem
    {
        /// <summary>Left edge of the column, in UI pixels.</summary>
        private const float Left = 20f;

        private const float SlotScale = 0.6f;
        private const float Step = 34f;
        private const float IconScale = 0.75f;
        private const float TextScale = 0.8f;

        private static readonly Color Dim = new Color(150, 150, 150, 160);
        private static readonly Color NameColour = new Color(235, 225, 255);

        private static Asset<Texture2D> slotTexture;

        public override void Load()
        {
            if (!Main.dedServ)
                slotTexture = ModContent.Request<Texture2D>("PisAndDag/Content/UI/MagicArsenalSlot");
        }

        public override void Unload() => slotTexture = null;

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Memory",
                () =>
                {
                    Draw(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }

        private static void Draw(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;

            if (player == null || !player.active || player.dead || Main.hideUI)
                return;

            if (player.HeldItem?.ModItem is not Catalyst catalyst)
                return;

            if (slotTexture == null || !slotTexture.IsLoaded)
                return;

            MagicArsenal arsenal = player.GetModPlayer<MagicArsenal>();
            List<Castable> casts = arsenal.OfKind(catalyst.Kind);
            Castable up = arsenal.Selected(catalyst.Kind);

            Texture2D slots = slotTexture.Value;
            float size = 52f * SlotScale;

            // Bottom of the column is the first memorised; it grows upward from there,
            // sitting on the left at mid height where nothing of vanilla's lives.
            float bottom = Main.screenHeight / Main.UIScale * 0.5f + Step * 2f;

            for (int i = 0; i < casts.Count; i++)
            {
                Castable cast = casts[i];
                bool chosen = up != null && cast.Type == up.Type;

                Vector2 at = new Vector2(Left, bottom - Step * i);

                Rectangle frame = slots.Frame(1, 4, 0, chosen ? 2 : 1);
                spriteBatch.Draw(slots, at, frame, Color.White, 0f, Vector2.Zero, SlotScale, SpriteEffects.None, 0f);

                Vector2 centre = at + new Vector2(size * 0.5f);
                ItemSlot.DrawItemIcon(cast.Item, ItemSlot.Context.HotbarItem, spriteBatch, centre, IconScale, 32f, Color.White);

                if (chosen)
                {
                    Vector2 text = new Vector2(at.X + size + 8f, centre.Y - FontAssets.MouseText.Value.MeasureString(cast.DisplayName.Value).Y * TextScale * 0.5f);
                    Utils.DrawBorderString(spriteBatch, cast.DisplayName.Value, text, NameColour, TextScale);
                }
            }

            if (casts.Count == 0)
                Utils.DrawBorderString(spriteBatch, Language.GetTextValue("Mods.PisAndDag.Magic.NothingIn"), new Vector2(Left, bottom + 8f), Dim, 0.7f);
        }
    }
}
