using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.NPCs.Pseudo;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Ocean
{
    /// <summary>
    /// Schools of fish in the Vast Ocean (SchoolFish: the Anthias, the Fusiliers and the Pacific
    /// Creolefish), for the looks only, on each player's own machine: pseudo-NPCs (PseudoNPCs), none of
    /// the game's, so they count against nothing. By request (SchoolFish's notes).
    ///
    /// WHERE: in the Vast Ocean and the Schooler Zone, the 1st and 2nd sections (VastOceanDepthMeter's
    /// names), by request ("make it schooler exist in vast ocean and schooler zone"): from six rows under
    /// the surface to the end of the 2nd section, where there is room for a school; about the player,
    /// six schools at a time with the screen's middle in the Vast Ocean, nine in the Schooler Zone (half as
    /// many again, by request), never more than ten. Each comes in out of
    /// sight, from somewhere past the edge of the screen, and is let go out of sight, far behind. Its kind
    /// by the depth (each kind's Depths: the Anthias high and by the rock, the Fusiliers anywhere, the
    /// Creolefish deeper), and 40 to 80 of them (by request: "change max per school to 40-80").
    ///
    /// A SCHOOL: a middle going its way through the water (mostly along, slowly turning; off the rock
    /// ahead and about it, under the surface and over the black, apart from the other schools, and
    /// leaning a little away from what comes near), and the point its fish gather on, which shifts about
    /// inside it every second or so, the crowd bulging after it and changing shape (SchoolFish: each fish
    /// keeps a drifting place of its own in an oval laid along the way the school goes; never round and
    /// round). The things the fish keep from (<see cref="Threats"/>):
    /// every player, every creature bigger than a critter, every shot going fast; each school looks only at
    /// those near it.
    ///
    /// THE LIGHT: the sea's own light where each school is (VastOceanSky.SeaGlow, as its bubbles have it);
    /// how much of the sun reaches it (to three sections down, by day); and how dark it is about it,
    /// where the fish give their own light (by night, and deeper by day) and light the water a little
    /// about them (Lighting.AddLight at the school's middle).
    ///
    /// THE GLINTS (<see cref="DrawGlints"/>, over the water, VastOceanSky's light): a star where a fish's
    /// flank catches the light turning (warm white in the sun, its own colour in the dark), a scale
    /// glittering now and then in the sun (more when they are frightened), and in the dark small lights
    /// left behind them, drifting and going out.
    /// </summary>
    public class VastOceanSchools : ModSystem
    {
        public const int MostSchools = 10;

        /// <summary>How many schools about the player, in the Vast Ocean (the 1st section) and in the Schooler Zone (the 2nd): half as many again there, by request ("schooler zone got 1.5x many more schools than vast ocean").</summary>
        public const int InVastOcean = 6, InSchoolerZone = 9;

        /// <summary>How likely a school is to swim as a vortex, by request ("make it only 1 in 3 chance a school does that; most of the time they swim normally with variations within the school").</summary>
        public const float VortexChance = 1f / 3f;

        /// <summary>How many fish a school is: 40 to 80.</summary>
        public const int Fewest = 40, Most = 80;

        /// <summary>A school's width (radius, pixels) for each fish's square root: 40 fish 114 pixels, 80 fish 161 (loose, by request, the fish at their own size).</summary>
        private const float Spacing = 18f;

        /// <summary>The sections they are in: the Vast Ocean and the Schooler Zone.</summary>
        public const int Sections = 2;

        /// <summary>The least rows of water over a school's middle, and the most rows under the surface (the end of the 2nd section).</summary>
        private const int HighestRows = 6;
        private const int LowestRows = Sections * VastOcean.SectionRows - 8;

        internal struct School
        {
            public bool Alive;
            public int Type;
            public int Count;
            public int VariantA, VariantB;
            public int Age;

            /// <summary>Its middle and how it goes; which way it would go, and how fast.</summary>
            public Vector2 Centre, Cruise;
            public float Heading, Speed;

            /// <summary>How wide it is (pixels).</summary>
            public float Radius;

            /// <summary>The point the fish gather on, from the middle, and where it is going; ticks till it goes somewhere else.</summary>
            public Vector2 Offset, OffsetTo;
            public int Retarget;

            /// <summary>Which way it turns when the rock is ahead on both sides (1 or -1); the way it is going, of length 1 (its fish's places are laid along it).</summary>
            public float Spin;
            public Vector2 Way;

            /// <summary>A vortex (one school in three, <see cref="VortexChance"/>): its fish mill round the gathering point, the way given by <see cref="Spin"/>, so hard (<see cref="Curl"/>). Else a crowd swimming along.</summary>
            public bool Vortex;
            public float Curl;

            /// <summary>The sea's light where it is; how much of the sun reaches it; how dark it is about it (its fish give their own light), 0 to 1.</summary>
            public Vector3 Sea;
            public float Sun, Dark;

            /// <summary>How many of <see cref="Threats"/> are near it (their places: <see cref="Near"/>).</summary>
            public int NearCount;
        }

        internal static readonly School[] Schools = new School[MostSchools];

        /// <summary>Each school's fish: their places in PseudoNPCs.Pool.</summary>
        internal static readonly int[,] Members = new int[MostSchools, Most];

        /// <summary>A thing the fish keep from: where, half its size, and how hard they are pushed off it at its body (pixels a tick, each tick).</summary>
        internal struct Threat
        {
            public Vector2 At;
            public float Body;
            public float Push;
        }

        private const int MostThreats = 64, MostNear = 10;
        internal static readonly Threat[] Threats = new Threat[MostThreats];
        private static int threatCount;
        internal static readonly int[,] Near = new int[MostSchools, MostNear];
        private static readonly float[,] nearBy = new float[MostSchools, MostNear];

        /// <summary>Eight ways round, for looking about.</summary>
        internal static readonly Vector2[] Ways = new Vector2[8];

        private static int nextSpawn;
        private static bool fresh = true;

        /// <summary>This tick: the middle of the screen in the world, half its diagonal, and what is on it and a little more (where the fish glint).</summary>
        internal static Vector2 Middle;
        internal static float Outside;
        internal static Rectangle Seen;

        /// <summary>A school comes in this far out past the screen's half diagonal, at most (pixels); and is let go past this.</summary>
        private const float ComeFrom = 700f, LetGoPast = 1000f;

        private static Asset<Texture2D> dot;

        public override void Load()
        {
            for (int a = 0; a < 8; a++)
                Ways[a] = new Vector2((float)Math.Cos(a * MathHelper.PiOver4), (float)Math.Sin(a * MathHelper.PiOver4));

            if (!Main.dedServ)
                dot = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateLight");
        }

        public override void Unload()
        {
            dot = null;
            SchoolFishLooks.Unload();
        }

        public override void OnWorldUnload() => Clear();

        private static void Clear()
        {
            for (int g = 0; g < MostSchools; g++)
                Let(g);

            glintCount = 0;
            threatCount = 0;
            nextSpawn = 0;
            fresh = true;
        }

        /// <summary>A school let go, and its fish.</summary>
        private static void Let(int g)
        {
            if (!Schools[g].Alive)
                return;

            for (int k = 0; k < Schools[g].Count; k++)
            {
                int fish = Members[g, k];

                // Only one still of this school (its place may have gone to another since).
                if (fish >= 0 && PseudoNPCs.Pool[fish].Active && PseudoNPCs.Pool[fish].Group == g && PseudoNPCs.Pool[fish].Type == Schools[g].Type)
                    PseudoNPCs.Kill(fish);
            }

            Schools[g] = default;
        }

        // ---- each tick ---------------------------------------------------------------------------------

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (SubworldKeeper.Current is not VastOcean)
            {
                if (!fresh)
                    Clear();

                return;
            }

            Middle = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Outside = new Vector2(Main.screenWidth, Main.screenHeight).Length() * 0.5f;
            Seen = new Rectangle((int)Main.screenPosition.X - 100, (int)Main.screenPosition.Y - 100, Main.screenWidth + 200, Main.screenHeight + 200);

            // Let go once well out of sight: past where they come in.
            float far = Outside + LetGoPast;

            for (int g = 0; g < MostSchools; g++)
            {
                if (Schools[g].Alive && Vector2.DistanceSquared(Schools[g].Centre, Middle) > far * far)
                    Let(g);
            }

            GatherThreats();

            for (int g = 0; g < MostSchools; g++)
            {
                if (Schools[g].Alive)
                    Move(g);
            }

            Spawn();
            fresh = false;
            MoveGlints();
        }

        /// <summary>
        /// Every player, every fast shot and every creature bigger than a critter that could reach a school
        /// (players first, then shots, then creatures, should there be more than there is room for).
        /// </summary>
        private static void GatherThreats()
        {
            threatCount = 0;

            for (int p = 0; p < Main.maxPlayers && threatCount < MostThreats; p++)
            {
                Player player = Main.player[p];

                if (player.active && !player.dead)
                    AddThreat(player.Center, 18f, 1.5f);
            }

            for (int i = 0; i < Main.maxProjectiles && threatCount < MostThreats; i++)
            {
                Projectile shot = Main.projectile[i];

                if (shot.active && shot.velocity.LengthSquared() >= 9f)
                    AddThreat(shot.Center, Math.Max(shot.width, shot.height) * 0.5f, 0.9f);
            }

            for (int n = 0; n < Main.maxNPCs && threatCount < MostThreats; n++)
            {
                NPC npc = Main.npc[n];

                if (!npc.active || npc.catchItem > 0 || npc.lifeMax <= 5 || Math.Max(npc.width, npc.height) < 12)
                    continue;

                float size = Math.Max(npc.width, npc.height) * npc.scale;

                AddThreat(npc.Center, size * 0.5f, 1.5f * MathHelper.Clamp(size / 30f, 0.6f, 2f));
            }
        }

        /// <summary>A threat, if it is near enough a school to frighten any of it.</summary>
        private static void AddThreat(Vector2 at, float body, float push)
        {
            for (int g = 0; g < MostSchools; g++)
            {
                if (!Schools[g].Alive)
                    continue;

                float reach = Reach(Schools[g].Radius, body);

                if (Vector2.DistanceSquared(at, Schools[g].Centre) < reach * reach)
                {
                    Threats[threatCount++] = new Threat { At = at, Body = body, Push = push };
                    return;
                }
            }
        }

        /// <summary>How far from a school's middle a threat of this size can frighten any of it (its fish keep about it within half its width again).</summary>
        private static float Reach(float radius, float body) => radius * 1.5f + body + SchoolFish.Spread * 3.5f + 20f;

        private static void Move(int g)
        {
            ref School s = ref Schools[g];
            SchoolFish kind = PseudoNPCs.Kind(s.Type) as SchoolFish;

            s.Age++;

            // The light about it.
            float rows = s.Centre.Y / 16f - VastOcean.Surface;
            Vector3 sea = VastOceanSky.SeaGlow(rows) * 2.2f;

            s.Sea = Vector3.Min(sea, Vector3.One);
            s.Sun = VastOceanSky.Daylight() * (1f - Smooth(0f, VastOcean.SunRayRows, rows));
            s.Dark = Smooth(0.45f, 0.95f, 1f - s.Sun);

            // The things near it: the nearest, if there are more than it looks at.
            s.NearCount = 0;

            for (int t = 0; t < threatCount; t++)
            {
                float reach = Reach(s.Radius, Threats[t].Body);
                float d2 = Vector2.DistanceSquared(Threats[t].At, s.Centre);

                if (d2 >= reach * reach)
                    continue;

                int k = s.NearCount < MostNear ? s.NearCount++ : MostNear;

                // Kept nearest first: in at its place, the farthest dropped when full.
                while (k > 0 && nearBy[g, k - 1] > d2)
                {
                    if (k < MostNear)
                    {
                        Near[g, k] = Near[g, k - 1];
                        nearBy[g, k] = nearBy[g, k - 1];
                    }

                    k--;
                }

                if (k < MostNear)
                {
                    Near[g, k] = t;
                    nearBy[g, k] = d2;
                }
            }

            // Its way: wandering; off the rock ahead.
            s.Heading += Main.rand.NextFloat(-0.015f, 0.015f);

            float look = s.Radius + 90f;

            if (!Open(s.Centre + s.Heading.ToRotationVector2() * look))
            {
                bool left = Open(s.Centre + (s.Heading - 0.7f).ToRotationVector2() * look);
                bool right = Open(s.Centre + (s.Heading + 0.7f).ToRotationVector2() * look);

                s.Heading += left && !right ? -0.05f : !left && right ? 0.05f : 0.07f * s.Spin;
            }

            // Room about it: off the rock within its width and a little more, under the surface, over the black, apart from the others.
            Vector2 room = Vector2.Zero;

            for (int a = 0; a < 8; a++)
            {
                if (!Open(s.Centre + Ways[a] * (s.Radius + 24f)))
                    room -= Ways[a];
            }

            if (rows < HighestRows + s.Radius / 16f)
                room.Y += 1f;
            else if (rows > LowestRows)
                room.Y -= 1f;

            for (int o = 0; o < MostSchools; o++)
            {
                if (o == g || !Schools[o].Alive)
                    continue;

                Vector2 apart = s.Centre - Schools[o].Centre;
                float d = apart.Length() + 0.001f;

                if (d < s.Radius + Schools[o].Radius + 30f)
                    room += apart / d * 0.7f;
            }

            Vector2 wants = s.Heading.ToRotationVector2() * s.Speed + room * 0.25f;

            // Leaning away from what comes near, a little: the fish part round it more than the school goes.
            for (int k = 0; k < s.NearCount; k++)
            {
                ref Threat threat = ref Threats[Near[g, k]];
                Vector2 away = s.Centre - threat.At;
                float d = away.Length() + 0.001f;
                float past = Math.Max(d - s.Radius, 0f);
                float spread = SchoolFish.Spread * 1.5f;

                wants += away / d * (0.45f * (float)Math.Exp(-past * past / (2f * spread * spread)));
            }

            s.Cruise += (wants - s.Cruise) * 0.03f;
            s.Centre += s.Cruise;

            // The way it goes, turned to slowly, so its crowd is not swung round when it stops and starts.
            if (s.Cruise.LengthSquared() > 0.01f)
            {
                s.Way = Vector2.Normalize(Vector2.Lerp(s.Way, Vector2.Normalize(s.Cruise), 0.02f) + new Vector2(0.0001f, 0f));
            }

            // The point they gather on, shifting about inside it.
            if (--s.Retarget <= 0)
            {
                float r = (float)Math.Sqrt(Main.rand.NextFloat()) * 0.9f * s.Radius;
                Vector2 to = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * r;

                s.OffsetTo = new Vector2(to.X, to.Y * 0.7f);
                s.Retarget = Main.rand.Next(40, 121);
            }

            s.Offset += (s.OffsetTo - s.Offset) * 0.025f;


            // In the dark, their light in the water about them.
            if (s.Dark > 0.05f && kind != null)
            {
                Vector3 glow = SchoolFishLooks.Glow(s.Type, s.VariantA).ToVector3() * (0.22f * s.Dark * Math.Min(1f, s.Count / 50f));

                Lighting.AddLight(s.Centre + s.Offset, glow);
            }
        }


        // ---- schools coming ---------------------------------------------------------------------------

        /// <summary>
        /// A school more if there are fewer about than the depth the screen shows wants: tried every
        /// two-thirds of a second, and all at once on coming into the place. Somewhere past the edge of
        /// the screen, with room for it (its width in water all round), not near another.
        /// </summary>
        private static void Spawn()
        {
            if (--nextSpawn > 0 && !fresh)
                return;

            nextSpawn = 40;

            Vector2 middle = Middle;
            float rows = middle.Y / 16f - VastOcean.Surface;

            if (rows > LowestRows + 60f)
                return;

            int want = rows < VastOcean.SectionRows ? InVastOcean : InSchoolerZone;
            int alive = 0;

            for (int g = 0; g < MostSchools; g++)
            {
                if (Schools[g].Alive)
                    alive++;
            }

            int tries = fresh ? want * 6 : 6;

            for (int k = 0; k < tries && alive < want; k++)
            {
                Vector2 at = middle + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * (Outside + Main.rand.NextFloat(260f, ComeFrom));

                if (TryStart(at))
                    alive++;
            }
        }

        private static bool TryStart(Vector2 at)
        {
            float rows = at.Y / 16f - VastOcean.Surface;

            if (rows < HighestRows + 3 || rows > LowestRows)
                return false;

            int g = Array.FindIndex(Schools, s => !s.Alive);

            if (g < 0)
                return false;

            for (int o = 0; o < MostSchools; o++)
            {
                if (Schools[o].Alive && Vector2.DistanceSquared(Schools[o].Centre, at) < 500f * 500f)
                    return false;
            }

            int count = Main.rand.Next(Fewest, Most + 1);
            float radius = Spacing * (float)Math.Sqrt(count);

            if (!Open(at))
                return false;

            for (int a = 0; a < 8; a++)
            {
                if (!Open(at + Ways[a] * (radius + 32f)))
                    return false;
            }

            SchoolFish kind = Choose(at, rows);

            if (kind == null)
                return false;

            // In towards the screen, mostly along rather than up or down.
            Vector2 inward = Middle - at;
            float heading = (float)Math.Atan2(inward.Y * 0.5f, inward.X) + Main.rand.NextFloat(-0.5f, 0.5f);
            float speed = Main.rand.NextFloat(kind.CruiseSlow, kind.CruiseFast);
            int variants = kind.Variants.Length;

            Schools[g] = new School
            {
                Alive = true,
                Type = kind.Type,
                VariantA = Main.rand.Next(variants),
                VariantB = Main.rand.Next(variants),
                Centre = at,
                Heading = heading,
                Speed = speed,
                Cruise = heading.ToRotationVector2() * speed,
                Radius = radius,
                Spin = Main.rand.NextBool() ? 1f : -1f,
                Vortex = Main.rand.NextFloat() < VortexChance,
                Curl = Main.rand.NextFloat(0.8f, 1.3f),
                Way = heading.ToRotationVector2(),
            };

            ref School s = ref Schools[g];

            for (int k = 0; k < count; k++)
            {
                Vector2 place = at + Main.rand.NextVector2Circular(radius, radius);

                if (!Open(place))
                    place = at;

                int made = PseudoNPCs.New(kind.Type, place, s.Cruise + Main.rand.NextVector2Circular(0.3f, 0.3f), g, k);

                if (made < 0)
                    break;

                Members[g, k] = made;
                s.Count = k + 1;
            }

            if (s.Count < Fewest / 2)
            {
                Let(g);
                return false;
            }

            return true;
        }

        /// <summary>A kind for a school here, by its likelihood at this depth (and by the rock).</summary>
        private static SchoolFish Choose(Vector2 at, float rows)
        {
            int section = Math.Clamp(1 + (int)(rows / VastOcean.SectionRows), 1, Sections);
            bool rock = false;

            for (int a = 0; a < 8 && !rock; a += 2)
            {
                for (int step = 1; step <= 4 && !rock; step++)
                    rock = !Open(at + Ways[a] * (80f * step)) && (at + Ways[a] * (80f * step)).Y / 16f > VastOcean.Surface + 1;
            }

            float total = 0f;

            foreach (SchoolFish kind in SchoolFish.All)
                total += Weight(kind, section, rock);

            if (total <= 0f)
                return null;

            float roll = Main.rand.NextFloat(total);

            foreach (SchoolFish kind in SchoolFish.All)
            {
                roll -= Weight(kind, section, rock);

                if (roll <= 0f)
                    return kind;
            }

            return SchoolFish.All[^1];
        }

        private static float Weight(SchoolFish kind, int section, bool rock) =>
            kind.Depths[Math.Clamp(section - 1, 0, kind.Depths.Length - 1)] * (rock ? kind.NearRock : 1f);

        /// <summary>Water a fish can be in: under the surface row, full enough, not in anything solid.</summary>
        internal static bool Open(Vector2 at)
        {
            int x = (int)(at.X / 16f);
            int y = (int)(at.Y / 16f);

            if (!WorldGen.InWorld(x, y, 2) || y <= VastOcean.Surface)
                return false;

            Tile tile = Main.tile[x, y];

            if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                return false;

            return tile.LiquidAmount > 96 && tile.LiquidType == LiquidID.Water;
        }

        // ---- the glints --------------------------------------------------------------------------------

        private const int MostGlints = 700;

        private struct Glint
        {
            public Vector2 At, Velocity;
            public int Age, Life;
            public float Size, Flicker, Strength;
            public Color Colour;

            /// <summary>A star (a flash, a glitter), or a small light left in the dark.</summary>
            public bool Star;
        }

        private static readonly Glint[] glints = new Glint[MostGlints];
        private static int glintCount;

        private static readonly Color Sunlit = new Color(255, 248, 220);

        /// <summary>A fish's flank catching the light as it turns: warm white in the sun, its own colour in the dark.</summary>
        internal static void Flash(Vector2 at, ref School school, Color own)
        {
            float lit = Math.Max(school.Sun, school.Dark * 0.85f);

            if (lit < 0.1f || glintCount >= MostGlints)
                return;

            glints[glintCount++] = new Glint
            {
                At = at,
                Life = 16,
                Size = Main.rand.NextFloat(8f, 12f),
                Strength = 1.1f * lit,
                Colour = school.Sun >= school.Dark ? Sunlit : own,
                Star = true,
            };
        }

        /// <summary>A scale glittering in the sun.</summary>
        internal static void Twinkle(Vector2 at, float sun)
        {
            if (glintCount >= MostGlints)
                return;

            glints[glintCount++] = new Glint
            {
                At = at,
                Life = 22,
                Size = Main.rand.NextFloat(4f, 8f),
                Strength = 0.9f * sun,
                Colour = Sunlit,
                Star = true,
            };
        }

        /// <summary>A small light left behind in the dark, drifting back from the fish and going out.</summary>
        internal static void Mote(Vector2 at, Vector2 velocity, Color own, float dark)
        {
            if (glintCount >= MostGlints)
                return;

            glints[glintCount++] = new Glint
            {
                At = at,
                Velocity = -velocity * 0.08f + Main.rand.NextVector2Circular(0.15f, 0.15f),
                Life = Main.rand.Next(50, 101),
                Size = Main.rand.NextFloat(2f, 3.5f),
                Flicker = Main.rand.NextFloat(MathHelper.TwoPi),
                Strength = 0.9f * dark,
                Colour = own,
            };
        }

        private static void MoveGlints()
        {
            for (int i = glintCount - 1; i >= 0; i--)
            {
                ref Glint g = ref glints[i];

                if (++g.Age >= g.Life)
                {
                    glints[i] = glints[--glintCount];
                    continue;
                }

                g.At += g.Velocity;
                g.Velocity *= 0.95f;
            }
        }

        /// <summary>The glints, added as light (VastOceanSky's batch over the water, sampled smoothly).</summary>
        internal static void DrawGlints(SpriteBatch batch)
        {
            if (glintCount == 0 || !Dot(out Texture2D picture))
                return;

            Vector2 origin = picture.Size() * 0.5f;
            float wide = picture.Width;
            float time = Main.GlobalTimeWrappedHourly;
            Rectangle view = new Rectangle((int)Main.screenPosition.X - 32, (int)Main.screenPosition.Y - 32, Main.screenWidth + 64, Main.screenHeight + 64);

            for (int i = 0; i < glintCount; i++)
            {
                ref Glint g = ref glints[i];

                if (!view.Contains((int)g.At.X, (int)g.At.Y))
                    continue;

                float life = g.Age / (float)g.Life;
                Vector2 at = g.At - Main.screenPosition;

                if (g.Star)
                {
                    // A point, two thin arms across it and a soft halo: up and gone over its life.
                    float up = (float)Math.Pow(Math.Sin(Math.PI * life), 1.5);
                    float size = g.Size * (0.6f + 0.4f * (float)Math.Sin(Math.PI * life));
                    Color colour = Lit(g.Colour, g.Strength * up);

                    batch.Draw(picture, at, null, colour, 0f, origin, 3f / wide, SpriteEffects.None, 0f);
                    batch.Draw(picture, at, null, colour * 0.8f, 0f, origin, new Vector2(size * 2.2f, 1.1f) / wide, SpriteEffects.None, 0f);
                    batch.Draw(picture, at, null, colour * 0.8f, 0f, origin, new Vector2(1.1f, size * 2.2f) / wide, SpriteEffects.None, 0f);
                    batch.Draw(picture, at, null, colour * 0.25f, 0f, origin, size * 1.6f / wide, SpriteEffects.None, 0f);
                }
                else
                {
                    float strength = g.Strength * Smooth(0f, 0.08f, life) * (1f - Smooth(0.35f, 1f, life)) * (0.65f + 0.35f * (float)Math.Sin(time * 9f + g.Flicker));

                    batch.Draw(picture, at, null, Lit(g.Colour, 0.9f * strength), 0f, origin, g.Size * 2f / wide, SpriteEffects.None, 0f);
                    batch.Draw(picture, at, null, Lit(g.Colour, 0.25f * strength), 0f, origin, g.Size * 6f / wide, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>The soft dot the glints and the glow are drawn with.</summary>
        internal static bool Dot(out Texture2D picture)
        {
            picture = dot != null && dot.IsLoaded ? dot.Value : null;
            return picture != null;
        }

        /// <summary>A colour to add as light (no alpha), so strong.</summary>
        internal static Color Lit(Color colour, float strength)
        {
            strength = MathHelper.Clamp(strength, 0f, 1f);

            return new Color((byte)(colour.R * strength), (byte)(colour.G * strength), (byte)(colour.B * strength), (byte)0);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }
}
