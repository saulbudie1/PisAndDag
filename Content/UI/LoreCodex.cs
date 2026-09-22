using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PisAndDag.Content.Items.LoreItem;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The Lore Codex: a shelf for the items the player is not allowed to lose.
    ///
    /// Drawn immediately rather than through UIState. The whole thing is a button and a
    /// grid of vanilla item slots, and going through the UI framework for that would mean
    /// reimplementing what ItemSlot.Draw already does — slot chrome, item scaling, stack
    /// counts, the favourite star — so that it could be wrapped in elements that add
    /// nothing. Immediate drawing also puts the click handling and the drawing in the same
    /// place, which for a grid this small is easier to follow than the two halves of a
    /// retained-mode widget.
    /// </summary>
    public class LoreCodex : ModSystem
    {
        // ---- the button ------------------------------------------------------
        //
        // Vanilla hardcodes its own two: the bestiary at x 498 and the emote menu at 534,
        // both at y 244 + 30 + 4, thirty pixels square. Thirty-six apart. This one
        // continues the row, and copies vanilla's shifts so it travels with them when a
        // chest opens or a sign is being renamed.

        private const int ButtonX = 570;
        private const int ButtonY = 244 + 30 + 4;
        private const int ButtonSize = 30;

        private const int ChestShiftX = 5;
        private const int ChestShiftY = 168;
        private const int EditChestShiftY = 24;

        // ---- the window ------------------------------------------------------

        private const int Columns = 4;

        /// <summary>Rows on screen before the grid stops growing and starts scrolling.</summary>
        private const int VisibleRows = 4;

        private const int WindowX = 498;
        private const int WindowY = 320;

        /// <summary>Vanilla's own inventory scale, so a codex slot matches an inventory slot.</summary>
        private const float SlotScale = 0.85f;

        /// <summary>Slot pitch and slot size at that scale, from vanilla's 56 and 52.</summary>
        private static int Step => (int)(56f * SlotScale);
        private static int SlotSize => (int)(52f * SlotScale);

        private const int Padding = 12;

        private static readonly Color PanelColour = new Color(23, 25, 81, 255) * 0.925f;

        public static bool Open;

        private static int scroll;

        /// <summary>
        /// Raw wheel position, read from the mouse rather than from Terraria's own scroll
        /// plumbing. It is three lines and it cannot be reading a field that moved.
        /// </summary>
        private static int lastWheel;

        private static Asset<Texture2D> buttonTexture;

        /// <summary>
        /// One-slot scratch array. ItemSlot.Draw wants an array and an index because that
        /// is how every vanilla inventory is shaped; the codex is a List, so each slot is
        /// lent to this for the length of one draw call.
        /// </summary>
        private static readonly Item[] scratch = new Item[1];

        public override void Load()
        {
            if (!Main.dedServ)
                buttonTexture = ModContent.Request<Texture2D>("PisAndDag/Content/UI/LoreCodexButton");
        }

        public override void Unload()
        {
            buttonTexture = null;
            Open = false;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventory = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");

            if (inventory == -1)
                return;

            // Directly on top of the inventory: the codex has to draw over the slots it
            // sits beside, and under the mouse item, which is a later layer.
            layers.Insert(inventory + 1, new LegacyGameInterfaceLayer(
                "PisAndDag: Lore Codex", DrawEverything, InterfaceScaleType.UI));
        }

        private static bool ChestOrShopOpen =>
            Main.LocalPlayer.chest != -1 || Main.npcShop != 0;

        private bool DrawEverything()
        {
            // Read the wheel every frame whether or not it is used, or the first scroll
            // after the window opens jumps by however far it turned while closed.
            int wheel = Mouse.GetState().ScrollWheelValue;
            int wheelDelta = wheel - lastWheel;
            lastWheel = wheel;

            // Escape closes the inventory, and the codex goes with it — there is no second
            // state to get out of sync. Opening a container closes it too: they occupy the
            // same corner of the screen and the same job in the player's head.
            if (!Main.playerInventory || ChestOrShopOpen)
            {
                Open = false;
                return true;
            }

            DrawButton();

            if (Open)
                DrawWindow(wheelDelta);

            return true;
        }

        private static void DrawButton()
        {
            if (buttonTexture == null || !buttonTexture.IsLoaded)
                return;

            int x = ButtonX;
            int y = ButtonY;

            if (ChestOrShopOpen)
            {
                x += ChestShiftX;
                y += ChestShiftY;
            }

            if (Main.editChest)
                y += EditChestShiftY;

            Rectangle box = new Rectangle(x, y, ButtonSize, ButtonSize);
            bool hover = box.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface;

            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease = false;
                    Open = !Open;
                    scroll = 0;
                    SoundEngine.PlaySound(SoundID.MenuTick);

                    // Same corner as the Magic Arsenal; only one of the two is open.
                    if (Open)
                        MagicArsenalUI.Open = false;
                }
            }

            Texture2D texture = buttonTexture.Value;

            // Two frames side by side, lit one on the right — the same shape vanilla's own
            // bestiary and emote buttons use, so the hover glow comes from swapping frames
            // rather than from tinting, and can be drawn rather than computed.
            Rectangle frame = texture.Frame(2, 1, hover ? 1 : 0);
            frame.Width -= 2;
            frame.Height -= 2;

            Main.spriteBatch.Draw(texture, box.Center.ToVector2(), frame, Color.White, 0f,
                frame.Size() / 2f, 1f, SpriteEffects.None, 0f);

            if (hover)
                Main.instance.MouseText("Open Lore Inventory", 0, 0);
        }

        private static void DrawWindow(int wheelDelta)
        {
            List<Item> codex = Main.LocalPlayer.GetModPlayer<LoreLock>().Codex;

            // Always one empty slot past the last stored item, so there is somewhere to
            // drop the next one. The grid grows by a row whenever that spare slot would
            // fall off the end of the current one.
            int slots = codex.Count + 1;
            int rows = Math.Max(1, (slots + Columns - 1) / Columns);
            int shown = Math.Min(rows, VisibleRows);

            int maxScroll = Math.Max(0, rows - VisibleRows);
            scroll = Math.Clamp(scroll, 0, maxScroll);

            int width = Columns * Step - (Step - SlotSize) + Padding * 2;
            int height = shown * Step - (Step - SlotSize) + Padding * 2;

            Rectangle panel = new Rectangle(WindowX - Padding, WindowY - Padding, width, height);
            bool overPanel = panel.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface;

            if (overPanel)
            {
                // Claims the mouse for the UI, which is what stops a click landing on the
                // world behind the window.
                Main.LocalPlayer.mouseInterface = true;

                if (wheelDelta != 0 && maxScroll > 0)
                    scroll = Math.Clamp(scroll - Math.Sign(wheelDelta), 0, maxScroll);
            }

            Utils.DrawInvBG(Main.spriteBatch, panel, PanelColour);

            // ItemSlot reads this rather than taking a scale argument.
            float restore = Main.inventoryScale;
            Main.inventoryScale = SlotScale;

            for (int row = 0; row < shown; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int index = (row + scroll) * Columns + column;

                    if (index >= slots)
                        continue;

                    Vector2 position = new Vector2(WindowX + column * Step, WindowY + row * Step);

                    DrawSlot(codex, index, position);
                }
            }

            Main.inventoryScale = restore;

            if (maxScroll > 0)
                DrawScrollBar(panel, rows, shown, maxScroll);
        }

        private static void DrawSlot(List<Item> codex, int index, Vector2 position)
        {
            bool stored = index < codex.Count;

            scratch[0] = stored ? codex[index] : new Item();

            Rectangle box = new Rectangle((int)position.X, (int)position.Y, SlotSize, SlotSize);
            bool hover = box.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface;

            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease = false;
                    Click(codex, index);
                }

                // How a custom UI asks vanilla to draw a real item tooltip: hand it the
                // item and let the normal tooltip pass do the rest, hover text and all.
                if (!scratch[0].IsAir)
                {
                    Main.HoverItem = scratch[0].Clone();
                    Main.hoverItemName = scratch[0].Name;
                }
            }

            ItemSlot.Draw(Main.spriteBatch, scratch, ItemSlot.Context.ChestItem, 0, position);

            scratch[0] = null;
        }

        /// <summary>
        /// Take one out, or put one in. Nothing but a lore item may go in, and never a
        /// second copy of one already on the shelf — the codex is a record of what the
        /// player has found, and a record with the same entry twice is a worse record.
        /// </summary>
        private static void Click(List<Item> codex, int index)
        {
            Item mouse = Main.mouseItem;
            bool stored = index < codex.Count;

            if (stored && mouse.IsAir)
            {
                Main.mouseItem = codex[index];
                codex.RemoveAt(index);
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (mouse.IsAir || !mouse.PAD().Lore)
                return;

            if (Holds(codex, mouse.type))
                return;

            if (stored)
            {
                // A swap, so dropping one lore item onto another does the obvious thing
                // instead of silently refusing.
                Item taken = codex[index];
                codex[index] = mouse;
                Main.mouseItem = taken;
            }
            else
            {
                codex.Add(mouse);
                Main.mouseItem = new Item();
            }

            SoundEngine.PlaySound(SoundID.Grab);
        }

        private static bool Holds(List<Item> codex, int type)
        {
            for (int i = 0; i < codex.Count; i++)
            {
                if (codex[i].type == type)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A track down the right edge with a thumb sized to the fraction on screen. Drawn
        /// from MagicPixel rather than from a bar texture, because at four pixels wide the
        /// only thing a nine-slice would add is a file.
        /// </summary>
        private static void DrawScrollBar(Rectangle panel, int rows, int shown, int maxScroll)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            const int barWidth = 4;
            int barX = panel.Right - Padding + 2;
            int barTop = panel.Y + Padding;
            int barHeight = panel.Height - Padding * 2;

            Main.spriteBatch.Draw(pixel, new Rectangle(barX, barTop, barWidth, barHeight),
                new Color(0, 0, 0, 140));

            int thumbHeight = Math.Max(12, barHeight * shown / rows);
            int travel = barHeight - thumbHeight;
            int thumbTop = barTop + (maxScroll == 0 ? 0 : travel * scroll / maxScroll);

            Main.spriteBatch.Draw(pixel, new Rectangle(barX, thumbTop, barWidth, thumbHeight),
                new Color(237, 130, 24));
        }
    }
}
