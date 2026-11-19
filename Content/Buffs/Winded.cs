using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// After Scuttlebutt's barrage (ScuttlebuttBarrage), by request ("8 second cool down"): the right-click cannot be used
    /// again until it is gone, counted from when the barrage ends. A debuff the Nurse cannot take off, as the game's own
    /// cooldowns are (SandSettling's way). Placeholder picture (PlaceholderTextures.md), drawn in code.
    /// </summary>
    public class Winded : ModBuff
    {
        /// <summary>Eight seconds.</summary>
        public const int Ticks = 8 * 60;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }
    }
}
