using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// The Worminator's worm is out (WorminatorSegments.cs): as long as its head is, it lasts; when the worm is
    /// gone it goes, and cancelling it sends the worm away (each segment lives only while its owner has this).
    /// As the game's own minion buffs are: no time shown, not saved.
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class WorminatorBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<WorminatorHead>()] > 0)
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
