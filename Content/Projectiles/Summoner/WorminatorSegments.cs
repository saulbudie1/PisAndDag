using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// The worm of the Worminator (the item is Content/Items/Summoner/Worminator.cs), by request: "Summons a worm,
    /// base 3 body segment.(+1 head 1 tail) each additional minion adds 1 segment", "Same deal with the stardust
    /// dragon but this one only in the ground, so AI like giant worm, 2x speed", "8 damage all around".
    ///
    /// Put together as the game's Stardust Dragon is: one projectile for each segment, a head and a tail that take
    /// no minion slot and are never let go to make room for another minion, and between them the segments that do
    /// (WorminatorBody, one slot each). The first summon makes the head, three segments and the tail; the first two of
    /// the three take no slot (WorminatorNeck), so that the whole worm takes the one slot a summon takes. Each summon
    /// after the first puts one more WorminatorBody in, before the tail. Every segment hurts what it touches for the
    /// item's damage, 8, each with its own cooldown on each enemy, as the dragon's segments do.
    ///
    /// Each segment has its place along the worm, a key, in ai[0]: the head 0, the two necks 1 and 2, the bodies 3
    /// and up, the tail TailKey. Each follows the one with the next lower key, so that a body let go from the middle
    /// closes up the worm behind it (the dragon keeps the same order by linking each segment to the one ahead).
    ///
    /// The head moves as the game's Giant Worm's head moves (NPC.AI_006_Worms, as it is in 1.4.4, for type 10), and
    /// the rest as its body follows it: in the ground it steers for what it goes after, out of it it falls, and
    /// farther than its owner's screen from them it moves as if in the ground, as a Giant Worm far from every
    /// player does. "2x speed": the head's move is run twice a tick while it has an enemy to go for, so that it goes
    /// the same way as the Giant Worm at twice its pace; with none, once (mine), as it circles under its owner.
    /// The whole worm is drawn by the head, behind the blocks, as the Giant Worm is drawn: in the ground it is seen
    /// only by the dirt it throws up, or with a Hunter Potion.
    ///
    /// The pictures are the Giant Worm's own head, body and tail, copied into the mod's folder for the user to
    /// change (PlaceholderTextures.md).
    /// </summary>
    public abstract class WorminatorSegment : ModProjectile
    {
        /// <summary>The Giant Worm's size, every segment of it (NPC.SetDefaults, types 10, 11 and 12).</summary>
        public const int Size = 14;

        /// <summary>How far each segment keeps behind the one ahead of it: its own width, as the Giant Worm's do (AI_006_Worms).</summary>
        public const float Spacing = Size;

        /// <summary>The keys (ai[0]): the head's, the first body's (the two necks have 1 and 2), and the tail's, above any body's.</summary>
        public const int HeadKey = 0, FirstBodyKey = 3, TailKey = 1000000;

        /// <summary>Mine: ticks before a segment hurts the same enemy again (the Stardust Dragon's segments: 7).</summary>
        public const int Cooldown = 12;

        /// <summary>Its place along the worm (ai[0]).</summary>
        public int Key => (int)Projectile.ai[0];

        /// <summary>Minion slots it takes: a body one, the rest none.</summary>
        protected virtual float Slots => 0f;

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;

            // As the Stardust Dragon's: the segments that hold the slot can be let go for another minion, the rest never.
            if (Slots > 0f)
                ProjectileID.Sets.MinionSacrificable[Type] = true;
            else
                ProjectileID.Sets.MinionCannotBeFreed[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = Size;
            Projectile.height = Size;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = Slots;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;

            // The head moves itself and every segment behind it, through anything.
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            // None of it is drawn by the game: the head draws the worm (WorminatorHead.PreDraw).
            Projectile.hide = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Cooldown;
        }

        public override bool? CanCutTiles() => false;

        public override bool MinionContactDamage() => true;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>Mine: an enemy is knocked away from the segment that hit it.</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.Center.X < Projectile.Center.X ? -1 : 1;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int buff = ModContent.BuffType<WorminatorBuff>();

            if (!owner.active || owner.dead)
            {
                owner.ClearBuff(buff);
                return;
            }

            if (owner.HasBuff(buff))
                Projectile.timeLeft = 2;

            Behave(owner);
        }

        /// <summary>A segment behind the head: it is moved by the head. Without a head it goes (its owner's machine decides it, and tells the rest).</summary>
        protected virtual void Behave(Player owner)
        {
            if (Projectile.owner == Main.myPlayer && FindHead(Projectile.owner) < 0)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) => false;

        // ---- the worm, as a whole --------------------------------------------------------------

        private static readonly List<(int key, int identity, int index)> sorting = new List<(int, int, int)>();

        /// <summary>The owner's segments, head first, in the order of their keys (the same on every machine).</summary>
        public static void Chain(int owner, List<int> into)
        {
            into.Clear();
            sorting.Clear();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.ModProjectile is WorminatorSegment)
                    sorting.Add(((int)p.ai[0], p.identity, i));
            }

            sorting.Sort((a, b) => a.key != b.key ? a.key.CompareTo(b.key) : a.identity.CompareTo(b.identity));

            foreach (var s in sorting)
                into.Add(s.index);
        }

        /// <summary>The owner's head (the first, if somehow there are two), or -1.</summary>
        public static int FindHead(int owner)
        {
            int head = -1;
            int type = ModContent.ProjectileType<WorminatorHead>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == type && (head < 0 || p.identity < Main.projectile[head].identity))
                    head = i;
            }

            return head;
        }

        /// <summary>
        /// What the Giant Worm counts as ground (AI_006_Worms): a block, not actuated, that is solid, or solid on top
        /// in the first row of its picture (a platform of the first kind); or more than a quarter of a tile of liquid.
        /// </summary>
        public static bool Ground(int i, int j)
        {
            if (!WorldGen.InWorld(i, j))
                return false;

            Tile tile = Main.tile[i, j];
            return (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || (Main.tileSolidTop[tile.TileType] && tile.TileFrameY == 0))) || tile.LiquidAmount > 64;
        }

        /// <summary>
        /// Whether a segment is in the ground, as the Giant Worm finds it (AI_006_Worms): ground on any tile its box
        /// overlaps. With dig, it throws up the dirt the Giant Worm does as it goes: on each solid block it overlaps,
        /// one time in 100, the dust of a blow that does not break it (WorldGen.KillTile, failing, effect only).
        /// </summary>
        public static bool InGround(Projectile p, bool dig)
        {
            int left = Math.Max((int)(p.position.X / 16f) - 1, 0);
            int right = Math.Min((int)((p.position.X + p.width) / 16f) + 2, Main.maxTilesX);
            int top = Math.Max((int)(p.position.Y / 16f) - 1, 0);
            int bottom = Math.Min((int)((p.position.Y + p.height) / 16f) + 2, Main.maxTilesY);

            bool found = false;

            for (int i = left; i < right; i++)
            {
                for (int j = top; j < bottom; j++)
                {
                    if (!Ground(i, j))
                        continue;

                    if (p.position.X + p.width > i * 16f && p.position.X < i * 16f + 16f && p.position.Y + p.height > j * 16f && p.position.Y < j * 16f + 16f)
                    {
                        found = true;

                        if (dig && !Main.dedServ && Main.rand.NextBool(100))
                        {
                            Tile tile = Main.tile[i, j];
                            if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType])
                                WorldGen.KillTile(i, j, true, true, false);
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>Mine: the dirt thrown up where the worm breaks out of the ground or into it: the blows' dust of up to four of the blocks round a point.</summary>
        public static void Burst(Vector2 at)
        {
            if (Main.dedServ)
                return;

            int x = (int)(at.X / 16f), y = (int)(at.Y / 16f), made = 0;

            for (int n = 0; n < 12 && made < 4; n++)
            {
                int i = x + Main.rand.Next(-1, 2), j = y + Main.rand.Next(-1, 2);
                if (!WorldGen.InWorld(i, j))
                    continue;

                Tile tile = Main.tile[i, j];
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType])
                {
                    WorldGen.KillTile(i, j, true, true, false);
                    made++;
                }
            }
        }

        /// <summary>
        /// Where a worm comes up: two blocks under the top of the ground below a point (the first ground within 40
        /// blocks down), or at the point if it is in the ground already. Also where the top of that ground is.
        /// </summary>
        public static bool GroundBelow(Vector2 from, out Vector2 spot, out Vector2 surface)
        {
            int x = (int)(from.X / 16f), y = (int)(from.Y / 16f);

            for (int k = 0; k <= 40; k++)
            {
                if (!WorldGen.InWorld(x, y + k, 10))
                    break;

                if (Ground(x, y + k))
                {
                    surface = new Vector2(x * 16f + 8f, (y + k) * 16f);
                    spot = k == 0 ? from : new Vector2(x * 16f + 8f, (y + k + 2) * 16f + 8f);
                    return true;
                }
            }

            spot = surface = from;
            return false;
        }

        /// <summary>
        /// The item's summon: a new worm, or one more segment for the one the player has. A new worm comes up out of
        /// the ground under the cursor, or under the player if there is none under the cursor (mine), going up.
        /// </summary>
        public static void Summon(Player player, IEntitySource source, int damage, float knockback, int baseDamage)
        {
            int head = FindHead(player.whoAmI);
            int body = ModContent.ProjectileType<WorminatorBody>();

            if (head >= 0)
            {
                List<int> worm = new List<int>();
                Chain(player.whoAmI, worm);

                int last = FirstBodyKey - 1;
                int tail = -1;
                foreach (int i in worm)
                {
                    Projectile p = Main.projectile[i];
                    if (p.ModProjectile is WorminatorTail)
                        tail = i;
                    else if (p.ModProjectile is WorminatorNeck || p.ModProjectile is WorminatorBody)
                        last = Math.Max(last, (int)p.ai[0]);
                }

                // In before the tail, where the tail is now: the tail is pushed back behind it as the worm moves.
                Vector2 at = Main.projectile[tail >= 0 ? tail : head].Center;
                Projectile added = Projectile.NewProjectileDirect(source, at, Vector2.Zero, body, damage, knockback, player.whoAmI, last + 1);
                added.originalDamage = baseDamage;
                added.rotation = Main.projectile[tail >= 0 ? tail : head].rotation;
                Burst(at);
                return;
            }

            if (!GroundBelow(Main.MouseWorld, out Vector2 spot, out Vector2 surface) && !GroundBelow(player.Bottom, out spot, out surface))
                spot = surface = Main.MouseWorld;

            int[] types =
            {
                ModContent.ProjectileType<WorminatorHead>(), ModContent.ProjectileType<WorminatorNeck>(), ModContent.ProjectileType<WorminatorNeck>(),
                body, ModContent.ProjectileType<WorminatorTail>(),
            };
            int[] keys = { HeadKey, 1, 2, FirstBodyKey, TailKey };

            // Laid out straight down from the head, the head going up: it breaks out under the cursor.
            for (int k = 0; k < types.Length; k++)
            {
                Vector2 velocity = k == 0 ? new Vector2(0f, -WorminatorHead.Speed) : Vector2.Zero;
                Projectile p = Projectile.NewProjectileDirect(source, spot + new Vector2(0f, k * Spacing), velocity, types[k], damage, knockback, player.whoAmI, keys[k]);
                p.originalDamage = baseDamage;
            }

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.WormDig, surface);
                Burst(surface + new Vector2(0f, 8f));
            }
        }
    }

    /// <summary>
    /// The head: it moves the worm (Behave) and draws it (PreDraw). It goes when the worm has no WorminatorBody
    /// left, for a worm without one would take no slot.
    /// </summary>
    public class WorminatorHead : WorminatorSegment
    {
        /// <summary>The Giant Worm's (AI_006_Worms, type 10): its top speed, and how much it changes its speed by in a move.</summary>
        public const float Speed = 6f, Accel = 0.05f;

        /// <summary>The Giant Worm's: how much faster it falls each move out of the ground (its top speed is its fall's too).</summary>
        public const float Gravity = 0.11f;

        /// <summary>"2x speed": two of the Giant Worm's moves a tick while it goes for an enemy, or is far from its owner; otherwise one (mine).</summary>
        public const int FastSteps = 2;

        /// <summary>Mine: it goes for the nearest enemy this close to its owner, or for the one they have marked this close.</summary>
        public const float Range = 700f, MarkedRange = 1200f;

        /// <summary>Mine: farther than this from its owner it moves at twice its pace even with nothing to go for.</summary>
        public const float FarFromOwner = 480f;

        /// <summary>
        /// The Giant Worm's: out of the ground, and farther than this in both directions from every player, it moves
        /// as if in the ground (here, from its owner), which brings it back to them through the air.
        /// </summary>
        public const float FreeBox = 1000f;

        /// <summary>Mine: farther than this from its owner, the whole worm is brought back into the ground under them.</summary>
        public const float Leash = 2400f;

        /// <summary>Mine: where it circles with nothing to fight, under its owner: this many blocks under the top of the ground, swinging this far to each side.</summary>
        private const float IdleDepth = 9f * 16f, IdleSwing = 6f * 16f, IdleBob = 2f * 16f;

        /// <summary>The Giant Worm's soundDelay: ticks before it may make its digging sound again.</summary>
        private int soundDelay;

        /// <summary>It was in the ground at its last move (for Burst).</summary>
        private bool wasInGround = true;

        /// <summary>Ticks it has had nothing to fight, for its swing under its owner.</summary>
        private int idle;

        private readonly List<int> worm = new List<int>();

        /// <summary>The enemy it goes for: ai[2] is its index plus one, 0 for none (its owner's machine picks it, and tells the rest).</summary>
        private NPC Target
        {
            get
            {
                int i = (int)Projectile.ai[2] - 1;
                if (i < 0 || i >= Main.maxNPCs)
                    return null;

                NPC npc = Main.npc[i];
                return npc.active && npc.CanBeChasedBy(Projectile) ? npc : null;
            }
        }

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            // It draws the whole worm: drawn while anything of a long worm can be on the screen.
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        /// <summary>Behind the blocks, with the NPCs that are (as the Giant Worm is drawn, NPC.behindTiles).</summary>
        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            if (Projectile.hide)
                behindNPCsAndTiles.Add(index);
        }

        protected override void Behave(Player owner)
        {
            bool mine = Projectile.owner == Main.myPlayer;

            Chain(Projectile.owner, worm);

            // Only one head moves a worm; and without a body there is no worm.
            if (worm.Count == 0 || worm[0] != Projectile.whoAmI || !HasBody())
            {
                if (mine)
                    Projectile.Kill();
                return;
            }

            if (mine)
            {
                PickTarget(owner);

                if (Vector2.Distance(owner.Center, Projectile.Center) > Leash)
                    Bring(owner);
            }

            NPC target = Target;
            Vector2 goal;
            int steps;

            if (target != null)
            {
                idle = 0;
                goal = target.Center;
                steps = FastSteps;
            }
            else
            {
                goal = IdleGoal(owner);
                steps = Vector2.Distance(owner.Center, Projectile.Center) > FarFromOwner ? FastSteps : 1;
            }

            if (soundDelay > 0)
                soundDelay--;

            for (int s = 0; s < steps; s++)
            {
                Move(owner, goal);
                Follow();
            }
        }

        private bool HasBody()
        {
            foreach (int i in worm)
            {
                if (Main.projectile[i].ModProjectile is WorminatorBody)
                    return true;
            }

            return false;
        }

        /// <summary>The marked enemy, or the nearest one to its owner, kept while it stays in range; ai[2], told to the rest when it changes.</summary>
        private void PickTarget(Player owner)
        {
            int pick = -1;

            NPC marked = Projectile.OwnerMinionAttackTargetNPC;
            if (marked != null && marked.CanBeChasedBy(Projectile) && Vector2.Distance(marked.Center, owner.Center) < MarkedRange)
            {
                pick = marked.whoAmI;
            }
            else
            {
                NPC now = Target;
                if (now != null && Vector2.Distance(now.Center, owner.Center) < Range)
                {
                    pick = now.whoAmI;
                }
                else
                {
                    float best = Range;
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (!npc.CanBeChasedBy(Projectile))
                            continue;

                        float distance = Vector2.Distance(npc.Center, owner.Center);
                        if (distance < best)
                        {
                            best = distance;
                            pick = i;
                        }
                    }
                }
            }

            if ((int)Projectile.ai[2] != pick + 1)
            {
                Projectile.ai[2] = pick + 1;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>Mine: with nothing to fight, it circles under its owner, swinging from side to side under the top of the ground.</summary>
        private Vector2 IdleGoal(Player owner)
        {
            idle++;

            float top = owner.Bottom.Y;
            if (GroundBelow(owner.Bottom, out _, out Vector2 surface))
                top = surface.Y;

            float t = idle / 60f;
            return new Vector2(owner.Center.X + (float)Math.Sin(t * 0.55f) * IdleSwing, top + IdleDepth + (float)Math.Sin(t * 1.3f) * IdleBob);
        }

        /// <summary>Mine: the whole worm, back into the ground under its owner, going up.</summary>
        private void Bring(Player owner)
        {
            if (!GroundBelow(owner.Bottom, out Vector2 spot, out _))
                spot = owner.Bottom;

            for (int k = 0; k < worm.Count; k++)
            {
                Projectile p = Main.projectile[worm[k]];
                p.Center = spot + new Vector2(0f, k * Spacing);
                p.velocity = Vector2.Zero;
                p.rotation = 0f;
            }

            Projectile.velocity = new Vector2(0f, -Speed);
            Projectile.netUpdate = true;
        }

        /// <summary>
        /// One move of the Giant Worm's head (AI_006_Worms, type 10, as it is in 1.4.4), with goal for the player it
        /// goes for. Both where it is and where it goes are taken to the corner of their tiles first, as the game does.
        /// </summary>
        private void Move(Player owner, Vector2 goal)
        {
            bool ground = InGround(Projectile, true);

            if (ground != wasInGround)
                Burst(Projectile.Center);
            wasInGround = ground;

            // Far from its owner (every player, the Giant Worm's), it moves as in the ground: through the air back to them.
            bool free = !ground && !new Rectangle((int)owner.position.X - (int)FreeBox, (int)owner.position.Y - (int)FreeBox, (int)FreeBox * 2, (int)FreeBox * 2).Intersects(Projectile.Hitbox);

            Vector2 center = Projectile.Center;
            float dx = (int)(goal.X / 16f) * 16f - (int)(center.X / 16f) * 16f;
            float dy = (int)(goal.Y / 16f) * 16f - (int)(center.Y / 16f) * 16f;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            Vector2 v = Projectile.velocity;

            if (!ground && !free)
            {
                v.Y += Gravity;
                if (v.Y > Speed)
                    v.Y = Speed;

                if (Math.Abs(v.X) + Math.Abs(v.Y) < Speed * 0.4f)
                {
                    if (v.X < 0f)
                        v.X -= Accel * 1.1f;
                    else
                        v.X += Accel * 1.1f;
                }
                else if (v.Y == Speed)
                {
                    if (v.X < dx)
                        v.X += Accel;
                    else if (v.X > dx)
                        v.X -= Accel;
                }
                else if (v.Y > 4f)
                {
                    if (v.X < 0f)
                        v.X += Accel * 0.9f;
                    else
                        v.X -= Accel * 0.9f;
                }
            }
            else
            {
                // Its digging sound, quieter than the Giant Worm's (mine), as often as the Giant Worm's: the farther it is from what it goes for, the longer between.
                if (soundDelay == 0 && ground)
                {
                    soundDelay = (int)MathHelper.Clamp(distance / 40f, 10f, 20f);
                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.WormDigQuiet, Projectile.position);
                }

                float absX = Math.Abs(dx), absY = Math.Abs(dy);
                float scale = distance > 0f ? Speed / distance : 0f;
                dx *= scale;
                dy *= scale;

                if ((v.X > 0f && dx > 0f) || (v.X < 0f && dx < 0f) || (v.Y > 0f && dy > 0f) || (v.Y < 0f && dy < 0f))
                {
                    if (v.X < dx)
                        v.X += Accel;
                    else if (v.X > dx)
                        v.X -= Accel;

                    if (v.Y < dy)
                        v.Y += Accel;
                    else if (v.Y > dy)
                        v.Y -= Accel;

                    if (Math.Abs(dy) < Speed * 0.2f && ((v.X > 0f && dx < 0f) || (v.X < 0f && dx > 0f)))
                    {
                        if (v.Y > 0f)
                            v.Y += Accel * 2f;
                        else
                            v.Y -= Accel * 2f;
                    }

                    if (Math.Abs(dx) < Speed * 0.2f && ((v.Y > 0f && dy < 0f) || (v.Y < 0f && dy > 0f)))
                    {
                        if (v.X > 0f)
                            v.X += Accel * 2f;
                        else
                            v.X -= Accel * 2f;
                    }
                }
                else if (absX > absY)
                {
                    if (v.X < dx)
                        v.X += Accel * 1.1f;
                    else if (v.X > dx)
                        v.X -= Accel * 1.1f;

                    if (Math.Abs(v.X) + Math.Abs(v.Y) < Speed * 0.5f)
                    {
                        if (v.Y > 0f)
                            v.Y += Accel;
                        else
                            v.Y -= Accel;
                    }
                }
                else
                {
                    if (v.Y < dy)
                        v.Y += Accel * 1.1f;
                    else if (v.Y > dy)
                        v.Y -= Accel * 1.1f;

                    if (Math.Abs(v.X) + Math.Abs(v.Y) < Speed * 0.5f)
                    {
                        if (v.X > 0f)
                            v.X += Accel;
                        else
                            v.X -= Accel;
                    }
                }
            }

            Projectile.velocity = v;
            Projectile.rotation = (float)Math.Atan2(v.Y, v.X) + 1.57f;
            Projectile.position += v;
            if (v.X != 0f)
                Projectile.direction = v.X > 0f ? 1 : -1;
        }

        /// <summary>
        /// Each segment after the head, to its own width behind the one ahead of it, along the line between them, and
        /// turned along it (AI_006_Worms, a body's move). One on top of the one ahead goes behind it along its turn.
        /// </summary>
        private void Follow()
        {
            for (int k = 1; k < worm.Count; k++)
            {
                Projectile ahead = Main.projectile[worm[k - 1]];
                Projectile p = Main.projectile[worm[k]];

                Vector2 center = p.Center;
                float dx = ahead.position.X + ahead.width / 2 - center.X;
                float dy = ahead.position.Y + ahead.height / 2 - center.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance < 0.01f)
                {
                    p.rotation = ahead.rotation;
                    p.Center = ahead.Center - (ahead.rotation - MathHelper.PiOver2).ToRotationVector2() * Spacing;
                    p.velocity = Vector2.Zero;
                }
                else
                {
                    p.rotation = (float)Math.Atan2(dy, dx) + 1.57f;
                    float pull = (distance - Spacing) / distance;
                    p.velocity = new Vector2(dx * pull, dy * pull);
                    p.position += p.velocity;
                    if (p.velocity.X != 0f)
                        p.direction = p.velocity.X > 0f ? 1 : -1;
                }

                InGround(p, true);
            }
        }

        /// <summary>
        /// The worm, tail first so that each segment lies over the one behind it: each picture turned with its
        /// segment about its middle, which is the middle of the segment, in the light where it is.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Chain(Projectile.owner, worm);

            for (int k = worm.Count - 1; k >= 0; k--)
            {
                Projectile p = Main.projectile[worm[k]];
                Texture2D texture = TextureAssets.Projectile[p.type].Value;
                Color light = Lighting.GetColor((int)(p.Center.X / 16f), (int)(p.Center.Y / 16f));

                Main.EntitySpriteDraw(texture, p.Center - Main.screenPosition + new Vector2(0f, p.gfxOffY), null, p.GetAlpha(light), p.rotation,
                    texture.Size() / 2f, p.scale, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>The two segments a first summon makes behind the head besides the body: drawn as a body, no minion slot.</summary>
    public class WorminatorNeck : WorminatorSegment
    {
        public override string Texture => "PisAndDag/Content/Projectiles/Summoner/WorminatorBody";
    }

    /// <summary>A body: the minion slot. The item summons one (its shoot), and the game lets one go to make room for another minion.</summary>
    public class WorminatorBody : WorminatorSegment
    {
        protected override float Slots => 1f;
    }

    /// <summary>The tail: no minion slot.</summary>
    public class WorminatorTail : WorminatorSegment
    {
    }
}
