using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Map;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A blow that goes into the ground and out through it. From the edges of whatever hit,
    /// the floor heaves up tile by tile in a wave that runs outward both ways: the ground
    /// beside the blow goes up at once and highest, and each tile further out goes a little
    /// later and a little lower, the wave slowing as it spreads, until it dies out. Every tile
    /// throws its own dust as it goes up, and a fog of fine dust in the ground's colour rolls
    /// out from under the blow and off behind the wave. The wave follows the lie of the ground
    /// up and down small steps and stops where the ground drops away or a wall stands up.
    ///
    /// Only ever drawn: no tile moves, nothing is hit. Client side; safe to call anywhere.
    /// </summary>
    public static class GroundWave
    {
        /// <summary>The colour of dust, which every ground's dust is taken toward.</summary>
        private static readonly Color Dun = new Color(168, 150, 124);

        /// <param name="bottom">The middle of the bottom of whatever hit the ground, world pixels.</param>
        /// <param name="halfWidth">Half its width, world pixels: the wave starts at its edges.</param>
        /// <param name="reach">How many tiles the wave runs out each way.</param>
        /// <param name="lift">How far the first tiles come up, in pixels.</param>
        /// <param name="spread">Ticks the wave takes to run its whole reach.</param>
        public static void Slam(Vector2 bottom, float halfWidth, int reach, float lift, int spread = 36)
        {
            if (Main.dedServ || reach <= 0)
                return;

            int cx = (int)(bottom.X / 16f);
            int row = Surface(cx, (int)((bottom.Y + 4f) / 16f), 4);
            if (row < 0)
                return;

            // The fog pushed out from under the body the moment it lands: mostly a short roll,
            // now and then a long one.
            Color under = FogColour(cx, row);
            for (int i = 0; i < 26; i++)
            {
                float side = Main.rand.NextBool() ? -1f : 1f;
                float r = Main.rand.NextFloat();
                Vector2 at = new Vector2(bottom.X + side * Main.rand.NextFloat(0f, halfWidth), row * 16f - Main.rand.NextFloat(0f, 10f));
                Vector2 push = new Vector2(side * (2.5f + 9f * r * r), -Main.rand.NextFloat(0.2f, 1.6f));

                PADParticleHandler.SpawnParticle(new DustFog(at, push, under,
                    Main.rand.Next(90, 150), Main.rand.NextFloat(0.7f, 1.2f), Main.rand.NextFloat(0.34f, 0.5f)));
            }

            // The wave, each way, from the column under the edge of the body.
            for (int away = -1; away <= 1; away += 2)
            {
                int edge = (int)((bottom.X + away * halfWidth) / 16f);
                int y = row;

                for (int i = 0; i <= reach; i++)
                {
                    int x = edge + away * i;
                    int s = Surface(x, y, 2);
                    if (s < 0)
                        break;
                    y = s;

                    float k = i / (float)reach;
                    float h = lift * (float)Math.Pow(1f - k, 1.2f) * Main.rand.NextFloat(0.85f, 1.15f);
                    if (h < 1f)
                        break;

                    // Slowing as it goes: the first few tiles go up together, the last ones
                    // well apart.
                    int wait = (int)(spread * Math.Pow(k, 1.35f));
                    int rise = 14 + (int)(10f * k);

                    PADParticleHandler.SpawnParticle(new LiftedTile(new Point(x, s), wait, rise, h, away, FogColour(x, s)));
                }
            }
        }

        /// <summary>
        /// The top of the ground in column <paramref name="x"/>, within <paramref name="reach"/>
        /// rows of <paramref name="guess"/>: a tile that can be stood on with nothing solid
        /// on top of it. -1 if there is none that near.
        /// </summary>
        private static int Surface(int x, int guess, int reach)
        {
            for (int y = guess - reach; y <= guess + reach; y++)
            {
                if (!WorldGen.InWorld(x, y, 2))
                    return -1;

                if (Stands(Main.tile[x, y]) && !Solid(Main.tile[x, y - 1]))
                    return y;
            }

            return -1;
        }

        private static bool Stands(Tile t) => t.HasUnactuatedTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);

        private static bool Solid(Tile t) => t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];

        /// <summary>
        /// The colour of the dust a blow knocks out of the ground here: the ground as the map
        /// shows it, dulled toward the colour of dust. Turf gives up the earth it grows on,
        /// not its green.
        /// </summary>
        private static Color FogColour(int x, int y)
        {
            if (Turf(Main.tile[x, y].TileType) && WorldGen.InWorld(x, y + 1) && Main.tile[x, y + 1].HasTile)
                y++;

            MapTile tile = MapHelper.CreateMapTile(x, y, byte.MaxValue);
            Color ground = MapHelper.GetMapTileXnaColor(ref tile);
            Color dull = Color.Lerp(ground, Dun, 0.55f);
            return new Color(dull.R, dull.G, dull.B, (byte)255);
        }

        private static bool Turf(ushort type) =>
            TileID.Sets.Conversion.Grass[type] || TileID.Sets.Conversion.JungleGrass[type]
            || TileID.Sets.Conversion.MushroomGrass[type] || TileID.Sets.Conversion.GolfGrass[type];
    }
}
