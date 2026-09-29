using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// A dash goes level. Tabi and the Master Ninja Gear, the Shield of Cthulhu, the
    /// Solar Flare and Crystal Assassin armours, the mounts that dash: while one is under
    /// way the player moves along the ground's line and nothing else, whatever they were
    /// doing before it, and falls again the moment it is spent.
    ///
    /// Vanilla's "dashing" is Player.dashDelay below zero: DashMovement sets it to -1 on
    /// the tick a dash starts and holds it there until the dash has slowed to a run (or
    /// the Shield of Cthulhu has bounced off something), when it becomes the cooldown.
    /// Gravity has been added to the fall by the time PreUpdateMovement runs and the
    /// player has not been moved yet, so taking the fall away here is the whole of it.
    /// </summary>
    public class LevelDash : ModPlayer
    {
        public override void PreUpdateMovement()
        {
            if (Player.dashDelay < 0)
                Player.velocity.Y = 0f;
        }
    }
}
