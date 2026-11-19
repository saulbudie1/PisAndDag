using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// The Legends Destroyer's shots, made by the server only (every machine sees them by the game's own sync):
    ///   DestroyerBolt  a bolt (its lasers, in its colour): held at a segment or Probe while a line warns where it will go,
    ///                  following its mark until ten ticks before, then let go, quickening (after Thanatos's warned lasers)
    ///   DestroyerRay   its beam (its pulse laser, its sweep): four tiles wide, from its mouth, while its head says so
    ///   DestroyerArc   a stroke of its lightning, between two places, for a moment (its sweep's chains)
    ///   ProbeBlast     two of its Probes flown into each other: a great burst of its lightning
    /// Damage reckoned as vanilla reckons its lasers (22 and 18 for normal and expert), a seventh again (Legends).
    /// </summary>
    internal static class DestroyerShots
    {
        public const float RayLength = 2400f;
        public const float RayWidth = 64f;

        public static bool IsOurs(int type) =>
            type == ModContent.ProjectileType<DestroyerBolt>() || type == ModContent.ProjectileType<DestroyerRay>()
            || type == ModContent.ProjectileType<DestroyerArc>() || type == ModContent.ProjectileType<ProbeBlast>();

        private static int Scale(int damage) => Math.Max(1, (int)(damage * Destroyer.DamageScale));

        public static int BoltDamage(NPC npc) => Scale(npc.GetAttackDamage_ForProjectiles(24f, 20f));

        public static int RayDamage(NPC npc) => Scale(npc.GetAttackDamage_ForProjectiles(60f, 48f));

        public static int ArcDamage(NPC npc) => Scale(npc.GetAttackDamage_ForProjectiles(32f, 26f));

        public static int BlastDamage(NPC npc) => Scale(npc.GetAttackDamage_ForProjectiles(52f, 42f));

        public static int ProbeBoltDamage(NPC npc) => Scale(npc.GetAttackDamage_ForProjectiles(20f, 17f));

        private static bool Server => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>A bolt held at <paramref name="from"/> (a segment or a Probe), warned for <paramref name="warn"/> ticks, at a player.</summary>
        public static void Bolt(NPC source, NPC from, Player at, int warn, int damage)
        {
            if (!Server)
                return;

            Projectile.NewProjectile(source.GetSource_FromAI(), from.Center, Vector2.Zero, ModContent.ProjectileType<DestroyerBolt>(), damage, 0f, Main.myPlayer,
                warn, from.whoAmI, at.whoAmI);
        }

        /// <summary>Its beam, from its head, for as long as its head keeps it on.</summary>
        public static void Ray(NPC head, int damage)
        {
            if (!Server)
                return;

            Projectile.NewProjectile(head.GetSource_FromAI(), head.Center, Vector2.Zero, ModContent.ProjectileType<DestroyerRay>(), damage, 0f, Main.myPlayer, head.whoAmI);
        }

        /// <summary>A stroke of its lightning from one place to another.</summary>
        public static void Chain(NPC head, Vector2 from, Vector2 to, int damage)
        {
            if (!Server)
                return;

            Projectile.NewProjectile(head.GetSource_FromAI(), from, Vector2.Zero, ModContent.ProjectileType<DestroyerArc>(), damage, 0f, Main.myPlayer, to.X, to.Y);
        }

        /// <summary>A great burst of its lightning, this far across (radius).</summary>
        public static void Blast(NPC source, Vector2 at, float radius, int damage)
        {
            if (!Server)
                return;

            Projectile.NewProjectile(source.GetSource_FromAI(), at, Vector2.Zero, ModContent.ProjectileType<ProbeBlast>(), damage, 0f, Main.myPlayer, radius);
        }
    }

    public class DestroyerBolt : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private int Warn => (int)Projectile.ai[0];

        private int Anchor => (int)Projectile.ai[1];

        private int Aimed => Math.Clamp((int)Projectile.ai[2], 0, Main.maxPlayers - 1);

        private ref float Age => ref Projectile.localAI[0];

        private ref float Aim => ref Projectile.localAI[1];

        private bool Launched => Age > Warn;

        /// <summary>Where it was let go from, and how long its warning was (this machine's), for the warning to fade from.</summary>
        private Vector2 launchedFrom;
        private float warnLength = 3200f;

        private const float TopSpeed = 19f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 900;
        }

        public override void AI()
        {
            // Let go by the server before this machine counted to it (it was made here a little later).
            if (Main.netMode == NetmodeID.MultiplayerClient && Age <= Warn && Projectile.velocity.LengthSquared() > 1f)
            {
                Age = Warn + 1;
                Aim = Projectile.velocity.ToRotation();
                launchedFrom = Projectile.Center - Projectile.velocity;
            }

            Age++;

            if (Age <= Warn)
            {
                NPC from = Anchor >= 0 && Anchor < Main.maxNPCs ? Main.npc[Anchor] : null;

                if (from == null || !from.active)
                {
                    if (Projectile.owner == Main.myPlayer)
                        Projectile.Kill();

                    return;
                }

                Projectile.Center = from.Center + from.velocity;
                Projectile.velocity = Vector2.Zero;
                Player p = Main.player[Aimed];

                if ((Age <= Warn - 10 || Age <= 1) && p.active && !p.dead)
                {
                    Aim = (p.Center - Projectile.Center).ToRotation();
                    warnLength = MathHelper.Clamp((Vector2.Distance(p.Center, Projectile.Center) + 240f) * 2f, 600f, 3200f);
                }

                Projectile.rotation = Aim;
                launchedFrom = Projectile.Center;

                if (Age == Warn)
                {
                    Projectile.velocity = Aim.ToRotationVector2() * 7f;
                    Projectile.timeLeft = 240;
                    Projectile.netUpdate = true;

                    if (!Main.dedServ && Projectile.identity % 6 == 0)
                        SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.45f, Pitch = 0.25f, MaxInstances = 6 }, Projectile.Center);
                }

                return;
            }

            if (Projectile.velocity.Length() < TopSpeed)
                Projectile.velocity *= 1.05f;

            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.25f, 0.42f, 0.42f);
        }

        public override bool CanHitPlayer(Player target) => Launched;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Launched)
                return false;

            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center - Projectile.velocity, Projectile.Center, 12f, ref along);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch batch = Main.spriteBatch;
            Vector2 at = Projectile.Center;

            if (!Launched)
            {
                float ready = MathHelper.Clamp(Age / Math.Max(1f, Warn), 0f, 1f);
                DestroyerArt.DrawWarning(batch, at, Aim, warnLength, ready, 0.9f);
                DestroyerArt.DrawGlow(batch, at, 10f + 22f * ready, DestroyerArt.Lit(DestroyerArt.Teal * (0.25f + 0.5f * ready)));
                return false;
            }

            // Its warning fades where it was, after it has gone.
            float gone = Age - Warn;

            if (gone < DestroyerArt.WarningFade)
                DestroyerArt.DrawWarning(batch, launchedFrom, Aim, warnLength, 1f, 0.9f * (1f - gone / DestroyerArt.WarningFade));

            float angle = Projectile.rotation;
            Vector2 dir = angle.ToRotationVector2();
            float length = 34f + Projectile.velocity.Length() * 1.4f;

            DestroyerArt.DrawOval(batch, at - dir * length * 0.3f, angle, length, 16f, DestroyerArt.Lit(DestroyerArt.Teal * 0.7f));
            DestroyerArt.DrawLine(batch, at - dir * 22f, at + dir * 6f, 5f, DestroyerArt.Lit(Color.White * 0.9f));
            DestroyerArt.DrawGlow(batch, at, 26f, DestroyerArt.Lit(DestroyerArt.Teal * 0.45f));
            return false;
        }
    }

    public class DestroyerRay : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>Hidden on a client whose head has not turned its beam on yet (or has turned it off before the server's kill came).</summary>
        private bool hidden;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }

        private bool Head(out NPC head, out Destroyer rework)
        {
            int at = (int)Projectile.ai[0];
            head = at >= 0 && at < Main.maxNPCs ? Main.npc[at] : null;
            rework = null;
            return head != null && head.active && head.type == NPCID.TheDestroyer && head.TryGetGlobalNPC(out rework) && rework.beamOn;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;

            if (!Head(out NPC head, out Destroyer rework))
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.Kill();
                    return;
                }

                hidden = true;
                Projectile.timeLeft = 2;
                return;
            }

            hidden = false;
            Projectile.timeLeft = 2;
            Age++;
            Projectile.Center = rework.BeamStart(head);
            Projectile.rotation = rework.beamAngle;
            Projectile.Opacity = rework.beamPower;

            if (Main.dedServ)
                return;

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 across = dir.RotatedBy(MathHelper.PiOver2);
            float wide = DestroyerShots.RayWidth * Projectile.Opacity;

            for (float d = 0f; d < DestroyerShots.RayLength; d += 96f)
                Lighting.AddLight(Projectile.Center + dir * d, 0.35f, 0.62f, 0.62f);

            // Motes off its edges, and its lightning skimming along them, on screen.
            for (int i = 0; i < 2; i++)
            {
                Vector2 on = Projectile.Center + dir * Main.rand.NextFloat(40f, 1600f);

                if (!OnScreen(on))
                    continue;

                float edge = Main.rand.NextBool() ? 1f : -1f;
                PADParticleHandler.SpawnParticle(new BloomGeneric(on + across * edge * wide * 0.45f, across * edge * Main.rand.NextFloat(0.8f, 2.4f) + dir * 2f,
                    DestroyerArt.TealHot * 0.7f, 0.12f, Main.rand.Next(14, 24)) { Pixelate = true });
            }

            if (Age % 4 == 0)
            {
                Vector2 a = Projectile.Center + dir * Main.rand.NextFloat(60f, 1200f);

                if (OnScreen(a))
                {
                    float edge = Main.rand.NextBool() ? 1f : -1f;
                    Lightning.Spawn(a + across * edge * wide * 0.4f, a + dir * Main.rand.NextFloat(70f, 160f) + across * edge * wide * Main.rand.NextFloat(0.5f, 0.9f),
                        DestroyerArt.Teal, 6f, 10, 0, pixelate: true);
                }
            }
        }

        private static bool OnScreen(Vector2 at) =>
            at.X > Main.screenPosition.X - 60f && at.X < Main.screenPosition.X + Main.screenWidth + 60f &&
            at.Y > Main.screenPosition.Y - 60f && at.Y < Main.screenPosition.Y + Main.screenHeight + 60f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (hidden || Projectile.Opacity < 0.5f)
                return false;

            Vector2 from = Projectile.Center;
            Vector2 to = from + Projectile.rotation.ToRotationVector2() * DestroyerShots.RayLength;
            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, DestroyerShots.RayWidth * 0.7f, ref along);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (hidden || Projectile.Opacity <= 0.01f)
                return false;

            Vector2 from = Projectile.Center;
            float angle = Projectile.rotation;
            Vector2 dir = angle.ToRotationVector2();
            Vector2 to = from + dir * DestroyerShots.RayLength;
            float age = Age;
            float wide = DestroyerShots.RayWidth * Projectile.Opacity * (1f + 0.05f * (float)Math.Sin(age * 1.3f));

            PADPixelCanvas.Queue(batch =>
            {
                DestroyerArt.DrawLine(batch, from, to, wide * 2.4f, DestroyerArt.Teal * 0.22f);
                DestroyerArt.DrawLine(batch, from, to, wide * 1.25f, DestroyerArt.Teal * 0.6f);
                DestroyerArt.DrawLine(batch, from, to, wide * 0.7f, DestroyerArt.TealHot * 0.85f);
                DestroyerArt.DrawLine(batch, from, to, wide * 0.3f, Color.White);

                // Its pulses: brighter knots running out along it.
                for (int k = 0; k < 8; k++)
                {
                    float d = (age * 26f + k * 300f) % DestroyerShots.RayLength;
                    DestroyerArt.DrawOval(batch, from + dir * d, angle, 200f, wide * 0.95f, Color.White * 0.22f);
                }

                DestroyerArt.DrawGlow(batch, from, wide * 3.2f, DestroyerArt.Teal * 0.55f);
                DestroyerArt.DrawGlow(batch, from, wide * 1.4f, Color.White * 0.8f, hotCore: true);
            }, additive: true);

            return false;
        }
    }

    public class DestroyerArc : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private Vector2 To => new(Projectile.ai[0], Projectile.ai[1]);

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 14;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Age++;

            if (Age == 1 && !Main.dedServ)
            {
                Lightning.Spawn(Projectile.Center, To, DestroyerArt.Teal, 12f, 14, 1);
                Lightning.Spawn(Projectile.Center, To, Color.White * 0.7f, 4f, 8, 0);
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.35f, Pitch = Main.rand.NextFloat(0f, 0.4f), MaxInstances = 4 }, Projectile.Center);
            }
        }

        public override bool CanHitPlayer(Player target) => Age <= 6;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, To, 20f, ref along);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    public class ProbeBlast : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private float Radius => Math.Max(32f, Projectile.ai[0]);

        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 12;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Age++;

            if (Age == 1 && !Main.dedServ)
                Burst(Projectile.Center, Radius);
        }

        public override bool CanHitPlayer(Player target) => Age <= 5;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Radius;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>The burst: a great flash, its lightning thrown out of it every way and forking, sparks, smoke, thunder.</summary>
        private static void Burst(Vector2 at, float radius)
        {
            void Add(PADParticle particle)
            {
                particle.Pixelate = true;
                PADParticleHandler.SpawnParticle(particle);
            }

            Add(new BloomGeneric(at, Vector2.Zero, DestroyerArt.Teal * 0.9f, radius / 60f, 26));
            Add(new BloomGeneric(at, Vector2.Zero, Color.White * 0.85f, radius / 150f, 14));

            for (int i = 0; i < 11; i++)
            {
                Vector2 end = at + Main.rand.NextVector2Unit() * radius * Main.rand.NextFloat(0.6f, 1.35f);
                Lightning.Spawn(at, end, i % 3 == 0 ? Color.White * 0.8f : DestroyerArt.Teal, Main.rand.NextFloat(12f, 20f), Main.rand.Next(18, 26), 2);
            }

            SparkShard.Spray(at, DestroyerArt.Teal, 22, 13f, 40f, 20, falls: true,
                palette: new[] { DestroyerArt.Teal, DestroyerArt.TealHot, Color.White });

            for (int i = 0; i < 8; i++)
                Add(new FlameSmoke(at + Main.rand.NextVector2Circular(24f, 24f), Main.rand.NextVector2Circular(2.4f, 2.4f), new Color(40, 52, 56), Main.rand.Next(40, 64), 14f, 46f, 0.6f));

            Lighting.AddLight(at, 1.2f, 2f, 2f);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.1f, MaxInstances = 4 }, at);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 1f, Pitch = -0.3f, MaxInstances = 4 }, at);
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.45f, MaxInstances = 2 }, at);

            Player me = Main.LocalPlayer;
            float near = Vector2.Distance(me.Center, at);

            if (near < 1400f)
            {
                me.PIS().BumpScreenShake(14f * (1f - near / 1400f), 22);

                if (near < 700f)
                    Cinematic.Flash(DestroyerArt.Teal, 0.08f * (1f - near / 700f));
            }
        }
    }
}
