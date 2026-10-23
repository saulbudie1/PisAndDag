using Terraria.Audio;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// Her own sounds (by request, "some weird brain tickling sound effect"), made here by formula, nothing recorded: glass
    /// and light heard close, laid over the game's own in her acts (EmpressOfLight.Sound). In Content/Sounds/Empress:
    ///
    ///   Tingle 1-3   a sparse cloud of tiny glass pings, high, with a whisper of air under them
    ///   Crinkle      glass under strain: micro-clicks, quicker and quicker, a snap now and then
    ///   Swell        a shimmer played backwards, building to the moment it is cut off
    ///   Drop 1-2     a falling star: a ting, then a note gliding down
    ///   Zap 1-2      a laser of glass: a ringing that falls off, a crack, a thump under it
    ///   Wobble       a glass tone beating slowly against itself, wavering
    ///   WhooshUp     a whispered swish, rising (Whoosh1); WhooshDown, falling (Whoosh2)
    ///   Chime        seven bells up a whole-tone run, a room after them
    ///   Flutter      butterflies taking off: many soft wingbeats rushing out and thinning
    ///   Hum          a low hollow beating hum, a faint glass shimmer over it
    ///   Shards       glass burst into sparkles: a crack, then pings falling off
    ///   Shock        a shockwave: a thump, a whoom closing down, a whistle falling under it
    ///
    /// And her second half's, in Legends (the bow, the veil, time stopped):
    ///
    ///   TimeStop     clock ticks slowing, a suck into a frozen glass chord, the low end gone, a high ring fading
    ///   BowForm      tiny pings spiralling up and in to a bright chord, a string set with a low thrum
    ///   Draw         a string of light drawn back: a glassy creak quickening under a climbing tone
    ///   Loose        the string let go: a bright twang, a whoosh away
    ///   Thunk        an arrow into a wall of light: a crisp glassy impact, a ring, a thump, quivering
    ///   Tick 1-2     a countdown tick of glass
    ///   Burst        a great burst of light: a boom, glass cascading, a long shimmering tail
    ///   Invert       the world's colours turned inside out: a chord swelling, then flipped
    ///   Fold         curtains of light folding shut: a silken swish, a glass clasp
    ///   Lock         a mark of light set: two bright pings
    ///   Beam         a beam of light along a line: a buzzing zap cut off with a crackle
    ///   Blink        gone and here again: a pop backwards into a falling glassy swish
    ///   Barrier      a sphere of glass rising and holding: a beating hum, a shimmer swelling
    ///   Crack 1-3    glass cracking under a blow
    ///   Shatter      a sphere of mirror shattering: a crunch, then glass falling and falling
    ///   Ricochet     a glass arrow off a wall: a high zing
    ///   Charge       light gathering to fire: a whine climbing, beating faster
    ///   Lightspeed   a shot at the speed of light: a thunderclap of glass, a sizzle, a boom under it
    ///   Sunrise      a false dawn: a choir of light swelling and brightening, bells joining
    ///   Sear         everything not in shadow burned white: a flash, a sizzle, an impact
    ///   Refract      an arrow struck into a prism and split in seven: a plink fanning into seven notes
    ///   Fall 1-2     a column of light falling from the sky: a whistle down, a soft bright impact
    ///   Blade        a hand made a blade of rainbow: a long glassy shing
    ///   Slash        the blade swept: a whoosh with a ringing edge
    /// </summary>
    internal static class EmpressSounds
    {
        private const string Folder = "PisAndDag/Content/Sounds/Empress/";

        public static readonly SoundStyle TimeStop = new SoundStyle(Folder + "TimeStop");
        public static readonly SoundStyle BowForm = new SoundStyle(Folder + "BowForm");
        public static readonly SoundStyle Draw = new SoundStyle(Folder + "Draw") { PitchVariance = 0.05f };
        public static readonly SoundStyle Loose = new SoundStyle(Folder + "Loose") { PitchVariance = 0.06f };
        public static readonly SoundStyle Thunk = new SoundStyle(Folder + "Thunk") { PitchVariance = 0.08f };
        public static readonly SoundStyle Tick = new SoundStyle(Folder + "Tick", 2);
        public static readonly SoundStyle Burst = new SoundStyle(Folder + "Burst") { PitchVariance = 0.06f };
        public static readonly SoundStyle Invert = new SoundStyle(Folder + "Invert");
        public static readonly SoundStyle Fold = new SoundStyle(Folder + "Fold");
        public static readonly SoundStyle Lock = new SoundStyle(Folder + "Lock") { PitchVariance = 0.05f };
        public static readonly SoundStyle Beam = new SoundStyle(Folder + "Beam") { PitchVariance = 0.06f };
        public static readonly SoundStyle Blink = new SoundStyle(Folder + "Blink") { PitchVariance = 0.08f };
        public static readonly SoundStyle Barrier = new SoundStyle(Folder + "Barrier");
        public static readonly SoundStyle Crack = new SoundStyle(Folder + "Crack", 3) { PitchVariance = 0.1f };
        public static readonly SoundStyle Shatter = new SoundStyle(Folder + "Shatter");
        public static readonly SoundStyle Ricochet = new SoundStyle(Folder + "Ricochet") { PitchVariance = 0.1f };
        public static readonly SoundStyle Charge = new SoundStyle(Folder + "Charge");
        public static readonly SoundStyle Lightspeed = new SoundStyle(Folder + "Lightspeed") { PitchVariance = 0.05f };
        public static readonly SoundStyle Sunrise = new SoundStyle(Folder + "Sunrise");
        public static readonly SoundStyle Sear = new SoundStyle(Folder + "Sear");
        public static readonly SoundStyle Refract = new SoundStyle(Folder + "Refract");
        public static readonly SoundStyle Fall = new SoundStyle(Folder + "Fall", 2) { PitchVariance = 0.1f };
        public static readonly SoundStyle Blade = new SoundStyle(Folder + "Blade");
        public static readonly SoundStyle Slash = new SoundStyle(Folder + "Slash") { PitchVariance = 0.08f };

        public static readonly SoundStyle Tingle = new SoundStyle(Folder + "Tingle", 3) { PitchVariance = 0.1f };
        public static readonly SoundStyle Crinkle = new SoundStyle(Folder + "Crinkle");
        public static readonly SoundStyle Swell = new SoundStyle(Folder + "Swell");
        public static readonly SoundStyle Drop = new SoundStyle(Folder + "Drop", 2) { PitchVariance = 0.05f };
        public static readonly SoundStyle Zap = new SoundStyle(Folder + "Zap", 2) { PitchVariance = 0.06f };
        public static readonly SoundStyle Wobble = new SoundStyle(Folder + "Wobble");
        public static readonly SoundStyle WhooshUp = new SoundStyle(Folder + "Whoosh1");
        public static readonly SoundStyle WhooshDown = new SoundStyle(Folder + "Whoosh2");
        public static readonly SoundStyle Chime = new SoundStyle(Folder + "Chime");
        public static readonly SoundStyle Flutter = new SoundStyle(Folder + "Flutter");
        public static readonly SoundStyle Hum = new SoundStyle(Folder + "Hum");
        public static readonly SoundStyle Shards = new SoundStyle(Folder + "Shards");
        public static readonly SoundStyle Shock = new SoundStyle(Folder + "Shock");
    }
}
