using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Per-NPC mod data, reached with npc.PAD().
    ///
    /// Instanced, so every NPC gets its own copy and a field written here belongs to that
    /// one body. That is the whole reason to have it — PADNPCs above is a singleton and
    /// exists for hooks, and a field on a singleton would silently be shared by every
    /// enemy in the world, which is the kind of bug that only shows up once two of the
    /// same type are on screen.
    ///
    /// Empty for now on purpose. This is the place per-enemy state goes when the first
    /// piece of gear needs it — debuff stacks, marks, how long since something last hit
    /// this body — rather than a static dictionary keyed by whoAmI, which breaks the
    /// moment an NPC slot is reused.
    /// </summary>
    public class PADGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;
    }
}
