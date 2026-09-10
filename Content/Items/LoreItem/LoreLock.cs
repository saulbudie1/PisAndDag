using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.LoreItem
{
    /// <summary>
    /// Keeps lore items on the player.
    ///
    /// Two halves do the work. PADGlobalItem forces the favourite star on, which is what
    /// vanilla already checks before quick-stacking, before the drop key, and before a
    /// shift-click into trash or a shop — four doors shut for one line. This class shuts
    /// the two that favouriting leaves open: dragging the item into the trash slot by
    /// hand, and dragging it into a container by hand.
    ///
    /// It works by undoing rather than by refusing. tModLoader has no "this item may not
    /// enter this slot" hook, and the alternative is detouring ItemSlot's click handling —
    /// several overloads deep in the UI, all of which other mods also want to detour. So
    /// the item is allowed to land and is picked back up the same tick, before a frame is
    /// ever drawn with it there. From the player's side it simply will not stay put.
    ///
    /// Only the open container is scanned, and only while one is open, so the cost is
    /// forty slots on the frames where a chest is on screen and nothing at all otherwise.
    /// Sweeping every chest in the world would be the thorough version and would also be
    /// eight thousand chests a tick.
    /// </summary>
    public class LoreLock : ModPlayer
    {
        private const string Refusal = "You cannot dispose of Lore Items.";

        private const string Duplicate = "No duplications.";

        /// <summary>
        /// Slots swept for duplicates: the main grid, the coin row and the ammo row.
        /// Index 58 is skipped deliberately — vanilla uses it as scratch for the item on
        /// the cursor, and a copy passing through it is mid-drag, not a second copy.
        /// </summary>
        private const int SweptSlots = 58;

        private static readonly Color RefusalColour = new Color(206, 176, 122);

        /// <summary>Ticks before the line can be printed again, so a drag does not spam it.</summary>
        private const int ScoldRate = 60;

        private int scoldCooldown;

        public override void PostUpdate()
        {
            // Containers and the trash slot are the local player's UI. A server has no
            // trash slot, and another player's chest is their business.
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (scoldCooldown > 0)
                scoldCooldown--;

            RescueTrash();
            RescueContainer();

            // Runs after the rescues, not before. A rescue can put a lore item back into
            // an inventory that already holds one, and the purge is what settles that —
            // the other order would leave the duplicate sitting there until next tick.
            PurgeDuplicates();
        }

        /// <summary>
        /// Keeps one of each lore item and destroys the rest.
        ///
        /// The copy in the lowest slot survives, which is the one the player has had
        /// longest in every ordinary case: a new pickup lands in the first free slot after
        /// it, and a rescued item is placed after it too.
        ///
        /// The outer loop bails on the first check for almost every slot, so the shape
        /// being quadratic costs nothing — it only walks pairs once a lore item is
        /// actually found, and there are never many.
        /// </summary>
        private void PurgeDuplicates()
        {
            for (int i = 0; i < SweptSlots; i++)
            {
                Item keep = Player.inventory[i];

                if (!IsLore(keep))
                    continue;

                for (int j = i + 1; j < SweptSlots; j++)
                {
                    Item other = Player.inventory[j];

                    if (!IsLore(other) || other.type != keep.type)
                        continue;

                    other.TurnToAir();
                    Scold(Duplicate);
                }
            }
        }

        private void RescueTrash()
        {
            // The trash slot belongs to the Player, not to Main — it is saved with the
            // character, so every player carries their own.
            if (!IsLore(Player.trashItem))
                return;

            Item held = Player.trashItem;
            Player.trashItem = new Item();

            Return(held);
        }

        private void RescueContainer()
        {
            Item[] slots = OpenContainer(out int chestIndex);

            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!IsLore(slots[i]))
                    continue;

                Item held = slots[i];
                slots[i] = new Item();

                // A real chest lives in the world, not on this client, so the server has
                // to be told the slot emptied. The banks are carried by the player and
                // sync with them, which is why only this branch sends anything.
                if (chestIndex >= 0 && Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.SyncChestItem, number: chestIndex, number2: i);

                Return(held);
            }
        }

        /// <summary>
        /// The container the player currently has open, or null. Negative chest values are
        /// vanilla's codes for the containers the player carries: piggy bank, safe,
        /// defender's forge, void vault.
        /// </summary>
        private Item[] OpenContainer(out int chestIndex)
        {
            chestIndex = -1;

            switch (Player.chest)
            {
                case -1:
                    return null;

                case -2:
                    return Player.bank.item;

                case -3:
                    return Player.bank2.item;

                case -4:
                    return Player.bank3.item;

                case -5:
                    return Player.bank4.item;

                default:
                    if (Player.chest < 0 || Player.chest >= Main.chest.Length || Main.chest[Player.chest] == null)
                        return null;

                    chestIndex = Player.chest;
                    return Main.chest[chestIndex].item;
            }
        }

        /// <summary>
        /// Puts an item back in the player's hands, and on the floor at their feet only if
        /// there is genuinely nowhere left to put it — which is better than the item
        /// evaporating because the inventory happened to be full at the wrong moment.
        /// </summary>
        private void Return(Item held)
        {
            Item leftover = Player.GetItem(Player.whoAmI, held, GetItemSettings.LootAllSettings);

            if (!leftover.IsAir)
                Player.QuickSpawnClonedItem(Player.GetSource_Misc("PADLoreLock"), leftover, leftover.stack);

            Scold(Refusal);
        }

        private void Scold(string line)
        {
            if (scoldCooldown > 0 || Main.dedServ)
                return;

            scoldCooldown = ScoldRate;
            Main.NewText(line, RefusalColour);
        }

        private static bool IsLore(Item item)
        {
            return item != null && !item.IsAir && item.PAD().Lore;
        }
    }
}
