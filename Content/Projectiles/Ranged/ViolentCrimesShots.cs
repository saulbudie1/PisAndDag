using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.NPCs.Mech;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    // ---- what Violent Crimes throws ------------------------------------------------------------------------------------------
    //
    //   ViolentCrimesFlame      a burst of its stream: unseen itself, it lays the new fire (PADFire) along its way; it goes
    //                           as vanilla's flamethrower flame goes (three moves a tick, 72 of them, slowing over the last
    //                           twelve; a hitbox that grows; through four enemies, a little weaker each; stopped by a wall)
    //   ViolentCrimesLaser      its flame laser: a column of fire from its mouth to the first wall, for sixteen ticks
    //   ViolentCrimesGas        its gas: a cloud that slows, swells and hangs, harming nothing, till its own fire or laser
    //                           touches it (or a cloud going up beside it); then it goes up, and hurts
    //   ViolentCrimesChlorine   what its throw leaves: a cloud that hurts what stands in it, and does not burn
    //
    // All its holder's (made on their machine, as any shot of theirs). The fire gives Industrial Flames, the chlorine
    // Chlorine Poisoning.

    /// <summary>
    /// What can light Violent Crimes' gas this tick, gathered once a tick the first time a cloud asks: its flames, its
    /// lasers, and its clouds going up. Nothing else lights it (by request: "flamable only by flame thrower and flame
    /// laser of violent crimes").
    /// </summary>
    internal static class ViolentCrimesFire
    {
        private struct Spot
        {
            public Vector2 At;
            public float Radius;
        }

        private struct Beam
        {
            public Vector2 From, To;
            public float Half;
        }

        private static readonly List<Spot> flames = new(), burning = new();
        private static readonly List<Beam> beams = new();
        private static uint builtOn = uint.MaxValue;

        /// <summary>How fast the fire runs from one cloud to the next (pixels a tick), and how far past touching it reaches.</summary>
        private const float FireRuns = 24f;
        private const float Reach = 26f;

        private static void Gather()
        {
            if (builtOn == Main.GameUpdateCount)
                return;

            builtOn = Main.GameUpdateCount;
            flames.Clear();
            burning.Clear();
            beams.Clear();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                switch (p.ModProjectile)
                {
                    case ViolentCrimesFlame flame:
                        flames.Add(new Spot { At = p.Center, Radius = flame.Radius });
                        break;

                    case ViolentCrimesLaser laser when laser.Burning:
                        beams.Add(new Beam { From = laser.From, To = laser.To, Half = ViolentCrimesLaser.Wide * 0.5f });
                        break;

                    case ViolentCrimesGas gas when gas.Spreading:
                        burning.Add(new Spot { At = p.Center, Radius = gas.Radius });
                        break;
                }
            }
        }

        /// <summary>Whether something lights a cloud of gas this big at this place, and the ticks it then takes to go up.</summary>
        public static bool Lights(Vector2 at, float radius, out int fuse)
        {
            Gather();
            fuse = 1;

            foreach (Spot flame in flames)
            {
                float reach = radius * 0.9f + flame.Radius;

                if (Vector2.DistanceSquared(at, flame.At) <= reach * reach)
                    return true;
            }

            foreach (Beam beam in beams)
            {
                Vector2 along = beam.To - beam.From;
                float length = along.LengthSquared();
                float k = length > 1f ? MathHelper.Clamp(Vector2.Dot(at - beam.From, along) / length, 0f, 1f) : 0f;
                float reach = radius * 0.9f + beam.Half;

                if (Vector2.DistanceSquared(at, beam.From + along * k) <= reach * reach)
                    return true;
            }

            foreach (Spot cloud in burning)
            {
                float far = Vector2.Distance(at, cloud.At);

                if (far <= radius + cloud.Radius + Reach)
                {
                    // The fire takes its time to cross.
                    fuse = 2 + (int)(far / FireRuns) + Main.rand.Next(3);
                    return true;
                }
            }

            return false;
        }
    }

    public class ViolentCrimesFlame : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>Vanilla's flame's clock: moves before it slows, and the moves it slows over (three moves a tick).</summary>
        private const int Burn = 60;
        private const int Dying = 12;
        private const int Life = Burn + Dying;

        /// <summary>How far back along its way it lays fire each tick: the gap to the burst behind it, so the stream has none.</summary>
        private const float LaysBack = ViolentCrimes.FlameSpeed * 3f * ViolentCrimes.FlameEvery;

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>How far it has come.</summary>
        private ref float Gone => ref Projectile.localAI[1];

        /// <summary>How far its heat reaches about its middle (its hitbox, as it grows).</summary>
        internal float Radius => 3f + Utils.Remap(Age, 0f, Life, 10f, 40f);

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 4;
            Projectile.extraUpdates = 2;
            Projectile.ArmorPenetration = 15;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.alpha = 255;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            Age++;

            if (Age >= Life)
            {
                Projectile.Kill();
                return;
            }

            if (Age >= Burn)
                Projectile.velocity *= 0.95f;

            Gone += Projectile.velocity.Length();

            // Water puts it out.
            if (Projectile.wet && !Projectile.lavaWet)
            {
                if (!Main.dedServ)
                    PADFire.Smoke(Projectile.Center, new Vector2(0f, -1.2f), 40f, 40, 0, 0.7f);

                Projectile.Kill();
                return;
            }

            // Once a tick (it moves three times in one).
            if (!Main.dedServ && Projectile.numUpdates == -1)
                Lay();
        }

        /// <summary>
        /// The fire it lays (this machine's): along the last of its way, thin and yellow by the nozzle, wide and red by its
        /// end (each flame born as far through its life as the fuel there is through its own), carried on after it a
        /// little; smoke off its far end, a spark now and then. Worked out outside the game first (scratchpad vc/scenes.py).
        /// </summary>
        private void Lay()
        {
            Vector2 at = Projectile.Center;

            if (!PADFire.Near(at, 320f))
                return;

            float step = Projectile.velocity.Length();

            if (step < 0.05f)
                return;

            Vector2 heading = Projectile.velocity / step;
            Vector2 across = new(-heading.Y, heading.X);
            float back = Math.Min(LaysBack, Gone);
            float aTick = step * 3f;
            int many = PADFire.Density >= 0.9f ? 4 : 3;

            for (int i = 0; i < many; i++)
            {
                float d = Main.rand.NextFloat(back);
                float fuel = Math.Max(0f, (Age - d / step) / Life);
                float side = Main.rand.NextFloat(-1f, 1f);
                float size = MathHelper.Lerp(16f, 112f, fuel) * Main.rand.NextFloat(0.75f, 1.15f);
                float carry = MathHelper.Lerp(0.4f, 0.14f, fuel) * Main.rand.NextFloat(0.7f, 1.2f);
                float begun = 0.6f * fuel;
                int life = Math.Max(4, (int)(Main.rand.Next(9, 16) / (1f - begun)));

                PADFire.Flame(at - heading * d + across * (side * size * 0.14f), heading * (aTick * carry) + across * (side * (0.2f + 1.5f * fuel)),
                    size, life, FirePalette.Fire, 0.02f + 0.08f * fuel, 0.5f, 0, Main.rand.NextFloat() < 0.4f, begun);
            }

            float through = Age / Life;

            if (through > 0.62f && Main.rand.NextFloat() < 0.12f * PADFire.Density)
                PADFire.Smoke(at + new Vector2(Main.rand.NextFloat(-24f, 24f), -24f + Main.rand.NextFloat(-20f, 10f)), heading * (aTick * 0.08f) + new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    Main.rand.NextFloat(54f, 96f), Main.rand.Next(40, 71), 8);

            if (Main.rand.NextFloat() < 0.1f)
                PADFire.Spark(at, heading * (aTick * 0.3f) + new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f)), Main.rand.Next(14, 29));

            float lit = 1f - 0.5f * through;
            Lighting.AddLight(at, 1.3f * lit, 0.62f * lit, 0.16f * lit);
        }

        /// <summary>Its hitbox grows as it goes (vanilla's flame's sum).</summary>
        public override void ModifyDamageHitbox(ref Rectangle hitbox)
        {
            int grow = (int)Utils.Remap(Age, 0f, Life, 10f, 40f);
            hitbox.Inflate(grow, grow);
        }

        /// <summary>Not through a wall (vanilla's flame's rule: a clear line from its middle to theirs).</summary>
        public override bool? CanHitNPC(NPC target) =>
            Collision.CanHit(Projectile.Center, 0, 0, target.Center, 0, 0) ? null : false;

        /// <summary>A wall stops it where it is; it burns out against it (vanilla's flame's).</summary>
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.velocity = oldVelocity * 0.95f;
            Projectile.position -= Projectile.velocity;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // A little weaker for each it has gone through (vanilla's flame's).
            Projectile.damage = (int)(Projectile.damage * 0.85f);
            target.AddBuff(ModContent.BuffType<IndustrialFlames>(), IndustrialFlames.Ticks);

            if (!Main.dedServ)
            {
                for (int i = 0; i < 3; i++)
                    PADFire.Spark(target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f),
                        Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(1.2f) * Main.rand.NextFloat(1.5f, 4f) - new Vector2(0f, 1f), Main.rand.Next(16, 30));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    public class ViolentCrimesLaser : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>How wide it is (two tiles, less its soft edge), how far it can reach, and how long it lasts.</summary>
        public const float Wide = 30f;
        public const float MostReach = 90f * 16f;
        public const int Life = 16;

        private ref float Age => ref Projectile.localAI[0];

        private Vector2 from, way = Vector2.UnitX;
        private float reach;
        private readonly float[] scan = new float[3];

        /// <summary>It is lit, on this machine: what its gas is lit by.</summary>
        internal bool Burning => Age >= 1f && reach > 0f;

        internal Vector2 From => from;

        internal Vector2 To => from + way * reach;

        /// <summary>In over three ticks, out over six.</summary>
        private float Strength => Math.Min(1f, Age / 3f) * Math.Min(1f, (Life - Age + 1f) / 6f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ArmorPenetration = 20;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = Life + 4;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Age++;

            if (Age > Life)
            {
                Projectile.Kill();
                return;
            }

            // The way it was fired, kept; from its gun's mouth as that moves (its kick, its holder's run).
            if (Projectile.velocity.LengthSquared() > 0.01f)
                way = Vector2.Normalize(Projectile.velocity);

            Player owner = Main.player[Projectile.owner];

            if (owner.active && !owner.dead && ViolentCrimesHoldout.TryMuzzle(owner, out Vector2 mouth))
            {
                // From the middle of its holder if a wall is between them and its mouth (as it was fired).
                Vector2 middle = owner.MountedCenter;
                Projectile.Center = Collision.CanHit(middle, 0, 0, mouth, 0, 0) ? mouth : middle;
            }

            from = Projectile.Center;

            // To the first wall.
            Collision.LaserScan(from, way, Wide * 0.6f, MostReach, scan);
            float found = (scan[0] + scan[1] + scan[2]) / 3f;
            reach = Age <= 1f ? found : MathHelper.Lerp(reach, found, 0.6f);

            if (Main.dedServ)
                return;

            float strength = Strength;
            PADFire.Jet(from, way, reach, Wide, FirePalette.Fire, strength);

            for (float k = 0f; k < reach; k += 110f)
            {
                Vector2 at = from + way * k;

                if (PADFire.Near(at, 160f))
                    Lighting.AddLight(at, 1.5f * strength, 0.75f * strength, 0.2f * strength);
            }

            // Where a wall stops it: it splashes.
            if (reach < MostReach - 12f)
            {
                Vector2 end = To;
                PADFire.Burn(end - way * 10f, 14f, 2.2f * strength, FirePalette.Fire, smokes: Main.rand.NextBool(3));

                if (Main.rand.NextBool(2))
                    PADFire.Spark(end, -way.RotatedByRandom(1.2f) * Main.rand.NextFloat(2f, 7f), Main.rand.Next(14, 28));
            }
        }

        public override bool? CanDamage() => Age >= 2f && Age <= Life - 3f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (reach <= 0f)
                return false;

            float along = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, To, Wide, ref along);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(ModContent.BuffType<IndustrialFlames>(), IndustrialFlames.Ticks);

            if (!Main.dedServ)
                PADFire.Puff(target.Center, way, 46f, FirePalette.Fire, 0.7f, 0.9f);
        }

        /// <summary>It cuts what grows along it.</summary>
        public override void CutTiles()
        {
            if (reach <= 0f)
                return;

            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(from, To, Wide, DelegateMethods.CutTiles);
        }

        /// <summary>Its glow under its flames (the flames themselves are PADFire's): red wide, orange, a hot line, fading out along its last fifth.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            if (reach <= 0f || Age < 1f)
                return false;

            float strength = Strength;
            float flick = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 43f);
            SpriteBatch batch = Main.spriteBatch;

            Fading(batch, Wide * 2.4f, new Color(206, 52, 14) * (0.24f * strength));
            Fading(batch, Wide * 1.15f, new Color(255, 128, 36) * (0.34f * strength * flick));
            Fading(batch, Wide * 0.36f, new Color(255, 236, 170) * (0.65f * strength * flick));
            DestroyerArt.DrawGlow(batch, from, Wide * 3.6f, DestroyerArt.Lit(new Color(255, 170, 70) * (0.6f * strength)), hotCore: true);
            return false;
        }

        private void Fading(SpriteBatch batch, float wide, Color colour)
        {
            float full = reach * 0.8f;
            DestroyerArt.DrawLine(batch, from, from + way * full, wide, DestroyerArt.Lit(colour));

            const int steps = 6;

            for (int i = 0; i < steps; i++)
            {
                float a = full + (reach - full) * i / steps, b = full + (reach - full) * (i + 1) / steps;
                DestroyerArt.DrawLine(batch, from + way * a, from + way * b, wide, DestroyerArt.Lit(colour * (1f - (i + 0.5f) / steps)));
            }
        }
    }

    public class ViolentCrimesGas : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private const float Hanging = 0f, Lit = 1f, GoingUp = 2f;

        /// <summary>How long it hangs unlit, how long its going up is seen, and how long of that it hurts.</summary>
        public const int Lasts = 600;
        private const int UpTicks = 12;
        private const int HurtTicks = 8;

        /// <summary>The ticks of its going up in which it lights the clouds beside it.</summary>
        private const int SpreadTicks = 8;

        private ref float State => ref Projectile.ai[0];

        private ref float Fuse => ref Projectile.ai[1];

        private ref float Age => ref Projectile.localAI[0];

        private ref float Up => ref Projectile.localAI[1];

        /// <summary>How big it is: swelling as it hangs.</summary>
        internal float Radius => 28f + 52f * Twins.Smooth(Age / 90f);

        /// <summary>Going up, and still lighting those beside it.</summary>
        internal bool Spreading => State == GoingUp && Up <= SpreadTicks;

        /// <summary>Hanging, unlit (what its holder's count of clouds is kept by).</summary>
        internal bool Unlit => State == Hanging;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 8;
            Projectile.timeLeft = Lasts;
        }

        public override void AI()
        {
            Age++;
            Projectile.rotation += 0.006f;
            bool mine = Projectile.owner == Main.myPlayer;

            // Its holder's machine says when it is lit, and when it goes up.
            if (mine && State == Hanging && Age > 3f && ViolentCrimesFire.Lights(Projectile.Center, Radius, out int fuse))
            {
                State = Lit;
                Fuse = fuse;
                Projectile.netUpdate = true;
            }

            if (mine && State == Lit && --Fuse <= 0f)
            {
                State = GoingUp;
                Projectile.netUpdate = true;
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

            Projectile.velocity *= 0.94f;

            if (Main.dedServ)
                return;

            float r = Radius;

            if ((int)Age % 6 == 0 && Projectile.timeLeft > 40 && PADFire.Near(Projectile.Center, 200f))
                PADFire.Gas(Projectile.Center + Main.rand.NextVector2Circular(r * 0.5f, r * 0.5f), Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    r * Main.rand.NextFloat(0.9f, 1.4f), Main.rand.Next(40, 61), 0.7f);
        }

        /// <summary>A wall stops it; it hangs against it.</summary>
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        /// <summary>It harms nothing till it goes up (by request: "non damaging until lit").</summary>
        public override bool? CanDamage() => State == GoingUp && Up >= 1f && Up <= HurtTicks ? null : false;

        public override bool? CanCutTiles() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Radius * 1.2f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Thrown out from its middle.
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
            target.AddBuff(ModContent.BuffType<IndustrialFlames>(), IndustrialFlames.Ticks);

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
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.3f, PitchVariance = 0.3f, MaxInstances = 5 }, at);

            if (Main.rand.NextBool(4))
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 3 }, at);

            Player me = Main.LocalPlayer;
            float near = Vector2.Distance(me.Center, at);

            if (near < 1200f)
                me.PIS().BumpScreenShake(8f * (1f - near / 1200f), 12);
        }

        /// <summary>Its body: pale and yellow-green, thin, on the half-size canvas; orange once it is lit.</summary>
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

    public class ViolentCrimesChlorine : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        public const int Lasts = 200;

        private ref float Age => ref Projectile.localAI[0];

        private float Radius => 30f + 36f * Twins.Smooth(Age / 50f);

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 20;
            Projectile.timeLeft = Lasts;
        }

        public override void AI()
        {
            Age++;
            Projectile.velocity *= 0.95f;
            Projectile.rotation += 0.01f;

            if (Main.dedServ)
                return;

            float r = Radius;

            if ((int)Age % 3 == 0 && Projectile.timeLeft > 30 && PADFire.Near(Projectile.Center, 200f))
                PADFire.Gas(Projectile.Center + Main.rand.NextVector2Circular(r * 0.55f, r * 0.55f), Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    r * Main.rand.NextFloat(0.9f, 1.4f), Main.rand.Next(30, 47));

            Lighting.AddLight(Projectile.Center, 0.16f, 0.45f, 0.14f);
        }

        public override bool? CanDamage() => Age > 4f && Projectile.timeLeft > 24 ? null : false;

        public override bool? CanCutTiles() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Radius * 0.9f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Gas: it throws nothing.
            modifiers.Knockback *= 0f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
            target.AddBuff(ModContent.BuffType<ChlorinePoisoning>(), ChlorinePoisoning.Ticks);

        /// <summary>Its body: three of the fire system's puffs one in another on the half-size canvas, dark green at its rim to a pale heart.</summary>
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
}
