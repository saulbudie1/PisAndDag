using System;
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
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// A cut of the Rust Side Sword let go of in the air (RustSideHoldout, right click): a
    /// crescent that goes on through the air, the way Foigh's own do (FoighSlash), but the
    /// player's. Pale, a cut of wind with nothing burning in it; red with its black, as his
    /// third phase's are, while the sword is drenched (BloodDrenchedSword: ai[1]).
    ///
    /// By request: "all slashes penetrate -1, go thru tiles and last for 2.5 sec max, super
    /// low local hit maybe 7". It goes straight at one speed through everything, ground and
    /// walls too, for <see cref="Life"/> ticks (2.5 seconds) and no more, thinning away over the
    /// last of them; through every enemy, each of which it can hit again every
    /// <see cref="HitCooldown"/> ticks while it is in them (its own count for each, not the
    /// game's). The first two of the three are <see cref="Plain"/>; the third is
    /// <see cref="Great"/>, larger and slower. What it does is the sword's damage times its
    /// share, set where it is let go of (the first two half as much again, the third three
    /// times), through all defense, as the sword's own cuts are.
    ///
    /// What hurts is its leading edge, the middle of it, as thick as <see cref="HurtWidth"/>.
    /// A red one leaves what it hits bleeding (UltraBleedingGeneric). No picture of its own: it
    /// is drawn in strips, with the pictures his are drawn with.
    /// </summary>
    public class RustSideSlash : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>Which it is (ai[0]).</summary>
        public const int Plain = 0;
        public const int Great = 1;

        /// <summary>Pixels a tick.</summary>
        public const float Speed = 14f;
        public const float GreatSpeed = 11f;

        /// <summary>From tip to tip; and from the leading edge back to where the body of it ends, at its middle.</summary>
        public const float Tall = 76f;
        public const float GreatTall = 120f;
        private const float Deep = 30f;
        private const float GreatDeep = 46f;

        /// <summary>The leading edge is this much of a circle either side of its foremost point.</summary>
        private const float Half = MathHelper.Pi * 64f / 180f;

        /// <summary>Ticks it flies, the most (by request, two and a half seconds); it comes over the first of them and thins away over the last.</summary>
        public const int Life = 150;
        private const int ComesIn = 3;
        private const int GoesOut = 18;

        /// <summary>It hurts while at least this much of it is there.</summary>
        private const float HurtsFrom = 0.5f;

        /// <summary>Ticks before it can hit the same enemy again (by request: "super low local hit maybe 7").</summary>
        public const int HitCooldown = 7;

        /// <summary>The part that hurts: the middle of the leading edge, this thick, in so many straight pieces.</summary>
        private const float HurtArc = 0.85f;
        private const float HurtWidth = 16f;
        private const int HurtParts = 6;

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> softLine;

        private static readonly Color Wind = new Color(214, 226, 240);

        // ai[0] Plain or Great · ai[1] 1 red (the sword drenched), 0 pale
        // localAI[1] the ticks this machine has seen of it

        public static float SpeedOf(int kind) => kind == Great ? GreatSpeed : Speed;

        public static float TallOf(int kind) => kind == Great ? GreatTall : Tall;

        private static float DeepOf(int kind) => kind == Great ? GreatDeep : Deep;

        private static int KindOf(Projectile projectile) => projectile.ai[0] >= 0.5f ? Great : Plain;

        private static bool RedOf(Projectile projectile) => projectile.ai[1] >= 0.5f;

        /// <summary>How much of it there is at this age, 0 to 1.</summary>
        private static float Shown(int age)
        {
            float comes = MathHelper.Clamp(age / (float)ComesIn, 0f, 1f);
            float goes = MathHelper.Clamp((Life - age) / (float)GoesOut, 0f, 1f);

            return comes * goes;
        }

        private static float Shown(Projectile projectile) => Shown((int)projectile.localAI[1]);

        /// <summary>Whether it hurts as it is: enough of it there.</summary>
        public static bool Hurts(Projectile projectile) => Shown(projectile) >= HurtsFrom;

        /// <summary>A place on the leading edge, <paramref name="k"/> from -1 (one tip) through 0 (the foremost point) to 1; and the way out from the edge there.</summary>
        private static Vector2 Edge(Vector2 foremost, Vector2 way, float tall, float k, out Vector2 outward)
        {
            float radius = tall * 0.5f / (float)Math.Sin(Half);
            float turn = k * Half;

            outward = way * (float)Math.Cos(turn) + new Vector2(-way.Y, way.X) * (float)Math.Sin(turn);

            return foremost - way * radius + outward * radius;
        }

        public override void Load()
        {
            if (Main.dedServ)
                return;

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            softLine = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            smear = null;
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
            Projectile.localNPCHitCooldown = HitCooldown;
            Projectile.timeLeft = Life;
        }

        public override void AI()
        {
            int age = (int)Projectile.localAI[1];

            if (age == 0 && !Main.dedServ)
                Born();

            age++;
            Projectile.localAI[1] = age;

            if (age >= Life)
            {
                Projectile.Kill();
                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            float shown = Shown(age);

            if (RedOf(Projectile))
                Lighting.AddLight(Projectile.Center, 0.55f * shown, 0.07f * shown, 0.06f * shown);
            else
                Lighting.AddLight(Projectile.Center, 0.3f * shown, 0.32f * shown, 0.36f * shown);

            if (!Main.dedServ)
                Wake(age, shown);
        }

        public override bool? CanDamage() => Hurts(Projectile) ? null : false;

        /// <summary>Its leading edge, in straight pieces, as thick as it hurts, where it is this tick and where it was last tick (it goes 11 to 14 px a tick).</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float tall = TallOf(KindOf(Projectile));
            float where = 0f;

            for (int back = 0; back <= 1; back++)
            {
                Vector2 foremost = Projectile.Center - Projectile.velocity * (back * 0.5f);
                Vector2 before = Edge(foremost, way, tall, -HurtArc, out _);

                for (int i = 1; i <= HurtParts; i++)
                {
                    Vector2 next = Edge(foremost, way, tall, MathHelper.Lerp(-HurtArc, HurtArc, i / (float)HurtParts), out _);

                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), before, next, HurtWidth, ref where))
                        return true;

                    before = next;
                }
            }

            return false;
        }

        /// <summary>Grass, vines and pots along its edge, as a swung sword's.</summary>
        public override void CutTiles()
        {
            if (!Hurts(Projectile))
                return;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float tall = TallOf(KindOf(Projectile));

            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Edge(Projectile.Center, way, tall, -HurtArc, out _), Edge(Projectile.Center, way, tall, HurtArc, out _), HurtWidth, DelegateMethods.CutTiles);
        }

        /// <summary>Through all defense, as the sword's own cuts; and away the way it goes.</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.ScalingArmorPenetration += 1f;
            modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // A red one, let go of while the sword was drenched, bleeds them (UltraBleedingGeneric).
            if (RedOf(Projectile))
                UltraBleedingGeneric.Cut(target);

            if (Main.dedServ)
                return;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);
            Vector2 across = new Vector2(-way.Y, way.X);

            if (RedOf(Projectile))
            {
                BloodSlash.Pair(at, across, 30f + 8f * KindOf(Projectile), 5f, 16);
                BloodDroplet.SpawnBurst(at, way, 6, 40f);
            }
            else
            {
                SparkShard.Spray(at, Foigh.SteelColour, 4, 4.5f, 18f, 12, falls: true, palette: Foigh.SparkColours);
            }
        }

        /// <summary>The first tick this machine sees of it: heard leaving the blade.</summary>
        private void Born()
        {
            bool great = KindOf(Projectile) == Great;

            SoundEngine.PlaySound(SoundID.DD2_SonicBoomBladeSlash with { Pitch = great ? -0.5f : RedOf(Projectile) ? -0.2f : 0.25f, Volume = great ? 1f : 0.8f, PitchVariance = 0.08f },
                Projectile.Center);
        }

        /// <summary>Every tick, on a machine with a screen: pale air off the back of it; or, red, black off the back of it and red and sparks in that.</summary>
        private void Wake(int age, float shown)
        {
            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int kind = KindOf(Projectile);
            float tall = TallOf(kind);
            float deep = DeepOf(kind);

            if (!RedOf(Projectile))
            {
                if (age % 2 == 0 && Main.rand.NextFloat() <= shown)
                {
                    Vector2 at = Edge(Projectile.Center, way, tall, Main.rand.NextFloat(-0.85f, 0.85f), out _) - way * Main.rand.NextFloat(2f, deep);

                    PADParticleHandler.SpawnParticle(new WindStreak(at, way * Main.rand.NextFloat(2f, 5f), Main.rand.NextFloat(18f, 38f), 4f, Main.rand.Next(7, 12)));
                }

                return;
            }

            for (int i = 0; i < (kind == Great ? 2 : 1); i++)
            {
                if (Main.rand.NextFloat() > shown)
                    continue;

                Vector2 at = Edge(Projectile.Center, way, tall, Main.rand.NextFloat(-0.85f, 0.85f), out _) - way * Main.rand.NextFloat(4f, deep * 0.6f);

                PADParticleHandler.SpawnParticle(new BurnSmoke(at, way * Main.rand.NextFloat(0.6f, 2.2f) + new Vector2(0f, -Main.rand.NextFloat(0f, 0.4f)),
                    Foigh.Soot, Foigh.BurnRed, Main.rand.Next(22, 38), Main.rand.NextFloat(9f, 15f), Main.rand.NextFloat(24f, 38f), Main.rand.NextFloat(0.7f, 0.95f)));
            }

            if (age % 2 == 0)
            {
                Vector2 at = Edge(Projectile.Center, way, tall, Main.rand.NextFloat(-0.9f, 0.9f), out _) - way * Main.rand.NextFloat(2f, 10f);

                PADParticleHandler.SpawnParticle(new BloodFume(at, way * Main.rand.NextFloat(0.5f, 1.6f), Main.rand.NextFloat(0.06f, 0.11f) * shown, Main.rand.Next(12, 22)));
            }

            if (age % 3 == 0)
            {
                Vector2 at = Edge(Projectile.Center, way, tall, Main.rand.NextFloat(-1f, 1f), out Vector2 outward);

                PADParticleHandler.SpawnParticle(new Ember(at, way * Main.rand.NextFloat(1f, 3f) + outward * Main.rand.NextFloat(0.2f, 1.2f), Foigh.Cinder,
                    Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(14, 26)));
            }
        }

        // ============================================================
        // DRAWING (FoighSlash's way: a moon of strips)
        // ============================================================

        private const int Steps = 28;
        private const float GlowOut = 13f;

        // Laid out again every frame it is drawn, only ever on the main thread.
        private static readonly Vector2[] EdgeAt = new Vector2[Steps + 1];
        private static readonly Vector2[] EdgeOut = new Vector2[Steps + 1];
        private static readonly float[] Back = new float[Steps + 1];
        private static readonly float[] Strength = new float[Steps + 1];

        /// <summary>One of them laid out: its body deepest behind its foremost point and nothing at its tips, standing off behind it in tongues that never hold still.</summary>
        private static void Lay(Vector2 foremost, Vector2 way, int kind, float time)
        {
            float tall = TallOf(kind);
            float deep = DeepOf(kind);
            float radius = tall * 0.5f / (float)Math.Sin(Half);

            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps * 2f - 1f;

                EdgeAt[i] = Edge(foremost, way, tall, k, out EdgeOut[i]);

                float along = radius * (k + 1f) * Half;
                float tongue = 0.5f + 0.5f * (float)(Math.Sin(along * 0.3f + time) * Math.Sin(along * 0.11f - time * 0.61f));

                Back[i] = deep * (float)Math.Pow(1f - k * k, 0.7f) * (0.75f + 0.5f * tongue);
                Strength[i] = Easing.Smoother(MathHelper.Clamp((1f - Math.Abs(k)) * 0.5f / 0.18f, 0f, 1f));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (smear == null || softLine == null || !smear.IsLoaded || !softLine.IsLoaded)
                return false;

            float shown = Shown(Projectile);

            if (shown <= 0.01f)
                return false;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            Lay(Projectile.Center, way, KindOf(Projectile), Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI);

            Main.spriteBatch.End();

            if (RedOf(Projectile))
                DrawRed(way, shown);
            else
                DrawPale(way, shown);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);

            return false;
        }

        /// <summary>A wide faint red under everything; the body, soot, over what is behind it; red streaks in that; the edge red and bright, a paler line down its middle.</summary>
        private static void DrawRed(Vector2 way, float shown)
        {
            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color glow = Light(Foigh.BurnRed, 0.4f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - way * (Back[i] + GlowOut), EdgeAt[i] + EdgeOut[i] * GlowOut, glow, glow, 0.5f);
            }
            Strip.Draw(softLine.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color soot = Dark(Foigh.Soot, 0.9f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - way * (Back[i] * 1.1f + 2f), EdgeAt[i], soot, soot, 0.5f);
            }
            Strip.Draw(softLine.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color soot = Dark(Foigh.Soot, 0.6f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - way * Back[i], EdgeAt[i], soot, soot, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color red = Light(Foigh.BurnRed, 0.3f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - way * Back[i], EdgeAt[i], red, red, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color edge = Light(Foigh.TrailRed, 0.9f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - EdgeOut[i] * Rim(i), EdgeAt[i] + EdgeOut[i] * 1.5f, edge, edge, 0.5f);
            }
            Strip.Draw(softLine.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color hot = Light(Foigh.BurnHot, 0.55f * shown * Strength[i]);
                float middle = Rim(i) * 0.5f;
                Strip.Pair(EdgeAt[i] - EdgeOut[i] * (middle + 1.6f), EdgeAt[i] - EdgeOut[i] * (middle - 1.6f), hot, hot, 0.5f);
            }
            Strip.Draw(softLine.Value);
        }

        /// <summary>The wind: a faint pale light about it; its body streaks of air, all light; its edge a bright line, with a hair of dusk under it so it shows on a bright sky.</summary>
        private static void DrawPale(Vector2 way, float shown)
        {
            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dusk = Dark(Foigh.DuskColour, 0.35f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - EdgeOut[i] * 4f, EdgeAt[i] + EdgeOut[i] * 1f, dusk, dusk, 0.5f);
            }
            Strip.Draw(softLine.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color glow = Light(Wind, 0.22f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - way * (Back[i] + 8f), EdgeAt[i] + EdgeOut[i] * 7f, glow, glow, 0.5f);
            }
            Strip.Draw(softLine.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color air = Light(Wind, 0.45f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - way * Back[i], EdgeAt[i], air, air, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color edge = Light(Color.White, 0.85f * shown * Strength[i]);
                Strip.Pair(EdgeAt[i] - EdgeOut[i] * 3f, EdgeAt[i] + EdgeOut[i] * 1.5f, edge, edge, 0.5f);
            }
            Strip.Draw(softLine.Value);
        }

        private static float Rim(int i) => Math.Min(7f, 2f + Back[i] * 0.3f);

        private static Color Light(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 0) * strength;

        private static Color Dark(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 255) * strength;
    }
}
