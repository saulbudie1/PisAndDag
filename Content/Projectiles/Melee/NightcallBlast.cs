using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Something of Nightcall's going up (ai[0] how far it reaches, ai[1] which): its cannon's blast where a blow lands
    /// (by request: "create an explosion dealing additional damage ontop of the fist damage"), or its charge's (by request:
    /// "explode radius 7 tiles"). Everything within reach is hit once, in its first ticks, knocked away from its middle;
    /// then it is only seen. Its fire is the fire system's (PADFire) in Skeletron Prime's green, as PrimeBurst's is: the
    /// cannon's a heart of it; the charge's a heart, a front going out, and a stem and cap climbing after, the ground under
    /// it heaved. Never a ring.
    /// </summary>
    public class NightcallBlast : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        public const int Cannon = 0, Core = 1;

        /// <summary>The ticks it hurts in.</summary>
        private const int Hurts = 3;

        private float Reach => Math.Max(16f, Projectile.ai[0]);

        private int Kind => (int)Projectile.ai[1];

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>Its size as a share of Skeletron Prime's own end's (what PrimeBurst's fire is sized by).</summary>
        private float Share => Reach / Prime.BlastReach;

        private int ClimbTicks => Kind == Core ? (int)(40f + 70f * Share) : 0;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 200;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanCutTiles() => false;

        public override bool? CanDamage() => Age <= Hurts ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Reach;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
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

            if (Kind == Core)
            {
                Front(at, t);
                Climb(at, t);
            }
        }

        /// <summary>The moment it goes up.</summary>
        private void Up(Vector2 at)
        {
            float r = Reach, k = Share, room = Prime.Room;
            bool core = Kind == Core;

            if (core)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearHot * 0.95f, 6f * k, (int)(12 + 14 * k)));
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 10f * k, (int)(22 + 28 * k)));
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearDeep * 0.7f, 15f * k, (int)(34 + 46 * k)));

                for (int i = 0; i < 4; i++)
                    PADFire.Burst(at + Main.rand.NextVector2Circular(r * 0.3f, r * 0.26f), Main.rand.NextFloat(66f, 86f), FirePalette.Nuclear, 0.55f, smokes: i < 2);
            }
            else
            {
                Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearHot * 0.9f, 0.5f + 5f * k, 10));
                Prime.Add(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.7f, 0.9f + 8f * k, 18));
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

            // The ground under the charge's heaved.
            if (core && Ground(at, r, out Vector2 foot))
                GroundWave.Slam(foot, 20f, (int)(r / 16f * 0.8f), 8f + 16f * k);

            Lighting.AddLight(at, 0.9f + 2f * k, 1.2f + 2.8f * k, 0.1f);

            if (core)
            {
                SoundEngine.PlaySound(PrimeBlast.NukeSound with { Volume = 0.6f, Pitch = 0.3f }, at);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.3f, MaxInstances = 3 }, at);
            }
            else
            {
                Prime.Sound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.1f, PitchVariance = 0.25f, MaxInstances = 6 }, at, 6);
            }

            Player me = Main.LocalPlayer;
            float felt = 1f - MathHelper.Clamp((Vector2.Distance(me.Center, at) - 300f) / 1400f, 0f, 1f);

            if (felt > 0.01f)
            {
                me.PIS().BumpScreenShake((core ? 12f : 2.5f) * felt, core ? 24 : 6);

                if (core)
                    Cinematic.Flash(new Color(238, 255, 200), 0.16f * felt);
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

        /// <summary>The charge's fire going out: a front of it, further each tick, to as far as it hurt.</summary>
        private void Front(Vector2 at, int t)
        {
            const int ticks = 7;

            if (t < 2 || t > ticks)
                return;

            float r = Reach, k = Share;
            float reach = r * (0.3f + 0.62f * (t - 1) / (ticks - 1));
            float turn = Main.rand.NextFloat(MathHelper.TwoPi);

            for (int i = 0; i < 3; i++)
            {
                Vector2 dir = (turn + i * MathHelper.TwoPi / 3 + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2();
                PADFire.Burst(at + new Vector2(dir.X, dir.Y * 0.85f) * reach * Main.rand.NextFloat(0.85f, 1.05f), Main.rand.NextFloat(52f, 70f) + 36f * k, FirePalette.Nuclear, 0.45f, smokes: i == 0);
            }

            Lighting.AddLight(at, 1.4f + k, 1.9f + 1.4f * k, 0.1f);
        }

        /// <summary>The charge's climbs after: a stem of fire, a cap over it, smoke off both.</summary>
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
}
