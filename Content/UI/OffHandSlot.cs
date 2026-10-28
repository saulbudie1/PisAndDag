using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The off hand's slot (OffHandPlayer), in its own frame (OffHandSlot.png: a bone rim on a plum ground,
    /// with a faint open hand while it is empty, OffHandSlotHand.png), just left of the trash, where the
    /// inventory has nothing; it moves with the trash when a chest or a shop is open. Shown only where the
    /// world has the off hand open (the Guide's quest). Takes only what OffHandPlayer.Fits; the cursor's
    /// item is swapped in by a left click, as any slot.
    /// </summary>
    public class OffHandSlot : ModSystem
    {
        private static Asset<Texture2D> frame;
        private static Asset<Texture2D> hand;

        private const int Context = ItemSlot.Context.InventoryItem;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            frame = ModContent.Request<Texture2D>("PisAndDag/Content/UI/OffHandSlot");
            hand = ModContent.Request<Texture2D>("PisAndDag/Content/UI/OffHandSlotHand");
        }

        public override void Unload()
        {
            frame = null;
            hand = null;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");

            if (index == -1)
                return;

            layers.Insert(index + 1, new LegacyGameInterfaceLayer("PisAndDag: Off Hand", () =>
            {
                Draw(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.UI));
        }

        /// <summary>Where the trash is, as the game puts it (Main.DrawTrashItemSlot), and its scale.</summary>
        private static Vector2 Trash(out float scale)
        {
            scale = 0.85f;
            float x = 448f;
            float y = 258f;

            if ((Main.LocalPlayer.chest != -1 || Main.npcShop > 0) && !Main.recBigList)
            {
                y += 168f;
                scale = 0.755f;
                x += 5f;
            }
            else if ((Main.LocalPlayer.chest == -1 || Main.npcShop == -1) && Main.trashSlotOffset != Terraria.DataStructures.Point16.Zero)
            {
                x += Main.trashSlotOffset.X;
                y += Main.trashSlotOffset.Y;
                scale = 0.755f;
            }

            return new Vector2(x, y);
        }

        private static void Draw(SpriteBatch spriteBatch)
        {
            if (!Main.playerInventory || !OffHandPlayer.Open || frame == null || !frame.IsLoaded)
                return;

            Player player = Main.LocalPlayer;

            if (!player.TryGetModPlayer(out OffHandPlayer off))
                return;

            Vector2 trash = Trash(out float scale);
            float size = TextureAssets.InventoryBack.Width() * scale;
            Vector2 at = trash - new Vector2(56f * scale, 0f);
            Rectangle area = new Rectangle((int)at.X, (int)at.Y, (int)size, (int)size);

            if (area.Contains(Main.MouseScreen.ToPoint()) && !PlayerInput.IgnoreMouseInterface)
            {
                player.mouseInterface = true;

                // While it is being used its item is in the hotbar: nothing to take here till it is back.
                if (!off.Busy)
                {
                    if (OffHandPlayer.Fits(Main.mouseItem))
                        ItemSlot.LeftClick(ref off.Item, Context);

                    if (!off.Item.IsAir)
                        ItemSlot.MouseHover(ref off.Item, Context);
                    else
                        Main.hoverItemName = "Off Hand" + Keys() + "\nA weapon or a tool, used by its own key, its primary use only";
                }
            }

            float oldScale = Main.inventoryScale;
            Main.inventoryScale = scale;

            spriteBatch.Draw(frame.Value, at, null, Main.inventoryBack, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            Item item = off.Busy ? null : off.Item;

            if (item != null && !item.IsAir)
            {
                ItemSlot.DrawItemIcon(item, Context, spriteBatch, at + new Vector2(size) / 2f, scale, 32f, Color.White);

                if (item.stack > 1)
                {
                    ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, item.stack.ToString(),
                        at + new Vector2(10f, 26f) * scale, Color.White, 0f, Vector2.Zero, new Vector2(scale), -1f, scale);
                }
            }
            else if (hand != null && hand.IsLoaded)
            {
                Texture2D glyph = hand.Value;
                spriteBatch.Draw(glyph, at + new Vector2(size) / 2f, null, Color.White * 0.3f, 0f, glyph.Size() / 2f, scale, SpriteEffects.None, 0f);
            }

            Main.inventoryScale = oldScale;
        }

        /// <summary>The key or button set for the off hand, in brackets, or nothing with none.</summary>
        private static string Keys()
        {
            List<string> keys = PADKeybinds.OffHand?.GetAssignedKeys();
            return keys == null || keys.Count == 0 ? " (no key set)" : " (" + string.Join(", ", keys.Distinct()) + ")";
        }
    }
}
