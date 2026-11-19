using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The Goblin Tinkerer's reforge, in a sub-box on the town chat panel.
    ///
    /// Vanilla runs the reforge from Main.DrawInventory with the chat box cleared away: the
    /// slot at (50, 270) under the inventory, "Reforge:" and the price beside it, the hammer
    /// 70 across and 40 down from the slot, the player's savings off to the right. Every bit of
    /// that is placed from the slot's top-left, two locals vanilla sets to 50 and 270 and
    /// reads from then on. So the whole reforge moves by moving those two numbers (Place),
    /// straight after vanilla sets them, into a ChatSubBox standing on the panel, which is drawn
    /// there first so everything vanilla draws lands on it. The savings are placed from
    /// invBottom rather than from the slot, so they are moved the same distance (Savings).
    ///
    /// Nothing else is touched: putting an item in and taking it out (ItemSlot's own left,
    /// right and shift clicks on the slot), the price and ItemLoader.ReforgePrice, CanReforge,
    /// PreReforge, the roll, PostReforge, the popup and the chime, the hammer's bloom and its
    /// gamepad point (from the moved numbers), the slot's gamepad point (from where it is
    /// drawn), and the item coming back when the reforge closes (Player.dropItemCheck) are all
    /// vanilla's, where vanilla does them.
    ///
    /// Only while TownChat has the townsperson's panel up for it; otherwise, and if the edit
    /// could not be made, the reforge is exactly vanilla's, at (50, 270).
    /// </summary>
    public static class ReforgeBox
    {
        /// <summary>Where vanilla puts the slot, and from where it places everything else.</summary>
        private const int VanillaX = 50;
        private const int VanillaY = 270;

        /// <summary>The hammer's centre, down from the slot's top: vanilla's 40.</summary>
        private const int HammerDown = 40;

        /// <summary>The savings, from the y they are given: the lowest thing ItemSlot.DrawSavings draws, the coins' counts.</summary>
        private const int SavingsDepth = 62;

        /// <summary>
        /// Room across for the widest of it: "Reforge:" 50 in from the slot, and a price in all
        /// four coins after it.
        /// </summary>
        private const int ContentWidth = 420;

        /// <summary>The whole edit went in. Until it has, Place and Savings change nothing.</summary>
        internal static bool Applied;

        /// <summary>This frame's reforge was moved into the box, and by how far down.</summary>
        private static bool moved;
        private static int movedDown;

        internal static void Clear()
        {
            Applied = false;
            moved = false;
            movedDown = 0;
        }

        /// <summary>
        /// Straight after vanilla sets the slot's top-left to (50, 270): the box on the panel,
        /// and the slot's top-left inside it. Not the panel's this frame, nothing moves.
        /// </summary>
        public static void Place(ref int x, ref int y)
        {
            moved = false;

            if (!Applied || !TownChat.LayoutForReforge(out Rectangle panel))
                return;

            int slotHeight = (int)(TextureAssets.InventoryBack.Height() * Main.inventoryScale);
            int hammerHeight = TextureAssets.Reforge[0].Value.Height;
            int savingsBottom = Main.instance.invBottom - VanillaY + SavingsDepth;

            int contentHeight = Math.Max(slotHeight, Math.Max(HammerDown + (hammerHeight + 1) / 2, savingsBottom));

            Rectangle box = ChatSubBox.OnPanel(panel, ContentWidth, contentHeight);
            ChatSubBox.Draw(Main.spriteBatch, box);
            ChatSubBox.KeepMouse(box);

            Vector2 at = ChatSubBox.Content(box);
            x = (int)at.X;
            y = (int)at.Y;

            moved = true;
            movedDown = y - VanillaY;
        }

        /// <summary>In place of the reforge's ItemSlot.DrawSavings: the same, moved down as far as the slot was.</summary>
        public static void Savings(SpriteBatch spriteBatch, float shopx, float shopy, bool horizontal)
        {
            if (moved)
                shopy += movedDown;

            ItemSlot.DrawSavings(spriteBatch, shopx, shopy, horizontal);
        }

        /// <summary>In place of the reforge slot's ItemSlot.Draw: the same, in the panel's slot frame while it is in the sub-box.</summary>
        public static void Slot(SpriteBatch spriteBatch, ref Item inv, int context, Vector2 position, Color lightColor)
        {
            if (moved)
                ChatSlot.Draw(spriteBatch, ref inv, context, position, lightColor);
            else
                ItemSlot.Draw(spriteBatch, ref inv, context, position, lightColor);
        }

        /// <summary>
        /// In Main.DrawInventory, inside "if (InReforgeMenu)" and before the Guide's menu:
        ///
        ///   ldc.i4 50; stloc x; ldc.i4 270; stloc y     Place(ref x, ref y) after it
        ///   call ItemSlot::DrawSavings                  Savings
        ///   call ItemSlot::Draw(SpriteBatch, ref Item, int, Vector2, Color), on reforgeItem    Slot
        ///
        /// All found, or none is changed.
        /// </summary>
        internal static bool Edit(ILContext il, out string missing)
        {
            Applied = false;
            missing = null;

            ILCursor c = new ILCursor(il);
            ILLabel guideMenu = null;

            if (!c.TryGotoNext(MoveType.After, i => i.MatchLdsfld(typeof(Main), nameof(Main.InReforgeMenu)), i => i.MatchBrfalse(out guideMenu)))
            {
                missing = "its reforge block";
                return false;
            }

            int end = il.IndexOf(guideMenu.Target);
            int xLocal = -1;
            int yLocal = -1;

            if (!c.TryGotoNext(MoveType.After,
                    i => i.MatchLdcI4(VanillaX), i => i.MatchStloc(out xLocal),
                    i => i.MatchLdcI4(VanillaY), i => i.MatchStloc(out yLocal))
                || c.Index > end)
            {
                missing = "the reforge slot's (50, 270)";
                return false;
            }

            Instruction placed = c.Prev;

            if (!c.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(ItemSlot), nameof(ItemSlot.DrawSavings))) || c.Index > end)
            {
                missing = "the reforge's DrawSavings";
                return false;
            }

            Instruction savings = c.Next;

            if (!c.TryGotoNext(MoveType.Before,
                    i => i.MatchLdsflda(typeof(Main), nameof(Main.reforgeItem)), i => i.MatchLdcI4(ItemSlot.Context.PrefixItem))
                || !c.TryGotoNext(MoveType.Before, i => IsSlotDraw(i))
                || c.Index > end)
            {
                missing = "the reforge slot's ItemSlot.Draw";
                return false;
            }

            Instruction slot = c.Next;

            savings.Operand = il.Import(typeof(ReforgeBox).GetMethod(nameof(Savings)));
            slot.Operand = il.Import(typeof(ReforgeBox).GetMethod(nameof(Slot)));

            c.Goto(placed, MoveType.After);
            c.Emit(OpCodes.Ldloca, il.Body.Variables[xLocal]);
            c.Emit(OpCodes.Ldloca, il.Body.Variables[yLocal]);
            c.Emit(OpCodes.Call, typeof(ReforgeBox).GetMethod(nameof(Place)));

            Applied = true;
            return true;
        }

        /// <summary>ItemSlot.Draw(SpriteBatch, ref Item, int, Vector2, Color): one item's slot.</summary>
        private static bool IsSlotDraw(Instruction i)
        {
            return i.MatchCall(out MethodReference m)
                && m.DeclaringType.FullName == typeof(ItemSlot).FullName
                && m.Name == nameof(ItemSlot.Draw)
                && m.Parameters.Count == 5
                && m.Parameters[1].ParameterType.IsByReference;
        }
    }
}
