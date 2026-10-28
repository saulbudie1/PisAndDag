using PisAndDag.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// The off hand: one more item, kept in its own slot by the trash (OffHandSlot), opened by the Guide's
    /// quest (GuideQuest.Done, the world's), and used with its own key (PADKeybinds.OffHand, the bind that
    /// was the wind's test key, so whatever button was set for that now uses the off hand). Its primary
    /// use only: never its right-click (by request: "cannot use alt function ... for offhand items").
    ///
    /// How it is used: while the key is held and the player is free to use an item, the off hand's item
    /// is swapped into the selected hotbar slot and used as the held item is (so every weapon and tool
    /// works as it always does, and the swap is what the other players see); when it is let go and the
    /// use is over, it is swapped back. The swap is always undone: when the key is let go, when the hotbar
    /// moves, before saving, and before dying, so nothing is ever lost or doubled between the two.
    ///
    /// Which items: weapons, tools and fishing rods (<see cref="Fits"/>).
    /// </summary>
    public class OffHandPlayer : ModPlayer
    {
        /// <summary>The off hand's item. Saved with the player, kept in every world; usable where the world has it open.</summary>
        internal Item Item = new Item();

        /// <summary>The hotbar slot the off hand's item is in while it is being used, or -1.</summary>
        private int swappedInto = -1;

        /// <summary>Its item is in the hotbar, being used.</summary>
        internal bool Busy => swappedInto >= 0;

        /// <summary>The key, held this tick (the local player only).</summary>
        private bool held;

        /// <summary>Whether this world has the off hand open.</summary>
        internal static bool Open => GuideQuest.Done;

        /// <summary>What may go in the off hand: something used, that hurts (a catalyst too), digs, chops, hammers or fishes; never a block, a potion or an accessory.</summary>
        internal static bool Fits(Item item)
        {
            if (item == null || item.IsAir)
                return true;

            if (item.useStyle == ItemUseStyleID.None || item.accessory || item.createTile >= 0 || item.createWall >= 0 || item.PAD().Lore)
                return false;

            // A weapon by its damage, or by its kind of damage: a catalyst has none of its own till a cast is up
            // (Items.Mage.Catalyst), so its damage is 0 wherever it is not held.
            bool weapon = item.damage > 0 || item.DamageType != DamageClass.Default || item.ModItem is Items.Mage.Catalyst;

            return weapon || item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.fishingPole > 0;
        }

        public override void Initialize()
        {
            Item = new Item();
            swappedInto = -1;
        }

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet)
        {
            held = PADKeybinds.OffHand != null && PADKeybinds.OffHand.Current;
        }

        /// <summary>Whether the off hand can be taken up now.</summary>
        private bool Free() =>
            Open && !Item.IsAir && Fits(Item) && !Player.dead && !Player.CCed && !Player.noItems && !Player.pulley && Player.selectedItem < 10
            && Main.mouseItem.IsAir && Player.itemAnimation == 0 && !Player.channel;

        public override void SetControls()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (swappedInto >= 0)
            {
                // Its primary use only, and only while the key is held (the mouse does not carry it on).
                Player.controlUseTile = false;
                Player.controlUseItem = held;
                return;
            }

            if (!held || !Free())
                return;

            swappedInto = Player.selectedItem;
            Swap();
            Player.altFunctionUse = 0;
            Player.controlUseTile = false;
            Player.controlUseItem = true;
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer || swappedInto < 0)
                return;

            // Put back once the use is over and the key is let go; at once if the hotbar moved.
            bool moved = Player.selectedItem != swappedInto;

            if (moved || !held && Player.itemAnimation == 0 && !Player.channel || !Open || Player.dead)
                PutBack();
        }

        /// <summary>The off hand's item and the hotbar slot's, exchanged.</summary>
        private void Swap()
        {
            (Player.inventory[swappedInto], Item) = (Item, Player.inventory[swappedInto]);
        }

        /// <summary>The swap undone, if there is one.</summary>
        internal void PutBack()
        {
            if (swappedInto < 0)
                return;

            Swap();
            swappedInto = -1;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref Terraria.DataStructures.PlayerDeathReason damageSource)
        {
            PutBack();
            return true;
        }

        public override void SaveData(TagCompound tag)
        {
            PutBack();

            if (!Item.IsAir)
                tag["OffHand"] = ItemIO.Save(Item);
        }

        public override void LoadData(TagCompound tag)
        {
            Item = tag.ContainsKey("OffHand") ? ItemIO.Load(tag.GetCompound("OffHand")) : new Item();
        }
    }
}
