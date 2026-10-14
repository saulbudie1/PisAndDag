using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// The second evil, buried.
    ///
    /// The world was made with one of the two; this is the other one, and it is not a
    /// biome. It is a shell about seven hundred tiles across and a hundred and eighty
    /// thick, wrapped around open air, two thirds of the way down to the Underworld
    /// under the beach, with thin arms taking hold of the rock in every direction but up.
    ///
    /// Nothing about it is drawn with a compass. The outer surface and the hollow inside
    /// it are both irregular — separately irregular, so the thickness varies as you walk
    /// round — and the floor you drop onto at the bottom is a thick bank of the same
    /// stone with water standing in it. Chasms run in the thickness and stop short of
    /// the middle. In the middle, twelve of them in a ring, evenly, at one radius.
    /// Vanilla never gives the player more than three together and never arranges them.
    ///
    /// See PAD_World_Plan.md §4.
    ///
    /// By request, there is a way down to it from the surface after all (TheWayDownPass): a
    /// narrow cave from the ground between the beach and the Dungeon to the top of the shaft,
    /// with a hall and the door partway down. Its own record has the request word for word.
    /// </summary>
    public class TheBallPass : GenPass
    {
        public const int Radius = 350;
        public const int Hollow = 170;

        /// <summary>How far the two surfaces are allowed to wander off a circle.</summary>
        private const double ShellRough = 0.13;
        public const double HollowRough = 0.26;

        /// <summary>The arms. Thin, many, and none of them straight.</summary>
        private const int ArmCount = 48;
        private const int ArmShortest = 150;
        private const int ArmLongest = 400;
        private const int ArmRoot = 6;

        /// <summary>How much longer and thicker the arms get when the Wall dies.</summary>
        private const float Again = 1.5f;

        /// <summary>It does not reach for the surface, and the player must have no way of knowing.</summary>
        private const int NeverAbove = 1500;

        /// <summary>Clearance from the one structure in range.</summary>
        private const int DungeonKeepOut = 60;

        private const int Orbs = 12;

        /// <summary>
        /// The ring, well inside the narrowest the hollow is allowed to get. Rim holds
        /// its own roughness to what it was asked for, so the hollow never comes in
        /// closer than Hollow * (1 - HollowRough) and the ring never ends up in the wall.
        /// </summary>
        public const int OrbRing = 105;

        /// <summary>
        /// The leaves of the Terraspark tree — every accessory the craft needs that the
        /// player cannot make from another accessory.
        ///
        /// Read out of Recipe.SetupRecipes rather than off the top of anyone's head,
        /// because the chain is longer than it looks and two of these are easy to forget:
        ///
        ///     Terraspark  = Frostspark + Lava Waders
        ///     Frostspark  = Lightning Boots + Ice Skates
        ///     Lightning   = Spectre Boots + Anklet of the Wind + Aglet
        ///     Spectre     = Hermes Boots + Rocket Boots
        ///     Lava Waders = Obsidian Water Walking Boots + Lava Charm + Obsidian Rose
        ///     Obsidian WW = Water Walking Boots + Obsidian Skull
        ///
        /// Nine, not six. The Anklet, the Aglet and the Rose are the ones that get left
        /// off a list written from memory.
        /// </summary>
        private static readonly int[] BootParts =
        {
            ItemID.HermesBoots,
            ItemID.RocketBoots,
            ItemID.AnkletoftheWind,
            ItemID.Aglet,
            ItemID.IceSkates,
            ItemID.WaterWalkingBoots,
            ItemID.ObsidianSkull,
            ItemID.LavaCharm,
            ItemID.ObsidianRose,
        };

        /// <summary>
        /// What else is in them. Nothing here is evil-specific; the evil-specific things
        /// are picked in Vessel, where it knows which evil it is.
        /// </summary>
        private static readonly int[] Miscellany =
        {
            ItemID.HealingPotion,
            ItemID.RecallPotion,
            ItemID.MiningPotion,
            ItemID.ShinePotion,
            ItemID.NightOwlPotion,
            ItemID.SwiftnessPotion,
            ItemID.RegenerationPotion,
            ItemID.SpelunkerPotion,
            ItemID.Bomb,
            ItemID.ThrowingKnife,
            ItemID.Glowstick,
        };

        /// <summary>
        /// The boot parts still unclaimed, shuffled once at the top of the ring and dealt
        /// out without repeats. Twelve chests each rolling their own item out of a pool of
        /// nine would hand the player three Aglets and no Lava Charm; dealing from one
        /// shuffled deck means every part that turns up is a part they did not have.
        /// </summary>
        private static readonly List<int> unclaimed = new();

        /// <summary>Chasms in the thickness. More than vanilla gives a whole biome.</summary>
        private const int ChasmFewest = 6;
        private const int ChasmMost = 10;

        /// <summary>The bank at the bottom, measured down from the middle of the hollow.</summary>
        public const int BaseFrom = 34;
        public const int BaseRough = 26;

        private static Rim shell;
        private static Rim hollow;

        /// <summary>The columns the ring's flues run up. Water is kept off them.</summary>
        private static readonly List<int> vents = new List<int>();

        public TheBallPass(string name, double weight) : base(name, weight) { }

        /// <summary>Where it came out. Saved with the world; hardmode needs it again.</summary>
        public static Point Centre { get; private set; }

        public static void Remember(Point at) => Centre = at;

        /// <summary>
        /// The top of the shaft, the one way in: the column it was begun in and the row it was
        /// begun at. Where the way down from the surface comes to it (TheWayDownPass). Not saved:
        /// in a world made before it was kept, the shaft is found again off the tiles.
        /// </summary>
        public static Point ShaftTop { get; private set; }

        /// <summary>How far the outer surface reaches from the middle at its farthest, and a little more: the way down keeps outside it.</summary>
        public static int ShellReach { get; private set; }

        public static void Forget()
        {
            Centre = Point.Zero;
            ShaftTop = Point.Zero;
            ShellReach = 0;
            shell = null;
            hollow = null;
        }

        /// <summary>
        /// The hardmode answer. Twice as many arms, **half again as long and half again
        /// as thick** as the ones the world was made with — 225 to 600 tiles instead of
        /// 150 to 400, nine tiles at the root instead of six — so the old arms are
        /// swallowed by new ones growing out past them.
        ///
        /// Still nothing above NeverAbove: the thing does not come up. The beach over it
        /// stays clean and the player goes on walking across it.
        /// </summary>
        public static void GrowArms(int cx, int cy)
        {
            ushort stone = Layout.BallStone;
            ushort wall = Layout.BallWall;

            for (int i = 0; i < ArmCount * 2; i++)
            {
                double angle = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                double up = Math.Max(0.0, -Math.Sin(angle));
                int length = (int)((ArmShortest + WorldGen.genRand.Next(ArmLongest - ArmShortest))
                                   * Again * (1.0 - 0.62 * up));

                double x = cx + Math.Cos(angle) * Radius;
                double y = cy + Math.Sin(angle) * Radius;
                double heading = angle;
                double sway = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                double swayRate = 0.03 + WorldGen.genRand.NextDouble() * 0.05;

                for (int step = 0; step < length; step++)
                {
                    sway += swayRate;
                    heading += (WorldGen.genRand.NextDouble() - 0.5) * 0.16 + Math.Sin(sway) * 0.02;
                    x += Math.Cos(heading);
                    y += Math.Sin(heading);

                    if (y < NeverAbove || !WorldGen.InWorld((int)x, (int)y, 40))
                        break;

                    float thickness = MathHelper.Lerp(ArmRoot * Again, Again, step / (float)length)
                                    + (WorldGen.genRand.NextBool(6) ? 2f : 0f);

                    Blob((int)x, (int)y, thickness, stone, wall, frame: true);
                }
            }

            GenLog.Log?.Info($"The Ball spread at {cx}, {cy}: {ArmCount * 2} arms at {Again:0.0}x the first ones.");
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Something under the sand";

            if (!ExtraLargeWorld.Active)
                return;

            int cx = WorldMirror.X(Layout.Wobble(Layout.BallX, 90));
            int cy = (int)(Main.worldSurface + (Main.UnderworldLayer - Main.worldSurface) * 2.0 / 3.0);
            cy += WorldGen.genRand.Next(-60, 61);

            // Keep the whole shell, arms aside, inside the world and out of the hell roof.
            cx = Math.Clamp(cx, Radius + 90, Main.maxTilesX - Radius - 90);

            // And out of the Dungeon by construction rather than by trimming afterwards:
            // the shell is 350 across and the Dungeon spreads a couple of hundred either
            // side of its entrance, so put a clear gap between the two centres.
            int gap = Radius + 250;

            if (Math.Abs(cx - GenVars.dungeonLocation) < gap)
                cx = GenVars.dungeonLocation + (WorldMirror.Flipped ? gap : -gap);

            cx = Math.Clamp(cx, Radius + 90, Main.maxTilesX - Radius - 90);
            cy = Math.Clamp(cy, (int)Main.rockLayer + Radius + 90, Main.UnderworldLayer - Radius - 140);
            Centre = new Point(cx, cy);

            // Two surfaces, rolled apart, so the thickness is different everywhere round.
            vents.Clear();
            shell = new Rim(Radius, ShellRough, 5);
            hollow = new Rim(Hollow, HollowRough, 6);
            ShellReach = (int)Math.Ceiling(shell.Widest) + 4;

            ushort stone = Layout.BallStone;
            ushort wall = Layout.BallWall;

            Arms(progress, cx, cy, stone, wall);
            Shell(progress, cx, cy, stone, wall);

            // The bank first, then the ring set into it, and the water last of all: the
            // ring cuts alcoves through the bank and would drain a pool it opened into.
            int[] bank = Chamber(cx, cy, stone);
            Chasms(cx, cy, wall);
            Shaft(cx, cy, stone);
            Ring(cx, cy, stone);
            Pools(cx, cy, HollowReach, bank);
            Ore(cx, cy);
            Dress(progress, cx, cy);

            GenVars.structures.AddProtectedStructure(
                new Rectangle(cx - Radius, cy - Radius, Radius * 2, Radius * 2), 20);

            GenLog.Log?.Info($"The Ball: {(Layout.BallIsCrimson ? "crimson" : "corruption")} at {cx}, {cy}.");
        }

        // ------------------------------------------------------------------- the arms

        /// <summary>
        /// Forty-eight random walks off the outer surface. Each one drifts and sways as
        /// it goes, so they curl rather than radiate, and each tapers from six tiles to
        /// one. The starting angles are rolled rather than stepped, so the roots are
        /// bunched in some directions and thin in others.
        /// </summary>
        private static void Arms(GenerationProgress progress, int cx, int cy, ushort stone, ushort wall)
        {
            for (int i = 0; i < ArmCount; i++)
            {
                progress.Set(i / (float)ArmCount * 0.30f);

                double angle = i / (double)ArmCount * MathHelper.TwoPi
                             + (WorldGen.genRand.NextDouble() - 0.5) * (MathHelper.TwoPi / ArmCount) * 1.8;

                // Upward arms are cut back hard. The thing stays down.
                double up = Math.Max(0.0, -Math.Sin(angle));
                int length = (int)((ArmShortest + WorldGen.genRand.Next(ArmLongest - ArmShortest)) * (1.0 - 0.62 * up));

                double reach = shell.At(angle) - 4;
                double x = cx + Math.Cos(angle) * reach;
                double y = cy + Math.Sin(angle) * reach;
                double heading = angle;
                double sway = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                double swayRate = 0.03 + WorldGen.genRand.NextDouble() * 0.05;

                for (int step = 0; step < length; step++)
                {
                    sway += swayRate;
                    heading += (WorldGen.genRand.NextDouble() - 0.5) * 0.16 + Math.Sin(sway) * 0.02;
                    x += Math.Cos(heading);
                    y += Math.Sin(heading);

                    if (y < NeverAbove || !WorldGen.InWorld((int)x, (int)y, 40))
                        break;

                    float thickness = MathHelper.Lerp(ArmRoot, 1f, step / (float)length)
                                    + (WorldGen.genRand.NextBool(7) ? 1.5f : 0f);

                    Blob((int)x, (int)y, thickness, stone, wall);
                }
            }
        }

        private static void Blob(int cx, int cy, float radius, ushort stone, ushort wall, bool frame = false)
        {
            int r = Math.Max(1, (int)Math.Ceiling(radius));

            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (!Allowed(x, y) || Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) > radius)
                        continue;

                    Tile t = Main.tile[x, y];

                    if (!t.HasTile && t.WallType == 0)
                        continue;           // leave open air open; the arms grip rock, they do not fill caves

                    if (t.HasTile && Rock(t.TileType))
                        t.TileType = stone;
                    if (t.WallType != 0 && NaturalWall(t.WallType))
                        t.WallType = wall;

                    // During generation the framing pass tidies up afterwards. Growing at
                    // hardmode does not get one, so it tidies up after itself.
                    if (frame)
                        WorldGen.SquareTileFrame(x, y, true);
                }
            }
        }

        /// <summary>
        /// Ground the arms are allowed to take hold of.
        ///
        /// An arm is a random walk six hundred tiles long and it will happily cross the
        /// Dungeon on the way — which is what it did: a crimson vein straight through the
        /// pink brick, floor to ceiling. **The arms grow through rock. They do not eat
        /// anything anybody built.** So this is a list of what counts as ground, and
        /// everything else — brick, wood, dungeon, temple, ore, furniture, the Ball's own
        /// orbs — is passed over. A whitelist rather than a blacklist on purpose: a new
        /// tile nobody thought about should be left alone, not chewed.
        /// </summary>
        private static bool Rock(ushort type) => type switch
        {
            TileID.Dirt or TileID.Stone or TileID.Grass or TileID.ClayBlock
                or TileID.Silt or TileID.Slush or TileID.Mud or TileID.Mudstone
                or TileID.Sand or TileID.HardenedSand or TileID.Sandstone
                or TileID.SnowBlock or TileID.IceBlock or TileID.CorruptIce or TileID.FleshIce
                or TileID.Granite or TileID.Marble or TileID.Ash
                or TileID.JungleGrass or TileID.CorruptGrass or TileID.CrimsonGrass
                or TileID.Ebonstone or TileID.Crimstone or TileID.Ebonsand or TileID.Crimsand
                or TileID.CorruptHardenedSand or TileID.CrimsonHardenedSand
                or TileID.CorruptSandstone or TileID.CrimsonSandstone
                or TileID.CorruptJungleGrass or TileID.CrimsonJungleGrass => true,
            _ => false,
        };

        /// <summary>
        /// Backgrounds the arms may stain. The same rule: cavern walls yes, anything with
        /// a mason's hand in it no — and a Dungeon wall is how the game knows it is the
        /// Dungeon, so recolouring one does more than look wrong.
        /// </summary>
        private static bool NaturalWall(ushort wall) => wall switch
        {
            WallID.Stone or WallID.DirtUnsafe or WallID.Dirt
                or WallID.GrassUnsafe or WallID.MudUnsafe or WallID.JungleUnsafe
                or WallID.CaveUnsafe or WallID.Cave2Unsafe or WallID.Cave3Unsafe
                or WallID.Cave4Unsafe or WallID.Cave5Unsafe or WallID.Cave6Unsafe
                or WallID.Cave7Unsafe or WallID.Cave8Unsafe
                or WallID.CaveWall or WallID.CaveWall2
                or WallID.RocksUnsafe1 or WallID.RocksUnsafe2
                or WallID.RocksUnsafe3 or WallID.RocksUnsafe4
                or WallID.EbonstoneUnsafe or WallID.CrimstoneUnsafe
                or WallID.CorruptGrassUnsafe or WallID.CrimsonGrassUnsafe
                or WallID.CorruptionUnsafe1 or WallID.CorruptionUnsafe2
                or WallID.CorruptionUnsafe3 or WallID.CorruptionUnsafe4
                or WallID.CrimsonUnsafe1 or WallID.CrimsonUnsafe2
                or WallID.CrimsonUnsafe3 or WallID.CrimsonUnsafe4
                or WallID.SnowWallUnsafe or WallID.IceUnsafe
                or WallID.MarbleUnsafe or WallID.GraniteUnsafe
                or WallID.Sandstone or WallID.HardenedSand => true,
            _ => false,
        };

        // ------------------------------------------------------------------ the shell

        /// <summary>
        /// The body of it. One sweep of the bounding box: outside the outer surface,
        /// nothing happens; inside the hollow, nothing happens yet; in between is stone.
        /// Both surfaces get a tile of grain on top of the shape, so neither of them is
        /// a curve you could have drawn.
        /// </summary>
        private static void Shell(GenerationProgress progress, int cx, int cy, ushort stone, ushort wall)
        {
            int reach = (int)Math.Ceiling(shell.Widest) + 6;

            for (int x = cx - reach; x <= cx + reach; x++)
            {
                progress.Set(0.30f + (x - cx + reach) / (float)(reach * 2) * 0.30f);

                for (int y = cy - reach; y <= cy + reach; y++)
                {
                    if (!Allowed(x, y))
                        continue;

                    double dx = x - cx, dy = y - cy;
                    double d = Math.Sqrt(dx * dx + dy * dy);

                    double outer = shell.Toward(dx, dy) + Layout.Grain(x, y, 3);

                    if (d > outer)
                        continue;

                    double inner = hollow.Toward(dx, dy) + Layout.Grain(x, y, 2) * 1.5;

                    if (d <= inner)
                        continue;       // the hollow is cut in Chamber, with the bank left in it

                    Tile t = Main.tile[x, y];
                    t.HasTile = true;
                    t.TileType = stone;
                    t.WallType = wall;
                    t.LiquidAmount = 0;
                    t.Slope = SlopeType.Solid;
                    t.IsHalfBlock = false;
                }
            }
        }

        // ---------------------------------------------------------------- the chamber

        /// <summary>
        /// The middle, and the bank you fall onto.
        ///
        /// Everything inside the hollow surface is opened, and then a thick bank of the
        /// same stone is left standing in the bottom of it with a rough top and water
        /// lying in the dips. No floor was laid. Whatever emptied this place did not
        /// tidy up after itself.
        /// </summary>
        private static int HollowReach => (int)Math.Ceiling(hollow.Widest) + 6;

        private static int[] Chamber(int cx, int cy, ushort stone)
        {
            int reach = HollowReach;
            int span = reach * 2 + 1;

            // The top of the bank, as a slow random walk with a shelf every so often.
            int[] bank = new int[span];
            int level = cy + BaseFrom;
            int flat = 0;

            for (int i = 0; i < span; i++)
            {
                if (flat > 0)
                {
                    flat--;
                }
                else
                {
                    level = Math.Clamp(level + WorldGen.genRand.Next(-2, 3), cy + BaseFrom - BaseRough, cy + BaseFrom + BaseRough);

                    if (WorldGen.genRand.NextBool(11))
                        flat = WorldGen.genRand.Next(3, 14);
                }

                bank[i] = level;
            }

            for (int x = cx - reach; x <= cx + reach; x++)
            {
                int top = bank[x - cx + reach] + Layout.Grain(x, 7, 2);

                for (int y = cy - reach; y <= cy + reach; y++)
                {
                    if (!Allowed(x, y))
                        continue;

                    double dx = x - cx, dy = y - cy;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double inner = hollow.Toward(dx, dy) + Layout.Grain(x, y, 2) * 1.5;

                    if (d > inner)
                        continue;

                    Tile t = Main.tile[x, y];
                    t.LiquidAmount = 0;

                    if (y >= top)
                    {
                        t.HasTile = true;
                        t.TileType = stone;
                        t.Slope = SlopeType.Solid;
                        t.IsHalfBlock = false;
                    }
                    else
                    {
                        t.HasTile = false;
                    }

                    if (t.WallType == 0)
                        t.WallType = Layout.BallCaveWall();
                }
            }

            return bank;
        }

        /// <summary>
        /// Water standing in the bank. The basins are cut into it rather than sat on top
        /// of it, and the lip is taken from the lowest ground around the cut, so nothing
        /// runs out of them when the world settles its liquids an hour later.
        /// </summary>
        private static void Pools(int cx, int cy, int reach, int[] bank)
        {
            int pools = WorldGen.genRand.Next(8, 15);

            for (int n = 0; n < pools; n++)
            {
                int half = WorldGen.genRand.Next(14, 42);
                int deep = WorldGen.genRand.Next(8, 20);
                int room = Math.Max(1, reach - half - 40);
                int at = cx + WorldGen.genRand.Next(-room, room + 1);

                // Not over one of the ring's flues, or the basin drains down it into a
                // niche and leaves a dry hole where the water was.
                for (int tries = 0; tries < 8 && Flued(at, half); tries++)
                    at = cx + WorldGen.genRand.Next(-room, room + 1);

                if (Flued(at, half))
                    continue;

                // The lip is the lowest point of the ground the basin is cut into.
                int lip = int.MinValue;

                for (int x = at - half; x <= at + half; x++)
                {
                    int i = x - cx + reach;

                    if (i < 0 || i >= bank.Length)
                        continue;

                    if (bank[i] > lip)
                        lip = bank[i];
                }

                if (lip == int.MinValue)
                    continue;

                for (int x = at - half; x <= at + half; x++)
                {
                    double across = (x - at) / (double)half;
                    int bowl = (int)(deep * Math.Sqrt(Math.Max(0.0, 1.0 - across * across)));

                    for (int y = lip + 1; y <= lip + bowl; y++)
                    {
                        if (!Allowed(x, y))
                            continue;

                        Tile t = Main.tile[x, y];
                        t.HasTile = false;
                        t.Slope = SlopeType.Solid;
                        t.IsHalfBlock = false;
                        t.LiquidType = LiquidID.Water;
                        t.LiquidAmount = 255;
                    }
                }
            }
        }

        /// <summary>Does this basin sit over a column the ring bored a flue up?</summary>
        private static bool Flued(int at, int half)
        {
            foreach (int column in vents)
            {
                if (column >= at - half - 2 && column <= at + half + 2)
                    return true;
            }

            return false;
        }

        // ----------------------------------------------------------------- the chasms

        /// <summary>
        /// The danger lives in the thickness.
        ///
        /// Six to ten chasms, cut the way vanilla cuts them — a wandering shaft that
        /// widens into pockets — except these run inside the shell rather than down from
        /// the surface, and none of them reaches the chamber. The way into the middle is
        /// the shaft and only the shaft; these are what the player finds instead while
        /// looking for it.
        /// </summary>
        private static void Chasms(int cx, int cy, ushort wall)
        {
            int count = WorldGen.genRand.Next(ChasmFewest, ChasmMost + 1);

            for (int n = 0; n < count; n++)
            {
                double angle = n / (double)count * MathHelper.TwoPi + WorldGen.genRand.NextDouble() * 0.9;

                // Start just inside the outer surface and work inward, stopping well
                // short of the hollow.
                double reach = shell.At(angle) - 20;
                double x = cx + Math.Cos(angle) * reach;
                double y = cy + Math.Sin(angle) * reach;
                double heading = angle + Math.PI;        // inward
                double width = WorldGen.genRand.Next(4, 9);

                for (int step = 0; step < 700; step++)
                {
                    heading += (WorldGen.genRand.NextDouble() - 0.5) * 0.30;
                    x += Math.Cos(heading);
                    y += Math.Sin(heading);

                    double dx = x - cx, dy = y - cy;
                    double fromCentre = Math.Sqrt(dx * dx + dy * dy);
                    double stop = hollow.Toward(dx, dy) + 22;

                    if (fromCentre < stop || fromCentre > shell.Toward(dx, dy) - 4)
                        break;

                    width = Math.Clamp(width + WorldGen.genRand.Next(-1, 2), 3, 12);

                    // Every so often it opens out. Vanilla chasms do this and it is what
                    // makes them worth walking rather than just falling down.
                    double here = WorldGen.genRand.NextBool(18) ? width * 2.4 : width;
                    Carve((int)x, (int)y, (float)here, wall);
                }
            }
        }

        private static void Carve(int cx, int cy, float radius, ushort wall)
        {
            int r = (int)Math.Ceiling(radius);

            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (!Allowed(x, y))
                        continue;

                    float edge = radius + Layout.Grain(x, y, 1);

                    if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) > edge)
                        continue;

                    Tile t = Main.tile[x, y];
                    t.HasTile = false;
                    t.LiquidAmount = 0;
                    t.WallType = wall;      // the walls stay, so it reads as carved and not as a gap
                }
            }
        }

        /// <summary>One way in, cut from above. It is old enough to be anybody's.</summary>
        private static void Shaft(int cx, int cy, ushort stone)
        {
            int half = 3;
            int x = cx + WorldGen.genRand.Next(-40, 41);

            ShaftTop = new Point(x, cy - (int)shell.Widest - 40);

            for (int y = cy - (int)shell.Widest - 40; y < cy; y++)
            {
                if (WorldGen.genRand.NextBool(24))
                    x += WorldGen.genRand.Next(-1, 2);

                double dy = y - cy;

                if (Math.Sqrt((x - cx) * (double)(x - cx) + dy * dy) < hollow.Toward(x - cx, dy) - 2)
                    break;              // through the roof of the hollow; that is far enough

                for (int i = x - half; i <= x + half; i++)
                {
                    if (!Allowed(i, y))
                        continue;

                    Tile t = Main.tile[i, y];

                    if (t.HasTile && t.TileType == stone && (i == x - half || i == x + half))
                        continue;           // leave the shaft's own walls as they are

                    t.HasTile = false;
                    t.LiquidAmount = 0;

                    if (t.WallType == 0)
                        t.WallType = Layout.BallCaveWall();
                }
            }
        }

        // -------------------------------------------------------------------- the ring

        /// <summary>
        /// Twelve, evenly, at one radius: a circle, not a row. Somebody counted them out
        /// and that is the whole point of them, so the ring is what is regular here and
        /// nothing else in the room is.
        ///
        /// Each one is set into a knuckle of the same stone with an alcove cut out in
        /// front of it. Above the bank that reads as a nodule hanging in the dark; in the
        /// lower half of the ring, where the bank has already filled the space, it reads
        /// as a niche somebody opened. Both are the same four lines of code.
        /// </summary>
        private static void Ring(int cx, int cy, ushort stone)
        {
            int placed = 0;
            int stocked = 0;

            bool ballIsCrimson = Layout.BallIsCrimson;

            // One deck per ring, shuffled here so Vessel only ever has to take the top of
            // it. Cleared first: a static outlives the world that filled it.
            unclaimed.Clear();
            unclaimed.AddRange(BootParts);

            for (int n = unclaimed.Count - 1; n > 0; n--)
            {
                int k = WorldGen.genRand.Next(n + 1);
                (unclaimed[n], unclaimed[k]) = (unclaimed[k], unclaimed[n]);
            }

            for (int i = 0; i < Orbs; i++)
            {
                double angle = i / (double)Orbs * MathHelper.TwoPi - MathHelper.PiOver2;
                int x = cx + (int)Math.Round(Math.Cos(angle) * OrbRing);
                int y = cy + (int)Math.Round(Math.Sin(angle) * OrbRing);

                Knuckle(x, y, stone);

                if (Vessel(x, y, ballIsCrimson))
                    stocked++;

                if (!Allowed(x, y))
                    continue;

                Tile check = Main.tile[x, y];

                // Read the world back rather than the intention. The version of this
                // that counted nothing but the tile type printed "12 of 12 Crimson
                // Hearts, frame 36" over a ring of twelve Shadow Orbs and read as proof
                // that it worked. A check that cannot fail is not a check.
                if (check.HasTile && check.TileType == TileID.Containers)
                {
                    placed++;
                    Vent(x, y);
                }
            }

            GenLog.Log?.Info(
                $"The Ball: {placed} of {Orbs} chests stood in the ring, {stocked} of them stocked. " +
                $"{(ballIsCrimson ? "Shadewood, Bloody Spine, Crimson Torches" : "Ebonwood, Worm Food, Corrupt Torches")}; " +
                $"world evil {(WorldGen.crimson ? "crimson" : "corruption")}.");
        }

        /// <summary>
        /// The chest set into one knuckle of the ring, and what is kept in it.
        ///
        /// It used to be a Shadow Orb, written by hand so that frameX alone decided
        /// which of the two it was — 36 and up for a Heart, below for an Orb, which is
        /// the one line WorldGen.CheckOrb reads and the reason a single world can hold
        /// both kinds. That worked at the moment it ran and did not survive to the save:
        /// the world generation preview showed Hearts and the loaded world held Orbs,
        /// and the frames in the .wld came back 0 across all twelve. Something late puts
        /// a hand-framed orb back to style zero.
        ///
        /// So the frame is not written by hand any more. WorldGen.PlaceChest is vanilla's
        /// own placer, it goes through TileObject.Place, and it frames the object the way
        /// the game frames every other chest in the world. Nothing here can disagree with
        /// it, because nothing here does the framing.
        ///
        /// The style is read off the item rather than typed in. ContentSamples holds a
        /// filled-in copy of every item in the game, so the Ebonwood Chest itself says
        /// which tile it places and in which style, and a magic number that could drift
        /// against an update never enters this file.
        ///
        /// (left, bottom) is the chest's bottom-left tile — PlaceChest looks at
        /// (x, y + 1) and (x + 1, y + 1) for what holds it up, and Knuckle has already
        /// laid that ledge. The footprint is the same four tiles the orb used.
        /// </summary>
        private static bool Vessel(int left, int bottom, bool crimson)
        {
            if (left < 10 || left + 1 > Main.maxTilesX - 10
                || bottom < 10 || bottom + 1 > Main.maxTilesY - 10)
                return false;

            // The wood of whichever evil this is. Ebonwood is the Corruption's, Shadewood
            // the Crimson's, and the ring is made of the evil the world is NOT.
            Item sample = ContentSamples.ItemsByType[crimson ? ItemID.ShadewoodChest : ItemID.EbonwoodChest];

            if (sample.createTile <= 0)
                return false;

            int index = WorldGen.PlaceChest(left, bottom, (ushort)sample.createTile, false, sample.placeStyle);

            if (index < 0)
                return false;

            Chest chest = Main.chest[index];
            int slot = 0;

            // What the ring is for. One summon each, so the twelve are twelve goes at the
            // boss of a biome the world was never supposed to have, and the player has
            // them before the biome does.
            Put(chest, ref slot, crimson ? ItemID.BloodySpine : ItemID.WormFood, 1);

            // The rest of what somebody would have left down here.
            Put(chest, ref slot, ItemID.Rope, WorldGen.genRand.Next(25, 76));
            Put(chest, ref slot, crimson ? ItemID.Vertebrae : ItemID.RottenChunk, WorldGen.genRand.Next(3, 10));
            Put(chest, ref slot, crimson ? ItemID.ViciousPowder : ItemID.VilePowder, WorldGen.genRand.Next(10, 26));
            Put(chest, ref slot, crimson ? ItemID.CrimtaneOre : ItemID.DemoniteOre, WorldGen.genRand.Next(8, 21));
            Put(chest, ref slot, ItemID.SilverCoin, WorldGen.genRand.Next(20, 91));

            // Two out of the odds and ends, and never the same thing twice in one chest —
            // a chest holding two Night Owl Potions reads as a bug rather than as luck.
            int first = WorldGen.genRand.Next(Miscellany.Length);
            int second = (first + 1 + WorldGen.genRand.Next(Miscellany.Length - 1)) % Miscellany.Length;

            Put(chest, ref slot, Miscellany[first], WorldGen.genRand.Next(3, 9));
            Put(chest, ref slot, Miscellany[second], WorldGen.genRand.Next(3, 9));

            // One chest in five is carrying a piece of the boots, off the top of the deck
            // the ring shuffled. Twelve chests at one in five is between two and three
            // parts in an average world, so the ring seeds the craft; it does not finish
            // it. Whatever turns up is a part no other chest in this ring has.
            if (WorldGen.genRand.NextBool(5) && unclaimed.Count > 0)
            {
                Put(chest, ref slot, unclaimed[0], 1);
                unclaimed.RemoveAt(0);
            }

            // A torch either side, stood on the ledge Knuckle laid, so the niche is lit
            // and the chest can be seen from across the chamber. The chest is two wide
            // from left, so left - 1 and left + 2 are the first columns clear of it.
            Lamp(left - 1, bottom, crimson);
            Lamp(left + 2, bottom, crimson);

            return true;
        }

        /// <summary>
        /// One torch of whichever evil this is, on the floor beside a chest.
        ///
        /// The style comes off the item for the same reason the chest's does: the Corrupt
        /// Torch itself knows which tile it places and in which style, so no number here
        /// can fall out of step with an update. PlaceTile does the framing and the anchor
        /// check, so a spot with nothing to stand on simply does not get a torch.
        /// </summary>
        private static void Lamp(int x, int y, bool crimson)
        {
            if (!Allowed(x, y))
                return;

            Item sample = ContentSamples.ItemsByType[crimson ? ItemID.CrimsonTorch : ItemID.CorruptTorch];

            if (sample.createTile <= 0)
                return;

            WorldGen.PlaceTile(x, y, sample.createTile, true, false, -1, sample.placeStyle);
        }

        /// <summary>One item into the next free slot, if there is one left.</summary>
        private static void Put(Chest chest, ref int slot, int type, int stack)
        {
            if (slot >= Chest.maxItems)
                return;

            chest.item[slot] = new Item();
            chest.item[slot].SetDefaults(type);
            chest.item[slot].stack = Math.Max(1, stack);
            slot++;
        }

        /// <summary>
        /// One tile of air, straight up, from a niche that is otherwise sealed.
        ///
        /// The lower half of the ring is inside the bank, so those six are walled in on
        /// every side and a player standing in the chamber has no way of knowing they are
        /// there. Each gets a single-tile flue up to whatever open air is above it — the
        /// chamber, a pool, a chasm — which is enough to see the glow through and narrow
        /// enough that it reads as something that was bored rather than built.
        ///
        /// It stops at the first tile that is already open, so the six hanging in the
        /// chamber never get one: the loop ends before it removes anything.
        /// </summary>
        private static void Vent(int x, int y)
        {
            for (int up = 4; up < Hollow + 120; up++)
            {
                int at = y - up;

                if (!Allowed(x, at))
                    return;

                Tile t = Main.tile[x, at];

                if (!t.HasTile)
                    return;             // open air; the flue is through

                t.HasTile = false;
                t.LiquidAmount = 0;

                if (t.WallType == 0)
                    t.WallType = Layout.BallCaveWall();

                WorldGen.SquareTileFrame(x, at, true);

                if (!vents.Contains(x))
                    vents.Add(x);
            }
        }

        /// <summary>A lump of the stone with a hole in the front of it and a ledge in the bottom.</summary>
        private static void Knuckle(int x, int y, ushort stone)
        {
            // The lump. Rough enough not to read as a box.
            for (int dx = -5; dx <= 6; dx++)
            {
                for (int dy = -5; dy <= 6; dy++)
                {
                    if (!Allowed(x + dx, y + dy))
                        continue;

                    double away = Math.Sqrt((dx - 0.5) * (dx - 0.5) + (dy - 0.5) * (dy - 0.5));

                    if (away > 5.0 + Layout.Grain(x + dx, y + dy, 1))
                        continue;

                    Tile t = Main.tile[x + dx, y + dy];
                    t.HasTile = true;
                    t.TileType = stone;
                    t.Slope = SlopeType.Solid;
                    t.IsHalfBlock = false;
                    t.LiquidAmount = 0;

                    if (t.WallType == 0)
                        t.WallType = Layout.BallWall;
                }
            }

            // The alcove: the square the orb goes in, and a little air over it.
            for (int dx = -1; dx <= 2; dx++)
            {
                for (int dy = -3; dy <= 0; dy++)
                {
                    if (!Allowed(x + dx, y + dy))
                        continue;

                    Tile t = Main.tile[x + dx, y + dy];
                    t.HasTile = false;
                    t.LiquidAmount = 0;

                    if (t.WallType == 0)
                        t.WallType = Layout.BallCaveWall();
                }
            }

            // The ledge it stands on.
            for (int dx = -1; dx <= 2; dx++)
            {
                if (!Allowed(x + dx, y + 1))
                    continue;

                Tile t = Main.tile[x + dx, y + 1];
                t.HasTile = true;
                t.TileType = stone;
                t.Slope = SlopeType.Solid;
                t.IsHalfBlock = false;
                t.LiquidAmount = 0;
            }
        }

        // ------------------------------------------------------------- what grew in it

        /// <summary>Veins in the thickness. The ore of whichever one of the two this is.</summary>
        private static void Ore(int cx, int cy)
        {
            int veins = WorldGen.genRand.Next(14, 30);

            for (int n = 0; n < veins; n++)
            {
                double angle = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                double inner = hollow.At(angle) + 20;
                double outer = shell.At(angle) - 20;

                if (outer <= inner)
                    continue;

                double at = inner + WorldGen.genRand.NextDouble() * (outer - inner);
                int x = cx + (int)(Math.Cos(angle) * at);
                int y = cy + (int)(Math.Sin(angle) * at);

                if (!Allowed(x, y))
                    continue;

                WorldGen.TileRunner(x, y, WorldGen.genRand.Next(4, 10), WorldGen.genRand.Next(5, 16),
                    Layout.BallOre, false, 0.0, 0.0, false, true, -1);
            }
        }

        /// <summary>
        /// What grows on it where it is open. Nobody planted any of this: the shell is
        /// the biome, so it carries the biome's own grass, its vines and its thorns
        /// wherever a chasm or the chamber has given them a face to grow off.
        /// </summary>
        private static void Dress(GenerationProgress progress, int cx, int cy)
        {
            int reach = (int)Math.Ceiling(shell.Widest) + 4;
            ushort stone = Layout.BallStone;
            ushort grass = Layout.BallGrass;

            for (int x = cx - reach; x <= cx + reach; x++)
            {
                progress.Set(0.60f + (x - cx + reach) / (float)(reach * 2) * 0.40f);

                for (int y = cy - reach; y <= cy + reach; y++)
                {
                    if (!Allowed(x, y) || !Allowed(x, y - 1) || !Allowed(x, y + 1))
                        continue;

                    Tile here = Main.tile[x, y];

                    if (!here.HasTile || here.TileType != stone)
                        continue;

                    Tile above = Main.tile[x, y - 1];
                    Tile below = Main.tile[x, y + 1];

                    bool openAbove = !above.HasTile && above.LiquidAmount == 0;
                    bool openBelow = !below.HasTile && below.LiquidAmount == 0;

                    if (openAbove && WorldGen.genRand.NextBool(4))
                    {
                        here.TileType = grass;
                        WorldGen.SquareTileFrame(x, y, true);

                        if (WorldGen.genRand.NextBool(3))
                            WorldGen.PlaceTile(x, y - 1, Layout.BallThorns, true, false, -1, 0);
                        else
                            WorldGen.PlaceTile(x, y - 1, Layout.BallPlants, true, false, -1, 0);

                        continue;
                    }

                    if (openBelow && WorldGen.genRand.NextBool(5))
                    {
                        here.TileType = grass;
                        WorldGen.SquareTileFrame(x, y, true);

                        int hang = WorldGen.genRand.Next(1, 7);

                        for (int n = 1; n <= hang; n++)
                        {
                            if (!Allowed(x, y + n))
                                break;

                            Tile at = Main.tile[x, y + n];

                            if (at.HasTile || at.LiquidAmount != 0)
                                break;

                            WorldGen.PlaceTile(x, y + n, Layout.BallVines, true, false, -1, 0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The arms wander and the Dungeon is the one thing down here that was already
        /// built, so they are told to stay off it. The shell never gets near enough to
        /// need this; the arms do.
        /// </summary>
        private static bool Allowed(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 40) || y >= Main.UnderworldLayer - 20)
                return false;

            bool nearDungeonColumn = Math.Abs(x - GenVars.dungeonLocation) < DungeonKeepOut + 220;
            bool atDungeonDepth = y < Main.rockLayer + 900;

            return !(nearDungeonColumn && atDungeonDepth);
        }
    }

    /// <summary>
    /// The third force, in the rock.
    ///
    /// Somewhere in the cavern under the Cleared Ground there is a cave like any other
    /// cave, except that the rock around it is Pearlstone, three hundred years before the
    /// Hallow has any business existing. Nothing was built and nothing was dug: the
    /// stone that was already there is simply the wrong stone, in one small continuous
    /// patch that thins out at the edges into the ordinary grey.
    ///
    /// It is not the Seal. §XXVII has exactly one of those, it is in the Underworld, and
    /// it is the Wall. This is something else, and the build does not say whose.
    /// </summary>
    public class SealedPocketPass : GenPass
    {
        /// <summary>Small. It is a patch of the wrong rock, not a room.</summary>
        private const int Smallest = 70;
        private const int Largest = 115;

        /// <summary>How far in from the edge the patch is solidly itself.</summary>
        private const int Fade = 14;

        /// <summary>How many places are looked at before one is taken.</summary>
        private const int Tries = 80;

        public SealedPocketPass(string name, double weight) : base(name, weight) { }

        public static Point Centre { get; private set; }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Something that should not be here yet";

            if (!ExtraLargeWorld.Active)
                return;

            int size = WorldGen.genRand.Next(Smallest, Largest + 1);
            Rim edge = new Rim(size, 0.30, 5);

            int reach = (int)Math.Ceiling(edge.Widest) + 4;
            Point at = Somewhere(reach);

            if (at == Point.Zero)
            {
                GenLog.Log?.Warn("The Sealed Pocket found nowhere in the cavern under the Cleared Ground that was not already spoken for. It is not in this world.");
                return;
            }

            Centre = at;
            int cx = at.X, cy = at.Y;

            for (int x = cx - reach; x <= cx + reach; x++)
            {
                progress.Set((x - cx + reach) / (float)(reach * 2));

                for (int y = cy - reach; y <= cy + reach; y++)
                {
                    if (!WorldGen.InWorld(x, y, 40))
                        continue;

                    double dx = x - cx, dy = y - cy;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double rim = edge.Toward(dx, dy) + Layout.Grain(x, y, 2);

                    if (d > rim)
                        continue;

                    // Solid through the middle, dissolving back into grey at the rim, so
                    // it is one mass with a ragged edge rather than a circle with a line
                    // round it.
                    if (d > rim - Fade && WorldGen.genRand.NextDouble() > (rim - d) / Fade)
                        continue;

                    Paint(x, y);
                }
            }

            GenVars.structures.AddProtectedStructure(new Rectangle(cx - reach, cy - reach, reach * 2, reach * 2), 6);

            GenLog.Log?.Info($"The Sealed Pocket: {cx}, {cy}, about {reach * 2} across.");
        }

        /// <summary>
        /// A cave in the cavern under the Cleared Ground, with nothing else already in
        /// it. Openness is what is being looked for: the patch is meant to have a cave
        /// in it, because a wall of Pearlstone the player can only see by mining into it
        /// is a wall of Pearlstone nobody ever sees.
        /// </summary>
        private static Point Somewhere(int reach)
        {
            (int left, int right) = Layout.Cleared;
            left = Math.Max(reach + 40, left + 60);
            right = Math.Min(Main.maxTilesX - reach - 40, right - 60);

            if (right <= left)
                return Point.Zero;

            int top = (int)Main.rockLayer + 200;
            int bottom = Math.Min(Main.UnderworldLayer - reach - 200, (int)Main.rockLayer + 1000);

            if (bottom <= top)
                return Point.Zero;

            Point best = Point.Zero;
            int most = 0;

            for (int n = 0; n < Tries; n++)
            {
                int x = WorldGen.genRand.Next(left, right);
                int y = WorldGen.genRand.Next(top, bottom);

                if (Clashes(x, y, reach))
                    continue;

                int open = 0;

                for (int sx = x - reach / 2; sx <= x + reach / 2; sx += 4)
                {
                    for (int sy = y - reach / 2; sy <= y + reach / 2; sy += 4)
                    {
                        if (!WorldGen.InWorld(sx, sy, 40))
                            continue;

                        if (!Main.tile[sx, sy].HasTile)
                            open++;
                    }
                }

                if (open > most)
                {
                    most = open;
                    best = new Point(x, y);
                }
            }

            return best;
        }

        /// <summary>Anything else that has already claimed this piece of the world.</summary>
        private static bool Clashes(int x, int y, int reach)
        {
            Rectangle box = new Rectangle(x - reach, y - reach, reach * 2, reach * 2);

            if (!GenVars.structures.CanPlace(box, 12))
                return true;

            // The Temple registers itself in tiles rather than on the map, so it is
            // checked by its own bounds.
            Rectangle temple = new Rectangle(
                GenVars.tLeft - 60, GenVars.tTop - 60,
                Math.Max(1, GenVars.tRight - GenVars.tLeft) + 120,
                Math.Max(1, GenVars.tBottom - GenVars.tTop) + 120);

            if (box.Intersects(temple))
                return true;

            if (Math.Abs(x - GenVars.dungeonLocation) < 400)
                return true;

            if (TheBallPass.Centre != Point.Zero
                && Math.Abs(x - TheBallPass.Centre.X) < TheBallPass.Radius + reach + 120)
                return true;

            return false;
        }

        /// <summary>
        /// The wrong rock, in place of the right one. Air stays air — the cave keeps its
        /// shape — and anything that is not plain ground is left where it is, so an ore
        /// vein or a chest that was already here is still here afterwards.
        /// </summary>
        private static void Paint(int x, int y)
        {
            Tile t = Main.tile[x, y];

            if (t.HasTile)
            {
                ushort into = Instead(t.TileType);

                if (into != 0)
                {
                    t.TileType = into;
                    WorldGen.SquareTileFrame(x, y, true);
                }
            }

            if (t.WallType != 0 && Backing(t.WallType))
                t.WallType = (ushort)(WallID.HallowUnsafe1 + WorldGen.genRand.Next(4));
        }

        /// <summary>What each ordinary ground tile turns into, or 0 for leave it alone.</summary>
        private static ushort Instead(ushort type) => type switch
        {
            TileID.Stone or TileID.ClayBlock or TileID.Silt or TileID.Mudstone
                or TileID.Granite or TileID.Marble => (ushort)TileID.Pearlstone,
            TileID.Dirt => (ushort)(WorldGen.genRand.NextBool(3) ? TileID.Pearlstone : TileID.Dirt),
            TileID.Grass or TileID.CorruptGrass or TileID.CrimsonGrass => (ushort)TileID.HallowedGrass,
            TileID.Sand => (ushort)TileID.Pearlsand,
            TileID.HardenedSand => (ushort)TileID.HallowHardenedSand,
            TileID.Sandstone => (ushort)TileID.HallowSandstone,
            TileID.IceBlock or TileID.CorruptIce => (ushort)TileID.HallowedIce,
            _ => (ushort)0,
        };

        /// <summary>
        /// The ordinary cavern backgrounds, and nothing anybody built. Listed one by one
        /// rather than as a range: the seven cave walls are not contiguous — a living
        /// tree's leaves sit in the middle of them — and a range would take that too.
        /// </summary>
        private static bool Backing(ushort wall) => wall switch
        {
            WallID.Stone or WallID.DirtUnsafe or WallID.Dirt
                or WallID.CaveUnsafe or WallID.Cave2Unsafe or WallID.Cave3Unsafe
                or WallID.Cave4Unsafe or WallID.Cave5Unsafe or WallID.Cave6Unsafe
                or WallID.Cave7Unsafe or WallID.Cave8Unsafe
                or WallID.CaveWall or WallID.CaveWall2
                or WallID.RocksUnsafe1 or WallID.RocksUnsafe2
                or WallID.RocksUnsafe3 or WallID.RocksUnsafe4 => true,
            _ => false,
        };
    }

    /// <summary>
    /// What the Ball does when the Wall dies.
    ///
    /// Every other seed in the world answers hardmode by throwing a V from the surface
    /// down. This one does not, and it does not need to be stopped from doing so — the
    /// game has no idea it is there. What it does instead is keep hold: the arms grow,
    /// further and in greater number, sideways and downward through the rock, and never
    /// up. The beach above it stays clean all game and the player goes on walking over it.
    /// </summary>
    public class BallSpread : GlobalNPC
    {
        /// <summary>Only ever once. The flag rides in the world file with everything else.</summary>
        public static bool Spread;

        public override void OnKill(NPC npc)
        {
            if (npc.type != NPCID.WallofFlesh || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Spread || !ExtraLargeWorld.Active || TheBallPass.Centre == Point.Zero)
                return;

            Spread = true;
            TheBallPass.GrowArms(TheBallPass.Centre.X, TheBallPass.Centre.Y);

            if (Main.netMode == NetmodeID.Server)
            {
                // Far too many tiles for a tile square, so the sections are simply
                // retired and the clients ask for them again.
                Netplay.ResetSections();
                NetMessage.SendData(MessageID.WorldData);
            }
        }
    }

    /// <summary>Carries the Ball's position and its one hardmode flag through the world file.</summary>
    public class BallState : ModSystem
    {
        public override void ClearWorld()
        {
            BallSpread.Spread = false;
            TheBallPass.Forget();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (BallSpread.Spread)
                tag["PADBallSpread"] = true;

            if (TheBallPass.Centre != Point.Zero)
            {
                tag["PADBallX"] = TheBallPass.Centre.X;
                tag["PADBallY"] = TheBallPass.Centre.Y;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            BallSpread.Spread = tag.ContainsKey("PADBallSpread");

            if (tag.ContainsKey("PADBallX"))
                TheBallPass.Remember(new Point(tag.GetInt("PADBallX"), tag.GetInt("PADBallY")));
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(BallSpread.Spread);
            writer.Write(TheBallPass.Centre.X);
            writer.Write(TheBallPass.Centre.Y);
        }

        public override void NetReceive(BinaryReader reader)
        {
            BallSpread.Spread = reader.ReadBoolean();
            TheBallPass.Remember(new Point(reader.ReadInt32(), reader.ReadInt32()));
        }
    }
}
