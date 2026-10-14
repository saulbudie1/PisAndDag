using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// One of the Bone Squad (the item is BoneSquad), by request: "Summons a squad of minions using
    /// their AIs and defaults". Each is one of the game's Dungeon skeletons (Kind, an NPC type), and
    /// does what that NPC does, by the game's own code for it (NPC.AI and NPC.UpdateNPC as they are
    /// in 1.4.4, taken over step by step), with the game's own numbers, against enemies instead of
    /// players. What it goes for is the enemy its owner has marked, or else the nearest enemy within
    /// 60 tiles; with none, its owner (each kind says how). Farther than 75 tiles from its owner it
    /// is brought back to them.
    ///
    /// It hurts what it walks or flies into for the NPC's own damage (normal mode), raised by its
    /// owner's summon damage as every minion's is, and the same enemy again only Cooldown ticks
    /// later; its spells and arrows hurt for the NPC's own numbers for them, raised the same way.
    ///
    /// The squad together takes 5.5 minion slots: Slots each, which the squad restores every tick
    /// (BoneSquad sets them to nothing while the staff calls a new squad, so that the old one makes
    /// no room it is about to leave anyway).
    /// </summary>
    public abstract class BoneSquadMinion : ModProjectile
    {
        /// <summary>From its middle to an enemy's, at most (mine, as Hoard of Undead's: 60 tiles).</summary>
        public const float Range = 60f * 16f;

        /// <summary>Farther than this from its owner (75 tiles), it is brought back to them, whatever it was doing (as Hoard of Undead's).</summary>
        public const float TooFar = 75f * 16f;

        /// <summary>Ticks before it hurts the same enemy again by touching it (Hoard of Undead's, the user's there).</summary>
        public const int Cooldown = 35;

        /// <summary>Ticks before any of a kind of spell or arrow of theirs hurts the same enemy again (mine).</summary>
        public const int ShotCooldown = 10;

        /// <summary>The NPC it is: its picture, its size, its AI, its damage.</summary>
        public abstract int Kind { get; }

        /// <summary>The minion slots it takes: the two Armored Bones 0.75 each, the other four 1, 5.5 in all.</summary>
        public virtual float Slots => 1f;

        /// <summary>An NPC's size, as NPC.SetDefaults has it (every one of the squad's is 18 by 40 but the Giant Cursed Skull's).</summary>
        public virtual int Width => 18;

        public virtual int Height => 40;

        /// <summary>The enemy it goes for (an index into Main.npc), or -1 for none (NPC.target, here only ever an enemy).</summary>
        protected int target = -1;

        /// <summary>It goes for an enemy.</summary>
        protected bool Fighting => target >= 0;

        protected bool begun;

        /// <summary>The game's damage for each of the squad's NPCs, normal mode (NPC.SetDefaults).</summary>
        public static int DamageOf(int npcType)
        {
            switch (npcType)
            {
                case NPCID.SkeletonArcher: return 45;
                case NPCID.RustyArmoredBonesAxe: return 70;
                case NPCID.RustyArmoredBonesFlail: return 55;
                case NPCID.RustyArmoredBonesSword: return 70;
                case NPCID.RustyArmoredBonesSwordNoArmor: return 75;
                case NPCID.BlueArmoredBones: return 45;
                case NPCID.BlueArmoredBonesMace: return 65;
                case NPCID.BlueArmoredBonesNoPants: return 45;
                case NPCID.BlueArmoredBonesSword: return 85;
                case NPCID.HellArmoredBones: return 70;
                case NPCID.HellArmoredBonesSpikeShield: return 65;
                case NPCID.HellArmoredBonesMace: return 40;
                case NPCID.HellArmoredBonesSword: return 75;
                case NPCID.RaggedCaster: return 40;
                case NPCID.RaggedCasterOpenCoat: return 35;
                case NPCID.Necromancer: return 50;
                case NPCID.NecromancerArmored: return 35;
                case NPCID.DiabolistRed: return 50;
                case NPCID.DiabolistWhite: return 60;
                case NPCID.BoneLee: return 90;
                case NPCID.GiantCursedSkull: return 60;
                default: return 1;
            }
        }

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
            Projectile.minionSlots = Slots;
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

        /// <summary>Its moving is done in AI, as an NPC's is done by NPC.UpdateNPC.</summary>
        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int buff = ModContent.BuffType<BoneSquadBuff>();

            if (!owner.active || owner.dead)
            {
                owner.ClearBuff(buff);
                return;
            }

            if (owner.HasBuff(buff))
                Projectile.timeLeft = 2;

            Projectile.minionSlots = Slots;

            if (!begun)
            {
                begun = true;
                Begin(owner);
            }

            // Too far from its owner, it is brought back, whatever it was doing.
            if (Vector2.Distance(owner.Center, Projectile.Center) > TooFar)
                Fetch(owner);

            Tick(owner);
        }

        /// <summary>The first tick it is seen (on whichever machine).</summary>
        protected virtual void Begin(Player owner)
        {
            Projectile.direction = Projectile.spriteDirection = owner.direction == 0 ? 1 : owner.direction;
        }

        /// <summary>Brought back to its owner, with nothing to fight.</summary>
        protected abstract void Fetch(Player owner);

        /// <summary>A tick of the NPC it is.</summary>
        protected abstract void Tick(Player owner);

        /// <summary>
        /// The enemy it goes for, as NPC.TargetClosest picks an NPC's player: the nearest, by how far
        /// across and how far up or down together, the first of any as near; within Range. The one its
        /// owner has marked before any, if it is within Range (as the game's minions do). -1 for none.
        /// </summary>
        protected int Choose(Player owner)
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

        /// <summary>The enemy it had is gone, or no longer to be gone for.</summary>
        protected void Forget()
        {
            if (target >= 0 && !Main.npc[target].CanBeChasedBy(Projectile))
                target = -1;
        }

        /// <summary>
        /// A spell's or an arrow's damage: the NPC's own for it, as much more as the staff's damage is
        /// over the squad's (BoneSquad.Damage), raised by its owner's summon damage. Fixed when it is
        /// made, as the game's own minions' shots are.
        /// </summary>
        protected int ShotDamage(int npcDamage)
        {
            float own = (float)Math.Round(npcDamage * (double)Projectile.originalDamage / DamageOf(Kind));
            return (int)Main.player[Projectile.owner].GetTotalDamage(DamageClass.Summon).ApplyTo(own);
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((short)target);
            writer.Write((sbyte)Projectile.direction);
            writer.Write((sbyte)Projectile.spriteDirection);
            SendState(writer);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            target = reader.ReadInt16();
            if (target < -1 || target >= Main.maxNPCs)
                target = -1;

            Projectile.direction = reader.ReadSByte();
            Projectile.spriteDirection = reader.ReadSByte();
            ReceiveState(reader);
        }

        protected virtual void SendState(BinaryWriter writer)
        {
        }

        protected virtual void ReceiveState(BinaryReader reader)
        {
        }

        // ---- drawn ------------------------------------------------------------------------------

        /// <summary>The NPC's own picture (loaded on the thread that draws, the first time).</summary>
        protected static Texture2D PictureOf(int npcType) =>
            ModContent.Request<Texture2D>("Terraria/Images/NPC_" + npcType, AssetRequestMode.ImmediateLoad).Value;

        /// <summary>
        /// The NPC drawn where and as Main.DrawNPCDirect draws it: its frame's middle over its box's
        /// middle, its feet 4 below its bottom (by the sheet's height over its frames, as the game
        /// works it out), shifted by NPCAddHeight and its step up or down, turned, flipped when it
        /// faces right; in the colour given (the game's for it), as BoneSquadLook draws.
        /// </summary>
        protected void DrawNpc(int npcType, int frame, float rotation, Color colour, float addHeight, int cut = 0)
        {
            Texture2D own = PictureOf(npcType);
            int frames = Main.npcFrameCount[npcType];
            int frameHeight = own.Height / frames;
            Rectangle source = new Rectangle(0, frame * frameHeight, own.Width, frameHeight - cut);
            Vector2 half = new Vector2(own.Width / 2, own.Height / frames / 2);

            Vector2 at = new Vector2(
                Projectile.position.X - Main.screenPosition.X + (float)(Projectile.width / 2) - (float)own.Width / 2f + half.X,
                Projectile.position.Y - Main.screenPosition.Y + (float)Projectile.height - (float)own.Height / (float)frames + 4f + half.Y + addHeight + Projectile.gfxOffY);

            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            BoneSquadLook.Ring ring = BoneSquadLook.RingOf(own, source, BoneSquadLook.SheetMiddle(own, frames, frameHeight), true);
            BoneSquadLook.Draw(null, own, at, source, colour, rotation, half, 1f, effects, ring);
        }
    }

    /// <summary>
    /// A squad member the ground holds: it falls, lands, walks into walls and up and down stairs and
    /// slopes, and is slowed in liquids, as NPC.UpdateNPC moves the game's walking NPCs (its gravity
    /// and NPC.UpdateCollision as they are in 1.4.4, the game's numbers): Hoard of Undead's
    /// (HoardOfUndeadZombie) with a skeleton's own gravity and liquids.
    /// </summary>
    public abstract class BoneSquadBody : BoneSquadMinion
    {
        /// <summary>Each step it takes in water, lava, honey and shimmer is its speed times this (NPC.UpdateCollision's, for every NPC).</summary>
        private const float WaterSpeed = 0.5f, LavaSpeed = 0.5f, HoneySpeed = 0.25f, ShimmerSpeed = 0.375f;

        /// <summary>How much faster it falls each tick at the ground's height and below (NPC.UpdateNPC_UpdateGravity's 0.3).</summary>
        protected const float Pull = 0.3f;

        /// <summary>NPC.stairFall: it is going down through stairs.</summary>
        protected bool stairFall;

        /// <summary>NPC.oldPosition: where it was before it last moved.</summary>
        protected Vector2 before;

        /// <summary>NPC.collideX and collideY: a block stopped it on its last move.</summary>
        protected bool collideX, collideY;

        /// <summary>NPC.wet, lavaWet, honeyWet and shimmerWet: in a liquid, and which, as it was found when it last moved.</summary>
        protected bool wet, lavaWet, honeyWet, shimmerWet;

        protected override void Begin(Player owner)
        {
            base.Begin(owner);
            before = Projectile.position;
        }

        /// <summary>To its owner's feet (the space a player stands in is free), with nothing to fight. Where it was before stays its last place, so that it is not taken for stuck where it lands.</summary>
        protected override void Fetch(Player owner)
        {
            Projectile.position = new Vector2(owner.Center.X - Projectile.width / 2f, owner.position.Y + owner.height - Projectile.height);
            Projectile.velocity = Vector2.Zero;
            target = -1;
            Projectile.gfxOffY = 0f;
            stairFall = false;
            wet = lavaWet = honeyWet = shimmerWet = false;
            Projectile.netUpdate = true;
        }

        protected override void SendState(BinaryWriter writer)
        {
            writer.Write(stairFall);
            writer.Write((byte)((wet ? 1 : 0) | (lavaWet ? 2 : 0) | (honeyWet ? 4 : 0) | (shimmerWet ? 8 : 0)));
        }

        protected override void ReceiveState(BinaryReader reader)
        {
            // Made on another machine: begun there.
            if (!begun)
            {
                begun = true;
                before = Projectile.position;
            }

            stairFall = reader.ReadBoolean();
            byte liquid = reader.ReadByte();
            wet = (liquid & 1) != 0;
            lavaWet = (liquid & 2) != 0;
            honeyWet = (liquid & 4) != 0;
            shimmerWet = (liquid & 8) != 0;
        }

        /// <summary>A tick of an NPC that the ground holds, in NPC.UpdateNPC's order: its gravity, its AI, falling, its move, its frame.</summary>
        protected override void Tick(Player owner)
        {
            Gravity(out float gravity, out float maxFallSpeed);
            Think(owner);

            Projectile.velocity.Y += gravity;
            if (Projectile.velocity.Y > maxFallSpeed)
                Projectile.velocity.Y = maxFallSpeed;

            if ((double)Projectile.velocity.X < 0.005 && (double)Projectile.velocity.X > -0.005)
                Projectile.velocity.X = 0f;

            Move(gravity, FallThrough);
            Frame();
        }

        /// <summary>Its AI (the game's for its NPC).</summary>
        protected abstract void Think(Player owner);

        /// <summary>Its frame (NPC.FindFrame for its NPC).</summary>
        protected abstract void Frame();

        /// <summary>NPC.Collision_DecideFallThroughPlatforms: whether it goes down through platforms.</summary>
        protected abstract bool FallThrough { get; }

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
        /// NPC.UpdateCollision: down slopes, in a liquid or not, through platforms or not, stopped by
        /// blocks, slowed in liquids, up and down stairs and slopes.
        /// </summary>
        private void Move(float gravity, bool fall)
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
    }
}
