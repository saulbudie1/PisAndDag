using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace PisAndDag.Content.Items
{
    /// <summary>
    /// Per-item mod data, reached with item.PAD().
    ///
    /// Instanced, so a field here belongs to one item stack rather than to the type — two
    /// copies of the same weapon can hold different values.
    ///
    /// Separate from PSItem, which stays as it is. That one is the mod's item HOOKS; this
    /// one is item STATE, and mixing the two means every hook override has to be aware of
    /// per-instance data it does not use. Anything that must survive a save needs Save /
    /// LoadData added here, and Clone if it should follow the item when a stack splits.
    /// </summary>
    public class PADGlobalItem : GlobalItem
    {
        public override bool InstancePerEntity => true;

        /// <summary>
        /// Marks an item the player is not allowed to lose.
        ///
        ///     public override void SetDefaults()
        ///     {
        ///         Item.PAD().Lore = true;
        ///         Item.PAD().LoreText = "\"It does not cause death. It completes it\"";
        ///         ...
        ///     }
        ///
        /// Set it in SetDefaults and nothing else is needed — no SaveData, no NetSend.
        /// Every item in the game is built through SetDefaults, including one loaded from
        /// a world file or arriving over the network, so the flag is rebuilt from scratch
        /// every time the item exists anywhere. Persisting it would only create a second
        /// source of truth that could disagree with the first.
        ///
        /// Enforcement lives in LoreLock. This is just the label.
        /// </summary>
        public bool Lore;

        /// <summary>
        /// What the item says about itself when the player holds shift over it. Newlines
        /// split into separate tooltip lines. Leave it null and shift does nothing —
        /// an item can be unlosable without having anything to say yet.
        /// </summary>
        public string LoreText;

        /// <summary>The marker at the foot of the tooltip.</summary>
        private static readonly Color MarkerColour = new Color(237, 130, 24);

        /// <summary>
        /// The lore itself. Deliberately quieter than the marker — the marker is a label
        /// and wants to be found, the lore is a voice and wants to be read.
        /// </summary>
        private static readonly Color LoreColour = new Color(196, 180, 152);

        /// <summary>
        /// The nudge that tells the player shift does something. Grey because it is the
        /// only line on the tooltip that is instruction rather than content, and it should
        /// stop being noticed once they know.
        /// </summary>
        private static readonly Color HintColour = new Color(150, 150, 150);

        /// <summary>
        /// Rides on vanilla's own protection rather than reimplementing it.
        ///
        /// A favourited item is already refused by quick stack, by the drop key, by
        /// shift-click to trash and by shift-click to a shop — four separate paths that
        /// vanilla guards for free. Forcing the flag every tick also means the player
        /// cannot un-favourite it: the star goes straight back on.
        /// </summary>
        public override void UpdateInventory(Item item, Player player)
        {
            if (Lore)
                item.favorited = true;
        }

        /// <summary>
        /// No modifiers, ever.
        ///
        /// One hook closes every door because of what `pre` means: -1 is the roll an item
        /// gets when it is crafted, bought, dropped or found in a chest, -2 is a reforge
        /// at the Goblin Tinkerer, and -3 is the question the tinkerer's UI asks before it
        /// will even accept the item into its slot. Refusing all three means a lore item
        /// cannot be rolled, cannot be reforged, and cannot be placed in the reforge slot
        /// to begin with — which is better than letting it in and rejecting it after.
        ///
        /// A lore item is a specific object with a history. Reforged Legendary is not.
        /// </summary>
        public override bool? PrefixChance(Item item, int pre, UnifiedRandom rand)
        {
            return Lore ? false : null;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (!Lore)
                return;

            // Read straight off the keyboard rather than through a helper. Whether shift
            // is down is not state anything else needs, and the raw check cannot be wrong
            // about which frame it is answering for.
            bool shift = Main.keyState.IsKeyDown(Keys.LeftShift)
                || Main.keyState.IsKeyDown(Keys.RightShift);

            bool hasLore = !string.IsNullOrWhiteSpace(LoreText);

            if (shift && hasLore)
            {
                // Everything but the name goes: stats, rarity, price, the favourite hints,
                // and any line another mod added. The point of the shift view is that the
                // item stops describing what it does and starts saying what it is.
                tooltips.RemoveAll(line => line.Name != "ItemName");

                string[] verses = LoreText.Split('\n');

                for (int i = 0; i < verses.Length; i++)
                {
                    tooltips.Add(new TooltipLine(Mod, "PADLore" + i, verses[i].Trim())
                    {
                        OverrideColor = LoreColour
                    });
                }
            }

            // Only while shift is up, and only on an item that actually has something to
            // say — offering the hint on lore-less items would teach the player that shift
            // does nothing, which is the opposite of what a hint is for.
            if (!shift && hasLore)
            {
                tooltips.Add(new TooltipLine(Mod, "PADLoreHint", "Hold Shift for lore")
                {
                    OverrideColor = HintColour
                });
            }

            // Added last either way, so it sits at the foot of the tooltip — under the
            // stats and the hint normally, and under the lore while shift is held.
            tooltips.Add(new TooltipLine(Mod, "PADLoreMarker", "Lore Item")
            {
                OverrideColor = MarkerColour
            });
        }
    }
}
