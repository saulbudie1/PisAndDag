using System;
using System.Linq;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Legends;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for Skeletron's rework (LegendsSkeletron), whose moves come at random: this asks for one by name.
    ///
    ///   /skeletron           what the nearest Skeletron is doing, and the moves there are
    ///   /skeletron &lt;move&gt;    that move next (Legends), as soon as he is hovering: one of his hands' while he has the hands
    ///                        for it, one of his own once both are gone
    ///   /skeletron hands     both his hands die (his loss of them, then his own moves)
    ///   /skeletron red       the red hat on him, or off
    ///
    /// A World command: run where the world is decided, the server in multiplayer, so it works from any player's chat.
    /// </summary>
    public class SkeletronCommand : ModCommand
    {
        private static readonly Color Said = new Color(214, 206, 170);

        public override CommandType Type => CommandType.World;

        public override string Command => "skeletron";

        public override string Usage => "/skeletron [move|hands|red]";

        public override string Description => "Skeletron: what he is doing, a move of his by name next, his hands killed, or the red hat on or off.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            NPC head = Nearest(caller.Player);

            if (head == null || !head.TryGetGlobalNPC(out LegendsSkeletron state))
            {
                caller.Reply("No Skeletron about.", Color.OrangeRed);
                return;
            }

            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "":
                    caller.Reply(state.Describe(head) + " Moves: " + string.Join(", ", LegendsSkeletron.MoveNames()) + ".", Said);
                    return;

                case "hands":
                {
                    int killed = 0;

                    foreach (NPC hand in Main.ActiveNPCs)
                    {
                        if (hand.type != NPCID.SkeletronHand || (int)hand.ai[1] != head.whoAmI)
                            continue;

                        hand.dontTakeDamage = false;
                        hand.StrikeInstantKill();
                        killed++;
                    }

                    caller.Reply(killed > 0 ? $"His hands: {killed} killed." : "He has no hands left.", Said);
                    return;
                }

                case "red":
                    state.redHat = !state.redHat;
                    head.netUpdate = true;
                    caller.Reply(state.redHat ? "The red hat on him." : "The red hat off him.", Said);
                    return;

                default:
                {
                    string refused = state.Ask(what);
                    caller.Reply(refused ?? $"{what}: next, as soon as he is hovering.", refused == null ? Said : Color.OrangeRed);
                    return;
                }
            }
        }

        private static NPC Nearest(Player player)
        {
            NPC best = null;
            float far = float.MaxValue;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type != NPCID.SkeletronHead)
                    continue;

                float d = player != null ? Vector2.DistanceSquared(npc.Center, player.Center) : 0f;

                if (d < far)
                {
                    far = d;
                    best = npc;
                }
            }

            return best;
        }
    }
}
