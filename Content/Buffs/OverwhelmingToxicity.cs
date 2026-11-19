using PisAndDag.Content.Biomes.Sludge;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Overwhelming Toxicity, from standing in toxic sludge (by request: "-3HP a sec"; and "if you dipped in the toxic
    /// sludge, you -20 HP a sec"): 3 life a second, 20 while their head is under it (SludgePlayer takes it). Kept three
    /// seconds after they come out. Placeholder picture (PlaceholderTextures.md), drawn in code.
    /// </summary>
    public class OverwhelmingToxicity : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>
    /// Stench, from going under toxic sludge (by request: "a permanent debuff Stench, making every thing from the shop
    /// 10x time more expensive. debuff is cleared only when you submerged fully in water"). It never runs out: it is
    /// kept through death and saved with the player, and the Nurse cannot take it; only going under clean water does
    /// (SludgePlayer). Shops: StenchShops. Placeholder picture (PlaceholderTextures.md), drawn in code.
    /// </summary>
    public class Stench : ModBuff
    {
        /// <summary>What its time is kept at (it is not shown).</summary>
        private const int Kept = 18000;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = false;
            Main.persistentBuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.buffTime[buffIndex] = Kept;
            player.GetModPlayer<SludgePlayer>().Stinks = true;
        }
    }
}
