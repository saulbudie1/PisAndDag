using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// One tile of ground heaved up by a blow and settling back: after a wait it is kicked
    /// up a little way and comes down again slowly, drawn as itself, with the earth it came
    /// up on showing under it. Up is the floor's way; a tile in a wall or a ceiling is
    /// heaved out of its face the same way (the push), toward the open side. A row of these
    /// with their waits and heights stepped off from the centre is a wave running out
    /// through the ground and dying as it goes (<see cref="GroundWave"/>).
    ///
    /// The moment it starts to rise it throws its own dust and a puff of dust fog in the
    /// colour of the ground. Only whole solid tiles lift: sloped and half tiles (their frames
    /// are not whole squares) and platforms (too thin to show earth under them) only throw
    /// the dust. Nothing about the world changes: the tile stays where it is, this only draws
    /// it moving.
    /// </summary>
    public class LiftedTile : PADParticle
    {
        /// <summary>Drawn by hand from the tile's own texture; this is only for the handler's table.</summary>
        public override string Texture => "Terraria/Images/MagicPixel";

        public override bool SetLifetime => true;

        public override bool UseCustomDraw => true;

        private readonly Point tile;
        private readonly int wait;
        private readonly int rise;
        private readonly float height;
        private readonly int away;
        private readonly Color fog;

        /// <summary>Which way it is heaved, one tile's step: out of the face it is in. (0, -1) for a floor.</summary>
        private readonly Point push;

        private readonly Texture2D texture;
        private readonly Rectangle frame;
        private readonly bool whole;

        /// <param name="tile">The tile, in tile coordinates.</param>
        /// <param name="wait">Ticks before it moves.</param>
        /// <param name="rise">Ticks up and back down.</param>
        /// <param name="height">How far up it goes, in pixels.</param>
        /// <param name="away">Which way the wave is running: -1 west, 1 east, 0 both.</param>
        /// <param name="fog">The colour of the dust it throws.</param>
        public LiftedTile(Point tile, int wait, int rise, float height, int away, Color fog)
            : this(tile, wait, rise, height, away, fog, new Point(0, -1))
        {
        }

        /// <param name="tile">The tile, in tile coordinates.</param>
        /// <param name="wait">Ticks before it moves.</param>
        /// <param name="rise">Ticks out and back.</param>
        /// <param name="height">How far out it goes, in pixels.</param>
        /// <param name="away">Which way the wave is running along the face: -1 toward lower x (or y on a wall), 1 higher, 0 both.</param>
        /// <param name="fog">The colour of the dust it throws.</param>
        /// <param name="push">Which way it is heaved, out of its face: (0, -1) a floor, (0, 1) a ceiling, (1, 0) or (-1, 0) a wall.</param>
        public LiftedTile(Point tile, int wait, int rise, float height, int away, Color fog, Point push)
        {
            this.tile = tile;
            this.push = push;
            this.wait = Math.Max(0, wait);
            this.rise = Math.Max(2, rise);
            this.height = height;
            this.away = away;
            this.fog = fog;

            Position = new Vector2(tile.X * 16f, tile.Y * 16f);

            // One tick past the rise, so the last frame drawn is the one where it is back
            // down, not one where it is still a few pixels up and then vanishes.
            Lifetime = this.wait + this.rise + 1;

            if (Main.dedServ || !WorldGen.InWorld(tile.X, tile.Y))
                return;

            Tile t = Main.tile[tile.X, tile.Y];
            if (!t.HasTile)
                return;

            whole = t.Slope == SlopeType.Solid && !t.IsHalfBlock
                && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];

            // The frame as the tile renderer would draw it this tick, animation included.
            short frameX = t.TileFrameX, frameY = t.TileFrameY;

            Main.instance.TilesRenderer.GetTileDrawData(tile.X, tile.Y, t, t.TileType, ref frameX, ref frameY,
                out _, out _, out _, out _, out int addX, out int addY, out _, out _, out _, out _);

            // The painted sheet if it is painted.
            texture = Main.instance.TilesRenderer.GetTileDrawTexture(t, tile.X, tile.Y);
            frame = new Rectangle(frameX + addX, frameY + addY, 16, 16);
        }

        public override void Update()
        {
            if (Time != wait + 1 || Main.dedServ || !WorldGen.InWorld(tile.X, tile.Y))
                return;

            Tile t = Main.tile[tile.X, tile.Y];
            if (!t.HasTile)
                return;

            // Along its face, and out of it. A floor: across, and up.
            Vector2 along = push.X == 0 ? Vector2.UnitX : Vector2.UnitY;
            Vector2 outward = new Vector2(push.X, push.Y);

            // Its own dust, knocked out of it.
            for (int i = 0; i < 2; i++)
            {
                int index = WorldGen.KillTile_MakeTileDust(tile.X, tile.Y, t);
                if (index < 0 || index >= Main.maxDust)
                    continue;

                Dust dust = Main.dust[index];
                dust.noGravity = false;
                dust.velocity = along * (Main.rand.NextFloat(-1.5f, 1.5f) + away * Main.rand.NextFloat(0.5f, 2f))
                    + outward * Main.rand.NextFloat(2f, 4f + height * 0.2f);
                dust.scale = Main.rand.NextFloat(0.8f, 1.2f);
            }

            // The fog it lets go at its face, rolling on the way the wave runs.
            Vector2 face = Position + new Vector2(push.X > 0 ? 16f : 0f, push.Y > 0 ? 16f : 0f);
            int puffs = height > 8f ? 2 : 1;
            for (int i = 0; i < puffs; i++)
            {
                Vector2 at = face + along * Main.rand.NextFloat(0f, 16f) - outward * Main.rand.NextFloat(-6f, 2f);
                Vector2 roll = along * (away * Main.rand.NextFloat(0.6f, 2.2f) + Main.rand.NextFloat(-0.4f, 0.4f))
                    + outward * Main.rand.NextFloat(0.3f, 1.1f);

                PADParticleHandler.SpawnParticle(new DustFog(at, roll, fog,
                    Main.rand.Next(70, 120), Main.rand.NextFloat(0.45f, 0.75f), Main.rand.NextFloat(0.28f, 0.4f)));
            }
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            // Mined out from under the wave: nothing left to lift.
            if (Time <= wait || texture == null || !whole || !Main.tile[tile.X, tile.Y].HasTile)
                return;

            // Kicked up hard and let down slowly, the way ground settles.
            float u = MathHelper.Clamp((Time - wait) / (float)rise, 0f, 1f);
            float settle = 1f - (1f - u) * (1f - u);
            float lift = height * (float)Math.Sin(Math.PI * settle);
            if (lift < 0.5f)
                return;

            Color light = Lighting.GetColor(tile);
            Vector2 at = Position - Main.screenPosition;

            // Behind it, the earth it came out of: its own back rows (the bottom two, for a
            // floor) drawn from its back to where it really sits, in shadow. This also covers
            // the tile the world still draws in place, which would otherwise show a second
            // face behind this one.
            Rectangle earth;
            Vector2 earthAt;
            Vector2 earthScale;

            if (push.Y < 0)
            {
                earth = new Rectangle(frame.X, frame.Y + 14, 16, 2);
                earthAt = new Vector2(0f, 16f - lift);
                earthScale = new Vector2(1f, lift / 2f);
            }
            else if (push.Y > 0)
            {
                earth = new Rectangle(frame.X, frame.Y, 16, 2);
                earthAt = Vector2.Zero;
                earthScale = new Vector2(1f, lift / 2f);
            }
            else if (push.X > 0)
            {
                earth = new Rectangle(frame.X, frame.Y, 2, 16);
                earthAt = Vector2.Zero;
                earthScale = new Vector2(lift / 2f, 1f);
            }
            else
            {
                earth = new Rectangle(frame.X + 14, frame.Y, 2, 16);
                earthAt = new Vector2(16f - lift, 0f);
                earthScale = new Vector2(lift / 2f, 1f);
            }

            Color shade = Color.Lerp(Color.Black, light, 0.6f);
            spriteBatch.Draw(texture, at + earthAt, earth, shade, 0f, Vector2.Zero, earthScale, SpriteEffects.None, 0f);

            spriteBatch.Draw(texture, at + new Vector2(push.X, push.Y) * lift, frame, light, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }
}
