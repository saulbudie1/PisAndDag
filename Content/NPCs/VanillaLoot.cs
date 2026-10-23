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
    ///
    /// And two of the mod's own, by request (PADWorkFlow §16.98): the Mantrap from the goblins of
    /// an army, one in Mantrap.DropChance each (the peon, the thief, the warrior, the sorcerer and
    /// the archer; not the scout, who comes before any army); Mycelium, Finish Them from the Spore
    /// Zombies, one in MyceliumFinishThem.SporeZombieChance, for a world whose mushroom caves were
    /// laid before it (FairyRingPass puts one in a chest there).
    /// </summary>
    public class VanillaLoot : GlobalNPC
    {
        /// <summary>One in this many Voodoo Demons drops a Molten Charm.</summary>
        public const int MoltenCharmChance = 10;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.VoodooDemon)
                npcLoot.Add(ItemDropRule.Common(ItemID.MoltenCharm, MoltenCharmChance));

            if (npc.type is NPCID.GoblinPeon or NPCID.GoblinThief or NPCID.GoblinWarrior or NPCID.GoblinSorcerer or NPCID.GoblinArcher)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Ranged.Mantrap>(), Items.Ranged.Mantrap.DropChance));

            if (npc.type is NPCID.ZombieMushroom or NPCID.ZombieMushroomHat)
            {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Mage.Incantation.MyceliumFinishThem>(),
                    Items.Mage.Incantation.MyceliumFinishThem.SporeZombieChance));
            }
        }
    }
}
