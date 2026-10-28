using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;

namespace PisAndDag.Content.NPCs.Empress
{ 
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

            // Her second half's acts.
            Moor,
            Negative,
            Cage,
            Constellation,
            Blink,
            Shield,
            Ricochet,
            Heart,
            Lightspeed,
            Dawn,
            Refraction,
            Rain,
            Blade,
        }

        // ---- what she says -----------------------------------------------------------------------------------------
        //
        // Each a speech: most of one line, a few of two.

        // Her three speeches (by request: "intro, transition and ending should be unique and long"), the user's own words,
        // as recorded for her voice (Claude input/AmyVoiceLines.docx, E01 to E16, the script Amy Lam records from; proofread): the same every fight, a
        // line at a time at the foot of the screen, each scene held until they are said.

        /// <summary>Her coming (EmpressOfLight.Arrival): as soon as she appears, before she drags them up in her cage.</summary>
        private static readonly string[] IntroWords =
        {
            "I was asleep.",
            "Up there, the lacewings sing me to my slumber, every night, the same... soft... lullaby...",
            "But tonight, one of them ceased.",
            "And as I came down in search of the cause, I found thee... with its crushed wings still on thy hands.",
            "Rotten mortal, thou couldst never know how rare such perfect little things are.",
            "I shall not leave a flaw where I ferret one out.",
            "Unsheathe thy steel, puny one. Cry me thy havoc ballads.",
        };

        /// <summary>Her change (EmpressOfLight.Change): as the world starts to bend; the last of it just before she changes.</summary>
        private static readonly string[] ChangeBendWords =
        {
            "Ugh.",
            "This silk, this vain shroud, I have forgotten the unfettered wilderness.",
            "Turn thy gaze upon me, fleshy mortal. No... not at the light. At me!",
        };

        /// <summary>
        /// Her change: as time stops. Nothing now (cut from the script; "fleshy mortal" went into her last line as the world
        /// bends): the stillness is held in silence instead (ChangeStillHold), her scream ending it. Lines put back here are
        /// said there again, and the stillness held until they are.
        /// </summary>
        private static readonly string[] ChangeStillWords = { };

        /// <summary>Her fall (EmpressOfLight.Death): as the border closes in; it is thrown out as the last of it goes.</summary>
        private static readonly string[] FallClosingWords =
        {
            "Ah... so this is what it feels like. I had forgotten.",
            "Thy kind are exceptional, filthy mortal...",
            "Do not look so pleased. Light is not killed so easily...",
        };

        /// <summary>Her fall: once she has burst into butterflies, and the one she fought is let go.</summary>
        private static readonly string[] FallFlownWords =
        {
            "Take flight, then.",
            "I shall keep vigil over thee...",
            "We shall... meet again.",
        };

        /// <summary>Her speeches as the remarks' pools would have them (never drawn from: each is said whole, in its scene).</summary>
        private static readonly string[][] OpeningWords = { IntroWords };

        private static readonly string[][] CrackWords =
        {
            new[] { "Hold still. There seems to be a flaw." },
            new[] { "Reality is as fragile as glass!" },
            new[] { "Thy world shatters as easily as a mirror." },
        };

        private static readonly string[][] PillarsWords =
        {
            new[] { "Every colour in its place." },
            new[] { "Stand where the light allows." },
            new[] { "In order, toy. Always in order." },
        };

        private static readonly string[][] PrismWords =
        {
            new[] { "I granted this to a mortal once. Isaac, I recall..." },
            new[] { "Absolute, stark raving madness!" },
            new[] { "Why wilt thou not die?" },
        };

        private static readonly string[][] SawWords =
        {
            new[] { "The brightness shall gnaw thee in two with its roaring teeth." },
            new[] { "Light has an edge, too." },
            new[] { "Do not squirm. I am being careful." },
        };

        private static readonly string[][] MirageWords =
        {
            new[] { "Bare thy eyes onto us. Only one of us looks back." },
            new[] { "Choose... Why bother? Thy kind always choose wrong." },
        };

        private static readonly string[][] EclipseWords =
        {
            new[] { "Without me, only darkness awaits thee." },
            new[] { "Darkness swallows thee. Let the night reign!" },
            new[] { "Art thou coveting death? I shall deliver it unto thee." },
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

        private static readonly string[][] ChangeWords = { ChangeBendWords, ChangeStillWords };

        // Her second half: unveiled, the day held still. The same voice, nearer; she is no longer careful with the toy.

        private static readonly string[][] MoorWords =
        {
            new[] { "Insolent fool. Thou darest defy a demigod?" },
            new[] { "Canst thou search out these arrows of mine?" }, 
        };

        private static readonly string[][] NegativeWords =
        {
            new[] { "Incessant, pestering mortal. I command all that thou seest." },
            new[] { "Everything thou art not makes everything thou art." }, 
        };

        private static readonly string[][] CageWords =
        {
            new[] { "This cage befits vermin like thee." },
            new[] { "Fret not, toy. It burns to thy touch." },
        };

        private static readonly string[][] ConstellationWords =
        {
            new[] { "Geometric excellence." },
            new[] { "I gift unto thee the constellation of death!" },
        };

        private static readonly string[][] BlinkWords =
        {
            new[] { "Here. No, here. Thou hast missed me again!" },
            new[] { "Thou lookest where I was. Such is the sluggishness of flesh." },
        };

        private static readonly string[][] ShieldWords =
        {
            new[] { "Fine! Thou shalt perform thy most grueling attacks." }, 
        };

        private static readonly string[][] RicochetWords =
        { 
            new[] { "I command thee to surrender." },
        };

        private static readonly string[][] HeartWords =
        {
            new[] { "..." }, 
        };

        private static readonly string[][] LightspeedWords =
        {
            new[] { "Blink and thou shalt miss it!" },
            new[] { "Dost thou truly believe thou couldst escape light?" },
        };

        private static readonly string[][] DawnWords =
        {
            new[] { "O pestering mortal, thou dost seem a wee bit pale." },
        };

        private static readonly string[][] RefractionWords =
        {
            new[] { "One, added to half a dozen." },
            new[] { "Thou art marked for death." },
        };

        private static readonly string[][] RainWords =
        {
            new[] { "Thou wouldst look up? Look at that! Whoa!" },
            new[] { "Hast thou ever witnessed falling stars?" },
        };

        private static readonly string[][] BladeWords =
        {
            new[] { "I shall slice this mucus off the earth and rid it of sickness!" }, 
            new[] { "Try to parry this incandescent blade, plague-bearing mortal." },
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
            Words.Moor => MoorWords,
            Words.Negative => NegativeWords,
            Words.Cage => CageWords,
            Words.Constellation => ConstellationWords,
            Words.Blink => BlinkWords,
            Words.Shield => ShieldWords,
            Words.Ricochet => RicochetWords,
            Words.Heart => HeartWords,
            Words.Lightspeed => LightspeedWords,
            Words.Dawn => DawnWords,
            Words.Refraction => RefractionWords,
            Words.Rain => RainWords,
            Words.Blade => BladeWords,
            _ => ChangeWords,
        };

        // ---- when --------------------------------------------------------------------------------------------------

        /// <summary>Ticks she is quiet after a remark, on top of the time it takes to say.</summary>
        private const int TalkRest = 420;

        /// <summary>One act in this many begins with a line of its own: of each so many, one, which by chance.</summary>
        private const int ActsPerLine = 3;

        /// <summary>Her second half: of each so many acts, so many begin with a line of their own, which by chance.</summary>
        private const int SecondActsGroup = 18;
        private const int SecondActsSpoken = 7;

        /// <summary>How far over her the one she fights must keep, and for how long, for her to remark on it.</summary>
        private const float AboveBy = 320f;
        private const int AboveTicks = 180;

        /// <summary>Her life, as a share of it, when she remarks on the first mark on her.</summary>
        private const float MarkedAt = 0.8f;

        // ---- each machine's own ------------------------------------------------------------------------------------

        /// <summary>Ticks before she may remark again.</summary>
        private int talkRest;

        /// <summary>Her acts this machine has seen begin, this fight (counted in threes: ActsPerLine).</summary>
        private int actsSeen;

        /// <summary>How many times each moment has had its say: the next of its lines is the next in turn.</summary>
        private readonly int[] said = new int[(int)Words.Blade + 1];

        /// <summary>Her second half's acts this machine has seen begin, this fight (counted in eighteens).</summary>
        private int secondActsSeen;

        /// <summary>Who was down last tick (a fall is seen as the change); and whether that has been looked at once yet.</summary>
        private readonly bool[] fallen = new bool[Main.maxPlayers];
        private bool fallsKnown;

        /// <summary>Ticks the one she fights has kept well over her; and the remarks she makes once, made.</summary>
        private int aboveFor;
        private bool saidAbove;
        private bool saidMarked;

        /// <summary>Her words taken down as her death began.</summary>
        private bool hushed;

        /// <summary>Every tick, every machine (PostAI): her death hushes her; a fall, the player over her, her first mark.</summary>
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
                // Her second half: a fall still remarked on (her acts' lines come as they begin: ActWords).
                if (LegendsSecond(npc))
                    Falls(npc);

                return;
            }

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

            if (SecondWords(act, out Words second))
            {
                // Seven of each eighteen: those whose stirred numbers are the seven least of their eighteen.
                int round = secondActsSeen / SecondActsGroup;
                int at = secondActsSeen % SecondActsGroup;
                secondActsSeen++;

                int key = (int)(arenaCentre.X / 16f) ^ 0x2D1;
                int mine = Stir(round * SecondActsGroup + at, key, 1 << 20);
                int below = 0;

                for (int q = 0; q < SecondActsGroup; q++)
                {
                    int theirs = Stir(round * SecondActsGroup + q, key, 1 << 20);

                    if (theirs < mine || theirs == mine && q < at)
                        below++;
                }

                // Said whenever it falls to be, once whatever she said last is done.
                if (below < SecondActsSpoken)
                    Remark(npc, second, always: talkRest <= TalkRest);

                return;
            }

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

        /// <summary>Her second half's act's own lines, if it is one of hers.</summary>
        private static bool SecondWords(Act which, out Words moment)
        {
            moment = which switch
            {
                Act.Moor => Words.Moor,
                Act.Negative => Words.Negative,
                Act.Cage => Words.Cage,
                Act.Constellation => Words.Constellation,
                Act.Blink => Words.Blink,
                Act.Shield => Words.Shield,
                Act.Ricochet => Words.Ricochet,
                Act.Heart => Words.Heart,
                Act.Lightspeed => Words.Lightspeed,
                Act.Dawn => Words.Dawn,
                Act.Refraction => Words.Refraction,
                Act.Rain => Words.Rain,
                Act.Blade => Words.Blade,
                _ => Words.Opening,
            };

            return moment != Words.Opening;
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
