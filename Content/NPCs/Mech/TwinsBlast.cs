using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
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
    /// The Twins' end: the fireball where they meet (by request: "a giant fire ball explosion of flamable gas and fire"). It
    /// hurts for its first few ticks, anywhere within nine tiles of its middle (asked after: a big hit, about twice their
    /// touch, dodgeable); then it is only seen, for two seconds more.
    ///
    /// Seen (its fire the new fire system's, PADFire): the gas going up first (a green flash), then the fire: a white heart, a great orange ball of billowing flame
    /// that climbs as it burns out, black smoke rolling up off the top of it into a column, embers and burning pieces of
    /// them thrown up and falling. The gas the Spazmatism trailed coming in catches after it, a run of flame back along its
    /// way. No rings, nothing thrown evenly all round (by request): it billows and climbs. Every machine sees it from its
    /// own spawning; only the server spawns it.
    /// </summary>
    public class TwinsBlast : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private const int Hurts = 6;
        private const int Lasts = 130;

        /// <summary>The flame's run back along the gas: this many pixels a tick.</summary>
        private const float CatchSpeed = 26f;

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>The gas trail as this machine saw it, and how far along it the flame has run.</summary>
        private readonly List<Vector2> gas = new();
        private int caught;

        private static readonly Color Heart = new(255, 248, 220);
        private static readonly Color Yellow = new(255, 214, 120);
        private static readonly Color Orange = new(255, 128, 40);
        private static readonly Color Red = new(206, 56, 18);
        private static readonly Color Soot = new(24, 18, 16);
        private static readonly Color BurnRed = new(150, 44, 18);

        private static readonly Color[] Flames = { Heart, Yellow, Orange, new(255, 170, 70), Red };

        /// <summary>
        /// The damage: about twice their touch (by request). As vanilla counts a boss's shots (doubled in a normal world,
        /// four times in Expert, six in Master), so fifty here is a hundred there. (It was 110/95 at first, counted as if
        /// it were not multiplied: more than twice what was asked.)
        /// </summary>
        public static int Damage(NPC npc) => npc.GetAttackDamage_ForProjectiles(50f, 48f);

        public static void Spawn(NPC source, Vector2 at)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Projectile.NewProjectile(source.GetSource_FromAI(), at, Vector2.Zero, ModContent.ProjectileType<TwinsBlast>(), Damage(source), 0f, Main.myPlayer);
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lasts;
        }

        public override bool CanHitPlayer(Player target) => Age <= Hurts;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Twins.BlastReach;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Age++;

            if (Main.dedServ)
                return;

            Vector2 at = Projectile.Center;
            int t = (int)Age;

            if (t == 1)
            {
                gas.AddRange(Twins.GasTrail);
                gas.Sort((a, b) => Vector2.DistanceSquared(a, at).CompareTo(Vector2.DistanceSquared(b, at)));
                Twins.GasTrail.Clear();
                Burst(at);
            }

            Burn(at, t);
            Catch(at, t);
        }

        private static void Add(PADParticle particle)
        {
            particle.Pixelate = true;
            PADParticleHandler.SpawnParticle(particle);
        }

        private static float Room => MathHelper.Clamp(PADParticleHandler.FreeSlots / 400f, 0.25f, 1f);

        /// <summary>The moment it goes up.</summary>
        private void Burst(Vector2 at)
        {
            float room = Room;
            float r = Twins.BlastReach;

            // The gas first, a heartbeat of green; then the fire.
            Add(new BloomGeneric(at, Vector2.Zero, Twins.Toxic * 0.8f, 3.4f, 10));
            Add(new BloomGeneric(at, Vector2.Zero, Heart * 0.95f, 2.6f, 22));
            Add(new BloomGeneric(at, Vector2.Zero, Orange * 0.8f, 5.2f, 40));
            Add(new BloomGeneric(at, Vector2.Zero, Red * 0.6f, 7f, 56));

            // The ball: the new fire (PADFire), a great burst of it and more through it, climbing.
            PADFire.Burst(at, r * 0.95f, FirePalette.Fire, 2.4f);

            for (int i = 0; i < 5; i++)
                PADFire.Burst(at + Main.rand.NextVector2Circular(r * 0.6f, r * 0.45f), r * 0.5f, FirePalette.Fire, 0.7f, smokes: false);

            PADFire.Flame(at, Vector2.Zero, r * 3.2f, 9, FirePalette.Toxic, 0f, 0.8f, round: true);

            // Black smoke rolling up off it.
            for (int i = 0; i < (int)(22 * room); i++)
            {
                Vector2 off = Main.rand.NextVector2Circular(r * 0.6f, r * 0.45f) + new Vector2(0f, -r * 0.25f);
                Vector2 v = new(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1f, 3f));
                Add(new BurnSmoke(at + off, v, Soot, BurnRed, Main.rand.Next(70, 120), Main.rand.NextFloat(30f, 50f), Main.rand.NextFloat(90f, 150f), Main.rand.NextFloat(0.8f, 0.95f)));
            }

            // Embers thrown up out of it, and falling.
            for (int i = 0; i < (int)(40 * room); i++)
            {
                Vector2 v = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.4f)).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(4f, 13f);
                Add(new Ember(at + Main.rand.NextVector2Circular(r * 0.4f, r * 0.3f), v, Flames[Main.rand.Next(1, Flames.Length)], Main.rand.NextFloat(0.6f, 1.1f), Main.rand.Next(40, 80), lift: -0.12f));
            }

            for (int i = 0; i < 40; i++)
            {
                Dust d = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(r * 0.6f, r * 0.5f), DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(2f, 9f)), 0, default, Main.rand.NextFloat(1.6f, 2.6f));
                d.noGravity = Main.rand.NextBool(3);
            }

            for (int i = 0; i < 16; i++)
            {
                Dust d = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(r * 0.5f, r * 0.4f), DustID.CursedTorch,
                    new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(2f, 7f)), 0, default, Main.rand.NextFloat(1.4f, 2.2f));
                d.noGravity = true;
            }

            // Their pieces, thrown up and out of it.
            Pieces(at, 146, 143);
            Pieces(at, 145, 144);

            Lighting.AddLight(at, 3f, 1.8f, 0.6f);

            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f, Pitch = -0.45f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.7f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 1f, Pitch = -0.3f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 3 }, at);

            Player me = Main.LocalPlayer;
            float near = Vector2.Distance(me.Center, at);

            if (near < 2200f)
            {
                me.PIS().BumpScreenShake(30f * (1f - near / 2200f), 50);
                Cinematic.Flash(new Color(255, 200, 130), 0.55f * (1f - near / 2200f));
            }
        }

        /// <summary>A twin's pieces (vanilla's own: its eye's halves, its nerve, its iris) thrown up out of the fire.</summary>
        private void Pieces(Vector2 at, int iris, int part)
        {
            int[] gores = { 2, 7, 9, iris, part };

            foreach (int g in gores)
            {
                Vector2 v = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.4f, 1.2f)).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(5f, 11f);
                Gore.NewGore(Projectile.GetSource_FromThis(), at + Main.rand.NextVector2Circular(30f, 30f), v, g);
            }
        }

        /// <summary>After: the ball climbing and burning out, the smoke column going up off it, embers raining.</summary>
        private static void Burn(Vector2 at, int t)
        {
            if (t > 70)
                return;

            float left = 1f - t / 70f;
            float room = Room;
            float r = Twins.BlastReach;
            Vector2 top = at + new Vector2(0f, -t * 1.6f);

            PADFire.Burn(top + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * r * left, 0f), r * 0.4f * left + 14f, 3.2f * left, FirePalette.Fire, smokes: false);

            if (t % 3 == 0 && Main.rand.NextFloat() < room)
            {
                // The column: out of the top of it, spreading as it climbs.
                Vector2 v = new(Main.rand.NextFloat(-0.8f, 0.8f) * (1.5f - left), -Main.rand.NextFloat(1.4f, 2.6f));
                Add(new BurnSmoke(top + Main.rand.NextVector2Circular(40f, 20f), v, Soot, BurnRed, Main.rand.Next(90, 140), Main.rand.NextFloat(34f, 52f), Main.rand.NextFloat(110f, 170f), 0.85f));
            }

            if (Main.rand.NextFloat() < 0.8f * left)
                Add(new Ember(top + Main.rand.NextVector2Circular(r * 0.6f, r * 0.4f), new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2.5f)), Flames[Main.rand.Next(1, Flames.Length)], Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(30, 60)));

            Lighting.AddLight(top, 2.6f * left, 1.4f * left, 0.45f * left);
        }

        /// <summary>The gas trailed in catching: the flame runs back along it, a puff of fire at each place as it gets there.</summary>
        private void Catch(Vector2 at, int t)
        {
            int sounds = 0;

            while (caught < gas.Count)
            {
                Vector2 g = gas[caught];

                if (Vector2.Distance(g, at) > (t - 2) * CatchSpeed)
                    break;

                caught++;

                PADFire.Burst(g + Main.rand.NextVector2Circular(14f, 14f), 46f, FirePalette.Fire, 0.3f, smokes: false);
                Add(new BloomGeneric(g, Vector2.Zero, Twins.Toxic * 0.45f, 0.6f, 10, lights: false));

                if (Main.rand.NextBool(2))
                    Add(new FlameSmoke(g, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.6f, 1.4f)), new Color(36, 30, 26), Main.rand.Next(40, 70), 14f, 44f, 0.7f));

                Dust d = Dust.NewDustPerfect(g, DustID.Torch, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 4f)), 0, default, 2f);
                d.noGravity = true;
                Lighting.AddLight(g, 1.4f, 0.8f, 0.3f);

                if (sounds++ == 0 && caught % 4 == 1)
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = Main.rand.NextFloat(-0.4f, 0f), MaxInstances = 4 }, g);
            }
        }
    }
}
