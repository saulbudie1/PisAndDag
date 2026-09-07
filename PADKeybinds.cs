using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Keybinds appear under Settings, Controls, Mod Controls — rebindable there, which
    /// is where players expect to change them.
    /// </summary>
    public class PADKeybinds : ModSystem
    {
        public static ModKeybind Reload { get; private set; }

        /// <summary>Test bind: throws an arc from the player to the cursor.</summary>
        public static ModKeybind Lightning { get; private set; }

        public override void Load()
        {
            Reload = KeybindLoader.RegisterKeybind(Mod, "Reload", "R");
            Lightning = KeybindLoader.RegisterKeybind(Mod, "Lightning", "P");
        }

        public override void Unload()
        {
            Reload = null;
            Lightning = null;
        }
    }
}
