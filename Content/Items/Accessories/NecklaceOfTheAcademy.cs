using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Necklace Of The Academy, by request: "(first hint at the Havu Lainey Magic Academy) has a bool
    /// BasicNecklace in globalplayer, +1 slot in arsenal, +5% magic damage. Obtained 1/15 chance from
    /// dark caster."
    ///
    /// Worn: PADPlayer.BasicNecklace, which opens one more slot of the Magic Arsenal (MagicArsenal:
    /// the first past those earned, marked with a small picture of the necklace), and 5% more magic
    /// damage. Where it comes from: the Dark Caster, 1 in 15 (<see cref="NecklaceOfTheAcademySource"/>).
    ///
    /// The academy is not named anywhere in the game yet; the necklace's last tooltip line is its
    /// first hint, and gives no more than two letters of it (Localization). Mine: Green rarity, as the
    /// dungeon's other finds; its picture is a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class NecklaceOfTheAcademy : ModItem
    {
        /// <summary>One in this many Dark Casters drops one.</summary>
        public const int DropChance = 15;

        /// <summary>5% more magic damage.</summary>
        public const float MagicDamage = 0.05f;

        /// <summary>Beside the mod's other accessories in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 1);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.PAD().BasicNecklace = true;
            player.GetDamage(DamageClass.Magic) += MagicDamage;
        }

        // Recipes stay commented out for now; none was asked for. It drops from the Dark Caster (below).
    }

    /// <summary>Where the necklace comes from: the Dark Caster, 1 in 15, in every mode.</summary>
    public class NecklaceOfTheAcademySource : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.DarkCaster)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NecklaceOfTheAcademy>(), NecklaceOfTheAcademy.DropChance));
        }
    }
}
