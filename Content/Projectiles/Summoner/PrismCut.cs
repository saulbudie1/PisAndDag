using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// The cut a Terraprisma blade leaves where it went in its opening (TerraprismaOpener): a line of light from where it
    /// waited to where it came out behind its target. It cuts as it is made: everything on its line is hit once at once,
    /// for the blade's damage, with a sound to make the skin prickle (by request: "terraprisma streak should instantly deal
    /// damage before exploding making mind tingling sounds"; PrismTingle1-3, made in code: a glass bell's chord
    /// shimmering, a shower of tiny high pings, a breath of air). Then it hangs a moment (<see cref="Fuse"/> ticks),
    /// gathering, and bursts along all its length: everything it crosses as it bursts is hit once more, for twice the
    /// blade's damage (by request: "leaving a streak of slash that explode that deals double damage"). The blade waits
    /// behind its target until it bursts (TerraprismaOpener).
    ///
    ///   ai[0], ai[1]   where it ends (it starts where it is)
    ///   ai[2]          the blade's colour, packed (<see cref="Pack"/>)
    ///
    /// A blade's shot as the game counts a minion's (whips' marks count on it); summon damage; in Legends 1.15 times as
    /// hard, as the blades are (EmpressWeapons). Never drawn itself: what is seen is particles, laid on every machine as it
    /// is made and as it bursts (PrismCutLine, the burst's flashes, rings and sparks, on the half-size canvas).
    /// </summary>
    public class PrismCut : ModProjectile
    {
        /// <summary>Its burst's damage, as many times the blade's (its cut's is the blade's).</summary>
        public const int Times = 2;

        /// <summary>Ticks its cut can hit (as it reaches its length); the tick it bursts; ticks it can hit as it bursts; ticks after, while what is left of it goes.</summary>
        public const int Cutting = 3, Fuse = 18, Burst = 3, After = 24;

        /// <summary>The cut's sound: three, so that blades cutting together do not ring the same.</summary>
        public static readonly SoundStyle Tingle = new("PisAndDag/Content/Sounds/PrismTingle", 3)
        {
            Volume = 0.75f,
            PitchVariance = 0.06f,
            MaxInstances = 4,
        };

        /// <summary>How far either side of its line it reaches.</summary>
        private const float Reach = 22f;

        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>A colour as a whole number a float holds exactly (24 bits).</summary>
        public static float Pack(Color colour) => colour.R | colour.G << 8 | colour.B << 16;

        private static Color Unpack(float packed)
        {
            int n = (int)packed;
            return new Color(n & 255, n >> 8 & 255, n >> 16 & 255);
        }

        private Vector2 From => Projectile.Center;

        private Vector2 To => new(Projectile.ai[0], Projectile.ai[1]);

        private Color Hue => Unpack(Projectile.ai[2]);

        /// <summary>Its ticks, from 1 on its first.</summary>
        private int Age => (int)Projectile.localAI[1];

        private bool Cuts => Age >= 1 && Age <= Cutting;

        private bool Bursting => Age >= Fuse && Age < Fuse + Burst;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.MinionShot[Type] = true;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Fuse + Burst + After + 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[1]++;

            if (Age == 1)
                Opened();

            // Bursting: whatever the cut hit can be hit again.
            if (Age == Fuse)
            {
                for (int i = 0; i < Projectile.localNPCImmunity.Length; i++)
                    Projectile.localNPCImmunity[i] = 0;

                Bursts();
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Cuts && !Bursting)
                return false;

            // As it cuts, as far as it has reached; as it bursts, all of it.
            Vector2 end = Cuts ? Vector2.Lerp(From, To, Math.Min(1f, Age / (float)Cutting)) : To;
            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), From, end, Reach * 2f, ref along);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Bursting)
                modifiers.SourceDamage *= Times;

            if (PADWorld.Legends)
                modifiers.SourceDamage *= EmpressWeapons.LegendsDamage;

            modifiers.HitDirectionOverride = To.X >= From.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Cuts)
                HitFx.Slash(target.Center, To - From, Hue, Math.Min(90f, 30f + Math.Max(target.width, target.height) * 0.6f));
        }

        public override bool PreDraw(ref Color lightColor) => false;

        // ---- what is seen ----------------------------------------------------------------------------------------------

        private static void Add(PADParticle particle)
        {
            particle.Pixelate = true;
            PADParticleHandler.SpawnParticle(particle);
        }

        /// <summary>The cut made: its line, a flash where the blade left and where it came out, the ribbon of her colours along it.</summary>
        private void Opened()
        {
            if (Main.dedServ)
                return;

            Vector2 way = To - From;
            float angle = way.ToRotation();
            Color hue = Hue;

            Add(new PrismCutLine(From, To, hue, Fuse + 2));
            Add(new BloomGeneric(From, Vector2.Zero, hue * 0.7f, 0.4f, 10));
            Add(new HitRing(From, Color.Lerp(hue, Color.White, 0.4f), 4f, 26f, 10));
            Add(new BloomGeneric(To, Vector2.Zero, hue * 0.9f, 0.55f, 12));
            Add(new HitRing(To, Color.Lerp(hue, Color.White, 0.5f), 6f, 40f, 14, 0.5f, angle));

            int ribbons = Math.Clamp((int)(way.Length() / 40f), 3, 16);

            for (int i = 0; i < ribbons; i++)
            {
                Vector2 at = Vector2.Lerp(From, To, (i + 0.5f) / ribbons);
                Add(new PrismStreak(at, way.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(0.5f, 2f), angle, way.Length() / ribbons * 1.4f, 6f,
                    EmpressArt.Prism(i / (float)ribbons + Main.rand.NextFloat(0.1f)), Main.rand.Next(12, 20)));
            }

            SoundEngine.PlaySound(Tingle, Vector2.Lerp(From, To, 0.5f));
        }

        /// <summary>The cut bursting: flashes and rings along it in her colours, a star at its middle, sparks thrown off it either side, what is left of it falling apart.</summary>
        private void Bursts()
        {
            if (Main.dedServ)
                return;

            Vector2 way = To - From;
            float length = way.Length();
            Vector2 along = way.SafeNormalize(Vector2.UnitX);
            Vector2 across = along.RotatedBy(MathHelper.PiOver2);
            Color hue = Hue;
            int bursts = Math.Clamp((int)(length / 48f), 3, 14);

            for (int i = 0; i < bursts; i++)
            {
                float t = (i + 0.5f) / bursts;
                Vector2 at = Vector2.Lerp(From, To, t) + across * Main.rand.NextFloat(-6f, 6f);
                Color colour = Color.Lerp(hue, EmpressArt.Prism(t + Main.rand.NextFloat(0.2f)), 0.5f);

                Add(new BloomGeneric(at, Vector2.Zero, colour * 0.85f, 0.5f, 12));
                Add(new HitRing(at, Color.Lerp(colour, Color.White, 0.35f), 4f, 30f + Main.rand.NextFloat(14f), 14, 0.55f, along.ToRotation()));

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 velocity = (across * side).RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 9f);
                    Add(new SparkShard(at, velocity, colour, 8f, 3f, Main.rand.NextFloat(18f, 30f), Main.rand.Next(10, 18)));
                }

                // What is left of the line, in pieces drifting apart.
                Add(new PrismStreak(at, across * Main.rand.NextFloat(-1.5f, 1.5f) + along * Main.rand.NextFloat(-0.5f, 0.5f), along.ToRotation(),
                    length / bursts * 0.8f, 5f, colour, Main.rand.Next(14, 24)));
            }

            Vector2 middle = Vector2.Lerp(From, To, 0.5f);
            Add(new ImpactStar(middle, Color.Lerp(hue, Color.White, 0.6f), Math.Min(160f, 60f + length * 0.15f), 14));
            Add(new HitRing(To, Color.Lerp(hue, Color.White, 0.4f), 8f, 70f, 18));

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.4f, Volume = 0.5f }, middle);
        }
    }
}
