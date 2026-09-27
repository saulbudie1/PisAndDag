using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// The shield Nurtured wears: a faint green bubble at the player's chest, 24 pixels across,
    /// that grows in, breathes, pulses, and lets go in a fall of leaves when the buff ends.
    ///
    /// Four layers of one sheet (NurtureShield.png, three frames), all additive and tinted
    /// here, the way Fables' Rover Drive builds its bubble out of a soft fill and a moving
    /// pattern, but with sprites where that uses a shader:
    ///   the bubble, a faint fill that brightens toward the rim;
    ///   plant cells over it, twice, turning opposite ways at different sizes, so the pattern
    ///   is never still;
    ///   the rim, a thin ring that flares on every pulse;
    ///   the pulse, the same ring swelling outward and thinning away every
    ///   <see cref="PulseEvery"/> ticks.
    ///
    /// It follows its player and reads the buff itself. Made and kept one per player by
    /// NurturedPlayer, on every client; <see cref="Gone"/> tells it the shield has finished.
    /// The motes that run about it (NurtureMote) take their strength and size from it.
    /// </summary>
    public class NurtureShield : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/NurtureShield";
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        // One per player, and it must not be refused by a full particle budget.
        public override bool Important => true;

        /// <summary>Motes that run about it.</summary>
        public const int Motes = 5;

        public const int GrowTicks = 26;
        public const int PulseEvery = 60;
        public const int PulseTicks = 34;

        /// <summary>The owner's shield fades out over the buff's last second.</summary>
        public const int FadeTicks = 60;

        /// <summary>When the buff is gone, the shield takes this long to go.</summary>
        public const int DropTicks = 20;

        /// <summary>
        /// The rim's radius at full size, in pixels: a shield 24 across, about half the player's
        /// height. The sheet is drawn at this size, 32 by 32 a frame with the rim at 12, so it
        /// is never scaled down: the game draws with point sampling, which breaks thin lines
        /// drawn smaller than they were made.
        /// </summary>
        private const float RimRadius = 12f;

        // Flora and Fauna: the element's own green, a lighter one, and a pale one for light.
        public static readonly Color Deep = new Color(0x1d, 0x9f, 0x49);
        public static readonly Color Light = new Color(0x6f, 0xd0, 0x6a);
        public static readonly Color Pale = new Color(0xd4, 0xf5, 0xb0);

        public readonly int Owner;

        /// <summary>Finished and removed. NurturedPlayer makes a new one when it sees this.</summary>
        public bool Gone { get; private set; }

        /// <summary>0 to 1: how much of it there is, grown in and faded out.</summary>
        public float Strength { get; private set; }

        /// <summary>The rim's radius now, in pixels.</summary>
        public float Radius => RimRadius * Scale;

        /// <summary>Ticks since the buff went; -1 while it is on.</summary>
        private int dropping = -1;

        /// <summary>When the last pulse went out, in the particle's own ticks.</summary>
        private int lastPulse;

        /// <summary>1 when the incantation is said again over it, falling back to 0.</summary>
        private float refresh;

        public NurtureShield(int owner)
        {
            Owner = owner;
            Player player = Main.player[owner];
            Position = player.MountedCenter + new Vector2(0f, player.gfxOffY);
            Color = Color.White;
            Scale = 0f;

            // The first pulse goes out just as it finishes growing.
            lastPulse = GrowTicks - PulseEvery;
        }

        /// <summary>Said again before it ran out: it swells a little and pulses now.</summary>
        public void Refresh()
        {
            refresh = 1f;
            lastPulse = Time;
        }

        /// <summary>Grows past full and settles back: a little give, like something alive.</summary>
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
            Position = player.MountedCenter + new Vector2(0f, player.gfxOffY);

            int type = ModContent.BuffType<Nurtured>();
            bool held = player.active && !player.dead && player.HasBuff(type);

            if (held)
            {
                dropping = -1;
            }
            else if (dropping < 0)
            {
                dropping = 0;
                LetGo(player);
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
            float breath = 1f + 0.025f * (float)Math.Sin(Time * 0.07f);

            float fade = 1f;

            if (held && Owner == Main.myPlayer)
            {
                int index = player.FindBuffIndex(type);

                if (index >= 0)
                    fade = Math.Min(1f, player.buffTime[index] / (float)FadeTicks);
            }

            if (dropping >= 0)
                fade *= 1f - dropping / (float)DropTicks;

            Scale = EaseOutBack(grow) * breath * (1f + 0.06f * refresh);
            Strength = Math.Min(1f, grow * 1.5f) * fade;
            refresh *= 0.92f;

            if (Time - lastPulse >= PulseEvery)
                lastPulse = Time;

            Lighting.AddLight(Position, Deep.ToVector3() * 0.28f * Strength);

            if (!held || Strength <= 0.05f)
                return;

            // Now and then a leaf comes loose from it, and a glint runs over it.
            if (Main.rand.NextBool(90))
                Leaves(player, 1, 0.6f);

            if (Main.rand.NextBool(14))
            {
                Vector2 on = Main.rand.NextVector2CircularEdge(Radius, Radius);
                Dust dust = Dust.NewDustPerfect(Position + on, DustID.DryadsWard,
                    on.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.3f, 0.9f),
                    0, default, Main.rand.NextFloat(0.6f, 0.95f));
                dust.noGravity = true;
            }
        }

        /// <summary>The buff is over: a rustle, and the shield goes to leaves.</summary>
        private void LetGo(Player player)
        {
            if (Strength <= 0.05f)
                return;

            Leaves(player, Main.rand.Next(4, 7), 1.6f);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.55f, Pitch = 0.2f, PitchVariance = 0.15f }, Position);

            for (int i = 0; i < 12; i++)
            {
                Vector2 on = Main.rand.NextVector2CircularEdge(Radius, Radius);
                Dust dust = Dust.NewDustPerfect(Position + on, DustID.DryadsWard,
                    on.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.6f, 1.8f), 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }

        /// <summary>Tree leaves off the upper half of the rim, which flutter down on their own.</summary>
        private void Leaves(Player player, int count, float push)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Main.rand.NextFloat(-MathHelper.Pi, 0f);
                Vector2 on = angle.ToRotationVector2() * Radius;
                Vector2 velocity = on.SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(0.3f, 1f) * push;

                Gore.NewGore(player.GetSource_FromThis(), Position + on - new Vector2(8f, 8f), velocity,
                    GoreID.TreeLeaf_Normal, Main.rand.NextFloat(0.8f, 1f));
            }
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture is null || Strength <= 0f || Scale <= 0f)
                return;

            Rectangle bubble = texture.Frame(1, 3, 0, 0);
            Rectangle cells = texture.Frame(1, 3, 0, 1);
            Rectangle ring = texture.Frame(1, 3, 0, 2);
            Vector2 origin = bubble.Size() * 0.5f;
            Vector2 at = Position - Main.screenPosition;
            float a = Strength;
            float s = Scale;

            // How far through the last pulse: 0 as it goes out, 1 once it has gone.
            float p = (Time - lastPulse) / (float)PulseTicks;
            float flare = p < 1f ? 1f - p : 0f;

            // Faint: a presence round the chest, not a wall.
            spriteBatch.Draw(texture, at, bubble, Deep * (0.32f * a), 0f, origin, s, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, at, cells, Light * (0.2f * a), Time * 0.004f, origin, s * 0.98f, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, at, cells, Deep * (0.12f * a), 1.1f - Time * 0.006f, origin, s * 0.86f,
                SpriteEffects.FlipHorizontally, 0f);
            spriteBatch.Draw(texture, at, ring, Pale * ((0.16f + 0.24f * flare + 0.3f * refresh) * a), 0f, origin, s,
                SpriteEffects.None, 0f);

            if (p < 1f)
            {
                float out1 = 1f - (1f - p) * (1f - p);
                spriteBatch.Draw(texture, at, ring, Light * (0.35f * (1f - p) * a), 0f, origin, s * (1f + 0.35f * out1),
                    SpriteEffects.None, 0f);
            }
        }
    }
}
