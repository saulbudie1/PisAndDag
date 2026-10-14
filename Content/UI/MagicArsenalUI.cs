using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Accessories;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.PADplayer;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The Magic Arsenal window: ten slots on a stone panel beside the inventory, the
    /// first three open, the rest banded shut. A spell or an incantation dropped in is
    /// memorised; picked up, it is not. The one that is up for each kind wears the
    /// ember frame. Right click a cast to make it the one that is up.
    ///
    /// Drawn immediately, the way the Lore Codex is, and for the same reasons. The two
    /// share a corner and a job, so opening one closes the other.
    ///
    /// The slots take and give only near a lived-in house. Elsewhere they still show,
    /// dimmed, and a click says why.
    ///
    /// The slot the Necklace Of The Academy holds open is a little bluer than the rest and wears
    /// a small picture of the necklace in its corner. A cast left in it when the necklace comes
    /// off shows through the bands, faint; a click there gives it back, and nothing can be put in.
    /// </summary>
    public class MagicArsenalUI : ModSystem
    {
        // ---- the button ------------------------------------------------------
        //
        // Directly above the Lore Codex button, on the row of vanilla's sort and quick
        // stack icons. Same shifts as the codex when a chest opens.

        private const int ButtonX = 570;
        private const int ButtonY = 244;
        private const int ButtonSize = 30;

        private const int ChestShiftX = 5;
        private const int ChestShiftY = 168;
        private const int EditChestShiftY = 24;

        // ---- the window ------------------------------------------------------

        private const int Columns = 5;
        private const int Rows = MagicArsenal.MaxSlots / Columns;

        private const int WindowX = 498;
        private const int WindowY = 320;

        /// <summary>Vanilla's own inventory scale, so an arsenal slot matches an inventory slot.</summary>
        private const float SlotScale = 0.85f;

        private static int Step => (int)(56f * SlotScale);
        private static int SlotSize => (int)(52f * SlotScale);

        private const int Padding = 14;

        /// <summary>Room above the slots for the name of the thing.</summary>
        private const int TitleHeight = 24;

        /// <summary>The panel texture's slice ends, in pixels.</summary>
        private const int PanelEnd = 16;

        /// <summary>The window's name: dark brown, outlined in light grey so it reads off the stone.</summary>
        private static readonly Color TitleColour = new Color(74, 44, 26);
        private static readonly Color TitleStroke = new Color(198, 194, 188);
        private static readonly Color NoteColour = new Color(150, 140, 130);
        private static readonly Color Dimmed = new Color(120, 120, 120);

        /// <summary>A cast kept in a shut slot, seen through the bands.</summary>
        private static readonly Color Kept = Color.White * 0.45f;

        /// <summary>How big the necklace's mark in the corner of its slot is drawn, at most, in pixels.</summary>
        private const float MarkSize = 12f;

        /// <summary>The necklace's slot, a little bluer than the rest: the enamel of its medallion.</summary>
        private static readonly Color NecklaceTint = new Color(196, 212, 255);

        public static bool Open;

        private static Asset<Texture2D> buttonTexture;
        private static Asset<Texture2D> panelTexture;
        private static Asset<Texture2D> slotTexture;

        /// <summary>Frames down the slot sheet.</summary>
        private enum SlotFrame
        {
            Empty,
            Filled,
            Selected,
            Locked,
        }

        public override void Load()
        {
            if (Main.dedServ)
                return;

            buttonTexture = ModContent.Request<Texture2D>("PisAndDag/Content/UI/MagicArsenalButton");
            panelTexture = ModContent.Request<Texture2D>("PisAndDag/Content/UI/MagicArsenalPanel");
            slotTexture = ModContent.Request<Texture2D>("PisAndDag/Content/UI/MagicArsenalSlot");
        }

        public override void Unload()
        {
            buttonTexture = null;
            panelTexture = null;
            slotTexture = null;
            Open = false;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventory = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");

            if (inventory == -1)
                return;

            layers.Insert(inventory + 1, new LegacyGameInterfaceLayer(
                "PisAndDag: Magic Arsenal", DrawEverything, InterfaceScaleType.UI));
        }

        private static bool ChestOrShopOpen =>
            Main.LocalPlayer.chest != -1 || Main.npcShop != 0;

        private bool DrawEverything()
        {
            if (!Main.playerInventory || ChestOrShopOpen)
            {
                Open = false;
                return true;
            }

            // One corner, one window. If the codex has it, this one is shut.
            if (LoreCodex.Open)
                Open = false;

            DrawButton();

            if (Open)
                DrawWindow();

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

                    if (Open)
                        LoreCodex.Open = false;

                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            Texture2D texture = buttonTexture.Value;
            Rectangle frame = texture.Frame(2, 1, hover ? 1 : 0);
            frame.Width -= 2;
            frame.Height -= 2;

            Main.spriteBatch.Draw(texture, box.Center.ToVector2(), frame, Color.White, 0f,
                frame.Size() / 2f, 1f, SpriteEffects.None, 0f);

            if (hover)
                Main.instance.MouseText(Language.GetTextValue("Mods.PisAndDag.Magic.OpenArsenal"), 0, 0);
        }

        private static void DrawWindow()
        {
            MagicArsenal arsenal = Main.LocalPlayer.GetModPlayer<MagicArsenal>();
            bool nearHome = arsenal.NearHome();

            int width = Columns * Step - (Step - SlotSize) + Padding * 2;
            int height = TitleHeight + Rows * Step - (Step - SlotSize) + Padding * 2;

            Rectangle panel = new Rectangle(WindowX - Padding, WindowY - Padding, width, height);

            if (panel.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface)
                Main.LocalPlayer.mouseInterface = true;

            if (panelTexture != null && panelTexture.IsLoaded)
            {
                Utils.DrawSplicedPanel(Main.spriteBatch, panelTexture.Value, panel.X, panel.Y, panel.Width, panel.Height,
                    PanelEnd, PanelEnd, PanelEnd, PanelEnd, Color.White);
            }

            // The name, and beside it either the count or the reason the slots are shut.
            string title = Language.GetTextValue("Mods.PisAndDag.Magic.Arsenal");
            Vector2 titleAt = new Vector2(WindowX, WindowY - 2);
            MagicColours.Stroked(Main.spriteBatch, FontAssets.MouseText.Value, title, titleAt,
                TitleColour, TitleStroke, new Vector2(0.9f));

            string note = nearHome
                ? Language.GetTextValue("Mods.PisAndDag.Magic.Count", Filled(arsenal), arsenal.OpenSlots)
                : Language.GetTextValue("Mods.PisAndDag.Magic.NoHouseShort");
            float noteWidth = FontAssets.MouseText.Value.MeasureString(note).X * 0.75f;
            Utils.DrawBorderString(Main.spriteBatch, note, new Vector2(WindowX + width - Padding * 2 - noteWidth, WindowY + 2), NoteColour, 0.75f);

            Castable upSpell = arsenal.Selected(CastKind.Spell);
            Castable upIncantation = arsenal.Selected(CastKind.Incantation);

            float restore = Main.inventoryScale;
            Main.inventoryScale = SlotScale;

            for (int i = 0; i < MagicArsenal.MaxSlots; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                Vector2 position = new Vector2(WindowX + column * Step, WindowY + TitleHeight + row * Step);

                DrawSlot(arsenal, i, position, nearHome, upSpell, upIncantation);
            }

            Main.inventoryScale = restore;
        }

        private static int Filled(MagicArsenal arsenal)
        {
            int count = 0;

            for (int i = 0; i < arsenal.OpenSlots; i++)
            {
                if (!arsenal.Slots[i].IsAir)
                    count++;
            }

            return count;
        }

        private static void DrawSlot(MagicArsenal arsenal, int index, Vector2 position, bool nearHome, Castable upSpell, Castable upIncantation)
        {
            Item item = arsenal.Slots[index];
            bool locked = index >= arsenal.OpenSlots;
            Castable cast = locked ? null : arsenal.CastAt(index);
            bool up = cast != null && (cast.Type == upSpell?.Type || cast.Type == upIncantation?.Type);

            SlotFrame frame = locked ? SlotFrame.Locked : up ? SlotFrame.Selected : item.IsAir ? SlotFrame.Empty : SlotFrame.Filled;

            Rectangle box = new Rectangle((int)position.X, (int)position.Y, SlotSize, SlotSize);
            bool hover = box.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface;

            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease = false;
                    Click(arsenal, index, locked, nearHome);
                }

                if (Main.mouseRight && Main.mouseRightRelease && cast != null)
                {
                    Main.mouseRightRelease = false;
                    arsenal.Select(cast);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }

                if (locked && item.IsAir)
                    Main.instance.MouseText(Language.GetTextValue("Mods.PisAndDag.Magic.Locked"), 0, 0);
                else if (locked)
                    Main.instance.MouseText(Language.GetTextValue("Mods.PisAndDag.Magic.LockedKept", item.Name), 0, 0);
                else if (!item.IsAir)
                {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
                else if (index == arsenal.NecklaceSlot)
                    Main.instance.MouseText(Language.GetTextValue("Mods.PisAndDag.Magic.NecklaceSlot",
                        Lang.GetItemNameValue(ModContent.ItemType<NecklaceOfTheAcademy>())), 0, 0);
            }

            Color tint = locked || nearHome ? Color.White : Dimmed;
            Color frameTint = index == arsenal.NecklaceSlot ? tint.MultiplyRGB(NecklaceTint) : tint;

            if (slotTexture != null && slotTexture.IsLoaded)
            {
                Texture2D texture = slotTexture.Value;
                Rectangle source = texture.Frame(1, 4, 0, (int)frame);

                Main.spriteBatch.Draw(texture, position, source, frameTint, 0f, Vector2.Zero, SlotScale, SpriteEffects.None, 0f);
            }

            if (!item.IsAir)
            {
                Vector2 centre = position + new Vector2(SlotSize * 0.5f);
                ItemSlot.DrawItemIcon(item, ItemSlot.Context.ChestItem, Main.spriteBatch, centre, SlotScale, 32f, locked ? Kept : tint);
            }

            if (index == arsenal.NecklaceSlot)
            {
                // The necklace itself, small, tucked into the bottom right corner: the slot is its.
                Item necklace = ContentSamples.ItemsByType[ModContent.ItemType<NecklaceOfTheAcademy>()];
                Vector2 corner = position + new Vector2(SlotSize - MarkSize * 0.5f - 2f);
                ItemSlot.DrawItemIcon(necklace, ItemSlot.Context.ChestItem, Main.spriteBatch, corner, 1f, MarkSize, tint);
            }
        }

        /// <summary>
        /// Take one out, or put one in. Only a cast goes in, never a second of the same,
        /// and only near a house. A cast dropped on a cast swaps. A shut slot takes nothing:
        /// it only gives back a cast left in it, into an empty hand.
        /// </summary>
        private static void Click(MagicArsenal arsenal, int index, bool locked, bool nearHome)
        {
            Item mouse = Main.mouseItem;
            Item slot = arsenal.Slots[index];

            if (locked && (slot.IsAir || !mouse.IsAir))
                return;

            if (mouse.IsAir && slot.IsAir)
                return;

            if (!nearHome)
            {
                arsenal.Say("NoHouse");
                return;
            }

            if (mouse.IsAir)
            {
                Main.mouseItem = slot;
                arsenal.Slots[index] = new Item();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (mouse.ModItem is not Castable)
            {
                arsenal.Say("NotACast");
                return;
            }

            if (arsenal.Holds(mouse.type) && slot.type != mouse.type)
            {
                arsenal.Say("AlreadyHeld");
                return;
            }

            arsenal.Slots[index] = mouse;
            Main.mouseItem = slot.IsAir ? new Item() : slot;

            SoundEngine.PlaySound(SoundID.Grab);
        }
    }
}
