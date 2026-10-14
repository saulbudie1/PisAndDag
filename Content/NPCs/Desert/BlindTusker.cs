using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;

namespace PisAndDag.Content.NPCs.Desert
{
    /// <summary>
    /// The Blind Tusker, by request: "Create a four legged four tusk blind creature width: 16*5,
    /// height: 3*16, with texture close to calamitys and use iron flow's coloring to color. 200HP,
    /// born in desert rarely, twice as rare as Antlion (yes prehardmode)"; "It charges at the player
    /// reaching top speed of 36 mph and can jump over long holes and can jump up to 15 tiles". It is
    /// where the Iron Flow comes from (<see cref="IronFlowChance"/>). Its name is mine.
    ///
    /// <b>Where.</b> Wherever the game puts an Antlion, at half her chance there
    /// (<see cref="SpawnChance"/>): on the desert's sand by day, in a sandstorm before the Eye, and
    /// under the desert; and only where it has the room to stand, five tiles by three.
    ///
    /// <b>What it does.</b> Blind, it hunts by what it hears: any player within
    /// <see cref="HearTiles"/> tiles across and <see cref="HearTilesUp"/> up or down, through
    /// anything. It picks a way and charges, gathering speed to <see cref="TopSpeed"/> (36 mph on the
    /// game's stopwatch), and keeps going until it has run past them by <see cref="OvershootTiles"/>
    /// tiles; then it skids, turns and comes again. A wall in its way it jumps, up to
    /// <see cref="JumpTiles"/> tiles high; a higher one stops it dead and throws it back. A gap
    /// narrower than itself it runs over. A hole it leaps, to ground across it it can reach, or else
    /// down to ground no more than <see cref="LeapDownTiles"/> under it, the hole's own floor among
    /// it; one with nothing it can reach, it skids short of and stands at the edge. With nobody to
    /// hear, it wanders, slowly, and turns back from walls and drops.
    ///
    /// ai[0] is what it is doing (<see cref="Mode"/>), ai[1] ticks in it, ai[2] the way it is going,
    /// ai[3] the speed it holds in the air. The server decides; turns of its own when wandering are
    /// the server's and sent.
    /// </summary>
    public class BlindTusker : ModNPC
    {
        // ---- the numbers ----------------------------------------------------------------------

        /// <summary>"width: 16*5, height: 3*16".</summary>
        public const int Wide = 16 * 5;
        public const int Tall = 3 * 16;

        /// <summary>"200HP".</summary>
        public const int Life = 200;

        /// <summary>Mine: between the Antlion Charger's 25 and the Giant Antlion Charger's 38.</summary>
        public const int ContactDamage = 30;

        /// <summary>Mine: the Antlion Charger's.</summary>
        public const int Armour = 10;

        /// <summary>Mine: heavy, but not a wall.</summary>
        public const float KnockbackTaken = 0.25f;

        /// <summary>"top speed of 36 mph".</summary>
        public const float TopSpeedMph = 36f;

        /// <summary>
        /// In pixels a tick: the game's stopwatch shows a speed as pixels a tick times 216000 over 42240
        /// (Main.DrawInfoAccs), so 36 mph is 7.04.
        /// </summary>
        public const float TopSpeed = TopSpeedMph * 42240f / 216000f;

        /// <summary>Mine: from a stand to top speed in 47 ticks.</summary>
        public const float Gather = 0.15f;

        /// <summary>Mine: what a skid takes off a tick, so from top speed it stops in 18 ticks, about four tiles on.</summary>
        public const float SkidBite = 0.4f;

        /// <summary>Mine: its pace with nobody to hear.</summary>
        public const float WanderSpeed = 1.2f;

        /// <summary>"can jump up to 15 tiles".</summary>
        public const int JumpTiles = 15;

        /// <summary>The game's gravity for an NPC, and its fastest fall (NPC.UpdateNPC_UpdateGravity).</summary>
        public const float Gravity = 0.3f;
        public const float FastestFall = 10f;

        /// <summary>Mine: how far it hears, in tiles.</summary>
        public const int HearTiles = 50;
        public const int HearTilesUp = 25;

        /// <summary>Mine: how far it runs on past who it charged before it skids.</summary>
        public const int OvershootTiles = 8;

        /// <summary>Mine: the furthest it looks across a hole for ground to land on, in tiles.</summary>
        public const int LeapLookTiles = 40;

        /// <summary>Mine: a drop no deeper than this it runs down into rather than leaping.</summary>
        public const int StepDownTiles = 3;

        /// <summary>Mine: a hole with nothing it can land on in it or across it, no deeper than this, it runs into all the same.</summary>
        public const int PitTiles = 8;

        /// <summary>Mine: the furthest under its feet it will leap down to, across a hole.</summary>
        public const int LeapDownTiles = 15;

        /// <summary>"twice as rare as Antlion".</summary>
        public const float Rarity = 0.5f;

        /// <summary>Mine: one in this many drop the Iron Flow.</summary>
        public const int IronFlowChance = 4;

        /// <summary>Frames, one under the other: standing, six running, in the air, skidding.</summary>
        public const int Frames = 9;
        private const int StandFrame = 0;
        private const int RunFirst = 1;
        private const int RunFrames = 6;
        private const int AirFrame = 7;
        private const int SkidFrame = 8;

        /// <summary>The run's frames turn over every this many pixels run: four ticks a frame at top speed.</summary>
        private const float StrideFrame = 28f;

        public enum Mode
        {
            Wander,
            Charge,
            Skid,
            Air,
            Reel,
        }

        private Mode Now
        {
            get => (Mode)(int)NPC.ai[0];
            set
            {
                NPC.ai[0] = (int)value;
                NPC.ai[1] = 0f;
            }
        }

        private int Way
        {
            get => NPC.ai[2] >= 0f ? 1 : -1;
            set => NPC.ai[2] = value;
        }

        /// <summary>The highest a jump at this launch rises: the game adds gravity before it moves, so 0.15 m (m - 1) for 0.3 m.</summary>
        public static float Rise(float launch)
        {
            int ticks = (int)(launch / Gravity);
            return ticks * launch - Gravity * ticks * (ticks + 1) / 2f;
        }

        /// <summary>The least launch that rises this many pixels: the inverse of <see cref="Rise"/>.</summary>
        public static float Launch(float rise) => 0.15f + (float)Math.Sqrt(0.0225 + 0.6 * Math.Max(rise, 0f));

        /// <summary>Its strongest jump: over a wall of <see cref="JumpTiles"/> with 4 pixels to spare.</summary>
        public static readonly float MostLaunch = Launch(JumpTiles * 16f + 4f);

        // ---- set up -----------------------------------------------------------------------------

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = Frames;

            // Running in its bestiary picture, as it is mostly seen.
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers() { Velocity = 1f });
        }

        public override void SetDefaults()
        {
            NPC.width = Wide;
            NPC.height = Tall;
            NPC.lifeMax = Life;
            NPC.damage = ContactDamage;
            NPC.defense = Armour;
            NPC.knockBackResist = KnockbackTaken;
            NPC.aiStyle = -1;
            NPC.value = Item.buyPrice(silver: 4);

            // The game's Pigron, lower: a boar's squeal and its death.
            NPC.HitSound = SoundID.NPCHit27 with { Pitch = -0.4f };
            NPC.DeathSound = SoundID.NPCDeath30 with { Pitch = -0.4f };
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Desert,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.UndergroundDesert,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Events.Sandstorm,
                new FlavorTextBestiaryInfoElement("Mods.PisAndDag.NPCs.BlindTusker.Bestiary"),
            });
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<IronFlow>(), IronFlowChance));
        }

        // ---- where -----------------------------------------------------------------------------

        /// <summary>
        /// Half the Antlion's chance, wherever the game would put her (NPC.SpawnNPC, 1.4.4), as
        /// tModLoader reckons each of those places (SpawnCondition), and nowhere it has no room:
        /// <list type="bullet">
        /// <item>on sand by day: her 1 in 5 (OverworldDayDesert), where her sandstone test passes;</item>
        /// <item>in a sandstorm, before the Eye and hardmode: her 1 in 4 (SandstormEvent);</item>
        /// <item>under the desert: hers is 1 of 5 picks, not turned to something else 1 in 15
        /// (DesertCave), and in hardmode only the fifth of the time that is not hardmode's own.</item>
        /// </list>
        /// The game tries a few rarer things first in each that this leaves out (a scorpion, a
        /// tumbleweed, a lost golfer, a tomb crawler).
        /// </summary>
        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            int x = spawnInfo.SpawnTileX;
            int y = spawnInfo.SpawnTileY;

            if (!Room(x, y))
                return 0f;

            float antlion = 0f;

            if (NPC.Spawning_SandstoneCheck(x, y))
                antlion += SpawnCondition.OverworldDayDesert.Chance;

            if (!NPC.downedBoss1 && !Main.hardMode)
                antlion += SpawnCondition.SandstormEvent.Chance * 0.25f;

            antlion += SpawnCondition.DesertCave.Chance * (1f / 5f) * (14f / 15f) * (Main.hardMode ? 0.2f : 1f);

            return antlion * Rarity;
        }

        /// <summary>
        /// Five tiles by three clear over the spawn tile: what it fills when put down there (NPC.NewNPC
        /// at the tile's middle and top, so its sides on tile edges two either side).
        /// </summary>
        private static bool Room(int x, int y)
        {
            if (!WorldGen.InWorld(x - 3, y - 4, 2) || !WorldGen.InWorld(x + 3, y, 2))
                return false;

            for (int i = x - 2; i <= x + 2; i++)
            {
                for (int j = y - 3; j <= y - 1; j++)
                {
                    if (WorldGen.SolidTile(i, j))
                        return false;
                }
            }

            return true;
        }

        // ---- every tick ----------------------------------------------------------------------

        public override void AI()
        {
            NPC.TargetClosest(false);
            Player target = Main.player[NPC.target];
            bool hears = Hears(target);
            bool server = Main.netMode != NetmodeID.MultiplayerClient;

            // Which way it first goes is the server's.
            if (NPC.localAI[1] == 0f)
            {
                NPC.localAI[1] = 1f;

                if (server && Now == Mode.Wander)
                {
                    Way = Main.rand.NextBool() ? 1 : -1;
                    NPC.netUpdate = true;
                }
            }

            NPC.ai[1]++;

            switch (Now)
            {
                case Mode.Wander:
                    Wander(target, hears, server);
                    break;

                case Mode.Charge:
                    Charge(target, hears);
                    break;

                case Mode.Skid:
                    Skid(target, hears);
                    break;

                case Mode.Air:
                    Air();
                    break;

                case Mode.Reel:
                    Reel(target, hears);
                    break;
            }

            // Up steps of a tile as the game's walkers do (Collision.StepUp), on the ground only.
            if (Grounded && Now != Mode.Air && NPC.velocity.X != 0f)
                Collision.StepUp(ref NPC.position, ref NPC.velocity, NPC.width, NPC.height, ref NPC.stepSpeed, ref NPC.gfxOffY, 1, false, 0);

            NPC.direction = Way;
            NPC.spriteDirection = Way;
        }

        /// <summary>On the ground: the game keeps a standing NPC's fall at nothing.</summary>
        private bool Grounded => NPC.velocity.Y == 0f;

        /// <summary>Any living player near enough, through anything: it hears, it does not see.</summary>
        private bool Hears(Player target)
        {
            if (target == null || !target.active || target.dead)
                return false;

            return Math.Abs(target.Center.X - NPC.Center.X) < HearTiles * 16f
                && Math.Abs(target.Center.Y - NPC.Center.Y) < HearTilesUp * 16f;
        }

        private void Wander(Player target, bool hears, bool server)
        {
            if (hears)
            {
                Begin(target);
                return;
            }

            // Now and then it stops a while, and now and then it turns: the server's to choose.
            // ai[3] is the stop's ticks left.
            if (server && NPC.ai[1] > 180f && Main.rand.NextBool(200))
            {
                if (Main.rand.NextBool(2))
                    Way = -Way;

                NPC.ai[1] = 0f;
                NPC.ai[3] = Main.rand.Next(40, 100);
                NPC.netUpdate = true;
            }

            float pace = WanderSpeed;

            if (NPC.ai[3] > 0f)
            {
                NPC.ai[3]--;
                pace = 0f;
            }

            if (Grounded && (WallAhead(Way, 8f, out int height) && height >= 2 || HoleStart(Way, 8f) != NoHole))
            {
                Way = -Way;
                NPC.velocity.X = 0f;
                return;
            }

            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, Way * pace, 0.1f);
        }

        /// <summary>A charge at them: their way, from where it stands.</summary>
        private void Begin(Player target)
        {
            Way = target.Center.X >= NPC.Center.X ? 1 : -1;
            Now = Mode.Charge;
            NPC.ai[3] = 0f;
            NPC.netUpdate = true;

            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.NPCHit27 with { Pitch = -0.75f, Volume = 0.7f }, NPC.Center);
        }

        private void Charge(Player target, bool hears)
        {
            float speed = Math.Abs(NPC.velocity.X);

            // Something turned it round: first it bleeds the wrong way off.
            if (NPC.velocity.X != 0f && Math.Sign(NPC.velocity.X) != Way)
            {
                NPC.velocity.X = -Way * Math.Max(0f, speed - SkidBite);
                return;
            }

            // Run past them far enough, or lost them: skid.
            bool past = hears && (target.Center.X - NPC.Center.X) * Way < -OvershootTiles * 16f;

            if (past || !hears)
            {
                Now = Mode.Skid;
                NPC.netUpdate = true;
                return;
            }

            speed = Math.Min(speed + Gather, TopSpeed);

            // On the ground it minds what is ahead; off it (over a step down, or into a hole) it
            // keeps going.
            if (Grounded && !Ahead(speed))
                return;

            NPC.velocity.X = Way * speed;
        }

        /// <summary>
        /// What is in front of it on the ground, at the speed it is gathering: false when that has
        /// it jump, reel or stop instead of running on.
        /// </summary>
        private bool Ahead(float speed)
        {
            // A wall: jumped from a few ticks off, or, too high, run into.
            if (WallAhead(Way, speed * 6f + 16f, out int height) && height >= 2)
            {
                if (height <= JumpTiles)
                {
                    Jump(Launch(height * 16f + 4f), Math.Max(speed, 3f));
                    return false;
                }

                if (NPC.collideX || WallAhead(Way, 4f, out _))
                {
                    NPC.velocity = new Vector2(-Way * 3f, -4f);
                    Now = Mode.Reel;
                    NPC.netUpdate = true;
                    Bump();
                    return false;
                }

                return true;
            }

            // A hole: looked at from as far off as it takes to stop, leapt from its edge.
            float stopping = speed * speed / (2f * SkidBite) + 16f;
            int hole = HoleStart(Way, Math.Max(stopping, speed + 2f));

            if (hole == NoHole)
                return true;

            float edge = hole * 16f + (Way > 0 ? 0f : 16f);
            float gap = (edge - (Way > 0 ? NPC.Right.X : NPC.Left.X)) * Way;
            bool atEdge = gap <= speed + 2f;

            if (FindLeap(hole, atEdge ? NPC.Center.X : edge - Way * Wide / 2f, speed, out float launch, out float across))
            {
                if (atEdge)
                {
                    Jump(launch, across);
                    return false;
                }

                return true;
            }

            // Nothing it can reach beyond: a shallow pit it runs into; a deep one it skids short
            // of, and once there it stands at the edge.
            if (PitDepth(hole) <= PitTiles)
                return true;

            if (gap <= 16f)
            {
                NPC.velocity.X = 0f;
                return false;
            }

            NPC.velocity.X = Way * speed;
            Now = Mode.Skid;
            NPC.netUpdate = true;
            return false;
        }

        private void Skid(Player target, bool hears)
        {
            float speed = Math.Max(0f, Math.Abs(NPC.velocity.X) - SkidBite);
            NPC.velocity.X = Math.Sign(NPC.velocity.X) * speed;

            if (!Main.dedServ && Grounded && speed > 1f)
            {
                float front = NPC.velocity.X > 0f ? NPC.Right.X - 20f : NPC.Left.X;
                Dust.NewDust(new Vector2(front, NPC.Bottom.Y - 6f), 20, 6, DustID.Sand, -NPC.velocity.X * 0.4f, -1f);
            }

            if (speed > 0.3f)
                return;

            NPC.velocity.X = 0f;

            if (hears)
            {
                Begin(target);
                return;
            }

            ToWander();
        }

        /// <summary>Back to wandering, with no stop owed (ai[3] held its speed in the air).</summary>
        private void ToWander()
        {
            Now = Mode.Wander;
            NPC.ai[3] = 0f;
            NPC.netUpdate = true;
        }

        private void Air()
        {
            // It holds its speed through the air (a wall it slides up is not the end of the jump).
            NPC.velocity.X = Way * NPC.ai[3];

            // Down: the collision stopped a fall last tick.
            if (NPC.ai[1] > 2f && NPC.collideY && NPC.oldVelocity.Y > 0f)
            {
                Land();
                Now = Mode.Charge;
                NPC.netUpdate = true;
            }
        }

        private void Reel(Player target, bool hears)
        {
            if (Grounded)
                NPC.velocity.X *= 0.85f;

            if (NPC.ai[1] < 40f)
                return;

            if (hears)
            {
                Begin(target);
                return;
            }

            Way = -Way;
            ToWander();
        }

        private void Jump(float launch, float across)
        {
            NPC.velocity.Y = -Math.Min(launch, MostLaunch);
            NPC.ai[3] = Math.Min(across, TopSpeed);
            NPC.velocity.X = Way * NPC.ai[3];
            Now = Mode.Air;
            NPC.netUpdate = true;

            if (!Main.dedServ)
            {
                for (int i = 0; i < 8; i++)
                    Dust.NewDust(new Vector2(NPC.Left.X, NPC.Bottom.Y - 4f), NPC.width, 4, DustID.Sand, -Way * 1.5f, -1.5f);
            }
        }

        private void Land()
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.6f, Volume = 0.8f }, NPC.Bottom);

            for (int i = 0; i < 14; i++)
                Dust.NewDust(new Vector2(NPC.Left.X, NPC.Bottom.Y - 6f), NPC.width, 6, DustID.Sand, Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(0.5f, 2.5f));
        }

        private void Bump()
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit27 with { Pitch = -0.3f }, NPC.Center);

            for (int i = 0; i < 10; i++)
                Dust.NewDust(new Vector2(Way > 0 ? NPC.Right.X - 8f : NPC.Left.X, NPC.Center.Y - 8f), 8, 16, DustID.Sand, -Way * 2f, Main.rand.NextFloat(-2f, 1f));
        }

        // ---- the ground ahead -----------------------------------------------------------------

        private const int NoHole = int.MinValue;

        /// <summary>Solid to run into, or (standing) to stand on: solid tiles, and the tops of platforms.</summary>
        private static bool Solid(int i, int j, bool standing)
        {
            if (!WorldGen.InWorld(i, j))
                return true;

            Tile tile = Framing.GetTileSafely(i, j);

            if (!tile.HasTile || tile.IsActuated)
                return false;

            if (Main.tileSolidTop[tile.TileType])
                return standing;

            return Main.tileSolid[tile.TileType];
        }

        /// <summary>The row under its feet.</summary>
        private int GroundRow => (int)((NPC.Bottom.Y + 1f) / 16f);

        /// <summary>The column this far past its front edge (none: the column its edge is in, or the first past it).</summary>
        private int ColumnAhead(int way, float ahead)
        {
            float edge = way > 0 ? NPC.Right.X + ahead : NPC.Left.X - ahead - 0.01f;
            return (int)Math.Floor(edge / 16f);
        }

        /// <summary>
        /// Whether a wall stands within this many pixels ahead, and how high above its feet it goes:
        /// from the row its feet are in, up to the first solid tile with three clear rows over it
        /// (room for it on top). It counts no higher than <see cref="JumpTiles"/> + 1, already too
        /// high.
        /// </summary>
        private bool WallAhead(int way, float ahead, out int height)
        {
            height = 0;
            int feet = GroundRow - 1;
            int first = ColumnAhead(way, 0f);
            int last = ColumnAhead(way, ahead);

            for (int i = first; way > 0 ? i <= last : i >= last; i += way)
            {
                if (!Solid(i, feet, false))
                    continue;

                int top = feet;

                while (feet - top < JumpTiles && (Solid(i, top - 1, false) || Solid(i, top - 2, false) || Solid(i, top - 3, false)))
                    top--;

                height = feet - top + 1;
                return true;
            }

            return false;
        }

        /// <summary>No ground in this column from the row its feet are in down to <see cref="StepDownTiles"/> under its ground.</summary>
        private static bool Open(int i, int ground)
        {
            for (int j = ground - 1; j <= ground + StepDownTiles; j++)
            {
                if (Solid(i, j, true))
                    return false;
            }

            return true;
        }

        /// <summary>Columns it is wide: a gap narrower than this it walks over, held up by one side or the other.</summary>
        private const int WideTiles = Wide / 16;

        /// <summary>
        /// The first column within this many pixels ahead where a hole it would fall into opens: open
        /// (<see cref="Open"/>) and at least <see cref="WideTiles"/> columns across. Or <see cref="NoHole"/>.
        /// </summary>
        private int HoleStart(int way, float within)
        {
            int ground = GroundRow;
            int first = ColumnAhead(way, 0f);
            int last = ColumnAhead(way, within);

            for (int i = first; way > 0 ? i <= last : i >= last; i += way)
            {
                if (!Open(i, ground))
                    continue;

                int run = 1;

                while (run < WideTiles && Open(i + way * run, ground))
                    run++;

                if (run >= WideTiles)
                    return i;
            }

            return NoHole;
        }

        /// <summary>How far down the ground is in this column, in tiles (up to 60).</summary>
        private int PitDepth(int i)
        {
            int ground = GroundRow;

            for (int d = 0; d < 60; d++)
            {
                if (Solid(i, ground + d, true))
                    return d;
            }

            return 60;
        }

        /// <summary>
        /// A leap from a middle at fromX to ground across the hole, within <see cref="LeapLookTiles"/>
        /// of where it opens: three columns side by side with tops no more than one row apart, no more
        /// than <see cref="JumpTiles"/> over its own ground and no more than <see cref="LeapDownTiles"/>
        /// under it, with room over them (<see cref="Surface"/>). First to ground at its own level or
        /// above (no lower than a step down), nearest first; failing that, down to lower ground, the
        /// floor of the hole itself among it. The first it can reach (<see cref="LeapTo"/>).
        /// </summary>
        private bool FindLeap(int hole, float fromX, float speed, out float launch, out float across)
        {
            int way = Way;
            int ground = GroundRow;

            for (int k = 1; k < Tops.Length; k++)
                Tops[k] = Surface(hole + way * k, ground, out int top) ? top : NoHole;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int k = 1; k <= LeapLookTiles; k++)
                {
                    int top = Tops[k];

                    if (top == NoHole || !Near(Tops[k + 1], top) || !Near(Tops[k + 2], top))
                        continue;

                    if ((top <= ground + StepDownTiles) != (pass == 0))
                        continue;

                    if (LeapTo(fromX, hole + way * k, top, speed, out launch, out across))
                        return true;
                }
            }

            launch = 0f;
            across = 0f;
            return false;
        }

        /// <summary>The top row of ground in each column across a hole, or <see cref="NoHole"/>: <see cref="FindLeap"/>'s, kept to spare making it each tick (the game runs NPCs one at a time).</summary>
        private static readonly int[] Tops = new int[LeapLookTiles + 3];

        /// <summary>A column's top within a row of this one.</summary>
        private static bool Near(int other, int top) => other != NoHole && Math.Abs(other - top) <= 1;

        /// <summary>The top row of ground in this column within reach (see <see cref="FindLeap"/>), with three rows clear over it.</summary>
        private static bool Surface(int i, int ground, out int surface)
        {
            surface = 0;

            for (int j = ground - JumpTiles; j <= ground + LeapDownTiles; j++)
            {
                if (!Solid(i, j, true))
                    continue;

                if (Solid(i, j - 1, false) || Solid(i, j - 2, false) || Solid(i, j - 3, false))
                    continue;

                surface = j;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The leap from a middle at fromX across to the landing: at its speed (at least 5), or failing
        /// that at top speed. Where it is t ticks after a launch v is t v + 0.3 t (t + 1) / 2 down (the
        /// game adds gravity before each move); solved for v twice, and the higher kept: its middle
        /// coming down on the landing's second column, and its front clearing the landing's near edge
        /// by 4 pixels (so a landing above it is not struck from the side). False when even top speed
        /// needs more than its strongest jump.
        /// </summary>
        private bool LeapTo(float fromX, int pad, int surface, float speed, out float launch, out float across)
        {
            int way = Way;
            float landX = (pad + way) * 16f + 8f;
            float faceX = way > 0 ? pad * 16f : (pad + 1) * 16f;
            float frontX = fromX + way * Wide / 2f;
            float dy = surface * 16f - NPC.Bottom.Y;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                float s = attempt == 0 ? Math.Min(Math.Max(speed, 5f), TopSpeed) : TopSpeed;
                float vy = FallTo(Math.Abs(landX - fromX) / s, dy);
                float overFace = (faceX - frontX) * way;

                if (overFace > 0f)
                    vy = Math.Min(vy, FallTo(overFace / s, dy - 4f));

                if (-vy <= MostLaunch)
                {
                    launch = Math.Max(-vy, 0f);
                    across = s;
                    return true;
                }
            }

            launch = 0f;
            across = 0f;
            return false;
        }

        /// <summary>The launch (down positive) that has it this far down after this many ticks, rounded up.</summary>
        private static float FallTo(float ticks, float down)
        {
            int t = Math.Max(1, (int)Math.Ceiling(ticks));
            return (down - Gravity * t * (t + 1) / 2f) / t;
        }

        // ---- the look -------------------------------------------------------------------------

        public override void FindFrame(int frameHeight)
        {
            int frame;

            if (NPC.IsABestiaryIconDummy)
            {
                NPC.frameCounter += 3.5;
                frame = RunFirst + (int)(NPC.frameCounter / StrideFrame) % RunFrames;
            }
            else if (Now == Mode.Skid)
                frame = SkidFrame;
            else if (NPC.velocity.Y != 0f)
                frame = AirFrame;
            else if (Math.Abs(NPC.velocity.X) < 0.3f)
            {
                frame = StandFrame;
                NPC.frameCounter = 0.0;
            }
            else
            {
                NPC.frameCounter += Math.Abs(NPC.velocity.X);
                frame = RunFirst + (int)(NPC.frameCounter / StrideFrame) % RunFrames;
            }

            NPC.frame.Y = frame * frameHeight;
        }

        /// <summary>
        /// Drawn by hand: its feet on the bottom of its box, its middle on the box's middle, turned to
        /// the way it goes (the picture faces left), tipped a little with the slope of its jump.
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            frame.Height = texture.Height / Frames;
            Vector2 origin = new Vector2(frame.Width / 2f, frame.Height);
            Vector2 at = new Vector2(NPC.Center.X, NPC.Bottom.Y + NPC.gfxOffY) - screenPos;
            SpriteEffects flip = NPC.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float tilt = 0f;

            if (Now == Mode.Air && !NPC.IsABestiaryIconDummy)
                tilt = MathHelper.Clamp(NPC.velocity.Y * 0.035f, -0.35f, 0.35f) * NPC.spriteDirection;

            spriteBatch.Draw(texture, at.Floor(), frame, NPC.GetAlpha(drawColor), tilt, origin, NPC.scale, flip, 0f);
            return false;
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (Main.dedServ)
                return;

            int count = NPC.life > 0 ? 4 : 30;

            for (int i = 0; i < count; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 2f, -1.5f);
        }
    }
}
