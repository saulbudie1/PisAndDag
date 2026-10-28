using Terraria.Audio;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// Skeletron's own sounds (by request: "Dont cheap out on the visual and sounds its what gets the people going"),
    /// made here by formula, nothing recorded: cartoonish but big, bone first. In Content/Sounds/Skeletron:
    ///
    ///   his coming     Cough 1-2 (the old man), BoneBurst (his hand bursting into arms), Tear (torn down the middle),
    ///                  Pop (the skull up out of him), RoarBig (his roar)
    ///   bone           Rattle 1-3, Clack (the jaw shut), Chomp 1-2, Thud (a fist into the ground), SelfCrack (his own
    ///                  skull cracking)
    ///   his hands      Dribble 1-2, Fadeaway, Punch, Whack, Bump, Tsk 1-3, FlickCharge, Flick, Snap, Handshake, WhipCrack,
    ///                  Swoop, Pinch, Tornado, Cackle, Seal 1-5, Poof, Summon, Pew 1-3, BlowSmoke, GroundCrack, SpikeRise,
    ///                  Jan, Ken, Pon, Snip, BowlingRoll, Strike, PurpleCharge
    ///   his skull      Ignite (eyes on fire), FlameBreath, JawWhir (the jaw thrown)
    ///   the guitar     Riff 1-3, Stab 1-3, RiffEnd (the rock-star solo)
    /// </summary>
    internal static class SkeletronSounds
    {
        private const string Folder = "PisAndDag/Content/Sounds/Skeletron/";

        public static readonly SoundStyle Cough1 = new SoundStyle(Folder + "Cough1");
        public static readonly SoundStyle Cough2 = new SoundStyle(Folder + "Cough2");
        public static readonly SoundStyle BoneBurst = new SoundStyle(Folder + "BoneBurst");
        public static readonly SoundStyle Tear = new SoundStyle(Folder + "Tear");
        public static readonly SoundStyle Pop = new SoundStyle(Folder + "Pop");
        public static readonly SoundStyle RoarBig = new SoundStyle(Folder + "RoarBig");

        public static readonly SoundStyle Rattle = new SoundStyle(Folder + "Rattle", 3) { PitchVariance = 0.12f, MaxInstances = 4 };
        public static readonly SoundStyle Clack = new SoundStyle(Folder + "Clack") { PitchVariance = 0.1f, MaxInstances = 3 };
        public static readonly SoundStyle Chomp = new SoundStyle(Folder + "Chomp", 2) { PitchVariance = 0.08f, MaxInstances = 3 };
        public static readonly SoundStyle Thud = new SoundStyle(Folder + "Thud") { PitchVariance = 0.08f, MaxInstances = 4 };
        public static readonly SoundStyle SelfCrack = new SoundStyle(Folder + "SelfCrack");

        public static readonly SoundStyle Dribble = new SoundStyle(Folder + "Dribble", 2) { PitchVariance = 0.05f };
        public static readonly SoundStyle Fadeaway = new SoundStyle(Folder + "Fadeaway");
        public static readonly SoundStyle Punch = new SoundStyle(Folder + "Punch") { PitchVariance = 0.08f, MaxInstances = 3 };
        public static readonly SoundStyle Whack = new SoundStyle(Folder + "Whack") { PitchVariance = 0.08f, MaxInstances = 3 };
        public static readonly SoundStyle Bump = new SoundStyle(Folder + "Bump");
        public static readonly SoundStyle Tsk = new SoundStyle(Folder + "Tsk", 3) { MaxInstances = 3 };
        public static readonly SoundStyle FlickCharge = new SoundStyle(Folder + "FlickCharge");
        public static readonly SoundStyle Flick = new SoundStyle(Folder + "Flick");
        public static readonly SoundStyle Snap = new SoundStyle(Folder + "Snap") { PitchVariance = 0.05f };
        public static readonly SoundStyle Handshake = new SoundStyle(Folder + "Handshake");
        public static readonly SoundStyle WhipCrack = new SoundStyle(Folder + "WhipCrack");
        public static readonly SoundStyle Swoop = new SoundStyle(Folder + "Swoop") { PitchVariance = 0.06f, MaxInstances = 2 };
        public static readonly SoundStyle Pinch = new SoundStyle(Folder + "Pinch");
        public static readonly SoundStyle Tornado = new SoundStyle(Folder + "Tornado") { MaxInstances = 2 };
        public static readonly SoundStyle Cackle = new SoundStyle(Folder + "Cackle");
        public static readonly SoundStyle Seal1 = new SoundStyle(Folder + "Seal1");
        public static readonly SoundStyle Seal2 = new SoundStyle(Folder + "Seal2");
        public static readonly SoundStyle Seal3 = new SoundStyle(Folder + "Seal3");
        public static readonly SoundStyle Seal4 = new SoundStyle(Folder + "Seal4");
        public static readonly SoundStyle Seal5 = new SoundStyle(Folder + "Seal5");
        public static readonly SoundStyle Poof = new SoundStyle(Folder + "Poof") { MaxInstances = 3 };
        public static readonly SoundStyle Summon = new SoundStyle(Folder + "Summon");
        public static readonly SoundStyle Pew = new SoundStyle(Folder + "Pew", 3) { PitchVariance = 0.06f, MaxInstances = 4 };
        public static readonly SoundStyle BlowSmoke = new SoundStyle(Folder + "BlowSmoke");
        public static readonly SoundStyle GroundCrack = new SoundStyle(Folder + "GroundCrack") { MaxInstances = 2 };
        public static readonly SoundStyle SpikeRise = new SoundStyle(Folder + "SpikeRise") { PitchVariance = 0.12f, MaxInstances = 5 };
        public static readonly SoundStyle Jan = new SoundStyle(Folder + "Jan");
        public static readonly SoundStyle Ken = new SoundStyle(Folder + "Ken");
        public static readonly SoundStyle Pon = new SoundStyle(Folder + "Pon");
        public static readonly SoundStyle Snip = new SoundStyle(Folder + "Snip");
        public static readonly SoundStyle BowlingRoll = new SoundStyle(Folder + "BowlingRoll") { MaxInstances = 2 };
        public static readonly SoundStyle Strike = new SoundStyle(Folder + "Strike");
        public static readonly SoundStyle PurpleCharge = new SoundStyle(Folder + "PurpleCharge");

        public static readonly SoundStyle Ignite = new SoundStyle(Folder + "Ignite");
        public static readonly SoundStyle FlameBreath = new SoundStyle(Folder + "FlameBreath") { MaxInstances = 2 };
        public static readonly SoundStyle JawWhir = new SoundStyle(Folder + "JawWhir") { MaxInstances = 2 };

        public static readonly SoundStyle Riff1 = new SoundStyle(Folder + "Riff1");
        public static readonly SoundStyle Riff2 = new SoundStyle(Folder + "Riff2");
        public static readonly SoundStyle Riff3 = new SoundStyle(Folder + "Riff3");
        public static readonly SoundStyle Stab = new SoundStyle(Folder + "Stab", 3) { MaxInstances = 3 };
        public static readonly SoundStyle RiffEnd = new SoundStyle(Folder + "RiffEnd");

        /// <summary>The seals in order, one a beat.</summary>
        public static SoundStyle Seal(int index) => (index % 5) switch
        {
            0 => Seal1,
            1 => Seal2,
            2 => Seal3,
            3 => Seal4,
            _ => Seal5,
        };

        /// <summary>The riffs in order.</summary>
        public static SoundStyle Riff(int index) => (index % 3) switch
        {
            0 => Riff1,
            1 => Riff2,
            _ => Riff3,
        };
    }
}
