using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for Foigh, the Swordsman, who has nowhere of his own to be met yet.
    ///
    ///   /foigh          puts him down for whoever typed it: on level ground forty-six tiles
    ///                   ahead of them, his back to them, his sword in the ground. His road
    ///                   starts by itself from there.
    ///   /foigh gone     takes him away again, and his sword and knives with him.
    ///
    /// A World command: run where the world is decided, the server in multiplayer, so it works
    /// from any player's chat.
    /// </summary>
    public class FoighCommand : ModCommand
    {
        private static readonly Color Done = new Color(255, 196, 92);

        public override CommandType Type => CommandType.World;

        public override string Command => "foigh";

        public override string Usage => "/foigh [gone]";

        public override string Description => "Foigh, the Swordsman: put him down ahead of you, or take him away.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "":
                {
                    string refused = Content.NPCs.Foigh.Foigh.Summon(caller.Player);

                    if (refused != null)
                    {
                        caller.Reply(refused, Color.OrangeRed);
                        return;
                    }

                    caller.Reply("Somebody is standing a way off, with his back to you.", Done);
                    return;
                }

                case "gone":
                {
                    int taken = 0;

                    foreach (NPC npc in Main.ActiveNPCs)
                    {
                        if (npc.ModNPC is not Content.NPCs.Foigh.Foigh)
                            continue;

                        npc.active = false;
                        npc.netSkip = -1;
                        npc.life = 0;
                        taken++;

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                    }

                    foreach (Projectile projectile in Main.ActiveProjectiles)
                    {
                        if (projectile.ModProjectile is Content.NPCs.Foigh.FoighSwordThrown
                            || projectile.ModProjectile is Content.NPCs.Foigh.FoighDagger
                            || projectile.ModProjectile is Content.NPCs.Foigh.FoighReturned)
                            projectile.Kill();
                    }

                    caller.Reply(taken > 0 ? "He is gone." : "He was not here.", Done);
                    return;
                }

                default:
                    caller.Reply("Usage: " + Usage, Color.OrangeRed);
                    return;
            }
        }
    }
}
