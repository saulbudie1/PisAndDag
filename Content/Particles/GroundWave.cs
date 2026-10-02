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
    ///
    /// Slam is the floor. Strike is any face, a wall or a ceiling as well: the same wave run
    /// out along the face both ways, each tile heaved out of it (LiftedTile's push).
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
        /// A blow into whatever face it met: a floor is <see cref="Slam"/>; a wall or a ceiling
        /// takes the same fog and wave, run out along it both ways from the edges of the blow,
        /// each tile heaved out of the face toward the open side, and dust knocked out of
        /// the face where it hit.
        /// </summary>
        /// <param name="contact">Where the blow met the face, world pixels: on its surface.</param>
        /// <param name="facing">Which way the face looks, out of it; the nearest of the four is taken.</param>
        /// <param name="halfWidth">Half the width of whatever hit, world pixels: the wave starts at its edges.</param>
        /// <param name="reach">How many tiles the wave runs out each way.</param>
        /// <param name="lift">How far the first tiles come out, in pixels.</param>
        /// <param name="spread">Ticks the wave takes to run its whole reach.</param>
        public static void Strike(Vector2 contact, Vector2 facing, float halfWidth, int reach, float lift, int spread = 36)
        {
            if (Main.dedServ || reach <= 0)
                return;

            // The face, as the nearest of the four: out of a floor is up, out of a ceiling
            // down, out of a wall to one side.
            bool across = Math.Abs(facing.Y) >= Math.Abs(facing.X);
            int sign = across ? (facing.Y < 0f ? -1 : 1) : (facing.X < 0f ? -1 : 1);

            if (across && sign < 0)
            {
                Slam(contact, halfWidth, reach, lift, spread);
                return;
            }

            Point open = across ? new Point(0, sign) : new Point(sign, 0);
            Point into = new Point(-open.X, -open.Y);
            Vector2 alongWay = across ? Vector2.UnitX : Vector2.UnitY;
            Vector2 openWay = new Vector2(open.X, open.Y);

            // u runs along the face, v into and out of it, both in tiles.
            float middle = across ? contact.X : contact.Y;
            float surface = across ? contact.Y : contact.X;
            int u0 = (int)(middle / 16f);
            int v0 = Face(across, sign, u0, (int)((surface - sign * 4f) / 16f), 4);

            if (v0 == Missing)
                return;

            // The plane of the face, in world pixels.
            float plane = (v0 + (sign > 0 ? 1 : 0)) * 16f;

            // The fog pushed out from under the blow, along the face and off it.
            Color under = FogColour(At(across, u0, v0), into);

            for (int i = 0; i < 26; i++)
            {
                float side = Main.rand.NextBool() ? -1f : 1f;
                float r = Main.rand.NextFloat();
                float u = middle + side * Main.rand.NextFloat(0f, halfWidth);
                float v = plane + sign * Main.rand.NextFloat(0f, 10f);
                Vector2 at = across ? new Vector2(u, v) : new Vector2(v, u);
                Vector2 push = alongWay * (side * (2.5f + 9f * r * r)) + openWay * Main.rand.NextFloat(0.2f, 1.6f);

                PADParticleHandler.SpawnParticle(new DustFog(at, push, under,
                    Main.rand.Next(90, 150), Main.rand.NextFloat(0.7f, 1.2f), Main.rand.NextFloat(0.34f, 0.5f)));
            }

            // Its own dust, knocked out of the face under the blow.
            int from = (int)((middle - halfWidth) / 16f);
            int to = (int)((middle + halfWidth) / 16f);

            for (int u = from; u <= to; u++)
            {
                int v = Face(across, sign, u, v0, 2);

                if (v == Missing)
                    continue;

                Point cell = At(across, u, v);
                Tile tile = Main.tile[cell.X, cell.Y];

                for (int i = 0; i < 3; i++)
                {
                    int index = WorldGen.KillTile_MakeTileDust(cell.X, cell.Y, tile);

                    // 6000 is the "no dust for this tile" answer, and one past the end of the array.
                    if (index < 0 || index >= Main.maxDust)
                        continue;

                    Dust dust = Main.dust[index];
                    dust.noGravity = false;
                    dust.velocity = alongWay * Main.rand.NextFloat(-3.5f, 3.5f) + openWay * Main.rand.NextFloat(4f, 9f);
                    dust.scale = Main.rand.NextFloat(0.9f, 1.5f);
                }
            }

            // The wave, each way along the face, from under the edges of the blow.
            for (int away = -1; away <= 1; away += 2)
            {
                int edge = (int)((middle + away * halfWidth) / 16f);
                int v = v0;

                for (int i = 0; i <= reach; i++)
                {
                    int u = edge + away * i;
                    int s = Face(across, sign, u, v, 2);
                    if (s == Missing)
                        break;
                    v = s;

                    float k = i / (float)reach;
                    float h = lift * (float)Math.Pow(1f - k, 1.2f) * Main.rand.NextFloat(0.85f, 1.15f);
                    if (h < 1f)
                        break;

                    int wait = (int)(spread * Math.Pow(k, 1.35f));
                    int rise = 14 + (int)(10f * k);
                    Point cell = At(across, u, s);

                    PADParticleHandler.SpawnParticle(new LiftedTile(cell, wait, rise, h, away, FogColour(cell, into), open));
                }
            }
        }

        private const int Missing = int.MinValue;

        /// <summary>The tile at u along a face and v into it: a ceiling runs across (u is x), a wall up and down (u is y).</summary>
        private static Point At(bool across, int u, int v) => across ? new Point(u, v) : new Point(v, u);

        /// <summary>
        /// Where the face is at <paramref name="u"/> along it, within <paramref name="reach"/>
        /// of <paramref name="guess"/>: a solid tile with open space on the side the face
        /// looks to (<paramref name="sign"/> along v). Looked for from the open side in.
        /// <see cref="Missing"/> if there is none that near.
        /// </summary>
        private static int Face(bool across, int sign, int u, int guess, int reach)
        {
            for (int s = reach; s >= -reach; s--)
            {
                int v = guess + sign * s;
                Point cell = At(across, u, v);
                Point beyond = At(across, u, v + sign);

                if (!WorldGen.InWorld(cell.X, cell.Y, 2) || !WorldGen.InWorld(beyond.X, beyond.Y, 2))
                    return Missing;

                if (Solid(Main.tile[cell.X, cell.Y]) && !Solid(Main.tile[beyond.X, beyond.Y]))
                    return v;
            }

            return Missing;
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
        /// not its green. Public: anything else that throws the ground's dust up (the ground
        /// over Amalgaermis) wants the same colour.
        /// </summary>
        public static Color FogColour(int x, int y) => FogColour(new Point(x, y), new Point(0, 1));

        /// <summary>The same for a tile in any face: turf gives up the earth behind it, <paramref name="into"/> the face.</summary>
        private static Color FogColour(Point cell, Point into)
        {
            int x = cell.X;
            int y = cell.Y;

            if (Turf(Main.tile[x, y].TileType) && WorldGen.InWorld(x + into.X, y + into.Y) && Main.tile[x + into.X, y + into.Y].HasTile)
            {
                x += into.X;
                y += into.Y;
            }

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
