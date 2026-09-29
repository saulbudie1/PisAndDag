using PisAndDag.Content.Buffs;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Pugream
{
    /// <summary>
    /// What Pugream says on the shore (Lore Bible §XXXI; Lore Planning §3).
    ///
    /// DRAFT LINES, for discussion. The wiring is final; the words are not.
    ///
    /// He introduces himself every time, because he assumes he has been forgotten, and
    /// he is usually right. He wants nothing and asks for nothing. He does not mention
    /// the arrow, then or ever: asked, he talks about supper. The sword is not his and
    /// he says so, and says nothing about whose it is. Plain words, contractions, a
    /// tired man being kind; none of Mors's old grammar. The two of them are the book's
    /// two wanderers and should sound nothing alike.
    ///
    /// The first time anyone speaks to him (the opener) is what lets him go: after that
    /// he is gone from the shore the first time nobody is looking (PugreamPresence).
    /// </summary>
    public class PugreamTalk : ModSystem
    {
        /// <summary>The top of his hat is about fifty pixels over his feet; the bubble sits above that.</summary>
        private const int HeadHeight = 52;

        /// <summary>A little under the default: tired, not slow.</summary>
        private const float Pace = 0.7f;

        private const string FirstMeeting =
            "Name's Pugream. We haven't met. You've got the look of someone who almost died but didn't! " +
            "HA Ha Ha! That's most of us, on a good day! Stay if you like. I'm only looking at the water.";

        /// <summary>After the one thing there is to say back. The bars are Dialogue.Pause: he hesitates.</summary>
        private const string Coconuts =
            "Oh by the way, I managed to gather some coconuts|||.|||.|||. Here, have one.|| Or two!";

        private const string Again =
            "Pugream! I say it every time. It saves you the pretending you remember.";

        private const string AgainOther =
            "Pugream. We've spoken,| I think. Faces don't stay with people. Mine least of all.";

        private const string TheSword =
            "It isn't mine. People think it is and I let them, it's easier. I'm taking it somewhere. " +
            "It's heavier than it looks, and it looks heavy!";

        private const string TheArrow =
            "I shoot at a lot of things. Supper, mostly. Whatever it was, it's done with, and you're " +
            "still here. Hold on to that part.";

        private const string WhereTo =
            "Wherever the road stops, I suppose. I'll know it when my feet do. It isn't far now. " +
            "It's never far now, and then suddenly|.|.|. it is.";

        /// <summary>Back to his answers. Never remembered: a way back that goes grey is a trap (MorsTalk).</summary>
        private static readonly TalkOption Back =
            new TalkOption("Return", _ => Dialogue.Return()) { Remembered = false };

        public override void PostSetupContent()
        {
            int pugream = ModContent.NPCType<PugreamStanding>();

            // Nothing is put down in front of him either. It is also what leans the
            // camera in when the player walks up (Dialogue).
            Stillness.Register(pugream);

            // His face on the panel; black until he says his name, which is the first thing he says.
            DialoguePortrait.Register(pugream, "PisAndDag/Content/NPCs/Pugream/Pugream_Head");

            Dialogue.Register(pugream, Opener, HeadHeight, Pace,
                new TalkOption("The sword", _ => Dialogue.Say(TheSword, Back)),
                new TalkOption("The arrow", _ => Dialogue.Say(TheArrow, Back)),
                new TalkOption("Where you're going", _ => Dialogue.Say(WhereTo, Back)));
        }

        /// <summary>Two coconuts, into the player's hands, and the conversation opens up again.</summary>
        private static void GiveCoconuts()
        {
            Player player = Main.LocalPlayer;
            player.QuickSpawnItem(player.GetSource_GiftOrReward(), ItemID.Coconut, 2);

            Dialogue.Say(Coconuts, Back);
        }

        private static string Opener(Player player, NPC npc)
        {
            if (PugreamPresence.Stage < 2)
            {
                // Spoken to. He goes when nobody is looking.
                PugreamPresence.Stage = 2;

                // Locked, like Mors's first meetings: one thing to say back, and the
                // player's feet do not work until it is said.
                Dialogue.OpenAsScene(
                    new TalkOption("!...", _ => GiveCoconuts()) { Remembered = false });

                return FirstMeeting;
            }

            return Main.rand.NextBool() ? Again : AgainOther;
        }
    }
}
