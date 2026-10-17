using System;
using log4net;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// A place that is all sea (Subworld.IsSea: the Vast Ocean), wider than the game's own sea
    /// can be. The game calls a column the sea only so far in from either end of a world. Where
    /// it reads WorldGen.beachDistance or oceanDistance for that, or asks WorldGen.oceanDepths,
    /// ExtraLargeOcean answers for the whole width here: the beach the player is on, the sea's
    /// backdrop, its creatures, beach sand not counted for the desert. What follows is the
    /// rest, measured with numbers of the game's own, in 1.4.4:
    ///
    ///   - fishing: the sea's catch, and a Truffle Worm bringing up Duke Fishron, only within
    ///     380 columns of either end (Projectile.FishingCheck_RollItemDrop, Projectile.FishingCheck);
    ///   - Duke Fishron's temper: lost over a player more than 400 columns (6400 pixels) from
    ///     both ends (NPC.AI_069_DukeFishron). What else loses it, a player too near the top of
    ///     the world or gone down past the surface line, is left as it is;
    ///   - the Pirate Map, dropped in Hardmode by what is killed within 380 of either end
    ///     (Conditions.PirateMap.CanDrop).
    ///
    /// Here each of those numbers is answered with the place's whole width (Duke Fishron's with
    /// more than any width), so the whole of it is the sea to them. Everywhere else they are
    /// the game's own. Each is a number in a method too long to be folded into its callers, so
    /// the edit is reached.
    /// </summary>
    public class SubworldSea : ModSystem
    {
        /// <summary>The game's sea, in columns in from either end (fishing, the Pirate Map).</summary>
        private const int SeaColumns = 380;

        /// <summary>Duke Fishron's, in pixels in from either end: 400 columns.</summary>
        private const float FishronReach = 6400f;

        private static ILog log;

        private static bool Sea => Subworld.Current?.IsSea == true;

        public override void Load()
        {
            log = Mod.Logger;

            IL_Projectile.FishingCheck_RollItemDrop += il => Reach(il, 2, 0);
            IL_Projectile.FishingCheck += il => Reach(il, 2, 0);
            IL_Conditions.PirateMap.CanDrop += il => Reach(il, 1, 1);
            IL_NPC.AI_069_DukeFishron += NoTemperOverTheWidth;
        }

        public override void Unload() => log = null;

        private static int Columns(int vanilla) => Sea ? Main.maxTilesX : vanilla;

        private static float ColumnsAsFloat(float vanilla) => Sea ? Main.maxTilesX : vanilla;

        private static float FishronColumns(float vanilla) => Sea ? float.MaxValue : vanilla;

        /// <summary>
        /// Every 380 in the method (as a whole number, and as a float) passed through
        /// <see cref="Columns"/> on its way: "x &lt; 380 || x &gt; maxTilesX - 380" is then
        /// true across the place. The counts are 1.4.4's; a method that has changed is said so
        /// in client.log, and fishes or drops as the game's own there.
        /// </summary>
        private static void Reach(ILContext il, int wholes, int floats)
        {
            ILCursor cursor = new ILCursor(il);
            int foundWholes = 0;
            int foundFloats = 0;

            while (cursor.TryGotoNext(MoveType.After, i => i.MatchLdcI4(SeaColumns)))
            {
                cursor.EmitDelegate<Func<int, int>>(Columns);
                foundWholes++;
            }

            cursor.Index = 0;

            while (cursor.TryGotoNext(MoveType.After, i => i.MatchLdcR4(SeaColumns)))
            {
                cursor.EmitDelegate<Func<float, float>>(ColumnsAsFloat);
                foundFloats++;
            }

            if (foundWholes != wholes || foundFloats != floats)
                log?.Warn($"Subworlds: the sea's 380 columns found {foundWholes} and {foundFloats} times in {il.Method?.Name}, not {wholes} and {floats} as in 1.4.4. In a place that is the sea, that is only the sea near its ends.");
        }

        /// <summary>
        /// Duke Fishron's temper, 1.4.4: "y &lt; 800 || y &gt; worldSurface * 16 || (x &gt; 6400
        /// &amp;&amp; x &lt; maxTilesX * 16 - 6400)". The first 6400 is passed through
        /// <see cref="FishronColumns"/>: here no player is past it, so the last part is never
        /// true and the rest stands.
        /// </summary>
        private static void NoTemperOverTheWidth(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            int found = 0;

            while (cursor.TryGotoNext(MoveType.After, i => i.MatchLdcR4(FishronReach)))
            {
                cursor.EmitDelegate<Func<float, float>>(FishronColumns);
                found++;
            }

            if (found != 1)
                log?.Warn($"Subworlds: Duke Fishron's 400 columns found {found} times, not once as in 1.4.4. In a place that is the sea, he may lose his temper away from its ends.");
        }
    }
}
