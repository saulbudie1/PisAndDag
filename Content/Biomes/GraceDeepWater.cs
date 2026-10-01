using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The water under Grace: clear, and a pale mint (118, 227, 194) rather than blue. The
    /// textures are vanilla's own water, recoloured, and see-through: vanilla's water texture
    /// is opaque and the liquid renderer draws it at 0.6, while this one is 0.40 to 0.70 opaque
    /// itself, so what is behind it shows through three parts in four. They are written
    /// premultiplied, as the liquid and tile batches expect of every texture.
    ///
    /// Textures, all in this folder, all loaded by name convention:
    ///   GraceDeepWaterStyle.png         48 x 1360  the liquid, as Misc/water_0
    ///   GraceDeepWaterStyle_Block.png   306 x 16   as Liquid_0
    ///   GraceDeepWaterStyle_Slope.png   72 x 16    as LiquidSlope_0
    ///   GraceDeepWaterfallStyle.png     512 x 40   as Waterfall_0
    ///   GraceDeepSplash.png             10 x 30    the splash dust, as vanilla's water dust
    ///   GraceDeepDroplet.png            18 x 300   the drip off a ceiling, as Gore_706
    ///
    /// The biome picks it (GraceDeepBiome.WaterStyle), and GraceDeepAir makes sure of it: tML
    /// only lets a modded water style in where the surface biome's background has not already
    /// chosen one.
    /// </summary>
    public class GraceDeepWaterStyle : ModWaterStyle
    {
        public static readonly Color Mint = new Color(118, 227, 194);

        public override int ChooseWaterfallStyle() => ModContent.GetInstance<GraceDeepWaterfallStyle>().Slot;

        public override int GetSplashDust() => ModContent.DustType<GraceDeepSplash>();

        public override int GetDropletGore() => ModContent.GoreType<GraceDeepDroplet>();

        // Light through it: barely held back, and what is lost is lost from the red, so what
        // is seen through it goes a shade toward the mint. Vanilla's is 0.88, 0.96, 1.015.
        public override void LightColorMultiplier(ref float r, ref float g, ref float b)
        {
            r = 0.93f;
            g = 0.99f;
            b = 0.97f;
        }

        public override Color BiomeHairColor() => Mint;
    }

    /// <summary>Its falls, the same mint and as clear.</summary>
    public class GraceDeepWaterfallStyle : ModWaterfallStyle
    {
    }

    /// <summary>A splash of it: vanilla's water dust, in mint.</summary>
    public class GraceDeepSplash : ModDust
    {
        public override void SetStaticDefaults()
        {
            UpdateType = DustID.Water;
        }
    }

    /// <summary>A drop of it off a ceiling: vanilla's drip, in mint.</summary>
    public class GraceDeepDroplet : ModGore
    {
        public override void SetStaticDefaults()
        {
            ChildSafety.SafeGore[Type] = true;
            GoreID.Sets.LiquidDroplet[Type] = true;
            UpdateType = GoreID.WaterDrip;
        }

        public override void OnSpawn(Gore gore, IEntitySource source)
        {
            // As vanilla's drips: fifteen frames, behind the tiles, and slow to go.
            gore.numFrames = 15;
            gore.behindTiles = true;
            gore.timeLeft = Gore.goreTime * 3;
        }
    }
}
