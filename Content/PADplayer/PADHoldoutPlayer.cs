using PisAndDag.Content.Projectiles.Base;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// Keeps every idle holdout in existence.
    ///
    /// Calamity runs this out of their own player class; this is the one line of it that
    /// matters, on its own so nothing else has to know about it. A holdout that is not
    /// spawned by using an item has to be spawned by something, and this is the something.
    /// </summary>
    public class PADHoldoutPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            BaseIdleHoldoutProjectile.CheckForEveryHoldout(Player);
        }
    }
}
