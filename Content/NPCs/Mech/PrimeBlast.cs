using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// Skeletron Prime's end: what it had shut in its mouth goes up (by request: "creating a giant explosion and an area
    /// that is radio active for 5 mins"). It hurts for its first few ticks, anywhere within thirty tiles of its middle
    /// (asked after: a big hit, dodgeable: about three times its head's touch); then it is only seen. What it hits
    /// is left standing in radioactive ground (PrimeFallout), which is where the rest of the harm is.
    ///
    /// Seen (its fire the fire system's, PADFire, in its green): everything white for a moment; a ball of its green
    /// fire that goes out to as far as it hurt over a few ticks, a front of it; then it climbs: a stem of fire and
    /// smoke up from where it was, a cap spreading out over it, its green arcs in the cap; the ground under it thrown
    /// out to both sides along it; its head's pieces and its arms' thrown up out of it; embers of it raining after.
    /// No ring (by request): it goes out as fire does, and climbs. Every machine sees it from its own spawning; only
    /// the server spawns it.
    /// </summary>
    public class PrimeBlast : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private const int Hurts = 8;
        private const int Climbs = 120;
        private const int Lasts = 240;

        /// <summary>Ticks its fire takes to go out to as far as it hurt.</summary>
        private const int FrontTicks = 10;

        public static readonly SoundStyle NukeSound = new("PisAndDag/Content/Sounds/Mech/PrimeNuke") { Volume = 1f, MaxInstances = 2 };

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>The ground under it, in world pixels (this machine's; none if it went up over a chasm).</summary>
        private float ground = -1f;

        private static readonly Color Soot = new(20, 22, 14);

        /// <summary>
        /// Its damage: about three times its head's touch (asked after: a big hit). As vanilla counts a boss's shots
        /// (doubled in a normal world, four times in Expert, six in Master), so seventy here is a hundred and forty there.
        /// </summary>
        public static int Damage(NPC npc) => npc.GetAttackDamage_ForProjectiles(70f, 66f);

        public static void Spawn(NPC source, Vector2 at)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Projectile.NewProjectile(source.GetSource_FromAI(), at, Vector2.Zero, ModContent.ProjectileType<PrimeBlast>(), Damage(source), 0f, Main.myPlayer);
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
            return Vector2.Distance(near, Projectile.Center) <= Prime.BlastReach;
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
                Burst(at);

            Hold(at, t);

            Front(at, t);
            Climb(at, t);
            Rain(at, t);
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
            float r = Prime.BlastReach;

            // Its light: three great glows, each wider and longer than the last. Smooth, not on the two-pixel grid: at
            // this size the grid would show them as squares.
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearHot * 0.95f, 6f, 26));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 10f, 50));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, PrimeArt.NuclearDeep * 0.7f, 15f, 80));

            // Its heart: a ball of its green fire, of many fires no bigger than any other fire is (so their pixels
            // are the grid's own), close about its middle.
            for (int i = 0; i < 10; i++)
                PADFire.Burst(at + Main.rand.NextVector2Circular(r * 0.3f, r * 0.26f), Main.rand.NextFloat(100f, 130f), FirePalette.Nuclear, 0.55f, smokes: i < 3);

            // Its head's pieces and its arms' (vanilla's own), thrown up and out of it.
            foreach (int g in new[] { 149, 150, 147, 148, 147, 148, 147, 148, 147, 148 })
            {
                Vector2 v = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.3f, 1.2f)).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(7f, 15f);
                Gore.NewGore(Projectile.GetSource_FromThis(), at + Main.rand.NextVector2Circular(40f, 40f), v, g);
            }

            for (int i = 0; i < 6; i++)
            {
                Vector2 v = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.2f, 1f)) * Main.rand.NextFloat(2f, 6f);
                Gore.NewGore(Projectile.GetSource_FromThis(), at + Main.rand.NextVector2Circular(60f, 60f), v, Main.rand.Next(61, 64));
            }

            // Embers of it thrown up out of it, and falling.
            for (int i = 0; i < (int)(70 * room); i++)
            {
                Vector2 v = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.3f, 1.4f)).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(5f, 17f);
                Add(new HoneyMote(at + Main.rand.NextVector2Circular(r * 0.2f, r * 0.2f), v, Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear,
                    Main.rand.NextFloat(0.6f, 1.1f), Main.rand.Next(50, 100), -0.1f));
            }

            for (int i = 0; i < 50; i++)
            {
                Dust d = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(r * 0.4f, r * 0.4f), DustID.CursedTorch,
                    new Vector2(Main.rand.NextFloat(-7f, 7f), -Main.rand.NextFloat(2f, 11f)), 0, default, Main.rand.NextFloat(1.8f, 3f));
                d.noGravity = Main.rand.NextBool(3);
            }

            Thrown(at, room);

            Lighting.AddLight(at, 2.9f, 4f, 0.2f);

            SoundEngine.PlaySound(NukeSound, at);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f, Pitch = -0.6f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.9f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 1f, Pitch = -0.5f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1f, Pitch = -0.6f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, at);

            Player me = Main.LocalPlayer;
            float near = Vector2.Distance(me.Center, at);

            if (near < 3600f)
            {
                float felt = 1f - MathHelper.Clamp((near - 1400f) / 2200f, 0f, 1f);
                me.PIS().BumpScreenShake(44f * felt, 80);
                Cinematic.Flash(new Color(238, 255, 200), felt);
            }
        }

        /// <summary>Ticks it keeps its head's end shot on (Prime.EndFrame) after it goes up: till its cloud has all but stopped climbing; then the camera is walked home.</summary>
        private const int ShotTicks = 150;
        private const int ShotHome = 50;

        /// <summary>The end's shot, kept on: over its cloud's cap as it climbs, the player in it.</summary>
        private static void Hold(Vector2 at, int t)
        {
            if (t > ShotTicks || !Prime.Watching(at))
                return;

            if (t == ShotTicks)
            {
                PisPlayer.PanHome(at, ShotHome);
                return;
            }

            float high = Prime.BlastReach * 1.15f * (1f - MathF.Exp(-t / 45f));
            PisPlayer.BlendCamera(Prime.EndFrame(at, Math.Max(Prime.EndAboveDark, high + Prime.CapRoom)), 1f);
        }

        /// <summary>The ground under it thrown out to both sides along it: dust rolling away, stone up and out. Not a ring: along the ground.</summary>
        private void Thrown(Vector2 at, float room)
        {
            int x = (int)(at.X / 16f), y0 = (int)(at.Y / 16f);

            for (int y = y0; y < y0 + 44; y++)
            {
                if (!WorldGen.InWorld(x, y, 2))
                    break;

                Tile tile = Main.tile[x, y];

                if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType])
                {
                    ground = y * 16f;
                    break;
                }
            }

            if (ground < 0f)
                return;

            Vector2 foot = new(at.X, ground);
            Color dust = new(126, 130, 96);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 spout = new Vector2(side * 0.9f, -1f).SafeNormalize(-Vector2.UnitY);

                for (int i = 0; i < (int)(26 * room); i++)
                    Add(new DustFog(foot + new Vector2(side * Main.rand.NextFloat(0f, 80f), -8f), new Vector2(side * Main.rand.NextFloat(5f, 18f), -Main.rand.NextFloat(0.2f, 1.8f)),
                        dust * 0.85f, Main.rand.Next(50, 100), Main.rand.NextFloat(0.9f, 1.7f), 0.3f));

                for (int i = 0; i < (int)(18 * room); i++)
                    Add(new StoneChip(foot + new Vector2(side * Main.rand.NextFloat(10f, 90f), -6f), spout.RotatedByRandom(0.5f) * Main.rand.NextFloat(5f, 15f),
                        Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0.1f, 0.5f)), Main.rand.Next(50, 90), Main.rand.NextFloat(0.9f, 1.6f)));
            }

            int gy = (int)(ground / 16f);

            for (int i = 0; i < 30; i++)
            {
                int tx = x + Main.rand.Next(-9, 10), ty = gy + Main.rand.Next(0, 2);

                if (!WorldGen.InWorld(tx, ty, 2) || !Main.tile[tx, ty].HasTile)
                    continue;

                int d = WorldGen.KillTile_MakeTileDust(tx, ty, Main.tile[tx, ty]);

                if (d >= 0 && d < Main.maxDust)
                    Main.dust[d].velocity = new Vector2(Math.Sign(tx - x + 0.5f) * Main.rand.NextFloat(2f, 8f), -Main.rand.NextFloat(2f, 9f));
            }
        }

        /// <summary>Its fire going out: a front of it, further each tick, to as far as it hurt.</summary>
        private static void Front(Vector2 at, int t)
        {
            if (t < 2 || t > FrontTicks)
                return;

            float r = Prime.BlastReach;
            float reach = r * (0.3f + 0.62f * (t - 1) / (float)(FrontTicks - 1));

            // Four fires a tick, a quarter turn apart, the four turned by a chance amount each tick: all round it over
            // its going out, and never a ring.
            float turn = Main.rand.NextFloat(MathHelper.TwoPi);

            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = (turn + i * MathHelper.PiOver2 + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2();
                PADFire.Burst(at + new Vector2(dir.X, dir.Y * 0.85f) * reach * Main.rand.NextFloat(0.85f, 1.05f), Main.rand.NextFloat(96f, 124f), FirePalette.Nuclear, 0.5f, smokes: i == 0);
            }

            Lighting.AddLight(at, 2.2f, 3f, 0.1f);
        }

        /// <summary>After: it climbs. A stem of fire up from where it was, a cap spreading over it, smoke off both, its arcs in the cap.</summary>
        private static void Climb(Vector2 at, int t)
        {
            if (t < 6 || t > Climbs)
                return;

            float left = 1f - (t - 6f) / (Climbs - 6f);
            float room = Room;
            float r = Prime.BlastReach;
            float high = r * 1.15f * (1f - MathF.Exp(-t / 45f));
            float wide = 60f + 2.3f * t;
            Vector2 cap = at + new Vector2(0f, -high);

            // Its stem.
            for (int i = 0; i < 3; i++)
                PADFire.Burn(at + new Vector2(Main.rand.NextFloat(-40f, 40f), -Main.rand.NextFloat(0f, high)), 30f, 1.5f * left, FirePalette.Nuclear, smokes: false);

            // Its cap: wider as it climbs, thickest at its middle.
            for (int i = 0; i < 3; i++)
                PADFire.Burn(cap + new Vector2(Main.rand.NextFloat(-wide, wide), Main.rand.NextFloat(-34f, 30f)), 44f, 2.2f * left, FirePalette.Nuclear, smokes: false);

            PADFire.Burn(cap + new Vector2(Main.rand.NextFloat(-wide, wide) * 0.5f, Main.rand.NextFloat(-56f, 10f)), 48f, 2.4f * left, FirePalette.Nuclear, smokes: false);

            if (t % 2 == 0 && Main.rand.NextFloat() < room)
            {
                // Smoke off its cap's edges, rolling out and up; and up its stem.
                float side = Main.rand.NextBool() ? 1f : -1f;
                Add(new BurnSmoke(cap + new Vector2(side * wide * Main.rand.NextFloat(0.6f, 1.1f), Main.rand.NextFloat(-30f, 30f)),
                    new Vector2(side * Main.rand.NextFloat(0.6f, 2f), -Main.rand.NextFloat(0.4f, 1.6f)), Soot, PrimeArt.NuclearDeep, Main.rand.Next(90, 150),
                    Main.rand.NextFloat(40f, 60f), Main.rand.NextFloat(130f, 200f), 0.85f));

                Add(new BurnSmoke(at + new Vector2(Main.rand.NextFloat(-50f, 50f), -Main.rand.NextFloat(0f, high)),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.2f, 2.6f)), Soot, PrimeArt.NuclearDeep, Main.rand.Next(70, 120),
                    Main.rand.NextFloat(30f, 46f), Main.rand.NextFloat(90f, 150f), 0.8f));
            }

            if (t % 6 == 0 && t < 96)
            {
                Vector2 from = cap + new Vector2(Main.rand.NextFloat(-wide, wide), Main.rand.NextFloat(-40f, 40f));
                Lightning.Spawn(from, from + Main.rand.NextVector2Circular(wide * 0.7f, 90f), PrimeArt.Nuclear, 14f, Main.rand.Next(10, 16), Main.rand.Next(1, 3));

                if (t % 18 == 0)
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f * left, Pitch = Main.rand.NextFloat(-0.4f, 0.1f), MaxInstances = 4 }, from);
            }

            float k = 3f * left;
            Lighting.AddLight(cap, 0.73f * k, 1f * k, 0.03f * k);
            Lighting.AddLight(at, 0.73f * k, 1f * k, 0.03f * k);
        }

        /// <summary>Embers of it coming down over all it reached, for a while after.</summary>
        private static void Rain(Vector2 at, int t)
        {
            if (t < 20 || t > Lasts - 40)
                return;

            float r = Prime.BlastReach;
            float left = 1f - (t - 20f) / (Lasts - 60f);

            for (int n = Main.rand.NextFloat() < 2.4f * left * Room - 1f ? 2 : 1; n > 0; n--)
            {
                if (Main.rand.NextFloat() > 1.2f * left)
                    continue;

                Vector2 from = at + new Vector2(Main.rand.NextFloat(-r, r), -Main.rand.NextFloat(r * 0.2f, r));

                if (!PADFire.Near(from, 60f))
                    continue;

                Add(new HoneyMote(from, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.4f, 1.6f)), Main.rand.NextBool(4) ? PrimeArt.NuclearHot : PrimeArt.Nuclear,
                    Main.rand.NextFloat(0.4f, 0.75f), Main.rand.Next(60, 110), -0.03f));
            }
        }
    }
}
