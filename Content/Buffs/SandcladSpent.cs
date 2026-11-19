using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>Sandclad's key used: neither set's can be used again until this is gone. Its picture Sand Settling's, for now.</summary>
    public class SandcladSpent : ModBuff
    {
        public override string Texture => "PisAndDag/Content/Buffs/SandSettling";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }
    }
}
