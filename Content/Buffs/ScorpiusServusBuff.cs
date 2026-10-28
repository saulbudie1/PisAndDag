using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Scorpius Servus's tails are out (ScorpiusTail): as long as any of the player's are, it lasts; when none are
    /// left it goes, and cancelling it takes them back. As the game's own minion buffs are: no time shown, not
    /// saved. The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class ScorpiusServusBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<ScorpiusTail>()] > 0)
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
