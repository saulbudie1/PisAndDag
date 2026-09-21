using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The three parallax layers behind the Mile. The game draws them itself: each
    /// texture is tiled across the screen at the scale, parallax and height the vanilla
    /// forest uses for the same layer, so only the picture is ours.
    ///
    /// Textures live in Content/Biomes/Backgrounds/ (any folder with "Backgrounds/" in
    /// its path is picked up automatically):
    ///
    ///   WastedMileFar.png     1024 x 500   drawn far back, scale ~2x, slowest parallax;
    ///                                      the horizon and whatever is burning on it
    ///   WastedMileMiddle.png  1024 x 500   the ridge line
    ///   WastedMileClose.png   1024 x 400   right behind the tiles: posts, wire, mounds
    ///
    /// Width must be 1024 or the tiling seam shows. Height is free; Calamity's run 400
    /// to 500 and the top of the image is what lines up with the horizon, so a taller
    /// texture just reaches further down (the bottom is covered by tiles anyway).
    /// Transparent above the skyline; the sky is drawn under these.
    /// </summary>
    public class WastedMileSurfaceBackground : ModSurfaceBackgroundStyle
    {
        private const string Folder = "Content/Biomes/Backgrounds/";

        /// <summary>
        /// How far up the close layer sits from where the game would put it, in screen
        /// pixels. The far and middle layers have no such knob (the game positions them
        /// itself), so those two are raised inside their textures instead: the picture
        /// starts higher in the file. This one is drawn at 2.5x, so a texture pixel is
        /// two and a half of these.
        /// </summary>
        private const float CloseLift = 90f;

        // Everyone else's far layer fades out as this one fades in. Same shape as
        // ExampleMod's and Calamity's; the game gives us the whole array and the
        // speed and expects the style to do its own bookkeeping.
        public override void ModifyFarFades(float[] fades, float transitionSpeed)
        {
            for (int i = 0; i < fades.Length; i++)
            {
                if (i == Slot)
                {
                    fades[i] += transitionSpeed;
                    if (fades[i] > 1f)
                        fades[i] = 1f;
                }
                else
                {
                    fades[i] -= transitionSpeed;
                    if (fades[i] < 0f)
                        fades[i] = 0f;
                }
            }
        }

        public override int ChooseFarTexture()
            => BackgroundTextureLoader.GetBackgroundSlot(Mod, Folder + "WastedMileFar");

        public override int ChooseMiddleTexture()
            => BackgroundTextureLoader.GetBackgroundSlot(Mod, Folder + "WastedMileMiddle");

        // a and b are the game's own placement: top = (view height fraction) * a + b.
        // b is the flat offset, so taking from it raises the layer.
        public override int ChooseCloseTexture(ref float scale, ref double parallax, ref float a, ref float b)
        {
            b -= CloseLift;
            return BackgroundTextureLoader.GetBackgroundSlot(Mod, Folder + "WastedMileClose");
        }
    }
}
