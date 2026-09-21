using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace PisAndDag
{
    // Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class PisAndDag : Mod
    {
        public override void Load()
        {
            // Screen filters are a client-side drawing concern; a dedicated server has no
            // graphics device to register them against.
            if (Main.dedServ)
                return;

            LoadLegendsFilter();
            LoadWastedMileFilter();
            LoadMorsCloak();
        }

        // RequestAssetIfExists, not Assets.Request. Request records the asset in the
        // repository even when it fails, and tModLoader re-throws that later from
        // Mod.TransferAllAssets() — so a try/catch at the call site does not help.
        // This asks first and registers nothing when the file is absent.
        //
        // tModLoader does NOT compile .fx during a mod build. Every .fx under Effects/
        // is source only: run it through fxcompiler or EasyXnb and put the resulting
        // .xnb beside it. Until that exists, the mod loads without that effect and
        // logs a line saying so.

        private void LoadLegendsFilter()
        {
            if (!RequestAssetIfExists("Effects/LegendsShader", out Asset<Effect> legends))
            {
                Logger.Warn(
                    "Legends screen filter disabled: Effects/LegendsShader.xnb is missing. " +
                    "Compile Effects/LegendsShader.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            // "LegendsPass" must match the pass name in the .fx technique block.
            Filters.Scene[LegendsScreenSystem.FilterKey] = new Filter(
                new ScreenShaderData(legends, "LegendsPass"),
                EffectPriority.High);

            Filters.Scene[LegendsScreenSystem.FilterKey].Load();

            // Only now is it safe for the driver system to touch Filters.Scene.
            LegendsScreenSystem.Registered = true;
        }

        /// <summary>
        /// The grade over the Wasted Mile. The biome switches it on and off through
        /// Player.ManageSpecialBiomeVisuals, which needs the key to exist in
        /// Filters.Scene first; WastedMileBiome.FilterReady is what tells it that it does.
        /// </summary>
        private void LoadWastedMileFilter()
        {
            if (!RequestAssetIfExists("Effects/WastedMile", out Asset<Effect> mile))
            {
                Logger.Warn(
                    "Wasted Mile screen filter disabled: Effects/WastedMile.xnb is missing. " +
                    "Compile Effects/WastedMile.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            // "MilePass" must match the pass name in the .fx technique block. Intensity
            // is the overall strength; the fade in and out is the filter's own opacity.
            Filters.Scene[Content.Biomes.WastedMileBiome.FilterKey] = new Filter(
                new ScreenShaderData(mile, "MilePass").UseIntensity(0.85f),
                EffectPriority.High);

            Filters.Scene[Content.Biomes.WastedMileBiome.FilterKey].Load();

            Content.Biomes.WastedMileBiome.FilterReady = true;
        }

        /// <summary>
        /// The sway on Mors's cloak. A sprite shader, applied by MorsIdle itself at draw
        /// time — nothing to register, it only has to exist. Missing means a still cloak.
        /// </summary>
        private void LoadMorsCloak()
        {
            if (!RequestAssetIfExists("Effects/MorsCloak", out Asset<Effect> cloak))
            {
                Logger.Warn(
                    "Mors cloak sway disabled: Effects/MorsCloak.xnb is missing. " +
                    "Compile Effects/MorsCloak.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            Content.NPCs.Mors.MorsIdle.CloakEffect = cloak;
        }
    }
}
