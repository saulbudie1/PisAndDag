using System;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// A slime mount comes up through water faster and faster, by request ("slimy saddle raise
    /// speed is exponential and keep that potential until he pops out of the water", then
    /// "applies for all water" and "same with gelatinous pillion"): the Slimy Saddle's slime and
    /// the Gelatinous Pillion's, the two the game counts as slime mounts
    /// (Mount.IsConsideredASlimeMount), in water anywhere; not in honey, lava or shimmer.
    ///
    /// The game floats a slime mount up through whatever it is in (Player.Update, 1.4.4): half a
    /// pixel a tick more each tick, and never faster than 4 a tick. Here, once the game has it
    /// going up, each tick it goes up faster by a share of itself: twice as fast every third of
    /// a second, from the game's 4 to 32 in a second and some 26 rows of water, and at that
    /// all the rest of the way up. What it has when it comes out of the water it keeps, a jump
    /// under way that would hold it to a jump's speed ended; from there the game's gravity takes
    /// it, as anything thrown up.
    ///
    /// 32 is eight times the game's, and the fastest the game moves anybody through water without
    /// their passing through a block: it looks for blocks no further than a row over a player's
    /// head (Collision.TileCollision), and in water moves a player half as far as their speed
    /// (Player.WaterCollision), so at 32 a tick a player goes up a row a tick, never past one
    /// unseen. Out of the water the game moves a player in steps of 16 pixels at the most
    /// (Player.DryCollision), whatever their speed.
    ///
    /// All of it before the game moves the player (PreUpdateMovement: after the game's own float,
    /// its gravity and its jump, before anything is moved).
    /// </summary>
    public class SlimeRise : ModPlayer
    {
        /// <summary>Two to the twentieth root: a tick's growth, twice as fast every 20 ticks.</summary>
        private const float Growth = 1.0352649f;

        /// <summary>The fastest, in pixels a tick.</summary>
        private const float Fastest = 32f;

        /// <summary>How fast it was going up after it moved last tick, in the water; nothing out of it.</summary>
        private float carried;

        /// <summary>On a slime mount in water.</summary>
        private bool InWater => Player.mount.Active && Player.mount.IsConsideredASlimeMount
            && Player.wet && !Player.lavaWet && !Player.honeyWet && !Player.shimmerWet;

        public override void PreUpdateMovement()
        {
            if (InWater)
            {
                if (Player.velocity.Y >= 0f)
                    return;

                // Going up by the game's float (4 a tick at the most): from there, faster by a
                // share of itself each tick.
                float up = Math.Max(-Player.velocity.Y, carried * Growth);

                Player.velocity.Y = -Math.Min(up, Fastest);
                return;
            }

            if (carried <= 0f)
                return;

            // Just come out of the water: what it had it keeps, less this tick's pull. A jump under
            // way sets a player's speed to a jump's every tick while it lasts (Player.JumpMovement):
            // ended, if it is the slower.
            float kept = carried - Player.gravity;

            if (kept > -Player.velocity.Y)
                Player.velocity.Y = -kept;

            if (kept > Player.jumpSpeed)
                Player.jump = 0;
        }

        public override void PostUpdate()
        {
            carried = InWater && Player.velocity.Y < 0f ? -Player.velocity.Y : 0f;
        }
    }
}
