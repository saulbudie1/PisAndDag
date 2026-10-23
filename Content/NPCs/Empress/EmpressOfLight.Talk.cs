using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- her words (Legends, her first half) -------------------------------------------------------------------
    //
    // By request ("she should trash talk dialog (a system that is only a line and underlined like edlen ring and it
    // auto skips after a while depending on the text) in phase 1 in legends. Look into trash talks from Elden ring and
    // Dark Souls bosses"). Theirs say little, in an old way of speaking, to the player and of them, by a name of their
    // own for them; mostly at the turns of the fight: as it begins, at a change, when the player dies. (Dark Souls'
    // bosses hardly speak at all.) Hers is that, a little more often, at the foot of the screen (Taunt):
    //
    //   AS IT BEGINS    a little after her name has gone down to the bar: two lines
    //   HER ACTS        one act in three, by request ("make it rare tho, like 1 in 3 attack have one"): of
    //                   each three she plays, one begins with a line of that act's, which one by chance
    //   HER HAND        whenever she has someone in it
    //   A FALL          whenever anyone in her arena dies
    //   ABOVE HER       once, when the one she fights keeps well over her a while
    //   MARKED          once, when a fifth of her life is gone
    //   HER CHANGE      at half her life, through her change (EmpressOfLight.Change): a line as it begins, another as
    //                   the day stops; and then nothing more
    //
    // Her way of speaking is her death's (thee and thou, and "toy"): she does not threaten; she corrects, and she is
    // amused. Each moment has a few lines, said in turn, so none comes twice before the rest have. After any of them
    // she is quiet a while (TalkRest), but for her hand, a fall, her first words and her change, which she always
    // remarks on. Each machine says them as its own copy of the fight comes to them (nothing is sent); which line it
    // is, is the same on all of them that have seen the same.

    public partial class EmpressOfLight
    {
        /// <summary>The moments she speaks at.</summary>
        private enum Words : byte
        {
            Opening,
            Crack,
            Pillars,
            Prism,
            Saw,
            Mirage,
            Eclipse,
            Aurora,
            Held,
            Fell,
            Above,
            Marked,
            Change,
        }

        // ---- what she says -----------------------------------------------------------------------------------------
        //
        // Each a speech: most of one line, a few of two.

        private static readonly string[][] OpeningWords =
        {
            new[] { "A lacewing, for a toy.", "It is a fair price." },
            new[] { "Thou didst break one of mine.", "Come. Let me see how thou art made." },
            new[] { "So. This is what called me down.", "I expected more." },
        };

        private static readonly string[][] CrackWords =
        {
            new[] { "Hold still. There is a flaw." },
            new[] { "It was always glass." },
            new[] { "Thy world breaks so easily." },
        };

        private static readonly string[][] PillarsWords =
        {
            new[] { "Every colour in its place." },
            new[] { "Stand where the light allows." },
            new[] { "In order, toy. Always in order." },
        };

        private static readonly string[][] PrismWords =
        {
            new[] { "Seven, from one. As it should be." },
            new[] { "See how the light obeys." },
            new[] { "It only turns faster." },
        };

        private static readonly string[][] SawWords =
        {
            new[] { "Come closer. I have made thee something." },
            new[] { "Light has an edge, too." },
            new[] { "Do not squirm. I am being careful." },
        };

        private static readonly string[][] MirageWords =
        {
            new[] { "Look well. Only one of us looks back." },
            new[] { "Choose. Thy kind always choose wrong." },
        };

        private static readonly string[][] EclipseWords =
        {
            new[] { "Without me, there is no light at all." },
            new[] { "Thou art very small, in the dark." },
            new[] { "There is one way out. Find it." },
        };

        private static readonly string[][] AuroraWords =
        {
            new[] { "The sky is drawing shut." },
            new[] { "Thou wert never meant to be up here." },
            new[] { "Stay where thou art put." },
        };

        private static readonly string[][] HeldWords =
        {
            new[] { "Be still." },
            new[] { "So light. All thy kind are." },
            new[] { "I have thee, toy." },
        };

        private static readonly string[][] FellWords =
        {
            new[] { "Broken already." },
            new[] { "Down. Where thy kind belong." },
            new[] { "I shall have another." },
            new[] { "Thy wings were only lent." },
        };

        private static readonly string[][] AboveWords =
        {
            new[] { "Thou fliest by my leave." },
        };

        private static readonly string[][] MarkedWords =
        {
            new[] { "A mark on me. It will be corrected." },
        };

        /// <summary>
        /// Her change (EmpressOfLight.Change), by request "something like malenia and morgott and godrick": the first line
        /// as it begins, the second as the day stops. One of the two each fight, the same on every machine.
        /// </summary>
        private static readonly string[][] ChangeWords =
        {
            new[] { "Look at me, toy. Not at the light.", "Be still. The hours are." },
            new[] { "Thou hast only seen what I wear.", "The day will wait. It always has." },
        };

        private static string[][] Pool(Words moment) => moment switch
        {
            Words.Opening => OpeningWords,
            Words.Crack => CrackWords,
            Words.Pillars => PillarsWords,
            Words.Prism => PrismWords,
            Words.Saw => SawWords,
            Words.Mirage => MirageWords,
            Words.Eclipse => EclipseWords,
            Words.Aurora => AuroraWords,
            Words.Held => HeldWords,
            Words.Fell => FellWords,
            Words.Above => AboveWords,
            Words.Marked => MarkedWords,
            _ => ChangeWords,
        };

        // ---- when --------------------------------------------------------------------------------------------------

        /// <summary>Ticks she is quiet after a remark, on top of the time it takes to say.</summary>
        private const int TalkRest = 420;

        /// <summary>One act in this many begins with a line of its own: of each so many, one, which by chance.</summary>
        private const int ActsPerLine = 3;

        /// <summary>Her first words, this long after her coming lets go (her name going down to the bar between).</summary>
        private const int OpeningAfter = 90;

        /// <summary>How far over her the one she fights must keep, and for how long, for her to remark on it.</summary>
        private const float AboveBy = 320f;
        private const int AboveTicks = 180;

        /// <summary>Her life, as a share of it, when she remarks on the first mark on her.</summary>
        private const float MarkedAt = 0.8f;

        // ---- each machine's own ------------------------------------------------------------------------------------

        /// <summary>Ticks before she may remark again; ticks to her first words (0: none to come).</summary>
        private int talkRest;
        private int openingIn;

        /// <summary>Her acts this machine has seen begin, this fight (counted in threes: ActsPerLine).</summary>
        private int actsSeen;

        /// <summary>How many times each moment has had its say: the next of its lines is the next in turn.</summary>
        private readonly int[] said = new int[(int)Words.Change + 1];

        /// <summary>Who was down last tick (a fall is seen as the change); and whether that has been looked at once yet.</summary>
        private readonly bool[] fallen = new bool[Main.maxPlayers];
        private bool fallsKnown;

        /// <summary>Ticks the one she fights has kept well over her; and the remarks she makes once, made.</summary>
        private int aboveFor;
        private bool saidAbove;
        private bool saidMarked;

        /// <summary>Her words taken down as her death began.</summary>
        private bool hushed;

        /// <summary>Every tick, every machine (PostAI): her death hushes her; her first words come; a fall, the player over her, her first mark.</summary>
        private void Talk(NPC npc)
        {
            if (Main.dedServ || !PADWorld.Legends)
                return;

            // Her death has the foot of the screen (her last words come in the panel).
            if (dying || scene == Scene.Death)
            {
                if (!hushed)
                {
                    Taunt.Hush(npc);
                    hushed = true;
                }

                return;
            }

            if (talkRest > 0)
                talkRest--;

            if (!LegendsHalf(npc))
            {
                openingIn = 0;
                return;
            }

            if (openingIn > 0 && --openingIn == 0)
                Remark(npc, Words.Opening, always: true);

            Falls(npc);

            // The one she fights, well over her and keeping there: once.
            Player target = TargetOf(npc);

            if (!saidAbove && scene == Scene.None && target != null && target.Center.Y < npc.Center.Y - AboveBy)
            {
                if (++aboveFor >= AboveTicks && talkRest == 0)
                {
                    Remark(npc, Words.Above);
                    saidAbove = true;
                }
            }
            else
            {
                aboveFor = 0;
            }

            // The first fifth of her gone: once.
            if (!saidMarked && scene == Scene.None && talkRest == 0 && npc.life < npc.lifeMax * MarkedAt)
            {
                Remark(npc, Words.Marked);
                saidMarked = true;
            }
        }

        /// <summary>Anyone in her arena down since last tick: she remarks on it (once, if two fall together).</summary>
        private void Falls(NPC npc)
        {
            bool first = !fallsKnown;
            bool fell = false;

            fallsKnown = true;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                bool down = p.active && p.dead;

                if (down && !fallen[i] && !first && Vector2.Distance(p.Center, arenaCentre) <= EmpressArena.Reach)
                    fell = true;

                fallen[i] = down;
            }

            if (fell)
                Remark(npc, Words.Fell, always: true);
        }

        /// <summary>As an act begins (RunAct, every machine): one in every three, a line of that act's.</summary>
        private void ActWords(NPC npc)
        {
            if (Main.dedServ)
                return;

            int group = actsSeen / ActsPerLine;
            int place = actsSeen % ActsPerLine;
            actsSeen++;

            // Which of each three has its line: by chance, but the same on every machine that has seen the same acts.
            if (place != Stir(group, (int)(arenaCentre.X / 16f), ActsPerLine))
                return;

            Words moment = act switch
            {
                Act.Crack => Words.Crack,
                Act.Pillars => Words.Pillars,
                Act.Prism => Words.Prism,
                Act.Saw => Words.Saw,
                Act.Mirage => Words.Mirage,
                Act.Eclipse => Words.Eclipse,
                Act.Aurora => Words.Aurora,
                _ => Words.Opening,
            };

            if (moment != Words.Opening)
                Remark(npc, moment);
        }

        /// <summary>A number below <paramref name="range"/> from two others, well stirred: the same for the same two, on every machine.</summary>
        private static int Stir(int a, int b, int range)
        {
            uint h = unchecked((uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA6Bu);
            h ^= h >> 16;
            h = unchecked(h * 0x7FEB352Du);
            h ^= h >> 15;
            h = unchecked(h * 0x846CA68Bu);
            h ^= h >> 16;

            return (int)(h % (uint)range);
        }

        /// <summary>
        /// The next of this moment's lines, said, if she is not resting from the last (or it is one she always says);
        /// heard by whoever is in her arena or near it.
        /// </summary>
        private void Remark(NPC npc, Words moment, bool always = false)
        {
            if (Main.dedServ || !PADWorld.Legends || !always && talkRest > 0)
                return;

            string[][] pool = Pool(moment);
            int n = pool.Length;

            // In turn from a place of the fight's own (where her arena stands), so every machine starts at the same one.
            int start = (int)(arenaCentre.X / 16f) * 7 + (int)(arenaCentre.Y / 16f) * 3 + (int)moment * 13;
            string[] speech = pool[((start + said[(int)moment]++) % n + n) % n];

            talkRest = TalkRest + Taunt.TicksFor(speech);

            if (NearArena())
                Taunt.Speech(npc, speech, EmpressArt.Pink, EmpressArt.Sky, reach: 0f);
        }
    }
}
