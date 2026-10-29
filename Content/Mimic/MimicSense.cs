using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items.Mage;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// What the copy is told each tick (MimicBrain.Situation), read off the world: itself and how it can move as the
    /// game has it after its last tick, its owner, every shot and enemy body that could hurt it, every enemy it could
    /// fight and which of them are after it, what its weapon is, its life and potions, whether it can parry.
    /// </summary>
    public static class MimicSense
    {
        /// <summary>How far it looks for what could hurt it and what it could fight, in pixels: 70 tiles.</summary>
        public const float Reach = 70 * 16f;

        private static readonly Threat[] threats = new Threat[320];

        /// <summary>
        /// What each projectile slot was doing last tick (its identity, velocity and heading), so the copy can be told
        /// how a shot is speeding up, falling or bending: the game does not say, but two ticks show it.
        /// </summary>
        private static readonly int[] lastIdentity = new int[Main.maxProjectiles + 1];
        private static readonly Vector2[] lastVelocity = new Vector2[Main.maxProjectiles + 1];
        private static readonly float[] lastHeading = new float[Main.maxProjectiles + 1];
        private static readonly uint[] lastSeen = new uint[Main.maxProjectiles + 1];
        private static readonly Foe[] foes = new Foe[48];
        private static readonly WorldGround ground = new WorldGround();

        public static Situation Read(Player copy, Player owner, int lastTarget, float jumpSpeed, int jumpTicks)
        {
            var s = new Situation
            {
                Me = Body(copy, jumpSpeed, jumpTicks),
                Owner = owner.Center,
                OwnerFacing = owner.direction,
                OwnerTarget = owner.HasMinionAttackTargetNPC ? owner.MinionAttackTargetNPC : -1,
                LastTargetIndex = lastTarget,
                Threats = threats,
                Foes = foes,
                Weapon = WeaponOf(copy),
                Life = copy.statLife,
                LifeMax = Math.Max(1, copy.statLifeMax2),
                Mana = copy.statMana,
                ManaMax = Math.Max(1, copy.statManaMax2),
                Potions = copy.CountItem(ItemID.SuperHealingPotion),
                PotionReady = copy.potionDelay <= 0,
                PotionHeal = Heal(copy),
                ParryReady = !copy.GetModPlayer<ParryPlayer>().Busy,
                ParryStartup = ParryTiming.CurvedSword.Startup,
                ParryReach = ParryPlayer.Reach,
                Ground = ground,
            };

            Vector2 me = copy.Center;
            Rectangle body = copy.Hitbox;

            // Inside the Empress's arena with her walls up, they hold it as they hold its owner (MimicPlayer).
            ground.Walled = NPCs.Empress.EmpressArena.Up && NPCs.Empress.EmpressArena.Holds
                && NPCs.Empress.Decagon.Inside(me, NPCs.Empress.EmpressArena.Centre, NPCs.Empress.EmpressArena.Size);
            float far = Reach * Reach;
            int n = 0;

            uint now = Main.GameUpdateCount;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (n >= threats.Length)
                    break;

                if (!p.hostile || p.damage <= 0)
                    continue;

                int i = p.whoAmI;
                int steps = 1 + p.extraUpdates;
                Vector2 vel = p.velocity * steps;
                float heading = p.velocity.LengthSquared() > 1e-4f ? p.velocity.ToRotation() : lastHeading[i];
                bool seenBefore = lastIdentity[i] == p.identity && lastSeen[i] == now - 1;
                Vector2 acc = seenBefore ? vel - lastVelocity[i] : Vector2.Zero;
                float bend = seenBefore ? Math.Abs(MathHelper.WrapAngle(heading - lastHeading[i])) : 0f;

                lastIdentity[i] = p.identity;
                lastVelocity[i] = vel;
                lastHeading[i] = heading;
                lastSeen[i] = now;

                var th = new Threat
                {
                    Pos = p.Center,
                    Vel = vel,
                    HalfW = p.width * 0.5f,
                    HalfH = p.height * 0.5f,
                    // A hostile shot lands for twice what it says (Projectile.Damage), times the difficulty's
                    // (CombinedHooks.ModifyHitByProjectile).
                    Weight = Weight(MimicBeams.HitOf(p), s.LifeMax),
                    Parryable = Parry.Can(p),
                    // Gone when its time runs out (a tick of it for each of its updates).
                    Life = p.timeLeft > 0 ? Math.Max(1, p.timeLeft / steps) : 0,
                };

                // A beam (a deathray, a sweeping light), a shot that waits before it flies, a trail, a shape: as the game
                // hits with it, perhaps as several.
                if (MimicBeams.Read(p, th, threats, ref n, me, body))
                    continue;

                if (Vector2.DistanceSquared(p.Center, me) > far)
                    continue;

                // Speeding up or falling, as the last two ticks show; bending toward someone, as room that grows.
                if (acc.LengthSquared() < 4f)
                    th.Acc = acc;

                if (bend > 0.004f)
                {
                    th.Homing = Math.Min(vel.Length(), vel.Length() * bend * 4f);
                    th.HomeTurn = bend;
                }

                threats[n++] = th;
            }

            int f = 0;

            // What its owner is fighting: their minions' target, else what they hit in the last two seconds. It is
            // fought even if the game would not let minions chase it (some of the mod's own are like that).
            MimicPlayer theirs = owner.GetModPlayer<MimicPlayer>();
            int ownerHit = theirs.LastHit >= 0 && theirs.LastHit < Main.maxNPCs && Main.GameUpdateCount - theirs.LastHitAt < 120
                && Main.npc[theirs.LastHit].active && Main.npc[theirs.LastHit].life > 0 ? theirs.LastHit : -1;

            if (s.OwnerTarget < 0)
                s.OwnerTarget = ownerHit;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (Vector2.DistanceSquared(npc.Center, me) > far)
                    continue;

                if (!npc.friendly && npc.damage > 0 && n < threats.Length)
                {
                    // A body: where it is going, how it is speeding up (its velocity before its last move), and whether it
                    // is after the copy.
                    Vector2 bodyAcc = npc.velocity - npc.oldVelocity;

                    threats[n++] = new Threat
                    {
                        Pos = npc.Center,
                        Vel = npc.velocity,
                        Acc = bodyAcc.LengthSquared() < 4f ? bodyAcc : Vector2.Zero,
                        HalfW = npc.width * 0.5f,
                        HalfH = npc.height * 0.5f,
                        Weight = Weight(npc.damage, s.LifeMax),
                        Parryable = false,
                        Chaser = npc.target == copy.whoAmI && npc.velocity.LengthSquared() > 4f,
                    };
                }

                bool theyFight = npc.whoAmI == ownerHit && !npc.friendly && !npc.dontTakeDamage;

                if (f < foes.Length && (npc.CanBeChasedBy() || theyFight))
                {
                    foes[f++] = new Foe
                    {
                        Pos = npc.Center,
                        Vel = npc.velocity,
                        HalfW = npc.width * 0.5f,
                        HalfH = npc.height * 0.5f,
                        Boss = npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type],
                        Seen = Collision.CanHitLine(copy.position, copy.width, copy.height, npc.position, npc.width, npc.height),
                        Index = npc.whoAmI,
                        TargetsMe = npc.target == copy.whoAmI,
                    };
                }
            }

            s.ThreatCount = n;
            s.FoeCount = f;
            return s;
        }

        /// <summary>What the potion it would drink heals, as the game works it out (Player.GetHealLife); a Super Healing Potion's if it has none.</summary>
        private static float Heal(Player copy)
        {
            Item potion = copy.QuickHeal_GetItemToUse();
            return potion != null ? copy.GetHealLife(potion, true) : 200f;
        }

        /// <summary>How much a hit matters: a fifth at the least, and two shares of the life it would take.</summary>
        private static float Weight(int damage, float lifeMax) => 0.2f + 2f * Math.Max(1, damage) / lifeMax;

        /// <summary>
        /// Its body and how it can move, as the game has it (Player.maxRunSpeed, accRunSpeed, runAcceleration,
        /// runSlowdown, gravity, maxFallSpeed; the jump as the game set it for the copy on its last tick; the wings'
        /// time and their numbers in Player.WingMovement, where the game gives some wings their own).
        /// </summary>
        private static Body Body(Player copy, float jumpSpeed, int jumpTicks)
        {
            var b = new Body
            {
                Pos = copy.Center,
                Vel = copy.velocity,
                HalfW = copy.width * 0.5f,
                HalfH = copy.height * 0.5f,
                Facing = copy.direction,
                RunBase = copy.maxRunSpeed,
                RunTop = Math.Max(copy.maxRunSpeed, copy.accRunSpeed),
                RunAccel = copy.runAcceleration,
                RunSlow = copy.runSlowdown,
                JumpSpeed = jumpSpeed,
                JumpTicks = jumpTicks,
                JumpLeft = copy.jump,
                Gravity = copy.gravity,
                MaxFall = copy.maxFallSpeed,
                Grounded = copy.velocity.Y == 0f,
                Immune = copy.immune ? copy.immuneTime : 0,
            };

            // Wings once flight is earned; and the Empress's favour, which lends the solar wings' flight before it is
            // (Buffs.FavorOfAnEmpress: PisPlayer runs it for the copy as for anyone with the buff).
            bool favour = copy.HasBuff(ModContent.BuffType<Buffs.FavorOfAnEmpress>());
            bool wings = copy.wingsLogic > 0 && copy.wingTimeMax > 0 && (copy.PIS().AllowedWings || favour);

            if (wings)
            {
                b.CanFly = true;
                b.FlyLeft = (int)copy.wingTime;
                b.FlyMax = copy.wingTimeMax;

                WingStats stats = ArmorIDs.Wing.Sets.Stats[copy.wingsLogic];
                b.FlySpeed = stats.AccRunSpeedOverride > 0f ? stats.AccRunSpeedOverride : b.RunTop;

                // The game's own (Player.WingMovement), and the stronger ones it gives a few.
                b.ConstantAscend = 0.1f;
                b.AscentFalling = 0.5f;
                b.AscentRising = 0.1f;
                b.MaxCanAscend = 0.5f;
                b.MaxAscent = 1.5f;

                switch (copy.wingsLogic)
                {
                    case 26:
                    case 37:
                        b.AscentFalling = 0.75f; b.AscentRising = 0.15f; b.MaxCanAscend = 1f; b.MaxAscent = 2.5f; b.ConstantAscend = 0.125f;
                        break;
                    case 44:
                        b.AscentFalling = 0.85f; b.AscentRising = 0.15f; b.MaxCanAscend = 1f; b.MaxAscent = 2.75f; b.ConstantAscend = 0.125f;
                        break;
                    case 45:
                        b.AscentFalling = 0.95f; b.AscentRising = 0.15f; b.MaxCanAscend = 1f; b.MaxAscent = 4.5f;
                        break;
                    case 29:
                    case 32:
                        b.AscentFalling = 0.85f; b.AscentRising = 0.15f; b.MaxCanAscend = 1f; b.MaxAscent = 3f; b.ConstantAscend = 0.135f;
                        break;
                    case 30:
                    case 31:
                        b.MaxCanAscend = 1f; b.MaxAscent = 2.45f; b.ConstantAscend = 0.15f;
                        break;
                }

                // The mod's Queen Slime's Wings climb as the game's 44 do.
                if (copy.wingsLogic == ContentSamples.ItemsByType[ModContent.ItemType<Items.Accessories.QueenSlimesWings>()].wingSlot)
                {
                    b.AscentFalling = 0.85f; b.AscentRising = 0.15f; b.MaxCanAscend = 1f; b.MaxAscent = 2.75f; b.ConstantAscend = 0.125f;
                }
            }

            b.CanDash = copy.dashType > 0 && copy.dashDelay == 0 && !copy.mount.Active;
            b.DashSpeed = copy.dashType == 3 ? 21f : 14.5f;

            // Its jumps in the air (bottles, the jar, the mod's bottle of three): how many it has, how many are left
            // before it lands, and how long the longest is held (the game's jump, times the jump's own share).
            float longest = 0f;

            foreach (ExtraJump jump in ExtraJumpLoader.OrderedJumps)
            {
                ref ExtraJumpState state = ref copy.GetJumpState(jump);

                if (!state.Enabled)
                    continue;

                int uses = jump is Items.Accessories.TripleFartJump ? Items.Accessories.TripleFartJump.Farts : 1;
                b.AirJumps += uses;

                if (state.Available)
                    b.AirJumpsLeft += jump is Items.Accessories.TripleFartJump ? Math.Max(1, copy.GetModPlayer<Items.Accessories.TripleFartPlayer>().Left) : 1;

                longest = Math.Max(longest, jump.GetDurationMultiplier(copy));
            }

            b.AirJumpTicks = (int)(jumpTicks * longest);
            return b;
        }

        /// <summary>
        /// The weapon as far as fighting goes: what kind (a blade swung, a spear, a yoyo, a whip, a summon weapon, a
        /// gun or a spell), how far it reaches, how fast its shot flies (and so how far ahead to aim), whether it has a
        /// right click, and whether its shots pass through blocks.
        /// </summary>
        public static Weapon WeaponOf(Player copy)
        {
            Weapon w = ReadWeapon(copy);
            w.Cluster = MimicWeapons.ClusterOf(copy.HeldItem, w);
            return w;
        }

        private static Weapon ReadWeapon(Player copy)
        {
            Item item = copy.HeldItem;
            var w = new Weapon { Kind = WeaponKind.Melee, Reach = 64f };

            if (item == null || item.IsAir)
                return w;

            w.HasAlt = ItemLoader.AltFunctionUse(item, copy);
            int shoot = item.shoot;
            Projectile sample = shoot > 0 && shoot < ProjectileLoader.ProjectileCount ? ContentSamples.ProjectilesByType[shoot] : null;

            w.Id = item.type;
            w.ManaCost = item.mana > 0 ? copy.GetManaCost(item) : 0f;

            if (item.ModItem is Catalyst catalyst)
            {
                // A spell through a catalyst: its own numbers (Catalyst.Apply puts them on the item each tick, its mana
                // among them), its shot's reach as the shot is made, and what is learned kept for the spell.
                Castable cast = catalyst.Selected(copy);
                w.Kind = WeaponKind.Magic;
                w.Speed = cast?.Speed ?? 10f;
                w.Reach = 520f;

                if (cast != null)
                {
                    w.Id = cast.Type;
                    Projectile shot = cast.Shot > 0 && cast.Shot < ProjectileLoader.ProjectileCount ? ContentSamples.ProjectilesByType[cast.Shot] : null;

                    if (shot?.ModProjectile is Projectiles.Magic.DancingSandGust)
                    {
                        // A spray from the staff's head, thirty tiles long, quicker the nearer.
                        w.Reach = Projectiles.Magic.DancingSandGust.Reach;
                        w.Speed = 0f;
                    }
                    else if (shot != null)
                    {
                        w.Speed = cast.Speed * (1 + shot.extraUpdates);
                        w.Reach = w.Speed > 0.5f ? MathHelper.Clamp(w.Speed * Math.Min(shot.timeLeft, 120), 160f, 960f) : 520f;
                        w.ThroughWalls = !shot.tileCollide;
                    }
                }

                return w;
            }

            if (sample != null && ProjectileID.Sets.IsAWhip[shoot])
            {
                w.Kind = WeaponKind.Whip;
                w.Reach = 200f * Math.Max(0.5f, item.shootSpeed / 4f) * 0.8f;
                w.ThroughWalls = true;
                return w;
            }

            if (ItemID.Sets.Yoyo[item.type])
            {
                w.Kind = WeaponKind.Yoyo;
                float range = sample != null ? ProjectileID.Sets.YoyosMaximumRange[shoot] : 200f;
                w.Reach = range > 0f ? range * 0.9f : 200f;
                return w;
            }

            if (ItemID.Sets.Spears[item.type] || sample != null && sample.aiStyle == 19)
            {
                w.Kind = WeaponKind.Spear;
                w.Reach = 60f + item.shootSpeed * 22f;
                return w;
            }

            if (item.CountsAsClass(DamageClass.Summon) && sample != null && (sample.minion || sample.sentry || ProjectileID.Sets.MinionTargettingFeature[shoot]))
            {
                w.Kind = WeaponKind.Summon;
                w.Reach = 800f;
                w.ThroughWalls = true;
                return w;
            }

            // A flail: thrown out on its chain and back, a reach of its own.
            if (sample != null && sample.aiStyle == 15)
            {
                w.Kind = WeaponKind.Yoyo;
                w.Reach = MathHelper.Clamp(200f + item.shootSpeed * 8f, 120f, 400f);
                return w;
            }

            // Blades whose hit is a shot made about the holder: a sword's slash (Night's Edge, Excalibur), a
            // shortsword's stab, a held blade, drill or spinner (Starlight, the mods' held swords). Up close, all of them.
            bool melee = item.CountsAsClass(DamageClass.Melee);

            if (sample != null && melee && sample.aiStyle == 190)
            {
                w.Reach = Math.Max(MeleeReach(item), 64f) + 16f;
                return w;
            }

            if (sample != null && melee && sample.aiStyle == 161)
            {
                w.Reach = 56f;
                return w;
            }

            if (sample != null && melee && (sample.aiStyle == 20 || sample.aiStyle == 75 || item.channel && item.noUseGraphic))
            {
                w.Reach = MathHelper.Clamp(Math.Max(sample.width, sample.height) * sample.scale * 0.6f + 40f, 64f, 260f);
                return w;
            }

            if (sample != null && item.shootSpeed > 0f)
            {
                w.Kind = item.CountsAsClass(DamageClass.Magic) ? WeaponKind.Magic : WeaponKind.Ranged;
                w.Speed = item.shootSpeed * (1 + sample.extraUpdates);
                w.Reach = MathHelper.Clamp(w.Speed * Math.Min(sample.timeLeft, 120), 160f, 960f);
                w.ThroughWalls = !sample.tileCollide;

                // A blade that also throws something (Terra Blade, Night's Edge) is still swung up close.
                if (!item.noMelee && item.useStyle == ItemUseStyleID.Swing)
                {
                    w.Kind = WeaponKind.Melee;
                    w.Reach = Math.Max(MeleeReach(item), 64f);
                    w.Speed = 0f;
                }

                return w;
            }

            w.Kind = WeaponKind.Melee;
            w.Reach = MeleeReach(item);
            return w;
        }

        /// <summary>A blade's reach from the holder's middle: its own length at its scale, and a little.</summary>
        private static float MeleeReach(Item item) => Math.Max(item.width, item.height) * item.scale * 1.15f + 12f;

        /// <summary>The blocks for the copy's movement: solid tiles, and the tops of platforms.</summary>
        private sealed class WorldGround : IGround
        {
            /// <summary>The Empress's walls hold the copy (MimicPlayer): the tiles outside her decagon are as solid to it.</summary>
            public bool Walled;

            public bool Solid(int x, int y) => !WorldGen.InWorld(x, y, 2) || WorldGen.SolidTile(x, y)
                || Walled && !NPCs.Empress.Decagon.Inside(new Vector2(x * 16 + 8, y * 16 + 8), NPCs.Empress.EmpressArena.Centre, NPCs.Empress.EmpressArena.Size);

            public bool Platform(int x, int y)
            {
                if (!WorldGen.InWorld(x, y, 2))
                    return false;

                Tile tile = Main.tile[x, y];
                return tile.HasTile && !tile.IsActuated && Main.tileSolidTop[tile.TileType] && !Main.tileSolid[tile.TileType]
                    || tile.HasTile && TileID.Sets.Platforms[tile.TileType];
            }
        }
    }
}
