using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Jelle's sand is out (SableConstruct): as long as any of the player's is, it lasts; when none is left it goes,
    /// and cancelling it calls the sand back. As the game's own minion buffs are: no time shown, not saved. The
    /// texture is a placeholder (PlaceholderTextures.md).
    /// </summary>
    public class JellesSableHelperBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<SableConstruct>()] > 0)
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
