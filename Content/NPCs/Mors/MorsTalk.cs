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
    /// He speaks slowly, in the old register, and he is not sure of anything. On a
    /// first meeting he must read as an odd old man on a road and nothing more; the
    /// most any line gives away is that he sits with people, that he forgets what he
    /// sets down, and that this ground is strange to him. Every line is built to be
    /// read again later. "I remember everyone" is a boast the first time and the
    /// wound the second.
    ///
    /// The first time anyone speaks to him inside the graveyard he says the graveyard
    /// line, once per world (MorsPresence.SaidGraveyard). After that he opens with
    /// whatever state you are in: hurt, or known to him, or lost.
    /// </summary>
    public class MorsTalk : ModSystem
    {
        /// <summary>His head is at the top of a 74px canvas; the bubble sits above that.</summary>
        private const int HeadHeight = 74;

        /// <summary>He is slow. A little over half the pace of anyone else.</summary>
        private const float Pace = 0.4f;

        private const string FirstAtGraveyard =
            "Ah. I thought I saw you dying, once. Or not. O nay, my mind is not straight, pay it " +
            "no heed. Folk crawl up out of this ground, some days. I have watched them do it. " +
            "This plot had feeling in it, once. Of that I am nearly certain.";

        private const string Hurt =
            "You are hurt. Do not fret over it. It happens.";

        private const string Known =
            "Ah. It is you. I think it is you.";

        private const string LostTrack =
            "A moment. I have lost the thread of myself again. It comes back. Mostly, it comes back.";

        private const string HaveWeMetDied =
            "Mm. Your face is one I have seen. Or one like it. There are a great many faces, and " +
            "they go by quickly. Forgive me. I could not tell you where.";

        private const string HaveWeMetNever =
            "Nay. I would remember. I remember everyone.";

        private const string TheKnife =
            "A knife. Old, that. I have seen its like on many a belt, I think. It is a familiar " +
            "shape. Most shapes are, by now.";

        public override void PostSetupContent()
        {
            int blade = ModContent.ItemType<BladeOfMors>();

            Dialogue.Register(ModContent.NPCType<MorsIdle>(), Opener, HeadHeight, Pace,
                new TalkOption("Have we met",
                    _ => Dialogue.Say(GraveyardZone.Deaths > 0 ? HaveWeMetDied : HaveWeMetNever)),
                new TalkOption(TalkOptions.Icon(blade) + "The knife",
                    _ => Dialogue.Say(TheKnife),
                    (player, _) => player.HasItem(blade)));
        }

        /// <summary>
        /// The first thing he says. The graveyard line once, if this is the graveyard.
        /// Then: a quarter of the time he loses the thread before he starts; hurt he
        /// notices before he notices you; otherwise he half knows you.
        /// </summary>
        private static string Opener(Player player, NPC npc)
        {
            // The world's graveyard, and only that one. Seven tombstones in a field
            // are not this place.
            bool graveyard = player.GetModPlayer<GraveyardPlayer>().Inside;

            if (graveyard && !MorsPresence.SaidGraveyard)
            {
                MorsPresence.SaidGraveyard = true;
                return FirstAtGraveyard;
            }

            if (Main.rand.NextBool(4))
                return LostTrack;

            if (player.statLife < player.statLifeMax2)
                return Hurt;

            return Known;
        }
    }
}
