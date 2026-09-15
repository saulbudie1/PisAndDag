using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Guide and his doll. Offered while the player is carrying a Guide Voodoo Doll;
    /// one line in, two lines to choose from, and either one ends the branch.
    ///
    /// The two answers are written not to overlap (Lore Bible §XXVI–XXVII, §V): "Your
    /// past" is the lineage — the learned houses that laid the Seal, and the key they
    /// wrote into their blood — with no demons in it; "The Voodoo Demon" is the demons and
    /// the balance, with no lineage in it. Neither says the whole thing; the ruins can.
    /// </summary>
    public class GuideTalk : ModSystem
    {
        private const string Intro =
            "Is that me? Where did you get that? I haven't seen this since my childhood.";

        private const string YourPast =
            "My family kept books older than this town. They said our line once sat among the " +
            "learned houses that laid the Seal beneath the world, and that those houses wrote " +
            "the key into their own blood, so it could be inherited but never stolen. I took it " +
            "for a story told to make a small boy feel important. I am less sure now.";

        private const string TheDemon =
            "Those things carry them for a reason. They are probably trying to break the " +
            "world's balance. It's a good thing the sacrificial effigy is in your hand and not " +
            "theirs, or the world is going to go catastrophic. Keep it away from the fire.";

        public override void PostSetupContent()
        {
            TalkOptions.Register(NPCID.Guide, new TalkOption(
                TalkOptions.Icon(ItemID.GuideVoodooDoll) + "The Voodoo",
                npc => TalkOptions.Say(Intro,
                    new TalkOption("Your past", _ => TalkOptions.Say(YourPast)),
                    new TalkOption("The Voodoo Demon", _ => TalkOptions.Say(TheDemon))),
                (player, npc) => player.HasItem(ItemID.GuideVoodooDoll)));
        }
    }

    /// <summary>
    /// The doll's tooltip points at him without saying so. Two lines under vanilla's own,
    /// in the mod's lore voice: a fact about the thing, then the one that sends the player
    /// to the Guide — "since he was small" is the same memory he opens with.
    /// </summary>
    public class GuideVoodooDollTooltip : GlobalItem
    {
        /// <summary>PADGlobalItem's LoreColour: quieter than a label, meant to be read.</summary>
        private static readonly Color LoreColour = new Color(196, 180, 152);

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.GuideVoodooDoll;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "PADVoodoo0", "Stitched by hands that knew the face.")
            {
                OverrideColor = LoreColour
            });

            tooltips.Add(new TooltipLine(Mod, "PADVoodoo1", "The one it resembles has not seen it since he was small.")
            {
                OverrideColor = LoreColour
            });
        }
    }
}
