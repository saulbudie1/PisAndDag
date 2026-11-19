using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// What the Legends Skeletron Prime throws (Prime.Legends, Prime.Moves, PrimeArm.Legends, PrimeArm.Group). The server
    /// makes them; every machine sees them.
    ///
    ///   (vanilla's own)   its laser's death lasers, where they are the Legends Prime's (PrimeVanillaShots): its green
    ///                     (by request: "shoot vanilla normally but green laser")
    ///   PrimeBeam         a thin column of laser held on by what fires it: its laser's from the corner, its head's eyes'
    ///   PrimeColumn       its laser's special: three tiles wide, four hundred a hit (by request), in any world
    ///   PrimeMissile      its cannon's: a mini missile that homes loosely (by request: "player can break the missle
    ///                     ankles": it turns only so fast, and can be stepped inside of)
    ///   PrimeBomb         a bomb let fall, or thrown: it goes up on what it hits
    ///   PrimeNuke         its cannon's special: shot up black, coming down under a parachute, going up on its fuse
    ///   PrimeBurst        whatever of these goes up: a nuclear fire as far as it reaches, hurting for a moment
    ///   PrimeShockwave    its arms' clash's ("creating supersonic wave")
    ///   PrimeClod         earth its saw scoops up and throws
    ///
    /// Damage is as vanilla counts a boss's shots (doubled in a normal world, four times in Expert, six in Master): its
    /// laser's 25 (vanilla's), a thin beam 30/27, its eyes' 45/40, a missile 30/27, a bomb 40/36, a nuke 60/54, its head's
    /// own going up 60/54, the shockwave 46/40 at its heart (a sixth of that at its end), a clod 22/20. The column alone
    /// is not counted so: four hundred, whatever the world.
    /// </summary>
    public static class PrimeShots
    {
        private static bool Server => Main.netMode != NetmodeID.MultiplayerClient;

        private static int Counted(NPC npc, float normal, float expert) => npc.GetAttackDamage_ForProjectiles(normal, expert);

        private static int New<T>(NPC owner, Vector2 at, Vector2 velocity, int damage, float a = 0f, float b = 0f, float c = 0f) where T : ModProjectile =>
            Projectile.NewProjectile(owner.GetSource_FromAI(), at, velocity, ModContent.ProjectileType<T>(), damage, 0f, Main.myPlayer, a, b, c);

        /// <summary>One of vanilla's death lasers (made the Legends Prime's as it is born: PrimeVanillaShots). Twenty-five, as vanilla's own.</summary>
        public static void Laser(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                Projectile.NewProjectile(owner.GetSource_FromAI(), at, velocity, ProjectileID.DeathLaser, 25, 0f, Main.myPlayer);
        }

        public static void Missile(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                New<PrimeMissile>(owner, at, velocity, Counted(owner, 30f, 27f));
        }

        public static void Bomb(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                New<PrimeBomb>(owner, at, velocity, Counted(owner, 40f, 36f));
        }

        /// <summary>A nuke shot up from a place, to be at <paramref name="dropAt"/>, under its parachute, as its fuse runs out.</summary>
        public static void Nuke(NPC owner, Vector2 from, Vector2 dropAt, int fuse)
        {
            if (Server)
                New<PrimeNuke>(owner, from, new Vector2(0f, -PrimeNuke.RiseSpeed), Counted(owner, 60f, 54f), fuse, dropAt.X, dropAt.Y);
        }

        /// <summary>A thin column out of an arm's tool (<paramref name="eye"/> 0), or one of its head's eyes (-1, 1), for as long as that keeps it on.</summary>
        public static void Beam(NPC owner, int life, int eye = 0)
        {
            if (Server)
                New<PrimeBeam>(owner, owner.Center, Vector2.Zero, eye == 0 ? Counted(owner, 30f, 27f) : Counted(owner, 45f, 40f), owner.whoAmI, life, eye);
        }

        /// <summary>Its laser's special: four hundred a hit (the game doubles a hostile shot's number; this one is not scaled by the world).</summary>
        public static void Column(NPC owner, int life)
        {
            if (Server)
                New<PrimeColumn>(owner, owner.Center, Vector2.Zero, PrimeColumn.Hit / 2, owner.whoAmI, life);
        }

        public static void Shockwave(NPC owner, Vector2 at)
        {
            if (Server)
                New<PrimeShockwave>(owner, at, Vector2.Zero, Counted(owner, 46f, 40f));
        }

        public static void Clod(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                New<PrimeClod>(owner, at, velocity, Counted(owner, 22f, 20f));
        }

        /// <summary>Its head going up where it is (Prime.Moves): as far as <paramref name="reach"/>.</summary>
        public static void Detonation(NPC owner, Vector2 at, float reach)
        {
            if (Server)
                New<PrimeBurst>(owner, at, Vector2.Zero, Counted(owner, 60f, 54f), reach, PrimeBurst.Great);
        }

        /// <summary>Its head meeting the ground (Prime.Moves): what is by it is thrown and burnt.</summary>
        public static void Slam(NPC owner, Vector2 at)
        {
            if (Server)
                New<PrimeBurst>(owner, at, Vector2.Zero, Counted(owner, 50f, 45f), PrimeBurst.SlamReach, PrimeBurst.Small);
        }

        /// <summary>Something of its own going up (a missile, a bomb, a nuke): the server's, from what went.</summary>
        internal static void Burst(Projectile from, float reach, int kind)
        {
            if (Server)
                Projectile.NewProjectile(from.GetSource_FromThis(), from.Center, Vector2.Zero, ModContent.ProjectileType<PrimeBurst>(), from.damage, 0f, Main.myPlayer, reach, kind);
        }

        /// <summary>Whether a projectile is one of these.</summary>
        public static bool IsOurs(int type) =>
            type == ModContent.ProjectileType<PrimeBeam>() || type == ModContent.ProjectileType<PrimeColumn>() || type == ModContent.ProjectileType<PrimeMissile>()
            || type == ModContent.ProjectileType<PrimeBomb>() || type == ModContent.ProjectileType<PrimeNuke>() || type == ModContent.ProjectileType<PrimeShockwave>()
            || type == ModContent.ProjectileType<PrimeClod>();

        /// <summary>What it has in flight, gone (its change, its end): the server's. A fire already going up is left to.</summary>
        public static void Clear()
        {
            if (!Server)
                return;

            foreach (Projectile shot in Main.ActiveProjectiles)
            {
                if (shot.hostile && IsOurs(shot.type))
                {
                    if (shot.ModProjectile is PrimeQuiet quiet)
                        quiet.Quiet = true;

                    shot.Kill();
                }
            }
        }

        // ---- drawn ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A column of its laser, smooth, straight into the world's batch: a wide haze of its green, a body, a white-hot
        /// heart, a hard edge down each side, brighter knots running out along it; a flare where it leaves. Fading out
        /// along its last fifth.
        /// </summary>
        internal static void DrawBeam(SpriteBatch batch, Vector2 from, Vector2 way, float reach, float wide, float strength)
        {
            if (strength <= 0.01f)
                return;

            float time = Main.GlobalTimeWrappedHourly;
            float flick = 0.88f + 0.12f * MathF.Sin(time * 47f);
            Vector2 side = new(-way.Y, way.X);
            float turn = way.ToRotation();

            Fading(batch, from, way, reach, wide * 2.7f, PrimeArt.NuclearDeep * (0.3f * strength));
            Fading(batch, from, way, reach, wide * 1.55f, PrimeArt.Nuclear * (0.38f * strength * flick));
            Fading(batch, from, way, reach, wide * 0.92f, Color.Lerp(PrimeArt.Nuclear, PrimeArt.NuclearHot, 0.45f) * (0.7f * strength * flick));
            Fading(batch, from, way, reach, wide * 0.4f, Color.Lerp(PrimeArt.NuclearHot, Color.White, 0.6f) * strength);

            // Its edges: what makes it a column and not a glow.
            for (int k = -1; k <= 1; k += 2)
                Fading(batch, from + side * k * wide * 0.47f, way, reach, 3.5f, PrimeArt.NuclearHot * (0.55f * strength));

            // Knots of light running out along it.
            const float gap = 230f, speed = 1500f;
            float start = time * speed % gap;

            for (float along = start; along < reach * 0.82f; along += gap)
                DestroyerArt.DrawOval(batch, from + way * along, turn, 190f, wide * 0.95f, PrimeArt.Lit(PrimeArt.NuclearHot * (0.26f * strength)));

            DestroyerArt.DrawGlow(batch, from, wide * 3.6f, PrimeArt.Lit(PrimeArt.Nuclear * (0.62f * strength)), hotCore: true);
            DestroyerArt.DrawGlow(batch, from, wide * 1.7f * flick, PrimeArt.Lit(Color.White * (0.8f * strength)), hotCore: true);
        }

        private static void Fading(SpriteBatch batch, Vector2 from, Vector2 way, float reach, float wide, Color colour)
        {
            float full = reach * 0.8f;
            DestroyerArt.DrawLine(batch, from, from + way * full, wide, PrimeArt.Lit(colour));

            const int steps = 8;

            for (int i = 0; i < steps; i++)
            {
                float a = full + (reach - full) * i / steps, b = full + (reach - full) * (i + 1) / steps;
                DestroyerArt.DrawLine(batch, from + way * a, from + way * b, wide, PrimeArt.Lit(colour * (1f - (i + 0.5f) / steps)));
            }
        }
    }

    /// <summary>One of its shots that can be put out without going up (its change, its end: PrimeShots.Clear).</summary>
    public abstract class PrimeQuiet : ModProjectile
    {
        /// <summary>Put out, not set off: it goes without its fire.</summary>
        internal bool Quiet;

        /// <summary>
        /// On a server: it is on a player. What it does to them is their own machine's to deal (and there it ends on
        /// them); the server's own goes on unless it is ended here too.
        /// </summary>
        protected bool OnAPlayer()
        {
            if (Main.netMode != NetmodeID.Server)
                return false;

            Rectangle box = Projectile.Hitbox;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];

                if (p.active && !p.dead && !p.ghost && box.Intersects(p.Hitbox))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Vanilla's death laser, where it is the Legends Skeletron Prime's (known as it is born, from what made it, and told
    /// to every machine): its green (by request), a spark of it left behind now and then.
    /// </summary>
    public class PrimeVanillaShots : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.DeathLaser;

        private bool ours;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!PADWorld.Legends || NPC.IsMechQueenUp || source is not EntitySource_Parent { Entity: NPC npc })
                return;

            ours = npc.type == NPCID.PrimeLaser || npc.type == NPCID.SkeletronPrime;
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter) => binaryWriter.Write(ours);

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) => ours = binaryReader.ReadBoolean();

        public override void PostAI(Projectile projectile)
        {
            // Once a tick (it moves several times in one).
            if (!ours || Main.dedServ || projectile.numUpdates != -1)
                return;

            Lighting.AddLight(projectile.Center, 0.5f, 0.72f, 0.02f);

            if (Main.rand.NextBool(5) && Prime.Seen(projectile.Center, 40f))
                PADFire.Spark(projectile.Center, -projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1.4f, 1.4f), Main.rand.Next(10, 18), FirePalette.Nuclear);
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (!ours)
                return true;

            // A bolt of its green in place of the laser's own picture.
            Vector2 at = projectile.Center;
            Vector2 way = projectile.velocity.SafeNormalize(Vector2.UnitY);
            SpriteBatch batch = Main.spriteBatch;
            DestroyerArt.DrawLine(batch, at - way * 62f, at + way * 10f, 16f, PrimeArt.Lit(PrimeArt.Nuclear * 0.5f));
            DestroyerArt.DrawLine(batch, at - way * 44f, at + way * 8f, 6f, PrimeArt.Lit(PrimeArt.NuclearHot));
            DestroyerArt.DrawGlow(batch, at + way * 4f, 22f, PrimeArt.Lit(Color.White * 0.6f), hotCore: true);
            return false;
        }
    }

    // ---- its beams ------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A column of laser held on by what fires it (an arm's tool, one of its head's eyes): it is where that is and points
    /// as that points, for as long as that keeps it on (Prime.BeamSource), or its time runs out.
    /// </summary>
    public abstract class PrimeBeamBase : PrimeQuiet
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        protected int Owner => (int)Projectile.ai[0];

        protected int Life => Math.Max(2, (int)Projectile.ai[1]);

        protected int Eye => (int)Projectile.ai[2];

        protected ref float Age => ref Projectile.localAI[0];

        protected abstract float Wide { get; }

        protected abstract float Reach { get; }

        /// <summary>Where it leaves, the way it goes, and whether what fires it is firing on this machine yet.</summary>
        private Vector2 from, way = Vector2.UnitY;
        private bool live;

        /// <summary>In over four ticks, out over eight.</summary>
        private float Strength => Math.Min(1f, Age / 4f) * Math.Min(1f, (Life - Age + 1f) / 8f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3600;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 900;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            NPC owner = Owner >= 0 && Owner < Main.maxNPCs ? Main.npc[Owner] : null;
            live = owner != null && owner.active && Prime.BeamSource(owner, Eye, out from, out way);

            if (!live)
            {
                // What fired it has stopped (or, on a client, word of it is not here yet): the server's to end.
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.Kill();

                return;
            }

            Age++;

            if (Age > Life)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = from;
            Projectile.timeLeft = 10;

            if (!Main.dedServ)
                Burn(Strength);
        }

        /// <summary>What it does to the air and the ground: its light along it, sparks off it, and where it first meets the ground, fire and smoke.</summary>
        private void Burn(float strength)
        {
            float wide = Wide, reach = Reach;
            Vector2 side = new(-way.Y, way.X);

            for (float k = 0f; k < reach; k += 130f)
            {
                Vector2 at = from + way * k;

                if (PADFire.Near(at, 140f))
                    Lighting.AddLight(at, 0.9f * strength, 1.3f * strength, 0.06f * strength);
            }

            // Sparks thrown off its sides, a few a tick, wherever it is in sight.
            for (int n = 0; n < 3; n++)
            {
                Vector2 at = from + way * Main.rand.NextFloat(reach * 0.7f);

                if (!Prime.Seen(at, 30f) || Main.rand.NextFloat() > strength)
                    continue;

                float k = Main.rand.NextBool() ? 1f : -1f;
                PADFire.Spark(at + side * k * wide * 0.45f, side * k * Main.rand.NextFloat(1f, 4f) + way * Main.rand.NextFloat(1f, 5f), Main.rand.Next(12, 24), FirePalette.Nuclear, 0.01f);
            }

            // Where it meets the ground: it burns there.
            for (float k = 24f; k < reach; k += 16f)
            {
                Vector2 at = from + way * k;

                if (!WorldGen.InWorld((int)(at.X / 16f), (int)(at.Y / 16f), 4))
                    break;

                Tile tile = Main.tile[(int)(at.X / 16f), (int)(at.Y / 16f)];

                if (!tile.HasTile || tile.IsActuated || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
                    continue;

                if (Prime.Seen(at, 80f))
                {
                    PADFire.Burn(at - way * 10f, wide * 0.5f, 0.7f * strength, FirePalette.Nuclear, smokes: Main.rand.NextBool(3));

                    if (Main.rand.NextBool(2))
                        PADParticleHandler.SpawnParticle(new SparkShard(at - way * 8f, (-way).RotatedByRandom(1.1f) * Main.rand.NextFloat(4f, 11f), PrimeArt.NuclearHot, 12f, 3f, 36f,
                            Main.rand.Next(10, 18), falls: true));

                    Lighting.AddLight(at, 1.2f * strength, 1.7f * strength, 0.1f * strength);
                }

                break;
            }
        }

        public override bool CanHitPlayer(Player target) => live && Age >= 3f && Age <= Life - 2f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!live)
                return false;

            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, from + way * Reach, Wide * 0.8f, ref along);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (live)
                PrimeShots.DrawBeam(Main.spriteBatch, from, way, Reach, Wide, Strength);

            return false;
        }
    }

    public class PrimeBeam : PrimeBeamBase
    {
        /// <summary>An arm's thin column, and one of its head's eyes'.</summary>
        public const float ArmWide = 26f, EyeWide = 30f;
        public const float ArmReach = 1500f, EyeReach = 2300f;

        protected override float Wide => Eye == 0 ? ArmWide : EyeWide;

        protected override float Reach => Eye == 0 ? ArmReach : EyeReach;
    }

    public class PrimeColumn : PrimeBeamBase
    {
        /// <summary>What it hits for (by request: "400 damage"), three tiles wide (by request), and how far it goes.</summary>
        public const int Hit = 400;
        public const float ColumnWide = 48f;
        public const float ColumnReach = 2600f;

        protected override float Wide => ColumnWide;

        protected override float Reach => ColumnReach;

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            // Four hundred in any world: not doubled again in Expert, nor trebled in Master.
            ProjectileID.Sets.PlayerHurtDamageIgnoresDifficultyScaling[Type] = true;
        }
    }

    // ---- its cannon's ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A mini missile (by request: "Shoots mini missles that loosely home onto player and player can break the missle
    /// ankles"). Out of the cannon the way it points, then after the nearest player, turning only so fast: it cannot
    /// follow a player who steps inside its turn, and swings wide to come round again. Its fuel runs out; then it falls.
    /// </summary>
    public class PrimeMissile : PrimeQuiet
    {
        private const int Straight = 14, Fuel = 230, Lasts = 330;
        private const float Fastest = 10.5f;

        /// <summary>The most it can turn in a tick: at its speed, a circle some three hundred pixels across its half.</summary>
        private const float Turns = 0.034f;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lasts;
        }

        public override void AI()
        {
            Age++;
            float speed = Projectile.velocity.Length();

            if (Age <= Fuel)
            {
                speed = Math.Min(Fastest, speed + 0.22f);
                Vector2 way = Projectile.velocity.SafeNormalize(-Vector2.UnitY);

                if (Age > Straight)
                {
                    int who = Player.FindClosest(Projectile.Center, 1, 1);
                    Player p = Main.player[who];

                    if (p.active && !p.dead)
                    {
                        float want = (p.Center - Projectile.Center).ToRotation();
                        float off = MathHelper.WrapAngle(want - way.ToRotation());
                        way = way.RotatedBy(MathHelper.Clamp(off, -Turns, Turns));
                    }
                }

                Projectile.velocity = way * speed;
            }
            else
            {
                // Out of fuel: it noses over and falls.
                Projectile.velocity.X *= 0.99f;
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.22f, 13f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (OnAPlayer())
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            if (Age == 1f)
                LaunchFx();

            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 tail = Projectile.Center + back * 13f;

            if (Age <= Fuel)
            {
                PADFire.Flame(tail, back * 2.2f + Projectile.velocity * 0.25f, Main.rand.NextFloat(9f, 14f), Main.rand.Next(6, 11), FirePalette.Nuclear, 0f, 0.6f);
                Lighting.AddLight(tail, 0.4f, 0.56f, 0.02f);
            }

            if ((int)Age % 2 == 0 && Prime.Seen(tail, 40f))
                Prime.Add(new MuzzleSmoke(tail, back * Main.rand.NextFloat(0.3f, 1f) + Main.rand.NextVector2Circular(0.3f, 0.3f), Main.rand.Next(22, 36), Main.rand.NextFloat(0.2f, 0.34f)));
        }

        /// <summary>It leaves the cannon: its flash, smoke blown back, the cough of it.</summary>
        private void LaunchFx()
        {
            Vector2 at = Projectile.Center, way = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
            Prime.Sound(SoundID.Item61 with { Volume = 0.7f, Pitch = 0.2f, PitchVariance = 0.15f, MaxInstances = 6 }, at, 6);

            if (!Prime.Seen(at, 200f))
                return;

            Prime.Add(new BloomGeneric(at, Vector2.Zero, new Color(255, 200, 120) * 0.9f, 0.5f, 8));

            for (int i = 0; i < 4; i++)
                Prime.Add(new MuzzleSmoke(at, way.RotatedByRandom(0.7f) * Main.rand.NextFloat(0.5f, 2.4f), Main.rand.Next(24, 40), Main.rand.NextFloat(0.26f, 0.42f)));

            Lighting.AddLight(at, 1f, 0.7f, 0.3f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => Projectile.Kill();

        public override void OnKill(int timeLeft)
        {
            if (!Quiet)
                PrimeShots.Burst(Projectile, PrimeBurst.MissileReach, PrimeBurst.Small);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Color lit = Color.Lerp(lightColor, Color.White, 0.35f);
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, lit, Projectile.rotation, texture.Size() * 0.5f, 1f, SpriteEffects.None, 0f);

            if (Age <= Fuel)
            {
                Vector2 tail = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitY) * 14f;
                DestroyerArt.DrawGlow(Main.spriteBatch, tail, 26f, PrimeArt.Lit(PrimeArt.Nuclear * 0.5f), hotCore: true);
            }

            return false;
        }
    }

    /// <summary>A bomb let fall from its cannon, or thrown by it: it goes up on the ground, or on a player.</summary>
    public class PrimeBomb : PrimeQuiet
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BombSkeletronPrime;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 420;
        }

        public override void AI()
        {
            Age++;
            Projectile.velocity.X *= 0.995f;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 15f);
            Projectile.rotation += 0.05f + Projectile.velocity.X * 0.02f;

            if (OnAPlayer())
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            if ((int)Age % 3 == 0 && Prime.Seen(Projectile.Center, 40f))
                Prime.Add(new MuzzleSmoke(Projectile.Center, -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.3f, 0.3f), Main.rand.Next(20, 32), Main.rand.NextFloat(0.2f, 0.3f)));

            Lighting.AddLight(Projectile.Center, 0.24f, 0.34f, 0.01f);
        }

        // Through platforms: it falls to the ground under them.
        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
        {
            fallThrough = true;
            return true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => Projectile.Kill();

        public override void OnKill(int timeLeft)
        {
            if (!Quiet)
                PrimeShots.Burst(Projectile, PrimeBurst.BombReach, PrimeBurst.Small);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Main.instance.LoadProjectile(ProjectileID.BombSkeletronPrime);
            Texture2D texture = TextureAssets.Projectile[ProjectileID.BombSkeletronPrime].Value;
            SpriteBatch batch = Main.spriteBatch;
            Vector2 at = Projectile.Center;
            batch.Draw(texture, at - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.3f), Projectile.rotation, texture.Size() * 0.5f, 1f, SpriteEffects.None, 0f);

            // Its light, blinking its green.
            float blink = (int)Age / 5 % 2 == 0 ? 1f : 0.35f;
            DestroyerArt.DrawGlow(batch, at, 30f, PrimeArt.Lit(PrimeArt.Nuclear * (0.45f * blink)));
            DestroyerArt.DrawGlow(batch, at, 10f, PrimeArt.Lit(PrimeArt.NuclearHot * (0.8f * blink)), hotCore: true);
            return false;
        }
    }

    /// <summary>
    /// Its cannon's special (by request: "shoot 3 of something black onto the sky in succession, and its falling down
    /// Nukes with parachute, the light sparks on the hand and changes to green and all those nukes explodes leaving a
    /// radiactive area radius 10 tiles"). Shot up black and gone off the top of the screen; then down under a parachute,
    /// slow, swinging, to be at its place as its fuse runs out: all of a volley's on the same tick (the tick the cannon's
    /// lamp has gone green). Its lamp blinks red, then green and quick. Where it goes up, the ground is radioactive for
    /// thirty seconds (PrimeFallout).
    /// </summary>
    public class PrimeNuke : PrimeQuiet
    {
        /// <summary>
        /// Ticks it climbs out of the cannon, and how fast; how far over its place it comes back into sight (over the
        /// top of the screen: each comes down the whole of that in what is left of its fuse, so all of a volley land
        /// together, the later ones a little quicker).
        /// </summary>
        public const int RiseTicks = 34;
        public const float RiseSpeed = 27f;
        public const float FallFrom = 760f;

        /// <summary>How far its fire reaches, and the ground it leaves (by request: ten tiles).</summary>
        public const float BlastReach = 10f * 16f;

        /// <summary>Its last ticks: its lamp green, and quick.</summary>
        private const int Armed = 56;

        private int Fuse => Math.Max(RiseTicks + 2, (int)Projectile.ai[0]);

        private Vector2 DropAt => new(Projectile.ai[1], Projectile.ai[2]);

        private ref float Age => ref Projectile.localAI[0];

        private bool Falling => Age > RiseTicks;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 900;
        }

        public override bool CanHitPlayer(Player target) => false;

        public override bool ShouldUpdatePosition() => !Falling;

        public override void AI()
        {
            Age++;
            int left = Fuse - (int)Age;

            if (left <= 0)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.Kill();

                return;
            }

            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 4);

            if (!Falling)
            {
                // Up and gone.
                Projectile.velocity = new Vector2(0f, -RiseSpeed);
                Projectile.rotation = MathHelper.Pi;

                if (!Main.dedServ && Prime.Seen(Projectile.Center, 60f))
                {
                    for (int i = 0; i < 2; i++)
                        Prime.Add(new MuzzleSmoke(Projectile.Center + new Vector2(0f, 14f + 12f * i), new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.5f, 2f)),
                            Main.rand.Next(28, 44), Main.rand.NextFloat(0.3f, 0.5f)));
                }

                return;
            }

            // Down under its parachute: where it must be for its fuse, swinging.
            float swing = MathF.Sin(Age * 0.045f + Projectile.whoAmI * 1.7f);
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = DropAt + new Vector2(18f * swing, -FallFrom * left / Math.Max(1f, Fuse - RiseTicks));
            Projectile.rotation = 0.14f * MathF.Cos(Age * 0.045f + Projectile.whoAmI * 1.7f);

            if (Main.dedServ)
                return;

            bool armed = left <= Armed;
            int every = armed ? 6 : 30;

            if (left % every == 0 && Prime.Seen(Projectile.Center, 200f))
                Prime.Sound(Prime.BeepSound with { Volume = armed ? 0.7f : 0.35f, Pitch = armed ? 0.5f : -0.1f }, Projectile.Center, 6);

            if (left == Armed && Prime.Seen(Projectile.Center, 200f))
            {
                Prime.Add(new BloomGeneric(LampAt, Vector2.Zero, PrimeArt.Nuclear * 0.9f, 0.5f, 14));
                Prime.Arc(LampAt, LampAt + Main.rand.NextVector2Circular(26f, 22f), PrimeArt.Nuclear, 8f, 12, 1);
            }

            Color c = armed ? PrimeArt.Nuclear : PrimeArt.Red;
            float lit = Blink(left, armed) * 0.4f;
            Lighting.AddLight(Projectile.Center, c.R / 255f * lit, c.G / 255f * lit, c.B / 255f * lit);
        }

        private Vector2 LampAt => Projectile.Center + new Vector2(0f, -4f).RotatedBy(Projectile.rotation);

        private static float Blink(int left, bool armed) => left % (armed ? 6 : 30) < (armed ? 3 : 8) ? 1f : 0.12f;

        public override void OnKill(int timeLeft)
        {
            if (Quiet || !Falling)
                return;

            PrimeShots.Burst(Projectile, BlastReach, PrimeBurst.Nuke);
            PrimeFallout.Add(Projectile.Center, BlastReach, PrimeFallout.ShortLasts);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D body = TextureAssets.Projectile[Type].Value;
            SpriteBatch batch = Main.spriteBatch;
            Vector2 at = Projectile.Center;
            Color lit = Color.Lerp(lightColor, Color.White, 0.25f);

            if (!Falling)
            {
                batch.Draw(body, at - Main.screenPosition, null, lit, Projectile.rotation, body.Size() * 0.5f, 0.85f, SpriteEffects.None, 0f);
                return false;
            }

            int left = Fuse - (int)Age;
            bool armed = left <= Armed;
            float open = Prime.Smooth((Age - RiseTicks) / 18f);

            // Its parachute over it, its cords down to it.
            Texture2D chute = PrimeArt.Chute;
            Vector2 up = new Vector2(0f, -1f).RotatedBy(Projectile.rotation * 0.6f);
            Vector2 top = at + up * (body.Height * 0.5f - 4f);
            Vector2 canopy = top + up * (20f + 34f * open);
            Vector2 across = new(-up.Y, up.X);
            float spread = chute.Width * 0.5f * (0.35f + 0.65f * open) - 4f;
            Color cord = new Color(36, 38, 44) * 0.9f;

            for (int k = -2; k <= 2; k++)
                DestroyerArt.DrawLine(batch, top, canopy + across * (spread * k / 2f) + up * 2f, 2f, cord);

            batch.Draw(chute, canopy - Main.screenPosition, null, lit, Projectile.rotation * 0.6f, new Vector2(chute.Width * 0.5f, chute.Height),
                new Vector2(0.35f + 0.65f * open, 0.5f + 0.5f * open), SpriteEffects.None, 0f);

            batch.Draw(body, at - Main.screenPosition, null, lit, Projectile.rotation, body.Size() * 0.5f, 1f, SpriteEffects.None, 0f);

            // Its lamp.
            Color c = armed ? PrimeArt.Nuclear : PrimeArt.Red;
            Color hot = armed ? PrimeArt.NuclearHot : PrimeArt.RedHot;
            float on = Blink(left, armed);
            DestroyerArt.DrawGlow(batch, LampAt, 34f, PrimeArt.Lit(c * (0.6f * on)));
            DestroyerArt.DrawGlow(batch, LampAt, 12f, PrimeArt.Lit(hot * (0.9f * on)), hotCore: true);
            return false;
        }
    }

    /// <summary>
    /// Something of Skeletron Prime's going up: a missile, a bomb, a nuke, its own head (Prime.Moves). A nuclear fire as
    /// far as it reaches (its own reach, given it), hurting everything in that for a moment; then only seen. The fire is
    /// the fire system's (PADFire) in its green: a heart of it, and for the great ones a front of it going out and a
    /// stem and cap climbing after. Never a ring.
    /// </summary>
    public class PrimeBurst : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>What kind: a small one (a missile's, a bomb's), a nuke's, its head's own.</summary>
        public const int Small = 0, Nuke = 1, Great = 2;

        public const float MissileReach = 46f, BombReach = 58f, SlamReach = 110f;

        private const int Hurts = 6;

        private float Reach => Math.Max(24f, Projectile.ai[0]);

        private int Kind => (int)Projectile.ai[1];

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>Its share of its end's blast (PrimeBlast): what its fire is sized by.</summary>
        private float Share => Reach / Prime.BlastReach;

        private int ClimbTicks => Reach < 150f ? 0 : (int)(40f + 70f * Share);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool CanHitPlayer(Player target) => Age <= Hurts;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Reach;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public override void AI()
        {
            Age++;
            int t = (int)Age;

            if (t > Math.Max(Hurts + 2, ClimbTicks + 4))
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            Vector2 at = Projectile.Center;

            if (t == 1)
                Up(at);

            Front(at, t);
            Climb(at, t);
        }

        /// <summary>The moment it goes up.</summary>
        private void Up(Vector2 at)
        {
            float r = Reach, k = Share, room = Prime.Room;
            bool great = r >= 150f;

            // Its light: smooth when it is big (the two-pixel grid would show a great glow as squares).
            if (great)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearHot * 0.95f, 6f * k, (int)(12 + 14 * k)));
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 10f * k, (int)(22 + 28 * k)));
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearDeep * 0.7f, 15f * k, (int)(34 + 46 * k)));
            }
            else
            {
                Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearHot * 0.9f, 0.5f + 5f * k, 10));
                Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.7f, 0.9f + 8f * k, 18));
            }

            // Its heart.
            if (great)
            {
                int fires = (int)MathHelper.Clamp(3f + 22f * (k - 0.33f), 3f, 10f);

                for (int i = 0; i < fires; i++)
                    PADFire.Burst(at + Main.rand.NextVector2Circular(r * 0.3f, r * 0.26f), Main.rand.NextFloat(76f, 100f) + 30f * k, FirePalette.Nuclear, 0.55f, smokes: i < 2);
            }
            else
            {
                PADFire.Burst(at, r * 0.9f, FirePalette.Nuclear, 0.75f, smokes: true);
            }

            for (int i = 0; i < (int)((6 + 64 * k) * room); i++)
            {
                Vector2 v = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.3f, 1.4f)).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(3f, 7f + 10f * k);
                Prime.Add(new HoneyMote(at + Main.rand.NextVector2Circular(r * 0.2f, r * 0.2f), v, Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear,
                    Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(34, 70), -0.1f));
            }

            for (int i = 0; i < (int)(8 + 42 * k); i++)
            {
                Dust d = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(r * 0.4f, r * 0.4f), DustID.CursedTorch,
                    new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(1f, 7f)) * (0.6f + k), 0, default, Main.rand.NextFloat(1.3f, 2.4f));
                d.noGravity = Main.rand.NextBool(3);
            }

            // The ground under it heaved (the ground's own way: a wave out through it, not a ring).
            if (great && Ground(at, r, out Vector2 foot))
                GroundWave.Slam(foot, 20f, (int)(r / 16f * 0.8f), 8f + 16f * k);

            Lighting.AddLight(at, 0.9f + 2f * k, 1.2f + 2.8f * k, 0.1f);

            if (great)
            {
                SoundEngine.PlaySound(PrimeBlast.NukeSound with { Volume = Kind == Great ? 0.95f : 0.55f, Pitch = Kind == Great ? 0f : 0.25f }, at);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f, Pitch = -0.5f, MaxInstances = 3 }, at);
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.9f, Pitch = -0.45f, MaxInstances = 3 }, at);
            }
            else
            {
                Prime.Sound(SoundID.Item14 with { Volume = 0.75f, Pitch = -0.15f, PitchVariance = 0.25f, MaxInstances = 6 }, at, 6);
            }

            Player me = Main.LocalPlayer;
            float near = Vector2.Distance(me.Center, at);
            float felt = 1f - MathHelper.Clamp((near - 500f - 900f * k) / 1600f, 0f, 1f);

            if (felt > 0.01f)
            {
                me.PIS().BumpScreenShake((4f + 30f * k) * felt, (int)(8 + 44 * k));

                if (Kind != Small)
                    Cinematic.Flash(new Color(238, 255, 200), (Kind == Great ? 0.6f : 0.24f) * felt);
            }
        }

        /// <summary>The ground under a place, within reach of it.</summary>
        private static bool Ground(Vector2 at, float reach, out Vector2 foot)
        {
            foot = at;
            int x = (int)(at.X / 16f), y0 = (int)(at.Y / 16f);

            for (int y = y0; y < y0 + (int)(reach / 16f) + 2; y++)
            {
                if (!WorldGen.InWorld(x, y, 2))
                    return false;

                Tile tile = Main.tile[x, y];

                if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    foot = new Vector2(at.X, y * 16f);
                    return true;
                }
            }

            return false;
        }

        /// <summary>A great one's fire going out: a front of it, further each tick, to as far as it hurt.</summary>
        private void Front(Vector2 at, int t)
        {
            const int ticks = 7;

            if (Reach < 150f || t < 2 || t > ticks)
                return;

            float r = Reach, k = Share;
            float reach = r * (0.3f + 0.62f * (t - 1) / (float)(ticks - 1));
            float turn = Main.rand.NextFloat(MathHelper.TwoPi);
            int fires = k > 0.5f ? 4 : 3;

            for (int i = 0; i < fires; i++)
            {
                Vector2 dir = (turn + i * MathHelper.TwoPi / fires + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2();
                PADFire.Burst(at + new Vector2(dir.X, dir.Y * 0.85f) * reach * Main.rand.NextFloat(0.85f, 1.05f), Main.rand.NextFloat(64f, 84f) + 36f * k, FirePalette.Nuclear, 0.45f, smokes: i == 0);
            }

            Lighting.AddLight(at, 1.4f + k, 1.9f + 1.4f * k, 0.1f);
        }

        /// <summary>A great one climbs after: a stem of fire, a cap over it, smoke off both.</summary>
        private void Climb(Vector2 at, int t)
        {
            int ticks = ClimbTicks;

            if (ticks <= 0 || t < 5 || t > ticks)
                return;

            float r = Reach, k = Share;
            float left = 1f - (t - 5f) / (ticks - 5f);
            float high = r * (0.75f + 0.4f * k) * (1f - MathF.Exp(-t / (18f + 26f * k)));
            float wide = 22f + 38f * k + (0.9f + 1.4f * k) * t;
            Vector2 cap = at + new Vector2(0f, -high);

            PADFire.Burn(at + new Vector2(Main.rand.NextFloat(-18f, 18f) * (1f + k), -Main.rand.NextFloat(0f, high)), 16f + 14f * k, (0.8f + 0.9f * k) * left, FirePalette.Nuclear, smokes: false);

            for (int i = 0; i < 2; i++)
                PADFire.Burn(cap + new Vector2(Main.rand.NextFloat(-wide, wide), Main.rand.NextFloat(-22f, 20f)), 22f + 22f * k, (1.2f + 1.2f * k) * left, FirePalette.Nuclear, smokes: false);

            if (t % 3 == 0 && Main.rand.NextFloat() < Prime.Room)
            {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Prime.Add(new BurnSmoke(cap + new Vector2(side * wide * Main.rand.NextFloat(0.6f, 1.1f), Main.rand.NextFloat(-20f, 20f)),
                    new Vector2(side * Main.rand.NextFloat(0.5f, 1.6f), -Main.rand.NextFloat(0.4f, 1.4f)), new Color(20, 22, 14), PrimeArt.NuclearDeep, Main.rand.Next(70, 120),
                    24f + 30f * k, 70f + 110f * k, 0.8f));
            }

            float glow = (1.2f + 1.6f * k) * left;
            Lighting.AddLight(cap, 0.73f * glow, glow, 0.03f * glow);
            Lighting.AddLight(at, 0.73f * glow, glow, 0.03f * glow);
        }
    }

    /// <summary>
    /// Its arms' clash's wave (by request: "the clash onto each other creating supersonic wave"): a front going out from
    /// where they met, faster than anything else of its does, less the further it goes; it throws back what it hits.
    /// </summary>
    public class PrimeShockwave : PrimeQuiet
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>How far it goes (sixty tiles), how fast, how thick its front is, and within what it is at its full.</summary>
        public const float Far = 60f * 16f;
        private const float Speed = 26f;
        private const float Thick = 42f;
        private const float FullWithin = 170f;

        private ref float Age => ref Projectile.localAI[0];

        private float Radius => Speed * Age;

        private static float Left(float far) => MathHelper.Lerp(1f, 0.16f, MathHelper.Clamp((far - FullWithin) / (Far - FullWithin), 0f, 1f));

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Age++;

            if (Radius > Far)
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            // The air it has squeezed, left behind its front as it goes: pale, thin, gone in a moment.
            float r = Radius, left = Left(r);

            for (int i = 0; i < 9; i++)
            {
                Vector2 out_ = Main.rand.NextVector2Unit();
                Vector2 at = Projectile.Center + out_ * (r - Main.rand.NextFloat(0f, 30f));

                if (!Prime.Seen(at, 40f) || Main.rand.NextFloat() > left * Prime.Room)
                    continue;

                Prime.Add(new SteamPuff(at, out_ * Main.rand.NextFloat(2f, 5f), new Color(214, 226, 214), Main.rand.Next(12, 22), Main.rand.NextFloat(16f, 26f),
                    Main.rand.NextFloat(44f, 70f), 0.22f * left));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
            Math.Abs(Vector2.Distance(targetHitbox.Center.ToVector2(), Projectile.Center) - Radius) < Thick;

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) =>
            modifiers.FinalDamage *= Left(Vector2.Distance(target.Center, Projectile.Center));

        /// <summary>It throws back what it hits, less the further out.</summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            Vector2 away = (target.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY);
            target.velocity = away * 17f * Left(Vector2.Distance(target.Center, Projectile.Center)) + new Vector2(0f, -3.5f);
        }

        /// <summary>
        /// Its front: a hard white line with its green at its back, thinner and fainter as it goes; a band of squeezed
        /// air behind it; a fainter line after.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch batch = Main.spriteBatch;
            float r = Radius, left = Left(r) * (1f - Prime.Smooth((r - Far * 0.8f) / (Far * 0.2f)));
            const int pieces = 96;

            for (int ring = 0; ring < 3; ring++)
            {
                float at = r - ring * 34f;

                if (at <= 4f)
                    continue;

                Vector2 last = Projectile.Center + Vector2.UnitX * at;

                for (int i = 1; i <= pieces; i++)
                {
                    Vector2 next = Projectile.Center + (MathHelper.TwoPi * i / pieces).ToRotationVector2() * at;

                    if (Prime.Seen((last + next) * 0.5f, 120f))
                    {
                        switch (ring)
                        {
                            case 0:
                                DestroyerArt.DrawLine(batch, last, next, 5f * left + 2f, PrimeArt.Lit(Color.White * (0.95f * left)));
                                break;

                            case 1:
                                DestroyerArt.DrawLine(batch, last, next, 54f * left + 10f, PrimeArt.Lit(new Color(200, 222, 190) * (0.1f * left)));
                                DestroyerArt.DrawLine(batch, last, next, 12f * left + 3f, PrimeArt.Lit(PrimeArt.Nuclear * (0.5f * left)));
                                break;

                            default:
                                DestroyerArt.DrawLine(batch, last, next, 4f * left + 1f, PrimeArt.Lit(PrimeArt.NuclearHot * (0.3f * left)));
                                break;
                        }
                    }

                    last = next;
                }
            }

            return false;
        }
    }

    /// <summary>Earth its saw scoops out of the ground and throws (by request: "throwing dirt and dust fragments"): it falls, and breaks on what it hits.</summary>
    public class PrimeClod : PrimeQuiet
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            Age++;
            Projectile.velocity.X *= 0.995f;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.32f, 15f);
            Projectile.rotation += 0.12f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);

            if (OnAPlayer())
            {
                Projectile.Kill();
                return;
            }

            if (!Main.dedServ && (int)Age % 3 == 0 && Prime.Seen(Projectile.Center, 30f))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), DustID.Dirt, Projectile.velocity * 0.2f, 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = false;
            }
        }

        // Not the first ticks: thrown out of the ground it came from.
        public override bool? CanCutTiles() => false;

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
        {
            fallThrough = true;
            return true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => Age > 10f;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => Projectile.Kill();

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ || Quiet)
                return;

            for (int i = 0; i < 9; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Dirt, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(0.5f, 3.5f)), 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = false;
            }

            if (Prime.Seen(Projectile.Center, 40f))
                Prime.Add(new DustFog(Projectile.Center, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -0.4f), new Color(118, 96, 74) * 0.8f, 30, 0.45f, 0.26f));

            Prime.Sound(SoundID.Dig with { Volume = 0.5f, PitchVariance = 0.3f, MaxInstances = 6 }, Projectile.Center, 6);
        }

        /// <summary>A lump of earth: four squares of it on the two-pixel grid, lit from above and to the left.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new(0, 0, 1, 1);
            SpriteBatch batch = Main.spriteBatch;
            Vector2 at = Projectile.Center - Main.screenPosition;
            float turn = MathF.Round(Projectile.rotation / MathHelper.PiOver2) * MathHelper.PiOver2;
            float light = Math.Max(0.35f, lightColor.ToVector3().Length() / 1.73f);

            Color Lit(Color c) => new Color(c.ToVector3() * light);

            void Square(float x, float y, float size, Color c) =>
                batch.Draw(pixel, at + new Vector2(x, y).RotatedBy(turn), one, Lit(c), turn, new Vector2(0.5f), size, SpriteEffects.None, 0f);

            Square(0f, 0f, 12f, new Color(52, 38, 30));
            Square(0f, 0f, 8f, new Color(116, 86, 60));
            Square(-2f, -2f, 4f, new Color(156, 120, 84));
            Square(3f, 3f, 4f, new Color(78, 56, 42));
            return false;
        }
    }
}
