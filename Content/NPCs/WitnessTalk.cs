using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Truffle and the blade. Offered while the player carries the Blade of
    /// Mors; the fungal network remembers what passed over it (Lore Bible
    /// §XXVIII), so Truffle remembers the one who carried it before.
    ///
    /// The two answers do not overlap: "Where they go" is the caves' own
    /// answer — death feeds the roots, nothing here is thrown away — with no
    /// visitor in it; "The one who asked" is the visitor — tall, wrong, back
    /// again with the same question — with no answer in it. The Blade's own
    /// verses say none remember his name; Truffle's say he did not either.
    /// Neither says who he is.
    /// </summary>
    public class TruffleTalk : ModSystem
    {
        private const string Intro =
            "That's been down here before. Not with you. The mushrooms remember what passes " +
            "over them, and they remember that. Somebody carried it past my door and stopped " +
            "to ask me where the dead go.";

        private const string WhereTheyGo =
            "Down, I told him. Into the soil, and the soil into the roots, and the roots into " +
            "us, and up again as something new. Nothing here is thrown away. The caves need " +
            "the dead the way a field needs rain. He said that sounded kind. Nobody else has " +
            "ever said that to me.";

        private const string TheOneWhoAsked =
            "Tall. Too tall, and his hands were wrong, though I couldn't say how. He thanked " +
            "me, walked on, and came back an hour later to ask the same thing. Then again. " +
            "He didn't remember asking. The mushrooms knew him. I don't think he did.";

        public override void PostSetupContent()
        {
            int blade = ModContent.ItemType<BladeOfMors>();

            TalkOptions.Register(NPCID.Truffle, new TalkOption(
                TalkOptions.Icon(blade) + "About this blade",
                npc => TalkOptions.Say(Intro,
                    new TalkOption("Where they go", _ => TalkOptions.Say(WhereTheyGo)),
                    new TalkOption("The one who asked", _ => TalkOptions.Say(TheOneWhoAsked))),
                (player, npc) => player.HasItem(blade)));
        }
    }


    /// <summary>
    /// The Dryad after Plantera. One line, no branch (Lore Bible §VI): when
    /// the thing in the jungle died she felt it in herself, and she has never
    /// asked what she carries. Life, vessels and the word for what Plantera
    /// was are all left out; the ruins can say those.
    /// </summary>
    public class DryadTalk : ModSystem
    {
        private const string Plantera =
            "When it died I felt it, all the way out here. Not the ground shaking. Something " +
            "in me sat up and listened, the way you turn when someone says your name in a " +
            "crowd. I have kept the forests since before this town had a name, and I have " +
            "never asked what keeps me. I'm not sure I want the answer.";

        public override void PostSetupContent()
        {
            TalkOptions.Register(NPCID.Dryad, new TalkOption(
                "Plantera",
                npc => TalkOptions.Say(Plantera),
                (player, npc) => NPC.downedPlantBoss));
        }
    }
}
