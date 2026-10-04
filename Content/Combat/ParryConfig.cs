using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// The parry's lists, for whoever runs the world: what the rules get wrong about some
    /// other mod's enemy or shot is put right here, by name, without a line of code.
    ///
    /// The rules class an enemy by its own numbers (ParryRules), and a shot by what it is.
    /// Five lists overrule them: enemies never parried; enemies parried whatever their size
    /// or rank (light or heavy by their weight alone); enemies parried but never staggered
    /// (one whose AI does not survive being stopped); shots that are solid; shots that never
    /// are. A thing on a "never" list is never parried, whatever else it is on.
    ///
    /// The world's: the server's is everyone's (ConfigScope.ServerSide), since what a parry
    /// does to a thing is done where the thing is simulated. Read once everything is loaded
    /// (other mods' names mean nothing before that) and again whenever it is changed. /parry
    /// says what the rules make of the enemy under the cursor, and why.
    /// </summary>
    public class ParryConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        public List<NPCDefinition> NeverParried = new List<NPCDefinition>();

        public List<NPCDefinition> ParriedAnyway = new List<NPCDefinition>();

        public List<NPCDefinition> NeverStaggered = new List<NPCDefinition>();

        public List<ProjectileDefinition> SolidShots = new List<ProjectileDefinition>();

        public List<ProjectileDefinition> NeverSolidShots = new List<ProjectileDefinition>();

        public override void OnChanged()
        {
            Read();
        }

        /// <summary>The five lists, as types, into the parry's own.</summary>
        internal void Read()
        {
            Parry.Lists lists = Parry.Chosen;

            lists.Clear();

            Enemies(NeverParried, lists.Never);
            Enemies(ParriedAnyway, lists.Anyway);
            Enemies(NeverStaggered, lists.NoStagger);
            Shots(SolidShots, lists.Solid);
            Shots(NeverSolidShots, lists.NoShot);
        }

        private static void Enemies(List<NPCDefinition> named, HashSet<int> types)
        {
            if (named == null)
                return;

            foreach (NPCDefinition one in named)
            {
                if (one == null || one.IsUnloaded)
                    continue;

                // A definition is a net ID: under none it is one of vanilla's sizes of a type,
                // and the type is what the lists go by.
                int type = one.Type;

                if (type < 0 && ContentSamples.NpcsByNetId.TryGetValue(type, out NPC sample) && sample != null)
                    type = sample.type;

                if (type > NPCID.None)
                    types.Add(type);
            }
        }

        private static void Shots(List<ProjectileDefinition> named, HashSet<int> types)
        {
            if (named == null)
                return;

            foreach (ProjectileDefinition one in named)
            {
                if (one == null || one.IsUnloaded)
                    continue;

                int type = one.Type;

                if (type > ProjectileID.None)
                    types.Add(type);
            }
        }
    }
}
