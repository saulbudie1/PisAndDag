using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// The distance from the target's edge each weapon did best at when the trainer tried it (r80/run/sweep: every weapon
    /// of the game's, the mod's and Calamity's, at its kind's own distance and at several set ones, in fights against
    /// every boss it knows; r80/weapons/gen_ranges.py). 36 of the 1179 tried do better somewhere other than where
    /// their kind would put them (those the sweep did not reach take what their group of like weapons did best at, where
    /// that was clearly better); the rest, and any weapon from another mod, go by their kind. It is where the copy
    /// starts with a weapon (MimicBrain.Where); it still learns from there (Practice).
    /// </summary>
    public static class MimicWeaponRanges
    {
        private static readonly Dictionary<int, float> Vanilla = new Dictionary<int, float>
        {
            [96] = 350f, // Musket
            [682] = 650f, // Marrow
            [796] = 150f, // TendonBow
            [964] = 150f, // Boomstick
            [1155] = 150f, // WaspGun
            [1446] = 350f, // SpectreStaff
            [1870] = 350f, // RedRyder
            [2515] = 150f, // PalmWoodBow
            [2622] = 650f, // RazorbladeTyphoon
            [2747] = 150f, // BorealWoodBow
            [2797] = 150f, // Xenopopper
            [3008] = 150f, // DartRifle
            [3105] = 350f, // ToxicFlask
            [3498] = 150f, // TinBow
            [3788] = 150f, // OnyxBlaster
            [4952] = 350f, // FairyQueenMagicItem
        };

        private static readonly Dictionary<string, float> Modded = new Dictionary<string, float>
        {
            ["CalamityMod/AethersWhisper"] = 150f,
            ["CalamityMod/ConferenceCall"] = 350f,
            ["CalamityMod/ContinentalGreatbow"] = 650f,
            ["CalamityMod/CorinthPrime"] = 150f,
            ["CalamityMod/DeadSunsWind"] = 150f,
            ["CalamityMod/DeathsAscension"] = 650f,
            ["CalamityMod/Downpour"] = 350f,
            ["CalamityMod/EidolonStaff"] = 350f,
            ["CalamityMod/FishboneBoomerang"] = 150f,
            ["CalamityMod/HalibutCannon"] = 350f,
            ["CalamityMod/HandheldTank"] = 650f,
            ["CalamityMod/Hydra"] = 350f,
            ["CalamityMod/NitroExpressRifle"] = 350f,
            ["CalamityMod/PearlGod"] = 150f,
            ["CalamityMod/PolarisParrotfish"] = 150f,
            ["CalamityMod/SandstreamScepter"] = 350f,
            ["CalamityMod/SoulHarvester"] = 650f,
            ["CalamityMod/SystemBane"] = 150f,
            ["CalamityMod/WavePounder"] = 150f,
            ["PisAndDag/DentedMegashark"] = 350f,
        };

        /// <summary>The distance found for this weapon, in pixels from the target's edge; 0 when there is none.</summary>
        public static float BestOf(Item item)
        {
            if (item == null || item.IsAir)
                return 0f;

            if (item.ModItem == null)
                return item.type < ItemID.Count && Vanilla.TryGetValue(item.type, out float v) ? v : 0f;

            return Modded.TryGetValue(item.ModItem.Mod.Name + "/" + item.ModItem.Name, out float m) ? m : 0f;
        }
    }
}
