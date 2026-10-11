using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.NPCs.Foigh;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// A straight cut out of the ground where the drenched Rust Side Sword was driven in
    /// (RustSideHoldout, the drive): Foigh's own when his is driven deeper (FoighLine), the
    /// player's. By request: "plunge deep and creates piercing AOE straight slashes". A
    /// straight line at a slant of its own through the ground and the air: a hair of dark
    /// red that brightens for the ticks it shows, and then, all along its length at once,
    /// open: a cut of the burning sword, red with its black twin, hanging a moment.
    ///
    /// It does not move. Open, for <see cref="Opens"/> ticks, it cuts every enemy it lies
    /// through, through walls and ground (it is not stopped by them), each once; the
    /// sword's damage, through all defense. "All slashes": it goes through everything, its
    /// own count of who it has hit (every <see cref="RustSideSlash.HitCooldown"/> ticks, more
    /// than it is open), and it is gone well inside two and a half seconds.
    ///
    /// What it cuts is left bleeding (UltraBleedingGeneric): there is no straight cut but the
    /// drenched sword's.
    ///
    /// ai[0] the way it lies (radians); ai[1] how far it runs each side of its middle;
    /// ai[2] the ticks it shows before it opens. How long it hangs after is sent with it.
    /// </summary>
    public class RustSideLine : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>Ticks one shows before it opens, the fewest and the most; ticks it cuts for; ticks it hangs after, the fewest and the most.</summary>
        public const int ShowsLeast = 12;
        public const int ShowsMost = 22;
        public const int Opens = 3;
        public const int HangsLeast = 6;
        public const int HangsMost = 28;

        /// <summary>How thick it is where it cuts, in pixels.</summary>
        public const float Width = 14f;

        private static Asset<Texture2D> softLine;

        /// <summary>Ticks it hangs after it has cut: as it was let out, sent with it.</summary>
        private int hangs = (HangsLeast + HangsMost) / 2;

        /// <summary>The ticks this machine has seen of it.</summary>
        private int Age => (int)Projectile.localAI[1];

        private int Shows => Math.Clamp((int)Projectile.ai[2], 1, ShowsMost);

        public bool Open => Age > Shows && Age <= Shows + Opens;

        /// <summary>Its two ends.</summary>
        public static void Ends(Projectile projectile, out Vector2 from, out Vector2 to)
        {
            Vector2 half = projectile.ai[0].ToRotationVector2() * projectile.ai[1];

            from = projectile.Center - half;
            to = projectile.Center + half;
        }

        /// <summary>What a line is let out by: the sword, and how long it hangs.</summary>
        private sealed class Hanging : EntitySource_Parent
        {
            public readonly int Hangs;

            public Hanging(Entity by, int hangs) : base(by, null)
            {
                Hangs = hangs;
            }
        }

        /// <summary>
        /// Lets one out, on the sword's owner's machine: lying <paramref name="turn"/> (radians)
        /// through <paramref name="middle"/>, <paramref name="half"/> either side of it; shown
        /// <paramref name="shows"/> ticks before it opens and hanging <paramref name="hangs"/>
        /// after; cutting for <paramref name="damage"/>.
        /// </summary>
        public static void Let(Projectile sword, Vector2 middle, float turn, float half, int shows, int hangs, int damage, float knockback)
        {
            Projectile.NewProjectile(new Hanging(sword, hangs), middle, Vector2.Zero, ModContent.ProjectileType<RustSideLine>(), damage, knockback, sword.owner,
                turn, half, shows);
        }

        public override void OnSpawn(IEntitySource source)
        {
            if (source is Hanging hanging)
                hangs = hanging.Hangs;

            Projectile.timeLeft = Shows + Opens + hangs + 2;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)hangs);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            hangs = reader.ReadByte();
        }

        public override void Load()
        {
            if (!Main.dedServ)
                softLine = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            softLine = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = RustSideSlash.HitCooldown;
            Projectile.timeLeft = ShowsMost + Opens + HangsMost + 2;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            int age = Age + 1;

            Projectile.localAI[1] = age;
            Projectile.velocity = Vector2.Zero;

            if (age > Shows + Opens + hangs)
            {
                Projectile.Kill();
                return;
            }

            if (age == Shows + 1)
                Opened();

            if (age > Shows)
            {
                float left = 1f - (age - Shows - 1) / (float)(Opens + hangs);
                Vector2 along = Projectile.ai[0].ToRotationVector2();

                for (int i = -2; i <= 2; i++)
                    Lighting.AddLight(Projectile.Center + along * (Projectile.ai[1] * i * 0.4f), 0.6f * left, 0.06f * left, 0.05f * left);
            }
        }

        public override bool? CanDamage() => Open ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Ends(Projectile, out Vector2 from, out Vector2 to);

            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, Width, ref point);
        }

        public override void CutTiles()
        {
            if (!Open)
                return;

            Ends(Projectile, out Vector2 from, out Vector2 to);

            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(from, to, Width, DelegateMethods.CutTiles);
        }

        /// <summary>Through all defense, and away from it, whichever side of it they are on.</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.ScalingArmorPenetration += 1f;

            Vector2 along = Projectile.ai[0].ToRotationVector2();
            Vector2 across = new Vector2(-along.Y, along.X);
            float side = Vector2.Dot(target.Center - Projectile.Center, across);
            float away = across.X * Math.Sign(side);

            modifiers.HitDirectionOverride = Math.Abs(away) > 0.2f ? Math.Sign(away) : target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // The drenched sword's, as every one is: it leaves them bleeding (UltraBleedingGeneric).
            UltraBleedingGeneric.Cut(target);

            if (Main.dedServ)
                return;

            Vector2 along = Projectile.ai[0].ToRotationVector2();
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);

            BloodSlash.Pair(target.Center, along, 30f + Math.Min(target.width, target.height) * 0.4f, 5f, 16);
            BloodDroplet.SpawnBurst(at, new Vector2(-along.Y, along.X), 6, 46f);
        }

        /// <summary>The tick it opens, on a machine with a screen: seen and heard all along it.</summary>
        private void Opened()
        {
            if (Main.dedServ)
                return;

            Ends(Projectile, out Vector2 from, out Vector2 to);

            Vector2 along = Projectile.ai[0].ToRotationVector2();
            int knots = Math.Max(2, (int)(Projectile.ai[1] * 2f / 64f));

            BloodSlash.Pair(Projectile.Center, along, Projectile.ai[1] + 8f, 12f, 22);

            for (int i = 0; i <= knots; i++)
            {
                Vector2 at = Vector2.Lerp(from, to, i / (float)knots);

                PADParticleHandler.SpawnParticle(new Ember(at, new Vector2(-along.Y, along.X) * Main.rand.NextFloat(-1.6f, 1.6f) + along * Main.rand.NextFloat(-1f, 1f),
                    Foigh.Cinder, Main.rand.NextFloat(0.45f, 0.8f), Main.rand.Next(14, 26)));
            }

            SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Pitch = -0.4f, Volume = 0.55f, PitchVariance = 0.12f, MaxInstances = 3 }, Projectile.Center);
        }

        // ============================================================
        // DRAWING (FoighLine's way)
        // ============================================================

        public override bool PreDraw(ref Color lightColor)
        {
            if (softLine == null || !softLine.IsLoaded)
                return false;

            int age = Age;

            if (age <= 0)
                return false;

            Ends(Projectile, out Vector2 from, out Vector2 to);

            Main.spriteBatch.End();

            if (age <= Shows)
            {
                // Showing: a hair of dark, so that it is seen against a bright wall; and a hair
                // of red along it, brighter by the tick until it opens.
                float shown = MathHelper.Clamp(age / 4f, 0f, 1f);
                float lit = shown * (0.35f + 0.65f * age / Shows);

                Band(from, to, 2.6f, Dark(Foigh.Soot, 0.6f * shown));
                Band(from, to, 7f, Light(Foigh.BurnRed, 0.22f * lit));
                Band(from, to, 1.1f, Light(Foigh.TrailRed, 0.9f * lit));
            }
            else
            {
                // Open: wide and bright at once, then thinner and dimmer to nothing; its black
                // twin beside it, the last of it to go.
                float t = MathHelper.Clamp((age - Shows - 1) / (float)(Opens + hangs), 0f, 1f);
                float left = 1f - Easing.Smoother(t);
                Vector2 along = to - from;
                Vector2 aside = new Vector2(-along.Y, along.X).SafeNormalize(Vector2.UnitY) * 7f;

                Band(from + aside, to + aside, 1.5f + 3f * left, Dark(Foigh.Soot, 0.85f * (1f - t)));
                Band(from, to, 16f * left, Light(Foigh.BurnRed, 0.5f * left));
                Band(from, to, 2f + 4f * left, Light(Foigh.TrailRed, 0.95f * left));
                Band(from, to, 1.4f, Light(Foigh.BurnHot, left));
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);

            return false;
        }

        /// <summary>One band along it, this wide to either side, this colour at its strongest: thinning to nothing at both ends.</summary>
        private static void Band(Vector2 from, Vector2 to, float half, Color colour)
        {
            const int steps = 24;

            Vector2 along = to - from;
            float length = along.Length();

            if (length < 2f || half <= 0f)
                return;

            along /= length;

            Vector2 across = new Vector2(-along.Y, along.X);

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Color here = colour * MathHelper.Clamp(Math.Min(k, 1f - k) / 0.1f, 0f, 1f);
                Vector2 at = from + along * (length * k);

                Strip.Pair(at - across * half, at + across * half, here, here, 0.5f);
            }

            Strip.Draw(softLine.Value);
        }

        private static Color Light(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 0) * strength;

        private static Color Dark(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 255) * strength;
    }
}
