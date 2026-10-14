using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The game's own items, added to what the game's own enemies drop.
    ///
    /// The Voodoo Demon: a Molten Charm, 1 in 10, by request ("Add molten charm in possible drops
    /// by voodoo doll demon. 1/10"). Rolled on its own for every kill, beside what the demon drops
    /// already (its Guide Voodoo Doll, every time), in every mode, and like the doll it falls where
    /// the demon dies.
    /// </summary>
    public class VanillaLoot : GlobalNPC
    {
        /// <summary>One in this many Voodoo Demons drops a Molten Charm.</summary>
        public const int MoltenCharmChance = 10;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.VoodooDemon)
                npcLoot.Add(ItemDropRule.Common(ItemID.MoltenCharm, MoltenCharmChance));
        }
    }
}
