using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Mage;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// The Magic Arsenal: what a caster carries in their head, kept as a row of slots
    /// beside the inventory, the way the Lore Codex is kept.
    ///
    /// A spell or an incantation is an item. Put in a slot, it is memorised; taken out,
    /// it is forgotten and back in the hand. One pool for both kinds, three slots to
    /// begin with and ten at most; what opens the rest is not built yet. No two of the
    /// same cast, ever. And the slots only take or give near a lived-in house: within
    /// <see cref="HomeReach"/> of the home tile of any town NPC that has one.
    ///
    /// Two keybinds move the selection, and only through the casts of the kind the held
    /// catalyst can fire. A selection is remembered per kind, so putting the seal down
    /// and picking it up again returns to the incantation that was up.
    ///
    /// Local to the player that owns it. The catalyst reads the selection to set its
    /// own numbers, and what leaves the hand is a projectile, which syncs itself.
    ///
    /// For testing, /magicarsenal max opens all ten whatever has been earned
    /// (<see cref="Maxed"/>), and /magicarsenal normal shuts them back to what has; see
    /// MagicArsenalCommand. <see cref="Unlocked"/> is never touched by it, so going back
    /// is exact.
    /// </summary>
    public class MagicArsenal : ModPlayer
    {
        public const int StartSlots = 3;
        public const int MaxSlots = 10;

        /// <summary>How far from a town NPC's home tile the arsenal can be changed, in pixels. Fifty tiles: about a screen.</summary>
        public const float HomeReach = 800f;

        /// <summary>How many of the ten have been earned. Saved.</summary>
        public int Unlocked = StartSlots;

        /// <summary>All ten open for testing, whatever has been earned. Saved, so it lasts until put back.</summary>
        public bool Maxed;

        /// <summary>How many of the ten can be used right now.</summary>
        public int OpenSlots => Maxed ? MaxSlots : Unlocked;

        /// <summary>The slots, always <see cref="MaxSlots"/> long, air where empty. Saved.</summary>
        public readonly Item[] Slots = new Item[MaxSlots];

        /// <summary>The selected item type for each kind, indexed by CastKind. 0 is nothing.</summary>
        private readonly int[] selected = new int[2];

        private uint lastSaid;

        public override void Initialize()
        {
            for (int i = 0; i < Slots.Length; i++)
                Slots[i] = new Item();
        }

        public bool Holds(int type)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsAir && Slots[i].type == type)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The cast in a slot, or null if it is empty or not a cast. Always the mod's one
        /// instance of that cast, not the item's own copy, so two lookups of the same
        /// cast are the same object and can be compared as such.
        /// </summary>
        public Castable CastAt(int slot) => Slots[slot].IsAir ? null : ModContent.GetModItem(Slots[slot].type) as Castable;

        /// <summary>Whether the player stands near a house someone lives in.</summary>
        public bool NearHome()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.townNPC || npc.homeless || npc.homeTileX < 0 || npc.homeTileY < 0)
                    continue;

                Vector2 home = new Vector2(npc.homeTileX * 16f + 8f, npc.homeTileY * 16f);

                if (Vector2.DistanceSquared(Player.Center, home) <= HomeReach * HomeReach)
                    return true;
            }

            return false;
        }

        /// <summary>Says a thing in the arsenal's colour, no more than once a half second.</summary>
        public void Say(string key)
        {
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount - lastSaid < 30)
                return;

            lastSaid = Main.GameUpdateCount;
            Main.NewText(Language.GetTextValue("Mods.PisAndDag.Magic." + key), 214, 127, 45);
        }

        /// <summary>The casts of one kind, in slot order.</summary>
        public List<Castable> OfKind(CastKind kind)
        {
            List<Castable> list = new();

            for (int i = 0; i < OpenSlots && i < Slots.Length; i++)
            {
                if (CastAt(i) is Castable cast && cast.Kind == kind)
                    list.Add(cast);
            }

            return list;
        }

        /// <summary>
        /// The cast that is up for this kind. If what was selected has been taken out,
        /// it falls to the first of the kind still in; if none, null.
        /// </summary>
        public Castable Selected(CastKind kind)
        {
            int type = selected[(int)kind];

            if (type > 0 && Holds(type) && ModContent.GetModItem(type) is Castable still && still.Kind == kind)
                return still;

            List<Castable> list = OfKind(kind);
            Castable first = list.Count > 0 ? list[0] : null;

            selected[(int)kind] = first?.Type ?? 0;
            return first;
        }

        /// <summary>Makes this cast the one that is up for its kind.</summary>
        public void Select(Castable cast)
        {
            if (cast == null || !Holds(cast.Type))
                return;

            selected[(int)cast.Kind] = cast.Type;
        }

        /// <summary>Moves the selection for this kind by one, wrapping. Sees nothing of the other kind.</summary>
        public void Cycle(CastKind kind, int direction)
        {
            List<Castable> list = OfKind(kind);

            if (list.Count == 0)
                return;

            Castable current = Selected(kind);
            int at = current == null ? -1 : list.FindIndex(cast => cast.Type == current.Type);

            at = ((at + direction) % list.Count + list.Count) % list.Count;
            selected[(int)kind] = list[at].Type;

            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Player.HeldItem?.ModItem is not Catalyst catalyst)
                return;

            if (PADKeybinds.SpellUp != null && PADKeybinds.SpellUp.JustPressed)
                Cycle(catalyst.Kind, +1);

            if (PADKeybinds.SpellDown != null && PADKeybinds.SpellDown.JustPressed)
                Cycle(catalyst.Kind, -1);
        }

        /// <summary>Opens all ten. Nothing earned changes.</summary>
        public void Max() => Maxed = true;

        /// <summary>
        /// Back to the slots earned. Nothing is lost with the ones that shut: a cast in one
        /// moves to the first empty slot still open, and if there is none it is given back to
        /// the player, at their feet, the way a bag's loot is.
        /// </summary>
        /// <param name="moved">Casts moved into open slots.</param>
        /// <param name="returned">Casts given back to the player.</param>
        public void Normal(out int moved, out int returned)
        {
            moved = 0;
            returned = 0;

            if (!Maxed)
                return;

            Maxed = false;

            for (int i = Unlocked; i < Slots.Length; i++)
            {
                if (Slots[i].IsAir)
                    continue;

                Item cast = Slots[i];
                Slots[i] = new Item();

                int free = -1;

                for (int j = 0; j < Unlocked && j < Slots.Length; j++)
                {
                    if (Slots[j].IsAir)
                    {
                        free = j;
                        break;
                    }
                }

                if (free >= 0)
                {
                    Slots[free] = cast;
                    moved++;
                }
                else
                {
                    Player.QuickSpawnItem(Player.GetSource_Misc("MagicArsenal"), cast, cast.stack);
                    returned++;
                }
            }
        }

        public override void SaveData(TagCompound tag)
        {
            tag["unlocked"] = Unlocked;

            if (Maxed)
                tag["maxed"] = true;

            // Every slot, air included, so a cast keeps its place across a save.
            tag["arsenal"] = Slots.Select(item => ItemIO.Save(item)).ToList();

            tag["selectedSpell"] = ModContent.GetModItem(selected[0])?.FullName ?? "";
            tag["selectedIncantation"] = ModContent.GetModItem(selected[1])?.FullName ?? "";
        }

        public override void LoadData(TagCompound tag)
        {
            Unlocked = tag.ContainsKey("unlocked") ? System.Math.Clamp(tag.GetInt("unlocked"), 1, MaxSlots) : StartSlots;
            Maxed = tag.ContainsKey("maxed") && tag.GetBool("maxed");

            for (int i = 0; i < Slots.Length; i++)
                Slots[i] = new Item();

            if (tag.ContainsKey("arsenal"))
            {
                int at = 0;

                foreach (TagCompound entry in tag.GetList<TagCompound>("arsenal"))
                {
                    if (at >= Slots.Length)
                        break;

                    Item item = ItemIO.Load(entry);

                    // A cast from a version of the mod that no longer has it loads as air
                    // and stays a hole, so nothing else shifts. A second copy, or a thing
                    // that is not a cast, is dropped.
                    if (!item.IsAir && (item.ModItem is not Castable || Holds(item.type)))
                        item = new Item();

                    Slots[at++] = item;
                }
            }

            selected[0] = ModContent.TryFind(tag.GetString("selectedSpell"), out ModItem spell) ? spell.Type : 0;
            selected[1] = ModContent.TryFind(tag.GetString("selectedIncantation"), out ModItem inc) ? inc.Type : 0;
        }
    }
}
