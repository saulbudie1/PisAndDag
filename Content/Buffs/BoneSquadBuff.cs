using PisAndDag.Content.Items.Summoner;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Bone Squad's skeletons are out (BoneSquadMinion): as long as any of the player's are, it
    /// lasts; when none are left it goes, and cancelling it sends them away (each lives only while
    /// its owner has this). As the game's own minion buffs are: no time shown, not saved.
    ///
    /// Its icon is the game's own (by request, "texture from the game files"): the skull of the
    /// Baby Skeletron Head's (mine).
    /// </summary>
    public class BoneSquadBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.BabySkeletronHead;

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            bool any = false;
            for (int i = 0; i < Main.maxProjectiles && !any; i++)
                any = BoneSquad.Member(Main.projectile[i], player.whoAmI);

            if (any)
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
