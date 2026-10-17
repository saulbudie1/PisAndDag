using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Tiles.SeaFloor
{
    /// <summary>
    /// What the floor's living things and vents give off, on each player's own machine, for the
    /// looks only, while the Vast Ocean is where they are; each from those on or near the screen
    /// (looked for again every third of a second):
    ///   - a black smoker's mouth: black smoke rising in puffs, now and then a white fleck of
    ///     bacteria;
    ///   - a white smoker's: the shimmer of the clear warm water pouring up;
    ///   - a seep's crack: bubbles of gas, a few at a time, rising and bursting as the sea's
    ///     others do (VastOceanMotes);
    ///   - the tube worms: flecks drifting up off them in the warm water;
    ///   - a cup sponge: a thin stream of specks from its mouth, the water it has filtered;
    ///   - the corals, the sponges, the sea lilies, the anemones: plankton drifting by, one speck
    ///     in a dozen blinking blue;
    ///   - the octocorals (sea pens, bubblegum and bamboo coral): motes of blue-green light
    ///     let off where they are touched, and now and then even untouched.
    /// </summary>
    public class SeaLifeEffects : ModSystem
    {
        private struct Seen
        {
            public int X, Y;
            public SeaLife.Kind Kind;
            public byte Style;
        }

        private static readonly List<Seen> seen = new List<Seen>();
        private static readonly Dictionary<ushort, SeaLife.Kind> kinds = new Dictionary<ushort, SeaLife.Kind>();
        private static int rescan;

        public override void PostSetupContent()
        {
            kinds.Clear();

            for (int k = 0; k < SeaLife.Kinds; k++)
            {
                ushort type = SeaLifeTile.TypeOf((SeaLife.Kind)k);

                if (type != 0)
                    kinds[type] = (SeaLife.Kind)k;
            }
        }

        public override void Unload()
        {
            kinds.Clear();
            seen.Clear();
        }

        public override void OnWorldUnload() => seen.Clear();

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu || SubworldKeeper.Current is not VastOcean)
            {
                seen.Clear();
                return;
            }

            if (--rescan <= 0)
            {
                rescan = 20;
                Look();
            }

            for (int k = 0; k < seen.Count; k++)
                Give(seen[k]);
        }

        /// <summary>Each of them on the screen or within six tiles of it, by the top left cell of it.</summary>
        private static void Look()
        {
            seen.Clear();

            int x0 = System.Math.Max(1, (int)(Main.screenPosition.X / 16f) - 6);
            int y0 = System.Math.Max(1, (int)(Main.screenPosition.Y / 16f) - 6);
            int x1 = System.Math.Min(Main.maxTilesX - 2, x0 + Main.screenWidth / 16 + 12);
            int y1 = System.Math.Min(Main.maxTilesY - 2, y0 + Main.screenHeight / 16 + 12);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile || tile.TileType < TileID.Count || !kinds.TryGetValue(tile.TileType, out SeaLife.Kind kind))
                        continue;

                    int across = SeaLife.Wide(kind) * 18;

                    if (tile.TileFrameY != 0 || tile.TileFrameX % across != 0)
                        continue;

                    seen.Add(new Seen { X = x, Y = y, Kind = kind, Style = (byte)(tile.TileFrameX / across) });
                }
            }
        }

        private static void Give(Seen s)
        {
            Vector2 at = new Vector2(s.X * 16, s.Y * 16);
            float wide = SeaLife.Wide(s.Kind) * 16f, tall = SeaLife.Tall(s.Kind) * 16f;
            float roll = Main.rand.NextFloat();

            switch (s.Kind)
            {
                case SeaLife.Kind.VentMouth:
                    if (roll < 0.28f)
                        PADParticleHandler.SpawnParticle(new VentSmoke(at + new Vector2(8f + Main.rand.NextFloat(-3f, 3f), 9f), new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.9f, 1.5f)), Main.rand.Next(150, 261)));
                    else if (roll < 0.30f)
                        PADParticleHandler.SpawnParticle(new SeaFloc(at + new Vector2(8f, 6f), new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.6f, 1.1f)), Main.rand.Next(160, 280)));
                    break;

                case SeaLife.Kind.WhiteVentMouth:
                    if (roll < 0.12f)
                        PADParticleHandler.SpawnParticle(new VentShimmer(at + new Vector2(8f + Main.rand.NextFloat(-3f, 3f), 8f), new Vector2(0f, -Main.rand.NextFloat(1.2f, 2.0f)), Main.rand.Next(40, 81)));
                    break;

                case SeaLife.Kind.SeepVent:
                    // Gas comes in fits: a few bubbles together now and then.
                    if (roll < 0.03f)
                    {
                        int count = Main.rand.Next(1, 4);

                        for (int k = 0; k < count; k++)
                            VastOceanMotes.Seep(at + new Vector2(8f, 12f));
                    }
                    break;

                case SeaLife.Kind.GiantTubeWorms:
                case SeaLife.Kind.SeepTubeWorms:
                    if (roll < (s.Kind == SeaLife.Kind.GiantTubeWorms ? 0.02f : 0.008f))
                        PADParticleHandler.SpawnParticle(new SeaFloc(at + new Vector2(Main.rand.NextFloat(wide), Main.rand.NextFloat(tall * 0.5f)), new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.2f, 0.5f)), Main.rand.Next(180, 320)));
                    break;

                case SeaLife.Kind.CupSponge:
                    if (roll < 0.06f)
                        PADParticleHandler.SpawnParticle(new SeaFloc(at + new Vector2(wide * 0.5f + Main.rand.NextFloat(-3f, 3f), s.Style == 0 ? 8f : 15f), new Vector2(Main.rand.NextFloat(-0.08f, 0.08f), -Main.rand.NextFloat(0.8f, 1.3f)), Main.rand.Next(90, 160)));
                    break;

                case SeaLife.Kind.BubblegumCoral:
                case SeaLife.Kind.BambooCoral:
                case SeaLife.Kind.SeaPen:
                {
                    int cx = s.X + SeaLife.Wide(s.Kind) / 2, cy = s.Y + SeaLife.Tall(s.Kind) / 2;
                    float touch = SeaLifeTile.Touch(cx, cy);

                    if (roll < 0.002f + 0.16f * touch)
                        PADParticleHandler.SpawnParticle(new SeaGlow(at + new Vector2(Main.rand.NextFloat(wide), Main.rand.NextFloat(tall * 0.8f)), Main.rand.NextVector2Circular(0.4f, 0.4f) + new Vector2(0f, -0.2f), Main.rand.Next(40, 91)));
                    break;
                }

                case SeaLife.Kind.ColdWaterCoral:
                case SeaLife.Kind.BlackCoral:
                case SeaLife.Kind.SeaLily:
                case SeaLife.Kind.GlassSponge:
                case SeaLife.Kind.DeepAnemone:
                    if (roll < 0.012f)
                        PADParticleHandler.SpawnParticle(new SeaPlankton(at + new Vector2(Main.rand.NextFloat(-16f, wide + 16f), Main.rand.NextFloat(-16f, tall)), Main.rand.NextVector2Circular(0.25f, 0.18f), Main.rand.Next(180, 360)));
                    break;
            }
        }
    }
}
