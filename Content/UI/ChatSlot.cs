using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The slot frame for the slots that live in the town chat panel: the shop's (ShopBox) and
    /// the reforge's (ReforgeBox). Nothing else: the player's inventory, chests and every other
    /// slot keep vanilla's.
    ///
    /// ItemSlot.Draw picks the slot's back by its context, from TextureAssets, and draws it at
    /// Main.inventoryScale before the item:
    ///
    ///   context 15 (ShopItem)                       InventoryBack6
    ///   context 5 (PrefixItem, the reforge slot)    InventoryBack4
    ///
    /// For the length of one call to it, from one of those two places, that asset is this one
    /// instead, and then it is put back. So everything else ItemSlot.Draw does, and every state
    /// that picks a back of its own (a favourite, a new item, the gamepad's highlight), is as
    /// vanilla has it.
    ///
    /// ChatSlot.png beside this file is 52×52, the size of vanilla's Inventory_Back, because
    /// ItemSlot centres the item by the back's size.
    /// </summary>
    public static class ChatSlot
    {
        private static Asset<Texture2D> texture;

        internal static void Clear()
        {
            texture = null;
        }

        private static Asset<Texture2D> Texture =>
            texture ??= ModContent.Request<Texture2D>("PisAndDag/Content/UI/ChatSlot", AssetRequestMode.ImmediateLoad);

        /// <summary>The asset ItemSlot.Draw draws this context's plain slot from: the shop's, or the reforge's.</summary>
        private static ref Asset<Texture2D> Back(int context)
        {
            if (context == ItemSlot.Context.PrefixItem)
                return ref TextureAssets.InventoryBack4;

            return ref TextureAssets.InventoryBack6;
        }

        /// <summary>ItemSlot.Draw, in this frame.</summary>
        public static void Draw(SpriteBatch spriteBatch, Item[] inv, int context, int slot, Vector2 position, Color lightColor)
        {
            ref Asset<Texture2D> back = ref Back(context);
            Asset<Texture2D> vanilla = back;
            back = Texture;

            try
            {
                ItemSlot.Draw(spriteBatch, inv, context, slot, position, lightColor);
            }
            finally
            {
                back = vanilla;
            }
        }

        /// <summary>ItemSlot.Draw for one item, in this frame.</summary>
        public static void Draw(SpriteBatch spriteBatch, ref Item inv, int context, Vector2 position, Color lightColor)
        {
            ref Asset<Texture2D> back = ref Back(context);
            Asset<Texture2D> vanilla = back;
            back = Texture;

            try
            {
                ItemSlot.Draw(spriteBatch, ref inv, context, position, lightColor);
            }
            finally
            {
                back = vanilla;
            }
        }
    }
}
