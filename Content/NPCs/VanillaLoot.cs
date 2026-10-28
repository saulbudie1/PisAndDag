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
    /// the archer; not the scout, who comes before any army); and Mycelium, Finish Them from anything
    /// killed in the mushroom caves underground, one in MyceliumFinishThem.DropChance (by request: "it
    /// needs to be dropped by any npc within underground mushroom biome with a 1/8 chance"; it was the
    /// Spore Zombies' alone, one in 25). FairyRingPass still puts one in a chest there.
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

            // Dancing Sand, by request: from the Antlion (the one in the sand), the Antlion Charger and the Giant Antlion Charger.
            if (npc.type is NPCID.Antlion or NPCID.WalkingAntlion or NPCID.GiantWalkingAntlion)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Mage.Spell.DancingSand>(), Items.Mage.Spell.DancingSand.DropChance));

        }

        public override void ModifyGlobalLoot(GlobalLoot globalLoot)
        {
            globalLoot.Add(ItemDropRule.ByCondition(new InMushroomCaves(), ModContent.ItemType<Items.Mage.Incantation.MyceliumFinishThem>(),
                Items.Mage.Incantation.MyceliumFinishThem.DropChance));
        }
    }

    /// <summary>
    /// Killed in the mushroom caves: the killer in the glowing mushroom biome, the kill under the surface. Not a
    /// critter, a friend, a town NPC or one from a statue (the game's own rules for what drops a biome's things).
    /// </summary>
    public class InMushroomCaves : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info)
        {
            NPC npc = info.npc;

            return npc != null && info.player != null && info.player.ZoneGlowshroom && npc.position.Y > Main.worldSurface * 16.0
                && npc.lifeMax > 5 && !npc.friendly && !npc.townNPC && !npc.SpawnedFromStatue && !NPCID.Sets.CountsAsCritter[npc.type];
        }

        public bool CanShowItemDropInUI() => true;

        public string GetConditionDescription() => "Killed in the mushroom caves underground";
    }
}
