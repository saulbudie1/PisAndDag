using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// The two places the call is hinted at, neither of which names it.
    ///
    /// The Clothier, one line in three, until the thing is dead: where, what to bring,
    /// and how many, in the manner of a man who wants you to go and would rather not
    /// say why. And the three animals themselves, one line under their tooltips, which
    /// reads as a joke about pets until it does not.
    /// </summary>
    public class AbomanationHints : GlobalNPC
    {
        /// <summary>One line in this many, while he has something to hint at.</summary>
        private const int Chance = 3;

        public override void GetChat(NPC npc, ref string chat)
        {
            if (npc.type != NPCID.Clothier || PADWorld.DownedAbomanation)
                return;

            if (Main.rand.NextBool(Chance))
                chat = Language.GetTextValue("Mods.PisAndDag.Hints.ClothierMile");
        }
    }

    /// <summary>The line under a bunny, a squirrel and a red squirrel.</summary>
    public class OfferingTooltip : GlobalItem
    {
        /// <summary>PADGlobalItem's LoreColour: quieter than a label, meant to be read.</summary>
        private static readonly Color LoreColour = new Color(196, 180, 152);

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.Bunny || entity.type == ItemID.Squirrel || entity.type == ItemID.SquirrelRed;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "PADOffering", Language.GetTextValue("Mods.PisAndDag.Hints.Offering"))
            {
                OverrideColor = LoreColour
            });
        }
    }
}
