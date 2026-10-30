using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Back Rocket (by request: "Player shoots a swaying rocket (based on vanilla textures make one of your own) that deals
    /// 80 damage every 2 secs; when it detects an NPC it homes onto it. Only attack when it detects NPC."). Worn: the launcher
    /// on the back (TentochiLauncher; hidden with the accessory's visibility, firing all the same) and its rockets
    /// (TentochiPlayer.Watch, TentochiRocket). Out of hardmode crates, and the Cyborg's (TentochiSources).
    /// </summary>
    public class BackRocket : TentochiItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 26;
            Item.accessory = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(gold: 30);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            TentochiPlayer tentochi = player.GetModPlayer<TentochiPlayer>();
            tentochi.Launcher = Item;
            tentochi.LauncherShown = !hideVisual;
        }
    }
}
