using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    // ============================================================
    // THE SHOCKWAVE: out along the ground either way from where he comes down
    // ============================================================

    /// <summary>
    /// A wave of gold running along the ground from where the knight came down (ai[0]: the way,
    /// 1 or -1; ai[1]: 1 for the long blade's, bigger): it follows the lie of the ground, heaves
    /// the turf as it passes, and throws leaves. Jumped over.
    /// </summary>
    public class KnightShock : KnightShot
    {
        protected override int[] Table => GildedKnight.ShockDamage;
        protected override string Death => "GildedKnightShock";
        protected override float Knock => 7f;

        private const int Life = 54;
        private const float Speed = 8.5f;

        private bool Big => Projectile.ai[1] > 0.5f;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 26;
            Projectile.height = 30;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            int way = Projectile.ai[0] >= 0f ? 1 : -1;
            Projectile.velocity = new Vector2(way * Speed * (Big ? 1.25f : 1f), 0f);

            // Its foot on the ground: up a step, down a step; gone where the ground ends or a wall stands.
            float foot = Projectile.Bottom.Y;
            int x = (int)((Projectile.Center.X + way * 10f) / 16f);
            int y = (int)((foot - 2f) / 16f);

            if (!WorldGen.InWorld(x, y, 4))
            {
                Projectile.Kill();
                return;
            }

            if (Solid(x, y) && Solid(x, y - 1) && Solid(x, y - 2))
            {
                Projectile.Kill();
                return;
            }

            if (Solid(x, y))
                Projectile.position.Y -= 16f;
            else if (!Solid(x, y + 1) && Solid(x, y + 2))
                Projectile.position.Y += 16f;
            else if (!Solid(x, y + 1) && !Solid(x, y + 2))
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            if (Projectile.timeLeft % 3 == 0)
            {
                Vector2 at = Projectile.Bottom;
                KnightFx.Dust(at, Big ? 0.8f : 0.5f);

                if (Main.rand.NextBool(2))
                    KnightFx.Sparks(at + new Vector2(0f, -6f), new Vector2(way * 0.3f, -1f), 1, KnightFx.Gold, 4f, 0.6f);
            }

            if (Projectile.timeLeft % 6 == 0)
                GroundWave.Slam(Projectile.Bottom, 6f, 1, Big ? 6f : 4f, 8);

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.75f, 0.35f) * 0.6f);
        }

        private static bool Solid(int x, int y)
        {
            Tile t = Main.tile[x, y];
            return t.HasTile && !t.IsActuated && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);
        }

        protected override bool Touches(Player me) => Projectile.Hitbox.Intersects(me.Hitbox);

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            int way = Projectile.ai[0] >= 0f ? 1 : -1;
            float life = Projectile.timeLeft / (float)Life;
            float a = Math.Min(1f, life * 3f);
            float h = Big ? 46f : 34f;

            // A standing crescent of light leaning forward, a hot foot on the ground.
            Texture2D tex = Crescent;
            Vector2 at = Projectile.Bottom - Main.screenPosition;
            float rot = way * MathHelper.PiOver2 - way * 0.25f;

            for (int k = 2; k >= 0; k--)
            {
                Vector2 back = new Vector2(-way * k * 10f, 0f);
                float fade = a * (1f - k * 0.3f);
                sb.Draw(tex, at + back + new Vector2(0f, -h * 0.5f), null, Light(KnightFx.Gold, 0.9f * fade), rot, new Vector2(tex.Width * 0.5f, tex.Height * 0.6f),
                    new Vector2(h / tex.Width * 1.6f, h / tex.Height * 1.4f), SpriteEffects.None, 0f);
            }

            DrawGlow(sb, Projectile.Bottom + new Vector2(0f, -8f), 70f, Light(KnightFx.Gold, 0.45f * a));
            DrawGlow(sb, Projectile.Bottom + new Vector2(0f, -4f), 26f, Light(KnightFx.White, 0.7f * a));
            return false;
        }
    }

    // ============================================================
    // THE CRESCENT: a cut of light let go
    // ============================================================

    /// <summary>
    /// A crescent of light off a blade (Rellana's moon crescents), going straight the way it was
    /// sent (velocity), its horns back; ai[0]: 0 the Grace's (pale), 1 the Sun's (warm; on fire in
    /// his second half). A parry turns it aside.
    /// </summary>
    public class KnightCrescent : KnightShot
    {
        protected override int[] Table => GildedKnight.CrescentDamage;
        protected override string Death => "GildedKnightCrescent";

        private const int Life = 90;

        private bool Sun => Projectile.ai[0] > 0.5f;
        private bool Burning => Projectile.ai[1] > 0.5f;

        protected override bool Parried => true;

        public override void SetStaticDefaults()
        {
            Parry.AddProjectile(Type, p => p.timeLeft > 4, (p, by) => p.Kill());
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.timeLeft == Life - 1 && !Main.dedServ)
                KnightSounds.Play(KnightSounds.Crescent, Projectile.Center);

            if (Main.dedServ)
                return;

            Color c = Sun ? (Burning ? KnightFx.Fire : KnightFx.Gold) : (Burning ? KnightFx.White : KnightFx.Pale);
            Lighting.AddLight(Projectile.Center, c.ToVector3() * 0.7f);

            if (Main.rand.NextBool(2))
            {
                Vector2 side = new Vector2(0f, Main.rand.NextFloat(-26f, 26f)).RotatedBy(Projectile.rotation);

                if (Sun && Burning)
                    KnightFx.Embers(Projectile.Center + side, 1, 4f, 1.5f);
                else
                    PADParticleHandler.SpawnParticle(new GraceMote(Projectile.Center + side, c, Main.rand.Next(30, 60)));
            }
        }

        protected override bool Touches(Player me) => InCircle(me, Projectile.Center, 26f);

        protected override void OnLanded(Player me)
        {
            KnightFx.Sparks(me.Center, Projectile.velocity, 10, KnightFx.Gold, 5f, 0.8f);
            KnightFx.Bloom(me.Center, KnightFx.Gold * 0.6f, 0.4f, 14);
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            KnightFx.Bloom(Projectile.Center, KnightFx.Gold * 0.5f, 0.45f, 18);
            KnightFx.Motes(Projectile.Center, 6, 20f, KnightFx.Pale);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = Crescent;
            float life = Projectile.timeLeft / (float)Life;
            float a = Math.Min(1f, life * 4f) * Math.Min(1f, (Life - Projectile.timeLeft + 1) / 4f);

            Color outer = Sun ? (Burning ? KnightFx.Fire : KnightFx.Gold) : (Burning ? KnightFx.Pale : KnightFx.Gold);
            Color inner = Sun ? (Burning ? new Color(255, 214, 120) : KnightFx.Pale) : KnightFx.White;

            // The texture's arc bulges up: turned so it bulges the way the crescent goes.
            float rot = Projectile.rotation + MathHelper.PiOver2;
            Vector2 origin = new Vector2(tex.Width * 0.5f, tex.Height * 0.55f);
            Vector2 scale = new Vector2(1.05f, 0.85f);

            // A wake of fainter copies behind it.
            for (int k = 4; k >= 1; k--)
            {
                Vector2 back = -Projectile.velocity * k * 1.6f;
                sb.Draw(tex, Projectile.Center + back - Main.screenPosition, null, Light(outer, 0.18f * a * (5 - k)), rot, origin, scale * (1f - k * 0.05f), SpriteEffects.None, 0f);
            }

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, Light(outer, 0.95f * a), rot, origin, scale * 1.08f, SpriteEffects.None, 0f);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, Light(inner, 0.9f * a), rot, origin, scale * 0.88f, SpriteEffects.None, 0f);
            DrawGlow(sb, Projectile.Center, 120f, Light(outer, 0.3f * a));
            return false;
        }
    }

    // ============================================================
    // THE LANCET: a lance of light down the line
    // ============================================================

    /// <summary>
    /// The lancet: from the Grace's point down the line it was aimed along (ai[0], a world angle),
    /// a lance of light the length of the field, or to the first wall it meets. It strikes in its
    /// first ticks, hangs, and narrows away, bursting where it ends.
    /// </summary>
    public class KnightLancet : KnightShot
    {
        protected override int[] Table => GildedKnight.LancetDamage;
        protected override string Death => "GildedKnightLancet";
        protected override float Knock => 7f;

        private const int Life = 30;
        private const float MaxReach = 1600f;
        private const float Width = 22f;

        /// <summary>How far it goes (worked out once, on its first tick, on each machine).</summary>
        private float length = -1f;

        /// <summary>Ticks since it was let go of; it strikes after ai[1] of them (the second half's two beside the first), its line shown till then.</summary>
        private int age;

        private int Delay => (int)Math.Max(0f, Projectile.ai[1]);

        protected override bool Live => age > Delay && age <= Delay + 12;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.timeLeft = Life + 30;
        }

        private Vector2 Along => new Vector2((float)Math.Cos(Projectile.ai[0]), (float)Math.Sin(Projectile.ai[0]));

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            age++;

            if (age == 1)
                Projectile.timeLeft = Life + Delay + 1;

            if (age <= Delay)
            {
                if (length < 0f)
                    length = Reach(Projectile.Center, Along);

                return;
            }

            if (age == Delay + 1)
            {
                length = Reach(Projectile.Center, Along);

                if (!Main.dedServ)
                {
                    Vector2 end = Projectile.Center + Along * length;
                    KnightSounds.Play(KnightSounds.LancetFire, Projectile.Center);
                    KnightFx.Bloom(end, KnightFx.White, 0.9f, 30);
                    KnightFx.Glint(end, 50f, 30);
                    KnightFx.Sparks(end, -Along, 14, KnightFx.Pale, 6f, 1.2f);
                    KnightFx.Bloom(Projectile.Center, KnightFx.Gold, 0.7f, 20);
                    KnightFx.Shake(Projectile.Center, 5f, 12);

                    for (float d = 30f; d < length; d += 46f)
                        PADParticleHandler.SpawnParticle(new GraceMote(Projectile.Center + Along * d + Main.rand.NextVector2Circular(10f, 10f), KnightFx.Pale, Main.rand.Next(40, 80)));
                }
            }

            if (!Main.dedServ)
            {
                for (float d = 0f; d < length; d += 120f)
                    Lighting.AddLight(Projectile.Center + Along * d, new Vector3(1f, 0.95f, 0.75f) * 0.8f);
            }
        }

        /// <summary>From a point down a way to the first solid tile, at most so far.</summary>
        private static float Reach(Vector2 from, Vector2 along)
        {
            for (float d = 0f; d < MaxReach; d += 8f)
            {
                Point tile = (from + along * d).ToTileCoordinates();

                if (!WorldGen.InWorld(tile.X, tile.Y, 2) || WorldGen.SolidTile(tile.X, tile.Y))
                    return d;
            }

            return MaxReach;
        }

        protected override bool Touches(Player me) => length > 0f && OnLine(me, Projectile.Center, Projectile.Center + Along * length, Width);

        public override bool PreDraw(ref Color lightColor)
        {
            if (length <= 0f)
                return false;

            SpriteBatch sb = Main.spriteBatch;

            // Waiting its turn: a thread of light down its line.
            if (age <= Delay)
            {
                float w = age / (float)Math.Max(1, Delay);
                DrawLine(sb, Line, Projectile.Center, Projectile.Center + Along * length, 2f + 2f * w, Light(KnightFx.Pale, 0.3f + 0.4f * w));
                return false;
            }

            float t = MathHelper.Clamp((age - Delay) / (float)Life, 0f, 1f);
            float strike = t < 0.15f ? t / 0.15f : 1f;
            float thin = 1f - Math.Max(0f, (t - 0.3f) / 0.7f);
            Vector2 from = Projectile.Center;
            Vector2 to = from + Along * length * strike;

            DrawLine(sb, Beam, from, to, 46f * thin, Light(KnightFx.Gold, 0.55f));
            DrawLine(sb, Beam, from, to, 22f * thin, Light(KnightFx.Pale, 0.9f));
            DrawLine(sb, Line, from, to, 8f * thin, Light(KnightFx.White, 1f));
            DrawGlow(sb, to, 160f * thin, Light(KnightFx.Pale, 0.6f));
            DrawGlow(sb, from, 110f * thin, Light(KnightFx.Gold, 0.6f));
            return false;
        }
    }

    // ============================================================
    // A VOTIVE BLADE: a blade of light that stands in the air and is loosed
    // ============================================================

    /// <summary>
    /// A blade of light, the Grace's shape in gold, standing in the air over the knight (ai[2]: his
    /// whoAmI; ai[0]: its place in the row, from 0; ai[1]: the tick it is loosed, counted from when
    /// it came), turning to point at whoever he fights; loosed, it goes straight at where they were
    /// and stands in what it strikes. A parry turns it aside. For the crown (localAI[0] 1), its place
    /// is in a ring over his head.
    /// </summary>
    public class KnightVotive : KnightShot
    {
        protected override int[] Table => GildedKnight.VotiveDamage;
        protected override string Death => "GildedKnightVotive";

        private const float Speed = 17f;
        private const int Stuck = 26;

        public override string Texture => KnightRig.Folder + "GraceBlade";

        /// <summary>Ticks since it came; and once it is stuck in something, ticks since.</summary>
        private int age;
        private int stuckFor = -1;

        private bool Flying => age >= (int)Projectile.ai[1] && stuckFor < 0;

        protected override bool Live => Flying;

        protected override bool Parried => true;

        public override void SetStaticDefaults()
        {
            Parry.AddProjectile(Type, p => p.ModProjectile is KnightVotive v && v.Flying, (p, by) => p.Kill());
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.timeLeft = 600;
        }

        /// <summary>The ring of the crown: localAI set by whoever spawned it on the server; every machine reads it off ai[1]'s size.</summary>
        private bool Crowned => Projectile.ai[0] >= 100f;

        private int Index => (int)Projectile.ai[0] % 100;

        public override void AI()
        {
            age++;

            // Gone with him: his end (or his leaving) takes what he called up but had not loosed.
            GildedKnight owner = Owner();
            NPC knight = owner?.NPC;
            bool alive = owner != null;

            if (age == 1 && !Main.dedServ)
            {
                KnightSounds.Play(KnightSounds.Votive with { Pitch = 0.1f * Index }, Projectile.Center);
                KnightFx.Bloom(Projectile.Center, KnightFx.Pale, 0.4f, 20);
                KnightFx.Glint(Projectile.Center, 26f, 20);
            }

            if (stuckFor >= 0)
            {
                stuckFor++;
                Projectile.velocity = Vector2.Zero;

                if (stuckFor > Stuck)
                    Projectile.Kill();

                return;
            }

            if (!Flying)
            {
                if (!alive)
                {
                    Projectile.Kill();
                    return;
                }

                // Its place over him, and turned toward them.
                GildedKnight k = (GildedKnight)knight.ModNPC;
                Vector2 home = Place(k, knight);
                Projectile.Center = Vector2.Lerp(Projectile.Center, home, 0.2f);
                Projectile.velocity = Vector2.Zero;

                Player target = k.Target();
                Vector2 to = target != null ? target.Center - Projectile.Center : new Vector2(k.Faces, 0f);
                float want = to.ToRotation();
                Projectile.rotation = Projectile.rotation.AngleLerp(want, age < 2 ? 1f : 0.18f);

                if (age == (int)Projectile.ai[1] - 1)
                {
                    Projectile.velocity = Projectile.rotation.ToRotationVector2() * Speed;

                    if (!Main.dedServ)
                    {
                        KnightSounds.Play(KnightSounds.VotiveLoose, Projectile.Center);
                        KnightFx.Bloom(Projectile.Center, KnightFx.Gold, 0.35f, 12);
                    }
                }

                return;
            }

            if (Projectile.velocity.LengthSquared() < 1f)
                Projectile.velocity = Projectile.rotation.ToRotationVector2() * Speed;

            Projectile.rotation = Projectile.velocity.ToRotation();

            // Stands in what it strikes.
            Point tip = (Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 30f).ToTileCoordinates();

            if (WorldGen.InWorld(tip.X, tip.Y, 2) && WorldGen.SolidTile(tip.X, tip.Y))
            {
                stuckFor = 0;

                if (!Main.dedServ)
                {
                    KnightSounds.Play(KnightSounds.VotiveHit, Projectile.Center);
                    KnightFx.Sparks(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 30f, -Projectile.velocity, 8, KnightFx.Gold, 4f, 1f);
                }

                return;
            }

            if (age > (int)Projectile.ai[1] + 120)
                Projectile.Kill();

            if (!Main.dedServ && Main.rand.NextBool(2))
                PADParticleHandler.SpawnParticle(new GraceMote(Projectile.Center, KnightFx.Pale, Main.rand.Next(20, 40)));
        }

        /// <summary>Where it stands over him: in a row behind and over his head, or in the crown's ring.</summary>
        private Vector2 Place(GildedKnight k, NPC knight)
        {
            Vector2 head = k.Feet + new Vector2(0f, -100f);

            if (Crowned)
            {
                float a = -MathHelper.PiOver2 + (Index - 3f) * 0.36f + (float)Math.Sin(age * 0.03f) * 0.04f;
                return head + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * 64f + new Vector2(0f, 10f);
            }

            return head + new Vector2(-k.Faces * (30f + Index * 30f), -Index * 10f + (float)Math.Sin(age * 0.06f + Index) * 3f);
        }

        protected override bool Touches(Player me)
        {
            Vector2 along = Projectile.rotation.ToRotationVector2();
            return OnLine(me, Projectile.Center - along * 30f, Projectile.Center + along * 34f, 12f);
        }

        protected override void OnLanded(Player me)
        {
            KnightFx.Sparks(me.Center, Projectile.velocity, 8, KnightFx.Pale, 5f, 0.8f);
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft)
        {
            if (!Main.dedServ)
                KnightFx.Motes(Projectile.Center, 5, 16f, KnightFx.Pale);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = Tex(KnightRig.Folder + "GraceBladeGlow");
            Texture2D body = Tex(KnightRig.Folder + "GraceBlade");

            float fade = stuckFor >= 0 ? 1f - stuckFor / (float)Stuck : Math.Min(1f, age / 10f);
            float rot = Projectile.rotation + MathHelper.PiOver2;

            // The middle of the blade at the projectile's centre: the picture is turned about its blade's middle.
            Vector2 origin = new Vector2(body.Width * 0.5f, 34f);
            Vector2 at = Projectile.Center - Main.screenPosition;

            if (Flying)
            {
                for (int k = 3; k >= 1; k--)
                    sb.Draw(body, at - Projectile.velocity * k * 0.9f, null, Light(KnightFx.Gold, 0.18f * fade * (4 - k)), rot, origin, 1f, SpriteEffects.None, 0f);
            }

            sb.Draw(body, at, null, Light(KnightFx.Gold, 0.85f * fade), rot, origin, 1f, SpriteEffects.None, 0f);
            sb.Draw(tex, at, null, Light(KnightFx.White, 1f * fade), rot, origin, 1f, SpriteEffects.None, 0f);
            DrawGlow(sb, Projectile.Center, 90f, Light(KnightFx.Gold, 0.35f * fade));
            return false;
        }
    }

    // ============================================================
    // A PYRE: fire out of the ground where a mark was
    // ============================================================

    /// <summary>
    /// A mark of fire on the ground (a ring, the turf glowing, a hum), and after ai[0] ticks a pillar
    /// of fire bursting up out of it. His change's, and the sunwheel's.
    /// </summary>
    public class KnightPyre : KnightShot
    {
        protected override int[] Table => GildedKnight.PyreDamage;
        protected override string Death => "GildedKnightPyre";
        protected override float Knock => 6f;

        private const int Burn = 40;
        private const float Wide2 = 30f;
        private const float High = 190f;

        private int age;

        private int Delay => (int)Math.Max(10f, Projectile.ai[0]);

        protected override bool Live => age >= Delay && age < Delay + 14;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 40;
            Projectile.height = 16;
            Projectile.timeLeft = 400;
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            if (age == 1 && !Main.dedServ)
                KnightSounds.Play(KnightSounds.PyreMark, Projectile.Bottom);

            if (age == Delay && !Main.dedServ)
            {
                KnightSounds.Play(KnightSounds.Pyre, Projectile.Bottom);
                KnightFx.Embers(Projectile.Bottom + new Vector2(0f, -40f), 24, 18f, 4f);
                KnightFx.Bloom(Projectile.Bottom + new Vector2(0f, -60f), KnightFx.Fire, 0.9f, 30);
                KnightFx.Dust(Projectile.Bottom, 0.9f);
                GroundWave.Slam(Projectile.Bottom, 14f, 2, 6f, 12);
                KnightFx.Shake(Projectile.Bottom, 3f, 10);
            }

            if (age >= Delay + Burn)
                Projectile.Kill();

            if (Main.dedServ)
                return;

            if (age < Delay)
            {
                if (Main.rand.NextBool(4))
                    KnightFx.Embers(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f), 1, 2f, 0.6f);

                Lighting.AddLight(Projectile.Bottom, new Vector3(1f, 0.55f, 0.2f) * 0.4f);
            }
            else
            {
                if (Main.rand.NextBool())
                    KnightFx.Embers(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -Main.rand.NextFloat(10f, High)), 1, 4f, 2.5f);

                Lighting.AddLight(Projectile.Bottom + new Vector2(0f, -60f), new Vector3(1f, 0.6f, 0.25f) * 1.2f);
            }
        }

        protected override bool Touches(Player me)
        {
            Rectangle pillar = new Rectangle((int)(Projectile.Bottom.X - Wide2 * 0.5f), (int)(Projectile.Bottom.Y - High), (int)Wide2, (int)High);
            return pillar.Intersects(me.Hitbox);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 foot = Projectile.Bottom;

            if (age < Delay)
            {
                // The mark: a ring flat on the ground, tightening, brightening.
                float t = age / (float)Delay;
                Texture2D ring = RingTex;
                float across = MathHelper.Lerp(70f, 34f, t);
                sb.Draw(ring, foot - Main.screenPosition + new Vector2(0f, -2f), null, Light(KnightFx.Fire, 0.4f + 0.5f * t), 0f, ring.Size() * 0.5f,
                    new Vector2(across / ring.Width, across * 0.28f / ring.Height), SpriteEffects.None, 0f);
                DrawGlow(sb, foot + new Vector2(0f, -4f), 40f + 30f * t, Light(KnightFx.Fire, 0.25f + 0.35f * t));
                return false;
            }

            float b = (age - Delay) / (float)Burn;
            float rise = Math.Min(1f, b * 6f);
            float fade = 1f - Math.Max(0f, (b - 0.4f) / 0.6f);
            Texture2D pillar = Pillar;
            Texture2D flame = Flame;

            sb.Draw(pillar, foot - Main.screenPosition, null, Light(KnightFx.Fire, 0.9f * fade), 0f, new Vector2(pillar.Width * 0.5f, pillar.Height),
                new Vector2(Wide2 * 1.8f / pillar.Width, High * rise / pillar.Height), SpriteEffects.None, 0f);
            sb.Draw(pillar, foot - Main.screenPosition, null, Light(new Color(255, 230, 170), 0.8f * fade), 0f, new Vector2(pillar.Width * 0.5f, pillar.Height),
                new Vector2(Wide2 * 0.8f / pillar.Width, High * rise * 0.9f / pillar.Height), SpriteEffects.None, 0f);

            for (int k = 0; k < 3; k++)
            {
                float wob = (float)Math.Sin(age * 0.4f + k * 2.1f) * 6f;
                sb.Draw(flame, foot - Main.screenPosition + new Vector2(wob + (k - 1) * 10f, 0f), null, Light(KnightFx.Ember, 0.7f * fade), 0f,
                    new Vector2(flame.Width * 0.5f, flame.Height), new Vector2(0.9f, (0.9f + 0.3f * k) * rise), SpriteEffects.None, 0f);
            }

            DrawGlow(sb, foot + new Vector2(0f, -High * 0.4f), 200f, Light(KnightFx.Fire, 0.35f * fade));
            return false;
        }
    }
}
