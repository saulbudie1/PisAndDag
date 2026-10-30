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

        /// <summary>
        /// Uses the off hand's item, its primary use only (OffHandPlayer). Registered under the name the wind's test
        /// bind had ("Wind"), so the button a player set for that carries over (by request: "replace keybind to use
        /// offhand's primary with whichever mouse is using to change wind"). The wind is stepped by "/wind" now.
        /// </summary>
        public static ModKeybind OffHand { get; private set; }

        /// <summary>Moves the memorised cast selection: Up to the next, Down to the previous. Only while a catalyst is held.</summary>
        public static ModKeybind SpellUp { get; private set; }
        public static ModKeybind SpellDown { get; private set; }

        /// <summary>Switches a worn M.U.P.R.A.L between its glow and its beam (MupralPlayer). L, as no key of the game's is.</summary>
        public static ModKeybind Lantern { get; private set; }

        /// <summary>
        /// Lets a worn Calamity Module's adrenaline go when it is full (TentochiPlayer). X: no key of the game's, nor of
        /// Calamity's (whose own adrenaline is B).
        /// </summary>
        public static ModKeybind Adrenaline { get; private set; }

        /// <summary>Lets a worn Adam Kit I go (TentochiPlayer.Detonate). K: no key of the game's, nor of Calamity's, and away from the hand that moves.</summary>
        public static ModKeybind Detonate { get; private set; }

        public override void Load()
        {
            Reload = KeybindLoader.RegisterKeybind(Mod, "Reload", "R");
            Lightning = KeybindLoader.RegisterKeybind(Mod, "Lightning", "P");
            OffHand = KeybindLoader.RegisterKeybind(Mod, "Wind", "O");
            SpellUp = KeybindLoader.RegisterKeybind(Mod, "SpellUp", "Up");
            SpellDown = KeybindLoader.RegisterKeybind(Mod, "SpellDown", "Down");
            Lantern = KeybindLoader.RegisterKeybind(Mod, "Lantern", "L");
            Adrenaline = KeybindLoader.RegisterKeybind(Mod, "Adrenaline", "X");
            Detonate = KeybindLoader.RegisterKeybind(Mod, "Detonate", "K");
        }

        public override void Unload()
        {
            Reload = null;
            Lightning = null;
            OffHand = null;
            SpellUp = null;
            SpellDown = null;
            Lantern = null;
            Adrenaline = null;
            Detonate = null;
        }
    }
}
