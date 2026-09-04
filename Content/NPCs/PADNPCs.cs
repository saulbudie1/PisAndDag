using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    public class PADNPCs : GlobalNPC

    {

        public override void OnChatButtonClicked(NPC npc, bool firstButton)
        {
            if (npc.type == NPCID.Guide)
            {
                if (firstButton)
                {
                    npc.StrikeInstantKill();
                }
            }
        }
        public override bool PreChatButtonClicked(NPC npc, bool firstButton)
        {

            if (npc.type == NPCID.Guide)
            {
                if (firstButton)
                {
                    npc.StrikeInstantKill();
                }
            }
            return base.PreChatButtonClicked(npc, firstButton);
        }
    }
}
