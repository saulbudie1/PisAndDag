using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The weight of the water, by request ("After section 7, every block you place will shakes
    /// and break within 1.3 seconds"): from the Vast Ocean's 8th section down, whatever a player
    /// puts down (a block, or any tile: a platform, a torch, a piece of furniture) shakes harder
    /// and harder and breaks 1.3 seconds after, as if it had been mined: what it is comes back as
    /// an item. Walls are not blocks, and are left. What the place was made with (its floor) is
    /// not touched: only what is put down by hand, as it is put down (GlobalTile.PlaceInWorld).
    ///
    /// The breaking is done where it was put down from (the player's own machine, or a single
    /// player's game), and in a game with others sent on as any mined tile is.
    ///
    /// The place keeps its own ground down there, by request ("Blocks on the sediment cannot be
    /// broken": VastOcean.KeepsTile); what is put down is noted on every machine as it is put
    /// down (<see cref="PutDownAt"/>), so that it is not kept, and can crumble.
    /// </summary>
    public class VastOceanPressure : ModSystem
    {
        /// <summary>1.3 seconds.</summary>
        public const int Holds = 78;

        /// <summary>Each tile put down down there, what it was and when.</summary>
        private static readonly Dictionary<Point, (ushort type, uint at)> failing = new();

        /// <summary>
        /// Every tile put down from the 8th section down, by anyone, on every machine: through
        /// the game's one place for putting a tile down (WorldGen.PlaceTile, by detour), a
        /// player's own as they put it down, and the server's as it is told of it. The place keeps
        /// its own ground there (VastOcean.KeepsTile, by request: "Blocks on the sediment cannot be
        /// broken"); these it does not, so that they can crumble, and be broken before they do.
        /// </summary>
        private static readonly HashSet<Point> putDown = new();

        /// <summary>Whether a tile put down by hand is here (or was: a block can only come back by being put down again).</summary>
        internal static bool PutDownAt(int x, int y) => putDown.Count > 0 && putDown.Contains(new Point(x, y));

        public override void Load()
        {
            On_WorldGen.PlaceTile += NotePutDown;
        }

        public override void ClearWorld()
        {
            failing.Clear();
            putDown.Clear();
        }

        public override void Unload()
        {
            failing.Clear();
            putDown.Clear();
        }

        /// <summary>A tile put down in the Vast Ocean from the 8th section down, noted on whatever machine it is put down on; not while the place is being laid out.</summary>
        private static bool NotePutDown(On_WorldGen.orig_PlaceTile orig, int i, int j, int Type, bool mute, bool forced, int plr, int style)
        {
            bool put = orig(i, j, Type, mute, forced, plr, style);

            if (put && !WorldGen.gen && j >= VastOcean.GroundKeptFrom && SubworldKeeper.Current is VastOcean)
                putDown.Add(new Point(i, j));

            return put;
        }

        internal static void Put(int i, int j, int type)
        {
            if (SubworldKeeper.Current is VastOcean && j >= VastOcean.CrumbleFrom && Main.netMode != NetmodeID.Server)
                failing[new Point(i, j)] = ((ushort)type, Main.GameUpdateCount);
        }

        /// <summary>Is this tile shaking, and how far through its 1.3 seconds (0 to 1).</summary>
        internal static bool Shaking(int i, int j, out float through)
        {
            through = 0f;

            if (failing.Count == 0 || !failing.TryGetValue(new Point(i, j), out var put))
                return false;

            through = MathHelper.Clamp((Main.GameUpdateCount - put.at) / (float)Holds, 0f, 1f);
            return true;
        }

        public override void PostUpdateEverything()
        {
            // What was put down and is gone again, forgotten now and then.
            if (putDown.Count > 0 && Main.GameUpdateCount % 600 == 0)
                putDown.RemoveWhere(at => !WorldGen.InWorld(at.X, at.Y) || !Main.tile[at.X, at.Y].HasTile);

            if (failing.Count == 0)
                return;

            foreach (var (at, put) in new List<KeyValuePair<Point, (ushort type, uint at)>>(failing))
            {
                Tile tile = Main.tile[at.X, at.Y];

                // Gone already, or something else there now.
                if (!tile.HasTile || tile.TileType != put.type)
                {
                    failing.Remove(at);
                    continue;
                }

                uint held = Main.GameUpdateCount - put.at;

                // Grit shaken off it now and then, more as it goes.
                if (!Main.dedServ && Main.rand.NextFloat() < 0.05f + 0.25f * held / Holds)
                    Dust.NewDust(new Vector2(at.X * 16, at.Y * 16), 16, 16, DustID.Sand, Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.4f, 0.4f), 120, default, 0.7f);

                if (held < Holds)
                    continue;

                failing.Remove(at);
                WorldGen.KillTile(at.X, at.Y);

                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, at.X, at.Y);
            }
        }
    }

    /// <summary>Notes what is put down, and shakes a block that is going to break.</summary>
    public class VastOceanPressureTile : GlobalTile
    {
        public override void PlaceInWorld(int i, int j, int type, Item item)
        {
            VastOceanPressure.Put(i, j, type);
        }

        /// <summary>
        /// A whole block drawn here, a little off where it is, more as it goes; anything else is
        /// left to the game to draw (it shakes off grit all the same).
        /// </summary>
        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (!VastOceanPressure.Shaking(i, j, out float through))
                return true;

            Tile tile = Main.tile[i, j];

            if (!Main.tileSolid[type] || Main.tileFrameImportant[type] || tile.IsHalfBlock || tile.Slope != SlopeType.Solid || tile.TileColor != 0)
                return true;

            Main.instance.LoadTiles(type);
            Texture2D texture = TextureAssets.Tile[type].Value;

            float shake = 0.5f + 2.5f * through;
            Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 jitter = new Vector2(Main.rand.NextFloat(-shake, shake), Main.rand.NextFloat(-shake, shake));
            Vector2 at = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen + jitter;

            spriteBatch.Draw(texture, at, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Lighting.GetColor(i, j));

            return false;
        }
    }
}
