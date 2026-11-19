using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Graphics;
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
    /// What the Legends Twins throw (Twins.Ret, Twins.Spaz, Twins.Duo). The server makes them; every machine sees them.
    ///
    ///   (vanilla's own)     their lasers, cursed flames and flamethrower, reworked where they are the Legends Twins'
    ///                       (TwinsVanillaShots): burning in the new fire (PADFire), giving Industrial Flames; the
    ///                       flamethrower's flames half as far again
    ///   TwinsFireColumn     the Retinazer's column of fire: out of its mouth the way it faces, for as long as it is given
    ///   TwinsGasShot        a cloud of chlorine off the Spazmatism's spiral: slowing, swelling, hanging
    ///   TwinsToxicSpear     its spear
    ///   TwinsFlammableGas   what it vomits: harmless, till something lights it, and then each lights the next
    ///   TwinsShockwave      their clash's
    ///
    /// Damage is as vanilla counts a boss's shots (doubled in a normal world, four times in Expert), a seventh more in
    /// Legends: the column 34/30, the spear 28/25, chlorine 36/32, the gas going up 50/44, the shockwave 46/40 at its heart (a tenth of that at its end).
    /// </summary>
    public static class TwinsShots
    {
        private static bool Server => Main.netMode != NetmodeID.MultiplayerClient;

        private static int Scaled(NPC npc, float normal, float expert) =>
            (int)(npc.GetAttackDamage_ForProjectiles(normal, expert) * (PADWorld.Legends ? Twins.DamageScale : 1f));

        private static int New<T>(NPC owner, Vector2 at, Vector2 velocity, int damage, float a = 0f, float b = 0f, float c = 0f) where T : ModProjectile =>
            Projectile.NewProjectile(owner.GetSource_FromAI(), at, velocity, ModContent.ProjectileType<T>(), damage, 0f, Main.myPlayer, a, b, c);

        /// <summary>A column of fire out of the Retinazer's mouth, this wide, for this many ticks.</summary>
        public static void FireColumn(NPC owner, float wide, int life)
        {
            if (Server)
                New<TwinsFireColumn>(owner, owner.Center, Vector2.Zero, Scaled(owner, 34f, 30f), owner.whoAmI, wide, life);
        }

        /// <summary>One of vanilla's cursed flames (it is made the Legends Spazmatism's as it is born, its damage raised there: TwinsVanillaShots).</summary>
        public static void CurseBall(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                Projectile.NewProjectile(owner.GetSource_FromAI(), at, velocity, ProjectileID.CursedFlameHostile, owner.GetAttackDamage_ForProjectiles(25f, 22f), 0f, Main.myPlayer);
        }

        /// <summary>One of vanilla's eye lasers (made the Legends Retinazer's as it is born).</summary>
        public static void Laser(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                Projectile.NewProjectile(owner.GetSource_FromAI(), at, velocity, ProjectileID.EyeLaser, owner.GetAttackDamage_ForProjectiles(20f, 19f), 0f, Main.myPlayer);
        }

        public static void GasShot(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                New<TwinsGasShot>(owner, at, velocity, Scaled(owner, 36f, 32f));
        }

        public static void Spear(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                New<TwinsToxicSpear>(owner, at, velocity, Scaled(owner, 28f, 25f));
        }

        public static void Fumes(NPC owner, Vector2 at, Vector2 velocity)
        {
            if (Server)
                New<TwinsFlammableGas>(owner, at, velocity, Scaled(owner, 50f, 44f));
        }

        /// <summary>Their clash's shockwave, from where they met; <paramref name="axis"/> the line they met along (the Retinazer's side of it).</summary>
        public static void Shockwave(NPC owner, Vector2 at, float axis)
        {
            if (Server)
                New<TwinsShockwave>(owner, at, Vector2.Zero, Scaled(owner, 46f, 40f), TwinsShockwave.Far, axis);
        }

        /// <summary>Whether a projectile is one of these.</summary>
        public static bool IsOurs(int type) =>
            type == ModContent.ProjectileType<TwinsFireColumn>() || type == ModContent.ProjectileType<TwinsGasShot>() || type == ModContent.ProjectileType<TwinsToxicSpear>()
            || type == ModContent.ProjectileType<TwinsFlammableGas>() || type == ModContent.ProjectileType<TwinsShockwave>();

        /// <summary>Every column of fire this eye has out, ended (its attack broken off). The server's.</summary>
        public static void EndColumns(NPC owner)
        {
            if (!Server)
                return;

            int type = ModContent.ProjectileType<TwinsFireColumn>();

            foreach (Projectile column in Main.ActiveProjectiles)
            {
                if (column.type == type && (int)column.ai[0] == owner.whoAmI)
                    column.Kill();
            }
        }

        // ---- the gas, and what lights it ------------------------------------------------------------------------------------

        /// <summary>The hanging cloud nearest a place.</summary>
        public static bool NearestFumes(Vector2 to, out Vector2 at)
        {
            at = default;
            int type = ModContent.ProjectileType<TwinsFlammableGas>();
            float best = float.MaxValue;

            foreach (Projectile cloud in Main.ActiveProjectiles)
            {
                if (cloud.type != type || cloud.ai[0] != 0f)
                    continue;

                float far = Vector2.DistanceSquared(cloud.Center, to);

                if (far < best)
                {
                    best = far;
                    at = cloud.Center;
                }
            }

            return best < float.MaxValue;
        }

        /// <summary>Every hanging cloud within reach of a place (and its own size) lit: it goes up next tick. Whether any was. The server's.</summary>
        public static bool TouchFumes(Vector2 at, float reach)
        {
            if (!Server)
                return false;

            bool any = false;
            int type = ModContent.ProjectileType<TwinsFlammableGas>();

            foreach (Projectile cloud in Main.ActiveProjectiles)
            {
                if (cloud.type != type || cloud.ai[0] != 0f || cloud.ModProjectile is not TwinsFlammableGas gas)
                    continue;

                if (Vector2.Distance(cloud.Center, at) > reach + gas.Radius)
                    continue;

                TwinsFlammableGas.Light(cloud, 1);
                any = true;
            }

            return any;
        }

        /// <summary>The hanging cloud nearest a place lit, however far. The server's.</summary>
        public static void LightNearest(Vector2 at)
        {
            if (!Server)
                return;

            int type = ModContent.ProjectileType<TwinsFlammableGas>();
            Projectile nearest = null;
            float best = float.MaxValue;

            foreach (Projectile cloud in Main.ActiveProjectiles)
            {
                if (cloud.type != type || cloud.ai[0] != 0f)
                    continue;

                float far = Vector2.DistanceSquared(cloud.Center, at);

                if (far < best)
                {
                    best = far;
                    nearest = cloud;
                }
            }

            if (nearest != null)
                TwinsFlammableGas.Light(nearest, 4);
        }
    }

    /// <summary>
    /// Vanilla's eye laser, death laser, cursed flame and eye fire, where they are the Legends Twins' (known as each is
    /// born, from what made it, and told to every machine): burning in the new fire, giving Industrial Flames (by request:
    /// "vanilla laser but flamin", "His flaming/fire attacks does Industrial Flames"); the eye fire's flames go half as far
    /// again (by request: "1.5x distance that flame travels": its speed, since vanilla fixes how long it lives).
    /// </summary>
    public class TwinsVanillaShots : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) =>
            entity.type is ProjectileID.EyeLaser or ProjectileID.DeathLaser or ProjectileID.CursedFlameHostile or ProjectileID.EyeFire;

        private const byte NotTheirs = 0, Laser = 1, Ball = 2, Thrower = 3;

        private byte kind;
        private int age;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!PADWorld.Legends || NPC.IsMechQueenUp || source is not EntitySource_Parent { Entity: NPC npc })
                return;

            if (npc.type == NPCID.Retinazer && projectile.type is ProjectileID.EyeLaser or ProjectileID.DeathLaser)
            {
                kind = Laser;
            }
            else if (npc.type == NPCID.Spazmatism && projectile.type == ProjectileID.CursedFlameHostile)
            {
                kind = Ball;
            }
            else if (npc.type == NPCID.Spazmatism && projectile.type == ProjectileID.EyeFire)
            {
                kind = Thrower;
                projectile.velocity *= 1.5f;
            }

            // A seventh more, as all of theirs in Legends (told to every machine with the rest of it).
            if (kind != NotTheirs)
                projectile.damage = (int)(projectile.damage * Twins.DamageScale);
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter) => binaryWriter.Write(kind);

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) => kind = binaryReader.ReadByte();

        public override void PostAI(Projectile projectile)
        {
            // Once a tick (they move several times in one).
            if (kind == NotTheirs || Main.dedServ || projectile.numUpdates != -1)
                return;

            age++;
            Vector2 at = projectile.Center;
            Vector2 drift = projectile.velocity * 0.2f;

            switch (kind)
            {
                case Laser:
                    PADFire.Flame(at, -drift + Main.rand.NextVector2Circular(0.6f, 0.6f), Main.rand.NextFloat(16f, 26f), Main.rand.Next(10, 17), FirePalette.Fire, 0.05f, 0.5f);

                    if (Main.rand.NextBool(6))
                        PADFire.Spark(at, -drift + Main.rand.NextVector2Circular(2f, 2f), Main.rand.Next(12, 22));

                    Lighting.AddLight(at, 1f, 0.5f, 0.12f);
                    break;

                case Ball:
                    PADFire.Flame(at, drift + Main.rand.NextVector2Circular(0.8f, 0.8f), Main.rand.NextFloat(24f, 38f), Main.rand.Next(12, 20), FirePalette.Toxic, 0.06f, 0.5f, round: Main.rand.NextBool());
                    Lighting.AddLight(at, 0.3f, 0.9f, 0.25f);
                    break;

                default:
                    for (int i = 0; i < 2; i++)
                        PADFire.Flame(at + Main.rand.NextVector2Circular(10f, 10f), projectile.velocity * 0.9f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                            Main.rand.NextFloat(24f, 34f) + age * 2.2f, Main.rand.Next(12, 20), FirePalette.Toxic, 0.04f, 0.45f);

                    Lighting.AddLight(at, 0.3f, 0.9f, 0.25f);
                    break;
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (kind != Laser)
                return true;

            // A streak of fire in place of the laser's own picture.
            Vector2 at = projectile.Center;
            Vector2 way = projectile.velocity.SafeNormalize(Vector2.UnitY);
            SpriteBatch batch = Main.spriteBatch;
            DestroyerArt.DrawLine(batch, at - way * 54f, at + way * 8f, 14f, DestroyerArt.Lit(new Color(240, 90, 24) * 0.55f));
            DestroyerArt.DrawLine(batch, at - way * 40f, at + way * 6f, 5f, DestroyerArt.Lit(new Color(255, 232, 170)));
            return false;
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (kind != NotTheirs)
                target.AddBuff(ModContent.BuffType<IndustrialFlames>(), IndustrialFlames.Ticks);
        }
    }

    public class TwinsFireColumn : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private int Owner => (int)Projectile.ai[0];

        private float Wide => Math.Max(16f, Projectile.ai[1]);

        private int Life => Math.Max(2, (int)Projectile.ai[2]);

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>Where it leaves the mouth, the way it goes, and whether its eye is firing on this machine yet.</summary>
        private Vector2 from, way = Vector2.UnitY;
        private bool live;

        /// <summary>In over four ticks, out over six.</summary>
        private float Strength => Math.Min(1f, Age / 4f) * Math.Min(1f, (Life - Age + 1f) / 6f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

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
            live = owner != null && owner.active && owner.type == NPCID.Retinazer && owner.TryGetGlobalNPC(out Twins rework) && rework.ColumnOn;

            if (!live)
            {
                // Its eye has stopped (or, on a client, word of its attack is not here yet): the server's to end.
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

            way = Twins.Facing(owner);
            from = Twins.Front(owner, 58f);
            Projectile.Center = from;
            Projectile.timeLeft = 10;

            if (Main.dedServ)
                return;

            float strength = Strength;
            PADFire.Jet(from, way, Twins.ColumnReach, Wide, FirePalette.Fire, strength);

            for (float k = 0f; k < Twins.ColumnReach; k += 110f)
            {
                Vector2 at = from + way * k;

                if (PADFire.Near(at, 160f))
                    Lighting.AddLight(at, 1.6f * strength, 0.8f * strength, 0.22f * strength);
            }
        }

        public override bool CanHitPlayer(Player target) => live && Age >= 3f && Age <= Life - 2f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!live)
                return false;

            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, from + way * Twins.ColumnReach, Wide * 0.8f, ref along);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(ModContent.BuffType<IndustrialFlames>(), IndustrialFlames.Ticks);

        /// <summary>Its glow under its flames (the flames themselves are PADFire's): red wide, orange, a hot line, fading out along its last fifth.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            if (!live)
                return false;

            float strength = Strength;
            float flick = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 43f);
            SpriteBatch batch = Main.spriteBatch;

            Fading(batch, Wide * 2.2f, new Color(206, 52, 14) * (0.22f * strength));
            Fading(batch, Wide * 1.1f, new Color(255, 128, 36) * (0.32f * strength * flick));
            Fading(batch, Wide * 0.36f, new Color(255, 236, 170) * (0.6f * strength * flick));
            DestroyerArt.DrawGlow(batch, from, Wide * 3.4f, DestroyerArt.Lit(new Color(255, 170, 70) * (0.6f * strength)), hotCore: true);
            return false;
        }

        private void Fading(SpriteBatch batch, float wide, Color colour)
        {
            float full = Twins.ColumnReach * 0.78f;
            DestroyerArt.DrawLine(batch, from, from + way * full, wide, DestroyerArt.Lit(colour));

            const int steps = 8;

            for (int i = 0; i < steps; i++)
            {
                float a = full + (Twins.ColumnReach - full) * i / steps, b = full + (Twins.ColumnReach - full) * (i + 1) / steps;
                DestroyerArt.DrawLine(batch, from + way * a, from + way * b, wide, DestroyerArt.Lit(colour * (1f - (i + 0.5f) / steps)));
            }
        }
    }

    public class TwinsGasShot : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private const int Lasts = 170;

        private ref float Age => ref Projectile.localAI[0];

        private float Radius => 26f + 44f * Twins.Smooth(Age / 50f);

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lasts;
        }

        public override void AI()
        {
            Age++;
            Projectile.velocity *= 0.965f;
            Projectile.rotation += 0.01f;

            if (Main.dedServ)
                return;

            float r = Radius;

            if ((int)Age % 2 == 0 && Projectile.timeLeft > 30)
                PADFire.Gas(Projectile.Center + Main.rand.NextVector2Circular(r * 0.55f, r * 0.55f), Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    r * Main.rand.NextFloat(0.9f, 1.4f), Main.rand.Next(30, 47));

            Lighting.AddLight(Projectile.Center, 0.18f, 0.5f, 0.16f);
        }

        public override bool CanHitPlayer(Player target) => Age > 6f && Projectile.timeLeft > 24;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Radius * 0.85f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(ModContent.BuffType<ChlorinePoisoning>(), ChlorinePoisoning.Ticks);

        /// <summary>
        /// Its body (by request: "use code gradient texture"): three of the fire system's puffs one in another on the
        /// half-size canvas, dark green at its rim through green to a pale heart set up and toward the light.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 at = Projectile.Center;
            float wide = Radius * 2.5f, turn = Projectile.rotation;
            float shown = Math.Min(1f, Age / 10f) * Math.Min(1f, Projectile.timeLeft / 30f);
            int which = Projectile.whoAmI;

            PADPixelCanvas.Queue(batch =>
            {
                PADFire.DrawPuff(batch, at, wide, new Color(22, 84, 34) * (0.5f * shown), turn, which);
                PADFire.DrawPuff(batch, at + new Vector2(wide * 0.03f, -wide * 0.05f), wide * 0.76f, new Color(70, 170, 58) * (0.5f * shown), -turn, which + 1);
                PADFire.DrawPuff(batch, at + new Vector2(wide * 0.07f, -wide * 0.12f), wide * 0.46f, new Color(190, 240, 120) * (0.45f * shown), turn * 2f, which + 2);
            });

            return false;
        }
    }

    public class TwinsToxicSpear : ModProjectile
    {
        private const float Fastest = 19f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI()
        {
            if (Projectile.velocity.Length() < Fastest)
                Projectile.velocity *= 1.018f;

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.dedServ)
                return;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitY);

            if (Projectile.timeLeft % 2 == 0)
                PADFire.Flame(Projectile.Center - way * 22f, -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f), Main.rand.NextFloat(14f, 22f), Main.rand.Next(9, 16), FirePalette.Toxic, 0.02f, 0.5f);

            Lighting.AddLight(Projectile.Center, 0.3f, 0.85f, 0.25f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center - way * 24f, Projectile.Center + way * 26f, 12f, ref along);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(ModContent.BuffType<ChlorinePoisoning>(), ChlorinePoisoning.Ticks);

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            SpriteBatch batch = Main.spriteBatch;
            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitY);

            // Its light, drawn out behind it; then itself, lit by nothing but itself.
            DestroyerArt.DrawOval(batch, Projectile.Center - way * 14f, way.ToRotation(), 110f, 26f, DestroyerArt.Lit(TwinsArt.ToxicBright * 0.4f));
            DestroyerArt.DrawLine(batch, Projectile.Center - way * 70f, Projectile.Center - way * 10f, 5f, DestroyerArt.Lit(TwinsArt.ToxicBright * 0.35f));
            batch.Draw(texture, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, new Vector2(texture.Width * 0.5f, 22f), 1f, SpriteEffects.None, 0f);
            return false;
        }
    }

    public class TwinsFlammableGas : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private const float Hanging = 0f, Lit = 1f, GoingUp = 2f;
        private const int Lasts = 780, UpTicks = 12, HurtTicks = 8;

        /// <summary>How fast the fire runs from one cloud to the next (pixels a tick), and how far past touching it reaches.</summary>
        private const float FireRuns = 24f;
        private const float Reach = 46f;

        private ref float State => ref Projectile.ai[0];

        private ref float Fuse => ref Projectile.ai[1];

        private ref float Age => ref Projectile.localAI[0];

        private ref float Up => ref Projectile.localAI[1];

        /// <summary>How big it is: swelling as it hangs (by request: "slows down from release and gets bigger").</summary>
        internal float Radius => 34f + 62f * Twins.Smooth(Age / 110f);

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lasts;
        }

        /// <summary>Lit: it goes up when its fuse is out. The server's.</summary>
        internal static void Light(Projectile cloud, int fuse)
        {
            if (cloud.ai[0] != Hanging)
                return;

            cloud.ai[0] = Lit;
            cloud.ai[1] = fuse;
            cloud.netUpdate = true;
        }

        public override void AI()
        {
            Age++;
            Projectile.rotation += 0.006f;
            bool server = Main.netMode != NetmodeID.MultiplayerClient;

            if (State == Lit && server && --Fuse <= 0f)
            {
                State = GoingUp;
                Projectile.netUpdate = true;
                int type = Type;

                // Each lights those beside it, the fire taking its time to reach them.
                foreach (Projectile other in Main.ActiveProjectiles)
                {
                    if (other.type != type || other.whoAmI == Projectile.whoAmI || other.ai[0] != Hanging || other.ModProjectile is not TwinsFlammableGas gas)
                        continue;

                    float far = Vector2.Distance(other.Center, Projectile.Center);

                    if (far <= Radius + gas.Radius + Reach)
                        Light(other, 2 + (int)(far / FireRuns) + Main.rand.Next(3));
                }
            }

            if (State == GoingUp)
            {
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = Math.Max(Projectile.timeLeft, 3);

                if (++Up == 1f)
                    Burst();

                if (Up > UpTicks)
                    Projectile.Kill();

                return;
            }

            Projectile.velocity *= 0.95f;

            if (Main.dedServ)
                return;

            float r = Radius;

            if ((int)Age % 5 == 0 && Projectile.timeLeft > 40)
                PADFire.Gas(Projectile.Center + Main.rand.NextVector2Circular(r * 0.5f, r * 0.5f), Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    r * Main.rand.NextFloat(0.9f, 1.4f), Main.rand.Next(40, 61), 0.8f);
        }

        public override bool CanHitPlayer(Player target) => State == GoingUp && Up <= HurtTicks;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Radius * 1.2f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(ModContent.BuffType<IndustrialFlames>(), IndustrialFlames.Ticks);

        /// <summary>It goes up: the gas flashing green as it catches, then fire, its light, the bang, the screen shaken by how near it is.</summary>
        private void Burst()
        {
            if (Main.dedServ)
                return;

            Vector2 at = Projectile.Center;
            float r = Radius;
            PADFire.Flame(at, Vector2.Zero, r * 2.6f, 8, FirePalette.Toxic, 0f, 0.7f, round: true);
            PADFire.Burst(at, r * 1.1f, FirePalette.Fire, 0.5f, smokes: Main.rand.NextBool(3));
            Lighting.AddLight(at, 2.4f, 1.3f, 0.4f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.3f, PitchVariance = 0.3f, MaxInstances = 5 }, at);

            if (Main.rand.NextBool(4))
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.8f, Pitch = -0.3f, MaxInstances = 3 }, at);

            Player me = Main.LocalPlayer;
            float near = Vector2.Distance(me.Center, at);

            if (near < 1500f)
                me.PIS().BumpScreenShake(12f * (1f - near / 1500f), 14);
        }

        /// <summary>Its body: paler and yellower than chlorine, thin, on the half-size canvas.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            if (State == GoingUp)
                return false;

            Vector2 at = Projectile.Center;
            float wide = Radius * 2.5f, turn = Projectile.rotation;
            float shown = Math.Min(1f, Age / 12f) * Math.Min(1f, Projectile.timeLeft / 40f);
            float fuse = State == Lit ? 1f : 0f;
            int which = Projectile.whoAmI;

            PADPixelCanvas.Queue(batch =>
            {
                PADFire.DrawPuff(batch, at, wide, Color.Lerp(new Color(60, 110, 40), new Color(200, 110, 30), fuse) * (0.34f * shown), turn, which);
                PADFire.DrawPuff(batch, at + new Vector2(wide * 0.05f, -wide * 0.09f), wide * 0.6f, Color.Lerp(new Color(176, 214, 96), new Color(255, 200, 90), fuse) * (0.3f * shown), -turn, which + 1);
            });

            return false;
        }
    }

    public class TwinsShockwave : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>How far it goes (seventy tiles), how fast, how thick its front is, and within what it is at its full.</summary>
        public const float Far = 70f * 16f;
        private const float Speed = 18f;
        private const float Thick = 44f;
        private const float FullWithin = 160f;

        private float Reach => Math.Max(200f, Projectile.ai[0]);

        private float Axis => Projectile.ai[1];

        private ref float Age => ref Projectile.localAI[0];

        private float Radius => Speed * Age;

        /// <summary>How much of it is left this far out (by request: "dies down as it goes").</summary>
        private float Left(float far) => MathHelper.Lerp(1f, 0.1f, MathHelper.Clamp((far - FullWithin) / Math.Max(1f, Reach - FullWithin), 0f, 1f));

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

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

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Age++;

            if (Radius > Reach)
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            // Their fires carried out on its front while it is young, each on its own side.
            float r = Radius, left = Left(r);

            if (Age <= 16f)
            {
                for (int i = 0; i < 10; i++)
                {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 out_ = a.ToRotationVector2();
                    bool fire = MathF.Cos(a - Axis) > 0f;
                    PADFire.Flame(Projectile.Center + out_ * r, out_ * Speed * 0.5f, Main.rand.NextFloat(30f, 54f), Main.rand.Next(10, 18), fire ? FirePalette.Fire : FirePalette.Toxic, 0.03f, 0.5f);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                Vector2 out_ = Main.rand.NextVector2Unit();
                Vector2 at = Projectile.Center + out_ * r;

                if (PADFire.Near(at, 60f))
                    Lighting.AddLight(at, 0.9f * left, 0.9f * left, 0.6f * left);
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
            target.velocity = away * 15f * Left(Vector2.Distance(target.Center, Projectile.Center)) + new Vector2(0f, -3f);
        }

        /// <summary>
        /// Its front: a ring in their two colours, the Retinazer's on its side of the line they met along and the
        /// Spazmatism's on the other, thinner and fainter as it goes; a fainter one a little behind it.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch batch = Main.spriteBatch;
            float r = Radius, left = Left(r) * (1f - Twins.Smooth((r - Reach * 0.8f) / (Reach * 0.2f)));
            const int pieces = 84;

            for (int ring = 0; ring < 2; ring++)
            {
                float at = r - ring * 46f;

                if (at <= 4f)
                    continue;

                float strength = left * (ring == 0 ? 1f : 0.4f);
                Vector2 last = Projectile.Center + Vector2.UnitX * at;

                for (int i = 1; i <= pieces; i++)
                {
                    float a = MathHelper.TwoPi * i / pieces;
                    Vector2 next = Projectile.Center + a.ToRotationVector2() * at;
                    Color c = Color.Lerp(TwinsArt.ToxicBright, TwinsArt.FireBright, 0.5f + 0.5f * MathF.Cos(a - Axis));
                    DestroyerArt.DrawLine(batch, last, next, 34f * strength + 8f, DestroyerArt.Lit(c * (0.2f * strength)));
                    DestroyerArt.DrawLine(batch, last, next, 7f * strength + 2f, DestroyerArt.Lit(Color.Lerp(c, Color.White, 0.5f) * (0.85f * strength)));
                    last = next;
                }
            }

            return false;
        }
    }
}
