using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// What the Vast Ocean floor's living things and vents give off (SeaLifeEffects spawns them),
    /// each in the light where it is (the game's, and whatever lights it), so that in the dark of
    /// the deep they are only seen where a light falls on them; the glow of the octocorals and
    /// the blink of the plankton are light of their own.
    /// </summary>
    internal static class SeaLight
    {
        /// <summary>The light on this point of the world.</summary>
        public static Color At(Vector2 at)
        {
            int x = (int)(at.X / 16f), y = (int)(at.Y / 16f);

            return WorldGen.InWorld(x, y, 1) ? Lighting.GetColor(x, y) : Color.Black;
        }

        public static float Smooth(float from, float to, float value)
        {
            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// Black smoke from a black smoker's mouth: a dark puff, rising fast at first and slower as it
    /// spreads, growing, carried a little by the current, gone in three or four seconds.
    /// </summary>
    public class VentSmoke : PADParticle
    {
        private readonly float spin;

        public override int FrameVariants => 3;

        public override bool SetLifetime => true;

        public VentSmoke(Vector2 at, Vector2 velocity, int life)
        {
            Position = at;
            Velocity = velocity;
            Lifetime = life;
            Variant = Main.rand.Next(3);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            Scale = 0.5f;
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity.Y = MathHelper.Lerp(Velocity.Y, -0.32f, 0.02f);
            Velocity.X = Velocity.X * 0.985f + 0.004f;
            Rotation += spin;
            Scale = 0.5f + 1.8f * (1f - (1f - t) * (1f - t));

            float opacity = SeaLight.Smooth(0f, 0.08f, t) * (1f - SeaLight.Smooth(0.5f, 1f, t)) * 0.9f;

            Color = SeaLight.At(Position) * opacity;
        }
    }

    /// <summary>
    /// A white fleck of the bacteria that grow where vent water mixes with the sea (what comes out
    /// of a vent in flocks after an eruption): drifting up slowly, wavering, fading.
    /// </summary>
    public class SeaFloc : PADParticle
    {
        private readonly float phase;

        public override int FrameVariants => 3;

        public override bool SetLifetime => true;

        public SeaFloc(Vector2 at, Vector2 velocity, int life)
        {
            Position = at;
            Velocity = velocity;
            Lifetime = life;
            Variant = Main.rand.Next(3);
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            Scale = Main.rand.NextFloat(0.8f, 1.15f);
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity *= 0.985f;
            Velocity.Y -= 0.003f;
            Position.X += (float)Math.Sin(Time * 0.05f + phase) * 0.12f;

            float opacity = SeaLight.Smooth(0f, 0.1f, t) * (1f - SeaLight.Smooth(0.6f, 1f, t)) * 0.85f;

            Color = SeaLight.At(Position) * opacity;
        }
    }

    /// <summary>
    /// A speck of plankton drifting by a filter feeder (the corals, the sponges, the sea lilies
    /// live on them); one in a dozen of them blinks blue with its own light as it goes.
    /// </summary>
    public class SeaPlankton : PADParticle
    {
        private readonly bool blinks;
        private readonly float phase;

        public override int FrameVariants => 3;

        public override bool SetLifetime => true;

        public override bool UseAdditiveBlend => blinks;

        public SeaPlankton(Vector2 at, Vector2 velocity, int life)
        {
            Position = at;
            Velocity = velocity;
            Lifetime = life;
            Variant = Main.rand.Next(3);
            blinks = Main.rand.NextBool(12);
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            Scale = 1f;
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity = Velocity.RotatedBy(Math.Sin(Time * 0.03f + phase) * 0.01f) * 0.998f;

            float opacity = SeaLight.Smooth(0f, 0.15f, t) * (1f - SeaLight.Smooth(0.7f, 1f, t));

            if (blinks)
            {
                float blink = Math.Max(0f, (float)Math.Sin(Time * 0.21f + phase));

                // Drawn added (BlendState.Additive takes the colour times its alpha), as the mod's other lights are.
                Color = new Color(90, 190, 255) * (opacity * blink);
            }
            else
            {
                Color = SeaLight.At(Position) * (opacity * 0.8f);
            }
        }
    }

    /// <summary>
    /// The octocorals' light let off where they are touched: a mote of blue-green drifting off and
    /// going out, its own light, added to what is behind it.
    /// </summary>
    public class SeaGlow : PADParticle
    {
        private readonly float hue;

        public override bool SetLifetime => true;

        public override bool UseAdditiveBlend => true;

        public SeaGlow(Vector2 at, Vector2 velocity, int life)
        {
            Position = at;
            Velocity = velocity;
            Lifetime = life;
            hue = Main.rand.NextFloat();
            Scale = Main.rand.NextFloat(0.55f, 1.1f);
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity *= 0.97f;
            Velocity.Y -= 0.004f;

            float strength = SeaLight.Smooth(0f, 0.12f, t) * (1f - SeaLight.Smooth(0.35f, 1f, t));
            Color hueColour = Color.Lerp(new Color(70, 170, 255), new Color(90, 255, 190), hue);

            Color = hueColour * strength;
        }
    }

    /// <summary>
    /// The shimmer of clear warm water pouring up from a white chimney: a faint streak rising fast
    /// and gone in a moment.
    /// </summary>
    public class VentShimmer : PADParticle
    {
        public override int FrameVariants => 3;

        public override bool SetLifetime => true;

        public VentShimmer(Vector2 at, Vector2 velocity, int life)
        {
            Position = at;
            Velocity = velocity;
            Lifetime = life;
            Variant = Main.rand.Next(3);
            Scale = 1f;
            Color = Color.Transparent;
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            Velocity.X += Main.rand.NextFloat(-0.05f, 0.05f);
            Velocity.X *= 0.95f;

            float opacity = SeaLight.Smooth(0f, 0.2f, t) * (1f - SeaLight.Smooth(0.4f, 1f, t)) * 0.55f;

            Color = SeaLight.At(Position) * opacity;
        }
    }
}
