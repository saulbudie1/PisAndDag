using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// Which kind of weapon the copy holds, among the kinds the trainer sorted every weapon into: all
    /// 1179 of the game's, the mod's and Calamity's, clustered by how each is used (r80/weapons/cluster.py: what
    /// it is, how far it reaches, how fast its shot flies, how often it is used, its mana, whether its shot pierces,
    /// is held, goes through blocks). The same numbers are read off any held item here (MimicSense.WeaponOf), so a
    /// weapon from any other mod falls into the kind nearest it, and the copy fights by the numbers trained for that
    /// kind (MimicGenes.For). The kinds:
    ///   0: Melee, reach ~63, use 20t (162 weapons; e.g. Meowmere, Star Wrath, Psycho Knife, Flying Dragon)
    ///   1: Melee, reach ~65, use 20t (47 weapons; e.g. Starlight, Solar Eruption, Flying Knife, Arkhalis)
    ///   2: Yoyo, reach ~260, use 25t (88 weapons; e.g. Terrarian, Shadow Jousting Lance, Sky Dragon's Fury, The Eye of Cthulhu)
    ///   3: Ranged, reach ~960, use 30t, shot 16px/t (96 weapons; e.g. Zenith, Kaleidoscope, Morning Star, Dark Harvest)
    ///   4: Ranged, reach ~960, use 30t, shot 15px/t (65 weapons; e.g. Celebration Mk2, Phantasm, Vortex Beater, Phantom Phoenix)
    ///   5: Ranged, reach ~960, use 6t, shot 22px/t (68 weapons; e.g. Eventide, S.D.M.G., Chain Gun, Elf Melter)
    ///   6: Ranged, reach ~960, use 22t, shot 14px/t (281 weapons; e.g. Daybreak, Stake Launcher, Nail Gun, Snowman Cannon)
    ///   7: Magic, reach ~960, use 24t, mana 20, shot 16px/t (110 weapons; e.g. Stellar Tune, Blizzard Staff, Last Prism, Lunar Flare)
    ///   8: Magic, reach ~960, use 20t, mana 14, shot 16px/t (119 weapons; e.g. Nightglow, Tome of Infinite Wisdom, Nebula Blaze, Sky Fracture)
    ///   9: Summon, reach ~800, use 30t, mana 10 (143 weapons; e.g. Ballista Staff, Rainbow Crystal Staff, Explosive Trap Staff, Staff of the Frost Hydra)
    /// </summary>
    public static class MimicWeapons
    {
        public const int Kinds = 10;

        // Each number's mean and spread over all of them (the kind's own marks are kept as they are).
        private static readonly float[] Mean = { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.033830f, 0.515607f, 3.046150f, 0.381584f, 0.598304f, 0.186599f, 0.383376f };
        private static readonly float[] Spread = { 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.000000f, 1.023653f, 0.447741f, 0.648890f, 0.650842f, 0.425978f, 0.389590f, 0.486210f };

        private static readonly float[,] Centre =
        {
            { 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -1.945699f, -1.151573f, -0.083433f, -0.567324f, 0.385096f, -0.478962f, -0.394928f },
            { 2.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -1.773968f, -1.151573f, -0.013623f, -0.429195f, 0.723229f, 2.087838f, -0.788499f },
            { 0.000000f, 0.886364f, 1.113636f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -0.588990f, -1.151573f, 0.274729f, -0.586293f, 0.942999f, 1.212793f, -0.788499f },
            { 0.000000f, 0.000000f, 0.000000f, 0.208333f, 1.770833f, 0.020833f, 0.000000f, 0.205299f, 0.523466f, 0.458127f, -0.586293f, 0.510171f, -0.478962f, 1.268227f },
            { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.542481f, 0.604906f, 0.141623f, -0.586293f, -0.140482f, 2.087838f, 1.078376f },
            { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.608852f, 0.826769f, -2.104318f, -0.586293f, -0.938487f, -0.365721f, -0.365055f },
            { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.000000f, 0.613414f, 0.649395f, 0.110384f, -0.586293f, -0.629269f, -0.478962f, -0.788499f },
            { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.018182f, 1.981818f, 0.000000f, 0.431902f, 0.685587f, 0.088972f, 1.806920f, 0.012519f, 0.571093f, 1.212135f },
            { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.000000f, 0.549953f, 0.865594f, -0.369025f, 1.570345f, -0.591780f, -0.457392f, -0.753932f },
            { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 2.000000f, 0.480532f, -1.151573f, 0.580401f, 0.538810f, 0.632729f, -0.461012f, 1.268227f }
        };

        private static readonly float[] f = new float[14];

        /// <summary>The kind nearest the weapon as it is read (its kind, reach and shot speed from MimicSense.WeaponOf).</summary>
        public static int ClusterOf(Item item, Weapon w)
        {
            if (item == null || item.IsAir)
                return -1;

            int use = Math.Max(1, item.useTime);
            Projectile sample = item.shoot > 0 && item.shoot < ProjectileLoader.ProjectileCount ? ContentSamples.ProjectilesByType[item.shoot] : null;
            int pen = sample == null || sample.penetrate < 0 ? 10 : Math.Min(sample.penetrate, 10);

            for (int i = 0; i < 7; i++)
                f[i] = (int)w.Kind == i ? 2f : 0f;

            f[7] = (float)Math.Log(Math.Max(w.Reach, 16f) / 64f);
            f[8] = (float)(Math.Log(1 + Math.Max(0f, w.Speed)) / Math.Log(31));
            f[9] = (float)Math.Log(use);
            f[10] = Math.Min(item.mana * 60f / use / 30f, 2f);
            f[11] = pen / 10f;
            f[12] = item.channel ? 1f : 0f;
            f[13] = w.ThroughWalls ? 1f : 0f;

            int best = 0;
            float bestD = float.MaxValue;

            for (int k = 0; k < Kinds; k++)
            {
                float d = 0f;

                for (int i = 0; i < f.Length; i++)
                {
                    float z = (f[i] - Mean[i]) / Spread[i] - Centre[k, i];
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
