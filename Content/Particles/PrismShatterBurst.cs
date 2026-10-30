using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Glass breaking as light, where it broke (PrismShatter.DrawBurst), for as long as its light lasts; with the glass's
    /// own panes thrown out of it if it was a mirror (PrismShatter.DrawGlassPanes): the Everclear Mirror's copy as it
    /// breaks (MimicSystem.Shatter). The world about it lit by its flash, going. Drawn with the particles (over the world),
    /// from its seed and its age alone.
    /// </summary>
    public class PrismShatterBurst : PADParticle
    {
        /// <summary>Never drawn as a sprite (its draw is its own); a texture the handler can load for it.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/GlowStar";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool Important => true;

        private readonly int seed;
        private readonly float reach;
        private readonly PrismShatter.Look look;
        private readonly bool panes;

        public PrismShatterBurst(Vector2 at, int seed, float reach, PrismShatter.Look look, bool panes)
        {
            Position = at;
            Velocity = Vector2.Zero;
            Lifetime = PrismShatter.Life;
            Scale = 1f;
            Color = Color.White;
            this.seed = seed;
            this.reach = reach;
            this.look = look;
            this.panes = panes;
        }

        public override void Update()
        {
            if (Time < 24)
            {
                float lit = 1f - Time / 24f;
                Lighting.AddLight(Position, 1.4f * lit, 1.35f * lit, 1.5f * lit);
            }
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            // Its light is stretched far past its textures' size: sampled smoothly, as hers is (the particles' batch samples
            // by the nearest texel whenever the game draws to its own target, which steps and blocks a glow). The batch is
            // ended for it, begun again smooth, and at the end begun again as the handler began it (its alpha-blended
            // bucket), so the particles after it are drawn as before.
            spriteBatch.End();
            Begin(spriteBatch, SamplerState.LinearClamp);

            PrismShatter.DrawBurst(spriteBatch, Position, seed, Time, reach, look);

            if (panes)
            {
                // The panes are triangles, drawn straight to the device: no batch open for them.
                spriteBatch.End();
                PrismShatter.DrawGlassPanes(Position, seed, Time, reach, look);
                Begin(spriteBatch, SamplerState.LinearClamp);
                PrismShatter.DrawGlassEdges(spriteBatch, Position, seed, Time, reach, look);
            }

            spriteBatch.End();
            Begin(spriteBatch, Main.DefaultSamplerState);
        }

        private static void Begin(SpriteBatch spriteBatch, SamplerState sampler) =>
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, sampler, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
    }
}
