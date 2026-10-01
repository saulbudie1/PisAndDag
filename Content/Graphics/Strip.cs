using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Graphics
{
    /// <summary>
    /// A ribbon of triangles drawn straight to the graphics device: the one shape a sprite batch
    /// cannot draw, a band that bends. A trail is laid down as pairs of points, one on each edge
    /// of the band; each point has its own colour, and the texture runs along the band by the u
    /// given with each pair and across it from the first edge (v 0) to the second (v 1).
    ///
    /// How it draws: <see cref="Begin"/>, a <see cref="Pair"/> for every step along the band,
    /// then <see cref="Draw"/> with the texture, with NO sprite batch open (End the batch before,
    /// Begin it again after). Points are in world pixels and go through the same view the sprite
    /// batches use (Main.GameViewMatrix: zoom, and the flip under reversed gravity), so the band
    /// sits exactly where a sprite drawn at the same place would.
    ///
    /// The blend is premultiplied alpha, as every other draw in the game: a colour with alpha 0
    /// adds its light, and a colour with alpha adds nothing and darkens what is behind it by that
    /// alpha. Textures are stored premultiplied for it (white, as bright as they are opaque).
    ///
    /// It is vanilla's own BasicEffect (texture times vertex colour), made on the main thread the
    /// first time anything is drawn and let go of when the mod unloads. Client only.
    /// </summary>
    public class Strip : ModSystem
    {
        private static BasicEffect effect;

        private static VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[256];

        private static int count;

        /// <summary>Starts a new band. Whatever was laid down before is dropped.</summary>
        public static void Begin() => count = 0;

        /// <summary>
        /// The next step along the band: <paramref name="first"/> on the v 0 edge,
        /// <paramref name="second"/> on the v 1 edge, both in world pixels, at
        /// <paramref name="u"/> along the texture.
        /// </summary>
        public static void Pair(Vector2 first, Vector2 second, Color firstColour, Color secondColour, float u)
        {
            if (count + 2 > vertices.Length)
                Array.Resize(ref vertices, vertices.Length * 2);

            Vector2 screen = Main.screenPosition;

            vertices[count++] = new VertexPositionColorTexture(new Vector3(first - screen, 0f), firstColour, new Vector2(u, 0f));
            vertices[count++] = new VertexPositionColorTexture(new Vector3(second - screen, 0f), secondColour, new Vector2(u, 1f));
        }

        /// <summary>
        /// Draws the band laid down since <see cref="Begin"/>. Needs at least two pairs, and no
        /// sprite batch open. Leaves the device's blend, depth, rasteriser and first sampler
        /// changed; the next sprite batch to Begin sets them back.
        /// </summary>
        public static void Draw(Texture2D texture, SamplerState sampler = null)
        {
            if (Main.dedServ || texture == null || count < 4)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;

            effect ??= new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = true,
            };

            Viewport viewport = device.Viewport;

            effect.World = Matrix.Identity;
            effect.View = Main.GameViewMatrix.TransformationMatrix;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, -1f, 1f);
            effect.Texture = texture;

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique.Passes[0].Apply();
            device.SamplerStates[0] = sampler ?? SamplerState.LinearClamp;

            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, count - 2);
        }

        public override void Unload()
        {
            BasicEffect old = effect;
            effect = null;
            count = 0;

            if (old != null)
                Main.QueueMainThreadAction(old.Dispose);
        }
    }
}
