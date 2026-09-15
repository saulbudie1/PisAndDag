using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Mechanic's answer line: offered while the player is carrying a Dented Megashark,
    /// and it opens the repair bench. The bench itself is MegasharkRepairUI; the line is
    /// one TalkOptions registration.
    /// </summary>
    public class MechanicTalk : ModSystem
    {
        public override void PostSetupContent()
        {
            TalkOptions.Register(NPCID.Mechanic, new TalkOption(
                TalkOptions.Icon(ItemID.SharkFin) + "About this Dented Megashark...",
                npc => MegasharkRepairUI.Show(npc.whoAmI),
                (player, npc) => player.HasItem(ModContent.ItemType<DentedMegashark>())));
        }
    }
}
