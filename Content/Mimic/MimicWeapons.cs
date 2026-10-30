using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// Which kind of weapon the copy holds (round 81, by request: "unsupervised training to cluster items (4 classes
    /// melee ranged magic summoner)"). Every weapon of the game's, this mod's and Calamity's (1179 of them,
    /// Claude outputs\MimicTrainer_r81\weapons\cluster81.py) was read as the copy reads a held item here
    /// (<see cref="Features"/>: how it is used, by what its shot does; its reach, its shot's speed, its use time, mana, pierce,
    /// channel, through walls, a right click, auto reuse) and, inside each of the four classes, sorted without labels by
    /// k-means, as many kinds as the silhouette said parted best. Any weapon of any mod falls into the kind of its class whose
    /// middle it is nearest, and the copy fights by that kind's trained numbers (MimicGenes.For).
    ///
    /// What a shot does is read from its own code: a shot that makes itself its holder's held projectile (it stores
    /// Player.heldProj: <see cref="ShotIsHeld"/>) is held out; any other flies off. (Round 80 took any melee weapon that hides
    /// itself and shoots a mod's shot for a held-out blade: Triactis' hammer, Galaxy Smasher, Stellar Contempt, Pwnagehammer
    /// and the knives were walked up to 141 px of a boss before they were thrown.) And what its shots really do is watched
    /// as the copy uses it (<see cref="Watched"/>, MimicSystem): how far they fly, whether they stay in the hand, how fast
    /// they fall. From the first few, the copy uses the weapon by those.
    ///
    /// The kinds:
    ///   0: Melee / Swing: reach ~56, use 19t (101; e.g. Psycho Knife, Fetid Baghnakhs, The Horseman's Blade, Keybrand, Blade Of Mors)
    ///   1: Melee / HeldBlade: reach ~64, use 20t (33; e.g. Starlight, Solar Eruption, Arkhalis, Terragrim, Murasama)
    ///   2: Melee / HeldBlade (+Thrown, Swing): reach ~85, use 22t, 26 with right click (26; e.g. Foigh's Rust Side Sword, Sword Of A Gilded Knight, Photon Ripper, Ark of the Cosmos, Galileo Gladius)
    ///   3: Melee / Spear: reach ~188, use 24t (38; e.g. Shadow Jousting Lance, Sky Dragon's Fury, Hallowed Jousting Lance, Obsidian Swordfish, Nadir)
    ///   4: Melee / Yoyo: reach ~296, use 25t (49; e.g. Terrarian, The Eye of Cthulhu, Kraken, Red's Throw, Pocket Sandstorm)
    ///   5: Melee / Thrown: reach ~960, use 20t, shot 16px/t, 1 with right click (44; e.g. Zenith, Daybreak, Paladin's Hammer, Possessed Hatchet, Defacer)
    ///   6: Melee / SwingShot: reach ~640, use 24t, shot 17px/t, 1 with right click (31; e.g. Star Wrath, Flying Dragon, Death Sickle, True Night's Edge, Orderbringer)
    ///   7: Melee / SwingShot: reach ~640, use 24t, shot 12px/t, 3 with right click (31; e.g. Meowmere, Influx Waver, Terra Blade, Golem Fist, Terror Blade)
    ///   8: Ranged / Thrown: reach ~960, use 20t, shot 16px/t (86; e.g. Bone Throwing Knife, Snowball Cannon, Poisoned Knife, Bone Javelin, She Who Sings)
    ///   9: Ranged / Thrown (+Shot, HeldShot): reach ~960, use 25t, shot 28px/t (50; e.g. Scarlet Devil, Executioner's Blade, Wrathwing, Ghoulish Gouger)
    ///   10: Ranged / Thrown: reach ~960, use 20t, shot 8px/t (25; e.g. Bone, Bouncy Grenade, Happy Grenade, Grenade, Meat Hook)
    ///   11: Ranged / Shot: reach ~960, use 27t, shot 8px/t (45; e.g. Electrosphere Launcher, Sniper Rifle, Phoenix Blaster, Handgun, Handswitch)
    ///   12: Ranged / Shot (+Thrown, HeldShot): reach ~960, use 19t, shot 25px/t, 24 with right click (24; e.g. The Last Grain, Sacrifice, Septic Skewer, Vega, Pristine Fury)
    ///   13: Ranged / Shot: reach ~960, use 18t, shot 16px/t (136; e.g. Eventide, S.D.M.G., Chain Gun, Elf Melter, Dented Megashark)
    ///   14: Ranged / HeldShot (+Shot, Thrown): reach ~360, use 22t, shot 3px/t (14; e.g. The Final Dawn, Kingsbane, Seadragon, Karasawa)
    ///   15: Ranged / HeldShot: reach ~960, use 30t, shot 15px/t, 9 with right click (57; e.g. Celebration Mk2, Phantasm, Vortex Beater, Phantom Phoenix, Voidragon)
    ///   16: Magic / Shot (+Thrown, Spray): reach ~960, use 22t, shot 18px/t, 10 with right click (188; e.g. Nightglow, Stellar Tune, Tome of Infinite Wisdom, Blizzard Staff, Dancing Sand)
    ///   17: Magic / HeldShot (+Shot, Spray): reach ~960, use 20t, shot 12px/t, 7 with right click (45; e.g. Last Prism, Charged Blaster Cannon, Laser Machinegun, Medusa Head, Betsy's Flames)
    ///   18: Summon / Whip (+HeldShot): reach ~160, use 30t (11; e.g. Kaleidoscope, Morning Star, Dark Harvest, Durendal, Joint Arms Of The Defiled)
    ///   19: Summon / Minion: reach ~800, use 36t, 2 with right click (108; e.g. Terraprisma, Raven Staff, Stardust Cell Staff, Pygmy Staff, Bone Squad)
    ///   20: Summon / Sentry (+Shot, Minion): reach ~800, use 30t (37; e.g. Ballista Staff, Rainbow Crystal Staff, Explosive Trap Staff, Staff of the Frost Hydra, Universe Splitter)
    /// </summary>
    public static class MimicWeapons
    {
        public const int Kinds = 21;

        /// <summary>The four classes (<see cref="ClassOf"/>).</summary>
        public static readonly string[] ClassNames = { "melee", "ranged", "magic", "summon" };

        /// <summary>How a weapon is used, by what it does (<see cref="Style"/>).</summary>
        public enum Styles { Swing, HeldBlade, Spear, Yoyo, Whip, Thrown, SwingShot, Shot, HeldShot, Spray, Minion, Sentry }

        public static readonly string[] StyleNames = { "a swung blade", "a held-out blade", "a spear", "a yoyo or flail", "a whip", "thrown", "a sword that shoots", "a shooter", "a held-out shooter", "a spray", "a minion", "a sentry" };

        private const int StyleCount = 12;
        private const int FeatureCount = StyleCount + 9;

        /// <summary>Each class's first kind and how many it has.</summary>
        private static readonly int[] ClassFirst = { 0, 8, 16, 18 };
        private static readonly int[] ClassKinds = { 8, 8, 2, 3 };

        /// <summary>Each kind's name, for the /mirror report.</summary>
        public static readonly string[] KindNames =
        {
            "Melee / Swing: reach ~56, use 19t",
            "Melee / HeldBlade: reach ~64, use 20t",
            "Melee / HeldBlade (+Thrown, Swing): reach ~85, use 22t, 26 with right click",
            "Melee / Spear: reach ~188, use 24t",
            "Melee / Yoyo: reach ~296, use 25t",
            "Melee / Thrown: reach ~960, use 20t, shot 16px/t, 1 with right click",
            "Melee / SwingShot: reach ~640, use 24t, shot 17px/t, 1 with right click",
            "Melee / SwingShot: reach ~640, use 24t, shot 12px/t, 3 with right click",
            "Ranged / Thrown: reach ~960, use 20t, shot 16px/t",
            "Ranged / Thrown (+Shot, HeldShot): reach ~960, use 25t, shot 28px/t",
            "Ranged / Thrown: reach ~960, use 20t, shot 8px/t",
            "Ranged / Shot: reach ~960, use 27t, shot 8px/t",
            "Ranged / Shot (+Thrown, HeldShot): reach ~960, use 19t, shot 25px/t, 24 with right click",
            "Ranged / Shot: reach ~960, use 18t, shot 16px/t",
            "Ranged / HeldShot (+Shot, Thrown): reach ~360, use 22t, shot 3px/t",
            "Ranged / HeldShot: reach ~960, use 30t, shot 15px/t, 9 with right click",
            "Magic / Shot (+Thrown, Spray): reach ~960, use 22t, shot 18px/t, 10 with right click",
            "Magic / HeldShot (+Shot, Spray): reach ~960, use 20t, shot 12px/t, 7 with right click",
            "Summon / Whip (+HeldShot): reach ~160, use 30t",
            "Summon / Minion: reach ~800, use 36t, 2 with right click",
            "Summon / Sentry (+Shot, Minion): reach ~800, use 30t",
        };

        // Each class's numbers' means and spreads (the style marks are kept as they are: 2 for its style).
        private static readonly float[][] Mean =
        {
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 1.082480f, 0.251367f, 3.064907f, 0.000000f, 0.862040f, 0.286119f, 0.141643f, 0.131728f, 0.614731f },
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.640829f, 0.824573f, 2.940994f, 0.000000f, 0.356522f, 0.153318f, 0.288330f, 0.113272f, 0.800915f },
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.515606f, 0.853778f, 2.968597f, 1.470466f, 0.472103f, 0.210300f, 0.480687f, 0.109442f, 0.849785f },
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.416011f, 0.010159f, 3.414110f, 0.687624f, 0.867308f, 0.019231f, 1.000000f, 0.019231f, 0.679487f },
        };

        private static readonly float[][] Spread =
        {
            new[] { 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.097254f, 0.393868f, 0.451632f, 0.100000f, 0.303947f, 0.451947f, 0.348685f, 0.424548f, 0.486660f },
            new[] { 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 0.220201f, 0.185244f, 0.745720f, 0.100000f, 0.380816f, 0.360295f, 0.452986f, 0.396331f, 0.399313f },
            new[] { 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 0.455903f, 0.331462f, 0.790454f, 0.547866f, 0.398327f, 0.407523f, 0.499628f, 0.390111f, 0.357283f },
            new[] { 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 0.408353f, 0.100000f, 0.231346f, 0.388800f, 0.319087f, 0.137336f, 0.100000f, 0.168750f, 0.466675f },
        };

        /// <summary>Each kind's middle, in its class's standardised numbers.</summary>
        private static readonly float[][] Centre =
        {
            new[] { 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -1.114519f, -0.638202f, -0.353117f, 0.000000f, 0.317082f, -0.611174f, -0.406221f, -0.310279f, -0.225579f }, // 0
            new[] { 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -0.769260f, -0.638202f, 0.077918f, 0.000000f, 0.364167f, 0.909069f, -0.406221f, -0.310279f, 0.293521f }, // 1
            new[] { 0.076923f, 1.384615f, 0.000000f, 0.000000f, 0.000000f, 0.538462f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -0.573714f, -0.478460f, -0.082309f, 0.000000f, 0.352663f, 0.303040f, 0.255607f, 3.222894f, 0.317470f }, // 2
            new[] { 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.171051f, -0.638202f, 0.228138f, 0.000000f, 0.453896f, -0.167260f, -0.406221f, -0.310279f, -0.343900f }, // 3
            new[] { 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.419535f, -0.638202f, 0.470588f, 0.000000f, 0.453896f, 1.579569f, -0.406221f, -0.310279f, -0.550265f }, // 4
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 1.344199f, 1.485825f, -0.196761f, 0.000000f, -0.174204f, -0.180494f, 0.441119f, -0.229979f, 0.091152f }, // 5
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 1.069310f, 1.584092f, 0.087745f, 0.000000f, -1.053157f, -0.633081f, 2.461700f, -0.196305f, 0.659091f }, // 6
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 1.081230f, 1.258048f, 0.304609f, 0.000000f, -1.689939f, -0.633081f, -0.406221f, 0.031641f, 0.659091f }, // 7
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.305274f, 0.113405f, 0.022682f, 0.000000f, 0.095851f, -0.425535f, -0.636508f, -0.285802f, 0.498568f }, // 8
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 1.720000f, 0.000000f, 0.240000f, 0.040000f, 0.000000f, 0.000000f, 0.000000f, 0.219897f, 0.837319f, 0.240798f, 0.000000f, 0.838930f, -0.425535f, 1.571064f, -0.285802f, 0.498568f }, // 9
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -0.366631f, -0.949812f, 0.173105f, 0.000000f, 0.135179f, -0.425535f, -0.371600f, -0.285802f, -2.005734f }, // 10
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -0.088492f, -0.570305f, 0.384348f, 0.000000f, -0.510218f, -0.363857f, -0.636508f, -0.285802f, -2.005734f }, // 11
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.416667f, 0.000000f, 1.416667f, 0.166667f, 0.000000f, 0.000000f, 0.000000f, 0.216339f, 0.621704f, -0.188966f, 0.000000f, -0.049950f, -0.309889f, -0.084615f, 3.498909f, 0.289876f }, // 12
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.258760f, 0.175216f, -0.292965f, 0.000000f, -0.399431f, -0.364311f, -0.587812f, -0.285802f, 0.498568f }, // 13
            new[] { 0.000000f, 0.000000f, 0.142857f, 0.000000f, 0.000000f, 0.285714f, 0.000000f, 0.571429f, 1.000000f, 0.000000f, 0.000000f, 0.000000f, -4.640826f, -2.430707f, -0.190535f, 0.000000f, 0.020388f, 0.763967f, 0.624962f, -0.285802f, 0.319689f }, // 14
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.008554f, -0.121583f, 0.200564f, 0.000000f, 0.432048f, 2.301277f, 1.532335f, 0.311784f, -0.116524f }, // 15
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.074468f, 0.000000f, 1.914894f, 0.000000f, 0.010638f, 0.000000f, 0.000000f, 0.081076f, 0.116560f, -0.028543f, -0.010102f, -0.112911f, -0.437731f, -0.248793f, -0.076016f, 0.092905f }, // 16
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.177778f, 1.777778f, 0.044444f, 0.000000f, 0.000000f, -0.338718f, -0.486962f, 0.119248f, 0.042206f, 0.471715f, 1.828744f, 1.039400f, 0.317580f, -0.388136f }, // 17
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 1.818182f, 0.000000f, 0.000000f, 0.000000f, 0.181818f, 0.000000f, 0.000000f, 0.000000f, -3.622926f, -0.101594f, 0.068466f, -1.638681f, -0.096977f, 0.521919f, 0.000000f, -0.113960f, -1.261217f }, // 18
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.268684f, -0.101594f, 0.111603f, 0.042480f, 0.259153f, -0.072607f, 0.000000f, 0.050649f, 0.289983f }, // 19
            new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.108108f, 0.000000f, 0.000000f, 0.054054f, 1.837838f, 0.292818f, 0.326749f, -0.346114f, 0.363181f, -0.727615f, 0.056768f, 0.000000f, -0.113960f, -0.471480f }, // 20
        };

        // ---- the class -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Its class: 3 summon (whips with it), 2 magic, 0 melee (true melee with it), 1 ranged (with rogue and thrown, and
        /// anything else that shoots); one of no class by how it is used.
        /// </summary>
        public static int ClassOf(Item item, Styles style)
        {
            if (item.CountsAsClass(DamageClass.Summon) || item.CountsAsClass(DamageClass.SummonMeleeSpeed))
                return 3;

            if (item.CountsAsClass(DamageClass.Magic))
                return 2;

            if (item.CountsAsClass(DamageClass.Melee))
                return 0;

            if (item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Throwing) || Rogue(item))
                return 1;

            switch (style)
            {
                case Styles.Minion:
                case Styles.Sentry:
                case Styles.Whip:
                    return 3;
                case Styles.Swing:
                case Styles.HeldBlade:
                case Styles.Spear:
                case Styles.Yoyo:
                    return 0;
                default:
                    return 1;
            }
        }

        /// <summary>Calamity's rogue (by its class's name: the mod does not depend on Calamity).</summary>
        public static bool Rogue(Item item) => item.DamageType != null && item.DamageType.Name.Contains("Rogue");

        // ---- what a shot does ------------------------------------------------------------------------------------------

        /// <summary>The game's own shots that are held out (drills and chainsaws, and the held weapons: Last Prism, Phantasm, Starlight...).</summary>
        private static bool VanillaHeld(int aiStyle) => aiStyle == 20 || aiStyle == 75;

        private static readonly Dictionary<int, bool> held = new Dictionary<int, bool>();
        private static Dictionary<short, OpCode> opcodes;
        private static FieldInfo heldProjField;

        /// <summary>
        /// Whether a kind of shot is held out: one of the game's held weapons' (<see cref="VanillaHeld"/>), or a mod's whose
        /// code (its class's and every class it is made from, up to ModProjectile) stores Player.heldProj, as every held-out
        /// projectile does to be drawn in its holder's hands. Read once for each kind of shot and kept.
        /// </summary>
        public static bool ShotIsHeld(int type)
        {
            if (type <= 0 || type >= ProjectileLoader.ProjectileCount)
                return false;

            if (held.TryGetValue(type, out bool known))
                return known;

            bool result;

            if (type < ProjectileID.Count)
            {
                result = VanillaHeld(ContentSamples.ProjectilesByType[type].aiStyle);
            }
            else
            {
                ModProjectile mp = ProjectileLoader.GetProjectile(type);
                result = mp != null && StoresHeldProj(mp.GetType());
            }

            held[type] = result;
            return result;
        }

        private static bool StoresHeldProj(Type t)
        {
            heldProjField ??= typeof(Player).GetField(nameof(Player.heldProj));

            if (heldProjField == null)
                return false;

            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (; t != null && t != typeof(ModProjectile) && t != typeof(object); t = t.BaseType)
            {
                foreach (MethodInfo m in t.GetMethods(all))
                {
                    if (Stores(m, heldProjField))
                        return true;
                }

                // Its lambdas and their closures are compiled into types nested in it.
                foreach (Type nested in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (nested.IsGenericTypeDefinition)
                        continue;

                    foreach (MethodInfo m in nested.GetMethods(all))
                    {
                        if (Stores(m, heldProjField))
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether a method's body stores a field (stfld or stsfld with it), walking its IL by the opcodes' own sizes.</summary>
        private static bool Stores(MethodInfo m, FieldInfo field)
        {
            byte[] il;

            try
            {
                il = m.GetMethodBody()?.GetILAsByteArray();
            }
            catch (Exception)
            {
                return false;
            }

            if (il == null)
                return false;

            if (opcodes == null)
            {
                opcodes = new Dictionary<short, OpCode>();

                foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (f.GetValue(null) is OpCode op)
                        opcodes[op.Value] = op;
                }
            }

            Module module = m.Module;
            Type[] typeArgs = m.DeclaringType != null && m.DeclaringType.IsGenericType ? m.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = m.IsGenericMethod ? m.GetGenericArguments() : null;
            int i = 0;

            while (i < il.Length)
            {
                short value = il[i];

                if (value == 0xFE && i + 1 < il.Length)
                {
                    value = (short)(0xFE00 | il[i + 1]);
                    i += 2;
                }
                else
                {
                    i += 1;
                }

                if (!opcodes.TryGetValue(value, out OpCode op))
                    return false;

                int size = OperandSize(op.OperandType, il, i);

                if (size < 0 || size > il.Length - i)
                    return false;

                if ((op == OpCodes.Stfld || op == OpCodes.Stsfld) && i + 4 <= il.Length)
                {
                    int token = BitConverter.ToInt32(il, i);

                    try
                    {
                        if (module.ResolveField(token, typeArgs, methodArgs) == field)
                            return true;
                    }
                    catch (Exception)
                    {
                    }
                }

                i += size;
            }

            return false;
        }

        private static int OperandSize(OperandType t, byte[] il, int at)
        {
            switch (t)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    if (at + 4 > il.Length)
                        return -1;

                    long n = BitConverter.ToInt32(il, at);
                    return n < 0 || n > il.Length ? -1 : (int)(4 + 4 * n);
                default:
                    return 4;
            }
        }

        /// <summary>
        /// Whether a melee weapon is a blade held out and swung by a shot of its own (Ark of the Elements, Murasama,
        /// Exoblade, Arkhalis): a placeholder shot (the item's code swings its own), a shot that holds itself
        /// (<see cref="ShotIsHeld"/>), or one the copy has seen held (<see cref="SeenHeld"/>).
        /// </summary>
        public static bool HeldOut(Item item, Projectile sample)
        {
            if (sample == null || !item.CountsAsClass(DamageClass.Melee))
                return false;

            return item.shoot == ProjectileID.PurificationPowder || ShotIsHeld(item.shoot) || Seen(item.type, out Watch w) && w.Held;
        }

        /// <summary>
        /// Items seen held out once the copy used them (its held projectile was the item's shot): a held-out blade or a
        /// held-out shooter whatever its numbers say, from then on.
        /// </summary>
        public static readonly HashSet<int> SeenHeld = new HashSet<int>();

        // ---- what its shots were seen to do (MimicSystem watches the copy's own) -------------------------------------------

        /// <summary>What the copy's own shots from a weapon were seen to do.</summary>
        public sealed class Watch
        {
            /// <summary>Shots seen out to their end.</summary>
            public int Shots;

            /// <summary>Of those, how many were held (the copy's held projectile) at some point.</summary>
            public int HeldShots;

            /// <summary>
            /// The furthest each flew from the copy before it ended of itself (ran out, struck a block), eased over the last
            /// ones; and how many such. A shot that hit something says only where that was, and is not counted.
            /// </summary>
            public float Far;
            public int Flown;

            /// <summary>What they fall by, pixels a tick each tick (eased over the shots that fell), and the ticks each flew straight first.</summary>
            public float Gravity;
            public int FallAfter;

            /// <summary>Seen often enough to go by: three shots.</summary>
            public bool Sure => Shots >= 3;

            /// <summary>Held out: most of its shots stayed in its hands.</summary>
            public bool Held => Sure && HeldShots * 2 > Shots;
        }

        private static readonly Dictionary<int, Watch> watched = new Dictionary<int, Watch>();

        public static bool Seen(int itemType, out Watch w) => watched.TryGetValue(itemType, out w) && w.Sure;

        /// <summary>A shot of the copy's, from the item it held when the shot was made, seen out to its end (MimicShots).</summary>
        public static void Watched(int itemType, float far, bool flown, bool wasHeld, float gravity, bool fell, int fallAfter)
        {
            if (!watched.TryGetValue(itemType, out Watch w))
                watched[itemType] = w = new Watch();

            if (flown)
            {
                w.Far = w.Flown == 0 ? far : w.Far + (far - w.Far) * 0.25f;
                w.Flown++;
            }

            if (fell)
            {
                w.Gravity = w.Gravity <= 0f ? gravity : w.Gravity + (gravity - w.Gravity) * 0.25f;
                w.FallAfter = fallAfter;
            }

            w.Shots++;

            if (wasHeld)
            {
                w.HeldShots++;

                if (w.Held)
                    SeenHeld.Add(itemType);
            }
        }

        /// <summary>How a weapon's shots were seen, for the /mirror report.</summary>
        public static string WatchReport(int itemType)
        {
            if (!watched.TryGetValue(itemType, out Watch w) || w.Shots == 0)
                return "";

            return w.Shots + " shots watched: " + (w.Held ? "held" : w.Flown > 0 ? "fly " + (int)w.Far + " px" : "fly")
                + (w.Gravity > 0.005f ? ", fall " + w.Gravity.ToString("0.00") + " px/t²" + (w.FallAfter > 0 ? " after " + w.FallAfter + " ticks" : "") : "");
        }

        // ---- how it is used ----------------------------------------------------------------------------------------------

        /// <summary>How the weapon is used (as cluster81.py's read): by its kind as read (MimicSense), and what its shot does.</summary>
        public static Styles Style(Item item, Weapon w)
        {
            Projectile sample = item.shoot > 0 && item.shoot < ProjectileLoader.ProjectileCount ? ContentSamples.ProjectilesByType[item.shoot] : null;

            switch (w.Kind)
            {
                case WeaponKind.Whip: return Styles.Whip;
                case WeaponKind.Spear: return Styles.Spear;
                case WeaponKind.Yoyo: return Styles.Yoyo;
                case WeaponKind.Summon: return sample != null && sample.sentry ? Styles.Sentry : Styles.Minion;
                case WeaponKind.Melee: return w.HeldOut ? Styles.HeldBlade : Styles.Swing;
            }

            // A melee weapon whose shot flies (one held out was read as a blade, above): thrown if it hides itself (a hammer,
            // a boomerang, knives), else a sword that shoots as it swings.
            if (item.CountsAsClass(DamageClass.Melee))
                return item.noUseGraphic ? Styles.Thrown : Styles.SwingShot;

            bool heldShot = SeenHeld.Contains(item.type) || ShotIsHeld(item.shoot) || item.noUseGraphic && item.channel;

            if (w.Speed <= 0f && !heldShot)
                return Styles.Spray;

            if (heldShot)
                return Styles.HeldShot;

            if (sample != null && (sample.aiStyle == 2 || sample.aiStyle == 3) || item.consumable || item.CountsAsClass(DamageClass.Throwing) || Rogue(item) || item.noUseGraphic)
                return Styles.Thrown;

            int tl = sample?.timeLeft ?? 3600;

            if (tl * w.Speed < 700f && item.useTime <= 10)
                return Styles.Spray;

            return Styles.Shot;
        }

        private static readonly float[] f = new float[FeatureCount];

        /// <summary>The numbers a weapon is sorted by (cluster81.py's features), from its style and what it was read as.</summary>
        public static float[] Features(Item item, Weapon w, Styles style)
        {
            Projectile sample = item.shoot > 0 && item.shoot < ProjectileLoader.ProjectileCount ? ContentSamples.ProjectilesByType[item.shoot] : null;
            int use = Math.Max(1, item.useTime);
            int pen = sample == null || sample.penetrate < 0 ? 10 : Math.Min(sample.penetrate, 10);

            for (int i = 0; i < StyleCount; i++)
                f[i] = i == (int)style ? 2f : 0f;

            f[StyleCount + 0] = (float)Math.Log(Math.Max(w.Reach, 16f) / 64f);
            f[StyleCount + 1] = (float)(Math.Log(1 + Math.Max(0f, w.Speed)) / Math.Log(31));
            f[StyleCount + 2] = (float)Math.Log(use);
            f[StyleCount + 3] = Math.Min(item.mana * 60f / use / 30f, 2f);
            f[StyleCount + 4] = pen / 10f;
            f[StyleCount + 5] = item.channel ? 1f : 0f;
            f[StyleCount + 6] = w.ThroughWalls ? 1f : 0f;
            f[StyleCount + 7] = w.HasAlt ? 1.5f : 0f;
            f[StyleCount + 8] = item.autoReuse ? 1f : 0f;
            return f;
        }

        /// <summary>The kind of the weapon as it is read (MimicSense.WeaponOf): the nearest middle of its class's kinds.</summary>
        public static int ClusterOf(Item item, Weapon w)
        {
            if (item == null || item.IsAir)
                return -1;

            Styles style = (Styles)w.Style;
            int c = Math.Clamp(w.Class, 0, 3);
            float[] x = Features(item, w, style);
            int best = -1;
            float bestD = float.MaxValue;

            for (int k = ClassFirst[c]; k < ClassFirst[c] + ClassKinds[c]; k++)
            {
                float d = 0f;

                for (int i = 0; i < FeatureCount; i++)
                {
                    float z = (x[i] - Mean[c][i]) / Spread[c][i] - Centre[k][i];
                    d += z * z;
                }

                if (d < bestD)
                {
                    bestD = d;
                    best = k;
                }
            }

            return best;
        }
    }
}
