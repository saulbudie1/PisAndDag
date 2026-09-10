using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles
{
    /// <summary>
    /// Per-projectile mod data, reached with projectile.PAD().
    ///
    /// Instanced for the same reason as PADGlobalNPC: a field here belongs to one shot.
    /// This is where a projectile carries something its ai[] slots have no room for — a
    /// source item, a flag saying it came from a set bonus, a stack that should not reset
    /// when the projectile is recycled into a different type.
    ///
    /// Anything stored here is LOCAL. ai[] rides along in the spawn packet and this does
    /// not, so a field that other clients must agree on either goes in ai[] or gets
    /// written and read through SendExtraAI / ReceiveExtraAI.
    /// </summary>
    public class PADGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
    }
}
