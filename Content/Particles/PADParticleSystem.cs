using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// Wires PADParticleHandler into the game loop: allocation, texture registration,
    /// the per-tick update, and the draw hook.
    /// </summary>
    public class PADParticleSystem : ModSystem
    {
        public override void Load()
        {
            PADParticleHandler.Load();

            if (!Main.dedServ)
            {
                // Particles draw at the dust layer — above tiles, NPCs and projectiles,
                // which is where blood and smoke belong. Running after orig() means
                // vanilla has already closed its own SpriteBatch, so the handler is free
                // to open its own.
                On_Main.DrawDust += DrawParticlesAfterDust;
            }
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                On_Main.DrawDust -= DrawParticlesAfterDust;

            PADParticleHandler.Unload();
        }

        /// <summary>
        /// Texture registration has to wait until every type in the mod is loadable,
        /// which Load() is too early for.
        /// </summary>
        public override void PostSetupContent()
        {
            PADParticleHandler.LoadParticleTextures(Mod);
        }

        public override void PostUpdateEverything()
        {
            PADParticleHandler.Update();
        }

        private static void DrawParticlesAfterDust(On_Main.orig_DrawDust orig, Main self)
        {
            orig(self);
            PADParticleHandler.DrawAll(Main.spriteBatch);
        }
    }
}
