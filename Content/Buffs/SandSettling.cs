using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// After Corosion's sandstep, by request ("with a cool down of 15 secs"): the right-click cannot be used again
    /// until it is gone. A debuff the Nurse cannot take off, as the game's own cooldowns are. Placeholder picture
    /// (PlaceholderTextures.md).
    /// </summary>
    public class SandSettling : ModBuff
    {
        /// <summary>Fifteen seconds.</summary>
        public const int Ticks = 15 * 60;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }
    }
}
