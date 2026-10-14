using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// A zombie of Hoard of Undead's (the item is HoardOfUndead), by request: "Summon zombie that
    /// fights for you". It looks like one of the eight zombies of the game's Zombie page
    /// (Variants), picked at random as it is summoned and kept in ai[0]. One minion slot. It hurts
    /// what it walks into for the item's damage, 30, and the same enemy again only Cooldown ticks
    /// later (local immunity); another enemy at once.
    ///
    /// It walks as the game's own zombie of its kind walks: NPC.AI_003_Fighters and the rest of
    /// NPC.UpdateNPC (its gravity, its tile collision, its frames) as they are in 1.4.4, taken
    /// over step by step, with the user's numbers for how fast it walks, how fast it falls (Pull)
    /// and how it moves in liquids. Its speed and how fast it gets to it; stepping up a block; its
    /// jumps over what is in front of it (a zombie's 5, 6, 7 or 8 up, each made faster by the same
    /// share, so that it gets up 6 tiles: JumpHeight); down through platforms to what is below it;
    /// giving up for a while on what it cannot get to. It goes for the nearest enemy within 60
    /// tiles, as a zombie goes for the nearest player, and for the one its owner has marked before
    /// any; with none, back to its owner, to stand behind them. It cannot go through walls: nothing
    /// ever takes it through a block. Farther than 75 tiles from its owner it is brought back to
    /// them, even from a fight.
    ///
    /// Drawn with the zombie's own picture at 70% opacity, a fifth of the way to grey, inside an
    /// outline whose four colours go round it counter clockwise.
    /// </summary>
    public class HoardOfUndeadZombie : ModProjectile
    {
        /// <summary>The zombies of the game's Zombie page, whose pictures it takes: ai[0] is which.</summary>
        public static readonly int[] Variants =
        {
            NPCID.Zombie, NPCID.BaldZombie, NPCID.PincushionZombie, NPCID.SlimedZombie,
            NPCID.SwampZombie, NPCID.TwiggyZombie, NPCID.FemaleZombie, NPCID.TorchZombie,
        };

        /// <summary>How fast each of them walks at most: AI_003_Fighters' for its kind (1, unless it is one of these), and 0.65 faster (the user's).</summary>
        private static readonly float[] TopSpeeds = { 1f+0.65f, 0.95f + 0.65f, 1.1f + 0.65f, 0.9f + 0.65f, 1.2f + 0.65f, 0.8f + 0.65f, 0.87f + 0.65f, 1f + 0.65f };

        /// <summary>A zombie's size, every one of these (NPC.SetDefaults).</summary>
        public const int Width = 18;

        public const int Height = 40;

        /// <summary>"Target max range: 60 tiles": from its middle to an enemy's.</summary>
        public const float Range = 60f * 16f;

        /// <summary>Ticks before it hits the same enemy again (the request: "if attacking the same target then attack cooldown is 1.5 second", 90; the user's since: 35).</summary>
        public const int Cooldown = 35;

        /// <summary>How long a zombie keeps at what it cannot get to before it gives up on it for a while (AI_003_Fighters' 60).</summary>
        private const float GivesUp = 60f;

        /// <summary>Each step it takes in water, lava, honey and shimmer is its speed times this (a zombie's: 0.5, 0.5, 0.25 and 0.375; these are the user's).</summary>
        private const float WaterSpeed = 1f + 0.65f, LavaSpeed = 1f + 0.65f, HoneySpeed = 1f + 0.65f, ShimmerSpeed = 0.875f + 0.65f;

        /// <summary>Mine: where it stands with nothing to fight, behind its owner: this far behind them, and this much further for each zombie of theirs before it.</summary>
        private const float Behind = 30f, Apart = 22f;

        /// <summary>Mine: this close to its place behind its owner, it stops.</summary>
        private const float Near = 16f;

        /// <summary>Farther than this from its owner (75 tiles), it is brought back to them, whatever it was doing: by request, even in the middle of a fight.</summary>
        private const float TooFar = 75f * 16f;

        /// <summary>How much faster it falls each tick, at the ground's height and below (a zombie's: 0.3; the user's: 0.65); less high in the sky and in liquids, as a zombie's (Gravity).</summary>
        private const float Pull = 0.65f;

        /// <summary>"increase jump height to 6 tiles": the highest it gets up, onto what stands 6 tiles over its feet, and not onto 7.</summary>
        public const float JumpHeight = 6f * 16f;

        /// <summary>
        /// How much faster than a zombie it leaves the ground, in each of its jumps (AI_003_Fighters'
        /// 5, 6, 7, 7.9 and 8 up): its highest, the 8, lifts its feet to half a block under
        /// JumpHeight against Pull, and it steps up the last half block as a zombie steps up a block.
        /// The lower ones keep their share of it, as a zombie's do. Out of liquids only (JumpLift).
        /// </summary>
        private static readonly float Lift = Launch(JumpHeight - 8f, Pull) / 8f;

        /// <summary>
        /// Lift, on land. In a liquid (as it was found when it last moved) its jumps are a zombie's own,
        /// against the liquid's own gravity (Gravity): Pull is not what holds it down there.
        /// </summary>
        private float JumpLift => wet ? 1f : Lift;

        /// <summary>"Its texture is 70% opacity, 20% desaturated".</summary>
        private const float Opacity = 0.7f, Desaturation = 0.2f;

        /// <summary>
        /// "an outline that lerps the color with time counter clockwise: #c3b8d1,#b2bfd9,#b6a5b8,#89b2c4",
        /// at 70% (the user's). The game draws premultiplied, so 70% is the whole colour times 0.7.
        /// </summary>
        public static readonly Color[] Outline =
        {
            new Color(0xc3, 0xb8, 0xd1) * 0.7f, new Color(0xb2, 0xbf, 0xd9) * 0.7f, new Color(0xb6, 0xa5, 0xb8) * 0.7f, new Color(0x89, 0xb2, 0xc4) * 0.7f,
        };

        /// <summary>Mine: a turn of the outline's colours, in seconds.</summary>
        public const float OutlineTurn = 2f;

        // ---- the zombie's own state, as the game keeps it on a zombie ----------------------------

        /// <summary>Which of Variants it is.</summary>
        public int Variant => Utils.Clamp((int)Projectile.ai[0], 0, Variants.Length - 1);

        /// <summary>The enemy it is going for (an index into Main.npc), or -1 for its owner (NPC.target).</summary>
        private int target = -1;

        /// <summary>NPC.directionY: 1 when what it goes for is below it.</summary>
        private int directionY = 1;

        /// <summary>A zombie's ai[3]: how long it has been getting nowhere. At 60 it gives up on what it goes for and walks on; past 600 it is over it.</summary>
        private float bored;

        /// <summary>A zombie's ai[0]: ticks stood still while it has given up, before it turns round.</summary>
        private float stood;

        /// <summary>NPC.stairFall: it is going down through stairs.</summary>
        private bool stairFall;

        /// <summary>NPC.oldPosition: where it was before it last moved.</summary>
        private Vector2 before;

        /// <summary>NPC.collideX and collideY: a block stopped it on its last move.</summary>
        private bool collideX, collideY;

        /// <summary>NPC.wet, lavaWet, honeyWet and shimmerWet: in a liquid, and which, as it was found when it last moved.</summary>
        private bool wet, lavaWet, honeyWet, shimmerWet;

        /// <summary>NPC.frameCounter, and its frame (0 to 2).</summary>
        private double frameCounter;

        private int frame;

        /// <summary>It stood at its place behind its owner last tick (mine).</summary>
        private bool resting;

        private bool begun;

        /// <summary>Nothing of this file's is drawn by the game: the zombie's own picture is asked for (PreDraw draws it).</summary>
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Zombie;

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = Width;
            Projectile.height = Height;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;

            // It moves itself, as an NPC is moved, and keeps its own facing.
            Projectile.tileCollide = false;
            Projectile.manualDirectionChange = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Cooldown;
        }

        public override bool? CanCutTiles() => false;

        public override bool MinionContactDamage() => true;

        /// <summary>Its moving is done in AI, as a zombie's is done by NPC.UpdateNPC.</summary>
        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int buff = ModContent.BuffType<HoardOfUndeadBuff>();

            if (!owner.active || owner.dead)
            {
                owner.ClearBuff(buff);
                return;
            }

            if (owner.HasBuff(buff))
                Projectile.timeLeft = 2;

            if (!begun)
            {
                begun = true;
                before = Projectile.position;
                Projectile.direction = Projectile.spriteDirection = owner.direction == 0 ? 1 : owner.direction;
            }

            // Too far from its owner, it is brought back, whatever it was doing.
            if (Vector2.Distance(owner.Center, Projectile.Center) > TooFar)
                Fetch(owner);

            // ---- a tick of a zombie, in NPC.UpdateNPC's order (what a step up shifted its picture by is let
            // back by the game's own Projectile.Update, as NPC.UpdateNPC lets an NPC's) ----
            Gravity(out float gravity, out float maxFallSpeed);
            Think(owner);

            Projectile.velocity.Y += gravity;
            if (Projectile.velocity.Y > maxFallSpeed)
                Projectile.velocity.Y = maxFallSpeed;

            if ((double)Projectile.velocity.X < 0.005 && (double)Projectile.velocity.X > -0.005)
                Projectile.velocity.X = 0f;

            Move(gravity);
            Frame();
        }

        /// <summary>
        /// To its owner's feet (the space a player stands in is free), with nothing to fight: the enemy
        /// it had is let go of, and it picks again from where it is now. Where it was before stays its
        /// last place, so that it is not taken for stuck where it lands.
        /// </summary>
        private void Fetch(Player owner)
        {
            Projectile.position = new Vector2(owner.Center.X - Projectile.width / 2f, owner.position.Y + owner.height - Projectile.height);
            Projectile.velocity = Vector2.Zero;
            target = -1;
            Projectile.gfxOffY = 0f;
            bored = stood = 0f;
            stairFall = false;
            wet = lavaWet = honeyWet = shimmerWet = false;
            Projectile.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((short)target);
            writer.Write((sbyte)Projectile.direction);
            writer.Write((sbyte)directionY);
            writer.Write(bored);
            writer.Write(stood);
            writer.Write(stairFall);
            writer.Write((byte)((wet ? 1 : 0) | (lavaWet ? 2 : 0) | (honeyWet ? 4 : 0) | (shimmerWet ? 8 : 0)));
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            // Made on another machine: begun there.
            if (!begun)
            {
                begun = true;
                before = Projectile.position;
            }

            target = reader.ReadInt16();
            Projectile.direction = reader.ReadSByte();
            directionY = reader.ReadSByte();
            bored = reader.ReadSingle();
            stood = reader.ReadSingle();
            stairFall = reader.ReadBoolean();
            byte liquid = reader.ReadByte();
            wet = (liquid & 1) != 0;
            lavaWet = (liquid & 2) != 0;
            honeyWet = (liquid & 4) != 0;
            shimmerWet = (liquid & 8) != 0;
            Projectile.spriteDirection = Projectile.direction;
        }

        // ---- NPC.UpdateNPC ------------------------------------------------------------------------

        /// <summary>NPC.UpdateNPC_UpdateGravity: weaker high in the sky, and in liquids (as they were found when it last moved).</summary>
        private void Gravity(out float gravity, out float maxFallSpeed)
        {
            maxFallSpeed = 10f;
            gravity = Pull;

            float worlds = Main.maxTilesX / 4200f;
            worlds *= worlds;
            float space = (float)((double)(Projectile.position.Y / 16f - (60f + 10f * worlds)) / (Main.worldSurface / 6.0));
            if ((double)space < 0.25)
                space = 0.25f;
            if (space > 1f)
                space = 1f;
            gravity *= space;

            if (!wet)
                return;

            if (shimmerWet)
            {
                gravity = 0.15f;
                maxFallSpeed = 5.5f;
            }
            else if (honeyWet)
            {
                gravity = 0.1f;
                maxFallSpeed = 4f;
            }
            else
            {
                gravity = 0.2f;
                maxFallSpeed = 7f;
            }
        }

        /// <summary>
        /// NPC.UpdateCollision, as a zombie has it: down slopes, in a liquid or not, through platforms
        /// to what is below it, stopped by blocks, slowed in liquids, up and down stairs and slopes.
        /// </summary>
        private void Move(float gravity)
        {
            Vector4 down = Collision.WalkDownSlope(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height, gravity);
            Projectile.position.X = down.X;
            Projectile.position.Y = down.Y;
            Projectile.velocity.X = down.Z;
            Projectile.velocity.Y = down.W;

            // Collision_LavaCollision and Collision_WaterCollision: whether it is in a liquid now, and which; coming
            // out of one, it loses half its speed across (the splash is the game's own, Projectile.Update's).
            if (Collision.LavaCollision(Projectile.position, Projectile.width, Projectile.height))
                lavaWet = true;

            bool liquid = Collision.WetCollision(Projectile.position, Projectile.width, Projectile.height);
            if (Collision.honey)
                honeyWet = true;
            if (Collision.shimmer)
                shimmerWet = true;

            if (liquid)
            {
                wet = true;
            }
            else if (wet)
            {
                Projectile.velocity.X *= 0.5f;
                wet = false;
            }

            if (!wet)
                lavaWet = honeyWet = shimmerWet = false;

            // Collision_DecideFallThroughPlatforms: a fighter goes down through them to what is below it.
            bool fall = directionY == 1;

            Vector2 was = Projectile.velocity;
            collideX = collideY = false;
            Projectile.velocity = Collision.TileCollision(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height, fall, fall, 1);

            if (Collision.up)
                Projectile.velocity.Y = 0.01f;

            Vector2 step = Projectile.velocity;

            if (wet)
            {
                float slow = shimmerWet ? ShimmerSpeed : honeyWet ? HoneySpeed : lavaWet ? LavaSpeed : WaterSpeed;
                step = Projectile.velocity * slow;

                if (Projectile.velocity.X != was.X)
                {
                    step.X = Projectile.velocity.X;
                    collideX = true;
                }

                if (Projectile.velocity.Y != was.Y)
                {
                    step.Y = Projectile.velocity.Y;
                    collideY = true;
                }

                // Mine: the user's liquids take it further than its speed (slow over 1), and the game asks only of
                // its speed what is in the way. Asked again of the step it takes, so that no liquid takes it into a block.
                if (slow > 1f)
                {
                    Vector2 asked = step;
                    step = Collision.TileCollision(Projectile.position, step, Projectile.width, Projectile.height, fall, fall, 1);

                    if (step.X != asked.X)
                        collideX = true;
                    if (step.Y != asked.Y)
                        collideY = true;
                }
            }
            else
            {
                if (was.X != Projectile.velocity.X)
                    collideX = true;
                if (was.Y != Projectile.velocity.Y)
                    collideY = true;
            }

            before = Projectile.position;
            Projectile.position += step;

            // Collision_MoveSlopesAndStairFall
            if (fall)
                stairFall = true;

            Vector4 slope = Collision.SlopeCollision(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height, gravity, stairFall);

            if (Collision.stairFall)
                stairFall = true;
            else if (!fall)
                stairFall = false;

            if (Collision.stair && Math.Abs(slope.Y - Projectile.position.Y) > 8f)
            {
                Projectile.gfxOffY -= slope.Y - Projectile.position.Y;
                Projectile.stepSpeed = 2f;
            }

            Projectile.position.X = slope.X;
            Projectile.position.Y = slope.Y;
            Projectile.velocity.X = slope.Z;
            Projectile.velocity.Y = slope.W;
        }

        /// <summary>NPC.FindFrame for a zombie: standing, the three steps of its walk, and the last of them in the air or turning.</summary>
        private void Frame()
        {
            if (Projectile.velocity.Y == 0f)
            {
                if (Projectile.direction == 1)
                    Projectile.spriteDirection = 1;
                if (Projectile.direction == -1)
                    Projectile.spriteDirection = -1;
            }

            if (Projectile.velocity.Y != 0f || (Projectile.direction == -1 && Projectile.velocity.X > 0f) || (Projectile.direction == 1 && Projectile.velocity.X < 0f))
            {
                frameCounter = 0.0;
                frame = 2;
                return;
            }

            if (Projectile.velocity.X == 0f)
            {
                frameCounter = 0.0;
                frame = 0;
                return;
            }

            frameCounter += Math.Abs(Projectile.velocity.X);

            if (frameCounter < 8.0)
                frame = 0;
            else if (frameCounter < 16.0)
                frame = 1;
            else if (frameCounter < 24.0)
                frame = 2;
            else if (frameCounter < 32.0)
                frame = 1;
            else
                frameCounter = 0.0;
        }

        // ---- NPC.AI_003_Fighters --------------------------------------------------------------

        /// <summary>What it goes for, as a zombie sees its player: a box, where it is and how big.</summary>
        private readonly struct Aim
        {
            public readonly Vector2 Position;
            public readonly int Width, Height;

            public Aim(Vector2 position, int width, int height)
            {
                Position = position;
                Width = width;
                Height = height;
            }

            public Rectangle Box => new Rectangle((int)Position.X, (int)Position.Y, Width, Height);

            public Vector2 Center => new Vector2(Position.X + (float)(Width / 2), Position.Y + (float)(Height / 2));

            public float Bottom => Position.Y + (float)Height;
        }

        /// <summary>
        /// What it goes for: the enemy it has; with none, its owner's own box moved to its place
        /// behind them, so that it follows them up and down as a zombie follows a player.
        /// </summary>
        private Aim Aiming(Player owner)
        {
            if (target >= 0)
            {
                NPC npc = Main.npc[target];
                return new Aim(npc.position, npc.width, npc.height);
            }

            return new Aim(new Vector2(Place(owner) - owner.width / 2f, owner.position.Y), owner.width, owner.height);
        }

        /// <summary>Its place behind its owner, further back for each zombie of theirs before it (mine).</summary>
        private float Place(Player owner)
        {
            int ahead = 0;
            for (int i = 0; i < Projectile.whoAmI; i++)
            {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == Projectile.owner && other.type == Projectile.type)
                    ahead++;
            }

            int facing = owner.direction == 0 ? 1 : owner.direction;
            return owner.Center.X - facing * (Behind + Apart * ahead);
        }

        /// <summary>
        /// The enemy it goes for, as NPC.TargetClosest picks a zombie's player: the nearest, by how
        /// far across and how far up or down together, the first of any as near; within 60 tiles.
        /// The one its owner has marked before any, if it is within them (mine, as the game's minions
        /// do). -1 for none.
        /// </summary>
        private int Choose(Player owner)
        {
            if (owner.HasMinionAttackTargetNPC)
            {
                NPC marked = Main.npc[owner.MinionAttackTargetNPC];
                if (marked.CanBeChasedBy(Projectile) && Vector2.Distance(marked.Center, Projectile.Center) <= Range)
                    return marked.whoAmI;
            }

            int best = -1;
            float bestDistance = 0f;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile) || Vector2.Distance(npc.Center, Projectile.Center) > Range)
                    continue;

                float distance = Math.Abs(npc.Center.X - Projectile.Center.X) + Math.Abs(npc.Center.Y - Projectile.Center.Y);
                if (best == -1 || distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>An active, unactuated tile of a solid kind (platforms are).</summary>
        private static bool Solid(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasUnactuatedTile && Main.tileSolid[tile.TileType];
        }

        /// <summary>
        /// AI_003_Fighters, the way a zombie goes through it, in its order. Left out: its moan, its
        /// leaving by day, and the doors it knocks on (to this, a closed door is a wall).
        /// </summary>
        private void Think(Player owner)
        {
            // The enemy it had may be gone.
            if (target >= 0 && !Main.npc[target].CanBeChasedBy(Projectile))
                target = -1;

            Aim aim = Aiming(owner);

            // Level with the feet of what it goes for, it is not under it.
            if (aim.Position.Y + (float)aim.Height == Projectile.position.Y + (float)Projectile.height)
                directionY = -1;

            if (Variants[Variant] == NPCID.TorchZombie)
                Torch();

            bool ground = false;
            bool stopped = Projectile.velocity.X == 0f;

            // Mine: at its place behind its owner it is waiting, not stuck.
            bool waiting = target == -1 && bored < GivesUp && Math.Abs(aim.Center.X - Projectile.Center.X) < Near;

            if (!waiting)
            {
                bool turning = Projectile.velocity.Y == 0f && ((Projectile.velocity.X > 0f && Projectile.direction < 0) || (Projectile.velocity.X < 0f && Projectile.direction > 0));

                // (Setting off from where it waited is not getting nowhere: mine.)
                bool still = Projectile.position.X == before.X && !resting;

                if (still || bored >= GivesUp || turning)
                    bored++;
                else if ((double)Math.Abs(Projectile.velocity.X) > 0.9 && bored > 0f)
                    bored--;

                if (bored > GivesUp * 10f)
                    bored = 0f;

                if (bored == GivesUp)
                    Projectile.netUpdate = true;

                if (aim.Box.Intersects(Projectile.Hitbox))
                    bored = 0f;
            }

            if (bored < GivesUp)
            {
                int had = target;
                int facing = Projectile.direction;
                int vertical = directionY;

                target = Choose(owner);
                aim = Aiming(owner);

                // NPC.TargetClosest, facing what it goes for, as it has a fighter do.
                Rectangle box = aim.Box;
                Projectile.direction = (float)(box.X + box.Width / 2) < Projectile.position.X + (float)(Projectile.width / 2) ? -1 : 1;
                directionY = (float)(box.Y + box.Height) <= Projectile.position.Y + (float)Projectile.height ? -1 : 1;

                if ((Projectile.direction != facing || directionY != vertical || target != had) && !collideX && !collideY)
                    Projectile.netUpdate = true;

                if (directionY > 0 && aim.Center.Y <= Projectile.Bottom.Y)
                    directionY = -1;
            }
            else
            {
                // Given up: it walks on the way it faces, and turns round when it stands against something.
                if (Projectile.velocity.X == 0f)
                {
                    if (Projectile.velocity.Y == 0f)
                    {
                        stood++;
                        if (stood >= 2f)
                        {
                            Projectile.direction *= -1;
                            Projectile.spriteDirection = Projectile.direction;
                            stood = 0f;
                        }
                    }
                }
                else
                {
                    stood = 0f;
                }

                if (Projectile.direction == 0)
                    Projectile.direction = 1;
            }

            // Mine: at its place behind its owner (with what it took to go for this tick), it is where it was
            // going, as a zombie is up against its player (nothing to give up on), and slows to a stop, as a
            // zombie slows when it goes too fast.
            bool rest = target == -1 && bored < GivesUp && Math.Abs(aim.Center.X - Projectile.Center.X) < Near;
            resting = rest;

            if (rest)
            {
                bored = 0f;
                if (Projectile.velocity.Y == 0f)
                    Projectile.velocity.X *= 0.8f;
            }
            else
            {
                // Walking: up to its top speed, 0.07 faster each tick; slowed on the ground going faster than that.
                float top = TopSpeeds[Variant];

                if (Projectile.velocity.X < 0f - top || Projectile.velocity.X > top)
                {
                    if (Projectile.velocity.Y == 0f)
                        Projectile.velocity *= 0.8f;
                }
                else if (Projectile.velocity.X < top && Projectile.direction == 1)
                {
                    Projectile.velocity.X += 0.07f;
                    if (Projectile.velocity.X > top)
                        Projectile.velocity.X = top;
                }
                else if (Projectile.velocity.X > 0f - top && Projectile.direction == -1)
                {
                    Projectile.velocity.X -= 0.07f;
                    if (Projectile.velocity.X < 0f - top)
                        Projectile.velocity.X = 0f - top;
                }
            }

            // On the ground, with room over its head to jump.
            if (Projectile.velocity.Y == 0f)
            {
                int below = (int)(Projectile.position.Y + (float)Projectile.height + 7f) / 16;
                int over = (int)(Projectile.position.Y - 9f) / 16;
                int left = (int)Projectile.position.X / 16;
                int right = (int)(Projectile.position.X + (float)Projectile.width) / 16;
                int from = (int)(Projectile.position.X + 8f) / 16;
                int to = (int)(Projectile.position.X + (float)Projectile.width - 8f) / 16;

                for (int x = from; x <= to; x++)
                {
                    if (Solid(x, over))
                    {
                        ground = false;
                        break;
                    }

                    if (x >= left && x <= right && Solid(x, below))
                        ground = true;
                }

                if (!ground && Projectile.velocity.Y < 0f)
                    Projectile.velocity.Y = 0f;
            }

            if (Projectile.velocity.Y >= 0f)
                StepUp();

            if (ground)
                Jump(aim, stopped);
        }

        /// <summary>AI_003_Fighters: walking into a step no higher than a block, it is put on top of it, its picture let up to it slowly.</summary>
        private void StepUp()
        {
            int way = 0;
            if (Projectile.velocity.X < 0f)
                way = -1;
            if (Projectile.velocity.X > 0f)
                way = 1;

            Vector2 next = Projectile.position;
            next.X += Projectile.velocity.X;

            int x = (int)((next.X + (float)(Projectile.width / 2) + (float)((Projectile.width / 2 + 1) * way)) / 16f);
            int y = (int)((next.Y + (float)Projectile.height - 1f) / 16f);

            if (!WorldGen.InWorld(x, y, 4))
                return;

            Tile step = Main.tile[x, y];
            Tile one = Main.tile[x, y - 1];
            Tile two = Main.tile[x, y - 2];
            Tile three = Main.tile[x, y - 3];
            Tile four = Main.tile[x, y - 4];
            Tile back = Main.tile[x - way, y - 3];

            if (!((float)(x * 16) < next.X + (float)Projectile.width && (float)(x * 16 + 16) > next.X))
                return;

            bool foot = (step.HasUnactuatedTile && !step.TopSlope && !one.TopSlope && Main.tileSolid[step.TileType] && !Main.tileSolidTop[step.TileType])
                || (one.IsHalfBlock && one.HasUnactuatedTile);

            bool room = (!one.HasUnactuatedTile || !Main.tileSolid[one.TileType] || Main.tileSolidTop[one.TileType]
                    || (one.IsHalfBlock && (!four.HasUnactuatedTile || !Main.tileSolid[four.TileType] || Main.tileSolidTop[four.TileType])))
                && (!two.HasUnactuatedTile || !Main.tileSolid[two.TileType] || Main.tileSolidTop[two.TileType])
                && (!three.HasUnactuatedTile || !Main.tileSolid[three.TileType] || Main.tileSolidTop[three.TileType])
                && (!back.HasUnactuatedTile || !Main.tileSolid[back.TileType]);

            if (!foot || !room)
                return;

            float floor = y * 16;
            if (step.IsHalfBlock)
                floor += 8f;
            if (one.IsHalfBlock)
                floor -= 8f;

            if (!(floor < next.Y + (float)Projectile.height))
                return;

            float rise = next.Y + (float)Projectile.height - floor;
            if (!(rise <= 16.1f))
                return;

            Projectile.gfxOffY += Projectile.position.Y + (float)Projectile.height - floor;
            Projectile.position.Y = floor - (float)Projectile.height;
            Projectile.stepSpeed = rise < 9f ? 1f : 2f;
        }

        /// <summary>
        /// How fast it has to leave the ground to rise this far: its gravity is added before each move
        /// (NPC.UpdateNPC), so leaving at v it rises v * v / 2g - v / 2, and up to an eighth of g more.
        /// </summary>
        private static float Launch(float height, float gravity) => gravity / 2f + (float)Math.Sqrt(gravity * gravity / 4f + 2f * gravity * height);

        /// <summary>
        /// AI_003_Fighters: walking the way it faces, on the ground, it jumps what is in front of it:
        /// 8 up for a wall four blocks high or more, 7 for three, 6 for two, 5 for one it stands too
        /// low to step up; 8, and half again as fast across, over a drop with what it goes for above
        /// it; 5 to get unstuck. In Expert and Master, also 7.9 up at what it goes for above it, if it
        /// can see it. Each of them JumpLift times as fast as a zombie's.
        /// </summary>
        private void Jump(Aim aim, bool stopped)
        {
            int x = (int)((Projectile.position.X + (float)(Projectile.width / 2) + (float)(15 * Projectile.direction)) / 16f);
            int y = (int)((Projectile.position.Y + (float)Projectile.height - 15f) / 16f);

            bool forward = (Projectile.velocity.X < 0f && Projectile.spriteDirection == -1) || (Projectile.velocity.X > 0f && Projectile.spriteDirection == 1);
            if (!forward)
                return;

            if (Projectile.height >= 32 && Solid(x, y - 2))
            {
                Projectile.velocity.Y = (Solid(x, y - 3) ? -8f : -7f) * JumpLift;
                Projectile.netUpdate = true;
            }
            else if (Solid(x, y - 1))
            {
                Projectile.velocity.Y = -6f * JumpLift;
                Projectile.netUpdate = true;
            }
            else if (Projectile.position.Y + (float)Projectile.height - (float)(y * 16) > 20f && Main.tile[x, y].HasUnactuatedTile && !Main.tile[x, y].TopSlope && Main.tileSolid[Main.tile[x, y].TileType])
            {
                Projectile.velocity.Y = -5f * JumpLift;
                Projectile.netUpdate = true;
            }
            else if (directionY < 0 && !Solid(x, y + 1) && !Solid(x + Projectile.direction, y + 1))
            {
                Projectile.velocity.Y = -8f * JumpLift;
                Projectile.velocity.X *= 1.5f;
                Projectile.netUpdate = true;
            }

            if (Projectile.velocity.Y == 0f && stopped && bored == 1f)
                Projectile.velocity.Y = -5f * JumpLift;

            if (Projectile.velocity.Y == 0f && Main.expertMode && aim.Bottom < Projectile.position.Y
                && Math.Abs(Projectile.Center.X - aim.Center.X) < (float)(aim.Width * 3)
                && Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, aim.Position, aim.Width, aim.Height))
            {
                if (aim.Bottom > Projectile.position.Y - (float)(6 * 16))
                {
                    Projectile.velocity.Y = -7.9f * JumpLift;
                }
                else
                {
                    int column = (int)(Projectile.Center.X / 16f);
                    int row = (int)(Projectile.Bottom.Y / 16f) - 1;
                    for (int up = row; up > row - 6; up--)
                    {
                        Tile tile = Main.tile[column, up];
                        if (tile.HasUnactuatedTile && TileID.Sets.Platforms[tile.TileType])
                        {
                            Projectile.velocity.Y = -7.9f * JumpLift;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>AI_003_Fighters: the Torch Zombie's torch lights where it is held, and now and then lets a spark go.</summary>
        private void Torch()
        {
            int y = (int)(Projectile.position.Y + 6f) / 16;
            float held = Projectile.spriteDirection < 0 ? Projectile.Center.X - 22f : Projectile.Center.X + 14f;
            int x = (int)held / 16;
            int beside = Projectile.spriteDirection < 0 ? x + 1 : x - 1;

            if (!WorldGen.InWorld(x, y, 0) || Framing.GetTileSafely(beside, y).LiquidAmount != 0 || Framing.GetTileSafely(x, y).LiquidAmount != 0)
                return;

            Lighting.AddLight(x, y, 1f, 0.95f, 0.8f);

            if (Main.rand.Next(30) == 0)
                Dust.NewDust(new Vector2(held, Projectile.position.Y + 6f), 1, 1, DustID.Torch);
        }

        // ---- drawn ------------------------------------------------------------------------------

        /// <summary>
        /// A zombie's picture as these draw it: its own, a fifth of the way to grey; and, for each of
        /// its frames, the art pixels (2 by 2 texels) of its outline, one art pixel wide, round the
        /// pixels that are not empty.
        /// </summary>
        private sealed class Look
        {
            public Texture2D Picture;

            public int FrameHeight;

            /// <summary>Per frame: each outline art pixel's top left texel, from the frame's top left (it may be outside it).</summary>
            public Point[][] Ring;

            /// <summary>The middle of the zombie, in texels from a frame's top left: what the outline's colours go round.</summary>
            public Vector2 Middle;
        }

        private static readonly Dictionary<int, Look> Looks = new Dictionary<int, Look>();

        public override void Unload()
        {
            foreach (Look look in Looks.Values)
            {
                Texture2D picture = look.Picture;
                Main.QueueMainThreadAction(() => picture.Dispose());
            }

            Looks.Clear();
        }

        /// <summary>Made the first time a zombie of that kind is drawn, on the thread that draws.</summary>
        private static Look LookOf(int npcType)
        {
            if (Looks.TryGetValue(npcType, out Look made))
                return made;

            Texture2D own = ModContent.Request<Texture2D>("Terraria/Images/NPC_" + npcType, AssetRequestMode.ImmediateLoad).Value;
            int width = own.Width;
            int height = own.Height;
            Color[] texels = new Color[width * height];
            own.GetData(texels);

            // A fifth of the way to its own grey (its brightness to the eye), each texel; the alpha kept.
            Color[] greyer = new Color[texels.Length];
            for (int i = 0; i < texels.Length; i++)
            {
                Color c = texels[i];
                float grey = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
                greyer[i] = new Color(
                    (int)Math.Round(c.R + (grey - c.R) * Desaturation),
                    (int)Math.Round(c.G + (grey - c.G) * Desaturation),
                    (int)Math.Round(c.B + (grey - c.B) * Desaturation),
                    (int)c.A);
            }

            Texture2D picture = new Texture2D(Main.instance.GraphicsDevice, width, height);
            picture.SetData(greyer);

            int frames = Main.npcFrameCount[npcType];
            int frameHeight = height / frames;
            int across = width / 2;
            int down = frameHeight / 2;

            // The game's pictures are drawn in art pixels of 2 by 2 texels: the outline is one art pixel
            // wide, on the same grid, round every one that is not empty (side by side, not corner to corner).
            Point[][] ring = new Point[frames][];
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;

            for (int f = 0; f < frames; f++)
            {
                bool Filled(int i, int j)
                {
                    if (i < 0 || j < 0 || i >= across || j >= down)
                        return false;

                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                            if (texels[(f * frameHeight + j * 2 + dy) * width + i * 2 + dx].A > 0)
                                return true;

                    return false;
                }

                var points = new List<Point>();
                for (int j = -1; j <= down; j++)
                {
                    for (int i = -1; i <= across; i++)
                    {
                        if (Filled(i, j))
                        {
                            minX = Math.Min(minX, i);
                            maxX = Math.Max(maxX, i);
                            minY = Math.Min(minY, j);
                            maxY = Math.Max(maxY, j);
                            continue;
                        }

                        if (Filled(i - 1, j) || Filled(i + 1, j) || Filled(i, j - 1) || Filled(i, j + 1))
                            points.Add(new Point(i * 2, j * 2));
                    }
                }

                ring[f] = points.ToArray();
            }

            made = new Look
            {
                Picture = picture,
                FrameHeight = frameHeight,
                Ring = ring,
                Middle = minX > maxX ? new Vector2(width / 2f, frameHeight / 2f) : new Vector2(minX + maxX + 1, minY + maxY + 1),
            };

            Looks[npcType] = made;
            return made;
        }

        /// <summary>The colours evenly round a turn, the last running back into the first: where a point a number of turns round is.</summary>
        private static Color Round(Color[] colours, float turns)
        {
            float around = (turns - (float)Math.Floor(turns)) * colours.Length;
            int i = Math.Min((int)around, colours.Length - 1);
            return Color.Lerp(colours[i], colours[(i + 1) % colours.Length], around - i);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Look look = LookOf(Variants[Variant]);
            Texture2D picture = look.Picture;
            int frameHeight = look.FrameHeight;
            Rectangle source = new Rectangle(0, frame * frameHeight, picture.Width, frameHeight);
            Vector2 half = new Vector2(picture.Width / 2, frameHeight / 2);

            // Where the game draws a zombie (Main.DrawNPCDirect): its middle over its box's, its feet 4 below
            // its bottom, shifted by its step up or down.
            Vector2 at = new Vector2(
                Projectile.position.X - Main.screenPosition.X + (float)(Projectile.width / 2) - (float)picture.Width / 2f + half.X,
                Projectile.position.Y - Main.screenPosition.Y + (float)Projectile.height - (float)frameHeight + 4f + half.Y + Projectile.gfxOffY);

            bool flipped = Projectile.spriteDirection == 1;
            Main.EntitySpriteDraw(picture, at, source, lightColor * Opacity, 0f, half, 1f, flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

            // The outline, art pixel by art pixel, each the colour of where it is round the zombie's middle:
            // the colours go round counter clockwise, a turn every OutlineTurn seconds; not lit.
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle dot = new Rectangle(0, 0, 1, 1);
            Vector2 corner = at - half;
            Vector2 middle = corner + new Vector2(flipped ? picture.Width - look.Middle.X : look.Middle.X, look.Middle.Y);
            float turned = Main.GlobalTimeWrappedHourly / OutlineTurn;

            foreach (Point point in look.Ring[frame])
            {
                Vector2 spot = corner + new Vector2(flipped ? picture.Width - 2 - point.X : point.X, point.Y);
                Vector2 from = spot + Vector2.One - middle;
                float angle = (float)Math.Atan2(-from.Y, from.X);
                Main.EntitySpriteDraw(pixel, spot, dot, Round(Outline, turned - angle / MathHelper.TwoPi), 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
