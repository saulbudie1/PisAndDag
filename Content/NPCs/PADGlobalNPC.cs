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
    /// Boss bar fields are set from the boss's own SetDefaults, one line each:
    ///
    ///     NPC.PAD().BossName  = "Megashark";
    ///     NPC.PAD().BossTitle = "Built in a shed. Never signed.";
    ///     NPC.PAD().OwnIntro  = true;     // only if the boss animates its own arrival
    ///
    /// No save, no sync: SetDefaults runs wherever the NPC is built, including on a
    /// client that received it over the network, so the fields rebuild themselves.
    /// The globals are instantiated before ModNPC.SetDefaults runs (tML's
    /// GlobalLoaderUtils.SetDefaults), which is what makes writing them from there safe.
    /// </summary>
    public class PADGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>
        /// Gives this NPC a boss bar with this name on it. Null or empty means no bar.
        /// Never withheld from the player — see BossPlate.
        /// </summary>
        public string BossName;

        /// <summary>
        /// The line under the name during the arrival only — it rises and flies with the
        /// name and is gone when the name touches the bar. Null means the name goes alone.
        /// Read live, so a boss can change it mid-fight by assigning it again.
        /// </summary>
        public string BossTitle;

        /// <summary>
        /// True when this boss animates its own arrival, so BossIntro's universal one
        /// stays out of the way. Such a boss calls BossIntro.PlayNameOnly itself if it
        /// wants the name and title to land on the bar the same way.
        /// </summary>
        public bool OwnIntro;
    }
}
