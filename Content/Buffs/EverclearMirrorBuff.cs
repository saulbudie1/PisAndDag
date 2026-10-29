using PisAndDag.Content.Mimic;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// The Everclear Mirror's copy is out (MimicSystem): as long as its anchor is (MimicAnchor), it lasts; when the
    /// anchor goes it goes, and cancelling it sends the copy away. As the game's own minion buffs are: no time shown,
    /// not saved.
    /// </summary>
    public class EverclearMirrorBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<MimicAnchor>()] > 0 && MimicSystem.Exists)
            {
                player.buffTime[buffIndex] = 18000;
            }
            else
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}
