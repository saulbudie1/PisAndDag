using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items
{
    /// <summary>
    /// The Rod of Discord and the Rod of Harmony do nothing in this mod's worlds. The same
    /// ground rule as the wings (PSItem): you get where you are going on your feet, and no
    /// wall is a way through.
    /// </summary>
    public class GroundedRods : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
            entity.type == ItemID.RodofDiscord || entity.type == ItemID.RodOfHarmony;

        public override bool CanUseItem(Item item, Player player) => false;

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "Grounded", "'However, you stay where you stand'"));
        }
    }
}
