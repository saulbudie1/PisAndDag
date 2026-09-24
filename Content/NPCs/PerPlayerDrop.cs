using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// A drop every player who fought gets their own of: one stack, seen and picked up only
    /// by the player it is for, all of them falling where the NPC died.
    ///
    /// Vanilla has the pieces but not this rule. Both of its per-player drops
    /// (DropPerPlayerOnThePlayer, and the treasure bag's DropLocalPerClientAndResetsNPCMoneyTo0)
    /// set the NPC's value to zero on the way out, and item drops run before the money does
    /// (NPC.NPCLoot), so in classic mode either one would cost the kill its coins. This is the
    /// bag's instanced drop (CommonCode.DropItemLocalPerClientAndSetNPCMoneyTo0) without that
    /// last line.
    ///
    /// Singleplayer: one ordinary stack. Server: one item, sent as an instanced item to every
    /// active player who hit the NPC, then cleared from the server's own list, which is how
    /// vanilla hands out treasure bags. Rolled once, so everyone gets the same count.
    /// </summary>
    public class PerPlayerDrop : CommonDrop
    {
        /// <summary>How long the item's slot stays reserved for the players it went to, as vanilla has it for bags.</summary>
        private const int SlotHeld = 54000;

        public PerPlayerDrop(int itemId, int least, int most) : base(itemId, 1, least, most, 1)
        {
        }

        public override ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
        {
            int stack = info.rng.Next(amountDroppedMinimum, amountDroppedMaximum + 1);
            NPC npc = info.npc;

            if (Main.netMode == NetmodeID.Server)
            {
                int index = Item.NewItem(npc.GetSource_Loot(), (int)npc.position.X, (int)npc.position.Y,
                    npc.width, npc.height, itemId, stack, true, -1, false, false);

                Main.timeItemSlotCannotBeReusedFor[index] = SlotHeld;

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (Main.player[i].active && npc.playerInteraction[i])
                        NetMessage.SendData(MessageID.InstancedItem, i, -1, null, index);
                }

                Main.item[index].active = false;
            }
            else
            {
                CommonCode.DropItem(info, itemId, stack);
            }

            return new ItemDropAttemptResult { State = ItemDropAttemptResultState.Success };
        }
    }
}
