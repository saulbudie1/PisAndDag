using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Everything Made By Tentochi (by request: "add ... a new accessory group: Made By Tentochi #d62747"): what it does,
    /// plainly; after that, a line of the maker's own description in a cool grey, as a product sheet has it ("Do not put
    /// some cheesy line in the tooltip anymore; for this class of Made By Tentochi; it is a serious corporate like
    /// description"), each item's Spec in the localization file; and at the very foot, under the price, "Made By Tentochi"
    /// in their red, a tag as the Lore Item one is (PADGlobalItem), by request: "made by tentochi should be a tag like Lore
    /// Item; at the bottom". Lines that name a key (<see cref="KeyLine"/>) are made here, with the key as it is bound now.
    /// </summary>
    public abstract class TentochiItem : ModItem
    {
        /// <summary>The grey of a maker's description.</summary>
        public static readonly Color SpecColour = new Color(158, 166, 182);

        public override string LocalizationCategory => "Items.Accessories";

        /// <summary>The maker's own description, last in the tooltip.</summary>
        public LocalizedText Spec => this.GetLocalization(nameof(Spec));

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            _ = Spec;
        }

        /// <summary>A line naming a key, made as the tooltip is (null for none).</summary>
        protected virtual string KeyLine() => null;

        /// <summary>Which of the item's own tooltip lines the key's line goes after (past the last, after the last).</summary>
        protected virtual int KeyLineAfter => int.MaxValue;

        /// <summary>The keys bound to a keybind, as the game names them, or "an unbound key".</summary>
        public static string KeyName(ModKeybind keybind)
        {
            List<string> keys = keybind?.GetAssignedKeys();

            if (keys == null || keys.Count == 0)
                return Language.GetTextValue("Mods.PisAndDag.Tentochi.Unbound");

            return string.Join(" / ", keys);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            int last = tooltips.FindLastIndex(line => line.Mod == "Terraria" && line.Name.StartsWith("Tooltip"));

            if (last < 0)
                last = tooltips.FindLastIndex(line => line.Mod == "Terraria" && (line.Name == "Equipable" || line.Name == "Defense"));

            if (last < 0)
                last = System.Math.Max(0, tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "ItemName"));

            string key = KeyLine();

            if (key != null)
            {
                int after = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "Tooltip" + KeyLineAfter);
                tooltips.Insert(after >= 0 ? after + 1 : last + 1, new TooltipLine(Mod, "TentochiKey", key));
                last++;
            }

            tooltips.Insert(last + 1, new TooltipLine(Mod, "TentochiSpec", Spec.Value) { OverrideColor = SpecColour });

            // Added last, so it sits at the foot of the tooltip, under the price and anything else, as the Lore Item tag does.
            tooltips.Add(new TooltipLine(Mod, "MadeByTentochi", Language.GetTextValue("Mods.PisAndDag.Tentochi.Brand"))
            {
                OverrideColor = TentochiFX.Red,
            });
        }
    }
}
