using PisAndDag.Content.Items.Accessories.Tentochi;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// The Calamity Module's adrenaline let go (by request: "Adrenaline mode for 5 seconds basically multiply all damage by
    /// 2.5x"): while it lasts, everything of the wearer's that hits does 2.5 times its damage (TentochiPlayer). A buff, so
    /// the others in the world know of it as the game tells them of any, and see it.
    /// </summary>
    public class AdrenalineRush : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<TentochiPlayer>().Surging = true;
        }
    }
}
