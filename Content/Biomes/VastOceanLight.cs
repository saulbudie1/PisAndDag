using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// Light in the Vast Ocean's depths, by request ("after section 2, any lightsource spread is
    /// limited and is decreased by half, after section 4 any lightsource only light itslef and
    /// not surrounding it (lightsticks and hooks and projectiles), it is pitch dark").
    ///
    /// The game spreads light from cell to cell, a little less with every cell it passes, by what
    /// the cell is: air, solid, water or honey (LightMap.BlurLine). What each cell is, it is told
    /// as the tiles are looked at (TileLightScanner.ExportTo); how much less each lets through, just
    /// before the light is spread (LightingEngine.UpdateLightDecay). Here water is told apart by
    /// its depth (VastOcean's sections):
    ///   - from the 3rd section down it lets light through as two cells of water do, so a light
    ///     reaches half as far (it is counted as honey, and honey is let through as water twice
    ///     over, for that turn of the light);
    ///   - from the 5th down it lets none through at all (counted as solid, and solid lets
    ///     nothing through, for that turn): a light lights the cell it is in and no other. A
    ///     torch held, a glowstick, a hook, a shot, a jellyfish: each is a point in the dark.
    /// Nothing is under the game's surface line to light it besides (no sky there), so the rest
    /// is pitch dark. Over the 3rd section, and in air anywhere, light is the game's own.
    ///
    /// Only for the game's Color and White lighting; its two older ones (Retro, Trippy) are
    /// another engine, left as they are.
    ///
    /// And one thing more, for the sea drawn behind (VastOceanSky): the game blacks out, over
    /// its surface line, full water that no light reaches at all (Main.DrawBlack: no wall, not
    /// lit in the least), which would lay a band of black across the top of the 1st section
    /// where the sun's light gives out, forty-odd rows down. The water there is given the
    /// faintest light there is (<see cref="Faintest"/>, less than the game carries on to the
    /// next cell), so none of it is ever quite unlit.
    ///
    /// And the black behind what no light reaches (<see cref="Blacken"/>), by request ("btw the
    /// tile under the lighthouse still tweaking"). The game does not draw a block that no light
    /// reaches at all (TileDrawing.DrawSingleTile), nor such a wall (WallDrawing, but in a
    /// world's bottom rows), and what shows there is what is behind: under its surface line the
    /// black it fills the world with behind everything (Main.DrawBackgroundBlackFill); over it,
    /// the black it lays behind what it takes to be too dark to see (Main.DrawBlack, which it
    /// lays over its surface line only, and in a world's bottom rows). In the Vast Ocean the sea
    /// is drawn over that fill (VastOceanSky), so under the surface line an unlit block was not
    /// drawn and the sea showed through it, brighter than the water about it (a block has no
    /// water drawn over it): the island's rock where it hangs under the surface line, under the
    /// lighthouse. Where the light the water carries in gives out along the rock, and where the
    /// lamp's (M.U.P.R.A.L, HEATR) comes and goes about its wearer, the rock went from black (lit
    /// the least bit, so drawn, and dark) to that blue and back, a tile at a time. In the Vast
    /// Ocean the game lays its black over the whole screen now, as it would over its surface line:
    /// what no light reaches is black wherever it is, the island's rock, a cave's walls; open
    /// water it never blacks under its surface line, so the sea shows there as before. (Not under
    /// the game's two old lightings when it draws straight to the screen: there it would black
    /// the open sea as well, so it is left as it is.)
    /// </summary>
    public class VastOceanLight : ModSystem
    {
        /// <summary>The engine's map being worked on (private in 1.4.4).</summary>
        private static FieldInfo workingMap;

        /// <summary>The faintest light: about two of the game's least steps of brightness (1/255, at its brightness of 1.2), far under what it lets spread (0.0185).</summary>
        private static readonly Vector3 Faintest = new Vector3(0.002f, 0.006f, 0.012f);

        /// <summary>What the last look at the tiles found: water deep enough to halve, or to keep a light to its own cell.</summary>
        private static bool halved;
        private static bool ownOnly;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            workingMap = typeof(LightingEngine).GetField("_workingLightMap", BindingFlags.NonPublic | BindingFlags.Instance);

            if (workingMap == null)
            {
                Mod.Logger.Warn("Vast Ocean: LightingEngine._workingLightMap is not there. Light in the depths will be the game's own.");
                return;
            }

            On_TileLightScanner.ExportTo += Masks;
            On_LightingEngine.UpdateLightDecay += Decay;
            On_Main.DrawBlack += Blacken;
        }

        public override void Unload()
        {
            workingMap = null;
        }

        /// <summary>After the game has said what every cell is: deep water said to be honey (halved) or solid (its own cell only).</summary>
        private static void Masks(On_TileLightScanner.orig_ExportTo orig, TileLightScanner self, Rectangle area, LightMap map, TileLightScannerOptions options)
        {
            orig(self, area, map, options);

            halved = false;
            ownOnly = false;

            if (SubworldKeeper.Current is not VastOcean)
                return;

            int to = area.Y + area.Height;

            // The water over the game's surface line, never quite unlit.
            for (int y = Math.Max(area.Y, VastOcean.Surface + 1); y < Math.Min(to, (int)Main.worldSurface + 1); y++)
            {
                int my = y - area.Y;

                for (int mx = 0; mx < area.Width; mx++)
                {
                    if (map.GetMask(mx, my) == LightMaskMode.Water)
                        map[mx, my] = Vector3.Max(map[mx, my], Faintest);
                }
            }

            for (int y = Math.Max(area.Y, VastOcean.HalvedLightFrom); y < to; y++)
            {
                bool own = y >= VastOcean.OwnLightFrom;
                LightMaskMode deep = own ? LightMaskMode.Solid : LightMaskMode.Honey;
                int my = y - area.Y;

                for (int mx = 0; mx < area.Width; mx++)
                {
                    if (map.GetMask(mx, my) != LightMaskMode.Water)
                        continue;

                    map.SetMaskAt(mx, my, deep);

                    if (own)
                        ownOnly = true;
                    else
                        halved = true;
                }
            }
        }

        /// <summary>
        /// The game's black behind what it takes to be too dark to see (Main.DrawBlack), in the
        /// Vast Ocean over every row of the screen: told to (its argument), as the game tells it
        /// for the pictures its camera takes, rather than over its surface line only, or the
        /// bottom rows. What it blacks is its own choice, cell by cell as ever: a block, or a cell
        /// not full of water, lit too little to see (not one seen through glass); full water only
        /// lit not at all, and with no wall in it only over its surface line. Not when it draws
        /// straight to the screen (the game's two old lightings, drawn every frame): then it would
        /// black full water under its surface line as well.
        /// </summary>
        private static void Blacken(On_Main.orig_DrawBlack orig, Main self, bool force)
        {
            orig(self, force || (!Main.drawToScreen && SubworldKeeper.Current is VastOcean));
        }

        /// <summary>After the game has set how much each kind of cell lets through: honey as water twice over, solid as nothing.</summary>
        private static void Decay(On_LightingEngine.orig_UpdateLightDecay orig, LightingEngine self)
        {
            orig(self);

            if (SubworldKeeper.Current is not VastOcean || workingMap.GetValue(self) is not LightMap map)
                return;

            if (halved)
            {
                // The game's water also flickers each cell by 0.98 to 1 (LightMap.BlurLine), 0.99
                // on average: twice over with that, a light reaches half as far.
                Vector3 water = map.LightDecayThroughWater * 0.99f;

                map.LightDecayThroughHoney = water * water;
            }

            if (ownOnly)
                map.LightDecayThroughSolid = 0f;
        }
    }
}
