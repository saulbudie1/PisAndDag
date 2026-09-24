using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.LoreItem;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.Structures;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mors
{
    /// <summary>
    /// What Mors says when you stand in front of him (Lore Bible §VIII, §XV, §XVI).
    ///
    /// He speaks slowly, formally, and out of date. He is older than the grammar
    /// everyone else is using and he never picked up the new one: thee and thou, hast
    /// and doth, naught and aught, and the word order that goes with them. Measured,
    /// not pantomime — the Soulsborne level of it (§IV), never a rustic accent. Short
    /// sentences all the same; an old man is not a florid one.
    ///
    /// The player's own answers stay plain. Only he is old, and the gap between the two
    /// registers is the first thing that is wrong with him.
    ///
    /// He is sure of nothing. The rule for every line: he begins something, loses it,
    /// and finishes a different sentence. He corrects himself and the correction is
    /// also wrong. He says a thing twice and means it differently the second time. On a
    /// first meeting he must read as an odd old man on a road and nothing more, and
    /// every line is built to be read again later. "I remember everyone" is a boast the
    /// first time and the wound the second.
    ///
    /// His two first meetings are scenes rather than conversations: the graveyard and
    /// the hill. Each opens locked, with one thing to say back and no way out of the
    /// panel, the player held where they stand and the camera leaned in. The ordinary
    /// answers do not appear inside one, which is the point: a man is telling you
    /// something and there is nothing to ask him yet. Nothing he says on the hill
    /// names the blade, the monument, or himself.
    /// </summary>
    public class MorsTalk : ModSystem
    {
        /// <summary>His head is at the top of a 74px canvas; the bubble sits above that.</summary>
        private const int HeadHeight = 74;

        /// <summary>He is slow. A little over half the pace of anyone else.</summary>
        private const float Pace = 0.4f;

        private const string FirstAtGraveyard =
            "Ah.|| I thought||, I saw thee dying|||, once. Or I did not.||| O nay,|| pay it no heed. My mind " +
            "runs not straight||, and hath not for some while. Folk crawl up out of this ground on " +
            "some days. I have watched them at it, and never once been asked to help. This plot " +
            "had feeling in it. It had. Of that much I am near certain.";

        private const string FirstAtMonument =
            "Ah.|| Thou, as well. I come here often. Or I came here once. I||| cannot tell which. " +
            "These flowers have no business in such rock.||| One stood here a long while. I should " +
            "have liked to meet him.";

        private const string AskPouch =
            "O. He is not here now. That much I can manage.|| Who he be,|| I could not say.||| Ah. Before " +
            "thou goest. A pouch.| Berries in it, or near enough. I set it down somewhere||| in the " +
            "cold dark brick room of the world. I would have it again,| should it cross thy path.";

        private const string Thanks =
            "Mm.|||.||.|. Kindly meant.||| Put thyself to no trouble over it. It is but a pouch, and I|| have " +
            "done without.||| I shall be somewhere when thou hast it.|| I am always somewhere.||| That much is" +
            "the one thing I can promise thee.";

        private const string Shrug =
            "Right.||.||.||. That is the sense of it. A man should keep what is his own. The knack never came " +
            "to me, and I have had a great while to learn it.";

        private const string Resting =
            "I am at rest. I do that here, mostly. Since when, I could not tell thee.";

        private const string StillCarried =
            "Thou carriest it still. Good. A man should keep account of his things. Whose was it, " +
            "dost thou know? No matter. It suits thee.";

        private const string GraveyardClose =
            "Mm.||.||.||. That is the right answer,|| I think. Most do talk.";

        private const string Hurt =
            "Thou art hurt. Fret not over it. It happens. It keeps happening, to all, and still|||| " +
            "none|| are used to it.";

        private const string Known =
            "Ah. It is thee. I think it is thee. Correct me not. I had rather be wrong quietly.";

        private const string LostTrack =
            "A moment. I have lost the thread of myself again. It cometh back. Most of it shall be" +
            "back.|| I no longer count what does not.";

        private const string HaveWeMetDied =
            "Mm. Thy face is one I have seen. Or one very like it. There are a great many faces, " +
            "and they go by at speed. Forgive me. I could tell thee that we have met. Where, nor " +
            "when, nor whether thou wert standing, I could not.";

        private const string HaveWeMetNever =
            "Nay. I would remember. I remember everyone.";

        private const string TheKnife =
            "A knife. Old, that. I have seen its like on many a belt. Or on one belt, many times " +
            "over. A familiar shape to me. Most shapes are, by now.";

        private const string KnifeOnTheHill =
            "Hold it up a moment. Lower. Toward the ground. There. That is the way of it. Nay, " +
            "ask me not. I could not tell thee why I asked. My hands were sure of it and I was " +
            "not. Put it away.";

        private const string TheFlowers =
            "Fireblossom.||| They bloom when the heat climbs and shut when it falls, and ask nothing " +
            "further of any man.|| They come up only where a thing was finished.|| Pro|per|ly| finished. " +
            "What was finished here|| I never made out, and I have had every chance to ask.";

        private const string ThePouch =
            "Mm. A pouch. A good one, and the berries have kept, which is a wonder. Nay. I have " +
            "lost naught. I would know if I had. Keep it. It is a poor road to walk with nothing " +
            "on thee.";

        /// <summary>He is standing on the ash hill rather than up in the graveyard.</summary>
        private static bool OnTheHill(NPC npc) => npc.Center.Y / 16f >= Main.UnderworldLayer;

        /// <summary>
        /// What sits under an answer that told the player something and changed nothing.
        ///
        /// Those branches used to end, which left one button on the panel and that button
        /// ended the conversation — so asking him about the flowers was the same as
        /// saying goodbye, and a man you can ask four things became a man you can ask one
        /// thing. This goes back to the list instead. Leave is still under it.
        ///
        /// Not on the pouch: that one moves the errand, and a branch that did something
        /// is entitled to stop. Not in the two scenes either, which are first meetings
        /// and have no way out but through by design.
        ///
        /// Never remembered, like Leave. It is not a line, and a way back that goes grey
        /// is a trap.
        /// </summary>
        private static readonly TalkOption Back =
            new TalkOption("Return", _ => Dialogue.Return()) { Remembered = false };

        public override void PostSetupContent()
        {
            int blade = ModContent.ItemType<BladeOfMors>();
            int pouch = ModContent.ItemType<PouchOfBerries>();
            int mors = ModContent.NPCType<MorsIdle>();

            // Nothing spawns while he is stood there. It is not kindness; the world
            // simply does not put anything down in front of him.
            Stillness.Register(mors);

            Dialogue.Register(mors, Opener, HeadHeight, Pace,
                new TalkOption("Have we met",
                    _ => Dialogue.Say(GraveyardZone.Deaths > 0 ? HaveWeMetDied : HaveWeMetNever, Back)),
                new TalkOption("The flowers",
                    _ => Dialogue.Say(TheFlowers, Back),
                    (_, npc) => OnTheHill(npc)),
                new TalkOption(TalkOptions.Icon(blade) + "The knife",
                    npc => Dialogue.Say(OnTheHill(npc) ? KnifeOnTheHill : TheKnife, Back),
                    (player, _) => player.HasItem(blade)),
                new TalkOption(TalkOptions.Icon(pouch) + "The pouch",
                    _ =>
                    {
                        MorsErrand.Returned = true;
                        Dialogue.Say(ThePouch);
                    },
                    (player, _) => player.HasItem(pouch) && !MorsErrand.Returned));
        }

        /// <summary>
        /// The graveyard scene. He is not asking anything and there is nothing to ask
        /// him, so the one answer is the player saying nothing, which he takes as an
        /// answer. It ends there and the ordinary conversation is open afterwards.
        /// </summary>
        private static void OpenGraveyardScene()
        {
            Dialogue.OpenAsScene(
                new TalkOption("Say nothing", _ => Dialogue.Say(GraveyardClose)) { Remembered = false });
        }

        /// <summary>
        /// The hill scene, built fresh each time it is needed so the answers close over
        /// nothing stale. One thing to say back, then the errand, then two answers and
        /// out. No Leave anywhere in it, and the player's feet do not work until the
        /// last line: he is talking, and it would be rude, and it would leave the world
        /// half-way through a thing it only does once.
        /// </summary>
        private static void OpenHillScene()
        {
            Dialogue.OpenAsScene(
                new TalkOption("What is he doing here", _ => Dialogue.Scene(AskPouch,
                    new TalkOption("I will find it", _ => Answer(true)) { Remembered = false },
                    new TalkOption("Watch your own belongings", _ => Answer(false)) { Remembered = false }))
                { Remembered = false });
        }

        /// <summary>
        /// Either answer ends the scene, and either way he is done with the hill: the
        /// asking was the point, not the pouch. Say with no follow-ups lets the player
        /// go. Saying yes is the only thing that sets the errand proper.
        /// </summary>
        private static void Answer(bool yes)
        {
            MorsErrand.Asked = true;
            MorsErrand.Accepted = yes;

            // He has said the thing he came down to say. He is not owed to the hill any
            // more, and MorsPresence takes him away as soon as nobody is looking.
            if (MorsPresence.Stage < 2)
                MorsPresence.Stage = 2;

            Dialogue.Say(yes ? Thanks : Shrug);
        }

        /// <summary>
        /// The first thing he says. The graveyard gets its one line, the hill gets its
        /// one scene, and after that: a quarter of the time he loses the thread before
        /// he starts; hurt he notices before he notices you; otherwise he half knows you.
        /// </summary>
        private static string Opener(Player player, NPC npc)
        {
            // The world's graveyard, and only that one. Seven tombstones in a field
            // are not this place.
            bool graveyard = player.GetModPlayer<GraveyardPlayer>().Inside;

            if (graveyard && !MorsPresence.SaidGraveyard)
            {
                MorsPresence.SaidGraveyard = true;
                OpenGraveyardScene();
                return FirstAtGraveyard;
            }

            if (OnTheHill(npc) && !MorsPresence.SaidMonument)
            {
                MorsPresence.SaidMonument = true;
                OpenHillScene();
                return FirstAtMonument;
            }

            // He gave it back and told them to keep it, and is pleased with them for
            // keeping it, and does not know whose it was.
            if (MorsErrand.Returned && player.HasItem(ModContent.ItemType<PouchOfBerries>())
                && Main.rand.NextBool(3))
                return StillCarried;

            if (OnTheHill(npc) && Main.rand.NextBool(3))
                return Resting;

            if (Main.rand.NextBool(4))
                return LostTrack;

            if (player.statLife < player.statLifeMax2)
                return Hurt;

            return Known;
        }
    }
}
