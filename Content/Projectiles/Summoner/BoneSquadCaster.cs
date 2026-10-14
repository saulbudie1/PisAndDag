using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// The squad's caster (ai[0]): a Ragged Caster, a Necromancer or a Diabolist, either of each
    /// (NPC 281 to 286). Its AI is the game's for them (aiStyle 8, as it is in 1.4.4): it stands,
    /// faces what it goes for, now and then raises its hands and casts (the Ragged Caster a lost
    /// soul, the Necromancer a shadow beam, the Diabolist an inferno bolt that bursts where the enemy
    /// stood), and every so often goes away in a puff and comes back near what it goes for. Its
    /// clock runs twice as fast (by request, "boost shoot frequency": twice as often), so it casts
    /// and moves twice as often; its spells a half again as fast ("all boost shoot speed").
    ///
    /// It casts only at an enemy, and raises its hands only to (mine: with none it has nothing to
    /// cast at). With no enemy it goes to its owner, appearing near them as it would near an
    /// enemy. Where it may appear is where the game lets these appear, but not only in the Dungeon
    /// (mine: they go where their owner goes); and not where an enemy is or is about to be, as the
    /// game's will not appear on a player (AI_AttemptToFindTeleportSpot).
    /// </summary>
    public class BoneSquadCaster : BoneSquadBody
    {
        /// <summary>How fast its clock runs: twice the game's (by request).</summary>
        private const int Steps = 2;

        /// <summary>Its spells a half again as fast as the game's (by request).</summary>
        public const float Faster = 1.5f;

        /// <summary>The game's caster's ai[0]: its clock. Its casts and its going away are at times on it.</summary>
        private float clock;

        /// <summary>The game's caster's ai[1]: ticks its hands are still up; the spell goes at 25.</summary>
        private float casting;

        /// <summary>The game's caster's ai[2] and ai[3]: the tile it is about to appear on (0 for none).</summary>
        private float toX, toY;

        private int frame;

        public override int Kind => Utils.Clamp((int)Projectile.ai[0], NPCID.RaggedCaster, NPCID.DiabolistWhite);

        /// <summary>The picture asked for when nothing draws it: the first of them (PreDraw draws its own kind).</summary>
        public override string Texture => "Terraria/Images/NPC_" + NPCID.RaggedCaster;

        private bool Ragged => Kind == NPCID.RaggedCaster || Kind == NPCID.RaggedCasterOpenCoat;

        private bool Necromancer => Kind == NPCID.Necromancer || Kind == NPCID.NecromancerArmored;

        private bool Diabolist => Kind == NPCID.DiabolistRed || Kind == NPCID.DiabolistWhite;

        /// <summary>The game's dust for each of them: the Ragged Caster's spirit, the Necromancer's shadow, the Diabolist's fire.</summary>
        private int DustType => Ragged ? DustID.SpectreStaff : Necromancer ? DustID.ShadowbeamStaff : DustID.InfernoFork;

        protected override bool FallThrough => false;

        protected override void Fetch(Player owner)
        {
            base.Fetch(owner);
            toX = toY = 0f;
        }

        protected override void SendState(BinaryWriter writer)
        {
            base.SendState(writer);
            writer.Write(clock);
            writer.Write(casting);
            writer.Write(toX);
            writer.Write(toY);
        }

        protected override void ReceiveState(BinaryReader reader)
        {
            base.ReceiveState(reader);
            clock = reader.ReadSingle();
            casting = reader.ReadSingle();
            toX = reader.ReadSingle();
            toY = reader.ReadSingle();
        }

        /// <summary>What it goes for: the enemy, or its owner.</summary>
        private Rectangle Aim(Player owner) => target >= 0 ? Main.npc[target].Hitbox : owner.Hitbox;

        /// <summary>The game's aiStyle 8 for the casters, step by step.</summary>
        protected override void Think(Player owner)
        {
            // NPC.TargetClosest: what it goes for, faced (its directionY is of no use to a caster).
            Forget();
            int had = target;
            target = Choose(owner);
            Rectangle aim = Aim(owner);
            int facing = Projectile.direction;
            Projectile.direction = (float)(aim.X + aim.Width / 2) < Projectile.position.X + (float)(Projectile.width / 2) ? -1 : 1;
            if ((Projectile.direction != facing || target != had) && !collideX && !collideY)
                Projectile.netUpdate = true;

            Projectile.velocity.X *= 0.93f;
            if ((double)Projectile.velocity.X > -0.1 && (double)Projectile.velocity.X < 0.1)
                Projectile.velocity.X = 0f;

            if (clock == 0f)
                clock = 500f;

            if (toX != 0f && toY != 0f)
                Appear();

            for (int step = 0; step < Steps; step++)
            {
                clock++;
                Schedule();

                if (clock >= 650f && Projectile.owner == Main.myPlayer)
                {
                    clock = 1f;
                    Vector2 middle = target >= 0 ? Main.npc[target].Center : owner.Center;
                    Point at = new Point((int)middle.X / 16, (int)middle.Y / 16);
                    if (FindSpot(at, out Point spot))
                    {
                        casting = 20f;
                        toX = spot.X;
                        toY = spot.Y;
                    }

                    Projectile.netUpdate = true;
                }
            }

            if (casting > 0f)
            {
                casting--;
                if (casting == 25f && Fighting && Projectile.owner == Main.myPlayer)
                    Cast(Main.npc[target]);
            }

            Ambient();
        }

        /// <summary>When it raises its hands, by its clock (the game's times for each), and when its clock runs out early.</summary>
        private void Schedule()
        {
            bool cast;
            if (Ragged)
            {
                cast = clock == 100f || clock == 120f || clock == 140f || clock == 200f || clock == 220f || clock == 240f || clock == 300f || clock == 320f || clock == 340f;
            }
            else if (Necromancer)
            {
                cast = clock == 100f || clock == 150f || clock == 200f || clock == 250f || clock == 300f;
            }
            else
            {
                cast = clock == 100f || clock == 200f || clock == 300f;
            }

            if (cast && Fighting)
            {
                casting = 30f;
                Projectile.netUpdate = true;
            }

            if (Ragged && clock >= 540f)
                clock = 700f;
            if (Necromancer && clock >= 450f)
                clock = 700f;
            if (Diabolist && clock > 400f)
                clock = 650f;
        }

        /// <summary>Gone where it was going: a puff where it was, its sound, and a puff where it is (the game's for each).</summary>
        private void Appear()
        {
            SoundEngine.PlaySound(SoundID.Item8, Projectile.position);
            Puff();

            Projectile.position.X = toX * 16f - (float)(Projectile.width / 2) + 8f;
            Projectile.position.Y = toY * 16f - (float)Projectile.height;
            Projectile.velocity = Vector2.Zero;
            before = Projectile.position;
            toX = toY = 0f;

            SoundEngine.PlaySound(SoundID.Item8, Projectile.position);
            Puff();
        }

        private void Puff()
        {
            for (int i = 0; i < 50; i++)
            {
                if (Necromancer)
                {
                    Dust dust = BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustType);
                    dust.velocity *= 2f;
                    dust.scale = 1.4f;
                }
                else
                {
                    Dust dust = BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustType, 0f, 0f, 100, default, 1.5f);
                    dust.velocity *= 3f;
                    dust.noGravity = true;
                }
            }
        }

        /// <summary>
        /// Its spell at the enemy, from the top of its head: the game's for each, at its speed a half
        /// again (the Necromancer's a little off, and where the enemy was ten ticks of its speed ago,
        /// as the game's aims; the Diabolist's told where the enemy stood), lasting 300 ticks as the
        /// game makes them.
        /// </summary>
        private void Cast(NPC enemy)
        {
            float speed = (Ragged ? 4f : Diabolist ? 8f : 6f) * Faster;
            Vector2 from = new Vector2(Projectile.position.X + (float)Projectile.width * 0.5f, Projectile.position.Y);
            float x = enemy.position.X + (float)enemy.width * 0.5f - from.X;
            float y = enemy.position.Y + (float)enemy.height * 0.5f - from.Y;

            if (Necromancer)
            {
                x += Main.rand.Next(-30, 31);
                y += Main.rand.Next(-30, 31);
                x -= enemy.velocity.X * 10f;
                y -= enemy.velocity.Y * 10f;
            }

            float length = speed / (float)Math.Sqrt(x * x + y * y);
            Vector2 velocity = new Vector2(x * length, y * length);

            if (Ragged)
            {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, velocity, ModContent.ProjectileType<BoneSquadLostSoul>(), ShotDamage(40), 0f, Projectile.owner);
            }
            else if (Diabolist)
            {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, velocity, ModContent.ProjectileType<BoneSquadInferno>(), ShotDamage(40), 0f, Projectile.owner, enemy.Center.X, enemy.Center.Y);
            }
            else
            {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, velocity, ModContent.ProjectileType<BoneSquadShadowBeam>(), ShotDamage(30), 0f, Projectile.owner);
            }
        }

        /// <summary>Its dust, one tick in two (the game's for each).</summary>
        private void Ambient()
        {
            if (Main.rand.Next(2) != 0)
                return;

            Vector2 at = new Vector2(Projectile.position.X, Projectile.position.Y + 2f);
            if (Necromancer)
            {
                Dust dust = BoneSquadDust.New(at, Projectile.width, Projectile.height, DustType);
                dust.velocity.X *= 0.5f;
                dust.velocity.Y *= 0.5f;
            }
            else if (Diabolist)
            {
                Dust dust = BoneSquadDust.New(at, Projectile.width, Projectile.height, DustType, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100);
                dust.noGravity = true;
                dust.velocity *= 0.4f;
                dust.velocity.Y -= 0.7f;
            }
            else
            {
                Dust dust = BoneSquadDust.New(at, Projectile.width, Projectile.height, DustType, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100, default, 0.1f);
                dust.noGravity = true;
                dust.velocity *= 0.5f;
                dust.fadeIn = 1.2f;
            }
        }

        /// <summary>
        /// NPC.AI_AttemptToFindTeleportSpot as the game calls it for these (20 tiles round, kept 5
        /// from an enemy, a tile's room round, not in the air): a hundred tries at a tile near what
        /// it goes for, each looking down from a random one for the ground, with room over it, not
        /// under lava, not its own tile or next to it. Not if it is more than 2000 from there. The
        /// game's casters also want the Dungeon's wall behind them; these do not (mine).
        /// </summary>
        private bool FindSpot(Point at, out Point spot)
        {
            const int range = 20, apart = 5, room = 1;

            spot = Point.Zero;
            int ownX = (int)Projectile.Center.X / 16;
            int ownY = (int)Projectile.Center.Y / 16;

            if (Math.Abs(ownX * 16 - at.X * 16) + Math.Abs(ownY * 16 - at.Y * 16) > 2000)
                return false;

            for (int tries = 0; tries < 100; tries++)
            {
                int x = Main.rand.Next(at.X - range, at.X + range + 1);
                for (int y = Main.rand.Next(at.Y - range, at.Y + range + 1); y < at.Y + range; y++)
                {
                    // Mine: the game's never come near the world's edge.
                    if (!WorldGen.InWorld(x, y, 4))
                        continue;

                    if (y >= ownY - 1 && y <= ownY + 1 && x >= ownX - 1 && x <= ownX + 1)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasUnactuatedTile || Main.tile[x, y - 1].LiquidType == LiquidID.Lava || !Main.tileSolid[tile.TileType])
                        continue;

                    if (Collision.SolidTiles(x - room, x + room, y - 3 - room, y - 1))
                        continue;

                    // Not on an enemy, nor where one is going (the game's 20 ticks ahead).
                    Rectangle area = new Rectangle(x * 16, y * 16, 16, 16);
                    area.Inflate(apart * 16, apart * 16);

                    bool clear = true;
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (!npc.CanBeChasedBy(Projectile))
                            continue;

                        Rectangle box = npc.Hitbox;
                        Rectangle going = Utils.Modified(box, (int)(npc.velocity.X * 20f), (int)(npc.velocity.Y * 20f), 0, 0);
                        Rectangle.Union(ref going, ref box, out going);
                        if (going.Intersects(area))
                        {
                            clear = false;
                            break;
                        }
                    }

                    if (clear)
                    {
                        spot = new Point(x, y);
                        return true;
                    }

                    break;
                }
            }

            return false;
        }

        /// <summary>NPC.FindFrame for the casters: standing, its hands up while it casts, in the air.</summary>
        protected override void Frame()
        {
            if (Projectile.velocity.Y == 0f)
            {
                if (Projectile.direction == 1)
                    Projectile.spriteDirection = 1;
                if (Projectile.direction == -1)
                    Projectile.spriteDirection = -1;
            }

            frame = 0;
            if (Projectile.velocity.Y != 0f)
                frame = 2;
            else if (casting > 0f)
                frame = 1;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawNpc(Kind, frame, 0f, lightColor, 0f);
            return false;
        }
    }
}
