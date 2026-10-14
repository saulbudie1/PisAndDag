using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// A squad member that walks: NPC.AI_003_Fighters, the way the game's skeletons go through it in
    /// 1.4.4, in its order, with their numbers (Hoard of Undead's zombie, HoardOfUndeadZombie, was
    /// the first of these): it walks at what it goes for, steps up a block, jumps what is in front
    /// of it, goes down through platforms to what is below it, and gives up for a while on what it
    /// cannot get to. With nothing to fight it goes back to its owner and stands behind them (mine,
    /// as Hoard of Undead's). Left out: its moan, its leaving by day and the doors it opens (to
    /// this, a closed door is a wall).
    /// </summary>
    public abstract class BoneSquadWalker : BoneSquadBody
    {
        /// <summary>How long it keeps at what it cannot get to before it gives up on it for a while (AI_003_Fighters' 60).</summary>
        private const float GivesUp = 60f;

        /// <summary>Mine (Hoard of Undead's): where it stands with nothing to fight, behind its owner: this far behind them, and this much further for each walker of the squad's before it.</summary>
        private const float Behind = 30f, Apart = 22f;

        /// <summary>Mine (Hoard of Undead's): this close to its place behind its owner, it stops.</summary>
        private const float Near = 16f;

        /// <summary>NPC.directionY: 1 when what it goes for is below it.</summary>
        protected int directionY = 1;

        /// <summary>AI_003's ai[3]: how long it has been getting nowhere. At 60 it gives up on what it goes for and walks on; past 600 it is over it.</summary>
        protected float bored;

        /// <summary>AI_003's ai[0]: ticks stood still while it has given up, before it turns round.</summary>
        protected float stood;

        /// <summary>It stood at its place behind its owner last tick (mine).</summary>
        protected bool resting;

        /// <summary>NPC.frameCounter, and its frame.</summary>
        protected double frameCounter;

        protected int frame;

        /// <summary>Busy with something it will not be bored at or turned from (the archer drawing its bow: AI_003's ai[2] above 0).</summary>
        protected virtual bool Busy => false;

        /// <summary>Main.NPCAddHeight for its NPC.</summary>
        protected virtual float AddHeight => 0f;

        /// <summary>
        /// AI_003_Fighters' highest jump, 8 up whatever is in front of it: at a wall four blocks high
        /// or more, and over a drop at what it goes for above it. Given how high that wall's top, or
        /// what it goes for, stands over its feet (px): the melee ones go up to 10 blocks
        /// (BoneSquadMelee).
        /// </summary>
        protected virtual float HighJump(float height) => 8f;

        protected override bool FallThrough => directionY == 1;

        protected override void Fetch(Player owner)
        {
            base.Fetch(owner);
            bored = stood = 0f;
            resting = false;
        }

        protected override void SendState(BinaryWriter writer)
        {
            base.SendState(writer);
            writer.Write((sbyte)directionY);
            writer.Write(bored);
            writer.Write(stood);
        }

        protected override void ReceiveState(BinaryReader reader)
        {
            base.ReceiveState(reader);
            directionY = reader.ReadSByte();
            bored = reader.ReadSingle();
            stood = reader.ReadSingle();
        }

        // ---- what it goes for ----------------------------------------------------------------

        /// <summary>What it goes for, as a fighter sees its player: a box, where it is and how big.</summary>
        protected readonly struct Aim
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

        /// <summary>The enemy it has; with none, its owner's own box moved to its place behind them, so that it follows them up and down as a fighter follows a player.</summary>
        protected Aim Aiming(Player owner)
        {
            if (target >= 0)
            {
                NPC npc = Main.npc[target];
                return new Aim(npc.position, npc.width, npc.height);
            }

            return new Aim(new Vector2(Place(owner) - owner.width / 2f, owner.position.Y), owner.width, owner.height);
        }

        /// <summary>Its place behind its owner, further back for each walker of the squad's before it (mine, as Hoard of Undead's).</summary>
        private float Place(Player owner)
        {
            int ahead = 0;
            for (int i = 0; i < Projectile.whoAmI; i++)
            {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == Projectile.owner && other.ModProjectile is BoneSquadWalker)
                    ahead++;
            }

            int facing = owner.direction == 0 ? 1 : owner.direction;
            return owner.Center.X - facing * (Behind + Apart * ahead);
        }

        /// <summary>
        /// NPC.TargetClosest's facing: towards what it goes for, across and up or down, and the
        /// network told when that changes, unless a block stopped it on its last move.
        /// </summary>
        protected void Face(Aim aim, int had)
        {
            int facing = Projectile.direction;
            int vertical = directionY;

            Rectangle box = aim.Box;
            Projectile.direction = (float)(box.X + box.Width / 2) < Projectile.position.X + (float)(Projectile.width / 2) ? -1 : 1;
            directionY = (float)(box.Y + box.Height) <= Projectile.position.Y + (float)Projectile.height ? -1 : 1;

            if ((Projectile.direction != facing || directionY != vertical || target != had) && !collideX && !collideY)
                Projectile.netUpdate = true;
        }

        /// <summary>An active, unactuated tile of a solid kind (platforms are).</summary>
        protected static bool Solid(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasUnactuatedTile && Main.tileSolid[tile.TileType];
        }

        // ---- NPC.AI_003_Fighters ----------------------------------------------------------------

        protected override void Think(Player owner)
        {
            Forget();
            Aim aim = Aiming(owner);

            // Level with the feet of what it goes for, it is not under it.
            if (aim.Position.Y + (float)aim.Height == Projectile.position.Y + (float)Projectile.height)
                directionY = -1;

            bool ground = false;
            bool stopped = Projectile.velocity.X == 0f;

            // Mine: at its place behind its owner it is waiting, not stuck.
            bool waiting = target == -1 && bored < GivesUp && Math.Abs(aim.Center.X - Projectile.Center.X) < Near;

            if (!waiting && !Busy)
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
                target = Choose(owner);
                aim = Aiming(owner);
                Face(aim, had);

                if (directionY > 0 && aim.Center.Y <= Projectile.Bottom.Y)
                    directionY = -1;
            }
            else if (!Busy)
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
            // going, as a fighter is up against its player, and slows to a stop.
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
                Walk();
            }

            Glow();
            Act(owner, ref aim, rest);

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

            // Mine: at its place it is not going anywhere, and does not jump at a wall it slows to a stop beside.
            if (ground && !rest)
                Jump(aim, stopped);
        }

        /// <summary>Its walking, AI_003_Fighters' for its kind (the archer's is part of its shooting: Act).</summary>
        protected virtual void Walk()
        {
        }

        /// <summary>Its light, AI_003_Fighters' for its kind.</summary>
        protected virtual void Glow()
        {
        }

        /// <summary>What its kind does besides walking (the archer's bow), in AI_003_Fighters' place for it.</summary>
        protected virtual void Act(Player owner, ref Aim aim, bool rest)
        {
        }

        /// <summary>
        /// AI_003_Fighters' walking: up to its top speed, so much faster each tick; on the ground, going
        /// faster than its top speed, it is slowed.
        /// </summary>
        protected void Accelerate(float top, float speedUp, float slowed)
        {
            if (Projectile.velocity.X < 0f - top || Projectile.velocity.X > top)
            {
                if (Projectile.velocity.Y == 0f)
                    Projectile.velocity *= slowed;
            }
            else if (Projectile.velocity.X < top && Projectile.direction == 1)
            {
                Projectile.velocity.X += speedUp;
                if (Projectile.velocity.X > top)
                    Projectile.velocity.X = top;
            }
            else if (Projectile.velocity.X > 0f - top && Projectile.direction == -1)
            {
                Projectile.velocity.X -= speedUp;
                if (Projectile.velocity.X < 0f - top)
                    Projectile.velocity.X = 0f - top;
            }
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

        /// <summary>How many blocks up it looks at a wall in front of it, for how high to jump (the melee ones jump no higher than 10).</summary>
        private const int WallRows = 10;

        /// <summary>
        /// AI_003_Fighters: walking the way it faces, on the ground, it jumps what is in front of it:
        /// its highest (HighJump: the game's 8 up, the melee ones' up to 10 blocks) for a wall four
        /// blocks high or more, 7 for three, 6 for two, 5 for one it stands too low to step up; its
        /// highest, and half again as fast across, over a drop with what it goes for above it; 5 to
        /// get unstuck. In Expert and Master, also 7.9 up at what it goes for above it, if it can see
        /// it. Then what its kind does after (Bone Lee's leap and faster jumps: AfterJump).
        /// </summary>
        private void Jump(Aim aim, bool stopped)
        {
            int x = (int)((Projectile.position.X + (float)(Projectile.width / 2) + (float)(15 * Projectile.direction)) / 16f);
            int y = (int)((Projectile.position.Y + (float)Projectile.height - 15f) / 16f);

            bool forward = (Projectile.velocity.X < 0f && Projectile.spriteDirection == -1) || (Projectile.velocity.X > 0f && Projectile.spriteDirection == 1);
            if (forward)
            {
                if (Projectile.height >= 32 && Solid(x, y - 2))
                {
                    if (Solid(x, y - 3))
                    {
                        // How high the wall stands over its feet, looked at up to WallRows blocks.
                        int rows = 4;
                        while (rows < WallRows && y - rows >= 0 && Solid(x, y - rows))
                            rows++;

                        Projectile.velocity.Y = -HighJump(Projectile.position.Y + (float)Projectile.height - (float)((y - rows + 1) * 16));
                    }
                    else
                    {
                        Projectile.velocity.Y = -7f;
                    }

                    Projectile.netUpdate = true;
                }
                else if (Solid(x, y - 1))
                {
                    Projectile.velocity.Y = -6f;
                    Projectile.netUpdate = true;
                }
                else if (Projectile.position.Y + (float)Projectile.height - (float)(y * 16) > 20f && Main.tile[x, y].HasUnactuatedTile && !Main.tile[x, y].TopSlope && Main.tileSolid[Main.tile[x, y].TileType])
                {
                    Projectile.velocity.Y = -5f;
                    Projectile.netUpdate = true;
                }
                else if (directionY < 0 && !Solid(x, y + 1) && !Solid(x + Projectile.direction, y + 1))
                {
                    Projectile.velocity.Y = -HighJump(Projectile.position.Y + (float)Projectile.height - aim.Bottom);
                    Projectile.velocity.X *= 1.5f;
                    Projectile.netUpdate = true;
                }

                if (Projectile.velocity.Y == 0f && stopped && bored == 1f)
                    Projectile.velocity.Y = -5f;

                if (Projectile.velocity.Y == 0f && Main.expertMode && aim.Bottom < Projectile.position.Y
                    && Math.Abs(Projectile.Center.X - aim.Center.X) < (float)(aim.Width * 3)
                    && Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, aim.Position, aim.Width, aim.Height))
                {
                    if (aim.Bottom > Projectile.position.Y - (float)(6 * 16))
                    {
                        Projectile.velocity.Y = -7.9f;
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
                                Projectile.velocity.Y = -7.9f;
                                break;
                            }
                        }
                    }
                }
            }

            AfterJump(aim);
        }

        /// <summary>What its kind does after its jumps, on the ground (Bone Lee's leap).</summary>
        protected virtual void AfterJump(Aim aim)
        {
        }

        // ---- drawn ------------------------------------------------------------------------------

        /// <summary>The colour the game draws its NPC in, from the light where it is (Hell Armored Bones change it).</summary>
        protected virtual Color Lit(Color light) => light;

        public override bool PreDraw(ref Color lightColor)
        {
            DrawNpc(Kind, frame, 0f, Lit(lightColor), AddHeight);
            return false;
        }
    }

    /// <summary>
    /// The squad's melee walkers, Bone Lee and the two Armored Bones, by request: "Make all melee
    /// bone minions be able to jump 10 blocks and if its on the return state but havent return for
    /// 5 sec, js tele them back". Their highest jump goes as high as the wall in front of them, or
    /// what they go for over a drop, needs, up to 10 blocks and never lower than the game's 8: onto
    /// a wall 10 blocks high, not 11 (on land; in a liquid it is the game's). With nothing to fight,
    /// one that has not been back with its owner for 5 seconds together is put at its owner's feet,
    /// as it is from 75 tiles off (Fetch), with nothing to show it. Back (mine): within 10 tiles of
    /// its owner, middle to middle, with nothing solid between them. Only its owner's machine
    /// decides it; the others are told. The Skeleton Archer shoots, and is left as it was.
    /// </summary>
    public abstract class BoneSquadMelee : BoneSquadWalker
    {
        /// <summary>By request ("be able to jump 10 blocks"): the highest it gets up, onto what stands 10 blocks over its feet, and not onto 11.</summary>
        public const float JumpHeight = 10f * 16f;

        /// <summary>By request ("havent return for 5 sec"): ticks on its way back, not back, before it is brought back.</summary>
        public const int Patience = 5 * 60;

        /// <summary>Mine: back with its owner within this of them, middle to middle, with nothing solid between them.</summary>
        public const float Back = 10f * 16f;

        /// <summary>Ticks it has been on its way back to its owner and not back (counted on every machine; acted on by its owner's).</summary>
        private int away;

        /// <summary>How much faster up its jumps are made once off the ground (Bone Lee's tenth: AfterJump); 1 for none.</summary>
        protected virtual float UpShare => 1f;

        /// <summary>
        /// Its highest jump, for a wall's top or what it goes for this high over its feet (px): fast
        /// enough that its feet rise to half a block under it, against Pull, with its kind's share up
        /// (UpShare), and it steps up the last half block as it steps up a block; never higher than
        /// for JumpHeight, never lower than the game's 8. In a liquid (as it was found when it last
        /// moved) the game's 8, against the liquid's own gravity: Pull is not what holds it down there.
        /// </summary>
        protected override float HighJump(float height)
        {
            float game = base.HighJump(height);
            if (wet)
                return game;

            float rise = Math.Max(Math.Min(height, JumpHeight) - 8f, 0f);
            return Math.Max(game, Launch(rise, Pull) / UpShare);
        }

        /// <summary>
        /// How fast it has to leave the ground to rise this far: its gravity is added before each move
        /// (NPC.UpdateNPC), so leaving at v it rises v * v / 2g - v / 2, and up to an eighth of g more.
        /// </summary>
        private static float Launch(float height, float gravity) => gravity / 2f + (float)Math.Sqrt(gravity * gravity / 4f + 2f * gravity * height);

        protected override void Fetch(Player owner)
        {
            base.Fetch(owner);
            away = 0;
        }

        protected override void Think(Player owner)
        {
            base.Think(owner);
            Recall(owner);
        }

        /// <summary>By request: with nothing to fight and not back for Patience ticks together, it is brought back to its owner (on its owner's machine; Fetch tells the others).</summary>
        private void Recall(Player owner)
        {
            if (Fighting || IsBack(owner))
            {
                away = 0;
                return;
            }

            away++;
            if (away >= Patience && Projectile.owner == Main.myPlayer)
                Fetch(owner);
        }

        /// <summary>Back with its owner (mine): within Back of them, middle to middle, with nothing solid between them (Collision.CanHit).</summary>
        private bool IsBack(Player owner) =>
            Vector2.Distance(Projectile.Center, owner.Center) <= Back
            && Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, owner.position, owner.width, owner.height);
    }

    /// <summary>
    /// Bone Lee (NPC 287): the fastest of them, 5 a tick at most, 0.2 faster each tick; on the
    /// ground at what it fights, close and running at it, it leaps at it (8 across, 4 up), and every
    /// jump of its is a fifth faster across and a tenth faster up; its highest, that tenth with it,
    /// gets it onto 10 blocks (BoneSquadMelee). It leaps at enemies only, not at its place behind its
    /// owner (mine). Its frames as the game's: 0 standing, 1 in the air, and the rest of its sheet,
    /// from 2, its run.
    /// </summary>
    public class BoneSquadBoneLee : BoneSquadMelee
    {
        /// <summary>AI_003_Fighters for Bone Lee: every jump of its, once off the ground, this much faster across and up.</summary>
        private const float Across = 1.2f, Up = 1.1f;

        public override int Kind => NPCID.BoneLee;

        public override string Texture => "Terraria/Images/NPC_" + NPCID.BoneLee;

        protected override float UpShare => Up;

        protected override void Walk() => Accelerate(5f, 0.2f, 0.7f);

        protected override void AfterJump(Aim aim)
        {
            if (Fighting && Projectile.velocity.Y == 0f
                && Math.Abs(Projectile.position.X + (float)(Projectile.width / 2) - (aim.Position.X + (float)(aim.Width / 2))) < 150f
                && Math.Abs(Projectile.position.Y + (float)(Projectile.height / 2) - (aim.Position.Y + (float)(aim.Height / 2))) < 50f
                && ((Projectile.direction > 0 && Projectile.velocity.X >= 1f) || (Projectile.direction < 0 && Projectile.velocity.X <= -1f)))
            {
                Projectile.velocity.X = 8 * Projectile.direction;
                Projectile.velocity.Y = -4f;
                Projectile.netUpdate = true;
            }

            if (Projectile.velocity.Y < 0f)
            {
                Projectile.velocity.X *= Across;
                Projectile.velocity.Y *= Up;
            }
        }

        /// <summary>NPC.FindFrame for Bone Lee (the town NPCs' frames, as the game gives it): standing, its run from frame 2, frame 1 in the air.</summary>
        protected override void Frame()
        {
            if (Projectile.velocity.Y == 0f)
            {
                if (Projectile.direction == 1)
                    Projectile.spriteDirection = 1;
                if (Projectile.direction == -1)
                    Projectile.spriteDirection = -1;

                if (Projectile.velocity.X == 0f)
                {
                    frame = 0;
                    frameCounter = 0.0;
                    return;
                }

                frameCounter += Math.Abs(Projectile.velocity.X) * 2f;
                frameCounter++;

                if (frame < 2)
                    frame = 2;

                if (frameCounter > 6.0)
                {
                    frame++;
                    frameCounter = 0.0;
                }

                if (frame >= Main.npcFrameCount[Kind])
                    frame = 2;
            }
            else
            {
                frameCounter = 0.0;
                frame = 1;
            }
        }
    }

    /// <summary>
    /// One of the twelve Armored Bones (NPC 269 to 280, ai[0]), the Rusty, the Blue and the Hell
    /// ones: two of them in a squad, never the same one twice. Each walks at its own top speed
    /// (AI_003_Fighters'), 0.65 faster (by request: "speed by +0.65"), 0.07 faster each tick; its
    /// highest jump gets it onto 10 blocks (BoneSquadMelee); the Hell ones give light, and are drawn
    /// brighter, as the game's are. 0.75 minion slots each.
    /// </summary>
    public class BoneSquadArmored : BoneSquadMelee
    {
        /// <summary>By request ("speed by +0.6f5", read with the user as 0.65): this much faster at most than the game's.</summary>
        public const float Faster = 0.65f;

        /// <summary>AI_003_Fighters' top speed for each, from 269 to 280.</summary>
        private static readonly float[] TopSpeeds = { 2f, 1f, 1.5f, 3f, 1.25f, 3f, 3.25f, 2f, 2.75f, 1.8f, 1.3f, 2.5f };

        public override int Kind => Utils.Clamp((int)Projectile.ai[0], NPCID.RustyArmoredBonesAxe, NPCID.HellArmoredBonesSword);

        public override float Slots => 0.75f;

        /// <summary>The picture asked for when nothing draws it: the first of them (PreDraw draws its own kind).</summary>
        public override string Texture => "Terraria/Images/NPC_" + NPCID.RustyArmoredBonesAxe;

        private bool Hell => Kind >= NPCID.HellArmoredBones && Kind <= NPCID.HellArmoredBonesSword;

        protected override float AddHeight
        {
            get
            {
                switch (Kind)
                {
                    case NPCID.RustyArmoredBonesAxe:
                    case NPCID.RustyArmoredBonesFlail:
                    case NPCID.RustyArmoredBonesSword:
                    case NPCID.BlueArmoredBonesMace:
                    case NPCID.HellArmoredBones:
                        return -2f;
                    default:
                        return 0f;
                }
            }
        }

        protected override void Walk() => Accelerate(TopSpeeds[Kind - NPCID.RustyArmoredBonesAxe] + Faster, 0.07f, 0.8f);

        protected override void Glow()
        {
            if (Hell)
                Lighting.AddLight((int)Projectile.Center.X / 16, (int)Projectile.Center.Y / 16, 0.2f, 0.1f, 0f);
        }

        /// <summary>Main.DrawNPCDirect: the Hell Armored Bones in at least full red and some green, whatever the light.</summary>
        protected override Color Lit(Color light)
        {
            if (Hell)
            {
                if (light.R < 255)
                    light.R = 255;
                if (light.G < 175)
                    light.G = 175;
            }

            return light;
        }

        /// <summary>NPC.FindFrame for the Armored Bones: standing, the rest of the sheet from frame 2 its walk, frame 0 in the air.</summary>
        protected override void Frame()
        {
            if (Projectile.velocity.Y == 0f)
            {
                if (Projectile.direction == 1)
                    Projectile.spriteDirection = 1;
                if (Projectile.direction == -1)
                    Projectile.spriteDirection = -1;

                if (Projectile.velocity.X == 0f)
                {
                    frame = 0;
                    frameCounter = 0.0;
                    return;
                }

                if (frame < 2)
                    frame = 2;

                frameCounter += Math.Abs(Projectile.velocity.X) * 2f;
                frameCounter++;

                if (frameCounter > 6.0)
                {
                    frame++;
                    frameCounter = 0.0;
                }

                if (frame >= Main.npcFrameCount[Kind])
                    frame = 2;
            }
            else
            {
                frameCounter = 0.0;
                frame = 0;
            }
        }
    }

    /// <summary>
    /// The Skeleton Archer (NPC 110): on the ground with an enemy in sight and in range, it stops,
    /// draws (70 ticks) and lets a flaming arrow go halfway through, aimed a little over the enemy
    /// by how far across it is, and a little off; drawn, it is not bored and does not turn round.
    /// Otherwise it walks, 1 a tick at most. Its arrows by request a half again as fast ("all boost
    /// shoot speed"), 16.5, aimed over the enemy by four ninths of the game's, since they drop that
    /// much less on the way (mine). Only at enemies (the game's archer cannot see a player who is
    /// stealthed and still; enemies are never stealthed).
    /// </summary>
    public class BoneSquadArcher : BoneSquadWalker
    {
        /// <summary>AI_003_Fighters' for the archer: 70 ticks drawn, the arrow let go at 35.</summary>
        private const int Draw = 70, Loose = Draw / 2;

        /// <summary>The game's arrow speed, 11, a half again as fast (by request).</summary>
        public const float ArrowSpeed = 11f * 1.5f;

        /// <summary>
        /// How far over the enemy it aims, of the game's tenth of how far across: four ninths of it,
        /// since an arrow a half again as fast is in the air two thirds as long and so drops four
        /// ninths as far (mine).
        /// </summary>
        public const float Lob = 0.1f * 4f / 9f;

        /// <summary>Its arrow's damage, the game's (normal mode).</summary>
        public const int ArrowDamage = 35;

        /// <summary>As far as it shoots, from its middle to where it aims (AI_003_Fighters').</summary>
        private const float Reach = 700f;

        /// <summary>AI_003's ai[1]: ticks drawn still to go.</summary>
        private float drawn;

        /// <summary>AI_003's ai[2]: drawn, and which way: 1 down, 2 down and across, 3 across, 4 up and across, 5 up; 0 not drawn.</summary>
        private float pose;

        public override int Kind => NPCID.SkeletonArcher;

        public override string Texture => "Terraria/Images/NPC_" + NPCID.SkeletonArcher;

        protected override bool Busy => pose > 0f;

        protected override void Fetch(Player owner)
        {
            base.Fetch(owner);
            drawn = pose = 0f;
        }

        protected override void SendState(BinaryWriter writer)
        {
            base.SendState(writer);
            writer.Write(drawn);
            writer.Write(pose);
        }

        protected override void ReceiveState(BinaryReader reader)
        {
            base.ReceiveState(reader);
            drawn = reader.ReadSingle();
            pose = reader.ReadSingle();
        }

        /// <summary>Which way it draws, by where it aims: AI_003_Fighters' five.</summary>
        private static float Pose(float x, float y)
        {
            if (Math.Abs(y) > Math.Abs(x) * 2f)
                return y > 0f ? 1f : 5f;
            if (Math.Abs(x) > Math.Abs(y) * 2f)
                return 3f;
            return y > 0f ? 2f : 4f;
        }

        /// <summary>
        /// From its middle to just over the enemy's middle, a little off (only its owner's machine
        /// picks how much: the game's server does for its archer).
        /// </summary>
        private Vector2 AimAt(NPC npc, out Vector2 from)
        {
            from = new Vector2(Projectile.position.X + (float)Projectile.width * 0.5f, Projectile.position.Y + (float)Projectile.height * 0.5f);
            float x = npc.position.X + (float)npc.width * 0.5f - from.X;
            float over = Math.Abs(x) * Lob;
            float y = npc.position.Y + (float)npc.height * 0.5f - from.Y - over;
            x += Main.rand.Next(-40, 41);
            y += Main.rand.Next(-40, 41);
            return new Vector2(x, y);
        }

        /// <summary>AI_003_Fighters' archer: drawing, letting go, deciding to draw, and walking when it is not drawn.</summary>
        protected override void Act(Player owner, ref Aim aim, bool rest)
        {
            if (drawn > 0f)
                drawn--;

            if (pose > 0f)
            {
                // NPC.TargetClosest: what it goes for, faced.
                int had = target;
                target = Choose(owner);
                aim = Aiming(owner);
                Face(aim, had);

                if (drawn == Loose && Fighting && Projectile.owner == Main.myPlayer)
                {
                    Vector2 shot = AimAt(Main.npc[target], out Vector2 from);
                    shot *= ArrowSpeed / shot.Length();
                    Projectile.netUpdate = true;

                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), from + shot, shot, ModContent.ProjectileType<BoneSquadArrow>(), ShotDamage(ArrowDamage), 0f, Projectile.owner);
                    pose = Pose(shot.X, shot.Y);
                }

                if (Projectile.velocity.Y != 0f || drawn <= 0f)
                {
                    pose = 0f;
                    drawn = 0f;
                }
                else
                {
                    Projectile.velocity.X *= 0.9f;
                    Projectile.spriteDirection = Projectile.direction;
                }
            }

            if (pose <= 0f && Projectile.velocity.Y == 0f && drawn <= 0f && Fighting && Projectile.owner == Main.myPlayer)
            {
                NPC npc = Main.npc[target];
                if (Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, npc.position, npc.width, npc.height))
                {
                    Vector2 shot = AimAt(npc, out _);
                    if (shot.Length() < Reach)
                    {
                        Projectile.netUpdate = true;
                        Projectile.velocity.X *= 0.5f;
                        drawn = Draw;
                        pose = Pose(shot.X, shot.Y);
                    }
                }
            }

            if (pose <= 0f && !rest)
                Accelerate(1f, 0.07f, 0.8f);
        }

        /// <summary>NPC.FindFrame for the archer (1.4.4's): drawn, the frame of the way it aims; walking, frames 6 on; frame 0 in the air.</summary>
        protected override void Frame()
        {
            if (Projectile.velocity.Y == 0f)
            {
                if (Projectile.direction == 1)
                    Projectile.spriteDirection = 1;
                if (Projectile.direction == -1)
                    Projectile.spriteDirection = -1;

                if (pose > 0f)
                {
                    Projectile.spriteDirection = Projectile.direction;
                    frame = (int)pose;
                    frameCounter = 0.0;
                    return;
                }

                if (frame < 6)
                    frame = 6;

                frameCounter += Math.Abs(Projectile.velocity.X) * 2f;
                frameCounter += Projectile.velocity.X;

                if (frameCounter > 6.0)
                {
                    frame++;
                    frameCounter = 0.0;
                }

                if (frame >= Main.npcFrameCount[Kind])
                    frame = 6;
            }
            else
            {
                frameCounter = 0.0;
                frame = 0;
            }
        }
    }
}
