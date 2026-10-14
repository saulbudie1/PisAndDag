using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// A vulture of the Vulture Staff's (the item is Content/Items/Summoner/VultureStaff.cs), by request: "Summon
    /// minion vultures to fight for you (modify vultures textures). idle: sit on ur 2 shoulders and stack into 2
    /// towers. I guess like finch staff but you can use your creativity to make it more interesting." One minion
    /// slot each.
    ///
    /// Idle, they sit on their owner's shoulders, as the Baby Finch sits on the head (Projectile.AI_158_GetHomeLocation:
    /// on the owner's middle, turned with them, with the head's bob as they walk): the first on the shoulder the owner
    /// faces with, the second on the other, the third on the first's back, and so on, two towers. They face the way
    /// their owner faces, hop across when the owner turns, flap to keep their balance when the owner jumps or lands,
    /// stretch their wings now and then, and the higher ones lean back when the owner runs (all mine).
    ///
    /// Mine, the rest. What they go for is what vultures go for: of the enemies near their owner, the one with the
    /// least of its life left (the marked one before any). They take off, circle over it together, and one at a
    /// time dive on it with a screech, and pull up when they hit it or miss. They hurt an enemy under half its life
    /// 25% more. When one of them kills, they all wait half as long between dives for 3 seconds (VultureFrenzy).
    ///
    /// The picture is the game's own Vulture's (NPC 61), copied into the mod's folder for the user to change
    /// (PlaceholderTextures.md): six frames of 78 by 60, facing left; frame 0 perched, 1 to 5 its wings in flight.
    /// </summary>
    public class VultureMinion : ModProjectile
    {
        /// <summary>The Vulture's sheet (NPC 61).</summary>
        public const int FrameWidth = 78, FrameHeight = 60, FrameCount = 6;

        /// <summary>What it is doing (ai[0]).</summary>
        public const int Perched = 0, Returning = 1, Circling = 2, Diving = 3, Climbing = 4;

        /// <summary>
        /// Mine: where the towers stand. The top of the shoulders is 5 above the middle of a player (the body's
        /// picture, Player_0_3, begins 26 down its 56-high frame, which is drawn 10 above the player's box); the two
        /// towers stand this far to each side of the head, each vulture up a tower this much further out than the
        /// one under it, so that the towers part as they rise; the perched picture's feet end 28 below the middle
        /// of its frame; each vulture of a tower stands this much higher than the one under it, on its back. Drawn
        /// for the game's Vulture, 38 wide and 38 high perched: a new picture of another size wants these changed.
        /// </summary>
        public const float ShoulderY = -5f, ShoulderX = 16f, Splay = 3f, FeetBelowMiddle = 28f, Stack = 24f;

        /// <summary>Mine: it goes for enemies this close to its owner, the marked one this close.</summary>
        public const float Range = 640f, MarkedRange = 1000f;

        /// <summary>Mine: over what it goes for they circle, this wide, this high above it, the circle flattened to this.</summary>
        public const float CircleRadius = 110f, CircleLift = 150f, CircleFlatten = 0.35f;

        /// <summary>Mine: ticks they take to go once round it.</summary>
        public const int CircleTicks = 180;

        /// <summary>Mine: ticks it circles between dives (in a frenzy, half), ticks a dive may last, ticks it climbs after one.</summary>
        public const int DiveWait = 48, FrenzyDiveWait = 24, DiveTime = 40, ClimbTime = 18;

        /// <summary>Mine: how fast a dive gets faster, and its top speed.</summary>
        public const float DiveAccel = 1.1f, DiveSpeed = 15f;

        /// <summary>Mine: 25% more damage to an enemy under half its life.</summary>
        public const float WeakBonus = 0.25f;

        /// <summary>Mine: ticks before it hurts the same enemy again.</summary>
        public const int Cooldown = 20;

        /// <summary>Farther than this from its owner it is put back on its shoulder at once (the Baby Finch's 2000).</summary>
        public const float TooFar = 2000f;

        private int Mode
        {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        /// <summary>Ticks in what it is doing (ai[1]).</summary>
        private ref float Timer => ref Projectile.ai[1];

        /// <summary>What it goes for: ai[2] is its index plus one, 0 for none (its owner's machine picks it and tells the rest).</summary>
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

        // ---- the look of it, on each machine on its own -------------------------------------------

        /// <summary>The mode last shown (for the sounds of taking off and diving), -1 before the first.</summary>
        private int shown = -1;

        /// <summary>What is left of a hop from one shoulder to the other, ticks left of its arc, and the way its owner faced last tick.</summary>
        private Vector2 hop;

        private int hopTime, lastDirection;

        /// <summary>How far back it leans on its tower, and ticks left of a stretch and of a balancing flap.</summary>
        private float lean;

        private int stretch, flap;

        /// <summary>Its owner's speed down last tick (a jump or a landing makes it flap).</summary>
        private float ownerFall;

        private bool begun;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = FrameCount;
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;

            // Where it was, the last few ticks, for the streak behind a dive.
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Cooldown;
        }

        public override bool? CanCutTiles() => false;

        public override bool MinionContactDamage() => true;

        /// <summary>It hurts only out hunting, not sitting or flying home.</summary>
        public override bool? CanDamage() => Mode >= Circling ? null : false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int buff = ModContent.BuffType<VultureStaffBuff>();

            if (!owner.active || owner.dead)
            {
                owner.ClearBuff(buff);
                return;
            }

            if (owner.HasBuff(buff))
                Projectile.timeLeft = 2;

            bool mine = Projectile.owner == Main.myPlayer;
            Group(out int index, out int count);
            Vector2 home = Home(owner, index);

            if (!begun)
            {
                begun = true;
                lastDirection = owner.direction;
                ownerFall = owner.velocity.Y;
            }

            if (Vector2.Distance(owner.Center, Projectile.Center) > TooFar)
            {
                Projectile.Center = home;
                Projectile.velocity = Vector2.Zero;
                Mode = Perched;
                Timer = 0f;
                hop = Vector2.Zero;
                if (mine)
                    Projectile.netUpdate = true;
            }

            if (mine)
                PickTarget(owner);

            NPC target = Target;

            switch (Mode)
            {
                case Perched:
                    Sit(owner, home, index, target, mine);
                    break;
                case Returning:
                    Return(owner, home, target, mine);
                    break;
                case Circling:
                    Circle(owner, index, count, target, mine);
                    break;
                case Diving:
                    Dive(target, mine);
                    break;
                default:
                    Climb(mine);
                    break;
            }

            if (Mode == Circling || Mode == Climbing)
                KeepApart();

            lastDirection = owner.direction;

            if (Mode != shown)
            {
                Shown(shown, Mode);
                shown = Mode;
            }

            Turn(owner, target);
            Animate(owner);

            // Sitting on the shoulder away from the way its owner faces, it is drawn behind the other tower; both behind the owner.
            Projectile.hide = Mode == Perched && index % 2 == 1;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            if (Projectile.hide)
                behindProjectiles.Add(index);
        }

        /// <summary>Which of its owner's vultures it is (by identity, the same order on every machine), and how many they have.</summary>
        private void Group(out int index, out int count)
        {
            index = count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == Type)
                {
                    count++;
                    if (p.identity < Projectile.identity)
                        index++;
                }
            }
        }

        /// <summary>
        /// Its place on its owner, where the middle of its frame is while it sits: index 0 on the shoulder the owner
        /// faces with, 1 on the other, 2 on 0's back, and so on. On the owner's middle as the Baby Finch has it,
        /// turned with them (their mount, their gravity), with the head's bob as they walk (Main.OffsetsPlayerHeadgear,
        /// 2 down when they stand, as the Baby Finch takes it).
        /// </summary>
        public static Vector2 Home(Player owner, int index)
        {
            int height = owner.bodyFrame.Height;
            if (height == 0)
                height = 1;

            Vector2 bob = Main.OffsetsPlayerHeadgear[owner.bodyFrame.Y / height];
            bob.Y -= 2f;
            bob.Y *= owner.gravDir;

            int side = index % 2 == 0 ? 1 : -1;
            int level = index / 2;
            Vector2 place = new Vector2(owner.direction * side * (ShoulderX + level * Splay), owner.gravDir * (ShoulderY - FeetBelowMiddle - level * Stack));

            return Utils.Floor(owner.RotatedRelativePoint(owner.MountedCenter, false, true) + bob + place);
        }

        /// <summary>
        /// The marked enemy; or of those near its owner and in their sight, the one with the least of its life left
        /// (the nearer of two alike), but the one it has while that has no more than a twentieth more left than it.
        /// </summary>
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
                int now = (int)Projectile.ai[2] - 1;
                float least = float.MaxValue, nearest = float.MaxValue, nowLeft = float.MaxValue;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy(Projectile))
                        continue;

                    float distance = Vector2.Distance(npc.Center, owner.Center);
                    if (distance > Range || !Collision.CanHitLine(owner.position, owner.width, owner.height, npc.position, npc.width, npc.height))
                        continue;

                    float left = npc.life / (float)npc.lifeMax;
                    if (i == now)
                        nowLeft = left;

                    if (left < least - 0.001f || (left < least + 0.001f && distance < nearest))
                    {
                        least = left;
                        nearest = distance;
                        pick = i;
                    }
                }

                if (pick >= 0 && now >= 0 && nowLeft <= least + 0.05f)
                    pick = now;
            }

            if ((int)Projectile.ai[2] != pick + 1)
            {
                Projectile.ai[2] = pick + 1;
                Projectile.netUpdate = true;
            }
        }

        private void Sit(Player owner, Vector2 home, int index, NPC target, bool mine)
        {
            // Its owner turned round, and its shoulder with them: it hops across, from where it was.
            int level = index / 2;
            if (owner.direction != lastDirection)
            {
                int side = index % 2 == 0 ? 1 : -1;
                hop.X -= 2f * owner.direction * side * (ShoulderX + level * Splay);
                hopTime = 12;
            }

            hop *= 0.7f;
            if (hop.Length() < 0.5f)
                hop = Vector2.Zero;

            float arc = hopTime > 0 ? (float)Math.Sin(hopTime / 12f * MathHelper.Pi) * 8f : 0f;
            if (hopTime > 0)
                hopTime--;

            // The higher ones lean back against their owner's running.
            lean = MathHelper.Lerp(lean, MathHelper.Clamp(-owner.velocity.X * 0.6f, -5f, 5f) * level, 0.12f);

            Projectile.Center = home + hop + new Vector2(lean, -arc * owner.gravDir);
            Projectile.velocity = Vector2.Zero;

            if (mine && target != null)
                TakeOff(owner, index);
        }

        /// <summary>Off its shoulder, up, to circle; the rest after it a little later each, so that they dive one after another.</summary>
        private void TakeOff(Player owner, int index)
        {
            Mode = Circling;
            Timer = -index * 18f;
            Projectile.velocity = new Vector2(owner.direction * 1.5f, -4.5f);
            Projectile.netUpdate = true;
        }

        private void Return(Player owner, Vector2 home, NPC target, bool mine)
        {
            if (mine && target != null)
            {
                Mode = Circling;
                Timer = 0f;
                Projectile.netUpdate = true;
                return;
            }

            Vector2 to = home - Projectile.Center;
            if (mine && to.Length() < 10f)
            {
                Mode = Perched;
                Timer = 0f;
                Projectile.Center = home;
                Projectile.velocity = Vector2.Zero;
                hop = Vector2.Zero;
                Projectile.netUpdate = true;
                return;
            }

            // After its shoulder as it moves: its owner's speed, and some of the way still to go.
            float top = Math.Max(9f, owner.velocity.Length() + 3f);
            Vector2 want = owner.velocity + to * 0.15f;
            if (want.Length() > top)
                want = Vector2.Normalize(want) * top;

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.2f);
        }

        private void Circle(Player owner, int index, int count, NPC target, bool mine)
        {
            if (target == null)
            {
                if (mine)
                {
                    Mode = Returning;
                    Timer = 0f;
                    Projectile.netUpdate = true;
                }

                return;
            }

            Timer++;

            // All on one circle, turning together, spread round it.
            float angle = (Main.GameUpdateCount % CircleTicks + index * CircleTicks / (float)Math.Max(count, 1)) / CircleTicks * MathHelper.TwoPi;
            Vector2 spot = target.Center + new Vector2((float)Math.Cos(angle) * CircleRadius, -CircleLift + (float)Math.Sin(angle) * CircleRadius * CircleFlatten);

            Vector2 want = (spot - Projectile.Center) * 0.12f;
            if (want.Length() > 12f)
                want = Vector2.Normalize(want) * 12f;

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.1f);

            int wait = owner.GetModPlayer<VultureFrenzy>().Frenzy > 0 ? FrenzyDiveWait : DiveWait;
            if (mine && Timer >= wait)
            {
                Mode = Diving;
                Timer = 0f;
                Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 6f;
                Projectile.netUpdate = true;
            }
        }

        private void Dive(NPC target, bool mine)
        {
            Timer++;

            if (target != null)
                Projectile.velocity += (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * DiveAccel;

            if (Projectile.velocity.Length() > DiveSpeed)
                Projectile.velocity = Vector2.Normalize(Projectile.velocity) * DiveSpeed;

            if (mine && (Timer > DiveTime || target == null))
                PullUp();
        }

        /// <summary>Out of a dive, hit or missed: up and away, slowing.</summary>
        private void PullUp()
        {
            Mode = Climbing;
            Timer = 0f;
            Projectile.velocity = new Vector2(Projectile.velocity.X * 0.4f, -Math.Abs(Projectile.velocity.Y) * 0.35f - 3f);
            Projectile.netUpdate = true;
        }

        private void Climb(bool mine)
        {
            Timer++;
            Projectile.velocity.Y -= 0.55f;
            if (Projectile.velocity.Y < -9f)
                Projectile.velocity.Y = -9f;
            Projectile.velocity.X *= 0.96f;

            if (mine && Timer >= ClimbTime)
            {
                Mode = Circling;
                Timer = 0f;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>Out of each other's way in the air, as the Baby Finches keep (mine, how much).</summary>
        private void KeepApart()
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (i == Projectile.whoAmI || !p.active || p.owner != Projectile.owner || p.type != Type || (int)p.ai[0] == Perched)
                    continue;

                Vector2 away = Projectile.Center - p.Center;
                if (away.Length() < 36f)
                    Projectile.velocity += away.SafeNormalize(Vector2.UnitX) * 0.25f;
            }
        }

        /// <summary>The sounds of a change, on every machine: its wings as it takes off, a screech as it dives (the Vulture's own hurt sound, higher).</summary>
        private void Shown(int was, int now)
        {
            if (was < 0 || Main.dedServ)
                return;

            if (was == Perched && now != Perched)
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, PitchVariance = 0.2f }, Projectile.Center);
            else if (now == Diving)
                SoundEngine.PlaySound(SoundID.NPCHit28 with { Volume = 0.35f, Pitch = 0.25f, PitchVariance = 0.1f }, Projectile.Center);
        }

        /// <summary>Which way it faces, and its tilt: as its owner while it sits; at what it dives on, its beak along the dive; banked with its speed in flight.</summary>
        private void Turn(Player owner, NPC target)
        {
            if (Mode == Perched)
            {
                Projectile.spriteDirection = owner.direction;
                Projectile.rotation = 0f;
                return;
            }

            float want;
            if (Mode == Diving)
            {
                if (target != null)
                    Projectile.spriteDirection = target.Center.X > Projectile.Center.X ? 1 : -1;

                // The picture faces left: turned on its own, its beak points the other way to a flipped one's.
                float aim = Projectile.velocity.ToRotation();
                if (Projectile.spriteDirection == -1)
                    aim = MathHelper.WrapAngle(aim - MathHelper.Pi);

                want = MathHelper.Clamp(aim, -1.25f, 1.25f);
            }
            else
            {
                if (Math.Abs(Projectile.velocity.X) > 0.4f)
                    Projectile.spriteDirection = Projectile.velocity.X > 0f ? 1 : -1;

                want = MathHelper.Clamp(Projectile.velocity.X * 0.045f, -0.35f, 0.35f);
            }

            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, want, 0.25f);
        }

        /// <summary>
        /// Sitting, frame 0; a stretch of its wings now and then (1, 2, 1), and flaps (1 and 2) to keep its balance when
        /// its owner jumps or lands. In a dive, its wings in (3). Flying, its wings go round, 1 to 5, five ticks each.
        /// </summary>
        private void Animate(Player owner)
        {
            if (Mode == Perched)
            {
                if (Math.Abs(owner.velocity.Y - ownerFall) > 3f)
                    flap = Math.Max(flap, 10);

                if (stretch == 0 && flap == 0 && Main.rand.NextBool(600))
                    stretch = 18;

                if (flap > 0)
                {
                    flap--;
                    Projectile.frame = flap / 3 % 2 == 0 ? 2 : 1;
                }
                else if (stretch > 0)
                {
                    stretch--;
                    Projectile.frame = stretch >= 12 || stretch < 6 ? 1 : 2;
                }
                else
                {
                    Projectile.frame = 0;
                }
            }
            else if (Mode == Diving)
            {
                Projectile.frame = 3;
            }
            else
            {
                if (Projectile.frame < 1)
                    Projectile.frame = 1;

                if (++Projectile.frameCounter >= 5)
                {
                    Projectile.frameCounter = 0;
                    Projectile.frame = Projectile.frame >= 5 ? 1 : Projectile.frame + 1;
                }
            }

            ownerFall = owner.velocity.Y;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (target.life < target.lifeMax * 0.5f)
                modifiers.SourceDamage *= 1f + WeakBonus;
        }

        /// <summary>A hit ends a dive; a kill sends the flock into a frenzy (on its owner's machine, where hits are made).</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target.life <= 0)
                Main.player[Projectile.owner].GetModPlayer<VultureFrenzy>().Frenzy = VultureFrenzy.Time;

            if (Mode == Diving && Projectile.owner == Main.myPlayer)
                PullUp();
        }

        /// <summary>
        /// Its frame, about the frame's middle, flipped to face right, and upside down sitting on an owner whose
        /// gravity is turned; in the light where it is. Behind a dive, a streak of where it was, red in a frenzy.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = new Rectangle(0, Projectile.frame * FrameHeight, FrameWidth, FrameHeight);
            Vector2 origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);

            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (Mode == Perched && Main.player[Projectile.owner].gravDir == -1f)
                effects |= SpriteEffects.FlipVertically;

            Color light = Projectile.GetAlpha(Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f)));
            Vector2 offset = new Vector2(0f, Projectile.gfxOffY) - Main.screenPosition;

            if (Mode == Diving)
            {
                Color streak = Main.player[Projectile.owner].GetModPlayer<VultureFrenzy>().Frenzy > 0 ? light.MultiplyRGB(new Color(255, 110, 100)) : light;
                // Every other place it was, the further back the fainter.
                for (int k = Projectile.oldPos.Length - 1; k >= 2; k -= 2)
                {
                    if (Projectile.oldPos[k] == Vector2.Zero)
                        continue;

                    float fade = 0.36f * (1f - k / (float)Projectile.oldPos.Length);
                    Main.EntitySpriteDraw(texture, Projectile.oldPos[k] + Projectile.Size / 2f + offset, frame, streak * fade, Projectile.oldRot[k], origin, Projectile.scale, effects, 0f);
                }
            }

            Main.EntitySpriteDraw(texture, Projectile.Center + offset, frame, light, Projectile.rotation, origin, Projectile.scale, effects, 0f);
            return false;
        }
    }

    /// <summary>The Vulture Staff's frenzy: ticks left of it, set by a vulture's kill (VultureMinion.OnHitNPC). Its owner's machine's own.</summary>
    public class VultureFrenzy : ModPlayer
    {
        /// <summary>Mine: 3 seconds.</summary>
        public const int Time = 180;

        public int Frenzy;

        public override void ResetEffects()
        {
            if (Frenzy > 0)
                Frenzy--;
        }
    }
}
