using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Cold coming off something frozen: a pale mist that sinks, spreads and thins, the way cold
    /// air does. DustFog's grainy puffs, tinted and turned the other way: where dust lifts, this
    /// settles. Lit by the light where it hangs, with a floor of its own (glow) so it still shows
    /// in the dark beside the thing that made it.
    ///
    /// No two wisps alike: each is stretched flat by its own amount, tilted a little off level,
    /// maybe mirrored, and swells, sways and sinks at its own rate, so a trail of them breaks up
    /// instead of reading as a row of the same cloud. <see cref="Spawn"/> also rolls the size,
    /// most of them small and a few large.
    /// </summary>
    public class ColdFog : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Particles/DustFog";

        public override bool SetLifetime => true;

        public override int FrameVariants => 3;

        public override bool UseCustomDraw => true;

        /// <summary>The mist's own colour, before the light and the fade.</summary>
        private readonly Color tint;

        /// <summary>How thick it is at its thickest, 0 to 1.</summary>
        private readonly float thickness;

        /// <summary>How much of its colour it keeps in the dark, 0 to 1.</summary>
        private readonly float glow;

        /// <summary>How much wider than tall it is drawn: mist lies flat.</summary>
        private readonly float stretch;

        private readonly float swell;
        private readonly float sway;
        private readonly float sink;
        private readonly float phase;
        private readonly float spin;
        private readonly SpriteEffects flip;

        /// <param name="position">Where it starts, in world pixels.</param>
        /// <param name="velocity">How it is thrown; it slows quickly, then sinks.</param>
        /// <param name="color">The mist's colour.</param>
        /// <param name="lifetime">Ticks it hangs.</param>
        /// <param name="scale">Its size at the start; it spreads as it thins.</param>
        /// <param name="thickness">How opaque it is at its thickest.</param>
        /// <param name="glow">How much of its colour it keeps where there is no light.</param>
        public ColdFog(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale,
            float thickness = 0.3f, float glow = 0.35f)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = scale;

            tint = color;
            this.thickness = thickness;
            this.glow = glow;
            stretch = Main.rand.NextFloat(1f, 1.9f);
            swell = scale * Main.rand.NextFloat(0.6f, 2f) / Math.Max(1, lifetime);
            sway = Main.rand.NextFloat(0.004f, 0.016f);
            sink = Main.rand.NextFloat(0.006f, 0.016f);
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.004f, 0.004f);
            flip = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Rotation = Main.rand.NextFloat(-0.35f, 0.35f);
            Variant = Main.rand.Next(FrameVariants);
            Color = Color.Transparent;
        }

        /// <summary>
        /// One wisp, sized at random between small and large, most of them nearer small; its
        /// thickness and how long it lasts rolled too. Does nothing on a server.
        /// </summary>
        public static void Spawn(Vector2 position, Vector2 velocity, Color color, float small, float large,
            int shortest, int longest, float glow = 0.5f)
        {
            if (Main.dedServ)
                return;

            float size = MathHelper.Lerp(small, large, (float)Math.Pow(Main.rand.NextFloat(), 2.0));

            PADParticleHandler.SpawnParticle(new ColdFog(position, velocity, color,
                Main.rand.Next(shortest, longest + 1), size, Main.rand.NextFloat(0.26f, 0.5f), glow));
        }

        public override void Update()
        {
            float t = LifetimeCompletion;

            // Thrown, it loses its push fast; then it sinks, slowly, and drifts.
            Velocity *= 0.92f;
            Velocity.Y += sink;
            Velocity.X += (float)Math.Sin(Time * 0.05f + phase) * sway;

            Scale += swell;
            Rotation += spin;

            // In quickly, then a long thinning out.
            float fade = Math.Min(1f, t / 0.12f) * (1f - t) * (1f - t);

            Point at = Position.ToTileCoordinates();
            Color light = WorldGen.InWorld(at.X, at.Y) ? Lighting.GetColor(at) : Color.White;

            Color = Color.Lerp(tint.MultiplyRGB(light), tint, glow) * (thickness * fade);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture is null)
                return;

            Rectangle frame = texture.Frame(1, FrameVariants, 0, Variant);

            spriteBatch.Draw(texture, Position - Main.screenPosition, frame, Color, Rotation, frame.Size() * 0.5f,
                new Vector2(Scale * stretch, Scale), flip, 0f);
        }
    }
}
