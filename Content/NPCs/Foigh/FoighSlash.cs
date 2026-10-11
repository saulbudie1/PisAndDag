using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// A cut of Foigh's burning sword, let go of: a crescent of what the sword burns
    /// with, black with a red edge, that goes on through the air. His third phase's own
    /// (Foigh.Rend lets three of them go, each at whoever he fights, wherever they are:
    /// along the ground at one who stands on it, up at one who hangs from the ceiling,
    /// down at one below him; the third the largest, and the slowest).
    ///
    /// It goes straight, whichever way it was sent, at one speed, through platforms and
    /// through players, and is gone against the first solid thing it meets, or when it
    /// has flown its time: it thins away over the last of that, and hurts nobody by
    /// then. With him gone, or down, it thins away from that tick.
    ///
    /// Half his sword's blow, through armour, varied as vanilla varies any hit, once,
    /// against each machine's own player, checked as anything that hurts by touch is;
    /// and it leaves the wound his sword leaves (Ultra Bleeding). What hurts is its
    /// leading edge, not what streams off the back of it. It can be jumped, and it can
    /// be parried (Parry.AddProjectile): met by a parry it is gone, for everybody (the
    /// server ends its own, and says so to the rest). By request ("cannot parry wind or
    /// blood slashes"): it is met by a parry already begun when it reaches them, its
    /// wind-up and all, not only by one whose wind is over (ParryPlayer.Takes). It comes
    /// some four tiles in the tenth of a second the wind takes, so that a parry had to be
    /// pressed while it was still four to thirteen tiles off: now anywhere from then until
    /// the tick it lands.
    ///
    /// No picture of its own: it is drawn in strips, as his sword's trail is, with the
    /// pictures those are drawn with. The projectile is the foremost point of it.
    ///
    /// ITS PALE KIND is his second phase's (Foigh.Gale lets two of them go): a cut of
    /// wind, pale, with nothing burning in it. The same edge, the same half of his
    /// sword's blow, a little faster, gone a little sooner; and no wound (that is his
    /// third phase's).
    /// </summary>
    public class FoighSlash : ModProjectile
    {
        /// <summary>None of its own (and it is never drawn as a sprite): one of its strips'.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>Which it is (ai[1]): one of the first two, or the third, the largest and the slowest; or a cut of wind, his second phase's.</summary>
        public const int Plain = 0;
        public const int Great = 1;
        public const int Pale = 2;

        /// <summary>Pixels a tick: the first two, the third, and the wind.</summary>
        public const float Speed = 12f;
        public const float GreatSpeed = 9f;
        public const float PaleSpeed = 14f;

        /// <summary>From tip to tip; and from its leading edge back to where the body of it ends, at its middle.</summary>
        public const float Tall = 68f;
        public const float GreatTall = 88f;
        private const float Deep = 32f;
        private const float GreatDeep = 42f;
        private const float PaleDeep = 22f;

        /// <summary>Its leading edge is this much of a circle either side of its foremost point.</summary>
        private const float Half = MathHelper.Pi * 64f / 180f;

        /// <summary>Ticks it flies; it comes over the first of them and thins away over the last.</summary>
        public const int Life = 80;
        public const int GreatLife = 100;
        public const int PaleLife = 66;
        private const int ComesIn = 3;
        private const int GoesOut = 14;

        /// <summary>It hurts, and can be turned aside, while at least this much of it is there.</summary>
        private const float HurtsFrom = 0.5f;

        /// <summary>This share of his sword's blow; and the knock.</summary>
        public const float Share = 0.5f;
        private const float Knockback = 5f;

        /// <summary>
        /// The part that hurts: the middle four fifths of its leading edge (not the last
        /// tenth of it at either tip, where there is next to nothing of it to see), this
        /// thick, in this many straight pieces. So nothing of it hurts where nothing of
        /// it is drawn.
        /// </summary>
        private const float HurtArc = 0.8f;
        private const float HurtWidth = 10f;
        private const int HurtParts = 6;

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> softLine;

        private static readonly Color Wind = new Color(214, 226, 240);

        // ai[0] his whoAmI · ai[1] which it is: Plain, Great or Pale
        // localAI[0] 1 once it has hit this machine's player · localAI[1] the ticks this machine has seen of it

        public static float SpeedOf(int kind) => kind == Great ? GreatSpeed : kind == Pale ? PaleSpeed : Speed;

        public static float TallOf(int kind) => kind == Great ? GreatTall : Tall;

        public static int LifeOf(int kind) => kind == Great ? GreatLife : kind == Pale ? PaleLife : Life;

        private static float DeepOf(int kind) => kind == Great ? GreatDeep : kind == Pale ? PaleDeep : Deep;

        private static int KindOf(Projectile projectile) => projectile.ai[1] >= 1.5f ? Pale : projectile.ai[1] >= 0.5f ? Great : Plain;

        /// <summary>How much of it there is at this age, 0 to 1.</summary>
        private static float Shown(int age, int kind)
        {
            float comes = MathHelper.Clamp(age / (float)ComesIn, 0f, 1f);
            float goes = MathHelper.Clamp((LifeOf(kind) - age) / (float)GoesOut, 0f, 1f);

            return comes * goes;
        }

        private static float Shown(Projectile projectile) => Shown((int)projectile.localAI[1], KindOf(projectile));

        /// <summary>Whether this one hurts as it is: enough of it there.</summary>
        public static bool Hurts(Projectile projectile) => Shown(projectile) >= HurtsFrom;

        /// <summary>
        /// A place on the leading edge of one whose foremost point is here and which goes
        /// this way: <paramref name="k"/> from -1 (one tip) through 0 (the foremost
        /// point) to 1 (the other tip); and the way out from the edge there.
        /// </summary>
        internal static Vector2 Edge(Vector2 foremost, Vector2 way, float tall, float k, out Vector2 outward)
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

        public override void SetStaticDefaults()
        {
            // In the air, enough of it there, and not already through this machine's player.
            Parry.AddProjectile(Type, projectile => projectile.localAI[0] == 0f && Hurts(projectile));
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = GreatLife + 20;
        }

        private Foigh Owner()
        {
            int i = (int)Projectile.ai[0];

            if (i < 0 || i >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[i];
            return npc.active ? npc.ModNPC as Foigh : null;
        }

        public override void AI()
        {
            int kind = KindOf(Projectile);
            int life = LifeOf(kind);
            int age = (int)Projectile.localAI[1];

            Foigh foigh = Owner();
            bool his = foigh != null && foigh.Fighting;

            // He is gone, or down: what is left of it thins away from now.
            if (!his && age < life - GoesOut)
                age = life - GoesOut;

            if (age == 0 && !Main.dedServ)
                Born(kind);

            age++;
            Projectile.localAI[1] = age;

            if (age >= life)
            {
                Projectile.Kill();
                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            float shown = Shown(age, kind);

            if (kind == Pale)
                Lighting.AddLight(Projectile.Center, 0.26f * shown, 0.28f * shown, 0.32f * shown);
            else
                Lighting.AddLight(Projectile.Center, 0.55f * shown, 0.07f * shown, 0.06f * shown);

            if (Main.dedServ)
                return;

            Wake(kind, age, shown);

            if (his && shown >= HurtsFrom)
                Strike(foigh, kind);
        }

        /// <summary>Against this machine's own player: half his sword's blow through armour, and its wound. Once.</summary>
        private void Strike(Foigh foigh, int kind)
        {
            if (Projectile.localAI[0] != 0f)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float tall = TallOf(kind);

            if (!Touches(me, way, tall))
                return;

            // On them: the parry first, if it came from their front, and whether or not its
            // wind is over (it comes too fast for that to be waited out). At their arc,
            // before it gets this far, an active parry finds it for itself
            // (ParryPlayer.PostUpdate).
            if (me.GetModPlayer<ParryPlayer>().Takes(Projectile, Projectile.Center - way * tall, true))
                return;

            if (me.immune)
                return;

            int damage = Main.DamageVar(foigh.SwordDamage() * Share, -me.luck);
            int direction = Projectile.velocity.X >= 0f ? 1 : -1;

            if (!Foigh.Pierce(me, "FoighSlash", damage, direction, Knockback))
                return;

            // It goes on through them, and is theirs no more.
            Projectile.localAI[0] = 1f;

            // His sword's cut still, and it leaves what his sword leaves. (Not the wind.)
            if (foigh.Phase >= 3 && kind != Pale)
                UltraBleeding.Wound(me);

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.6f, Volume = 1f }, me.Center);
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.3f, Volume = 0.6f }, me.Center);
            me.PIS().BumpScreenShake(5f, 10);

            // Seen on them as his sword's cuts are: across the way it went, red, and its black twin.
            BloodSlash.Pair(me.Center, new Vector2(-way.Y, way.X), 44f, 6f, 18);
            BloodDroplet.SpawnBurst(me.Center, way, 10, 46f);
        }

        /// <summary>
        /// Whether its leading edge is on this player: the edge in straight pieces, as
        /// thick as it is, where it is this tick. (It goes less than a player's width in
        /// a tick, and its tips trail behind its middle: nobody who runs into it at
        /// under 31 px a tick comes out the other side of it between two looks (under
        /// 29, into the wind, the fastest of them), and the game's fastest dash is 21.9.)
        /// </summary>
        private bool Touches(Player me, Vector2 way, float tall)
        {
            float where = 0f;
            Vector2 before = Edge(Projectile.Center, way, tall, -HurtArc, out _);

            for (int i = 1; i <= HurtParts; i++)
            {
                Vector2 next = Edge(Projectile.Center, way, tall, MathHelper.Lerp(-HurtArc, HurtArc, i / (float)HurtParts), out _);

                if (Collision.CheckAABBvLineCollision(me.position, me.Size, before, next, HurtWidth, ref where))
                    return true;

                before = next;
            }

            return false;
        }

        /// <summary>The first tick this machine sees of it: it is heard leaving the blade.</summary>
        private void Born(int kind)
        {
            SoundEngine.PlaySound(SoundID.DD2_SonicBoomBladeSlash with { Pitch = kind == Great ? -0.6f : kind == Pale ? 0.35f : -0.3f, Volume = kind == Great ? 1f : kind == Pale ? 0.7f : 0.85f, PitchVariance = 0.08f },
                Projectile.Center);
        }

        /// <summary>Every tick, on a machine with a screen: black off the back of it, left behind as it goes; red in that; sparks.</summary>
        private void Wake(int kind, int age, float shown)
        {
            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float tall = TallOf(kind);
            float deep = DeepOf(kind);

            // The wind leaves only air behind it.
            if (kind == Pale)
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

        /// <summary>
        /// Gone while there was still something of it: against the ground, a wall or a
        /// ceiling, or met by a parry. What it was made of is let go where it was.
        /// </summary>
        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            int kind = KindOf(Projectile);
            float shown = Shown(Projectile);

            if (shown < 0.25f)
                return;

            // The way it was going (a wall has had its speed by now).
            Vector2 way = Projectile.rotation.ToRotationVector2();
            float tall = TallOf(kind);

            if (kind == Pale)
            {
                SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f, Volume = 0.4f * shown }, Projectile.Center);

                for (int i = 0; i < 6; i++)
                {
                    Vector2 at = Edge(Projectile.Center, way, tall, Main.rand.NextFloat(-0.9f, 0.9f), out Vector2 outward);

                    PADParticleHandler.SpawnParticle(new WindStreak(at, (outward - way) * Main.rand.NextFloat(1.5f, 4f), Main.rand.NextFloat(16f, 30f), 4f, Main.rand.Next(7, 12)));
                }

                return;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.5f, Volume = 0.6f * shown }, Projectile.Center);

            for (int i = 0; i < (kind == Great ? 10 : 7); i++)
            {
                Vector2 at = Edge(Projectile.Center, way, tall, Main.rand.NextFloat(-0.9f, 0.9f), out Vector2 outward) - way * Main.rand.NextFloat(2f, 12f);
                Vector2 off = -way * Main.rand.NextFloat(0.4f, 2.2f) + new Vector2(-outward.Y, outward.X) * Main.rand.NextFloat(-1.4f, 1.4f);

                PADParticleHandler.SpawnParticle(new BurnSmoke(at, off, Foigh.Soot, Foigh.BurnRed, Main.rand.Next(26, 44), Main.rand.NextFloat(10f, 16f), Main.rand.NextFloat(30f, 46f),
                    Main.rand.NextFloat(0.7f, 0.95f) * shown));

                if (i % 2 == 0)
                {
                    PADParticleHandler.SpawnParticle(new Ember(at, off * 1.5f + new Vector2(0f, -Main.rand.NextFloat(0.2f, 1.4f)), Foigh.Cinder,
                        Main.rand.NextFloat(0.45f, 0.8f), Main.rand.Next(16, 30)));
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2 at = Edge(Projectile.Center, way, tall, Main.rand.NextFloat(-0.8f, 0.8f), out _);

                PADParticleHandler.SpawnParticle(new BloodFume(at, -way * Main.rand.NextFloat(0.2f, 1f), Main.rand.NextFloat(0.1f, 0.17f) * shown, Main.rand.Next(16, 28)));
            }
        }

        // ============================================================
        // DRAWING
        // ============================================================

        /// <summary>Steps along its edge, from one tip to the other.</summary>
        private const int Steps = 28;

        /// <summary>The red about it reaches this far past it, ahead and behind.</summary>
        private const float GlowOut = 13f;

        // Laid out again every frame it is drawn, only ever on the main thread: the leading
        // edge, the way out from it there, how far back the body of it goes from there, and
        // how strongly that part of it shows.
        private static readonly Vector2[] EdgeAt = new Vector2[Steps + 1];
        private static readonly Vector2[] EdgeOut = new Vector2[Steps + 1];
        private static readonly float[] Back = new float[Steps + 1];
        private static readonly float[] Strength = new float[Steps + 1];

        /// <summary>
        /// One of them laid out, from one tip to the other: a moon, its body deepest
        /// behind its foremost point and nothing at its tips, standing off behind it in
        /// tongues that never hold still (as the soot off his blade does: two turns of
        /// different lengths against each other, by the pixel along the edge, so that a
        /// large one and a small one burn alike).
        /// </summary>
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
            int kind = KindOf(Projectile);

            Lay(Projectile.Center, way, kind, Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI);

            Main.spriteBatch.End();

            if (kind == Pale)
                DrawPale(way, shown);
            else
                DrawBands(way, shown);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);

            return false;
        }

        /// <summary>
        /// A wide faint red under everything (against the dark, where soot alone is
        /// nothing, the body is then a black shape cut out of it); the body, soot, over
        /// what is behind it; streaks in that, darker toward the edge, and red in them;
        /// and the edge itself, red and bright, with a paler line down the middle of it.
        /// </summary>
        private static void DrawBands(Vector2 way, float shown)
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

        /// <summary>
        /// The wind: a faint pale light about it; its body streaks of air, all light and
        /// nothing over what is behind it; and its edge a bright line.
        /// </summary>
        private static void DrawPale(Vector2 way, float shown)
        {
            Strip.Begin();

            for (int i = 0; i <= Steps; i++)
            {
                Color glow = Light(Wind, 0.2f * shown * Strength[i]);

                Strip.Pair(EdgeAt[i] - way * (Back[i] + 8f), EdgeAt[i] + EdgeOut[i] * 7f, glow, glow, 0.5f);
            }

            Strip.Draw(softLine.Value);

            Strip.Begin();

            for (int i = 0; i <= Steps; i++)
            {
                Color air = Light(Wind, 0.42f * shown * Strength[i]);

                Strip.Pair(EdgeAt[i] - way * Back[i], EdgeAt[i], air, air, i / (float)Steps);
            }

            Strip.Draw(smear.Value);

            Strip.Begin();

            for (int i = 0; i <= Steps; i++)
            {
                Color edge = Light(Color.White, 0.8f * shown * Strength[i]);

                Strip.Pair(EdgeAt[i] - EdgeOut[i] * 3f, EdgeAt[i] + EdgeOut[i] * 1.5f, edge, edge, 0.5f);
            }

            Strip.Draw(softLine.Value);
        }

        /// <summary>How far in from the edge its red line begins, at this step: thinner toward the tips, where there is less of a body behind it.</summary>
        private static float Rim(int i) => Math.Min(7f, 2f + Back[i] * 0.3f);

        /// <summary>A colour as light: added to what is there. And as soot: over it.</summary>
        private static Color Light(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 0) * strength;

        private static Color Dark(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 255) * strength;
    }
}
