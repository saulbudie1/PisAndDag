using PisAndDag.Content.Projectiles.Mage;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// The first incantation. A ball of fire, thrown, that falls and bursts. Held, it
    /// gathers into something bigger; see <see cref="MeagreFlameHold"/>.
    ///
    /// Dropped by Fire Imps (<see cref="MeagreFlameSource"/>).
    /// </summary>
    public class MeagreFlame : Castable
    {
        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 40;
        public override int Mana => 18;
        public override int UseTime => 60;
        public override bool Channel => true;
        public override int Shot => ModContent.ProjectileType<MeagreFlameHold>();
        public override string CastType => "Evocation";
        public override string Element => "Flame";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 46;
            Item.height = 53;
            Item.rare = ItemRarityID.Green;
        }
    }

    /// <summary>Where it comes from: a Fire Imp, one kill in twenty.</summary>
    public class MeagreFlameSource : GlobalNPC
    {
        /// <summary>One Fire Imp in this many drops it.</summary>
        public const int ImpChance = 20;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.FireImp)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<MeagreFlame>(), ImpChance));
        }
    }
}
