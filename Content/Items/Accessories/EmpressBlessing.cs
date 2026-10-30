using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// The Empress' Blessing (by request: "Make Empress of light drop a legends exclusive accessory: Empress' Blessing dash 20
    /// tiles and have invincibility during that dash. It also have the ability to increase summoner's slot by 1 and magic
    /// arsenal slot by 1."). Worn: the 20 tile dash, nothing able to hurt its wearer while it goes (AccessoryDash); one more
    /// minion slot; one more Magic Arsenal slot, held open as the Necklace Of The Academy holds one (PADPlayer.EmpressBlessing,
    /// MagicArsenal.BlessingSlot).
    ///
    /// Only in a Legends world: from the Empress herself where she drops no bag, in her bag where she does (both
    /// <see cref="LegendsOnly"/>), every time. A "Legends" line under its tooltip, in her colours going round.
    /// </summary>
    public class EmpressBlessing : ModItem
    {
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 10);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.maxMinions += 1;
            player.PAD().EmpressBlessing = true;
            player.GetModPlayer<AccessoryDash>().Wear(AccessoryDash.Kind.Empress);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            float turn = Main.GlobalTimeWrappedHourly * 0.35f;
            tooltips.Add(new TooltipLine(Mod, "Legends", Language.GetTextValue("Mods.PisAndDag.Conditions.LegendsTag"))
            {
                OverrideColor = Color.Lerp(EmpressArt.Prism(turn), Color.White, 0.25f),
            });
        }
    }

    /// <summary>A drop only in a Legends world (PADWorld.Legends), as the world is when it drops.</summary>
    public class LegendsOnly : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        public bool CanDrop(DropAttemptInfo info) => PADWorld.Legends;

        public bool CanShowItemDropInUI() => true;

        public string GetConditionDescription() => Language.GetTextValue("Mods.PisAndDag.Conditions.Legends");
    }

    /// <summary>The Empress' Blessing from the Empress herself, where she drops no bag (Classic), in a Legends world.</summary>
    public class EmpressBlessingDrop : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.HallowBoss;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<EmpressBlessing>()));
            npcLoot.Add(classic);
        }
    }

    /// <summary>The Empress' Blessing in her bag (Expert and Master), in a Legends world.</summary>
    public class EmpressBlessingBag : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.FairyQueenBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<EmpressBlessing>()));
        }
    }
}
