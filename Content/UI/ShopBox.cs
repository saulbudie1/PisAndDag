using System;
using System.Collections.Generic;
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
    /// A townsperson's shop, inside their chat panel.
    ///
    /// Vanilla runs the shop from Main.DrawInventory: "Shop" and the player's savings at
    /// (504, invBottom), and the 10 × 4 grid at (73, invBottom) at an inventory scale of 0.755,
    /// with a box over the grid that keeps the mouse. Every position in it, for drawing and for
    /// the mouse alike, is worked out from those three numbers: the grid's slot positions are
    /// two locals made from 73 and invBottom, and each slot's hit test and its draw both read
    /// those same two locals. So the shop moves by moving the numbers, where vanilla loads them
    /// (Edit), and nothing else:
    ///
    ///   73 (three times: the mouse box, twice; the slots)       GridX: the panel's text column
    ///   invBottom (the mouse box, twice; the slots)             GridY: the top of the text column
    ///   504, invBottom (the "Shop" title and the savings)      TitleX, TitleY: down the answers' column
    ///
    /// Buying, selling by dropping on the grid, buyback, prices on hover, shift-click and
    /// right-click buying, the Painter's two shops, the Travelling Merchant's and every mod's
    /// shop are vanilla's own code reading the moved numbers. The panel is drawn first, at the
    /// start of the block (Begin), so the grid lands on it, and stays up with the face and the
    /// name tab; Close stands in the answers' column and does what vanilla does when the
    /// conversation ends (Main.CloseNPCChatOrSign: the shop shuts itself, in this same block, as
    /// soon as nobody is being talked to). The grid's slots are drawn in ChatSlot's frame.
    ///
    /// Only while TownChat has the townsperson's panel up for it; otherwise, and if the edit
    /// could not be made, the shop is exactly vanilla's.
    /// </summary>
    public static class ShopBox
    {
        /// <summary>Vanilla's shop: the inventory scale it sets, its grid and the pitch of its slots.</summary>
        private const float ShopScale = 0.755f;
        private const int Columns = 10;
        private const int Rows = 4;
        private const int Pitch = 56;

        /// <summary>Vanilla's anchors, as they appear in the block.</summary>
        private const int VanillaGridX = 73;
        private const float VanillaTitleX = 504f;

        /// <summary>The whole edit went in. Until it has, every seam changes nothing.</summary>
        internal static bool Applied;

        /// <summary>This frame's shop is in the panel, and where its parts go.</summary>
        private static bool moved;
        private static int gridX;
        private static int gridY;
        private static float titleX;
        private static int titleY;

        internal static void Clear()
        {
            Applied = false;
            moved = false;
        }

        /// <summary>The grid's size at the shop's scale, as vanilla lays it out: the last slot's corner.</summary>
        internal static Point GridSize()
        {
            int slotWidth = (int)Math.Ceiling(TextureAssets.InventoryBack.Width() * ShopScale);
            int slotHeight = (int)Math.Ceiling(TextureAssets.InventoryBack.Height() * ShopScale);

            return new Point((int)((Columns - 1) * Pitch * ShopScale) + slotWidth, (int)((Rows - 1) * Pitch * ShopScale) + slotHeight);
        }

        /// <summary>At the top of vanilla's shop block: the panel, drawn, and where the grid and the title go in it.</summary>
        public static void Begin()
        {
            moved = false;

            if (!Applied || !TownChat.LayoutForShop(GridSize(), out Point grid, out Vector2 title))
                return;

            gridX = grid.X;
            gridY = grid.Y;
            titleX = title.X;
            titleY = (int)title.Y;
            moved = true;
        }

        /// <summary>For each 73 in the block.</summary>
        public static float GridLeft(float vanilla) => moved ? gridX : vanilla;
        public static int GridLeftInt(int vanilla) => moved ? gridX : vanilla;

        /// <summary>For the grid's and its mouse box's invBottom.</summary>
        public static int GridTop(int vanilla) => moved ? gridY : vanilla;

        /// <summary>For the title's and the savings' 504 and invBottom.</summary>
        public static float TitleLeft(float vanilla) => moved ? titleX : vanilla;
        public static int TitleTop(int vanilla) => moved ? titleY : vanilla;

        /// <summary>In place of the grid's ItemSlot.Draw: the same, in the panel's slot frame while the shop is in the panel.</summary>
        public static void Slot(SpriteBatch spriteBatch, Item[] inv, int context, int slot, Vector2 position, Color lightColor)
        {
            if (moved)
                ChatSlot.Draw(spriteBatch, inv, context, slot, position, lightColor);
            else
                ItemSlot.Draw(spriteBatch, inv, context, slot, position, lightColor);
        }

        /// <summary>
        /// In Main.DrawInventory, the block under "if (npcShop &gt; 0 &amp;&amp; !recBigList)", up to where
        /// that test jumps when it fails. Inside it, exactly:
        ///
        ///   ldc.r4 504                 twice    TitleLeft after each
        ///   ldfld Main::invBottom      five times: the first two TitleTop after them, the other three GridTop
        ///   ldc.i4 73                  once     GridLeftInt after it
        ///   ldc.r4 73                  twice    GridLeft after each
        ///   call ItemSlot::Draw(SpriteBatch, Item[], int, int, Vector2, Color)   once   Slot
        ///
        /// and Begin at the block's top. Any count off, and nothing is changed.
        /// </summary>
        internal static bool Edit(ILContext il, out string missing)
        {
            Applied = false;
            missing = null;

            ILCursor c = new ILCursor(il);
            ILLabel after = null;

            if (!c.TryGotoNext(MoveType.After,
                    i => i.MatchCall(typeof(Main), "get_npcShop"), i => i.MatchLdcI4(0), i => i.MatchBle(out _),
                    i => i.MatchLdsfld(typeof(Main), nameof(Main.recBigList)), i => i.MatchBrtrue(out after)))
            {
                missing = "its shop block";
                return false;
            }

            Instruction start = c.Next;
            int end = il.IndexOf(after.Target);

            var titleX = new List<Instruction>();
            var bottoms = new List<Instruction>();
            var gridXInt = new List<Instruction>();
            var gridXFloat = new List<Instruction>();
            var draws = new List<Instruction>();

            for (int i = il.IndexOf(start); i < end; i++)
            {
                Instruction ins = il.Instrs[i];

                if (ins.MatchLdcR4(VanillaTitleX))
                    titleX.Add(ins);
                else if (ins.MatchLdfld(typeof(Main), nameof(Main.invBottom)))
                    bottoms.Add(ins);
                else if (ins.MatchLdcI4(VanillaGridX))
                    gridXInt.Add(ins);
                else if (ins.MatchLdcR4(VanillaGridX))
                    gridXFloat.Add(ins);
                else if (ins.MatchCall(out MethodReference m) && m.DeclaringType.FullName == typeof(ItemSlot).FullName
                    && m.Name == nameof(ItemSlot.Draw) && m.Parameters.Count == 6 && m.Parameters[1].ParameterType.FullName == typeof(Item[]).FullName)
                    draws.Add(ins);
            }

            if (titleX.Count != 2 || bottoms.Count != 5 || gridXInt.Count != 1 || gridXFloat.Count != 2 || draws.Count != 1)
            {
                missing = $"the shop's anchors as expected (504 ×{titleX.Count}, invBottom ×{bottoms.Count}, 73 ×{gridXInt.Count}+{gridXFloat.Count}, slot draws ×{draws.Count})";
                return false;
            }

            // Everything is there. Now the changes, each of which leaves the stack as it found it.
            foreach (Instruction ins in titleX)
                After(c, ins, nameof(TitleLeft));

            for (int i = 0; i < bottoms.Count; i++)
                After(c, bottoms[i], i < 2 ? nameof(TitleTop) : nameof(GridTop));

            After(c, gridXInt[0], nameof(GridLeftInt));

            foreach (Instruction ins in gridXFloat)
                After(c, ins, nameof(GridLeft));

            draws[0].Operand = il.Import(typeof(ShopBox).GetMethod(nameof(Slot)));

            c.Goto(start, MoveType.Before);
            c.Emit(OpCodes.Call, typeof(ShopBox).GetMethod(nameof(Begin)));

            Applied = true;
            return true;
        }

        /// <summary>A call to one of the seams straight after a load, taking the loaded value and giving back the one to use.</summary>
        private static void After(ILCursor c, Instruction load, string seam)
        {
            c.Goto(load, MoveType.After);
            c.Emit(OpCodes.Call, typeof(ShopBox).GetMethod(seam));
        }
    }
}
