using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.MegaShark
{
    /// <summary>
    /// How the Megashark is called: you shoot the ocean with its ancestor.
    ///
    /// Twenty rounds from a Minishark into the sea, once the three machines are down
    /// and with no boss about, and it comes up under you. Any bullet; what counts is
    /// the gun and the water. A round is counted when it goes wet in the ocean, so
    /// firing along the beach does nothing. At ten there is one line, so a player who
    /// stumbled into it knows the sea noticed.
    ///
    /// Counted on the side that spawns things — singleplayer, or the server — from
    /// the projectile's own owner, so the count and the spawn agree. Not saved: it is
    /// a thing you do in one sitting.
    /// </summary>
    public class MegasharkCallProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>Fired from a Minishark.</summary>
        private bool minishark;

        /// <summary>Already counted, so a round that keeps travelling wet counts once.</summary>
        private bool counted;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            minishark = source is EntitySource_ItemUse_WithAmmo use && use.Item?.type == ItemID.Minishark;
        }

        public override void PostAI(Projectile projectile)
        {
            if (!minishark || counted || !projectile.wet)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!Main.player.IndexInRange(projectile.owner))
                return;

            counted = true;
            Main.player[projectile.owner].GetModPlayer<MegasharkCaller>().Round(projectile.Center);
        }
    }

    public class MegasharkCaller : ModPlayer
    {
        public const int RoundsToCall = 20;
        private const int RoundsToNotice = 10;

        private static readonly Color SeaColour = new Color(96, 132, 168);

        private int rounds;

        /// <summary>A Minishark round has gone into the water. Only the ocean's water counts.</summary>
        public void Round(Vector2 where)
        {
            if (!Player.ZoneBeach)
                return;

            if (!Ready())
                return;

            rounds++;

            if (rounds == RoundsToNotice)
            {
                Say("Something below turned to look.");
                return;
            }

            if (rounds < RoundsToCall)
                return;

            rounds = 0;

            // It sinks itself below this point on its first tick to stage the entrance
            // (see Megashark). Server-side in multiplayer; NewNPC syncs it.
            NPC.NewNPC(new EntitySource_WorldEvent(), (int)Player.Center.X, (int)Player.Center.Y,
                ModContent.NPCType<Megashark>());
        }

        /// <summary>The three machines are down, it is not already here, and nothing else is.</summary>
        private static bool Ready()
        {
            if (!NPC.downedMechBoss1 || !NPC.downedMechBoss2 || !NPC.downedMechBoss3)
                return false;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.boss || npc.type == NPCID.EaterofWorldsHead || npc.type == ModContent.NPCType<Megashark>())
                    return false;
            }

            return true;
        }

        /// <summary>One line to the one who fired, wherever they are.</summary>
        private void Say(string line)
        {
            if (Main.netMode == NetmodeID.Server)
                ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(line), SeaColour, Player.whoAmI);
            else
                Main.NewText(line, SeaColour);
        }

        public override void OnEnterWorld()
        {
            rounds = 0;
        }
    }
}
