using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Biomes.Sludge
{
    /// <summary>
    /// Toxic sludge, the liquid of the sludge pocket at the bottom of the Jungle (SludgePocket), by request ("create the
    /// liquid toxic sludge ... Toxic sludge sits on water and turn nearby water tiles into toxic sludge liquid tile with
    /// chance of 1 in 100 a second. It is a foul brown color").
    ///
    /// tModLoader 1.4.4 has no way to add a liquid of one's own (the game knows four, and everything about them is
    /// in arrays of four), so sludge is water marked as sludge, cell by cell: the game moves it, fills and empties it,
    /// and draws it as water; this keeps which cells are sludge, and paints them over.
    ///
    ///   the marks     every cell of the world that is sludge (a set of their places), saved with the world and told
    ///                 to every client, the server deciding (PADPacket.Sludge). A mark on a cell that has no water in
    ///                 it any more, or has lava, honey or shimmer, is gone.
    ///   it floats     water over a cell of sludge changes places with it, so sludge comes up through water and sits
    ///                 on top of it.
    ///   it runs       an empty open cell beside sludge (left, right or under it) that has filled since the last look
    ///                 is sludge, as what ran into it was.
    ///   it spreads    water beside sludge (above, under, left or right) is made sludge, a chance in a hundred a second
    ///                 for each.
    ///   how often     every cell is looked at once every <see cref="PassTicks"/> ticks (a third of a second), a share of
    ///                 them each tick, so that a whole sea of it costs the same every tick and never all at once.
    ///   how much      at most <see cref="Most"/> cells; past that it neither runs nor spreads.
    ///   kept          as runs (the first place of each, and how many follow it along the row): saved so, and told so.
    ///
    /// Drawn (on every machine that draws, every frame, in the world's batch just after the game lays its water on the screen): each cell
    /// of sludge over its water, as full as the water in it, a foul brown, its surface scummed and bubbling; it gives
    /// a little light of its own, and now and then a fume comes off it and a bubble is heard.
    /// What it does to a player who stands or goes under in it: SludgePlayer.
    /// </summary>
    public class ToxicSludge : ModSystem
    {
        /// <summary>Most cells of sludge in a world; ticks between floating and running, and between spreading; how often the server tells what changed.</summary>
        public const int Most = 250000;
        private const int PassTicks = 20, TellEvery = 20;

        /// <summary>The chance (one in this) that water beside sludge is made sludge, each second; and so each look (three looks a second).</summary>
        public const int SpreadsOneIn = 100;
        private const int SpreadsOneInALook = SpreadsOneIn * 60 / PassTicks;

        /// <summary>The colours of it: its depths, its body, its scum, the scum's lit edge, a bubble's skin.</summary>
        public static readonly Color Deep = new(52, 38, 22), Body = new(84, 62, 32), Scum = new(118, 94, 46), ScumLit = new(146, 122, 60), Bubble = new(160, 138, 78);

        private static readonly HashSet<int> cells = new();
        private static HashSet<int> beside = new(), besideNext = new();
        private static readonly HashSet<int> added = new(), removed = new();
        private static uint ticks;

        /// <summary>The cells of this pass, and how far through them it is.</summary>
        private static int[] order = Array.Empty<int>();
        private static int orderCount, at;

        public static int Count => cells.Count;

        private static int Key(int x, int y) => y * Main.maxTilesX + x;

        private static void Place(int key, out int x, out int y)
        {
            x = key % Main.maxTilesX;
            y = key / Main.maxTilesX;
        }

        /// <summary>The cell is marked as sludge (whatever is in it).</summary>
        public static bool Marked(int x, int y) => WorldGen.InWorld(x, y) && cells.Contains(Key(x, y));

        /// <summary>The cell is sludge: marked, and water in it.</summary>
        public static bool At(int x, int y)
        {
            if (!Marked(x, y))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;
        }

        /// <summary>How high the liquid in a cell stands, in world pixels: its cell full if there is liquid over it.</summary>
        public static float Top(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            bool full = y > 0 && Main.tile[x, y - 1].LiquidAmount > 0;
            return (y + 1) * 16f - (full ? 16f : tile.LiquidAmount / 255f * 16f);
        }

        /// <summary>The world place is under the surface of sludge.</summary>
        public static bool In(Vector2 world)
        {
            int x = (int)(world.X / 16f), y = (int)(world.Y / 16f);
            return At(x, y) && world.Y >= Top(x, y);
        }

        /// <summary>
        /// A cell made sludge. Where the world is decided (and by worldgen); the clients are told. Never refused for how
        /// much there is already (a cavern's lake is laid whole): <see cref="Most"/> limits only what runs and spreads.
        /// </summary>
        public static void Mark(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return;

            int key = Key(x, y);

            if (cells.Add(key))
                Changed(key, true);
        }

        public static void Unmark(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return;

            int key = Key(x, y);

            if (cells.Remove(key))
                Changed(key, false);
        }

        private static void Changed(int key, bool sludge)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            if (sludge)
            {
                if (!removed.Remove(key))
                    added.Add(key);
            }
            else if (!added.Remove(key))
            {
                removed.Add(key);
            }
        }

        public override void ClearWorld()
        {
            cells.Clear();
            beside.Clear();
            besideNext.Clear();
            added.Clear();
            removed.Clear();
            orderCount = at = 0;
        }

        /// <summary>Every cell as runs along the rows: the first place of each run, then how many, pair after pair.</summary>
        private static int[] Runs()
        {
            int[] all = new int[cells.Count];
            cells.CopyTo(all);
            Array.Sort(all);
            List<int> runs = new();

            for (int i = 0; i < all.Length;)
            {
                int j = i + 1;

                while (j < all.Length && all[j] == all[j - 1] + 1)
                    j++;

                runs.Add(all[i]);
                runs.Add(j - i);
                i = j;
            }

            return runs.ToArray();
        }

        private static void AddRuns(int[] runs, int limit)
        {
            for (int i = 0; i + 1 < runs.Length; i += 2)
                for (int k = 0; k < runs[i + 1]; k++)
                    if (runs[i] + k >= 0 && runs[i] + k < limit)
                        cells.Add(runs[i] + k);
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (cells.Count == 0)
                return;

            tag["SludgeRuns"] = Runs();
            tag["SludgeWide"] = Main.maxTilesX;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            cells.Clear();

            if (tag.GetInt("SludgeWide") != Main.maxTilesX)
                return;

            if (tag.ContainsKey("SludgeRuns"))
            {
                AddRuns(tag.GetIntArray("SludgeRuns"), Main.maxTilesX * Main.maxTilesY);
                return;
            }

            if (!tag.ContainsKey("SludgeCells"))
                return;

            foreach (int key in tag.GetIntArray("SludgeCells"))
                if (key >= 0 && key < Main.maxTilesX * Main.maxTilesY)
                    cells.Add(key);
        }

        // ---- how it moves, where the world is decided ------------------------------------------------------------------

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            ticks++;

            if (cells.Count > 0 || beside.Count > 0 || at < orderCount)
                Look();

            if (Main.netMode == NetmodeID.Server && ticks % TellEvery == 0 && (added.Count > 0 || removed.Count > 0))
                TellChanges();
        }

        private static bool Water(Tile tile) => tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;

        private static bool Open(Tile tile) => !tile.HasTile || tile.IsActuated || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];

        /// <summary>
        /// This tick's share of the pass: each cell in it dried or taken (gone), floated up through water over it, spread
        /// into water beside it by chance, and its empty open neighbours noted to look at next pass. A pass begins by
        /// marking the neighbours noted last pass that have filled since (what ran).
        /// </summary>
        private static void Look()
        {
            if (at >= orderCount)
            {
                foreach (int key in beside)
                {
                    Place(key, out int x, out int y);

                    if (WorldGen.InWorld(x, y, 1) && Water(Main.tile[x, y]))
                        Mark(x, y);
                }

                (beside, besideNext) = (besideNext, beside);
                besideNext.Clear();

                if (order.Length < cells.Count)
                    order = new int[cells.Count + cells.Count / 4 + 64];

                cells.CopyTo(order);
                orderCount = cells.Count;
                at = 0;
            }

            int share = Math.Min(orderCount - at, Math.Max(64, (orderCount + PassTicks - 1) / PassTicks));
            int wide = Main.maxTilesX;

            for (int i = at; i < at + share; i++)
            {
                int key = order[i];

                if (!cells.Contains(key))
                    continue;

                Place(key, out int x, out int y);

                if (!WorldGen.InWorld(x, y, 1) || !Water(Main.tile[x, y]))
                {
                    Unmark(x, y);
                    continue;
                }

                // It floats: water over it changes places with it.
                if (Water(Main.tile[x, y - 1]) && !cells.Contains(key - wide))
                {
                    Unmark(x, y);
                    Mark(x, y - 1);
                    continue;
                }

                if (cells.Count < Most)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0), ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);

                        if (!cells.Contains(Key(nx, ny)) && Water(Main.tile[nx, ny]) && Main.rand.NextBool(SpreadsOneInALook))
                            Mark(nx, ny);
                    }
                }

                Beside(besideNext, x - 1, y);
                Beside(besideNext, x + 1, y);
                Beside(besideNext, x, y + 1);
            }

            at += share;
        }

        private static void Beside(HashSet<int> into, int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            Tile tile = Main.tile[x, y];

            if (tile.LiquidAmount == 0 && Open(tile))
                into.Add(Key(x, y));
        }

        // ---- told across the network ------------------------------------------------------------------------------------

        private enum Message : byte { Changes, All, More, Ask }

        /// <summary>Most places (or runs) in one packet.</summary>
        private const int PerPacket = 6000;

        /// <summary>A client come into the world asks the server for every cell (SludgePlayer.OnEnterWorld).</summary>
        public static void Ask(Mod mod)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = mod.GetPacket();
            packet.Write((byte)PADPacket.Sludge);
            packet.Write((byte)Message.Ask);
            packet.Send();
        }

        /// <summary>Every cell, as runs, to one client (or all: -1), in as many packets as it takes; the first clears what they had.</summary>
        public static void TellAll(Mod mod, int to)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            int[] runs = Runs();
            int pairs = runs.Length / 2, sent = 0;

            do
            {
                int many = Math.Min(PerPacket / 2, pairs - sent);
                ModPacket packet = mod.GetPacket();
                packet.Write((byte)PADPacket.Sludge);
                packet.Write((byte)(sent == 0 ? Message.All : Message.More));
                packet.Write(many);

                for (int i = 0; i < many; i++)
                {
                    packet.Write(runs[(sent + i) * 2]);
                    packet.Write(runs[(sent + i) * 2 + 1]);
                }

                packet.Send(to);
                sent += many;
            }
            while (sent < pairs);
        }

        private static void TellChanges()
        {
            Mod mod = ModContent.GetInstance<PisAndDag>();
            int[] come = new int[added.Count], gone = new int[removed.Count];
            added.CopyTo(come);
            removed.CopyTo(gone);
            added.Clear();
            removed.Clear();

            for (int a = 0, r = 0; a < come.Length || r < gone.Length;)
            {
                int many = Math.Min(PerPacket, come.Length - a), fewer = Math.Min(PerPacket - many, gone.Length - r);
                ModPacket packet = mod.GetPacket();
                packet.Write((byte)PADPacket.Sludge);
                packet.Write((byte)Message.Changes);
                packet.Write(many);

                for (int i = 0; i < many; i++)
                    packet.Write(come[a + i]);

                packet.Write(fewer);

                for (int i = 0; i < fewer; i++)
                    packet.Write(gone[r + i]);

                packet.Send();
                a += many;
                r += fewer;
            }
        }

        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI)
        {
            var message = (Message)reader.ReadByte();

            if (message == Message.Ask)
            {
                if (Main.netMode == NetmodeID.Server)
                    TellAll(mod, whoAmI);

                return;
            }

            // Only the server tells which cells are sludge.
            if (Main.netMode == NetmodeID.Server)
                return;

            if (message == Message.All || message == Message.More)
            {
                if (message == Message.All)
                    cells.Clear();

                int runs = reader.ReadInt32();
                int[] pairs = new int[runs * 2];

                for (int i = 0; i < pairs.Length; i++)
                    pairs[i] = reader.ReadInt32();

                AddRuns(pairs, Main.maxTilesX * Main.maxTilesY);
                return;
            }

            int come = reader.ReadInt32();

            for (int i = 0; i < come; i++)
                cells.Add(reader.ReadInt32());

            int gone = reader.ReadInt32();

            for (int i = 0; i < gone; i++)
                cells.Remove(reader.ReadInt32());
        }

        // ---- how it looks ------------------------------------------------------------------------------------------------

        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.DrawInfernoRings += AfterWater;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                On_Main.DrawInfernoRings -= AfterWater;
        }

        /// <summary>
        /// The sludge, over the water in front, every frame. Drawn in the world's own batch just after the game has laid
        /// its water on the screen (Main.DoDraw: the water, its foreground overlays, then the inferno rings, this), not
        /// into the game's picture of the water: that picture is made again only now and then, and other things work on
        /// it as it is made (the M.U.P.R.A.L.'s light, Main.RenderWater), and drawn into it the sludge was not seen at all
        /// (r82ax: a lake whose every cell was marked showing as clear water).
        /// </summary>
        private static void AfterWater(On_Main.orig_DrawInfernoRings orig, Main self)
        {
            orig(self);

            if (cells.Count == 0 || Main.gameMenu)
                return;

            try
            {
                Draw(Main.spriteBatch);
            }
            catch (Exception e)
            {
                ModContent.GetInstance<PisAndDag>().Logger.Error("Toxic sludge could not be drawn this frame.", e);
            }
        }

        private static uint Hash(int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13;
            h *= 1274126177u;
            return h ^ (h >> 16);
        }

        private static void Draw(SpriteBatch batch)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 corner = Main.screenPosition;
            int left = Math.Max(1, (int)(corner.X / 16f) - 1), top = Math.Max(1, (int)(corner.Y / 16f) - 1);
            int right = Math.Min(Main.maxTilesX - 2, (int)((corner.X + Main.screenWidth) / 16f) + 2);
            int bottom = Math.Min(Main.maxTilesY - 2, (int)((corner.Y + Main.screenHeight) / 16f) + 2);
            float time = Main.GlobalTimeWrappedHourly * 60f;

            void Square(float x, float y, int w, int h, Color colour) =>
                batch.Draw(pixel, new Rectangle((int)(x - corner.X), (int)(y - corner.Y), w, h), colour);

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    if (!At(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];
                    bool full = Main.tile[x, y - 1].LiquidAmount > 0;
                    int high = full ? 16 : Math.Max(2, (int)(tile.LiquidAmount / 255f * 16f) / 2 * 2);
                    float cellTop = y * 16f + 16 - high;
                    Color light = Lighting.GetColor(x, y);

                    // A little light of its own, so that it shows in the dark as a foul shape.
                    Vector3 seen = Vector3.Max(light.ToVector3(), new Vector3(0.16f, 0.14f, 0.1f));
                    Color Lit(Color c, float alpha = 0.94f) => new Color(c.ToVector3() * seen) * alpha;

                    bool deep = At(x, y - 1) && At(x, y - 2);
                    Square(x * 16f, cellTop, 16, high, Lit(deep ? Deep : Body));

                    // Its grain: a few darker and lighter flecks on the two-pixel grid, drifting slowly.
                    uint h = Hash(x, y);

                    for (int f = 0; f < (deep ? 1 : 3); f++)
                    {
                        uint g = Hash(x * 3 + f, y * 7 + (int)((time / 90f + (h >> (f * 4) & 15)) % 64));
                        int fx = (int)(g % 8) * 2, fy = (int)(g / 8 % 8) * 2;

                        if (16 - fy > high)
                            continue;

                        Square(x * 16f + fx, y * 16f + fy, 2, 2, Lit(f == 0 ? Scum : Deep, 0.8f));
                    }

                    if (full)
                        continue;

                    // Its surface: scum, its lit edge, and now and then a bubble rising out of it and bursting.
                    Square(x * 16f, cellTop, 16, 2, Lit(ScumLit));
                    Square(x * 16f, cellTop + 2, 16, 2, Lit(Scum, 0.9f));

                    float period = 140f + h % 160;
                    float phase = (time + h % 997) % period / period;

                    if (phase < 0.3f)
                    {
                        float swell = phase / 0.3f;
                        int size = 2 + (int)(swell * 3f) * 2;
                        int bx = 2 + (int)(h % 5) * 2;
                        Square(x * 16f + bx, cellTop - size + 2, size, size, Lit(Bubble, 0.9f));
                        Square(x * 16f + bx, cellTop - size + 2, 2, 2, Lit(ScumLit, 0.95f));
                    }
                }
            }
        }

        // ---- the sense of it, near the player ----------------------------------------------------------------------

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || cells.Count == 0 || Main.gameMenu || Main.LocalPlayer == null)
                return;

            Player me = Main.LocalPlayer;
            int px = (int)(me.Center.X / 16f), py = (int)(me.Center.Y / 16f);

            // A few cells about them a tick: light off its surface, a fume, a bubble heard.
            for (int k = 0; k < 24; k++)
            {
                int x = px + Main.rand.Next(-45, 46), y = py + Main.rand.Next(-28, 29);

                if (!WorldGen.InWorld(x, y, 2) || !At(x, y) || Main.tile[x, y - 1].LiquidAmount > 0)
                    continue;

                Vector2 surface = new Vector2(x * 16f + 8f, Top(x, y));
                Lighting.AddLight(surface, 0.16f, 0.13f, 0.05f);

                if (Main.rand.NextBool(18))
                {
                    PADParticleHandler.SpawnParticle(new DustFog(surface + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.7f, -0.3f)),
                        new Color(108, 98, 52), Main.rand.Next(50, 80), Main.rand.NextFloat(0.3f, 0.55f), 0.22f));
                }

                if (Main.rand.NextBool(220))
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.6f, PitchVariance = 0.25f }, surface);
            }
        }
    }
}
