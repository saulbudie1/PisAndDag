using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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
    ///
    /// It also owns the codex shelf, and enforces that a lore item exists in exactly one
    /// place: the pack or the shelf, never both.
    /// </summary>
    public class LoreLock : ModPlayer
    {
        private const string Refusal = "It refuses to leave your side.";

        private const string Duplicate = "There can only be one.";

        /// <summary>
        /// Slots swept for duplicates: the main grid, the coin row and the ammo row.
        /// Index 58 is skipped deliberately — vanilla uses it as scratch for the item on
        /// the cursor, and a copy passing through it is mid-drag, not a second copy.
        /// </summary>
        private const int SweptSlots = 58;

        private const string CodexTag = "PADCodex";

        /// <summary>
        /// The shelf behind the Lore Codex window.
        ///
        /// Lives on the player rather than in the world, so it travels with the character
        /// between worlds — the point of a lore item is that this particular player found
        /// it. Unbounded on purpose: the window scrolls, and there is no reason to invent
        /// a ceiling the fiction does not have.
        /// </summary>
        public readonly List<Item> Codex = new List<Item>();

        public override void SaveData(TagCompound tag)
        {
            if (Codex.Count == 0)
                return;

            // A lambda rather than the ItemIO.Save method group: Save has an optional
            // second parameter, and a method group with one of those will not convert to
            // a one-argument delegate.
            tag[CodexTag] = Codex.Select(item => ItemIO.Save(item)).ToList();
        }

        public override void LoadData(TagCompound tag)
        {
            Codex.Clear();

            if (!tag.ContainsKey(CodexTag))
                return;

            foreach (TagCompound entry in tag.GetList<TagCompound>(CodexTag))
            {
                Item item = ItemIO.Load(entry);

                // A lore item whose mod is gone loads as air. Keeping it would leave a
                // hole in the grid that cannot be clicked out of.
                if (item.IsAir)
                    continue;

                // The shelf refuses a second copy when one is placed by hand, but a save
                // written before the inventory and the shelf shared a uniqueness check
                // may hold one anyway. Dropping it here means the fix repairs old
                // characters instead of only preventing new breakage.
                if (CodexHolds(item.type))
                    continue;

                Codex.Add(item);
            }
        }

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

        /// <summary>True if the codex shelf already holds this item type.</summary>
        public bool CodexHolds(int type)
        {
            for (int i = 0; i < Codex.Count; i++)
            {
                if (Codex[i].type == type)
                    return true;
            }

            return false;
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
            // The shelf counts. Uniqueness used to be checked inside the inventory and
            // inside the codex separately, which meant neither ever saw the other and one
            // copy on the shelf plus one in the pack passed both tests. "There can only be
            // one" has to mean one across everything the player owns, or it means nothing.
            //
            // The shelved copy is the survivor: putting an item in the codex is a
            // deliberate act, and a loose copy turning up afterwards is the newcomer.
            // Anything the player pulls back OFF the shelf leaves the codex as it goes, so
            // withdrawing never trips this.
            if (Codex.Count > 0)
            {
                for (int i = 0; i < SweptSlots; i++)
                {
                    Item loose = Player.inventory[i];

                    if (!IsLore(loose) || !CodexHolds(loose.type))
                        continue;

                    loose.TurnToAir();
                    Scold(Duplicate);
                }
            }

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
