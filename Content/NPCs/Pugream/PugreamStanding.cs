using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.NPCs.Pugream
{
    /// <summary>
    /// Pugream where the player can walk up to him: standing on the shore, looking at
    /// the water, after the Eye. What he says is PugreamTalk's; where and when he stands
    /// is PugreamPresence's. This is only the man standing.
    ///
    /// He does not move. He looks out to sea, and when somebody comes close enough to
    /// talk he turns to them, and when they go he turns back.
    /// </summary>
    public class PugreamStanding : PugreamBody
    {
        /// <summary>Closer than this and he turns to whoever it is.</summary>
        private const float TurnRange = 260f;

        /// <summary>Horizontal dead zone before he turns round, so he does not twitch on the line.</summary>
        private const float TurnDeadZone = 12f;

        public override void AI()
        {
            NPC.velocity.X = 0f;

            Player near = null;
            float best = TurnRange;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead)
                    continue;

                float distance = Vector2.Distance(player.Center, NPC.Center);

                if (distance < best)
                {
                    best = distance;
                    near = player;
                }
            }

            if (near != null)
            {
                if (Math.Abs(near.Center.X - NPC.Center.X) > TurnDeadZone)
                    Facing = Math.Sign(near.Center.X - NPC.Center.X);
            }
            else
            {
                // Out to sea: the world's edge on the side he is standing.
                Facing = NPC.Center.X < Main.maxTilesX * 8f ? -1 : 1;
            }

            NPC.direction = Facing;
            NPC.spriteDirection = Facing;
        }
    }
}
