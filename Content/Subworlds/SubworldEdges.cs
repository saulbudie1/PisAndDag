using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// No creature at a place's very edge, by report ("also some errors": the game's "Index was
    /// outside the bounds of the array"; its log has it in Collision.StepUp, from
    /// NPC.AI_007_TownEntities, in the Vast Ocean: a sea turtle gone off the edge of the sea). The
    /// game keeps a player forty tiles in from a world's edge (Player.BordersMovement) and its
    /// camera as far (it shows nothing nearer the edge than that), but nothing keeps its creatures
    /// off the last tiles, and the walkers among them look at the tile ahead of them before asking
    /// whether it is in the world at all (Collision.StepUp reads Main.tile there first). Here a
    /// creature that comes within <see cref="Edge"/> tiles of a subworld's edge, where nobody can
    /// be or see, is not let move (its AI does not run), so that nothing reads past the edge; and
    /// whoever runs the place (the server, or the one player's own game) takes it away. A boss, or
    /// a townsperson, is put back inside instead.
    /// </summary>
    public class SubworldEdges : GlobalNPC
    {
        /// <summary>How near the edge, in tiles, a creature may come.</summary>
        private const int Edge = 20;

        public override bool PreAI(NPC npc)
        {
            if (!Subworld.Active)
                return true;

            float left = Edge * 16f, right = (Main.maxTilesX - Edge) * 16f;
            float top = Edge * 16f, bottom = (Main.maxTilesY - Edge) * 16f;

            if (npc.position.X >= left && npc.position.X + npc.width <= right && npc.position.Y >= top && npc.position.Y + npc.height <= bottom)
                return true;

            if (npc.boss || npc.townNPC || NPCID.Sets.ShouldBeCountedAsBoss[npc.type])
            {
                if (npc.position.X < left && npc.velocity.X < 0f || npc.position.X + npc.width > right && npc.velocity.X > 0f)
                    npc.velocity.X = 0f;

                if (npc.position.Y < top && npc.velocity.Y < 0f || npc.position.Y + npc.height > bottom && npc.velocity.Y > 0f)
                    npc.velocity.Y = 0f;

                npc.position.X = MathHelper.Clamp(npc.position.X, left, right - npc.width);
                npc.position.Y = MathHelper.Clamp(npc.position.Y, top, bottom - npc.height);
                return true;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }

            return false;
        }
    }
}
