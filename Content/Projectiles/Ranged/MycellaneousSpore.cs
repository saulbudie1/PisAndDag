using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The life a Mycellaneous arrow takes, going home: a spore of blue light that leaves the struck
    /// enemy the way the arrow was going, slows, and curves back to whoever shot it. The life lands
    /// when it reaches them (Player.Heal, the green number and all), as the game's own life-stealing
    /// orbs land theirs (the Vampire Knives' and the Spectre Hood's: aiStyle 52 in
    /// Projectile.VanillaAI, which heals on reaching the player's hitbox).
    ///
    /// ai[0] is the life it carries (<see cref="MycellaneousArrow"/>). It is shot on the shooter's
    /// machine and the game sends it on, so everyone sees it fly; only the shooter's machine gives the
    /// life, and Moon Bite takes it, as it takes the game's own (Player.moonLeech, checked where they
    /// check it). A shooter who dies first gets nothing. One gone further than it will follow
    /// (<see cref="Reach"/>: a mirror, a teleporter) gets the life at once, and so does one it has not
    /// reached by the end of its <see cref="Flight"/>, which it never needs.
    ///
    /// No picture of its own: it is drawn in light, a bloom over a short tail (the BloomGeneric
    /// texture, as Meagre Flame's ball), and it sheds motes (<see cref="SporeMote"/>) and now and then
    /// one of the game's own glowing mushroom spores, which hang in the air a while as they do in the
    /// caves (DustID.GlowingMushroom: Dust.UpdateDust keeps it under 0.75 a tick and lit).
    /// </summary>
    public class MycellaneousSpore : ModProjectile
    {
        /// <summary>Ticks it drifts out from the hit, slowing, before it turns home.</summary>
        public const int OutTicks = 10;

        /// <summary>How much of its speed it keeps each tick while it drifts out.</summary>
        public const float Drag = 0.88f;

        /// <summary>Its speed as it turns home, what it gains each tick after, and the most it goes.</summary>
        public const float HomeSpeed = 3f;
        public const float HomeGain = 0.45f;
        public const float MaxSpeed = 24f;

        /// <summary>
        /// How much of the way toward home it turns each tick at first, and what that gains each tick:
        /// a wide curve at the start, and in the end it flies straight at them.
        /// </summary>
        public const float Turn = 0.08f;
        public const float TurnGain = 0.012f;

        /// <summary>Further than this from its shooter and it stops following: the life lands at once.</summary>
        public const float Reach = 2400f;

        /// <summary>The ticks it may fly. At the last the life lands anyway.</summary>
        public const int Flight = 300;

        /// <summary>Its tail, in ticks: a short streak when it is slow, a long one at full speed.</summary>
        public const int TailLength = 8;

        /// <summary>A mote shed for every this many pixels it flies, and at most this many a tick.</summary>
        public const float MoteEvery = 8f;
        public const int MostMotes = 3;

        /// <summary>One of the game's own spores left behind every this many ticks.</summary>
        public const int SporeEvery = 6;

        /// <summary>The Glowing Mushroom's blues, deep to pale, and a near-white heart.</summary>
        public static readonly Color Deep = new Color(50, 100, 255);
        public static readonly Color Light = new Color(120, 180, 255);
        public static readonly Color Heart = new Color(220, 240, 255);

        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        /// <summary>The life it carries.</summary>
        public int Life => (int)Projectile.ai[0];

        /// <summary>How big it is drawn: 1 for 2 life, larger for 3, larger again for a critical hit's 5.</summary>
        private float Size => 1f + 0.15f * Math.Max(0, Life - Mycellaneous.HealLow);

        /// <summary>Where its centre was on the ticks before, newest first. Each machine keeps its own.</summary>
        private readonly Vector2[] tail = new Vector2[TailLength];
        private int filled;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Flight;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            int age = (int)Projectile.localAI[0];
            Projectile.localAI[0]++;

            if (age == 0 && !Main.dedServ)
                Burst(Projectile.Center, Deep, 0.24f, 6, 12);

            Vector2 toward = owner.Center - Projectile.Center;
            float distance = toward.Length();

            if (age < OutTicks)
            {
                Projectile.velocity *= Drag;
            }
            else
            {
                if (Projectile.Hitbox.Intersects(owner.Hitbox) || distance > Reach || Projectile.timeLeft <= 2)
                {
                    Land(owner);
                    return;
                }

                float home = age - OutTicks;
                float speed = Math.Min(MaxSpeed, HomeSpeed + HomeGain * home);
                float turn = Math.Min(1f, Turn + TurnGain * home);

                // Never more than the distance left, so it does not fly past them and round again.
                Vector2 want = toward.SafeNormalize(Vector2.UnitY) * Math.Min(speed, distance);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
            }

            for (int i = tail.Length - 1; i > 0; i--)
                tail[i] = tail[i - 1];

            tail[0] = Projectile.Center;
            filled = Math.Min(filled + 1, tail.Length);

            if (!Main.dedServ)
                Shed(age);
        }

        /// <summary>The life lands, on the shooter's machine, and it is gone.</summary>
        private void Land(Player owner)
        {
            if (Projectile.owner == Main.myPlayer && !owner.moonLeech)
                owner.Heal(Life);

            Projectile.Kill();
        }

        /// <summary>
        /// The landing, seen everywhere: here, where it reached them, or where the kill the shooter's
        /// machine sends on reaches this one. A shooter who is dead or gone gets no flash.
        /// </summary>
        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            Player owner = Main.player[Projectile.owner];

            if (owner.active && !owner.dead)
                Burst(owner.Center, Light, 0.32f, 8, 16);
        }

        /// <summary>Motes along the stretch it flies this tick, a spore of the game's now and then, and its light.</summary>
        private void Shed(int age)
        {
            Vector2 from = Projectile.Center;
            Vector2 step = Projectile.velocity;
            float size = Size;

            int motes = Math.Clamp((int)(step.Length() / MoteEvery), 1, MostMotes);

            for (int i = 0; i < motes; i++)
            {
                Vector2 at = from + step * ((i + Main.rand.NextFloat()) / motes) + Main.rand.NextVector2Circular(2f, 2f);
                Vector2 drift = Main.rand.NextVector2Circular(0.6f, 0.6f) - step * 0.04f;
                Color colour = Main.rand.NextBool(3) ? Deep : Light;

                PADParticleHandler.SpawnParticle(new SporeMote(at, drift, colour,
                    Main.rand.NextFloat(0.6f, 1f) * size, Main.rand.Next(18, 31)));
            }

            if (age % SporeEvery == 0)
                Dust.NewDustPerfect(from, DustID.GlowingMushroom, Vector2.Zero, 0, default, Main.rand.NextFloat(0.5f, 0.7f));

            Lighting.AddLight(from, 0.12f * size, 0.3f * size, 0.65f * size);
        }

        /// <summary>A flash of light, a scatter of motes and two of the game's spores, at a point.</summary>
        private void Burst(Vector2 at, Color colour, float bloom, int motes, int life)
        {
            float size = Size;

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, colour * 0.8f, bloom * size, life));

            for (int i = 0; i < motes; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1.2f, 2.6f);
                Color mote = Main.rand.NextBool(3) ? Deep : Light;

                PADParticleHandler.SpawnParticle(new SporeMote(at + Main.rand.NextVector2Circular(4f, 4f), velocity, mote,
                    Main.rand.NextFloat(0.7f, 1.1f) * size, Main.rand.Next(20, 35)));
            }

            for (int i = 0; i < 2; i++)
                Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(8f, 8f), DustID.GlowingMushroom, Vector2.Zero, 0, default,
                    Main.rand.NextFloat(0.6f, 0.8f));
        }

        /// <summary>
        /// A short tail of light drawn as lines from where it was, and a bloom on it: deep blue, pale
        /// blue, a white heart, breathing a little. Additive, as Meagre Flame's ball is drawn, and the
        /// batch put back as the game had it (Main.DrawProjectiles).
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D bloom = TextureAssets.Projectile[Type].Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);
            Vector2 head = Projectile.Center - Main.screenPosition;
            Vector2 middle = bloom.Size() * 0.5f;
            float size = Size;
            float pulse = 1f + 0.12f * (float)Math.Sin(Projectile.localAI[0] * 0.5f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 from = head;

            for (int i = 0; i < filled; i++)
            {
                Vector2 to = tail[i] - Main.screenPosition;
                Vector2 run = to - from;
                float length = run.Length();

                if (length > 0.01f)
                {
                    float f = i / (float)tail.Length;
                    Color colour = Color.Lerp(Light, Deep, f) * ((1f - f) * (1f - f) * 0.8f);

                    Main.EntitySpriteDraw(pixel, from, one, colour, run.ToRotation(), new Vector2(0f, 0.5f),
                        new Vector2(length + 0.6f, 3.5f * size * (1f - 0.7f * f)), SpriteEffects.None, 0);
                }

                from = to;
            }

            Main.EntitySpriteDraw(bloom, head, null, Deep * 0.75f, 0f, middle, 0.17f * size * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, head, null, Light * 0.85f, 0f, middle, 0.1f * size, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, head, null, Heart, 0f, middle, 0.05f * size * pulse, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
