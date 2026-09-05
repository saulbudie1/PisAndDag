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

            // RequestAssetIfExists, not Assets.Request. Request records the asset in the
            // repository even when it fails, and tModLoader re-throws that later from
            // Mod.TransferAllAssets() — so a try/catch at the call site does not help.
            // This asks first and registers nothing when the file is absent.
            //
            // tModLoader does NOT compile .fx during a mod build. Effects/LegendsShader.fx
            // is source only: run it through fxcompiler or EasyXnb and put the resulting
            // LegendsShader.xnb beside it. Until that exists, the mod loads without the
            // filter and logs the line below.
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
    }
}
