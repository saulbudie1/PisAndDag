using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// The Gilded Knight's own sounds, made by formula, nothing recorded (Content/Sounds/Knight):
    ///
    ///   his armour      Step 1-3 (a boot on stone and the plates ringing after it: by request,
    ///                   "each walk he takes makes a metal wrangling armor sound"), Kneel
    ///   his swords      Unsheathe 1-2, Point (the blade levelled at them), Swing 1-3, FlameSwing 1-2
    ///                   (the Sun on fire), Clash (a blade turned aside), Ward (the gold about him
    ///                   after), Slam, Shockwave
    ///   his light       Crescent, LancetCharge, LancetFire, Votive, VotiveLoose, VotiveHit, Ignite,
    ///                   Pyre, PyreMark, GreatCrescent, Procession, GrowBlade, BladeSlam, BladeSweep,
    ///                   SunForm, SunFall, SunBurst
    ///   his end         nothing (by request: it is silent)
    /// </summary>
    internal static class KnightSounds
    {
        private const string Folder = "PisAndDag/Content/Sounds/Knight/";

        public static readonly SoundStyle StepSound = new SoundStyle(Folder + "Step", 3) { PitchVariance = 0.08f, MaxInstances = 3, Volume = 0.85f };
        public static readonly SoundStyle Kneel = new SoundStyle(Folder + "Kneel");

        public static readonly SoundStyle Unsheathe1 = new SoundStyle(Folder + "Unsheathe1");
        public static readonly SoundStyle Unsheathe2 = new SoundStyle(Folder + "Unsheathe2");
        public static readonly SoundStyle Point = new SoundStyle(Folder + "Point");
        public static readonly SoundStyle Swing = new SoundStyle(Folder + "Swing", 3) { PitchVariance = 0.06f, MaxInstances = 4 };
        public static readonly SoundStyle FlameSwing = new SoundStyle(Folder + "FlameSwing", 2) { PitchVariance = 0.06f, MaxInstances = 4 };
        public static readonly SoundStyle Clash = new SoundStyle(Folder + "Clash") { PitchVariance = 0.05f, MaxInstances = 3 };
        public static readonly SoundStyle Ward = new SoundStyle(Folder + "Ward") { MaxInstances = 2 };
        public static readonly SoundStyle Slam = new SoundStyle(Folder + "Slam") { MaxInstances = 2 };
        public static readonly SoundStyle Shockwave = new SoundStyle(Folder + "Shockwave") { MaxInstances = 2 };

        public static readonly SoundStyle Crescent = new SoundStyle(Folder + "Crescent") { PitchVariance = 0.05f, MaxInstances = 3 };
        public static readonly SoundStyle LancetCharge = new SoundStyle(Folder + "LancetCharge");
        public static readonly SoundStyle LancetFire = new SoundStyle(Folder + "LancetFire");
        public static readonly SoundStyle Votive = new SoundStyle(Folder + "Votive") { PitchVariance = 0.04f, MaxInstances = 8 };
        public static readonly SoundStyle VotiveLoose = new SoundStyle(Folder + "VotiveLoose") { PitchVariance = 0.05f, MaxInstances = 8 };
        public static readonly SoundStyle VotiveHit = new SoundStyle(Folder + "VotiveHit") { PitchVariance = 0.08f, MaxInstances = 6 };
        public static readonly SoundStyle Ignite = new SoundStyle(Folder + "Ignite");
        public static readonly SoundStyle Pyre = new SoundStyle(Folder + "Pyre") { PitchVariance = 0.06f, MaxInstances = 6 };
        public static readonly SoundStyle PyreMark = new SoundStyle(Folder + "PyreMark") { MaxInstances = 6, Volume = 0.8f };
        public static readonly SoundStyle GreatCrescent = new SoundStyle(Folder + "GreatCrescent");
        public static readonly SoundStyle Procession = new SoundStyle(Folder + "Procession") { PitchVariance = 0.07f, MaxInstances = 10 };
        public static readonly SoundStyle GrowBlade = new SoundStyle(Folder + "GrowBlade");
        public static readonly SoundStyle BladeSlam = new SoundStyle(Folder + "BladeSlam");
        public static readonly SoundStyle BladeSweep = new SoundStyle(Folder + "BladeSweep");
        public static readonly SoundStyle SunForm = new SoundStyle(Folder + "SunForm");
        public static readonly SoundStyle SunFall = new SoundStyle(Folder + "SunFall");
        public static readonly SoundStyle SunBurst = new SoundStyle(Folder + "SunBurst");


        /// <summary>A footfall: the boot and the plates ringing after it.</summary>
        public static void Step(Vector2 at) => SoundEngine.PlaySound(StepSound, at);

        public static void Play(SoundStyle style, Vector2 at) => SoundEngine.PlaySound(style, at);
    }
}
