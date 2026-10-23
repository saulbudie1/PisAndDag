using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Deep
{
    /// <summary>
    /// Seized by a Crevice Eel's bite (CreviceEelHead.OnHitPlayer), by request ("make their
    /// movement more creepy and predatorial. research eel hunting mechanic."): a moray's second
    /// jaws, in its throat, take what it bites and pull it back. The bitten is held to the head
    /// for <see cref="HoldTicks"/> ticks, going where it goes (back to its crevice's mouth as it
    /// recoils, there let go: CreviceEelHead.LetsGo; or shaken as it shakes them), unless the eel
    /// is gone, they are torn more than <see cref="TearTiles"/> tiles from it, or the rock stops
    /// them (they are not pressed into it). Run on the bitten one's own machine (the game asks
    /// the eel's bite there, and only there: PlayerLoader.OnHurt, for the local player), where
    /// their moving is decided: each tick just before they move (ModPlayer.PreUpdateMovement),
    /// their speed set to keep them at its jaws (twice what it would be out of the water, as the
    /// game moves a player in water half their speed: Player.WaterCollision).
    /// </summary>
    public class CreviceEelSeized : ModPlayer
    {
        public const int HoldTicks = 30;
        public const float TearTiles = 6f;

        /// <summary>The fastest it pulls, pixels a tick (as the speed is set; in water, half that moved): a little more than twice the head's going back into its crevice (CreviceEelHead.BackSpeed).</summary>
        private const float MostPull = 12f;

        private int by = -1, byId, left;
        private Vector2 jawsBefore;

        /// <summary>Where they were before the last pull, how far it meant to take them, and how many ticks running the rock has stopped them.</summary>
        private Vector2 wasAt, meant;
        private int stopped;

        /// <summary>Held by this eel (its slot and its number, CreviceEelHead.Id), from now.</summary>
        public void Seize(NPC eel, int id)
        {
            by = eel.whoAmI;
            byId = id;
            left = HoldTicks;
            jawsBefore = eel.Center;
            meant = Vector2.Zero;
            stopped = 0;
        }

        public override void PreUpdateMovement()
        {
            if (left <= 0)
                return;

            NPC eel = by >= 0 && by < Main.maxNPCs ? Main.npc[by] : null;

            if (--left <= 0 || eel == null || !eel.active || eel.ModNPC is not CreviceEelHead head || head.Id != byId || head.LetsGo || Player.dead)
            {
                LetGo();
                return;
            }

            Vector2 jaws = eel.Center;
            Vector2 to = jaws - Player.Center;

            if (to.Length() > TearTiles * 16f)
            {
                LetGo();
                return;
            }

            // Held against the rock (the last pull took them less than a third of the way it
            // meant to, three ticks running): torn free, not pressed into it.
            if (meant.LengthSquared() > 1f && Vector2.Distance(Player.position, wasAt) < meant.Length() / 3f)
            {
                if (++stopped >= 3)
                {
                    LetGo();
                    return;
                }
            }
            else
            {
                stopped = 0;
            }

            // As far as the jaws went since, and a third of the way still between.
            float through = Player.wet && !Player.merman && !Player.ignoreWater && !Player.trident ? 2f : 1f;
            Vector2 pull = (jaws - jawsBefore + to * 0.3f) * through;

            if (pull.Length() > MostPull)
                pull = pull.SafeNormalize(Vector2.Zero) * MostPull;

            Player.velocity = pull;
            jawsBefore = jaws;
            wasAt = Player.position;
            meant = pull / through;
        }

        /// <summary>Let go: the hold over, and most of the pull's speed with it, so that they are not flung on past where it left them.</summary>
        private void LetGo()
        {
            left = 0;

            if (meant.LengthSquared() > 0f)
                Player.velocity *= 0.25f;

            meant = Vector2.Zero;
        }
    }
}
