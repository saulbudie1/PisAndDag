using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    public class PADNPCs : GlobalNPC
    {
        private static string GuideLine = "I am sorry, " + Main.player[Main.myPlayer].name + ", but I can only help you with crafting right now, I can not tell you anything else";
         
        public override void Load()
        {
            On_ShopHelper.GetShoppingSettings += ForceGuideHappinessLine;
        }

        public override void Unload()
        {
            On_ShopHelper.GetShoppingSettings -= ForceGuideHappinessLine;
        }

        private static ShoppingSettings ForceGuideHappinessLine(On_ShopHelper.orig_GetShoppingSettings orig, ShopHelper self, Player player, NPC npc)
        { 
            ShoppingSettings settings = orig(self, player, npc);

            if (npc.type == NPCID.Guide)
                settings.HappinessReport = GuideLine;

            return settings;
        }
        public override bool PreChatButtonClicked(NPC npc, bool firstButton)
        { 
            if (npc.type == NPCID.Guide && firstButton)
            {
                Main.npcChatText = GuideLine;
                return false;   
            } 
            return base.PreChatButtonClicked(npc, firstButton);
        }
         
        public override void GetChat(NPC npc, ref string chat)
        {
            if (npc.type == NPCID.Guide)
                chat = GuideLine;
        }
    }
}
