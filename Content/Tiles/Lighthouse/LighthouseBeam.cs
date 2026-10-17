using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Tiles.Lighthouse
{
    /// <summary>
    /// The lighthouse's beams over the Vast Ocean (LighthouseLens, VastOceanIsland), drawn after the
    /// tiles: two beams from the lens, opposite each other, turning round it once in twelve
    /// seconds. Seen from the side a beam is as long as it points across (the sine of its turn), so
    /// each sweeps out to one side and draws back in; as one comes round to point at whoever is
    /// looking the lens flares, a flash every six seconds; a beam coming toward them is brighter
    /// than one going away. Lit from half an hour before dusk until half an hour after dawn, fading
    /// in and out over that half hour, with a small glow about the lamp all the while.
    ///
    /// Only in the Vast Ocean, only if the lens is where the place put it, and only when it is
    /// near enough the screen for a beam to cross it. Drawn the game's way for light (no alpha of
    /// its own, which adds it to what is behind it), as the waygate's light is (WaygateRadiance,
    /// whose round falloff it borrows for the glow and the flare); the beam's picture beside this
    /// file is mine (PlaceholderTextures.md).
    /// </summary>
    public class LighthouseBeam : ModSystem
    {
        /// <summary>Seconds to a whole turn.</summary>
        public const float Period = 12f;

        /// <summary>The longest a beam is drawn, and how wide it has spread at its end, in pixels: its body, and the narrower heart in it.</summary>
        private const float Reach = 1100f, Spread = 300f, Heart = 90f;

        /// <summary>Half an hour of the game's clock, in its ticks.</summary>
        private const double HalfHour = 1800.0;

        /// <summary>The lamp's light: a warm white.</summary>
        private static readonly Color Warm = new Color(255, 236, 196);

        private static Asset<Texture2D> beam, glow;

        /// <summary>How far round the lens has turned, 0 to two pi, by the clock alone, so that everyone sees it the same.</summary>
        public static float Turn => Main.GlobalTimeWrappedHourly % Period / Period * MathHelper.TwoPi;

        /// <summary>The lens's picture for its turn (LighthouseLens): eight frames to the half turn, its two bull's-eyes alike.</summary>
        public static int LensFrame => (int)(Turn / MathHelper.Pi * LighthouseLens.Frames) % LighthouseLens.Frames;

        /// <summary>How lit the lamp is, 0 to 1: all night, and fading over the half hour after dawn and before dusk.</summary>
        public static float Lit
        {
            get
            {
                if (!Main.dayTime)
                    return 1f;

                double nearest = Math.Min(Main.time, Main.dayLength - Main.time);
                float t = MathHelper.Clamp((float)(1.0 - nearest / HalfHour), 0f, 1f);

                return t * t * (3f - 2f * t);
            }
        }

        public override void Load()
        {
            if (Main.dedServ)
                return;

            beam = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/Lighthouse/LighthouseBeam");
            glow = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateLight");
        }

        public override void Unload()
        {
            beam = null;
            glow = null;
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || beam == null || !beam.IsLoaded || !glow.IsLoaded || SubworldKeeper.Current is not VastOcean)
                return;

            float lit = Lit;
            if (lit <= 0f)
                return;

            Point at = VastOceanIsland.LensAt;
            if (!WorldGen.InWorld(at.X, at.Y, 1))
                return;

            Tile tile = Main.tile[at.X, at.Y];
            if (!tile.HasTile || tile.TileType != ModContent.TileType<LighthouseLens>() || tile.TileFrameX != 0 || tile.TileFrameY != 0)
                return;

            Vector2 heart = new Vector2((at.X + LighthouseLens.Wide * 0.5f) * 16f, (at.Y + 1.75f) * 16f);
            Vector2 middle = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            if (Math.Abs(heart.X - middle.X) > Reach + Main.screenWidth || Math.Abs(heart.Y - middle.Y) > Spread + Main.screenHeight)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            try
            {
                Draw(Main.spriteBatch, heart - Main.screenPosition, lit);
            }
            finally
            {
                Main.spriteBatch.End();
            }
        }

        private static void Draw(SpriteBatch batch, Vector2 at, float lit)
        {
            Texture2D cone = beam.Value, round = glow.Value;
            Vector2 coneOrigin = new Vector2(0f, cone.Height * 0.5f);
            Vector2 roundOrigin = round.Size() * 0.5f;
            float turn = Turn;

            batch.Draw(round, at, null, Light(0.45f * lit), 0f, roundOrigin, 70f / round.Width, SpriteEffects.None, 0f);

            for (int k = 0; k < 2; k++)
            {
                float psi = turn + k * MathHelper.Pi;
                float across = (float)Math.Sin(psi), toward = (float)Math.Cos(psi);
                float length = Reach * Math.Abs(across);

                if (length > 12f)
                {
                    float strength = lit * (0.4f + 0.45f * Math.Max(0f, toward)) * MathHelper.Clamp(length / 160f, 0f, 1f);
                    float rotation = across > 0f ? 0f : MathHelper.Pi;

                    batch.Draw(cone, at, null, Light(0.55f * strength), rotation, coneOrigin, new Vector2(length / cone.Width, Spread / cone.Height), SpriteEffects.None, 0f);
                    batch.Draw(cone, at, null, Light(0.6f * strength), rotation, coneOrigin, new Vector2(length / cone.Width, Heart / cone.Height), SpriteEffects.None, 0f);
                }

                if (toward > 0.8f)
                {
                    float flare = (toward - 0.8f) / 0.2f;
                    flare *= flare * lit;

                    batch.Draw(round, at, null, Light(0.9f * flare), 0f, roundOrigin, 300f / round.Width, SpriteEffects.None, 0f);
                    batch.Draw(round, at, null, Light(flare), 0f, roundOrigin, 90f / round.Width, SpriteEffects.None, 0f);
                    batch.Draw(round, at, null, Light(0.6f * flare), 0f, roundOrigin, new Vector2(520f, 26f) / round.Width, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>The lamp's colour at this strength, with no alpha: drawn blended as usual over what is behind it, it is added to it.</summary>
        private static Color Light(float strength)
        {
            strength = MathHelper.Clamp(strength, 0f, 1f);

            return new Color((byte)(Warm.R * strength), (byte)(Warm.G * strength), (byte)(Warm.B * strength), (byte)0);
        }
    }
}
