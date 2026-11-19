using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>Nightcall's charge spent (Nightcall.Cooldown): it cannot be charged again until this is gone. Its picture drawn in code (scratchpad nightcall/sprites.py).</summary>
    public class NightcallSpent : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }
    }
}
