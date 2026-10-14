using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// The Vulture Staff's vultures are out (VultureMinion): as long as any of the player's are, it lasts; when
    /// none are left it goes, and cancelling it sends them away (each vulture lives only while its owner has
    /// this). As the game's own minion buffs are: no time shown, not saved.
    ///
    /// The texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class VultureStaffBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<VultureMinion>()] > 0)
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
