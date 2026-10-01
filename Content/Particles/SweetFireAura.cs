using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Projectiles.Mage;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// The ring of fire Sweet Fire wears: three flames going round the player at the waist,
    /// seen a little from above, each trailing a tail that cools from white through yellow and
    /// orange to red and rises as it goes, the far side of the ring dimmer and smaller than the
    /// near. Light by design: small, half see-through, a little light on the ground about the
    /// player, an ember now and then. It grows in, and when the buff ends it throws its embers
    /// out and goes.
    ///
    /// Drawn every frame about where the player is now, so it keeps to them when they run.
    /// A soft dot (SweetFireAura.png, white, stored premultiplied) in the alpha pass, each dot
    /// adding its light and covering a little of what is behind it, so the fire keeps its
    /// colour against a bright sky as well as in a cave (the flamethrowers' flames do the
    /// same). Made and kept one per player by SweetFirePlayer, on every client;
    /// <see cref="Gone"/> tells it the ring has finished.
    /// </summary>
    public class SweetFireAura : PADParticle
    {
        public override bool UseCustomDraw => true;

        // One per player, and it must not be refused by a full particle budget.
        public override bool Important => true;

        /// <summary>Flames going round, evenly spaced.</summary>
        public const int Heads = 3;

        /// <summary>Dots in a flame's tail, and the turn between one and the next, in radians.</summary>
        private const int Tail = 12;
        private const float TailStep = 0.1f;

        /// <summary>Radians a tick the ring turns: once round in about a second and a half.</summary>
        private const float Turn = 0.07f;

        /// <summary>The ring: its half width and half height in pixels, and how far below the player's centre it goes round.</summary>
        private const float RadiusX = 30f;
        private const float RadiusY = 10f;
        private const float Lower = 6f;

        /// <summary>How far a tail's last dot has risen, in pixels: fire goes up.</summary>
        private const float Rise = 10f;

        public const int GrowTicks = 20;

        /// <summary>The owner's ring fades out over the buff's last second.</summary>
        public const int FadeTicks = 60;

        /// <summary>When the buff is gone, the ring takes this long to go.</summary>
        public const int DropTicks = 16;

        /// <summary>White at the head, then the Meagre Flame's yellow and orange, then a red going out.</summary>
        private static readonly Color Head = new Color(255, 240, 200);
        private static readonly Color Red = new Color(190, 46, 18);

        public readonly int Owner;

        /// <summary>Finished and removed. SweetFirePlayer makes a new one when it sees this.</summary>
        public bool Gone { get; private set; }

        /// <summary>0 to 1: how much of it there is, grown in and faded out.</summary>
        public float Strength { get; private set; }

        /// <summary>Ticks since the buff went; -1 while it is on.</summary>
        private int dropping = -1;

        /// <summary>Where the ring has turned to, in radians.</summary>
        private float spin;

        public SweetFireAura(int owner)
        {
            Owner = owner;
            Position = Centre(Main.player[owner]);
            Color = Color.White;
            Scale = 1f;
            spin = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        private static Vector2 Centre(Player player) =>
            player.MountedCenter + new Vector2(0f, player.gfxOffY + Lower * player.gravDir);

        /// <summary>Grows past full and settles back.</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public override void Update()
        {
            Player player = Main.player[Owner];
            Position = Centre(player);

            bool held = player.active && !player.dead && player.HasBuff<SweetFire>();

            if (held)
            {
                dropping = -1;
            }
            else if (dropping < 0)
            {
                dropping = 0;
                LetGo();
            }
            else
            {
                dropping++;
            }

            if (dropping >= DropTicks)
            {
                Gone = true;
                Kill();
                return;
            }

            float grow = Math.Min(1f, Time / (float)GrowTicks);
            float fade = 1f;

            if (held && Owner == Main.myPlayer)
            {
                int index = player.FindBuffIndex(ModContent.BuffType<SweetFire>());

                if (index >= 0)
                    fade = Math.Min(1f, player.buffTime[index] / (float)FadeTicks);
            }

            if (dropping >= 0)
                fade *= 1f - dropping / (float)DropTicks;

            Scale = 0.4f + 0.6f * EaseOutBack(grow);
            Strength = grow * fade;
            spin += Turn;

            if (Strength <= 0.02f)
                return;

            // A little warm light where each flame is.
            for (int k = 0; k < Heads; k++)
                Lighting.AddLight(Dot(k, 0), new Vector3(0.36f, 0.17f, 0.05f) * Strength);

            // Now and then an ember comes off a flame and climbs.
            if (held && Main.rand.NextBool(4))
            {
                Vector2 at = Dot(Main.rand.Next(Heads), Main.rand.Next(3)) + Main.rand.NextVector2Circular(2f, 2f);
                Vector2 velocity = player.velocity * 0.6f + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1f));
                Color colour = Main.rand.NextBool(3) ? MeagreFlameBall.Yellow : MeagreFlameBall.Orange;

                PADParticleHandler.SpawnParticle(new Ember(at, velocity, colour, Main.rand.NextFloat(0.35f, 0.6f), Main.rand.Next(14, 24), lift: 0.04f));
            }
        }

        /// <summary>Where dot <paramref name="i"/> of flame <paramref name="k"/>'s tail is: 0 is the flame itself.</summary>
        private Vector2 Dot(int k, int i) => Position + Offset(Angle(k, i), i);

        private float Angle(int k, int i) => spin + MathHelper.TwoPi * k / Heads - i * TailStep;

        private Vector2 Offset(float angle, int i) =>
            new Vector2((float)Math.Cos(angle) * RadiusX, (float)Math.Sin(angle) * RadiusY) * Scale
            - new Vector2(0f, Rise * i / (Tail - 1f));

        /// <summary>The buff is over: the ring breathes out, and its embers go every way.</summary>
        private void LetGo()
        {
            if (Strength <= 0.05f)
                return;

            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.35f, Pitch = -0.4f }, Position);

            for (int i = 0; i < 16; i++)
            {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 on = new Vector2((float)Math.Cos(angle) * RadiusX, (float)Math.Sin(angle) * RadiusY) * Scale;
                Vector2 velocity = on.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(0.8f, 2f) - new Vector2(0f, 0.6f);
                Color colour = Main.rand.NextBool(3) ? MeagreFlameBall.Yellow : MeagreFlameBall.Orange;

                PADParticleHandler.SpawnParticle(new Ember(Position + on, velocity, colour, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(16, 26), lift: 0.05f));
            }
        }

        /// <summary>Along a tail, 0 at the flame and 1 at the end: white, yellow, orange, red.</summary>
        private static Color Heat(float along)
        {
            if (along < 0.12f)
                return Color.Lerp(Head, MeagreFlameBall.Yellow, along / 0.12f);

            if (along < 0.45f)
                return Color.Lerp(MeagreFlameBall.Yellow, MeagreFlameBall.Orange, (along - 0.12f) / 0.33f);

            return Color.Lerp(MeagreFlameBall.Orange, Red, (along - 0.45f) / 0.55f);
        }

        /// <summary>A colour for the premultiplied dot: this much of its light added, this much of what is behind covered.</summary>
        private static Color Lit(Color colour, float light, float cover)
        {
            Vector3 rgb = colour.ToVector3() * light;
            return new Color(rgb.X, rgb.Y, rgb.Z, cover);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            if (Strength <= 0.02f)
                return;

            Texture2D dot = PADParticleHandler.GetTexture(Type);
            Vector2 origin = dot.Size() * 0.5f;

            for (int k = 0; k < Heads; k++)
            {
                // The tail first, so the flame is drawn over it.
                for (int i = Tail - 1; i >= 0; i--)
                {
                    float angle = Angle(k, i);
                    float along = i / (Tail - 1f);

                    // 0 at the back of the ring, 1 at the front.
                    float near = 0.5f + 0.5f * (float)Math.Sin(angle);
                    float flicker = 0.85f + 0.15f * (float)Math.Sin(Time * 0.9f + k * 2.1f + i * 1.3f);
                    float alpha = Strength * (float)Math.Pow(1f - along, 1.2) * (0.35f + 0.65f * near) * flicker;
                    float size = (1.35f - 0.75f * along) * (0.75f + 0.3f * near) * Scale;

                    Vector2 at = Position + Offset(angle, i) - Main.screenPosition;

                    // A warm glow round the flame itself.
                    if (i == 0)
                        spriteBatch.Draw(dot, at, null, Lit(MeagreFlameBall.Orange, 0.35f * alpha, 0.25f * alpha), 0f, origin, size * 1.6f, SpriteEffects.None, 0f);

                    spriteBatch.Draw(dot, at, null, Lit(Heat(along), 0.9f * alpha, 0.55f * alpha), 0f, origin, size * 0.7f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
