using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.UI;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Kill bookkeeping for the mod's bosses and the flight announcement, and the dialogue
    /// bubble. What the Guide says is GuideHints.
    /// </summary>
    public class PADNPCs : GlobalNPC
    {
        private const string FlightUnlockText = "Terrarians are no longer bounded to the ground.";

        private static readonly Color FlightUnlockColor = new Color(230, 76, 160);

        /// <summary>
        /// Announces the flight unlock the first time the Empress of Light falls.
        ///
        /// Gated on PADWorld.FlightAnnounced rather than on NPC.downedEmpressOfLight,
        /// because whether vanilla has set that flag by the time OnKill runs is an
        /// ordering detail — depending on it would either announce twice or never. Our own
        /// flag answers the question directly, and is rebuilt on load so a world that had
        /// already beaten her stays quiet.
        /// </summary>
        public override void OnKill(NPC npc)
        {
            if (npc.type == ModContent.NPCType<MegaShark.Megashark>() && !PADWorld.DownedMegashark)
            {
                // OnKill runs where kills are decided: singleplayer, or the server. The
                // world-data packet carries the flag to everyone else.
                PADWorld.DownedMegashark = true;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.WorldData);
            }

            if (npc.type == ModContent.NPCType<Abomanation_Small.AbomanationHead>() && !PADWorld.DownedAbomanation)
            {
                PADWorld.DownedAbomanation = true;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.WorldData);
            }

            if (npc.type != NPCID.HallowBoss || PADWorld.FlightAnnounced)
                return;

            PADWorld.FlightAnnounced = true;

            // The server does the talking in multiplayer; a client saying it too would
            // print the line twice for whoever is hosting.
            if (Main.netMode == NetmodeID.Server)
            {
                ChatHelper.BroadcastChatMessage(
                    NetworkText.FromLiteral(FlightUnlockText), FlightUnlockColor);
            }
            else if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Main.NewText(FlightUnlockText, FlightUnlockColor);
            }
        }

        /// <summary>The bubble over anything Dialogue knows how to talk to.</summary>
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (Dialogue.IsSpeaker(npc.type))
                Dialogue.DrawBubble(npc, spriteBatch, screenPos);
        }
    }
}
