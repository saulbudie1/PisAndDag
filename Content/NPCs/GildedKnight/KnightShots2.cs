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
    // THE GREAT CRESCENT: an arc of fire the size of the sweep, held, and burst
    // ============================================================

    /// <summary>
    /// The great crescent (Rellana's enormous fire arc, and the burst after it): a crescent of fire
    /// round the knight's front, Radius out, hanging in the air where the Sun swept it; it burns
    /// whoever is in its band while it forms; held a breath, it bursts, and the half-circle it
    /// stood round goes up in fire. ai[0]: the way he faces. ai[2]: his whoAmI.
    /// </summary>
    public class KnightGreat : KnightShot
    {
        protected override int[] Table => GildedKnight.GreatDamage;
        protected override string Death => "GildedKnightGreat";
        protected override float Knock => 9f;

        public const float Radius = 210f;
        private const int Form = 8;
        /// <summary>The tick it bursts: the move's Burst cue's (it is spawned on the Great cue's tick, and moves after him in it).</summary>
        public const int BurstAt = 29;
        private const int Life = 60;

        private int age;
        private bool burstLanded;

        private int Way => Projectile.ai[0] >= 0f ? 1 : -1;

        protected override bool Live => age <= Form + 4;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            // Gone with him if his fight is over before it bursts.
            if (age < BurstAt && Owner() == null)
            {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            if (age == 1)
            {
                KnightSounds.Play(KnightSounds.GreatCrescent, Projectile.Center);
                KnightFx.Shake(Projectile.Center, 6f, 18);
            }

            if (age <= BurstAt)
            {
                // Fire shed along the arc as it hangs.
                for (int i = 0; i < 3; i++)
                {
                    float a = Main.rand.NextFloat(-1.25f, 1.25f);
                    Vector2 p = Projectile.Center + new Vector2(Way * (float)Math.Cos(a), (float)Math.Sin(a)) * Radius * Main.rand.NextFloat(0.9f, 1.02f);
                    KnightFx.Embers(p, 1, 4f, 1.6f);
                }

                Lighting.AddLight(Projectile.Center + new Vector2(Way * Radius * 0.7f, 0f), new Vector3(1f, 0.55f, 0.2f) * 1.4f);
            }

            if (age == BurstAt)
            {
                KnightFx.Flash(Projectile.Center, new Color(255, 160, 70), 0.18f);
                KnightFx.Shake(Projectile.Center, 12f, 30);
                KnightFx.Bloom(Projectile.Center + new Vector2(Way * Radius * 0.5f, 0f), KnightFx.Fire, 2.4f, 40);
                KnightFx.Bloom(Projectile.Center + new Vector2(Way * Radius * 0.5f, 0f), KnightFx.White, 1.2f, 24);

                for (int i = 0; i < 70; i++)
                {
                    float a = Main.rand.NextFloat(-1.4f, 1.4f);
                    float r = Main.rand.NextFloat(0.1f, 1f) * Radius;
                    Vector2 p = Projectile.Center + new Vector2(Way * (float)Math.Cos(a), (float)Math.Sin(a)) * r;
                    KnightFx.Embers(p, 1, 6f, 4f);
                }

                KnightFx.Ring(Projectile.Center + new Vector2(Way * Radius * 0.5f, 0f), 30f, Radius * 1.3f, 30, KnightFx.Fire);
            }

            // The burst: whoever is within the half-circle he swept round, once.
            if (age >= BurstAt && age <= BurstAt + 6 && !burstLanded)
            {
                Player me = Main.LocalPlayer;

                if (me.active && !me.dead && !me.immune && GildedKnight.Fights(me))
                {
                    Vector2 d = me.Center - Projectile.Center;

                    if (d.Length() <= Radius && d.X * Way >= -30f)
                    {
                        int damage = Main.DamageVar(GildedKnight.Damage(GildedKnight.GreatDamage), -me.luck);
                        var reason = Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                            Terraria.Localization.NetworkText.FromKey("Mods.PisAndDag.DeathText.GildedKnightGreat", me.name));

                        if (me.Hurt(reason, damage, Way, false, false, ImmunityCooldownID.General, true, 0f, 0f, 9f) > 0)
                            burstLanded = true;
                    }
                }
            }
        }

        /// <summary>While it forms, its band: from the arc's inside to its outside, in front of him.</summary>
        protected override bool Touches(Player me)
        {
            Vector2 d = me.Center - Projectile.Center;
            float r = d.Length();
            return r > Radius - 44f && r < Radius + 16f && d.X * Way > -20f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = Crescent;
            float form = Math.Min(1f, age / (float)Form);
            float hold = age < BurstAt ? 1f : Math.Max(0f, 1f - (age - BurstAt) / 10f);
            float a = form * hold;

            // The crescent texture's arc, turned to bulge ahead of him, scaled to the radius.
            float rot = Way * MathHelper.PiOver2;
            Vector2 origin = new Vector2(tex.Width * 0.5f, tex.Height * 0.98f);
            float s = Radius / (tex.Height * 0.88f);
            Vector2 at = Projectile.Center - Main.screenPosition;

            sb.Draw(tex, at, null, Light(KnightFx.Deep, 0.7f * a), rot, origin, new Vector2(s * 1.1f, s * 1.08f) * (0.85f + 0.15f * form), SpriteEffects.None, 0f);
            sb.Draw(tex, at, null, Light(KnightFx.Fire, 1f * a), rot, origin, new Vector2(s, s) * (0.85f + 0.15f * form), SpriteEffects.None, 0f);
            sb.Draw(tex, at, null, Light(new Color(255, 236, 170), 0.85f * a), rot, origin, new Vector2(s * 0.92f, s * 0.93f) * (0.85f + 0.15f * form), SpriteEffects.None, 0f);

            // The burst: the half-circle filled, white-hot and gone.
            if (age >= BurstAt - 2)
            {
                float b = MathHelper.Clamp((age - BurstAt + 2) / 6f, 0f, 1f) * Math.Max(0f, 1f - (age - BurstAt) / 24f);
                Vector2 middle = Projectile.Center + new Vector2(Way * Radius * 0.45f, 0f);
                DrawGlow(sb, middle, Radius * 3.2f, Light(KnightFx.Fire, 0.8f * b));
                DrawGlow(sb, middle, Radius * 1.6f, Light(new Color(255, 220, 150), 0.9f * b));
            }

            return false;
        }
    }

    // ============================================================
    // THE PROCESSION OF FIRE: bursts running out along the ground
    // ============================================================

    /// <summary>
    /// Fire running out along the ground from where the Sun went into it, at whoever he fights:
    /// a burst every few ticks, each a step further (ai[0]: the way), until the field's end or
    /// Count of them (Rellana's line of fire). It is only where the fire goes from; the bursts are
    /// KnightBurst.
    /// </summary>
    public class KnightProcession : ModProjectile
    {
        public override string Texture => KnightRig.Folder + "KnightCrescent";

        public const int Count = 14;
        public const int Every = 6;
        public const float Apart = 44f;

        private int age;

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = Count * Every + 4;
            Projectile.penetrate = -1;
            Projectile.aiStyle = -1;
        }

        public override bool? CanCutTiles() => false;

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // The fire runs down the road while his fight is on; his end (or his leaving) stops it (ai[2]: his whoAmI).
            int who = (int)Projectile.ai[2];

            if (who < 0 || who >= Main.maxNPCs || !Main.npc[who].active || Main.npc[who].ModNPC is not GildedKnight knight || knight.Over)
            {
                Projectile.Kill();
                return;
            }

            if (age % Every == 0 && age / Every < Count)
            {
                int k = age / Every + 1;
                int way = Projectile.ai[0] >= 0f ? 1 : -1;
                Vector2 at = Projectile.Center + new Vector2(way * k * Apart, 0f);
                float ground = GroundAt(at);

                // Standing on the ground: its middle half its height over it (KnightBurst is 70 tall).
                if (ground > 0f)
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), new Vector2(at.X, ground - 35f), Vector2.Zero, ModContent.ProjectileType<KnightBurst>(), 0, 0f, Main.myPlayer);
            }

            age++;
        }

        /// <summary>The ground near a point (a world y), looking a few tiles up and down; 0 if there is none.</summary>
        private static float GroundAt(Vector2 at)
        {
            int x = (int)(at.X / 16f);
            int y0 = (int)(at.Y / 16f) - 4;

            for (int y = y0; y < y0 + 12; y++)
            {
                if (!WorldGen.InWorld(x, y, 2))
                    return 0f;

                if (WorldGen.SolidTile(x, y) || (Main.tile[x, y].HasTile && Main.tileSolidTop[Main.tile[x, y].TileType]))
                    return y * 16f;
            }

            return 0f;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>One of the procession's bursts: fire up out of the ground, a hot flash, gone.</summary>
    public class KnightBurst : KnightShot
    {
        protected override int[] Table => GildedKnight.ProcessionDamage;
        protected override string Death => "GildedKnightProcession";
        protected override float Knock => 6f;

        private const int Life = 26;
        private int age;

        protected override bool Live => age >= 2 && age < 10;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 50;
            Projectile.height = 70;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            age++;

            if (age == 1 && !Main.dedServ)
            {
                Vector2 foot = Projectile.Bottom;
                KnightSounds.Play(KnightSounds.Procession, foot);
                KnightFx.Embers(foot + new Vector2(0f, -20f), 14, 14f, 3.5f);
                KnightFx.Bloom(foot + new Vector2(0f, -26f), KnightFx.Fire, 0.7f, 22);
                KnightFx.Dust(foot, 0.7f);
                GroundWave.Slam(foot, 10f, 1, 5f, 8);
                KnightFx.Shake(foot, 2.5f, 6);
            }

            if (!Main.dedServ)
                Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.55f, 0.2f) * (1f - age / (float)Life) * 1.4f);
        }

        protected override bool Touches(Player me) => Projectile.Hitbox.Intersects(me.Hitbox);

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            float t = age / (float)Life;
            float rise = Math.Min(1f, t * 5f);
            float fade = 1f - t;
            Vector2 foot = Projectile.Bottom;
            Texture2D flame = Flame;

            for (int k = 0; k < 3; k++)
            {
                float lean = (k - 1) * 0.25f;
                sb.Draw(flame, foot - Main.screenPosition + new Vector2((k - 1) * 9f, 0f), null, Light(k == 1 ? new Color(255, 220, 140) : KnightFx.Fire, 0.9f * fade),
                    lean, new Vector2(flame.Width * 0.5f, flame.Height), new Vector2(1f, (0.8f + 0.25f * (k == 1 ? 1 : 0)) * rise), SpriteEffects.None, 0f);
            }

            DrawGlow(sb, foot + new Vector2(0f, -30f), 140f * (0.5f + 0.5f * rise), Light(KnightFx.Fire, 0.45f * fade));
            return false;
        }
    }

    // ============================================================
    // THE LONG BLADE: the pillar where it lands, and the sweep along the ground
    // ============================================================

    /// <summary>
    /// Where the long blade comes down: a column of light from the ground to the sky, a shockwave
    /// out of its foot either way. Strikes as it lands.
    /// </summary>
    public class KnightPillar : KnightShot
    {
        protected override int[] Table => GildedKnight.LongDamage;
        protected override string Death => "GildedKnightLong";
        protected override float Knock => 9f;

        private const int Life = 40;
        private const float Wide2 = 56f;
        private const float High = 900f;

        private int age;

        protected override bool Live => age < 10;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            if (age == 1 && !Main.dedServ)
            {
                KnightSounds.Play(KnightSounds.BladeSlam, Projectile.Center);
                KnightFx.Flash(Projectile.Center, new Color(255, 236, 190), 0.14f);
                KnightFx.Shake(Projectile.Center, 10f, 24);
                KnightFx.Bloom(Projectile.Center, KnightFx.White, 1.4f, 30);
                KnightFx.Sparks(Projectile.Center, -Vector2.UnitY, 26, KnightFx.Pale, 9f, 1.3f);
                KnightFx.Dust(Projectile.Center, 1.5f);
                GroundWave.Slam(Projectile.Center, 28f, 9, 12f, 28);
            }

            if (!Main.dedServ)
            {
                for (float h = 0f; h < 600f; h += 100f)
                    Lighting.AddLight(Projectile.Center + new Vector2(0f, -h), new Vector3(1f, 0.95f, 0.75f) * (1f - age / (float)Life) * 1.4f);
            }
        }

        protected override bool Touches(Player me)
        {
            Rectangle col = new Rectangle((int)(Projectile.Center.X - Wide2 * 0.5f), (int)(Projectile.Center.Y - High), (int)Wide2, (int)High);
            return col.Intersects(me.Hitbox);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            float t = age / (float)Life;
            float fade = 1f - t;
            float wide = Wide2 * (t < 0.15f ? 1.6f - t * 4f : 1f) * (1f - 0.6f * t);
            Texture2D p = Pillar;
            Vector2 foot = Projectile.Center - Main.screenPosition;

            sb.Draw(p, foot, null, Light(KnightFx.Gold, 0.8f * fade), 0f, new Vector2(p.Width * 0.5f, p.Height), new Vector2(wide * 2.4f / p.Width, High / p.Height), SpriteEffects.None, 0f);
            sb.Draw(p, foot, null, Light(KnightFx.White, 1f * fade), 0f, new Vector2(p.Width * 0.5f, p.Height), new Vector2(wide / p.Width, High / p.Height), SpriteEffects.None, 0f);
            DrawGlow(sb, Projectile.Center, 220f * fade, Light(KnightFx.White, 0.7f * fade));
            return false;
        }
    }

    /// <summary>
    /// The long blade swept level along the ground at knee height (by him, from where it lay, out
    /// the length of the field): its edge runs out from him at Speed (ai[0]: the way), cutting
    /// whoever is on the ground in its path. Jumped over.
    /// </summary>
    public class KnightSweep : KnightShot
    {
        protected override int[] Table => GildedKnight.LongDamage;
        protected override string Death => "GildedKnightLong";
        protected override float Knock => 8f;

        public const float Speed = 36f;
        private const float Band = 30f;
        private const int Life = 50;

        private int age;
        private float reach;

        private int Way => Projectile.ai[0] >= 0f ? 1 : -1;

        /// <summary>How far out it has reached, for the drawing (KnightCanvas draws the long blade itself along it).</summary>
        public float Reach => reach;

        protected override bool Live => age < 32;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;
            reach = Math.Min(1500f, reach + Speed);

            if (age == 1 && !Main.dedServ)
            {
                KnightSounds.Play(KnightSounds.BladeSweep, Projectile.Center);
                KnightFx.Shake(Projectile.Center, 5f, 24);
            }

            if (!Main.dedServ && age < 32)
            {
                Vector2 edge = Projectile.Center + new Vector2(Way * reach, 0f);
                KnightFx.Sparks(edge + new Vector2(0f, 10f), new Vector2(Way, -0.6f), 2, KnightFx.Pale, 5f, 0.5f);

                if (age % 2 == 0)
                    KnightFx.Leaves(new Vector2(edge.X, Projectile.Center.Y + Band), 2, 3.5f);

                Lighting.AddLight(edge, new Vector3(1f, 0.95f, 0.75f));
            }
        }

        /// <summary>The band the blade lies along, from him to its edge, at knee height.</summary>
        protected override bool Touches(Player me)
        {
            float x0 = Projectile.Center.X, x1 = Projectile.Center.X + Way * reach;
            Rectangle band = new Rectangle((int)Math.Min(x0, x1), (int)(Projectile.Center.Y - Band * 0.5f), (int)Math.Abs(x1 - x0), (int)Band);
            return band.Intersects(me.Hitbox);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            float fade = age < 32 ? 1f : Math.Max(0f, 1f - (age - 32) / 18f);
            Vector2 from = Projectile.Center;
            Vector2 to = from + new Vector2(Way * reach, 0f);

            DrawLine(sb, Beam, from, to, 44f, Light(KnightFx.Gold, 0.45f * fade));
            DrawLine(sb, Beam, from, to, 18f, Light(KnightFx.Pale, 0.9f * fade));
            DrawLine(sb, Line, from, to, 6f, Light(KnightFx.White, 1f * fade));
            DrawGlow(sb, to, 150f, Light(KnightFx.White, 0.7f * fade));
            return false;
        }
    }

    // ============================================================
    // THE GILDED SUN
    // ============================================================

    /// <summary>
    /// The gilded sun (Rellana's full moon, in fire and gold): over the Sun's point as he holds it
    /// up, a sun forms and swells, two rings turning round it, light drawn up into it out of the
    /// field; brought down, it falls on where they stood (ai[0], ai[1]: that place, set when it
    /// falls) and where it lands a pillar of fire to the sky. ai[2]: his whoAmI. localAI: ticks.
    /// </summary>
    public class KnightSunOrb : KnightShot
    {
        protected override int[] Table => GildedKnight.SunDamage;
        protected override string Death => "GildedKnightSun";
        protected override float Knock => 10f;

        /// <summary>
        /// By its own count of ticks (1 the tick it forms, the move's SunForm): the fall begins at Falls,
        /// the tick of the move's SunFall, when the knight has just said where it lands; it lands Fall
        /// ticks later; the pillar stands for Burns.
        /// </summary>
        public const int Falls = KnightMoves.SunDown - 4 - KnightMoves.SunForm + 1;
        public const int Fall = 30;
        public const int Burns = 54;
        public const float Radius = 120f;

        private int age;
        private Vector2 high;

        protected override bool Live => age >= Falls + Fall && age < Falls + Fall + 18;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.timeLeft = Falls + Fall + Burns + 2;
        }

        private Vector2 Landing => new Vector2(Projectile.ai[0], Projectile.ai[1]);

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            // Gone with him before it falls: his end takes the sun he raised.
            GildedKnight k = Owner();

            if (age < Falls)
            {
                if (k == null)
                {
                    Projectile.Kill();
                    return;
                }

                // Over the Sun's point, swelling.
                KnightRig.Frame f = KnightRig.Build(k.Pose, k.Feet, k.Faces, null, k.SunSwing, k.GraceSwing);
                high = f.SunTip + new Vector2(0f, -70f);
                Projectile.Center = high;

                if (!Main.dedServ)
                {
                    if (age == 1)
                    {
                        KnightSounds.Play(KnightSounds.SunForm, high);
                        KnightFx.Flash(high, new Color(255, 200, 120), 0.08f);
                    }

                    // Light drawn up into it out of the field.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 from = high + Main.rand.NextVector2CircularEdge(260f, 200f);
                        PADParticleHandler.SpawnParticle(new SparkShard(from, (high - from) * 0.06f, KnightFx.Gold, 6f, 1.4f, 26f, 18, false, true, 1f));
                    }

                    if (Main.rand.NextBool(2))
                        KnightFx.Motes(k.Feet + new Vector2(Main.rand.NextFloat(-400f, 400f), -4f), 1, 4f, KnightFx.Gold);

                    Lighting.AddLight(high, new Vector3(1f, 0.7f, 0.3f) * (0.6f + age / (float)Falls));
                    if (KnightFx.Near(high))
                        Cinematic.Shade(high, 700f, 0.25f * Math.Min(1f, age / 40f));
                }

                return;
            }

            if (age < Falls + Fall)
            {
                if (age == Falls && !Main.dedServ)
                    KnightSounds.Play(KnightSounds.SunFall, high);

                float s = (age - Falls) / (float)Fall;
                float e = s * s;
                Projectile.Center = Vector2.Lerp(high, Landing + new Vector2(0f, -40f), e);

                if (!Main.dedServ)
                {
                    KnightFx.Embers(Projectile.Center, 3, 20f, 2f);
                    Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.7f, 0.3f) * 1.6f);
                    if (KnightFx.Near(Projectile.Center))
                        Cinematic.Shade(Projectile.Center, 600f, 0.3f);
                }

                return;
            }

            Projectile.Center = Landing;

            if (age == Falls + Fall && !Main.dedServ)
            {
                KnightSounds.Play(KnightSounds.SunBurst, Landing);
                KnightFx.Flash(Landing, new Color(255, 214, 140), 0.38f);
                KnightFx.Shake(Landing, 16f, 40);
                KnightFx.Bloom(Landing, KnightFx.White, 3f, 50);
                KnightFx.Bloom(Landing, KnightFx.Fire, 2.2f, 70);
                KnightFx.Ring(Landing, 30f, 420f, 44, KnightFx.Gold);
                KnightFx.Ring(Landing, 20f, 260f, 36, KnightFx.White * 0.8f);
                KnightFx.Embers(Landing, 60, 80f, 6f);
                GroundWave.Slam(Landing, Radius, 14, 14f, 36);
                KnightFx.Dust(Landing, 2f);
            }

            if (!Main.dedServ)
            {
                for (float h = 0f; h < 700f; h += 100f)
                    Lighting.AddLight(Landing + new Vector2(0f, -h), new Vector3(1f, 0.75f, 0.35f) * 1.6f);

                if (Main.rand.NextBool())
                    KnightFx.Embers(Landing + new Vector2(Main.rand.NextFloat(-Radius, Radius), -Main.rand.NextFloat(0f, 400f)), 1, 6f, 3f);
            }
        }

        protected override bool Touches(Player me)
        {
            Rectangle col = new Rectangle((int)(Landing.X - Radius), (int)(Landing.Y - 900f), (int)(Radius * 2f), 900);
            return col.Intersects(me.Hitbox);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;

            if (age < Falls + Fall)
            {
                float grow = Math.Min(1f, age / (float)Falls);
                float size = 30f + 110f * grow;
                Vector2 c = Projectile.Center;

                // The sun: layered glow, a white core.
                DrawGlow(sb, c, size * 3.4f, Light(KnightFx.Fire, 0.55f));
                DrawGlow(sb, c, size * 1.8f, Light(KnightFx.Gold, 0.85f));
                DrawGlow(sb, c, size * 0.9f, Light(KnightFx.White, 1f));

                // Two rings turning about it, tilted.
                Texture2D ring = RingTex;
                float spin = age * 0.05f;

                for (int k = 0; k < 2; k++)
                {
                    float tilt = k == 0 ? 0.32f : -0.5f;
                    float squash = 0.35f + 0.15f * (float)Math.Sin(spin * 2f + k);
                    sb.Draw(ring, c - Main.screenPosition, null, Light(k == 0 ? KnightFx.Gold : KnightFx.Pale, 0.8f), tilt + spin * (k == 0 ? 1f : -0.7f), ring.Size() * 0.5f,
                        new Vector2(size * 2.4f / ring.Width, size * 2.4f * squash / ring.Height), SpriteEffects.None, 0f);
                }

                // Its mark on the ground as it falls.
                if (age >= Falls)
                {
                    float s = (age - Falls) / (float)Fall;
                    sb.Draw(ring, Landing - Main.screenPosition, null, Light(KnightFx.Fire, 0.4f + 0.5f * s), 0f, ring.Size() * 0.5f,
                        new Vector2(Radius * 2.2f / ring.Width, Radius * 0.5f / ring.Height), SpriteEffects.None, 0f);
                }

                return false;
            }

            // The pillar of fire where it landed.
            float b = (age - Falls - Fall) / (float)Burns;
            float fade = 1f - b;
            float wide = Radius * 2f * (b < 0.1f ? 1.4f - b * 4f : 1f) * (1f - 0.5f * b);
            Texture2D p = Pillar;
            Vector2 foot = Landing - Main.screenPosition;

            sb.Draw(p, foot, null, Light(KnightFx.Fire, 0.9f * fade), 0f, new Vector2(p.Width * 0.5f, p.Height), new Vector2(wide * 1.6f / p.Width, 1000f / p.Height), SpriteEffects.None, 0f);
            sb.Draw(p, foot, null, Light(KnightFx.Gold, 1f * fade), 0f, new Vector2(p.Width * 0.5f, p.Height), new Vector2(wide / p.Width, 1000f / p.Height), SpriteEffects.None, 0f);
            sb.Draw(p, foot, null, Light(KnightFx.White, 1f * fade), 0f, new Vector2(p.Width * 0.5f, p.Height), new Vector2(wide * 0.4f / p.Width, 1000f / p.Height), SpriteEffects.None, 0f);
            DrawGlow(sb, Landing, Radius * 4f * fade, Light(KnightFx.Fire, 0.6f * fade));
            return false;
        }
    }
}
